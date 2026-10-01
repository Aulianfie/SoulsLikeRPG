using UnityEngine;

public enum BossSkillFamily
{
    Ordinary,
    Stomp,
    Dash,
    Whirlwind,
    Jump,
    ThrowStone
}

public enum BossSkillSide
{
    Any,
    Left,
    Right
}

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
    [SerializeField]
    private BossDamageKind _damageKind;
    [SerializeField, Min(0)]
    private float _minRange;
    [SerializeField, Min(.1f)]
    private float _maxRange = 4;
    [SerializeField, Range(0, 180)]
    private float _maxAngle = 85;
    [SerializeField, Min(0)]
    private float _baseWeight = 1;
    [SerializeField, Min(0)]
    private float _cooldown = 6;
    [SerializeField, Min(1)]
    private int _damage = 25;
    [SerializeField, Range(0, 1)]
    private float _directionLock = .3f;
    [SerializeField, Range(0, 1)]
    private float _hitStart = .4f;
    [SerializeField, Range(0, 1)]
    private float _hitEnd = .6f;
    [SerializeField, Range(0, 1)]
    private float _release = .55f;
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
    private int _hands = 3;

    // Public properties
    public string Id => _id;
    public string StateName => _stateName;
    public int StateHash => Animator.StringToHash("Base Layer." + _stateName);
    public BossSkillFamily Family => _family;
    public BossSkillSide Side => _side;
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
    public float Completion => _completion;
    public float Recovery => _recovery;
    public float PlaybackSpeed => _playbackSpeed;
    public float MoveStart => _moveStart;
    public float MoveEnd => _moveEnd;
    public float MoveSpeed => _moveSpeed;
    public float Radius => _radius;
    public int Hands => _hands;

    private void OnValidate()
    {
        _maxRange = Mathf.Max(_minRange, _maxRange);
        _hitEnd = Mathf.Max(_hitStart, _hitEnd);
        _moveEnd = Mathf.Max(_moveStart, _moveEnd);
        _completion = Mathf.Max(_completion, _hitEnd, _release, _moveEnd);
    }
}
