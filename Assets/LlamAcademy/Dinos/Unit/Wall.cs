using System.Collections;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Utility;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Unit
{
    public class Wall : Unit
    {
        [field: SerializeField] public GameObject Root { get; set; }
        [field: SerializeField] public float HealthToCostMultiplier { get; private set; } = 0.2f;
        [SerializeField] private NavMeshUpdateData UpdateNavMeshData;

        private float ObstacleSpawnHeight;

        protected override void Awake()
        {
            base.Awake();

            if (Root == null)
            {
                Root = gameObject;
            }

            Rigidbody = null;

            ObstacleSpawnHeight = (UpdateNavMeshData.Obstacles != null && UpdateNavMeshData.Obstacles.Length > 0 && UpdateNavMeshData.Obstacles[0] != null)
                ? UpdateNavMeshData.Obstacles[0].transform.position.y
                : 0;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (HealthBar != null && HealthBar.gameObject != null)
            {
                Destroy(HealthBar.gameObject);
            }

            UpdateNavMeshData.Destroy();
            if (AI.ThreatAwarePathfinding.HasInstance)
            {
                AI.ThreatAwarePathfinding.Instance.RecalculateThreatField();
            }
        }

        private void OnEnable()
        {
            if (HealthBar != null)
            {
                HealthBar.gameObject.SetActive(true);

                if (HealthBarCanvas.Instance != null)
                {
                    HealthBarCanvas.Instance.Register(HealthBar, this);
                }
            }

            UpdateNavMeshData.RemoveNavMeshData();
            UpdateNavMeshData.RestoreOriginalObstaclePosition(ObstacleSpawnHeight);
        }

        protected override void OnTargetEnter(IDamageable target) {}

        protected override void OnTargetExit(IDamageable target) {}

        public void Repair(int amount)
        {
            if (Health == 0 && amount > 0)
            {
                gameObject.SetActive(true);
            }
            Health += amount;
            if (Health > MaxHealth)
            {
                Debug.LogWarning($"Health attempted to exceed max health by {MaxHealth - Health}. Clamped to Max Health. This probably means there is a math error elsewhere.");
                Health = MaxHealth;
            }

            if (HealthBar != null)
            {
                HealthBar.SetProgress((float)Health / MaxHealth);
            }
        }

        public void FullRestore()
        {
            if (Root != null)
            {
                Root.SetActive(true);
            }
            gameObject.SetActive(true);

            // Kích hoạt lại toàn bộ các GameObject con cháu (bảo đảm cọc gỗ đứng và khiên tròn đều active)
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                if (child != null) child.gameObject.SetActive(true);
            }

            if (MaxHealth <= 0 || MaxHealth == 450 || MaxHealth == 250) MaxHealth = 900;
            Health = MaxHealth;

            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c != null) c.enabled = true;
            }

            Renderer[] rends = GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r != null) r.enabled = true;
            }

            NavMeshObstacle[] obstacles = GetComponentsInChildren<NavMeshObstacle>(true);
            foreach (var obs in obstacles)
            {
                if (obs != null)
                {
                    obs.enabled = true;
                    obs.carving = true;
                }
            }

            UpdateNavMeshData.RestoreOriginalObstaclePosition(ObstacleSpawnHeight);
            EnsureHealthBarAttached();
            if (HealthBar != null)
            {
                HealthBar.SetProgress(1.0f);
            }
        }

        private static bool s_IsWideningBreach = false;

        private static void WidenBreachOpening(Vector3 breachPos, float collapseRadius)
        {
            if (s_IsWideningBreach) return;
            s_IsWideningBreach = true;
            try
            {
                // 1. Phá hủy thêm đoạn tường gối đầu kế bên trong phạm vi 2.8m để mở toang lối đi rộng ~5.0m
                Collider[] hits = Physics.OverlapSphere(breachPos, collapseRadius);
                foreach (var hit in hits)
                {
                    if (hit == null) continue;
                    Wall adjacentWall = hit.GetComponentInParent<Wall>();
                    if (adjacentWall != null && adjacentWall.gameObject != null && adjacentWall.Health > 0)
                    {
                        float dist = Vector3.Distance(adjacentWall.transform.position, breachPos);
                        if (dist > 0.4f && dist <= collapseRadius)
                        {
                            adjacentWall.Die();
                            break; // 1 đoạn kế bên sụp là đã mở toang 5m+ cực kỳ rộng rãi!
                        }
                    }
                }

                // 2. Trim bớt BoxCollider và NavMeshObstacle của các tường đứng 2 bên mép (từ 2.8m đến 4.8m)
                // để không cấn vào lối đi của khủng long to lớn như T-Rex và Ankylosaurus
                Collider[] edgeHits = Physics.OverlapSphere(breachPos, 4.8f);
                foreach (var hit in edgeHits)
                {
                    if (hit == null) continue;
                    Wall edgeWall = hit.GetComponentInParent<Wall>();
                    if (edgeWall != null && edgeWall.Health > 0)
                    {
                        if (edgeWall.TryGetComponent(out BoxCollider bc))
                        {
                            Vector3 sz = bc.size;
                            sz.x = Mathf.Max(sz.x * 0.80f, 1.2f);
                            bc.size = sz;
                        }
                        if (edgeWall.TryGetComponent(out NavMeshObstacle nmo))
                        {
                            Vector3 sz = nmo.size;
                            sz.x = Mathf.Max(sz.x * 0.80f, 1.2f);
                            nmo.size = sz;
                        }
                    }
                }
            }
            finally
            {
                s_IsWideningBreach = false;
            }
        }

        public override void Die()
        {
            // 0. Xử lý các chiến binh NPC đang đứng trên công trình này sụp đổ và hy sinh
            EliminateDefendersOnCollapsingStructure();

            // 1. Lập tức vô hiệu hóa Collider, NavMeshObstacle và Renderer ngay trong frame này
            // để Unity NavMesh lập tức un-carve giải phóng lối đi và vật lý không chặn quái
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) if (c != null) c.enabled = false;
            NavMeshObstacle[] obstacles = GetComponentsInChildren<NavMeshObstacle>(true);
            foreach (var obs in obstacles) if (obs != null) obs.enabled = false;
            Renderer[] rends = GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends) if (r != null) r.enabled = false;

            // 1b. Nếu công trình bị phá hủy là cầu gỗ/dốc/cổng, lập tức làm biến mất toàn bộ mặt sàn cầu
            // (Smooth Floor, Walkway_Deck, ván gỗ plank) xung quanh để không để lại sàn ma trôi nổi
            DeactivateAssociatedBridgeFloors();

            // 2. Tự động sụp đổ đoạn tường liền kề để tạo khoảng trống đột phá rộng 5m thênh thang
            WidenBreachOpening(transform.position, 2.8f);

            UpdateNavMeshData.MoveObstacles();

            // 3. Đăng ký vị trí khe hở vào TacticalBreachMemory để điều phối quái và quân cố thủ
            Vector3 basePos = new Vector3(-2.23f, 0f, -38.26f);
            if (PrehistoricVillageBase.Instance != null)
            {
                basePos = PrehistoricVillageBase.Instance.transform.position;
            }
            else if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.BaseTarget != null)
            {
                basePos = PrehistoricGameplayManager.Instance.BaseTarget.position;
            }
            else if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.VillageBase != null)
            {
                basePos = PrehistoricGameModeManager.Instance.VillageBase.transform.position;
            }
            else if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
            {
                basePos = RoundManager.Instance.DinoTarget.position;
            }

            Vector3 toBase = (basePos - transform.position);
            toBase.y = 0;
            if (toBase.sqrMagnitude > 0.01f) toBase.Normalize();
            else toBase = new Vector3(0, 0, -1f);

            // Bảo đảm toBase luôn hướng về phía nam (Z âm - vào sâu trong làng)
            if (toBase.z > -0.1f)
            {
                toBase = new Vector3(toBase.x, 0, -1f).normalized;
            }

            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.RegisterBreach(transform.position, toBase, 5.0f);
            }

            AI.ThreatAwarePathfinding.Instance?.RecalculateThreatField();

            PrehistoricDinoBase.OnWallBreached(this);
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(HandleDeath());
            }
            else
            {
                if (Root != null)
                {
                    Root.gameObject.SetActive(false);
                }
                else
                {
                    gameObject.SetActive(false);
                }

                if (NavMeshManager.Instance != null)
                {
                    NavMeshManager.Instance.NavMeshUpdatedByObstacles();
                }
            }
        }

                
        /// <summary>
        /// Tự động dò tìm và vô hiệu hóa toàn bộ mặt sàn gỗ (Smooth Floor, Walkway_Deck, ván gỗ)
        /// liên kết với cầu/dốc khi cầu bị khủng long phá hủy, xóa bỏ hoàn toàn hiện tượng sàn ma trôi nổi.
        /// </summary>
        private void DeactivateAssociatedBridgeFloors()
        {
            string n = gameObject.name.ToLower();
            bool isBridge = n.Contains("bridge") || n.Contains("arch") || n.Contains("ramp") || n.Contains("gate");
            if (!isBridge) return;

            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t == null || t == transform || (Root != null && t == Root.transform)) continue;
                string tn = t.gameObject.name;
                bool isFloor = tn.Contains("Smooth Floor") || 
                               tn.Contains("Walkway_Deck") || 
                               tn.Contains("Collider_Walkway_Deck") || 
                               tn.Contains("FloorDeck") ||
                               tn.Contains("Plank_Defenses");

                if (isFloor)
                {
                    float dist = Vector3.Distance(new Vector3(t.position.x, 0, t.position.z), new Vector3(transform.position.x, 0, transform.position.z));
                    if (dist < 35f)
                    {
                        Renderer[] rList = t.GetComponentsInChildren<Renderer>(true);
                        foreach (var r in rList) if (r != null) r.enabled = false;
                        Collider[] cList = t.GetComponentsInChildren<Collider>(true);
                        foreach (var c in cList) if (c != null) c.enabled = false;
                        t.gameObject.SetActive(false);
                    }
                }
            }
        }

