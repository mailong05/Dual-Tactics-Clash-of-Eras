using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Rendering;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Player
{
    /// <summary>
    /// Hiển thị trực quan bóng mờ (Ghost Preview), Vòng ngắm mặt đất (Cursor Ring),
    /// và Khung cho phép đặt khủng long (Deployment Zone Frame) trong Chế độ Công Thành.
    /// </summary>
    public class PlaceDinoVisualization : MonoBehaviour
    {
        public bool IsValidPlacementLocation { get; private set; }

        [SerializeField] private List<GameObject> Visualizations = new();
        [SerializeField] private LayerMask UnsafeLayers;

        [SerializeField] private SafeZoneVisualizer SafeZonePrefab;
        [SerializeField] private GameObject WorldRoot;

        private List<Renderer> Renderers = new();
        private int Count = 1;
        private DinoSO Dino;
        [SerializeField] private GameObject SafeZone;

        // Vòng ngắm mặt đất tại vị trí con trỏ chuột
        private LineRenderer _CursorRing;
        // Đường viền Khung Thả Quân (Deployment Zone Border)
        private LineRenderer _ZoneBorder;

        private bool _IsOnNavMesh = true;
        private bool _IsInZone = true;
        private bool _CanAfford = true;

        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int FRESNEL_COLOR = Shader.PropertyToID("_FresnelColor");
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR = Shader.PropertyToID("_Color");

        private void Awake()
        {
            EnsureIndicators();
        }

        private void Start()
        {
            if (SafeZone != null) SafeZone.SetActive(false);

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange += OnGameStateChange;
            }
        }

        private void OnDestroy()
        {
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange -= OnGameStateChange;
            }
        }

        private void EnsureIndicators()
        {
            // 1. Khởi tạo Khung Viền Thả Quân trên mặt đất
            if (_ZoneBorder == null)
            {
                GameObject borderObj = new GameObject("DeploymentZoneBorder");
                borderObj.transform.position = Vector3.zero;
                _ZoneBorder = borderObj.AddComponent<LineRenderer>();
                _ZoneBorder.useWorldSpace = true;
                _ZoneBorder.loop = true;
                _ZoneBorder.positionCount = 4;
                _ZoneBorder.startWidth = 0.35f;
                _ZoneBorder.endWidth = 0.35f;
                _ZoneBorder.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ZoneBorder.receiveShadows = false;
                _ZoneBorder.material = PrehistoricShaderUtility.CreateLineMaterial(new Color(0.2f, 1f, 0.4f, 0.8f));

                float y = 0.15f;
                _ZoneBorder.SetPosition(0, new Vector3(DinoSpawner.MIN_DEPLOY_X, y, DinoSpawner.MIN_DEPLOY_Z));
                _ZoneBorder.SetPosition(1, new Vector3(DinoSpawner.MAX_DEPLOY_X, y, DinoSpawner.MIN_DEPLOY_Z));
                _ZoneBorder.SetPosition(2, new Vector3(DinoSpawner.MAX_DEPLOY_X, y, DinoSpawner.MAX_DEPLOY_Z));
                _ZoneBorder.SetPosition(3, new Vector3(DinoSpawner.MIN_DEPLOY_X, y, DinoSpawner.MAX_DEPLOY_Z));
                _ZoneBorder.enabled = false;
            }

            // 2. Khởi tạo Vòng Tròn ngắm mặt đất tại chân con trỏ
            if (_CursorRing == null)
            {
                GameObject ringObj = new GameObject("CursorPlacementRing");
                ringObj.transform.SetParent(transform, false);
                ringObj.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                _CursorRing = ringObj.AddComponent<LineRenderer>();
                _CursorRing.useWorldSpace = false;
                _CursorRing.loop = true;
                int segments = 36;
                _CursorRing.positionCount = segments;
                _CursorRing.startWidth = 0.15f;
                _CursorRing.endWidth = 0.15f;
                _CursorRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _CursorRing.receiveShadows = false;
                _CursorRing.material = PrehistoricShaderUtility.CreateLineMaterial(Color.white);

                float radius = 1.6f;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * (Mathf.PI * 2f / segments);
                    _CursorRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                }
                _CursorRing.enabled = false;
            }
        }

        private void OnGameStateChange(GameState oldState, GameState newState)
        {
            if (newState == GameState.Running)
            {
                if (SafeZone != null && SafeZone.activeSelf && SafeZone.TryGetComponent(out Renderer r))
                {
                    StartCoroutine(FadeOut(r.material));
                }
            }
        }

        private IEnumerator FadeIn(Material material)
        {
            if (SafeZone != null) SafeZone.SetActive(true);
            float time = 0;
            int STRENGTH_PROPERTY = Shader.PropertyToID("_Strength");
            while (time < 1)
            {
                if (material != null) material.SetFloat(STRENGTH_PROPERTY, time);
                time += Time.deltaTime * 4;
                yield return null;
            }

            if (material != null) material.SetFloat(STRENGTH_PROPERTY, 1);
        }

        private IEnumerator FadeOut(Material material)
        {
            float time = 1;
            int STRENGTH_PROPERTY = Shader.PropertyToID("_Strength");
            while (time > 0)
            {
                if (material != null) material.SetFloat(STRENGTH_PROPERTY, time);
                time -= Time.deltaTime * 4;
                yield return null;
            }

            if (material != null) material.SetFloat(STRENGTH_PROPERTY, 0);
            if (SafeZone != null) SafeZone.SetActive(false);
        }

        public void ChangeDino(DinoSO dinoSO)
        {
            EnsureIndicators();

            foreach (GameObject go in Visualizations)
            {
                if (go != null) Destroy(go.gameObject);
            }

            Visualizations.Clear();
            Renderers.Clear();
            Dino = dinoSO;

            if (dinoSO == null)
            {
                if (SafeZone != null && SafeZone.activeSelf) SafeZone.SetActive(false);
                if (_ZoneBorder != null) _ZoneBorder.enabled = false;
                if (_CursorRing != null) _CursorRing.enabled = false;
                return;
            }

            // Bật Khung viền thả quân khi đang chọn khủng long
            if (_ZoneBorder != null) _ZoneBorder.enabled = true;
            if (_CursorRing != null) _CursorRing.enabled = true;

            Unit.Unit prefab = dinoSO.Prefab;
            if (prefab == null)
            {
                string soName = dinoSO.name.ToLower();
                GameObject resGo = null;
                if (soName.Contains("velo") || soName.Contains("raptor")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Velociraptor");
                else if (soName.Contains("ankyl")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Ankylosaurus");
                else if (soName.Contains("ptero")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Pterodactyl");
                else if (soName.Contains("trex") || soName.Contains("t-rex")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_TRexBoss");
                if (resGo != null) prefab = resGo.GetComponent<Unit.Unit>();
            }

            if (prefab == null)
            {
                if (SafeZone != null && SafeZone.activeSelf) SafeZone.SetActive(false);
                if (_ZoneBorder != null) _ZoneBorder.enabled = false;
                if (_CursorRing != null) _CursorRing.enabled = false;
                return;
            }

            if (SafeZone != null && SafeZone.activeSelf) SafeZone.SetActive(false);

            for (int i = 0; i < Count; i++)
            {
                Quaternion rot = Quaternion.identity;
                Vector3 targetBasePos = (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.VillageBase != null)
                    ? PrehistoricGameModeManager.Instance.VillageBase.transform.position
                    : ((RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null) ? RoundManager.Instance.DinoTarget.position : new Vector3(-2.23f, 0f, -38.26f));
                Vector3 dir = targetBasePos - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f) rot = Quaternion.LookRotation(dir);

                Transform modelSource = prefab.transform.Find("Model");
                GameObject ghostGo = null;
                if (modelSource != null)
                {
                    ghostGo = Instantiate(modelSource.gameObject, transform.position, rot, transform);
                    ghostGo.transform.localScale = modelSource.localScale;
                }
                else
                {
                    ghostGo = Instantiate(prefab.gameObject, transform.position, rot, transform);
                    ghostGo.transform.localScale = prefab.transform.localScale;
                }

                ghostGo.name = "GhostVisual_" + prefab.name;
                ghostGo.transform.localPosition = Vector3.zero;
                // Căn chuẩn góc quay của Model trong ghost luôn hướng thẳng về căn cứ
                ghostGo.transform.localRotation = Quaternion.identity;

                // Triệt tiêu 100% toàn bộ component logic, AI, NavMeshAgent, Collider để không bao giờ can thiệp vào game
                foreach (var nav in ghostGo.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(nav);
                foreach (var u in ghostGo.GetComponentsInChildren<Unit.Unit>(true)) DestroyImmediate(u);
                foreach (var collider in ghostGo.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
                foreach (var rb in ghostGo.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
                foreach (var a in ghostGo.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(a);
                foreach (var anim in ghostGo.GetComponentsInChildren<Animator>(true)) anim.enabled = false;

                Visualizations.Add(ghostGo);

                Renderer[] renderers = ghostGo.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r != null)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        Renderers.Add(r);
                    }
                }
            }
        }

        public void SetPlacementValidity(bool canAfford, bool onNavMesh, bool inZone = true)
        {
            _CanAfford = canAfford;
            _IsOnNavMesh = onNavMesh;
            _IsInZone = inZone;
            IsValidPlacementLocation = canAfford && onNavMesh && inZone;
        }

        private void Update()
        {
            EnsureIndicators();

            if (Dino == null || Visualizations.Count == 0)
            {
                IsValidPlacementLocation = false;
                if (_ZoneBorder != null) _ZoneBorder.enabled = false;
                if (_CursorRing != null) _CursorRing.enabled = false;
                return;
            }

            for (int i = Visualizations.Count - 1; i >= 0; i--)
            {
                if (Visualizations[i] == null)
                {
                    Visualizations.RemoveAt(i);
                    continue;
                }
            }

            // 1. Luôn hướng bóng mờ xem trước quay đầu thẳng về căn cứ làng
            Vector3 targetBasePos = (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.VillageBase != null)
                ? PrehistoricGameModeManager.Instance.VillageBase.transform.position
                : ((RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null) ? RoundManager.Instance.DinoTarget.position : new Vector3(-2.23f, 0f, -38.26f));
            Vector3 lookDir = targetBasePos - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }

            bool canAfford = DinoSpawner.Instance != null && DinoSpawner.Instance.ResourcesToSpend >= Dino.Cost;
            bool inZone = DinoSpawner.IsInDeploymentZone(transform.position);
            bool isValid = canAfford && _IsOnNavMesh && inZone;
            IsValidPlacementLocation = isValid;

            // 2. Màu sắc hiển thị: Xanh lá dạ quang nếu hợp lệ, Đỏ cảnh báo nếu không hợp lệ
            Color targetColor = isValid ? new Color(0.2f, 1f, 0.4f, 0.8f) : new Color(1f, 0.25f, 0.25f, 0.8f);

            // Cập nhật viền vòng tròn ngắm
            if (_CursorRing != null)
            {
                _CursorRing.enabled = true;
                _CursorRing.startColor = targetColor;
                _CursorRing.endColor = targetColor;
            }

            // Cập nhật Khung Viền Thả Quân
            if (_ZoneBorder != null)
            {
                _ZoneBorder.enabled = true;
                Color borderCol = inZone ? new Color(0.2f, 1f, 0.4f, 0.7f) : new Color(1f, 0.6f, 0.2f, 0.5f);
                _ZoneBorder.startColor = borderCol;
                _ZoneBorder.endColor = borderCol;
            }

            for (int r = 0; r < Renderers.Count; r++)
            {
                Renderer rend = Renderers[r];
                if (rend == null) continue;
                foreach (var mat in rend.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty(TINT)) mat.SetColor(TINT, targetColor);
                    if (mat.HasProperty(FRESNEL_COLOR)) mat.SetColor(FRESNEL_COLOR, targetColor);
                    if (mat.HasProperty(BASE_COLOR)) mat.SetColor(BASE_COLOR, targetColor);
                    if (mat.HasProperty(COLOR)) mat.SetColor(COLOR, targetColor);
                }
            }
        }
    }
}
