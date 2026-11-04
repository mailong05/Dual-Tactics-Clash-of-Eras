using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LlamAcademy.Dinos.Editor
{
    /// <summary>
    /// Công cụ tự động đóng gói toàn bộ gói Package đồ án cho sinh viên nộp bài:
    /// 1. Xuất file Unity Package (.unitypackage) chứa toàn bộ mã nguồn gameplay, AI, AR, UI, Shaders.
    /// 2. Hỗ trợ chuẩn bị thư mục nộp bài gồm Build, Backend, Tài liệu theo yêu cầu Giảng viên.
    /// </summary>
    public static class PrehistoricPackageExporter
    {
        private const string PackageName = "AdaptiveTowerDefense3D_CompletePackage.unitypackage";

        [MenuItem("Prehistoric TD/📦 Xuất Unity Package Đồ Án (.unitypackage)...", priority = 98)]
        public static void ExportSubmissionPackage()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string exportDir = Path.Combine(projectRoot, "Packages_Submission");

            if (!Directory.Exists(exportDir))
            {
                Directory.CreateDirectory(exportDir);
            }

            string targetPath = Path.Combine(exportDir, PackageName);

            // Các thư mục cốt lõi cần đóng gói
            string[] assetPaths = new string[]
            {
                "Assets/LlamAcademy",
                "Assets/Models",
                "Assets/Prefabs",
                "Assets/Resources",
                "Assets/Stylized Lava Texture"
            };

            Debug.Log($"<color=cyan>[Package Exporter]</color> Đang đóng gói assets sang: <b>{targetPath}</b> ...");

            EditorUtility.DisplayProgressBar("Đóng Gói Đồ Án", "Đang nén các asset vào file .unitypackage...", 0.4f);

            try
            {
                AssetDatabase.ExportPackage(
                    assetPaths,
                    targetPath,
                    ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies
                );

                EditorUtility.ClearProgressBar();

                if (File.Exists(targetPath))
                {
                    long sizeBytes = new FileInfo(targetPath).Length;
                    double sizeMb = Math.Round(sizeBytes / (1024.0 * 1024.0), 2);

                    Debug.Log($"<color=green>[Package Exporter]</color> Xuất Package thành công! Dung lượng: {sizeMb} MB.");

                    bool open = EditorUtility.DisplayDialog(
                        "Đóng Gói Thành Công!",
                        $"Đã tạo thành công Unity Package nộp bài tại:\n{targetPath}\n(Dung lượng: {sizeMb} MB)\n\nBạn có muốn mở thư mục chứa file ngay không?",
                        "Mở Thư Mục",
                        "Đóng"
                    );

                    if (open)
                    {
                        EditorUtility.RevealInFinder(targetPath);
                    }
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Package Exporter] Lỗi khi xuất package: {ex.Message}");
                EditorUtility.DisplayDialog("Lỗi Đóng Gói", $"Đã xảy ra lỗi: {ex.Message}", "Đóng");
            }
        }
    }
}