private void EliminateDefendersOnCollapsingStructure()
        {
            Bounds b = new Bounds(transform.position, Vector3.one * 2f);
            Collider mainCol = GetComponent<Collider>();
            if (mainCol != null) b = mainCol.bounds;

            float minHeight = transform.position.y + 0.6f;
            float horizontalRadius = Mathf.Max(b.extents.x, b.extents.z, 2.0f) + 0.8f;

            var defenders = Enemy.Defender.AllDefenders;
            if (defenders == null || defenders.Count == 0) return;

            System.Collections.Generic.List<Enemy.Defender> toEliminate = null;

            for (int i = 0; i < defenders.Count; i++)
            {
                var d = defenders[i];
                if (d == null || d.Health <= 0 || !d.gameObject.activeInHierarchy) continue;

                Vector3 defPos = d.transform.position;
                bool standingOnThis = false;

                // 1. Bắn raycast xuống dưới từ vị trí NPC kiểm tra có trúng collider công trình này không
                if (Physics.Raycast(defPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2.5f))
                {
                    if (hit.collider != null && (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform || (Root != null && hit.collider.transform.IsChildOf(Root.transform))))
                    {
                        standingOnThis = true;
                    }
                }

                // 2. Dự phòng: Kiểm tra vị trí đứng trên cao trong phạm vi ngang của công trình
                if (!standingOnThis && defPos.y >= minHeight)
                {
                    Vector3 diff = defPos - transform.position;
                    diff.y = 0;
                    if (diff.magnitude <= horizontalRadius)
                    {
                        standingOnThis = true;
                    }
                }

                if (standingOnThis)
                {
                    if (toEliminate == null) toEliminate = new System.Collections.Generic.List<Enemy.Defender>();
                    toEliminate.Add(d);
                }
            }

            if (toEliminate != null)
            {
                for (int i = 0; i < toEliminate.Count; i++)
                {
                    var def = toEliminate[i];
                    if (def != null && def.Health > 0)
                    {
                        Debug.Log($"<color=orange>[Structure Collapse]</color> NPC {def.name} hy sinh khi công trình {gameObject.name} bị phá hủy!");
                        def.TakeDamage(def.Health);
                        def.Die();
                    }
                }
            }
        }

