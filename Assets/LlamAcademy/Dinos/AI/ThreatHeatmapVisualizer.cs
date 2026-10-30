using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.AI
{
    /// <summary>
    /// Visual Heatmap Debugger & Path Overlay for Academic Evaluation.
    /// Projects a terrain-conforming semi-transparent colored grid on the ground:
    /// - Green: Blind spots / Safe corridors
    /// - Yellow: Medium threat
    /// - Red: High danger kill zone
    /// - Purple: Walls / Barricades
    /// Draws LineRenderers for Flanker vs Tanker calculated routes following terrain undulations.
    /// </summary>
    public class ThreatHeatmapVisualizer : MonoBehaviour
    {
        private static ThreatHeatmapVisualizer _instance;
        private static bool s_isApplicationQuitting = false;

        public static bool HasInstance => _instance != null && !s_isApplicationQuitting;

        public static ThreatHeatmapVisualizer Instance
        {
            get
            {
                if (s_isApplicationQuitting) return null;
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ThreatHeatmapVisualizer>();
                    if (_instance == null && !s_isApplicationQuitting)
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
        private MeshFilter _meshFilter;
        private MeshRenderer _overlayRenderer;
        private Mesh _heatmapMesh;
        private Material _overlayMaterial;
        private Texture2D _heatmapTexture;
        private Color32[] _colorBuffer;

        private LineRenderer _flankerLine;
        private LineRenderer _tankerLine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
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

        private void OnApplicationQuit()
        {
            s_isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (ThreatAwarePathfinding.Instance != null)
            {
                ThreatAwarePathfinding.Instance.OnThreatFieldUpdated -= UpdateHeatmapTexture;
            }
            if (_heatmapTexture != null) Destroy(_heatmapTexture);
            if (_heatmapMesh != null) Destroy(_heatmapMesh);
            if (_overlayMaterial != null) Destroy(_overlayMaterial);
            if (_instance == this) _instance = null;
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
                // Rebuild terrain mesh if missing or scene reloaded
                if (_overlayPlane == null || _heatmapMesh == null)
                {
                    CreateHeatmapPlane();
                }
                UpdateHeatmapTexture();
                RefreshVisualPaths();
            }
        }

        /// <summary>
        /// Probes ground surface height (Terrain / NavMesh / Colliders) at any world (X, Z) coordinate.
        /// Prevents overlay and path lines from sinking beneath undulating forest floors or hills.
        /// </summary>
        public static float GetGroundElevation(float worldX, float worldZ)
        {
            // 1. Raycast downwards from sky towards ground
            if (Physics.Raycast(new Vector3(worldX, 60f, worldZ), Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.isTrigger &&
                    !hit.collider.name.Contains("Threat_Heatmap") &&
                    !hit.collider.name.Contains("Dino") &&
                    !hit.collider.name.Contains("Defender"))
                {
                    return hit.point.y;
                }
            }

            // 2. NavMesh elevation sampling fallback
            if (UnityEngine.AI.NavMesh.SamplePosition(new Vector3(worldX, 0f, worldZ), out UnityEngine.AI.NavMeshHit navHit, 15f, UnityEngine.AI.NavMesh.AllAreas))
            {
                return navHit.position.y;
            }

            return 0f;
        }

        /// <summary>
        /// Generates a subdivided 3D grid mesh that perfectly drapes over undulating terrain and forest hills.
        /// </summary>
        private Mesh GenerateTerrainConformingMesh(int w, int h, Vector3 origin, float cellSize)
        {
            Mesh mesh = new Mesh();
            mesh.name = "TerrainConformingHeatmapMesh";

            int vertCountX = w + 1;
            int vertCountZ = h + 1;
            int numVertices = vertCountX * vertCountZ;

            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];
            Vector3[] normals = new Vector3[numVertices];

            for (int z = 0; z <= h; z++)
            {
                for (int x = 0; x <= w; x++)
                {
                    int index = z * vertCountX + x;
                    float worldX = origin.x + x * cellSize;
                    float worldZ = origin.z + z * cellSize;
                    float groundY = GetGroundElevation(worldX, worldZ);

                    // Float slightly above terrain surface (+0.20m) to guarantee clear visibility without z-fighting
                    vertices[index] = new Vector3(worldX, groundY + 0.20f, worldZ);
                    uvs[index] = new Vector2((float)x / w, (float)z / h);
                    normals[index] = Vector3.up;
                }
            }

            int numQuads = w * h;
            int[] triangles = new int[numQuads * 6];
            int triIndex = 0;

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    int v0 = z * vertCountX + x;
                    int v1 = z * vertCountX + (x + 1);
                    int v2 = (z + 1) * vertCountX + x;
                    int v3 = (z + 1) * vertCountX + (x + 1);

                    // Clockwise triangle 1: (v0, v2, v1)
                    triangles[triIndex++] = v0;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v1;

                    // Clockwise triangle 2: (v1, v2, v3)
                    triangles[triIndex++] = v1;
                    triangles[triIndex++] = v2;
                    triangles[triIndex++] = v3;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        private void CreateHeatmapPlane()
        {
            if (_overlayPlane != null)
            {
                Destroy(_overlayPlane);
            }

            int w = ThreatAwarePathfinding.Instance != null ? ThreatAwarePathfinding.Instance.GridWidth : 64;
            int h = ThreatAwarePathfinding.Instance != null ? ThreatAwarePathfinding.Instance.GridHeight : 64;
            float cellSize = ThreatAwarePathfinding.Instance != null ? ThreatAwarePathfinding.Instance.CellSize : 1.4f;
            Vector3 origin = ThreatAwarePathfinding.Instance != null ? ThreatAwarePathfinding.Instance.GridOrigin : new Vector3(-44.8f, 0f, -44.8f);

            if (_heatmapTexture == null || _heatmapTexture.width != w || _heatmapTexture.height != h)
            {
                _heatmapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                _heatmapTexture.filterMode = FilterMode.Bilinear;
                _heatmapTexture.wrapMode = TextureWrapMode.Clamp;
                _colorBuffer = new Color32[w * h];
            }

            // Create terrain-conforming GameObject and MeshFilter
            _overlayPlane = new GameObject("Threat_Heatmap_Overlay_Mesh");
            _overlayPlane.transform.SetParent(transform);
            _overlayPlane.transform.position = Vector3.zero;
            _overlayPlane.transform.rotation = Quaternion.identity;
            _overlayPlane.transform.localScale = Vector3.one;

            _meshFilter = _overlayPlane.AddComponent<MeshFilter>();
            _heatmapMesh = GenerateTerrainConformingMesh(w, h, origin, cellSize);
            _meshFilter.mesh = _heatmapMesh;

            _overlayRenderer = _overlayPlane.AddComponent<MeshRenderer>();
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
            _overlayMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
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
            DrawLinePath(_flankerLine, flankerPath, 0.40f);

            // 2. Calculate Tanker Path (Ankylosaurus charges through walls directly)
            List<Vector3> tankerPath = ThreatAwarePathfinding.Instance.FindThreatAwarePath(spawnPos, basePos, DinoAISpeciesType.AnkylosaurusTanker);
            DrawLinePath(_tankerLine, tankerPath, 0.50f);
        }

        private void DrawLinePath(LineRenderer line, List<Vector3> path, float yOffset)
        {
            if (line == null || path == null || path.Count == 0) return;

            line.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++)
            {
                float groundY = GetGroundElevation(path[i].x, path[i].z);
                line.SetPosition(i, new Vector3(path[i].x, groundY + yOffset + 0.15f, path[i].z));
            }
            line.enabled = IsHeatmapVisible;
        }

        private void OnGUI()
        {
            // Toggle Button in bottom-left
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
