using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private PlayerStatsConfig _config;

    private int _currentHealth;

    public event Action<int, int> HealthChanged;
    public event Action Died;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _config != null ? _config.MaxHealth : 0;
    public bool IsDead => _currentHealth <= 0;

    private void Awake()
    {
        if (_config == null)
        {
            Debug.LogError("PlayerHealth 缺少 PlayerStatsConfig。", this);
            return;
        }

        _currentHealth = MaxHealth;
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (damageInfo.Damage <= 0 || IsDead)
            return;

        int nextHealth = Mathf.Max(0, _currentHealth - damageInfo.Damage);
        if (nextHealth == 0)
        {
            Die();
            return;
        }

        _currentHealth = nextHealth;
        HealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead || _config == null)
            return;

        int nextHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
        if (nextHealth == _currentHealth)
            return;

        _currentHealth = nextHealth;
        HealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    public void Die()
    {
        if (IsDead)
            return;

        _currentHealth = 0;
        HealthChanged?.Invoke(_currentHealth, MaxHealth);
        Died?.Invoke();
    }
}
