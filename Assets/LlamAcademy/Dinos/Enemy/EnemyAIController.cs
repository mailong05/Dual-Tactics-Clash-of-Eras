using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace LlamAcademy.Dinos.Enemy
{
    [DefaultExecutionOrder(10)]
    public class EnemyAIController : MonoBehaviour
    {
        [field: SerializeField] public Difficulty Difficulty { get; private set; } = Difficulty.Normal;
        [SerializeField] private AIStyleConfig ActiveConfig;
        private Dictionary<UnitType, HashSet<Defender>> Defenders = new();
        [SerializeField] private List<Transform> WaypointTransforms = new();
        [SerializeField] private List<Vector3> Waypoints;

        public static EnemyAIController Instance { get; private set; }

        public delegate void DefenderEvent(Defender defender);
        public delegate void WallEvent(Wall wall);

        public event DefenderEvent OnSpawnDefender;
        public event DefenderEvent OnDefenderDeath;

        public event WallEvent OnSpawnWall;
        public event WallEvent OnWallDeath;

        [Header("Debug")]
        [SerializeField] private List<WallData> WallDatas = new();
        [field: SerializeField] public int ResourcesToSpend { get; set; }

        public static readonly Vector3[] SafeVillageGroundSpots = new Vector3[]
        {
            new Vector3(-3.0f, 0.10f, -30.0f),
            new Vector3(0.0f, 0.10f, -31.0f),
            new Vector3(3.0f, 0.10f, -30.0f),
            new Vector3(-6.0f, 0.10f, -32.0f),
            new Vector3(6.0f, 0.10f, -32.0f),
            new Vector3(-2.2f, 0.10f, -34.0f),
            new Vector3(2.2f, 0.10f, -34.0f),
            new Vector3(-8.0f, 0.10f, -33.0f),
            new Vector3(8.0f, 0.10f, -33.0f),
            new Vector3(-10.0f, 0.10f, -35.0f),
            new Vector3(10.0f, 0.10f, -35.0f),
            new Vector3(0.0f, 0.10f, -36.0f)
        };

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"Multiple EnemyAIController detected. Deleting the second one {name}");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            Waypoints = new List<Vector3>();
            if (WaypointTransforms != null)
            {
                WaypointTransforms.RemoveAll(t => t == null);
                foreach (Transform t in WaypointTransforms)
                {
                    if (t != null)
                    {
                        Waypoints.Add(t.position);
                    }
                }
            }

            EnsureValidWaypoints();

            Defenders.Add(UnitType.Archer, new());
            Defenders.Add(UnitType.Mage, new());
            Defenders.Add(UnitType.Cannoneer, new());

            Wall[] baseWalls = FindObjectsByType<Wall>(FindObjectsInactive.Include);
            for (int i = 0; i < baseWalls.Length; i++)
            {
                if (baseWalls[i] == null) continue;
                WallDatas.Add(new WallData(baseWalls[i], 0, 0));
                baseWalls[i].OnDeath += HandleWallDeath;
                baseWalls[i].OnTakeDamage += HandleWallDamage;
            }
        }

        private void HandleWallDamage(IDamageable wall, int damage)
        {
            if (wall == null || wall.Transform == null) return;
            if (RoundManager.Instance == null || RoundManager.Instance.State != GameState.Running) return;

            Vector3 wallPos = wall.Transform.position;
            Vector3 basePos = Defender.GetBasePosition();
            Vector3 dirToBase = (basePos - wallPos);
            dirToBase.y = 0;
            if (dirToBase.sqrMagnitude < 0.01f) dirToBase = Vector3.back;
            else dirToBase.Normalize();

            // Vị trí cố thủ phòng ngự lý tưởng: cách tường 4.0m về phía trong làng (hướng về căn cứ)
            // Giúp NPC chiến binh đứng an toàn phía sau chướng ngại vật/cổng làng và xả tên/phóng giáo vào quái
            Vector3 defensiveStandPos = wallPos + dirToBase * 4.0f;

            foreach (Defender defender in Defenders.SelectMany(keyValuePair => keyValuePair.Value.Where((def) => UnitShouldChangeTarget(def, wall))))
            {
                if (defender == null || defender.Health <= 0) continue;

                Vector3 targetDest = defensiveStandPos;
                bool found = false;

                // 1. Thử tìm vị trí NavMesh hợp lệ xung quanh điểm cố thủ phòng ngự với agent filter
                if (defender.Agent != null && NavMesh.SamplePosition(defensiveStandPos, out NavMeshHit hit, 12f,
                        new NavMeshQueryFilter { agentTypeID = defender.Agent.agentTypeID, areaMask = defender.Agent.areaMask }))
                {
                    targetDest = hit.position;
                    found = true;
                }
                // 2. Fallback sang NavMesh.AllAreas diện rộng 18m xung quanh điểm cố thủ
                else if (NavMesh.SamplePosition(defensiveStandPos, out hit, 18f, NavMesh.AllAreas))
                {
                    targetDest = hit.position;
                    found = true;
                }
                // 3. Fallback quanh chính vị trí tường
                else if (NavMesh.SamplePosition(wallPos, out hit, 18f, NavMesh.AllAreas))
                {
                    targetDest = hit.position;
                    found = true;
                }
                // 4. Fallback quanh vị trí hiện tại của defender hướng về tường
                else if (defender.Agent != null && defender.Agent.isOnNavMesh)
                {
                    Vector3 advancePos = Vector3.MoveTowards(defender.transform.position, defensiveStandPos, 6.0f);
                    if (NavMesh.SamplePosition(advancePos, out hit, 10f, NavMesh.AllAreas))
                    {
                        targetDest = hit.position;
                        found = true;
                    }
                }

                if (found)
                {
                    defender.SetDestination(targetDest);
                }

                // Phát tín hiệu đe dọa tường bị cắn phá cho Defender để ngay lập tức nhắm bắn quái
                defender.AlertWallThreat(wallPos);
            }
        }

        private static bool UnitShouldChangeTarget(Defender defender, IDamageable wall)
        {
            AIState defenderState = defender.State;
            Vector3 defenderPosition = defender.transform.position;
            return defenderState switch
            {
                AIState.Attacking => false,
                AIState.CommandedMove => Vector3.Distance(wall.Transform.position, defenderPosition) <= Vector3.Distance(defender.TargetLocation, defenderPosition),
                _ => true
            };
        }

        private void HandleWallDeath(IDamageable diedObject)
        {
            if (diedObject == null || diedObject.Transform == null) return;
            Wall diedWall = diedObject.Transform.GetComponent<Wall>();
            if (diedWall == null) return;
            int diedIndex = WallDatas.FindIndex((wallData) => wallData.Wall != null && wallData.Wall.Equals(diedWall));
            if (diedIndex != -1)
            {
                WallDatas[diedIndex] = new WallData(diedWall, WallDatas[diedIndex].UpgradeLevel, WallDatas[diedIndex].TimesDied + 1);
                OnWallDeath?.Invoke(diedWall);
            }
        }

        private IEnumerator Start()
        {
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange += Instance_OnGameStateChange;
            }
            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated += Instance_OnNavMeshUpdated;
            }
            yield return null; // allow all other units to get set up before we do turn (such as walls)
            yield return DoTurn();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange -= Instance_OnGameStateChange;
            }

            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated -= Instance_OnNavMeshUpdated;
            }
        }

        private void EnsureValidWaypoints()
        {
            if (Waypoints == null) Waypoints = new List<Vector3>();

            if (Waypoints.Count == 0)
            {
                Vector3[] defaultPatrolSpots = new Vector3[]
                {
                    new Vector3(-3.89f, 4.20f, -14.70f), // Chòi Tiền Tiêu Trung Tâm (Trực tiếp trên sàn boong quan sát)
                    new Vector3(-23.87f, 4.20f, -14.70f), // Chòi Tiền Tiêu Cánh Trái
                    new Vector3(26.10f, 4.20f, -14.80f), // Chòi Tiền Tiêu Cánh Phải
                    new Vector3(-13.91f, 4.15f, -14.70f), // Cầu Đi Bộ Trên Cao 1 (Nối Chòi Trái <-> Chòi Giữa)
                    new Vector3(6.09f, 4.15f, -14.70f),  // Cầu Đi Bộ Trên Cao 3 (Nối Chòi Giữa <-> Cầu 2)
                    new Vector3(16.09f, 4.15f, -14.70f), // Cầu Đi Bộ Trên Cao 2 (Nối Cầu 3 <-> Chòi Phải)
                    new Vector3(-4.0f, 0f, -6.0f),
                    new Vector3(0.0f, 0f, -6.0f),
                    new Vector3(-2.2f, 0f, -9.0f),
                    new Vector3(-8.0f, 0f, -8.0f),
                    new Vector3(6.0f, 0f, -8.0f),
                    new Vector3(-5.0f, 0f, -14.0f),
                    new Vector3(4.0f, 0f, -14.0f),
                    new Vector3(-2.2f, 0f, -20.0f),
                    new Vector3(-6.0f, 0f, -26.0f),
                    new Vector3(4.0f, 0f, -26.0f),
                    new Vector3(-2.2f, 0f, -32.0f)
                };

                foreach (Vector3 spot in defaultPatrolSpots)
                {
                    if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                    {
                        Waypoints.Add(hit.position);
                    }
                    else
                    {
                        Waypoints.Add(spot);
                    }
                }
            }
        }

        private void Instance_OnNavMeshUpdated()
        {
            // Loại bỏ các điểm không nằm trên NavMesh và đảm bảo luôn có điểm tuần tra hợp lệ
            Waypoints.RemoveAll((waypoint) => !NavMesh.SamplePosition(waypoint, out _, 3.0f, NavMesh.AllAreas));
            EnsureValidWaypoints();

            foreach (KeyValuePair<UnitType, HashSet<Defender>> defenders in Defenders)
            {
                foreach (Defender defender in defenders.Value)
                {
                    if (defender != null && defender.State == AIState.Patrol)
                    {
                        defender.Patrol(Waypoints);
                    }
                }
            }
        }

        private void Instance_OnGameStateChange(GameState oldState, GameState newState)
        {
            if (newState == GameState.EnemyRepairs)
            {
                StartCoroutine(DoTurn());
            }
        }

        private IEnumerator DoTurn()
        {
            float defenseSpend = Random.Range(ActiveConfig.DefenseSpendRange.x, ActiveConfig.DefenseSpendRange.y);
            float offenseSpend = 1 - defenseSpend;
            int initialSpend = ResourcesToSpend;
            int remainingDefensiveSpend;
            // ResourcesToSpend is updated internally in the HandleXXX calls
            // Defensive spend is spread between repairs & upgrades
            if (ActiveConfig.SpawnStyle == AISpawnStyle.SpawnBuildingsFirst)
            {
                remainingDefensiveSpend = initialSpend - HandleRepairs(defenseSpend, initialSpend);
                HandleUpgrades(defenseSpend, remainingDefensiveSpend);
                yield return null; // give 1 frame to move obstacles around before recalculating triangulation
                NavMeshManager.Instance.RecalculateTriangulation(true);
                SpawnNewTroops(1, ResourcesToSpend); // force use the remaining budget
            }
            else {
                SpawnNewTroops(offenseSpend, ResourcesToSpend);
                HandleRepairs(defenseSpend, ResourcesToSpend);
                HandleUpgrades(1, ResourcesToSpend);// force use the remaining budget
                yield return null; // give 1 frame to move obstacles around before recalculating triangulation
                NavMeshManager.Instance.RecalculateTriangulation(true);
            }

            RoundManager.Instance.CompleteEnemySetup();
        }

        /// <summary>
        /// Uses the provided resources and repairs lowest percentage of health walls first.
        /// </summary>
        /// <param name="resourceSpend">Percentage of max spend to be used</param>
        /// <param name="maxSpend"></param>
        /// <returns>Amount spent</returns>
        private int HandleRepairs(float resourceSpend, int maxSpend)
        {
            int remainingSpend = Mathf.CeilToInt(maxSpend * resourceSpend);
            int initialSpend = remainingSpend;

            WallDatas.RemoveAll(data => data.Wall == null);
            List<Wall> walls = WallDatas.ConvertAll(data => data.Wall);
            walls.RemoveAll(w => w == null);
            // repair lowest % hp walls first
            walls.Sort((a, b) =>
            {
                float aRatio = a.MaxHealth > 0 ? (a.Health / (float)a.MaxHealth) : 1f;
                float bRatio = b.MaxHealth > 0 ? (b.Health / (float)b.MaxHealth) : 1f;
                return aRatio.CompareTo(bRatio);
            });

            foreach (Wall wall in walls)
            {
                if (wall == null) continue;
                int healthRepairNeeded = wall.MaxHealth - wall.Health;
                if (healthRepairNeeded <= 0) continue;
                int cost = Mathf.CeilToInt(healthRepairNeeded * wall.HealthToCostMultiplier);
                if (remainingSpend >= cost)
                {
                    wall.Repair(healthRepairNeeded);
                    remainingSpend -= cost;
                    Debug.Log($"Repairing wall {healthRepairNeeded} at cost of {cost}");
                    ResourcesToSpend -= cost;
                }
                else if (remainingSpend > 0)
                {
                    cost = remainingSpend;
                    float mult = wall.HealthToCostMultiplier > 0 ? wall.HealthToCostMultiplier : 1f;
                    healthRepairNeeded = Mathf.CeilToInt(remainingSpend / mult);
                    wall.Repair(healthRepairNeeded);
                    Debug.Log($"Partially repairing wall {healthRepairNeeded} at cost of {cost}");
                    remainingSpend -= cost;
                    ResourcesToSpend -= cost;
                    break;
                }
            }

            Debug.Log($"Spent {initialSpend - remainingSpend} based on max spend of {maxSpend} & ratio of {resourceSpend}");
            return initialSpend - remainingSpend;
        }

        /// <summary>
        /// Using the provided resource spend this will upgrade any available walls, prioritizing the walls that have
        /// been destroyed the most since the last upgrade.
        /// </summary>
        /// <param name="resourceSpend">Percentage of max spend to be used</param>
        /// <param name="maxSpend"></param>
        /// <param name="depth">Used internally to allow multiple upgrade passes. This should not be provided as a nonzero value</param>
        /// <returns>Amount spent</returns>
        private int HandleUpgrades(float resourceSpend, int maxSpend, int depth = 0)
        {
            int remainingSpend = Mathf.CeilToInt(maxSpend * resourceSpend);
            int initialSpend = remainingSpend;

            // Clean up any destroyed or null walls
            WallDatas.RemoveAll(wallData => wallData.Wall == null);

            // prefer to upgrade walls where the player is attacking, then the lowest upgrade level
            List<WallData> upgradePriority = WallDatas
                .Where(wallData => wallData.Wall != null &&
                                   wallData.Wall.UnitType != null &&
                                   wallData.Wall.UnitType.Upgrade != null &&
                                   wallData.Wall.UnitType.Upgrade.Prefab != null)
                .OrderByDescending(wallData => wallData.TimesDied)
                .ThenBy(wallData => wallData.UpgradeLevel)
                .ToList();

            Debug.Log($"Found {upgradePriority.Count} possible upgrades at the current tier.");
            foreach (WallData wallData in upgradePriority)
            {
                if (wallData.Wall == null || wallData.Wall.UnitType == null || wallData.Wall.UnitType.Upgrade == null) continue;
                UnitSO upgrade = wallData.Wall.UnitType.Upgrade;
                if (upgrade.Prefab == null) continue;

                if (remainingSpend >= upgrade.Cost)
                {
                    int index = WallDatas.FindIndex(data => data.Equals(wallData));
                    if (index == -1) continue;

                    Transform wallTransform = wallData.Wall.transform;
                    Wall upgradedWall = Instantiate(
                        upgrade.Prefab,
                        wallTransform.position,
                        wallTransform.rotation,
                        wallTransform.parent
                    ) as Wall;

                    if (upgradedWall == null) continue;

                    Debug.Log($"Upgrading {wallData.Wall.gameObject.name} to tier {wallData.UpgradeLevel + 1} for a cost of {upgrade.Cost}");
                    remainingSpend -= upgrade.Cost;
                    ResourcesToSpend -= upgrade.Cost;

                    Destroy(wallData.Wall.gameObject);
                    WallDatas[index] = new WallData(
                        upgradedWall,
                        WallDatas[index].UpgradeLevel + 1,
                        0 // reset deaths after upgrade
                    );
                    upgradedWall.OnDeath += HandleWallDeath;
                    upgradedWall.OnTakeDamage += HandleWallDamage;
                    OnSpawnWall?.Invoke(upgradedWall);
                }
            }

            if (remainingSpend > 0 && depth == 0 && upgradePriority.Count > 0)
            {
                Debug.Log("Extra money left over for upgrades. Trying a second round of upgrades.");
                return (initialSpend - remainingSpend) + HandleUpgrades(1, remainingSpend, 1);
            }

            Debug.Log($"Spent {initialSpend - remainingSpend} on upgrades with a max resource spend of {maxSpend} and a ratio of {resourceSpend}. Depth: {depth}");
            return initialSpend - remainingSpend;
        }

        /// <summary>
        /// Using the provided resource parameters this will spawn units based on the <see cref="ActiveConfig"/> and unit
        /// weight ranges
        /// </summary>
        /// <param name="resourceSpend">Percentage of the max spent to use</param>
        /// <param name="maxSpend"></param>
        /// <returns>The amount spent</returns>
        private int SpawnNewTroops(float resourceSpend, int maxSpend)
        {
            int remainingSpend = Mathf.CeilToInt(maxSpend * resourceSpend);
            int initialSpend = remainingSpend;

            float[] weights = new float[ActiveConfig.Units.Count];
            for (int i = 0; i < ActiveConfig.Units.Count; i++)
            {
                weights[i] = Random.Range(ActiveConfig.Units[i].WeightRange.x, ActiveConfig.Units[i].WeightRange.y);
            }

            float totalWeight = weights.Sum();
            for (int i = 0; i < totalWeight; i++)
            {
                weights[i] /= totalWeight;
            }

            bool failedToSpawnUnit = false;
            while (remainingSpend > 0 && !failedToSpawnUnit)
            {
                failedToSpawnUnit = true;
                float value = Random.value;

                Vector3[] vertices = NavMeshManager.Instance.EnemyTriangulation.vertices;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (value < weights[i])
                    {
                        int cost = ActiveConfig.Units[i].UnitSO.Cost;
                        if (cost <= remainingSpend)
                        {
                            Vector3 tacticalSlot = GetNextAvailableTacticalSlot(ActiveConfig.Units[i].UnitSO.Type, out DefenderSector sector, out List<Vector3> waypoints);
                            bool isHigh = (sector == DefenderSector.HighGroundWatchtowers);

                            NavMeshAgent prefabAgent = ActiveConfig.Units[i].UnitSO.Prefab.GetComponent<NavMeshAgent>();
                            int agentType = prefabAgent != null ? prefabAgent.agentTypeID : 0;
                            NavMeshQueryFilter agentFilter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };

                            Vector3 spawnPos = tacticalSlot;
                            if (TryFindSolidSpawnPosition(tacticalSlot, isHigh, agentFilter, out Vector3 solidPos))
                            {
                                spawnPos = solidPos;
                            }
                            else if (NavMesh.SamplePosition(tacticalSlot, out NavMeshHit navHit, 3.5f, agentFilter))
                            {
                                if (navHit.position.y >= 0.08f) spawnPos = navHit.position;
                            }
                            if (!isHigh && spawnPos.y < 0.10f) spawnPos.y = 0.10f;

                            Defender defender = Instantiate(
                                ActiveConfig.Units[i].UnitSO.Prefab,
                                spawnPos,
                                Quaternion.Euler(0, Random.Range(0, 360f), 0)
                            ) as Defender;

                            if (defender != null)
                            {
                                defender.transform.position = spawnPos;
                                if (defender.Agent != null)
                                {
                                    defender.Agent.radius = 0.22f;
                                    defender.Agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                                    defender.Agent.autoTraverseOffMeshLink = true;
                                    defender.Agent.enabled = true;
                                    defender.SafeWarpOrPlace(spawnPos, 3.5f);
                                }
                                if (defender.TryGetComponent(out Rigidbody rb))
                                {
                                    rb.isKinematic = true;
                                    rb.useGravity = false;
                                }
                            }

                            Defenders[ActiveConfig.Units[i].UnitSO.Type].Add(defender);
                            defender.OnDeath += Defender_OnDeath;
                            OnSpawnDefender?.Invoke(defender);

                            failedToSpawnUnit = false;
                            Debug.Log($"Spawning defender of type {defender.UnitType.Type} for cost {cost} at {spawnPos} in sector {sector}");
                            remainingSpend -= cost;
                            ResourcesToSpend -= cost;

                            defender.AssignDefensiveSector(sector, tacticalSlot, waypoints);
                            break;
                        }
                    }

                    value -= weights[i];
                }
            }

            Debug.Log($"Spent {initialSpend - remainingSpend} on units with a max spend of {maxSpend} and ratio {resourceSpend}.");
            return initialSpend - remainingSpend;
        }

        private void Defender_OnDeath(IDamageable damageable)
        {
            Defender defender = damageable.Transform.GetComponent<Defender>();
            if (defender != null && defender.UnitType != null && Defenders.ContainsKey(defender.UnitType.Type))
            {
                Defenders[defender.UnitType.Type].Remove(defender);
            }
            if (defender != null)
            {
                defender.OnDeath -= Defender_OnDeath;
                OnDefenderDeath?.Invoke(defender);
            }
        }

        public int TotalAliveDefenders
        {
            get
            {
                int count = 0;
                foreach (var pair in Defenders)
                {
                    count += pair.Value.Count(d => d != null);
                }
                return count;
            }
        }

        public List<Vector3> WaypointsList => Waypoints;

        public void RegisterDefender(Defender defender)
        {
            if (defender == null) return;
            UnitType type = defender.UnitType != null ? defender.UnitType.Type : UnitType.Archer;
            if (!Defenders.ContainsKey(type))
            {
                Defenders[type] = new HashSet<Defender>();
            }
            Defenders[type].Add(defender);
            defender.OnDeath += Defender_OnDeath;
            OnSpawnDefender?.Invoke(defender);
        }

        public static readonly Vector3[] CenterTowerLookoutSlots = new Vector3[]
        {
            new Vector3(-3.89f, 4.15f, -11.80f), // Tiền duyên chính diện đón đầu quái (ban công trước thoáng đãng)
            new Vector3(-5.50f, 4.15f, -12.30f), // Ban công chếch trái
            new Vector3(-2.20f, 4.15f, -12.30f), // Ban công chếch phải
            new Vector3(-6.20f, 4.15f, -14.70f), // Cửa ngõ thông sang Cầu 1 (Trái)
            new Vector3(-1.50f, 4.15f, -14.70f), // Cửa ngõ thông sang Cầu 3 (Phải)
            new Vector3(-3.89f, 4.15f, -17.50f)  // Boong sau hướng đón thang lên từ làng
        };

        public static readonly Vector3[] LeftTowerLookoutSlots = new Vector3[]
        {
            new Vector3(-23.87f, 4.15f, -11.80f), // Tiền duyên chính diện (ban công trước)
            new Vector3(-25.50f, 4.15f, -12.30f), // Ban công chếch trái
            new Vector3(-22.20f, 4.15f, -12.30f), // Ban công chếch phải
            new Vector3(-21.00f, 4.15f, -14.70f), // Cửa ngõ thông sang Cầu 1
            new Vector3(-23.87f, 4.15f, -17.50f)  // Boong sau đón thang lên từ làng
        };

        public static readonly Vector3[] RightTowerLookoutSlots = new Vector3[]
        {
            new Vector3(26.10f, 4.15f, -11.80f), // Tiền duyên chính diện (ban công trước)
            new Vector3(24.40f, 4.15f, -12.30f), // Ban công chếch trái
            new Vector3(27.80f, 4.15f, -12.30f), // Ban công chếch phải
            new Vector3(23.50f, 4.15f, -14.70f), // Cửa ngõ thông sang Cầu 2
            new Vector3(26.10f, 4.15f, -17.50f)  // Boong sau đón thang lên từ làng
        };

        // Cầu Gỗ Tây: Z = -14.70m, X từ -16.5m đến -11.3m (3 vị trí chiến thuật cố định)
        public static readonly Vector3[] WestBridgeSlots = new Vector3[]
        {
            new Vector3(-16.50f, 4.10f, -14.70f),
            new Vector3(-13.91f, 4.10f, -14.70f),
            new Vector3(-11.30f, 4.10f, -14.70f)
        };

        // Cầu Gỗ Đông: Z = -14.70m, X từ 3.5m đến 18.5m (5 vị trí chiến thuật cố định)
        public static readonly Vector3[] EastBridgeSlots = new Vector3[]
        {
            new Vector3(3.50f, 4.10f, -14.70f),
            new Vector3(7.20f, 4.10f, -14.70f),
            new Vector3(11.10f, 4.10f, -14.70f),
            new Vector3(15.00f, 4.10f, -14.70f),
            new Vector3(18.50f, 4.10f, -14.70f)
        };

        public static readonly Vector3[] BridgeLookoutSlots = new Vector3[]
        {
            new Vector3(-16.50f, 4.10f, -14.70f),
            new Vector3(-13.91f, 4.10f, -14.70f),
            new Vector3(-11.30f, 4.10f, -14.70f),
            new Vector3(3.50f, 4.10f, -14.70f),
            new Vector3(7.20f, 4.10f, -14.70f),
            new Vector3(11.10f, 4.10f, -14.70f),
            new Vector3(15.00f, 4.10f, -14.70f),
            new Vector3(18.50f, 4.10f, -14.70f)
        };

        public static readonly Vector3[] WestBridgePatrolWaypoints = new Vector3[]
        {
            new Vector3(-17.00f, 4.10f, -14.70f),
            new Vector3(-13.91f, 4.10f, -14.70f),
            new Vector3(-11.00f, 4.10f, -14.70f)
        };

        public static readonly Vector3[] EastBridgePatrolWaypoints = new Vector3[]
        {
            new Vector3(3.00f, 4.10f, -14.70f),
            new Vector3(8.00f, 4.10f, -14.70f),
            new Vector3(13.00f, 4.10f, -14.70f),
            new Vector3(18.00f, 4.10f, -14.70f)
        };

        // Tuyến phòng thủ tiền tuyến mặt đất: Dàn trải đều theo hàng ngang đón đầu quái vật (Z = -7.0m đến -10.0m)
        public static readonly Vector3[] FrontlineGroundSlots = new Vector3[]
        {
            // Cửa chính trung tâm tiền tuyến
            new Vector3(0.00f, 0.10f, -7.00f),
            new Vector3(-3.50f, 0.10f, -7.50f),
            new Vector3(3.50f, 0.10f, -7.50f),

            // Tiền tuyến Cánh Trái (trải rộng bảo vệ sườn Tây)
            new Vector3(-8.00f, 0.10f, -8.00f),
            new Vector3(-13.00f, 0.10f, -8.50f),
            new Vector3(-18.00f, 0.10f, -9.00f),
            new Vector3(-22.00f, 0.10f, -9.50f),

            // Tiền tuyến Cánh Phải (trải rộng bảo vệ sườn Đông)
            new Vector3(8.00f, 0.10f, -8.00f),
            new Vector3(13.00f, 0.10f, -8.50f),
            new Vector3(18.00f, 0.10f, -9.00f),
            new Vector3(22.00f, 0.10f, -9.50f)
        };

        // Tuyến trung tâm bảo vệ chân dốc thang và cổng làng (Z = -16.0m đến -21.0m)
        public static readonly Vector3[] MidfieldGroundSlots = new Vector3[]
        {
            new Vector3(-3.89f, 0.10f, -20.50f), // Chân dốc lên chòi giữa
            new Vector3(-23.87f, 0.10f, -20.50f), // Chân dốc lên chòi trái
            new Vector3(26.10f, 0.10f, -20.50f), // Chân dốc lên chòi phải
            new Vector3(-7.00f, 0.10f, -16.00f),
            new Vector3(7.00f, 0.10f, -16.00f)
        };

        // Tuyến sân làng và đường làng thông thoáng (Z = -19.0m đến -26.0m, hoàn toàn tránh xa hàng rào chông Z = -30.62m)
        public static readonly Vector3[] WoodenRoadSlots = new Vector3[]
        {
            new Vector3(-16.00f, 0.10f, -22.50f),
            new Vector3(-11.50f, 0.10f, -24.00f),
            new Vector3(-7.00f, 0.10f, -21.00f),
            new Vector3(-2.00f, 0.10f, -23.50f),
            new Vector3(3.00f, 0.10f, -22.00f),
            new Vector3(8.00f, 0.10f, -24.00f),
            new Vector3(12.50f, 0.10f, -21.50f),
            new Vector3(17.00f, 0.10f, -23.00f),
            new Vector3(-5.00f, 0.10f, -25.50f),
            new Vector3(5.00f, 0.10f, -25.50f)
        };

        public static readonly Vector3[] HighGroundPatrolWaypoints = new Vector3[]
        {
            new Vector3(-23.87f, 4.15f, -11.80f), // Ban công Chòi Trái (thoáng đãng)
            new Vector3(-15.50f, 4.15f, -14.70f), // Tim Cầu 1 (nối Trái - Giữa)
            new Vector3(-3.89f, 4.15f, -11.80f),  // Ban công Chòi Giữa (thoáng đãng)
            new Vector3(5.50f, 4.15f, -14.70f),   // Tim Cầu 3 (nối Giữa - Phải)
            new Vector3(15.50f, 4.15f, -14.70f),  // Tim Cầu 2 (đoạn nối tiếp)
            new Vector3(26.10f, 4.15f, -11.80f)   // Ban công Chòi Phải (thoáng đãng)
        };

        public static readonly Vector3[] FrontlinePatrolWaypoints = new Vector3[]
        {
            new Vector3(-18.00f, 0.10f, -8.50f), // Cánh trái
            new Vector3(-7.00f, 0.10f, -7.50f),  // Trung trái
            new Vector3(0.00f, 0.10f, -7.00f),   // Cửa chính
            new Vector3(7.00f, 0.10f, -7.50f),   // Trung phải
            new Vector3(18.00f, 0.10f, -8.50f)   // Cánh phải
        };

        public static readonly Vector3[] WoodenRoadPatrolWaypoints = new Vector3[]
        {
            new Vector3(-15.00f, 0.10f, -22.50f),
            new Vector3(-7.00f, 0.10f, -24.00f),
            new Vector3(0.00f, 0.10f, -21.50f),
            new Vector3(7.00f, 0.10f, -24.00f),
            new Vector3(15.00f, 0.10f, -22.50f)
        };

        private static int s_NextZoneRoundRobin = 0;

        public static bool TryFindSolidSpawnPosition(Vector3 targetPos, bool isHighGround, NavMeshQueryFilter filter, out Vector3 validSpawnPos)
        {
            validSpawnPos = targetPos;

            // 1. Raycast từ trên cao xuống để tìm chính xác mặt sàn / mặt đất cứng
            float rayStartY = isHighGround ? 8.0f : 5.0f;
            Vector3 rayOrigin = new Vector3(targetPos.x, rayStartY, targetPos.z);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 16.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Bỏ qua nếu trúng sinh vật sống (Unit khác)
                if (hit.collider.GetComponentInParent<Unit.Unit>() == null || hit.collider.GetComponentInParent<Unit.Wall>() != null)
                {
                    if (isHighGround)
                    {
                        // Sàn chòi / cầu trên cao ở khoảng Y = 3.8m đến 4.6m (tránh nóc lều Y = 7.2m)
                        if (hit.point.y >= 3.8f && hit.point.y <= 4.6f)
                        {
                            if (NavMesh.SamplePosition(hit.point, out NavMeshHit nHit, 2.0f, filter))
                            {
                                if (nHit.position.y >= 3.8f)
                                {
                                    validSpawnPos = nHit.position;
                                    return true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Mặt đất tự nhiên của làng ở khoảng Y = 0.05m đến 0.50m (tuyệt đối không spawn trên đỉnh cọc rào / tường Y = 1m - 3m)
                        if (hit.point.y >= 0.05f && hit.point.y <= 0.50f)
                        {
                            if (NavMesh.SamplePosition(hit.point, out NavMeshHit nHit, 3.0f, filter))
                            {
                                if (nHit.position.y <= 0.60f)
                                {
                                    validSpawnPos = nHit.position;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Fallback sang NavMesh.SamplePosition với bộ lọc cao độ chặt chẽ
            if (isHighGround)
            {
                Vector3 highTarget = new Vector3(targetPos.x, 4.15f, targetPos.z);
                if (NavMesh.SamplePosition(highTarget, out NavMeshHit nHit, 2.5f, filter))
                {
                    if (nHit.position.y >= 3.8f)
                    {
                        validSpawnPos = nHit.position;
                        return true;
                    }
                }
            }
            else
            {
                Vector3 groundTarget = new Vector3(targetPos.x, 0.10f, targetPos.z);
                if (NavMesh.SamplePosition(groundTarget, out NavMeshHit nHit, 3.5f, filter))
                {
                    if (nHit.position.y <= 0.60f)
                    {
                        validSpawnPos = nHit.position;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsSlotOccupied(Vector3 slotPos, float thresholdRadius = 1.8f)
        {
            float sqrThresh = thresholdRadius * thresholdRadius;
            foreach (var pair in Defenders)
            {
                foreach (var d in pair.Value)
                {
                    if (d == null || d.Health <= 0) continue;
                    Vector3 assigned = d.IsStationedAtTower ? d.TowerPostPosition : d.DefaultPostPosition;
                    Vector3 diff = assigned - slotPos;
                    diff.y = 0;
                    if (diff.sqrMagnitude < sqrThresh) return true;
                }
            }
            return false;
        }

        public Vector3 GetNextAvailableTacticalSlot(UnitType unitType, out DefenderSector sector, out List<Vector3> patrolWaypoints)
        {
            // Luân phiên phân bổ đồng đều 100% giữa 3 khu vực chiến thuật do người dùng yêu cầu:
            // Sector 0: Trên chòi và cầu gỗ trên cao (HighGroundWatchtowers)
            // Sector 1: Dưới đất tiền tuyến phía trước (GroundFrontline)
            // Sector 2: Trên con đường và sân làng thông thoáng (VillageWoodenRoad)
            int startSector = (s_NextZoneRoundRobin++) % 3;

            for (int s = 0; s < 3; s++)
            {
                int currentSector = (startSector + s) % 3;
                switch (currentSector)
                {
                    case 0: // Cầu gỗ trên cao (3 vị trí cố định Cầu Tây, 5 vị trí cố định Cầu Đông; KHÔNG trên chòi)
                    {
                        foreach (var slot in WestBridgeSlots)
                        {
                            if (!IsSlotOccupied(slot, 2.0f))
                            {
                                sector = DefenderSector.HighGroundWatchtowers;
                                patrolWaypoints = new List<Vector3>(WestBridgePatrolWaypoints);
                                return slot;
                            }
                        }
                        foreach (var slot in EastBridgeSlots)
                        {
                            if (!IsSlotOccupied(slot, 2.0f))
                            {
                                sector = DefenderSector.HighGroundWatchtowers;
                                patrolWaypoints = new List<Vector3>(EastBridgePatrolWaypoints);
                                return slot;
                            }
                        }
                        break;
                    }

                    case 1: // Dưới đất tiền tuyến đón đầu quái
                    {
                        foreach (var slot in FrontlineGroundSlots)
                        {
                            if (!IsSlotOccupied(slot, 2.0f))
                            {
                                sector = DefenderSector.GroundFrontline;
                                patrolWaypoints = new List<Vector3>(FrontlinePatrolWaypoints);
                                return slot;
                            }
                        }
                        foreach (var slot in MidfieldGroundSlots)
                        {
                            if (!IsSlotOccupied(slot, 2.0f))
                            {
                                sector = DefenderSector.GroundFrontline;
                                patrolWaypoints = new List<Vector3>(FrontlinePatrolWaypoints);
                                return slot;
                            }
                        }
                        break;
                    }

                    case 2: // Sân làng và lối đi nội bộ thông thoáng
                    {
                        foreach (var slot in WoodenRoadSlots)
                        {
                            if (!IsSlotOccupied(slot, 2.0f))
                            {
                                sector = DefenderSector.VillageWoodenRoad;
                                patrolWaypoints = new List<Vector3>(WoodenRoadPatrolWaypoints);
                                return slot;
                            }
                        }
                        break;
                    }
                }
            }

            // Fallback ngẫu nhiên trên sân làng an toàn
            sector = DefenderSector.VillageWoodenRoad;
            patrolWaypoints = new List<Vector3>(WoodenRoadPatrolWaypoints);
            float randomX = Random.Range(-16.0f, 16.0f);
            float randomZ = Random.Range(-20.0f, -25.5f);
            Vector3 fallbackPos = new Vector3(randomX, 0.10f, randomZ);
            if (NavMesh.SamplePosition(fallbackPos, out NavMeshHit fHit, 4.0f, NavMesh.AllAreas))
            {
                return fHit.position;
            }
            return fallbackPos;
        }

        public Vector3 GetNextAvailableTacticalSlot(UnitType unitType, out bool isHighGround)
        {
            Vector3 slot = GetNextAvailableTacticalSlot(unitType, out DefenderSector sector, out _);
            isHighGround = (sector == DefenderSector.HighGroundWatchtowers);
            return slot;
        }

        public void AssignAllDefendersTacticalPositions()
        {
            foreach (var pair in Defenders)
            {
                foreach (var def in pair.Value)
                {
                    if (def != null && def.Health > 0 && !def.HasRecordedDefaultPost)
                    {
                        Vector3 slot = GetNextAvailableTacticalSlot(pair.Key, out DefenderSector sector, out List<Vector3> waypoints);
                        def.AssignDefensiveSector(sector, slot, waypoints);
                    }
                }
            }
        }

        public Vector3 GetAvailableTowerSlot(Vector3 referencePos)
        {
            Vector3[] slots = CenterTowerLookoutSlots;
            float distCenter = Vector3.Distance(new Vector3(-3.89f, 0, -14.70f), new Vector3(referencePos.x, 0, referencePos.z));
            float distLeft = Vector3.Distance(new Vector3(-23.87f, 0, -14.70f), new Vector3(referencePos.x, 0, referencePos.z));
            float distRight = Vector3.Distance(new Vector3(26.10f, 0, -14.80f), new Vector3(referencePos.x, 0, referencePos.z));

            if (distLeft < distCenter && distLeft < distRight) slots = LeftTowerLookoutSlots;
            else if (distRight < distCenter && distRight < distLeft) slots = RightTowerLookoutSlots;

            foreach (var slot in slots)
            {
                if (!IsSlotOccupied(slot, 1.5f)) return slot;
            }

            return slots[0];
        }

        public void EnsureDefendersAlive(int minDefenders = 4)
        {
            if (ActiveConfig == null || ActiveConfig.Units == null || ActiveConfig.Units.Count == 0) return;
            if (NavMeshManager.Instance == null || NavMeshManager.Instance.EnemyTriangulation.vertices == null) return;
            if (NavMeshManager.Instance.EnemyTriangulation.vertices.Length < 2) return;

            // Dọn dẹp các mục null
            foreach (var pair in Defenders)
            {
                pair.Value.RemoveWhere(d => d == null);
            }

            // Đảm bảo toàn bộ chiến binh đang sống được phân bổ vào các vị trí chiến thuật
            AssignAllDefendersTacticalPositions();

            int currentCount = TotalAliveDefenders;
            if (currentCount < minDefenders)
            {
                int needed = minDefenders - currentCount;

                for (int i = 0; i < needed; i++)
                {
                    var unitConfig = ActiveConfig.Units[Random.Range(0, ActiveConfig.Units.Count)];
                    if (unitConfig.UnitSO == null || unitConfig.UnitSO.Prefab == null) continue;

                    Vector3 tacticalSlot = GetNextAvailableTacticalSlot(unitConfig.UnitSO.Type, out DefenderSector sector, out List<Vector3> waypoints);
                    bool isHigh = (sector == DefenderSector.HighGroundWatchtowers);

                    NavMeshAgent prefabAgent = unitConfig.UnitSO.Prefab.GetComponent<NavMeshAgent>();
                    int agentType = prefabAgent != null ? prefabAgent.agentTypeID : 0;
                    NavMeshQueryFilter agentFilter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };

                    Vector3 spawnPos = tacticalSlot;
                    if (TryFindSolidSpawnPosition(tacticalSlot, isHigh, agentFilter, out Vector3 solidPos))
                    {
                        spawnPos = solidPos;
                    }
                    else if (NavMesh.SamplePosition(tacticalSlot, out NavMeshHit hit, 3.5f, agentFilter))
                    {
                        if (hit.position.y >= 0.08f) spawnPos = hit.position;
                    }
                    if (!isHigh && spawnPos.y < 0.10f) spawnPos.y = 0.10f;

                    Defender defender = Instantiate(
                        unitConfig.UnitSO.Prefab,
                        spawnPos,
                        Quaternion.Euler(0, Random.Range(0, 360f), 0)
                    ) as Defender;

                    if (defender != null)
                    {
                        defender.transform.position = spawnPos;
                        if (defender.Agent != null)
                        {
                            defender.Agent.radius = 0.22f;
                            defender.Agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                            defender.Agent.autoTraverseOffMeshLink = true;
                            defender.Agent.enabled = true;
                            defender.SafeWarpOrPlace(spawnPos, 3.5f);
                        }
                        if (defender.TryGetComponent(out Rigidbody rb))
                        {
                            rb.isKinematic = true;
                            rb.useGravity = false;
                        }
                        if (!Defenders.ContainsKey(unitConfig.UnitSO.Type))
                        {
                            Defenders[unitConfig.UnitSO.Type] = new HashSet<Defender>();
                        }
                        Defenders[unitConfig.UnitSO.Type].Add(defender);
                        defender.OnDeath += Defender_OnDeath;
                        OnSpawnDefender?.Invoke(defender);

                        defender.AssignDefensiveSector(sector, tacticalSlot, waypoints);
                    }
                }
            }
        }

        public void ClearAllDefenders()
        {
            foreach (var pair in Defenders)
            {
                foreach (Defender d in pair.Value)
                {
                    if (d != null)
                    {
                        Destroy(d.gameObject);
                    }
                }
                pair.Value.Clear();
            }
        }

        private Coroutine _StuckValidationCoroutine;

        /// <summary>
        /// Kích hoạt quét và dọn dẹp các NPC bị kẹt ngay khi bắt đầu ván mới hoặc bắt đầu round mới (chỉ chạy trong lúc chuẩn bị, tuyệt đối không chạy khi đang đánh nhau).
        /// </summary>
        public void TriggerStuckValidation()
        {
            if (_StuckValidationCoroutine != null)
            {
                StopCoroutine(_StuckValidationCoroutine);
            }
            _StuckValidationCoroutine = StartCoroutine(ValidateAndRespawnStuckDefendersRoutine());
        }

        public IEnumerator ValidateAndRespawnStuckDefendersRoutine()
        {
            // Chỉ chạy trong giai đoạn chuẩn bị / round mới, tuyệt đối KHÔNG chạy khi đang trong trận chiến
            if (RoundManager.Instance != null && RoundManager.Instance.State == GameState.Running)
            {
                yield break;
            }

            yield return new WaitForSeconds(0.5f);

            if (RoundManager.Instance != null && RoundManager.Instance.State == GameState.Running)
            {
                yield break;
            }

            foreach (var pair in Defenders)
            {
                foreach (var def in pair.Value)
                {
                    if (def == null || def.Health <= 0) continue;

                    Vector3 pos = def.transform.position;
                    // Nếu NPC mất kết nối NavMesh hoặc bị rơi lơ lửng, đưa về vị trí chốt đã phân công
                    if (def.Agent == null || !def.Agent.enabled || !def.Agent.isOnNavMesh)
                    {
                        Vector3 targetPos = def.HasRecordedDefaultPost ? def.DefaultPostPosition : pos;
                        def.SafeWarpOrPlace(targetPos, 4.0f);
                    }
                    else if (pos.y >= 3.0f && !Defender.IsOnValidHighGroundPlane(pos))
                    {
                        // Nếu ở trên cao nhưng lệch khỏi cầu gỗ, đưa về vị trí chốt an toàn trên cầu
                        Vector3 targetPos = def.HasRecordedDefaultPost ? def.DefaultPostPosition : pos;
                        def.SafeWarpOrPlace(targetPos, 4.0f);
                    }
                }
            }
        }

        private Vector3 GetRandomSafeGroundSlot()
        {
            List<Vector3> available = new List<Vector3>();
            foreach (var s in FrontlineGroundSlots) { if (!IsSlotOccupied(s, 1.8f)) available.Add(s); }
            foreach (var s in MidfieldGroundSlots) { if (!IsSlotOccupied(s, 1.8f)) available.Add(s); }
            foreach (var s in WoodenRoadSlots) { if (!IsSlotOccupied(s, 1.8f)) available.Add(s); }

            if (available.Count > 0)
            {
                return available[Random.Range(0, available.Count)];
            }
            return new Vector3(Random.Range(-10f, 10f), 0.10f, Random.Range(-18f, -24f));
        }

        [System.Serializable]
        private struct WallData : IEquatable<WallData>
        {
            public readonly Wall Wall;
            public readonly int UpgradeLevel;
            public readonly int TimesDied;

            public WallData(Wall wall, int upgradeLevel, int timesDied)
            {
                Wall = wall;
                UpgradeLevel = upgradeLevel;
                TimesDied = timesDied;
            }

            public bool Equals(WallData other)
            {
                return Equals(Wall, other.Wall) && UpgradeLevel == other.UpgradeLevel && TimesDied == other.TimesDied;
            }

            public override bool Equals(object obj)
            {
                return obj is WallData other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Wall, UpgradeLevel, TimesDied);
            }
        }
    }
}
