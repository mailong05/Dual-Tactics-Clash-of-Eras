using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LlamAcademy.Dinos.Mobile
{
    /// <summary>
    /// Utility class for adaptive mobile UI scaling, DPI adaptation, and touch hit testing.
    /// Ensures IMGUI elements render at finger-friendly proportions on all Android screen sizes and resolutions.
    /// PC desktop resolution and layout remain 100% unaffected.
    /// </summary>
    public static class MobileUIHelper
    {
        // Reference virtual height in landscape mode (640p provides ideal touch targets for mobile)
        private const float BASE_REFERENCE_HEIGHT = 640f;

        public static bool ForceMobileSimulation = false;

        public static bool IsMobile
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return true;
#else
                return Application.isMobilePlatform 
                    || Application.platform == RuntimePlatform.Android 
                    || Application.platform == RuntimePlatform.IPhonePlayer 
                    || ForceMobileSimulation;
#endif
            }
        }

        /// <summary>
        /// Proportional scale factor for mobile devices.
        /// On PC standalone, always returns 1.0f.
        /// </summary>
        public static float ScaleFactor
        {
            get
            {
                if (!IsMobile) return 1.0f;
                float factor = (float)Screen.height / BASE_REFERENCE_HEIGHT;
                return Mathf.Max(1.0f, factor);
            }
        }

        public static float ScreenWidth => IsMobile ? (Screen.width / ScaleFactor) : Screen.width;
        public static float ScreenHeight => IsMobile ? (Screen.height / ScaleFactor) : Screen.height;

        private static Matrix4x4 _cachedMatrix = Matrix4x4.identity;
        private static bool _isScalingActive = false;

        // Dynamic registry of active UI Rectangles (in virtual GUI coordinate space)
        private static readonly List<Rect> _activeUIRects = new();

        public static void ClearUIRects()
        {
            _activeUIRects.Clear();
        }

        public static void RegisterUIRect(Rect virtualRect)
        {
            if (!_activeUIRects.Contains(virtualRect))
            {
                _activeUIRects.Add(virtualRect);
            }
        }

        /// <summary>
        /// Begins scaling the IMGUI matrix on mobile platforms.
        /// Must be paired with EndScaling().
        /// </summary>
        public static void BeginScaling()
        {
            if (!IsMobile) return;
            _cachedMatrix = GUI.matrix;
            _isScalingActive = true;

            float s = ScaleFactor;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1.0f));
        }

        /// <summary>
        /// Restores the original IMGUI matrix.
        /// </summary>
        public static void EndScaling()
        {
            if (!IsMobile || !_isScalingActive) return;
            GUI.matrix = _cachedMatrix;
            _isScalingActive = false;
        }

        /// <summary>
        /// Tests if a screen-space coordinate (such as a touch position) lies on top of an active UI element.
        /// Screen position: (0,0) is bottom-left.
        /// </summary>
        public static bool IsPointerOverUI(Vector2 screenPosition)
        {
            // 1. Check uGUI Canvas EventSystem
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

            // 2. Check IMGUI active control
            if (GUIUtility.hotControl != 0)
            {
                return true;
            }

            // 3. Convert screen coordinates (bottom-left origin) to virtual GUI coordinates (top-left origin)
            float s = ScaleFactor;
            float virtualGuiX = screenPosition.x / s;
            float virtualGuiY = (Screen.height - screenPosition.y) / s;
            Vector2 guiPoint = new Vector2(virtualGuiX, virtualGuiY);

            for (int i = 0; i < _activeUIRects.Count; i++)
            {
                if (_activeUIRects[i].Contains(guiPoint))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
