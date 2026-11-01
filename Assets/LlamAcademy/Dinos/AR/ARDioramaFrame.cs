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
        public static ARDioramaFrame Instance
        {
            get
            {
                if (_instance == null)
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

        [Header("=== Diorama Dimensions ===")]
        public Vector3 DioramaCenter = new Vector3(0f, 0f, -5f);
        public Vector2 DioramaSize = new Vector2(90f, 92f); // Width (X), Length (Z)
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
        /// Builds the physical 3D Prehistoric Stone Pedestal Base enclosing the village.
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

            // 1. Main Pedestal Slab underneath the terrain
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Diorama_Main_Slab";
            slab.transform.SetParent(_pedestalRoot.transform, false);
            slab.transform.localPosition = new Vector3(0f, -PedestalHeight * 0.5f, 0f);
            slab.transform.localScale = new Vector3(DioramaSize.x + 2f, PedestalHeight, DioramaSize.y + 2f);
            ApplyMaterial(slab, _stoneMaterial);

            // 2. 4 Beveled Stone Border Rims
            float halfW = DioramaSize.x * 0.5f;
            float halfL = DioramaSize.y * 0.5f;
            float rimThickness = 1.6f;
            float rimHeight = 0.8f;

            // North Rim (+Z)
            CreateRimBorder("Rim_North", new Vector3(0f, rimHeight * 0.5f, halfL), new Vector3(DioramaSize.x + 2f, rimHeight, rimThickness));
            // South Rim (-Z)
            CreateRimBorder("Rim_South", new Vector3(0f, rimHeight * 0.5f, -halfL), new Vector3(DioramaSize.x + 2f, rimHeight, rimThickness));
            // East Rim (+X)
            CreateRimBorder("Rim_East", new Vector3(halfW, rimHeight * 0.5f, 0f), new Vector3(rimThickness, rimHeight, DioramaSize.y));
            // West Rim (-X)
            CreateRimBorder("Rim_West", new Vector3(-halfW, rimHeight * 0.5f, 0f), new Vector3(rimThickness, rimHeight, DioramaSize.y));

            // 3. 4 Corner Stone Totem Pillars
            float pillarH = 2.4f;
            float pillarW = 2.8f;
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
            rim.transform.SetParent(_pedestalRoot.transform, false);
            rim.transform.localPosition = localPos;
            rim.transform.localScale = localScale;
            ApplyMaterial(rim, _stoneMaterial);
        }

        private void CreatePillar(string pillarName, Vector3 localPos, Vector3 localScale)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = pillarName;
            pillar.transform.SetParent(_pedestalRoot.transform, false);
            pillar.transform.localPosition = localPos;
            pillar.transform.localScale = new Vector3(localScale.x, localScale.y * 0.5f, localScale.z);
            ApplyMaterial(pillar, _stoneMaterial);
        }

        private void BuildShadowMatteReceiver()
        {
            if (_shadowMattePlane != null) return;

            _shadowMattePlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _shadowMattePlane.name = "AR_Shadow_Matte_Receiver";
            _shadowMattePlane.transform.SetParent(_pedestalRoot.transform, false);

            // Lay flat horizontally at table surface (Y = -PedestalHeight - 0.02m)
            _shadowMattePlane.transform.localPosition = new Vector3(0f, -PedestalHeight - 0.02f, 0f);
            _shadowMattePlane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _shadowMattePlane.transform.localScale = new Vector3(DioramaSize.x * 2.2f, DioramaSize.y * 2.2f, 1f);

            // Shadow Matte Material
            Shader shadowShader = Shader.Find("Prehistoric/ARShadowMatte");
            if (shadowShader != null)
            {
                _shadowMatteMaterial = new Material(shadowShader);
            }
            else
            {
                // Unlit transparent shadow fallback
                _shadowMatteMaterial = PrehistoricShaderUtility.CreateUnlitMaterial(new Color(0.08f, 0.08f, 0.08f, 0.28f));
            }

            MeshRenderer mr = _shadowMattePlane.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = _shadowMatteMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = true;
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
        }

        /// <summary>
        /// Scans and hides background objects outside the diorama bounds so only the tabletop diorama displays in AR.
        /// </summary>
        private void ApplyBoundaryClipping()
        {
            _culledSceneryObjects.Clear();
            Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            float halfW = DioramaSize.x * 0.5f + 4f;
            float halfL = DioramaSize.y * 0.5f + 4f;

            foreach (Renderer r in allRenderers)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;

                // Do not cull diorama frame or gameplay units
                if (r.transform.IsChildOf(_pedestalRoot.transform)) continue;
                if (r.GetComponentInParent<Unit.Unit>() != null) continue;
                if (r.GetComponentInParent<Unit.Wall>() != null) continue;

                Vector3 pos = r.transform.position;
                float diffX = Mathf.Abs(pos.x - DioramaCenter.x);
                float diffZ = Mathf.Abs(pos.z - DioramaCenter.z);

                // If located outside the tabletop diorama bounds (e.g. distant backdrop mountains, infinite plane)
                if (diffX > halfW || diffZ > halfL)
                {
                    r.gameObject.SetActive(false);
                    _culledSceneryObjects.Add(r.gameObject);
                }
            }
            Debug.Log($"<color=cyan>[AR Diorama]</color> Bounded Clipping applied: { _culledSceneryObjects.Count } distant scenery objects culled outside tabletop diorama.");
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
