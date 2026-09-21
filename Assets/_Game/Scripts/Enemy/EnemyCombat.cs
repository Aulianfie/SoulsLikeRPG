using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyCombat : MonoBehaviour
{
    [SerializeField]
    private WeaponHitbox _weaponHitbox;

    [SerializeField, Min(1)]
    private int _attackDamage = 20;

    [Header("Wind-up（前摇）")]
    [Tooltip("攻击动画前摇段的播放速度，<1 表示抬手更慢更长")]
    [SerializeField, Range(0.2f, 1f)]
    private float _windupSpeedMultiplier = 0.5f;

    [Tooltip("前摇段的结束点（normalizedTime），之后恢复正常播放速度")]
    [SerializeField, Range(0f, 1f)]
    private float _windupEndNormalizedTime = 0.4f;

    [SerializeField, Range(0f, 1f)]
    private float _hitboxStartNormalizedTime = 0.12f;

    [SerializeField, Range(0f, 1f)]
    private float _hitboxEndNormalizedTime = 0.6f;

    [SerializeField, Min(0f)]
    private float _attackCooldown = 0.5f;

    private bool _attackInProgress;
    private bool _hitboxActive;
    private float _nextAttackTime;

    public bool CanStartAttack =>
        _weaponHitbox != null &&
        !_attackInProgress &&
        Time.time >= _nextAttackTime;

    public float WindupSpeedMultiplier => _windupSpeedMultiplier;
    public float WindupEndNormalizedTime => _windupEndNormalizedTime;

    private void Awake()
    {
        if (_weaponHitbox == null)
            _weaponHitbox = GetComponentInChildren<WeaponHitbox>(true);

        if (_weaponHitbox != null)
            return;

        Debug.LogError(
            "EnemyCombat 找不到 WeaponHitbox，敌人无法造成伤害。",
            this
        );

        enabled = false;
    }

    public void BeginAttack()
    {
        if (!CanStartAttack)
            return;

        _weaponHitbox.EndAttack();
        _attackInProgress = true;
        _hitboxActive = false;
    }

    public void TickAttack(float normalizedTime)
    {
        if (!_attackInProgress)
            return;

        bool shouldBeActive =
            normalizedTime >= _hitboxStartNormalizedTime &&
            normalizedTime <= _hitboxEndNormalizedTime;

        if (shouldBeActive && !_hitboxActive)
        {
            _weaponHitbox.BeginAttack(_attackDamage);
            _hitboxActive = true;
        }
        else if (!shouldBeActive && _hitboxActive)
        {
            _weaponHitbox.EndAttack();
            _hitboxActive = false;
        }
    }

    public void CompleteAttack()
    {
        EndAttack(true);
    }

    public void CancelAttack()
    {
        EndAttack(_attackInProgress);
    }

    private void EndAttack(bool startCooldown)
    {
        _weaponHitbox?.EndAttack();
        _hitboxActive = false;

        if (startCooldown)
            _nextAttackTime = Time.time + _attackCooldown;

        _attackInProgress = false;
    }

    private void OnDisable()
    {
        CancelAttack();
    }

    private void OnValidate()
    {
        _hitboxEndNormalizedTime = Mathf.Max(
            _hitboxStartNormalizedTime,
            _hitboxEndNormalizedTime
        );
        _windupEndNormalizedTime = Mathf.Clamp01(
            Mathf.Min(
                _windupEndNormalizedTime,
                _hitboxEndNormalizedTime
            )
        );
    }
}
