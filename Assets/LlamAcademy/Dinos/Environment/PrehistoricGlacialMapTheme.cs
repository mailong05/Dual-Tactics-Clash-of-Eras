using System;
using System.Collections.Generic;
using LlamAcademy.Dinos.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LlamAcademy.Dinos.Environment
{
    /// <summary>
    /// Quản lý hiệu ứng đồ họa, ánh sáng, tuyết rơi và không khí cho Bản đồ 3:
    /// "❄️ Đỉnh Núi Băng Tuyết" (Glacial Peak / Tundra).
    /// Bão tuyết lốc lạnh, thác băng hà (Glacial Cascades) dội từ đỉnh núi đá phủ tuyết,
    /// bầu trời xanh bắc cực lạnh giá và các chỏm băng pha lê vĩnh cửu.
    /// Tuyệt đối KHÔNG che khuất pháo đài làng, căn cứ hay bất kỳ NPC nào phía sau!
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PrehistoricGlacialMapTheme : MonoBehaviour
    {
        public static PrehistoricGlacialMapTheme Instance { get; private set; }

        private GameObject _vfxRoot;
        private Material _glacialWaterfallMaterial;
        private Material _iceCrystalMaterial;
        private Material _snowParticleMaterial;
        private readonly List<Light> _glacialLights = new List<Light>();
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
                GameObject go = new GameObject("[Prehistoric_Glacial_Theme]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<PrehistoricGlacialMapTheme>();
            }
            Instance.ApplyGlacialEnvironment();
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
            if (PrehistoricMapManager.CurrentMap == PrehistoricMapType.GlacialTundra)
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
            if (PrehistoricMapManager.CurrentMap == PrehistoricMapType.GlacialTundra)
            {
                ApplyGlacialEnvironment();
            }
        }

        public void ApplyGlacialEnvironment()
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

            // 2. Thiết lập bầu không khí Bắc cực băng tuyết (Crisp Arctic Winter)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.94f, 1f); // Sương lam tuyết lạnh giá
            RenderSettings.fogDensity = 0.0075f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.86f, 1.0f, 1f);      // Xanh lam bầu trời bắc cực
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.64f, 0.78f, 1f);  // Lam xám vách núi tuyết
            RenderSettings.ambientGroundColor = new Color(0.78f, 0.86f, 0.94f, 1f);   // Ánh phản quang tuyết trắng

            // 3. Chỉnh Sun Directional Light sang ánh nắng mùa đông chói rực rỡ
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    l.color = new Color(0.92f, 0.96f, 1.0f);
                    l.intensity = 1.25f;
                    break;
                }
            }

            // 4. Khởi tạo Material và hệ thống Thác Băng Hà bám sát vách núi
            EnsureMaterials();
            BuildMountainGlacialCascades();
            BuildMountainGlacialLights();
            BuildSnowfallParticles();
            DecorateMountainIceSpires();
            ApplyGlacialTerrainTint();

            Debug.Log("<color=cyan>[Prehistoric TD]</color> ❄️ ĐÃ KÍCH HOẠT THÀNH CÔNG BẢN ĐỒ BĂNG TUYẾT: ĐỈNH NÚI BĂNG TUYẾT (Glacial Peak)!");
        }

        private void EnsureMaterials()
        {
            // 1. Material cho dải thác băng hà chảy cuộn từ sườn núi
            if (_glacialWaterfallMaterial == null)
            {
                Material baseMat = Resources.Load<Material>("Materials/Glacial_Ice");
                if (baseMat == null)
                {
                    baseMat = Resources.Load<Material>("Materials/Ice");
                }

                if (baseMat != null)
                {
                    _glacialWaterfallMaterial = new Material(baseMat);
                }
                else
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    _glacialWaterfallMaterial = new Material(shader);
                    _glacialWaterfallMaterial.color = new Color(0.58f, 0.84f, 1.0f, 0.92f);
                    if (_glacialWaterfallMaterial.HasProperty("_BaseColor"))
                    {
                        _glacialWaterfallMaterial.SetColor("_BaseColor", new Color(0.58f, 0.84f, 1.0f, 0.92f));
                    }
                    if (_glacialWaterfallMaterial.HasProperty("_Smoothness"))
                    {
                        _glacialWaterfallMaterial.SetFloat("_Smoothness", 0.94f);
                    }
                    if (_glacialWaterfallMaterial.HasProperty("_Metallic"))
                    {
                        _glacialWaterfallMaterial.SetFloat("_Metallic", 0.15f);
                    }
                    if (_glacialWaterfallMaterial.HasProperty("_EmissionColor"))
                    {
                        _glacialWaterfallMaterial.EnableKeyword("_EMISSION");
                        _glacialWaterfallMaterial.SetColor("_EmissionColor", new Color(0.20f, 0.58f, 1.15f));
                    }
                }

                _glacialWaterfallMaterial.mainTextureScale = new Vector2(1f, 3.0f);
            }

            // 2. Material cho các tinh thể băng pha lê trên đỉnh núi
            if (_iceCrystalMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _iceCrystalMaterial = new Material(shader);
                _iceCrystalMaterial.color = new Color(0.40f, 0.82f, 1.0f, 0.95f);
                if (_iceCrystalMaterial.HasProperty("_BaseColor"))
                {
                    _iceCrystalMaterial.SetColor("_BaseColor", new Color(0.40f, 0.82f, 1.0f, 0.95f));
                }
                if (_iceCrystalMaterial.HasProperty("_Smoothness"))
                {
                    _iceCrystalMaterial.SetFloat("_Smoothness", 0.96f);
                }
                if (_iceCrystalMaterial.HasProperty("_EmissionColor"))
                {
                    _iceCrystalMaterial.EnableKeyword("_EMISSION");
                    _iceCrystalMaterial.SetColor("_EmissionColor", new Color(0.25f, 0.70f, 1.35f));
                }
            }
        }

        /// <summary>
        /// Tạo hệ thống thác băng hà (Glacial Cascades) đổ dọc từ đỉnh các dãy núi đá bao quanh.
        /// Toàn bộ các dòng chảy nằm hoàn toàn trên sườn núi ở phía sau và 2 bên rìa (z <= -48 hoặc |x| >= 33),
        /// tuyệt đối KHÔNG bao phủ, KHÔNG che khuất làng, pháo đài hay bất kỳ NPC nào!
        /// </summary>
        private void BuildMountainGlacialCascades()
        {
            if (_vfxRoot != null)
            {
                Destroy(_vfxRoot);
                _vfxRoot = null;
            }

            _vfxRoot = new GameObject("[Prehistoric_Glacial_Effects]");
            _vfxRoot.transform.position = Vector3.zero;

            // 1. Thác băng trung tâm đỉnh núi sau làng (mountain-desert 4)
            CreateGlacialWaterfallStrip("Glacial_Stream_Center_Main", new Vector3[]
            {
                new Vector3(0f, 25f, -64f),
                new Vector3(-0.8f, 18f, -60f),
                new Vector3(-1.2f, 11f, -55f),
                new Vector3(-1.5f, 5f, -51f),
                new Vector3(-1.8f, 2.5f, -49f)
            }, 2.4f);

            // 2. Nhánh thác băng phía Đông núi sau làng
            CreateGlacialWaterfallStrip("Glacial_Stream_Center_East", new Vector3[]
            {
                new Vector3(7.5f, 23f, -62f),
                new Vector3(8.5f, 16f, -58f),
                new Vector3(9.8f, 10f, -53f),
                new Vector3(10.8f, 4.5f, -49f),
                new Vector3(11.5f, 2.5f, -48f)
            }, 2.0f);

            // 3. Dòng thác băng đỉnh núi phía Tây Nam (mountain-desert 3)
            CreateGlacialWaterfallStrip("Glacial_Stream_West_Peak", new Vector3[]
            {
                new Vector3(-28f, 24f, -62f),
                new Vector3(-26f, 17f, -58f),
                new Vector3(-24f, 11f, -54f),
                new Vector3(-22.5f, 5.5f, -50f),
                new Vector3(-21f, 2.5f, -48f)
            }, 2.2f);

            // 4. Dòng thác băng vách núi sườn Tây (mountain-desert) - rìa trái ngoài map (x = -42 đến -33)
            CreateGlacialWaterfallStrip("Glacial_Stream_West_Flank", new Vector3[]
            {
                new Vector3(-42f, 21f, -32f),
                new Vector3(-39f, 15f, -28f),
                new Vector3(-36.5f, 9f, -23f),
                new Vector3(-34.5f, 4.5f, -19f),
                new Vector3(-33f, 2.0f, -17f)
            }, 2.0f);

            // 5. Dòng thác băng đỉnh núi phía Đông Nam (mountain-desert 2)
            CreateGlacialWaterfallStrip("Glacial_Stream_East_Peak", new Vector3[]
            {
                new Vector3(28f, 24f, -55f),
                new Vector3(26f, 17f, -51f),
                new Vector3(24f, 11f, -47f),
                new Vector3(22.5f, 5.5f, -44f),
                new Vector3(21.5f, 2.5f, -43f)
            }, 2.2f);

            // 6. Dòng thác băng vách núi sườn Đông (mountain-desert 1) - rìa phải ngoài map (x = 42 đến 33)
            CreateGlacialWaterfallStrip("Glacial_Stream_East_Flank", new Vector3[]
            {
                new Vector3(42f, 21f, -32f),
                new Vector3(39f, 15f, -28f),
                new Vector3(36.5f, 9f, -23f),
                new Vector3(34.5f, 4.5f, -19f),
                new Vector3(33f, 2.0f, -17f)
            }, 2.0f);

            // 7. Dòng thác băng vách núi Bắc cửa quái xuất hiện
            CreateGlacialWaterfallStrip("Glacial_Stream_North_Crest", new Vector3[]
            {
                new Vector3(0f, 18f, 56f),
                new Vector3(0f, 12f, 51f),
                new Vector3(0f, 6f, 45f),
                new Vector3(0f, 1.5f, 40f)
            }, 2.4f);
        }

        private GameObject CreateGlacialWaterfallStrip(string name, Vector3[] points, float width)
        {
            if (points == null || points.Length < 2) return null;

            GameObject stripGo = new GameObject(name);
            stripGo.transform.SetParent(_vfxRoot.transform);
            stripGo.transform.position = Vector3.zero;

            MeshFilter mf = stripGo.AddComponent<MeshFilter>();
            MeshRenderer mr = stripGo.AddComponent<MeshRenderer>();
            mr.material = _glacialWaterfallMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            int pointCount = points.Length;
            int vertCount = pointCount * 2;
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            List<int> triangles = new List<int>();

            // Raycast ôm sát bề mặt vách núi đá
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

                    // Mặt sau
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

        private void BuildMountainGlacialLights()
        {
            _glacialLights.Clear();
            _lightBaseIntensities.Clear();

            // Nguồn sáng xanh băng tuyết lung linh trên đỉnh núi đá
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
                GameObject lightGo = new GameObject($"Glacial_Lava_Light_{i + 1}");
                lightGo.transform.SetParent(_vfxRoot.transform);
                lightGo.transform.position = mountainLightPositions[i];

                Light pLight = lightGo.AddComponent<Light>();
                pLight.type = LightType.Point;
                pLight.color = new Color(0.35f, 0.75f, 1.0f); // Xanh lam ngọc băng tuyết
                pLight.range = 24f;
                pLight.intensity = 2.0f;
                pLight.shadows = LightShadows.None;

                _glacialLights.Add(pLight);
                _lightBaseIntensities.Add(2.0f);
            }
        }

        private Material GetSnowParticleMaterial()
        {
            if (_snowParticleMaterial != null) return _snowParticleMaterial;

            Material baseMat = Resources.Load<Material>("Materials/Particle_SnowFlurry");
            if (baseMat == null)
            {
                baseMat = Resources.Load<Material>("Materials/Trail_LavaFire");
            }

            if (baseMat != null)
            {
                _snowParticleMaterial = new Material(baseMat);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit")
                             ?? Shader.Find("Particles/Standard Unlit")
                             ?? Shader.Find("Sprites/Default");
                _snowParticleMaterial = new Material(shader);
                _snowParticleMaterial.color = new Color(0.92f, 0.96f, 1.0f, 0.90f);
                if (_snowParticleMaterial.HasProperty("_BaseColor")) _snowParticleMaterial.SetColor("_BaseColor", new Color(0.92f, 0.96f, 1.0f, 0.90f));
                if (_snowParticleMaterial.HasProperty("_EmissionColor"))
                {
                    _snowParticleMaterial.EnableKeyword("_EMISSION");
                    _snowParticleMaterial.SetColor("_EmissionColor", new Color(0.5f, 0.85f, 1.2f));
                }
            }
            return _snowParticleMaterial;
        }

        /// <summary>
        /// Tạo hiệu ứng bão tuyết rơi rải rác khắp toàn thung lũng (Snow Flurry / Blizzard)
        /// </summary>
        private void BuildSnowfallParticles()
        {
            GameObject psGo = new GameObject("Glacial_Snowfall_VFX");
            psGo.transform.SetParent(_vfxRoot.transform);
            psGo.transform.position = new Vector3(0f, 22.0f, 0f);

            ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 7.0f;
            main.startSpeed = 2.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new Color(0.92f, 0.96f, 1.0f, 0.90f);
            main.maxParticles = 650;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 85f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(90f, 4f, 90f);

            var velOverLifetime = ps.velocityOverLifetime;
            velOverLifetime.enabled = true;
            velOverLifetime.y = new ParticleSystem.MinMaxCurve(-3.5f, -2.0f); // Tuyết rơi từ trên trời xuống
            velOverLifetime.x = new ParticleSystem.MinMaxCurve(-1.2f, 0.4f);  // Gió bấc thổi nhẹ theo chiều ngang
            velOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.0f, 0.4f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.15f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetSnowParticleMaterial();
        }

        /// <summary>
        /// Tạo các khối trụ tinh thể băng pha lê khổng lồ ngự trên các chóp núi cao
        /// (Nằm hoàn toàn ở các đỉnh núi xa, tuyệt đối không chạm vào làng hay đường đi)
        /// </summary>
        private void DecorateMountainIceSpires()
        {
            Vector3[] spirePositions = new Vector3[]
            {
                new Vector3(-4f, 26f, -65f),
                new Vector3(5f, 24f, -63f),
                new Vector3(-30f, 25f, -63f),
                new Vector3(30f, 24f, -56f),
                new Vector3(-43f, 22f, -31f),
                new Vector3(43f, 22f, -31f),
                new Vector3(-3f, 19f, 57f),
                new Vector3(3f, 19f, 57f)
            };

            for (int i = 0; i < spirePositions.Length; i++)
            {
                GameObject spireGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spireGo.name = $"Glacial_Ice_Spire_{i + 1}";
                spireGo.transform.SetParent(_vfxRoot.transform);
                spireGo.transform.position = spirePositions[i];
                spireGo.transform.localScale = new Vector3(1.4f, UnityEngine.Random.Range(4.5f, 7.5f), 1.4f);
                spireGo.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-12f, 12f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-12f, 12f));

                // Bỏ collider để không ảnh hưởng physics/navmesh
                Collider col = spireGo.GetComponent<Collider>();
                if (col != null) Destroy(col);

                MeshRenderer mr = spireGo.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.material = _iceCrystalMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        private void Update()
        {
            if (!IsThemeActive) return;

            // 1. Cuộn bề mặt thác băng hà lấp lánh chảy chậm rãi trên vách núi
            if (_glacialWaterfallMaterial != null)
            {
                Vector2 wfOffset = _glacialWaterfallMaterial.mainTextureOffset;
                wfOffset.y -= Time.deltaTime * 0.08f;
                wfOffset.x += Mathf.Sin(Time.time * 0.5f) * 0.003f;
                _glacialWaterfallMaterial.mainTextureOffset = wfOffset;
            }

            // 2. Dao động ánh sáng băng tuyết lung linh trên đỉnh núi
            for (int i = 0; i < _glacialLights.Count; i++)
            {
                if (_glacialLights[i] != null)
                {
                    float noise = Mathf.PerlinNoise(Time.time * 1.5f + i * 1.2f, 0f);
                    _glacialLights[i].intensity = _lightBaseIntensities[i] * (0.88f + noise * 0.28f);
                }
            }

            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.deltaTime;
            }
        }

        private void ApplyGlacialTerrainTint()
        {
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var mr in renderers)
            {
                if (mr == null || mr.sharedMaterial == null) continue;
                string matName = mr.sharedMaterial.name;
                if (matName.Contains("LPTP") || matName.Contains("Ground") || matName.Contains("Terrain") || matName.Contains("Rock") || matName.Contains("Sand") || matName.Contains("Path"))
                {
                    Material mat = mr.material;
                    if (!_originalMaterialColors.ContainsKey(mat))
                    {
                        Color origColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.color : Color.white);
                        _originalMaterialColors[mat] = origColor;
                    }

                    // Tông màu tuyết trắng phủ sương giá Bắc Cực
                    Color winterTint = matName.Contains("Rock") 
                        ? new Color(0.70f, 0.82f, 0.92f, 1f)  // Đá phủ sương băng
                        : new Color(0.85f, 0.92f, 0.98f, 1f); // Mặt đất phủ tuyết trắng

                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", winterTint);
                    if (mat.HasProperty("_Color")) mat.color = winterTint;
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
            if (_glacialWaterfallMaterial != null)
            {
                Destroy(_glacialWaterfallMaterial);
            }
            if (_iceCrystalMaterial != null)
            {
                Destroy(_iceCrystalMaterial);
            }
            if (_snowParticleMaterial != null)
            {
                Destroy(_snowParticleMaterial);
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
                    GUI.backgroundColor = new Color(0.08f, 0.22f, 0.38f, 0.92f * alpha);
                    GUI.Box(bannerRect, GUIContent.none);

                    GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 20,
                        fontStyle = FontStyle.Bold,
                        richText = true
                    };
                    titleStyle.normal.textColor = new Color(0.85f, 0.95f, 1.0f, alpha);

                    GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        richText = true
                    };
                    subStyle.normal.textColor = new Color(0.75f, 0.88f, 1.0f, alpha);

                    GUI.Label(new Rect(bannerX, bannerY + 8f, bannerW, 30f), "❄️ CHIẾN DỊCH: ĐỈNH NÚI BĂNG TUYẾT (GLACIAL PEAK)", titleStyle);
                    GUI.Label(new Rect(bannerX, bannerY + 38f, bannerW, 28f), "Bão tuyết lạnh giá bao phủ thung lũng! Thác băng hà đổ xuống từ vách núi vĩnh cửu!", subStyle);
                }
                else
                {
                    // 2. Huy hiệu Bản đồ cố định hiển thị ở dưới thanh IO CLOUD (Y = 56)
                    float badgeW = 210f;
                    float badgeH = 24f;
                    float badgeX = (Screen.width - badgeW) / 2f;
                    float badgeY = 56f;

                    GUI.backgroundColor = new Color(0.10f, 0.25f, 0.42f, 0.90f);
                    GUI.Box(new Rect(badgeX, badgeY, badgeW, badgeH), GUIContent.none);

                    GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        richText = true
                    };
                    badgeStyle.normal.textColor = new Color(0.80f, 0.95f, 1.0f);
                    GUI.Label(new Rect(badgeX, badgeY, badgeW, badgeH), "❄️ ĐỈNH NÚI BĂNG TUYẾT", badgeStyle);
                }

                GUI.backgroundColor = prevBg;
                GUI.color = prevColor;
            }
        }
    }
}
