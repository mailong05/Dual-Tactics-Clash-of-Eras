using UnityEditor;
using UnityEngine;
using LlamAcademy.Dinos.RoundManagement;

namespace LlamAcademy.Dinos.Editor
{
    [CustomEditor(typeof(PrehistoricWallPathGuide))]
    public class PrehistoricWallPathGuideEditor : UnityEditor.Editor
    {
        [MenuItem("Prehistoric TD/Công Cụ Hàng Rào/Tạo Bộ Căn Mốc Hàng Rào (Wall Guide)", false, 50)]
        [MenuItem("GameObject/3D Object/Prehistoric/Tạo Bộ Căn Mốc Hàng Rào", false, 10)]
        public static void CreateOrSelectWallGuide()
        {
            PrehistoricWallPathGuide guide = Object.FindFirstObjectByType<PrehistoricWallPathGuide>();
            if (guide == null)
            {
                GameObject root = new GameObject("[Bo_Can_Moc_Hang_Rao]");
                root.transform.position = Vector3.zero;
                guide = root.AddComponent<PrehistoricWallPathGuide>();
                guide.CreateDefaultMarkers();
                Undo.RegisterCreatedObjectUndo(root, "Create Wall Guide");
            }

            Selection.activeGameObject = guide.gameObject;
            SceneView.FrameLastActiveSceneView();
            Debug.Log("<color=green><b>[Prehistoric TD]</b></color> Đã mở 'Bộ Căn Mốc Hàng Rào'. Các điểm mốc hiển thị hình cầu màu Xanh Lá (Bắt đầu), Đỏ (Kết thúc) và Xanh Dương (Uốn cong) trong Scene View!");
        }

        public override void OnInspectorGUI()
        {
            PrehistoricWallPathGuide guide = (PrehistoricWallPathGuide)target;

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.2f, 0.8f, 1f) }
            };

            GUIStyle helpBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                richText = true,
                padding = new RectOffset(10, 10, 10, 10)
            };

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🛠 CÔNG CỤ CĂN TỌA ĐỘ VÀ XÂY HÀNG RÀO THEO MỐC", titleStyle);
            EditorGUILayout.Space(4);

            string guideHelp = "<b>HƯỚNG DẪN SỬ DỤNG NHANH:</b>\n" +
                               "1. Bấm nút <b>'Tạo / Reset Mốc Mẫu'</b> bên dưới.\n" +
                               "2. Trong <b>Scene View</b>, chọn từng mốc (Moc_1_Bat_Dau, Moc_3_Ket_Thuc...):\n" +
                               "   - Dùng phím <b>W</b> để kéo di chuyển quả cầu đến đúng vị trí bạn muốn rào.\n" +
                               "   - Mốc Xanh Lá = Bắt đầu, Mốc Đỏ = Kết thúc, Vàng/Cam = Preview các đoạn rào.\n" +
                               "3. Bấm <b>'➕ Thêm Mốc Ở Giữa'</b> nếu muốn đường rào cong uốn lượn.\n" +
                               "4. Bấm <b>'🔨 XÂY DỰNG HÀNG RÀO THEO MỐC'</b>: Toàn bộ rào gỗ chuẩn có va chạm và máu sẽ tự động mọc lên khép kín từng centimet!\n" +
                               "5. Bấm <b>'📋 In Tọa Độ Ra Console'</b> để lấy tọa độ gửi lại cho AI.";

            EditorGUILayout.HelpBox(guideHelp, MessageType.Info);
            EditorGUILayout.Space(8);

            // Nút Tạo / Khôi phục mốc
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button("📍 Tạo / Khôi Phục 3 Mốc Mẫu (Start & End)", GUILayout.Height(32)))
            {
                guide.CreateDefaultMarkers();
                SceneView.RepaintAll();
            }

            // Nút Thêm mốc ở giữa
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("➕ Thêm Điểm Mốc Ở Giữa (Thêm Điểm Uốn Cong)", GUILayout.Height(28)))
            {
                guide.AddIntermediateMarker();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);

            // Nút In tọa độ ra Console
            GUI.backgroundColor = new Color(1f, 0.9f, 0.4f);
            if (GUILayout.Button("📋 In Tọa Độ Các Mốc Ra Console (Gửi cho AI)", GUILayout.Height(28)))
            {
                guide.LogCurrentMarkerPositions();
            }

            EditorGUILayout.Space(8);

            // Nút Xây dựng hàng rào thật
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.3f);
            if (GUILayout.Button("🔨 XÂY DỰNG HÀNG RÀO THEO MỐC (BUILD WALL)", GUILayout.Height(40)))
            {
                guide.BuildWallAlongMarkers();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);

            // Nút Xóa hàng rào
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("❌ Xóa Hàng Rào Đã Dựng (Clear Wall)", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận xóa hàng rào", "Bạn có chắc chắn muốn xóa hàng rào đã dựng dưới Right_Flank_Wall không?", "Xóa", "Hủy"))
                {
                    guide.ClearBuiltWall();
                    SceneView.RepaintAll();
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(12);

            EditorGUILayout.LabelField("Cấu hình nâng cao:", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }
    }
}
