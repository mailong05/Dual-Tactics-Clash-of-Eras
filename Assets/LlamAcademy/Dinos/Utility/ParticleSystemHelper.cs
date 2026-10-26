using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
using UnityEngine.Pool;

namespace LlamAcademy.Dinos.Utility
{
    public class ParticleSystemHelper : MonoBehaviour
    {
        public static ParticleSystemHelper Instance { get; private set; }
        private Dictionary<AttackTypeSO, ObjectPool<ParticleSystem>> AttackPools = new ();


        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"Multiple Particle System Helpers in the scene! Destroying this one ({name})");
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public ObjectPool<ParticleSystem> GetParticleSystemPoolFor(AttackTypeSO attack)
        {
            if (AttackPools.ContainsKey(attack)) return AttackPools[attack];

            if (attack.AttackParticlePrefab != null)
            {
                AttackPools.Add(attack, new ObjectPool<ParticleSystem>(() => CreateFunc(attack), OnGetParticleSystem));
                return AttackPools[attack];
            }

            return null;
        }


        private ParticleSystem CreateFunc(AttackTypeSO attackType)
        {
            return Instantiate(attackType.AttackParticlePrefab);
        }

        private void OnGetParticleSystem(ParticleSystem particleSystem)
        {
            particleSystem.gameObject.SetActive(true);
            particleSystem.Play(true);
        }

        /// <summary>
        /// Moves the provided particle system to the target position based on the <see cref="attack"/>'s <see cref="AttackTypeSO.MoveToTargetConfig"/>.
        /// This will also attempt to apply <see cref="attack"/>'s effect if the target is IDamageable.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="particleSystem"></param>
        /// <param name="attack"></param>
        public void MoveParticleSystemToTarget(Transform target, ParticleSystem particleSystem, AttackTypeSO attack)
        {
            StartCoroutine(DoMoveParticleSystemToTarget(target, particleSystem, attack));
        }

        private IEnumerator DoMoveParticleSystemToTarget(Transform target, ParticleSystem particleSystem, AttackTypeSO attack)
        {
            AttackTypeSO.MoveToTargetConfig config = attack.AttackParticleMoveConfig;
            float time = 0;
            float durationMultiplier = 1 / Mathf.Max(0.001f, config.MoveToTargetTime);
            Vector3 initialPosition = particleSystem != null ? particleSystem.transform.position : Vector3.zero;
            Vector3 lastKnownTargetPos = target != null ? target.position : initialPosition;
            yield return new WaitForSeconds(config.MoveToTargetDelay);

            while (time <= config.MoveToTargetTime)
            {
                if (particleSystem == null) yield break;

                if (target != null)
                {
                    lastKnownTargetPos = target.position;
                }

                particleSystem.transform.position = Vector3.Lerp(initialPosition, lastKnownTargetPos, time);
                Vector3 toTarget = lastKnownTargetPos - initialPosition;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    particleSystem.transform.forward = toTarget.normalized;
                }
                time += Time.deltaTime * durationMultiplier;
                yield return null;
            }

            if (particleSystem != null)
            {
                particleSystem.transform.position = lastKnownTargetPos;
            }

            if (target != null && target.TryGetComponent(out IDamageable damageable))
            {
                attack.ApplyEffect(damageable);
            }
        }

        public void ReAddSystemToPoolOnDisable(ObjectPool<ParticleSystem> pool, ParticleSystem system)
        {
            StartCoroutine(DoReAddSystemToPoolOnDisable(pool, system));
        }

        private IEnumerator DoReAddSystemToPoolOnDisable(ObjectPool<ParticleSystem> pool, ParticleSystem system)
        {
            yield return null; //allow 1 frame delay for it to get started
            yield return new WaitForSeconds(system.main.duration);
            system.transform.SetParent(null);
            pool.Release(system);
        }
    }
}
