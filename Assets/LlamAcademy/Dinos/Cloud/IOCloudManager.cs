using System;
using System.Collections;
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
    /// Handles real-time telemetry streaming, remote neural inference, and cloud save synchronization.
    /// </summary>
    public class IOCloudManager : MonoBehaviour
    {
        public static IOCloudManager Instance { get; private set; }

        [Header("IO Cloud Service Configuration")]
        [SerializeField] private string CloudBaseUrl = "http://localhost:8000";
        [SerializeField] private float TimeoutSeconds = 3.5f;
        [SerializeField] private bool EnableCloudAiPrediction = true;

        [Header("Cloud State")]
        public bool IsCloudConnected { get; private set; } = false;
        public string LastTacticalRationale { get; private set; } = "Local AI Director standby.";
        public float LastVulnerabilityScore { get; private set; } = 0.5f;

        public event Action<bool> OnCloudStatusChanged;
        public event Action<DeepLearningWaveResponse> OnDeepLearningWaveReceived;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(CheckCloudLiveness());
        }

        public IEnumerator CheckCloudLiveness()
        {
            string url = $"{CloudBaseUrl}/health";
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
                    Debug.Log("<color=green>[IO Cloud]</color> Successfully connected to Docker Deep Learning AI Backend!");
                }
                else
                {
                    Debug.LogWarning("<color=yellow>[IO Cloud]</color> Cloud backend offline. Seamlessly falling back to onboard Heuristic AI Director.");
                }
            }
        }

        /// <summary>
        /// Collects real-time telemetry and requests counter-wave distribution from the Deep Learning model.
        /// </summary>
        public void RequestDeepLearningWave(int roundNumber, Action<DeepLearningWaveResponse> callback = null)
        {
            if (!EnableCloudAiPrediction || !IsCloudConnected)
            {
                callback?.Invoke(null);
                return;
            }

            StartCoroutine(PostPredictWaveRoutine(roundNumber, callback));
        }

        private IEnumerator PostPredictWaveRoutine(int roundNumber, Action<DeepLearningWaveResponse> callback)
        {
            string url = $"{CloudBaseUrl}/api/v1/ai/predict-wave";

            // Count player defenses in the scene
            int watchtowers = FindObjectsByType<ArcherTower>(FindObjectsSortMode.None).Length;
            int ballistas = FindObjectsByType<BallistaTower>(FindObjectsSortMode.None).Length;
            int catapults = FindObjectsByType<CatapultTower>(FindObjectsSortMode.None).Length;
            int shamans = FindObjectsByType<TeslaTower>(FindObjectsSortMode.None).Length;
            int tarpits = FindObjectsByType<FrostTower>(FindObjectsSortMode.None).Length;
            int spiketraps = FindObjectsByType<GroundTrap>(FindObjectsSortMode.None).Length;
            int barricades = FindObjectsByType<Wall>(FindObjectsSortMode.None).Length;

            int currentGold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 200;
            int baseHp = 100;

            TelemetryPayload payload = new TelemetryPayload
            {
                player_id = SystemInfo.deviceUniqueIdentifier,
                round = roundNumber,
                gold = currentGold,
                base_hp = baseHp,
                game_mode = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance.CurrentMode.ToString() : "TowerDefense",
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

            string jsonBody = JsonUtility.ToJson(payload);

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.CeilToInt(TimeoutSeconds);

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string respText = req.downloadHandler.text;
                        DeepLearningWaveResponse resp = JsonUtility.FromJson<DeepLearningWaveResponse>(respText);
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
                    IsCloudConnected = false;
                    OnCloudStatusChanged?.Invoke(false);
                    callback?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// Sends final match score to IO Cloud Leaderboard
        /// </summary>
        public void SubmitScoreToCloud(string playerName, int score, int waveReached)
        {
            if (!IsCloudConnected) return;

            StartCoroutine(SubmitScoreRoutine(playerName, score, waveReached));
        }

        private IEnumerator SubmitScoreRoutine(string playerName, int score, int waveReached)
        {
            string url = $"{CloudBaseUrl}/api/v1/cloud/leaderboard/submit";
            ScorePayload payload = new ScorePayload
            {
                player_id = SystemInfo.deviceUniqueIdentifier,
                player_name = playerName,
                score = score,
                wave_reached = waveReached,
                mode = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance.CurrentMode.ToString() : "TowerDefense"
            };

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 3;
                yield return req.SendWebRequest();
            }
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
}
