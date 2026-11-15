using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>Mallas procedurales y ayudantes para armar piezas (eje Y arriba, metros).</summary>
    public static class MeshGen
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        static Mesh _cube, _sphere, _capsule;

        public static Mesh Cube => _cube ? _cube : (_cube = Builtin(PrimitiveType.Cube));
        public static Mesh Sphere => _sphere ? _sphere : (_sphere = Builtin(PrimitiveType.Sphere));
        public static Mesh Capsule => _capsule ? _capsule : (_capsule = Builtin(PrimitiveType.Capsule));

        static Mesh Builtin(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            return mesh;
        }

        /// <summary>Tronco de cono centrado en el origen (alto h sobre el eje Y). rTop = 0 hace un cono.</summary>
        public static Mesh Frustum(float rTop, float rBottom, float h, int seg = 40)
        {
            string key = $"fr{rTop:0.###}_{rBottom:0.###}_{h:0.###}_{seg}";
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;

            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float y0 = -h / 2f, y1 = h / 2f;
            float slope = rBottom - rTop;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                var normal = new Vector3(c * h, slope, s * h).normalized;
                v.Add(new Vector3(c * rBottom, y0, s * rBottom)); n.Add(normal); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(new Vector3(c * rTop, y1, s * rTop)); n.Add(normal); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                t.Add(b0); t.Add(t0); t.Add(t1);
                t.Add(b0); t.Add(t1); t.Add(b1);
            }
            Cap(v, n, uv, t, rTop, y1, seg, true);
            Cap(v, n, uv, t, rBottom, y0, seg, false);
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        static void Cap(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, float r, float y, int seg, bool top)
        {
            if (r <= 0.0001f) return;
            int center = v.Count;
            var normal = top ? Vector3.up : Vector3.down;
            v.Add(new Vector3(0, y, 0)); n.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(normal);
                uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = center + 1 + i, b = center + 2 + i;
                if (top) { t.Add(center); t.Add(b); t.Add(a); }
                else { t.Add(center); t.Add(a); t.Add(b); }
            }
        }

        public static Mesh Cylinder(float r, float h, int seg = 40) => Frustum(r, r, h, seg);

        /// <summary>Crea una pieza (MeshFilter + MeshRenderer) hija de parent.</summary>
        public static GameObject Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler = default, Vector3? scale = null, string name = "Pieza")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 size, Material mat, Vector3 pos, Vector3 euler = default, string name = "Caja")
            => Part(parent, Cube, mat, pos, euler, size, name);

        public static GameObject Cyl(Transform parent, float rTop, float rBottom, float h, Material mat, Vector3 pos, Vector3 euler = default, string name = "Cilindro", int seg = 40)
            => Part(parent, Frustum(rTop, rBottom, h, seg), mat, pos, euler, null, name);

        public static GameObject Ball(Transform parent, float radius, Material mat, Vector3 pos, string name = "Esfera")
            => Part(parent, Sphere, mat, pos, Vector3.zero, Vector3.one * radius * 2f, name);

        /// <summary>Ruido fractal 2D (fBm) basado en Perlin.</summary>
        public static float Fbm(float x, float y, int octaves = 5)
        {
            float sum = 0, amp = 0.5f, freq = 1f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq + 13.1f * i, y * freq + 7.7f * i);
                norm += amp; amp *= 0.5f; freq *= 2f;
            }
            return sum / norm;
        }

        /// <summary>Ruido continuo sobre una esfera (sin costura): promedia tres proyecciones.</summary>
        public static float SphereNoise(Vector3 d, float scale, int octaves = 5)
        {
            return (Fbm(d.x * scale + 10f, d.y * scale + 20f, octaves) +
                    Fbm(d.y * scale + 30f, d.z * scale + 40f, octaves) +
                    Fbm(d.z * scale + 50f, d.x * scale + 60f, octaves)) / 3f;
        }
    }
}
