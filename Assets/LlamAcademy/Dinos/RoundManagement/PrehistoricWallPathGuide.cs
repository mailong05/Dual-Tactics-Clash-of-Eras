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

        public const string CustomWallContainerName = "Village_Custom_Perimeter_Wall";

        /// <summary>
        /// 5 tọa độ mốc chính thức do người chơi xác định trên Scene View
        /// </summary>
        public static readonly Vector3[] OfficialWaypoints = new Vector3[]
        {
            new Vector3(-36.00f, 1.00f, -13.00f),
            new Vector3(-20.00f, 0.00f, -3.50f),
            new Vector3(-1.00f, 0.00f, -4.00f),
            new Vector3(0.00f, 0.00f, -4.00f),
            new Vector3(40.00f, 0.00f, -7.00f)
        };

        [Header("Danh Sách Điểm Mốc Tọa Độ (Kéo thả từ Hierarchy)")]
        [Tooltip("Thứ tự: Điểm bắt đầu -> Các điểm giữa uốn cong -> Điểm kết thúc")]
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
            EnsurePrefabLoaded();

            if (TargetParent == null)
            {
                GameObject wallRoot = PrehistoricGameplayManager.FindCustomPerimeterWall();
                if (wallRoot == null) wallRoot = new GameObject(CustomWallContainerName);
                TargetParent = wallRoot.transform;
            }

            // Ở chế độ Thủ Thành (Tower Defense) khi Play: TUYỆT ĐỐI ẩn hàng rào này, người chơi tự đặt rào
            if (Application.isPlaying)
            {
                bool isAssault = PrehistoricGameplayManager.IsAssaultMode;
                if (!isAssault && TargetParent != null)
                {
                    TargetParent.gameObject.SetActive(false);
                }
            }
        }

        private void EnsurePrefabLoaded()
        {
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
        }

        /// <summary>
        /// Tự động khởi tạo 5 điểm mốc mặc định theo đúng tọa độ người chơi cung cấp
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

            string[] markerNames = new string[]
            {
                "Moc_1_Bat_Dau",
                "Moc_2_Uon_Cong",
                "Moc_3_Uon_Cong",
                "Moc_4_Uon_Cong",
                "Moc_5_Ket_Thuc"
            };

            for (int i = 0; i < OfficialWaypoints.Length; i++)
            {
                GameObject mObj = new GameObject(markerNames[i]);
                mObj.transform.SetParent(transform, false);
                mObj.transform.position = OfficialWaypoints[i];
                Markers.Add(mObj.transform);
            }

            Debug.Log("<color=green>[Wall Guide]</color> Đã tạo 5 mốc chuẩn theo thiết kế của bạn. Bạn có thể kéo thả di chuyển các mốc bằng phím W.");
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

            List<Vector3> pts = new List<Vector3>();
            for (int i = 0; i < Markers.Count; i++)
            {
                if (Markers[i] != null) pts.Add(Markers[i].position);
            }

            if (pts.Count < 2) return;

            if (TargetParent == null)
            {
                GameObject wallRoot = PrehistoricGameplayManager.FindCustomPerimeterWall();
                if (wallRoot == null) wallRoot = new GameObject(CustomWallContainerName);
                TargetParent = wallRoot.transform;
            }

            // Vô hiệu hóa triệt để các nhóm tường cũ trong Scene để không gây xung đột hoặc đè lên
            string[] obsoleteGroups = new string[] { "Left_Wing_Wall", "Right_Wing_Wall", "Right_Flank_Wall", "Gate_Portcullis_Barricade" };
            foreach (var gName in obsoleteGroups)
            {
                GameObject g = GameObject.Find(gName);
                if (g != null) g.SetActive(false);
            }

            EnsurePrefabLoaded();
            ClearBuiltWall();

            BuildWallInternal(pts.ToArray(), TargetParent, BarricadePrefab, Spacing, BarricadeHealth);
        }

        /// <summary>
        /// Phương thức tĩnh để PrehistoricGameplayManager có thể tự động xây hàng rào chuẩn khi game chạy
        /// nếu chưa được người chơi dựng trước trong Scene View
        /// </summary>
        public static GameObject BuildOfficialPerimeterWall(Transform targetParent = null)
        {
            GameObject wallRoot;
            if (targetParent != null)
            {
                wallRoot = targetParent.gameObject;
            }
            else
            {
                wallRoot = PrehistoricGameplayManager.FindCustomPerimeterWall();
                if (wallRoot == null) wallRoot = new GameObject(CustomWallContainerName);
            }
            wallRoot.SetActive(true);

            // Vô hiệu hóa các tường cũ
            string[] obsoleteGroups = new string[] { "Left_Wing_Wall", "Right_Wing_Wall", "Right_Flank_Wall", "Gate_Portcullis_Barricade" };
            foreach (var gName in obsoleteGroups)
            {
                GameObject g = GameObject.Find(gName);
                if (g != null) g.SetActive(false);
            }

            GameObject prefab = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Barricade");
#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Prehistoric/Prefab_Barricade.prefab");
            }
