using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using LlamAcademy.Dinos.Unit;

namespace LlamAcademy.Dinos.Editor
{
    [InitializeOnLoad]
    public static class FixAllIssues
    {
        private const string PREFAB_DIR = "Assets/Prefabs/Prehistoric";
        private const string RES_PREFAB_DIR = "Assets/Resources/Prefabs/Prehistoric";

        static FixAllIssues()
        {
            EditorApplication.delayCall += RunFixes;
        }

        [MenuItem("Tools/Prehistoric TD/Run Fix All Issues Now")]
        public static void RunFixes()
        {
            try
            {
                Debug.Log("<color=cyan>[FixAllIssues]</color> Starting comprehensive fix for Velociraptor, T-Rex, Catapult & Totem...");

                FixPbrVelociraptorMaterials();
                FixVelociraptorPrefabAndController();
                FixTRexControllerAndDeploy();
                FixCatapultAndTotemOrientation();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("<color=green>[FixAllIssues] ALL FIXES APPLIED SUCCESSFULLY!</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FixAllIssues] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void FixPbrVelociraptorMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[FixAllIssues] Could not find Universal Render Pipeline/Lit shader!");
                return;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/PBRVelociraptor" });
            int fixedCount = 0;

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                bool isCornea = mat.name.IndexOf("cornea", StringComparison.OrdinalIgnoreCase) >= 0;

                // Extract textures from old standard material
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                Texture occlMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
                Color col = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : col;
                float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.4f;

                mat.shader = urpLit;

                // Re-assign to URP properties
                Texture textureToUse = baseMap != null ? baseMap : mainTex;
                if (textureToUse != null)
                {
                    mat.SetTexture("_BaseMap", textureToUse);
                }

                Color colorToUse = (baseCol != Color.white) ? baseCol : col;
                mat.SetColor("_BaseColor", colorToUse);

                if (bumpMap != null)
                {
                    mat.SetTexture("_BumpMap", bumpMap);
                    mat.EnableKeyword("_NORMALMAP");
                }

                if (occlMap != null)
                {
                    mat.SetTexture("_OcclusionMap", occlMap);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                }

                mat.SetFloat("_Smoothness", smoothness);

                if (isCornea)
                {
                    // Transparent cornea
                    mat.SetFloat("_Surface", 1); // 1 = Transparent
                    mat.SetFloat("_Blend", 0);   // Alpha blend
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = 3000;
                    mat.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f, 0.1f));
                }
                else
                {
                    mat.SetFloat("_Surface", 0); // 0 = Opaque
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.renderQueue = 2000;
                }

                EditorUtility.SetDirty(mat);
                fixedCount++;
            }

