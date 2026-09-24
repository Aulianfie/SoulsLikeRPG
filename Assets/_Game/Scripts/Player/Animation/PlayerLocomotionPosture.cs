using UnityEngine;

/// <summary>
/// 减轻移动动画自带的上身前俯。只调整脊柱及其子骨骼，保留腿部和脚步轨迹。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLocomotionPosture : MonoBehaviour
{
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int LocomotionHash =
        Animator.StringToHash("Base Layer.Locomotion");

    [SerializeField] private PlayerMovementConfig _movementConfig;

    [Tooltip("冲刺时对上身前俯的最大校正角度；普通移动按速度比例应用。")]
    [SerializeField, Range(0f, 45f)] private float _maxPitchCorrection = 25f;

    private Animator _animator;
    private Transform _spine;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_movementConfig == null ||
            _animator == null ||
            _animator.avatar == null ||
            !_animator.avatar.isHuman ||
            !_animator.avatar.isValid)
        {
            Debug.LogError("PlayerLocomotionPosture 缺少有效的移动配置或 Humanoid Animator。", this);
            enabled = false;
            return;
        }

        _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
        if (_spine == null)
        {
            Debug.LogError("PlayerLocomotionPosture 找不到 Spine 骨骼。", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        float locomotionWeight = GetLocomotionWeight();
        if (locomotionWeight <= 0f || _movementConfig.SprintSpeed <= 0f)
            return;

        float speedRatio = Mathf.Clamp01(
            _animator.GetFloat(MoveSpeedHash) / _movementConfig.SprintSpeed
        );
        float correction = _maxPitchCorrection * speedRatio * locomotionWeight;

        // Animator 已在本帧写入原姿态；只校正上身，不旋转 Player 或腿部。
        _spine.rotation =
            Quaternion.AngleAxis(correction, _animator.transform.right) *
            _spine.rotation;
    }

    private float GetLocomotionWeight()
    {
        bool currentIsLocomotion =
            _animator.GetCurrentAnimatorStateInfo(0).fullPathHash == LocomotionHash;
        if (!_animator.IsInTransition(0))
            return currentIsLocomotion ? 1f : 0f;

        bool nextIsLocomotion =
            _animator.GetNextAnimatorStateInfo(0).fullPathHash == LocomotionHash;
        float progress = Mathf.Clamp01(
            _animator.GetAnimatorTransitionInfo(0).normalizedTime
        );
        return Mathf.Lerp(
            currentIsLocomotion ? 1f : 0f,
            nextIsLocomotion ? 1f : 0f,
            progress
        );
    }
}
