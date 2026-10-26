using LlamAcademy.Dinos.Unit;
using UnityEngine;

namespace LlamAcademy.Dinos.Config
{
    [CreateAssetMenu(fileName = "Auto Attack", menuName = "AI/Attacks/Auto Attack")]
    public class DamagingAttackSO : AttackTypeSO
    {
        public override void ApplyEffect(IDamageable target)
        {
            if (target == null) return;
            ArmorTypeSO armor = target.UnitType != null ? target.UnitType.ArmorType : null;
            target.TakeDamage(GetDamage(armor));
        }
    }
}
