using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMana : MonoBehaviour
{
    [SerializeField] private PlayerStatsConfig _config;

    private float _currentMana;

    public event Action<float, float> ManaChanged;

    public float CurrentMana => _currentMana;
    public float MaxMana => _config != null ? _config.MaxMana : 0f;

    private void Awake()
    {
        if (_config == null)
        {
            Debug.LogError("PlayerMana 缺少 PlayerStatsConfig。", this);
            return;
        }

        _currentMana = MaxMana;
    }

    public bool CanConsume(float amount)
    {
        return amount >= 0f && _currentMana >= amount;
    }

    public bool Consume(float amount)
    {
        if (amount <= 0f || !CanConsume(amount))
            return false;

        _currentMana -= amount;
        ManaChanged?.Invoke(_currentMana, MaxMana);
        return true;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f || _config == null)
            return;

        float nextMana = Mathf.Min(MaxMana, _currentMana + amount);
        if (Mathf.Approximately(nextMana, _currentMana))
            return;

        _currentMana = nextMana;
        ManaChanged?.Invoke(_currentMana, MaxMana);
    }
}
