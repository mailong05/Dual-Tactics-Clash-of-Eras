using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class BossDino : PrehistoricDinoBase
    {
        [Header("Boss Apex Settings")]
        [SerializeField] private float RoarRadius = 15.0f;
        [SerializeField] private float RoarStunDuration = 3.5f;
        [SerializeField] private LayerMask TowerLayers;
        [SerializeField] private ParticleSystem RoarVFX;

        private HashSet<int> TriggeredThresholds = new();

        protected override void Awake()
        {
            base.Awake();
            MaxHealth = 10000;
            Health = 10000;
            MoveSpeed = 2.6f;
            AttackDamage = 65;
            BuildingDamageMultiplier = 2.5f;
            GoldReward = 150;
        }

        public override void TakeDamage(int damage)
        {
            base.TakeDamage(damage);

            float hpPercent = (float)Health / MaxHealth;

            if (hpPercent <= 0.75f && !TriggeredThresholds.Contains(75))
            {
                TriggeredThresholds.Add(75);
                TriggerBossRoar();
            }
            else if (hpPercent <= 0.50f && !TriggeredThresholds.Contains(50))
            {
                TriggeredThresholds.Add(50);
                TriggerBossRoar();
            }
            else if (hpPercent <= 0.25f && !TriggeredThresholds.Contains(25))
            {
                TriggeredThresholds.Add(25);
                TriggerBossRoar();
            }
        }

        private void TriggerBossRoar()
        {
            Debug.Log($"<color=red>[BOSS T-REX]</color> Roars fiercely! Nearby defenses stunned for {RoarStunDuration}s!");

            if (RoarVFX != null) RoarVFX.Play();

            Collider[] nearbyTowers = Physics.OverlapSphere(transform.position, RoarRadius, TowerLayers);
            foreach (Collider col in nearbyTowers)
            {
                if (col.TryGetComponent(out Unit towerUnit) && !(towerUnit is PrehistoricDinoBase))
                {
                    StartCoroutine(StunTowerRoutine(towerUnit));
                }
            }
        }

        private IEnumerator StunTowerRoutine(Unit tower)
        {
            if (tower == null) yield break;

            tower.enabled = false;
            yield return new WaitForSeconds(RoarStunDuration);
            if (tower != null)
            {
                tower.enabled = true;
            }
        }
    }
}
