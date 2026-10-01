using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.AI;
using System.Reflection;
using static Day11Validation;

public static class Day11ActionValidation
{
    private static PlayerMana Mana => Player.GetComponent<PlayerMana>();
    private static bool Idle => Player.CurrentState == Player.LocomotionState && Player.Motor.IsGrounded;
    private static bool HitOpen => Get<bool>(Player.Equipment.CurrentHitbox, "_isActive");

    [MenuItem("Tools/SoulsLike RPG/Day11/3 Validate All Actions (Play Mode)")]
    public static void Run() { Day11Setup.ConfigureActions(); Begin("Actions"); }

    public static IEnumerator Scenarios()
    {
        var targetObject = new GameObject("Day11_ValidationTarget");
        targetObject.layer = LayerMask.NameToLayer("Enemy");
        var box = targetObject.AddComponent<BoxCollider>();
        box.size = new Vector3(12, 8, 12);
        var target = targetObject.AddComponent<Day11ValidationTarget>();
        Physics.IgnoreCollision(Player.GetComponent<CharacterController>(), box);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var originalBackground = InputSystem.settings.backgroundBehavior;
        var originalUpdateMode = InputSystem.settings.updateMode;
        // Batch mode has no focused Game View. Only the test fixture changes routing.
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        Player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        try
        {
            Check(!Day11Validation.Animator.applyRootMotion, "Root motion disabled: code owns jump displacement");
            Check(Day11Validation.Animator.layerCount == 3 && Day11Validation.Animator.GetLayerName(1) == "ItemUse" && Day11Validation.Animator.GetLayerName(2) == "WeaponSwitch", "ItemUse and WeaponSwitch layers preserved");
            for (int slot = 0; slot < 2; slot++)
            {
                if (Player.Equipment.CurrentSlotIndex != slot)
                {
                    Check(Player.Equipment.EquipSlot(slot), "Action weapon switch starts: " + slot);
                    yield return new Func<bool>(() => Idle && Player.Equipment.CurrentSlotIndex == slot);
                }
                Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
                yield return new Func<bool>(() => Idle);
                targetObject.transform.position = Player.transform.position + Vector3.up * 2;
                Physics.SyncTransforms();
                WeaponData weapon = Player.Equipment.CurrentWeapon;
                var weaponView = UnityEngine.Object.FindObjectOfType<WeaponSlotView>();
                Check(weaponView != null && Get<Image>(weaponView, "_icon").sprite == weapon.Icon, "Weapon HUD icon follows equipped weapon: " + slot);
                AttackData jump = weapon.Moveset.JumpAttack, skill = weapon.Moveset.WeaponSkill;
                Check(jump != null && skill != null && weapon.Moveset.LightCombo == weapon.LightAttackCombo, "All three moveset actions wired: " + slot);
                Check(skill.ManaCost == (slot == 0 ? 20 : 35), "Configured MP cost: " + slot);
                Check(!Player.TryBeginAttack(PlayerAttackType.Jump), "Grounded Jump attack rejected: " + slot);
                Player.Stamina.Consume(Player.Stamina.CurrentStamina);
                Set(Player.InputReader, "_jumpRequested", true);
                yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState && !Player.Motor.IsGrounded);
                Check(!Player.TryBeginAttack(PlayerAttackType.Jump) && Player.CurrentState == Player.AirborneState && !Player.HasUsedJumpAttack, "Insufficient stamina jump rejected without using flight allowance: " + slot);
                yield return new Func<bool>(() => Idle);

                // Real CharacterController flight, buffered LMB, animated weapon overlap damage.
                Player.Stamina.RestoreFull(); Mana.Restore(Mana.MaxMana); target.ResetHits();
                Set(Player.Motor, "_horizontalVelocity", Vector3.forward * 2);
                Set(Player.InputReader, "_jumpRequested", true);
                yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState && !Player.Motor.IsGrounded);
                float speed = Player.Motor.HorizontalSpeed, vertical = Player.Motor.VerticalVelocity;
                Vector3 takeoff = Player.transform.position;
                float stamina = Player.Stamina.CurrentStamina, mana = Mana.CurrentMana;
                Check(!Player.TryBeginAttack(PlayerAttackType.WeaponSkill) && Mana.CurrentMana == mana, "Airborne skill rejected without charging: " + slot);
                PulseLight();
                yield return new Func<bool>(() => Player.CurrentState == Player.AttackState);
                Check(Player.Combat.CurrentAttackType == PlayerAttackType.Jump, "Airborne LMB selects Jump attack: " + slot);
                Check(Mathf.Abs(Player.Stamina.CurrentStamina - (stamina - jump.StaminaCost * weapon.StaminaMultiplier)) < .1f, "Jump stamina charged once: " + slot);
                Check(Mana.CurrentMana == mana, "Jump does not charge MP: " + slot);
                Check(Mathf.Abs(Player.Motor.HorizontalSpeed - speed) < .01f && speed > .1f, "Jump retains takeoff horizontal speed: " + slot);
                Check(Player.HasUsedJumpAttack && !Player.TryBeginAttack(PlayerAttackType.Jump), "Repeated jump startup rejected in one flight: " + slot);
                yield return .12f;
                Check(Player.Motor.VerticalVelocity < vertical && Vector3.Distance(Player.transform.position, takeoff) > .1f, "Jump gravity and displacement continue: " + slot);
                yield return new Func<bool>(() => HitOpen);
                Check(Day11Validation.Animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == (slot == 0 ? "Jump_LongSword" : "Jump_GreatSword")), "Correct jump override playing: " + slot);
                yield return new Func<bool>(() => target.HitCount > 0);
                yield return new Func<bool>(() => Idle);
                Check(target.HitCount == 1 && target.TotalDamage > 0, "Jump overlap damages each target once: " + slot);
                Check(!HitOpen && !Player.Combat.IsAttacking, "Jump lands, completes recovery and closes hitbox: " + slot);
                yield return .03f;
                Check(!Player.HasUsedJumpAttack, "Landing resets one-flight jump flag: " + slot);

