using System.Collections.Generic;
using UnityEngine;

public sealed class BossDamageArea : MonoBehaviour
{
    [SerializeField] LayerMask _targetLayers = 1;
    [SerializeField] LayerMask _groundLayers = 1;
    [SerializeField, Min(.1f)] float _waveHeight = .55f;
    [SerializeField] Material _cueMaterial;
    readonly Collider[] overlaps = new Collider[32];
    readonly RaycastHit[] groundHits = new RaycastHit[16];
    Vector3 previousSweep;
    bool haveSweep;
    public int LastDamageCount { get; private set; }
    public void ResetSweep() { haveSweep = false; LastDamageCount = 0; }

    public void Pulse(Vector3 center, float radius, int damage, HashSet<IDamageable> hitTargets)
    {
        float ground = GroundHeight(center + Vector3.up, center.y);
        center.y = ground;
        Detect(center, radius, damage, hitTargets, true);
        ShowPulse(center, radius);
    }

    public void Sweep(Vector3 center, float radius, int damage, HashSet<IDamageable> hitTargets)
    {
        int steps = haveSweep ? Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(center, previousSweep) / .2f), 1, 32) : 1;
        for (int i = 1; i <= steps; i++) Detect(haveSweep ? Vector3.Lerp(previousSweep, center, i / (float)steps) : center, radius, damage, hitTargets, false);
        previousSweep = center; haveSweep = true;
    }

    void Detect(Vector3 center, float radius, int damage, HashSet<IDamageable> hitTargets, bool groundWave)
    {
        int count = Physics.OverlapSphereNonAlloc(center + (groundWave ? Vector3.up * .3f : Vector3.zero), radius, overlaps, _targetLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlaps[i];
            if (collider.transform.IsChildOf(transform)) continue;
            var receiver = collider.GetComponentInParent<IDamageable>();
            if (receiver == null || hitTargets.Contains(receiver)) continue;
            Vector3 targetPoint = collider.bounds.center;
            if (groundWave)
            {
                Vector3 feet = collider.bounds.center; feet.y = collider.bounds.min.y;
                float height = GroundHeight(feet + Vector3.up * .15f, float.NegativeInfinity);
                Vector3 flat = feet - center; flat.y = 0;
                if (flat.sqrMagnitude > radius * radius || float.IsNegativeInfinity(height) || Mathf.Abs(height - center.y) > .6f || feet.y - height > _waveHeight) continue;
            }
            hitTargets.Add(receiver);
            receiver.TakeDamage(new DamageInfo { Damage = damage, Attacker = gameObject, HitPoint = targetPoint, HitDirection = (targetPoint - transform.position).normalized });
            LastDamageCount++;
        }
    }

    float GroundHeight(Vector3 start, float fallback)
    {
        int count = Physics.RaycastNonAlloc(start, Vector3.down, groundHits, 8, _groundLayers, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity; float result = fallback;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PlayerHealth>() != null || hit.normal.y < .6f) continue;
            if (hit.distance < closest) { closest = hit.distance; result = hit.point.y; }
        }
        return result;
    }

    void ShowPulse(Vector3 center, float radius)
    {
        if (_cueMaterial == null) return;
        var cue = new GameObject("GolemGroundPulse", typeof(LineRenderer));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cue, gameObject.scene);
        GetComponent<BossSkillRunner>()?.TrackEffect(cue);
        var line = cue.GetComponent<LineRenderer>(); line.sharedMaterial = _cueMaterial; line.useWorldSpace = true; line.loop = true; line.widthMultiplier = .1f; line.positionCount = 48;
        for (int i = 0; i < 48; i++) { float angle = i * Mathf.PI * 2 / 48; line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, .07f, Mathf.Sin(angle) * radius)); }
        Destroy(cue, .7f);
    }
}
