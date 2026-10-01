using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyAnimator), typeof(EnemyMotor), typeof(BossDamageArea))]
public sealed class BossSkillRunner : MonoBehaviour
{
    // Serialized fields
    [SerializeField]
    private WeaponHitbox _leftHand;
    [SerializeField]
    private WeaponHitbox _rightHand;
    [SerializeField]
    private Transform _leftFoot;
    [SerializeField]
    private Transform _rightFoot;
    [SerializeField]
    private Transform _throwSocket;
    [SerializeField]
    private BossRockProjectile _rockPrefab;
    [SerializeField]
    private LayerMask _obstacleLayers = 1;

    // Dependencies
    private EnemyAnimator _animationDriver;
    private EnemyMotor _motor;
    private BossDamageArea _damageArea;
    private BossBlackboard _blackboard;

    // Runtime state
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private readonly List<BossRockProjectile> _projectiles = new List<BossRockProjectile>();
    private readonly List<GameObject> _effects = new List<GameObject>();
    private BossSkillData _currentSkill;
    private int _currentStateHash;
    private bool _isDirectionLocked;
    private bool _hasReleased;
    private bool _areHandsActive;
    private bool _hasStartedSpecialMovement;
    private bool _isMovementBlocked;
    private Vector3 _lockedDirection;
    private Vector3 _aimPosition;
    private float _startedAt;
    private float _traveledDistance;
    private float _travelDistanceLimit;

    // Public properties
    public bool IsRunning => _currentSkill != null;
    public bool DirectionLocked => _isDirectionLocked;
    public bool Released => _hasReleased;
    public float NormalizedTime { get; private set; }

    public string Phase
    {
        get
        {
            if (_currentSkill == null)
            {
                return "无技能";
            }
            else if (NormalizedTime < _currentSkill.DirectionLock)
            {
                return "前摇追踪";
            }
            else if (NormalizedTime < _currentSkill.HitStart)
            {
                return "方向锁定";
            }
            else if (NormalizedTime <= _currentSkill.HitEnd)
            {
                return "出手";
            }
            else
            {
                return "收招";
            }
        }
    }

    public Vector3 ThrowPosition
    {
        get
        {
            if (_throwSocket != null)
            {
                return _throwSocket.position;
            }
            else
            {
                return transform.position + Vector3.up * 3;
            }
        }
    }

