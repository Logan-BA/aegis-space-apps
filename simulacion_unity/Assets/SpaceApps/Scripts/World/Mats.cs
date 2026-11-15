using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceApps
{
    /// <summary>
    /// Materiales creados por código. Funciona con el pipeline integrado (Standard) y con URP
    /// (si el proyecto se cambia a URP, se usan sus shaders automáticamente).
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static bool Urp => GraphicsSettings.currentRenderPipeline != null;

        static Shader LitShader => Shader.Find(Urp ? "Universal Render Pipeline/Lit" : "Standard");
        static Shader UnlitShader => Shader.Find(Urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");

        /// <summary>Material opaco (cacheado: no modificar el resultado; para materiales propios use LitUnique).</summary>
        public static Material Lit(Color c, float metallic = 0.2f, float smoothness = 0.5f)
        {
            string key = $"lit{c}{metallic}{smoothness}";
            if (!Cache.TryGetValue(key, out var m) || m == null)
            {
                m = LitUnique(c, metallic, smoothness);
                Cache[key] = m;
            }
            return m;
        }

        public static Material LitUnique(Color c, float metallic = 0.2f, float smoothness = 0.5f)
        {
            var m = new Material(LitShader);
            SetColor(m, c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat(Urp ? "_Smoothness" : "_Glossiness", smoothness);
            return m;
        }

        public static Material Emissive(Color c, float intensity = 3f)
        {
            string key = $"em{c}{intensity}";
            if (!Cache.TryGetValue(key, out var m) || m == null)
            {
                m = LitUnique(c, 0f, 0.8f);
                SetEmission(m, c * intensity);
                Cache[key] = m;
            }
            return m;
        }

        public static Material Unlit(Color c)
        {
            var m = new Material(UnlitShader);
            SetColor(m, c);
            return m;
        }

        /// <summary>Material semitransparente (halo de radiación, volúmenes de aire).</summary>
        public static Material Transparent(Color c)
        {
            var m = new Material(LitShader);
            if (Urp)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                m.SetFloat("_Mode", 2f); // Fade
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)RenderQueue.Transparent;
            SetColor(m, c);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void SetEmission(Material m, Color e)
        {
            if (e.maxColorComponent > 0.001f) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", e);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        public static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }
    }
}
