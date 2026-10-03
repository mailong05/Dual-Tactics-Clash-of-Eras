using System.Collections.Generic;
using System.IO;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Enemy;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Unit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.Editor
{
    [InitializeOnLoad]
    public class PrehistoricSetupTool : UnityEditor.Editor
    {
        private const string PREFAB_DIR = "Assets/Prefabs/Prehistoric";
        private const string CONFIG_DIR = "Assets/Config/Prehistoric";

        public enum DimensionMode
        {
            MaxExtent,       // Scale so Mathf.Max(size.x, size.y, size.z) == target
            Height,          // Scale so size.y == target
            HorizontalWidth  // Scale so Mathf.Max(size.x, size.z) == target
        }

        [InitializeOnLoadMethod]
        private static void AutoSetupOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;

                if (SceneManager.GetActiveScene().isLoaded)
                {
                    bool needsFix = false;

                    // 1. Check if dinosaurs are lying down (Euler X ~ 270 / -90 deg)
                    string[] dinoPrefabNames = new string[] { "Prefab_Velociraptor", "Prefab_Pterodactyl", "Prefab_Ankylosaurus", "Prefab_TRexBoss" };
                    foreach (string dpName in dinoPrefabNames)
                    {
                        string pPath = $"{PREFAB_DIR}/{dpName}.prefab";
                        if (File.Exists(pPath))
                        {
                            GameObject prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                            if (prefabObj != null)
                            {
                                Transform modelChild = prefabObj.transform.Find("Model");
                                // If Model child is at Euler X ~ 270 (-90 deg), it is lying down horizontally!
                                if (modelChild != null && Mathf.Abs(modelChild.localEulerAngles.x - 270f) < 15f)
                                {
                                    needsFix = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            needsFix = true;
                            break;
                        }
                    }

                    // 2. Check starter defense models
                    GameObject starter = GameObject.Find("Starter_Defenses");
                    if (starter == null)
                    {
                        needsFix = true;
                    }
                    else
                    {
                        Renderer[] rList = starter.GetComponentsInChildren<Renderer>();
                        foreach (Renderer r in rList)
                        {
                            if (r != null && (r.bounds.size.x > 7.5f || r.bounds.size.y > 7.5f || r.bounds.size.z > 7.5f))
                            {
                                needsFix = true;
                                break;
                            }
                        }
                    }

                    // 3. Check if Prefab_Watchtower is missing upright rotation (-90 on X)
                    string wtPath = $"{PREFAB_DIR}/Prefab_Watchtower.prefab";
                    if (File.Exists(wtPath))
                    {
                        GameObject prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(wtPath);
                        if (prefabObj != null)
                        {
                            Transform modelChild = prefabObj.transform.Find("Model");
                            if (modelChild == null || Mathf.Abs(modelChild.localEulerAngles.x) < 5f)
                            {
                                needsFix = true;
                            }
                        }
                    }
                    else
                    {
                        needsFix = true;
                    }

                    // 4. Check if PrehistoricGameModeManager is missing
                    if (Object.FindFirstObjectByType<PrehistoricGameModeManager>() == null)
                    {
                        needsFix = true;
                    }

                    // 5. Check if any TowerSO has a missing/null Prefab reference (e.g. Spike Trap)
                    string[] towerCheckNames = new string[] { "Tower_Watchtower", "Tower_Ballista", "Tower_Catapult", "Tower_ShamanTotem", "Tower_TarPit", "Tower_SpikeTrap", "Tower_Barricade" };
                    foreach (string tName in towerCheckNames)
                    {
                        string soPath = $"{CONFIG_DIR}/{tName}.asset";
                        TowerSO so = AssetDatabase.LoadAssetAtPath<TowerSO>(soPath);
                        if (so == null || so.Prefab == null)
                        {
                            needsFix = true;
                            break;
                        }
                    }

                    if (needsFix)
                    {
                        Debug.Log("<color=yellow>[Prehistoric TD]</color> Detected uncalibrated models, missing Dual-Mode Manager, or unassigned tower prefabs. Auto-configuring now...");
                        FixAllModelScales(false);
                    }
                }
            };
        }

        [MenuItem("Tools/Prehistoric TD/Fix All Model Scales & Stand Upright (1-Click)", priority = 0)]
        public static void MenuFixAllScales()
        {
            FixAllModelScales(true);
        }

        [MenuItem("Tools/Prehistoric TD/Setup Everything (1-Click)", priority = 1)]
        public static void MenuSetupEverything()
        {
            FixAllModelScales(true);
        }

        public static void DiagnoseAllDinos()
        {
            try
            {
                string[] dinoPaths = new string[]
                {
                    "Assets/velociraptor.glb",
                    "Assets/pterodactyl_1.glb",
                    "Assets/ankylosaurus_updated.glb",
                    "Assets/animated_t-rex_dinosaur_biting_attack_loop.glb"
                };

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("=== DINO DIAGNOSTICS REPORT ===");

                foreach (string path in dinoPaths)
                {
                    sb.AppendLine($"\n--- MODEL: {path} ---");
                    GameObject glb = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (glb == null)
                    {
                        sb.AppendLine("  ERROR: Asset not found!");
                        continue;
                    }

                    GameObject testRoot = new GameObject("Diag_" + Path.GetFileNameWithoutExtension(path));
                    GameObject testVisual = (GameObject)PrefabUtility.InstantiatePrefab(glb, testRoot.transform);
                    testVisual.transform.localPosition = Vector3.zero;
                    testVisual.transform.localRotation = Quaternion.identity;
                    testVisual.transform.localScale = Vector3.one;

                    Transform head = FindBoneByKeywords(testVisual.transform, "head", "neck", "jaw", "skull", "snout", "mouth", "beak");
                    Transform hips = FindBoneByKeywords(testVisual.transform, "hip", "pelvis", "spine", "root", "body");
                    Transform foot = FindBoneByKeywords(testVisual.transform, "foot", "toe", "leg", "claw");

                    if (head != null && hips != null)
                    {
                        Vector3 forwardVec = head.position - hips.position;
                        sb.AppendLine($"  Head Bone: '{head.name}' at {head.position}, Hips: '{hips.name}' at {hips.position}");
                        sb.AppendLine($"  Forward Direction Vector: {forwardVec}");
                        sb.AppendLine($"  Computed Rotation: {GetDinoRotation(path)}");
                    }
                    else
                    {
                        sb.AppendLine("  No recognizable head/hips bones found. Defaulting to Vector3.zero.");
                    }

                    if (foot != null && head != null)
                    {
                        sb.AppendLine($"  Foot Bone: '{foot.name}' at {foot.position}, Y diff (Head - Foot): {head.position.y - foot.position.y}");
                    }

                    Bounds bIdentity = CalculateAccurateBounds(testVisual);
                    sb.AppendLine($"  Identity Bounds: Size=({bIdentity.size.x:F2}w, {bIdentity.size.y:F2}h, {bIdentity.size.z:F2}d), Min=({bIdentity.min.x:F2}, {bIdentity.min.y:F2}, {bIdentity.min.z:F2})");

                    testVisual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    Bounds bMinus90 = CalculateAccurateBounds(testVisual);
                    sb.AppendLine($"  -90 deg X Bounds: Size=({bMinus90.size.x:F2}w, {bMinus90.size.y:F2}h, {bMinus90.size.z:F2}d), Min=({bMinus90.min.x:F2}, {bMinus90.min.y:F2}, {bMinus90.min.z:F2})");

                    DestroyImmediate(testRoot);
                }

                File.WriteAllText("Assets/dino_diagnostics.txt", sb.ToString());
                Debug.Log("<color=cyan>[Prehistoric TD]</color> Wrote dino diagnostics to Assets/dino_diagnostics.txt");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Prehistoric TD] Diagnostics encountered an error: {ex.Message}");
            }
        }

        public static void FixAllModelScales(bool showDialog = true)
        {
            Debug.Log("<color=green>[Prehistoric TD]</color> Starting Universal 3D Model Scale & Upright Orientation Calibration...");

            DiagnoseAllDinos();
            EnsureDirectories();
            ReimportGLBModels();

            Dictionary<string, GameObject> prefabs = CreateAllPrefabs();
            List<TowerSO> towerSOs = CreateTowerDataAssets(prefabs);
            List<DinoSO> dinoSOs = CreateDinoDataAssets(prefabs);

            SetupSceneManagers(towerSOs, dinoSOs, prefabs);
            PlaceStarterDefenses(prefabs, towerSOs);
            SetupInGameHUD(towerSOs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Debug.Log("<color=green>[Prehistoric TD]</color> ALL 10 MODELS & PREFABS STOOD UPRIGHT & SCALED PERFECTLY!");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Đã Dựng Đứng & Chuẩn Hóa Tất Cả Mô Hình Khủng Long & Công Trình!",
                    "Toàn bộ 10 mô hình 3D thời tiền sử đã được xoay đứng thẳng và tiếp đất chính xác:\n\n" +
                    "• Khủng Long Velociraptor: Đứng thẳng trên 2 chân sau, dài 2.6m\n" +
                    "• Khủng Long Ankylosaurus: Đứng vững 4 chân, giáp lưng hướng lên trời, dài 3.6m\n" +
                    "• Boss T-Rex Khổng Lồ: Đứng thẳng hùng vĩ, cao/dài 6.5m\n" +
                    "• Khủng Long Bay Pterodactyl: Bay lượn cân bằng, sải cánh 3.2m\n\n" +
                    "• Chòi Cung Thủ Gỗ (Watchtower): Đứng thẳng uy nghi, cao 5.8m\n" +
                    "• Cột Vật Tổ Sét (Shaman Totem): Dựng đứng, cao 4.2m\n" +
                    "• Hố Chông Gai Gỗ (Spike Trap): Chông chĩa thẳng lên trời, rộng 2.4m x 2.4m\n" +
                    "• Tháp Nỏ Bắn Lao (Ballista): Đứng vững trên 3 chân, rộng 2.5m, cao 2.0m\n" +
                    "• Máy Bắn Đá Lửa (Catapult): Cần phóng hướng lên, dài 3.2m, cao 2.4m\n" +
                    "• Rào Cọc Gỗ Cản Đường (Barricade): Cọc nhọn dựng đứng chắn đường, ngang 3.0m\n\n" +
                    "Hỗ trợ 2 chế độ chơi độc lập: Thủ Thành (Tower Defense) & Khủng Long Tấn Công (Dino Assault) với menu đổi chế độ [⚙️ ĐỔI CHẾ ĐỘ] (Phím M / F1).",
                    "Tuyệt vời!");
            }
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PREFAB_DIR)) Directory.CreateDirectory(PREFAB_DIR);
            if (!Directory.Exists(CONFIG_DIR)) Directory.CreateDirectory(CONFIG_DIR);
            AssetDatabase.Refresh();
        }

        private static void ReimportGLBModels()
        {
            string[] glbFiles = Directory.GetFiles("Assets", "*.glb", SearchOption.TopDirectoryOnly);
            foreach (string glb in glbFiles)
            {
                AssetDatabase.ImportAsset(glb.Replace("\\", "/"), ImportAssetOptions.ForceUpdate);
            }
        }

        private static Dictionary<string, GameObject> CreateAllPrefabs()
        {
            Dictionary<string, GameObject> createdPrefabs = new();

            // 1. Chòi Cung Thủ Gỗ (Watchtower - Height 5.8m, Upright -90 deg X)
            createdPrefabs["Watchtower"] = CreateOrUpdatePrefab(
                "Assets/wooden_watchtower_lvl_1-3.glb",
                $"{PREFAB_DIR}/Prefab_Watchtower.prefab",
                5.8f,
                DimensionMode.Height,
                go =>
                {
                    if (!go.TryGetComponent(out ArcherTower _)) go.AddComponent<ArcherTower>();
                    EnsureBoxCollider(go, new Vector3(2.6f, 5.8f, 2.6f), new Vector3(0, 2.9f, 0));
                });

            // 2. Tháp Nỏ Bắn Lao (Ballista - Max Extent 2.5m, Upright -90 deg X)
            createdPrefabs["Ballista"] = CreateOrUpdatePrefab(
                "Assets/ballista.glb",
                $"{PREFAB_DIR}/Prefab_Ballista.prefab",
                2.5f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out BallistaTower _)) go.AddComponent<BallistaTower>();
                    EnsureBoxCollider(go, new Vector3(2.2f, 2.0f, 2.5f), new Vector3(0, 1.0f, 0));
                });

            // 3. Máy Bắn Đá Lửa (Catapult - Max Extent 3.2m, Upright -90 deg X)
            createdPrefabs["Catapult"] = CreateOrUpdatePrefab(
                "Assets/free__catapult.glb",
                $"{PREFAB_DIR}/Prefab_Catapult.prefab",
                3.2f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out CatapultTower _)) go.AddComponent<CatapultTower>();
                    EnsureBoxCollider(go, new Vector3(2.6f, 2.4f, 3.2f), new Vector3(0, 1.2f, 0));
                });

            // 4. Cột Vật Tổ Sét (Shaman Totem - Height 4.2m, Upright -90 deg X)
            createdPrefabs["ShamanTotem"] = CreateOrUpdatePrefab(
                "Assets/highmountain_tauren_shaman_totem.glb",
                $"{PREFAB_DIR}/Prefab_ShamanTotem.prefab",
                4.2f,
                DimensionMode.Height,
                go =>
                {
                    if (!go.TryGetComponent(out TeslaTower _)) go.AddComponent<TeslaTower>();
                    EnsureBoxCollider(go, new Vector3(1.6f, 4.2f, 1.6f), new Vector3(0, 2.1f, 0));

                    Transform fp = go.transform.Find("FirePoint");
                    if (fp == null)
                    {
                        GameObject fpGO = new GameObject("FirePoint");
                        fpGO.transform.SetParent(go.transform);
                        fp = fpGO.transform;
                    }
                    fp.localPosition = new Vector3(0, 3.9f, 0);
                });

            // 5. Vũng Hắc Ín (Tar Pit Trap - Procedural)
            createdPrefabs["TarPit"] = CreateOrUpdateTarPitPrefab($"{PREFAB_DIR}/Prefab_TarPit.prefab");

            // 6. Hố Chông Gai Gỗ (Spike Trap - Ground Width 2.4m, Upright -90 deg X so spikes point UP)
            createdPrefabs["SpikeTrap"] = CreateOrUpdatePrefab(
                "Assets/wooden_spike_trap.glb",
                $"{PREFAB_DIR}/Prefab_SpikeTrap.prefab",
                2.4f,
                DimensionMode.HorizontalWidth,
                go =>
                {
                    BoxCollider col = EnsureBoxCollider(go, new Vector3(2.4f, 0.4f, 2.4f), new Vector3(0, 0.2f, 0));
                    col.isTrigger = true;
                    if (!go.TryGetComponent(out GroundTrap _)) go.AddComponent<GroundTrap>();
                });

            // 7. Rào Cọc Gỗ Cản Đường (Barricade - Width across road 3.0m, Upright -90 deg X)
            createdPrefabs["Barricade"] = CreateOrUpdatePrefab(
                "Assets/wooden__barricade_low.glb",
                $"{PREFAB_DIR}/Prefab_Barricade.prefab",
                3.0f,
                DimensionMode.HorizontalWidth,
                go =>
                {
                    if (!go.TryGetComponent(out Wall _)) go.AddComponent<Wall>();
                    if (!go.TryGetComponent(out NavMeshObstacle obs))
                    {
                        obs = go.AddComponent<NavMeshObstacle>();
                    }
                    obs.carving = true;
                    obs.size = new Vector3(3.0f, 1.4f, 1.2f);
                    obs.center = new Vector3(0, 0.7f, 0);
                    EnsureBoxCollider(go, new Vector3(3.0f, 1.4f, 1.2f), new Vector3(0, 0.7f, 0));
                });

            // 8. Khủng Long Bay Pterodactyl (Wingspan 3.2m, Natural Upright Y-up)
            createdPrefabs["Pterodactyl"] = CreateOrUpdatePrefab(
                "Assets/pterodactyl_1.glb",
                $"{PREFAB_DIR}/Prefab_Pterodactyl.prefab",
                3.2f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out FlyingUnit _)) go.AddComponent<FlyingUnit>();
                    EnsureCapsuleCollider(go, 1.2f, 2.0f);
                },
                GetDinoRotation("Assets/pterodactyl_1.glb"));

            // 9. Khủng Long Chạy Nhanh Velociraptor (Length 2.6m, Natural Upright Y-up)
            createdPrefabs["Velociraptor"] = CreateOrUpdatePrefab(
                "Assets/velociraptor.glb",
                $"{PREFAB_DIR}/Prefab_Velociraptor.prefab",
                2.6f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out RunnerDino _)) go.AddComponent<RunnerDino>();
                    EnsureCapsuleCollider(go, 0.8f, 1.8f);
                    EnsureNavMeshAgent(go, 5.5f, 0.6f, 1.8f);
                },
                GetDinoRotation("Assets/velociraptor.glb"));

            // 10. Khủng Long Công Thành Ankylosaurus (Length 3.6m, Natural Upright Y-up)
            createdPrefabs["Ankylosaurus"] = CreateOrUpdatePrefab(
                "Assets/ankylosaurus_updated.glb",
                $"{PREFAB_DIR}/Prefab_Ankylosaurus.prefab",
                3.6f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out SiegeDino _)) go.AddComponent<SiegeDino>();
                    EnsureBoxCollider(go, new Vector3(2.2f, 1.8f, 3.6f), new Vector3(0, 0.9f, 0));
                    EnsureNavMeshAgent(go, 2.4f, 1.0f, 1.8f);
                },
                GetDinoRotation("Assets/ankylosaurus_updated.glb"));

            // 11. Boss T-Rex Khổng Lồ (Length/Height 6.5m, Natural Upright Y-up)
            createdPrefabs["TRexBoss"] = CreateOrUpdatePrefab(
                "Assets/animated_t-rex_dinosaur_biting_attack_loop.glb",
                $"{PREFAB_DIR}/Prefab_TRexBoss.prefab",
                6.5f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out BossDino _)) go.AddComponent<BossDino>();
                    EnsureCapsuleCollider(go, 2.0f, 5.5f);
                    EnsureNavMeshAgent(go, 2.6f, 1.8f, 5.5f);
                },
                GetDinoRotation("Assets/animated_t-rex_dinosaur_biting_attack_loop.glb"));

            return createdPrefabs;
        }

        public static Vector3 GetDinoRotation(string glbPath)
        {
            // Rigged GLB characters have root bones already oriented with Y-up.
            // By default their upright orientation is Vector3.zero (Euler 0, 0, 0).
            // We inspect bone positions to ensure they face forward (+Z) and not backward (-Z) or sideways.
            try
            {
                GameObject glb = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
                if (glb != null)
                {
                    GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(glb);
                    temp.transform.position = Vector3.zero;
                    temp.transform.rotation = Quaternion.identity;
                    temp.transform.localScale = Vector3.one;

                    Transform head = FindBoneByKeywords(temp.transform, "head", "neck", "jaw", "skull", "snout", "mouth", "beak");
                    Transform hips = FindBoneByKeywords(temp.transform, "hip", "pelvis", "spine", "root", "body");

                    Vector3 chosen = Vector3.zero;
                    if (head != null && hips != null)
                    {
                        Vector3 forwardVec = head.position - hips.position;
                        float absX = Mathf.Abs(forwardVec.x);
                        float absZ = Mathf.Abs(forwardVec.z);

                        if (absZ >= absX && forwardVec.z < -0.15f)
                        {
                            // Facing backward (-Z) -> rotate 180 on Y so it faces forward
                            chosen = new Vector3(0f, 180f, 0f);
                        }
                        else if (absX > absZ && forwardVec.x > 0.15f)
                        {
                            // Facing right (+X) -> rotate -90 on Y
                            chosen = new Vector3(0f, -90f, 0f);
                        }
                        else if (absX > absZ && forwardVec.x < -0.15f)
                        {
                            // Facing left (-X) -> rotate 90 on Y
                            chosen = new Vector3(0f, 90f, 0f);
                        }
                        else
                        {
                            // Facing forward (+Z) -> Euler (0, 0, 0)
                            chosen = Vector3.zero;
                        }
                    }

                    DestroyImmediate(temp);
                    return chosen;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Prehistoric TD] Could not detect rotation for {glbPath}: {ex.Message}");
            }

            return Vector3.zero;
        }

        private static Transform FindBoneByKeywords(Transform parent, params string[] keywords)
        {
            foreach (string kw in keywords)
            {
                Transform found = FindDeepChild(parent, kw);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDeepChild(Transform parent, string keyword)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
                Transform deeper = FindDeepChild(child, keyword);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private static GameObject CreateOrUpdatePrefab(string glbPath, string prefabPath, float targetDimension, DimensionMode mode, System.Action<GameObject> configureAction, Vector3? customRotation = null)
        {
            if (File.Exists(prefabPath))
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject glbModel = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            if (glbModel != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(glbModel, root.transform);
                visual.name = "Model";
                visual.transform.localPosition = Vector3.zero;

                // Fix standing orientation: Static Blender models use Z-up (-90 deg X). Rigged dinos use Y-up (0 deg X)
                Vector3 uprightEuler = customRotation ?? new Vector3(-90f, 0f, 0f);
                visual.transform.localRotation = Quaternion.Euler(uprightEuler);
                visual.transform.localScale = Vector3.one;

                FitModelToTargetDimensions(visual, targetDimension, mode);
            }
            else
            {
                Debug.LogWarning($"<color=red>[Prehistoric TD]</color> GLB Model not found at '{glbPath}'! Creating placeholder primitive.");
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (cube.TryGetComponent(out Collider defaultCubeCol)) DestroyImmediate(defaultCubeCol);
                cube.name = "FallbackModel";
                cube.transform.SetParent(root.transform);
                cube.transform.localPosition = new Vector3(0, targetDimension / 2f, 0);
                cube.transform.localScale = new Vector3(targetDimension * 0.5f, targetDimension, targetDimension * 0.5f);
                Renderer r = cube.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.5f, 0.35f, 0.2f);
            }

            configureAction?.Invoke(root);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            return savedPrefab;
        }

        public static void FitModelToTargetDimensions(GameObject visual, float targetValue, DimensionMode mode)
        {
            if (targetValue <= 0.01f) return;

            // Measure bounds while model is in its upright standing rotation
            Bounds bounds = CalculateAccurateBounds(visual);

            float currentDimension = mode switch
            {
                DimensionMode.Height => bounds.size.y,
                DimensionMode.HorizontalWidth => Mathf.Max(bounds.size.x, bounds.size.z),
                _ => Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
            };

            if (currentDimension < 0.001f)
            {
                Debug.LogWarning($"[Scale Tool] Bounds for {visual.transform.parent.name} were zero! Using default 0.01f scale.");
                visual.transform.localScale = Vector3.one * 0.01f;
                return;
            }

            float scale = targetValue / currentDimension;
            visual.transform.localScale = Vector3.one * scale;

            // Grounding & Centering:
            // Shift model so lowest point is at Y=0 and center is at (X=0, Z=0)
            float groundedY = -bounds.min.y * scale;
            float centerX = -bounds.center.x * scale;
            float centerZ = -bounds.center.z * scale;

            visual.transform.localPosition = new Vector3(centerX, groundedY, centerZ);

            Debug.Log($"<color=green>[Scale & Upright Tool]</color> Stood up & Scaled <b>{visual.transform.parent.name}</b>: standing bounds ({bounds.size.x:F2}w, {bounds.size.y:F2}h, {bounds.size.z:F2}d) -> scale={scale:F6} -> final size=({bounds.size.x * scale:F2}m, {bounds.size.y * scale:F2}m, {bounds.size.z * scale:F2}m), grounded Y={groundedY:F3}");
        }

        private static Bounds CalculateAccurateBounds(GameObject visual)
        {
            Vector3 savedPos = visual.transform.localPosition;
            Vector3 savedScale = visual.transform.localScale;

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;

            Bounds b = new Bounds();
            bool hasBounds = false;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;

                if (r is SkinnedMeshRenderer smr)
                {
                    Bounds sb = smr.localBounds;
                    if (sb.size.sqrMagnitude > 0.001f)
                    {
                        Vector3 c = sb.center;
                        Vector3 e = sb.extents;
                        Transform t = smr.transform;
                        Vector3[] corners = new Vector3[8]
                        {
                            t.TransformPoint(c + new Vector3( e.x,  e.y,  e.z)),
                            t.TransformPoint(c + new Vector3( e.x,  e.y, -e.z)),
                            t.TransformPoint(c + new Vector3( e.x, -e.y,  e.z)),
                            t.TransformPoint(c + new Vector3( e.x, -e.y, -e.z)),
                            t.TransformPoint(c + new Vector3(-e.x,  e.y,  e.z)),
                            t.TransformPoint(c + new Vector3(-e.x,  e.y, -e.z)),
                            t.TransformPoint(c + new Vector3(-e.x, -e.y,  e.z)),
                            t.TransformPoint(c + new Vector3(-e.x, -e.y, -e.z)),
                        };

                        foreach (Vector3 pt in corners)
                        {
                            Vector3 localPt = visual.transform.parent != null
                                ? visual.transform.parent.InverseTransformPoint(pt)
                                : pt;

                            if (!hasBounds)
                            {
                                b = new Bounds(localPt, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                b.Encapsulate(localPt);
                            }
                        }
                    }
                    else if (smr.bones != null && smr.bones.Length > 0)
                    {
                        foreach (Transform bone in smr.bones)
                        {
                            if (bone == null) continue;
                            Vector3 localPt = visual.transform.parent != null
                                ? visual.transform.parent.InverseTransformPoint(bone.position)
                                : bone.position;

                            if (!hasBounds)
                            {
                                b = new Bounds(localPt, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                b.Encapsulate(localPt);
                            }
                        }
                    }
                    else if (smr.sharedMesh != null)
                    {
                        Bounds mb = smr.sharedMesh.bounds;
                        Vector3 c = mb.center;
                        Vector3 e = mb.extents;
                        Transform t = smr.transform;
                        Vector3[] corners = new Vector3[8]
                        {
                            t.TransformPoint(c + new Vector3( e.x,  e.y,  e.z)),
                            t.TransformPoint(c + new Vector3( e.x,  e.y, -e.z)),
                            t.TransformPoint(c + new Vector3( e.x, -e.y,  e.z)),
                            t.TransformPoint(c + new Vector3( e.x, -e.y, -e.z)),
                            t.TransformPoint(c + new Vector3(-e.x,  e.y,  e.z)),
                            t.TransformPoint(c + new Vector3(-e.x,  e.y, -e.z)),
                            t.TransformPoint(c + new Vector3(-e.x, -e.y,  e.z)),
                            t.TransformPoint(c + new Vector3(-e.x, -e.y, -e.z)),
                        };

                        foreach (Vector3 pt in corners)
                        {
                            Vector3 localPt = visual.transform.parent != null
                                ? visual.transform.parent.InverseTransformPoint(pt)
                                : pt;

                            if (!hasBounds)
                            {
                                b = new Bounds(localPt, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                b.Encapsulate(localPt);
                            }
                        }
                    }
                }
                else if (r is MeshRenderer mr && mr.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                {
                    Bounds mb = mf.sharedMesh.bounds;
                    Vector3 c = mb.center;
                    Vector3 e = mb.extents;
                    Transform t = mr.transform;

                    Vector3[] corners = new Vector3[8]
                    {
                        t.TransformPoint(c + new Vector3( e.x,  e.y,  e.z)),
                        t.TransformPoint(c + new Vector3( e.x,  e.y, -e.z)),
                        t.TransformPoint(c + new Vector3( e.x, -e.y,  e.z)),
                        t.TransformPoint(c + new Vector3( e.x, -e.y, -e.z)),
                        t.TransformPoint(c + new Vector3(-e.x,  e.y,  e.z)),
                        t.TransformPoint(c + new Vector3(-e.x,  e.y, -e.z)),
                        t.TransformPoint(c + new Vector3(-e.x, -e.y,  e.z)),
                        t.TransformPoint(c + new Vector3(-e.x, -e.y, -e.z)),
                    };

                    foreach (Vector3 pt in corners)
                    {
                        Vector3 localPt = visual.transform.parent != null
                            ? visual.transform.parent.InverseTransformPoint(pt)
                            : pt;

                        if (!hasBounds)
                        {
                            b = new Bounds(localPt, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            b.Encapsulate(localPt);
                        }
                    }
                }
                else if (r.bounds.size.magnitude > 0.01f)
                {
                    Vector3 min = visual.transform.parent != null ? visual.transform.parent.InverseTransformPoint(r.bounds.min) : r.bounds.min;
                    Vector3 max = visual.transform.parent != null ? visual.transform.parent.InverseTransformPoint(r.bounds.max) : r.bounds.max;
                    if (!hasBounds)
                    {
                        b = new Bounds(min, Vector3.zero);
                        b.Encapsulate(max);
                        hasBounds = true;
                    }
                    else
                    {
                        b.Encapsulate(min);
                        b.Encapsulate(max);
                    }
                }
            }

            visual.transform.localPosition = savedPos;
            visual.transform.localScale = savedScale;

            return b;
        }

        private static GameObject CreateOrUpdateTarPitPrefab(string prefabPath)
        {
            if (File.Exists(prefabPath))
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject tarPit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            if (tarPit.TryGetComponent(out Collider defaultCol)) DestroyImmediate(defaultCol);
            tarPit.name = "Prefab_TarPit";
            tarPit.transform.localScale = new Vector3(3.5f, 0.05f, 3.5f);

            Renderer rend = tarPit.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.08f, 0.08f, 0.09f, 0.95f);
            }

            if (!tarPit.TryGetComponent(out FrostTower _)) tarPit.AddComponent<FrostTower>();
            BoxCollider col = EnsureBoxCollider(tarPit, new Vector3(3.5f, 0.5f, 3.5f), new Vector3(0, 0.25f, 0));
            col.isTrigger = true;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(tarPit, prefabPath);
            DestroyImmediate(tarPit);
            return saved;
        }

        private static List<TowerSO> CreateTowerDataAssets(Dictionary<string, GameObject> prefabs)
        {
            List<TowerSO> list = new();

            list.Add(GetOrCreateTowerSO("Tower_Watchtower", "Chòi Cung Thủ Gỗ", 40, UnityEngine.InputSystem.Key.Digit1, prefabs["Watchtower"], false, 1.5f));
            list.Add(GetOrCreateTowerSO("Tower_Ballista", "Tháp Nỏ Bắn Lao", 80, UnityEngine.InputSystem.Key.Digit2, prefabs["Ballista"], false, 1.4f));
            list.Add(GetOrCreateTowerSO("Tower_Catapult", "Máy Bắn Đá Lửa", 100, UnityEngine.InputSystem.Key.Digit3, prefabs["Catapult"], false, 1.8f));
            list.Add(GetOrCreateTowerSO("Tower_ShamanTotem", "Cột Vật Tổ Sét", 120, UnityEngine.InputSystem.Key.Digit4, prefabs["ShamanTotem"], false, 1.4f));
            list.Add(GetOrCreateTowerSO("Tower_TarPit", "Vũng Hắc Ín (Chậm 50%)", 50, UnityEngine.InputSystem.Key.Digit5, prefabs["TarPit"], false, 1.8f));
            list.Add(GetOrCreateTowerSO("Tower_SpikeTrap", "Hố Chông Gai Gỗ", 30, UnityEngine.InputSystem.Key.Digit6, prefabs["SpikeTrap"], false, 1.2f));
            list.Add(GetOrCreateTowerSO("Tower_Barricade", "Rào Cọc Gỗ Cản Đường", 20, UnityEngine.InputSystem.Key.Digit7, prefabs["Barricade"], true, 1.6f));

            return list;
        }

        private static List<DinoSO> CreateDinoDataAssets(Dictionary<string, GameObject> prefabs)
        {
            List<DinoSO> list = new();

            list.Add(GetOrCreateDinoSO("Dino_Velociraptor", 20, prefabs["Velociraptor"], 85));
            list.Add(GetOrCreateDinoSO("Dino_Pterodactyl", 35, prefabs["Pterodactyl"], 120));
            list.Add(GetOrCreateDinoSO("Dino_Ankylosaurus", 50, prefabs["Ankylosaurus"], 350));
            list.Add(GetOrCreateDinoSO("Dino_TRexBoss", 200, prefabs["TRexBoss"], 1200));

            return list;
        }

        private static TowerSO GetOrCreateTowerSO(string fileName, string displayName, int cost, UnityEngine.InputSystem.Key hotkey, GameObject prefab, bool isWall, float radius)
        {
            string path = $"{CONFIG_DIR}/{fileName}.asset";
            TowerSO so = AssetDatabase.LoadAssetAtPath<TowerSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<TowerSO>();
                AssetDatabase.CreateAsset(so, path);
            }

            SerializedObject serialized = new SerializedObject(so);
            SerializedProperty nameProp = serialized.FindProperty("<DisplayName>k__BackingField");
            if (nameProp != null) nameProp.stringValue = displayName;
            SerializedProperty costProp = serialized.FindProperty("<Cost>k__BackingField");
            if (costProp != null) costProp.intValue = cost;
            SerializedProperty hotkeyProp = serialized.FindProperty("<Hotkey>k__BackingField");
            if (hotkeyProp != null) hotkeyProp.enumValueIndex = (int)hotkey;
            SerializedProperty isWallProp = serialized.FindProperty("<IsWall>k__BackingField");
            if (isWallProp != null) isWallProp.boolValue = isWall;
            SerializedProperty radiusProp = serialized.FindProperty("<PlacementRadius>k__BackingField");
            if (radiusProp != null) radiusProp.floatValue = radius;

            if (prefab != null)
            {
                Unit.Unit unitComp = prefab.GetComponent<Unit.Unit>();
                SerializedProperty prefabProp = serialized.FindProperty("<Prefab>k__BackingField");
                if (prefabProp != null) prefabProp.objectReferenceValue = unitComp;
                if (unitComp != null && unitComp.UnitType == null)
                {
                    unitComp.UnitType = so;
                    EditorUtility.SetDirty(prefab);
                }
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            return so;
        }

        private static DinoSO GetOrCreateDinoSO(string fileName, int cost, GameObject prefab, int health)
        {
            string path = $"{CONFIG_DIR}/{fileName}.asset";
            DinoSO so = AssetDatabase.LoadAssetAtPath<DinoSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<DinoSO>();
                AssetDatabase.CreateAsset(so, path);
            }

            SerializedObject serialized = new SerializedObject(so);
            SerializedProperty costProp = serialized.FindProperty("<Cost>k__BackingField");
            if (costProp != null) costProp.intValue = cost;
            SerializedProperty healthProp = serialized.FindProperty("<Health>k__BackingField");
            if (healthProp != null) healthProp.intValue = health;

            if (prefab != null)
            {
                Unit.Unit unitComp = prefab.GetComponent<Unit.Unit>();
                SerializedProperty prefabProp = serialized.FindProperty("<Prefab>k__BackingField");
                if (prefabProp != null) prefabProp.objectReferenceValue = unitComp;
                if (unitComp != null && unitComp.UnitType == null)
                {
                    unitComp.UnitType = so;
                    EditorUtility.SetDirty(prefab);
                }
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            return so;
        }

        private static void SetupSceneManagers(List<TowerSO> towerSOs, List<DinoSO> dinoSOs, Dictionary<string, GameObject> prefabs)
        {
            GameObject managersRoot = GameObject.Find("[Prehistoric TD Systems]");
            if (managersRoot == null)
            {
                managersRoot = new GameObject("[Prehistoric TD Systems]");
                Undo.RegisterCreatedObjectUndo(managersRoot, "Create Systems Root");
            }

            // 1. TowerPlacer
            if (!managersRoot.TryGetComponent(out TowerPlacer placer))
            {
                placer = managersRoot.AddComponent<TowerPlacer>();
            }

            // 2. Visualization
            if (!managersRoot.TryGetComponent(out PlaceTowerVisualization vis))
            {
                vis = managersRoot.AddComponent<PlaceTowerVisualization>();
            }

            SerializedObject placerSerialized = new SerializedObject(placer);
            placerSerialized.FindProperty("_Gold").intValue = 500;
            SerializedProperty towerListProp = placerSerialized.FindProperty("_AvailableTowers");
            if (towerListProp != null)
            {
                towerListProp.ClearArray();
                for (int i = 0; i < towerSOs.Count; i++)
                {
                    towerListProp.InsertArrayElementAtIndex(i);
                    towerListProp.GetArrayElementAtIndex(i).objectReferenceValue = towerSOs[i];
                }
            }
            SerializedProperty visProp = placerSerialized.FindProperty("Visualization");
            if (visProp != null) visProp.objectReferenceValue = vis;
            placerSerialized.ApplyModifiedProperties();

            // 3. DeadlinessHeatmap
            if (!managersRoot.TryGetComponent(out DeadlinessHeatmap _))
            {
                managersRoot.AddComponent<DeadlinessHeatmap>();
            }

            // 4. AdaptiveWaveManager
            if (!managersRoot.TryGetComponent(out AdaptiveWaveManager waveMgr))
            {
                waveMgr = managersRoot.AddComponent<AdaptiveWaveManager>();
            }

            // 5. PrehistoricGameModeManager (Allows switching between Tower Defense & Dino Assault)
            if (!managersRoot.TryGetComponent(out PrehistoricGameModeManager modeMgr))
            {
                modeMgr = managersRoot.AddComponent<PrehistoricGameModeManager>();
            }

            SerializedObject modeSerialized = new SerializedObject(modeMgr);
            SerializedProperty towersListProp = modeSerialized.FindProperty("AvailableTowers");
            if (towersListProp != null)
            {
                towersListProp.ClearArray();
                for (int i = 0; i < towerSOs.Count; i++)
                {
                    towersListProp.InsertArrayElementAtIndex(i);
                    towersListProp.GetArrayElementAtIndex(i).objectReferenceValue = towerSOs[i];
                }
            }

            SerializedProperty dinosListProp = modeSerialized.FindProperty("AvailableDinos");
            if (dinosListProp != null)
            {
                dinosListProp.ClearArray();
                for (int i = 0; i < dinoSOs.Count; i++)
                {
                    dinosListProp.InsertArrayElementAtIndex(i);
                    dinosListProp.GetArrayElementAtIndex(i).objectReferenceValue = dinoSOs[i];
                }
            }
            modeSerialized.ApplyModifiedProperties();

            modeMgr.SetGameMode(PrehistoricGameMode.TowerDefense);

            Transform[] spawnPoints = SetupSpawnPoints();

            SerializedObject waveSerialized = new SerializedObject(waveMgr);
            SerializedProperty spawnListProp = waveSerialized.FindProperty("SpawnPoints");
            spawnListProp.ClearArray();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                spawnListProp.InsertArrayElementAtIndex(i);
                spawnListProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            }

            SerializedProperty catalogProp = waveSerialized.FindProperty("MonsterCatalog");
            catalogProp.ClearArray();

            AddArchetype(catalogProp, 0, "Velociraptor Swarm", dinoSOs[0], 2.5f, AdaptiveWaveManager.ArchetypeRole.SwarmRunner, 12);
            AddArchetype(catalogProp, 1, "Pterodactyl Flyer", dinoSOs[1], 1.5f, AdaptiveWaveManager.ArchetypeRole.AerialFlyer, 20);
            AddArchetype(catalogProp, 2, "Ankylosaurus Siege", dinoSOs[2], 1.0f, AdaptiveWaveManager.ArchetypeRole.SiegeBreaker, 30);
            AddArchetype(catalogProp, 3, "T-Rex Apex Boss", dinoSOs[3], 0.3f, AdaptiveWaveManager.ArchetypeRole.Boss, 120);

            waveSerialized.ApplyModifiedProperties();
        }

        private static Transform[] SetupSpawnPoints()
        {
            GameObject spRoot = GameObject.Find("Prehistoric_SpawnPoints");
            if (spRoot == null)
            {
                spRoot = new GameObject("Prehistoric_SpawnPoints");
                Undo.RegisterCreatedObjectUndo(spRoot, "Create Spawn Points");
            }

            Transform sp1 = spRoot.transform.Find("Spawn_North");
            if (sp1 == null) { GameObject g = new GameObject("Spawn_North"); g.transform.SetParent(spRoot.transform); g.transform.position = new Vector3(-2.2f, 0, 35f); sp1 = g.transform; }

            Transform sp2 = spRoot.transform.Find("Spawn_East");
            if (sp2 == null) { GameObject g = new GameObject("Spawn_East"); g.transform.SetParent(spRoot.transform); g.transform.position = new Vector3(25f, 0, 20f); sp2 = g.transform; }

            Transform sp3 = spRoot.transform.Find("Spawn_West");
            if (sp3 == null) { GameObject g = new GameObject("Spawn_West"); g.transform.SetParent(spRoot.transform); g.transform.position = new Vector3(-30f, 0, 15f); sp3 = g.transform; }

            return new Transform[] { sp1, sp2, sp3 };
        }

        private static void AddArchetype(SerializedProperty catalog, int index, string name, DinoSO so, float weight, AdaptiveWaveManager.ArchetypeRole role, int gold)
        {
            catalog.InsertArrayElementAtIndex(index);
            SerializedProperty elem = catalog.GetArrayElementAtIndex(index);
            elem.FindPropertyRelative("Name").stringValue = name;
            elem.FindPropertyRelative("UnitSO").objectReferenceValue = so;
            elem.FindPropertyRelative("BaseWeight").floatValue = weight;
            elem.FindPropertyRelative("Role").enumValueIndex = (int)role;
            elem.FindPropertyRelative("GoldRewardOnDeath").intValue = gold;
        }

        private static void PlaceStarterDefenses(Dictionary<string, GameObject> prefabs, List<TowerSO> towerSOs)
        {
            GameObject defensesGroup = GameObject.Find("Starter_Defenses");
            if (defensesGroup != null)
            {
                DestroyImmediate(defensesGroup);
            }

            // Remove any loose/unscaled model instances left in the scene
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject obj in rootObjects)
            {
                string lower = obj.name.ToLower();
                if (lower.Contains("totem") || lower.Contains("highmountain") || lower.Contains("spike_trap") ||
                    lower.Contains("watchtower") || lower.Contains("barricade") || lower.Contains("ballista") ||
                    lower.Contains("catapult") || lower.Contains("pterodactyl") || lower.Contains("velociraptor") ||
                    lower.Contains("ankylosaurus") || lower.Contains("t-rex") || lower.Contains("feet_low") ||
                    lower.Contains("shield_base") || (lower.Contains("trap") && !lower.Contains("prehistoric")))
                {
                    DestroyImmediate(obj);
                }
            }

            defensesGroup = new GameObject("Starter_Defenses");
            Undo.RegisterCreatedObjectUndo(defensesGroup, "Place Starter Defenses");

            Transform targetBase = RoundManager.Instance != null ? RoundManager.Instance.DinoTarget : null;
            Vector3 basePos = targetBase != null ? targetBase.position : new Vector3(-2.23f, 0, -38.26f);

            // 1. Place 2 Watchtowers guarding front left & right
            if (prefabs.TryGetValue("Watchtower", out GameObject wt) && wt != null)
            {
                TowerSO wtSO = towerSOs != null ? towerSOs.Find(t => t.name.Contains("Watchtower")) : null;
                GameObject w1 = Instantiate(wt, basePos + new Vector3(-7f, 0, 11f), Quaternion.identity, defensesGroup.transform);
                GameObject w2 = Instantiate(wt, basePos + new Vector3(7f, 0, 11f), Quaternion.identity, defensesGroup.transform);
                if (wtSO != null)
                {
                    if (w1.TryGetComponent(out Unit.Unit u1)) u1.UnitType = wtSO;
                    if (w2.TryGetComponent(out Unit.Unit u2)) u2.UnitType = wtSO;
                }
            }

            // 2. Place 1 Catapult on high ground behind
            if (prefabs.TryGetValue("Catapult", out GameObject cat) && cat != null)
            {
                TowerSO catSO = towerSOs != null ? towerSOs.Find(t => t.name.Contains("Catapult")) : null;
                GameObject c = Instantiate(cat, basePos + new Vector3(0, 0, 17f), Quaternion.identity, defensesGroup.transform);
                if (catSO != null && c.TryGetComponent(out Unit.Unit uc)) uc.UnitType = catSO;
            }

            // 3. Place 1 Shaman Totem
            if (prefabs.TryGetValue("ShamanTotem", out GameObject tot) && tot != null)
            {
                TowerSO totSO = towerSOs != null ? towerSOs.Find(t => t.name.Contains("ShamanTotem")) : null;
                GameObject t = Instantiate(tot, basePos + new Vector3(-4f, 0, 8f), Quaternion.identity, defensesGroup.transform);
                if (totSO != null && t.TryGetComponent(out Unit.Unit ut)) ut.UnitType = totSO;
            }

            // 4. Place 2 Wooden Barricades forming a funnel chokepoint
            if (prefabs.TryGetValue("Barricade", out GameObject bar) && bar != null)
            {
                TowerSO barSO = towerSOs != null ? towerSOs.Find(t => t.IsWall) : null;
                GameObject b1 = Instantiate(bar, basePos + new Vector3(-3.5f, 0, 12f), Quaternion.Euler(0, 25f, 0), defensesGroup.transform);
                GameObject b2 = Instantiate(bar, basePos + new Vector3(3.5f, 0, 12f), Quaternion.Euler(0, -25f, 0), defensesGroup.transform);
                if (barSO != null)
                {
                    if (b1.TryGetComponent(out Unit.Unit ub1)) ub1.UnitType = barSO;
                    if (b2.TryGetComponent(out Unit.Unit ub2)) ub2.UnitType = barSO;
                }
            }

            // 5. Place 1 Spike Trap in the funnel bottleneck
            if (prefabs.TryGetValue("SpikeTrap", out GameObject st) && st != null)
            {
                TowerSO stSO = towerSOs != null ? towerSOs.Find(t => t.name.Contains("SpikeTrap")) : null;
                GameObject s = Instantiate(st, basePos + new Vector3(0, 0, 11f), Quaternion.identity, defensesGroup.transform);
                if (stSO != null && s.TryGetComponent(out Unit.Unit us)) us.UnitType = stSO;
            }

            Debug.Log("<color=cyan>[Prehistoric TD]</color> Placed Starter Defenses with proper scaling, upright orientation, and assigned UnitTypes!");
        }

        private static void SetupInGameHUD(List<TowerSO> towerSOs)
        {
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Prehistoric_Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            if (!canvas.TryGetComponent(out TowerSelectionUI ui))
            {
                ui = canvas.gameObject.AddComponent<TowerSelectionUI>();
            }
        }

        private static BoxCollider EnsureBoxCollider(GameObject go, Vector3 size, Vector3 center)
        {
            if (!go.TryGetComponent(out BoxCollider col)) col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.center = center;
            return col;
        }

        private static CapsuleCollider EnsureCapsuleCollider(GameObject go, float radius, float height)
        {
            if (!go.TryGetComponent(out CapsuleCollider col)) col = go.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = height;
            col.center = new Vector3(0, height / 2f, 0);
            return col;
        }

        private static NavMeshAgent EnsureNavMeshAgent(GameObject go, float speed, float radius = 0.5f, float height = 2.0f)
        {
            if (!go.TryGetComponent(out NavMeshAgent agent)) agent = go.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.radius = radius;
            agent.height = height;
            return agent;
        }
    }
}
