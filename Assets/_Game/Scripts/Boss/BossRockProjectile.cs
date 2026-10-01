using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public sealed class BossRockProjectile : MonoBehaviour
{
    [SerializeField] LayerMask _collisionLayers = 1;
    [SerializeField] GameObject _breakEffect;
    [SerializeField, Min(.1f)] float _radius = .45f;
    [SerializeField, Min(.1f)] float _lifetime = 8;
    readonly RaycastHit[] hits = new RaycastHit[32];
    readonly Collider[] overlaps = new Collider[16];
    GameObject owner;
    Vector3 velocity;
    int damage;
    float age;
    bool resolved;
    public bool Resolved => resolved;
    public GameObject Owner => owner;

    public void Launch(GameObject attacker, Vector3 target, int amount)
    {
        owner = attacker; damage = amount; age = 0; resolved = false;
        float duration = Mathf.Clamp(Vector3.Distance(transform.position, target) / 12, .65f, 2);
        velocity = (target - transform.position - .5f * Physics.gravity * duration * duration) / duration;
    }

    void FixedUpdate()
    {
        if (resolved || owner == null) { Destroy(gameObject); return; }
        float dt = Time.fixedDeltaTime; age += dt;
        if (age >= _lifetime) { Dissolve(); return; }
        Vector3 displacement = velocity * dt + .5f * Physics.gravity * dt * dt;
        int overlapCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, overlaps, _collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++) if (IsValid(overlaps[i])) { Resolve(overlaps[i], transform.position); return; }
        float distance = displacement.magnitude;
        int count = Physics.SphereCastNonAlloc(transform.position, _radius, displacement.normalized, hits, distance, _collisionLayers, QueryTriggerInteraction.Ignore);
        int nearest = -1; float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++) if (IsValid(hits[i].collider) && hits[i].distance < nearestDistance) { nearest = i; nearestDistance = hits[i].distance; }
        if (nearest >= 0) { Resolve(hits[nearest].collider, hits[nearest].point); return; }
        transform.position += displacement; velocity += Physics.gravity * dt;
        transform.Rotate(90 * dt, 160 * dt, 60 * dt, Space.Self);
    }

    bool IsValid(Collider collider) => collider != null && collider.gameObject != gameObject && (owner == null || !collider.transform.IsChildOf(owner.transform));

    void Resolve(Collider collider, Vector3 point)
    {
        if (resolved) return;
        resolved = true;
        GetComponent<SphereCollider>().enabled = false;
        collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Damage = damage, Attacker = owner, HitPoint = point, HitDirection = velocity.normalized });
        if (_breakEffect != null)
        {
            var effect = Instantiate(_breakEffect, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            owner?.GetComponent<BossSkillRunner>()?.TrackEffect(effect);
            Destroy(effect, 1.5f);
        }
        Destroy(gameObject);
    }

    public void Dissolve() { resolved = true; GetComponent<SphereCollider>().enabled = false; Destroy(gameObject); }
}
