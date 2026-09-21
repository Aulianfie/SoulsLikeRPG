using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class EnemyMotor : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float _moveSpeed = 2.5f;

    [SerializeField, Min(0f)]
    private float _rotationSharpness = 12f;

    [SerializeField]
    private float _gravity = -25f;

    [SerializeField]
    private float _groundStickForce = -2f;

    private CharacterController _characterController;
    private float _verticalVelocity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    public void TickChase(
        Vector3 targetPosition,
        float stopDistance,
        float deltaTime
    )
    {
        Vector3 offset = targetPosition - transform.position;
        offset.y = 0f;

        float distance = offset.magnitude;
        Vector3 direction = distance > 0.0001f
            ? offset / distance
            : Vector3.zero;

        FaceDirection(direction, deltaTime);

        float allowedDistance = Mathf.Max(0f, distance - stopDistance);
        float moveDistance = Mathf.Min(
            _moveSpeed * deltaTime,
            allowedDistance
        );

        Move(direction * moveDistance, deltaTime);
    }

    public void FaceTarget(Vector3 targetPosition, float deltaTime)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        FaceDirection(direction.normalized, deltaTime);
    }

    public void Stop()
    {
        // CharacterController 没有持续的水平速度，状态切换时无需额外制动。
    }

    private void FaceDirection(Vector3 direction, float deltaTime)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            direction,
            Vector3.up
        );

        float rotationAmount = 1f - Mathf.Exp(
            -_rotationSharpness * deltaTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationAmount
        );
    }

    private void Move(Vector3 horizontalDisplacement, float deltaTime)
    {
        if (_characterController.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = _groundStickForce;
        else
            _verticalVelocity += _gravity * deltaTime;

        Vector3 displacement = horizontalDisplacement;
        displacement.y = _verticalVelocity * deltaTime;
        _characterController.Move(displacement);
    }

    private void OnDisable()
    {
        _verticalVelocity = 0f;
    }
}
