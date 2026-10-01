using System;
using UnityEngine;

// 每个实例拥有独立次数；静态配置由两种瓶子的 ConsumableData 共享。
[RequireComponent(typeof(PlayerHealth))]
public class PlayerConsumable : MonoBehaviour, IPlayerQuickItem
{
    [SerializeField] private ConsumableData _data;
    [SerializeField] private GameObject _heldBottle;
    private PlayerHealth _health;
    private int _currentCharges;
    private bool _consuming;

    public event Action<int, int> ChargesChanged;
    public ConsumableData Data => _data;
    public int CurrentCharges => _currentCharges;
    public int MaxCharges => _data != null ? _data.MaxCharges : 0;
    public float ConsumePoint => _data != null ? _data.ConsumePoint : 0.45f;
    public float CompletionPoint => _data != null ? _data.CompletionPoint : 0.95f;
    public float MovementMultiplier => _data != null ? _data.MovementMultiplier : 0.4f;
    // 消耗最后一瓶后仍需完成动画。
    public bool IsAvailable => isActiveAndEnabled && _data != null;
    public bool CanUse => IsAvailable && !_consuming && _health != null && !_health.IsDead &&
        _currentCharges > 0 && _data.CanApply(gameObject);

    protected virtual void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _currentCharges = MaxCharges;
        SetUseVisual(false);
    }

    public bool TryUse(PlayerStateMachine player)
    {
        return player != null && player.gameObject == gameObject && CanUse && player.TryBeginUseItem(this);
    }

    public bool TryConsume()
    {
        // 消费点重新检查；动画途中已补满资源时不会浪费次数。
        if (!CanUse) return false;
        _consuming = true;
        try
        {
            if (!_data.Apply(gameObject)) return false;
            SetCharges(_currentCharges - 1);
            return true;
        }
        finally { _consuming = false; }
    }

    public void Refill() => SetCharges(MaxCharges);
    public void RestoreCharges(int charges) => SetCharges(charges < 0 ? MaxCharges : charges);

    private void SetCharges(int charges)
    {
        int next = Mathf.Clamp(charges, 0, MaxCharges);
        if (next == _currentCharges) return;
        _currentCharges = next;
        ChargesChanged?.Invoke(_currentCharges, MaxCharges);
    }

    public void SetUseVisual(bool visible)
    {
        if (_heldBottle != null) _heldBottle.SetActive(visible);
    }

    protected virtual void OnDisable() => SetUseVisual(false);
}
