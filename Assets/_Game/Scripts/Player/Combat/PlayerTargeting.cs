using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputReader))]
public sealed class PlayerTargeting : MonoBehaviour
{
    private const float OcclusionCheckInterval = 1f;

    [SerializeField, Min(0.1f)] private float _lockRange = 18f;
    [SerializeField] private LayerMask _targetLayers = 1 << 3;
    [SerializeField] private LayerMask _occlusionMask = 1 << 0;
    [SerializeField] private Camera _camera;
    [SerializeField] private bool _logTargetChanges = true;

    private readonly List<Candidate> _candidates = new List<Candidate>();

    // 本轮锁定会话的目标快照（建立时按"距屏幕中心"排序，会话期间固定）。
    private readonly List<Targetable> _sessionTargets = new List<Targetable>();

    // 本轮会话中已经锁定过的目标，用于防止重复锁定。
    private readonly List<Targetable> _visitedTargets = new List<Targetable>();

    private PlayerInputReader _inputReader;
    private float _nextOcclusionCheckTime;

    public Targetable CurrentTarget { get; private set; }
    public Transform CurrentLockPoint =>
        CurrentTarget != null ? CurrentTarget.LockPoint : null;

    private struct Candidate
    {
        public Targetable Target;
        public float CenterDistanceSquared;
    }

    private void Awake()
    {
        _inputReader = GetComponent<PlayerInputReader>();
        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
        {
            Debug.LogError("PlayerTargeting 找不到 MainCamera。", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // 当前目标失效或离开屏幕时立即结束会话。
        // 直接解除锁定并结束本轮会话，等待下次中键重新建立。
        if (!ReferenceEquals(CurrentTarget, null) &&
            (!IsTargetValid(CurrentTarget) ||
             !IsTargetOnScreen(CurrentTarget, out _)))
        {
            EndSession();
            return;
        }

        if (CurrentTarget != null && Time.time >= _nextOcclusionCheckTime)
        {
            _nextOcclusionCheckTime = Time.time + OcclusionCheckInterval;
            if (!HasLineOfSight(CurrentTarget))
            {
                EndSession();
                return;
            }
        }

        if (_inputReader.ConsumeLockOn())
            HandleLockOnPressed();
    }

    private void OnDisable()
    {
        EndSession();
        _candidates.Clear();
    }

    /// <summary>
    /// 中键处理：
    /// 未锁定时建立新一轮会话并锁定"距屏幕中心最近"的目标；
    /// 已锁定时切换到会话中下一个未访问的目标，没有则解锁并结束会话。
    /// </summary>
    private void HandleLockOnPressed()
    {
        if (CurrentTarget == null)
            StartSession();
        else
            AdvanceToNextTarget();
    }

    /// <summary>
    /// 建立新的锁定会话：
    /// 搜索可视目标 -> 按"距屏幕中心"升序排序 -> 保存本轮快照，
    /// 锁定首个目标并标记已访问。
    /// 会话建立后目标集合固定：中途新进入范围的敌人不参与本轮。
    /// </summary>
    private void StartSession()
    {
        FindVisibleTargets();
        if (_candidates.Count == 0)
            return;

        // 主要顺序只由"距屏幕中心"决定（不依赖 OverlapSphere /
        // Hierarchy / InstanceID 的返回顺序）。
        _candidates.Sort((a, b) =>
            a.CenterDistanceSquared.CompareTo(b.CenterDistanceSquared));

        _sessionTargets.Clear();
        _visitedTargets.Clear();

        foreach (Candidate candidate in _candidates)
            _sessionTargets.Add(candidate.Target);

        LockTarget(_sessionTargets[0]);
    }

    /// <summary>
    /// 切换到会话中下一个未访问且当前可见的有效目标；
    /// 找不到时解锁并清空会话。
    /// </summary>
    private void AdvanceToNextTarget()
    {
        Targetable next = null;

        foreach (Targetable target in _sessionTargets)
        {
            if (target == null)
                continue;

            if (_visitedTargets.Contains(target))
                continue;

            if (!IsTargetValid(target) || !IsTargetVisible(target, out _))
                continue;

            next = target;
            break;
        }

        if (next == null)
        {
            EndSession();
            return;
        }

        LockTarget(next);
    }

    /// <summary>锁定目标并记入已访问列表。</summary>
    private void LockTarget(Targetable target)
    {
        SetTarget(target);
        _nextOcclusionCheckTime = Time.time + OcclusionCheckInterval;

        if (!_visitedTargets.Contains(target))
            _visitedTargets.Add(target);
    }

    /// <summary>结束会话：解除锁定并清空快照与访问记录。</summary>
    private void EndSession()
    {
        SetTarget(null);
        _sessionTargets.Clear();
        _visitedTargets.Clear();
    }

    private void FindVisibleTargets()
    {
        _candidates.Clear();
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            _lockRange,
            _targetLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            Targetable target = hit.GetComponentInParent<Targetable>();
            if (!IsTargetValid(target))
                continue;

            bool alreadyAdded = false;
            foreach (Candidate candidate in _candidates)
            {
                if (candidate.Target == target)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (alreadyAdded)
                continue;

            if (!IsTargetVisible(target, out Vector3 viewport))
                continue;

            float x = viewport.x - 0.5f;
            float y = viewport.y - 0.5f;
            _candidates.Add(new Candidate
            {
                Target = target,
                CenterDistanceSquared = x * x + y * y
            });
        }
    }

    private bool IsTargetValid(Targetable target)
    {
        return target != null &&
            target.IsAvailable &&
            (target.LockPoint.position - transform.position).sqrMagnitude <=
            _lockRange * _lockRange;
    }

    private bool IsTargetVisible(Targetable target, out Vector3 viewport)
    {
        return IsTargetOnScreen(target, out viewport) && HasLineOfSight(target);
    }

    private bool IsTargetOnScreen(Targetable target, out Vector3 viewport)
    {
        viewport = _camera.WorldToViewportPoint(target.LockPoint.position);
        return viewport.z > 0f &&
            viewport.x >= 0f && viewport.x <= 1f &&
            viewport.y >= 0f && viewport.y <= 1f;
    }

    private bool HasLineOfSight(Targetable target)
    {
        Vector3 origin = _camera.transform.position;
        Vector3 direction = target.LockPoint.position - origin;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            direction.magnitude,
            _occlusionMask,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<Targetable>() == target)
                continue;

            return false;
        }

        return true;
    }

    private void SetTarget(Targetable target)
    {
        if (ReferenceEquals(CurrentTarget, target))
            return;

        CurrentTarget = target;
        if (_logTargetChanges)
            Debug.Log($"Lock-on Target: {(target != null ? target.name : "None")}", this);
    }
}
