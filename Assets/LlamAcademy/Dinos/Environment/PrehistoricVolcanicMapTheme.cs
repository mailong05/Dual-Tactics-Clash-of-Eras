using System;
using System.Collections.Generic;
using LlamAcademy.Dinos.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LlamAcademy.Dinos.Environment
{
    /// <summary>
    /// Quản lý hiệu ứng đồ họa, ánh sáng, dung nham và không khí cho Bản đồ 2:
    /// "🌋 Hẻm Núi Dung Nham" (Volcanic Gorge).
    /// Tập trung hoàn toàn vào thác dung nham (Mountain Lava Cascades) chảy từ trên các sườn núi đá bao quanh.
    /// Đảm bảo KHÔNG che khuất pháo đài làng, căn cứ hay bất kỳ NPC nào phía sau.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PrehistoricVolcanicMapTheme : MonoBehaviour
    {
        public static PrehistoricVolcanicMapTheme Instance { get; private set; }

        private GameObject _vfxRoot;
        private Material _lavaWaterfallMaterial;
        private readonly List<Light> _lavaLights = new List<Light>();
        private readonly List<float> _lightBaseIntensities = new List<float>();
        private readonly Dictionary<Material, Color> _originalMaterialColors = new Dictionary<Material, Color>();

        // Lưu trữ thông số môi trường gốc để khôi phục khi chuyển map
        private static bool _originalFog;
        private static Color _originalFogColor;
        private static float _originalFogDensity;
        private static Color _originalAmbientSky;
        private static Color _originalAmbientEquator;
        private static Color _originalAmbientGround;
        private static bool _hasSavedOriginals = false;

        private float _bannerTimer = 0f;
        private const float BANNER_DURATION = 6.0f;
        public bool IsThemeActive { get; private set; } = false;

        public static void EnsureThemeActive()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[Prehistoric_Volcanic_Theme]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<PrehistoricVolcanicMapTheme>();
            }
            Instance.ApplyVolcanicEnvironment();
        }

        public static void EnsureThemeInactive()
        {
            if (Instance != null)
            {
                Instance.RestoreDefaultEnvironment();
                Destroy(Instance.gameObject);
                Instance = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            if (PrehistoricMapManager.CurrentMap == PrehistoricMapType.VolcanicGorge)
            {
                EnsureThemeActive();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (PrehistoricMapManager.CurrentMap == PrehistoricMapType.VolcanicGorge)
            {
                ApplyVolcanicEnvironment();
            }
        }

        public void ApplyVolcanicEnvironment()
        {
            IsThemeActive = true;
            _bannerTimer = BANNER_DURATION;

            // 1. Lưu lại thông số ánh sáng gốc của Thung Lũng Cổ Đại
            if (!_hasSavedOriginals)
            {
                _originalFog = RenderSettings.fog;
                _originalFogColor = RenderSettings.fogColor;
                _originalFogDensity = RenderSettings.fogDensity;
                _originalAmbientSky = RenderSettings.ambientSkyColor;
                _originalAmbientEquator = RenderSettings.ambientEquatorColor;
                _originalAmbientGround = RenderSettings.ambientGroundColor;
                _hasSavedOriginals = true;
            }

            // 2. Thiết lập bầu không khí núi lửa rực lửa & khói bụi nham thạch
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.11f, 0.04f, 1f); // Nâu đỏ tro tàn
            RenderSettings.fogDensity = 0.0095f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(1.0f, 0.40f, 0.15f, 1f);      // Cam đỏ bầu trời lửa
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.22f, 0.08f, 1f);  // Rực hồng lòng chảo
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.04f, 0.02f, 1f);   // Đen bazan nham thạch nguội

            // 3. Chỉnh màu Sun Directional Light sang ánh vàng nắng hoàng hôn rực rỡ
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    l.color = new Color(1f, 0.70f, 0.42f);
                    l.intensity = 1.15f;
                    break;
                }
            }

            // 4. Khởi tạo Material và hệ thống Thác Dung Nham bám sát vách núi
            EnsureWaterfallMaterial();
            BuildMountainLavaWaterfalls();
            BuildMountainLavaLights();
            BuildVolcanicEmberParticles();
            DecorateLavaBoulders();
            ApplyVolcanicTerrainTint();

            Debug.Log("<color=orange>[Prehistoric TD]</color> 🌋 ĐÃ KÍCH HOẠT THÀNH CÔNG BẢN ĐỒ VÀ THÁC DUNG NHAM NÚI: HẺM NÚI DUNG NHAM (Volcanic Gorge)!");
        }

        private void EnsureWaterfallMaterial()
        {
            if (_lavaWaterfallMaterial != null) return;

            Material baseMat = Resources.Load<Material>("Materials/Stylize_Lava");
            if (baseMat == null)
            {
                baseMat = Resources.Load<Material>("Stylize_Lava");
            }

            if (baseMat != null)
            {
                _lavaWaterfallMaterial = new Material(baseMat);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _lavaWaterfallMaterial = new Material(shader);
                _lavaWaterfallMaterial.color = new Color(1f, 0.35f, 0.05f);
                if (_lavaWaterfallMaterial.HasProperty("_EmissionColor"))
                {
                    _lavaWaterfallMaterial.EnableKeyword("_EMISSION");
                    _lavaWaterfallMaterial.SetColor("_EmissionColor", new Color(1.6f, 0.50f, 0.08f));
                }
            }

            _lavaWaterfallMaterial.mainTextureScale = new Vector2(1f, 2.5f);
        }

        /// <summary>
        /// Tạo hệ thống thác dung nham (Mountain Lava Cascades) chảy dọc từ đỉnh các dãy núi đá bao quanh.
        /// Toàn bộ các dòng chảy nằm hoàn toàn trên sườn núi ở phía sau và 2 bên rìa (z <= -48 hoặc |x| >= 33),
        /// tuyệt đối KHÔNG bao phủ, KHÔNG che khuất làng, pháo đài hay bất kỳ NPC nào!
        /// </summary>
        private void BuildMountainLavaWaterfalls()
        {
            if (_vfxRoot != null)
            {
                Destroy(_vfxRoot);
                _vfxRoot = null;
            }

            _vfxRoot = new GameObject("[Prehistoric_Volcanic_Effects]");
            _vfxRoot.transform.position = Vector3.zero;

            // 1. Dòng dung nham trung tâm đỉnh núi sau làng (mountain-desert 4)
            // Bắt đầu từ đỉnh cao (y = 25m, z = -64m), chảy xuống sườn núi và dừng ở chân núi (y = 2.5m, z = -49m)
            CreateLavaWaterfallStrip("Mountain_Stream_Center_Main", new Vector3[]
            {
                new Vector3(0f, 25f, -64f),
                new Vector3(-0.8f, 18f, -60f),
                new Vector3(-1.2f, 11f, -55f),
                new Vector3(-1.5f, 5f, -51f),
                new Vector3(-1.8f, 2.5f, -49f)
            }, 2.4f);

            // 2. Nhánh dung nham phía Đông núi sau làng
            CreateLavaWaterfallStrip("Mountain_Stream_Center_East", new Vector3[]
            {
                new Vector3(7.5f, 23f, -62f),
                new Vector3(8.5f, 16f, -58f),
                new Vector3(9.8f, 10f, -53f),
                new Vector3(10.8f, 4.5f, -49f),
                new Vector3(11.5f, 2.5f, -48f)
            }, 2.0f);

            // 3. Dòng dung nham đỉnh núi phía Tây Nam (mountain-desert 3)
            CreateLavaWaterfallStrip("Mountain_Stream_West_Peak", new Vector3[]
            {
                new Vector3(-28f, 24f, -62f),
                new Vector3(-26f, 17f, -58f),
                new Vector3(-24f, 11f, -54f),
                new Vector3(-22.5f, 5.5f, -50f),
                new Vector3(-21f, 2.5f, -48f)
            }, 2.2f);

            // 4. Dòng dung nham vách núi sườn Tây (mountain-desert) - nằm tít rìa trái map (x = -42 đến -33)
            CreateLavaWaterfallStrip("Mountain_Stream_West_Flank", new Vector3[]
            {
                new Vector3(-42f, 21f, -32f),
                new Vector3(-39f, 15f, -28f),
                new Vector3(-36.5f, 9f, -23f),
                new Vector3(-34.5f, 4.5f, -19f),
                new Vector3(-33f, 2.0f, -17f)
            }, 2.0f);

            // 5. Dòng dung nham đỉnh núi phía Đông Nam (mountain-desert 2)
            CreateLavaWaterfallStrip("Mountain_Stream_East_Peak", new Vector3[]
            {
                new Vector3(28f, 24f, -55f),
                new Vector3(26f, 17f, -51f),
                new Vector3(24f, 11f, -47f),
                new Vector3(22.5f, 5.5f, -44f),
                new Vector3(21.5f, 2.5f, -43f)
            }, 2.2f);

            // 6. Dòng dung nham vách núi sườn Đông (mountain-desert 1) - nằm tít rìa phải map (x = 42 đến 33)
            CreateLavaWaterfallStrip("Mountain_Stream_East_Flank", new Vector3[]
            {
                new Vector3(42f, 21f, -32f),
                new Vector3(39f, 15f, -28f),
                new Vector3(36.5f, 9f, -23f),
                new Vector3(34.5f, 4.5f, -19f),
                new Vector3(33f, 2.0f, -17f)
            }, 2.0f);

            // 7. Dòng dung nham vách núi Bắc phía quái xuất hiện (North Canyon Crest)
            CreateLavaWaterfallStrip("Mountain_Stream_North_Crest", new Vector3[]
            {
                new Vector3(0f, 18f, 56f),
                new Vector3(0f, 12f, 51f),
                new Vector3(0f, 6f, 45f),
                new Vector3(0f, 1.5f, 40f)
            }, 2.4f);
        }

        private GameObject CreateLavaWaterfallStrip(string name, Vector3[] points, float width)
        {
            if (points == null || points.Length < 2) return null;

            GameObject stripGo = new GameObject(name);
            stripGo.transform.SetParent(_vfxRoot.transform);
            stripGo.transform.position = Vector3.zero;

            MeshFilter mf = stripGo.AddComponent<MeshFilter>();
            MeshRenderer mr = stripGo.AddComponent<MeshRenderer>();
            mr.material = _lavaWaterfallMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            int pointCount = points.Length;
            int vertCount = pointCount * 2;
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            List<int> triangles = new List<int>();

            // Tính toán tọa độ ôm sát bề mặt vách núi bằng Raycast
            Vector3[] adjustedPoints = new Vector3[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                Vector3 p = points[i];
                if (Physics.Raycast(new Vector3(p.x, 45f, p.z), Vector3.down, out RaycastHit hit, 60f))
                {
                    adjustedPoints[i] = hit.point + hit.normal * 0.12f;
                }
                else
                {
                    adjustedPoints[i] = p;
                }
            }

            float totalDist = 0f;
            float[] dists = new float[pointCount];
            for (int i = 1; i < pointCount; i++)
            {
                totalDist += Vector3.Distance(adjustedPoints[i - 1], adjustedPoints[i]);
                dists[i] = totalDist;
            }

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 dir;
                if (i < pointCount - 1)
                    dir = (adjustedPoints[i + 1] - adjustedPoints[i]).normalized;
                else
                    dir = (adjustedPoints[i] - adjustedPoints[i - 1]).normalized;

                Vector3 right = Vector3.Cross(dir, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f)
                    right = Vector3.Cross(dir, Vector3.forward).normalized;

                float v = (dists[i] / Mathf.Max(totalDist, 1f)) * 3.0f;
                float taper = Mathf.Lerp(width * 0.85f, width * 1.15f, (float)i / (pointCount - 1));

                vertices[i * 2] = adjustedPoints[i] - right * (taper * 0.5f);
                vertices[i * 2 + 1] = adjustedPoints[i] + right * (taper * 0.5f);

                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);

                if (i < pointCount - 1)
                {
                    int baseIdx = i * 2;
                    // Mặt trước
                    triangles.Add(baseIdx);
                    triangles.Add(baseIdx + 2);
                    triangles.Add(baseIdx + 1);

                    triangles.Add(baseIdx + 1);
                    triangles.Add(baseIdx + 2);
                    triangles.Add(baseIdx + 3);

                    // Mặt sau (Double-sided để nhìn rõ từ mọi góc)
                    triangles.Add(baseIdx + 1);
                    triangles.Add(baseIdx + 2);
                    triangles.Add(baseIdx);

                    triangles.Add(baseIdx + 3);
                    triangles.Add(baseIdx + 2);
                    triangles.Add(baseIdx + 1);
                }
            }

            Mesh mesh = new Mesh
            {
                name = $"{name}_Mesh",
                vertices = vertices,
                uv = uvs,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            return stripGo;
        }

        private void BuildMountainLavaLights()
        {
            _lavaLights.Clear();
            _lightBaseIntensities.Clear();

            // Đặt các nguồn sáng cam đỏ ấm áp trên đỉnh các ngọn núi để chiếu sáng sườn đá
            Vector3[] mountainLightPositions = new Vector3[]
            {
                new Vector3(0f, 22f, -60f),         // Đỉnh núi trung tâm sau làng
                new Vector3(-28f, 20f, -58f),       // Đỉnh núi Tây Nam
                new Vector3(28f, 20f, -52f),        // Đỉnh núi Đông Nam
                new Vector3(-40f, 18f, -28f),       // Vách núi sườn Tây
                new Vector3(40f, 18f, -28f),        // Vách núi sườn Đông
                new Vector3(0f, 16f, 52f)           // Đỉnh núi phía Bắc
            };

            for (int i = 0; i < mountainLightPositions.Length; i++)
            {
                GameObject lightGo = new GameObject($"Mountain_Lava_Light_{i + 1}");
                lightGo.transform.SetParent(_vfxRoot.transform);
                lightGo.transform.position = mountainLightPositions[i];

                Light pLight = lightGo.AddComponent<Light>();
                pLight.type = LightType.Point;
                pLight.color = new Color(1.0f, 0.42f, 0.05f); // Cam đỏ nóng chảy
                pLight.range = 22f;
                pLight.intensity = 2.2f;
                pLight.shadows = LightShadows.None;

                _lavaLights.Add(pLight);
                _lightBaseIntensities.Add(2.2f);
            }
        }

        private void BuildVolcanicEmberParticles()
        {
            GameObject psGo = new GameObject("Volcanic_Embers_VFX");
            psGo.transform.SetParent(_vfxRoot.transform);
            psGo.transform.position = new Vector3(0f, 1.0f, 0f);

            ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 5.0f;
            main.startSpeed = 2.0f;
            main.startSize = 0.28f;
            main.startColor = new Color(1f, 0.65f, 0.15f, 0.85f);
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(85f, 2f, 85f);

            var velOverLifetime = ps.velocityOverLifetime;
            velOverLifetime.enabled = true;
            velOverLifetime.y = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            velOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.18f, 0.02f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private void DecorateLavaBoulders()
        {
            GameObject boulderPrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_LavaBoulder");
            if (boulderPrefab == null) return;

            // Đặt các tảng đá dung nham cháy rực rải rác ngoài rìa vách đá xa khu vực làng
            Vector3[] boulderPositions = new Vector3[]
            {
                new Vector3(-44f, 1.2f, 32f),
                new Vector3(-48f, 1.4f, 18f),
                new Vector3(45f, 1.2f, -25f),
                new Vector3(48f, 1.4f, -10f),
                new Vector3(-35f, 1.0f, -46f),
                new Vector3(35f, 1.0f, 44f)
            };

            for (int i = 0; i < boulderPositions.Length; i++)
            {
                GameObject b = Instantiate(boulderPrefab, boulderPositions[i], Quaternion.Euler(UnityEngine.Random.Range(0, 360), UnityEngine.Random.Range(0, 360), 0), _vfxRoot.transform);
                b.name = $"Lava_Boulder_{i + 1}";
                b.transform.localScale = Vector3.one * UnityEngine.Random.Range(2.0f, 3.2f);
            }
        }

        private void Update()
        {
            if (!IsThemeActive) return;

            // 1. Cuộn bề mặt thác dung nham vách núi chảy nhanh từ trên đỉnh núi dội xuống
            if (_lavaWaterfallMaterial != null)
            {
                Vector2 wfOffset = _lavaWaterfallMaterial.mainTextureOffset;
                wfOffset.y -= Time.deltaTime * 0.18f; // Chảy cuồn cuộn xuống vách đá
                wfOffset.x += Time.deltaTime * 0.012f;
                _lavaWaterfallMaterial.mainTextureOffset = wfOffset;
            }

            // 2. Dao động ánh sáng dung nham trên đỉnh núi
            for (int i = 0; i < _lavaLights.Count; i++)
            {
                if (_lavaLights[i] != null)
                {
                    float noise = Mathf.PerlinNoise(Time.time * 2.2f + i * 1.5f, 0f);
                    _lavaLights[i].intensity = _lightBaseIntensities[i] * (0.85f + noise * 0.32f);
                }
            }

            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.deltaTime;
            }
        }

        private void ApplyVolcanicTerrainTint()
        {
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var mr in renderers)
            {
                if (mr == null || mr.sharedMaterial == null) continue;
                string matName = mr.sharedMaterial.name;
                if (matName.Contains("LPTP") || matName.Contains("Ground") || matName.Contains("Terrain") || matName.Contains("Rock") || matName.Contains("Sand"))
                {
                    Material mat = mr.material;
                    if (!_originalMaterialColors.ContainsKey(mat))
                    {
                        Color origColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.color : Color.white);
                        _originalMaterialColors[mat] = origColor;
                    }

                    // Tông màu đá bazan đen & đất tro núi lửa
                    Color volcanicTint = new Color(0.42f, 0.32f, 0.28f, 1f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", volcanicTint);
                    if (mat.HasProperty("_Color")) mat.color = volcanicTint;
                }
            }
        }

        public void RestoreDefaultEnvironment()
        {
            IsThemeActive = false;

            if (_hasSavedOriginals)
            {
                RenderSettings.fog = _originalFog;
                RenderSettings.fogColor = _originalFogColor;
                RenderSettings.fogDensity = _originalFogDensity;
                RenderSettings.ambientSkyColor = _originalAmbientSky;
                RenderSettings.ambientEquatorColor = _originalAmbientEquator;
                RenderSettings.ambientGroundColor = _originalAmbientGround;
            }

            // Khôi phục màu gốc của mặt đất và vách đá
            foreach (var kvp in _originalMaterialColors)
            {
                if (kvp.Key != null)
                {
                    if (kvp.Key.HasProperty("_BaseColor")) kvp.Key.SetColor("_BaseColor", kvp.Value);
                    if (kvp.Key.HasProperty("_Color")) kvp.Key.color = kvp.Value;
                }
            }
            _originalMaterialColors.Clear();

            if (_vfxRoot != null)
            {
                Destroy(_vfxRoot);
                _vfxRoot = null;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (_lavaWaterfallMaterial != null)
            {
                Destroy(_lavaWaterfallMaterial);
            }
        }

        private void OnGUI()
        {
            if (!IsThemeActive) return;

            bool isMenuOpen = UI.PrehistoricMainMenu.Instance != null && UI.PrehistoricMainMenu.Instance.IsOpen;
            if (!isMenuOpen)
            {
                Color prevBg = GUI.backgroundColor;
                Color prevColor = GUI.color;

                // 1. Banner mở đầu hoành tráng trong 6 giây đầu
                if (_bannerTimer > 0f)
                {
                    float alpha = Mathf.Clamp01(_bannerTimer);
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    float bannerW = 620f;
                    float bannerH = 75f;
                    float bannerX = (Screen.width - bannerW) / 2f;
                    float bannerY = 60f;

                    Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);
                    GUI.backgroundColor = new Color(0.28f, 0.06f, 0.02f, 0.92f * alpha);
                    GUI.Box(bannerRect, GUIContent.none);

                    GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 20,
                        fontStyle = FontStyle.Bold,
                        richText = true
                    };
                    titleStyle.normal.textColor = new Color(1f, 0.88f, 0.2f, alpha);

                    GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        richText = true
                    };
                    subStyle.normal.textColor = new Color(1f, 0.82f, 0.70f, alpha);

                    GUI.Label(new Rect(bannerX, bannerY + 8f, bannerW, 30f), "🌋 CHIẾN DỊCH: HẺM NÚI DUNG NHAM (VOLCANIC GORGE)", titleStyle);
                    GUI.Label(new Rect(bannerX, bannerY + 38f, bannerW, 28f), "Dòng nham thạch sôi sục dội từ các ngọn núi! Quái thú hung tợn hơn bao giờ hết!", subStyle);
                }
                else
                {
                    // 2. Huy hiệu Bản đồ cố định hiển thị ở dưới thanh IO CLOUD (Y = 56, không bị che chữ hay đè lên AI Director)
                    float badgeW = 210f;
                    float badgeH = 24f;
                    float badgeX = (Screen.width - badgeW) / 2f;
                    float badgeY = 56f;

                    GUI.backgroundColor = new Color(0.35f, 0.08f, 0.02f, 0.90f);
                    GUI.Box(new Rect(badgeX, badgeY, badgeW, badgeH), GUIContent.none);

                    GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        richText = true
                    };
                    badgeStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
                    GUI.Label(new Rect(badgeX, badgeY, badgeW, badgeH), "🌋 HẺM NÚI DUNG NHAM", badgeStyle);
                }

                GUI.backgroundColor = prevBg;
                GUI.color = prevColor;
            }
        }
    }
}
