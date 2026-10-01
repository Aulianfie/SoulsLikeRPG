using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static Day11Validation;

public static class Day11FeelValidation
{
    private static bool Idle => Player.CurrentState == Player.LocomotionState && Player.Motor.IsGrounded;
    [MenuItem("Tools/SoulsLike RPG/Day11/Validate Jump Aim and Skill Motion")]
    public static void Run() => Begin("Feel");
    public static IEnumerator Scenarios()
    {
        var mana = Player.GetComponent<PlayerMana>();
        foreach (var enemy in UnityEngine.Object.FindObjectsOfType<EnemyStateMachine>(true)) enemy.gameObject.SetActive(false);
        for (int slot = 0; slot < 2; slot++)
        {
            Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
            yield return new Func<bool>(() => Idle);
            if (Player.Equipment.CurrentSlotIndex != slot)
            {
                Check(Player.Equipment.EquipSlot(slot), "Motion test equips weapon: " + slot);
                yield return new Func<bool>(() => Idle && Player.Equipment.CurrentSlotIndex == slot);
            }
            var skill = Player.Equipment.CurrentWeapon.Moveset.WeaponSkill;
            Check(skill.MoveDistance > 0 && skill.MotionStart < skill.MotionEnd, "Editable skill distance/window configured: " + slot);
            mana.RestoreFull(); Vector3 start = Player.transform.position;
            Check(Player.TryBeginAttack(PlayerAttackType.WeaponSkill), "Free skill starts: " + slot);
            yield return new Func<bool>(() => Idle);
            Vector3 delta = Player.transform.position - start;
            Check(Mathf.Abs(delta.z - skill.MoveDistance) < .025f && Mathf.Abs(delta.x) < .01f, "Free skill advances configured distance: " + slot + " actual=" + delta.z);
            Check(Player.Motor.HorizontalSpeed == 0, "Skill leaves no residual horizontal speed: " + slot);
            Vector3 end = Player.transform.position; yield return .15f;
            Check(Vector3.Distance(end, Player.transform.position) < .025f, "No slide after skill recovery: " + slot);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Day11_MotionWall"; wall.transform.position = new Vector3(1000, 1.5f, 1000.7f);
            wall.transform.localScale = new Vector3(3, 3, .1f); Physics.SyncTransforms();
            Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
            yield return new Func<bool>(() => Idle); mana.RestoreFull(); start = Player.transform.position;
            Check(Player.TryBeginAttack(PlayerAttackType.WeaponSkill), "Wall skill starts: " + slot);
            yield return new Func<bool>(() => Idle);
            float actual = Player.transform.position.z - start.z;
            Check(actual > .05f && actual < .4f && actual < skill.MoveDistance, "Wall blocks skill displacement: " + slot + " actual=" + actual);
            UnityEngine.Object.Destroy(wall); yield return .05f;

            Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
            yield return new Func<bool>(() => Idle); mana.RestoreFull();
            Check(Player.TryBeginAttack(PlayerAttackType.WeaponSkill), "Interrupted motion skill starts: " + slot);
            yield return new Func<bool>(() => Player.transform.position.z > 1000.03f);
            Player.Health.TakeDamage(new DamageInfo { Damage = 1 }); start = Player.transform.position;
            Check(Player.CurrentState == Player.HurtState && Player.Motor.HorizontalSpeed == 0, "Hurt immediately stops skill movement: " + slot);
            yield return .15f;
            Check(Mathf.Abs(Player.transform.position.z - start.z) < .02f, "Hurt has no remaining skill displacement: " + slot);
            yield return new Func<bool>(() => Idle); Player.Health.RestoreFull();
        }

        Check(Player.Equipment.EquipSlot(0), "Select long sword for jump phase test");
        yield return new Func<bool>(() => Idle && Player.Equipment.CurrentSlotIndex == 0);
        var enemyActor = UnityEngine.Object.FindObjectsOfType<EnemyStateMachine>(true).First();
        enemyActor.enabled = false;
        foreach (var agent in enemyActor.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
        enemyActor.gameObject.SetActive(true);
        enemyActor.transform.SetPositionAndRotation(new Vector3(1000, 0, 1000.8f), Quaternion.Euler(0, 180, 0));
        var target = enemyActor.GetComponent<Targetable>(); var health = enemyActor.GetComponent<EnemyHealth>(); health.RestoreFull();
        Physics.SyncTransforms();
        foreach (var collider in enemyActor.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(Player.GetComponent<CharacterController>(), collider);
        Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
        yield return new Func<bool>(() => Idle);
        Player.Stamina.RestoreFull(); Set(Player.InputReader, "_jumpRequested", true);
        yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState && !Player.Motor.IsGrounded);
        Check(Player.TryBeginAttack(PlayerAttackType.Jump), "Long jump phase starts");
        yield return new Func<bool>(() => Get<bool>(Player.AttackState, "_jumpHolding"));
        int hp = health.CurrentHealth; float previousY = Player.transform.position.y;
        yield return .05f;
        Check(Get<bool>(Player.AttackState, "_jumpHolding") && !Get<bool>(Player.Equipment.CurrentHitbox, "_isActive") && health.CurrentHealth == hp,
            "Long windup above enemy head does not damage enemy");
        Check(Mathf.Abs(Player.transform.position.y - previousY) > .01f, "Gravity continues while long jump windup pose waits");
        yield return new Func<bool>(() => Idle);
        Check(health.CurrentHealth < hp && Day11Validation.Animator.GetFloat("JumpAttackSpeed") == 1f, "Near-ground long strike damages enemy and resets playback speed");

        // Existing target beyond the old 60 degree cone; track through windup, freeze once committed.
        enemyActor.transform.position = new Vector3(1000, 0, 1002);
        health.RestoreFull(); Physics.SyncTransforms();
        Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.Euler(0, -85, 0));
        yield return new Func<bool>(() => Idle);
        Player.Targeting.ClearTarget();
        typeof(PlayerTargeting).GetMethod("LockTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Player.Targeting, new object[] { target });
        // Keep target on screen with a fixture camera so acquisition/camera motion is independent of aim.
        var cameraObject = new GameObject("Day11_TargetingFixtureCamera"); var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(1000, 2, 995); camera.transform.LookAt(target.LockPoint);
        var originalCamera = Get<Camera>(Player.Targeting, "_camera"); Set(Player.Targeting, "_camera", camera);
        Player.Stamina.RestoreFull(); Set(Player.InputReader, "_jumpRequested", true);
        yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState && !Player.Motor.IsGrounded);
        Check(Player.TryBeginAttack(PlayerAttackType.Jump), "Locked jump starts outside old assist cone");
        yield return .18f;
        float angle = Vector3.Angle(Player.transform.forward, Vector3.ProjectOnPlane(target.LockPoint.position - Player.transform.position, Vector3.up));
        Check(Player.Targeting.CurrentTarget == target && angle < 20f, "Jump continuously turns toward locked target during windup: angle=" + angle);
        camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
        yield return .12f;
        Check(Player.Targeting.CurrentTarget == target, "Brief airborne offscreen movement preserves existing lock");
        camera.transform.LookAt(target.LockPoint);
        yield return new Func<bool>(() => Get<bool>(Player.AttackState, "_jumpStrikeCommitted"));
        Quaternion committed = Player.transform.rotation;
        enemyActor.transform.position += Vector3.right * 1.2f; camera.transform.LookAt(target.LockPoint); Physics.SyncTransforms();
        yield return .08f;
        Check(Quaternion.Angle(Player.transform.rotation, committed) < 1f, "Jump strike stops steering after commitment");
        yield return new Func<bool>(() => Idle);
        Player.Targeting.ClearTarget(); Set(Player.Targeting, "_camera", originalCamera); UnityEngine.Object.Destroy(cameraObject);
        enemyActor.gameObject.SetActive(false);
    }
}
