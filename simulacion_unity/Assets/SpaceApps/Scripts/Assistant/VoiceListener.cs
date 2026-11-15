using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Debug = UnityEngine.Debug;

namespace SpaceApps.Assistant
{
    /// <summary>
    /// Oído de ASTRA: conversación por micrófono en tiempo real, 100 % local.
    ///  - Captura el micrófono a 16 kHz y detecta la voz (VAD por energía con piso de ruido adaptativo):
    ///    sabe cuándo empiezas a hablar y cuándo terminas (0,8 s de silencio).
    ///  - Transcribe cada frase con whisper.cpp (StreamingAssets/whisper: whisper-server + modelo small),
    ///    un servidor local que mantiene el modelo cargado.
    ///  - Modos: MANOS LIBRES (escucha siempre y responde sola) y PRESIONAR PARA HABLAR (botón o F2).
    ///  - Interrupción: si hablas fuerte mientras ASTRA habla, se calla y te escucha. Estima el eco de
    ///    los parlantes para no interrumpirse con su propia voz.
    /// </summary>
    public class VoiceListener : MonoBehaviour
    {
        public bool HandsFree;
        public bool PushToTalkUI;               // botón HABLAR mantenido (lo pone el panel)
        public bool AllowBargeIn = true;
        public bool RequireWakeWord;            // manos libres: solo responde si la frase dice "ASTRA"
        public float Sensitivity = 1f;          // >1 = más sensible
        public string Status = "";
        public float InputLevel { get; private set; }
        public bool Recording { get; private set; }
        public bool Transcribing { get; private set; }
        public bool Ready { get; private set; }
        public bool Available { get; private set; }
        public bool PushToTalkActive { get; private set; }

        public event Action<string> OnText;
        public Func<string> PromptProvider;
        public Func<bool> AssistantSpeaking, AssistantBusy;
        public Func<float> AssistantOutput;
        public Action StopAssistant;

        const int Rate = 16000, Frame = 320;   // 20 ms
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
        readonly List<float> _utt = new List<float>();
        readonly Queue<float[]> _preroll = new Queue<float[]>();
        readonly float[] _frame = new float[Frame];
        int _frameFill;
        AudioClip _mic;
        string _device;
        int _readPos;
        float _floor = 0.006f, _echoGain = 0.35f, _cooldown;
        int _above, _silence, _voiced;
        bool _bargeIn, _wasSpeaking;
        Process _server;
        string _url;

        // ------------------------------------------------------------ servidor whisper

        void Start()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "whisper");
            string exe = Path.Combine(dir, "whisper-server.exe");
            string model = Directory.Exists(dir) ? FirstModel(dir) : null;
            Available = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
            if (!Available) { Status = "Micrófono: solo en Windows"; return; }
            if (!File.Exists(exe) || model == null) { Available = false; Status = "Falta el reconocedor de voz (StreamingAssets/whisper)"; return; }
            KillOrphans(exe);
            try
            {
                int port = FreePort();
                _url = $"http://127.0.0.1:{port}";
                int threads = Mathf.Clamp(SystemInfo.processorCount - 4, 4, 8);
                var psi = new ProcessStartInfo(exe, $"-m \"{model}\" -l es -t {threads} --host 127.0.0.1 --port {port} -nt")
                {
                    WorkingDirectory = dir, CreateNoWindow = true, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                _server = Process.Start(psi);
                var srv = _server;
                new Thread(() => { try { while (srv.StandardOutput.ReadLine() != null) { } } catch { } }) { IsBackground = true }.Start();
                new Thread(() => { try { while (srv.StandardError.ReadLine() != null) { } } catch { } }) { IsBackground = true }.Start();
                Status = "Cargando reconocimiento de voz…";
                Task.Run(async () =>
                {
                    for (int i = 0; i < 120 && !Ready; i++)
                    {
                        try { await Http.GetAsync(_url + "/").ConfigureAwait(false); Ready = true; }
                        catch { await Task.Delay(500).ConfigureAwait(false); }
                    }
                    if (Ready) await WarmUp().ConfigureAwait(false);
                    _main.Enqueue(() =>
                    {
                        Status = Ready ? "" : "El reconocimiento de voz no arrancó";
                        Debug.Log($"[ASTRA] oído: whisper.cpp {(Ready ? "listo" : "NO responde")} · {Path.GetFileName(model)} · {threads} hilos");
                    });
                });
            }
            catch (Exception e) { Available = false; Status = "Reconocimiento de voz: " + e.Message; }
        }

