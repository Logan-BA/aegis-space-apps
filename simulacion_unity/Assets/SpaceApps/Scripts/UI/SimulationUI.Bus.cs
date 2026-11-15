using UnityEngine;

namespace SpaceApps
{
    /// <summary>Modo Bus AEGIS: muestra lo que envía el motor del equipo, sin diagnósticos propios.</summary>
    public partial class SimulationUI
    {
        int _busTab;
        Vector2 _scrollBL, _scrollBR;
        string _urlEdit;
        static readonly string[] BusTabs = { "Indicadores", "Alertas", "Mensaje" };

        void DrawBus()
        {
            BusTopBar(new Rect(0, 0, W, 56));
            var left = new Rect(10, 70, 370, H - 80);
            var right = new Rect(W - 450, 70, 440, H - 80);
            var center = new Rect(left.xMax + 10, 70, right.x - left.xMax - 20, H - 80);
            var link = _app.Link;
            var head = new Rect(center.x, center.y, center.width, 60);
            _blockers.Add(head);
            Fill(head, PanelCol, 3);
            string scene = ScenarioBuilder.Labels.TryGetValue(_app.SceneKey, out var sl) ? sl : _app.SceneKey;
            GUI.Label(new Rect(head.x + 14, head.y + 6, head.width - 28, 26), $"<b>{scene}</b>", _title);
            GUI.Label(new Rect(head.x + 14, head.y + 32, head.width - 28, 22),
                $"Escenario {(string.IsNullOrEmpty(link.Scenario) ? "—" : link.Scenario)}{(string.IsNullOrEmpty(link.Phase) ? "" : " · fase " + link.Phase)} · día {(link.Day?.ToString() ?? "—")}{(link.TotalDays != null ? " / " + link.TotalDays : "")}", _small);
            BusLeftPanel(left);
            BusRightPanel(right);
            BusChartPanel(new Rect(center.x, center.yMax - 250, center.width, 250));
            if (ShowHelp) HelpOverlay(true);
        }

