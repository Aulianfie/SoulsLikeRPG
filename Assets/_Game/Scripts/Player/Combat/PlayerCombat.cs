using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAnimator))]
public sealed class PlayerCombat : MonoBehaviour
{
    [System.Serializable]
    private struct LightAttackTiming
    {
        [SerializeField, Range(0f, 1f)]
        private float _hitWindowStart;

        [SerializeField, Range(0f, 1f)]
        private float _hitWindowEnd;

        [SerializeField, Range(0.1f, 1f)]
        private float _completionNormalizedTime;

        public float HitWindowStart => _hitWindowStart;
        public float HitWindowEnd => _hitWindowEnd;
        public float CompletionNormalizedTime =>
            _completionNormalizedTime;

        public LightAttackTiming(
            float hitWindowStart,
            float hitWindowEnd,
            float completionNormalizedTime
        )
        {
            _hitWindowStart = hitWindowStart;
            _hitWindowEnd = hitWindowEnd;
            _completionNormalizedTime = completionNormalizedTime;
        }

        public void Validate()
        {
            _hitWindowEnd = Mathf.Max(
                _hitWindowStart,
                _hitWindowEnd
            );
            _completionNormalizedTime = Mathf.Max(
                _hitWindowEnd,
                _completionNormalizedTime
            );
        }
    }

    [SerializeField, Min(0f)]
    private float _transitionDuration = 0.08f;

    [SerializeField]
    private WeaponHitbox _weaponHitbox;

    [SerializeField, Min(1)]
    private int _lightAttackDamage = 25;

    [SerializeField, Min(1)]
    private int _maxComboCount = 5;

    [Header("Attack Timing (Normalized 0-1)")]
    [SerializeField]
    private LightAttackTiming[] _lightAttackTimings =
    {
        new LightAttackTiming(0.14f, 0.27f, 0.46f),
        new LightAttackTiming(0.30f, 0.46f, 0.56f),
        new LightAttackTiming(0.22f, 0.38f, 0.48f),
        new LightAttackTiming(0.23f, 0.38f, 0.44f),
        new LightAttackTiming(0.22f, 0.38f, 0.50f)
    };

    private PlayerAnimator _playerAnimator;
    private bool _hitboxActive;

    /// <summary>
    /// 当前连击段索引（0-based），0 表示第一段。
    /// </summary>
    public int ComboIndex { get; private set; }

    /// <summary>
    /// 是否已缓存下一次攻击输入（每段最多缓存一次）。
    /// </summary>
    public bool AttackQueued { get; private set; }

    public int MaxComboCount => Mathf.Min(
        _maxComboCount,
        _lightAttackTimings == null
            ? 0
            : _lightAttackTimings.Length
    );

    private void Awake()
    {
        EnsureAttackTimings();
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

    /// <summary>
    /// 开始第一段攻击，并重置连击状态。
    /// </summary>
    public void StartLightAttack()
    {
        ComboIndex = 0;
        AttackQueued = false;
        CloseHitWindow();
        _playerAnimator.PlayLightAttack(ComboIndex, _transitionDuration);
    }

    /// <summary>
    /// 缓存一次下一段攻击输入。每段攻击最多缓存一次，
    /// 由调用方在体力检查通过后调用。
    /// </summary>
    public void QueueNextAttack()
    {
        AttackQueued = true;
    }

    /// <summary>
    /// 当前段结束时尝试进入下一段。
    /// 返回 true 表示已切换到下一段动画，攻击状态应继续保持；
    /// 返回 false 表示连击结束，comboIndex 已重置。
    /// </summary>
    public bool TryStartNextComboHit()
    {
        if (!AttackQueued || ComboIndex + 1 >= MaxComboCount)
        {
            ResetCombo();
            return false;
        }

        AttackQueued = false;
        ComboIndex++;
        CloseHitWindow();
        _playerAnimator.PlayLightAttack(ComboIndex, _transitionDuration);
        return true;
    }

    /// <summary>
    /// 重置连击进度（Hurt / Dodge / Dead / 连击结束都会走这里）。
    /// </summary>
    public void ResetCombo()
    {
        ComboIndex = 0;
        AttackQueued = false;
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

        LightAttackTiming timing = GetCurrentAttackTiming();
        bool shouldBeActive =
            normalizedTime >= timing.HitWindowStart &&
            normalizedTime < timing.HitWindowEnd;

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
        LightAttackTiming timing = GetCurrentAttackTiming();
        return _playerAnimator.IsLightAttackFinished(
            timing.CompletionNormalizedTime
        );
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

    private void OnValidate()
    {
        EnsureAttackTimings();

        for (int i = 0; i < _lightAttackTimings.Length; i++)
        {
            LightAttackTiming timing = _lightAttackTimings[i];
            timing.Validate();
            _lightAttackTimings[i] = timing;
        }
    }

    private LightAttackTiming GetCurrentAttackTiming()
    {
        EnsureAttackTimings();
        int timingIndex = Mathf.Clamp(
            ComboIndex,
            0,
            _lightAttackTimings.Length - 1
        );
        return _lightAttackTimings[timingIndex];
    }

    private void EnsureAttackTimings()
    {
        if (
            _lightAttackTimings == null ||
            _lightAttackTimings.Length == 0
        )
        {
            _lightAttackTimings = BuildDefaultAttackTimings();
        }
    }

    private static LightAttackTiming[] BuildDefaultAttackTimings()
    {
        return new[]
        {
            new LightAttackTiming(0.14f, 0.27f, 0.46f),
            new LightAttackTiming(0.30f, 0.46f, 0.56f),
            new LightAttackTiming(0.22f, 0.38f, 0.48f),
            new LightAttackTiming(0.23f, 0.38f, 0.44f),
            new LightAttackTiming(0.22f, 0.38f, 0.50f)
        };
    }
}
