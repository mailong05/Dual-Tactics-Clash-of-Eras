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
            BuildingDamageMultiplier = 2.5f;
            GoldReward = 12;

            if (VisualModel != null)
            {
                // Căn chỉnh đầu khủng long nhỏ quay chuẩn 100% về phía trước (+Z / hướng căn cứ làng)
                VisualModel.localRotation = Quaternion.Euler(0f, 180f, 0f);
                _BaseModelLocalRot = VisualModel.localRotation;
            }
        }
    }
}