        /// <summary>Primera transcripción "en vacío": así la primera frase real no espera la carga.</summary>
        async Task WarmUp()
        {
            try
            {
                var tone = new float[Rate];
                for (int i = 0; i < tone.Length; i++) tone[i] = 0.02f * Mathf.Sin(i * 0.05f);
                using (var form = new MultipartFormDataContent())
                {
                    form.Add(new ByteArrayContent(ToWav(tone)), "file", "calentar.wav");
                    form.Add(new StringContent("json"), "response_format");
                    await Http.PostAsync(_url + "/inference", form).ConfigureAwait(false);
                }
            }
            catch { }
        }

        static string FirstModel(string dir)
        {
            var m = Directory.GetFiles(dir, "ggml-*.bin");
            return m.Length > 0 ? m[0] : null;
        }

        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        /// <summary>Cierra servidores que quedaron abiertos si la app se cerró de golpe.</summary>
        static void KillOrphans(string exe)
        {
            foreach (var p in Process.GetProcessesByName("whisper-server"))
            {
                try { if (string.Equals(Path.GetFullPath(p.MainModule.FileName), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase)) p.Kill(); }
                catch { }
            }
        }

        void OnDestroy()
        {
            try { if (_server != null && !_server.HasExited) _server.Kill(); } catch { }
            if (_mic != null && Microphone.IsRecording(_device)) Microphone.End(_device);
        }

        // ------------------------------------------------------------ micrófono

        bool StartMic()
        {
            if (Microphone.devices.Length == 0) { Status = "No se encontró un micrófono"; HandsFree = false; return false; }
            _device = Microphone.devices[0];
            _mic = Microphone.Start(_device, true, 10, Rate);
            if (_mic == null) { Status = "No se pudo abrir el micrófono (revise los permisos de Windows)"; HandsFree = false; return false; }
            _readPos = 0;
            Debug.Log("[ASTRA] micrófono: " + _device + " · " + _mic.frequency + " Hz");
            return true;
        }

        static bool F2Held()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f2Key.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.F2);
#else
            return false;
