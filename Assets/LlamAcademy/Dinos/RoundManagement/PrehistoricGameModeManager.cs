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

            EnsureAudioAndSettings();
        }

        private void EnsureAudioAndSettings()
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
            EnsureAvailableDinos();
            ApplyModeConfiguration(_CurrentMode);

            if (DinoSpawner.Instance != null)
            {
                DinoSpawner.Instance.OnSpawnDino += OnPlayerDinoSpawned;
            }
        }

        private void OnDestroy()
        {
            if (DinoSpawner.Instance != null)
            {
                DinoSpawner.Instance.OnSpawnDino -= OnPlayerDinoSpawned;
            }
        }

        private void OnPlayerDinoSpawned(Unit.Unit dino)
        {
            if (_CurrentMode != PrehistoricGameMode.DinoAssault || dino == null) return;

            if (dino is Unit.BossDino)
            {
                Debug.LogWarning("<color=red><b>[AI Chỉ Huy Làng]</b></color> BÁO ĐỘNG ĐỎ: T-Rex Apex xuất trận! Toàn bộ cung thủ và lính giáo tập trung hỏa lực!");
                if (PrehistoricGameplayManager.Instance != null)
                {
                    PrehistoricGameplayManager.Instance.ShowAnnouncement("⚠ BÁO ĐỘNG ĐỎ: T-REX XUẤT HIỆN! CHIẾN BINH TẬP TRUNG HỎA LỰC!", 3.5f);
                }
            }
            else if (dino is Unit.SiegeDino)
            {
                Debug.Log("<color=orange><b>[AI Chỉ Huy Làng]</b></color> Phát hiện Khủng Long Giáp (Ankylosaurus)! Quân cố thủ dàn đội hình chống phá thành!");
                if (EnemyAIController.Instance != null)
                {
                    EnemyAIController.Instance.EnsureDefendersAlive(PrehistoricGameplayManager.GetTargetDefendersForWave(AssaultLevel));
                }
            }
            else if (dino is Unit.RunnerDino)
            {
                Debug.Log("<color=yellow><b>[AI Chỉ Huy Làng]</b></color> Phát hiện bầy Velociraptor tốc độ cao! Cung thủ tiền tiêu chuẩn bị xạ tiễn!");
            }
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
            if (UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen)
            {
                return;
            }
            // Phím tắt M / F1 đổi chế độ trong trận đã được chuyển ra Menu Chính
            if (Keyboard.current != null && (Keyboard.current.mKey.wasPressedThisFrame))
            {
                if (PrehistoricGameplayManager.Instance != null)
                {
                    PrehistoricGameplayManager.Instance.ShowAnnouncement("ℹ Muốn đổi chế độ chơi: Hãy mở Cài đặt (Esc) và bấm [Menu Chính]!", 2.5f);
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
                    var flyings = FindObjectsByType<Unit.FlyingUnit>(FindObjectsSortMode.None);
                    foreach (var f in flyings) { if (f != null && f.Health > 0) livingDinos++; }

                    DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
                    int food = spawner != null ? spawner.ResourcesToSpend : 0;

                    if (livingDinos == 0 && food < 15)
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
                else if (AssaultPhase == PrehistoricAssaultPhase.Victory || AssaultPhase == PrehistoricAssaultPhase.Defeat)
                {
                    bool isEscPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                    bool isQPressed = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
                    if (isEscPressed || isQPressed)
                    {
                        QuitGame();
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
            Audio.PrehistoricAudioManager.Instance?.PlayBgmForMode(_CurrentMode);
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
                // Tắt triệt để mọi Coroutine của AdaptiveWaveManager để không bao giờ sinh quái ngẫu nhiên
                waveMgr.StopAllCoroutines();
                waveMgr.enabled = false;
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
                    // Khởi tạo lượng thịt ban đầu dồi dào (250 thịt cơ bản + 75 thịt mỗi cấp màn)
                    int targetMeat = 250 + (AssaultLevel - 1) * 75;
                    if (Application.isPlaying && dinoSpawner.ResourcesToSpend < targetMeat)
                    {
                        dinoSpawner.ResourcesToSpend = targetMeat;
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
                    enemyAI.ResetDefendersToExactCount(PrehistoricGameplayManager.GetTargetDefendersForWave(AssaultLevel));
                }
            }

            // 3. Xóa các đối tượng cũ/ẩn và đảm bảo phòng tuyến làng hoàn chỉnh
            GameObject oldWaypoints = GameObject.Find("Archer Waypoints");
            if (oldWaypoints != null) Destroy(oldWaypoints);
            GameObject starterDefenses = GameObject.Find("Starter_Defenses");
            if (starterDefenses != null) Destroy(starterDefenses);
            foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (r != null && r.gameObject != null && r.gameObject.name.Contains("NavMeshVisualization"))
                {
                    r.enabled = false;
                    r.gameObject.SetActive(false);
                    Destroy(r.gameObject);
                }
            }

            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.EnsureVillageFortressWallActive();
                PrehistoricGameplayManager.Instance.ConfigureAllCustomBarricades();
                PrehistoricGameplayManager.Instance.EnsureVillageInteriorDefenses();
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
            if (dino != null)
            {
                Audio.PrehistoricAudioManager.Instance?.PlayDinoSound(dino);
            }
        }

        private void OnGUI()
        {
            if (UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen)
            {
                return;
            }
            Mobile.MobileUIHelper.BeginScaling();
            try
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
            finally
            {
                Mobile.MobileUIHelper.EndScaling();
            }
        }

        private void DrawTopModeBanner()
        {
            // Bố trí ở góc trên bên trái, nằm ngay cạnh nút Cài Đặt/Menu để giải phóng toàn bộ góc phải
            float width = 205f;
            float height = 34f;
            float x = 175f;
            float y = 168f;

            Rect bannerRect = new Rect(x, y, width, height);
            Mobile.MobileUIHelper.RegisterUIRect(bannerRect);

            string modeText;
            if (_CurrentMode == PrehistoricGameMode.TowerDefense)
            {
                modeText = "<b><color=#55FF55>🛡️ THỦ THÀNH</color></b> <size=10><color=#CCCCCC>(Đổi Mode)</color></size>";
            }
            else
            {
                string baseHpStr = (VillageBase != null && VillageBase.Health > 0) ? $" ({VillageBase.Health} HP)" : "";
                modeText = $"<b><color=#FF7755>🦖 CÔNG THÀNH M{AssaultLevel}</color></b><size=10><color=#FFD700>{baseHpStr}</color></size>";
            }

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.15f, 0.22f, 0.30f, 0.90f);

            if (GUI.Button(bannerRect, modeText))
            {
                ShowModeSelectModal = !ShowModeSelectModal;
            }

            GUI.backgroundColor = oldBg;
        }

        private void DrawModeSelectionModal()
        {
            // Dim background
            GUI.Box(new Rect(0, 0, Mobile.MobileUIHelper.ScreenWidth, Mobile.MobileUIHelper.ScreenHeight), GUIContent.none);

            float modalWidth = Mathf.Min(640f, Mobile.MobileUIHelper.ScreenWidth - 24f);
            float modalHeight = 390f;
            float modalX = (Mobile.MobileUIHelper.ScreenWidth - modalWidth) / 2f;
            float modalY = (Mobile.MobileUIHelper.ScreenHeight - modalHeight) / 2f;

            Rect modalRect = new Rect(modalX, modalY, modalWidth, modalHeight);
            Mobile.MobileUIHelper.RegisterUIRect(modalRect);

            GUILayout.BeginArea(modalRect, GUI.skin.window);

            GUILayout.Space(10);
            GUILayout.Label("<size=20><b><color=#FFD700>⚔️ CHỌN CHẾ ĐỘ CHƠI PREHISTORIC TD ⚔️</color></b></size>", GUI.skin.label);
            string escTip = Mobile.MobileUIHelper.IsMobile ? "Chọn luật chơi bạn muốn trải nghiệm bên dưới:" : "Chọn luật chơi bạn muốn trải nghiệm bên dưới (Có thể đổi bất kỳ lúc nào bằng phím M):";
            GUILayout.Label($"<color=#CCCCCC>{escTip}</color>");
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
            string closeLabel = Mobile.MobileUIHelper.IsMobile ? "Đóng Menu" : "Đóng Menu [ESC]";
            if (GUILayout.Button(closeLabel, GUILayout.Height(30)))
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
                float maxDockW = Mobile.MobileUIHelper.ScreenWidth - 24f;
                float baseTotalW = AvailableDinos.Count * cardWidth + actionBtnWidth + 24f;
                if (baseTotalW > maxDockW)
                {
                    float shrinkRatio = maxDockW / baseTotalW;
                    cardWidth *= shrinkRatio;
                    actionBtnWidth *= shrinkRatio;
                }
                float totalWidth = AvailableDinos.Count * cardWidth + actionBtnWidth + 24f;
                float startX = (Mobile.MobileUIHelper.ScreenWidth - totalWidth) / 2f;
                float startY = Mobile.MobileUIHelper.ScreenHeight - 85f;

                Rect dockRect = new Rect(startX, startY, totalWidth, 75);
                Mobile.MobileUIHelper.RegisterUIRect(dockRect);

                GUILayout.BeginArea(dockRect, GUI.skin.box);
                GUILayout.BeginHorizontal();

                for (int i = 0; i < AvailableDinos.Count; i++)
                {
                    DinoSO d = AvailableDinos[i];
                    if (d == null) continue;

                    bool canAfford = food >= d.Cost;
                    bool isSelected = SelectedDino == d;

                    string displayName = GetDinoDisplayName(d);
                    string hotkey = (!Mobile.MobileUIHelper.IsMobile) ? $"[{i + 1}] " : "";
                    string btnLabel = $"<b>{hotkey}{displayName}</b>\n<color={(canAfford ? "#FFD700" : "#FFAAAA")}>{d.Cost} thịt</color>";

                    if (isSelected) GUI.backgroundColor = new Color(1f, 0.3f, 0.2f);
                    else GUI.backgroundColor = canAfford ? new Color(0.2f, 0.25f, 0.3f, 0.95f) : new Color(0.4f, 0.4f, 0.4f, 0.6f);

                    if (GUILayout.Button(btnLabel, GUILayout.Height(55), GUILayout.Width(cardWidth)))
                    {
                        SelectDino(isSelected ? null : d);
                        Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
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
                        Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
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

            // 3. Right-Side Utility Dock in Assault Mode (Đồng bộ với chế độ Thủ thành)
            float dockWidth = 165f;
            float rightX = Mobile.MobileUIHelper.ScreenWidth - dockWidth - 16f;
            float dockY = 208f;
            float btnH = 28f;

            // Nút Bật/Tắt AI Director
            Rect aiRect = new Rect(rightX, dockY, dockWidth, btnH);
            Mobile.MobileUIHelper.RegisterUIRect(aiRect);
            bool isAiOpen = UI.PrehistoricNeuralMonitorUI.Instance != null && UI.PrehistoricNeuralMonitorUI.Instance.IsOpen;
            GUI.backgroundColor = isAiOpen ? new Color(0.2f, 0.9f, 0.6f, 0.95f) : new Color(0.12f, 0.28f, 0.36f, 0.88f);
            if (GUI.Button(aiRect, isAiOpen ? "<b>🧠 AI Monitor: [BẬT]</b>" : "<b>🧠 AI Director [F1]</b>"))
            {
                UI.PrehistoricNeuralMonitorUI.Instance?.ToggleWindow();
            }
            dockY += btnH + 6f;

            // Nút Chuyển Đổi Chế Độ AR
            Rect arRect = new Rect(rightX, dockY, dockWidth, btnH);
            Mobile.MobileUIHelper.RegisterUIRect(arRect);
            bool isArActive = AR.ARTabletopController.Instance != null && AR.ARTabletopController.Instance.IsARModeActive;
            GUI.backgroundColor = isArActive ? new Color(0.9f, 0.4f, 0.2f, 0.95f) : new Color(0.15f, 0.35f, 0.75f, 0.88f);
            if (GUI.Button(arRect, isArActive ? "<b>🔙 THOÁT AR (3D)</b>" : "<b>📱 CHẾ ĐỘ AR</b>"))
            {
                AR.ARTabletopController.Instance?.ToggleARMode();
            }
            dockY += btnH + 6f;

            // Nút Toàn Màn Hình [F11] trên PC
            if (!Mobile.MobileUIHelper.IsMobile)
            {
                Rect fsRect = new Rect(rightX, dockY, dockWidth, btnH);
                Mobile.MobileUIHelper.RegisterUIRect(fsRect);
                GUI.backgroundColor = Screen.fullScreen ? new Color(0.2f, 0.7f, 0.9f, 0.85f) : new Color(0.22f, 0.24f, 0.28f, 0.88f);
                string fsLabel = Screen.fullScreen ? "<b>⛶ CỬA SỔ [F11]</b>" : "<b>⛶ FULLSCREEN [F11]</b>";
                if (GUI.Button(fsRect, fsLabel))
                {
                    Screen.fullScreen = !Screen.fullScreen;
                }
            }
            GUI.backgroundColor = Color.white;
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

            // TÁCH BIỆT TRIỆT ĐỂ: Dừng hoàn toàn AdaptiveWaveManager và PrehistoricGameplayManager,
            // đảm bảo KHÔNG MỘT CON KHỦNG LONG LẠ NÀO tự ý xuất hiện khi bấm Xuất quân
            AdaptiveWaveManager waveMgr = AdaptiveWaveManager.Instance != null ? AdaptiveWaveManager.Instance : FindFirstObjectByType<AdaptiveWaveManager>();
            if (waveMgr != null)
            {
                waveMgr.StopAllCoroutines();
                waveMgr.enabled = false;
            }
            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.StopAllCoroutines();
            }

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Running;
            }

            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.ClearAll();
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
                dino.EnsureOnNavMesh(15f);
                dino.ConfigureNavMeshAgent();
                if (dino.Agent != null && dino.Agent.isOnNavMesh)
                {
                    dino.Agent.isStopped = false;
                    dino.Agent.SetDestination(targetPos);
                }
                dino.SetDestination(targetPos);
            }

            var flyingUnits = FindObjectsByType<Unit.FlyingUnit>(FindObjectsSortMode.None);
            foreach (var fly in flyingUnits)
            {
                if (fly == null || fly.Health <= 0) continue;
                fly.SetDestination(targetPos);
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

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1.0f;
            Save.PrehistoricSaveManager.Instance?.AutoSaveCurrentRun(hasActiveRun: true);
            ClearAllDinos();

            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.ReturnToMainMenu();
            }
            else
            {
                if (UI.PrehistoricMainMenu.Instance != null)
                {
                    UI.PrehistoricMainMenu.Instance.IsOpen = true;
                }
                Audio.PrehistoricAudioManager.Instance?.PlayOpeningBgm();
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void TriggerAssaultVictory()
        {
            if (AssaultPhase == PrehistoricAssaultPhase.Victory) return;
            AssaultPhase = PrehistoricAssaultPhase.Victory;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Audio.PrehistoricAudioManager.Instance?.PlayVictory();
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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Audio.PrehistoricAudioManager.Instance?.PlayGameOver();
            Debug.Log($"<color=red>[Assault Defeat]</color> Toàn bộ khủng long đã tử trận màn {AssaultLevel}!");
        }

        private struct DefenseSpawnSpec
        {
            public string PrefabName;
            public Vector3 Position;
            public Quaternion Rotation;
            public int MinLevel;

            public DefenseSpawnSpec(string prefabName, Vector3 pos, Quaternion rot, int minLevel)
            {
                PrefabName = prefabName;
                Position = pos;
                Rotation = rot;
                MinLevel = minLevel;
            }
        }

        private static readonly DefenseSpawnSpec[] VillageDefenseSpecs = new DefenseSpawnSpec[]
        {
            // Level 1: Tiền đồn mở đầu, Máy bắn đá, Cột sét Shaman & Bẫy chông (2 Chòi Cung Thủ tiền tiêu đã luôn có sẵn trên tường thành)
            new DefenseSpawnSpec("Prefab_Catapult", new Vector3(-5.5f, 0f, -6f), Quaternion.Euler(0, 45, 0), 1),
            new DefenseSpawnSpec("Prefab_ShamanTotem", new Vector3(-5.5f, 0f, -1f), Quaternion.identity, 1),
            new DefenseSpawnSpec("Prefab_SpikeTrap", new Vector3(-2.2f, 0.1f, -8.5f), Quaternion.identity, 1),
            new DefenseSpawnSpec("Prefab_Barricade", new Vector3(-2.2f, 0f, -12.0f), Quaternion.Euler(0, 90, 0), 1),

            // Level 2: Tăng cường Tháp nỏ tầm xa (Ballista), thêm Máy bắn đá & Cột sét
            new DefenseSpawnSpec("Prefab_Ballista", new Vector3(5.5f, 0f, -14.0f), Quaternion.Euler(0, -45, 0), 2),
            new DefenseSpawnSpec("Prefab_Catapult", new Vector3(-6.5f, 0f, -18.0f), Quaternion.Euler(0, 35, 0), 2),
            new DefenseSpawnSpec("Prefab_ShamanTotem", new Vector3(6.0f, 0f, -22.0f), Quaternion.identity, 2),
            new DefenseSpawnSpec("Prefab_SpikeTrap", new Vector3(-2.2f, 0.1f, -16.0f), Quaternion.identity, 2),
            new DefenseSpawnSpec("Prefab_Barricade", new Vector3(-2.2f, 0f, -20.0f), Quaternion.Euler(0, 90, 0), 2),

            // Level 3: Pháo đài lõi - Tháp nỏ khổng lồ, Cột bão sét & Máy bắn đá
            new DefenseSpawnSpec("Prefab_Ballista", new Vector3(-6.5f, 0f, -26.0f), Quaternion.Euler(0, 40, 0), 3),
            new DefenseSpawnSpec("Prefab_ShamanTotem", new Vector3(-5.0f, 0f, -32.0f), Quaternion.identity, 3),
            new DefenseSpawnSpec("Prefab_Catapult", new Vector3(5.0f, 0f, -34.0f), Quaternion.Euler(0, -35, 0), 3),
            new DefenseSpawnSpec("Prefab_SpikeTrap", new Vector3(-2.2f, 0.1f, -25.0f), Quaternion.identity, 3),
            new DefenseSpawnSpec("Prefab_Barricade", new Vector3(-2.2f, 0f, -30.0f), Quaternion.Euler(0, 90, 0), 3)
        };

        public void SetupVillageDefensesForLevel(int level)
        {
            // 0. Phục hồi 100% tất cả tường thành và rào chắn (kể cả những tường đã bị phá hủy / vô hiệu hóa ở màn trước)
            var allWalls = FindObjectsByType<Unit.Wall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var wall in allWalls)
            {
                if (wall != null)
                {
                    wall.FullRestore();
                }
            }
            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.ClearAll();
            }

            // 1. Hồi phục 100% máu toàn bộ công trình phòng thủ làng đang còn trên sân
            var allUnits = FindObjectsByType<Unit.Unit>(FindObjectsSortMode.None);
            foreach (var u in allUnits)
            {
                if (u == null) continue;
                if (u is Unit.PrehistoricDinoBase || u is Unit.Dino || u is Unit.FlyingUnit) continue;

                if (u.MaxHealth > 0)
                {
                    u.Health = u.MaxHealth;
                    u.EnsureHealthBarAttached();
                }
            }

            // 2. Hồi phục máu căn cứ làng (giữ nguyên 1000 HP theo yêu cầu)
            EnsureVillageBase();
            if (VillageBase != null)
            {
                int baseHp = 1000;
                VillageBase.RestoreBase(baseHp);
            }

            // 3. Tái lập & Bổ sung vũ khí phòng thủ (Máy bắn đá, Tháp nỏ, Cột sét, Bẫy chông, Rào cản)
            foreach (var spec in VillageDefenseSpecs)
            {
                if (level < spec.MinLevel) continue;

                Collider[] hits = Physics.OverlapSphere(spec.Position, 2.0f);
                bool hasActiveStructure = false;
                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent(out Unit.Unit existingUnit) && !(existingUnit is Unit.PrehistoricDinoBase) && !(existingUnit is Unit.Dino) && !(existingUnit is Unit.FlyingUnit))
                    {
                        if (existingUnit.Health > 0)
                        {
                            hasActiveStructure = true;
                            break;
                        }
                    }
                }

                if (!hasActiveStructure)
                {
                    GameObject prefab = Resources.Load<GameObject>($"Prefabs/Prehistoric/{spec.PrefabName}");
#if UNITY_EDITOR
                    if (prefab == null)
                    {
                        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Prehistoric/{spec.PrefabName}.prefab");
                    }
#endif
                    if (prefab != null)
                    {
                        GameObject spawned = Instantiate(prefab, spec.Position, spec.Rotation);
                        if (spawned.TryGetComponent(out Unit.Unit unitComp))
                        {
                            unitComp.EnsureHealthBarAttached();
                            if (Utility.HealthBarCanvas.Instance != null)
                            {
                                Utility.HealthBarCanvas.Instance.CreateHealthBarForUnit(unitComp);
                            }
                        }
                    }
                }
            }

            // 4. Cập nhật NavMesh trước khi spawn NPC
            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.RecalculateTriangulation(true);
            }

            // 5. Làm mới và hồi phục toàn bộ NPC Thợ săn / Cung thủ phòng thủ làng theo quy định nghiêm ngặt của màn
            EnemyAIController enemyAI = EnemyAIController.Instance != null ? EnemyAIController.Instance : FindFirstObjectByType<EnemyAIController>();
            if (enemyAI != null)
            {
                enemyAI.ResetDefendersToExactCount(PrehistoricGameplayManager.GetTargetDefendersForWave(level));
            }
        }

        public void NextAssaultLevel()
        {
            AssaultLevel++;

            // Dọn dẹp sạch toàn bộ khủng long đợt trước khi qua màn mới
            ClearAllDinos();

            // Khôi phục và gia cố phòng tuyến làng (Vũ khí, máy bắn đá, tháp nỏ, tạo sét, bẫy chông)
            SetupVillageDefensesForLevel(AssaultLevel);

            // Cung cấp thịt dồi dào theo màn mới để người chơi dàn trận
            int targetMeat = 250 + (AssaultLevel - 1) * 75;
            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            if (spawner != null)
            {
                spawner.ResourcesToSpend = Mathf.Max(spawner.ResourcesToSpend + 120, targetMeat);
            }

            AssaultPhase = PrehistoricAssaultPhase.Setup;
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Setup;
            }

            Debug.Log($"<color=cyan>[Assault Level Up]</color> Tiến vào Màn {AssaultLevel}! Đã dọn sạch khủng long cũ, cấp thịt dồi dào cho màn mới!");
        }

        public void RetryCurrentAssaultLevel()
        {
            ClearAllDinos();

            // Tái thiết lập phòng tuyến làng cho màn hiện tại
            SetupVillageDefensesForLevel(AssaultLevel);

            // Hồi phục lượng thịt dồi dào theo màn
            int targetMeat = 250 + (AssaultLevel - 1) * 75;
            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            if (spawner != null && spawner.ResourcesToSpend < targetMeat)
            {
                spawner.ResourcesToSpend = targetMeat;
            }

            AssaultPhase = PrehistoricAssaultPhase.Setup;
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Setup;
            }
        }

        
        public void StartNewAssaultFromMenu()
        {
            SetGameMode(PrehistoricGameMode.DinoAssault);
            AssaultLevel = 1;
            ClearAllDinos();
            SetupVillageDefensesForLevel(1);

            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            if (spawner != null)
            {
                spawner.ResourcesToSpend = 250;
            }

            AssaultPhase = PrehistoricAssaultPhase.Setup;
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Setup;
            }

            if (Save.PrehistoricSaveManager.Instance != null)
            {
                var prof = Save.PrehistoricSaveManager.Instance.CurrentData.AssaultProfile;
                if (prof != null)
                {
                    prof.AssaultLevel = 1;
                    prof.Meat = 250;
                    prof.HasActiveRun = true;
                }
                Save.PrehistoricSaveManager.Instance.AutoSaveAssaultRun(true);
            }

            Audio.PrehistoricAudioManager.Instance?.PlayBgmForMode(PrehistoricGameMode.DinoAssault);
            Debug.Log("<color=cyan>[Assault]</color> Khởi động chiến dịch Công Thành từ Màn 1!");
        }

        public void LoadAssaultLevel(int targetLevel, int savedMeat, int savedHp)
        {
            SetGameMode(PrehistoricGameMode.DinoAssault);

            AssaultLevel = Mathf.Max(1, targetLevel);
            ClearAllDinos();
            SetupVillageDefensesForLevel(AssaultLevel);

            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindAnyObjectByType<DinoSpawner>();
            if (spawner != null)
            {
                spawner.ResourcesToSpend = savedMeat > 0 ? savedMeat : (250 + (AssaultLevel - 1) * 75);
            }

            if (Unit.PrehistoricVillageBase.Instance != null && savedHp > 0)
            {
                Unit.PrehistoricVillageBase.Instance.Health = savedHp;
            }

            AssaultPhase = PrehistoricAssaultPhase.Setup;
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Setup;
            }

            if (UI.PrehistoricMainMenu.Instance != null)
            {
                UI.PrehistoricMainMenu.Instance.IsOpen = false;
            }

            Audio.PrehistoricAudioManager.Instance?.PlayBgmForMode(PrehistoricGameMode.DinoAssault);
            Debug.Log($"<color=cyan>[Assault]</color> Đã tải chiến dịch Công Thành: Màn {AssaultLevel} ({savedMeat} Thịt)!");
        }

        public void ContinueAssaultFromMenu()
        {
            int targetLevel = 1;
            int savedMeat = 250;

            if (Save.PrehistoricSaveManager.Instance != null)
            {
                Save.PrehistoricSaveManager.Instance.LoadSaveData();
                var prof = Save.PrehistoricSaveManager.Instance.CurrentData.AssaultProfile;
                if (prof != null)
                {
                    targetLevel = Mathf.Max(1, prof.AssaultLevel);
                    if (prof.Meat > 0) savedMeat = prof.Meat;
                }
            }

            LoadAssaultLevel(targetLevel, savedMeat, 1000);
        }

        public void RestartAssaultFromLevel1()
        {
            AssaultLevel = 1;
            ClearAllDinos();
            SetupVillageDefensesForLevel(1);

            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            if (spawner != null)
            {
                spawner.ResourcesToSpend = 250;
            }

            AssaultPhase = PrehistoricAssaultPhase.Setup;
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.State = GameState.Setup;
            }
            Debug.Log("<color=cyan>[Assault Reset]</color> Chơi lại chế độ Công Thành từ Màn 1!");
        }

        public void ClearAllDinos()
        {
            // Quét dọn triệt để TOÀN BỘ khủng long/quái vật kể cả inactive hoặc đang trong animation ngã chết
            var allUnits = FindObjectsByType<Unit.Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in allUnits)
            {
                if (d != null && (d is Unit.Dino || d is Unit.PrehistoricDinoBase || d is Unit.RunnerDino || d is Unit.FlyingUnit || d is Unit.SiegeDino || d is Unit.BossDino))
                {
                    // Bỏ qua các ghost unit đang preview đặt quân
                    if (d.transform.parent != null && d.transform.parent.name.IndexOf("Visualization", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    if (Utility.HealthBarCanvas.Instance != null) Utility.HealthBarCanvas.Instance.Unregister(d);
                    if (d.gameObject != null)
                    {
                        Destroy(d.gameObject);
                    }
                }
            }

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.ClearActiveDinos();
            }

            // Dọn dẹp triệt để các đạn bay, rác scene, thanh máu mồ côi
            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.CleanAllGameplayGarbage();
            }
        }

        private void DrawAssaultVictoryModal()
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
            GUILayout.Space(10);
            GUILayout.Label("<size=22><b><color=#FFD700>🏆 CHIẾM THÀNH THÀNH CÔNG! 🏆</color></b></size>", GUI.skin.label);
            GUILayout.Label($"<size=15>Bầy khủng long của bạn đã san phẳng Căn Cứ Làng Bộ Lạc ở <b>Màn {AssaultLevel}</b>!</size>");
            GUILayout.Space(12);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<size=14><b><color=#55FF55>📊 THÔNG SỐ ĐỘ KHÓ MÀN TIẾP THEO (MÀN " + (AssaultLevel + 1) + "):</color></b></size>");
            GUILayout.Label($"• <b>Dàn trận màn mới</b>: Làm sạch chiến trường, cấp {250 + AssaultLevel * 75} Thịt để tự do bố trí đội quân mới!\n" +
                            $"• <b>Phòng tuyến Làng củng cố</b>: Tái lập & bổ sung Máy bắn đá, Tháp nỏ, Cột sét, Bẫy gai!\n" +
                            $"• <b>Quân phòng thủ Làng (NPC)</b>: {PrehistoricGameplayManager.GetTargetDefendersForWave(AssaultLevel + 1)} quân phòng thủ\n" +
                            $"• <b>Máu Căn Cứ Làng</b>: {1000 + AssaultLevel * 350} HP (+350 HP)\n" +
                            $"• <b>Chiến thuật</b>: Lựa chọn chủng loại khủng long phù hợp để đột phá và chiếm thành!");
            GUILayout.EndVertical();

            GUILayout.Space(16);
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
            if (GUILayout.Button("<b><size=14>⚔️ QUA MÀN KẾ TIẾP\n(TĂNG ĐỘ KHÓ)</size></b>", GUILayout.Height(55)))
            {
                NextAssaultLevel();
            }

            GUI.backgroundColor = new Color(0.9f, 0.45f, 0.2f);
            if (GUILayout.Button($"<b><size=14>🔄 CHƠI LẠI MÀN {AssaultLevel}</size></b>", GUILayout.Height(55)))
            {
                RetryCurrentAssaultLevel();
            }

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.7f);
            if (GUILayout.Button("<b><size=14>💾 LƯU GAME\n(CHỌN KHE)</size></b>", GUILayout.Height(55)))
            {
                UI.PrehistoricSaveLoadDialog.EnsureInstance().OpenSaveDialog(PrehistoricGameMode.DinoAssault);
            }

            GUI.backgroundColor = new Color(0.2f, 0.65f, 0.95f);
            if (GUILayout.Button("<b><size=14>🏠 MENU CHÍNH\n(LƯU TIẾN TRÌNH)</size></b>", GUILayout.Height(55)))
            {
                ReturnToMainMenu();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawAssaultDefeatModal()
        {
            GUI.depth = -1000;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Mobile.MobileUIHelper.ScreenWidth, Mobile.MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(740f, Mobile.MobileUIHelper.ScreenWidth - 24f);
            float height = 340f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - width) / 2f;
            float y = (Mobile.MobileUIHelper.ScreenHeight - height) / 2f;

            Rect modalRect = new Rect(x, y, width, height);
            Mobile.MobileUIHelper.RegisterUIRect(modalRect);

            GUILayout.BeginArea(modalRect, GUI.skin.window);
            GUILayout.Space(10);
            GUILayout.Label("<size=22><b><color=#FF4444>💀 BẦY KHỦNG LONG BỊ ĐẨY LÙI! 💀</color></b></size>", GUI.skin.label);
            GUILayout.Label($"<size=14>Toàn bộ khủng long đã ngã xuống trước phòng tuyến kiên cố của Làng ở Màn {AssaultLevel}!</size>");
            GUILayout.Space(15);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("💡 <i>Gợi ý chiến thuật: Hãy chiêu mộ Khủng Long Giáp (Ankylosaurus) đi trước hứng sát thương từ tháp canh, kết hợp Velociraptor tốc độ cao và T-Rex phá hủy căn cứ!</i>");
            GUILayout.EndVertical();

            GUILayout.Space(18);
            GUILayout.BeginHorizontal();

            float btnW = (width - 55) / 4f;

            GUI.backgroundColor = new Color(1f, 0.45f, 0.2f);
            if (GUILayout.Button($"<b><size=14>🔄 CHƠI LẠI\nMÀN {AssaultLevel}</size></b>", GUILayout.Height(55), GUILayout.Width(btnW)))
            {
                RetryCurrentAssaultLevel();
            }

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.7f);
            if (GUILayout.Button("<b><size=14>💾 LƯU GAME\n(CHỌN KHE)</size></b>", GUILayout.Height(55), GUILayout.Width(btnW)))
            {
                UI.PrehistoricSaveLoadDialog.EnsureInstance().OpenSaveDialog(PrehistoricGameMode.DinoAssault);
            }

            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f);
            if (GUILayout.Button("<b><size=14>⏮️ CHƠI LẠI\nTỪ ĐẦU</size></b>", GUILayout.Height(55), GUILayout.Width(btnW)))
            {
                RestartAssaultFromLevel1();
            }

            GUI.backgroundColor = new Color(0.2f, 0.65f, 0.95f, 1f);
            if (GUILayout.Button("<b><size=14>🏠 MENU\nCHÍNH</size></b>", GUILayout.Height(55), GUILayout.Width(btnW)))
            {
                ReturnToMainMenu();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
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
                float btnX = Mobile.MobileUIHelper.ScreenWidth - btnWidth - 25f;
                float btnY = Mobile.MobileUIHelper.ScreenHeight - btnHeight - 25f;

                Rect btnRect = new Rect(btnX, btnY, btnWidth, btnHeight);
                Mobile.MobileUIHelper.RegisterUIRect(btnRect);

                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
                if (GUI.Button(btnRect, "<b><size=15>🛡️ GỌI ĐỢT QUÁI\n(START WAVE)</size></b>"))
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
