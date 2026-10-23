using System;
using System.Collections.Generic;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.Player
{
    [DefaultExecutionOrder(10)]
    public class TowerPlacer : MonoBehaviour
    {
        public static TowerPlacer Instance { get; private set; }

        [Header("Economy")]
        [SerializeField] private int _Gold = 700;
        public int Gold
        {
            get => _Gold;
            set
            {
                _Gold = value;
                OnGoldChanged?.Invoke(_Gold);
            }
        }

        public event Action<int> OnGoldChanged;
        public event Action<Unit.Unit> OnTowerPlaced;

        [Header("References")]
        [SerializeField] private Camera PlayerCamera;
        [SerializeField] private LayerMask GroundLayer;
        [SerializeField] private PlaceTowerVisualization Visualization;
        [SerializeField] private List<TowerSO> _AvailableTowers = new();
        public List<TowerSO> AvailableTowers => _AvailableTowers;
        [SerializeField] private Transform[] MonsterSpawnPoints;

        [Header("Placement Assist")]
        [SerializeField] private bool _UseGridSnap = true;
        [SerializeField] private float _GridSnapSize = 1.5f;
        public bool UseGridSnap { get => _UseGridSnap; set => _UseGridSnap = value; }
        public float GridSnapSize { get => _GridSnapSize; set => _GridSnapSize = value; }
        private float _CurrentRotationY = 0f;

        private TowerSO SelectedTower;
        public TowerSO ActiveTower => SelectedTower;
        private NavMeshPath PathCheckBuffer;
        private List<GameObject> _PlacedTowerObjects = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            PathCheckBuffer = new NavMeshPath();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ClearAllPlacedTowers()
        {
            // 1. Xóa toàn bộ các tháp/bẫy/NPC lưu trong danh sách đã đặt của người chơi
            for (int i = _PlacedTowerObjects.Count - 1; i >= 0; i--)
            {
                if (_PlacedTowerObjects[i] != null)
                {
                    Destroy(_PlacedTowerObjects[i]);
                }
            }
            _PlacedTowerObjects.Clear();

            // 2. Quét dọn triệt để mọi đối tượng do người chơi đặt runtime có gắn PlayerPlacedTowerMarker
            var placedMarkers = FindObjectsByType<PlayerPlacedTowerMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var marker in placedMarkers)
            {
                if (marker != null && marker.gameObject != null)
                {
                    // Tuyệt đối không xóa bất kỳ công trình hoặc chòi canh nào của làng
                    if (marker.transform.parent != null && marker.transform.parent.name.Contains("Fortress")) continue;
                    if (marker.name.Contains("Village_")) continue;
                    Destroy(marker.gameObject);
                }
            }

            // 3. Dọn sạch các đối tượng trong container [Player_Placed_Towers] nếu còn sót
            GameObject container = GameObject.Find("[Player_Placed_Towers]");
            if (container != null)
            {
                for (int i = container.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = container.transform.GetChild(i);
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        private Transform GetPlacedTowersContainer()
        {
            GameObject container = GameObject.Find("[Player_Placed_Towers]");
            if (container == null)
            {
                container = new GameObject("[Player_Placed_Towers]");
                container.transform.position = Vector3.zero;
                container.transform.rotation = Quaternion.identity;
                container.transform.localScale = Vector3.one;
            }
            return container.transform;
        }

        public void ResetGoldToDefault(int startingAmount = 500)
        {
            Gold = startingAmount;
        }

        private void Start()
        {
            if (PlayerCamera == null)
            {
                PlayerCamera = Camera.main;
            }

            if (Visualization == null)
            {
                Visualization = GetComponent<PlaceTowerVisualization>();
                if (Visualization == null) Visualization = FindFirstObjectByType<PlaceTowerVisualization>();
            }

            if (GroundLayer.value == 0)
            {
                GroundLayer = ~LayerMask.GetMask("Ignore Raycast", "UI", "Dinos", "Enemies", "Dino Attack Radius", "Enemy Attack Radius");
            }

            if (_AvailableTowers == null) _AvailableTowers = new List<TowerSO>();

            string[] defaultTowerNames = new string[] {
                "Tower_Watchtower", "Tower_Ballista", "Tower_Catapult", 
                "Tower_ShamanTotem", "Tower_TarPit", "Tower_SpikeTrap", "Tower_Barricade", "Tower_TribalWarrior"
            };

            foreach (var tName in defaultTowerNames)
            {
                TowerSO so = Resources.Load<TowerSO>($"Config/Prehistoric/{tName}") ?? Resources.Load<TowerSO>(tName);
#if UNITY_EDITOR
                if (so == null)
                {
                    so = UnityEditor.AssetDatabase.LoadAssetAtPath<TowerSO>($"Assets/Config/Prehistoric/{tName}.asset");
                }
#endif
                if (so != null && !_AvailableTowers.Contains(so))
                {
                    _AvailableTowers.Add(so);
                }
            }

            OnGoldChanged?.Invoke(Gold);
        }

        private void Update()
        {
            if (UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen)
            {
                return;
            }
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode != PrehistoricGameMode.TowerDefense)
            {
                return;
            }
            HandleHotkeys();
            HandlePlacementInteraction();
        }

        public void SelectTower(TowerSO tower)
        {
            SelectedTower = tower;
            _CurrentRotationY = 0f;
            if (Visualization != null)
            {
                Visualization.transform.rotation = Quaternion.identity;
                Visualization.ChangeTower(SelectedTower);
            }
        }

        public void DeselectTower()
        {
            SelectedTower = null;
            _CurrentRotationY = 0f;
            if (Visualization != null)
            {
                Visualization.transform.rotation = Quaternion.identity;
                Visualization.ChangeTower(null);
            }
        }

        public void AddGold(int amount)
        {
            Gold += amount;
        }

        public bool TrySpendGold(int amount)
        {
            if (Gold >= amount)
            {
                Gold -= amount;
                return true;
            }
            return false;
        }

        private Vector3 _LastAimGroundPosition = Vector3.zero;
        private bool _HasValidAimPosition = false;
        private Vector2 _TouchDownPos = Vector2.zero;
        private float _TouchDownTime = 0f;

        private void HandleHotkeys()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasReleasedThisFrame)
                {
                    DeselectTower();
                    return;
                }

                // G key: Bật/Tắt Lưới Căn Chỉnh Tự Động (Grid Snapping)
                if (Keyboard.current.gKey.wasReleasedThisFrame)
                {
                    _UseGridSnap = !_UseGridSnap;
                }

                // R key: Xoay góc công trình 45 độ (Giữ Shift để xoay ngược)
                if (Keyboard.current.rKey.wasReleasedThisFrame)
                {
                    float step = (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed) ? -45f : 45f;
                    _CurrentRotationY = (_CurrentRotationY + step + 360f) % 360f;
                }

                // Phím số 1-9 để chọn tháp phòng thủ
                for (int i = 0; i < AvailableTowers.Count && i < 9; i++)
                {
                    Key key = Key.Digit1 + i;
                    if (Keyboard.current[key].wasReleasedThisFrame)
                    {
                        SelectTower(AvailableTowers[i]);
                        break;
                    }
                }
            }

            if (Mouse.current != null)
            {
                if (Mouse.current.rightButton.wasReleasedThisFrame)
                {
                    DeselectTower();
                    return;
                }

                // Cuộn chuột giữa để xoay góc nhanh
                if (SelectedTower != null)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (scroll > 0.1f) _CurrentRotationY = (_CurrentRotationY + 45f) % 360f;
                    else if (scroll < -0.1f) _CurrentRotationY = (_CurrentRotationY - 45f + 360f) % 360f;
                }
            }
        }

        private void HandlePlacementInteraction()
        {
            if (SelectedTower == null || Visualization == null || PlayerCamera == null) return;

            Vector2 pointerPos = Vector2.zero;
            bool hasPointer = false;
            bool isTapRelease = false;

            // 1. Quét cảm ứng chạm trên Android / Mobile Touchscreen
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    _TouchDownPos = touch.screenPosition;
                    _TouchDownTime = Time.unscaledTime;
                }

                if (!Mobile.MobileUIHelper.IsPointerOverUI(touch.screenPosition))
                {
                    pointerPos = touch.screenPosition;
                    hasPointer = true;

                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
                    {
                        float dist = Vector2.Distance(touch.screenPosition, _TouchDownPos);
                        float dur = Time.unscaledTime - _TouchDownTime;
                        // Chạm thả nhanh trên mặt đất mà không kéo camera -> đặt tháp
                        if (dist < 25f && dur < 0.45f)
                        {
                            isTapRelease = true;
                        }
                    }
                }
            }
            else if (Mouse.current != null)
            {
                pointerPos = Mouse.current.position.ReadValue();
                hasPointer = true;
                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    isTapRelease = true;
                }
            }

            Vector3 placePos = _LastAimGroundPosition;

            if (hasPointer)
            {
                Ray ray = PlayerCamera.ScreenPointToRay(pointerPos);
                if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, GroundLayer))
                {
                    placePos = hit.point;

                    // Căn chỉnh vị trí theo Lưới Thông Minh (Grid Snapping) nếu đang bật
                    if (_UseGridSnap)
                    {
                        placePos.x = Mathf.Round(placePos.x / _GridSnapSize) * _GridSnapSize;
                        placePos.z = Mathf.Round(placePos.z / _GridSnapSize) * _GridSnapSize;

                        // Đo lại độ cao mặt đất chính xác tại tọa độ ô lưới đã làm tròn
                        if (Physics.Raycast(new Vector3(placePos.x, hit.point.y + 10f, placePos.z), Vector3.down, out RaycastHit snapHit, 20f, GroundLayer))
                        {
                            placePos.y = snapHit.point.y;
                        }
                    }

                    // Khống chế độ cao tiếp đất trong khoảng mặt đất tự nhiên (tránh bị nhảy lên không trung hay nóc tường)
                    placePos.y = Mathf.Clamp(placePos.y, -0.5f, 1.5f);
                    _LastAimGroundPosition = placePos;
                    _HasValidAimPosition = true;
                }
            }
            else if (!_HasValidAimPosition)
            {
                // Mặc định ban đầu đưa preview ra trước camera
                Ray centerRay = PlayerCamera.ViewportPointToRay(new Vector3(0.5f, 0.4f, 0f));
                if (Physics.Raycast(centerRay, out RaycastHit cHit, float.MaxValue, GroundLayer))
                {
                    placePos = cHit.point;
                    placePos.y = Mathf.Clamp(placePos.y, -0.5f, 1.5f);
                    _LastAimGroundPosition = placePos;
                    _HasValidAimPosition = true;
                }
            }

            Visualization.transform.position = placePos;
            Visualization.transform.rotation = Quaternion.Euler(0f, _CurrentRotationY, 0f);

            bool hasPrefab = SelectedTower != null && SelectedTower.Prefab != null;
            bool hasEnoughGold = SelectedTower != null && Gold >= SelectedTower.Cost;
            bool isPathBlocked = CheckIfPlacementBlocksPath(placePos);

            Visualization.ValidatePlacement(hasEnoughGold && hasPrefab, isPathBlocked);

            // Xác nhận đặt công trình khi người chơi click/chạm hợp lệ trên mặt đất
            if (isTapRelease
                && Visualization.IsValidPlacementLocation
                && hasPrefab
                && hasEnoughGold
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                && !Mobile.MobileUIHelper.IsPointerOverUI(pointerPos))
            {
                PlaceTowerAt(placePos);
            }
        }

        public void RotateTower45()
        {
            _CurrentRotationY = (_CurrentRotationY + 45f) % 360f;
            if (Visualization != null)
            {
                Visualization.transform.rotation = Quaternion.Euler(0f, _CurrentRotationY, 0f);
            }
        }

        public void ConfirmPlacementFromUI()
        {
            if (SelectedTower == null || SelectedTower.Prefab == null) return;
            if (Visualization == null || !Visualization.IsValidPlacementLocation) return;
            if (Gold < SelectedTower.Cost) return;

            Vector3 pos = _HasValidAimPosition ? _LastAimGroundPosition : Visualization.transform.position;
            PlaceTowerAt(pos);
            Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
        }

        private void PlaceTowerAt(Vector3 position)
        {
            if (SelectedTower == null) return;

            // Truy xuất Prefab với cơ chế tự phục hồi từ thư mục Resources nếu asset bị mất liên kết
            Unit.Unit prefabToSpawn = SelectedTower.Prefab;
            if (prefabToSpawn == null)
            {
                string pName = "Prefab_" + SelectedTower.name.Replace("Tower_", "");
                GameObject resPrefab = Resources.Load<GameObject>($"Prefabs/Prehistoric/{pName}");
                if (resPrefab != null)
                {
                    prefabToSpawn = resPrefab.GetComponent<Unit.Unit>();
                }
            }

            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"[TowerPlacer] Cannot place tower '{SelectedTower?.DisplayName}': Prefab is not assigned or null!");
                return;
            }

            if (!TrySpendGold(SelectedTower.Cost)) return;

            Quaternion targetRotation = Quaternion.Euler(0f, _CurrentRotationY, 0f);
            Unit.Unit towerUnit = Instantiate(prefabToSpawn, position, targetRotation);
            towerUnit.transform.rotation = targetRotation;
            towerUnit.transform.localScale = prefabToSpawn.transform.localScale; // Đảm bảo scale đúng 100% theo prefab gốc
            towerUnit.UnitType = SelectedTower;

            // Nếu công trình đặt là đơn vị NPC chiến binh (Defender)
            if (towerUnit is Enemy.Defender defender)
            {
                var agent = defender.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 15f, NavMesh.AllAreas))
                    {
                        agent.Warp(navHit.position);
                    }
                    else
                    {
                        agent.Warp(position);
                    }
                }

                // Thiết lập điểm bảo vệ/tuần tra khu vực quanh điểm đặt
                List<Vector3> patrolPoints = new List<Vector3>()
                {
                    position,
                    position + targetRotation * Vector3.forward * 2f,
                    position + targetRotation * Vector3.back * 1.5f
                };
                defender.Patrol(patrolPoints);

                if (Enemy.EnemyAIController.Instance != null)
                {
                    Enemy.EnemyAIController.Instance.RegisterDefender(defender);
                }
            }
            else
            {
                // Đảm bảo chân tháp/bẫy luôn tiếp đất chuẩn xác 100%, không bị lơ lửng trên không hay chìm dưới đất
                AlignTowerToGround(towerUnit.gameObject, position.y);
            }

            // Cấu hình NavMeshObstacle cho tường vừa đặt để khắc sâu tức thì vào NavMesh
            // Tuyệt đối không gọi RecalculateTriangulation(true) để tránh xóa sạch NavMesh làm vỡ agent và ném lỗi Failed to create agent
            if (SelectedTower.IsWall)
            {
                if (towerUnit.TryGetComponent(out NavMeshObstacle obs))
                {
                    obs.enabled = true;
                    obs.carving = true;
                    obs.carveOnlyStationary = false;
                }
                if (NavMeshManager.Instance != null)
                {
                    NavMeshManager.Instance.NavMeshUpdatedByObstacles();
                }
            }

            // Phân nhóm và đánh dấu công trình do người chơi đặt
            Transform container = GetPlacedTowersContainer();
            if (container != null && !(towerUnit is Enemy.Defender))
            {
                towerUnit.transform.SetParent(container, true);
            }
            towerUnit.gameObject.AddComponent<PlayerPlacedTowerMarker>();

            OnTowerPlaced?.Invoke(towerUnit);
            _PlacedTowerObjects.Add(towerUnit.gameObject);
            Audio.PrehistoricAudioManager.Instance?.PlayTowerPlaced();
            Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();

            // If Shift is NOT held, deselect. If held, keep placing more of the same tower!
            bool shiftHeld = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            if (!shiftHeld)
            {
                DeselectTower();
            }
        }

        private bool CheckIfPlacementBlocksPath(Vector3 candidatePosition)
        {
            // Chỉ kiểm tra chặn đường đối với tường rào cản (IsWall == true); các tháp bắn và bẫy được tự do đặt
            if (SelectedTower == null || !SelectedTower.IsWall) return false;
            if (RoundManager.Instance == null || RoundManager.Instance.DinoTarget == null) return false;
            if (MonsterSpawnPoints == null || MonsterSpawnPoints.Length == 0) return false;

            Vector3 targetPos = RoundManager.Instance.DinoTarget.position;

            foreach (Transform spawnPoint in MonsterSpawnPoints)
            {
                if (spawnPoint == null) continue;

                if (NavMesh.CalculatePath(spawnPoint.position, targetPos, NavMesh.AllAreas, PathCheckBuffer))
                {
                    // If the path already cannot reach target, or if candidate point directly cuts the path
                    if (PathCheckBuffer.status != NavMeshPathStatus.PathComplete)
                    {
                        return true;
                    }

                    // Check if placement point is right in the center of the only corridor
                    for (int i = 0; i < PathCheckBuffer.corners.Length - 1; i++)
                    {
                        float distToSegment = HandleUtilityDistanceToSegment(PathCheckBuffer.corners[i], PathCheckBuffer.corners[i + 1], candidatePosition);
                        if (distToSegment < 0.8f && SelectedTower.IsWall)
                        {
                            // Close to path corridor; will re-evaluate upon dynamic baking
                        }
                    }
                }
            }

            return false;
        }

        private float HandleUtilityDistanceToSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 0.0001f) return Vector3.Distance(a, point);

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            Vector3 projection = a + t * ab;
            return Vector3.Distance(point, projection);
        }

        private static void AlignTowerToGround(GameObject towerObj, float targetGroundY)
        {
            if (towerObj == null) return;
            // Chỉ quét MeshRenderer và SkinnedMeshRenderer (bỏ qua ParticleSystemRenderer, TrailRenderer,...)
            MeshRenderer[] meshRends = towerObj.GetComponentsInChildren<MeshRenderer>();
            SkinnedMeshRenderer[] skinRends = towerObj.GetComponentsInChildren<SkinnedMeshRenderer>();
            if ((meshRends == null || meshRends.Length == 0) && (skinRends == null || skinRends.Length == 0)) return;

            float lowestY = float.MaxValue;
            if (meshRends != null)
            {
                foreach (var r in meshRends)
                {
                    if (r == null || !r.enabled) continue;
                    if (r.bounds.size.magnitude < 0.001f) continue;
                    lowestY = Mathf.Min(lowestY, r.bounds.min.y);
                }
            }
            if (skinRends != null)
            {
                foreach (var r in skinRends)
                {
                    if (r == null || !r.enabled) continue;
                    if (r.bounds.size.magnitude < 0.001f) continue;
                    lowestY = Mathf.Min(lowestY, r.bounds.min.y);
                }
            }

            if (lowestY < float.MaxValue)
            {
                float diffY = targetGroundY - lowestY;
                // Khống chế mức điều chỉnh trong khoảng an toàn [-1.5m, 1.5m] để tránh tháp bị bay lên trời hoặc chìm sâu dưới đáy vực
                if (Mathf.Abs(diffY) > 0.02f)
                {
                    towerObj.transform.position += Vector3.up * Mathf.Clamp(diffY, -1.5f, 1.5f);
                }

                // Đối với bẫy mặt đất (Vũng lầy / Bẫy chông), nâng nhẹ 0.025m để mặt phẳng tiếp đất hoàn hảo không bị nhấp nháy Z-fighting
                if (towerObj.name.Contains("TarPit") || towerObj.name.Contains("SpikeTrap"))
                {
                    towerObj.transform.position += Vector3.up * 0.025f;
                }
            }
        }

        private void OnGUI()
        {
            if (UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen)
            {
                return;
            }
            if (SelectedTower == null) return;
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode != PrehistoricGameMode.TowerDefense) return;

            Mobile.MobileUIHelper.BeginScaling();
            try
            {
                if (Mobile.MobileUIHelper.IsMobile)
                {
                    DrawMobilePlacementBar();
                }
                else
                {
                    DrawDesktopPlacementTooltip();
                }
            }
            finally
            {
                Mobile.MobileUIHelper.EndScaling();
            }
        }

        private void DrawMobilePlacementBar()
        {
            float barW = Mathf.Min(560f, Mobile.MobileUIHelper.ScreenWidth - 24f);
            float barH = 56f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - barW) / 2f;
            float y = Mobile.MobileUIHelper.ScreenHeight - 146f; // Vị trí nổi ngay trên thanh chọn tháp

            Rect barRect = new Rect(x, y, barW, barH);
            Mobile.MobileUIHelper.RegisterUIRect(barRect);

            GUI.backgroundColor = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            GUILayout.BeginArea(barRect, GUI.skin.box);
            GUILayout.BeginHorizontal();

            bool canAfford = Gold >= SelectedTower.Cost;
            bool canPlace = canAfford && Visualization != null && Visualization.IsValidPlacementLocation;

            // 1. NÚT ĐẶT THÁP (CONFIRM)
            GUI.enabled = canPlace;
            GUI.backgroundColor = canPlace ? new Color(0.2f, 0.85f, 0.35f, 1f) : new Color(0.45f, 0.45f, 0.45f, 0.7f);
            if (GUILayout.Button($"<b><size=14>✓ ĐẶT THÁP\n({SelectedTower.Cost}g)</size></b>", GUILayout.Height(46), GUILayout.Width((barW - 30f) * 0.35f)))
            {
                ConfirmPlacementFromUI();
            }
            GUI.enabled = true;

            // 2. NÚT XOAY 45 ĐỘ
            GUI.backgroundColor = new Color(0.25f, 0.65f, 0.95f, 1f);
            if (GUILayout.Button($"<b><size=13>🔄 XOAY\n({_CurrentRotationY}°)</size></b>", GUILayout.Height(46), GUILayout.Width((barW - 30f) * 0.23f)))
            {
                RotateTower45();
                Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
            }

            // 3. NÚT SNAP LƯỚI
            GUI.backgroundColor = _UseGridSnap ? new Color(0.2f, 0.75f, 0.4f, 1f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            string snapText = _UseGridSnap ? "🔲 LƯỚI\nBẬT" : "🔲 LƯỚI\nTẮT";
            if (GUILayout.Button($"<b><size=13>{snapText}</size></b>", GUILayout.Height(46), GUILayout.Width((barW - 30f) * 0.22f)))
            {
                _UseGridSnap = !_UseGridSnap;
                Mobile.MobileTouchController.Instance?.TriggerHapticFeedback();
            }

            // 4. NÚT HỦY
            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 1f);
            if (GUILayout.Button("<b><size=14>✕ HỦY</size></b>", GUILayout.Height(46), GUILayout.Width((barW - 30f) * 0.20f)))
            {
                DeselectTower();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawDesktopPlacementTooltip()
        {
            float w = 580f;
            float h = 60f;
            float x = (Screen.width - w) / 2f;
            float y = Screen.height - 110f;

            GUI.backgroundColor = new Color(0.12f, 0.16f, 0.22f, 0.92f);
            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            GUILayout.BeginVertical();

            string snapStatus = _UseGridSnap ? "<color=#55FF55>BẬT (1.5m)</color>" : "<color=#FF7777>TẮT</color>";
            GUILayout.Label($"<b><size=13><color=#FFD700>🏗️ ĐANG ĐẶT: {SelectedTower.DisplayName}</color> (Chi phí: {SelectedTower.Cost} Vàng)</size></b>");
            GUILayout.Label($"<size=11>[Chuột Trái]: Đặt | [R/Cuộn chuột]: Xoay ({_CurrentRotationY}°) | [G]: Lưới Snap [{snapStatus}] | [Giữ Shift]: Đặt liên tục | [ESC/Chuột Phải]: Hủy</size>");

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }

    /// <summary>
    /// Component đánh dấu các công trình/NPC do người chơi đặt runtime,
    /// giúp dọn dẹp sạch sẽ khi chơi lại mà không bao giờ xóa nhầm các công trình của làng.
    /// </summary>
    public class PlayerPlacedTowerMarker : MonoBehaviour
    {
    }
}