    public int LiveProjectileCount
    {
        get
        {
            int count = 0;
            foreach (var stone in _projectiles)
            {
                if (stone != null &&
                    !stone.Resolved)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private void Awake()
    {
        _animationDriver = GetComponent<EnemyAnimator>();
        _motor = GetComponent<EnemyMotor>();
        _damageArea = GetComponent<BossDamageArea>();
    }

    public void Bind(BossBlackboard blackboard)
    {
        _blackboard = blackboard;
    }

    public bool Begin(BossSkillData skill)
    {
        if (IsRunning ||
            skill == null ||
            _blackboard == null ||
            _blackboard.Target == null ||
            GetComponent<EnemyHealth>().CurrentHealth <= 0)
        {
            return false;
        }

        if (!_animationDriver.PlayState(skill.StateHash))
        {
            Debug.LogError("Boss 找不到动画状态：" + skill.StateName, this);
            return false;
        }

        _motor.Stop();
        // 双手共用本次技能的命中集合，避免同一目标被左右手重复结算。
        _hitTargets.Clear();
        _damageArea.ResetSweep();
        _currentSkill = skill;
        _currentStateHash = skill.StateHash;
        _blackboard.Commit(skill, Time.time);
        _isMovementBlocked = false;
        _hasStartedSpecialMovement = false;
        _areHandsActive = false;
        _hasReleased = false;
        _isDirectionLocked = false;
        NormalizedTime = 0;
        _startedAt = Time.time;
        _traveledDistance = 0;
        if (skill.Family == BossSkillFamily.Dash)
        {
            _travelDistanceLimit = Mathf.Max(.2f, _blackboard.Distance - 1.8f);
        }
        else
        {
            _travelDistanceLimit = _blackboard.Distance + 2;
        }

        _lockedDirection = transform.forward;
        _aimPosition = _blackboard.Target.position + Vector3.up;
        _animationDriver.SetSpeed(skill.PlaybackSpeed);
        return true;
    }

    public BTStatus Tick(float dt)
    {
        if (!IsRunning)
        {
            return BTStatus.Success;
        }

        if (!_animationDriver.TryGetStateNormalizedTime(_currentStateHash, out float t))
        {
            if (Time.time - _startedAt > 15)
            {
                Debug.LogError("Boss 技能动画未完成：" + _currentSkill.Id, this);
                Abort();
            }

            return BTStatus.Running;
        }

        NormalizedTime = t;
        if (!_isDirectionLocked)
        {
            if (_blackboard.Target != null)
            {
                if (_currentSkill.Family != BossSkillFamily.Stomp)
                {
                    _motor.FaceTarget(_blackboard.Target.position, dt);
                }

                _aimPosition = _blackboard.Target.position + Vector3.up;
            }

            if (t >= _currentSkill.DirectionLock)
            {
                _isDirectionLocked = true;
                _lockedDirection = transform.forward;
            }
        }

        if (_currentSkill.MoveSpeed > 0 &&
            t >= _currentSkill.MoveStart &&
            t <= _currentSkill.MoveEnd &&
            !_isMovementBlocked)
        {
            if (!_hasStartedSpecialMovement)
            {
                _motor.BeginSpecialMovement();
                _hasStartedSpecialMovement = true;
            }

            float step = Mathf.Min(_currentSkill.MoveSpeed * dt, Mathf.Max(0, _travelDistanceLimit - _traveledDistance));
            if (step > 0 &&
                _motor.MoveSpecial(_lockedDirection, step, _obstacleLayers))
            {
                _traveledDistance += step;
            }
            else
            {
                _isMovementBlocked = true;
                _motor.EndSpecialMovement();
            }
        }

        if (_hasStartedSpecialMovement &&
            t > _currentSkill.MoveEnd)
        {
            _motor.EndSpecialMovement();
        }

        bool active = t >= _currentSkill.HitStart &&
            t <= _currentSkill.HitEnd;
        if (_currentSkill.DamageKind == BossDamageKind.Hands)
        {
            if (active &&
                !_areHandsActive)
            {
                if ((_currentSkill.Hands & 1) != 0 &&
                    _leftHand != null)
                {
                    _leftHand.BeginAttack(_currentSkill.Damage, _hitTargets);
                }

                if ((_currentSkill.Hands & 2) != 0 &&
                    _rightHand != null)
                {
                    _rightHand.BeginAttack(_currentSkill.Damage, _hitTargets);
                }

                _areHandsActive = true;
            }
            else if (!active &&
                _areHandsActive)
            {
                CloseHands();
            }
        }
        else if (_currentSkill.DamageKind == BossDamageKind.BodySweep &&
            active)
        {
            _damageArea.Sweep(transform.position + Vector3.up * 1.3f, _currentSkill.Radius, _currentSkill.Damage, _hitTargets);
        }

        if (!_hasReleased &&
            t >= _currentSkill.Release)
        {
            _hasReleased = true;
            if (_currentSkill.DamageKind == BossDamageKind.GroundPulse)
            {
                Transform foot;
                if (_currentSkill.Side == BossSkillSide.Left)
                {
                    foot = _leftFoot;
                }
                else if (_currentSkill.Side == BossSkillSide.Right)
                {
                    foot = _rightFoot;
                }
                else
                {
                    foot = null;
                }

                Vector3 center;
                if (foot != null)
                {
                    center = foot.position;
                }
                else
                {
                    center = transform.position;
                }

                center.y = transform.position.y;
                _damageArea.Pulse(center, _currentSkill.Radius, _currentSkill.Damage, _hitTargets);
            }
            else if (_currentSkill.DamageKind == BossDamageKind.Projectile &&
                _rockPrefab != null)
            {
                _projectiles.RemoveAll(p => p == null);
                var stone = Instantiate(_rockPrefab, ThrowPosition, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stone.gameObject, gameObject.scene);
                stone.Launch(gameObject, _aimPosition, _currentSkill.Damage);
                _projectiles.Add(stone);
            }
        }

        if (t < _currentSkill.Completion)
        {
            return BTStatus.Running;
        }

        Finish(true);
        return BTStatus.Success;
    }

    private void CloseHands()
    {
        _leftHand?.EndAttack();
        _rightHand?.EndAttack();
        _areHandsActive = false;
    }

    private void Finish(bool completed)
    {
        CloseHands();
        _motor.EndSpecialMovement();
        _motor.Stop();
        _animationDriver.SetSpeed(1);
        _blackboard?.Finish(Time.time, completed);
        _currentSkill = null;
        if (completed)
        {
            _animationDriver.PlayIdle();
        }
    }

    public void Abort(bool recordHistory = true)
    {
        if (_currentSkill != null &&
            recordHistory)
        {
            Finish(false);
        }
        else
        {
            CloseHands();
            _motor?.EndSpecialMovement();
            _motor?.Stop();
            if (_animationDriver != null)
            {
                _animationDriver.SetSpeed(1);
            }

            _currentSkill = null;
            if (_blackboard != null)
            {
                _blackboard.CurrentSkill = null;
            }
        }
    }

    public void TrackEffect(GameObject effect)
    {
        _effects.RemoveAll(e => e == null);
        _effects.Add(effect);
    }

    public void ClearProjectiles()
    {
        foreach (var stone in _projectiles)
        {
            if (stone != null)
            {
                stone.Dissolve();
            }
        }

        _projectiles.Clear();
        foreach (var effect in _effects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }

        _effects.Clear();
    }

    private void OnDisable()
    {
        Abort(false);
        ClearProjectiles();
    }
}
