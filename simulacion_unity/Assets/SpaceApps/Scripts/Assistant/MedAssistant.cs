using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SpaceApps.Bus;
using UnityEngine;

namespace SpaceApps.Assistant
{
    /// <summary>
    /// ASTRA: asistente médica de a bordo (enfermera e IA de triaje).
    ///  - Responde con el modelo local de Ollama usando SIEMPRE los datos actuales de la simulación.
    ///  - Si Ollama no está, responde con su motor médico propio (MedicalKnowledge).
    ///  - Habla (voz de Windows), mueve boca, ojos y cejas, y expresa emociones.
    ///  - Avisa sola cuando aparece un problema crítico o una tormenta solar.
    ///  - Aprende de la valoración: las respuestas marcadas como útiles se guardan en IA/memoria_astra.jsonl
    ///    y se usan como ejemplos en las siguientes preguntas (y para crear el modelo "astra").
    /// </summary>
    public class MedAssistant : MonoBehaviour
    {
        public IAstraFace Face { get; private set; }
        public SpeechVoice Voice { get; private set; }
        public VoiceListener Ear { get; private set; }
        public bool LastByVoice { get; private set; }
        public readonly OllamaClient Ollama = new OllamaClient();

        public string Status = "Iniciando…";
        public string LastQuestion = "", LastAnswer = "", Source = "";
        public Emotion Emotion = Emotion.Neutral;
        public bool Busy { get; private set; }
        public bool AutoAlerts = true;
        public string Model { get; private set; }
        public int Learned => _examples.Count;
        public string MemoryPath { get; private set; }

        SpaceAppsSimulation _app;
        string[] _preferred = { "astra", "qwen2.5:3b", "llama3.2:3b", "qwen2.5:7b" };
        float _temperature = 0.4f;
        int _maxTokens = 220;
        string _persona = DefaultPersona;
        readonly List<(string role, string content)> _history = new List<(string, string)>();
        readonly List<(string q, string a)> _examples = new List<(string, string)>();
        readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
        readonly HashSet<string> _announced = new HashSet<string>();
        float _checkTimer = 20f, _alertTimer, _greetTimer = 2.5f, _speCooldown, _idle;
        int _lastDay;
        bool _feedbackGiven;
        string _faceStyle = "realista";

        public const string DefaultPersona =
            "Eres ASTRA, la asistente médica de a bordo (enfermera e inteligencia artificial de triaje) de una misión espacial simulada para NASA Space Apps. " +
            "Cuidas la vida de la tripulación cuando no hay un médico disponible. Personalidad: cálida, atenta, profesional y clara. " +
            "Hablas en español neutro, con frases cortas, porque tus respuestas se leen en voz alta. " +
            "Explicas para que el público general entienda: qué pasa, por qué pasa (causas), qué efecto tiene en el cuerpo y en la misión, y qué deben hacer. " +
            "Usa SOLO los datos del ESTADO DE LA SIMULACIÓN que recibes; si un dato no está, dilo. No inventes cifras ni nombres. " +
            "Si algo es crítico, sé firme y directa. Recuerda cuando corresponda que es una simulación educativa y no reemplaza a un médico real. " +
            "Empieza por lo más grave. No recites listas de cifras: menciona como máximo dos números importantes y explica qué significan. " +
            "Formato obligatorio: empieza con UNA etiqueta de emoción entre corchetes: [AMABLE], [PREOCUPADA], [FIRME], [ALERTA], [NEUTRAL] o [FELIZ]. " +
            "Después responde en máximo 4 oraciones, sin listas, sin asteriscos y sin markdown.";

        void Start()
        {
            _app = GetComponent<SpaceAppsSimulation>();
            Voice = gameObject.AddComponent<SpeechVoice>();
            Ear = gameObject.AddComponent<VoiceListener>();
            Ear.OnText += t => Ask(t, true);
            Ear.AssistantSpeaking = () => Voice.IsSpeaking;
            Ear.AssistantBusy = () => Busy;
            Ear.AssistantOutput = () => Voice.OutputRms;
            Ear.StopAssistant = () => Voice.Stop();
            Ear.PromptProvider = WhisperPrompt;
            LoadConfig();
            // Rostro realista (avatar Rocketbox) o, si no está o se pide, el holograma de líneas
            if (_faceStyle != "lineas") Face = AvatarFace.TryCreate();
            if (Face == null) Face = HoloFace.Create();
            LoadMemory();
            _ = CheckOllama();
        }

