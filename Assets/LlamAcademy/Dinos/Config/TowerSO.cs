using LlamAcademy.Dinos.Enemy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LlamAcademy.Dinos.Config
{
    [CreateAssetMenu(fileName = "Tower", menuName = "AI/Tower Unit", order = 1)]
    public class TowerSO : UnitSO
    {
        [field: SerializeField] public string DisplayName { get; private set; } = "Tower";
        [field: SerializeField, TextArea] public string Description { get; private set; }
        [field: SerializeField] public Sprite Sprite { get; private set; }
        [field: SerializeField] public new int Cost { get; set; } = 50;
        [field: SerializeField] public Key Hotkey { get; private set; }
        [field: SerializeField] public bool IsWall { get; private set; } = false;
        [field: SerializeField] public bool CanTargetFlying { get; private set; } = true;
        [field: SerializeField] public float PlacementRadius { get; private set; } = 1.0f;
    }
}
