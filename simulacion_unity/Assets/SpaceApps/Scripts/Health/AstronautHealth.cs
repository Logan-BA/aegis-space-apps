using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>Registro diario (para gráficos y CSV).</summary>
    public struct DayRecord
    {
        public int Day;
        public float Bone, Muscle, Vo2, Plasma, Sans, Dose, Stress, Health;
        public float HrRest, Hrv, Sbp, Dbp, SpO2, CoreTemp, Cortisol, Glucose, UrinaryCa, SleepH, Co2Exposure, Pm25Exposure;
    }

    /// <summary>
    /// Modelo fisiológico de UN astronauta (espacio de estados). Paso = 1 hora.
    ///
    /// Variables lentas (días-meses), EDO de primer orden con tasas por día / 24:
    ///   dB/dt  = −0.04·u·(1−0.6E)·M         hueso (%)           u = 1−g, E = ejercicio, M = preparación
    ///   dMu/dt = −0.12·u·(1−0.7E)·M         músculo (%)
    ///   dV/dt  = −0.10·u·(1−0.75E)·M        VO2 máx (%)
    ///   dPl/dt = (12·u − Pl)/τ, τ = 2 d     déficit de plasma (%)
    ///   dS/dt  = 0.0035·u·(1+(CO2−2)/4)     índice SANS (0..1)
    ///   dD/dt  = tasa·(1−0.35·blindaje) + SPE  dosis (mSv)
    ///   dΨ/dt  = aislamiento + ruido + CO2 + sueño − apoyo   estrés (0..100)
    ///
    /// Variables rápidas (horas): signos vitales, bioquímica y bioeléctricas, calculadas a partir
    /// del estado lento, la hora del día (ritmo circadiano), la actividad y el ambiente local.
    /// </summary>
    public class AstronautHealth
    {
        // ---------------- identidad y preparación
        public string Name, Role;
        public float Fitness;          // 0..1
        public int ConditioningDays;   // mínimo 14
        public float MassKg0, HeightM;
        public int Age;
        public int Index;              // posición en la tripulación

        // ---------------- estado lento
        public float Bone, Muscle, Vo2, PlasmaDeficit, Sans, DoseMsv, SpeDoseMsv, Stress, SleepDebtH, SpaceFever, DaysAway;
        public int Hours;
        public int Day => Hours / 24;

        // ---------------- signos vitales y hemodinámica (docx 1)
        public float HeartRate, Hrv, Sbp, Dbp, SpO2, RespRate, CoreTemp;
        // ---------------- bioquímica
        public float Glucose, Lactate, Cortisol, BloodPh, UrinaryCa, Potassium;
        // ---------------- neuromuscular y bioeléctrica
        public float EmgFatigue, EegAlertness, Gsr;
        // ---------------- conductuales y metabólicos
        public float SleepHoursLast = 8f, RemPercent = 22f, EnergyKcal, Mets, Bmi, MassKg;
        // ---------------- exposición ambiental del día
        public float Co2Exposure, Pm25Exposure, NoiseExposure;
        public float LastAcuteMsv, LastSpeMsv;
        float _co2Acc, _pmAcc, _noiseAcc, _doseToday;
        int _accN;
        float _orthoTimer;      // horas de hipotensión ortostática tras volver a gravedad
        float _prevG = 1f;
        float _mealGlucose;

        public int Cell = 1;        // módulo donde está ahora (-1 = EVA)
        public float Activity = 1f; // 0 dormido, 1 despierto, 3 ejercicio
        public bool OnEva;

        public readonly List<DayRecord> History = new List<DayRecord>();
        readonly HashSet<string> _flags = new HashSet<string>();

        public AstronautHealth(string name, string role, float fitness, float massKg, float heightM, int age, int conditioning, int index)
        {
            Name = name; Role = role; Fitness = fitness; MassKg0 = massKg; HeightM = heightM; Age = age;
            ConditioningDays = conditioning; Index = index;
            Reset();
        }

        public void Reset()
        {
            Bone = Muscle = Vo2 = 100f;
            PlasmaDeficit = Sans = DoseMsv = SpeDoseMsv = SleepDebtH = SpaceFever = DaysAway = 0f;
            Stress = 10f; Hours = 0; _orthoTimer = 0f; _prevG = 1f;
            _co2Acc = _pmAcc = _noiseAcc = _doseToday = 0f; _accN = 0;
            History.Clear(); _flags.Clear();
            MassKg = MassKg0;
            Bmi = MassKg / (HeightM * HeightM);
            HeartRate = 62f; Hrv = 50f; Sbp = 118f; Dbp = 76f; SpO2 = 98f; RespRate = 14f; CoreTemp = 36.8f;
            Glucose = 90f; Lactate = 1f; Cortisol = 12f; BloodPh = 7.40f; UrinaryCa = 200f; Potassium = 4.2f;
        }

        public float Preparation => 0.5f * Fitness + 0.5f * Mathf.Clamp01(ConditioningDays / 14f);
        public float DeclineMultiplier => 1.25f - 0.4f * Preparation;
        public float CancerRiskPercent => 5f * DoseMsv / 1000f;

        /// <summary>Avanza 1 hora. Devuelve alertas de umbrales clínicos fijos (las dinámicas las da EarlyWarning).</summary>
        public void StepHour(Scenario s, Countermeasures cm, CabinEnvironment env, int hour, float speRawMsv, List<string> alerts)
        {
            const float dtDay = 1f / 24f;
            float g = s.Gravity + (s.Transit ? cm.ArtificialG : 0f);
            float u = Mathf.Clamp01(1f - g);
            float m = DeclineMultiplier;
            float e = cm.Exercise;

            // ---------- entorno local
            float co2 = Cell >= 0 ? env.Co2[Cell] : 0.3f;     // en EVA respira del traje
            float pm = Cell >= 0 ? env.Pm25[Cell] : 0f;
            float noise = Cell >= 0 ? env.NoiseDb : 30f;
            bool asleep = Activity < 0.5f, exercising = Activity >= 3f;

            // ---------- variables lentas
            if (g >= 0.9f)
            {
                Bone = Mathf.Min(100f, Bone + 0.015f * dtDay);
                Muscle = Mathf.Min(100f, Muscle + 0.3f * dtDay);
                Vo2 = Mathf.Min(100f, Vo2 + 0.3f * dtDay);
                Sans = Mathf.Max(0f, Sans - 0.002f * dtDay);
            }
            else
            {
                Bone -= 0.04f * u * (1f - 0.6f * e) * m * dtDay;
                Muscle -= 0.12f * u * (1f - 0.7f * e) * m * dtDay;
                Vo2 -= 0.10f * u * (1f - 0.75f * e) * m * dtDay;
            }
            Bone = Mathf.Max(Bone, 50f); Muscle = Mathf.Max(Muscle, 50f); Vo2 = Mathf.Max(Vo2, 60f);
            PlasmaDeficit += (12f * u - PlasmaDeficit) * (1f - Mathf.Exp(-dtDay / 2f));
            // SANS: crece con microgravedad y CO2, saturando hacia 1 (≈0.47 a 6 meses en la ISS)
            if (u > 0f) Sans = Mathf.Clamp01(Sans + 0.0035f * u * (1f + Mathf.Max(0f, (co2 - 2f) / 4f)) * (1f - Sans) * dtDay);
            if (s.Isolation > 0.3f) DaysAway += dtDay;
            // "Fiebre espacial": la temperatura central sube ~0.5 °C en meses de microgravedad
            SpaceFever += ((u > 0.5f ? 0.5f : 0f) - SpaceFever) * (dtDay / 60f);

            // Retorno a gravedad con déficit de plasma -> hipotensión ortostática
            if (_prevG < 0.1f && g > 0.3f && PlasmaDeficit > 6f)
            {
                _orthoTimer = 48f;
                alerts.Add($"{Name}: hipotensión ortostática al volver a gravedad");
            }
            _prevG = g;
            _orthoTimer = Mathf.Max(0f, _orthoTimer - 1f);

            // ---------- radiación
            float gcr = s.DoseRate * (1f - 0.35f * cm.Shielding) * dtDay * (OnEva ? 1.6f : 1f);
            float spe = 0f;
            if (speRawMsv > 0f && s.SpeExposure > 0f)
                spe = speRawMsv * s.SpeExposure * (OnEva ? 1f : (cm.Shelter ? 0.1f : 1f - 0.35f * cm.Shielding));
            DoseMsv += gcr + spe; SpeDoseMsv += spe; _doseToday += gcr + spe;
            if (spe > 1f) { LastSpeMsv = spe; alerts.Add($"{Name}: tormenta solar +{spe:0} mSv"); Stress += 5f; }

            // ---------- psicológico / estrés: tiende a un equilibrio que fijan el entorno y el apoyo
            //   Ψ* = 10 + 30·aislamiento + 0.02·días lejos + 1.5·(ruido−60) + 6·(CO2−4) + 1.5·deuda − 18·apoyo
            //   dΨ/dt = (Ψ* − Ψ) / 20 días
            float target = 10f + 30f * s.Isolation + Mathf.Min(20f, 0.02f * DaysAway)
                           + 1.5f * Mathf.Max(0f, noise - 60f) + 6f * Mathf.Max(0f, co2 - 4f)
                           + 1.5f * SleepDebtH - 18f * cm.Support;
            target = Mathf.Clamp(target, 5f, 100f);
            Stress = Mathf.Clamp(Stress + (target - Stress) * dtDay / 20f, 0f, 100f);

            // ---------- sueño (se evalúa al despertar, 06:00)
            if (hour == 6)
            {
                float disruption = s.Circadian * (1f - 0.6f * cm.Lighting);
                SleepHoursLast = Mathf.Clamp(8f - 2.2f * disruption - 0.06f * Mathf.Max(0f, noise - 50f)
                                             - 1.0f * Stress / 100f - 0.25f * Mathf.Max(0f, co2 - 3f)
                                             - 0.3f * Mathf.Max(0f, env.TempC - 26f), 4f, 8.5f);
                RemPercent = Mathf.Clamp(23f - 8f * disruption - 0.05f * Stress, 10f, 25f);
                // deuda de sueño con recuperación parcial cada noche (equilibrio ≈ 5 h si se duermen 6.5 h)
                SleepDebtH = Mathf.Clamp(SleepDebtH * 0.8f + Mathf.Max(0f, 7.5f - SleepHoursLast), 0f, 20f);
            }

            // ---------- ritmo circadiano
            float circ = Mathf.Sin((hour - 10f) / 24f * Mathf.PI * 2f);   // máximo ~16 h
            float cortisolRhythm = 4f + 11f * Mathf.Max(0f, Mathf.Cos((hour - 8f) / 24f * Mathf.PI * 2f)); // pico 08 h

            // ---------- signos vitales
            float exer = exercising ? 1f : 0f;
            float hrRest = 62f + (100f - Vo2) * 0.4f + Stress * 0.12f + Mathf.Max(0f, co2 - 4f) * 1.5f
                           + Mathf.Max(0f, env.TempC - 26f) * 1.2f + SleepDebtH * 0.4f - (Fitness - 0.5f) * 10f;
            if (_orthoTimer > 0f && !asleep) hrRest += 18f;
            HeartRate = hrRest + exer * (55f + (100f - Vo2) * 0.8f) - (asleep ? 7f : 0f);
            Hrv = Mathf.Clamp(55f * (0.6f + 0.4f * Fitness) - Stress * 0.25f - SleepDebtH * 1.2f + (asleep ? 10f : 0f) - exer * 25f, 8f, 110f);
            Sbp = 118f + Stress * 0.15f + Mathf.Max(0f, noise - 65f) * 0.4f + exer * 28f - PlasmaDeficit * 0.4f
                  + (Age - 40) * 0.3f - (asleep ? 8f : 0f) - (_orthoTimer > 0f && !asleep ? 25f : 0f);
            Dbp = 76f + Stress * 0.08f + exer * 4f - (asleep ? 6f : 0f) - (_orthoTimer > 0f && !asleep ? 10f : 0f);
            float ppo2 = Cell >= 0 ? env.PpO2Kpa : 21f;
            SpO2 = Mathf.Clamp(98.3f - Mathf.Max(0f, 19f - ppo2) * 2.5f - Mathf.Max(0f, co2 - 6f) * 0.3f - exer * 1f, 80f, 100f);
            RespRate = 14f + Mathf.Max(0f, co2 - 3f) * 0.9f + exer * 12f + Stress * 0.02f - (asleep ? 2f : 0f);
            CoreTemp = 36.8f + 0.3f * circ + exer * 0.9f * (1f + SpaceFever) + SpaceFever
                       + Mathf.Max(0f, env.TempC - 27f) * 0.12f + Mathf.Max(0f, env.Humidity - 70f) * 0.01f;

            // ---------- bioquímica
            if (hour == 8 || hour == 13 || hour == 19) _mealGlucose = 35f;
            _mealGlucose *= 0.6f;
            Cortisol = cortisolRhythm * (1f + Stress / 100f * 0.8f + SleepDebtH * 0.03f);
            Glucose = 88f + _mealGlucose + (100f - Muscle) * 0.5f + Cortisol * 0.4f - exer * 10f;
            Lactate = 1f + exer * 3.2f * (1.3f - Vo2 / 100f * 0.5f);
            BloodPh = 7.40f - (co2 - 0.3f) * 0.004f - Mathf.Max(0f, Lactate - 2f) * 0.012f;
            UrinaryCa = 200f + 160f * u * (1f - 0.6f * e) * m;                 // mg/día: hipercalciuria por pérdida ósea
            Potassium = 4.2f - PlasmaDeficit * 0.01f;

            // ---------- neuromuscular / bioeléctrica
            EmgFatigue = Mathf.Clamp01((100f - Muscle) / 30f + exer * 0.3f);
            EegAlertness = Mathf.Clamp01(asleep ? 0.1f : 1f - SleepDebtH / 12f - Mathf.Max(0f, co2 - 4f) * 0.05f);
            Gsr = 2f + Stress * 0.08f + exer * 3f + Mathf.Max(0f, noise - 65f) * 0.1f - (asleep ? 1f : 0f);

            // ---------- metabólico
            MassKg = MassKg0 - (100f - Muscle) * 0.28f - (100f - Bone) * 0.03f;
            Bmi = MassKg / (HeightM * HeightM);
            Mets = asleep ? 0.9f : exercising ? 7f : 1.6f;
            float bmr = 10f * MassKg + 625f * HeightM - 5f * Age - 78f;  // Mifflin-St Jeor (constante promedio)
            EnergyKcal = bmr * 1.3f + e * 2.5f * 7f * MassKg;

            // ---------- exposición acumulada del día
            _co2Acc += co2; _pmAcc += pm; _noiseAcc += noise; _accN++;

            Hours++;
            if (Hours % 24 == 0) CloseDay();
            CheckThresholds(alerts);
        }

        void CloseDay()
        {
            Co2Exposure = _co2Acc / Mathf.Max(1, _accN);
            Pm25Exposure = _pmAcc / Mathf.Max(1, _accN);
            NoiseExposure = _noiseAcc / Mathf.Max(1, _accN);
            _co2Acc = _pmAcc = _noiseAcc = 0f; _accN = 0;
            LastAcuteMsv = _doseToday; _doseToday = 0f;
            History.Add(new DayRecord
            {
                Day = Day, Bone = Bone, Muscle = Muscle, Vo2 = Vo2, Plasma = PlasmaDeficit, Sans = Sans, Dose = DoseMsv,
                Stress = Stress, Health = HealthIndex, HrRest = HeartRate, Hrv = Hrv, Sbp = Sbp, Dbp = Dbp, SpO2 = SpO2,
                CoreTemp = CoreTemp, Cortisol = Cortisol, Glucose = Glucose, UrinaryCa = UrinaryCa, SleepH = SleepHoursLast,
                Co2Exposure = Co2Exposure, Pm25Exposure = Pm25Exposure,
            });
        }

        // ---------------- puntajes
        public float ScoreBone => Mathf.Clamp01((Bone - 75f) / 25f) * 100f;
        public float ScoreMuscle => Mathf.Clamp01((Muscle - 70f) / 30f) * 100f;
        public float ScoreCardio => Mathf.Clamp01((Vo2 - 70f) / 30f) * (1f - PlasmaDeficit / 24f) * 100f;
        public float ScoreVision => (1f - Sans) * 100f;
        public float ScoreRadiation => Mathf.Clamp01(1f - DoseMsv / 1000f) * 100f;
        public float ScorePsych => Mathf.Clamp(100f - Stress - SleepDebtH * 1.5f, 0f, 100f);

        public float HealthIndex =>
            0.20f * ScoreBone + 0.15f * ScoreMuscle + 0.15f * ScoreCardio +
            0.10f * ScoreVision + 0.25f * ScoreRadiation + 0.15f * ScorePsych;

        /// <summary>Severidad 0..1 por zona del cuerpo para colorear el astronauta 3D.</summary>
        public float Severity(string region)
        {
            float boneSev = Mathf.Clamp01((100f - Bone) / 20f);
            float muscleSev = Mathf.Clamp01((100f - Muscle) / 30f);
            switch (region)
            {
                case "legs": return Mathf.Max(boneSev, muscleSev);
                case "spine": case "pelvis": return boneSev;
                case "arms": return muscleSev * 0.7f;
                case "chest": return Mathf.Max(Mathf.Clamp01((100f - Vo2) / 30f), Mathf.Max(PlasmaDeficit / 24f, Mathf.Clamp01((Sbp - 130f) / 30f)));
                case "eyes": return Sans;
                case "head": return Mathf.Max(Stress / 100f, Mathf.Max(PlasmaDeficit / 24f, SleepDebtH / 15f));
                case "lungs": return Mathf.Clamp01(Mathf.Max((Co2Exposure - 3f) / 4f, (Pm25Exposure - 15f) / 40f));
                case "kidney": return Mathf.Clamp01((UrinaryCa - 250f) / 100f);
                case "radiation": return Mathf.Clamp01(DoseMsv / 1000f);
            }
            return 0f;
        }

        /// <summary>Lista de síntomas legibles con su severidad.</summary>
        public List<(string zone, string text, float sev)> Symptoms()
        {
            var l = new List<(string, string, float)>();
            if (PlasmaDeficit > 4f) l.Add(("Cabeza", "Desplazamiento de fluidos: cara hinchada, congestión", PlasmaDeficit / 12f));
            if (_orthoTimer > 0f) l.Add(("Corazón", "Hipotensión ortostática: mareo al ponerse de pie", 0.8f));
            if (Bone < 95f) l.Add(("Cadera/columna", $"Pérdida ósea {100f - Bone:0.0} %", (100f - Bone) / 20f));
            if (UrinaryCa > 280f) l.Add(("Riñón", $"Calcio en orina {UrinaryCa:0} mg/día: riesgo de cálculos", Mathf.Clamp01((UrinaryCa - 250f) / 100f)));
            if (Muscle < 92f) l.Add(("Piernas", $"Atrofia muscular {100f - Muscle:0.0} %", (100f - Muscle) / 30f));
            if (Vo2 < 92f) l.Add(("Corazón/pulmón", $"Capacidad aeróbica −{100f - Vo2:0.0} %", (100f - Vo2) / 30f));
            if (Sans > 0.2f) l.Add(("Ojos", "SANS: edema de disco óptico, visión cercana borrosa", Sans));
            if (Co2Exposure > 4f) l.Add(("Cabeza", $"CO₂ {Co2Exposure:0.0} mmHg: cefalea, menor concentración", Mathf.Clamp01((Co2Exposure - 3f) / 4f)));
            if (Pm25Exposure > 25f) l.Add(("Pulmones", $"Polvo PM2.5 {Pm25Exposure:0} µg/m³: irritación de vías respiratorias", Mathf.Clamp01(Pm25Exposure / 60f)));
            if (SleepDebtH > 4f) l.Add(("Mente", $"Deuda de sueño {SleepDebtH:0.0} h: fatiga, alerta EEG baja", SleepDebtH / 15f));
            if (Sbp > 135f && Activity < 3f) l.Add(("Corazón", $"Presión elevada {Sbp:0}/{Dbp:0} mmHg (estrés ambiental)", Mathf.Clamp01((Sbp - 130f) / 30f)));
            if (LastAcuteMsv > 100f) l.Add(("Todo el cuerpo", $"Dosis aguda alta ({LastAcuteMsv:0} mSv): náuseas, fatiga", Mathf.Clamp(LastAcuteMsv / 1000f, 0.3f, 1f)));
            if (DoseMsv > 300f) l.Add(("Todo el cuerpo", $"Riesgo de cáncer +{CancerRiskPercent:0.0} % (dosis {DoseMsv:0} mSv)", DoseMsv / 1000f));
            if (Stress > 50f) l.Add(("Mente", "Estrés por aislamiento, cortisol elevado", Stress / 100f));
            return l;
        }

        void CheckThresholds(List<string> alerts)
        {
            Flag(alerts, "osteo", Bone < 90f, $"{Name}: osteopenia (hueso < 90 %)");
            Flag(alerts, "fract", Bone < 80f, $"{Name}: riesgo ALTO de fractura (hueso < 80 %)");
            Flag(alerts, "musc", Muscle < 85f, $"{Name}: atrofia muscular > 15 %");
            Flag(alerts, "sans", Sans > 0.5f, $"{Name}: SANS afecta la visión");
            Flag(alerts, "dose", DoseMsv > 600f, $"{Name}: superó el límite de carrera de 600 mSv");
            Flag(alerts, "ars", LastSpeMsv > 500f, $"{Name}: síndrome agudo de radiación probable");
            Flag(alerts, "stress", Stress > 70f, $"{Name}: estrés crítico");
            Flag(alerts, "kidney", UrinaryCa > 300f, $"{Name}: hipercalciuria (> 300 mg/día)");
        }

        void Flag(List<string> alerts, string key, bool cond, string text)
        {
            if (cond && _flags.Add(key)) alerts.Add(text);
        }
    }
}