#endif
        }

        void Update()
        {
            while (_main.TryDequeue(out var a)) a();
            if (!Available) return;
            PushToTalkActive = (PushToTalkUI || F2Held()) && !SimulationUI.TextFocus;
            if (_mic == null)
            {
                InputLevel = 0f;
                if (!(HandsFree || PushToTalkActive) || !StartMic()) return;
            }

            int pos = Microphone.GetPosition(_device);
            int len = _mic.samples;
            int avail = (pos - _readPos + len) % len;
            if (avail <= 0) return;
            var data = new float[avail];
            int first = Mathf.Min(avail, len - _readPos);
            var tmp = new float[first];
            _mic.GetData(tmp, _readPos);
            Array.Copy(tmp, data, first);
            if (avail > first) { var tmp2 = new float[avail - first]; _mic.GetData(tmp2, 0); Array.Copy(tmp2, 0, data, first, avail - first); }
            _readPos = pos;

            for (int i = 0; i < data.Length; i++)
            {
                _frame[_frameFill++] = data[i];
                if (_frameFill == Frame) { ProcessFrame(); _frameFill = 0; }
            }
        }

        void ProcessFrame()
        {
            float sum = 0f;
            for (int i = 0; i < Frame; i++) sum += _frame[i] * _frame[i];
            float rms = Mathf.Sqrt(sum / Frame);
            InputLevel = Mathf.Lerp(InputLevel, Mathf.Clamp01(rms * 12f), 0.4f);
            var copy = (float[])_frame.Clone();
            bool speaking = AssistantSpeaking != null && AssistantSpeaking();
            bool busy = AssistantBusy != null && AssistantBusy();
            float outRms = AssistantOutput != null ? AssistantOutput() : 0f;
            if (_wasSpeaking && !speaking) _cooldown = 0.35f;   // cola de su propia voz
            _wasSpeaking = speaking;
            if (_cooldown > 0f) _cooldown -= Frame / (float)Rate;

            // Presionar para hablar: graba todo mientras se mantiene
            if (PushToTalkActive)
            {
                if (!Recording) { BeginUtterance(); if (speaking) StopAssistant?.Invoke(); }
                _utt.AddRange(copy);
                if (rms > 0.01f) _voiced++;
                return;
            }
            if (Recording && !HandsFree) { EndUtterance(); return; }   // se soltó el botón

            if (!HandsFree || Transcribing || (busy && !speaking) || _cooldown > 0f) { KeepPreroll(copy); return; }

            float s = 1f / Mathf.Max(0.3f, Sensitivity);
            float startThr = Mathf.Max(0.014f * s, _floor * 3.2f);
            if (speaking)
            {
                if (!AllowBargeIn) { KeepPreroll(copy); return; }
                // Eco estimado de los parlantes: la interrupción debe superarlo con holgura
                if (outRms > 0.02f && !Recording) _echoGain = Mathf.Lerp(_echoGain, Mathf.Clamp(rms / outRms, 0.02f, 2f), 0.01f);
                startThr = Mathf.Max(startThr, Mathf.Max(0.06f * s, _echoGain * outRms * 3f));
            }

            if (!Recording)
            {
                if (!speaking && rms < startThr) _floor = Mathf.Lerp(_floor, Mathf.Clamp(rms, 0.001f, 0.05f), 0.02f);
                _above = rms > startThr ? _above + 1 : 0;
                KeepPreroll(copy);
                if (_above >= (speaking ? 12 : 4))
                {
                    BeginUtterance();
                    if (speaking) { _bargeIn = true; StopAssistant?.Invoke(); }
                }
            }
            else
            {
                _utt.AddRange(copy);
                float endThr = Mathf.Max(0.009f * s, _floor * 2.2f);
                if (rms > endThr) { _voiced++; _silence = 0; } else _silence++;
                if (_silence >= 40 || _utt.Count > Rate * 15) EndUtterance();
            }
        }

        void KeepPreroll(float[] f)
        {
            _preroll.Enqueue(f);
            while (_preroll.Count > 15) _preroll.Dequeue();   // 300 ms antes de detectar la voz
        }

        void BeginUtterance()
        {
            Recording = true;
            _utt.Clear();
            foreach (var f in _preroll) _utt.AddRange(f);
            _preroll.Clear();
            _silence = 0; _voiced = 0;
            Status = "Te escucho…";
        }

        void EndUtterance()
        {
            Recording = false;
            _above = 0;
            float voicedSec = _voiced * Frame / (float)Rate;
            if (voicedSec < 0.3f) { Status = ""; _utt.Clear(); return; }
            var samples = _utt.ToArray();
            _utt.Clear();
            Transcribe(samples, _bargeIn);
            _bargeIn = false;
        }

        // ------------------------------------------------------------ transcripción

        /// <summary>Transcribe un audio (16 kHz mono) y entrega el texto como pregunta.</summary>
        public void Transcribe(float[] samples, bool interrupted = false)
        {
            if (!Ready) { Status = "El reconocimiento de voz aún está cargando…"; return; }
            Transcribing = true;
            Status = "Transcribiendo…";
            string prompt = PromptProvider != null ? PromptProvider() : "";
            bool wake = RequireWakeWord && HandsFree && !PushToTalkActive;
            Task.Run(async () =>
            {
                string text = null, err = null;
                var sw = Stopwatch.StartNew();
                try
                {
                    using (var form = new MultipartFormDataContent())
                    {
                        var file = new ByteArrayContent(ToWav(samples));
                        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                        form.Add(file, "file", "voz.wav");
                        form.Add(new StringContent("json"), "response_format");
                        form.Add(new StringContent("0.0"), "temperature");
                        form.Add(new StringContent("es"), "language");
                        if (!string.IsNullOrEmpty(prompt)) form.Add(new StringContent(prompt, Encoding.UTF8), "prompt");
                        var resp = await Http.PostAsync(_url + "/inference", form).ConfigureAwait(false);
                        string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode) err = $"whisper {(int)resp.StatusCode}";
                        else text = Bus.MiniJson.Text(Bus.MiniJson.Path(Bus.MiniJson.Parse(body), "text"));
                    }
                }
                catch (Exception e) { err = e.Message; }
                double secs = sw.Elapsed.TotalSeconds;
                _main.Enqueue(() =>
                {
                    Transcribing = false;
                    if (err != null) { Status = "Reconocimiento de voz: " + err; return; }
                    string clean = Clean(text);
                    Debug.Log($"[ASTRA] oído ({secs:0.0} s, {samples.Length / (float)Rate:0.0} s de audio): \"{text?.Trim()}\" → \"{clean}\"");
                    if (string.IsNullOrEmpty(clean)) { Status = interrupted ? "" : "No te entendí, ¿puedes repetir?"; return; }
                    if (wake)
                    {
                        string n = MedicalKnowledge.Norm(clean);
                        int k = n.IndexOf("astra", StringComparison.Ordinal);
                        if (k < 0) { Status = "Di \"ASTRA\" para hablarme"; return; }
                        clean = clean.Substring(Mathf.Min(clean.Length, k + 5)).TrimStart(',', '.', ' ', '!', '¡');
                        if (clean.Length < 2) clean = "Hola";
                    }
                    Status = "";
                    OnText?.Invoke(clean);
                });
            });
        }

        static readonly string[] Junk =
        {
            "subtitulos", "amara.org", "gracias por ver", "suscribete", "suscribanse", "dale like", "musica",
            "transcripcion", "www.", "http", "conversacion con astra",
        };

        /// <summary>Quita marcas tipo [Música] y frases que Whisper inventa con ruido o silencio.</summary>
        static string Clean(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\[[^\]]*\]|\([^\)]*\)|\*[^\*]*\*", " ");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
            string n = MedicalKnowledge.Norm(t);
            foreach (var j in Junk) if (n.Contains(j)) return "";
            int letters = 0;
            foreach (var c in t) if (char.IsLetter(c)) letters++;
            return letters < 2 ? "" : t;
        }

        /// <summary>WAV PCM 16 bits mono 16 kHz, normalizado (micrófonos bajos se oyen igual de bien).</summary>
        static byte[] ToWav(float[] s)
        {
            float peak = 0f;
            foreach (var x in s) peak = Mathf.Max(peak, Mathf.Abs(x));
            float gain = peak > 1e-4f ? Mathf.Min(0.9f / peak, 20f) : 1f;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + s.Length * 2);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(s.Length * 2);
                foreach (var x in s) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(x * gain * 32767f), -32768, 32767));
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Lee un WAV (para pruebas: -oir archivo.wav) y lo pasa a 16 kHz mono.</summary>
        public static float[] LoadWav16k(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            int ch = 1, rate = 16000, pos = 12, ds = -1, dl = 0;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                int len = BitConverter.ToInt32(b, pos + 4);
                if (id == "fmt ") { ch = BitConverter.ToInt16(b, pos + 10); rate = BitConverter.ToInt32(b, pos + 12); }
                else if (id == "data") { ds = pos + 8; dl = Math.Min(len, b.Length - ds); break; }
                pos += 8 + len + (len & 1);
            }
            if (ds < 0) return new float[0];
            int frames = dl / 2 / ch;
            double ratio = rate / (double)Rate;
            int n = (int)(frames / ratio);
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                int k = Math.Min(frames - 1, (int)(i * ratio));
                o[i] = BitConverter.ToInt16(b, ds + k * ch * 2) / 32768f;
            }
            return o;
        }
    }
}
