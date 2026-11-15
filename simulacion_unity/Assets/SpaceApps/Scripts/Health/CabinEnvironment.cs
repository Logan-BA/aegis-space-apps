using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Ambiente de la cabina (o aire exterior en la Tierra). Paso = 1 hora.
    ///
    /// CO2 y material particulado se resuelven en una malla 1D de 12 módulos con la ecuación de
    /// difusión-advección (ventilación en circuito cerrado):
    ///     dC/dt = D·∂²C/∂x² − v·∂C/∂x + S(x,t) − k·C·δ(x = depurador)
    /// con diferencias finitas explícitas (esquema upwind) y 10 sub-pasos por hora.
    /// Así cada astronauta respira el CO2 del módulo donde está a esa hora.
    /// </summary>
    public class CabinEnvironment
    {
        public const int Cells = 12;
        public static readonly string[] CellNames =
        {
            "ECLSS (depurador)", "Dormitorio 1", "Dormitorio 2", "Dormitorio 3", "Laboratorio A", "Laboratorio B",
            "Control", "Laboratorio C", "Almacén", "Gimnasio", "Cocina", "Esclusa"
        };
        public const int GymCell = 9, KitchenCell = 10, AirlockCell = 11;

        // Malla 1D
        public readonly float[] Co2 = new float[Cells];   // mmHg
        public readonly float[] Pm25 = new float[Cells];  // µg/m³
        readonly float[] _a = new float[Cells];
        readonly float[] _b = new float[Cells];
        readonly float[] _srcCo2 = new float[Cells];
        readonly float[] _srcPm = new float[Cells];

        // Variables globales (docx: calidad del aire, microclima, ruido)
        public float O2Percent = 21f, PressureKpa = 101.3f, TempC = 22.5f, Humidity = 45f, NoiseDb = 55f;
        public float CoPpm = 0.4f, No2 = 1f, So2 = 0.3f, O3 = 2f, Uv = 0f;
        public float CoEvent;   // sobrecalentamiento/combustión lenta (ppm extra)

        // Parámetros del transporte
        const float Diffusion = 0.8f;     // celdas²/h
        const float Velocity = 2.0f;      // celdas/h (ventilación)
        const int SubSteps = 10;

        public float MeanCo2 { get { float s = 0; foreach (var c in Co2) s += c; return s / Cells; } }
        public float MaxCo2 { get { float m = 0; foreach (var c in Co2) m = Mathf.Max(m, c); return m; } }
        public float MeanPm25 { get { float s = 0; foreach (var c in Pm25) s += c; return s / Cells; } }
        public static float Co2Ppm(float mmHg) => mmHg / 760f * 1e6f;
        public float PpO2Kpa => PressureKpa * O2Percent / 100f;

        public void Reset(Scenario s)
        {
            for (int i = 0; i < Cells; i++) { Co2[i] = s.Cabin ? 2.5f : 0.32f; Pm25[i] = s.Cabin ? 3f : 12f; }
            CoEvent = 0f;
            SetAtmosphere(s);
        }

        void SetAtmosphere(Scenario s)
        {
            // Hábitats de superficie: atmósfera de exploración de menor presión y más O2
            bool surface = s.Key == "moon_surface" || s.Key == "mars_surface";
            PressureKpa = surface ? 70.3f : 101.3f;
            O2Percent = surface ? 26.5f : 21f;
        }

        /// <param name="crewCells">módulo donde está cada astronauta (-1 = afuera en EVA)</param>
        /// <param name="activity">0 dormido, 1 despierto, 3 ejercicio</param>
        public void StepHour(Scenario s, Countermeasures cm, int hour, int[] crewCells, float[] activity, float evaDust, System.Random rng)
        {
            SetAtmosphere(s);
            if (!s.Cabin) { OutdoorEarth(hour, rng); return; }

            int awake = 0, exercising = 0;
            System.Array.Clear(_srcCo2, 0, Cells);
            System.Array.Clear(_srcPm, 0, Cells);
            for (int i = 0; i < crewCells.Length; i++)
            {
                int c = crewCells[i];
                if (activity[i] >= 1f) awake++;
                if (activity[i] >= 3f) exercising++;
                if (c < 0) continue;
                _srcCo2[c] += 0.7f * Mathf.Max(0.6f, activity[i]);
                _srcPm[c] += 1.5f * Mathf.Max(0.5f, activity[i]);
            }
            Pm25[AirlockCell] += evaDust;

            float k = 0.6f + 1.6f * cm.Scrubber;     // remoción del depurador (1/h)
            Transport(Co2, _srcCo2, k, 0f, 0.3f);
            Transport(Pm25, _srcPm, 2f * k, 0.08f, 0.5f);

            O2Percent += 0.15f * Mathf.Sin(hour * 0.7f);
            TempC = 22f + 0.45f * exercising + 0.15f * awake + 0.3f * Mathf.Sin((hour - 9) / 24f * Mathf.PI * 2f);
            Humidity = 40f + 1.5f * awake + 3f * exercising;
            NoiseDb = s.NoiseBaseDb + (exercising > 0 ? 8f : 0f) + (awake > 0 ? 3f : -4f) + (float)(rng.NextDouble() - 0.5);
            if (CoEvent <= 0.01f && rng.NextDouble() < 1.0 / 3000.0) CoEvent = 18f + (float)rng.NextDouble() * 15f;
            CoEvent *= 0.82f;
            CoPpm = 0.3f + 0.1f * awake / 4f + CoEvent;
            No2 = 1f; So2 = 0.3f; O3 = 2f; Uv = 0f;
        }

        void Transport(float[] c, float[] src, float removal, float deposition, float floor)
        {
            float dt = 1f / SubSteps;
            System.Array.Copy(c, _a, Cells);
            for (int step = 0; step < SubSteps; step++)
            {
                for (int i = 0; i < Cells; i++)
                {
                    float left = _a[(i - 1 + Cells) % Cells];
                    float right = _a[(i + 1) % Cells];
                    float diff = Diffusion * (left - 2f * _a[i] + right);
                    float adv = -Velocity * (_a[i] - left);          // upwind, flujo hacia +x
                    float v = _a[i] + dt * (diff + adv + src[i] - deposition * _a[i]);
                    if (i == 0) v -= dt * removal * _a[i];
                    _b[i] = Mathf.Max(floor, v);
                }
                System.Array.Copy(_b, _a, Cells);
            }
            System.Array.Copy(_a, c, Cells);
        }

        /// <summary>Tierra: aire urbano exterior con picos de tráfico (variables del documento HACKATON).</summary>
        void OutdoorEarth(int hour, System.Random rng)
        {
            float traffic = Mathf.Exp(-Mathf.Pow((hour - 8) / 1.5f, 2f)) + Mathf.Exp(-Mathf.Pow((hour - 19) / 1.8f, 2f));
            float sun = Mathf.Max(0f, Mathf.Sin((hour - 6) / 12f * Mathf.PI));
            float noise = (float)(rng.NextDouble() - 0.5);
            for (int i = 0; i < Cells; i++)
            {
                Co2[i] = 0.32f + 0.03f * traffic;
                Pm25[i] = 10f + 18f * traffic + 3f * noise;
            }
            CoPpm = 0.4f + 1.2f * traffic;
            No2 = 18f + 35f * traffic;
            So2 = 4f + 3f * traffic;
            O3 = 35f + 60f * sun;
            Uv = 9f * sun;
            TempC = 17f + 7f * Mathf.Sin((hour - 9) / 24f * Mathf.PI * 2f);
            Humidity = 60f - 15f * sun;
            NoiseDb = 42f + 22f * traffic + 8f * sun;
            O2Percent = 20.9f;
            PressureKpa = 101.3f;
        }
    }
}
