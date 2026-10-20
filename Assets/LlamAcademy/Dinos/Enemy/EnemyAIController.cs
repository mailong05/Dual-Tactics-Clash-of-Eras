using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using LlamAcademy.Dinos.Utility;
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

        public static readonly Vector3[] VillageDefenseSpots = new Vector3[]
        {
            // --- XEN KẼ TIỀN TUYẾN - TRUNG TUYẾN - HẬU TUYẾN (Đảm bảo mọi Wave từ Wave 1 đều có Cận Vệ Căn Cứ) ---
            new Vector3(-6.5f, 0.0f, -14.0f),  // Chốt 0: Tiền tuyến sân trái cổng làng (bọc sườn chòi trái)
            new Vector3(4.5f, 0.0f, -14.0f),   // Chốt 1: Tiền tuyến sân phải cổng làng (bọc sườn chòi phải)
            new Vector3(-2.2f, 0.0f, -30.0f),  // Chốt 2: [HẬU TUYẾN] Vệ binh trung tâm trước Căn Cứ / Đài Tế
            new Vector3(-2.2f, 0.0f, -18.5f),  // Chốt 3: [TRUNG TUYẾN] Tiền trạm trung tâm sân làng
            new Vector3(-4.0f, 0.0f, -32.0f),  // Chốt 4: [HẬU TUYẾN] Cận vệ bảo vệ Căn Cứ / Trứng (tả)
            new Vector3(-2.2f, 0.0f, -15.5f),  // Chốt 5: Tiền tuyến trục chính sân làng (sau nhịp cầu)
            new Vector3(-0.4f, 0.0f, -32.0f),  // Chốt 6: [HẬU TUYẾN] Cận vệ bảo vệ Căn Cứ / Trứng (hữu)
            new Vector3(-4.5f, 0.0f, -16.5f),  // Chốt 7: Sân trái trung tâm (cạnh cầu gỗ)
            new Vector3(2.8f, 0.0f, -16.5f),   // Chốt 8: Sân phải trung tâm (trước lều rơm)
            new Vector3(-2.2f, 0.0f, -34.0f),  // Chốt 9: [HẬU TUYẾN] Hộ vệ tối hậu sát Căn Cứ Làng
            new Vector3(-5.0f, 0.0f, -21.0f),  // Chốt 10: [TRUNG TUYẾN] Lối ngõ sườn trái về căn cứ
            new Vector3(3.5f, 0.0f, -21.0f),   // Chốt 11: [TRUNG TUYẾN] Lối ngõ sườn phải về căn cứ
            new Vector3(-2.2f, 0.0f, -27.5f),  // Chốt 12: [HẬU TUYẾN] Vành đai bảo vệ Căn Cứ Làng
            new Vector3(-8.5f, 0.0f, -16.5f),  // Chốt 13: Cánh tây sân trái (sát hàng rào)
            new Vector3(6.8f, 0.0f, -16.5f),   // Chốt 14: Cánh đông sân phải (sát hàng rào)
            new Vector3(-6.5f, 0.0f, -19.0f),  // Chốt 15: Hậu tuyến sân trái
            new Vector3(4.5f, 0.0f, -19.0f),   // Chốt 16: Hậu tuyến sân phải
            new Vector3(-8.5f, 0.0f, -13.5f),  // Chốt 17: Góc tây bắc sân trái
            new Vector3(6.5f, 0.0f, -13.5f),   // Chốt 18: Góc đông bắc sân phải
            new Vector3(-0.5f, 0.0f, -15.0f),  // Chốt 19: Cánh hữu trung tâm
            new Vector3(-3.8f, 0.0f, -15.0f),  // Chốt 20: Cánh tả trung tâm
            new Vector3(-2.2f, 0.0f, -24.0f),  // Chốt 21: Vệ binh tiền đồn căn cứ
            new Vector3(-4.5f, 0.0f, -25.5f),  // Chốt 22: Vệ binh cánh trái căn cứ
            new Vector3(0.5f, 0.0f, -25.5f),   // Chốt 23: Vệ binh cánh phải căn cứ
            new Vector3(-3.2f, 0.0f, -12.5f),  // Chốt 24: Tiền tuyến trước vòm cổng (tả)
            new Vector3(-1.2f, 0.0f, -12.5f),  // Chốt 25: Tiền tuyến trước vòm cổng (hữu)
            new Vector3(-7.5f, 0.0f, -15.0f),  // Chốt 26: Vệ binh cánh trái trước
            new Vector3(5.5f, 0.0f, -15.0f),   // Chốt 27: Vệ binh cánh phải trước
            new Vector3(-9.5f, 0.0f, -18.0f),  // Chốt 28: Vành đai Tây ngoài
            new Vector3(7.8f, 0.0f, -18.0f),   // Chốt 29: Vành đai Đông ngoài
            new Vector3(-0.8f, 0.0f, -17.5f),  // Chốt 30: Trục trung tâm làng (hữu)
            new Vector3(-3.6f, 0.0f, -17.5f),  // Chốt 31: Trục trung tâm làng (tả)
            new Vector3(-6.2f, 0.0f, -22.5f),  // Chốt 32: Vành đai tiếp cận sườn trái
            new Vector3(4.2f, 0.0f, -22.5f),   // Chốt 33: Vành đai tiếp cận sườn phải
            new Vector3(-1.2f, 0.0f, -21.5f),  // Chốt 34: Trung tuyến sau làng (hữu)
            new Vector3(-3.2f, 0.0f, -21.5f)   // Chốt 35: Trung tuyến sau làng (tả)
        };

        /// <summary>
        /// Tìm tọa độ điểm xuất hiện đảm bảo 100% nằm trên NavMesh của chiến binh làng (Humanoid).
        /// </summary>
        public static Vector3 GetValidDefenderSpawnPosition(int index)
        {
            Vector3 targetSpot = VillageDefenseSpots[Mathf.Abs(index) % VillageDefenseSpots.Length];

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = 0, // Humanoid
                areaMask = NavMesh.AllAreas
            };

            // 1. Thử sample vị trí với filter Humanoid quanh 4m
            if (NavMesh.SamplePosition(targetSpot, out NavMeshHit hit, 4.0f, filter))
            {
                return hit.position;
            }

            // 2. Thử sample vị trí rộng hơn 8m
            if (NavMesh.SamplePosition(targetSpot, out NavMeshHit hitWide, 8.0f, filter))
            {
                return hitWide.position;
            }

            // 3. Dự phòng bằng các đỉnh đã bake trên EnemySurface trong phạm vi sân làng (Z: -28 đến -12)
            if (NavMeshManager.Instance != null && NavMeshManager.Instance.EnemyTriangulation.vertices != null && NavMeshManager.Instance.EnemyTriangulation.vertices.Length > 0)
            {
                var verts = NavMeshManager.Instance.EnemyTriangulation.vertices;
                float bestDist = float.MaxValue;
                Vector3 bestVert = targetSpot;
                for (int v = 0; v < verts.Length; v++)
                {
                    if (verts[v].z >= -28f && verts[v].z <= -12f && Mathf.Abs(verts[v].x) <= 12f)
                    {
                        float d = Vector3.Distance(targetSpot, verts[v]);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestVert = verts[v];
                        }
                    }
                }
                if (bestDist < 40f)
                {
                    return bestVert;
                }
            }

            return targetSpot;
        }

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
                foreach (Vector3 spot in VillageDefenseSpots)
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
            // QUY ĐỊNH NGHIÊM NGẶT SỐ LƯỢNG NPC: Khi PrehistoricGameplayManager đang quản lý,
            // số lượng NPC luôn được thiết lập cố định chuẩn xác theo từng màn (6, 8, 10, ...),
            // không cho phép legacy AI tự động tiêu tài nguyên để sinh thêm quân tràn lan.
            if (LlamAcademy.Dinos.RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                return 0;
            }

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
                    count += pair.Value.Count(d => d != null && d.gameObject != null && d.gameObject.activeInHierarchy && d.Health > 0);
                }
                return count;
            }
        }

        /// <summary>
        /// Reset toàn bộ NPC chiến binh làng và sinh chính xác số lượng NPC theo quy định của màn.
        /// Xóa sạch NPC cũ và chia đều 50% Cung thủ (Archer), 50% Lính giáo (Spearman).
        /// </summary>
        public void ResetDefendersToExactCount(int targetCount)
        {
            // 1. Quét dọn triệt để 100% toàn bộ NPC chiến binh cũ trên Scene
            ClearAllDefenders(includePlayerPlaced: true);

            if (targetCount <= 0) return;

            // 2. Tìm prefab Defender đáng tin cậy nhất (ưu tiên Prefab_TribalWarrior trong Resources)
            Unit.Unit defenderPrefab = null;

            GameObject resObj = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_TribalWarrior") ?? Resources.Load<GameObject>("Prefabs/Prehistoric/Archer");
#if UNITY_EDITOR
            if (resObj == null)
            {
                resObj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LlamAcademy/Dinos/Enemy AI/Archer/Archer.prefab");
            }
#endif
            if (resObj != null)
            {
                defenderPrefab = resObj.GetComponent<Unit.Unit>();
            }

            if (defenderPrefab == null && ActiveConfig != null && ActiveConfig.Units != null && ActiveConfig.Units.Count > 0)
            {
                var valid = ActiveConfig.Units.FirstOrDefault(u => u.UnitSO != null && u.UnitSO.Prefab != null);
                if (valid.UnitSO != null) defenderPrefab = valid.UnitSO.Prefab;
            }

            if (defenderPrefab == null)
            {
                var warriorSO = Resources.Load<Config.TowerSO>("Config/Prehistoric/Tower_TribalWarrior");
#if UNITY_EDITOR
                if (warriorSO == null)
                {
                    warriorSO = UnityEditor.AssetDatabase.LoadAssetAtPath<Config.TowerSO>("Assets/Config/Prehistoric/Tower_TribalWarrior.asset");
                }
#endif
                if (warriorSO != null && warriorSO.Prefab != null)
                {
                    defenderPrefab = warriorSO.Prefab;
                }
            }

            if (defenderPrefab == null)
            {
                Debug.LogError("<color=red>[EnemyAIController]</color> Không tìm thấy Prefab cho Defender!");
                return;
            }

            // 3. Sinh đúng chính xác targetCount NPC tại các chốt phòng thủ trọng yếu trong sân làng
            for (int i = 0; i < targetCount; i++)
            {
                Vector3 postPos = GetValidDefenderSpawnPosition(i);

                // Luôn quay mặt về phía Bắc (Vector3.forward, Y = 0) hướng ra cổng làng
                Defender newDef = Instantiate(defenderPrefab, postPos, Quaternion.Euler(0, 0, 0)) as Defender;
                if (newDef != null)
                {
                    newDef.gameObject.SetActive(true);

                    // Đồng bộ an toàn NavMeshAgent lên NavMesh của làng
                    var agent = newDef.GetComponent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.enabled = false;
                        newDef.transform.position = postPos;
                        Physics.SyncTransforms();
                        agent.enabled = true;
                        if (NavMesh.SamplePosition(postPos, out NavMeshHit snapHit, 3.0f, NavMesh.AllAreas))
                        {
                            agent.Warp(snapHit.position);
                        }
                        else
                        {
                            agent.Warp(postPos);
                        }
                    }

                    // Cân đối 50% Archer (bắn cung), 50% Spearman (phóng giáo & cận chiến)
                    newDef.Role = (i % 2 == 0) ? DefenderRole.Archer : DefenderRole.Spearman;
                    newDef.MaxHealth = Mathf.Max(newDef.MaxHealth, 180);
                    newDef.Health = newDef.MaxHealth;
                    newDef.EnsureHealthBarAttached();
                    if (HealthBarCanvas.HasInstance)
                    {
                        HealthBarCanvas.Instance.CreateHealthBarForUnit(newDef);
                    }

                    UnitType uType = newDef.UnitType != null ? newDef.UnitType.Type : UnitType.Archer;
                    if (!Defenders.ContainsKey(uType))
                    {
                        Defenders[uType] = new HashSet<Defender>();
                    }
                    Defenders[uType].Add(newDef);
                    newDef.OnDeath += Defender_OnDeath;
                    OnSpawnDefender?.Invoke(newDef);

                    List<Vector3> myPatrol = new List<Vector3>
                    {
                        postPos,
                        postPos + new Vector3(0.8f, 0f, 0.3f),
                        postPos + new Vector3(-0.8f, 0f, -0.3f)
                    };
                    newDef.Patrol(myPatrol);
                    newDef.SetDestination(postPos);
                }
            }

            Debug.Log($"<color=green>[EnemyAIController]</color> Đã reset chính xác <b>{targetCount} NPC Chiến Binh</b> tại các vị trí phòng thủ trong làng!");
        }

        public void EnsureDefendersAlive(int minDefenders = 4)
        {
            if (ActiveConfig == null)
            {
                ActiveConfig = Resources.Load<AIStyleConfig>("Configs/Balanced") ?? Resources.Load<AIStyleConfig>("Balanced");
#if UNITY_EDITOR
                if (ActiveConfig == null)
                {
                    ActiveConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<AIStyleConfig>("Assets/LlamAcademy/Dinos/Enemy AI/Configs/Balanced.asset");
                }
#endif
            }

            // Dọn dẹp các mục null hoặc không còn active
            foreach (var pair in Defenders)
            {
                pair.Value.RemoveWhere(d => d == null || !d.gameObject.activeInHierarchy || d.Health <= 0);
            }

            EnsureValidWaypoints();

            int currentCount = TotalAliveDefenders;
            if (currentCount < minDefenders)
            {
                int needed = minDefenders - currentCount;
                Unit.Unit fallbackPrefab = null;
                GameObject resObj = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_TribalWarrior") ?? Resources.Load<GameObject>("Prefabs/Prehistoric/Archer");
#if UNITY_EDITOR
                if (resObj == null)
                {
                    resObj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LlamAcademy/Dinos/Enemy AI/Archer/Archer.prefab");
                }
#endif
                if (resObj != null) fallbackPrefab = resObj.GetComponent<Unit.Unit>();

                for (int i = 0; i < needed; i++)
                {
                    Unit.Unit prefabToUse = fallbackPrefab;
                    UnitType uType = UnitType.Archer;

                    if (ActiveConfig != null && ActiveConfig.Units != null && ActiveConfig.Units.Count > 0)
                    {
                        var unitConfig = ActiveConfig.Units[Random.Range(0, ActiveConfig.Units.Count)];
                        if (unitConfig.UnitSO != null && unitConfig.UnitSO.Prefab != null)
                        {
                            prefabToUse = unitConfig.UnitSO.Prefab;
                            uType = unitConfig.UnitSO.Type;
                        }
                    }

                    if (prefabToUse == null) continue;

                    Vector3 postPos = GetValidDefenderSpawnPosition(currentCount + i);

                    Defender defender = Instantiate(
                        prefabToUse,
                        postPos,
                        Quaternion.Euler(0, 0, 0)
                    ) as Defender;

                    if (defender != null)
                    {
                        defender.gameObject.SetActive(true);

                        // Đồng bộ an toàn NavMeshAgent lên NavMesh của làng
                        var agent = defender.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null)
                        {
                            agent.enabled = false;
                            defender.transform.position = postPos;
                            Physics.SyncTransforms();
                            agent.enabled = true;
                            if (NavMesh.SamplePosition(postPos, out NavMeshHit snapHit, 3.0f, NavMesh.AllAreas))
                            {
                                agent.Warp(snapHit.position);
                            }
                            else
                            {
                                agent.Warp(postPos);
                            }
                        }

                        defender.Role = ((currentCount + i) % 2 == 0) ? DefenderRole.Archer : DefenderRole.Spearman;
                        defender.MaxHealth = Mathf.Max(defender.MaxHealth, 180);
                        defender.Health = defender.MaxHealth;
                        defender.EnsureHealthBarAttached();
                        if (HealthBarCanvas.HasInstance)
                        {
                            HealthBarCanvas.Instance.CreateHealthBarForUnit(defender);
                        }

                        if (!Defenders.ContainsKey(uType))
                        {
                            Defenders[uType] = new HashSet<Defender>();
                        }
                        Defenders[uType].Add(defender);
                        defender.OnDeath += Defender_OnDeath;
                        OnSpawnDefender?.Invoke(defender);

                        List<Vector3> myPatrol = new List<Vector3>
                        {
                            postPos,
                            postPos + new Vector3(0.8f, 0f, 0.3f),
                            postPos + new Vector3(-0.8f, 0f, -0.3f)
                        };
                        defender.Patrol(myPatrol);
                        defender.SetDestination(postPos);
                    }
                }
            }
        }

        public void RegisterDefender(Defender defender)
        {
            if (defender == null) return;
            if (defender.UnitType != null)
            {
                if (!Defenders.ContainsKey(defender.UnitType.Type))
                {
                    Defenders[defender.UnitType.Type] = new HashSet<Defender>();
                }
                Defenders[defender.UnitType.Type].Add(defender);
            }
            defender.OnDeath += Defender_OnDeath;
            OnSpawnDefender?.Invoke(defender);
        }

        public void ClearAllDefenders(bool includePlayerPlaced = true)
        {
            var allSceneDefenders = FindObjectsByType<Defender>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in allSceneDefenders)
            {
                if (d != null && d.gameObject != null)
                {
                    bool isPlayerPlaced = d.GetComponent<PlayerPlacedTowerMarker>() != null;
                    if (!includePlayerPlaced && isPlayerPlaced)
                    {
                        // Giữ lại NPC do người chơi bỏ vàng ra đặt: hồi 100% máu
                        d.gameObject.SetActive(true);
                        d.Health = d.MaxHealth;
                        d.EnsureHealthBarAttached();
                        continue;
                    }

                    if (HealthBarCanvas.HasInstance)
                    {
                        HealthBarCanvas.Instance.Unregister(d);
                    }
                    d.gameObject.SetActive(false);
                    Destroy(d.gameObject);
                }
            }

            foreach (var pair in Defenders)
            {
                pair.Value.Clear();
            }

            if (includePlayerPlaced)
            {
                Defender.AllDefenders.Clear();
            }
            else
            {
                Defender.AllDefenders.RemoveAll(d => d == null || !d.gameObject.activeInHierarchy);
            }

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.ClearAliveDefenders();
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
