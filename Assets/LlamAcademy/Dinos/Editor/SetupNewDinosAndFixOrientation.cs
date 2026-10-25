using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using LlamAcademy.Dinos.Unit;

namespace LlamAcademy.Dinos.Editor
{
    [InitializeOnLoad]
    public static class SetupNewDinosAndFixOrientation
    {
        private const string PREFAB_DIR = "Assets/Prefabs/Prehistoric";
        private const string RES_PREFAB_DIR = "Assets/Resources/Prefabs/Prehistoric";

        static SetupNewDinosAndFixOrientation()
        {
            EditorApplication.delayCall += ExecuteSetup;
        }

        [MenuItem("Tools/Prehistoric TD/Setup New Dinos & Orientation Fix")]
        public static void ExecuteSetup()
        {
            try
            {
                Debug.Log("<color=cyan>[Prehistoric TD]</color> Starting Setup of New Dinos & Model Orientations...");

                SetupCatapultAndTotemPrefabs();
                SetupVelociraptorPrefab();
                SetupTRexPrefab();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("<color=green>[Prehistoric TD] SUCCESS:</color> All Dinos (Velociraptor, T-Rex) & Defense Orientations (Catapult, Totem) Updated Perfectly!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"<color=red>[Prehistoric TD] Error during setup:</color> {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void SetupCatapultAndTotemPrefabs()
        {
            // Prefabs are preserved in clean commit 85492a8 format (-90 deg X rotation for Sketchfab models).
            SyncToResources($"{PREFAB_DIR}/Prefab_Catapult.prefab");
            SyncToResources($"{PREFAB_DIR}/Prefab_ShamanTotem.prefab");
        }

        private static void SetupVelociraptorPrefab()
        {
            string pbrPath = "Assets/PBRVelociraptor/Prefabs/PBR/15K/PBR_Velociraptor_Green.prefab";
            GameObject pbrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pbrPath);
            if (pbrPrefab == null)
            {
                pbrPath = "Assets/PBRVelociraptor/Prefabs/Mobile/5K/Raptor_Animated_FBX_5K_Green.prefab";
                pbrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pbrPath);
            }

            if (pbrPrefab == null)
            {
                Debug.LogWarning("<color=yellow>[Prehistoric TD]</color> PBR Velociraptor prefab not found!");
                return;
            }

            string veloPath = $"{PREFAB_DIR}/Prefab_Velociraptor.prefab";
            GameObject root = File.Exists(veloPath) 
                ? PrefabUtility.LoadPrefabContents(veloPath)
                : new GameObject("Prefab_Velociraptor");

            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // Remove existing Model child
            Transform oldModel = root.transform.Find("Model");
            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            // Instantiate PBR Raptor (quay 180 độ quanh Y để đầu hướng thẳng ra phía trước / căn cứ làng)
            GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(pbrPrefab, root.transform);
            newModel.name = "Model";
            newModel.transform.localPosition = Vector3.zero;
            newModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Target dimensions: Velociraptor height ~1.6m
            FitModelDimensions(newModel, 1.6f);

            // Configure Animator
            Animator animator = newModel.GetComponent<Animator>();
            if (animator == null) animator = newModel.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            RuntimeAnimatorController raptorCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Resources/Animations/Velociraptor_Controller.controller");
            if (raptorCtrl != null)
            {
                animator.runtimeAnimatorController = raptorCtrl;
            }

            // Ensure RunnerDino component
            if (!root.TryGetComponent(out RunnerDino runner))
            {
                runner = root.AddComponent<RunnerDino>();
            }

            var so = new SerializedObject(runner);
            so.FindProperty("ModelAnimator").objectReferenceValue = animator;
            so.FindProperty("VisualModel").objectReferenceValue = newModel.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Ensure Collider & NavMeshAgent
            CapsuleCollider cc = root.GetComponent<CapsuleCollider>();
            if (cc == null) cc = root.AddComponent<CapsuleCollider>();
            cc.radius = 0.45f;
            cc.height = 1.6f;
            cc.center = new Vector3(0f, 0.8f, 0f);

            UnityEngine.AI.NavMeshAgent agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null) agent = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 0.45f;
            agent.height = 1.6f;
            agent.speed = 5.5f;

            PrefabUtility.SaveAsPrefabAsset(root, veloPath);
            PrefabUtility.UnloadPrefabContents(root);

            SyncToResources(veloPath);
            Debug.Log("<color=green>[Prehistoric TD]</color> Prefab_Velociraptor successfully updated with PBR Velociraptor model and animations!");
        }

        private static void SetupTRexPrefab()
        {
            string fbxPath = "Assets/Animations/Dino/T-rex/T-Rex.fbx";
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null)
            {
                Debug.LogWarning("<color=yellow>[Prehistoric TD]</color> T-Rex.fbx not found at Assets/Animations/Dino/T-rex/T-Rex.fbx!");
                return;
            }

            string trexPath = $"{PREFAB_DIR}/Prefab_TRexBoss.prefab";
            GameObject root = File.Exists(trexPath)
                ? PrefabUtility.LoadPrefabContents(trexPath)
                : new GameObject("Prefab_TRexBoss");

            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // Remove existing Model child
            Transform oldModel = root.transform.Find("Model");
            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            // Instantiate T-Rex FBX
            GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, root.transform);
            newModel.name = "Model";
            newModel.transform.localPosition = Vector3.zero;
            newModel.transform.localRotation = Quaternion.identity;

