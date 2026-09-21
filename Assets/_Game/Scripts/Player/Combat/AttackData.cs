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

    [Header("Hit Window (Normalized)")]
    [SerializeField, Range(0f, 1f)] private float _hitWindowStart = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _hitWindowEnd = 0.55f;

    [Header("Completion & Recovery")]
    [Tooltip("动画完成判定点（normalizedTime），到此点视为挥砍结束")]
    [SerializeField, Range(0.1f, 1f)]
    private float _completionNormalizedTime = 0.6f;

    [Tooltip("完成点之后的额外后摇时间（秒），结束后才能衔接下一段或退出攻击")]
    [SerializeField, Min(0f)] private float _recoveryTime = 0.1f;

    [Header("Combo Input Window (Normalized)")]
    [Tooltip("允许缓存下一段攻击输入的开始点（normalizedTime）")]
    [SerializeField, Range(0f, 1f)] private float _comboInputStart = 0.1f;

    [Tooltip("允许缓存下一段攻击输入的结束点（normalizedTime）")]
    [SerializeField, Range(0f, 1f)] private float _comboInputEnd = 0.5f;

    [Header("Rotation Assist")]
    [Tooltip("攻击开始后允许向输入方向转向的持续时间（秒）")]
    [SerializeField, Min(0f)] private float _rotateAssistTime = 0.12f;

    [Header("Optional Motion (预留，暂未接入位移)")]
    [SerializeField, Min(0f)] private float _moveDistance = 0f;
    [SerializeField, Min(0f)] private float _forwardImpulse = 0f;

    public string AnimationStateName => _animationStateName;
    public float StartTimeOffset => _startTimeOffset;
    public int Damage => _damage;
    public float StaminaCost => _staminaCost;
    public float HitWindowStart => _hitWindowStart;
    public float HitWindowEnd => _hitWindowEnd;
    public float CompletionNormalizedTime => _completionNormalizedTime;
    public float RecoveryTime => _recoveryTime;
    public float ComboInputStart => _comboInputStart;
    public float ComboInputEnd => _comboInputEnd;
    public float RotateAssistTime => _rotateAssistTime;
    public float MoveDistance => _moveDistance;
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
    }
}
