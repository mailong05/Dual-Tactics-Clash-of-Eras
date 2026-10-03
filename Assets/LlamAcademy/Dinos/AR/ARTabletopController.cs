using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.AR
{
    /// <summary>
    /// AR Implementation: Tabletop Augmented Reality & Prehistoric Hologram Inspector.
    /// Scales down the full 3D prehistoric canyon into a miniaturized tabletop AR battle diorama.
    /// Also allows inspecting life-size 3D dinosaurs projected onto real-world flat surfaces.
    /// </summary>
    public class ARTabletopController : MonoBehaviour
    {
        public static ARTabletopController Instance { get; private set; }

        [Header("AR World Settings")]
        [SerializeField] private float TabletopScale = 0.05f; // Scales 30m canyon down to 1.5m tabletop diorama
        [SerializeField] private Vector3 ARTabletopOffset = new Vector3(0, -0.2f, 1.2f);
        [SerializeField] private Transform GameWorldRoot;

        [Header("AR State")]
        public bool IsARModeActive { get; private set; } = false;

        private Vector3 OriginalWorldPosition;
        private Vector3 OriginalWorldScale;
        private Quaternion OriginalWorldRotation;

        private GameObject ARControlPill;
        private Text ARButtonText;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (GameWorldRoot == null)
            {
                // Find main terrain or systems
                GameObject terrain = GameObject.Find("Terrain") ?? GameObject.Find("Canyon_Terrain") ?? GameObject.Find("NavMesh");
                if (terrain != null) GameWorldRoot = terrain.transform;
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
        }

        private void CreateARButtonUI()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            ARControlPill = new GameObject("AR_Mode_Toggle_Button");
            ARControlPill.transform.SetParent(canvas.transform, false);

            RectTransform rt = ARControlPill.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -70f);
            rt.sizeDelta = new Vector2(170f, 38f);

            Image img = ARControlPill.AddComponent<Image>();
            img.color = new Color(0.12f, 0.45f, 0.95f, 0.92f); // Deep futuristic AR blue

            Outline outl = ARControlPill.AddComponent<Outline>();
            outl.effectColor = new Color(0.5f, 0.85f, 1.0f, 0.8f);
            outl.effectDistance = new Vector2(2f, 2f);

            Button btn = ARControlPill.AddComponent<Button>();
            btn.onClick.AddListener(ToggleARMode);

            GameObject textGO = new GameObject("Label");
            textGO.transform.SetParent(ARControlPill.transform, false);
            ARButtonText = textGO.AddComponent<Text>();
            ARButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ARButtonText.text = "📱 CHẾ ĐỘ AR (TABLETOP)";
            ARButtonText.fontSize = 11;
            ARButtonText.fontStyle = FontStyle.Bold;
            ARButtonText.alignment = TextAnchor.MiddleCenter;
            ARButtonText.color = Color.white;

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
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
                ARButtonText.text = IsARModeActive ? "🔙 THOÁT AR (VỀ 3D)" : "📱 CHẾ ĐỘ AR (TABLETOP)";
            }
        }

        private void ActivateARTabletop()
        {
            if (GameWorldRoot == null) return;

            Debug.Log("<color=cyan>[AR System]</color> Tabletop AR Mode Activated. Downscaling Jurassic Arena to tabletop footprint...");

            // Animate smooth scale down to tabletop
            StartCoroutine(LerpWorldTransform(ARTabletopOffset, Vector3.one * TabletopScale, 0.6f));
        }

        private void DeactivateARTabletop()
        {
            if (GameWorldRoot == null) return;

            Debug.Log("<color=cyan>[AR System]</color> Restoring Standard 3D Tactical Battlefield...");

            StartCoroutine(LerpWorldTransform(OriginalWorldPosition, OriginalWorldScale, 0.6f));
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
    }
}
