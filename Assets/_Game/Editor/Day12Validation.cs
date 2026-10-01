using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static Day11Validation;

// 复用 Day11 实播驱动与原始存档字节备份/恢复，避免第二套测试生命周期。
public static class Day12Validation
{
    private static PlayerMana Mana => Player.GetComponent<PlayerMana>();
    private static bool Idle => Player.CurrentState == Player.LocomotionState && Player.Motor.IsGrounded;
    private static Image Icon => Object.FindObjectOfType<QuickItemPresenter>().transform.Find("FlaskIcon").GetComponent<Image>();
    private static TMP_Text Count => Object.FindObjectOfType<QuickItemPresenter>().transform.Find("Count").GetComponent<TMP_Text>();

    [MenuItem("Tools/SoulsLike RPG/Day12/2 Validate Consumables (Play Mode)")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("需要已保存的 Edit Mode 主场景。");
        Directory.CreateDirectory("Logs/Day12");
        ValidateAssets();
        ValidateSaveFormats();
        Day11Validation.Begin("Consumables");
    }

    private static void ValidateAssets()
    {
        var hp = AssetDatabase.LoadAssetAtPath<ConsumableData>(Day12Setup.ConfigFolder + "SO_HPFlask.asset");
        var mp = AssetDatabase.LoadAssetAtPath<ConsumableData>(Day12Setup.ConfigFolder + "SO_MPFlask.asset");
        Require(hp != null && mp != null && hp.Icon != null && mp.Icon != null && hp.Icon != mp.Icon, "Distinct HP/MP configs and icons");
        Require(hp.MaxCharges == 3 && mp.MaxCharges == 3, "Three independent charges per flask");
        Require(hp.Effects.Count == 1 && hp.Effects[0] is RestoreHealthEffect h && h.Amount == 40, "HP effect is configured as 40");
        Require(mp.Effects.Count == 1 && mp.Effects[0] is RestoreManaEffect m && m.Amount == 50, "MP effect is configured as 50");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/Player_Day1.prefab");
        foreach (var root in new[] { prefab, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/UI/Prefabs/PF_HealingFlaskUI.prefab") })
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "No missing scripts at " + t.name);
        var items = new SerializedObject(prefab.GetComponent<PlayerItemController>()).FindProperty("_quickItemSlots");
        Require(items.arraySize == 2, "Player prefab has two item slots");
        for (int i = 0; i < 2; i++)
        {
            var item = (PlayerConsumable)items.GetArrayElementAtIndex(i).objectReferenceValue;
            var so = new SerializedObject(item);
            Require(item.Data == (i == 0 ? hp : mp) && so.FindProperty("_heldBottle").objectReferenceValue != null, "Slot config and held model assigned: " + i);
        }
        var sceneItems = Object.FindObjectOfType<PlayerItemController>();
        Require(new SerializedObject(sceneItems).FindProperty("_quickItemSlots").arraySize == 2, "Scene inherits two slots");
        foreach (var root in new[] { prefab, sceneItems.gameObject })
        {
            var red = root.GetComponent<PlayerHealingFlask>();
            var blue = root.GetComponents<PlayerConsumable>().Single(p => p != red);
            Require(MountsMatch(Held(red), Held(blue)), "HP/MP parent and every model transform match: " + root.name);
        }
        var hud = Object.FindObjectOfType<QuickItemPresenter>();
        Require(new SerializedObject(hud).FindProperty("_items").objectReferenceValue == sceneItems, "HUD references scene item controller");
        foreach (Transform t in sceneItems.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Scene player no missing scripts: " + t.name);
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Game/Input/Player.inputactions");
        Require(actions.FindAction("SwitchQuickItem").bindings.Any(b => b.path == "<Keyboard>/1"), "Keyboard 1 binding");
        Require(actions.FindAction("SwitchQuickItem").bindings.Any(b => b.path == "<Gamepad>/dpad/down"), "DPadDown binding");
        Require(actions.FindAction("UseItem").bindings.Any(b => b.path == "<Keyboard>/r") &&
            actions.FindAction("SwitchWeapon").bindings.Any(b => b.path == "<Mouse>/scroll/y"), "R and wheel bindings preserved");
        File.WriteAllText("Logs/Day12/AssetValidation.txt", "PASS: configs, effects, icons, prefab/scene slots, held models, HUD references, input bindings and no missing scripts.\n");
    }

    private static void ValidateSaveFormats()
    {
        string path = "Logs/Day12/SaveProbe.json";
        var results = new List<string>();
        Action<bool, string> verify = (ok, message) => { Require(ok, message); results.Add("PASS: " + message); };
        var data = new GameSaveData("03_AncientDungeon_Checkpoint", "") { flaskCharges = 1, hpFlaskCharges = 1, mpFlaskCharges = 2, currentQuickItemSlot = 1, souls = 123 };
        verify(SaveService.Save(data, path), "v5 save succeeds");
        var loaded = SaveService.Load(path);
        verify(loaded != null && loaded.hpFlaskCharges == 1 && loaded.mpFlaskCharges == 2 && loaded.currentQuickItemSlot == 1 && loaded.souls == 123, "v5 round trip preserves items and souls");
        data.hpFlaskCharges = data.mpFlaskCharges = data.flaskCharges = 0;
        verify(SaveService.Save(data, path) && SaveService.Load(path).hpFlaskCharges == 0 && SaveService.Load(path).mpFlaskCharges == 0, "Zero charges remain zero");
        for (int version = 1; version <= 4; version++)
        {
            data.version = version; data.flaskCharges = 2;
            string json = JsonUtility.ToJson(data);
            foreach (string field in new[] { "hpFlaskCharges", "mpFlaskCharges", "currentQuickItemSlot" })
                json = System.Text.RegularExpressions.Regex.Replace(json, ",?\"" + field + "\":-?\\d+", "");
            if (version == 1) json = "{\"version\":1,\"sceneName\":\"03_AncientDungeon_Checkpoint\",\"checkpointId\":\"test\"}";
            File.WriteAllText(path, json); loaded = SaveService.Load(path);
            verify(loaded != null && loaded.version == 5 && loaded.hpFlaskCharges == (version == 4 ? 2 : -1) &&
                loaded.mpFlaskCharges == -1 && loaded.currentQuickItemSlot == 0, "v" + version + " migrates through full version chain");
        }
        data.version = 5;
        string valid = JsonUtility.ToJson(data);
        foreach (string field in new[] { "hpFlaskCharges", "mpFlaskCharges", "currentQuickItemSlot", "flaskCharges", "souls" })
        {
            string json = System.Text.RegularExpressions.Regex.Replace(valid, ",?\"" + field + "\":-?\\d+", "");
            File.WriteAllText(path, json);
            verify(SaveService.Load(path) == null && File.ReadAllText(path) == json, "Missing v5 field rejected and file untouched: " + field);
        }
        foreach (string json in new[] { valid.Replace("\"version\":5", "\"version\":999"), "{broken", valid.Replace("\"currentQuickItemSlot\":1", "\"currentQuickItemSlot\":2") })
        {
            File.WriteAllText(path, json);
            verify(SaveService.Load(path) == null && File.ReadAllText(path) == json, "Future/corrupt/invalid slot rejected without overwrite");
        }
        // 损坏 v4 缺少 flaskCharges 不能被迁移成合法的满瓶。
        File.WriteAllText(path, valid.Replace("\"version\":5", "\"version\":4").Replace(",\"flaskCharges\":2", ""));
        verify(SaveService.Load(path) == null, "Incomplete v4 remains protected");
        File.Delete(path);
        File.WriteAllLines("Logs/Day12/SaveValidation.txt", results);
    }

    public static IEnumerator Scenarios()
    {
        var hp = Player.Items.GetItem(0);
        var mp = Player.Items.GetItem(1);
        Check(hp != null && mp != null && hp != mp, "Two independent runtime items");
        Check(MountsMatch(Held(hp), Held(mp)), "Live HP/MP share hand bone and complete model pose");
        Player.Health.RestoreFull(); Mana.RestoreFull(); Player.Items.RefillRestItems();
        Player.Items.EquipItemSlot(0);
        int itemEvents = 0; int chargeEvents = 0;
        Action<IPlayerQuickItem> onItem = _ => itemEvents++;
        Action<int, int> onCharge = (_, __) => chargeEvents++;
        Player.Items.CurrentItemChanged += onItem;
        hp.ChargesChanged += onCharge;
        try
        {
            Check(!hp.CanUse && !Player.Items.TryUseCurrentItem() && Idle && hp.CurrentCharges == 3, "Full HP cannot enter use state or spend charge");
            Check(Icon.sprite == hp.Data.Icon && Count.text == "3 / 3", "HP HUD icon and count");
            Check(Player.Items.SwitchNextItem() && Player.Items.CurrentItem == mp && itemEvents == 1, "HP to MP fires one event");
            Check(Icon.sprite == mp.Data.Icon && Count.text == "3 / 3", "MP HUD refreshes immediately");
            Check(!mp.CanUse && !Player.Items.TryUseCurrentItem() && mp.CurrentCharges == 3, "Full MP cannot use or spend charge");
            Check(Player.Items.SwitchNextItem() && Player.Items.CurrentItem == hp && itemEvents == 2, "MP to HP wraps");
            Check(Player.Items.SwitchPreviousItem() && Player.Items.CurrentItem == mp, "Previous switch wraps to MP");
            Check(!Player.Items.EquipItemSlot(-1) && !Player.Items.EquipItemSlot(2), "Invalid slot rejected");
            Player.Items.EquipItemSlot(0);
            Player.Health.TakeDamage(new DamageInfo { Damage = 60 });
            yield return new Func<bool>(() => Idle);
            int health = Player.Health.CurrentHealth;
            hp.RestoreCharges(0);
            Check(!Player.Items.TryUseCurrentItem() && hp.CurrentCharges == 0 && Idle, "Zero HP charges cannot enter use state");
            Check(Count.text == "0 / 3" && Icon.color.a < 1, "Zero HUD shows zero and dim icon");
            hp.Refill(); chargeEvents = 0;
            Check(Player.Items.TryUseCurrentItem() && Player.CurrentState == Player.UseItemState, "HP starts generic UseItem state");
            Check(Player.Health.CurrentHealth == health && hp.CurrentCharges == 3, "Before ConsumePoint HP and charges unchanged");
            Check(Held(hp).activeSelf && !Held(mp).activeSelf && WeaponsHidden(), "Correct bottle shown and all weapons hidden");
            Check(Player.Items.SwitchNextItem() && Player.Items.CurrentItem == mp && Held(hp).activeSelf && !Held(mp).activeSelf,
                "UseItem allows switching to MP while keeping captured HP bottle visible");
            yield return new Func<bool>(() => hp.CurrentCharges == 2);
            Check(Player.Health.CurrentHealth == Mathf.Min(Player.Health.MaxHealth, health + 40) && chargeEvents == 1, "ConsumePoint restores 40 HP and one charge event");
            Check(mp.CurrentCharges == 3 && Player.Items.CurrentItem == mp && Icon.sprite == mp.Data.Icon && Count.text == "3 / 3",
                "Switch during HP use preserves MP charges and selected MP HUD");
            Check(Player.Items.SwitchPreviousItem() && Player.Items.EquipItemSlot(1), "Both previous and explicit selection work during UseItem");
            yield return new Func<bool>(() => Idle);
            Check(hp.CurrentCharges == 2 && chargeEvents == 1 && !Held(hp).activeSelf && !WeaponsHidden(), "Completion applies once and restores visuals");
            Player.Items.EquipItemSlot(0);

            Player.Health.TakeDamage(new DamageInfo { Damage = 30 }); yield return new Func<bool>(() => Idle);
            hp.Refill(); health = Player.Health.CurrentHealth;
            Check(Player.Items.TryUseCurrentItem(), "Pre-consume Hurt scenario starts");
            Player.Health.TakeDamage(new DamageInfo { Damage = 1 });
            Check(Player.CurrentState == Player.HurtState && hp.CurrentCharges == 3 && Player.Health.CurrentHealth == health - 1, "Hurt before ConsumePoint neither consumes nor heals");
            Check(!Held(hp).activeSelf && !WeaponsHidden() && Player.Items.SwitchNextItem(), "Hurt cleans visuals and allows switching");
            Player.Items.EquipItemSlot(0);
            yield return new Func<bool>(() => Idle);
            Check(hp.CurrentCharges == 3, "Interrupted HP use never consumes later");
            Check(Player.Items.TryUseCurrentItem(), "Post-consume Hurt scenario starts");
            yield return new Func<bool>(() => hp.CurrentCharges == 2);
            health = Player.Health.CurrentHealth;
            Player.Health.TakeDamage(new DamageInfo { Damage = 1 });
            Check(hp.CurrentCharges == 2 && Player.Health.CurrentHealth == health - 1 && !Held(hp).activeSelf, "Hurt after ConsumePoint keeps committed heal and charge");
            yield return new Func<bool>(() => Idle);
            Check(hp.CurrentCharges == 2, "Post-consume interruption never applies again");

            Player.Items.EquipItemSlot(1); mp.Refill(); Mana.Consume(Mana.CurrentMana);
            mp.RestoreCharges(0);
            Check(!Player.Items.TryUseCurrentItem() && Idle && mp.CurrentCharges == 0, "Zero MP charges reject use");
            mp.Refill(); float mana = Mana.CurrentMana;
            Check(Player.Items.TryUseCurrentItem() && Player.CurrentState == Player.UseItemState, "MP shares generic state and same ItemUse layer");
            Check(Mana.CurrentMana == mana && mp.CurrentCharges == 3 && Held(mp).activeSelf && !Held(hp).activeSelf, "MP unchanged before ConsumePoint and blue bottle shown");
            yield return new Func<bool>(() => mp.CurrentCharges == 2);
            Check(Mathf.Approximately(Mana.CurrentMana, Mathf.Min(Mana.MaxMana, mana + 50)), "MP ConsumePoint restores exactly 50, capped at max");
            yield return new Func<bool>(() => Idle);
            Check(mp.CurrentCharges == 2 && !Held(mp).activeSelf && !WeaponsHidden(), "MP consumes once and restores visuals");
            Mana.RestoreFull(); Mana.Consume(10);
            Check(Player.Items.TryUseCurrentItem(), "Near-full MP use starts");
            yield return new Func<bool>(() => Idle);
            Check(Mana.CurrentMana == Mana.MaxMana && mp.CurrentCharges == 1, "MP restore caps at max and costs one charge");
            Mana.Consume(20); mp.Refill();
            Check(Player.Items.TryUseCurrentItem(), "Mid-animation full-resource scenario starts");
            Mana.RestoreFull(); yield return new Func<bool>(() => Idle);
            Check(mp.CurrentCharges == 3, "Resource filled before ConsumePoint causes no consumption");
            Mana.Consume(60);
            Check(Player.Items.TryUseCurrentItem(), "MP pre-consume interruption starts");
            Player.Health.TakeDamage(new DamageInfo { Damage = 1 });
            Check(mp.CurrentCharges == 3 && Mana.CurrentMana == Mana.MaxMana - 60 && !Held(mp).activeSelf, "MP pre-consume Hurt keeps charges and Mana");
            yield return new Func<bool>(() => Idle);
            Check(Player.Items.TryUseCurrentItem(), "MP post-consume interruption starts");
            yield return new Func<bool>(() => mp.CurrentCharges == 2);
            mana = Mana.CurrentMana; Player.Health.TakeDamage(new DamageInfo { Damage = 1 });
            Check(mp.CurrentCharges == 2 && Mana.CurrentMana == mana && !Held(mp).activeSelf, "MP post-consume Hurt keeps effect and charge");
            yield return new Func<bool>(() => Idle);

            var mixed = Object.Instantiate(hp.Data);
            try
            {
                var effects = new SerializedObject(mixed);
                var array = effects.FindProperty("_effects"); array.arraySize = 2;
                array.GetArrayElementAtIndex(0).objectReferenceValue = hp.Data.Effects[0];
                array.GetArrayElementAtIndex(1).objectReferenceValue = mp.Data.Effects[0];
                effects.ApplyModifiedPropertiesWithoutUndo();
                Player.Health.RestoreFull(); Mana.RestoreFull();
                Check(!mixed.CanApply(Player.gameObject), "Multi-effect data cannot waste use when all resources full");
                Mana.Consume(40);
                Check(mixed.CanApply(Player.gameObject) && mixed.Apply(Player.gameObject) && Mana.CurrentMana == Mana.MaxMana &&
                    Player.Health.CurrentHealth == Player.Health.MaxHealth, "Multi-effect item can restore MP while HP effect is inapplicable");
                Player.Health.TakeDamage(new DamageInfo { Damage = 50 }); yield return new Func<bool>(() => Idle);
                Mana.Consume(60); int mixedHP = Player.Health.CurrentHealth; float mixedMP = Mana.CurrentMana;
                Check(mixed.Apply(Player.gameObject) && Player.Health.CurrentHealth == mixedHP + 40 && Mana.CurrentMana == mixedMP + 50,
                    "Effects array applies both independent effects without state/controller type checks");
            }
            finally { Object.Destroy(mixed); }

            var hud = Object.FindObjectOfType<QuickItemPresenter>();
            hud.enabled = false; Player.Items.EquipItemSlot(0); hp.RestoreCharges(1); hud.enabled = true;
            Check(Icon.sprite == hp.Data.Icon && Count.text == "1 / 3", "Re-enabled HUD rebinds current item");
            mp.RestoreCharges(0);
            Check(Count.text == "1 / 3" && Icon.sprite == hp.Data.Icon, "Inactive slot change cannot corrupt visible HUD");
            yield return ValidateInput();
            yield return ValidateGameplaySwitching();

            var checkpoint = Object.FindObjectOfType<CheckpointManager>();
            var site = Object.FindObjectsOfType<CheckpointSite>().First();
            hp.RestoreCharges(0); mp.RestoreCharges(0); Mana.Consume(20); Player.Stamina.Consume(10);
            Check(checkpoint.ActivateCheckpoint(site), "Real checkpoint rest succeeds");
            Check(hp.CurrentCharges == 3 && mp.CurrentCharges == 3 && Player.Health.CurrentHealth == Player.Health.MaxHealth &&
                Mana.CurrentMana == Mana.MaxMana && Player.Stamina.CurrentStamina == Player.Stamina.MaxStamina, "Rest restores HP/MP/stamina and both items");
            Check(Count.text == "3 / 3", "Rest refreshes current HUD");
            var saved = SaveService.Load();
            Check(saved != null && saved.hpFlaskCharges == 3 && saved.mpFlaskCharges == 3, "Rest saves both full items");
            Check(!Player.Items.SwitchNextItem() && !Player.Items.TryUseCurrentItem(), "Open checkpoint menu keeps switch and use blocked");
            foreach (var menu in Object.FindObjectsOfType<ProgressionPresenter>()) menu.CloseMenu();
            yield return new Func<bool>(() => Idle && Player.InputReader.isActiveAndEnabled);
            foreach (EnemyStateMachine enemy in Object.FindObjectsOfType<EnemyStateMachine>()) enemy.gameObject.SetActive(false);

            // 保存后重新载入实际主场景，验证 Awake/Start/HUD 的初始化顺序。
            hp.RestoreCharges(1); mp.RestoreCharges(2); Player.Items.EquipItemSlot(1);
            int souls = Player.GetComponent<SoulWallet>().CurrentSouls;
            Check(checkpoint.SaveCurrentProgression(), "Save snapshot succeeds after item change");
            saved = SaveService.Load();
            Check(saved.hpFlaskCharges == 1 && saved.mpFlaskCharges == 2 && saved.currentQuickItemSlot == 1 && saved.souls == souls, "Saved charges, selection and souls correct");
        }
        finally { Player.Items.CurrentItemChanged -= onItem; hp.ChargesChanged -= onCharge; }
        EditorSceneManager.LoadSceneInPlayMode(Day12Setup.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return .5f;
        var reloaded = Object.FindObjectOfType<PlayerItemController>();
        Check(reloaded.GetItem(0).CurrentCharges == 1 && reloaded.GetItem(1).CurrentCharges == 2 && reloaded.CurrentSlotIndex == 1, "Scene restart restores both charges and selected slot");
        Check(Icon.sprite == reloaded.GetItem(1).Data.Icon && Count.text == "2 / 3", "Scene restart binds MP icon and restored count");
    }

    private static IEnumerator ValidateInput()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        var gamepad = InputSystem.AddDevice<Gamepad>();
        var input = Player.GetComponent<PlayerInput>();
        var editor = InputSystem.settings.editorInputBehaviorInPlayMode;
        var background = InputSystem.settings.backgroundBehavior;
        var update = InputSystem.settings.updateMode;
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            input.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            Player.Items.EquipItemSlot(0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1)); Pump();
            yield return new Func<bool>(() => Player.Items.CurrentSlotIndex == 1);
            Check(Icon.sprite == Player.Items.GetItem(1).Data.Icon, "Real Keyboard 1 binding switches current item and HUD");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Pump();
            Mana.Consume(20); Player.Items.GetItem(1).Refill();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R)); Pump();
            yield return new Func<bool>(() => Player.CurrentState == Player.UseItemState);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1)); Pump();
            yield return new Func<bool>(() => Player.Items.CurrentSlotIndex == 0);
            Check(Player.CurrentState == Player.UseItemState && Held(Player.Items.GetItem(1)).activeSelf && !Held(Player.Items.GetItem(0)).activeSelf,
                "Real switch input works during UseItem without changing captured MP visual");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Pump();
            yield return new Func<bool>(() => Idle);
            Check(Player.Items.CurrentSlotIndex == 0 && Player.Items.GetItem(1).CurrentCharges == 2 &&
                !Player.InputReader.ConsumeSwitchQuickItem(), "MP use consumes captured MP once and switch does not repeat after completion");

            Player.Stamina.RestoreFull();
            Check(Player.TryBeginAttack(PlayerAttackType.Light), "Attack starts for real keyboard switch");
            yield return PressSwitch(keyboard, "Keyboard 1 during attack");
            Check(Player.CurrentState == Player.AttackState, "Selecting another item leaves attack running");
            yield return new Func<bool>(() => Idle);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift)); Pump();
            yield return .1f;
            Check(Player.InputReader.SprintInput && Player.InputReader.MoveInput.y > 0 && Idle, "Real movement and sprint input active");
            yield return PressSwitch(keyboard, "Keyboard 1 while sprinting", Key.W, Key.LeftShift);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Pump();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); Pump();
            yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState && Player.Motor.VerticalVelocity > 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Pump();
            yield return PressSwitch(keyboard, "Keyboard 1 during jump ascent");
            yield return new Func<bool>(() => Player.CurrentState == Player.AirborneState && Player.Motor.VerticalVelocity < 0);
            yield return PressSwitch(keyboard, "Keyboard 1 while falling");
            yield return new Func<bool>(() => Idle);
            yield return PressSwitch(keyboard, "Keyboard 1 after landing");

            Player.Items.EquipItemSlot(1);
            input.SwitchCurrentControlScheme("Gamepad", gamepad);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown)); Pump();
            yield return new Func<bool>(() => Player.Items.CurrentSlotIndex == 0);
            Check(Icon.sprite == Player.Items.GetItem(0).Data.Icon, "Simulated gamepad DPadDown switches current item and HUD");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); Pump();
        }
        finally
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = editor;
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.updateMode = update;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(gamepad);
            Player.InputReader.ClearActionRequests();
        }
    }

    private static IEnumerator PressSwitch(Keyboard keyboard, string label, params Key[] heldKeys)
    {
        int expected = 1 - Player.Items.CurrentSlotIndex;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys.Concat(new[] { Key.Digit1 }).ToArray())); Pump();
        yield return new Func<bool>(() => Player.Items.CurrentSlotIndex == expected);
        Check(Icon.sprite == Player.Items.CurrentItem.Data.Icon, label + " updates selected slot and HUD");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys)); Pump();
    }

    private static IEnumerator ValidateGameplaySwitching()
    {
        Player.Stamina.RestoreFull(); Mana.RestoreFull();
        Check(Player.TryBeginAttack(PlayerAttackType.Light), "Light attack starts for switch guard");
        Check(Player.Items.SwitchNextItem() && Player.CurrentState == Player.AttackState && !Player.Items.TryUseCurrentItem(),
            "Attack allows selection and continues, while use remains restricted");
        yield return new Func<bool>(() => Idle);
        Player.Stamina.RestoreFull();
        Check(Player.TryBeginAttack(PlayerAttackType.WeaponSkill), "WeaponSkill starts for MP resource loop");
        float mana = Mana.CurrentMana;
        Check(mana < Mana.MaxMana && Player.Items.SwitchNextItem() && Player.CurrentState == Player.AttackState,
            "Skill consumes MP and allows switching without interruption");
        yield return new Func<bool>(() => Idle);
        Player.Items.EquipItemSlot(1); Player.Items.GetItem(1).Refill();
        Check(Player.Items.TryUseCurrentItem(), "MP flask starts after WeaponSkill");
        yield return new Func<bool>(() => Idle);
        Check(Mathf.Approximately(Mana.CurrentMana, Mathf.Min(Mana.MaxMana, mana + 50)), "WeaponSkill to MP flask completes resource loop");
        Player.Equipment.CycleWeapon();
        Check(Player.CurrentState == Player.WeaponSwitchState && Player.Items.SwitchNextItem(), "WeaponSwitch allows item selection");
        yield return new Func<bool>(() => Idle);
        Player.Stamina.RestoreFull();
        Player.ChangeState(Player.DodgeState);
        Check(Player.Items.SwitchNextItem() && Player.CurrentState == Player.DodgeState, "Dodge allows selection without interrupting dodge");
        yield return new Func<bool>(() => Idle);
        Time.timeScale = 0;
        Check(!Player.Items.SwitchNextItem() && !Player.Items.TryUseCurrentItem(), "Pause forbids switch and use");
        Time.timeScale = 1;
        Player.Health.TakeDamage(new DamageInfo { Damage = 10 }); yield return new Func<bool>(() => Idle);
        Player.Items.EquipItemSlot(0); Player.Items.GetItem(0).Refill();
        Check(Player.Items.TryUseCurrentItem(), "Death interruption before consumption starts");
        Player.Health.Die();
        Check(Player.CurrentState == Player.DeadState && !Player.Items.SwitchNextItem() && !Player.Items.TryUseCurrentItem() &&
            Player.Items.GetItem(0).CurrentCharges == 3 && !Held(Player.Items.GetItem(0)).activeSelf && !WeaponsHidden(), "Death interrupts use with no consumption and cleans bottle/weapons");
        yield return new Func<bool>(() => !Player.Health.IsDead && Player.InputReader.isActiveAndEnabled && Idle);
        Check(Player.Items.GetItem(0).CurrentCharges == 3 && Player.Items.GetItem(1).CurrentCharges == 3, "Actual respawn refills both flasks");
        foreach (EnemyStateMachine enemy in Object.FindObjectsOfType<EnemyStateMachine>()) enemy.gameObject.SetActive(false);
        Player.Motor.Teleport(new Vector3(1000, .1f, 1000), Quaternion.identity);
        yield return new Func<bool>(() => Idle);
    }

    private static GameObject Held(IPlayerQuickItem item) => (GameObject)new SerializedObject((Object)item).FindProperty("_heldBottle").objectReferenceValue;
    private static bool MountsMatch(GameObject red, GameObject blue)
    {
        if (red.transform.parent != blue.transform.parent) return false;
        var a = red.GetComponentsInChildren<Transform>(true);
        var b = blue.GetComponentsInChildren<Transform>(true);
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if ((a[i].localPosition - b[i].localPosition).sqrMagnitude > 1e-10f ||
                Quaternion.Angle(a[i].localRotation, b[i].localRotation) > .01f ||
                (a[i].localScale - b[i].localScale).sqrMagnitude > 1e-10f) return false;
        return true;
    }
    private static bool WeaponsHidden() => Player.Equipment.CurrentHitbox.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled);
    private static void Pump() => typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
        null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Manual });
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
