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
        [SerializeField] private int _Gold = 500;
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
            for (int i = _PlacedTowerObjects.Count - 1; i >= 0; i--)
            {
                if (_PlacedTowerObjects[i] != null)
                {
                    Destroy(_PlacedTowerObjects[i]);
                }
            }
            _PlacedTowerObjects.Clear();

            // Quét dọn các Unit Defender do người chơi đặt runtime không thuộc Starter_Defenses
            GameObject starterDefenses = GameObject.Find("Starter_Defenses");
            var allUnits = FindObjectsByType<Unit.Unit>(FindObjectsSortMode.None);
            foreach (var u in allUnits)
            {
                if (u != null && u.UnitType is TowerSO && (starterDefenses == null || !u.transform.IsChildOf(starterDefenses.transform)))
                {
                    Destroy(u.gameObject);
                }
            }
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

            OnGoldChanged?.Invoke(Gold);
        }

        private void Update()
        {
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
            if (Visualization != null)
            {
                Visualization.ChangeTower(SelectedTower);
            }
        }

        public void DeselectTower()
        {
            SelectedTower = null;
            if (Visualization != null)
            {
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

        private void HandleHotkeys()
        {
            if (Keyboard.current.escapeKey.wasReleasedThisFrame || Mouse.current.rightButton.wasReleasedThisFrame)
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

            // Cuộn chuột giữa để xoay góc nhanh
            if (SelectedTower != null && Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0.1f) _CurrentRotationY = (_CurrentRotationY + 45f) % 360f;
                else if (scroll < -0.1f) _CurrentRotationY = (_CurrentRotationY - 45f + 360f) % 360f;
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

        private void HandlePlacementInteraction()
        {
            if (SelectedTower == null || Visualization == null || PlayerCamera == null) return;

            Ray ray = PlayerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, GroundLayer))
            {
                Vector3 placePos = hit.point;

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

                Visualization.transform.position = placePos;
                Visualization.transform.rotation = Quaternion.Euler(0f, _CurrentRotationY, 0f);

                bool hasPrefab = SelectedTower != null && SelectedTower.Prefab != null;
                bool hasEnoughGold = SelectedTower != null && Gold >= SelectedTower.Cost;
                bool isPathBlocked = CheckIfPlacementBlocksPath(placePos);

                Visualization.ValidatePlacement(hasEnoughGold && hasPrefab, isPathBlocked);

                // Click chuột trái để hoàn tất đặt công trình
                if (Mouse.current.leftButton.wasReleasedThisFrame
                    && Visualization.IsValidPlacementLocation
                    && hasPrefab
                    && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                {
                    PlaceTowerAt(placePos);
                }
            }
        }

        private void PlaceTowerAt(Vector3 position)
        {
            if (SelectedTower == null || SelectedTower.Prefab == null)
            {
                Debug.LogWarning($"[TowerPlacer] Cannot place tower '{SelectedTower?.DisplayName}': Prefab is not assigned or null!");
                return;
            }

            if (!TrySpendGold(SelectedTower.Cost)) return;

            Quaternion targetRotation = Quaternion.Euler(0f, _CurrentRotationY, 0f);
            Unit.Unit towerUnit = Instantiate(SelectedTower.Prefab, position, targetRotation);
            towerUnit.transform.rotation = targetRotation;
            towerUnit.transform.localScale = SelectedTower.Prefab.transform.localScale; // Đảm bảo scale đúng 100% theo prefab gốc
            towerUnit.UnitType = SelectedTower;

            // Đảm bảo chân tháp/bẫy luôn tiếp đất chuẩn xác 100%, không bị lơ lửng trên không
            AlignTowerToGround(towerUnit.gameObject, position.y);

            // Recalculate NavMesh if placing a wall or obstacle structure
            if (SelectedTower.IsWall && NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.RecalculateTriangulation(true);
            }

            OnTowerPlaced?.Invoke(towerUnit);
            _PlacedTowerObjects.Add(towerUnit.gameObject);

            // If Shift is NOT held, deselect. If held, keep placing more of the same tower!
            if (!Keyboard.current.leftShiftKey.isPressed && !Keyboard.current.rightShiftKey.isPressed)
            {
                DeselectTower();
            }
        }

        private bool CheckIfPlacementBlocksPath(Vector3 candidatePosition)
        {
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
            Renderer[] rends = towerObj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            float lowestY = float.MaxValue;
            foreach (Renderer r in rends)
            {
                if (r == null || !r.enabled) continue;
                if (r.bounds.size.magnitude < 0.001f) continue;
                lowestY = Mathf.Min(lowestY, r.bounds.min.y);
            }

            if (lowestY < float.MaxValue && Mathf.Abs(lowestY - targetGroundY) > 0.02f)
            {
                towerObj.transform.position += Vector3.up * (targetGroundY - lowestY);
            }
        }

        private void OnGUI()
        {
            if (SelectedTower == null) return;
            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode != PrehistoricGameMode.TowerDefense) return;

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
}
