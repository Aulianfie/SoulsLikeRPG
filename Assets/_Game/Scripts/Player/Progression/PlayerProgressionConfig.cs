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

    [Header("Upgrade Cost")]
    [SerializeField, Min(1)] private int _baseUpgradeCost = 100;
    [SerializeField, Min(0)] private int _upgradeCostPerLevel = 50;
    [SerializeField] private bool _useUpgradeCostCurve = true;
    [Tooltip("横轴：升级前的当前等级；纵轴：本次升级所需 Soul。末端外按最后一段斜率延续。")]
    [SerializeField] private AnimationCurve _upgradeCostCurve =
        AnimationCurve.Linear(1f, 150f, 100f, 5100f);

    public int BaseHealth => _baseHealth;
    public int HealthPerVigor => _healthPerVigor;
    public float BaseStamina => _baseStamina;
    public float StaminaPerEndurance => _staminaPerEndurance;
    public float DamagePerStrength => _damagePerStrength;
    public int BaseUpgradeCost => _baseUpgradeCost;
    public int UpgradeCostPerLevel => _upgradeCostPerLevel;

    public int CalculateUpgradeCost(int level)
    {
        level = Mathf.Max(1, level);
        double cost = (long)_baseUpgradeCost + (long)level * _upgradeCostPerLevel;
        if (_useUpgradeCostCurve && _upgradeCostCurve != null && _upgradeCostCurve.length > 0)
        {
            cost = _upgradeCostCurve.Evaluate(level);
            Keyframe[] keys = _upgradeCostCurve.keys;
            if (keys.Length > 1 && level > keys[keys.Length - 1].time)
            {
                Keyframe last = keys[keys.Length - 1];
                Keyframe previous = keys[keys.Length - 2];
                double span = last.time - previous.time;
                if (span > 0)
                    cost = last.value + (level - (double)last.time) *
                        (last.value - previous.value) / span;
            }
        }

        if (double.IsNaN(cost) || double.IsInfinity(cost))
            return int.MaxValue;

        return (int)Math.Max(1, Math.Min(int.MaxValue,
            Math.Round(cost, MidpointRounding.AwayFromZero)));
    }

    private void OnValidate()
    {
        _baseHealth = Mathf.Max(1, _baseHealth);
        _healthPerVigor = Mathf.Max(0, _healthPerVigor);
        _baseStamina = Mathf.Max(0.01f, _baseStamina);
        _staminaPerEndurance = Mathf.Max(0f, _staminaPerEndurance);
        _damagePerStrength = Mathf.Max(0f, _damagePerStrength);
        _baseUpgradeCost = Mathf.Max(1, _baseUpgradeCost);
        _upgradeCostPerLevel = Mathf.Max(0, _upgradeCostPerLevel);
    }

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
