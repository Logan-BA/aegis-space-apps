using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpaceApps.Assistant
{
    /// <summary>Una condición de salud en el espacio, explicada para la tripulación y el público.</summary>
    public class Condition
    {
        public string Key, Name, What, Causes, Effects, Mission, Actions, Simple;
        public string[] Words;   // palabras con que la gente pregunta por ella
    }

    /// <summary>
    /// Conocimiento médico de ASTRA y motor de respuestas local (sin modelo de lenguaje).
    /// Se usa siempre para las alertas automáticas y como respaldo si Ollama no está disponible;
    /// también arma el "estado de la simulación" que recibe el modelo local.
    /// </summary>
    public static class MedicalKnowledge
    {
        public static readonly List<Condition> Conditions = new List<Condition>
        {
            new Condition { Key = "Densidad ósea", Name = "pérdida de densidad ósea", Words = new[] { "hueso", "huesos", "osea", "oseo", "osteo", "fractura", "calcio" },
                What = "los huesos pierden minerales y se vuelven más frágiles",
                Causes = "sin gravedad el esqueleto no carga peso, y el cuerpo deja de reconstruirlo: pierde cerca de 1 a 1,5 % por mes en cadera y columna",
                Effects = "más riesgo de fracturas, sobre todo al volver a pisar con gravedad, y calcio en la orina que puede formar cálculos en el riñón",
                Mission = "una fractura en Marte o en el viaje no se puede tratar como en un hospital, y la recuperación en la Tierra tarda años",
                Actions = "hacer el ejercicio de resistencia todos los días, mantener calcio y vitamina D, y evitar golpes o saltos bruscos al aterrizar",
                Simple = "es como una viga que nadie usa: si no recibe peso, el cuerpo la va desarmando" },
            new Condition { Key = "Masa muscular", Name = "atrofia muscular", Words = new[] { "musculo", "musculos", "fuerza", "atrofia", "piernas" },
                What = "los músculos se achican y pierden fuerza, sobre todo en piernas y espalda",
                Causes = "en microgravedad no hace falta sostener el cuerpo, así que los músculos posturales casi no trabajan",
                Effects = "menos fuerza y resistencia, más cansancio y dificultad para caminar al volver a la gravedad",
                Mission = "en la superficie de Marte se necesita fuerza para caminar con el traje y hacer tareas",
                Actions = "cumplir las dos horas y media diarias de ejercicio con carga y bicicleta, y comer suficiente proteína",
                Simple = "es lo mismo que pasa con una pierna enyesada: lo que no se usa, se pierde" },
            new Condition { Key = "Dosis de radiación", Name = "exposición a radiación", Words = new[] { "radiacion", "dosis", "cancer", "rayos", "tormenta", "sol", "solar", "msv" },
                What = "el cuerpo acumula radiación del espacio, que daña el ADN de las células",
                Causes = "fuera de la protección del campo magnético de la Tierra llegan rayos cósmicos y, a veces, tormentas solares",
                Effects = "aumenta el riesgo de cáncer, cataratas y daño al corazón a largo plazo; una tormenta fuerte sin refugio puede causar náuseas y fatiga en horas",
                Mission = "el límite de toda la carrera de un astronauta es de 600 milisievert; un viaje completo a Marte puede acercarse o pasarlo",
                Actions = "mantener el blindaje al máximo, ir al refugio apenas se avise una tormenta solar y reducir las salidas al exterior",
                Simple = "es como tomar sol sin protector durante años: el daño no se ve de inmediato, pero se acumula" },
            new Condition { Key = "Visión (SANS)", Name = "síndrome neuro-ocular del vuelo espacial", Words = new[] { "vista", "vision", "ojo", "ojos", "sans", "borroso", "ver" },
                What = "la parte de atrás del ojo se hincha y la visión de cerca se vuelve borrosa",
                Causes = "sin gravedad los líquidos del cuerpo suben a la cabeza y aumentan la presión alrededor del ojo; el CO2 alto lo empeora",
                Effects = "visión borrosa de cerca, pliegues en la retina y, en algunos casos, cambios que duran después del vuelo",
                Mission = "la tripulación necesita ver bien para leer instrumentos y pilotar en el momento del aterrizaje",
                Actions = "controles de vista periódicos, mantener el CO2 bajo y usar lentes de corrección si se indica",
                Simple = "es como estar mucho tiempo cabeza abajo: la sangre y los líquidos presionan los ojos" },
            new Condition { Key = "Capacidad aeróbica", Name = "desacondicionamiento cardiovascular", Words = new[] { "corazon", "aerobic", "vo2", "cansancio", "energia", "cardio" },
                What = "el corazón y los pulmones pierden capacidad para llevar oxígeno durante el esfuerzo",
                Causes = "sin gravedad el corazón trabaja menos y se adapta a ese menor esfuerzo",
                Effects = "cansancio más rápido, pulso más alto ante cualquier tarea",
                Mission = "las caminatas exteriores y las emergencias exigen buena resistencia física",
                Actions = "ejercicio aeróbico diario en la bicicleta o la cinta, y controlar la frecuencia cardíaca durante el esfuerzo",
                Simple = "es como un motor que nunca acelera: cuando por fin lo necesitas, rinde menos" },
            new Condition { Key = "Cardiovascular", Name = "intolerancia ortostática", Words = new[] { "mareo", "desmayo", "presion", "plasma", "fluidos", "liquidos" },
                What = "el volumen de sangre baja y la presión puede caer al ponerse de pie con gravedad",
                Causes = "en microgravedad el cuerpo elimina líquidos porque cree que sobran en la cabeza",
                Effects = "mareos, visión oscura o desmayo al pararse después de aterrizar",
                Mission = "es crítico al llegar a Marte y al volver a la Tierra, cuando hay que moverse rápido",
                Actions = "tomar líquidos y sal antes del aterrizaje, usar trajes de compresión y levantarse despacio",
                Simple = "es como pararse rápido después de estar mucho rato acostado, pero mucho más fuerte" },
            new Condition { Key = "Sueño", Name = "falta de sueño", Words = new[] { "sueño", "sueno", "dormir", "duerme", "cansado", "insomnio", "fatiga" },
                What = "la tripulación duerme menos horas de las que necesita y acumula cansancio",
                Causes = "no hay un ciclo normal de día y noche, hay ruido de ventiladores, estrés y a veces CO2 alto",
                Effects = "menos atención, errores, mal humor y peor recuperación física",
                Mission = "un error por cansancio en una maniobra puede poner en riesgo a toda la tripulación",
                Actions = "mantener horarios fijos, usar luz que imita el día y la noche, y reducir el ruido en los dormitorios",
                Simple = "es como estudiar toda la noche antes de un examen, pero durante meses" },
            new Condition { Key = "Estrés psicológico", Name = "estrés por aislamiento", Words = new[] { "estres", "animo", "triste", "ansiedad", "aislamiento", "psicolog", "familia", "soledad" },
                What = "la carga emocional de estar lejos, encerrados y en riesgo durante mucho tiempo",
                Causes = "aislamiento, poca privacidad, comunicación con demora con la Tierra y la presión de la misión",
                Effects = "cansancio, irritabilidad, conflictos y peor sueño; sube el cortisol, la hormona del estrés",
                Mission = "el trabajo en equipo es vital; el estrés alto aumenta los errores",
                Actions = "contacto con la familia, momentos de descanso y apoyo psicológico regular",
                Simple = "es como pasar meses en un cuarto pequeño con las mismas personas y sin poder salir" },
            new Condition { Key = "Función renal", Name = "riesgo de cálculos renales", Words = new[] { "riñon", "rinon", "calculo", "orina", "piedras" },
                What = "sube el calcio en la orina y pueden formarse piedras en el riñón",
                Causes = "el calcio que sale de los huesos en microgravedad termina en la orina",
                Effects = "dolor muy fuerte y posible infección si se forma un cálculo",
                Mission = "un cálculo renal lejos de la Tierra es una emergencia médica difícil de tratar",
                Actions = "beber suficiente agua, cuidar la dieta y proteger los huesos con ejercicio",
                Simple = "es como cuando el agua dura deja sarro en una tetera" },
            new Condition { Key = "Calidad del aire", Name = "dióxido de carbono alto", Words = new[] { "aire", "co2", "dioxido", "respirar", "polvo", "oxigeno" },
                What = "el aire de la nave acumula dióxido de carbono que exhala la tripulación",
                Causes = "el depurador no alcanza a limpiarlo y el aire circula en un circuito cerrado",
                Effects = "dolor de cabeza, menos concentración, respiración más rápida y puede empeorar la visión",
                Mission = "afecta la atención en tareas críticas y el sueño de toda la tripulación",
                Actions = "subir la potencia del depurador de CO2 y ventilar bien las zonas donde hay gente",
                Simple = "es como estar en un cuarto cerrado con mucha gente y sin ventanas" },
        };

        // ------------------------------------------------------------ estado de la simulación para la IA

        /// <summary>Resumen compacto con los datos reales de la simulación (lo lee el modelo local).</summary>
        public static string Snapshot(Simulation sim, int selected)
        {
            var ci = CultureInfo.InvariantCulture;
            var s = sim.Current;
            var sb = new StringBuilder();
            string leg = sim.Legs.Count > 0 ? MissionData.Scenarios[sim.Legs[sim.LegIndex].key].Label : s.Label;
            sb.AppendLine($"ESTADO DE LA SIMULACIÓN (día {sim.Day} de {sim.TotalDays.ToString("0", ci)}; misión: {MissionData.Missions[sim.MissionIndex].Label}; fase actual: {leg}; gravedad {s.Gravity.ToString("0.###", ci)} g; radiación del entorno {s.DoseRate.ToString("0.##", ci)} mSv/día; CO2 promedio de cabina {sim.Env.MeanCo2.ToString("0.0", ci)} mmHg; tormentas solares hasta ahora: {sim.SpeCount}).");
            var h0 = sim.Crew[selected];
            sb.AppendLine($"Tripulante seleccionado en pantalla: {h0.Name} ({h0.Role}).");
            sb.AppendLine("Tripulación:");
            foreach (var h in sim.Crew)
                sb.AppendLine(string.Format(ci, "- {0} ({1}, {2} años): índice de salud {3:0}/100; densidad ósea {4:0.0}%; masa muscular {5:0.0}%; VO2 máx {6:0.0}%; índice SANS {7:0.00}; dosis acumulada {8:0} mSv de 600; sueño {9:0.0} h, deuda {10:0.0} h; estrés {11:0}/100; FC {12:0} lpm; presión {13:0}/{14:0}; SpO2 {15:0}%; temperatura {16:0.0} °C; calcio en orina {17:0} mg/día.",
                    h.Name, h.Role, h.Age, h.HealthIndex, h.Bone, h.Muscle, h.Vo2, h.Sans, h.DoseMsv, h.SleepHoursLast, h.SleepDebtH, h.Stress, h.HeartRate, h.Sbp, h.Dbp, h.SpO2, h.CoreTemp, h.UrinaryCa));
            var probs = Advisor.Problems(sim);
            sb.AppendLine("Problemas activos (del más grave al menos grave):");
            if (probs.Count == 0) sb.AppendLine("- Ninguno: todos los sistemas en rango normal.");
            foreach (var g in probs.GroupBy(p => p.System).Take(6))
            {
                var p = g.First();
                string who = string.Join(", ", g.Select(x => sim.Crew[x.Crew].Name).Distinct());
                sb.AppendLine($"- {p.System} ({(p.Severity >= 0.65f ? "CRÍTICO" : "PRECAUCIÓN")}) en {who}: {p.ProText}.");
            }
            var cm = sim.Cm;
            sb.AppendLine(string.Format(ci, "Contramedidas actuales: ejercicio {0:0}%, blindaje {1:0}%, refugio solar {2}, depurador de CO2 {3:0}%, apoyo psicológico {4:0}%, iluminación circadiana {5:0}%, gravedad artificial {6:0.0} g.",
                cm.Exercise * 100, cm.Shielding * 100, cm.Shelter ? "sí" : "no", cm.Scrubber * 100, cm.Support * 100, cm.Lighting * 100, cm.ArtificialG));
            var pend = Advisor.Recommend(sim).Where(r => !r.Applied).ToList();
            if (pend.Count > 0) sb.AppendLine("Recomendaciones pendientes: " + string.Join("; ", pend.Select(r => $"{r.Title} ({r.Benefit})")) + ".");
            return sb.ToString();
        }

        // ------------------------------------------------------------ motor de respuestas local

        public static string Norm(string s)
        {
            var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in d) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString();
        }

        static bool Has(string q, params string[] words) => words.Any(w => q.Contains(w));

        static int FindCrew(Simulation sim, string q)
        {
            for (int i = 0; i < sim.Crew.Count; i++)
            {
                var parts = Norm(sim.Crew[i].Name).Split(' ');
                if (parts.Any(p => p.Length > 2 && q.Contains(p))) return i;
                if (q.Contains(Norm(sim.Crew[i].Role))) return i;
            }
            return -1;
        }

        static Condition FindCondition(string q) => Conditions.FirstOrDefault(c => c.Words.Any(w => q.Contains(Norm(w))));

        public static string Status(float sev) => sev >= 0.65f ? "crítico" : sev >= 0.35f ? "en precaución" : "normal";

        /// <summary>Respuesta local: (emoción, texto para decir en voz alta).</summary>
        public static (Emotion emo, string text) Answer(Simulation sim, int selected, string question)
        {
            string q = Norm(question);
            var probs = Advisor.Problems(sim);
            int who = FindCrew(sim, q);
            var cond = FindCondition(q);

            if (Has(q, "hola", "buenos dias", "buenas"))
                return (Emotion.Amable, $"Hola, soy ASTRA, la asistente médica de a bordo. Estoy vigilando a los {sim.Crew.Count} tripulantes. Puedes preguntarme cómo está cada uno, por qué pasa algo o qué deben hacer.");
            if (Has(q, "quien eres", "que eres", "que haces", "tu nombre", "como te llamas"))
                return (Emotion.Amable, "Soy ASTRA, la asistente de salud y triaje de la nave. Si la tripulación se queda sin médico, analizo sus datos, explico qué les pasa y recomiendo qué hacer para mantenerlos con vida.");
            if (Has(q, "gracias"))
                return (Emotion.Feliz, "Con gusto. Sigo atenta a cada tripulante; avísame si alguien se siente mal.");
            if (Has(q, "emergencia", "urgente", "grave", "critico", "peligro", "prioridad", "triaje"))
                return Triage(sim, probs);

            if (cond != null)
            {
                bool why = Has(q, "por que", "causa", "porque");
                bool todo = Has(q, "que hago", "que hacer", "que debe", "recomiend", "consejo", "tratamiento", "como evitar", "prevenir", "solucion");
                var target = who >= 0 ? who : selected;
                var p = probs.FirstOrDefault(x => x.System == cond.Key && x.Crew == target) ?? probs.FirstOrDefault(x => x.System == cond.Key);
                string state = p != null ? $"En {sim.Crew[p.Crew].Name} está {Status(p.Severity)}: {p.ProText}." : "Ahora mismo está dentro de lo normal en la tripulación.";
                string body = why && todo ? $"Sucede porque {cond.Causes}. Lo que deben hacer: {cond.Actions}." :
                              why ? $"Sucede porque {cond.Causes}." :
                              todo ? $"Lo que deben hacer: {cond.Actions}." :
                              $"Es {cond.Name}: {cond.What}. {Cap(cond.Simple)}.";
                var emo = p == null ? Emotion.Amable : p.Severity >= 0.65f ? Emotion.Preocupada : Emotion.Neutral;
                if (p != null && p.Severity >= 0.65f && todo && !Applied(sim, cond.Key)) emo = Emotion.Firme;
                return (emo, $"{body} {state}");
            }

            if (who < 0 && Has(q, "mision", "fase", "cuanto falta", "dia ", "destino", "marte", "viaje", "donde estamos"))
                return (Emotion.Neutral, MissionText(sim));
            if (who >= 0 || Has(q, "como esta", "diagnost", "estado", "que le pasa", "como se encuentra", "salud"))
            {
                if (who < 0 && Has(q, "todos", "tripulacion", "equipo")) return CrewSummary(sim, probs);
                return Diagnose(sim, who >= 0 ? who : selected, probs);
            }
            if (Has(q, "todos", "tripulacion", "equipo")) return CrewSummary(sim, probs);
            if (Has(q, "que hago", "que hacer", "recomiend", "consejo", "contramedida"))
                return Recommend(sim);
            return (Emotion.Pensando, "No estoy segura de haber entendido. Puedes preguntarme por un tripulante, por los huesos, la radiación, la vista, el sueño, el aire o por la misión.");
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        static bool Applied(Simulation sim, string system)
        {
            var cm = sim.Cm;
            switch (system)
            {
                case "Densidad ósea": case "Masa muscular": case "Capacidad aeróbica": return cm.Exercise > 0.95f;
                case "Dosis de radiación": return cm.Shielding > 0.95f && cm.Shelter;
                case "Calidad del aire": return cm.Scrubber > 0.95f;
                case "Sueño": return cm.Lighting > 0.95f;
                case "Estrés psicológico": return cm.Support > 0.95f;
                default: return true;
            }
        }

        public static (Emotion, string) Diagnose(Simulation sim, int i, List<Problem> probs)
        {
            var h = sim.Crew[i];
            var mine = probs.Where(p => p.Crew == i).OrderByDescending(p => p.Severity).ToList();
            if (mine.Count == 0)
                return (Emotion.Feliz, $"{h.Name} está bien: índice de salud {h.HealthIndex:0} de 100, sin problemas fuera de rango. Que siga con su ejercicio y su descanso.");
            var top = mine[0];
            var c = Conditions.FirstOrDefault(x => x.Key == top.System);
            var sb = new StringBuilder($"{h.Name} tiene un índice de salud de {h.HealthIndex:0} sobre 100. Lo más importante es {c?.Name ?? top.System}, que está {Status(top.Severity)}. ");
            if (c != null) sb.Append($"Pasa porque {c.Causes}. Recomiendo {c.Actions}.");
            if (mine.Count > 1) sb.Append($" También vigilo {string.Join(" y ", mine.Skip(1).Take(2).Select(p => p.System.ToLowerInvariant()))}.");
            return (top.Severity >= 0.65f ? Emotion.Preocupada : Emotion.Neutral, sb.ToString());
        }

        static (Emotion, string) CrewSummary(Simulation sim, List<Problem> probs)
        {
            var worst = sim.Crew.OrderBy(h => h.HealthIndex).First();
            int crit = probs.Count(p => p.Severity >= 0.65f);
            string sys = probs.Count > 0 ? $"El problema más extendido es {probs.GroupBy(p => p.System).OrderByDescending(g => g.Count()).First().Key.ToLowerInvariant()}." : "No hay problemas fuera de rango.";
            return (crit > 0 ? Emotion.Preocupada : Emotion.Amable,
                $"La tripulación tiene un índice promedio de {sim.Crew.Average(h => h.HealthIndex):0} sobre 100. Quien más me preocupa es {worst.Name}, con {worst.HealthIndex:0}. {sys} Hay {crit} alertas críticas.");
        }

        static (Emotion, string) Triage(Simulation sim, List<Problem> probs)
        {
            var crit = probs.Where(p => p.Severity >= 0.65f).ToList();
            if (crit.Count == 0) return (Emotion.Amable, "No hay emergencias en este momento. Todos los signos vitales están estables.");
            var p = crit[0];
            var c = Conditions.FirstOrDefault(x => x.Key == p.System);
            return (Emotion.Alerta, $"Prioridad uno: {c?.Name ?? p.System} en {sim.Crew[p.Crew].Name}. {(c != null ? "Acción inmediata: " + c.Actions + "." : "")} Hay {crit.Count} alertas críticas en total.");
        }

        static (Emotion, string) Recommend(Simulation sim)
        {
            var pend = Advisor.Recommend(sim).Where(r => !r.Applied && !r.Experimental).ToList();
            if (pend.Count == 0) return (Emotion.Amable, "Todas las contramedidas recomendadas ya están activas. Lo más importante ahora es cumplir el ejercicio diario, dormir lo suficiente y avisar cualquier síntoma.");
            var r = pend[0];
            return (Emotion.Firme, $"Necesito que activen esto: {r.Title.ToLowerInvariant()}. El motivo: {r.Why.TrimEnd('.')}. Efecto esperado: {r.Benefit}.");
        }

        static string MissionText(Simulation sim)
        {
            if (sim.Legs.Count == 0) return $"Estamos en modo libre, en {sim.Current.Label.ToLowerInvariant()}, día {sim.Day}.";
            int acc = 0; for (int i = 0; i < sim.LegIndex; i++) acc += sim.Legs[i].days;
            int left = (int)sim.TotalDays - sim.Day;
            string leg = MissionData.Scenarios[sim.Legs[sim.LegIndex].key].Label;
            string next = sim.LegIndex + 1 < sim.Legs.Count ? MissionData.Scenarios[sim.Legs[sim.LegIndex + 1].key].Label.ToLowerInvariant() : "el final de la misión";
            int legLeft = acc + sim.Legs[sim.LegIndex].days - sim.Day;
            return $"Es el día {sim.Day} de la misión {MissionData.Missions[sim.MissionIndex].Label}. Estamos en la fase {leg.ToLowerInvariant()}, con gravedad de {sim.Current.Gravity:0.##} g. Faltan {legLeft} días para {next} y {left} días para terminar.";
        }

        /// <summary>Aviso automático cuando aparece un problema crítico.</summary>
        public static (Emotion, string) Announce(Simulation sim, List<Problem> group)
        {
            var p = group[0];
            var c = Conditions.FirstOrDefault(x => x.Key == p.System);
            var names = group.Select(x => sim.Crew[x.Crew].Name).Distinct().ToList();
            string who = names.Count == sim.Crew.Count ? "toda la tripulación" : string.Join(" y ", names);
            return (Emotion.Alerta, $"Atención: {c?.Name ?? p.System} en nivel crítico en {who}. {(c != null ? Cap(c.Actions) + "." : "")}");
        }
    }
}
