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
            EnemySurface.BuildNavMesh();
            EnemyTriangulation = NavMesh.CalculateTriangulation();
            SetVisualizationFromTriangulation();

            foreach (NavMeshSurface surface in DinoSurfaces)
            {
                surface.BuildNavMesh();
            }
        }

        /// <summary>
        /// Recalculates the NavMeshTriangulation for the enemy surface, rebaking all known surfaces.
        /// </summary>
        /// <param name="rebuildEnemySurface"></param>
        public void RecalculateTriangulation(bool rebuildEnemySurface = false)
        {
            if (rebuildEnemySurface)
            {
                // Tạm thời vô hiệu hóa tất cả NavMeshAgent đang active trong Scene để tránh Unity ném lỗi "Failed to create agent because it is not close enough to the NavMesh"
                NavMeshAgent[] allAgents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                System.Collections.Generic.List<NavMeshAgent> activeAgents = new System.Collections.Generic.List<NavMeshAgent>();
                foreach (var a in allAgents)
                {
                    if (a != null && a.enabled && a.gameObject != null && a.gameObject.activeInHierarchy)
                    {
                        a.enabled = false;
                        activeAgents.Add(a);
                    }
                }

                try
                {
                    if (EnemySurface != null)
                    {
                        EnemySurface.BuildNavMesh();
                    }

                    if (DinoSurfaces != null)
                    {
                        foreach (NavMeshSurface surface in DinoSurfaces)
                        {
                            if (surface != null)
                            {
                                surface.BuildNavMesh();
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[NavMeshManager] Note on rebuilding surfaces: {ex.Message}");
                }
                finally
                {
                    foreach (var a in activeAgents)
                    {
                        if (a != null && a.gameObject != null && a.gameObject.activeInHierarchy)
                        {
                            if (NavMesh.SamplePosition(a.transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                            {
                                a.Warp(hit.position);
                            }
                            a.enabled = true;
                        }
                    }
                }
            }
            else
            {
                // Tuyệt đối KHÔNG gọi NavMesh.RemoveAllNavMeshData() vì sẽ làm mất NavMesh của agent và quăng exception.
                // Runtime carving của NavMeshObstacle tự động cập nhật vào NavMesh.
                // Chỉ nạp NavMeshData nếu bề mặt chưa được add vào NavMesh:
                if (EnemySurface != null)
                {
                    EnemySurface.AddData();
                }
                if (DinoSurfaces != null)
                {
                    foreach (NavMeshSurface surface in DinoSurfaces)
                    {
                        if (surface != null)
                        {
                            surface.AddData();
                        }
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
