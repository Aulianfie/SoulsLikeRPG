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
    private bool _specialMovement;
    private readonly RaycastHit[] _specialHits = new RaycastHit[16];

    public bool IsOnNavMesh => _agent != null &&
        _agent.enabled &&
        _agent.isOnNavMesh;
    public bool IsPathPending => IsOnNavMesh &&
        _agent.pathPending;
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
        {
            Debug.LogWarning("EnemyMotor 未处于 NavMesh 上，请烘焙场景并检查敌人出生点。", this);
        }
    }

    private void LateUpdate()
    {
        if (IsOnNavMesh &&
            !_agent.isStopped &&
            !_specialMovement)
        {
            FaceDirection(_agent.desiredVelocity, Time.deltaTime);
        }
    }

    public bool MoveTo(Vector3 position)
    {
        return MoveTo(position, _defaultStoppingDistance);
    }

    public bool MoveTo(Vector3 position, float stoppingDistance)
    {
        if (_specialMovement)
        {
            return false;
        }

        if (!IsOnNavMesh)
        {
            return false;
        }

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
        {
            return;
        }

        _agent.isStopped = true;
        _agent.ResetPath();
    }

    /// <summary>
    /// 传送敌人到指定位置和朝向，并确保敌人仍然处于 NavMesh 上。
    /// 如果传送后敌人不在 NavMesh 上，会尝试通过禁用 NavMeshAgent 并直接设置 Transform 来修复位置，但会发出警告。
    /// </summary>
    /// <param name = "position"></param>
    /// <param name = "rotation"></param>
    /// <returns></returns>
    public bool Teleport(Vector3 position, Quaternion rotation)
    {
        EndSpecialMovement();
        Stop();
        _agent.stoppingDistance = _defaultStoppingDistance;
        if (!IsOnNavMesh ||
            !_agent.Warp(position))
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
        return _hasDestination &&
            IsOnNavMesh &&
            !_agent.pathPending &&
            _agent.pathStatus == NavMeshPathStatus.PathComplete &&
            _agent.remainingDistance <= _agent.stoppingDistance + ArrivalTolerance;
    }

    public bool HasValidPath()
    {
        return IsOnNavMesh &&
            _agent.hasPath &&
            _agent.pathStatus == NavMeshPathStatus.PathComplete;
    }

    public void FaceTarget(Vector3 targetPosition, float deltaTime)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        FaceDirection(direction, deltaTime);
    }

    public void BeginSpecialMovement()
    {
        Stop();
        _specialMovement = true;
        if (IsOnNavMesh)
        {
            _agent.updatePosition = false;
        }
    }

    public bool CanMoveSpecial(Vector3 direction, float distance, LayerMask obstacleLayers)
    {
        if (!IsOnNavMesh ||
            direction.sqrMagnitude < .001f)
        {
            return false;
        }

        direction.y = 0;
        direction.Normalize();
        Vector3 destination = transform.position + direction * distance;
        if (NavMesh.Raycast(transform.position, destination, out _, _agent.areaMask))
        {
            return false;
        }

        float radius = Mathf.Max(.15f, _agent.radius);
        float height = Mathf.Max(radius * 2, _agent.height);
        Vector3 bottom = transform.position + Vector3.up * (radius + .12f);
        Vector3 top = transform.position + Vector3.up * (height - radius);
        int count = Physics.CapsuleCastNonAlloc(
            bottom,
            top,
            radius,
            direction,
            _specialHits,
            distance + .05f,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );
        for (int i = 0; i < count; i++)
        {
            var collider = _specialHits[i].collider;
            if (collider.transform.IsChildOf(transform) ||
                collider.GetComponentInParent<PlayerHealth>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public bool MoveSpecial(Vector3 direction, float distance, LayerMask obstacleLayers)
    {
        if (!_specialMovement ||
            !CanMoveSpecial(direction, distance, obstacleLayers))
        {
            return false;
        }

        Vector3 next = transform.position + direction.normalized * distance;
        if (!NavMesh.SamplePosition(next, out NavMeshHit hit, .25f, _agent.areaMask))
        {
            return false;
        }

        transform.position = hit.position;
        _agent.nextPosition = hit.position;
        return true;
    }

    public void EndSpecialMovement()
    {
        if (!_specialMovement)
        {
            return;
        }

        _specialMovement = false;
        if (IsOnNavMesh)
        {
            _agent.Warp(transform.position);
            _agent.updatePosition = true;
        }
    }

    private void FaceDirection(Vector3 direction, float deltaTime)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        float rotationAmount = 1f - Mathf.Exp(-_rotationSharpness * deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationAmount);
    }

    private void OnDisable()
    {
        EndSpecialMovement();
        Stop();
    }
}
