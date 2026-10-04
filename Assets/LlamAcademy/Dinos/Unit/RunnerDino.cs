using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class RunnerDino : PrehistoricDinoBase
    {
        protected override void Awake()
        {
            base.Awake();
            MoveSpeed = 5.5f;
            MaxHealth = 480;
            Health = 480;
            AttackDamage = 16;
            AttackInterval = 0.9f;
            GoldReward = 12;
        }
    }
}
