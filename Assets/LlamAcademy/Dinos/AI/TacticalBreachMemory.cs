using System;
using System.Collections.Generic;
using UnityEngine;

namespace LlamAcademy.Dinos.AI
{
    [System.Serializable]
    public class BreachPoint
    {
        public Vector3 Position;
        public Vector3 ApproachPoint;
        public Vector3 IngressPoint;
        public Vector3 Direction;
        public Vector3 Lateral;
        public float Width;
        public float Timestamp;
        public int UnitsTraversed;

        // Giới hạn số lượng khủng long cùng lúc đi qua khe hở này (theo yêu cầu: 4 con)
        public const int MAX_CONCURRENT_UNITS = 4;
        private readonly List<Unit.Unit> _assignedUnits = new();

        public int CurrentAssignedCount
        {
            get
            {
                CleanDeadUnits();
                return _assignedUnits.Count;
            }
        }

        public bool HasSlotAvailable
        {
            get
            {
                CleanDeadUnits();
                return _assignedUnits.Count < MAX_CONCURRENT_UNITS;
            }
        }

        public bool IsUnitAssigned(Unit.Unit unit)
        {
            return unit != null && _assignedUnits.Contains(unit);
        }

        public int GetUnitSlotIndex(Unit.Unit unit)
        {
            if (unit == null) return 0;
            CleanDeadUnits();
            int idx = _assignedUnits.IndexOf(unit);
            return idx >= 0 ? idx : 0;
        }

        /// <summary>
        /// Phân chia 4 làn đường thâm nhập riêng biệt qua cửa đột phá rộng 5m
        /// để 4 con khủng long không dẫm đạp hoặc chèn ép nhau.
        /// </summary>
        public Vector3 GetIngressPointForUnit(Unit.Unit unit)
        {
            int index = GetUnitSlotIndex(unit);
            float lateralOffset = 0f;
            switch (index)
            {
                case 0: lateralOffset = -0.9f; break;
                case 1: lateralOffset = 0.9f; break;
                case 2: lateralOffset = -1.8f; break;
                case 3: lateralOffset = 1.8f; break;
            }
            float depthOffset = (index >= 2) ? 1.0f : 0f;
            return IngressPoint + Lateral * lateralOffset + Direction * depthOffset;
        }

        public Vector3 GetApproachPointForUnit(Unit.Unit unit)
        {
            int index = GetUnitSlotIndex(unit);
            float lateralOffset = 0f;
            switch (index)
            {
                case 0: lateralOffset = -0.9f; break;
                case 1: lateralOffset = 0.9f; break;
                case 2: lateralOffset = -1.8f; break;
                case 3: lateralOffset = 1.8f; break;
            }
            return ApproachPoint + Lateral * lateralOffset;
        }

        public bool TryClaimSlot(Unit.Unit unit)
        {
            if (unit == null || unit.Health <= 0) return false;
            CleanDeadUnits();
            if (_assignedUnits.Contains(unit)) return true;
            if (_assignedUnits.Count < MAX_CONCURRENT_UNITS)
            {
                _assignedUnits.Add(unit);
                return true;
            }
            return false;
        }

        public void ReleaseSlot(Unit.Unit unit)
        {
            if (unit != null)
            {
                _assignedUnits.Remove(unit);
            }
            CleanDeadUnits();
        }

        public void ClearAssigned()
        {
            _assignedUnits.Clear();
        }

        private void CleanDeadUnits()
        {
            _assignedUnits.RemoveAll(u => u == null || !u.enabled || u.Health <= 0);
        }

        public BreachPoint(Vector3 position, Vector3 direction, float width = 5.0f)
        {
            Position = position;
            Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            Direction.y = 0;
            if (Direction == Vector3.zero) Direction = Vector3.forward;

            Lateral = Vector3.Cross(Vector3.up, Direction).normalized;
            if (Lateral == Vector3.zero) Lateral = Vector3.right;

            // Điểm tiếp cận phía ngoài hàng rào (bên ngoài làng)
            ApproachPoint = position - Direction * 3.0f;
            ApproachPoint.y = position.y;

            // Điểm thâm nhập sâu vào bên trong làng (vượt qua hàng rào 4.5m)
            IngressPoint = position + Direction * 4.5f;
            IngressPoint.y = position.y;

            Width = width;
            Timestamp = Time.time;
            UnitsTraversed = 0;
        }

        public bool HasPassedPlane(Vector3 unitPos)
        {
            Vector3 toUnit = unitPos - Position;
            toUnit.y = 0;
            // Khủng long đã vượt qua mặt phẳng hàng rào vào sâu bên trong làng ít nhất 0.8m
            return Vector3.Dot(toUnit, Direction) > 0.8f;
        }
    }

