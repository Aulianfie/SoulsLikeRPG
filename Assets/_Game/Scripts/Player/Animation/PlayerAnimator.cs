using UnityEngine;

[RequireComponent(typeof(PlayerMotor))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _dampTime = 0.1f;
    
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int LocomotionStateHash =
        Animator.StringToHash("Base Layer.Locomotion");
    private static readonly int DodgeStateHash =
        Animator.StringToHash("Base Layer.Dodge");
    private static readonly int HurtStateHash =
        Animator.StringToHash("Base Layer.Hurt");
    private static readonly int DeadStateHash =
        Animator.StringToHash("Base Layer.Dead");

    private const int BaseLayerIndex = 0;

    private PlayerMotor _motor;
    private int _currentAttackStateHash;

    private void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        if (_animator == null)
        {
            Debug.LogError( "PlayerAnimator 没有配置 Animator 引用。", this );
            enabled = false;
        }
    }

    private void Update()
    {
        _animator.SetFloat(
            MoveSpeedHash,
            _motor.HorizontalSpeed,
            _dampTime,
            Time.deltaTime
        );

        _animator.SetBool(GroundedHash, _motor.IsGrounded);
        _animator.SetFloat(VerticalSpeedHash, _motor.VerticalVelocity);
    }

    /// <summary>
    /// 播放 Base Layer 下指定名称的轻攻击状态（例如 Attack1，来自 AttackData）。
    /// startTimeOffset：从动画的第几秒开始播放（用于连击时跳过前摇），0 = 从头。
    /// 返回 false 表示状态名无效或 Animator 上不存在该状态（此时不会切换动画），
    /// 调用方必须安全中止攻击流程。
    /// </summary>
    public bool PlayLightAttack(
        string animationStateName,
        float transitionDuration,
        float startTimeOffset
    )
    {
        if (string.IsNullOrEmpty(animationStateName))
        {
            Debug.LogError(
                $"PlayerAnimator（{gameObject.name}）收到空的攻击状态名。",
                this
            );
            return false;
        }

        if (_animator == null)
        {
            Debug.LogError(
                $"PlayerAnimator（{gameObject.name}）没有可用的 Animator。",
                this
            );
            return false;
        }

        string fullPathName = "Base Layer." + animationStateName;

        // HasState 要求 stateID 为状态名哈希，个别版本对 fullPathHash 的
        // 处理不一致，这里两种哈希都检查，避免误判"状态不存在"。
        // 状态名写错时两个哈希都不存在，仍会正确报错。
        bool stateExists =
            _animator.HasState(
                BaseLayerIndex,
                Animator.StringToHash(animationStateName)
            ) ||
            _animator.HasState(
                BaseLayerIndex,
                Animator.StringToHash(fullPathName)
            );

        if (!stateExists)
        {
            Debug.LogError(
                $"PlayerAnimator（{gameObject.name}）在 Animator 中找不到 " +
                $"状态 \"{fullPathName}\"（AnimationStateName = " +
                $"\"{animationStateName}\"）。请检查 AttackData 与 " +
                "AnimatorController 的状态名是否一致。",
                this
            );
            return false;
        }

        _currentAttackStateHash = Animator.StringToHash(fullPathName);

        _animator.CrossFadeInFixedTime(
            _currentAttackStateHash,
            transitionDuration,
            BaseLayerIndex,
            startTimeOffset
        );

        return true;
    }
    /// <summary>
    /// 判断当前动画状态是否为轻攻击状态，并且动画播放的归一化时间是否达到指定的完成时间。
    /// 1. 检查当前动画是否处于过渡状态，如果是，则返回 false，因为动画还没有完全进入轻攻击状态。
    /// 2. 获取当前动画状态信息，并检查它的 fullPathHash 是否与轻攻击状态的哈希值匹配。
    /// 3. 如果匹配，则检查 normalizedTime 是否大于或等于指定的完成时间，如果是，则返回 true，表示轻攻击动画已经完成。
    /// 4. 如果不匹配或 normalizedTime 小于完成时间，则返回 false，表示轻攻击动画还没有完成。
    /// </summary>
    /// <param name="completionNormalizedTime"></param>
    /// <returns></returns>
    public bool IsLightAttackFinished(float completionNormalizedTime)
    {
        return
            TryGetLightAttackNormalizedTime(out float normalizedTime) &&
            normalizedTime >= completionNormalizedTime;
    }

    public bool TryGetLightAttackNormalizedTime(
        out float normalizedTime
    )
    {
        normalizedTime = 0f;

        if (_animator.IsInTransition(BaseLayerIndex))
            return false;

        AnimatorStateInfo stateInfo =
            _animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);

        if (stateInfo.fullPathHash != _currentAttackStateHash)
            return false;

        normalizedTime = stateInfo.normalizedTime;
        return true;
    }

    public void PlayLocomotion(float transitionDuration)
    {
        _animator.CrossFadeInFixedTime(
            LocomotionStateHash,
            transitionDuration,
            BaseLayerIndex
        );
    }

    public void PlayDodge(float transitionDuration)
    {
        _animator.CrossFadeInFixedTime(
            DodgeStateHash,
            transitionDuration,
            BaseLayerIndex
        );
    }

    public bool IsDodgeFinished(float completionNormalizedTime)
    {
        return
            TryGetDodgeNormalizedTime(out float normalizedTime) &&
            normalizedTime >= completionNormalizedTime;
    }

    public bool TryGetDodgeNormalizedTime(out float normalizedTime)
    {
        normalizedTime = 0f;

        if (_animator.IsInTransition(BaseLayerIndex))
            return false;

        AnimatorStateInfo stateInfo =
            _animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);

        if (stateInfo.fullPathHash != DodgeStateHash)
            return false;

        normalizedTime = stateInfo.normalizedTime;
        return true;
    }

    public void PlayHurt()
    {
        // 受击是 10 帧短动画，用零时长的 CrossFade 做瞬时切换：
        // 混合时间会稀释前几帧，让受击反应看起来慢半拍。
        _animator.CrossFadeInFixedTime(
            HurtStateHash,
            0f,
            BaseLayerIndex,
            0f
        );
    }

    public bool IsHurtFinished(float completionNormalizedTime)
    {
        if (_animator.IsInTransition(BaseLayerIndex))
            return false;

        AnimatorStateInfo stateInfo =
            _animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);

        return
            stateInfo.fullPathHash == HurtStateHash &&
            stateInfo.normalizedTime >= completionNormalizedTime;
    }

    public void PlayDeath(float transitionDuration)
    {
        _animator.CrossFadeInFixedTime(
            DeadStateHash,
            transitionDuration,
            BaseLayerIndex
        );
    }

    /// <summary>
    /// 角色或脚本被关闭后，将动画速度恢复为零，避免重新启用时保留 Run 参数
    /// </summary>
    private void OnDisable()
    {
        if (_animator != null)
        {
            _animator.SetFloat(MoveSpeedHash, 0f);
            _animator.SetBool(GroundedHash, true);
            _animator.SetFloat(VerticalSpeedHash, 0f);
        }
    }
}
