using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAnimator : MonoBehaviour
{
    private const int BaseLayerIndex = 0;

    /// <summary>
    /// Animator.StringToHash() 会把它转换成一个整数，"Base Layer.Hit" → 123456789之后播放动画时就可以传整数
    /// </summary>
    private static readonly int IdleStateHash =
        Animator.StringToHash("Base Layer.Idle");
    private static readonly int HurtStateHash =
        Animator.StringToHash("Base Layer.Hit");
    private static readonly int DeathStateHash =
        Animator.StringToHash("Base Layer.Death");

    [SerializeField]
    private Animator _animator;

    [SerializeField, Min(0f)]
    private float _transitionDuration = 0.08f;

    [SerializeField, Range(0.5f, 1f)]
    private float _hurtCompletionNormalizedTime = 0.95f;

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
