using UnityEngine;

/// <summary>
/// 平滑 CharacterController 上下台阶时模型的显示高度，不改变碰撞体位置。
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMotor))]
public sealed class PlayerModelStepSmoothing : MonoBehaviour
{
    [SerializeField, Range(0.02f, 0.3f)] private float _verticalSmoothTime = 0.08f;
    [SerializeField, Min(0f)] private float _maxVerticalLag = 0.12f;
    [SerializeField, Min(0f)] private float _snapHeightChange = 1f;

    private PlayerMotor _motor;
    private Transform _modelRoot;
    private Vector3 _modelLocalOffset;
    private float _smoothedY;
    private float _verticalSmoothVelocity;
    private float _previousDesiredY;

    private void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.transform.parent == null)
        {
            Debug.LogError("PlayerModelStepSmoothing 找不到 Player 模型根节点。", this);
            enabled = false;
            return;
        }

        _modelRoot = animator.transform;
        _modelLocalOffset = _modelRoot.localPosition;
    }

    private void OnEnable()
    {
        if (_modelRoot == null)
            return;

        Vector3 desired = _modelRoot.parent.TransformPoint(_modelLocalOffset);
        _smoothedY = desired.y;
        _previousDesiredY = desired.y;
        _verticalSmoothVelocity = 0f;
        _modelRoot.position = desired;
    }

    private void LateUpdate()
    {
        Vector3 desired = _modelRoot.parent.TransformPoint(_modelLocalOffset);
        bool snap = _motor.ShouldEnterAirborne ||
            Mathf.Abs(desired.y - _previousDesiredY) > _snapHeightChange;
        _previousDesiredY = desired.y;

        if (snap)
        {
            _smoothedY = desired.y;
            _verticalSmoothVelocity = 0f;
        }
        else
        {
            _smoothedY = Mathf.SmoothDamp(
                _smoothedY, desired.y, ref _verticalSmoothVelocity,
                _verticalSmoothTime
            );
            _smoothedY = Mathf.Clamp(
                _smoothedY,
                desired.y - _maxVerticalLag,
                desired.y + _maxVerticalLag
            );
        }

        // 水平位置和旋转仍由原有 Player/Animator 逻辑决定。
        _modelRoot.position = new Vector3(desired.x, _smoothedY, desired.z);
    }
}
