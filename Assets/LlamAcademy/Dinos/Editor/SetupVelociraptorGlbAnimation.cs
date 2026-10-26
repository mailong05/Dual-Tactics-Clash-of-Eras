using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using LlamAcademy.Dinos.Unit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LlamAcademy.Dinos.Editor
{
    [InitializeOnLoad]
    public static class SetupVelociraptorGlbAnimation
    {
        private const string GLB_PATH = "Assets/velociraptor.glb";
        private const string ANIM_DIR = "Assets/Resources/Animations";
        private const string CONTROLLER_PATH = "Assets/Resources/Animations/Velociraptor_Controller.controller";
        private const string PREFAB_PATH = "Assets/Prefabs/Prehistoric/Prefab_Velociraptor.prefab";
        private const string RES_PREFAB_PATH = "Assets/Resources/Prefabs/Prehistoric/Prefab_Velociraptor.prefab";

        static SetupVelociraptorGlbAnimation()
        {
            // Migrated to ConvertVelociraptorToNative to ensure standalone player build visibility
        }

        [MenuItem("Tools/Prehistoric TD/Setup Velociraptor from GLB (File User Gui)")]
        public static async void ExtractAndSetup()
        {
            try
            {
                Debug.Log("<color=cyan>[VelociraptorGlbSetup]</color> Extracting animation and model from user's velociraptor.glb...");

                if (!Directory.Exists(ANIM_DIR)) Directory.CreateDirectory(ANIM_DIR);

                // 1. Load GLB via glTFast
                var gltf = new GltfImport();
                var importSettings = new ImportSettings
                {
                    AnimationMethod = AnimationMethod.Legacy
                };

                bool success = await gltf.LoadFile(GLB_PATH, importSettings: importSettings);
                if (!success)
                {
                    Debug.LogError("[VelociraptorGlbSetup] Failed to load velociraptor.glb via glTFast!");
                    return;
                }

                var rawClips = gltf.GetAnimationClips();
                if (rawClips == null || rawClips.Length == 0)
                {
                    Debug.LogError("[VelociraptorGlbSetup] No animation clips found in velociraptor.glb!");
                    return;
                }

                Debug.Log($"[VelociraptorGlbSetup] Found {rawClips.Length} clips in velociraptor.glb: " +
                          string.Join(", ", rawClips.Select(c => c.name)));

                // Map clips: roar, eat, walk1, walk2, run
                AnimationClip rawWalk = rawClips.FirstOrDefault(c => c.name.ToLower().Contains("walk1")) 
                                     ?? rawClips.FirstOrDefault(c => c.name.ToLower().Contains("walk"));
                AnimationClip rawRun = rawClips.FirstOrDefault(c => c.name.ToLower().Contains("run")) ?? rawWalk;
                AnimationClip rawAttack = rawClips.FirstOrDefault(c => c.name.ToLower().Contains("roar"))
                                       ?? rawClips.FirstOrDefault(c => c.name.ToLower().Contains("eat")) ?? rawWalk;

                AnimationClip cleanWalk = CreateCleanClip(rawWalk, "Velociraptor_Walk", true);
                AnimationClip cleanRun = CreateCleanClip(rawRun, "Velociraptor_Run", true);
                AnimationClip cleanAttack = CreateCleanClip(rawAttack, "Velociraptor_Attack", false);

                string walkPath = $"{ANIM_DIR}/Velociraptor_Walk.anim";
                string runPath = $"{ANIM_DIR}/Velociraptor_Run.anim";
                string attackPath = $"{ANIM_DIR}/Velociraptor_Attack.anim";

                SaveClipAsset(cleanWalk, walkPath);
                SaveClipAsset(cleanRun, runPath);
                SaveClipAsset(cleanAttack, attackPath);

                AssetDatabase.SaveAssets();

                // 2. Setup AnimatorController
                AnimatorController controller = SetupAnimatorController(cleanWalk, cleanRun, cleanAttack);

                // 3. Update Prefab with velociraptor.glb model
                UpdateVelociraptorPrefab(PREFAB_PATH, controller);
                if (File.Exists(RES_PREFAB_PATH))
                {
                    UpdateVelociraptorPrefab(RES_PREFAB_PATH, controller);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("<color=green>[VelociraptorGlbSetup] SUCCESS:</color> Velociraptor model and animations updated 100% from user's velociraptor.glb!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VelociraptorGlbSetup] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static AnimationClip CreateCleanClip(AnimationClip sourceClip, string clipName, bool loop)
        {
            if (sourceClip == null) return null;

            AnimationClip cleanClip = new AnimationClip();
            cleanClip.name = clipName;
            cleanClip.legacy = false;
            cleanClip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

            var bindings = AnimationUtility.GetCurveBindings(sourceClip);
            foreach (var b in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, b);
                string targetPath = b.path;

                if (targetPath.StartsWith("Sketchfab_model/"))
                {
                    targetPath = targetPath.Substring("Sketchfab_model/".Length);
                }
                else if (targetPath == "Sketchfab_model")
                {
                    targetPath = "";
                }

                EditorCurveBinding cleanBinding = new EditorCurveBinding
                {
                    path = targetPath,
                    type = b.type,
                    propertyName = b.propertyName
                };

                AnimationUtility.SetEditorCurve(cleanClip, cleanBinding, curve);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(cleanClip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(cleanClip, settings);

            return cleanClip;
        }

        private static void SaveClipAsset(AnimationClip clip, string path)
        {
            if (clip == null) return;
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
        }

        private static AnimatorController SetupAnimatorController(AnimationClip walkClip, AnimationClip runClip, AnimationClip attackClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
            }

            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Forward", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Walk", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Run", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "IsAttacking", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);

            var rootSm = controller.layers[0].stateMachine;

            // Clear old states
            var existingStates = rootSm.states.ToList();
            foreach (var s in existingStates)
            {
                rootSm.RemoveState(s.state);
            }

            // Create States
            var idleState = rootSm.AddState("Idle");
            idleState.motion = walkClip;
            idleState.speed = 0.05f; // subtle natural idle pose/breathing

            var walkState = rootSm.AddState("Walk");
            walkState.motion = walkClip;
            walkState.speed = 1.35f;

            var runState = rootSm.AddState("Run");
            runState.motion = runClip != null ? runClip : walkClip;
            runState.speed = 1.6f;

            var attackState = rootSm.AddState("Attack");
            attackState.motion = attackClip != null ? attackClip : walkClip;
            attackState.speed = 1.35f;

            rootSm.defaultState = idleState;

            // Transitions:
            // Idle -> Walk
            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.12f;

            // Walk -> Idle
            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.15f;

            // Walk -> Run
            var walkToRun = walkState.AddTransition(runState);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed");
            walkToRun.hasExitTime = false;
            walkToRun.duration = 0.15f;

            // Run -> Walk
            var runToWalk = runState.AddTransition(walkState);
            runToWalk.AddCondition(AnimatorConditionMode.Less, 3.5f, "Speed");
            runToWalk.hasExitTime = false;
            runToWalk.duration = 0.15f;

            // Run -> Idle
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.15f;

            // AnyState -> Attack
            var anyToAttack = rootSm.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.08f;
            anyToAttack.canTransitionToSelf = false;

            // Attack -> Walk
            var attackToWalk = attackState.AddTransition(walkState);
            attackToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            attackToWalk.hasExitTime = true;
            attackToWalk.exitTime = 0.85f;
            attackToWalk.duration = 0.12f;

            // Attack -> Idle
            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.85f;
            attackToIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void UpdateVelociraptorPrefab(string prefabPath, AnimatorController controller)
        {
            GameObject glbSource = AssetDatabase.LoadAssetAtPath<GameObject>(GLB_PATH);
            if (glbSource == null)
            {
                Debug.LogError($"[VelociraptorGlbSetup] Cannot load {GLB_PATH} as GameObject!");
                return;
            }

            GameObject root = File.Exists(prefabPath)
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject("Prefab_Velociraptor");

            try
            {
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                // Remove old Model
                Transform oldModel = root.transform.Find("Model");
                if (oldModel != null)
                {
                    Object.DestroyImmediate(oldModel.gameObject);
                }

                // Instantiate GLB model
                GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(glbSource, root.transform);
                newModel.name = "Model";
                newModel.transform.localPosition = Vector3.zero;
                newModel.transform.localRotation = Quaternion.identity; // Head naturally faces +Z in glb

                // Fit dimensions: Velociraptor height ~1.6m
                FitModelDimensions(newModel, 1.6f);

                // Setup Animator on Model
                Animator animator = newModel.GetComponent<Animator>();
                if (animator == null) animator = newModel.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;

                // Remove duplicate Animation (Legacy) if any
                Animation legacy = newModel.GetComponent<Animation>();
                if (legacy != null) Object.DestroyImmediate(legacy);

                // Ensure RunnerDino
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

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"<color=cyan>[VelociraptorGlbSetup]</color> Prefab successfully saved: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FitModelDimensions(GameObject model, float targetHeight)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float currentHeight = bounds.size.y;
            if (currentHeight > 0.05f)
            {
                float scaleFactor = targetHeight / currentHeight;
                model.transform.localScale = Vector3.one * scaleFactor;
            }
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(p => p.name == name))
            {
                controller.AddParameter(name, type);
            }
        }
    }
}
