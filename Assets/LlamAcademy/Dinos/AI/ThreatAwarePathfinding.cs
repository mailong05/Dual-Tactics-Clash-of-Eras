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
    /// Biến trạng thái hành vi chiến thuật của khủng long do AI Director tính toán một lần trước đợt đánh:
    /// - FrontalAssault: Tấn công trực diện / Đột kích thẳng trung lộ (khi trung lộ không có hỏa lực tháp)
    /// - FlankerEvasion: Bọc sườn né tháp qua các góc mù hỏa lực (khi hỏa lực đầu làng quá mạnh)
    /// - SwarmRush: Tấn công dồn bầy đàn vào điểm yếu (lấy tốc độ làm chủ đạo)
    /// - MassSwarmOverwhelm: Dùng số lượng áp đảo toàn diện (biển quái tràn qua cổng)
    /// - SiegeBreaker: Đột phá tường thành / Phá hủy chướng ngại vật (dành cho Ankylosaurus Tanker)
    /// </summary>
    public enum DinoTacticalBehavior
    {
        FrontalAssault,       // Tấn công trực diện / Đột kích thẳng trung lộ
        FlankerEvasion,       // Bọc sườn né tháp qua các góc mù hỏa lực
        SwarmRush,           // Tấn công dồn bầy đàn vào điểm yếu
        MassSwarmOverwhelm,  // Dùng số lượng áp đảo (biển quái)
        SiegeBreaker         // Đột phá tường thành / Phá hủy chướng ngại vật
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
        private static bool s_isApplicationQuitting = false;

        public static bool HasInstance => _instance != null && !s_isApplicationQuitting;

        public static ThreatAwarePathfinding Instance
        {
            get
            {
                if (s_isApplicationQuitting) return null;
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ThreatAwarePathfinding>();
                    if (_instance == null && !s_isApplicationQuitting)
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

        public static ThreatAwarePathfinding EnsureInstance()
        {
            return Instance;
        }

        private void OnApplicationQuit()
        {
            s_isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        [Header("=== Grid Configuration ===")]
        public int GridWidth = 64;
        public int GridHeight = 64;
        public float CellSize = 1.4f;
        public Vector3 GridOrigin = new Vector3(-44.8f, 0f, -44.8f);

        private ThreatGridNode[,] _grid;
        private bool _isGridInitialized = false;

        public event Action OnThreatFieldUpdated;

        [Header("=== AI Director Pre-Calculated Tactics ===")]
        public DinoTacticalBehavior CurrentWaveTacticalStrategy = DinoTacticalBehavior.SwarmRush;
        public string StrategyDescription = "Sẵn sàng phân tích ma trận phòng thủ";
        public bool HasAnalyzedThisWave { get; private set; } = false;

        private bool _cachedIsCenterHeavy = false;
        private float _cachedCenterThreat = 0f;
        private int _cachedTowersCoveringCenter = 0;
        private List<Vector3> _cachedFlankerPath = new();
        private List<Vector3> _cachedTankerPath = new();
        private List<Vector3> _cachedDirectRushPath = new();

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
                    bool onNav = UnityEngine.AI.NavMesh.SamplePosition(worldPos, out UnityEngine.AI.NavMeshHit hit, CellSize * 0.75f, UnityEngine.AI.NavMesh.AllAreas);
                    if (onNav)
                    {
                        worldPos.y = hit.position.y;
                    }
                    // Chỉ loại bỏ các điểm ngoài vách núi dốc đứng thực sự (X > 25.5f hoặc X < -26.5f), bảo đảm mở thông hoàn toàn hành lang bọc sườn hai cánh
                    if (worldPos.x > 25.5f && worldPos.z >= -12.0f && worldPos.z <= 4.0f)
                    {
                        onNav = false;
                    }
                    else if (worldPos.x < -26.5f && worldPos.z >= -12.0f && worldPos.z <= 4.0f)
                    {
                        onNav = false;
                    }
                    var node = new ThreatGridNode(x, z, worldPos);
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
                    if (onNav)
                    {
                        node.WorldPos.y = hit.position.y;
                    }
                    if (node.WorldPos.x > 25.5f && node.WorldPos.z >= -12.0f && node.WorldPos.z <= 4.0f)
                    {
                        onNav = false;
                    }
                    else if (node.WorldPos.x < -26.5f && node.WorldPos.z >= -12.0f && node.WorldPos.z <= 4.0f)
                    {
                        onNav = false;
                    }
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
                    attackRange = 15f;
                    dps = 45f;
                }
                else if (unit is BallistaTower || unitName.Contains("ballista"))
                {
                    attackRange = 18f;
                    dps = 65f;
                }
                else if (unit is CatapultTower || unitName.Contains("catapult"))
                {
                    attackRange = 20f;
                    dps = 60f;
                }
                else if (unit is TeslaTower || unitName.Contains("tesla"))
                {
                    attackRange = 13f;
                    dps = 55f;
                }
                else if (unit is FrostTower || unitName.Contains("frost"))
                {
                    attackRange = 14f;
                    dps = 35f;
                }
                else if (unitName.Contains("watchtower") || unitName.Contains("tower"))
                {
                    attackRange = 15f;
                    dps = 40f;
                }
                else
                {
                    // Regular defender NPC
                    attackRange = 12f;
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
            bool isAssaultMode = RoundManagement.PrehistoricGameplayManager.IsAssaultMode;
            foreach (var wall in allWalls)
            {
                if (wall == null || !wall.gameObject.activeInHierarchy || wall.Health <= 0) continue;
                string wn = wall.gameObject.name.ToLower();
                // River bridges and walkways are walkable pathways, not blocking wall obstacles
                if (wn.Contains("bridge") || wn.Contains("ramp") || wn.Contains("walkway") || wn.Contains("deck")) continue;

                // In Tower Defense mode, scenic village structures should not add wall penalties unless placed by player
                if (!isAssaultMode && !wall.gameObject.name.Contains("(Clone)"))
                {
                    if (wn.Contains("arch") || wn.Contains("hut") || wn.Contains("tent") || wn.Contains("tower") || wn.Contains("watchtower") || wn.Contains("platform")) continue;
                }

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

        /// <summary>
        /// THEO YÊU CẦU NGƯỜI DÙNG: Kiểm tra xem khu vực Đầu Làng / Trung Lộ (X: [-14, 14], Z: [-22, 8])
        /// có bố trí hỏa lực tháp mạnh hay không.
        /// Sử dụng giá trị đã phân tích và lưu cache trong 5s đếm ngược chiến thuật,
        /// TRIỆT TIÊU 100% FindObjectsByType trong vòng lặp Update frame-by-frame!
        /// </summary>
        public bool IsCenterEntranceHeavyThreat()
        {
            return _cachedIsCenterHeavy;
        }

        /// <summary>
        /// PHÂN TÍCH MA TRẬN PHÒNG THỦ CỦA NGƯỜI CHƠI DUY NHẤT 1 LẦN TRONG 5S ĐẾM NGƯỢC (Theo yêu cầu người dùng).
        /// - Tính toán hỏa lực tháp, điểm mù, mật độ tháp ở đầu làng.
        /// - Thiết lập biến hành vi thích ứng chuẩn xác cho từng cách bố trí của người chơi:
        ///   + 0 tháp: Tấn Công Trực Diện (FrontalAssault)
        ///   + 1 tháp: Xung Phong Bầy Đàn (SwarmRush)
        ///   + >= 2 tháp hỏa lực mạnh đầu làng: Bọc Sườn Né Tháp (FlankerEvasion)
        ///   + >= 3 rào chắn chặn đường: Phá Tường Mở Đường (SiegeBreaker)
        ///   + Đợt >= 5: Biển Quái Áp Đảo (MassSwarmOverwhelm)
        /// - Tính toán trước các lộ trình di chuyển (Pre-computed waypoints) an toàn, tuyệt đối không đi lạc vào rừng sâu.
        /// </summary>
        public void AnalyzeDefenseLayoutOnce(Vector3 spawnPos, Vector3 basePos)
        {
            // 1. Tái tính toán ma trận Threat Field một lần duy nhất
            RecalculateThreatField();

            // 2. Quét đánh giá cụm hỏa lực ở khu vực đầu làng / cửa ngõ trung lộ
            var allUnits = FindObjectsByType<Unit.Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _cachedCenterThreat = 0f;
            _cachedTowersCoveringCenter = 0;
            int frontBarricades = 0;
            Vector3 centerEntrance = new Vector3(0f, 0f, -8f);

            foreach (var unit in allUnits)
            {
                if (unit == null || !unit.gameObject.activeInHierarchy || unit.Health <= 0) continue;
                if (unit is PrehistoricDinoBase || unit is Dino || unit is FlyingUnit || unit is PrehistoricVillageBase) continue;
                if (unit.gameObject.name.Contains("HealthBar") || unit.gameObject.name.Contains("Canvas")) continue;

                Vector3 pos = unit.transform.position;
                float distToCenter = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(centerEntrance.x, centerEntrance.z));

                string un = unit.gameObject.name.ToLower();

                // Các kiến trúc cảnh quan của ngôi làng (cầu sông, đường dốc, cổng vòm, lều bộ lạc, chòi canh) 
                // TUYỆT ĐỐI không bao giờ bị nhận diện nhầm là rào chắn cản đường
                bool isScenicStructure = un.Contains("bridge") || un.Contains("ramp") || un.Contains("arch") || 
                                         un.Contains("hut") || un.Contains("tent") || un.Contains("platform") || 
                                         un.Contains("watchtower") || un.Contains("walkway") || un.Contains("deck");

                // Phân loại tháp tấn công tầm xa thực sự
                bool isOffensiveTower = (unit is ArcherTower || unit is BallistaTower || unit is CatapultTower || unit is TeslaTower || unit is FrostTower);
                if (!isOffensiveTower)
                {
                    isOffensiveTower = (un.Contains("tower") || un.Contains("ballista") || un.Contains("catapult") || un.Contains("totem"))
                                    && !un.Contains("barricade") && !un.Contains("wall") && !un.Contains("trap")
                                    && !isScenicStructure;
                }

                // Phân loại rào chắn / tường cản đường:
                bool isAssault = RoundManagement.PrehistoricGameplayManager.IsAssaultMode;
                bool isBarricade = false;
                if (!isScenicStructure)
                {
                    if (isAssault)
                    {
                        // Chế độ Công Thành: Hàng rào gỗ bao quanh làng của đối phương
                        isBarricade = (unit is Wall) && (un.Contains("barricade") || un.Contains("palisade") || un.Contains("wall") || un.Contains("plank") || un.Contains("fence"));
                    }
                    else
                    {
                        // Chế độ Thủ Thành: TUYỆT ĐỐI CHỈ tính rào chắn/tường do chính người chơi đặt trong trận (các bản Clone)
                        isBarricade = (unit is Wall || un.Contains("barricade") || un.Contains("cọc gỗ")) && unit.gameObject.name.Contains("(Clone)");
                    }
                }

                if (distToCenter <= 18.0f)
                {
                    if (isOffensiveTower)
                    {
                        _cachedTowersCoveringCenter++;
                        if (unit is CatapultTower || unit.gameObject.name.Contains("Catapult")) _cachedCenterThreat += 35f;
                        else if (unit is BallistaTower || unit.gameObject.name.Contains("Ballista")) _cachedCenterThreat += 30f;
                        else if (unit is ArcherTower || unit.gameObject.name.Contains("Archer")) _cachedCenterThreat += 25f;
                        else if (unit is TeslaTower || unit.gameObject.name.Contains("Totem")) _cachedCenterThreat += 25f;
                        else _cachedCenterThreat += 20f;
                    }
                    else if (isBarricade)
                    {
                        frontBarricades++;
                    }
                }
            }

            _cachedIsCenterHeavy = (_cachedTowersCoveringCenter >= 2 && _cachedCenterThreat >= 45.0f);

            int currentWave = (RoundManagement.PrehistoricGameplayManager.Instance != null) 
                ? RoundManagement.PrehistoricGameplayManager.Instance.CurrentWave 
                : 1;

            // 3. Thiết lập biến hành vi thích ứng chính xác cho từng trường hợp bố trí:
            if (_cachedIsCenterHeavy)
            {
                // TRƯỜNG HỢP 1: Cửa ngõ tập trung cụm hỏa lực mạnh (từ 2 tháp trở lên, DPS >= 45) -> Bọc sườn 2 cánh né hỏa lực
                CurrentWaveTacticalStrategy = DinoTacticalBehavior.FlankerEvasion;
                StrategyDescription = $"🐆 BỌC SƯỜN NÉ THÁP (Cửa ngõ {_cachedTowersCoveringCenter} tháp, {_cachedCenterThreat:F0} DPS -> Bọc sườn 2 cánh né hỏa lực)";
            }
            else if (frontBarricades >= 3)
            {
                // TRƯỜNG HỢP 2: Người chơi xây nhiều rào chắn / mê cung chặn lối đi (từ 3 rào trở lên)
                CurrentWaveTacticalStrategy = DinoTacticalBehavior.SiegeBreaker;
                StrategyDescription = $"🛡️ PHÁ TƯỜNG MỞ ĐƯỜNG (Phát hiện {frontBarricades} rào chắn chặn lối -> Tanker phá rào dọn đường)";
            }
            else if (currentWave >= 5)
            {
                // TRƯỜNG HỢP 3: Các đợt cao (Đợt 5 trở đi) -> Tổng lực bầy đàn đa chủng loại áp đảo
                CurrentWaveTacticalStrategy = DinoTacticalBehavior.MassSwarmOverwhelm;
                StrategyDescription = $"🌊 BIỂN QUÁI ÁP ĐẢO (Đợt {currentWave}: Lấy số lượng quân áp đảo toàn bộ trung lộ)";
            }
            else if (_cachedTowersCoveringCenter == 1)
            {
                // TRƯỜNG HỢP 4: Hỏa lực mỏng (chỉ có đúng 1 tháp ở cửa ngõ)
                CurrentWaveTacticalStrategy = DinoTacticalBehavior.SwarmRush;
                StrategyDescription = "⚡ XUNG PHONG BẦY ĐÀN (Hỏa lực mỏng 1 tháp -> Bầy đàn tăng tốc đột phá xuyên cửa ngõ)";
            }
            else
            {
                // TRƯỜNG HỢP 5: Trung lộ hoàn toàn trống (không có bất kỳ tháp nào ở cửa ngõ)
                CurrentWaveTacticalStrategy = DinoTacticalBehavior.FrontalAssault;
                StrategyDescription = "⚔️ TẤN CÔNG TRỰC DIỆN (Trung lộ trống -> Đột kích thần tốc thẳng vào nhà chính)";
            }

            // 4. Lập trước toàn bộ lộ trình Waypoints MỘT LẦN DUY NHẤT:
            _cachedFlankerPath = FindThreatAwarePathInternal(spawnPos, basePos, DinoAISpeciesType.VelociraptorFlanker);
            _cachedTankerPath = FindThreatAwarePathInternal(spawnPos, basePos, DinoAISpeciesType.AnkylosaurusTanker);

            _cachedDirectRushPath = new List<Vector3>
            {
                spawnPos,
                new Vector3(spawnPos.x * 0.4f, 0f, 12f),
                new Vector3(-2.2f, 0f, 0f),
                new Vector3(-2.2f, 0f, -14f),
                new Vector3(-2.23f, 0f, -28f),
                basePos
            };

            HasAnalyzedThisWave = true;
            Debug.Log($"<color=cyan>[ThreatAwarePathfinding]</color> Hoàn tất AI 5s: {StrategyDescription} | Hỏa lực: {_cachedCenterThreat:F1} | Tháp đầu làng: {_cachedTowersCoveringCenter} | Rào: {frontBarricades}");
            OnThreatFieldUpdated?.Invoke();
        }

        public DinoTacticalBehavior GetAssignedTacticalBehavior(DinoAISpeciesType species)
        {
            switch (species)
            {
                case DinoAISpeciesType.VelociraptorFlanker:
                    return CurrentWaveTacticalStrategy;
                case DinoAISpeciesType.AnkylosaurusTanker:
                    return DinoTacticalBehavior.SiegeBreaker;
                case DinoAISpeciesType.TRexApexBoss:
                    return (_cachedIsCenterHeavy && UnityEngine.Random.value < 0.35f) 
                        ? DinoTacticalBehavior.FlankerEvasion 
                        : DinoTacticalBehavior.FrontalAssault;
                case DinoAISpeciesType.PterodactylFlyer:
                    return DinoTacticalBehavior.FrontalAssault;
                default:
                    return CurrentWaveTacticalStrategy;
            }
        }

        public List<Vector3> GetPrecomputedWaypoints(DinoAISpeciesType species)
        {
            if (species == DinoAISpeciesType.VelociraptorFlanker)
            {
                if (CurrentWaveTacticalStrategy == DinoTacticalBehavior.FlankerEvasion && _cachedFlankerPath != null && _cachedFlankerPath.Count > 0)
                {
                    return new List<Vector3>(_cachedFlankerPath);
                }
                if (_cachedDirectRushPath != null && _cachedDirectRushPath.Count > 0)
                {
                    return new List<Vector3>(_cachedDirectRushPath);
                }
            }
            else if (species == DinoAISpeciesType.AnkylosaurusTanker)
            {
                if (_cachedTankerPath != null && _cachedTankerPath.Count > 0)
                {
                    return new List<Vector3>(_cachedTankerPath);
                }
            }

            if (_cachedDirectRushPath != null && _cachedDirectRushPath.Count > 0)
            {
                return new List<Vector3>(_cachedDirectRushPath);
            }

            Vector3 defaultBase = new Vector3(-2.23f, 0f, -38.26f);
            return new List<Vector3> { defaultBase };
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
            return FindThreatAwarePathInternal(startPos, targetPos, species);
        }

        private List<Vector3> FindThreatAwarePathInternal(Vector3 startPos, Vector3 targetPos, DinoAISpeciesType species)
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
                    bool isCenterHeavy = IsCenterEntranceHeavyThreat();
                    if (isCenterHeavy)
                    {
                        // HỎA LỰC MẠNH Ở ĐẦU LÀNG: Velociraptor dùng chiến thuật BỌC SƯỜN NÉ THÁP (Flanker Evasion)
                        wThreat = 6.5f;
                        wWall = 25f;
                    }
                    else
                    {
                        // HỎA LỰC YẾU HOẶC KHÔNG CÓ THÁP: Đổi thành XUNG PHONG BẦY ĐÀN (Swarm Rush), xông thẳng trung lộ!
                        wThreat = 0.5f;
                        wWall = 1.2f;
                    }
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
                // Find node with lowest FCost in openSet (sử dụng tie-breaker nhất quán để triệt tiêu hiện tượng jitter giật lắc đường đi)
                ThreatGridNode currentNode = openSet[0];
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].FCost < currentNode.FCost)
                    {
                        currentNode = openSet[i];
                    }
                    else if (Mathf.Approximately(openSet[i].FCost, currentNode.FCost))
                    {
                        if (openSet[i].HCost < currentNode.HCost)
                        {
                            currentNode = openSet[i];
                        }
                        else if (Mathf.Approximately(openSet[i].HCost, currentNode.HCost))
                        {
                            // Deterministic tie-breaker cố định theo chỉ số GridX & GridZ để không bị nhảy qua lại giữa 2 nhánh
                            if (openSet[i].GridX < currentNode.GridX || (openSet[i].GridX == currentNode.GridX && openSet[i].GridZ < currentNode.GridZ))
                            {
                                currentNode = openSet[i];
                            }
                        }
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
                        // TUYỆT ĐỐI không bao giờ bước lùi ngược lên rừng sâu phía Bắc
                        if (neighbor.WorldPos.z > startPos.z + 1.5f) continue;
                        // Vách đá, núi cao ngoài rìa NavMesh và không phải tường phòng thủ -> Không đi xuyên qua
                        if (!neighbor.IsWalkable && !neighbor.HasWall) continue;

                        // Step distance: 1.0 for orthogonal, 1.414 for diagonal
                        float stepDist = (dx != 0 && dz != 0) ? 1.414f * CellSize : CellSize;

                        // Cost calculation with threat and wall penalty
                        float threatFactor = 1.0f + wThreat * (neighbor.ThreatScore * 0.05f);
                        float wallPenalty = wWall * neighbor.WallPenalty;
                        float flankBonusPenalty = 0f;

                        // CHIẾN THUẬT THÍCH ỨNG CHO VELOCIRAPTOR:
                        if (species == DinoAISpeciesType.VelociraptorFlanker)
                        {
                            bool isCenterHeavy = IsCenterEntranceHeavyThreat();
                            if (isCenterHeavy)
                            {
                                bool isLeftFlank = startPos.x < 0f;

                                // Khi còn ở ngoài sông (Z > 2.0f), ưu tiên né tim đường (X: -4.5..4.5) để tránh hỏa lực tập trung
                                if (neighbor.WorldPos.z > 2.0f && Mathf.Abs(neighbor.WorldPos.x) < 4.5f)
                                {
                                    flankBonusPenalty += 25f; // Nudge nhẹ để né tim đường, tuyệt đối không làm đứt đường A*
                                }

                                // Khuyến khích di chuyển men theo hành lang sườn tương ứng (X: -12..-5 hoặc X: 5..12)
                                if (isLeftFlank && neighbor.WorldPos.x >= -12.0f && neighbor.WorldPos.x <= -5.0f)
                                {
                                    threatFactor *= 0.3f;
                                }
                                else if (!isLeftFlank && neighbor.WorldPos.x >= 5.0f && neighbor.WorldPos.x <= 12.0f)
                                {
                                    threatFactor *= 0.3f;
                                }
                            }
                            else
                            {
                                // Hỏa lực mỏng hoặc trung lộ trống: Khuyến khích chạy thẳng trục trung lộ
                                if (Mathf.Abs(neighbor.WorldPos.x) <= 6.0f)
                                {
                                    threatFactor *= 0.4f;
                                }
                            }
                        }

                        float tentativeGCost = currentNode.GCost + stepDist * threatFactor + wallPenalty + flankBonusPenalty;

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

            // Fallback an toàn: Nếu A* không chạm được mục tiêu (ví dụ bị vật cản bịt kín), lập tức dùng hành lang an toàn tiến về làng
            if (closestDist > CellSize * 3.5f)
            {
                return GetGuaranteedCorridorPath(startPos, targetPos, species);
            }

            return RetracePath(startNode, closestReachedNode);
        }

        public List<Vector3> GetGuaranteedCorridorPath(Vector3 startPos, Vector3 targetPos, DinoAISpeciesType species)
        {
            bool isCenterHeavy = IsCenterEntranceHeavyThreat();
            if (species == DinoAISpeciesType.VelociraptorFlanker && isCenterHeavy)
            {
                float flankX = (startPos.x < 0f) ? -8.0f : 8.0f;
                return new List<Vector3>
                {
                    startPos,
                    new Vector3(flankX, 0f, 15f),
                    new Vector3(flankX * 0.7f, 0f, 2f),
                    new Vector3(-2.2f, 0f, -8f),
                    new Vector3(-2.2f, 0f, -22f),
                    targetPos
                };
            }
            return new List<Vector3>
            {
                startPos,
                new Vector3(startPos.x * 0.5f, 0f, 12f),
                new Vector3(-2.2f, 0f, 0f),
                new Vector3(-2.2f, 0f, -14f),
                new Vector3(-2.23f, 0f, -28f),
                targetPos
            };
        }

        private List<Vector3> RetracePath(ThreatGridNode startNode, ThreatGridNode endNode)
        {
            List<Vector3> path = new List<Vector3>();
            ThreatGridNode curr = endNode;

            while (curr != null && curr != startNode)
            {
                // Chỉ lấy các điểm không vượt quá độ cao phía Bắc của điểm xuất phát
                if (curr.WorldPos.z <= startNode.WorldPos.z + 1.0f)
                {
                    Vector3 pt = curr.WorldPos;
                    pt.x = Mathf.Clamp(pt.x, -22f, 22f);
                    path.Add(pt);
                }
                curr = curr.Parent;
            }

            path.Reverse();

            // Smooth path by skipping collinear or intermediate points
            List<Vector3> simplified = SimplifyPath(path);
            if (simplified.Count == 0 || Vector3.Distance(simplified[simplified.Count - 1], endNode.WorldPos) > 2.0f)
            {
                simplified.Add(endNode.WorldPos);
            }
            return simplified;
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
