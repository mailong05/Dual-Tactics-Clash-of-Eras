using System.Collections;
using LlamAcademy.Dinos.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.AR
{
    /// <summary>
    /// Augmented Reality Implementation (Tabletop AR & Real Camera Pass-Through Prehistoric Diorama).
    /// Supports:
    /// 1. Real Camera Pass-Through on Android (Rear Camera) and PC (Webcam) via WebCamTexture.
    /// 2. Overlays the full 3D Jurassic battlefield (village, walls, towers, dinosaurs) on top of the real world surface (desk/floor).
    /// 3. Zero-depth background quad with RenderQueue 1000 ensures 100% of 3D models render seamlessly over the camera feed.
    /// 4. Full multi-touch orbit, pinch-zoom, and pan via CameraControl and MobileTouchController.
    /// 5. Graceful fallback to Cyber-Hologram Tabletop if camera is unavailable or permission denied.
    /// </summary>
    [DefaultExecutionOrder(-6)]
    public class ARTabletopController : MonoBehaviour
    {
        public static ARTabletopController Instance { get; private set; }

        [Header("AR State")]
        public bool IsARModeActive { get; private set; } = false;
        public bool IsCameraStreaming { get; private set; } = false;
        public string CameraStatusMessage { get; private set; } = "";

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
            ARControlPill.SetActive(false); // Ẩn uGUI pill để dùng nút bấm đồng bộ trong thanh điều khiển góc phải
            ARControlPill.transform.SetParent(canvas.transform, false);

            RectTransform rt = ARControlPill.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -68f);
            rt.sizeDelta = new Vector2(210f, 42f);

            Image img = ARControlPill.AddComponent<Image>();
            img.color = new Color(0.10f, 0.42f, 0.90f, 0.92f); // AR Cyan-Blue

            Outline outl = ARControlPill.AddComponent<Outline>();
            outl.effectColor = new Color(0.4f, 0.85f, 1.0f, 0.9f);
            outl.effectDistance = new Vector2(2f, 2f);

            Button btn = ARControlPill.AddComponent<Button>();
            btn.onClick.AddListener(ToggleARMode);

            GameObject textGO = new GameObject("Label");
            textGO.transform.SetParent(ARControlPill.transform, false);
            ARButtonText = textGO.AddComponent<Text>();
            ARButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ARButtonText.text = "📱 <b>CHẾ ĐỘ AR (CAMERA)</b>";
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
            // Procedural AR holographic tracking grid plate under the village & battlefield
            ARPlaneGrid = new GameObject("AR_Tracking_Grid_Plane");
            ARPlaneGrid.transform.position = new Vector3(0f, -0.05f, -10f);

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "AR_Grid_Mesh";
            plane.transform.SetParent(ARPlaneGrid.transform, false);
            plane.transform.localScale = new Vector3(6f, 1f, 8f);

            // Translucent holographic cyan material
            Material mat = Rendering.PrehistoricShaderUtility.CreateUnlitMaterial(new Color(0.1f, 0.65f, 0.95f, 0.22f));
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

            // In AR Mode, update background quad alignment and handle gestures
            if (IsARModeActive)
            {
                HandleARGestures();
                UpdateBackgroundQuadTransform();
            }
        }

        private void HandleARGestures()
        {
            // PC Drag & Orbit
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed && !Mobile.MobileUIHelper.IsPointerOverUI(Mouse.current.position.ReadValue()))
                {
                    bool isPlacing = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;
                    if (!isPlacing)
                    {
                        Vector2 delta = Mouse.current.delta.ReadValue();
                        if (Mathf.Abs(delta.x) > 0.05f)
                        {
                            CameraControl.Instance?.OrbitCamera(delta.x * 0.40f);
                        }
                    }
                }

                // Keyboard Rotate [ / ]
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.leftBracketKey.isPressed)
                    {
                        CameraControl.Instance?.OrbitCamera(-55f * Time.deltaTime);
                    }
                    if (Keyboard.current.rightBracketKey.isPressed)
                    {
                        CameraControl.Instance?.OrbitCamera(55f * Time.deltaTime);
                    }
                }
            }
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
                ARButtonText.text = IsARModeActive ? "🔙 <b>THOÁT AR (VỀ 3D)</b>" : "📱 <b>CHẾ ĐỘ AR (CAMERA)</b>";
            }
        }

        private void ActivateARTabletop()
        {
            Debug.Log("<color=cyan>[AR System]</color> Tabletop AR Mode Activated. Initializing Video Pass-Through & Diorama...");

            // 1. Setup Camera for AR Pass-Through
            SetupCameraForAR();

            // 2. Enable AR holographic baseplate grid
            if (ARPlaneGrid != null) ARPlaneGrid.SetActive(true);

            // 3. Start Live Camera Video Feed
            if (CameraInitRoutine != null) StopCoroutine(CameraInitRoutine);
            CameraInitRoutine = StartCoroutine(StartWebCamFeedRoutine());

            if (RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement("📱 ĐÃ BẬT AR SA BÀN! HÌNH ẢNH CAMERA THỰC TẾ ĐANG KHỞI ĐỘNG...", 3.5f);
            }
        }

        private void DeactivateARTabletop()
        {
            Debug.Log("<color=cyan>[AR System]</color> Restoring Standard 3D Tactical Battlefield...");

            // 1. Stop Camera Feed & teardown background
            StopWebCamFeed();

            // 2. Restore Camera settings
            RestoreCameraFromAR();

            // 3. Disable AR holographic baseplate
            if (ARPlaneGrid != null) ARPlaneGrid.SetActive(false);

            if (RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement("🎮 ĐÃ THOÁT AR, TRỞ VỀ GÓC NHÌN 3D CHIẾN TRƯỜNG TIỀN SỬ.", 2.5f);
            }
        }

        private void SetupCameraForAR()
        {
            if (Camera.main == null) return;

            // Cache original skybox & clear settings
            _OriginalSkybox = RenderSettings.skybox;
            RenderSettings.skybox = null; // Temporarily disable Skybox so URP doesn't draw sky over the camera quad!

            _OriginalCameraClearFlags = Camera.main.clearFlags;
            _OriginalCameraBackgroundColor = Camera.main.backgroundColor;

            // Use SolidColor clear with zero alpha so camera pass-through quad displays cleanly behind all 3D geometry
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0f, 0f, 0f, 0f);

            // Focus camera directly onto the prehistoric village center for an instant tabletop diorama view
            CameraControl.Instance?.ResetToCenter();
        }

        private void RestoreCameraFromAR()
        {
            // Restore Skybox
            if (_OriginalSkybox != null)
            {
                RenderSettings.skybox = _OriginalSkybox;
            }

            // Restore Camera clear settings
            if (Camera.main != null)
            {
                Camera.main.clearFlags = _OriginalCameraClearFlags;
                Camera.main.backgroundColor = _OriginalCameraBackgroundColor;
            }
        }

        // =========================================================
        // Real-Time Camera Feed (WebCamTexture) Pass-Through System
        // =========================================================

        private IEnumerator StartWebCamFeedRoutine()
        {
            CameraStatusMessage = "Đang xin quyền và dò tìm Camera...";

#if UNITY_ANDROID
            // 1. Kiểm tra và xin quyền Camera trên Android
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Debug.Log("<color=cyan>[AR Camera]</color> Requesting Android CAMERA permission...");
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);

                // Chờ người dùng nhấn "Cho phép" trên hộp thoại cấp quyền hệ thống (tối đa 15 giây)
                float permTimeout = 15.0f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && permTimeout > 0f)
                {
                    permTimeout -= Time.unscaledDeltaTime;
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }

            // Chờ một nhịp nhỏ để Android Camera Service sẵn sàng sau khi cấp quyền
            yield return new WaitForSecondsRealtime(0.4f);
