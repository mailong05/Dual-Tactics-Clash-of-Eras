using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace LlamAcademy.Dinos.Mobile
{
    /// <summary>
    /// Mobile Deployment Touch Controller.
    /// Provides smooth gesture recognition for mobile touchscreen devices:
    /// - 1-Finger Pan: Slides camera across the battlefield
    /// - 2-Finger Pinch: Zooms in/out
    /// - 2-Finger Twist: Rotates camera perspective
    /// - Tap: Interacts with mobile UI and places prehistoric defenses
    /// </summary>
    public class MobileTouchController : MonoBehaviour
    {
        public static MobileTouchController Instance { get; private set; }

        [Header("Camera Control Settings")]
        [SerializeField] private Camera TargetCamera;
        [SerializeField] private float PanSensitivity = 0.04f;
        [SerializeField] private float ZoomSensitivity = 0.08f;
        [SerializeField] private float MinZoom = 8.0f;
        [SerializeField] private float MaxZoom = 45.0f;
        [SerializeField] private float RotateSensitivity = 0.25f;

        [Header("Mobile Optimization")]
        [SerializeField] private bool ForceMobileSimulation = false;
        [SerializeField] private int TargetFrameRate = 60;

        public bool IsMobileDevice { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            IsMobileDevice = Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld || ForceMobileSimulation;

            // Target 60 FPS for smooth mobile gameplay
            Application.targetFrameRate = TargetFrameRate;
            QualitySettings.vSyncCount = 0;

            if (TargetCamera == null) TargetCamera = Camera.main;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (TargetCamera == null) return;

            var touches = Touch.activeTouches;

            // 1-Finger Drag: Pan battlefield camera
            if (touches.Count == 1)
            {
                var t = touches[0];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    Vector2 delta = t.delta;
                    Vector3 move = new Vector3(-delta.x * PanSensitivity, 0, -delta.y * PanSensitivity);
                    TargetCamera.transform.position += Quaternion.Euler(0, TargetCamera.transform.eulerAngles.y, 0) * move;
                }
            }
            // 2-Finger Pinch / Twist: Zoom & Rotate
            else if (touches.Count >= 2)
            {
                var t0 = touches[0];
                var t1 = touches[1];

                Vector2 prevPos0 = t0.screenPosition - t0.delta;
                Vector2 prevPos1 = t1.screenPosition - t1.delta;

                float prevDist = Vector2.Distance(prevPos0, prevPos1);
                float curDist = Vector2.Distance(t0.screenPosition, t1.screenPosition);
                float pinchDelta = curDist - prevDist;

                // Pinch to Zoom
                if (Mathf.Abs(pinchDelta) > 1.5f)
                {
                    Vector3 fwd = TargetCamera.transform.forward;
                    Vector3 newPos = TargetCamera.transform.position + fwd * (pinchDelta * ZoomSensitivity);
                    float curHeight = newPos.y;
                    if (curHeight >= MinZoom && curHeight <= MaxZoom)
                    {
                        TargetCamera.transform.position = newPos;
                    }
                }

                // Two-finger twist to Rotate
                Vector2 prevDir = (prevPos1 - prevPos0).normalized;
                Vector2 curDir = (t1.screenPosition - t0.screenPosition).normalized;
                float angleDelta = Vector2.SignedAngle(prevDir, curDir);
                if (Mathf.Abs(angleDelta) > 0.5f)
                {
                    TargetCamera.transform.RotateAround(TargetCamera.transform.position + TargetCamera.transform.forward * 20f, Vector3.up, -angleDelta * RotateSensitivity);
                }
            }
        }

        /// <summary>
        /// Triggers mobile haptic vibration when placing a tower or completing an interaction
        /// </summary>
        public void TriggerHapticFeedback()
        {
            if (Application.isMobilePlatform)
            {
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
            }
        }
    }
}