#endif

            // Dọn sạch con cũ
            for (int i = wallRoot.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(wallRoot.transform.GetChild(i).gameObject);
            }

            BuildWallInternal(OfficialWaypoints, wallRoot.transform, prefab, 2.6f, 1200);
            return wallRoot;
        }

        private static void BuildWallInternal(Vector3[] waypoints, Transform parent, GameObject prefab, float spacing, int health)
        {
            if (waypoints == null || waypoints.Length < 2) return;

            List<Vector3> placedPositions = new List<Vector3>();
            List<Quaternion> placedRotations = new List<Quaternion>();

            for (int m = 0; m < waypoints.Length - 1; m++)
            {
                Vector3 startPos = waypoints[m];
                Vector3 endPos = waypoints[m + 1];
                Vector3 segVec = endPos - startPos;
                segVec.y = 0;
                float segLen = segVec.magnitude;
                if (segLen < 0.1f) continue;

                Vector3 segDir = segVec.normalized;
                int count = Mathf.Max(1, Mathf.RoundToInt(segLen / Mathf.Max(0.5f, spacing)));
                float actualStep = segLen / count;

                // Góc xoay hướng ra phía trước (về hướng rừng rậm, đón đầu khủng long)
                Vector3 normal = Vector3.Cross(Vector3.up, segDir);
                if (Vector3.Dot(normal, Vector3.forward) < 0) normal = -normal;
                Quaternion targetRot = Quaternion.LookRotation(normal, Vector3.up);

                int endIndex = (m == waypoints.Length - 2) ? count : count - 1;
                for (int i = 0; i <= endIndex; i++)
                {
                    Vector3 samplePos = startPos + segDir * (i * actualStep);
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

            for (int i = 0; i < placedPositions.Count; i++)
            {
                GameObject seg;
#if UNITY_EDITOR
                if (prefab != null)
                {
                    seg = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                }
                else
                {
                    seg = new GameObject($"Perimeter_Barricade_{i}");
                    seg.transform.SetParent(parent);
                }
                Undo.RegisterCreatedObjectUndo(seg, "Build Barricade");
#else
                if (prefab != null)
                {
                    seg = Instantiate(prefab, parent);
                }
                else
                {
                    seg = new GameObject($"Perimeter_Barricade_{i}");
                    seg.transform.SetParent(parent);
                }
#endif
                seg.name = $"Perimeter_Barricade_{i}";
                seg.transform.position = placedPositions[i];
                seg.transform.rotation = placedRotations[i];
                seg.transform.localScale = new Vector3(1.25f, 1.25f, 1.2f);
                seg.SetActive(true);

                foreach (var r in seg.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null) r.enabled = true;
                }

                var col = seg.GetComponent<BoxCollider>();
                if (col == null) col = seg.AddComponent<BoxCollider>();
                col.size = new Vector3(3.2f, 2.5f, 1.5f);
                col.center = new Vector3(0f, 1.25f, 0f);
                col.enabled = true;

                var obs = seg.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                if (obs == null) obs = seg.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obs.size = new Vector3(3.2f, 2.5f, 1.5f);
                obs.center = new Vector3(0f, 1.25f, 0f);
                obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obs.carving = true;
                obs.carveOnlyStationary = false;
                obs.enabled = true;

                var wall = seg.GetComponent<Unit.Wall>();
                if (wall == null) wall = seg.AddComponent<Unit.Wall>();
                wall.MaxHealth = health;
                wall.Health = health;
                wall.Root = seg;
                wall.EnsureHealthBarAttached();
                seg.tag = "Wall";

                if (Utility.HealthBarCanvas.Instance != null)
                {
                    var hb = Utility.HealthBarCanvas.Instance.CreateHealthBarForUnit(wall);
                    if (hb != null)
                    {
                        hb.gameObject.SetActive(true);
                        hb.SetProgress(1.0f);
                        hb.SetUnitName("🪵 Hàng Rào Gỗ", new Color(0.9f, 0.75f, 0.4f));
                    }
                }
            }

            Debug.Log($"<color=green><b>[Wall Guide] THÀNH CÔNG!</b></color> Đã dựng {placedPositions.Count} đoạn hàng rào kiên cố theo đúng 5 mốc chính thức!");
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

            Vector3 pA = Markers[Markers.Count - 2].position;
            Vector3 pB = Markers[Markers.Count - 1].position;
            Vector3 newPos = (pA + pB) * 0.5f;

            int newIndex = Markers.Count;
            GameObject pNew = new GameObject($"Moc_{newIndex}_Uon_Cong");
            pNew.transform.SetParent(transform, false);
            pNew.transform.position = newPos;

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

            Gizmos.color = LineColor;
            for (int i = 0; i < Markers.Count - 1; i++)
            {
                if (Markers[i] == null || Markers[i + 1] == null) continue;
                Gizmos.DrawLine(Markers[i].position, Markers[i + 1].position);

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
