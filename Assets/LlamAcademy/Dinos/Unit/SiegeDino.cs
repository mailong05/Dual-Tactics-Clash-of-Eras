using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class SiegeDino : PrehistoricDinoBase
    {
        [Header("Siege Breaker Specialization")]
        [SerializeField] private float WallDetectRadius = 8.5f;

        protected override float WallScanRadius => Mathf.Max(WallDetectRadius, 9.5f);

        protected override void Awake()
        {
            MoveSpeed = 3.0f;
            base.Awake();
            ConfigureNavMeshAgent();
            BuildingDamageMultiplier = 3.5f;
            AttackDamage = 25;
            MaxHealth = (UnitType != null && UnitType.Health > 0) ? UnitType.Health : 1912;
            Health = MaxHealth;
            GoldReward = 40;
        }

        protected override IDamageable ScanForBlockers()
        {
            if (_IsNavigatingBreach) return null;
            return base.ScanForBlockers();
        }
    }
}
