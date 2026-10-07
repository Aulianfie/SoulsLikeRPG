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
    private bool _isLaunched;

    // Public properties
    public bool Resolved => _isResolved;
    public GameObject Owner => _owner;
    public bool IsLaunched => _isLaunched;

    private void Awake()
    {
        // 石头随手部动画经过地面时只显示模型，发射之前不参与碰撞。
        GetComponent<SphereCollider>().enabled = false;
    }

    /// <summary>
    /// 石头准备被 Boss 挖掘起来，抛出前不参与碰撞检测。
    /// 这个方法会在 Boss 手部动画的事件中调用。
    /// </summary>
    /// <param name="attacker"></param>
    public void PrepareHeld(GameObject attacker)
    {
        _owner = attacker;
        _isLaunched = false;
        _isResolved = false;
        _age = 0;
        GetComponent<SphereCollider>().enabled = false;
    }

    /// <summary>
    /// 石头在 Boss 手部动画中被抛出，开始参与碰撞检测。
    /// </summary>
    /// <param name="attacker"></param>
    /// <param name="target"></param>
    /// <param name="amount"></param>
    public void Launch(GameObject attacker, Vector3 target, int amount)
    {
        transform.SetParent(null, true);
        _owner = attacker;
        _damage = amount;
        _age = 0;
        _isResolved = false;
        _isLaunched = true;
        GetComponent<SphereCollider>().enabled = true;

        // 根据石头位置和目标位置计算抛物线的初速度，确保石头在 0.65~2 秒内落到目标点。
        float duration = Mathf.Clamp(Vector3.Distance(transform.position, target) / 12, .65f, 2);
        // 目标位置 = 起点 + 初速度 × t + 1/2 × 重力 × t² 把初速度反解出来
        _velocity = (target - transform.position - .5f * Physics.gravity * duration * duration) / duration;
    }

    private void FixedUpdate()
    {
        // 如果石头已经与目标碰撞，则销毁石头
        if (_isResolved ||
            _owner == null)
        {
            Destroy(gameObject);
            return;
        }

        // 如果石头还没有被抛出，则下一个 tick 再处理
        if (!_isLaunched)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        _age += dt;

        // 如果石头飞行时间超过最大寿命，则销毁石头
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="collider"></param>
    /// <returns></returns>
    private bool IsValid(Collider collider)
    {
        return collider != null &&
            collider.gameObject != gameObject &&
            (_owner == null ||
            !collider.transform.IsChildOf(_owner.transform));
    }

    /// <summary>
    /// 处理石头与目标的碰撞，造成伤害并播放破碎特效。
    /// </summary>
    /// <param name="collider"></param>
    /// <param name="point"></param>
    private void Resolve(Collider collider, Vector3 point)
    {
        if (_isResolved)
        {
            return;
        }

        _isResolved = true;
        GetComponent<SphereCollider>().enabled = false;
        // 如果石头碰撞到的物体实现了 IDamageable 接口，则调用 TakeDamage 方法造成伤害
        collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo
            {
                Damage = _damage,
                Attacker = _owner,
                HitPoint = point,
                HitDirection = _velocity.normalized
            });
        
        // 播放破碎特效并在 1.5 秒后销毁
        if (_breakEffect != null)
        {
            var effect = Instantiate(_breakEffect, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            _owner?.GetComponent<BossSkillRunner>()?.TrackEffect(effect);
            Destroy(effect, 1.5f);
        }

        Destroy(gameObject);
    }
    /// <summary>
    /// Dissolve 不同于 Resolve，强制清理石头，不会造成伤害，也不会播放破碎特效。
    /// </summary>
    public void Dissolve()
    {
        _isResolved = true;
        GetComponent<SphereCollider>().enabled = false;
        Destroy(gameObject);
    }
}
