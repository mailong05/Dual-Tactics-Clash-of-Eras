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
            }

            Instance = this;

            NavMesh.RemoveAllNavMeshData();
            EnemySurface.BuildNavMesh();
            EnemyTriangulation = NavMesh.CalculateTriangulation();
            NavMeshFilter = NavMeshRenderer.GetComponent<MeshFilter>();
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
            NavMesh.RemoveAllNavMeshData();
            if (rebuildEnemySurface)
            {
                EnemySurface.BuildNavMesh();
            }
            else
            {
                NavMesh.AddNavMeshData(EnemySurface.navMeshData);
            }
            EnemyTriangulation = NavMesh.CalculateTriangulation();
            SetVisualizationFromTriangulation();
            foreach (NavMeshSurface surface in DinoSurfaces)
            {
                NavMesh.AddNavMeshData(surface.navMeshData);
            }

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
            NavMeshFilter.mesh = new Mesh();
            NavMeshFilter.mesh.SetVertices(EnemyTriangulation.vertices);
            NavMeshFilter.mesh.SetIndices(EnemyTriangulation.indices, MeshTopology.Triangles, 0);
        }
    }
}
