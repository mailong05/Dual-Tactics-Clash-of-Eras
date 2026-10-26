using System;
using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Enemy;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Unit;
using LlamAcademy.Dinos.Utility;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LlamAcademy.Dinos.RoundManagement
{
    public enum PrehistoricWavePhase
    {
        BuildingPhase,      // Người chơi tự do đặt tháp, chuẩn bị chiến lược
        CombatPhase,        // Khủng long tấn công, tháp phòng thủ
        WaveCleared,        // Đợt hoàn tất, nhận thưởng vàng
        GameOver,           // Ngôi làng bị phá hủy, thất thủ
        Victory             // Tiêu diệt Boss T-Rex ở Đợt 10, khải hoàn
    }

    [DefaultExecutionOrder(-5)]
    public class PrehistoricGameplayManager : MonoBehaviour
    {
        public static PrehistoricGameplayManager Instance { get; private set; }

        [Header("=== Village Base Defense Stats ===")]
        [SerializeField] private int _MaxBaseHealth = 1000;
        [SerializeField] private int _CurrentBaseHealth = 1000;
        public int MaxBaseHealth => _MaxBaseHealth;
        public int CurrentBaseHealth => _CurrentBaseHealth;
        public Transform BaseTarget { get; private set; }

        [Header("=== Wave Configuration ===")]
        [SerializeField] private int _CurrentWave = 1;
        [SerializeField] private int _MaxWaves = 10;
        [SerializeField] private PrehistoricWavePhase _CurrentPhase = PrehistoricWavePhase.BuildingPhase;
        public int CurrentWave => _CurrentWave;
        public int MaxWaves => _MaxWaves;
        public PrehistoricWavePhase CurrentPhase => _CurrentPhase;

        [Header("=== Spawn Points ===")]
        [SerializeField] private Transform[] SpawnPoints;

        [Header("=== Dinosaur Prefabs ===")]
        [SerializeField] private GameObject RaptorPrefab;
        [SerializeField] private GameObject PterodactylPrefab;
        [SerializeField] private GameObject AnkylosaurusPrefab;
        [SerializeField] private GameObject TRexBossPrefab;

        [Header("=== Economy & Rewards ===")]
        [SerializeField] private int StartingGold = 950;
        [SerializeField] private int WaveCompletionBaseGold = 150;

        [Header("=== State & Tracking ===")]
        [SerializeField] private int MonstersAliveCount = 0;
        [SerializeField] private int TotalMonstersInWave = 0;
        public int MonstersAlive => MonstersAliveCount;

        // Events
        public event Action<int, int> OnBaseHealthChanged; // current, max
        public event Action<int> OnWaveStarted;             // waveIndex
        public event Action<int, int> OnWaveCompleted;       // waveIndex, goldReward
        public event Action OnGameOver;
        public event Action OnVictory;
        public event Action<PrehistoricWavePhase> OnPhaseChanged;

        public List<Unit.Unit> ActiveMonsters = new();
        private int _MonstersRemainingToSpawn = 0;
        private Coroutine WaveSpawnRoutine;
        private BossDino ActiveBossInstance;
        private string ScreenAnnouncement = "";
        private float AnnouncementTimer = 0f;

        // Vị trí gốc của Camera và Player để reset hoàn toàn khi chơi lại
        private Vector3 _InitialCameraPos;
        private Quaternion _InitialCameraRot;
        private Vector3 _InitialPlayerPos;
        private Quaternion _InitialPlayerRot;
        private GameObject _PlayerObj;

        // Điều khiển thanh đặt tháp và cuộn ngang linh hoạt (PC & Mobile)
        private Vector2 _ToolbarScrollPosition = Vector2.zero;
        private bool _IsDraggingToolbar = false;
        private float _ToolbarDragStartMouseX = 0f;
        private float _ToolbarDragStartScrollX = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_MaxBaseHealth < 1000) _MaxBaseHealth = 1000;
            _CurrentBaseHealth = _MaxBaseHealth;

            CleanupInvisibleAndStrayObjects();
            DeactivateDefaultSceneBarricades();
            // Đảm bảo Smooth Floor không bị bất kỳ NavMeshModifier nào loại trừ khỏi bản đồ
            var allNavMods = FindObjectsByType<Unity.AI.Navigation.NavMeshModifier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mod in allNavMods)
            {
                if (mod != null && mod.gameObject.name.Contains("Smooth Floor"))
                {
                    mod.ignoreFromBuild = false;
                }
            }

            EnsureScenicFortressWalkways();
            EnsureVillageBridgesDestructible();
            ConfigureAllCustomBarricades();
            EnsureAudioManagerAndSettingsMenu();
            LocateBaseTarget();
            EnsureSpawnPoints();
            LoadDinoPrefabsIfNull();
        }

        public void CleanupInvisibleAndStrayObjects()
        {
            // Danh sách các vật thể lạc ngoài map hoặc rác thực sự (bao gồm 2 chòi canh thủ gỗ tàng hình)
            string[] invisibleOrStrayNames = new string[]
            {
                "Village_Left_Gap_Wall_128_neg152",
                "Village_Watchtower_Left",
                "Village_Watchtower_Right"
            };

            // Dọn dẹp triệt để bất kỳ component Wall nào vô tình bị gắn trên Canvas, UI hoặc HealthBar
            var allStrayWalls = FindObjectsByType<Unit.Wall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sw in allStrayWalls)
            {
                if (sw == null) continue;
                if (sw.GetComponent<RectTransform>() != null || sw.gameObject.layer == 5 ||
                    sw.name.Contains("HealthBar") || sw.name.Contains("Fill") || sw.name.Contains("UnitNameLabel"))
                {
                    Destroy(sw);
                }
            }

            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                // 1. Xóa các vật thể rác hoặc lỗi tọa độ ngoài bản đồ (kể cả 2 chòi canh tàng hình)
                foreach (string strayName in invisibleOrStrayNames)
                {
                    if (n.Equals(strayName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (t.TryGetComponent(out Unit.Unit u) && HealthBarCanvas.HasInstance)
                        {
                            HealthBarCanvas.Instance.Unregister(u);
                        }
                        Destroy(t.gameObject);
                        break;
                    }
                }

                // 2. Xóa các thanh cản hoặc tường lạc ở tọa độ bất thường ngoài bản đồ
                if (t != null && (t.position.x > 80f || t.position.z < -100f))
                {
                    if (n.Contains("Wall") || n.Contains("Barricade") || n.Contains("Gap"))
                    {
                        Destroy(t.gameObject);
                    }
                }

                // 2.1. Xóa triệt để các thanh máu tĩnh mồ côi được lưu sẵn trong Scene của các tường đã bị vô hiệu hóa
                if (n.StartsWith("HealthBar_Left_Wall_") || n.StartsWith("HealthBar_Right_Wall_") ||
                    n.StartsWith("HealthBar_Right_Flank_") || n.StartsWith("HealthBar_Gate_") ||
                    n.StartsWith("HealthBar_Village_Left_Gap_") || n.StartsWith("HealthBar_Village_Watchtower_"))
                {
                    Destroy(t.gameObject);
                    continue;
                }

                // 3. ĐẢM BẢO CÁC CHÒI GỖ (Big Tower) VÀ CỔNG VÒM (Arch Entry Wall) LUÔN HIỂN THỊ ĐẦY ĐỦ VÀ KHÔNG KẸT ĐƯỜNG
                if (t != null && (n.Contains("Big Tower") || n.Contains("Arch Entry")))
                {
                    // Bật lại toàn bộ MeshRenderer / SkinnedMeshRenderer nếu bị tắt
                    Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends)
                    {
                        if (r != null) r.enabled = true;
                    }

                    // Tắt carving trên NavMeshObstacle của chòi/vòm để không tạo lỗ thủng trên NavMesh gây kẹt khủng long
                    UnityEngine.AI.NavMeshObstacle[] obstacles = t.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
                    foreach (var obs in obstacles)
                    {
                        if (obs != null) obs.carving = false;
                    }
                }

                // 4. Đảm bảo Cầu Gỗ (Bridge) và Sàn Gỗ (Plank) tuyệt đối là đường đi thông suốt, KHÔNG bị NavMeshObstacle cản trở
                // CHÚ Ý QUAN TRỌNG: Tuyệt đối KHÔNG xóa nhầm hàng rào gỗ phòng thủ Plank_Defenses!
                if (t != null && !n.Contains("defenses") && (n.IndexOf("bridge", StringComparison.OrdinalIgnoreCase) >= 0 || (n.IndexOf("plank", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("defenses", StringComparison.OrdinalIgnoreCase) < 0)))
                {
                    // Bật hiển thị cầu
                    Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends)
                    {
                        if (r != null) r.enabled = true;
                    }

                    if (t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle bo)) Destroy(bo);
                }
            }

            // 5. Dọn sạch các thanh HealthBar rác không có cha bị lưu nhầm vào scene
            var allRootGOs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var go in allRootGOs)
            {
                if (go != null && go.name.StartsWith("HealthBar_"))
                {
                    Destroy(go);
                }
            }

            // 6. Xóa triệt để các thanh máu lơ lửng của 2 chòi canh gỗ tàng hình dưới HealthBarCanvas
            if (HealthBarCanvas.HasInstance)
            {
                var canvas = HealthBarCanvas.Instance;
                for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                {
                    Transform c = canvas.transform.GetChild(i);
                    if (c != null && (c.name.Contains("Village_Watchtower_Left") || c.name.Contains("Village_Watchtower_Right")))
                    {
                        Destroy(c.gameObject);
                    }
                }
            }
        }

        public static bool IsAssaultMode => (PrehistoricGameModeManager.Instance != null && 
                                             PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault);

        /// <summary>
        /// Trong chế độ Tower Defense: Vô hiệu hóa toàn bộ tường chắn mặc định có sẵn của cảnh
        /// để người chơi có 100% quyền tự do chiến thuật (Căn cứ mở vs Mê cung - Mazing).
        /// Hàng rào gỗ mặc định chỉ được kích hoạt tự động ở chế độ Công Thành (Dino Assault).
        /// </summary>
        public void DeactivateDefaultSceneBarricades()
        {
            if (IsAssaultMode)
            {
                RestoreAllVillageBarricades();
            }
            else
            {
                RemoveAllVillageFrontBarricades();
            }
        }

        public void EnsureScenicFortressWalkways()
        {
            if (IsAssaultMode)
            {
                RestoreAllVillageBarricades();
            }
            else
            {
                RemoveAllVillageFrontBarricades();
            }
        }

        /// <summary>
        /// Xóa bỏ và vô hiệu hóa triệt để toàn bộ hàng rào gỗ mặc định phía trước làng ở chế độ Thủ Thành (Tower Defense)
        /// để người chơi tự do xây dựng phòng tuyến bằng Toolbar hoặc để lối đi mở.
        /// </summary>
        public void RemoveAllVillageFrontBarricades()
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                // Tuyệt đối không can thiệp vào tường do người chơi xây đặt trong trận (các bản Clone)
                if (n.Contains("(Clone)")) continue;

                // Không can thiệp vào Player, Camera, Light, Canvas, Dino, Base, Egg
                if (n.Contains("Camera") || n.Contains("Light") || n.Contains("Canvas") ||
                    n.Contains("Player") || n.Contains("Dino") || n.Contains("Base") || n.Contains("Egg"))
                {
                    continue;
                }

                // Không xóa chòi canh, vòm cổng, đuốc, cầu, sàn, lều làng
                bool isTower = n.Contains("Big Tower") || n.Contains("Watchtower") || n.Contains("Bastion_Tower") || n.Contains("Tower_Tribal");
                bool isArch = n.StartsWith("Arch Entry Wall", StringComparison.OrdinalIgnoreCase) || n.Contains("Overhead_Arch") || n.Contains("Arch_Entry");
                bool isTorch = n.Contains("Torch");
                bool isFloorOrBridge = n.Contains("Smooth Floor") || n.Contains("Walkway_Deck") ||
                                       n.Contains("Collider_Walkway_Deck") || n.Contains("FloorDeck") ||
                                       n.StartsWith("Bridge_Tribal", StringComparison.OrdinalIgnoreCase) ||
                                       n.StartsWith("Ramp_Tribal", StringComparison.OrdinalIgnoreCase);
                bool isHut = n.Contains("Hut_Tribal") || n.Contains("Tribal_Tent") || (n.Contains("Hut") && !n.Contains("HealthBar"));

                if (isTower || isArch || isTorch || isFloorOrBridge || isHut) continue;

                bool isChildOfFortress = false;
                if (t.parent != null)
                {
                    string pn = t.parent.name;
                    if (pn == "Left_Wing_Wall" || pn == "Right_Wing_Wall" || pn == "Right_Flank_Wall" || pn == "Gate_Complex" || pn == "Village_Fortress_Wall")
                    {
                        isChildOfFortress = true;
                    }
                }

                bool isFence = isChildOfFortress ||
                               n.Contains("Wall_Segment") || n.Contains("Plank_Defenses") ||
                               n.Contains("wooden_barricade") || n.Contains("wooden__barricade") ||
                               n.Contains("Barricade") || n.Contains("Palisade") ||
                               n.Contains("Gate_Portcullis_Barricade") ||
                               n.Contains("Flank_Barricade") || n.Contains("Village_Left_Gap_Wall");

                if (isFence)
                {
                    if (t.TryGetComponent(out Unit.Wall w))
                    {
                        if (Utility.HealthBarCanvas.HasInstance) Utility.HealthBarCanvas.Instance.Unregister(w);
                        Destroy(w);
                    }
                    if (t.TryGetComponent(out Unit.Unit u) && Utility.HealthBarCanvas.HasInstance)
                    {
                        Utility.HealthBarCanvas.Instance.Unregister(u);
                    }

                    var allObs = t.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
                    foreach (var obs in allObs)
                    {
                        if (obs != null)
                        {
                            obs.enabled = false;
                            obs.carving = false;
                            Destroy(obs);
                        }
                    }

                    var allCols = t.GetComponentsInChildren<Collider>(true);
                    foreach (var c in allCols) if (c != null) c.enabled = false;

                    var allRends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in allRends) if (r != null) r.enabled = false;

                    for (int c = t.childCount - 1; c >= 0; c--)
                    {
                        Transform child = t.GetChild(c);
                        if (child != null && child.name.Contains("HealthBar")) Destroy(child.gameObject);
                    }

                    t.gameObject.SetActive(false);
                }
            }

            // Dọn dẹp thanh máu rác của các tường rào đã tắt
            if (Utility.HealthBarCanvas.HasInstance)
            {
                var canvas = Utility.HealthBarCanvas.Instance;
                for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                {
                    Transform c = canvas.transform.GetChild(i);
                    if (c != null && (c.name.Contains("Wall_Segment") || c.name.Contains("Portcullis") || c.name.Contains("Plank_Defenses") || c.name.Contains("Flank_Barricade")))
                    {
                        Destroy(c.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// Khôi phục và làm mới toàn bộ hệ thống công trình, chòi canh (2500 HP), cầu gỗ (3500 HP).
        /// Riêng hàng rào gỗ phía trước làng:
        /// - Chế độ Thủ Thành: TUYỆT ĐỐI không tạo/bật (để người chơi tự xây hoặc để lối đi mở).
        /// - Chế độ Công Thành: Kích hoạt đầy đủ 100% với máu chuẩn (1200 HP) và thanh máu.
        /// </summary>
        public void RestoreAllVillageBarricades()
        {
            bool isAssault = IsAssaultMode;
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // BƯỚC 1: Kích hoạt toàn bộ container cha và dọn dẹp các component thừa trên container cha để không sinh thanh máu ma
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                bool isContainer = n == "Village_Fortress_Wall" || n == "Left_Wing_Wall" ||
                                   n == "Right_Wing_Wall" || n == "Right_Flank_Wall" ||
                                   n == "Gate_Complex" || n == "Fortress_Root" || n == "Village Props";

                if (isContainer)
                {
                    t.gameObject.SetActive(true);
                    if (t.TryGetComponent(out Unit.Wall cw)) Destroy(cw);
                    if (t.TryGetComponent(out BoxCollider cbc)) Destroy(cbc);
                    if (t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle cobs)) Destroy(cobs);
                    if (t.TryGetComponent(out Unit.Unit cu) && Utility.HealthBarCanvas.HasInstance)
                    {
                        Utility.HealthBarCanvas.Instance.Unregister(cu);
                    }
                }
            }

            // BƯỚC 2: Phục hồi và kích hoạt các công trình, tường, sàn cầu và vật thể phòng thủ
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                // Bỏ qua tường do người chơi xây (Clone) - đã được xử lý riêng
                if (n.Contains("(Clone)")) continue;

                // Bỏ qua Player, Camera, Light, Canvas, Dino, Base, Egg
                if (n.Contains("Camera") || n.Contains("Light") || n.Contains("Canvas") ||
                    n.Contains("Player") || n.Contains("Dino") || n.Contains("Base") || n.Contains("Egg"))
                {
                    continue;
                }

                // Bỏ qua container cha
                if (n == "Village_Fortress_Wall" || n == "Left_Wing_Wall" ||
                    n == "Right_Wing_Wall" || n == "Right_Flank_Wall" ||
                    n == "Gate_Complex" || n == "Fortress_Root" || n == "Village Props")
                {
                    continue;
                }

                // Nhận diện loại công trình
                bool isFloor = n.Contains("Smooth Floor") || n.Contains("Walkway_Deck") ||
                               n.Contains("Collider_Walkway_Deck") || n.Contains("FloorDeck");

                bool isBridge = n.StartsWith("Bridge_Tribal", StringComparison.OrdinalIgnoreCase) ||
                                n.StartsWith("Ramp_Tribal", StringComparison.OrdinalIgnoreCase);

                bool isTower = n.Contains("Big Tower with Platform") || n.Contains("Watchtower") || n.Contains("Bastion_Tower") || n.Contains("Tower_Tribal");
                bool isArch = n.StartsWith("Arch Entry Wall", StringComparison.OrdinalIgnoreCase) || n.Contains("Overhead_Arch") || n.Contains("Arch_Entry");
                bool isHut = n.Contains("Hut_Tribal") || n.Contains("Tribal_Tent") || (n.Contains("Hut") && !n.Contains("HealthBar"));

                // Kiểm tra trực tiếp tên hoặc đối tượng nằm trong nhóm tường thành / rào chắn
                bool isChildOfFortress = false;
                if (t.parent != null)
                {
                    string pn = t.parent.name;
                    if (pn == "Left_Wing_Wall" || pn == "Right_Wing_Wall" || pn == "Right_Flank_Wall" || pn == "Gate_Complex" || pn == "Village_Fortress_Wall")
                    {
                        isChildOfFortress = true;
                    }
                }

                bool isFence = (isChildOfFortress && !isTower && !isArch && !isBridge && !isFloor && !n.Contains("Torch")) ||
                               n.Contains("Wall_Segment") || n.Contains("Plank_Defenses") ||
                               n.Contains("wooden_barricade") || n.Contains("wooden__barricade") ||
                               n.Contains("Barricade") || n.Contains("Palisade") ||
                               n.Contains("Gate_Portcullis_Barricade") ||
                               n.Contains("Flank_Barricade") || n.Contains("Village_Left_Gap_Wall");

                // Nếu là sàn ngầm qua sông (Smooth Floor)
                if (isFloor)
                {
                    t.gameObject.SetActive(true);
                    Transform[] floorChildren = t.GetComponentsInChildren<Transform>(true);
                    foreach (var c in floorChildren) if (c != null) c.gameObject.SetActive(true);
                    Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends) if (r != null) r.enabled = true;
                    Collider[] cols = t.GetComponentsInChildren<Collider>(true);
                    foreach (var c in cols) if (c != null) c.enabled = true;
                    if (t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle obs))
                    {
                        obs.carving = false;
                    }
                    continue;
                }

                // Nếu là hàng rào gỗ mặc định ở chế độ Thủ Thành (Tower Defense) -> VÔ HIỆU HÓA HOÀN TOÀN!
                if (isFence && !isAssault)
                {
                    if (t.TryGetComponent(out Unit.Wall fw))
                    {
                        if (Utility.HealthBarCanvas.HasInstance) Utility.HealthBarCanvas.Instance.Unregister(fw);
                        Destroy(fw);
                    }
                    if (t.TryGetComponent(out Unit.Unit fu) && Utility.HealthBarCanvas.HasInstance)
                    {
                        Utility.HealthBarCanvas.Instance.Unregister(fu);
                    }
                    var fObs = t.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
                    foreach (var obs in fObs)
                    {
                        if (obs != null) { obs.enabled = false; obs.carving = false; Destroy(obs); }
                    }
                    var fCols = t.GetComponentsInChildren<Collider>(true);
                    foreach (var c in fCols) if (c != null) c.enabled = false;
                    var fRends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in fRends) if (r != null) r.enabled = false;
                    for (int c = t.childCount - 1; c >= 0; c--)
                    {
                        Transform child = t.GetChild(c);
                        if (child != null && child.name.Contains("HealthBar")) Destroy(child.gameObject);
                    }
                    t.gameObject.SetActive(false);
                    continue;
                }

                if (!isFence && !isTower && !isArch && !isHut && !isBridge) continue;

                // Nếu là node con trực thuộc bên trong một Wall khác, kích hoạt nhưng không tạo Wall riêng
                Unit.Wall parentWall = t.parent != null ? t.parent.GetComponentInParent<Unit.Wall>(true) : null;
                if (parentWall != null && parentWall.gameObject != t.gameObject)
                {
                    t.gameObject.SetActive(true);
                    Renderer[] childRends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in childRends) if (r != null) r.enabled = true;
                    Collider[] childCols = t.GetComponentsInChildren<Collider>(true);
                    foreach (var c in childCols) if (c != null) c.enabled = true;
                    continue;
                }

                // Kích hoạt GameObject và toàn bộ hierarchy con cháu
                t.gameObject.SetActive(true);
                Transform[] allChildren = t.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    if (child != null) child.gameObject.SetActive(true);
                }

                // Kích hoạt lại toàn bộ Renderers
                Renderer[] allRends = t.GetComponentsInChildren<Renderer>(true);
                foreach (var r in allRends)
                {
                    if (r != null) r.enabled = true;
                }

                // Kích hoạt lại toàn bộ Colliders
                Collider[] allCols = t.GetComponentsInChildren<Collider>(true);
                foreach (var c in allCols)
                {
                    if (c != null) c.enabled = true;
                }

                // Xác định kích thước BoxCollider & HP chuẩn theo loại (Đã tăng máu Chòi lên 2500, Cầu gỗ lên 3500)
                int targetHp = 1200;
                Vector3 bcSize = new Vector3(3.2f, 2.0f, 1.4f);
                Vector3 bcCenter = new Vector3(0f, 1.0f, 0f);

                if (isBridge)
                {
                    targetHp = 3500; // Tăng máu Cầu Gỗ lên 3500 HP
                    bcSize = new Vector3(6.0f, 1.5f, 4.0f);
                    bcCenter = new Vector3(0f, 0.75f, 0f);
                }
                else if (isTower)
                {
                    targetHp = 2500; // Tăng máu Chòi Canh lên 2500 HP
                    bcSize = new Vector3(8.5f, 9.0f, 8.5f);
                    bcCenter = new Vector3(0f, 4.5f, 0f);
                }
                else if (isArch)
                {
                    targetHp = 1500;
                    bcSize = new Vector3(9.0f, 6.0f, 2.5f);
                    bcCenter = new Vector3(0f, 3.0f, 0f);
                }
                else if (isHut)
                {
                    targetHp = 1200;
                    bcSize = new Vector3(6.0f, 5.0f, 6.0f);
                    bcCenter = new Vector3(0f, 2.5f, 0f);
                }
                else // isFence (chỉ ở chế độ Công Thành)
                {
                    targetHp = 1200;
                    bcSize = new Vector3(3.2f, 2.0f, 1.4f);
                    bcCenter = new Vector3(0f, 1.0f, 0f);
                }

                if (!t.TryGetComponent(out BoxCollider bc))
                {
                    bc = t.gameObject.AddComponent<BoxCollider>();
                }
                bc.size = bcSize;
                bc.center = bcCenter;
                bc.enabled = true;

                // Cấu hình NavMeshObstacle:
                // Đối với cầu sông (Bridge/Ramp), TUYỆT ĐỐI không bật carving để khủng long và quân phòng thủ băng qua thông suốt!
                if (!isBridge)
                {
                    if (!t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle nmo))
                    {
                        nmo = t.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                    }
                    nmo.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                    nmo.size = bcSize;
                    nmo.center = bcCenter;
                    nmo.enabled = true;
                    nmo.carving = isFence; // Chỉ carve cho rào cọc, chòi không carve để tránh thủng NavMesh
                    nmo.carveOnlyStationary = false;
                }
                else
                {
                    if (t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle bObs))
                    {
                        bObs.enabled = false;
                        bObs.carving = false;
                        Destroy(bObs);
                    }
                }

                // Cấu hình Component Wall và phục hồi 100% máu
                if (!t.TryGetComponent(out Unit.Wall w))
                {
                    w = t.gameObject.AddComponent<Unit.Wall>();
                }
                w.MaxHealth = targetHp;
                w.Health = targetHp;
                w.FullRestore();

                // Gắn thanh máu chuẩn hiển thị trên đầu công trình
                w.EnsureHealthBarAttached();
                if (Utility.HealthBarCanvas.Instance != null)
                {
                    var hb = Utility.HealthBarCanvas.Instance.CreateHealthBarForUnit(w);
                    if (hb != null)
                    {
                        hb.gameObject.SetActive(true);
                        hb.SetProgress(1.0f);
                    }
                }
            }
        }

        public void ConfigureAllVillageFortifications(int level = 1)
        {
            bool isAssault = IsAssaultMode;
            float hpMult = 1.0f + Mathf.Max(0, level - 1) * 0.15f;
            if (Environment.PrehistoricMapManager.CurrentMap == Environment.PrehistoricMapType.VolcanicGorge)
            {
                hpMult *= 1.35f;
            }

            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // BƯỚC 1: Dọn dẹp triệt để mọi Wall, Collider, NavMeshObstacle trên các CONTAINER CHA RỖNG
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                bool isContainerGroup = n == "Village_Fortress_Wall" ||
                                        n == "Left_Wing_Wall" ||
                                        n == "Right_Wing_Wall" ||
                                        n == "Right_Flank_Wall" ||
                                        n == "Gate_Complex" ||
                                        n == "Fortress_Root" ||
                                        n == "Village Props";

                if (isContainerGroup)
                {
                    t.gameObject.SetActive(true);
                    if (t.TryGetComponent(out Unit.Wall cw)) Destroy(cw);
                    if (t.TryGetComponent(out BoxCollider cbc)) Destroy(cbc);
                    if (t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle cobs)) Destroy(cobs);
                    if (t.TryGetComponent(out Unit.Unit cu) && Utility.HealthBarCanvas.HasInstance)
                    {
                        Utility.HealthBarCanvas.Instance.Unregister(cu);
                    }
                }
            }

            // BƯỚC 2: Cấu hình và kích hoạt các công trình vật lý thực sự
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                // 1. Không can thiệp vào tường do người chơi đặt (Clone)
                if (n.Contains("(Clone)")) continue;

                // 2. Không can thiệp vào sàn ngầm
                if (n.Contains("Smooth Floor") || n.Contains("Walkway_Deck"))
                {
                    continue;
                }

                // 3. Không can thiệp vào Player, Camera, Light, Canvas, Projectile, Căn cứ chính
                if (n.Contains("Camera") || n.Contains("Light") || n.Contains("Canvas") ||
                    n.Contains("Player") || n.Contains("Dino") || n.Contains("Base") || n.Contains("Egg"))
                {
                    continue;
                }

                // 4. Bỏ qua các container cha
                if (n == "Village_Fortress_Wall" || n == "Left_Wing_Wall" || n == "Right_Wing_Wall" || 
                    n == "Right_Flank_Wall" || n == "Gate_Complex" || n == "Fortress_Root" || n == "Village Props")
                {
                    continue;
                }

                bool isBridge = n.StartsWith("Bridge_Tribal", StringComparison.OrdinalIgnoreCase) ||
                                n.StartsWith("Ramp_Tribal", StringComparison.OrdinalIgnoreCase);

                bool isTower = n.Contains("Big Tower with Platform") || n.Contains("Watchtower") || n.Contains("Bastion_Tower") || n.Contains("Tower_Tribal");
                bool isArch = n.StartsWith("Arch Entry Wall", StringComparison.OrdinalIgnoreCase) || n.Contains("Overhead_Arch") || n.Contains("Arch_Entry");
                bool isHut = n.Contains("Hut_Tribal") || n.Contains("Tribal_Tent") || (n.Contains("Hut") && !n.Contains("HealthBar"));
                bool isFence = n.Contains("Plank_Defenses") ||
                               n.Contains("Wall_Segment") || n.Contains("Flank_Barricade") ||
                               n.Contains("wooden_barricade") || n.Contains("wooden__barricade") ||
                               n.Contains("Barricade") || n.Contains("Palisade") ||
                               n.Contains("Gate_Portcullis_Barricade") ||
                               n.Contains("Left_Wall_Segment") || n.Contains("Right_Wall_Segment") ||
                               n.Contains("Village_Left_Gap_Wall");

                // Nếu ở chế độ Thủ Thành: TUYỆT ĐỐI không bật hàng rào gỗ mặc định!
                if (isFence && !isAssault)
                {
                    if (t.TryGetComponent(out Unit.Wall fw))
                    {
                        if (Utility.HealthBarCanvas.HasInstance) Utility.HealthBarCanvas.Instance.Unregister(fw);
                        Destroy(fw);
                    }
                    if (t.TryGetComponent(out Unit.Unit fu) && Utility.HealthBarCanvas.HasInstance)
                    {
                        Utility.HealthBarCanvas.Instance.Unregister(fu);
                    }
                    var fObs = t.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
                    foreach (var obs in fObs)
                    {
                        if (obs != null) { obs.enabled = false; obs.carving = false; Destroy(obs); }
                    }
                    var fCols = t.GetComponentsInChildren<Collider>(true);
                    foreach (var c in fCols) if (c != null) c.enabled = false;
                    var fRends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in fRends) if (r != null) r.enabled = false;
                    t.gameObject.SetActive(false);
                    continue;
                }

                if (!isTower && !isArch && !isHut && !isFence && !isBridge) continue;

                // Nếu là node con bên trong một Wall khác, không gắn trùng lặp
                Unit.Wall parentWall = t.parent != null ? t.parent.GetComponentInParent<Unit.Wall>(true) : null;
                if (parentWall != null && parentWall.gameObject != t.gameObject)
                {
                    continue;
                }

                // Kích hoạt GameObject và phục hồi toàn bộ Renderers
                t.gameObject.SetActive(true);
                Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends) if (r != null) r.enabled = true;

                // Cấu hình Collider & HP tùy theo loại công trình (Tăng máu Chòi: 2500, Cầu: 3500)
                int targetHp = 1200;
                Vector3 colSize = Vector3.zero;
                Vector3 colCenter = Vector3.zero;

                if (isBridge)
                {
                    targetHp = Mathf.RoundToInt(3500 * hpMult);
                    colSize = new Vector3(6.0f, 1.5f, 4.0f);
                    colCenter = new Vector3(0f, 0.75f, 0f);
                }
                else if (isTower)
                {
                    targetHp = Mathf.RoundToInt(2500 * hpMult);
                    colSize = new Vector3(8.5f, 9.0f, 8.5f);
                    colCenter = new Vector3(0f, 4.5f, 0f);
                }
                else if (isArch)
                {
                    targetHp = Mathf.RoundToInt(1500 * hpMult);
                    colSize = new Vector3(9.0f, 6.0f, 2.5f);
                    colCenter = new Vector3(0f, 3.0f, 0f);
                }
                else if (isHut)
                {
                    targetHp = Mathf.RoundToInt(1200 * hpMult);
                    colSize = new Vector3(6.0f, 5.0f, 6.0f);
                    colCenter = new Vector3(0f, 2.5f, 0f);
                }
                else if (isFence)
                {
                    targetHp = Mathf.RoundToInt(1200 * hpMult);
                    colSize = new Vector3(3.2f, 2.0f, 1.4f);
                    colCenter = new Vector3(0f, 1.0f, 0f);
                }

                // Cấu hình BoxCollider
                BoxCollider bc = t.GetComponent<BoxCollider>();
                if (bc == null)
                {
                    bc = t.gameObject.AddComponent<BoxCollider>();
                }
                if (colSize != Vector3.zero)
                {
                    bc.size = colSize;
                    bc.center = colCenter;
                }
                bc.enabled = true;

                // Phục hồi và kích hoạt toàn bộ Collider con
                Collider[] allCols = t.GetComponentsInChildren<Collider>(true);
                foreach (var c in allCols) if (c != null) c.enabled = true;

                // Cấu hình NavMeshObstacle: không carve cho cầu sông
                if (!isBridge)
                {
                    UnityEngine.AI.NavMeshObstacle obs = t.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                    if (obs == null) obs = t.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                    obs.enabled = true;
                    obs.carving = isFence;
                    obs.carveOnlyStationary = false;
                    if (bc != null)
                    {
                        obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                        obs.size = bc.size;
                        obs.center = bc.center;
                    }
                }
                else
                {
                    if (t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle bObs))
                    {
                        bObs.enabled = false;
                        bObs.carving = false;
                        Destroy(bObs);
                    }
                }

                // Cấu hình Component Wall và Máu
                if (!t.TryGetComponent(out Unit.Wall w))
                {
                    w = t.gameObject.AddComponent<Unit.Wall>();
                }
                w.MaxHealth = targetHp;
                w.Health = targetHp;

                // Tạo và gắn Thanh máu hiển thị trên màn hình
                w.EnsureHealthBarAttached();
                if (Utility.HealthBarCanvas.Instance != null)
                {
                    var hb = Utility.HealthBarCanvas.Instance.CreateHealthBarForUnit(w);
                    if (hb != null)
                    {
                        hb.gameObject.SetActive(true);
                        hb.SetProgress(1.0f);
                    }
                }
            }

            EnsureVillageBridgesDestructible();
        }

        public void EnsureScenicBridgeWatchtowers()
        {
            EnsureScenicFortressWalkways();
            EnsureVillageBridgesDestructible();
        }

        /// <summary>
        /// Đảm bảo tất cả các cầu gỗ, dốc gỗ, cổng vòm và hành lang kết nối làng
        /// (Bridge_Tribal, Ramp_Tribal, Arch Entry Wall, Overhead_Arch, Big Tower with Platform)
        /// TUYỆT ĐỐI THÔNG THOÁNG, không bị gắn NavMeshObstacle cản đường, không bị BoxCollider tàng hình bịt kín,
        /// đồng thời gán Wall máu cao (3500 HP) và gắn thanh máu rõ ràng cho cầu gỗ.
        /// </summary>
        public void EnsureVillageBridgesDestructible()
        {
            // Phục hồi lại Smooth Floor và các sàn gỗ kết nối
            var allFloors = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var fl in allFloors)
            {
                if (fl == null) continue;
                string fn = fl.gameObject.name;
                if (fn.Contains("Smooth Floor") || fn.Contains("Walkway_Deck") || fn.Contains("Collider_Walkway_Deck"))
                {
                    fl.gameObject.SetActive(true);
                    foreach (var r in fl.GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = true;
                    foreach (var c in fl.GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = true;
                    // Đảm bảo sàn không bao giờ có NavMeshObstacle
                    foreach (var obs in fl.GetComponentsInChildren<NavMeshObstacle>(true))
                    {
                        obs.enabled = false;
                        Destroy(obs);
                    }
                }
            }

            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                // Chỉ xử lý các cầu sông (Bridge_Tribal, Ramp_Tribal) để khủng long băng qua sông thông suốt:
                bool isRiverBridge = n.StartsWith("Bridge_Tribal", StringComparison.OrdinalIgnoreCase) ||
                                     n.StartsWith("Ramp_Tribal", StringComparison.OrdinalIgnoreCase);

                if (!isRiverBridge) continue;

                t.gameObject.SetActive(true);
                Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends) if (r != null) r.enabled = true;
                Collider[] cols = t.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols) if (c != null) c.enabled = true;

                // Triệt tiêu NavMeshObstacle trên cầu sông bắc qua nước để khủng long bước lên mặt cầu
                NavMeshObstacle[] allObs = t.GetComponentsInChildren<NavMeshObstacle>(true);
                foreach (var obs in allObs)
                {
                    if (obs != null)
                    {
                        obs.enabled = false;
                        obs.carving = false;
                        Destroy(obs);
                    }
                }

                // Cấu hình Wall và Máu cao cho cầu gỗ (3500 HP)
                int bridgeHp = 3500;
                if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
                {
                    float hpMult = 1.0f + Mathf.Max(0, PrehistoricGameModeManager.Instance.AssaultLevel - 1) * 0.15f;
                    bridgeHp = Mathf.RoundToInt(3500 * hpMult);
                }
                if (!t.TryGetComponent(out Unit.Wall w))
                {
                    w = t.gameObject.AddComponent<Unit.Wall>();
                }
                w.MaxHealth = bridgeHp;
                w.Health = bridgeHp;
                w.FullRestore();
                w.EnsureHealthBarAttached();
                if (HealthBarCanvas.Instance != null)
                {
                    var hb = HealthBarCanvas.Instance.CreateHealthBarForUnit(w);
                    if (hb != null)
                    {
                        hb.gameObject.SetActive(true);
                        hb.SetProgress(1.0f);
                    }
                }
            }
        }


        private void EnsureAudioManagerAndSettingsMenu()
        {
            if (Audio.PrehistoricAudioManager.Instance == null)
            {
                GameObject audioGO = new GameObject("PrehistoricAudioManager");
                audioGO.AddComponent<Audio.PrehistoricAudioManager>();
            }
            if (UI.PrehistoricSettingsMenu.Instance == null)
            {
                GameObject settingsGO = new GameObject("PrehistoricSettingsMenu");
                settingsGO.AddComponent<UI.PrehistoricSettingsMenu>();
            }
            if (UI.PrehistoricMainMenu.Instance == null)
            {
                GameObject menuGO = new GameObject("PrehistoricMainMenu");
                menuGO.AddComponent<UI.PrehistoricMainMenu>();
            }
            if (AR.ARTabletopController.Instance == null)
            {
                GameObject arGO = new GameObject("ARTabletopController");
                arGO.AddComponent<AR.ARTabletopController>();
            }
            if (Save.PrehistoricSaveManager.Instance == null)
            {
                GameObject saveGO = new GameObject("PrehistoricSaveManager");
                saveGO.AddComponent<Save.PrehistoricSaveManager>();
            }
            if (UI.PrehistoricSaveLoadDialog.Instance == null)
            {
                UI.PrehistoricSaveLoadDialog.EnsureInstance();
            }
        }

        private void Start()
        {
            LocateBaseTarget();
            EnsureSpawnPoints();
            LoadDinoPrefabsIfNull();

            // Ghi nhận vị trí ban đầu của Camera
            if (Camera.main != null)
            {
                _InitialCameraPos = Camera.main.transform.position;
                _InitialCameraRot = Camera.main.transform.rotation;
            }

            // Ghi nhận vị trí ban đầu của Player (nếu có trong scene)
            _PlayerObj = GameObject.FindWithTag("Player");
            if (_PlayerObj != null)
            {
                _InitialPlayerPos = _PlayerObj.transform.position;
                _InitialPlayerRot = _PlayerObj.transform.rotation;
            }

            // Set Initial Gold in TowerPlacer
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.ResetGoldToDefault(StartingGold);
            }

            // Sync with RoundManager if present
            SyncWithLegacyRoundManager();

            // 1. Vô hiệu hóa toàn bộ tường chắn/chòi mặc định của cảnh cho chế độ TD tự do mazing TRƯỚC TIÊN
            DeactivateDefaultSceneBarricades();
            EnsureScenicBridgeWatchtowers();

            // 2. Cấu hình va chạm và AI nhận diện chướng ngại vật cho toàn bộ tường do người chơi đặt (Clone)
            ConfigureAllCustomBarricades();

            // 3. Kích hoạt NPC chiến binh bộ lạc xuất hiện cùng phòng thủ làng
            EnsureVillageDefendersActive();

            foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (r != null && r.gameObject != null && r.gameObject.name.Contains("NavMeshVisualization"))
                {
                    r.enabled = false;
                    r.gameObject.SetActive(false);
                    Destroy(r.gameObject);
                }
            }

            // 4. Kích hoạt AdaptiveWaveManager làm Deep Learning AI Director phân tích chiến thuật
            AdaptiveWaveManager waveMgr = FindFirstObjectByType<AdaptiveWaveManager>();
            if (waveMgr != null)
            {
                waveMgr.enabled = true;
            }

            // 5. Kích hoạt On-Device Deep Learning Neural Director & XAI Monitor HUD (sau khi đã dọn sạch tường mặc định)
            if (AI.PrehistoricNeuralDirector.Instance != null)
            {
                AI.PrehistoricNeuralDirector.Instance.EnableOnDeviceInference = true;
                AI.PrehistoricNeuralDirector.Instance.Predict(_CurrentWave);
            }
            if (UI.PrehistoricNeuralMonitorUI.Instance != null)
            {
                // Sẵn sàng hiển thị XAI Monitor HUD
            }

            SetPhase(PrehistoricWavePhase.BuildingPhase);
            if (UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen)
            {
                Audio.PrehistoricAudioManager.Instance?.PlayOpeningBgm();
            }
            else
            {
                ShowAnnouncement($"ĐỢT {_CurrentWave}: HÃY ĐẶT THÁP PHÒNG THỦ TRƯỚC KHI BẮT ĐẦU!", 4.0f);
            }
        }

        private void Update()
        {
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
            {
                return;
            }

            if (UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen)
            {
                return;
            }
            if (AnnouncementTimer > 0f)
            {
                AnnouncementTimer -= Time.deltaTime;
                if (AnnouncementTimer <= 0f) ScreenAnnouncement = "";
            }

            bool isSpacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool isRPressed = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            bool isEnterPressed = Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);

            // Phase-specific input handling
            if (_CurrentPhase == PrehistoricWavePhase.BuildingPhase)
            {
                if (isSpacePressed)
                {
                    StartNextWave();
                }
            }
            else if (_CurrentPhase == PrehistoricWavePhase.GameOver)
            {
                bool isEscPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                bool isQPressed = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

                if (isSpacePressed || isEnterPressed)
                {
                    RetryCurrentWave();
                }
                else if (isRPressed)
                {
                    FullResetGame();
                }
                else if (isEscPressed || isQPressed)
                {
                    QuitGame();
                }
            }
            else if (_CurrentPhase == PrehistoricWavePhase.Victory)
            {
                bool isEscPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                bool isQPressed = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

                if (isSpacePressed || isEnterPressed || isRPressed)
                {
                    FullResetGame();
                }
                else if (isEscPressed || isQPressed)
                {
                    QuitGame();
                }
            }

            // Hotkey F11 hoặc Alt+Enter để chuyển đổi Fullscreen / Toàn màn hình
            if (Keyboard.current != null)
            {
                bool isF11Pressed = Keyboard.current.f11Key.wasPressedThisFrame;
                bool isAltEnter = (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed) && Keyboard.current.enterKey.wasPressedThisFrame;

                if (isF11Pressed || isAltEnter)
                {
                    ToggleFullscreen();
                }
            }

            // In Wave 10, track Boss health
            if (_CurrentWave >= 8 && _CurrentPhase == PrehistoricWavePhase.CombatPhase)
            {
                if (ActiveBossInstance == null || ActiveBossInstance.Health <= 0 || !ActiveBossInstance.gameObject.activeInHierarchy)
                {
                    var bosses = FindObjectsByType<BossDino>(FindObjectsSortMode.None);
                    for (int b = 0; b < bosses.Length; b++)
                    {
                        if (bosses[b] != null && bosses[b].Health > 0 && bosses[b].gameObject.activeInHierarchy)
                        {
                            ActiveBossInstance = bosses[b];
                            break;
                        }
                    }
                }
            }
        }

        public void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            if (Screen.fullScreen)
            {
                Resolution currentRes = Screen.currentResolution;
                Screen.SetResolution(currentRes.width, currentRes.height, FullScreenMode.FullScreenWindow);
                ShowAnnouncement("🖥 ĐÃ BẬT CHẾ ĐỘ TOÀN MÀN HÌNH (FULLSCREEN)", 2.5f);
            }
            else
            {
                ShowAnnouncement("🪟 ĐÃ CHUYỂN VỀ CHẾ ĐỘ CỬA SỔ (WINDOWED)", 2.5f);
            }
        }

        public void LocateBaseTarget()
        {
            try
            {
                if (BaseTarget != null && BaseTarget.gameObject != null)
                {
                    return;
                }
            }
            catch
            {
                BaseTarget = null;
            }

            GameObject eggSpawn = GameObject.Find("Dino Egg Spawn");
            if (eggSpawn != null)
            {
                BaseTarget = eggSpawn.transform;
            }
            else
            {
                GameObject baseObj = new GameObject("Dino Egg Spawn");
                baseObj.transform.position = new Vector3(-2.23f, 0f, -38.26f);
                BaseTarget = baseObj.transform;
            }

            if (BaseTarget != null)
            {
                if (!BaseTarget.TryGetComponent(out BoxCollider col))
                {
                    col = BaseTarget.gameObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(4f, 3f, 4f);
                    col.center = new Vector3(0f, 1.5f, 0f);
                }
                if (!BaseTarget.TryGetComponent(out Unit.PrehistoricVillageBase vb))
                {
                    vb = BaseTarget.gameObject.AddComponent<Unit.PrehistoricVillageBase>();
                }
                vb.SetBaseStats(_CurrentBaseHealth);
            }

            if (RoundManager.Instance != null)
            {
                // Ensure legacy RoundManager also has DinoTarget set
                var prop = typeof(RoundManager).GetProperty("DinoTarget");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(RoundManager.Instance, BaseTarget);
                }
            }
        }

        public Vector3 GetValidBaseTargetPosition()
        {
            LocateBaseTarget();
            if (BaseTarget != null)
            {
                try
                {
                    if (BaseTarget.gameObject != null)
                    {
                        return BaseTarget.position;
                    }
                }
                catch { }
            }
            return new Vector3(-2.23f, 0f, -38.26f);
        }

        public void EnsureSpawnPoints()
        {
            bool allValid = SpawnPoints != null && SpawnPoints.Length >= 3;
            if (allValid)
            {
                for (int i = 0; i < SpawnPoints.Length; i++)
                {
                    try
                    {
                        if (SpawnPoints[i] == null || SpawnPoints[i].gameObject == null)
                        {
                            allValid = false;
                            break;
                        }
                    }
                    catch
                    {
                        allValid = false;
                        break;
                    }
                }
            }
            if (allValid) return;

            GameObject spRoot = GameObject.Find("Prehistoric_SpawnPoints");
            if (spRoot == null)
            {
                spRoot = new GameObject("Prehistoric_SpawnPoints");
            }

            Transform sp1 = spRoot.transform.Find("Spawn_North");
            if (sp1 == null)
            {
                GameObject g = new GameObject("Spawn_North");
                g.transform.SetParent(spRoot.transform);
                g.transform.position = new Vector3(-2.2f, 0f, 35f);
                sp1 = g.transform;
            }

            Transform sp2 = spRoot.transform.Find("Spawn_East");
            if (sp2 == null)
            {
                GameObject g = new GameObject("Spawn_East");
                g.transform.SetParent(spRoot.transform);
                g.transform.position = new Vector3(25f, 0f, 20f);
                sp2 = g.transform;
            }

            Transform sp3 = spRoot.transform.Find("Spawn_West");
            if (sp3 == null)
            {
                GameObject g = new GameObject("Spawn_West");
                g.transform.SetParent(spRoot.transform);
                g.transform.position = new Vector3(-30f, 0f, 15f);
                sp3 = g.transform;
            }

            SpawnPoints = new Transform[] { sp1, sp2, sp3 };
        }

        public Vector3 GetValidSpawnPosition(int routeIndex = -1)
        {
            EnsureSpawnPoints();

            Transform chosenSpawn = null;
            if (AI.PrehistoricNeuralDirector.Instance != null && SpawnPoints != null && SpawnPoints.Length > 0)
            {
                try
                {
                    chosenSpawn = AI.PrehistoricNeuralDirector.Instance.SelectSpawnGateByQLearning(SpawnPoints);
                }
                catch { }
            }

            if (chosenSpawn == null)
            {
                if (SpawnPoints != null)
                {
                    for (int i = 0; i < SpawnPoints.Length; i++)
                    {
                        try
                        {
                            if (SpawnPoints[i] != null && SpawnPoints[i].gameObject != null)
                            {
                                chosenSpawn = SpawnPoints[i];
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            Vector3 basePos;
            if (chosenSpawn != null)
            {
                try
                {
                    basePos = chosenSpawn.position;
                }
                catch
                {
                    basePos = new Vector3(-2.2f, 0f, 35f);
                }
            }
            else
            {
                Vector3[] fallbackGates = new Vector3[]
                {
                    new Vector3(-2.2f, 0f, 35f),
                    new Vector3(25f, 0f, 20f),
                    new Vector3(-30f, 0f, 15f)
                };
                int idx = routeIndex >= 0 ? (routeIndex % fallbackGates.Length) : UnityEngine.Random.Range(0, fallbackGates.Length);
                basePos = fallbackGates[idx];
            }

            return basePos + new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0f, UnityEngine.Random.Range(-2.5f, 2.5f));
        }

        private void LoadDinoPrefabsIfNull()
        {
            if (RaptorPrefab == null) RaptorPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Velociraptor");
            if (PterodactylPrefab == null) PterodactylPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Pterodactyl");
            if (AnkylosaurusPrefab == null) AnkylosaurusPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Ankylosaurus");
            if (TRexBossPrefab == null) TRexBossPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_TRexBoss");

#if UNITY_EDITOR
            if (RaptorPrefab == null) RaptorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_Velociraptor.prefab");
            if (PterodactylPrefab == null) PterodactylPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_Pterodactyl.prefab");
            if (AnkylosaurusPrefab == null) AnkylosaurusPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_Ankylosaurus.prefab");
            if (TRexBossPrefab == null) TRexBossPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_TRexBoss.prefab");
#endif
        }

        public void StartNextWave()
        {
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault) return;
            if (_CurrentPhase != PrehistoricWavePhase.BuildingPhase) return;

            LocateBaseTarget();
            EnsureSpawnPoints();
            LoadDinoPrefabsIfNull();

            // Xóa sạch toàn bộ rác và quái vật còn sót lại từ đợt cũ trước khi bắt đầu đợt mới
            CleanAllGameplayGarbage();

            // Đảm bảo các NPC chiến binh bộ lạc luôn có mặt để chiến đấu
            EnsureVillageDefendersActive();

            SetPhase(PrehistoricWavePhase.CombatPhase);
            OnWaveStarted?.Invoke(_CurrentWave);
            Audio.PrehistoricAudioManager.Instance?.PlayWaveHorn();

            if (WaveSpawnRoutine != null) StopCoroutine(WaveSpawnRoutine);
            WaveSpawnRoutine = StartCoroutine(SpawnWaveRoutine(_CurrentWave));
        }

        private IEnumerator SpawnWaveRoutine(int waveIndex)
        {
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault) yield break;

            LocateBaseTarget();
            EnsureSpawnPoints();
            LoadDinoPrefabsIfNull();

            ActiveMonsters.Clear();
            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.ClearAll();
            }
            List<GameObject> spawnQueue = GenerateWaveComposition(waveIndex);
            TotalMonstersInWave = spawnQueue.Count;
            _MonstersRemainingToSpawn = spawnQueue.Count;
            MonstersAliveCount = _MonstersRemainingToSpawn;

            // Phát tiếng gầm của từng loài khủng long xuất hiện trong đợt này
            List<string> dinoTypesInWave = new List<string>();
            foreach (var prefab in spawnQueue)
            {
                if (prefab != null && !dinoTypesInWave.Contains(prefab.name))
                {
                    dinoTypesInWave.Add(prefab.name);
                }
            }
            Audio.PrehistoricAudioManager.Instance?.PlayWaveDinoRoars(dinoTypesInWave);

            ShowAnnouncement($"⚔ ĐỢT {waveIndex} BẮT ĐẦU! {TotalMonstersInWave} KHỦNG LONG ĐANG TẤN CÔNG! ⚔", 3.0f);
            Debug.Log($"<color=orange>[Prehistoric TD]</color> Starting Wave {waveIndex} with {TotalMonstersInWave} dinos!");

            // Send telemetry & query Deep Learning AI Director if connected to IO Cloud
            if (LlamAcademy.Dinos.Cloud.IOCloudManager.Instance != null && LlamAcademy.Dinos.Cloud.IOCloudManager.Instance.IsCloudConnected)
            {
                LlamAcademy.Dinos.Cloud.IOCloudManager.Instance.RequestDeepLearningWave(waveIndex);
            }

            yield return new WaitForSeconds(1.0f);

            for (int i = 0; i < spawnQueue.Count; i++)
            {
                if (_CurrentPhase == PrehistoricWavePhase.GameOver) yield break;

                GameObject dinoPrefab = spawnQueue[i];
                if (dinoPrefab != null)
                {
                    Vector3 spawnPos = GetValidSpawnPosition(i % 3);
                    Vector3 targetPos = GetValidBaseTargetPosition();

                    // Đảm bảo spawnPos được định vị chuẩn xác trên NavMesh của Khủng long trước khi Instantiate
                    NavMeshQueryFilter spawnFilter = new NavMeshQueryFilter { agentTypeID = 1479372276, areaMask = NavMesh.AllAreas };
                    if (NavMesh.SamplePosition(spawnPos, out NavMeshHit sHit, 25f, spawnFilter))
                    {
                        spawnPos = sHit.position;
                    }
                    else if (NavMesh.SamplePosition(spawnPos, out sHit, 25f, NavMesh.AllAreas))
                    {
                        spawnPos = sHit.position;
                    }

                    Vector3 dir = (targetPos - spawnPos).normalized;
                    dir.y = 0;
                    Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

                    GameObject spawned = Instantiate(dinoPrefab, spawnPos, rot);
                    if (Environment.PrehistoricMapManager.CurrentMap == Environment.PrehistoricMapType.VolcanicGorge)
                    {
                        Environment.PrehistoricVolcanicMutant.ApplyMutation(spawned);
                    }
                    if (spawned.TryGetComponent(out Unit.Unit unitComp))
                    {
                        ActiveMonsters.Add(unitComp);
                        unitComp.OnDeath += HandleMonsterDeath;

                        // TĂNG MÁU KHỦNG LONG THEO MỖI MÀN (+20% mỗi màn) ĐỂ CÂN BẰNG VỚI SỐ LƯỢNG NPC PHÒNG THỦ LÀNG
                        float waveHpMult = 1.0f + (waveIndex - 1) * 0.20f;
                        int baseHp = unitComp.MaxHealth > 0 ? unitComp.MaxHealth : (unitComp.UnitType != null ? unitComp.UnitType.Health : 280);
                        int scaledMaxHp = Mathf.RoundToInt(baseHp * waveHpMult);
                        unitComp.MaxHealth = scaledMaxHp;
                        unitComp.Health = scaledMaxHp;
                        unitComp.EnsureHealthBarAttached();
                    }

                    if (spawned.TryGetComponent(out Unit.PrehistoricDinoBase dinoBase))
                    {
                        dinoBase.EnsureOnNavMesh(25f);
                        dinoBase.ConfigureNavMeshAgent();
                        if (dinoBase.Agent != null && dinoBase.Agent.isOnNavMesh)
                        {
                            dinoBase.Agent.isStopped = false;
                            dinoBase.Agent.updateRotation = true;
                            dinoBase.Agent.SetDestination(targetPos);
                        }
                    }
                    else if (spawned.TryGetComponent(out Unit.FlyingUnit flyComp))
                    {
                        if (BaseTarget != null)
                        {
                            try
                            {
                                if (BaseTarget.gameObject != null) flyComp.SetTargetBase(BaseTarget);
                            }
                            catch { }
                        }
                    }
                }

                _MonstersRemainingToSpawn--;
                ActiveMonsters.RemoveAll(item => item == null);
                MonstersAliveCount = _MonstersRemainingToSpawn + ActiveMonsters.Count;

                // Staggered interval between dino spawns
                float spawnDelay = Mathf.Clamp(1.2f - (waveIndex * 0.05f), 0.5f, 1.2f);
                yield return new WaitForSeconds(spawnDelay);
            }

            // Chờ cho đến khi toàn bộ quái trong danh sách spawn ĐÃ RA HẾT VÀ TOÀN BỘ quái trên map ĐÃ CHẾT hoặc chạm căn cứ
            while ((_MonstersRemainingToSpawn > 0 || ActiveMonsters.Count > 0) && _CurrentPhase == PrehistoricWavePhase.CombatPhase)
            {
                ActiveMonsters.RemoveAll(item => item == null);
                MonstersAliveCount = _MonstersRemainingToSpawn + ActiveMonsters.Count;
                yield return new WaitForSeconds(0.25f);
            }

            if (_CurrentPhase != PrehistoricWavePhase.CombatPhase) yield break;

            // Wave cleared!
            if (_CurrentWave >= _MaxWaves)
            {
                TriggerVictory();
            }
            else
            {
                TriggerWaveCleared();
            }
        }

        private List<GameObject> GenerateWaveComposition(int waveIndex)
        {
            // 1. ƯU TIÊN SỐ 1: Deep Learning Neural Director trực tiếp điều phối đội hình quái vật theo thời gian thực
            if (AI.PrehistoricNeuralDirector.Instance != null)
            {
                var neuralQueue = BuildAdaptiveWaveFromNeuralPrediction(waveIndex);
                if (neuralQueue != null && neuralQueue.Count > 0)
                {
                    return neuralQueue;
                }
            }

            // 2. Dự phòng: AdaptiveWaveManager nếu có
            if (AdaptiveWaveManager.Instance != null)
            {
                string rationale;
                var adaptiveQueue = AdaptiveWaveManager.Instance.GenerateAdaptiveWave(waveIndex, RaptorPrefab, PterodactylPrefab, AnkylosaurusPrefab, TRexBossPrefab, out rationale);
                Debug.Log($"<color=#00FFAA><b>[AI Director Deep Learning]</b></color> Đợt {waveIndex}: {rationale}");
                ShowAnnouncement($"<b><color=#00FFAA>[AI TÁC CHIẾN]</color></b> ĐỢT {waveIndex}:\n{rationale}", 4.5f);
                if (adaptiveQueue != null && adaptiveQueue.Count > 0)
                {
                    return adaptiveQueue;
                }
            }

            List<GameObject> queue = new();

            switch (waveIndex)
            {
                case 1:
                    // Wave 1: 13 Velociraptors (+3 con)
                    AddMultiple(queue, RaptorPrefab, 13);
                    break;
                case 2:
                    // Wave 2: 12 Raptors + 5 Pterodactyls (Tổng: 17 con, +3 con)
                    AddMultiple(queue, RaptorPrefab, 12);
                    AddMultiple(queue, PterodactylPrefab, 5);
                    break;
                case 3:
                    // Wave 3: 13 Raptors + 6 Pterodactyls + 4 Ankylosaurus (Tổng: 23 con, +3 con)
                    AddMultiple(queue, RaptorPrefab, 13);
                    AddMultiple(queue, PterodactylPrefab, 6);
                    AddMultiple(queue, AnkylosaurusPrefab, 4);
                    break;
                case 4:
                    // Wave 4: 15 Raptors + 7 Pterodactyls + 5 Ankylosaurus (Tổng: 27 con, +3 con)
                    AddMultiple(queue, RaptorPrefab, 15);
                    AddMultiple(queue, PterodactylPrefab, 7);
                    AddMultiple(queue, AnkylosaurusPrefab, 5);
                    break;
                case 5:
                    // Wave 5: 17 Raptors + 8 Pterodactyls + 6 Ankylosaurus (Tổng: 31 con, +3 con)
                    AddMultiple(queue, RaptorPrefab, 17);
                    AddMultiple(queue, PterodactylPrefab, 8);
                    AddMultiple(queue, AnkylosaurusPrefab, 6);
                    break;
                case 6:
                    // Wave 6: 19 Raptors + 9 Pterodactyls + 7 Ankylosaurus (Tổng: 35 con, +3 con)
                    AddMultiple(queue, RaptorPrefab, 19);
                    AddMultiple(queue, PterodactylPrefab, 9);
                    AddMultiple(queue, AnkylosaurusPrefab, 7);
                    break;
                case 7:
                    // Wave 7: 21 Raptors + 10 Pterodactyls + 8 Ankylosaurus (Tổng: 39 con, +3 con)
                    AddMultiple(queue, RaptorPrefab, 21);
                    AddMultiple(queue, PterodactylPrefab, 10);
                    AddMultiple(queue, AnkylosaurusPrefab, 8);
                    break;
                case 8:
                    // Wave 8: 22 Raptors + 11 Pterodactyls + 9 Ankylosaurus + 1 BẠO CHÚA T-REX! (Tổng: 43 con)
                    AddMultiple(queue, RaptorPrefab, 22);
                    AddMultiple(queue, PterodactylPrefab, 11);
                    AddMultiple(queue, AnkylosaurusPrefab, 9);
                    if (TRexBossPrefab != null) queue.Add(TRexBossPrefab);
                    break;
                case 9:
                    // Wave 9: 24 Raptors + 12 Pterodactyls + 10 Ankylosaurus + 1 BẠO CHÚA T-REX! (Tổng: 47 con)
                    AddMultiple(queue, RaptorPrefab, 24);
                    AddMultiple(queue, PterodactylPrefab, 12);
                    AddMultiple(queue, AnkylosaurusPrefab, 10);
                    if (TRexBossPrefab != null) queue.Add(TRexBossPrefab);
                    break;
                case 10:
                default:
                    // Wave 10: ĐẠI CHIẾN TỐI HẬU - 2 CON BẠO CHÚA T-REX CÙNG XUẤT TRẬN! (Tổng: 37 con)
                    AddMultiple(queue, RaptorPrefab, 12);
                    AddMultiple(queue, PterodactylPrefab, 6);
                    AddMultiple(queue, AnkylosaurusPrefab, 5);
                    if (TRexBossPrefab != null)
                    {
                        queue.Add(TRexBossPrefab); // T-Rex Boss 1
                        queue.Add(TRexBossPrefab); // T-Rex Boss 2
                    }
                    AddMultiple(queue, RaptorPrefab, 8);
                    AddMultiple(queue, PterodactylPrefab, 4);
                    break;
            }

                        // Shuffle queue slightly so monster types alternate naturally
            for (int i = 0; i < queue.Count - 2; i++)
            {
                int r = UnityEngine.Random.Range(i, queue.Count);
                // Keep boss in the latter half
                if (queue[r] == TRexBossPrefab && i < queue.Count / 3) continue;
                (queue[i], queue[r]) = (queue[r], queue[i]);
            }

            return queue;
        }

        /// <summary>
        /// Sinh đội hình quái vật trực tiếp từ Mạng Nơ-ron Sâu Đa Nhiệm On-Device (PrehistoricTacticsNet)
        /// kết hợp Hệ số Thích ứng Độ khó (Dynamic Difficulty Adjustment - DDA).
        /// Đảm bảo tính thích ứng thực chiến 100%:
        /// - Căn cứ mở (0 tường): Tập trung bầy Velociraptor Swarm Rush càn quét tốc độ cao.
        /// - Căn cứ mê cung (Có tường): Lập tức triển khai Quái bay Pterodactyl vượt tường & Thiết giáp Ankylosaurus phá tường (kể cả từ Wave 1 hoặc 2).
        /// </summary>
        private List<GameObject> BuildAdaptiveWaveFromNeuralPrediction(int waveIndex)
        {
            List<GameObject> queue = new();
            var director = AI.PrehistoricNeuralDirector.Instance;
            if (director == null) return queue;

            // 1. Thực hiện suy luận Mạng Nơ-ron Đa Nhiệm On-Device (PrehistoricTacticsNet)
            var pred = director.Predict(waveIndex);
            string rationale = pred.TacticalRationale;
            Debug.Log($"<color=#00FFAA><b>[AI Director Deep Learning]</b></color> Đợt {waveIndex} | Tường: {pred.WallCount} | {rationale}");
            ShowAnnouncement($"<b><color=#00FFAA>[AI TÁC CHIẾN]</color></b> ĐỢT {waveIndex}:\n{rationale}", 4.5f);

            // 2. Tính quy mô đợt theo Dynamic Difficulty Adjustment (DDA)
            int totalMonsters = pred.RecommendedWaveSize;
            int minTotal = 13 + (waveIndex - 1) * 3;
            totalMonsters = Mathf.Max(totalMonsters, minTotal);

            // Boss T-Rex xuất hiện ở màn 8, 9 (1 con) và màn 10 (2 con)
            int bossCount = (waveIndex == 8 || waveIndex == 9) ? 1 : (waveIndex >= 10 ? 2 : 0);
            int normalCount = Mathf.Max(6, totalMonsters - bossCount);

            // 3. Phân bổ xác suất Softmax từng loài khủng long
            float runnerProb = pred.ArchetypeDistribution != null && pred.ArchetypeDistribution.Length > 0 ? pred.ArchetypeDistribution[0] : 0.50f;
            float flyerProb = pred.ArchetypeDistribution != null && pred.ArchetypeDistribution.Length > 1 ? pred.ArchetypeDistribution[1] : 0.25f;
            float tankProb = pred.ArchetypeDistribution != null && pred.ArchetypeDistribution.Length > 2 ? pred.ArchetypeDistribution[2] : 0.25f;

            int raptorCount;
            int pteroCount;
            int ankylCount;

            if (pred.WallCount == 0)
            {
                // CĂN CỨ MỞ (0 TƯỜNG):
                // Tập trung bầy Velociraptor chạy thẳng
                if (waveIndex == 1)
                {
                    raptorCount = normalCount;
                    pteroCount = 0;
                    ankylCount = 0;
                }
                else if (waveIndex == 2)
                {
                    pteroCount = Mathf.RoundToInt(normalCount * Mathf.Min(flyerProb, 0.25f));
                    ankylCount = 0;
                    raptorCount = normalCount - pteroCount;
                }
                else
                {
                    float sum = runnerProb + flyerProb + tankProb;
                    if (sum > 0f) { runnerProb /= sum; flyerProb /= sum; tankProb /= sum; }
                    pteroCount = Mathf.RoundToInt(normalCount * flyerProb);
                    ankylCount = Mathf.RoundToInt(normalCount * tankProb);
                    raptorCount = normalCount - (pteroCount + ankylCount);
                    if (raptorCount < 4) raptorCount = 4;
                }
            }
            else
            {
                // CĂN CỨ MÊ CUNG (NGƯỜI CHƠI XÂY TƯỜNG):
                // Deep Learning kích hoạt NGAY LẬP TỨC chiến thuật khắc chế:
                // Tăng quái bay (vượt tường) và thiết giáp (phá tường), giảm runner
                float sum = runnerProb + flyerProb + tankProb;
                if (sum > 0f) { runnerProb /= sum; flyerProb /= sum; tankProb /= sum; }
                pteroCount = Mathf.Max(2, Mathf.RoundToInt(normalCount * flyerProb));
                ankylCount = Mathf.Max(1, Mathf.RoundToInt(normalCount * tankProb));
                raptorCount = Mathf.Max(3, normalCount - (pteroCount + ankylCount));
            }

            // Đảm bảo tổng số lượng bù đắp nếu làm tròn bị thiếu
            int sumAlloc = raptorCount + pteroCount + ankylCount;
            if (sumAlloc < normalCount)
            {
                raptorCount += (normalCount - sumAlloc);
            }

            // Nạp vào hàng đợi xuất trận
            if (RaptorPrefab != null) for (int i = 0; i < raptorCount; i++) queue.Add(RaptorPrefab);
            if (PterodactylPrefab != null) for (int i = 0; i < pteroCount; i++) queue.Add(PterodactylPrefab);
            if (AnkylosaurusPrefab != null) for (int i = 0; i < ankylCount; i++) queue.Add(AnkylosaurusPrefab);
            if (TRexBossPrefab != null)
            {
                for (int b = 0; b < bossCount; b++)
                {
                    queue.Add(TRexBossPrefab);
                }
            }

            // Xáo trộn thứ tự xuất trận ngẫu nhiên tự nhiên (đảm bảo boss không đi đầu)
            for (int i = 0; i < queue.Count - 2; i++)
            {
                int r = UnityEngine.Random.Range(i, queue.Count);
                if (queue[r] == TRexBossPrefab && i < queue.Count / 3) continue;
                (queue[i], queue[r]) = (queue[r], queue[i]);
            }

            return queue;
        }

        private void AddMultiple(List<GameObject> list, GameObject prefab, int count)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++) list.Add(prefab);
        }

        public void HandleMonsterDeath(IDamageable diedObject)
        {
            if (diedObject == null) return;
            diedObject.OnDeath -= HandleMonsterDeath;

            Unit.Unit deadMonster = diedObject as Unit.Unit;
            if (deadMonster != null)
            {
                // Chỉ xử lý nếu quái thực sự nằm trong danh sách ActiveMonsters (ngăn chặn triệt để đếm trừ 2 lần)
                if (!ActiveMonsters.Remove(deadMonster))
                {
                    return;
                }
            }
            else
            {
                return;
            }

            ActiveMonsters.RemoveAll(item => item == null);
            MonstersAliveCount = _MonstersRemainingToSpawn + ActiveMonsters.Count;

            // Award reward gold based on dino type
            int reward = 15;
            if (deadMonster is RunnerDino) reward = 15;
            else if (deadMonster is FlyingUnit) reward = 25;
            else if (deadMonster is SiegeDino) reward = 45;
            else if (deadMonster is BossDino) reward = 250;

            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.AddGold(reward);
            }

            Save.PrehistoricSaveManager.Instance?.RecordMonsterKilled();

            // Heatmap feedback
            if (DeadlinessHeatmap.Instance != null && deadMonster != null)
            {
                DeadlinessHeatmap.Instance.RecordDeath(deadMonster.transform.position, _CurrentWave, deadMonster.GetType().Name);
            }

            // Reinforcement Learning Q-Learning Route feedback (phạt điểm tử vong trên cổng di chuyển)
            if (AI.PrehistoricNeuralDirector.Instance != null && deadMonster != null)
            {
                AI.PrehistoricNeuralDirector.Instance.RecordDinoDeath(deadMonster.transform.position);
            }
        }

        public void HandleMonsterDeath(Unit.Unit deadMonster)
        {
            HandleMonsterDeath((IDamageable)deadMonster);
        }

        private float _LastBaseAnnouncementTime = -99f;

        public void OnDinoReachedBase(Unit.Unit dino, int damage = 10)
        {
            if (_CurrentPhase != PrehistoricWavePhase.CombatPhase) return;
            if (dino == null) return;

            // Khủng long tiếp cận căn cứ: KHÔNG phá hủy khủng long! Quái ở lại cắn phá căn cứ liên tục!
            DamageBase(damage, dino);
        }

        public void DamageBase(int damage, Unit.Unit attackingDino = null)
        {
            if (_CurrentPhase == PrehistoricWavePhase.GameOver) return;

            _CurrentBaseHealth = Mathf.Max(0, _CurrentBaseHealth - damage);
            if (Unit.PrehistoricVillageBase.Instance != null && Unit.PrehistoricVillageBase.Instance.Health != _CurrentBaseHealth)
            {
                Unit.PrehistoricVillageBase.Instance.Health = _CurrentBaseHealth;
            }
            OnBaseHealthChanged?.Invoke(_CurrentBaseHealth, _MaxBaseHealth);

            // Reinforcement Learning: Thưởng điểm cho lộ trình đột kích thành công vào căn cứ (+30 reward)
            if (AI.PrehistoricNeuralDirector.Instance != null)
            {
                Vector3 hitPos = attackingDino != null ? attackingDino.transform.position : (BaseTarget != null ? BaseTarget.position : Vector3.zero);
                AI.PrehistoricNeuralDirector.Instance.RecordBaseHit(hitPos);
            }

            string attackerName = attackingDino != null ? attackingDino.GetType().Name : "Khủng long";
            if (Time.time - _LastBaseAnnouncementTime > 2.0f)
            {
                _LastBaseAnnouncementTime = Time.time;
                ShowAnnouncement($"<color=red>CẢNH BÁO: {attackerName} đang cắn phá Căn Cứ! -{damage} HP!</color>", 1.8f);
            }
            Debug.LogWarning($"<color=red>[Village Defense]</color> Base took {damage} damage! Remaining HP: {_CurrentBaseHealth}/{_MaxBaseHealth}");

            if (_CurrentBaseHealth <= 0)
            {
                TriggerGameOver();
            }
        }

        private void TriggerWaveCleared()
        {
            SetPhase(PrehistoricWavePhase.WaveCleared);

            if (WaveSpawnRoutine != null)
            {
                StopCoroutine(WaveSpawnRoutine);
                WaveSpawnRoutine = null;
            }

            // Dọn sạch toàn bộ rác và quái vật đợt vừa qua
            CleanAllGameplayGarbage();

            int rewardGold = WaveCompletionBaseGold + (_CurrentWave * 35);
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.AddGold(rewardGold);
            }

            ShowAnnouncement($"★ CHIẾN THẮNG ĐỢT {_CurrentWave}! THƯỞNG +{rewardGold} VÀNG! ★", 4.0f);
            OnWaveCompleted?.Invoke(_CurrentWave, rewardGold);
            Audio.PrehistoricAudioManager.Instance?.PlayVictory();
            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: true);

            // Cập nhật hệ số thích ứng độ khó DDA của AI Director
            AI.PrehistoricNeuralDirector.Instance?.RecordWaveOutcome(true, (float)_CurrentBaseHealth / _MaxBaseHealth, 45f);

            // Mở khóa chuột để người chơi tương tác chọn menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ProceedToNextWave()
        {
            Time.timeScale = 1.0f;
            ClearAllActiveMonsters();

            _CurrentWave++;

            // Phục hồi làng cho đợt mới và dọn tháp cũ nếu qua màn mới cần dàn trận mới
            ResetEntireVillage(keepPlayerTowers: false);
            TowerPlacer.Instance?.ClearAllPlacedTowers();
            int waveGold = StartingGold + (_CurrentWave - 1) * 300;
            TowerPlacer.Instance?.ResetGoldToDefault(waveGold);

            SetPhase(PrehistoricWavePhase.BuildingPhase);
            ResetPlayerAndCameraPosition();

            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: true);
            Audio.PrehistoricAudioManager.Instance?.PlayBgmForMode(PrehistoricGameMode.TowerDefense);
            ShowAnnouncement($"⚔️ ĐỢT {_CurrentWave}: HÃY ĐẶT THÊM THÁP VÀ BẤM 'BẮT ĐẦU ĐỢT' (SPACE)! 🛡️", 4.0f);
        }

        private IEnumerator TransitionToNextWaveRoutine()
        {
            yield return new WaitForSeconds(3.5f);
            ProceedToNextWave();
        }

        private void TriggerGameOver()
        {
            SetPhase(PrehistoricWavePhase.GameOver);
            StopAllCoroutines();
            Audio.PrehistoricAudioManager.Instance?.PlayGameOver();
            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: false);

            // Mở khóa con trỏ chuột để người chơi tương tác với menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ShowAnnouncement("☠ THẤT THỦ! NGÔI LÀNG TIỀN SỬ ĐÃ BỊ PHÁ HỦY! ☠", 10.0f);
            Debug.LogWarning("<color=red>[Prehistoric TD]</color> GAME OVER! The village was destroyed by dinosaurs!");
            AI.PrehistoricNeuralDirector.Instance?.RecordWaveOutcome(false, 0f, 60f);
            OnGameOver?.Invoke();
        }

        private void TriggerVictory()
        {
            SetPhase(PrehistoricWavePhase.Victory);
            StopAllCoroutines();
            Audio.PrehistoricAudioManager.Instance?.PlayVictory();

            // Mở khóa bản đồ kế tiếp khi chiến thắng đợt 10
            if (Environment.PrehistoricMapManager.CurrentMap == Environment.PrehistoricMapType.AncientValley)
            {
                Save.PrehistoricSaveManager.Instance?.UnlockMap(Environment.PrehistoricMapType.VolcanicGorge);
            }
            else if (Environment.PrehistoricMapManager.CurrentMap == Environment.PrehistoricMapType.VolcanicGorge)
            {
                Save.PrehistoricSaveManager.Instance?.UnlockMap(Environment.PrehistoricMapType.GlacialTundra);
            }

            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: false);

            // Mở khóa con trỏ chuột
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ShowAnnouncement("🏆 CHIẾN THẮNG KHẢI HOÀN! BẠN ĐÃ BẢO VỆ NGÔI LÀNG THÀNH CÔNG! 🏆", 15.0f);
            Debug.Log("<color=green>[Prehistoric TD]</color> VICTORY! Boss T-Rex defeated!");
            AI.PrehistoricNeuralDirector.Instance?.RecordWaveOutcome(true, (float)_CurrentBaseHealth / _MaxBaseHealth, 60f);
            OnVictory?.Invoke();
        }

        private void SetPhase(PrehistoricWavePhase newPhase)
        {
            _CurrentPhase = newPhase;
            SyncWithLegacyRoundManager();
            OnPhaseChanged?.Invoke(_CurrentPhase);
        }

        private void SyncWithLegacyRoundManager()
        {
            if (RoundManager.Instance == null) return;

            GameState targetLegacyState = _CurrentPhase switch
            {
                PrehistoricWavePhase.BuildingPhase => GameState.Setup,
                PrehistoricWavePhase.CombatPhase => GameState.Running,
                PrehistoricWavePhase.WaveCleared => GameState.Scoring,
                PrehistoricWavePhase.GameOver => GameState.GameOver,
                PrehistoricWavePhase.Victory => GameState.Victory,
                _ => GameState.Setup
            };

            // Safely set private State field via reflection if needed
            var stateProp = typeof(RoundManager).GetProperty("State");
            if (stateProp != null && stateProp.CanWrite)
            {
                stateProp.SetValue(RoundManager.Instance, targetLegacyState);
            }
            else
            {
                var field = typeof(RoundManager).GetField("_State", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(RoundManager.Instance, targetLegacyState);
                }
            }
        }

        public void ClearAllActiveMonsters()
        {
            for (int i = ActiveMonsters.Count - 1; i >= 0; i--)
            {
                if (ActiveMonsters[i] != null)
                {
                    ActiveMonsters[i].OnDeath -= HandleMonsterDeath;
                    if (Utility.HealthBarCanvas.Instance != null) Utility.HealthBarCanvas.Instance.Unregister(ActiveMonsters[i]);
                    if (ActiveMonsters[i].gameObject != null)
                    {
                        Destroy(ActiveMonsters[i].gameObject);
                    }
                }
            }
            ActiveMonsters.Clear();

            // Quét dọn triệt để TOÀN BỘ Unit khủng long/quái vật kể cả inactive hoặc đang ngã chết (TUYỆT ĐỐI KHÔNG XÓA DEFENDER, TƯỜNG, THÁP HAY GHOST PREVIEW)
            var allUnits = FindObjectsByType<Unit.Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var unit in allUnits)
            {
                if (unit != null && (unit is Dino || unit is PrehistoricDinoBase || unit is RunnerDino || unit is FlyingUnit || unit is SiegeDino || unit is BossDino))
                {
                    // Bỏ qua các ghost unit đang preview đặt quân
                    if (unit.transform.parent != null && unit.transform.parent.name.IndexOf("Visualization", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    unit.OnDeath -= HandleMonsterDeath;
                    if (Utility.HealthBarCanvas.Instance != null) Utility.HealthBarCanvas.Instance.Unregister(unit);
                    if (unit.gameObject != null)
                    {
                        Destroy(unit.gameObject);
                    }
                }
            }

            _MonstersRemainingToSpawn = 0;
            MonstersAliveCount = 0;
            TotalMonstersInWave = 0;
        }

        /// <summary>
        /// Dọn dẹp triệt để mọi vật thể rác còn sót lại sau mỗi ván chơi hoặc khi chuyển màn:
        /// 1. Tất cả đường đạn, vũ khí bay còn sót (Flying_Arrow, Flying_Spear, Giant_Spear, Flaming_Lava_Boulder, Tesla_Bolt...)
        /// 2. Toàn bộ khủng long/quái vật (kể cả inactive, disabled, đang ngã tumble)
        /// 3. Dọn dẹp các thanh máu mồ côi trên HealthBarCanvas
        /// 4. Xóa sạch bộ nhớ đột phá TacticalBreachMemory
        /// 5. Làm mới NavMesh để đảm bảo không còn lỗ thủng ảo do Obstacle cũ để lại
        /// </summary>
        public void CleanAllGameplayGarbage()
        {
            // 1. Quét dọn toàn bộ đường đạn và vũ khí bay còn lơ lửng trên scene
            string[] projectileNames = new string[]
            {
                "Flying_Arrow", "Flying_Spear", "Giant_Spear", "Flaming_Lava_Boulder", "Tesla_Bolt"
            };

            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null) continue;
                string n = t.name;

                for (int j = 0; j < projectileNames.Length; j++)
                {
                    if (n.StartsWith(projectileNames[j], System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (!n.Contains("Tower") && !n.Contains("Prefab") && t.GetComponent<Unit.Unit>() == null)
                        {
                            Destroy(t.gameObject);
                            break;
                        }
                    }
                }
            }

            // 2. Xóa sạch toàn bộ khủng long/quái vật
            ClearAllActiveMonsters();

            // 3. Dọn dẹp thanh máu mồ côi
            if (Utility.HealthBarCanvas.Instance != null)
            {
                Utility.HealthBarCanvas.Instance.ClearOrphaned();
            }

            // 4. Xóa sạch bộ nhớ đột phá AI
            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.ClearAll();
            }

            // 5. Cập nhật NavMesh
            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.NavMeshUpdatedByObstacles();
            }
        }

        public bool IsActiveMonster(Unit.Unit unit)
        {
            if (unit == null) return false;
            return ActiveMonsters.Contains(unit);
        }

        public static int GetMonsterCountForWave(int wave)
        {
            switch (wave)
            {
                case 1: return 13;
                case 2: return 17;
                case 3: return 23;
                case 4: return 27;
                case 5: return 31;
                case 6: return 35;
                case 7: return 39;
                case 8: return 43; // có 1 Bạo Chúa T-Rex
                case 9: return 47; // có 1 Bạo Chúa T-Rex
                case 10: return 37; // ĐẠI CHIẾN: có 2 Bạo Chúa T-Rex
                default: return Mathf.Clamp(10 + wave * 4, 13, 55);
            }
        }

        public static int GetTargetDefendersForWave(int wave)
        {
            // Quy định nghiêm ngặt 100% số lượng NPC theo mỗi màn (tăng số NPC theo yêu cầu người chơi):
            // Màn 1: 10 NPC (cũ: 6) | Màn 2: 13 NPC (cũ: 8) | Màn 3: 16 NPC (cũ: 10)
            // Màn 4: 19 NPC (cũ: 12) | Màn 5: 22 NPC (cũ: 14) | Màn 6: 25 NPC (cũ: 16)
            // Màn 7: 28 NPC (cũ: 18) | Màn 8: 31 NPC (cũ: 20) | Màn 9: 34 NPC (cũ: 22) | Màn 10+: 36 NPC (cũ: 24)
            return Mathf.Clamp(7 + wave * 3, 10, 36);
        }

        public void EnsureVillageDefendersActive()
        {
            if (PrehistoricGameModeManager.Instance == null || PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.TowerDefense)
            {
                // Ở chế độ Thủ Thành: Tuyệt đối không tự động sinh lính tự do trên cầu gỗ và sân làng!
                // Người chơi tự tay mua lính từ toolbar phím 5 nếu cần. Cầu gỗ phải hoàn toàn thông thoáng!
                var ai = LlamAcademy.Dinos.Enemy.EnemyAIController.Instance != null 
                    ? LlamAcademy.Dinos.Enemy.EnemyAIController.Instance 
                    : FindFirstObjectByType<LlamAcademy.Dinos.Enemy.EnemyAIController>();
                if (ai != null)
                {
                    ai.ClearAllDefenders(includePlayerPlaced: false);
                }
                return;
            }

            // Ở chế độ Công Thành (Dino Assault): Làng AI cần lính phòng thủ theo cấp độ màn
            var enemyAI = LlamAcademy.Dinos.Enemy.EnemyAIController.Instance != null 
                ? LlamAcademy.Dinos.Enemy.EnemyAIController.Instance 
                : FindFirstObjectByType<LlamAcademy.Dinos.Enemy.EnemyAIController>();
            if (enemyAI != null)
            {
                enemyAI.enabled = true;
                int assaultLvl = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance.AssaultLevel : _CurrentWave;
                int targetCount = GetTargetDefendersForWave(assaultLvl);
                enemyAI.ResetDefendersToExactCount(targetCount);
            }
        }

        /// <summary>
        /// Người dùng yêu cầu xóa bỏ 2 chòi canh thủ gỗ bị tàng hình.
        /// Hàm này dọn sạch mọi instance còn sót lại và thanh máu lơ lửng của chúng.
        /// </summary>
        public void EnsureVillageWatchtowers()
        {
            CleanupVillageWatchtowers();
        }

        public void CleanupVillageWatchtowers()
        {
            GameObject fortressRoot = GameObject.Find("Village_Fortress_Wall");
            if (fortressRoot != null)
            {
                Transform leftWt = fortressRoot.transform.Find("Village_Watchtower_Left");
                if (leftWt != null)
                {
                    if (leftWt.TryGetComponent(out Unit.Unit u) && HealthBarCanvas.HasInstance)
                    {
                        HealthBarCanvas.Instance.Unregister(u);
                    }
                    Destroy(leftWt.gameObject);
                }

                Transform rightWt = fortressRoot.transform.Find("Village_Watchtower_Right");
                if (rightWt != null)
                {
                    if (rightWt.TryGetComponent(out Unit.Unit u) && HealthBarCanvas.HasInstance)
                    {
                        HealthBarCanvas.Instance.Unregister(u);
                    }
                    Destroy(rightWt.gameObject);
                }
            }

            GameObject strayLt = GameObject.Find("Village_Watchtower_Left");
            if (strayLt != null)
            {
                if (strayLt.TryGetComponent(out Unit.Unit u) && HealthBarCanvas.HasInstance)
                {
                    HealthBarCanvas.Instance.Unregister(u);
                }
                Destroy(strayLt);
            }

            GameObject strayRt = GameObject.Find("Village_Watchtower_Right");
            if (strayRt != null)
            {
                if (strayRt.TryGetComponent(out Unit.Unit u) && HealthBarCanvas.HasInstance)
                {
                    HealthBarCanvas.Instance.Unregister(u);
                }
                Destroy(strayRt);
            }

            if (HealthBarCanvas.HasInstance)
            {
                var canvas = HealthBarCanvas.Instance;
                for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                {
                    Transform c = canvas.transform.GetChild(i);
                    if (c != null && (c.name.Contains("Village_Watchtower_Left") || c.name.Contains("Village_Watchtower_Right")))
                    {
                        Destroy(c.gameObject);
                    }
                }
            }
        }

        public void EnsureVillageFortressWallActive()
        {
            CleanupVillageWatchtowers();
            DeactivateDefaultSceneBarricades();
        }

        /// <summary>
        /// Tự động quét và kích hoạt toàn bộ các tường chắn / barricade mà người chơi tự đặt thêm vào map trong Editor.
        /// Đảm bảo đầy đủ BoxCollider, NavMeshObstacle (Carving = true) và Component Wall để khủng long không thể đi xuyên qua.
        /// </summary>
        public void ConfigureAllCustomBarricades()
        {
            // Dọn dẹp component Wall/NavMeshObstacle/BoxCollider nếu vô tình bị gán trên các container nhóm cha
            string[] containerNames = new string[] {
                "Village_Fortress_Wall", "Left_Wing_Wall", "Right_Wing_Wall", "Right_Flank_Wall",
                "Gate_Complex", "Gate_Entryway"
            };
            foreach (var cName in containerNames)
            {
                GameObject cObj = GameObject.Find(cName);
                if (cObj != null)
                {
                    if (cObj.TryGetComponent(out Unit.Wall errW)) Destroy(errW);
                    if (cObj.TryGetComponent(out BoxCollider errB)) Destroy(errB);
                    if (cObj.TryGetComponent(out UnityEngine.AI.NavMeshObstacle errO)) Destroy(errO);
                }
            }

            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var barricadeSO = Resources.Load<Config.TowerSO>("Config/Prehistoric/Tower_Barricade");
            if (barricadeSO == null) barricadeSO = Resources.Load<Config.TowerSO>("Tower_Barricade");

            int count = 0;
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name.ToLower();

                // Bỏ qua nếu là đối tượng tuyến 2 (Big Tower / Arch Entry) để EnsureVillageInteriorDefenses xử lý riêng
                if (n.Contains("big tower") || n.Contains("arch entry"))
                {
                    continue;
                }

                // 1. Bỏ qua các container rỗng hoặc đối tượng hệ thống không phải tường, và TUYỆT ĐỐI không biến cầu/đường đi thành tường
                if (n == "gate_complex" || n == "village_fortress_wall" || n == "left_wing_wall" || n == "right_wing_wall" || n == "right_flank_wall" ||
                    n.Contains("bridge") || (n.Contains("plank") && !n.Contains("defenses")) || n.Contains("road") || n.Contains("path") || n.Contains("walkway") ||
                    n.Contains("floor") || n.Contains("ground") || n.Contains("watchtower") ||
                    n.Contains("camera") || n.Contains("light") || n.Contains("spawn") ||
                    n.Contains("system") || n.Contains("event") || n.Contains("healthbar") ||
                    n.Contains("canvas") || n.Contains("ui") || n.Contains("dino") ||
                    n.Contains("player") || n.Contains("projectile") || n.Contains("trap") ||
                    n.Contains("pot") || n.Contains("vase") || n.Contains("flag") ||
                    n.Contains("egg") || n.Contains("skull") || n.Contains("spine") ||
                    n.Contains("totem") || n.Contains("hut") || n.Contains("mountain") ||
                    n.Contains("torch") || n.Contains("ramp") || n.Contains("particle") ||
                    n.Contains("look target") || n.Contains("navmesh") || n.Contains("vegetation") ||
                    n.Contains("overhead") || n.Contains("gap"))
                {
                    continue;
                }

                // 2. Bỏ qua nếu object là node con bên trong bất kỳ đối tượng Wall nào khác
                Unit.Wall parentWall = t.GetComponentInParent<Unit.Wall>(true);
                if (parentWall != null && parentWall.gameObject != t.gameObject)
                {
                    if (t.TryGetComponent(out Unit.Wall errWall)) Destroy(errWall);
                    if (t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle errObs)) Destroy(errObs);
                    continue;
                }

                // 3. Nhận diện các object rào chắn, tường thành thực sự (kể cả hàng rào sườn phải Plank_Defenses)
                bool hasWallTag = false;
                try { hasWallTag = t.CompareTag("Wall") || t.CompareTag("Obstacle"); } catch {}

                bool isWallCandidate = (n.Contains("barricade") || n.Contains("palisade") || n.Contains("fence") ||
                                       n.Contains("wooden__barricade") || n.Contains("wooden_barricade") ||
                                       n.Contains("plank_defenses") ||
                                       (n.Contains("wall") && !n.Contains("arch entry")) ||
                                       hasWallTag ||
                                       (t.GetComponent<Unit.Wall>() != null && !n.Contains("bridge") && (!n.Contains("plank") || n.Contains("defenses"))))
                                       && !n.Contains("big tower") && !n.Contains("arch entry");

                if (!isWallCandidate) continue;

                GameObject go = t.gameObject;

                // Chỉ cấu hình cho các tường do người chơi xây đặt trong trận (các bản Clone)
                // Các tường mặc định của cảnh đã bị xóa/tắt để quái không đi xuyên tường
                if (!go.name.Contains("(Clone)"))
                {
                    continue;
                }

                // 4. Tính toán bounds chính xác từ Renderer / Collider của go và các con cháu của nó
                Bounds combinedBounds = new Bounds();
                bool hasBounds = false;

                Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null || !r.enabled) continue;
                    if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                    if (!hasBounds)
                    {
                        combinedBounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(r.bounds);
                    }
                }

                if (!hasBounds)
                {
                    Collider[] childCols = go.GetComponentsInChildren<Collider>(true);
                    foreach (var c in childCols)
                    {
                        if (c == null || !c.enabled || (c is BoxCollider bc && bc.gameObject == go)) continue;
                        if (!hasBounds)
                        {
                            combinedBounds = c.bounds;
                            hasBounds = true;
                        }
                        else
                        {
                            combinedBounds.Encapsulate(c.bounds);
                        }
                    }
                }

                // Nếu không có renderer lẫn collider thì bỏ qua (node rỗng)
                if (!hasBounds && !go.TryGetComponent(out MeshFilter _)) continue;

                Vector3 localCenter;
                Vector3 localSize;

                if (hasBounds)
                {
                    localCenter = go.transform.InverseTransformPoint(combinedBounds.center);
                    Vector3 worldSize = combinedBounds.size;
                    Vector3 lossy = go.transform.lossyScale;

                    float absX = Mathf.Abs(lossy.x) > 0.0001f ? Mathf.Abs(lossy.x) : 1f;
                    float absY = Mathf.Abs(lossy.y) > 0.0001f ? Mathf.Abs(lossy.y) : 1f;
                    float absZ = Mathf.Abs(lossy.z) > 0.0001f ? Mathf.Abs(lossy.z) : 1f;

                    float lx = Mathf.Max(0.5f, worldSize.x / absX);
                    float ly = Mathf.Max(1.0f, worldSize.y / absY);
                    float lz = Mathf.Max(0.5f, worldSize.z / absZ);

                    // Đảm bảo độ dày tối thiểu 1.0m trong world space ở trục mỏng nhất để không bị lọt xuyên tường
                    if (lx * absX < 1.0f && lz * absZ >= lx * absX)
                    {
                        lx = 1.0f / absX;
                    }
                    else if (lz * absZ < 1.0f)
                    {
                        lz = 1.0f / absZ;
                    }

                    localSize = new Vector3(lx, ly, lz);
                }
                else
                {
                    if (go.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                    {
                        localCenter = mf.sharedMesh.bounds.center;
                        localSize = mf.sharedMesh.bounds.size;
                    }
                    else
                    {
                        localCenter = new Vector3(0f, 1f, 0f);
                        localSize = new Vector3(3f, 2f, 1.2f);
                    }
                }

                // 5. Vô hiệu hóa triệt để mọi NavMeshLink trên bức tường này để khủng long không thể đi tắt/nhảy xuyên qua
                var links = go.GetComponentsInChildren<Unity.AI.Navigation.NavMeshLink>(true);
                foreach (var link in links)
                {
                    if (link != null) link.enabled = false;
                }

                // 6. BoxCollider bao phủ toàn bộ bề mặt rào chắn / tường (bảo tồn nguyên vẹn cấu trúc nếu collider đã tồn tại)
                if (!go.TryGetComponent(out BoxCollider col))
                {
                    col = go.AddComponent<BoxCollider>();
                    col.center = localCenter;
                    col.size = localSize;
                }
                else if (col.size.sqrMagnitude < 0.01f)
                {
                    col.center = localCenter;
                    col.size = localSize;
                }
                col.enabled = true;

                // 7. NavMeshObstacle Carving được bật tức thì (carveOnlyStationary = false)
                // để NavMeshAgent của khủng long lập tức coi đây là chướng ngại vật cản tuyệt đối không thể đi xuyên qua
                if (!go.TryGetComponent(out UnityEngine.AI.NavMeshObstacle obs))
                {
                    obs = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                    obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                    obs.center = col.center;
                    obs.size = col.size;
                }
                else if (obs.size.sqrMagnitude < 0.01f)
                {
                    obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                    obs.center = col.center;
                    obs.size = col.size;
                }
                obs.carving = true;
                obs.carveOnlyStationary = false;
                obs.enabled = true;

                // 8. Đảm bảo có Component Wall (Unit) với lượng máu cọc gỗ gấp đôi (900 HP)
                if (!go.TryGetComponent(out Unit.Wall wall))
                {
                    wall = go.AddComponent<Unit.Wall>();
                }
                if (wall.MaxHealth <= 0 || wall.MaxHealth == 450 || wall.MaxHealth == 250)
                {
                    wall.MaxHealth = 900;
                    wall.Health = 900;
                }
                if (barricadeSO != null && wall.UnitType == null)
                {
                    wall.UnitType = barricadeSO;
                }
                wall.EnsureHealthBarAttached();
                count++;
            }

            if (count > 0)
            {
                Debug.Log($"<color=green>[Prehistoric TD]</color> Đã tích hợp thành công {count} bức tường chắn/pháo đài/rào chắn vào hệ thống phòng thủ & NavMesh cản quái!");
            }
        }

        /// <summary>
        /// Cấu hình tuyến phòng thủ thứ 2 của làng: Các cổng vòm (Arch Entry Wall) và chòi bộ lạc trung tâm (Big Tower with Platform).
        /// 1. Vòm cổng Arch Entry Wall: Tắt convex collider đang bịt kín vòm, thêm BoxCollider ở 2 cột trụ và sàn đi bộ trên cao,
        ///    để vòm cổng ở giữa rộng 5.4m hoàn toàn thông thoáng cho khủng long chạy thẳng vào làng mà không bị kẹt vô hình.
        /// 2. Gán Wall component (HP) cho Arch Entry và Big Tower để nếu quái húc trực diện thì có thể tấn công và phá hủy được.
        /// 3. Bỏ qua va chạm vật lý giữa khủng long và khủng long (Layer 8) để quái không bị chèn ép nêm chặt lẫn nhau.
        /// </summary>
        public void EnsureVillageInteriorDefenses()
        {
            Physics.IgnoreLayerCollision(8, 8, true);
            EnsureScenicFortressWalkways();
            EnsureVillageBridgesDestructible();
        }

        private static BoxCollider EnsureBoxCollider(GameObject go, Vector3 size, Vector3 center)
        {
            if (!go.TryGetComponent(out BoxCollider col)) col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.center = center;
            return col;
        }

        private static NavMeshObstacle EnsureNavMeshObstacle(GameObject go, Vector3 size, Vector3 center)
        {
            if (!go.TryGetComponent(out NavMeshObstacle obs)) obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            obs.size = size;
            obs.center = center;
            obs.carving = true;
            obs.carveOnlyStationary = true;
            return obs;
        }

        public void ResetPlayerAndCameraPosition()
        {
            if (Camera.main != null && _InitialCameraPos != Vector3.zero)
            {
                Camera.main.transform.position = _InitialCameraPos;
                Camera.main.transform.rotation = _InitialCameraRot;
            }

            if (_PlayerObj != null && _InitialPlayerPos != Vector3.zero)
            {
                _PlayerObj.transform.position = _InitialPlayerPos;
                _PlayerObj.transform.rotation = _InitialPlayerRot;
            }
        }

        /// <summary>
        /// Thoát game hoàn toàn (Dừng PlayMode trong Editor hoặc tắt ứng dụng trong bản build).
        /// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Reset và khôi phục toàn bộ ngôi làng:
        /// - Tường thành, rào chắn, chướng ngại vật (FullRestore)
        /// - Xóa bộ nhớ đột phá TacticalBreachMemory của khủng long
        /// - Cập nhật NavMesh
        /// - Căn cứ Làng (Base HP)
        /// - Làm mới và hồi sinh toàn bộ NPC dân làng / chiến binh bảo vệ làng
        /// - Nếu keepPlayerTowers = false: dọn tháp đã đặt và reset vàng về 500
        /// - Nếu keepPlayerTowers = true: hồi đầy 100% máu cho các tháp phòng thủ hiện có
        /// </summary>
        /// <summary>
        /// Dọn sạch thanh máu của tất cả các công trình cảnh quan map (chòi, lều, cọc rào cảnh) trong chế độ Thủ Thành,
        /// để tránh thanh máu ma xuất hiện lơ lửng trên màn hình (đặc biệt là góc trên bên phải dưới Mini-map).
        /// </summary>
        public void CleanAllScenicWallHealthBars()
        {
            if (Utility.HealthBarCanvas.HasInstance)
            {
                Utility.HealthBarCanvas.Instance.ClearOrphaned();
            }
        }

        public void ResetEntireVillage(bool keepPlayerTowers = true)
        {
            LocateBaseTarget();
            EnsureSpawnPoints();
            LoadDinoPrefabsIfNull();

            CleanupInvisibleAndStrayObjects();
            DeactivateDefaultSceneBarricades();

            bool isTowerDefense = (PrehistoricGameModeManager.Instance == null || 
                                   PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.TowerDefense);

            // 1. Phục hồi 100% tất cả tường do người chơi đặt (Clone) nếu giữ lại
            if (keepPlayerTowers)
            {
                var allWalls = FindObjectsByType<Unit.Wall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var wall in allWalls)
                {
                    if (wall != null && wall.gameObject.name.Contains("(Clone)"))
                    {
                        wall.FullRestore();
                    }
                }
            }

            // Đảm bảo vô hiệu hóa triệt để tường mặc định của cảnh (để quái không đi xuyên tường)
            DeactivateDefaultSceneBarricades();

            // Đảm bảo cấu hình va chạm và chướng ngại vật cho tường người chơi đặt
            ConfigureAllCustomBarricades();
            EnsureScenicFortressWalkways();
            EnsureVillageBridgesDestructible();

            // Phục hồi và đảm bảo 2 chòi canh trên bờ tường thành (Village_Watchtower_Left & Right)
            EnsureVillageWatchtowers();
            EnsureScenicBridgeWatchtowers();

            // Phục hồi và làm mới 100% toàn bộ hàng rào gỗ trước làng như mới
            RestoreAllVillageBarricades();

            // 2. Xóa sạch bộ nhớ đột phá (Tactical Breach Memory) của AI khủng long
            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.ClearAll();
            }

            // 3. Cập nhật NavMesh sau khi toàn bộ tường và chướng ngại vật được khôi phục
            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.NavMeshUpdatedByObstacles();
                NavMeshManager.Instance.RecalculateTriangulation(false);
            }

            // 4. Hồi phục 100% Căn Cứ Làng (Base HP)
            _CurrentBaseHealth = _MaxBaseHealth;
            if (Unit.PrehistoricVillageBase.Instance != null)
            {
                Unit.PrehistoricVillageBase.Instance.RestoreBase(_MaxBaseHealth);
            }
            OnBaseHealthChanged?.Invoke(_CurrentBaseHealth, _MaxBaseHealth);

            // 5. Xử lý các công trình phòng thủ của người chơi:
            if (!keepPlayerTowers)
            {
                // Xóa bỏ tất cả các vật thể người chơi đã đặt (tháp, bẫy, NPC warrior) và reset vàng theo màn hiện tại
                if (TowerPlacer.Instance != null)
                {
                    TowerPlacer.Instance.ClearAllPlacedTowers();
                    int waveGold = StartingGold + (_CurrentWave - 1) * 300;
                    TowerPlacer.Instance.ResetGoldToDefault(waveGold);
                    TowerPlacer.Instance.DeselectTower();
                }
            }
            else
            {
                // Giữa các wave (bàn mới): Hồi phục đầy đủ 100% máu cho các tháp phòng thủ của người chơi nếu bị hư hại
                var allUnits = FindObjectsByType<Unit.Unit>(FindObjectsSortMode.None);
                foreach (var u in allUnits)
                {
                    if (u == null) continue;
                    if (u is Unit.PrehistoricDinoBase || u is Unit.Dino || u is Unit.FlyingUnit) continue;
                    if (u is Unit.Wall || u is Unit.PrehistoricVillageBase) continue;
                    if (u.MaxHealth > 0 && u.Health < u.MaxHealth)
                    {
                        u.Health = u.MaxHealth;
                        u.EnsureHealthBarAttached();
                    }
                }
            }

            // 6. Xử lý NPC Chiến binh phòng thủ làng:
            var enemyAI = LlamAcademy.Dinos.Enemy.EnemyAIController.Instance != null 
                ? LlamAcademy.Dinos.Enemy.EnemyAIController.Instance 
                : FindFirstObjectByType<LlamAcademy.Dinos.Enemy.EnemyAIController>();

            if (isTowerDefense)
            {
                // Ở chế độ Thủ Thành: DỌN SẠCH TOÀN BỘ NPC CHIẾN BINH TỰ DO!
                // Tuyệt đối không sinh lính tự động dồn cục trên cầu gỗ!
                // Người chơi tự mua lính phòng thủ (Tribal Warrior 45g) từ Toolbar nếu muốn!
                if (enemyAI != null)
                {
                    enemyAI.ClearAllDefenders(includePlayerPlaced: keepPlayerTowers);
                }
            }
            else
            {
                // Ở chế độ Công Thành (Dino Assault): Làng AI sinh số lượng lính phòng thủ theo cấp độ màn
                if (enemyAI != null)
                {
                    enemyAI.enabled = true;
                    int assaultLvl = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance.AssaultLevel : _CurrentWave;
                    int defenderCount = GetTargetDefendersForWave(assaultLvl);
                    enemyAI.ResetDefendersToExactCount(defenderCount);
                }
            }

            // 7. Dọn sạch các thanh máu rác/mồ côi nếu có trên màn hình
            if (Utility.HealthBarCanvas.HasInstance)
            {
                Utility.HealthBarCanvas.Instance.ClearOrphaned();
            }

            Debug.Log($"<color=green>[Prehistoric TD]</color> Đã thiết lập lại (Reset) 100% ngôi làng cho Màn {_CurrentWave}: Mode={(isTowerDefense ? "Thủ Thành" : "Công Thành")}, Căn cứ ({_CurrentBaseHealth} HP)!");
        }

        public void FullResetGame()
        {
            Time.timeScale = 1.0f;

            // 1. Dừng mọi tiến trình Coroutine sinh quái
            if (WaveSpawnRoutine != null)
            {
                StopCoroutine(WaveSpawnRoutine);
                WaveSpawnRoutine = null;
            }
            StopAllCoroutines();

            // 2. Dọn sạch 100% khủng long trên toàn bộ bản đồ
            CleanAllGameplayGarbage();
            PrehistoricDinoBase.ClearClipsCache();

            // 3. Đặt lại Đợt chơi về Màn 1
            _CurrentWave = 1;

            // 4. Đồng bộ với RoundManager TRƯỚC KHI reset làng để dọn dẹp state và hủy các Invoke cũ
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.ResetRoundToStart();
                RoundManager.Instance.State = GameState.Setup;
            }

            // 5. Chơi lại từ đầu: Reset toàn bộ và chơi từ màn 1 (xóa vật thể người chơi đặt, 500 vàng, không lính rác)
            ResetEntireVillage(keepPlayerTowers: false);

            // 6. Đặt lại trạng thái về Pha Xây Dựng (Building Phase)
            SetPhase(PrehistoricWavePhase.BuildingPhase);

            // 7. Đặt lại vị trí ban đầu của Camera và Nhân vật
            ResetPlayerAndCameraPosition();
            if (CameraControl.Instance != null)
            {
                CameraControl.Instance.SetPerspective(CameraPerspective.Rear_TowerDefense);
                CameraControl.Instance.ResetToCenter();
            }

            ShowAnnouncement("⚔ CHƠI LẠI TỪ ĐẦU (MÀN 1)! HÃY XÂY DỰNG LẠI PHÒNG TUYẾN! ⚔", 4.0f);
        }

        public void RetryCurrentWave()
        {
            Time.timeScale = 1.0f;

            if (WaveSpawnRoutine != null)
            {
                StopCoroutine(WaveSpawnRoutine);
                WaveSpawnRoutine = null;
            }
            StopAllCoroutines();

            CleanAllGameplayGarbage();
            PrehistoricDinoBase.ClearClipsCache();

            // Đồng bộ với RoundManager TRƯỚC KHI reset làng
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.SetCurrentRound(_CurrentWave);
                RoundManager.Instance.State = GameState.Setup;
            }

            // Chơi lại màn X: Reset hoàn toàn theo màn (xóa quái, xóa tháp cũ, xóa lính rác, reset vàng chuẩn cho màn X)
            ResetEntireVillage(keepPlayerTowers: false);

            SetPhase(PrehistoricWavePhase.BuildingPhase);

            ResetPlayerAndCameraPosition();
            if (CameraControl.Instance != null)
            {
                CameraControl.Instance.SetPerspective(CameraPerspective.Rear_TowerDefense);
                CameraControl.Instance.ResetToCenter();
            }

            int waveGold = StartingGold + (_CurrentWave - 1) * 300;
            ShowAnnouncement($"🔄 ĐÃ RESET HOÀN TOÀN MÀN {_CurrentWave}! BẠN CÓ {waveGold}g ĐỂ DÀN TRẬN MỚI! BẤM SPACE ĐỂ BẮT ĐẦU! ⚔", 4.0f);
        }

        public void RestartGame()
        {
            FullResetGame();
        }

        public void StartNewDefenseGameFromMenu()
        {
            if (PrehistoricGameModeManager.Instance != null)
            {
                PrehistoricGameModeManager.Instance.SetGameMode(PrehistoricGameMode.TowerDefense);
            }

            LocateBaseTarget();
            EnsureSpawnPoints();
            LoadDinoPrefabsIfNull();

            FullResetGame();

            if (Save.PrehistoricSaveManager.Instance != null)
            {
                var prof = Save.PrehistoricSaveManager.Instance.CurrentData.DefenseProfile;
                if (prof != null)
                {
                    prof.CurrentWave = 1;
                    prof.Gold = StartingGold;
                    prof.BaseHealth = _MaxBaseHealth;
                    prof.HasActiveRun = true;
                }
                Save.PrehistoricSaveManager.Instance.AutoSaveDefenseRun(true);
            }

            Audio.PrehistoricAudioManager.Instance?.PlayBgmForMode(PrehistoricGameMode.TowerDefense);
            ShowAnnouncement("⚔ BẮT ĐẦU CHIẾN DỊCH THỦ THÀNH MỚI: ĐỢT 1! ⚔", 4.0f);
        }

        public void LoadDefenseWave(int targetWave, int savedGold, int savedHp)
        {
            if (PrehistoricGameModeManager.Instance != null)
            {
                PrehistoricGameModeManager.Instance.SetGameMode(PrehistoricGameMode.TowerDefense);
            }

            LocateBaseTarget();
            EnsureSpawnPoints();
            LoadDinoPrefabsIfNull();

            Time.timeScale = 1.0f;
            if (WaveSpawnRoutine != null)
            {
                StopCoroutine(WaveSpawnRoutine);
                WaveSpawnRoutine = null;
            }
            StopAllCoroutines();

            CleanAllGameplayGarbage();
            PrehistoricDinoBase.ClearClipsCache();

            _CurrentWave = Mathf.Clamp(targetWave, 1, _MaxWaves);
            _CurrentBaseHealth = savedHp > 0 ? savedHp : _MaxBaseHealth;

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.SetCurrentRound(_CurrentWave);
            }

            ResetEntireVillage(keepPlayerTowers: false);

            if (Player.TowerPlacer.Instance != null)
            {
                int finalGold = savedGold > 0 ? savedGold : (StartingGold + (_CurrentWave - 1) * 300);
                Player.TowerPlacer.Instance.ResetGoldToDefault(finalGold);
            }

            SetPhase(PrehistoricWavePhase.BuildingPhase);
            ResetPlayerAndCameraPosition();

            if (UI.PrehistoricMainMenu.Instance != null)
            {
                UI.PrehistoricMainMenu.Instance.IsOpen = false;
            }

            Audio.PrehistoricAudioManager.Instance?.PlayBgmForMode(PrehistoricGameMode.TowerDefense);
            ShowAnnouncement($"⚔ ĐÃ TẢI ĐỢT {_CurrentWave}: CHUẨN BỊ CHIẾN ĐẤU! ⚔", 4.0f);
        }

        public void ContinueDefenseFromMenu()
        {
            int targetWave = 1;
            int savedGold = StartingGold;
            int savedHp = _MaxBaseHealth;

            if (Save.PrehistoricSaveManager.Instance != null)
            {
                Save.PrehistoricSaveManager.Instance.LoadSaveData();
                var prof = Save.PrehistoricSaveManager.Instance.CurrentData.DefenseProfile;
                if (prof != null)
                {
                    targetWave = Mathf.Clamp(prof.CurrentWave, 1, _MaxWaves);
                    if (prof.Gold > 0) savedGold = prof.Gold;
                    if (prof.BaseHealth > 0) savedHp = prof.BaseHealth;
                }
            }

            LoadDefenseWave(targetWave, savedGold, savedHp);
        }

        public void StartNewGameFromMenu()
        {
            StartNewDefenseGameFromMenu();
        }

        public void ContinueGameFromMenu()
        {
            ContinueDefenseFromMenu();
        }

                public void ReturnToMainMenu()
        {
            Time.timeScale = 1.0f;
            if (WaveSpawnRoutine != null)
            {
                StopCoroutine(WaveSpawnRoutine);
                WaveSpawnRoutine = null;
            }
            StopAllCoroutines();

            // LƯU TIẾN TRÌNH CHÍNH XÁC CỦA CHẾ ĐỘ ĐANG CHƠI TRƯỚC KHI DỌN DẸP
            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: true);

            CleanAllGameplayGarbage();
            PrehistoricDinoBase.ClearClipsCache();

            if (PrehistoricGameModeManager.Instance != null)
            {
                PrehistoricGameModeManager.Instance.ClearAllDinos();
            }

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Setup;
            }

            if (UI.PrehistoricSettingsMenu.Instance != null)
            {
                UI.PrehistoricSettingsMenu.Instance.IsOpen = false;
            }

            if (UI.PrehistoricMainMenu.Instance != null)
            {
                UI.PrehistoricMainMenu.Instance.IsOpen = true;
            }

            Audio.PrehistoricAudioManager.Instance?.PlayOpeningBgm();
        }

        public void ShowAnnouncement(string message, float duration = 3.0f)
        {
            ScreenAnnouncement = message;
            AnnouncementTimer = duration;
        }

        private void OnGUI()
        {
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
            {
                return;
            }

            if (UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen)
            {
                return;
            }
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode != PrehistoricGameMode.TowerDefense)
            {
                return;
            }

            Mobile.MobileUIHelper.BeginScaling();
            try
            {
                DrawTopLeftStatusBanner();
                DrawWaveActionControl();
                DrawScreenAnnouncement();

                if (_CurrentPhase != PrehistoricWavePhase.GameOver && _CurrentPhase != PrehistoricWavePhase.Victory && _CurrentPhase != PrehistoricWavePhase.WaveCleared)
                {
                    DrawTowerPlacementToolbar();
                }

                if (_CurrentWave >= 8 && _CurrentPhase == PrehistoricWavePhase.CombatPhase)
                {
                    var aliveBosses = FindObjectsByType<BossDino>(FindObjectsSortMode.None);
                    bool hasAliveBoss = false;
                    for (int b = 0; b < aliveBosses.Length; b++)
                    {
                        if (aliveBosses[b] != null && aliveBosses[b].Health > 0 && aliveBosses[b].gameObject.activeInHierarchy)
                        {
                            hasAliveBoss = true;
                            break;
                        }
                    }
                    if (hasAliveBoss)
                    {
                        DrawBossHealthBar();
                    }
                }

                if (_CurrentPhase == PrehistoricWavePhase.GameOver)
                {
                    DrawGameOverModal();
                }
                else if (_CurrentPhase == PrehistoricWavePhase.Victory)
                {
                    DrawVictoryModal();
                }
                else if (_CurrentPhase == PrehistoricWavePhase.WaveCleared)
                {
                    DrawWaveVictoryModal();
                }
            }
            finally
            {
                Mobile.MobileUIHelper.EndScaling();
            }
        }

        private void DrawTowerPlacementToolbar()
        {
            if (TowerPlacer.Instance == null || TowerPlacer.Instance.AvailableTowers == null || TowerPlacer.Instance.AvailableTowers.Count == 0) return;

            var towers = TowerPlacer.Instance.AvailableTowers;
            int gold = TowerPlacer.Instance.Gold;

            float screenW = Mobile.MobileUIHelper.ScreenWidth;
            float screenH = Mobile.MobileUIHelper.ScreenHeight;

            // Kích thước chuẩn từng nút công trình đảm bảo text không bao giờ bị cắt cụt
            float slotWidth = 118f;
            float slotHeight = 56f;
            float spacing = 6f;

            // Tổng chiều dài nội dung cần để chứa toàn bộ danh sách tháp/NPC
            float totalContentWidth = towers.Count * (slotWidth + spacing) + 12f;

            // Chiều rộng khung chứa toolbar ở đáy màn hình
            float toolbarWidth = Mathf.Min(screenW - 20f, totalContentWidth + 24f);
            float toolbarHeight = 80f;
            float x = (screenW - toolbarWidth) / 2f;
            float y = screenH - toolbarHeight - 10f;

            Rect toolbarRect = new Rect(x, y, toolbarWidth, toolbarHeight);
            Mobile.MobileUIHelper.RegisterUIRect(toolbarRect);

            // Kiểm tra xem danh sách có cần cuộn không
            bool needsScroll = totalContentWidth > (toolbarWidth - 20f);
            float maxScrollX = Mathf.Max(0f, totalContentWidth - (toolbarWidth - (needsScroll ? 78f : 20f)));

            // Xử lý sự kiện kéo chuột / chạm vuốt (Drag to Scroll) và cuộn con lăn chuột (Mouse Scrollwheel)
            Event currentEvent = Event.current;
            if (currentEvent != null)
            {
                if (currentEvent.type == EventType.ScrollWheel && toolbarRect.Contains(currentEvent.mousePosition))
                {
                    _ToolbarScrollPosition.x = Mathf.Clamp(_ToolbarScrollPosition.x + currentEvent.delta.y * 45f, 0f, maxScrollX);
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.MouseDown && toolbarRect.Contains(currentEvent.mousePosition))
                {
                    _IsDraggingToolbar = true;
                    _ToolbarDragStartMouseX = currentEvent.mousePosition.x;
                    _ToolbarDragStartScrollX = _ToolbarScrollPosition.x;
                }
                else if (currentEvent.type == EventType.MouseDrag && _IsDraggingToolbar)
                {
                    float delta = currentEvent.mousePosition.x - _ToolbarDragStartMouseX;
                    _ToolbarScrollPosition.x = Mathf.Clamp(_ToolbarDragStartScrollX - delta, 0f, maxScrollX);
                }
                else if (currentEvent.rawType == EventType.MouseUp)
                {
                    _IsDraggingToolbar = false;
                }
            }

            // Vẽ khung nền toolbar thanh lịch, mờ nhẹ
            GUI.backgroundColor = new Color(0.1f, 0.14f, 0.2f, 0.95f);
            GUI.Box(toolbarRect, "");
            GUI.backgroundColor = Color.white;

            // Nếu danh sách bị tràn, vẽ thêm 2 nút mũi tên [◀] và [▶] hai bên để bấm cuộn cực kỳ tiện lợi trên cả PC và Mobile
            float leftArrowWidth = needsScroll ? 28f : 0f;
            float rightArrowWidth = needsScroll ? 28f : 0f;
            float scrollAreaWidth = toolbarWidth - leftArrowWidth - rightArrowWidth - 8f;

            if (needsScroll)
            {
                Rect leftBtnRect = new Rect(x + 4, y + (toolbarHeight - slotHeight) / 2f, leftArrowWidth - 2, slotHeight);
                GUI.backgroundColor = _ToolbarScrollPosition.x > 5f ? new Color(0.3f, 0.7f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                if (GUI.Button(leftBtnRect, "<b>◀</b>"))
                {
                    _ToolbarScrollPosition.x = Mathf.Max(0f, _ToolbarScrollPosition.x - (slotWidth + spacing));
                }

                Rect rightBtnRect = new Rect(x + toolbarWidth - rightArrowWidth - 2, y + (toolbarHeight - slotHeight) / 2f, rightArrowWidth - 2, slotHeight);
                GUI.backgroundColor = _ToolbarScrollPosition.x < maxScrollX - 5f ? new Color(0.3f, 0.7f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                if (GUI.Button(rightBtnRect, "<b>▶</b>"))
                {
                    _ToolbarScrollPosition.x = Mathf.Min(maxScrollX, _ToolbarScrollPosition.x + (slotWidth + spacing));
                }
                GUI.backgroundColor = Color.white;
            }

            // Vùng ScrollView chứa danh sách các tháp / NPC
            Rect scrollAreaRect = new Rect(x + leftArrowWidth + 4, y + 6, scrollAreaWidth, toolbarHeight - 12);
            Rect viewContentRect = new Rect(0, 0, totalContentWidth, slotHeight + 6);

            _ToolbarScrollPosition = GUI.BeginScrollView(scrollAreaRect, _ToolbarScrollPosition, viewContentRect, false, false);

            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                if (t == null) continue;

                bool canAfford = gold >= t.Cost;
                bool isSelected = TowerPlacer.Instance.ActiveTower == t;

                GUI.enabled = canAfford || isSelected;

                if (isSelected) GUI.backgroundColor = new Color(0.2f, 0.95f, 0.35f, 1f);
                else if (!canAfford) GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.75f);
                else GUI.backgroundColor = Color.white;

                string hotkeyStr = (!Mobile.MobileUIHelper.IsMobile && i < 9) ? $"[{i + 1}] " : "";
                string label = $"{hotkeyStr}<b>{t.DisplayName}</b>\n<color=#FFD700>{t.Cost}g</color>";

                Rect btnRect = new Rect(i * (slotWidth + spacing) + 4, 3, slotWidth, slotHeight);

                if (GUI.Button(btnRect, label))
                {
                    if (isSelected)
                    {
                        TowerPlacer.Instance.DeselectTower();
                    }
                    else
                    {
                        TowerPlacer.Instance.SelectTower(t);
                        Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
                    }
                }
            }

            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
            GUI.EndScrollView();

            if (TowerPlacer.Instance.ActiveTower != null && !Mobile.MobileUIHelper.IsMobile)
            {
                float tipW = 500f;
                float tipH = 30f;
                float tipX = (screenW - tipW) / 2f;
                float tipY = y - tipH - 4f;

                GUI.backgroundColor = new Color(0f, 0f, 0f, 0.88f);
                GUI.Box(new Rect(tipX, tipY, tipW, tipH), $"<size=12><color=#55FF55>Đang chọn: <b>{TowerPlacer.Instance.ActiveTower.DisplayName}</b> | [Chuột Trái] Đặt | [Shift] Đặt nhiều | [Chuột Phải/ESC] Hủy</color></size>");
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawTopLeftStatusBanner()
        {
            int gold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 0;
            float hpPercent = Mathf.Clamp01((float)_CurrentBaseHealth / _MaxBaseHealth);

            Rect bannerRect = new Rect(20, 20, 360, 140);
            Mobile.MobileUIHelper.RegisterUIRect(bannerRect);

            GUILayout.BeginArea(bannerRect, GUI.skin.box);
            GUILayout.BeginVertical();

            // 1. Village Base Health Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b><size=15>MÁU LÀNG:</size></b>", GUILayout.Width(90));
            Color hpColor = hpPercent > 0.5f ? Color.green : (hpPercent > 0.25f ? Color.yellow : Color.red);
            GUI.color = hpColor;
            GUILayout.Label($"<b><size=16>{_CurrentBaseHealth} / {_MaxBaseHealth} HP</size></b>");
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            // Progress bar
            Rect barRect = GUILayoutUtility.GetRect(340, 16);
            GUI.Box(barRect, "");
            Rect fillRect = new Rect(barRect.x + 1, barRect.y + 1, (barRect.width - 2) * hpPercent, barRect.height - 2);
            GUI.color = hpColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Space(6);

            // 2. Gold & Wave Stats
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b><size=16><color=#FFD700>VÀNG: {gold}g</color></size></b>", GUILayout.Width(160));
            GUILayout.Label($"<b><size=16><color=#00FFFF>ĐỢT: {_CurrentWave} / {_MaxWaves}</color></size></b>");
            GUILayout.EndHorizontal();

            // 3. Alive Monsters Counter
            if (_CurrentPhase == PrehistoricWavePhase.CombatPhase)
            {
                GUILayout.Label($"<color=#FF5555>Quái còn sống: <b>{MonstersAliveCount} / {TotalMonstersInWave}</b></color>");
            }
            else
            {
                GUILayout.Label("<color=#55FF55>Trạng thái: Giai đoạn xây dựng (Pha chuẩn bị)</color>");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawWaveActionControl()
        {
            // Bố trí thanh điều khiển góc phải thông minh (Dưới Minimap Radar, hoàn toàn không bị chồng lấn)
            float dockWidth = 165f;
            float x = Mobile.MobileUIHelper.ScreenWidth - dockWidth - 16f;
            float currentY = 208f;

            // 1. Nút Hành Động Chính: Bắt đầu đợt / Trạng thái chiến đấu
            if (_CurrentPhase == PrehistoricWavePhase.BuildingPhase)
            {
                float btnHeight = 46f;
                Rect waveRect = new Rect(x, currentY, dockWidth, btnHeight);
                Mobile.MobileUIHelper.RegisterUIRect(waveRect);

                GUI.backgroundColor = new Color(0.12f, 0.85f, 0.35f, 1f);
                string btnLabel = Mobile.MobileUIHelper.IsMobile 
                    ? $"<b><size=14>▶ BẮT ĐẦU ĐỢT {_CurrentWave}\n(CHẠM XUẤT TRẬN)</size></b>"
                    : $"<b><size=14>▶ BẮT ĐẦU ĐỢT {_CurrentWave}\n(PHÍM SPACE / BẤM)</size></b>";

                if (GUI.Button(waveRect, btnLabel))
                {
                    StartNextWave();
                    Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
                }
                GUI.backgroundColor = Color.white;
                currentY += btnHeight + 6f;
            }
            else if (_CurrentPhase == PrehistoricWavePhase.CombatPhase)
            {
                float boxHeight = 40f;
                Rect combatRect = new Rect(x, currentY, dockWidth, boxHeight);
                Mobile.MobileUIHelper.RegisterUIRect(combatRect);

                GUI.backgroundColor = new Color(0.85f, 0.22f, 0.15f, 0.92f);
                GUI.Box(combatRect, $"<b><size=13><color=white>⚔ ĐANG CHIẾN ĐẤU ⚔\nQuái còn: {MonstersAliveCount}</color></size></b>");
                GUI.backgroundColor = Color.white;
                currentY += boxHeight + 6f;
            }

            // 2. Nút Bật/Tắt AI Director (Explainable AI Monitor)
            float btnH = 28f;
            Rect aiRect = new Rect(x, currentY, dockWidth, btnH);
            Mobile.MobileUIHelper.RegisterUIRect(aiRect);

            bool isAiOpen = UI.PrehistoricNeuralMonitorUI.Instance != null && UI.PrehistoricNeuralMonitorUI.Instance.IsOpen;
            GUI.backgroundColor = isAiOpen ? new Color(0.2f, 0.9f, 0.6f, 0.95f) : new Color(0.12f, 0.28f, 0.36f, 0.88f);

            string aiLabel = isAiOpen ? "<b>🧠 AI Monitor: [BẬT]</b>" : "<b>🧠 AI Director [F1]</b>";
            if (GUI.Button(aiRect, aiLabel))
            {
                UI.PrehistoricNeuralMonitorUI.Instance?.ToggleWindow();
            }
            currentY += btnH + 6f;

            // 3. Nút Chuyển Đổi Chế Độ AR Tabletop
            Rect arRect = new Rect(x, currentY, dockWidth, btnH);
            Mobile.MobileUIHelper.RegisterUIRect(arRect);

            bool isArActive = AR.ARTabletopController.Instance != null && AR.ARTabletopController.Instance.IsARModeActive;
            GUI.backgroundColor = isArActive ? new Color(0.9f, 0.4f, 0.2f, 0.95f) : new Color(0.15f, 0.35f, 0.75f, 0.88f);

            string arLabel = isArActive ? "<b>🔙 THOÁT AR (3D)</b>" : "<b>📱 CHẾ ĐỘ AR</b>";
            if (GUI.Button(arRect, arLabel))
            {
                AR.ARTabletopController.Instance?.ToggleARMode();
            }
            currentY += btnH + 6f;

            // 4. Nút Toàn Màn Hình [F11] (Chỉ hiển thị trên PC, tự động ẩn trên Mobile)
            if (!Mobile.MobileUIHelper.IsMobile)
            {
                Rect fsRect = new Rect(x, currentY, dockWidth, btnH);
                Mobile.MobileUIHelper.RegisterUIRect(fsRect);

                GUI.backgroundColor = Screen.fullScreen ? new Color(0.2f, 0.7f, 0.9f, 0.85f) : new Color(0.22f, 0.24f, 0.28f, 0.88f);
                string fsLabel = Screen.fullScreen ? "<b>⛶ CỬA SỔ [F11]</b>" : "<b>⛶ FULLSCREEN [F11]</b>";
                if (GUI.Button(fsRect, fsLabel))
                {
                    ToggleFullscreen();
                }
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawScreenAnnouncement()
        {
            if (string.IsNullOrEmpty(ScreenAnnouncement)) return;

            float w = Mathf.Min(650f, Mobile.MobileUIHelper.ScreenWidth - 40f);
            float h = 60f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - w) / 2f;
            float y = 75f;

            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Box(new Rect(x, y, w, h), $"<b><size=18>{ScreenAnnouncement}</size></b>");
            GUI.backgroundColor = Color.white;
        }

        private void DrawBossHealthBar()
        {
            float w = Mathf.Min(550f, Mobile.MobileUIHelper.ScreenWidth - 40f);
            float h = 55f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - w) / 2f;
            float y = 140f;

            float bossPercent = Mathf.Clamp01((float)ActiveBossInstance.Health / ActiveBossInstance.MaxHealth);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f, 0.9f);
            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            GUILayout.BeginVertical();

            GUILayout.Label($"<b><size=15><color=#FF2222>👑 BẠO CHÚA T-REX (APEX BOSS) - {ActiveBossInstance.Health} / {ActiveBossInstance.MaxHealth} HP</color></size></b>", GUILayout.Width(w));

            Rect bRect = GUILayoutUtility.GetRect(w - 20, 18);
            GUI.Box(bRect, "");
            Rect fRect = new Rect(bRect.x + 1, bRect.y + 1, (bRect.width - 2) * bossPercent, bRect.height - 2);
            GUI.color = Color.red;
            GUI.DrawTexture(fRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }

        private void DrawGameOverModal()
        {
            GUI.depth = -1000;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Lớp phủ nền mờ đen toàn màn hình
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Mobile.MobileUIHelper.ScreenWidth, Mobile.MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float modalW = Mathf.Min(740f, Mobile.MobileUIHelper.ScreenWidth - 24f);
            float modalH = 360f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - modalW) / 2f;
            float y = (Mobile.MobileUIHelper.ScreenHeight - modalH) / 2f;

            Rect modalRect = new Rect(x, y, modalW, modalH);
            Mobile.MobileUIHelper.RegisterUIRect(modalRect);

            GUI.backgroundColor = new Color(0.28f, 0.05f, 0.05f, 0.98f);
            GUI.Box(modalRect, "");

            GUILayout.BeginArea(new Rect(x + 20, y + 16, modalW - 40, modalH - 32));
            GUILayout.BeginVertical();

            GUILayout.Label("<b><size=22><color=#FF4444>☠ THẤT THỦ! LÀNG TIỀN SỬ ĐÃ BỊ PHÁ HỦY ☠</color></size></b>", GUILayout.ExpandWidth(true));
            GUILayout.Space(8);
            GUILayout.Label($"<size=16>Bầy khủng long hung hãn đã tràn qua tuyến phòng thủ ở <b>Đợt {_CurrentWave} / {_MaxWaves}</b>!</size>");
            GUILayout.Label("<size=14><color=#E0E0E0>Hệ thống phòng thủ làng (Căn cứ, Tháp) sẵn sàng thiết lập lại toàn bộ cho màn mới.</color></size>");
            GUILayout.Space(18);

            // 4 nút lựa chọn rõ ràng: Thử lại đợt này / Lưu game chọn khe / Chơi từ Wave 1 / Về Menu
            GUILayout.BeginHorizontal();

            float btnW = (modalW - 65) / 4f;

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f, 1f);
            if (GUILayout.Button($"<b><size=14>🔄 CHƠI LẠI\nĐỢT {_CurrentWave}</size></b>", GUILayout.Height(64), GUILayout.Width(btnW)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                RetryCurrentWave();
            }

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.7f, 1f);
            if (GUILayout.Button("<b><size=14>💾 LƯU GAME\n(CHỌN KHE)</size></b>", GUILayout.Height(64), GUILayout.Width(btnW)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                UI.PrehistoricSaveLoadDialog.EnsureInstance().OpenSaveDialog(PrehistoricGameMode.TowerDefense);
            }

            GUI.backgroundColor = new Color(0.95f, 0.55f, 0.15f, 1f);
            if (GUILayout.Button("<b><size=14>⏮️ CHƠI LẠI\nTỪ ĐẦU</size></b>", GUILayout.Height(64), GUILayout.Width(btnW)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                FullResetGame();
            }

            GUI.backgroundColor = new Color(0.2f, 0.65f, 0.95f, 1f);
            if (GUILayout.Button("<b><size=14>🏠 MENU\nCHÍNH</size></b>", GUILayout.Height(64), GUILayout.Width(btnW)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                ReturnToMainMenu();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }

        private void DrawWaveVictoryModal()
        {
            GUI.depth = -1000;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Mobile.MobileUIHelper.ScreenWidth, Mobile.MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(740f, Mobile.MobileUIHelper.ScreenWidth - 24f);
            float height = 400f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - width) / 2f;
            float y = (Mobile.MobileUIHelper.ScreenHeight - height) / 2f;

            Rect modalRect = new Rect(x, y, width, height);
            Mobile.MobileUIHelper.RegisterUIRect(modalRect);

            GUILayout.BeginArea(modalRect, GUI.skin.window);
            GUILayout.Space(8);
            GUILayout.Label($"<size=22><b><color=#FFD700>🏆 CHIẾN THẮNG ĐỢT {_CurrentWave}! 🏆</color></b></size>", GUI.skin.label);
            int rewardGold = WaveCompletionBaseGold + (_CurrentWave * 35);
            GUILayout.Label($"<size=15>Bảo vệ làng thành công! Thưởng đợt: <b><color=#55FF55>+{rewardGold} Vàng</color></b> | Căn cứ còn: <b>{_CurrentBaseHealth} / {_MaxBaseHealth} HP</b></size>");
            GUILayout.Space(10);

            int nextWave = _CurrentWave + 1;
            int nextMonsters = GetMonsterCountForWave(nextWave);
            int nextDefenders = GetTargetDefendersForWave(nextWave);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<size=14><b><color=#55FF55>📊 THÔNG SỐ ĐỘ KHÓ ĐỢT TIẾP THEO (ĐỢT {nextWave} / {_MaxWaves}):</color></b></size>");
            GUILayout.Label($"• <b>Bầy khủng long tấn công</b>: {nextMonsters} con (Độ hung hãn và tốc độ gia tăng)\n" +
                            $"• <b>Phòng tuyến Làng</b>: Căn cứ phục hồi 100% HP\n" +
                            $"• <b>Chiến lược</b>: Sử dụng vàng vừa nhận để xây thêm tháp, đặt bẫy và chiêu mộ chiến binh!");
            GUILayout.EndVertical();

            GUILayout.Space(14);
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
            if (GUILayout.Button("<b><size=14>⚔️ MÀN TIẾP THEO\n(ĐỢT " + nextWave + ")</size></b>", GUILayout.Height(55)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                ProceedToNextWave();
            }

            GUI.backgroundColor = new Color(0.9f, 0.5f, 0.15f);
            if (GUILayout.Button($"<b><size=14>🔄 CHƠI LẠI ĐỢT {_CurrentWave}</size></b>", GUILayout.Height(55)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                RetryCurrentWave();
            }

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.7f);
            if (GUILayout.Button("<b><size=14>💾 LƯU GAME\n(CHỌN KHE)</size></b>", GUILayout.Height(55)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                UI.PrehistoricSaveLoadDialog.EnsureInstance().OpenSaveDialog(PrehistoricGameMode.TowerDefense);
            }

            GUI.backgroundColor = new Color(0.2f, 0.65f, 0.95f);
            if (GUILayout.Button("<b><size=14>🏠 MENU CHÍNH\n(LƯU TIẾN TRÌNH)</size></b>", GUILayout.Height(55)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: true);
                ReturnToMainMenu();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawVictoryModal()
        {
            GUI.depth = -1000;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Lớp phủ nền mờ
            GUI.color = new Color(0f, 0.08f, 0.04f, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, Mobile.MobileUIHelper.ScreenWidth, Mobile.MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float modalW = Mathf.Min(740f, Mobile.MobileUIHelper.ScreenWidth - 24f);
            float modalH = 410f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - modalW) / 2f;
            float y = (Mobile.MobileUIHelper.ScreenHeight - modalH) / 2f;

            Rect modalRect = new Rect(x, y, modalW, modalH);
            Mobile.MobileUIHelper.RegisterUIRect(modalRect);

            GUI.backgroundColor = new Color(0.06f, 0.24f, 0.12f, 0.98f);
            GUI.Box(modalRect, "");

            GUILayout.BeginArea(new Rect(x + 24, y + 18, modalW - 48, modalH - 36));
            GUILayout.BeginVertical();

            GUILayout.Label("<b><size=24><color=#FFD700>🏆 CHIẾN THẮNG KHẢI HOÀN - HOÀN THÀNH TRÒ CHƠI! 🏆</color></size></b>", GUILayout.ExpandWidth(true));
            GUILayout.Space(8);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<size=16><b>🎉 Chúc mừng bạn đã phá đảo Màn 10 và hoàn thành xuất sắc trò chơi!</b></size>");
            GUILayout.Space(4);
            if (Environment.PrehistoricMapManager.CurrentMap == Environment.PrehistoricMapType.AncientValley)
            {
                GUILayout.Label("<size=14><color=#FF9900><b>🔥 ĐÃ MỞ KHÓA BẢN ĐỒ MỚI: NÚI DUNG NHAM (VOLCANIC GORGE)!</b></color></size>");
                GUILayout.Label("<size=12><color=#FFE0B2>Khủng long tại Núi Dung Nham đã biến dị thành quái vật dung nham rực lửa (Magma Explosion, Obsidian Armor, Shockwave)!</color></size>");
            }
            else
            {
                GUILayout.Label("<size=14><color=#00FFAA><b>✨ Bạn đã hoàn thành trò chơi và chiến thắng tại vùng đất hiểm trở!</b></color></size>");
                GUILayout.Label("<size=12><color=#E0FFE8>Cảm ơn bạn đã đồng hành và chỉ huy bảo vệ bộ lạc tiền sử vượt qua các đợt tiến công khốc liệt.</color></size>");
            }
            GUILayout.Space(4);
            GUILayout.Label($"<size=13>Ngôi làng tiền sử được bảo vệ kiên cố với <b>{_CurrentBaseHealth} / {_MaxBaseHealth} HP</b> còn lại!</size>");
            GUILayout.EndVertical();

            GUILayout.Space(14);

            GUILayout.BeginHorizontal();

            float btnW = (modalW - 65) / 4f;

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f, 1f);
            if (GUILayout.Button("<b><size=14>🎉 CHƠI LẠI\nTỪ ĐẦU</size></b>", GUILayout.Height(62), GUILayout.Width(btnW)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                FullResetGame();
            }

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.7f, 1f);
            if (GUILayout.Button("<b><size=14>💾 LƯU GAME\n(CHỌN KHE)</size></b>", GUILayout.Height(62), GUILayout.Width(btnW)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                UI.PrehistoricSaveLoadDialog.EnsureInstance().OpenSaveDialog(PrehistoricGameMode.TowerDefense);
            }

            GUI.backgroundColor = new Color(0.2f, 0.65f, 0.95f, 1f);
            if (GUILayout.Button("<b><size=14>🏠 MENU\nCHÍNH</size></b>", GUILayout.Height(62), GUILayout.Width(btnW)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                ReturnToMainMenu();
            }

            if (Environment.PrehistoricMapManager.CurrentMap == Environment.PrehistoricMapType.AncientValley)
            {
                GUI.backgroundColor = new Color(0.95f, 0.40f, 0.12f, 1f);
                if (GUILayout.Button("<b><size=14>🌋 NÚI DUNG NHAM\n(BẢN ĐỒ MỚI)</size></b>", GUILayout.Height(62), GUILayout.Width(btnW)))
                {
                    Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                    Environment.PrehistoricMapManager.LoadMap(Environment.PrehistoricMapType.VolcanicGorge, () =>
                    {
                        FullResetGame();
                    });
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 1f);
                if (GUILayout.Button("<b><size=14>🚪 THOÁT\nGAME</size></b>", GUILayout.Height(62), GUILayout.Width(btnW)))
                {
                    Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                    QuitGame();
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
