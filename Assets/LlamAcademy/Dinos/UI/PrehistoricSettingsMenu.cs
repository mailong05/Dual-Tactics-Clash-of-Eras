using LlamAcademy.Dinos.AR;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Mobile;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.UI
{
    /// <summary>
    /// In-Game Settings and Options Menu.
    /// Provides audio configuration (Master, BGM, SFX), game controls (Retry, Reset, Camera perspective, AR toggle),
    /// progression save management (Local auto-save & optional cloud sync), and quit functionality across PC and Mobile.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class PrehistoricSettingsMenu : MonoBehaviour
    {
        public static PrehistoricSettingsMenu Instance { get; private set; }

        public bool IsOpen { get; set; } = false;

        private Vector2 _scrollPos = Vector2.zero;
        private string _chiefNameInput = "";
        private string _saveFeedbackMsg = "";
        private float _saveFeedbackTimer = 0f;

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

        private void Start()
        {
            if (PrehistoricSaveManager.Instance != null)
            {
                _chiefNameInput = PrehistoricSaveManager.Instance.CurrentData.PlayerChiefName;
            }
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

            if (_saveFeedbackTimer > 0f)
            {
                _saveFeedbackTimer -= Time.unscaledDeltaTime;
                if (_saveFeedbackTimer <= 0f)
                {
                    _saveFeedbackMsg = "";
                }
            }
        }

        public void ToggleMenu()
        {
            IsOpen = !IsOpen;
            if (IsOpen && PrehistoricSaveManager.Instance != null)
            {
                _chiefNameInput = PrehistoricSaveManager.Instance.CurrentData.PlayerChiefName;
            }
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
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, MobileUIHelper.ScreenWidth, MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float modalW = Mathf.Min(640f, MobileUIHelper.ScreenWidth - 20f);
            float modalH = Mathf.Min(560f, MobileUIHelper.ScreenHeight - 16f);
            float x = (MobileUIHelper.ScreenWidth - modalW) / 2f;
            float y = (MobileUIHelper.ScreenHeight - modalH) / 2f;

            Rect modalRect = new Rect(x, y, modalW, modalH);
            MobileUIHelper.RegisterUIRect(modalRect);

            GUI.backgroundColor = new Color(0.10f, 0.14f, 0.20f, 0.98f);
            GUI.Box(modalRect, GUIContent.none);

            float padX = 14f;
            float padY = 10f;
            float contentW = modalW - padX * 2f;
            float contentH = modalH - padY * 2f;

            GUILayout.BeginArea(new Rect(x + padX, y + padY, contentW, contentH));
            GUILayout.BeginVertical();

            // Header (fixed height)
            float headerH = 34f;
            GUILayout.BeginHorizontal(GUILayout.Height(headerH));
            GUILayout.Label("<b><size=18><color=#FFD700>⚙️ CÀI ĐẶT & TÙY CHỌN TRÒ CHƠI</color></size></b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(34), GUILayout.Height(26)))
            {
                IsOpen = false;
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Scrollable area dynamically sized to leave clean space for bottom buttons
            float bottomBtnsH = 42f;
            float scrollH = Mathf.Max(100f, contentH - headerH - bottomBtnsH - 16f);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(scrollH));

            // ==========================================
            // Section 1: Audio Controls (Master, BGM, SFX)
            // ==========================================
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b><size=13><color=#55FF55>🔊 CÀI ĐẶT ÂM THANH & NHẠC NỀN</color></size></b>");
            GUILayout.Space(2);

            var audio = PrehistoricAudioManager.Instance;
            if (audio != null)
            {
                // Master Volume
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Âm lượng Tổng:</b> {Mathf.RoundToInt(audio.MasterVolume * 100)}%", GUILayout.Width(165));
                float newMaster = GUILayout.HorizontalSlider(audio.MasterVolume, 0f, 1f, GUILayout.Height(18));
                if (Mathf.Abs(newMaster - audio.MasterVolume) > 0.01f)
                {
                    audio.MasterVolume = newMaster;
                }
                GUILayout.EndHorizontal();

                // BGM Volume & Toggle
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Nhạc nền (BGM):</b> {Mathf.RoundToInt(audio.BgmVolume * 100)}%", GUILayout.Width(165));
                float newBgm = GUILayout.HorizontalSlider(audio.BgmVolume, 0f, 1f, GUILayout.Height(18));
                if (Mathf.Abs(newBgm - audio.BgmVolume) > 0.01f)
                {
                    audio.BgmVolume = newBgm;
                }
                GUI.backgroundColor = audio.IsBgmEnabled ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.6f, 0.2f, 0.2f);
                if (GUILayout.Button(audio.IsBgmEnabled ? "BẬT" : "TẮT", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    audio.IsBgmEnabled = !audio.IsBgmEnabled;
                    audio.PlayButtonClick();
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                // SFX Volume & Toggle
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Hiệu ứng (SFX):</b> {Mathf.RoundToInt(audio.SfxVolume * 100)}%", GUILayout.Width(165));
                float newSfx = GUILayout.HorizontalSlider(audio.SfxVolume, 0f, 1f, GUILayout.Height(18));
                if (Mathf.Abs(newSfx - audio.SfxVolume) > 0.01f)
                {
                    audio.SfxVolume = newSfx;
                }
                GUI.backgroundColor = audio.IsSfxEnabled ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.6f, 0.2f, 0.2f);
                if (GUILayout.Button(audio.IsSfxEnabled ? "BẬT" : "TẮT", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    audio.IsSfxEnabled = !audio.IsSfxEnabled;
                    audio.PlayButtonClick();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("🔊 Thử", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    audio.PlayWaveHorn();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // ==========================================
            // Section 2: Save Progression & Cloud Sync
            // ==========================================
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b><size=13><color=#FFD700>💾 LƯU TIẾN TRÌNH & ĐỒNG BỘ CLOUD</color></size></b>");
            GUILayout.Space(2);

            var saveMgr = PrehistoricSaveManager.Instance;
            if (saveMgr != null)
            {
                var data = saveMgr.CurrentData;

                // Player chief name edit
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Tên Tù Trưởng:</b>", GUILayout.Width(110));
                string editedName = GUILayout.TextField(_chiefNameInput, 20, GUILayout.Height(22));
                if (editedName != _chiefNameInput)
                {
                    _chiefNameInput = editedName;
                    saveMgr.SetPlayerChiefName(_chiefNameInput);
                }
                GUILayout.EndHorizontal();

                // Stats overview
                GUILayout.Label($"<size=11>🏆 Đợt cao nhất: <b>Đợt {data.HighestWaveReached}</b> | Điểm kỷ lục: <b>{data.TotalScore}</b> | Quái đã diệt: <b>{data.TotalMonstersKilled}</b></size>");
                if (!string.IsNullOrEmpty(data.LastSavedTimestamp))
                {
                    GUILayout.Label($"<size=10><color=#A0A0A0>🕒 Lần lưu gần nhất: {data.LastSavedTimestamp}</color></size>");
                }

                if (!string.IsNullOrEmpty(_saveFeedbackMsg))
                {
                    GUILayout.Label($"<size=11><color=#00FFAA><b>{_saveFeedbackMsg}</b></color></size>");
                }

                GUILayout.Space(3);
                GUILayout.BeginHorizontal();

                // Quick Save Local
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.9f);
                if (GUILayout.Button("<b>💾 LƯU MÁY (LOCAL)</b>", GUILayout.Height(28)))
                {
                    saveMgr.AutoSaveCurrentRun(hasActiveRun: true);
                    _saveFeedbackMsg = "✓ Đã lưu thành công vào bộ nhớ máy (Local JSON)!";
                    _saveFeedbackTimer = 3.0f;
                    audio?.PlayButtonClick();
                }

                // Cloud Sync
                GUI.backgroundColor = new Color(0.25f, 0.8f, 0.45f);
                if (GUILayout.Button("<b>☁ ĐỒNG BỘ CLOUD</b>", GUILayout.Height(28)))
                {
                    saveMgr.SyncToCloud((success, msg) =>
                    {
                        _saveFeedbackMsg = msg;
                        _saveFeedbackTimer = 3.5f;
                    });
                    audio?.PlayButtonClick();
                }

                // Reset Save
                GUI.backgroundColor = new Color(0.7f, 0.25f, 0.25f);
                if (GUILayout.Button("<b>🗑 XÓA SAVE</b>", GUILayout.Width(85), GUILayout.Height(28)))
                {
                    saveMgr.ResetSaveData();
                    _saveFeedbackMsg = "Đã đặt lại dữ liệu lưu tiến trình.";
                    _saveFeedbackTimer = 3.0f;
                    audio?.PlayButtonClick();
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // ==========================================
            // Section 3: Gameplay Actions (Retry, Reset, Camera, AR)
            // ==========================================
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b><size=13><color=#00FFFF>⚔️ ĐIỀU KHIỂN TRẬN ĐẤU & GÓC NHÌN</color></size></b>");
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();

            // Retry Current Wave / Level
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.4f);
            bool isAssault = PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault;
            string retryTitle = isAssault ? "<b><size=12>🔄 THỬ LẠI MÀN</size></b>\n<size=10>(Màn hiện tại)</size>" : "<b><size=12>🔄 THỬ LẠI ĐỢT</size></b>\n<size=10>(Reset làng, giữ tháp)</size>";
            if (GUILayout.Button(retryTitle, GUILayout.Height(42)))
            {
                IsOpen = false;
                if (isAssault)
                {
                    PrehistoricGameModeManager.Instance?.RetryCurrentAssaultLevel();
                }
                else
                {
                    PrehistoricGameplayManager.Instance?.RetryCurrentWave();
                }
            }

            // Full Reset Game
            GUI.backgroundColor = new Color(0.9f, 0.55f, 0.2f);
            string resetTitle = isAssault ? "<b><size=12>⏮️ CHƠI TỪ ĐẦU</size></b>\n<size=10>(Về Màn 1)</size>" : "<b><size=12>⏮️ CHƠI TỪ ĐẦU</size></b>\n<size=10>(Về Wave 1, 500g)</size>";
            if (GUILayout.Button(resetTitle, GUILayout.Height(42)))
            {
                IsOpen = false;
                if (isAssault)
                {
                    PrehistoricGameModeManager.Instance?.RestartAssaultFromLevel1();
                }
                else
                {
                    PrehistoricGameplayManager.Instance?.FullResetGame();
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();

            // Toggle Camera Perspective
            GUI.backgroundColor = new Color(0.3f, 0.65f, 0.95f);
            if (GUILayout.Button("<b>📷 ĐỔI GÓC NHÌN</b>\n<size=10>(Trước ↔ Sau Làng)</size>", GUILayout.Height(38)))
            {
                CameraControl.Instance?.TogglePerspective();
            }

            // Toggle AR Mode
            bool isARActive = ARTabletopController.Instance != null && ARTabletopController.Instance.IsARModeActive;
            GUI.backgroundColor = isARActive ? new Color(0.9f, 0.3f, 0.2f) : new Color(0.1f, 0.65f, 0.95f);
            string arLabel = isARActive ? "<b>📱 THOÁT AR</b>\n<size=10>(Trở về góc 3D)</size>" : "<b>📱 BẬT CHẾ ĐỘ AR</b>\n<size=10>(Camera Pass-through)</size>";
            if (GUILayout.Button(arLabel, GUILayout.Height(38)))
            {
                ARTabletopController.Instance?.ToggleARMode();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            GUILayout.Space(6);

            // ==========================================
            // Section 4: Bottom Action Buttons (Resume, Quit)
            // ==========================================
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button("<b><size=14>▶ TIẾP TỤC CHƠI (RESUME)</size></b>", GUILayout.Height(42)))
            {
                IsOpen = false;
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }

            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
            if (GUILayout.Button("<b><size=14>🚪 THOÁT GAME (QUIT)</size></b>", GUILayout.Height(42), GUILayout.Width(160)))
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
            PrehistoricSaveManager.Instance?.AutoSaveCurrentRun();
            Debug.Log("<color=red>[Game]</color> Quitting application...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
