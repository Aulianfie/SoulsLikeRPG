#if UNITY_EDITOR
using UnityEngine;

// Created only by the editor validation suite; never added to a saved scene.
public sealed class Day11ValidationTarget : MonoBehaviour, IDamageable
{
    public int HitCount { get; private set; }
    public int TotalDamage { get; private set; }
    public void ResetHits() { HitCount = 0; TotalDamage = 0; }
    public void TakeDamage(DamageInfo damage) { HitCount++; TotalDamage += damage.Damage; }
}
#endif
