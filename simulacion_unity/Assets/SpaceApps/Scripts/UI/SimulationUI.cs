using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Interfaz del simulador (IMGUI, sin paquetes) con estética de centro de control:
    /// fondo grafito, acento azul acero y colores de estado sobrios (nominal, precaución, crítico).
    ///  - Vista General: tripulación, alertas prioritarias, mapa corporal, sistemas fisiológicos,
    ///    contramedidas recomendadas y monitor de signos vitales.
    ///  - Vista Profesional: todos los datos en tarjetas, sensores, gráficos y ajuste manual.
    ///  - Modo Bus AEGIS: representa lo que manda el motor del equipo.
    /// </summary>
    [RequireComponent(typeof(SpaceAppsSimulation))]
    public partial class SimulationUI : MonoBehaviour
    {
        // ---------------- paleta
        static readonly Color BarCol = new Color(0.055f, 0.07f, 0.09f, 0.98f);
        static readonly Color PanelCol = new Color(0.07f, 0.085f, 0.11f, 0.95f);
        static readonly Color CardCol = new Color(0.1f, 0.12f, 0.15f, 1f);
        static readonly Color CardHi = new Color(0.13f, 0.17f, 0.22f, 1f);
        static readonly Color Line = new Color(0.2f, 0.24f, 0.3f, 1f);
        static readonly Color Accent = new Color(0.27f, 0.52f, 0.78f);
        static readonly Color Ok = new Color(0.3f, 0.68f, 0.48f), Warn = new Color(0.86f, 0.64f, 0.27f), Danger = new Color(0.84f, 0.32f, 0.27f);
        static readonly Color TextCol = new Color(0.9f, 0.92f, 0.95f), Muted = new Color(0.56f, 0.61f, 0.68f);
        static readonly Color CGreen = Ok, CYellow = Warn, CRed = Danger;

        SpaceAppsSimulation _app;
        GUIStyle _panel, _label, _small, _title, _big, _huge, _center, _btn, _btnSel, _btnGreen, _btnRed, _tabStyle, _caps;
        Texture2D _white, _panelTex;
        ChartTexture _chart;
        VitalMonitor _monitor;
        public bool ShowHelp = true;
        public bool ProView;
        int _tab, _chartKind;
        float _chartTimer, _sensorTimer, _scale = 1f;
        float W, H;
        readonly List<Rect> _blockers = new List<Rect>();

        static readonly string[] Tabs = { "Resumen", "Vitales", "Sangre", "Aire", "Sensores", "Alertas" };
        static readonly string[] Charts = { "Fisiología", "Radiación", "Vitales", "Aire", "Sueño" };

        public void SetTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, Tabs.Length - 1);
            _busTab = Mathf.Clamp(tab, 0, BusTabs.Length - 1);
            if (tab > 0) ProView = true;
        }

        void Awake()
        {
            _app = GetComponent<SpaceAppsSimulation>();
            _chart = new ChartTexture();
            _monitor = new VitalMonitor();
        }

        void Update()
        {
            if (_app == null || _app.Sim == null) return;
            float hr = 70f, rr = 14f;
            if (_app.CurrentMode == SpaceAppsSimulation.Mode.Local)
            {
                var h = _app.Sim.Crew[_app.Selected];
                hr = h.HeartRate; rr = h.RespRate;
            }
            _monitor.Tick(Time.unscaledDeltaTime, hr, rr);
        }

        void InitStyles()
        {
            if (_label != null) return;
            _white = Texture2D.whiteTexture;
            _panelTex = new Texture2D(1, 1); _panelTex.SetPixel(0, 0, PanelCol); _panelTex.Apply();
            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };
            _panel.normal.background = _panelTex;
            _label = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, wordWrap = true };
            _label.normal.textColor = TextCol;
            _small = new GUIStyle(_label) { fontSize = 12 };
            _small.normal.textColor = Muted;
            _caps = new GUIStyle(_label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = false };
            _caps.normal.textColor = Muted;
            _title = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false };
            _big = new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = false };
            _huge = new GUIStyle(_label) { fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = false };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            var slate = new Color(0.16f, 0.2f, 0.26f);
            _btn = ButtonStyle(slate, Accent, 13);
            _btnSel = ButtonStyle(Accent, Accent, 13);
            _btnGreen = ButtonStyle(new Color(0.2f, 0.45f, 0.33f), Accent, 13);
            _btnRed = ButtonStyle(new Color(0.58f, 0.24f, 0.21f), Accent, 13);
            _tabStyle = ButtonStyle(slate, Accent, 12);
            _tabStyle.padding = new RectOffset(3, 3, 4, 4);
        }

        /// <summary>Botón plano con color sólido (onNormal = estado seleccionado).</summary>
        static GUIStyle ButtonStyle(Color baseCol, Color selected, int size)
        {
            var s = new GUIStyle { fontSize = size, richText = true, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            s.padding = new RectOffset(8, 8, 6, 6);
            s.margin = new RectOffset(3, 3, 3, 3);
            s.border = new RectOffset(4, 4, 4, 4);
            s.normal.background = RoundTex(baseCol);
            s.hover.background = RoundTex(Color.Lerp(baseCol, Color.white, 0.1f));
            s.active.background = RoundTex(Color.Lerp(baseCol, Color.black, 0.2f));
            s.onNormal.background = RoundTex(selected);
            s.onHover.background = RoundTex(Color.Lerp(selected, Color.white, 0.1f));
            s.onActive.background = RoundTex(selected);
            s.normal.textColor = s.hover.textColor = s.active.textColor = new Color(0.88f, 0.9f, 0.94f);
            s.onNormal.textColor = s.onHover.textColor = s.onActive.textColor = Color.white;
            return s;
        }

        static Texture2D RoundTex(Color c)
        {
            const int n = 12; const float rad = 3f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(rad - x - 0.5f, x + 0.5f - (n - rad))), dy = Mathf.Max(0f, Mathf.Max(rad - y - 0.5f, y + 0.5f - (n - rad)));
                    float a = Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (_app == null || _app.Sim == null) return;
            InitStyles();
            _scale = Mathf.Max(0.6f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            W = Screen.width / _scale; H = Screen.height / _scale;
            _blockers.Clear();

            if (_app.SpeFlash > 0f) Fill(new Rect(0, 0, W, H), new Color(0.9f, 0.45f, 0.15f, _app.SpeFlash * 0.2f), 0);

            if (_app.CurrentMode == SpaceAppsSimulation.Mode.Bus) DrawBus();
            else
            {
                TopBar(new Rect(0, 0, W, 56));
                var left = new Rect(8, 64, 360, H - 72);
                var right = new Rect(W - 448, 64, 440, H - 72);
                var center = new Rect(left.xMax + 8, 64, right.x - left.xMax - 16, H - 72);
                MissionPath(new Rect(center.x, center.y, center.width, 64));
                CrewTags(center);
                if (ProView) DrawPro(left, right, center); else DrawGeneral(left, right, center);
                if (ShowHelp) HelpOverlay(false);
            }
            Vector2 mouse = Event.current.mousePosition;
            bool block = ShowHelp;
            foreach (var r in _blockers) block |= r.Contains(mouse);
            _app.Orbit.BlockInput = block;
        }

        // ------------------------------------------------------------------ barra superior

        void TopBar(Rect r)
        {
            _blockers.Add(r);
            Fill(r, BarCol, 0);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), Accent, 0);
            var sim = _app.Sim;
            GUI.Label(new Rect(16, 7, 400, 24), "AEGIS  <color=#8F9BAD>|</color>  MONITOR DE SALUD DE TRIPULACIÓN", new GUIStyle(_title) { fontSize = 14 });
            GUI.Label(new Rect(16, 29, 400, 20), $"{MissionData.Missions[sim.MissionIndex].Label} · día {sim.Day} · {sim.Hour:00}:00 h · {sim.Crew.Count} tripulantes", _small);

            GUILayout.BeginArea(new Rect(430, 10, W - 436, 38));
            GUILayout.BeginHorizontal();
            var hgt = GUILayout.Height(32);
            GUILayout.Label("MISIÓN", new GUIStyle(_caps) { alignment = TextAnchor.MiddleRight }, GUILayout.Width(54), hgt);
            if (GUILayout.Button("◀", _btn, GUILayout.Width(30), hgt)) _app.ApplyMission((sim.MissionIndex + MissionData.Missions.Length - 1) % MissionData.Missions.Length);
            GUILayout.Label(MissionData.Missions[sim.MissionIndex].Label, new GUIStyle(_center) { fontSize = 13, wordWrap = false }, GUILayout.Width(170), hgt);
            if (GUILayout.Button("▶", _btn, GUILayout.Width(30), hgt)) _app.ApplyMission((sim.MissionIndex + 1) % MissionData.Missions.Length);
            GUILayout.Space(10);
            if (GUILayout.Button(_app.Running ? "PAUSA" : sim.Finished ? "REPETIR" : "INICIAR", _app.Running ? _btnRed : _btnGreen, GUILayout.Width(96), hgt)) _app.TogglePlay();
            GUILayout.Space(10);
            GUILayout.Label("VELOCIDAD", new GUIStyle(_caps) { alignment = TextAnchor.MiddleRight }, GUILayout.Width(78), hgt);
            foreach (var (label, hps) in new[] { ("2 d/s", 48f), ("8 d/s", 192f), ("30 d/s", 720f) })
                if (GUILayout.Button(label, Mathf.Abs(_app.HoursPerSecond - hps) < 1f ? _btnSel : _btn, GUILayout.Width(58), hgt)) _app.HoursPerSecond = hps;
            GUILayout.Space(10);
            if (GUILayout.Button("GENERAL", !ProView ? _btnSel : _btn, GUILayout.Width(84), hgt)) ProView = false;
            if (GUILayout.Button("PROFESIONAL", ProView ? _btnSel : _btn, GUILayout.Width(110), hgt)) ProView = true;
            GUILayout.Space(10);
            if (GUILayout.Button(_app.InteriorView ? "EXTERIOR" : "INTERIOR", _btn, GUILayout.Width(88), hgt)) _app.ToggleInterior();
            if (GUILayout.Button("AEGIS", _btn, GUILayout.Width(66), hgt)) _app.SetMode(SpaceAppsSimulation.Mode.Bus);
            if (GUILayout.Button("?", _btn, GUILayout.Width(32), hgt)) ShowHelp = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ línea de tiempo de la misión

        static readonly Dictionary<string, (string label, Color color)> LegStyle = new Dictionary<string, (string, Color)>
        {
            ["conditioning"] = ("Preparación", new Color(0.33f, 0.45f, 0.42f)),
            ["earth"] = ("Retorno a la Tierra", new Color(0.33f, 0.45f, 0.42f)),
            ["leo"] = ("Órbita baja", new Color(0.3f, 0.42f, 0.6f)),
            ["moon_transit"] = ("Tránsito", new Color(0.38f, 0.4f, 0.58f)),
            ["moon_surface"] = ("Superficie lunar", new Color(0.5f, 0.5f, 0.52f)),
            ["mars_transit"] = ("Tránsito", new Color(0.38f, 0.4f, 0.58f)),
            ["mars_surface"] = ("Superficie de Marte", new Color(0.62f, 0.42f, 0.3f)),
        };

        void MissionPath(Rect r)
        {
            var sim = _app.Sim;
            _blockers.Add(r);
            Fill(r, PanelCol, 4);
            string sceneLabel = ScenarioBuilder.Labels.TryGetValue(_app.SceneKey, out var sl) ? sl : _app.SceneKey;
            string legName = sim.Legs.Count > 0 && LegStyle.TryGetValue(sim.Legs[sim.LegIndex].key, out var cur) ? cur.label : sim.Current.Label;
            var enlace = _app.GetComponent<SpaceApps.Enlace.EnlaceTelemetria>();
            string linkTxt = enlace != null ? enlace.Resumen() : null;
            string header = $"<b>DÍA {sim.Day}</b>  ·  FASE: {legName.ToUpperInvariant()}";
            string extra = $"   <color=#8F9BAD>·  {sceneLabel}  ·  {sim.Current.Gravity:0.###} g  ·  {sim.Current.DoseRate:0.##} mSv/día</color>";
            if (linkTxt != null)
            {
                // Estado del enlace con la Tierra (a la derecha). Si no cabe, se omite el detalle del escenario.
                bool allOn = enlace.Estaciones.Count > 0 && enlace.Estaciones.TrueForAll(e => e.Online);
                var ls = new GUIStyle(_small) { fontSize = 11, alignment = TextAnchor.UpperRight, wordWrap = false };
                string link = $"<color={(allOn ? "#7FE0A0" : "#E0B070")}>●</color> <color=#AEB8C6>{linkTxt}</color>";
                float lw = ls.CalcSize(new GUIContent(linkTxt + "  ●")).x;
                if (_label.CalcSize(new GUIContent(header + extra)).x + lw + 30 > r.width - 24) extra = "";
                GUI.Label(new Rect(r.x + 12, r.y + 7, r.width - 24, 18), link, ls);
            }
            GUI.Label(new Rect(r.x + 12, r.y + 5, r.width - 24, 22), header + extra, _label);
            if (sim.Legs.Count == 0) { GUI.Label(new Rect(r.x + 12, r.y + 32, r.width - 24, 22), "Modo libre: seleccione una misión para ver su cronograma.", _small); return; }

            var track = new Rect(r.x + 12, r.y + 34, r.width - 24, 6);
            float total = sim.TotalDays, x = track.x;
            int acc = 0;
            foreach (var leg in sim.Legs)
            {
                (string label, Color color) st = LegStyle.TryGetValue(leg.key, out var s) ? s : (leg.key, Color.gray);
                float w = leg.days / total * track.width;
                bool done = sim.Day >= acc + leg.days, now = sim.Day >= acc && !done;
                Fill(new Rect(x, track.y, Mathf.Max(1f, w - 2f), track.height), now ? st.color * 1.25f : done ? st.color : st.color * 0.55f, 1);
                if (w > 80) GUI.Label(new Rect(x, track.yMax + 2, w, 18), $"<color={(now ? "#E6EAF0" : "#6F7A8A")}>{st.label} · {leg.days} d</color>", new GUIStyle(_small) { fontSize = 11 });
                x += w; acc += leg.days;
            }
            float px = track.x + Mathf.Clamp01(sim.Day / total) * track.width;
            Fill(new Rect(px - 1, track.y - 6, 2, track.height + 12), Color.white, 0);
        }

        // ------------------------------------------------------------------ etiquetas sobre los tripulantes 3D

        void CrewTags(Rect center)
        {
            var sim = _app.Sim;
            for (int i = 0; i < sim.Crew.Count; i++)
            {
                if (!_app.TagPosition(i, _scale, out var p) || !center.Contains(p)) continue;
                var h = sim.Crew[i];
                var col = CrewAvatar.StatusColor(sim.Warnings[i].Status);
                bool sel = i == _app.Selected;
                var r = new Rect(p.x - 62, p.y - 30, 124, 30);
                Fill(r, new Color(0.06f, 0.075f, 0.1f, sel ? 0.95f : 0.75f), 3);
                Fill(new Rect(r.x, r.y, 3, r.height), col, 0);
                if (sel) Outline(r, Accent, 1, 3);
                GUI.Label(new Rect(r.x + 8, r.y + 1, 116, 16), $"<b>{h.Name}</b>", new GUIStyle(_small) { fontSize = 11, normal = { textColor = TextCol } });
                GUI.Label(new Rect(r.x + 8, r.y + 14, 116, 16), $"Índice {h.HealthIndex:0} · {StatusWord(1f - h.HealthIndex / 100f)}", new GUIStyle(_small) { fontSize = 10 });
            }
        }

        // ------------------------------------------------------------------ guía rápida

        void HelpOverlay(bool bus)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f), 0);
            var r = new Rect(W / 2 - 360, H / 2 - 260, 720, 520);
            Fill(r, PanelCol, 4);
            Fill(new Rect(r.x, r.y, r.width, 2), Accent, 0);
            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 20, r.width - 48, r.height - 36));
            GUILayout.Label("<size=20><b>Guía rápida</b></size>", _label);
            GUILayout.Label("Simulación de la salud de una tripulación en misión espacial. El modelo integra microgravedad, radiación, " +
                            "atmósfera de la cabina, ruido, ritmo circadiano y aislamiento, y calcula día a día su efecto sobre cada tripulante.", _label);
            GUILayout.Space(8);
            if (!bus)
            {
                HelpRow("rocket", "Barra superior", "Selección de misión, inicio y velocidad de la simulación, vista General o Profesional e interior/exterior de la nave.");
                HelpRow("pulse", "Escena 3D", "Los cuatro tripulantes. El anillo y el panel del pecho indican su estado. Clic sobre un tripulante para seleccionarlo.");
                HelpRow("radiation", "Panel izquierdo", "Estado de la tripulación y alertas prioritarias, ordenadas por severidad.");
                HelpRow("bone0", "Panel derecho", "Mapa corporal del tripulante seleccionado, sistemas fisiológicos y contramedidas recomendadas por el asesor.");
                HelpRow("heart", "Monitor", "Electrocardiograma, respiración y signos vitales del tripulante seleccionado.");
            }
            else HelpRow("pulse", "Modo AEGIS", "La escena representa los datos del motor del equipo (PC 1). Ingrese ws://IP_DE_PC1:8001/ws y conecte.");
            GUILayout.Label("<color=#8F9BAD>Estados: NOMINAL · PRECAUCIÓN · CRÍTICO. Cámara: clic derecho y arrastrar para girar, rueda para acercar.</color>", _small);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (!bus && GUILayout.Button("INICIAR SIMULACIÓN", new GUIStyle(_btnGreen) { fontSize = 14 }, GUILayout.Height(38))) { ShowHelp = false; if (!_app.Running) _app.TogglePlay(); }
            if (GUILayout.Button("CERRAR", _btn, GUILayout.Height(38), GUILayout.Width(130))) ShowHelp = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void HelpRow(string icon, string title, string text)
        {
            var rr = GUILayoutUtility.GetRect(10, 54, GUILayout.ExpandWidth(true));
            Fill(rr, CardCol, 3);
            Icon(new Rect(rr.x + 12, rr.y + 13, 28, 28), icon, Muted);
            GUI.Label(new Rect(rr.x + 52, rr.y + 5, 200, 20), $"<b>{title}</b>", _label);
            GUI.Label(new Rect(rr.x + 52, rr.y + 24, rr.width - 62, 32), text, _small);
            GUILayout.Space(5);
        }

        // ------------------------------------------------------------------ ayudantes de dibujo

        void Fill(Rect r, Color c, float radius) => GUI.DrawTexture(r, _white, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        void Outline(Rect r, Color c, float width, float radius) => GUI.DrawTexture(r, _white, ScaleMode.StretchToFill, true, 0f, c, width, radius);
        static void Icon(Rect r, string name, Color c) => GUI.DrawTexture(r, Icons.Get(name), ScaleMode.ScaleToFit, true, 0f, c, 0f, 0f);
        static void Icon(Rect r, Texture2D tex, Color c) => GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true, 0f, c, 0f, 0f);

        static Color Status(float sev) => sev < 0.35f ? Ok : sev < 0.65f ? Warn : Danger;
        static string StatusWord(float sev) => sev < 0.35f ? "NOMINAL" : sev < 0.65f ? "PRECAUCIÓN" : "CRÍTICO";
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Etiqueta de estado con borde fino (NOMINAL / PRECAUCIÓN / CRÍTICO).</summary>
        void Tag(Rect r, float sev)
        {
            var c = Status(sev);
            Fill(r, new Color(c.r, c.g, c.b, 0.14f), 2);
            Outline(r, new Color(c.r, c.g, c.b, 0.7f), 1, 2);
            GUI.Label(r, $"<color={Hex(c)}>{StatusWord(sev)}</color>", new GUIStyle(_small) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
        }

        /// <summary>Barra de progreso fina.</summary>
        void ThinBar(Rect r, float value01, Color c)
        {
            Fill(r, new Color(1, 1, 1, 0.08f), 1);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value01), r.height), c, 1);
        }

        void HealthBar(Rect r, float value01, int segments = 10) => ThinBar(r, value01, Status(1f - value01));

        void SectionHeader(Rect r, string text)
        {
            GUI.Label(r, text, _caps);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), Line, 0);
        }
    }
}
