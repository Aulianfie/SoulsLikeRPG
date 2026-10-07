using System.Collections.Generic;
using UnityEngine;

public sealed class BossDamageArea : MonoBehaviour
{
    private const float PulseCueHeight = .07f;

    // Serialized fields
    [SerializeField]
    private LayerMask _targetLayers = 1;
    [SerializeField]
    private LayerMask _groundLayers = 1;
    [SerializeField, Min(.1f)]
    private float _waveHeight = .55f;
    [SerializeField]
    private Material _cueMaterial;

    // Runtime state
    private Collider[] _targetOverlaps = new Collider[32];
    private readonly RaycastHit[] _groundHits = new RaycastHit[16];
    private Vector3 _previousSweepPosition;
    private bool _hasPreviousSweep;

    // Public properties
    public int LastDamageCount { get; private set; }

    public void ResetSweep()
    {
        _hasPreviousSweep = false;
        LastDamageCount = 0;
    }

    /// <summary>
    /// 处理圆形砸地攻击
    /// 1. Detect 会检测所有在范围内的目标，满足条件的会受到伤害。
    /// 2. ShowPulse 会在地面上显示一个圆形提示，持续约 0.7 秒后自动销毁。
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    /// <param name="damage"></param>
    /// <param name="hitTargets"></param>
    public void Pulse(Vector3 center, float radius, int damage, HashSet<IDamageable> hitTargets)
    {
        float ground = GroundHeight(center + Vector3.up, center.y);
        center.y = ground;
        Detect(center, radius, damage, hitTargets, true);
        ShowPulse(center, radius);
    }

