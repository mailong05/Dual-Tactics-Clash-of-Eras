using System;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.InputSystem;
namespace LlamAcademy.Dinos.UI
{
    /// <summary>
    /// Explainable AI (XAI) Dual Monitor HUD.
    /// Dedicated telemetry and visualization for BOTH game modes:
    /// 1. [Thủ Thành - Tower Defense]: Deep Learning (PrehistoricTacticsNet), Softmax distribution,
    ///    Spatial Route Bias (Q-Learning), Mazing adaptation, and Dynamic Difficulty Adjustment (DDA).
    /// 2. [Công Thành - Dino Assault]: Village AI Defense Director, Garrison Defenders, Automated Defense Batteries,
    ///    Tactical Breach Memory, Player Deployed Dino Threat Breakdown, and Reactive Village Defense Strategy.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class PrehistoricNeuralMonitorUI : MonoBehaviour
    {
        private static PrehistoricNeuralMonitorUI _Instance;
        public static PrehistoricNeuralMonitorUI Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = FindFirstObjectByType<PrehistoricNeuralMonitorUI>();
                    if (_Instance == null)
                    {
                        GameObject go = new GameObject("PrehistoricNeuralMonitorUI");
                        _Instance = go.AddComponent<PrehistoricNeuralMonitorUI>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _Instance;
            }
        }
        [Header("=== Window State ===")]
        [SerializeField] private bool _IsOpen = false;
        [SerializeField] private int _SelectedModeTab = -1; // -1: Auto, 0: Tower Defense, 1: Dino Assault
        public bool IsOpen
        {
            get => _IsOpen;
            set => _IsOpen = value;
        }
        // GUI Styles and Textures
        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _boldLabelStyle;
        private GUIStyle _barBgStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _tabButtonStyle;
        private GUIStyle _rationaleBoxStyle;
        private GUIStyle _cardBoxStyle;
        private Texture2D _panelTex;
        private Texture2D _barBgTex;
        private Texture2D _barFillTex;
        private Texture2D _cardBgTex;
        private Vector2 _scrollPos = Vector2.zero;
        private void Awake()
        {
            if (_Instance != null && _Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        private void Update()
        {
            // Toggle window via F1 key or backquote `
            if (Keyboard.current != null)
            {
                if (Keyboard.current.f1Key.wasPressedThisFrame || Keyboard.current.backquoteKey.wasPressedThisFrame)
                {
                    ToggleWindow();
                }
            }
        }
        public void ToggleWindow()
        {
            _IsOpen = !_IsOpen;
            if (_IsOpen)
            {
                // Auto-sync with current game mode
                bool isAssaultNow = (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault);
                if (!isAssaultNow && PrehistoricNeuralDirector.Instance != null)
                {
                    int currentWave = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance.CurrentWave : 1;
                    PrehistoricNeuralDirector.Instance.Predict(currentWave);
                }
            }
        }
        private void EnsureStyles()
        {
            if (_windowStyle != null) return;
            _panelTex = MakeColorTexture(new Color(0.05f, 0.07f, 0.11f, 0.95f));
            _barBgTex = MakeColorTexture(new Color(0.11f, 0.15f, 0.22f, 0.90f));
            _barFillTex = MakeColorTexture(Color.white);
            _cardBgTex = MakeColorTexture(new Color(0.08f, 0.12f, 0.18f, 0.95f));
            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = _panelTex, textColor = Color.white },
                padding = new RectOffset(16, 16, 16, 16),
                border = new RectOffset(4, 4, 4, 4)
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 1f, 0.85f) },
                alignment = TextAnchor.MiddleCenter
            };
            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.35f) }
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f) },
                richText = true
            };
            _boldLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                richText = true
            };
            _barBgStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _barBgTex }
            };
            _cardBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _cardBgTex, textColor = Color.white },
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _tabButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _rationaleBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _barBgTex, textColor = new Color(0.88f, 1f, 0.92f) },
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8),
                richText = true
            };
        }
        private Texture2D MakeColorTexture(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
        private void OnGUI()
        {
            if (!_IsOpen) return;
            EnsureStyles();
            Mobile.MobileUIHelper.BeginScaling();
            try
            {
                GUI.depth = -2000; // Render on top of everything
                // Draw full XAI Monitor window
                float screenW = Mobile.MobileUIHelper.ScreenWidth;
                float screenH = Mobile.MobileUIHelper.ScreenHeight;
                float winWidth = Mathf.Min(620f, screenW * 0.95f);
                float winHeight = Mathf.Min(680f, screenH * 0.92f);
                float winX = (screenW - winWidth) * 0.5f;
                float winY = (screenH - winHeight) * 0.5f;
                Rect winRect = new Rect(winX, winY, winWidth, winHeight);
                Mobile.MobileUIHelper.RegisterUIRect(winRect);
                GUILayout.BeginArea(winRect, _windowStyle);
                _scrollPos = GUILayout.BeginScrollView(_scrollPos);
                // 1. Main Header
                GUILayout.Label("🧠 PREHISTORIC AI DIRECTOR MONITOR", _headerStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#55FF88>● ON-DEVICE NEURAL & HEURISTIC ENGINE (C#)</color>", _labelStyle);
                float latency = PrehistoricNeuralDirector.Instance != null ? PrehistoricNeuralDirector.Instance.LastInferenceLatencyMs : 0.05f;
                GUILayout.Label($"<color=#AAAAAA>⚡ Latency: {latency:F2} ms | Edge AI</color>", _labelStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
                // 2. Tách riêng giao diện cho từng chế độ riêng biệt (KHÔNG dùng Tab)
                bool isAssault = (PrehistoricGameModeManager.Instance != null && 
                                  PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault);
                GUILayout.BeginHorizontal();
                if (!isAssault)
                {
                    GUILayout.Label("<b><color=#55FFAA>🛡️ GIÁM SÁT AI DIRECTOR - CHẾ ĐỘ THỦ THÀNH (TOWER DEFENSE)</color></b>", _labelStyle);
                }
                else
                {
                    GUILayout.Label("<b><color=#FFAA55>⚔️ GIÁM SÁT BỘ NÃO THỦ LÀNG - CHẾ ĐỘ CÔNG THÀNH (DEFENDER AI)</color></b>", _labelStyle);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
                DrawSeparator();

                // Banner phản xạ thời gian thực (Live Neural Tactical Alert)
                if (PrehistoricNeuralDirector.Instance != null && 
                    !string.IsNullOrEmpty(PrehistoricNeuralDirector.Instance.ActiveLiveTacticalAlert) &&
                    Time.time <= PrehistoricNeuralDirector.Instance.LiveAlertExpireTime)
                {
                    Color oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.85f, 0.22f, 0.15f, 0.95f);
                    GUILayout.BeginVertical("box");
                    GUILayout.Label($"<b><color=#FFFF55>⚡ PHẢN XẠ THỜI GIAN THỰC (LIVE ADAPTATION):</color></b>\n<color=#FFFFFF>{PrehistoricNeuralDirector.Instance.ActiveLiveTacticalAlert}</color>", _labelStyle);
                    GUILayout.EndVertical();
                    GUI.backgroundColor = oldBg;
                    GUILayout.Space(4);
                }

                // 3. Render Mode-Specific Telemetry
                if (!isAssault)
                {
                    DrawTowerDefenseUIDirector(winWidth);
                }
                else
                {
                    DrawAssaultDefenseUIDirector(winWidth);
                }
                GUILayout.Space(10);
                // 4. Footer Action Buttons
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⟳ CẬP NHẬT TRÍ TUỆ NHÂN TẠO", _buttonStyle, GUILayout.Height(32)))
                {
                    if (!isAssault && PrehistoricNeuralDirector.Instance != null)
                    {
                        int currentWave = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance.CurrentWave : 1;
                        PrehistoricNeuralDirector.Instance.Predict(currentWave);
                    }
                }
                if (GUILayout.Button("✕ ĐÓNG CỬA SỔ [F1]", _buttonStyle, GUILayout.Height(32)))
                {
                    _IsOpen = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                Mobile.MobileUIHelper.EndScaling();
            }
        }
        private void DrawTowerDefenseUIDirector(float winWidth)
        {
            if (PrehistoricNeuralDirector.Instance != null && PrehistoricNeuralDirector.Instance.HasPrediction)
            {
                var pred = PrehistoricNeuralDirector.Instance.LatestPrediction;
                // =========================================================================
                // MỤC ĐẶC BIỆT: NHẬN DIỆN CHIẾN THUẬT MAZING CỦA NGƯỜI CHƠI (TỰ DO XÂY TƯỜNG)
                // =========================================================================
                GUILayout.Space(4);
                if (pred.WallCount == 0)
                {
                    GUILayout.BeginVertical(_cardBoxStyle);
                    GUILayout.Label("🟢 <color=#4ADE80><b>CHIẾN THUẬT PHÒNG THỦ: CĂN CỨ MỞ (0 TƯỜNG ĐẶT)</b></color>", _boldLabelStyle);
                    GUILayout.Label("<color=#FDE047>⚡ ĐIỀU CHỈNH DEEP LEARNING:</color> Phát hiện người chơi để căn cứ trống trải! Mạng nơ-ron tự động kích hoạt bầy <color=#FB923C><b>Velociraptor Swarm Rush (+85%)</b></color> tốc độ cao tràn thẳng qua trung tâm trước khi người chơi kịp hoàn thiện tháp. Cắt giảm Pterodactyl (-60%) và Ankylosaurus (-50%) vì không có tường cản!", _labelStyle);
                    GUILayout.Label("<color=#94A3B8>Tác động Logits: Runner +0.85 | Flyer -0.60 | Tank -0.50 | Thích ứng: Mở đường càn quét</color>", _labelStyle);
                    GUILayout.EndVertical();
                }
                else
                {
                    float wallPctFlyer = Mathf.Clamp01(pred.WallCount / 6f) * 70f;
                    float wallPctTank = Mathf.Clamp01(pred.WallCount / 6f) * 55f;
                    GUILayout.BeginVertical(_cardBoxStyle);
                    GUILayout.Label($"🟡 <color=#FACC15><b>CHIẾN THUẬT PHÒNG THỦ: CĂN CỨ MÊ CUNG ({pred.WallCount} TƯỜNG ĐẶT)</b></color>", _boldLabelStyle);
                    GUILayout.Label($"<color=#FDE047>⚡ ĐIỀU CHỈNH DEEP LEARNING:</color> Phát hiện người chơi xây {pred.WallCount} tường chắn tạo mê cung! Mạng nơ-ron thích ứng triển khai <color=#38BDF8><b>Pterodactyl (+{wallPctFlyer:F0}%)</b></color> bay thẳng qua đầu tường và <color=#4ADE80><b>Ankylosaurus (+{wallPctTank:F0}%)</b></color> thiết giáp húc đổ tường giải vây cho đồng đội!", _labelStyle);
                    GUILayout.Label($"<color=#94A3B8>Tác động Logits: Flyer +{(pred.WallCount / 6f) * 0.70f:F2} | Tank +{(pred.WallCount / 6f) * 0.55f:F2} | Runner -{(pred.WallCount / 6f) * 0.40f:F2} (giảm tắc nghẽn)</color>", _labelStyle);
                    GUILayout.EndVertical();
                }
                GUILayout.Space(6);
                DrawSeparator();
                // =========================================================================
                // 1. Section 1: Softmax Dino Composition
                // =========================================================================
                GUILayout.Space(4);
                GUILayout.Label("1. CƠ CẤU ĐỘI HÌNH KHỦNG LONG PHẢN CÔNG (SOFTMAX DISTRIBUTION)", _subHeaderStyle);
                DrawProgressBar("🦖 Velociraptor (Runner)", pred.ArchetypeDistribution[0], $"{(pred.ArchetypeDistribution[0] * 100f):F1}%", new Color(1f, 0.6f, 0.2f));
                DrawProgressBar("🦅 Pterodactyl (Không kích Flyer)", pred.ArchetypeDistribution[1], $"{(pred.ArchetypeDistribution[1] * 100f):F1}%", new Color(0.3f, 0.8f, 1f));
                DrawProgressBar("🛡️ Ankylosaurus (Phá Tường Tank)", pred.ArchetypeDistribution[2], $"{(pred.ArchetypeDistribution[2] * 100f):F1}%", new Color(0.4f, 0.9f, 0.4f));
                DrawProgressBar("👑 T-Rex Boss (Apex Kích Thước)", pred.ArchetypeDistribution[3], $"{(pred.ArchetypeDistribution[3] * 100f):F1}%", new Color(1f, 0.25f, 0.35f));
                GUILayout.Space(6);
                DrawSeparator();
                // =========================================================================
                // 2. Section 2: Q-Learning Spatial Policy (Spawn Gates)
                // =========================================================================
                GUILayout.Space(4);
                GUILayout.Label("2. HỌC TĂNG CƯỜNG CHỌN CỔNG ĐỘT KÍCH (Q-LEARNING POLICY)", _subHeaderStyle);
                DrawProgressBar("Cổng Bắc (Đột Kích Trực Diện)", pred.RouteDistribution[0], $"{(pred.RouteDistribution[0] * 100f):F1}%", new Color(1f, 0.4f, 0.4f));
                DrawProgressBar("Cổng Đông (Đánh Bọc Sườn)", pred.RouteDistribution[1], $"{(pred.RouteDistribution[1] * 100f):F1}%", new Color(0.4f, 0.7f, 1f));
                DrawProgressBar("Cổng Tây (Tập Kích Vòng Ngoài)", pred.RouteDistribution[2], $"{(pred.RouteDistribution[2] * 100f):F1}%", new Color(0.8f, 0.5f, 1f));
                GUILayout.Space(2);
                if (pred.QValues != null && pred.QValues.Length >= 3)
                {
                    GUILayout.Label($"<color=#AAAAAA>Q-Values: [Bắc: {pred.QValues[0]:F2} | Đông: {pred.QValues[1]:F2} | Tây: {pred.QValues[2]:F2}] | Số đợt đã huấn luyện: {pred.RlEpisodes}</color>", _labelStyle);
                }
                GUILayout.Space(6);
                DrawSeparator();
                // =========================================================================
                // 3. Section 3: Dynamic Difficulty Adjustment (DDA)
                // =========================================================================
                GUILayout.Space(4);
                GUILayout.Label("3. ĐIỀU CHỈNH ĐỘ KHÓ ĐỘNG THEO KỸ NĂNG NGƯỜI CHƠI (DDA)", _subHeaderStyle);
                DrawProgressBar("Đánh Giá Kỹ Năng Người Chơi", pred.PlayerSkillRating, $"{(pred.PlayerSkillRating * 100f):F0} / 100", new Color(0.9f, 0.85f, 0.2f));
                DrawProgressBar("Chỉ Số Dễ Bị Xâm Nhập (Vulnerability)", pred.VulnerabilityScore, $"{(pred.VulnerabilityScore * 100f):F0}%", new Color(0.5f, 1f, 0.5f));
                DrawProgressBar("Chỉ Số Hung Hãn Toàn Cục (Aggression)", pred.AggressionIndex, $"{(pred.AggressionIndex * 100f):F0}%", new Color(1f, 0.4f, 0.3f));
                GUILayout.Space(2);
                GUILayout.Label($"Quy mô đợt đề xuất: <b>{pred.RecommendedWaveSize} khủng long</b> (Hệ số quy mô: <b>{pred.DifficultyMultiplier:F2}x</b>)", _labelStyle);
                GUILayout.Space(6);
                DrawSeparator();
                // =========================================================================
                // 4. Section 4: Telemetry Features (Inputs to MLP)
                // =========================================================================
                GUILayout.Space(4);
                GUILayout.Label("4. DỮ LIỆU ĐẦU VÀO MẠNG NƠ-RON (12 TELEMETRY FEATURES)", _subHeaderStyle);
                if (pred.InputFeatures != null && pred.InputFeatures.Length >= 12)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(GUILayout.Width(winWidth * 0.46f));
                    DrawMiniMetric("Máu Làng", pred.InputFeatures[0]);
                    DrawMiniMetric("Số Đợt Hiện Tại", pred.InputFeatures[1]);
                    DrawMiniMetric("Vàng Hiện Có", pred.InputFeatures[2]);
                    DrawMiniMetric("Tổng Số Tháp", pred.InputFeatures[3]);
                    DrawMiniMetric("Chòi Cung Thủ", pred.InputFeatures[4]);
                    DrawMiniMetric("Tháp Bắn Nỏ", pred.InputFeatures[5]);
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical(GUILayout.Width(winWidth * 0.46f));
                    DrawMiniMetric("Trụ Sấm Sét", pred.InputFeatures[6]);
                    DrawMiniMetric("Tháp Băng/Hắc Ín", pred.InputFeatures[7]);
                    DrawMiniMetric("Bẫy Chông Độc", pred.InputFeatures[8]);
                    DrawMiniMetric($"Tường Rào Chắn ({pred.WallCount})", pred.InputFeatures[9]);
                    DrawMiniMetric("Mật độ DPS", pred.InputFeatures[10]);
                    DrawMiniMetric("DDA Skill Rating", pred.InputFeatures[11]);
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(6);
                DrawSeparator();
                // =========================================================================
                // 5. Section 5: Explainable AI Tactical Rationale
                // =========================================================================
                GUILayout.Space(4);
                GUILayout.Label("5. GIẢI THÍCH CHIẾN THUẬT (EXPLAINABLE AI RATIONALE)", _subHeaderStyle);
                GUILayout.Box(pred.TacticalRationale, _rationaleBoxStyle, GUILayout.MinHeight(54));
            }
            else
            {
                GUILayout.Label("Đang chờ mạng nơ-ron thu thập dữ liệu...", _labelStyle);
            }
        }
        private void DrawAssaultDefenseUIDirector(float winWidth)
        {
            var director = PrehistoricNeuralDirector.Instance;
            if (director == null) return;
            var data = director.GetAssaultDefenseTelemetry();
            GUILayout.Space(6);
            // Mode Banner
            GUILayout.BeginVertical(_cardBoxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>⚔️ CHẾ ĐỘ: CÔNG THÀNH TIỀN SỬ (MÀN {data.AssaultLevel})</b>", _boldLabelStyle);
            GUILayout.Label($"<color=#FDE047>● {data.PhaseName}</color>", _boldLabelStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Chiến lược làng: <color=#38BDF8><b>{data.VillageStrategyTitle}</b></color>", _labelStyle);
            GUILayout.EndVertical();
            GUILayout.Space(6);
            DrawSeparator();
            // 1. Village Core & Readiness
            GUILayout.Space(4);
            GUILayout.Label("1. TÌNH TRẠNG LÕI CĂN CỨ LÀNG (BASE CORE INTEGRITY)", _subHeaderStyle);
            Color hpCol = data.BaseHealthPercent > 0.6f ? new Color(0.2f, 0.9f, 0.3f) : (data.BaseHealthPercent > 0.3f ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.25f, 0.25f));
            DrawProgressBar("Máu Lõi Căn Cứ", data.BaseHealthPercent, $"{data.BaseHealth}/{data.MaxBaseHealth}", hpCol);
            DrawProgressBar("Chỉ Số Sẵn Sàng Phòng Thủ", data.DefenseReadiness, $"{(data.DefenseReadiness * 100f):F0}%", new Color(0.35f, 0.85f, 1f));
            DrawProgressBar("Mức Độ Đe Dọa Từ Khủng Long", data.ThreatLevel, $"{(data.ThreatLevel * 100f):F0}%", new Color(1f, 0.45f, 0.3f));
            GUILayout.Space(6);
            DrawSeparator();
            // 2. Garrison & Automated Defenses
            GUILayout.Space(4);
            GUILayout.Label("2. QUÂN LỰC DÂN BINH & PIN PHÒNG THỦ TỰ ĐỘNG", _subHeaderStyle);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(winWidth * 0.46f));
            DrawMiniMetric($"Chiến Binh Cận Chiến ({data.HunterCount})", Mathf.Clamp01(data.HunterCount / 8f));
            DrawMiniMetric($"Cung Thủ Bờ Tường/Cầu ({data.ArcherCount})", Mathf.Clamp01(data.ArcherCount / 8f));
            DrawMiniMetric($"Tổng Quân Phòng Ngự ({data.DefenderCount})", Mathf.Clamp01(data.DefenderCount / 12f));
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(winWidth * 0.46f));
            DrawMiniMetric($"Máy Bắn Đá Lửa ({data.ActiveCatapults})", Mathf.Clamp01(data.ActiveCatapults / 4f));
            DrawMiniMetric($"Tháp Nỏ Phóng Lao ({data.ActiveBallistas})", Mathf.Clamp01(data.ActiveBallistas / 4f));
            DrawMiniMetric($"Cột Sấm Sét Shaman ({data.ActiveShamanTotems})", Mathf.Clamp01(data.ActiveShamanTotems / 4f));
            DrawMiniMetric($"Bẫy Chông Gai Độc ({data.ActiveSpikeTraps})", Mathf.Clamp01(data.ActiveSpikeTraps / 6f));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            DrawSeparator();
            // 3. Walls & Tactical Breach Memory
            GUILayout.Space(4);
            GUILayout.Label("3. GIÁM SÁT ĐỘT PHÁ TƯỜNG THÀNH (TACTICAL BREACH MEMORY)", _subHeaderStyle);
            GUILayout.BeginVertical(_cardBoxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Tổng số phân đoạn tường: <b>{data.TotalWalls}</b>", _labelStyle);
            GUILayout.Label($"Điểm tường đã bị phá hủy: <color=#FF6666><b>{data.BreachedWalls}</b></color>", _labelStyle);
            GUILayout.EndHorizontal();
            if (data.BreachedWalls > 0)
            {
                GUILayout.Label($"⚠️ <color=#FFAA00><b>PHÒNG TUYẾN BỊ CHỌC THỦNG:</b></color> AI đang điều động {data.HunterCount} Chiến binh chiếm giữ các vị trí cửa đột phá (Breach Slots: {data.ActiveBreachSlots}) để ngăn khủng long tiếp cận Nhà Chính!", _labelStyle);
            }
            else
            {
                GUILayout.Label("🛡️ <color=#55FF88><b>PHÒNG TUYẾN NGUYÊN VẸN:</b></color> Toàn bộ tường thành kiên cố đang bao bọc cổng làng và chặn đứng các đợt càn quét trực diện.", _labelStyle);
            }
            GUILayout.EndVertical();
            GUILayout.Space(6);
            DrawSeparator();
            // 4. Player Deployed Dinos Threat Breakdown
            GUILayout.Space(4);
            GUILayout.Label("4. PHÂN TÍCH QUÂN BÀI KHỦNG LONG CỦA NGƯỜI CHƠI", _subHeaderStyle);
            DrawProgressBar("🦖 Velociraptor (Runner Bầy Đàn)", Mathf.Clamp01(data.PlayerRaptors / 8f), $"{data.PlayerRaptors} con", new Color(1f, 0.65f, 0.2f));
            DrawProgressBar("🛡️ Ankylosaurus (Siege Tank Phá Tường)", Mathf.Clamp01(data.PlayerTanks / 4f), $"{data.PlayerTanks} con", new Color(0.4f, 0.85f, 0.4f));
            DrawProgressBar("👑 T-Rex Boss (Apex Sát Thủ)", Mathf.Clamp01(data.PlayerTRexes / 2f), $"{data.PlayerTRexes} con", new Color(1f, 0.25f, 0.35f));
            GUILayout.Space(6);
            DrawSeparator();
            // 5. Explainable AI Defense Rationale
            GUILayout.Space(4);
            GUILayout.Label("5. CHIẾN THUẬT PHẢN ỨNG CỦA AI PHÒNG THỦ LÀNG (EXPLAINABLE AI)", _subHeaderStyle);
            GUILayout.Box(data.TacticalRationale, _rationaleBoxStyle, GUILayout.MinHeight(54));
        }
        private void DrawProgressBar(string label, float val, string displayValue, Color fillCol)
        {
            val = Mathf.Clamp01(val);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(190));
            Rect r = GUILayoutUtility.GetRect(120, 14, GUILayout.ExpandWidth(true));
            GUI.Box(r, GUIContent.none, _barBgStyle);
            Rect fillR = new Rect(r.x, r.y, r.width * val, r.height);
            Color oldCol = GUI.color;
            GUI.color = fillCol;
            GUI.DrawTexture(fillR, _barFillTex);
            GUI.color = oldCol;
            GUILayout.Label(displayValue, _boldLabelStyle, GUILayout.Width(50));
            GUILayout.EndHorizontal();
        }
        private void DrawMiniMetric(string name, float val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _labelStyle, GUILayout.Width(110));
            Rect r = GUILayoutUtility.GetRect(60, 10, GUILayout.ExpandWidth(true));
            GUI.Box(r, GUIContent.none, _barBgStyle);
            Rect fillR = new Rect(r.x, r.y, r.width * Mathf.Clamp01(val), r.height);
            Color oldCol = GUI.color;
            GUI.color = new Color(0.35f, 0.85f, 1f, 0.85f);
            GUI.DrawTexture(fillR, _barFillTex);
            GUI.color = oldCol;
            GUILayout.EndHorizontal();
        }
        private void DrawSeparator()
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1), GUILayout.ExpandWidth(true));
            Color old = GUI.color;
            GUI.color = new Color(0.2f, 0.3f, 0.45f, 0.5f);
            GUI.DrawTexture(r, _barFillTex);
            GUI.color = old;
        }
        private void OnDestroy()
        {
            if (_panelTex != null) Destroy(_panelTex);
            if (_barBgTex != null) Destroy(_barBgTex);
            if (_barFillTex != null) Destroy(_barFillTex);
            if (_cardBgTex != null) Destroy(_cardBgTex);
        }
    }
}
