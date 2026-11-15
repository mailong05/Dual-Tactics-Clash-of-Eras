using System.Collections.Generic;
using LlamAcademy.Dinos.Rendering;
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
        private bool _isDioramaBuilt = false;

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
            BuildStonePedestal();
            BuildShadowMatteReceiver();
            SetDioramaActive(false); // Default inactive until AR mode turns on
        }

        /// <summary>
        /// Builds the physical 3D Prehistoric Stone Pedestal Base enclosing the village and tactical forest.
        /// </summary>
        public void BuildStonePedestal()
        {
            if (_isDioramaBuilt) return;

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

            _shadowMattePlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _shadowMattePlane.name = "AR_Shadow_Matte_Receiver";
            _shadowMattePlane.transform.SetParent(_pedestalRoot.transform, false);

            // Lay flat horizontally at table surface (Y = -0.01m)
            _shadowMattePlane.transform.localPosition = new Vector3(0f, -0.01f, 0f);
            _shadowMattePlane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Kích thước vừa vặn bao trọn khu vực làng và hành lang di chuyển (không vượt ra ngoài bán kính bóng)
            _shadowMattePlane.transform.localScale = new Vector3(DioramaSize.x * 1.25f, DioramaSize.y * 1.25f, 1f);

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
                    // Tuyệt đối không để material opaque unlit màu đen che khuất khung hình camera!
                    mr.enabled = false;
                }
            }

            Collider col = _shadowMattePlane.GetComponent<Collider>();
            if (col != null) Destroy(col);
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
            if (!isARActive && !_isDioramaBuilt) return;
            if (!_isDioramaBuilt) BuildStonePedestal();
            if (_shadowMattePlane == null) BuildShadowMatteReceiver();

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
        /// Scans and hides background objects outside the diorama bounds so only the tabletop diorama displays in AR.
        /// </summary>
        private void ApplyBoundaryClipping()
        {
            _culledSceneryObjects.Clear();

            // 1. Ẩn trực tiếp các đối tượng cảnh quan tự nhiên lớn thuộc World:
            // - "Ground": Chứa toàn bộ mặt đất sa mạc (Cracked Sand) và toàn bộ 200 cây cối (Vegetation: Palm, Cycads, Bushes, Grass)
            // - Toàn bộ 5 khối núi đá: "mountain-desert", "mountain-desert (1)", "mountain-desert (2)", v.v.
            // - "Smooth Floor": Tấm sàn phụ
            // - "Camera Bounds": Khung giới hạn camera
            GameObject world = GameObject.Find("World");
            if (world != null)
            {
                Transform worldT = world.transform;
                for (int i = 0; i < worldT.childCount; i++)
                {
                    Transform child = worldT.GetChild(i);
                    string cName = child.name.ToLowerInvariant();

                    // Bảo vệ các đối tượng làng hoặc logic gameplay nếu nằm trong World
                    bool isVillageObj = cName.Contains("village")
                        || cName.Contains("bridge")
                        || cName.Contains("wall")
                        || cName.Contains("tower")
                        || cName.Contains("fortress")
                        || cName.Contains("hut")
                        || cName.Contains("base")
                        || cName.Contains("spawn")
                        || cName.Contains("navmesh");

                    if (isVillageObj) continue;

                    // Ẩn Ground, toàn bộ núi đá mountain-desert, Smooth Floor, v.v.
                    if (cName.Contains("ground") || cName.Contains("mountain") || cName.Contains("floor")
                        || cName.Contains("sand") || cName.Contains("vegetation") || cName.Contains("bounds"))
                    {
                        if (child.gameObject.activeSelf)
                        {
                            child.gameObject.SetActive(false);
                            _culledSceneryObjects.Add(child.gameObject);
                        }
                    }
                }
            }
            else
            {
                GameObject groundGo = GameObject.Find("Ground");
                if (groundGo != null && groundGo.activeSelf)
                {
                    groundGo.SetActive(false);
                    _culledSceneryObjects.Add(groundGo);
                }
            }

            // 2. Quét toàn bộ Renderer còn lại trong Scene để ẩn triệt để mọi núi đá, cây cối, vách đá:
            // Chỉ giữ lại DUY NHẤT: Công trình làng (tường, tháp, chòi, cầu, nhà chính), quân lính/khủng long, và đối tượng AR
            Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (Renderer r in allRenderers)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;

                // Bỏ qua nếu thuộc AR / UI / Camera / Khung sa bàn
                if (r.gameObject.name.StartsWith("AR_") || r.gameObject.name.StartsWith("Threat_") || r.gameObject.name.StartsWith("HealthBar_")) continue;
                if (_pedestalRoot != null && r.transform.IsChildOf(_pedestalRoot.transform)) continue;

                // Bảo vệ toàn bộ công trình làng và quân lính
                if (r.GetComponentInParent<Unit.Unit>() != null) continue;
                if (r.GetComponentInParent<Unit.Wall>() != null) continue;

                string objName = r.gameObject.name.ToLowerInvariant();
                bool isVillageOrUnit = objName.Contains("village")
                    || objName.Contains("bridge")
                    || objName.Contains("fortress")
                    || objName.Contains("hut")
                    || objName.Contains("arch")
                    || objName.Contains("fence")
                    || objName.Contains("tower")
                    || objName.Contains("spawn")
                    || objName.Contains("base")
                    || objName.Contains("gate")
                    || objName.Contains("defense")
                    || objName.Contains("look target");

                if (isVillageOrUnit) continue;

                // Bất kỳ Renderer nào không thuộc làng hay quân lính đều là cảnh quan tự nhiên (núi, cây, đá)
                // -> Ẩn đi để nhường chỗ hoàn toàn cho không gian phòng thực tế từ camera AR (chuẩn xác Hình 2)!
                r.gameObject.SetActive(false);
                _culledSceneryObjects.Add(r.gameObject);
            }

            Debug.Log($"<color=cyan>[AR Diorama]</color> Đã dọn dẹp sạch toàn bộ cảnh quan tự nhiên: {_culledSceneryObjects.Count} đối tượng (núi đá, rừng cây, mặt đất) đã được ẩn để lộ 100% camera phòng thực tế chuẩn Hình 2!");
        }

        /// <summary>
        /// Restores all culled distant scenery objects when exiting AR back to standard 3D God-view.
        /// </summary>
        private void RestoreBoundaryClipping()
        {
            for (int i = 0; i < _culledSceneryObjects.Count; i++)
            {
                if (_culledSceneryObjects[i] != null)
                {
                    _culledSceneryObjects[i].SetActive(true);
                }
            }
            _culledSceneryObjects.Clear();
            if (_shadowMattePlane != null) _shadowMattePlane.SetActive(false);
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
