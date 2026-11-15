using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceApps
{
    /// <summary>
    /// Pantallas de sensores de CO2 a lo largo del pasillo (una por módulo de la malla 1D).
    /// SpaceAppsSimulation las colorea cada hora: verde &lt; 3 mmHg, amarillo &lt; 5, rojo ≥ 5.
    /// </summary>
    public class CabinBeacons : MonoBehaviour
    {
        public Material[] Materials = new Material[CabinEnvironment.Cells];

        public void Show(CabinEnvironment env)
        {
            for (int i = 0; i < Materials.Length; i++)
            {
                if (Materials[i] == null) continue;
                float c = env.Co2[i];
                Color col = c < 3f ? Color.Lerp(new Color(0.2f, 1f, 0.4f), new Color(1f, 0.85f, 0.1f), Mathf.InverseLerp(1f, 3f, c))
                                   : Color.Lerp(new Color(1f, 0.85f, 0.1f), new Color(1f, 0.15f, 0.1f), Mathf.InverseLerp(3f, 5f, c));
                Mats.SetColor(Materials[i], col);
                Mats.SetEmission(Materials[i], col * 2.5f);
            }
        }
    }

    /// <summary>
    /// Interior de la nave inspirado en las imágenes de referencia del equipo: pasillo con marcos
    /// hexagonales, piso de rejilla, paredes de tuberías, pasarelas laterales con barandas, tiras de
    /// luz y compuerta al fondo. Si existe Resources/Models/interior_nave (FBX de Blender) se usa ese.
    /// </summary>
    public static class InteriorBuilder
    {
        const float Length = 48f, FrameStep = 8f, StartZ = -6f;
        static readonly Vector2[] FramePts =
        {
            new Vector2(-1.8f, 0f), new Vector2(1.8f, 0f), new Vector2(3f, 1.2f), new Vector2(3f, 3.6f),
            new Vector2(1.8f, 4.8f), new Vector2(-1.8f, 4.8f), new Vector2(-3f, 3.6f), new Vector2(-3f, 1.2f),
        };
        static Texture2D _grid;

        public static void Build(Transform root, Camera cam)
        {
            RenderSettings.skybox = null;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.1f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.035f);
            RenderSettings.fogDensity = 0.03f;
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 10);

            var model = ModelLibrary.TryLoad("interior_nave", 0f, false);
            if (model != null)
            {
                // El FBX de Blender queda centrado en el origen: el pasillo va de -Length/2 a +Length/2
                model.transform.SetParent(root, false);
                Lights(root, -Length / 2f);
                Beacons(root, -Length / 2f);
                return;
            }

            var metal = Mats.Lit(new Color(0.22f, 0.23f, 0.25f), 0.8f, 0.55f);
            var frame = Mats.Lit(new Color(0.32f, 0.33f, 0.35f), 0.85f, 0.65f);
            var pipe = Mats.Lit(new Color(0.18f, 0.19f, 0.21f), 0.9f, 0.65f);
            var pipeLight = Mats.Lit(new Color(0.45f, 0.46f, 0.48f), 0.6f, 0.5f);
            var grate = Mats.LitUnique(new Color(0.55f, 0.56f, 0.6f), 0.8f, 0.5f);
            Mats.SetTexture(grate, GridTexture());
            grate.mainTextureScale = new Vector2(2f, 24f);
            var strip = Mats.Emissive(new Color(0.85f, 0.92f, 1f), 4f);
            var screen = Mats.Emissive(new Color(0.15f, 0.5f, 1f), 2.5f);

            float zEnd = StartZ + Length, mid = (StartZ + zEnd) * 0.5f;
            MeshGen.Box(root, new Vector3(3.6f, 0.12f, Length), grate, new Vector3(0, -0.06f, mid), default, "Pasarela");
            MeshGen.Box(root, new Vector3(0.12f, 0.08f, Length), frame, new Vector3(0, 0.01f, mid), default, "Riel");
            MeshGen.Box(root, new Vector3(14f, 0.1f, Length), metal, new Vector3(0, -0.8f, mid), default, "PisoInferior");
            MeshGen.Box(root, new Vector3(14f, 0.3f, Length), Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.6f, 0.4f), new Vector3(0, 7.5f, mid), default, "Techo");
            foreach (float s in new[] { -1f, 1f })
                MeshGen.Box(root, new Vector3(0.2f, 8.5f, Length), metal, new Vector3(s * 7f, 3.4f, mid), default, "Pared");

            for (float z = StartZ + 2f; z < zEnd - 1f; z += FrameStep) Frame(root, z, frame, strip);

            var rng = new System.Random(1977);
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i < 14; i++)
                {
                    float y = 0.3f + i * 0.5f + (float)(rng.NextDouble() * 0.2 - 0.1);
                    float x = s * (4.2f + (float)rng.NextDouble() * 1.6f);
                    float r = 0.06f + (float)rng.NextDouble() * 0.12f;
                    MeshGen.Cyl(root, r, r, Length, rng.NextDouble() < 0.2 ? pipeLight : pipe, new Vector3(x, y, mid), new Vector3(90, 0, 0), "Tubo", 12);
                }
                MeshGen.Cyl(root, 0.55f, 0.55f, Length, pipeLight, new Vector3(s * 5.6f, 6.4f, mid), new Vector3(90, 0, 0), "Tanque", 20);
                for (float zz = StartZ + 5f; zz < zEnd - 2f; zz += 2.5f + (float)rng.NextDouble() * 2.5f)
                    MeshGen.Cyl(root, 0.08f, 0.08f, 6.5f, pipe, new Vector3(s * (4f + (float)rng.NextDouble()), 3.25f, zz), default, "Bajante", 10);
                MeshGen.Box(root, new Vector3(1.6f, 0.08f, Length), grate, new Vector3(s * 3.9f, 2.6f, mid), default, "PasarelaLateral");
                MeshGen.Cyl(root, 0.03f, 0.03f, Length, frame, new Vector3(s * 3.15f, 3.6f, mid), new Vector3(90, 0, 0), "Baranda", 8);
                MeshGen.Cyl(root, 0.02f, 0.02f, Length, frame, new Vector3(s * 3.15f, 3.1f, mid), new Vector3(90, 0, 0), "BarandaMedia", 8);
                for (float pz = StartZ + 1f; pz < zEnd; pz += 2f)
                    MeshGen.Cyl(root, 0.025f, 0.025f, 1f, frame, new Vector3(s * 3.15f, 3.1f, pz), default, "Poste", 8);
                for (int k = 0; k < 3; k++)
                    MeshGen.Box(root, new Vector3(0.05f, 0.45f, 0.35f), screen, new Vector3(s * 3.35f, 1.5f, StartZ + 10f + k * 14f), default, "Pantalla");
            }
            Bulkhead(root, zEnd, frame, strip);
            Lights(root, StartZ);
            Beacons(root, StartZ);
        }

        static void Frame(Transform root, float z, Material frame, Material strip)
        {
            int n = FramePts.Length;
            for (int i = 1; i < n; i++)   // i = 0 sería la viga sobre el piso
            {
                Vector2 a = FramePts[i], b = FramePts[(i + 1) % n];
                Vector2 m = (a + b) * 0.5f, d = b - a;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                MeshGen.Box(root, new Vector3(d.magnitude + 0.35f, 0.35f, 0.5f), frame, new Vector3(m.x, m.y, z), new Vector3(0, 0, ang), "Marco");
                if (i >= 3 && i <= 5)
                {
                    Vector2 inward = new Vector2(-d.y, d.x).normalized;
                    Vector2 p = m + inward * 0.19f;
                    MeshGen.Box(root, new Vector3(d.magnitude * 0.45f, 0.04f, 0.08f), strip, new Vector3(p.x, p.y, z - 0.26f), new Vector3(0, 0, ang), "TiraLuz");
                }
            }
        }

        static void Bulkhead(Transform root, float z, Material frame, Material strip)
        {
            var door = Mats.Lit(new Color(0.55f, 0.56f, 0.58f), 0.85f, 0.7f);
            var inset = Mats.Lit(new Color(0.35f, 0.36f, 0.38f), 0.85f, 0.6f);
            MeshGen.Box(root, new Vector3(6.2f, 5f, 0.3f), door, new Vector3(0, 2.4f, z), default, "Compuerta");
            MeshGen.Box(root, new Vector3(3.2f, 0.8f, 0.1f), inset, new Vector3(0, 3.4f, z - 0.18f), default, "PanelSuperior");
            MeshGen.Box(root, new Vector3(3.6f, 0.9f, 0.1f), inset, new Vector3(0, 1.3f, z - 0.18f), default, "PanelInferior");
            foreach (float s in new[] { -1f, 1f })
                MeshGen.Box(root, new Vector3(0.9f, 2.6f, 0.1f), inset, new Vector3(s * 2.2f, 2.4f, z - 0.18f), default, "PanelLateral");
            Frame(root, z - 0.6f, frame, strip);
        }

        static void Lights(Transform root, float z0)
        {
            for (float z = z0 + 2f; z < z0 + Length; z += 8f)
                PointLight(root, new Vector3(0, 4.3f, z), new Color(0.85f, 0.9f, 1f), 1.6f, 10f, z < z0 + 20f);
            var spot = new GameObject("FocoCompuerta");
            spot.transform.SetParent(root, false);
            spot.transform.localPosition = new Vector3(0, 5.5f, z0 + Length - 5f);
            spot.transform.localEulerAngles = new Vector3(60, 0, 0);
            var l = spot.AddComponent<Light>();
            l.type = LightType.Spot; l.intensity = 5f; l.range = 14f; l.spotAngle = 50f;
            foreach (float s in new[] { -1f, 1f })
                PointLight(root, new Vector3(s * 4f, 1f, z0 + 20f), new Color(0.2f, 0.45f, 1f), 0.8f, 14f, false);
        }

        static void PointLight(Transform root, Vector3 pos, Color c, float intensity, float range, bool shadows)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        static void Beacons(Transform root, float z0)
        {
            var b = root.gameObject.AddComponent<CabinBeacons>();
            float spacing = Length / CabinEnvironment.Cells;
            for (int i = 0; i < CabinEnvironment.Cells; i++)
            {
                var m = Mats.LitUnique(Color.green, 0f, 0.8f);
                b.Materials[i] = m;
                float side = i % 2 == 0 ? -1f : 1f;
                MeshGen.Box(root, new Vector3(0.06f, 0.22f, 0.5f), m, new Vector3(side * 2.95f, 1.9f, z0 + spacing * (i + 0.5f)), default, "SensorCO2_" + CabinEnvironment.CellNames[i]);
            }
        }

        static Texture2D GridTexture()
        {
            if (_grid) return _grid;
            _grid = new Texture2D(64, 64, TextureFormat.RGB24, true) { name = "Rejilla", wrapMode = TextureWrapMode.Repeat };
            for (int i = 0; i < 64; i++)
                for (int j = 0; j < 64; j++)
                {
                    bool border = i % 16 < 2 || j % 16 < 2, fine = i % 4 == 0 || j % 4 == 0;
                    _grid.SetPixel(i, j, border ? new Color(0.55f, 0.56f, 0.58f) : fine ? new Color(0.38f, 0.39f, 0.41f) : new Color(0.06f, 0.06f, 0.07f));
                }
            _grid.Apply();
            return _grid;
        }
    }
}
