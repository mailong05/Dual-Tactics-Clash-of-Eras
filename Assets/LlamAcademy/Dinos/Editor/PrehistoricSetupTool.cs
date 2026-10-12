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

        static PrehistoricSetupTool()
        {
            FixBlenderImportIssue();
            EditorApplication.delayCall += EnsureWallInSceneOnLoad;
        }

        private static void EnsureWallInSceneOnLoad()
        {
            try
            {
                GameObject wall = GameObject.Find("Village_Fortress_Wall");
                GameObject wtLeft = GameObject.Find("Village_Watchtower_Left");
                Transform customGap = wall != null ? wall.transform.Find("Village_Left_Gap_Wall_128_neg152") : null;
                Transform leftWing = wall != null ? wall.transform.Find("Left_Wing_Wall") : null;
                if (wall == null || wtLeft == null || customGap == null || leftWing == null || leftWing.childCount < 14)
                {
                    BuildVillageFortressWallWithGate();
                    BakeAllCustomSceneWalls();
                    CleanSceneGhostObjects();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    Debug.Log("<color=green>[Prehistoric TD]</color> Tự động cấu trúc lại Tường Thành khép kín 100%, 2 Chòi Cung Thủ tiền tiêu và Bức tường lắp chỗ trống bên trái (128, 0, -152)!");
                }
            }
            catch {}
        }

        public static void FixBlenderImportIssue()
        {
            try
            {
                string assetsPath = Application.dataPath;
                if (!Directory.Exists(assetsPath)) return;

                List<string> blendFiles = new List<string>();
                string[] files = Directory.GetFiles(assetsPath, "*.*", SearchOption.AllDirectories);
                foreach (string f in files)
                {
                    string ext = Path.GetExtension(f).ToLower();
                    if ((ext == ".blend" || ext == ".blend1") && !f.EndsWith(".meta"))
                    {
                        blendFiles.Add(f);
                    }
                }

                if (blendFiles.Count > 0)
                {
                    string projectRoot = Directory.GetParent(assetsPath).FullName;
                    string backupDir = Path.Combine(projectRoot, "Backup_Blend_Files");
                    if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                    List<string> logs = new List<string>();
                    foreach (string bf in blendFiles)
                    {
                        string fileName = Path.GetFileName(bf);
                        string destPath = Path.Combine(backupDir, fileName);
                        File.Copy(bf, destPath, true);
                        File.Delete(bf);

                        string meta = bf + ".meta";
                        if (File.Exists(meta)) File.Delete(meta);

                        logs.Add($"Auto-relocated raw Blender file out of Assets: {fileName} -> Backup_Blend_Files/{fileName}");
                        Debug.Log($"<color=cyan>[Prehistoric TD]</color> Đã tự động di chuyển file '{fileName}' ra ngoài Assets/ vào thư mục 'Backup_Blend_Files' để khắc phục triệt để lỗi Blender not found.");
                    }

                    File.WriteAllLines(Path.Combine(projectRoot, "blender_fix_log.txt"), logs);
                    AssetDatabase.Refresh();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Prehistoric TD] FixBlenderImportIssue: " + ex.Message);
            }
        }

        [MenuItem("Prehistoric TD/Khắc Phục Lỗi Blender (.blend)")]
        public static void MenuFixBlender()
        {
            FixBlenderImportIssue();
            EditorUtility.DisplayDialog("Xử Lý File Blender", "Đã quét và di chuyển an toàn toàn bộ file .blend ra thư mục Backup_Blend_Files ngoài Assets!", "OK");
        }

        [InitializeOnLoadMethod]
        private static void AutoSetupOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;

                if (SceneManager.GetActiveScene().isLoaded)
                {
                    RunDiagnosticsAndMaterialsFix();

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

                    // 2. Check starter defense models and remove any legacy barricades
                    GameObject starter = GameObject.Find("Starter_Defenses");
                    if (starter == null)
                    {
                        needsFix = true;
                    }
                    else
                    {
                        // Clean up any old barricades in starter defenses
                        for (int ci = starter.transform.childCount - 1; ci >= 0; ci--)
                        {
                            Transform child = starter.transform.GetChild(ci);
                            if (child != null && (child.name.Contains("Barricade") || child.name.Contains("Plank_Defenses") || child.name.Contains("Shield_Base")))
                            {
                                DestroyImmediate(child.gameObject);
                            }
                        }

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

                    // 6. Check if ProBuilder Default material is broken or missing URP shader
                    Material pbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProBuilder Default.mat");
                    if (pbMat != null && (pbMat.shader == null || pbMat.shader.name.Contains("InternalError") || !pbMat.shader.name.Contains("Universal")))
                    {
                        needsFix = true;
                    }

                    // 7. Check if Village_Fortress_Wall is missing
                    if (GameObject.Find("Village_Fortress_Wall") == null)
                    {
                        needsFix = true;
                    }

                    // Check if PrehistoricGameplayManager is missing
                    if (Object.FindFirstObjectByType<PrehistoricGameplayManager>() == null)
                    {
                        needsFix = true;
                    }

                    // 8. Check if Prefab_Barricade is correctly rotated (0 deg on Y, width across road along X)
                    string bPath = $"{PREFAB_DIR}/Prefab_Barricade.prefab";
                    if (File.Exists(bPath))
                    {
                        GameObject bObj = AssetDatabase.LoadAssetAtPath<GameObject>(bPath);
                        if (bObj != null)
                        {
                            Transform mChild = bObj.transform.Find("Model");
                            if (mChild == null || Mathf.Abs(mChild.localEulerAngles.y) > 10f)
                            {
                                needsFix = true;
                            }
                        }
                    }
                    else
                    {
                        needsFix = true;
                    }

                    // 9. Check if Velociraptor is still using old oversized scale (> 0.5f radius)
                    string vrPath = $"{PREFAB_DIR}/Prefab_Velociraptor.prefab";
                    if (File.Exists(vrPath))
                    {
                        GameObject vrObj = AssetDatabase.LoadAssetAtPath<GameObject>(vrPath);
                        if (vrObj != null)
                        {
                            CapsuleCollider cc = vrObj.GetComponent<CapsuleCollider>();
                            if (cc != null && cc.radius > 0.5f)
                            {
                                needsFix = true;
                            }
                        }
                    }

                    if (needsFix)
                    {
                        Debug.Log("<color=yellow>[Prehistoric TD]</color> Detected uncalibrated models, missing gameplay manager, or material shader issues. Auto-configuring now...");
                        FixAllModelScales(false);
                    }
                }
            };
        }

        public static void RunDiagnosticsAndMaterialsFix()
        {
            try
            {
                // 0. Find any .blend files causing Unity Blender import error
                string[] blendFiles = Directory.GetFiles("Assets", "*.blend", SearchOption.AllDirectories);
                string[] blend1Files = Directory.GetFiles("Assets", "*.blend1", SearchOption.AllDirectories);
                List<string> allBlends = new List<string>(blendFiles);
                allBlends.AddRange(blend1Files);
                File.WriteAllLines("Assets/blend_files_found.txt", allBlends);

                // 1. Diagnostics on wooden__barricade_low.glb
                System.Text.StringBuilder sbBar = new System.Text.StringBuilder();
                sbBar.AppendLine("=== BARRICADE MODEL REPORT ===");
                GameObject barGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/wooden__barricade_low.glb");
                if (barGlb != null)
                {
                    GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(barGlb);
                    Transform[] allT = temp.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in allT)
                    {
                        sbBar.AppendLine($"Child: '{t.name}' (parent: '{t.parent?.name}')");
                        sbBar.AppendLine($"  pos: {t.localPosition}, rot: {t.localEulerAngles}, scale: {t.localScale}");
                        if (t.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                        {
                            sbBar.AppendLine($"  Mesh: '{mf.sharedMesh.name}' bounds size: {mf.sharedMesh.bounds.size}, center: {mf.sharedMesh.bounds.center}");
                        }
                        if (t.TryGetComponent(out Renderer rend))
                        {
                            sbBar.AppendLine($"  Renderer: bounds size: {rend.bounds.size}, center: {rend.bounds.center}");
                            foreach (Material m in rend.sharedMaterials)
                            {
                                sbBar.AppendLine($"    Mat: {(m != null ? m.name : "NULL")}, Shader: {(m != null && m.shader != null ? m.shader.name : "NULL")}");
                            }
                        }
                    }
                    Bounds totalB = CalculateAccurateBounds(temp);
                    sbBar.AppendLine($"TOTAL BOUNDS at identity: size={totalB.size}, center={totalB.center}, min={totalB.min}, max={totalB.max}");
                    temp.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
                    Bounds totalRotB = CalculateAccurateBounds(temp);
                    sbBar.AppendLine($"TOTAL BOUNDS at Euler(-90,0,0): size={totalRotB.size}, center={totalRotB.center}, min={totalRotB.min}, max={totalRotB.max}");
                    DestroyImmediate(temp);
                }
                File.WriteAllText("Assets/barricade_model_info.txt", sbBar.ToString());

                // 2. Diagnostics on Scene Village Layout
                System.Text.StringBuilder sbVil = new System.Text.StringBuilder();
                sbVil.AppendLine("=== VILLAGE LAYOUT REPORT ===");
                Transform targetBase = RoundManager.Instance != null ? RoundManager.Instance.DinoTarget : null;
                if (targetBase != null) sbVil.AppendLine($"RoundManager DinoTarget: '{targetBase.name}' at {targetBase.position}");
                else sbVil.AppendLine("RoundManager DinoTarget: NULL");

                GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (GameObject r in roots)
                {
                    Renderer[] rRends = r.GetComponentsInChildren<Renderer>(true);
                    if (rRends.Length > 0)
                    {
                        Bounds b = rRends[0].bounds;
                        for (int i = 1; i < rRends.Length; i++) b.Encapsulate(rRends[i].bounds);
                        sbVil.AppendLine($"Root '{r.name}': pos={r.transform.position}, bounds size={b.size}, center={b.center}, min={b.min}, max={b.max}");
                    }
                    else
                    {
                        sbVil.AppendLine($"Root '{r.name}': pos={r.transform.position}");
                    }

                    Transform[] children = r.GetComponentsInChildren<Transform>(true);
                    foreach (Transform c in children)
                    {
                        string ln = c.name.ToLower();
                        if (ln.Contains("tent") || ln.Contains("hut") || ln.Contains("house") || ln.Contains("fire") ||
                            ln.Contains("platform") || ln.Contains("ramp") || ln.Contains("walk") || ln.Contains("catwalk") ||
                            ln.Contains("bridge") || ln.Contains("wall") || ln.Contains("gate") || ln.Contains("tower") ||
                            ln.Contains("target") || ln.Contains("base") || ln.Contains("village") || ln.Contains("palisade") ||
                            ln.Contains("defense") || ln.Contains("prop") || ln.Contains("starter"))
                        {
                            Vector3 p = c.position;
                            string bStr = "";
                            if (c.TryGetComponent(out Renderer cr)) bStr = $" | bSize={cr.bounds.size}, bCenter={cr.bounds.center}";
                            sbVil.AppendLine($"  Child '{c.name}' (parent: {c.parent?.name}) at {p}{bStr}");
                        }
                    }
                }
                File.WriteAllText("Assets/village_layout_report.txt", sbVil.ToString());

                // 3. Scene Materials Report & Fix
                System.Text.StringBuilder sbMat = new System.Text.StringBuilder();
                sbMat.AppendLine("=== SCENE MATERIALS REPORT ===");
                Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Prehistoric_Wood.mat");
                Material pbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProBuilder Default.mat");
                Material fallbackMat = woodMat != null ? woodMat : pbMat;

                Renderer[] allRends = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                int replacedCount = 0;
                foreach (Renderer rend in allRends)
                {
                    if (rend == null) continue;
                    Material[] mats = rend.sharedMaterials;
                    bool rendChanged = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material m = mats[i];
                        bool isBroken = false;
                        string reason = "";
                        if (m == null) { isBroken = true; reason = "NULL material"; }
                        else if (m.shader == null) { isBroken = true; reason = "NULL shader"; }
                        else if (m.shader.name.Contains("InternalError")) { isBroken = true; reason = "Hidden/InternalErrorShader"; }
                        else if (m.shader.name.StartsWith("Standard")) { isBroken = true; reason = "Standard shader in URP"; }
                        else if (m.shader.name.Contains("Autodesk")) { isBroken = true; reason = "Autodesk shader"; }
                        else if (m.name.Contains("standardSurface")) { isBroken = true; reason = "standardSurface material"; }

                        if (isBroken)
                        {
                            sbMat.AppendLine($"FIXING: '{rend.gameObject.name}' (Path: {GetHierarchyPath(rend.transform)}) - Mat[{i}]: '{(m != null ? m.name : "NULL")}', Reason: {reason}");
                            mats[i] = fallbackMat;
                            rendChanged = true;
                            replacedCount++;
                        }
                    }
                    if (rendChanged)
                    {
                        rend.sharedMaterials = mats;
                        EditorUtility.SetDirty(rend);
                    }
                }
                sbMat.AppendLine($"Total materials fixed in scene: {replacedCount}");
                File.WriteAllText("Assets/scene_materials_report.txt", sbMat.ToString());

                if (replacedCount > 0)
                {
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    AssetDatabase.SaveAssets();
                }

                Debug.Log($"<color=green>[Prehistoric TD]</color> Diagnostics & Materials Fix completed! Replaced {replacedCount} broken materials.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Prehistoric TD] Diagnostics error: {ex}");
            }
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "";
            if (t.parent == null) return t.name;
            return GetHierarchyPath(t.parent) + "/" + t.name;
        }

        [MenuItem("Tools/Prehistoric TD/Setup Everything & Complete Gameplay (1-Click)", priority = 0)]
        public static void MenuSetupEverything()
        {
            FixAllModelScales(true);
        }

        [MenuItem("Tools/Prehistoric TD/Fix All Materials (URP Lit)", priority = 10)]
        public static void MenuFixMaterials()
        {
            FixAllMaterials();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Vật Liệu Hoàn Tất", "Đã chuẩn hóa toàn bộ vật liệu sang Universal Render Pipeline/Lit (loại bỏ hoàn toàn lỗi màu hồng Hidden/InternalErrorShader)!", "Đóng");
        }

        [MenuItem("Tools/Prehistoric TD/Build Village Fortress Wall with Gate", priority = 11)]
        public static void MenuBuildFortressWall()
        {
            BuildVillageFortressWallWithGate();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Tường Thành Hoàn Tất", "Đã kiến tạo hệ thống Tường Thành Có Cổng kiên cố trước làng để chặn khủng long!", "Đóng");
        }

        [MenuItem("Tools/Prehistoric TD/Recalibrate Dino Scales Only", priority = 12)]
        public static void MenuRecalibrateDinos()
        {
            Dictionary<string, GameObject> prefabs = CreateAllPrefabs();
            CreateTowerDataAssets(prefabs);
            CreateDinoDataAssets(prefabs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Kích Thước Khủng Long Hoàn Tất", "Đã tinh chỉnh tỉ lệ chuẩn xác cho cả 4 loài khủng long:\n• Velociraptor: Bầy đàn 1.6m\n• Pterodactyl: Bay lượn 2.6m\n• Ankylosaurus: Bọc thép công thành 5.0m\n• T-Rex Apex Boss: Khổng lồ 5.5m (cao) x 11.5m (dài)!", "Đóng");
        }

        [MenuItem("Tools/Prehistoric TD/Clean Scene Ghost, Rooftop & Underground Objects", priority = 13)]
        [MenuItem("Prehistoric TD/Dọn Dẹp Vật Thể Rác & Nóc Nhà", priority = 13)]
        public static void MenuCleanGhostObjects()
        {
            CleanSceneGhostObjects();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Dọn Dẹp Hoàn Tất", "Đã dọn dẹp triệt để các vật thể tàng hình (ghost healthbars), vật thể bay trên nóc nhà / trên không và các clone nằm dưới lòng đất!", "Đóng");
        }

        public static void CleanSceneGhostObjects()
        {
            int cleanedCount = 0;

            // 1. Dọn dẹp HealthBarCanvas children
            GameObject hbc = GameObject.Find("HealthBarCanvas");
            if (hbc != null)
            {
                var children = new List<GameObject>();
                foreach (Transform child in hbc.transform)
                {
                    children.Add(child.gameObject);
                }
                foreach (var child in children)
                {
                    Undo.DestroyObjectImmediate(child);
                    cleanedCount++;
                }
            }

            // 2. Dọn dẹp Starter_Defenses và các clone bất thường
            GameObject starterDef = GameObject.Find("Starter_Defenses");
            if (starterDef != null)
            {
                Undo.DestroyObjectImmediate(starterDef);
                cleanedCount++;
            }

            // 3. Dọn dẹp các mẩu ván chắn (Plank_Defenses) bay trên nóc nhà / bầu trời hoặc tọa độ dị thường
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                if (n.Contains("Plank_Defenses") || n.Contains("standardSurface1"))
                {
                    Vector3 pos = t.position;
                    // Nếu ở độ cao trên nóc nhà (y > 6.0) hoặc tọa độ bay xa
                    if (pos.y > 6.0f || Mathf.Abs(pos.x) > 50f || Mathf.Abs(pos.z) > 50f)
                    {
                        Undo.DestroyObjectImmediate(t.gameObject);
                        cleanedCount++;
                    }
                }
            }

            Debug.Log($"<color=green>[Prehistoric TD]</color> Đã quét và dọn dẹp thành công {cleanedCount} vật thể rác / nóc nhà / dưới lòng đất trong scene!");
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

        public static void FixAllMaterials()
        {
            // 1. Fix ProBuilder Default.mat
            Material pbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProBuilder Default.mat");
            if (pbMat != null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (urpLit != null)
                {
                    pbMat.shader = urpLit;
                    if (pbMat.HasProperty("_BaseColor")) pbMat.SetColor("_BaseColor", new Color(0.58f, 0.44f, 0.30f, 1f));
                    if (pbMat.HasProperty("_Color")) pbMat.SetColor("_Color", new Color(0.58f, 0.44f, 0.30f, 1f));
                    if (pbMat.HasProperty("_Smoothness")) pbMat.SetFloat("_Smoothness", 0.1f);
                    EditorUtility.SetDirty(pbMat);
                }
            }

            // 2. Scan all project materials for broken/missing shaders
            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            int fixedCount = 0;
            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null && (m.shader == null || m.shader.name.Contains("InternalError") || m.shader.name.StartsWith("Standard") || m.shader.name.StartsWith("ProBuilder/")))
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader != null)
                    {
                        m.shader = urpShader;
                        EditorUtility.SetDirty(m);
                        fixedCount++;
                    }
                }
            }

            // 3. Scan all scene renderers for broken materials
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Prehistoric_Wood.mat");
            Material targetReplacementMat = woodMat != null ? woodMat : pbMat;

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (Renderer r in renderers)
            {
                if (r == null || r.sharedMaterials == null) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    bool isBroken = false;
                    if (mats[i] == null) isBroken = true;
                    else if (mats[i].shader == null) isBroken = true;
                    else if (mats[i].shader.name.Contains("InternalError")) isBroken = true;
                    else if (mats[i].shader.name.StartsWith("Standard")) isBroken = true;
                    else if (mats[i].shader.name.Contains("Autodesk")) isBroken = true;
                    else if (mats[i].name.Contains("standardSurface")) isBroken = true;

                    if (isBroken || r.gameObject.name.ToLower().Contains("tarpit") || r.transform.root.name.ToLower().Contains("tarpit"))
                    {
                        Material tarPitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_TarPit.mat");
                        if ((r.gameObject.name.ToLower().Contains("tarpit") || r.transform.root.name.ToLower().Contains("tarpit")) && tarPitMat != null)
                        {
                            mats[i] = tarPitMat;
                            changed = true;
                        }
                        else if (targetReplacementMat != null)
                        {
                            mats[i] = targetReplacementMat;
                            changed = true;
                        }
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[Prehistoric TD]</color> Fixed {fixedCount} materials with missing/URP shaders!");
        }

        public static void FixAllModelScales(bool showDialog = true)
        {
            Debug.Log("<color=green>[Prehistoric TD]</color> Starting Universal 3D Model Scale & Upright Orientation Calibration...");

            FixAllMaterials();
            DiagnoseAllDinos();
            EnsureDirectories();
            ReimportGLBModels();

            Dictionary<string, GameObject> prefabs = CreateAllPrefabs();
            List<TowerSO> towerSOs = CreateTowerDataAssets(prefabs);
            List<DinoSO> dinoSOs = CreateDinoDataAssets(prefabs);

            SetupSceneManagers(towerSOs, dinoSOs, prefabs);
            PlaceStarterDefenses(prefabs, towerSOs);
            BuildVillageFortressWallWithGate();
            SetupInGameHUD(towerSOs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Debug.Log("<color=green>[Prehistoric TD]</color> ALL MODELS & COMPLETE GAMEPLAY LOOP CONFIGURED PERFECTLY!");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Đã Cập Nhật Gameplay Hoàn Chỉnh & Xây Dựng Tường Thành Có Cổng!",
                    "Hoàn tất toàn bộ yêu cầu của bạn:\n\n" +
                    "• ĐÃ XÂY DỰNG TƯỜNG THÀNH CÓ CỔNG (Village Fortress Wall with Gate) chặn khủng long!\n" +
                    "• QUẢ CẦU LỬA DẠNG LAVA (Stylized Lava Material) cho Máy Bắn Đá Lửa rực sáng!\n" +
                    "• VIẾT LẠI TOÀN BỘ GAMEPLAY HOÀN CHỈNH:\n" +
                    "   - PrehistoricGameplayManager điều phối vòng lặp Thủ Thành hoàn hảo\n" +
                    "   - Hệ thống Máu Làng (100 HP), hiển thị thanh máu trực quan\n" +
                    "   - 10 Đợt khủng long có nhịp độ tăng dần (Raptor, Pterodactyl, Ankylosaurus & Apex Boss T-Rex)\n" +
                    "   - Nút Bắt Đầu Đợt (Phím Space / Click) rõ ràng ở pha chuẩn bị\n" +
                    "   - Thưởng vàng, nâng cấp & bảo toàn chiến thuật 7 loại tháp\n" +
                    "   - Màn hình Thất Thủ (Game Over) & Khải Hoàn (Victory) đầy đủ nút Chơi Lại!",
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
                    if (!go.TryGetComponent(out CatapultTower ct)) ct = go.AddComponent<CatapultTower>();
                    EnsureBoxCollider(go, new Vector3(2.6f, 2.4f, 3.2f), new Vector3(0, 1.2f, 0));

                    GameObject lavaBoulder = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_LavaBoulder.prefab");
                    GameObject explosionVFX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/SmallExplosion.prefab");
                    var so = new SerializedObject(ct);
                    if (lavaBoulder != null) so.FindProperty("BoulderPrefab").objectReferenceValue = lavaBoulder;
                    if (explosionVFX != null) so.FindProperty("ImpactExplosionVFX").objectReferenceValue = explosionVFX.GetComponent<ParticleSystem>();
                    so.ApplyModifiedPropertiesWithoutUndo();
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

            // 7. Rào Cọc Gỗ Cản Đường (Barricade - Width across road 3.0m along X, Upright -90 deg X, 0 deg Y)
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
                    obs.size = new Vector3(3.0f, 1.4f, 0.8f);
                    obs.center = new Vector3(0, 0.7f, 0);
                    EnsureBoxCollider(go, new Vector3(3.0f, 1.4f, 0.8f), new Vector3(0, 0.7f, 0));

                    Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Prehistoric_Wood.mat");
                    if (woodMat != null)
                    {
                        Renderer[] rList = go.GetComponentsInChildren<Renderer>(true);
                        foreach (Renderer r in rList)
                        {
                            Material[] mats = new Material[r.sharedMaterials.Length];
                            for (int mi = 0; mi < mats.Length; mi++) mats[mi] = woodMat;
                            r.sharedMaterials = mats;
                        }
                    }
                },
                new Vector3(-90f, 0f, 0f));

            // 8. Khủng Long Bay Pterodactyl (Wingspan 2.6m, Aerial threat)
            createdPrefabs["Pterodactyl"] = CreateOrUpdatePrefab(
                "Assets/pterodactyl_1.glb",
                $"{PREFAB_DIR}/Prefab_Pterodactyl.prefab",
                2.6f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out FlyingUnit _)) go.AddComponent<FlyingUnit>();
                    EnsureCapsuleCollider(go, 0.6f, 1.0f);
                },
                GetDinoRotation("Assets/pterodactyl_1.glb"));

            // 9. Khủng Long Chạy Nhanh Velociraptor (Pack Runner: Length 1.6m, Height ~0.85m, nimble swarm)
            createdPrefabs["Velociraptor"] = CreateOrUpdatePrefab(
                "Assets/velociraptor.glb",
                $"{PREFAB_DIR}/Prefab_Velociraptor.prefab",
                1.6f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out RunnerDino _)) go.AddComponent<RunnerDino>();
                    EnsureCapsuleCollider(go, 0.4f, 1.0f);
                    EnsureNavMeshAgent(go, 5.5f, 0.4f, 1.0f);
                },
                GetDinoRotation("Assets/velociraptor.glb"));

            // 10. Khủng Long Công Thành Ankylosaurus (Siege Tank: Length 5.0m, Width ~2.1m, Height ~2.0m)
            createdPrefabs["Ankylosaurus"] = CreateOrUpdatePrefab(
                "Assets/ankylosaurus_updated.glb",
                $"{PREFAB_DIR}/Prefab_Ankylosaurus.prefab",
                5.0f,
                DimensionMode.MaxExtent,
                go =>
                {
                    if (!go.TryGetComponent(out SiegeDino _)) go.AddComponent<SiegeDino>();
                    EnsureBoxCollider(go, new Vector3(2.2f, 2.0f, 5.0f), new Vector3(0, 1.0f, 0));
                    EnsureNavMeshAgent(go, 2.2f, 1.1f, 2.0f);
                },
                GetDinoRotation("Assets/ankylosaurus_updated.glb"));

            // 11. Boss T-Rex Khổng Lồ (Apex Boss: Towering Height 5.5m, Length ~11.5m, Width ~3.1m)
            createdPrefabs["TRexBoss"] = CreateOrUpdatePrefab(
                "Assets/animated_t-rex_dinosaur_biting_attack_loop.glb",
                $"{PREFAB_DIR}/Prefab_TRexBoss.prefab",
                5.5f,
                DimensionMode.Height,
                go =>
                {
                    if (!go.TryGetComponent(out BossDino _)) go.AddComponent<BossDino>();
                    EnsureCapsuleCollider(go, 1.8f, 5.5f);
                    EnsureNavMeshAgent(go, 2.4f, 1.8f, 5.5f);
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
                Material tarMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_TarPit.mat");
                if (tarMat != null)
                {
                    rend.sharedMaterial = tarMat;
                }
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

            list.Add(GetOrCreateTowerSO("Tower_Watchtower", "Chòi Cung Thủ Gỗ", 40, UnityEngine.InputSystem.Key.Digit1, prefabs["Watchtower"], false, 1.5f, 600));
            list.Add(GetOrCreateTowerSO("Tower_Ballista", "Tháp Nỏ Bắn Lao", 80, UnityEngine.InputSystem.Key.Digit2, prefabs["Ballista"], false, 1.4f, 550));
            list.Add(GetOrCreateTowerSO("Tower_Catapult", "Máy Bắn Đá Lửa", 100, UnityEngine.InputSystem.Key.Digit3, prefabs["Catapult"], false, 1.8f, 700));
            list.Add(GetOrCreateTowerSO("Tower_ShamanTotem", "Cột Vật Tổ Sét", 120, UnityEngine.InputSystem.Key.Digit4, prefabs["ShamanTotem"], false, 1.4f, 650));
            list.Add(GetOrCreateTowerSO("Tower_TarPit", "Vũng Hắc Ín (Chậm 50%)", 50, UnityEngine.InputSystem.Key.Digit5, prefabs["TarPit"], false, 1.8f, 400));
            list.Add(GetOrCreateTowerSO("Tower_SpikeTrap", "Hố Chông Gai Gỗ", 30, UnityEngine.InputSystem.Key.Digit6, prefabs["SpikeTrap"], false, 1.2f, 350));
            list.Add(GetOrCreateTowerSO("Tower_Barricade", "Rào Cọc Gỗ Cản Đường", 20, UnityEngine.InputSystem.Key.Digit7, prefabs["Barricade"], true, 1.6f, 250));

            return list;
        }

        private static List<DinoSO> CreateDinoDataAssets(Dictionary<string, GameObject> prefabs)
        {
            List<DinoSO> list = new();

            list.Add(GetOrCreateDinoSO("Dino_Velociraptor", 20, prefabs["Velociraptor"], 240));
            list.Add(GetOrCreateDinoSO("Dino_Pterodactyl", 35, prefabs["Pterodactyl"], 360));
            list.Add(GetOrCreateDinoSO("Dino_Ankylosaurus", 50, prefabs["Ankylosaurus"], 1275));
            list.Add(GetOrCreateDinoSO("Dino_TRexBoss", 100, prefabs["TRexBoss"], 5250));

            return list;
        }

        private static TowerSO GetOrCreateTowerSO(string fileName, string displayName, int cost, UnityEngine.InputSystem.Key hotkey, GameObject prefab, bool isWall, float radius, int health = 0)
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
            SerializedProperty healthProp = serialized.FindProperty("<Health>k__BackingField");
            if (healthProp != null && health > 0) healthProp.intValue = health;

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

            // 0. PrehistoricGameplayManager (Master Game Loop & Base Defense)
            if (!managersRoot.TryGetComponent(out PrehistoricGameplayManager gameMgr))
            {
                gameMgr = managersRoot.AddComponent<PrehistoricGameplayManager>();
            }

            SerializedObject gameMgrSerialized = new SerializedObject(gameMgr);
            if (prefabs.TryGetValue("Velociraptor", out GameObject pRap))
                gameMgrSerialized.FindProperty("RaptorPrefab").objectReferenceValue = pRap;
            if (prefabs.TryGetValue("Pterodactyl", out GameObject pPter))
                gameMgrSerialized.FindProperty("PterodactylPrefab").objectReferenceValue = pPter;
            if (prefabs.TryGetValue("Ankylosaurus", out GameObject pAnk))
                gameMgrSerialized.FindProperty("AnkylosaurusPrefab").objectReferenceValue = pAnk;
            if (prefabs.TryGetValue("TRexBoss", out GameObject pTRex))
                gameMgrSerialized.FindProperty("TRexBossPrefab").objectReferenceValue = pTRex;
            gameMgrSerialized.ApplyModifiedProperties();

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
            waveMgr.enabled = false;

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

            // 6. IO Cloud Integration Manager (Docker & Cloud telemetry)
            if (!managersRoot.TryGetComponent(out LlamAcademy.Dinos.Cloud.IOCloudManager _))
            {
                managersRoot.AddComponent<LlamAcademy.Dinos.Cloud.IOCloudManager>();
            }

            // 7. 2D Tactical Minimap / Radar System (Unity 2D requirement)
            if (!managersRoot.TryGetComponent(out LlamAcademy.Dinos.Tactical2D.TacticalMinimap2D _))
            {
                managersRoot.AddComponent<LlamAcademy.Dinos.Tactical2D.TacticalMinimap2D>();
            }

            // 8. Mobile Touch Controller (Mobile deployment requirement)
            if (!managersRoot.TryGetComponent(out LlamAcademy.Dinos.Mobile.MobileTouchController _))
            {
                managersRoot.AddComponent<LlamAcademy.Dinos.Mobile.MobileTouchController>();
            }

            // 9. AR Tabletop Controller (AR requirement)
            if (!managersRoot.TryGetComponent(out LlamAcademy.Dinos.AR.ARTabletopController _))
            {
                managersRoot.AddComponent<LlamAcademy.Dinos.AR.ARTabletopController>();
            }

            // 10. HealthBarCanvas (World Space UI for units & walls)
            var allHbCanvases = Object.FindObjectsByType<LlamAcademy.Dinos.Utility.HealthBarCanvas>();
            LlamAcademy.Dinos.Utility.HealthBarCanvas hbCanvas = null;

            foreach (var hbc in allHbCanvases)
            {
                if (hbc.gameObject.name == "HealthBarCanvas")
                {
                    if (hbCanvas == null) hbCanvas = hbc;
                    else DestroyImmediate(hbc);
                }
                else
                {
                    // Gỡ component HealthBarCanvas khỏi các GameObject khác (như Health Bar Canvas cũ)
                    DestroyImmediate(hbc);
                }
            }

            if (hbCanvas == null)
            {
                GameObject canvasObj = new GameObject("HealthBarCanvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = null;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 500;
                hbCanvas = canvasObj.AddComponent<LlamAcademy.Dinos.Utility.HealthBarCanvas>();
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create HealthBarCanvas");
            }
            else
            {
                Canvas canvas = hbCanvas.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = null;
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 500;
                }
            }

            // Gán reference HealthBarPrefab chuẩn xác vào HealthBarCanvas
            var hbPrefab = AssetDatabase.LoadAssetAtPath<LlamAcademy.Dinos.UI.HealthBar>("Assets/Resources/Health Bar.prefab");
            if (hbPrefab != null && hbCanvas != null)
            {
                var so = new SerializedObject(hbCanvas);
                var prop = so.FindProperty("HealthBarPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = hbPrefab;
                    so.ApplyModifiedProperties();
                }
            }
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

            // 2 Chòi Cung Thủ tiền tiêu đã được tích hợp kiên cố tại 2 cánh cổng thành của Village_Fortress_Wall
            Debug.Log("<color=cyan>[Prehistoric TD]</color> Phòng tuyến tiền tiêu trước làng đã được đồng bộ chuẩn xác!");
        }

        public static GameObject BuildVillageFortressWallWithGate()
        {
            // 1. Clean up old wall roots and hidden/stray archer towers
            GameObject oldWall = GameObject.Find("Village_Fortress_Wall");
            if (oldWall != null) DestroyImmediate(oldWall);
            GameObject oldPerimeter = GameObject.Find("Village_Perimeter_Wall");
            if (oldPerimeter != null) DestroyImmediate(oldPerimeter);
            GameObject oldStarter = GameObject.Find("Starter_Defenses");
            if (oldStarter != null) DestroyImmediate(oldStarter);
            GameObject oldWaypoints = GameObject.Find("Archer Waypoints");
            if (oldWaypoints != null) DestroyImmediate(oldWaypoints);

            // Clean any stray watchtowers
            GameObject oldWtL = GameObject.Find("Village_Watchtower_Left");
            if (oldWtL != null) DestroyImmediate(oldWtL);
            GameObject oldWtR = GameObject.Find("Village_Watchtower_Right");
            if (oldWtR != null) DestroyImmediate(oldWtR);

            GameObject fortressRoot = new GameObject("Village_Fortress_Wall");
            Undo.RegisterCreatedObjectUndo(fortressRoot, "Build Village Fortress Wall with Gate");

            GameObject towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LlamAcademy/Dinos/Vendor/polyperfect/Low Poly Tribal Pack/T/- Prefabs_T/Tribal_T/Tower_Tribal_Tier1.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LlamAcademy/Dinos/Vendor/polyperfect/Low Poly Tribal Pack/T/- Prefabs_T/Tribal_T/Bridge_Tribal_Tier1.prefab");
            GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LlamAcademy/Dinos/Vendor/polyperfect/Low Poly Tribal Pack/T/- Prefabs_T/Tribal_T/Torch_Standing_Tribal.prefab");
            GameObject barricadePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_Barricade.prefab");
            TowerSO barricadeSO = AssetDatabase.LoadAssetAtPath<TowerSO>("Assets/Config/Prehistoric/Tower_Barricade.asset");

            GameObject watchtowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Prefab_Watchtower.prefab");
            TowerSO watchtowerSO = AssetDatabase.LoadAssetAtPath<TowerSO>($"{CONFIG_DIR}/Tower_Watchtower.asset");

            const float wallZ = -2.5f;

            // 1. CỔNG THÀNH TRUNG TÂM (Gate Complex - Tâm cổng tại X = -2.2f)
            GameObject gateGroup = new GameObject("Gate_Complex");
            gateGroup.transform.SetParent(fortressRoot.transform);

            // Mái Vòm Cổng Thành Tiền Sử (Archway)
            if (archPrefab != null)
            {
                GameObject arch = (GameObject)PrefabUtility.InstantiatePrefab(archPrefab, gateGroup.transform);
                arch.name = "Gate_Overhead_Arch";
                arch.transform.position = new Vector3(-2.2f, 0.2f, wallZ);
                arch.transform.rotation = Quaternion.identity;
                arch.transform.localScale = new Vector3(0.74f, 0.75f, 0.7f);
            }

            // Đuốc Lửa Cổng Thành
            if (torchPrefab != null)
            {
                GameObject tl = (GameObject)PrefabUtility.InstantiatePrefab(torchPrefab, gateGroup.transform);
                tl.name = "Gate_Torch_Left";
                tl.transform.position = new Vector3(-4.5f, 0f, wallZ + 1.6f);
                tl.transform.localScale = Vector3.one * 1.3f;

                GameObject tr = (GameObject)PrefabUtility.InstantiatePrefab(torchPrefab, gateGroup.transform);
                tr.name = "Gate_Torch_Right";
                tr.transform.position = new Vector3(0.1f, 0f, wallZ + 1.6f);
                tr.transform.localScale = Vector3.one * 1.3f;
            }

            // Cánh Cổng Gỗ Kiên Cố Chắn Khủng Long (Rộng 6m, bao phủ từ X = -5.2f đến +0.8f)
            if (barricadePrefab != null)
            {
                GameObject gateBar = (GameObject)PrefabUtility.InstantiatePrefab(barricadePrefab, gateGroup.transform);
                gateBar.name = "Gate_Portcullis_Barricade";
                gateBar.transform.position = new Vector3(-2.2f, 0f, wallZ);
                gateBar.transform.rotation = Quaternion.identity;
                gateBar.transform.localScale = new Vector3(2.0f, 1.4f, 1.2f);
                if (barricadeSO != null && gateBar.TryGetComponent(out Unit.Unit ugb))
                {
                    ugb.UnitType = barricadeSO;
                    ugb.Health = 1000;
                    ugb.MaxHealth = 1000;
                    ugb.EnsureHealthBarAttached();
                }
                EnsureNavMeshObstacle(gateBar, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
            }

            // 2. TƯỜNG THÀNH CÁNH TRÁI KHÉP KÍN 100% (Left Wing Wall - Nối liền từ mép cổng X = -5.0f đến vách núi X = -32.0f)
            GameObject leftWallGroup = new GameObject("Left_Wing_Wall");
            leftWallGroup.transform.SetParent(fortressRoot.transform);

            // 15 đoạn tường gối chồng lên nhau 1.15m (mỗi đoạn rộng 3.75m, khoảng cách tâm 2.6m -> Tuyệt đối không có kẽ hở từ X = -5.0f đến X = -41.4f)
            float[] leftPositionsX = new float[] {
                -5.0f, -7.6f, -10.2f, -12.8f, -15.4f, -18.0f, -20.6f, -23.2f, -25.8f, -28.4f, -31.0f,
                -33.6f, -36.2f, -38.8f, -41.4f
            };
            for (int i = 0; i < leftPositionsX.Length; i++)
            {
                if (barricadePrefab != null)
                {
                    GameObject seg = (GameObject)PrefabUtility.InstantiatePrefab(barricadePrefab, leftWallGroup.transform);
                    seg.name = $"Left_Wall_Segment_{i + 1}";
                    seg.transform.position = new Vector3(leftPositionsX[i], 0f, wallZ);
                    seg.transform.rotation = Quaternion.identity;
                    seg.transform.localScale = new Vector3(1.25f, 1.25f, 1.2f);
                    if (barricadeSO != null && seg.TryGetComponent(out Unit.Unit uw))
                    {
                        uw.UnitType = barricadeSO;
                        uw.Health = 450;
                        uw.MaxHealth = 450;
                        uw.EnsureHealthBarAttached();
                    }
                    EnsureNavMeshObstacle(seg, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
                }
            }

            // Pháo Đài Góc Trái sát vách núi (X = -44.0f)
            if (towerPrefab != null)
            {
                GameObject leftBastion = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab, leftWallGroup.transform);
                leftBastion.name = "Left_Bastion_Tower";
                leftBastion.transform.position = new Vector3(-44.0f, 0f, wallZ);
                leftBastion.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
                leftBastion.transform.localScale = Vector3.one * 0.7f;
                EnsureBoxCollider(leftBastion, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
                EnsureNavMeshObstacle(leftBastion, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
            }

            // 3. TƯỜNG THÀNH CÁNH PHẢI KHÉP KÍN 100% (Right Wing Wall - Nối liền từ mép cổng X = +0.6f đến vách núi X = +28.0f)
            GameObject rightWallGroup = new GameObject("Right_Wing_Wall");
            rightWallGroup.transform.SetParent(fortressRoot.transform);

            // 11 đoạn tường gối chồng lên nhau 1.15m (mỗi đoạn rộng 3.75m, khoảng cách tâm 2.6m -> Tuyệt đối không có kẽ hở)
            float[] rightPositionsX = new float[] { 0.6f, 3.2f, 5.8f, 8.4f, 11.0f, 13.6f, 16.2f, 18.8f, 21.4f, 24.0f, 26.6f };
            for (int i = 0; i < rightPositionsX.Length; i++)
            {
                if (barricadePrefab != null)
                {
                    GameObject seg = (GameObject)PrefabUtility.InstantiatePrefab(barricadePrefab, rightWallGroup.transform);
                    seg.name = $"Right_Wall_Segment_{i + 1}";
                    seg.transform.position = new Vector3(rightPositionsX[i], 0f, wallZ);
                    seg.transform.rotation = Quaternion.identity;
                    seg.transform.localScale = new Vector3(1.25f, 1.25f, 1.2f);
                    if (barricadeSO != null && seg.TryGetComponent(out Unit.Unit uw))
                    {
                        uw.UnitType = barricadeSO;
                        uw.Health = 450;
                        uw.MaxHealth = 450;
                        uw.EnsureHealthBarAttached();
                    }
                    EnsureNavMeshObstacle(seg, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
                }
            }

            // Pháo Đài Góc Phải sát vách núi
            if (towerPrefab != null)
            {
                GameObject rightBastion = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab, rightWallGroup.transform);
                rightBastion.name = "Right_Bastion_Tower";
                rightBastion.transform.position = new Vector3(29.0f, 0f, wallZ);
                rightBastion.transform.rotation = Quaternion.Euler(0f, -30f, 0f);
                rightBastion.transform.localScale = Vector3.one * 0.7f;
                EnsureBoxCollider(rightBastion, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
                EnsureNavMeshObstacle(rightBastion, new Vector3(5f, 8.5f, 5f), new Vector3(0f, 4.25f, 0f));
            }

            // 4. THÊM 2 CHÒI CUNG THỦ TIỀN TIÊU Ở 2 BÊN PHÍA TRƯỚC LÀNG (Front Flanking Archer Watchtowers)
            if (watchtowerPrefab != null)
            {
                // Chòi Cung Thủ Bên Trái (Front Left Flank)
                GameObject wtLeft = (GameObject)PrefabUtility.InstantiatePrefab(watchtowerPrefab, fortressRoot.transform);
                wtLeft.name = "Village_Watchtower_Left";
                wtLeft.transform.position = new Vector3(-6.0f, 0f, -5.5f);
                wtLeft.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
                if (watchtowerSO != null && wtLeft.TryGetComponent(out Unit.Unit uwl))
                {
                    uwl.UnitType = watchtowerSO;
                    uwl.MaxHealth = 800;
                    uwl.Health = 800;
                    uwl.EnsureHealthBarAttached();
                }
                EnsureNavMeshObstacle(wtLeft, new Vector3(2.6f, 5.8f, 2.6f), new Vector3(0f, 2.9f, 0f));

                // Chòi Cung Thủ Bên Phải (Front Right Flank)
                GameObject wtRight = (GameObject)PrefabUtility.InstantiatePrefab(watchtowerPrefab, fortressRoot.transform);
                wtRight.name = "Village_Watchtower_Right";
                wtRight.transform.position = new Vector3(1.6f, 0f, -5.5f);
                wtRight.transform.rotation = Quaternion.Euler(0f, -15f, 0f);
                if (watchtowerSO != null && wtRight.TryGetComponent(out Unit.Unit uwr))
                {
                    uwr.UnitType = watchtowerSO;
                    uwr.MaxHealth = 800;
                    uwr.Health = 800;
                    uwr.EnsureHealthBarAttached();
                }
                EnsureNavMeshObstacle(wtRight, new Vector3(2.6f, 5.8f, 2.6f), new Vector3(0f, 2.9f, 0f));
            }

            // 5. BỔ SUNG BỨC TƯỜNG Ở TỌA ĐỘ (128, 0, -152) THEO YÊU CẦU NGƯỜI CHƠI ĐỂ LẮP CHỖ TRỐNG BÊN TRÁI
            if (barricadePrefab != null)
            {
                GameObject customGapWall = (GameObject)PrefabUtility.InstantiatePrefab(barricadePrefab, fortressRoot.transform);
                customGapWall.name = "Village_Left_Gap_Wall_128_neg152";
                customGapWall.transform.position = new Vector3(128f, 0f, -152f);
                customGapWall.transform.rotation = Quaternion.identity;
                customGapWall.transform.localScale = Vector3.one;
                if (barricadeSO != null && customGapWall.TryGetComponent(out Unit.Unit ucg))
                {
                    ucg.UnitType = barricadeSO;
                    ucg.Health = 450;
                    ucg.MaxHealth = 450;
                    ucg.EnsureHealthBarAttached();
                }
                EnsureBoxCollider(customGapWall, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
                EnsureNavMeshObstacle(customGapWall, new Vector3(3.2f, 2.0f, 1.4f), new Vector3(0f, 1.0f, 0f));
            }

            Debug.Log("<color=green>[Prehistoric TD]</color> Successfully constructed 100% gapless fortified Village Wall with Gate, 2 Front Archer Watchtowers, and Custom Left Gap Wall at (128, 0, -152)!");
            return fortressRoot;
        }

        private static NavMeshObstacle EnsureNavMeshObstacle(GameObject go, Vector3 size, Vector3 center)
        {
            if (!go.TryGetComponent(out NavMeshObstacle obs)) obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            obs.size = size;
            obs.center = center;
            obs.carving = true;
            obs.carveOnlyStationary = true;
            return obs;
        }

        private static void AlignObjectToGround(GameObject obj, float targetY)
        {
            if (obj == null) return;
            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            float lowestY = float.MaxValue;
            foreach (Renderer r in rends)
            {
                if (r == null || !r.enabled) continue;
                if (r.bounds.size.magnitude < 0.001f) continue;
                lowestY = Mathf.Min(lowestY, r.bounds.min.y);
            }

            if (lowestY < float.MaxValue && Mathf.Abs(lowestY - targetY) > 0.02f)
            {
                obj.transform.position += Vector3.up * (targetY - lowestY);
            }
        }

        private static Vector3 SnapToGround(Vector3 pos, float fallbackY = 0f)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(pos.x, 20f, pos.z), Vector3.down, 40f);
            if (hits != null && hits.Length > 0)
            {
                RaycastHit bestHit = hits[0];
                float minDiff = Mathf.Abs(hits[0].point.y - fallbackY);
                for (int i = 1; i < hits.Length; i++)
                {
                    float diff = Mathf.Abs(hits[i].point.y - fallbackY);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestHit = hits[i];
                    }
                }
                return bestHit.point;
            }
            return new Vector3(pos.x, fallbackY, pos.z);
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

        [MenuItem("Prehistoric TD/Bake All Custom Scene Walls", priority = 30)]
        public static void BakeAllCustomSceneWalls()
        {
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var barricadeSO = AssetDatabase.LoadAssetAtPath<Config.TowerSO>($"{CONFIG_DIR}/Tower_Barricade.asset");

            int count = 0;
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                string n = t.gameObject.name;

                // Bỏ qua nếu object là con cháu bên trong bất kỳ đối tượng Wall nào khác (ví dụ sub-mesh bên trong Prefab_Barricade)
                Unit.Wall parentWall = t.GetComponentInParent<Unit.Wall>(true);
                if (parentWall != null && parentWall.gameObject != t.gameObject)
                {
                    // Dọn dẹp nếu có lỡ dính component Wall / Obstacle trên node con
                    if (t.TryGetComponent(out Unit.Wall errWall)) Undo.DestroyObjectImmediate(errWall);
                    if (t.TryGetComponent(out NavMeshObstacle errObs)) Undo.DestroyObjectImmediate(errObs);
                    continue;
                }

                // Chỉ cấu hình cho các đối tượng có MeshRenderer / MeshFilter (bỏ qua các node transform rỗng như Plank_Defenses_low)
                if (!t.TryGetComponent(out MeshRenderer mr) && !t.TryGetComponent(out MeshFilter mf))
                {
                    if (t.TryGetComponent(out Unit.Wall errWall2)) Undo.DestroyObjectImmediate(errWall2);
                    if (t.TryGetComponent(out NavMeshObstacle errObs2)) Undo.DestroyObjectImmediate(errObs2);
                    continue;
                }

                if (n.Contains("Plank_Defenses") || n.Contains("wooden__barricade") || n.Contains("Village_Custom_Wall") || n.Contains("Village_Left_Gap_Wall"))
                {
                    GameObject go = t.gameObject;

                    Undo.RecordObject(go, "Bake Custom Wall");

                    // 1. BoxCollider
                    if (!go.TryGetComponent(out BoxCollider col))
                    {
                        col = Undo.AddComponent<BoxCollider>(go);
                    }
                    if (go.TryGetComponent(out MeshFilter mf2) && mf2.sharedMesh != null)
                    {
                        col.center = mf2.sharedMesh.bounds.center;
                        Vector3 ms = mf2.sharedMesh.bounds.size;
                        col.size = new Vector3(ms.x, ms.y, Mathf.Max(ms.z, 40f));
                    }
                    else
                    {
                        col.center = new Vector3(386.04f, 7.91f, 0.60f);
                        col.size = new Vector3(100f, 115f, 40f);
                    }

                    // 2. NavMeshObstacle
                    if (!go.TryGetComponent(out NavMeshObstacle obs))
                    {
                        obs = Undo.AddComponent<NavMeshObstacle>(go);
                    }
                    obs.shape = NavMeshObstacleShape.Box;
                    obs.size = col.size;
                    obs.center = col.center;
                    obs.carving = true;
                    obs.carveOnlyStationary = false;

                    // 3. Wall component
                    if (!go.TryGetComponent(out Unit.Wall wall))
                    {
                        wall = Undo.AddComponent<Unit.Wall>(go);
                    }
                    wall.MaxHealth = 450;
                    wall.Health = 450;
                    if (barricadeSO != null)
                    {
                        wall.UnitType = barricadeSO;
                    }

                    EditorUtility.SetDirty(go);
                    count++;
                }
            }

            if (count > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                Debug.Log($"<color=green>[Prehistoric TD]</color> Đã nướng (Bake) thành công Collider, NavMeshObstacle & Wall cho {count} bức tường chắn trong scene!");
                EditorUtility.DisplayDialog("Bake Walls Thành Công", $"Đã tích hợp thành công {count} bức tường tự đặt vào hệ thống phòng thủ và chặn đường NavMesh!", "OK");
            }
            else
            {
                Debug.Log("<color=yellow>[Prehistoric TD]</color> Không tìm thấy bức tường chắn tự thêm nào cần cấu hình.");
            }
        }
    }
}
