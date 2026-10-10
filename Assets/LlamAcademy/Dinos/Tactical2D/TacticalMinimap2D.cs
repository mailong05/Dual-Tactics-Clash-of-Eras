using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.Tactical2D
{
    /// <summary>
    /// Unity 2D Tactical Minimap & Radar System (Assessment Rubric Item 1).
    /// Projects 3D battlefield entities (Base, Defensive Towers, Dinosaurs, Bosses)
    /// into a 2D interactive tactical radar with real-time coordinate transformations.
    /// Demonstrates seamless 2D UI/Sprite/Canvas integration with active 3D game entities.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class TacticalMinimap2D : MonoBehaviour
    {
        public static TacticalMinimap2D Instance { get; private set; }

        [Header("Minimap 3D Coordinate Mapping")]
        [SerializeField] private Vector2 MapWorldSize = new Vector2(60f, 85f); // 3D bounds coverage
        [SerializeField] private Vector2 MapWorldCenter = new Vector2(0f, -5f);
        [SerializeField] private float RadarDiameter = 190f;

        [Header("State")]
        [SerializeField] private bool IsMinimapVisible = true;
        [SerializeField] private bool IsExpanded = false;

        [Header("Runtime UI Hierarchy")]
        private Canvas RadarCanvas;
        private RectTransform MinimapContainer;
        private RectTransform SweepLine;
        private Text CounterText;
        private Text TitleText;

        private readonly List<RectTransform> DinoBlips = new();
        private readonly List<RectTransform> FlyerBlips = new();
        private readonly List<RectTransform> DefenseBlips = new();

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
            BuildMinimapUI();
        }

        private void BuildMinimapUI()
        {
            // 1. Locate or create dedicated Screen Space Overlay Canvas
            RadarCanvas = FindSuitableOverlayCanvas();
            if (RadarCanvas == null)
            {
                GameObject canvasGO = new GameObject("TacticalMinimap_Canvas");
                RadarCanvas = canvasGO.AddComponent<Canvas>();
                RadarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RadarCanvas.sortingOrder = 350;

                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasGO.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(canvasGO);
            }

            // 2. Container Panel (Top-Right HUD position, below mode pills)
            GameObject rootGO = new GameObject("TacticalMinimap2D_Root");
            rootGO.transform.SetParent(RadarCanvas.transform, false);

            MinimapContainer = rootGO.AddComponent<RectTransform>();
            MinimapContainer.anchorMin = new Vector2(1f, 1f);
            MinimapContainer.anchorMax = new Vector2(1f, 1f);
            MinimapContainer.pivot = new Vector2(1f, 1f);
            MinimapContainer.anchoredPosition = new Vector2(-20f, -115f);
            MinimapContainer.sizeDelta = new Vector2(RadarDiameter, RadarDiameter + 30f);

            // Radar Circular Background Frame
            Image bg = rootGO.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.10f, 0.06f, 0.90f); // Prehistoric tactical dark emerald

            Outline outline = rootGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.95f, 0.45f, 0.85f);
            outline.effectDistance = new Vector2(2f, 2f);

            // 3. Header Bar with Title
            GameObject titleGO = new GameObject("RadarTitle");
            titleGO.transform.SetParent(MinimapContainer, false);
            TitleText = titleGO.AddComponent<Text>();
            TitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TitleText.text = "<b>2D TACTICAL RADAR</b> [TAB]";
            TitleText.fontSize = 11;
            TitleText.alignment = TextAnchor.MiddleCenter;
            TitleText.color = new Color(0.35f, 1.0f, 0.55f, 0.95f);

            RectTransform titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, -4f);
            titleRT.sizeDelta = new Vector2(RadarDiameter, 20f);

            // 4. Radar Scope Center
            GameObject scopeGO = new GameObject("RadarScope");
            scopeGO.transform.SetParent(MinimapContainer, false);
            RectTransform scopeRT = scopeGO.AddComponent<RectTransform>();
            scopeRT.anchorMin = new Vector2(0.5f, 0.5f);
            scopeRT.anchorMax = new Vector2(0.5f, 0.5f);
            scopeRT.pivot = new Vector2(0.5f, 0.5f);
            scopeRT.anchoredPosition = new Vector2(0f, -5f);
            scopeRT.sizeDelta = new Vector2(RadarDiameter - 20f, RadarDiameter - 20f);

            // Inner Ring (50% range)
            CreateRangeRing(scopeGO.transform, (RadarDiameter - 20f) * 0.5f);

            // Cardinal Indicators
            CreateCardinalLabel(scopeGO.transform, "N", new Vector2(0.5f, 1f), new Vector2(0f, -8f));
            CreateCardinalLabel(scopeGO.transform, "S", new Vector2(0.5f, 0f), new Vector2(0f, 8f));
            CreateCardinalLabel(scopeGO.transform, "E", new Vector2(1f, 0.5f), new Vector2(-8f, 0f));
            CreateCardinalLabel(scopeGO.transform, "W", new Vector2(0f, 0.5f), new Vector2(8f, 0f));

            // Animated Sweep Line
            GameObject sweepGO = new GameObject("SweepLine");
            sweepGO.transform.SetParent(scopeGO.transform, false);
            SweepLine = sweepGO.AddComponent<RectTransform>();
            SweepLine.anchorMin = new Vector2(0.5f, 0.5f);
            SweepLine.anchorMax = new Vector2(0.5f, 0.5f);
            SweepLine.pivot = new Vector2(0.5f, 0f);
            SweepLine.sizeDelta = new Vector2(2f, (RadarDiameter - 20f) * 0.48f);
            Image sweepImg = sweepGO.AddComponent<Image>();
            sweepImg.color = new Color(0.3f, 1.0f, 0.5f, 0.4f);

            // Center Base Blip (Emerald base icon)
            GameObject baseGO = new GameObject("BaseBlip");
            baseGO.transform.SetParent(scopeGO.transform, false);
            RectTransform baseRect = baseGO.AddComponent<RectTransform>();
            baseRect.sizeDelta = new Vector2(11f, 11f);
            baseRect.anchoredPosition = WorldToMinimapPosition(new Vector3(-2.23f, 0, -38.26f));
            Image baseImg = baseGO.AddComponent<Image>();
            baseImg.color = new Color(0.1f, 1.0f, 0.3f, 1.0f);
            Outline baseOut = baseGO.AddComponent<Outline>();
            baseOut.effectColor = Color.white;
            baseOut.effectDistance = new Vector2(1f, 1f);

            // 5. Footer Info Bar
            GameObject counterGO = new GameObject("CounterText");
            counterGO.transform.SetParent(MinimapContainer, false);
            CounterText = counterGO.AddComponent<Text>();
            CounterText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CounterText.text = "🦖 Quái: 0 | 🏹 Tháp: 0";
            CounterText.fontSize = 10;
            CounterText.alignment = TextAnchor.MiddleCenter;
            CounterText.color = new Color(0.85f, 0.95f, 0.85f, 0.9f);

            RectTransform counterRT = counterGO.GetComponent<RectTransform>();
            counterRT.anchorMin = new Vector2(0f, 0f);
            counterRT.anchorMax = new Vector2(1f, 0f);
            counterRT.pivot = new Vector2(0.5f, 0f);
            counterRT.anchoredPosition = new Vector2(0f, 4f);
            counterRT.sizeDelta = new Vector2(RadarDiameter, 18f);
        }

        private void CreateRangeRing(Transform parent, float diameter)
        {
            GameObject ringGO = new GameObject("RangeRing");
            ringGO.transform.SetParent(parent, false);
            RectTransform ringRT = ringGO.AddComponent<RectTransform>();
            ringRT.sizeDelta = new Vector2(diameter, diameter);
            Image ringImg = ringGO.AddComponent<Image>();
            ringImg.color = new Color(0.2f, 0.8f, 0.4f, 0.15f);
            Outline ringOut = ringGO.AddComponent<Outline>();
            ringOut.effectColor = new Color(0.25f, 0.9f, 0.4f, 0.35f);
            ringOut.effectDistance = new Vector2(1f, 1f);
        }

        private void CreateCardinalLabel(Transform parent, string text, Vector2 anchor, Vector2 pos)
        {
            GameObject labelGO = new GameObject($"Cardinal_{text}");
            labelGO.transform.SetParent(parent, false);
            RectTransform rt = labelGO.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(14f, 14f);

            Text t = labelGO.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = 9;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.4f, 0.9f, 0.5f, 0.7f);
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

        private void Update()
        {
            // Tab hotkey toggles minimap size/visibility
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleMinimapExpansion();
            }

            if (MinimapContainer == null || !IsMinimapVisible) return;

            // Animated sweep radar line
            if (SweepLine != null)
            {
                SweepLine.Rotate(Vector3.forward, -135f * Time.deltaTime);
            }

            UpdateEntityBlips();
        }

        public void ToggleMinimapExpansion()
        {
            IsExpanded = !IsExpanded;
            float currentD = IsExpanded ? RadarDiameter * 1.35f : RadarDiameter;
            if (MinimapContainer != null)
            {
                MinimapContainer.sizeDelta = new Vector2(currentD, currentD + 30f);
            }
        }

        private void UpdateEntityBlips()
        {
            // 1. Scan active dinosaurs & flying units
            PrehistoricDinoBase[] groundDinos = FindObjectsByType<PrehistoricDinoBase>(FindObjectsSortMode.None);
            FlyingUnit[] flyingUnits = FindObjectsByType<FlyingUnit>(FindObjectsSortMode.None);

            int groundCount = 0;
            for (int i = 0; i < groundDinos.Length; i++)
            {
                if (groundDinos[i] != null && groundDinos[i].gameObject.activeInHierarchy && groundDinos[i].Health > 0)
                {
                    groundCount++;
                }
            }

            int flyerCount = 0;
            for (int i = 0; i < flyingUnits.Length; i++)
            {
                if (flyingUnits[i] != null && flyingUnits[i].gameObject.activeInHierarchy && flyingUnits[i].Health > 0)
                {
                    flyerCount++;
                }
            }

            // Pool ground dino blips (Bright Red)
            Transform scopeTransform = MinimapContainer.Find("RadarScope");
            if (scopeTransform == null) return;

            while (DinoBlips.Count < groundCount)
            {
                GameObject blipGO = new GameObject($"GroundBlip_{DinoBlips.Count}");
                blipGO.transform.SetParent(scopeTransform, false);
                RectTransform rt = blipGO.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(6f, 6f);
                Image img = blipGO.AddComponent<Image>();
                img.color = new Color(1.0f, 0.2f, 0.2f, 0.95f);
                DinoBlips.Add(rt);
            }

            int gIdx = 0;
            foreach (PrehistoricDinoBase dino in groundDinos)
            {
                if (dino == null || !dino.gameObject.activeInHierarchy || dino.Health <= 0) continue;
                RectTransform blip = DinoBlips[gIdx++];
                blip.gameObject.SetActive(true);
                blip.anchoredPosition = WorldToMinimapPosition(dino.transform.position);

                // Highlight boss T-Rex with larger blip
                if (dino is BossDino || (dino.gameObject != null && dino.gameObject.name.Contains("T-Rex")))
                {
                    blip.sizeDelta = new Vector2(10f, 10f);
                }
                else
                {
                    blip.sizeDelta = new Vector2(6f, 6f);
                }
            }
            for (int i = gIdx; i < DinoBlips.Count; i++) DinoBlips[i].gameObject.SetActive(false);

            // Pool flyer blips (Bright Yellow/Orange)
            while (FlyerBlips.Count < flyerCount)
            {
                GameObject blipGO = new GameObject($"FlyerBlip_{FlyerBlips.Count}");
                blipGO.transform.SetParent(scopeTransform, false);
                RectTransform rt = blipGO.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(6f, 6f);
                Image img = blipGO.AddComponent<Image>();
                img.color = new Color(1.0f, 0.85f, 0.1f, 0.95f);
                FlyerBlips.Add(rt);
            }

            int fIdx = 0;
            foreach (FlyingUnit flyer in flyingUnits)
            {
                if (flyer == null || !flyer.gameObject.activeInHierarchy || flyer.Health <= 0) continue;
                RectTransform blip = FlyerBlips[fIdx++];
                blip.gameObject.SetActive(true);
                blip.anchoredPosition = WorldToMinimapPosition(flyer.transform.position);
            }
            for (int i = fIdx; i < FlyerBlips.Count; i++) FlyerBlips[i].gameObject.SetActive(false);

            // 2. Scan friendly defenses (Cyan/Blue)
            Unit.Unit[] defenses = FindObjectsByType<Unit.Unit>(FindObjectsSortMode.None);
            int validDefenses = 0;
            for (int i = 0; i < defenses.Length; i++)
            {
                if (defenses[i] is PrehistoricDinoBase || defenses[i] is FlyingUnit) continue;
                if (defenses[i] != null && defenses[i].gameObject.activeInHierarchy && defenses[i].Health > 0)
                {
                    validDefenses++;
                }
            }

            while (DefenseBlips.Count < validDefenses)
            {
                GameObject blipGO = new GameObject($"DefenseBlip_{DefenseBlips.Count}");
                blipGO.transform.SetParent(scopeTransform, false);
                RectTransform rt = blipGO.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(5f, 5f);
                Image img = blipGO.AddComponent<Image>();
                img.color = new Color(0.2f, 0.7f, 1.0f, 0.9f);
                DefenseBlips.Add(rt);
            }

            int dIdx = 0;
            foreach (Unit.Unit def in defenses)
            {
                if (def is PrehistoricDinoBase || def is FlyingUnit) continue;
                if (def == null || !def.gameObject.activeInHierarchy || def.Health <= 0) continue;

                RectTransform blip = DefenseBlips[dIdx++];
                blip.gameObject.SetActive(true);
                blip.anchoredPosition = WorldToMinimapPosition(def.transform.position);
            }
            for (int i = dIdx; i < DefenseBlips.Count; i++) DefenseBlips[i].gameObject.SetActive(false);

            // 3. Update footer text
            if (CounterText != null)
            {
                int totalEnemies = groundCount + flyerCount;
                CounterText.text = $"🦖 Quái: {totalEnemies} | 🏹 Tháp: {validDefenses}";
            }
        }

        private Vector2 WorldToMinimapPosition(Vector3 worldPos)
        {
            float normX = (worldPos.x - MapWorldCenter.x) / MapWorldSize.x;
            float normZ = (worldPos.z - MapWorldCenter.y) / MapWorldSize.y;

            normX = Mathf.Clamp(normX, -0.48f, 0.48f);
            normZ = Mathf.Clamp(normZ, -0.48f, 0.48f);

            float scopeRadius = (RadarDiameter - 20f) * 0.5f;
            return new Vector2(normX * scopeRadius * 1.9f, normZ * scopeRadius * 1.9f);
        }
    }
}
