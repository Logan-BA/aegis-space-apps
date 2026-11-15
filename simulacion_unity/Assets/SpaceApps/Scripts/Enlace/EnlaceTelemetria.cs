using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpaceApps.Assistant;
using SpaceApps.Bus;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SpaceApps.Enlace
{
    /// <summary>
    /// Transmisor de telemetría de la nave hacia las estaciones terrenas (WebSocket, binario).
    /// Cada "intervalo_s" arma el estado de la misión (tripulación, cabina, alertas, enlace), lo cifra
    /// (solo quien tiene la clave puede leerlo), lo protege contra errores y lo envía por un canal con ruido
    /// espacial simulado. Si una estación no está conectada, guarda las tramas y las envía al reconectar
    /// (almacenar y reenviar). Configuración: StreamingAssets/enlace/enlace.json.
    /// </summary>
    public class EnlaceTelemetria : MonoBehaviour
    {
        public bool Activo;
        public float Intervalo = 1f;
        public bool RuidoCosmico = true, PicosSeu = true, Comprimir = true;
        public float RetrasoEscala;                 // 0 = sin esperar el retraso real de la luz
        public string Nave = "AEGIS-1";
        public string Estado = "";
        public int TramasEnviadas, TramasPerdidasCanal;
        public readonly EnlaceCanal Canal = new EnlaceCanal();

        public class Estacion
        {
            public string Url;
            public volatile bool Online;
            public int Enviadas;
            public string Error = "";
            public readonly ConcurrentQueue<(double at, byte[] data)> Cola = new ConcurrentQueue<(double, byte[])>();
        }
        public readonly List<Estacion> Estaciones = new List<Estacion>();

        SpaceAppsSimulation _app;
        byte[] _kEnc, _kMac;
        uint _session, _seq;
        float _timer;
        readonly System.Random _rng = new System.Random();
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        const int MaxCola = 600;

        void Start()
        {
            _app = GetComponent<SpaceAppsSimulation>();
            string dir = Path.Combine(Application.streamingAssetsPath, "enlace");
            var urls = new List<string>();
            string keyFile = "clave_nasa.key";
            try
            {
                string cfgPath = Path.Combine(dir, "enlace.json");
                if (File.Exists(cfgPath) && MiniJson.Parse(File.ReadAllText(cfgPath)) is Dictionary<string, object> cfg)
                {
                    Activo = MiniJson.Path(cfg, "activo") is bool a && a;
                    if (MiniJson.Path(cfg, "estaciones") is List<object> l) urls.AddRange(l.Select(o => o.ToString()));
                    Intervalo = Mathf.Max(0.2f, (float)(MiniJson.Number(MiniJson.Path(cfg, "intervalo_s")) ?? Intervalo));
                    if (MiniJson.Path(cfg, "ruido_cosmico") is bool r) RuidoCosmico = r;
                    if (MiniJson.Path(cfg, "picos_seu") is bool p) PicosSeu = p;
                    if (MiniJson.Path(cfg, "comprimir") is bool c) Comprimir = c;
                    RetrasoEscala = (float)(MiniJson.Number(MiniJson.Path(cfg, "retraso_luz_escala")) ?? 0);
                    Nave = MiniJson.Text(MiniJson.Path(cfg, "nave")) ?? Nave;
                    keyFile = MiniJson.Text(MiniJson.Path(cfg, "clave")) ?? keyFile;
                }
                // -estacion ws://IP:8765 (se puede repetir) agrega estaciones y activa el enlace
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-estacion") { urls.Add(args[i + 1]); Activo = true; }
                if (args.Contains("-sinenlace")) Activo = false;
                if (!Activo) { Estado = "Enlace apagado"; return; }

                string keyPath = Path.IsPathRooted(keyFile) ? keyFile : Path.Combine(dir, keyFile);
                if (!File.Exists(keyPath))
                {
                    // Primera ejecución (la clave nunca se publica): se crea una nueva para esta nave.
                    // Hay que copiarla a cada estación terrena autorizada.
                    Directory.CreateDirectory(Path.GetDirectoryName(keyPath));
                    File.WriteAllText(keyPath, "# Clave maestra AEGIS (AES-256 + HMAC-SHA256). SECRETA: copiar solo a las estaciones autorizadas.\n" +
                                               EnlaceProtocolo.NewMasterKeyHex() + "\n");
                    Debug.Log("[Enlace] clave nueva creada: " + keyPath + " (cópiela a receptor_python y receptor_rust)");
                }
                (_kEnc, _kMac) = EnlaceProtocolo.DeriveKeys(EnlaceProtocolo.LoadMasterKey(keyPath));
            }
            catch (Exception e)
            {
                Activo = false;
                Estado = "Enlace sin clave: " + e.Message;
                Debug.LogWarning("[Enlace] " + Estado);
                return;
            }

            var b = new byte[4];
            new System.Random().NextBytes(b);
            _session = BitConverter.ToUInt32(b, 0);
            foreach (var url in urls.Distinct())
            {
                var st = new Estacion { Url = url };
                Estaciones.Add(st);
                Task.Run(() => Run(st, _cts.Token));
            }
            Estado = $"Transmitiendo a {Estaciones.Count} estación(es)";
            Debug.Log($"[Enlace] activo · sesión {_session:X8} · {string.Join(", ", urls)}");
        }

        void OnDestroy() => _cts.Cancel();

        void Update()
        {
            if (!Activo || _app == null || _app.Sim == null || _app.CurrentMode != SpaceAppsSimulation.Mode.Local) return;
            bool storm = _app.SpeFlash > 0.05f;
            Canal.Update(_app.Sim, storm);
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = Intervalo;
            Transmit(storm);
        }

        void Transmit(bool storm)
        {
            string json = BuildJson(_app.Sim, storm);
            uint seq = _seq++;
            long tMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            byte[] frame = EnlaceProtocolo.BuildFrame(json, _kEnc, _kMac, _session, seq, tMs, Comprimir);
            byte[] fec = EnlaceProtocolo.FecEncode(frame);
            double at = Clock.Elapsed.TotalSeconds + Math.Min(30.0, Canal.LightSeconds * RetrasoEscala);
            foreach (var st in Estaciones)
            {
                // cada estación recibe su propia copia con su propio ruido
                if (RuidoCosmico && _rng.NextDouble() < Canal.LossProb) { TramasPerdidasCanal++; continue; }
                var copy = (byte[])fec.Clone();
                if (RuidoCosmico) Canal.ApplyNoise(copy, _rng);
                st.Cola.Enqueue((at, copy));
                while (st.Cola.Count > MaxCola) st.Cola.TryDequeue(out _);
            }
            TramasEnviadas++;
        }

        async Task Run(Estacion st, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                using (var ws = new ClientWebSocket())
                {
                    try
                    {
                        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                        await ws.ConnectAsync(new Uri(st.Url), ct).ConfigureAwait(false);
                        st.Online = true; st.Error = "";
                        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                        {
                            if (st.Cola.TryPeek(out var item) && item.at <= Clock.Elapsed.TotalSeconds)
                            {
                                await ws.SendAsync(new ArraySegment<byte>(item.data), WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
                                st.Cola.TryDequeue(out _);
                                st.Enviadas++;
                            }
                            else await Task.Delay(25, ct).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e) { st.Error = e.InnerException?.Message ?? e.Message; }
                    st.Online = false;
                }
                try { await Task.Delay(3000, ct).ConfigureAwait(false); } catch { }
            }
        }

        // ------------------------------------------------------------ contenido (JSON)

        string BuildJson(Simulation sim, bool storm)
        {
            var j = new StringBuilder(2048);
            j.Append('{');
            S(j, "esquema", "aegis.telemetria.v1"); S(j, "nave", Nave);
            S(j, "mision", MissionData.Missions[sim.MissionIndex].Label);
            N(j, "dia", sim.Day); N(j, "hora", sim.Hour); N(j, "dia_total", sim.TotalDays);
            S(j, "fase", sim.Current.Label); S(j, "fase_clave", sim.CurrentKey);
            N(j, "gravedad_g", sim.Current.Gravity); N(j, "radiacion_msv_dia", sim.Current.DoseRate);
            N(j, "limite_dosis_msv", 600);
            N(j, "tormentas_solares", sim.SpeCount); B(j, "tormenta_activa", storm);
            B(j, "mision_terminada", sim.Finished);

            var env = sim.Env;
            j.Append("\"cabina\":{");
            N(j, "co2_mmhg", env.MeanCo2); N(j, "co2_max_mmhg", env.MaxCo2); N(j, "pm25", env.MeanPm25);
            N(j, "o2_pct", env.O2Percent); N(j, "temp_c", env.TempC); N(j, "humedad_pct", env.Humidity); N(j, "ruido_db", env.NoiseDb, last: true);
            j.Append("},");

            var cm = sim.Cm;
            j.Append("\"contramedidas\":{");
            N(j, "ejercicio", cm.Exercise); N(j, "blindaje", cm.Shielding); B(j, "refugio", cm.Shelter); N(j, "depurador", cm.Scrubber);
            N(j, "apoyo", cm.Support); N(j, "luz", cm.Lighting); N(j, "gravedad_artificial_g", cm.ArtificialG, last: true);
            j.Append("},");

            j.Append("\"tripulacion\":[");
            for (int i = 0; i < sim.Crew.Count; i++)
            {
                var h = sim.Crew[i];
                if (i > 0) j.Append(',');
                j.Append('{');
                S(j, "nombre", h.Name); S(j, "rol", h.Role); N(j, "edad", h.Age);
                N(j, "indice_salud", h.HealthIndex); N(j, "hueso_pct", h.Bone); N(j, "musculo_pct", h.Muscle); N(j, "vo2_pct", h.Vo2);
                N(j, "sans", h.Sans); N(j, "dosis_msv", h.DoseMsv); N(j, "riesgo_cancer_pct", h.CancerRiskPercent);
                N(j, "sueno_h", h.SleepHoursLast); N(j, "deuda_sueno_h", h.SleepDebtH); N(j, "estres", h.Stress);
                N(j, "calcio_orina_mg", h.UrinaryCa); B(j, "en_eva", h.OnEva);
                // Signos vitales del día (sensores). Un impacto de radiación en la electrónica (SEU) puede
                // producir un valor absurdo: el receptor debe filtrarlo.
                float fc = h.HeartRate, pas = h.Sbp, pad = h.Dbp, spo2 = h.SpO2, temp = h.CoreTemp;
                if (h.History.Count > 0) { var r = h.History[h.History.Count - 1]; fc = r.HrRest; pas = r.Sbp; pad = r.Dbp; spo2 = r.SpO2; temp = r.CoreTemp; }
                if (PicosSeu && Canal.DistanceKm > 0)
                {
                    float p = 0.02f * (storm ? 6f : 1f) * (Canal.DistanceKm > 70_000 ? 2f : 1f);
                    if (_rng.NextDouble() < p)
                        switch (_rng.Next(5))
                        {
                            case 0: fc = (float)(fc * (1.8 + _rng.NextDouble())); break;
                            case 1: spo2 = (float)(spo2 - 25 - _rng.NextDouble() * 20); break;
                            case 2: temp = (float)(temp + 3 + _rng.NextDouble() * 3); break;
                            case 3: pas = (float)(pas * 1.7); break;
                            default: pad = 0f; break;
                        }
                }
                j.Append("\"vitales\":{");
                N(j, "fc", fc); N(j, "pas", pas); N(j, "pad", pad); N(j, "spo2", spo2); N(j, "temp_c", temp); N(j, "fr", h.RespRate, last: true);
                j.Append("}}");
            }
            j.Append("],");

            j.Append("\"alertas\":[");
            bool first = true;
            foreach (var g in Advisor.Problems(sim).GroupBy(p => p.System))
            {
                var worst = g.OrderByDescending(p => p.Severity).First();
                if (!first) j.Append(',');
                first = false;
                j.Append('{');
                S(j, "sistema", g.Key); S(j, "nivel", worst.Severity >= 0.65f ? "CRITICO" : "PRECAUCION"); N(j, "severidad", worst.Severity);
                j.Append("\"tripulantes\":[").Append(string.Join(",", g.Select(p => OllamaClient.Q(sim.Crew[p.Crew].Name)))).Append("],");
                S(j, "detalle", worst.ProText, last: true);
                j.Append('}');
            }
            j.Append("],");

            j.Append("\"enlace\":{");
            S(j, "via", Canal.Via); N(j, "distancia_km", Canal.DistanceKm); N(j, "distancia_ua", Canal.DistanceAu);
            N(j, "retraso_luz_s", Canal.LightSeconds); N(j, "angulo_sol_grados", Canal.SepDeg); N(j, "ebn0_db", Canal.EbN0Db);
            N(j, "ber", Canal.Ber);
            j.Append("\"fuentes_ruido\":[").Append(string.Join(",", Canal.Sources.Select(OllamaClient.Q))).Append("]}");
            j.Append('}');
            return j.ToString();
        }

        static void S(StringBuilder j, string k, string v, bool last = false) { j.Append('"').Append(k).Append("\":").Append(OllamaClient.Q(v ?? "")); if (!last) j.Append(','); }
        static void B(StringBuilder j, string k, bool v, bool last = false) { j.Append('"').Append(k).Append("\":").Append(v ? "true" : "false"); if (!last) j.Append(','); }
        static void N(StringBuilder j, string k, double v, bool last = false)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) v = 0;
            j.Append('"').Append(k).Append("\":").Append(Math.Abs(v) >= 1e6 || (v != 0 && Math.Abs(v) < 1e-3) ? v.ToString("0.###e0", Ci) : v.ToString("0.###", Ci));
            if (!last) j.Append(',');
        }

        /// <summary>Resumen para la interfaz.</summary>
        public string Resumen()
        {
            if (!Activo) return null;
            int on = Estaciones.Count(e => e.Online);
            string dist = Canal.DistanceKm <= 0 ? "Tierra" : Canal.DistanceAu >= 0.01 ? $"{Canal.DistanceAu:0.00} UA" : $"{Canal.DistanceKm / 1000:0} mil km";
            string luz = Canal.LightSeconds >= 60 ? $"{Canal.LightSeconds / 60:0.0} min luz" : $"{Canal.LightSeconds:0.0} s luz";
            string ber = Canal.Ber > 0 ? $"BER {Canal.Ber:0.0e0}" : "sin errores";
            return $"ENLACE {on}/{Estaciones.Count} · {dist} · {luz} · {ber}";
        }
    }
}
