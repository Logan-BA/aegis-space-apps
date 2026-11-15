using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps.Assistant
{
    public enum Emotion { Neutral, Amable, Feliz, Preocupada, Firme, Alerta, Pensando }

    /// <summary>
    /// Rostro holográfico de ASTRA (estilo malla de triángulos luminosa).
    /// La cabeza se genera por código: elipsoide con cuencas de los ojos, nariz, pómulos, labios,
    /// mentón, orejas y cuello. Ojos, párpados, cejas y labios son líneas animadas:
    /// parpadeo, mirada, boca sincronizada con la voz y expresión según la emoción.
    /// Se dibuja en una RenderTexture (capa 29) que la interfaz muestra en el panel.
    /// </summary>
    public class HoloFace : MonoBehaviour, IAstraFace
    {
        public const int Layer = 29;
        public RenderTexture Texture { get; private set; }

        public float SpeakLevel { get; set; }          // 0..1, apertura de boca (de la voz)
        public bool Thinking { get; set; }             // mirada hacia arriba mientras piensa
        Emotion _emotion = Emotion.Neutral;

        // Parámetros de expresión actuales y objetivo
        float _smile, _browIn, _browOut, _eyeOpen = 1f, _hue;
        float _tSmile, _tBrowIn, _tBrowOut, _tEyeOpen = 1f;
        Color _color = new Color(0.3f, 0.78f, 1f), _tColor = new Color(0.3f, 0.78f, 1f);
        float _blink, _nextBlink = 2f, _t, _open;
        Vector2 _gaze, _gazeTarget;
        float _nextGaze = 1f;

        Mesh _head, _wire, _points, _features, _irisMesh;
        Vector3[] _base, _verts;
        Material _fillMat, _wireMat, _pointMat, _featMat, _irisMat, _starMat;
        Transform _root;

        const float RX = 0.40f, RY = 0.52f, RZ = 0.46f;
        const int NU = 54, NV = 46;

        public static HoloFace Create()
        {
            var go = new GameObject("ASTRA_Rostro");
            go.transform.position = new Vector3(0f, -2000f, 0f);
            var f = go.AddComponent<HoloFace>();
            f.Build();
            return f;
        }

        public void SetEmotion(Emotion e)
        {
            _emotion = e;
            switch (e)
            {
                case Emotion.Amable: Target(0.45f, 0.15f, 0.05f, 0.92f, new Color(0.35f, 0.82f, 1f)); break;
                case Emotion.Feliz: Target(0.8f, 0.25f, 0.2f, 0.85f, new Color(0.45f, 0.9f, 1f)); break;
                case Emotion.Preocupada: Target(-0.35f, 0.85f, -0.2f, 1.05f, new Color(0.55f, 0.7f, 1f)); break;
                case Emotion.Firme: Target(-0.45f, -0.9f, 0.35f, 0.82f, new Color(1f, 0.5f, 0.38f)); break;
                case Emotion.Alerta: Target(-0.1f, 0.7f, 0.6f, 1.3f, new Color(1f, 0.74f, 0.3f)); break;
                case Emotion.Pensando: Target(0.05f, 0.35f, -0.1f, 0.95f, new Color(0.4f, 0.75f, 1f)); break;
                default: Target(0.12f, 0f, 0f, 1f, new Color(0.3f, 0.78f, 1f)); break;
            }
        }

        public Emotion Current => _emotion;

        void Target(float smile, float browIn, float browOut, float eyeOpen, Color c)
        {
            _tSmile = smile; _tBrowIn = browIn; _tBrowOut = browOut; _tEyeOpen = eyeOpen; _tColor = c;
        }

        // ------------------------------------------------------------ construcción

        void Build()
        {
            var shader = Shader.Find("SpaceApps/Holograma");
            Material M(float intensity, float fresnel, float scan)
            {
                var m = new Material(shader);
                m.SetFloat("_Intensity", intensity); m.SetFloat("_Fresnel", fresnel); m.SetFloat("_Scan", scan);
                return m;
            }
            _fillMat = M(0.35f, 2.2f, 0.25f);
            _wireMat = M(0.42f, 0f, 0.2f);
            _pointMat = M(1.4f, 0f, 0.1f);
            _featMat = M(1.6f, 0f, 0.05f);
            _irisMat = M(1.3f, 0f, 0f);
            _starMat = M(0.8f, 0f, 0f);

            _root = new GameObject("Cabeza").transform;
            _root.SetParent(transform, false);

            BuildHead();
            _features = new Mesh { name = "Rasgos" };
            _features.MarkDynamic();
            _irisMesh = new Mesh { name = "Iris" };
            _irisMesh.MarkDynamic();
            Child("Relleno", _head, _fillMat);
            Child("Malla", _wire, _wireMat);
            Child("Nodos", _points, _pointMat);
            Child("RasgosAnimados", _features, _featMat);
            Child("IrisAnimado", _irisMesh, _irisMat);
            Child("Particulas", Stars(), _starMat).transform.SetParent(transform, false);

            Texture = new RenderTexture(640, 320, 16) { name = "ASTRA_RT", antiAliasing = 4 };
            var camGo = new GameObject("CamaraASTRA");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, -0.06f, -3.3f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 23f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.025f, 0.05f, 1f);
            cam.cullingMask = 1 << Layer;
            cam.targetTexture = Texture;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
            cam.depth = -11;
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            SetEmotion(Emotion.Neutral);
        }

        GameObject Child(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static float G(float x, float y, float cx, float cy, float sx, float sy)
        {
            float dx = (x - cx) / sx, dy = (y - cy) / sy;
            return Mathf.Exp(-(dx * dx + dy * dy));
        }

        /// <summary>Relieve del rostro (positivo = hacia la cámara, que está en -Z).</summary>
        static float Relief(float x, float y)
        {
            float ax = Mathf.Abs(x);
            return -0.055f * G(ax, y, 0.145f, 0.06f, 0.07f, 0.045f)          // cuencas de los ojos
                   + 0.03f * G(x, y, 0f, 0.15f, 0.24f, 0.03f)                 // arco superciliar
                   + 0.10f * G(x, y, 0f, -0.06f, 0.032f, 0.085f)              // tabique nasal
                   + 0.05f * G(x, y, 0f, -0.11f, 0.05f, 0.03f)                // punta y aletas de la nariz
                   + 0.028f * G(ax, y, 0.2f, -0.03f, 0.07f, 0.06f)            // pómulos
                   + 0.035f * G(x, y, 0f, -0.225f, 0.11f, 0.045f)             // labios
                   + 0.03f * G(x, y, 0f, -0.40f, 0.09f, 0.05f);               // mentón
        }

        Vector3 SurfacePoint(float theta, float phi)
        {
            var p = new Vector3(RX * Mathf.Cos(theta) * Mathf.Sin(phi), RY * Mathf.Sin(theta), -RZ * Mathf.Cos(theta) * Mathf.Cos(phi));
            // mandíbula más angosta hacia el mentón
            float narrow = 1f - 0.3f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, -0.48f, p.y));
            p.x *= narrow;
            float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.75f, -p.z / RZ));
            p.z -= Relief(p.x, p.y) * front;
            // orejas
            float side = Mathf.Abs(p.x) / RX;
            if (side > 0.85f) p.x += Mathf.Sign(p.x) * 0.045f * G(p.y, p.z, 0.01f, 0.02f, 0.09f, 0.07f);
            return p;
        }

        void BuildHead()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float thMin = -1.25f, thMax = 1.42f, phMin = -1.95f, phMax = 1.95f;
            for (int j = 0; j <= NV; j++)
                for (int i = 0; i <= NU; i++)
                {
                    float th = Mathf.Lerp(thMin, thMax, j / (float)NV);
                    float ph = Mathf.Lerp(phMin, phMax, i / (float)NU) + ((j % 2) * 0.5f / NU) * (phMax - phMin);
                    verts.Add(SurfacePoint(th, ph));
                }
            int row = NU + 1;
            for (int j = 0; j < NV; j++)
                for (int i = 0; i < NU; i++)
                {
                    int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            // cuello: anillos hacia abajo
            int neckStart = verts.Count, rings = 6, seg = 30;
            for (int k = 0; k <= rings; k++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.Lerp(-1.9f, 1.9f, i / (float)seg);
                    float y = Mathf.Lerp(-0.42f, -0.95f, k / (float)rings);
                    float r = Mathf.Lerp(0.16f, 0.22f + 0.25f * Mathf.Max(0f, k - 4) / 2f, k / (float)rings);
                    verts.Add(new Vector3(Mathf.Sin(a) * r * 1.1f, y, -Mathf.Cos(a) * r * 0.9f + 0.03f));
                }
            for (int k = 0; k < rings; k++)
                for (int i = 0; i < seg; i++)
                {
                    int a = neckStart + k * (seg + 1) + i, b = a + 1, c = a + seg + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }

            _base = verts.ToArray();
            _verts = (Vector3[])_base.Clone();
            var cols = new Color[_base.Length];
            for (int i = 0; i < cols.Length; i++)
            {
                float front = Mathf.Clamp01(-_base[i].z / RZ);
                cols[i] = Color.white * Mathf.Lerp(0.35f, 1f, front);
            }
            _head = new Mesh { name = "CabezaHolo", vertices = _verts, triangles = tris.ToArray(), colors = cols };
            _head.RecalculateNormals();
            _head.MarkDynamic();

            // Malla de líneas (aristas de los triángulos) y nodos (vértices)
            var lines = new List<int>();
            var seen = new HashSet<long>();
            for (int t = 0; t < tris.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = tris[t + e], b = tris[t + (e + 1) % 3];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (seen.Add(key)) { lines.Add(a); lines.Add(b); }
                }
            _wire = new Mesh { name = "MallaHolo", vertices = _verts, colors = cols };
            _wire.SetIndices(lines, MeshTopology.Lines, 0);
            _wire.MarkDynamic();
            var pidx = new int[_verts.Length];
            for (int i = 0; i < pidx.Length; i++) pidx[i] = i;
            _points = new Mesh { name = "NodosHolo", vertices = _verts, colors = cols };
            _points.SetIndices(pidx, MeshTopology.Points, 0);
            _points.MarkDynamic();
        }

        static Mesh Stars()
        {
            var rnd = new System.Random(11);
            var v = new Vector3[260]; var c = new Color[v.Length]; var idx = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = new Vector3((float)rnd.NextDouble() * 3.4f - 1.7f, (float)rnd.NextDouble() * 1.6f - 0.85f, (float)rnd.NextDouble() * 0.8f + 0.2f);
                c[i] = Color.white * (0.2f + (float)rnd.NextDouble() * 0.8f);
                idx[i] = i;
            }
            var m = new Mesh { name = "Particulas", vertices = v, colors = c };
            m.SetIndices(idx, MeshTopology.Points, 0);
            return m;
        }

        // ------------------------------------------------------------ animación

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            float k = 1f - Mathf.Exp(-dt * 5f);
            _smile = Mathf.Lerp(_smile, _tSmile, k);
            _browIn = Mathf.Lerp(_browIn, _tBrowIn, k);
            _browOut = Mathf.Lerp(_browOut, _tBrowOut, k);
            _eyeOpen = Mathf.Lerp(_eyeOpen, _tEyeOpen, k);
            _color = Color.Lerp(_color, _tColor, k);

            // parpadeo natural
            _nextBlink -= dt;
            if (_nextBlink <= 0f) { _blink = 1f; _nextBlink = Random.Range(2.5f, 6f); }
            _blink = Mathf.Max(0f, _blink - dt * 7f);
            float lid = Mathf.Clamp01(_eyeOpen * (1f - Mathf.Sin(_blink * Mathf.PI)));

            // mirada: al pensar mira arriba; si no, pequeños movimientos
            _nextGaze -= dt;
            if (_nextGaze <= 0f) { _gazeTarget = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-0.3f, 0.3f)); _nextGaze = Random.Range(1.2f, 3.5f); }
            var gt = Thinking ? new Vector2(0.5f, 0.8f) : _gazeTarget;
            _gaze = Vector2.Lerp(_gaze, gt, 1f - Mathf.Exp(-dt * 8f));

            // apertura de boca suavizada a partir del nivel de la voz
            _open = Mathf.Lerp(_open, Mathf.Clamp01(SpeakLevel), 1f - Mathf.Exp(-dt * 18f));

            // cabeza: leve movimiento; asiente un poco al hablar
            _root.localRotation = Quaternion.Euler(2.5f * Mathf.Sin(_t * 0.6f) + 3f * _open * Mathf.Sin(_t * 9f) + (Thinking ? -6f : 0f),
                                                    6f * Mathf.Sin(_t * 0.35f), 1.5f * Mathf.Sin(_t * 0.5f));

            // mandíbula: los vértices bajo la boca bajan con la apertura
            for (int i = 0; i < _base.Length; i++)
            {
                var p = _base[i];
                if (p.y < -0.235f && p.z < 0f)
                {
                    float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.235f, -0.3f, p.y)) * Mathf.Clamp01(1f - Mathf.Abs(p.x) / 0.3f) * Mathf.Clamp01(-p.z / RZ);
                    p.y -= _open * 0.055f * w;
                    if (p.y > -0.45f) p.z += _open * 0.01f * w;
                }
                _verts[i] = p;
            }
            _head.vertices = _verts; _wire.vertices = _verts; _points.vertices = _verts;

            UpdateFeatures(lid);
            var flicker = 0.93f + 0.07f * Mathf.Sin(_t * 23f) * Mathf.Sin(_t * 7f);
            foreach (var m in new[] { _fillMat, _wireMat, _pointMat, _featMat, _irisMat, _starMat }) m.SetColor("_Color", _color * flicker);
        }

        readonly List<Vector3> _fv = new List<Vector3>();
        readonly List<Color> _fc = new List<Color>();
        readonly List<int> _fi = new List<int>();

        static float SurfaceZ(float x, float y)
        {
            float n = 1f - (x / RX) * (x / RX) - (y / RY) * (y / RY);
            return -RZ * Mathf.Sqrt(Mathf.Max(0.05f, n)) - Relief(x, y) - 0.008f;
        }

        void Strip(System.Func<float, Vector2> f, int n, float bright = 1f)
        {
            int start = _fv.Count;
            for (int i = 0; i <= n; i++)
            {
                var p = f(i / (float)n);
                _fv.Add(new Vector3(p.x, p.y, SurfaceZ(p.x, p.y)));
                _fc.Add(Color.white * bright);
                if (i > 0) { _fi.Add(start + i - 1); _fi.Add(start + i); }
            }
        }

        void UpdateFeatures(float lid)
        {
            _fv.Clear(); _fc.Clear(); _fi.Clear();
            var irisV = new List<Vector3>(); var irisT = new List<int>(); var irisC = new List<Color>();
            foreach (float s in new[] { -1f, 1f })
            {
                float cx = s * 0.145f, cy = 0.06f;
                float wide = Mathf.Max(0f, _eyeOpen - 1f);
                // párpado superior e inferior
                Strip(u => { float t = u * 2f - 1f; return new Vector2(cx + t * 0.058f, cy + (0.024f + 0.01f * wide) * (1f - t * t) * lid - 0.004f); }, 14);
                Strip(u => { float t = u * 2f - 1f; return new Vector2(cx + t * 0.058f, cy - 0.016f * (1f - t * t) * Mathf.Min(1f, lid + 0.3f) - 0.004f); }, 14, 0.7f);
                // iris y pupila (ocultos al cerrar el ojo)
                if (lid > 0.25f)
                {
                    var c = new Vector2(cx + _gaze.x * 0.018f, cy + _gaze.y * 0.008f);
                    float ry = 0.017f * Mathf.Min(1f, lid);
                    Disc(irisV, irisT, irisC, c, 0.017f, ry, 0.6f);
                    Disc(irisV, irisT, irisC, c, 0.007f, ry * 0.42f, 1.6f);
                }
                // ceja: el extremo interno sube (preocupación) o baja (enojo)
                Strip(u => new Vector2(cx + s * (u * 0.11f - 0.045f),
                                       0.142f + 0.015f * Mathf.Sin(Mathf.PI * u) + _browIn * 0.028f * (1f - u) + _browOut * 0.018f * u + 0.006f * wide), 12, 0.9f);
            }
            // labios: comisuras arriba (sonrisa) o abajo; el inferior baja al hablar
            float sm = _smile;
            Strip(u => { float t = u * 2f - 1f; return new Vector2(t * 0.078f, -0.236f + 0.007f * (1f - t * t) + sm * 0.018f * t * t - 0.004f * (1f - t * t) * Mathf.Abs(t) * 2f); }, 18);
            Strip(u => { float t = u * 2f - 1f; return new Vector2(t * 0.078f, -0.246f + sm * 0.018f * t * t - _open * 0.05f * (1f - t * t)); }, 18);
            Strip(u => { float t = u * 2f - 1f; return new Vector2(t * 0.062f, -0.262f + sm * 0.012f * t * t - _open * 0.052f * (1f - t * t) - 0.004f); }, 14, 0.55f);

            _features.Clear();
            _features.SetVertices(_fv); _features.SetColors(_fc);
            _features.SetIndices(_fi, MeshTopology.Lines, 0);
            _irisMesh.Clear();
            _irisMesh.SetVertices(irisV); _irisMesh.SetColors(irisC); _irisMesh.SetTriangles(irisT, 0);
        }

        static void Disc(List<Vector3> v, List<int> t, List<Color> c, Vector2 center, float rx, float ry, float bright)
        {
            int start = v.Count, seg = 16;
            v.Add(new Vector3(center.x, center.y, SurfaceZ(center.x, center.y) - 0.002f));
            c.Add(Color.white * bright);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float x = center.x + Mathf.Cos(a) * rx, y = center.y + Mathf.Sin(a) * ry;
                v.Add(new Vector3(x, y, SurfaceZ(x, y) - 0.002f));
                c.Add(Color.white * bright * 0.4f);
                if (i > 0) { t.Add(start); t.Add(start + i); t.Add(start + i + 1); }
            }
        }
    }
}
