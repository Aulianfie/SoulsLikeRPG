using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SoulWallet : MonoBehaviour
{
    [SerializeField, Min(0)] private int _currentSouls = 1000;

    public event Action<int> SoulsChanged;

    public int CurrentSouls => _currentSouls;

    public void SetSouls(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount == _currentSouls)
            return;
        _currentSouls = amount;
        SoulsChanged?.Invoke(_currentSouls);
    }

    public void AddSouls(int amount)
    {
        if (amount <= 0)
            return;

        int next = (int)Math.Min(int.MaxValue, (long)_currentSouls + amount);
        if (next == _currentSouls)
            return;

        _currentSouls = next;
        SoulsChanged?.Invoke(_currentSouls);
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 && _currentSouls >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount))
            return false;

        if (amount == 0)
            return true;

        _currentSouls -= amount;
        SoulsChanged?.Invoke(_currentSouls);
        return true;
    }

    private void OnValidate()
    {
        _currentSouls = Mathf.Max(0, _currentSouls);
    }
}
