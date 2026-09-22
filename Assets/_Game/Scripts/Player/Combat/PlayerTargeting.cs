using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputReader))]
public sealed class PlayerTargeting : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _lockRange = 18f;
    [SerializeField] private LayerMask _targetLayers = 1 << 3;
    [SerializeField] private Camera _camera;
    [SerializeField] private bool _logTargetChanges = true;

    private readonly List<Candidate> _candidates = new List<Candidate>();
    private PlayerInputReader _inputReader;

    public Targetable CurrentTarget { get; private set; }
    public Transform CurrentLockPoint =>
        CurrentTarget != null ? CurrentTarget.LockPoint : null;

    private struct Candidate
    {
        public Targetable Target;
        public Vector3 Viewport;
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
        if (!ReferenceEquals(CurrentTarget, null) &&
            !IsTargetValid(CurrentTarget))
            SetTarget(null);

        if (_inputReader.ConsumeLockOn())
            SelectTarget();
    }

    private void OnDisable()
    {
        SetTarget(null);
        _candidates.Clear();
    }

    private void SelectTarget()
    {
        FindVisibleTargets();
        if (_candidates.Count == 0)
            return;

        if (CurrentTarget == null)
        {
            Candidate best = _candidates[0];
            for (int i = 1; i < _candidates.Count; i++)
            {
                if (_candidates[i].CenterDistanceSquared <
                    best.CenterDistanceSquared)
                    best = _candidates[i];
            }

            SetTarget(best.Target);
            return;
        }

        _candidates.Sort((a, b) =>
        {
            int xOrder = a.Viewport.x.CompareTo(b.Viewport.x);
            if (xOrder != 0)
                return xOrder;

            int yOrder = a.Viewport.y.CompareTo(b.Viewport.y);
            return yOrder != 0
                ? yOrder
                : a.Target.GetInstanceID().CompareTo(b.Target.GetInstanceID());
        });

        int currentIndex = _candidates.FindIndex(
            candidate => candidate.Target == CurrentTarget);
        int nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + 1) % _candidates.Count;
        SetTarget(_candidates[nextIndex].Target);
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

            Vector3 viewport = _camera.WorldToViewportPoint(
                target.LockPoint.position);
            if (viewport.z <= 0f ||
                viewport.x < 0f || viewport.x > 1f ||
                viewport.y < 0f || viewport.y > 1f)
                continue;

            float x = viewport.x - 0.5f;
            float y = viewport.y - 0.5f;
            _candidates.Add(new Candidate
            {
                Target = target,
                Viewport = viewport,
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

    private void SetTarget(Targetable target)
    {
        if (ReferenceEquals(CurrentTarget, target))
            return;

        CurrentTarget = target;
        if (_logTargetChanges)
            Debug.Log($"Lock-on Target: {(target != null ? target.name : "None")}", this);
    }
}
