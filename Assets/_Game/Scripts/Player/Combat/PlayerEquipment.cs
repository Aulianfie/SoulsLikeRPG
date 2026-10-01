using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCombat))]
public sealed class PlayerEquipment : MonoBehaviour
{
    [Serializable]
    private sealed class WeaponSlot
    {
        public WeaponData Data;
        public WeaponHitbox Hitbox;
    }

    [SerializeField] private WeaponSlot[] _slots = Array.Empty<WeaponSlot>();
    [SerializeField, Min(0)] private int _defaultSlot;
    [Header("Shared Weapon Switch")]
    [Tooltip("从开始切换到恢复可攻击的时长（秒）；不影响移动或奔跑速度。")]
    [SerializeField, Min(0.1f)] private float _switchDuration = 0.4f;
    [Header("Animation Timing (Normalized)")]
    [Tooltip("右手伸向肩后时隐藏旧武器；所有槽位共用同一动作。")]
    [SerializeField, Range(0f, 1f)] private float _switchHidePoint = 0.38f;
    [Tooltip("实际替换装备、攻击配置和模型的时间点。")]
    [SerializeField, Range(0f, 1f)] private float _switchEquipPoint = 0.50f;
    [Tooltip("从肩后取出新武器时恢复显示。")]
    [SerializeField, Range(0f, 1f)] private float _switchShowPoint = 0.65f;
    [SerializeField, Range(0f, 1f)] private float _switchCompletionPoint = 0.93f;
    private PlayerStateMachine _player;
    private PlayerCombat _combat;
    private int _pendingSlotIndex = -1;

    public float SwitchDuration => _switchDuration;
    public float SwitchHidePoint => _switchHidePoint;
    public float SwitchEquipPoint => _switchEquipPoint;
    public float SwitchShowPoint => _switchShowPoint;
    public float SwitchCompletionPoint => _switchCompletionPoint;

    public WeaponData CurrentWeapon { get; private set; }
    public WeaponHitbox CurrentHitbox { get; private set; }
    public int CurrentSlotIndex { get; private set; } = -1;
    public event Action<WeaponData> WeaponChanged;

    public bool CanSwitch => isActiveAndEnabled && _player != null &&
        _player.InputReader != null && _player.InputReader.isActiveAndEnabled &&
        Time.timeScale > 0f && _player.Health != null && !_player.Health.IsDead &&
        _player.CurrentState == _player.LocomotionState;

    private void Awake()
    {
        _player = GetComponent<PlayerStateMachine>();
        _combat = GetComponent<PlayerCombat>();
    }

    private void Start()
    {
        if (CurrentWeapon == null && !ApplySlot(_defaultSlot))
            Debug.LogError("PlayerEquipment 默认武器槽配置不完整。", this);
    }

    public bool EquipSlot(int slotIndex)
    {
        if (!CanSwitch || !IsSlotValid(slotIndex)) return false;
        if (slotIndex == CurrentSlotIndex) return true;
        // 旧场景没有切换动画时仍可直接换装；初始装备不播放动画。
        if (!_player.PlayerAnimator.HasWeaponSwitchAnimation) return ApplySlot(slotIndex);
        if (!_player.Motor.IsGrounded) return false;
        _pendingSlotIndex = slotIndex;
        _player.ChangeState(_player.WeaponSwitchState);
        return _player.CurrentState == _player.WeaponSwitchState;
    }

    public void CycleWeapon() => CycleWeapon(1);

    public void CycleWeapon(int direction)
    {
        if (!CanSwitch || direction == 0 || _slots.Length < 2) return;
        int step = direction > 0 ? 1 : -1;
        for (int offset = 1; offset < _slots.Length; offset++)
        {
            int index = (CurrentSlotIndex + step * offset + _slots.Length) % _slots.Length;
            if (EquipSlot(index)) return;
        }
    }

    public bool CommitWeaponSwitch()
    {
        if (!isActiveAndEnabled || _player.CurrentState != _player.WeaponSwitchState)
            return false;
        int index = _pendingSlotIndex;
        _pendingSlotIndex = -1;
        return ApplySlot(index);
    }

    public void CancelWeaponSwitch() => _pendingSlotIndex = -1;

    private bool IsSlotValid(int index)
    {
        if (index < 0 || index >= _slots.Length) return false;
        WeaponSlot slot = _slots[index];
        return slot != null && slot.Data != null && slot.Hitbox != null &&
            slot.Data.LightAttackCombo != null && slot.Data.LightAttackCombo.Get(0) != null;
    }

    private bool ApplySlot(int index)
    {
        if (!IsSlotValid(index)) return false;
        WeaponSlot slot = _slots[index];
        if (index == CurrentSlotIndex) return true;

        // 切换前关闭所有命中窗口，确保旧武器不会在下一帧继续伤害。
        foreach (WeaponSlot weapon in _slots)
        {
            if (weapon == null || weapon.Hitbox == null) continue;
            weapon.Hitbox.EndAttack();
            weapon.Hitbox.gameObject.SetActive(false);
        }
        CurrentSlotIndex = index;
        CurrentWeapon = slot.Data;
        CurrentHitbox = slot.Hitbox;
        CurrentHitbox.gameObject.SetActive(true);
        _combat.SetWeapon(CurrentWeapon, CurrentHitbox);
        _player.InputReader.ClearAllBuffers();
        WeaponChanged?.Invoke(CurrentWeapon);
        return true;
    }

    private void OnDisable()
    {
        CancelWeaponSwitch();
        foreach (WeaponSlot slot in _slots) slot?.Hitbox?.EndAttack();
    }

    private void OnValidate()
    {
        _switchDuration = Mathf.Max(0.1f, _switchDuration);
        _switchCompletionPoint = Mathf.Clamp(_switchCompletionPoint, 0.1f, 1f);
        _switchEquipPoint = Mathf.Max(_switchHidePoint, _switchEquipPoint);
        _switchShowPoint = Mathf.Max(_switchEquipPoint, _switchShowPoint);
        _switchCompletionPoint = Mathf.Max(_switchShowPoint, _switchCompletionPoint);
    }
}
