using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LlamAcademy.Dinos.Mobile
{
    /// <summary>
    /// Utility class for adaptive UI scaling, DPI adaptation, and touch/mouse hit testing.
    /// Ensures IMGUI elements render at clear, comfortable, proportional sizes on all screen resolutions,
    /// both on Mobile (Android/iOS) and on PC Standalone (.exe) across Full HD, 2K, and 4K displays.
    /// </summary>
    public static class MobileUIHelper
    {
        // Reference virtual height in landscape mode (720p base reference provides ideal button proportions across all resolutions)
        private const float BASE_REFERENCE_HEIGHT = 720f;

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
        /// Proportional scale factor for all platforms (Mobile & PC Standalone .exe).
        /// Adapts dynamically to screen resolution: 720p=1.0x, 1080p=1.5x, 1440p=2.0x, 4K=3.0x.
        /// </summary>
        public static float ScaleFactor
        {
            get
            {
                float factor = (float)Screen.height / BASE_REFERENCE_HEIGHT;
                return Mathf.Max(1.0f, factor);
            }
        }

        public static float ScreenWidth => Screen.width / ScaleFactor;
        public static float ScreenHeight => Screen.height / ScaleFactor;

        private static Matrix4x4 _cachedMatrix = Matrix4x4.identity;
        private static bool _isScalingActive = false;

        private static int _lastRegisteredFrame = -1;
        private static readonly List<Rect> _activeUIRects = new();

        public static void ClearUIRects()
        {
            _activeUIRects.Clear();
        }

        public static void RegisterUIRect(Rect virtualRect)
        {
            // Tự động làm mới danh sách rect theo frame, loại bỏ hoàn toàn các modal cũ đã đóng
            if (Time.frameCount != _lastRegisteredFrame)
            {
                _activeUIRects.Clear();
                _lastRegisteredFrame = Time.frameCount;
            }

            if (!_activeUIRects.Contains(virtualRect))
            {
                _activeUIRects.Add(virtualRect);
            }
        }

        /// <summary>
        /// Begins scaling the IMGUI matrix across all platforms.
        /// Must be paired with EndScaling().
        /// </summary>
        public static void BeginScaling()
        {
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
            if (!_isScalingActive) return;
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

            // Nếu các UI rect không thuộc frame hiện tại hoặc frame liền trước, không chặn
            if (Time.frameCount > _lastRegisteredFrame + 1)
            {
                return false;
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
