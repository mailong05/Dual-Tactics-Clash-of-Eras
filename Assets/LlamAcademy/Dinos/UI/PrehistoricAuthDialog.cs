using System;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Cloud;
using LlamAcademy.Dinos.Mobile;
using LlamAcademy.Dinos.Save;
using UnityEngine;

namespace LlamAcademy.Dinos.UI
{
    public enum AuthMode
    {
        Login,
        Register
    }

    /// <summary>
    /// Dedicated First-Run Login / Registration Dialog.
    /// Provides a clean, focused, professional mobile/PC authentication screen on first launch:
    /// - Switch between [Đăng Nhập] and [Đăng Ký].
    /// - [Chơi Khách / Offline] to bypass and play immediately with local save.
    /// - [Thoát Game] to quit.
    /// Once logged in or skipped, never bothers the player again unless they log out in Settings.
    /// </summary>
    [DefaultExecutionOrder(-2)]
    public class PrehistoricAuthDialog : MonoBehaviour
    {
        public static PrehistoricAuthDialog Instance { get; private set; }

        public bool IsOpen { get; private set; } = false;
        private AuthMode _currentMode = AuthMode.Login;

        private string _inputUsername = "";
        private string _inputPin = "";
        private string _inputDisplayName = "";

        private string _feedbackMessage = "";
        private Color _feedbackColor = Color.white;
        private float _feedbackTimer = 0f;
        private bool _isBusy = false;

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

