using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_PlayerProgression_Default",
    menuName = "SoulsLike RPG/Player/Progression Config")]
public sealed class PlayerProgressionConfig : ScriptableObject
{
    [Header("Vigor")]
    [SerializeField, Min(1)] private int _baseHealth = 100;
    [SerializeField, Min(0)] private int _healthPerVigor = 10;

    [Header("Endurance")]
    [SerializeField, Min(0.01f)] private float _baseStamina = 100f;
    [SerializeField, Min(0f)] private float _staminaPerEndurance = 8f;

    [Header("Strength")]
    [SerializeField, Min(0f)] private float _damagePerStrength = 0.05f;

    public int BaseHealth => _baseHealth;
    public int HealthPerVigor => _healthPerVigor;
    public float BaseStamina => _baseStamina;
    public float StaminaPerEndurance => _staminaPerEndurance;
    public float DamagePerStrength => _damagePerStrength;

    public int CalculateMaxHealth(int vigor)
    {
        return (int)Math.Min(int.MaxValue,
            (long)_baseHealth + (Mathf.Max(1, vigor) - 1L) * _healthPerVigor);
    }

    public float CalculateMaxStamina(int endurance)
    {
        return _baseStamina + (Mathf.Max(1, endurance) - 1f) * _staminaPerEndurance;
    }

    public float CalculateDamageMultiplier(int strength)
    {
        return 1f + (Mathf.Max(1, strength) - 1f) * _damagePerStrength;
    }
}
