using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Utility;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class FlyingUnit : Unit
    {
        [Header("Flight Settings")]
        [SerializeField] private float FlightHeight = 5.0f;
        [SerializeField] private float FlightSpeed = 7.0f;
        [SerializeField] private float TurnSpeed = 5.0f;
        [SerializeField] private int DamageToBase = 65;

        public bool IsAerial => true;

        private Transform TargetBase;
        private Transform VisualModel;
        private Vector3 _BaseModelLocalPos;
        private Quaternion _BaseModelLocalRot;
        private Animation _LegacyAnimation;
        private bool _hasReachedBase = false;
        public bool HasReachedBase => _hasReachedBase;
        private float _LastAttackTime = 0f;

        protected override void Awake()
        {
            base.Awake();
            OverheadAIStateInspector.AttachTo(gameObject, DinoAISpeciesType.PterodactylFlyer);
            MaxHealth = (UnitType != null && UnitType.Health > 0) ? UnitType.Health : 1450;
            Health = MaxHealth;
            if (GetComponent<Collider>() == null && GetComponentInChildren<Collider>() == null)
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.radius = 1.2f;
                sc.center = Vector3.zero;
            }

            Transform modelChild = transform.Find("Model");
            VisualModel = modelChild != null ? modelChild : (transform.childCount > 0 ? transform.GetChild(0) : transform);
            if (VisualModel != null)
            {
                _BaseModelLocalPos = VisualModel.localPosition;
                _BaseModelLocalRot = VisualModel.localRotation;
                InitializeWingBones();
            }

            _LegacyAnimation = GetComponentInChildren<Animation>(true);
            if (_LegacyAnimation != null)
            {
                _LegacyAnimation.playAutomatically = true;
                foreach (AnimationState state in _LegacyAnimation)
                {
                    state.wrapMode = WrapMode.Loop;
                }
                _LegacyAnimation.Play();
            }
        }

        private Transform _LeftWing;
        private Transform _RightWing;
        private Quaternion _LeftWingBaseRot;
        private Quaternion _RightWingBaseRot;

        private void InitializeWingBones()
        {
            if (VisualModel == null) return;
            Transform[] allChildren = VisualModel.GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                string n = t.name.ToLower();
                if (_LeftWing == null && (n.Contains("lupperarm") || n.Contains("lwing01") || n.Contains("wing.l")))
                {
                    _LeftWing = t;
                    _LeftWingBaseRot = t.localRotation;
                }
                else if (_RightWing == null && (n.Contains("rupperarm") || n.Contains("rwing01") || n.Contains("wing.r")))
                {
                    _RightWing = t;
                    _RightWingBaseRot = t.localRotation;
                }
            }
        }

        protected override void Start()
        {
            base.Start();

            FindTargetBase();

            // Adjust starting altitude
            Vector3 pos = transform.position;
            pos.y += FlightHeight;
            transform.position = pos;

            EnsureHealthBarAttached();
        }

        public void FindTargetBase()
        {
            if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
            {
                TargetBase = RoundManager.Instance.DinoTarget;
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

        public void SetTargetBase(Transform t)
        {
            TargetBase = t;
        }

        public void SetDestination(Vector3 destination)
        {
            if (TargetBase == null)
            {
                GameObject destObj = new GameObject("FlyingTargetDestination");
                destObj.transform.position = destination;
                TargetBase = destObj.transform;
            }
            else
            {
                TargetBase.position = destination;
            }
        }

        public override void EnsureHealthBarAttached()
        {
            base.EnsureHealthBarAttached();
        }

        protected override void Update()
        {
            base.Update();

            bool isDefenseCombat = PrehistoricGameplayManager.Instance != null 
                && PrehistoricGameplayManager.Instance.enabled 
                && PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.CombatPhase;

            bool isAssaultCombat = PrehistoricGameModeManager.Instance != null 
                && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault 
                && PrehistoricGameModeManager.Instance.IsAssaultActive;

            if (!isDefenseCombat && !isAssaultCombat)
            {
                if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running) return;
            }

            if (TargetBase == null)
            {
                FindTargetBase();
                if (TargetBase == null) return;
            }

            Vector3 targetDestination = TargetBase.position + Vector3.up * FlightHeight;
            Vector3 direction = (targetDestination - transform.position).normalized;

            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * TurnSpeed);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetDestination, FlightSpeed * Time.deltaTime);

            // Flight undulating motion & wing-beat banking
            if (VisualModel != null)
            {
                bool hasAnim = _LegacyAnimation != null && _LegacyAnimation.enabled && _LegacyAnimation.isPlaying;
                if (!hasAnim)
                {
                    float flapTime = Time.time * 6.0f;
                    float verticalBob = Mathf.Sin(flapTime) * 0.12f;
                    float pitchOscillation = Mathf.Cos(flapTime) * 4.0f;
                    VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, verticalBob, 0f);
                    VisualModel.localRotation = _BaseModelLocalRot * Quaternion.Euler(pitchOscillation, 0f, 0f);

                    if (_LeftWing != null)
                    {
                        float wingFlap = Mathf.Sin(flapTime) * 25.0f;
                        _LeftWing.localRotation = _LeftWingBaseRot * Quaternion.Euler(0f, 0f, wingFlap);
                    }
                    if (_RightWing != null)
                    {
                        float wingFlap = Mathf.Sin(flapTime) * 25.0f;
                        _RightWing.localRotation = _RightWingBaseRot * Quaternion.Euler(0f, 0f, -wingFlap);
                    }
                }
            }

            // Reached base: hover and continuously attack
            if (Vector3.Distance(transform.position, targetDestination) < 4.0f)
            {
                _hasReachedBase = true;

                // Quay mặt về phía căn cứ
                Vector3 look = (targetDestination - transform.position);
                look.y = 0;
                if (look.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 6f);
                }

                // Giữ cự ly lượn trên không trung căn cứ
                transform.position = Vector3.Lerp(transform.position, targetDestination + Vector3.up * 2.8f, Time.deltaTime * 2.5f);

                // Tấn công căn cứ theo chu kỳ
                if (Time.time >= _LastAttackTime + 1.0f)
                {
                    _LastAttackTime = Time.time;
                    if (PrehistoricVillageBase.Instance != null && PrehistoricVillageBase.Instance.Health > 0)
                    {
                        PrehistoricVillageBase.Instance.TakeDamage(DamageToBase);
                    }
                    else if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.enabled)
                    {
                        PrehistoricGameplayManager.Instance.DamageBase(DamageToBase, this);
                    }
                    else if (TargetBase != null && TargetBase.TryGetComponent(out IDamageable baseDamageable))
                    {
                        baseDamageable.TakeDamage(DamageToBase);
                    }
                }
                return;
            }
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}

        public override void Die()
        {
            Collider[] allCols = GetComponentsInChildren<Collider>();
            foreach (var c in allCols) if (c != null) c.enabled = false;

            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.HandleMonsterDeath(this);
            }
            RaiseDeathEvent();
            Destroy(gameObject);
        }
    }
}
