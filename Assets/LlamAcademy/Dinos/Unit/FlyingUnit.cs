using LlamAcademy.Dinos.RoundManagement;
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

            // Reached base
            if (Vector3.Distance(transform.position, targetDestination) < 1.5f)
            {
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
            RaiseDeathEvent();
            Destroy(gameObject);
        }
    }
}
