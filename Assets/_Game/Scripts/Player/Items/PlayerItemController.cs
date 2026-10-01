using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerItemController : MonoBehaviour
{
    [SerializeField]
    private MonoBehaviour[] _quickItemSlots = Array.Empty<MonoBehaviour>();
    [SerializeField, Min(0)]
    private int _defaultSlot;
    // Day9 的序列化兼容入口；Day12 显式写入双槽。
    [SerializeField, HideInInspector]
    private MonoBehaviour _currentQuickItem;
    private PlayerStateMachine _player;

    public int CurrentSlotIndex { get; private set; }
    public int SlotCount => _quickItemSlots.Length;
    public IPlayerQuickItem CurrentItem => GetItem(CurrentSlotIndex);

    public event Action<IPlayerQuickItem> CurrentItemChanged;
    public event Action QuickItemsChanged;
    public bool CanSwitch => isActiveAndEnabled &&
        _player != null &&
        _player.InputReader != null &&
        _player.InputReader.isActiveAndEnabled &&
        Time.timeScale > 0f &&
        _player.Health != null &&
        !_player.Health.IsDead &&
        _player.CurrentState != null &&
        _player.CurrentState != _player.DeadState;

    private void Awake()
    {
        _player = GetComponent<PlayerStateMachine>();
        if (_quickItemSlots.Length == 0 &&
            _currentQuickItem != null)
        {
            _quickItemSlots = new[]
            {
                _currentQuickItem
            };
        }

        CurrentSlotIndex = Mathf.Clamp(_defaultSlot, 0, Mathf.Max(0, SlotCount - 1));
        if (_player == null ||
            CurrentItem == null)
        {
            Debug.LogError("PlayerItemController 缺少玩家或 Quick Item 槽配置。", this);
        }
    }

    private void OnEnable()
    {
        foreach (MonoBehaviour slot in _quickItemSlots)
        {
            if (slot is IPlayerQuickItem item)
            {
                item.ChargesChanged += HandleChargesChanged;
            }
        }
    }

    private void OnDisable()
    {
        foreach (MonoBehaviour slot in _quickItemSlots)
        {
            if (slot is IPlayerQuickItem item)
            {
                item.ChargesChanged -= HandleChargesChanged;
            }
        }
    }

    public IPlayerQuickItem GetItem(int index)
    {
        if (index >= 0 &&
            index < SlotCount &&
            _quickItemSlots[index] != null)
        {
            return _quickItemSlots[index] as IPlayerQuickItem;
        }
        else
        {
            return null;
        }
    }

    public bool TryUseCurrentItem()
    {
        return CanSwitch &&
            _player.CurrentState == _player.LocomotionState &&
            _player.Motor.IsGrounded &&
            CurrentItem != null &&
            CurrentItem.CanUse &&
            CurrentItem.TryUse(_player);
    }

    public bool TryUseItem()
    {
        return TryUseCurrentItem();
    }

    public bool SwitchNextItem()
    {
        return Switch(1);
    }

    public bool SwitchPreviousItem()
    {
        return Switch(-1);
    }

    private bool Switch(int direction)
    {
        if (!CanSwitch ||
            SlotCount < 2)
        {
            return false;
        }

        return EquipItemSlot((CurrentSlotIndex + direction + SlotCount) % SlotCount);
    }

    public bool EquipItemSlot(int index)
    {
        if (!CanSwitch ||
            GetItem(index) == null ||
            !GetItem(index).IsAvailable)
        {
            return false;
        }

        SelectSlot(index);
        return true;
    }

    private void SelectSlot(int index)
    {
        if (CurrentSlotIndex == index)
        {
            return;
        }

        CurrentSlotIndex = index;
        CurrentItemChanged?.Invoke(CurrentItem);
        QuickItemsChanged?.Invoke();
    }

    private void HandleChargesChanged(int current, int maximum)
    {
        QuickItemsChanged?.Invoke();
    }

    public void RefillRestItems()
    {
        foreach (MonoBehaviour slot in _quickItemSlots)
        {
            if (slot is IRestRefillable item)
            {
                item.Refill();
            }
        }
    }

    // 存档初始化可以早于玩家 Start，无需通过游戏中的切换条件。
    public void RestoreFromSave(GameSaveData save)
    {
        if (save == null)
        {
            return;
        }

        GetItem(0)?.RestoreCharges(save.hpFlaskCharges);
        GetItem(1)?.RestoreCharges(save.mpFlaskCharges);
        SelectSlot(GetItem(save.currentQuickItemSlot) != null ? save.currentQuickItemSlot : 0);
    }

    public void WriteToSave(GameSaveData save)
    {
        save.hpFlaskCharges = GetItem(0)?.CurrentCharges ?? -1;
        save.mpFlaskCharges = GetItem(1)?.CurrentCharges ?? -1;
        save.currentQuickItemSlot = CurrentSlotIndex;
        save.flaskCharges = save.hpFlaskCharges;
    }
}