    /// <summary>
    /// Trung tâm Điều phối Tác chiến Khe hở (Tactical Breach Memory).
    /// Ghi nhận tức thời mọi vị trí rào chắn bị phá hủy, điều phối tối đa 4 con khủng long gần nhất
    /// đi qua mỗi khe hở, các con khác tỏa ra phá hủy tường chỗ khác.
    /// Sau khi các con trước qua thành công, tiếp tục cho phép 4 con gần đó đi qua.
    /// </summary>
    public class TacticalBreachMemory : MonoBehaviour
    {
        private static TacticalBreachMemory _instance;
        private static bool _isQuitting = false;

        public static bool HasInstance => _instance != null && !_isQuitting;

        public static TacticalBreachMemory Instance
        {
            get
            {
                if (_isQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<TacticalBreachMemory>();
                    if (_instance == null && Application.isPlaying && !_isQuitting)
                    {
                        GameObject go = new GameObject("[AI_TacticalBreachMemory]");
                        _instance = go.AddComponent<TacticalBreachMemory>();
                    }
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _isQuitting = false;
        }

        [SerializeField] private List<BreachPoint> _ActiveBreaches = new();
        public IReadOnlyList<BreachPoint> ActiveBreaches => _ActiveBreaches;

        public event Action<BreachPoint> OnBreachOpened;
        public event Action OnBreachesCleared;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                _isQuitting = false;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                _isQuitting = true;
            }
        }

        /// <summary>
        /// Ghi nhận bức tường vừa bị phá hủy làm điểm đột phá tác chiến.
        /// </summary>
        public BreachPoint RegisterBreach(Vector3 wallPosition, Vector3 directionTowardsBase, float width = 3.2f)
        {
            for (int i = 0; i < _ActiveBreaches.Count; i++)
            {
                if (Vector3.Distance(_ActiveBreaches[i].Position, wallPosition) < 2.0f)
                {
                    _ActiveBreaches[i].Timestamp = Time.time;
                    _ActiveBreaches[i].Width += width * 0.5f;
                    return _ActiveBreaches[i];
                }
            }

            BreachPoint newBreach = new BreachPoint(wallPosition, directionTowardsBase, width);
            _ActiveBreaches.Add(newBreach);

            Debug.Log($"<color=#FF5533><b>[Tactical AI Breach]</b></color> Đã phá thủng hàng rào tại ({wallPosition.x:F1}, {wallPosition.z:F1})! Cấp phép 4 con gần nhất đi qua lối này, các con khác tỏa ra phá tường chỗ khác!");

            OnBreachOpened?.Invoke(newBreach);
            return newBreach;
        }

        /// <summary>
        /// Tìm khe hở tốt nhất hướng về căn cứ.
        /// TỰ ĐỘNG BỎ QUA nếu unit đã ở bên trong làng (đã vượt qua khe hở này) hoặc khe hở không giúp tiến gần căn cứ.
        /// </summary>
        public BreachPoint GetBestBreachTowards(Vector3 unitPos, Vector3 targetBasePos, Unit.Unit unit = null, float maxSearchDistance = 25.0f)
        {
            if (_ActiveBreaches.Count == 0) return null;

            BreachPoint best = null;
            float bestScore = float.MinValue;

            Vector3 toBase = (targetBasePos - unitPos);
            toBase.y = 0;
            float currentDistToBase = toBase.magnitude;

            for (int i = _ActiveBreaches.Count - 1; i >= 0; i--)
            {
                BreachPoint bp = _ActiveBreaches[i];
                if (bp == null) continue;

                // 1. NẾU UNIT ĐÃ VƯỢT QUA MẶT PHẲNG KHE HỞ NÀY (ĐÃ Ở TRONG LÀNG), BỎ QUA NGAY!
                // Tuyệt đối không để khủng long quay đầu lại khe hở mà nó vừa mới đi qua!
                if (bp.HasPassedPlane(unitPos))
                {
                    continue;
                }

                // 2. Điểm thâm nhập của khe hở phải thực sự giúp rút ngắn khoảng cách tới căn cứ
                float ingressDistToBase = Vector3.Distance(bp.IngressPoint, targetBasePos);
                if (ingressDistToBase >= currentDistToBase - 0.5f)
                {
                    continue; // Khe hở này nằm phía sau hoặc không giúp tiến gần căn cứ
                }

                // 3. Nếu unit được cung cấp: kiểm tra xem khe hở này có còn slot không (< 4 con)
                if (unit != null && !bp.IsUnitAssigned(unit) && !bp.HasSlotAvailable)
                {
                    continue; // Khe hở này đã đủ 4 con! Bỏ qua để unit đi phá tường chỗ khác
                }

                float distToBreach = Vector3.Distance(unitPos, bp.Position);
                if (distToBreach > maxSearchDistance) continue;

                float progressBonus = currentDistToBase - ingressDistToBase;
                float score = (progressBonus * 2.0f) - distToBreach;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = bp;
                }
            }