            Debug.Log($"<color=green>[FixAllIssues]</color> Successfully converted {fixedCount} Velociraptor materials to URP Lit (NO MORE PINK!).");
        }

        public static void FixVelociraptorPrefabAndController()
        {
            string fbxPath = "Assets/PBRVelociraptor/Models/15K/Raptor_Animated_FBX.fbx";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            // 1. Setup Controller
            string ctrlPath = "Assets/Resources/Animations/Velociraptor_Controller.controller";
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            }

            // Find clips from FBX
            AnimationClip idleClip = null;
            AnimationClip walkClip = null;
            AnimationClip runClip = null;
            AnimationClip biteClip = null;
            AnimationClip deathClip = null;

            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in allAssets)
            {
                if (a is AnimationClip clip)
                {
                    string cn = clip.name.ToLower();
                    if (cn.Contains("idle1")) idleClip = clip;
                    else if (cn.Contains("walk")) walkClip = clip;
                    else if (cn.Contains("run1")) runClip = clip;
                    else if (cn.Contains("bite1")) biteClip = clip;
                    else if (cn.Contains("death1")) deathClip = clip;
                }
            }

            // Fallback search if not found by exact name
            if (idleClip == null || walkClip == null || runClip == null)
            {
                foreach (var a in allAssets)
                {
                    if (a is AnimationClip clip)
                    {
                        string cn = clip.name.ToLower();
                        if (idleClip == null && cn.Contains("idle")) idleClip = clip;
                        if (walkClip == null && cn.Contains("walk")) walkClip = clip;
                        if (runClip == null && cn.Contains("run")) runClip = clip;
                        if (biteClip == null && (cn.Contains("bite") || cn.Contains("attack"))) biteClip = clip;
                        if (deathClip == null && (cn.Contains("death") || cn.Contains("die"))) deathClip = clip;
                    }
                }
            }

            Debug.Log($"[FixAllIssues] Raptor clips: Idle={idleClip?.name}, Walk={walkClip?.name}, Run={runClip?.name}, Bite={biteClip?.name}, Death={deathClip?.name}");

            // Ensure parameters
            EnsureParameter(ctrl, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(ctrl, "Forward", AnimatorControllerParameterType.Float);
            EnsureParameter(ctrl, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(ctrl, "Walk", AnimatorControllerParameterType.Bool);
            EnsureParameter(ctrl, "Run", AnimatorControllerParameterType.Bool);
            EnsureParameter(ctrl, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(ctrl, "IsAttacking", AnimatorControllerParameterType.Trigger);
            EnsureParameter(ctrl, "Bite", AnimatorControllerParameterType.Trigger);
            EnsureParameter(ctrl, "Die", AnimatorControllerParameterType.Trigger);
            EnsureParameter(ctrl, "Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            // Clear old states to avoid broken states
            foreach (var cs in sm.states)
            {
                sm.RemoveState(cs.state);
            }
            foreach (var ast in sm.anyStateTransitions)
            {
                sm.RemoveAnyStateTransition(ast);
            }

            AnimatorState stWalk = sm.AddState("Walk");
            stWalk.motion = walkClip;
            stWalk.speed = 1.15f;

            AnimatorState stIdle = sm.AddState("Idle");
            stIdle.motion = idleClip;

            AnimatorState stRun = sm.AddState("Run");
            stRun.motion = runClip != null ? runClip : walkClip;
            stRun.speed = 1.25f;

            AnimatorState stAttack = sm.AddState("Attack");
            stAttack.motion = biteClip;
            stAttack.speed = 1.2f;

            AnimatorState stDie = sm.AddState("Die");
            stDie.motion = deathClip;

            // Default state = Walk so the moment raptors spawn and move they run immediately!
            sm.defaultState = stWalk;

            // Transitions:
            // Walk -> Idle (when not moving)
            var tWalkToIdle = stWalk.AddTransition(stIdle);
            tWalkToIdle.hasExitTime = false;
            tWalkToIdle.duration = 0.15f;
            tWalkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

            // Idle -> Walk (when moving)
            var tIdleToWalk = stIdle.AddTransition(stWalk);
            tIdleToWalk.hasExitTime = false;
            tIdleToWalk.duration = 0.1f;
            tIdleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

            // Walk -> Run
            var tWalkToRun = stWalk.AddTransition(stRun);
            tWalkToRun.hasExitTime = false;
            tWalkToRun.duration = 0.2f;
            tWalkToRun.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed");

            // Run -> Walk
            var tRunToWalk = stRun.AddTransition(stWalk);
            tRunToWalk.hasExitTime = false;
            tRunToWalk.duration = 0.2f;
            tRunToWalk.AddCondition(AnimatorConditionMode.Less, 3.5f, "Speed");

            // Attack -> Walk
            var tAtkToWalk = stAttack.AddTransition(stWalk);
            tAtkToWalk.hasExitTime = true;
            tAtkToWalk.exitTime = 0.85f;
            tAtkToWalk.duration = 0.2f;

            // AnyState -> Attack
            var tAnyAttack = sm.AddAnyStateTransition(stAttack);
            tAnyAttack.hasExitTime = false;
            tAnyAttack.duration = 0.1f;
            tAnyAttack.canTransitionToSelf = false;
            tAnyAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            // AnyState -> Die
            var tAnyDie = sm.AddAnyStateTransition(stDie);
            tAnyDie.hasExitTime = false;
            tAnyDie.duration = 0.1f;
            tAnyDie.canTransitionToSelf = false;
            tAnyDie.AddCondition(AnimatorConditionMode.If, 0, "Die");

            EditorUtility.SetDirty(ctrl);

            // 2. Setup Prefab with rotation offset -90° Y
            string veloPath = $"{PREFAB_DIR}/Prefab_Velociraptor.prefab";
            if (File.Exists(veloPath))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(veloPath);
                Transform model = root.transform.Find("Model");
                if (model != null)
                {
                    // Rotate -90° around Y (270°) so raptor faces straight forward (+Z along motion)
                    model.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    Debug.Log($"[FixAllIssues] Set Velociraptor Model localRotation to Euler(0, -90, 0) -> faces forward +Z.");
                }

                Animator anim = root.GetComponentInChildren<Animator>(true);
                if (anim != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                PrefabUtility.SaveAsPrefabAsset(root, veloPath);
                PrefabUtility.UnloadPrefabContents(root);
                SyncToResources(veloPath);
            }
        }

        public static void FixTRexControllerAndDeploy()
        {
            string ctrlPath = "Assets/Resources/Animations/TRex_Controller.controller";
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) return;

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            // Find Walk, Idle, Run, Attack states
            AnimatorState stWalk = null;
            AnimatorState stIdle = null;
            AnimatorState stAttackRun = null;

            foreach (var cs in sm.states)
            {
                string sName = cs.state.name.ToLower();
                if (sName == "walk") stWalk = cs.state;
                else if (sName == "idle") stIdle = cs.state;
                else if (sName == "attackrun") stAttackRun = cs.state;
            }

            // Remove broken empty AttackRun state
            if (stAttackRun != null)
            {
                sm.RemoveState(stAttackRun);
                Debug.Log("[FixAllIssues] Removed broken dummy state 'AttackRun' from TRex_Controller.");
            }

            // Set default state to Walk or Idle
            if (stWalk != null)
            {
                // Setting default to Walk ensures that as soon as deployment happens, legs cycle immediately!
                sm.defaultState = stWalk;
                Debug.Log("[FixAllIssues] Set TRex_Controller default state to 'Walk'. Legs will walk immediately upon spawn/deploy!");
            }
            else if (stIdle != null)
            {
                sm.defaultState = stIdle;
            }

            // If Walk -> Idle or Idle -> Walk transitions need tuning:
            if (stWalk != null && stIdle != null)
            {
                // Ensure Idle -> Walk has 0 exit time and instant response
                foreach (var t in stIdle.transitions)
                {
                    if (t.destinationState == stWalk)
                    {
                        t.hasExitTime = false;
                        t.duration = 0.05f;
                    }
                }
            }

            EditorUtility.SetDirty(ctrl);
        }

        public static void FixCatapultAndTotemOrientation()
        {
            // Catapult: Make sure it's 100% horizontal flat on the ground.
            string catPath = $"{PREFAB_DIR}/Prefab_Catapult.prefab";
            if (File.Exists(catPath))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(catPath);
                Transform model = root.transform.Find("Model");
                if (model != null)
                {
                    // Check if mesh has internal tilt and level it flat
                    // In free__catapult.glb, the front of the catapult is at Z+ in local coordinates.
                    // When rotated 180° Y, it faces +Z (towards incoming dinos).
                    // We ensure X and Z rotation are strictly 0.
                    model.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    model.localPosition = new Vector3(0f, 1.4613f, 0f);
                }

                root.transform.rotation = Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(root, catPath);
                PrefabUtility.UnloadPrefabContents(root);
                SyncToResources(catPath);
                Debug.Log("<color=green>[FixAllIssues]</color> Prefab_Catapult rotation set to level (X=0, Y=180, Z=0).");
            }

            // Shaman Totem: Make sure it's 100% upright pillar (zero tilt on X/Z)
            string totemPath = $"{PREFAB_DIR}/Prefab_ShamanTotem.prefab";
            if (File.Exists(totemPath))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(totemPath);
                Transform model = root.transform.Find("Model");
                if (model != null)
                {
                    model.localRotation = Quaternion.identity;
                    model.localPosition = new Vector3(-0.0389f, 0.5159f, -0.0028f);
                }

                root.transform.rotation = Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(root, totemPath);
                PrefabUtility.UnloadPrefabContents(root);
                SyncToResources(totemPath);
                Debug.Log("<color=green>[FixAllIssues]</color> Prefab_ShamanTotem rotation set to upright (X=0, Y=0, Z=0).");
            }
        }

        private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in ctrl.parameters)
            {
                if (p.name == name) return;
            }
            ctrl.AddParameter(name, type);
        }

        private static void SyncToResources(string sourcePath)
        {
            if (!Directory.Exists(RES_PREFAB_DIR)) Directory.CreateDirectory(RES_PREFAB_DIR);
            string dest = Path.Combine(RES_PREFAB_DIR, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, dest, true);
        }
    }
}
