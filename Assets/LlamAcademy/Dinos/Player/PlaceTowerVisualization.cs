using System.Collections.Generic;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
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
        private LineRenderer FootprintIndicator;

        private GameObject CurrentPreviewInstance;
        private List<Renderer> Renderers = new();
        private Collider[] OverlapHits = new Collider[32];
        private TowerSO CurrentTower;

        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR = Shader.PropertyToID("_Color");
        private static readonly int FRESNEL_COLOR = Shader.PropertyToID("_FresnelColor");

        private void Awake()
        {
            if (ObstacleLayers.value == 0)
            {
                ObstacleLayers = LayerMask.GetMask("Default", "Building");
            }
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

            EnsureFootprintIndicator();
        }

        private void EnsureFootprintIndicator()
        {
            if (FootprintIndicator != null) return;

            GameObject fpObj = new GameObject("FootprintIndicator");
            fpObj.transform.SetParent(transform, false);
            FootprintIndicator = fpObj.AddComponent<LineRenderer>();
            FootprintIndicator.useWorldSpace = false;
            FootprintIndicator.loop = true;
            FootprintIndicator.startWidth = 0.08f;
            FootprintIndicator.endWidth = 0.08f;
            FootprintIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FootprintIndicator.receiveShadows = false;

            FootprintIndicator.material = LlamAcademy.Dinos.Rendering.PrehistoricShaderUtility.CreateLineMaterial(Color.white);
            FootprintIndicator.enabled = false;
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

            if (towerSO == null)
            {
                if (RangeIndicator != null) RangeIndicator.enabled = false;
                if (FootprintIndicator != null) FootprintIndicator.enabled = false;
                return;
            }

            // Retrieve prefab with fallback to Resources
            GameObject prefabObj = towerSO.Prefab != null ? towerSO.Prefab.gameObject : null;
            if (prefabObj == null)
            {
                string pName = "Prefab_" + towerSO.name.Replace("Tower_", "");
                prefabObj = Resources.Load<GameObject>($"Prefabs/Prehistoric/{pName}");
            }

            if (prefabObj == null)
            {
                if (RangeIndicator != null) RangeIndicator.enabled = false;
                if (FootprintIndicator != null) FootprintIndicator.enabled = false;
                return;
            }

            EnsureFootprintIndicator();

            // Instantiate ghost preview — dùng SetParent(true) để giữ nguyên world scale gốc của prefab
            CurrentPreviewInstance = Instantiate(prefabObj, transform.position, Quaternion.identity);
            CurrentPreviewInstance.transform.SetParent(transform, true);
            CurrentPreviewInstance.transform.localPosition = Vector3.zero;

            // Vô hiệu hóa toàn bộ Collider trên preview ghost
            foreach (Collider c in CurrentPreviewInstance.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }

            // Vô hiệu hóa NavMeshAgent & NavMeshObstacle trên preview ghost
            foreach (NavMeshAgent a in CurrentPreviewInstance.GetComponentsInChildren<NavMeshAgent>(true))
            {
                a.enabled = false;
            }
            foreach (NavMeshObstacle o in CurrentPreviewInstance.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                o.enabled = false;
            }

            // VÔ HIỆU HÓA TOÀN BỘ SCRIPT (BallistaTower, CatapultTower, ArcherTower, Unit,...)
            // để ghost preview KHÔNG tự động bắn quái, KHÔNG xoay lệch, và KHÔNG tạo thanh máu thừa
            foreach (MonoBehaviour mb in CurrentPreviewInstance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb != this)
                {
                    mb.enabled = false;
                }
            }

            // Collect renderers and apply ghost material if provided
            Renderer[] childRenderers = CurrentPreviewInstance.GetComponentsInChildren<Renderer>(true);
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
            SetupRangeIndicator(towerSO, prefabObj);

            // Setup Footprint Indicator circle
            SetupFootprintIndicator(towerSO);
        }

        private void SetupRangeIndicator(TowerSO towerSO, GameObject prefabObj)
        {
            if (RangeIndicator == null) return;

            float range = 0f;
            if (towerSO.AttackConfig != null)
            {
                range = towerSO.AttackConfig.GetMaxAttackRange();
            }

            // Fallback: nếu AttackConfig chưa gán, đọc tầm đánh trực tiếp từ các component tháp
            if (range <= 0.1f && prefabObj != null)
            {
                if (prefabObj.TryGetComponent(out Unit.BallistaTower _)) range = 16.0f;
                else if (prefabObj.TryGetComponent(out Unit.CatapultTower _)) range = 14.0f;
                else if (prefabObj.TryGetComponent(out Unit.ArcherTower _)) range = 16.0f;
                else if (prefabObj.name.Contains("Totem") || towerSO.name.Contains("Totem")) range = 12.0f;
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

        private void SetupFootprintIndicator(TowerSO towerSO)
        {
            if (FootprintIndicator == null) return;

            float radius = towerSO.PlacementRadius > 0 ? towerSO.PlacementRadius : 0.8f;
            FootprintIndicator.enabled = true;
            int segments = 36;
            FootprintIndicator.positionCount = segments;
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.Deg2Rad * (i * angleStep);
                float x = Mathf.Sin(angle) * radius;
                float z = Mathf.Cos(angle) * radius;
                FootprintIndicator.SetPosition(i, new Vector3(x, 0.05f, z));
            }
        }

        public void ValidatePlacement(bool playerHasEnoughGold, bool pathIsBlocked)
        {
            if (CurrentTower == null)
            {
                IsValidPlacementLocation = false;
                return;
            }

            if (ObstacleLayers.value == 0)
            {
                ObstacleLayers = LayerMask.GetMask("Default", "Building");
            }

            float checkRadius = CurrentTower.PlacementRadius > 0 ? CurrentTower.PlacementRadius : 0.8f;
            float sphereRadius = checkRadius * 0.82f;
            // Đặt tâm sphere ở độ cao vừa khít (sphereRadius + 0.12m) để đáy hình cầu không chạm xuống đất,
            // tránh bị Collider mặt đất (Terrain/Default) gây cản trở sai khiến tháp bị báo đỏ không thể đặt
            Vector3 checkCenter = transform.position + Vector3.up * (sphereRadius + 0.12f);

            int hitCount = Physics.OverlapSphereNonAlloc(checkCenter, sphereRadius, OverlapHits, ObstacleLayers);

            int solidObstacleCount = 0;
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = OverlapHits[i];
                if (col == null || col.isTrigger) continue;
                if (CurrentPreviewInstance != null && col.transform.IsChildOf(CurrentPreviewInstance.transform)) continue;
                if (col.transform.IsChildOf(transform)) continue;

                // Bỏ qua mặt đất, địa hình hẻm núi tự nhiên
                if (col is TerrainCollider) continue;
                string colName = col.gameObject.name.ToLower();
                if (colName.Contains("terrain") || colName.Contains("ground") || colName.Contains("floor") || 
                    colName.Contains("canyon") || colName.Contains("sand") || colName.Contains("dirt")) continue;
                if (col.CompareTag("Terrain")) continue;

                // Bỏ qua các mục tiêu đã chết
                if (col.TryGetComponent(out IDamageable dmg) && dmg.Health <= 0) continue;
                if (col.GetComponentInParent<IDamageable>() is IDamageable pDmg && pDmg.Health <= 0) continue;

                solidObstacleCount++;
            }

            bool isPhysicsClear = (solidObstacleCount == 0);
            bool validSurface = true;
            if (CurrentTower.IsWall)
            {
                // Rào cọc gỗ cần nằm gần đường di chuyển NavMesh (cho phép dung sai 2.5m)
                validSurface = NavMesh.SamplePosition(transform.position, out _, 2.5f, NavMesh.AllAreas);
            }
            else
            {
                // Các tháp nỏ, chòi canh, máy bắn đá có thể đặt thoải mái trên mặt đất
                validSurface = true;
            }

            IsValidPlacementLocation = playerHasEnoughGold && isPhysicsClear && validSurface && (!CurrentTower.IsWall || !pathIsBlocked);

            Color targetColor = IsValidPlacementLocation ? new Color(0.2f, 0.95f, 0.35f, 0.8f) : new Color(0.95f, 0.2f, 0.2f, 0.8f);

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

            if (FootprintIndicator != null && FootprintIndicator.enabled)
            {
                FootprintIndicator.startColor = color;
                FootprintIndicator.endColor = color;
            }
        }
    }
}
