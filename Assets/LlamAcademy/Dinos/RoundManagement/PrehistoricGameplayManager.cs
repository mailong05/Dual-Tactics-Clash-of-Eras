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
        [SerializeField] private int StartingGold = 500;
        [SerializeField] private int WaveCompletionBaseGold = 100;

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
            EnsureVillageFortressWallActive();
            ConfigureAllCustomBarricades();
            EnsureVillageInteriorDefenses();
            EnsureAudioManagerAndSettingsMenu();
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

                // 4. Đảm bảo Cầu Gỗ (Bridge) và Sàn Gỗ (Plank) tuyệt đối là đường đi thông suốt, KHÔNG bị gán Wall/Obstacle
                // CHÚ Ý QUAN TRỌNG: Tuyệt đối KHÔNG xóa nhầm hàng rào gỗ phòng thủ Plank_Defenses!
                if (t != null && !n.Contains("defenses") && (n.IndexOf("bridge", StringComparison.OrdinalIgnoreCase) >= 0 || (n.IndexOf("plank", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("defenses", StringComparison.OrdinalIgnoreCase) < 0)))
                {
                    // Bật hiển thị cầu
                    Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends)
                    {
                        if (r != null) r.enabled = true;
                    }

                    if (t.TryGetComponent(out Unit.Wall bw)) Destroy(bw);
                    if (t.TryGetComponent(out UnityEngine.AI.NavMeshObstacle bo)) Destroy(bo);
                    for (int c = t.childCount - 1; c >= 0; c--)
                    {
                        Transform child = t.GetChild(c);
                        if (child != null && child.name.Contains("HealthBar"))
                        {
                            Destroy(child.gameObject);
                        }
                    }
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

            // Kích hoạt AdaptiveWaveManager làm Deep Learning AI Director phân tích chiến thuật
            AdaptiveWaveManager waveMgr = FindFirstObjectByType<AdaptiveWaveManager>();
            if (waveMgr != null)
            {
                waveMgr.enabled = true;
            }

            // Kích hoạt NPC chiến binh bộ lạc xuất hiện cùng phòng thủ làng
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

            // Cấu hình va chạm và AI nhận diện chướng ngại vật cho toàn bộ tường trên map (tôn trọng 100% chỉnh sửa của người chơi)
            ConfigureAllCustomBarricades();

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

        private void EnsureSpawnPoints()
        {
            if (SpawnPoints != null && SpawnPoints.Length >= 3 && SpawnPoints[0] != null) return;

            GameObject spRoot = GameObject.Find("Prehistoric_SpawnPoints");
            if (spRoot == null)
            {
                spRoot = new GameObject("Prehistoric_SpawnPoints");
            }

            Transform sp1 = spRoot.transform.Find("Spawn_North");
            if (sp1 == null) { GameObject g = new GameObject("Spawn_North"); g.transform.SetParent(spRoot.transform); g.transform.position = new Vector3(-2.2f, 0f, 35f); sp1 = g.transform; }

            Transform sp2 = spRoot.transform.Find("Spawn_East");
            if (sp2 == null) { GameObject g = new GameObject("Spawn_East"); g.transform.SetParent(spRoot.transform); g.transform.position = new Vector3(25f, 0f, 20f); sp2 = g.transform; }

            Transform sp3 = spRoot.transform.Find("Spawn_West");
            if (sp3 == null) { GameObject g = new GameObject("Spawn_West"); g.transform.SetParent(spRoot.transform); g.transform.position = new Vector3(-30f, 0f, 15f); sp3 = g.transform; }

            SpawnPoints = new Transform[] { sp1, sp2, sp3 };
        }

        private void LoadDinoPrefabsIfNull()
        {
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
                    Transform spawnPoint = SpawnPoints[UnityEngine.Random.Range(0, SpawnPoints.Length)];
                    Vector3 spawnPos = spawnPoint.position + new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0f, UnityEngine.Random.Range(-2.5f, 2.5f));

                    Vector3 dir = BaseTarget != null ? (BaseTarget.position - spawnPos).normalized : Vector3.back;
                    dir.y = 0;
                    Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

                    GameObject spawned = Instantiate(dinoPrefab, spawnPos, rot);
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
                        dinoBase.EnsureOnNavMesh(15f);
                        dinoBase.ConfigureNavMeshAgent();
                        if (dinoBase.Agent != null && dinoBase.Agent.isOnNavMesh)
                        {
                            dinoBase.Agent.isStopped = false;
                            if (BaseTarget != null) dinoBase.Agent.SetDestination(BaseTarget.position);
                        }
                    }
                    else if (spawned.TryGetComponent(out Unit.FlyingUnit flyComp))
                    {
                        if (BaseTarget != null) flyComp.SetTargetBase(BaseTarget);
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

            int rewardGold = WaveCompletionBaseGold + (_CurrentWave * 25);
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.AddGold(rewardGold);
            }

            ShowAnnouncement($"★ CHIẾN THẮNG ĐỢT {_CurrentWave}! THƯỞNG +{rewardGold} VÀNG! ★", 4.0f);
            OnWaveCompleted?.Invoke(_CurrentWave, rewardGold);
            Audio.PrehistoricAudioManager.Instance?.PlayVictory();
            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: true);

            // Mở khóa chuột để người chơi tương tác chọn menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ProceedToNextWave()
        {
            Time.timeScale = 1.0f;
            ClearAllActiveMonsters();

            _CurrentWave++;

            // Phục hồi làng cho đợt mới và giữ lại các tháp người chơi đã đặt
            ResetEntireVillage(keepPlayerTowers: true);

            SetPhase(PrehistoricWavePhase.BuildingPhase);
            ResetPlayerAndCameraPosition();

            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: true);
            Audio.PrehistoricAudioManager.Instance?.PlayBgmForMode(PrehistoricGameMode.TowerDefense);
            ShowAnnouncement($"⚔️ ĐỢT {_CurrentWave}: HÃY ĐẶT THÊM THÁP VÀ BẤM 'BẮT ĐẦU ĐỢT' (SPACE)! 🛡️", 4.0f);
        }

        private IEnumerator TransitionToNextWaveRoutine()
        {
            yield return new WaitForSeconds(3.5f);

            // Tự động dọn sạch mọi quái vật đợt cũ còn sót lại trước khi mở đợt mới
            ClearAllActiveMonsters();

            _CurrentWave++;

            // Reset và phục hồi toàn bộ ngôi làng (tường, căn cứ, NPC chiến binh) cho bàn/đợt mới
            ResetEntireVillage(keepPlayerTowers: true);

            SetPhase(PrehistoricWavePhase.BuildingPhase);
            ShowAnnouncement($"ĐỢT {_CurrentWave}: HÃY ĐẶT THÊM THÁP VÀ BẤM 'BẮT ĐẦU ĐỢT' (SPACE)!", 4.0f);
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
            OnGameOver?.Invoke();
        }

        private void TriggerVictory()
        {
            SetPhase(PrehistoricWavePhase.Victory);
            StopAllCoroutines();
            Audio.PrehistoricAudioManager.Instance?.PlayVictory();
            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: false);

            // Mở khóa con trỏ chuột
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ShowAnnouncement("🏆 CHIẾN THẮNG KHẢI HOÀN! BẠN ĐÃ BẢO VỆ NGÔI LÀNG THÀNH CÔNG! 🏆", 15.0f);
            Debug.Log("<color=green>[Prehistoric TD]</color> VICTORY! Boss T-Rex defeated!");
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
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
            {
                return;
            }

            var ai = LlamAcademy.Dinos.Enemy.EnemyAIController.Instance != null 
                ? LlamAcademy.Dinos.Enemy.EnemyAIController.Instance 
                : FindFirstObjectByType<LlamAcademy.Dinos.Enemy.EnemyAIController>();
            if (ai != null)
            {
                ai.enabled = true;
                int targetCount = GetTargetDefendersForWave(_CurrentWave);
                // Luôn reset chính xác số lượng NPC theo quy định màn, không thừa không thiếu
                ai.ResetDefendersToExactCount(targetCount);
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
            return;
            GameObject existing = GameObject.Find("Village_Fortress_Wall");
            {
                // Kiểm tra xem đã có đủ 2 chòi cung thủ tiền tiêu và số đoạn tường mới chưa
                Transform wtLeft = existing.transform.Find("Village_Watchtower_Left");
                Transform wtRight = existing.transform.Find("Village_Watchtower_Right");
                Transform leftWing = existing.transform.Find("Left_Wing_Wall");
                Transform rightWing = existing.transform.Find("Right_Wing_Wall");
                Transform customGapWall = existing.transform.Find("Village_Left_Gap_Wall_128_neg152");

                Transform rightFlank = existing.transform.Find("Right_Flank_Wall");

                bool hasWatchtowers = wtLeft != null && wtRight != null;
                bool hasFullWings = leftWing != null && leftWing.childCount >= 14 && rightWing != null && rightWing.childCount >= 18 && rightFlank != null;
                bool hasCustomGap = customGapWall != null;

                if (hasWatchtowers && hasFullWings && hasCustomGap)
                {
                    return; // Đã là tường thành khép kín hoàn chỉnh chuẩn 100%
                }

                Destroy(existing);
            }

            // Xóa mọi chòi cung thủ cũ/ẩn hoặc lạc đàn và cổng cũ bị trùng
            GameObject oldWaypoints = GameObject.Find("Archer Waypoints");
            if (oldWaypoints != null) Destroy(oldWaypoints);
            GameObject oldStarter = GameObject.Find("Starter_Defenses");
            if (oldStarter != null) Destroy(oldStarter);
            GameObject strayWtL = GameObject.Find("Village_Watchtower_Left");
            if (strayWtL != null) Destroy(strayWtL);
            GameObject strayWtR = GameObject.Find("Village_Watchtower_Right");
            if (strayWtR != null) Destroy(strayWtR);
            GameObject strayGate = GameObject.Find("Gate_Complex");
            if (strayGate != null && strayGate.transform.parent == null) Destroy(strayGate);
            GameObject strayGateBar = GameObject.Find("Gate_Portcullis_Barricade");
            if (strayGateBar != null && strayGateBar.transform.parent == null) Destroy(strayGateBar);

            GameObject fortressRoot = new GameObject("Village_Fortress_Wall");

            GameObject towerPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Fortress_Tower");
            GameObject archPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Fortress_Arch");
            GameObject torchPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Fortress_Torch");
            GameObject barricadePrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Barricade");
            GameObject watchtowerPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Watchtower");
            Config.TowerSO barricadeSO = Resources.Load<Config.TowerSO>("Config/Prehistoric/Tower_Barricade");
            if (barricadeSO == null) barricadeSO = Resources.Load<Config.TowerSO>("Tower_Barricade");
            Config.TowerSO watchtowerSO = Resources.Load<Config.TowerSO>("Config/Prehistoric/Tower_Watchtower");
            if (watchtowerSO == null) watchtowerSO = Resources.Load<Config.TowerSO>("Tower_Watchtower");

            const float wallZ = -2.5f;

            // 1. CỔNG THÀNH TRUNG TÂM KHÉP KÍN (Gate Complex: X = -5.0f đến X = +0.6f)
            GameObject gateGroup = new GameObject("Gate_Complex");
            gateGroup.transform.SetParent(fortressRoot.transform);

            // Tháp Canh Cổng Trái
            if (towerPrefab != null)
            {
                GameObject lt = Instantiate(towerPrefab, new Vector3(-5.8f, 0f, wallZ), Quaternion.Euler(0f, 15f, 0f), gateGroup.transform);
                lt.name = "Gate_Tower_Left";
                lt.transform.localScale = Vector3.one * 0.65f;
                EnsureBoxCollider(lt, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
                EnsureNavMeshObstacle(lt, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
            }

            // Tháp Canh Cổng Phải
            if (towerPrefab != null)
            {
                GameObject rt = Instantiate(towerPrefab, new Vector3(1.4f, 0f, wallZ), Quaternion.Euler(0f, -15f, 0f), gateGroup.transform);
                rt.name = "Gate_Tower_Right";
                rt.transform.localScale = Vector3.one * 0.65f;
                EnsureBoxCollider(rt, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
                EnsureNavMeshObstacle(rt, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
            }

            // Tạo nhóm Gate_Entryway đại diện cho TOÀN BỘ CỔNG (Khung cổng vòm gỗ + Đuốc + Cánh cổng chắn)
            GameObject gateEntryway = new GameObject("Gate_Entryway");
            gateEntryway.transform.SetParent(gateGroup.transform);

            // Mái Vòm Cổng Thành Tiền Sử (Archway - Cổng gỗ 3 cột trong Ảnh 3)
            GameObject arch = null;
            if (archPrefab != null)
            {
                arch = Instantiate(archPrefab, new Vector3(-2.2f, 0.2f, wallZ), Quaternion.identity, gateEntryway.transform);
                arch.name = "Gate_Overhead_Arch";
                arch.transform.localScale = new Vector3(0.74f, 0.75f, 0.7f);
                MeshCollider[] archMeshCols = arch.GetComponentsInChildren<MeshCollider>(true);
                foreach (var mc in archMeshCols) if (mc != null) mc.enabled = false;
            }

            // Đuốc Lửa Cổng Thành
            GameObject tl = null;
            GameObject tr = null;
            if (torchPrefab != null)
            {
                tl = Instantiate(torchPrefab, new Vector3(-4.5f, 0f, wallZ + 1.6f), Quaternion.identity, gateEntryway.transform);
                tl.name = "Gate_Torch_Left";
                tl.transform.localScale = Vector3.one * 1.3f;

                tr = Instantiate(torchPrefab, new Vector3(0.1f, 0f, wallZ + 1.6f), Quaternion.identity, gateEntryway.transform);
                tr.name = "Gate_Torch_Right";
                tr.transform.localScale = Vector3.one * 1.3f;
            }

            // Cánh Cổng Gỗ Kiên Cố Chắn Khủng Long (1500 HP - gấp 1.5 lần 1000 HP)
            if (barricadePrefab != null)
            {
                GameObject gateBar = Instantiate(barricadePrefab, new Vector3(-2.2f, 0f, wallZ), Quaternion.identity, gateEntryway.transform);
                gateBar.name = "Gate_Portcullis_Barricade";
                gateBar.transform.localScale = new Vector3(1.8f, 1.4f, 1.0f);
                if (gateBar.TryGetComponent(out Unit.Wall ugb))
                {
                    if (barricadeSO != null) ugb.UnitType = barricadeSO;
                    ugb.Health = 1500;
                    ugb.MaxHealth = 1500;
                    ugb.EnsureHealthBarAttached();
                    // Khi cánh cổng hết máu, làm biến mất TOÀN BỘ CỔNG (Gate_Entryway gồm Khung cổng vòm, Đuốc, và Cánh cổng)
                    ugb.Root = gateEntryway;
                    ugb.OnDeath += (deadUnit) => {
                        if (gateEntryway != null) gateEntryway.SetActive(false);
                        if (arch != null) arch.SetActive(false);
                        if (tl != null) tl.SetActive(false);
                        if (tr != null) tr.SetActive(false);
                    };
                }
                EnsureNavMeshObstacle(gateBar, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
            }

            // 2. TƯỜNG THÀNH CÁNH TRÁI KHÉP KÍN 100% (Left Wing Wall: X = -5.0f đến X = -31.0f)
            GameObject leftWallGroup = new GameObject("Left_Wing_Wall");
            leftWallGroup.transform.SetParent(fortressRoot.transform);

            float[] leftPositionsX = new float[] {
                -5.0f, -7.6f, -10.2f, -12.8f, -15.4f, -18.0f, -20.6f, -23.2f, -25.8f, -28.4f, -31.0f,
                -33.6f, -36.2f, -38.8f, -41.4f
            };
            for (int i = 0; i < leftPositionsX.Length; i++)
            {
                if (barricadePrefab != null)
                {
                    GameObject seg = Instantiate(barricadePrefab, new Vector3(leftPositionsX[i], 0f, wallZ), Quaternion.identity, leftWallGroup.transform);
                    seg.name = $"Left_Wall_Segment_{i + 1}";
                    seg.transform.localScale = new Vector3(1.25f, 1.25f, 1.2f);
                    if (seg.TryGetComponent(out Unit.Unit uw))
                    {
                        if (barricadeSO != null) uw.UnitType = barricadeSO;
                        uw.Health = 900;
                        uw.MaxHealth = 900;
                        uw.EnsureHealthBarAttached();
                    }
                    EnsureNavMeshObstacle(seg, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
                }
            }

            // Pháo Đài Góc Trái sát vách núi (X = -44.0f)
            if (towerPrefab != null)
            {
                GameObject leftBastion = Instantiate(towerPrefab, new Vector3(-44.0f, 0f, wallZ), Quaternion.Euler(0f, 30f, 0f), leftWallGroup.transform);
                leftBastion.name = "Left_Bastion_Tower";
                leftBastion.transform.localScale = Vector3.one * 0.7f;
                EnsureBoxCollider(leftBastion, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
                EnsureNavMeshObstacle(leftBastion, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
            }

            // 3. TƯỜNG THÀNH CÁNH PHẢI KHÉP KÍN 100% (Right Wing Wall: X = +0.6f đến X = +42.2f nối liền vào vách núi phía Đông)
            GameObject rightWallGroup = new GameObject("Right_Wing_Wall");
            rightWallGroup.transform.SetParent(fortressRoot.transform);

            // 17 đoạn tường gối chồng lên nhau 1.15m (mỗi đoạn rộng 3.75m, khoảng cách tâm 2.6m -> Tuyệt đối không có kẽ hở từ X = +0.6f đến X = +42.2f)
            float[] rightPositionsX = new float[] {
                0.6f, 3.2f, 5.8f, 8.4f, 11.0f, 13.6f, 16.2f, 18.8f, 21.4f, 24.0f, 26.6f,
                29.2f, 31.8f, 34.4f, 37.0f, 39.6f, 42.2f
            };
            for (int i = 0; i < rightPositionsX.Length; i++)
            {
                if (barricadePrefab != null)
                {
                    GameObject seg = Instantiate(barricadePrefab, new Vector3(rightPositionsX[i], 0f, wallZ), Quaternion.identity, rightWallGroup.transform);
                    seg.name = $"Right_Wall_Segment_{i + 1}";
                    seg.transform.localScale = new Vector3(1.25f, 1.25f, 1.2f);
                    if (seg.TryGetComponent(out Unit.Unit uw))
                    {
                        if (barricadeSO != null) uw.UnitType = barricadeSO;
                        uw.Health = 900;
                        uw.MaxHealth = 900;
                        uw.EnsureHealthBarAttached();
                    }
                    EnsureNavMeshObstacle(seg, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
                }
            }

            // Pháo Đài Góc Phải sát vách núi (X = 44.5f cắm sâu vào vách núi phía Đông)
            if (towerPrefab != null)
            {
                GameObject rightBastion = Instantiate(towerPrefab, new Vector3(44.5f, 0f, wallZ), Quaternion.Euler(0f, -30f, 0f), rightWallGroup.transform);
                rightBastion.name = "Right_Bastion_Tower";
                rightBastion.transform.localScale = Vector3.one * 0.7f;
                EnsureBoxCollider(rightBastion, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
                EnsureNavMeshObstacle(rightBastion, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
            }

            // 4. BỔ SUNG TƯỜNG SƯỜN PHẢI (Right Flank Barricade Wall) ĐỐI XỨNG HOÀN TOÀN VỚI TƯỜNG SƯỜN TRÁI CỦA LÀNG
            GameObject rightFlankGroup = new GameObject("Right_Flank_Wall");
            rightFlankGroup.transform.SetParent(fortressRoot.transform);

            Vector3[] rightFlankPositions = new Vector3[] {
                // Tuyến rào chắn sườn núi phía Đông (đối xứng hoàn toàn với 5 rào chắn sườn Tây)
                new Vector3(28.92f, 1.91f, 4.91f),
                new Vector3(28.97f, 1.86f, 2.22f),
                new Vector3(28.95f, 1.91f, -0.29f),
                new Vector3(29.02f, 1.91f, -3.28f),
                new Vector3(34.81f, 1.91f, -10.39f),
                // Rào chắn kết nối từ chòi bộ lạc phía Đông ra sườn rào
                new Vector3(14.5f, 0.2f, 0.85f),
                new Vector3(17.5f, 0.2f, 0.85f),
                new Vector3(20.5f, 0.2f, 0.85f),
                new Vector3(23.5f, 0.2f, 0.85f),
                new Vector3(26.5f, 0.2f, 0.85f)
            };
            for (int i = 0; i < rightFlankPositions.Length; i++)
            {
                if (barricadePrefab != null)
                {
                    GameObject rf = Instantiate(barricadePrefab, rightFlankPositions[i], Quaternion.Euler(0f, i < 5 ? 90f : 0f, 0f), rightFlankGroup.transform);
                    rf.name = $"Right_Flank_Barricade_{i + 1}";
                    rf.transform.localScale = new Vector3(1.25f, 1.25f, 1.2f);
                    if (rf.TryGetComponent(out Unit.Unit uwf))
                    {
                        if (barricadeSO != null) uwf.UnitType = barricadeSO;
                        uwf.Health = 900;
                        uwf.MaxHealth = 900;
                        uwf.EnsureHealthBarAttached();
                    }
                    EnsureNavMeshObstacle(rf, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
                }
            }

            // 4. THÊM 2 CHÒI CUNG THỦ TIỀN TIÊU Ở 2 BÊN PHÍA TRƯỚC LÀNG (Front Flanking Archer Watchtowers - 1200 HP)
            if (watchtowerPrefab != null)
            {
                // Chòi Cung Thủ Bên Trái
                GameObject wtLeft = Instantiate(watchtowerPrefab, new Vector3(-6.0f, 0f, -5.5f), Quaternion.Euler(0f, 15f, 0f), fortressRoot.transform);
                wtLeft.name = "Village_Watchtower_Left";
                if (wtLeft.TryGetComponent(out Unit.Unit uwl))
                {
                    if (watchtowerSO != null) uwl.UnitType = watchtowerSO;
                    uwl.MaxHealth = 1200;
                    uwl.Health = 1200;
                    uwl.EnsureHealthBarAttached();
                }
                EnsureNavMeshObstacle(wtLeft, new Vector3(2.6f, 5.8f, 2.6f), new Vector3(0f, 2.9f, 0f));

                // Chòi Cung Thủ Bên Phải
                GameObject wtRight = Instantiate(watchtowerPrefab, new Vector3(1.6f, 0f, -5.5f), Quaternion.Euler(0f, -15f, 0f), fortressRoot.transform);
                wtRight.name = "Village_Watchtower_Right";
                if (wtRight.TryGetComponent(out Unit.Unit uwr))
                {
                    if (watchtowerSO != null) uwr.UnitType = watchtowerSO;
                    uwr.MaxHealth = 1200;
                    uwr.Health = 1200;
                    uwr.EnsureHealthBarAttached();
                }
                EnsureNavMeshObstacle(wtRight, new Vector3(2.6f, 5.8f, 2.6f), new Vector3(0f, 2.9f, 0f));
            }

            // 5. BỔ SUNG BỨC TƯỜNG Ở TỌA ĐỘ (128, 0, -152) THEO YÊU CẦU NGƯỜI CHƠI ĐỂ LẮP CHỖ TRỐNG BÊN TRÁI
            if (barricadePrefab != null)
            {
                GameObject customGapWall = Instantiate(barricadePrefab, new Vector3(128f, 0f, -152f), Quaternion.identity, fortressRoot.transform);
                customGapWall.name = "Village_Left_Gap_Wall_128_neg152";
                customGapWall.transform.localScale = Vector3.one;
                if (customGapWall.TryGetComponent(out Unit.Unit ucg))
                {
                    if (barricadeSO != null) ucg.UnitType = barricadeSO;
                    ucg.Health = 900;
                    ucg.MaxHealth = 900;
                    ucg.EnsureHealthBarAttached();
                }
                EnsureBoxCollider(customGapWall, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
                EnsureNavMeshObstacle(customGapWall, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
            }

            Debug.Log("<color=green>[Prehistoric TD]</color> Khởi tạo thành công Tường Thành Có Cổng khép kín 100%, 2 Chòi Cung Thủ tiền tiêu và Bức tường lắp chỗ trống bên trái (128, 0, -152)!");
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
            // Bỏ qua va chạm vật lý giữa khủng long và khủng long (Layer 8)
            Physics.IgnoreLayerCollision(8, 8, true);

            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                // 1. CỔNG VÒM VÀ CẦU GỖ TUYẾN 2 (Arch Entry Wall) hoặc Khung vòm cổng chính (Gate_Overhead_Arch)
                if (n.StartsWith("Arch Entry Wall", StringComparison.OrdinalIgnoreCase) || n.Contains("Overhead_Arch") || n.Contains("Bridge_Tribal"))
                {
                    // Bật lại Renderer
                    Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends) if (r != null) r.enabled = true;

                    // Tắt MeshCollider dạng Convex đang bịt kín khoảng trống vòm cổng
                    MeshCollider[] meshCols = t.GetComponentsInChildren<MeshCollider>(true);
                    foreach (var mc in meshCols)
                    {
                        if (mc != null) mc.enabled = false;
                    }

                    // Tắt carving trên NavMeshObstacle của cầu để đường đi NavMesh luôn thông suốt
                    UnityEngine.AI.NavMeshObstacle[] obstacles = t.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
                    foreach (var obs in obstacles)
                    {
                        if (obs != null) obs.carving = false;
                    }

                    // Thêm 2 BoxCollider ở 2 bên chân cột trụ của vòm cổng (để giữa vòm cổng rộng 5.4m hoàn toàn thông suốt)
                    Transform leftPillar = t.Find("Collider_Pillar_Left");
                    if (leftPillar == null)
                    {
                        GameObject lp = new GameObject("Collider_Pillar_Left");
                        lp.layer = 2; // Ignore Raycast để không bị quái chọn làm mục tiêu cắn
                        lp.transform.SetParent(t, false);
                        lp.transform.localPosition = new Vector3(-3.8f, 2.2f, 0f);
                        BoxCollider bcL = lp.AddComponent<BoxCollider>();
                        bcL.size = new Vector3(2.2f, 4.4f, 3.2f);
                    }
                    else
                    {
                        leftPillar.gameObject.layer = 2;
                        if (leftPillar.TryGetComponent(out BoxCollider bcL)) bcL.enabled = true;
                    }

                    Transform rightPillar = t.Find("Collider_Pillar_Right");
                    if (rightPillar == null)
                    {
                        GameObject rp = new GameObject("Collider_Pillar_Right");
                        rp.layer = 2; // Ignore Raycast để không bị quái chọn làm mục tiêu cắn
                        rp.transform.SetParent(t, false);
                        rp.transform.localPosition = new Vector3(3.8f, 2.2f, 0f);
                        BoxCollider bcR = rp.AddComponent<BoxCollider>();
                        bcR.size = new Vector3(2.2f, 4.4f, 3.2f);
                    }
                    else
                    {
                        rightPillar.gameObject.layer = 2;
                        if (rightPillar.TryGetComponent(out BoxCollider bcR)) bcR.enabled = true;
                    }

                    // Mặt sàn phía trên cho Cung Thủ Làng đứng
                    Transform floorDeck = t.Find("Collider_Walkway_Deck");
                    if (floorDeck == null)
                    {
                        GameObject fd = new GameObject("Collider_Walkway_Deck");
                        fd.layer = 2; // Ignore Raycast để không bị quái chọn làm mục tiêu cắn
                        fd.transform.SetParent(t, false);
                        fd.transform.localPosition = new Vector3(0f, 4.2f, 0f);
                        BoxCollider bcF = fd.AddComponent<BoxCollider>();
                        bcF.size = new Vector3(10.0f, 0.4f, 3.2f);
                    }
                    else
                    {
                        floorDeck.gameObject.layer = 2;
                        if (floorDeck.TryGetComponent(out BoxCollider bcF)) bcF.enabled = true;
                    }

                    // Gán Wall component (600 HP - gấp 1.5 lần 400 HP) cho cổng vòm
                    if (!t.TryGetComponent(out Unit.Wall wArch))
                    {
                        wArch = t.gameObject.AddComponent<Unit.Wall>();
                        wArch.MaxHealth = 600;
                        wArch.Health = 600;
                        wArch.Root = t.gameObject;
                        wArch.EnsureHealthBarAttached();
                    }
                    else
                    {
                        if (wArch.MaxHealth <= 0 || wArch.MaxHealth == 400) wArch.MaxHealth = 600;
                        if (wArch.Health <= 0 || wArch.Health == 400) wArch.Health = wArch.MaxHealth;
                        wArch.Root = t.gameObject;
                        wArch.EnsureHealthBarAttached();
                    }
                }
                // 2. CHÒI BỘ LẠC TRUNG TÂM (Big Tower with Platform - 900 HP)
                else if (n.StartsWith("Big Tower with Platform", StringComparison.OrdinalIgnoreCase))
                {
                    Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends) if (r != null) r.enabled = true;

                    // Tắt carving trên NavMeshObstacle
                    UnityEngine.AI.NavMeshObstacle[] obstacles = t.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
                    foreach (var obs in obstacles)
                    {
                        if (obs != null) obs.carving = false;
                    }

                    // Tắt MeshCollider dạng convex nếu có
                    MeshCollider[] meshCols = t.GetComponentsInChildren<MeshCollider>(true);
                    foreach (var mc in meshCols)
                    {
                        if (mc != null) mc.enabled = false;
                    }

                    // Đảm bảo có BoxCollider bao quanh thân chòi/sàn tròn
                    EnsureBoxCollider(t.gameObject, new Vector3(7.5f, 4.5f, 7.5f), new Vector3(0f, 2.25f, 0f));

                    // Gán Wall component (900 HP - gấp 1.5 lần 600 HP) cho chòi
                    if (!t.TryGetComponent(out Unit.Wall wTower))
                    {
                        wTower = t.gameObject.AddComponent<Unit.Wall>();
                        wTower.MaxHealth = 900;
                        wTower.Health = 900;
                        wTower.Root = t.gameObject;
                        wTower.EnsureHealthBarAttached();
                    }
                    else
                    {
                        if (wTower.MaxHealth <= 0 || wTower.MaxHealth == 600) wTower.MaxHealth = 900;
                        if (wTower.Health <= 0 || wTower.Health == 600) wTower.Health = wTower.MaxHealth;
                        wTower.Root = t.gameObject;
                        wTower.EnsureHealthBarAttached();
                    }
                }
            }
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
        public void ResetEntireVillage(bool keepPlayerTowers = true)
        {
            CleanupInvisibleAndStrayObjects();

            // 1. Phục hồi 100% tất cả tường thành và rào chắn (kể cả những tường đã bị phá hủy / SetActive(false))
            var allWalls = FindObjectsByType<Unit.Wall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var wall in allWalls)
            {
                if (wall != null)
                {
                    wall.FullRestore();
                }
            }

            // Đảm bảo cấu hình va chạm và chướng ngại vật
            ConfigureAllCustomBarricades();
            EnsureVillageInteriorDefenses();

            // Phục hồi và đảm bảo 2 chòi canh trên bờ tường thành (Village_Watchtower_Left & Right)
            EnsureVillageWatchtowers();

            // 2. Xóa sạch bộ nhớ đột phá (Tactical Breach Memory) của AI khủng long
            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.ClearAll();
            }

            // 3. Cập nhật NavMesh sau khi toàn bộ tường và chướng ngại vật được khôi phục
            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.NavMeshUpdatedByObstacles();
                NavMeshManager.Instance.RecalculateTriangulation(true);
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
                    int waveGold = StartingGold + (_CurrentWave - 1) * 200;
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

            // 6. Làm mới và hồi sinh toàn bộ NPC Dân Làng / Chiến binh bảo vệ (Defenders) theo màn hiện tại
            var enemyAI = LlamAcademy.Dinos.Enemy.EnemyAIController.Instance != null 
                ? LlamAcademy.Dinos.Enemy.EnemyAIController.Instance 
                : FindFirstObjectByType<LlamAcademy.Dinos.Enemy.EnemyAIController>();
            if (enemyAI != null)
            {
                enemyAI.enabled = true;
                int defenderCount = GetTargetDefendersForWave(_CurrentWave);
                // Luôn luôn reset chính xác 100% số lượng NPC theo quy định của màn
                enemyAI.ResetDefendersToExactCount(defenderCount);
            }

            // 7. Dọn sạch các thanh máu rác/mồ côi nếu có trên màn hình
            if (Utility.HealthBarCanvas.HasInstance)
            {
                Utility.HealthBarCanvas.Instance.ClearOrphaned();
            }

            Debug.Log($"<color=green>[Prehistoric TD]</color> Đã thiết lập lại (Reset) 100% ngôi làng cho Màn {_CurrentWave}: Tường thành, 2 Chòi Canh Làng, Căn cứ ({_CurrentBaseHealth} HP), NPC chiến binh, và bộ nhớ AI!");
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
            }

            // 5. Chơi lại từ đầu: Reset toàn bộ và chơi từ màn 1 (xóa vật thể người chơi đặt, 500 vàng, 4 NPC)
            ResetEntireVillage(keepPlayerTowers: false);

            // 6. Đặt lại trạng thái về Pha Xây Dựng (Building Phase)
            SetPhase(PrehistoricWavePhase.BuildingPhase);

            // 7. Đặt lại vị trí ban đầu của Camera và Nhân vật
            ResetPlayerAndCameraPosition();

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
            }

            // Chơi lại màn X: Reset theo màn (tất cả mọi thứ NPC, vật thể, xóa bỏ tất cả các vật thể người chơi đã đặt)
            ResetEntireVillage(keepPlayerTowers: false);

            SetPhase(PrehistoricWavePhase.BuildingPhase);

            ResetPlayerAndCameraPosition();
            ShowAnnouncement($"🔄 CHƠI LẠI MÀN {_CurrentWave}! PHÒNG TUYẾN ĐÃ ĐƯỢC RESET! BẤM SPACE ĐỂ BẮT ĐẦU! ⚔", 4.0f);
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
                Player.TowerPlacer.Instance.ResetGoldToDefault(savedGold > 0 ? savedGold : StartingGold);
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
            if (_CurrentPhase == PrehistoricWavePhase.BuildingPhase)
            {
                float btnWidth = 260f;
                float btnHeight = 60f;
                float x = Mobile.MobileUIHelper.ScreenWidth - btnWidth - 25f;
                float y = 25f;

                Rect waveRect = new Rect(x, y, btnWidth, btnHeight);
                Mobile.MobileUIHelper.RegisterUIRect(waveRect);

                GUI.backgroundColor = new Color(0.1f, 0.8f, 0.2f, 1f);
                string btnLabel = Mobile.MobileUIHelper.IsMobile 
                    ? $"<b><size=16>▶ BẮT ĐẦU ĐỢT {_CurrentWave}\n(CHẠM ĐỂ XUẤT TRẬN)</size></b>"
                    : $"<b><size=16>▶ BẮT ĐẦU ĐỢT {_CurrentWave}\n(PHÍM SPACE / BẤM VÀO ĐÂY)</size></b>";

                if (GUI.Button(waveRect, btnLabel))
                {
                    StartNextWave();
                    Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
                }
                GUI.backgroundColor = Color.white;
            }
            else if (_CurrentPhase == PrehistoricWavePhase.CombatPhase)
            {
                float boxWidth = 240f;
                float boxHeight = 50f;
                float x = Mobile.MobileUIHelper.ScreenWidth - boxWidth - 25f;
                float y = 25f;

                Rect combatRect = new Rect(x, y, boxWidth, boxHeight);
                Mobile.MobileUIHelper.RegisterUIRect(combatRect);

                GUI.backgroundColor = new Color(0.8f, 0.2f, 0.1f, 0.9f);
                GUI.Box(combatRect, $"<b><size=15><color=white>⚔ ĐANG CHIẾN ĐẤU ⚔\nQuái còn lại: {MonstersAliveCount}</color></size></b>");
                GUI.backgroundColor = Color.white;
            }

            // Nút chuyển đổi Toàn Màn Hình nhanh [F11] cho PC
            if (!Mobile.MobileUIHelper.IsMobile)
            {
                float fsBtnW = 160f;
                float fsBtnH = 32f;
                float fsX = Mobile.MobileUIHelper.ScreenWidth - fsBtnW - 25f;
                float fsY = (_CurrentPhase == PrehistoricWavePhase.BuildingPhase) ? 92f : 82f;
                GUI.backgroundColor = Screen.fullScreen ? new Color(0.2f, 0.7f, 0.9f, 0.85f) : new Color(0.25f, 0.25f, 0.25f, 0.85f);
                string fsLabel = Screen.fullScreen ? "<b>⛶ CỬA SỔ [F11]</b>" : "<b>⛶ FULLSCREEN [F11]</b>";
                if (GUI.Button(new Rect(fsX, fsY, fsBtnW, fsBtnH), fsLabel))
                {
                    ToggleFullscreen();
                }
                GUI.backgroundColor = Color.white;
            }
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
            GUILayout.Label("<size=14><color=#E0E0E0>Hệ thống phòng thủ làng (Tường, Căn cứ, NPC) sẵn sàng phục hồi lại toàn bộ 100%.</color></size>");
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
            int rewardGold = WaveCompletionBaseGold + (_CurrentWave * 25);
            GUILayout.Label($"<size=15>Bảo vệ làng thành công! Thưởng đợt: <b><color=#55FF55>+{rewardGold} Vàng</color></b> | Căn cứ còn: <b>{_CurrentBaseHealth} / {_MaxBaseHealth} HP</b></size>");
            GUILayout.Space(10);

            int nextWave = _CurrentWave + 1;
            int nextMonsters = GetMonsterCountForWave(nextWave);
            int nextDefenders = GetTargetDefendersForWave(nextWave);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<size=14><b><color=#55FF55>📊 THÔNG SỐ ĐỘ KHÓ ĐỢT TIẾP THEO (ĐỢT {nextWave} / {_MaxWaves}):</color></b></size>");
            GUILayout.Label($"• <b>Bầy khủng long tấn công</b>: {nextMonsters} con (Độ hung hãn và tốc độ gia tăng)\n" +
                            $"• <b>Chiến binh phòng thủ Làng (NPC)</b>: {nextDefenders} quân phòng thủ tiếp viện\n" +
                            $"• <b>Phòng tuyến Làng</b>: Tự động phục hồi toàn bộ tường gỗ, cổng thành và căn cứ 100% HP\n" +
                            $"• <b>Chiến lược</b>: Sử dụng vàng vừa nhận để đặt thêm tháp, sẵn sàng cho đợt công kích mới!");
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
            GUILayout.Label("<size=14><color=#00FFAA><b>✨ Bạn đã hoàn thành trò chơi và chúng tôi sẽ tiếp tục phát triển phần chơi mới!</b></color></size>");
            GUILayout.Label("<size=12><color=#E0FFE8>Cảm ơn bạn đã đồng hành và chỉ huy bảo vệ bộ lạc tiền sử vượt qua 10 đợt tiến công khốc liệt của bầy khủng long bạo chúa.</color></size>");
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

            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 1f);
            if (GUILayout.Button("<b><size=14>🚪 THOÁT\nGAME</size></b>", GUILayout.Height(62), GUILayout.Width(btnW)))
            {
                Audio.PrehistoricAudioManager.Instance?.PlayButtonClick();
                QuitGame();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
