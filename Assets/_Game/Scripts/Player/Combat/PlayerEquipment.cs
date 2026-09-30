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
    private PlayerStateMachine _player;
    private PlayerCombat _combat;

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
        return CanSwitch && ApplySlot(slotIndex);
    }

    public void CycleWeapon() => CycleWeapon(1);

    public void CycleWeapon(int direction)
    {
        if (!CanSwitch || direction == 0 || _slots.Length < 2) return;
        int step = direction > 0 ? 1 : -1;
        for (int offset = 1; offset < _slots.Length; offset++)
        {
            int index = (CurrentSlotIndex + step * offset + _slots.Length) % _slots.Length;
            if (ApplySlot(index)) return;
        }
    }

    private bool ApplySlot(int index)
    {
        if (index < 0 || index >= _slots.Length) return false;
        WeaponSlot slot = _slots[index];
        if (slot == null || slot.Data == null || slot.Hitbox == null ||
            slot.Data.LightAttackCombo == null || slot.Data.LightAttackCombo.Get(0) == null)
            return false;
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
        foreach (WeaponSlot slot in _slots) slot?.Hitbox?.EndAttack();
    }
}
