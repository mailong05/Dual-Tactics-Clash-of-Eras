using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
using UnityEngine.Networking;

namespace LlamAcademy.Dinos.Cloud
{
    /// <summary>
    /// Connects Unity to the containerized IO Cloud & Deep Learning AI Director Backend.
    /// Supports configurable Cloud/LAN endpoints (PC & Android APK), real-time telemetry streaming,
    /// remote neural inference, persistent Cloud Save/Load, global Leaderboard, and Live-Ops config.
    /// </summary>
    public class IOCloudManager : MonoBehaviour
    {
        public static IOCloudManager Instance { get; private set; }

        private const string PrefCloudUrlKey = "IOCloud_BaseUrl";

        [Header("IO Cloud Service Configuration")]
        [SerializeField] private string CloudBaseUrl = "http://localhost:8000";
        [SerializeField] private float TimeoutSeconds = 3.5f;
        [SerializeField] private bool EnableCloudAiPrediction = true;

        [Header("Cloud State")]
        public bool IsCloudConnected { get; private set; } = false;
        public string LastTacticalRationale { get; private set; } = "Local AI Director standby.";
        public float LastVulnerabilityScore { get; private set; } = 0.5f;
        public string ActiveEventName { get; private set; } = "";
        public string LastSyncMessage { get; private set; } = "";
        public RemoteConfigResponse CurrentRemoteConfig { get; private set; }

        public event Action<bool> OnCloudStatusChanged;
        public event Action<DeepLearningWaveResponse> OnDeepLearningWaveReceived;
        public event Action<CloudSaveResponse> OnCloudSaveLoaded;
        public Func<(string playerName, int coins, int highestWave, string mode)> OnCollectSaveSnapshot;

        [Header("Cloud HUD Status")]
        [SerializeField] private bool ShowCloudBadge = true;

        private bool _isPanelExpanded = false;
        private string _editableUrl = "";
        private readonly List<LeaderboardEntry> _cachedLeaderboard = new();

        private GUIStyle _cloudStatusStyle;
        private GUIStyle _cloudSubStyle;
        private GUIStyle _cloudRationaleStyle;

        public string ActiveCloudBaseUrl => string.IsNullOrWhiteSpace(CloudBaseUrl)
            ? "http://localhost:8000"
            : CloudBaseUrl.Trim().TrimEnd('/');

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            string savedUrl = PlayerPrefs.GetString(PrefCloudUrlKey, "");
            if (!string.IsNullOrWhiteSpace(savedUrl))
            {
                CloudBaseUrl = savedUrl.Trim().TrimEnd('/');
            }
            _editableUrl = ActiveCloudBaseUrl;
        }

        private void Start()
        {
            StartCoroutine(CheckCloudLiveness());
        }

        /// <summary>
        /// Updates the target Cloud/Docker endpoint URL (persisted across sessions for Android APK & PC).
        /// </summary>
        public void SetCloudBaseUrl(string newUrl, bool reconnectImmediately = true)
        {
            if (string.IsNullOrWhiteSpace(newUrl)) return;
            CloudBaseUrl = newUrl.Trim().TrimEnd('/');
            _editableUrl = CloudBaseUrl;
            PlayerPrefs.SetString(PrefCloudUrlKey, CloudBaseUrl);
            PlayerPrefs.Save();

            if (reconnectImmediately)
            {
                StartCoroutine(CheckCloudLiveness());
            }
        }

