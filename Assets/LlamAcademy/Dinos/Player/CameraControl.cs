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

        [SerializeField]
        private AnimationCurve SpeedRamp = new() { keys = new Keyframe[] { new Keyframe(0, 0), new Keyframe(0, 1) } };

        [SerializeField] private bool EnableMousePan;
        [SerializeField]
        private float EdgeScrollWidth = 100;

        [SerializeField] private BoxCollider WorldBounds;
        [SerializeField]
        [Range(0.01f, 50)]
        private float KeyboardSpeed = 8f;

        private CinemachineCamera CinemachineCamera;
        private CinemachineFollow _FollowComponent;
        private float MouseScrollStartTime;
        private bool IsMouseScrolling;

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
            // Phím C để đổi góc nhìn Camera giữa Trước Làng và Sau Làng
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                TogglePerspective();
            }

            HandleKeyboardInput();
            if (EnableMousePan)
            {
                HandleMouseInput();
            }

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

            if (_FollowComponent != null)
            {
                if (_CurrentPerspective == CameraPerspective.Front_DinoAssault)
                {
                    // Camera trước làng (nhìn theo hướng Nam về phía cổng làng)
                    _FollowComponent.FollowOffset = new Vector3(0f, 22f, 12f);
                    transform.rotation = Quaternion.Euler(58f, 180f, 0f);
                }
                else
                {
                    // Camera sau làng (nhìn theo hướng Bắc ra ngoài cổng và chiến trường)
                    _FollowComponent.FollowOffset = new Vector3(0f, 22f, -14f);
                    transform.rotation = Quaternion.Euler(48f, 0f, 0f);
                }
            }
        }

        private void HandleMouseInput()
        {
            if (CinemachineCamera == null || CinemachineCamera.Follow == null) return;

            Vector3 moveDirection = Vector3.zero;
            Vector3 screenPosition = Mouse.current.position.ReadValue();

            float scrollRightPosition = Screen.width - EdgeScrollWidth;
            float scrollUpPosition = Screen.height - EdgeScrollWidth;

            if (_CurrentPerspective == CameraPerspective.Rear_TowerDefense)
            {
                if (screenPosition.x < EdgeScrollWidth) moveDirection += Vector3.left;
                else if (screenPosition.x > scrollRightPosition) moveDirection += Vector3.right;

                if (screenPosition.y < EdgeScrollWidth) moveDirection += Vector3.back;
                else if (screenPosition.y > scrollUpPosition) moveDirection += Vector3.forward;
            }
            else
            {
                if (screenPosition.x < EdgeScrollWidth) moveDirection += Vector3.right;
                else if (screenPosition.x > scrollRightPosition) moveDirection += Vector3.left;

                if (screenPosition.y < EdgeScrollWidth) moveDirection += Vector3.forward;
                else if (screenPosition.y > scrollUpPosition) moveDirection += Vector3.back;
            }

            if (moveDirection != Vector3.zero)
            {
                if (!IsMouseScrolling) MouseScrollStartTime = Time.time;
                IsMouseScrolling = true;
            }
            else
            {
                IsMouseScrolling = false;
            }

            CinemachineCamera.Follow.position += SpeedRamp.Evaluate(Time.time - MouseScrollStartTime) * Time.deltaTime * moveDirection;
        }

        private void HandleKeyboardInput()
        {
            if (CinemachineCamera == null || CinemachineCamera.Follow == null || Keyboard.current == null) return;

            Vector3 moveDirection = Vector3.zero;
            bool moveUp = Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed;
            bool moveDown = Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed;
            bool moveLeft = Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed;
            bool moveRight = Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed;

            if (_CurrentPerspective == CameraPerspective.Rear_TowerDefense)
            {
                // Khi nhìn từ sau làng ra Bắc: Phím W / Mũi tên lên đi về phía trước (+Z), S / Mũi tên xuống lùi về làng (-Z)
                if (moveUp) moveDirection += Vector3.forward;
                if (moveDown) moveDirection += Vector3.back;
                if (moveLeft) moveDirection += Vector3.left;
                if (moveRight) moveDirection += Vector3.right;
            }
            else
            {
                // Khi nhìn từ trước làng vào Nam: Phím W / Mũi tên lên đi về làng (-Z), S / Mũi tên xuống đi ra ngoài (+Z)
                if (moveUp) moveDirection += Vector3.back;
                if (moveDown) moveDirection += Vector3.forward;
                if (moveLeft) moveDirection += Vector3.right;
                if (moveRight) moveDirection += Vector3.left;
            }

            CinemachineCamera.Follow.position += KeyboardSpeed * Time.deltaTime * moveDirection;
        }

        private void ClampToWorld()
        {
            if (CinemachineCamera == null || CinemachineCamera.Follow == null || WorldBounds == null) return;

            Vector3 pos = CinemachineCamera.Follow.position;
            pos.x = Mathf.Clamp(pos.x, WorldBounds.bounds.min.x, WorldBounds.bounds.max.x);
            pos.z = Mathf.Clamp(pos.z, WorldBounds.bounds.min.z, WorldBounds.bounds.max.z);
            CinemachineCamera.Follow.position = pos;
        }
    }
}
