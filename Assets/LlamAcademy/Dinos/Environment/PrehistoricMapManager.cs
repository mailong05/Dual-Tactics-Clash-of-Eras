using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LlamAcademy.Dinos.Environment
{
    public enum PrehistoricMapType
    {
        AncientValley = 0,     // Thung Lũng Cổ Đại (Scene: Dinos)
        VolcanicGorge = 1      // Hẻm Núi Dung Nham (Scene: Dinos_VolcanicGorge)
    }

    /// <summary>
    /// Quản lý việc chuyển đổi, nhận diện và lưu trữ bản đồ được chọn giữa:
    /// Map 1: Thung Lũng Cổ Đại (Ancient Valley)
    /// Map 2: Hẻm Núi Dung Nham (Volcanic Gorge)
    /// Hỗ trợ cả 2 cơ chế:
    /// 1. Tải scene riêng biệt nếu khả dụng trong Build Settings.
    /// 2. Chuyển đổi môi trường trực tiếp (Dynamic In-Place Theme Transformation) 
    ///    đảm bảo 100% chuyển map thành công tức thì không bao giờ bị đứng hoặc kẹt scene cũ.
    /// </summary>
    public static class PrehistoricMapManager
    {
        public const string SCENE_VALLEY = "Dinos";
        public const string SCENE_VOLCANIC = "Dinos_VolcanicGorge";

        private static PrehistoricMapType _currentMap = PrehistoricMapType.AncientValley;
        private static Action _pendingOnLoaded = null;
        private static bool _isRegistered = false;

        public static PrehistoricMapType CurrentMap
        {
            get
            {
                string sceneName = SceneManager.GetActiveScene().name;
                if (sceneName.IndexOf("volcanic", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return PrehistoricMapType.VolcanicGorge;
                }
                return _currentMap;
            }
            set
            {
                _currentMap = value;
                ApplyMapTheme(_currentMap);
            }
        }

        public static string GetMapDisplayName(PrehistoricMapType mapType)
        {
            return mapType switch
            {
                PrehistoricMapType.VolcanicGorge => "🌋 Hẻm Núi Dung Nham",
                _ => "🏞️ Thung Lũng Cổ Đại"
            };
        }

        public static string GetMapDescription(PrehistoricMapType mapType)
        {
            return mapType switch
            {
                PrehistoricMapType.VolcanicGorge => "Vùng lòng chảo núi lửa rực lửa với dòng dung nham nóng chảy dưới 3 cầu, vách đá bazan đen và các chốt chặn hiểm trở.",
                _ => "Thung lũng tiền sử huyền bí với rừng cọ nguyên sinh, 3 cầu gỗ cổ xưa và địa hình hẻm núi đá tự nhiên."
            };
        }

        public static string GetSceneName(PrehistoricMapType mapType)
        {
            return mapType switch
            {
                PrehistoricMapType.VolcanicGorge => SCENE_VOLCANIC,
                _ => SCENE_VALLEY
            };
        }

        public static void LoadMap(PrehistoricMapType mapType, Action onLoaded = null)
        {
            _currentMap = mapType;
            string targetScene = GetSceneName(mapType);
            string currentScene = SceneManager.GetActiveScene().name;

            bool canStreamScene = Application.CanStreamedLevelBeLoaded(targetScene);

            if (canStreamScene && currentScene != targetScene)
            {
                _pendingOnLoaded = onLoaded;
                if (!_isRegistered)
                {
                    SceneManager.sceneLoaded += HandleSceneLoaded;
                    _isRegistered = true;
                }

                try
                {
                    Debug.Log($"<color=orange>[PrehistoricMapManager]</color> Đang tải Scene bản đồ: {targetScene}...");
                    SceneManager.LoadScene(targetScene);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PrehistoricMapManager] Không thể tải Scene '{targetScene}': {ex.Message}. Tự động áp dụng Dynamic Theme Transformation trực tiếp!");
                }
            }

            // Nếu scene hiện tại đã là target hoặc scene chưa build: biến đổi trực tiếp cảnh quan hiện tại
            ApplyMapTheme(mapType);
            onLoaded?.Invoke();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyMapTheme(_currentMap);

            if (_pendingOnLoaded != null)
            {
                Action callback = _pendingOnLoaded;
                _pendingOnLoaded = null;
                callback.Invoke();
            }
        }

        public static void ApplyMapTheme(PrehistoricMapType mapType)
        {
            if (mapType == PrehistoricMapType.VolcanicGorge)
            {
                PrehistoricVolcanicMapTheme.EnsureThemeActive();
            }
            else
            {
                PrehistoricVolcanicMapTheme.EnsureThemeInactive();
            }
        }
    }
}
