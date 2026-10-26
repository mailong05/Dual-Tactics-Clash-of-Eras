using System.IO;
using System.Linq;
using GLTFast;
using LlamAcademy.Dinos.Unit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LlamAcademy.Dinos.Editor
{
    [InitializeOnLoad]
    public static class ConvertVelociraptorToNative
    {
        private const string GLB_PATH = "Assets/velociraptor.glb";
        private const string PREFAB_PATH = "Assets/Prefabs/Prehistoric/Prefab_Velociraptor.prefab";
        private const string RES_PREFAB_PATH = "Assets/Resources/Prefabs/Prehistoric/Prefab_Velociraptor.prefab";
        private const string MESH_DIR = "Assets/Models/Velociraptor_Native";
        private const string CONTROLLER_PATH = "Assets/Resources/Animations/Velociraptor_Controller.controller";

        static ConvertVelociraptorToNative()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("VelociraptorNativeDone_V1", false))
                {
                    SessionState.SetBool("VelociraptorNativeDone_V1", true);
                    ConvertToNative();
                }
            };
        }

        [MenuItem("Tools/Prehistoric TD/Convert Velociraptor To Native Assets")]
        public static void ConvertToNative()
        {
            Debug.Log("<color=yellow>[NativeConverter]</color> Starting conversion of velociraptor.glb to native assets...");

            if (!Directory.Exists(MESH_DIR)) Directory.CreateDirectory(MESH_DIR);

            // 1. Load GLB Game Object
            GameObject glbGo = AssetDatabase.LoadAssetAtPath<GameObject>(GLB_PATH);
            if (glbGo == null)
            {
                Debug.LogError("[NativeConverter] Could not load Assets/velociraptor.glb as GameObject!");
                return;
            }

            // Instantiate a temporary instance in the scene to inspect and duplicate
            GameObject tempInstance = Object.Instantiate(glbGo);
            tempInstance.name = "Temp_Velociraptor_Extraction";

            try
            {
                // Find all renderers
                SkinnedMeshRenderer[] smrs = tempInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                MeshFilter[] mfs = tempInstance.GetComponentsInChildren<MeshFilter>(true);

                Debug.Log($"[NativeConverter] Found {smrs.Length} SkinnedMeshRenderers and {mfs.Length} MeshFilters in velociraptor.glb.");

                // Create native Materials
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                // Texture 0: body diffuse, Texture 2: body normal
                Texture2D tex0 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Velociraptor_Textures/tex_0.png");
                Texture2D tex1 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Velociraptor_Textures/tex_1.png");
                Texture2D tex2 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Velociraptor_Textures/tex_2.png");
                Texture2D tex3 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Velociraptor_Textures/tex_3.png");
                Texture2D tex4 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Velociraptor_Textures/tex_4.png");
                Texture2D tex5 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Velociraptor_Textures/tex_5.png");

                Material bodyMat = new Material(litShader) { name = "Velociraptor_Body_Mat" };
                if (tex0 != null) bodyMat.SetTexture("_BaseMap", tex0);
                if (tex2 != null)
                {
                    bodyMat.SetTexture("_BumpMap", tex2);
                    bodyMat.EnableKeyword("_NORMALMAP");
                }
                bodyMat.SetFloat("_Smoothness", 0.35f);
                string bodyMatPath = $"{MESH_DIR}/Velociraptor_Body_Mat.mat";
                AssetDatabase.CreateAsset(bodyMat, bodyMatPath);

                Material eyeMat = new Material(litShader) { name = "Velociraptor_Eye_Mat" };
                if (tex3 != null) eyeMat.SetTexture("_BaseMap", tex3);
                if (tex5 != null)
                {
                    eyeMat.SetTexture("_BumpMap", tex5);
                    eyeMat.EnableKeyword("_NORMALMAP");
                }
                eyeMat.SetFloat("_Smoothness", 0.6f);
                string eyeMatPath = $"{MESH_DIR}/Velociraptor_Eye_Mat.mat";
                AssetDatabase.CreateAsset(eyeMat, eyeMatPath);

                // Duplicate and save SkinnedMeshes as native .asset files
                for (int i = 0; i < smrs.Length; i++)
                {
                    var smr = smrs[i];
                    if (smr.sharedMesh != null)
                    {
                        Mesh nativeMesh = Object.Instantiate(smr.sharedMesh);
                        nativeMesh.name = $"Velociraptor_NativeMesh_{i}_{smr.gameObject.name}";
                        string meshPath = $"{MESH_DIR}/{nativeMesh.name}.asset";
                        AssetDatabase.CreateAsset(nativeMesh, meshPath);
                        smr.sharedMesh = nativeMesh;
                    }

                    // Assign materials: Object_0 is body, Object_1 is eyes/teeth or vice versa
                    Material[] newMats = new Material[smr.sharedMaterials.Length];
                    for (int m = 0; m < newMats.Length; m++)
                    {
                        Material origMat = smr.sharedMaterials[m];
                        string origName = origMat != null ? origMat.name.ToLower() : "";
                        if (origName.Contains("eye") || origName.Contains("cornea") || i == 1)
                        {
                            newMats[m] = eyeMat;
                        }
                        else
                        {
                            newMats[m] = bodyMat;
                        }
                    }
                    smr.sharedMaterials = newMats;
                }

                // Handle regular MeshFilters if any
                for (int i = 0; i < mfs.Length; i++)
                {
                    var mf = mfs[i];
                    if (mf.sharedMesh != null)
                    {
                        Mesh nativeMesh = Object.Instantiate(mf.sharedMesh);
                        nativeMesh.name = $"Velociraptor_NativeStaticMesh_{i}_{mf.gameObject.name}";
                        string meshPath = $"{MESH_DIR}/{nativeMesh.name}.asset";
                        AssetDatabase.CreateAsset(nativeMesh, meshPath);
                        mf.sharedMesh = nativeMesh;
                    }
                }

                AssetDatabase.SaveAssets();

                // Now update both prefabs
                UpdatePrefabWithNativeModel(PREFAB_PATH, tempInstance);
                if (File.Exists(RES_PREFAB_PATH))
                {
                    UpdatePrefabWithNativeModel(RES_PREFAB_PATH, tempInstance);
                }

                Debug.Log("<color=green>[NativeConverter] SUCCESS:</color> Velociraptor converted into 100% native Unity assets and unpacked in prefabs!");
            }
            finally
            {
                Object.DestroyImmediate(tempInstance);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void UpdatePrefabWithNativeModel(string prefabPath, GameObject nativeModelSource)
        {
            if (!File.Exists(prefabPath)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform oldModel = root.transform.Find("Model");
                if (oldModel != null)
                {
                    Object.DestroyImmediate(oldModel.gameObject);
                }

                // Create a clean, independent Model hierarchy from nativeModelSource
                GameObject modelClone = Object.Instantiate(nativeModelSource, root.transform);
                modelClone.name = "Model";
                modelClone.transform.localPosition = Vector3.zero;
                modelClone.transform.localRotation = Quaternion.identity;

                // Scale to natural height ~1.6m
                Renderer[] rends = modelClone.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    if (b.size.y > 0.05f)
                    {
                        float scale = 1.6f / b.size.y;
                        modelClone.transform.localScale = Vector3.one * scale;
                    }
                }

                // Animator
                Animator anim = modelClone.GetComponent<Animator>();
                if (anim == null) anim = modelClone.AddComponent<Animator>();
                RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CONTROLLER_PATH);
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.enabled = true;

                // Remove legacy animation if any
                Animation leg = modelClone.GetComponent<Animation>();
                if (leg != null) Object.DestroyImmediate(leg);

                // Wire up RunnerDino
                if (!root.TryGetComponent(out RunnerDino runner))
                {
                    runner = root.AddComponent<RunnerDino>();
                }
                var so = new SerializedObject(runner);
                so.FindProperty("ModelAnimator").objectReferenceValue = anim;
                so.FindProperty("VisualModel").objectReferenceValue = modelClone.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Ensure NavMeshAgent
                UnityEngine.AI.NavMeshAgent agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.agentTypeID = 1479372276;
                    agent.radius = 0.45f;
                    agent.height = 1.6f;
                    agent.speed = 5.5f;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[NativeConverter] Successfully saved prefab at {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
