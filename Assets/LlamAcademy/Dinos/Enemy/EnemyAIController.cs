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

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"Multiple EnemyAIController detected. Deleting the second one {name}");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            Waypoints = WaypointTransforms.ConvertAll(transform => transform.position);
            Defenders.Add(UnitType.Archer, new());
            Defenders.Add(UnitType.Mage, new());
            Defenders.Add(UnitType.Cannoneer, new());

            Wall[] baseWalls = FindObjectsByType<Wall>(FindObjectsInactive.Include);
            for (int i = 0; i < baseWalls.Length; i++)
            {
                WallDatas.Add(new WallData(baseWalls[i], 0, 0));
                baseWalls[i].OnDeath += HandleWallDeath;
                baseWalls[i].OnTakeDamage += HandleWallDamage;
            }
        }

        private void HandleWallDamage(IDamageable wall, int damage)
        {
            if (RoundManager.Instance.State != GameState.Running) return;

            foreach (Defender defender in Defenders.SelectMany(keyValuePair => keyValuePair.Value.Where((def) => UnitShouldChangeTarget(def, wall))))
            {
                if (NavMesh.SamplePosition(wall.Transform.position, out NavMeshHit hit, 10,
                        new NavMeshQueryFilter()
                            { agentTypeID = defender.Agent.agentTypeID, areaMask = defender.Agent.areaMask }))
                {
                    defender.SetDestination(hit.position);
                }
                else
                {
                    Debug.LogWarning($"Could not find location close to wall {wall.Transform.name} to send defender to!");
                }
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
            Wall diedWall = diedObject.Transform.GetComponent<Wall>();
            int diedIndex = WallDatas.FindIndex((wallData) => wallData.Wall.Equals(diedWall));
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

        private void Instance_OnNavMeshUpdated()
        {
            // remove all invalid points
            Waypoints.RemoveAll((waypoint) => !NavMesh.SamplePosition(waypoint, out _, 1, NavMesh.AllAreas));
            foreach (KeyValuePair<UnitType, HashSet<Defender>> defenders in Defenders)
            {
                foreach (Defender defender in defenders.Value)
                {
                    if (defender.State == AIState.Patrol)
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

            List<Wall> walls = WallDatas.ConvertAll(data => data.Wall);
            // repair lowest % hp walls first
            walls.Sort((a, b) => (a.Health / (float)a.MaxHealth).CompareTo(b.Health / (float)b.MaxHealth));

            foreach (Wall wall in walls)
            {
                int healthRepairNeeded = wall.MaxHealth - wall.Health;
                int cost = Mathf.CeilToInt(healthRepairNeeded * wall.HealthToCostMultiplier);
                if (wall != null)
                {
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
                        healthRepairNeeded = Mathf.CeilToInt(remainingSpend / wall.HealthToCostMultiplier);
                        wall.Repair(healthRepairNeeded);
                        Debug.Log($"Partially repairing wall {healthRepairNeeded} at cost of {cost}");
                        remainingSpend -= cost;
                        ResourcesToSpend -= cost;
                        break;
                    }
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

            // prefer to upgrade walls where the player is attacking, then the lowest upgrade level
            List<WallData> upgradePriority = WallDatas
                .OrderByDescending(wallData => wallData.TimesDied)
                .ThenBy(wallData => wallData.UpgradeLevel)
                .Where(wallData => wallData.Wall.UnitType.Upgrade != null)
                .ToList();

            Debug.Log($"Found {upgradePriority.Count} possible upgrades at the current tier.");
            foreach (WallData wallData in upgradePriority)
            {
                UnitSO upgrade = wallData.Wall.UnitType.Upgrade;
                if (remainingSpend >= upgrade.Cost)
                {
                    int index = WallDatas.FindIndex(data => data.Equals(wallData));
                    Wall upgradedWall = Instantiate(
                        upgrade.Prefab,
                        wallData.Wall.transform.position,
                        wallData.Wall.Transform.rotation,
                        wallData.Wall.transform.parent
                    ) as Wall;

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

            if (remainingSpend > 0 && depth == 0)
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
                            int index = Random.Range(1, vertices.Length);

                            Defender defender = Instantiate(
                                ActiveConfig.Units[i].UnitSO.Prefab,
                                Vector3.Lerp(vertices[index - 1], vertices[index], Random.value),
                                Quaternion.Euler(0, Random.Range(0, 360f), 0)
                            ) as Defender;

                            Defenders[ActiveConfig.Units[i].UnitSO.Type].Add(defender);
                            defender.OnDeath += Defender_OnDeath;
                            OnSpawnDefender?.Invoke(defender);

                            failedToSpawnUnit = false;
                            Debug.Log($"Spawning defender of type {defender.UnitType.Type} for cost {cost}");
                            remainingSpend -= cost;
                            ResourcesToSpend -= cost;

                            defender.Patrol(Waypoints);
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

            int currentCount = TotalAliveDefenders;
            if (currentCount < minDefenders)
            {
                int needed = minDefenders - currentCount;
                Vector3[] vertices = NavMeshManager.Instance.EnemyTriangulation.vertices;

                for (int i = 0; i < needed; i++)
                {
                    var unitConfig = ActiveConfig.Units[Random.Range(0, ActiveConfig.Units.Count)];
                    if (unitConfig.UnitSO == null || unitConfig.UnitSO.Prefab == null) continue;

                    int index = Random.Range(1, vertices.Length);
                    Vector3 spawnPos = Vector3.Lerp(vertices[index - 1], vertices[index], Random.value);

                    Defender defender = Instantiate(
                        unitConfig.UnitSO.Prefab,
                        spawnPos,
                        Quaternion.Euler(0, Random.Range(0, 360f), 0)
                    ) as Defender;

                    if (defender != null)
                    {
                        if (!Defenders.ContainsKey(unitConfig.UnitSO.Type))
                        {
                            Defenders[unitConfig.UnitSO.Type] = new HashSet<Defender>();
                        }
                        Defenders[unitConfig.UnitSO.Type].Add(defender);
                        defender.OnDeath += Defender_OnDeath;
                        OnSpawnDefender?.Invoke(defender);
                        defender.Patrol(Waypoints);
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
