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
    private static readonly int LightAttackStateHash =
        Animator.StringToHash("Base Layer.LightAttack");

    private const int BaseLayerIndex = 0;

    private PlayerMotor _motor;

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

    public void PlayLightAttack(float transitionDuration)
    {
        _animator.CrossFadeInFixedTime(
            LightAttackStateHash,
            transitionDuration,
            BaseLayerIndex
        );
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
        if (_animator.IsInTransition(BaseLayerIndex))
            return false;

        AnimatorStateInfo stateInfo =
            _animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);

        return
            stateInfo.fullPathHash == LightAttackStateHash &&
            stateInfo.normalizedTime >= completionNormalizedTime;
    }

    public void PlayLocomotion(float transitionDuration)
    {
        _animator.CrossFadeInFixedTime(
            LocomotionStateHash,
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
