using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

#if AR_FOUNDATION_ENABLED
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
#endif

namespace LlamAcademy.Dinos.AR
{
    /// <summary>
    /// Hybrid Augmented Reality System:
    /// 1. Mobile Android (AR Foundation 6.6.2 + Google ARCore): Real 6-DOF Plane Detection, Raycast Tap-to-Place,
    ///    and XROrigin world-scale mapping so the 3D battlefield locks rock-solidly onto a physical desk while
    ///    preserving 1.0x NavMesh & Threat-Aware A* pathfinding integrity.
    /// 2. PC / Non-ARCore Fallback: Compact Tabletop Diorama over live WebCamTexture pass-through (with holographic grid fallback),
    ///    automatically culling out-of-bounds terrain slabs and projecting directional shadows via ARShadowMatte.
    /// </summary>
    [DefaultExecutionOrder(-6)]
    public class ARTabletopController : MonoBehaviour
    {
        public static ARTabletopController Instance { get; private set; }

        [Header("AR State")]
        public bool IsARModeActive { get; private set; } = false;
        public bool IsCameraStreaming { get; private set; } = false;
        public bool IsUsingRealARFoundation { get; private set; } = false;
        public bool IsSurfacePlaced { get; private set; } = false;
        public string CameraStatusMessage { get; private set; } = "";

        [Header("Spatial Anchor & Diorama Scaling")]
        public float DioramaScale { get; private set; } = 1.0f;
        public Vector3 TableAnchorOffset { get; private set; } = Vector3.zero;
        public float TableAnchorYaw { get; private set; } = 0f;
        public bool IsPlaneAnchorLocked { get; private set; } = true;

        // Camera state caches
        private Material _OriginalSkybox;
        private CameraClearFlags _OriginalCameraClearFlags = CameraClearFlags.Skybox;
        private Color _OriginalCameraBackgroundColor = Color.black;
#if AR_FOUNDATION_ENABLED
        private Transform _OriginalCameraParent;
        private Vector3 _OriginalCameraLocalPos;
        private Quaternion _OriginalCameraLocalRot;
        private float _OriginalNearClip = 0.3f;
        private float _OriginalFarClip = 1000f;
        private bool _HadCinemachineBrain = false;
        private CinemachineBrain _CachedBrain;
#endif

        // Fallback Video Pass-Through Objects
        private WebCamTexture _WebCamTexture;
        private GameObject _BackgroundQuad;
        private MeshRenderer _BackgroundQuadRenderer;
        private Material _BackgroundQuadMaterial;

#if AR_FOUNDATION_ENABLED
        private GameObject _arSessionGO;
        private GameObject _xrOriginGO;
        private XROrigin _xrOrigin;
        private ARSession _arSession;
        private ARPlaneManager _arPlaneManager;
        private ARRaycastManager _arRaycastManager;
        private ARCameraManager _arCameraManager;
        private ARCameraBackground _arCameraBackground;
        private TrackedPoseDriver _trackedPoseDriver;
        private GameObject _runtimePlanePrefab;
        private Material _arPlaneMaterial;
        private Vector3 _placedTrackingLocalPos = Vector3.zero;
        private readonly List<ARRaycastHit> _arHits = new();
#endif

        // UI & Hologram base
        private GameObject ARControlPill;
        private Text ARButtonText;
        private GameObject ARPlaneGrid;
        private Coroutine CameraInitRoutine;

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
            CreateARButtonUI();
            CreateARPlaneGrid();
        }

        private void CreateARButtonUI()
        {
            Canvas canvas = FindSuitableOverlayCanvas();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("AR_ScreenCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 360;

                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            ARControlPill = new GameObject("AR_Mode_Toggle_Button");
            ARControlPill.SetActive(false);
            ARControlPill.transform.SetParent(canvas.transform, false);

            RectTransform rt = ARControlPill.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -68f);
            rt.sizeDelta = new Vector2(210f, 42f);

            Image img = ARControlPill.AddComponent<Image>();
            img.color = new Color(0.10f, 0.42f, 0.90f, 0.92f);

            Outline outl = ARControlPill.AddComponent<Outline>();
            outl.effectColor = new Color(0.4f, 0.85f, 1.0f, 0.9f);
            outl.effectDistance = new Vector2(2f, 2f);

            Button btn = ARControlPill.AddComponent<Button>();
            btn.onClick.AddListener(ToggleARMode);

            GameObject textGO = new GameObject("Label");
            textGO.transform.SetParent(ARControlPill.transform, false);
            ARButtonText = textGO.AddComponent<Text>();
            ARButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ARButtonText.text = "📱 <b>CHẾ ĐỘ AR SA BÀN</b>";
            ARButtonText.fontSize = 12;
            ARButtonText.alignment = TextAnchor.MiddleCenter;
            ARButtonText.color = Color.white;

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
        }

