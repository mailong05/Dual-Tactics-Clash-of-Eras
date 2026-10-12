using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class RunnerDino : PrehistoricDinoBase
    {
        protected override void Awake()
        {
            base.Awake();
            MoveSpeed = 5.5f;
            MaxHealth = (UnitType != null && UnitType.Health > 0) ? UnitType.Health : 240;
            Health = MaxHealth;
            AttackDamage = 16;
            AttackInterval = 0.9f;
            GoldReward = 12;
        }
    }
}
