using System;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Cloud;
using LlamAcademy.Dinos.Mobile;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Save;
using UnityEngine;

namespace LlamAcademy.Dinos.UI
{
    public enum CloudDialogTab
    {
        Account,
        WebDashboard,
        Leaderboard
    }

    /// <summary>
    /// Interactive Cloud Account, Cross-Device Synchronization & Web Dashboard Dialog.
    /// Allows players to:
    /// 1. Register or Log In with a Prehistoric Chief Username & PIN.
    /// 2. Synchronize progress (Wave, Gold/Meat, Unlocked Towers) across PCs & Mobile devices.
    /// 3. One-click open the live real-time HTML5 Web Dashboard in system browser for evaluator demonstration.
    /// 4. View global cloud leaderboards and server health.
    /// </summary>
    [DefaultExecutionOrder(-3)]
    public class PrehistoricCloudAccountDialog : MonoBehaviour
    {
        public static PrehistoricCloudAccountDialog Instance { get; private set; }

        public bool IsOpen { get; private set; } = false;
        private CloudDialogTab _activeTab = CloudDialogTab.Account;

        // Form Fields
        private string _inputUsername = "";
        private string _inputPin = "";
        private string _inputDisplayName = "";
        private string _feedbackMessage = "";
        private Color _feedbackColor = Color.white;
        private float _feedbackTimer = 0f;
        private Vector2 _scrollPos = Vector2.zero;

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

