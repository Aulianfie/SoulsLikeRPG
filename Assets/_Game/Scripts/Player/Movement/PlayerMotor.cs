using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputReader))]
public sealed class PlayerMotor : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _rotationSharpness = 12f;
    [SerializeField] private float _gravity = -20f;
    [SerializeField] private float _groundStickForce = -2f;

    private CharacterController _characterController;
    private PlayerInputReader _inputReader;
    private Transform _cameraTransform;
    private float _verticalVelocity; // 角色在 Y 轴上的速度

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _inputReader = GetComponent<PlayerInputReader>();
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError(
                "PlayerMotor 找不到带有 MainCamera Tag 的相机。",
                this
            );

            enabled = false;
            return;
        }

        _cameraTransform = mainCamera.transform;
    }

    private void Update()
    {
        Vector3 moveDirection =
            GetCameraRelativeDirection(_inputReader.MoveInput);

        RotateTowards(moveDirection);
        Move(moveDirection);
    }

    /// <summary>
    /// 将玩家的移动输入转换为相对于摄像机的世界空间方向。
    /// 1. 获取摄像机的前向和右向向量，并将它们的 y 分量设为 0，以确保角色只在水平面上移动。
    /// 2. 将摄像机的前向向量和右向向量归一化，以确保它们的长度为 1。
    /// 3. 根据玩家的移动输入（MoveInput）计算最终的移动方向。
    /// 4. 返回计算出的移动方向，并将其限制在单位圆内，以确保角色不会因为输入过大而移动得过快。
    /// 
    /// </summary>
    /// <param name="moveInput"></param>
    /// <returns></returns>
    private Vector3 GetCameraRelativeDirection(Vector2 moveInput)
    {
        Vector3 cameraForward = _cameraTransform.forward;
        Vector3 cameraRight = _cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection =
            cameraForward * moveInput.y
            + cameraRight * moveInput.x;

        return Vector3.ClampMagnitude(moveDirection, 1f);
    }

    /// <summary>
    /// 将角色旋转朝向移动方向。
    /// 1. 检查移动方向的平方长度是否小于一个很小的阈值，如果是，则不进行旋转。
    /// 2. 使用 Quaternion.LookRotation 创建一个目标旋转，使角色面向移动方向。
    /// 3. 计算旋转的平滑因子，使用指数衰减函数来确保旋转速度随时间变化。
    /// 4. 使用 Quaternion.Slerp 在当前旋转和目标旋转之间进行插值，实现平滑旋转效果。
    /// 5. 将计算出的旋转应用到角色的 Transform 上。
    /// </summary>
    /// <param name="moveDirection"></param>
    private void RotateTowards(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(moveDirection, Vector3.up);

        float rotationAmount =
            1f - Mathf.Exp(
                -_rotationSharpness * Time.deltaTime
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationAmount
        );
    }

    /// <summary>
    /// 根据移动方向和重力计算角色的最终移动速度，并使用 CharacterController.Move 方法移动角色。
    /// 1. 计算水平速度，将移动方向乘以移动速度。
    /// 2. 检查角色是否在地面上，如果是，则将垂直速度设置为一个小的负值，以确保角色贴地。
    /// 3. 如果角色不在地面上，则将垂直速度增加重力值乘以时间增量，以模拟自由落体运动。
    /// 4. 将水平速度和垂直速度组合成最终的移动速度。
    /// 5. 使用 CharacterController.Move 方法将角色移动到新的位置。
    /// </summary>
    /// <param name="moveDirection"></param>
    private void Move(Vector3 moveDirection)
    {
        Vector3 horizontalVelocity =
            moveDirection * _moveSpeed;

        if (_characterController.isGrounded)
        {
            _verticalVelocity = _groundStickForce;
        }
        else
        {
            _verticalVelocity += _gravity * Time.deltaTime;
        }

        Vector3 finalVelocity = horizontalVelocity;
        finalVelocity.y = _verticalVelocity;

        _characterController.Move(
            finalVelocity * Time.deltaTime
        );
    }

    private void OnDisable()
    {
        _verticalVelocity = 0f;
    }
}
