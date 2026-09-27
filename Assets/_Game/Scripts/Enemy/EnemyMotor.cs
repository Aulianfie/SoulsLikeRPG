using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyMotor : MonoBehaviour
{
    private const float ArrivalTolerance = 0.15f;

    [SerializeField, Min(0f)]
    private float _rotationSharpness = 12f;

    private NavMeshAgent _agent;
    private float _defaultStoppingDistance;
    private bool _hasDestination;

    public bool IsOnNavMesh => _agent != null && _agent.enabled && _agent.isOnNavMesh;
    public bool IsPathPending => IsOnNavMesh && _agent.pathPending;
    public int AreaMask => _agent.areaMask;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _defaultStoppingDistance = _agent.stoppingDistance;
        _agent.updateRotation = false;
    }

    private void Start()
    {
        if (!IsOnNavMesh)
            Debug.LogWarning("EnemyMotor 未处于 NavMesh 上，请烘焙场景并检查敌人出生点。", this);
    }

    private void LateUpdate()
    {
        if (IsOnNavMesh && !_agent.isStopped)
            FaceDirection(_agent.desiredVelocity, Time.deltaTime);
    }

    public bool MoveTo(Vector3 position)
    {
        return MoveTo(position, _defaultStoppingDistance);
    }

    public bool MoveTo(Vector3 position, float stoppingDistance)
    {
        if (!IsOnNavMesh)
            return false;

        _agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        _agent.isStopped = false;
        if (!_agent.SetDestination(position))
        {
            Stop();
            return false;
        }

        _hasDestination = true;
        return true;
    }

    public void Stop()
    {
        _hasDestination = false;

        if (!IsOnNavMesh)
            return;

        _agent.isStopped = true;
        _agent.ResetPath();
    }
    /// <summary>
    /// 传送敌人到指定位置和朝向，并确保敌人仍然处于 NavMesh 上。
    /// 如果传送后敌人不在 NavMesh 上，会尝试通过禁用 NavMeshAgent 并直接设置 Transform 来修复位置，但会发出警告。
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public bool Teleport(Vector3 position, Quaternion rotation)
    {
        Stop();
        _agent.stoppingDistance = _defaultStoppingDistance;

        if (!IsOnNavMesh || !_agent.Warp(position))
        {
            bool wasEnabled = _agent.enabled;
            _agent.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _agent.enabled = wasEnabled;
            Debug.LogWarning("EnemyMotor 传送后未处于 NavMesh 上，敌人暂时无法巡逻。", this);
            return false;
        }

        transform.rotation = rotation;
        _agent.velocity = Vector3.zero;
        return true;
    }

    public bool HasReachedDestination()
    {
        return
            _hasDestination &&
            IsOnNavMesh &&
            !_agent.pathPending &&
            _agent.pathStatus == NavMeshPathStatus.PathComplete &&
            _agent.remainingDistance <= _agent.stoppingDistance + ArrivalTolerance;
    }

    public bool HasValidPath()
    {
        return
            IsOnNavMesh &&
            _agent.hasPath &&
            _agent.pathStatus == NavMeshPathStatus.PathComplete;
    }

    public void FaceTarget(Vector3 targetPosition, float deltaTime)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        FaceDirection(direction, deltaTime);
    }

    private void FaceDirection(Vector3 direction, float deltaTime)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        float rotationAmount = 1f - Mathf.Exp(-_rotationSharpness * deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationAmount);
    }

    private void OnDisable()
    {
        Stop();
    }
}
