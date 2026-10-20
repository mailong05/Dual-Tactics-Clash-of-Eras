using System;
using System.IO;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Mobile;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.UI
{
    /// <summary>
    /// Giao diện Menu Chính (Main Menu) hiển thị ngay khi mới vào game.
    /// Bao gồm 4 tính năng chính:
    /// 1. Chơi mới (Bắt đầu lại từ Đợt 1)
    /// 2. Tiếp tục (Nối tiếp tiến trình đợt đã lưu)
    /// 3. Cài đặt (Tùy chỉnh âm lượng Master/BGM/SFX, góc nhìn, AR, save/load)
    /// 4. Thoát game (Lưu tiến trình và đóng ứng dụng)
    /// Hỗ trợ hình nền phong cảnh Cover 2D hoặc Cảnh 3D làng trực tiếp kèm Logo Dinos chính thức.
    /// Hoạt động mượt mà trên cả PC và Mobile (Android/iOS).
    /// </summary>
    [DefaultExecutionOrder(-8)]
    public class PrehistoricMainMenu : MonoBehaviour
    {
        public static PrehistoricMainMenu Instance { get; private set; }

        [Header("Menu State")]
        public bool IsOpen = true;
        public bool Use2DBackground = true;

        private Texture2D _coverTexture;
        private Texture2D _logoTexture;
        private Texture2D _vignetteTexture;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadTextures();
            CreateVignetteTexture();
        }

        private void Start()
        {
            if (IsOpen && PrehistoricAudioManager.Instance != null)
            {
                PrehistoricAudioManager.Instance.PlayOpeningBgm();
            }
        }

        private void LoadTextures()
        {
            // 1. Tải ảnh bìa Prehistoric Cover (Youtube Cover)
            _coverTexture = Resources.Load<Texture2D>("Textures/Prehistoric_Cover");
            if (_coverTexture == null)
            {
                string[] possibleCoverPaths = new string[]
                {
                    Path.Combine(Application.dataPath, "Resources", "Textures", "Prehistoric_Cover.png"),
                    Path.Combine(Application.dataPath, "LlamAcademy", "Dinos", "UI", "Textures", "LlamAcademy", "Youtube Cover.png"),
                    Path.Combine(Application.dataPath, "LlamAcademy", "Dinos", "UI", "Textures", "Youtube Cover.png")
                };

                foreach (string p in possibleCoverPaths)
                {
                    if (File.Exists(p))
                    {
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(p);
                            _coverTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            _coverTexture.LoadImage(bytes);
                            break;
                        }
                        catch {}
                    }
                }
            }

            // 2. Tải Logo Dinos chính thức của game
            _logoTexture = Resources.Load<Texture2D>("Textures/Dinos_Logo");
            if (_logoTexture == null)
            {
                string[] possibleLogoPaths = new string[]
                {
                    Path.Combine(Application.dataPath, "Resources", "Textures", "Dinos_Logo.png"),
                    Path.Combine(Application.dataPath, "LlamAcademy", "Dinos", "UI", "Textures", "Dinos Logo.png")
                };

                foreach (string p in possibleLogoPaths)
                {
                    if (File.Exists(p))
                    {
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(p);
                            _logoTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            _logoTexture.LoadImage(bytes);
                            break;
                        }
                        catch {}
                    }
                }
            }
        }

        private void CreateVignetteTexture()
        {
            if (_vignetteTexture == null)
            {
                _vignetteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _vignetteTexture.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.70f));
                _vignetteTexture.Apply();
            }
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Xoay nhẹ góc camera xung quanh ngôi làng nếu đang dùng hình nền 3D trực tiếp
            if (!Use2DBackground && Camera.main != null)
            {
                Camera.main.transform.RotateAround(Vector3.zero, Vector3.up, 2.5f * Time.unscaledDeltaTime);
            }

            // Nhấn ESC để đóng menu Cài đặt nếu đang mở
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                if (PrehistoricSettingsMenu.Instance != null && PrehistoricSettingsMenu.Instance.IsOpen)
                {
                    PrehistoricSettingsMenu.Instance.IsOpen = false;
                }
            }
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            // Nếu menu Cài đặt đang mở đè lên, ta tạm nhường quyền vẽ cho Settings Modal
            if (PrehistoricSettingsMenu.Instance != null && PrehistoricSettingsMenu.Instance.IsOpen)
            {
                return;
            }

            MobileUIHelper.BeginScaling();
            try
            {
                GUI.depth = -1200;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                float screenW = MobileUIHelper.ScreenWidth;
                float screenH = MobileUIHelper.ScreenHeight;

                // Đăng ký toàn màn hình để ngăn chặn mọi thao tác click xuyên vào thế giới 3D
                Rect fullScreenRect = new Rect(0, 0, screenW, screenH);
                MobileUIHelper.RegisterUIRect(fullScreenRect);

                // ==========================================
                // 1. VẼ HÌNH NỀN (BACKGROUND)
                // ==========================================
                if (Use2DBackground && _coverTexture != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(fullScreenRect, _coverTexture, ScaleMode.ScaleAndCrop);

                    // Lớp phủ màu tối điện ảnh tạo độ tương phản cao cho chữ và nút bấm
                    GUI.color = new Color(0.04f, 0.07f, 0.12f, 0.65f);
                    GUI.DrawTexture(fullScreenRect, _vignetteTexture != null ? _vignetteTexture : Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                else
                {
                    // Nền 3D trực tiếp với lớp phủ làm dịu
                    GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.50f);
                    GUI.DrawTexture(fullScreenRect, _vignetteTexture != null ? _vignetteTexture : Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                // ==========================================
                // 2. NÚT CHUYỂN ĐỔI NỀN (TOP-RIGHT)
                // ==========================================
                float toggleW = 160f;
                float toggleH = 28f;
                Rect toggleRect = new Rect(screenW - toggleW - 16f, 14f, toggleW, toggleH);
                MobileUIHelper.RegisterUIRect(toggleRect);

                GUI.backgroundColor = new Color(0.15f, 0.22f, 0.32f, 0.85f);
                string toggleLabel = Use2DBackground ? "<b>🎥 ĐỔI SANG NỀN 3D</b>" : "<b>🖼️ ĐỔI SANG NỀN 2D</b>";
                if (GUI.Button(toggleRect, $"<size=11>{toggleLabel}</size>"))
                {
                    Use2DBackground = !Use2DBackground;
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                }
                GUI.backgroundColor = Color.white;

                // ==========================================
                // 3. LOGO VÀ TIÊU ĐỀ GAME
                // ==========================================
                float logoMaxW = Mathf.Min(480f, screenW - 32f);
                float logoH = 140f;
                if (_logoTexture != null && _logoTexture.width > 0)
                {
                    float aspect = (float)_logoTexture.height / _logoTexture.width;
                    logoH = Mathf.Min(150f, logoMaxW * aspect);
                }

                float logoX = (screenW - logoMaxW) / 2f;
                float logoY = Mathf.Max(12f, screenH * 0.04f);

                if (_logoTexture != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(logoX, logoY, logoMaxW, logoH), _logoTexture, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    // Fallback Text Logo nếu chưa load được texture
                    GUI.Label(new Rect(logoX, logoY, logoMaxW, 60f),
                        "<size=28><b><color=#FFAA00>🦖 ADAPTIVE TOWER DEFENSE 3D 🦖</color></b></size>",
                        GetCenteredStyle(28, Color.yellow));
                    logoH = 60f;
                }

                // Phụ đề phong cách tiền sử hào hùng
                float subY = logoY + logoH + 2f;
                GUI.Label(new Rect(10f, subY, screenW - 20f, 26f),
                    "<size=13><b><color=#FFD700>★ CHIẾN THUẬT PHÒNG THỦ & SINH TỒN THỜI TIỀN SỬ ★</color></b></size>",
                    GetCenteredStyle(13, new Color(1f, 0.84f, 0.2f)));

                // ==========================================
                // 4. KHUNG MENU CHÍNH (4 NÚT BẤM)
                // ==========================================
                float cardW = Mathf.Min(380f, screenW - 28f);
                float cardH = 268f;
                float cardX = (screenW - cardW) / 2f;
                float cardY = subY + 28f;

                // Điều chỉnh vị trí nếu màn hình thấp (mobile ngang)
                if (cardY + cardH > screenH - 42f)
                {
                    cardY = screenH - cardH - 42f;
                }

                Rect cardRect = new Rect(cardX, cardY, cardW, cardH);
                MobileUIHelper.RegisterUIRect(cardRect);

                GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.94f);
                GUI.Box(cardRect, GUIContent.none);
                GUI.backgroundColor = Color.white;

                float padX = 16f;
                float padY = 14f;
                float innerW = cardW - padX * 2f;
                float innerH = cardH - padY * 2f;

                GUILayout.BeginArea(new Rect(cardX + padX, cardY + padY, innerW, innerH));
                GUILayout.BeginVertical();

                // Lấy thông tin Save file hiện có
                bool hasSave = PrehistoricSaveManager.Instance != null && PrehistoricSaveManager.Instance.HasSaveFile();
                int savedWave = 1;
                if (hasSave && PrehistoricSaveManager.Instance.CurrentData != null)
                {
                    savedWave = Mathf.Max(1, PrehistoricSaveManager.Instance.CurrentData.CurrentWave);
                }

                // ------------------------------------------
                // Nút 1: CHƠI MỚI (Bắt đầu từ Đợt 1)
                // ------------------------------------------
                GUI.backgroundColor = new Color(0.96f, 0.54f, 0.12f, 0.98f);
                if (GUILayout.Button("<b><size=15>🎮 CHƠI MỚI</size></b>\n<size=10><color=#FFF0D0>Bắt đầu chiến dịch từ Đợt 1</color></size>", GUILayout.Height(50)))
                {
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                    OnNewGameClicked();
                }

                GUILayout.Space(6);

                // ------------------------------------------
                // Nút 2: TIẾP TỤC (Tiếp tục đợt đã lưu)
                // ------------------------------------------
                if (hasSave && savedWave > 1)
                {
                    GUI.backgroundColor = new Color(0.20f, 0.78f, 0.38f, 0.98f);
                    string contLabel = $"<b><size=15>⏩ TIẾP TỤC (ĐỢT {savedWave})</size></b>\n<size=10><color=#E0FFE8>Tiếp nối trận chiến đang dang dở</color></size>";
                    if (GUILayout.Button(contLabel, GUILayout.Height(50)))
                    {
                        PrehistoricAudioManager.Instance?.PlayButtonClick();
                        OnContinueClicked();
                    }
                }
                else
                {
                    GUI.backgroundColor = new Color(0.35f, 0.40f, 0.45f, 0.65f);
                    string contLabel = "<b><size=15><color=#A0A0A0>⏩ TIẾP TỤC</color></size></b>\n<size=10><color=#888888>(Chưa có đợt lưu trước đó)</color></size>";
                    if (GUILayout.Button(contLabel, GUILayout.Height(50)))
                    {
                        PrehistoricAudioManager.Instance?.PlayButtonClick();
                        OnContinueClicked();
                    }
                }

                GUILayout.Space(6);

                // ------------------------------------------
                // Nút 3: CÀI ĐẶT (Settings Menu)
                // ------------------------------------------
                GUI.backgroundColor = new Color(0.20f, 0.60f, 0.90f, 0.98f);
                if (GUILayout.Button("<b><size=15>⚙️ CÀI ĐẶT</size></b>\n<size=10><color=#E0F0FF>Âm thanh, góc nhìn, AR & lưu dữ liệu</color></size>", GUILayout.Height(48)))
                {
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                    OnSettingsClicked();
                }

                GUILayout.Space(6);

                // ------------------------------------------
                // Nút 4: THOÁT GAME (Quit)
                // ------------------------------------------
                GUI.backgroundColor = new Color(0.85f, 0.22f, 0.22f, 0.98f);
                if (GUILayout.Button("<b><size=15>🚪 THOÁT GAME</size></b>\n<size=10><color=#FFD0D0>Lưu tiến trình và thoát ứng dụng</color></size>", GUILayout.Height(48)))
                {
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                    OnQuitClicked();
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndVertical();
                GUILayout.EndArea();

                // ==========================================
                // 5. THANH THÔNG TIN THỐNG KÊ Ở CHÂN MÀN HÌNH
                // ==========================================
                float footY = screenH - 32f;
                string chief = "Tù Trưởng Tiền Sử";
                int highWave = 1;
                int highScore = 0;
                if (PrehistoricSaveManager.Instance != null && PrehistoricSaveManager.Instance.CurrentData != null)
                {
                    chief = PrehistoricSaveManager.Instance.CurrentData.PlayerChiefName;
                    highWave = PrehistoricSaveManager.Instance.CurrentData.HighestWaveReached;
                    highScore = PrehistoricSaveManager.Instance.CurrentData.TotalScore;
                }

                string footText = $"<size=11><color=#C0D0E0>🏆 <b>{chief}</b> | Kỷ lục: <b>Đợt {highWave}</b> | Điểm số: <b>{highScore}</b>  •  v1.2.0 (PC & Android)</color></size>";
                GUI.Label(new Rect(10f, footY, screenW - 20f, 26f), footText, GetCenteredStyle(11, Color.white));
            }
            finally
            {
                MobileUIHelper.EndScaling();
            }
        }

        private GUIStyle GetCenteredStyle(int fontSize, Color color)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.alignment = TextAnchor.MiddleCenter;
            s.fontSize = fontSize;
            s.normal.textColor = color;
            s.richText = true;
            return s;
        }

        private void OnNewGameClicked()
        {
            IsOpen = false;
            var mgr = PrehistoricGameplayManager.Instance != null 
                ? PrehistoricGameplayManager.Instance 
                : FindFirstObjectByType<PrehistoricGameplayManager>();
            if (mgr != null)
            {
                mgr.StartNewGameFromMenu();
            }
        }

        private void OnContinueClicked()
        {
            IsOpen = false;
            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.ContinueGameFromMenu();
            }
        }

        private void OnSettingsClicked()
        {
            if (PrehistoricSettingsMenu.Instance != null)
            {
                PrehistoricSettingsMenu.Instance.IsOpen = true;
            }
        }

        private void OnQuitClicked()
        {
            PrehistoricSaveManager.Instance?.AutoSaveCurrentRun();
            Debug.Log("<color=red>[Prehistoric TD]</color> Người chơi đã chọn Thoát Game từ Menu Chính.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
