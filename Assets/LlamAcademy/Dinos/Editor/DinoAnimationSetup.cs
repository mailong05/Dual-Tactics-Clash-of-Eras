using System.Collections.Generic;
using System.IO;
using System.Linq;
using LlamAcademy.Dinos.Unit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LlamAcademy.Dinos.Editor
{
    [InitializeOnLoad]
    public static class DinoAnimationSetup
    {
        private const string ANIM_DIR = "Assets/Resources/Animations";

        static DinoAnimationSetup()
        {
            EditorApplication.delayCall += ExecuteSetup;
        }

        [MenuItem("Tools/Prehistoric TD/Setup All Dino Animations")]
        public static void ExecuteSetup()
        {
            if (!Directory.Exists(ANIM_DIR))
            {
                Directory.CreateDirectory(ANIM_DIR);
                AssetDatabase.Refresh();
            }

            SetupVelociraptor();
            SetupAnkylosaurus();
            SetupTRex();
            SetupPterodactyl();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[DinoAnimationSetup]</color> Đã thiết lập xong toàn bộ AnimatorController và gán Animation cho 4 loài khủng long!");
        }

        private static void SetupVelociraptor()
        {
            string glbPath = "Assets/velociraptor.glb";
            string controllerPath = $"{ANIM_DIR}/Velociraptor_Controller.controller";
            string prefabPath = "Assets/Prefabs/Prehistoric/Prefab_Velociraptor.prefab";

            var clips = LoadClipsFromAsset(glbPath);
            AnimationClip walkClip = FindClip(clips, "walk1", "walk2", "walk") ?? clips.FirstOrDefault();
            AnimationClip runClip = FindClip(clips, "run") ?? walkClip;
            AnimationClip attackClip = FindClip(clips, "roar", "eat", "attack") ?? walkClip;

            SetClipLooping(walkClip, true);
            SetClipLooping(runClip, true);

            AnimatorController controller = GetOrCreateController(controllerPath);
            SetupControllerStates(controller, walkClip, runClip, attackClip);

            AssignControllerToPrefab(prefabPath, controller);
        }

        private static void SetupAnkylosaurus()
        {
            string glbPath = "Assets/ankylosaurus_updated.glb";
            string controllerPath = $"{ANIM_DIR}/Ankylosaurus_Controller.controller";
            string prefabPath = "Assets/Prefabs/Prehistoric/Prefab_Ankylosaurus.prefab";

            var clips = LoadClipsFromAsset(glbPath);
            AnimationClip moveClip = clips.FirstOrDefault();
            SetClipLooping(moveClip, true);

            AnimatorController controller = GetOrCreateController(controllerPath);
            SetupSingleStateController(controller, "Walk", moveClip);

            AssignControllerToPrefab(prefabPath, controller);
        }

        private static void SetupTRex()
        {
            string glbPath = "Assets/animated_t-rex_dinosaur_biting_attack_loop.glb";
            string controllerPath = $"{ANIM_DIR}/TRex_Controller.controller";
            string prefabPath = "Assets/Prefabs/Prehistoric/Prefab_TRexBoss.prefab";

            var clips = LoadClipsFromAsset(glbPath);
            AnimationClip attackRunClip = clips.FirstOrDefault();
            SetClipLooping(attackRunClip, true);

            AnimatorController controller = GetOrCreateController(controllerPath);
            SetupSingleStateController(controller, "AttackRun", attackRunClip);

            AssignControllerToPrefab(prefabPath, controller);
        }

        private static void SetupPterodactyl()
        {
            string glbPath = "Assets/pterodactyl_1.glb";
            string controllerPath = $"{ANIM_DIR}/Pterodactyl_Controller.controller";
            string prefabPath = "Assets/Prefabs/Prehistoric/Prefab_Pterodactyl.prefab";

            var clips = LoadClipsFromAsset(glbPath);
            AnimationClip flyClip = FindClip(clips, "flying", "fly") ?? clips.FirstOrDefault();
            AnimationClip walkClip = FindClip(clips, "walking", "walk") ?? flyClip;

            SetClipLooping(flyClip, true);
            SetClipLooping(walkClip, true);

            AnimatorController controller = GetOrCreateController(controllerPath);
            SetupControllerStates(controller, flyClip, flyClip, walkClip);

            AssignControllerToPrefab(prefabPath, controller);
        }

        private static List<AnimationClip> LoadClipsFromAsset(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var clips = new List<AnimationClip>();
            if (assets == null) return clips;

            foreach (var a in assets)
            {
                if (a is AnimationClip clip)
                {
                    clips.Add(clip);
                }
            }
            return clips;
        }

        private static AnimationClip FindClip(List<AnimationClip> clips, params string[] names)
        {
            foreach (var n in names)
            {
                var match = clips.FirstOrDefault(c => c.name.ToLower().Contains(n.ToLower()));
                if (match != null) return match;
            }
            return null;
        }

        private static void SetClipLooping(AnimationClip clip, bool loop)
        {
            if (clip == null) return;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        private static AnimatorController GetOrCreateController(string path)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            return controller;
        }

        private static void SetupSingleStateController(AnimatorController controller, string stateName, AnimationClip clip)
        {
            if (controller == null || clip == null) return;

            // Clear existing parameters & layers
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;
            var states = rootStateMachine.states;
            AnimatorState state = null;

            foreach (var s in states)
            {
                if (s.state.name == stateName)
                {
                    state = s.state;
                    break;
                }
            }

            if (state == null)
            {
                state = rootStateMachine.AddState(stateName);
            }

            state.motion = clip;
            rootStateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
        }

        private static void SetupControllerStates(AnimatorController controller, AnimationClip walkClip, AnimationClip runClip, AnimationClip attackClip)
        {
            if (controller == null) return;

            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Walk", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Run", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "IsAttacking", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            // Clear old states to avoid duplicate spaghetti transitions
            var oldStates = sm.states;
            foreach (var st in oldStates)
            {
                sm.RemoveState(st.state);
            }

            var walkState = sm.AddState("Walk");
            walkState.motion = walkClip;

            var runState = sm.AddState("Run");
            runState.motion = runClip;

            sm.defaultState = walkState;

            // Transition: Walk -> Run (Speed > 3.0)
            var toRun = walkState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.2f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 3.0f, "Speed");

            // Transition: Run -> Walk (Speed <= 3.0)
            var toWalk = runState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.2f;
            toWalk.AddCondition(AnimatorConditionMode.Less, 3.01f, "Speed");

            if (attackClip != null)
            {
                var attackState = sm.AddState("Attack");
                attackState.motion = attackClip;

                var anyToAttack = sm.AddAnyStateTransition(attackState);
                anyToAttack.hasExitTime = false;
                anyToAttack.duration = 0.15f;
                anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");

                var backFromAttack = attackState.AddTransition(walkState);
                backFromAttack.hasExitTime = true;
                backFromAttack.exitTime = 0.85f;
                backFromAttack.duration = 0.2f;
            }

            EditorUtility.SetDirty(controller);
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(p => p.name == name))
            {
                controller.AddParameter(name, type);
            }
        }

        private static void AssignControllerToPrefab(string prefabPath, AnimatorController controller)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Transform modelT = instance.transform.Find("Model");
                GameObject targetGo = modelT != null ? modelT.gameObject : instance;

                Animator anim = targetGo.GetComponent<Animator>();
                if (anim == null)
                {
                    anim = targetGo.AddComponent<Animator>();
                }

                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.enabled = true;

                // Cập nhật field ModelAnimator trên PrehistoricDinoBase
                PrehistoricDinoBase dinoBase = instance.GetComponent<PrehistoricDinoBase>();
                if (dinoBase != null)
                {
                    SerializedObject so = new SerializedObject(dinoBase);
                    SerializedProperty animProp = so.FindProperty("ModelAnimator");
                    if (animProp != null)
                    {
                        animProp.objectReferenceValue = anim;
                        so.ApplyModifiedProperties();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log($"<color=cyan>[DinoAnimationSetup]</color> Gán AnimatorController thành công cho: <b>{prefab.name}</b>!");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
