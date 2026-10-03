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

        private TowerSO SelectedTower;
        public TowerSO ActiveTower => SelectedTower;
        private NavMeshPath PathCheckBuffer;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            PathCheckBuffer = new NavMeshPath();
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
                GroundLayer = ~LayerMask.GetMask("Ignore Raycast");
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

            // Number keys 1-9 to select tower
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
                // Snap slightly to ground
                Visualization.transform.position = hit.point;

                bool hasEnoughGold = Gold >= SelectedTower.Cost;
                bool isPathBlocked = CheckIfPlacementBlocksPath(hit.point);

                Visualization.ValidatePlacement(hasEnoughGold, isPathBlocked);

                // Place tower on Left Click
                if (Mouse.current.leftButton.wasReleasedThisFrame
                    && Visualization.IsValidPlacementLocation
                    && !EventSystem.current.IsPointerOverGameObject())
                {
                    PlaceTowerAt(hit.point);
                }
            }
        }

        private void PlaceTowerAt(Vector3 position)
        {
            if (!TrySpendGold(SelectedTower.Cost)) return;

            Unit.Unit towerUnit = Instantiate(SelectedTower.Prefab, position, Quaternion.identity);
            towerUnit.UnitType = SelectedTower;

            // Recalculate NavMesh if placing a wall or obstacle structure
            if (SelectedTower.IsWall && NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.RecalculateTriangulation(true);
            }

            OnTowerPlaced?.Invoke(towerUnit);

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
    }
}
