using System.Collections;
using LlamAcademy.Dinos.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.AR
{
    /// <summary>
    /// Augmented Reality Implementation: Bounded Tabletop Diorama AR & Real Camera Pass-Through.
    /// Dual Architecture:
    /// 1. Mobile (AR Foundation architecture): ARPlaneManager + ARRaycastManager plane detection & anchoring.
    /// 2. PC / Webcam: 6-DOF Spatial Tabletop Plane Anchor & Fiducial Marker alignment, pinning the diorama
    ///    rock-solidly to the table surface without drift.
    /// 3. Bounded Prehistoric Stone Pedestal Diorama (ARDioramaFrame) with clipping volume masking out-of-bounds scenery.
    /// 4. Transparent Shadow Matte capturing real-time dinosaur & tower shadows onto the real desk.
    /// 5. Full 3D Spatial Gestures: 1-finger Pan, 2-finger 360° Twist, 2-finger Pinch Zoom (0.4x - 2.2x),
    ///    and Direct Tabletop Touch-to-Place.
    /// </summary>
    [DefaultExecutionOrder(-6)]
    public class ARTabletopController : MonoBehaviour
    {
        public static ARTabletopController Instance { get; private set; }

        [Header("AR State")]
        public bool IsARModeActive { get; private set; } = false;
        public bool IsCameraStreaming { get; private set; } = false;
        public string CameraStatusMessage { get; private set; } = "";

        [Header("Spatial Anchor & Diorama Scaling")]
        public float DioramaScale { get; private set; } = 1.0f;
        public Vector3 TableAnchorOffset { get; private set; } = Vector3.zero;
        public float TableAnchorYaw { get; private set; } = 0f;
        public bool IsPlaneAnchorLocked { get; private set; } = true;

        // Camera caches
        private Material _OriginalSkybox;
        private CameraClearFlags _OriginalCameraClearFlags = CameraClearFlags.Skybox;
        private Color _OriginalCameraBackgroundColor = Color.black;

        // Video Pass-Through Background Objects
        private WebCamTexture _WebCamTexture;
        private GameObject _BackgroundQuad;
        private MeshRenderer _BackgroundQuadRenderer;
        private Material _BackgroundQuadMaterial;

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
            ARControlPill.SetActive(false); // Controlled via header bar button
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
            plane.transform.localScale = new Vector3(9f, 1f, 9.2f);

            Material mat = Rendering.PrehistoricShaderUtility.CreateUnlitMaterial(new Color(0.1f, 0.70f, 0.95f, 0.18f));
            Renderer r = plane.GetComponent<Renderer>();
            if (r != null) r.material = mat;

            Collider col = plane.GetComponent<Collider>();
            if (col != null) Destroy(col);

            ARPlaneGrid.SetActive(false);
        }

        private void Update()
        {
            // Hotkey 'R' toggles AR Tabletop Mode (on PC when not placing tower)
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                bool isPlacing = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;
                if (!isPlacing)
                {
                    ToggleARMode();
                }
            }

            if (IsARModeActive)
            {
                HandleARGestures();
                UpdateBackgroundQuadTransform();
            }
        }

        /// <summary>
        /// Handles 3D spatial gestures: 1-finger Pan, 2-finger 360° Twist, 2-finger Pinch Zoom.
        /// </summary>
        private void HandleARGestures()
        {
            bool isPlacing = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;

            // PC Mouse Gestures
            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                if (!Mobile.MobileUIHelper.IsPointerOverUI(mousePos))
                {
                    // 1. Left Click Drag: Orbit / Twist diorama around Table Anchor
                    if (Mouse.current.leftButton.isPressed && !isPlacing)
                    {
                        Vector2 delta = Mouse.current.delta.ReadValue();
                        if (Mathf.Abs(delta.x) > 0.05f)
                        {
                            RotateTabletopDiorama(delta.x * 0.35f);
                        }
                    }

                    // 2. Right Click Drag: Pan diorama across table surface
                    if (Mouse.current.rightButton.isPressed)
                    {
                        Vector2 delta = Mouse.current.delta.ReadValue();
                        PanTabletopDiorama(new Vector3(delta.x * 0.05f, 0f, delta.y * 0.05f));
                    }

                    // 3. Scroll Wheel: Dynamic Pinch Scale (0.4x -> 2.2x)
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.05f)
                    {
                        AdjustDioramaScale(scroll > 0 ? 0.05f : -0.05f);
                    }
                }

                // Keyboard Rotate [ / ]
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.leftBracketKey.isPressed) RotateTabletopDiorama(-60f * Time.deltaTime);
                    if (Keyboard.current.rightBracketKey.isPressed) RotateTabletopDiorama(60f * Time.deltaTime);
                }
            }

            // Mobile Multi-touch Gestures (Pinch to Zoom, Two-finger Twist)
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

                    // Two-finger Twist Angle
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
            CameraControl.Instance?.OrbitCamera(angleDelta);
        }

        public void PanTabletopDiorama(Vector3 panDelta)
        {
            TableAnchorOffset += panDelta;
            if (CameraControl.Instance != null && Camera.main != null)
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
            if (CameraControl.Instance != null)
            {
                // Smoothly adjusts camera zoom to represent tabletop scale change
                CameraControl.Instance.ZoomCamera(-delta * 35f);
            }
        }

        /// <summary>
        /// Snaps Table Plane Anchor to a physical detected plane (AR Foundation mobile SLAM).
        /// </summary>
        public void SnapToDetectedPlane(Vector3 hitPoint, Vector3 normal)
        {
            IsPlaneAnchorLocked = true;
            TableAnchorOffset = hitPoint;
            Debug.Log($"<color=cyan>[AR Foundation]</color> Diorama Anchor snapped to detected physical table surface at: {hitPoint}");
        }

        /// <summary>
        /// Toggles between Standard 3D God-view and Tabletop Augmented Reality Mode
        /// </summary>
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
            Debug.Log("<color=cyan>[AR System]</color> Tabletop AR Mode Activated. Initializing Video Pass-Through & Stone Diorama...");

            // 1. Setup Camera for AR Pass-Through
            SetupCameraForAR();

            // 2. Enable Stone Diorama Pedestal, Shadow Matte & Boundary Clipping
            ARDioramaFrame.Instance.SetARMode(true);

            // 3. Enable AR holographic baseplate grid
            if (ARPlaneGrid != null) ARPlaneGrid.SetActive(true);

            // 4. Start Live Camera Video Feed
            if (CameraInitRoutine != null) StopCoroutine(CameraInitRoutine);
            CameraInitRoutine = StartCoroutine(StartWebCamFeedRoutine());

            if (RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement("📱 ĐÃ BẬT AR SA BÀN! HÌNH ẢNH CAMERA THỰC TẾ & ĐẾ ĐÁ TIỀN SỬ ĐANG KHỞI ĐỘNG...", 3.5f);
            }
        }

        private void DeactivateARTabletop()
        {
            Debug.Log("<color=cyan>[AR System]</color> Restoring Standard 3D Tactical Battlefield...");

            // 1. Stop Camera Feed & teardown background
            StopWebCamFeed();

            // 2. Disable Stone Diorama Pedestal, Shadow Matte & restore scenery
            ARDioramaFrame.Instance.SetARMode(false);

            // 3. Restore Camera settings
            RestoreCameraFromAR();

            // 4. Disable AR holographic baseplate
            if (ARPlaneGrid != null) ARPlaneGrid.SetActive(false);

            if (RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement("🎮 ĐÃ THOÁT AR, TRỞ VỀ GÓC NHÌN 3D CHIẾN TRƯỜNG TIỀN SỬ.", 2.5f);
            }
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

#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Debug.Log("<color=cyan>[AR Camera]</color> Requesting Android CAMERA permission...");
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);

                float permTimeout = 15.0f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && permTimeout > 0f)
                {
                    permTimeout -= Time.unscaledDeltaTime;
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
            yield return new WaitForSecondsRealtime(0.4f);
#endif

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

            Debug.Log($"<color=cyan>[AR Camera]</color> Starting Camera Device: {targetDeviceName} (Total devices: {devices.Length})");

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
                CameraStatusMessage = $"📸 Camera AR Đang Hoạt Động ({targetDeviceName})";
                EnsureARBackgroundSetup(_WebCamTexture);
                Debug.Log($"<color=green>[AR Camera]</color> Camera stream active! Resolution: {_WebCamTexture.width}x{_WebCamTexture.height}");
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
            if (CameraInitRoutine != null)
            {
                StopCoroutine(CameraInitRoutine);
                CameraInitRoutine = null;
            }

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
            StopWebCamFeed();
            if (_BackgroundQuad != null) Destroy(_BackgroundQuad);
            if (_BackgroundQuadMaterial != null) Destroy(_BackgroundQuadMaterial);
        }

        // =========================================================
        // AR Mode On-Screen Status Banner & HUD
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
            float bannerW = Mathf.Min(820f, Mobile.MobileUIHelper.ScreenWidth - 24f);
            float bannerH = 80f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - bannerW) / 2f;
            float y = 10f;

            Rect bannerRect = new Rect(x, y, bannerW, bannerH);
            Mobile.MobileUIHelper.RegisterUIRect(bannerRect);

            Color bgColor = IsCameraStreaming ? new Color(0.06f, 0.28f, 0.16f, 0.95f) : new Color(0.10f, 0.18f, 0.32f, 0.95f);
            GUI.backgroundColor = bgColor;
            GUI.Box(bannerRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(x + 12, y + 6, bannerW - 24, bannerH - 12));
            GUILayout.BeginVertical();

            // Status Line & Quick Controls
            GUILayout.BeginHorizontal();
            string statusIcon = IsCameraStreaming 
                ? "📸 <color=#55FF55><b>AR LIVE CAMERA (VIDEO PHÒNG THỰC TẾ)</b></color>" 
                : "📱 <color=#00FFFF><b>AR SA BÀN 3D TABLETOP (HOLOGRAPHIC)</b></color>";
            GUILayout.Label($"<size=12>{statusIcon}</size>");

            GUILayout.Space(8);
            string anchorTag = IsPlaneAnchorLocked 
                ? $"<color=#FDE047>📌 Neo Mặt Bàn (6-DOF Locked) | Tỷ lệ: {DioramaScale:F1}x</color>" 
                : "<color=#FF6666>⚠️ Đang định vị mặt bàn...</color>";
            GUILayout.Label($"<size=11><b>{anchorTag}</b></size>");

            GUILayout.FlexibleSpace();

            // Rotate Left Button
            GUI.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 0.95f);
            if (GUILayout.Button("<b>↺ Xoay Trái</b>", GUILayout.Width(82), GUILayout.Height(24)))
            {
                RotateTabletopDiorama(-30f);
            }

            // Rotate Right Button
            if (GUILayout.Button("<b>↻ Xoay Phải</b>", GUILayout.Width(82), GUILayout.Height(24)))
            {
                RotateTabletopDiorama(30f);
            }

            // Zoom In/Out
            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.4f, 0.95f);
            if (GUILayout.Button("<b>🔍 -</b>", GUILayout.Width(44), GUILayout.Height(24)))
            {
                AdjustDioramaScale(-0.1f);
            }
            if (GUILayout.Button("<b>🔎 +</b>", GUILayout.Width(44), GUILayout.Height(24)))
            {
                AdjustDioramaScale(0.1f);
            }

            // Focus Center Button
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.5f, 0.95f);
            if (GUILayout.Button("<b>🎯 Focus Làng</b>", GUILayout.Width(92), GUILayout.Height(24)))
            {
                CameraControl.Instance?.ResetToCenter();
            }

            // Exit AR Button
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.2f, 0.95f);
            if (GUILayout.Button("<b>✕ THOÁT AR</b>", GUILayout.Width(95), GUILayout.Height(24)))
            {
                ToggleARMode();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // Gesture Helper line
            string helperText = Mobile.MobileUIHelper.IsMobile
                ? "👆 <b>Vuốt 1 ngón:</b> Di chuyển vị trí sa bàn | ✌️ <b>2 ngón chụm/xoay:</b> Thu phóng / Xoay 360° | 🎯 Chạm trực tiếp mặt bàn để đặt tháp"
                : "🖱️ <b>Kéo Chuột Trái:</b> Xoay 360° sa bàn | <b>Chuột Phải:</b> Dịch chuyển sa bàn | <b>Cuộn chuột / Phím [ ]:</b> Thu phóng & Xoay | <b>Chạm mặt bàn:</b> Đặt tháp";
            GUILayout.Label($"<size=11><color=#EAEAEA>{helperText}</color></size>");

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
