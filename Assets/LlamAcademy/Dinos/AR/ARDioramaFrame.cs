using System.Collections.Generic;
using LlamAcademy.Dinos.Rendering;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Unit;
using UnityEngine;

namespace LlamAcademy.Dinos.AR
{
    /// <summary>
    /// Bounded Tabletop Diorama (Đế Sa Bàn Đá Tiền Sử & Khung Cắt Không Gian AR).
    /// Architecture:
    /// 1. 3D Prehistoric Stone Pedestal Base: Museum-grade beveled stone foundation enclosing the village,
    ///    river, and perimeter (Bounds: X [-45, 45], Z [-51, 41]).
    /// 2. 4 Beveled stone borders and 4 corner stone pillars framing the playable diorama.
    /// 3. Clipping Volume / Out-of-bounds Culling: Masks away endless distant mountains/scenery outside
    ///    the diorama bounds so that only the tabletop diorama renders over the real room.
    /// 4. Shadow Matte Receiver: Transparent ground receiver capturing directional shadows onto the real desk.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class ARDioramaFrame : MonoBehaviour
    {
        private static ARDioramaFrame _instance;
        private static bool _isQuitting = false;
        public static bool HasInstance => _instance != null;

        public static ARDioramaFrame Instance
        {
            get
            {
                if (_instance == null && !_isQuitting)
                {
                    _instance = FindFirstObjectByType<ARDioramaFrame>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ARDioramaFrame_Manager");
                        _instance = go.AddComponent<ARDioramaFrame>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        [Header("=== Diorama Dimensions ===")]
        public Vector3 DioramaCenter = new Vector3(0f, 0f, 0f); // Centered across village (Z: -38) and spawn forest (Z: +42)
        public Vector2 DioramaSize = new Vector2(76f, 96f); // Complete tabletop footprint (X: [-38, 38], Z: [-48, 48])
        public float PedestalHeight = 1.8f;

        private GameObject _pedestalRoot;
        private GameObject _shadowMattePlane;
        private Material _stoneMaterial;
        private Material _shadowMatteMaterial;

        // Cached distant out-of-bounds scenery to toggle for AR clipping
        private readonly List<GameObject> _culledSceneryObjects = new List<GameObject>();
        private readonly List<Renderer> _culledSceneryRenderers = new List<Renderer>();
        private bool _isDioramaBuilt = false;
        private bool _isARModeActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (_pedestalRoot == null) BuildStonePedestal();
            if (_shadowMattePlane == null) BuildShadowMatteReceiver();
            SetDioramaActive(_isARModeActive); // Đồng bộ chuẩn xác theo trạng thái AR hiện tại
        }

        /// <summary>
        /// Quản lý nền sa bàn AR: Triệt tiêu hoàn toàn viền xám và cột đá xung quanh (Hình 4 fix).
        /// Không tạo bất kỳ slab, rim, hay pillar xám nào để giữ không gian AR và 3D hoàn toàn tự nhiên.
        /// </summary>
        public void BuildStonePedestal()
        {
            if (_pedestalRoot != null)
            {
                Destroy(_pedestalRoot);
                _pedestalRoot = null;
            }
            _isDioramaBuilt = true;
        }

        private void BuildShadowMatteReceiver()
        {
            if (_shadowMattePlane != null) return;

            // Dùng PrimitiveType.Plane có sẵn mặt phẳng ngang XZ và pháp tuyến (0, 1, 0) hướng thẳng lên trời
            // Khởi tạo độc lập hoàn toàn, không phụ thuộc vào _pedestalRoot để triệt tiêu 100% ngoại lệ MissingReferenceException
            _shadowMattePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _shadowMattePlane.name = "AR_Shadow_Matte_Receiver";

            // Đặt tại Y = -0.01m (ngay dưới chân công trình) và tâm tại Z = -5m (tâm sa bàn làng),
            // bao trọn toàn bộ cụm làng và hướng khủng long tiến công
            _shadowMattePlane.transform.position = new Vector3(0f, -0.01f, -5f);
            _shadowMattePlane.transform.rotation = Quaternion.identity;
            // Plane kích thước gốc là 10m x 10m -> Scale (20, 1, 20) = 200m x 200m bao trọn toàn bộ chiến trường
            _shadowMattePlane.transform.localScale = new Vector3(20f, 1f, 20f);

            Collider col = _shadowMattePlane.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // 1. Thử nạp Material chuyên dụng trong Resources
            Material loadedMat = Resources.Load<Material>("Materials/M_ARShadowMatte");
            if (loadedMat != null)
            {
                _shadowMatteMaterial = new Material(loadedMat);
            }
            else
            {
                // 2. Thử tìm Shader ARShadowMatte
                Shader shadowShader = Shader.Find("Prehistoric/ARShadowMatte");
                if (shadowShader != null)
                {
                    _shadowMatteMaterial = new Material(shadowShader);
                }
            }

            MeshRenderer mr = _shadowMattePlane.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (_shadowMatteMaterial != null)
                {
                    mr.material = _shadowMatteMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = true;
                    mr.enabled = true;
                }
                else
                {
                    mr.enabled = false;
                }
            }
        }

        private void ApplyMaterial(GameObject go, Material mat)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                // Ensure no obstacle raycast issues
                c.isTrigger = false;
            }
        }

