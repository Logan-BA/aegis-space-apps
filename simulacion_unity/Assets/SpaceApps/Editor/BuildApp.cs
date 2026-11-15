using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpaceApps.EditorTools
{
    /// <summary>
    /// Compila la aplicación de Windows en ../App/SaludAstronauta/SaludAstronauta.exe
    /// Menú Space Apps → Compilar app de Windows, o en modo batch:
    ///   Unity -batchmode -projectPath ... -executeMethod SpaceApps.EditorTools.BuildApp.Windows -quit
    /// </summary>
    public static class BuildApp
    {
        public static string OutputDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "App", "SaludAstronauta"));

        [MenuItem("Space Apps/Compilar app de Windows", priority = 40)]
        public static void Windows()
        {
            if (!File.Exists("Assets/Scenes/SpaceApps.unity")) SpaceAppsSetup.CreateScene();
            EnsureShaderVariants();

            PlayerSettings.companyName = "Space Apps";
            PlayerSettings.productName = "Salud del Astronauta";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            Directory.CreateDirectory(OutputDir);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SpaceApps.unity" },
                locationPathName = Path.Combine(OutputDir, "SaludAstronauta.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[SpaceApps][Build] {s.result} · {s.totalErrors} errores · {s.totalSize / (1024 * 1024)} MB · {s.outputPath}");
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// Los materiales se crean por código: guardar en Resources un material por variante usada
        /// (emisión, transparencia) evita que la build descarte esas variantes del shader Standard.
        /// </summary>
        static void EnsureShaderVariants()
        {
            const string dir = "Assets/SpaceApps/Resources/ShaderRefs";
            Directory.CreateDirectory(dir);
            var std = Shader.Find("Standard");
            void Save(string name, System.Action<Material> setup)
            {
                string path = $"{dir}/{name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) AssetDatabase.DeleteAsset(path);
                var m = new Material(std);
                setup(m);
                AssetDatabase.CreateAsset(m, path);
            }
            Save("Standard_Opaco", m => { });
            Save("Standard_Emision", m => { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); });
            Save("Standard_Textura", m => m.SetTexture("_MainTex", Texture2D.whiteTexture));
            Save("Standard_Transparente", m =>
            {
                m.SetFloat("_Mode", 2f);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.renderQueue = 3000;
            });
            foreach (var name in new[] { "Unlit/Color", "Skybox/Procedural", "SpaceApps/Holograma", "SpaceApps/HoloAvatar" })
            {
                var sh = Shader.Find(name);
                string path = $"{dir}/{name.Replace('/', '_')}.mat";
                if (sh != null && AssetDatabase.LoadAssetAtPath<Material>(path) == null) AssetDatabase.CreateAsset(new Material(sh), path);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
