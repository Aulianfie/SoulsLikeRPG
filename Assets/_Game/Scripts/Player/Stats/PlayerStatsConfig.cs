using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_PlayerStats_Default",
    menuName = "SoulsLike RPG/Player/Stats Config")]
public sealed class PlayerStatsConfig : ScriptableObject
{
    [Header("Resources")]
    [SerializeField, Min(1)] private int _maxHealth = 100;
    [SerializeField, Min(0.01f)] private float _maxMana = 100f;
    [SerializeField, Min(0.01f)] private float _maxStamina = 100f;

    [Header("Stamina Costs")]
    [SerializeField, Min(0f)] private float _attackStaminaCost = 20f;
    [SerializeField, Min(0f)] private float _dodgeStaminaCost = 30f;

    [Header("Stamina Regeneration")]
    [SerializeField, Min(0f)] private float _staminaRegenDelay = 0.8f;
    [SerializeField, Min(0f)] private float _staminaRegenRate = 25f;

    public int MaxHealth => _maxHealth;
    public float MaxMana => _maxMana;
    public float MaxStamina => _maxStamina;
    public float AttackStaminaCost => _attackStaminaCost;
    public float DodgeStaminaCost => _dodgeStaminaCost;
    public float StaminaRegenDelay => _staminaRegenDelay;
    public float StaminaRegenRate => _staminaRegenRate;
}
