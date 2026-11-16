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
        /// Builds the physical 3D Prehistoric Stone Pedestal Base enclosing the village and tactical forest.
        /// </summary>
        public void BuildStonePedestal()
        {
            if (_isDioramaBuilt && _pedestalRoot != null) return;
            if (_pedestalRoot != null) Destroy(_pedestalRoot);

            _pedestalRoot = new GameObject("AR_Stone_Diorama_Pedestal");
            _pedestalRoot.transform.position = DioramaCenter;

            // Stone Material (Dark Prehistoric Granite / Basalt with subtle moss tone)
            Shader litShader = PrehistoricShaderUtility.GetLitShader();
            _stoneMaterial = new Material(litShader);
            Color stoneCol = new Color(0.18f, 0.20f, 0.24f, 1f);
            if (_stoneMaterial.HasProperty("_BaseColor")) _stoneMaterial.SetColor("_BaseColor", stoneCol);
            if (_stoneMaterial.HasProperty("_Color")) _stoneMaterial.SetColor("_Color", stoneCol);
            if (_stoneMaterial.HasProperty("_Smoothness")) _stoneMaterial.SetFloat("_Smoothness", 0.25f);
            if (_stoneMaterial.HasProperty("_Metallic")) _stoneMaterial.SetFloat("_Metallic", 0.15f);

            // 1. Main Pedestal Slab underneath the terrain (Invisible safety foundation)
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Diorama_Main_Slab";
            slab.transform.SetParent(_pedestalRoot.transform, false);
            slab.transform.localPosition = new Vector3(0f, -PedestalHeight * 0.5f - 0.05f, 0f);
            slab.transform.localScale = new Vector3(DioramaSize.x + 2f, PedestalHeight, DioramaSize.y + 2f);
            Renderer rSlab = slab.GetComponent<Renderer>();
            if (rSlab != null) rSlab.enabled = false;

            // 1b. Safety Arena Sub-Floor: solid ground plane right underneath the sand at Y = -0.10m
            // with a solid BoxCollider on Layer 6 (Grass/Ground) so physics raycast can never fall through!
            // Mesh ẩn hoàn toàn để không che tấm sa bàn AR Hologram màu cyan (Hình 2 chuẩn xác)
            GameObject arenaTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arenaTop.name = "Diorama_Arena_Floor";
            arenaTop.layer = 6; // Layer 6: Grass / Ground surface
            arenaTop.transform.SetParent(_pedestalRoot.transform, false);
            arenaTop.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            arenaTop.transform.localScale = new Vector3(DioramaSize.x, 0.12f, DioramaSize.y);
            Renderer rArena = arenaTop.GetComponent<Renderer>();
            if (rArena != null) rArena.enabled = false;
            BoxCollider topColComp = arenaTop.GetComponent<BoxCollider>();
            if (topColComp != null)
            {
                topColComp.enabled = true;
                topColComp.isTrigger = false;
            }

            // 2. 4 Beveled Stone Border Rims (Ẩn mesh để giữ mặt phẳng AR trong suốt chuẩn Hình 2)
            float halfW = DioramaSize.x * 0.5f;
            float halfL = DioramaSize.y * 0.5f;
            float rimThickness = 1.4f;
            float rimHeight = 0.5f;

            CreateRimBorder("Rim_North", new Vector3(0f, rimHeight * 0.5f, halfL), new Vector3(DioramaSize.x + 2f, rimHeight, rimThickness));
            CreateRimBorder("Rim_South", new Vector3(0f, rimHeight * 0.5f, -halfL), new Vector3(DioramaSize.x + 2f, rimHeight, rimThickness));
            CreateRimBorder("Rim_East", new Vector3(halfW, rimHeight * 0.5f, 0f), new Vector3(rimThickness, rimHeight, DioramaSize.y));
            CreateRimBorder("Rim_West", new Vector3(-halfW, rimHeight * 0.5f, 0f), new Vector3(rimThickness, rimHeight, DioramaSize.y));

            // 3. 4 Corner Stone Totem Pillars (Ẩn mesh)
            float pillarH = 1.8f;
            float pillarW = 2.4f;
            CreatePillar("Corner_Pillar_NE", new Vector3(halfW, pillarH * 0.5f - 0.2f, halfL), new Vector3(pillarW, pillarH, pillarW));
            CreatePillar("Corner_Pillar_NW", new Vector3(-halfW, pillarH * 0.5f - 0.2f, halfL), new Vector3(pillarW, pillarH, pillarW));
            CreatePillar("Corner_Pillar_SE", new Vector3(halfW, pillarH * 0.5f - 0.2f, -halfL), new Vector3(pillarW, pillarH, pillarW));
            CreatePillar("Corner_Pillar_SW", new Vector3(-halfW, pillarH * 0.5f - 0.2f, -halfL), new Vector3(pillarW, pillarH, pillarW));

            _isDioramaBuilt = true;
        }

        private void CreateRimBorder(string rimName, Vector3 localPos, Vector3 localScale)
        {
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = rimName;
            rim.layer = 2; // Ignore Raycast so clicks pass through to ground
            rim.transform.SetParent(_pedestalRoot.transform, false);
            rim.transform.localPosition = localPos;
            rim.transform.localScale = localScale;
            // Ẩn hoàn toàn Renderer để giữ mặt bàn AR phẳng, không có viền đá che khuất
            Renderer r = rim.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            Collider c = rim.GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void CreatePillar(string pillarName, Vector3 localPos, Vector3 localScale)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = pillarName;
            pillar.layer = 2; // Ignore Raycast
            pillar.transform.SetParent(_pedestalRoot.transform, false);
            pillar.transform.localPosition = localPos;
            pillar.transform.localScale = new Vector3(localScale.x, localScale.y * 0.5f, localScale.z);
            // Ẩn hoàn toàn Renderer cột đá
            Renderer r = pillar.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            Collider c = pillar.GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void BuildShadowMatteReceiver()
        {
            if (_shadowMattePlane != null) return;

            // Dùng PrimitiveType.Plane có sẵn mặt phẳng ngang XZ và pháp tuyến (0, 1, 0) hướng thẳng lên trời
            // Khởi tạo độc lập hoàn toàn, không phụ thuộc vào _pedestalRoot để triệt tiêu 100% ngoại lệ MissingReferenceException
            _shadowMattePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _shadowMattePlane.name = "AR_Shadow_Matte_Receiver";

            // Đặt hơi thấp hơn chân công trình một chút (Y = -0.02m) để triệt tiêu Z-fighting,
            // tâm tại Z = -15m bao trọn toàn bộ cụm làng và hướng khủng long tiến công
            _shadowMattePlane.transform.position = new Vector3(0f, -0.02f, -15f);
            _shadowMattePlane.transform.rotation = Quaternion.identity;
            // Plane kích thước gốc là 10m x 10m -> Scale (16, 1, 16) = 160m x 160m bao trọn toàn bộ chiến trường
            _shadowMattePlane.transform.localScale = new Vector3(16f, 1f, 16f);

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
            _culledSceneryRenderers.Clear();

            // Quét toàn bộ Renderer trong Scene:
            // Bảo vệ toàn bộ công trình làng, tường thành và quân khủng long để đổ bóng xuống bàn;
            // Ẩn toàn bộ mặt sàn, cát nứt, cây cối rừng, thảm cỏ, tảng đá sa mạc xung quanh
            Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (Renderer r in allRenderers)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;

                // Bỏ qua AR camera feed, UI, Heatmap, và khung shadow matte
                string rName = r.gameObject.name;
                if (rName.StartsWith("AR_") || rName.StartsWith("Threat_") || rName.StartsWith("HealthBar_")) continue;
                if (_pedestalRoot != null && r.transform.IsChildOf(_pedestalRoot.transform)) continue;

                // Bảo vệ toàn bộ công trình làng, tường thành, tháp bắn và quân khủng long
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
                    // Luôn bật đổ bóng để các công trình và khủng long đổ bóng rõ nét lên mặt bàn thực tế!
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                    continue;
                }

                // Toàn bộ cảnh quan tự nhiên (cây cối rừng, cây cọ, tảng đá sa mạc, thảm cỏ, núi, mặt sàn đất, cracked sand) đều ẩn Renderer
                if (r.enabled)
                {
                    r.enabled = false;
                    _culledSceneryRenderers.Add(r);
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

            // 2. Tắt tấm hứng bóng AR
            if (_shadowMattePlane != null) _shadowMattePlane.SetActive(false);
            if (_pedestalRoot != null) _pedestalRoot.SetActive(false);

            // 3. BẢO CHỨNG TUYỆT ĐỐI 100%: Quét và bật lại toàn bộ Renderer trong Scene
            // (World, Ground, Núi Đá pb_Mesh, Cracked Sand, Rừng cây, Smooth Floor, v.v.)
            // để đảm bảo khi thoát AR về góc nhìn 3D không bao giờ bị biến thành khoảng xám vô cực (Hình 10)!
            Renderer[] allSceneRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in allSceneRenderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (n.StartsWith("AR_") || n.StartsWith("Threat_")) continue;
                if (!r.enabled) r.enabled = true;
                if (!r.gameObject.activeSelf && !n.StartsWith("Diorama_"))
                {
                    r.gameObject.SetActive(true);
                }
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
