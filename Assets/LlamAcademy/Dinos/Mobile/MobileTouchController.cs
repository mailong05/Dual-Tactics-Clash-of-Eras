using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;

namespace LlamAcademy.Dinos.Mobile
{
    /// <summary>
    /// Mobile Deployment Touch Controller.
    /// Provides smooth gesture recognition for mobile touchscreen devices:
    /// - 1-Finger Pan: Slides camera across the battlefield (when not placing tower/dino)
    /// - 2-Finger Pinch: Zooms camera in/out
    /// - 2-Finger Midpoint Pan: Pans camera freely even during placement
    /// - 2-Finger Twist: Rotates camera perspective
    /// - Haptic feedback for tactile satisfaction on mobile devices
    /// </summary>
    public class MobileTouchController : MonoBehaviour
    {
        public static MobileTouchController Instance { get; private set; }

        [Header("Camera Sensitivity Settings")]
        [SerializeField] private float PanSensitivity = 1.0f;
        [SerializeField] private float ZoomSensitivity = 1.0f;
        [SerializeField] private float RotateSensitivity = 0.5f;

        [Header("Mobile Optimization")]
        [SerializeField] private bool ForceMobileSimulation = false;
        [SerializeField] private int TargetFrameRate = 60;

        public bool IsMobileDevice { get; private set; }

        private int _ignoredTouchId = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            IsMobileDevice = MobileUIHelper.IsMobile || ForceMobileSimulation;
            MobileUIHelper.ForceMobileSimulation = ForceMobileSimulation;

            // Target 60 FPS for smooth mobile gameplay
            Application.targetFrameRate = TargetFrameRate;
            QualitySettings.vSyncCount = 0;
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
            if (!IsMobileDevice && !MobileUIHelper.IsMobile) return;

            var touches = Touch.activeTouches;
            if (touches.Count == 0)
            {
                _ignoredTouchId = -1;
                return;
            }

            bool isPlacingTower = TowerPlacer.Instance != null && TowerPlacer.Instance.ActiveTower != null;
            bool isPlacingDino = PrehistoricGameModeManager.Instance != null 
                && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault;

            // 1-Finger Touch: Pan battlefield camera (ONLY if NOT placing tower/dino and NOT tapping UI)
            if (touches.Count == 1)
            {
                var t = touches[0];

                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (MobileUIHelper.IsPointerOverUI(t.screenPosition))
                    {
                        _ignoredTouchId = t.touchId;
                    }
                    else
                    {
                        _ignoredTouchId = -1;
                    }
                }

                // When actively placing a tower or dino, 1-finger is dedicated to aiming placement!
                if (isPlacingTower || isPlacingDino)
                {
                    return;
                }

                if (_ignoredTouchId != t.touchId && t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    if (CameraControl.Instance != null)
                    {
                        CameraControl.Instance.PanCamera(t.delta * PanSensitivity);
                    }
                }
            }
            // 2-Finger Touch: Pinch to Zoom, Midpoint Pan, and Twist to Rotate (Available even during placement)
            else if (touches.Count >= 2)
            {
                var t0 = touches[0];
                var t1 = touches[1];

                Vector2 prevPos0 = t0.screenPosition - t0.delta;
                Vector2 prevPos1 = t1.screenPosition - t1.delta;

                float prevDist = Vector2.Distance(prevPos0, prevPos1);
                float curDist = Vector2.Distance(t0.screenPosition, t1.screenPosition);
                float pinchDelta = curDist - prevDist;

                // 2-Finger Pinch to Zoom
                if (Mathf.Abs(pinchDelta) > 1.2f)
                {
                    if (CameraControl.Instance != null)
                    {
                        CameraControl.Instance.ZoomCamera(pinchDelta * ZoomSensitivity);
                    }
                }

                // 2-Finger Midpoint Pan
                Vector2 midDelta = (t0.delta + t1.delta) * 0.5f;
                if (midDelta.sqrMagnitude > 0.5f && CameraControl.Instance != null)
                {
                    CameraControl.Instance.PanCamera(midDelta * PanSensitivity);
                }

                // 2-Finger Twist to Rotate
                Vector2 prevDir = (prevPos1 - prevPos0).normalized;
                Vector2 curDir = (t1.screenPosition - t0.screenPosition).normalized;
                float angleDelta = Vector2.SignedAngle(prevDir, curDir);
                if (Mathf.Abs(angleDelta) > 0.4f && CameraControl.Instance != null)
                {
                    CameraControl.Instance.OrbitCamera(-angleDelta * RotateSensitivity);
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
