using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.RoundManagement
{
    [DefaultExecutionOrder(10)]
    public class NavMeshManager : MonoBehaviour
    {
        [SerializeField] private NavMeshSurface EnemySurface;
        [SerializeField] private NavMeshSurface[] DinoSurfaces;

        [SerializeField] private bool ShowDebugMesh;

        [System.NonSerialized] public NavMeshTriangulation EnemyTriangulation;

        [SerializeField] private MeshRenderer NavMeshRenderer;
        private MeshFilter NavMeshFilter;

        public delegate void NavMeshUpdatedEvent();

        public event NavMeshUpdatedEvent OnNavMeshUpdated;

        public static NavMeshManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"Multiple NavMeshManagers in the scene. Destroying second one {name}.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ShowDebugMesh = false;

            // Triệt tiêu vĩnh viễn mesh xanh gỡ lỗi (NavMeshVisualization) trong toàn bộ scene
            if (NavMeshRenderer != null)
            {
                NavMeshRenderer.enabled = false;
                if (NavMeshRenderer.gameObject != null)
                {
                    NavMeshRenderer.gameObject.SetActive(false);
                    Destroy(NavMeshRenderer.gameObject);
                }
                NavMeshRenderer = null;
            }

            foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (r != null && r.gameObject != null && r.gameObject.name.Contains("NavMeshVisualization"))
                {
                    r.enabled = false;
                    r.gameObject.SetActive(false);
                    Destroy(r.gameObject);
                }
            }

            NavMesh.RemoveAllNavMeshData();

            // Giới hạn volume baking của EnemySurface strictly ở Y >= 0.05m:
            // Bao phủ toàn bộ mặt đất làng (Y = 0.10m), cầu thang ramp và chòi cao (Y = 4.15m),
            // TRIỆT ĐỂ LOẠI BỎ 100% mọi vật thể ngầm ở Y = -3.35m để KHÔNG BAO GIỜ sinh ra NavMesh dưới lòng đất!
            if (EnemySurface != null)
            {
                EnemySurface.collectObjects = CollectObjects.Volume;
                EnemySurface.center = new Vector3(0.5f, 4.5f, -18.0f);
                EnemySurface.size = new Vector3(75.0f, 8.9f, 45.0f);
            }

            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.EnsureWatchtowersAndRampsWalkable();
            }

            EnemySurface.BuildNavMesh();
            EnemyTriangulation = NavMesh.CalculateTriangulation();
            SetVisualizationFromTriangulation();

            foreach (NavMeshSurface surface in DinoSurfaces)
            {
                if (surface != null)
                {
                    surface.collectObjects = CollectObjects.Volume;
                    surface.center = new Vector3(0.5f, 4.5f, -18.0f);
                    surface.size = new Vector3(75.0f, 8.9f, 45.0f);
                    surface.BuildNavMesh();
                }
            }
        }

        /// <summary>
        /// Recalculates the NavMeshTriangulation for the enemy surface, rebaking all known surfaces.
        /// </summary>
        /// <param name="rebuildEnemySurface"></param>
        public void RecalculateTriangulation(bool rebuildEnemySurface = false)
        {
            NavMesh.RemoveAllNavMeshData();
            if (rebuildEnemySurface)
            {
                if (EnemySurface != null)
                {
                    EnemySurface.collectObjects = CollectObjects.Volume;
                    EnemySurface.center = new Vector3(0.5f, 4.5f, -18.0f);
                    EnemySurface.size = new Vector3(75.0f, 8.9f, 45.0f);
                }

                if (PrehistoricGameplayManager.Instance != null)
                {
                    PrehistoricGameplayManager.Instance.EnsureWatchtowersAndRampsWalkable();
                }

                EnemySurface.BuildNavMesh();

                foreach (NavMeshSurface surface in DinoSurfaces)
                {
                    if (surface != null)
                    {
                        surface.collectObjects = CollectObjects.Volume;
                        surface.center = new Vector3(0.5f, 4.5f, -18.0f);
                        surface.size = new Vector3(75.0f, 8.9f, 45.0f);
                        surface.BuildNavMesh();
                    }
                }
            }
            else
            {
                NavMesh.AddNavMeshData(EnemySurface.navMeshData);
                foreach (NavMeshSurface surface in DinoSurfaces)
                {
                    if (surface != null && surface.navMeshData != null)
                    {
                        NavMesh.AddNavMeshData(surface.navMeshData);
                    }
                }
            }
            EnemyTriangulation = NavMesh.CalculateTriangulation();
            SetVisualizationFromTriangulation();

            OnNavMeshUpdated?.Invoke();
        }

        /// <summary>
        /// When the NavMesh has been updated by obstacles, call this to ensure any event listeners are notified.
        /// This does not recalculate triangulation or rebuild any surfaces.
        /// </summary>
        public void NavMeshUpdatedByObstacles()
        {
            OnNavMeshUpdated?.Invoke();
        }

        private void SetVisualizationFromTriangulation()
        {
            // Không bao giờ tạo hay hiển thị mesh xanh gỡ lỗi lên chiến trường
            if (NavMeshRenderer != null)
            {
                NavMeshRenderer.enabled = false;
                if (NavMeshRenderer.gameObject != null)
                {
                    NavMeshRenderer.gameObject.SetActive(false);
                    Destroy(NavMeshRenderer.gameObject);
                }
                NavMeshRenderer = null;
            }

            if (NavMeshFilter != null)
            {
                NavMeshFilter.mesh = null;
            }
        }
    }
}
