using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    [RequireComponent(typeof(BoxCollider))]
    public class GroundTrap : Unit
    {
        [Header("Trap Settings")]
        [SerializeField] private int TrapDamage = 40;
        [SerializeField] private float RearmCooldown = 4.0f;
        [SerializeField] private int MaxTriggers = 5;
        [SerializeField] private LayerMask TargetLayers;
        [SerializeField] private ParticleSystem TriggerVFX;
        [SerializeField] private Animator TrapAnimator;

        private bool IsReady = true;
        private int TriggerCount = 0;

        protected override void Awake()
        {
            base.Awake();
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsReady) return;

            if (TargetLayers.value != 0 && (TargetLayers.value & (1 << other.gameObject.layer)) == 0) return;

            // BẢO VỆ ĐỒNG MINH (Defenders, Wall, Player, Base)
            if (!IsHostileMonster(other)) return;

            if (other.TryGetComponent(out IDamageable damageable))
            {
                TriggerTrap(damageable);
            }
        }

        private void TriggerTrap(IDamageable victim)
        {
            IsReady = false;
            TriggerCount++;

            victim.TakeDamage(TrapDamage);

            if (TriggerVFX != null) TriggerVFX.Play();
            if (TrapAnimator != null) TrapAnimator.SetTrigger("Spring");

            if (TriggerCount >= MaxTriggers)
            {
                Die();
            }
            else
            {
                StartCoroutine(RearmRoutine());
            }
        }

        private IEnumerator RearmRoutine()
        {
            yield return new WaitForSeconds(RearmCooldown);
            IsReady = true;
            if (TrapAnimator != null) TrapAnimator.SetTrigger("Rearm");
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}

        public override void Die()
        {
            RaiseDeathEvent();
            Destroy(gameObject, 0.4f);
        }
    }
}
