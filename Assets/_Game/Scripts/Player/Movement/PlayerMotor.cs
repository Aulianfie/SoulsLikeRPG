using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMotor : MonoBehaviour
{
    [SerializeField] private PlayerMovementConfig _config;

    private CharacterController _characterController;
    private Transform _cameraTransform;
    private bool _isInitialized;

    private Vector3 _horizontalVelocity; // 负责地面上的前后左右移动
    private float _verticalVelocity; // 负责重力和贴地

    public float HorizontalSpeed => _horizontalVelocity.magnitude;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (_config == null)
        {
            Debug.LogError("PlayerMotor 没有配置 PlayerMovementConfig。", this);
            enabled = false;
            return;
        }

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
        _isInitialized = true;
    }
    /// <summary>
    /// 把 Motor内部的Update换成了TickLocomotion方法，方便在状态机中调用
    /// 本质上就是逐帧检查移动输入，并根据输入计算角色的移动方向、速度和旋转，然后应用这些变化到角色的Transform上。
    /// </summary>
    /// <param name="moveInput"></param>
    /// <param name="sprintInput"></param>
    /// <param name="deltaTime"></param>
    public void TickLocomotion(
        Vector2 moveInput,
        bool sprintInput,
        float deltaTime
    )
    {
        if (!_isInitialized)
            return;

        Vector3 moveDirection =
            GetCameraRelativeDirection(moveInput);

        UpdateHorizontalVelocity(moveDirection, sprintInput, deltaTime);
        RotateTowards(_horizontalVelocity, deltaTime);
        Move(deltaTime);
    }

    /// <summary>
    /// 计算玩家现在想达到的速度，并根据加速度或减速度平滑地更新水平速度。
    /// 1. 根据玩家的移动输入计算目标速度。
    /// 2. 根据目标速度的大小选择使用加速度还是减速度。
    /// 3. 使用 Vector3.MoveTowards 方法将当前水平速度平滑地移动到目标速度。
    /// 4. 将计算出的水平速度存储在 _horizontalVelocity 中，以便在后续的移动和旋转中使用。
    /// 方便后续拓展冲刺
    /// </summary>
    private void UpdateHorizontalVelocity(
        Vector3 moveDirection,
        bool sprintInput,
        float deltaTime
    )
    {
        // 存在移动输入 并且 按下冲刺键 才会使用冲刺速度
        bool isSprinting =
            sprintInput && moveDirection.sqrMagnitude > 0.0001f;
        float currentMoveSpeed = isSprinting ? _config.SprintSpeed : _config.MoveSpeed;
        
        // 如果没有输入，targetVelocity = moveDirection = 0
        Vector3 targetVelocity = moveDirection * currentMoveSpeed;
        // 根据当前速度和目标速度的大小选择使用加速度还是减速度
        float speedChangeRate = targetVelocity.sqrMagnitude > _horizontalVelocity.sqrMagnitude
            ? _config.Acceleration
            : _config.Deceleration;
        
        _horizontalVelocity = Vector3.MoveTowards(
            _horizontalVelocity,
            targetVelocity,
            speedChangeRate * deltaTime
        );
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
    private void RotateTowards(Vector3 moveDirection, float deltaTime)
    {
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(moveDirection, Vector3.up);

        float rotationAmount =
            1f - Mathf.Exp(
                -_config.RotationSharpness * deltaTime
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationAmount
        );
    }

    /// <summary>
    /// 根据当前的水平速度和垂直速度移动角色。
    /// 1. 检查角色是否在地面上，如果是，则将垂直速度设置为一个小的负值，以确保角色贴地。
    /// 2. 如果角色不在地面上，则根据重力加速度更新垂直速度。
    /// 3. 创建一个最终的速度向量，将水平速度和垂直速度组合在一起。
    /// 4. 使用 CharacterController.Move 方法将角色移动到新的位置。
    /// 
    /// </summary>
    private void Move(float deltaTime)
    {
        if (_characterController.isGrounded)
        {
            _verticalVelocity = _config.GroundStickForce;
        }
        else
        {
            _verticalVelocity += _config.Gravity * deltaTime;
        }

        Vector3 finalVelocity = _horizontalVelocity;
        finalVelocity.y = _verticalVelocity;

        _characterController.Move(
            finalVelocity * deltaTime
        );
    }

    private void OnDisable()
    {
        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
    }
}
