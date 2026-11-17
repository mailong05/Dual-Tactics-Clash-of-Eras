using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LlamAcademy.Dinos.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class RuntimeUI : MonoBehaviour
    {
        [SerializeField] private DinoSO[] Dinos;
        [SerializeField] private ResourceSO[] ResourceSOs;

        [SerializeField] private VisualTreeAsset DinoTemplate;
        [SerializeField] private VisualTreeAsset ResourceTemplate;

        private UIDocument Document;
        private Dictionary<DinoSO, VisualElement> DinoToButtonDictionary = new();

        private Button StartButton => Document?.rootVisualElement?.Q<Button>("start-button");
        private Label WaveText => Document?.rootVisualElement?.Q<Label>("wave-text");
        private VisualElement DinoButtonContainer => Document?.rootVisualElement?.Q("dino-button-container");
        private VisualElement ResourcesContainer => Document?.rootVisualElement?.Q("resources-container");
        private Label RoundEndText => Document?.rootVisualElement?.Q<Label>("win-lose-text");

        private VisualElement Logo => Document?.rootVisualElement?.Q("logo-container");

        private void Awake()
        {
            Document = GetComponent<UIDocument>();

            StartButton?.RegisterCallback<ClickEvent>(HandleStartClick);

            if (ResourcesContainer != null && ResourceSOs != null && ResourceTemplate != null)
            {
                foreach (ResourceSO resourceSO in ResourceSOs)
                {
                    VisualElement resourceElement = new();
                    resourceElement.style.width = 225;
                    ResourceTemplate.CloneTree(resourceElement);
                    resourceElement.dataSource = resourceSO;
                    ResourcesContainer.Add(resourceElement);
                }
            }

            // Ensure T-Rex is included in Dinos list if missing
            if (Dinos != null && System.Array.Find(Dinos, d => d != null && d.name.Contains("TRex")) == null)
            {
                DinoSO trexSO = null;
#if UNITY_EDITOR
                trexSO = UnityEditor.AssetDatabase.LoadAssetAtPath<DinoSO>("Assets/Config/Prehistoric/Dino_TRexBoss.asset");
#endif
                if (trexSO != null)
                {
                    var list = new List<DinoSO>(Dinos);
                    list.Add(trexSO);
                    Dinos = list.ToArray();
                }
            }

            if (DinoButtonContainer != null && Dinos != null && DinoTemplate != null)
            {
                foreach (DinoSO dino in Dinos)
                {
                    if (dino == null) continue;
                    VisualElement dinoElement = new();
                    dinoElement.AddToClassList("dino-button-container");
                    DinoTemplate.CloneTree(dinoElement);
                    dinoElement.dataSource = dino;

                    dinoElement.RegisterCallback<ClickEvent, DinoSO>(HandleClickDino, dino);

                    DinoButtonContainer.Add(dinoElement);

                    DinoToButtonDictionary.Add(dino, dinoElement);
                }
            }

            // Luôn ẩn và vô hiệu hóa UIDocument để loại bỏ hoàn toàn nút Wave 0, Start và các card cũ đè lên HUD
            SetVisible(false);
        }

        private void OnEnable()
        {
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (Document != null && Document.enabled)
            {
                Document.enabled = false;
            }
        }

        public void SetVisible(bool visible)
        {
            if (Document != null)
            {
                Document.enabled = false;
                if (Document.rootVisualElement != null)
                {
                    Document.rootVisualElement.style.display = DisplayStyle.None;
                    Document.rootVisualElement.visible = false;
                }
            }
        }

        private void HandleGameModeChanged(PrehistoricGameMode newMode)
        {
            SetVisible(false);
        }

        private IEnumerator Start()
        {
            if (PrehistoricGameModeManager.Instance != null)
            {
                PrehistoricGameModeManager.Instance.OnGameModeChanged += HandleGameModeChanged;
                SetVisible(false);
            }

            RoundManager.Instance.OnGameStateChange += OnGameStateChange;
            DinoSpawner.Instance.OnSpawnDino += HandleDinoSpawnOrDeath;
            DinoSpawner.Instance.OnDinoDeath += HandleDinoSpawnOrDeath;
            yield return new WaitForSeconds(2);
            if (Logo != null) Logo.AddToClassList("out");
        }

        private void Update()
        {
            if (PrehistoricUIModalManager.IsAnyModalOpen()) return;
            if (Document != null && Document.rootVisualElement != null && Document.rootVisualElement.style.display == DisplayStyle.None)
            {
                return;
            }
            if (PrehistoricGameModeManager.Instance != null) return;
            if (Keyboard.current == null || Dinos == null) return;

            foreach (DinoSO dino in Dinos)
            {
                if (dino == null || !DinoToButtonDictionary.ContainsKey(dino)) continue;

                if (Keyboard.current[dino.Hotkey] != null && Keyboard.current[dino.Hotkey].wasReleasedThisFrame && DinoToButtonDictionary[dino].enabledSelf)
                {
                    SelectDino(dino);
                    AnimateClick(DinoToButtonDictionary[dino]);
                }
            }
        }

        private void OnDisable()
        {
            if (PrehistoricGameModeManager.Instance != null)
            {
                PrehistoricGameModeManager.Instance.OnGameModeChanged -= HandleGameModeChanged;
            }
            if (RoundManager.Instance != null) RoundManager.Instance.OnGameStateChange -= OnGameStateChange;
            if (DinoSpawner.Instance != null)
            {
                DinoSpawner.Instance.OnSpawnDino -= HandleDinoSpawnOrDeath;
                DinoSpawner.Instance.OnDinoDeath -= HandleDinoSpawnOrDeath;
            }
        }

        private void HandleDinoSpawnOrDeath(Unit.Unit _)
        {
            SetStartButtonInteractable(RoundManager.Instance.State);
            SetDinoButtonStates(RoundManager.Instance.State);
        }

        private void SetDinoButtonStates(GameState currentGameState)
        {
            foreach (KeyValuePair<DinoSO, VisualElement> keyValuePair in DinoToButtonDictionary)
            {
                keyValuePair.Value.SetEnabled(currentGameState == GameState.Setup && keyValuePair.Key.Cost <= DinoSpawner.Instance.ResourcesToSpend);
            }
        }

        private void OnGameStateChange(GameState oldState, GameState newState)
        {
            SetStartButtonInteractable(newState);
            SetDinoButtonStates(newState);

            if (newState == GameState.Ending)
            {
                if (RoundEndText != null)
                {
                    RoundEndText.RemoveFromClassList("hidden");
                    RoundEndText.AddToClassList("visible");
                    if (RoundManager.Instance.AliveDinos > 0)
                    {
                        RoundEndText.text = "Round <color=#229922>Won</color>";
                    }
                    else
                    {
                        RoundEndText.text = "Round <color=#ac0000>Lost</color>";
                    }
                }
            }
            else if (newState == GameState.Ended)
            {
                if (RoundEndText != null)
                {
                    RoundEndText.RemoveFromClassList("visible");
                    RoundEndText.AddToClassList("hidden");
                }
            }
            else if (newState == GameState.Setup)
            {
                if (WaveText != null)
                {
                    WaveText.text = $"Wave {RoundManager.Instance.Round}";
                }
            }
        }
        private void SetStartButtonInteractable(GameState state) => StartButton?.SetEnabled(state == GameState.Setup && RoundManager.Instance.AliveDinos > 0);

        private void HandleStartClick(ClickEvent evt)
        {
            AnimateClick(evt.target as VisualElement);
            RoundManager.Instance.StartRound();
        }

        private void HandleClickDino(ClickEvent evt, DinoSO dino)
        {
            SelectDino(dino);
        }

        private void SelectDino(DinoSO dino)
        {
            DinoSpawner.Instance.ChangeActiveDino(dino);
        }

        private void HandleMouseUpOnDino(MouseUpEvent evt)
        {
            VisualElement element = evt.currentTarget as VisualElement;
            element.RemoveFromClassList("out");
            element.AddToClassList("in");
            evt.StopPropagation();
        }

        private void HandleMouseDownOnDino(MouseDownEvent evt)
        {
            VisualElement element = evt.currentTarget as VisualElement;
            element.RemoveFromClassList("in");
            element.AddToClassList("out");
            evt.StopPropagation();
        }

        private void AnimateClick(VisualElement element)
        {
            element.AddToClassList("out");
            element.RemoveFromClassList("in");
            element.RegisterCallback<TransitionEndEvent>(HandlePressTransitionComplete);
        }

        private void HandlePressTransitionComplete(TransitionEndEvent evt)
        {
            if (!evt.AffectsProperty("scale")) return;

            VisualElement element = evt.target as VisualElement;
            element.RemoveFromClassList("out");
            element.AddToClassList("in");
            element.UnregisterCallback<TransitionEndEvent>(HandlePressTransitionComplete);
        }
    }
}
