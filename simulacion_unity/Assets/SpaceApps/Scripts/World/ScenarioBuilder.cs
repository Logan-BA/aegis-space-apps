using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceApps
{
    /// <summary>Gira lentamente un objeto (planetas).</summary>
    public class Spin : MonoBehaviour
    {
        public float DegreesPerSecond = 0.6f;
        void Update() => transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }

    /// <summary>
    /// Arma cada escenario 3D alrededor del astronauta (que está en el origen).
    /// Escenas: earth, leo, deep_space_moon, deep_space_mars, moon_surface, mars_surface, ship_interior.
    /// </summary>
    public static class ScenarioBuilder
    {
        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["earth"] = "Tierra - centro de entrenamiento",
            ["leo"] = "Órbita baja - estación espacial",
            ["deep_space_moon"] = "Espacio profundo - rumbo a la Luna",
            ["deep_space_mars"] = "Espacio profundo - rumbo a Marte",
            ["moon_surface"] = "Superficie lunar",
            ["mars_surface"] = "Superficie de Marte",
            ["ship_interior"] = "Interior de la nave - pasillo principal",
        };

        static readonly Dictionary<string, Texture2D> TexCache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Mesh> TerrainCache = new Dictionary<string, Mesh>();

        public static GameObject Build(string key, Camera cam)
        {
            var root = new GameObject("Escenario_" + key).transform;
            RenderSettings.fog = false;
            switch (key)
            {
                case "earth": EarthGround(root, cam); break;
                case "deep_space_moon": DeepSpace(root, cam, false); break;
                case "deep_space_mars": DeepSpace(root, cam, true); break;
                case "moon_surface": MoonSurface(root, cam); break;
                case "mars_surface": MarsSurface(root, cam); break;
                case "ship_interior": InteriorBuilder.Build(root, cam); break;
                default: Leo(root, cam); break;
            }
            return root.gameObject;
        }

        // ------------------------------------------------------------ escenarios

        static void EarthGround(Transform root, Camera cam)
        {
            Sky(cam, new Color(0.45f, 0.6f, 0.85f), 1f, new Color(0.35f, 0.33f, 0.3f), 1.0f);
            Sun(root, new Vector3(40, -35, 0), 1.2f, new Color(1f, 0.96f, 0.9f));
            Terrain(root, "tierra", new Color(0.2f, 0.33f, 0.14f), new Color(0.45f, 0.47f, 0.3f), 3f, 0.012f, 11);
            var rocket = ModelLibrary.TryLoad("cohete_lanzador") ?? ShipBuilder.Launcher();
            rocket.transform.SetParent(root, false);
            rocket.transform.localPosition = new Vector3(45, 0, 120);
            MeshGen.Box(root, new Vector3(30, 1, 30), Mats.Lit(new Color(0.5f, 0.5f, 0.5f)), new Vector3(45, -0.45f, 120), default, "Plataforma");
            MeshGen.Box(root, new Vector3(4, 110, 4), Mats.Lit(new Color(0.35f, 0.35f, 0.38f), 0.7f, 0.5f), new Vector3(57, 55, 120), default, "TorreServicio");
            MeshGen.Box(root, new Vector3(24, 8, 14), Mats.Lit(new Color(0.85f, 0.85f, 0.82f)), new Vector3(-22, 4, 26), new Vector3(0, -20, 0), "CentroEntrenamiento");
            MeshGen.Box(root, new Vector3(10, 0.05f, 10), Mats.Lit(new Color(0.3f, 0.3f, 0.33f)), new Vector3(0, 0.02f, 0), default, "Pista");
        }

        static void Leo(Transform root, Camera cam)
        {
            Backdrop(cam, root, 0.12f);
            Sun(root, new Vector3(25, -60, 0), 1.5f, Color.white);
            var station = ModelLibrary.TryLoad("estacion", 100f) ?? ShipBuilder.Station();
            station.transform.SetParent(root, false);
            station.transform.localPosition = new Vector3(5, -2, 65);
            station.transform.localEulerAngles = new Vector3(-10, -25, 0);
            Planet(root, "Tierra", 3000f, new Vector3(0, -3400, 800), EarthGradient(), 2.2f, 3);
        }

        static void DeepSpace(Transform root, Camera cam, bool toMars)
        {
            Backdrop(cam, root, 0.08f);
            Sun(root, new Vector3(15, 70, 0), 1.4f, Color.white);
            var ship = ModelLibrary.TryLoad("nave_transito") ?? ShipBuilder.TransitShip();
            var holder = new GameObject("NaveHorizontal").transform;
            holder.SetParent(root, false);
            ship.transform.SetParent(holder, false);
            ship.transform.localPosition = new Vector3(0, -20, 0);   // centra la nave sobre su eje
            holder.localEulerAngles = new Vector3(0, -20, 90);
            holder.localPosition = new Vector3(0, 0, 40);
            if (toMars)
            {
                Planet(root, "Marte", 140f, new Vector3(-500, 120, 1700), MarsGradient(), 3f, 7);
                Planet(root, "TierraLejana", 6f, new Vector3(900, -150, -2500), EarthGradient(), 2.2f, 3);
            }
            else
            {
                Planet(root, "Luna", 120f, new Vector3(450, 80, 1500), MoonGradient(), 4f, 5);
                Planet(root, "Tierra", 90f, new Vector3(-900, -200, -2200), EarthGradient(), 2.2f, 3);
            }
        }

        static void MoonSurface(Transform root, Camera cam)
        {
            Backdrop(cam, root, 0.1f);
            Sun(root, new Vector3(18, -50, 0), 1.7f, Color.white);
            Terrain(root, "luna", new Color(0.3f, 0.3f, 0.31f), new Color(0.68f, 0.68f, 0.66f), 7f, 0.015f, 21);
            Rocks(root, new Color(0.5f, 0.5f, 0.5f), 60, 42);
            var lander = ModelLibrary.TryLoad("modulo_lunar") ?? ShipBuilder.Lander();
            lander.transform.SetParent(root, false);
            lander.transform.localPosition = new Vector3(12, 0, 22);
            Planet(root, "TierraEnCielo", 70f, new Vector3(-300, 420, 1400), EarthGradient(), 2.2f, 3);
        }

        static void MarsSurface(Transform root, Camera cam)
        {
            Sky(cam, new Color(0.85f, 0.55f, 0.35f), 0.55f, new Color(0.45f, 0.25f, 0.15f), 0.75f);
            Sun(root, new Vector3(30, -40, 0), 0.95f, new Color(1f, 0.88f, 0.75f));
            Terrain(root, "marte", new Color(0.45f, 0.2f, 0.1f), new Color(0.78f, 0.46f, 0.26f), 9f, 0.012f, 33);
            Rocks(root, new Color(0.42f, 0.22f, 0.14f), 80, 7);
            var hab = ModelLibrary.TryLoad("modulo_marte") ?? ShipBuilder.MarsHabitat();
            hab.transform.SetParent(root, false);
            hab.transform.localPosition = new Vector3(-16, 0, 18);
            hab.transform.localEulerAngles = new Vector3(0, -30, 0);
            var lander = ShipBuilder.Lander();
            lander.transform.SetParent(root, false);
            lander.transform.localPosition = new Vector3(14, 0, 22);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.75f, 0.52f, 0.36f);
            RenderSettings.fogDensity = 0.004f;
        }

        // ------------------------------------------------------------ piezas comunes

        /// <summary>Cielo con atmósfera (Tierra, Marte).</summary>
        static void Sky(Camera cam, Color tint, float thickness, Color ground, float ambient)
        {
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", tint);
            sky.SetFloat("_AtmosphereThickness", thickness);
            sky.SetColor("_GroundColor", ground);
            sky.SetFloat("_Exposure", 1.2f);
            RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(tint, Color.white, 0.5f) * 0.5f * ambient;
        }

        /// <summary>Espacio: fondo negro + campo de estrellas.</summary>
        public static void Backdrop(Camera cam, Transform root, float ambient)
        {
            RenderSettings.skybox = null;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.75f) * ambient;
            StarField.Create(root);
        }

        public static Light Sun(Transform root, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject("Sol");
            go.transform.SetParent(root, false);
            go.transform.localEulerAngles = new Vector3(Mathf.Abs(euler.x), euler.y, euler.z);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.Soft;
            l.cullingMask = ~(1 << BodyMap.Layer);   // no iluminar el mapa corporal
            RenderSettings.sun = l;
            return l;
        }

        static void Planet(Transform root, string name, float radius, Vector3 pos, Gradient grad, float scale, int seed)
        {
            string key = name + seed;
            if (!TexCache.TryGetValue(key, out var tex) || tex == null)
            {
                const int w = 512, h = 256;
                tex = new Texture2D(w, h, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, name = key };
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    float lat = (y / (float)(h - 1) - 0.5f) * Mathf.PI;
                    for (int x = 0; x < w; x++)
                    {
                        float lon = x / (float)w * Mathf.PI * 2f;
                        var d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                        float n = MeshGen.SphereNoise(d + Vector3.one * seed, scale);
                        n = Mathf.Clamp01((n - 0.3f) / 0.4f);
                        var c = grad.Evaluate(n);
                        if (name.StartsWith("Tierra") && Mathf.Abs(d.y) > 0.88f) c = Color.Lerp(c, Color.white, 0.85f); // casquetes
                        px[y * w + x] = c;
                    }
                }
                tex.SetPixels(px); tex.Apply();
                TexCache[key] = tex;
            }
            var mat = Mats.LitUnique(Color.white, 0f, 0.15f);
            Mats.SetTexture(mat, tex);
            var go = MeshGen.Ball(root, radius, mat, pos, name);
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<Spin>();
        }

        static void Terrain(Transform root, string key, Color low, Color high, float height, float freq, int seed)
        {
            const int n = 161; const float size = 400f;
            if (!TerrainCache.TryGetValue(key, out var mesh) || mesh == null)
            {
                var v = new Vector3[n * n]; var uv = new Vector2[n * n]; var t = new int[(n - 1) * (n - 1) * 6];
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        float wx = (x / (float)(n - 1) - 0.5f) * size, wz = (z / (float)(n - 1) - 0.5f) * size;
                        float hgt = MeshGen.Fbm(wx * freq + seed, wz * freq + seed * 2f);
                        float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 35f, new Vector2(wx, wz).magnitude));
                        v[z * n + x] = new Vector3(wx, (hgt - 0.35f) * height * mask, wz);
                        uv[z * n + x] = new Vector2(x / (float)(n - 1), z / (float)(n - 1));
                    }
                int k = 0;
                for (int z = 0; z < n - 1; z++)
                    for (int x = 0; x < n - 1; x++)
                    {
                        int i = z * n + x;
                        t[k++] = i; t[k++] = i + n; t[k++] = i + n + 1;
                        t[k++] = i; t[k++] = i + n + 1; t[k++] = i + 1;
                    }
                mesh = new Mesh { name = "Terreno_" + key, vertices = v, uv = uv, triangles = t };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                TerrainCache[key] = mesh;
            }
            if (!TexCache.TryGetValue("terr" + key, out var tex) || tex == null)
            {
                const int w = 256;
                tex = new Texture2D(w, w, TextureFormat.RGB24, true) { name = "terr" + key };
                var px = new Color[w * w];
                for (int y = 0; y < w; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float f = MeshGen.Fbm(x * 0.05f + seed, y * 0.05f, 4) * 0.7f + MeshGen.Fbm(x * 0.4f, y * 0.4f + seed, 2) * 0.3f;
                        px[y * w + x] = Color.Lerp(low, high, Mathf.Clamp01((f - 0.3f) / 0.4f));
                    }
                tex.SetPixels(px); tex.Apply();
                TexCache["terr" + key] = tex;
            }
            var mat = Mats.LitUnique(Color.white, 0f, 0.05f);
            Mats.SetTexture(mat, tex);
            mat.mainTextureScale = new Vector2(8f, 8f);
            var go = MeshGen.Part(root, mesh, mat, Vector3.zero, default, null, "Terreno");
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Rocks(Transform root, Color color, int count, int seed)
        {
            var rng = new System.Random(seed);
            var mat = Mats.Lit(color, 0f, 0.1f);
            var parent = new GameObject("Rocas").transform;
            parent.SetParent(root, false);
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = 3f + (float)rng.NextDouble() * 10f;
                float r = 0.08f + (float)rng.NextDouble() * 0.42f;
                var go = MeshGen.Ball(parent, r, mat, new Vector3(Mathf.Cos(a) * d, r * 0.25f, Mathf.Sin(a) * d), "Roca");
                go.transform.localScale = new Vector3(r * 2f, r * 1.2f, r * 1.7f);
                go.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f);
            }
        }

        static Gradient Grad(params (float t, Color c)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) ck[i] = new GradientColorKey(keys[i].c, keys[i].t);
            g.SetKeys(ck, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return g;
        }

        static Gradient EarthGradient() => Grad(
            (0f, new Color(0.02f, 0.10f, 0.35f)), (0.48f, new Color(0.06f, 0.25f, 0.55f)), (0.52f, new Color(0.8f, 0.75f, 0.55f)),
            (0.62f, new Color(0.18f, 0.42f, 0.15f)), (0.8f, new Color(0.4f, 0.35f, 0.22f)), (1f, new Color(0.95f, 0.95f, 0.97f)));
        static Gradient MarsGradient() => Grad(
            (0f, new Color(0.35f, 0.12f, 0.06f)), (0.4f, new Color(0.7f, 0.3f, 0.13f)), (0.75f, new Color(0.85f, 0.5f, 0.28f)), (1f, new Color(0.95f, 0.85f, 0.75f)));
        static Gradient MoonGradient() => Grad(
            (0f, new Color(0.25f, 0.25f, 0.26f)), (0.5f, new Color(0.55f, 0.55f, 0.54f)), (1f, new Color(0.85f, 0.85f, 0.83f)));
    }
}
