using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Lee de un material de CUALQUIER shader (de otro paquete, por ejemplo) lo que necesita el shader ligero
    /// VRKit/PropTextura: textura principal, color, recorte de hojas y doble cara. Lo hace por nombres típicos de
    /// propiedades, así que con shaders muy raros puede equivocarse (colores cambiados): para eso está la copia
    /// editable (Tools › VRKit › Mundo abierto › Copia editable de materiales) y la lista Material Overrides del OpenWorld.
    /// </summary>
    public static class PropMaterialReader
    {
        public const string ShaderName = "VRKit/PropTextura";

        static readonly string[] kTextureNames = { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_AlbedoTex", "_Diffuse", "_DiffuseMap", "_ColorMap", "_Texture" };
        static readonly string[] kColorNames = { "_BaseColor", "_Color", "_AlbedoColor" };

        public struct Look
        {
            public Texture texture;
            public string textureProperty;
            public Vector4 scaleOffset;
            public Color color;
            public string colorProperty;
            public float cutoff;      // 0 = sin recorte
            public bool twoSided;

            public override string ToString()
            {
                return "textura «" + (texture != null ? texture.name : "ninguna") + "»" + (textureProperty != null ? " (" + textureProperty + ")" : "") +
                       ", color " + ColorUtility.ToHtmlStringRGB(color) + (colorProperty != null ? " (" + colorProperty + ")" : " (blanco por defecto)") +
                       (cutoff > 0f ? ", hojas recortadas" : "") + (twoSided ? ", doble cara" : "");
            }
        }

        public static Look Read(Material m)
        {
            var look = new Look { scaleOffset = new Vector4(1f, 1f, 0f, 0f), color = Color.white };
            if (m == null) return look;
            // Todas las texturas del material, puntuadas: gana la que más parece «el color» (albedo).
            // Se descartan normal maps, máscaras y texturas de efectos (viento, nieve, charcos…).
            string bestName = null; Texture best = null; float bestScore = float.MinValue;
            string[] names = null;
            try { names = m.GetTexturePropertyNames(); } catch { names = null; }
            if (names == null || names.Length == 0) names = kTextureNames;
            foreach (var n in names)
            {
                if (!m.HasProperty(n)) continue;
                var t = m.GetTexture(n);
                if (t == null) continue;
                float s = Score(n, t);
                if (s > bestScore) { bestScore = s; best = t; bestName = n; }
            }
            if (best != null && bestScore > -100f)
            {
                look.texture = best;
                look.textureProperty = bestName;
                var s = m.GetTextureScale(bestName); var o = m.GetTextureOffset(bestName);
                look.scaleOffset = new Vector4(s.x, s.y, o.x, o.y);
            }
            foreach (var n in kColorNames) if (m.HasProperty(n)) { look.color = m.GetColor(n); look.colorProperty = n; break; }

            bool cutout = (m.renderQueue >= 2450 && m.renderQueue < 3000) || m.IsKeywordEnabled("_ALPHATEST_ON")
                          || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f);
            look.cutoff = cutout ? Mathf.Clamp(m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f, 0.05f, 0.95f) : 0f;
            look.twoSided = cutout || (m.HasProperty("_Cull") && m.GetFloat("_Cull") < 0.5f)
                            || (m.HasProperty("_DoubleSided") && m.GetFloat("_DoubleSided") > 0.5f)
                            || (m.HasProperty("_TwoSided") && m.GetFloat("_TwoSided") > 0.5f);
            return look;
        }

        /// <summary>Un material que ya usa VRKit/PropTextura se usa tal cual (es una copia corregida a mano, por ejemplo).</summary>
        public static bool IsReady(Material m) { return m != null && m.shader != null && m.shader.name == ShaderName; }

        /// <summary>Pone en target (VRKit/PropTextura) lo leído.</summary>
        public static void Apply(Material target, Look look)
        {
            target.SetTexture("_BaseMap", look.texture);
            target.SetTextureScale("_BaseMap", new Vector2(look.scaleOffset.x, look.scaleOffset.y));
            target.SetTextureOffset("_BaseMap", new Vector2(look.scaleOffset.z, look.scaleOffset.w));
            target.SetColor("_BaseColor", new Color(look.color.r, look.color.g, look.color.b, 1f));
            target.SetFloat("_Cutoff", look.cutoff);
            target.SetFloat("_Cull", look.twoSided ? 0f : 2f);
        }

        static readonly string[] kNotColor = { "normal", "bump", "nrm", "nml", "mask", "metal", "rough", "smooth", "occlusion", "_ao", "height", "parallax",
            "emiss", "detail", "noise", "wind", "snow", "wet", "puddle", "flow", "foam", "ripple", "spec", "trail", "splat", "control", "lightmap",
            "shadow", "depth", "cube", "sky", "lut", "season", "dissolve", "gradient" };
        static readonly string[] kLikeColor = { "albedo", "basecolor", "base", "main", "diffuse", "color", "colour", "col", "alb", "leaf", "leaves", "bark", "petal", "flower" };

        /// <summary>Cuánto parece una textura de color. -100 o menos = descartada.</summary>
        static float Score(string property, Texture t)
        {
            string pn = property.ToLowerInvariant(), tn = t.name.ToLowerInvariant();
            if (LooksLikeNormalMap(t)) return -1000f;
            foreach (var w in kNotColor) if (pn.Contains(w) || tn.Contains(w)) return -500f;
            float s = 0f;
            int i = System.Array.IndexOf(kTextureNames, property);
            if (i >= 0) s += 5f - i * 0.1f;
            foreach (var w in kLikeColor) { if (pn.Contains(w)) { s += 3f; break; } }
            foreach (var w in kLikeColor) { if (tn.Contains(w)) { s += 2f; break; } }
            if (tn.EndsWith("_d") || tn.EndsWith("_c") || tn.EndsWith("_a")) s += 1f;
            return s;
        }

        static bool LooksLikeNormalMap(Texture t)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("normal") || n.EndsWith("_n") || n.EndsWith("_nrm") || n.EndsWith("_nor") || n.EndsWith("_nml") || n.Contains("bump")) return true;
#if UNITY_EDITOR
            // En el editor se puede preguntar cómo está importada: lo más fiable
            var path = UnityEditor.AssetDatabase.GetAssetPath(t);
            var ti = string.IsNullOrEmpty(path) ? null : UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (ti != null && (ti.textureType == UnityEditor.TextureImporterType.NormalMap || !ti.sRGBTexture)) return true;
#endif
            return false;
        }
    }
}
