using System;
using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Enemy;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
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
        [SerializeField] private int _MaxBaseHealth = 100;
        [SerializeField] private int _CurrentBaseHealth = 100;
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

        private List<Unit.Unit> ActiveMonsters = new();
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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _CurrentBaseHealth = _MaxBaseHealth;
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

            // Kích hoạt NPC chiến binh bộ lạc xuất hiện cùng phòng thủ làng
            EnsureVillageDefendersActive();

            SetPhase(PrehistoricWavePhase.BuildingPhase);
            ShowAnnouncement($"ĐỢT {_CurrentWave}: HÃY ĐẶT THÁP PHÒNG THỦ TRƯỚC KHI BẮT ĐẦU!", 4.0f);
        }

        private void Update()
        {
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
            else if (_CurrentPhase == PrehistoricWavePhase.GameOver || _CurrentPhase == PrehistoricWavePhase.Victory)
            {
                if (isSpacePressed || isEnterPressed || isRPressed)
                {
                    FullResetGame();
                }
            }

            // In Wave 10, track Boss health
            if (_CurrentWave == _MaxWaves && ActiveBossInstance == null && _CurrentPhase == PrehistoricWavePhase.CombatPhase)
            {
                ActiveBossInstance = FindFirstObjectByType<BossDino>();
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
            if (_CurrentPhase != PrehistoricWavePhase.BuildingPhase) return;

            // Xóa sạch toàn bộ quái vật còn sót lại từ đợt cũ trước khi bắt đầu đợt mới
            ClearAllActiveMonsters();

            // Đảm bảo các NPC chiến binh bộ lạc luôn có mặt để chiến đấu
            EnsureVillageDefendersActive();

            SetPhase(PrehistoricWavePhase.CombatPhase);
            OnWaveStarted?.Invoke(_CurrentWave);

            if (WaveSpawnRoutine != null) StopCoroutine(WaveSpawnRoutine);
            WaveSpawnRoutine = StartCoroutine(SpawnWaveRoutine(_CurrentWave));
        }

        private IEnumerator SpawnWaveRoutine(int waveIndex)
        {
            ActiveMonsters.Clear();
            List<GameObject> spawnQueue = GenerateWaveComposition(waveIndex);
            TotalMonstersInWave = spawnQueue.Count;
            MonstersAliveCount = TotalMonstersInWave;

            ShowAnnouncement($"⚔ ĐỢT {waveIndex} BẮT ĐẦU! {TotalMonstersInWave} KHỦNG LONG ĐANG TẤN CÔNG! ⚔", 3.0f);
            Debug.Log($"<color=orange>[Prehistoric TD]</color> Starting Wave {waveIndex} with {TotalMonstersInWave} dinos!");

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
                    }
                }

                // Staggered interval between dino spawns
                float spawnDelay = Mathf.Clamp(1.2f - (waveIndex * 0.05f), 0.5f, 1.2f);
                yield return new WaitForSeconds(spawnDelay);
            }

            // Wait until all monsters are dead or reached base
            while (MonstersAliveCount > 0 && _CurrentPhase == PrehistoricWavePhase.CombatPhase)
            {
                yield return new WaitForSeconds(0.4f);
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
            List<GameObject> queue = new();

            switch (waveIndex)
            {
                case 1:
                    // Wave 1: 8 Velociraptors (Học cách đặt tháp cơ bản)
                    AddMultiple(queue, RaptorPrefab, 8);
                    break;
                case 2:
                    // Wave 2: 10 Raptors + 4 Pterodactyls (Bắt đầu xuất hiện quái bay)
                    AddMultiple(queue, RaptorPrefab, 10);
                    AddMultiple(queue, PterodactylPrefab, 4);
                    break;
                case 3:
                    // Wave 3: 8 Raptors + 4 Pterodactyls + 3 Ankylosaurus (Xuất hiện quái trâu bọc thép)
                    AddMultiple(queue, RaptorPrefab, 8);
                    AddMultiple(queue, PterodactylPrefab, 4);
                    AddMultiple(queue, AnkylosaurusPrefab, 3);
                    break;
                case 4:
                    // Wave 4: 14 Raptors + 6 Pterodactyls + 4 Ankylosaurus
                    AddMultiple(queue, RaptorPrefab, 14);
                    AddMultiple(queue, PterodactylPrefab, 6);
                    AddMultiple(queue, AnkylosaurusPrefab, 4);
                    break;
                case 5:
                    // Wave 5: 16 Raptors + 8 Pterodactyls + 6 Ankylosaurus
                    AddMultiple(queue, RaptorPrefab, 16);
                    AddMultiple(queue, PterodactylPrefab, 8);
                    AddMultiple(queue, AnkylosaurusPrefab, 6);
                    break;
                case 6:
                    // Wave 6: Bão quái bay + bầy săn mồi
                    AddMultiple(queue, PterodactylPrefab, 14);
                    AddMultiple(queue, RaptorPrefab, 18);
                    AddMultiple(queue, AnkylosaurusPrefab, 4);
                    break;
                case 7:
                    // Wave 7: Binh đoàn bọc thép công thành
                    AddMultiple(queue, AnkylosaurusPrefab, 10);
                    AddMultiple(queue, RaptorPrefab, 18);
                    AddMultiple(queue, PterodactylPrefab, 8);
                    break;
                case 8:
                    // Wave 8: Áp đảo toàn diện
                    AddMultiple(queue, RaptorPrefab, 24);
                    AddMultiple(queue, PterodactylPrefab, 12);
                    AddMultiple(queue, AnkylosaurusPrefab, 8);
                    break;
                case 9:
                    // Wave 9: Tiền trạm trước trận chung kết
                    AddMultiple(queue, AnkylosaurusPrefab, 12);
                    AddMultiple(queue, PterodactylPrefab, 15);
                    AddMultiple(queue, RaptorPrefab, 28);
                    break;
                case 10:
                default:
                    // Wave 10: TRẬN CHIẾN CUỐI CÙNG - BOSS T-REX APEX KHỔNG LỒ!
                    AddMultiple(queue, RaptorPrefab, 12);
                    AddMultiple(queue, PterodactylPrefab, 6);
                    AddMultiple(queue, AnkylosaurusPrefab, 4);
                    queue.Add(TRexBossPrefab); // The mighty T-Rex Apex Boss!
                    AddMultiple(queue, RaptorPrefab, 8);
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
                ActiveMonsters.Remove(deadMonster);
            }
            MonstersAliveCount = Mathf.Max(0, MonstersAliveCount - 1);

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

        public void OnDinoReachedBase(Unit.Unit dino, int damage = 10)
        {
            if (_CurrentPhase != PrehistoricWavePhase.CombatPhase) return;

            DamageBase(damage, dino);

            // Dino reached base and caused damage, remove from active pool
            ActiveMonsters.Remove(dino);
            MonstersAliveCount = Mathf.Max(0, MonstersAliveCount - 1);

            if (dino != null)
            {
                Destroy(dino.gameObject, 0.1f);
            }
        }

        public void DamageBase(int damage, Unit.Unit attackingDino = null)
        {
            if (_CurrentPhase == PrehistoricWavePhase.GameOver) return;

            _CurrentBaseHealth = Mathf.Max(0, _CurrentBaseHealth - damage);
            OnBaseHealthChanged?.Invoke(_CurrentBaseHealth, _MaxBaseHealth);

            string attackerName = attackingDino != null ? attackingDino.GetType().Name : "Khủng long";
            ShowAnnouncement($"<color=red>CẢNH BÁO: {attackerName} xâm nhập Căn Cứ! -{damage} HP!</color>", 2.0f);
            Debug.LogWarning($"<color=red>[Village Defense]</color> Base took {damage} damage! Remaining HP: {_CurrentBaseHealth}/{_MaxBaseHealth}");

            if (_CurrentBaseHealth <= 0)
            {
                TriggerGameOver();
            }
        }

        private void TriggerWaveCleared()
        {
            SetPhase(PrehistoricWavePhase.WaveCleared);

            int rewardGold = WaveCompletionBaseGold + (_CurrentWave * 25);
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.AddGold(rewardGold);
            }

            ShowAnnouncement($"★ CHIẾN THẮNG ĐỢT {_CurrentWave}! THƯỞNG +{rewardGold} VÀNG! ★", 3.5f);
            OnWaveCompleted?.Invoke(_CurrentWave, rewardGold);

            StartCoroutine(TransitionToNextWaveRoutine());
        }

        private IEnumerator TransitionToNextWaveRoutine()
        {
            yield return new WaitForSeconds(3.5f);

            // Tự động dọn sạch mọi quái vật đợt cũ còn sót lại trước khi mở đợt mới
            ClearAllActiveMonsters();

            _CurrentWave++;
            SetPhase(PrehistoricWavePhase.BuildingPhase);
            ShowAnnouncement($"ĐỢT {_CurrentWave}: HÃY ĐẶT THÊM THÁP VÀ BẤM 'BẮT ĐẦU ĐỢT' (SPACE)!", 4.0f);
        }

        private void TriggerGameOver()
        {
            SetPhase(PrehistoricWavePhase.GameOver);
            StopAllCoroutines();

            ShowAnnouncement("☠ THẤT THỦ! NGÔI LÀNG TIỀN SỬ ĐÃ BỊ PHÁ HỦY! ☠", 10.0f);
            Debug.LogError("<color=red>[Prehistoric TD]</color> GAME OVER! The village was destroyed by dinosaurs!");
            OnGameOver?.Invoke();
        }

        private void TriggerVictory()
        {
            SetPhase(PrehistoricWavePhase.Victory);
            StopAllCoroutines();

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
                    if (ActiveMonsters[i].gameObject != null)
                    {
                        Destroy(ActiveMonsters[i].gameObject);
                    }
                }
            }
            ActiveMonsters.Clear();

            // Quét dọn triệt để CHỈ các Unit khủng long/quái vật còn sót lại trên scene (TUYỆT ĐỐI KHÔNG XÓA DEFENDER, TƯỜNG HAY THÁP)
            var allUnits = FindObjectsByType<Unit.Unit>(FindObjectsSortMode.None);
            foreach (var unit in allUnits)
            {
                if (unit != null && (unit is Dino || unit is PrehistoricDinoBase || unit is RunnerDino || unit is FlyingUnit || unit is SiegeDino || unit is BossDino))
                {
                    unit.OnDeath -= HandleMonsterDeath;
                    if (unit.gameObject != null)
                    {
                        Destroy(unit.gameObject);
                    }
                }
            }

            MonstersAliveCount = 0;
            TotalMonstersInWave = 0;
        }

        public bool IsActiveMonster(Unit.Unit unit)
        {
            if (unit == null) return false;
            return ActiveMonsters.Contains(unit);
        }

        public void EnsureVillageDefendersActive()
        {
            if (LlamAcademy.Dinos.Enemy.EnemyAIController.Instance != null)
            {
                var ai = LlamAcademy.Dinos.Enemy.EnemyAIController.Instance;
                ai.enabled = true;
                ai.ResourcesToSpend += 300 + (_CurrentWave * 80);
                ai.EnsureDefendersAlive(Mathf.Clamp(3 + _CurrentWave / 2, 4, 8));
            }
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
            ClearAllActiveMonsters();

            // 3. Xóa sạch 100% các công trình / tháp phòng thủ mà người chơi đã đặt
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.ClearAllPlacedTowers();
                TowerPlacer.Instance.ResetGoldToDefault(StartingGold);
                TowerPlacer.Instance.DeselectTower();
            }

            // 4. Khôi phục hoàn toàn Máu Làng về 100/100 HP
            _CurrentBaseHealth = _MaxBaseHealth;
            OnBaseHealthChanged?.Invoke(_CurrentBaseHealth, _MaxBaseHealth);

            // 5. Đặt lại Đợt chơi về Wave 1
            _CurrentWave = 1;

            // 6. Đặt lại trạng thái về Pha Xây Dựng (Building Phase)
            SetPhase(PrehistoricWavePhase.BuildingPhase);

            // 7. Đồng bộ với RoundManager
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.ResetRoundToStart();
            }

            // 8. Đặt lại vị trí ban đầu của Camera và Nhân vật
            ResetPlayerAndCameraPosition();

            // 9. Nạp lại Scene để đảm bảo môi trường game nguyên bản 100%
            try
            {
                string currentScene = SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(currentScene))
                {
                    SceneManager.LoadScene(currentScene);
                    return;
                }
                else
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Prehistoric TD] Scene reload exception fallback: {e.Message}");
            }

            ShowAnnouncement("⚔ BẮT ĐẦU VÁN MỚI! HÃY XÂY DỰNG LẠI PHÒNG TUYẾN TỪ ĐỢT 1! ⚔", 4.0f);
        }

        public void RetryCurrentWave()
        {
            FullResetGame();
        }

        public void RestartGame()
        {
            FullResetGame();
        }

        public void ShowAnnouncement(string message, float duration = 3.0f)
        {
            ScreenAnnouncement = message;
            AnnouncementTimer = duration;
        }

        private void OnGUI()
        {
            DrawTopLeftStatusBanner();
            DrawWaveActionControl();
            DrawScreenAnnouncement();

            if (_CurrentPhase != PrehistoricWavePhase.GameOver && _CurrentPhase != PrehistoricWavePhase.Victory)
            {
                DrawTowerPlacementToolbar();
            }

            if (_CurrentWave == _MaxWaves && ActiveBossInstance != null && ActiveBossInstance.Health > 0)
            {
                DrawBossHealthBar();
            }

            if (_CurrentPhase == PrehistoricWavePhase.GameOver)
            {
                DrawGameOverModal();
            }
            else if (_CurrentPhase == PrehistoricWavePhase.Victory)
            {
                DrawVictoryModal();
            }
        }

        private void DrawTowerPlacementToolbar()
        {
            if (TowerPlacer.Instance == null || TowerPlacer.Instance.AvailableTowers == null || TowerPlacer.Instance.AvailableTowers.Count == 0) return;

            var towers = TowerPlacer.Instance.AvailableTowers;
            int gold = TowerPlacer.Instance.Gold;

            float slotWidth = 110f;
            float toolbarWidth = towers.Count * slotWidth + 20f;
            float toolbarHeight = 72f;
            float x = (Screen.width - toolbarWidth) / 2f;
            float y = Screen.height - toolbarHeight - 12f;

            GUILayout.BeginArea(new Rect(x, y, toolbarWidth, toolbarHeight), GUI.skin.box);
            GUILayout.BeginHorizontal();

            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                if (t == null) continue;

                bool canAfford = gold >= t.Cost;
                bool isSelected = TowerPlacer.Instance.ActiveTower == t;

                GUI.enabled = canAfford || isSelected;

                if (isSelected) GUI.backgroundColor = new Color(0.2f, 0.9f, 0.3f, 1f);
                else if (!canAfford) GUI.backgroundColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
                else GUI.backgroundColor = Color.white;

                string hotkeyStr = i < 9 ? $"[{i + 1}] " : "";
                string label = $"{hotkeyStr}<b>{t.DisplayName}</b>\n<color=#FFD700>{t.Cost}g</color>";

                if (GUILayout.Button(label, GUILayout.Width(slotWidth), GUILayout.Height(54)))
                {
                    if (isSelected)
                    {
                        TowerPlacer.Instance.DeselectTower();
                    }
                    else
                    {
                        TowerPlacer.Instance.SelectTower(t);
                    }
                }
            }

            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (TowerPlacer.Instance.ActiveTower != null)
            {
                float tipW = 480f;
                float tipH = 30f;
                float tipX = (Screen.width - tipW) / 2f;
                float tipY = y - tipH - 4f;

                GUI.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
                GUI.Box(new Rect(tipX, tipY, tipW, tipH), $"<size=12><color=#55FF55>Đang chọn: <b>{TowerPlacer.Instance.ActiveTower.DisplayName}</b> | [Chuột Trái] Đặt | [Shift] Đặt nhiều | [Chuột Phải/ESC] Hủy</color></size>");
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawTopLeftStatusBanner()
        {
            int gold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 0;
            float hpPercent = Mathf.Clamp01((float)_CurrentBaseHealth / _MaxBaseHealth);

            GUILayout.BeginArea(new Rect(20, 20, 360, 140), GUI.skin.box);
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
                float x = Screen.width - btnWidth - 25f;
                float y = 25f;

                GUI.backgroundColor = new Color(0.1f, 0.8f, 0.2f, 1f);
                if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), $"<b><size=16>▶ BẮT ĐẦU ĐỢT {_CurrentWave}\n(PHÍM SPACE / BẤM VÀO ĐÂY)</size></b>"))
                {
                    StartNextWave();
                }
                GUI.backgroundColor = Color.white;
            }
            else if (_CurrentPhase == PrehistoricWavePhase.CombatPhase)
            {
                float boxWidth = 240f;
                float boxHeight = 50f;
                float x = Screen.width - boxWidth - 25f;
                float y = 25f;

                GUI.backgroundColor = new Color(0.8f, 0.2f, 0.1f, 0.9f);
                GUI.Box(new Rect(x, y, boxWidth, boxHeight), $"<b><size=15><color=white>⚔ ĐANG CHIẾN ĐẤU ⚔\nQuái còn lại: {MonstersAliveCount}</color></size></b>");
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawScreenAnnouncement()
        {
            if (string.IsNullOrEmpty(ScreenAnnouncement)) return;

            float w = 650f;
            float h = 60f;
            float x = (Screen.width - w) / 2f;
            float y = 75f;

            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Box(new Rect(x, y, w, h), $"<b><size=18>{ScreenAnnouncement}</size></b>");
            GUI.backgroundColor = Color.white;
        }

        private void DrawBossHealthBar()
        {
            float w = 550f;
            float h = 55f;
            float x = (Screen.width - w) / 2f;
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
            // Lớp phủ nền mờ đen toàn màn hình
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float modalW = 600f;
            float modalH = 340f;
            float x = (Screen.width - modalW) / 2f;
            float y = (Screen.height - modalH) / 2f;

            GUI.backgroundColor = new Color(0.28f, 0.05f, 0.05f, 0.98f);
            GUI.Box(new Rect(x, y, modalW, modalH), "");

            GUILayout.BeginArea(new Rect(x + 25, y + 20, modalW - 50, modalH - 40));
            GUILayout.BeginVertical();

            GUILayout.Label("<b><size=22><color=#FF4444>☠ THẤT THỦ! LÀNG TIỀN SỬ ĐÃ BỊ PHÁ HỦY ☠</color></size></b>", GUILayout.ExpandWidth(true));
            GUILayout.Space(8);
            GUILayout.Label($"<size=16>Bầy khủng long hung hãn đã tràn qua tuyến phòng thủ ở <b>Đợt {_CurrentWave} / {_MaxWaves}</b>!</size>");
            GUILayout.Label("<size=14><color=#E0E0E0>Hệ thống phòng thủ cần được tái cấu trúc hoàn toàn từ đầu để bảo vệ ngôi làng.</color></size>");
            GUILayout.Space(22);

            // Nút Thử Lại Duy Nhất: Reset toàn bộ game từ đầu
            GUI.backgroundColor = new Color(0.9f, 0.25f, 0.2f, 1f);
            if (GUILayout.Button("<b><size=18>🔄 THỬ LẠI TỪ ĐẦU (RESET TOÀN BỘ GAME)</size></b>\n<size=13>Phím tắt: [SPACE] / [ENTER] / [R]\n<i>(Reset nhà cửa, tháp đã xây, nhân vật, khủng long, bắt đầu lại từ Đợt 1 với 500 vàng)</i></size>", GUILayout.Height(75)))
            {
                FullResetGame();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }

        private void DrawVictoryModal()
        {
            // Lớp phủ nền mờ
            GUI.color = new Color(0f, 0.12f, 0.05f, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float modalW = 580f;
            float modalH = 300f;
            float x = (Screen.width - modalW) / 2f;
            float y = (Screen.height - modalH) / 2f;

            GUI.backgroundColor = new Color(0.05f, 0.25f, 0.1f, 0.98f);
            GUI.Box(new Rect(x, y, modalW, modalH), "");

            GUILayout.BeginArea(new Rect(x + 25, y + 20, modalW - 50, modalH - 40));
            GUILayout.BeginVertical();

            GUILayout.Label("<b><size=24><color=#FFD700>🏆 CHIẾN THẮNG KHẢI HOÀN! 🏆</color></size></b>", GUILayout.ExpandWidth(true));
            GUILayout.Space(10);
            GUILayout.Label("<size=16>Chúc mừng bạn! Bạo Chúa T-Rex và toàn bộ bầy khủng long đã bị tiêu diệt hoàn toàn!</size>");
            GUILayout.Label($"<size=15>Ngôi làng tiền sử được bảo vệ trọn vẹn với <b>{_CurrentBaseHealth} HP</b> còn lại!</size>");
            GUILayout.Space(20);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f, 1f);
            if (GUILayout.Button("<b><size=18>🎉 CHƠI LẠI TRẬN MỚI [PHÍM SPACE / ENTER / R]</size></b>\n<size=13><i>(Reset ván đấu, bắt đầu một trận mới hoàn toàn từ Đợt 1)</i></size>", GUILayout.Height(65)))
            {
                FullResetGame();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