        private Canvas FindSuitableOverlayCanvas()
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Canvas c in allCanvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name != "HealthBarCanvas")
                {
                    return c;
                }
            }
            return null;
        }

        private void CreateARPlaneGrid()
        {
            ARPlaneGrid = new GameObject("AR_Tracking_Grid_Plane");
            ARPlaneGrid.transform.position = new Vector3(0f, -0.05f, -5f);

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "AR_Grid_Mesh";
            plane.transform.SetParent(ARPlaneGrid.transform, false);
            plane.transform.localScale = new Vector3(7.2f, 1f, 7.6f);

            Material mat = Rendering.PrehistoricShaderUtility.CreateUnlitMaterial(new Color(0.1f, 0.70f, 0.95f, 0.16f));
            Renderer r = plane.GetComponent<Renderer>();
            if (r != null) r.material = mat;

            Collider col = plane.GetComponent<Collider>();
            if (col != null) Destroy(col);

            ARPlaneGrid.SetActive(false);
        }

        private void Update()
        {
            // Hotkey 'R' toggles AR Tabletop Mode (when not placing tower, not in Main Menu, and not on GameOver/Victory modal)
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                bool isMenuOpen = UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen;
                var gp = RoundManagement.PrehistoricGameplayManager.Instance;
                bool isEndModal = gp != null && (gp.CurrentPhase == RoundManagement.PrehistoricWavePhase.GameOver || gp.CurrentPhase == RoundManagement.PrehistoricWavePhase.Victory);
                bool isPlacing = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;
                if (!isPlacing && !isMenuOpen && !isEndModal)
                {
                    ToggleARMode();
                }
            }

            if (!IsARModeActive) return;

            if (IsUsingRealARFoundation)
            {
                HandleARFoundationInput();
            }
            else
            {
                HandleFallbackARGestures();
                UpdateBackgroundQuadTransform();
            }
        }

        private void LateUpdate()
        {
#if AR_FOUNDATION_ENABLED
            if (IsARModeActive && IsUsingRealARFoundation && _xrOrigin != null)
            {
                UpdateXROriginWorldTransform();
            }
#endif
        }

        private void HandleARFoundationInput()
        {
#if AR_FOUNDATION_ENABLED
            // 1. Scanning Phase: Tap on detected real plane to place diorama onto the physical desk
            if (!IsSurfacePlaced)
            {
                Vector2 tapPos = Vector2.zero;
                bool tapped = false;

                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                {
                    tapPos = Touchscreen.current.primaryTouch.position.ReadValue();
                    tapped = true;
                }
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    tapPos = Mouse.current.position.ReadValue();
                    tapped = true;
                }

                if (tapped && !Mobile.MobileUIHelper.IsPointerOverUI(tapPos) && _arRaycastManager != null)
                {
                    if (_arRaycastManager.Raycast(tapPos, _arHits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
                    {
                        Pose hitPose = _arHits[0].pose;
                        _placedTrackingLocalPos = _xrOrigin.TrackablesParent != null
                            ? _xrOrigin.TrackablesParent.InverseTransformPoint(hitPose.position)
                            : _xrOrigin.transform.InverseTransformPoint(hitPose.position);

                        IsSurfacePlaced = true;
                        IsPlaneAnchorLocked = true;
                        SetARPlanesVisible(false);

                        if (RoundManagement.PrehistoricGameplayManager.Instance != null)
                        {
                            RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement("✅ ĐÃ ĐẶT SA BÀN LÊN MẶT BÀN THẬT (6-DOF)!", 3.0f);
                        }
                    }
                }
                return;
            }

            // 2. Placed Phase: 2-finger Twist & Pinch resize/rotate diorama right on the real desk
            if (Touchscreen.current != null && Touchscreen.current.touches.Count >= 2)
            {
                var t0 = Touchscreen.current.touches[0];
                var t1 = Touchscreen.current.touches[1];
                if (t0.isInProgress && t1.isInProgress)
                {
                    Vector2 pos0 = t0.position.ReadValue();
                    Vector2 pos1 = t1.position.ReadValue();
                    Vector2 prev0 = pos0 - t0.delta.ReadValue();
                    Vector2 prev1 = pos1 - t1.delta.ReadValue();

                    float prevDist = Vector2.Distance(prev0, prev1);
                    float curDist = Vector2.Distance(pos0, pos1);
                    float pinchDelta = (curDist - prevDist) * 0.003f;
                    if (Mathf.Abs(pinchDelta) > 0.001f)
                    {
                        AdjustDioramaScale(pinchDelta);
                    }

                    float prevAngle = Mathf.Atan2(prev1.y - prev0.y, prev1.x - prev0.x) * Mathf.Rad2Deg;
                    float curAngle = Mathf.Atan2(pos1.y - pos0.y, pos1.x - pos0.x) * Mathf.Rad2Deg;
                    float twistDelta = Mathf.DeltaAngle(prevAngle, curAngle);
                    if (Mathf.Abs(twistDelta) > 0.1f)
                    {
                        RotateTabletopDiorama(-twistDelta * 0.8f);
                    }
                }
            }
#endif
        }

        private void HandleFallbackARGestures()
        {
            bool isPlacing = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;

            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                if (!Mobile.MobileUIHelper.IsPointerOverUI(mousePos))
                {
                    if (Mouse.current.leftButton.isPressed && !isPlacing)
                    {
                        Vector2 delta = Mouse.current.delta.ReadValue();
                        if (Mathf.Abs(delta.x) > 0.05f)
                        {
                            RotateTabletopDiorama(delta.x * 0.35f);
                        }
                    }

                    if (Mouse.current.rightButton.isPressed)
                    {
                        Vector2 delta = Mouse.current.delta.ReadValue();
                        PanTabletopDiorama(new Vector3(delta.x * 0.05f, 0f, delta.y * 0.05f));
                    }

                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.05f)
                    {
                        AdjustDioramaScale(scroll > 0 ? 0.05f : -0.05f);
                    }
                }

                if (Keyboard.current != null)
                {
                    if (Keyboard.current.leftBracketKey.isPressed) RotateTabletopDiorama(-60f * Time.deltaTime);
                    if (Keyboard.current.rightBracketKey.isPressed) RotateTabletopDiorama(60f * Time.deltaTime);
                }
            }

            if (Touchscreen.current != null && Touchscreen.current.touches.Count >= 2)
            {
                var t0 = Touchscreen.current.touches[0];
                var t1 = Touchscreen.current.touches[1];

                if (t0.isInProgress && t1.isInProgress)
                {
                    Vector2 pos0 = t0.position.ReadValue();
                    Vector2 pos1 = t1.position.ReadValue();
                    Vector2 prev0 = pos0 - t0.delta.ReadValue();
                    Vector2 prev1 = pos1 - t1.delta.ReadValue();

                    float prevDist = Vector2.Distance(prev0, prev1);
                    float curDist = Vector2.Distance(pos0, pos1);
                    float pinchDelta = (curDist - prevDist) * 0.003f;
                    if (Mathf.Abs(pinchDelta) > 0.001f)
                    {
                        AdjustDioramaScale(pinchDelta);
                    }

                    float prevAngle = Mathf.Atan2(prev1.y - prev0.y, prev1.x - prev0.x) * Mathf.Rad2Deg;
                    float curAngle = Mathf.Atan2(pos1.y - pos0.y, pos1.x - pos0.x) * Mathf.Rad2Deg;
                    float twistDelta = Mathf.DeltaAngle(prevAngle, curAngle);
                    if (Mathf.Abs(twistDelta) > 0.1f)
                    {
                        RotateTabletopDiorama(-twistDelta * 0.8f);
                    }
                }
            }
        }

        public void RotateTabletopDiorama(float angleDelta)
        {
            TableAnchorYaw += angleDelta;
            if (!IsUsingRealARFoundation)
            {
                CameraControl.Instance?.OrbitCamera(angleDelta);
            }
        }

        public void PanTabletopDiorama(Vector3 panDelta)
        {
            TableAnchorOffset += panDelta;
            if (!IsUsingRealARFoundation && CameraControl.Instance != null && Camera.main != null)
            {
                Vector3 right = Camera.main.transform.right;
                right.y = 0;
                Vector3 forward = Camera.main.transform.forward;
                forward.y = 0;
                Vector3 worldMove = right.normalized * panDelta.x + forward.normalized * panDelta.z;
                CameraControl.Instance.transform.position += worldMove;
            }
        }

        public void AdjustDioramaScale(float delta)
        {
            DioramaScale = Mathf.Clamp(DioramaScale + delta, 0.4f, 2.2f);
            if (!IsUsingRealARFoundation && CameraControl.Instance != null)
            {
                CameraControl.Instance.ZoomCamera(-delta * 35f);
            }
        }

        public void RepositionARTableSurface()
        {
#if AR_FOUNDATION_ENABLED
            if (IsUsingRealARFoundation)
            {
                IsSurfacePlaced = false;
                IsPlaneAnchorLocked = false;
                SetARPlanesVisible(true);
            }
#endif
        }

        public void ToggleARMode()
        {
            IsARModeActive = !IsARModeActive;

            if (IsARModeActive)
            {
                ActivateARTabletop();
            }
            else
            {
                DeactivateARTabletop();
            }

            if (ARButtonText != null)
            {
                ARButtonText.text = IsARModeActive ? "🔙 <b>THOÁT AR (VỀ 3D)</b>" : "📱 <b>CHẾ ĐỘ AR SA BÀN</b>";
            }
        }

        private void ActivateARTabletop()
        {
            Debug.Log("<color=cyan>[AR System]</color> Tabletop AR Mode Activated. Checking ARCore / Camera Pass-Through...");

            SetupCameraForAR();
            ARDioramaFrame.Instance.SetARMode(true);

            if (CameraInitRoutine != null) StopCoroutine(CameraInitRoutine);
            CameraInitRoutine = StartCoroutine(InitializeARExperienceRoutine());
        }

        private void DeactivateARTabletop()
        {
            Debug.Log("<color=cyan>[AR System]</color> Restoring Standard 3D Tactical Battlefield...");

            if (CameraInitRoutine != null)
            {
                StopCoroutine(CameraInitRoutine);
                CameraInitRoutine = null;
            }

            CleanupARFoundation();
            StopWebCamFeed();

            if (ARDioramaFrame.HasInstance)
            {
                ARDioramaFrame.Instance.SetARMode(false);
            }

            RestoreCameraFromAR();

            if (ARPlaneGrid != null) ARPlaneGrid.SetActive(false);

            IsUsingRealARFoundation = false;
            IsSurfacePlaced = false;

            if (RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement("🎮 ĐÃ THOÁT AR, TRỞ VỀ GÓC NHÌN 3D CHIẾN TRƯỜNG TIỀN SỬ.", 2.5f);
            }
        }

        private IEnumerator InitializeARExperienceRoutine()
        {
#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                float permTimeout = 15.0f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && permTimeout > 0f)
                {
                    permTimeout -= Time.unscaledDeltaTime;
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
#endif

#if AR_FOUNDATION_ENABLED
            // Attempt real AR Foundation (ARCore on Android / XR Simulation in Editor)
            if (Application.isMobilePlatform && XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                CameraStatusMessage = "Đang khởi động ARCore Plane Tracking...";
                var xrMgr = XRGeneralSettings.Instance.Manager;
                if (xrMgr.activeLoader == null)
                {
                    yield return xrMgr.InitializeLoader();
                }

                if (xrMgr.activeLoader != null)
                {
                    xrMgr.StartSubsystems();
                    SetupARFoundationObjects();

                    // Check ARSession availability up to 3.5 seconds
                    yield return ARSession.CheckAvailability();
                    float waitSec = 3.5f;
                    while (ARSession.state == ARSessionState.CheckingAvailability && waitSec > 0f)
                    {
                        waitSec -= Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (ARSession.state == ARSessionState.Ready ||
                        ARSession.state == ARSessionState.SessionInitializing ||
                        ARSession.state == ARSessionState.SessionTracking)
                    {
                        IsUsingRealARFoundation = true;
                        IsSurfacePlaced = false;
                        IsPlaneAnchorLocked = false;
                        IsCameraStreaming = true;
                        CameraStatusMessage = "📡 ARCore 6-DOF đang quét mặt bàn — Chạm vào lưới xanh để đặt sa bàn!";

                        if (RoundManagement.PrehistoricGameplayManager.Instance != null)
                        {
                            RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement(
                                "📡 ARCORE ĐANG QUÉT MẶT BÀN! Rà camera quanh mặt bàn rồi CHẠM để ĐẶT SA BÀN 3D!", 4.5f);
                        }
                        yield break;
                    }
                }

                // Device doesn't support ARCore (or ARCore APK missing) -> cleanup and fall back smoothly
                Debug.LogWarning("[AR Foundation] ARCore unavailable on this device. Falling back to Video Pass-Through Tabletop.");
                CleanupARFoundation();
            }
#endif

            // PC Standalone / Non-ARCore Fallback: Compact Diorama + Live Camera Pass-Through
            IsUsingRealARFoundation = false;
            IsSurfacePlaced = true;
            IsPlaneAnchorLocked = true;
            if (ARPlaneGrid != null) ARPlaneGrid.SetActive(true);

            yield return StartWebCamFeedRoutine();
        }

#if AR_FOUNDATION_ENABLED
        private void SetupARFoundationObjects()
        {
            if (Camera.main == null) return;

            Camera mainCam = Camera.main;
            _OriginalCameraParent = mainCam.transform.parent;
            _OriginalCameraLocalPos = mainCam.transform.localPosition;
            _OriginalCameraLocalRot = mainCam.transform.localRotation;
            _OriginalNearClip = mainCam.nearClipPlane;
            _OriginalFarClip = mainCam.farClipPlane;

            _CachedBrain = mainCam.GetComponent<CinemachineBrain>();
            if (_CachedBrain != null)
            {
                _HadCinemachineBrain = _CachedBrain.enabled;
                _CachedBrain.enabled = false;
            }

            // 1. ARSession root
            _arSessionGO = new GameObject("AR_Session_Root");
            _arSession = _arSessionGO.AddComponent<ARSession>();

            // 2. XROrigin + CameraOffset hierarchy (scale keeps 1.0x NavMesh intact!)
            _xrOriginGO = new GameObject("AR_XR_Origin");
            _xrOrigin = _xrOriginGO.AddComponent<XROrigin>();

            GameObject camOffsetGO = new GameObject("Camera Offset");
            camOffsetGO.transform.SetParent(_xrOriginGO.transform, false);
            _xrOrigin.CameraFloorOffsetObject = camOffsetGO;
            _xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            // Reparent MainCamera cleanly under XROrigin/Camera Offset
            mainCam.transform.SetParent(camOffsetGO.transform, false);
            mainCam.transform.localPosition = Vector3.zero;
            mainCam.transform.localRotation = Quaternion.identity;
            mainCam.nearClipPlane = 0.05f;
            mainCam.farClipPlane = 4500f;
            _xrOrigin.Camera = mainCam;

            // Attach TrackedPoseDriver for live 6-DOF phone movement
            _trackedPoseDriver = mainCam.GetComponent<TrackedPoseDriver>();
            if (_trackedPoseDriver == null)
            {
                _trackedPoseDriver = mainCam.gameObject.AddComponent<TrackedPoseDriver>();
            }
            ConfigureTrackedPoseDriverActions(_trackedPoseDriver);
            _trackedPoseDriver.enabled = true;

            // Attach ARCameraManager & ARCameraBackground (URP ARBackgroundRendererFeature renders camera feed!)
            _arCameraManager = mainCam.GetComponent<ARCameraManager>();
            if (_arCameraManager == null) _arCameraManager = mainCam.gameObject.AddComponent<ARCameraManager>();
            _arCameraManager.autoFocusRequested = true;
            _arCameraManager.enabled = true;

            _arCameraBackground = mainCam.GetComponent<ARCameraBackground>();
            if (_arCameraBackground == null) _arCameraBackground = mainCam.gameObject.AddComponent<ARCameraBackground>();
            _arCameraBackground.enabled = true;

            // Attach ARPlaneManager (horizontal desk/floor detection) + ARRaycastManager
            _runtimePlanePrefab = CreateRuntimeARPlanePrefab();
            _arPlaneManager = _xrOriginGO.AddComponent<ARPlaneManager>();
            _arPlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            _arPlaneManager.planePrefab = _runtimePlanePrefab;

            _arRaycastManager = _xrOriginGO.AddComponent<ARRaycastManager>();
        }

        private static void ConfigureTrackedPoseDriverActions(TrackedPoseDriver driver)
        {
            var posAction = new InputAction("Position", InputActionType.Value, expectedControlType: "Vector3");
            posAction.AddBinding("<XRHMD>/centerEyePosition");
            posAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            driver.positionInput = new InputActionProperty(posAction);

            var rotAction = new InputAction("Rotation", InputActionType.Value, expectedControlType: "Quaternion");
            rotAction.AddBinding("<XRHMD>/centerEyeRotation");
            rotAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            driver.rotationInput = new InputActionProperty(rotAction);

            var stateAction = new InputAction("TrackingState", InputActionType.Value, expectedControlType: "Integer");
            stateAction.AddBinding("<XRHMD>/trackingState");
            stateAction.AddBinding("<HandheldARInputDevice>/trackingState");
            driver.trackingStateInput = new InputActionProperty(stateAction);
        }

        private GameObject CreateRuntimeARPlanePrefab()
        {
            GameObject prefab = new GameObject("AR_Detected_Plane_Prefab");
            prefab.SetActive(false);

            prefab.AddComponent<ARPlane>();
            prefab.AddComponent<ARPlaneMeshVisualizer>();
            prefab.AddComponent<MeshFilter>();
            MeshRenderer mr = prefab.AddComponent<MeshRenderer>();

            Shader unlitShader = Rendering.PrehistoricShaderUtility.GetUnlitShader();
            _arPlaneMaterial = new Material(unlitShader);
            Color cyanGrid = new Color(0.05f, 0.85f, 1.0f, 0.28f);
            if (_arPlaneMaterial.HasProperty("_BaseColor")) _arPlaneMaterial.SetColor("_BaseColor", cyanGrid);
            if (_arPlaneMaterial.HasProperty("_Color")) _arPlaneMaterial.SetColor("_Color", cyanGrid);
            if (_arPlaneMaterial.HasProperty("_Surface")) _arPlaneMaterial.SetFloat("_Surface", 1f);
            _arPlaneMaterial.renderQueue = 3000;
            mr.material = _arPlaneMaterial;

            return prefab;
        }

        private void SetARPlanesVisible(bool visible)
        {
            if (_arPlaneManager == null) return;
            foreach (ARPlane plane in _arPlaneManager.trackables)
            {
                if (plane != null) plane.gameObject.SetActive(visible);
            }
        }

        private void UpdateXROriginWorldTransform()
        {
            if (_xrOrigin == null) return;

            if (!IsSurfacePlaced)
            {
                // Scanning phase: move camera far beyond farClipPlane (4500m) so the battlefield is hidden while scanning AR planes on the real desk!
                _xrOrigin.transform.localScale = Vector3.one;
                _xrOrigin.transform.rotation = Quaternion.identity;
                _xrOrigin.transform.position = new Vector3(0f, 12000f, 0f);
                return;
            }

            // Placed phase: keep NavMesh 1.0x intact while scaling XROrigin around the anchored desk hit point!
            float worldScale = 48f / Mathf.Clamp(DioramaScale, 0.4f, 2.2f);
            _xrOrigin.transform.localScale = Vector3.one * worldScale;

            Quaternion originRot = Quaternion.Euler(0f, -TableAnchorYaw, 0f);
            _xrOrigin.transform.rotation = originRot;

            Vector3 mapCenter = ARDioramaFrame.HasInstance
                ? ARDioramaFrame.Instance.DioramaCenter + new Vector3(0f, -ARDioramaFrame.Instance.PedestalHeight, 0f)
                : new Vector3(0f, -1.8f, -5f);

            _xrOrigin.transform.position = mapCenter - (originRot * (_placedTrackingLocalPos * worldScale));
        }
#endif

        private void CleanupARFoundation()
        {
#if AR_FOUNDATION_ENABLED
            if (Camera.main != null)
            {
                Camera mainCam = Camera.main;
                if (_arCameraBackground != null) _arCameraBackground.enabled = false;
                if (_arCameraManager != null) _arCameraManager.enabled = false;
                if (_trackedPoseDriver != null) _trackedPoseDriver.enabled = false;

                if (mainCam.transform.parent != _OriginalCameraParent)
                {
                    mainCam.transform.SetParent(_OriginalCameraParent, false);
                    mainCam.transform.localPosition = _OriginalCameraLocalPos;
                    mainCam.transform.localRotation = _OriginalCameraLocalRot;
                }
                mainCam.nearClipPlane = _OriginalNearClip;
                mainCam.farClipPlane = _OriginalFarClip;

                if (_CachedBrain != null)
                {
                    _CachedBrain.enabled = _HadCinemachineBrain;
                }
            }

            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null && XRGeneralSettings.Instance.Manager.activeLoader != null)
            {
                XRGeneralSettings.Instance.Manager.StopSubsystems();
                XRGeneralSettings.Instance.Manager.DeinitializeLoader();
            }

            if (_xrOriginGO != null) { Destroy(_xrOriginGO); _xrOriginGO = null; _xrOrigin = null; }
            if (_arSessionGO != null) { Destroy(_arSessionGO); _arSessionGO = null; _arSession = null; }
            if (_runtimePlanePrefab != null) { Destroy(_runtimePlanePrefab); _runtimePlanePrefab = null; }
            if (_arPlaneMaterial != null) { Destroy(_arPlaneMaterial); _arPlaneMaterial = null; }
#endif
        }

        private void SetupCameraForAR()
        {
            if (Camera.main == null) return;

            _OriginalSkybox = RenderSettings.skybox;
            RenderSettings.skybox = null;

            _OriginalCameraClearFlags = Camera.main.clearFlags;
            _OriginalCameraBackgroundColor = Camera.main.backgroundColor;

            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0f, 0f, 0f, 0f);

            CameraControl.Instance?.ResetToCenter();
        }

        private void RestoreCameraFromAR()
        {
            if (_OriginalSkybox != null)
            {
                RenderSettings.skybox = _OriginalSkybox;
            }

            if (Camera.main != null)
            {
                Camera.main.clearFlags = _OriginalCameraClearFlags;
                Camera.main.backgroundColor = _OriginalCameraBackgroundColor;
            }
        }

        private IEnumerator StartWebCamFeedRoutine()
        {
            CameraStatusMessage = "Đang xin quyền và dò tìm Camera...";

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogWarning("[AR Camera] No camera devices found. Using Cyber Tabletop Hologram mode.");
                CameraStatusMessage = "Không tìm thấy Camera phần cứng. Đang dùng Sa Bàn 3D Hologram.";
                IsCameraStreaming = false;
                EnsureARBackgroundSetup(null);
                yield break;
            }

            string targetDeviceName = devices[0].name;
            for (int i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing)
                {
                    targetDeviceName = devices[i].name;
                    break;
                }
            }

            try
            {
                if (_WebCamTexture != null)
                {
                    if (_WebCamTexture.isPlaying) _WebCamTexture.Stop();
                    Destroy(_WebCamTexture);
                }

                _WebCamTexture = new WebCamTexture(targetDeviceName, 1280, 720, 30);
                _WebCamTexture.Play();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AR Camera] Error initializing WebCamTexture: {ex.Message}");
                CameraStatusMessage = "Không thể mở luồng Camera. Đang dùng Sa Bàn 3D Hologram.";
                IsCameraStreaming = false;
                EnsureARBackgroundSetup(null);
                yield break;
            }

            float timeout = 6.0f;
            while (_WebCamTexture != null && (!_WebCamTexture.isPlaying || _WebCamTexture.width <= 16) && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (_WebCamTexture != null && _WebCamTexture.isPlaying)
            {
                IsCameraStreaming = true;
                CameraStatusMessage = $"📸 PC AR Preview ({targetDeviceName})";
                EnsureARBackgroundSetup(_WebCamTexture);
            }
            else
            {
                IsCameraStreaming = false;
                CameraStatusMessage = "Chưa nhận được khung hình camera. Đang dùng Sa Bàn 3D Hologram.";
                EnsureARBackgroundSetup(null);
            }
        }

        private void EnsureARBackgroundSetup(Texture videoTexture)
        {
            if (Camera.main == null) return;

            if (_BackgroundQuad == null)
            {
                _BackgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _BackgroundQuad.name = "AR_Video_Background_Quad";

                Collider col = _BackgroundQuad.GetComponent<Collider>();
                if (col != null) Destroy(col);

                _BackgroundQuadRenderer = _BackgroundQuad.GetComponent<MeshRenderer>();
                _BackgroundQuadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _BackgroundQuadRenderer.receiveShadows = false;
                _BackgroundQuadRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _BackgroundQuadRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                Shader unlitShader = Rendering.PrehistoricShaderUtility.GetUnlitShader();
                _BackgroundQuadMaterial = new Material(unlitShader);

                _BackgroundQuadMaterial.renderQueue = 1000;
                _BackgroundQuadMaterial.SetInt("_ZWrite", 0);
                _BackgroundQuadMaterial.DisableKeyword("_ZWRITE_ON");
                _BackgroundQuadMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

                if (_BackgroundQuadMaterial.HasProperty("_Surface"))
                {
                    _BackgroundQuadMaterial.SetFloat("_Surface", 1);
                }

                _BackgroundQuadRenderer.material = _BackgroundQuadMaterial;
            }

            _BackgroundQuad.transform.SetParent(Camera.main.transform, false);
            UpdateBackgroundQuadTransform();

            if (_BackgroundQuadMaterial != null)
            {
                if (videoTexture != null)
                {
                    if (_BackgroundQuadMaterial.HasProperty("_BaseMap"))
                        _BackgroundQuadMaterial.SetTexture("_BaseMap", videoTexture);
                    if (_BackgroundQuadMaterial.HasProperty("_MainTex"))
                        _BackgroundQuadMaterial.SetTexture("_MainTex", videoTexture);
                    _BackgroundQuadMaterial.mainTexture = videoTexture;
                    _BackgroundQuadMaterial.color = Color.white;
                }
                else
                {
                    _BackgroundQuadMaterial.mainTexture = Texture2D.whiteTexture;
                    _BackgroundQuadMaterial.color = new Color(0.04f, 0.08f, 0.16f, 1f);
                }
            }

            _BackgroundQuad.SetActive(true);
        }

        private void UpdateBackgroundQuadTransform()
        {
            if (_BackgroundQuad == null || Camera.main == null) return;

            float distance = 120f;
            if (Camera.main.farClipPlane > 60f)
            {
                distance = Mathf.Clamp(Camera.main.farClipPlane * 0.90f, 60f, 250f);
            }

            float fovRad = Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float frustumHeight = 2.0f * distance * Mathf.Tan(fovRad);
            float frustumWidth = frustumHeight * Camera.main.aspect;

            _BackgroundQuad.transform.localPosition = new Vector3(0f, 0f, distance);

            if (_WebCamTexture != null && _WebCamTexture.isPlaying)
            {
                int rotationAngle = _WebCamTexture.videoRotationAngle;
                float scaleY = _WebCamTexture.videoVerticallyMirrored ? -1f : 1f;

                if (rotationAngle == 90 || rotationAngle == 270)
                {
                    _BackgroundQuad.transform.localEulerAngles = new Vector3(0f, 0f, -rotationAngle);
                    _BackgroundQuad.transform.localScale = new Vector3(frustumHeight * 1.3f, frustumWidth * scaleY * 1.3f, 1f);
                }
                else
                {
                    _BackgroundQuad.transform.localEulerAngles = new Vector3(0f, 0f, -rotationAngle);
                    _BackgroundQuad.transform.localScale = new Vector3(frustumWidth * 1.3f, frustumHeight * scaleY * 1.3f, 1f);
                }
            }
            else
            {
                _BackgroundQuad.transform.localEulerAngles = Vector3.zero;
                _BackgroundQuad.transform.localScale = new Vector3(frustumWidth * 1.3f, frustumHeight * 1.3f, 1f);
            }
        }

        private void StopWebCamFeed()
        {
            if (_WebCamTexture != null)
            {
                if (_WebCamTexture.isPlaying)
                {
                    _WebCamTexture.Stop();
                }
                Destroy(_WebCamTexture);
                _WebCamTexture = null;
            }

            IsCameraStreaming = false;
            CameraStatusMessage = "";

            if (_BackgroundQuad != null)
            {
                _BackgroundQuad.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (IsARModeActive)
            {
                DeactivateARTabletop();
            }
        }

        private void OnDestroy()
        {
            CleanupARFoundation();
            StopWebCamFeed();
            if (_BackgroundQuad != null) Destroy(_BackgroundQuad);
            if (_BackgroundQuadMaterial != null) Destroy(_BackgroundQuadMaterial);
        }

        // =========================================================
        // Non-Overlapping AR Status Banner & Controls HUD
        // =========================================================

        private void OnGUI()
        {
            if (!IsARModeActive) return;

            Mobile.MobileUIHelper.BeginScaling();
            try
            {
                DrawARStatusBanner();
            }
            finally
            {
                Mobile.MobileUIHelper.EndScaling();
            }
        }

        private void DrawARStatusBanner()
        {
            float screenW = Mobile.MobileUIHelper.ScreenWidth;
            // Occupy only the safe center corridor [x >= 340 .. screenW - 210] so left HUD & right 2D Radar NEVER overlap!
            float bannerW = Mathf.Clamp(screenW - 565f, 440f, 640f);
            float bannerH = 74f;
            float x = Mathf.Max(342f, (screenW - bannerW) * 0.5f);
            float y = 10f;

            Rect bannerRect = new Rect(x, y, bannerW, bannerH);
            Mobile.MobileUIHelper.RegisterUIRect(bannerRect);

            Color bgColor = IsUsingRealARFoundation
                ? new Color(0.05f, 0.28f, 0.22f, 0.94f)
                : (IsCameraStreaming ? new Color(0.08f, 0.24f, 0.16f, 0.94f) : new Color(0.10f, 0.18f, 0.32f, 0.94f));
            GUI.backgroundColor = bgColor;
            GUI.Box(bannerRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(x + 10f, y + 6f, bannerW - 20f, bannerH - 10f));
            GUILayout.BeginVertical();

            // Row 1: Mode Status + Action Buttons (generous widths so ZERO text clipping)
            GUILayout.BeginHorizontal();
            string statusIcon;
            if (IsUsingRealARFoundation)
            {
                statusIcon = IsSurfacePlaced
                    ? $"📡 <color=#55FFAA><b>ARCORE 6-DOF ĐÃ GHIM BÀN</b></color> <color=#FDE047>({DioramaScale:F1}x)</color>"
                    : "📡 <color=#38BDF8><b>ARCORE: CHẠM MẶT BÀN ĐỂ ĐẶT SA BÀN</b></color>";
            }
            else
            {
                statusIcon = IsCameraStreaming
                    ? $"📸 <color=#55FF55><b>CAMERA AR SA BÀN</b></color> <color=#FDE047>({DioramaScale:F1}x)</color>"
                    : $"📱 <color=#00FFFF><b>HOLOGRAM AR SA BÀN</b></color> <color=#FDE047>({DioramaScale:F1}x)</color>";
            }
            GUILayout.Label($"<size=11>{statusIcon}</size>");

            GUILayout.FlexibleSpace();

            if (IsUsingRealARFoundation && IsSurfacePlaced)
            {
                GUI.backgroundColor = new Color(0.85f, 0.65f, 0.15f, 0.95f);
                if (GUILayout.Button("<b>📌 Đặt lại</b>", GUILayout.Width(76), GUILayout.Height(24)))
                {
                    RepositionARTableSurface();
                }
            }

            GUI.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 0.95f);
            if (GUILayout.Button("<b>↺</b>", GUILayout.Width(32), GUILayout.Height(24))) RotateTabletopDiorama(-30f);
            if (GUILayout.Button("<b>↻</b>", GUILayout.Width(32), GUILayout.Height(24))) RotateTabletopDiorama(30f);

            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.4f, 0.95f);
            if (GUILayout.Button("<b>➖</b>", GUILayout.Width(34), GUILayout.Height(24))) AdjustDioramaScale(-0.15f);
            if (GUILayout.Button("<b>➕</b>", GUILayout.Width(34), GUILayout.Height(24))) AdjustDioramaScale(0.15f);

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.5f, 0.95f);
            if (GUILayout.Button("<b>🎯 Làng</b>", GUILayout.Width(66), GUILayout.Height(24)))
            {
                CameraControl.Instance?.ResetToCenter();
            }

            GUI.backgroundColor = new Color(0.9f, 0.25f, 0.2f, 0.95f);
            if (GUILayout.Button("<b>✕ Thoát AR</b>", GUILayout.Width(86), GUILayout.Height(24)))
            {
                ToggleARMode();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Row 2: Context-sensitive instruction
            string helperText;
            if (IsUsingRealARFoundation)
            {
                helperText = IsSurfacePlaced
                    ? "🚶 Bước quanh mặt bàn xem 360° thực tế | ✌️ Chụm/Xoay 2 ngón chỉnh cỡ & hướng | 🎯 Chạm bàn đặt tháp"
                    : "📡 Di chuyển camera chậm quanh mặt bàn thật -> Chạm vào vùng lưới xanh để ĐẶT SA BÀN!";
            }
            else if (Mobile.MobileUIHelper.IsMobile)
            {
                helperText = "👆 Vuốt 1 ngón: Di chuyển | ✌️ 2 ngón: Thu phóng / Xoay 360° | 🎯 Chạm sa bàn để đặt quân";
            }
            else
            {
                helperText = "🖱️ Chuột Trái: Xoay 360° | Chuột Phải: Dịch chuyển | Cuộn chuột: Phóng/Thu (Bản Android hỗ trợ ARCore quét bàn 6-DOF)";
            }
            GUILayout.Label($"<size=10><color=#E5E7EB>{helperText}</color></size>");

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
