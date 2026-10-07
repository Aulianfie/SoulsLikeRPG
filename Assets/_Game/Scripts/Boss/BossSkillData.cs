using UnityEngine;

/// <summary>
/// 记录某个技能属于哪大类技能的
/// Ordinary: 普通攻击
/// GroundSlam: 左右脚踩地与跳跃落地攻击
/// Dash: 冲刺
/// Whirlwind: 旋转冲刺攻击
/// ThrowStone: 投石
/// </summary>
public enum BossSkillFamily
{
    Ordinary = 0,
    GroundSlam = 1,
    Dash = 2,
    Whirlwind = 3,
    // 4 原为 Jump，现有 Jump 配置迁移到 GroundSlam；保留其他类别的序列化数值。
    ThrowStone = 5
}

/// <summary>
/// 技能的攻击方向要求
/// Any: 任意方向
/// Left: 仅左侧
/// Right: 仅右侧
/// </summary>
public enum BossSkillSide
{
    Any,
    Left,
    Right
}

/// <summary>
/// 技能的伤害类型
/// Hands: 双手攻击
/// GroundPulse: 地面脉冲
/// BodySweep: 躯干扫击
/// Projectile: 投射物
/// </summary>
public enum BossDamageKind
{
    Hands,
    GroundPulse,
    BodySweep,
    Projectile
}

[CreateAssetMenu(menuName = "SoulsLike RPG/Boss/Skill")]
public sealed class BossSkillData : ScriptableObject
{
    // Serialized fields
    [SerializeField]
    private string _id;
    [SerializeField]
    private string _stateName;
    [SerializeField]
    private BossSkillFamily _family;
    [SerializeField]
    private BossSkillSide _side;

    /// <summary>该技能使用哪一种伤害判定方式。</summary>
    [SerializeField]
    private BossDamageKind _damageKind;

    // ==================== 技能选择条件 ====================

    /// <summary>技能允许使用的最小距离。</summary>
    [SerializeField, Min(0)]
    private float _minRange;
    /// <summary>技能允许使用的最大距离。</summary>
    [SerializeField, Min(.1f)]
    private float _maxRange = 4;
    /// <summary>
    /// 技能允许使用的最大朝向夹角。
    /// 玩家偏离 Boss 正面过多时，该技能不会进入候选池。
    /// </summary>
    [SerializeField, Range(0, 180)]
    private float _maxAngle = 85;
    /// <summary>
    /// 技能基础权重。
    /// Selector 会在此基础上结合距离、历史技能等信息动态调整最终权重。
    /// </summary>
    [SerializeField, Min(0)]
    private float _baseWeight = 1;
    /// <summary>该技能类别再次可用前需要等待的冷却时间。</summary>
    [SerializeField, Min(0)]
    private float _cooldown = 6;

    /// <summary>技能造成的基础伤害。</summary>
    [SerializeField, Min(1)]
    private int _damage = 25;

    /// <summary>
    /// normalizedTime 到达该值后锁定攻击方向。
    /// 在此之前 Boss 可以改变朝向玩家的方向。
    /// </summary>
    [SerializeField, Range(0, 1)]
    private float _directionLock = .3f;

    /// <summary>伤害判定开始的 normalizedTime。</summary>
    [SerializeField, Range(0, 1)]
    private float _hitStart = .4f;

    /// <summary>伤害判定结束的 normalizedTime。</summary>
    [SerializeField, Range(0, 1)]
    private float _hitEnd = .6f;
    [Tooltip("留空时沿用原 Hit Start / Hit End；多段攻击为每次出手配置独立窗口。")]
    [SerializeField]
    private BossHitWindow[] _hitWindows;

    /// <summary>
    /// 单次效果的触发时间。
    /// 例如踩地产生冲击波、投石释放手中的石头。
    /// </summary>
    [SerializeField, Range(0, 1)]
    private float _release = .55f;

    [Tooltip("投射物在挖掘完成后出现在手中的动画归一化时间；释放时间仍由 Release 控制。")]
    [SerializeField, Range(0, 1)]
    private float _projectilePickup = .25f;

    
    [SerializeField, Range(.5f, 1)]
    private float _completion = .99f;
    [SerializeField, Min(0)]
    private float _recovery = .45f;
    [SerializeField, Min(.1f)]
    private float _playbackSpeed = 1;
    [SerializeField, Range(0, 1)]
    private float _moveStart = .25f;
    [SerializeField, Range(0, 1)]
    private float _moveEnd = .7f;
    [SerializeField, Min(0)]
    private float _moveSpeed;
    [SerializeField, Min(.1f)]
    private float _radius = 3;
    [SerializeField, Range(1, 3)]
    private int _hands = 3; // 1 = 左手，2 = 右手，3 = 双手

    // Public properties
    public string Id => _id;
    public string StateName => _stateName;
    public int StateHash => Animator.StringToHash("Base Layer." + _stateName);
    public BossSkillFamily Family => _family;
    public BossSkillSide Side => _side;
    public bool IsSidedGroundSlam => _family == BossSkillFamily.GroundSlam && _side != BossSkillSide.Any;
    public BossDamageKind DamageKind => _damageKind;
    public float MinRange => _minRange;
    public float MaxRange => _maxRange;
    public float MaxAngle => _maxAngle;
    public float BaseWeight => _baseWeight;
    public float Cooldown => _cooldown;
    public int Damage => _damage;
    public float DirectionLock => _directionLock;
    public float HitStart => _hitStart;
    public float HitEnd => _hitEnd;
    public float Release => _release;
    public float ProjectilePickup => _projectilePickup;
    public float Completion => _completion;
    public float Recovery => _recovery;
    public float PlaybackSpeed => _playbackSpeed;
    public float MoveStart => _moveStart;
    public float MoveEnd => _moveEnd;
    public float MoveSpeed => _moveSpeed;
    public float Radius => _radius;
    public int Hands => _hands;

    public int HitWindowCount => _hitWindows != null && _hitWindows.Length > 0 ? _hitWindows.Length : 1;

    public int GetHitWindowIndex(float normalizedTime)
    {
        if (_hitWindows == null || _hitWindows.Length == 0)
        {
            if (normalizedTime >= _hitStart && normalizedTime <= _hitEnd)
            {
                return 0;
            }

            return -1;
        }

        for (int i = 0; i < _hitWindows.Length; i++)
        {
            if (normalizedTime >= _hitWindows[i].Start && normalizedTime <= _hitWindows[i].End)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnValidate()
    {
        _maxRange = Mathf.Max(_minRange, _maxRange);
        _hitEnd = Mathf.Max(_hitStart, _hitEnd);
        _projectilePickup = Mathf.Min(_projectilePickup, _release);
        _moveEnd = Mathf.Max(_moveStart, _moveEnd);
        _completion = Mathf.Max(_completion, _hitEnd, _release, _moveEnd);

        if (_hitWindows != null)
        {
            foreach (var window in _hitWindows)
            {
                _completion = Mathf.Max(_completion, window.End);
            }
        }
    }
}
