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

    [DefaultExecutionOrder(-10)]
    public class PrehistoricGameModeManager : MonoBehaviour
    {
        public static PrehistoricGameModeManager Instance { get; private set; }

        [Header("Active Game Mode")]
        [SerializeField] private PrehistoricGameMode _CurrentMode = PrehistoricGameMode.TowerDefense;
        public PrehistoricGameMode CurrentMode => _CurrentMode;

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
            ApplyModeConfiguration(_CurrentMode);
        }

        private void Update()
        {
            // Hotkey 'M' or 'F1' to toggle Mode Select Menu
            if (Keyboard.current != null && (Keyboard.current.mKey.wasPressedThisFrame || Keyboard.current.f1Key.wasPressedThisFrame))
            {
                ShowModeSelectModal = !ShowModeSelectModal;
            }

            // In Dino Assault mode, handle hotkeys 1-4 for choosing dinos
            if (_CurrentMode == PrehistoricGameMode.DinoAssault && !ShowModeSelectModal)
            {
                HandleDinoHotkeys();
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
                    // Ensure food resources available for player to summon dinos
                    if (Application.isPlaying && dinoSpawner.ResourcesToSpend < 100)
                    {
                        dinoSpawner.ResourcesToSpend = 150;
                    }
                }
            }

            EnemyAIController enemyAI = EnemyAIController.Instance != null ? EnemyAIController.Instance : FindFirstObjectByType<EnemyAIController>();
            if (enemyAI != null)
            {
                enemyAI.enabled = (mode == PrehistoricGameMode.DinoAssault);
            }

            // 3. Battlefield Starter Defenses
            GameObject starterDefenses = GameObject.Find("Starter_Defenses");
            if (starterDefenses != null)
            {
                // In Tower Defense mode, starter defenses act as initial base protection.
                // In Dino Assault mode, starter defenses act as enemy defenses for player's dinos to smash!
                starterDefenses.SetActive(true);
            }
        }

        private void HandleDinoHotkeys()
        {
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

            // 3. Mode-Specific Controls
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
            float width = 360f;
            float height = 45f;
            float x = (Screen.width - width) / 2f;
            float y = 12f;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
            GUILayout.BeginHorizontal();

            string modeText = _CurrentMode == PrehistoricGameMode.TowerDefense
                ? "<b><color=#55FF55>🛡️ CHẾ ĐỘ: THỦ THÁP (TD)</color></b>"
                : "<b><color=#FF5555>🦖 CHẾ ĐỘ: KHỦNG LONG CÔNG THÀNH</color></b>";

            GUILayout.Label(modeText, GUILayout.Height(35));

            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("<b>[⚙️ ĐỔI CHẾ ĐỘ]</b>", GUILayout.Width(130), GUILayout.Height(35)))
            {
                ShowModeSelectModal = !ShowModeSelectModal;
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

        private void DrawDinoAssaultHUD()
        {
            DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
            int food = spawner != null ? spawner.ResourcesToSpend : 0;

            // 1. Top-Left Food & Command Banner
            GUILayout.BeginArea(new Rect(20, 20, 320, 115), GUI.skin.box);
            GUILayout.Label($"<b><size=18><color=#FF6644>🥩 THỨC ĂN (MEAT): {food}</color></size></b>");
            if (SelectedDino != null)
            {
                GUILayout.Label($"<color=#55FF55>Đang chọn: {SelectedDino.name} (Tốn {SelectedDino.Cost} thịt)</color>");
                GUILayout.Label("<size=11><i>[Click chuột trái] Thả khủng long | [ESC] Hủy</i></size>");
            }
            else
            {
                GUILayout.Label("<color=#CCCCCC>Chọn loài khủng long bên dưới để thả quân</color>");
            }
            GUILayout.EndArea();

            // 2. Start Assault Button
            float btnWidth = 220f;
            float btnHeight = 55f;
            float btnX = Screen.width - btnWidth - 25f;
            float btnY = Screen.height - btnHeight - 25f;

            bool isSetupPhase = RoundManager.Instance == null || RoundManager.Instance.State == GameState.Setup;
            if (isSetupPhase)
            {
                GUI.backgroundColor = new Color(1f, 0.35f, 0.2f);
                if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "<b><size=15>⚔️ XUẤT QUÂN\n(START ASSAULT)</size></b>"))
                {
                    if (RoundManager.Instance != null)
                    {
                        RoundManager.Instance.StartRound();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            // 3. Bottom Dino Selection Bar
            if (AvailableDinos != null && AvailableDinos.Count > 0)
            {
                float barWidth = AvailableDinos.Count * 145f;
                float startX = (Screen.width - barWidth) / 2f;
                float startY = Screen.height - 75f;

                GUILayout.BeginArea(new Rect(startX, startY, barWidth, 65), GUI.skin.box);
                GUILayout.BeginHorizontal();

                for (int i = 0; i < AvailableDinos.Count; i++)
                {
                    DinoSO d = AvailableDinos[i];
                    if (d == null) continue;

                    bool canAfford = food >= d.Cost;
                    bool isSelected = SelectedDino == d;

                    GUI.enabled = canAfford;
                    string btnLabel = $"[{i + 1}] {d.name}\n<b>{d.Cost} thịt</b>";

                    if (isSelected) GUI.backgroundColor = Color.red;
                    else GUI.backgroundColor = canAfford ? Color.white : new Color(0.5f, 0.5f, 0.5f);

                    if (GUILayout.Button(btnLabel, GUILayout.Height(50), GUILayout.Width(135)))
                    {
                        SelectDino(isSelected ? null : d);
                    }
                }

                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        private void DrawTowerDefenseStartRoundButton()
        {
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
