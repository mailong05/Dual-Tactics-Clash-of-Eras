#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if AR_FOUNDATION_ENABLED
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
#endif

namespace LlamAcademy.Dinos.Editor
{
    /// <summary>
    /// Automatically detects Unity AR Foundation 6.6.2 + ARCore packages, defines AR_FOUNDATION_ENABLED,
    /// injects ARBackgroundRendererFeature into URP Renderers (required for AR camera feed in URP),
    /// and configures ARCoreLoader in XR Plug-in Management for Android builds.
    /// </summary>
    [InitializeOnLoad]
    public static class PrehistoricARAutoSetup
    {
        private const string DefineSymbol = "AR_FOUNDATION_ENABLED";

        static PrehistoricARAutoSetup()
        {
            EditorApplication.delayCall += EnsureARPackagesAndConfiguration;
        }

        [MenuItem("Prehistoric TD/📱 Cấu Hình AR Foundation & ARCore URP...", priority = 80)]
        public static void ConfigureFromMenu()
        {
            EnsureARPackagesAndConfiguration(verbose: true);
        }

        public static void EnsureARPackagesAndConfiguration()
        {
            EnsureARPackagesAndConfiguration(verbose: false);
        }

        public static void EnsureARPackagesAndConfiguration(bool verbose)
        {
            bool hasARFoundation = HasLoadedAssembly("Unity.XR.ARFoundation");
            bool hasXRCoreUtils = HasLoadedAssembly("Unity.XR.CoreUtils");
            bool hasXRManagement = HasLoadedAssembly("Unity.XR.Management");

            if (!hasARFoundation || !hasXRCoreUtils || !hasXRManagement)
            {
                if (verbose)
                {
                    EditorUtility.DisplayDialog(
                        "Đang Tải Package AR Foundation",
                        "Unity đang tải các gói AR Foundation / ARCore / XR Management từ Packages/manifest.json.\n" +
                        "Vui lòng đợi Unity tải xong (thanh tiến trình góc dưới phải), hệ thống sẽ tự kích hoạt ARCore ngay lập tức!",
                        "Đã hiểu"
                    );
                }
                return;
            }

            bool definesChanged = EnsureDefineOnTarget(NamedBuildTarget.Android, DefineSymbol);
            definesChanged |= EnsureDefineOnTarget(NamedBuildTarget.Standalone, DefineSymbol);

            if (definesChanged)
            {
                Debug.Log("<color=cyan>[AR AutoSetup]</color> Đã bật Scripting Define Symbol: <b>AR_FOUNDATION_ENABLED</b> (Android & Standalone). Đang đợi Unity biên dịch lại...");
                return;
            }

#if AR_FOUNDATION_ENABLED
            int rendererCount = EnsureARBackgroundRendererFeatureOnAllURPRenderers();
            bool arCoreOk = EnsureARCoreLoaderConfigured();

            if (verbose)
            {
                EditorUtility.DisplayDialog(
                    "AR Foundation & ARCore Đã Sẵn Sàng!",
                    $"• Đã tích hợp ARBackgroundRendererFeature vào {rendererCount} URP Renderer Assets.\n" +
                    $"• Trạng thái Google ARCore Loader (Android): {(arCoreOk ? "ĐÃ BẬT ✓" : "Kiểm tra lại XR Plug-in Management")}\n\n" +
                    "Giờ bạn có thể xuất file Android (.apk) để chạy ARCore quét mặt bàn thật 6-DOF!",
                    "Tuyệt vời!"
                );
            }
#endif
        }

        private static bool HasLoadedAssembly(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));
        }

        private static bool EnsureDefineOnTarget(NamedBuildTarget target, string symbol)
        {
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            var list = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (list.Contains(symbol)) return false;

            list.Add(symbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            return true;
        }

#if AR_FOUNDATION_ENABLED
        private static int EnsureARBackgroundRendererFeatureOnAllURPRenderers()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            int updatedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;

                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rendererData == null) continue;

                bool alreadyHas = rendererData.rendererFeatures.Any(f => f is ARBackgroundRendererFeature);
                if (!alreadyHas)
                {
                    var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                    feature.name = "ARBackgroundRendererFeature";
                    AssetDatabase.AddObjectToAsset(feature, rendererData);
                    rendererData.rendererFeatures.Add(feature);

                    // Force internal renderer feature map update cleanly
                    MethodInfo validateMethod = typeof(ScriptableRendererData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    validateMethod?.Invoke(rendererData, null);

                    EditorUtility.SetDirty(feature);
                    EditorUtility.SetDirty(rendererData);
                    updatedCount++;
                    Debug.Log($"<color=green>[AR URP Setup]</color> Added ARBackgroundRendererFeature to {path}");
                }
            }

            if (updatedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }
            return guids.Length;
        }

        private static bool EnsureARCoreLoaderConfigured()
        {
            const string loaderTypeName = "UnityEngine.XR.ARCore.ARCoreLoader";
            BuildTargetGroup group = BuildTargetGroup.Android;

            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget buildTargetSettings);
            if (buildTargetSettings == null)
            {
                string settingsDir = "Assets/XR";
                if (!Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);
                string assetPath = Path.Combine(settingsDir, "XRGeneralSettingsPerBuildTarget.asset");

                buildTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(assetPath);
                if (buildTargetSettings == null)
                {
                    buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(buildTargetSettings, assetPath);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
            }

            XRGeneralSettings settings = buildTargetSettings.SettingsForBuildTarget(group);
            if (settings == null)
            {
                string settingsDir = "Assets/XR";
                if (!Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);

                settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                settings.name = "Android_XRGeneralSettings";
                AssetDatabase.AddObjectToAsset(settings, buildTargetSettings);
                buildTargetSettings.SetSettingsForBuildTarget(group, settings);
            }

            if (settings.Manager == null)
            {
                XRManagerSettings manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "Android_XRManagerSettings";
                AssetDatabase.AddObjectToAsset(manager, buildTargetSettings);
                settings.Manager = manager;
            }

            // Do not auto-start ARCore on launch; ARTabletopController starts/stops AR subsystems on demand!
            if (settings.InitManagerOnStart)
            {
                settings.InitManagerOnStart = false;
                EditorUtility.SetDirty(settings);
            }

            bool assigned = settings.Manager.activeLoaders.Any(l => l != null && l.GetType().FullName == loaderTypeName);
            if (!assigned)
            {
                assigned = XRPackageMetadataStore.AssignLoader(settings.Manager, loaderTypeName, group);
                if (assigned)
                {
                    EditorUtility.SetDirty(settings.Manager);
                    EditorUtility.SetDirty(settings);
                    EditorUtility.SetDirty(buildTargetSettings);
                    AssetDatabase.SaveAssets();
                    Debug.Log("<color=green>[ARCore Setup]</color> Enabled ARCoreLoader in Android XR Plug-in Management!");
                }
            }

            return assigned;
        }
#endif
    }
}
#endif
