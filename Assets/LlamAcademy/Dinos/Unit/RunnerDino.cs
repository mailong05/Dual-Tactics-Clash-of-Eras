using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class RunnerDino : PrehistoricDinoBase
    {
        protected override void Awake()
        {
            MoveSpeed = 5.5f;
            base.Awake();
            ConfigureNavMeshAgent();
            MaxHealth = (UnitType != null && UnitType.Health > 0) ? UnitType.Health : 420;
            Health = MaxHealth;
            AttackDamage = 16;
            AttackInterval = 0.9f;
            GoldReward = 12;
        }
    }
}
