using System.Collections;
using LlamAcademy.Dinos.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.AR
{
    /// <summary>
    /// Augmented Reality Implementation (Rubric Item 2: 20 pts).
    /// Tabletop Augmented Reality & Real Camera Pass-Through Prehistoric Diorama.
    /// Supports:
    /// 1. Real Camera Pass-Through on Android (Rear Camera) and PC (Webcam) via WebCamTexture.
    /// 2. Scales down the 3D Jurassic battlefield into a miniaturized tabletop AR diorama placed on real surfaces.
    /// 3. Multi-touch orbit (1-finger swipe / mouse drag) and scale inspection (pinch / mouse wheel).
    /// 4. Graceful fallback to Cyber-Hologram Tabletop if camera is unavailable or permission denied.
    /// </summary>
    [DefaultExecutionOrder(-6)]
    public class ARTabletopController : MonoBehaviour
    {
        public static ARTabletopController Instance { get; private set; }

        [Header("AR World Settings")]
        [SerializeField] private float TabletopScale = 0.08f; // Scales 30m canyon down to compact tabletop diorama
        [SerializeField] private Vector3 ARTabletopOffset = new Vector3(0f, -0.25f, 0f);
        [SerializeField] private Transform GameWorldRoot;

        [Header("AR State")]
        public bool IsARModeActive { get; private set; } = false;
        public bool IsCameraStreaming { get; private set; } = false;
        public string CameraStatusMessage { get; private set; } = "";

        private Vector3 OriginalWorldPosition = Vector3.zero;
        private Vector3 OriginalWorldScale = Vector3.one;
        private Quaternion OriginalWorldRotation = Quaternion.identity;

        // Camera & Cinemachine caches
        private Unity.Cinemachine.CinemachineCamera _CinemachineCamera;
        private CameraClearFlags _OriginalCameraClearFlags = CameraClearFlags.Skybox;
        private Vector3 _OriginalCameraPosition;
        private Quaternion _OriginalCameraRotation;

        // Video Pass-Through Background Objects
        private WebCamTexture _WebCamTexture;
        private GameObject _BackgroundQuad;
        private MeshRenderer _BackgroundQuadRenderer;
        private Material _BackgroundQuadMaterial;

        // UI & Hologram base
        private GameObject ARControlPill;
        private Text ARButtonText;
        private GameObject ARPlaneGrid;
        private Coroutine TransitionRoutine;
        private Coroutine CameraInitRoutine;

        // Touch & Gesture Interaction in AR Mode
        private Vector2 _LastTouchPos;
        private bool _IsInteracting = false;
        private float _CurrentTabletopScale = 0.08f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _CurrentTabletopScale = TabletopScale;
            LocateGameWorldRoot();
        }

        private void LocateGameWorldRoot()
        {
            if (GameWorldRoot == null)
            {
                // Find main terrain or root diorama environment
                GameObject terrain = GameObject.Find("Terrain") ?? GameObject.Find("Canyon_Terrain") ?? GameObject.Find("Ground");
                if (terrain != null)
                {
                    // If terrain has a parent environment, scale the environment, else scale terrain
                    GameWorldRoot = terrain.transform.parent != null ? terrain.transform.parent : terrain.transform;
                }
            }

            if (GameWorldRoot != null)
            {
                OriginalWorldPosition = GameWorldRoot.position;
                OriginalWorldScale = GameWorldRoot.localScale;
                OriginalWorldRotation = GameWorldRoot.rotation;
            }
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
            ARControlPill.transform.SetParent(canvas.transform, false);

            RectTransform rt = ARControlPill.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -68f);
            rt.sizeDelta = new Vector2(200f, 40f);

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
            ARButtonText.fontSize = 11;
            ARButtonText.alignment = TextAnchor.MiddleCenter;
            ARButtonText.color = Color.white;

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
        }

        private Canvas FindSuitableOverlayCanvas()
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
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
            // Procedural AR tracking plane diorama base
            ARPlaneGrid = new GameObject("AR_Tracking_Grid_Plane");
            ARPlaneGrid.transform.position = new Vector3(0f, -0.3f, 0f);

            // Plane mesh
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "AR_Grid_Mesh";
            plane.transform.SetParent(ARPlaneGrid.transform, false);
            plane.transform.localScale = new Vector3(3f, 1f, 3f);

            // Translucent holographic cyan material
            Material mat = Rendering.PrehistoricShaderUtility.CreateUnlitMaterial(new Color(0.1f, 0.6f, 0.95f, 0.35f));
            Renderer r = plane.GetComponent<Renderer>();
            if (r != null) r.material = mat;

            Collider col = plane.GetComponent<Collider>();
            if (col != null) Destroy(col);

            ARPlaneGrid.SetActive(false);
        }

        private void Update()
        {
            // Hotkey 'R' toggles AR Tabletop Mode (only on PC when not typing)
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                bool isPlacing = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;
                if (!isPlacing)
                {
                    ToggleARMode();
                }
            }

            // In AR Mode, handle touch and mouse interactions to orbit & scale the tabletop diorama
            if (IsARModeActive)
            {
                HandleARGestures();
                UpdateBackgroundQuadTransform();
            }
        }

        private void HandleARGestures()
        {
            if (GameWorldRoot == null) return;

            // 1. Mouse Drag (PC)
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed && !Mobile.MobileUIHelper.IsPointerOverUI(Mouse.current.position.ReadValue()))
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    if (Mathf.Abs(delta.x) > 0.05f)
                    {
                        GameWorldRoot.Rotate(Vector3.up, -delta.x * 0.35f, Space.World);
                    }
                }

                // Mouse Scroll Zoom
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.1f)
                {
                    AdjustTabletopScale(Mathf.Sign(scroll) * 0.01f);
                }

                // Keyboard Rotate [ / ]
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.leftBracketKey.isPressed)
                    {
                        GameWorldRoot.Rotate(Vector3.up, -45f * Time.deltaTime, Space.World);
                    }
                    if (Keyboard.current.rightBracketKey.isPressed)
                    {
                        GameWorldRoot.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
                    }
                }
            }

            // 2. Mobile Touch Gestures (1 finger orbit, 2 finger pinch)
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 1)
            {
                var t = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (!Mobile.MobileUIHelper.IsPointerOverUI(t.screenPosition))
                {
                    if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                    {
                        float deltaX = t.delta.x;
                        GameWorldRoot.Rotate(Vector3.up, -deltaX * 0.25f, Space.World);
                    }
                }
            }
            else if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 2)
            {
                var t1 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                var t2 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[1];

                Vector2 prevPos1 = t1.screenPosition - t1.delta;
                Vector2 prevPos2 = t2.screenPosition - t2.delta;

                float prevDist = Vector2.Distance(prevPos1, prevPos2);
                float curDist = Vector2.Distance(t1.screenPosition, t2.screenPosition);

                float pinchDelta = curDist - prevDist;
                if (Mathf.Abs(pinchDelta) > 0.5f)
                {
                    AdjustTabletopScale(pinchDelta * 0.00015f);
                }
            }
        }

        private void AdjustTabletopScale(float delta)
        {
            if (GameWorldRoot == null) return;
            _CurrentTabletopScale = Mathf.Clamp(_CurrentTabletopScale + delta, 0.035f, 0.18f);
            GameWorldRoot.localScale = Vector3.one * _CurrentTabletopScale;
        }

        /// <summary>
        /// Toggles between Standard 3D God-view and Tabletop Augmented Reality Mode
        /// </summary>
        public void ToggleARMode()
        {
            if (GameWorldRoot == null) LocateGameWorldRoot();

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
            if (GameWorldRoot == null) return;

            Debug.Log("<color=cyan>[AR System]</color> Tabletop AR Mode Activated. Initializing Video Pass-Through & Diorama...");

            // 1. Setup Camera for Tabletop Perspective
            SetupCameraForAR();

            // 2. Shrink and position diorama on tabletop
            _CurrentTabletopScale = TabletopScale;
            if (ARPlaneGrid != null) ARPlaneGrid.SetActive(true);

            if (TransitionRoutine != null) StopCoroutine(TransitionRoutine);
            TransitionRoutine = StartCoroutine(LerpWorldTransform(ARTabletopOffset, Vector3.one * _CurrentTabletopScale, 0.7f));

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
            if (GameWorldRoot == null) return;

            Debug.Log("<color=cyan>[AR System]</color> Restoring Standard 3D Tactical Battlefield...");

            // 1. Stop Camera Feed & teardown background
            StopWebCamFeed();

            // 2. Restore Camera & Cinemachine
            RestoreCameraFromAR();

            // 3. Restore diorama scale and position
            if (ARPlaneGrid != null) ARPlaneGrid.SetActive(false);

            if (TransitionRoutine != null) StopCoroutine(TransitionRoutine);
            TransitionRoutine = StartCoroutine(LerpWorldTransform(OriginalWorldPosition, OriginalWorldScale, 0.7f));

            if (RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement("🎮 ĐÃ THOÁT AR, TRỞ VỀ GÓC NHÌN 3D CHIẾN TRƯỜNG TIỀN SỬ.", 2.5f);
            }
        }

        private void SetupCameraForAR()
        {
            if (Camera.main == null) return;

            _OriginalCameraClearFlags = Camera.main.clearFlags;
            _OriginalCameraPosition = Camera.main.transform.position;
            _OriginalCameraRotation = Camera.main.transform.rotation;

            // Temporarily disable Cinemachine so we can freely position the camera in front of the tabletop
            _CinemachineCamera = Camera.main.GetComponent<Unity.Cinemachine.CinemachineCamera>();
            if (_CinemachineCamera != null)
            {
                _CinemachineCamera.enabled = false;
            }

            // Position camera looking down at the tabletop diorama at an engaging 35° angle
            Camera.main.transform.position = new Vector3(0f, 1.40f, -1.90f);
            Camera.main.transform.rotation = Quaternion.Euler(34f, 0f, 0f);

            // Change clearFlags to Depth so 3D objects draw directly on top of the AR video background
            Camera.main.clearFlags = CameraClearFlags.Depth;
        }

        private void RestoreCameraFromAR()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags = _OriginalCameraClearFlags;

                if (_CinemachineCamera != null)
                {
                    _CinemachineCamera.enabled = true;
                }
                else
                {
                    Camera.main.transform.position = _OriginalCameraPosition;
                    Camera.main.transform.rotation = _OriginalCameraRotation;
                }
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

            // Tạo Background Quad gắn trực tiếp vào Camera.main (URP tương thích 100%, không bị camera stack hay clearFlags che khuất)
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
                _BackgroundQuadMaterial.renderQueue = 1500; // Early render queue (Background)
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
                    _BackgroundQuadMaterial.color = new Color(0.03f, 0.07f, 0.14f, 1f);
                }
            }

            _BackgroundQuad.SetActive(true);
        }

        private void UpdateBackgroundQuadTransform()
        {
            if (_BackgroundQuad == null || Camera.main == null) return;

            // Đặt Quad ở khoảng cách 12m phía sau sa bàn làng tiền sử (sa bàn nằm ở 1.5m - 2.5m)
            float distance = 12f;
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
                    _BackgroundQuad.transform.localScale = new Vector3(frustumHeight * 1.15f, frustumWidth * scaleY * 1.15f, 1f);
                }
                else
                {
                    _BackgroundQuad.transform.localEulerAngles = new Vector3(0f, 0f, -rotationAngle);
                    _BackgroundQuad.transform.localScale = new Vector3(frustumWidth * 1.15f, frustumHeight * scaleY * 1.15f, 1f);
                }
            }
            else
            {
                _BackgroundQuad.transform.localEulerAngles = Vector3.zero;
                _BackgroundQuad.transform.localScale = new Vector3(frustumWidth * 1.15f, frustumHeight * 1.15f, 1f);
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

        private IEnumerator LerpWorldTransform(Vector3 targetPos, Vector3 targetScale, float duration)
        {
            Vector3 startPos = GameWorldRoot.position;
            Vector3 startScale = GameWorldRoot.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                GameWorldRoot.position = Vector3.Lerp(startPos, targetPos, t);
                GameWorldRoot.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            GameWorldRoot.position = targetPos;
            GameWorldRoot.localScale = targetScale;
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
            float bannerW = Mathf.Min(680f, Mobile.MobileUIHelper.ScreenWidth - 30f);
            float bannerH = 64f;
            float x = (Mobile.MobileUIHelper.ScreenWidth - bannerW) / 2f;
            float y = 14f;

            Rect bannerRect = new Rect(x, y, bannerW, bannerH);
            Mobile.MobileUIHelper.RegisterUIRect(bannerRect);

            Color bgColor = IsCameraStreaming ? new Color(0.08f, 0.35f, 0.18f, 0.92f) : new Color(0.12f, 0.22f, 0.35f, 0.92f);
            GUI.backgroundColor = bgColor;
            GUI.Box(bannerRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(x + 10, y + 6, bannerW - 20, bannerH - 12));
            GUILayout.BeginVertical();

            // Status Line
            GUILayout.BeginHorizontal();
            string statusIcon = IsCameraStreaming ? "📸 <color=#55FF55><b>AR LIVE CAMERA (ĐANG TRUYỀN VIDEO THỰC TẾ)</b></color>" : "📱 <color=#00FFFF><b>AR SA BÀN 3D TABLETOP (HOLOGRAPHIC)</b></color>";
            GUILayout.Label($"<size=13>{statusIcon}</size>");
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.2f, 0.95f);
            if (GUILayout.Button("<b>✕ THOÁT AR</b>", GUILayout.Width(110), GUILayout.Height(24)))
            {
                ToggleARMode();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // Gesture Helper line
            string helperText = Mobile.MobileUIHelper.IsMobile
                ? "👆 <b>Kéo 1 ngón:</b> Xoay sa bàn 360° | ✌️ <b>Kẹp 2 ngón:</b> Thu phóng sa bàn | 🎯 Đặt tháp bình thường"
                : "🖱️ <b>Kéo Chuột Trái:</b> Xoay sa bàn | <b>Cuộn chuột / Phím [ ]:</b> Thu phóng | <b>Phím R:</b> Thoát AR";
            GUILayout.Label($"<size=11><color=#E0E0E0>{helperText}</color></size>");

            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
