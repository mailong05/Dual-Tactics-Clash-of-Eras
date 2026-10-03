using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.Tactical2D
{
    /// <summary>
    /// Unity 2D Tactical Minimap & Radar System.
    /// Maps 3D battlefield entities (Base, Towers, Dinosaurs, Routes) into a 2D interactive tactical radar.
    /// Demonstrates 2D UI/Sprite coordinate transformations interacting dynamically with 3D game entities.
    /// </summary>
    public class TacticalMinimap2D : MonoBehaviour
    {
        public static TacticalMinimap2D Instance { get; private set; }

        [Header("Minimap Dimensions")]
        [SerializeField] private Vector2 MapWorldSize = new Vector2(60f, 80f); // 3D bounds coverage
        [SerializeField] private Vector2 MapWorldCenter = new Vector2(0f, 0f);
        [SerializeField] private float RadarDiameter = 180f;

        [Header("Visual Elements")]
        private RectTransform MinimapContainer;
        private RectTransform SweepLine;
        private List<RectTransform> DinoBlips = new();
        private List<RectTransform> DefenseBlips = new();

        private Sprite DotSprite;
        private Sprite BaseSprite;

        private void Awake()
        {
            if (Instance != null)
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
            // Create circular radar background
            GameObject rootGO = new GameObject("TacticalMinimap2D_Root");
            rootGO.transform.SetParent(transform, false);

            MinimapContainer = rootGO.AddComponent<RectTransform>();
            MinimapContainer.anchorMin = new Vector2(0f, 1f);
            MinimapContainer.anchorMax = new Vector2(0f, 1f);
            MinimapContainer.pivot = new Vector2(0f, 1f);
            MinimapContainer.anchoredPosition = new Vector2(20f, -20f);
            MinimapContainer.sizeDelta = new Vector2(RadarDiameter, RadarDiameter);

            // Background panel
            Image bg = rootGO.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.12f, 0.08f, 0.85f); // Deep radar green

            // Radar Border
            Outline outline = rootGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.9f, 0.4f, 0.7f);
            outline.effectDistance = new Vector2(2f, 2f);

            // Title Header
            GameObject titleGO = new GameObject("RadarTitle");
            titleGO.transform.SetParent(MinimapContainer, false);
            Text titleTxt = titleGO.AddComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.text = "2D TACTICAL RADAR";
            titleTxt.fontSize = 11;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            RectTransform titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -6);
            titleRect.sizeDelta = new Vector2(RadarDiameter, 16);

            // Animated Sweep Line
            GameObject sweepGO = new GameObject("SweepLine");
            sweepGO.transform.SetParent(MinimapContainer, false);
            SweepLine = sweepGO.AddComponent<RectTransform>();
            SweepLine.anchorMin = new Vector2(0.5f, 0.5f);
            SweepLine.anchorMax = new Vector2(0.5f, 0.5f);
            SweepLine.pivot = new Vector2(0.5f, 0f);
            SweepLine.sizeDelta = new Vector2(2f, RadarDiameter * 0.48f);
            Image sweepImg = sweepGO.AddComponent<Image>();
            sweepImg.color = new Color(0.3f, 1f, 0.5f, 0.35f);

            // Center base icon (Green home)
            GameObject baseGO = new GameObject("BaseBlip");
            baseGO.transform.SetParent(MinimapContainer, false);
            RectTransform baseRect = baseGO.AddComponent<RectTransform>();
            baseRect.sizeDelta = new Vector2(10f, 10f);
            baseRect.anchoredPosition = WorldToMinimapPosition(new Vector3(-2.23f, 0, -38.26f));
            Image baseImg = baseGO.AddComponent<Image>();
            baseImg.color = new Color(0.1f, 1.0f, 0.2f, 1.0f); // Bright green base
        }

        private void Update()
        {
            if (MinimapContainer == null) return;

            // Rotate sweep line
            if (SweepLine != null)
            {
                SweepLine.Rotate(Vector3.forward, -120f * Time.deltaTime);
            }

            UpdateDinoBlips();
            UpdateDefenseBlips();
        }

        private void UpdateDinoBlips()
        {
            // Find all active enemies in scene
            PrehistoricDinoBase[] dinos = FindObjectsByType<PrehistoricDinoBase>(FindObjectsSortMode.None);
            FlyingUnit[] flyers = FindObjectsByType<FlyingUnit>(FindObjectsSortMode.None);
            int totalEnemies = dinos.Length + flyers.Length;

            // Resize blip pool
            while (DinoBlips.Count < totalEnemies)
            {
                GameObject blipGO = new GameObject($"DinoBlip_{DinoBlips.Count}");
                blipGO.transform.SetParent(MinimapContainer, false);
                RectTransform rt = blipGO.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(6f, 6f);
                Image img = blipGO.AddComponent<Image>();
                img.color = new Color(1.0f, 0.2f, 0.2f, 0.95f); // Red enemy blip
                DinoBlips.Add(rt);
            }

            int index = 0;
            foreach (PrehistoricDinoBase dino in dinos)
            {
                if (dino == null || !dino.gameObject.activeInHierarchy || dino.Health <= 0) continue;
                RectTransform blip = DinoBlips[index++];
                blip.gameObject.SetActive(true);
                blip.anchoredPosition = WorldToMinimapPosition(dino.transform.position);
            }
            foreach (FlyingUnit flyer in flyers)
            {
                if (flyer == null || !flyer.gameObject.activeInHierarchy || flyer.Health <= 0) continue;
                RectTransform blip = DinoBlips[index++];
                blip.gameObject.SetActive(true);
                blip.anchoredPosition = WorldToMinimapPosition(flyer.transform.position);
            }

            for (int i = index; i < DinoBlips.Count; i++)
            {
                DinoBlips[i].gameObject.SetActive(false);
            }
        }

        private void UpdateDefenseBlips()
        {
            // Track towers & barricades
            Unit.Unit[] defenses = FindObjectsByType<Unit.Unit>(FindObjectsSortMode.None);
            int validDefenses = 0;

            for (int i = 0; i < defenses.Length; i++)
            {
                if (defenses[i] is PrehistoricDinoBase || defenses[i] is FlyingUnit) continue;
                validDefenses++;
            }

            while (DefenseBlips.Count < validDefenses)
            {
                GameObject blipGO = new GameObject($"DefenseBlip_{DefenseBlips.Count}");
                blipGO.transform.SetParent(MinimapContainer, false);
                RectTransform rt = blipGO.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(5f, 5f);
                Image img = blipGO.AddComponent<Image>();
                img.color = new Color(0.2f, 0.6f, 1.0f, 0.85f); // Cyan friendly defense
                DefenseBlips.Add(rt);
            }

            int index = 0;
            foreach (Unit.Unit def in defenses)
            {
                if (def is PrehistoricDinoBase || def is FlyingUnit) continue;
                if (def == null || !def.gameObject.activeInHierarchy || def.Health <= 0) continue;

                RectTransform blip = DefenseBlips[index++];
                blip.gameObject.SetActive(true);
                blip.anchoredPosition = WorldToMinimapPosition(def.transform.position);
            }

            for (int i = index; i < DefenseBlips.Count; i++)
            {
                DefenseBlips[i].gameObject.SetActive(false);
            }
        }

        private Vector2 WorldToMinimapPosition(Vector3 worldPos)
        {
            float normX = (worldPos.x - MapWorldCenter.x) / MapWorldSize.x;
            float normZ = (worldPos.z - MapWorldCenter.y) / MapWorldSize.y;

            normX = Mathf.Clamp(normX, -0.5f, 0.5f);
            normZ = Mathf.Clamp(normZ, -0.5f, 0.5f);

            return new Vector2(normX * RadarDiameter * 0.9f, normZ * RadarDiameter * 0.9f);
        }
    }
}
