using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceApps
{
    /// <summary>
    /// Íconos generados por código (sin archivos de imagen): figuras blancas con borde suave que se
    /// tiñen con GUI.color. Se dibujan con funciones de distancia (SDF) a texturas de 128 × 128.
    /// Nombres: heart, lungs, bone0..bone3 (hueso con grietas crecientes), dumbbell, battery, eye,
    /// radiation, moon, face_happy, face_neutral, face_sad, kidney, drop, thermo, shield, star,
    /// planet, rocket, air, pulse, sun, gauge.
    /// </summary>
    public static class Icons
    {
        const int S = 128;
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string name)
        {
            if (Cache.TryGetValue(name, out var t) && t != null) return t;
            t = Make(Shape(name));
            t.name = "Icono_" + name;
            Cache[name] = t;
            return t;
        }

        /// <summary>Hueso con grietas según severidad 0..1 (0 sano, 1 roto).</summary>
        public static Texture2D Bone(float severity) => Get("bone" + Mathf.Clamp(Mathf.FloorToInt(severity * 4f), 0, 3));

        public static string FaceName(float stress01) => stress01 < 0.35f ? "face_happy" : stress01 < 0.65f ? "face_neutral" : "face_sad";
        public static Texture2D Face(float stress01) => Get(FaceName(stress01));

        static Texture2D Make(Func<Vector2, float> sdf)
        {
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[S * S];
            float pix = 2f / S;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2((x + 0.5f) / S * 2f - 1f, (y + 0.5f) / S * 2f - 1f);
                    float a = Mathf.Clamp01(0.5f - sdf(p) / pix);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // ------------------------------------------------------------ figuras

        static Func<Vector2, float> Shape(string name)
        {
            switch (name)
            {
                case "heart": return p => Heart(p);
                case "lungs": return Lungs;
                case "bone0": return p => BoneShape(p, 0);
                case "bone1": return p => BoneShape(p, 1);
                case "bone2": return p => BoneShape(p, 2);
                case "bone3": return p => BoneShape(p, 3);
                case "dumbbell":
                    return p => Min(Seg(p, new Vector2(-0.62f, 0), new Vector2(0.62f, 0), 0.07f),
                                    Box(p, new Vector2(-0.42f, 0), new Vector2(0.08f, 0.42f), 0.04f),
                                    Box(p, new Vector2(0.42f, 0), new Vector2(0.08f, 0.42f), 0.04f),
                                    Box(p, new Vector2(-0.62f, 0), new Vector2(0.07f, 0.3f), 0.04f),
                                    Box(p, new Vector2(0.62f, 0), new Vector2(0.07f, 0.3f), 0.04f));
                case "battery":
                    return p => Mathf.Min(Mathf.Max(Box(p, Vector2.zero, new Vector2(0.72f, 0.42f), 0.08f), -Box(p, Vector2.zero, new Vector2(0.62f, 0.32f), 0.04f)),
                                          Box(p, new Vector2(0.8f, 0), new Vector2(0.08f, 0.16f), 0.03f));
                case "eye":
                    return p =>
                    {
                        float lens = Mathf.Max(Circle(p, new Vector2(0, -0.62f), 1.0f), Circle(p, new Vector2(0, 0.62f), 1.0f));
                        float inner = Mathf.Max(Circle(p, new Vector2(0, -0.62f), 0.9f), Circle(p, new Vector2(0, 0.62f), 0.9f));
                        float outline = Mathf.Max(lens, -inner);
                        float iris = Mathf.Max(Circle(p, Vector2.zero, 0.3f), -Circle(p, new Vector2(0.08f, 0.08f), 0.09f));
                        return Mathf.Min(outline, iris);
                    };
                case "radiation":
                    return p =>
                    {
                        float r = p.magnitude;
                        float center = r - 0.15f;
                        float ring = Mathf.Max(r - 0.9f, 0.28f - r);
                        float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                        float best = 999f;
                        foreach (float c in new[] { 90f, 210f, 330f })
                        {
                            float da = Mathf.Abs(Mathf.DeltaAngle(ang, c)) - 30f;
                            best = Mathf.Min(best, da * Mathf.Deg2Rad * Mathf.Max(r, 0.2f));
                        }
                        return Mathf.Min(center, Mathf.Max(ring, best));
                    };
                case "moon":
                    return p => Min(Mathf.Max(Circle(p, new Vector2(-0.1f, -0.05f), 0.72f), -Circle(p, new Vector2(0.25f, 0.25f), 0.6f)),
                                    Star(p - new Vector2(0.55f, -0.45f), 0.14f), Star(p - new Vector2(0.7f, 0.05f), 0.09f));
                case "face_happy": return p => FaceShape(p, 1);
                case "face_neutral": return p => FaceShape(p, 0);
                case "face_sad": return p => FaceShape(p, -1);
                case "kidney":
                    return p => Mathf.Max(Ellipse(p, Vector2.zero, new Vector2(0.58f, 0.78f)), -Circle(p, new Vector2(0.62f, 0f), 0.32f));
                case "drop":
                    return p => Mathf.Min(Circle(p, new Vector2(0, -0.28f), 0.52f),
                                          Poly(p, new Vector2(-0.48f, -0.1f), new Vector2(0.48f, -0.1f), new Vector2(0f, 0.88f)));
                case "thermo":
                    return p => Mathf.Min(Seg(p, new Vector2(0, 0.75f), new Vector2(0, -0.3f), 0.16f), Circle(p, new Vector2(0, -0.58f), 0.3f));
                case "shield":
                    return p => Poly(p, new Vector2(-0.7f, 0.78f), new Vector2(0.7f, 0.78f), new Vector2(0.7f, 0.12f), new Vector2(0f, -0.88f), new Vector2(-0.7f, 0.12f));
                case "star": return p => Star(p, 0.85f);
                case "planet": return p => Circle(p, Vector2.zero, 0.82f);
                case "rocket":
                    return p => Min(Poly(p, new Vector2(-0.22f, -0.5f), new Vector2(0.22f, -0.5f), new Vector2(0.22f, 0.35f), new Vector2(0f, 0.9f), new Vector2(-0.22f, 0.35f)),
                                    Poly(p, new Vector2(-0.22f, -0.5f), new Vector2(-0.5f, -0.75f), new Vector2(-0.22f, -0.1f)),
                                    Poly(p, new Vector2(0.22f, -0.5f), new Vector2(0.22f, -0.1f), new Vector2(0.5f, -0.75f)));
                case "air":
                    return p => Min(Seg(p, new Vector2(-0.7f, 0.4f), new Vector2(0.4f, 0.4f), 0.08f),
                                    Seg(p, new Vector2(-0.7f, 0f), new Vector2(0.7f, 0f), 0.08f),
                                    Seg(p, new Vector2(-0.7f, -0.4f), new Vector2(0.2f, -0.4f), 0.08f));
                case "pulse":
                    return p => Min(Seg(p, new Vector2(-0.9f, 0f), new Vector2(-0.4f, 0f), 0.06f), Seg(p, new Vector2(-0.4f, 0f), new Vector2(-0.2f, 0.7f), 0.06f),
                                    Seg(p, new Vector2(-0.2f, 0.7f), new Vector2(0.05f, -0.7f), 0.06f), Seg(p, new Vector2(0.05f, -0.7f), new Vector2(0.25f, 0f), 0.06f),
                                    Seg(p, new Vector2(0.25f, 0f), new Vector2(0.9f, 0f), 0.06f));
                case "sun":
                    return p =>
                    {
                        float d = Circle(p, Vector2.zero, 0.42f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            d = Mathf.Min(d, Seg(p, dir * 0.58f, dir * 0.85f, 0.07f));
                        }
                        return d;
                    };
                case "gauge":
                    return p => Min(Mathf.Max(Mathf.Max(p.magnitude - 0.85f, 0.65f - p.magnitude), -p.y + 0.05f),
                                    Seg(p, Vector2.zero, new Vector2(0.45f, 0.5f), 0.07f), Circle(p, Vector2.zero, 0.12f));
                default: return p => Circle(p, Vector2.zero, 0.8f);
            }
        }

        static float Heart(Vector2 p)
        {
            var q = (p - new Vector2(0f, -0.86f)) / 1.75f;
            q.x = Mathf.Abs(q.x);
            float d;
            if (q.y + q.x > 1f) d = (q - new Vector2(0.25f, 0.75f)).magnitude - Mathf.Sqrt(2f) / 4f;
            else
            {
                float a = (q - new Vector2(0f, 1f)).sqrMagnitude;
                float m = 0.5f * Mathf.Max(q.x + q.y, 0f);
                float b = (q - new Vector2(m, m)).sqrMagnitude;
                d = Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(q.x - q.y);
            }
            return d * 1.75f;
        }

        static float Lungs(Vector2 p)
        {
            float left = Ellipse(p, new Vector2(-0.4f, -0.12f), new Vector2(0.33f, 0.6f));
            float right = Ellipse(p, new Vector2(0.4f, -0.12f), new Vector2(0.33f, 0.6f));
            float trachea = Seg(p, new Vector2(0f, 0.88f), new Vector2(0f, 0.3f), 0.08f);
            float b1 = Seg(p, new Vector2(0f, 0.32f), new Vector2(-0.28f, 0.12f), 0.07f);
            float b2 = Seg(p, new Vector2(0f, 0.32f), new Vector2(0.28f, 0.12f), 0.07f);
            return Min(left, right, trachea, b1, b2);
        }

        static float BoneShape(Vector2 p, int crack)
        {
            // hueso inclinado 30°
            float c = Mathf.Cos(-0.52f), s = Mathf.Sin(-0.52f);
            var q = new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
            float d = Min(Seg(q, new Vector2(-0.55f, 0), new Vector2(0.55f, 0), 0.15f),
                          Circle(q, new Vector2(-0.64f, 0.14f), 0.19f), Circle(q, new Vector2(-0.64f, -0.14f), 0.19f),
                          Circle(q, new Vector2(0.64f, 0.14f), 0.19f), Circle(q, new Vector2(0.64f, -0.14f), 0.19f));
            if (crack == 0) return d;
            // grieta en zigzag que avanza desde el borde superior; en el nivel 3 parte el hueso
            var pts = new[] { new Vector2(0.02f, 0.24f), new Vector2(-0.06f, 0.1f), new Vector2(0.06f, 0.0f), new Vector2(-0.05f, -0.1f), new Vector2(0.03f, -0.24f) };
            int segs = crack == 1 ? 1 : crack == 2 ? 3 : 4;
            float w = crack == 3 ? 0.055f : 0.028f;
            float cr = 999f;
            for (int i = 0; i < segs; i++) cr = Mathf.Min(cr, Seg(q, pts[i], pts[i + 1], w));
            return Mathf.Max(d, -cr);
        }

        static float FaceShape(Vector2 p, int mood)
        {
            float ring = Mathf.Max(Circle(p, Vector2.zero, 0.86f), -Circle(p, Vector2.zero, 0.72f));
            float eyes = Mathf.Min(Circle(p, new Vector2(-0.28f, 0.22f), 0.1f), Circle(p, new Vector2(0.28f, 0.22f), 0.1f));
            float mouth;
            if (mood > 0) mouth = Mathf.Max(Mathf.Abs((p - new Vector2(0, 0.05f)).magnitude - 0.42f) - 0.06f, p.y + 0.12f);
            else if (mood < 0) mouth = Mathf.Max(Mathf.Abs((p - new Vector2(0, -0.72f)).magnitude - 0.42f) - 0.06f, -(p.y + 0.42f));
            else mouth = Seg(p, new Vector2(-0.3f, -0.35f), new Vector2(0.3f, -0.35f), 0.06f);
            return Min(ring, eyes, mouth);
        }

        static float Star(Vector2 p, float r)
        {
            var v = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rr = i % 2 == 0 ? r : r * 0.45f;
                v[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            }
            return Poly(p, v);
        }

        // ------------------------------------------------------------ primitivas SDF

        static float Min(params float[] d) { float m = d[0]; for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]); return m; }
        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
        {
            var q = p - c;
            float k = new Vector2(q.x / r.x, q.y / r.y).magnitude;
            return (k - 1f) * Mathf.Min(r.x, r.y);
        }

        static float Box(Vector2 p, Vector2 c, Vector2 half, float round)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(round, round);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
        }

        static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        static float Poly(Vector2 p, params Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
    }
}
