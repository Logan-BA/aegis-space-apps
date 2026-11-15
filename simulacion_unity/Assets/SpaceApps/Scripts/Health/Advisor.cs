using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>Una contramedida recomendada por el sistema, con su motivo y su beneficio calculado.</summary>
    public class Recommendation
    {
        public string Key, Icon, Title, KidText, Why, Benefit;
        public float Current, Target;
        public bool Experimental;
        public bool Applied => Mathf.Abs(Current - Target) < 0.05f;
    }

    /// <summary>Un problema de salud detectado, con texto simple y texto profesional.</summary>
    public class Problem
    {
        public int Crew;
        public string Icon, System, KidText, ProText;
        public float Severity;       // 0..1
        public float BoneSeverity;   // para dibujar el hueso agrietado
    }

    /// <summary>
    /// Asesor médico de la misión:
    ///  - Calcula las contramedidas a partir del estado de la tripulación y del entorno (no las
    ///    escribe el usuario). Cada recomendación trae el beneficio estimado con las mismas
    ///    ecuaciones del modelo de salud.
    ///  - Piloto automático: si está activo, aplica las recomendaciones cada día.
    ///  - Lista de problemas críticos de toda la tripulación, del más grave al menos grave.
    /// </summary>
    public static class Advisor
    {
        public static bool AutoPilot = true;

        public static List<Recommendation> Recommend(Simulation sim)
        {
            var s = sim.Current;
            var cm = sim.Cm;
            var list = new List<Recommendation>();
            var crew = sim.Crew;
            float worstBone = crew.Min(h => h.Bone), worstStress = crew.Max(h => h.Stress), worstDebt = crew.Max(h => h.SleepDebtH);
            float m = crew.Average(h => h.DeclineMultiplier);
            float u = Mathf.Clamp01(1f - s.Gravity - (s.Transit ? cm.ArtificialG : 0f));

            // 1. Ejercicio
            if (u > 0f)
            {
                float now = 0.04f * u * (1f - 0.6f * cm.Exercise) * m * 30f, best = 0.04f * u * (1f - 0.6f) * m * 30f;
                float mNow = 0.12f * u * (1f - 0.7f * cm.Exercise) * m * 30f, mBest = 0.12f * u * 0.3f * m * 30f;
                list.Add(new Recommendation
                {
                    Key = "exercise", Icon = "dumbbell", Title = "Ejercicio 2,5 h al día", Current = cm.Exercise, Target = 1f,
                    KidText = "¡A entrenar! Sin gravedad los huesos y músculos se debilitan si no se usan.",
                    Why = $"Gravedad {s.Gravity:0.##} g: el hueso pierde carga.",
                    Benefit = cm.Exercise >= 0.99f ? $"Protege: hueso −{best:0.00} %/mes, músculo −{mBest:0.0} %/mes" : $"Hueso −{now:0.00} → −{best:0.00} %/mes",
                });
            }
            else
            {
                list.Add(new Recommendation
                {
                    Key = "exercise", Icon = "dumbbell", Title = "Ejercicio de recuperación", Current = cm.Exercise, Target = 0.6f,
                    KidText = "En la Tierra el cuerpo se recupera: ejercicio suave cada día.",
                    Why = "Con gravedad normal el hueso y el músculo se recuperan solos.", Benefit = "Mantiene el estado físico sin sobrecargar",
                });
            }

            // 2. Blindaje
            if (s.DoseRate > 0.1f)
            {
                float now = s.DoseRate * (1f - 0.35f * cm.Shielding), best = s.DoseRate * (1f - 0.35f);
                list.Add(new Recommendation
                {
                    Key = "shielding", Icon = "shield", Title = "Blindaje máximo", Current = cm.Shielding, Target = 1f,
                    KidText = "Paredes más gruesas (agua y plástico) frenan la radiación del espacio.",
                    Why = $"Radiación del entorno: {s.DoseRate:0.##} mSv/día.",
                    Benefit = cm.Shielding >= 0.99f ? $"Protege: {best:0.00} mSv/día en vez de {s.DoseRate:0.00}" : $"Dosis {now:0.00} → {best:0.00} mSv/día",
                });
            }

            // 3. Refugio para tormentas solares
            if (s.SpeExposure > 0f)
                list.Add(new Recommendation
                {
                    Key = "shelter", Icon = "sun", Title = "Refugio solar", Current = cm.Shelter ? 1f : 0f, Target = 1f,
                    KidText = "Si el Sol lanza una tormenta, la tripulación se esconde en la zona más protegida.",
                    Why = $"Fuera de la magnetosfera ({s.SpeExposure * 100f:0} % de exposición).",
                    Benefit = $"Tormenta fuerte: {400f * s.SpeExposure:0} → {40f * s.SpeExposure:0} mSv",
                });

            // 4. Depurador de CO2
            if (s.Cabin)
            {
                float maxCo2 = sim.Env.MaxCo2;
                float target = maxCo2 > 3f ? 1f : maxCo2 > 2.5f ? Mathf.Max(cm.Scrubber, 0.6f) : cm.Scrubber;
                float kNow = 0.6f + 1.6f * cm.Scrubber, kBest = 0.6f + 1.6f * target;
                list.Add(new Recommendation
                {
                    Key = "scrubber", Icon = "air", Title = "Limpiar el aire (CO₂)", Current = cm.Scrubber, Target = target,
                    KidText = "El aire de la nave se limpia con máquinas; si hay mucho CO₂ duele la cabeza.",
                    Why = $"CO₂ más alto en la cabina: {maxCo2:0.0} mmHg.",
                    Benefit = $"CO₂ máximo ≈ {maxCo2:0.0} → {Mathf.Max(0.3f, maxCo2 * kNow / kBest):0.0} mmHg",
                });
            }

            // 5. Iluminación circadiana
            if (s.Circadian > 0.1f || worstDebt > 3f)
            {
                float gain = 2.2f * s.Circadian * 0.6f * (1f - cm.Lighting);
                list.Add(new Recommendation
                {
                    Key = "lighting", Icon = "moon", Title = "Luz de día y noche", Current = cm.Lighting, Target = 1f,
                    KidText = "Luces que cambian como el Sol ayudan a dormir bien.",
                    Why = $"Ciclo día/noche alterado ({s.Circadian * 100f:0} %) · deuda de sueño máx. {worstDebt:0.0} h.",
                    Benefit = $"Sueño +{gain:0.0} h por noche",
                });
            }

            // 6. Apoyo psicológico
            if (s.Isolation > 0.3f || worstStress > 40f)
                list.Add(new Recommendation
                {
                    Key = "support", Icon = "gauge", Title = "Apoyo psicológico", Current = cm.Support, Target = 1f,
                    KidText = "Hablar con la familia y descansar baja el estrés.",
                    Why = $"Aislamiento {s.Isolation:0.0} · estrés máx. {worstStress:0}.",
                    Benefit = $"Estrés de equilibrio −{18f * (1f - cm.Support):0} puntos",
                });

            // 7. Gravedad artificial (experimental, solo en tránsito)
            if (s.Transit)
            {
                float cur = 0.04f * Mathf.Clamp01(1f - cm.ArtificialG) * 0.4f * m * 30f, best = 0.04f * 0.7f * 0.4f * m * 30f;
                list.Add(new Recommendation
                {
                    Key = "artificialg", Icon = "planet", Title = "Gravedad artificial 0,3 g", Current = cm.ArtificialG / 0.3f, Target = 1f,
                    Experimental = true,
                    KidText = "Hacer girar la nave crea un poco de peso, como en una calesita.",
                    Why = "Tecnología en estudio; reduce la falta de peso durante el viaje.",
                    Benefit = $"Hueso −{cur:0.00} → −{best:0.00} %/mes",
                });
            }

            // Ordenar: primero las que faltan aplicar
            return list.OrderBy(r => r.Applied).ThenBy(r => r.Experimental).ToList();
        }

        public static void Apply(Simulation sim, Recommendation r)
        {
            var cm = sim.Cm;
            switch (r.Key)
            {
                case "exercise": cm.Exercise = r.Target; break;
                case "shielding": cm.Shielding = r.Target; break;
                case "shelter": cm.Shelter = r.Target > 0.5f; break;
                case "scrubber": cm.Scrubber = r.Target; break;
                case "lighting": cm.Lighting = r.Target; break;
                case "support": cm.Support = r.Target; break;
                case "artificialg": cm.ArtificialG = 0.3f * r.Target; break;
            }
            sim.AddLog($"Contramedida aplicada: {r.Title}");
        }

        /// <summary>Piloto automático: aplica cada día las recomendaciones (no las experimentales).</summary>
        public static void AutoApply(Simulation sim)
        {
            if (!AutoPilot) return;
            foreach (var r in Recommend(sim))
                if (!r.Applied && !r.Experimental)
                {
                    Apply(sim, r);
                }
        }

        // ------------------------------------------------------------ problemas críticos

        public static List<Problem> Problems(Simulation sim)
        {
            var list = new List<Problem>();
            for (int i = 0; i < sim.Crew.Count; i++)
            {
                var h = sim.Crew[i];
                float boneSev = Mathf.Clamp01((100f - h.Bone) / 20f);
                Add(list, i, "bone", "Densidad ósea", boneSev, boneSev,
                    boneSev > 0.5f ? "¡Huesos frágiles! Riesgo de fractura" : "Los huesos se están debilitando",
                    $"Densidad ósea {h.Bone:0.0} % (−{100f - h.Bone:0.0} %) · Ca urinario {h.UrinaryCa:0} mg/d");
                Add(list, i, "dumbbell", "Masa muscular", Mathf.Clamp01((100f - h.Muscle) / 30f), 0,
                    "Los músculos pierden fuerza", $"Masa muscular {h.Muscle:0.0} % · EMG fatiga {h.EmgFatigue:0.00}");
                Add(list, i, "radiation", "Dosis de radiación", Mathf.Clamp01(h.DoseMsv / 600f), 0,
                    h.DoseMsv > 600f ? "¡Pasó el límite de radiación!" : "Acumula radiación del espacio",
                    $"Dosis {h.DoseMsv:0} mSv de 600 (límite de carrera) · riesgo de cáncer +{h.CancerRiskPercent:0.0} %");
                Add(list, i, "eye", "Visión (SANS)", h.Sans, 0, "La vista se nubla (presión en los ojos)", $"Índice SANS {h.Sans:0.00}");
                Add(list, i, "battery", "Capacidad aeróbica", Mathf.Clamp01((100f - h.Vo2) / 30f), 0,
                    "Se cansa más rápido", $"VO₂ máx {h.Vo2:0.0} % · FC reposo {h.HeartRate:0} lpm");
                Add(list, i, "moon", "Sueño", Mathf.Clamp01(h.SleepDebtH / 10f), 0,
                    "Duerme poco y está cansado", $"Sueño {h.SleepHoursLast:0.0} h · deuda {h.SleepDebtH:0.0} h · REM {h.RemPercent:0} %");
                Add(list, i, "gauge", "Estrés psicológico", Mathf.Clamp01((h.Stress - 20f) / 60f), 0,
                    "Se siente estresado y lejos de casa", $"Estrés {h.Stress:0}/100 · cortisol {h.Cortisol:0.0} µg/dL · GSR {h.Gsr:0.0} µS");
                Add(list, i, "kidney", "Función renal", Mathf.Clamp01((h.UrinaryCa - 250f) / 100f), 0,
                    "Riesgo de piedras en el riñón", $"Calcio urinario {h.UrinaryCa:0} mg/día");
                Add(list, i, "air", "Calidad del aire", Mathf.Clamp01((h.Co2Exposure - 3f) / 3f), 0,
                    "El aire tiene mucho CO₂", $"CO₂ medio respirado {h.Co2Exposure:0.0} mmHg · PM2.5 {h.Pm25Exposure:0} µg/m³");
                Add(list, i, "heart", "Cardiovascular", 0.6f * Mathf.Clamp01(Mathf.Max(h.PlasmaDeficit - 6f, 0f) / 6f), 0,
                    "Puede marearse al volver a la gravedad", $"Déficit de plasma {h.PlasmaDeficit:0.0} % · PA {h.Sbp:0}/{h.Dbp:0}");
            }
            return list.OrderByDescending(p => p.Severity).ToList();
        }

        static void Add(List<Problem> l, int crew, string icon, string sys, float sev, float boneSev, string kid, string pro)
        {
            if (sev < 0.35f) return;   // solo lo que está en "Cuidado" o "¡Peligro!"
            l.Add(new Problem { Crew = crew, Icon = icon, System = sys, Severity = sev, BoneSeverity = boneSev, KidText = kid, ProText = pro });
        }

        // ------------------------------------------------------------ logros (gamificación)

        public static List<(string icon, string title, bool earned, string how)> Badges(Simulation sim)
        {
            var c = sim.Crew;
            int day = sim.Day;
            return new List<(string, string, bool, string)>
            {
                ("bone0", "Huesos de acero", day > 90 && c.All(h => h.Bone > 95f), "Hueso > 95 % en todos después de 90 días"),
                ("shield", "Escudo solar", sim.SpeCount > 0 && c.All(h => h.SpeDoseMsv < 50f), "Pasar una tormenta solar con menos de 50 mSv"),
                ("moon", "Buen descanso", day > 30 && c.All(h => h.SleepDebtH < 4f), "Deuda de sueño < 4 h en todos"),
                ("radiation", "Bajo el límite", day > 300 && c.All(h => h.DoseMsv < 600f), "Más de 300 días sin pasar 600 mSv"),
                ("star", "Misión completa", sim.Finished, "Terminar la misión"),
            };
        }
    }
}
