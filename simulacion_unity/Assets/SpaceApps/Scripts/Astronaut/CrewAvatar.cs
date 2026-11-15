using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Un tripulante en la escena. Usa el modelo del equipo (Models/astronauta_traje: traje con
    /// esqueleto y animación Idle). Si no existe, usa el cuerpo procedural (AstronautBody).
    /// Indicadores visuales sobrios: anillo de estado bajo los pies y luz del panel del pecho
    /// que late con la frecuencia cardíaca del tripulante.
    /// </summary>
    public class CrewAvatar : MonoBehaviour
    {
        /// <summary>Giro para que el modelo mire a la cámara (que está del lado -Z).</summary>
        public static float SuitYaw = 0f;

        // Esqueleto del modelo del equipo: (hueso, hijo para medir su dirección)
        static readonly string[] ArmBones = { "bicep.L", "antbraco.L", "mao.L", "bicep.R", "antbraco.R", "mao.R" };
        static readonly string[] LegBones = { "coxa.L", "canela.L", "pe.L", "coxa.R", "canela.R", "pe.R" };
        readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> _rest = new Dictionary<Transform, Quaternion>();

        public int Index;
        AstronautBody _fallback;
        Transform _model, _ring;
        Material _ringMat;
        readonly List<Material> _panelMats = new List<Material>();
        readonly List<Material> _lampMats = new List<Material>();
        Color _status = Color.green;
        bool _floating, _selected;
        float _hr = 70f, _beat, _float, _t, _phase;

        public bool UsesSuit => _model != null;

        public static CrewAvatar Create(int index, Transform parent)
        {
            var go = new GameObject("Tripulante_" + (index + 1));
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<CrewAvatar>();
            a.Index = index;
            a._phase = index * 1.37f;
            a.Build();
            return a;
        }

        void Build()
        {
            var suit = ModelLibrary.TryLoad("astronauta_traje", 1.85f);
            if (suit != null)
            {
                _model = suit.transform;
                _model.SetParent(transform, false);
                _model.localRotation = Quaternion.Euler(0f, SuitYaw, 0f);
                foreach (var r in _model.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.materials;   // instancias propias de este tripulante
                    foreach (var m in mats)
                    {
                        if (m.name.Contains("Panel")) _panelMats.Add(m);
                        if (m.name.Contains("Lampara") || m.name.Contains("LuzMochila")) _lampMats.Add(m);
                    }
                }
                // Las animaciones del FBX no mueven los brazos (vienen en T): la pose se controla aquí
                var anim = _model.GetComponentInChildren<Animation>();
                if (anim != null) { anim.Stop(); anim.enabled = false; }
                foreach (var t in _model.GetComponentsInChildren<Transform>(true))
                    if (System.Array.IndexOf(ArmBones, t.name) >= 0 || System.Array.IndexOf(LegBones, t.name) >= 0)
                    {
                        _bones[t.name] = t;
                        _rest[t] = t.localRotation;
                    }
                foreach (var smr in _model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
            }
            else
            {
                _fallback = gameObject.AddComponent<AstronautBody>();
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            // Aro de estado bajo los pies (fino, sin relleno)
            _ringMat = Mats.LitUnique(new Color(0.3f, 0.7f, 0.5f), 0f, 0.6f);
            var ring = MeshGen.Part(transform, Annulus(0.44f, 0.5f, 64), _ringMat, new Vector3(0, 0.015f, 0), default, null, "AnilloEstado");
            _ring = ring.transform;
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.95f, 0);
            col.height = 1.9f;
            col.radius = 0.4f;
        }

        /// <summary>Modo local: estado completo del modelo de salud.</summary>
        public void SetHealth(AstronautHealth h, EarlyWarning w, bool floating, bool selected)
        {
            _status = StatusColor(w.Status);
            _hr = h.HeartRate;
            _floating = floating; _selected = selected;
            if (_fallback) _fallback.SetHealth(h, w, floating);
        }

        /// <summary>Modo bus AEGIS: severidades visuales calculadas a partir del motor.</summary>
        public void SetExternal(Dictionary<string, float> severity, bool floating, Color led, bool selected)
        {
            _status = led;
            _hr = 70f;
            _floating = floating; _selected = selected;
            if (_fallback) _fallback.SetExternal(severity, null, floating, led, 0);
        }

        public static Color StatusColor(EarlyWarning.Level l) =>
            l == EarlyWarning.Level.Red ? new Color(0.82f, 0.29f, 0.25f) : l == EarlyWarning.Level.Yellow ? new Color(0.85f, 0.64f, 0.25f) : new Color(0.25f, 0.65f, 0.45f);

        void Update()
        {
            _t += Time.deltaTime;
            _beat = (_beat + Time.deltaTime * _hr / 60f) % 1f;
            float pulse = Mathf.Exp(-Mathf.Pow((_beat - 0.3f) / 0.08f, 2f));

            // Panel del pecho: luz con el color de estado que late con el corazón
            foreach (var m in _panelMats)
            {
                Mats.SetColor(m, Color.Lerp(new Color(0.12f, 0.15f, 0.2f), _status, 0.6f));
                Mats.SetEmission(m, _status * (0.4f + 1.6f * pulse));
            }
            foreach (var m in _lampMats) Mats.SetEmission(m, new Color(1f, 0.95f, 0.85f) * 1.5f);

            // Aro: color de estado; más visible si está seleccionado
            Mats.SetColor(_ringMat, Color.Lerp(new Color(0.1f, 0.12f, 0.15f), _status, _selected ? 0.85f : 0.4f));
            Mats.SetEmission(_ringMat, _status * (_selected ? 0.5f : 0.1f));
            _ring.localScale = Vector3.one * (_selected ? 1.1f : 0.95f);

            if (_model == null) return;
            // Microgravedad: flota y gira lentamente; con gravedad, de pie
            _float = Mathf.MoveTowards(_float, _floating ? 1f : 0f, Time.deltaTime * 0.6f);
            float f = _float;
            _model.localPosition = new Vector3(0f, f * (0.45f + 0.08f * Mathf.Sin(_t * 0.7f + _phase)), 0f);
            _model.localRotation = Quaternion.Euler(f * 6f * Mathf.Sin(_t * 0.31f + _phase), SuitYaw + f * 14f * Mathf.Sin(_t * 0.17f + _phase), f * 8f * Mathf.Sin(_t * 0.23f + _phase));
        }

        /// <summary>
        /// Pose del esqueleto: de pie con los brazos a los costados, o la postura neutra de
        /// microgravedad (brazos adelante y arriba, cadera y rodillas flexionadas).
        /// Se orienta cada hueso hacia una dirección objetivo en el marco del tripulante.
        /// </summary>
        void LateUpdate()
        {
            if (_model == null || _bones.Count == 0) return;
            foreach (var kv in _rest) kv.Key.localRotation = kv.Value;
            var root = _model;
            Vector3 up = root.up, front = -root.forward, right = root.right;
            float f = _float, breath = 0.03f * Mathf.Sin(_t * 1.6f + _phase);
            foreach (var side in new[] { "L", "R" })
            {
                if (!_bones.TryGetValue("bicep." + side, out var upper)) continue;
                float s = Mathf.Sign(root.InverseTransformPoint(upper.position).x);
                Vector3 Dir(float x, float y, float z) => (right * s * x + up * y + front * z).normalized;
                Aim(upper, _bones.GetValueOrDefault("antbraco." + side), Vector3.Slerp(Dir(0.2f, -1f, 0.05f + breath), Dir(0.6f, -0.55f, 0.5f), f));
                Aim(_bones.GetValueOrDefault("antbraco." + side), _bones.GetValueOrDefault("mao." + side), Vector3.Slerp(Dir(0.1f, -1f, 0.3f), Dir(0.3f, -0.15f, 0.95f), f));
                Aim(_bones.GetValueOrDefault("coxa." + side), _bones.GetValueOrDefault("canela." + side), Vector3.Slerp(Dir(0.04f, -1f, 0f), Dir(0.12f, -0.75f, 0.6f), f));
                Aim(_bones.GetValueOrDefault("canela." + side), _bones.GetValueOrDefault("pe." + side), Vector3.Slerp(Dir(0.02f, -1f, 0f), Dir(0.05f, -0.8f, -0.55f), f));
            }
        }

        static void Aim(Transform bone, Transform child, Vector3 targetDir)
        {
            if (bone == null || child == null) return;
            var d = child.position - bone.position;
            if (d.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(d.normalized, targetDir) * bone.rotation;
        }

        /// <summary>Malla de aro plano (corona circular) en el plano XZ.</summary>
        static Mesh Annulus(float rIn, float rOut, int seg)
        {
            var v = new Vector3[seg * 2]; var t = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * rIn; v[i * 2 + 1] = d * rOut;
                int j = (i + 1) % seg;
                t[i * 6] = i * 2; t[i * 6 + 1] = j * 2 + 1; t[i * 6 + 2] = i * 2 + 1;
                t[i * 6 + 3] = i * 2; t[i * 6 + 4] = j * 2; t[i * 6 + 5] = j * 2 + 1;
            }
            var m = new Mesh { name = "Aro", vertices = v, triangles = t };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
