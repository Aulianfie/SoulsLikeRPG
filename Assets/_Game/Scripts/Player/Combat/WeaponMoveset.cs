using UnityEngine;

// Shared static action definitions; execution state belongs to PlayerCombat.
[CreateAssetMenu(fileName = "WeaponMoveset", menuName = "SoulsLike RPG/Combat/Weapon Moveset")]
public sealed class WeaponMoveset : ScriptableObject
{
    [SerializeField] private AttackCombo _lightCombo;
    [SerializeField] private AttackData _jumpAttack;
    [SerializeField] private AttackData _weaponSkill;

    public AttackCombo LightCombo => _lightCombo;
    public AttackData JumpAttack => _jumpAttack;
    public AttackData WeaponSkill => _weaponSkill;
}