        // ------------------------------------------------------------ configuración y memoria

        void LoadConfig()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "astra.json");
                if (File.Exists(path))
                {
                    var cfg = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                    Ollama.BaseUrl = MiniJson.Text(MiniJson.Path(cfg, "ollama_url")) ?? Ollama.BaseUrl;
                    if (MiniJson.Path(cfg, "modelos") is List<object> ms) _preferred = ms.Select(m => m.ToString()).ToArray();
                    _temperature = (float)(MiniJson.Number(MiniJson.Path(cfg, "temperatura")) ?? _temperature);
                    _maxTokens = (int)(MiniJson.Number(MiniJson.Path(cfg, "max_tokens")) ?? _maxTokens);
                    if (MiniJson.Path(cfg, "voz") is bool v) Voice.Enabled = v;
                    if (MiniJson.Path(cfg, "avisos_automaticos") is bool a) AutoAlerts = a;
                    string motor = MiniJson.Text(MiniJson.Path(cfg, "voz_motor"));
                    if (motor != null) Voice.PreferPiper = motor.ToLowerInvariant() != "windows";
                    Voice.PiperSpeed = (float)(MiniJson.Number(MiniJson.Path(cfg, "voz_velocidad")) ?? Voice.PiperSpeed);
                    Voice.Volume = Mathf.Clamp01((float)(MiniJson.Number(MiniJson.Path(cfg, "voz_volumen")) ?? Voice.Volume));
                    _faceStyle = (MiniJson.Text(MiniJson.Path(cfg, "rostro")) ?? "realista").ToLowerInvariant();
                    if (MiniJson.Path(cfg, "mic_manos_libres") is bool hf) Ear.HandsFree = hf;
                    if (MiniJson.Path(cfg, "mic_interrumpir") is bool bi) Ear.AllowBargeIn = bi;
                    if (MiniJson.Path(cfg, "mic_palabra_clave") is bool ww) Ear.RequireWakeWord = ww;
                    Ear.Sensitivity = (float)(MiniJson.Number(MiniJson.Path(cfg, "mic_sensibilidad")) ?? Ear.Sensitivity);
                }
                string persona = Path.Combine(Application.streamingAssetsPath, "astra_persona.txt");
                if (File.Exists(persona)) _persona = File.ReadAllText(persona).Trim();
            }
            catch (Exception e) { Debug.LogWarning("[ASTRA] configuración: " + e.Message); }
        }

        /// <summary>Carpeta IA del proyecto (junto a LEEME.md) o, si no se encuentra, junto a la app.</summary>
        static string IaFolder()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 5 && dir != null; i++, dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "LEEME.md"))) return Path.Combine(dir.FullName, "IA");
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "IA"));
        }

        void LoadMemory()
        {
            string dir = IaFolder();
            Directory.CreateDirectory(dir);
            MemoryPath = Path.Combine(dir, "memoria_astra.jsonl");
            _examples.Clear();
            foreach (var file in new[] { Path.Combine(dir, "ejemplos_base.jsonl"), MemoryPath })
            {
                if (!File.Exists(file)) continue;
                foreach (var line in File.ReadAllLines(file))
                {
                    try
                    {
                        if (!(MiniJson.Parse(line) is Dictionary<string, object> d)) continue;
                        bool util = d.TryGetValue("util", out var u) && u is bool b && b;
                        string q = MiniJson.Text(MiniJson.Path(d, "pregunta")), a = MiniJson.Text(MiniJson.Path(d, "respuesta"));
                        if (util && !string.IsNullOrEmpty(q) && !string.IsNullOrEmpty(a))
                        {
                            _examples.RemoveAll(x => x.q == q);
                            _examples.Add((q, a));
                        }
                        else if (d.TryGetValue("util", out var u2) && u2 is bool b2 && !b2) _examples.RemoveAll(x => x.q == q);
                    }
                    catch { /* línea dañada: se ignora */ }
                }
            }
        }

        void AppendMemory(string json)
        {
            try { File.AppendAllText(MemoryPath, json + "\n", new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogWarning("[ASTRA] memoria: " + e.Message); }
        }

        /// <summary>Valoración de la última respuesta: las útiles se aprenden como ejemplos.</summary>
        public void Feedback(bool useful)
        {
            if (string.IsNullOrEmpty(LastAnswer) || _feedbackGiven) return;
            _feedbackGiven = true;
            string tagged = $"[{Emotion.ToString().ToUpperInvariant()}] {LastAnswer}";
            AppendMemory($"{{\"tipo\":\"valoracion\",\"fecha\":{OllamaClient.Q(DateTime.Now.ToString("s"))},\"pregunta\":{OllamaClient.Q(LastQuestion)},\"respuesta\":{OllamaClient.Q(tagged)},\"util\":{(useful ? "true" : "false")}}}");
            if (useful) { _examples.RemoveAll(x => x.q == LastQuestion); _examples.Add((LastQuestion, tagged)); }
            else _examples.RemoveAll(x => x.q == LastQuestion);
            Status = useful ? "Gracias: respuesta guardada como ejemplo" : "Anotado: la evitaré como ejemplo";
        }

        public bool CanRate => !string.IsNullOrEmpty(LastQuestion) && !_feedbackGiven && !Busy;

        // ------------------------------------------------------------ Ollama

        async Task CheckOllama()
        {
            bool ok = await Ollama.Refresh();
            _main.Enqueue(() =>
            {
                string prev = Model;
                Model = ok ? Ollama.Pick(_preferred) : null;
                if (Model != null && Model != prev) { string m = Model; _ = Task.Run(() => WarmUp(m)); }
                Source = Model != null ? $"IA local · {Model}" : ok ? "Ollama sin modelos: motor médico propio" : "Motor médico propio (Ollama no disponible)";
                if (!Busy && Status.StartsWith("Iniciando")) Status = "Lista";
            });
        }

        // ------------------------------------------------------------ preguntas

        /// <summary>Vocabulario para que Whisper reconozca bien los nombres de la tripulación y los términos.</summary>
        string WhisperPrompt()
        {
            var names = _app != null && _app.Sim != null ? string.Join(", ", _app.Sim.Crew.Select(c => c.Name)) : "CMD Alfa, PIL Bravo, MED Charlie, ING Delta";
            return $"Conversación con ASTRA, la asistente médica de a bordo. Tripulación: {names}. " +
                   "Temas: diagnóstico, visión, SANS, radiación, densidad ósea, músculo, sueño, estrés, CO2, misión a Marte, la Luna.";
        }

        public void Ask(string question) => Ask(question, false);

        public void Ask(string question, bool byVoice)
        {
            question = (question ?? "").Trim();
            if (question.Length == 0 || Busy) return;
            LastByVoice = byVoice;
            Busy = true;
            _feedbackGiven = false;
            LastQuestion = question;
            Status = "Pensando…";
            Face.Thinking = true;
            Face.SetEmotion(Emotion.Pensando);
            Voice.Stop();
            var sim = _app.Sim;
            int sel = _app.Selected;
            string snapshot = MedicalKnowledge.Snapshot(sim, sel);
            var local = MedicalKnowledge.Answer(sim, sel, question);

            if (Model == null)
            {
                Finish(local.emo, local.text, "motor médico", question);
                return;
            }
            var msgs = BuildMessages(_persona, snapshot, RelevantExamples(question, 3), _history.Skip(Math.Max(0, _history.Count - 6)), question, local.text);
            string model = Model;
            Task.Run(async () =>
            {
                try
                {
                    var t0 = DateTime.Now;
                    string raw = await Ollama.Chat(model, msgs, _temperature, _maxTokens);
                    var (emo, text) = Parse(raw, local.emo);
                    if (Severity(local.emo) > Severity(emo)) emo = local.emo;   // la gravedad la decide el motor médico
                    if (text.Length < 15) { emo = local.emo; text = local.text; }
                    double secs = (DateTime.Now - t0).TotalSeconds;
                    _main.Enqueue(() => Finish(emo, text, $"{model} · {secs:0.0} s", question));
                }
                catch (Exception e)
                {
                    _main.Enqueue(() =>
                    {
                        Source = "Motor médico propio (Ollama falló: " + e.Message + ")";
                        Finish(local.emo, local.text, "motor médico", question);
                        _ = CheckOllama();
                    });
                }
            });
        }

        /// <summary>
        /// Arma la conversación para Ollama. Los "hechos verificados" vienen del motor médico propio
        /// (ecuaciones de la simulación + base de conocimiento): el modelo de lenguaje solo los redacta
        /// en lenguaje amigable, así un modelo pequeño no inventa causas ni tratamientos.
        /// </summary>
        public static List<(string, string)> BuildMessages(string persona, string snapshot, IEnumerable<(string q, string a)> examples,
            IEnumerable<(string, string)> history, string question, string facts)
        {
            var msgs = new List<(string, string)> { ("system", persona), ("system", snapshot) };
            foreach (var ex in examples) { msgs.Add(("user", ex.q)); msgs.Add(("assistant", ex.a)); }
            msgs.AddRange(history);
            if (!string.IsNullOrEmpty(facts))
                msgs.Add(("system", "HECHOS VERIFICADOS POR EL MOTOR MÉDICO PARA ESTA PREGUNTA: " + facts +
                    " Responde usando estos hechos y el estado de la simulación. Puedes simplificar y usar una comparación cotidiana, " +
                    "pero NO agregues causas, tratamientos, comparaciones con cifras ni números que no estén aquí."));
            msgs.Add(("user", question));
            return msgs;
        }

        static int Severity(Emotion e) => e == Emotion.Alerta ? 4 : e == Emotion.Firme ? 3 : e == Emotion.Preocupada ? 2 : 0;

        /// <summary>Carga el modelo en memoria apenas se detecta, para que la primera pregunta no espere.</summary>
        async Task WarmUp(string model)
        {
            try
            {
                _main.Enqueue(() => { if (!Busy) Status = "Cargando la IA local…"; });
                await Ollama.Chat(model, new List<(string, string)> { ("system", _persona), ("user", "Hola") }, 0.2f, 1).ConfigureAwait(false);
                _main.Enqueue(() => { if (!Busy && Status.StartsWith("Cargando")) Status = "Lista"; });
            }
            catch (Exception e) { Debug.LogWarning("[ASTRA] precarga: " + e.Message); }
        }

        IEnumerable<(string q, string a)> RelevantExamples(string question, int n)
        {
            var words = new HashSet<string>(MedicalKnowledge.Norm(question).Split(' ', ',', '.', '?', '¿').Where(w => w.Length > 3));
            return _examples
                .Select(ex => (ex, score: MedicalKnowledge.Norm(ex.q).Split(' ').Count(w => words.Contains(w))))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .Take(n)
                .Select(x => x.ex);
        }

        static readonly Regex Tag = new Regex(@"^\s*\[(?<e>[A-Za-zÁÉÍÓÚáéíóú]+)\]\s*", RegexOptions.Compiled);

        static (Emotion, string) Parse(string raw, Emotion fallback)
        {
            string text = raw ?? "";
            var emo = fallback;
            var m = Tag.Match(text);
            if (m.Success)
            {
                emo = ToEmotion(m.Groups["e"].Value, fallback);
                text = text.Substring(m.Length);
            }
            text = Regex.Replace(text, @"\[(AMABLE|PREOCUPADA|FIRME|ALERTA|NEUTRAL|FELIZ)\]", "", RegexOptions.IgnoreCase);
            text = text.Replace("*", "").Replace("#", "").Replace("\n\n", "\n").Trim();
            if (text.Length > 700) text = text.Substring(0, text.LastIndexOf('.', 699) > 200 ? text.LastIndexOf('.', 699) + 1 : 700);
            return (emo, text);
        }

        static Emotion ToEmotion(string s, Emotion fallback)
        {
            switch (MedicalKnowledge.Norm(s))
            {
                case "amable": return Emotion.Amable;
                case "preocupada": return Emotion.Preocupada;
                case "firme": return Emotion.Firme;
                case "alerta": return Emotion.Alerta;
                case "feliz": return Emotion.Feliz;
                case "neutral": return Emotion.Neutral;
                default: return fallback;
            }
        }

        void Finish(Emotion emo, string text, string source, string question)
        {
            Busy = false;
            Face.Thinking = false;
            Emotion = emo;
            Face.SetEmotion(emo);
            LastAnswer = text;
            Status = "Respuesta · " + source;
            _history.Add(("user", question));
            _history.Add(("assistant", $"[{emo.ToString().ToUpperInvariant()}] {text}"));
            Voice.Speak(text, emo);
            AppendMemory($"{{\"tipo\":\"conversacion\",\"fecha\":{OllamaClient.Q(DateTime.Now.ToString("s"))},\"dia_mision\":{_app.Sim.Day},\"tripulante\":{OllamaClient.Q(_app.Sim.Crew[_app.Selected].Name)},\"fuente\":{OllamaClient.Q(source)},\"pregunta\":{OllamaClient.Q(question)},\"respuesta\":{OllamaClient.Q($"[{emo.ToString().ToUpperInvariant()}] {text}")}}}");
        }

        /// <summary>Habla sin pregunta (avisos automáticos y saludo).</summary>
        void Say(Emotion emo, string text)
        {
            if (Busy) return;
            Emotion = emo;
            Face.SetEmotion(emo);
            LastAnswer = text;
            LastQuestion = "";
            Status = "Aviso automático";
            Voice.Speak(text, emo);
        }

        // ------------------------------------------------------------ ciclo

        void Update()
        {
            while (_main.TryDequeue(out var a)) a();
            if (_app == null || _app.Sim == null || Face == null) return;
            Face.SpeakLevel = Voice.Level;
            // tras unos segundos en silencio vuelve a una expresión amable
            if (Voice.IsSpeaking || Busy) _idle = 0f;
            else if ((_idle += Time.deltaTime) > 7f && Face.Current != Emotion.Amable) Face.SetEmotion(Emotion.Amable);

            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f) { _checkTimer = 20f; _ = CheckOllama(); }

            if (_greetTimer > 0f)
            {
                _greetTimer -= Time.deltaTime;
                if (_greetTimer <= 0f)
                    Say(Emotion.Amable, "Hola, soy ASTRA, la asistente médica de a bordo. Vigilo la salud de toda la tripulación y te explico qué pasa y qué hacer. Pregúntame lo que necesites.");
            }

            var sim = _app.Sim;
            if (sim.Day < _lastDay) _announced.Clear();
            _lastDay = sim.Day;
            if (!AutoAlerts) return;

            _speCooldown -= Time.deltaTime;
            if (_app.SpeFlash > 0.95f && _speCooldown <= 0f && !Busy)
            {
                _speCooldown = 20f;
                Say(Emotion.Alerta, "¡Alerta! Tormenta solar detectada. Toda la tripulación al refugio blindado ahora mismo.");
                return;
            }
            _alertTimer -= Time.deltaTime;
            if (_alertTimer > 0f || Busy || Voice.IsSpeaking) return;
            _alertTimer = 1f;
            // un aviso por sistema (no por persona), para no repetir lo mismo cuatro veces
            foreach (var g in Advisor.Problems(sim).Where(p => p.Severity >= 0.65f).GroupBy(p => p.System))
            {
                if (_announced.Add(g.Key))
                {
                    var (emo, text) = MedicalKnowledge.Announce(sim, g.ToList());
                    Say(emo, text);
                    _alertTimer = 10f;
                    break;
                }
            }
        }
    }
}
