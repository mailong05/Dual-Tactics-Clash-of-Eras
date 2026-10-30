using System;
using System.Collections.Generic;
using LlamAcademy.Dinos.Unit;
using UnityEngine;

namespace LlamAcademy.Dinos.AI
{
    public enum DinoAISpeciesType
    {
        VelociraptorFlanker,
        AnkylosaurusTanker,
        PterodactylFlyer,
        TRexApexBoss
    }

    /// <summary>
    /// Node in the 2D Tactical Threat Grid
    /// </summary>
    public class ThreatGridNode
    {
        public int GridX;
        public int GridZ;
        public Vector3 WorldPos;

        public float ThreatScore;     // Accumulated threat from player towers' DPS and ranges
        public float WallPenalty;     // Penalty if a Wall / Barricade sits on this cell
        public bool HasWall;
        public bool IsWalkable = true;
        public Wall WallRef;

        // A* runtime variables
        public float GCost;
        public float HCost;
        public float FCost => GCost + HCost;
        public ThreatGridNode Parent;

        public ThreatGridNode(int x, int z, Vector3 worldPos)
        {
            GridX = x;
            GridZ = z;
            WorldPos = worldPos;
        }

        public void ResetAStar()
        {
            GCost = float.MaxValue;
            HCost = 0f;
            Parent = null;
        }
    }

    /// <summary>
    /// Threat-Aware A* Pathfinding & Threat Field Grid Matrix.
    /// Provides mathematically sound, multi-agent threat-avoiding and siege-breaching routes.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class ThreatAwarePathfinding : MonoBehaviour
    {
        private static ThreatAwarePathfinding _instance;
        public static ThreatAwarePathfinding Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ThreatAwarePathfinding>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ThreatAwarePathfinding_Manager");
                        _instance = go.AddComponent<ThreatAwarePathfinding>();
                        go.AddComponent<ThreatHeatmapVisualizer>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("=== Grid Configuration ===")]
        public int GridWidth = 64;
        public int GridHeight = 64;
        public float CellSize = 1.4f;
        public Vector3 GridOrigin = new Vector3(-44.8f, 0f, -44.8f);

        private ThreatGridNode[,] _grid;
        private bool _isGridInitialized = false;

