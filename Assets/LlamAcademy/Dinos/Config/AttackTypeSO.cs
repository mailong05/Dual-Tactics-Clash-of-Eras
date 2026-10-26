using System.Linq;
using LlamAcademy.Dinos.Unit;
using UnityEngine;

namespace LlamAcademy.Dinos.Config
{
    public abstract class AttackTypeSO : ScriptableObject
    {
        [field: SerializeField][Range(0.1f, 3)] public float AttackDelay { get; private set; } = 1;
        [field: SerializeField] public bool DelayDamage { get; private set; } = false;
        [field: SerializeField] public float DelayDamageTime { get; private set; } = 0.75f;
        [field: SerializeField] public int Priority { get; private set; }
        [field: SerializeField] public ArmorTypeDamage[] DamagePerType { get; private set; }
        [field: SerializeField] public float MaxAttackRange { get; private set; } = 1.5f;
        [field: SerializeField] public ParticleSystem AttackParticlePrefab { get; private set; }
        [field: SerializeField] public MoveToTargetConfig AttackParticleMoveConfig { get; private set; }

        public int GetDamage(ArmorTypeSO armorType)
        {
            if (DamagePerType == null || DamagePerType.Length == 0)
            {
                return 5; // Sát thương mặc định nếu chưa cấu hình bảng sát thương
            }

            if (armorType == null)
            {
                // Nếu không có ArmorType (ví dụ cầu gỗ chưa gán ArmorType), dùng entry đầu tiên
                return DamagePerType[0] != null ? DamagePerType[0].Damage : 5;
            }

            ArmorTypeDamage armorTypeDamage = DamagePerType.FirstOrDefault((type) => type.ArmorType == armorType);

            if (armorTypeDamage != null)
            {
                return armorTypeDamage.Damage;
            }

            // Fallback: dùng entry đầu tiên nếu không tìm thấy loại giáp phù hợp
            if (DamagePerType.Length > 0 && DamagePerType[0] != null)
            {
                return DamagePerType[0].Damage;
            }

            return 5;
        }

        public float GetMinAttackRange(IDamageable damageable)
        {
            if (damageable == null) return 0;

            ArmorTypeDamage armorTypeDamage = DamagePerType.FirstOrDefault(item => item.ArmorType == damageable.UnitType.ArmorType);
            if (armorTypeDamage == null)
            {
                return 0;
            }

            return armorTypeDamage.MinAttackRange;
        }

        public abstract void ApplyEffect(IDamageable target);

        [System.Serializable]
        public class ArmorTypeDamage
        {
            public int Damage = 5;
            public ArmorTypeSO ArmorType;
            public float MinAttackRange = 0;
        }

        [System.Serializable]
        public class MoveToTargetConfig
        {
            [field: SerializeField] public bool MoveToTarget { get; private set; }
            [field: SerializeField] public float MoveToTargetTime { get; private set; } = 2f;
            [field: SerializeField] public float MoveToTargetDelay;
            [field: SerializeField] public Vector3 LocalSpawnOffset { get; private set; } = Vector3.zero;
        }
    }
}
