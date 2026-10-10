#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LlamAcademy.Dinos.Editor
{
    public class PrehistoricBuildWindow : EditorWindow
    {
        private string _OutputPath = "Builds/Windows/AdaptiveTowerDefense3D.exe";
        private bool _IsDevBuild = false;
        private bool _AutoRunAfterBuild = true;
        private bool _OpenFolderAfterBuild = true;

        [MenuItem("Prehistoric TD/🚀 Xuất File Game (.exe)...", priority = 90)]
        [MenuItem("Prehistoric TD/Build Game (.exe)...", priority = 91)]
        public static void ShowWindow()
        {
            var window = GetWindow<PrehistoricBuildWindow>("Build Game (.exe)");
            window.minSize = new Vector2(480, 420);
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_OutputPath))
            {
                _OutputPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows", "AdaptiveTowerDefense3D.exe");
            }
            else if (!Path.IsPathRooted(_OutputPath))
            {
                _OutputPath = Path.GetFullPath(_OutputPath);
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("🦖 CÔNG CỤ XUẤT GAME ADAPTIVE TOWER DEFENSE 3D (.EXE)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Tool này sẽ biên dịch toàn bộ gameplay, 3D model, animation, âm thanh và giao diện thành 1 file .exe độc lập chạy siêu mượt trên Windows để bạn dễ dàng test và gửi người khác chơi thử.", MessageType.Info);
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Cấu hình Xuất File:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            _OutputPath = EditorGUILayout.TextField("Đường dẫn file .exe:", _OutputPath);
            if (GUILayout.Button("Chọn...", GUILayout.Width(70)))
            {
                string dir = Path.GetDirectoryName(_OutputPath);
                string file = Path.GetFileName(_OutputPath);
                string selected = EditorUtility.SaveFilePanel("Chọn nơi lưu file .exe", dir, string.IsNullOrEmpty(file) ? "AdaptiveTowerDefense3D.exe" : file, "exe");
                if (!string.IsNullOrEmpty(selected))
                {
                    _OutputPath = selected;
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            _IsDevBuild = EditorGUILayout.Toggle("Bản Debug (Development Build)", _IsDevBuild);
            _AutoRunAfterBuild = EditorGUILayout.Toggle("Tự động mở game sau khi build xong", _AutoRunAfterBuild);
            _OpenFolderAfterBuild = EditorGUILayout.Toggle("Mở thư mục chứa file sau khi build", _OpenFolderAfterBuild);

            GUILayout.Space(15);
            EditorGUILayout.LabelField("Thông tin màn chơi chính:", EditorStyles.boldLabel);
            string mainScenePath = PrehistoricBuildTool.GetMainScenePath();
            EditorGUILayout.LabelField("Scene khởi đầu:", mainScenePath, EditorStyles.wordWrappedLabel);

            GUILayout.Space(15);

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
            if (GUILayout.Button("🔨 TIẾN HÀNH BUILD FILE .EXE", GUILayout.Height(42)))
            {
                PrehistoricBuildTool.BuildPlayer(_OutputPath, _IsDevBuild, _AutoRunAfterBuild, _OpenFolderAfterBuild);
            }

            GUI.backgroundColor = new Color(0.25f, 0.7f, 1f);
            GUILayout.Space(6);
            if (GUILayout.Button("🚀 BUILD VÀ CHẠY GAME NGAY", GUILayout.Height(36)))
            {
                PrehistoricBuildTool.BuildPlayer(_OutputPath, _IsDevBuild, true, _OpenFolderAfterBuild);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📂 Mở Thư Mục Builds", GUILayout.Height(30)))
            {
                string dir = Path.GetDirectoryName(_OutputPath);
                if (Directory.Exists(dir))
                {
                    EditorUtility.RevealInFinder(dir);
                }
                else
                {
                    EditorUtility.DisplayDialog("Thông báo", $"Thư mục chưa tồn tại: {dir}", "Đóng");
                }
            }

            bool exeExists = File.Exists(_OutputPath);
            GUI.enabled = exeExists;
            if (GUILayout.Button("▶️ Chạy Bản .exe Đã Có", GUILayout.Height(30)))
            {
                PrehistoricBuildTool.RunExecutable(_OutputPath);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (exeExists)
            {
                FileInfo fi = new FileInfo(_OutputPath);
                GUILayout.Space(6);
                EditorGUILayout.HelpBox($"✓ Đã có bản build sẵn: {fi.Name} (Dung lượng: {fi.Length / (1024 * 1024):F1} MB, Ngày tạo: {fi.LastWriteTime})", MessageType.None);
            }
        }
    }

    public static class PrehistoricBuildTool
    {
        public const string DEFAULT_SCENE_PATH = "Assets/LlamAcademy/Dinos/Scenes/Dinos.unity";
        public const string DEFAULT_EXE_NAME = "AdaptiveTowerDefense3D.exe";

        public static string GetDefaultOutputPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows", DEFAULT_EXE_NAME);
        }

        public static string GetMainScenePath()
        {
            if (File.Exists(DEFAULT_SCENE_PATH)) return DEFAULT_SCENE_PATH;

            string[] found = Directory.GetFiles("Assets", "Dinos.unity", SearchOption.AllDirectories);
            if (found.Length > 0) return found[0].Replace("\\", "/");

            return EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : "";
        }

        [MenuItem("Prehistoric TD/⚡ Build Nhanh (.exe)", priority = 92)]
        public static void QuickBuild()
        {
            BuildPlayer(GetDefaultOutputPath(), false, false, true);
        }

        [MenuItem("Prehistoric TD/▶️ Build & Chạy Game Ngay", priority = 93)]
        public static void QuickBuildAndRun()
        {
            BuildPlayer(GetDefaultOutputPath(), false, true, false);
        }

        [MenuItem("Prehistoric TD/📂 Mở Thư Mục Chứa Game (Builds)", priority = 94)]
        public static void OpenBuildFolder()
        {
            string dir = Path.GetDirectoryName(GetDefaultOutputPath());
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        public static bool BuildPlayer(string outputPath, bool isDevBuild, bool autoRun, bool openFolder)
        {
            string scenePath = GetMainScenePath();
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog("Lỗi Build", $"Không tìm thấy scene gameplay chính tại:\n{scenePath}", "Đóng");
                return false;
            }

            // Đảm bảo cấu hình EditorBuildSettings có scene Dinos.unity ở vị trí đầu tiên
            EnsureBuildScenes(scenePath);

            // Cấu hình PlayerSettings chuẩn cho game đồ họa đẹp
            ConfigurePlayerSettings();

            string outputDir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = isDevBuild 
                    ? BuildOptions.Development | BuildOptions.AllowDebugging 
                    : BuildOptions.None
            };

            Debug.Log($"<color=cyan>[Prehistoric Build]</color> Bắt đầu xuất file game sang: <b>{outputPath}</b> ...");
            Stopwatch sw = Stopwatch.StartNew();

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            sw.Stop();

            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                long sizeMb = (long)(summary.totalSize / (1024 * 1024));
                string successMsg = $"✓ XUẤT GAME THÀNH CÔNG!\n\n" +
                                   $"• Vị trí file: {outputPath}\n" +
                                   $"• Dung lượng: ~{sizeMb} MB\n" +
                                   $"• Thời gian build: {sw.ElapsedMilliseconds / 1000f:F1} giây\n" +
                                   $"• Đã tối ưu hóa đồ họa URP và sẵn sàng chạy!";

                Debug.Log($"<color=green>[Prehistoric Build]</color> {successMsg.Replace("\n", " ")}");

                if (openFolder && !autoRun)
                {
                    EditorUtility.RevealInFinder(outputPath);
                }

                if (autoRun)
                {
                    RunExecutable(outputPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("Build Hoàn Tất!", successMsg, "Tuyệt vời");
                }
                return true;
            }
            else
            {
                string errorMsg = $"Build thất bại với kết quả: {summary.result}. Tổng số lỗi: {summary.totalErrors}.\nChi tiết kiểm tra trong cửa sổ Unity Console!";
                Debug.LogError($"<color=red>[Prehistoric Build]</color> {errorMsg}");
                EditorUtility.DisplayDialog("Build Thất Bại", errorMsg, "Đóng");
                return false;
            }
        }

        public static void RunExecutable(string exePath)
        {
            if (!File.Exists(exePath))
            {
                EditorUtility.DisplayDialog("Lỗi Chạy Game", $"Không tìm thấy file .exe tại:\n{exePath}", "Đóng");
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };
                Process.Start(psi);
                Debug.Log($"<color=green>[Prehistoric Build]</color> Đã khởi chạy game thành công: {exePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>[Prehistoric Build]</color> Không thể khởi chạy file: {ex.Message}");
            }
        }

        private static void EnsureBuildScenes(string mainScenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int existingIdx = scenes.FindIndex(s => s.path == mainScenePath);
            if (existingIdx >= 0)
            {
                scenes[existingIdx].enabled = true;
                if (existingIdx != 0)
                {
                    var scene = scenes[existingIdx];
                    scenes.RemoveAt(existingIdx);
                    scenes.Insert(0, scene);
                }
            }
            else
            {
                scenes.Insert(0, new EditorBuildSettingsScene(mainScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "3ChangLinh";
            PlayerSettings.productName = "Adaptive Tower Defense 3D";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;

            // Đảm bảo mức đồ họa xuất file là Ultra (cực đại), đồng bộ 100% với Editor
            int maxQuality = QualitySettings.names.Length - 1;
            QualitySettings.SetQualityLevel(maxQuality, true);
            Debug.Log($"<color=cyan>[Prehistoric Build]</color> Cấu hình mức đồ họa build: {QualitySettings.names[maxQuality]} (Cực đại - Đồng bộ Editor)");
        }

        /// <summary>
        /// Phương thức hỗ trợ build tự động từ dòng lệnh (Command line / PowerShell batch mode)
        /// </summary>
        public static void CommandLineBuild()
        {
            string outputPath = GetDefaultOutputPath();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-outputPath" && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                }
            }

            bool success = BuildPlayer(outputPath, false, false, false);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
#endif