                // Prove the actual Q binding enters the shared attack state and updates existing MP.
                targetObject.transform.position = Player.transform.position + Vector3.up * 2;
                Physics.SyncTransforms(); target.ResetHits(); Mana.Restore(Mana.MaxMana);
                mana = Mana.CurrentMana; int manaEvents = 0;
                Action<float, float> observeMana = (current, max) => manaEvents++;
                Mana.ManaChanged += observeMana;
                Check(Get<InputAction>(Player.InputReader, "_weaponSkillAction").enabled, "Skill input action enabled: " + slot);
                var skillAction = Get<InputAction>(Player.InputReader, "_weaponSkillAction");
                Debug.Log("[Day11 Input] Controls=" + string.Join(",", skillAction.controls.Select(c => c.path)) + "; pairedKeyboard=" + keyboard.deviceId + "; cachedSameAsset=" + (skillAction.actionMap.asset == Player.GetComponent<PlayerInput>().actions));
                PumpInput();
                InputState.Change(keyboard, new KeyboardState(Key.Q), InputUpdateType.Manual);
                Debug.Log("[Day11 Input] QState=" + keyboard.qKey.isPressed + "; request=" + Get<bool>(Player.InputReader, "_weaponSkillRequested") + "; update=" + InputState.currentUpdateType);
                Check(Get<bool>(Player.InputReader, "_weaponSkillRequested"), "Q binding invokes gameplay input callback: " + slot);
                yield return new Func<bool>(() => Player.CurrentState == Player.AttackState);
                PumpInput();
                InputState.Change(keyboard, new KeyboardState(), InputUpdateType.Manual);
                Check(Player.Combat.CurrentAttackType == PlayerAttackType.WeaponSkill, "Actual keyboard Q starts WeaponSkill: " + slot);
                Check(Mana.CurrentMana == mana - skill.ManaCost && manaEvents == 1, "Skill charges MP and emits HUD event once: " + slot);
                var hud = UnityEngine.Object.FindObjectOfType<PlayerHUDView>();
                Check(hud != null && Mathf.Abs(Get<Image>(hud, "_manaFill").fillAmount - Mana.CurrentMana / Mana.MaxMana) < .001f, "Existing MP HUD reflects skill charge: " + slot);
                int originalSlot = Player.Equipment.CurrentSlotIndex;
                PulseLight(); PulseDodge(); PulseSkill(); PulseItemAndSwitch();
                yield return .10f;
                Check(Player.CurrentState == Player.AttackState && Player.Combat.CurrentAttackType == PlayerAttackType.WeaponSkill, "Skill rejects light, repeat, dodge, item and switch requests: " + slot);
                Check(Player.Equipment.CurrentSlotIndex == originalSlot && !Player.Combat.AttackQueued, "Skill does not switch weapon or queue combo: " + slot);
                Check(!Player.TryBeginAttack(PlayerAttackType.WeaponSkill) && manaEvents == 1, "Repeated skill cannot charge again: " + slot);
                yield return new Func<bool>(() => HitOpen);
                Check(Day11Validation.Animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == (slot == 0 ? "Skill_LongSword" : "Skill_GreatSword")), "Correct skill override playing: " + slot);
                yield return new Func<bool>(() => target.HitCount > 0);
                yield return new Func<bool>(() => Idle);
                yield return .10f;
                Check(Idle && !HitOpen && manaEvents == 1 && Mana.CurrentMana == mana - skill.ManaCost, "Skill completes without buffered repeat or extra charge: " + slot);
                Mana.ManaChanged -= observeMana;
                Check(target.HitCount == 1 && target.TotalDamage > 0, "Skill overlap damages each target once: " + slot);
                Mana.Consume(Mana.CurrentMana); mana = Mana.CurrentMana;
                PulseSkill(); yield return .10f;
                Check(Idle && Mana.CurrentMana == mana && !HitOpen, "Insufficient MP skill rejected without animation/hitbox: " + slot);
                Mana.Restore(Mana.MaxMana);

                // Failed setup uses runtime copies; asset definitions remain untouched.
                var badWeapon = UnityEngine.Object.Instantiate(weapon);
                var badMoveset = UnityEngine.Object.Instantiate(weapon.Moveset);
                var badSkill = UnityEngine.Object.Instantiate(skill);
                Set(badSkill, "_animationStateName", "Day11MissingAnimation");
                Set(badMoveset, "_weaponSkill", badSkill); Set(badWeapon, "_moveset", badMoveset);
                Player.Combat.SetWeapon(badWeapon, Player.Equipment.CurrentHitbox); mana = Mana.CurrentMana;
                Check(!Player.TryBeginAttack(PlayerAttackType.WeaponSkill) && Idle && Mana.CurrentMana == mana && !HitOpen, "Missing Animator state fails before charging MP: " + slot);
                Set(badMoveset, "_weaponSkill", null);
                Check(!Player.TryBeginAttack(PlayerAttackType.WeaponSkill) && Mana.CurrentMana == mana, "Missing skill definition fails before charging MP: " + slot);
                Player.Combat.SetWeapon(weapon, Player.Equipment.CurrentHitbox);
                UnityEngine.Object.Destroy(badSkill); UnityEngine.Object.Destroy(badMoveset); UnityEngine.Object.Destroy(badWeapon);

                Player.Stamina.RestoreFull();
                PulseLight(); yield return new Func<bool>(() => Player.CurrentState == Player.AttackState);
                Check(Player.Combat.CurrentAttackType == PlayerAttackType.Light && Player.Combat.ComboIndex == 0, "Skill followed by light returns to combo segment zero: " + slot);
                yield return new Func<bool>(() => Idle);
                Mana.Restore(Mana.MaxMana);
                Check(Player.TryBeginAttack(PlayerAttackType.WeaponSkill), "Skill begins for Hurt interruption: " + slot);
                yield return new Func<bool>(() => HitOpen);
                Player.Health.TakeDamage(new DamageInfo { Damage = 1 });
                Check(Player.CurrentState == Player.HurtState && !HitOpen && !Player.Combat.IsAttacking, "Hurt interrupts skill and closes hitbox immediately: " + slot);
                Check(!Player.TryBeginAttack(PlayerAttackType.WeaponSkill), "Hurt cannot start skill: " + slot);
                yield return new Func<bool>(() => Idle);
                Player.Health.RestoreFull();
                Time.timeScale = 0; mana = Mana.CurrentMana;
                Check(!Player.TryBeginAttack(PlayerAttackType.WeaponSkill) && Mana.CurrentMana == mana, "Paused startup cannot spend MP: " + slot);
                Time.timeScale = 1;
            }

            // One-flight guard remains even when Hurt interrupts the attack above ground.
            box.enabled = false;
            Player.Motor.Teleport(new Vector3(1000, 30, 1000), Quaternion.identity);
            Player.ChangeState(Player.AirborneState);
            yield return .08f; Player.Stamina.RestoreFull();
            Check(Player.TryBeginAttack(PlayerAttackType.Jump), "Falling from a ledge can start Jump attack");
            Player.Health.TakeDamage(new DamageInfo { Damage = 1 });
            Check(!HitOpen && Player.CurrentState == Player.HurtState && Player.HasUsedJumpAttack, "Hurt interrupts airborne jump but preserves one-flight guard");
            yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState);
            Check(!Player.TryBeginAttack(PlayerAttackType.Jump), "Second jump after airborne Hurt rejected before landing");
            yield return new Func<bool>(() => Idle);
            yield return .03f;
            Player.Stamina.RestoreFull(); Set(Player.InputReader, "_jumpRequested", true);
            yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState && !Player.Motor.IsGrounded);
            Check(Player.TryBeginAttack(PlayerAttackType.Jump), "New takeoff allows Jump attack again");
            yield return new Func<bool>(() => Idle);

            Player.Stamina.RestoreFull(); PulseDodge(); PulseLight(); PulseSkill(); PulseItemAndSwitch();
            yield return new Func<bool>(() => Player.CurrentState != Player.LocomotionState);
            Check(Player.CurrentState == Player.DodgeState, "Simultaneous inputs: Dodge wins over attack/item/switch");
            yield return new Func<bool>(() => Idle);
            Player.Stamina.RestoreFull(); PulseLight(); PulseSkill(); PulseItemAndSwitch();
            yield return new Func<bool>(() => Player.CurrentState != Player.LocomotionState);
            Check(Player.CurrentState == Player.AttackState && Player.Combat.CurrentAttackType == PlayerAttackType.Light, "Simultaneous inputs: Light wins over skill/item/switch");
            yield return new Func<bool>(() => Idle);
            Player.Health.TakeDamage(new DamageInfo { Damage = 10 });
            yield return new Func<bool>(() => Idle);
            Player.Items.EquipItemSlot(0);
            var flask = Player.GetComponent<PlayerHealingFlask>(); flask.Refill();
            int charges = flask.CurrentCharges, hp = Player.Health.CurrentHealth;
            Check(Player.Items.TryUseItem() && Player.CurrentState == Player.UseItemState, "HP Flask still enters generic UseItem state after actions");
            float healMana = Mana.CurrentMana;
            Check(!Player.TryBeginAttack(PlayerAttackType.WeaponSkill) && Mana.CurrentMana == healMana, "Healing cannot start or charge WeaponSkill");
            yield return new Func<bool>(() => Idle);
            Check(flask.CurrentCharges == charges - 1 && Player.Health.CurrentHealth > hp && Day11Validation.Animator.GetLayerWeight(1) == 0, "Flask consumes one charge, heals and releases ItemUse layer");
            Mana.Restore(Mana.MaxMana); PulseSkill(); PulseItemAndSwitch();
            yield return new Func<bool>(() => Player.CurrentState != Player.LocomotionState);
            Check(Player.CurrentState == Player.AttackState && Player.Combat.CurrentAttackType == PlayerAttackType.WeaponSkill, "Simultaneous inputs: Skill wins over item/switch");
            yield return new Func<bool>(() => HitOpen);
            Player.Health.TakeDamage(new DamageInfo { Damage = Player.Health.CurrentHealth + 1 });
            Check(Player.CurrentState == Player.DeadState && !HitOpen && !Player.Combat.IsAttacking, "Death interrupts skill and closes hitbox immediately");
            Check(!Player.TryBeginAttack(PlayerAttackType.WeaponSkill), "Dead player cannot start skill");
            // Use the actual death -> fade -> checkpoint respawn controller.
            yield return new Func<bool>(() => !Player.Health.IsDead && Player.InputReader.isActiveAndEnabled && Player.CurrentState == Player.LocomotionState);
            Check(Player.Health.CurrentHealth == Player.Health.MaxHealth && !Player.Combat.IsAttacking && !HitOpen && !Player.HasUsedJumpAttack, "Actual respawn restores health and clears action/flight state");
            Check(flask.CurrentCharges == flask.MaxCharges && Player.Stamina.CurrentStamina == Player.Stamina.MaxStamina, "Actual respawn refills Flask and stamina");
            Check(Mana.CurrentMana == Mana.MaxMana, "Actual respawn restores full MP");
            var checkpoint = UnityEngine.Object.FindObjectOfType<CheckpointManager>();
            Check(checkpoint.SaveCurrentProgression() && SaveService.Load() != null, "Checkpoint save remains valid after actions and respawn");
            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<EnemyStateMachine>(true)) enemy.gameObject.SetActive(false);
            Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
            yield return new Func<bool>(() => Idle);
            yield return .8f;
            var lockEnemy = UnityEngine.Object.FindObjectsOfType<EnemyStateMachine>(true).First();
            lockEnemy.enabled = false;
            foreach (var agent in lockEnemy.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            lockEnemy.gameObject.SetActive(true);
            var lockTarget = lockEnemy.GetComponent<Targetable>();
            Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector3 desiredPoint = Player.transform.position + forward * 3 + Vector3.up;
            lockEnemy.transform.position += desiredPoint - lockTarget.LockPoint.position;
            Physics.SyncTransforms();
            typeof(PlayerTargeting).GetMethod("HandleLockOnPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Player.Targeting, null);
            Check(Player.Targeting.CurrentTarget == lockTarget, "Existing lock-on acquires real enemy after actions and respawn");
            Vector3 direction = Vector3.ProjectOnPlane(lockTarget.LockPoint.position - Player.transform.position, Vector3.up);
            Player.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0, 20, 0) * direction);
            float angle = Vector3.Angle(Player.transform.forward, direction);
            Mana.Restore(Mana.MaxMana);
            Check(Player.TryBeginAttack(PlayerAttackType.WeaponSkill), "WeaponSkill starts while locked on");
            yield return .10f;
            Check(Player.Targeting.CurrentTarget == lockTarget && Vector3.Angle(Player.transform.forward, direction) < angle, "Skill preserves lock-on and uses existing rotation assist");
            yield return new Func<bool>(() => Idle);
            Player.Targeting.ClearTarget(); lockEnemy.gameObject.SetActive(false);
        }
        finally
        {
            Time.timeScale = 1;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.updateMode = originalUpdateMode;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            UnityEngine.Object.Destroy(targetObject);
        }
    }

    private static void PulseSkill() { Set(Player.InputReader, "_weaponSkillRequested", true); Set(Player.InputReader, "_weaponSkillRequestFrame", Time.frameCount); }
    private static void PumpInput()
    {
        // InputSystem.Update() chooses Editor updates when batch mode lacks Game View focus.
        // Explicit Manual player updates still exercise the real device/binding/callback path.
        typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Manual });
    }
    private static void PulseDodge() { Set(Player.InputReader, "_dodgeRequested", true); Set(Player.InputReader, "_dodgeExpireTime", Time.time + .15f); }
    private static void PulseItemAndSwitch()
    {
        Set(Player.InputReader, "_useItemRequested", true); Set(Player.InputReader, "_useItemRequestFrame", Time.frameCount);
        Set(Player.InputReader, "_switchWeaponDirection", 1); Set(Player.InputReader, "_switchWeaponRequestFrame", Time.frameCount);
    }
}
