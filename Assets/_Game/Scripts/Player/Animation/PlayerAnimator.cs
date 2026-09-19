using UnityEngine;

[RequireComponent(typeof(PlayerMotor))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _dampTime = 0.1f;
    
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");

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
