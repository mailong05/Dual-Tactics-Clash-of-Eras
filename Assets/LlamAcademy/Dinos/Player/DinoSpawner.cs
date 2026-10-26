using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.Player
{
    /// <summary>
    /// Hệ thống triệu hồi và đặt quân Khủng Long trong Chế độ Công Thành (Dino Assault).
    /// Đã được thiết kế lại hoàn chỉnh:
    /// - Nhận diện click/chạm mượt mà, chính xác trên mọi địa hình.
    /// - Tự động định vị trên NavMesh và hướng mặt khủng long về phía căn cứ đối phương.
    /// - Giữ lựa chọn khủng long khi còn đủ tài nguyên thịt để người chơi có thể đặt quân liên tục.
    /// - Chặn hoàn toàn xung đột với thanh máu hoặc UI trong không gian 3D.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class DinoSpawner : MonoBehaviour
    {
        [SerializeField] private ResourceSO FoodResource;

        public int ResourcesToSpend
        {
            get => FoodResource != null ? FoodResource.Amount : 0;
            set { if (FoodResource != null) FoodResource.Amount = value; }
        }

        public static DinoSpawner Instance { get; private set; }
        public bool AllowPlacingDinos { get; set; } = true;
        public DinoSO ActiveDinoSO => SpawnDino;

        public delegate void SpawnDinoEvent(Unit.Unit spawnedDino);
        public event SpawnDinoEvent OnSpawnDino;

        public delegate void DinoDeathEvent(Unit.Unit deadDino);
        public event DinoDeathEvent OnDinoDeath;

        private DinoSO SpawnDino;

        [SerializeField] private Camera Camera;
        [SerializeField] private LayerMask GroundLayer;
        [SerializeField] private PlaceDinoVisualization Visualization;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Quét toàn bộ bề mặt vật lý hợp lệ (loại trừ UI, Ignore Raycast và các vòng trigger phạm vi đánh)
            GroundLayer = ~LayerMask.GetMask("Ignore Raycast", "UI", "Dino Attack Radius", "Enemy Attack Radius");
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

        public void Update()
        {
            // Chỉ hoạt động trong Chế độ Công Thành (DinoAssault)
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode != PrehistoricGameMode.DinoAssault)
            {
                return;
            }

            // Chỉ cho phép đặt quân trong giai đoạn dàn trận (Setup Phase)
            bool isSetupPhase = true;
            if (PrehistoricGameModeManager.Instance != null)
            {
                isSetupPhase = (PrehistoricGameModeManager.Instance.AssaultPhase == PrehistoricAssaultPhase.Setup);
            }
            else if (RoundManager.Instance != null)
            {
                isSetupPhase = (RoundManager.Instance.State == GameState.Setup);
            }

            AllowPlacingDinos = isSetupPhase;

            if (!isSetupPhase)
            {
                if (SpawnDino != null) SpawnDino = null;
                if (Visualization != null && Visualization.gameObject.activeSelf)
                {
                    Visualization.ChangeDino(null);
                }
                return;
            }

            if (Camera == null || !Camera.isActiveAndEnabled)
            {
                Camera = Camera.main;
            }

            // 1. Thu nhận tọa độ con trỏ (Touch hoặc Mouse)
            Vector2 pointerPos = Vector2.zero;
            bool hasPointer = false;
            bool isClickAction = false;

            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                pointerPos = touch.screenPosition;
                hasPointer = true;
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
                {
                    isClickAction = true;
                }
            }
            else if (Mouse.current != null)
            {
                pointerPos = Mouse.current.position.ReadValue();
                hasPointer = true;
                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    isClickAction = true;
                }
                if (Mouse.current.rightButton.wasReleasedThisFrame)
                {
                    ChangeActiveDino(null);
                }
            }
            else
            {
                pointerPos = Input.mousePosition;
                hasPointer = true;
                if (Input.GetMouseButtonUp(0))
                {
                    isClickAction = true;
                }
                if (Input.GetMouseButtonUp(1))
                {
                    ChangeActiveDino(null);
                }
            }

            if (!hasPointer || Camera == null) return;

            // 2. Bắn tia Raycast xuống chiến trường
            Ray ray = Camera.ScreenPointToRay(pointerPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, GroundLayer, QueryTriggerInteraction.Ignore))
            {
                // Căn chuẩn vị trí trên mặt phẳng NavMesh theo đúng AgentType của Dino
                Vector3 placePos = hit.point;
                int targetAgentType = 1479372276;
                if (SpawnDino != null && SpawnDino.Prefab != null && SpawnDino.Prefab.TryGetComponent(out NavMeshAgent pa))
                {
                    targetAgentType = pa.agentTypeID != 0 ? pa.agentTypeID : 1479372276;
                }

                NavMeshQueryFilter navFilter = new NavMeshQueryFilter { agentTypeID = targetAgentType, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 30.0f, navFilter))
                {
                    placePos = navHit.position;
                }
                else if (NavMesh.SamplePosition(hit.point, out navHit, 30.0f, new NavMeshQueryFilter { agentTypeID = 1479372276, areaMask = NavMesh.AllAreas }))
                {
                    placePos = navHit.position;
                }

                // Cập nhật vị trí bóng mờ xem trước (Ghost Preview)
                if (Visualization != null)
                {
                    Visualization.transform.position = placePos;
                }

                // 3. Thực hiện đặt quân khi người chơi click/chạm hợp lệ
                bool isPointerOverUI = Mobile.MobileUIHelper.IsPointerOverUI(pointerPos);

                if (isClickAction && !isPointerOverUI && SpawnDino != null && SpawnDino.Prefab != null)
                {
                    if (ResourcesToSpend >= SpawnDino.Cost)
                    {
                        // Khấu trừ tài nguyên thịt
                        ResourcesToSpend -= SpawnDino.Cost;

                        // Tính góc xoay hướng thẳng về căn cứ đối phương
                        Quaternion spawnRot = Quaternion.identity;
                        Transform targetBase = (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.VillageBase != null)
                            ? PrehistoricGameModeManager.Instance.VillageBase.transform
                            : ((RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null) ? RoundManager.Instance.DinoTarget : null);

                        if (targetBase != null)
                        {
                            Vector3 dir = targetBase.position - placePos;
                            dir.y = 0;
                            if (dir.sqrMagnitude > 0.01f) spawnRot = Quaternion.LookRotation(dir);
                        }

                        // Triệu hồi khủng long vào chiến trường tại vị trí đã căn chuẩn NavMesh
                        Unit.Unit spawnedDino = Instantiate(SpawnDino.Prefab, placePos, spawnRot);
                        if (Environment.PrehistoricMapManager.CurrentMap == Environment.PrehistoricMapType.VolcanicGorge)
                        {
                            Environment.PrehistoricVolcanicMutant.ApplyMutation(spawnedDino.gameObject);
                        }

                        spawnedDino.transform.localScale = SpawnDino.Prefab.transform.localScale;
                        spawnedDino.UnitType = SpawnDino;

                        int assaultLvl = (PrehistoricGameModeManager.Instance != null) ? PrehistoricGameModeManager.Instance.AssaultLevel : 1;
                        float assaultHpMult = 1.0f + (assaultLvl - 1) * 0.15f;
                        int finalHp = Mathf.RoundToInt(SpawnDino.Health * assaultHpMult);
                        spawnedDino.MaxHealth = finalHp;
                        spawnedDino.Health = finalHp;
                        spawnedDino.OnDeath += (obj) => OnDinoDeath?.Invoke(obj.Transform.GetComponent<Unit.Unit>());
                        spawnedDino.enabled = true;

                        // Gắn thanh máu ngay lập tức
                        if (Utility.HealthBarCanvas.Instance != null)
                        {
                            Utility.HealthBarCanvas.Instance.CreateHealthBarForUnit(spawnedDino);
                        }

                        // Ở Setup Phase: Khủng long đứng vào đội hình sẵn sàng, chưa tự ý di chuyển
                        NavMeshAgent agent = spawnedDino.GetComponent<NavMeshAgent>();
                        if (spawnedDino is Unit.PrehistoricDinoBase pDino)
                        {
                            pDino.EnsureOnNavMesh(30.0f);
                            pDino.ConfigureNavMeshAgent();
                            if (pDino.Agent != null && pDino.Agent.isOnNavMesh)
                            {
                                pDino.Agent.isStopped = true;
                            }
                        }
                        else if (agent != null)
                        {
                            if (!agent.isOnNavMesh)
                            {
                                agent.enabled = false;
                                spawnedDino.transform.position = placePos;
                                Physics.SyncTransforms();
                                try { agent.enabled = true; } catch { }
                            }
                            if (agent.isOnNavMesh) agent.isStopped = true;
                        }

                        OnSpawnDino?.Invoke(spawnedDino);
                        Audio.PrehistoricAudioManager.Instance?.PlayDinoSound(SpawnDino);
                        Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();

                        // Nếu không còn đủ thịt cho một con nữa, hủy chọn
                        if (ResourcesToSpend < SpawnDino.Cost)
                        {
                            ChangeActiveDino(null);
                        }
                    }
                    else
                    {
                        // Không đủ thịt: hiển thị thông báo rõ ràng lên màn hình cho người chơi
                        PrehistoricGameplayManager.Instance?.ShowAnnouncement(
                            $"<color=#FF4444>KHÔNG ĐỦ THỊT!</color> Cần {SpawnDino.Cost} thịt (Hiện có {ResourcesToSpend})", 2.0f);
                        Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
                    }
                }
            }

            // Hủy chọn bằng nút ESC
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                ChangeActiveDino(null);
            }
        }

        public void ChangeActiveDino(DinoSO dino)
        {
            SpawnDino = dino;
            if (Visualization != null)
            {
                Visualization.ChangeDino(dino);
            }
            if (dino != null)
            {
                Audio.PrehistoricAudioManager.Instance?.PlayDinoSound(dino);
            }
        }
    }
}
