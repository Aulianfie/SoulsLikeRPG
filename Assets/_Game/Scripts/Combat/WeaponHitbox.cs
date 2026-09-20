using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponHitbox : MonoBehaviour
{
    private const int MaxOverlaps = 16;

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
    private int _damage;

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

    private void Update()
    {
        if (_isActive)
            DetectTargets();
    }

    public void BeginAttack(int damage)
    {
        _damage = damage;
        _hitTargets.Clear();
        _isActive = true;
    }

    public void EndAttack()
    {
        _isActive = false;
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

        int overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _overlaps,
            shapeTransform.rotation,
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
