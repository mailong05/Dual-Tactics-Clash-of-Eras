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

        protected override void Awake()
        {
            base.Awake();
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();

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

        private async void TryLoadGlbAnimations()
        {
            string glbName = GetModelGlbFileName();
            if (s_ClipsCache.TryGetValue(glbName, out var cachedClips))
            {
                ApplyAnimationClips(cachedClips);
                return;
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
                    var clips = gltf.GetAnimationClips();
                    if (clips != null && clips.Length > 0)
                    {
                        s_ClipsCache[glbName] = clips;
                        ApplyAnimationClips(clips);
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
                foreach (var clip in clips)
                {
                    if (clip == null) continue;
                    clip.legacy = true;
                    clip.wrapMode = WrapMode.Loop;
                    if (ModelLegacyAnimation[clip.name] == null)
                    {
                        ModelLegacyAnimation.AddClip(clip, clip.name);
                    }
                }

                foreach (var clip in clips)
                {
                    string cn = clip.name.ToLower();
                    if (_WalkClipName == null && (cn.Contains("walk") || cn.Contains("run") || cn.Contains("anim") || cn.Contains("fly")))
                        _WalkClipName = clip.name;
                    if (_AttackClipName == null && (cn.Contains("roar") || cn.Contains("eat") || cn.Contains("attack") || cn.Contains("bite")))
                        _AttackClipName = clip.name;
                }

                if (_WalkClipName != null && ModelLegacyAnimation[_WalkClipName] != null)
                {
                    ModelLegacyAnimation.clip = ModelLegacyAnimation[_WalkClipName].clip;
                    ModelLegacyAnimation.Play(_WalkClipName);
                }
                else if (clips.Length > 0)
                {
                    _WalkClipName = clips[0].name;
                    ModelLegacyAnimation.clip = clips[0];
                    ModelLegacyAnimation.Play(_WalkClipName);
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
            if (CurrentTarget != null && CurrentTarget.Health > 0)
            {
                float distToTarget = Vector3.Distance(transform.position, CurrentTarget.Transform.position);
                if (distToTarget <= AttackRange + 0.5f)
                {
                    if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;

                    // Face target
                    Vector3 look = (CurrentTarget.Transform.position - transform.position);
                    look.y = 0;
                    if (look != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 6f);

                    if (Time.time >= LastAttackTime + AttackInterval)
                    {
                        LastAttackTime = Time.time;
                        PerformAttack(CurrentTarget);
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
                    Agent.SetDestination(TargetBase.position);
                }

                // Check if reached base
                float distToBase = Vector3.Distance(transform.position, TargetBase.position);
                if (distToBase <= AttackRange + 1.5f)
                {
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
            int hits = Physics.OverlapSphereNonAlloc(transform.position, AttackRange, ScanBuffer, TargetLayers);
            for (int i = 0; i < hits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                if (col.TryGetComponent(out Wall wall) && wall.Health > 0)
                {
                    return wall;
                }
                if (col.TryGetComponent(out Unit tower) && !(tower is PrehistoricDinoBase) && tower.Health > 0)
                {
                    return tower;
                }
            }
            return null;
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
