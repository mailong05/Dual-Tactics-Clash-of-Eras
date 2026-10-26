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
        private int _FallbackMeat = 350;

        public int ResourcesToSpend
        {
            get => FoodResource != null ? FoodResource.Amount : _FallbackMeat;
            set 
            { 
                if (FoodResource != null) FoodResource.Amount = value; 
                _FallbackMeat = value;
            }
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

            // Quét toàn bộ bề mặt vật lý hợp lệ (loại trừ UI, Ignore Raycast, quái vật, collider phụ và Block Dino Spawn)
            GroundLayer = ~LayerMask.GetMask("Ignore Raycast", "UI", "Dinos", "Enemies", "Dino Attack Radius", "Enemy Attack Radius", "Block Dino Spawn");
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
                    if (!Mobile.MobileUIHelper.IsPointerOverUI(pointerPos))
                    {
                        isClickAction = true;
                    }
                }
            }
            else if (Mouse.current != null)
            {
                pointerPos = Mouse.current.position.ReadValue();
                hasPointer = true;
                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    if (!Mobile.MobileUIHelper.IsPointerOverUI(pointerPos))
                    {
                        isClickAction = true;
                    }
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
                    if (!Mobile.MobileUIHelper.IsPointerOverUI(pointerPos))
                    {
                        isClickAction = true;
                    }
                }
                if (Input.GetMouseButtonUp(1))
                {
                    ChangeActiveDino(null);
                }
            }

            if (!hasPointer || Camera == null) return;

            // 2. Bắn tia Raycast xuống chiến trường
            Ray ray = Camera.ScreenPointToRay(pointerPos);
            bool foundGround = false;
            RaycastHit hit = default;

            if (Physics.Raycast(ray, out hit, 1000f, GroundLayer, QueryTriggerInteraction.Ignore))
            {
                foundGround = true;
            }
            else
            {
                RaycastHit[] allHits = Physics.RaycastAll(ray, 1000f, GroundLayer, QueryTriggerInteraction.Ignore);
                if (allHits != null && allHits.Length > 0)
                {
                    System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));
                    hit = allHits[0];
                    foundGround = true;
                }
            }

            if (foundGround)
            {
                // Căn chuẩn vị trí: Bám sát chính xác tọa độ click chuột của người chơi, nắn nhẹ cao độ theo NavMesh
                Vector3 placePos = hit.point;
                bool foundNavMesh = false;

                if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 3.5f, NavMesh.AllAreas))
                {
                    // Nếu click gần NavMesh (<= 1.8m), đặt vào vị trí NavMesh; nếu ở ngoài một chút, giữ tọa độ XZ click và căn chỉnh độ cao Y
                    if (Vector3.Distance(hit.point, navHit.position) <= 1.8f)
                    {
                        placePos = navHit.position;
                    }
                    else
                    {
                        placePos = new Vector3(hit.point.x, navHit.position.y, hit.point.z);
                    }
                    foundNavMesh = true;
                }
                else if (NavMesh.SamplePosition(hit.point, out navHit, 8.0f, NavMesh.AllAreas))
                {
                    placePos = navHit.position;
                    foundNavMesh = true;
                }

                Unit.Unit targetPrefab = SpawnDino != null ? SpawnDino.Prefab : null;
                if (targetPrefab == null && SpawnDino != null)
                {
                    string soName = SpawnDino.name.ToLower();
                    GameObject resGo = null;
                    if (soName.Contains("velo") || soName.Contains("raptor")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Velociraptor");
                    else if (soName.Contains("ankyl")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Ankylosaurus");
                    else if (soName.Contains("ptero")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Pterodactyl");
                    else if (soName.Contains("trex") || soName.Contains("t-rex")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_TRexBoss");
                    if (resGo != null) targetPrefab = resGo.GetComponent<Unit.Unit>();
                }

                // Cập nhật vị trí bóng mờ xem trước (Ghost Preview) và báo hợp lệ
                if (Visualization != null)
                {
                    Visualization.transform.position = placePos;
                    Visualization.SetPlacementValidity(ResourcesToSpend >= (SpawnDino != null ? SpawnDino.Cost : 0), foundNavMesh);
                }

                // 3. Thực hiện đặt quân khi người chơi click/chạm hợp lệ
                if (isClickAction && SpawnDino != null && targetPrefab != null)
                {
                    if (!foundNavMesh)
                    {
                        // Thử mở rộng quét lần cuối trước khi từ chối
                        if (NavMesh.SamplePosition(hit.point, out navHit, 12f, NavMesh.AllAreas))
                        {
                            placePos = navHit.position;
                            foundNavMesh = true;
                        }
                    }

                    if (!foundNavMesh)
                    {
                        PrehistoricGameplayManager.Instance?.ShowAnnouncement(
                            "<color=#FFCC00>⚠ VỊ TRÍ KHÔNG THỂ ĐẶT KHỦNG LONG!</color> Hãy đặt lên mặt đất/đường đi hợp lệ.", 1.5f);
                        return;
                    }

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
                        Unit.Unit spawnedDino = Instantiate(targetPrefab, placePos, spawnRot);
                        if (Environment.PrehistoricMapManager.CurrentMap == Environment.PrehistoricMapType.VolcanicGorge)
                        {
                            Environment.PrehistoricVolcanicMutant.ApplyMutation(spawnedDino.gameObject);
                        }

                        spawnedDino.transform.localScale = targetPrefab.transform.localScale;
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
                            pDino.EnsureOnNavMesh(15.0f);
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
