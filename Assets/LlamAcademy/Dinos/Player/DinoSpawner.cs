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

        private float _PassiveFoodTimer = 0f;

        public void Update()
        {
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode != PrehistoricGameMode.DinoAssault)
            {
                return;
            }

            // Hồi phục lượng thịt định kỳ để người chơi thoải mái thử nghiệm triệu hồi quân
            _PassiveFoodTimer += Time.deltaTime;
            if (_PassiveFoodTimer >= 1.0f)
            {
                _PassiveFoodTimer = 0f;
                ResourcesToSpend += 8;
            }

            bool canPlace = RoundManager.Instance == null || RoundManager.Instance.State == GameState.Setup || RoundManager.Instance.State == GameState.Running;
            if (canPlace)
            {
                if (Camera == null) Camera = Camera.main;

                if (Camera != null && Physics.Raycast(
                           Camera.ScreenPointToRay(Mouse.current.position.ReadValue()),
                           out RaycastHit hit,
                           float.MaxValue,
                           GroundLayer))
                {
                    if (Visualization != null)
                    {
                        Visualization.transform.position = hit.point;
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
                        Transform targetT = (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
                            ? RoundManager.Instance.DinoTarget
                            : null;
                        if (targetT != null)
                        {
                            Vector3 dir = targetT.position - hit.point;
                            dir.y = 0;
                            if (dir.sqrMagnitude > 0.01f) rot = Quaternion.LookRotation(dir);
                        }

                        Vector3 spawnPos = hit.point;
                        if (UnityEngine.AI.NavMesh.SamplePosition(hit.point, out UnityEngine.AI.NavMeshHit navHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            spawnPos = navHit.position;
                        }

                        Unit.Unit spawnedDino = Instantiate(SpawnDino.Prefab, spawnPos, rot);
                        spawnedDino.transform.localScale = SpawnDino.Prefab.transform.localScale;
                        spawnedDino.OnDeath += (obj) => OnDinoDeath?.Invoke(obj.Transform.GetComponent<Unit.Unit>());
                        spawnedDino.UnitType = SpawnDino;
                        spawnedDino.enabled = true;

                        // Định vị NavMeshAgent chính xác trên NavMesh để khủng long di chuyển ngay
                        UnityEngine.AI.NavMeshAgent agent = spawnedDino.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null)
                        {
                            agent.enabled = true;
                            agent.Warp(spawnPos);
                        }

                        // Lập tức dẫn quân tiến công mục tiêu
                        if (targetT != null)
                        {
                            if (spawnedDino is Unit.Dino dinoComp)
                            {
                                dinoComp.SetDestination(targetT.position);
                            }
                            else if (spawnedDino is Unit.PrehistoricDinoBase dinoBase)
                            {
                                dinoBase.SetDestination(targetT.position);
                            }
                        }

                        OnSpawnDino?.Invoke(spawnedDino);

                        // Nếu không giữ Shift, hủy chọn sau khi đặt
                        if (!Keyboard.current.leftShiftKey.isPressed && !Keyboard.current.rightShiftKey.isPressed)
                        {
                            SpawnDino = null;
                            if (Visualization != null) Visualization.ChangeDino(null);
                        }
                    }
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
            if (Visualization != null)
            {
                Visualization.ChangeDino(Dino);
            }
        }

        private bool HasResourcesToSpawn(DinoSO Dino)
        {
            return ResourcesToSpend >= Dino.Cost;
        }
    }
}
