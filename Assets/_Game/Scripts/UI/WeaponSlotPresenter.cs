using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WeaponSlotView))]
public sealed class WeaponSlotPresenter : MonoBehaviour
{
    [SerializeField] private PlayerEquipment _equipment;
    private WeaponSlotView _view;

    private void Awake() => _view = GetComponent<WeaponSlotView>();

    private void OnEnable()
    {
        if (_equipment == null)
        {
            Debug.LogError("WeaponSlotPresenter 缺少玩家装备引用。", this);
            return;
        }
        _equipment.WeaponChanged += Refresh;
        Refresh(_equipment.CurrentWeapon);
    }

    // 装备的默认槽在 Start 中初始化；同时覆盖组件启动顺序的差异。
    private void Start()
    {
        if (_equipment != null) Refresh(_equipment.CurrentWeapon);
    }

    private void OnDisable()
    {
        if (_equipment != null) _equipment.WeaponChanged -= Refresh;
    }

    private void Refresh(WeaponData weapon) => _view.SetWeapon(weapon);
}
