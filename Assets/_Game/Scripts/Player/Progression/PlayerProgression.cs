using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth), typeof(PlayerStamina))]
[RequireComponent(typeof(SoulWallet))]
public sealed class PlayerProgression : MonoBehaviour
{
    [SerializeField] private PlayerProgressionConfig _config;
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField, Min(1)] private int _vigor = 1;
    [SerializeField, Min(1)] private int _endurance = 1;
    [SerializeField, Min(1)] private int _strength = 1;

    private PlayerHealth _health;
    private PlayerStamina _stamina;
    private SoulWallet _wallet;
    private bool _isUpgrading;

    public event Action ProgressionChanged;

    public int Level => _level;
    public int Vigor => _vigor;
    public int Endurance => _endurance;
    public int Strength => _strength;
    public int UpgradeCost => _config != null ? _config.CalculateUpgradeCost(_level) : 0;
    public int MaxHealth => _config != null ? _config.CalculateMaxHealth(_vigor) : 0;
    public float MaxStamina => _config != null ? _config.CalculateMaxStamina(_endurance) : 0f;
    public float DamageMultiplier => _config != null
        ? _config.CalculateDamageMultiplier(_strength) : 1f;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _stamina = GetComponent<PlayerStamina>();
        _wallet = GetComponent<SoulWallet>();
        if (_config == null)
            Debug.LogError("PlayerProgression 缺少 PlayerProgressionConfig。", this);
    }

    private void Start()
    {
        // 等待资源组件的 Awake 完成，初始成长属性增加的容量也会补入当前值。
        RecalculateStats();
    }

    /// <summary>统一设置基础成长数据，供初始化、调试及后续存档恢复使用。</summary>
    public void SetProgression(int level, int vigor, int endurance, int strength)
    {
        if (level < 1 || vigor < 1 || endurance < 1 || strength < 1)
            throw new ArgumentOutOfRangeException(nameof(level), "Level 和成长属性必须至少为 1。");
        if (_isUpgrading)
            throw new InvalidOperationException("升级过程中不能替换基础成长数据。");

        _level = level;
        _vigor = vigor;
        _endurance = endurance;
        _strength = strength;
        RecalculateStats();
    }

    public bool CanUpgrade(StatType stat)
    {
        return !_isUpgrading && _config != null && _wallet != null &&
            _health != null && !_health.IsDead && _level < int.MaxValue &&
            IsValidStat(stat) && GetStatValue(stat) < int.MaxValue &&
            _wallet.CanAfford(UpgradeCost);
    }

    public bool TryUpgrade(StatType stat)
    {
        if (!CanUpgrade(stat))
            return false;

        int cost = UpgradeCost;
        _isUpgrading = true;
        try
        {
            // SoulsChanged 监听者不能在这次扣款尚未完成时重入升级。
            if (!_wallet.TrySpend(cost))
                return false;

            switch (stat)
            {
                case StatType.Vigor: _vigor++; break;
                case StatType.Endurance: _endurance++; break;
                case StatType.Strength: _strength++; break;
            }
            _level++;
            RecalculateStats();
        }
        finally
        {
            _isUpgrading = false;
        }

        // 等事务保护解除后通知 UI，CanUpgrade 才能反映下一次升级是否可用。
        ProgressionChanged?.Invoke();
        return true;
    }

    public StatUpgradePreview GetUpgradePreview(StatType stat)
    {
        if (!IsValidStat(stat))
            throw new ArgumentOutOfRangeException(nameof(stat));

        int current = GetStatValue(stat);
        int next = current == int.MaxValue ? current : current + 1;
        return new StatUpgradePreview(current, next,
            CalculateStatEffect(stat, current), CalculateStatEffect(stat, next));
    }

    private int GetStatValue(StatType stat)
    {
        switch (stat)
        {
            case StatType.Vigor: return _vigor;
            case StatType.Endurance: return _endurance;
            case StatType.Strength: return _strength;
            default: throw new ArgumentOutOfRangeException(nameof(stat));
        }
    }

    private float CalculateStatEffect(StatType stat, int value)
    {
        if (_config == null)
            return 0f;

        switch (stat)
        {
            case StatType.Vigor: return _config.CalculateMaxHealth(value);
            case StatType.Endurance: return _config.CalculateMaxStamina(value);
            case StatType.Strength: return _config.CalculateDamageMultiplier(value);
            default: throw new ArgumentOutOfRangeException(nameof(stat));
        }
    }

    private static bool IsValidStat(StatType stat)
    {
        return stat == StatType.Vigor || stat == StatType.Endurance || stat == StatType.Strength;
    }

    public int CalculateAttackDamage(int baseDamage)
    {
        // DamageInfo 使用 int；统一四舍五入，不修改共享 AttackData。
        double damage = Math.Max(0, baseDamage) * (double)DamageMultiplier;
        return (int)Math.Min(int.MaxValue, Math.Round(damage, MidpointRounding.AwayFromZero));
    }

    private void RecalculateStats()
    {
        if (_config == null)
            return;

        _health.SetMaxHealth(MaxHealth);
        _stamina.SetMaxStamina(MaxStamina);
        if (!_isUpgrading)
            ProgressionChanged?.Invoke();
    }

    private void OnValidate()
    {
        _level = Mathf.Max(1, _level);
        _vigor = Mathf.Max(1, _vigor);
        _endurance = Mathf.Max(1, _endurance);
        _strength = Mathf.Max(1, _strength);
    }
}