    /// <summary>
    /// 处理矩形砸地攻击
    /// 1. Detect 会检测所有在范围内的目标，满足条件的会受到伤害。
    /// 2. ShowStomp 会在地面上显示一个矩形提示，持续约 0.7 秒后自动销毁。
    /// </summary>
    /// <param name="footPosition"></param>
    /// <param name="reach"></param>
    /// <param name="side"></param>
    /// <param name="damage"></param>
    /// <param name="hitTargets"></param>
    public void Stomp(Vector3 footPosition, float reach, BossSkillSide side, int damage, HashSet<IDamageable> hitTargets)
    {
        Quaternion orientation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up));
        Vector3 right = orientation * Vector3.right;
        Vector3 forward = orientation * Vector3.forward;
        Vector2 halfSize = new Vector2(reach * .5f, reach);
        float sideOffset = side == BossSkillSide.Left ? -halfSize.x : halfSize.x;

        // 内侧边界对齐 Boss 中线；脚的前后落点决定矩形位置，避免另一侧脚也造成伤害。
        Vector3 center = transform.position + forward * Vector3.Dot(footPosition - transform.position, forward);
        center += right * sideOffset;
        center.y = GroundHeight(footPosition + Vector3.up, footPosition.y);
        Detect(center, reach, damage, hitTargets, true, halfSize, orientation);
        ShowStomp(center, halfSize, orientation);
    }

    /// <summary>
    /// 旋转攻击中的连续扫掠
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    /// <param name="damage"></param>
    /// <param name="hitTargets"></param>
    public void Sweep(Vector3 center, float radius, int damage, HashSet<IDamageable> hitTargets)
    {
        int steps;
        // steps 的计算逻辑：
        // 1. 如果上一次 Sweep 没有记录位置，steps = 1
        // 2. 如果上一次 Sweep 有记录位置，计算当前中心与上一次中心的距离，按每 0.2 米一个步长计算 steps，最多 32 步。
        if (_hasPreviousSweep)
        {
            steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(center, _previousSweepPosition) / .2f), 1, 32);
        }
        else
        {
            steps = 1;
        }

        for (int i = 1; i <= steps; i++)
        {
            Detect(
                _hasPreviousSweep ? Vector3.Lerp(_previousSweepPosition, center, i / (float)steps) : center,
                radius,
                damage,
                hitTargets,
                false
            );
        }

        _previousSweepPosition = center;
        _hasPreviousSweep = true;
    }

    private void Detect(Vector3 center, float radius, int damage, HashSet<IDamageable> hitTargets, bool groundWave,
        Vector2 rectangleHalfSize = default, Quaternion orientation = default)
    {
        Vector3 queryCenter = center + (groundWave ? Vector3.up * .3f : Vector3.zero);
        bool rectangular = rectangleHalfSize.x > 0;
        int count;
        while (true)
        {
            // 分别处理矩形和圆形的 Overlap 检测，使用 OverlapBoxNonAlloc 或 OverlapSphereNonAlloc
            if (rectangular)
            {
                count = Physics.OverlapBoxNonAlloc(
                    queryCenter,
                    new Vector3(rectangleHalfSize.x, radius, rectangleHalfSize.y),
                    _targetOverlaps,
                    orientation,
                    _targetLayers,
                    QueryTriggerInteraction.Ignore
                );
            }
            else
            {
                count = Physics.OverlapSphereNonAlloc(
                    queryCenter,
                    radius,
                    _targetOverlaps,
                    _targetLayers,
                    QueryTriggerInteraction.Ignore
                );
            }
            if (count < _targetOverlaps.Length)
            {
                break;
            }

            // 环境与玩家共用 Default 层；缓冲区满时不能认为玩家不在伤害范围内。
            // 仅在容量不足时扩容，后续检测继续复用同一数组。
            System.Array.Resize(ref _targetOverlaps, _targetOverlaps.Length * 2);
        }

        // 
        for (int i = 0; i < count; i++)
        {
            Collider collider = _targetOverlaps[i];
            if (collider.transform.IsChildOf(transform))
            {
                continue;
            }

            var receiver = collider.GetComponentInParent<IDamageable>();
            if (receiver == null ||
                hitTargets.Contains(receiver))
            {
                continue;
            }

            Vector3 targetPoint = collider.bounds.center;
            if (groundWave)
            {
                Vector3 feet = collider.bounds.center;
                feet.y = collider.bounds.min.y;
                float height = GroundHeight(feet + Vector3.up * .15f, float.NegativeInfinity);
                Vector3 flat = feet - center;
                flat.y = 0;
                bool outsideFootprint;
                if (rectangular)
                {
                    Vector3 local = Quaternion.Inverse(orientation) * flat;
                    outsideFootprint = Mathf.Abs(local.x) > rectangleHalfSize.x ||
                        Mathf.Abs(local.z) > rectangleHalfSize.y;
                }
                else
                {
                    outsideFootprint = flat.sqrMagnitude > radius * radius;
                }

                if (outsideFootprint ||
                    float.IsNegativeInfinity(height) ||
                    Mathf.Abs(height - center.y) > .6f ||
                    feet.y - height > _waveHeight)
                {
                    continue;
                }
            }

            hitTargets.Add(receiver);
            receiver.TakeDamage(new DamageInfo
                {
                    Damage = damage,
                    Attacker = gameObject,
                    HitPoint = targetPoint,
                    HitDirection = (targetPoint - transform.position).normalized
                });
            LastDamageCount++;
        }
    }

    private float GroundHeight(Vector3 start, float fallback)
    {
        int count = Physics.RaycastNonAlloc(start, Vector3.down, _groundHits, 8, _groundLayers, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        float result = fallback;
        for (int i = 0; i < count; i++)
        {
            var hit = _groundHits[i];
            if (hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<PlayerHealth>() != null ||
                hit.normal.y < .6f)
            {
                continue;
            }

            if (hit.distance < closest)
            {
                closest = hit.distance;
                result = hit.point.y;
            }
        }

        return result;
    }

    /// <summary>
    /// 显示地面冲击的圆形提示，持续约 0.7 秒后自动销毁。
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    private void ShowPulse(Vector3 center, float radius)
    {
        LineRenderer line = CreatePulseCue(48);
        if (line == null)
        {
            return;
        }

        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, PulseCueHeight, Mathf.Sin(angle) * radius));
        }
    }

    /// <summary>
    /// 显示地面冲击的矩形提示，持续约 0.7 秒后自动销毁。
    /// </summary>
    /// <param name="center"></param>
    /// <param name="halfSize"></param>
    /// <param name="orientation"></param>
    private void ShowStomp(Vector3 center, Vector2 halfSize, Quaternion orientation)
    {
        LineRenderer line = CreatePulseCue(4);
        if (line == null)
        {
            return;
        }

        line.SetPosition(0, center + orientation * new Vector3(-halfSize.x, PulseCueHeight, -halfSize.y));
        line.SetPosition(1, center + orientation * new Vector3(-halfSize.x, PulseCueHeight, halfSize.y));
        line.SetPosition(2, center + orientation * new Vector3(halfSize.x, PulseCueHeight, halfSize.y));
        line.SetPosition(3, center + orientation * new Vector3(halfSize.x, PulseCueHeight, -halfSize.y));
    }

    /// <summary>
    /// 创建地面冲击提示线
    /// </summary>
    /// <param name="pointCount"></param>
    /// <returns></returns>
    private LineRenderer CreatePulseCue(int pointCount)
    {
        if (_cueMaterial == null)
        {
            return null;
        }

        var cue = new GameObject("GolemGroundPulse", typeof(LineRenderer));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cue, gameObject.scene);
        GetComponent<BossSkillRunner>()?.TrackEffect(cue);
        var line = cue.GetComponent<LineRenderer>();
        line.sharedMaterial = _cueMaterial;
        line.useWorldSpace = true;
        line.loop = true;
        line.widthMultiplier = .1f;
        line.positionCount = pointCount;
        Destroy(cue, .7f);
        return line;
    }
}
