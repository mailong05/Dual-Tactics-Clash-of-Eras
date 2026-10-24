using System.Collections;
using System.Collections.Generic;
using System.IO;
using GLTFast;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Utility;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Unit
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PrehistoricDinoBase : Unit
    {
        [Header("Prehistoric Dino Stats")]
        [SerializeField] protected float MoveSpeed = 3.5f;
        [SerializeField] protected float AttackRange = 2.2f;
        [SerializeField] protected float AttackInterval = 1.2f;
        [SerializeField] public int AttackDamage = 25;
        [SerializeField] protected float BuildingDamageMultiplier = 1.0f;
        [SerializeField] protected int GoldReward = 15;
        [SerializeField] protected LayerMask TargetLayers;

        [Header("Animation & Visuals")]
        [SerializeField] protected Animator ModelAnimator;
        [SerializeField] protected Animation ModelLegacyAnimation;
        [SerializeField] protected Transform VisualModel;

        public Transform TargetBase { get; protected set; }
        protected float LastAttackTime;
        protected IDamageable CurrentTarget;
        public IDamageable TargetDamageable => CurrentTarget;
        protected Collider[] ScanBuffer = new Collider[32];

        // GLB Animation Cache & Clips
        private static readonly Dictionary<string, AnimationClip[]> s_ClipsCache = new();
        private string _WalkClipName;
        private string _AttackClipName;

        // Skeletal Bone Rig (Universal Biped & Quadruped)
        private bool _IsQuadruped = false;

        // Biped / Back Legs (T-Rex, Velociraptor, Ankylosaurus Back Legs)
        private Transform _LeftThighBone;
        private Transform _LeftShinBone;
        private Transform _LeftFootBone;
        private Quaternion _LeftThighBaseRot;
        private Quaternion _LeftShinBaseRot;
        private Quaternion _LeftFootBaseRot;

        private Transform _RightThighBone;
        private Transform _RightShinBone;
        private Transform _RightFootBone;
        private Quaternion _RightThighBaseRot;
        private Quaternion _RightShinBaseRot;
        private Quaternion _RightFootBaseRot;

        // Quadruped Front Legs (Ankylosaurus)
        private Transform _FrontLeftThighBone;
        private Transform _FrontLeftShinBone;
        private Transform _FrontLeftFootBone;
        private Quaternion _FrontLeftThighBaseRot;
        private Quaternion _FrontLeftShinBaseRot;
        private Quaternion _FrontLeftFootBaseRot;

        private Transform _FrontRightThighBone;
        private Transform _FrontRightShinBone;
        private Transform _FrontRightFootBone;
        private Quaternion _FrontRightThighBaseRot;
        private Quaternion _FrontRightShinBaseRot;
        private Quaternion _FrontRightFootBaseRot;

        // Forearms (T-Rex, Velociraptor)
        private Transform _LeftArmBone;
        private Transform _RightArmBone;
        private Quaternion _LeftArmBaseRot;
        private Quaternion _RightArmBaseRot;

        private Transform _SpineBone;
        private Quaternion _SpineBaseRot;

        private Transform _HeadBone;
        private Transform _NeckBone;
        private Transform _JawBone;
        private Quaternion _HeadBaseRot;
        private Quaternion _NeckBaseRot;
        private Quaternion _JawBaseRot;

        private readonly List<Transform> _TailBones = new();
        private readonly List<Quaternion> _TailBaseRots = new();

        // Procedural Locomotion State
        private float _GaitTimer;
        private float _CurrentSpeed;
        private float _AttackAnimProgress = -1f;
        private Vector3 _BaseModelLocalPos;
        private Quaternion _BaseModelLocalRot;
        private bool _HasBaseModelTransform;
        private bool _IsAttackingAnim;
        private bool _hasReachedBase = false;
        public bool HasReachedBase => _hasReachedBase;
        private static readonly HashSet<PrehistoricDinoBase> s_ActiveDinos = new();
        public static IReadOnlyCollection<PrehistoricDinoBase> ActiveDinos => s_ActiveDinos;

        [Header("Tactical Breach AI")]
        protected bool _IsNavigatingBreach = false;
        public bool IsNavigatingBreach => _IsNavigatingBreach;
        protected LlamAcademy.Dinos.AI.BreachPoint _CurrentBreachPoint = null;
        public LlamAcademy.Dinos.AI.BreachPoint CurrentBreachPoint => _CurrentBreachPoint;
        protected float _DefaultAgentRadius = 0.45f;
        protected float _BreachTransitTimer = 0f;
        protected float _StuckTimer = 0f;
        protected readonly HashSet<LlamAcademy.Dinos.AI.BreachPoint> _TraversedBreaches = new();

        private Vector3 _OrigBoxColliderSize = Vector3.zero;
        private float _OrigCapsuleRadius = 0f;
        private bool _HasModifiedCollider = false;

        private void SlimColliderForBreach()
        {
            if (_HasModifiedCollider) return;

            if (TryGetComponent(out CapsuleCollider cc))
            {
                _OrigCapsuleRadius = cc.radius;
                cc.radius = Mathf.Min(cc.radius, 0.38f);
                _HasModifiedCollider = true;
            }
            else if (TryGetComponent(out BoxCollider bc))
            {
                _OrigBoxColliderSize = bc.size;
                bc.size = new Vector3(Mathf.Min(bc.size.x, 0.75f), bc.size.y, Mathf.Min(bc.size.z, 1.0f));
                _HasModifiedCollider = true;
            }

            // Tạm thời vô hiệu hóa va chạm vật lý với tường lân cận để không bị kẹt góc
            Collider myCol = GetComponent<Collider>();
            if (myCol != null)
            {
                Collider[] wallsNear = Physics.OverlapSphere(transform.position, 12f, TargetLayers);
                for (int i = 0; i < wallsNear.Length; i++)
                {
                    var wCol = wallsNear[i];
                    if (wCol != null && wCol != myCol && (wCol.GetComponentInParent<Wall>() != null || wCol.CompareTag("Wall") || wCol.CompareTag("Obstacle")))
                    {
                        Physics.IgnoreCollision(myCol, wCol, true);
                    }
                }
            }
        }

        private void RestoreOriginalColliderAndAgent()
        {
            if (Agent != null && Agent.enabled)
            {
                Agent.radius = _DefaultAgentRadius;
                Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                Agent.avoidancePriority = 50;
            }

            if (_HasModifiedCollider)
            {
                if (TryGetComponent(out CapsuleCollider cc) && _OrigCapsuleRadius > 0f)
                {
                    cc.radius = _OrigCapsuleRadius;
                }
                else if (TryGetComponent(out BoxCollider bc) && _OrigBoxColliderSize != Vector3.zero)
                {
                    bc.size = _OrigBoxColliderSize;
                }
                _HasModifiedCollider = false;
            }

            Collider myCol = GetComponent<Collider>();
            if (myCol != null)
            {
                Collider[] wallsNear = Physics.OverlapSphere(transform.position, 12f, TargetLayers);
                for (int i = 0; i < wallsNear.Length; i++)
                {
                    var wCol = wallsNear[i];
                    if (wCol != null && wCol != myCol && (wCol.GetComponentInParent<Wall>() != null || wCol.CompareTag("Wall") || wCol.CompareTag("Obstacle")))
                    {
                        Physics.IgnoreCollision(myCol, wCol, false);
                    }
                }
            }
        }

        public bool EnsureOnNavMesh(float maxDistance = 25f)
        {
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            if (Agent == null) return false;

            if (Agent.enabled && Agent.isOnNavMesh) return true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                if (Agent.isOnNavMesh)
                {
                    Agent.Warp(hit.position);
                }
                else
                {
                    Agent.enabled = false;
                    transform.position = hit.position;
                    Physics.SyncTransforms();
                    Agent.enabled = true;
                }
                return Agent.isOnNavMesh;
            }

            if (NavMesh.SamplePosition(transform.position + Vector3.up * 1.5f, out hit, maxDistance * 1.5f, NavMesh.AllAreas))
            {
                if (Agent.isOnNavMesh)
                {
                    Agent.Warp(hit.position);
                }
                else
                {
                    Agent.enabled = false;
                    transform.position = hit.position;
                    Physics.SyncTransforms();
                    Agent.enabled = true;
                }
                return Agent.isOnNavMesh;
            }

            return false;
        }

        public void ConfigureNavMeshAgent()
        {
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            if (Agent == null) return;

            Agent.speed = MoveSpeed;
            Agent.acceleration = 28f;
            Agent.angularSpeed = 480f;
            Agent.autoBraking = false;
            Agent.stoppingDistance = 0.5f;
            Agent.autoRepath = true;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            if (Agent.radius > 0.45f) Agent.radius = 0.45f;
            _DefaultAgentRadius = Agent.radius;

            // Giới hạn BoxCollider của khủng long không quá to để không bị kẹt góc tường hoặc chèn ép nhau
            if (TryGetComponent(out BoxCollider bc))
            {
                Vector3 sz = bc.size;
                if (sz.x > 1.3f || sz.z > 2.6f)
                {
                    bc.size = new Vector3(Mathf.Min(sz.x, 1.3f), sz.y, Mathf.Min(sz.z, 2.6f));
                }
            }
        }

        protected virtual void OnEnable()
        {
            s_ActiveDinos.Add(this);
            ConfigureNavMeshAgent();
            EnsureOnNavMesh(15f);
            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated += HandleNavMeshUpdated;
            }
        }

        protected virtual void OnDisable()
        {
            s_ActiveDinos.Remove(this);
            if (_CurrentBreachPoint != null && TacticalBreachMemory.HasInstance)
            {
                _CurrentBreachPoint.ReleaseSlot(this);
                TacticalBreachMemory.Instance.DispatchNextDinosToBreach(_CurrentBreachPoint);
                RestoreOriginalColliderAndAgent();
                _CurrentBreachPoint = null;
                _IsNavigatingBreach = false;
            }

            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated -= HandleNavMeshUpdated;
            }
        }

        private void HandleNavMeshUpdated()
        {
            // When NavMesh updates (e.g. wall destroyed, breach opened), immediately repath towards base!
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh && TargetBase != null)
            {
                // If targeting a wall but a path through a hole is now open, abandon the wall!
                if (CurrentTarget is Wall)
                {
                    CurrentTarget = null;
                }
                Agent.isStopped = false;
                Agent.ResetPath();
                Agent.SetDestination(TargetBase.position);
            }
        }

        public static void OnWallBreached(Wall breachedWall)
        {
            if (breachedWall == null) return;
            Vector3 wallPos = breachedWall.transform.position;

            BreachPoint breach = null;
            if (TacticalBreachMemory.HasInstance)
            {
                breach = TacticalBreachMemory.Instance.GetBestBreachTowards(wallPos, wallPos, 4f);
            }

            Vector3 basePos = Vector3.zero;
            bool hasBase = false;
            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.BaseTarget != null)
            {
                basePos = PrehistoricGameplayManager.Instance.BaseTarget.position;
                hasBase = true;
            }
            else if (PrehistoricVillageBase.Instance != null)
            {
                basePos = PrehistoricVillageBase.Instance.transform.position;
                hasBase = true;
            }

            float wallDistToBase = hasBase ? Vector3.Distance(wallPos, basePos) : 9999f;

            // Thu thập các khủng long còn sống ở khu vực xung quanh
            List<PrehistoricDinoBase> candidateDinos = new();
            foreach (var dino in s_ActiveDinos)
            {
                if (dino == null || !dino.enabled || dino.Health <= 0) continue;

                // Nếu đang tấn công đúng bức tường vừa vỡ, giải phóng mục tiêu
                if (dino.CurrentTarget == breachedWall)
                {
                    dino.CurrentTarget = null;
                }

                // Nếu khủng long đã ở gần căn cứ hơn bức tường này (đã vượt qua tường vào sâu hơn trong làng),
                // TUYỆT ĐỐI KHÔNG kéo ngược lại hoặc chuyển hướng đánh tường khác!
                if (hasBase)
                {
                    float dinoDistToBase = Vector3.Distance(dino.transform.position, basePos);
                    if (dinoDistToBase < wallDistToBase - 0.5f)
                    {
                        if (dino.Agent != null && dino.Agent.isOnNavMesh)
                        {
                            dino.Agent.isStopped = false;
                            dino.Agent.SetDestination(basePos);
                        }
                        continue;
                    }
                }

                if (Vector3.Distance(dino.transform.position, wallPos) < 28f)
                {
                    // Nếu khủng long đã ở bên trong làng rồi, không kéo ngược lại
                    if (breach != null && breach.HasPassedPlane(dino.transform.position)) continue;
                    candidateDinos.Add(dino);
                }
            }

            // Sắp xếp các con khủng long theo khoảng cách tăng dần tới điểm đột phá
            candidateDinos.Sort((a, b) =>
                Vector3.Distance(a.transform.position, wallPos).CompareTo(Vector3.Distance(b.transform.position, wallPos)));

            // CHỈ CHO PHÉP TỐI ĐA 4 CON GẦN NHẤT ĐI QUA LỐI ĐÓ (giữ đúng logic 4 con)
            int assignedCount = 0;
            for (int i = 0; i < candidateDinos.Count; i++)
            {
                var dino = candidateDinos[i];

                if (breach != null && breach.HasSlotAvailable && assignedCount < BreachPoint.MAX_CONCURRENT_UNITS)
                {
                    if (breach.TryClaimSlot(dino))
                    {
                        dino.AssignToBreach(breach);
                        assignedCount++;
                        continue;
                    }
                }

                // CÁC CON KHÁC: TỎA RA PHÁ HỦY TƯỜNG TRƯỚC MẶT CHỖ KHÁC (chỉ tường nằm giữa khủng long và căn cứ)!
                dino.RedirectToOtherWalls(breach);
            }
        }

        public void AssignToBreach(BreachPoint breach)
        {
            if (breach == null || Health <= 0 || !enabled) return;
            _CurrentBreachPoint = breach;
            _IsNavigatingBreach = true;
            _BreachTransitTimer = 0f;
            CurrentTarget = null;

            SlimColliderForBreach();

            Vector3 assignedIngress = breach.GetIngressPointForUnit(this);

            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.radius = 0.20f;
                Agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                Agent.avoidancePriority = 25 + breach.GetUnitSlotIndex(this) * 5;
                Agent.isStopped = false;
                Agent.ResetPath();
                Agent.SetDestination(assignedIngress);
            }
        }

        public void RedirectToOtherWalls(BreachPoint breach)
        {
            if (Health <= 0 || !enabled) return;

            if (_IsNavigatingBreach && _CurrentBreachPoint == breach)
            {
                if (breach != null) breach.ReleaseSlot(this);
                RestoreOriginalColliderAndAgent();
                _IsNavigatingBreach = false;
                _CurrentBreachPoint = null;
            }

            if (TargetBase == null) FindTargetBase();
            Vector3 basePos = TargetBase != null ? TargetBase.position : Vector3.zero;

            // NẾU KHỦNG LONG ĐÃ Ở GẦN CĂN CỨ HƠN KHE HỞ NÀY:
            // Tuyệt đối không bao giờ quay đầu lại đánh tường phía sau! Tiếp tục tiến thẳng về căn cứ!
            if (TargetBase != null && breach != null)
            {
                float myDist = Vector3.Distance(transform.position, basePos);
                float breachDist = Vector3.Distance(breach.Position, basePos);
                if (myDist <= breachDist - 1.0f)
                {
                    CurrentTarget = null;
                    if (Agent != null && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
                        Agent.SetDestination(basePos);
                    }
                    return;
                }
            }

            if (CurrentTarget is Wall targetWall)
            {
                if (breach != null && Vector3.Distance(targetWall.transform.position, breach.Position) < 7.5f)
                {
                    CurrentTarget = null;
                }
            }

            // Chỉ tìm một bức tường nằm PHÍA TRƯỚC MẶT hướng về căn cứ (và gần căn cứ hơn khủng long)
            CurrentTarget = ScanForAlternativeWalls(breach);

            if (CurrentTarget == null && Agent != null && Agent.enabled && Agent.isOnNavMesh && TargetBase != null)
            {
                Agent.isStopped = false;
                Agent.ResetPath();
                Agent.SetDestination(basePos);
            }
        }

        protected virtual IDamageable ScanForAlternativeWalls(BreachPoint breach)
        {
            if (TargetBase == null) FindTargetBase();
            if (TargetBase == null) return null;

            Vector3 myPos = transform.position;
            Vector3 basePos = TargetBase.position;
            float myDistToBase = Vector3.Distance(myPos, basePos);
            Vector3 toBaseDir = (basePos - myPos);
            toBaseDir.y = 0;
            if (toBaseDir.sqrMagnitude < 0.001f) return null;
            toBaseDir.Normalize();

            float scanRadius = 16f;
            int blockHits = Physics.OverlapSphereNonAlloc(myPos, scanRadius, ScanBuffer, TargetLayers);
            IDamageable bestWall = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < blockHits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                Wall wall = col.GetComponentInParent<Wall>();
                if (wall == null) wall = EnsureDynamicWall(col);
                if (wall != null && wall.Health > 0)
                {
                    // Chỉ bỏ qua bức tường đã mở (trong bán kính 2.2m), cho phép tấn công các tường liền kề
                    if (TacticalBreachMemory.HasInstance && TacticalBreachMemory.Instance.IsWallNearActiveBreach(wall.transform.position, 2.2f))
                    {
                        continue;
                    }

                    Vector3 closest;
                    try { closest = col.ClosestPoint(myPos); }
                    catch { closest = col.bounds.ClosestPoint(myPos); }

                    // QUY TẮC CỐT LÕI: Tường thay thế không được nằm ở sau lưng
                    float wallDistToBase = Vector3.Distance(closest, basePos);
                    Vector3 toWall = closest - myPos;
                    toWall.y = 0;
                    float d = toWall.magnitude;
                    float dot = Vector3.Dot(toBaseDir, toWall.normalized);

                    if (dot < 0.2f || wallDistToBase > myDistToBase + 1.0f)
                    {
                        continue; // Tường nằm ở sau lưng -> BỎ QUA!
                    }

                    Vector3 lateralDir = Vector3.Cross(Vector3.up, toBaseDir).normalized;
                    float lateralOffset = Mathf.Abs(Vector3.Dot(lateralDir, toWall));
                    if (lateralOffset > 15.0f) continue;

                    if (d < minDistance)
                    {
                        minDistance = d;
                        bestWall = wall;
                    }
                }
            }
            return bestWall;
        }

        public void NotifyNearbyBreach(Vector3 breachPos)
        {
            if (Health <= 0 || !enabled) return;
            CurrentTarget = null;

            if (TacticalBreachMemory.HasInstance)
            {
                var breach = TacticalBreachMemory.Instance.GetBestBreachTowards(transform.position, TargetBase != null ? TargetBase.position : breachPos, this, 28f);
                if (breach != null && breach.TryClaimSlot(this))
                {
                    AssignToBreach(breach);
                    return;
                }
            }

            // Không có slot (đã đủ 4 con), tỏa ra phá tường chỗ khác
            RedirectToOtherWalls(null);
        }

        protected override void Awake()
        {
            base.Awake();
            ConfigureNavMeshAgent();

            InitializeVisualAndAnimation();
            DiscoverSkeletalBones();
            TryLoadGlbAnimations();
        }

        protected virtual void InitializeVisualAndAnimation()
        {
            if (VisualModel == null)
            {
                Transform modelChild = transform.Find("Model");
                VisualModel = modelChild != null ? modelChild : (transform.childCount > 0 ? transform.GetChild(0) : transform);
            }

            if (VisualModel != null)
            {
                _BaseModelLocalPos = VisualModel.localPosition;
                _BaseModelLocalRot = VisualModel.localRotation;
                _HasBaseModelTransform = true;
            }

            if (ModelAnimator == null)
            {
                ModelAnimator = GetComponentInChildren<Animator>(true);
            }

            if (ModelLegacyAnimation == null)
            {
                ModelLegacyAnimation = GetComponentInChildren<Animation>(true);
            }

            if (ModelLegacyAnimation != null)
            {
                ModelLegacyAnimation.playAutomatically = true;
                foreach (AnimationState state in ModelLegacyAnimation)
                {
                    state.wrapMode = WrapMode.Loop;
                }

                if (ModelLegacyAnimation.clip != null)
                {
                    ModelLegacyAnimation.Play();
                }
                else if (ModelLegacyAnimation.GetClipCount() > 0)
                {
                    foreach (AnimationState st in ModelLegacyAnimation)
                    {
                        ModelLegacyAnimation.clip = st.clip;
                        ModelLegacyAnimation.Play(st.name);
                        break;
                    }
                }
            }

            if (ModelAnimator != null)
            {
                ModelAnimator.enabled = true;
                ModelAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void DiscoverSkeletalBones()
        {
            if (VisualModel == null) return;

            Transform[] allBones = VisualModel.GetComponentsInChildren<Transform>(true);
            
            // Check if model has 4 legs (Quadruped)
            foreach (Transform t in allBones)
            {
                if (t == VisualModel) continue;
                string n = t.name.ToLower();
                if (n.Contains("frontupleg") || n.Contains("frontleg") || n.Contains("frontlowleg") || n.Contains("frontfoot"))
                {
                    _IsQuadruped = true;
                    break;
                }
            }

            if (this is SiegeDino || gameObject.name.ToLower().Contains("ankyl"))
            {
                _IsQuadruped = true;
            }

            foreach (Transform t in allBones)
            {
                if (t == VisualModel) continue;
                string n = t.name.ToLower();

                // Quadruped Front Left
                if (_FrontLeftThighBone == null && (n.Contains("frontupleg.l") || n.Contains("front_upleg.l")))
                {
                    _FrontLeftThighBone = t;
                    _FrontLeftThighBaseRot = t.localRotation;
                }
                else if (_FrontLeftShinBone == null && (n.Contains("frontlowleg.l") || n.Contains("front_lowleg.l")))
                {
                    _FrontLeftShinBone = t;
                    _FrontLeftShinBaseRot = t.localRotation;
                }
                else if (_FrontLeftFootBone == null && (n.Contains("frontfoot.l") || n.Contains("frontleg.l")))
                {
                    _FrontLeftFootBone = t;
                    _FrontLeftFootBaseRot = t.localRotation;
                }

                // Quadruped Front Right
                else if (_FrontRightThighBone == null && (n.Contains("frontupleg.r") || n.Contains("front_upleg.r")))
                {
                    _FrontRightThighBone = t;
                    _FrontRightThighBaseRot = t.localRotation;
                }
                else if (_FrontRightShinBone == null && (n.Contains("frontlowleg.r") || n.Contains("front_lowleg.r")))
                {
                    _FrontRightShinBone = t;
                    _FrontRightShinBaseRot = t.localRotation;
                }
                else if (_FrontRightFootBone == null && (n.Contains("frontfoot.r") || n.Contains("frontleg.r")))
                {
                    _FrontRightFootBone = t;
                    _FrontRightFootBaseRot = t.localRotation;
                }

                // Back Left / Main Left Leg Thigh
                else if (_LeftThighBone == null && (n.Contains("bn_leftupleg") || n.Contains("leg.001.l") || n.Contains("backupleg.l") || n.Contains("thigh.l") || n.Contains("upleg.l")))
                {
                    _LeftThighBone = t;
                    _LeftThighBaseRot = t.localRotation;
                }
                // Back Left Shin / Knee
                else if (_LeftShinBone == null && (n.Contains("bn_leftleg") || n.Contains("leg.002.l") || n.Contains("backlowleg.l") || n.Contains("shin.l") || n.Contains("knee.l")))
                {
                    _LeftShinBone = t;
                    _LeftShinBaseRot = t.localRotation;
                }
                // Back Left Foot / Ankle
                else if (_LeftFootBone == null && (n.Contains("bn_leftfoot") || n.Contains("leg.003.l") || n.Contains("backfoot.l") || (n.Contains("backleg.l") && !n.Contains("backupleg")) || n.Contains("foot.l")))
                {
                    _LeftFootBone = t;
                    _LeftFootBaseRot = t.localRotation;
                }

                // Back Right / Main Right Leg Thigh
                else if (_RightThighBone == null && (n.Contains("bn_rightupleg") || n.Contains("leg.001.r") || n.Contains("backupleg.r") || n.Contains("thigh.r") || n.Contains("upleg.r")))
                {
                    _RightThighBone = t;
                    _RightThighBaseRot = t.localRotation;
                }
                // Back Right Shin / Knee
                else if (_RightShinBone == null && (n.Contains("bn_rightleg") || n.Contains("leg.002.r") || n.Contains("backlowleg.r") || n.Contains("shin.r") || n.Contains("knee.r")))
                {
                    _RightShinBone = t;
                    _RightShinBaseRot = t.localRotation;
                }
                // Back Right Foot / Ankle
                else if (_RightFootBone == null && (n.Contains("bn_rightfoot") || n.Contains("leg.003.r") || n.Contains("backfoot.r") || (n.Contains("backleg.r") && !n.Contains("backupleg")) || n.Contains("foot.r")))
                {
                    _RightFootBone = t;
                    _RightFootBaseRot = t.localRotation;
                }

                // Forearms (T-Rex / Raptor)
                else if (_LeftArmBone == null && (n.Contains("bn_leftarm") || n.Contains("arm.001.l") || n.Contains("arm.l")))
                {
                    _LeftArmBone = t;
                    _LeftArmBaseRot = t.localRotation;
                }
                else if (_RightArmBone == null && (n.Contains("bn_rightarm") || n.Contains("arm.001.r") || n.Contains("arm.r")))
                {
                    _RightArmBone = t;
                    _RightArmBaseRot = t.localRotation;
                }

                // Spine / Hips
                else if (_SpineBone == null && (n.Contains("bn_spine.4") || n.Contains("spine1") || n.Contains("hips_21") || n.Contains("hips")))
                {
                    _SpineBone = t;
                    _SpineBaseRot = t.localRotation;
                }

                // Head
                else if (_HeadBone == null && (n.Contains("head") || n.Contains("skull")))
                {
                    _HeadBone = t;
                    _HeadBaseRot = t.localRotation;
                }
                // Neck
                else if (_NeckBone == null && n.Contains("neck"))
                {
                    _NeckBone = t;
                    _NeckBaseRot = t.localRotation;
                }
                // Jaw / Mouth
                else if (_JawBone == null && (n.Contains("jaw") || n.Contains("mouth")))
                {
                    _JawBone = t;
                    _JawBaseRot = t.localRotation;
                }
                // Tail segments (store up to 10 tail bones)
                else if (n.Contains("tail") && _TailBones.Count < 10)
                {
                    _TailBones.Add(t);
                    _TailBaseRots.Add(t.localRotation);
                }
            }
        }

        protected virtual string GetModelGlbFileName()
        {
            string n = gameObject.name.ToLower();
            if (n.Contains("trex") || n.Contains("t-rex") || this is BossDino)
                return "animated_t-rex_dinosaur_biting_attack_loop.glb";
            if (n.Contains("ankyl") || this is SiegeDino)
                return "ankylosaurus_updated.glb";
            if (n.Contains("ptero") || this is FlyingUnit)
                return "pterodactyl_1.glb";
            return "velociraptor.glb";
        }

        private static bool AreClipsValid(AnimationClip[] clips)
        {
            if (clips == null || clips.Length == 0) return false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) return false;
            }
            return true;
        }

        public static void ClearClipsCache()
        {
            s_ClipsCache.Clear();
        }

        private async void TryLoadGlbAnimations()
        {
            string glbName = GetModelGlbFileName();
            if (s_ClipsCache.TryGetValue(glbName, out var cachedClips))
            {
                if (AreClipsValid(cachedClips))
                {
                    ApplyAnimationClips(cachedClips);
                    return;
                }
                else
                {
                    s_ClipsCache.Remove(glbName);
                }
            }

            string filePath = Path.Combine(Application.dataPath, glbName);
            if (!File.Exists(filePath)) return;

            try
            {
                var gltf = new GltfImport();
                var importSettings = new ImportSettings
                {
                    AnimationMethod = AnimationMethod.Legacy
                };
                bool success = await gltf.LoadFile(filePath, importSettings: importSettings);
                if (success)
                {
                    var rawClips = gltf.GetAnimationClips();
                    if (rawClips != null && rawClips.Length > 0)
                    {
                        List<AnimationClip> persistentClips = new();
                        foreach (var c in rawClips)
                        {
                            if (c != null)
                            {
                                try
                                {
                                    AnimationClip clone = Instantiate(c);
                                    clone.name = c.name;
                                    clone.hideFlags = HideFlags.DontSave;
                                    persistentClips.Add(clone);
                                }
                                catch {}
                            }
                        }

                        if (persistentClips.Count > 0)
                        {
                            AnimationClip[] persistentArray = persistentClips.ToArray();
                            s_ClipsCache[glbName] = persistentArray;
                            ApplyAnimationClips(persistentArray);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DinoAnim] Error loading {glbName}: {ex.Message}");
            }
        }

        private void ApplyAnimationClips(AnimationClip[] clips)
        {
            if (clips == null || clips.Length == 0 || VisualModel == null) return;

            List<AnimationClip> validClips = new();
            foreach (var clip in clips)
            {
                if (clip != null) validClips.Add(clip);
            }
            if (validClips.Count == 0) return;

            // When real GLB legacy clips are present, disable ModelAnimator to avoid Mecanim vs Legacy collision!
            if (ModelAnimator != null)
            {
                ModelAnimator.enabled = false;
            }

            if (ModelLegacyAnimation == null)
            {
                ModelLegacyAnimation = VisualModel.GetComponent<Animation>();
                if (ModelLegacyAnimation == null)
                {
                    ModelLegacyAnimation = VisualModel.gameObject.AddComponent<Animation>();
                }
            }

            if (ModelLegacyAnimation != null)
            {
                ModelLegacyAnimation.playAutomatically = true;
                foreach (var clip in validClips)
                {
                    if (clip == null) continue;
                    try
                    {
                        clip.legacy = true;
                        clip.wrapMode = WrapMode.Loop;
                        string cName = clip.name;
                        if (ModelLegacyAnimation[cName] == null)
                        {
                            ModelLegacyAnimation.AddClip(clip, cName);
                        }
                    }
                    catch {}
                }

                foreach (var clip in validClips)
                {
                    if (clip == null) continue;
                    try
                    {
                        string cn = clip.name.ToLower();
                        if (_WalkClipName == null && (cn.Contains("walk") || cn.Contains("run") || cn.Contains("fly") || cn.Contains("anim")))
                            _WalkClipName = clip.name;
                        if (_AttackClipName == null && (cn.Contains("attack") || cn.Contains("bite") || cn.Contains("roar") || cn.Contains("eat")))
                            _AttackClipName = clip.name;
                    }
                    catch {}
                }

                // If only 1 clip exists (e.g. TRex or Ankylosaurus with single "Animation" clip), use it for both walk and attack!
                if (_WalkClipName == null && validClips.Count > 0)
                {
                    _WalkClipName = validClips[0].name;
                }
                if (_AttackClipName == null && !string.IsNullOrEmpty(_WalkClipName))
                {
                    _AttackClipName = _WalkClipName;
                }

                if (!string.IsNullOrEmpty(_WalkClipName) && ModelLegacyAnimation[_WalkClipName] != null)
                {
                    ModelLegacyAnimation.clip = ModelLegacyAnimation[_WalkClipName].clip;
                    ModelLegacyAnimation.Play(_WalkClipName);
                }
            }
        }

        protected override void Start()
        {
            base.Start();

            if (TargetLayers.value == 0) TargetLayers = ~LayerMask.GetMask("Ignore Raycast");

            ConfigureNavMeshAgent();
            EnsureOnNavMesh(15f);

            FindTargetBase();

            if (TargetBase != null && Agent != null && Agent.isOnNavMesh)
            {
                Agent.isStopped = false;
                Agent.SetDestination(TargetBase.position);
            }

            EnsureHealthBarAttached();
        }

        public override void EnsureHealthBarAttached()
        {
            base.EnsureHealthBarAttached();
        }

        public void SetDestination(Vector3 destination)
        {
            if (Agent != null && Agent.isOnNavMesh)
            {
                Agent.isStopped = false;
                Agent.SetDestination(destination);
            }
        }

        protected virtual void FindTargetBase()
        {
            if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
            {
                TargetBase = RoundManager.Instance.DinoTarget;
                return;
            }

            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.BaseTarget != null)
            {
                TargetBase = PrehistoricGameplayManager.Instance.BaseTarget;
                return;
            }

            if (PrehistoricVillageBase.Instance != null)
            {
                TargetBase = PrehistoricVillageBase.Instance.transform;
                return;
            }

            if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.VillageBase != null)
            {
                TargetBase = PrehistoricGameModeManager.Instance.VillageBase.transform;
                return;
            }

            GameObject eggObj = GameObject.Find("Dino Egg Spawn");
            if (eggObj != null)
            {
                TargetBase = eggObj.transform;
                return;
            }

            GameObject eggTag = GameObject.FindWithTag("Finish");
            if (eggTag != null)
            {
                TargetBase = eggTag.transform;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (Agent != null && (!Agent.enabled || !Agent.isOnNavMesh))
            {
                EnsureOnNavMesh(25.0f);
            }

            float currentSpeed = (Agent != null && Agent.enabled && Agent.isOnNavMesh) ? Agent.velocity.magnitude : 0f;
            UpdateAnimation(currentSpeed);

            bool isDefenseCombat = PrehistoricGameplayManager.Instance != null 
                && PrehistoricGameplayManager.Instance.enabled 
                && PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.CombatPhase;

            bool isAssaultCombat = PrehistoricGameModeManager.Instance != null 
                && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault 
                && PrehistoricGameModeManager.Instance.IsAssaultActive;

            if (!isDefenseCombat && !isAssaultCombat)
            {
                if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running)
                {
                    if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;
                    return;
                }
            }

            if (TargetBase == null)
            {
                FindTargetBase();
                if (TargetBase == null) return;
            }

            // Check if current target is valid
            bool hasValidTarget = (CurrentTarget != null && CurrentTarget.Health > 0);

            // 1. TACTICAL BREACH EXPLOITATION CHECK:
            // Nếu đang nhắm một bức tường nhưng có khe hở gần đó (< 26m) hướng về căn cứ và CÒN CHỖ TRỐNG (< 4 con):
            // Lập tức chuyển sang đi qua khe hở thay vì đập thêm tường!
            if (!_IsNavigatingBreach && TacticalBreachMemory.HasInstance && TargetBase != null)
            {
                if (CurrentTarget is Wall targetWall)
                {
                    float distToTarget = Vector3.Distance(transform.position, targetWall.transform.position);
                    if (distToTarget > AttackRange + 1.5f)
                    {
                        var bestBreach = TacticalBreachMemory.Instance.GetBestBreachTowards(transform.position, TargetBase.position, this, 26.0f);
                        if (bestBreach != null && !_TraversedBreaches.Contains(bestBreach) && bestBreach.TryClaimSlot(this))
                        {
                            AssignToBreach(bestBreach);
                            hasValidTarget = false;
                        }
                    }
                }
            }

            // 2. ACTIVE BREACH NAVIGATION STATE:
            if (_IsNavigatingBreach && _CurrentBreachPoint != null)
            {
                _BreachTransitTimer += Time.deltaTime;
                Vector3 targetIngress = _CurrentBreachPoint.GetIngressPointForUnit(this);
                float distToIngress = Vector3.Distance(transform.position, targetIngress);
                bool hasPassedPlane = _CurrentBreachPoint.HasPassedPlane(transform.position);

                // Khủng long đã thực sự vượt qua mặt phẳng tường vào sâu bên trong làng (hoặc an toàn quá 6s):
                if (hasPassedPlane || distToIngress < 1.2f || _BreachTransitTimer > 6.0f)
                {
                    RestoreOriginalColliderAndAgent();

                    // Đánh dấu đã qua khe hở này để vĩnh viễn không bao giờ quay lại
                    _TraversedBreaches.Add(_CurrentBreachPoint);

                    if (Agent != null && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
                        Agent.ResetPath();
                        if (TargetBase != null) Agent.SetDestination(TargetBase.position);
                    }
                    if (TacticalBreachMemory.HasInstance)
                    {
                        TacticalBreachMemory.Instance.OnUnitTraversedBreach(_CurrentBreachPoint, this);
                    }
                    _IsNavigatingBreach = false;
                    _CurrentBreachPoint = null;
                }
                else
                {
                    // Đang tiến qua làn đột phá:
                    if (Agent != null && Agent.isOnNavMesh)
                    {
                        Agent.radius = 0.20f;
                        Agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                        Agent.isStopped = false;
                        if (!Agent.hasPath || Vector3.Distance(Agent.destination, targetIngress) > 0.6f)
                        {
                            Agent.SetDestination(targetIngress);
                        }
                    }

                    // Hỗ trợ lực đẩy tiến xuyên qua khe nếu vướng carving cục bộ
                    Vector3 breachDir = (targetIngress - transform.position);
                    breachDir.y = 0;
                    if (breachDir.sqrMagnitude > 0.04f)
                    {
                        breachDir.Normalize();
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(breachDir), Time.deltaTime * 8f);

                        // Sử dụng Agent.Move để đồng bộ tuyệt đối với NavMeshAgent, không gây giật lag hoặc kẹt
                        if (Agent != null && Agent.isOnNavMesh)
                        {
                            if (Agent.velocity.sqrMagnitude < 0.35f)
                            {
                                Agent.Move(breachDir * (MoveSpeed * 0.85f * Time.deltaTime));
                            }
                        }
                        else
                        {
                            transform.position += breachDir * (MoveSpeed * 0.85f * Time.deltaTime);
                        }
                    }

                    // Tuyệt đối không tấn công các hàng rào bên cạnh!
                    CurrentTarget = null;
                    hasValidTarget = false;
                    return;
                }
            }

            if (hasValidTarget)
            {
                Collider targetCol = CurrentTarget.Transform.GetComponentInChildren<Collider>();
                Vector3 targetCenter = targetCol != null ? targetCol.bounds.center : CurrentTarget.Transform.position;
                Vector3 closestPoint = targetCol != null ? targetCol.ClosestPoint(transform.position) : targetCenter;

                // KIỂM TRA MỤC TIÊU CÓ THỰC SỰ Ở PHÍA SAU LƯNG (ĐÃ ĐI QUA RỒI) HAY KHÔNG:
                if (TargetBase != null && !(CurrentTarget is PrehistoricVillageBase))
                {
                    float myDist = Vector3.Distance(transform.position, TargetBase.position);
                    float targetDist = Vector3.Distance(closestPoint, TargetBase.position);
                    Vector3 toTarget = closestPoint - transform.position;
                    toTarget.y = 0;
                    Vector3 toBase = TargetBase.position - transform.position;
                    toBase.y = 0;
                    float dot = (toBase.sqrMagnitude > 0.01f && toTarget.sqrMagnitude > 0.01f)
                        ? Vector3.Dot(toBase.normalized, toTarget.normalized)
                        : 1.0f;

                    // Chỉ hủy bỏ mục tiêu khi nó THỰC SỰ ở phía sau lưng (dot < -0.2f) VÀ ở xa căn cứ hơn ít nhất 1.2m
                    if (dot < -0.2f && targetDist > myDist + 1.2f)
                    {
                        CurrentTarget = null;
                        hasValidTarget = false;
                    }
                }
            }

            // 0. ANTI-STUCK WATCHDOG: Nếu quái đang di chuyển nhưng bị khựng/kẹt (velocity < 0.15f) quá 0.75s
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh && Agent.velocity.sqrMagnitude < 0.15f)
            {
                _StuckTimer += Time.deltaTime;
                if (_StuckTimer >= 0.75f)
                {
                    IDamageable emergencyBlocker = FindEmergencyWallBlocker(5.8f);
                    if (emergencyBlocker != null)
                    {
                        CurrentTarget = emergencyBlocker;
                        hasValidTarget = true;
                    }
                    else
                    {
                        // Kiểm tra nếu ngay trước mặt có vật thể cản trở vật lý
                        if (Physics.SphereCast(transform.position + Vector3.up * 0.8f, 0.6f, transform.forward, out RaycastHit rHit, 3.2f, TargetLayers))
                        {
                            Wall dynamicWall = EnsureDynamicWall(rHit.collider);
                            if (dynamicWall != null && dynamicWall.Health > 0)
                            {
                                CurrentTarget = dynamicWall;
                                hasValidTarget = true;
                            }
                            else
                            {
                                // Vật thể tĩnh không thể phá hủy: Nudge nhẹ Agent sang ngang để trượt lướt qua góc
                                Vector3 sideNudge = Vector3.Cross(Vector3.up, transform.forward).normalized * 0.6f;
                                Agent.Move(sideNudge * Time.deltaTime * MoveSpeed);
                            }
                        }

                        Agent.isStopped = false;
                        Agent.ResetPath();
                        if (TargetBase != null) Agent.SetDestination(TargetBase.position);
                    }
                    _StuckTimer = 0f;
                }
            }
            else
            {
                _StuckTimer = 0f;
            }

            if (hasValidTarget)
            {
                Collider targetCol = CurrentTarget.Transform.GetComponentInChildren<Collider>();
                Vector3 targetCenter = targetCol != null ? targetCol.bounds.center : CurrentTarget.Transform.position;
                Vector3 closestPoint = targetCol != null ? targetCol.ClosestPoint(transform.position) : targetCenter;

                // TÍNH KHOẢNG CÁCH 2D MẶT PHẲNG ĐỂ LOẠI BỎ SAI SỐ ĐỘ CAO Y / DỐC TERRAIN
                Vector3 flatDiff = transform.position - closestPoint;
                flatDiff.y = 0;
                float distToTarget = flatDiff.magnitude;

                float agentRadius = (Agent != null && Agent.enabled) ? Agent.radius : 0.8f;

                // Kiểm tra mục tiêu là công trình (Tường, Cổng thành, Rào cọc gỗ, Căn cứ Làng, hoặc Tháp canh):
                bool isStructure = (CurrentTarget is Wall) || 
                                   (CurrentTarget is PrehistoricVillageBase) || 
                                   (CurrentTarget is Unit uTarget && !(uTarget is PrehistoricDinoBase));

                // 1. Cự ly tấn công trực diện cho con đi đầu
                float directAttackRange = isStructure 
                    ? Mathf.Max(AttackRange + agentRadius + 3.0f, 5.0f) 
                    : (AttackRange + agentRadius + 0.8f);

                // 2. CỰ LY BẦY ĐÀN (SWARM PACK ATTACK):
                // Khi 1 bầy khủng long (4-8 con) cùng bu lại cắn công trình, các con đứng hàng 2, hàng 3 theo đội hình cánh cung
                // Cho phép toàn bộ bầy trong cự ly 8.0m đều được tính vào trạng thái cắn phá và xả sát thương đồng thời!
                bool isBlockedOrSwarming = isStructure && (distToTarget <= 8.0f) &&
                    (Agent != null && Agent.isOnNavMesh);

                if (distToTarget <= directAttackRange || isBlockedOrSwarming)
                {
                    if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;

                    // Face target
                    Vector3 look = (targetCenter - transform.position);
                    look.y = 0;
                    if (look.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 6f);
                    }

                    if (Time.time >= LastAttackTime + AttackInterval)
                    {
                        LastAttackTime = Time.time;
                        PerformAttack(CurrentTarget);
                    }
                    return;
                }
                else
                {
                    // Tiến đến gần bề mặt bức tường/mục tiêu để tấn công
                    if (Agent != null && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
                        Vector3 walkTarget = closestPoint;
                        if (isStructure)
                        {
                            Vector3 dirOut = (transform.position - closestPoint);
                            dirOut.y = 0;
                            if (dirOut.sqrMagnitude > 0.001f) dirOut.Normalize();
                            else dirOut = (transform.position - targetCenter).normalized;

                            // Tản nhẹ đội hình bầy đàn sang 2 bên theo phương tiếp tuyến để quái vây quanh công trình thay vì đè lên đúng 1 điểm
                            Vector3 tangent = Vector3.Cross(Vector3.up, dirOut).normalized;
                            int dinoHash = Mathf.Abs(GetHashCode());
                            float sideOffset = ((dinoHash % 5) - 2) * 1.5f;

                            Vector3 standAttempt = closestPoint + dirOut * 1.4f + tangent * sideOffset;
                            if (NavMesh.SamplePosition(standAttempt, out NavMeshHit sHit, 3.5f, NavMesh.AllAreas))
                            {
                                walkTarget = sHit.position;
                            }
                        }
                        Agent.SetDestination(walkTarget);
                    }
                    return;
                }
            }

            // Look for blocking walls or defenders in path
            CurrentTarget = ScanForBlockers();

            if (CurrentTarget == null)
            {
                // Resume movement to base
                if (Agent != null && Agent.isOnNavMesh)
                {
                    Agent.isStopped = false;
                    if (!Agent.hasPath || Agent.destination != TargetBase.position)
                    {
                        Agent.SetDestination(TargetBase.position);
                    }
                }

                // Check if reached base (Hỗ trợ bầy đàn cắn phá căn cứ đồng thời)
                Collider baseCol = TargetBase.GetComponentInChildren<Collider>();
                Vector3 baseClosest = baseCol != null ? baseCol.ClosestPoint(transform.position) : TargetBase.position;
                Vector3 flatBaseDiff = transform.position - baseClosest;
                flatBaseDiff.y = 0;
                float distToBase = flatBaseDiff.magnitude;

                float baseAgentRadius = (Agent != null && Agent.enabled) ? Agent.radius : 0.8f;
                float baseDirectThreshold = Mathf.Max(AttackRange + baseAgentRadius + 3.0f, 5.0f);
                bool isSwarmingBase = (distToBase <= 8.5f) && (Agent != null && Agent.isOnNavMesh);

                if (distToBase <= baseDirectThreshold || isSwarmingBase)
                {
                    _hasReachedBase = true;

                    IDamageable baseDamageable = null;
                    if (PrehistoricVillageBase.Instance != null && PrehistoricVillageBase.Instance.Health > 0)
                    {
                        baseDamageable = PrehistoricVillageBase.Instance;
                    }
                    else if (TargetBase.TryGetComponent(out IDamageable bDmg) && bDmg.Health > 0)
                    {
                        baseDamageable = bDmg;
                    }

                    if (baseDamageable != null)
                    {
                        CurrentTarget = baseDamageable;
                        if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;

                        // Xoay mặt về hướng căn cứ
                        Vector3 baseDir = (TargetBase.position - transform.position);
                        baseDir.y = 0;
                        if (baseDir.sqrMagnitude > 0.001f)
                        {
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(baseDir), Time.deltaTime * 8f);
                        }

                        // Thực hiện cắn phá căn cứ theo chu kỳ tấn công
                        if (Time.time >= LastAttackTime + AttackInterval)
                        {
                            LastAttackTime = Time.time;
                            PerformAttack(CurrentTarget);
                        }
                        return;
                    }
                }
            }
        }

        protected virtual void UpdateAnimation(float currentSpeed)
        {
            _CurrentSpeed = currentSpeed;
            bool isMoving = currentSpeed > 0.15f;

            // Nhịp bước đi tăng tiến mượt mà theo vận tốc di chuyển
            if (isMoving)
            {
                float strideMultiplier = _IsQuadruped ? 2.5f : 3.2f;
                float strideRate = Mathf.Max(currentSpeed * strideMultiplier, 2.8f);
                _GaitTimer += Time.deltaTime * strideRate;
            }

            bool hasRealLegacyAnim = ModelLegacyAnimation != null 
                && ModelLegacyAnimation.enabled 
                && ModelLegacyAnimation.GetClipCount() > 0;

            // 1. Mecanim Animator Parameters
            if (ModelAnimator != null && ModelAnimator.enabled)
            {
                SetAnimatorFloatIfExists(ModelAnimator, "Speed", currentSpeed);
                SetAnimatorFloatIfExists(ModelAnimator, "Forward", currentSpeed);
                SetAnimatorBoolIfExists(ModelAnimator, "IsMoving", isMoving);
                SetAnimatorBoolIfExists(ModelAnimator, "Walk", isMoving);
                SetAnimatorBoolIfExists(ModelAnimator, "Run", currentSpeed > 3.0f);
            }

            // 2. Legacy Animation Clip Playback (nếu có sẵn clip thực sự)
            if (hasRealLegacyAnim)
            {
                string activeClip = _WalkClipName;
                if (string.IsNullOrEmpty(activeClip) && ModelLegacyAnimation.clip != null)
                {
                    activeClip = ModelLegacyAnimation.clip.name;
                }

                if (!string.IsNullOrEmpty(activeClip) && ModelLegacyAnimation[activeClip] != null)
                {
                    if (!_IsAttackingAnim)
                    {
                        if (!ModelLegacyAnimation.isPlaying || !ModelLegacyAnimation.IsPlaying(activeClip))
                        {
                            ModelLegacyAnimation.CrossFade(activeClip, 0.2f);
                        }

                        if (isMoving)
                        {
                            float speedRatio = currentSpeed / Mathf.Max(MoveSpeed * 0.7f, 1f);
                            ModelLegacyAnimation[activeClip].speed = Mathf.Clamp(speedRatio, 0.75f, 1.6f);
                        }
                        else
                        {
                            ModelLegacyAnimation[activeClip].speed = 0.35f;
                        }
                    }
                }
            }

            // 3. Dao động trọng tâm thân mình (Root Body Bobbing & Rolling Waddle)
            if (_HasBaseModelTransform && VisualModel != null && !_IsAttackingAnim)
            {
                if (isMoving)
                {
                    if (_IsQuadruped)
                    {
                        // Khủng long thiết giáp 4 chân: bước đi lặc lè, chuyển dịch trọng tâm uyển chuyển
                        float bobY = Mathf.Abs(Mathf.Sin(_GaitTimer * 2f)) * 0.05f;
                        float waddleRoll = Mathf.Sin(_GaitTimer) * 3.5f;
                        float heavePitch = Mathf.Cos(_GaitTimer * 2f) * 1.2f;

                        VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, bobY, 0f);
                        VisualModel.localRotation = _BaseModelLocalRot * Quaternion.Euler(heavePitch, 0f, waddleRoll);
                    }
                    else
                    {
                        // Khủng long 2 chân (T-Rex & Velociraptor): sải chân dài nhịp nhàng, lắc hông & nhấp nhô
                        float bobY = Mathf.Abs(Mathf.Sin(_GaitTimer * 2f)) * 0.08f;
                        float swayRoll = Mathf.Sin(_GaitTimer) * 2.8f;
                        float pitch = Mathf.Cos(_GaitTimer * 2f) * 1.8f;

                        VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, bobY, 0f);
                        VisualModel.localRotation = _BaseModelLocalRot * Quaternion.Euler(pitch, 0f, swayRoll);
                    }
                }
                else
                {
                    float breathe = Mathf.Sin(Time.time * 2.0f) * 0.02f;
                    VisualModel.localPosition = Vector3.Lerp(VisualModel.localPosition, _BaseModelLocalPos + new Vector3(0f, breathe, 0f), Time.deltaTime * 4f);
                    VisualModel.localRotation = Quaternion.Slerp(VisualModel.localRotation, _BaseModelLocalRot, Time.deltaTime * 4f);
                }
            }
        }

        protected virtual void LateUpdate()
        {
            if (VisualModel == null) return;

            // Kiểm tra xem có Mecanim hoặc Legacy clip thực sự đang phát cử động xương hay không
            bool hasRealMecanimMotion = ModelAnimator != null 
                && ModelAnimator.enabled 
                && ModelAnimator.runtimeAnimatorController != null
                && ModelAnimator.GetCurrentAnimatorClipInfo(0).Length > 0;

            bool hasRealLegacyAnim = ModelLegacyAnimation != null 
                && ModelLegacyAnimation.enabled 
                && ModelLegacyAnimation.isPlaying 
                && !string.IsNullOrEmpty(_WalkClipName);

            bool skipLegs = hasRealMecanimMotion || hasRealLegacyAnim;
            bool isMoving = _CurrentSpeed > 0.15f;

            if (!skipLegs)
            {
                if (_IsQuadruped)
                {
                    // =========================================================================
                    // KHỦNG LONG THIẾT GIÁP 4 CHÂN (Ankylosaurus)
                    // Dáng đi 4 chân đối xứng chéo (Diagonal Trot Gait):
                    // Cặp chéo A (Trước Trái + Sau Phải) và Cặp chéo B (Trước Phải + Sau Trái)
                    // =========================================================================
                    if (isMoving)
                    {
                        float pairASwing = Mathf.Sin(_GaitTimer) * 24f;
                        float pairBSwing = -pairASwing;

                        // Cặp chéo A: Chân trước trái & Chân sau phải cùng bước
                        if (_FrontLeftThighBone != null)
                            _FrontLeftThighBone.localRotation = _FrontLeftThighBaseRot * Quaternion.Euler(pairASwing, 0f, 0f);
                        if (_FrontLeftShinBone != null)
                            _FrontLeftShinBone.localRotation = _FrontLeftShinBaseRot * Quaternion.Euler(Mathf.Clamp(-pairASwing * 0.85f, 0f, 25f), 0f, 0f);

                        if (_RightThighBone != null)
                            _RightThighBone.localRotation = _RightThighBaseRot * Quaternion.Euler(pairASwing, 0f, 0f);
                        if (_RightShinBone != null)
                            _RightShinBone.localRotation = _RightShinBaseRot * Quaternion.Euler(Mathf.Clamp(-pairASwing * 0.85f, 0f, 25f), 0f, 0f);

                        // Cặp chéo B: Chân trước phải & Chân sau trái cùng bước
                        if (_FrontRightThighBone != null)
                            _FrontRightThighBone.localRotation = _FrontRightThighBaseRot * Quaternion.Euler(pairBSwing, 0f, 0f);
                        if (_FrontRightShinBone != null)
                            _FrontRightShinBone.localRotation = _FrontRightShinBaseRot * Quaternion.Euler(Mathf.Clamp(-pairBSwing * 0.85f, 0f, 25f), 0f, 0f);

                        if (_LeftThighBone != null)
                            _LeftThighBone.localRotation = _LeftThighBaseRot * Quaternion.Euler(pairBSwing, 0f, 0f);
                        if (_LeftShinBone != null)
                            _LeftShinBone.localRotation = _LeftShinBaseRot * Quaternion.Euler(Mathf.Clamp(-pairBSwing * 0.85f, 0f, 25f), 0f, 0f);
                    }
                    else
                    {
                        float t = Time.deltaTime * 6f;
                        if (_FrontLeftThighBone != null) _FrontLeftThighBone.localRotation = Quaternion.Slerp(_FrontLeftThighBone.localRotation, _FrontLeftThighBaseRot, t);
                        if (_FrontLeftShinBone != null) _FrontLeftShinBone.localRotation = Quaternion.Slerp(_FrontLeftShinBone.localRotation, _FrontLeftShinBaseRot, t);
                        if (_FrontRightThighBone != null) _FrontRightThighBone.localRotation = Quaternion.Slerp(_FrontRightThighBone.localRotation, _FrontRightThighBaseRot, t);
                        if (_FrontRightShinBone != null) _FrontRightShinBone.localRotation = Quaternion.Slerp(_FrontRightShinBone.localRotation, _FrontRightShinBaseRot, t);
                        if (_LeftThighBone != null) _LeftThighBone.localRotation = Quaternion.Slerp(_LeftThighBone.localRotation, _LeftThighBaseRot, t);
                        if (_LeftShinBone != null) _LeftShinBone.localRotation = Quaternion.Slerp(_LeftShinBone.localRotation, _LeftShinBaseRot, t);
                        if (_RightThighBone != null) _RightThighBone.localRotation = Quaternion.Slerp(_RightThighBone.localRotation, _RightThighBaseRot, t);
                        if (_RightShinBone != null) _RightShinBone.localRotation = Quaternion.Slerp(_RightShinBone.localRotation, _RightShinBaseRot, t);
                    }
                }
                else
                {
                    // =========================================================================
                    // KHỦNG LONG 2 CHÂN (T-Rex & Velociraptor)
                    // Hai chân bước đi đều đặn, co gập khớp gối tự nhiên, kết hợp vung 2 chi trước
                    // =========================================================================
                    if (isMoving)
                    {
                        float legSwing = Mathf.Sin(_GaitTimer) * 28f;

                        // Chân Trái: Đùi vung trước-sau, Gối co gập khi đẩy lùi, Bàn chân giữ thế
                        if (_LeftThighBone != null)
                            _LeftThighBone.localRotation = _LeftThighBaseRot * Quaternion.Euler(legSwing, 0f, 0f);
                        if (_LeftShinBone != null)
                            _LeftShinBone.localRotation = _LeftShinBaseRot * Quaternion.Euler(Mathf.Clamp(-legSwing * 1.15f, 0f, 32f), 0f, 0f);
                        if (_LeftFootBone != null)
                            _LeftFootBone.localRotation = _LeftFootBaseRot * Quaternion.Euler(Mathf.Clamp(legSwing * 0.45f, -15f, 15f), 0f, 0f);

                        // Chân Phải: Pha ngược lại hoàn toàn so với Chân Trái
                        if (_RightThighBone != null)
                            _RightThighBone.localRotation = _RightThighBaseRot * Quaternion.Euler(-legSwing, 0f, 0f);
                        if (_RightShinBone != null)
                            _RightShinBone.localRotation = _RightShinBaseRot * Quaternion.Euler(Mathf.Clamp(legSwing * 1.15f, 0f, 32f), 0f, 0f);
                        if (_RightFootBone != null)
                            _RightFootBone.localRotation = _RightFootBaseRot * Quaternion.Euler(Mathf.Clamp(-legSwing * 0.45f, -15f, 15f), 0f, 0f);

                        // Hai chi trước vung nhẹ nhịp nhàng đối trọng
                        if (_LeftArmBone != null)
                            _LeftArmBone.localRotation = _LeftArmBaseRot * Quaternion.Euler(-legSwing * 0.45f, 0f, 0f);
                        if (_RightArmBone != null)
                            _RightArmBone.localRotation = _RightArmBaseRot * Quaternion.Euler(legSwing * 0.45f, 0f, 0f);
                    }
                    else
                    {
                        float t = Time.deltaTime * 6f;
                        if (_LeftThighBone != null) _LeftThighBone.localRotation = Quaternion.Slerp(_LeftThighBone.localRotation, _LeftThighBaseRot, t);
                        if (_LeftShinBone != null) _LeftShinBone.localRotation = Quaternion.Slerp(_LeftShinBone.localRotation, _LeftShinBaseRot, t);
                        if (_LeftFootBone != null) _LeftFootBone.localRotation = Quaternion.Slerp(_LeftFootBone.localRotation, _LeftFootBaseRot, t);
                        if (_RightThighBone != null) _RightThighBone.localRotation = Quaternion.Slerp(_RightThighBone.localRotation, _RightThighBaseRot, t);
                        if (_RightShinBone != null) _RightShinBone.localRotation = Quaternion.Slerp(_RightShinBone.localRotation, _RightShinBaseRot, t);
                        if (_RightFootBone != null) _RightFootBone.localRotation = Quaternion.Slerp(_RightFootBone.localRotation, _RightFootBaseRot, t);
                        if (_LeftArmBone != null) _LeftArmBone.localRotation = Quaternion.Slerp(_LeftArmBone.localRotation, _LeftArmBaseRot, t);
                        if (_RightArmBone != null) _RightArmBone.localRotation = Quaternion.Slerp(_RightArmBone.localRotation, _RightArmBaseRot, t);
                    }
                }
            }

            // =========================================================================
            // ĐUÔI UỐN LƯỢN / QUẢ CHÙY ĐUÔI LẮC LƯ THEO NHỊP (Multi-Joint Tail Sway)
            // =========================================================================
            for (int i = 0; i < _TailBones.Count; i++)
            {
                if (_TailBones[i] == null) continue;
                float phase = isMoving ? (_GaitTimer - (i * 0.45f)) : (Time.time * 2.0f - (i * 0.35f));
                float amp = _IsQuadruped ? (isMoving ? 12.0f : 4.0f) : (isMoving ? 7.5f : 3.0f);
                float sway = Mathf.Sin(phase) * amp;
                _TailBones[i].localRotation = _TailBaseRots[i] * Quaternion.Euler(0f, sway, 0f);
            }

            // =========================================================================
            // ĐẦU & CỔ GẬT GÙ THEO NHỊP BƯỚC CHÂN (Head & Neck Rhythm)
            // =========================================================================
            if (isMoving)
            {
                float headBob = Mathf.Cos(_GaitTimer * 2f) * (_IsQuadruped ? 2.5f : 3.8f);
                if (_HeadBone != null)
                {
                    _HeadBone.localRotation = _HeadBaseRot * Quaternion.Euler(headBob, 0f, 0f);
                }
                if (_NeckBone != null)
                {
                    _NeckBone.localRotation = _NeckBaseRot * Quaternion.Euler(-headBob * 0.45f, 0f, 0f);
                }
            }
            else
            {
                if (_HeadBone != null) _HeadBone.localRotation = Quaternion.Slerp(_HeadBone.localRotation, _HeadBaseRot, Time.deltaTime * 4f);
                if (_NeckBone != null) _NeckBone.localRotation = Quaternion.Slerp(_NeckBone.localRotation, _NeckBaseRot, Time.deltaTime * 4f);
            }

            // =========================================================================
            // HÀM HÁ TO & CẮN MẠNH KHI TẤN CÔNG (Attack Jaw Bite Animation)
            // =========================================================================
            if (_AttackAnimProgress >= 0f)
            {
                float jawAngle = Mathf.Sin(_AttackAnimProgress * Mathf.PI) * 35f;
                if (_JawBone != null)
                {
                    _JawBone.localRotation = _JawBaseRot * Quaternion.Euler(jawAngle, 0f, 0f);
                }
                if (_HeadBone != null)
                {
                    _HeadBone.localRotation = _HeadBaseRot * Quaternion.Euler(jawAngle * 0.35f, 0f, 0f);
                }
            }
        }

        private static Wall EnsureDynamicWall(Collider col)
        {
            if (col == null) return null;

            // 1. Nếu col hoặc bất kỳ cha nào đã có sẵn component Wall thì dùng luôn, tuyệt đối không tạo thêm!
            Wall existingWall = col.GetComponentInParent<Wall>();
            if (existingWall != null) return existingWall;

            // 2. Chỉ leo lên các đối tượng cấp prefab đơn lẻ, KHÔNG leo lên các container nhóm cha
            Transform targetT = col.transform;
            while (targetT.parent != null && targetT.parent != col.transform.root &&
                   !targetT.parent.name.ToLower().Contains("world") &&
                   !targetT.parent.name.ToLower().Contains("props"))
            {
                string pn = targetT.parent.name.ToLower();
                if (pn.Contains("fortress") || pn.Contains("wing") || pn.Contains("flank") ||
                    pn.Contains("gate_complex") || pn.Contains("gate_entryway") ||
                    pn.Contains("container") || pn.Contains("group"))
                {
                    break; // Dừng lại ở đối tượng con, không leo lên container nhóm!
                }
                targetT = targetT.parent;
            }

            string n = targetT.gameObject.name.ToLower();

            // Tuyệt đối KHÔNG biến container nhóm tổng, cầu sàn đi bộ, mặt đất, terrain, collider phụ vô hình thành tường
            if (n == "village_fortress_wall" || n == "left_wing_wall" || n == "right_wing_wall" ||
                n == "right_flank_wall" || n == "gate_complex" || n == "gate_entryway" ||
                n == "world" || n == "props" ||
                n.Contains("pillar") || n.Contains("deck") || n.Contains("walkway") || n.Contains("helper") ||
                n.Contains("road") || n.Contains("path") || n.Contains("floor") || n.Contains("ground") || n.Contains("terrain"))
            {
                return null;
            }

            // Đối tượng bắt buộc phải có Renderer hiển thị (tránh chọn trúng collider tàng hình)
            if (targetT.GetComponentInChildren<Renderer>() == null && targetT.GetComponentInParent<Renderer>() == null)
            {
                return null;
            }

            bool hasWallTag = false;
            try { hasWallTag = col.CompareTag("Wall") || col.CompareTag("Obstacle"); } catch {}

            if (n.Contains("wall") || n.Contains("barricade") ||
                n.Contains("fence") || n.Contains("palisade") || n.Contains("barrier") ||
                n.Contains("plank_defenses") || n.Contains("big tower") || n.Contains("arch entry") ||
                n.Contains("tower") || n.Contains("platform") ||
                hasWallTag)
            {
                GameObject go = targetT.gameObject;
                if (!go.TryGetComponent(out Wall wall))
                {
                    wall = go.AddComponent<Wall>();
                    wall.MaxHealth = 900;
                    wall.Health = 900;
                    wall.EnsureHealthBarAttached();
                }
                return wall;
            }
            return null;
        }

        protected virtual float WallScanRadius => 8.5f;

        protected virtual IDamageable ScanForBlockers()
        {
            if (_IsNavigatingBreach) return null;
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh) return null;
            if (Agent.pathPending) return null;

            if (TargetBase == null) FindTargetBase();
            if (TargetBase == null) return null;

            Vector3 myPos = transform.position;
            Vector3 basePos = TargetBase.position;
            Vector3 toBase = basePos - myPos;
            toBase.y = 0;
            float myDistToBase = toBase.magnitude;
            if (myDistToBase < 0.2f) return null;
            Vector3 toBaseDir = toBase.normalized;

            // 1. TÌM VẬT CẢN (WALL / BARRICADE / TOWER) CHẶN TRƯỚC MẶT TRÊN ĐƯỜNG TIẾN VỀ CĂN CỨ
            IDamageable directFrontBlocker = FindDirectFrontBlocker(myPos, basePos, myDistToBase, toBaseDir);

            // 2. NẾU CÓ VẬT CẢN TRỰC TIẾP TRƯỚC MẶT:
            if (directFrontBlocker != null)
            {
                Collider blockerCol = directFrontBlocker.Transform.GetComponentInChildren<Collider>();
                Vector3 closestPoint = blockerCol != null ? blockerCol.ClosestPoint(myPos) : directFrontBlocker.Transform.position;
                float distToBlocker = Vector3.Distance(myPos, closestPoint);

                float agentRadius = (Agent != null && Agent.enabled) ? Agent.radius : 0.8f;

                // A. NẾU ĐÃ ÁP SÁT CỰ LY CẬN CHIẾN HOẶC VẬT CẢN TRƯỚC MẶT: TẤN CÔNG NGAY LẬP TỨC!
                float attackThreshold = (directFrontBlocker is Wall || directFrontBlocker is PrehistoricVillageBase)
                    ? Mathf.Max(AttackRange + agentRadius + 4.5f, 8.5f)
                    : (AttackRange + agentRadius + 1.2f);

                if (distToBlocker <= attackThreshold)
                {
                    return directFrontBlocker;
                }

                // B. Nếu còn ở xa hơn: Kiểm tra xem có khe hở đột phá nào gần đó (< 20m) hướng về căn cứ và CÒN CHỖ TRỐNG (< 4 con) không?
                if (TacticalBreachMemory.HasInstance)
                {
                    var nearbyBreach = TacticalBreachMemory.Instance.GetBestBreachTowards(myPos, basePos, this, 20.0f);
                    if (nearbyBreach != null && !_TraversedBreaches.Contains(nearbyBreach) && nearbyBreach.TryClaimSlot(this))
                    {
                        AssignToBreach(nearbyBreach);
                        return null; // Chuyển sang đi qua khe hở đột phá
                    }
                }

                // C. Nếu không có khe hở nào gần đó còn slot: Tấn công vật cản trước mặt!
                return directFrontBlocker;
            }

            // 3. NẾU PHÍA TRƯỚC HOÀN TOÀN THÔNG SUỐT (KHÔNG CÓ VẬT CẢN NÀO CHẶN):
            // Khủng long tiếp tục chạy thẳng một mạch về phía căn cứ!
            if (Agent.isOnNavMesh && (!Agent.hasPath || Agent.destination != basePos))
            {
                Agent.isStopped = false;
                Agent.SetDestination(basePos);
            }
            return null;
        }

        private IDamageable FindEmergencyWallBlocker(float radius)
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position, radius, ScanBuffer, TargetLayers);
            IDamageable nearestWall = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                Wall wall = col.GetComponentInParent<Wall>();
                if (wall == null) wall = EnsureDynamicWall(col);
                if (wall != null && wall.Health > 0)
                {
                    Vector3 closest;
                    try { closest = col.ClosestPoint(transform.position); }
                    catch { closest = col.bounds.ClosestPoint(transform.position); }

                    Vector3 flat = closest - transform.position;
                    flat.y = 0;
                    float d = flat.magnitude;

                    float dotFwd = Vector3.Dot(transform.forward, flat.normalized);
                    if (dotFwd >= -0.2f && d < nearestDist)
                    {
                        nearestDist = d;
                        nearestWall = wall;
                    }
                }
            }
            return nearestWall;
        }

        protected virtual IDamageable FindDirectFrontBlocker(Vector3 myPos, Vector3 basePos, float myDistToBase, Vector3 toBaseDir)
        {
            float scanRadius = Mathf.Max(AttackRange + 3.5f, WallScanRadius);
            int blockHits = Physics.OverlapSphereNonAlloc(myPos, scanRadius, ScanBuffer, TargetLayers);

            IDamageable bestBlocker = null;
            float bestScore = float.MinValue;

            Vector3 lateralDir = Vector3.Cross(Vector3.up, toBaseDir).normalized;

            for (int i = 0; i < blockHits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                Wall wall = col.GetComponentInParent<Wall>();
                if (wall == null) wall = EnsureDynamicWall(col);

                IDamageable candidate = null;
                if (wall != null && wall.Health > 0)
                {
                    // Nếu khủng long đang tích cực di chuyển qua khe hở (đã có slot), bỏ qua các bức tường bên mép
                    if (_IsNavigatingBreach && TacticalBreachMemory.HasInstance && TacticalBreachMemory.Instance.IsWallNearActiveBreach(wall.transform.position, 3.5f))
                    {
                        continue;
                    }
                    candidate = wall;
                }
                else
                {
                    Unit tower = col.GetComponentInParent<Unit>();
                    if (tower != null && !(tower is PrehistoricDinoBase) && tower.Health > 0)
                    {
                        candidate = tower;
                    }
                }

                if (candidate == null) continue;

                Vector3 closest;
                try { closest = col.ClosestPoint(myPos); }
                catch { closest = col.bounds.ClosestPoint(myPos); }

                Vector3 flatToCol = new Vector3(closest.x - myPos.x, 0f, closest.z - myPos.z);
                float distToCol = flatToCol.magnitude;
                if (distToCol < 0.05f) distToCol = 0.05f;
                Vector3 dirToCol = flatToCol / distToCol;

                float colDistToBase = Vector3.Distance(closest, basePos);

                // Kiểm tra góc hướng về căn cứ và hướng nhìn phía trước:
                float dotToBase = Vector3.Dot(toBaseDir, dirToCol);
                float dotForward = Vector3.Dot(transform.forward, dirToCol);

                float agentRadius = (Agent != null && Agent.enabled) ? Agent.radius : 0.8f;
                bool isVeryClose = distToCol <= Mathf.Max(AttackRange + agentRadius + 2.0f, 4.8f);

                if (isVeryClose)
                {
                    // Khi đã ở cự ly áp sát cận chiến (ngay trước mặt):
                    // Chỉ bỏ qua nếu vật thể THỰC SỰ ở hẳn sau lưng cả hướng nhìn lẫn căn cứ
                    if (dotForward < -0.2f && dotToBase < -0.2f && colDistToBase > myDistToBase + 1.2f)
                    {
                        continue;
                    }
                }
                else
                {
                    // Khi ở xa hơn:
                    // 1. Phải hướng về phía căn cứ (dotToBase >= 0.05f) hoặc nằm ngay trước mặt (dotForward >= 0.25f)
                    // 2. Không được nằm xa căn cứ hơn khủng long quá 1.5m
                    if ((dotToBase < 0.05f && dotForward < 0.25f) || colDistToBase > myDistToBase + 1.5f)
                    {
                        continue;
                    }

                    // 3. Nằm trong hành lang di chuyển về căn cứ (mở rộng lên 5.5m để bao quát 2 cánh)
                    float lateralOffset = Mathf.Abs(Vector3.Dot(lateralDir, flatToCol));
                    if (lateralOffset > 5.5f)
                    {
                        continue;
                    }
                }

                // Điểm số ưu tiên: Càng gần và càng thẳng hướng căn cứ / hướng nhìn thì điểm càng cao
                float closeBonus = isVeryClose ? 250f : 0f;
                float gateBonus = (candidate.Transform != null && candidate.Transform.name.Contains("Gate_Portcullis_Barricade")) ? 60f : 0f;
                float score = closeBonus + gateBonus + (dotToBase * 15f) + (dotForward * 10f) - distToCol;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBlocker = candidate;
                }
            }

            return bestBlocker;
        }

        protected static float CalculatePathLength(UnityEngine.AI.NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2) return 0f;
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            return length;
        }

        protected virtual void PerformAttack(IDamageable target)
        {
            bool isStructure = (target is Wall) || (target is PrehistoricVillageBase);
            int damage = isStructure ? Mathf.CeilToInt(AttackDamage * BuildingDamageMultiplier) : AttackDamage;
            target.TakeDamage(damage);

            // Báo động nguy cơ phá thành lũy/căn cứ cho toàn bộ Defender
            if (target is Wall wallTarget)
            {
                Enemy.Defender.BroadcastWallThreat(wallTarget.transform.position, this);
            }
            else if (target is PrehistoricVillageBase baseTarget)
            {
                Enemy.Defender.BroadcastWallThreat(baseTarget.transform.position, this);
            }

            bool hasAttackClip = ModelLegacyAnimation != null 
                && !string.IsNullOrEmpty(_AttackClipName) 
                && ModelLegacyAnimation[_AttackClipName] != null;

            if (hasAttackClip)
            {
                ModelLegacyAnimation[_AttackClipName].speed = 1.3f;
                ModelLegacyAnimation.CrossFade(_AttackClipName, 0.12f);
                StartCoroutine(AttackClipResetRoutine());
            }
            else
            {
                if (ModelAnimator != null && ModelAnimator.enabled)
                {
                    SetAnimatorTriggerIfExists(ModelAnimator, "IsAttacking");
                    SetAnimatorTriggerIfExists(ModelAnimator, "Attack");
                    SetAnimatorTriggerIfExists(ModelAnimator, "Bite");
                }
                StartCoroutine(AttackMotionRoutine());
            }
        }

        protected virtual IEnumerator AttackClipResetRoutine()
        {
            _IsAttackingAnim = true;
            yield return new WaitForSeconds(0.55f);
            if (ModelLegacyAnimation != null && !string.IsNullOrEmpty(_WalkClipName) && ModelLegacyAnimation[_WalkClipName] != null)
            {
                ModelLegacyAnimation.CrossFade(_WalkClipName, 0.2f);
            }
            _IsAttackingAnim = false;
        }

        protected virtual IEnumerator AttackMotionRoutine()
        {
            _IsAttackingAnim = true;
            _AttackAnimProgress = 0f;
            Vector3 origPos = _HasBaseModelTransform && VisualModel != null ? _BaseModelLocalPos : Vector3.zero;
            Quaternion origRot = _HasBaseModelTransform && VisualModel != null ? _BaseModelLocalRot : Quaternion.identity;

            float totalDuration = 0.42f;
            float elapsed = 0f;
            while (elapsed < totalDuration && VisualModel != null)
            {
                elapsed += Time.deltaTime;
                _AttackAnimProgress = Mathf.Clamp01(elapsed / totalDuration);

                // Lunge forward curve
                float lunge = Mathf.Sin(_AttackAnimProgress * Mathf.PI);
                VisualModel.localPosition = origPos + new Vector3(0f, -0.06f * lunge, 0.45f * lunge);
                VisualModel.localRotation = origRot * Quaternion.Euler(12f * lunge, 0f, 0f);
                yield return null;
            }

            _AttackAnimProgress = -1f;
            if (VisualModel != null)
            {
                VisualModel.localPosition = origPos;
                VisualModel.localRotation = origRot;
            }
            _IsAttackingAnim = false;
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}

        public override void Die()
        {
            if (_CurrentBreachPoint != null && TacticalBreachMemory.HasInstance)
            {
                _CurrentBreachPoint.ReleaseSlot(this);
                TacticalBreachMemory.Instance.DispatchNextDinosToBreach(_CurrentBreachPoint);
                RestoreOriginalColliderAndAgent();
                _CurrentBreachPoint = null;
                _IsNavigatingBreach = false;
            }

            // VÔ HIỆU HÓA NGAY LẬP TỨC COLLIDER VÀ NAVMESH AGENT:
            // Tránh xác quái vật trong lúc ngã chết (0.5s) tạo thành vật cản vô hình kẹt đường đồng đội
            Collider[] allCols = GetComponentsInChildren<Collider>();
            foreach (var c in allCols) if (c != null) c.enabled = false;
            if (Agent != null)
            {
                if (Agent.isOnNavMesh) Agent.isStopped = true;
                Agent.enabled = false;
            }

            if (ModelAnimator != null)
            {
                SetAnimatorTriggerIfExists(ModelAnimator, "Die");
                SetAnimatorTriggerIfExists(ModelAnimator, "Death");
            }

            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.HandleMonsterDeath(this);
            }
            else if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.AddGold(GoldReward);
            }

            RaiseDeathEvent();
            StartCoroutine(DeathTumbleRoutine());
        }

        protected virtual IEnumerator DeathTumbleRoutine()
        {
            if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;

            float t = 0f;
            Quaternion startRot = transform.rotation;
            Quaternion fallRot = startRot * Quaternion.Euler(0f, 0f, 75f);

            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float p = t / 0.35f;
                transform.rotation = Quaternion.Slerp(startRot, fallRot, p);
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);
            Destroy(gameObject);
        }

        private static void SetAnimatorFloatIfExists(Animator anim, string paramName, float value)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float && p.name == paramName)
                {
                    anim.SetFloat(paramName, value);
                    return;
                }
            }
        }

        private static void SetAnimatorBoolIfExists(Animator anim, string paramName, bool value)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == paramName)
                {
                    anim.SetBool(paramName, value);
                    return;
                }
            }
        }

        private static void SetAnimatorTriggerIfExists(Animator anim, string paramName)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == paramName)
                {
                    anim.SetTrigger(paramName);
                    return;
                }
            }
        }
    }
}
