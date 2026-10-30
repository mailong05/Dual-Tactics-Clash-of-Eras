using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.AI
{
    /// <summary>
    /// Visual Heatmap Debugger & Path Overlay for Academic Evaluation.
    /// Projects a semi-transparent colored grid on the ground:
    /// - Green: Blind spots / Safe corridors
    /// - Yellow: Medium threat
    /// - Red: High danger kill zone
    /// - Purple: Walls / Barricades
    /// Draws LineRenderers for Flanker vs Tanker calculated routes.
    /// </summary>
    public class ThreatHeatmapVisualizer : MonoBehaviour
    {
        private static ThreatHeatmapVisualizer _instance;
        public static ThreatHeatmapVisualizer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ThreatHeatmapVisualizer>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ThreatHeatmapVisualizer");
                        _instance = go.AddComponent<ThreatHeatmapVisualizer>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("=== Heatmap Settings ===")]
        public bool IsHeatmapVisible = false;

        private GameObject _overlayPlane;
        private MeshRenderer _overlayRenderer;
        private Material _overlayMaterial;
        private Texture2D _heatmapTexture;
        private Color32[] _colorBuffer;

        private LineRenderer _flankerLine;
        private LineRenderer _tankerLine;

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
            CreateHeatmapPlane();
            CreatePathLineRenderers();

            if (ThreatAwarePathfinding.Instance != null)
            {
                ThreatAwarePathfinding.Instance.OnThreatFieldUpdated += UpdateHeatmapTexture;
            }

            SetVisibility(IsHeatmapVisible);
        }

        private void OnDestroy()
        {
            if (ThreatAwarePathfinding.Instance != null)
            {
                ThreatAwarePathfinding.Instance.OnThreatFieldUpdated -= UpdateHeatmapTexture;
            }
            if (_heatmapTexture != null) Destroy(_heatmapTexture);
        }

        private void Update()
        {
            // Hotkey 'H' toggles Threat Heatmap Overlay
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                ToggleHeatmap();
            }

            // Periodically refresh paths if visible
            if (IsHeatmapVisible && Time.frameCount % 30 == 0)
            {
                RefreshVisualPaths();
            }
        }

        public void ToggleHeatmap()
        {
            SetVisibility(!IsHeatmapVisible);
        }

        public void SetVisibility(bool visible)
        {
            IsHeatmapVisible = visible;
            if (_overlayPlane != null) _overlayPlane.SetActive(visible);
            if (_flankerLine != null) _flankerLine.enabled = visible;
            if (_tankerLine != null) _tankerLine.enabled = visible;

            if (visible)
            {
                UpdateHeatmapTexture();
                RefreshVisualPaths();
            }
        }

        private void CreateHeatmapPlane()
        {
            int w = ThreatAwarePathfinding.Instance != null ? ThreatAwarePathfinding.Instance.GridWidth : 64;
            int h = ThreatAwarePathfinding.Instance != null ? ThreatAwarePathfinding.Instance.GridHeight : 64;
            float cellSize = ThreatAwarePathfinding.Instance != null ? ThreatAwarePathfinding.Instance.CellSize : 1.4f;

            _heatmapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _heatmapTexture.filterMode = FilterMode.Bilinear;
            _heatmapTexture.wrapMode = TextureWrapMode.Clamp;
            _colorBuffer = new Color32[w * h];

            _overlayPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _overlayPlane.name = "Threat_Heatmap_Overlay_Plane";
            _overlayPlane.transform.SetParent(transform);

            // Lay flat on ground slightly above terrain
            float totalWidth = w * cellSize;
            float totalHeight = h * cellSize;
            Vector3 origin = ThreatAwarePathfinding.Instance != null ? ThreatAwarePathfinding.Instance.GridOrigin : new Vector3(-44.8f, 0f, -44.8f);

            _overlayPlane.transform.position = new Vector3(origin.x + totalWidth * 0.5f, 0.12f, origin.z + totalHeight * 0.5f);
            _overlayPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _overlayPlane.transform.localScale = new Vector3(totalWidth, totalHeight, 1f);

            Collider col = _overlayPlane.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _overlayRenderer = _overlayPlane.GetComponent<MeshRenderer>();
            _overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;

            // Transparent additive/alpha blend unlit shader
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _overlayMaterial = new Material(shader);
            _overlayMaterial.SetFloat("_Surface", 1); // Transparent
            _overlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _overlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _overlayMaterial.SetInt("_ZWrite", 0);
            _overlayMaterial.renderQueue = 3100; // Transparent overlay pass

            if (_overlayMaterial.HasProperty("_BaseMap"))
                _overlayMaterial.SetTexture("_BaseMap", _heatmapTexture);
            if (_overlayMaterial.HasProperty("_MainTex"))
                _overlayMaterial.SetTexture("_MainTex", _heatmapTexture);
            _overlayMaterial.color = Color.white;

            _overlayRenderer.material = _overlayMaterial;
        }

        private void CreatePathLineRenderers()
        {
            // Flanker Path (Cyan / Emerald)
            GameObject flankerGO = new GameObject("Flanker_Path_Line");
            flankerGO.transform.SetParent(transform);
            _flankerLine = flankerGO.AddComponent<LineRenderer>();
            ConfigureLineRenderer(_flankerLine, new Color(0.1f, 0.95f, 0.85f, 0.95f), new Color(0.1f, 0.95f, 0.4f, 0.95f), 0.5f);

            // Tanker Path (Orange / Crimson)
            GameObject tankerGO = new GameObject("Tanker_Path_Line");
            tankerGO.transform.SetParent(transform);
            _tankerLine = tankerGO.AddComponent<LineRenderer>();
            ConfigureLineRenderer(_tankerLine, new Color(1.0f, 0.55f, 0.1f, 0.95f), new Color(0.95f, 0.15f, 0.15f, 0.95f), 0.65f);
        }

        private void ConfigureLineRenderer(LineRenderer line, Color startColor, Color endColor, float width)
        {
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width * 0.7f;
            line.startColor = startColor;
            line.endColor = endColor;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            mat.color = startColor;
            line.material = mat;
            line.positionCount = 0;
            line.enabled = false;
        }

        public void UpdateHeatmapTexture()
        {
            if (ThreatAwarePathfinding.Instance == null || _heatmapTexture == null) return;

            int w = ThreatAwarePathfinding.Instance.GridWidth;
            int h = ThreatAwarePathfinding.Instance.GridHeight;
            float maxThreat = Mathf.Max(ThreatAwarePathfinding.Instance.GetMaxThreatScore(), 1.0f);

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    var node = ThreatAwarePathfinding.Instance.GetNode(x, z);
                    if (node == null) continue;

                    Color cellColor;
                    if (node.HasWall)
                    {
                        // Wall / Barricade: Violet tint
                        cellColor = new Color(0.55f, 0.2f, 0.85f, 0.55f);
                    }
                    else
                    {
                        float normThreat = Mathf.Clamp01(node.ThreatScore / maxThreat);

                        if (normThreat <= 0.05f)
                        {
                            // Safe Blind Spot / Corridor (Emerald Green)
                            cellColor = new Color(0.10f, 0.85f, 0.25f, 0.22f);
                        }
                        else if (normThreat < 0.45f)
                        {
                            // Low to Moderate Danger (Green -> Yellow)
                            float t = (normThreat - 0.05f) / 0.40f;
                            cellColor = Color.Lerp(new Color(0.2f, 0.85f, 0.2f, 0.25f), new Color(0.95f, 0.85f, 0.1f, 0.42f), t);
                        }
                        else
                        {
                            // Heavy Threat / Kill Zone (Yellow -> Red)
                            float t = (normThreat - 0.45f) / 0.55f;
                            cellColor = Color.Lerp(new Color(0.95f, 0.85f, 0.1f, 0.42f), new Color(0.95f, 0.12f, 0.10f, 0.65f), t);
                        }
                    }

                    _colorBuffer[z * w + x] = cellColor;
                }
            }

            _heatmapTexture.SetPixels32(_colorBuffer);
            _heatmapTexture.Apply();
        }

        public void RefreshVisualPaths()
        {
            if (ThreatAwarePathfinding.Instance == null) return;

            Vector3 spawnPos = new Vector3(-2.2f, 0f, 32f);
            Vector3 basePos = new Vector3(-2.2f, 0f, -38f);

            if (PrehistoricGameplayManager.Instance != null)
            {
                basePos = PrehistoricGameplayManager.Instance.GetValidBaseTargetPosition();
            }

            // 1. Calculate Flanker Path (Velociraptor avoids towers through blind spots)
            List<Vector3> flankerPath = ThreatAwarePathfinding.Instance.FindThreatAwarePath(spawnPos, basePos, DinoAISpeciesType.VelociraptorFlanker);
            DrawLinePath(_flankerLine, flankerPath, 0.35f);

            // 2. Calculate Tanker Path (Ankylosaurus charges through walls directly)
            List<Vector3> tankerPath = ThreatAwarePathfinding.Instance.FindThreatAwarePath(spawnPos, basePos, DinoAISpeciesType.AnkylosaurusTanker);
            DrawLinePath(_tankerLine, tankerPath, 0.45f);
        }

        private void DrawLinePath(LineRenderer line, List<Vector3> path, float yOffset)
        {
            if (line == null || path == null || path.Count == 0) return;

            line.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++)
            {
                line.SetPosition(i, new Vector3(path[i].x, path[i].y + yOffset, path[i].z));
            }
            line.enabled = IsHeatmapVisible;
        }

        private void OnGUI()
        {
            // Toggle Button in top-left
            GUI.skin = null;
            GUI.depth = -200;

            int btnW = 260;
            int btnH = 36;
            int posX = 15;
            int posY = Screen.height - 110;

            GUI.backgroundColor = IsHeatmapVisible ? new Color(0.2f, 0.85f, 0.4f, 0.95f) : new Color(0.25f, 0.35f, 0.45f, 0.85f);
            string btnText = IsHeatmapVisible ? "🗺️ ẨN BẢN ĐỒ ĐE DỌA (HEATMAP) [H]" : "🗺️ HIỆN BẢN ĐỒ ĐE DỌA (HEATMAP) [H]";

            if (GUI.Button(new Rect(posX, posY, btnW, btnH), $"<b>{btnText}</b>"))
            {
                ToggleHeatmap();
            }

            // Legend when visible
            if (IsHeatmapVisible)
            {
                Rect legendRect = new Rect(posX, posY - 150, 310, 142);
                GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.92f);
                GUI.Box(legendRect, "");

                GUILayout.BeginArea(new Rect(posX + 8, posY - 146, 294, 134));
                GUILayout.Label("<color=cyan><b>CHÚ GIẢI THUẬT TOÁN THREAT $A^*$</b></color>", GetHeaderStyle());
                GUILayout.Label("🟩 <b>Xanh lá</b>: Vùng mù hỏa lực / Hành lang an toàn", GetItemStyle());
                GUILayout.Label("🟨 <b>Vàng</b>: Vùng sát thương trung bình", GetItemStyle());
                GUILayout.Label("🟥 <b>Đỏ</b>: Tử địa tập trung hỏa lực tháp", GetItemStyle());
                GUILayout.Label("<color=#20F0D0>━</color> <b>Đường Xanh Lam</b>: Lộ trình Bọc Sườn (Flanker $A^*$)", GetItemStyle());
                GUILayout.Label("<color=#FF8820>━</color> <b>Đường Cam Đỏ</b>: Lộ trình Đục Phá (Tanker $A^*$)", GetItemStyle());
                GUILayout.EndArea();
            }
        }

        private GUIStyle GetHeaderStyle()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = 12;
            s.fontStyle = FontStyle.Bold;
            s.richText = true;
            return s;
        }

        private GUIStyle GetItemStyle()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = 11;
            s.richText = true;
            s.normal.textColor = Color.white;
            return s;
        }
    }
}
