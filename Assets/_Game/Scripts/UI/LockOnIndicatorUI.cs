using UnityEngine;

/// <summary>
/// Day5 锁定标记 UI：在 PlayerTargeting 当前锁定目标的 LockPoint 世界位置上
/// 显示唯一的 2D 锁定标记（Screen Space - Overlay Canvas 内）。
/// 只负责"显示当前已经锁定的目标"，不参与锁定算法；
/// 目标切换会自动跟随新的 CurrentLockPoint，解除锁定 / 目标失效 /
/// 位于摄像机后方时自动隐藏。
/// </summary>
[DisallowMultipleComponent]
public sealed class LockOnIndicatorUI : MonoBehaviour
{
    [Tooltip("玩家身上的锁定组件，提供 CurrentLockPoint。")]
    [SerializeField] private PlayerTargeting _targeting;

    [Tooltip("把世界坐标投影到屏幕坐标的相机（通常是 Main Camera）。")]
    [SerializeField] private Camera _camera;

    [Tooltip("锁定标记 Image（显隐由本组件控制）。")]
    [SerializeField] private RectTransform _indicator;

    [Tooltip("标记所在 Canvas 的 RectTransform（坐标转换基准）。")]
    [SerializeField] private RectTransform _canvasRect;

    private RectTransform _containerRect;

    private void Awake()
    {
        if (
            _targeting == null ||
            _camera == null ||
            _indicator == null ||
            _canvasRect == null
        )
        {
            Debug.LogError(
                "LockOnIndicatorUI 引用不完整，锁定标记不会显示。",
                this
            );
            enabled = false;
            return;
        }

        // 本组件挂在 LockOnIndicator 容器上，容器负责定位，
        // Image 子级只负责显示/隐藏。
        _containerRect = transform as RectTransform;
        _indicator.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        Transform lockPoint = _targeting.CurrentLockPoint;

        // 未锁定 / 目标失效（LockPoint 为空）时隐藏。
        if (lockPoint == null)
        {
            Hide();
            return;
        }

        Vector3 screenPoint = _camera.WorldToScreenPoint(lockPoint.position);

        // 目标位于摄像机后方时不显示。
        if (screenPoint.z <= 0f)
        {
            Hide();
            return;
        }

        // Screen Space - Overlay：转换用相机参数传 null。
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPoint,
                null,
                out Vector2 localPoint
            )
        )
        {
            Hide();
            return;
        }

        // localPoint 以 Canvas 的 pivot 为原点；容器锚点在中心 (0.5, 0.5)，
        // 需要补上"pivot -> 中心"的偏移（pivot 为标准中心时该偏移为 0）。
        Vector2 pivotOffset =
            (new Vector2(0.5f, 0.5f) - _canvasRect.pivot) *
            _canvasRect.rect.size;

        if (_containerRect != null)
            _containerRect.anchoredPosition = localPoint - pivotOffset;

        if (!_indicator.gameObject.activeSelf)
            _indicator.gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (_indicator.gameObject.activeSelf)
            _indicator.gameObject.SetActive(false);
    }
}
