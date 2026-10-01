using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyAnimator), typeof(EnemyMotor), typeof(BossDamageArea))]
public sealed class BossSkillRunner : MonoBehaviour
{
    [SerializeField] WeaponHitbox _leftHand;
    [SerializeField] WeaponHitbox _rightHand;
    [SerializeField] Transform _leftFoot;
    [SerializeField] Transform _rightFoot;
    [SerializeField] Transform _throwSocket;
    [SerializeField] BossRockProjectile _rockPrefab;
    [SerializeField] LayerMask _obstacleLayers = 1;
    readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    readonly List<BossRockProjectile> projectiles = new List<BossRockProjectile>();
    readonly List<GameObject> effects = new List<GameObject>();
    EnemyAnimator animationDriver;
    EnemyMotor motor;
    BossDamageArea area;
    BossBlackboard board;
    BossSkillData current;
    int stateHash;
    bool locked, released, handsOpen, specialStarted, movementBlocked;
    Vector3 direction, aim;
    float startedAt, travel, travelLimit;
    public bool IsRunning => current != null;
    public bool DirectionLocked => locked;
    public bool Released => released;
    public float NormalizedTime { get; private set; }
    public string Phase => current == null ? "无技能" : NormalizedTime < current.DirectionLock ? "前摇追踪" : NormalizedTime < current.HitStart ? "方向锁定" : NormalizedTime <= current.HitEnd ? "出手" : "收招";
    public Vector3 ThrowPosition => _throwSocket != null ? _throwSocket.position : transform.position + Vector3.up * 3;
    public int LiveProjectileCount { get { int count = 0; foreach (var stone in projectiles) if (stone != null && !stone.Resolved) count++; return count; } }

    void Awake() { animationDriver = GetComponent<EnemyAnimator>(); motor = GetComponent<EnemyMotor>(); area = GetComponent<BossDamageArea>(); }
    public void Bind(BossBlackboard blackboard) { board = blackboard; }

    public bool Begin(BossSkillData skill)
    {
        if (IsRunning || skill == null || board == null || board.Target == null || GetComponent<EnemyHealth>().CurrentHealth <= 0) return false;
        if (!animationDriver.PlayState(skill.StateHash)) { Debug.LogError("Boss 找不到动画状态：" + skill.StateName, this); return false; }
        motor.Stop(); hitTargets.Clear(); area.ResetSweep(); current = skill; stateHash = skill.StateHash;
        board.Commit(skill, Time.time);
        locked = released = handsOpen = specialStarted = movementBlocked = false;
        NormalizedTime = 0; startedAt = Time.time; travel = 0;
        travelLimit = skill.Family == BossSkillFamily.Dash ? Mathf.Max(.2f, board.Distance - 1.8f) : board.Distance + 2;
        direction = transform.forward; aim = board.Target.position + Vector3.up;
        animationDriver.SetSpeed(skill.PlaybackSpeed);
        return true;
    }

    public BTStatus Tick(float dt)
    {
        if (!IsRunning) return BTStatus.Success;
        if (!animationDriver.TryGetStateNormalizedTime(stateHash, out float t))
        {
            if (Time.time - startedAt > 15) { Debug.LogError("Boss 技能动画未完成：" + current.Id, this); Abort(); }
            return BTStatus.Running;
        }
        NormalizedTime = t;
        if (!locked)
        {
            if (board.Target != null)
            {
                if (current.Family != BossSkillFamily.Stomp) motor.FaceTarget(board.Target.position, dt);
                aim = board.Target.position + Vector3.up;
            }
            if (t >= current.DirectionLock) { locked = true; direction = transform.forward; }
        }
        if (current.MoveSpeed > 0 && t >= current.MoveStart && t <= current.MoveEnd && !movementBlocked)
        {
            if (!specialStarted) { motor.BeginSpecialMovement(); specialStarted = true; }
            float step = Mathf.Min(current.MoveSpeed * dt, Mathf.Max(0, travelLimit - travel));
            if (step > 0 && motor.MoveSpecial(direction, step, _obstacleLayers)) travel += step;
            else { movementBlocked = true; motor.EndSpecialMovement(); }
        }
        if (specialStarted && t > current.MoveEnd) motor.EndSpecialMovement();

        bool active = t >= current.HitStart && t <= current.HitEnd;
        if (current.DamageKind == BossDamageKind.Hands)
        {
            if (active && !handsOpen)
            {
                if ((current.Hands & 1) != 0 && _leftHand != null) _leftHand.BeginAttack(current.Damage, hitTargets);
                if ((current.Hands & 2) != 0 && _rightHand != null) _rightHand.BeginAttack(current.Damage, hitTargets);
                handsOpen = true;
            }
            else if (!active && handsOpen) CloseHands();
        }
        else if (current.DamageKind == BossDamageKind.BodySweep && active)
            area.Sweep(transform.position + Vector3.up * 1.3f, current.Radius, current.Damage, hitTargets);
        if (!released && t >= current.Release)
        {
            released = true;
            if (current.DamageKind == BossDamageKind.GroundPulse)
            {
                Transform foot = current.Side == BossSkillSide.Left ? _leftFoot : current.Side == BossSkillSide.Right ? _rightFoot : null;
                Vector3 center = foot != null ? foot.position : transform.position; center.y = transform.position.y;
                area.Pulse(center, current.Radius, current.Damage, hitTargets);
            }
            else if (current.DamageKind == BossDamageKind.Projectile && _rockPrefab != null)
            {
                projectiles.RemoveAll(p => p == null);
                var stone = Instantiate(_rockPrefab, ThrowPosition, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stone.gameObject, gameObject.scene);
                stone.Launch(gameObject, aim, current.Damage); projectiles.Add(stone);
            }
        }
        if (t < current.Completion) return BTStatus.Running;
        Finish(true); return BTStatus.Success;
    }

    void CloseHands() { _leftHand?.EndAttack(); _rightHand?.EndAttack(); handsOpen = false; }
    void Finish(bool completed)
    {
        CloseHands(); motor.EndSpecialMovement(); motor.Stop(); animationDriver.SetSpeed(1);
        board?.Finish(Time.time, completed); current = null;
        if (completed) animationDriver.PlayIdle();
    }
    public void Abort(bool recordHistory = true)
    {
        if (current != null && recordHistory) Finish(false);
        else
        {
            CloseHands(); motor?.EndSpecialMovement(); motor?.Stop();
            if (animationDriver != null) animationDriver.SetSpeed(1);
            current = null; if (board != null) board.CurrentSkill = null;
        }
    }
    public void TrackEffect(GameObject effect) { effects.RemoveAll(e => e == null); effects.Add(effect); }
    public void ClearProjectiles()
    {
        foreach (var stone in projectiles) if (stone != null) stone.Dissolve(); projectiles.Clear();
        foreach (var effect in effects) if (effect != null) Destroy(effect); effects.Clear();
    }
    void OnDisable() { Abort(false); ClearProjectiles(); }
}
