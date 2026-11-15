using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Monitor de signos vitales como el de un hospital: traza de ECG (verde) y de respiración
    /// (amarilla) que se dibujan de izquierda a derecha con un borrado delante del trazo.
    /// La forma del ECG (ondas P, QRS y T) se genera con la frecuencia cardíaca del modelo.
    /// </summary>
    public class VitalMonitor
    {
        public readonly Texture2D Tex;
        public float HeartPulse { get; private set; }    // 0..1, pico en cada latido (para el corazón animado)
        public float Breath { get; private set; }        // 0..1, 1 = pulmones llenos

        const int W = 480, H = 150;
        const float PixelsPerSecond = 140f;
        readonly Color32[] _px = new Color32[W * H];
        readonly float[] _ecg = new float[W], _resp = new float[W];
        float _cardiac, _breathPhase, _cursor;
        int _x;
        static readonly Color32 Bg = new Color32(2, 10, 6, 255), Grid = new Color32(12, 50, 28, 255);
        static readonly Color32 EcgCol = new Color32(60, 255, 120, 255), RespCol = new Color32(255, 220, 60, 255);

        public VitalMonitor()
        {
            Tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "MonitorVital" };
            for (int i = 0; i < W; i++) { _ecg[i] = float.NaN; _resp[i] = float.NaN; }
        }

        /// <summary>Avanza el trazo. hr en latidos/min, rr en respiraciones/min.</summary>
        public void Tick(float dt, float hr, float rr)
        {
            dt = Mathf.Min(dt, 0.1f);
            _cursor += dt * PixelsPerSecond;
            float secPerPx = 1f / PixelsPerSecond;
            while (_cursor >= 1f)
            {
                _cursor -= 1f;
                _cardiac = (_cardiac + secPerPx * hr / 60f) % 1f;
                _breathPhase = (_breathPhase + secPerPx * rr / 60f) % 1f;
                _ecg[_x] = Ecg(_cardiac);
                _resp[_x] = Resp(_breathPhase);
                _x = (_x + 1) % W;
                for (int g = 1; g <= 12; g++) { _ecg[(_x + g) % W] = float.NaN; _resp[(_x + g) % W] = float.NaN; }   // borrado delante
            }
            HeartPulse = Mathf.Exp(-Mathf.Pow((_cardiac - 0.31f) / 0.07f, 2f));
            Breath = Resp(_breathPhase);
            Redraw();
        }

        static float Ecg(float t)
        {
            float G(float c, float w, float a) => a * Mathf.Exp(-Mathf.Pow((t - c) / w, 2f));
            return G(0.16f, 0.025f, 0.12f) + G(0.28f, 0.008f, -0.12f) + G(0.30f, 0.010f, 1f) + G(0.32f, 0.010f, -0.28f) + G(0.55f, 0.045f, 0.3f);
        }

        static float Resp(float t) => t < 0.4f ? 0.5f - 0.5f * Mathf.Cos(t / 0.4f * Mathf.PI) : 0.5f + 0.5f * Mathf.Cos((t - 0.4f) / 0.6f * Mathf.PI);

        void Redraw()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    _px[y * W + x] = (x % 24 == 0 || y % 24 == 0) ? Grid : Bg;
            Trace(_ecg, EcgCol, 92, 52);
            Trace(_resp, RespCol, 12, 34);
            Tex.SetPixels32(_px);
            Tex.Apply(false);
        }

        void Trace(float[] v, Color32 c, int baseY, int amp)
        {
            int prevY = -1;
            for (int x = 0; x < W; x++)
            {
                if (float.IsNaN(v[x])) { prevY = -1; continue; }
                int y = Mathf.Clamp(baseY + Mathf.RoundToInt(v[x] * amp), 0, H - 1);
                int y0 = prevY < 0 ? y : prevY;
                for (int yy = Mathf.Min(y0, y); yy <= Mathf.Max(y0, y); yy++)
                {
                    _px[yy * W + x] = c;
                    if (x + 1 < W) _px[yy * W + x + 1] = c;
                }
                prevY = y;
            }
        }
    }
}
