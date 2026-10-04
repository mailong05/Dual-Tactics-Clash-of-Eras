using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Unit
{
    public class FrostTower : Unit
    {
        [Header("Frost Settings")]
        [SerializeField] private float FrostRadius = 8.0f;
        [SerializeField] private float PulseInterval = 2.0f;
        [SerializeField] private int PulseDamage = 10;
        [SerializeField] [Range(0.1f, 0.9f)] private float SlowPercentage = 0.5f;
        [SerializeField] private float SlowDuration = 3.0f;
        [SerializeField] private LayerMask EnemyLayers;
        [SerializeField] private ParticleSystem FrostPulseVFX;

        private float LastPulseTime;
        private Collider[] OverlapBuffer = new Collider[30];

        protected override void Update()
        {
            base.Update();

            if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running) return;

            if (Time.time >= LastPulseTime + PulseInterval)
            {
                TriggerFrostPulse();
            }
        }

        private void TriggerFrostPulse()
        {
            LastPulseTime = Time.time;

            if (FrostPulseVFX != null)
            {
                FrostPulseVFX.Play();
            }

            int count = Physics.OverlapSphereNonAlloc(transform.position, FrostRadius, OverlapBuffer, EnemyLayers);
            for (int i = 0; i < count; i++)
            {
                if (OverlapBuffer[i] == null) continue;
                if (!IsHostileMonster(OverlapBuffer[i])) continue; // BẢO VỆ ĐỒNG MINH (Defenders, Walls, Player, Base)

                if (OverlapBuffer[i].TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(PulseDamage);
                }

                if (OverlapBuffer[i].TryGetComponent(out NavMeshAgent agent))
                {
                    StartCoroutine(ApplySlowDebuff(agent));
                }
            }
        }

        private IEnumerator ApplySlowDebuff(NavMeshAgent agent)
        {
            if (agent == null || !agent.enabled) yield break;

            float originalSpeed = agent.speed;
            agent.speed = originalSpeed * (1f - SlowPercentage);

            yield return new WaitForSeconds(SlowDuration);

            if (agent != null && agent.enabled)
            {
                agent.speed = originalSpeed;
            }
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}
        public override void Die()
        {
            Destroy(gameObject);
        }
    }
}
