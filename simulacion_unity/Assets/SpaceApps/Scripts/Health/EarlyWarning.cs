using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Diagnóstico automatizado y detección temprana (docx sección 4):
    ///  1. Umbrales DINÁMICOS: media y desviación móviles (EWMA) por señal y por astronauta.
    ///     Si |z| = |x − μ| / σ supera 3.5 se marca una anomalía antes de cruzar el umbral clínico.
    ///  2. Umbrales CLÍNICOS fijos (SpO2 &lt; 92 %, PA ≥ 140, T &gt; 38 °C, CO2 &gt; 6 mmHg...).
    ///  3. PRONÓSTICO: regresión lineal de los últimos 30 días del índice de salud y de la
    ///     densidad ósea para estimar en cuántos días se cruza un umbral.
    ///  4. CLASIFICACIÓN: verde / amarillo / rojo según alertas activas e índice de salud.
    /// </summary>
    public class EarlyWarning
    {
        public enum Level { Green, Yellow, Red }

        class Ewma
        {
            public float Mean, Var; public int N;
            public float Update(float x, float alpha)
            {
                if (N == 0) { Mean = x; Var = 0.25f; N = 1; return 0f; }
                float sd = Mathf.Sqrt(Mathf.Max(Var, 1e-4f));
                float z = (x - Mean) / sd;
                float d = x - Mean;
                Mean += alpha * d;
                Var = (1f - alpha) * (Var + alpha * d * d);
                N++;
                return N > 72 ? z : 0f;   // 3 días de calentamiento
            }
        }

        readonly Dictionary<string, Ewma> _monitors = new Dictionary<string, Ewma>();
        readonly Dictionary<string, int> _cooldown = new Dictionary<string, int>();
        public readonly List<string> ActiveFlags = new List<string>();
        public Level Status = Level.Green;
        public float DaysToHealth60 = float.PositiveInfinity, DaysToBone90 = float.PositiveInfinity;
        public float HealthSlopePerDay;
        public int HapticEvents;          // vibraciones enviadas al actuador háptico

        public void Reset()
        {
            _monitors.Clear(); _cooldown.Clear(); ActiveFlags.Clear();
            Status = Level.Green; HapticEvents = 0;
            DaysToHealth60 = DaysToBone90 = float.PositiveInfinity; HealthSlopePerDay = 0f;
        }

        public void StepHour(AstronautHealth h, CabinEnvironment env, List<string> alerts)
        {
            ActiveFlags.Clear();
            int hour = h.Hours;
            // Solo se evalúan en reposo para no confundir el ejercicio con una anomalía
            bool rest = h.Activity >= 0.5f && h.Activity < 3f;
            if (rest)
            {
                Dynamic(h, "FC", h.HeartRate, "frecuencia cardíaca", alerts, hour);
                Dynamic(h, "HRV", h.Hrv, "variabilidad cardíaca", alerts, hour);
                Dynamic(h, "PAS", h.Sbp, "presión sistólica", alerts, hour);
                Dynamic(h, "SpO2", h.SpO2, "saturación de O₂", alerts, hour);
                Dynamic(h, "T", h.CoreTemp, "temperatura central", alerts, hour);
                Dynamic(h, "GSR", h.Gsr, "respuesta galvánica (estrés)", alerts, hour);
            }
            float co2 = h.Cell >= 0 ? env.Co2[h.Cell] : 0.3f;
            float pm = h.Cell >= 0 ? env.Pm25[h.Cell] : 0f;
            Clinical(h, "spo2", h.SpO2 < 92f, $"SpO₂ {h.SpO2:0.0} %", alerts, hour);
            Clinical(h, "hta", rest && h.Sbp >= 140f, $"presión {h.Sbp:0}/{h.Dbp:0} mmHg (hipertensión)", alerts, hour);
            Clinical(h, "taq", rest && h.HeartRate > 100f, $"taquicardia en reposo {h.HeartRate:0} lpm", alerts, hour);
            Clinical(h, "fiebre", h.CoreTemp > 38f && h.Activity < 3f, $"temperatura {h.CoreTemp:0.0} °C", alerts, hour);
            Clinical(h, "co2", co2 > 6f, $"CO₂ {co2:0.0} mmHg en {ModuleName(h.Cell)}", alerts, hour);
            Clinical(h, "pm", pm > 35f, $"PM2.5 {pm:0} µg/m³ en {ModuleName(h.Cell)}", alerts, hour);
            Clinical(h, "co", env.CoPpm > 9f, $"CO {env.CoPpm:0} ppm (revisar sobrecalentamiento)", alerts, hour);
            Clinical(h, "ruido", h.Cell >= 0 && env.NoiseDb > 70f, $"ruido {env.NoiseDb:0} dB", alerts, hour);
            Clinical(h, "sueno", h.SleepDebtH > 8f, $"deuda de sueño {h.SleepDebtH:0.0} h", alerts, hour);

            if (h.Hours % 24 == 0) Forecast(h);

            int n = ActiveFlags.Count;
            float hi = h.HealthIndex;
            Status = (hi < 55f || n >= 3) ? Level.Red : (hi < 75f || n >= 1) ? Level.Yellow : Level.Green;
        }

        static string ModuleName(int cell) => cell >= 0 ? CabinEnvironment.CellNames[cell] : "EVA";

        void Dynamic(AstronautHealth h, string key, float x, string label, List<string> alerts, int hour)
        {
            if (!_monitors.TryGetValue(key, out var mon)) { mon = new Ewma(); _monitors[key] = mon; }
            float z = mon.Update(x, 0.01f);
            if (Mathf.Abs(z) > 3.5f)
            {
                ActiveFlags.Add(key);
                if (Cool(key, hour)) { alerts.Add($"{h.Name}: anomalía en {label} (z = {z:+0.0;-0.0}) [umbral dinámico]"); HapticEvents++; }
            }
        }

        void Clinical(AstronautHealth h, string key, bool cond, string text, List<string> alerts, int hour)
        {
            if (!cond) return;
            ActiveFlags.Add(key);
            if (Cool(key, hour)) { alerts.Add($"{h.Name}: {text}"); HapticEvents++; }
        }

        bool Cool(string key, int hour)
        {
            if (_cooldown.TryGetValue(key, out int last) && hour - last < 24) return false;
            _cooldown[key] = hour;
            return true;
        }

        void Forecast(AstronautHealth h)
        {
            int n = Mathf.Min(30, h.History.Count);
            if (n < 7) return;
            float sx = 0, sy = 0, sxx = 0, sxy = 0, sb = 0, sxb = 0;
            int start = h.History.Count - n;
            for (int i = 0; i < n; i++)
            {
                var r = h.History[start + i];
                float x = i;
                sx += x; sy += r.Health; sxx += x * x; sxy += x * r.Health;
                sb += r.Bone; sxb += x * r.Bone;
            }
            float den = n * sxx - sx * sx;
            if (Mathf.Abs(den) < 1e-5f) return;
            HealthSlopePerDay = (n * sxy - sx * sy) / den;
            float boneSlope = (n * sxb - sx * sb) / den;
            DaysToHealth60 = HealthSlopePerDay < -1e-4f && h.HealthIndex > 60f ? (h.HealthIndex - 60f) / -HealthSlopePerDay : float.PositiveInfinity;
            DaysToBone90 = boneSlope < -1e-5f && h.Bone > 90f ? (h.Bone - 90f) / -boneSlope : float.PositiveInfinity;
        }
    }
}
