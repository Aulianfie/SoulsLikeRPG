using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    [Header("Input Buffer")]
    [SerializeField, Min(0f)] private float _lightAttackBufferDuration = 0.20f;
    [SerializeField, Min(0f)] private float _dodgeBufferDuration = 0.15f;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool SprintInput { get; private set; }

    private InputActionMap _gameplayMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _sprintAction;
    private InputAction _jumpAction;
    private InputAction _lightAttackAction;
    private InputAction _dodgeAction;
    private bool _jumpRequested;
    private bool _lightAttackRequested;
    private bool _dodgeRequested;
    private float _lightAttackExpireTime;
    private float _dodgeExpireTime;

    public bool HasBufferedLightAttack =>
        _lightAttackRequested && Time.time <= _lightAttackExpireTime;

    public bool HasBufferedDodge =>
        _dodgeRequested && Time.time <= _dodgeExpireTime;

    /// <summary>
    /// 初始化玩家输入读取器，绑定输入动作到相应的回调函数。
    /// 1. 获取 PlayerInput 组件，并找到 "Gameplay" 动作映射
    /// 2. 获取 "Move"、"Look" 和 "Sprint" 动作
    /// 3. 在 OnEnable 中，订阅这些动作的 performed 和 canceled 事件，以便在玩家输入时更新 MoveInput、LookInput 和 SprintInput。
    /// 4. 在 OnDisable 中，取消订阅这些事件，以避免内存泄漏或重复调用。
    /// 5. 在 OnMove、OnLook 和 OnSprint 回调中，读取输入值并更新相应的属性，同时输出调试信息。
    /// </summary>
    private void CacheActions()
    {
        var playerInput = GetComponent<PlayerInput>();
        _gameplayMap = playerInput.actions.FindActionMap(
            "Gameplay",
            true
        );
        _moveAction = _gameplayMap.FindAction("Move", true);
        _lookAction = _gameplayMap.FindAction("Look", true);
        _sprintAction = _gameplayMap.FindAction("Sprint", true);
        _jumpAction = _gameplayMap.FindAction("Jump", true);
        _lightAttackAction = _gameplayMap.FindAction(
            "LightAttack",
            true
        );
        _dodgeAction = _gameplayMap.FindAction("Dodge", true);
    }

    private void OnEnable()
    {
        CacheActions();

        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;

        _lookAction.performed += OnLook;
        _lookAction.canceled += OnLook;

        _sprintAction.performed += OnSprint;
        _sprintAction.canceled += OnSprint;

        _jumpAction.performed += OnJump;
        _lightAttackAction.performed += OnLightAttack;
        _dodgeAction.performed += OnDodge;

        _gameplayMap.Enable();
    }

    private void OnDisable()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;
        }

        if (_lookAction != null)
        {
            _lookAction.performed -= OnLook;
            _lookAction.canceled -= OnLook;
        }

        if (_sprintAction != null)
        {
            _sprintAction.performed -= OnSprint;
            _sprintAction.canceled -= OnSprint;
        }

        if (_jumpAction != null)
            _jumpAction.performed -= OnJump;

        if (_lightAttackAction != null)
            _lightAttackAction.performed -= OnLightAttack;

        if (_dodgeAction != null)
            _dodgeAction.performed -= OnDodge;

        _jumpRequested = false;
        ClearAllBuffers();

        _gameplayMap?.Disable();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
        // Debug.Log($"Move: {MoveInput}");
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
        // Debug.Log($"Look: {LookInput}");
    }

    private void OnSprint(InputAction.CallbackContext context)
    {
        SprintInput = context.ReadValueAsButton();
        // Debug.Log($"Sprint: {SprintInput}");
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        _jumpRequested = true;
    }

    private void OnLightAttack(InputAction.CallbackContext context)
    {
        _lightAttackRequested = true;
        _lightAttackExpireTime = Time.time + _lightAttackBufferDuration;
    }

    private void OnDodge(InputAction.CallbackContext context)
    {
        _dodgeRequested = true;
        _dodgeExpireTime = Time.time + _dodgeBufferDuration;
    }

    public bool ConsumeJump()
    {
        if (!_jumpRequested)
            return false;

        _jumpRequested = false;
        return true;
    }

    public bool ConsumeLightAttack()
    {
        return ConsumeBufferedLightAttack();
    }

    public bool ConsumeDodge()
    {
        return ConsumeBufferedDodge();
    }

    public bool ConsumeBufferedLightAttack()
    {
        bool wasBuffered = HasBufferedLightAttack;
        ClearLightAttackBuffer();
        return wasBuffered;
    }

    public bool ConsumeBufferedDodge()
    {
        bool wasBuffered = HasBufferedDodge;
        ClearDodgeBuffer();
        return wasBuffered;
    }

    public void ClearLightAttackBuffer()
    {
        _lightAttackRequested = false;
        _lightAttackExpireTime = 0f;
    }

    public void ClearDodgeBuffer()
    {
        _dodgeRequested = false;
        _dodgeExpireTime = 0f;
    }

    public void ClearAllBuffers()
    {
        ClearLightAttackBuffer();
        ClearDodgeBuffer();
    }
}
