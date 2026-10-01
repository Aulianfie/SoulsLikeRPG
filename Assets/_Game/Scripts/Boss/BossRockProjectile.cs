using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public sealed class BossRockProjectile : MonoBehaviour
{
    // Serialized fields
    [SerializeField]
    private LayerMask _collisionLayers = 1;
    [SerializeField]
    private GameObject _breakEffect;
    [SerializeField, Min(.1f)]
    private float _radius = .45f;
    [SerializeField, Min(.1f)]
    private float _lifetime = 8;

    // Runtime state
    private readonly RaycastHit[] _collisionHits = new RaycastHit[32];
    private readonly Collider[] _initialOverlaps = new Collider[16];
    private GameObject _owner;
    private Vector3 _velocity;
    private int _damage;
    private float _age;
    private bool _isResolved;

    // Public properties
    public bool Resolved => _isResolved;
    public GameObject Owner => _owner;

    public void Launch(GameObject attacker, Vector3 target, int amount)
    {
        _owner = attacker;
        _damage = amount;
        _age = 0;
        _isResolved = false;
        float duration = Mathf.Clamp(Vector3.Distance(transform.position, target) / 12, .65f, 2);
        _velocity = (target - transform.position - .5f * Physics.gravity * duration * duration) / duration;
    }

    private void FixedUpdate()
    {
        if (_isResolved ||
            _owner == null)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.fixedDeltaTime;
        _age += dt;
        if (_age >= _lifetime)
        {
            Dissolve();
            return;
        }

        Vector3 displacement = _velocity * dt + .5f * Physics.gravity * dt * dt;
        int overlapCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _initialOverlaps, _collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++)
        {
            if (IsValid(_initialOverlaps[i]))
            {
                Resolve(_initialOverlaps[i], transform.position);
                return;
            }
        }

        float distance = displacement.magnitude;
        int count = Physics.SphereCastNonAlloc(
            transform.position,
            _radius,
            displacement.normalized,
            _collisionHits,
            distance,
            _collisionLayers,
            QueryTriggerInteraction.Ignore
        );
        int nearest = -1;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (IsValid(_collisionHits[i].collider) &&
                _collisionHits[i].distance < nearestDistance)
            {
                nearest = i;
                nearestDistance = _collisionHits[i].distance;
            }
        }

        if (nearest >= 0)
        {
            Resolve(_collisionHits[nearest].collider, _collisionHits[nearest].point);
            return;
        }

        transform.position += displacement;
        _velocity += Physics.gravity * dt;
        transform.Rotate(90 * dt, 160 * dt, 60 * dt, Space.Self);
    }

    private bool IsValid(Collider collider)
    {
        return collider != null &&
            collider.gameObject != gameObject &&
            (_owner == null ||
            !collider.transform.IsChildOf(_owner.transform));
    }

    private void Resolve(Collider collider, Vector3 point)
    {
        if (_isResolved)
        {
            return;
        }

        _isResolved = true;
        GetComponent<SphereCollider>().enabled = false;
        collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo
            {
                Damage = _damage,
                Attacker = _owner,
                HitPoint = point,
                HitDirection = _velocity.normalized
            });
        if (_breakEffect != null)
        {
            var effect = Instantiate(_breakEffect, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            _owner?.GetComponent<BossSkillRunner>()?.TrackEffect(effect);
            Destroy(effect, 1.5f);
        }

        Destroy(gameObject);
    }

    public void Dissolve()
    {
        _isResolved = true;
        GetComponent<SphereCollider>().enabled = false;
        Destroy(gameObject);
    }
}
