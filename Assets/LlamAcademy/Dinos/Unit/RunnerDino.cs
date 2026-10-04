using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class RunnerDino : PrehistoricDinoBase
    {
        protected override void Awake()
        {
            base.Awake();
            MoveSpeed = 5.5f;
            MaxHealth = 110;
            Health = 110;
            AttackDamage = 16;
            AttackInterval = 0.9f;
            GoldReward = 12;
        }
    }
}
