using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps.Assistant
{
    /// <summary>Lo que el panel y la asistente necesitan de un rostro (realista u holograma de líneas).</summary>
    public interface IAstraFace
    {
        RenderTexture Texture { get; }
        float SpeakLevel { get; set; }
        bool Thinking { get; set; }
        Emotion Current { get; }
        void SetEmotion(Emotion e);
    }

    /// <summary>
    /// Rostro realista de ASTRA: avatar Medical_Female_02 de Microsoft Rocketbox (licencia MIT) con
    /// 176 gestos faciales (blendshapes ARKit, visemas y FACS), dibujado como holograma azul.
    /// Boca sincronizada con la voz, parpadeo, mirada, leve movimiento de cabeza y expresiones:
    /// amable, feliz, preocupada, firme, alerta y pensando. Se dibuja en una RenderTexture
    /// (capa 29) que muestra el panel de ASTRA.
    /// </summary>
    public class AvatarFace : MonoBehaviour, IAstraFace
    {
        public const int Layer = HoloFace.Layer;
        const string Path = "Astra/";

        public RenderTexture Texture { get; private set; }
        public float SpeakLevel { get; set; }
        public bool Thinking { get; set; }
        public Emotion Current => _emotion;

        Emotion _emotion = Emotion.Amable;
        SkinnedMeshRenderer _smr;
        readonly Dictionary<string, int> _idx = new Dictionary<string, int>();
        readonly Dictionary<int, float> _cur = new Dictionary<int, float>();
        readonly Dictionary<int, float> _target = new Dictionary<int, float>();
        readonly List<Material> _mats = new List<Material>();
        Transform _head;
        Quaternion _headBase;
        Vector3 _fwd, _right;
        float _blinkTimer = 2f, _blinkT = -1f, _gazeTimer, _speak;
        Vector2 _gaze, _gazeTarget;
        Color _tint = Tint(Emotion.Amable);

        public static AvatarFace TryCreate()
        {
            var prefab = Resources.Load<GameObject>(Path + "Medical_Female_02_facial");
            if (prefab == null) return null;
            var root = new GameObject("ASTRA_Avatar");
            root.transform.position = new Vector3(0f, -3000f, 0f);
            var model = Instantiate(prefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            var face = root.AddComponent<AvatarFace>();
            if (!face.Setup(model)) { Destroy(root); return null; }
            return face;
        }

        bool Setup(GameObject model)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var a in model.GetComponentsInChildren<Animation>(true)) Destroy(a);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Destroy(a);
            _smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (_smr == null || _smr.sharedMesh == null) return false;
            _smr.updateWhenOffscreen = true;

            var mesh = _smr.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string n = mesh.GetBlendShapeName(i);
                int dot = n.LastIndexOf('.');
                _idx[dot >= 0 ? n.Substring(dot + 1) : n] = i;
            }

            Transform Find(string name)
            {
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
                return null;
            }
            _head = Find("Bip01 Head");
            var lEye = Find("Bip01 LEye");
            var rEye = Find("Bip01 REye");
            if (_head == null || lEye == null || rEye == null) return false;

            // Brazos abajo (el modelo viene en pose T)
            LowerArm(Find("Bip01 L UpperArm"), Find("Bip01 L Forearm"));
            LowerArm(Find("Bip01 R UpperArm"), Find("Bip01 R Forearm"));
            _headBase = _head.rotation;

            // Materiales: holograma con el detalle de cada textura original
            var shader = Shader.Find("SpaceApps/HoloAvatar");
            var src = _smr.sharedMaterials;
            var mats = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                string n = src[i] != null ? src[i].name.ToLowerInvariant() : "";
                string tex = n.Contains("opacity") ? "f153_opacity_color" : n.Contains("stetoskop") ? "f153_stetoskop_color" :
                             n.Contains("body") ? "f153_body_color" : "f153_head_color";
                var m = new Material(shader);
                m.SetTexture("_MainTex", Resources.Load<Texture2D>(Path + tex));
                m.SetFloat("_Cutoff", n.Contains("opacity") ? 0.35f : 0.02f);
                if (n.Contains("body")) m.SetFloat("_Detail", 0.8f);
                mats[i] = m;
                _mats.Add(m);
            }
            _smr.sharedMaterials = mats;
            ApplyTint(_tint);

            // Cámara: cabeza y hombros, de frente
            Vector3 eyes = (lEye.position + rEye.position) * 0.5f;
            float s = Mathf.Max(0.01f, Vector3.Distance(lEye.position, rEye.position) / 0.062f);
            _fwd = eyes - _head.position; _fwd.y = 0f;
            if (_fwd.sqrMagnitude < 1e-8f) _fwd = model.transform.forward;
            _fwd.Normalize();
            _right = Vector3.Cross(Vector3.up, _fwd);

            Texture = new RenderTexture(768, 384, 24) { name = "ASTRA_Rostro", antiAliasing = 4 };
            var camGo = new GameObject("ASTRA_Camara");
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            Vector3 target = eyes + Vector3.down * (0.065f * s);
            cam.transform.position = target + _fwd * (0.82f * s) + Vector3.up * (0.02f * s);
            cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
            cam.fieldOfView = 25f;
            cam.nearClipPlane = 0.05f * s;
            cam.farClipPlane = 5f * s;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.012f, 0.03f, 0.055f, 1f);
            cam.cullingMask = 1 << Layer;
            cam.targetTexture = Texture;
            cam.allowHDR = false;
            return true;
        }

        void LowerArm(Transform upper, Transform fore)
        {
            if (upper == null || fore == null) return;
            Vector3 dir = (fore.position - upper.position).normalized;
            Vector3 side = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
            Vector3 aim = (Vector3.down * 0.96f + side * 0.22f).normalized;
            upper.rotation = Quaternion.FromToRotation(dir, aim) * upper.rotation;
        }

        public void SetEmotion(Emotion e) => _emotion = e;

        static Color Tint(Emotion e)
        {
            switch (e)
            {
                case Emotion.Firme: return new Color(1f, 0.5f, 0.45f);
                case Emotion.Alerta: return new Color(1f, 0.72f, 0.32f);
                case Emotion.Feliz: return new Color(0.42f, 0.9f, 0.95f);
                default: return new Color(0.36f, 0.72f, 1f);
            }
        }

        void ApplyTint(Color c)
        {
            foreach (var m in _mats)
            {
                m.SetColor("_Color", c);
                m.SetColor("_Rim", Color.Lerp(c, Color.white, 0.45f));
            }
        }

        void Set(string name, float w)
        {
            if (_idx.TryGetValue(name, out int i)) _target[i] = Mathf.Max(_target.TryGetValue(i, out var o) ? o : 0f, w);
        }

        void Pair(string left, string right, float w) { Set(left, w); Set(right, w); }

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            foreach (var key in new List<int>(_target.Keys)) _target[key] = 0f;
            _speak = Mathf.Lerp(_speak, SpeakLevel, 1f - Mathf.Exp(-dt * 25f));

            // Expresión
            var emo = Thinking ? Emotion.Pensando : _emotion;
            float smile = 0f;
            switch (emo)
            {
                case Emotion.Amable: smile = 32f; Pair("AK_07_CheekSquintLeft", "AK_08_CheekSquintRight", 12f); Set("AK_03_BrowInnerUp", 8f); break;
                case Emotion.Feliz: smile = 62f; Pair("AK_07_CheekSquintLeft", "AK_08_CheekSquintRight", 32f); Pair("AK_19_EyeSquintLeft", "AK_20_EyeSquintRight", 12f); break;
                case Emotion.Preocupada: Set("AK_03_BrowInnerUp", 75f); Pair("AK_30_MouthFrownLeft", "AK_31_MouthFrownRight", 32f); Pair("AK_36_MouthPressLeft", "AK_37_MouthPressRight", 12f); break;
                case Emotion.Firme: Pair("AK_01_BrowDownLeft", "AK_02_BrowDownRight", 60f); Pair("AK_36_MouthPressLeft", "AK_37_MouthPressRight", 30f); Pair("AK_19_EyeSquintLeft", "AK_20_EyeSquintRight", 22f); Pair("AK_50_NoseSneerLeft", "AK_51_NoseSneerRight", 10f); break;
                case Emotion.Alerta: Pair("AK_21_EyeWideLeft", "AK_22_EyeWideRight", 65f); Set("AK_03_BrowInnerUp", 45f); Pair("AK_04_BrowOuterUpLeft", "AK_05_BrowOuterUpRight", 55f); break;
                case Emotion.Pensando: Set("AK_03_BrowInnerUp", 22f); Set("AK_04_BrowOuterUpLeft", 35f); Pair("AK_17_EyeLookUpLeft", "AK_18_EyeLookUpRight", 42f); Set("AK_33_MouthLeft", 14f); Set("AK_38_MouthPucker", 10f); break;
            }
            smile *= 1f - 0.45f * _speak;
            Pair("AK_44_MouthSmileLeft", "AK_45_MouthSmileRight", smile);

            // Habla: mandíbula + visemas que alternan suavemente
            if (_speak > 0.02f)
            {
                float n1 = Mathf.PerlinNoise(t * 7.3f, 0.1f), n2 = Mathf.PerlinNoise(t * 6.1f, 3.7f), n3 = Mathf.PerlinNoise(t * 8.7f, 9.2f);
                Set("AK_25_JawOpen", _speak * 30f);
                Set("AA_VI_10_aa", _speak * 60f * n1);
                Set("AA_VI_13_O", _speak * 40f * n2);
                Set("AA_VI_11_E", _speak * 38f * n3);
                Set("AA_VI_02_FF", _speak * 18f * Mathf.Clamp01(n3 - n1));
            }

            // Parpadeo (a veces doble)
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f) { _blinkT = 0f; _blinkTimer = Random.value < 0.15f ? 0.28f : Random.Range(2.2f, 5.2f); }
            if (_blinkT >= 0f)
            {
                _blinkT += dt;
                float b = 1f - Mathf.Abs(_blinkT / 0.065f - 1f);
                if (_blinkT > 0.13f) { _blinkT = -1f; b = 0f; }
                Pair("AK_09_EyeBlinkLeft", "AK_10_EyeBlinkRight", Mathf.Clamp01(b) * 100f);
            }

            // Mirada: pequeños movimientos de ojos
            _gazeTimer -= dt;
            if (_gazeTimer <= 0f) { _gazeTarget = Random.insideUnitCircle * 12f; _gazeTimer = Random.Range(0.7f, 2.6f); }
            _gaze = Vector2.Lerp(_gaze, _gazeTarget, 1f - Mathf.Exp(-dt * 18f));
            if (_gaze.x > 0) { Set("AK_15_EyeLookOutLeft", _gaze.x); Set("AK_14_EyeLookInRight", _gaze.x); }
            else { Set("AK_13_EyeLookInLeft", -_gaze.x); Set("AK_16_EyeLookOutRight", -_gaze.x); }
            if (_gaze.y > 0) Pair("AK_17_EyeLookUpLeft", "AK_18_EyeLookUpRight", _gaze.y);
            else Pair("AK_11_EyeLookDownLeft", "AK_12_EyeLookDownRight", -_gaze.y);

            // Aplicar con suavizado (el parpadeo y la boca, rápidos)
            float k = Mathf.Min(1f, 1.6f * (1f - Mathf.Exp(-dt * 12f)));
            foreach (var kv in _target)
            {
                float c = _cur.TryGetValue(kv.Key, out var v) ? v : 0f;
                c = Mathf.Lerp(c, kv.Value, k);
                _cur[kv.Key] = c;
                _smr.SetBlendShapeWeight(kv.Key, c);
            }

            _tint = Color.Lerp(_tint, Tint(_emotion), 1f - Mathf.Exp(-dt * 4f));
            ApplyTint(_tint);
        }

        void LateUpdate()
        {
            if (_head == null) return;
            float t = Time.time;
            float yaw = (Mathf.PerlinNoise(t * 0.21f, 1.3f) - 0.5f) * 7f;
            float pitch = (Mathf.PerlinNoise(t * 0.17f, 5.1f) - 0.5f) * 5f + _speak * 2.5f * Mathf.Sin(t * 5.3f);
            float roll = (Mathf.PerlinNoise(t * 0.13f, 8.4f) - 0.5f) * 4f + (Thinking ? 7f : 0f);
            if (_emotion == Emotion.Firme && !Thinking) pitch += 3f;
            _head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, _right) * Quaternion.AngleAxis(roll, _fwd) * _headBase;
        }

        void OnDestroy() { if (Texture != null) Texture.Release(); }
    }
}