#endif

            // 2. Quét danh sách thiết bị Camera
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogWarning("[AR Camera] No camera devices found. Using Cyber Tabletop Hologram mode.");
                CameraStatusMessage = "Không tìm thấy Camera phần cứng. Đang dùng Sa Bàn 3D Hologram.";
                IsCameraStreaming = false;
                EnsureARBackgroundSetup(null);
                yield break;
            }

            // 3. Ưu tiên tìm camera sau (rear camera) trên điện thoại, hoặc webcam đầu tiên trên PC
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

            // 4. Chờ luồng camera phát và nhận được kích thước khung hình hợp lệ
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

            // Tạo Background Quad gắn trực tiếp vào Camera.main
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

                // QUAN TRỌNG NHẤT: RenderQueue = 1000 (Background), ZWrite = 0 (tắt ghi depth), ZTest = Always
                // Giúp tấm nền video vẽ ĐẦU TIÊN và KHÔNG BAO GIỜ đè/che khuất bất kỳ mô hình 3D nào!
                _BackgroundQuadMaterial.renderQueue = 1000;
                _BackgroundQuadMaterial.SetInt("_ZWrite", 0);
                _BackgroundQuadMaterial.DisableKeyword("_ZWRITE_ON");
                _BackgroundQuadMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

                if (_BackgroundQuadMaterial.HasProperty("_Surface"))
                {
                    _BackgroundQuadMaterial.SetFloat("_Surface", 1); // Transparent/Background pass
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
                    // Sa bàn Hologram phong cách công nghệ cao khi không có camera
                    _BackgroundQuadMaterial.mainTexture = Texture2D.whiteTexture;
                    _BackgroundQuadMaterial.color = new Color(0.04f, 0.08f, 0.16f, 1f);
                }
            }

            _BackgroundQuad.SetActive(true);
        }

        private void UpdateBackgroundQuadTransform()
        {
            if (_BackgroundQuad == null || Camera.main == null) return;

            // Đặt Quad ở khoảng cách 120m phía sau chiến trường (chiến trường nằm trong khoảng 25m - 85m)
            // Nhờ ZWrite = 0 và ZTest = Always, Quad đóng vai trò bầu trời nền thực tế không bao giờ che khuất mô hình 3D
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

                // Trên điện thoại, cảm biến camera thường là Portrait (góc 90 hoặc 270)
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
            float bannerW = Mathf.Min(780f, Mobile.MobileUIHelper.ScreenWidth - 30f);
            float bannerH = 74f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - bannerW) / 2f;
            float y = 12f;

            Rect bannerRect = new Rect(x, y, bannerW, bannerH);
            Mobile.MobileUIHelper.RegisterUIRect(bannerRect);

            Color bgColor = IsCameraStreaming ? new Color(0.08f, 0.35f, 0.18f, 0.94f) : new Color(0.12f, 0.22f, 0.38f, 0.94f);
            GUI.backgroundColor = bgColor;
            GUI.Box(bannerRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(x + 12, y + 6, bannerW - 24, bannerH - 12));
            GUILayout.BeginVertical();

            // Status Line & Quick Controls
            GUILayout.BeginHorizontal();
            string statusIcon = IsCameraStreaming 
                ? "📸 <color=#55FF55><b>AR LIVE CAMERA (VIDEO PHÒNG THỰC TẾ)</b></color>" 
                : "📱 <color=#00FFFF><b>AR SA BÀN 3D TABLETOP (HOLOGRAPHIC)</b></color>";
            GUILayout.Label($"<size=13>{statusIcon}</size>");
            GUILayout.FlexibleSpace();

            // Rotate Left Button
            GUI.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 0.95f);
            if (GUILayout.Button("<b>↺ Xoay Trái</b>", GUILayout.Width(90), GUILayout.Height(26)))
            {
                CameraControl.Instance?.OrbitCamera(-30f);
            }

            // Rotate Right Button
            if (GUILayout.Button("<b>↻ Xoay Phải</b>", GUILayout.Width(90), GUILayout.Height(26)))
            {
                CameraControl.Instance?.OrbitCamera(30f);
            }

            // Focus Center Button
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.5f, 0.95f);
            if (GUILayout.Button("<b>🎯 Focus Làng</b>", GUILayout.Width(100), GUILayout.Height(26)))
            {
                CameraControl.Instance?.ResetToCenter();
            }

            // Exit AR Button
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.2f, 0.95f);
            if (GUILayout.Button("<b>✕ THOÁT AR</b>", GUILayout.Width(105), GUILayout.Height(26)))
            {
                ToggleARMode();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // Gesture Helper line
            string helperText = Mobile.MobileUIHelper.IsMobile
                ? "👆 <b>Vuốt 1 ngón:</b> Di chuyển xem toàn cảnh sa bàn | ✌️ <b>2 ngón chụm/xoay:</b> Thu phóng / Xoay góc nhìn | 🎯 Đặt tháp & điều quân bình thường"
                : "🖱️ <b>Kéo Chuột Trái:</b> Xoay quanh sa bàn | <b>Cuộn chuột / Phím [ ]:</b> Thu phóng & Xoay | <b>Phím F:</b> Về giữa làng | <b>Phím R:</b> Thoát AR";
            GUILayout.Label($"<size=11><color=#EAEAEA>{helperText}</color></size>");

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
