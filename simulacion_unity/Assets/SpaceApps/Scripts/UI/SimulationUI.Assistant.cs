using SpaceApps.Assistant;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>Panel de ASTRA: rostro holográfico, respuesta, preguntas rápidas, texto/micrófono y valoración.</summary>
    public partial class SimulationUI
    {
        /// <summary>True mientras se escribe en el cuadro de ASTRA (la barra espaciadora no pausa la simulación).</summary>
        public static bool TextFocus;

        MedAssistant _astra;
        string _astraInput = "";
        Vector2 _astraScroll;
        bool _pttMouse;

        static readonly (string label, string question)[] Quick =
        {
            ("Diagnóstico", "¿Cómo está {0}? Dame su diagnóstico."),
            ("¿Qué hacer?", "¿Qué debe hacer la tripulación ahora para cuidar su salud?"),
            ("Explicar", "Explica de forma sencilla, para el público, el problema más grave de {0}: causas, efectos y cómo afecta a la misión."),
            ("Misión", "¿Cómo va la misión y qué viene ahora?"),
        };

        static string EmotionLabel(Emotion e)
        {
            switch (e)
            {
                case Emotion.Amable: return "AMABLE";
                case Emotion.Feliz: return "CONTENTA";
                case Emotion.Preocupada: return "PREOCUPADA";
                case Emotion.Firme: return "FIRME";
                case Emotion.Alerta: return "ALERTA";
                case Emotion.Pensando: return "PENSANDO";
                default: return "ATENTA";
            }
        }

        void AssistantPanel(Rect r)
        {
            if (_astra == null) _astra = _app.GetComponent<MedAssistant>();
            Fill(r, new Color(0.03f, 0.05f, 0.08f, 1f), 4);
            Outline(r, new Color(Accent.r, Accent.g, Accent.b, 0.5f), 1, 4);
            if (_astra == null || _astra.Face == null) { GUI.Label(r, "Iniciando ASTRA…", _center); return; }
            float w = r.width - 16, x = r.x + 8, y = r.y + 6;
            var ear = _astra.Ear;
            GUI.Label(new Rect(x, y, w - 184, 20), "ASTRA  <color=#8F9BAD>· IA MÉDICA</color>", _caps);
            bool canTalk = ear != null && ear.Available;
            GUI.enabled = canTalk;
            if (GUI.Button(new Rect(r.xMax - 184, y - 1, 88, 20), canTalk && ear.HandsFree ? "● EN VIVO" : "CONVERSAR",
                    new GUIStyle(canTalk && ear.HandsFree ? _btnGreen : _btn) { fontSize = 10, padding = new RectOffset(2, 2, 2, 2) }) && canTalk)
            {
                ear.HandsFree = !ear.HandsFree;
                if (ear.HandsFree && !ear.Ready) ear.Status = "Cargando reconocimiento de voz…";
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.xMax - 92, y - 1, 84, 20), _astra.Voice.Enabled ? "VOZ: SÍ" : "VOZ: NO", new GUIStyle(_astra.Voice.Enabled ? _btnSel : _btn) { fontSize = 10, padding = new RectOffset(2, 2, 2, 2) }))
            {
                _astra.Voice.Enabled = !_astra.Voice.Enabled;
                if (!_astra.Voice.Enabled) _astra.Voice.Stop();
            }
            y += 24;

            // Rostro holográfico
            var face = new Rect(x, y, w, w * 0.5f);
            GUI.DrawTexture(face, _astra.Face.Texture, ScaleMode.ScaleAndCrop);
            Outline(face, new Color(0.2f, 0.45f, 0.7f, 0.6f), 1, 3);
            bool listening = ear != null && ear.HandsFree;
            string state = ear != null && ear.Recording ? "<color=#FF8A80>● TE ESCUCHO…</color>" :
                           ear != null && ear.Transcribing ? "<color=#FFD27F>TRANSCRIBIENDO…</color>" :
                           _astra.Busy ? "PENSANDO…" : _astra.Voice.IsSpeaking ? "HABLANDO" :
                           listening ? "<color=#7FE0A0>ESCUCHANDO</color>" : "EN ESPERA";
            if (ear != null && (listening || ear.Recording))
            {
                var bar = new Rect(face.center.x - 40, face.yMax - 13, 80, 4);
                Fill(bar, new Color(1f, 1f, 1f, 0.12f), 2);
                Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(ear.InputLevel), bar.height), ear.Recording ? new Color(1f, 0.45f, 0.4f) : new Color(0.45f, 0.85f, 0.6f), 2);
            }
            GUI.Label(new Rect(face.x + 8, face.yMax - 20, 200, 18), $"<color=#7FD3FF>● {EmotionLabel(_astra.Face.Current)}</color>", new GUIStyle(_small) { fontSize = 10, fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(face.xMax - 168, face.yMax - 20, 160, 18), $"<color=#8F9BAD>{state}</color>", new GUIStyle(_small) { fontSize = 10, alignment = TextAnchor.UpperRight });
            y = face.yMax + 6;

            // Respuesta
            var box = new Rect(x, y, w, 96);
            Fill(box, CardCol, 3);
            string text = ear != null && ear.Recording ? "<color=#FF8A80>Te escucho… (termina de hablar y respondo)</color>" :
                          ear != null && ear.Transcribing ? "<color=#FFD27F>Entendiendo lo que dijiste…</color>" :
                          (string.IsNullOrEmpty(_astra.LastQuestion) ? "" : $"<color=#8F9BAD>{(_astra.LastByVoice ? "Tú (voz)" : "Tú")}: {_astra.LastQuestion}</color>\n") +
                          (_astra.Busy ? "<color=#8F9BAD>Analizando los datos de la tripulación…</color>" : _astra.LastAnswer);
            var st = new GUIStyle(_label) { fontSize = 12, wordWrap = true };
            float th = st.CalcHeight(new GUIContent(text), w - 26);
            _astraScroll = GUI.BeginScrollView(new Rect(box.x + 4, box.y + 3, box.width - 6, box.height - 6), _astraScroll, new Rect(0, 0, w - 26, Mathf.Max(th, box.height - 8)));
            GUI.Label(new Rect(2, 0, w - 26, th), text, st);
            GUI.EndScrollView();
            y = box.yMax + 5;

            // Preguntas rápidas
            float bw = (w - 3 * 4) / 4f;
            var name = _app.Sim.Crew[_app.Selected].Name;
            for (int i = 0; i < Quick.Length; i++)
                if (GUI.Button(new Rect(x + i * (bw + 4), y, bw, 22), Quick[i].label, new GUIStyle(_btn) { fontSize = 11, padding = new RectOffset(2, 2, 2, 2) }) && !_astra.Busy)
                    _astra.Ask(string.Format(Quick[i].question, name));
            y += 27;

            // Pregunta escrita o por voz
            GUI.SetNextControlName("astra_in");
            var inputRect = new Rect(x, y, w - 128, 26);
            var tf = new GUIStyle(GUI.skin.textField) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "astra_in";
            _astraInput = GUI.TextField(inputRect, _astraInput, 240, tf);
            if (string.IsNullOrEmpty(_astraInput) && GUI.GetNameOfFocusedControl() != "astra_in")
                GUI.Label(new Rect(inputRect.x + 6, inputRect.y + 4, inputRect.width, 20), "<color=#5E6878>Pregúntale a ASTRA…</color>", new GUIStyle(_small) { fontSize = 12 });
            TextFocus = GUI.GetNameOfFocusedControl() == "astra_in";
            bool send = GUI.Button(new Rect(inputRect.xMax + 4, y, 66, 26), "ENVIAR", new GUIStyle(_btnSel) { fontSize = 11 });
            if ((send || enter) && !string.IsNullOrWhiteSpace(_astraInput) && !_astra.Busy)
            {
                _astra.Ask(_astraInput);
                _astraInput = "";
                if (enter) Event.current.Use();
            }
            // HABLAR: mantener presionado (o la tecla F2) mientras se habla
            var micRect = new Rect(inputRect.xMax + 74, y, 54, 26);
            var ev = Event.current;
            if (canTalk && ev.type == EventType.MouseDown && ev.button == 0 && micRect.Contains(ev.mousePosition)) { _pttMouse = true; ev.Use(); }
            if (ev.rawType == EventType.MouseUp) _pttMouse = false;
            if (ear != null) ear.PushToTalkUI = _pttMouse;
            bool ptt = ear != null && ear.PushToTalkActive;
            GUI.enabled = canTalk;
            GUI.Box(micRect, ptt ? "● HABLA" : "HABLAR", new GUIStyle(ptt ? _btnRed : _btn) { fontSize = 11 });
            GUI.enabled = true;
            y += 31;

            // Valoración (aprendizaje) y estado
            if (_astra.CanRate)
            {
                GUI.Label(new Rect(x, y, 80, 20), "¿Te sirvió?", new GUIStyle(_small) { fontSize = 11 });
                if (GUI.Button(new Rect(x + 72, y, 40, 19), "SÍ", new GUIStyle(_btnGreen) { fontSize = 10, padding = new RectOffset(1, 1, 1, 1) })) _astra.Feedback(true);
                if (GUI.Button(new Rect(x + 116, y, 40, 19), "NO", new GUIStyle(_btnRed) { fontSize = 10, padding = new RectOffset(1, 1, 1, 1) })) _astra.Feedback(false);
            }
            string mic = ear == null ? "" : !string.IsNullOrEmpty(ear.Status) ? ear.Status + " · " : ear.HandsFree ? "Manos libres · F2 = hablar · " : "";
            GUI.Label(new Rect(x + (_astra.CanRate ? 162 : 0), y + 1, w - (_astra.CanRate ? 162 : 0), 18),
                $"<color=#6F7A8A>{mic}{_astra.Source.Replace("IA local · ", "IA local: ").Replace(":latest", "")} · {_astra.Learned} ej.</color>", new GUIStyle(_small) { fontSize = 10, wordWrap = false, clipping = TextClipping.Clip });
        }
    }
}
