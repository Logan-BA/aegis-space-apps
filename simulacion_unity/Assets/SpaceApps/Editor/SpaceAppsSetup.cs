using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceApps.EditorTools
{
    /// <summary>
    /// Menú "Space Apps":
    ///  - Crear escena principal: arma Assets/Scenes/SpaceApps.unity lista para Play.
    ///  - Probar simulación: corre una misión completa sin gráficos y escribe un resumen en la consola.
    /// También usable en modo batch:
    ///   Unity -batchmode -projectPath ... -executeMethod SpaceApps.EditorTools.SpaceAppsSetup.CreateSceneBatch -quit
    /// </summary>
    public static class SpaceAppsSetup
    {
        const string ScenePath = "Assets/Scenes/SpaceApps.unity";

        [MenuItem("Space Apps/Crear escena principal", priority = 1)]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var l in Object.FindObjectsByType<Light>()) Object.DestroyImmediate(l.gameObject);
            var go = new GameObject("SpaceApps");
            go.AddComponent<SpaceAppsSimulation>();
            go.AddComponent<SimulationUI>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            CreateShaderReferences();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpaceApps] Escena creada: {ScenePath}. Presiona Play.");
        }

        public static void CreateSceneBatch()
        {
            CreateScene();
            SmokeTest();
        }

        /// <summary>
        /// Los materiales se crean por código con Shader.Find; en una build los shaders no usados se
        /// eliminan. Guardar un material por shader dentro de Resources los mantiene incluidos.
        /// </summary>
        static void CreateShaderReferences()
        {
            const string dir = "Assets/SpaceApps/Resources/ShaderRefs";
            Directory.CreateDirectory(dir);
            foreach (var name in new[] { "Standard", "Unlit/Color", "Skybox/Procedural" })
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                string path = $"{dir}/{name.Replace('/', '_')}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) AssetDatabase.CreateAsset(new Material(sh), path);
            }
        }

        [MenuItem("Space Apps/Probar simulación (misión a Marte)", priority = 20)]
        public static void SmokeTest()
        {
            var sim = new Simulation();
            sim.ApplyMission(4);
            int guard = 0;
            while (!sim.Finished && guard++ < 40000)
            {
                sim.StepHour();
                if (sim.MissionHours == 24 * 200 + 10 || sim.MissionHours == 24 * 600 + 10)   // tránsito y superficie de Marte, 10 h (en reposo)
                {
                    var a = sim.Crew[0]; var w = sim.Warnings[0];
                    Debug.Log($"[SpaceApps][Prueba] día {sim.Day} ({sim.Current.Label}) {a.Name} en {(a.Cell >= 0 ? CabinEnvironment.CellNames[a.Cell] : "EVA")}: " +
                              $"hueso {a.Bone:0.0} músculo {a.Muscle:0.0} VO2 {a.Vo2:0.0} plasma {a.PlasmaDeficit:0.0} dosis {a.DoseMsv:0} · " +
                              $"FC {a.HeartRate:0} HRV {a.Hrv:0} PA {a.Sbp:0}/{a.Dbp:0} SpO2 {a.SpO2:0.0} FR {a.RespRate:0} T {a.CoreTemp:0.00} · " +
                              $"cortisol {a.Cortisol:0.0} glucosa {a.Glucose:0} Ca {a.UrinaryCa:0} sueño {a.SleepHoursLast:0.0}h deuda {a.SleepDebtH:0.0} · " +
                              $"estado {w.Status} alertas {string.Join(",", w.ActiveFlags)} · salud {a.HealthIndex:0} pronóstico60 {w.DaysToHealth60:0}");
                    Debug.Log($"[SpaceApps][Prueba] CO2 por módulo: {string.Join(" ", sim.Env.Co2.Select(c => c.ToString("0.0")))} · PM2.5 {sim.Env.MeanPm25:0.0} · ruido {sim.Env.NoiseDb:0} dB · T {sim.Env.TempC:0.0} °C");
                }
            }
            foreach (var h in sim.Crew)
                Debug.Log($"[SpaceApps][Prueba] {h.Name}: día {h.Day} · hueso {h.Bone:0.0}% · músculo {h.Muscle:0.0}% · VO2 {h.Vo2:0.0}% · " +
                          $"dosis {h.DoseMsv:0} mSv · SANS {h.Sans:0.00} · estrés {h.Stress:0} · salud {h.HealthIndex:0} · " +
                          $"FC {h.HeartRate:0} · PA {h.Sbp:0}/{h.Dbp:0} · SpO2 {h.SpO2:0.0} · CO2 medio {h.Co2Exposure:0.00} mmHg");
            Debug.Log($"[SpaceApps][Prueba] eventos solares: {sim.SpeCount} · entradas de bitácora: {sim.Log.Count} · CO2 módulos: " +
                      string.Join(" ", sim.Env.Co2.Select(c => c.ToString("0.0"))));

            // Construye cada escenario una vez para detectar errores
            var camGo = new GameObject("CamPrueba");
            var cam = camGo.AddComponent<Camera>();
            foreach (var key in ScenarioBuilder.Labels.Keys)
            {
                var root = ScenarioBuilder.Build(key, cam);
                int n = root.GetComponentsInChildren<Renderer>().Length;
                Debug.Log($"[SpaceApps][Prueba] escenario {key}: {n} objetos");
                Object.DestroyImmediate(root);
            }
            var body = new GameObject("CuerpoPrueba").AddComponent<AstronautBody>();
            Object.DestroyImmediate(body.gameObject);
            Object.DestroyImmediate(camGo);
            Debug.Log("[SpaceApps][Prueba] OK");
        }
    }

    /// <summary>Ajustes de importación para los modelos de Blender/FreeCAD en Resources/Models.</summary>
    public class SpaceAppsModelImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.bakeAxisConversion = assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
            // El traje del equipo trae esqueleto y animaciones (Idle, Walk…): se importan como Legacy
            bool rigged = System.IO.Path.GetFileName(assetPath).StartsWith("astronauta_traje", System.StringComparison.OrdinalIgnoreCase);
            mi.animationType = rigged ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
            mi.importAnimation = rigged;
            if (rigged) mi.animationWrapMode = WrapMode.Loop;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }

    /// <summary>Avatar de ASTRA (Rocketbox, Resources/Astra): esqueleto y gestos faciales, texturas a 1024.</summary>
    public class AstraAvatarImporter : AssetPostprocessor
    {
        static bool IsAstra(string p) => p.Replace('\\', '/').Contains("/Resources/Astra/");

        void OnPreprocessModel()
        {
            if (!IsAstra(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.bakeAxisConversion = true;
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = false;
            mi.importBlendShapes = true;
            mi.importBlendShapeNormals = ModelImporterNormals.Calculate;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.optimizeGameObjects = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        void OnPreprocessTexture()
        {
            if (!IsAstra(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = assetPath.Contains("opacity");
        }
    }
}
