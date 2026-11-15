using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SpaceApps.Assistant
{
    /// <summary>
    /// Voz de ASTRA, 100 % local:
    ///  1) Piper (voz neuronal, StreamingAssets/piper: piper.exe + voz .onnx) — natural, recomendada.
    ///  2) Si no está, la síntesis de Windows (SAPI) con un proceso PowerShell persistente.
    /// La respuesta se divide en oraciones: la primera suena apenas está lista mientras se generan
    /// las siguientes. Cada trozo se recorta (silencios) y se normaliza (volumen alto y parejo).
    /// Level (0..1) mueve la boca del rostro.
    /// </summary>
    public class SpeechVoice : MonoBehaviour
    {
        public bool Enabled = true;
        public bool PreferPiper = true;
        public float PiperSpeed = 0.9f;          // length_scale de Piper: menor = más rápido
        public float Volume = 1f;
        public float Level { get; private set; }
        public float OutputRms { get; private set; }          // nivel real de su voz (para estimar el eco)
        public bool IsSpeaking => (_source != null && _source.isPlaying) || _play < _queue.Count || _fakeTime > 0f;
        public string LastError = "";
        public string Engine { get; private set; } = "";

        class Job { public string Key, Wav; public volatile bool Ready, Failed; public AudioClip Clip; }

        AudioSource _source;
        float _fakeTime;
        readonly float[] _buf = new float[256];
        readonly List<Job> _queue = new List<Job>();
        readonly Dictionary<string, Job> _pending = new Dictionary<string, Job>(StringComparer.OrdinalIgnoreCase);
        int _play, _jobCounter;
        float _askTime = -1f;
        Process _proc;
        StreamWriter _in;
        bool _piper;
        string _dir;

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _dir = Path.Combine(Application.temporaryCachePath, "astra_voz");
            Directory.CreateDirectory(_dir);
        }

        void Start() { if (IsWindows && Enabled) StartEngine(); }   // después de leer astra.json

        static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

        // ------------------------------------------------------------ motores de voz

        const string SapiScript =
"$ErrorActionPreference = 'Stop'\r\n" +
"Add-Type -AssemblyName System.Speech\r\n" +
"$s = New-Object System.Speech.Synthesis.SpeechSynthesizer\r\n" +
"$v = $s.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -like 'es*' -and $_.Enabled } | Select-Object -First 1\r\n" +
"$lang = 'es-MX'\r\n" +
"if ($v) { $s.SelectVoice($v.VoiceInfo.Name); $lang = $v.VoiceInfo.Culture.Name }\r\n" +
"$s.Volume = 100\r\n" +
"$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)\r\n" +
"[Console]::Out.WriteLine(\"LISTO`t\" + $s.Voice.Name)\r\n" +
"while ($true) {\r\n" +
"  $line = [Console]::In.ReadLine()\r\n" +
"  if ($line -eq $null) { break }\r\n" +
"  $p = $line.Split(\"`t\")\r\n" +
"  try {\r\n" +
"    $wav = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p[1]))\r\n" +
"    $ssml = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p[2])).Replace('LANG', $lang)\r\n" +
"    $s.SetOutputToWaveFile($wav, $fmt)\r\n" +
"    $s.SpeakSsml($ssml)\r\n" +
"    $s.SetOutputToNull()\r\n" +
"    [Console]::Out.WriteLine(\"OK`t\" + $p[0])\r\n" +
"  } catch { $s.SetOutputToNull(); [Console]::Out.WriteLine(\"ERR`t\" + $p[0] + \"`t\" + $_.Exception.Message) }\r\n" +
"}\r\n";

        void StartEngine()
        {
            StopEngine();
            string pdir = Path.Combine(Application.streamingAssetsPath, "piper");
            string exe = Path.Combine(pdir, "piper.exe");
            string model = Directory.Exists(pdir) ? Directory.GetFiles(pdir, "*.onnx").Length > 0 ? Directory.GetFiles(pdir, "*.onnx")[0] : null : null;
            try
            {
                ProcessStartInfo psi;
                if (PreferPiper && File.Exists(exe) && model != null)
                {
                    _piper = true;
                    psi = new ProcessStartInfo(exe, $"-m \"{model}\" --json-input --length_scale {PiperSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} --sentence_silence 0.1 -q")
                        { WorkingDirectory = pdir };
                    Engine = "Piper · " + Path.GetFileNameWithoutExtension(model);
                }
                else
                {
                    _piper = false;
                    string ps1 = Path.Combine(_dir, "servidor_voz.ps1");
                    File.WriteAllText(ps1, SapiScript, new UTF8Encoding(true));
                    psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{ps1}\"");
                    Engine = "Windows SAPI";
                }
                psi.CreateNoWindow = true; psi.UseShellExecute = false;
                psi.RedirectStandardInput = true; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                _proc = Process.Start(psi);
                _in = new StreamWriter(_proc.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                var proc = _proc;
                bool piper = _piper;
                new Thread(() => ReadOutput(proc, piper)) { IsBackground = true, Name = "ASTRA voz" }.Start();
                new Thread(() => { try { string l; while ((l = proc.StandardError.ReadLine()) != null) if (l.Trim().Length > 0) LastError = l.Trim(); } catch { } })
                    { IsBackground = true }.Start();
                Debug.Log("[ASTRA] voz: " + Engine);
            }
            catch (Exception e)
            {
                LastError = e.Message; _proc = null; _in = null;
                if (_piper) { PreferPiper = false; StartEngine(); }   // sin Piper: voz de Windows
            }
        }

        void ReadOutput(Process proc, bool piper)
        {
            try
            {
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("LISTO")) continue;
                    string key; bool ok = true;
                    if (piper) key = SafeFull(line);
                    else
                    {
                        var p = line.Split('\t');
                        if (p.Length < 2) continue;
                        ok = p[0] == "OK"; key = p[1];
                        if (!ok && p.Length > 2) LastError = p[2];
                    }
                    Job job;
                    lock (_pending) { if (_pending.TryGetValue(key, out job)) _pending.Remove(key); }
                    if (job == null) continue;
                    if (ok) job.Ready = true; else job.Failed = true;
                }
            }
            catch (Exception e) { LastError = e.Message; }
            lock (_pending) { foreach (var j in _pending.Values) j.Failed = true; _pending.Clear(); }
        }

        static string SafeFull(string p) { try { return Path.GetFullPath(p); } catch { return p; } }

        void StopEngine()
        {
            try { if (_proc != null && !_proc.HasExited) { _in?.Close(); if (!_proc.WaitForExit(300)) _proc.Kill(); } } catch { }
            _proc = null; _in = null;
        }

        void OnDestroy() => StopEngine();

        // ------------------------------------------------------------ hablar

        public void Stop()
        {
            _queue.Clear();
            _play = 0;
            if (_source.isPlaying) _source.Stop();
            _fakeTime = 0f;
            int stale; lock (_pending) stale = _pending.Count;
            if (stale >= 3) StartEngine();          // descarta una respuesta larga anterior sin esperarla
        }

        public void Speak(string text, Emotion emotion)
        {
            Stop();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!Enabled || !IsWindows) { Fake(text); return; }
            if (_proc == null || _proc.HasExited) StartEngine();
            if (_proc == null) { Fake(text); return; }
            _askTime = Time.realtimeSinceStartup;
            foreach (var sentence in Sentences(Spoken(text)))
            {
                int id = ++_jobCounter;
                string wav = SafeFull(Path.Combine(_dir, $"frase_{id % 40:00}.wav"));
                var job = new Job { Wav = wav, Key = _piper ? wav : id.ToString() };
                lock (_pending) _pending[job.Key] = job;
                _queue.Add(job);
                string line = _piper
                    ? "{\"text\":" + OllamaClient.Q(sentence) + ",\"output_file\":" + OllamaClient.Q(wav) + "}"
                    : id + "\t" + B64(wav) + "\t" + B64(Ssml(sentence, emotion));
                try { _in.WriteLine(line); }
                catch (Exception e) { LastError = e.Message; job.Failed = true; }
            }
        }

        void Fake(string text) => _fakeTime = Mathf.Clamp(text.Length * 0.05f, 1.5f, 14f);   // sin voz: la boca se mueve igual

        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

        /// <summary>Texto pensado para leer en voz alta: siglas y unidades en palabras.</summary>
        public static string Spoken(string t)
        {
            t = t.Replace("·", ",").Replace("CO₂", "CO dos").Replace("CO2", "CO dos").Replace("SpO2", "saturación de oxígeno")
                 .Replace("SpO₂", "saturación de oxígeno").Replace("VO2 máx", "VO dos máximo").Replace("VO₂ máx", "VO dos máximo")
                 .Replace("mSv", " milisievert").Replace("mmHg", " milímetros de mercurio").Replace("lpm", "latidos por minuto")
                 .Replace("µg/dL", "microgramos por decilitro").Replace("mg/día", "miligramos por día").Replace("°C", " grados");
            t = Regex.Replace(t, @"\bCMD\b", "comandante");
            t = Regex.Replace(t, @"\bPIL\b", "piloto");
            t = Regex.Replace(t, @"\bMED\b", "médica");
            t = Regex.Replace(t, @"\bING\b", "ingeniera");
            t = Regex.Replace(t, @"(\d+)\s*/\s*(\d+)", "$1 de $2");
            t = Regex.Replace(t, @"(\d)\s*%", "$1 por ciento");
            t = Regex.Replace(t, @"(\d+)\.(\d+)", "$1 coma $2");
            t = Regex.Replace(t, @"(\d+),(\d+)", "$1 coma $2");
            return Regex.Replace(t, @"\s+", " ").Trim();
        }

        /// <summary>Divide en oraciones (la primera corta, para empezar a hablar antes).</summary>
        static List<string> Sentences(string text)
        {
            var list = new List<string>();
            var cur = new StringBuilder();
            foreach (var part in Regex.Split(text, @"(?<=[\.\!\?;:])\s+"))
            {
                if (part.Trim().Length == 0) continue;
                if (cur.Length > 0) cur.Append(' ');
                cur.Append(part.Trim());
                int min = list.Count == 0 ? 25 : 70;
                if (cur.Length >= min) { list.Add(cur.ToString()); cur.Clear(); }
            }
            if (cur.Length > 0) { if (list.Count > 0 && cur.Length < 25) list[list.Count - 1] += " " + cur; else list.Add(cur.ToString()); }
            return list;
        }

        /// <summary>Windows SAPI: un poco más rápida que su ritmo lento por defecto, sin bajar el tono.</summary>
        static string Ssml(string text, Emotion e)
        {
            string rate = "+14%", pitch = "+2%";
            switch (e)
            {
                case Emotion.Amable: rate = "+12%"; pitch = "+5%"; break;
                case Emotion.Feliz: rate = "+16%"; pitch = "+8%"; break;
                case Emotion.Preocupada: rate = "+10%"; pitch = "+1%"; break;
                case Emotion.Firme: rate = "+12%"; pitch = "+0%"; break;
                case Emotion.Alerta: rate = "+20%"; pitch = "+6%"; break;
            }
            string safe = System.Security.SecurityElement.Escape(text);
            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"LANG\"><prosody rate=\"{rate}\" pitch=\"{pitch}\" volume=\"x-loud\">{safe}</prosody></speak>";
        }

        void Update()
        {
            for (int i = _play; i < _queue.Count; i++)
            {
                var j = _queue[i];
                if (j.Ready && j.Clip == null && !j.Failed) { j.Clip = LoadWav(j.Wav); if (j.Clip == null) j.Failed = true; }
            }
            if (!_source.isPlaying && _play < _queue.Count)
            {
                var j = _queue[_play];
                if (j.Failed) _play++;
                else if (j.Clip != null)
                {
                    _source.clip = j.Clip; _source.volume = Volume; _source.Play(); _play++;
                    if (_askTime >= 0f) { Debug.Log($"[ASTRA] voz ({Engine}): empezó a hablar en {Time.realtimeSinceStartup - _askTime:0.00} s"); _askTime = -1f; }
                }
            }
            if (_source.isPlaying)
            {
                _source.GetOutputData(_buf, 0);
                float sum = 0f;
                foreach (var x in _buf) sum += x * x;
                float rms = Mathf.Sqrt(sum / _buf.Length);
                OutputRms = rms * Volume;
                Level = Mathf.Lerp(Level, Mathf.Clamp01(rms * 4.5f), 0.6f);
            }
            else if (_fakeTime > 0f)
            {
                OutputRms = 0f;
                _fakeTime -= Time.deltaTime;
                Level = Mathf.Clamp01(0.35f + 0.35f * Mathf.Sin(Time.time * 15f) * Mathf.Sin(Time.time * 4.3f));
            }
            else { OutputRms = 0f; Level = Mathf.MoveTowards(Level, 0f, Time.deltaTime * 4f); }
        }

        /// <summary>Lee un WAV PCM de 16 bits, recorta el silencio de los extremos y normaliza al 95 %.</summary>
        AudioClip LoadWav(string path)
        {
            try
            {
                byte[] b = File.ReadAllBytes(path);
                int channels = 1, rate = 22050, bits = 16, pos = 12, dataStart = -1, dataLen = 0;
                while (pos + 8 <= b.Length)
                {
                    string id = Encoding.ASCII.GetString(b, pos, 4);
                    int len = BitConverter.ToInt32(b, pos + 4);
                    if (id == "fmt ") { channels = BitConverter.ToInt16(b, pos + 10); rate = BitConverter.ToInt32(b, pos + 12); bits = BitConverter.ToInt16(b, pos + 22); }
                    else if (id == "data") { dataStart = pos + 8; dataLen = Math.Min(len, b.Length - dataStart); break; }
                    pos += 8 + len + (len & 1);
                }
                if (dataStart < 0 || bits != 16) { LastError = "WAV no compatible"; return null; }
                int n = dataLen / 2;
                var s = new float[n];
                float peak = 0f;
                for (int i = 0; i < n; i++) { s[i] = BitConverter.ToInt16(b, dataStart + i * 2) / 32768f; peak = Mathf.Max(peak, Mathf.Abs(s[i])); }
                if (peak < 1e-4f) return null;
                float gate = peak * 0.02f;
                int a = 0, z = n - 1;
                while (a < n && Mathf.Abs(s[a]) < gate) a++;
                while (z > a && Mathf.Abs(s[z]) < gate) z--;
                a = Math.Max(0, a - rate / 50); z = Math.Min(n - 1, z + rate / 10);
                a -= a % channels;
                int count = (z - a + 1) / channels * channels;
                float gain = Mathf.Min(0.95f / peak, 8f);
                var outS = new float[count];
                for (int i = 0; i < count; i++) outS[i] = Mathf.Clamp(s[a + i] * gain, -1f, 1f);
                var clip = AudioClip.Create("astra", count / channels, channels, rate, false);
                clip.SetData(outS, 0);
                return clip;
            }
            catch (Exception e) { LastError = e.Message; return null; }
        }
    }
}