        void BusTopBar(Rect r)
        {
            _blockers.Add(r);
            Fill(r, BarCol, 0);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), Accent, 0);
            GUI.Label(new Rect(16, 7, 400, 24), "AEGIS  <color=#8F9BAD>|</color>  MONITOR DE SALUD DE LA TRIPULACIÓN", new GUIStyle(_title) { fontSize = 15 });
            GUI.Label(new Rect(16, 29, 400, 20), "Enlace con el motor AEGIS del equipo (PC 1)", _small);
            var link = _app.Link;
            GUILayout.BeginArea(new Rect(440, 10, W - 450, 42));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SIMULACIÓN LOCAL", _btn, GUILayout.Width(220), GUILayout.Height(36))) _app.SetMode(SpaceAppsSimulation.Mode.Local);
            if (GUILayout.Button(_app.InteriorView ? "EXTERIOR" : "INTERIOR", _btn, GUILayout.Width(140), GUILayout.Height(36))) _app.ToggleInterior();
            if (GUILayout.Button("?", _btn, GUILayout.Width(36), GUILayout.Height(36))) ShowHelp = true;
            string st;
            switch (link.Client.Status)
            {
                case Bus.AegisBusClient.State.Connected: st = $"<color=#7ee08a>● Conectado</color> ({(string.IsNullOrEmpty(link.Client.NegotiatedProtocol) ? "sin subprotocolo" : link.Client.NegotiatedProtocol)})"; break;
                case Bus.AegisBusClient.State.Connecting: st = "<color=#ffd24a>● Conectando…</color>"; break;
                case Bus.AegisBusClient.State.Error: st = $"<color=#ff5a4a>● Sin conexión</color> {link.Client.LastError} (reintentando)"; break;
                default: st = "<color=#a9bce0>● Desconectado</color>"; break;
            }
            GUILayout.Label($"{st} · {link.Url} · mensajes: {link.Client.Received}", new GUIStyle(_label) { alignment = TextAnchor.MiddleLeft }, GUILayout.Height(36));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void BusLeftPanel(Rect r)
        {
            _blockers.Add(r);
            var link = _app.Link;
            GUILayout.BeginArea(r, _panel);
            _scrollBL = GUILayout.BeginScrollView(_scrollBL);
            GUILayout.Label("CONEXIÓN CON EL MOTOR (PC 1)", _title);
            if (_urlEdit == null) _urlEdit = link.Url;
            GUILayout.Label("WebSocket:", _small);
            _urlEdit = GUILayout.TextField(_urlEdit, new GUIStyle(GUI.skin.textField) { fontSize = 15 });
            GUILayout.Label($"Subprotocolo: <b>{link.Subprotocol}</b>", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Conectar", _btn, GUILayout.Height(32))) { link.Url = _urlEdit.Trim(); link.Connect(); }
            if (GUILayout.Button("Desconectar", _btn, GUILayout.Height(32))) link.Disconnect();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Recargar mapeo (aegis_bus.json)", _btn, GUILayout.Height(30))) { link.LoadConfig(); _urlEdit = link.Url; }
            GUILayout.Label("Formato: ws://IP_DE_PC1:8001/ws. El motor manda el día, el escenario y la tripulación; " +
                            "cambiar el día en el dashboard cambia esta escena.", _small);
            GUILayout.Space(8);
            GUILayout.Label("TRIPULACIÓN (DEL MOTOR)", _title);
            if (link.Crew.Count == 0) GUILayout.Label("Esperando datos del bus…", _label);
            for (int i = 0; i < link.Crew.Count; i++)
            {
                var a = link.Crew[i];
                var card = GUILayoutUtility.GetRect(10, 64, GUILayout.ExpandWidth(true));
                bool sel = i == _app.BusSelected;
                Fill(new Rect(card.x, card.y, card.width, 58), sel ? CardHi : CardCol, 3);
                Fill(new Rect(card.x + 12, card.y + 12, 14, 14), _app.BusLed(a), 7);
                GUI.Label(new Rect(card.x + 34, card.y + 4, card.width - 40, 22), $"<b>{a.Name}</b>", _label);
                GUI.Label(new Rect(card.x + 34, card.y + 28, card.width - 40, 22), $"Dosis {Fmt(a.DoseMsv, "0")} mSv · sueño {Fmt(a.SleepH, "0.0")} h", _small);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) { _app.BusSelected = i; _app.Dirty = true; }
            }
            GUILayout.Space(8);
            GUILayout.Label("En este modo la escena solo representa lo que calcula el motor: no genera diagnósticos, " +
                            "tratamientos ni umbrales propios. Los colores del cuerpo son una escala visual relativa al inicio de la misión.", _small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void BusRightPanel(Rect r)
        {
            _blockers.Add(r);
            var link = _app.Link;
            GUILayout.BeginArea(r, _panel);
            _busTab = GUILayout.Toolbar(_busTab, BusTabs, _tabStyle, GUILayout.Height(32));
            _scrollBR = GUILayout.BeginScrollView(_scrollBR);
            if (_busTab == 0)
            {
                if (link.Crew.Count == 0) GUILayout.Label("Sin datos de tripulación todavía.", _label);
                else
                {
                    var a = link.Crew[Mathf.Clamp(_app.BusSelected, 0, link.Crew.Count - 1)];
                    GUILayout.Label($"<b>{a.Name}</b> · día {(link.Day?.ToString() ?? "—")}", _title);
                    float bone = a.BonePct ?? 100f, aer = a.AerobicPct ?? 100f;
                    Tiles(
                        new T("radiation", "Dosis acumulada", $"{Fmt(a.DoseMsv, "0")} mSv", (float)(a.DoseMsv ?? 0), 0, 600, 0, 1000, "referencia del motor"),
                        new T("moon", "Sueño", $"{Fmt(a.SleepH, "0.0")} h", (float)(a.SleepH ?? 8), 7, 9, 3, 10, "referencia del motor"),
                        new T("bone0", "Densidad ósea", a.BonePct is float bp ? $"{bp:0.0} % inicio" : Fmt(a.BoneRaw, "0.###"), bone, 90, 101, 70, 101, "% del inicio"),
                        new T("battery", "Capacidad aeróbica", a.AerobicPct is float ap ? $"{ap:0.0} % inicio" : Fmt(a.AerobicRaw, "0.##"), aer, 85, 101, 60, 101, "% del inicio"));
                    if (!string.IsNullOrEmpty(link.SolarEvent)) GUILayout.Label($"<color=#ff9a4a>Evento solar: {link.SolarEvent}</color>", _label);
                    GUILayout.Label("Valores tal como los envía el motor; las referencias y sus fuentes están en el dashboard.", _small);
                }
            }
            else if (_busTab == 1)
            {
                if (link.Alerts.Count == 0) GUILayout.Label("El motor no informa alertas.", _label);
                foreach (var al in link.Alerts)
                {
                    string col = al.IsRed ? "#ff5a4a" : "#ffd24a";
                    GUILayout.Label($"<color={col}>●</color> {al.Text}" +
                                    (string.IsNullOrEmpty(al.Level) ? "" : $" <color=#8090b0>[{al.Level}]</color>") +
                                    (string.IsNullOrEmpty(al.Source) ? "" : $"\n<color=#8090b0>Fuente: {al.Source}</color>"), _label);
                }
            }
            else
            {
                GUILayout.Label($"Tipo: {link.LastType} {(string.IsNullOrEmpty(link.LastParseError) ? "" : "<color=#ff5a4a>" + link.LastParseError + "</color>")}", _small);
                string raw = link.LastRaw ?? "";
                if (raw.Length > 4000) raw = raw.Substring(0, 4000) + " …";
                GUILayout.TextArea(raw, GUILayout.ExpandHeight(true));
                GUILayout.Label("Si algún campo no aparece, agregar su nombre en StreamingAssets/aegis_bus.json y pulsar Recargar mapeo.", _small);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string Fmt(double? v, string f) => v?.ToString(f) ?? "—";

        void BusChartPanel(Rect r)
        {
            _blockers.Add(r);
            var link = _app.Link;
            GUILayout.BeginArea(r, _panel);
            GUILayout.Label("TENDENCIA RECIBIDA DEL MOTOR", _title);
            var area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndArea();
            _chartTimer -= Time.unscaledDeltaTime;
            if (_app.Dirty && _chartTimer <= 0f && link.Crew.Count > 0)
            {
                var a = link.Crew[Mathf.Clamp(_app.BusSelected, 0, link.Crew.Count - 1)];
                _a.Clear(); _b.Clear(); _c.Clear(); _d.Clear(); _legend.Clear();
                int maxDay = 1;
                var last = (dose: 0f, sleep: 0f, bone: 0f, aer: 0f);
                foreach (var kv in a.History) maxDay = Mathf.Max(maxDay, kv.Key);
                for (int dIdx = 0; dIdx <= maxDay; dIdx++)
                {
                    if (a.History.TryGetValue(dIdx, out var v)) last = v;
                    _a.Add(last.bone); _b.Add(last.aer); _c.Add(last.sleep * 10f); _d.Add(last.dose / 10f);
                }
                _chart.Begin(0, 110, Mathf.Max(maxDay, link.TotalDays ?? maxDay));
                Legend(("Densidad ósea % inicio", new Color(0.95f, 0.9f, 0.75f)), ("Capacidad aeróbica % inicio", new Color(0.4f, 0.75f, 1f)),
                       ("Sueño h ×10", new Color(0.6f, 0.7f, 1f)), ("Dosis mSv ÷10", new Color(1f, 0.35f, 0.6f)));
                _chart.Series(_a, _legend[0].Item2); _chart.Series(_b, _legend[1].Item2);
                _chart.Series(_c, _legend[2].Item2); _chart.Series(_d, _legend[3].Item2);
                _chart.End();
                _chartTimer = 0.25f; _app.Dirty = false;
            }
            var img = new Rect(r.x + area.x, r.y + area.y, area.width, area.height - 24);
            GUI.DrawTexture(img, _chart.Tex, ScaleMode.StretchToFill);
            DrawLegend(img);
        }
    }
}
