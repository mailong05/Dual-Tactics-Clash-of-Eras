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
        [SerializeField] private GameObject Root;
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
            // 1. Lập tức vô hiệu hóa Collider và NavMeshObstacle ngay trong frame này
            // để Unity NavMesh lập tức un-carve giải phóng lối đi và vật lý không chặn quái
            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (var c in cols) if (c != null) c.enabled = false;
            NavMeshObstacle[] obstacles = GetComponentsInChildren<NavMeshObstacle>();
            foreach (var obs in obstacles) if (obs != null) obs.enabled = false;

            // 2. Tự động sụp đổ đoạn tường liền kề để tạo khoảng trống đột phá rộng 5m thênh thang
            WidenBreachOpening(transform.position, 2.8f);

            UpdateNavMeshData.MoveObstacles();

            // 3. Đăng ký vị trí khe hở vào TacticalBreachMemory để điều phối quái và quân cố thủ
            Vector3 toBase = Vector3.forward;
            if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
            {
                toBase = (RoundManager.Instance.DinoTarget.position - transform.position).normalized;
            }
            else if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.VillageBase != null)
            {
                toBase = (PrehistoricGameModeManager.Instance.VillageBase.transform.position - transform.position).normalized;
            }
            toBase.y = 0;
            if (toBase == Vector3.zero) toBase = transform.forward;
            LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.RegisterBreach(transform.position, toBase, 5.0f);

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

                if (RebakeSurfacesOnDeath != null)
                {
                    foreach (NavMeshSurface surface in RebakeSurfacesOnDeath)
                    {
                        if (surface != null)
                        {
                            surface.BuildNavMesh();
                        }
                    }
                }
            }

            /// <summary>
            /// Remove all NavMeshData from all <see cref="RebakeSurfacesOnDeath"/> and disable all <see cref="EnableObjectsBeforeRebake"/> and <see cref="EnableComponentsBeforeRebake"/>
            /// </summary>
            public void RemoveNavMeshData()
            {
                if (RebakeSurfacesOnDeath != null)
                {
                    foreach (NavMeshSurface surface in RebakeSurfacesOnDeath)
                    {
                        if (surface != null)
                        {
                            surface.RemoveData();
                        }
                    }
                }

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

                if (RebakeSurfacesOnDeath != null)
                {
                    foreach (NavMeshSurface surface in RebakeSurfacesOnDeath)
                    {
                        if (surface != null)
                        {
                            surface.RemoveData();
                        }
                    }
                }
            }
        }

    }
}
