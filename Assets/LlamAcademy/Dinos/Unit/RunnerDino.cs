using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class RunnerDino : PrehistoricDinoBase
    {
        protected override void Awake()
        {
            base.Awake();
            MoveSpeed = 5.5f;
            MaxHealth = 85;
            Health = 85;
            AttackDamage = 14;
            AttackInterval = 0.9f;
            GoldReward = 12;
        }
    }
}
