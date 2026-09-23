using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class EnemyTerritory : MonoBehaviour
{
    private const int GizmoSegments = 64;
    private const int PatrolSampleAttempts = 10;
    private const float PatrolSampleDistance = 0.75f;

    [SerializeField]
    private Transform _territoryCenterOverride = null;

    [SerializeField, Min(0f)]
    private float _patrolRadius = 5f;

    [SerializeField, Min(0f)]
    private float _detectionRadius = 8f;

    [SerializeField, Min(0f)]
    private float _leashRadius = 12f;

    private Vector3 _homePosition;
    private bool _hasHomePosition;
    private NavMeshPath _patrolPath;

    public Vector3 HomePosition => _hasHomePosition
        ? _homePosition
        : GetConfiguredCenter();

    public float PatrolRadius => _patrolRadius;
    public float DetectionRadius => _detectionRadius;
    public float LeashRadius => _leashRadius;

    private void Awake()
    {
        // Capture the center once so chasing cannot move the territory.
        _homePosition = GetConfiguredCenter();
        _hasHomePosition = true;
    }

    public float DistanceFromHome(Vector3 position)
    {
        Vector3 home = HomePosition;
        float x = position.x - home.x;
        float z = position.z - home.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    public bool IsInsidePatrolArea(Vector3 position)
    {
        return DistanceFromHome(position) <= _patrolRadius;
    }

    public bool IsInsideDetectionArea(Vector3 position)
    {
        return DistanceFromHome(position) <= _detectionRadius;
    }

    public bool IsInsideLeashArea(Vector3 position)
    {
        return DistanceFromHome(position) <= _leashRadius;
    }

    public bool TryGetPatrolPoint(Vector3 startPosition, int areaMask, out Vector3 point)
    {
        point = default;
        if (_patrolRadius <= 0f)
            return false;

        if (_patrolPath == null)
            _patrolPath = new NavMeshPath();

        Vector3 home = HomePosition;
        for (int i = 0; i < PatrolSampleAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * _patrolRadius;
            Vector3 candidate = new Vector3(
                home.x + offset.x,
                startPosition.y,
                home.z + offset.y
            );

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, PatrolSampleDistance, areaMask))
                continue;

            if (!IsInsidePatrolArea(hit.position))
                continue;

            Vector3 displacement = hit.position - startPosition;
            displacement.y = 0f;
            if (displacement.sqrMagnitude < 0.25f)
                continue;

            if (!NavMesh.CalculatePath(startPosition, hit.position, areaMask, _patrolPath) ||
                _patrolPath.status != NavMeshPathStatus.PathComplete)
                continue;

            point = hit.position;
            return true;
        }

        return false;
    }

    private Vector3 GetConfiguredCenter()
    {
        return _territoryCenterOverride != null
            ? _territoryCenterOverride.position
            : transform.position;
    }

    private void OnValidate()
    {
        _patrolRadius = Mathf.Max(0f, _patrolRadius);
        _detectionRadius = Mathf.Max(_patrolRadius, _detectionRadius);
        _leashRadius = Mathf.Max(_detectionRadius, _leashRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = HomePosition;
        DrawCircle(center, _patrolRadius, Color.yellow);
        DrawCircle(center, _detectionRadius, Color.blue);
        DrawCircle(center, _leashRadius, Color.red);
    }

    private static void DrawCircle(Vector3 center, float radius, Color color)
    {
        Gizmos.color = color;

        Vector3 previous = center + Vector3.forward * radius;
        for (int i = 1; i <= GizmoSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / GizmoSegments;
            Vector3 next = center + new Vector3(
                Mathf.Sin(angle) * radius,
                0f,
                Mathf.Cos(angle) * radius
            );
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
