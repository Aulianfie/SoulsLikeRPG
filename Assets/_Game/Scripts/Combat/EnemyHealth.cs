using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)]
    private int _maxHealth = 100;
    private int _currentHealth;
    public event Action<int, int> HealthChanged;
    public event Action Died;
    public event Action Revived;
    public event Action<DamageInfo> DamageTaken;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;

    private void OnEnable()
    {
        _currentHealth = _maxHealth;
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
        Revived?.Invoke();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (damageInfo.Damage <= 0 ||
            _currentHealth <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - damageInfo.Damage);
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
        if (_currentHealth == 0)
        {
            Died?.Invoke();
        }

        DamageTaken?.Invoke(damageInfo);
        Debug.Log($"{name} 剩余生命：{_currentHealth}/{_maxHealth}", this);
    }

    public void RestoreFull()
    {
        bool wasDead = _currentHealth <= 0;
        _currentHealth = _maxHealth;
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
        if (wasDead)
        {
            Revived?.Invoke();
        }
    }
}
