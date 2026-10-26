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

        protected Collider[] ScanBuffer = new Collider[64];

        // GLB Animation Cache & Clips

        private static readonly Dictionary<string, AnimationClip[]> s_ClipsCache = new();

        private static readonly Dictionary<string, GameObject> s_GlbTemplateCache = new();

        protected string _IdleClipName;
        protected string _WalkClipName;
        protected string _AttackClipName;

        // Skeletal Bone Rig (Universal Biped & Quadruped)

        protected bool _IsQuadruped = false;

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

        protected Vector3 _BaseModelLocalPos;

        protected Quaternion _BaseModelLocalRot;

        protected bool _HasBaseModelTransform;

        protected bool _IsAttackingAnim;

        protected bool _IsEngagedInMelee = false;

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

            // Truy vấn vị trí NavMesh gần nhất trong AllAreas
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                Physics.SyncTransforms();

                if (!Agent.enabled)
                {
                    try { Agent.enabled = true; } catch { }
                }

                if (Agent.isOnNavMesh)
                {
                    Agent.Warp(hit.position);
                    return true;
                }
            }

            return false;
        }

        public void ConfigureNavMeshAgent()

        {

            if (Agent == null) Agent = GetComponent<NavMeshAgent>();

            if (Agent == null) return;

            Agent.speed = MoveSpeed;

            Agent.acceleration = 24f;

            Agent.angularSpeed = 360f;

            Agent.autoBraking = true;

            Agent.stoppingDistance = 0.5f;

            Agent.autoRepath = true;

            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            if (this is BossDino || gameObject.name.ToLower().Contains("trex"))

            {

                Agent.radius = 0.95f;

            }

            else if (this is SiegeDino || gameObject.name.ToLower().Contains("ankyl"))

            {

                Agent.radius = 0.85f;

            }

            else

            {

                Agent.radius = 0.45f;

            }

            _DefaultAgentRadius = Agent.radius;

            // Giới hạn Collider của khủng long không quá to để không bị kẹt góc tường hoặc chèn ép nhau

            if (TryGetComponent(out BoxCollider bc))

            {

                Vector3 sz = bc.size;

                bc.size = new Vector3(Mathf.Min(sz.x, 1.6f), sz.y, Mathf.Min(sz.z, 3.2f));

            }

            if (TryGetComponent(out CapsuleCollider cc))

            {

                cc.radius = Mathf.Min(cc.radius, 1.25f);

            }

        }

        protected virtual void OnEnable()

        {

            // Nếu là bóng mờ xem trước (Ghost Preview), không kích hoạt NavMesh / AI

            if (GetComponentInParent<PlaceDinoVisualization>() != null || (transform.parent != null && transform.parent.GetComponent<PlaceDinoVisualization>() != null))

            {

                if (Agent != null) Agent.enabled = false;

                return;

            }

            s_ActiveDinos.Add(this);

            ConfigureNavMeshAgent();

            EnsureOnNavMesh(25f);

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

                    closest = GetSafeClosestPoint(col, myPos);

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

            // Nếu là bóng mờ xem trước đặt quân, tắt NavMeshAgent và dừng khởi tạo AI

            if (GetComponentInParent<PlaceDinoVisualization>() != null || (transform.parent != null && transform.parent.GetComponent<PlaceDinoVisualization>() != null))

            {

                if (Agent != null) Agent.enabled = false;

                enabled = false;

                return;

            }

            // Ensure Kinematic Rigidbody exists to prevent PhysX static collider moving depenetration jitter

            if (!TryGetComponent(out Rigidbody rb))

            {

                rb = gameObject.AddComponent<Rigidbody>();

            }

            rb.isKinematic = true;

            rb.useGravity = false;

            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Gán layer 8 (Dinos) và bật cờ bỏ qua va chạm vật lý giữa khủng long với nhau

            gameObject.layer = 8;

            Physics.IgnoreLayerCollision(8, 8, true);

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

                string dinoName = gameObject.name.ToLower();

                if (ModelAnimator.runtimeAnimatorController == null)

                {

                    if (dinoName.Contains("trex") || dinoName.Contains("t-rex") || this is BossDino)

                    {

                        ModelAnimator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/TRex_Controller");

                    }

                    else if (dinoName.Contains("ankyl") || this is SiegeDino)

                    {

                        // Khung long Thiet Giap Ankylosaurus van hanh he thong dang di 4 chan doc quyen trong SiegeDino

                        ModelAnimator.runtimeAnimatorController = null;

                        ModelAnimator.enabled = false;

                    }

                    else if (dinoName.Contains("velo") || this is RunnerDino)
                    {
                        // Khủng long nhỏ Velociraptor: Vận hành hoạt ảnh Legacy từ file GLB gốc của người dùng
                        Transform animHost = VisualModel != null ? VisualModel.Find("Sketchfab_model") : null;
                        if (animHost == null) animHost = VisualModel != null ? VisualModel : transform;
                        if (ModelLegacyAnimation == null)
                        {
                            ModelLegacyAnimation = animHost.GetComponent<Animation>();
                            if (ModelLegacyAnimation == null) ModelLegacyAnimation = animHost.gameObject.AddComponent<Animation>();
                        }
                        AnimationClip walkClip = Resources.Load<AnimationClip>("Animations/Velociraptor_Walk");
                        AnimationClip runClip = Resources.Load<AnimationClip>("Animations/Velociraptor_Run");
                        AnimationClip attackClip = Resources.Load<AnimationClip>("Animations/Velociraptor_Attack");
                        if (walkClip != null)
                        {
                            walkClip.legacy = true;
                            walkClip.wrapMode = WrapMode.Loop;
                            if (ModelLegacyAnimation["Velociraptor_Walk"] == null) ModelLegacyAnimation.AddClip(walkClip, "Velociraptor_Walk");
                            ModelLegacyAnimation.clip = walkClip;
                            _WalkClipName = "Velociraptor_Walk";
                            _IdleClipName = "Velociraptor_Walk";
                        }
                        if (runClip != null)
                        {
                            runClip.legacy = true;
                            runClip.wrapMode = WrapMode.Loop;
                            if (ModelLegacyAnimation["Velociraptor_Run"] == null) ModelLegacyAnimation.AddClip(runClip, "Velociraptor_Run");
                        }
                        if (attackClip != null)
                        {
                            attackClip.legacy = true;
                            attackClip.wrapMode = WrapMode.Once;
                            if (ModelLegacyAnimation["Velociraptor_Attack"] == null) ModelLegacyAnimation.AddClip(attackClip, "Velociraptor_Attack");
                            _AttackClipName = "Velociraptor_Attack";
                        }
                        ModelLegacyAnimation.playAutomatically = true;
                        if (!string.IsNullOrEmpty(_WalkClipName)) ModelLegacyAnimation.Play(_WalkClipName);
                        if (VisualModel != null)
                        {
                            VisualModel.localRotation = Quaternion.identity;
                            _BaseModelLocalRot = Quaternion.identity;
                        }
                        if (ModelAnimator != null)
                        {
                            ModelAnimator.runtimeAnimatorController = null;
                            ModelAnimator.enabled = false;
                        }
                    }

                }

                if (ModelLegacyAnimation != null && ModelLegacyAnimation.clip == null)

                {

                    if (dinoName.Contains("ankyl") || this is SiegeDino)

                    {

                        AnimationClip ankyClip = Resources.Load<AnimationClip>("Animations/Ankylosaurus_Walk");

                        if (ankyClip != null)

                        {

                            ankyClip.legacy = true;

                            ankyClip.wrapMode = WrapMode.Loop;

                            ModelLegacyAnimation.clip = ankyClip;

                            ModelLegacyAnimation.AddClip(ankyClip, "Walk");

                            ModelLegacyAnimation.Play("Walk");

                        }

                    }

                }

                if (ModelAnimator != null && ModelAnimator.runtimeAnimatorController != null)
                {
                    ModelAnimator.enabled = true;
                    ModelAnimator.applyRootMotion = false;
                    ModelAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                else if (ModelAnimator != null)
                {
                    ModelAnimator.enabled = false;
                }

            }

            if (gameObject.name.ToLower().Contains("trex") || this is BossDino)

            {

                Material trexMat = Resources.Load<Material>("Materials/M_TRex");

                if (trexMat != null)

                {

                    foreach (var r in GetComponentsInChildren<Renderer>(true))

                    {

                        if (r.sharedMaterial == null || r.sharedMaterial.name.Contains("Default") || r.sharedMaterial.name.Contains("None"))

                        {

                            r.sharedMaterial = trexMat;

                        }

                    }

                }

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

            s_GlbTemplateCache.Clear();

        }

        private void EnsureMaterialsValid()
        {
            if (VisualModel == null) return;
            var renderers = VisualModel.GetComponentsInChildren<Renderer>(true);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            foreach (var r in renderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                bool needsFix = false;
                if (mats == null || mats.Length == 0)
                {
                    needsFix = true;
                }
                else
                {
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null || mats[i].shader == null || mats[i].shader.name == "Hidden/InternalErrorShader")
                        {
                            needsFix = true;
                            break;
                        }
                    }
                }

                if (needsFix)
                {
                    Material fallbackMat = null;
                    string dName = gameObject.name.ToLower();
                    if (dName.Contains("velo") || this is RunnerDino)
                    {
                        fallbackMat = Resources.Load<Material>("Models/Velociraptor_Native/Velociraptor_Body_Mat");
                    }
                    if (fallbackMat == null)
                    {
                        fallbackMat = new Material(litShader);
                    }

                    int count = (mats != null && mats.Length > 0) ? mats.Length : 1;
                    Material[] newMats = new Material[count];
                    for (int i = 0; i < count; i++)
                    {
                        newMats[i] = (mats != null && i < mats.Length && mats[i] != null && mats[i].shader != null && mats[i].shader.name != "Hidden/InternalErrorShader")
                            ? mats[i]
                            : fallbackMat;
                    }
                    r.sharedMaterials = newMats;
                }
            }
        }

        private bool HasVisibleMesh()

        {

            if (VisualModel == null) return false;

            var smrs = VisualModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var s in smrs) if (s != null && s.sharedMesh != null) return true;

            var mfs = VisualModel.GetComponentsInChildren<MeshFilter>(true);

            foreach (var m in mfs) if (m != null && m.sharedMesh != null) return true;

            return false;

        }

        private static byte[] LoadGlbBytes(string glbName)

        {

            try

            {

                // 1. Resources (.bytes) - Hoạt động 100% trong bản build Standalone Player (.exe)

                string assetName = Path.GetFileNameWithoutExtension(glbName);

                TextAsset ta = Resources.Load<TextAsset>(assetName);

                if (ta != null && ta.bytes != null && ta.bytes.Length > 0)

                {

                    return ta.bytes;

                }

                // 2. StreamingAssets - Hoạt động 100% trong bản build Standalone Player (.exe)

                string streamingPath = Path.Combine(Application.streamingAssetsPath, glbName);

                if (File.Exists(streamingPath))

                {

                    return File.ReadAllBytes(streamingPath);

                }

                // 3. Application.dataPath - Fallback trong Editor

                string dataPath = Path.Combine(Application.dataPath, glbName);

                if (File.Exists(dataPath))

                {

                    return File.ReadAllBytes(dataPath);

                }

            }

            catch (System.Exception ex)

            {

                Debug.LogWarning($"[DinoVisual] Failed reading bytes for {glbName}: {ex.Message}");

            }

            return null;

        }

        private async System.Threading.Tasks.Task EnsureVisualModelFromGlbAsync(string glbName)

        {

            try

            {

                if (VisualModel == null)

                {

                    GameObject vmGo = new GameObject("Model");

                    vmGo.transform.SetParent(transform, false);

                    VisualModel = vmGo.transform;

                }

                if (s_GlbTemplateCache.TryGetValue(glbName, out GameObject cachedTemplate) && cachedTemplate != null)

                {

                    AttachGlbVisualModel(cachedTemplate, glbName);

                    return;

                }

                byte[] glbBytes = LoadGlbBytes(glbName);

                if (glbBytes == null || glbBytes.Length == 0) return;

                var gltf = new GltfImport();

                bool loaded = await gltf.LoadGltfBinary(glbBytes);

                if (loaded)

                {

                    GameObject templateObj = new GameObject($"__GlbTemplate_{glbName}");

                    templateObj.SetActive(false);

                    DontDestroyOnLoad(templateObj);

                    await gltf.InstantiateMainSceneAsync(templateObj.transform);

                    s_GlbTemplateCache[glbName] = templateObj;

                    AttachGlbVisualModel(templateObj, glbName);

                }

            }

            catch (System.Exception ex)

            {

                Debug.LogWarning($"[DinoVisual] Error instantiating visual mesh for {glbName}: {ex.Message}");

            }

        }

        private void AttachGlbVisualModel(GameObject template, string glbName)

        {

            if (template == null || VisualModel == null) return;

            // Instantiate template hierarchy into VisualModel

            GameObject newVisual = Instantiate(template, VisualModel);

            newVisual.name = "GlbVisualMesh";

            newVisual.transform.localPosition = Vector3.zero;

            newVisual.transform.localRotation = Quaternion.identity;

            newVisual.SetActive(true);

            // Hook up Animator

            if (ModelAnimator == null)

            {

                ModelAnimator = newVisual.GetComponentInChildren<Animator>(true);

            }

            if (ModelAnimator != null && ModelAnimator.runtimeAnimatorController == null)

            {

                string lower = glbName.ToLower();

                if (lower.Contains("velo") || this is RunnerDino)

                {

                    ModelAnimator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/Velociraptor_Controller");

                }

                else if (lower.Contains("trex") || this is BossDino)

                {

                    ModelAnimator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/TRex_Controller");

                }

                ModelAnimator.applyRootMotion = false;

                ModelAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                ModelAnimator.enabled = true;

            }

            // Adjust scale (~1.6m height for velociraptor)

            Renderer[] renderers = newVisual.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length > 0)

            {

                Bounds bounds = renderers[0].bounds;

                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                if (bounds.size.y > 0.05f)

                {

                    float targetH = (this is RunnerDino) ? 1.6f : ((this is BossDino) ? 5.5f : 2.2f);

                    float scaleFactor = targetH / bounds.size.y;

                    newVisual.transform.localScale = Vector3.one * scaleFactor;

                }

            }

        }

        private async void TryLoadGlbAnimations()

        {

            string glbName = GetModelGlbFileName();

            // 1. Phục hồi visual mesh ngay lập tức nếu mesh bị thiếu (ví dụ standalone player .exe)

            if (!HasVisibleMesh() && !string.IsNullOrEmpty(glbName))

            {

                await EnsureVisualModelFromGlbAsync(glbName);

            }

            // Nếu đã có Animator hoạt động với controller, dùng Mecanim trực tiếp

            if (ModelAnimator != null && ModelAnimator.runtimeAnimatorController != null)

            {

                return;

            }

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

            byte[] glbData = LoadGlbBytes(glbName);

            if (glbData == null || glbData.Length == 0) return;

            try

            {

                var gltf = new GltfImport();

                var importSettings = new ImportSettings

                {

                    AnimationMethod = AnimationMethod.Legacy

                };

                bool success = await gltf.LoadGltfBinary(glbData, importSettings: importSettings);

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

                        if (_IdleClipName == null && (cn.Contains("idle") || cn.Contains("wait") || cn.Contains("stand")))

                            _IdleClipName = clip.name;

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

                if (ModelAnimator != null && ModelAnimator.enabled)

                {

                    SetAnimatorBoolIfExists(ModelAnimator, "IsMoving", true);

                    SetAnimatorBoolIfExists(ModelAnimator, "Walk", true);

                    SetAnimatorFloatIfExists(ModelAnimator, "Speed", MoveSpeed > 0 ? MoveSpeed : 2.5f);

                }

                if (ModelLegacyAnimation != null && ModelLegacyAnimation.enabled)

                {

                    if (ModelLegacyAnimation.clip != null) ModelLegacyAnimation.Play();

                    else if (!string.IsNullOrEmpty(_WalkClipName) && ModelLegacyAnimation[_WalkClipName] != null) ModelLegacyAnimation.Play(_WalkClipName);

                }

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

                if (ModelAnimator != null && ModelAnimator.enabled)

                {

                    SetAnimatorBoolIfExists(ModelAnimator, "IsMoving", true);

                    SetAnimatorBoolIfExists(ModelAnimator, "Walk", true);

                    SetAnimatorFloatIfExists(ModelAnimator, "Speed", MoveSpeed > 0 ? MoveSpeed : 2.5f);

                }

                if (ModelLegacyAnimation != null && ModelLegacyAnimation.enabled)

                {

                    if (ModelLegacyAnimation.clip != null) ModelLegacyAnimation.Play();

                    else if (!string.IsNullOrEmpty(_WalkClipName) && ModelLegacyAnimation[_WalkClipName] != null) ModelLegacyAnimation.Play(_WalkClipName);

                }

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

            float agentRadius = (Agent != null && Agent.enabled) ? Agent.radius : 0.8f;

            float meleeAttackRange = Mathf.Max(AttackRange + agentRadius + 0.6f, 2.6f);

            float meleeExitRange = meleeAttackRange + 0.85f; // Ngưỡng Hysteresis triệt tiêu 100% rung giật bật/tắt di chuyển 60Hz

            // 1. Kiểm tra mục tiêu hiện tại còn tồn tại không

            if (CurrentTarget != null && (CurrentTarget.Health <= 0 || CurrentTarget.Transform == null || !CurrentTarget.Transform.gameObject.activeInHierarchy))
            {
                CurrentTarget = null;
                _IsEngagedInMelee = false;
                // Giải phóng ngay lập tức khủng long bị kẹt tại vị trí công trình vừa sụp đổ:
                if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit unstickHit, 3.5f, NavMesh.AllAreas))
                    {
                        Agent.Warp(unstickHit.position);
                    }
                    Agent.isStopped = false;
                    Agent.updateRotation = true;
                    if (TargetBase != null)
                    {
                        Agent.SetDestination(TargetBase.position);
                    }
                }
            }

            // 2. Tìm vật cản trực tiếp trước mặt trên đường tiến nếu chưa có target

            if (CurrentTarget == null)

            {

                CurrentTarget = FindImmediatePathBlocker(Mathf.Max(WallScanRadius, 15.0f));

            }

            // 3. Nếu đang có vật cản trước mặt: Tiến đến cự ly cận chiến và phá hủy

            if (CurrentTarget != null && CurrentTarget.Health > 0)

            {

                Collider targetCol = CurrentTarget.Transform.GetComponentInChildren<Collider>();

                Vector3 targetCenter = targetCol != null ? targetCol.bounds.center : CurrentTarget.Transform.position;

                Vector3 closestPoint = targetCol != null ? GetSafeClosestPoint(targetCol, transform.position) : targetCenter;

                Vector3 flatDiff = transform.position - closestPoint;

                flatDiff.y = 0;

                float distToClosest = flatDiff.magnitude;

                // State machine với Hysteresis: Vào cự ly thì khóa đánh, chỉ thoát khi mục tiêu rời xa > meleeExitRange
                if (!_IsEngagedInMelee && (distToClosest <= meleeAttackRange || (Agent != null && Agent.isOnNavMesh && Agent.remainingDistance <= Agent.stoppingDistance + 0.35f && distToClosest <= meleeExitRange)))
                {
                    _IsEngagedInMelee = true;
                }
                else if (_IsEngagedInMelee && distToClosest > meleeExitRange)
                {
                    _IsEngagedInMelee = false;
                }

                if (_IsEngagedInMelee)
                {
                    // Đứng yên tấn công mượt mà, giữ nguyên path để tránh 60Hz ResetPath giật cục
                    if (Agent != null && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = true;
                        Agent.updateRotation = false;
                        Agent.velocity = Vector3.zero;
                    }

                    Vector3 look = (closestPoint - transform.position);
                    look.y = 0;
                    if (look.sqrMagnitude < 0.2f)
                    {
                        look = (targetCenter - transform.position);
                        look.y = 0;
                    }

                    // Góc deadzone 8 độ và tốc độ xoay 90 deg/s mượt mà, không xoay giật khi đang cắn
                    if (!_IsAttackingAnim && look.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(look.normalized);
                        if (Quaternion.Angle(transform.rotation, targetRot) > 8.0f)
                        {
                            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 90f * Time.deltaTime);
                        }
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
                    if (Agent != null && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
                        Agent.updateRotation = true;

                        // Di chuyển đến điểm cách mép công trình một khoảng an toàn (standoffPoint) trên NavMesh,
                        // tuyệt đối không đâm xuyên vào lòng collider công trình gây bật ngược va chạm vật lý!
                        Vector3 standoffDir = (transform.position - closestPoint);
                        standoffDir.y = 0;
                        if (standoffDir.sqrMagnitude < 0.01f) standoffDir = -transform.forward;
                        Vector3 standoffPoint = closestPoint + standoffDir.normalized * (agentRadius + 0.5f);
                        if (NavMesh.SamplePosition(standoffPoint, out NavMeshHit standHit, 2.0f, NavMesh.AllAreas))
                        {
                            standoffPoint = standHit.position;
                        }

                        if (!Agent.hasPath || Vector3.Distance(Agent.destination, standoffPoint) > 0.8f)
                        {
                            Agent.SetDestination(standoffPoint);
                        }
                    }
                    return;
                }
            }

            // 4. Nếu không vướng vật cản: Hành quân thẳng về Nhà chính Làng (TargetBase)

            _IsEngagedInMelee = false;

            Collider baseCol = TargetBase.GetComponentInChildren<Collider>();

            Vector3 baseClosest = baseCol != null ? GetSafeClosestPoint(baseCol, transform.position) : TargetBase.position;

            Vector3 toBaseFlat = transform.position - baseClosest;

            toBaseFlat.y = 0;

            float distToBase = toBaseFlat.magnitude;

            float baseAttackThreshold = Mathf.Max(AttackRange + agentRadius + 0.8f, 3.2f);

            float baseExitThreshold = baseAttackThreshold + 1.0f;

            if (distToBase <= baseAttackThreshold)

            {

                if (Agent != null && Agent.isOnNavMesh)

                {

                    Agent.isStopped = true;

                    Agent.updateRotation = false;

                    Agent.velocity = Vector3.zero;

                    if (Agent.hasPath) Agent.ResetPath();

                }

                Vector3 baseDir = (baseClosest - transform.position);

                baseDir.y = 0;

                if (baseDir.sqrMagnitude < 0.2f)

                {

                    baseDir = (TargetBase.position - transform.position);

                    baseDir.y = 0;

                }

                // Tuyệt đối không xoay body khi đang vung đòn cắn (_IsAttackingAnim) để triệt tiêu hiện tượng giật lắc đầu

                if (!_IsAttackingAnim && baseDir.sqrMagnitude > 0.01f)

                {

                    Quaternion baseRot = Quaternion.LookRotation(baseDir.normalized);

                    if (Quaternion.Angle(transform.rotation, baseRot) > 5.0f)

                    {

                        transform.rotation = Quaternion.RotateTowards(transform.rotation, baseRot, 120f * Time.deltaTime);

                    }

                }

                if (Time.time >= LastAttackTime + AttackInterval)

                {

                    LastAttackTime = Time.time;

                    IDamageable baseDamageable = (PrehistoricVillageBase.Instance != null && PrehistoricVillageBase.Instance.Health > 0)

                        ? (IDamageable)PrehistoricVillageBase.Instance

                        : TargetBase.GetComponent<IDamageable>();

                    if (baseDamageable != null)

                    {

                        PerformAttack(baseDamageable);

                    }

                }

                return;

            }

            else

            {

                // Chỉ đặt destination khi chưa có path hoặc vị trí đích lệch xa > 2m

                if (Agent != null && Agent.isOnNavMesh)

                {

                    Agent.isStopped = false;

                    Agent.updateRotation = true;

                    if (!Agent.hasPath || Vector3.Distance(Agent.destination, TargetBase.position) > 2.0f)

                    {

                        Agent.SetDestination(TargetBase.position);

                    }

                }

            }

        }

        protected virtual IDamageable FindImmediatePathBlocker(float maxDist)
        {
            if (TargetBase == null) return null;

            Vector3 myPos = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 basePos = TargetBase.position;
            Vector3 toBase = basePos - myPos;
            toBase.y = 0;
            float myDistToBase = toBase.magnitude;
            if (myDistToBase < 0.1f) return null;
            Vector3 toBaseDir = toBase.normalized;
            Vector3 lateralDir = Vector3.Cross(Vector3.up, toBaseDir).normalized;

            int hits = Physics.OverlapSphereNonAlloc(myPos, maxDist, ScanBuffer, TargetLayers);
            IDamageable bestBlocker = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < hits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                // Kiểm tra Wall
                Wall wall = col.GetComponentInParent<Wall>();
                if (wall == null) wall = EnsureDynamicWall(col);

                IDamageable candidate = null;
                if (wall != null && wall.Health > 0)
                {
                    candidate = wall;
                }
                else
                {
                    Unit unit = col.GetComponentInParent<Unit>();
                    if (unit != null && !(unit is PrehistoricDinoBase) && unit.Health > 0)
                    {
                        // Logic theo yêu cầu người chơi: Khủng long KHÔNG đi săn đuổi NPC!
                        // Khủng long luôn tiến thẳng về Nhà chính Làng, đập phá mọi vật cản trước mặt.
                        // Chỉ cắn NPC nếu NPC đứng áp sát ngay trước mũi cắn (cự ly cận chiến).
                        if (unit is Enemy.Defender)
                        {
                            Vector3 closestP = GetSafeClosestPoint(col, myPos);
                            float distNpc = Vector3.Distance(myPos, closestP);
                            if (distNpc <= (AttackRange + 0.6f))
                            {
                                candidate = unit;
                            }
                        }
                        else
                        {
                            candidate = unit;
                        }
                    }
                }

                if (candidate == null) continue;

                Vector3 closest = GetSafeClosestPoint(col, myPos);
                Vector3 toCol = closest - myPos;
                toCol.y = 0;
                float d = toCol.magnitude;

                float colDistToBase = Vector3.Distance(closest, basePos);
                float dotFwd = toCol.sqrMagnitude > 0.01f ? Vector3.Dot(fwd, toCol.normalized) : 1f;
                float dotToBase = toCol.sqrMagnitude > 0.01f ? Vector3.Dot(toBaseDir, toCol.normalized) : 1f;
                float lateralOffset = Mathf.Abs(Vector3.Dot(lateralDir, toCol));

                // 1. Nếu đã ở cự ly áp sát cận chiến: Luôn ưu tiên đánh tối đa!
                bool isTouching = d <= (AttackRange + 1.8f);

                // 2. Nếu ở xa hơn: Chỉ chọn mục tiêu nằm ở nửa phía trước hướng tiến về căn cứ và nằm trong hành lang tiến quân
                if (!isTouching)
                {
                    if (dotToBase < -0.15f && dotFwd < -0.15f) continue;
                    if (colDistToBase > myDistToBase + 2.0f) continue;
                    if (lateralOffset > 8.5f && d > 6.0f) continue;
                }

                // Điểm số ưu tiên:
                // - Đang chạm vào hoặc ở ngay sát mũi: Ưu tiên tối thượng (+300)
                // - Tháp phòng thủ (Catapult, Ballista, Tesla, Archer) là hiểm họa nguy hiểm (+50)
                // - Càng thẳng hướng tiến về căn cứ (dotToBase * 30) và hướng mặt (dotFwd * 20) càng ưu tiên
                // - Càng gần càng ưu tiên (-d * 5)
                float threatBonus = 0f;
                if (candidate is PrehistoricVillageBase) threatBonus = 120f;
                else if (candidate is CatapultTower || candidate is BallistaTower || candidate is TeslaTower || candidate is ArcherTower) threatBonus = 60f;
                else if (candidate is Wall) threatBonus = 40f;
                else if (candidate is Enemy.Defender) threatBonus = -100f; // Luôn ưu tiên công trình và căn cứ hơn NPC

                float touchBonus = isTouching ? 300f : 0f;
                float score = touchBonus + threatBonus + (dotToBase * 30f) + (dotFwd * 20f) - (d * 5f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBlocker = candidate;
                }
            }

            return bestBlocker;
        }

        protected virtual void UpdateAnimation(float currentSpeed)

        {

            _CurrentSpeed = currentSpeed;

            bool isMoving = !_IsEngagedInMelee && !_IsAttackingAnim && (Agent != null && Agent.isOnNavMesh && !Agent.isStopped && (currentSpeed > 0.15f || (Agent.hasPath && Agent.remainingDistance > 0.35f)));

            if (isMoving)

            {

                float strideMultiplier = _IsQuadruped ? 2.5f : 3.2f;

                float strideRate = Mathf.Max(currentSpeed * strideMultiplier, 2.8f);

                _GaitTimer += Time.deltaTime * strideRate;

            }

            // 1. Mecanim Animator Parameters: Cấp tốc độ hiệu dụng đảm bảo trigger Walk/Run ngay khi di chuyển

            if (ModelAnimator != null && ModelAnimator.enabled)

            {

                float effectiveSpeed = isMoving ? Mathf.Max(currentSpeed, MoveSpeed > 0 ? MoveSpeed : 3.0f) : 0f;

                SetAnimatorFloatIfExists(ModelAnimator, "Speed", effectiveSpeed);

                SetAnimatorFloatIfExists(ModelAnimator, "Forward", effectiveSpeed);

                SetAnimatorBoolIfExists(ModelAnimator, "IsMoving", isMoving);

                SetAnimatorBoolIfExists(ModelAnimator, "Walk", isMoving);

                SetAnimatorBoolIfExists(ModelAnimator, "Run", isMoving && effectiveSpeed > 3.5f);

            }

            // 2. Legacy Animation

            bool hasRealLegacyAnim = ModelLegacyAnimation != null 

                && ModelLegacyAnimation.enabled 

                && ModelLegacyAnimation.GetClipCount() > 0;

            if (hasRealLegacyAnim && !_IsAttackingAnim)

            {

                string activeClip = isMoving ? _WalkClipName : _IdleClipName;
                if (isMoving && currentSpeed > 3.8f && ModelLegacyAnimation["Run"] != null)
                {
                    activeClip = "Run";
                }
                if (string.IsNullOrEmpty(activeClip) && ModelLegacyAnimation.clip != null) activeClip = ModelLegacyAnimation.clip.name;
                if (!string.IsNullOrEmpty(activeClip) && ModelLegacyAnimation[activeClip] != null)
                {
                    if (!ModelLegacyAnimation.IsPlaying(activeClip))
                    {
                        ModelLegacyAnimation.CrossFade(activeClip, 0.15f);
                    }
                    if (isMoving)
                    {
                        ModelLegacyAnimation[activeClip].speed = Mathf.Clamp(currentSpeed / 3.2f, 0.75f, 2.0f);
                    }
                    else
                    {
                        ModelLegacyAnimation[activeClip].speed = 0.35f;
                    }
                }

            }

            // 3. Dao động trọng tâm thân mình chỉ dùng khi KHÔNG có Mecanim animation clip

            bool hasRealMecanim = ModelAnimator != null && ModelAnimator.enabled && ModelAnimator.runtimeAnimatorController != null;

            if (!hasRealMecanim && !hasRealLegacyAnim && !(this is SiegeDino) && _HasBaseModelTransform && VisualModel != null && !_IsAttackingAnim)

            {

                if (isMoving)

                {

                    float bobY = Mathf.Abs(Mathf.Sin(_GaitTimer * 2f)) * 0.06f;

                    float sway = Mathf.Sin(_GaitTimer) * 2.5f;

                    VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, bobY, 0f);

                    VisualModel.localRotation = _BaseModelLocalRot * Quaternion.Euler(0f, 0f, sway);

                }

                else

                {

                    VisualModel.localPosition = Vector3.Lerp(VisualModel.localPosition, _BaseModelLocalPos, Time.deltaTime * 4f);

                    VisualModel.localRotation = Quaternion.Slerp(VisualModel.localRotation, _BaseModelLocalRot, Time.deltaTime * 4f);

                }

            }

        }

        protected virtual void LateUpdate()

        {

            if (VisualModel == null) return;

            // Nếu Mecanim hoặc Legacy animation đang chạy, nhường 100% quyền cho clip chuyển động, tuyệt đối KHÔNG can thiệp xương

            bool hasRealMecanimMotion = ModelAnimator != null 

                && ModelAnimator.enabled 

                && ModelAnimator.runtimeAnimatorController != null;

            bool hasRealLegacyAnim = ModelLegacyAnimation != null 
                && ModelLegacyAnimation.enabled 
                && (ModelLegacyAnimation.isPlaying || ModelLegacyAnimation.GetClipCount() > 0);

            if (hasRealMecanimMotion || hasRealLegacyAnim)

            {

                return;

            }

            bool skipLegs = false;

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

            if (!skipLegs)

            {

                // =========================================================================

                // ĐUÔI UỐN LƯỢN / QUẢ CHÙY ĐUÔI LẮC LƯ THEO NHỊP (Multi-Joint Tail Sway)

                // Chỉ chạy khi mô hình không có animation Mecanim/Legacy native

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

                // Chỉ chạy can thiệp xương giả lập nếu KHÔNG có Mecanim motion

                // =========================================================================

                if (!hasRealMecanimMotion && _AttackAnimProgress >= 0f)

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

            // Tuyệt đối KHÔNG biến cầu sông, mặt đất, terrain, căn cứ trứng, collider phụ mặt sàn thành tường
            if (n.Contains("bridge") || n.Contains("ramp") || n.Contains("smooth floor") ||
                n == "world" || n == "props" ||
                n.Contains("road") || n.Contains("path") || n.Contains("floor") || n.Contains("ground") || n.Contains("terrain") ||
                n.Contains("base") || n.Contains("egg"))
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
                n.Contains("plank_defenses") || n.Contains("tower") || n.Contains("arch") ||
                n.Contains("gate") || n.Contains("fortress") ||
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

                Vector3 closestPoint = blockerCol != null ? GetSafeClosestPoint(blockerCol, myPos) : directFrontBlocker.Transform.position;

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

                public static Vector3 GetSafeClosestPoint(Collider col, Vector3 point)

        {

            if (col == null) return point;

            if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)

            {

                return col.ClosestPoint(point);

            }

            if (col is MeshCollider mc && mc.convex)

            {

                return col.ClosestPoint(point);

            }

            return col.bounds.ClosestPoint(point);

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

                    closest = GetSafeClosestPoint(col, transform.position);

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

                closest = GetSafeClosestPoint(col, myPos);

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

            bool isStructure = (target is Wall) || (target is PrehistoricVillageBase) || (target is ArcherTower) || (target is BallistaTower) || (target is CatapultTower) || (target is TeslaTower) || (target is FrostTower);

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

            bool hasRealMecanim = ModelAnimator != null 

                && ModelAnimator.enabled 

                && ModelAnimator.runtimeAnimatorController != null;

            if (hasAttackClip)

            {

                ModelLegacyAnimation[_AttackClipName].speed = 1.3f;

                ModelLegacyAnimation.CrossFade(_AttackClipName, 0.12f);

                StartCoroutine(AttackClipResetRoutine());

            }

            else if (hasRealMecanim)

            {

                // Khủng long dùng Mecanim Animator chuẩn (T-Rex, Velociraptor):

                // Kích hoạt animation cắn mượt mà, KHÔNG chạy lung lay xương giả lập gây giật lắc

                if (ModelAnimator != null)

                {

                    ModelAnimator.ResetTrigger("Attack");

                    ModelAnimator.ResetTrigger("Bite");

                    ModelAnimator.ResetTrigger("IsAttacking");

                }

                SetAnimatorTriggerIfExists(ModelAnimator, "IsAttacking");

                SetAnimatorTriggerIfExists(ModelAnimator, "Attack");

                SetAnimatorTriggerIfExists(ModelAnimator, "Bite");

                StartCoroutine(MecanimAttackRoutine());

            }

            else

            {

                // Chỉ chạy lung lay giả lập nếu mô hình hoàn toàn không có animation

                StartCoroutine(AttackMotionRoutine());

            }

        }

        protected virtual IEnumerator MecanimAttackRoutine()

        {

            _IsAttackingAnim = true;

            // Khóa trạng thái trong suốt thời gian vung đầu/cắn để không bị gián đoạn hay co giật

            float biteDuration = Mathf.Min(AttackInterval * 0.85f, 0.95f);

            yield return new WaitForSeconds(biteDuration);

            _IsAttackingAnim = false;

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

        // =========================================================================
        // PRIMAL FRENZY AURA (Được kích hoạt bởi Tiếng gầm của Bạo Chúa T-Rex)
        // =========================================================================
        public void ApplyPrimalFrenzy(float duration, float speedBoost)
        {
            StartCoroutine(PrimalFrenzyRoutine(duration, speedBoost));
        }

        private System.Collections.IEnumerator PrimalFrenzyRoutine(float duration, float speedBoost)
        {
            float origSpeed = MoveSpeed;
            MoveSpeed *= speedBoost;
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.speed = MoveSpeed;
            }
            yield return new WaitForSeconds(duration);
            MoveSpeed = origSpeed;
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.speed = MoveSpeed;
            }
        }

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
