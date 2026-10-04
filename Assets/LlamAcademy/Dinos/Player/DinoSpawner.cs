using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.Player
{
    [DefaultExecutionOrder(10)]
    public class DinoSpawner : MonoBehaviour
    {
        [SerializeField] private int AvailableResources;
        [SerializeField] private ResourceSO FoodResource;
        public int ResourcesToSpend
        {
            get => FoodResource.Amount;
            set => FoodResource.Amount = value;
        }

        public static DinoSpawner Instance { get; private set; }
        public delegate void SpawnDinoEvent(Unit.Unit spawnedDino);
        public event SpawnDinoEvent OnSpawnDino;
        public delegate void DinoDeathEvent(Unit.Unit deadDino);
        public event DinoDeathEvent OnDinoDeath;

        private DinoSO SpawnDino;

        [SerializeField]
        private Camera Camera;
        [SerializeField]
        private LayerMask GroundLayer;

        [SerializeField]
        private PlaceDinoVisualization Visualization;

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"Multiple RoundManagers detected. Deleting the second one {name}");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ResourcesToSpend = 0;
        }

        private void Start()
        {
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange += Instance_OnGameStateChange;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange -= Instance_OnGameStateChange;
            }
        }

        private void Instance_OnGameStateChange(GameState oldState, GameState newState)
        {
            if (newState == GameState.Setup || newState == GameState.Running)
            {
                if (SpawnDino != null && Visualization != null)
                {
                    Visualization.ChangeDino(SpawnDino);
                }
            }
            else
            {
                if (Visualization != null)
                {
                    Visualization.ChangeDino(null);
                }
            }
        }

        public void Update()
        {
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode != PrehistoricGameMode.DinoAssault)
            {
                return;
            }

            if (RoundManager.Instance.State == GameState.Setup || RoundManager.Instance.State == GameState.Running)
            {
                if (Physics.Raycast(
                           Camera.ScreenPointToRay(Mouse.current.position.ReadValue()),
                           out RaycastHit hit,
                           float.MaxValue,
                           GroundLayer))
                {
                    if (Visualization != null)
                    {
                        Visualization.transform.position = hit.point;
                    }
                }

                if (Mouse.current.leftButton.wasReleasedThisFrame
                     && SpawnDino != null
                     && HasResourcesToSpawn(SpawnDino)
                     && Visualization != null
                     && Visualization.IsValidPlacementLocation
                     && hit.collider != null
                     && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                {
                    ResourcesToSpend -= SpawnDino.Cost;
                    Quaternion rot = Quaternion.identity;
                    if (RoundManager.Instance.DinoTarget != null)
                    {
                        rot = Quaternion.LookRotation((RoundManager.Instance.DinoTarget.position - hit.point).normalized);
                    }
                    Unit.Unit spawnedDino = Instantiate(SpawnDino.Prefab, hit.point, rot);
                    spawnedDino.OnDeath += (obj) => OnDinoDeath?.Invoke(obj.Transform.GetComponent<Unit.Unit>());
                    spawnedDino.UnitType = SpawnDino;
                    spawnedDino.enabled = true;

                    // Nếu đang trong trận chiến, lập tức dẫn quân tiến công mục tiêu
                    if (RoundManager.Instance.State == GameState.Running)
                    {
                        if (spawnedDino is Unit.Dino dinoComp)
                        {
                            if (RoundManager.Instance.DinoTarget != null)
                            {
                                dinoComp.SetDestination(RoundManager.Instance.DinoTarget.position);
                            }
                        }
                        else if (spawnedDino is Unit.PrehistoricDinoBase dinoBase)
                        {
                            if (RoundManager.Instance.DinoTarget != null)
                            {
                                dinoBase.SetDestination(RoundManager.Instance.DinoTarget.position);
                            }
                        }
                    }

                    OnSpawnDino?.Invoke(spawnedDino);
                    SpawnDino = null;
                    if (Visualization != null) Visualization.ChangeDino(null);
                }

                if (Keyboard.current.escapeKey.wasReleasedThisFrame)
                {
                    SpawnDino = null;
                    if (Visualization != null) Visualization.ChangeDino(null);
                }
            }
        }

        public void ChangeActiveDino(DinoSO Dino)
        {
            SpawnDino = Dino;
            if (RoundManager.Instance != null && (RoundManager.Instance.State == GameState.Setup || RoundManager.Instance.State == GameState.Running))
            {
                if (Visualization != null)
                {
                    Visualization.ChangeDino(Dino);
                }
            }
        }

        private bool HasResourcesToSpawn(DinoSO Dino)
        {
            return ResourcesToSpend >= Dino.Cost;
        }
    }
}
