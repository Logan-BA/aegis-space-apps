using System.Collections.Generic;
using System.Diagnostics;
using SpaceApps.Assistant;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace SpaceApps.EditorTools
{
    /// <summary>
    /// Prueba de ASTRA sin interfaz: avanza una misión a Marte, arma el estado de la simulación y
    /// pregunta al modelo local de Ollama (y al motor médico propio). Menú Space Apps → Probar ASTRA.
    /// </summary>
    public static class AstraTest
    {
        [MenuItem("Space Apps/Probar ASTRA (IA local)", priority = 60)]
        public static void Run()
        {
            var sim = new Simulation();
            sim.ApplyMission(4);
            for (int h = 0; h < 480 * 24; h++) { sim.StepHour(); if (sim.Hour == 0) Advisor.AutoApply(sim); }
            string snapshot = MedicalKnowledge.Snapshot(sim, 0);
            Debug.Log("[ASTRA][Prueba] ESTADO ENVIADO A LA IA:\n" + snapshot);

            var preguntas = new[]
            {
                "¿Cómo está CMD Alfa? Dame su diagnóstico.",
                "¿Por qué se le nubla la vista a la tripulación y qué deben hacer?",
                "Explica para el público qué pasa con la radiación en esta misión.",
            };
            foreach (var q in preguntas)
            {
                var local = MedicalKnowledge.Answer(sim, 0, q);
                Debug.Log($"[ASTRA][Prueba] LOCAL ({local.emo}) {q} → {local.text}");
            }

            var ollama = new OllamaClient();
            if (!System.Threading.Tasks.Task.Run(() => ollama.Refresh()).GetAwaiter().GetResult()) { Debug.Log("[ASTRA][Prueba] Ollama no responde: " + ollama.LastError); return; }
            string model = ollama.Pick("astra", "qwen2.5:3b");
            Debug.Log($"[ASTRA][Prueba] modelos: {string.Join(", ", ollama.Models)} · usando {model}");
            var w = Stopwatch.StartNew();
            System.Threading.Tasks.Task.Run(() => ollama.Chat(model, new List<(string, string)> { ("system", MedAssistant.DefaultPersona), ("user", "Hola") }, 0.2f, 1)).GetAwaiter().GetResult();
            Debug.Log($"[ASTRA][Prueba] precarga del modelo: {w.Elapsed.TotalSeconds:0.0} s");
            foreach (var q in preguntas)
            {
                var local = MedicalKnowledge.Answer(sim, 0, q);
                var msgs = MedAssistant.BuildMessages(MedAssistant.DefaultPersona, snapshot, new List<(string, string)>(), new List<(string, string)>(), q, local.text);
                var sw = Stopwatch.StartNew();
                string ans = System.Threading.Tasks.Task.Run(() => ollama.Chat(model, msgs, 0.4f, 220)).GetAwaiter().GetResult();
                Debug.Log($"[ASTRA][Prueba] OLLAMA {sw.Elapsed.TotalSeconds:0.0} s · {q}\n→ {ans}");
            }
        }
    }
}
