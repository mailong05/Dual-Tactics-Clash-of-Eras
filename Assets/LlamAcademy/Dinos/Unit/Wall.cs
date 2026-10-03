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

                HealthBarCanvas.Instance.Register(HealthBar, this);
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

        public override void Die()
        {
            UpdateNavMeshData.MoveObstacles();
            StartCoroutine(HandleDeath());
        }

        private IEnumerator HandleDeath()
        {
            yield return UpdateNavMeshData.Rebake();
            // destruction here would be cool
            Root.gameObject.SetActive(false);
            NavMeshManager.Instance.NavMeshUpdatedByObstacles();
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
