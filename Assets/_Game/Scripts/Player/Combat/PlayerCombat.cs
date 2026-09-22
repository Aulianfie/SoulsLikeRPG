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

    /// <summary>
    /// 第一段攻击是否已配置 AttackData。
    /// 用于区分"合法的 0 消耗"与"未配置时的 fallback"。
    /// </summary>
    public bool HasFirstAttack =>
        _attackCombo != null && _attackCombo.Get(0) != null;

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

    public bool IsInDodgeCancelWindow
    {
        get
        {
            AttackData data = CurrentAttack;
            if (data == null ||
                !_playerAnimator.TryGetLightAttackNormalizedTime(
                    out float normalizedTime))
                return false;

            return normalizedTime >= data.DodgeCancelStart &&
                normalizedTime <= data.DodgeCancelEnd;
        }
    }

    /// <summary>
    /// 是否已到达"允许正式衔接下一段攻击"的动画位置。
    /// ComboTransitionPoint 复用手感窗口终点 ComboInputEnd：
    /// 已缓存的下一段会在此点直接切段，不再等待完成点与后摇。
    /// </summary>
    public bool IsComboTransitionReached
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

            return normalizedTime >= data.ComboInputEnd;
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
    /// 返回 false 表示 AttackData 缺失或 Animator State 不存在（播放失败），
    /// 此时连击已重置、命中窗口已关闭，调用方应立即安全退出攻击流程。
    /// </summary>
    public bool StartLightAttack()
    {
        ComboIndex = 0;
        AttackQueued = false;
        _recoveryTimer = 0f;
        CloseHitWindow();

        if (PlayCurrentAttack())
            return true;

        ResetCombo();
        return false;
    }

    /// <summary>
    /// 缓存一次下一段攻击输入。每段攻击最多缓存一次，
    /// 由调用方在连击窗口与体力预检查通过后调用（此阶段不扣体力）。
    /// </summary>
    public void QueueNextAttack()
    {
        AttackQueued = true;
    }

    /// <summary>
    /// 到达 ComboTransitionPoint（或完成点）后尝试进入已缓存的下一段攻击。
    /// 返回 true 表示已切换到下一段动画，攻击状态应继续保持；
    /// 返回 false 表示没有可衔接的下一段（连击已重置），
    /// 或下一段动画播放失败（命中窗口已关闭、连击已重置，调用方应立即退出攻击流程）。
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

        if (PlayCurrentAttack())
            return true;

        ResetCombo();
        return false;
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

    /// <summary>
    /// 播放当前段的攻击动画。
    /// 返回 false 表示 AttackData 缺失或动画状态不存在，调用方必须中止攻击流程。
    /// </summary>
    private bool PlayCurrentAttack()
    {
        AttackData data = CurrentAttack;

        if (data == null)
        {
            Debug.LogError(
                "PlayerCombat 当前连击段没有 AttackData。",
                this
            );
            return false;
        }

        if (
            _playerAnimator.PlayLightAttack(
                data.AnimationStateName,
                _transitionDuration,
                data.StartTimeOffset
            )
        )
        {
            return true;
        }

        Debug.LogError(
            $"PlayerCombat 播放攻击失败：AttackData \"{data.name}\" 配置的 " +
            $"AnimationStateName \"{data.AnimationStateName}\" 在 Animator " +
            $"Base Layer 中不存在（查找状态：\"Base Layer.{data.AnimationStateName}\"）。" +
            "已中止本次攻击，请检查 AttackCombo 配置。",
            this
        );
        return false;
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