            return best;
        }

        public BreachPoint GetBestBreachTowards(Vector3 unitPos, Vector3 targetBasePos, float maxSearchDistance)
        {
            return GetBestBreachTowards(unitPos, targetBasePos, null, maxSearchDistance);
        }

        public void NotifyUnitTraversed(BreachPoint breach)
        {
            OnUnitTraversedBreach(breach, null);
        }

        /// <summary>
        /// Kiểm tra xem một vị trí tường có nằm sát mép khe hở cùng hàng rào hay không.
        /// Chỉ bỏ qua các đoạn tường cùng hàng (độ sâu < 2.0m, khoảng cách ngang <= 4.5m).
        /// TUYỆT ĐỐI KHÔNG bỏ qua các hàng rào tầng trong (secondary defenses) nằm sâu hơn trong làng!
        /// </summary>
        public bool IsWallNearActiveBreach(Vector3 wallPos, float threshold = 4.5f)
        {
            if (_ActiveBreaches.Count == 0) return false;

            for (int i = 0; i < _ActiveBreaches.Count; i++)
            {
                BreachPoint bp = _ActiveBreaches[i];
                if (bp == null) continue;

                Vector3 diff = wallPos - bp.Position;
                diff.y = 0;

                // Tính khoảng cách dọc theo hướng thâm nhập vào làng (Direction)
                float depthAlongDirection = Vector3.Dot(diff, bp.Direction);

                // Nếu bức tường này nằm sâu hơn về phía làng (độ sâu > 2.0m),
                // đây là hàng rào tầng trong! Khủng long BẮT BUỘC phải tấn công nó để tiến vào trung tâm!
                if (depthAlongDirection > 2.0f)
                {
                    continue;
                }

                // Tính khoảng cách ngang theo tuyến tường
                float lateralDist = Vector3.Dot(diff, bp.Lateral);
                if (Mathf.Abs(depthAlongDirection) < 2.0f && Mathf.Abs(lateralDist) <= threshold)
                {
                    return true; // Đúng là đoạn tường mép cạnh lỗ thủng cùng hàng rào
                }
            }
            return false;
        }

        /// <summary>
        /// Khi 1 con khủng long đã vượt qua khe hở thành công vào trong làng, giải phóng slot và tự động điều phối
        /// con khủng long tiếp theo gần đó lấp vào slot vừa trống để đi qua!
        /// </summary>
        public void OnUnitTraversedBreach(BreachPoint breach, Unit.Unit unit)
        {
            if (breach == null) return;
            breach.ReleaseSlot(unit);
            breach.UnitsTraversed++;

            // Điều phối con tiếp theo gần đó lấp vào slot trống
            DispatchNextDinosToBreach(breach);
        }

        /// <summary>
        /// Tìm các con khủng long gần nhất chưa qua rào để cấp phép đi qua các slot còn trống (tối đa 4 con cùng lúc).
        /// </summary>
        public void DispatchNextDinosToBreach(BreachPoint breach)
        {
            if (breach == null || !breach.HasSlotAvailable) return;

            // Tìm các khủng long còn sống ở gần khe hở nhưng chưa vượt qua hàng rào
            List<Unit.PrehistoricDinoBase> candidates = new();
            foreach (var dino in Unit.PrehistoricDinoBase.ActiveDinos)
            {
                if (dino == null || !dino.enabled || dino.Health <= 0) continue;
                if (dino.IsNavigatingBreach) continue; // Đang đi qua khe hở rồi
                if (breach.HasPassedPlane(dino.transform.position)) continue; // Đã ở trong làng

                float d = Vector3.Distance(dino.transform.position, breach.Position);
                if (d <= 28f)
                {
                    candidates.Add(dino);
                }
            }

            candidates.Sort((a, b) => 
                Vector3.Distance(a.transform.position, breach.Position).CompareTo(Vector3.Distance(b.transform.position, breach.Position)));

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!breach.HasSlotAvailable) break;

                var dino = candidates[i];
                if (breach.TryClaimSlot(dino))
                {
                    dino.AssignToBreach(breach);
                }
            }
        }

        public void ClearAll()
        {
            foreach (var b in _ActiveBreaches)
            {
                b?.ClearAssigned();
            }
            _ActiveBreaches.Clear();
            OnBreachesCleared?.Invoke();
        }
    }
}
