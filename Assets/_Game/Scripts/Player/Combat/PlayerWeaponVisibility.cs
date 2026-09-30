using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWeaponVisibility : MonoBehaviour, IPlayerWeaponVisibility
{
    [SerializeField] private Renderer[] _weaponRenderers = Array.Empty<Renderer>();

    private bool[] _previousRendererStates;
    private bool _hidden;

    public void HideWeapon()
    {
        if (_hidden) return;
        _previousRendererStates = new bool[_weaponRenderers.Length];
        for (int index = 0; index < _weaponRenderers.Length; index++)
        {
            Renderer renderer = _weaponRenderers[index];
            if (renderer == null) continue;
            _previousRendererStates[index] = renderer.enabled;
            renderer.enabled = false;
        }
        _hidden = true;
    }

    public void ShowWeapon()
    {
        if (!_hidden) return;
        for (int index = 0; index < _weaponRenderers.Length; index++)
        {
            Renderer renderer = _weaponRenderers[index];
            if (renderer != null) renderer.enabled = _previousRendererStates[index];
        }
        _hidden = false;
    }

    private void OnDisable() => ShowWeapon();
}
