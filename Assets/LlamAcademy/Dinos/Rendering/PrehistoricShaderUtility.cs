using UnityEngine;

namespace LlamAcademy.Dinos.Rendering
{
    public static class PrehistoricShaderUtility
    {
        private static Shader _CachedLitShader;
        private static Shader _CachedUnlitShader;
        private static Material _FallbackLitMat;
        private static Material _FallbackUnlitMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeGraphicsAndQuality()
        {
            // Đảm bảo game chạy ở mức đồ họa cao nhất (Ultra) khi build độc lập
            int maxQuality = QualitySettings.names.Length - 1;
            if (QualitySettings.GetQualityLevel() < maxQuality)
            {
                QualitySettings.SetQualityLevel(maxQuality, true);
                Debug.Log($"<color=cyan>[Prehistoric Graphics]</color> Tự động nâng cấp mức đồ họa lên: <b>{QualitySettings.names[maxQuality]}</b>");
            }

            CacheShaders();
        }

        public static void CacheShaders()
        {
            if (_CachedLitShader == null)
            {
                _CachedLitShader = Shader.Find("Universal Render Pipeline/Lit");
                if (_CachedLitShader == null) _CachedLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (_CachedLitShader == null) _CachedLitShader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (_CachedUnlitShader == null)
            {
                _CachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (_CachedUnlitShader == null) _CachedUnlitShader = Shader.Find("Sprites/Default");
                if (_CachedUnlitShader == null) _CachedUnlitShader = _CachedLitShader;
            }

            if (_FallbackLitMat == null)
            {
                _FallbackLitMat = Resources.Load<Material>("Materials/URP_Lit_Fallback");
                if (_FallbackLitMat == null) _FallbackLitMat = Resources.Load<Material>("Materials/URP_SimpleLit_Fallback");
            }

            if (_FallbackUnlitMat == null)
            {
                _FallbackUnlitMat = Resources.Load<Material>("Materials/URP_Unlit_Fallback");
            }
        }

        public static Shader GetLitShader()
        {
            if (_CachedLitShader == null) CacheShaders();
            if (_CachedLitShader != null) return _CachedLitShader;
            if (_FallbackLitMat != null) return _FallbackLitMat.shader;
            return Shader.Find("Sprites/Default");
        }

        public static Shader GetUnlitShader()
        {
            if (_CachedUnlitShader == null) CacheShaders();
            if (_CachedUnlitShader != null) return _CachedUnlitShader;
            if (_FallbackUnlitMat != null) return _FallbackUnlitMat.shader;
            return GetLitShader();
        }

        public static Material CreateSafeMaterial(Color color, float smoothness = 0.2f)
        {
            CacheShaders();
            Material mat = null;

            if (_CachedLitShader != null)
            {
                mat = new Material(_CachedLitShader);
            }
            else if (_FallbackLitMat != null)
            {
                mat = new Material(_FallbackLitMat);
            }
            else if (_CachedUnlitShader != null)
            {
                mat = new Material(_CachedUnlitShader);
            }
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Sprites/Default");
                mat = s != null ? new Material(s) : new Material(Shader.Find("UI/Default"));
            }

            ApplyColor(mat, color, smoothness);
            return mat;
        }

        public static Material CreateUnlitMaterial(Color color)
        {
            CacheShaders();
            Material mat = null;

            if (_CachedUnlitShader != null)
            {
                mat = new Material(_CachedUnlitShader);
            }
            else if (_FallbackUnlitMat != null)
            {
                mat = new Material(_FallbackUnlitMat);
            }
            else
            {
                mat = CreateSafeMaterial(color, 0f);
            }

            ApplyColor(mat, color, 0f);
            return mat;
        }

        public static Material CreateLineMaterial(Color color)
        {
            return CreateUnlitMaterial(color);
        }

        private static void ApplyColor(Material mat, Color color, float smoothness)
        {
            if (mat == null) return;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        }
    }
}