        /// <summary>
        /// Activates or deactivates the Tabletop Diorama frame and out-of-bounds scenery clipping.
        /// </summary>
        public void SetARMode(bool isARActive)
        {
            _isARModeActive = isARActive;

            if (isARActive)
            {
                if (!_isDioramaBuilt || _pedestalRoot == null) BuildStonePedestal();
                if (_shadowMattePlane == null) BuildShadowMatteReceiver();

                // Tắt Heatmap mesh khi bật AR để tránh vẽ đè hình nón tím/lưới xanh lên phòng thực tế
                if (AI.ThreatHeatmapVisualizer.Instance != null)
                {
                    AI.ThreatHeatmapVisualizer.Instance.SetVisibility(false);
                }
            }

            SetDioramaActive(isARActive);

            if (isARActive)
            {
                ApplyBoundaryClipping();
            }
            else
            {
                RestoreBoundaryClipping();
            }
        }

        private void SetDioramaActive(bool active)
        {
            if (_pedestalRoot != null) _pedestalRoot.SetActive(active);
            if (_shadowMattePlane != null) _shadowMattePlane.SetActive(active);
        }

        /// <summary>
        /// Scans and hides ground planes and wild scenery so only the village structures and dinosaurs display over the camera view (Hình 3 chuẩn xác).
        /// Sử dụng kỹ thuật ẩn Renderer.enabled (không tắt GameObject) để bảo vệ 100% cấu trúc phân cấp, Transform và logic game.
        /// </summary>
        private void ApplyBoundaryClipping()
        {
            // Chỉ thu thập renderers mới nếu chưa có danh sách lưu trữ
            // Nếu đã lưu danh sách thì giữ nguyên để bảo tồn 100% dữ liệu phục hồi khi thoát AR
            if (_culledSceneryRenderers.Count == 0)
            {
                Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                foreach (Renderer r in allRenderers)
                {
                    if (r == null || !r.gameObject.activeInHierarchy) continue;

                    string rName = r.gameObject.name;
                    if (rName.StartsWith("AR_") || rName.StartsWith("Threat_") || rName.StartsWith("HealthBar_")) continue;
                    if (_pedestalRoot != null && r.transform.IsChildOf(_pedestalRoot.transform)) continue;

                    bool isUnitOrWall = r.GetComponentInParent<Unit.Unit>() != null || r.GetComponentInParent<Unit.Wall>() != null;
                    string objName = rName.ToLowerInvariant();
                    bool isVillageStructure = objName.Contains("village")
                        || objName.Contains("wall")
                        || objName.Contains("tower")
                        || objName.Contains("hut")
                        || objName.Contains("bridge")
                        || objName.Contains("gate")
                        || objName.Contains("fence")
                        || objName.Contains("palisade")
                        || objName.Contains("base")
                        || objName.Contains("totem")
                        || objName.Contains("look target")
                        || objName.Contains("dino")
                        || objName.Contains("trex")
                        || objName.Contains("raptor")
                        || objName.Contains("ankylosaur")
                        || objName.Contains("pterodactyl");

                    if (isUnitOrWall || isVillageStructure)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        r.receiveShadows = true;
                        continue;
                    }

                    if (r.enabled)
                    {
                        r.enabled = false;
                        _culledSceneryRenderers.Add(r);
                    }
                }
            }
            else
            {
                for (int i = 0; i < _culledSceneryRenderers.Count; i++)
                {
                    if (_culledSceneryRenderers[i] != null && _culledSceneryRenderers[i].enabled)
                    {
                        _culledSceneryRenderers[i].enabled = false;
                    }
                }
            }

