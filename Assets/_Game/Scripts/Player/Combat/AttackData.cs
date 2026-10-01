using UnityEngine;

/// <summary>
/// 单段攻击的配置数据。
/// 只保存"数据"，不保存任何运行时状态（连击索引、计时器等都放在 PlayerCombat 里）。
/// </summary>
[CreateAssetMenu(
    fileName = "AttackData",
    menuName = "SoulsLike RPG/Combat/Attack Data")]
public sealed class AttackData : ScriptableObject
{
    [Header("Animation")]
    [Tooltip("AnimatorController 中 Base Layer 下的状态名，例如 Attack1")]
    [SerializeField] private string _animationStateName = "Attack1";

    [Tooltip(
        "动画起始播放入点（秒，动画时间轴上的绝对时间）。\n" +
        "连击切入该段时从该时间点开始播放，用于跳过长前摇；0 = 从动画开头播放。\n" +
        "换算：秒 = 归一化时间 × 动画时长")]
    [SerializeField, Min(0f)] private float _startTimeOffset = 0f;

    [Header("Damage & Stamina")]
    [SerializeField, Min(0)] private int _damage = 25;
    [SerializeField, Min(0f)] private float _staminaCost = 20f;
    [Tooltip("战技消耗的蓝量；普通攻击和跳劈通常为0。")]
    [SerializeField, Min(0f)] private float _manaCost;

    [Header("Hit Window (Normalized)")]
    [SerializeField, Range(0f, 1f)] private float _hitWindowStart = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _hitWindowEnd = 0.55f;

    [Header("Completion & Recovery")]
    [Tooltip("动画完成判定点（normalizedTime），到此点视为挥砍结束")]
    [SerializeField, Range(0.1f, 1f)]
    private float _completionNormalizedTime = 0.6f;

    [Tooltip("未衔接下一段时，完成点之后等待的收招时间（秒）。已缓存的连招不等待此时间。")]
    [SerializeField, Min(0f)] private float _recoveryTime = 0.1f;

    [Header("Combo Input Window (Normalized)")]
    [Tooltip("允许缓存下一段攻击输入的开始点（normalizedTime）")]
    [SerializeField, Range(0f, 1f)] private float _comboInputStart = 0.1f;

    [Tooltip("允许缓存下一段攻击输入的结束点（normalizedTime）")]
    [SerializeField, Range(0f, 1f)] private float _comboInputEnd = 0.5f;

    [Header("Combo Transition (Normalized)")]
    [Tooltip(
        "已缓存下一击时，允许切入下一段的动画位置；越小衔接越快。\n" +
        "独立于输入窗口和 Recovery Time；到点后收到窗口内输入也会立即衔接。\n" +
        "0 = 沿用 Combo Input End（兼容旧配置）。有效点限制在命中窗口结束与完成点之间。")]
    [SerializeField, Range(0f, 1f)] private float _comboTransitionPoint = 0f;

    [Header("Dodge Cancel Window (Normalized)")]
    [SerializeField, Range(0f, 1f)] private float _dodgeCancelStart = 0.55f;
    [SerializeField, Range(0f, 1f)] private float _dodgeCancelEnd = 0.90f;

    [Header("Rotation Assist")]
    [Tooltip("攻击开始后允许向输入方向转向的持续时间（秒）")]
    [SerializeField, Min(0f)] private float _rotateAssistTime = 0.12f;

    [Header("Attack Motion (战技前进位移)")]
    [Tooltip("战技在指定动画时段内尝试前进的总距离，单位米；0为原地。碰撞可能使实际移动距离更短。")]
    [SerializeField, Min(0f)] private float _moveDistance = 0f;
    [Tooltip("开始前进的归一化动画进度。")]
    [SerializeField, Range(0f, 1f)] private float _motionStart = 0.18f;
    [Tooltip("结束前进的归一化动画进度。收招阶段停止位移。")]
    [SerializeField, Range(0f, 1f)] private float _motionEnd = 0.5f;

    [Header("Jump Strike Alignment (跳劈挥砍与落地衔接)")]
    [Tooltip("前摇在抬刀姿态等待，下降到地面附近后继续挥砍；等待时角色仍受重力影响。")]
    [SerializeField] private bool _alignJumpStrikeToLanding;
    [SerializeField, Range(0f, 1f)] private float _jumpWindupHoldPoint = 0.25f;
    [Tooltip("下落时离地不超过此距离才释放挥砍。地面检测忽略敌人碰撞层。")]
    [SerializeField, Min(0f)] private float _jumpStrikeGroundDistance = 0.9f;

    [Header("Optional Impulse (预留)")]
    [SerializeField, Min(0f)] private float _forwardImpulse = 0f;

    public string AnimationStateName => _animationStateName;
    public float StartTimeOffset => _startTimeOffset;
    public int Damage => _damage;
    public float StaminaCost => _staminaCost;
    public float ManaCost => _manaCost;
    public float HitWindowStart => _hitWindowStart;
    public float HitWindowEnd => _hitWindowEnd;
    public float CompletionNormalizedTime => _completionNormalizedTime;
    public float RecoveryTime => _recoveryTime;
    public float ComboInputStart => _comboInputStart;
    public float ComboInputEnd => _comboInputEnd;
    public float ComboTransitionPoint => Mathf.Clamp(
        _comboTransitionPoint > 0f ? _comboTransitionPoint : _comboInputEnd,
        _hitWindowEnd,
        _completionNormalizedTime
    );
    public float DodgeCancelStart => _dodgeCancelStart;
    public float DodgeCancelEnd => _dodgeCancelEnd;
    public float RotateAssistTime => _rotateAssistTime;
    public float MoveDistance => _moveDistance;
    public float MotionStart => _motionStart;
    public float MotionEnd => _motionEnd;
    public bool AlignJumpStrikeToLanding => _alignJumpStrikeToLanding;
    public float JumpWindupHoldPoint => _jumpWindupHoldPoint;
    public float JumpStrikeGroundDistance => _jumpStrikeGroundDistance;
    public float ForwardImpulse => _forwardImpulse;

    private void OnValidate()
    {
        _hitWindowEnd = Mathf.Max(_hitWindowStart, _hitWindowEnd);
        _completionNormalizedTime = Mathf.Max(
            _hitWindowEnd,
            _completionNormalizedTime
        );
        _comboInputEnd = Mathf.Max(_comboInputStart, _comboInputEnd);
        _comboInputStart = Mathf.Min(
            _comboInputStart,
            _comboInputEnd
        );
        if (_comboTransitionPoint > 0f)
        {
            _comboTransitionPoint = Mathf.Clamp(
                _comboTransitionPoint,
                _hitWindowEnd,
                _completionNormalizedTime
            );
        }
        _dodgeCancelEnd = Mathf.Max(_dodgeCancelStart, _dodgeCancelEnd);
        _motionEnd = Mathf.Max(_motionStart, _motionEnd);
        _jumpWindupHoldPoint = Mathf.Min(_jumpWindupHoldPoint, Mathf.Max(0f, _hitWindowStart - .02f));
    }
}
