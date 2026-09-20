using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAnimator))]
public sealed class PlayerCombat : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float _transitionDuration = 0.08f;

    [SerializeField, Range(0.5f, 1f)]
    private float _completionNormalizedTime = 0.95f;

    [SerializeField]
    private WeaponHitbox _weaponHitbox;

    [SerializeField, Min(1)]
    private int _lightAttackDamage = 25;

    [SerializeField, Range(0f, 1f)]
    private float _hitWindowStart = 0.25f;

    [SerializeField, Range(0f, 1f)]
    private float _hitWindowEnd = 0.55f;

    private PlayerAnimator _playerAnimator;
    private bool _hitboxActive;

    private void Awake()
    {
        _playerAnimator = GetComponent<PlayerAnimator>();

        if (_weaponHitbox == null)
        {
            _weaponHitbox = GetComponentInChildren<WeaponHitbox>(true);
        }

        if (_weaponHitbox == null)
        {
            Debug.LogError(
                "PlayerCombat 找不到 WeaponHitbox。",
                this
            );
        }
    }

    public void StartLightAttack()
    {
        CloseHitWindow();
        _playerAnimator.PlayLightAttack(_transitionDuration);
    }

    public void TickLightAttack()
    {
        if (
            _weaponHitbox == null ||
            !_playerAnimator.TryGetLightAttackNormalizedTime(
                out float normalizedTime
            )
        )
        {
            return;
        }

        bool shouldBeActive =
            normalizedTime >= _hitWindowStart &&
            normalizedTime < _hitWindowEnd;

        if (shouldBeActive && !_hitboxActive)
        {
            _weaponHitbox.BeginAttack(_lightAttackDamage);
            _hitboxActive = true;
        }
        else if (!shouldBeActive && _hitboxActive)
        {
            CloseHitWindow();
        }
    }

    public bool IsLightAttackFinished()
    {
        return _playerAnimator.IsLightAttackFinished(
            _completionNormalizedTime
        );
    }

    public void FinishLightAttack()
    {
        CloseHitWindow();
        _playerAnimator.PlayLocomotion(_transitionDuration);
    }

    private void CloseHitWindow()
    {
        if (_weaponHitbox != null)
            _weaponHitbox.EndAttack();

        _hitboxActive = false;
    }

    private void OnDisable()
    {
        CloseHitWindow();
    }

    private void OnValidate()
    {
        _hitWindowEnd = Mathf.Max(
            _hitWindowStart,
            _hitWindowEnd
        );
    }
}
