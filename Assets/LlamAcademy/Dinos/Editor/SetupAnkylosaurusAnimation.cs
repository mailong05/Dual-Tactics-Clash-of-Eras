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
    public static class SetupAnkylosaurusAnimation
    {
        static SetupAnkylosaurusAnimation()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("AnkyAnimExtracted", false))
                {
                    SessionState.SetBool("AnkyAnimExtracted", true);
                    ExtractAndSetup();
                }
            };
        }
        private const string GLB_PATH = "Assets/ankylosaurus_updated.glb";
        private const string ANIM_SAVE_PATH = "Assets/Resources/Animations/Ankylosaurus_Walk.anim";
        private const string CONTROLLER_PATH = "Assets/Resources/Animations/Ankylosaurus_Controller.controller";
        private const string PREFAB_PATH = "Assets/Prefabs/Prehistoric/Prefab_Ankylosaurus.prefab";
        private const string RES_PREFAB_PATH = "Assets/Resources/Prefabs/Prehistoric/Prefab_Ankylosaurus.prefab";

        [MenuItem("Tools/Prehistoric TD/Setup Ankylosaurus Animation (Complete Fix)")]
        public static async void ExtractAndSetup()
        {
            try
            {
                Debug.Log("<color=cyan>[AnkylosaurusSetup]</color> Starting Ankylosaurus animation extraction & setup...");

                // 1. Load GLB via glTFast
                var gltf = new GltfImport();
                var importSettings = new ImportSettings
                {
                    AnimationMethod = AnimationMethod.Legacy
                };

                bool success = await gltf.LoadFile(GLB_PATH, importSettings: importSettings);
                if (!success)
                {
                    Debug.LogError("[AnkylosaurusSetup] Failed to load ankylosaurus_updated.glb via glTFast!");
                    return;
                }

                var rawClips = gltf.GetAnimationClips();
                if (rawClips == null || rawClips.Length == 0)
                {
                    Debug.LogError("[AnkylosaurusSetup] No animation clips found in ankylosaurus_updated.glb!");
                    return;
                }

                AnimationClip sourceClip = rawClips[0];
                Debug.Log($"[AnkylosaurusSetup] Successfully extracted raw clip: {sourceClip.name}, length: {sourceClip.length}s");

                // 2. Build clean AnimationClip with adjusted relative paths
                AnimationClip cleanClip = new AnimationClip();
                cleanClip.name = "Ankylosaurus_Walk";
                cleanClip.legacy = false; // Compatible with Mecanim AnimatorController
                cleanClip.wrapMode = WrapMode.Loop;

                var bindings = AnimationUtility.GetCurveBindings(sourceClip);
                Debug.Log($"[AnkylosaurusSetup] Total curve bindings: {bindings.Length}");

                foreach (var b in bindings)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, b);
                    string targetPath = b.path;

                    // Strip root node name ("Sketchfab_model/") if present
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

                // Set clip loop settings
                var settings = AnimationUtility.GetAnimationClipSettings(cleanClip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(cleanClip, settings);

                // Save cleanClip as asset
                string animDir = Path.GetDirectoryName(ANIM_SAVE_PATH);
                if (!Directory.Exists(animDir)) Directory.CreateDirectory(animDir);

                AssetDatabase.CreateAsset(cleanClip, ANIM_SAVE_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>[AnkylosaurusSetup]</color> Saved clean AnimationClip to: {ANIM_SAVE_PATH}");

                // 3. Setup Ankylosaurus_Controller.controller
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

                var rootSm = controller.layers[0].stateMachine;

                // Find or add Walk state
                AnimatorState walkState = null;
                foreach (var s in rootSm.states)
                {
                    if (s.state.name.ToLower() == "walk")
                    {
                        walkState = s.state;
                        break;
                    }
                }

                if (walkState == null)
                {
                    walkState = rootSm.AddState("Walk");
                }

                walkState.motion = cleanClip;
                walkState.speed = 1.6f; // Walking pace appropriate for heavy siege tank
                rootSm.defaultState = walkState;

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=green>[AnkylosaurusSetup]</color> Ankylosaurus_Controller updated with Walk motion!");

                // 4. Update Prefabs (Assets/Prefabs/Prehistoric & Assets/Resources/Prefabs/Prehistoric)
                UpdateAnkylosaurusPrefab(PREFAB_PATH, controller, cleanClip);
                if (File.Exists(RES_PREFAB_PATH))
                {
                    UpdateAnkylosaurusPrefab(RES_PREFAB_PATH, controller, cleanClip);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("<color=green>[AnkylosaurusSetup] SUCCESS:</color> Ankylosaurus Animation & Prefabs completely fixed!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AnkylosaurusSetup] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void UpdateAnkylosaurusPrefab(string prefabPath, AnimatorController controller, AnimationClip walkClip)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform modelT = root.transform.Find("Model");
                GameObject targetGo = modelT != null ? modelT.gameObject : root;

                Animator anim = targetGo.GetComponent<Animator>();
                if (anim == null) anim = targetGo.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.enabled = true;

                Animation legacyAnim = targetGo.GetComponent<Animation>();
                if (legacyAnim == null) legacyAnim = targetGo.AddComponent<Animation>();
                legacyAnim.clip = walkClip;
                legacyAnim.playAutomatically = true;
                legacyAnim.wrapMode = WrapMode.Loop;
                legacyAnim.enabled = true;

                SiegeDino siege = root.GetComponent<SiegeDino>();
                if (siege != null)
                {
                    SerializedObject so = new SerializedObject(siege);
                    so.FindProperty("ModelAnimator").objectReferenceValue = anim;
                    so.FindProperty("ModelLegacyAnimation").objectReferenceValue = legacyAnim;
                    so.FindProperty("VisualModel").objectReferenceValue = targetGo.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"<color=cyan>[AnkylosaurusSetup]</color> Successfully configured prefab: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
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
