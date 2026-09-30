using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerHealingFlask : MonoBehaviour, IPlayerQuickItem
{
    [SerializeField, Min(1)] private int _maxCharges = 3;
    [SerializeField, Min(1)] private int _healAmount = 40;
    [SerializeField, Range(0f, 1f)] private float _healPoint = 0.45f;
    [SerializeField, Range(0f, 1f)] private float _movementMultiplier = 0.4f;
    [SerializeField] private GameObject _heldBottle;
    [SerializeField] private Renderer[] _hiddenWeaponRenderers = Array.Empty<Renderer>();

    private PlayerHealth _health;
    private int _currentCharges;
    private bool[] _previousRendererStates;
    private bool _showingBottle;

    public event Action<int, int> ChargesChanged;
    public int CurrentCharges => _currentCharges;
    public int MaxCharges => Mathf.Max(1, _maxCharges);
    public int HealAmount => Mathf.Max(1, _healAmount);
    public float HealPoint => Mathf.Clamp(_healPoint, 0.05f, 0.9f);
    public float MovementMultiplier => Mathf.Clamp01(_movementMultiplier);
    public bool CanUse => isActiveAndEnabled && _health != null && !_health.IsDead &&
        _health.CurrentHealth < _health.MaxHealth && _currentCharges > 0;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _currentCharges = MaxCharges;
        _previousRendererStates = new bool[_hiddenWeaponRenderers.Length];
        if (_heldBottle != null) _heldBottle.SetActive(false);
    }

    public bool TryUse(PlayerStateMachine player)
    {
        return player != null && player.gameObject == gameObject && CanUse && player.TryBeginHealing(this);
    }

    public bool TryConsume()
    {
        if (!CanUse) return false;
        SetCharges(_currentCharges - 1);
        return true;
    }

    public void Refill() => SetCharges(MaxCharges);

    // -1 是旧存档或未配置血瓶场景的哨兵；新配置的最大数量仍是权威值。
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
        if (_showingBottle == visible) return;
        for (int index = 0; index < _hiddenWeaponRenderers.Length; index++)
        {
            Renderer renderer = _hiddenWeaponRenderers[index];
            if (renderer == null) continue;
            if (visible) _previousRendererStates[index] = renderer.enabled;
            renderer.enabled = visible ? false : _previousRendererStates[index];
        }
        _showingBottle = visible;
    }

    private void OnDisable() => SetUseVisual(false);
}
