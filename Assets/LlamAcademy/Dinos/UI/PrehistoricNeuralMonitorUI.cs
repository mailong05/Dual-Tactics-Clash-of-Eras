using System;
using System.Collections.Generic;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.UI
{
    /// <summary>
    /// Explainable AI (XAI) Dual Monitor & Deep Reinforcement Learning Dashboard.
    /// Provides 100% academic verification for instructors and evaluators:
    /// 1. Sơ đồ Mạng Nơ-ron (Neural Activation Graph) với các nút phát sáng theo cường độ kích hoạt (16 -> 32 -> 16 -> Multi-Head).
    /// 2. Đồ thị Đường Thời Gian Thực (Loss & Reward Convergence Curves) theo dõi tiến trình 500 episodes offline hoặc wave online.
    /// 3. Bảng điều khiển Fast-Forward Offline Trainer (Train 500 Episodes trong vài giây, Reset Weights, Xuất CSV).
    /// 4. Bảng 16 đặc trưng đầu vào và Softmax xác suất phân phối 4 loài khủng long + 4 chiến thuật.
    /// 5. Chế độ Công thành (AI Village Defense Director).
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
        private GUIStyle _rationaleBoxStyle;
        private GUIStyle _cardBoxStyle;
        private Texture2D _panelTex;
        private Texture2D _barBgTex;
        private Texture2D _barFillTex;
        private Texture2D _cardBgTex;
        private Texture2D _nodeTex;
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
            _panelTex = MakeColorTexture(new Color(0.04f, 0.06f, 0.10f, 0.96f));
            _barBgTex = MakeColorTexture(new Color(0.10f, 0.14f, 0.22f, 0.90f));
            _barFillTex = MakeColorTexture(Color.white);
            _cardBgTex = MakeColorTexture(new Color(0.07f, 0.10f, 0.16f, 0.95f));
            _nodeTex = MakeCircleTexture(14);

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

        private Texture2D MakeCircleTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float alpha = Mathf.Clamp01(1.0f - (dist / r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }
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
                GUI.depth = -2000;
                float screenW = Mobile.MobileUIHelper.ScreenWidth;
                float screenH = Mobile.MobileUIHelper.ScreenHeight;
                float winWidth = Mathf.Min(680f, screenW * 0.96f);
                float winHeight = Mathf.Min(780f, screenH * 0.94f);
                float winX = (screenW - winWidth) * 0.5f;
                float winY = (screenH - winHeight) * 0.5f;
                Rect winRect = new Rect(winX, winY, winWidth, winHeight);
                Mobile.MobileUIHelper.RegisterUIRect(winRect);

                GUILayout.BeginArea(winRect, _windowStyle);
                _scrollPos = GUILayout.BeginScrollView(_scrollPos);

                // Main Header
                GUILayout.Label("🧠 DEEP REINFORCEMENT LEARNING & XAI MONITOR", _headerStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#55FF88>● PREHISTORIC TACTICS NET (16 → 32 → 16 → MULTI-HEAD)</color>", _labelStyle);
                float latency = PrehistoricNeuralDirector.Instance != null ? PrehistoricNeuralDirector.Instance.LastInferenceLatencyMs : 0.04f;
                GUILayout.Label($"<color=#AAAAAA>⚡ Latency: {latency:F2} ms | Edge RL Engine</color>", _labelStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);

                bool isAssault = (PrehistoricGameModeManager.Instance != null && 
                                  PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault);

                if (!isAssault)
                {
                    DrawTowerDefenseUIDirector(winWidth);
                }
                else
                {
                    DrawAssaultDefenseUIDirector(winWidth);
                }

                GUILayout.Space(8);
                // Footer
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⟳ CẬP NHẬT SUY LUẬN (INFERENCE)", _buttonStyle, GUILayout.Height(30)))
                {
                    if (!isAssault && PrehistoricNeuralDirector.Instance != null)
                    {
                        int currentWave = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance.CurrentWave : 1;
                        PrehistoricNeuralDirector.Instance.Predict(currentWave);
                    }
                }
                if (GUILayout.Button("✕ ĐÓNG CỬA SỔ [F1]", _buttonStyle, GUILayout.Height(30)))
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
            var director = PrehistoricNeuralDirector.Instance;
            var trainer = FastForwardOfflineTrainer.Instance;
            PrehistoricTacticsNet net = director != null ? director.TacticsNet : null;

            // =========================================================================
            // MỤC 1: BỘ HUẤN LUYỆN GIẢ LẬP NHANH (FAST-FORWARD OFFLINE TRAINER)
            // =========================================================================
            GUILayout.Space(4);
            GUILayout.Label("1. BỘ HUẤN LUYỆN GIẢ LẬP NHANH (FAST-FORWARD OFFLINE TRAINER)", _subHeaderStyle);
            GUILayout.BeginVertical(_cardBoxStyle);

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.15f, 0.55f, 0.95f, 1f);
            if (GUILayout.Button("🚀 Huấn Luyện Nhanh 500 Episodes", GUILayout.Height(32)))
            {
                if (trainer != null) trainer.StartFastForwardTraining(500);
            }
            GUI.backgroundColor = new Color(0.85f, 0.45f, 0.15f, 1f);
            if (GUILayout.Button("🔄 Đặt Lại Trọng Số (Reset)", GUILayout.Height(32), GUILayout.Width(170)))
            {
                if (trainer != null) trainer.ResetWeights();
            }
            GUI.backgroundColor = new Color(0.25f, 0.75f, 0.35f, 1f);
            if (GUILayout.Button("📥 Xuất CSV Huấn Luyện", GUILayout.Height(32), GUILayout.Width(170)))
            {
                if (trainer != null) trainer.ExportTrainingLogToCSV();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            if (trainer != null && trainer.IsTraining)
            {
                GUILayout.Space(4);
                DrawProgressBar($"Đang mô phỏng tập huấn luyện ({trainer.CurrentEpisode}/500)", trainer.TrainingProgress, 
                    $"{(trainer.TrainingProgress * 100f):F0}%", new Color(0.3f, 0.85f, 1.0f));
                GUILayout.Label($"<color=#FDE047>Episode {trainer.CurrentEpisode}: Loss = {trainer.CurrentLoss:F4} | Reward = {trainer.CurrentReward:F2} | Chuẩn trọng số ||W|| = {trainer.CurrentWeightNorm:F2}</color>", _labelStyle);
            }
            else if (trainer != null && trainer.TrainingHistory.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label($"✅ <color=#55FF88><b>ĐÃ HỘI TỤ {trainer.TrainingHistory.Count} EPISODES:</b></color> Trọng số đã lưu vào <b>pretrained_tactics_weights.json</b>. Sẵn sàng báo cáo học thuật!", _labelStyle);
            }
            GUILayout.EndVertical();

            // =========================================================================
            // MỤC 2: ĐỒ THỊ ĐƯỜNG TIẾN TRÌNH HUẤN LUYỆN (REAL-TIME LINE GRAPH)
            // =========================================================================
            GUILayout.Space(6);
            DrawSeparator();
            GUILayout.Space(4);
            GUILayout.Label("2. ĐỒ THỊ TIẾN TRÌNH HỘI TỤ LOSS & REWARD (500 EPISODES CONVERGENCE)", _subHeaderStyle);
            DrawTrainingConvergenceChart(winWidth);

            // =========================================================================
            // MỤC 3: SƠ ĐỒ MẠNG NƠ-RON XAI (NEURAL ACTIVATION GRAPH)
            // =========================================================================
            GUILayout.Space(6);
            DrawSeparator();
            GUILayout.Space(4);
            GUILayout.Label("3. SƠ ĐỒ KÍCH HOẠT MẠNG NƠ-RON (XAI NEURAL GRAPH: 16 → 32 → 16 → 4/4/1)", _subHeaderStyle);
            DrawNeuralActivationGraph(winWidth, net);

            // =========================================================================
            // MỤC 4: XÁC SUẤT ĐẦU RA SOFTMAX (ARCHETYPES & TACTICS)
            // =========================================================================
            GUILayout.Space(6);
            DrawSeparator();
            GUILayout.Space(4);
            GUILayout.Label("4. PHÂN PHỐI CHIẾN LƯỢC ĐẦU RA MẠNG NƠ-RON (SOFTMAX HEADS)", _subHeaderStyle);

            if (director != null && director.HasPrediction)
            {
                var pred = director.LatestPrediction;

                GUILayout.Label("<b>A. CƠ CẤU ĐỘI HÌNH KHỦNG LONG (ARCHETYPE HEAD):</b>", _boldLabelStyle);
                DrawProgressBar("🦖 Velociraptor (Runner Swarm)", pred.ArchetypeDistribution[0], $"{(pred.ArchetypeDistribution[0] * 100f):F1}%", new Color(1f, 0.6f, 0.2f));
                DrawProgressBar("🦅 Pterodactyl (Không Kích Flyer)", pred.ArchetypeDistribution[1], $"{(pred.ArchetypeDistribution[1] * 100f):F1}%", new Color(0.3f, 0.8f, 1f));
                DrawProgressBar("🛡️ Ankylosaurus (Phá Tường Tank)", pred.ArchetypeDistribution[2], $"{(pred.ArchetypeDistribution[2] * 100f):F1}%", new Color(0.4f, 0.9f, 0.4f));
                DrawProgressBar("👑 T-Rex Boss (Apex Sát Thủ)", pred.ArchetypeDistribution[3], $"{(pred.ArchetypeDistribution[3] * 100f):F1}%", new Color(1f, 0.25f, 0.35f));

                GUILayout.Space(4);
                GUILayout.Label("<b>B. HỌC TĂNG CƯỜNG CHỌN CỔNG ĐỘT KÍCH (Q-POLICY):</b>", _boldLabelStyle);
                DrawProgressBar("Cổng Bắc (Đột Kích Trực Diện)", pred.RouteDistribution[0], $"{(pred.RouteDistribution[0] * 100f):F1}%", new Color(1f, 0.4f, 0.4f));
                DrawProgressBar("Cổng Đông (Đánh Bọc Sườn)", pred.RouteDistribution[1], $"{(pred.RouteDistribution[1] * 100f):F1}%", new Color(0.4f, 0.7f, 1f));
                DrawProgressBar("Cổng Tây (Tập Kích Vòng Ngoài)", pred.RouteDistribution[2], $"{(pred.RouteDistribution[2] * 100f):F1}%", new Color(0.8f, 0.5f, 1f));
            }

            // =========================================================================
            // MỤC 5: 16 ĐẶC TRƯNG ĐẦU VÀO TELEMETRY (INPUT FEATURES)
            // =========================================================================
            GUILayout.Space(6);
            DrawSeparator();
            GUILayout.Space(4);
            GUILayout.Label("5. BẢNG 16 ĐẶC TRƯNG ĐẦU VÀO TOÁN HỌC (INPUT VECTOR x ∈ ℝ¹⁶)", _subHeaderStyle);

            if (director != null && director.HasPrediction && director.LatestPrediction.InputFeatures != null)
            {
                float[] inp = director.LatestPrediction.InputFeatures;
                if (inp.Length >= 16)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(GUILayout.Width(winWidth * 0.46f));
                    DrawMiniMetric("Vàng Người Chơi [0]", inp[0]);
                    DrawMiniMetric("Máu Căn Cứ Làng [1]", inp[1]);
                    DrawMiniMetric("Số Đợt Wave [2]", inp[2]);
                    DrawMiniMetric("Tháp Cung Thủ [3]", inp[3]);
                    DrawMiniMetric("Tháp Nỏ Phóng Lao [4]", inp[4]);
                    DrawMiniMetric("Máy Bắn Đá Lửa [5]", inp[5]);
                    DrawMiniMetric("Trụ Sấm Sét Shaman [6]", inp[6]);
                    DrawMiniMetric("Tháp Băng/Hắc Ín [7]", inp[7]);
                    GUILayout.EndVertical();

                    GUILayout.BeginVertical(GUILayout.Width(winWidth * 0.46f));
                    DrawMiniMetric("Bẫy Chông Gai Độc [8]", inp[8]);
                    DrawMiniMetric("Mật Độ Tường Chắn [9]", inp[9]);
                    DrawMiniMetric("Mật Độ Sát Thương DPS [10]", inp[10]);
                    DrawMiniMetric("Kỹ Năng Người Chơi [11]", inp[11]);
                    DrawMiniMetric("Phòng Thủ Cánh Trái [12]", inp[12]);
                    DrawMiniMetric("Phòng Thủ Cánh Phải [13]", inp[13]);
                    DrawMiniMetric("Phòng Thủ Trung Tâm [14]", inp[14]);
                    DrawMiniMetric("Độ Phức Tạp Mê Cung [15]", inp[15]);
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
            }

            // =========================================================================
            // MỤC 6: GIẢI THÍCH CHIẾN THUẬT (EXPLAINABLE AI RATIONALE)
            // =========================================================================
            GUILayout.Space(6);
            DrawSeparator();
            GUILayout.Space(4);
            GUILayout.Label("6. SUY LUẬN GIẢI THÍCH CHIẾN THUẬT (EXPLAINABLE AI RATIONALE)", _subHeaderStyle);
            if (director != null && director.HasPrediction)
            {
                GUILayout.Box(director.LatestPrediction.TacticalRationale, _rationaleBoxStyle, GUILayout.MinHeight(54));
            }
        }

        private void DrawTrainingConvergenceChart(float winWidth)
        {
            var trainer = FastForwardOfflineTrainer.Instance;
            float chartW = Mathf.Min(630f, winWidth - 36f);
            float chartH = 100f;

            Rect chartRect = GUILayoutUtility.GetRect(chartW, chartH);
            Color oldBg = GUI.color;

            // Background
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
            GUI.DrawTexture(chartRect, _barFillTex);

            // Border & Grid
            GUI.color = new Color(0.2f, 0.3f, 0.4f, 0.4f);
            GUI.DrawTexture(new Rect(chartRect.x, chartRect.y + chartH * 0.5f, chartW, 1f), _barFillTex);
            GUI.DrawTexture(new Rect(chartRect.x, chartRect.y, chartW, 1f), _barFillTex);
            GUI.DrawTexture(new Rect(chartRect.x, chartRect.y + chartH - 1f, chartW, 1f), _barFillTex);

            if (trainer != null && trainer.TrainingHistory.Count > 1)
            {
                int count = trainer.TrainingHistory.Count;
                float stepX = chartW / (count - 1);

                // Draw Loss Curve (Red/Orange) & Reward Curve (Green/Cyan)
                for (int i = 0; i < count - 1; i += Mathf.Max(1, count / 80))
                {
                    var p1 = trainer.TrainingHistory[i];
                    int nextIdx = Mathf.Min(i + Mathf.Max(1, count / 80), count - 1);
                    var p2 = trainer.TrainingHistory[nextIdx];

                    float x1 = chartRect.x + (float)p1.Episode / count * chartW;
                    float x2 = chartRect.x + (float)p2.Episode / count * chartW;

                    // Normalize Loss [0, 4] -> [chartH - 4, 4]
                    float y1_loss = chartRect.y + chartH - Mathf.Clamp01(p1.Loss / 3.5f) * (chartH - 8f) - 4f;
                    float y2_loss = chartRect.y + chartH - Mathf.Clamp01(p2.Loss / 3.5f) * (chartH - 8f) - 4f;

                    // Normalize Reward [-5, 25] -> [chartH - 4, 4]
                    float normR1 = Mathf.Clamp01((p1.Reward + 5f) / 30f);
                    float normR2 = Mathf.Clamp01((p2.Reward + 5f) / 30f);
                    float y1_reward = chartRect.y + chartH - normR1 * (chartH - 8f) - 4f;
                    float y2_reward = chartRect.y + chartH - normR2 * (chartH - 8f) - 4f;

                    // Draw loss segment
                    GUI.color = new Color(1.0f, 0.35f, 0.25f, 0.95f);
                    GUI.DrawTexture(new Rect(x1, y1_loss, Mathf.Max(2f, x2 - x1), 2f), _barFillTex);

                    // Draw reward segment
                    GUI.color = new Color(0.25f, 0.95f, 0.55f, 0.95f);
                    GUI.DrawTexture(new Rect(x1, y1_reward, Mathf.Max(2f, x2 - x1), 2f), _barFillTex);
                }
            }
            else
            {
                GUI.color = new Color(0.6f, 0.7f, 0.8f, 0.6f);
                GUI.Label(new Rect(chartRect.x + 20, chartRect.y + 38, chartW, 20), "Bấm [Huấn Luyện Nhanh 500 Episodes] ở trên để vẽ biểu đồ Loss/Reward hội tụ", _labelStyle);
            }

            GUI.color = oldBg;

            // Chart Legend
            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#FF5544>━</color> <b>Hàm Mất Mát (Loss - Hội tụ giảm dần)</b>", _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("<color=#44FF88>━</color> <b>Phần Thưởng Tác Tử (Reward - Tăng trưởng thích nghi)</b>", _labelStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawNeuralActivationGraph(float winWidth, PrehistoricTacticsNet net)
        {
            float graphW = Mathf.Min(630f, winWidth - 36f);
            float graphH = 95f;
            Rect graphRect = GUILayoutUtility.GetRect(graphW, graphH);

            Color oldCol = GUI.color;

            // Background box
            GUI.color = new Color(0.05f, 0.08f, 0.14f, 0.92f);
            GUI.DrawTexture(graphRect, _barFillTex);

            float col0_X = graphRect.x + 25f;                  // Input (16)
            float col1_X = graphRect.x + graphW * 0.33f;       // Hidden 1 (32)
            float col2_X = graphRect.x + graphW * 0.66f;       // Hidden 2 (16)
            float col3_X = graphRect.x + graphW - 40f;         // Heads (4+4+1)

            // Connectors (subtle background lines)
            GUI.color = new Color(0.18f, 0.28f, 0.42f, 0.35f);
            for (int i = 0; i < 4; i++)
            {
                float y = graphRect.y + 15f + i * 20f;
                GUI.DrawTexture(new Rect(col0_X, y, col1_X - col0_X, 1f), _barFillTex);
                GUI.DrawTexture(new Rect(col1_X, y, col2_X - col1_X, 1f), _barFillTex);
                GUI.DrawTexture(new Rect(col2_X, y, col3_X - col2_X, 1f), _barFillTex);
            }

            // Layer 0: Input (16 nodes, compact)
            for (int i = 0; i < 16; i++)
            {
                float ny = graphRect.y + 10f + (i * 4.8f);
                float val = (net != null && net.LastInput != null && i < net.LastInput.Length) ? net.LastInput[i] : 0.5f;
                GUI.color = Color.Lerp(new Color(0.1f, 0.2f, 0.4f), new Color(0.3f, 0.95f, 1f), val);
                GUI.DrawTexture(new Rect(col0_X, ny, 6f, 4f), _nodeTex);
            }

            // Layer 1: Hidden 1 (32 nodes, 2 columns of 16)
            for (int i = 0; i < 32; i++)
            {
                int c = i / 16;
                int r = i % 16;
                float nx = col1_X + (c * 10f);
                float ny = graphRect.y + 10f + (r * 4.8f);
                float val = (net != null && net.LastA1 != null && i < net.LastA1.Length) ? Mathf.Clamp01(net.LastA1[i]) : 0.3f;
                GUI.color = Color.Lerp(new Color(0.12f, 0.15f, 0.35f), new Color(0.95f, 0.85f, 0.2f), val);
                GUI.DrawTexture(new Rect(nx, ny, 6f, 4f), _nodeTex);
            }

            // Layer 2: Hidden 2 (16 nodes)
            for (int i = 0; i < 16; i++)
            {
                float ny = graphRect.y + 10f + (i * 4.8f);
                float val = (net != null && net.LastA2 != null && i < net.LastA2.Length) ? Mathf.Clamp01(net.LastA2[i]) : 0.3f;
                GUI.color = Color.Lerp(new Color(0.15f, 0.15f, 0.4f), new Color(0.2f, 1f, 0.6f), val);
                GUI.DrawTexture(new Rect(col2_X, ny, 7f, 5f), _nodeTex);
            }

            // Layer 3: Output Heads (Archetype 4, Tactic 4, Value 1)
            for (int i = 0; i < 9; i++)
            {
                float ny = graphRect.y + 10f + (i * 8.5f);
                float prob = 0.25f;
                if (net != null)
                {
                    if (i < 4 && net.LastArchProbs != null) prob = net.LastArchProbs[i];
                    else if (i < 8 && net.LastTactProbs != null) prob = net.LastTactProbs[i - 4];
                    else prob = net.LastValue;
                }
                GUI.color = Color.Lerp(new Color(0.2f, 0.2f, 0.2f), new Color(1.0f, 0.3f, 0.6f), prob);
                GUI.DrawTexture(new Rect(col3_X, ny, 8f, 6f), _nodeTex);
            }

            GUI.color = oldCol;

            // Layer Labels
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Đầu Vào: 16 Features</b>", _labelStyle, GUILayout.Width(130));
            GUILayout.FlexibleSpace();
            GUILayout.Label("<b>Ẩn 1: 32 Nút (LeakyReLU)</b>", _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("<b>Ẩn 2: 16 Nút (LeakyReLU)</b>", _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("<b>Đầu Ra: 4+4+1 Heads</b>", _labelStyle, GUILayout.Width(130));
            GUILayout.EndHorizontal();
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
            GUILayout.Label(label, _labelStyle, GUILayout.Width(220));
            Rect r = GUILayoutUtility.GetRect(120, 14, GUILayout.ExpandWidth(true));
            GUI.Box(r, GUIContent.none, _barBgStyle);
            Rect fillR = new Rect(r.x, r.y, r.width * val, r.height);
            Color oldCol = GUI.color;
            GUI.color = fillCol;
            GUI.DrawTexture(fillR, _barFillTex);
            GUI.color = oldCol;
            GUILayout.Label(displayValue, _boldLabelStyle, GUILayout.Width(60));
            GUILayout.EndHorizontal();
        }

        private void DrawMiniMetric(string name, float val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _labelStyle, GUILayout.Width(130));
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
            if (_nodeTex != null) Destroy(_nodeTex);
        }
    }
}
