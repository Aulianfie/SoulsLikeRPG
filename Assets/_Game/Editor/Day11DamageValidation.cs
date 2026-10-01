using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using static Day11Validation;

// Tests the actual scene enemy collider and EnemyHealth, rather than a large damage probe.
public static class Day11DamageValidation
{
    private static bool Idle => Player.CurrentState == Player.LocomotionState && Player.Motor.IsGrounded;
    [MenuItem("Tools/SoulsLike RPG/Day11/Validate Rest and Real Enemy Damage")]
    public static void Run() => Begin("DamageFixed");
    public static void Baseline() => Begin("DamageBaseline");

    public static IEnumerator Scenarios()
    {
        bool strict = SessionState.GetString("Day11.Validation.Running.Mode", "") == "DamageFixed";
        var rows = new List<string> { "weapon,action,distance,jumpDelay,hpLoss,damageEvents,overlapTimes,activeOverlapTimes" };
        var poses = new List<string> { "weapon,action,distance,jumpDelay,time,hitActive,overlapsEnemy,bladeX,bladeY,bladeZ,playerY" };
        var mana = Player.GetComponent<PlayerMana>();
        var checkpoint = UnityEngine.Object.FindObjectOfType<CheckpointManager>();
        var site = UnityEngine.Object.FindObjectsOfType<CheckpointSite>().First();
        Player.Health.TakeDamage(new DamageInfo { Damage = 5 });
        yield return new Func<bool>(() => Idle);
        mana.Consume(20); Player.Stamina.Consume(10);
        Check(checkpoint.ActivateCheckpoint(site), "Actual checkpoint rest accepted");
        UnityEngine.Object.FindObjectOfType<ProgressionPresenter>().CloseMenu();
        File.WriteAllText("Logs/Day11/" + (strict ? "fixed" : "baseline") + "_rest.txt", $"HP={Player.Health.CurrentHealth}/{Player.Health.MaxHealth}; MP={mana.CurrentMana}/{mana.MaxMana}; Stamina={Player.Stamina.CurrentStamina}/{Player.Stamina.MaxStamina}\n");
        if (strict)
        {
            Check(mana.CurrentMana == mana.MaxMana, "Checkpoint rest restores all MP");
            var hud = UnityEngine.Object.FindObjectOfType<PlayerHUDView>();
            Check(Get<Image>(hud, "_manaFill").fillAmount == 1, "Checkpoint MP restoration updates HUD");
        }
        foreach (var e in UnityEngine.Object.FindObjectsOfType<EnemyStateMachine>(true)) e.gameObject.SetActive(false);
        var enemy = UnityEngine.Object.FindObjectsOfType<EnemyStateMachine>(true).First();
        enemy.enabled = false;
        foreach (var a in enemy.GetComponentsInChildren<NavMeshAgent>(true)) a.enabled = false;
        enemy.gameObject.SetActive(true);
        var health = enemy.GetComponent<EnemyHealth>();
        var colliders = enemy.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
        Check(colliders.Length > 0 && colliders.Any(c => c.gameObject.layer == 3), "Real scene enemy collision layer and body colliders available");
        foreach (var c in colliders) Physics.IgnoreCollision(Player.GetComponent<CharacterController>(), c);
        var damageSamples = new Dictionary<string, List<int>>();
        for (int slot = 0; slot < 2; slot++)
        {
            Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
            yield return new Func<bool>(() => Idle);
            if (Player.Equipment.CurrentSlotIndex != slot)
            {
                Check(Player.Equipment.EquipSlot(slot), "Real enemy test equips weapon: " + slot);
                yield return new Func<bool>(() => Idle && Player.Equipment.CurrentSlotIndex == slot);
            }
            foreach (var type in new[] { PlayerAttackType.Jump, PlayerAttackType.WeaponSkill })
            {
                foreach (var c in colliders) Physics.IgnoreCollision(Player.GetComponent<CharacterController>(), c, type == PlayerAttackType.Jump);
                string key = slot + "/" + type;
                damageSamples[key] = new List<int>();
                foreach (float distance in new[] { .8f, 1.2f, 1.6f })
                foreach (float delay in type == PlayerAttackType.Jump ? new[] { .08f, .25f, .45f } : new[] { 0f })
                {
                    Player.Targeting.ClearTarget();
                    Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
                    enemy.transform.SetPositionAndRotation(new Vector3(1000, 0, 1000 + distance), Quaternion.Euler(0, 180, 0));
                    health.RestoreFull(); Player.Stamina.RestoreFull(); mana.Restore(mana.MaxMana);
                    Physics.SyncTransforms();
                    yield return new Func<bool>(() => Idle);
                    if (type == PlayerAttackType.Jump)
                    {
                        Set(Player.InputReader, "_jumpRequested", true);
                        yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState && !Player.Motor.IsGrounded);
                        yield return delay;
                    }
                    int before = health.CurrentHealth, events = 0;
                    Action<int, int> observe = (current, max) => { if (current < before) events++; };
                    health.HealthChanged += observe;
                    bool started = Player.TryBeginAttack(type);
                    var overlaps = new List<float>(); var activeOverlaps = new List<float>();
                    var shape = Get<BoxCollider>(Player.Equipment.CurrentHitbox, "_shape");
                    while (Player.CurrentState == Player.AttackState)
                    {
                        if (Player.PlayerAnimator.TryGetAttackNormalizedTime(out float t))
                        {
                            Vector3 center = shape.transform.TransformPoint(shape.center);
                            Vector3 scale = shape.transform.lossyScale;
                            Vector3 ext = Vector3.Scale(shape.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                            bool overlap = Physics.OverlapBox(center, ext, shape.transform.rotation, 1 << 3, QueryTriggerInteraction.Ignore).Any(c => c.GetComponentInParent<EnemyHealth>() == health);
                            bool active = Get<bool>(Player.Equipment.CurrentHitbox, "_isActive");
                            if (overlap) { overlaps.Add(t); if (active) activeOverlaps.Add(t); }
                            poses.Add(string.Join(",", slot, type, distance.ToString(CultureInfo.InvariantCulture), delay.ToString(CultureInfo.InvariantCulture), t.ToString("F3", CultureInfo.InvariantCulture), active, overlap, (center.x-1000).ToString("F3", CultureInfo.InvariantCulture), center.y.ToString("F3", CultureInfo.InvariantCulture), (center.z-1000).ToString("F3", CultureInfo.InvariantCulture), Player.transform.position.y.ToString("F3", CultureInfo.InvariantCulture)));
                        }
                        yield return null;
                    }
                    yield return new Func<bool>(() => Idle);
                    health.HealthChanged -= observe;
                    int loss = before - health.CurrentHealth;
                    damageSamples[key].Add(loss);
                    string overlapRange = overlaps.Count == 0 ? "none" : overlaps.Min().ToString("F3", CultureInfo.InvariantCulture) + "-" + overlaps.Max().ToString("F3", CultureInfo.InvariantCulture);
                    string activeRange = activeOverlaps.Count == 0 ? "none" : activeOverlaps.Min().ToString("F3", CultureInfo.InvariantCulture) + "-" + activeOverlaps.Max().ToString("F3", CultureInfo.InvariantCulture);
                    rows.Add($"{slot},{type},{distance.ToString(CultureInfo.InvariantCulture)},{delay.ToString(CultureInfo.InvariantCulture)},{loss},{events},{overlapRange},{activeRange}");
                    File.WriteAllLines("Logs/Day11/" + (strict ? "fixed" : "baseline") + "_damage.csv", rows);
                    File.WriteAllLines("Logs/Day11/" + (strict ? "fixed" : "baseline") + "_blade_poses.csv", poses);
                    Check(started, "Real enemy attack starts: " + key + " distance=" + distance + " delay=" + delay);
                    if (strict && distance == .8f)
                    {
                        var data = Player.Combat.GetAttack(type);
                        int expected = Mathf.RoundToInt(Player.GetComponent<PlayerProgression>().CalculateAttackDamage(data.Damage) * Player.Equipment.CurrentWeapon.DamageMultiplier);
                        Check(loss == Mathf.Min(before, expected) && events == 1, "Near real enemy takes configured damage once: " + key + " delay=" + delay + " HP loss=" + loss);
                    }
                }
                if (strict) Check(damageSamples[key].Any(x => x > 0), "Real enemy hit confirmed: " + key);
            }
        }
        enemy.gameObject.SetActive(false);
    }
}
