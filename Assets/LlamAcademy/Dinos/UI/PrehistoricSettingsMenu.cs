using LlamAcademy.Dinos.AR;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Mobile;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.UI
{
    /// <summary>
    /// In-Game Settings and Options Menu.
    /// Provides audio configuration (Master, BGM, SFX), game controls (Retry, Reset, Camera perspective, AR toggle),
    /// and quit functionality across both PC (exe) and Mobile (apk).
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class PrehistoricSettingsMenu : MonoBehaviour
    {
        public static PrehistoricSettingsMenu Instance { get; private set; }

        public bool IsOpen { get; set; } = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Keyboard.current != null)
            {
                // ESC or P or F10 toggles menu (only if not currently aiming tower placement)
                bool isPlacingTower = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;
                if ((Keyboard.current.pKey.wasReleasedThisFrame || Keyboard.current.f10Key.wasReleasedThisFrame) ||
                    (!isPlacingTower && Keyboard.current.escapeKey.wasReleasedThisFrame))
                {
                    ToggleMenu();
                }
            }
        }

        public void ToggleMenu()
        {
            IsOpen = !IsOpen;
            PrehistoricAudioManager.Instance?.PlayButtonClick();
            MobileTouchController.Instance?.TriggerHapticFeedback();
        }

        private void OnGUI()
        {
            MobileUIHelper.BeginScaling();
            try
            {
                DrawSettingsToggleButton();

                if (IsOpen)
                {
                    DrawSettingsModal();
                }
            }
            finally
            {
                MobileUIHelper.EndScaling();
            }
        }

        private void DrawSettingsToggleButton()
        {
            // Floating gear button at top-left (below status banner)
            float btnW = 145f;
            float btnH = 34f;
            float x = 20f;
            float y = 168f;

            Rect btnRect = new Rect(x, y, btnW, btnH);
            MobileUIHelper.RegisterUIRect(btnRect);

            GUI.backgroundColor = IsOpen ? new Color(1f, 0.4f, 0.2f, 0.95f) : new Color(0.18f, 0.24f, 0.32f, 0.90f);
            string label = IsOpen ? "<b>✕ ĐÓNG MENU</b>" : "<b>⚙️ CÀI ĐẶT / MENU</b>";

            if (GUI.Button(btnRect, label))
            {
                ToggleMenu();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawSettingsModal()
        {
            GUI.depth = -1100;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Dark semi-transparent full-screen backdrop
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, MobileUIHelper.ScreenWidth, MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float modalW = Mathf.Min(600f, MobileUIHelper.ScreenWidth - 30f);
            float modalH = Mathf.Min(520f, MobileUIHelper.ScreenHeight - 30f);
            float x = (MobileUIHelper.ScreenWidth - modalW) / 2f;
            float y = (MobileUIHelper.ScreenHeight - modalH) / 2f;

            Rect modalRect = new Rect(x, y, modalW, modalH);
            MobileUIHelper.RegisterUIRect(modalRect);

            GUI.backgroundColor = new Color(0.12f, 0.16f, 0.22f, 0.98f);
            GUI.Box(modalRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(x + 20, y + 15, modalW - 40, modalH - 30));
            GUILayout.BeginVertical();

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><size=19><color=#FFD700>⚙️ CÀI ĐẶT & TÙY CHỌN TRÒ CHƠI</color></size></b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(36), GUILayout.Height(28)))
            {
                IsOpen = false;
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ==========================================
            // Section 1: Audio Controls (Master, BGM, SFX)
            // ==========================================
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b><size=14><color=#55FF55>🔊 CÀI ĐẶT ÂM THANH & NHẠC NỀN</color></size></b>");
            GUILayout.Space(4);

            var audio = PrehistoricAudioManager.Instance;
            if (audio != null)
            {
                // Master Volume
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Âm lượng Tổng:</b> {Mathf.RoundToInt(audio.MasterVolume * 100)}%", GUILayout.Width(170));
                float newMaster = GUILayout.HorizontalSlider(audio.MasterVolume, 0f, 1f, GUILayout.Height(20));
                if (Mathf.Abs(newMaster - audio.MasterVolume) > 0.01f)
                {
                    audio.MasterVolume = newMaster;
                }
                GUILayout.EndHorizontal();

                // BGM Volume & Toggle
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Nhạc nền (BGM):</b> {Mathf.RoundToInt(audio.BgmVolume * 100)}%", GUILayout.Width(170));
                float newBgm = GUILayout.HorizontalSlider(audio.BgmVolume, 0f, 1f, GUILayout.Height(20));
                if (Mathf.Abs(newBgm - audio.BgmVolume) > 0.01f)
                {
                    audio.BgmVolume = newBgm;
                }
                GUI.backgroundColor = audio.IsBgmEnabled ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.6f, 0.2f, 0.2f);
                if (GUILayout.Button(audio.IsBgmEnabled ? "BẬT" : "TẮT", GUILayout.Width(55), GUILayout.Height(22)))
                {
                    audio.IsBgmEnabled = !audio.IsBgmEnabled;
                    audio.PlayButtonClick();
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                // SFX Volume & Toggle
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Hiệu ứng (SFX):</b> {Mathf.RoundToInt(audio.SfxVolume * 100)}%", GUILayout.Width(170));
                float newSfx = GUILayout.HorizontalSlider(audio.SfxVolume, 0f, 1f, GUILayout.Height(20));
                if (Mathf.Abs(newSfx - audio.SfxVolume) > 0.01f)
                {
                    audio.SfxVolume = newSfx;
                }
                GUI.backgroundColor = audio.IsSfxEnabled ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.6f, 0.2f, 0.2f);
                if (GUILayout.Button(audio.IsSfxEnabled ? "BẬT" : "TẮT", GUILayout.Width(55), GUILayout.Height(22)))
                {
                    audio.IsSfxEnabled = !audio.IsSfxEnabled;
                    audio.PlayButtonClick();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("🔊 Thử", GUILayout.Width(55), GUILayout.Height(22)))
                {
                    audio.PlayWaveHorn();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // ==========================================
            // Section 2: Gameplay Actions (Retry, Reset, Camera, AR)
            // ==========================================
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b><size=14><color=#00FFFF>⚔️ ĐIỀU KHIỂN TRẬN ĐẤU & CHẾ ĐỘ</color></size></b>");
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();

            // Retry Current Wave
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.4f);
            if (GUILayout.Button("<b><size=13>🔄 THỬ LẠI ĐỢT</size></b>\n<size=10>(Reset làng, giữ tháp)</size>", GUILayout.Height(46)))
            {
                IsOpen = false;
                PrehistoricGameplayManager.Instance?.RetryCurrentWave();
            }

            // Full Reset Game
            GUI.backgroundColor = new Color(0.9f, 0.55f, 0.2f);
            if (GUILayout.Button("<b><size=13>⏮️ CHƠI TỪ ĐẦU</size></b>\n<size=10>(Về Wave 1, 500g)</size>", GUILayout.Height(46)))
            {
                IsOpen = false;
                PrehistoricGameplayManager.Instance?.FullResetGame();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();

            // Toggle Camera Perspective
            GUI.backgroundColor = new Color(0.3f, 0.65f, 0.95f);
            if (GUILayout.Button("<b>📷 ĐỔI GÓC NHÌN</b>\n<size=10>(Trước ↔ Sau Làng)</size>", GUILayout.Height(42)))
            {
                CameraControl.Instance?.TogglePerspective();
            }

            // Toggle AR Mode
            bool isARActive = ARTabletopController.Instance != null && ARTabletopController.Instance.IsARModeActive;
            GUI.backgroundColor = isARActive ? new Color(0.9f, 0.3f, 0.2f) : new Color(0.1f, 0.65f, 0.95f);
            string arLabel = isARActive ? "<b>📱 THOÁT AR</b>\n<size=10>(Trở về góc 3D)</size>" : "<b>📱 BẬT CHẾ ĐỘ AR</b>\n<size=10>(Camera Pass-through)</size>";
            if (GUILayout.Button(arLabel, GUILayout.Height(42)))
            {
                ARTabletopController.Instance?.ToggleARMode();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // ==========================================
            // Section 3: Bottom Action Buttons (Resume, Quit)
            // ==========================================
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button("<b><size=15>▶ TIẾP TỤC CHƠI (RESUME)</size></b>", GUILayout.Height(48)))
            {
                IsOpen = false;
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }

            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
            if (GUILayout.Button("<b><size=15>🚪 THOÁT GAME (QUIT)</size></b>", GUILayout.Height(48), GUILayout.Width(170)))
            {
                QuitGame();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void QuitGame()
        {
            PrehistoricAudioManager.Instance?.PlayButtonClick();
            Debug.Log("<color=red>[Game]</color> Quitting application...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
