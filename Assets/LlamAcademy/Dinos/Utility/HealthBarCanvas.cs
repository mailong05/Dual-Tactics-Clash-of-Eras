using System;

using System.Collections.Generic;

using System.Linq;

using LlamAcademy.Dinos.UI;

using LlamAcademy.Dinos.Unit;

using UnityEngine;



namespace LlamAcademy.Dinos.Utility

{

    [RequireComponent(typeof(Canvas))]

    public class HealthBarCanvas : MonoBehaviour

    {

        private static HealthBarCanvas _Instance;

        private static bool _IsShuttingDown = false;



        public static bool HasInstance => _Instance != null && !_IsShuttingDown;



        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]

        private static void ResetStatics()

        {

            _Instance = null;

            _IsShuttingDown = false;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        }



        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)

        {

            _IsShuttingDown = false;

        }



        public static HealthBarCanvas Instance

        {

            get

            {

                if (_IsShuttingDown) return null;



                if (!Application.isPlaying)

                {

                    if (_Instance == null)

                    {

                        _Instance = FindFirstObjectByType<HealthBarCanvas>();

                    }

                    return _Instance;

                }



                if (_Instance == null)

                {

                    _Instance = FindFirstObjectByType<HealthBarCanvas>();

                    if (_Instance == null && !_IsShuttingDown)

                    {

                        GameObject canvasObj = new GameObject("HealthBarCanvas");

                        Canvas canvas = canvasObj.AddComponent<Canvas>();

                        canvas.renderMode = RenderMode.WorldSpace;

                        _Instance = canvasObj.AddComponent<HealthBarCanvas>();

                    }

                }

                return _Instance;

            }

            private set => _Instance = value;

        }



        private Dictionary<Unit.Unit, HealthBar> HealthBars = new();
        private readonly List<KeyValuePair<Unit.Unit, HealthBar>> _activePairsSnapshot = new(128);

        private Canvas _canvas;



        private void Awake()

        {

            _IsShuttingDown = false;



            if (_Instance != null && _Instance != this)

            {

                Debug.LogWarning($"Duplicate HealthBarCanvas component on ({name})! Removing duplicate component.");

                Destroy(this);

                return;

            }



            _Instance = this;

            _canvas = GetComponent<Canvas>();

            if (_canvas != null)

            {

                _canvas.renderMode = RenderMode.WorldSpace;

                _canvas.worldCamera = null; // Trong URP, KHÔNG gán worldCamera để tránh Canvas bounding box bị frustum culling

                _canvas.overrideSorting = true;

                _canvas.sortingOrder = 500; // Đảm bảo luôn vẽ nổi trên địa hình và khủng long

            }



            // Vô hiệu hóa GraphicRaycaster để thanh máu hiển thị không bao giờ chặn click/chạm đặt tháp và thả khủng long

            if (TryGetComponent(out UnityEngine.UI.GraphicRaycaster raycaster))

            {

                Destroy(raycaster);

            }



            // Dọn dẹp triệt để mọi thanh máu mồ côi (orphaned) bị lưu tĩnh trong file Scene
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && (child.name.StartsWith("HealthBar_") || child.name.Contains("Flank") || child.name.Contains("Blocker")))
                {
                    Destroy(child.gameObject);
                }
            }

            var allStrayBars = FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var hb in allStrayBars)
            {
                if (hb != null && (hb.name.StartsWith("HealthBar_") || hb.name.Contains("Flank") || hb.name.Contains("Blocker")))
                {
                    Destroy(hb.gameObject);
                }
            }



            if (HealthBarPrefab == null)

            {

                GameObject prefabObj = Resources.Load<GameObject>("Health Bar");

                if (prefabObj != null)

                {

                    HealthBarPrefab = prefabObj.GetComponent<HealthBar>();

                }

#if UNITY_EDITOR

                if (HealthBarPrefab == null)

                {

                    HealthBarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<HealthBar>("Assets/Resources/Health Bar.prefab");

                    if (HealthBarPrefab == null)

                    {

                        HealthBarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<HealthBar>("Assets/LlamAcademy/Dinos/Prefabs/Health Bar.prefab");

                    }

                }

#endif

            }

        }



        private void Start()

        {

            // Đảm bảo tất cả các tường rào chắn (kể cả Plank_Defenses sườn phải) đều được gắn và đăng ký thanh máu

            var allWalls = FindObjectsByType<Unit.Wall>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var wall in allWalls)

            {

                if (wall != null && wall.gameObject.activeInHierarchy)

                {

                    wall.EnsureHealthBarAttached();

                }

            }

        }



        private void LateUpdate()
        {
            Camera cam = Camera.main;
            Quaternion camRot = cam != null ? cam.transform.rotation : Quaternion.identity;

            bool missedCleaningAUnit = false;

            // Chụp snapshot an toàn danh sách các cặp (Unit, HealthBar) vào bộ đệm tái sử dụng
            // Triệt tiêu 100% ngoại lệ "InvalidOperationException: Collection was modified; enumeration operation may not execute"
            // khi có các sự kiện bật/tắt AR, quái chết (HandleUnitDeath), hoặc gỡ bỏ rào cọc giữa chừng frame.
            _activePairsSnapshot.Clear();
            try
            {
                foreach (var kvp in HealthBars)
                {
                    _activePairsSnapshot.Add(kvp);
                }
            }
            catch (System.InvalidOperationException)
            {
                // Bộ sưu tập bị sửa đổi trong lúc chụp snapshot, bỏ qua an toàn frame này
                return;
            }

            for (int i = 0; i < _activePairsSnapshot.Count; i++)
            {
                var keyValuePair = _activePairsSnapshot[i];

                if (keyValuePair.Key == null || keyValuePair.Value == null)
                {
                    missedCleaningAUnit = true;
                    continue;
                }

                // Nếu Unit bị ẩn (inactive) hoặc đã chết: Ẩn thanh máu đi, không để bay lơ lửng trên màn hình
                if (!keyValuePair.Key.gameObject.activeInHierarchy || keyValuePair.Key.Health <= 0)
                {
                    if (keyValuePair.Value.gameObject.activeSelf)
                    {
                        keyValuePair.Value.gameObject.SetActive(false);
                    }
                    continue;
                }

                // Trong Chế độ Công Thành (Dino Assault) ở Pha Chuẩn Bị (Setup Phase):
                // Ẩn thanh máu của các khủng long đang thả trong rừng để không gây rối mắt khi dàn quân.
                // Khi bấm [XUẤT QUÂN] chuyển sang pha chiến đấu thì thanh máu khủng long sẽ tự động bật.
                bool isAssaultSetup = RoundManagement.PrehistoricGameModeManager.Instance != null &&
                                     RoundManagement.PrehistoricGameModeManager.Instance.CurrentMode == RoundManagement.PrehistoricGameMode.DinoAssault &&
                                     RoundManagement.PrehistoricGameModeManager.Instance.AssaultPhase == RoundManagement.PrehistoricAssaultPhase.Setup;
                bool isDinoUnit = keyValuePair.Key is Unit.Dino || keyValuePair.Key is Unit.PrehistoricDinoBase;
                if (isAssaultSetup && isDinoUnit)
                {
                    if (keyValuePair.Value.gameObject.activeSelf)
                    {
                        keyValuePair.Value.gameObject.SetActive(false);
                    }
                    continue;
                }

                // Đảm bảo thanh máu của mọi công trình, chòi canh, cầu gỗ, rào cọc luôn hiển thị rõ ràng:
                if (!keyValuePair.Value.gameObject.activeSelf)
                {
                    keyValuePair.Value.gameObject.SetActive(true);
                }

                Collider col = keyValuePair.Key.GetComponentInChildren<Collider>();
                Renderer rend = keyValuePair.Key.GetComponentInChildren<Renderer>();
                string lowerName = keyValuePair.Key.gameObject.name.ToLower();
                bool isBridge = lowerName.Contains("bridge") || lowerName.Contains("ramp");
                bool isFence = !isBridge && (lowerName.Contains("segment") ||
                               lowerName.Contains("barricade") ||
                               lowerName.Contains("palisade") ||
                               lowerName.Contains("plank_defenses") ||
                               (keyValuePair.Key is Unit.Wall && !lowerName.Contains("tower") && !lowerName.Contains("arch") && !lowerName.Contains("hut")));

                Vector3 targetPos;
                if (isBridge)
                {
                    float topY = rend != null ? rend.bounds.max.y : (col != null ? col.bounds.max.y : (keyValuePair.Key.Transform.position.y + 1.5f));
                    float centerX = rend != null ? rend.bounds.center.x : (col != null ? col.bounds.center.x : keyValuePair.Key.Transform.position.x);
                    float centerZ = rend != null ? rend.bounds.center.z : (col != null ? col.bounds.center.z : keyValuePair.Key.Transform.position.z);
                    targetPos = new Vector3(centerX, topY + 0.8f, centerZ);
                }
                else if (isFence)
                {
                    // Đặt thanh máu nổi rõ ràng trên đỉnh của hàng rào cọc gỗ (+0.65m trên đỉnh cọc) để không bị che khuất
                    float topY = rend != null ? rend.bounds.max.y : (col != null ? col.bounds.max.y : (keyValuePair.Key.Transform.position.y + 3.0f));
                    float centerX = rend != null ? rend.bounds.center.x : (col != null ? col.bounds.center.x : keyValuePair.Key.Transform.position.x);
                    float centerZ = rend != null ? rend.bounds.center.z : (col != null ? col.bounds.center.z : keyValuePair.Key.Transform.position.z);
                    targetPos = new Vector3(centerX, topY + 0.65f, centerZ);
                }
                else if (rend != null || col != null)
                {
                    float topY = rend != null ? rend.bounds.max.y : col.bounds.max.y;
                    float centerX = rend != null ? rend.bounds.center.x : col.bounds.center.x;
                    float centerZ = rend != null ? rend.bounds.center.z : col.bounds.center.z;
                    targetPos = new Vector3(centerX, topY + 0.6f, centerZ);
                }
                else
                {
                    targetPos = keyValuePair.Key.Transform.position + keyValuePair.Value.FollowOffset;
                }
                keyValuePair.Value.transform.position = targetPos;

                if (cam != null)
                {
                    keyValuePair.Value.transform.rotation = camRot;
                }

                if (keyValuePair.Key.MaxHealth > 0)
                {
                    keyValuePair.Value.SetProgress((float)keyValuePair.Key.Health / keyValuePair.Key.MaxHealth);
                }
            }

            if (missedCleaningAUnit)
            {
                ClearOrphaned();
            }
        }

        public void ClearAll()
        {
            _activePairsSnapshot.Clear();
            foreach (var kvp in HealthBars)
            {
                _activePairsSnapshot.Add(kvp);
            }
            HealthBars.Clear();

            for (int i = 0; i < _activePairsSnapshot.Count; i++)
            {
                var val = _activePairsSnapshot[i].Value;
                if (val != null && val.gameObject != null)
                {
                    Destroy(val.gameObject);
                }
            }
        }

        public void ClearOrphaned()
        {
            _activePairsSnapshot.Clear();
            foreach (var kvp in HealthBars)
            {
                _activePairsSnapshot.Add(kvp);
            }

            var validPairs = new Dictionary<Unit.Unit, HealthBar>();
            for (int i = 0; i < _activePairsSnapshot.Count; i++)
            {
                var kvp = _activePairsSnapshot[i];
                if (kvp.Key == null || kvp.Value == null || !kvp.Key.gameObject.activeInHierarchy || kvp.Key.Health <= 0)
                {
                    if (kvp.Value != null && kvp.Value.gameObject != null)
                    {
                        kvp.Value.gameObject.SetActive(false);
                        Destroy(kvp.Value.gameObject);
                    }
                }
                else
                {
                    validPairs[kvp.Key] = kvp.Value;
                }
            }

            HealthBars = validPairs;

            // Quét sạch triệt để TOÀN BỘ các thanh máu con nằm dưới HealthBarCanvas mà không thuộc validPairs
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                HealthBar hb = child.GetComponent<HealthBar>();
                if (hb == null || !validPairs.ContainsValue(hb))
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            // Đồng thời quét sạch mọi thanh máu mồ côi tĩnh lưu trong Scene trên toàn map
            var allSceneBars = FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var hb in allSceneBars)
            {
                if (hb != null && !validPairs.ContainsValue(hb))
                {
                    hb.gameObject.SetActive(false);
                    Destroy(hb.gameObject);
                }
            }
        }

        public void ClearAllHealthBars()
        {
            foreach (var kvp in HealthBars)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    kvp.Value.gameObject.SetActive(false);
                    Destroy(kvp.Value.gameObject);
                }
            }
            HealthBars.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }



        [SerializeField] private HealthBar HealthBarPrefab;



        public HealthBar CreateHealthBarForUnit(Unit.Unit unit)
        {
            if (unit == null) return null;

            // Tuyệt đối không tạo thanh máu cho các rào cản tàng hình mép bản đồ (Flank Blocker / Flank Barricade)
            string uNameLower = unit.gameObject.name.ToLower();
            if (uNameLower.Contains("flank") || uNameLower.Contains("blocker"))
            {
                return null;
            }



            if (HealthBars.TryGetValue(unit, out HealthBar existingHb) && existingHb != null)

            {

                existingHb.gameObject.SetActive(true);

                return existingHb;

            }



            if (HealthBarPrefab == null)

            {

                GameObject prefabObj = Resources.Load<GameObject>("Health Bar");

                if (prefabObj != null)

                {

                    HealthBarPrefab = prefabObj.GetComponent<HealthBar>();

                }

#if UNITY_EDITOR

                if (HealthBarPrefab == null)

                {

                    HealthBarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<HealthBar>("Assets/Resources/Health Bar.prefab");

                    if (HealthBarPrefab == null)

                    {

                        HealthBarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<HealthBar>("Assets/LlamAcademy/Dinos/Prefabs/Health Bar.prefab");

                    }

                }

#endif

            }



            if (HealthBarPrefab == null) return null;



            HealthBar hb = Instantiate(HealthBarPrefab, transform);

            hb.name = $"HealthBar_{unit.gameObject.name}";

            hb.gameObject.SetActive(true);

            hb.transform.localScale = Vector3.one;



            // Tự động căn độ cao hiển thị thanh máu chuẩn xác theo kích thước từng loài khủng long

            float height = 2.2f;

            string unitNameLower = unit.gameObject.name.ToLower();

            if (unitNameLower.Contains("trex") || unitNameLower.Contains("t-rex"))

            {

                height = 5.8f;

            }

            else if (unitNameLower.Contains("big tower") || unitNameLower.Contains("watchtower"))
            {
                height = 9.2f;
            }
            else if (unitNameLower.Contains("arch entry") || unitNameLower.Contains("overhead_arch") || unitNameLower.Contains("arch"))
            {
                height = 6.2f;
            }
            else if (unitNameLower.Contains("hut") || unitNameLower.Contains("tent"))
            {
                height = 4.2f;
            }
            else if (unitNameLower.Contains("segment") || unitNameLower.Contains("barricade") || unitNameLower.Contains("palisade") || unitNameLower.Contains("plank_defenses"))
            {
                height = 1.95f;
            }
            else if (unitNameLower.Contains("ankyl"))

            {

                height = 2.8f;

            }

            else if (unitNameLower.Contains("ptero"))

            {

                height = 3.2f;

            }

            else if (unitNameLower.Contains("raptor"))

            {

                height = 2.0f;

            }

            else if (unit is PrehistoricVillageBase || unitNameLower.Contains("base") || unitNameLower.Contains("egg"))

            {

                height = 3.8f;

            }

            else if (unit.TryGetComponent(out Collider col))

            {

                height = col.bounds.size.y + 0.8f;

            }



            hb.FollowOffset = new Vector3(0f, height, 0f);

            Register(hb, unit);

            hb.SetProgress(1.0f);

            return hb;

        }



        public void Register(HealthBar healthBar, Unit.Unit unit)

        {

            if (unit == null || healthBar == null) return;



            if (HealthBars.ContainsKey(unit))

            {

                if (HealthBars[unit] != healthBar && HealthBars[unit] != null)

                {

                    Destroy(HealthBars[unit].gameObject);

                }

                HealthBars[unit] = healthBar;

            }

            else

            {

                HealthBars.Add(unit, healthBar);

            }



            healthBar.gameObject.SetActive(true);

            healthBar.transform.SetParent(transform);

            healthBar.transform.localScale = Vector3.one;

            healthBar.transform.localRotation = Quaternion.identity;



            // Thiết lập tên hiển thị của đối tượng trên thanh máu

            string displayName = GetUnitDisplayName(unit);

            Collider unitCol = unit.GetComponentInChildren<Collider>();

            bool isHostile = unitCol != null && Unit.Unit.IsHostileMonster(unitCol);

            Color nameColor = isHostile ? new Color(1f, 0.45f, 0.35f) : new Color(0.45f, 1f, 0.55f);

            if (displayName.Contains("T-Rex"))

            {

                nameColor = new Color(1f, 0.85f, 0.2f);

            }

            else if (displayName.Contains("Căn Cứ") || displayName.Contains("Làng"))

            {

                nameColor = new Color(1f, 0.85f, 0.2f);

            }

            healthBar.SetUnitName(displayName, nameColor);



            float initialProgress = unit.MaxHealth > 0 ? (float)unit.Health / unit.MaxHealth : 1.0f;

            healthBar.SetProgress(initialProgress);



            unit.OnDeath -= HandleUnitDeath;

            unit.OnDeath += HandleUnitDeath;

        }



        public static string GetUnitDisplayName(Unit.Unit unit)

        {

            if (unit == null) return "";



            if (unit is PrehistoricVillageBase) return "🏛️ Căn Cứ Làng Bộ Lạc";



            if (unit.UnitType is Config.TowerSO tower && !string.IsNullOrEmpty(tower.DisplayName))

            {

                return tower.DisplayName;

            }



            if (unit is Enemy.Defender def)

            {

                return def.Role == Enemy.DefenderRole.Archer ? "🏹 Cung Thủ Làng" : "🗡️ Chiến Binh Phóng Giáo";

            }



            if (unit is ArcherTower) return "🏹 Chòi Cung Thủ Gỗ";

            if (unit is BallistaTower) return "🏹 Tháp Nỏ Khổng Lồ";

            if (unit is CatapultTower) return "☄️ Máy Bắn Đá";

            if (unit is TeslaTower) return "⚡ Trụ Sấm Sét";

            if (unit is FrostTower) return "❄️ Tháp Băng Tiền Sử";

            string lower = unit.gameObject.name.ToLower();
            if (lower.Contains("big tower") || lower.Contains("watchtower")) return "🛖 Chòi Canh Bộ Lạc";
            if (lower.Contains("arch entry") || lower.Contains("overhead_arch") || lower.Contains("arch")) return "🪵 Cầu Vòm Gỗ Làng";
            if (lower.Contains("hut") || lower.Contains("tent")) return "⛺ Lều Dân Làng";
            if (lower.Contains("perimeter_barricade") || (unit.transform.parent != null && unit.transform.parent.name == "Village_Custom_Perimeter_Wall")) return "🪵 Hàng Rào Gỗ";
            if (lower.Contains("plank_defenses") || lower.Contains("barricade") || lower.Contains("palisade") || lower.Contains("segment") || lower.Contains("wall") || unit is Wall) return "";

            if (lower.Contains("trex") || lower.Contains("t-rex")) return "👑 T-Rex Bạo Chúa";

            // Khủng long bầy đàn thông thường và cạm bẫy: Không gắn nhãn tên văn bản để tránh rối màn hình khi số lượng đông
            if (lower.Contains("ptero") || lower.Contains("pterodactyl")) return "";
            if (lower.Contains("ankyl") || lower.Contains("ankylosaurus")) return "";
            if (lower.Contains("raptor") || lower.Contains("velociraptor")) return "";
            if (lower.Contains("mud") || lower.Contains("tar")) return "";
            if (lower.Contains("spike")) return "";

            if (lower.Contains("egg") || lower.Contains("base") || lower.Contains("village")) return "🏛️ Căn Cứ Làng Bộ Lạc";

            if (unit.UnitType != null && !string.IsNullOrEmpty(unit.UnitType.name))
            {
                string uName = unit.UnitType.name.ToLower();
                if (uName.Contains("raptor") || uName.Contains("ankyl") || uName.Contains("ptero")) return "";
                return unit.UnitType.name.Replace("Unit_", "").Replace("Dino_", "").Replace("Tower_", "");
            }

            if (lower.Contains("dino") || lower.Contains("monster")) return "";



            return unit.gameObject.name.Replace("(Clone)", "").Trim();

        }



        public void Unregister(Unit.Unit unit)

        {

            if (unit == null) return;



            unit.OnDeath -= HandleUnitDeath;

            if (HealthBars.Remove(unit, out HealthBar healthBar))

            {

                if (healthBar != null && healthBar.gameObject != null)

                {

                    healthBar.gameObject.SetActive(false);

                    Destroy(healthBar.gameObject);

                }

            }

        }



        private void HandleUnitDeath(IDamageable diedobject)

        {

            if (diedobject != null && diedobject.Transform != null)

            {

                Unit.Unit unit = diedobject.Transform.GetComponent<Unit.Unit>();

                Unregister(unit);

            }

        }



        private void OnApplicationQuit()

        {

            _IsShuttingDown = true;

        }



        private void OnDestroy()

        {

            if (_Instance == this)

            {

                _Instance = null;

                _IsShuttingDown = true;

            }

        }

    }

}

