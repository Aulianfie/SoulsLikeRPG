using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponHitbox : MonoBehaviour
{
    private const int MaxOverlaps = 16;
    private const int MaxSweepSamples = 6;
    private const float MaxSweepStepDistance = 0.15f;
    private const float MaxSweepStepAngle = 15f;

    [SerializeField]
    private LayerMask _targetLayers;

    [SerializeField]
    private BoxCollider _shape;

    private readonly Collider[] _overlaps =
        new Collider[MaxOverlaps];

    private readonly HashSet<IDamageable> _hitTargets =
        new HashSet<IDamageable>();

    private GameObject _attacker;
    private bool _isActive;
    private bool _hasPreviousPose;
    private int _damage;
    private Vector3 _previousCenter;
    private Quaternion _previousRotation;

    private void Awake()
    {
        _attacker = transform.root.gameObject;

        if (_shape == null)
            _shape = GetComponentInChildren<BoxCollider>(true);

        if (_shape == null)
        {
            Debug.LogError(
                "WeaponHitbox 找不到用于检测剑刃的 BoxCollider。",
                this
            );

            enabled = false;
            return;
        }

        // BoxCollider 仅作为可视化、可调节的检测形状。
        _shape.enabled = false;
    }

    private void LateUpdate()
    {
        if (_isActive)
            DetectTargets();
    }

    public void BeginAttack(int damage)
    {
        _damage = damage;
        _hitTargets.Clear();
        _hasPreviousPose = false;
        _isActive = true;
    }

    public void EndAttack()
    {
        _isActive = false;
        _hasPreviousPose = false;
        _damage = 0;
        _hitTargets.Clear();
    }

    private void DetectTargets()
    {
        Transform shapeTransform = _shape.transform;
        Vector3 center = shapeTransform.TransformPoint(_shape.center);

        Vector3 scale = shapeTransform.lossyScale;
        scale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        Vector3 halfExtents = Vector3.Scale(
            _shape.size * 0.5f,
            scale
        );

        Quaternion rotation = shapeTransform.rotation;

        if (!_hasPreviousPose)
        {
            DetectTargetsAtPose(center, halfExtents, rotation);
            RememberPose(center, rotation);
            return;
        }

        int positionSamples = Mathf.CeilToInt(
            Vector3.Distance(_previousCenter, center) /
            MaxSweepStepDistance
        );
        int rotationSamples = Mathf.CeilToInt(
            Quaternion.Angle(_previousRotation, rotation) /
            MaxSweepStepAngle
        );
        int sampleCount = Mathf.Clamp(
            Mathf.Max(positionSamples, rotationSamples),
            1,
            MaxSweepSamples
        );

        // 补查武器在相邻两帧之间扫过的空间，避免快速挥砍穿过目标。
        for (int i = 1; i <= sampleCount; i++)
        {
            float interpolation = i / (float)sampleCount;
            Vector3 sampleCenter = Vector3.Lerp(
                _previousCenter,
                center,
                interpolation
            );
            Quaternion sampleRotation = Quaternion.Slerp(
                _previousRotation,
                rotation,
                interpolation
            );

            DetectTargetsAtPose(
                sampleCenter,
                halfExtents,
                sampleRotation
            );
        }

        RememberPose(center, rotation);
    }

    private void DetectTargetsAtPose(
        Vector3 center,
        Vector3 halfExtents,
        Quaternion rotation
    )
    {
        int overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _overlaps,
            rotation,
            _targetLayers,
            QueryTriggerInteraction.Ignore
        );
        for (int i = 0; i < overlapCount; i++)
        {
            Collider targetCollider = _overlaps[i];
            IDamageable target =
                targetCollider.GetComponentInParent<IDamageable>();

            if (target == null || !_hitTargets.Add(target))
                continue;

            Vector3 hitPoint = targetCollider.ClosestPoint(center);
            Vector3 directionOrigin = _attacker != null
                ? _attacker.transform.position
                : center;
            Vector3 hitDirection =
                (targetCollider.bounds.center - directionOrigin)
                .normalized;

            DamageInfo damageInfo = new DamageInfo
            {
                Damage = _damage,
                HitPoint = hitPoint,
                HitDirection = hitDirection,
                Attacker = _attacker
            };

            target.TakeDamage(damageInfo);
        }
    }

    private void RememberPose(Vector3 center, Quaternion rotation)
    {
        _previousCenter = center;
        _previousRotation = rotation;
        _hasPreviousPose = true;
    }

    private void OnDisable()
    {
        EndAttack();
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider shape = _shape;

        if (shape == null)
            shape = GetComponentInChildren<BoxCollider>(true);

        if (shape == null)
            return;

        Gizmos.color = _isActive ? Color.green : Color.yellow;
        Gizmos.matrix = shape.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(shape.center, shape.size);
    }
}
