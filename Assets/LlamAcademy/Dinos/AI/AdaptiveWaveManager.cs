using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Enemy;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LlamAcademy.Dinos.AI
{
    [DefaultExecutionOrder(10)]
    public class AdaptiveWaveManager : MonoBehaviour
    {
        public static AdaptiveWaveManager Instance { get; private set; }

        [System.Serializable]
        public class MonsterArchetype
        {
            public string Name;
            public DinoSO UnitSO;
            [Range(0.1f, 10f)] public float BaseWeight = 1.0f;
            public ArchetypeRole Role;
            public int GoldRewardOnDeath = 15;
        }

        public enum ArchetypeRole
        {
            SwarmRunner,     // Fast, cheap, counters single-target snipers/archers
            ArmoredTank,     // High HP, heavy armor, counters basic physical attacks
            SiegeBreaker,    // Attacks walls directly, counters complex mazes
            AerialFlyer,     // Ignores ground walls, counters ground-only traps/cannons
            Boss             // Apex units on milestone waves
        }

        [Header("Spawn Configuration")]
        [SerializeField] private Transform[] SpawnPoints;
        [SerializeField] private List<MonsterArchetype> MonsterCatalog = new();
        public List<MonsterArchetype> Catalog => MonsterCatalog;
        [SerializeField] private int BaseWaveBudget = 150;
        [SerializeField] private float BudgetGrowthMultiplier = 1.35f;

        [Header("Adaptive Intelligence Weights")]
        [SerializeField] private float SwarmCounterBonus = 2.5f;
        [SerializeField] private float TankCounterBonus = 2.0f;
        [SerializeField] private float FlyerCounterBonus = 2.0f;

        [Header("Debug & Info")]
        [SerializeField] private int CurrentWave = 1;
        [SerializeField] private int ActiveMonstersCount = 0;
        [SerializeField] private string LastDecisionLog = "";

        public event Action<int> OnWaveStarted;
        public event Action<int> OnWaveCompleted;

        private List<Unit.Unit> ActiveWaveUnits = new();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange += HandleGameStateChange;
            }
        }

        private void OnDestroy()
        {
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange -= HandleGameStateChange;
            }
        }

        private void HandleGameStateChange(GameState oldState, GameState newState)
        {
            // Tách biệt hoàn toàn: Tuyệt đối không sinh quái địch trong chế độ Khủng Long Công Thành (Dino Assault)
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
            {
                return;
            }

            // Khi PrehistoricGameplayManager đang quản lý wave của game (10 Wave chuẩn),
            // AdaptiveWaveManager tuyệt đối không tự ý spawn trùng lặp wave quái riêng biệt
            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.enabled)
            {
                return;
            }

            if (newState == GameState.Running)
            {
                StartNextAdaptiveWave();
            }
        }

        public void StartNextAdaptiveWave()
        {
            // Tách biệt hoàn toàn: Tuyệt đối không sinh quái địch trong chế độ Khủng Long Công Thành (Dino Assault)
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
            {
                return;
            }

            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.enabled)
            {
                return;
            }

            StopAllCoroutines();
            StartCoroutine(SpawnAdaptiveWaveRoutine());
        }

        private IEnumerator SpawnAdaptiveWaveRoutine()
        {
            // Tách biệt hoàn toàn: Tuyệt đối không sinh quái địch trong chế độ Khủng Long Công Thành (Dino Assault)
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
            {
                yield break;
            }
            CurrentWave = RoundManager.Instance != null ? RoundManager.Instance.Round : CurrentWave;
            OnWaveStarted?.Invoke(CurrentWave);

            // Step 1: Audit Player Defenses & Analyze Strategy
            DefenseAudit audit = AuditPlayerDefenses();

            // Step 2: Calculate Adaptive Weights (Counter-Composition)
            Dictionary<MonsterArchetype, float> weights = ComputeAdaptiveWeights(audit);

            // Step 3: Compute Wave Budget with Dynamic Difficulty Scaling
            int waveBudget = Mathf.CeilToInt(BaseWaveBudget * Mathf.Pow(BudgetGrowthMultiplier, CurrentWave - 1));

            // Query IO Cloud Deep Learning AI Director if connected
            if (LlamAcademy.Dinos.Cloud.IOCloudManager.Instance != null && LlamAcademy.Dinos.Cloud.IOCloudManager.Instance.IsCloudConnected)
            {
                LlamAcademy.Dinos.Cloud.IOCloudManager.Instance.RequestDeepLearningWave(CurrentWave);
                LastDecisionLog = $"Wave {CurrentWave} [IO Cloud Deep Learning Active]: {LlamAcademy.Dinos.Cloud.IOCloudManager.Instance.LastTacticalRationale}";
            }
            else
            {
                LastDecisionLog = $"Wave {CurrentWave} Budget: {waveBudget}g. Audit: Arch={audit.SingleTargetTowers}, Cann={audit.SplashTowers}, Wall={audit.WallCount}.";
            }

            Debug.Log($"<color=#00FFAA>[Adaptive AI Director]</color> {LastDecisionLog}");

            ActiveWaveUnits.Clear();

            // Step 4: Spend Budget to spawn composition
            int remainingBudget = waveBudget;
            Transform targetBase = RoundManager.Instance != null ? RoundManager.Instance.DinoTarget : null;

            while (remainingBudget > 0)
            {
                MonsterArchetype chosenArchetype = SelectWeightedArchetype(weights, remainingBudget);
                if (chosenArchetype == null) break;

                Transform spawnPoint = SpawnPoints != null && SpawnPoints.Length > 0
                    ? SpawnPoints[Random.Range(0, SpawnPoints.Length)]
                    : transform;

                Vector3 spawnPos = spawnPoint.position + new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
                Quaternion rot = targetBase != null
                    ? Quaternion.LookRotation((targetBase.position - spawnPos).normalized)
                    : Quaternion.identity;

                Unit.Unit monster = Instantiate(chosenArchetype.UnitSO.Prefab, spawnPos, rot);
                monster.UnitType = chosenArchetype.UnitSO;
                monster.enabled = true;

                ActiveWaveUnits.Add(monster);
                ActiveMonstersCount++;

                int reward = chosenArchetype.GoldRewardOnDeath;
                monster.OnDeath += (deadUnit) => HandleMonsterDeath(monster, reward);

                remainingBudget -= chosenArchetype.UnitSO.Cost;

                // Stagger spawn delay between units
                yield return new WaitForSeconds(Random.Range(0.6f, 1.2f));
            }

            // Step 5: Wait until all monsters are cleared
            while (ActiveMonstersCount > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            // Step 6: Wave Completed
            Debug.Log($"<color=#00FFAA>[Adaptive AI]</color> Wave {CurrentWave} Cleared!");
            OnWaveCompleted?.Invoke(CurrentWave);

            if (DeadlinessHeatmap.Instance != null)
            {
                DeadlinessHeatmap.Instance.DecayOldRecords(CurrentWave);
            }
        }

        private void HandleMonsterDeath(Unit.Unit monster, int goldReward)
        {
            ActiveWaveUnits.Remove(monster);
            ActiveMonstersCount = Mathf.Max(0, ActiveMonstersCount - 1);

            // Record to Heatmap
            if (DeadlinessHeatmap.Instance != null)
            {
                DeadlinessHeatmap.Instance.RecordDeath(monster.transform.position, CurrentWave);
            }

            // Award Gold to player
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.AddGold(goldReward);
            }
        }

        private struct DefenseAudit
        {
            public int SingleTargetTowers;
            public int SplashTowers;
            public int MagicTowers;
            public int WallCount;
        }

        private DefenseAudit AuditPlayerDefenses()
        {
            DefenseAudit audit = new DefenseAudit();
            Unit.Unit[] allUnits = FindObjectsByType<Unit.Unit>(FindObjectsInactive.Exclude);

            foreach (var unit in allUnits)
            {
                if (unit == null) continue;
                string uName = unit.gameObject.name;

                if (unit is Wall)
                {
                    // Chỉ tính các tường / rào cọc phụ do người chơi đặt thêm (không tính các đoạn rào gốc của làng)
                    if (uName.Contains("(Clone)"))
                    {
                        audit.WallCount++;
                    }
                }
                else if (unit is ArcherTower || unit is BallistaTower)
                {
                    audit.SingleTargetTowers++;
                }
                else if (unit is CatapultTower)
                {
                    audit.SplashTowers++;
                }
                else if (unit is TeslaTower || unit is FrostTower)
                {
                    audit.MagicTowers++;
                }
            }

            return audit;
        }

        private Dictionary<MonsterArchetype, float> ComputeAdaptiveWeights(DefenseAudit audit)
        {
            Dictionary<MonsterArchetype, float> weights = new();

            foreach (var arch in MonsterCatalog)
            {
                float weight = arch.BaseWeight;

                // Counter single-target snipers with swarms
                if (arch.Role == ArchetypeRole.SwarmRunner && audit.SingleTargetTowers > audit.SplashTowers)
                {
                    weight += SwarmCounterBonus * (audit.SingleTargetTowers - audit.SplashTowers);
                }

                // Counter heavy splash with armored tanks or flying units
                if (arch.Role == ArchetypeRole.ArmoredTank && audit.SplashTowers > 0)
                {
                    weight += TankCounterBonus * audit.SplashTowers;
                }

                if (arch.Role == ArchetypeRole.AerialFlyer && audit.SplashTowers > 0)
                {
                    weight += FlyerCounterBonus * audit.SplashTowers;
                }

                // Counter complex mazes with siege wall-breakers
                if (arch.Role == ArchetypeRole.SiegeBreaker && audit.WallCount > 8)
                {
                    weight += 2.0f * (audit.WallCount / 8f);
                }

                weights[arch] = Mathf.Max(0.1f, weight);
            }

            return weights;
        }

        private MonsterArchetype SelectWeightedArchetype(Dictionary<MonsterArchetype, float> weights, int maxBudget)
        {
            var affordable = weights.Keys.Where(arch => arch.UnitSO != null && arch.UnitSO.Cost <= maxBudget).ToList();
            if (affordable.Count == 0) return null;

            float totalWeight = affordable.Sum(arch => weights[arch]);
            float roll = Random.Range(0, totalWeight);

            foreach (var arch in affordable)
            {
                if (roll <= weights[arch]) return arch;
                roll -= weights[arch];
            }

            return affordable[0];
        }

        /// <summary>
        /// Sinh đội hình quái vật thích ứng chiến thuật theo bố trí phòng thủ thời gian thực của người chơi.
        /// </summary>
        public List<GameObject> GenerateAdaptiveWave(int waveIndex, GameObject raptorPrefab, GameObject pteroPrefab, GameObject ankylPrefab, GameObject bossPrefab, out string tacticalRationale)
        {
            List<GameObject> queue = new();

            // 1. Thực hiện suy luận Mạng Nơ-ron Đa Nhiệm On-Device (PrehistoricTacticsNet)
            PrehistoricNeuralDirector.NeuralPredictionResult pred = PrehistoricNeuralDirector.Instance.Predict(waveIndex);
            tacticalRationale = pred.TacticalRationale;

            // 2. Tính toán quy mô đợt theo hệ số thích ứng độ khó DDA
            int totalMonsters = pred.RecommendedWaveSize;
            int bossSpawnCount = (waveIndex == 8 || waveIndex == 9) ? 1 : (waveIndex >= 10 ? 2 : 0);

            // 3. Phân bổ số lượng từng loài theo phân phối xác suất Softmax
            float runnerProb = pred.ArchetypeDistribution != null && pred.ArchetypeDistribution.Length > 0 ? pred.ArchetypeDistribution[0] : 0.45f;
            float flyerProb = pred.ArchetypeDistribution != null && pred.ArchetypeDistribution.Length > 1 ? pred.ArchetypeDistribution[1] : 0.25f;
            float tankProb = pred.ArchetypeDistribution != null && pred.ArchetypeDistribution.Length > 2 ? pred.ArchetypeDistribution[2] : 0.30f;

            int normalCount = Mathf.Max(6, totalMonsters - bossSpawnCount);
            int raptorCount;
            int pteroCount;
            int ankylCount;

            if (pred.WallCount == 0)
            {
                // CĂN CỨ MỞ (0 TƯỜNG):
                // Màn 1: 100% bầy Velociraptor chạy thẳng
                if (waveIndex == 1)
                {
                    raptorCount = normalCount;
                    pteroCount = 0;
                    ankylCount = 0;
                }
                else if (waveIndex == 2)
                {
                    pteroCount = Mathf.RoundToInt(normalCount * Mathf.Min(flyerProb, 0.25f));
                    ankylCount = 0;
                    raptorCount = normalCount - pteroCount;
                }
                else
                {
                    float sum = runnerProb + flyerProb + tankProb;
                    if (sum > 0f) { runnerProb /= sum; flyerProb /= sum; tankProb /= sum; }
                    pteroCount = Mathf.RoundToInt(normalCount * flyerProb);
                    ankylCount = Mathf.RoundToInt(normalCount * tankProb);
                    raptorCount = normalCount - (pteroCount + ankylCount);
                    if (raptorCount < 4) raptorCount = 4;
                }
            }
            else
            {
                // CĂN CỨ MÊ CUNG (CÓ TƯỜNG):
                // Deep Learning kích hoạt NGAY LẬP TỨC đội hình khắc chế:
                // Tăng quái bay Pterodactyl và thiết giáp Ankylosaurus phá tường (kể cả từ Wave 1 hoặc 2)
                float sum = runnerProb + flyerProb + tankProb;
                if (sum > 0f) { runnerProb /= sum; flyerProb /= sum; tankProb /= sum; }
                pteroCount = Mathf.Max(2, Mathf.RoundToInt(normalCount * flyerProb));
                ankylCount = Mathf.Max(1, Mathf.RoundToInt(normalCount * tankProb));
                raptorCount = Mathf.Max(3, normalCount - (pteroCount + ankylCount));
            }

            // Đảm bảo tổng số lượng quái tối thiểu hợp lý cho mỗi màn
            int minTotal = 13 + (waveIndex - 1) * 3;
            if (raptorCount + pteroCount + ankylCount < minTotal)
            {
                raptorCount += (minTotal - (raptorCount + pteroCount + ankylCount));
            }

            if (raptorPrefab != null) for (int i = 0; i < raptorCount; i++) queue.Add(raptorPrefab);
            if (pteroPrefab != null) for (int i = 0; i < pteroCount; i++) queue.Add(pteroPrefab);
            if (ankylPrefab != null) for (int i = 0; i < ankylCount; i++) queue.Add(ankylPrefab);
            if (bossPrefab != null)
            {
                for (int b = 0; b < bossSpawnCount; b++)
                {
                    queue.Add(bossPrefab);
                }
            }

            // Xáo trộn thứ tự xuất hiện tự nhiên
            for (int i = 0; i < queue.Count - 2; i++)
            {
                int r = Random.Range(i, queue.Count);
                if (queue[r] == bossPrefab && i < queue.Count / 3) continue;
                (queue[i], queue[r]) = (queue[r], queue[i]);
            }

            return queue;
        }
    }
}