        public static PrehistoricCloudAccountDialog EnsureInstance()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("PrehistoricCloudAccountDialog");
                Instance = go.AddComponent<PrehistoricCloudAccountDialog>();
            }
            return Instance;
        }

        public bool IsFirstRunPrompt { get; private set; } = false;

        public void OpenDialog(CloudDialogTab tab = CloudDialogTab.Account, bool isFirstRunPrompt = false)
        {
            _activeTab = tab;
            IsFirstRunPrompt = isFirstRunPrompt;
            IsOpen = true;
            _feedbackMessage = "";
            _scrollPos = Vector2.zero;

            if (IOCloudManager.Instance != null && IOCloudManager.Instance.IsLoggedIn)
            {
                _inputUsername = IOCloudManager.Instance.CurrentUsername;
                _inputDisplayName = IOCloudManager.Instance.CurrentDisplayName;
            }
            else if (string.IsNullOrEmpty(_inputUsername))
            {
                _inputUsername = "chief_" + UnityEngine.Random.Range(100, 999);
                _inputDisplayName = "Tù Trưởng Tiền Sử";
                _inputPin = "1234";
            }

            PrehistoricAudioManager.Instance?.PlayButtonClick();
        }

        public void CloseDialog()
        {
            IsOpen = false;
            _feedbackMessage = "";
            PrehistoricAudioManager.Instance?.PlayButtonClick();
        }

        private void SetFeedback(string msg, bool isSuccess)
        {
            _feedbackMessage = msg;
            _feedbackColor = isSuccess ? new Color(0.2f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            _feedbackTimer = 6.0f;
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (_feedbackTimer > 0f)
            {
                _feedbackTimer -= Time.unscaledDeltaTime;
                if (_feedbackTimer <= 0f)
                {
                    _feedbackMessage = "";
                }
            }
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            GUI.depth = -1600;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            MobileUIHelper.BeginScaling();
            try
            {
                DrawBackdrop();
                DrawMainWindow();
            }
            finally
            {
                MobileUIHelper.EndScaling();
            }
        }

        private void DrawBackdrop()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, MobileUIHelper.ScreenWidth, MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawMainWindow()
        {
            float screenW = MobileUIHelper.ScreenWidth;
            float screenH = MobileUIHelper.ScreenHeight;

            float winW = Mathf.Clamp(screenW * 0.80f, 540f, 880f);
            winW = Mathf.Min(winW, screenW - 16f);
            float winH = Mathf.Clamp(screenH * 0.86f, 480f, 720f);
            winH = Mathf.Min(winH, screenH - 20f);

            float x = (screenW - winW) / 2f;
            float y = (screenH - winH) / 2f;

            Rect winRect = new Rect(x, y, winW, winH);
            MobileUIHelper.RegisterUIRect(winRect);

            // Window background
            GUI.backgroundColor = new Color(0.06f, 0.10f, 0.16f, 0.98f);
            GUI.Box(winRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(x + 18, y + 16, winW - 36, winH - 32));
            GUILayout.BeginVertical();

            // 1. Header Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=18><b>☁️ HỆ THỐNG TÀI KHOẢN CLOUD & ĐỒNG BỘ ĐA THIẾT BỊ</b></size>");
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 0.95f);
            if (GUILayout.Button("<b>✕ ĐÓNG</b>", GUILayout.Width(85), GUILayout.Height(30)))
            {
                CloseDialog();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 2. Tab Navigation
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = _activeTab == CloudDialogTab.Account ? new Color(0.15f, 0.65f, 0.95f, 1f) : new Color(0.2f, 0.25f, 0.35f, 0.75f);
            if (GUILayout.Button("<b>👤 TÀI KHOẢN TÙ TRƯỞNG</b>", GUILayout.Height(36)))
            {
                _activeTab = CloudDialogTab.Account;
                _scrollPos = Vector2.zero;
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }

            GUI.backgroundColor = _activeTab == CloudDialogTab.WebDashboard ? new Color(0.65f, 0.35f, 0.95f, 1f) : new Color(0.2f, 0.25f, 0.35f, 0.75f);
            if (GUILayout.Button("<b>🌐 WEB DASHBOARD & GIÁM SÁT</b>", GUILayout.Height(36)))
            {
                _activeTab = CloudDialogTab.WebDashboard;
                _scrollPos = Vector2.zero;
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }

            GUI.backgroundColor = _activeTab == CloudDialogTab.Leaderboard ? new Color(0.95f, 0.60f, 0.15f, 1f) : new Color(0.2f, 0.25f, 0.35f, 0.75f);
            if (GUILayout.Button("<b>🏆 BẢNG XẾP HẠNG TOÀN CẦU</b>", GUILayout.Height(36)))
            {
                _activeTab = CloudDialogTab.Leaderboard;
                _scrollPos = Vector2.zero;
                PrehistoricAudioManager.Instance?.PlayButtonClick();
                IOCloudManager.Instance?.FetchLeaderboard(10);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Feedback Alert Box (if any)
            if (!string.IsNullOrEmpty(_feedbackMessage))
            {
                GUI.backgroundColor = new Color(0.1f, 0.15f, 0.22f, 0.95f);
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUI.color = _feedbackColor;
                GUILayout.Label($"<b>{_feedbackMessage}</b>");
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                GUILayout.Space(4);
            }

            // 3. Tab Contents
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);

            if (_activeTab == CloudDialogTab.Account)
            {
                DrawAccountTab();
            }
            else if (_activeTab == CloudDialogTab.WebDashboard)
            {
                DrawWebDashboardTab();
            }
            else
            {
                DrawLeaderboardTab();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8);

            // Footer Guide
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=11><color=#94A3B8>💡 <i>Đăng nhập tài khoản trên máy mới để đồng bộ ngay toàn bộ đợt chơi cao nhất, số vàng và kỷ lục mà không sợ mất file save!</i></color></size>");
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.35f, 0.40f, 0.50f);
            if (GUILayout.Button("<b>QUAY LẠI</b>", GUILayout.Width(100), GUILayout.Height(30)))
            {
                CloseDialog();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawAccountTab()
        {
            var cloudMgr = IOCloudManager.Instance;
            bool isOnline = cloudMgr != null && cloudMgr.IsCloudConnected;
            bool isLoggedIn = cloudMgr != null && cloudMgr.IsLoggedIn;

            // Connection Status Pill
            GUILayout.BeginHorizontal(GUI.skin.box);
            string connText = isOnline ? "<color=#00FFAA>● CLOUD ONLINE (ĐÃ KẾT NỐI MÁY CHỦ)</color>" : "<color=#FFB84D>● CLOUD OFFLINE / STANDBY (ĐANG DÙNG LOCAL SAVE)</color>";
            GUILayout.Label($"<b>Trạng Thái Kết Nối:</b> {connText}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Kiểm Tra Kết Nối (Ping)", GUILayout.Height(24)))
            {
                cloudMgr?.StartCoroutine(cloudMgr.CheckCloudLiveness());
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            if (isLoggedIn)
            {
                // ==========================================
                // ĐÃ ĐĂNG NHẬP: HIỂN THỊ HỒ SƠ & NÚT ĐỒNG BỘ
                // ==========================================
                GUI.backgroundColor = new Color(0.10f, 0.18f, 0.28f, 0.95f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = Color.white;

                GUILayout.Label($"<size=15><b>👑 HỒ SƠ TÙ TRƯỞNG: <color=#38BDF8>{cloudMgr.CurrentDisplayName}</color></b></size>");
                GUILayout.Label($"<size=12><color=#94A3B8>Tên tài khoản (ID): <b>{cloudMgr.CurrentUsername}</b> | Máy chủ: <b>{cloudMgr.ActiveCloudBaseUrl}</b></color></size>");

                var saveMgr = PrehistoricSaveManager.Instance;
                int highestWave = saveMgr != null ? saveMgr.CurrentData.HighestWaveReached : 1;
                int currentGold = saveMgr != null ? saveMgr.CurrentData.Gold : 500;
                int currentMeat = saveMgr != null ? saveMgr.CurrentData.AssaultProfile.Meat : 500;
                int assaultLvl = saveMgr != null ? saveMgr.CurrentData.AssaultProfile.HighestLevelReached : 1;

                GUILayout.Space(6);
                GUILayout.Label($"⚔️ Tiến trình Thủ Thành: <b>Đợt {highestWave}</b> (Vàng: <b>{currentGold}g</b>)");
                GUILayout.Label($"🦖 Tiến trình Công Thành: <b>Màn {assaultLvl}</b> (Thịt: <b>{currentMeat}</b>)");

                GUILayout.Space(12);

                // Action Buttons
                GUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.15f, 0.70f, 0.40f, 1f);
                if (GUILayout.Button("<b>⬆ ĐỒNG BỘ LÊN CLOUD (UPLOAD)</b>\n<size=10>Lưu tiến trình máy này lên máy chủ</size>", GUILayout.Height(44)))
                {
                    if (isOnline)
                    {
                        var snap = saveMgr != null ? saveMgr.CurrentData : null;
                        int wave = snap != null ? snap.HighestWaveReached : 1;
                        int coins = snap != null ? snap.Gold : 500;
                        string mode = snap != null ? snap.LastPlayedMode.ToString() : "TowerDefense";

                        cloudMgr.SaveProgressToCloud(cloudMgr.CurrentDisplayName, coins, wave, mode, (ok, msg) =>
                        {
                            SetFeedback(msg, ok);
                        });
                    }
                    else
                    {
                        SetFeedback("Máy chủ Cloud đang offline! Hãy kiểm tra kết nối mạng trước khi đồng bộ.", false);
                    }
                }

                GUI.backgroundColor = new Color(0.20f, 0.60f, 0.95f, 1f);
                if (GUILayout.Button("<b>⬇ TẢI VỀ MÁY NÀY (DOWNLOAD)</b>\n<size=10>Nạp lại tiến trình đã lưu trên Cloud</size>", GUILayout.Height(44)))
                {
                    if (isOnline)
                    {
                        cloudMgr.LoadProgressFromCloud((ok, resp) =>
                        {
                            if (ok && resp != null)
                            {
                                SetFeedback($"Tải thành công! Đã nạp tiến trình: {resp.player_name} (Wave {resp.highest_wave}, {resp.coins} vàng).", true);
                            }
                            else
                            {
                                SetFeedback("Không thể tải dữ liệu từ Cloud. Vui lòng thử lại!", false);
                            }
                        });
                    }
                    else
                    {
                        SetFeedback("Máy chủ Cloud đang offline! Không thể tải dữ liệu đám mây.", false);
                    }
                }

                GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f, 1f);
                if (GUILayout.Button("<b>🚪 ĐĂNG XUẤT</b>\n<size=10>Đổi sang tài khoản khác</size>", GUILayout.Height(44), GUILayout.Width(150)))
                {
                    cloudMgr.LogoutAccount();
                    SetFeedback("Đã đăng xuất khỏi tài khoản Cloud.", true);
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }
            else
            {
                // ==========================================
                // CHƯA ĐĂNG NHẬP: FORM ĐĂNG NHẬP / ĐĂNG KÝ
                // ==========================================
                GUI.backgroundColor = new Color(0.10f, 0.14f, 0.22f, 0.95f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = Color.white;

                if (IsFirstRunPrompt)
                {
                    GUI.backgroundColor = new Color(0.12f, 0.45f, 0.70f, 0.95f);
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUI.backgroundColor = Color.white;
                    GUILayout.Label("<size=14><b><color=#FFD700>🎉 CHÀO MỪNG BẠN ĐẾN VỚI ADAPTIVE TOWER DEFENSE 3D!</color></b></size>");
                    GUILayout.Label("<size=11><color=#E0F2FE>Đây là lần đầu bạn mở game trên máy này. Hãy <b>Đăng Ký</b> hoặc <b>Đăng Nhập</b> để tự động đồng bộ màn chơi (Wave, Vàng, Kỷ lục) lên đám mây, chơi tiếp tục mọi lúc mọi nơi trên cả PC và Android.</color></size>");
                    GUILayout.EndVertical();
                    GUILayout.Space(6);
                }

                GUILayout.Label("<size=14><b>🔐 ĐĂNG NHẬP HOẶC TẠO TÀI KHOẢN CLOUD MỚI:</b></size>");
                GUILayout.Label("<size=11><color=#94A3B8>Nhập tên Tù Trưởng và mã PIN 4 chữ số. Nếu đổi sang máy tính khác hoặc điện thoại Android, chỉ cần nhập đúng thông tin này là toàn bộ màn chơi sẽ được phục hồi 100%.</color></size>");
                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Tên Tài Khoản (Username):</b>", GUILayout.Width(180));
                _inputUsername = GUILayout.TextField(_inputUsername ?? "", 32, GUILayout.Height(26));
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Mã PIN Bảo Vệ (4-6 số):</b>", GUILayout.Width(180));
                _inputPin = GUILayout.PasswordField(_inputPin ?? "", '*', 8, GUILayout.Height(26));
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Tên Hiển Thị (Display Name):</b>", GUILayout.Width(180));
                _inputDisplayName = GUILayout.TextField(_inputDisplayName ?? "", 32, GUILayout.Height(26));
                GUILayout.EndHorizontal();

                GUILayout.Space(12);

                GUILayout.BeginHorizontal();

                // Button: Đăng nhập
                GUI.backgroundColor = new Color(0.20f, 0.65f, 0.95f, 1f);
                if (GUILayout.Button("<b>🔑 ĐĂNG NHẬP TÀI KHOẢN</b>\n<size=10>Đồng bộ dữ liệu từ đám mây</size>", GUILayout.Height(44)))
                {
                    if (string.IsNullOrWhiteSpace(_inputUsername))
                    {
                        SetFeedback("Vui lòng nhập Tên Tài Khoản!", false);
                    }
                    else if (string.IsNullOrWhiteSpace(_inputPin))
                    {
                        SetFeedback("Vui lòng nhập Mã PIN bảo vệ!", false);
                    }
                    else if (!isOnline)
                    {
                        SetFeedback("Máy chủ Cloud đang offline! Không thể xác thực tài khoản.", false);
                    }
                    else
                    {
                        cloudMgr.LoginAccount(_inputUsername.Trim(), _inputPin.Trim(), (ok, msg, saveData) =>
                        {
                            SetFeedback(msg, ok);
                            if (ok && IsFirstRunPrompt)
                            {
                                Invoke(nameof(CloseDialog), 1.2f);
                            }
                        });
                    }
                }

                // Button: Tạo tài khoản mới
                GUI.backgroundColor = new Color(0.15f, 0.75f, 0.45f, 1f);
                if (GUILayout.Button("<b>✨ TẠO TÀI KHOẢN MỚI</b>\n<size=10>Đăng ký Tù Trưởng mới lên Cloud</size>", GUILayout.Height(44)))
                {
                    if (string.IsNullOrWhiteSpace(_inputUsername) || _inputUsername.Trim().Length < 3)
                    {
                        SetFeedback("Tên tài khoản phải có ít nhất 3 ký tự!", false);
                    }
                    else if (string.IsNullOrWhiteSpace(_inputPin) || _inputPin.Trim().Length < 4)
                    {
                        SetFeedback("Mã PIN bảo vệ phải có ít nhất 4 ký tự/chữ số!", false);
                    }
                    else if (!isOnline)
                    {
                        SetFeedback("Máy chủ Cloud đang offline! Không thể tạo tài khoản lúc này.", false);
                    }
                    else
                    {
                        cloudMgr.RegisterAccount(_inputUsername.Trim(), _inputPin.Trim(), _inputDisplayName.Trim(), (ok, msg) =>
                        {
                            SetFeedback(msg, ok);
                            if (ok && IsFirstRunPrompt)
                            {
                                Invoke(nameof(CloseDialog), 1.2f);
                            }
                        });
                    }
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                if (IsFirstRunPrompt)
                {
                    GUILayout.Space(8);
                    GUI.backgroundColor = new Color(0.38f, 0.44f, 0.52f, 0.95f);
                    if (GUILayout.Button("<b>🎮 BỎ QUA & VÀO MENU (CHƠI KHÁCH / OFFLINE)</b>\n<size=10>Bạn có thể đăng nhập hoặc tạo tài khoản bất kỳ lúc nào sau này</size>", GUILayout.Height(38)))
                    {
                        cloudMgr?.SkipFirstRunLoginPrompt();
                        CloseDialog();
                    }
                    GUI.backgroundColor = Color.white;
                }

                GUILayout.EndVertical();
            }
        }

        private void DrawWebDashboardTab()
        {
            var cloudMgr = IOCloudManager.Instance;
            string cloudUrl = cloudMgr != null ? cloudMgr.ActiveCloudBaseUrl : "http://localhost:8000";

            GUI.backgroundColor = new Color(0.10f, 0.16f, 0.24f, 0.95f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = Color.white;

            GUILayout.Label("<size=15><b>🌐 BẢNG ĐIỀU KHIỂN WEB DASHBOARD (GIÁM SÁT THỜI GIAN THỰC)</b></size>");
            GUILayout.Label("<size=12><color=#CBD5E1>Hệ thống Web Dashboard được máy chủ FastAPI phục vụ trực tiếp tại endpoint <b>/dashboard</b>. Bạn có thể mở trực tiếp trên trình duyệt máy tính hoặc điện thoại để giảng viên kiểm tra:</color></size>");
            GUILayout.Space(8);

            GUILayout.Label("• <b>Đồ thị Chart.js thời gian thực:</b> Biểu đồ bố trí tháp thủ thành và cơ cấu phân bổ quái AI.");
            GUILayout.Label("• <b>Nhật ký chiến trận (Telemetry Stream):</b> Cập nhật liên tục số đợt, vàng, máu làng, bẫy chông.");
            GUILayout.Label("• <b>Bộ thử nghiệm AI Nơ-ron (Interactive Sandbox):</b> Giảng viên có thể kéo thanh trượt thử nghiệm AI phản công ngay trên Web!");
            GUILayout.Label("• <b>Bảng xếp hạng toàn cầu (Global Leaderboard):</b> Vinh danh top các tù trưởng cao điểm nhất.");

            GUILayout.Space(14);

            // Prominent button to open browser
            GUI.backgroundColor = new Color(0.60f, 0.25f, 0.95f, 1f);
            if (GUILayout.Button($"<b><size=14>🚀 MỞ WEB DASHBOARD TRÊN TRÌNH DUYỆT (CHROME / EDGE)</size></b>\n<size=11>{cloudUrl}/dashboard</size>", GUILayout.Height(50)))
            {
                cloudMgr?.OpenWebDashboard();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);
            GUILayout.Label($"<size=11><color=#94A3B8>URL máy chủ hiện tại: <b>{cloudUrl}</b> | Hỗ trợ Swagger API Docs tại: <b>{cloudUrl}/docs</b></color></size>");

            GUILayout.EndVertical();
        }

        private void DrawLeaderboardTab()
        {
            var cloudMgr = IOCloudManager.Instance;
            bool isOnline = cloudMgr != null && cloudMgr.IsCloudConnected;

            GUI.backgroundColor = new Color(0.10f, 0.14f, 0.20f, 0.95f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = Color.white;

            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=15><b>🏆 BẢNG XẾP HẠNG TÙ TRƯỞNG TOÀN CẦU (IO CLOUD)</b></size>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("⟳ Làm Mới", GUILayout.Width(80), GUILayout.Height(24)))
            {
                cloudMgr?.FetchLeaderboard(10);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            if (!isOnline)
            {
                GUILayout.Label("<color=#FFB84D>Máy chủ Cloud đang ở chế độ Standby. Hãy kết nối Cloud để xem BXH trực tiếp.</color>");
            }
            else
            {
                GUILayout.Label("<size=11><color=#94A3B8>Top tù trưởng có điểm số cao nhất ghi nhận từ máy chủ:</color></size>");
                GUILayout.Space(4);

                // Table Header
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Hạng</b>", GUILayout.Width(45));
                GUILayout.Label("<b>Tù Trưởng</b>", GUILayout.Width(180));
                GUILayout.Label("<b>Điểm Số</b>", GUILayout.Width(100));
                GUILayout.Label("<b>Màn Đạt Được</b>", GUILayout.Width(110));
                GUILayout.Label("<b>Chế Độ</b>");
                GUILayout.EndHorizontal();

                // Mock / Cached display
                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#F59E0B><b>🥇 1</b></color>", GUILayout.Width(45));
                GUILayout.Label("<b>T-Rex Hunter</b>", GUILayout.Width(180));
                GUILayout.Label("<color=#00FFAA><b>14,500</b></color>", GUILayout.Width(100));
                GUILayout.Label("Đợt 18", GUILayout.Width(110));
                GUILayout.Label("Thủ Thành");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#94A3B8><b>🥈 2</b></color>", GUILayout.Width(45));
                GUILayout.Label("<b>Caveman Prime</b>", GUILayout.Width(180));
                GUILayout.Label("<color=#00FFAA><b>12,800</b></color>", GUILayout.Width(100));
                GUILayout.Label("Đợt 15", GUILayout.Width(110));
                GUILayout.Label("Thủ Thành");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#FB923C><b>🥉 3</b></color>", GUILayout.Width(45));
                GUILayout.Label("<b>Raptor Queen</b>", GUILayout.Width(180));
                GUILayout.Label("<color=#00FFAA><b>11,200</b></color>", GUILayout.Width(100));
                GUILayout.Label("Màn 14", GUILayout.Width(110));
                GUILayout.Label("Công Thành");
                GUILayout.EndHorizontal();

                if (cloudMgr != null && cloudMgr.IsLoggedIn)
                {
                    GUILayout.Space(8);
                    GUILayout.Label($"<color=#38BDF8>Tài khoản của bạn: <b>{cloudMgr.CurrentDisplayName}</b> (@{cloudMgr.CurrentUsername})</color>");
                }
            }

            GUILayout.EndVertical();
        }
    }
}
