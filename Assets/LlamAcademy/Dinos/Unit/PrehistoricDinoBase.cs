using System.Collections;
using System.Collections.Generic;
using System.IO;
using GLTFast;
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

        protected Transform TargetBase;
        protected float LastAttackTime;
        protected IDamageable CurrentTarget;
        protected Collider[] ScanBuffer = new Collider[15];

        // GLB Animation Cache & Clips
        private static readonly Dictionary<string, AnimationClip[]> s_ClipsCache = new();
        private string _WalkClipName;
        private string _AttackClipName;

        // Skeletal Bone Rig
        private Transform _LeftLegBone;
        private Transform _RightLegBone;
        private Quaternion _LeftLegBaseRot;
        private Quaternion _RightLegBaseRot;

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
        private static readonly HashSet<PrehistoricDinoBase> s_ActiveDinos = new();

        protected virtual void OnEnable()
        {
            s_ActiveDinos.Add(this);
            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated += HandleNavMeshUpdated;
            }
        }

        protected virtual void OnDisable()
        {
            s_ActiveDinos.Remove(this);
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
            foreach (var dino in s_ActiveDinos)
            {
                if (dino == null || !dino.enabled || dino.Health <= 0) continue;
                if (dino.CurrentTarget == breachedWall)
                {
                    dino.CurrentTarget = null;
                }
                if (dino.Agent != null && dino.Agent.enabled && dino.Agent.isOnNavMesh && dino.TargetBase != null)
                {
                    dino.Agent.isStopped = false;
                    dino.Agent.ResetPath();
                    dino.Agent.SetDestination(dino.TargetBase.position);
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
            if (Agent != null)
            {
                Agent.speed = MoveSpeed;
                Agent.autoRepath = true;
                Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }

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
            foreach (Transform t in allBones)
            {
                if (t == VisualModel) continue;
                string n = t.name.ToLower();

                // Left Leg / Thigh / BackLeg
                if (_LeftLegBone == null && (n.Contains("leg.001.l") || n.Contains("leftupleg") || n.Contains("backleg.l") || n.Contains("backupleg.l") || n.Contains("leg.l") || n.Contains("thigh.l") || n.Contains("upleg.l")))
                {
                    _LeftLegBone = t;
                    _LeftLegBaseRot = t.localRotation;
                }
                // Right Leg / Thigh / BackLeg
                else if (_RightLegBone == null && (n.Contains("leg.001.r") || n.Contains("rightupleg") || n.Contains("backleg.r") || n.Contains("backupleg.r") || n.Contains("leg.r") || n.Contains("thigh.r") || n.Contains("upleg.r")))
                {
                    _RightLegBone = t;
                    _RightLegBaseRot = t.localRotation;
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
                // Tail segments
                else if (n.Contains("tail") && _TailBones.Count < 8)
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
                        if (_WalkClipName == null && (cn.Contains("walk") || cn.Contains("run") || cn.Contains("anim") || cn.Contains("fly")))
                            _WalkClipName = clip.name;
                        if (_AttackClipName == null && (cn.Contains("roar") || cn.Contains("eat") || cn.Contains("attack") || cn.Contains("bite")))
                            _AttackClipName = clip.name;
                    }
                    catch {}
                }

                if (_WalkClipName != null && ModelLegacyAnimation[_WalkClipName] != null)
                {
                    ModelLegacyAnimation.clip = ModelLegacyAnimation[_WalkClipName].clip;
                    ModelLegacyAnimation.Play(_WalkClipName);
                }
                else if (validClips.Count > 0 && validClips[0] != null)
                {
                    try
                    {
                        _WalkClipName = validClips[0].name;
                        ModelLegacyAnimation.clip = validClips[0];
                        ModelLegacyAnimation.Play(_WalkClipName);
                    }
                    catch {}
                }
            }
        }

        protected override void Start()
        {
            base.Start();

            if (TargetLayers.value == 0) TargetLayers = ~LayerMask.GetMask("Ignore Raycast");

            if (Agent != null)
            {
                Agent.speed = MoveSpeed;
                Agent.stoppingDistance = AttackRange * 0.8f;
            }

            FindTargetBase();

            if (TargetBase != null && Agent != null && Agent.isOnNavMesh)
            {
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

            if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running)
            {
                bool isAssaultCombat = PrehistoricGameModeManager.Instance != null 
                    && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault 
                    && PrehistoricGameModeManager.Instance.IsAssaultActive;

                if (!isAssaultCombat)
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

            // INTELLIGENT BREACH BEHAVIOR:
            // If the current target is a Wall, but our path to TargetBase is COMPLETE (a breach has opened!),
            // check if we can navigate through the hole without needing to finish destroying this wall!
            if (hasValidTarget && CurrentTarget is Wall)
            {
                if (Agent != null && Agent.enabled && Agent.isOnNavMesh && Agent.hasPath && Agent.pathStatus == NavMeshPathStatus.PathComplete)
                {
                    Collider wallCol = CurrentTarget.Transform.GetComponentInChildren<Collider>();
                    Vector3 wallPos = wallCol != null ? wallCol.bounds.center : CurrentTarget.Transform.position;
                    Vector3 toWall = (wallPos - transform.position);
                    toWall.y = 0;
                    float dist = toWall.magnitude;
                    float forwardAlignment = dist > 0.01f ? Vector3.Dot(transform.forward, toWall.normalized) : 0f;

                    // Chỉ từ bỏ tường này nếu hướng đi tiếp theo rẽ sang chỗ khác để chui qua lỗ thủng đã vỡ
                    Vector3 steerDir = (Agent.steeringTarget - transform.position);
                    steerDir.y = 0;
                    float steerAwayDot = steerDir.sqrMagnitude > 0.01f ? Vector3.Dot(steerDir.normalized, toWall.normalized) : 1f;

                    if (steerAwayDot < 0.5f || forwardAlignment < 0.3f)
                    {
                        CurrentTarget = null;
                        hasValidTarget = false;
                        Agent.isStopped = false;
                        Agent.SetDestination(TargetBase.position);
                    }
                }
            }

            if (hasValidTarget)
            {
                Collider targetCol = CurrentTarget.Transform.GetComponentInChildren<Collider>();
                Vector3 targetCenter = targetCol != null ? targetCol.bounds.center : CurrentTarget.Transform.position;
                Vector3 closestPoint = targetCol != null ? targetCol.ClosestPoint(transform.position) : targetCenter;
                float distToTarget = Vector3.Distance(transform.position, closestPoint);

                if (distToTarget <= AttackRange + 0.5f)
                {
                    if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;

                    // Face target
                    Vector3 look = (targetCenter - transform.position);
                    look.y = 0;
                    if (look != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 6f);

                    if (Time.time >= LastAttackTime + AttackInterval)
                    {
                        LastAttackTime = Time.time;
                        PerformAttack(CurrentTarget);
                    }
                    return;
                }
                else
                {
                    // Tiến đến gần bức tường/mục tiêu để tấn công
                    if (Agent != null && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
                        Agent.SetDestination(targetCenter);
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

                // Check if reached base
                float distToBase = Vector3.Distance(transform.position, TargetBase.position);
                if (distToBase <= AttackRange + 1.5f)
                {
                    if (_hasReachedBase) return;
                    _hasReachedBase = true;
                    enabled = false;
                    if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;

                    if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
                    {
                        if (PrehistoricGameModeManager.Instance.VillageBase != null && PrehistoricGameModeManager.Instance.VillageBase.Health > 0)
                        {
                            CurrentTarget = PrehistoricGameModeManager.Instance.VillageBase;
                            return;
                        }
                    }
                    else if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.enabled)
                    {
                        PrehistoricGameplayManager.Instance.OnDinoReachedBase(this, AttackDamage);
                        return;
                    }

                    if (TargetBase.TryGetComponent(out IDamageable baseDamageable))
                    {
                        CurrentTarget = baseDamageable;
                    }
                    return;
                }
            }
        }

        protected virtual void UpdateAnimation(float currentSpeed)
        {
            _CurrentSpeed = currentSpeed;
            bool isMoving = currentSpeed > 0.15f;

            // 1. Mecanim Animator Parameters
            if (ModelAnimator != null)
            {
                SetAnimatorFloatIfExists(ModelAnimator, "Speed", currentSpeed);
                SetAnimatorFloatIfExists(ModelAnimator, "Forward", currentSpeed);
                SetAnimatorBoolIfExists(ModelAnimator, "IsMoving", isMoving);
                SetAnimatorBoolIfExists(ModelAnimator, "Walk", isMoving);
                SetAnimatorBoolIfExists(ModelAnimator, "Run", currentSpeed > 3.0f);
            }

            // 2. Legacy Animation Clip Playback
            if (ModelLegacyAnimation != null)
            {
                string activeClip = _WalkClipName;
                if (string.IsNullOrEmpty(activeClip) && ModelLegacyAnimation.clip != null)
                {
                    activeClip = ModelLegacyAnimation.clip.name;
                }

                if (!string.IsNullOrEmpty(activeClip) && ModelLegacyAnimation[activeClip] != null)
                {
                    if (isMoving)
                    {
                        if (!ModelLegacyAnimation.isPlaying || !ModelLegacyAnimation.IsPlaying(activeClip))
                        {
                            ModelLegacyAnimation.CrossFade(activeClip, 0.2f);
                        }
                        float animSpeed = Mathf.Clamp(currentSpeed / Mathf.Max(MoveSpeed * 0.7f, 1f), 0.6f, 1.8f);
                        ModelLegacyAnimation[activeClip].speed = animSpeed;
                    }
                    else
                    {
                        ModelLegacyAnimation[activeClip].speed = 0.2f;
                    }
                }
            }

            // 3. Root Visual Stride Bob & Rolling Sway
            if (_HasBaseModelTransform && VisualModel != null && !_IsAttackingAnim)
            {
                if (isMoving)
                {
                    float strideRate = Mathf.Max(currentSpeed * 2.5f, 2.5f);
                    _GaitTimer += Time.deltaTime * strideRate;

                    float bobY = Mathf.Abs(Mathf.Sin(_GaitTimer * 2f)) * 0.08f;
                    float swayRoll = Mathf.Sin(_GaitTimer) * 3.5f;
                    float pitch = Mathf.Cos(_GaitTimer * 2f) * 1.5f;

                    VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, bobY, 0f);
                    VisualModel.localRotation = _BaseModelLocalRot * Quaternion.Euler(pitch, 0f, swayRoll);
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

            bool isMoving = _CurrentSpeed > 0.15f;

            // 1. Chân bước đi / chạy nhịp nhàng (Alternating Leg Swings)
            if (isMoving)
            {
                float legSwing = Mathf.Sin(_GaitTimer) * 26f;
                if (_LeftLegBone != null)
                {
                    _LeftLegBone.localRotation = _LeftLegBaseRot * Quaternion.Euler(legSwing, 0f, 0f);
                }
                if (_RightLegBone != null)
                {
                    _RightLegBone.localRotation = _RightLegBaseRot * Quaternion.Euler(-legSwing, 0f, 0f);
                }
            }
            else
            {
                if (_LeftLegBone != null)
                {
                    _LeftLegBone.localRotation = Quaternion.Slerp(_LeftLegBone.localRotation, _LeftLegBaseRot, Time.deltaTime * 6f);
                }
                if (_RightLegBone != null)
                {
                    _RightLegBone.localRotation = Quaternion.Slerp(_RightLegBone.localRotation, _RightLegBaseRot, Time.deltaTime * 6f);
                }
            }

            // 2. Đuôi uốn lượn hình sin mềm mại theo nhịp di chuyển (Multi-Joint Tail Sway)
            for (int i = 0; i < _TailBones.Count; i++)
            {
                if (_TailBones[i] == null) continue;
                float phase = isMoving ? (_GaitTimer - (i * 0.45f)) : (Time.time * 2.2f - (i * 0.35f));
                float amp = isMoving ? 6.5f : 3.0f;
                float sway = Mathf.Sin(phase) * amp;
                _TailBones[i].localRotation = _TailBaseRots[i] * Quaternion.Euler(0f, sway, 0f);
            }

            // 3. Đầu & Cổ lắc lư theo nhịp bước chân (Head & Neck Rhythm)
            if (isMoving)
            {
                float headBob = Mathf.Cos(_GaitTimer * 2f) * 3.5f;
                if (_HeadBone != null)
                {
                    _HeadBone.localRotation = _HeadBaseRot * Quaternion.Euler(headBob, 0f, 0f);
                }
                if (_NeckBone != null)
                {
                    _NeckBone.localRotation = _NeckBaseRot * Quaternion.Euler(-headBob * 0.5f, 0f, 0f);
                }
            }

            // 4. Hàm há to & cắn mạnh khi tấn công (Attack Jaw Bite Animation)
            if (_AttackAnimProgress >= 0f)
            {
                float jawAngle = Mathf.Sin(_AttackAnimProgress * Mathf.PI) * 32f;
                if (_JawBone != null)
                {
                    _JawBone.localRotation = _JawBaseRot * Quaternion.Euler(jawAngle, 0f, 0f);
                }
                if (_HeadBone != null)
                {
                    _HeadBone.localRotation = _HeadBaseRot * Quaternion.Euler(jawAngle * 0.4f, 0f, 0f);
                }
            }
        }

        protected virtual IDamageable ScanForBlockers()
        {
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh) return null;

            Vector3 fwdDir = transform.forward;
            if (Agent.hasPath && Agent.velocity.sqrMagnitude > 0.1f)
            {
                fwdDir = Agent.velocity.normalized;
            }
            fwdDir.y = 0;
            if (fwdDir == Vector3.zero) fwdDir = transform.forward;
            fwdDir.Normalize();

            // 1. Tia quét trực diện tầm ngực phía trước (SphereCast forward check)
            // Phát hiện tường chắn ngay hướng di chuyển
            Vector3 chestPos = transform.position + Vector3.up * 0.8f;
            if (Physics.SphereCast(chestPos, 0.4f, fwdDir, out RaycastHit forwardHit, AttackRange + 0.8f, TargetLayers))
            {
                Wall hitWall = forwardHit.collider.GetComponentInParent<Wall>();
                if (hitWall != null && hitWall.Health > 0)
                {
                    return hitWall;
                }

                Unit hitDefender = forwardHit.collider.GetComponentInParent<Unit>();
                if (hitDefender != null && !(hitDefender is PrehistoricDinoBase) && hitDefender.Health > 0)
                {
                    return hitDefender;
                }
            }

            // 2. Quét vùng chạm / xuyên thấu (Overlap check)
            float scanRadius = Mathf.Max(AttackRange + 0.8f, 3.2f);
            int hits = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, ScanBuffer, TargetLayers);
            IDamageable directForwardObstacle = null;
            float bestForwardAlignment = 0.35f;

            for (int i = 0; i < hits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                Wall wall = col.GetComponentInParent<Wall>();
                if (wall != null && wall.Health > 0)
                {
                    // Nếu khủng long đang chạm sát hoặc bị đẩy vào trong bounds của tường
                    if (col.bounds.Contains(transform.position) || col.bounds.SqrDistance(transform.position) < 0.15f)
                    {
                        // Ngăn chặn tuyệt đối khủng long đi xuyên qua
                        Vector3 pushBack = (transform.position - col.bounds.center);
                        pushBack.y = 0;
                        if (pushBack.sqrMagnitude > 0.001f)
                        {
                            transform.position += pushBack.normalized * 0.08f;
                        }
                        return wall;
                    }

                    Vector3 closestPt = col.ClosestPoint(transform.position);
                    Vector3 flatToWall = new Vector3(closestPt.x - transform.position.x, 0f, closestPt.z - transform.position.z);
                    float flatDist = flatToWall.magnitude;
                    if (flatDist <= AttackRange + 0.6f)
                    {
                        Vector3 flatDir = flatDist > 0.01f ? (flatToWall / flatDist) : fwdDir;
                        float forwardDot = Vector3.Dot(fwdDir, flatDir);
                        if (forwardDot > bestForwardAlignment)
                        {
                            directForwardObstacle = wall;
                            bestForwardAlignment = forwardDot;
                        }
                    }
                    continue;
                }

                Unit tower = col.GetComponentInParent<Unit>();
                if (tower != null && !(tower is PrehistoricDinoBase) && tower.Health > 0)
                {
                    Vector3 closestPt = col.ClosestPoint(transform.position);
                    Vector3 flatToTower = new Vector3(closestPt.x - transform.position.x, 0f, closestPt.z - transform.position.z);
                    float flatDist = flatToTower.magnitude;
                    if (flatDist <= AttackRange + 0.6f)
                    {
                        Vector3 flatDir = flatDist > 0.01f ? (flatToTower / flatDist) : fwdDir;
                        float forwardDot = Vector3.Dot(fwdDir, flatDir);
                        if (forwardDot > bestForwardAlignment)
                        {
                            directForwardObstacle = tower;
                            bestForwardAlignment = forwardDot;
                        }
                    }
                }
            }

            if (directForwardObstacle != null)
            {
                return directForwardObstacle;
            }

            // 3. Nếu không có bức tường nào chặn trực diện ngay trước mặt:
            // Nếu NavMesh đã có đường đi HOÀN TOÀN (PathComplete) qua một lỗ thủng đã vỡ,
            // khủng long sẽ men theo NavMesh đi qua lỗ thủng đó mà KHÔNG tấn công các bức tường xung quanh!
            bool pathIsComplete = (Agent.hasPath && Agent.pathStatus == NavMeshPathStatus.PathComplete);
            if (pathIsComplete)
            {
                return null;
            }

            // 4. Khi chưa có lỗ thủng nào bị phá (PathPartial hoặc PathInvalid):
            // Toàn bộ đường vào làng bị phong tỏa 100%, khủng long bắt buộc phải tấn công bức tường gần nhất chắn đường vào làng
            Vector3 targetDir = TargetBase != null ? (TargetBase.position - transform.position).normalized : transform.forward;
            targetDir.y = 0;
            float wideScanRadius = Mathf.Max(AttackRange + 2.0f, 4.5f);
            int blockHits = Physics.OverlapSphereNonAlloc(transform.position, wideScanRadius, ScanBuffer, TargetLayers);

            IDamageable closestBlocker = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < blockHits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                Wall wall = col.GetComponentInParent<Wall>();
                if (wall != null && wall.Health > 0)
                {
                    Vector3 closest = col.ClosestPoint(transform.position);
                    Vector3 flatToWall = new Vector3(closest.x - transform.position.x, 0f, closest.z - transform.position.z);
                    if (Vector3.Dot(targetDir, flatToWall.normalized) > -0.2f)
                    {
                        float d = flatToWall.magnitude;
                        if (d < minDistance)
                        {
                            minDistance = d;
                            closestBlocker = wall;
                        }
                    }
                    continue;
                }

                Unit tower = col.GetComponentInParent<Unit>();
                if (tower != null && !(tower is PrehistoricDinoBase) && tower.Health > 0)
                {
                    Vector3 closest = col.ClosestPoint(transform.position);
                    float d = Vector3.Distance(transform.position, closest);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        closestBlocker = tower;
                    }
                }
            }

            return closestBlocker;
        }

        protected virtual void PerformAttack(IDamageable target)
        {
            int damage = target is Wall ? Mathf.CeilToInt(AttackDamage * BuildingDamageMultiplier) : AttackDamage;
            target.TakeDamage(damage);

            if (ModelAnimator != null)
            {
                SetAnimatorTriggerIfExists(ModelAnimator, "IsAttacking");
                SetAnimatorTriggerIfExists(ModelAnimator, "Attack");
                SetAnimatorTriggerIfExists(ModelAnimator, "Bite");
            }

            if (ModelLegacyAnimation != null && !string.IsNullOrEmpty(_AttackClipName) && ModelLegacyAnimation[_AttackClipName] != null)
            {
                ModelLegacyAnimation.CrossFade(_AttackClipName, 0.15f);
            }

            StartCoroutine(AttackMotionRoutine());
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
