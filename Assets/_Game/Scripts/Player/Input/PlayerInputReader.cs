using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool SprintInput { get; private set; }

    private InputActionMap _gameplayMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _sprintAction;

    /// <summary>
    /// 初始化玩家输入读取器，绑定输入动作到相应的回调函数。
    /// 1. 获取 PlayerInput 组件，并找到 "Gameplay" 动作映射
    /// 2. 获取 "Move"、"Look" 和 "Sprint" 动作
    /// 3. 在 OnEnable 中，订阅这些动作的 performed 和 canceled 事件，以便在玩家输入时更新 MoveInput、LookInput 和 SprintInput。
    /// 4. 在 OnDisable 中，取消订阅这些事件，以避免内存泄漏或重复调用。
    /// 5. 在 OnMove、OnLook 和 OnSprint 回调中，读取输入值并更新相应的属性，同时输出调试信息。
    /// </summary>
    private void Awake()
    {
        var playerInput = GetComponent<PlayerInput>();
        _gameplayMap = playerInput.actions.FindActionMap("Gameplay");
        _moveAction = _gameplayMap.FindAction("Move");
        _lookAction = _gameplayMap.FindAction("Look");
        _sprintAction = _gameplayMap.FindAction("Sprint");
    }

    private void OnEnable()
    {
        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;

        _lookAction.performed += OnLook;
        _lookAction.canceled += OnLook;

        _sprintAction.performed += OnSprint;
        _sprintAction.canceled += OnSprint;

        _gameplayMap.Enable();
    }

    private void OnDisable()
    {
        _moveAction.performed -= OnMove;
        _moveAction.canceled -= OnMove;

        _lookAction.performed -= OnLook;
        _lookAction.canceled -= OnLook;

        _sprintAction.performed -= OnSprint;
        _sprintAction.canceled -= OnSprint;

        _gameplayMap.Disable();
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
}
