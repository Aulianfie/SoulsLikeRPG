using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth), typeof(PlayerStamina))]
public sealed class PlayerProgression : MonoBehaviour
{
    [SerializeField] private PlayerProgressionConfig _config;
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField, Min(1)] private int _vigor = 1;
    [SerializeField, Min(1)] private int _endurance = 1;
    [SerializeField, Min(1)] private int _strength = 1;

    private PlayerHealth _health;
    private PlayerStamina _stamina;

    public event Action ProgressionChanged;

    public int Level => _level;
    public int Vigor => _vigor;
    public int Endurance => _endurance;
    public int Strength => _strength;
    public int MaxHealth => _config != null ? _config.CalculateMaxHealth(_vigor) : 0;
    public float MaxStamina => _config != null ? _config.CalculateMaxStamina(_endurance) : 0f;
    public float DamageMultiplier => _config != null
        ? _config.CalculateDamageMultiplier(_strength) : 1f;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _stamina = GetComponent<PlayerStamina>();
        if (_config == null)
            Debug.LogError("PlayerProgression 缺少 PlayerProgressionConfig。", this);
    }

    private void Start()
    {
        // 等待资源组件的 Awake 完成，初始成长属性增加的容量也会补入当前值。
        RecalculateStats();
    }

    /// <summary>统一设置基础成长数据；本阶段不处理升级费用或存档。</summary>
    public void SetProgression(int level, int vigor, int endurance, int strength)
    {
        if (level < 1 || vigor < 1 || endurance < 1 || strength < 1)
            throw new ArgumentOutOfRangeException(nameof(level), "Level 和成长属性必须至少为 1。");

        _level = level;
        _vigor = vigor;
        _endurance = endurance;
        _strength = strength;
        RecalculateStats();
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
