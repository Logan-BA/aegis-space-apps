using UnityEditor;
using UnityEngine;

namespace SpaceApps.EditorTools
{
    /// <summary>Diagnóstico de los modelos del equipo (animaciones y tamaños). Menú Space Apps → Revisar modelos.</summary>
    public static class ModelCheck
    {
        [MenuItem("Space Apps/Revisar modelos del equipo", priority = 50)]
        public static void Run()
        {
            foreach (var name in new[] { "astronauta_traje", "anatomia", "modulo_marte" })
            {
                var prefab = Resources.Load<GameObject>("Models/" + name);
                if (prefab == null) { Debug.Log($"[SpaceApps][Modelo] {name}: NO encontrado"); continue; }
                var path = AssetDatabase.GetAssetPath(prefab);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                var anim = prefab.GetComponentInChildren<Animation>();
                var clips = AssetDatabase.LoadAllAssetsAtPath(path);
                int nClips = 0; string names = "";
                foreach (var o in clips) if (o is AnimationClip c && !c.name.StartsWith("__preview")) { nClips++; names += $"{c.name}(legacy={c.legacy}) "; }
                Debug.Log($"[SpaceApps][Modelo] {name}: animType={imp?.animationType} importAnim={imp?.importAnimation} " +
                          $"Animation={(anim != null)} clips={nClips} · {names}");
                if (anim != null) foreach (AnimationState st in anim) Debug.Log($"[SpaceApps][Modelo]   estado: {st.name} {st.length:0.00}s");
            }
        }
    }
}
