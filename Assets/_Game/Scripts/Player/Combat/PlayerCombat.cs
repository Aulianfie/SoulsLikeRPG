using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAnimator))]
public sealed class PlayerCombat : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float _transitionDuration = 0.08f;

    [SerializeField]
    private WeaponHitbox _weaponHitbox;

    [Tooltip("轻攻击连招配置：每段参数（动画/伤害/体力/连击窗口/后摇等）见 AttackData")]
    [SerializeField]
    private AttackCombo _attackCombo;

    private PlayerAnimator _playerAnimator;
    private bool _hitboxActive;
    private float _recoveryTimer;

    /// <summary>
    /// 当前连击段索引（0-based），0 表示第一段。
    /// </summary>
    public int ComboIndex { get; private set; }

    /// <summary>
    /// 是否已缓存下一次攻击输入（每段最多缓存一次）。
    /// </summary>
    public bool AttackQueued { get; private set; }

    public int MaxComboCount => _attackCombo != null
        ? _attackCombo.Count
        : 0;

    /// <summary>是否还有下一段攻击可以衔接。</summary>
    public bool HasNextAttack =>
        _attackCombo != null && ComboIndex + 1 < _attackCombo.Count;

    /// <summary>当前段的配置数据；未配置连招时返回 null。</summary>
    public AttackData CurrentAttack =>
        _attackCombo != null ? _attackCombo.Get(ComboIndex) : null;

    /// <summary>下一段攻击的体力消耗；没有下一段时返回 0。</summary>
    public float NextAttackStaminaCost
    {
        get
        {
            AttackData next = _attackCombo != null
                ? _attackCombo.Get(ComboIndex + 1)
                : null;

            return next != null ? next.StaminaCost : 0f;
        }
    }

    /// <summary>第一段攻击的体力消耗（供 Locomotion 进入攻击前检查）。</summary>
    public float FirstAttackStaminaCost
    {
        get
        {
            AttackData first = _attackCombo != null
                ? _attackCombo.Get(0)
                : null;

            return first != null ? first.StaminaCost : 0f;
        }
    }

    /// <summary>当前段允许的转向辅助时长（秒）。</summary>
    public float CurrentRotateAssistTime =>
        CurrentAttack != null ? CurrentAttack.RotateAssistTime : 0f;

    /// <summary>当前动画是否处在"允许缓存下一段输入"的连击窗口内。</summary>
    public bool IsInComboInputWindow
    {
        get
        {
            AttackData data = CurrentAttack;

            if (
                data == null ||
                !_playerAnimator.TryGetLightAttackNormalizedTime(
                    out float normalizedTime
                )
            )
            {
                return false;
            }

            return
                normalizedTime >= data.ComboInputStart &&
                normalizedTime <= data.ComboInputEnd;
        }
    }

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

        if (_attackCombo == null || _attackCombo.Count == 0)
        {
            Debug.LogError(
                "PlayerCombat 没有配置 AttackCombo，攻击无法播放。",
                this
            );
        }
    }

    /// <summary>
    /// 开始第一段攻击，并重置连击状态。
    /// </summary>
    public void StartLightAttack()
    {
        ComboIndex = 0;
        AttackQueued = false;
        _recoveryTimer = 0f;
        CloseHitWindow();
        PlayCurrentAttack();
    }

    /// <summary>
    /// 缓存一次下一段攻击输入。每段攻击最多缓存一次，
    /// 由调用方在连击窗口与体力检查通过后调用。
    /// </summary>
    public void QueueNextAttack()
    {
        AttackQueued = true;
    }

    /// <summary>
    /// 当前段结束（含后摇）后尝试进入下一段。
    /// 返回 true 表示已切换到下一段动画，攻击状态应继续保持；
    /// 返回 false 表示连击结束，comboIndex 已重置。
    /// </summary>
    public bool TryStartNextComboHit()
    {
        if (!AttackQueued || !HasNextAttack)
        {
            ResetCombo();
            return false;
        }

        AttackQueued = false;
        ComboIndex++;
        _recoveryTimer = 0f;
        CloseHitWindow();
        PlayCurrentAttack();
        return true;
    }

    /// <summary>
    /// 重置连击进度（Hurt / Dodge / Dead / 连击结束都会走这里）。
    /// </summary>
    public void ResetCombo()
    {
        ComboIndex = 0;
        AttackQueued = false;
        _recoveryTimer = 0f;
    }

    public void TickLightAttack()
    {
        AttackData data = CurrentAttack;

        if (data == null)
            return;

        TickHitWindow(data);
        TickRecovery(data);
    }

    /// <summary>动画是否到达本段的完成点（挥砍结束）。</summary>
    public bool IsLightAttackFinished()
    {
        AttackData data = CurrentAttack;

        if (data == null)
            return true;

        return _playerAnimator.IsLightAttackFinished(
            data.CompletionNormalizedTime
        );
    }

    /// <summary>完成点之后的额外后摇是否结束。</summary>
    public bool IsRecoveryDone()
    {
        AttackData data = CurrentAttack;

        if (data == null)
            return true;

        return _recoveryTimer >= data.RecoveryTime;
    }

    public void FinishLightAttack()
    {
        CloseHitWindow();
        ResetCombo();
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

    private void PlayCurrentAttack()
    {
        AttackData data = CurrentAttack;

        if (data == null)
        {
            Debug.LogError(
                "PlayerCombat 当前连击段没有 AttackData。",
                this
            );
            return;
        }

        _playerAnimator.PlayLightAttack(
            data.AnimationStateName,
            _transitionDuration
        );
    }

    private void TickHitWindow(AttackData data)
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
            normalizedTime >= data.HitWindowStart &&
            normalizedTime < data.HitWindowEnd;

        if (shouldBeActive && !_hitboxActive)
        {
            _weaponHitbox.BeginAttack(data.Damage);
            _hitboxActive = true;
        }
        else if (!shouldBeActive && _hitboxActive)
        {
            CloseHitWindow();
        }
    }

    private void TickRecovery(AttackData data)
    {
        if (_recoveryTimer >= data.RecoveryTime)
            return;

        if (
            !_playerAnimator.IsLightAttackFinished(
                data.CompletionNormalizedTime
            )
        )
        {
            return;
        }

        _recoveryTimer += Time.deltaTime;
    }
}
