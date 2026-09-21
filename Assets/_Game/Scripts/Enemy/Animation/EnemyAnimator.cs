using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAnimator : MonoBehaviour
{
    private const int BaseLayerIndex = 0;

    /// <summary>
    /// Animator.StringToHash() 会把状态路径转换成整数，供播放和状态比较使用。
    /// </summary>
    private static readonly int IdleStateHash =
        Animator.StringToHash("Base Layer.Idle");
    private static readonly int ChaseStateHash =
        Animator.StringToHash("Base Layer.Chase");
    private static readonly int AttackStateHash =
        Animator.StringToHash("Base Layer.Attack");
    private static readonly int HurtStateHash =
        Animator.StringToHash("Base Layer.Hurt");
    private static readonly int DeathStateHash =
        Animator.StringToHash("Base Layer.Dead");

    [SerializeField]
    private Animator _animator;

    [SerializeField, Min(0f)]
    private float _transitionDuration = 0.08f;

    [SerializeField, Range(0.5f, 1f)]
    private float _hurtCompletionNormalizedTime = 0.95f;

    [SerializeField, Range(0.5f, 1f)]
    private float _attackCompletionNormalizedTime = 0.95f;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

        if (_animator != null)
            return;

        Debug.LogError(
            "EnemyAnimator 找不到 Animator 引用。",
            this
        );

        enabled = false;
    }

    public void PlayIdle()
    {
        CrossFade(IdleStateHash);
    }

    public void PlayHurt()
    {
        CrossFade(HurtStateHash);
    }

    public void PlayChase()
    {
        CrossFade(ChaseStateHash);
    }

    public void PlayAttack()
    {
        CrossFade(AttackStateHash);
    }

    public void PlayDeath()
    {
        CrossFade(DeathStateHash);
    }

    public bool IsHurtFinished()
    {
        if (
            _animator == null ||
            _animator.IsInTransition(BaseLayerIndex)
        )
        {
            return false;
        }

        AnimatorStateInfo stateInfo =
            _animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);

        return
            stateInfo.fullPathHash == HurtStateHash &&
            stateInfo.normalizedTime >=
                _hurtCompletionNormalizedTime;
    }

    public bool TryGetAttackNormalizedTime(out float normalizedTime)
    {
        normalizedTime = 0f;

        if (_animator == null)
            return false;

        AnimatorStateInfo stateInfo =
            _animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);

        if (stateInfo.fullPathHash != AttackStateHash)
            return false;

        normalizedTime = stateInfo.normalizedTime;
        return true;
    }

    public bool IsAttackFinished()
    {
        return
            TryGetAttackNormalizedTime(out float normalizedTime) &&
            !_animator.IsInTransition(BaseLayerIndex) &&
            normalizedTime >= _attackCompletionNormalizedTime;
    }

    private void CrossFade(int stateHash)
    {
        if (_animator == null)
            return;

        _animator.CrossFadeInFixedTime(
            stateHash,
            _transitionDuration,
            BaseLayerIndex
        );
    }
}