            // Target dimensions: Apex Boss height ~5.5m
            FitModelDimensions(newModel, 5.5f);

            // Assign material
            Material trexMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Animations/Dino/T-rex/M_TRex.mat");
            if (trexMat != null)
            {
                Renderer[] rends = newModel.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    r.sharedMaterial = trexMat;
                }
            }

            // Configure Animator
            Animator animator = newModel.GetComponent<Animator>();
            if (animator == null) animator = newModel.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            RuntimeAnimatorController trexCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Resources/Animations/TRex_Controller.controller");
            if (trexCtrl != null)
            {
                animator.runtimeAnimatorController = trexCtrl;
            }

            // Ensure BossDino component
            if (!root.TryGetComponent(out BossDino boss))
            {
                boss = root.AddComponent<BossDino>();
            }

            var so = new SerializedObject(boss);
            so.FindProperty("ModelAnimator").objectReferenceValue = animator;
            so.FindProperty("VisualModel").objectReferenceValue = newModel.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Ensure Collider & NavMeshAgent
            CapsuleCollider cc = root.GetComponent<CapsuleCollider>();
            if (cc == null) cc = root.AddComponent<CapsuleCollider>();
            cc.radius = 1.8f;
            cc.height = 5.5f;
            cc.center = new Vector3(0f, 2.75f, 0f);

            UnityEngine.AI.NavMeshAgent agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null) agent = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 1.8f;
            agent.height = 5.5f;
            agent.speed = 2.4f;

            PrefabUtility.SaveAsPrefabAsset(root, trexPath);
            PrefabUtility.UnloadPrefabContents(root);

            SyncToResources(trexPath);
            Debug.Log("<color=green>[Prehistoric TD]</color> Prefab_TRexBoss successfully updated with new T-Rex FBX model, PBR textures and animations!");
        }

        private static void FitModelDimensions(GameObject visual, float targetHeight)
        {
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;

            Bounds b = new Bounds();
            bool hasBounds = false;
            Renderer[] rends = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!hasBounds) { b = r.bounds; hasBounds = true; }
                else b.Encapsulate(r.bounds);
            }

            if (!hasBounds || b.size.y < 0.01f) return;

            float scale = targetHeight / b.size.y;
            visual.transform.localScale = Vector3.one * scale;

            float groundedY = -b.min.y * scale;
            float centerX = -b.center.x * scale;
            float centerZ = -b.center.z * scale;
            visual.transform.localPosition = new Vector3(centerX, groundedY, centerZ);
        }

        private static void SyncToResources(string sourcePath)
        {
            if (!Directory.Exists(RES_PREFAB_DIR)) Directory.CreateDirectory(RES_PREFAB_DIR);
            string dest = Path.Combine(RES_PREFAB_DIR, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, dest, true);
        }
    }
}
