using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Sensores simulados (docx sección 5): cada lectura = valor real del modelo + ruido gaussiano
    /// propio del sensor + pérdidas de dato ocasionales. Así la interfaz muestra lo que vería un
    /// sistema real y no el valor "perfecto" del modelo.
    /// </summary>
    public class SensorReading
    {
        public string Sensor, Variable, Unit;
        public float Value;
        public bool Dropout;
        public string Text => Dropout ? "-- (sin señal)" : $"{Value:0.##} {Unit}";
    }

    public static class SensorSuite
    {
        static readonly System.Random Rng = new System.Random(7);

        static float Gauss(float sigma)
        {
            double u1 = 1.0 - Rng.NextDouble(), u2 = Rng.NextDouble();
            return sigma * (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
        }

        static SensorReading R(string sensor, string variable, float truth, float sigma, string unit, float dropout = 0.01f)
        {
            return new SensorReading
            {
                Sensor = sensor, Variable = variable, Unit = unit,
                Value = truth + Gauss(sigma), Dropout = Rng.NextDouble() < dropout,
            };
        }

        public static List<SensorReading> Biomedical(AstronautHealth h)
        {
            return new List<SensorReading>
            {
                R("ECG (electrodos)", "Frecuencia cardíaca", h.HeartRate, 0.5f, "lpm", 0.002f),
                R("ECG (electrodos)", "HRV (RMSSD)", h.Hrv, 2f, "ms", 0.002f),
                R("PPG óptico", "Frecuencia cardíaca", h.HeartRate, 2.5f, "lpm", 0.02f),
                R("PPG óptico", "SpO₂", Mathf.Min(100f, h.SpO2), 0.8f, "%", 0.02f),
                R("Manguito oscilométrico", "Presión sistólica", h.Sbp, 3f, "mmHg"),
                R("Manguito oscilométrico", "Presión diastólica", h.Dbp, 2f, "mmHg"),
                R("Banda torácica", "Frecuencia respiratoria", h.RespRate, 0.7f, "rpm"),
                R("Termistor (cápsula)", "Temperatura central", h.CoreTemp, 0.05f, "°C"),
                R("Infrarrojo (frente)", "Temperatura", h.CoreTemp - 0.4f, 0.25f, "°C"),
                R("GSR / EDA", "Conductancia de la piel", h.Gsr, 0.3f, "µS"),
                R("EMG superficial", "Índice de fatiga muscular", h.EmgFatigue, 0.03f, "", 0.005f),
                R("EEG (diadema)", "Índice de alerta", h.EegAlertness, 0.04f, "", 0.01f),
                R("Acelerómetro triaxial", "Actividad", h.Mets, 0.15f, "MET"),
                R("Biosensor saliva", "Cortisol", h.Cortisol, 0.8f, "µg/dL", 0f),
                R("Glucómetro continuo", "Glucosa", h.Glucose, 3f, "mg/dL", 0.005f),
                R("Analizador portátil", "Lactato", h.Lactate, 0.15f, "mmol/L", 0f),
                R("Analizador portátil", "pH sanguíneo", h.BloodPh, 0.01f, "", 0f),
                R("Analizador portátil", "Potasio", h.Potassium, 0.08f, "mmol/L", 0f),
                R("Análisis de orina", "Calcio urinario", h.UrinaryCa, 10f, "mg/día", 0f),
            };
        }

        public static List<SensorReading> Environmental(CabinEnvironment env, int cell)
        {
            int c = Mathf.Clamp(cell, 0, CabinEnvironment.Cells - 1);
            return new List<SensorReading>
            {
                R("NDIR (tipo SCD30)", "CO₂", CabinEnvironment.Co2Ppm(env.Co2[c]), 30f, "ppm"),
                R("Láser dispersión (tipo PMS5003)", "PM2.5", env.Pm25[c], Mathf.Max(1f, env.Pm25[c] * 0.1f), "µg/m³"),
                R("Láser dispersión (tipo PMS5003)", "PM10", env.Pm25[c] * 1.7f, Mathf.Max(1.5f, env.Pm25[c] * 0.15f), "µg/m³"),
                R("Electroquímico (serie MQ-7)", "CO", env.CoPpm, 0.3f + env.CoPpm * 0.15f, "ppm"),
                R("Electroquímico", "NO₂", env.No2, 1f, "µg/m³"),
                R("Electroquímico", "SO₂", env.So2, 0.5f, "µg/m³"),
                R("Electroquímico", "O₃", env.O3, 2f, "µg/m³"),
                R("Celda galvánica", "O₂", env.O2Percent, 0.1f, "%"),
                R("BME280", "Temperatura", env.TempC, 0.5f, "°C"),
                R("BME280", "Humedad relativa", env.Humidity, 3f, "%"),
                R("BME280", "Presión", env.PressureKpa, 0.1f, "kPa"),
                R("Micrófono calibrado", "Ruido (Leq)", env.NoiseDb, 1.5f, "dB(A)"),
                R("Radiómetro UV", "Índice UV", env.Uv, 0.2f, ""),
            };
        }

        /// <summary>Color del LED RGB del astronauta (actuador) según el nivel de alerta.</summary>
        public static Color LedColor(EarlyWarning.Level level)
        {
            switch (level)
            {
                case EarlyWarning.Level.Red: return new Color(1f, 0.15f, 0.1f);
                case EarlyWarning.Level.Yellow: return new Color(1f, 0.8f, 0.1f);
                default: return new Color(0.2f, 1f, 0.35f);
            }
        }
    }
}
