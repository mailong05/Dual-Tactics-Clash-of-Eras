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
        public float Width;
        public float Timestamp;
        public int UnitsTraversed;

        public BreachPoint(Vector3 position, Vector3 direction, float width = 3.2f)
        {
            Position = position;
            Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            Direction.y = 0;
            if (Direction == Vector3.zero) Direction = Vector3.forward;

            // Điểm tiếp cận phía ngoài hàng rào (bên ngoài làng)
            ApproachPoint = position - Direction * 2.0f;
            ApproachPoint.y = position.y;

            // Điểm thâm nhập sâu vào bên trong làng (vượt qua hàng rào)
            IngressPoint = position + Direction * 3.5f;
            IngressPoint.y = position.y;

            Width = width;
            Timestamp = Time.time;
            UnitsTraversed = 0;
        }

        public bool HasPassedPlane(Vector3 unitPos)
        {
            Vector3 toUnit = unitPos - Position;
            toUnit.y = 0;
            // Nếu vị trí của unit đã vượt qua mặt phẳng hàng rào về phía IngressPoint
            return Vector3.Dot(toUnit, Direction) > 0.5f;
        }
    }

    /// <summary>
    /// Trung tâm Điều phối Tác chiến Khe hở (Tactical Breach Memory).
    /// Ghi nhận tức thời mọi vị trí rào chắn bị phá hủy, điều phối bầy khủng long tràn qua cửa ngõ
    /// và thông báo cho NPC Quân Cố thủ lập phòng tuyến chốt chặn chokepoint.
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
            // Kiểm tra xem đã có breach nào gần vị trí này (< 2.0m) chưa
            for (int i = 0; i < _ActiveBreaches.Count; i++)
            {
                if (Vector3.Distance(_ActiveBreaches[i].Position, wallPosition) < 2.0f)
                {
                    _ActiveBreaches[i].Timestamp = Time.time;
                    _ActiveBreaches[i].Width += width * 0.5f; // Mở rộng khẩu độ khe hở
                    return _ActiveBreaches[i];
                }
            }

            BreachPoint newBreach = new BreachPoint(wallPosition, directionTowardsBase, width);
            _ActiveBreaches.Add(newBreach);

            Debug.Log($"<color=#FF5533><b>[Tactical AI Breach]</b></color> Đã phá thủng hàng rào tại ({wallPosition.x:F1}, {wallPosition.z:F1})! Mở đường thâm nhập vào làng!");

            OnBreachOpened?.Invoke(newBreach);
            return newBreach;
        }

        /// <summary>
        /// Tìm khe hở gần nhất có lợi thế hướng về căn cứ mục tiêu trong phạm vi cho phép.
        /// </summary>
        public BreachPoint GetBestBreachTowards(Vector3 unitPos, Vector3 targetBasePos, float maxSearchDistance = 25.0f)
        {
            if (_ActiveBreaches.Count == 0) return null;

            BreachPoint best = null;
            float bestScore = float.MinValue;

            Vector3 toBase = (targetBasePos - unitPos);
            toBase.y = 0;
            float distToBase = toBase.magnitude;

            for (int i = _ActiveBreaches.Count - 1; i >= 0; i--)
            {
                BreachPoint bp = _ActiveBreaches[i];
                if (bp == null) continue;

                float distToBreach = Vector3.Distance(unitPos, bp.Position);
                if (distToBreach > maxSearchDistance) continue;

                // Điểm số tác chiến: càng gần khe hở càng tốt, và khe hở càng rút ngắn khoảng cách tới Base càng tốt
                float distFromBreachToBase = Vector3.Distance(bp.IngressPoint, targetBasePos);
                float progressBonus = distToBase - distFromBreachToBase;

                float score = (progressBonus * 2.0f) - distToBreach;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = bp;
                }
            }

            return best;
        }

        /// <summary>
        /// Kiểm tra xem một vị trí (hoặc một bức tường kế bên) có nằm sát một khe hở đã mở hay không.
        /// Nếu nằm trong phạm vi (7m), khủng long TUYỆT ĐỐI KHÔNG TẤN CÔNG bức tường này mà phải chui qua khe hở!
        /// </summary>
        public bool IsWallNearActiveBreach(Vector3 wallPos, float threshold = 7.0f)
        {
            if (_ActiveBreaches.Count == 0) return false;

            for (int i = 0; i < _ActiveBreaches.Count; i++)
            {
                if (_ActiveBreaches[i] == null) continue;
                Vector3 diff = wallPos - _ActiveBreaches[i].Position;
                diff.y = 0;
                if (diff.magnitude <= threshold)
                {
                    return true;
                }
            }
            return false;
        }

        public void NotifyUnitTraversed(BreachPoint breach)
        {
            if (breach != null)
            {
                breach.UnitsTraversed++;
            }
        }

        public void ClearAll()
        {
            _ActiveBreaches.Clear();
            OnBreachesCleared?.Invoke();
        }
    }
}
