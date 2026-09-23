using Cinemachine;
using UnityEngine;

/// <summary>
/// Day5 Task3 锁定相机装配：
/// 1. 根据 PlayerTargeting 是否锁定，切换锁定虚拟相机的优先级
///    （锁定 15 / 未锁定 5），由 CinemachineBrain 负责两个相机之间的平滑混合，
///    不进行任何手动相机旋转。
/// 2. CameraRoot 水平位置跟随玩家，世界 Y 轴平滑跟随台阶高度；
///    每帧用 SmoothDamp 把 CameraTarget 平滑到锁定目标的 LockPoint；
///    未锁定时平滑回到 CameraRoot。锁定相机始终 LookAt CameraTarget。
///
/// 说明：CameraRoot / CameraTarget 是玩家子对象，玩家自身的移动与转身会
/// 通过父子层级"拖拽"它们的世界位置/旋转。因此这里在 LateUpdate
/// （执行顺序早于 CinemachineBrain）用内部权威状态重写两个锚点，
/// 保证 Brain 读取到的是干净、平滑的值。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class LockOnCameraRig : MonoBehaviour
{
    [Tooltip("玩家身上的锁定组件，CurrentTarget 变化驱动整台相机装配。")]
    [SerializeField] private PlayerTargeting _targeting;

    [Tooltip("未锁定时 CameraTarget 的归位锚点（玩家胸部，静止不动）。")]
    [SerializeField] private Transform _cameraRoot;

    [Tooltip("被锁定相机 LookAt 的平滑目标点（Player 预制体上的 CameraTarget）。")]
    [SerializeField] private Transform _cameraTarget;

    [Tooltip("锁定期间抬升优先级的虚拟相机。")]
    [SerializeField] private CinemachineVirtualCamera _lockOnCamera;

    [Header("Priority")]
    [SerializeField] private int _lockedPriority = 15;
    [SerializeField] private int _unlockedPriority = 5;

    [Header("Smoothing")]
    [Tooltip("CameraTarget 的平滑时间（秒），越大跟随/切换越慢越柔和。")]
    [SerializeField, Min(0.01f)] private float _smoothTime = 0.35f;

    [Tooltip("CameraRoot 的转向速度：锁定期间锚点平滑转向目标，确保锁定相机始终位于玩家背对目标一侧；数值越小镜头越惰性（横移时镜头几乎不转）。")]
    [SerializeField, Min(0.01f)] private float _rootTurnSharpness = 2.5f;

    [Tooltip("CameraRoot 世界 Y 轴跟随玩家高度的平滑时间（秒）。")]
    [SerializeField, Min(0.01f)] private float _rootVerticalSmoothTime = 0.15f;

    [Tooltip("目标高度单帧变化超过此值时，视为传送并立即同步。")]
    [SerializeField, Min(0f)] private float _rootVerticalSnapDistance = 2f;

    [Tooltip("持续升降时 CameraRoot 与目标高度允许的最大距离（米）。")]
    [SerializeField, Min(0.01f)] private float _rootVerticalMaxLag = 1.5f;

    private Vector3 _velocity;
    private Vector3 _cameraRootLocalOffset;
    private float _smoothedRootY;
    private float _rootVerticalVelocity;
    private float _previousDesiredRootY;
    private float _previousDesiredRootStepY;

    // 权威状态：与 Transform 的父子拖拽解耦，
    // 每帧由这里计算并硬写入 CameraTarget / CameraRoot，
    // 保证 CinemachineBrain 读取到的值只来源于本组件的平滑逻辑。
    private Vector3 _smoothedPosition;
    private Quaternion _rootRotation;

    private void Awake()
    {
        if (
            _targeting == null ||
            _cameraRoot == null ||
            _cameraTarget == null ||
            _lockOnCamera == null
        )
        {
            Debug.LogError(
                "LockOnCameraRig 引用不完整，锁定相机不会生效。",
                this
            );
            enabled = false;
            return;
        }

        _lockOnCamera.Priority = _unlockedPriority;
        _cameraRootLocalOffset = _cameraRoot.localPosition;
        _smoothedRootY = _cameraRoot.position.y;
        _previousDesiredRootY = _smoothedRootY;
        _smoothedPosition = _cameraTarget.position;
        _rootRotation = _cameraRoot.rotation;
    }

    private void LateUpdate()
    {
        bool locked =
            _targeting.CurrentTarget != null &&
            _targeting.CurrentTarget.LockPoint != null;

        _lockOnCamera.Priority =
            locked ? _lockedPriority : _unlockedPriority;

        UpdateCameraRootPosition();
        UpdateCameraRootRotation(locked);
        UpdateCameraTarget(locked);
    }

    /// <summary>水平位置立即跟随玩家，仅平滑 CameraRoot 的世界高度。</summary>
    private void UpdateCameraRootPosition()
    {
        Vector3 desired = _cameraRoot.parent != null
            ? _cameraRoot.parent.TransformPoint(_cameraRootLocalOffset)
            : _cameraRootLocalOffset;

        float targetHeightStep = desired.y - _previousDesiredRootY;
        // 持续下落的单帧位移会逐渐增大；只把突然改变的位移视为传送。
        bool suddenHeightJump =
            Mathf.Abs(targetHeightStep) > _rootVerticalSnapDistance &&
            Mathf.Abs(targetHeightStep - _previousDesiredRootStepY) >
            _rootVerticalSnapDistance;
        _previousDesiredRootY = desired.y;
        _previousDesiredRootStepY = targetHeightStep;

        if (suddenHeightJump)
        {
            _smoothedRootY = desired.y;
            _rootVerticalVelocity = 0f;
        }
        else
        {
            _smoothedRootY = Mathf.SmoothDamp(
                _smoothedRootY,
                desired.y,
                ref _rootVerticalVelocity,
                _rootVerticalSmoothTime
            );

            _smoothedRootY = Mathf.Clamp(
                _smoothedRootY,
                desired.y - _rootVerticalMaxLag,
                desired.y + _rootVerticalMaxLag
            );
        }

        _cameraRoot.position = new Vector3(desired.x, _smoothedRootY, desired.z);
    }

    /// <summary>
    /// 维护 CameraRoot 的朝向：
    /// 锁定时平滑转向"锚点 -> 目标"方向，锁定相机的背后偏移随之落在
    /// "玩家远离目标的一侧"（角色自由转身不会带动相机甩动）；
    /// 未锁定时平滑回到玩家朝向。
    /// </summary>
    private void UpdateCameraRootRotation(bool locked)
    {
        Vector3 desiredForward;

        if (locked)
        {
            desiredForward =
                _targeting.CurrentTarget.LockPoint.position -
                _cameraRoot.position;
        }
        else
        {
            desiredForward = _cameraRoot.parent != null
                ? _cameraRoot.parent.forward
                : _cameraRoot.forward;
        }

        desiredForward.y = 0f;

        if (desiredForward.sqrMagnitude < 0.0001f)
            return;

        Quaternion desiredRotation =
            Quaternion.LookRotation(desiredForward, Vector3.up);

        float rotationAmount =
            1f - Mathf.Exp(-_rootTurnSharpness * Time.deltaTime);

        _rootRotation = Quaternion.Slerp(
            _rootRotation,
            desiredRotation,
            rotationAmount
        );

        // 硬写入世界旋转，覆盖玩家转身对子对象的拖拽。
        _cameraRoot.rotation = _rootRotation;
    }

    /// <summary>每帧把 CameraTarget 平滑到锁定目标 / 归位锚点。</summary>
    private void UpdateCameraTarget(bool locked)
    {
        Vector3 desired = locked
            ? _targeting.CurrentTarget.LockPoint.position
            : _cameraRoot.position;

        _smoothedPosition = Vector3.SmoothDamp(
            _smoothedPosition,
            desired,
            ref _velocity,
            _smoothTime
        );

        // 硬写入世界位置，覆盖玩家移动对子对象的拖拽；
        // LateUpdate + 执行顺序 -50 保证 CinemachineBrain 读到最新值。
        _cameraTarget.position = _smoothedPosition;
    }
}
