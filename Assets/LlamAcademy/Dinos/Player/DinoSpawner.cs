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

            // Mở rộng GroundLayer quét toàn bộ bề mặt địa hình/đường đi/sàn (trừ Ignore Raycast và UI)
            if (GroundLayer.value == 0 || GroundLayer.value == 64)
            {
                GroundLayer = ~LayerMask.GetMask("Ignore Raycast", "UI");
            }
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
            bool isAssault = PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault;
            if (newState == GameState.Setup || newState == GameState.Running || isAssault)
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

            bool isAssault = PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault;
            bool canPlace = RoundManager.Instance == null || RoundManager.Instance.State == GameState.Setup || RoundManager.Instance.State == GameState.Running || isAssault;
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
                        if (targetT == null && PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.VillageBase != null)
                        {
                            targetT = PrehistoricGameModeManager.Instance.VillageBase.transform;
                        }

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
                        spawnedDino.UnitType = SpawnDino;
                        if (spawnedDino.MaxHealth <= 0)
                        {
                            spawnedDino.MaxHealth = SpawnDino.Health;
                            spawnedDino.Health = SpawnDino.Health;
                        }
                        spawnedDino.OnDeath += (obj) => OnDinoDeath?.Invoke(obj.Transform.GetComponent<Unit.Unit>());
                        spawnedDino.enabled = true;

                        // Khởi tạo thanh máu và tên đơn vị ngay lập tức
                        if (Utility.HealthBarCanvas.Instance != null)
                        {
                            Utility.HealthBarCanvas.Instance.CreateHealthBarForUnit(spawnedDino);
                        }

                        // Định vị NavMeshAgent chính xác trên NavMesh để khủng long di chuyển ngay
                        UnityEngine.AI.NavMeshAgent agent = spawnedDino.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null)
                        {
                            agent.enabled = true;
                            agent.Warp(spawnPos);
                        }

                        bool isCombatActive = isAssault && PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.IsAssaultActive;
                        if (isCombatActive)
                        {
                            // Đang trong trận: xuất kích xông thẳng vào căn cứ địch!
                            if (agent != null && agent.isOnNavMesh)
                            {
                                agent.isStopped = false;
                                if (targetT != null) agent.SetDestination(targetT.position);
                            }
                            if (spawnedDino is Unit.PrehistoricDinoBase dinoBase)
                            {
                                if (targetT != null) dinoBase.SetDestination(targetT.position);
                            }
                            else if (spawnedDino is Unit.Dino dinoComp)
                            {
                                if (targetT != null) dinoComp.SetDestination(targetT.position);
                            }
                        }
                        else
                        {
                            // Đang ở giai đoạn Setup (dàn trận): giữ nguyên vị trí, hướng mặt về phía mục tiêu
                            if (agent != null && agent.isOnNavMesh)
                            {
                                agent.isStopped = true;
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
