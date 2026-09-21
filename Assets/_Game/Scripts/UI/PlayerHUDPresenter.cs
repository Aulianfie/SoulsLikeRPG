using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHUDView))]
public sealed class PlayerHUDPresenter : MonoBehaviour
{
    [SerializeField] private PlayerHUDView _view;
    [SerializeField] private PlayerHealth _health;
    [SerializeField] private PlayerMana _mana;
    [SerializeField] private PlayerStamina _stamina;

    private bool _hasStarted;

    private void Awake()
    {
        if (_view == null)
        {
            _view = GetComponent<PlayerHUDView>();
        }
    }

    private void OnEnable()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("PlayerHUDPresenter 的 HUD 或玩家数据引用未配置完整。", this);
            return;
        }

        _health.HealthChanged += HandleHealthChanged;
        _mana.ManaChanged += HandleManaChanged;
        _stamina.StaminaChanged += HandleStaminaChanged;

        if (_hasStarted)
        {
            Refresh();
        }
    }

    private void Start()
    {
        _hasStarted = true;

        if (HasRequiredReferences())
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.HealthChanged -= HandleHealthChanged;

        if (_mana != null)
            _mana.ManaChanged -= HandleManaChanged;

        if (_stamina != null)
            _stamina.StaminaChanged -= HandleStaminaChanged;
    }

    private void Refresh()
    {
        _view.SetHealth(_health.CurrentHealth, _health.MaxHealth);
        _view.SetMana(_mana.CurrentMana, _mana.MaxMana);
        _view.SetStamina(_stamina.CurrentStamina, _stamina.MaxStamina);
    }

    private bool HasRequiredReferences()
    {
        return _view != null && _health != null && _mana != null && _stamina != null;
    }

    private void HandleHealthChanged(int currentValue, int maxValue)
    {
        _view.SetHealth(currentValue, maxValue);
    }

    private void HandleManaChanged(float currentValue, float maxValue)
    {
        _view.SetMana(currentValue, maxValue);
    }

    private void HandleStaminaChanged(float currentValue, float maxValue)
    {
        _view.SetStamina(currentValue, maxValue);
    }
}
