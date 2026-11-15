using System.Linq;
using System.Threading;
using SpaceApps.Bus;
using UnityEditor;
using UnityEngine;

namespace SpaceApps.EditorTools
{
    /// <summary>
    /// Prueba de conexión con el bus AEGIS (real o herramientas/bus_aegis_simulado.py).
    /// Menú Space Apps → Probar conexión con el bus, o en modo batch:
    ///   Unity -batchmode -projectPath ... -executeMethod SpaceApps.EditorTools.AegisBusTest.Run -aegis ws://127.0.0.1:8001/ws -quit
    /// </summary>
    public static class AegisBusTest
    {
        [MenuItem("Space Apps/Probar conexión con el bus AEGIS", priority = 30)]
        public static void Run()
        {
            var link = new AegisLink();
            var args = System.Environment.GetCommandLineArgs();
            int ai = System.Array.IndexOf(args, "-aegis");
            if (ai >= 0 && ai + 1 < args.Length) link.Url = args[ai + 1];
            Debug.Log($"[SpaceApps][Bus] conectando a {link.Url} (subprotocolo {link.Subprotocol})");
            link.Connect();
            var t0 = System.DateTime.Now;
            int processed = 0;
            while ((System.DateTime.Now - t0).TotalSeconds < 15 && link.Client.Received < 4)
            {
                Thread.Sleep(100);
            }
            while (link.Client.TryDequeue(out var m)) { link.Ingest(m); processed++; }
            Debug.Log($"[SpaceApps][Bus] estado: {link.Client.Status} · subprotocolo negociado: '{link.Client.NegotiatedProtocol}' · mensajes: {processed} · error: {link.Client.LastError}");
            if (processed == 0)
            {
                Debug.LogError("[SpaceApps][Bus] FALLO: no llegó ningún mensaje");
                link.Disconnect();
                return;
            }
            Debug.Log($"[SpaceApps][Bus] día {link.Day} / {link.TotalDays} · escenario '{link.Scenario}' · fase '{link.Phase}' · escena 3D → {link.SceneKey()} · eventos solares vistos: {link.SolarEventCounter}");
            foreach (var a in link.Crew)
            {
                var sev = SpaceAppsSimulation.BusSeverity(a);
                Debug.Log($"[SpaceApps][Bus] {a.Id} {a.Name}: dosis {a.DoseMsv} mSv · sueño {a.SleepH} h · hueso {a.BoneRaw} ({a.BonePct:0.0} %) · aeróbica {a.AerobicRaw} ({a.AerobicPct:0.0} %) · " +
                          "escala visual " + string.Join(", ", sev.Select(kv => $"{kv.Key} {kv.Value:0.00}")));
            }
            foreach (var al in link.Alerts)
                Debug.Log($"[SpaceApps][Bus] alerta [{al.Level}{(al.IsRed ? ", roja" : "")}] {al.AstronautId}: {al.Text} · fuente: {al.Source}");
            link.Disconnect();
            Debug.Log("[SpaceApps][Bus] OK");
        }
    }
}