        public event Action OnThreatFieldUpdated;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            InitializeGrid();
        }

        private void Start()
        {
            RecalculateThreatField();
        }

        public void InitializeGrid()
        {
            if (_grid != null && _isGridInitialized) return;

            _grid = new ThreatGridNode[GridWidth, GridHeight];
            for (int x = 0; x < GridWidth; x++)
            {
                for (int z = 0; z < GridHeight; z++)
                {
                    Vector3 worldPos = new Vector3(
                        GridOrigin.x + (x + 0.5f) * CellSize,
                        0f,
                        GridOrigin.z + (z + 0.5f) * CellSize
                    );
                    var node = new ThreatGridNode(x, z, worldPos);
                    // Khảo sát khả dụng trên NavMesh để loại trừ vách núi đá / vực sâu
                    bool onNav = UnityEngine.AI.NavMesh.SamplePosition(worldPos, out UnityEngine.AI.NavMeshHit hit, CellSize * 0.75f, UnityEngine.AI.NavMesh.AllAreas);
                    node.IsWalkable = onNav;
                    _grid[x, z] = node;
                }
            }
            _isGridInitialized = true;
        }

        /// <summary>
        /// Recalculates the Threat Field Matrix across all cells based on active towers and walls.
        /// Called when towers or walls are built, upgraded, or destroyed.
        /// </summary>
        public void RecalculateThreatField()
        {
            if (_grid == null || !_isGridInitialized) InitializeGrid();
            if (_grid == null) return;

            // 1. Reset all cells
            for (int x = 0; x < GridWidth; x++)
            {
                for (int z = 0; z < GridHeight; z++)
                {
                    var node = _grid[x, z];
                    if (node == null) continue;
                    node.ThreatScore = 0f;
                    node.WallPenalty = 0f;
                    node.HasWall = false;
                    node.WallRef = null;
                    bool onNav = UnityEngine.AI.NavMesh.SamplePosition(node.WorldPos, out UnityEngine.AI.NavMeshHit hit, CellSize * 0.75f, UnityEngine.AI.NavMesh.AllAreas);
                    node.IsWalkable = onNav;
                }
            }

            // 2. Scan active Towers and accumulate DPS threats
            var allUnits = FindObjectsByType<Unit.Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var unit in allUnits)
            {
                if (unit == null || !unit.gameObject.activeInHierarchy || unit.Health <= 0) continue;
                if (unit is PrehistoricDinoBase || unit is Dino || unit is FlyingUnit || unit is PrehistoricVillageBase) continue;

                // Check if it's a defensive tower or turret
                float attackRange = 16f;
                float dps = 40f;

                string unitName = unit.gameObject.name.ToLower();
                if (unit is ArcherTower || unitName.Contains("archer"))
                {
                    attackRange = 30f;
                    dps = 45f;
                }
                else if (unit is BallistaTower || unitName.Contains("ballista"))
                {
                    attackRange = 38f;
                    dps = 65f;
                }
                else if (unit is CatapultTower || unitName.Contains("catapult"))
                {
                    attackRange = 40f;
                    dps = 60f;
                }
                else if (unit is TeslaTower || unitName.Contains("tesla"))
                {
                    attackRange = 18f;
                    dps = 55f;
                }
                else if (unit is FrostTower || unitName.Contains("frost"))
                {
                    attackRange = 20f;
                    dps = 35f;
                }
                else if (unitName.Contains("watchtower") || unitName.Contains("tower"))
                {
                    attackRange = 28f;
                    dps = 40f;
                }
                else
                {
                    // Regular defender NPC
                    attackRange = 18f;
                    dps = 25f;
                }

                Vector3 towerPos = unit.transform.position;
                float sqrRange = attackRange * attackRange;

                int minX = Mathf.Clamp(Mathf.FloorToInt((towerPos.x - attackRange - GridOrigin.x) / CellSize), 0, GridWidth - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt((towerPos.x + attackRange - GridOrigin.x) / CellSize), 0, GridWidth - 1);
                int minZ = Mathf.Clamp(Mathf.FloorToInt((towerPos.z - attackRange - GridOrigin.z) / CellSize), 0, GridHeight - 1);
                int maxZ = Mathf.Clamp(Mathf.CeilToInt((towerPos.z + attackRange - GridOrigin.z) / CellSize), 0, GridHeight - 1);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        var node = _grid[x, z];
                        float dist = Vector3.Distance(new Vector3(towerPos.x, 0, towerPos.z), new Vector3(node.WorldPos.x, 0, node.WorldPos.z));
                        if (dist <= attackRange)
                        {
                            // Threat decreases gracefully with distance
                            float cellThreat = dps / (1.0f + dist * 0.15f);
                            node.ThreatScore += cellThreat;
                        }
                    }
                }
            }

            // 3. Scan active Walls and Barricades
            var allWalls = FindObjectsByType<Wall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var wall in allWalls)
            {
                if (wall == null || !wall.gameObject.activeInHierarchy || wall.Health <= 0) continue;
                string wn = wall.gameObject.name.ToLower();
                // River bridges are walkable pathways, not blocking wall obstacles
                if (wn.Contains("bridge") || wn.Contains("ramp")) continue;

                Collider col = wall.GetComponentInChildren<Collider>();
                Bounds b = col != null ? col.bounds : new Bounds(wall.transform.position, Vector3.one * 3f);

                int minX = Mathf.Clamp(Mathf.FloorToInt((b.min.x - GridOrigin.x) / CellSize), 0, GridWidth - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt((b.max.x - GridOrigin.x) / CellSize), 0, GridWidth - 1);
                int minZ = Mathf.Clamp(Mathf.FloorToInt((b.min.z - GridOrigin.z) / CellSize), 0, GridHeight - 1);
                int maxZ = Mathf.Clamp(Mathf.CeilToInt((b.max.z - GridOrigin.z) / CellSize), 0, GridHeight - 1);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        var node = _grid[x, z];
                        node.HasWall = true;
                        node.WallRef = wall;
                        node.WallPenalty = Mathf.Clamp(wall.Health * 0.12f, 25f, 350f);
                    }
                }
            }

            OnThreatFieldUpdated?.Invoke();
        }

        public ThreatGridNode GetNodeFromWorldPos(Vector3 worldPos)
        {
            if (_grid == null || !_isGridInitialized) InitializeGrid();
            if (_grid == null) return null;
            int x = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - GridOrigin.x) / CellSize), 0, GridWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((worldPos.z - GridOrigin.z) / CellSize), 0, GridHeight - 1);
            return _grid[x, z];
        }

        public ThreatGridNode GetNode(int x, int z)
        {
            if (_grid == null || !_isGridInitialized) InitializeGrid();
            if (_grid == null) return null;
            if (x < 0 || x >= GridWidth || z < 0 || z >= GridHeight) return null;
            return _grid[x, z];
        }

        /// <summary>
        /// Improved Threat-Aware A* Pathfinding.
        /// Evaluates: f(n) = g(n) + h(n)
        /// g(v) = g(u) + dist(u, v) * (1 + w_threat * Threat(v)) + w_wall * WallPenalty(v)
        /// </summary>
        public List<Vector3> FindThreatAwarePath(Vector3 startPos, Vector3 targetPos, DinoAISpeciesType species)
        {
            if (_grid == null || !_isGridInitialized) InitializeGrid();
            if (_grid == null) return new List<Vector3> { targetPos };

            // Species-specific behavioral weights:
            // - Velociraptor (Flanker): wThreat = 4.0 (strictly avoids tower fire, seeks blind spots), wWall = 3.0
            // - Ankylosaurus (Tanker): wThreat = 0.25 (ignores tower fire), wWall = 0.15 (smashes through walls)
            // - Pterodactyl (Flyer): wThreat = 1.0, wWall = 0 (flies over walls)
            // - TRex (Apex Boss): wThreat = 0.45, wWall = 0.2
            float wThreat = 3.5f;
            float wWall = 2.0f;

            switch (species)
            {
                case DinoAISpeciesType.VelociraptorFlanker:
                    wThreat = 4.5f;
                    wWall = 3.2f;
                    break;
                case DinoAISpeciesType.AnkylosaurusTanker:
                    wThreat = 0.25f;
                    wWall = 0.15f;
                    break;
                case DinoAISpeciesType.PterodactylFlyer:
                    wThreat = 1.1f;
                    wWall = 0.0f;
                    break;
                case DinoAISpeciesType.TRexApexBoss:
                    wThreat = 0.45f;
                    wWall = 0.22f;
                    break;
            }

            ThreatGridNode startNode = GetNodeFromWorldPos(startPos);
            ThreatGridNode targetNode = GetNodeFromWorldPos(targetPos);

            if (startNode == null || targetNode == null) return new List<Vector3> { targetPos };

            // Reset A* variables across the grid
            for (int x = 0; x < GridWidth; x++)
            {
                for (int z = 0; z < GridHeight; z++)
                {
                    _grid[x, z].ResetAStar();
                }
            }

            var openSet = new List<ThreatGridNode>();
            var closedSet = new HashSet<ThreatGridNode>();

            startNode.GCost = 0f;
            startNode.HCost = Vector3.Distance(startNode.WorldPos, targetNode.WorldPos);
            openSet.Add(startNode);

            int iterations = 0;
            const int maxIterations = 2500;

            ThreatGridNode closestReachedNode = startNode;
            float closestDist = float.MaxValue;

            while (openSet.Count > 0 && iterations++ < maxIterations)
            {
                // Find node with lowest FCost in openSet
                ThreatGridNode currentNode = openSet[0];
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].FCost < currentNode.FCost ||
                       (Mathf.Approximately(openSet[i].FCost, currentNode.FCost) && openSet[i].HCost < currentNode.HCost))
                    {
                        currentNode = openSet[i];
                    }
                }

                openSet.Remove(currentNode);
                closedSet.Add(currentNode);

                float distToTarget = Vector3.Distance(currentNode.WorldPos, targetNode.WorldPos);
                if (distToTarget < closestDist)
                {
                    closestDist = distToTarget;
                    closestReachedNode = currentNode;
                }

                // Check goal reached (within 1 cell radius)
                if (currentNode == targetNode || distToTarget <= CellSize * 1.5f)
                {
                    return RetracePath(startNode, currentNode);
                }

                // Expand neighbors (8 directions)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;

                        int nx = currentNode.GridX + dx;
                        int nz = currentNode.GridZ + dz;

                        ThreatGridNode neighbor = GetNode(nx, nz);
                        if (neighbor == null || closedSet.Contains(neighbor)) continue;
                        // Vách đá, núi cao ngoài rìa NavMesh và không phải tường phòng thủ -> Không đi xuyên qua
                        if (!neighbor.IsWalkable && !neighbor.HasWall) continue;

                        // Step distance: 1.0 for orthogonal, 1.414 for diagonal
                        float stepDist = (dx != 0 && dz != 0) ? 1.414f * CellSize : CellSize;

                        // Cost calculation with threat and wall penalty
                        float threatFactor = 1.0f + wThreat * (neighbor.ThreatScore * 0.05f);
                        float wallPenalty = wWall * neighbor.WallPenalty;

                        float tentativeGCost = currentNode.GCost + stepDist * threatFactor + wallPenalty;

                        if (tentativeGCost < neighbor.GCost)
                        {
                            neighbor.Parent = currentNode;
                            neighbor.GCost = tentativeGCost;
                            neighbor.HCost = Vector3.Distance(neighbor.WorldPos, targetNode.WorldPos);

                            if (!openSet.Contains(neighbor))
                            {
                                openSet.Add(neighbor);
                            }
                        }
                    }
                }
            }

            // Fallback to closest reached node if complete path not found
            return RetracePath(startNode, closestReachedNode);
        }

        private List<Vector3> RetracePath(ThreatGridNode startNode, ThreatGridNode endNode)
        {
            List<Vector3> path = new List<Vector3>();
            ThreatGridNode curr = endNode;

            while (curr != null && curr != startNode)
            {
                path.Add(curr.WorldPos);
                curr = curr.Parent;
            }

            path.Reverse();

            // Smooth path by skipping collinear or intermediate points
            return SimplifyPath(path);
        }

        private List<Vector3> SimplifyPath(List<Vector3> rawPath)
        {
            if (rawPath.Count <= 2) return rawPath;

            List<Vector3> simplified = new List<Vector3>();
            simplified.Add(rawPath[0]);

            for (int i = 1; i < rawPath.Count - 1; i += 2)
            {
                simplified.Add(rawPath[i]);
            }

            simplified.Add(rawPath[rawPath.Count - 1]);
            return simplified;
        }

        /// <summary>
        /// Retrieves highest threat value on the entire grid (used for normalizing heatmap colors)
        /// </summary>
        public float GetMaxThreatScore()
        {
            if (_grid == null || !_isGridInitialized) return 1f;
            float max = 0.01f;
            for (int x = 0; x < GridWidth; x++)
            {
                for (int z = 0; z < GridHeight; z++)
                {
                    if (_grid[x, z].ThreatScore > max) max = _grid[x, z].ThreatScore;
                }
            }
            return max;
        }
    }
}