        public static PrehistoricAuthDialog EnsureInstance()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("PrehistoricAuthDialog");
                Instance = go.AddComponent<PrehistoricAuthDialog>();
            }
            return Instance;
        }

        public void OpenDialog(AuthMode mode = AuthMode.Login)
        {
            _currentMode = mode;
            IsOpen = true;
            _feedbackMessage = "";
            _feedbackTimer = 0f;
            _isBusy = false;

            if (string.IsNullOrEmpty(_inputUsername))
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
            _isBusy = false;
            PrehistoricAudioManager.Instance?.PlayButtonClick();

            // Khi đóng hộp thoại đăng nhập, luôn bảo đảm Menu Chính mở để người chơi chủ động bấm Chơi Mới hoặc Tiếp Tục
            if (PrehistoricMainMenu.Instance != null)
            {
                PrehistoricMainMenu.Instance.IsOpen = true;
            }
            PrehistoricAudioManager.Instance?.PlayOpeningBgm();
        }

        private void SetFeedback(string msg, bool isSuccess)
        {
            _feedbackMessage = msg;
            _feedbackColor = isSuccess ? new Color(0.2f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            _feedbackTimer = 6.0f;
            _isBusy = false;
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

            GUI.depth = -2000;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            MobileUIHelper.BeginScaling();
            try
            {
                // Đăng ký toàn màn hình để ngăn chặn 100% click xuyên vào Menu Chính hoặc cảnh 3D bên dưới
                Rect fullScreenRect = new Rect(0, 0, MobileUIHelper.ScreenWidth, MobileUIHelper.ScreenHeight);
                MobileUIHelper.RegisterUIRect(fullScreenRect);

                DrawBackdrop(fullScreenRect);
                DrawAuthWindow();

                // Hỗ trợ phím Enter để đăng nhập / đăng ký nhanh và triệt tiêu phím không để lọt vào gameplay
                if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                {
                    if (!_isBusy)
                    {
                        if (_currentMode == AuthMode.Login) ExecuteLogin();
                        else ExecuteRegister();
                    }
                    Event.current.Use();
                }
            }
            finally
            {
                MobileUIHelper.EndScaling();
            }
        }

        private void DrawBackdrop(Rect fullScreenRect)
        {
            // Nền tối hoàn toàn (Alpha = 1.0) chặn đứng mọi hình ảnh của gameplay phía sau
            GUI.color = new Color(0.04f, 0.07f, 0.12f, 1.0f);
            GUI.DrawTexture(fullScreenRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawAuthWindow()
        {
            float screenW = MobileUIHelper.ScreenWidth;
            float screenH = MobileUIHelper.ScreenHeight;

            float winW = Mathf.Clamp(screenW * 0.46f, 440f, 540f);
            winW = Mathf.Min(winW, screenW - 20f);
            float winH = _currentMode == AuthMode.Register ? 470f : 420f;
            winH = Mathf.Min(winH, screenH - 24f);

            float x = (screenW - winW) * 0.5f;
            float y = (screenH - winH) * 0.5f;
            Rect winRect = new Rect(x, y, winW, winH);

            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.98f);
            GUI.Box(winRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(x + 16f, y + 16f, winW - 32f, winH - 32f));
            GUILayout.BeginVertical();

            // 1. Header & Title
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=16><b><color=#FFD700>⚔️ ADAPTIVE TOWER DEFENSE 3D</color></b></size>");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label("<size=12><color=#94A3B8>Hệ thống Tài Khoản Tù Trưởng & Đồng Bộ Đám Mây</color></size>");
            GUILayout.Space(10);

            // 2. Mode Selector: [ĐĂNG NHẬP] | [ĐĂNG KÝ]
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = _currentMode == AuthMode.Login ? new Color(0.18f, 0.65f, 0.95f, 1f) : new Color(0.22f, 0.28f, 0.36f, 1f);
            if (GUILayout.Button("<b>🔑 ĐĂNG NHẬP</b>", GUILayout.Height(36)))
            {
                _currentMode = AuthMode.Login;
                _feedbackMessage = "";
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }

            GUI.backgroundColor = _currentMode == AuthMode.Register ? new Color(0.16f, 0.75f, 0.45f, 1f) : new Color(0.22f, 0.28f, 0.36f, 1f);
            if (GUILayout.Button("<b>✨ ĐĂNG KÝ MỚI</b>", GUILayout.Height(36)))
            {
                _currentMode = AuthMode.Register;
                _feedbackMessage = "";
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // 3. Input Form
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Space(4);

            // Username
            GUILayout.Label("<size=11><b>Tên Tài Khoản (Username):</b></size>");
            _inputUsername = GUILayout.TextField(_inputUsername ?? "", 24, GUILayout.Height(28));
            GUILayout.Space(6);

            // PIN
            GUILayout.Label("<size=11><b>Mã PIN Bảo Vệ (4 - 8 chữ số):</b></size>");
            _inputPin = GUILayout.PasswordField(_inputPin ?? "", '*', 8, GUILayout.Height(28));
            GUILayout.Space(6);

            // Display Name (Only in Register mode)
            if (_currentMode == AuthMode.Register)
            {
                GUILayout.Label("<size=11><b>Tên Hiển Thị Tù Trưởng:</b></size>");
                _inputDisplayName = GUILayout.TextField(_inputDisplayName ?? "", 24, GUILayout.Height(28));
                GUILayout.Space(6);
            }

            GUILayout.EndVertical();

            // Feedback Message
            if (!string.IsNullOrEmpty(_feedbackMessage))
            {
                GUI.color = _feedbackColor;
                GUILayout.Label($"<b><size=11>{_feedbackMessage}</size></b>");
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Space(4);
            }

            GUILayout.Space(6);

            // 4. Primary Action Button (Login or Register)
            var cloudMgr = IOCloudManager.Instance;
            bool isOnline = cloudMgr != null && cloudMgr.IsCloudConnected;

            if (_currentMode == AuthMode.Login)
            {
                GUI.backgroundColor = new Color(0.20f, 0.65f, 0.95f, 1f);
                string btnText = _isBusy ? "⏳ Đang kết nối máy chủ..." : "<b>🚀 ĐĂNG NHẬP VÀO GAME</b>";
                if (GUILayout.Button(btnText, GUILayout.Height(42)) && !_isBusy)
                {
                    ExecuteLogin();
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.16f, 0.75f, 0.45f, 1f);
                string btnText = _isBusy ? "⏳ Đang đăng ký tài khoản..." : "<b>✨ TẠO TÀI KHOẢN MỚI</b>";
                if (GUILayout.Button(btnText, GUILayout.Height(42)) && !_isBusy)
                {
                    ExecuteRegister();
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // 5. Secondary Action Bar: [CHƠI KHÁCH / OFFLINE] & [THOÁT GAME]
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.35f, 0.42f, 0.50f, 0.95f);
            if (GUILayout.Button("<b>🎮 CHƠI KHÁCH (OFFLINE)</b>", GUILayout.Height(34)))
            {
                cloudMgr?.SkipFirstRunLoginPrompt();
                CloseDialog();
            }

            GUI.backgroundColor = new Color(0.70f, 0.22f, 0.22f, 0.95f);
            if (GUILayout.Button("<b>🚪 THOÁT GAME</b>", GUILayout.Width(110), GUILayout.Height(34)))
            {
                ExecuteQuit();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void ExecuteLogin()
        {
            if (string.IsNullOrWhiteSpace(_inputUsername))
            {
                SetFeedback("Vui lòng nhập Tên Tài Khoản!", false);
                return;
            }
            if (string.IsNullOrWhiteSpace(_inputPin))
            {
                SetFeedback("Vui lòng nhập Mã PIN!", false);
                return;
            }

            var cloudMgr = IOCloudManager.Instance;
            if (cloudMgr == null)
            {
                SetFeedback("Hệ thống Cloud chưa sẵn sàng.", false);
                return;
            }

            _isBusy = true;
            cloudMgr.LoginAccount(_inputUsername.Trim(), _inputPin.Trim(), (ok, msg, saveData) =>
            {
                _isBusy = false;
                SetFeedback(msg, ok);
                if (ok)
                {
                    cloudMgr.MarkHasLoggedIn();
                    Invoke(nameof(CloseDialog), 1.0f);
                }
            });
        }

        private void ExecuteRegister()
        {
            if (string.IsNullOrWhiteSpace(_inputUsername) || _inputUsername.Trim().Length < 3)
            {
                SetFeedback("Tên tài khoản phải có ít nhất 3 ký tự!", false);
                return;
            }
            if (string.IsNullOrWhiteSpace(_inputPin) || _inputPin.Trim().Length < 4)
            {
                SetFeedback("Mã PIN bảo vệ phải có ít nhất 4 ký tự/chữ số!", false);
                return;
            }

            var cloudMgr = IOCloudManager.Instance;
            if (cloudMgr == null)
            {
                SetFeedback("Hệ thống Cloud chưa sẵn sàng.", false);
                return;
            }

            string dispName = string.IsNullOrWhiteSpace(_inputDisplayName) ? _inputUsername.Trim() : _inputDisplayName.Trim();

            _isBusy = true;
            cloudMgr.RegisterAccount(_inputUsername.Trim(), _inputPin.Trim(), dispName, (ok, msg) =>
            {
                _isBusy = false;
                SetFeedback(msg, ok);
                if (ok)
                {
                    cloudMgr.MarkHasLoggedIn();
                    Invoke(nameof(CloseDialog), 1.0f);
                }
            });
        }

        private void ExecuteQuit()
        {
            PrehistoricAudioManager.Instance?.PlayButtonClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
