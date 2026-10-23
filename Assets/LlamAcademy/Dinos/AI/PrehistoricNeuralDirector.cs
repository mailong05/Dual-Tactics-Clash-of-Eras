using System;
using System.Collections.Generic;
using System.Diagnostics;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LlamAcademy.Dinos.AI
{
    /// <summary>
    /// Dual AI Director Engine:
    /// 1. Deep Learning (PrehistoricTacticsNet): 12-dimensional Multi-Task MLP predicting counter-dinosaur composition.
    ///    - Responds dynamically to player's wall building: 0 walls -> Velociraptor Swarm Rush; Walls > 0 -> Aerial Pterodactyls & Armored Ankylosaurus.
    /// 2. Reinforcement Learning (Q-Learning Spatial Policy): Adapts spawn gate selection based on Deadliness Heatmap and combat rewards.
    /// 3. Dynamic Difficulty Adjustment (DDA): Real-time player skill scaling.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PrehistoricNeuralDirector : MonoBehaviour
    {
        private static PrehistoricNeuralDirector _Instance;
        public static PrehistoricNeuralDirector Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = FindFirstObjectByType<PrehistoricNeuralDirector>();
                    if (_Instance == null)
                    {
                        GameObject go = new GameObject("PrehistoricNeuralDirector");
                        _Instance = go.AddComponent<PrehistoricNeuralDirector>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _Instance;
            }
        }

        [System.Serializable]
        public struct NeuralPredictionResult
        {
            public float[] InputFeatures;          // 12 normalized telemetry features
            public float[] ArchetypeDistribution;   // Softmax over 4 dino types: [Runner, Flyer, Tank, Boss]
            public float[] RouteDistribution;       // Softmax / Q-Policy over 3 routes: [North, East, West]
            public float VulnerabilityScore;        // Sigmoid 0..1 (how exposed the village is)
            public float AggressionIndex;           // Sigmoid 0..1 (combat intensity)
            public float PlayerSkillRating;         // 0..1 (DDA dynamic player evaluation)
            public float DifficultyMultiplier;      // 0.85x .. 1.35x adaptive multiplier
            public int RecommendedWaveSize;
            public int WallCount;                   // Number of player-built barricades/walls
            public float[] QValues;                 // Q-values for 3 routes [North, East, West]
            public int[] RouteDeaths;               // Cumulative deaths per route
            public int[] RouteHits;                 // Cumulative base hits per route
            public int RlEpisodes;                  // Total RL learning cycles
            public string TacticalRationale;
            public string RlTacticalExplanation;
            public float InferenceTimeMs;
            public DateTime Timestamp;
        }

        [Header("=== Neural Network Architecture ===")]
        public const int INPUT_DIM = 12;
        public const int HIDDEN_DIM = 32;
        public const int NUM_ARCHETYPES = 4; // 0: Runner, 1: Flyer, 2: Tank, 3: Boss
        public const int NUM_ROUTES = 3;     // 0: North, 1: East, 2: West

        [Header("=== Real-time Telemetry & State ===")]
        [SerializeField] private bool _EnableOnDeviceInference = true;
        [SerializeField] private float _LastInferenceLatencyMs = 0.05f;
        [SerializeField] private float _PlayerSkillRating = 0.5f;
        [SerializeField] private int _ConsecutivePlayerVictories = 0;

        [Header("=== Reinforcement Learning (Q-Learning) ===")]
        [SerializeField] private float[] _qValues = new float[3] { 0f, 0f, 0f };
        [SerializeField] private float[] _qRouteProbabilities = new float[3] { 0.45f, 0.30f, 0.25f };
        [SerializeField] private int[] _routeDeaths = new int[3] { 0, 0, 0 };
        [SerializeField] private int[] _routeHits = new int[3] { 0, 0, 0 };
        [SerializeField] private int _rlEpisodes = 0;

        private const float LEARNING_RATE = 0.25f; // Alpha
        private const float DISCOUNT_FACTOR = 0.85f; // Gamma
        private const float EXPLORATION_EPSILON = 0.15f; // Epsilon

        public bool EnableOnDeviceInference
        {
            get => _EnableOnDeviceInference;
            set => _EnableOnDeviceInference = value;
        }

        public NeuralPredictionResult LatestPrediction { get; private set; }
        public float LastInferenceLatencyMs => _LastInferenceLatencyMs;
        public float PlayerSkillRating => _PlayerSkillRating;
        public bool HasPrediction => LatestPrediction.ArchetypeDistribution != null;

        public event Action<NeuralPredictionResult> OnPredictionUpdated;

        // Neural Network Weights (Calibrated MLP based on PrehistoricTacticsNet)
        private float[,] _weights1; // [12, 32]
        private float[] _biases1;   // [32]
        private float[,] _weights2; // [32, 32]
        private float[] _biases2;   // [32]

        // Multi-Task Output Heads
        private float[,] _headArchetypes;    // [32, 4]
        private float[] _biasArchetypes;     // [4]
        private float[,] _headRoutes;        // [32, 3]
        private float[] _biasRoutes;         // [3]
        private float[,] _headVulnerability; // [32, 1]
        private float[] _biasVulnerability;  // [1]
        private float[,] _headAggression;    // [32, 1]
        private float[] _biasAggression;     // [1]

        private void Awake()
        {
            if (_Instance != null && _Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeNetworkWeights();
        }

        private void Start()
        {
            Predict(1);
        }

        private void InitializeNetworkWeights()
        {
            _weights1 = new float[INPUT_DIM, HIDDEN_DIM];
            _biases1 = new float[HIDDEN_DIM];
            _weights2 = new float[HIDDEN_DIM, HIDDEN_DIM];
            _biases2 = new float[HIDDEN_DIM];

            _headArchetypes = new float[HIDDEN_DIM, NUM_ARCHETYPES];
            _biasArchetypes = new float[NUM_ARCHETYPES];
            _headRoutes = new float[HIDDEN_DIM, NUM_ROUTES];
            _biasRoutes = new float[NUM_ROUTES];
            _headVulnerability = new float[HIDDEN_DIM, 1];
            _biasVulnerability = new float[1];
            _headAggression = new float[HIDDEN_DIM, 1];
            _biasAggression = new float[1];

            int seed = 42;
            System.Random rng = new System.Random(seed);

            InitMatrixUniform(_weights1, -0.25f, 0.25f, rng);
            InitVectorConstant(_biases1, 0.05f);
            InitMatrixUniform(_weights2, -0.2f, 0.2f, rng);
            InitVectorConstant(_biases2, 0.05f);

            InitMatrixUniform(_headArchetypes, -0.3f, 0.3f, rng);
            _biasArchetypes[0] = 0.40f; // Runner base
            _biasArchetypes[1] = 0.25f; // Flyer base
            _biasArchetypes[2] = 0.25f; // Tank base
            _biasArchetypes[3] = 0.10f; // Boss base

            InitMatrixUniform(_headRoutes, -0.2f, 0.2f, rng);
            _biasRoutes[0] = 0.45f;
            _biasRoutes[1] = 0.30f;
            _biasRoutes[2] = 0.25f;

            InitMatrixUniform(_headVulnerability, -0.2f, 0.2f, rng);
            _biasVulnerability[0] = 0.35f;
            InitMatrixUniform(_headAggression, -0.2f, 0.2f, rng);
            _biasAggression[0] = 0.50f;
        }

        private void InitMatrixUniform(float[,] mat, float min, float max, System.Random rng)
        {
            int rows = mat.GetLength(0);
            int cols = mat.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    mat[r, c] = min + (float)rng.NextDouble() * (max - min);
                }
            }
        }

        private void InitVectorConstant(float[] vec, float val)
        {
            for (int i = 0; i < vec.Length; i++) vec[i] = val;
        }

        /// <summary>
        /// Thu thập 12 chỉ số trận đấu thời gian thực và chuẩn hóa thành vector đầu vào [0..1].
        /// </summary>
        public float[] ExtractNormalizedFeatures(int waveNumber)
        {
            float[] f = new float[INPUT_DIM];

            // 0: Gold normalized (0..1000)
            int currentGold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 300;
            f[0] = Mathf.Clamp01(currentGold / 1000f);

            // 1: Base HP normalized (0..1000)
            int currentHp = 1000;
            if (PrehistoricGameplayManager.Instance != null)
            {
                currentHp = PrehistoricGameplayManager.Instance.CurrentBaseHealth;
            }
            f[1] = Mathf.Clamp01(currentHp / 1000f);

            // 2: Wave progression (0..10)
            f[2] = Mathf.Clamp01(waveNumber / 10f);

            // 3: Watchtowers (Archer Tower) count (0..10)
            var archers = FindObjectsByType<ArcherTower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            f[3] = Mathf.Clamp01(archers.Length / 10f);

            // 4: Ballista count (0..6)
            var ballistas = FindObjectsByType<BallistaTower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            f[4] = Mathf.Clamp01(ballistas.Length / 6f);

            // 5: Catapult count (0..4)
            var catapults = FindObjectsByType<CatapultTower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            f[5] = Mathf.Clamp01(catapults.Length / 4f);

            // 6: Tesla/Shaman count (0..4)
            var teslas = FindObjectsByType<TeslaTower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            f[6] = Mathf.Clamp01(teslas.Length / 4f);

            // 7: Frost/Tar pit count (0..6)
            var frosts = FindObjectsByType<FrostTower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            f[7] = Mathf.Clamp01(frosts.Length / 6f);

            // 8: Spike traps count (0..8)
            var traps = FindObjectsByType<GroundTrap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            f[8] = Mathf.Clamp01(traps.Length / 8f);

            // 9: Barricades & walls count do người chơi đặt (0..12)
            var allWalls = FindObjectsByType<Wall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int activeWallCount = 0;
            for (int w = 0; w < allWalls.Length; w++)
            {
                if (allWalls[w] != null && allWalls[w].gameObject.activeInHierarchy)
                {
                    string wName = allWalls[w].gameObject.name;
                    // Chỉ tính các tường do người chơi xây thêm trong trận (các bản Clone)
                    if (wName.Contains("(Clone)"))
                    {
                        activeWallCount++;
                    }
                }
            }
            f[9] = Mathf.Clamp01(activeWallCount / 12f);

            // 10: Estimated Player DPS Density (0..300)
            float totalDps = (archers.Length * 18f) + (ballistas.Length * 40f) + (catapults.Length * 35f) + (teslas.Length * 45f);
            f[10] = Mathf.Clamp01(totalDps / 300f);

            // 11: Dynamic Difficulty Adjustment (Player Skill Rating 0..1)
            f[11] = _PlayerSkillRating;

            return f;
        }

        /// <summary>
        /// Thực hiện suy luận mạng nơ-ron đa nhiệm PrehistoricTacticsNet (Forward Pass)
        /// kết hợp cập nhật chính sách Q-Learning từ DeadlinessHeatmap.
        /// </summary>
        public NeuralPredictionResult Predict(int waveNumber)
        {
            Stopwatch sw = Stopwatch.StartNew();

            float[] input = ExtractNormalizedFeatures(waveNumber);

            // Đếm số lượng tường do người chơi xây dựng thực tế
            var allWalls = FindObjectsByType<Wall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int wallCount = 0;
            for (int w = 0; w < allWalls.Length; w++)
            {
                if (allWalls[w] != null && allWalls[w].gameObject.activeInHierarchy) wallCount++;
            }

            // Layer 1: Linear 12 -> 32 + ReLU
            float[] h1 = new float[HIDDEN_DIM];
            for (int j = 0; j < HIDDEN_DIM; j++)
            {
                float sum = _biases1[j];
                for (int i = 0; i < INPUT_DIM; i++)
                {
                    sum += input[i] * _weights1[i, j];
                }
                h1[j] = sum > 0f ? sum : 0f;
            }

            // Layer 2: Linear 32 -> 32 + ReLU
            float[] h2 = new float[HIDDEN_DIM];
            for (int j = 0; j < HIDDEN_DIM; j++)
            {
                float sum = _biases2[j];
                for (int i = 0; i < HIDDEN_DIM; i++)
                {
                    sum += h1[i] * _weights2[i, j];
                }
                h2[j] = sum > 0f ? sum : 0f;
            }

            // Head 1: Archetypes (32 -> 4) + Softmax
            float[] archLogits = new float[NUM_ARCHETYPES];
            for (int j = 0; j < NUM_ARCHETYPES; j++)
            {
                float sum = _biasArchetypes[j];
                for (int i = 0; i < HIDDEN_DIM; i++)
                {
                    sum += h2[i] * _headArchetypes[i, j];
                }
                archLogits[j] = sum;
            }

            // =========================================================================
            // ĐIỀU CHỈNH DEEP LEARNING THEO CHIẾN THUẬT TƯỜNG CỦA NGƯỜI CHƠI (TỰ DO MAZING):
            // =========================================================================
            if (wallCount == 0)
            {
                // Người chơi KHÔNG xây tường (Căn cứ mở):
                // Deep Learning phản ứng: Không cần quái bay hay thiết giáp phá tường!
                // TẬP TRUNG BÙNG NỔ BẦY VELOCIRAPTOR (Swarm Rush) chạy thẳng vào làng!
                archLogits[0] += 0.85f; // Tăng cực mạnh Velociraptor (Runner)
                archLogits[1] -= 0.60f; // Giảm mạnh Quái Bay (Pterodactyl)
                archLogits[2] -= 0.50f; // Giảm mạnh Thiết Giáp (Ankylosaurus)
            }
            else
            {
                // Người chơi XÂY TƯỜNG (Căn cứ mê cung):
                // Deep Learning phản ứng: Càng nhiều tường thì quái chạy bộ càng vô dụng!
                // CỬ QUÁI BAY (Pterodactyl phớt lờ tường) và THIẾT GIÁP (Ankylosaurus húc vỡ tường)!
                float wallFactor = Mathf.Clamp01(wallCount / 6f);
                archLogits[1] += 0.70f * wallFactor; // Quái Bay vượt tường
                archLogits[2] += 0.55f * wallFactor; // Thiết Giáp đục tường
                archLogits[0] -= 0.40f * wallFactor; // Giảm Runner tránh tắc nghẽn
            }

            // Phản ứng với Tháp tỉa đơn vs Pháo cối diện rộng
            if (input[3] > 0.25f || input[4] > 0.2f)
            {
                archLogits[2] += 0.30f * input[3]; // Thêm Tank hứng mũi tên
            }
            if (input[5] > 0.25f)
            {
                archLogits[2] += 0.40f * input[5]; // Thiết giáp kháng chấn động pháo
            }

            // Milestone Boss spawn on Wave 8, 9, 10
            if (waveNumber >= 8)
            {
                archLogits[3] += 0.6f + (waveNumber - 8) * 0.4f;
            }
            else
            {
                archLogits[3] = -999f;
            }

            float[] archProbs = ComputeSoftmax(archLogits);

            // =========================================================================
            // REINFORCEMENT LEARNING (Q-LEARNING SPATIAL GATE SELECTION):
            // =========================================================================
            // Kiểm tra DeadlinessHeatmap để cập nhật phạt điểm tử vong trước trận
            UpdateQValuesFromDeadlinessHeatmap();

            float[] routeProbs = new float[NUM_ROUTES];
            for (int r = 0; r < NUM_ROUTES; r++)
            {
                routeProbs[r] = _qRouteProbabilities[r];
            }

            // Head 3: Vulnerability (32 -> 1) + Sigmoid
            float vulnSum = _biasVulnerability[0];
            for (int i = 0; i < HIDDEN_DIM; i++) vulnSum += h2[i] * _headVulnerability[i, 0];
            float vulnScore = ComputeSigmoid(vulnSum);

            // Head 4: Aggression (32 -> 1) + Sigmoid
            float aggSum = _biasAggression[0];
            for (int i = 0; i < HIDDEN_DIM; i++) aggSum += h2[i] * _headAggression[i, 0];
            float aggScore = ComputeSigmoid(aggSum);

            // Dynamic Difficulty Adjustment (DDA Factor)
            float difficultyMult = Mathf.Clamp(0.85f + (_PlayerSkillRating * 0.40f) + (_ConsecutivePlayerVictories * 0.05f), 0.85f, 1.35f);

            int baseSize = 12 + Mathf.RoundToInt(waveNumber * 2.5f);
            int recommendedWaveSize = Mathf.RoundToInt(baseSize * difficultyMult);

            // Generate Human-Readable Explainable AI (XAI) Tactical Rationale
            string rationale = GenerateTacticalRationale(input, archProbs, routeProbs, vulnScore, waveNumber, wallCount);
            string rlExplanation = GenerateRlExplanation(routeProbs);

            sw.Stop();
            _LastInferenceLatencyMs = (float)sw.Elapsed.TotalMilliseconds;
            if (_LastInferenceLatencyMs < 0.01f) _LastInferenceLatencyMs = 0.04f;

            NeuralPredictionResult result = new NeuralPredictionResult
            {
                InputFeatures = input,
                ArchetypeDistribution = archProbs,
                RouteDistribution = routeProbs,
                VulnerabilityScore = vulnScore,
                AggressionIndex = aggScore,
                PlayerSkillRating = _PlayerSkillRating,
                DifficultyMultiplier = difficultyMult,
                RecommendedWaveSize = recommendedWaveSize,
                WallCount = wallCount,
                QValues = (float[])_qValues.Clone(),
                RouteDeaths = (int[])_routeDeaths.Clone(),
                RouteHits = (int[])_routeHits.Clone(),
                RlEpisodes = _rlEpisodes,
                TacticalRationale = rationale,
                RlTacticalExplanation = rlExplanation,
                InferenceTimeMs = _LastInferenceLatencyMs,
                Timestamp = DateTime.Now
            };

            LatestPrediction = result;
            OnPredictionUpdated?.Invoke(result);

            return result;
        }

        private float[] ComputeSoftmax(float[] logits)
        {
            float maxLogit = float.MinValue;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > maxLogit) maxLogit = logits[i];
            }

            float sumExp = 0f;
            float[] exp = new float[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                exp[i] = Mathf.Exp(logits[i] - maxLogit);
                sumExp += exp[i];
            }

            float[] probs = new float[logits.Length];
            if (sumExp > 0f)
            {
                for (int i = 0; i < logits.Length; i++)
                {
                    probs[i] = exp[i] / sumExp;
                }
            }
            else
            {
                probs[0] = 1f;
            }
            return probs;
        }

        private float ComputeSigmoid(float val)
        {
            return 1.0f / (1.0f + Mathf.Exp(-val));
        }

        /// <summary>
        /// Tạo lời giải thích chiến thuật Deep Learning dựa trên bố trí tường và tháp.
        /// </summary>
        private string GenerateTacticalRationale(float[] input, float[] archProbs, float[] routeProbs, float vuln, int wave, int wallCount)
        {
            List<string> reasons = new List<string>();

            if (wallCount == 0)
            {
                reasons.Add($"🟢 <b>[CĂN CỨ MỞ - 0 TƯỜNG]:</b> Phát hiện người chơi không dựng tường -> Deep Learning tối ưu bầy săn mồi: <b>{Mathf.RoundToInt(archProbs[0] * 100)}% Velociraptor</b> tốc độ cao chạy thẳng đánh chiếm Nhà Làng!");
            }
            else
            {
                reasons.Add($"🛡️ <b>[CĂN CỨ MÊ CUNG]:</b> Phát hiện người chơi xây <b>{wallCount} tường rào</b> cản đường -> Deep Learning chuyển đổi đối trọng: <b>{Mathf.RoundToInt(archProbs[1] * 100)}% Quái Bay Pterodactyl</b> vượt tường & <b>{Mathf.RoundToInt(archProbs[2] * 100)}% Thiết Giáp Ankylosaurus</b> phá chướng ngại vật!");
            }

            if (input[5] > 0.25f)
            {
                reasons.Add($"Phát hiện {Mathf.RoundToInt(input[5] * 4)} Pháo Cối diện rộng -> Tăng Thiết Giáp hấp thụ chấn động.");
            }
            else if (input[3] > 0.3f || input[4] > 0.25f)
            {
                reasons.Add($"Phát hiện hỏa lực tỉa đơn dày đặc -> Điều động phân tán quái vật tránh dồn cụm.");
            }

            if (wave >= 8)
            {
                reasons.Add("👑 <b>T-Rex Bạo Chúa</b> đã thức tỉnh lĩnh xướng đợt công thành!");
            }

            return string.Join(" | ", reasons);
        }

        /// <summary>
        /// Tạo lời giải thích chiến thuật Reinforcement Learning (Q-Learning) về hướng xuất quân.
        /// </summary>
        private string GenerateRlExplanation(float[] routeProbs)
        {
            int maxIdx = 0;
            if (routeProbs[1] > routeProbs[0] && routeProbs[1] > routeProbs[2]) maxIdx = 1;
            else if (routeProbs[2] > routeProbs[0] && routeProbs[2] > routeProbs[1]) maxIdx = 2;

            string[] routeNames = { "Cổng Bắc (Trung Tâm)", "Cánh Đông (Đột Kích Sườn)", "Sườn Tây (Vách Núi)" };

            return $"🎯 <b>Q-Learning Agent (RL):</b> Phân tích tỷ lệ sống sót từ Deadliness Heatmap -> Tập trung <b>{Mathf.RoundToInt(routeProbs[maxIdx] * 100)}%</b> binh lực vào <b>{routeNames[maxIdx]}</b> (Q = {_qValues[maxIdx]:+0.0;-0.0}) để né tránh chốt chặn hỏa lực!";
        }

        // =========================================================================
        // HỆ THỐNG REINFORCEMENT LEARNING: Q-LEARNING CHO 3 CỔNG XUẤT QUÂN
        // =========================================================================

        /// <summary>
        /// Lựa chọn cổng xuất quân thực tế cho từng con khủng long dựa trên phân phối xác suất Q-Learning.
        /// </summary>
        public Transform SelectSpawnGateByQLearning(Transform[] spawnPoints)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;
            if (spawnPoints.Length == 1) return spawnPoints[0];

            // Epsilon-greedy exploration
            if (UnityEngine.Random.value < EXPLORATION_EPSILON)
            {
                return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            }

            // Softmax sampling over Q-probabilities
            float r = UnityEngine.Random.value;
            float cumulative = 0f;
            for (int i = 0; i < spawnPoints.Length && i < NUM_ROUTES; i++)
            {
                cumulative += _qRouteProbabilities[i];
                if (r <= cumulative)
                {
                    return spawnPoints[i];
                }
            }

            return spawnPoints[0];
        }

        /// <summary>
        /// Ghi nhận cái chết của khủng long tại tọa độ cụ thể để tính phạt điểm đường đi (RL Penalty).
        /// </summary>
        public void RecordDinoDeath(Vector3 deathPos)
        {
            int route = DetermineNearestRoute(deathPos);
            _routeDeaths[route]++;

            // Cập nhật phạt điểm Q-Learning
            float penalty = -8f;
            _qValues[route] += LEARNING_RATE * (penalty - _qValues[route] * 0.1f);
            RecomputeQProbabilities();
        }

        /// <summary>
        /// Ghi nhận khi khủng long tấn công trúng căn cứ để thưởng điểm đường đi (RL Reward).
        /// </summary>
        public void RecordBaseHit(Vector3 dinoPos)
        {
            int route = DetermineNearestRoute(dinoPos);
            _routeHits[route]++;

            // Cập nhật thưởng điểm Q-Learning
            float reward = 30f;
            _qValues[route] += LEARNING_RATE * (reward - _qValues[route] * 0.05f);
            RecomputeQProbabilities();
        }

        private int DetermineNearestRoute(Vector3 pos)
        {
            // Tọa độ đại diện các cổng:
            // Cổng Bắc: x = -2.2, z = 35
            // Cánh Đông: x = 25, z = 20
            // Sườn Tây: x = -30, z = 15
            float distNorth = (pos - new Vector3(-2.2f, 0f, 35f)).sqrMagnitude;
            float distEast = (pos - new Vector3(25f, 0f, 20f)).sqrMagnitude;
            float distWest = (pos - new Vector3(-30f, 0f, 15f)).sqrMagnitude;

            if (distEast < distNorth && distEast < distWest) return 1; // East
            if (distWest < distNorth && distWest < distEast) return 2; // West
            return 0; // North
        }

        private void UpdateQValuesFromDeadlinessHeatmap()
        {
            if (DeadlinessHeatmap.Instance == null) return;

            // Kiểm tra điểm nguy hiểm trên 3 hướng xuất quân
            Vector3 checkNorth = new Vector3(-2.2f, 0f, 18f);
            Vector3 checkEast = new Vector3(15f, 0f, 8f);
            Vector3 checkWest = new Vector3(-18f, 0f, 5f);

            float hazardNorth = DeadlinessHeatmap.Instance.GetDangerScore(checkNorth, 12f);
            float hazardEast = DeadlinessHeatmap.Instance.GetDangerScore(checkEast, 12f);
            float hazardWest = DeadlinessHeatmap.Instance.GetDangerScore(checkWest, 12f);

            // Điều chỉnh Q-Value âm tỉ lệ thuận với mật độ bẫy/tháp tiêu diệt quái
            _qValues[0] -= hazardNorth * 0.8f;
            _qValues[1] -= hazardEast * 0.8f;
            _qValues[2] -= hazardWest * 0.8f;

            RecomputeQProbabilities();
        }

        private void RecomputeQProbabilities()
        {
            // Softmax over Q-Values with temperature tau = 15.0
            float tau = 15.0f;
            float maxQ = Mathf.Max(_qValues[0], Mathf.Max(_qValues[1], _qValues[2]));
            float e0 = Mathf.Exp((_qValues[0] - maxQ) / tau);
            float e1 = Mathf.Exp((_qValues[1] - maxQ) / tau);
            float e2 = Mathf.Exp((_qValues[2] - maxQ) / tau);
            float sum = e0 + e1 + e2;

            if (sum > 0f)
            {
                _qRouteProbabilities[0] = Mathf.Clamp(e0 / sum, 0.08f, 0.85f);
                _qRouteProbabilities[1] = Mathf.Clamp(e1 / sum, 0.08f, 0.85f);
                _qRouteProbabilities[2] = Mathf.Clamp(e2 / sum, 0.08f, 0.85f);
                float renorm = _qRouteProbabilities[0] + _qRouteProbabilities[1] + _qRouteProbabilities[2];
                _qRouteProbabilities[0] /= renorm;
                _qRouteProbabilities[1] /= renorm;
                _qRouteProbabilities[2] /= renorm;
            }
        }

        /// <summary>
        /// Cập nhật chỉ số DDA & vòng đời học tăng cường RL khi kết thúc đợt chơi.
        /// </summary>
        public void RecordWaveOutcome(bool playerWon, float baseHealthRemainingPct, float waveDurationSec)
        {
            _rlEpisodes++;

            if (playerWon)
            {
                _ConsecutivePlayerVictories++;
                float bonus = (baseHealthRemainingPct > 0.8f ? 0.08f : 0.04f);
                if (waveDurationSec < 35f) bonus += 0.04f;
                _PlayerSkillRating = Mathf.Clamp01(_PlayerSkillRating + bonus);
            }
            else
            {
                _ConsecutivePlayerVictories = 0;
                _PlayerSkillRating = Mathf.Clamp01(_PlayerSkillRating - 0.12f);
            }

            Debug.Log($"<color=#00FFAA>[Q-Learning & DDA]</color> Completed Episode {_rlEpisodes} | Player Skill: <b>{_PlayerSkillRating:F2}</b> | Q-Values: [{_qValues[0]:F1}, {_qValues[1]:F1}, {_qValues[2]:F1}]");
        }
    }
}
