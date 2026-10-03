using System.Collections.Generic;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Player
{
    public class PlaceTowerVisualization : MonoBehaviour
    {
        public bool IsValidPlacementLocation { get; private set; }

        [SerializeField] private LayerMask ObstacleLayers;
        [SerializeField] private Material PreviewMaterial;
        [SerializeField] private LineRenderer RangeIndicator;

        private GameObject CurrentPreviewInstance;
        private List<Renderer> Renderers = new();
        private Collider[] OverlapHits = new Collider[5];
        private TowerSO CurrentTower;

        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR = Shader.PropertyToID("_Color");
        private static readonly int FRESNEL_COLOR = Shader.PropertyToID("_FresnelColor");

        private void Awake()
        {
            if (RangeIndicator == null)
            {
                RangeIndicator = GetComponentInChildren<LineRenderer>();
            }

            if (RangeIndicator != null)
            {
                RangeIndicator.useWorldSpace = false;
                RangeIndicator.loop = true;
                RangeIndicator.enabled = false;
            }
        }

        public void ChangeTower(TowerSO towerSO)
        {
            if (CurrentPreviewInstance != null)
            {
                Destroy(CurrentPreviewInstance);
                CurrentPreviewInstance = null;
            }

            Renderers.Clear();
            CurrentTower = towerSO;

            if (towerSO == null || towerSO.Prefab == null)
            {
                if (RangeIndicator != null) RangeIndicator.enabled = false;
                return;
            }

            // Instantiate ghost preview
            CurrentPreviewInstance = Instantiate(towerSO.Prefab.gameObject, transform.position, Quaternion.identity, transform);
            CurrentPreviewInstance.transform.localPosition = Vector3.zero;

            // Disable all colliders and NavMeshAgents on ghost
            foreach (Collider c in CurrentPreviewInstance.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            NavMeshAgent agent = CurrentPreviewInstance.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            NavMeshObstacle obstacle = CurrentPreviewInstance.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;

            // Collect renderers and apply ghost material if provided
            Renderer[] childRenderers = CurrentPreviewInstance.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in childRenderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (PreviewMaterial != null)
                {
                    r.material = new Material(PreviewMaterial);
                }
                Renderers.Add(r);
            }

            // Setup Range Indicator circle
            SetupRangeIndicator(towerSO);
        }

        private void SetupRangeIndicator(TowerSO towerSO)
        {
            if (RangeIndicator == null) return;

            float range = 0f;
            if (towerSO.AttackConfig != null)
            {
                range = towerSO.AttackConfig.GetMaxAttackRange();
            }

            if (range <= 0.1f)
            {
                RangeIndicator.enabled = false;
                return;
            }

            RangeIndicator.enabled = true;
            int segments = 40;
            RangeIndicator.positionCount = segments;
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.Deg2Rad * (i * angleStep);
                float x = Mathf.Sin(angle) * range;
                float z = Mathf.Cos(angle) * range;
                RangeIndicator.SetPosition(i, new Vector3(x, 0.1f, z));
            }
        }

        public void ValidatePlacement(bool playerHasEnoughGold, bool pathIsBlocked)
        {
            if (CurrentTower == null)
            {
                IsValidPlacementLocation = false;
                return;
            }

            float checkRadius = CurrentTower.PlacementRadius > 0 ? CurrentTower.PlacementRadius : 0.8f;
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, checkRadius, OverlapHits, ObstacleLayers);

            // Must have enough gold, no overlapping obstacles, and must NOT block the entire maze path
            bool isPhysicsClear = hitCount == 0;
            bool onNavMesh = NavMesh.SamplePosition(transform.position, out _, 1.0f, NavMesh.AllAreas);

            IsValidPlacementLocation = playerHasEnoughGold && isPhysicsClear && onNavMesh && !pathIsBlocked;

            Color targetColor = IsValidPlacementLocation ? new Color(0.2f, 0.9f, 0.3f, 0.6f) : new Color(0.9f, 0.2f, 0.2f, 0.6f);

            SetGhostColor(targetColor);
        }

        private void SetGhostColor(Color color)
        {
            for (int i = 0; i < Renderers.Count; i++)
            {
                if (Renderers[i] == null) continue;

                Material mat = Renderers[i].material;
                if (mat.HasProperty(TINT)) mat.SetColor(TINT, color);
                if (mat.HasProperty(BASE_COLOR)) mat.SetColor(BASE_COLOR, color);
                if (mat.HasProperty(COLOR)) mat.SetColor(COLOR, color);
                if (mat.HasProperty(FRESNEL_COLOR)) mat.SetColor(FRESNEL_COLOR, color);
            }

            if (RangeIndicator != null && RangeIndicator.enabled)
            {
                RangeIndicator.startColor = color;
                RangeIndicator.endColor = color;
            }
        }
    }
}