private IEnumerator HandleDeath()
        {
            yield return UpdateNavMeshData.Rebake();
            // destruction here would be cool
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }

            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.NavMeshUpdatedByObstacles();
            }
            AI.ThreatAwarePathfinding.Instance?.RecalculateThreatField();
            PrehistoricDinoBase.OnWallBreached(this);
        }

        [System.Serializable]
        private struct NavMeshUpdateData
        {
            [field: SerializeField] public NavMeshObstacle[] Obstacles { get; private set; }
            [field: SerializeField] public float DeathObstacleHeight { get; private set; }
            [field: SerializeField] public NavMeshSurface[] RebakeSurfacesOnDeath { get; private set; }
            [field: SerializeField] public GameObject[] EnableObjectsBeforeRebake { get; private set; }
            [field: SerializeField] public MonoBehaviour[] EnableComponentsBeforeRebake { get; private set; }

            /// <summary>
            /// Bake all <see cref="RebakeSurfacesOnDeath"/> after enabling all <see cref="EnableObjectsBeforeRebake"/> and <see cref="EnableComponentsBeforeRebake"/>
            /// </summary>
            public IEnumerator Rebake()
            {
                if (EnableObjectsBeforeRebake != null)
                {
                    foreach (GameObject gameObject in EnableObjectsBeforeRebake)
                    {
                        if (gameObject != null)
                        {
                            gameObject.SetActive(true);
                            gameObject.transform.SetParent(null, true);
                        }
                    }
                }

                if (EnableComponentsBeforeRebake != null)
                {
                    foreach (MonoBehaviour behaviour in EnableComponentsBeforeRebake)
                    {
                        if (behaviour != null)
                        {
                            behaviour.enabled = true;
                        }
                    }
                }

                yield return null;

                // Runtime NavMeshObstacle carving handles passage opening automatically.
                // Rebuilding surface at runtime during combat throws 'Failed to create agent' in Unity.
                yield return null;
            }

            /// <summary>
            /// Remove all NavMeshData from all <see cref="RebakeSurfacesOnDeath"/> and disable all <see cref="EnableObjectsBeforeRebake"/> and <see cref="EnableComponentsBeforeRebake"/>
            /// </summary>
            public void RemoveNavMeshData()
            {
                // Do not remove surface data here as it wipes out the whole level's NavMesh for agents.

                if (EnableObjectsBeforeRebake != null)
                {
                    foreach (GameObject gameObject in EnableObjectsBeforeRebake)
                    {
                        if (gameObject != null)
                        {
                            gameObject.SetActive(false);
                        }
                    }
                }

                if (EnableComponentsBeforeRebake != null)
                {
                    foreach (MonoBehaviour behaviour in EnableComponentsBeforeRebake)
                    {
                        if (behaviour != null)
                        {
                            behaviour.enabled = false;
                        }
                    }
                }
            }

            /// <summary>
            /// Shift all obstacles by the <see cref="DeathObstacleHeight"/>
            /// </summary>
            public void MoveObstacles()
            {
                if (Obstacles == null) return;

                foreach (NavMeshObstacle obstacle in Obstacles)
                {
                    if (obstacle != null)
                    {
                        Vector3 originalPosition = obstacle.transform.position;
                        obstacle.transform.position = new Vector3(originalPosition.x, DeathObstacleHeight, originalPosition.z);
                    }
                }
            }

            /// <summary>
            /// Restores all obstacles to the specified height.
            /// Note this only supports Y shifting obstacles.
            /// </summary>
            /// <param name="spawnHeight"></param>
            public void RestoreOriginalObstaclePosition(float spawnHeight)
            {
                if (Obstacles == null) return;

                foreach (NavMeshObstacle obstacle in Obstacles)
                {
                    if (obstacle != null)
                    {
                        Vector3 originalPosition = obstacle.transform.position;
                        obstacle.transform.position = new Vector3(originalPosition.x, spawnHeight, originalPosition.z);
                        obstacle.transform.SetParent(null, true);
                    }
                }
            }

            /// <summary>
            /// Removes all baked NavMeshData and destroys all related obstacles
            /// </summary>
            public void Destroy()
            {
                if (Obstacles != null)
                {
                    foreach (NavMeshObstacle obstacle in Obstacles)
                    {
                        if (obstacle != null)
                        {
                            Object.Destroy(obstacle.gameObject);
                        }
                    }
                }

                // Do not remove surface data on obstacle destroy.
            }
        }

    }
}
