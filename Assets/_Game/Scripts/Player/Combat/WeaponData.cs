using UnityEngine;

// 仅保存静态配置；槽位、模型与当前连击由玩家组件持有。
[CreateAssetMenu(fileName = "WeaponData", menuName = "SoulsLike RPG/Combat/Weapon Data")]
public sealed class WeaponData : ScriptableObject
{
    [SerializeField] private string _weaponId;
    [SerializeField] private string _displayName;
    [SerializeField] private GameObject _weaponPrefab;
    [SerializeField] private Sprite _icon;
    [SerializeField] private WeaponMoveset _moveset;
    // Retain the old serialized reference for safe migration of older assets.
    [SerializeField, HideInInspector] private AttackCombo _lightAttackCombo;
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
    [SerializeField, Min(0f)] private float _staminaMultiplier = 1f;
    [SerializeField] private AnimatorOverrideController _animatorOverrideController;
    [SerializeField] private WeaponType _weaponType;

    public string WeaponId => _weaponId;
    public string DisplayName => _displayName;
    public GameObject WeaponPrefab => _weaponPrefab;
    public Sprite Icon => _icon;
    public WeaponMoveset Moveset => _moveset;
    public AttackCombo LightAttackCombo => _moveset != null ? _moveset.LightCombo : _lightAttackCombo;
    public float DamageMultiplier => _damageMultiplier;
    public float StaminaMultiplier => _staminaMultiplier;
    public AnimatorOverrideController AnimatorOverrideController => _animatorOverrideController;
    public WeaponType WeaponType => _weaponType;
}
