using System;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.UI
{
    /// <summary>
    /// Explainable AI (XAI) Holographic Monitor HUD.
    /// Visualizes real-time Neural Network telemetry, Softmax distribution, Spatial Route Bias (Q-Learning),
    /// Deep Learning Mazing adaptation, and Dynamic Difficulty Adjustment (DDA) metrics directly in-game.
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
            if (_IsOpen && PrehistoricNeuralDirector.Instance != null)
            {
                int currentWave = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance.CurrentWave : 1;
                PrehistoricNeuralDirector.Instance.Predict(currentWave);
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
                float winWidth = Mathf.Min(600f, screenW * 0.95f);
                float winHeight = Mathf.Min(650f, screenH * 0.92f);
                float winX = (screenW - winWidth) * 0.5f;
                float winY = (screenH - winHeight) * 0.5f;

                Rect winRect = new Rect(winX, winY, winWidth, winHeight);
                Mobile.MobileUIHelper.RegisterUIRect(winRect);

                GUILayout.BeginArea(winRect, _windowStyle);

                _scrollPos = GUILayout.BeginScrollView(_scrollPos);

                // 1. Header
                GUILayout.Label("🧠 PREHISTORIC TACTICS NET - AI DIRECTOR", _headerStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#55FF88>● ON-DEVICE NEURAL INFERENCE (C#)</color>", _labelStyle);
                float latency = PrehistoricNeuralDirector.Instance != null ? PrehistoricNeuralDirector.Instance.LastInferenceLatencyMs : 0.05f;
                GUILayout.Label($"<color=#AAAAAA>⚡ Latency: {latency:F2} ms | Edge AI</color>", _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.Space(4);
                DrawSeparator();

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
                    // 1. Multi-Task Head 1: Dinosaur Archetype Probabilities (Softmax)
                    // =========================================================================
                    GUILayout.Space(4);
                    GUILayout.Label("1. DỰ ĐOÁN PHỐI HỢP QUÁI VẬT (SOFTMAX DISTRIBUTION)", _subHeaderStyle);

                    string[] archNames = { "Velociraptor (Runner)", "Pterodactyl (Flyer)", "Ankylosaurus (Tank)", "T-Rex (Apex Boss)" };
                    Color[] archColors = {
                        new Color(1f, 0.7f, 0.2f),   // Orange
                        new Color(0.3f, 0.8f, 1f),   // Cyan
                        new Color(0.4f, 1f, 0.4f),   // Lime
                        new Color(1f, 0.35f, 0.35f)  // Red
                    };

                    for (int i = 0; i < archNames.Length; i++)
                    {
                        float prob = pred.ArchetypeDistribution != null && i < pred.ArchetypeDistribution.Length ? pred.ArchetypeDistribution[i] : 0f;
                        DrawProgressBar(archNames[i], prob, $"{prob * 100f:F1}%", archColors[i]);
                    }

                    GUILayout.Space(6);
                    DrawSeparator();

                    // =========================================================================
                    // 2. Multi-Task Head 2 & Reinforcement Learning: Route Spatial Policy (Q-Learning)
                    // =========================================================================
                    GUILayout.Space(4);
                    GUILayout.Label("2. HỌC TĂNG CƯỜNG CHỌN CỔNG (Q-LEARNING SPATIAL POLICY)", _subHeaderStyle);
                    GUILayout.Label("<color=#38BDF8>● Thuật toán: Q(s, a) ← Q(s, a) + α[R + γ max Q' - Q]  |  α=0.25, γ=0.85, ε=0.15</color>", _labelStyle);
                    GUILayout.Label($"<color=#4ADE80>● Tích hợp Deadliness Heatmap</color> | Huấn luyện: <color=#FDE047><b>{pred.RlEpisodes} episodes</b></color>", _labelStyle);

                    string[] routeNames = { "Cổng Bắc (Trung Tâm)", "Cánh Đông (Đột Kích Sườn)", "Sườn Tây (Men Vách Núi)" };
                    Color[] routeColors = {
                        new Color(0.85f, 0.5f, 1f),
                        new Color(0.35f, 0.85f, 1f),
                        new Color(1f, 0.75f, 0.35f)
                    };

                    for (int i = 0; i < routeNames.Length; i++)
                    {
                        float prob = pred.RouteDistribution != null && i < pred.RouteDistribution.Length ? pred.RouteDistribution[i] : 0.33f;
                        float qVal = pred.QValues != null && i < pred.QValues.Length ? pred.QValues[i] : 0f;
                        int deaths = pred.RouteDeaths != null && i < pred.RouteDeaths.Length ? pred.RouteDeaths[i] : 0;
                        int hits = pred.RouteHits != null && i < pred.RouteHits.Length ? pred.RouteHits[i] : 0;

                        DrawProgressBar(routeNames[i], prob, $"{prob * 100f:F1}%", routeColors[i]);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(195);
                        GUILayout.Label($"<size=10><color=#94A3B8>Q: <color=#38BDF8><b>{qVal:+0.0;-0.0;0.0}</b></color> | ☠ Tử vong: <color=#F87171><b>{deaths}</b></color> (-8đ) | 🎯 Đột kích: <color=#4ADE80><b>{hits}</b></color> (+30đ)</color></size>", _labelStyle);
                        GUILayout.EndHorizontal();
                    }

                    if (!string.IsNullOrEmpty(pred.RlTacticalExplanation))
                    {
                        GUILayout.Space(2);
                        GUILayout.Box($"<color=#E2E8F0><b>Phân Tích Q-Policy:</b> {pred.RlTacticalExplanation}</color>", _rationaleBoxStyle);
                    }

                    GUILayout.Space(6);
                    DrawSeparator();

                    // =========================================================================
                    // 3. Multi-Task Head 3 & 4: Evaluation & DDA Engine
                    // =========================================================================
                    GUILayout.Space(4);
                    GUILayout.Label("3. THÍCH ỨNG ĐỘ KHÓ & ĐÁNH GIÁ SƠ HỞ (DDA & VULNERABILITY)", _subHeaderStyle);

                    DrawProgressBar("Điểm Yếu Phòng Tuyến (Vulnerability)", pred.VulnerabilityScore, $"{pred.VulnerabilityScore * 100f:F1}%",
                        pred.VulnerabilityScore > 0.5f ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 0.8f, 1f));

                    DrawProgressBar("Kỹ Năng Người Chơi (DDA Player Skill)", pred.PlayerSkillRating, $"{pred.PlayerSkillRating * 100f:F1}%",
                        new Color(0.4f, 1f, 0.7f));

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Hệ số áp lực: <color=#FFFF55><b>{pred.DifficultyMultiplier:F2}x</b></color>", _boldLabelStyle);
                    GUILayout.Label($"Quy mô đợt quái đề xuất: <color=#55FFFF><b>{pred.RecommendedWaveSize} con</b></color>", _boldLabelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.Space(6);
                    DrawSeparator();

                    // =========================================================================
                    // 4. Section 4: 12-Dimensional Telemetry Sensor Grid
                    // =========================================================================
                    GUILayout.Space(4);
                    GUILayout.Label("4. CẢM BIẾN TRẬN ĐẤU (12 INPUT TELEMETRY FEATURES)", _subHeaderStyle);
                    if (pred.InputFeatures != null && pred.InputFeatures.Length >= 12)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.BeginVertical(GUILayout.Width(winWidth * 0.46f));
                        DrawMiniMetric("Vàng tích lũy", pred.InputFeatures[0]);
                        DrawMiniMetric("Máu Căn Cứ", pred.InputFeatures[1]);
                        DrawMiniMetric("Tiến trình đợt", pred.InputFeatures[2]);
                        DrawMiniMetric("Chòi Cung Thủ", pred.InputFeatures[3]);
                        DrawMiniMetric("Tháp Nỏ Ballista", pred.InputFeatures[4]);
                        DrawMiniMetric("Pháo Bắn Đá", pred.InputFeatures[5]);
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

                GUILayout.Space(10);

                // 6. Footer Action Buttons
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⟳ CẬP NHẬT SUY LUẬN", _buttonStyle, GUILayout.Height(32)))
                {
                    int currentWave = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance.CurrentWave : 1;
                    PrehistoricNeuralDirector.Instance?.Predict(currentWave);
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
