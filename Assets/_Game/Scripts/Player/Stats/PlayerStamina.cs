using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerStamina : MonoBehaviour
{
    [SerializeField] private PlayerStatsConfig _config;

    private float _currentStamina;
    private float _regenResumeTime;

    public event Action<float, float> StaminaChanged;

    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _config != null ? _config.MaxStamina : 0f;
    public float AttackCost => _config != null ? _config.AttackStaminaCost : 0f;
    public float DodgeCost => _config != null ? _config.DodgeStaminaCost : 0f;

    private void Awake()
    {
        if (_config == null)
        {
            Debug.LogError("PlayerStamina 缺少 PlayerStatsConfig。", this);
            enabled = false;
            return;
        }

        _currentStamina = MaxStamina;
        enabled = false;
    }

    private void Update()
    {
        if (Time.time < _regenResumeTime)
            return;

        float nextStamina = Mathf.MoveTowards(
            _currentStamina,
            MaxStamina,
            _config.StaminaRegenRate * Time.deltaTime);

        if (!Mathf.Approximately(nextStamina, _currentStamina))
        {
            _currentStamina = nextStamina;
            StaminaChanged?.Invoke(_currentStamina, MaxStamina);
        }

        if (Mathf.Approximately(_currentStamina, MaxStamina))
        {
            _currentStamina = MaxStamina;
            enabled = false;
        }
    }

    public bool CanConsume(float amount)
    {
        return amount >= 0f && _currentStamina >= amount;
    }

    public bool Consume(float amount)
    {
        if (amount <= 0f || !CanConsume(amount) || _config == null)
            return false;

        _currentStamina -= amount;
        _regenResumeTime = Time.time + _config.StaminaRegenDelay;
        StaminaChanged?.Invoke(_currentStamina, MaxStamina);
        enabled = true;
        return true;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f || _config == null)
            return;

        float nextStamina = Mathf.Min(MaxStamina, _currentStamina + amount);
        if (Mathf.Approximately(nextStamina, _currentStamina))
            return;

        _currentStamina = nextStamina;
        StaminaChanged?.Invoke(_currentStamina, MaxStamina);

        if (Mathf.Approximately(_currentStamina, MaxStamina))
        {
            enabled = false;
        }
    }
}
