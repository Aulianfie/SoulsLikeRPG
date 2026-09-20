using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int _maxHealth = 100;

    private int _currentHealth;

    public event Action<int, int> HealthChanged;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;

    private void OnEnable()
    {
        _currentHealth = _maxHealth;
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || _currentHealth <= 0)
            return;

        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        HealthChanged?.Invoke(_currentHealth, _maxHealth);

        Debug.Log(
            $"{name} 剩余生命：{_currentHealth}/{_maxHealth}",
            this
        );
    }
}
