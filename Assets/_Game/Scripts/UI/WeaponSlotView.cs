using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WeaponSlotView : MonoBehaviour
{
    [SerializeField] private Image _icon;

    public void SetWeapon(WeaponData weapon)
    {
        if (_icon == null) return;
        _icon.sprite = weapon != null ? weapon.Icon : null;
        _icon.enabled = _icon.sprite != null;
    }
}
