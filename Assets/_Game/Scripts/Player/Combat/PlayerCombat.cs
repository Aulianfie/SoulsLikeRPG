using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAnimator))]
public sealed class PlayerCombat : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float _transitionDuration = 0.08f;

    [SerializeField, Range(0.5f, 1f)]
    private float _completionNormalizedTime = 0.95f;

    private PlayerAnimator _playerAnimator;

    private void Awake()
    {
        _playerAnimator = GetComponent<PlayerAnimator>();
    }

    public void StartLightAttack()
    {
        _playerAnimator.PlayLightAttack(_transitionDuration);
    }

    public bool IsLightAttackFinished()
    {
        return _playerAnimator.IsLightAttackFinished(
            _completionNormalizedTime
        );
    }

    public void FinishLightAttack()
    {
        _playerAnimator.PlayLocomotion(_transitionDuration);
    }
}
