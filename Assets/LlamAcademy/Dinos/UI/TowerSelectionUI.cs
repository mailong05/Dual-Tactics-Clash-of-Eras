using System.Collections.Generic;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.UI
{
    public class TowerSelectionUI : MonoBehaviour
    {
        [Header("UI References (Optional)")]
        [SerializeField] private TextMeshProUGUI GoldText;
        [SerializeField] private Transform ButtonContainer;
        [SerializeField] private GameObject TowerButtonPrefab;
        [SerializeField] private List<TowerSO> AvailableTowers = new();

        private List<TowerButtonInstance> CreatedButtons = new();

        private void Start()
        {
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.OnGoldChanged += HandleGoldChanged;
                HandleGoldChanged(TowerPlacer.Instance.Gold);
                if (AvailableTowers.Count == 0 && TowerPlacer.Instance.AvailableTowers != null)
                {
                    AvailableTowers = new List<TowerSO>(TowerPlacer.Instance.AvailableTowers);
                }
            }

            BuildTowerButtons();
        }

        private void OnDestroy()
        {
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.OnGoldChanged -= HandleGoldChanged;
            }
        }

        private void HandleGoldChanged(int currentGold)
        {
            if (GoldText != null)
            {
                GoldText.text = $"<color=#FFD700>Gold:</color> {currentGold}";
            }

            foreach (TowerButtonInstance btn in CreatedButtons)
            {
                if (btn != null && btn.Button != null)
                {
                    btn.Button.interactable = currentGold >= btn.TowerSO.Cost;
                }
            }
        }

        private void BuildTowerButtons()
        {
            if (ButtonContainer == null || TowerButtonPrefab == null) return;

            foreach (Transform child in ButtonContainer)
            {
                Destroy(child.gameObject);
            }
            CreatedButtons.Clear();

            int hotkeyIndex = 1;
            foreach (TowerSO tower in AvailableTowers)
            {
                if (tower == null) continue;

                GameObject btnObj = Instantiate(TowerButtonPrefab, ButtonContainer);
                Button btn = btnObj.GetComponent<Button>();
                TextMeshProUGUI label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                Image icon = btnObj.transform.Find("Icon")?.GetComponent<Image>();

                if (label != null)
                {
                    label.text = $"{hotkeyIndex}. {tower.DisplayName}\n<color=#FFD700>{tower.Cost}g</color>";
                }

                if (icon != null && tower.Sprite != null)
                {
                    icon.sprite = tower.Sprite;
                }

                TowerSO capturedTower = tower;
                btn.onClick.AddListener(() =>
                {
                    if (TowerPlacer.Instance != null)
                    {
                        TowerPlacer.Instance.SelectTower(capturedTower);
                    }
                });

                CreatedButtons.Add(new TowerButtonInstance
                {
                    Button = btn,
                    TowerSO = capturedTower
                });

                hotkeyIndex++;
            }

            if (TowerPlacer.Instance != null)
            {
                HandleGoldChanged(TowerPlacer.Instance.Gold);
            }
        }

        /// <summary>
        /// Foolproof prehistoric OnGUI HUD overlay in case canvas buttons are unassigned
        /// </summary>
        private void OnGUI()
        {
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode != PrehistoricGameMode.TowerDefense)
            {
                return;
            }

            if (TowerPlacer.Instance == null) return;

            var towers = TowerPlacer.Instance.AvailableTowers;
            if (towers == null || towers.Count == 0) return;

            int gold = TowerPlacer.Instance.Gold;
            TowerSO active = TowerPlacer.Instance.ActiveTower;

            // Top-Left Gold & Status Banner
            GUILayout.BeginArea(new Rect(20, 20, 320, 110), GUI.skin.box);
            GUILayout.Label($"<b><size=18><color=#FFD700>VÀNG HIỆN CÓ: {gold}g</color></size></b>");
            if (active != null)
            {
                GUILayout.Label($"<color=#55FF55>Đang chọn: {active.DisplayName} ({active.Cost}g)</color>");
                GUILayout.Label("<size=11><i>[Click chuột trái] Đặt | [ESC] Hủy | [Shift] Đặt liên tục</i></size>");
            }
            else
            {
                GUILayout.Label("<color=#CCCCCC>Chọn tháp bên dưới hoặc bấm phím số (1-7)</color>");
            }
            GUILayout.EndArea();

            // Bottom Tower Selection Bar
            float barWidth = Mathf.Min(Screen.width - 40, towers.Count * 145f);
            float startX = (Screen.width - barWidth) / 2f;
            float startY = Screen.height - 75f;

            GUILayout.BeginArea(new Rect(startX, startY, barWidth, 65), GUI.skin.box);
            GUILayout.BeginHorizontal();

            for (int i = 0; i < towers.Count; i++)
            {
                TowerSO t = towers[i];
                if (t == null) continue;

                bool canAfford = gold >= t.Cost;
                bool isSelected = active == t;

                GUI.enabled = canAfford;
                string btnLabel = $"[{i + 1}] {t.DisplayName}\n<b>{t.Cost}g</b>";

                if (isSelected)
                {
                    GUI.backgroundColor = Color.green;
                }
                else
                {
                    GUI.backgroundColor = canAfford ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                }

                if (GUILayout.Button(btnLabel, GUILayout.Height(50), GUILayout.Width(135)))
                {
                    TowerPlacer.Instance.SelectTower(isSelected ? null : t);
                }
            }

            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private class TowerButtonInstance
        {
            public Button Button;
            public TowerSO TowerSO;
        }
    }
}
