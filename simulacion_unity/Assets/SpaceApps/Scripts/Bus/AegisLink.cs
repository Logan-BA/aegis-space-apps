using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SpaceApps.Bus
{
    public class AegisAstronaut
    {
        public string Id, Name;
        public double? DoseMsv, SleepH, BoneRaw, AerobicRaw;
        public double? BoneBase, AerobicBase;
        public int BaseDay = int.MaxValue;
        public readonly SortedDictionary<int, (float dose, float sleep, float bone, float aerobic)> History =
            new SortedDictionary<int, (float, float, float, float)>();

        /// <summary>Densidad ósea como % del valor inicial (acepta %, fracción o unidades absolutas).</summary>
        public float? BonePct => Percent(BoneRaw, BoneBase);
        public float? AerobicPct => Percent(AerobicRaw, AerobicBase);

        static float? Percent(double? v, double? baseline)
        {
            if (v == null) return null;
            double x = v.Value;
            if (x > 0 && x <= 1.2) return (float)(x * 100);          // fracción
            if (x >= 50 && x <= 110) return (float)x;                 // ya viene en %
            if (baseline != null && baseline.Value > 0) return (float)(x / baseline.Value * 100);
            return null;
        }
    }

    public class AegisAlert
    {
        public string Text, Level, Source, AstronautId;
        public bool IsRed;
    }

    /// <summary>
    /// Enlace con el bus AEGIS: lee la configuración (StreamingAssets/aegis_bus.json), recibe mensajes
    /// del motor y mantiene el último estado conocido (día, escenario, fase, tripulación, alertas,
    /// evento solar). No calcula diagnósticos: muestra lo que manda el motor.
    /// </summary>
    public class AegisLink
    {
        public readonly AegisBusClient Client = new AegisBusClient();
        public string Url = "ws://127.0.0.1:8001/ws", Subprotocol = "aegis.v1", Hello = "";
        public int? Day, TotalDays;
        public string Scenario = "", Phase = "", LastType = "", SolarEvent = "";
        public readonly List<AegisAstronaut> Crew = new List<AegisAstronaut>();
        public readonly List<AegisAlert> Alerts = new List<AegisAlert>();
        public string LastRaw = "", LastParseError = "";
        public int SolarEventCounter;
        public bool Changed;

        Dictionary<string, object> _cfg = new Dictionary<string, object>();
        Dictionary<string, List<string>> _paths = new Dictionary<string, List<string>>();
        Dictionary<string, string> _scenarioScenes = new Dictionary<string, string>(), _phaseScenes = new Dictionary<string, string>();
        List<string> _redLevels = new List<string>();
        string _lastSolarKey = "";

        public static string ConfigPath => Path.Combine(Application.streamingAssetsPath, "aegis_bus.json");

        public AegisLink() => LoadConfig();

        public void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                _cfg = MiniJson.Parse(File.ReadAllText(ConfigPath)) as Dictionary<string, object> ?? new Dictionary<string, object>();
                Url = MiniJson.Text(MiniJson.Path(_cfg, "url")) ?? Url;
                Subprotocol = MiniJson.Text(MiniJson.Path(_cfg, "subprotocolo")) ?? Subprotocol;
                Hello = MiniJson.Text(MiniJson.Path(_cfg, "mensaje_inicial")) ?? "";
                _paths.Clear();
                if (MiniJson.Path(_cfg, "rutas") is Dictionary<string, object> rutas)
                    foreach (var kv in rutas)
                        _paths[kv.Key] = (kv.Value as List<object>)?.Select(o => o?.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();
                _scenarioScenes = Dict("escenas_por_escenario");
                _phaseScenes = Dict("escenas_por_fase");
                _redLevels = (MiniJson.Path(_cfg, "niveles_rojos") as List<object>)?.Select(o => o.ToString().ToLowerInvariant()).ToList() ?? new List<string>();
            }
            catch (System.Exception e) { LastParseError = "Configuración: " + e.Message; }
        }

        Dictionary<string, string> Dict(string key)
        {
            var d = new Dictionary<string, string>();
            if (MiniJson.Path(_cfg, key) is Dictionary<string, object> o)
                foreach (var kv in o) d[kv.Key.ToLowerInvariant()] = kv.Value?.ToString();
            return d;
        }

        List<string> P(string key) => _paths.TryGetValue(key, out var l) ? l : new List<string> { key };

        public void Connect() => Client.Connect(Url, Subprotocol, Hello);
        public void Disconnect() => Client.Disconnect();

        /// <summary>Procesa los mensajes pendientes. Llamar desde el hilo principal.</summary>
        public void Poll(int max = 50)
        {
            for (int i = 0; i < max && Client.TryDequeue(out var msg); i++) Ingest(msg);
        }

        public void Ingest(string msg)
        {
            LastRaw = msg;
            object root;
            try { root = MiniJson.Parse(msg); LastParseError = ""; }
            catch (System.Exception e) { LastParseError = "Mensaje no es JSON válido: " + e.Message; return; }

            LastType = MiniJson.Text(MiniJson.First(root, P("tipo"), false)) ?? LastType;
            var day = MiniJson.Number(MiniJson.First(root, P("dia")));
            if (day != null) Day = (int)day.Value;
            var total = MiniJson.Number(MiniJson.First(root, P("dias_totales")));
            if (total != null) TotalDays = (int)total.Value;
            Scenario = MiniJson.Text(NameOf(MiniJson.First(root, P("escenario")))) ?? Scenario;
            Phase = MiniJson.Text(NameOf(MiniJson.First(root, P("fase"), false))) ?? Phase;

            // Tripulación: lista de objetos; si no hay, el mensaje entero describe a un solo astronauta
            var crewNode = MiniJson.First(root, P("tripulacion"), false);
            if (crewNode is List<object> list)
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i] is Dictionary<string, object> a) UpdateAstronaut(a, i);
            }
            else if (crewNode is Dictionary<string, object> map && map.Values.All(v => v is Dictionary<string, object>))
            {
                int i = 0;
                foreach (var kv in map) UpdateAstronaut((Dictionary<string, object>)kv.Value, i++, kv.Key);
            }
            else if (root is Dictionary<string, object> single && MiniJson.First(single, P("dosis_msv")) != null)
                UpdateAstronaut(single, 0);

            var alertsNode = MiniJson.First(root, P("alertas"), false);
            if (alertsNode is List<object> al)
            {
                Alerts.Clear();
                foreach (var item in al)
                {
                    var a = new AegisAlert();
                    if (item is Dictionary<string, object> ad)
                    {
                        a.Text = MiniJson.Text(MiniJson.First(ad, P("alerta_texto"), false)) ?? Short(ad);
                        a.Level = MiniJson.Text(MiniJson.First(ad, P("alerta_nivel"), false)) ?? "";
                        a.Source = MiniJson.Text(NameOf(MiniJson.First(ad, P("alerta_fuente"), false))) ?? "";
                        a.AstronautId = MiniJson.Text(MiniJson.First(ad, P("alerta_astronauta"), false));
                    }
                    else a.Text = MiniJson.Text(item);
                    a.IsRed = _redLevels.Any(r => (a.Level ?? "").ToLowerInvariant().Contains(r));
                    Alerts.Add(a);
                }
            }

            var solar = MiniJson.First(root, P("evento_solar"), false);
            if (solar != null && !(solar is bool b && !b))
            {
                string key = solar is Dictionary<string, object> sd ? Short(sd) : MiniJson.Text(solar);
                if (key != _lastSolarKey) { _lastSolarKey = key; SolarEventCounter++; }
                SolarEvent = key;
            }
            else SolarEvent = "";
            Changed = true;
        }

        void UpdateAstronaut(Dictionary<string, object> a, int index, string keyId = null)
        {
            string id = MiniJson.Text(MiniJson.First(a, P("id"), false)) ?? keyId ?? index.ToString();
            var ast = Crew.FirstOrDefault(c => c.Id == id);
            if (ast == null) { ast = new AegisAstronaut { Id = id }; Crew.Add(ast); }
            ast.Name = MiniJson.Text(MiniJson.First(a, P("nombre"), false)) ?? ast.Name ?? id;
            ast.DoseMsv = MiniJson.Number(MiniJson.First(a, P("dosis_msv"))) ?? ast.DoseMsv;
            ast.SleepH = MiniJson.Number(MiniJson.First(a, P("sueno_h"))) ?? ast.SleepH;
            ast.BoneRaw = MiniJson.Number(MiniJson.First(a, P("densidad_osea"))) ?? ast.BoneRaw;
            ast.AerobicRaw = MiniJson.Number(MiniJson.First(a, P("capacidad_aerobica"))) ?? ast.AerobicRaw;
            int day = Day ?? 0;
            if (day < ast.BaseDay) { ast.BaseDay = day; ast.BoneBase = ast.BoneRaw; ast.AerobicBase = ast.AerobicRaw; }
            ast.History[day] = ((float)(ast.DoseMsv ?? 0), (float)(ast.SleepH ?? 0), ast.BonePct ?? 0f, ast.AerobicPct ?? 0f);
        }

        static object NameOf(object v)
        {
            if (v is Dictionary<string, object> d)
                foreach (var k in new[] { "name", "nombre", "label", "id", "title" })
                    if (d.TryGetValue(k, out var x)) return x;
            return v;
        }

        static string Short(Dictionary<string, object> d) =>
            string.Join(", ", d.Take(4).Select(kv => $"{kv.Key}: {MiniJson.Text(kv.Value as string ?? (kv.Value is double ? kv.Value : null) ?? "…")}"));

        /// <summary>Escena 3D que corresponde al escenario y la fase recibidos.</summary>
        public string SceneKey()
        {
            string phase = (Phase ?? "").ToLowerInvariant();
            foreach (var kv in _phaseScenes) if (phase.Contains(kv.Key)) return kv.Value;
            string sc = (Scenario ?? "").ToLowerInvariant();
            string mapped = null;
            foreach (var kv in _scenarioScenes) if (sc.Contains(kv.Key)) { mapped = kv.Value; break; }
            if (mapped == "mars")
            {
                // Sin fase explícita: tránsito de ida, superficie y regreso según el avance de la misión
                float total = TotalDays ?? 900f, f = (Day ?? 0) / Mathf.Max(1f, total);
                return f < 0.23f || f > 0.77f ? "deep_space_mars" : "mars_surface";
            }
            return mapped ?? "deep_space_mars";
        }
    }
}
