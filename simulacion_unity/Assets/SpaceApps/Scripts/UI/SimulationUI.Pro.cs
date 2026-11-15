using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>Vista Profesional: todos los datos en tarjetas, sensores, alertas, gráficos y ajuste manual.</summary>
    public partial class SimulationUI
    {
        Vector2 _scrollL, _scrollR;
        List<SensorReading> _bioReadings = new List<SensorReading>(), _envReadings = new List<SensorReading>();

        void DrawPro(Rect left, Rect right, Rect center)
        {
            RefreshAdvisor();
            ProLeft(left);
            ProRight(right);
            ChartPanel(new Rect(center.x, center.yMax - 250, center.width, 250), _app.Sim);
        }

        // ------------------------------------------------------------------ izquierda: tripulación y ajuste manual

        void ProLeft(Rect r)
        {
            _blockers.Add(r);
            var sim = _app.Sim;
            GUILayout.BeginArea(r, _panel);
            _scrollL = GUILayout.BeginScrollView(_scrollL);
            GUILayout.Label("TRIPULACIÓN", _caps);
            var names = new string[sim.Crew.Count];
            for (int i = 0; i < names.Length; i++)
            {
                var lvl = sim.Warnings[i].Status;
                string dot = lvl == EarlyWarning.Level.Red ? "<color=#ff5a4a>●</color>" : lvl == EarlyWarning.Level.Yellow ? "<color=#ffd24a>●</color>" : "<color=#7ee08a>●</color>";
                names[i] = $"{dot} {sim.Crew[i].Name}";
            }
            int sel = GUILayout.SelectionGrid(_app.Selected, names, 2, _tabStyle);
            if (sel != _app.Selected) { _app.Selected = sel; _app.Dirty = true; }
            var h = sim.Crew[_app.Selected];
            GUILayout.Label($"{h.Role} · {h.Age} años · {h.HeightM:0.00} m · {h.MassKg:0.0} kg", _small);
            h.Fitness = Slider("Condición física", h.Fitness, 0f, 1f, "0.00");
            GUILayout.Label($"Acondicionamiento previo: <b>{sim.ConditioningDays} días</b> (mín. 14)", _label);
            int cond = Mathf.RoundToInt(GUILayout.HorizontalSlider(sim.ConditioningDays, 0, 60));
            if (cond != sim.ConditioningDays) { sim.ConditioningDays = cond; _app.ApplyMission(sim.MissionIndex); }

            GUILayout.Space(8);
            GUILayout.Label("CONTRAMEDIDAS (AJUSTE MANUAL)", _caps);
            bool auto = GUILayout.Toggle(Advisor.AutoPilot, " Piloto automático (las calcula el asesor)");
            if (auto != Advisor.AutoPilot) { Advisor.AutoPilot = auto; if (auto) Advisor.AutoApply(sim); }
            GUILayout.Label(Advisor.AutoPilot ? "Mover un control desactiva el piloto automático." : "Modo manual: los valores los fijas tú.", _small);
            var cm = sim.Cm;
            float before = cm.Exercise + cm.Shielding + cm.Scrubber + cm.Support + cm.Lighting + cm.ArtificialG + (cm.Shelter ? 1 : 0);
            cm.Exercise = Slider("Ejercicio diario (2,5 h = 1)", cm.Exercise, 0f, 1f, "0.00");
            cm.Shielding = Slider("Blindaje de la nave", cm.Shielding, 0f, 1f, "0.00");
            cm.Shelter = GUILayout.Toggle(cm.Shelter, " Refugio para tormentas solares");
            cm.Scrubber = Slider("Depurador CO₂ / filtro HEPA", cm.Scrubber, 0f, 1f, "0.00");
            cm.Support = Slider("Apoyo psicológico", cm.Support, 0f, 1f, "0.00");
            cm.Lighting = Slider("Iluminación circadiana", cm.Lighting, 0f, 1f, "0.00");
            cm.ArtificialG = Slider("Gravedad artificial en tránsito (g)", cm.ArtificialG, 0f, 0.5f, "0.00");
            float after = cm.Exercise + cm.Shielding + cm.Scrubber + cm.Support + cm.Lighting + cm.ArtificialG + (cm.Shelter ? 1 : 0);
            if (Mathf.Abs(after - before) > 1e-4f) Advisor.AutoPilot = false;
            GUILayout.Space(8);
            GUILayout.Label("ENTORNO", _caps);
            sim.SolarActivity = Slider("Actividad solar", sim.SolarActivity, 0f, 1f, "0.00");
            GUILayout.Space(6);
            if (GUILayout.Button("EXPORTAR DATOS (CSV)", _btn, GUILayout.Height(32))) sim.ExportCsv();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        float Slider(string label, float v, float min, float max, string fmt)
        {
            GUILayout.Label($"{label}: <b>{v.ToString(fmt)}</b>", _label);
            return GUILayout.HorizontalSlider(v, min, max);
        }

        // ------------------------------------------------------------------ derecha: pestañas con tarjetas

        void ProRight(Rect r)
        {
            _blockers.Add(r);
            var sim = _app.Sim;
            GUILayout.BeginArea(r, _panel);
            _tab = GUILayout.Toolbar(_tab, Tabs, _tabStyle, GUILayout.Height(32));
            _scrollR = GUILayout.BeginScrollView(_scrollR);
            var h = sim.Crew[_app.Selected];
            var w = sim.Warnings[_app.Selected];
            GUILayout.Label($"<b>{h.Name}</b> · {h.Role} · en {(h.Cell >= 0 ? CabinEnvironment.CellNames[h.Cell] : "EVA")}", _title);
            switch (_tab)
            {
                case 0: TabSummary(h, w); break;
                case 1: TabVitals(h); break;
                case 2: TabBlood(h); break;
                case 3: TabAir(sim, h); break;
                case 4: TabSensors(sim, h); break;
                default: TabAlerts(sim, h, w); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        struct T
        {
            public string Icon, Title, Value, Normal; public float V, Lo, Hi, Min, Max;
            public T(string icon, string title, string value, float v, float lo, float hi, float min, float max, string normal = null)
            { Icon = icon; Title = title; Value = value; V = v; Lo = lo; Hi = hi; Min = min; Max = max; Normal = normal; }
        }

        /// <summary>Filas de dos tarjetas de dato con ícono, valor grande y barra de rango normal.</summary>
        void Tiles(params T[] tiles)
        {
            for (int i = 0; i < tiles.Length; i += 2)
            {
                var row = GUILayoutUtility.GetRect(10, 104, GUILayout.ExpandWidth(true));
                float cw = (row.width - 8) / 2f;
                Tile(new Rect(row.x, row.y, cw, 96), tiles[i]);
                if (i + 1 < tiles.Length) Tile(new Rect(row.x + cw + 8, row.y, cw, 96), tiles[i + 1]);
            }
        }

        void Tile(Rect r, T t)
        {
            float dist = t.V < t.Lo ? (t.Lo - t.V) / Mathf.Max(1e-4f, t.Hi - t.Lo) : t.V > t.Hi ? (t.V - t.Hi) / Mathf.Max(1e-4f, t.Hi - t.Lo) : 0f;
            var col = dist <= 0f ? Ok : dist < 0.25f ? Warn : Danger;
            Fill(r, CardCol, 3);
            Fill(new Rect(r.x, r.y, 6, r.height), col, 3);
            Icon(new Rect(r.x + 12, r.y + 10, 34, 34), t.Icon, col);
            GUI.Label(new Rect(r.x + 52, r.y + 6, r.width - 56, 20), t.Title, new GUIStyle(_small) { fontSize = 12 });
            GUI.Label(new Rect(r.x + 52, r.y + 22, r.width - 56, 30), t.Value, new GUIStyle(_big) { fontSize = 21 });
            // barra de rango: gris = posible, verde = normal, marca blanca = valor actual
            var bar = new Rect(r.x + 12, r.yMax - 22, r.width - 24, 8);
            Fill(bar, new Color(1, 1, 1, 0.12f), 4);
            float a = Mathf.InverseLerp(t.Min, t.Max, t.Lo), b = Mathf.InverseLerp(t.Min, t.Max, t.Hi);
            Fill(new Rect(bar.x + bar.width * a, bar.y, bar.width * (b - a), bar.height), new Color(Ok.r, Ok.g, Ok.b, 0.45f), 4);
            float m = Mathf.Clamp01(Mathf.InverseLerp(t.Min, t.Max, t.V));
            Fill(new Rect(bar.x + bar.width * m - 2, bar.y - 4, 4, bar.height + 8), Color.white, 2);
            GUI.Label(new Rect(r.x + 12, r.yMax - 40, r.width - 24, 18), $"normal: {t.Normal ?? $"{t.Lo:0.##}–{t.Hi:0.##}"}", new GUIStyle(_small) { fontSize = 10, alignment = TextAnchor.UpperRight });
        }

        void Section(string icon, string title)
        {
            var rr = GUILayoutUtility.GetRect(10, 30, GUILayout.ExpandWidth(true));
            Icon(new Rect(rr.x, rr.y + 5, 18, 18), icon, Muted);
            GUI.Label(new Rect(rr.x + 28, rr.y, rr.width - 28, 28), $"<b>{title}</b>", _title);
        }

        void TabSummary(AstronautHealth h, EarlyWarning w)
        {
            float hi = h.HealthIndex;
            GUILayout.Label($"<size=24><b><color={Hex(Status(1f - hi / 100f))}>Salud global: {hi:0} / 100</color></b></size>", _label);
            Tiles(
                new T("bone0", "Densidad ósea", $"{h.Bone:0.0} %", h.Bone, 90, 100, 70, 100, "> 90 %"),
                new T("dumbbell", "Masa muscular", $"{h.Muscle:0.0} %", h.Muscle, 85, 100, 60, 100, "> 85 %"),
                new T("battery", "VO₂ máx", $"{h.Vo2:0.0} %", h.Vo2, 85, 100, 60, 100, "> 85 %"),
                new T("heart", "Déficit de plasma", $"{h.PlasmaDeficit:0.0} %", h.PlasmaDeficit, 0, 6, 0, 15, "< 6 %"),
                new T("eye", "Índice SANS", $"{h.Sans:0.00}", h.Sans, 0, 0.3f, 0, 1, "< 0,3"),
                new T("radiation", "Dosis acumulada", $"{h.DoseMsv:0} mSv", h.DoseMsv, 0, 600, 0, 1000, "< 600 mSv"),
                new T("radiation", "Riesgo de cáncer", $"+{h.CancerRiskPercent:0.0} %", h.CancerRiskPercent, 0, 3, 0, 6, "< 3 %"),
                new T("gauge", "Estrés", $"{h.Stress:0}", h.Stress, 0, 50, 0, 100, "< 50"));
            Section("gauge", "Pronóstico (regresión de 30 días)");
            GUILayout.Label($"Tendencia de salud: {w.HealthSlopePerDay:+0.000;-0.000} puntos/día", _label);
            GUILayout.Label(hi <= 60f ? "<color=#ff5a4a>Salud ya por debajo de 60</color>"
                : float.IsInfinity(w.DaysToHealth60) ? "Salud < 60: no se alcanza con la tendencia actual" : $"Salud < 60 en ~<b>{w.DaysToHealth60:0}</b> días", _label);
            GUILayout.Label(float.IsInfinity(w.DaysToBone90) ? "Osteopenia (hueso < 90 %): no prevista" : $"Osteopenia en ~<b>{w.DaysToBone90:0}</b> días", _label);
            Section("pulse", "Síntomas");
            var sy = h.Symptoms();
            if (sy.Count == 0) GUILayout.Label("<color=#7ee08a>Sin síntomas relevantes</color>", _label);
            foreach (var s in sy) GUILayout.Label($"<color={Hex(Status(s.sev))}>●</color> <b>{s.zone}:</b> {s.text}", _label);
        }

        void TabVitals(AstronautHealth h)
        {
            var mon = GUILayoutUtility.GetRect(10, 110, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(mon, _monitor.Tex, ScaleMode.StretchToFill);
            float beat = 1f + 0.22f * _monitor.HeartPulse;
            Icon(new Rect(mon.xMax - 60 - 10 * beat, mon.y + 6, 44 * beat, 44 * beat), "heart", new Color(1f, 0.3f, 0.35f));
            Section("heart", "Signos vitales y hemodinámica");
            Tiles(
                new T("heart", "Frecuencia cardíaca", $"{h.HeartRate:0} lpm", h.HeartRate, 50, 100, 30, 190),
                new T("pulse", "Variabilidad (HRV)", $"{h.Hrv:0} ms", h.Hrv, 20, 100, 0, 120),
                new T("gauge", "Presión arterial", $"{h.Sbp:0}/{h.Dbp:0}", h.Sbp, 90, 139, 70, 200, "90–139 sistólica"),
                new T("drop", "Saturación de O₂", $"{h.SpO2:0.0} %", h.SpO2, 94, 100, 80, 100),
                new T("lungs", "Frecuencia respiratoria", $"{h.RespRate:0} rpm", h.RespRate, 10, 22, 0, 40),
                new T("thermo", "Temperatura central", $"{h.CoreTemp:0.0} °C", h.CoreTemp, 36, 37.8f, 35, 40.5f));
            Section("pulse", "Neuromuscular y bioeléctrica");
            Tiles(
                new T("dumbbell", "EMG · fatiga muscular", $"{h.EmgFatigue:0.00}", h.EmgFatigue, 0, 0.5f, 0, 1),
                h.Activity < 0.5f
                    ? new T("eye", "EEG · nivel de alerta", $"{h.EegAlertness:0.00} (dormido)", h.EegAlertness, 0, 1, 0, 1, "bajo al dormir")
                    : new T("eye", "EEG · nivel de alerta", $"{h.EegAlertness:0.00}", h.EegAlertness, 0.6f, 1, 0, 1, "> 0,6 despierto"),
                new T("drop", "GSR · conductancia", $"{h.Gsr:0.0} µS", h.Gsr, 1, 8, 0, 15));
            Section("moon", "Conductuales y metabólicos");
            Tiles(
                new T("moon", "Sueño última noche", $"{h.SleepHoursLast:0.0} h", h.SleepHoursLast, 7, 9, 3, 10),
                new T("moon", "Fase REM", $"{h.RemPercent:0} %", h.RemPercent, 18, 30, 0, 35),
                new T("sun", "Gasto energético", $"{h.EnergyKcal:0} kcal", h.EnergyKcal, 2000, 3500, 1000, 4500),
                new T("dumbbell", "Actividad (acelerómetro)", $"{h.Mets:0.0} MET", h.Mets, 0.8f, 8, 0, 12),
                new T("gauge", "Masa corporal", $"{h.MassKg:0.0} kg", h.MassKg, h.MassKg0 - 3f, h.MassKg0 + 3f, h.MassKg0 - 15f, h.MassKg0 + 5f, "±3 kg del inicio"),
                new T("gauge", "IMC", $"{h.Bmi:0.0}", h.Bmi, 18.5f, 27, 15, 35));
        }

        void TabBlood(AstronautHealth h)
        {
            Section("drop", "Química de la sangre y orina");
            Tiles(
                new T("drop", "Glucosa", $"{h.Glucose:0} mg/dL", h.Glucose, 70, 140, 40, 220),
                new T("drop", "Lactato", $"{h.Lactate:0.0} mmol/L", h.Lactate, 0.5f, 2.2f, 0, 8),
                new T("gauge", "Cortisol (estrés)", $"{h.Cortisol:0.0} µg/dL", h.Cortisol, 3, 22, 0, 40),
                new T("drop", "pH sanguíneo", $"{h.BloodPh:0.000}", h.BloodPh, 7.35f, 7.45f, 7.2f, 7.6f),
                new T("drop", "Potasio", $"{h.Potassium:0.00} mmol/L", h.Potassium, 3.5f, 5f, 2.5f, 6f),
                new T("kidney", "Calcio en orina", $"{h.UrinaryCa:0} mg/día", h.UrinaryCa, 100, 300, 0, 450));
            GUILayout.Label("El calcio sale del hueso en microgravedad y llega a la orina (riesgo de cálculos renales). " +
                            "El cortisol sigue el ritmo del día (pico a las 08 h) y sube con el estrés y la falta de sueño.", _small);
        }

        void TabAir(Simulation sim, AstronautHealth h)
        {
            var e = sim.Env;
            int c = Mathf.Max(0, h.Cell);
            Section("air", sim.Current.Cabin ? $"Aire en {CabinEnvironment.CellNames[c]}" : "Aire exterior (ciudad)");
            Tiles(
                new T("air", "CO₂", $"{CabinEnvironment.Co2Ppm(e.Co2[c]):0} ppm", e.Co2[c], 0, 3, 0, 8, "< 3 mmHg"),
                new T("air", "PM2.5 (polvo fino)", $"{e.Pm25[c]:0.0} µg/m³", e.Pm25[c], 0, 15, 0, 80),
                new T("air", "PM10", $"{e.Pm25[c] * 1.7f:0.0} µg/m³", e.Pm25[c] * 1.7f, 0, 45, 0, 150),
                new T("air", "Monóxido de carbono", $"{e.CoPpm:0.0} ppm", e.CoPpm, 0, 9, 0, 30),
                new T("air", "NO₂", $"{e.No2:0} µg/m³", e.No2, 0, 25, 0, 100),
                new T("sun", "Ozono O₃", $"{e.O3:0} µg/m³", e.O3, 0, 100, 0, 200),
                new T("lungs", "Oxígeno", $"{e.O2Percent:0.0} %", e.PpO2Kpa, 19, 26, 14, 30, "ppO₂ 19–26 kPa"),
                new T("thermo", "Temperatura", $"{e.TempC:0.0} °C", e.TempC, 18, 27, 10, 35),
                new T("drop", "Humedad", $"{e.Humidity:0} %", e.Humidity, 25, 70, 0, 100),
                new T("pulse", "Ruido", $"{e.NoiseDb:0} dB", e.NoiseDb, 0, 65, 30, 90, "< 65 dB"));
            if (sim.Current.Cabin)
            {
                Section("air", "CO₂ por módulo de la nave");
                var row = GUILayoutUtility.GetRect(10, 70, GUILayout.ExpandWidth(true));
                float bw = row.width / CabinEnvironment.Cells;
                for (int i = 0; i < CabinEnvironment.Cells; i++)
                {
                    float v = e.Co2[i];
                    var col = v < 3f ? Color.Lerp(Ok, Warn, Mathf.InverseLerp(1f, 3f, v)) : Color.Lerp(Warn, Danger, Mathf.InverseLerp(3f, 5f, v));
                    float bh = Mathf.Clamp01(v / 6f) * 46f;
                    Fill(new Rect(row.x + i * bw + 2, row.y + 50 - bh, bw - 4, bh), col, 3);
                    GUI.Label(new Rect(row.x + i * bw, row.y + 50, bw, 18), $"{v:0.0}", new GUIStyle(_small) { fontSize = 10, alignment = TextAnchor.UpperCenter });
                    if (i == h.Cell) Outline(new Rect(row.x + i * bw + 1, row.y, bw - 2, 50), Color.white, 2, 3);
                }
                GUILayout.Label("0 depurador · 1-3 dormitorios · 4-8 laboratorios · 9 gimnasio · 10 cocina · 11 esclusa. Recuadro blanco = dónde está ahora.", _small);
            }
        }

        void TabSensors(Simulation sim, AstronautHealth h)
        {
            _sensorTimer -= Time.unscaledDeltaTime;
            if (_sensorTimer <= 0f || _bioReadings.Count == 0)
            {
                _sensorTimer = 1f;
                _bioReadings = SensorSuite.Biomedical(h);
                _envReadings = SensorSuite.Environmental(sim.Env, h.Cell);
            }
            Section("pulse", "Sensores biomédicos (lectura real con ruido)");
            foreach (var s in _bioReadings) GUILayout.Label($"<b>{s.Variable}</b> · {s.Text}  <color=#8090b0>[{s.Sensor}]</color>", _label);
            Section("air", "Sensores ambientales");
            foreach (var s in _envReadings) GUILayout.Label($"<b>{s.Variable}</b> · {s.Text}  <color=#8090b0>[{s.Sensor}]</color>", _label);
        }

        void TabAlerts(Simulation sim, AstronautHealth h, EarlyWarning w)
        {
            var led = SensorSuite.LedColor(w.Status);
            var rr = GUILayoutUtility.GetRect(10, 40, GUILayout.ExpandWidth(true));
            Fill(new Rect(rr.x, rr.y + 6, 28, 28), led, 14);
            GUI.Label(new Rect(rr.x + 38, rr.y + 2, rr.width - 40, 40),
                $"LED de estado: <b>{(w.Status == EarlyWarning.Level.Red ? "ROJO" : w.Status == EarlyWarning.Level.Yellow ? "AMARILLO" : "VERDE")}</b> · vibraciones enviadas: {w.HapticEvents}", _label);
            GUILayout.Label("Señales fuera de rango ahora: " + (w.ActiveFlags.Count == 0 ? "ninguna" : string.Join(", ", w.ActiveFlags)), _label);
            Section("star", "Bitácora (más reciente primero)");
            for (int i = sim.Log.Count - 1; i >= 0 && i >= sim.Log.Count - 120; i--) GUILayout.Label(sim.Log[i], _small);
        }

        // ------------------------------------------------------------------ gráficos

        void ChartPanel(Rect r, Simulation sim)
        {
            _blockers.Add(r);
            GUILayout.BeginArea(r, _panel);
            int k = GUILayout.Toolbar(_chartKind, Charts, _tabStyle, GUILayout.Height(30));
            if (k != _chartKind) { _chartKind = k; _app.Dirty = true; }
            var area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndArea();

            _chartTimer -= Time.unscaledDeltaTime;
            if (_app.Dirty && _chartTimer <= 0f) { RedrawChart(sim); _chartTimer = 0.25f; _app.Dirty = false; }
            var img = new Rect(r.x + area.x, r.y + area.y, area.width, area.height - 24);
            GUI.DrawTexture(img, _chart.Tex, ScaleMode.StretchToFill);
            GUI.Label(new Rect(img.x + 4, img.y + 2, 60, 18), $"{_chart.YMax:0.#}", _small);
            GUI.Label(new Rect(img.x + 4, img.yMax - 18, 60, 18), $"{_chart.YMin:0.#}", _small);
            GUI.Label(new Rect(img.x, img.yMax, 60, 16), "día 0", _small);
            GUI.Label(new Rect(img.xMax - 80, img.yMax, 80, 16), $"día {_chart.XMax:0}", _small);
            DrawLegend(img);
        }

        void DrawLegend(Rect img)
        {
            float lx = img.x + 70;
            foreach (var (name, color) in _legend)
            {
                Fill(new Rect(lx, img.yMax + 4, 12, 12), color, 2);
                GUI.Label(new Rect(lx + 15, img.yMax - 1, 190, 18), name, _small);
                lx += 20 + _small.CalcSize(new GUIContent(name)).x + 12;
            }
        }

        readonly List<(string, Color)> _legend = new List<(string, Color)>();
        readonly List<float> _a = new List<float>(), _b = new List<float>(), _c = new List<float>(), _d = new List<float>();

        void RedrawChart(Simulation sim)
        {
            var h = sim.Crew[_app.Selected];
            _a.Clear(); _b.Clear(); _c.Clear(); _d.Clear(); _legend.Clear();
            float xMax = sim.TotalDays;
            switch (_chartKind)
            {
                case 0:
                    foreach (var r in h.History) { _a.Add(r.Bone); _b.Add(r.Muscle); _c.Add(r.Vo2); _d.Add(r.Health); }
                    _chart.Begin(30, 100, xMax);
                    Legend(("Hueso %", new Color(0.95f, 0.9f, 0.75f)), ("Músculo %", new Color(1f, 0.5f, 0.4f)), ("VO₂ máx %", new Color(0.4f, 0.75f, 1f)), ("Salud global", CGreen));
                    break;
                case 1:
                    foreach (var r in h.History) { _a.Add(r.Dose); _b.Add(r.Stress * 10f); }
                    _chart.Begin(0, Mathf.Max(1200f, h.DoseMsv * 1.1f), xMax);
                    _chart.Reference(600f, CRed);
                    Legend(("Dosis mSv (límite 600)", new Color(1f, 0.35f, 0.6f)), ("Estrés ×10", new Color(0.75f, 0.6f, 1f)));
                    break;
                case 2:
                    foreach (var r in h.History) { _a.Add(r.HrRest); _b.Add(r.Sbp); _c.Add(r.SpO2); _d.Add(r.CoreTemp * 2f); }
                    _chart.Begin(40, 160, xMax);
                    Legend(("FC nocturna lpm", new Color(1f, 0.45f, 0.45f)), ("PA sistólica", new Color(1f, 0.8f, 0.3f)), ("SpO₂ %", new Color(0.4f, 0.75f, 1f)), ("T central ×2", new Color(0.9f, 0.9f, 0.9f)));
                    break;
                case 3:
                    foreach (var r in h.History) { _a.Add(r.Co2Exposure * 10f); _b.Add(r.Pm25Exposure); }
                    _chart.Begin(0, 80, xMax);
                    _chart.Reference(60f, CRed);
                    Legend(("CO₂ ×10 mmHg (límite 6)", new Color(0.5f, 1f, 0.8f)), ("PM2.5 µg/m³", new Color(0.85f, 0.65f, 0.4f)));
                    break;
                default:
                    foreach (var r in h.History) { _a.Add(r.SleepH * 5f); _b.Add(r.Cortisol); _c.Add(r.Glucose / 5f); }
                    _chart.Begin(0, 50, xMax);
                    Legend(("Sueño h ×5", new Color(0.6f, 0.7f, 1f)), ("Cortisol µg/dL", new Color(1f, 0.7f, 0.3f)), ("Glucosa ÷5", new Color(0.6f, 1f, 0.6f)));
                    break;
            }
            int acc = 0;
            foreach (var leg in sim.Legs) { _chart.VerticalMarker(acc); acc += leg.days; }
            if (_a.Count > 0) _chart.Series(_a, _legend[0].Item2);
            if (_b.Count > 0) _chart.Series(_b, _legend[1].Item2);
            if (_c.Count > 0 && _legend.Count > 2) _chart.Series(_c, _legend[2].Item2);
            if (_d.Count > 0 && _legend.Count > 3) _chart.Series(_d, _legend[3].Item2);
            _chart.End();
        }

        void Legend(params (string, Color)[] items) => _legend.AddRange(items);
    }
}
