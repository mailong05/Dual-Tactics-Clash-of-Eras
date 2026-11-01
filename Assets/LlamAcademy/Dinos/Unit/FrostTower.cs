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
        private Collider[] OverlapBuffer = new Collider[64];

        protected override void Awake()
        {
            base.Awake();
            if (EnemyLayers.value == 0)
            {
                EnemyLayers = LayerMask.GetMask("Dinos", "Default");
            }
        }

        protected override void Start()
        {
            base.Start();
            FrostRadius = Mathf.Max(FrostRadius, 14.0f);

            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                Material tarMat = Rendering.PrehistoricShaderUtility.CreateTarPitMaterial();
                if (tarMat != null)
                {
                    rend.sharedMaterial = tarMat;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!IsCombatActive()) return;

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

            var affectedAgents = new HashSet<NavMeshAgent>();

            // 1. Quét ActiveDinos trực tiếp trong phạm vi hố băng/nhựa đường
            if (PrehistoricDinoBase.ActiveDinos != null)
            {
                foreach (var dino in PrehistoricDinoBase.ActiveDinos)
                {
                    if (dino == null || dino.Health <= 0 || !dino.gameObject.activeInHierarchy) continue;
                    if (Vector3.Distance(transform.position, dino.transform.position) <= FrostRadius)
                    {
                        dino.TakeDamage(PulseDamage);
                        if (dino.Agent != null && dino.Agent.enabled && !affectedAgents.Contains(dino.Agent))
                        {
                            affectedAgents.Add(dino.Agent);
                            StartCoroutine(ApplySlowDebuff(dino.Agent));
                        }
                    }
                }
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
                    if (!affectedAgents.Contains(agent))
                    {
                        affectedAgents.Add(agent);
                        StartCoroutine(ApplySlowDebuff(agent));
                    }
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
