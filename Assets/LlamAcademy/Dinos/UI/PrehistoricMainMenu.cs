using System;
using System.IO;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Environment;
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
        public PrehistoricGameMode SelectedMode = PrehistoricGameMode.TowerDefense;
        public PrehistoricMapType SelectedMap = PrehistoricMapType.AncientValley;

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
            PrehistoricSaveLoadDialog.EnsureInstance();
        }

        private void Start()
        {
            SelectedMap = PrehistoricMapManager.CurrentMap;
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

            // Nhấn ESC để đóng menu Cài đặt hoặc Hộp thoại Lưu/Tải nếu đang mở
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                if (PrehistoricSaveLoadDialog.Instance != null && PrehistoricSaveLoadDialog.Instance.IsOpen)
                {
                    PrehistoricSaveLoadDialog.Instance.CloseDialog();
                }
                else if (PrehistoricSettingsMenu.Instance != null && PrehistoricSettingsMenu.Instance.IsOpen)
                {
                    PrehistoricSettingsMenu.Instance.IsOpen = false;
                }
            }
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            // Kiểm tra xem có Modal phụ nào (Cài đặt hoặc Lưu/Tải game) đang mở đè lên không
            bool isSubModalOpen = (PrehistoricSettingsMenu.Instance != null && PrehistoricSettingsMenu.Instance.IsOpen)
                || (PrehistoricSaveLoadDialog.Instance != null && PrehistoricSaveLoadDialog.Instance.IsOpen);

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

                // Nếu Modal phụ (Cài đặt hoặc Danh sách Lưu/Tải game) đang mở:
                // Ta chỉ hiển thị hình nền và nhượng toàn bộ quyền tương tác phía trên cho Modal
                if (isSubModalOpen)
                {
                    return;
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
                // 3. TÍNH TOÁN KÍCH THƯỚC ĐÁP ỨNG TOÀN KHUNG HÌNH (RESPONSIVE PC & MOBILE)
                // ==========================================
                // Tỉ lệ co giãn linh hoạt theo chiều cao màn hình thực tế (chuẩn hóa trên 1080p, 2K, 4K, 720p, mobile)
                float scale = Mathf.Clamp(screenH / 880f, 0.90f, 1.75f);

                // Kích thước Logo
                float logoMaxW = Mathf.Clamp(screenW * 0.38f, 380f, 640f * scale);
                float logoH = 135f * scale;
                if (_logoTexture != null && _logoTexture.width > 0)
                {
                    float aspect = (float)_logoTexture.height / _logoTexture.width;
                    logoH = Mathf.Clamp(logoMaxW * aspect, 105f * scale, 175f * scale);
                }

                // Kích thước Card Menu chính (mở rộng thoải mái trên PC 1080p+, vừa vặn màn hình)
                float cardW = Mathf.Clamp(screenW * 0.44f, 520f, 720f * scale);
                cardW = Mathf.Min(cardW, screenW - 24f);

                float cardH = Mathf.Clamp(590f * scale, 520f, screenH - 60f);

                // Căn giữa toàn bộ cụm Logo + Phụ đề + Card Menu theo chiều dọc màn hình
                float subH = 28f * scale;
                float gapLogoSub = 6f * scale;
                float gapSubCard = 16f * scale;
                float totalContentH = logoH + gapLogoSub + subH + gapSubCard + cardH;

                float startY = Mathf.Max(16f, (screenH - totalContentH - 34f) / 2f);
                float logoX = (screenW - logoMaxW) / 2f;
                float logoY = startY;

                if (_logoTexture != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(logoX, logoY, logoMaxW, logoH), _logoTexture, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    GUI.Label(new Rect(logoX, logoY, logoMaxW, 60f * scale),
                        $"<size={Mathf.RoundToInt(28 * scale)}><b><color=#FFAA00>🦖 ADAPTIVE TOWER DEFENSE 3D 🦖</color></b></size>",
                        GetCenteredStyle(Mathf.RoundToInt(28 * scale), Color.yellow));
                    logoH = 60f * scale;
                }

                // Phụ đề phong cách tiền sử
                float subY = logoY + logoH + gapLogoSub;
                GUI.Label(new Rect(10f, subY, screenW - 20f, subH),
                    $"<size={Mathf.RoundToInt(14 * scale)}><b><color=#FFD700>★ CHIẾN THUẬT PHÒNG THỦ & SINH TỒN THỜI TIỀN SỬ ★</color></b></size>",
                    GetCenteredStyle(Mathf.RoundToInt(14 * scale), new Color(1f, 0.84f, 0.2f)));

                // ==========================================
                // 4. KHUNG MENU CHÍNH (BỘ CHỌN CHẾ ĐỘ + NÚT BẤM CỠ LỚN SANG TRỌNG)
                // ==========================================
                float cardX = (screenW - cardW) / 2f;
                float cardY = subY + subH + gapSubCard;

                if (cardY + cardH > screenH - 36f)
                {
                    cardY = screenH - cardH - 36f;
                }

                Rect cardRect = new Rect(cardX, cardY, cardW, cardH);
                MobileUIHelper.RegisterUIRect(cardRect);

                GUI.backgroundColor = new Color(0.06f, 0.10f, 0.16f, 0.96f);
                GUI.Box(cardRect, GUIContent.none);
                GUI.backgroundColor = Color.white;

                float padX = 18f * scale;
                float padY = 16f * scale;
                float innerW = cardW - padX * 2f;
                float innerH = cardH - padY * 2f;

                GUILayout.BeginArea(new Rect(cardX + padX, cardY + padY, innerW, innerH));
                GUILayout.BeginVertical();

                // ------------------------------------------
                // TAB CHỌN CHẾ ĐỘ CHƠI (THỦ THÀNH vs CÔNG THÀNH)
                // ------------------------------------------
                GUILayout.Label($"<size={Mathf.RoundToInt(13 * scale)}><b><color=#FFD700>⚔️ LỰA CHỌN CHẾ ĐỘ CHIẾN DỊCH:</color></b></size>", GUI.skin.label);
                GUILayout.BeginHorizontal();

                bool isDefense = (SelectedMode == PrehistoricGameMode.TowerDefense);
                bool isAssault = (SelectedMode == PrehistoricGameMode.DinoAssault);
                float tabBtnH = Mathf.Round(48f * scale);

                GUI.backgroundColor = isDefense ? new Color(0.18f, 0.75f, 0.95f, 1f) : new Color(0.20f, 0.25f, 0.32f, 0.70f);
                string defTabLabel = isDefense 
                    ? $"<b><size={Mathf.RoundToInt(14 * scale)}>🛡️ THỦ THÀNH</size></b>\n<size={Mathf.RoundToInt(11 * scale)}>(Đang chọn)</size>"
                    : $"<size={Mathf.RoundToInt(14 * scale)}>🛡️ THỦ THÀNH</size>\n<size={Mathf.RoundToInt(11 * scale)}>(Bảo vệ Làng)</size>";
                if (GUILayout.Button(defTabLabel, GUILayout.Height(tabBtnH)))
                {
                    SelectedMode = PrehistoricGameMode.TowerDefense;
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                }

                GUI.backgroundColor = isAssault ? new Color(0.95f, 0.45f, 0.15f, 1f) : new Color(0.20f, 0.25f, 0.32f, 0.70f);
                string assTabLabel = isAssault
                    ? $"<b><size={Mathf.RoundToInt(14 * scale)}>🦖 CÔNG THÀNH</size></b>\n<size={Mathf.RoundToInt(11 * scale)}>(Đang chọn)</size>"
                    : $"<size={Mathf.RoundToInt(14 * scale)}>🦖 CÔNG THÀNH</size>\n<size={Mathf.RoundToInt(11 * scale)}>(Chỉ huy Khủng long)</size>";
                if (GUILayout.Button(assTabLabel, GUILayout.Height(tabBtnH)))
                {
                    SelectedMode = PrehistoricGameMode.DinoAssault;
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                GUILayout.Space(6f * scale);

                // ------------------------------------------
                // TAB CHỌN BẢN ĐỒ CHIẾN TRƯỜNG (MAP SELECTION)
                // ------------------------------------------
                GUILayout.Label($"<size={Mathf.RoundToInt(13 * scale)}><b><color=#FFD700>🗺️ LỰA CHỌN BẢN ĐỒ CHIẾN TRƯỜNG:</color></b></size>", GUI.skin.label);
                GUILayout.BeginHorizontal();

                bool isValley = (SelectedMap == PrehistoricMapType.AncientValley);
                bool isVolcanic = (SelectedMap == PrehistoricMapType.VolcanicGorge);
                float mapBtnH = Mathf.Round(44f * scale);

                GUI.backgroundColor = isValley ? new Color(0.18f, 0.75f, 0.45f, 1f) : new Color(0.20f, 0.25f, 0.32f, 0.70f);
                string valleyLabel = isValley
                    ? $"<b><size={Mathf.RoundToInt(13 * scale)}>🏞️ THUNG LŨNG CỔ ĐẠI</size></b>\n<size={Mathf.RoundToInt(10 * scale)}>(Đang chọn)</size>"
                    : $"<size={Mathf.RoundToInt(13 * scale)}>🏞️ THUNG LŨNG CỔ ĐẠI</size>\n<size={Mathf.RoundToInt(10 * scale)}>(Bản đồ Rừng đá)</size>";
                if (GUILayout.Button(valleyLabel, GUILayout.Height(mapBtnH)))
                {
                    SelectedMap = PrehistoricMapType.AncientValley;
                    PrehistoricMapManager.ApplyMapTheme(PrehistoricMapType.AncientValley);
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                }

                GUI.backgroundColor = isVolcanic ? new Color(0.95f, 0.40f, 0.12f, 1f) : new Color(0.20f, 0.25f, 0.32f, 0.70f);
                string volcanicLabel = isVolcanic
                    ? $"<b><size={Mathf.RoundToInt(13 * scale)}>🌋 HẺM NÚI DUNG NHAM</size></b>\n<size={Mathf.RoundToInt(10 * scale)}>(Đang chọn)</size>"
                    : $"<size={Mathf.RoundToInt(13 * scale)}>🌋 HẺM NÚI DUNG NHAM</size>\n<size={Mathf.RoundToInt(10 * scale)}>(Bản đồ Núi lửa mới)</size>";
                if (GUILayout.Button(volcanicLabel, GUILayout.Height(mapBtnH)))
                {
                    SelectedMap = PrehistoricMapType.VolcanicGorge;
                    PrehistoricMapManager.ApplyMapTheme(PrehistoricMapType.VolcanicGorge);
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                GUILayout.Space(6f * scale);

                // Thông tin tiến trình của chế độ đang chọn
                bool hasActiveSave = false;
                string modeProgressInfo = "";

                if (isDefense)
                {
                    hasActiveSave = PrehistoricSaveManager.Instance != null && PrehistoricSaveManager.Instance.HasActiveDefenseSave();
                    int curWave = 1;
                    int curGold = 500;
                    if (PrehistoricSaveManager.Instance != null && PrehistoricSaveManager.Instance.CurrentData.DefenseProfile != null)
                    {
                        curWave = PrehistoricSaveManager.Instance.CurrentData.DefenseProfile.CurrentWave;
                        curGold = PrehistoricSaveManager.Instance.CurrentData.DefenseProfile.Gold;
                    }
                    if (curWave >= 10 && !hasActiveSave)
                    {
                        modeProgressInfo = "🏆 <b>ĐÃ PHÁ ĐẢO HOÀN THÀNH TRÒ CHƠI (MÀN 10)</b>";
                    }
                    else
                    {
                        modeProgressInfo = hasActiveSave 
                            ? $"Tiến trình: <b>Đợt {curWave}</b> (Vàng: <b>{curGold}g</b>)"
                            : "Chưa có trận chiến dang dở";
                    }
                }
                else
                {
                    hasActiveSave = PrehistoricSaveManager.Instance != null && PrehistoricSaveManager.Instance.HasActiveAssaultSave();
                    int curLevel = 1;
                    int curMeat = 250;
                    if (PrehistoricSaveManager.Instance != null && PrehistoricSaveManager.Instance.CurrentData.AssaultProfile != null)
                    {
                        curLevel = PrehistoricSaveManager.Instance.CurrentData.AssaultProfile.AssaultLevel;
                        curMeat = PrehistoricSaveManager.Instance.CurrentData.AssaultProfile.Meat;
                    }
                    modeProgressInfo = hasActiveSave 
                        ? $"Tiến trình: <b>Màn {curLevel}</b> (Thịt: <b>{curMeat}</b>)"
                        : "Chưa có trận chiến dang dở";
                }

                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"<size={Mathf.RoundToInt(12 * scale)}><color=#E0EEFF>📌 {modeProgressInfo}</color></size>", GUI.skin.label);
                GUILayout.EndHorizontal();

                GUILayout.Space(8f * scale);

                float mainBtnH = Mathf.Round(56f * scale);

                // ------------------------------------------
                // Nút 1: CHƠI MỚI (New Game)
                // ------------------------------------------
                GUI.backgroundColor = new Color(0.96f, 0.54f, 0.12f, 0.98f);
                string newGameLabel = isDefense 
                    ? $"<b><size={Mathf.RoundToInt(16 * scale)}>🎮 CHƠI MỚI THỦ THÀNH</size></b>\n<size={Mathf.RoundToInt(11 * scale)}><color=#FFF0D0>Bắt đầu chiến dịch từ Đợt 1</color></size>"
                    : $"<b><size={Mathf.RoundToInt(16 * scale)}>🎮 CHƠI MỚI CÔNG THÀNH</size></b>\n<size={Mathf.RoundToInt(11 * scale)}><color=#FFF0D0>Bắt đầu chiếm làng từ Màn 1</color></size>";

                if (GUILayout.Button(newGameLabel, GUILayout.Height(mainBtnH)))
                {
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                    OnNewGameClicked();
                }

                GUILayout.Space(6f * scale);

                // ------------------------------------------
                // Nút 2: TIẾP TỤC (Continue)
                // ------------------------------------------
                if (hasActiveSave)
                {
                    GUI.backgroundColor = new Color(0.20f, 0.78f, 0.38f, 0.98f);
                    string contLabel = isDefense
                        ? $"<b><size={Mathf.RoundToInt(16 * scale)}>⏩ TIẾP TỤC (ĐỢT {PrehistoricSaveManager.Instance.CurrentData.DefenseProfile.CurrentWave})</size></b>\n<size={Mathf.RoundToInt(11 * scale)}><color=#E0FFE8>Tiếp nối chiến dịch Thủ Thành</color></size>"
                        : $"<b><size={Mathf.RoundToInt(16 * scale)}>⏩ TIẾP TỤC (MÀN {PrehistoricSaveManager.Instance.CurrentData.AssaultProfile.AssaultLevel})</size></b>\n<size={Mathf.RoundToInt(11 * scale)}><color=#E0FFE8>Tiếp nối chiến dịch Công Thành</color></size>";

                    if (GUILayout.Button(contLabel, GUILayout.Height(mainBtnH)))
                    {
                        PrehistoricAudioManager.Instance?.PlayButtonClick();
                        OnContinueClicked();
                    }
                }
                else
                {
                    GUI.backgroundColor = new Color(0.35f, 0.40f, 0.45f, 0.65f);
                    string contLabel = isDefense
                        ? $"<b><size={Mathf.RoundToInt(16 * scale)}><color=#A0A0A0>⏩ TIẾP TỤC THỦ THÀNH</color></size></b>\n<size={Mathf.RoundToInt(11 * scale)}><color=#888888>(Chưa có đợt lưu trước đó)</color></size>"
                        : $"<b><size={Mathf.RoundToInt(16 * scale)}><color=#A0A0A0>⏩ TIẾP TỤC CÔNG THÀNH</color></size></b>\n<size={Mathf.RoundToInt(11 * scale)}><color=#888888>(Chưa có màn lưu trước đó)</color></size>";

                    if (GUILayout.Button(contLabel, GUILayout.Height(mainBtnH)))
                    {
                        PrehistoricAudioManager.Instance?.PlayButtonClick();
                        OnContinueClicked();
                    }
                }

                GUILayout.Space(6f * scale);

                // ------------------------------------------
                // Nút 3: TẢI GAME (Load Game / Danh Sách Lưu)
                // ------------------------------------------
                GUI.backgroundColor = new Color(0.20f, 0.65f, 0.95f, 0.98f);
                string loadLabel = $"<b><size={Mathf.RoundToInt(15 * scale)}>📂 TẢI GAME (DANH SÁCH LƯU)</size></b>\n<size={Mathf.RoundToInt(11 * scale)}><color=#E0F2FE>Chọn màn chơi từ các file đã lưu</color></size>";

                if (GUILayout.Button(loadLabel, GUILayout.Height(mainBtnH * 0.90f)))
                {
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                    PrehistoricSaveLoadDialog.EnsureInstance().OpenLoadDialog(isDefense ? PrehistoricGameMode.TowerDefense : PrehistoricGameMode.DinoAssault);
                }

                GUILayout.Space(6f * scale);

                // ------------------------------------------
                // Hàng nút Cài đặt & Thoát game
                // ------------------------------------------
                float subBtnH = Mathf.Round(46f * scale);
                GUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.20f, 0.60f, 0.90f, 0.98f);
                if (GUILayout.Button($"<b><size={Mathf.RoundToInt(14 * scale)}>⚙️ CÀI ĐẶT</size></b>", GUILayout.Height(subBtnH)))
                {
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                    OnSettingsClicked();
                }

                GUI.backgroundColor = new Color(0.85f, 0.22f, 0.22f, 0.98f);
                if (GUILayout.Button($"<b><size={Mathf.RoundToInt(14 * scale)}>🚪 THOÁT GAME</size></b>", GUILayout.Height(subBtnH)))
                {
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                    OnQuitClicked();
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

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
            PrehistoricMapManager.LoadMap(SelectedMap, () =>
            {
                if (SelectedMode == PrehistoricGameMode.TowerDefense)
                {
                    var gmm = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance : FindFirstObjectByType<PrehistoricGameModeManager>();
                    gmm?.SetGameMode(PrehistoricGameMode.TowerDefense);

                    var mgr = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance : FindFirstObjectByType<PrehistoricGameplayManager>();
                    if (mgr != null)
                    {
                        mgr.StartNewDefenseGameFromMenu();
                    }
                }
                else
                {
                    var gmm = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance : FindFirstObjectByType<PrehistoricGameModeManager>();
                    if (gmm != null)
                    {
                        gmm.StartNewAssaultFromMenu();
                    }
                }
            });
        }

        private void OnContinueClicked()
        {
            IsOpen = false;
            PrehistoricMapManager.LoadMap(SelectedMap, () =>
            {
                if (SelectedMode == PrehistoricGameMode.TowerDefense)
                {
                    var gmm = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance : FindFirstObjectByType<PrehistoricGameModeManager>();
                    gmm?.SetGameMode(PrehistoricGameMode.TowerDefense);

                    var mgr = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance : FindFirstObjectByType<PrehistoricGameplayManager>();
                    if (mgr != null)
                    {
                        mgr.ContinueDefenseFromMenu();
                    }
                }
                else
                {
                    var gmm = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance : FindFirstObjectByType<PrehistoricGameModeManager>();
                    if (gmm != null)
                    {
                        gmm.ContinueAssaultFromMenu();
                    }
                }
            });
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