            Debug.Log($"<color=cyan>[AR Diorama]</color> Đã ẩn toàn bộ mặt sàn và cảnh quan 3D ({_culledSceneryRenderers.Count} renderers), lộ 100% camera mặt bàn và bật đổ bóng chuẩn Hình 3!");
        }

        /// <summary>
        /// Restores all culled distant scenery objects when exiting AR back to standard 3D God-view.
        /// </summary>
        private void RestoreBoundaryClipping()
        {
            // 1. Phục hồi các Renderer đã ẩn tạm thời
            for (int i = 0; i < _culledSceneryRenderers.Count; i++)
            {
                if (_culledSceneryRenderers[i] != null)
                {
                    _culledSceneryRenderers[i].enabled = true;
                }
            }
            _culledSceneryRenderers.Clear();

            // 2. Tắt tấm hứng bóng AR và tiêu hủy hoàn toàn các khối sa bàn phụ
            if (_shadowMattePlane != null) _shadowMattePlane.SetActive(false);
            if (_pedestalRoot != null)
            {
                Destroy(_pedestalRoot);
                _pedestalRoot = null;
            }

            // 3. Phục hồi toàn bộ Root GameObjects trong Scene (World, Ground, Level00, Núi Đá, Cracked Sand, Rừng cây, Smooth Floor)
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isLoaded)
            {
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root == null) continue;
                    string rootName = root.name;
                    if (rootName.StartsWith("AR_") || rootName.StartsWith("Diorama_") || rootName.StartsWith("Rim_") || rootName.StartsWith("Corner_Pillar")) continue;

                    if (!root.activeSelf) root.SetActive(true);

                    Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (var t in allTransforms)
                    {
                        if (t == null) continue;
                        if (t.name.StartsWith("AR_") || t.name.StartsWith("Diorama_") || t.name.StartsWith("Rim_") || t.name.StartsWith("Corner_Pillar")) continue;

                        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                        Renderer rend = t.GetComponent<Renderer>();
                        if (rend != null && !rend.enabled) rend.enabled = true;
                    }
                }
            }

            // 4. Phục hồi toàn bộ renderers cảnh quan, chặn tuyệt đối việc bật nhầm các khối viền xám
            Renderer[] allSceneRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in allSceneRenderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (n.StartsWith("AR_") || n.StartsWith("Threat_") || n.StartsWith("Diorama_") || n.StartsWith("Rim_") || n.StartsWith("Corner_Pillar")) continue;
                if (!r.enabled) r.enabled = true;
                if (!r.gameObject.activeSelf) r.gameObject.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            RestoreBoundaryClipping();
            if (_pedestalRoot != null) Destroy(_pedestalRoot);
            if (_stoneMaterial != null) Destroy(_stoneMaterial);
            if (_shadowMatteMaterial != null) Destroy(_shadowMatteMaterial);
        }
    }
}
