using System;
using System.Collections.Generic;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Enemy;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.RoundManagement
{
    public enum PrehistoricGameMode
    {
        TowerDefense,   // Chế độ 1: Thủ Tháp Tiền Sử (Người chơi xây tháp, AI thả khủng long)
        DinoAssault     // Chế độ 2: Khủng Long Công Thành (Người chơi thả khủng long, AI xây tháp thủ)
    }

    public enum PrehistoricAssaultPhase
    {
        Setup,
        Combat,
        Victory,
        Defeat
    }

    [DefaultExecutionOrder(-10)]
    public class PrehistoricGameModeManager : MonoBehaviour
    {
        public static PrehistoricGameModeManager Instance { get; private set; }

        [Header("Active Game Mode")]
        [SerializeField] private PrehistoricGameMode _CurrentMode = PrehistoricGameMode.TowerDefense;
        public PrehistoricGameMode CurrentMode => _CurrentMode;

        [Header("Assault Mode Progression")]
        public int AssaultLevel = 1;
        public PrehistoricAssaultPhase AssaultPhase { get; private set; } = PrehistoricAssaultPhase.Setup;
        public bool IsAssaultActive => _CurrentMode == PrehistoricGameMode.DinoAssault && AssaultPhase == PrehistoricAssaultPhase.Combat;
        public Unit.PrehistoricVillageBase VillageBase { get; private set; }
        private float _DefeatCheckTimer = 0f;

        [Header("Catalog References")]
        [SerializeField] private List<TowerSO> AvailableTowers = new();
        [SerializeField] private List<DinoSO> AvailableDinos = new();

        public event Action<PrehistoricGameMode> OnGameModeChanged;

        private bool ShowModeSelectModal = false;
        private DinoSO SelectedDino = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            EnsureAvailableDinos();
            ApplyModeConfiguration(_CurrentMode);
        }

        public bool IsGameInProgress()
        {
            if (_CurrentMode == PrehistoricGameMode.DinoAssault)
            {
                return AssaultPhase == PrehistoricAssaultPhase.Combat;
            }
            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.enabled)
            {
                return PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.CombatPhase;
            }
            if (RoundManager.Instance != null)
            {
                return RoundManager.Instance.State == GameState.Running;
            }
            return false;
        }

        private void Update()
        {
            // Hotkey 'M' or 'F1' to toggle Mode Select Menu
            if (Keyboard.current != null && (Keyboard.current.mKey.wasPressedThisFrame || Keyboard.current.f1Key.wasPressedThisFrame))
            {
                if (IsGameInProgress())
                {
                    if (PrehistoricGameplayManager.Instance != null)
                    {
                        PrehistoricGameplayManager.Instance.ShowAnnouncement("⚠ ĐANG TRONG TRẬN CHIẾN! KHÔNG THỂ ĐỔI CHẾ ĐỘ!", 2.5f);
                    }
                }
                else
                {
                    ShowModeSelectModal = !ShowModeSelectModal;
                }
            }

            // In Dino Assault mode, handle hotkeys 1-4 for choosing dinos
            if (_CurrentMode == PrehistoricGameMode.DinoAssault && !ShowModeSelectModal)
            {
                HandleDinoHotkeys();

                // Quản lý thắng / thua trong trận công thành
                if (AssaultPhase == PrehistoricAssaultPhase.Combat)
                {
                    if (VillageBase != null && VillageBase.Health <= 0)
                    {
                        TriggerAssaultVictory();
                        return;
                    }

                    int livingDinos = 0;
                    var dinos = FindObjectsByType<Unit.PrehistoricDinoBase>(FindObjectsSortMode.None);
                    foreach (var d in dinos) { if (d != null && d.Health > 0) livingDinos++; }
                    var stds = FindObjectsByType<Unit.Dino>(FindObjectsSortMode.None);
                    foreach (var d in stds) { if (d != null && d.Health > 0) livingDinos++; }

                    DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
                    int food = spawner != null ? spawner.ResourcesToSpend : 0;

                    if (livingDinos == 0 && food < 20)
                    {
                        _DefeatCheckTimer += Time.deltaTime;
                        if (_DefeatCheckTimer >= 5.0f)
                        {
                            TriggerAssaultDefeat();
                        }
                    }
                    else
                    {
                        _DefeatCheckTimer = 0f;
                    }
                }
            }
        }

        public void SetGameMode(PrehistoricGameMode newMode)
        {
            _CurrentMode = newMode;
            ApplyModeConfiguration(_CurrentMode);
            ShowModeSelectModal = false;
            OnGameModeChanged?.Invoke(_CurrentMode);
            Debug.Log($"<color=cyan>[GameModeManager]</color> Switched to Mode: <b>{_CurrentMode}</b>");
        }

        private void ApplyModeConfiguration(PrehistoricGameMode mode)
        {
            // 1. Tower Defense Components
            PrehistoricGameplayManager gameMgr = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance : FindFirstObjectByType<PrehistoricGameplayManager>();
            if (gameMgr != null)
            {
                gameMgr.enabled = (mode == PrehistoricGameMode.TowerDefense);
            }

            TowerPlacer placer = TowerPlacer.Instance != null ? TowerPlacer.Instance : FindFirstObjectByType<TowerPlacer>();
            if (placer != null)
            {
                placer.enabled = (mode == PrehistoricGameMode.TowerDefense);
                if (Application.isPlaying && mode != PrehistoricGameMode.TowerDefense) placer.DeselectTower();
            }

            AdaptiveWaveManager waveMgr = AdaptiveWaveManager.Instance != null ? AdaptiveWaveManager.Instance : FindFirstObjectByType<AdaptiveWaveManager>();
            if (waveMgr != null)
            {
                waveMgr.enabled = (mode == PrehistoricGameMode.TowerDefense);
            }

            // 2. Dino Assault Components
            DinoSpawner dinoSpawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            if (dinoSpawner != null)
            {
                dinoSpawner.enabled = (mode == PrehistoricGameMode.DinoAssault);
                if (mode != PrehistoricGameMode.DinoAssault)
                {
                    if (Application.isPlaying) dinoSpawner.ChangeActiveDino(null);
                }
                else
                {
                    // Ensure food resources available for player to summon dinos (at least 250 so T-Rex can be summoned)
                    if (Application.isPlaying && dinoSpawner.ResourcesToSpend < 250)
                    {
                        dinoSpawner.ResourcesToSpend = 250;
                    }
                }
            }

            EnemyAIController enemyAI = EnemyAIController.Instance != null ? EnemyAIController.Instance : FindFirstObjectByType<EnemyAIController>();
            if (enemyAI != null)
            {
                // Cho phép NPC Defender chiến đấu bảo vệ làng ở cả 2 chế độ!
                enemyAI.enabled = true;
                if (Application.isPlaying)
                {
                    enemyAI.EnsureDefendersAlive(4 + (AssaultLevel - 1) * 2);
                }
            }

            // 3. Battlefield Starter Defenses & Village Base
            GameObject starterDefenses = GameObject.Find("Starter_Defenses");
            if (starterDefenses != null)
            {
                starterDefenses.SetActive(true);
            }

            if (mode == PrehistoricGameMode.DinoAssault)
            {
                EnsureVillageBase();
                AssaultPhase = PrehistoricAssaultPhase.Setup;
                if (RoundManager.Instance != null) RoundManager.Instance.State = GameState.Setup;
            }

            // 4. Dual Camera Perspectives
            if (CameraControl.Instance != null)
            {
                CameraControl.Instance.SetPerspective(mode == PrehistoricGameMode.TowerDefense
                    ? CameraPerspective.Rear_TowerDefense
                    : CameraPerspective.Front_DinoAssault);
            }

            // 5. Mode-Specific UI Visibility
            RuntimeUI runtimeUI = FindFirstObjectByType<RuntimeUI>(FindObjectsInactive.Include);
            if (runtimeUI != null)
            {
                runtimeUI.SetVisible(false);
                runtimeUI.gameObject.SetActive(false);
            }
        }

        private void HandleDinoHotkeys()
        {
            EnsureAvailableDinos();
            if (Keyboard.current == null || AvailableDinos == null || AvailableDinos.Count == 0) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame && AvailableDinos.Count > 0) SelectDino(AvailableDinos[0]);
            if (Keyboard.current.digit2Key.wasPressedThisFrame && AvailableDinos.Count > 1) SelectDino(AvailableDinos[1]);
            if (Keyboard.current.digit3Key.wasPressedThisFrame && AvailableDinos.Count > 2) SelectDino(AvailableDinos[2]);
            if (Keyboard.current.digit4Key.wasPressedThisFrame && AvailableDinos.Count > 3) SelectDino(AvailableDinos[3]);

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SelectDino(null);
            }
        }

        private void SelectDino(DinoSO dino)
        {
            SelectedDino = dino;
            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            if (spawner != null)
            {
                spawner.ChangeActiveDino(dino);
            }
        }

        private void OnGUI()
        {
            // 1. Top Bar: Mode Indicator & Switch Button
            DrawTopModeBanner();

            // 2. Mode Select Modal (when opened)
            if (ShowModeSelectModal)
            {
                DrawModeSelectionModal();
                return;
            }

            // 3. Victory Modal in Dino Assault mode
            if (_CurrentMode == PrehistoricGameMode.DinoAssault && AssaultPhase == PrehistoricAssaultPhase.Victory)
            {
                DrawAssaultVictoryModal();
                return;
            }

            // 4. Defeat Modal in Dino Assault mode
            if (_CurrentMode == PrehistoricGameMode.DinoAssault && AssaultPhase == PrehistoricAssaultPhase.Defeat)
            {
                DrawAssaultDefeatModal();
                return;
            }

            // 5. Mode-Specific Controls: Luôn hiển thị thanh chọn khủng long khi ở chế độ Công Thành
            if (_CurrentMode == PrehistoricGameMode.DinoAssault)
            {
                DrawDinoAssaultHUD();
            }
            else
            {
                DrawTowerDefenseStartRoundButton();
            }
        }

        private void DrawTopModeBanner()
        {
            float width = 390f;
            float height = 52f;
            float x = Screen.width - width - 20f;
            float y = 15f;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
            GUILayout.BeginHorizontal();

            string modeText;
            if (_CurrentMode == PrehistoricGameMode.TowerDefense)
            {
                modeText = "<b><color=#55FF55>🛡️ CHẾ ĐỘ: THỦ THÁP (TD)</color></b>";
            }
            else
            {
                string baseHpStr = (VillageBase != null && VillageBase.Health > 0)
                    ? $" ({VillageBase.Health}/{VillageBase.MaxHealth} HP)"
                    : "";
                modeText = $"<b><color=#FF5555>🦖 CÔNG THÀNH - MÀN {AssaultLevel}</color></b>\n<size=11><color=#FFD700>🏛️ Căn Cứ Làng:{baseHpStr}</color></size>";
            }

            GUILayout.Label(modeText, GUILayout.Height(40));

            bool inCombat = IsGameInProgress();
            if (inCombat)
            {
                GUI.enabled = false;
                GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                GUILayout.Button("<b>[🔒 ĐANG ĐẤU]</b>", GUILayout.Width(115), GUILayout.Height(38));
                GUI.enabled = true;
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                if (GUILayout.Button("<b>[⚙️ ĐỔI CHẾ ĐỘ]</b>", GUILayout.Width(125), GUILayout.Height(38)))
                {
                    ShowModeSelectModal = !ShowModeSelectModal;
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawModeSelectionModal()
        {
            // Dim background
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

            float modalWidth = 640f;
            float modalHeight = 390f;
            float modalX = (Screen.width - modalWidth) / 2f;
            float modalY = (Screen.height - modalHeight) / 2f;

            GUILayout.BeginArea(new Rect(modalX, modalY, modalWidth, modalHeight), GUI.skin.window);

            GUILayout.Space(10);
            GUILayout.Label("<size=20><b><color=#FFD700>⚔️ CHỌN CHẾ ĐỘ CHƠI PREHISTORIC TD ⚔️</color></b></size>", GUI.skin.label);
            GUILayout.Label("<color=#CCCCCC>Chọn luật chơi bạn muốn trải nghiệm bên dưới (Có thể đổi bất kỳ lúc nào bằng phím M):</color>");
            GUILayout.Space(15);

            // Card 1: Tower Defense
            GUI.backgroundColor = _CurrentMode == PrehistoricGameMode.TowerDefense ? new Color(0.2f, 0.8f, 0.3f) : Color.white;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<size=16><b>🛡️ CHẾ ĐỘ 1: THỦ THÁP TIỀN SỬ (TOWER DEFENSE)</b></size>");
            GUILayout.Label("• <b>Vai trò</b>: Bạn là Thủ Lĩnh Bộ Lạc phòng thủ căn cứ/trứng.\n• <b>Hành động</b>: Xây 7 loại tháp, nỏ, máy bắn đá, bẫy chông, rào cọc bằng Vàng.\n• <b>Kẻ địch</b>: Máy (AI) sẽ gửi các đợt khủng long hoang dã tấn công theo Wave.");
            if (GUILayout.Button("<b>👉 VÀO CHƠI THỦ THÁP</b>", GUILayout.Height(38)))
            {
                SetGameMode(PrehistoricGameMode.TowerDefense);
            }
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // Card 2: Dino Assault
            GUI.backgroundColor = _CurrentMode == PrehistoricGameMode.DinoAssault ? new Color(0.9f, 0.4f, 0.2f) : Color.white;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<size=16><b>🦖 CHẾ ĐỘ 2: BẦY KHỦNG LONG CÔNG THÀNH (DINO ASSAULT)</b></size>");
            GUILayout.Label("• <b>Vai trò</b>: Bạn là Chúa Tể Khủng Long tấn công căn cứ đối phương.\n• <b>Hành động</b>: Dùng Thức Ăn (Thịt) để chiêu mộ Velociraptor, Ankylosaurus, T-Rex.\n• <b>Kẻ địch</b>: Máy (AI) xây dựng thành lũy và tháp canh chống trả bạn.");
            if (GUILayout.Button("<b>👉 VÀO CHƠI CÔNG THÀNH</b>", GUILayout.Height(38)))
            {
                SetGameMode(PrehistoricGameMode.DinoAssault);
            }
            GUILayout.EndVertical();

            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            if (GUILayout.Button("Đóng Menu [ESC]", GUILayout.Height(30)))
            {
                ShowModeSelectModal = false;
            }

            GUILayout.EndArea();
        }

        private void EnsureAvailableDinos()
        {
            if (AvailableDinos == null) AvailableDinos = new List<DinoSO>();
            if (AvailableDinos.Count > 0) return;

            // 1. Quét tìm từ AdaptiveWaveManager MonsterCatalog
            AdaptiveWaveManager waveMgr = AdaptiveWaveManager.Instance != null ? AdaptiveWaveManager.Instance : FindFirstObjectByType<AdaptiveWaveManager>();
            if (waveMgr != null && waveMgr.Catalog != null)
            {
                foreach (var arch in waveMgr.Catalog)
                {
                    if (arch != null && arch.UnitSO != null && !AvailableDinos.Contains(arch.UnitSO))
                    {
                        AvailableDinos.Add(arch.UnitSO);
                    }
                }
            }

            // 2. Tìm tất cả các DinoSO đã load trong bộ nhớ Resources
            if (AvailableDinos.Count == 0)
            {
                DinoSO[] allDinos = Resources.FindObjectsOfTypeAll<DinoSO>();
                foreach (var d in allDinos)
                {
                    if (d != null && !AvailableDinos.Contains(d))
                    {
                        AvailableDinos.Add(d);
                    }
                }
            }

#if UNITY_EDITOR
            // 3. Fallback: Quét tìm toàn bộ asset DinoSO trong thư mục Config của dự án
            if (AvailableDinos.Count == 0)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DinoSO");
                foreach (string g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    DinoSO d = UnityEditor.AssetDatabase.LoadAssetAtPath<DinoSO>(path);
                    if (d != null && !AvailableDinos.Contains(d))
                    {
                        AvailableDinos.Add(d);
                    }
                }
            }
#endif

            // Sắp xếp thứ tự theo lượng thịt (Velociraptor -> Pterodactyl -> Ankylosaurus -> T-Rex)
            AvailableDinos.Sort((a, b) => a.Cost.CompareTo(b.Cost));
        }

        private static string GetDinoDisplayName(DinoSO d)
        {
            if (d == null) return "Dino";
            string n = d.name.ToLower();
            if (n.Contains("trex") || n.Contains("t-rex")) return "👑 T-Rex Bạo Chúa";
            if (n.Contains("ptero")) return "🦅 Thằn Lằn Bay";
            if (n.Contains("ankyl")) return "🛡️ Khủng Long Giáp";
            if (n.Contains("raptor")) return "🦖 Velociraptor";
            return d.name;
        }

        private void DrawDinoAssaultHUD()
        {
            EnsureAvailableDinos();

            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            int food = spawner != null ? spawner.ResourcesToSpend : 0;

            // 1. Top-Left Food & Command Banner
            GUILayout.BeginArea(new Rect(20, 20, 360, 130), GUI.skin.box);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b><size=17><color=#FF6644>🥩 THỨC ĂN (MEAT): {food}</color></size></b>");
            if (GUILayout.Button("+100 Thịt", GUILayout.Width(80), GUILayout.Height(26)))
            {
                if (spawner != null) spawner.ResourcesToSpend += 100;
            }
            GUILayout.EndHorizontal();

            if (SelectedDino != null)
            {
                string dName = GetDinoDisplayName(SelectedDino);
                GUILayout.Label($"<color=#55FF55><b>Đang chọn:</b> {dName} ({SelectedDino.Cost} thịt)</color>");
                GUILayout.Label("<size=11><i>[Click chuột trái] Thả quân vào đường | [ESC] Hủy</i></size>");
            }
            else
            {
                GUILayout.Label("<color=#CCCCCC>Chọn một loài khủng long bên dưới (hoặc bấm 1-4) để thả quân:</color>");
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();

            // 2. Bottom Unified Dino Command Dock (Thanh điều khiển khủng long & Xuất quân thống nhất)
            if (AvailableDinos != null && AvailableDinos.Count > 0)
            {
                float cardWidth = 145f;
                float actionBtnWidth = 175f;
                float totalWidth = AvailableDinos.Count * cardWidth + actionBtnWidth + 24f;
                float startX = (Screen.width - totalWidth) / 2f;
                float startY = Screen.height - 85f;

                GUILayout.BeginArea(new Rect(startX, startY, totalWidth, 75), GUI.skin.box);
                GUILayout.BeginHorizontal();

                for (int i = 0; i < AvailableDinos.Count; i++)
                {
                    DinoSO d = AvailableDinos[i];
                    if (d == null) continue;

                    bool canAfford = food >= d.Cost;
                    bool isSelected = SelectedDino == d;

                    string displayName = GetDinoDisplayName(d);
                    string btnLabel = $"<b>[{i + 1}] {displayName}</b>\n<color={(canAfford ? "#FFD700" : "#FFAAAA")}>{d.Cost} thịt</color>";

                    if (isSelected) GUI.backgroundColor = new Color(1f, 0.3f, 0.2f);
                    else GUI.backgroundColor = canAfford ? new Color(0.2f, 0.25f, 0.3f, 0.95f) : new Color(0.4f, 0.4f, 0.4f, 0.6f);

                    if (GUILayout.Button(btnLabel, GUILayout.Height(55), GUILayout.Width(cardWidth)))
                    {
                        SelectDino(isSelected ? null : d);
                    }
                }

                // Nút Xuất Quân đặt ngay bên phải thanh chọn quân, loại bỏ 100% tình trạng chồng chéo
                bool isSetupPhase = RoundManager.Instance == null || RoundManager.Instance.State == GameState.Setup;
                if (isSetupPhase)
                {
                    GUI.backgroundColor = new Color(1f, 0.4f, 0.15f);
                    if (GUILayout.Button("<b><size=14>⚔️ XUẤT QUÂN\n(START ASSAULT)</size></b>", GUILayout.Height(55), GUILayout.Width(actionBtnWidth)))
                    {
                        if (RoundManager.Instance != null)
                        {
                            RoundManager.Instance.StartRound();
                        }
                        else
                        {
                            LaunchAssault();
                        }
                    }
                }
                else
                {
                    GUILayout.BeginVertical(GUILayout.Width(actionBtnWidth));
                    GUI.backgroundColor = new Color(0.2f, 0.75f, 0.3f);
                    if (GUILayout.Button("<b>📢 TỔNG TIẾN CÔNG</b>", GUILayout.Height(30)))
                    {
                        OrderAllCharge();
                    }
                    GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f);
                    if (GUILayout.Button("<b>🏳️ ĐẦU HÀNG / LÀM LẠI</b>", GUILayout.Height(24)))
                    {
                        RetryCurrentAssaultLevel();
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndVertical();
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        public void EnsureVillageBase()
        {
            if (VillageBase != null && VillageBase.gameObject != null)
            {
                VillageBase.OnBaseDestroyed -= HandleVillageBaseDestroyed;
                VillageBase.OnBaseDestroyed += HandleVillageBaseDestroyed;
                return;
            }

            VillageBase = FindFirstObjectByType<Unit.PrehistoricVillageBase>();
            if (VillageBase != null)
            {
                VillageBase.OnBaseDestroyed -= HandleVillageBaseDestroyed;
                VillageBase.OnBaseDestroyed += HandleVillageBaseDestroyed;
                return;
            }

            GameObject eggObj = GameObject.Find("Dino Egg Spawn");
            if (eggObj == null && RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
            {
                eggObj = RoundManager.Instance.DinoTarget.gameObject;
            }

            if (eggObj != null)
            {
                VillageBase = eggObj.GetComponent<Unit.PrehistoricVillageBase>();
                if (VillageBase == null)
                {
                    VillageBase = eggObj.AddComponent<Unit.PrehistoricVillageBase>();
                }
            }
            else
            {
                GameObject baseObj = new GameObject("PrehistoricVillageBase");
                baseObj.transform.position = new Vector3(-2.23f, 0f, -38.26f);
                var col = baseObj.AddComponent<BoxCollider>();
                col.size = new Vector3(4f, 3f, 4f);
                col.center = new Vector3(0f, 1.5f, 0f);
                VillageBase = baseObj.AddComponent<Unit.PrehistoricVillageBase>();
            }

            if (VillageBase != null)
            {
                int baseHp = 1000 + (AssaultLevel - 1) * 350;
                VillageBase.SetBaseStats(baseHp);
                VillageBase.EnsureHealthBarAttached();
                VillageBase.OnBaseDestroyed -= HandleVillageBaseDestroyed;
                VillageBase.OnBaseDestroyed += HandleVillageBaseDestroyed;
            }
        }

        public void LaunchAssault()
        {
            AssaultPhase = PrehistoricAssaultPhase.Combat;
            _DefeatCheckTimer = 0f;
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Running;
            }

            EnsureVillageBase();
            OrderAllCharge();

            Debug.Log($"<color=orange>[Assault Mode]</color> Xuất quân thành công màn {AssaultLevel}!");
        }

        public void OrderAllCharge()
        {
            Vector3 targetPos = (VillageBase != null)
                ? VillageBase.transform.position
                : (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null
                    ? RoundManager.Instance.DinoTarget.position
                    : new Vector3(-2.23f, 0f, -38.26f));

            var allDinos = FindObjectsByType<Unit.PrehistoricDinoBase>(FindObjectsSortMode.None);
            foreach (var dino in allDinos)
            {
                if (dino == null || dino.Health <= 0) continue;
                var agent = dino.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(targetPos);
                }
                dino.SetDestination(targetPos);
            }

            var stdDinos = FindObjectsByType<Unit.Dino>(FindObjectsSortMode.None);
            foreach (var dino in stdDinos)
            {
                if (dino == null || dino.Health <= 0) continue;
                var agent = dino.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(targetPos);
                }
                dino.SetDestination(targetPos);
            }
        }

        private void HandleVillageBaseDestroyed()
        {
            TriggerAssaultVictory();
        }

        public void TriggerAssaultVictory()
        {
            if (AssaultPhase == PrehistoricAssaultPhase.Victory) return;
            AssaultPhase = PrehistoricAssaultPhase.Victory;
            Debug.Log($"<color=green>[Assault Victory]</color> Chiếm thành thành công màn {AssaultLevel}!");

            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.ShowAnnouncement("🎉 CHIẾM THÀNH THÀNH CÔNG! LÀNG BỘ LẠC ĐÃ BỊ SAN BẰNG!", 6f);
            }
        }

        public void TriggerAssaultDefeat()
        {
            if (AssaultPhase == PrehistoricAssaultPhase.Defeat || AssaultPhase == PrehistoricAssaultPhase.Victory) return;
            AssaultPhase = PrehistoricAssaultPhase.Defeat;
            Debug.Log($"<color=red>[Assault Defeat]</color> Toàn bộ khủng long đã tử trận màn {AssaultLevel}!");
        }

        public void NextAssaultLevel()
        {
            AssaultLevel++;
            ClearAllDinos();

            // Tăng viện quân phòng thủ NPC của làng
            EnemyAIController enemyAI = EnemyAIController.Instance != null ? EnemyAIController.Instance : FindFirstObjectByType<EnemyAIController>();
            if (enemyAI != null)
            {
                enemyAI.ClearAllDefenders();
                enemyAI.EnsureDefendersAlive(4 + (AssaultLevel - 1) * 2);
            }

            // Tăng máu nhà chính làng
            EnsureVillageBase();
            if (VillageBase != null)
            {
                VillageBase.gameObject.SetActive(true);
                int newHp = 1000 + (AssaultLevel - 1) * 350;
                VillageBase.SetBaseStats(newHp);
                VillageBase.EnsureHealthBarAttached();
            }

            // Thưởng thịt cho người chơi
            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            if (spawner != null)
            {
                spawner.ResourcesToSpend += 250;
            }

            AssaultPhase = PrehistoricAssaultPhase.Setup;
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Setup;
            }

            Debug.Log($"<color=cyan>[Assault Level Up]</color> Tiến vào Màn {AssaultLevel} thành công! NPC phòng thủ tăng, Máu Làng tăng, nhận +250 Thịt!");
        }

        public void RetryCurrentAssaultLevel()
        {
            ClearAllDinos();

            // Hồi sinh quân phòng thủ của màn này
            EnemyAIController enemyAI = EnemyAIController.Instance != null ? EnemyAIController.Instance : FindFirstObjectByType<EnemyAIController>();
            if (enemyAI != null)
            {
                enemyAI.ClearAllDefenders();
                enemyAI.EnsureDefendersAlive(4 + (AssaultLevel - 1) * 2);
            }

            // Hồi phục 100% máu nhà chính
            EnsureVillageBase();
            if (VillageBase != null)
            {
                VillageBase.gameObject.SetActive(true);
                int baseHp = 1000 + (AssaultLevel - 1) * 350;
                VillageBase.SetBaseStats(baseHp);
                VillageBase.EnsureHealthBarAttached();
            }

            // Hồi phục lượng thịt tối thiểu để người chơi thử nghiệm lại
            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            if (spawner != null && spawner.ResourcesToSpend < 250)
            {
                spawner.ResourcesToSpend = 250;
            }

            AssaultPhase = PrehistoricAssaultPhase.Setup;
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Setup;
            }
        }

        private void ClearAllDinos()
        {
            var allDinos = FindObjectsByType<Unit.PrehistoricDinoBase>(FindObjectsSortMode.None);
            foreach (var d in allDinos)
            {
                if (d != null) Destroy(d.gameObject);
            }
            var stdDinos = FindObjectsByType<Unit.Dino>(FindObjectsSortMode.None);
            foreach (var d in stdDinos)
            {
                if (d != null) Destroy(d.gameObject);
            }
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.ClearActiveDinos();
            }
        }

        private void DrawAssaultVictoryModal()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

            float width = 640f;
            float height = 370f;
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.window);
            GUILayout.Space(10);
            GUILayout.Label("<size=22><b><color=#FFD700>🏆 CHIẾM THÀNH THÀNH CÔNG! 🏆</color></b></size>", GUI.skin.label);
            GUILayout.Label($"<size=15>Bầy khủng long của bạn đã san phẳng Căn Cứ Làng Bộ Lạc ở <b>Màn {AssaultLevel}</b>!</size>");
            GUILayout.Space(12);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<size=14><b><color=#55FF55>📊 THÔNG SỐ ĐỘ KHÓ MÀN TIẾP THEO (MÀN " + (AssaultLevel + 1) + "):</color></b></size>");
            GUILayout.Label($"• <b>Quân phòng thủ Làng (NPC)</b>: +2 Thợ săn / Cung thủ (Tổng: {4 + AssaultLevel * 2} quân)\n" +
                            $"• <b>Máu Căn Cứ Làng</b>: {1000 + AssaultLevel * 350} HP (+350 HP)\n" +
                            $"• <b>Chi viện lương thực</b>: +250 Thịt chiêu mộ bầy khủng long!");
            GUILayout.EndVertical();

            GUILayout.Space(16);
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
            if (GUILayout.Button("<b><size=14>⚔️ QUA MÀN KẾ TIẾP\n(TĂNG ĐỘ KHÓ)</size></b>", GUILayout.Height(55)))
            {
                NextAssaultLevel();
            }

            GUI.backgroundColor = new Color(0.9f, 0.45f, 0.2f);
            if (GUILayout.Button("<b><size=14>🔄 CHƠI LẠI MÀN NÀY\n(REPLAY LEVEL)</size></b>", GUILayout.Height(55)))
            {
                RetryCurrentAssaultLevel();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawAssaultDefeatModal()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

            float width = 560f;
            float height = 300f;
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.window);
            GUILayout.Space(10);
            GUILayout.Label("<size=22><b><color=#FF4444>💀 BẦY KHỦNG LONG BỊ ĐẨY LÙI! 💀</color></b></size>", GUI.skin.label);
            GUILayout.Label($"<size=14>Toàn bộ khủng long đã ngã xuống trước phòng tuyến kiên cố của Làng ở Màn {AssaultLevel}!</size>");
            GUILayout.Space(15);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("💡 <i>Gợi ý chiến thuật: Hãy chiêu mộ Khủng Long Giáp (Ankylosaurus) đi trước hứng sát thương từ tháp canh, kết hợp Velociraptor tốc độ cao và T-Rex phá hủy căn cứ!</i>");
            GUILayout.EndVertical();

            GUILayout.Space(18);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.2f);
            if (GUILayout.Button("<b><size=15>🔄 THỬ LẠI MÀN NÀY</size></b>", GUILayout.Height(50)))
            {
                RetryCurrentAssaultLevel();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndArea();
        }

        private void DrawTowerDefenseStartRoundButton()
        {
            if (PrehistoricGameplayManager.Instance != null) return;

            // Draw Next Wave button in Tower Defense mode if in setup state
            bool isSetupPhase = RoundManager.Instance == null || RoundManager.Instance.State == GameState.Setup;
            if (isSetupPhase)
            {
                float btnWidth = 220f;
                float btnHeight = 55f;
                float btnX = Screen.width - btnWidth - 25f;
                float btnY = Screen.height - btnHeight - 25f;

                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
                if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "<b><size=15>🛡️ GỌI ĐỢT QUÁI\n(START WAVE)</size></b>"))
                {
                    if (RoundManager.Instance != null)
                    {
                        RoundManager.Instance.StartRound();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }
    }
}
