using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Cuerpo humano procedural con zonas que muestran los síntomas:
    ///  - verde → amarillo → rojo según la severidad de cada zona
    ///  - piernas y brazos se adelgazan con la atrofia ("piernas de pájaro")
    ///  - la cabeza se hincha con el desplazamiento de fluidos
    ///  - halo rojo que crece con la dosis de radiación
    ///  - LED RGB en el pecho (actuador): verde / amarillo / rojo según el sistema de alertas
    ///  - en microgravedad adopta la postura neutra y flota
    /// </summary>
    public class AstronautBody : MonoBehaviour
    {
        static readonly Color Skin = new Color(0.80f, 0.84f, 0.90f);
        static readonly Color BoneColor = new Color(0.95f, 0.93f, 0.85f);
        static readonly string[] Regions = { "head", "eyes", "chest", "lungs", "spine", "pelvis", "arms", "legs", "kidney" };

        class Part { public Renderer R; public Material M; public Color Base; public string Region; }

        readonly List<Part> _parts = new List<Part>();
        readonly Dictionary<string, float> _sev = new Dictionary<string, float>();
        readonly List<Transform> _legMeshes = new List<Transform>(), _armMeshes = new List<Transform>();
        readonly Transform[] _shoulders = new Transform[2], _elbows = new Transform[2], _hips = new Transform[2], _knees = new Transform[2];
        Transform _body, _head, _heart;
        readonly List<Transform> _lungs = new List<Transform>();
        float _heartPulse, _breath;

        /// <summary>Latido y respiración (del monitor de signos vitales): el corazón late y los pulmones se inflan.</summary>
        public void SetRhythm(float heartPulse01, float breath01) { _heartPulse = heartPulse01; _breath = breath01; }
        Material _auraMat, _ledMat;
        Renderer _aura;
        Light _ledLight;
        bool _floating;
        float _float, _radiation, _t;
        Color _led = Color.green;
        float _hapticTimer;

        void Awake() => Build();

        void Build()
        {
            _body = new GameObject("Cuerpo").transform;
            _body.SetParent(transform, false);

            Add(_body, MeshGen.Cube, new Vector3(0, 0.98f, 0), new Vector3(0.32f, 0.16f, 0.18f), "pelvis", BoneColor);
            Add(_body, MeshGen.Capsule, new Vector3(0, 1.32f, 0), new Vector3(0.34f, 0.31f, 0.25f), "chest", Skin);
            Add(_body, MeshGen.Cylinder(0.03f, 0.62f, 12), new Vector3(0, 1.30f, -0.13f), Vector3.one, "spine", BoneColor);
            _heart = Add(_body, MeshGen.Sphere, new Vector3(0.05f, 1.42f, 0.12f), Vector3.one * 0.1f, "chest", new Color(0.85f, 0.35f, 0.35f)); // corazón
            foreach (float s in new[] { -1f, 1f })
            {
                _lungs.Add(Add(_body, MeshGen.Sphere, new Vector3(s * 0.085f, 1.34f, 0.095f), new Vector3(0.11f, 0.19f, 0.07f), "lungs", new Color(0.9f, 0.6f, 0.65f)));
                Add(_body, MeshGen.Sphere, new Vector3(s * 0.07f, 1.13f, -0.115f), new Vector3(0.06f, 0.09f, 0.05f), "kidney", new Color(0.7f, 0.35f, 0.3f));
            }
            Add(_body, MeshGen.Cylinder(0.06f, 0.12f, 16), new Vector3(0, 1.66f, 0), Vector3.one, "head", Skin);
            _head = Add(_body, MeshGen.Sphere, new Vector3(0, 1.80f, 0), Vector3.one * 0.24f, "head", Skin);
            foreach (float s in new[] { -1f, 1f })
                Add(_body, MeshGen.Sphere, new Vector3(s * 0.045f, 1.82f, 0.105f), Vector3.one * 0.044f, "eyes", new Color(0.15f, 0.2f, 0.3f));

            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                _shoulders[i] = Pivot(_body, new Vector3(s * 0.23f, 1.56f, 0), "Hombro");
                _armMeshes.Add(Add(_shoulders[i], MeshGen.Capsule, new Vector3(0, -0.16f, 0), new Vector3(0.11f, 0.16f, 0.11f), "arms", Skin));
                _elbows[i] = Pivot(_shoulders[i], new Vector3(0, -0.32f, 0), "Codo");
                _armMeshes.Add(Add(_elbows[i], MeshGen.Capsule, new Vector3(0, -0.14f, 0), new Vector3(0.09f, 0.14f, 0.09f), "arms", Skin));
                Add(_elbows[i], MeshGen.Sphere, new Vector3(0, -0.31f, 0), Vector3.one * 0.1f, "arms", Skin);

                _hips[i] = Pivot(_body, new Vector3(s * 0.10f, 0.92f, 0), "Cadera");
                _legMeshes.Add(Add(_hips[i], MeshGen.Capsule, new Vector3(0, -0.23f, 0), new Vector3(0.15f, 0.23f, 0.15f), "legs", Skin));
                _knees[i] = Pivot(_hips[i], new Vector3(0, -0.46f, 0), "Rodilla");
                _legMeshes.Add(Add(_knees[i], MeshGen.Capsule, new Vector3(0, -0.21f, 0), new Vector3(0.12f, 0.21f, 0.12f), "legs", Skin));
                Add(_knees[i], MeshGen.Cube, new Vector3(0, -0.44f, 0.05f), new Vector3(0.10f, 0.06f, 0.24f), "legs", Skin);
            }

            // Halo de radiación
            _auraMat = Mats.Transparent(new Color(1f, 0.15f, 0.35f, 0f));
            var aura = MeshGen.Part(_body, MeshGen.Capsule, _auraMat, new Vector3(0, 1f, 0), default, new Vector3(0.9f, 1.05f, 0.9f), "HaloRadiacion");
            _aura = aura.GetComponent<Renderer>();
            _aura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // LED RGB del sistema de alertas (actuador)
            _ledMat = Mats.LitUnique(Color.green, 0f, 0.9f);
            MeshGen.Part(_body, MeshGen.Sphere, _ledMat, new Vector3(-0.09f, 1.5f, 0.17f), default, Vector3.one * 0.035f, "LED_Alerta");
            var lg = new GameObject("LuzLED");
            lg.transform.SetParent(_body, false);
            lg.transform.localPosition = new Vector3(-0.09f, 1.5f, 0.3f);
            _ledLight = lg.AddComponent<Light>();
            _ledLight.type = LightType.Point; _ledLight.range = 0.8f; _ledLight.intensity = 0.6f;
        }

        Transform Pivot(Transform parent, Vector3 pos, string name)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = pos;
            return p;
        }

        Transform Add(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, string region, Color baseColor)
        {
            var mat = Mats.LitUnique(baseColor, 0f, 0.45f);
            var go = MeshGen.Part(parent, mesh, mat, pos, default, scale, region);
            _parts.Add(new Part { R = go.GetComponent<Renderer>(), M = mat, Base = baseColor, Region = region });
            return go.transform;
        }

        /// <summary>Actualiza el cuerpo con el estado del astronauta seleccionado.</summary>
        public void SetHealth(AstronautHealth h, EarlyWarning w, bool floating)
        {
            foreach (var r in Regions) _sev[r] = h.Severity(r);
            _radiation = h.Severity("radiation");
            _floating = floating;
            float k = Mathf.Lerp(0.68f, 1f, Mathf.Clamp01((h.Muscle - 70f) / 30f));
            float ka = Mathf.Lerp(0.8f, 1f, Mathf.Clamp01((h.Muscle - 70f) / 30f));
            foreach (var t in _legMeshes) { var s = t.localScale; t.localScale = new Vector3(BaseWidth(t) * k, s.y, BaseWidth(t) * k); }
            foreach (var t in _armMeshes) { var s = t.localScale; t.localScale = new Vector3(BaseWidth(t) * ka, s.y, BaseWidth(t) * ka); }
            float puff = 1f + h.PlasmaDeficit / 12f * 0.12f;
            _head.localScale = new Vector3(0.24f * puff, 0.24f * puff * 0.97f, 0.24f * puff);
            _led = SensorSuite.LedColor(w.Status);
            if (w.HapticEvents > _lastHaptic) { _hapticTimer = 0.6f; _lastHaptic = w.HapticEvents; }
        }

        /// <summary>
        /// Modo bus AEGIS: el cuerpo se pinta con severidades calculadas a partir de los datos del motor
        /// (sin diagnósticos propios). musclePct puede ser null si el motor no lo envía.
        /// </summary>
        public void SetExternal(Dictionary<string, float> severity, float? musclePct, bool floating, Color led, int hapticCounter)
        {
            foreach (var r in Regions) _sev[r] = severity.TryGetValue(r, out var v) ? v : 0f;
            _radiation = severity.TryGetValue("radiation", out var rad) ? rad : 0f;
            _floating = floating;
            float k = Mathf.Lerp(0.68f, 1f, Mathf.Clamp01(((musclePct ?? 100f) - 70f) / 30f));
            foreach (var t in _legMeshes) { var s = t.localScale; t.localScale = new Vector3(BaseWidth(t) * k, s.y, BaseWidth(t) * k); }
            _head.localScale = Vector3.one * 0.24f;
            _led = led;
            if (hapticCounter > _lastHaptic) { _hapticTimer = 0.6f; _lastHaptic = hapticCounter; }
        }

        int _lastHaptic;
        readonly Dictionary<Transform, float> _baseWidth = new Dictionary<Transform, float>();
        float BaseWidth(Transform t)
        {
            if (!_baseWidth.TryGetValue(t, out var w)) { w = t.localScale.x; _baseWidth[t] = w; }
            return w;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float pulse = 0.65f + 0.35f * Mathf.Sin(_t * 4f);
            foreach (var p in _parts)
            {
                _sev.TryGetValue(p.Region, out float s);
                var col = SeverityColor(s);
                Mats.SetColor(p.M, Color.Lerp(p.Base, col, Mathf.Clamp(s * 1.3f, 0f, 0.85f)));
                Mats.SetEmission(p.M, s > 0.12f ? col * (s * 1.4f * pulse) : Color.black);
            }
            var a = new Color(1f, 0.15f, 0.35f, _radiation * 0.3f * (0.8f + 0.2f * Mathf.Sin(_t * 2f)));
            Mats.SetColor(_auraMat, a);
            _aura.enabled = _radiation > 0.02f;

            float blink = _led.r > 0.9f && _led.g < 0.5f ? (Mathf.Sin(_t * 10f) > 0 ? 1f : 0.2f) : 1f;
            Mats.SetColor(_ledMat, _led);
            Mats.SetEmission(_ledMat, _led * 3f * blink);
            _ledLight.color = _led; _ledLight.intensity = 0.6f * blink;

            if (_heart) _heart.localScale = Vector3.one * 0.1f * (1f + 0.35f * _heartPulse);
            foreach (var l in _lungs) l.localScale = new Vector3(0.11f, 0.19f, 0.07f) * (0.88f + 0.22f * _breath);

            // Postura: de pie (0) <-> postura neutra flotando (1)
            _float = Mathf.MoveTowards(_float, _floating ? 1f : 0f, Time.deltaTime * 0.8f);
            float f = _float;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                _shoulders[i].localEulerAngles = new Vector3(-35f * f, 0, side * 25f * f);
                _elbows[i].localEulerAngles = new Vector3(-45f * f, 0, 0);
                _hips[i].localEulerAngles = new Vector3(-40f * f, 0, side * 6f * f);
                _knees[i].localEulerAngles = new Vector3(55f * f, 0, 0);
            }
            // Vibración háptica (alerta enviada al actuador): pequeño temblor del cuerpo
            _hapticTimer = Mathf.Max(0f, _hapticTimer - Time.deltaTime);
            float shake = _hapticTimer > 0f ? Mathf.Sin(_t * 90f) * 0.006f : 0f;
            _body.localPosition = new Vector3(shake, f * (0.35f + Mathf.Sin(_t * 0.8f) * 0.08f), 0);
            _body.localEulerAngles = new Vector3(f * Mathf.Sin(_t * 0.29f) * 6f, 0, f * Mathf.Sin(_t * 0.37f) * 9f);
        }

        static Color SeverityColor(float s)
        {
            var green = new Color(0.25f, 0.85f, 0.45f); var yellow = new Color(1f, 0.85f, 0.2f); var red = new Color(1f, 0.18f, 0.12f);
            return s < 0.5f ? Color.Lerp(green, yellow, s * 2f) : Color.Lerp(yellow, red, (s - 0.5f) * 2f);
        }
    }
}
