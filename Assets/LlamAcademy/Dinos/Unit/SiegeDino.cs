using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class SiegeDino : PrehistoricDinoBase
    {
        [Header("Siege Breaker Specialization")]
        [SerializeField] private float WallDetectRadius = 4.5f;

        protected override void Awake()
        {
            base.Awake();
            BuildingDamageMultiplier = 3.5f;
            AttackDamage = 25;
            MoveSpeed = 2.4f;
            MaxHealth = 350;
            Health = 350;
            GoldReward = 30;
        }

        protected override IDamageable ScanForBlockers()
        {
            // Prioritize walls even further away
            int hits = Physics.OverlapSphereNonAlloc(transform.position, WallDetectRadius, ScanBuffer, TargetLayers);
            for (int i = 0; i < hits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                if (col.TryGetComponent(out Wall wall) && wall.Health > 0)
                {
                    return wall;
                }
            }

            return base.ScanForBlockers();
        }
    }
}
