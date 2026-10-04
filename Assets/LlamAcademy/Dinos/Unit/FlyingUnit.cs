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
        [SerializeField] private float FlightSpeed = 4.5f;
        [SerializeField] private float TurnSpeed = 5.0f;
        [SerializeField] private int DamageToBase = 20;

        public bool IsAerial => true;

        private Transform TargetBase;
        private Transform VisualModel;
        private Vector3 _BaseModelLocalPos;
        private Quaternion _BaseModelLocalRot;
        private Animation _LegacyAnimation;

        protected override void Awake()
        {
            base.Awake();
            MaxHealth = 720;
            Health = 720;

            Transform modelChild = transform.Find("Model");
            VisualModel = modelChild != null ? modelChild : (transform.childCount > 0 ? transform.GetChild(0) : transform);
            if (VisualModel != null)
            {
                _BaseModelLocalPos = VisualModel.localPosition;
                _BaseModelLocalRot = VisualModel.localRotation;
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

        protected override void Start()
        {
            base.Start();

            if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
            {
                TargetBase = RoundManager.Instance.DinoTarget;
            }

            // Adjust starting altitude
            Vector3 pos = transform.position;
            pos.y += FlightHeight;
            transform.position = pos;

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

        protected override void Update()
        {
            base.Update();

            if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running) return;
            if (TargetBase == null) return;

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
                float flapTime = Time.time * 6.0f;
                float verticalBob = Mathf.Sin(flapTime) * 0.12f;
                float pitchOscillation = Mathf.Cos(flapTime) * 4.0f;
                VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, verticalBob, 0f);
                VisualModel.localRotation = _BaseModelLocalRot * Quaternion.Euler(pitchOscillation, 0f, 0f);
            }

            // Reached base
            if (Vector3.Distance(transform.position, targetDestination) < 3.0f)
            {
                if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.CurrentMode == PrehistoricGameMode.DinoAssault)
                {
                    if (PrehistoricGameModeManager.Instance.VillageBase != null)
                    {
                        PrehistoricGameModeManager.Instance.VillageBase.TakeDamage(DamageToBase);
                    }
                    Die();
                    return;
                }
                else if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.enabled)
                {
                    PrehistoricGameplayManager.Instance.OnDinoReachedBase(this, DamageToBase);
                    return;
                }

                if (TargetBase.TryGetComponent(out IDamageable baseDamageable))
                {
                    baseDamageable.TakeDamage(DamageToBase);
                }
                Die();
            }
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}

        public override void Die()
        {
            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.HandleMonsterDeath(this);
            }
            RaiseDeathEvent();
            Destroy(gameObject);
        }
    }
}
