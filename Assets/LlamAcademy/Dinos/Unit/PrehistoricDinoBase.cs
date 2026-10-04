using System.Collections;
using System.Collections.Generic;
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

        // Procedural Locomotion State
        private float _StrideTimer;
        private Vector3 _BaseModelLocalPos;
        private Quaternion _BaseModelLocalRot;
        private bool _HasBaseModelTransform;
        private bool _IsAttackingAnim;

        protected override void Awake()
        {
            base.Awake();
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();

            InitializeVisualAndAnimation();
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

        protected virtual void EnsureHealthBarAttached()
        {
            if (HealthBar == null)
            {
                HealthBar = GetComponentInChildren<HealthBar>();
            }

            if (HealthBar == null && HealthBarCanvas.Instance != null)
            {
                HealthBar = HealthBarCanvas.Instance.CreateHealthBarForUnit(this);
            }
            else if (HealthBar != null && HealthBarCanvas.Instance != null)
            {
                HealthBarCanvas.Instance.Register(HealthBar, this);
            }
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
            bool isMoving = currentSpeed > 0.15f;

            // 1. Mecanim Animator
            if (ModelAnimator != null)
            {
                SetAnimatorFloatIfExists(ModelAnimator, "Speed", currentSpeed);
                SetAnimatorFloatIfExists(ModelAnimator, "Forward", currentSpeed);
                SetAnimatorBoolIfExists(ModelAnimator, "IsMoving", isMoving);
                SetAnimatorBoolIfExists(ModelAnimator, "Walk", isMoving);
                SetAnimatorBoolIfExists(ModelAnimator, "Run", currentSpeed > 3.0f);
            }

            // 2. Legacy Animation
            if (ModelLegacyAnimation != null && ModelLegacyAnimation.clip != null)
            {
                string clipName = ModelLegacyAnimation.clip.name;
                if (ModelLegacyAnimation[clipName] != null)
                {
                    if (isMoving)
                    {
                        if (!ModelLegacyAnimation.isPlaying) ModelLegacyAnimation.Play(clipName);
                        float animSpeed = Mathf.Clamp(currentSpeed / Mathf.Max(MoveSpeed, 1f), 0.7f, 2.0f);
                        ModelLegacyAnimation[clipName].speed = animSpeed;
                    }
                    else
                    {
                        ModelLegacyAnimation[clipName].speed = 0.2f;
                    }
                }
            }

            // 3. Procedural Gait (Step bobbing, body sway, breathing)
            if (_HasBaseModelTransform && VisualModel != null && !_IsAttackingAnim)
            {
                if (isMoving)
                {
                    float strideRate = Mathf.Max(currentSpeed * 2.2f, 2.5f);
                    _StrideTimer += Time.deltaTime * strideRate;

                    float bobY = Mathf.Abs(Mathf.Sin(_StrideTimer * 2f)) * 0.06f;
                    float swayRoll = Mathf.Sin(_StrideTimer) * 3.5f;
                    float pitch = Mathf.Cos(_StrideTimer * 2f) * 1.5f;

                    VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, bobY, 0f);
                    VisualModel.localRotation = _BaseModelLocalRot * Quaternion.Euler(pitch, 0f, swayRoll);
                }
                else
                {
                    float breathe = Mathf.Sin(Time.time * 2.0f) * 0.015f;
                    VisualModel.localPosition = Vector3.Lerp(VisualModel.localPosition, _BaseModelLocalPos + new Vector3(0f, breathe, 0f), Time.deltaTime * 4f);
                    VisualModel.localRotation = Quaternion.Slerp(VisualModel.localRotation, _BaseModelLocalRot, Time.deltaTime * 4f);
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

            StartCoroutine(AttackMotionRoutine());
        }

        protected virtual IEnumerator AttackMotionRoutine()
        {
            _IsAttackingAnim = true;
            Vector3 origPos = _HasBaseModelTransform && VisualModel != null ? _BaseModelLocalPos : Vector3.zero;
            Quaternion origRot = _HasBaseModelTransform && VisualModel != null ? _BaseModelLocalRot : Quaternion.identity;

            // Wind up
            float t = 0f;
            while (t < 0.12f && VisualModel != null)
            {
                t += Time.deltaTime;
                float p = t / 0.12f;
                VisualModel.localPosition = origPos - new Vector3(0f, -0.05f, 0.15f) * p;
                VisualModel.localRotation = origRot * Quaternion.Euler(-8f * p, 0f, 0f);
                yield return null;
            }

            // Snap forward
            t = 0f;
            while (t < 0.12f && VisualModel != null)
            {
                t += Time.deltaTime;
                float p = t / 0.12f;
                VisualModel.localPosition = origPos + new Vector3(0f, -0.05f, 0.45f) * p;
                VisualModel.localRotation = origRot * Quaternion.Euler(12f * p, 0f, 0f);
                yield return null;
            }

            // Return to stance
            t = 0f;
            while (t < 0.2f && VisualModel != null)
            {
                t += Time.deltaTime;
                float p = t / 0.2f;
                VisualModel.localPosition = Vector3.Lerp(origPos + new Vector3(0f, -0.05f, 0.45f), origPos, p);
                VisualModel.localRotation = Quaternion.Slerp(origRot * Quaternion.Euler(12f, 0f, 0f), origRot, p);
                yield return null;
            }

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
