using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>Esfera de estrellas (quads que miran al centro) que sigue a la cámara.</summary>
    public class StarField : MonoBehaviour
    {
        public static StarField Create(Transform parent, int count = 4000, float radius = 18000f, int seed = 3)
        {
            var go = new GameObject("Estrellas");
            go.transform.SetParent(parent, false);
            var sf = go.AddComponent<StarField>();
            var rng = new System.Random(seed);
            var v = new List<Vector3>(); var t = new List<int>(); var c = new List<Color>();
            for (int i = 0; i < count; i++)
            {
                // dirección uniforme sobre la esfera, con más densidad en la banda galáctica
                Vector3 d;
                do { d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1); }
                while (d.sqrMagnitude > 1f || d.sqrMagnitude < 0.01f);
                d.Normalize();
                if (i % 3 == 0) { d.y *= 0.25f; d.Normalize(); }
                float size = radius * (0.0006f + 0.0016f * Mathf.Pow((float)rng.NextDouble(), 6f));
                Vector3 center = d * radius;
                Vector3 right = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized * size;
                Vector3 up = Vector3.Cross(right, d).normalized * size;
                int b = v.Count;
                v.Add(center - right - up); v.Add(center - right + up); v.Add(center + right + up); v.Add(center + right - up);
                // doble cara: visible desde el centro sin depender del orden de vértices
                t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
                t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
            var mesh = new Mesh { name = "Estrellas", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetTriangles(t, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.2f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Unlit(new Color(0.92f, 0.94f, 1f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return sf;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam) transform.position = cam.transform.position;
        }
    }
}