        public IEnumerator CheckCloudLiveness()
        {
            string url = $"{ActiveCloudBaseUrl}/health";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = Mathf.CeilToInt(TimeoutSeconds);
                yield return req.SendWebRequest();

                bool prev = IsCloudConnected;
                IsCloudConnected = (req.result == UnityWebRequest.Result.Success);

                if (IsCloudConnected != prev)
                {
                    OnCloudStatusChanged?.Invoke(IsCloudConnected);
                }

                if (IsCloudConnected)
                {
                    LastSyncMessage = $"Đã kết nối {ActiveCloudBaseUrl}";
                    Debug.Log($"<color=green>[IO Cloud]</color> Successfully connected to IO Cloud Backend at {ActiveCloudBaseUrl}!");
                    StartCoroutine(FetchRemoteConfigRoutine());
                    StartCoroutine(FetchLeaderboardRoutine(5));
                }
                else
                {
                    LastSyncMessage = "Offline - dùng AI On-Device dự phòng.";
                    Debug.LogWarning($"<color=yellow>[IO Cloud]</color> Cloud backend offline ({ActiveCloudBaseUrl}). Seamlessly falling back to onboard AI Director.");
                }
            }
        }

        /// <summary>
        /// Collects real-time telemetry, logs it to IO Cloud analytics, and requests counter-wave prediction.
        /// </summary>
        public void RequestDeepLearningWave(int roundNumber, Action<DeepLearningWaveResponse> callback = null)
        {
            if (!EnableCloudAiPrediction || !IsCloudConnected)
            {
                DeepLearningWaveResponse localResp = GenerateLocalNeuralHeuristics(roundNumber);
                LastTacticalRationale = localResp.tactical_rationale;
                LastVulnerabilityScore = localResp.vulnerability_score;
                OnDeepLearningWaveReceived?.Invoke(localResp);
                callback?.Invoke(localResp);
                return;
            }

            TelemetryPayload payload = BuildCurrentTelemetryPayload(roundNumber);
            StartCoroutine(PostTelemetryRoutine(payload));
            StartCoroutine(PostPredictWaveRoutine(payload, callback));
        }

        private TelemetryPayload BuildCurrentTelemetryPayload(int roundNumber)
        {
            int watchtowers = FindObjectsByType<ArcherTower>(FindObjectsSortMode.None).Length;
            int ballistas = FindObjectsByType<BallistaTower>(FindObjectsSortMode.None).Length;
            int catapults = FindObjectsByType<CatapultTower>(FindObjectsSortMode.None).Length;
            int shamans = FindObjectsByType<TeslaTower>(FindObjectsSortMode.None).Length;
            int tarpits = FindObjectsByType<FrostTower>(FindObjectsSortMode.None).Length;
            int spiketraps = FindObjectsByType<GroundTrap>(FindObjectsSortMode.None).Length;
            int barricades = FindObjectsByType<Wall>(FindObjectsSortMode.None).Length;

            int currentGold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 200;
            int baseHp = 100;

            return new TelemetryPayload
            {
                player_id = SystemInfo.deviceUniqueIdentifier,
                round = Mathf.Max(1, roundNumber),
                gold = Mathf.Max(0, currentGold),
                base_hp = baseHp,
                game_mode = PrehistoricGameModeManager.Instance != null
                    ? PrehistoricGameModeManager.Instance.CurrentMode.ToString()
                    : "TowerDefense",
                watchtowers = watchtowers,
                ballistas = ballistas,
                catapults = catapults,
                shamans = shamans,
                tarpits = tarpits,
                spiketraps = spiketraps,
                barricades = barricades,
                avg_survival_sec = 8.5f,
                defense_density = Mathf.Clamp(barricades + spiketraps + tarpits, 0, 10) / 2.0f
            };
        }

        private DeepLearningWaveResponse GenerateLocalNeuralHeuristics(int roundNumber)
        {
            var pred = AI.PrehistoricNeuralDirector.Instance.Predict(roundNumber);
            return new DeepLearningWaveResponse
            {
                model_type = "PrehistoricTacticsNet (On-Device Neural Engine)",
                device = "Local_CPU",
                recommended_wave_size = pred.RecommendedWaveSize,
                tactical_rationale = pred.TacticalRationale,
                vulnerability_score = pred.VulnerabilityScore
            };
        }

        private IEnumerator PostTelemetryRoutine(TelemetryPayload payload)
        {
            string url = $"{ActiveCloudBaseUrl}/api/v1/cloud/telemetry";
            string jsonBody = JsonUtility.ToJson(payload);

            using (UnityWebRequest req = CreateJsonPost(url, jsonBody, 3))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("<color=cyan>[IO Cloud Telemetry]</color> Battle metrics logged to persistent backend.");
                }
            }
        }

        private IEnumerator PostPredictWaveRoutine(TelemetryPayload payload, Action<DeepLearningWaveResponse> callback)
        {
            string url = $"{ActiveCloudBaseUrl}/api/v1/ai/predict-wave";
            string jsonBody = JsonUtility.ToJson(payload);

            using (UnityWebRequest req = CreateJsonPost(url, jsonBody, Mathf.CeilToInt(TimeoutSeconds)))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        DeepLearningWaveResponse resp = JsonUtility.FromJson<DeepLearningWaveResponse>(req.downloadHandler.text);
                        LastTacticalRationale = resp.tactical_rationale;
                        LastVulnerabilityScore = resp.vulnerability_score;

                        Debug.Log($"<color=cyan>[Deep Learning AI]</color> Model recommended wave size: {resp.recommended_wave_size} | Rationale: {resp.tactical_rationale}");
                        OnDeepLearningWaveReceived?.Invoke(resp);
                        callback?.Invoke(resp);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[IO Cloud] Error parsing Deep Learning response: {ex.Message}");
                        callback?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogWarning($"[IO Cloud] Prediction request failed: {req.error}. Using onboard fallback.");
                    SetDisconnectedState();
                    callback?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// Saves player progression persistently to IO Cloud (/api/v1/cloud/save).
        /// </summary>
        public void SaveProgressToCloud(string playerName, int coins, int highestWave, string selectedMode, Action<bool, string> onComplete = null)
        {
            StartCoroutine(SaveProgressRoutine(playerName, coins, highestWave, selectedMode, onComplete));
        }

        private IEnumerator SaveProgressRoutine(string playerName, int coins, int highestWave, string selectedMode, Action<bool, string> onComplete)
        {
            string url = $"{ActiveCloudBaseUrl}/api/v1/cloud/save";
            CloudSaveRequest payload = new CloudSaveRequest
            {
                player_id = SystemInfo.deviceUniqueIdentifier,
                player_name = string.IsNullOrEmpty(playerName) ? "Prehistoric Chief" : playerName,
                unlocked_towers = new[] { "Watchtower", "Ballista", "Catapult", "TeslaTower", "TarPit", "SpikeTrap", "Barricade" },
                unlocked_dinos = new[] { "Velociraptor", "Pterodactyl", "Ankylosaurus", "TRex" },
                coins = Mathf.Max(0, coins),
                highest_wave = Mathf.Max(1, highestWave),
                selected_mode = string.IsNullOrEmpty(selectedMode) ? "TowerDefense" : selectedMode
            };

            using (UnityWebRequest req = CreateJsonPost(url, JsonUtility.ToJson(payload), 3))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    LastSyncMessage = $"Đã lưu Cloud ({coins} vàng, Wave {highestWave})";
                    onComplete?.Invoke(true, LastSyncMessage);
                }
                else
                {
                    LastSyncMessage = $"Lưu Cloud thất bại: {req.error}";
                    onComplete?.Invoke(false, LastSyncMessage);
                }
            }
        }

        /// <summary>
        /// Loads player progression from IO Cloud (/api/v1/cloud/load/{player_id}).
        /// </summary>
        public void LoadProgressFromCloud(Action<bool, CloudSaveResponse> onComplete = null)
        {
            StartCoroutine(LoadProgressRoutine(onComplete));
        }

        private IEnumerator LoadProgressRoutine(Action<bool, CloudSaveResponse> onComplete)
        {
            string url = $"{ActiveCloudBaseUrl}/api/v1/cloud/load/{UnityWebRequest.EscapeURL(SystemInfo.deviceUniqueIdentifier)}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 3;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    CloudSaveResponse loaded = JsonUtility.FromJson<CloudSaveResponse>(req.downloadHandler.text);
                    LastSyncMessage = $"Đã tải Cloud: {loaded.player_name} | {loaded.coins} vàng | Wave {loaded.highest_wave}";
                    OnCloudSaveLoaded?.Invoke(loaded);
                    onComplete?.Invoke(true, loaded);
                }
                else
                {
                    LastSyncMessage = $"Tải Cloud thất bại: {req.error}";
                    onComplete?.Invoke(false, null);
                }
            }
        }

        /// <summary>
        /// Sends final match score to IO Cloud Leaderboard and refreshes cached rankings.
        /// </summary>
        public void SubmitScoreToCloud(string playerName, int score, int waveReached)
        {
            if (!IsCloudConnected) return;
            StartCoroutine(SubmitScoreRoutine(playerName, score, waveReached));
        }

        private IEnumerator SubmitScoreRoutine(string playerName, int score, int waveReached)
        {
            string url = $"{ActiveCloudBaseUrl}/api/v1/cloud/leaderboard/submit";
            ScorePayload payload = new ScorePayload
            {
                player_id = SystemInfo.deviceUniqueIdentifier,
                player_name = string.IsNullOrEmpty(playerName) ? "Prehistoric Chief" : playerName,
                score = Mathf.Max(0, score),
                wave_reached = Mathf.Max(1, waveReached),
                mode = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance.CurrentMode.ToString() : "TowerDefense"
            };

            using (UnityWebRequest req = CreateJsonPost(url, JsonUtility.ToJson(payload), 3))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    LastSyncMessage = $"Đã ghi điểm {score} lên BXH Cloud!";
                    StartCoroutine(FetchLeaderboardRoutine(5));
                }
            }
        }

        public void FetchLeaderboard(int limit = 5)
        {
            StartCoroutine(FetchLeaderboardRoutine(limit));
        }

        private IEnumerator FetchLeaderboardRoutine(int limit)
        {
            string url = $"{ActiveCloudBaseUrl}/api/v1/cloud/leaderboard?limit={limit}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 3;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    LeaderboardResponse resp = JsonUtility.FromJson<LeaderboardResponse>(req.downloadHandler.text);
                    _cachedLeaderboard.Clear();
                    if (resp != null && resp.leaderboard != null)
                    {
                        _cachedLeaderboard.AddRange(resp.leaderboard);
                    }
                }
            }
        }

        private IEnumerator FetchRemoteConfigRoutine()
        {
            string url = $"{ActiveCloudBaseUrl}/api/v1/cloud/config";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 3;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    CurrentRemoteConfig = JsonUtility.FromJson<RemoteConfigResponse>(req.downloadHandler.text);
                    if (CurrentRemoteConfig != null)
                    {
                        ActiveEventName = CurrentRemoteConfig.event_name;
                    }
                }
            }
        }

        private UnityWebRequest CreateJsonPost(string url, string jsonBody, int timeoutSec)
        {
            UnityWebRequest req = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = timeoutSec;
            return req;
        }

        private void SetDisconnectedState()
        {
            if (IsCloudConnected)
            {
                IsCloudConnected = false;
                OnCloudStatusChanged?.Invoke(false);
            }
        }

        private void OnGUI()
        {
            if (!ShowCloudBadge) return;

            Mobile.MobileUIHelper.BeginScaling();
            try
            {
                DrawCloudOverlayHUD();
            }
            finally
            {
                Mobile.MobileUIHelper.EndScaling();
            }
        }

        private void DrawCloudOverlayHUD()
        {
            bool arBannerVisible = AR.ARTabletopController.Instance != null && AR.ARTabletopController.Instance.IsARModeActive;

            float width = _isPanelExpanded ? 430f : 380f;
            float baseHeight = (!string.IsNullOrEmpty(LastTacticalRationale) && !LastTacticalRationale.Contains("standby")) ? 62f : 44f;
            float height = _isPanelExpanded ? (baseHeight + 235f) : baseHeight;
            float x = (Mobile.MobileUIHelper.ScreenWidth - width) / 2f;
            float y = arBannerVisible ? 96f : 10f;

            Rect panelRect = new Rect(x, y, width, height);
            Mobile.MobileUIHelper.RegisterUIRect(panelRect);

            GUI.color = new Color(0.06f, 0.12f, 0.18f, 0.92f);
            GUI.Box(panelRect, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(x + 8f, y + 4f, width - 16f, height - 8f));
            GUILayout.BeginHorizontal();

            string statusColor = IsCloudConnected ? "#00FFAA" : "#FFB84D";
            string statusText = IsCloudConnected ? "ONLINE" : "STANDBY (Local AI)";
            GUILayout.Label($"<b>☁ IO CLOUD:</b> <color={statusColor}>{statusText}</color>", GetCloudStatusStyle());

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(IsCloudConnected ? "Ping ✓" : "Connect", GUILayout.Width(62), GUILayout.Height(18)))
            {
                StartCoroutine(CheckCloudLiveness());
            }
            if (GUILayout.Button(_isPanelExpanded ? "▴ Ẩn" : "▾ Chi tiết", GUILayout.Width(68), GUILayout.Height(18)))
            {
                _isPanelExpanded = !_isPanelExpanded;
                if (_isPanelExpanded && IsCloudConnected)
                {
                    StartCoroutine(FetchLeaderboardRoutine(5));
                }
            }
            GUILayout.EndHorizontal();

            string aiSubtitle = string.IsNullOrEmpty(ActiveEventName)
                ? "🧠 <b>AI DIRECTOR:</b> PrehistoricTacticsNet"
                : $"🧠 <b>AI DIRECTOR</b> | 🎉 {ActiveEventName}";
            GUILayout.Label(aiSubtitle, GetCloudSubStyle());

            if (!string.IsNullOrEmpty(LastTacticalRationale) && !LastTacticalRationale.Contains("standby"))
            {
                GUILayout.Label($"<i>\"{LastTacticalRationale}\"</i>", GetCloudRationaleStyle());
            }

            if (_isPanelExpanded)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Server URL:</b>", GetCloudSubStyle(), GUILayout.Width(72));
                _editableUrl = GUILayout.TextField(_editableUrl ?? "", GUILayout.Height(20));
                if (GUILayout.Button("Lưu", GUILayout.Width(42), GUILayout.Height(20)))
                {
                    SetCloudBaseUrl(_editableUrl, true);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("☁ Lưu Tiến Trình", GUILayout.Height(22)))
                {
                    if (OnCollectSaveSnapshot != null)
                    {
                        var snap = OnCollectSaveSnapshot.Invoke();
                        SaveProgressToCloud(snap.playerName, snap.coins, snap.highestWave, snap.mode);
                    }
                    else
                    {
                        int gold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 200;
                        SaveProgressToCloud("Chief Long", gold, 1,
                            PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance.CurrentMode.ToString() : "TowerDefense");
                    }
                }
                if (GUILayout.Button("☁ Tải Từ Cloud", GUILayout.Height(22)))
                {
                    LoadProgressFromCloud();
                }
                if (GUILayout.Button("🏆 Làm mới BXH", GUILayout.Height(22)))
                {
                    FetchLeaderboard(5);
                }
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(LastSyncMessage))
                {
                    GUILayout.Label($"<color=#93C5FD>{LastSyncMessage}</color>", GetCloudSubStyle());
                }

                GUILayout.Space(3);
                GUILayout.Label("<b>🏆 BẢNG XẾP HẠNG IO CLOUD:</b>", GetCloudSubStyle());
                if (_cachedLeaderboard.Count == 0)
                {
                    GUILayout.Label("<i>(Chưa có dữ liệu BXH hoặc đang offline)</i>", GetCloudRationaleStyle());
                }
                else
                {
                    for (int i = 0; i < Mathf.Min(_cachedLeaderboard.Count, 5); i++)
                    {
                        var row = _cachedLeaderboard[i];
                        GUILayout.Label($"{i + 1}. <b>{row.player_name}</b> — {row.score}đ (Wave {row.wave_reached}, {row.mode})", GetCloudSubStyle());
                    }
                }
            }

            GUILayout.EndArea();
        }

        private GUIStyle GetCloudStatusStyle()
        {
            if (_cloudStatusStyle == null)
            {
                _cloudStatusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    richText = true,
                    normal = { textColor = Color.white }
                };
            }
            return _cloudStatusStyle;
        }

        private GUIStyle GetCloudSubStyle()
        {
            if (_cloudSubStyle == null)
            {
                _cloudSubStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    richText = true,
                    normal = { textColor = new Color(0.75f, 0.92f, 1f) }
                };
            }
            return _cloudSubStyle;
        }

        private GUIStyle GetCloudRationaleStyle()
        {
            if (_cloudRationaleStyle == null)
            {
                _cloudRationaleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    wordWrap = true,
                    richText = true,
                    normal = { textColor = new Color(1f, 0.85f, 0.4f) }
                };
            }
            return _cloudRationaleStyle;
        }
    }

    [Serializable]
    public class TelemetryPayload
    {
        public string player_id;
        public int round;
        public int gold;
        public int base_hp;
        public string game_mode;
        public int watchtowers;
        public int ballistas;
        public int catapults;
        public int shamans;
        public int tarpits;
        public int spiketraps;
        public int barricades;
        public float avg_survival_sec;
        public float defense_density;
    }

    [Serializable]
    public class DeepLearningWaveResponse
    {
        public string model_type;
        public string device;
        public float vulnerability_score;
        public int recommended_wave_size;
        public string tactical_rationale;
    }

    [Serializable]
    public class ScorePayload
    {
        public string player_id;
        public string player_name;
        public int score;
        public int wave_reached;
        public string mode;
    }

    [Serializable]
    public class CloudSaveRequest
    {
        public string player_id;
        public string player_name;
        public string[] unlocked_towers;
        public string[] unlocked_dinos;
        public int coins;
        public int highest_wave;
        public string selected_mode;
    }

    [Serializable]
    public class CloudSaveResponse
    {
        public string player_id;
        public string player_name;
        public string[] unlocked_towers;
        public string[] unlocked_dinos;
        public int coins;
        public int highest_wave;
        public string selected_mode;
        public double last_saved;
    }

    [Serializable]
    public class LeaderboardEntry
    {
        public string player_id;
        public string player_name;
        public int score;
        public int wave_reached;
        public string mode;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public LeaderboardEntry[] leaderboard;
        public int count;
    }

    [Serializable]
    public class RemoteConfigResponse
    {
        public string event_name;
        public float gold_multiplier;
        public float dino_speed_multiplier;
        public float boss_health_multiplier;
        public string server_message;
    }
}
