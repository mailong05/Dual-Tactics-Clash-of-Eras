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
            get => FoodResource != null ? FoodResource.Amount : AvailableResources;
            set
            {
                if (FoodResource != null) FoodResource.Amount = value;
                AvailableResources = value;
            }
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
            if (ResourcesToSpend < 60)
            {
                ResourcesToSpend = 60;
            }

            // Mở rộng GroundLayer quét toàn bộ bề mặt địa hình/đường đi/sàn (trừ Ignore Raycast, UI, quái, và trigger bán kính đánh)
            if (GroundLayer.value == 0 || GroundLayer.value == 64)
            {
                GroundLayer = ~LayerMask.GetMask("Ignore Raycast", "UI", "Dinos", "Enemies", "Dino Attack Radius", "Enemy Attack Radius");
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

            // Hồi phục lượng thịt định kỳ chậm rãi và cân bằng (+2 thịt mỗi 2.5 giây)
            _PassiveFoodTimer += Time.deltaTime;
            if (_PassiveFoodTimer >= 2.5f)
            {
                _PassiveFoodTimer = 0f;
                ResourcesToSpend += 2;
            }

            bool isAssault = PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault;
            bool canPlace = RoundManager.Instance == null || RoundManager.Instance.State == GameState.Setup || RoundManager.Instance.State == GameState.Running || isAssault;
            if (canPlace)
            {
                if (Camera == null) Camera = Camera.main;

                Vector2 pointerPos = Vector2.zero;
                bool hasPointer = false;
                bool isReleaseAction = false;

                if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
                {
                    var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                    if (!Mobile.MobileUIHelper.IsPointerOverUI(touch.screenPosition))
                    {
                        pointerPos = touch.screenPosition;
                        hasPointer = true;
                        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
                        {
                            isReleaseAction = true;
                        }
                    }
                }
                else if (Mouse.current != null)
                {
                    pointerPos = Mouse.current.position.ReadValue();
                    hasPointer = true;
                    if (Mouse.current.leftButton.wasReleasedThisFrame)
                    {
                        isReleaseAction = true;
                    }
                }

                if (hasPointer && Camera != null && Physics.Raycast(
                           Camera.ScreenPointToRay(pointerPos),
                           out RaycastHit hit,
                           float.MaxValue,
                           GroundLayer,
                           QueryTriggerInteraction.Ignore))
                {
                    if (Visualization != null)
                    {
                        Visualization.transform.position = hit.point;
                    }

                    if (isReleaseAction
                         && SpawnDino != null
                         && HasResourcesToSpawn(SpawnDino)
                         && Visualization != null
                         && Visualization.IsValidPlacementLocation
                         && hit.collider != null
                         && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                         && !Mobile.MobileUIHelper.IsPointerOverUI(pointerPos))
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
                        if (UnityEngine.AI.NavMesh.SamplePosition(hit.point, out UnityEngine.AI.NavMeshHit navHit, 25.0f, UnityEngine.AI.NavMesh.AllAreas))
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

                        UnityEngine.AI.NavMeshAgent navAgent = spawnedDino.GetComponent<UnityEngine.AI.NavMeshAgent>();

                        if (spawnedDino is Unit.PrehistoricDinoBase pDino)
                        {
                            pDino.EnsureOnNavMesh(25.0f);
                            pDino.ConfigureNavMeshAgent();
                        }
                        else if (navAgent != null)
                        {
                            if (!navAgent.isOnNavMesh)
                            {
                                navAgent.enabled = false;
                                spawnedDino.transform.position = spawnPos;
                                Physics.SyncTransforms();
                                navAgent.enabled = true;
                            }
                            if (navAgent.isOnNavMesh)
                            {
                                navAgent.Warp(spawnPos);
                            }
                        }

                        bool isCombatActive = isAssault && PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.IsAssaultActive;
                        if (isCombatActive)
                        {
                            if (navAgent != null && navAgent.isOnNavMesh)
                            {
                                navAgent.isStopped = false;
                                if (targetT != null) navAgent.SetDestination(targetT.position);
                            }
                            if (spawnedDino is Unit.PrehistoricDinoBase dinoBase)
                            {
                                if (targetT != null) dinoBase.SetDestination(targetT.position);
                            }
                            else if (spawnedDino is Unit.FlyingUnit flyBase)
                            {
                                if (targetT != null) flyBase.SetDestination(targetT.position);
                            }
                            else if (spawnedDino is Unit.Dino dinoComp)
                            {
                                if (targetT != null) dinoComp.SetDestination(targetT.position);
                            }
                        }
                        else
                        {
                            // Đang ở giai đoạn Setup (dàn trận): giữ nguyên vị trí, hướng mặt về phía mục tiêu
                            if (navAgent != null && navAgent.isOnNavMesh)
                            {
                                navAgent.isStopped = true;
                            }
                        }

                        OnSpawnDino?.Invoke(spawnedDino);
                        Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();

                        // Nếu không giữ Shift, hủy chọn sau khi đặt
                        bool shiftHeld = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
                        if (!shiftHeld)
                        {
                            SpawnDino = null;
                            if (Visualization != null) Visualization.ChangeDino(null);
                        }
                    }
                }

                if (Keyboard.current != null && Keyboard.current.escapeKey.wasReleasedThisFrame)
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
