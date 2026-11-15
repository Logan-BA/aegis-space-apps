using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>Gráfico de líneas dibujado en una Texture2D (para mostrar con IMGUI).</summary>
    public class ChartTexture
    {
        public readonly Texture2D Tex;
        readonly int _w, _h;
        readonly Color32[] _px;
        static readonly Color32 Bg = new Color32(5, 8, 20, 200);
        static readonly Color32 Grid = new Color32(255, 255, 255, 22);
        static readonly Color32 Marker = new Color32(255, 255, 255, 60);

        public float YMin, YMax, XMax = 100;

        public ChartTexture(int w = 760, int h = 210)
        {
            _w = w; _h = h; _px = new Color32[w * h];
            Tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        }

        public void Begin(float yMin, float yMax, float xMax)
        {
            YMin = yMin; YMax = yMax; XMax = Mathf.Max(1f, xMax);
            for (int i = 0; i < _px.Length; i++) _px[i] = Bg;
            for (int g = 0; g <= 4; g++) HLine(Mathf.RoundToInt(g / 4f * (_h - 1)), Grid, false);
        }

        public void VerticalMarker(float x) { int px = X(x); for (int y = 0; y < _h; y++) Set(px, y, Marker); }

        public void Reference(float y, Color c) => HLine(Y(y), c, true);

        public void Series(IReadOnlyList<float> values, Color c)
        {
            if (values == null || values.Count < 2) return;
            Color32 col = c;
            int step = Mathf.Max(1, values.Count / _w);
            int px0 = X(1), py0 = Y(values[0]);
            for (int i = step; i < values.Count; i += step)
            {
                int px1 = X(i + 1), py1 = Y(values[i]);
                Line(px0, py0, px1, py1, col);
                Line(px0, py0 + 1, px1, py1 + 1, col);
                px0 = px1; py0 = py1;
            }
        }

        public void End() { Tex.SetPixels32(_px); Tex.Apply(false); }

        int X(float day) => Mathf.Clamp(Mathf.RoundToInt(day / XMax * (_w - 1)), 0, _w - 1);
        int Y(float v) => Mathf.Clamp(Mathf.RoundToInt((v - YMin) / Mathf.Max(1e-4f, YMax - YMin) * (_h - 1)), 0, _h - 1);

        void HLine(int y, Color32 c, bool dashed)
        {
            for (int x = 0; x < _w; x++) if (!dashed || (x / 6) % 2 == 0) { Set(x, y, c); Set(x, y + 1, c); }
        }

        void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            _px[y * _w + x] = c;
        }

        void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }
}
