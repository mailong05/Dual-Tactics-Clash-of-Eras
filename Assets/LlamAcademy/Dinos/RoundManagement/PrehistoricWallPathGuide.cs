using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LlamAcademy.Dinos.RoundManagement
{
    /// <summary>
    /// Công cụ hỗ trợ người dùng đặt các điểm mốc (Marker) trong Unity Scene
    /// để xác định chính xác vị trí bắt đầu, các điểm uốn cong và điểm kết thúc của hàng rào.
    /// Tự động rải các đoạn rào gỗ Prefab_Barricade khớp từng centimet dọc theo đường nối các mốc.
    /// </summary>
    [ExecuteInEditMode]
    public class PrehistoricWallPathGuide : MonoBehaviour
    {
        public static PrehistoricWallPathGuide Instance { get; private set; }

        [Header("Danh Sách Điểm Mốc Tọa Độ (Kéo thả từ Hierarchy)")]
        [Tooltip("Thứ tự: Điểm bắt đầu (nối từ hàng rào cũ) -> Các điểm giữa -> Điểm kết thúc (cắm vào vách núi)")]
        public List<Transform> Markers = new List<Transform>();

        [Header("Cấu Hình Hàng Rào")]
        [Tooltip("Khoảng cách giữa các đoạn rào (mặc định 2.6m để các cọc gỗ gối chồng khép kín)")]
        public float Spacing = 2.6f;

        [Tooltip("Prefab hàng rào gỗ chính thức")]
        public GameObject BarricadePrefab;

        [Tooltip("Nhóm cha lưu trữ hàng rào")]
        public Transform TargetParent;

        [Tooltip("Máu của mỗi đoạn rào khi sinh ra")]
        public int BarricadeHealth = 1200;

        [Header("Hiển Thị Trực Quan (Gizmo)")]
        public Color LineColor = Color.yellow;
        public Color MarkerColor = Color.cyan;
        public float MarkerGizmoRadius = 0.5f;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
            if (BarricadePrefab == null)
            {
                BarricadePrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Barricade");
#if UNITY_EDITOR
                if (BarricadePrefab == null)
                {
                    BarricadePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_Barricade.prefab");
                }
#endif
            }

            if (TargetParent == null)
            {
                GameObject flank = GameObject.Find("Right_Flank_Wall");
                if (flank != null) TargetParent = flank.transform;
            }
        }

        /// <summary>
        /// Tự động khởi tạo 2 điểm mốc mặc định (Điểm bắt đầu và Điểm kết thúc) nếu danh sách rỗng
        /// </summary>
        [ContextMenu("Tạo Mốc Mặc Định (Start & End)")]
        public void CreateDefaultMarkers()
        {
            if (Markers == null) Markers = new List<Transform>();

            // Xóa các mốc cũ
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Moc_"))
                {
#if UNITY_EDITOR
                    Undo.DestroyObjectImmediate(child.gameObject);
#else
                    DestroyImmediate(child.gameObject);
#endif
                }
            }
            Markers.Clear();

            // Điểm 1: Nối tiếp từ mép hàng rào mặt tiền (Segment 7 ở X = 16.2f, Z = -2.5f)
            GameObject pStart = new GameObject("Moc_1_Bat_Dau");
            pStart.transform.SetParent(transform, false);
            pStart.transform.position = new Vector3(16.5f, 0f, -2.5f);
            Markers.Add(pStart.transform);

            // Điểm 2: Điểm giữa bo quanh gờ đá
            GameObject pMid = new GameObject("Moc_2_Uon_Cong");
            pMid.transform.SetParent(transform, false);
            pMid.transform.position = new Vector3(20.0f, 0f, -3.5f);
            Markers.Add(pMid.transform);

            // Điểm 3: Điểm kết thúc cắm sâu vào vách núi đá bên phải
            GameObject pEnd = new GameObject("Moc_3_Ket_Thuc");
            pEnd.transform.SetParent(transform, false);
            pEnd.transform.position = new Vector3(24.0f, 0f, -2.5f);
            Markers.Add(pEnd.transform);

            Debug.Log("<color=green>[Wall Guide]</color> Đã tạo 3 mốc mẫu. Bạn có thể di chuyển chúng trong Scene View bằng phím W.");
        }

        /// <summary>
        /// Xây dựng toàn bộ hàng rào gỗ thật trải dài theo các điểm mốc
        /// </summary>
        [ContextMenu("🔨 Xây Dựng Hàng Rào Theo Mốc (Build Wall)")]
        public void BuildWallAlongMarkers()
        {
            if (Markers == null || Markers.Count < 2)
            {
                Debug.LogWarning("<color=yellow>[Wall Guide]</color> Cần ít nhất 2 điểm mốc để xây hàng rào! Hãy bấm 'Tạo Mốc Mặc Định' trước.");
                return;
            }

            if (TargetParent == null)
            {
                GameObject flank = GameObject.Find("Right_Flank_Wall");
                if (flank == null) flank = new GameObject("Right_Flank_Wall");
                TargetParent = flank.transform;
            }

            if (BarricadePrefab == null)
            {
                BarricadePrefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Barricade");
#if UNITY_EDITOR
                if (BarricadePrefab == null)
                {
                    BarricadePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_Barricade.prefab");
                }
#endif
            }

            // Dọn sạch các đoạn rào cũ dưới TargetParent
            ClearBuiltWall();

            // Tính toán tổng các điểm đặt rào dọc theo các đoạn mốc
            List<Vector3> placedPositions = new List<Vector3>();
            List<Quaternion> placedRotations = new List<Quaternion>();

            for (int m = 0; m < Markers.Count - 1; m++)
            {
                if (Markers[m] == null || Markers[m + 1] == null) continue;

                Vector3 startPos = Markers[m].position;
                Vector3 endPos = Markers[m + 1].position;
                Vector3 segVec = endPos - startPos;
                segVec.y = 0; // Giữ trên mặt phẳng nằm ngang
                float segLen = segVec.magnitude;
                if (segLen < 0.1f) continue;

                Vector3 segDir = segVec.normalized;
                int count = Mathf.Max(1, Mathf.RoundToInt(segLen / Mathf.Max(0.5f, Spacing)));
                float actualStep = segLen / count;

                // Tính góc xoay để mặt khiên/gai hướng ra phía ngoài rừng (phía trước)
                Vector3 normal = Vector3.Cross(Vector3.up, segDir);
                if (Vector3.Dot(normal, Vector3.forward) < 0) normal = -normal;
                Quaternion targetRot = Quaternion.LookRotation(normal, Vector3.up);

                int endIndex = (m == Markers.Count - 2) ? count : count - 1;
                for (int i = 0; i <= endIndex; i++)
                {
                    Vector3 samplePos = startPos + segDir * (i * actualStep);
                    // Dò tìm độ cao mặt đất chính xác
                    if (UnityEngine.AI.NavMesh.SamplePosition(samplePos, out UnityEngine.AI.NavMeshHit navHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        samplePos.y = navHit.position.y;
                    }
                    else if (Physics.Raycast(new Vector3(samplePos.x, 10f, samplePos.z), Vector3.down, out RaycastHit groundHit, 20f))
                    {
                        samplePos.y = groundHit.point.y;
                    }

                    placedPositions.Add(samplePos);
                    placedRotations.Add(targetRot);
                }
            }

            // Tiến hành khởi tạo các GameObject rào chắn thật
            for (int i = 0; i < placedPositions.Count; i++)
            {
                GameObject seg;
#if UNITY_EDITOR
                if (BarricadePrefab != null)
                {
                    seg = (GameObject)PrefabUtility.InstantiatePrefab(BarricadePrefab, TargetParent);
                }
                else
                {
                    seg = new GameObject($"Flank_Barricade_{i}");
                    seg.transform.SetParent(TargetParent);
                }
                Undo.RegisterCreatedObjectUndo(seg, "Build Barricade");
#else
                if (BarricadePrefab != null)
                {
                    seg = Instantiate(BarricadePrefab, TargetParent);
                }
                else
                {
                    seg = new GameObject($"Flank_Barricade_{i}");
                    seg.transform.SetParent(TargetParent);
                }
#endif
                seg.name = $"Flank_Barricade_{i}";
                seg.transform.position = placedPositions[i];
                seg.transform.rotation = placedRotations[i];
                seg.transform.localScale = new Vector3(1.25f, 1.25f, 1.2f);
                seg.SetActive(true);

                // Bật MeshRenderer
                foreach (var r in seg.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null) r.enabled = true;
                }

                // Cấu hình BoxCollider
                var col = seg.GetComponent<BoxCollider>();
                if (col == null) col = seg.AddComponent<BoxCollider>();
                col.size = new Vector3(3.2f, 2.5f, 1.5f);
                col.center = new Vector3(0f, 1.25f, 0f);
                col.enabled = true;

                // Cấu hình NavMeshObstacle có Carving
                var obs = seg.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                if (obs == null) obs = seg.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obs.size = new Vector3(3.2f, 2.5f, 1.5f);
                obs.center = new Vector3(0f, 1.25f, 0f);
                obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obs.carving = true;
                obs.carveOnlyStationary = false;
                obs.enabled = true;

                // Cấu hình Wall
                var wall = seg.GetComponent<Unit.Wall>();
                if (wall == null) wall = seg.AddComponent<Unit.Wall>();
                wall.MaxHealth = BarricadeHealth;
                wall.Health = BarricadeHealth;
                wall.EnsureHealthBarAttached();
                seg.tag = "Wall";

                if (Utility.HealthBarCanvas.Instance != null)
                {
                    var hb = Utility.HealthBarCanvas.Instance.CreateHealthBarForUnit(wall);
                    if (hb != null)
                    {
                        hb.gameObject.SetActive(true);
                        hb.SetProgress(1.0f);
                    }
                }
            }

            Debug.Log($"<color=green><b>[Wall Guide] THÀNH CÔNG!</b></color> Đã dựng {placedPositions.Count} đoạn hàng rào kiên cố theo các điểm mốc!");

            // In danh sách tọa độ ra Console để người dùng có thể gửi lại tọa độ nếu cần
            string coordsLog = "Danh sách tọa độ các đoạn rào đã tạo:\n";
            for (int i = 0; i < placedPositions.Count; i++)
            {
                var p = placedPositions[i];
                coordsLog += $"new Vector3({p.x:F1}f, {p.y:F1}f, {p.z:F1}f),\n";
            }
            Debug.Log(coordsLog);
        }

        /// <summary>
        /// Thêm một điểm mốc uốn cong mới ở giữa điểm áp chót và điểm cuối
        /// </summary>
        [ContextMenu("➕ Thêm Điểm Mốc Ở Giữa")]
        public void AddIntermediateMarker()
        {
            if (Markers == null) Markers = new List<Transform>();
            if (Markers.Count < 2)
            {
                CreateDefaultMarkers();
                return;
            }

            // Tính vị trí trung bình giữa mốc áp chót và mốc cuối
            Vector3 pA = Markers[Markers.Count - 2].position;
            Vector3 pB = Markers[Markers.Count - 1].position;
            Vector3 newPos = (pA + pB) * 0.5f;

            int newIndex = Markers.Count;
            GameObject pNew = new GameObject($"Moc_{newIndex}_Uon_Cong");
            pNew.transform.SetParent(transform, false);
            pNew.transform.position = newPos;

            // Chèn vào ngay trước điểm kết thúc
            Markers.Insert(Markers.Count - 1, pNew.transform);
            Debug.Log($"<color=green>[Wall Guide]</color> Đã thêm mốc mới '{pNew.name}' tại tọa độ {newPos}. Bạn có thể kéo thả nó để uốn cong hàng rào!");
        }

        /// <summary>
        /// In nhanh tọa độ hiện tại của các mốc ra Console để người chơi có thể copy gửi cho AI
        /// </summary>
        [ContextMenu("📋 In Tọa Độ Các Mốc Ra Console")]
        public void LogCurrentMarkerPositions()
        {
            if (Markers == null || Markers.Count == 0)
            {
                Debug.LogWarning("[Wall Guide] Chưa có điểm mốc nào!");
                return;
            }

            string info = "<b>=== TỌA ĐỘ CÁC ĐIỂM MỐC HÀNG RÀO HIỆN TẠI ===</b>\n";
            for (int i = 0; i < Markers.Count; i++)
            {
                if (Markers[i] == null) continue;
                Vector3 p = Markers[i].position;
                string role = (i == 0) ? "[BẮT ĐẦU]" : (i == Markers.Count - 1 ? "[KẾT THÚC]" : $"[ĐIỂM GIỮA {i}]");
                info += $"{role} {Markers[i].name}: X={p.x:F2}, Y={p.y:F2}, Z={p.z:F2}  =>  new Vector3({p.x:F2}f, {p.y:F2}f, {p.z:F2}f)\n";
            }
            info += "================================================";
            Debug.Log(info);
        }

        /// <summary>
        /// Xóa sạch hàng rào đã dựng để đặt lại mốc nếu muốn
        /// </summary>
        [ContextMenu("❌ Xóa Hàng Rào Đã Dựng (Clear Wall)")]
        public void ClearBuiltWall()
        {
            if (TargetParent == null) return;
            for (int i = TargetParent.childCount - 1; i >= 0; i--)
            {
                Transform child = TargetParent.GetChild(i);
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(child.gameObject);
#else
                DestroyImmediate(child.gameObject);
#endif
            }
            Debug.Log("<color=orange>[Wall Guide]</color> Đã dọn dẹp các đoạn rào cũ.");
        }

        private void OnDrawGizmos()
        {
            if (Markers == null || Markers.Count == 0) return;

            // 1. Vẽ các điểm mốc Marker
            for (int i = 0; i < Markers.Count; i++)
            {
                if (Markers[i] == null) continue;
                Gizmos.color = (i == 0) ? Color.green : (i == Markers.Count - 1 ? Color.red : MarkerColor);
                Gizmos.DrawSphere(Markers[i].position, MarkerGizmoRadius);
                Gizmos.DrawWireSphere(Markers[i].position, MarkerGizmoRadius * 1.3f);

#if UNITY_EDITOR
                string label = (i == 0) ? " [ĐIỂM BẮT ĐẦU]" : (i == Markers.Count - 1 ? " [ĐIỂM KẾT THÚC]" : $" [MỐC {i}]");
                Handles.Label(Markers[i].position + Vector3.up * 1.2f, Markers[i].name + label);
#endif
            }

            // 2. Vẽ đường nối phát sáng giữa các mốc
            Gizmos.color = LineColor;
            for (int i = 0; i < Markers.Count - 1; i++)
            {
                if (Markers[i] == null || Markers[i + 1] == null) continue;
                Gizmos.DrawLine(Markers[i].position, Markers[i + 1].position);

                // Vẽ các ô vuông preview vị trí từng đoạn rào sẽ mọc lên
                Vector3 diff = Markers[i + 1].position - Markers[i].position;
                diff.y = 0;
                float dist = diff.magnitude;
                if (dist > 0.1f)
                {
                    int steps = Mathf.Max(1, Mathf.RoundToInt(dist / Spacing));
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector3 pt = Markers[i].position + diff.normalized * (s * (dist / steps));
                        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
                        Gizmos.DrawWireCube(pt + Vector3.up * 1.0f, new Vector3(3.2f, 2.0f, 1.4f));
                    }
                }
            }
        }
    }
}
