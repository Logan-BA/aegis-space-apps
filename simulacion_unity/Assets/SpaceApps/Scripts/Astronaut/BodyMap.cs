using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Mapa corporal: el cuerpo anatómico del equipo (Models/anatomia, zonas Cabeza, Ojos, Torso,
    /// Brazos, Pelvis, Piernas) se dibuja en una RenderTexture que la interfaz muestra en el panel.
    /// Cada zona se colorea según la severidad del tripulante seleccionado.
    /// </summary>
    public class BodyMap : MonoBehaviour
    {
        public const int Layer = 30;
        public static float AnatomyYaw = 0f;   // para que el frente mire a la cámara del mapa

        public RenderTexture Texture { get; private set; }
        readonly Dictionary<string, List<Material>> _zones = new Dictionary<string, List<Material>>();
        readonly Dictionary<string, float> _sev = new Dictionary<string, float>();
        Transform _model;
        float _t;

        static readonly Color Skin = new Color(0.62f, 0.66f, 0.72f), Garment = new Color(0.16f, 0.19f, 0.24f);
        public static readonly string[] Zones = { "Cabeza", "Ojos", "Torso", "Brazos", "Pelvis", "Piernas" };

        public static BodyMap Create()
        {
            var go = new GameObject("MapaCorporal");
            go.transform.position = new Vector3(0f, -1000f, 0f);
            var map = go.AddComponent<BodyMap>();
            map.Build();
            return map;
        }

        void Build()
        {
            var model = ModelLibrary.TryLoad("anatomia");
            if (model == null) return;
            _model = model.transform;
            _model.SetParent(transform, false);
            _model.localRotation = Quaternion.Euler(0f, AnatomyYaw, 0f);
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var r in _model.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (var m in r.materials)
                    foreach (var z in Zones)
                        if (m.name.Contains(z))
                        {
                            if (!_zones.TryGetValue(z, out var l)) _zones[z] = l = new List<Material>();
                            l.Add(m);
                        }
            }

            Texture = new RenderTexture(420, 760, 16) { name = "MapaCorporalRT", antiAliasing = 4 };
            var camGo = new GameObject("CamaraMapa");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.92f, -4f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.98f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.075f, 0.09f, 0.115f, 1f);
            cam.cullingMask = 1 << Layer;
            cam.targetTexture = Texture;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
            cam.depth = -10;

            foreach (var (name, rot, intensity) in new[] { ("LuzMapaClave", new Vector3(25f, 35f, 0f), 1.1f), ("LuzMapaContra", new Vector3(10f, 200f, 0f), 0.6f) })
            {
                var lg = new GameObject(name);
                lg.transform.SetParent(transform, false);
                lg.transform.rotation = Quaternion.Euler(rot);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = intensity;
                l.color = new Color(0.9f, 0.95f, 1f);
                l.cullingMask = 1 << Layer;
                l.shadows = LightShadows.None;
            }
        }

        /// <summary>Severidad 0..1 por zona.</summary>
        public void SetSeverity(Dictionary<string, float> sev)
        {
            foreach (var z in Zones) _sev[z] = sev.TryGetValue(z, out var v) ? v : 0f;
        }

        public float Severity(string zone) => _sev.TryGetValue(zone, out var v) ? v : 0f;

        /// <summary>Convierte las severidades del modelo de salud a las zonas del mapa.</summary>
        public static Dictionary<string, float> FromHealth(AstronautHealth h) => new Dictionary<string, float>
        {
            ["Cabeza"] = Mathf.Max(h.Severity("head"), h.Severity("lungs") * 0.5f),
            ["Ojos"] = h.Severity("eyes"),
            ["Torso"] = Mathf.Max(h.Severity("chest"), Mathf.Max(h.Severity("lungs"), Mathf.Max(h.Severity("kidney"), h.Severity("spine")))),
            ["Brazos"] = h.Severity("arms"),
            ["Pelvis"] = h.Severity("pelvis"),
            ["Piernas"] = h.Severity("legs"),
        };

        public static Dictionary<string, float> FromBus(Dictionary<string, float> s)
        {
            float G(string k) => s.TryGetValue(k, out var v) ? v : 0f;
            return new Dictionary<string, float>
            {
                ["Cabeza"] = G("head"), ["Ojos"] = 0f, ["Torso"] = G("chest"), ["Brazos"] = 0f,
                ["Pelvis"] = G("pelvis"), ["Piernas"] = G("legs"),
            };
        }

        public static Color StatusOf(float sev) =>
            sev < 0.35f ? new Color(0.25f, 0.65f, 0.45f) : sev < 0.65f ? new Color(0.85f, 0.64f, 0.25f) : new Color(0.82f, 0.29f, 0.25f);

        void Update()
        {
            if (_model == null) return;
            _t += Time.deltaTime;
            _model.localRotation = Quaternion.Euler(0f, AnatomyYaw + 22f * Mathf.Sin(_t * 0.35f), 0f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(_t * 3f);
            foreach (var kv in _zones)
            {
                float s = Severity(kv.Key);
                var basis = kv.Key == "Pelvis" ? Garment : Skin;
                var col = s < 0.2f ? basis : Color.Lerp(basis, StatusOf(s), 0.3f + 0.6f * s);
                foreach (var m in kv.Value)
                {
                    Mats.SetColor(m, col);
                    Mats.SetEmission(m, s >= 0.35f ? StatusOf(s) * (0.25f + 0.35f * s) * pulse : Color.black);
                }
            }
        }
    }
}
