using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.Player
{
    public enum CameraPerspective
    {
        Front_DinoAssault,  // Camera trước làng: nhìn từ trước vào làng (chế độ Khủng Long Công Thành)
        Rear_TowerDefense   // Camera sau làng: nhìn từ sau làng ra chiến trường (chế độ Thủ Thành)
    }

    [RequireComponent(typeof(CinemachineCamera))]
    public class CameraControl : MonoBehaviour
    {
        public static CameraControl Instance { get; private set; }

        [Header("Dual Camera Perspectives")]
        [SerializeField] private CameraPerspective _CurrentPerspective = CameraPerspective.Rear_TowerDefense;
        public CameraPerspective CurrentPerspective => _CurrentPerspective;

        // Base offsets for each perspective
        private readonly Vector3 _RearBaseOffset = new Vector3(0f, 35f, -26f);
        private readonly Vector3 _FrontBaseOffset = new Vector3(0f, 26f, 22f);

        [Header("Keyboard Movement")]
        [SerializeField] [Range(5f, 50f)] private float KeyboardSpeed = 16f;
        [SerializeField] [Range(1f, 5f)] private float ShiftMultiplier = 2.2f;

        [Header("Mouse Drag (Pan Map)")]
        [SerializeField] private bool EnableMouseDrag = true;
        [SerializeField] [Range(0.01f, 0.2f)] private float MouseDragSensitivity = 0.045f;
        [SerializeField] private bool InvertMouseDrag = true; // Grab style: dragging cursor moves terrain with cursor

        [Header("Mouse Zoom")]
        [SerializeField] private bool EnableZoom = true;
        [SerializeField] [Range(0.3f, 1.0f)] private float MinZoom = 0.45f;
        [SerializeField] [Range(1.0f, 2.5f)] private float MaxZoom = 1.7f;
        [SerializeField] [Range(0.02f, 0.3f)] private float ZoomSensitivity = 0.1f;
        [SerializeField] [Range(2f, 20f)] private float ZoomSmoothSpeed = 10f;
        private float _CurrentZoom = 1.0f;
        private float _TargetZoom = 1.0f;

        [Header("Camera Orbit / Rotation")]
        [SerializeField] private bool EnableOrbit = true;
        [SerializeField] [Range(20f, 150f)] private float OrbitSpeed = 75f;
        private float _OrbitAngleY = 0f;

        [Header("Edge Scroll")]
        [SerializeField] private bool EnableEdgeScroll = true;
        [SerializeField] private float EdgeScrollWidth = 25f;
        [SerializeField] private float EdgeScrollSpeed = 16f;

        [Header("Bounds & Restraints")]
        [SerializeField] private BoxCollider WorldBounds;
        [SerializeField] private Vector2 FallbackMin = new Vector2(-55f, -48f);
        [SerializeField] private Vector2 FallbackMax = new Vector2(55f, 28f);

        [Header("UI Controls Overlay")]
        [SerializeField] private bool ShowControlsHint = true;

        [SerializeField]
        private AnimationCurve SpeedRamp = new() { keys = new Keyframe[] { new Keyframe(0, 0), new Keyframe(0, 1) } };
        [SerializeField] private bool EnableMousePan = true;

        private CinemachineCamera CinemachineCamera;
        private CinemachineFollow _FollowComponent;
        private Vector2 _LastMousePosition;
        private bool _IsMouseDragging;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            CinemachineCamera = GetComponent<CinemachineCamera>();
            _FollowComponent = GetComponent<CinemachineFollow>();
        }

        private void Start()
        {
            // Backward compatibility
            if (!EnableMousePan) EnableEdgeScroll = false;

            // Set initial perspective based on game mode
            if (RoundManagement.PrehistoricGameModeManager.Instance != null)
            {
                SetPerspective(RoundManagement.PrehistoricGameModeManager.Instance.CurrentMode == RoundManagement.PrehistoricGameMode.TowerDefense
                    ? CameraPerspective.Rear_TowerDefense
                    : CameraPerspective.Front_DinoAssault);
            }
            else
            {
                SetPerspective(CameraPerspective.Rear_TowerDefense);
            }
        }

        private void Update()
        {
            if (Keyboard.current != null)
            {
                // Phím C: Đổi góc nhìn Camera giữa Trước Làng và Sau Làng
                if (Keyboard.current.cKey.wasPressedThisFrame)
                {
                    TogglePerspective();
                }

                // Phím F: Đưa camera quay về trung tâm làng
                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    ResetToCenter();
                }

                // Phím H: Bật/Tắt bảng phím tắt trợ giúp Camera
                if (Keyboard.current.hKey.wasPressedThisFrame)
                {
                    ShowControlsHint = !ShowControlsHint;
                }
            }

            HandleKeyboardMovement();
            HandleCameraOrbit();

            if (EnableMouseDrag)
            {
                HandleMouseDrag();
            }

            if (EnableZoom)
            {
                HandleMouseZoom();
            }

            if (EnableEdgeScroll && !_IsMouseDragging)
            {
                HandleEdgeScroll();
            }

            // Smooth Zoom Interpolation
            _CurrentZoom = Mathf.Lerp(_CurrentZoom, _TargetZoom, Time.deltaTime * ZoomSmoothSpeed);
            UpdateFollowOffset();

            ClampToWorld();
        }

        public void TogglePerspective()
        {
            CameraPerspective next = _CurrentPerspective == CameraPerspective.Rear_TowerDefense
                ? CameraPerspective.Front_DinoAssault
                : CameraPerspective.Rear_TowerDefense;
            SetPerspective(next);

            string label = next == CameraPerspective.Rear_TowerDefense
                ? "📷 ĐÃ CHUYỂN SANG CAMERA SAU LÀNG (NHÌN RA CHIẾN TRƯỜNG - THỦ THÀNH)"
                : "📷 ĐÃ CHUYỂN SANG CAMERA TRƯỚC LÀNG (NHÌN VÀO LÀNG - CÔNG THÀNH)";

            if (RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement(label, 2.5f);
            }
            Debug.Log($"<color=cyan>[CameraControl]</color> Switched Camera to: <b>{next}</b>");
        }

        public void SetPerspective(CameraPerspective perspective)
        {
            _CurrentPerspective = perspective;
            if (_FollowComponent == null) _FollowComponent = GetComponent<CinemachineFollow>();
            if (CinemachineCamera == null) CinemachineCamera = GetComponent<CinemachineCamera>();

            _OrbitAngleY = 0f;
            _TargetZoom = 1.0f;
            _CurrentZoom = 1.0f;

            if (CinemachineCamera != null && CinemachineCamera.Follow != null)
            {
                CinemachineCamera.Follow.position = (_CurrentPerspective == CameraPerspective.Front_DinoAssault)
                    ? new Vector3(0f, 0f, 4f)
                    : new Vector3(0f, 0f, -18f);
            }

            UpdateFollowOffset();
        }

        public void ResetToCenter()
        {
            if (CinemachineCamera == null || CinemachineCamera.Follow == null) return;

            CinemachineCamera.Follow.position = (_CurrentPerspective == CameraPerspective.Rear_TowerDefense)
                ? new Vector3(0f, 0f, -18f)
                : new Vector3(0f, 0f, 4f);

            _OrbitAngleY = 0f;
            _TargetZoom = 1.0f;
            UpdateFollowOffset();

            if (RoundManagement.PrehistoricGameplayManager.Instance != null)
            {
                RoundManagement.PrehistoricGameplayManager.Instance.ShowAnnouncement("🎯 ĐÃ ĐƯA CAMERA VỀ TRUNG TÂM LÀNG", 1.5f);
            }
        }

        private void UpdateFollowOffset()
        {
            if (_FollowComponent == null) return;

            Vector3 baseOffset = (_CurrentPerspective == CameraPerspective.Rear_TowerDefense)
                ? _RearBaseOffset
                : _FrontBaseOffset;

            Quaternion orbitRotation = Quaternion.Euler(0f, _OrbitAngleY, 0f);
            _FollowComponent.FollowOffset = orbitRotation * (baseOffset * _CurrentZoom);
        }

        private void HandleKeyboardMovement()
        {
            if (CinemachineCamera == null || CinemachineCamera.Follow == null || Keyboard.current == null) return;

            Vector3 moveDir = Vector3.zero;
            bool moveUp = Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed;
            bool moveDown = Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed;
            bool moveLeft = Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed;
            bool moveRight = Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed;

            Vector3 camForward = CinemachineCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = CinemachineCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            if (moveUp) moveDir += camForward;
            if (moveDown) moveDir -= camForward;
            if (moveLeft) moveDir -= camRight;
            if (moveRight) moveDir += camRight;

            if (moveDir != Vector3.zero)
            {
                moveDir.Normalize();
                float speed = KeyboardSpeed;
                if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
                {
                    speed *= ShiftMultiplier;
                }
                CinemachineCamera.Follow.position += moveDir * (speed * Time.deltaTime);
            }
        }

        private void HandleCameraOrbit()
        {
            if (!EnableOrbit || Keyboard.current == null) return;

            if (Keyboard.current.qKey.isPressed)
            {
                _OrbitAngleY -= OrbitSpeed * Time.deltaTime;
            }
            if (Keyboard.current.eKey.isPressed)
            {
                _OrbitAngleY += OrbitSpeed * Time.deltaTime;
            }
        }

        private void HandleMouseDrag()
        {
            if (CinemachineCamera == null || CinemachineCamera.Follow == null || Mouse.current == null) return;

            bool isPlacingTower = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;
            bool middleHeld = Mouse.current.middleButton.isPressed;
            bool rightHeld = Mouse.current.rightButton.isPressed && !isPlacingTower;

            if (middleHeld || rightHeld)
            {
                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                if (!_IsMouseDragging)
                {
                    _IsMouseDragging = true;
                    _LastMousePosition = currentMousePos;
                }
                else
                {
                    Vector2 delta = currentMousePos - _LastMousePosition;
                    _LastMousePosition = currentMousePos;

                    if (delta.sqrMagnitude > 0.001f)
                    {
                        Vector3 camRight = CinemachineCamera.transform.right;
                        camRight.y = 0f;
                        camRight.Normalize();

                        Vector3 camForward = CinemachineCamera.transform.forward;
                        camForward.y = 0f;
                        camForward.Normalize();

                        float dragScale = MouseDragSensitivity * _CurrentZoom;
                        float sign = InvertMouseDrag ? -1f : 1f;
                        Vector3 move = (camRight * (delta.x * sign) + camForward * (delta.y * sign)) * dragScale;

                        CinemachineCamera.Follow.position += move;
                    }
                }
            }
            else
            {
                _IsMouseDragging = false;
            }
        }

        private void HandleMouseZoom()
        {
            if (Mouse.current == null) return;

            // If placing tower, only zoom if holding Ctrl to let mouse wheel rotate tower
            bool isPlacingTower = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;
            if (isPlacingTower)
            {
                bool ctrlHeld = Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
                if (!ctrlHeld) return;
            }

            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.1f)
            {
                float scrollDir = Mathf.Sign(scrollY);
                _TargetZoom -= scrollDir * ZoomSensitivity;
                _TargetZoom = Mathf.Clamp(_TargetZoom, MinZoom, MaxZoom);
            }
        }

        private void HandleEdgeScroll()
        {
            if (CinemachineCamera == null || CinemachineCamera.Follow == null || Mouse.current == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            if (screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height)
                return;

            Vector3 moveDir = Vector3.zero;
            Vector3 camRight = CinemachineCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 camForward = CinemachineCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            if (screenPos.x <= EdgeScrollWidth) moveDir -= camRight;
            else if (screenPos.x >= Screen.width - EdgeScrollWidth) moveDir += camRight;

            if (screenPos.y <= EdgeScrollWidth) moveDir -= camForward;
            else if (screenPos.y >= Screen.height - EdgeScrollWidth) moveDir += camForward;

            if (moveDir != Vector3.zero)
            {
                moveDir.Normalize();
                CinemachineCamera.Follow.position += moveDir * (EdgeScrollSpeed * Time.deltaTime);
            }
        }

        private void ClampToWorld()
        {
            if (CinemachineCamera == null || CinemachineCamera.Follow == null) return;

            Vector3 pos = CinemachineCamera.Follow.position;
            float minX = FallbackMin.x;
            float maxX = FallbackMax.x;
            float minZ = FallbackMin.y;
            float maxZ = FallbackMax.y;

            if (WorldBounds != null)
            {
                minX = Mathf.Min(minX, WorldBounds.bounds.min.x - 5f);
                maxX = Mathf.Max(maxX, WorldBounds.bounds.max.x + 5f);
                minZ = Mathf.Min(minZ, WorldBounds.bounds.min.z - 5f);
                maxZ = Mathf.Max(maxZ, WorldBounds.bounds.max.z + 5f);
            }

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
            CinemachineCamera.Follow.position = pos;
        }

        private void OnGUI()
        {
            if (!ShowControlsHint) return;

            // Compact floating hint bar at bottom-left
            float panelWidth = 330f;
            float panelHeight = 85f;
            float x = 15f;
            float y = Screen.height - panelHeight - 15f;

            GUI.color = new Color(0.1f, 0.12f, 0.15f, 0.85f);
            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(x + 8f, y + 6f, panelWidth - 16f, panelHeight - 12f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>🎥 CAMERA TỰ DO</b>", GetHintHeaderStyle());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕ (H)", GUILayout.Width(45), GUILayout.Height(18)))
            {
                ShowControlsHint = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("• <b>WASD / Mũi tên:</b> Di chuyển (<b>Shift</b>: Tăng tốc)", GetHintTextStyle());
            GUILayout.Label("• <b>Chuột Giữa / Phải:</b> Giữ & kéo map tự do", GetHintTextStyle());
            GUILayout.Label("• <b>Cuộn chuột:</b> Zoom | <b>Q/E:</b> Xoay | <b>F:</b> Về làng | <b>C:</b> Góc nhìn", GetHintTextStyle());

            GUILayout.EndArea();
        }

        private GUIStyle _hintHeaderStyle;
        private GUIStyle GetHintHeaderStyle()
        {
            if (_hintHeaderStyle == null)
            {
                _hintHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(1f, 0.85f, 0.4f) }
                };
            }
            return _hintHeaderStyle;
        }

        private GUIStyle _hintTextStyle;
        private GUIStyle GetHintTextStyle()
        {
            if (_hintTextStyle == null)
            {
                _hintTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
                    margin = new RectOffset(0, 0, 0, 1),
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }
            return _hintTextStyle;
        }
    }
}
