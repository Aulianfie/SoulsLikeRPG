using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_PlayerMovement_Default",
    menuName = "SoulsLikeRPG/Player/Movement Config"
)]
public sealed class PlayerMovementConfig : ScriptableObject
{
    [Header("Speed")]
    [SerializeField, Min(0f)] private float _moveSpeed = 4f;
    [SerializeField, Min(0f)] private float _sprintSpeed = 6.5f;

    [Header("Responsiveness")]
    [SerializeField, Min(0f)] private float _acceleration = 20f;
    [SerializeField, Min(0f)] private float _deceleration = 25f;
    [SerializeField, Min(0f)] private float _rotationSharpness = 12f;

    [Header("Vertical Movement")]
    [SerializeField] private float _gravity = -20f;
    [SerializeField] private float _groundStickForce = -2f;
    [SerializeField, Min(0f)] private float _jumpHeight = 1.5f;

    public float MoveSpeed => _moveSpeed;
    public float SprintSpeed => _sprintSpeed;
    public float Acceleration => _acceleration;
    public float Deceleration => _deceleration;
    public float RotationSharpness => _rotationSharpness;
    public float Gravity => _gravity;
    public float GroundStickForce => _groundStickForce;
    public float JumpHeight => _jumpHeight;
}
