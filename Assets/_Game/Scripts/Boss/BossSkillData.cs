using UnityEngine;

public enum BossSkillFamily { Ordinary, Stomp, Dash, Whirlwind, Jump, ThrowStone }
public enum BossSkillSide { Any, Left, Right }
public enum BossDamageKind { Hands, GroundPulse, BodySweep, Projectile }

[CreateAssetMenu(menuName = "SoulsLike RPG/Boss/Skill")]
public sealed class BossSkillData : ScriptableObject
{
    [SerializeField] string _id;
    [SerializeField] string _stateName;
    [SerializeField] BossSkillFamily _family;
    [SerializeField] BossSkillSide _side;
    [SerializeField] BossDamageKind _damageKind;
    [SerializeField, Min(0)] float _minRange;
    [SerializeField, Min(.1f)] float _maxRange = 4;
    [SerializeField, Range(0, 180)] float _maxAngle = 85;
    [SerializeField, Min(0)] float _baseWeight = 1;
    [SerializeField, Min(0)] float _cooldown = 6;
    [SerializeField, Min(1)] int _damage = 25;
    [SerializeField, Range(0, 1)] float _directionLock = .3f;
    [SerializeField, Range(0, 1)] float _hitStart = .4f;
    [SerializeField, Range(0, 1)] float _hitEnd = .6f;
    [SerializeField, Range(0, 1)] float _release = .55f;
    [SerializeField, Range(.5f, 1)] float _completion = .99f;
    [SerializeField, Min(0)] float _recovery = .45f;
    [SerializeField, Min(.1f)] float _playbackSpeed = 1;
    [SerializeField, Range(0, 1)] float _moveStart = .25f;
    [SerializeField, Range(0, 1)] float _moveEnd = .7f;
    [SerializeField, Min(0)] float _moveSpeed;
    [SerializeField, Min(.1f)] float _radius = 3;
    [SerializeField, Range(1, 3)] int _hands = 3;
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
    void OnValidate()
    {
        _maxRange = Mathf.Max(_minRange, _maxRange);
        _hitEnd = Mathf.Max(_hitStart, _hitEnd);
        _moveEnd = Mathf.Max(_moveStart, _moveEnd);
        _completion = Mathf.Max(_completion, _hitEnd, _release, _moveEnd);
    }
}
