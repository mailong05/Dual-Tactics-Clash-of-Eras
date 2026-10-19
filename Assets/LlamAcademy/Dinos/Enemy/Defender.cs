using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Behavior;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using LlamAcademy.Dinos.Utility;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Enemy
{
    public enum DefenderRole
    {
        Archer,
        Spearman
    }

    public enum DefenderSector
    {
        HighGroundWatchtowers,  // Chòi gác & Cầu gỗ trên cao
        GroundFrontline,        // Tiền tuyến mặt đất phía trước
        VillageWoodenRoad,      // Tuyến đường cọc gỗ chạy ngang làng
        VillageCenter           // Trung tâm chân dốc và sân làng
    }

    [RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    public class Defender : Unit.Unit
    {
        [Header("Defender Combat Configuration")]
        [SerializeField] public DefenderRole Role = DefenderRole.Archer;
        [SerializeField] private float AttackRange = 14.0f;
        [SerializeField] private float AttackInterval = 1.2f;
        [SerializeField] private int AttackDamage = 28;
        [SerializeField] private float ProjectileSpeed = 24.0f;

        public AIState State =>
            GraphAgent != null && GraphAgent.GetVariable(EnemyGraphConstants.COMMAND, out BlackboardVariable<AIState> stateVariable)
                ? stateVariable.Value
                : AIState.Idle;

        public Vector3 TargetLocation => Agent != null && Agent.enabled && Agent.isOnNavMesh ? Agent.destination : transform.position;

        public static readonly List<Defender> AllDefenders = new();
        public bool IsEmergencyBaseDefender { get; private set; } = false;
        public Unit.Unit AssignedInvaderTarget { get; private set; } = null;
        private Vector3 _DefaultPostPosition;
        private bool _HasRecordedDefaultPost = false;
        public Vector3 DefaultPostPosition => _DefaultPostPosition;
        public bool HasRecordedDefaultPost => _HasRecordedDefaultPost;

        [Header("Sector & Patrol Configuration")]
        public DefenderSector Sector { get; private set; } = DefenderSector.GroundFrontline;
        private List<Vector3> _SectorPatrolWaypoints = new();
        private int _SectorPatrolIndex = 0;

        [Header("Watchtower Stationing")]
        public bool IsStationedAtTower { get; set; } = false;
        public Vector3 TowerPostPosition { get; set; } = Vector3.zero;

        [Header("Autonomous Free Roaming")]
        private float _RoamingStateTimer = 0f;
        private float _RoamingWaitDuration = 2.5f;
        private bool _IsPausingDuringRoam = false;

        public void AssignDefensiveSector(DefenderSector sector, Vector3 postPosition, List<Vector3> patrolWaypoints = null)
        {
            Sector = sector;
            _DefaultPostPosition = postPosition;
            _HasRecordedDefaultPost = true;
            IsEmergencyBaseDefender = false;
            AssignedInvaderTarget = null;

            if (sector == DefenderSector.HighGroundWatchtowers)
            {
                IsStationedAtTower = true;
                TowerPostPosition = postPosition;
            }
            else
            {
                IsStationedAtTower = false;
                TowerPostPosition = Vector3.zero;
            }

            if (patrolWaypoints != null && patrolWaypoints.Count > 0)
            {
                _SectorPatrolWaypoints = new List<Vector3>(patrolWaypoints);
                _SectorPatrolIndex = Random.Range(0, _SectorPatrolWaypoints.Count);
            }
            else
            {
                _SectorPatrolWaypoints.Clear();
            }

            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.isStopped = false;
                Agent.SetDestination(postPosition);
            }
            else
            {
                SetDestination(postPosition);
            }
        }

        public void StationAtTower(Vector3 postPosition)
        {
            AssignDefensiveSector(DefenderSector.HighGroundWatchtowers, postPosition);
        }

        public void AssignDefensivePost(Vector3 postPosition, bool isHighGround = false)
        {
            AssignDefensiveSector(isHighGround ? DefenderSector.HighGroundWatchtowers : DefenderSector.GroundFrontline, postPosition);
        }

        private float _LastAttackTime;
        private GameObject _HeldWeapon;
        private Transform _HandBone;
        private Collider[] _ScanBuffer = new Collider[24];
        private static int _SpawnCounter = 0;
        private bool _IsThrusting = false;
        private Vector3 _OriginalWeaponLocalPos;
        private Quaternion _OriginalWeaponLocalRot;
        private Vector3 _ThreatenedWallPos = Vector3.zero;
        private float _LastThreatTime = -99f;


        public void AlertWallThreat(Vector3 threatenedWallPos)
        {
            _ThreatenedWallPos = threatenedWallPos;
            _LastThreatTime = Time.time;
        }

        protected override void Awake()
        {
            base.Awake();
            if (MaxHealth <= 0)
            {
                MaxHealth = 180;
                Health = 180;
            }

            // Phân chia 50% NPC cầm cung bắn tên, 50% NPC cầm giáo phóng & đâm quái
            _SpawnCounter++;
            if (UnitType != null && UnitType.Type == LlamAcademy.Dinos.Unit.UnitType.Archer)
            {
                Role = DefenderRole.Archer;
            }
            else if (UnitType != null && (UnitType.Type == LlamAcademy.Dinos.Unit.UnitType.Cannoneer || UnitType.Type == LlamAcademy.Dinos.Unit.UnitType.Mage))
            {
                Role = DefenderRole.Spearman;
            }
            else
            {
                Role = (_SpawnCounter % 2 == 0) ? DefenderRole.Archer : DefenderRole.Spearman;
            }

            ConfigureRoleStats();

            if (Rigidbody != null)
            {
                Rigidbody.isKinematic = true;
                Rigidbody.useGravity = false;
            }

            if (Agent != null)
            {
                Agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                Agent.radius = 0.22f;
                Agent.avoidancePriority = Random.Range(20, 80);
                Agent.autoTraverseOffMeshLink = true;
            }
        }

        private void ConfigureRoleStats()
        {
            if (Role == DefenderRole.Archer)
            {
                AttackRange = 15.0f;
                AttackInterval = 1.2f;
                AttackDamage = 28;
                ProjectileSpeed = 24.0f;
                if (Agent != null) Agent.speed = 3.2f;
            }
            else // Spearman
            {
                AttackRange = 14.0f; // Tầm phóng giáo xa 14m (đủ tầm từ chòi quan sát ném xuống cổng làng), cận chiến < 2.5m
                AttackInterval = 1.6f;
                AttackDamage = 38;
                ProjectileSpeed = 26.0f;
                if (Agent != null) Agent.speed = 3.5f;
            }
        }

        protected override void Start()
        {
            base.Start();

            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated += OnNavMeshUpdated;
            }

            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.OnBreachOpened += HandleBreachOpened;
            }

            EquipWeaponVisual();

            EnsureGrounded();
        }

        [Header("Physics & Gravity Falling System")]
        [SerializeField] private float _Gravity = -16.0f;
        [SerializeField] private float _TerminalVelocity = -22.0f;
        private bool _IsFalling = false;
        private float _VerticalVelocity = 0f;
        public bool IsFalling => _IsFalling;
        private float _LastNavMeshPlacementAttemptTime = -999f;

        /// <summary>
        /// Bắn raycast tìm mặt sàn hoặc mặt đất gần nhất bên dưới NPC, bỏ qua các collider của chính NPC và quái vật.
        /// </summary>
        public bool RaycastFloor(Vector3 origin, float distance, out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
            float closestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null) continue;
                // Bỏ qua collider thuộc về chính NPC này (capsule, hitbox, weapon)
                if (h.collider.transform.root == transform.root) continue;
                if (h.collider.isTrigger) continue;

                // Bỏ qua các sinh vật sống khác (khủng long, NPC khác) để NPC không bị đứng trên đầu sinh vật
                if (h.collider.GetComponentInParent<Unit.Unit>() != null && h.collider.GetComponentInParent<Unit.Wall>() == null)
                {
                    continue;
                }

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Xác thực tọa độ xem có thuộc đúng mặt phẳng sàn cầu gỗ thực tế trong game hay không.
        /// Cầu Tây (West Bridge): X [-18.91, -8.91], Z [-16.35, -13.05] (tâm Z = -14.70m, rộng 3.3m), Y ~ 4.10m
        /// Cầu Đông (East Bridge): X [1.09, 21.09], Z [-16.35, -13.05] (tâm Z = -14.70m, rộng 3.3m), Y ~ 4.10m
        /// Bất kỳ tọa độ nào ở trên cao (Y >= 3.0m) nhưng lệch khỏi 2 mặt phẳng này đều là đứng/đi trên không trung hoặc lọt vào chòi.
        /// </summary>
        public static bool IsOnValidHighGroundPlane(Vector3 pos)
        {
            if (pos.y < 3.0f) return false;

            // Kiểm tra chiều rộng (trục Z): tâm -14.70m, biên độ ván gỗ tối đa +-2.0m
            if (Mathf.Abs(pos.z - (-14.70f)) > 2.0f)
            {
                return false;
            }

            // Chiều dài dọc trục X:
            // Cầu Tây: [-18.5, -9.5]
            // Cầu Đông: [1.8, 20.5]
            bool onWestBridge = pos.x >= -18.5f && pos.x <= -9.5f;
            bool onEastBridge = pos.x >= 1.8f && pos.x <= 20.5f;

            return onWestBridge || onEastBridge;
        }

        /// <summary>
        /// Kích hoạt trạng thái rơi tự do cho NPC khi mất điểm tựa bên dưới chân.
        /// </summary>
        public void StartFalling()
        {
            if (_IsFalling) return;

            _IsFalling = true;
            _VerticalVelocity = 0f;

            if (Agent != null && Agent.enabled)
            {
                Agent.enabled = false;
            }
        }

        /// <summary>
        /// Warp hoặc di chuyển NavMeshAgent an toàn tuyệt đối mà không bao giờ gây lỗi:
        /// "Failed to create agent because it is not close enough to the NavMesh" hoặc
        /// ""Warp" can only be called on an active agent that has been placed on a NavMesh".
        /// </summary>
        public bool SafeWarpOrPlace(Vector3 targetPos, float maxSearchRadius = 3.5f)
        {
            if (Agent == null) return false;

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = Agent.agentTypeID,
                areaMask = Agent.areaMask != 0 ? Agent.areaMask : NavMesh.AllAreas
            };

            // 1. Thử sample vị trí targetPos trên NavMesh
            if (!NavMesh.SamplePosition(targetPos, out NavMeshHit hit, maxSearchRadius, filter))
            {
                if (!NavMesh.SamplePosition(targetPos, out hit, 6.0f, filter))
                {
                    // Hoàn toàn không tìm thấy điểm NavMesh hợp lệ gần đó:
                    // Đặt vị trí transform, KHÔNG BẬT AGENT ĐỂ TRÁNH LỖI "Failed to create agent"
                    if (Agent.enabled) Agent.enabled = false;
                    transform.position = targetPos;
                    return false;
                }
            }

            if (hit.position.y < 0.08f)
            {
                hit.position = new Vector3(hit.position.x, 0.10f, hit.position.z);
            }

            // 2. Nếu Agent đã đang enabled và isOnNavMesh:
            if (Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.Warp(hit.position);
                return true;
            }

            // 3. Nếu Agent chưa trên NavMesh hoặc đang disabled:
            Agent.enabled = false;
            transform.position = hit.position;
            Physics.SyncTransforms();

            // Kiểm tra xem transform.position có đủ gần NavMesh (<= 0.5m) để bật agent an toàn
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit finalCheck, 0.5f, filter))
            {
                transform.position = finalCheck.position;
                Physics.SyncTransforms();
                Agent.enabled = true;
                if (Agent.isOnNavMesh)
                {
                    Agent.Warp(finalCheck.position);
                    return true;
                }
                else
                {
                    Agent.enabled = false;
                    return false;
                }
            }
            else
            {
                Agent.enabled = false;
                return false;
            }
        }

        /// <summary>
        /// Cập nhật trọng lực và cơ chế rơi tự do theo thời gian thực (spawn trên không trung, chòi/cầu sập).
        /// </summary>
        public void UpdateFallingAndGravity()
        {
            if (Health <= 0) return;

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = (Agent != null ? Agent.agentTypeID : 0),
                areaMask = (Agent != null && Agent.areaMask != 0 ? Agent.areaMask : NavMesh.AllAreas)
            };

            // 1. Nếu Agent đang di chuyển bình thường trên NavMesh:
            // Tuyệt đối KHÔNG can thiệp vật lý/rơi tự do! NavMeshAgent đảm bảo bám sàn 100% mượt mà!
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                _IsFalling = false;
                _VerticalVelocity = 0f;

                // Khóa an toàn: nếu agent đang ở trên NavMesh mà Y < 0.08m (lọt sàn):
                if (transform.position.y < 0.08f)
                {
                    Vector3 fixPos = new Vector3(transform.position.x, 0.10f, transform.position.z);
                    SafeWarpOrPlace(fixPos, 2.0f);
                }

                // BẢO VỆ MẶT PHẲNG CẦU GỖ:
                // Nếu NPC ở trên cao (Y > 1.2m) mà vì lý do nào đó chệch khỏi mặt phẳng cầu gỗ:
                // Lập tức đưa an toàn về vị trí chốt được chỉ định trên cầu gỗ, tuyệt đối không ngắt agent gây vòng lặp rơi.
                if (transform.position.y > 1.2f && !IsOnValidHighGroundPlane(transform.position))
                {
                    Vector3 returnPos = _HasRecordedDefaultPost ? _DefaultPostPosition : transform.position;
                    SafeWarpOrPlace(returnPos, 3.0f);
                }
                return;
            }

            // 2. NẾU ĐANG TRONG TRẠNG THÁI RƠI TỰ DO (_IsFalling == true):
            if (_IsFalling)
            {
                _VerticalVelocity = Mathf.Max(_VerticalVelocity + _Gravity * Time.deltaTime, _TerminalVelocity);
                float fallDelta = _VerticalVelocity * Time.deltaTime; // Giá trị âm (hướng xuống)
                float fallStep = Mathf.Abs(fallDelta);

                Vector3 currentPos = transform.position;
                Vector3 rayOrigin = currentPos + Vector3.up * 0.4f;
                float checkDist = fallStep + 0.6f;

                // Ưu tiên bắn raycast tìm mặt sàn đỡ (cầu gỗ, mặt đất,...)
                if (RaycastFloor(rayOrigin, checkDist, out RaycastHit landHit))
                {
                    // Nếu mặt sàn trúng raycast ở trên cao (Y > 1.2m):
                    // CHỈ ĐƯỢC PHÉP TIẾP ĐẤT NẾU NẰM TRÊN MẶT PHẲNG CẦU GỖ THẬT!
                    // Nếu là ngoài mép cầu hoặc collider ảo trên cao, BỎ QUA và tiếp tục rơi xuống đất!
                    if (landHit.point.y > 1.2f)
                    {
                        if (IsOnValidHighGroundPlane(landHit.point))
                        {
                            LandOnSurface(landHit.point);
                            return;
                        }
                    }
                    else
                    {
                        // Tiếp đất vững chắc trên mặt đất làng
                        LandOnSurface(landHit.point);
                        return;
                    }
                }

                // Nếu chưa chạm collider sàn đỡ, cập nhật độ cao rơi
                Vector3 nextPos = currentPos + Vector3.up * fallDelta;

                // Chống rơi xuyên qua đáy bản đồ (cao độ làng tối thiểu ~0.10m)
                if (nextPos.y <= 0.10f)
                {
                    nextPos.y = 0.10f;
                    LandOnSurface(nextPos);
                    return;
                }

                transform.position = nextPos;
                return;
            }

            // 3. Nếu KHÔNG rơi, nhưng Agent chưa trên NavMesh (ví dụ vừa spawn hoặc bị hẫng chân):
            Vector3 checkOrigin = transform.position + Vector3.up * 0.4f;
            if (RaycastFloor(checkOrigin, 0.7f, out RaycastHit floorHit))
            {
                if (floorHit.point.y > 1.2f)
                {
                    if (IsOnValidHighGroundPlane(floorHit.point))
                    {
                        LandOnSurface(floorHit.point);
                        return;
                    }
                }
                else
                {
                    LandOnSurface(floorHit.point);
                    return;
                }
            }

            // Nếu dưới chân không có sàn đỡ hoặc không hợp lệ: Bắt đầu rơi tự do!
            if (transform.position.y > 0.20f)
            {
                StartFalling();
                return;
            }

            // 4. Nếu ở sát mặt đất làng (Y <= 0.20f), kẹp cao độ an toàn và thử Warp định kỳ (1s/lần)
            if (transform.position.y < 0.10f)
            {
                transform.position = new Vector3(transform.position.x, 0.10f, transform.position.z);
            }

            if (Agent != null && (!Agent.enabled || !Agent.isOnNavMesh))
            {
                if (Time.time - _LastNavMeshPlacementAttemptTime >= 1.0f)
                {
                    _LastNavMeshPlacementAttemptTime = Time.time;
                    Vector3 groundPos = new Vector3(transform.position.x, 0.10f, transform.position.z);
                    SafeWarpOrPlace(groundPos, 5.0f);
                    if (Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
                    }
                }
            }
        }

        /// <summary>
        /// Xử lý tiếp đất/sàn an toàn khi kết thúc rơi tự do.
        /// </summary>
        private void LandOnSurface(Vector3 surfacePoint)
        {
            _IsFalling = false;
            _VerticalVelocity = 0f;

            // Đảm bảo không bao giờ nhận điểm tiếp đất âm dưới lòng đất
            if (surfacePoint.y < 0.08f)
            {
                surfacePoint.y = 0.10f;
            }

            transform.position = surfacePoint;
            Physics.SyncTransforms();

            // Nếu trước đó NPC đứng trên chòi gác cao (IsStationedAtTower),
            // nhưng do chòi bị sập nên đã rơi xuống đất (độ cao Y < 2.0m),
            // giải phóng vị trí chòi để NPC chuyển sang phòng thủ mặt đất!
            if (IsStationedAtTower && transform.position.y < 2.0f)
            {
                IsStationedAtTower = false;
                TowerPostPosition = Vector3.zero;
                _DefaultPostPosition = transform.position;
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = Agent != null ? Agent.agentTypeID : 0, areaMask = Agent != null ? Agent.areaMask : NavMesh.AllAreas };

            // Gán vị trí hợp lệ trên NavMesh tại cao độ mặt sàn tiếp đất
            if (NavMesh.SamplePosition(surfacePoint, out NavMeshHit navHit, 3.0f, filter))
            {
                if (navHit.position.y >= 0.08f && Mathf.Abs(navHit.position.y - surfacePoint.y) < 1.2f)
                {
                    transform.position = navHit.position;
                    surfacePoint = navHit.position;
                    Physics.SyncTransforms();
                }
            }

            // Kích hoạt lại NavMeshAgent một cách an toàn tuyệt đối
            if (Agent != null)
            {
                SafeWarpOrPlace(surfacePoint, 3.5f);
                if (Agent.isOnNavMesh)
                {
                    Agent.isStopped = false;
                    if (_HasRecordedDefaultPost)
                    {
                        Agent.SetDestination(_DefaultPostPosition);
                    }
                }
            }
        }

        /// <summary>
        /// Đảm bảo NPC luôn tiếp đất vững chắc hoặc kích hoạt rơi tự do nếu ở trên không.
        /// </summary>
        public void EnsureGrounded()
        {
            if (Health <= 0) return;
            if (_IsFalling) return; // Đang rơi tự do, không can thiệp!

            // Nếu đã trên NavMesh, hoàn toàn ổn định, không can thiệp
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                _IsFalling = false;
                _VerticalVelocity = 0f;
                return;
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = Agent != null ? Agent.agentTypeID : 0, areaMask = Agent != null ? Agent.areaMask : NavMesh.AllAreas };

            // 1. Kiểm tra NavMesh xung quanh vị trí hiện tại
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 2.5f, filter))
            {
                if (navHit.position.y >= 0.08f && Mathf.Abs(navHit.position.y - transform.position.y) < 1.5f)
                {
                    _IsFalling = false;
                    _VerticalVelocity = 0f;
                    SafeWarpOrPlace(navHit.position);
                    if (Agent != null && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
                    }
                    return;
                }
            }

            // 2. Nếu ở gần mặt đất (Y <= 1.5f), snap ngay về mặt đất làng, tuyệt đối không rơi
            if (transform.position.y <= 1.5f)
            {
                _IsFalling = false;
                _VerticalVelocity = 0f;
                Vector3 groundPos = new Vector3(transform.position.x, 0.10f, transform.position.z);
                SafeWarpOrPlace(groundPos, 4.0f);

                if (Agent != null && Agent.isOnNavMesh)
                {
                    Agent.isStopped = false;
                }
                return;
            }

            // 3. Nếu ở trên không trung cao (trên chòi/cầu), tìm sàn đỡ dưới chân
            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            if (RaycastFloor(rayOrigin, 2.5f, out RaycastHit supportHit) && (supportHit.point.y <= 1.2f || IsOnValidHighGroundPlane(supportHit.point)))
            {
                LandOnSurface(supportHit.point);
            }
            else
            {
                StartFalling();
            }
        }

        private void OnEnable()
        {
            if (!AllDefenders.Contains(this))
            {
                AllDefenders.Add(this);
            }

            // Triệt tiêu xung đột va chạm vật lý giữa các chiến binh đồng minh
            // Giúp NPC có thể lướt qua nhau trong lối hẹp (cầu, thang, cửa chòi) mà không bao giờ bị kẹt hay xô đẩy rơi xuống đất
            Collider myCol = GetComponent<Collider>();
            if (myCol != null)
            {
                foreach (var other in AllDefenders)
                {
                    if (other != null && other != this)
                    {
                        Collider otherCol = other.GetComponent<Collider>();
                        if (otherCol != null)
                        {
                            Physics.IgnoreCollision(myCol, otherCol, true);
                        }
                    }
                }
            }
        }

        private void OnDisable()
        {
            AllDefenders.Remove(this);

            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated -= OnNavMeshUpdated;
            }

            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.HasInstance)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.OnBreachOpened -= HandleBreachOpened;
            }
        }

        private void HandleBreachOpened(LlamAcademy.Dinos.AI.BreachPoint breach)
        {
            if (Health <= 0 || breach == null || IsStationedAtTower) return;
            float dist = Vector3.Distance(transform.position, breach.Position);
            if (dist > 38.0f) return; // Quá xa, giữ nguyên vị trí phân bổ làng

            // Lập tức cơ động chốt chặn cửa ngõ đột phá (Chokepoint Defense)
            Vector3 defensiveSpot = (Role == DefenderRole.Spearman)
                ? breach.IngressPoint + breach.Direction * 0.5f // Lính giáo dựng phên dậu chốt ngay mép trong
                : breach.IngressPoint + breach.Direction * 4.5f + Vector3.Cross(breach.Direction, Vector3.up) * (Random.value > 0.5f ? 2.5f : -2.5f); // Cung thủ đứng bọc lót sườn sau

            if (UnityEngine.AI.NavMesh.SamplePosition(defensiveSpot, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                SetDestination(hit.position);
            }
            else
            {
                SetDestination(breach.IngressPoint);
            }
        }

        private void EquipWeaponVisual()
        {
            if (_HeldWeapon != null) return;

            _HandBone = FindHandBone(transform);

            if (Role == DefenderRole.Archer)
            {
                _HeldWeapon = CreateBowVisual();
            }
            else
            {
                _HeldWeapon = CreateSpearVisual();
            }

            if (_HeldWeapon != null)
            {
                if (_HandBone != null)
                {
                    _HeldWeapon.transform.SetParent(_HandBone, false);
                }
                else
                {
                    _HeldWeapon.transform.SetParent(transform, false);
                    _HeldWeapon.transform.localPosition = new Vector3(0.3f, 0.85f, 0.35f);
                    _HeldWeapon.transform.localRotation = Quaternion.Euler(15f, 10f, 0f);
                }

                _OriginalWeaponLocalPos = _HeldWeapon.transform.localPosition;
                _OriginalWeaponLocalRot = _HeldWeapon.transform.localRotation;
            }
        }

        private Transform FindHandBone(Transform root)
        {
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                string n = t.name.ToLower();
                if ((n.Contains("hand") && (n.Contains("r") || n.Contains("right"))) ||
                    n.Contains("wrist_r") || n.Contains("hand_r") || n.Contains("r_hand"))
                {
                    return t;
                }
            }
            foreach (Transform t in allChildren)
            {
                if (t.name.ToLower().Contains("hand")) return t;
            }
            return null;
        }

        private static Material CreateSafeWeaponMaterial(Color color, float smoothness = 0.2f)
        {
            return LlamAcademy.Dinos.Rendering.PrehistoricShaderUtility.CreateSafeMaterial(color, smoothness);
        }

        private GameObject CreateBowVisual()
        {
            GameObject bow = new GameObject("Weapon_Bow");
            bow.transform.localPosition = new Vector3(0.05f, 0.05f, 0.05f);
            bow.transform.localRotation = Quaternion.Euler(0f, 85f, 80f);

            Material woodMat = CreateSafeWeaponMaterial(new Color(0.42f, 0.25f, 0.12f), 0.15f);
            Material stringMat = CreateSafeWeaponMaterial(new Color(0.92f, 0.88f, 0.80f), 0.05f);

            // Cán cầm trung tâm
            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(grip.GetComponent<Collider>());
            grip.name = "Grip";
            grip.transform.SetParent(bow.transform, false);
            grip.transform.localScale = new Vector3(0.04f, 0.18f, 0.04f);
            grip.transform.localPosition = Vector3.zero;
            grip.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Cánh cung trên
            GameObject upper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(upper.GetComponent<Collider>());
            upper.name = "UpperLimb";
            upper.transform.SetParent(bow.transform, false);
            upper.transform.localScale = new Vector3(0.035f, 0.28f, 0.035f);
            upper.transform.localPosition = new Vector3(0, 0.38f, 0.08f);
            upper.transform.localRotation = Quaternion.Euler(22f, 0, 0);
            upper.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Cánh cung dưới
            GameObject lower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(lower.GetComponent<Collider>());
            lower.name = "LowerLimb";
            lower.transform.SetParent(bow.transform, false);
            lower.transform.localScale = new Vector3(0.035f, 0.28f, 0.035f);
            lower.transform.localPosition = new Vector3(0, -0.38f, 0.08f);
            lower.transform.localRotation = Quaternion.Euler(-22f, 0, 0);
            lower.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Dây cung căng chắc
            GameObject bString = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(bString.GetComponent<Collider>());
            bString.name = "String";
            bString.transform.SetParent(bow.transform, false);
            bString.transform.localScale = new Vector3(0.012f, 0.52f, 0.012f);
            bString.transform.localPosition = new Vector3(0, 0, 0.15f);
            bString.GetComponent<Renderer>().sharedMaterial = stringMat;

            bow.transform.localScale = Vector3.one * 0.9f;
            return bow;
        }

        private GameObject CreateSpearVisual()
        {
            GameObject spear = new GameObject("Weapon_Spear");
            spear.transform.localPosition = new Vector3(0.02f, 0f, 0.05f);
            spear.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);

            Material woodMat = CreateSafeWeaponMaterial(new Color(0.36f, 0.20f, 0.08f), 0.15f);
            Material flintMat = CreateSafeWeaponMaterial(new Color(0.22f, 0.24f, 0.26f), 0.35f);
            Material gripMat = CreateSafeWeaponMaterial(new Color(0.65f, 0.38f, 0.18f), 0.1f);

            // Cán giáo dài bằng gỗ
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.name = "Shaft";
            shaft.transform.SetParent(spear.transform, false);
            shaft.transform.localScale = new Vector3(0.045f, 0.95f, 0.045f);
            shaft.transform.localPosition = new Vector3(0, 0.2f, 0);
            shaft.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Bọc da tay cầm
            GameObject leather = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(leather.GetComponent<Collider>());
            leather.name = "Grip";
            leather.transform.SetParent(spear.transform, false);
            leather.transform.localScale = new Vector3(0.055f, 0.22f, 0.055f);
            leather.transform.localPosition = new Vector3(0, 0.05f, 0);
            leather.GetComponent<Renderer>().sharedMaterial = gripMat;

            // Mũi giáo đá/sắt sắc bén
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(tip.GetComponent<Collider>());
            tip.name = "FlintTip";
            tip.transform.SetParent(spear.transform, false);
            tip.transform.localScale = new Vector3(0.075f, 0.24f, 0.025f);
            tip.transform.localPosition = new Vector3(0, 1.25f, 0);
            tip.GetComponent<Renderer>().sharedMaterial = flintMat;

            spear.transform.localScale = Vector3.one * 1.0f;
            return spear;
        }

        public static Vector3 GetBasePosition()
        {
            if (PrehistoricVillageBase.Instance != null)
                return PrehistoricVillageBase.Instance.transform.position;
            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.BaseTarget != null)
                return PrehistoricGameplayManager.Instance.BaseTarget.position;
            if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
                return RoundManager.Instance.DinoTarget.position;
            return new Vector3(-2.23f, 0f, -38.26f);
        }

        private static readonly List<Unit.Unit> s_InvadingDinos = new();
        private static float s_LastCoordinationTime = -99f;

        public static List<Unit.Unit> GetDinosaursInBase(Vector3 basePos, float baseRadius = 15.0f)
        {
            s_InvadingDinos.Clear();

            // 1. Quét từ ActiveDinos của PrehistoricDinoBase
            foreach (var dino in PrehistoricDinoBase.ActiveDinos)
            {
                if (dino == null || dino.Health <= 0 || !dino.gameObject.activeInHierarchy) continue;
                if (dino.HasReachedBase || Vector3.Distance(dino.transform.position, basePos) <= baseRadius)
                {
                    s_InvadingDinos.Add(dino);
                }
            }

            // 2. Quét từ ActiveMonsters của PrehistoricGameplayManager (bao gồm cả FlyingUnit và RunnerDino)
            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.ActiveMonsters != null)
            {
                foreach (var monster in PrehistoricGameplayManager.Instance.ActiveMonsters)
                {
                    if (monster == null || monster.Health <= 0 || !monster.gameObject.activeInHierarchy) continue;
                    if (s_InvadingDinos.Contains(monster)) continue;

                    if (monster is PrehistoricDinoBase pdb && (pdb.HasReachedBase || Vector3.Distance(pdb.transform.position, basePos) <= baseRadius))
                    {
                        s_InvadingDinos.Add(monster);
                    }
                    else if (monster is FlyingUnit fu && (fu.HasReachedBase || Vector3.Distance(fu.transform.position, basePos) <= baseRadius))
                    {
                        s_InvadingDinos.Add(monster);
                    }
                    else if (Vector3.Distance(monster.transform.position, basePos) <= baseRadius)
                    {
                        s_InvadingDinos.Add(monster);
                    }
                }
            }

            return s_InvadingDinos;
        }

        public static void CoordinateBaseDefenders()
        {
            if (Time.time - s_LastCoordinationTime < 0.25f) return;
            s_LastCoordinationTime = Time.time;

            Vector3 basePos = GetBasePosition();
            var invadingDinos = GetDinosaursInBase(basePos, 15.0f);
            int dinoCount = invadingDinos.Count;

            // Dọn dẹp AllDefenders null hoặc đã chết
            AllDefenders.RemoveAll(d => d == null || d.Health <= 0 || !d.gameObject.activeInHierarchy);

            if (dinoCount == 0)
            {
                // Không có khủng long trong căn cứ: Giải phóng mọi NPC về nhiệm vụ thông thường
                for (int i = 0; i < AllDefenders.Count; i++)
                {
                    var d = AllDefenders[i];
                    if (d.IsEmergencyBaseDefender)
                    {
                        d.IsEmergencyBaseDefender = false;
                        d.AssignedInvaderTarget = null;
                        if (d._HasRecordedDefaultPost && d.Agent != null && d.Agent.enabled && d.Agent.isOnNavMesh && !d.Agent.isOnOffMeshLink)
                        {
                            d.Agent.SetDestination(d._DefaultPostPosition);
                        }
                    }
                }
                return;
            }

            // CÓ KHỦNG LONG XÂM NHẬP / CẮN PHÁ CĂN CỨ:
            // Phân công số lượng NPC dựa trên số lượng khủng long trong căn cứ (mỗi con 2 NPC)
            int quota = Mathf.Clamp(dinoCount * 2, 2, AllDefenders.Count);

            // Sắp xếp các NPC theo khoảng cách gần căn cứ nhất để điều động chi viện
            AllDefenders.Sort((a, b) =>
                Vector3.Distance(a.transform.position, basePos).CompareTo(Vector3.Distance(b.transform.position, basePos))
            );

            for (int i = 0; i < AllDefenders.Count; i++)
            {
                var def = AllDefenders[i];
                if (def == null) continue;

                // Tuyệt đối không điều động chiến binh đang chốt giữ trên chòi cao đi chi viện mặt đất
                if (def.IsStationedAtTower)
                {
                    def.IsEmergencyBaseDefender = false;
                    def.AssignedInvaderTarget = null;
                    continue;
                }

                if (i < quota)
                {
                    def.IsEmergencyBaseDefender = true;
                    def.AssignedInvaderTarget = invadingDinos[i % dinoCount];
                }
                else
                {
                    // Các NPC còn lại tiếp tục bảo vệ tuyến ngoài làng
                    def.IsEmergencyBaseDefender = false;
                    def.AssignedInvaderTarget = null;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (Health <= 0) return;

            // Khóa an toàn tuyệt đối chống chìm dưới lòng đất (Absolute Surface Floor Protection)
            if (transform.position.y < 0.08f)
            {
                transform.position = new Vector3(transform.position.x, 0.10f, transform.position.z);
            }

            // Xử lý trọng lực và rơi tự do cho NPC khi sinh ra trên không trung hoặc khi chòi/cầu bị phá hủy
            UpdateFallingAndGravity();

            // Nếu đang rơi tự do giữa không trung, tạm dừng các hành vi mặt đất cho đến khi tiếp đất an toàn
            if (_IsFalling) return;

            if (!_HasRecordedDefaultPost)
            {
                _DefaultPostPosition = transform.position;
                _HasRecordedDefaultPost = true;
            }

            // Tự động điều phối NPC bảo vệ căn cứ theo số lượng khủng long đang xâm nhập
            CoordinateBaseDefenders();

            // Chu trình chiến đấu liên tục bảo vệ ngôi làng
            HandleCombatBehavior();
        }

        private void HandleCombatBehavior()
        {
            // Nếu NPC đang bước qua NavMeshLink (cầu thang lên chòi), Unity tự động traverse mượt mà
            if (Agent != null && Agent.isOnOffMeshLink)
            {
                if (Animator != null)
                {
                    Animator.SetFloat(AnimationConstants.SPEED_PARAMETER, Mathf.Max(Agent.speed, 2.5f));
                }
                return;
            }

            // 0. NẾU LÀ NPC ĐƯỢC PHÂN CÔNG CHỐT GIỮ HOẶC TUẦN TRA TRÊN CHÒI CAO VÀ CẦU TRÊN CAO
            if (IsStationedAtTower || Sector == DefenderSector.HighGroundWatchtowers)
            {
                float highGroundRange = AttackRange + 6.0f;
                Collider towerTargetCol = FindNearestHostileMonster(highGroundRange);

                if (towerTargetCol != null)
                {
                    Vector3 targetPos = towerTargetCol.bounds.center;
                    float distToTarget = Vector3.Distance(transform.position, targetPos);

                    Vector3 faceDir = (targetPos - transform.position);
                    faceDir.y = 0;
                    if (faceDir.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 10f);
                    }

                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh && !Agent.isOnOffMeshLink)
                    {
                        Agent.isStopped = true;
                    }

                    if (Time.time >= _LastAttackTime + AttackInterval)
                    {
                        _LastAttackTime = Time.time;
                        PerformAttack(towerTargetCol, targetPos, distToTarget);
                    }
                }
                else
                {
                    if (Animator != null)
                    {
                        Animator.SetBool(AnimationConstants.IS_ATTACKING_PARAMETER, false);
                    }

                    UpdateAutonomousRoaming();
                }
                return;
            }

            // 1. NẾU LÀ NPC ĐƯỢC CHỌN CHI VIỆN CĂN CỨ: ƯU TIÊN SỐ 1 HẠ GỤC KHỦNG LONG TRONG CĂN CỨ
            if (IsEmergencyBaseDefender)
            {
                Unit.Unit targetDino = (AssignedInvaderTarget != null && AssignedInvaderTarget.Health > 0 && AssignedInvaderTarget.gameObject.activeInHierarchy)
                    ? AssignedInvaderTarget
                    : null;

                if (targetDino == null)
                {
                    Vector3 basePos = GetBasePosition();
                    var invaders = GetDinosaursInBase(basePos, 16.0f);
                    if (invaders.Count > 0)
                    {
                        targetDino = invaders[0];
                        AssignedInvaderTarget = targetDino;
                    }
                }

                if (targetDino != null)
                {
                    Vector3 targetPos = targetDino.transform.position;
                    float distToTarget = Vector3.Distance(transform.position, targetPos);

                    // Xoay mặt về hướng khủng long đang cắn phá căn cứ
                    Vector3 faceDir = (targetPos - transform.position);
                    faceDir.y = 0;
                    if (faceDir.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 10f);
                    }

                    // Nếu đã vào tầm đánh
                    if (distToTarget <= AttackRange)
                    {
                        if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                        {
                            Agent.isStopped = true;
                        }

                        if (Time.time >= _LastAttackTime + AttackInterval)
                        {
                            _LastAttackTime = Time.time;
                            Collider col = targetDino.GetComponentInChildren<Collider>();
                            PerformAttack(col, targetPos, distToTarget);
                        }
                    }
                    else
                    {
                        // Tiến nhanh về căn cứ tiếp cận khủng long theo đội hình tản ra bao vây
                        Vector3 appPos = CalculateDispersedApproach(targetPos);
                        if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                        {
                            Agent.isStopped = false;
                            if (!Agent.hasPath || Vector3.Distance(Agent.destination, appPos) > 1.0f)
                            {
                                Agent.SetDestination(appPos);
                            }
                        }
                    }
                    return;
                }
            }

            // 2. TÁC CHIẾN BÌNH THƯỜNG (KHI KHÔNG CÓ BÁO ĐỘNG HOẶC LÀ NPC TUYẾN NGOÀI)
            Collider targetCol = FindNearestHostileMonster(AttackRange + 3.0f);

            // Nếu không có quái ngay sát cạnh, nhưng tường/cổng làng đang bị tấn công gần đây (< 8 giây)
            if (targetCol == null && Time.time - _LastThreatTime < 8.0f && _ThreatenedWallPos != Vector3.zero)
            {
                targetCol = FindHostileMonsterNearPoint(_ThreatenedWallPos, 14.0f);
            }

            if (targetCol != null)
            {
                Vector3 targetPos = targetCol.bounds.center;
                float distToTarget = Vector3.Distance(transform.position, targetPos);

                // Xoay mượt mà đối diện hướng khủng long tấn công
                Vector3 faceDir = (targetPos - transform.position);
                faceDir.y = 0;
                if (faceDir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 10f);
                }

                // Nếu mục tiêu trong tầm đánh
                if (distToTarget <= AttackRange)
                {
                    // Cung thủ kiting chiến thuật: nếu quái cận chiến áp sát (< 3.8m), lùi lại giữ cự ly an toàn
                    if (Role == DefenderRole.Archer && distToTarget < 3.8f)
                    {
                        Vector3 retreatDir = (transform.position - targetPos).normalized;
                        retreatDir.y = 0;
                        Vector3 kitePos = transform.position + retreatDir * 4.0f;
                        if (NavMesh.SamplePosition(kitePos, out NavMeshHit kHit, 4.0f, NavMesh.AllAreas))
                        {
                            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                            {
                                Agent.isStopped = false;
                                if (!Agent.hasPath || Vector3.Distance(Agent.destination, kHit.position) > 0.5f)
                                {
                                    Agent.SetDestination(kHit.position);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                        {
                            Agent.isStopped = true;
                        }
                    }

                    if (Time.time >= _LastAttackTime + AttackInterval)
                    {
                        _LastAttackTime = Time.time;
                        PerformAttack(targetCol, targetPos, distToTarget);
                    }
                }
                else
                {
                    // Tiếp cận khủng long theo chiến thuật tản rộng: Spearman bao vây tròn 360 độ, Archer xòe nan quạt
                    Vector3 approachTarget = CalculateDispersedApproach(targetPos);
                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
                        if (!Agent.hasPath || Vector3.Distance(Agent.destination, approachTarget) > 1.0f)
                        {
                            Agent.SetDestination(approachTarget);
                        }
                    }
                }
            }
            else
            {
                if (Animator != null)
                {
                    Animator.SetBool(AnimationConstants.IS_ATTACKING_PARAMETER, false);
                }

                UpdateAutonomousRoaming();
            }
        }

        /// <summary>
        /// Cơ chế tuần tra và tự do di chuyển linh hoạt của NPC chiến binh làng trong thời gian chuẩn bị và khi không có quái vật.
        /// Giúp ngôi làng luôn tràn đầy sức sống, NPC chủ động đi lại giữ khoảng cách, cảnh giới các hướng.
        /// </summary>
        private void UpdateAutonomousRoaming()
        {
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh || Agent.isOnOffMeshLink) return;

            _RoamingStateTimer += Time.deltaTime;

            if (_IsPausingDuringRoam)
            {
                // NPC dừng lại đứng cảnh giới, quan sát trận địa
                Agent.isStopped = true;
                if (Animator != null)
                {
                    Animator.SetFloat(AnimationConstants.SPEED_PARAMETER, 0f);
                }

                // Xoay mặt đón hướng quái vật xâm nhập (ngược hướng căn cứ làng)
                Vector3 basePos = GetBasePosition();
                Vector3 lookDir = (transform.position - basePos);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 3.0f);
                }

                ApplyMutualSeparation();

                if (_RoamingStateTimer >= _RoamingWaitDuration)
                {
                    _RoamingStateTimer = 0f;
                    _IsPausingDuringRoam = false;
                    _RoamingWaitDuration = Random.Range(0.8f, 2.0f);

                    Vector3 nextDest = PickNextRoamingDestination();
                    Agent.isStopped = false;
                    if (Agent.speed < 2.5f) Agent.speed = (Role == DefenderRole.Archer ? 3.2f : 3.5f);
                    Agent.SetDestination(nextDest);
                }
            }
            else
            {
                // NPC đang bước đi tuần tra
                Agent.isStopped = false;

                if (!Agent.pathPending)
                {
                    bool arrived = Agent.hasPath && Agent.remainingDistance <= 1.2f;
                    bool timeout = _RoamingStateTimer >= 8.0f;
                    bool noPathStuck = !Agent.hasPath && _RoamingStateTimer >= 1.5f;
                    bool velocityStuck = _RoamingStateTimer >= 2.0f && Agent.velocity.sqrMagnitude < 0.04f;

                    if (arrived || timeout || noPathStuck || velocityStuck)
                    {
                        _RoamingStateTimer = 0f;
                        _IsPausingDuringRoam = true;
                        Agent.isStopped = true;
                        if (Animator != null)
                        {
                            Animator.SetFloat(AnimationConstants.SPEED_PARAMETER, 0f);
                        }
                    }
                    else
                    {
                        if (Animator != null)
                        {
                            float currentSpeed = Agent.velocity.magnitude;
                            Animator.SetFloat(AnimationConstants.SPEED_PARAMETER, currentSpeed > 0.1f ? currentSpeed : Agent.speed);
                        }
                        ApplyMutualSeparation();
                    }
                }
                else
                {
                    // Trong khi pathPending == true (đang tính toán đường đi), tiếp tục duy trì chuyển động
                    if (Animator != null)
                    {
                        Animator.SetFloat(AnimationConstants.SPEED_PARAMETER, Agent.speed);
                    }
                }
            }
        }

        private static Vector3 GetNearestTowerCenter(Vector3 pos)
        {
            Vector3 centerTower = new Vector3(-3.87f, 4.15f, -14.68f);
            Vector3 leftTower = new Vector3(-23.87f, 4.15f, -14.68f);
            Vector3 rightTower = new Vector3(26.10f, 4.15f, -14.68f);

            float dC = (pos - centerTower).sqrMagnitude;
            float dL = (pos - leftTower).sqrMagnitude;
            float dR = (pos - rightTower).sqrMagnitude;

            if (dL < dC && dL < dR) return leftTower;
            if (dR < dC && dR < dL) return rightTower;
            return centerTower;
        }

        private Vector3 PickNextRoamingDestination()
        {
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = (Agent != null ? Agent.agentTypeID : 0), areaMask = (Agent != null ? Agent.areaMask : NavMesh.AllAreas) };

            // 1. Cầu gỗ trên cao: Chỉ tuần tra qua lại trên đúng chiều dài cầu gỗ đó, khóa chặt trục Z = -14.70m (tuyệt đối không vào chòi gác)
            if (IsStationedAtTower || Sector == DefenderSector.HighGroundWatchtowers || transform.position.y >= 3.0f)
            {
                Vector3 anchor = (_HasRecordedDefaultPost && _DefaultPostPosition != Vector3.zero) ? _DefaultPostPosition : transform.position;
                bool isWestBridge = anchor.x < 0; // Cầu Tây vs Cầu Đông
                
                // Giới hạn trục X chuẩn xác cho tuần tra cầu gỗ:
                // Cầu Tây: [-17.2m, -10.5m] (trên cầu gỗ Tây, tránh xa 2 đầu nối chòi)
                // Cầu Đông: [3.0m, 19.0m] (trên cầu gỗ Đông, tránh xa 2 đầu nối chòi)
                float minX = isWestBridge ? -17.2f : 3.0f;
                float maxX = isWestBridge ? -10.5f : 19.0f;

                float currentX = transform.position.x;
                float targetX;
                if (currentX < (minX + maxX) * 0.5f)
                {
                    targetX = Random.Range(currentX + 2.0f, maxX);
                }
                else
                {
                    targetX = Random.Range(minX, currentX - 2.0f);
                }
                targetX = Mathf.Clamp(targetX, minX, maxX);

                // Khóa Z chuẩn xác ở tim cầu (-14.70m), cao độ sàn ván Y = 4.10m
                Vector3 bridgeCandidate = new Vector3(targetX, 4.10f, -14.70f);
                if (NavMesh.SamplePosition(bridgeCandidate, out NavMeshHit bHit, 2.0f, filter))
                {
                    if (bHit.position.y >= 3.5f)
                    {
                        return bHit.position;
                    }
                }
                return bridgeCandidate;
            }

            // 2. Nếu có danh sách mốc tuần tra theo Sector (tiền tuyến hoặc sân làng)
            if (_SectorPatrolWaypoints != null && _SectorPatrolWaypoints.Count > 0)
            {
                // 70% di chuyển sang mốc tiếp theo trong danh sách, 30% đi dạo quanh mốc hiện tại
                if (Random.value < 0.7f)
                {
                    _SectorPatrolIndex = (_SectorPatrolIndex + 1) % _SectorPatrolWaypoints.Count;
                    Vector3 target = _SectorPatrolWaypoints[_SectorPatrolIndex];
                    Vector2 r = Random.insideUnitCircle * 1.5f;
                    Vector3 candidate = target + new Vector3(r.x, 0, r.y);
                    if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, filter))
                    {
                        if (hit.position.y >= 0.08f) return hit.position;
                    }
                    if (target.y >= 0.08f) return target;
                }
                else
                {
                    Vector3 current = _SectorPatrolWaypoints[_SectorPatrolIndex];
                    Vector2 r = Random.insideUnitCircle * 2.0f;
                    Vector3 candidate = current + new Vector3(r.x, 0, r.y);
                    if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.0f, filter))
                    {
                        if (hit.position.y >= 0.08f) return hit.position;
                    }
                }
            }

            // 3. Mặt đất: Tuần tra tự do, di chuyển linh hoạt quanh khu vực làng được giao
            Vector3 center = (_HasRecordedDefaultPost && _DefaultPostPosition != Vector3.zero) ? _DefaultPostPosition : transform.position;
            float roamDist = Random.Range(3.5f, 9.0f);
            float roamAngle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 groundCandidate = new Vector3(center.x + Mathf.Cos(roamAngle) * roamDist, 0.15f, center.z + Mathf.Sin(roamAngle) * roamDist);
            if (NavMesh.SamplePosition(groundCandidate, out NavMeshHit gHit, 5.0f, filter))
            {
                if (gHit.position.y >= 0.08f && gHit.position.y <= 2.5f)
                {
                    return gHit.position;
                }
            }

            // Fallback: Thử tìm điểm NavMesh trong bán kính 6.0m quanh vị trí hiện tại
            Vector2 localOffset = Random.insideUnitCircle.normalized * Random.Range(3.0f, 6.0f);
            Vector3 localCandidate = transform.position + new Vector3(localOffset.x, 0, localOffset.y);
            if (NavMesh.SamplePosition(localCandidate, out NavMeshHit localGHit, 4.0f, filter))
            {
                if (localGHit.position.y >= 0.08f && localGHit.position.y <= 2.5f)
                {
                    return localGHit.position;
                }
            }

            return center;
        }

        /// <summary>
        /// Tính toán vị trí tiếp cận phân tán: Spearman bao vây vòng tròn 360 độ, Archer xòe nan quạt giữ cự ly.
        /// Triệt tiêu 100% hiện tượng NPC bu dồn thành một cụm!
        /// </summary>
        private Vector3 CalculateDispersedApproach(Vector3 targetPos)
        {
            Vector3 approachDest = targetPos;
            if (Role == DefenderRole.Archer)
            {
                // Cung thủ: Xòe nan quạt ±54 độ, giữ khoảng cách 8.5m
                int archerSlot = Mathf.Abs(GetHashCode()) % 7;
                float fanAngle = (archerSlot - 3) * 18f;
                Vector3 awayDir = (transform.position - targetPos);
                awayDir.y = 0;
                if (awayDir.sqrMagnitude < 0.001f) awayDir = Vector3.back;
                awayDir.Normalize();
                Vector3 spreadDir = Quaternion.Euler(0, fanAngle, 0) * awayDir;
                approachDest = targetPos + spreadDir * 8.5f;
            }
            else // Spearman
            {
                // Chiến binh giáo: Bao vây 8 góc đối xứng quanh quái vật (bán kính 2.8m)
                int flankSlot = Mathf.Abs(GetHashCode()) % 8;
                float angleRad = (flankSlot * 45f + 22.5f) * Mathf.Deg2Rad;
                Vector3 surroundOffset = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)) * 2.8f;
                approachDest = targetPos + surroundOffset;
            }

            if (NavMesh.SamplePosition(approachDest, out NavMeshHit appHit, 3.5f, NavMesh.AllAreas))
            {
                return appHit.position;
            }
            return approachDest;
        }

        private void PerformAttack(Collider targetCol, Vector3 targetPos, float distance)
        {
            if (Animator != null)
            {
                Animator.SetBool(AnimationConstants.IS_ATTACKING_PARAMETER, true);
            }

            Vector3 spawnOrigin = (_HeldWeapon != null)
                ? _HeldWeapon.transform.position + transform.forward * 0.4f + Vector3.up * 0.2f
                : transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;

            if (Role == DefenderRole.Archer)
            {
                // Bắn tên bay về phía khủng long
                GameObject arrow = CreateFlyingArrow(spawnOrigin, (targetPos - spawnOrigin).normalized);
                StartCoroutine(AnimateProjectile(arrow, targetCol, targetPos, AttackDamage, ProjectileSpeed, 0.15f));
            }
            else // Spearman
            {
                if (distance <= 2.5f)
                {
                    // Đâm giáo cận chiến uy lực
                    StartCoroutine(AnimateMeleeThrust(targetCol));
                }
                else
                {
                    // Phóng ngọn giáo bay cắm vào khủng long
                    GameObject flyingSpear = CreateFlyingSpear(spawnOrigin, (targetPos - spawnOrigin).normalized);
                    StartCoroutine(AnimateProjectile(flyingSpear, targetCol, targetPos, AttackDamage, ProjectileSpeed, 0.12f));

                    // Tạm ẩn giáo trên tay rồi rút ngọn giáo mới ra phóng tiếp
                    StartCoroutine(AnimateSpearThrowRespawn());
                }
            }
        }

        private IEnumerator AnimateMeleeThrust(Collider targetCol)
        {
            if (_HeldWeapon == null || _IsThrusting) yield break;
            _IsThrusting = true;

            float elapsed = 0f;
            float duration = 0.22f;
            Vector3 startPos = _OriginalWeaponLocalPos;
            Vector3 thrustPos = _OriginalWeaponLocalPos + Vector3.forward * 0.5f + Vector3.up * 0.1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float pingPong = Mathf.Sin(t * Mathf.PI);
                _HeldWeapon.transform.localPosition = Vector3.Lerp(startPos, thrustPos, pingPong);
                yield return null;
            }

            _HeldWeapon.transform.localPosition = startPos;
            _IsThrusting = false;

            if (targetCol != null && IsHostileMonster(targetCol) && targetCol.TryGetComponent(out IDamageable dmg))
            {
                dmg.TakeDamage(AttackDamage + 5);
            }
        }

        private IEnumerator AnimateSpearThrowRespawn()
        {
            if (_HeldWeapon == null) yield break;
            _HeldWeapon.SetActive(false);
            yield return new WaitForSeconds(0.45f);
            if (_HeldWeapon != null && Health > 0)
            {
                _HeldWeapon.SetActive(true);
            }
        }

        private IEnumerator AnimateProjectile(GameObject proj, Collider target, Vector3 lastKnownPos, int dmgAmount, float speed, float arcFactor)
        {
            float elapsed = 0f;
            Vector3 start = proj.transform.position;

            while (proj != null)
            {
                Vector3 currentTargetPos = (target != null && target.gameObject.activeInHierarchy)
                    ? target.bounds.center
                    : lastKnownPos;

                float distance = Vector3.Distance(start, currentTargetPos);
                float duration = Mathf.Max(0.06f, distance / speed);

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 linear = Vector3.Lerp(start, currentTargetPos, t);
                float arc = Mathf.Sin(t * Mathf.PI) * (distance * arcFactor);
                proj.transform.position = new Vector3(linear.x, linear.y + arc, linear.z);

                Vector3 moveDir = (currentTargetPos - proj.transform.position).normalized;
                if (moveDir != Vector3.zero) proj.transform.rotation = Quaternion.LookRotation(moveDir);

                if (t >= 1.0f || (target != null && Vector3.Distance(proj.transform.position, currentTargetPos) < 0.8f))
                {
                    if (target != null && IsHostileMonster(target) && target.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.TakeDamage(dmgAmount);
                    }
                    Destroy(proj);
                    yield break;
                }

                yield return null;
            }
        }

        private GameObject CreateFlyingArrow(Vector3 pos, Vector3 dir)
        {
            GameObject arrow = new GameObject("Flying_Arrow");
            arrow.transform.position = pos;
            arrow.transform.rotation = Quaternion.LookRotation(dir);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(arrow.transform, false);
            shaft.transform.localScale = new Vector3(0.04f, 0.45f, 0.04f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            Material mat = CreateSafeWeaponMaterial(new Color(0.48f, 0.30f, 0.15f), 0.15f);
            shaft.GetComponent<Renderer>().sharedMaterial = mat;

            return arrow;
        }

        private GameObject CreateFlyingSpear(Vector3 pos, Vector3 dir)
        {
            GameObject spear = new GameObject("Flying_Spear");
            spear.transform.position = pos;
            spear.transform.rotation = Quaternion.LookRotation(dir);

            Material woodMat = CreateSafeWeaponMaterial(new Color(0.38f, 0.22f, 0.10f), 0.15f);
            Material flintMat = CreateSafeWeaponMaterial(new Color(0.20f, 0.22f, 0.24f), 0.35f);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(spear.transform, false);
            shaft.transform.localScale = new Vector3(0.05f, 0.9f, 0.05f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            shaft.GetComponent<Renderer>().sharedMaterial = woodMat;

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(tip.GetComponent<Collider>());
            tip.transform.SetParent(spear.transform, false);
            tip.transform.localScale = new Vector3(0.08f, 0.25f, 0.03f);
            tip.transform.localPosition = new Vector3(0, 0, 0.95f);
            tip.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            tip.GetComponent<Renderer>().sharedMaterial = flintMat;

            return spear;
        }

        private Collider FindNearestHostileMonster(float searchRange)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, searchRange, _ScanBuffer);
            Collider best = null;
            float bestPriorityScore = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _ScanBuffer[i];
                if (!IsHostileMonster(col)) continue;

                Vector3 targetPos = col.bounds.center;
                float dist = Vector3.Distance(transform.position, targetPos);

                // Điểm số phân cấp độ nguy hiểm chiến thuật (Tactical Threat Scoring)
                float score = 100f - dist;

                Vector3 basePos = GetBasePosition();
                bool isInvadingBase = Vector3.Distance(targetPos, basePos) <= 15.0f;
                if (col.TryGetComponent(out PrehistoricDinoBase pdb) && pdb.HasReachedBase) isInvadingBase = true;
                if (col.TryGetComponent(out FlyingUnit fu) && fu.HasReachedBase) isInvadingBase = true;

                // 0. KHỦNG LONG ĐANG XÂM NHẬP / CẮN PHÁ CĂN CỨ: ƯU TIÊN SỐ 1 TUYỆT ĐỐI!
                if (isInvadingBase)
                {
                    score += 500f;
                }

                string n = col.gameObject.name.ToLower();
                // 1. Quái chạy nhanh (Raptor) tiếp cận căn cứ: độ nguy hiểm cực cao
                if (n.Contains("raptor") || n.Contains("runner")) score += 35f;

                // 2. Boss T-Rex: dồn hỏa lực tiêu diệt mục tiêu thủ lĩnh
                if (n.Contains("trex") || n.Contains("t-rex") || n.Contains("boss")) score += 40f;

                // 3. Quái đang vượt qua khe hở thâm nhập làng
                if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null &&
                    LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.IsWallNearActiveBreach(targetPos, 5.0f))
                {
                    score += 50f;
                }

                if (score > bestPriorityScore)
                {
                    bestPriorityScore = score;
                    best = col;
                }
            }
            return best;
        }

        private Collider FindHostileMonsterNearPoint(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, _ScanBuffer);
            Collider best = null;
            float closestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = _ScanBuffer[i];
                if (!IsHostileMonster(col)) continue;
                float d = Vector3.Distance(transform.position, col.bounds.center);
                if (d < closestDist)
                {
                    closestDist = d;
                    best = col;
                }
            }
            return best;
        }

        private void OnNavMeshUpdated()
        {
            if (Agent == null || !Agent.enabled) return;
            if (Agent.isOnOffMeshLink) return; // Đang vượt cầu thang hoặc sàn chòi qua NavMeshLink, không được can thiệp
            if (_IsFalling) return; // Đang rơi tự do, không can thiệp

            // 1. Nếu Agent đã ở trên NavMesh hợp lệ
            if (Agent.isOnNavMesh)
            {
                // Kiểm tra an toàn độ cao mặt đất: tuyệt đối không cho phép NPC ở dưới lòng đất
                if (transform.position.y < 0.08f)
                {
                    Vector3 safeGround = new Vector3(transform.position.x, 0.10f, transform.position.z);
                    SafeWarpOrPlace(safeGround);
                }
                return;
            }

            // 2. Nếu Agent chưa trên NavMesh hoặc vừa bị mất liên kết sau khi rebake:
            NavMeshQueryFilter filter = new NavMeshQueryFilter() { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask };
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.5f, filter))
            {
                if (hit.position.y >= 0.08f && Mathf.Abs(hit.position.y - transform.position.y) < 1.5f)
                {
                    SafeWarpOrPlace(hit.position);
                    return;
                }
            }

            // Nếu ở mặt đất (Y <= 1.5m): bảo vệ NPC tuyệt đối không bao giờ rơi lún xuống đất
            if (transform.position.y <= 1.5f)
            {
                Vector3 gPos = new Vector3(transform.position.x, 0.10f, transform.position.z);
                SafeWarpOrPlace(gPos, 4.0f);
                return;
            }

            // Nếu ở trên cao (chòi/cầu), kiểm tra xem có sàn bên dưới không
            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            if (!RaycastFloor(rayOrigin, 2.0f, out _))
            {
                // Sàn bên dưới đã biến mất (ví dụ chòi/cầu bị phá) => Rơi tự do!
                StartFalling();
            }
            else if (NavMesh.SamplePosition(transform.position, out NavMeshHit farHit, 3.0f, filter))
            {
                if (farHit.position.y >= 3.5f && Mathf.Abs(farHit.position.y - transform.position.y) < 1.5f)
                {
                    SafeWarpOrPlace(farHit.position);
                }
            }
        }

        protected override void OnTargetExit(IDamageable target)
        {
            if (GraphAgent != null && GraphAgent.GetVariable(EnemyGraphConstants.NEARBY_ATTACKABLES,
                    out BlackboardVariable<List<GameObject>> nearbyAttackables))
            {
                nearbyAttackables.Value.Remove(target.Transform.gameObject);
                GraphAgent.SetVariableValue(EnemyGraphConstants.NEARBY_ATTACKABLES, nearbyAttackables.Value);
            }
        }

        protected override void OnTargetEnter(IDamageable target)
        {
            if (GraphAgent != null && GraphAgent.GetVariable(EnemyGraphConstants.NEARBY_ATTACKABLES,
                    out BlackboardVariable<List<GameObject>> nearbyAttackables))
            {
                nearbyAttackables.Value.Add(target.Transform.gameObject);
                GraphAgent.SetVariableValue(EnemyGraphConstants.NEARBY_ATTACKABLES, nearbyAttackables.Value);
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Attacking);
            }
        }

        public void SetDestination(Vector3 position)
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.TARGET_LOCATION, position);
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.CommandedMove);
            }
            else if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.SetDestination(position);
            }
        }

        private List<Vector3> _PatrolWaypoints;
        private int _CurrentPatrolIndex = 0;
        public bool IsPatrolling => _PatrolWaypoints != null && _PatrolWaypoints.Count > 0;

        private float _LastSeparationTime = -99f;

        private void ApplyMutualSeparation()
        {
            if (Time.time - _LastSeparationTime < 0.25f) return;
            _LastSeparationTime = Time.time;

            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh || _IsFalling) return;
            if (Agent.isOnOffMeshLink) return;

            Vector3 repelling = Vector3.zero;
            int count = 0;

            for (int i = 0; i < AllDefenders.Count; i++)
            {
                Defender other = AllDefenders[i];
                if (other == null || other == this || other.Health <= 0) continue;
                if (IsStationedAtTower != other.IsStationedAtTower) continue;

                Vector3 diff = transform.position - other.transform.position;
                diff.y = 0;
                float distSq = diff.sqrMagnitude;
                if (distSq < 4.84f && distSq > 0.0001f) // khoảng cách < 2.2m
                {
                    float dist = Mathf.Sqrt(distSq);
                    repelling += (diff / dist) * (2.2f - dist);
                    count++;
                }
            }

            if (count > 0)
            {
                // Trên cầu (Y >= 3.0m, Z quanh -14.70m): triệt tiêu lực xô ngang trục Z để không bao giờ đẩy chiến binh va vào lan can hai bên
                if (transform.position.y >= 3.0f && Mathf.Abs(transform.position.z - (-14.70f)) < 2.5f)
                {
                    repelling.z *= 0.1f;
                }

                // Tản nhẹ sang hai bên theo hướng vuông góc hoặc đẩy lùi ra không gian thoáng
                Vector3 newTarget = transform.position + repelling.normalized * Mathf.Min(repelling.magnitude * 0.9f, 2.2f);
                if (NavMesh.SamplePosition(newTarget, out NavMeshHit sHit, 2.5f, NavMesh.AllAreas))
                {
                    if (IsStationedAtTower && sHit.position.y < 3.0f) return; // Không đẩy rơi khỏi chòi
                    _DefaultPostPosition = sHit.position;
                    if (Vector3.Distance(transform.position, _DefaultPostPosition) > 0.4f)
                    {
                        Agent.isStopped = false;
                        Agent.SetDestination(_DefaultPostPosition);
                    }
                }
            }
        }

        public void Patrol(List<Vector3> waypoints)
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.PATROL_WAYPOINTS, waypoints);
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Patrol);
            }
            else if (waypoints != null && waypoints.Count > 0)
            {
                _PatrolWaypoints = new List<Vector3>(waypoints);
                _CurrentPatrolIndex = Random.Range(0, _PatrolWaypoints.Count);
                AssignDefensivePost(_PatrolWaypoints[_CurrentPatrolIndex], false);
            }
        }

        public void Idle()
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Idle);
            }
            else if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.ResetPath();
            }
        }

        public void Attack(IDamageable damageable)
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.TARGET, damageable);
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Attacking);
            }
        }

        public override void Die()
        {
            AllDefenders.Remove(this);
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Idle);
            }
            if (Animator != null)
            {
                Animator.SetTrigger(AnimationConstants.DIE_PARAMETER);
            }
            if (_HeldWeapon != null)
            {
                Destroy(_HeldWeapon);
            }
            if (TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
            }
            Invoke(nameof(DestroyGO), 2.5f);
        }

        private void DestroyGO()
        {
            RaiseDeathEvent();
            Destroy(gameObject);
        }
    }
}
