using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)]
    private int _maxHealth = 100;

    private int _currentHealth;
    private EnemyStateMachine _stateMachine;

    public event Action<int, int> HealthChanged;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;

    private void Awake()
    {
        _stateMachine = GetComponent<EnemyStateMachine>();

        if (_stateMachine == null)
        {
            Debug.LogError(
                "EnemyHealth 找不到 EnemyStateMachine。",
                this
            );
        }
    }

    private void OnEnable()
    {
        _currentHealth = _maxHealth;
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (damageInfo.Damage <= 0 || _currentHealth <= 0)
            return;

        _currentHealth = Mathf.Max(
            0,
            _currentHealth - damageInfo.Damage
        );
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
        _stateMachine?.HandleDamageTaken();

        Debug.Log(
            $"{name} 剩余生命：{_currentHealth}/{_maxHealth}",
            this
        );
    }
}
