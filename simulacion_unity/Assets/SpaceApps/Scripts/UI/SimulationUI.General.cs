using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>Vista General: resumen operativo de la tripulación con mapa corporal y contramedidas.</summary>
    public partial class SimulationUI
    {
        Vector2 _scrollGL, _scrollGR;
        List<Problem> _problems = new List<Problem>();
        List<Recommendation> _recs = new List<Recommendation>();
        float _refreshTimer;

        void RefreshAdvisor()
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.3f;
            _problems = Advisor.Problems(_app.Sim);
            _recs = Advisor.Recommend(_app.Sim);
        }

        void DrawGeneral(Rect left, Rect right, Rect center)
        {
            RefreshAdvisor();
            GeneralLeft(left);
            GeneralRight(right);
            Monitor(new Rect(center.x, center.yMax - 188, center.width, 188));
        }

        // ------------------------------------------------------------------ izquierda: tripulación y alertas

        void GeneralLeft(Rect r)
        {
            _blockers.Add(r);
            Fill(r, PanelCol, 4);
            var sim = _app.Sim;
            const float assistantH = 384f;
            var groups = _problems.GroupBy(p => p.System).ToList();
            float contentH = 26 + sim.Crew.Count * 50 + 34 + Mathf.Max(1, groups.Count) * 50 + 10;
            var view = new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - assistantH - 24);
            _scrollGL = GUI.BeginScrollView(view, _scrollGL, new Rect(0, 0, view.width - 16, Mathf.Max(contentH, view.height)));
            float w = view.width - 16, y = 0;

            SectionHeader(new Rect(0, y, w, 20), "TRIPULACIÓN"); y += 26;
            for (int i = 0; i < sim.Crew.Count; i++)
            {
                var h = sim.Crew[i];
                var card = new Rect(0, y, w, 44);
                bool sel = i == _app.Selected;
                Fill(card, sel ? CardHi : CardCol, 3);
                if (sel) Outline(card, Accent, 1, 3);
                Fill(new Rect(card.x, card.y, 3, card.height), CrewAvatar.StatusColor(sim.Warnings[i].Status), 0);
                float hi = h.HealthIndex;
                GUI.Label(new Rect(card.x + 12, card.y + 3, 230, 20), $"<b>{h.Name}</b>  <color=#8F9BAD><size=11>{h.Role}</size></color>", new GUIStyle(_label) { wordWrap = false });
                GUI.Label(new Rect(card.xMax - 84, card.y + 1, 74, 24), $"<b>{hi:0}</b><size=10><color=#8F9BAD> /100</color></size>", new GUIStyle(_big) { fontSize = 18, alignment = TextAnchor.UpperRight });
                ThinBar(new Rect(card.x + 12, card.y + 30, w - 110, 4), hi / 100f, Status(1f - hi / 100f));
                Tag(new Rect(card.xMax - 92, card.y + 24, 84, 15), 1f - hi / 100f);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) { _app.Selected = i; _app.Dirty = true; }
                y += 50;
            }

            y += 4;
            SectionHeader(new Rect(0, y, w, 20), $"ALERTAS PRIORITARIAS  <color=#8F9BAD>({groups.Count})</color>"); y += 26;
            if (groups.Count == 0) { GUI.Label(new Rect(0, y, w, 24), "Sin alertas activas. Todos los sistemas en rango nominal.", _small); y += 30; }
            foreach (var g in groups)
            {
                var p = g.First();
                var who = g.Select(x => sim.Crew[x.Crew].Name).Distinct().ToList();
                string whoText = who.Count == sim.Crew.Count ? "todos" : string.Join(", ", who);
                var row = new Rect(0, y, w, 44);
                Fill(row, CardCol, 3);
                Fill(new Rect(row.x, row.y, 3, row.height), Status(p.Severity), 0);
                Icon(new Rect(row.x + 10, row.y + 11, 22, 22), p.Icon == "bone" ? Icons.Bone(p.BoneSeverity) : Icons.Get(p.Icon), Status(p.Severity));
                GUI.Label(new Rect(row.x + 40, row.y + 3, w - 140, 20), $"<b>{p.System}</b> <color=#8F9BAD>· {whoText}</color>", new GUIStyle(_label) { fontSize = 13, wordWrap = false });
                GUI.Label(new Rect(row.x + 40, row.y + 22, w - 50, 18), p.ProText, new GUIStyle(_small) { fontSize = 11, wordWrap = false, clipping = TextClipping.Clip });
                Tag(new Rect(row.xMax - 92, row.y + 5, 84, 15), p.Severity);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { _app.Selected = p.Crew; _app.Dirty = true; }
                y += 50;
            }
            GUI.EndScrollView();

            AssistantPanel(new Rect(r.x + 10, r.yMax - assistantH - 8, r.width - 20, assistantH));
        }
        // ------------------------------------------------------------------ derecha: mapa corporal, sistemas, contramedidas

        void GeneralRight(Rect r)
        {
            _blockers.Add(r);
            Fill(r, PanelCol, 4);
            var sim = _app.Sim;
            var h = sim.Crew[_app.Selected];
            float contentH = 80 + 30 + 290 + 40 + _recs.Count * 76 + 20;
            var view = new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 20);
            _scrollGR = GUI.BeginScrollView(view, _scrollGR, new Rect(0, 0, view.width - 16, Mathf.Max(contentH, view.height)));
            float w = view.width - 16, y = 0;

            // Encabezado del tripulante
            var head = new Rect(0, y, w, 70);
            Fill(head, CardHi, 3);
            float hi = h.HealthIndex;
            GUI.Label(new Rect(head.x + 12, head.y + 6, w - 130, 22), $"<b>{h.Name}</b>  <color=#8F9BAD>{h.Role}</color>", _title);
            GUI.Label(new Rect(head.x + 12, head.y + 28, w - 130, 18), $"{h.Age} años · {h.MassKg:0.0} kg · {(h.Cell >= 0 ? CabinEnvironment.CellNames[h.Cell] : "EVA")}", _small);
            GUI.Label(new Rect(head.xMax - 112, head.y + 2, 102, 36), $"<b>{hi:0}</b><size=12><color=#8F9BAD> /100</color></size>", new GUIStyle(_huge) { fontSize = 28, alignment = TextAnchor.UpperRight });
            GUI.Label(new Rect(head.xMax - 112, head.y + 34, 102, 16), "ÍNDICE DE SALUD", new GUIStyle(_caps) { fontSize = 10, alignment = TextAnchor.UpperRight });
            ThinBar(new Rect(head.x + 12, head.y + 58, w - 24, 4), hi / 100f, Status(1f - hi / 100f));
            y += 80;

            // Mapa corporal + sistemas fisiológicos
            SectionHeader(new Rect(0, y, w, 20), "MAPA CORPORAL Y SISTEMAS FISIOLÓGICOS"); y += 28;
            var mapRect = new Rect(0, y, 150, 272);
            Fill(mapRect, new Color(0.075f, 0.09f, 0.115f, 1f), 3);
            if (_app.BodyMap != null && _app.BodyMap.Texture != null) GUI.DrawTexture(mapRect, _app.BodyMap.Texture, ScaleMode.ScaleToFit);
            else GUI.Label(mapRect, "Modelo anatómico no disponible", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            Outline(mapRect, Line, 1, 3);

            float co2 = h.Cell >= 0 ? sim.Env.Co2[h.Cell] : 0.3f;
            var systems = new List<(Texture2D icon, string label, string value, float sev)>
            {
                (Icons.Bone(Mathf.Clamp01((100f - h.Bone) / 20f)), "Densidad ósea", $"{h.Bone:0.0} %", Mathf.Clamp01((100f - h.Bone) / 20f)),
                (Icons.Get("dumbbell"), "Masa muscular", $"{h.Muscle:0.0} %", Mathf.Clamp01((100f - h.Muscle) / 30f)),
                (Icons.Get("heart"), "VO₂ máx", $"{h.Vo2:0.0} %", Mathf.Clamp01((100f - h.Vo2) / 30f)),
                (Icons.Get("eye"), "Índice SANS", $"{h.Sans:0.00}", h.Sans),
                (Icons.Get("radiation"), "Dosis acumulada", $"{h.DoseMsv:0} mSv", Mathf.Clamp01(h.DoseMsv / 600f)),
                (Icons.Get("moon"), "Sueño / deuda", $"{h.SleepHoursLast:0.0} / {h.SleepDebtH:0.0} h", Mathf.Clamp01(h.SleepDebtH / 10f)),
                (Icons.Get("gauge"), "Estrés", $"{h.Stress:0} /100", Mathf.Clamp01((h.Stress - 20f) / 60f)),
                (Icons.Get("air"), "CO₂ respirado", $"{co2:0.0} mmHg", Mathf.Clamp01((co2 - 3f) / 3f)),
            };
            float sx = 160, sw = w - 160;
            for (int i = 0; i < systems.Count; i++)
            {
                var s = systems[i];
                var row = new Rect(sx, y + i * 34, sw, 31);
                Fill(row, CardCol, 2);
                Icon(new Rect(row.x + 6, row.y + 6, 19, 19), s.icon, Status(s.sev));
                GUI.Label(new Rect(row.x + 30, row.y + 1, sw - 34, 16), s.label, new GUIStyle(_small) { fontSize = 11 });
                GUI.Label(new Rect(row.x + 30, row.y + 13, sw - 120, 18), $"<b>{s.value}</b>", new GUIStyle(_label) { fontSize = 13, wordWrap = false });
                Tag(new Rect(row.xMax - 84, row.y + 8, 78, 15), s.sev);
            }
            y += 290;

            // Contramedidas calculadas por el asesor
            SectionHeader(new Rect(0, y, w, 20), "CONTRAMEDIDAS RECOMENDADAS");
            if (GUI.Button(new Rect(w - 150, y - 4, 150, 22), Advisor.AutoPilot ? "AUTOMÁTICO: ACTIVO" : "AUTOMÁTICO: INACTIVO", new GUIStyle(Advisor.AutoPilot ? _btnSel : _btn) { fontSize = 10 }))
            {
                Advisor.AutoPilot = !Advisor.AutoPilot;
                if (Advisor.AutoPilot) Advisor.AutoApply(sim);
            }
            y += 28;
            foreach (var rec in _recs)
            {
                var row = new Rect(0, y, w, 70);
                Fill(row, CardCol, 3);
                Fill(new Rect(row.x, row.y, 3, row.height), rec.Applied ? Ok : Accent, 0);
                Icon(new Rect(row.x + 10, row.y + 10, 22, 22), rec.Icon.StartsWith("face") ? "gauge" : rec.Icon, rec.Applied ? Ok : Muted);
                GUI.Label(new Rect(row.x + 40, row.y + 4, w - 150, 20), $"<b>{rec.Title}</b>{(rec.Experimental ? " <color=#8F9BAD>· experimental</color>" : "")}", new GUIStyle(_label) { fontSize = 13, wordWrap = false });
                GUI.Label(new Rect(row.x + 40, row.y + 23, w - 50, 18), rec.Why, new GUIStyle(_small) { fontSize = 11, wordWrap = false });
                GUI.Label(new Rect(row.x + 40, row.y + 43, w - 50, 22), $"<color=#7FBF9C>Efecto: {rec.Benefit}</color>", new GUIStyle(_small) { fontSize = 11, wordWrap = false });
                var btn = new Rect(row.xMax - 92, row.y + 6, 84, 22);
                if (rec.Applied)
                {
                    Fill(btn, new Color(Ok.r, Ok.g, Ok.b, 0.14f), 2);
                    GUI.Label(btn, $"<color={Hex(Ok)}>ACTIVA</color>", new GUIStyle(_small) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                }
                else if (GUI.Button(btn, "APLICAR", new GUIStyle(_btnSel) { fontSize = 11 })) { Advisor.Apply(sim, rec); _refreshTimer = 0f; }
                y += 76;
            }
            GUI.EndScrollView();
        }

        // ------------------------------------------------------------------ monitor de signos vitales

        void Monitor(Rect r)
        {
            _blockers.Add(r);
            Fill(r, new Color(0.03f, 0.04f, 0.045f, 0.97f), 4);
            Outline(r, Line, 1, 4);
            var h = _app.Sim.Crew[_app.Selected];
            GUI.Label(new Rect(r.x + 12, r.y + 6, 400, 20), $"MONITOR DE SIGNOS VITALES  <color=#8F9BAD>· {h.Name}</color>", _caps);

            float beat = 1f + 0.15f * _monitor.HeartPulse;
            Icon(new Rect(r.x + 20 + 18 - 18 * beat, r.y + 46 + 18 - 18 * beat, 36 * beat, 36 * beat), "heart", new Color(0.8f, 0.32f, 0.32f));
            GUI.Label(new Rect(r.x + 8, r.y + 86, 60, 16), "Corazón", new GUIStyle(_small) { fontSize = 10, alignment = TextAnchor.UpperCenter });
            float br = 0.88f + 0.18f * _monitor.Breath;
            Icon(new Rect(r.x + 20 + 17 - 17 * br, r.y + 112 + 17 - 17 * br, 34 * br, 34 * br), "lungs", new Color(0.8f, 0.66f, 0.35f));
            GUI.Label(new Rect(r.x + 8, r.y + 150, 60, 16), "Pulmones", new GUIStyle(_small) { fontSize = 10, alignment = TextAnchor.UpperCenter });

            float numbersW = 236;
            var wave = new Rect(r.x + 74, r.y + 30, r.width - 74 - numbersW - 10, r.height - 40);
            GUI.DrawTexture(wave, _monitor.Tex, ScaleMode.StretchToFill);
            GUI.Label(new Rect(wave.x + 6, wave.y + 2, 120, 16), "<color=#4FE08A>ECG II</color>", new GUIStyle(_small) { fontSize = 10 });
            GUI.Label(new Rect(wave.x + 6, wave.yMax - 42, 120, 16), "<color=#E8C547>RESP</color>", new GUIStyle(_small) { fontSize = 10 });

            float nx = r.xMax - numbersW, ny = r.y + 8;
            Vital(new Rect(nx, ny, 113, 56), "FC", $"{h.HeartRate:0}", "lpm", new Color(0.35f, 0.9f, 0.5f), h.HeartRate > 100f && h.Activity < 3f);
            Vital(new Rect(nx + 118, ny, 113, 56), "SpO₂", $"{h.SpO2:0}", "%", new Color(0.4f, 0.8f, 0.95f), h.SpO2 < 92f);
            Vital(new Rect(nx, ny + 60, 113, 56), "PANI", $"{h.Sbp:0}/{h.Dbp:0}", "mmHg", new Color(0.95f, 0.45f, 0.45f), h.Sbp >= 140f && h.Activity < 3f);
            Vital(new Rect(nx + 118, ny + 60, 113, 56), "FR", $"{h.RespRate:0}", "rpm", new Color(0.92f, 0.78f, 0.3f), h.RespRate > 22f && h.Activity < 3f);
            Vital(new Rect(nx, ny + 120, 231, 52), "TEMP", $"{h.CoreTemp:0.0}", h.Activity >= 3f ? "°C · en ejercicio" : h.Activity < 0.5f ? "°C · en reposo nocturno" : "°C", new Color(0.88f, 0.9f, 0.92f), h.CoreTemp > 38f && h.Activity < 3f);
        }

        void Vital(Rect r, string label, string value, string unit, Color c, bool alarm)
        {
            Fill(r, alarm ? new Color(0.35f, 0.08f, 0.08f, 1f) : new Color(0.06f, 0.075f, 0.08f, 1f), 3);
            GUI.Label(new Rect(r.x + 8, r.y + 2, r.width - 10, 16), $"<color={Hex(c)}>{label}</color>", new GUIStyle(_small) { fontSize = 10, fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(r.x + 8, r.y + 15, r.width - 10, 28), $"<color={Hex(c)}><b>{value}</b></color>", new GUIStyle(_big) { fontSize = 21 });
            GUI.Label(new Rect(r.x + 8, r.yMax - 16, r.width - 10, 16), unit, new GUIStyle(_small) { fontSize = 10 });
        }
    }
}
