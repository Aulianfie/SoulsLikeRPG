using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// 临时验收探针：结束时恢复用户存档，不保存运行时场景。
[InitializeOnLoad]
public static class WeaponSwitchRuntimeProbe
{
    private const string SessionPrefix = "WeaponSwitch.Probe.";
    private const string Backup = "Logs/WeaponSwitchBackup/OriginalSave.json";
    private static readonly List<string> Checks = new List<string>();
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static string failure;
    private static double deadline;
    private static int lastFrame;
    private static PlayerStateMachine player;
    private static PlayerHealingFlask flask;
    private static Animator animator;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static Vector3 origin;
    private static Quaternion rotation;


    private static Renderer[] weaponRenderers;
    private static bool[] weaponStates;
    [Serializable] private class Result { public bool passed; public string[] checks; public string failure; }
    static WeaponSwitchRuntimeProbe() { EditorApplication.playModeStateChanged += Changed; }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("需要编辑模式。");
        Directory.CreateDirectory("Logs/WeaponSwitchBackup");
        var scene = EditorSceneManager.GetActiveScene();
        SessionState.SetBool(SessionPrefix + "SceneDirty", scene.isDirty);
        const string snapshot = "Assets/_Game/Scenes/WeaponSwitch_TestSnapshot.unity";
        if (!EditorSceneManager.SaveScene(scene, snapshot, true)) throw new IOException("Cannot backup in-memory scene");
        File.Copy(snapshot, "Logs/WeaponSwitchBackup/SceneBeforePlay.unity", true);
        AssetDatabase.DeleteAsset(snapshot);
        if (File.Exists("Logs/WeaponSwitch_RuntimeConsole.txt"))
            File.Copy("Logs/WeaponSwitch_RuntimeConsole.txt", "Logs/WeaponSwitchBackup/Previous_RuntimeConsole.txt", true);
        File.WriteAllText("Logs/WeaponSwitch_RuntimeConsole.txt", "");
        SessionState.SetString(SessionPrefix + "Path", SaveService.SaveFilePath);
        SessionState.SetBool(SessionPrefix + "Exists", File.Exists(SaveService.SaveFilePath));
        if (File.Exists(SaveService.SaveFilePath)) File.WriteAllBytes(Backup, File.ReadAllBytes(SaveService.SaveFilePath));
        SessionState.SetBool(SessionPrefix + "Running", true);
        // 已备份原始字节，测试使用确定的新游戏存档。
        if (!SaveService.Save(new GameSaveData(EditorSceneManager.GetActiveScene().name, "")))
            throw new InvalidOperationException("无法创建测试存档。");
        EditorApplication.isPlaying = true;
    }

    public static void RunLoadZero()
    {
        SessionState.SetBool(SessionPrefix + "LoadZero", true);
        Run();
        if (!SaveService.Save(new GameSaveData(EditorSceneManager.GetActiveScene().name, "") { flaskCharges = 0 }))
            throw new InvalidOperationException("无法创建零瓶数存档。");
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionPrefix + "Running", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Checks.Clear(); Steps.Clear(); failure = null; lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 180;
            Application.logMessageReceived += Log;
            Steps.Push(Verify()); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            string path = SessionState.GetString(SessionPrefix + "Path", "");
            if (SessionState.GetBool(SessionPrefix + "Exists", false)) File.WriteAllBytes(path, File.ReadAllBytes(Backup));
            else if (File.Exists(path)) File.Delete(path);
            SessionState.SetBool(SessionPrefix + "Running", false);
            bool zero = SessionState.GetBool(SessionPrefix + "LoadZero", false);
            SessionState.SetBool(SessionPrefix + "LoadZero", false);
            File.WriteAllText(zero ? "Logs/WeaponSwitch_LoadValidation.json" : "Logs/WeaponSwitch_RuntimeValidation.json", JsonUtility.ToJson(new Result
                { passed = failure == null, checks = Checks.ToArray(), failure = failure }, true));
            Debug.Log("[Day10 Combo Probe] " + (failure == null ? "PASS" : failure));
        }
    }
    private static void Log(string message, string stack, LogType type)
    {
        File.AppendAllText("Logs/WeaponSwitch_RuntimeConsole.txt", type + ": " + message + "\n");
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = message + "\n" + stack;
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (failure != null) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Probe exceeded 180 seconds.");
            while (Steps.Count > 0)
            {
                IEnumerator step = Steps.Peek();
                if (!step.MoveNext()) { Steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { Steps.Push(nested); continue; }
                return;
            }
            End();
        }
        catch (Exception exception) { failure = exception.ToString(); End(); }
    }
    private static void End()
    {
        if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        if (mouse != null) InputSystem.QueueStateEvent(mouse, new MouseState());
        Time.timeScale = 1; EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string text)
    {
        if (!condition) throw new Exception(text + "; state=" + player?.CurrentStateName + "; hp=" + player?.Health.CurrentHealth + "; flask=" + flask?.CurrentCharges);
        if (weaponRenderers != null && player.CurrentState != player.HealState)
            for (int i = 0; i < weaponRenderers.Length; i++)
                if (weaponRenderers[i].enabled != weaponStates[i])
                    throw new Exception("Weapon renderer not restored: " + text);
        Checks.Add(text); File.WriteAllLines("Logs/WeaponSwitch_ProbeProgress.txt", Checks);
    }
    private static IEnumerator Frames(int count) { for(int i=0;i<count;i++) yield return null; }
    private static IEnumerator Seconds(float duration)
    {
        float end = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < end) yield return null;
    }
    private static IEnumerator Until(Func<bool> condition, string label, float seconds = 8)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition()) { if (Time.realtimeSinceStartup > end) throw new TimeoutException(label); yield return null; }
    }
    private static IEnumerator Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return Frames(3);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames(2);
    }
    private static IEnumerator Ready(int damage = 60)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        player.Targeting.ClearTarget(); player.Health.DisableIFrame();
        player.Health.RestoreFull(); flask.Refill(); player.Stamina.RestoreFull();
        player.Motor.Teleport(origin, rotation); yield return Seconds(.2f);
        yield return Until(() => player.CurrentState == player.LocomotionState && player.Motor.IsGrounded, "Ready ground");
        if (damage > 0)
        {
            player.Health.TakeDamage(new DamageInfo { Damage = damage });
            yield return Until(() => player.CurrentState == player.LocomotionState, "Hurt recovery");
        }
    }
    private static void DisableEnemies()
    { foreach (var enemy in Object.FindObjectsOfType<EnemyStateMachine>(true)) enemy.gameObject.SetActive(false); }

    private static PlayerEquipment equipment;
    private static AnimationClip sharedClip;
    private static int changed;
    private static float switchNormalized;
    private static float SwitchTime()
    {
        if (player.PlayerAnimator.TryGetWeaponSwitchNormalizedTime(out float time)) switchNormalized = time;
        return switchNormalized;
    }
    private static bool ActiveHitboxes() => player.GetComponentsInChildren<WeaponHitbox>(true)
        .Any(h => (bool)typeof(WeaponHitbox).GetField("_isActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(h));
    private static bool Visible() => equipment.CurrentHitbox.GetComponentsInChildren<Renderer>(true).All(r => r.enabled);
    private static bool Hidden() => player.GetComponentsInChildren<WeaponHitbox>(true)
        .SelectMany(h => h.GetComponentsInChildren<Renderer>(true)).All(r => !r.enabled);
    private static IEnumerator Click()
    {
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return Frames(2);
        InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Frames(1);
    }
    private static IEnumerator Scroll(float amount)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, amount) }); yield return Frames(2);
        InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Frames(1);
    }
    private static IEnumerator BeginSwitch()
    {
        yield return Until(() => player.CurrentState == player.LocomotionState && player.Motor.IsGrounded, "Switch ready");
        switchNormalized = 0;
        yield return Scroll(-120);
        yield return Until(() => player.CurrentState == player.WeaponSwitchState, "Scroll enters shared switch state");
    }
    private static IEnumerator Switch(int expected, bool capture)
    {
        player.Stamina.RestoreFull();
        int old = equipment.CurrentSlotIndex;
        int events = changed;
        float stamina = player.Stamina.CurrentStamina;
        int charges = flask.CurrentCharges;
        Vector3 position = player.transform.position;
        float start = Time.time;
        yield return BeginSwitch();
        Check(equipment.CurrentSlotIndex == old && changed == events, "Scroll starts action before changing equipment");
        Check(!equipment.CanSwitch && !equipment.EquipSlot(expected), "Repeat switch rejected during action");
        yield return Until(() => SwitchTime() >= .20f, "Shared clip entered");
        Check(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip == sharedClip), "Both directions play identical shared clip: " + sharedClip.name);
        if (capture) Capture("Logs/WeaponSwitch_Before.png");
        yield return Click(); yield return Press(Key.R, Key.Space); yield return Scroll(1200);
        Check(player.CurrentState == player.WeaponSwitchState && player.Stamina.CurrentStamina == stamina && flask.CurrentCharges == charges,
            "Attack, use item, jump and scroll cannot interrupt or consume resources during switch");
        yield return Until(() => SwitchTime() >= .42f, "Hidden phase");
        Check(equipment.CurrentSlotIndex == old && Hidden() && !ActiveHitboxes(), "Old weapon hidden before swap; all damage windows closed");
        yield return Until(() => equipment.CurrentSlotIndex == expected, "Equip point");
        Check(changed == events + 1 && SwitchTime() >= equipment.SwitchEquipPoint && Hidden(), "Equip event fires once at animation midpoint while hidden");
        Check(player.Combat.MaxComboCount == (expected == 0 ? 5 : 3), "Correct combat configuration applied at equip point");
        Check(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip == sharedClip), "Changing Animator override preserves shared switch motion");
        yield return Until(() => SwitchTime() >= .70f, "Draw phase");
        Check(Visible() && !ActiveHitboxes(), "New weapon shown during draw without opening damage window");
        Check(player.GetComponentsInChildren<WeaponHitbox>(true).Count(h => h.gameObject.activeSelf) == 1, "Exactly one precreated weapon active");
        if (capture) Capture(expected == 1 ? "Logs/WeaponSwitch_GreatSword.png" : "Logs/WeaponSwitch_LongSword.png");
        yield return Until(() => player.CurrentState == player.LocomotionState, "Action finishes");
        yield return Frames(4);
        Check(equipment.CurrentSlotIndex == expected && Visible() && changed == events + 1, "Action exits with visible weapon and no deferred scroll or attack");
        Check(Time.time - start < 1.15f && Vector3.Distance(player.transform.position, position) < .08f, "Shared action completes in about .8s with no root-motion drift");
    }
    private static IEnumerator Combo(int count)
    {
        player.Stamina.RestoreFull();
        yield return Click();
        Check(player.CurrentState == player.AttackState, "Attack works after animated equipment switch");
        for (int i = 0; i < count; i++)
        {
            int index = i;
            yield return Until(() => player.Combat.ComboIndex == index && player.Combat.IsInComboInputWindow, "Combo segment input");
            Check(!equipment.CanSwitch, "Switch remains blocked in combo segment " + (index + 1));
            if (equipment.CurrentSlotIndex == 1)
                Check(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name.Contains("Attack_A_" + (index + 1) + "_")), "Greatsword override attack " + (index + 1) + " retained");
            if (i + 1 < count) yield return Click();
            yield return Until(() => index + 1 < count ? player.Combat.ComboIndex == index + 1 : player.CurrentState == player.LocomotionState, "Combo advance");
        }
        Check(player.CurrentState == player.LocomotionState && !ActiveHitboxes(), "Complete " + count + "-hit combo closes hitbox and returns to movement");
    }
    private static IEnumerator Verify()
    {
        yield return Frames(12);
        player = Object.FindObjectOfType<PlayerStateMachine>(); flask = player.GetComponent<PlayerHealingFlask>();
        equipment = player.Equipment; animator = player.GetComponentInChildren<Animator>();
        sharedClip = AssetDatabase.LoadAllAssetsAtPath(WeaponSwitchSetup.ClipPath).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
        var input = player.GetComponent<PlayerInput>(); keyboard = input.devices.OfType<Keyboard>().First(); mouse = input.devices.OfType<Mouse>().First();
        DisableEnemies(); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return Until(() => player.Motor.IsGrounded && player.CurrentState == player.LocomotionState, "Initial ground");
        origin = player.transform.position; rotation = player.transform.rotation;
        equipment.WeaponChanged += weapon => changed++;
        Check(animator.avatar.isValid && animator.avatar.isHuman && sharedClip.humanMotion && !sharedClip.isLooping, "Shared clip retargets to current valid Humanoid player");
        Check(equipment.CurrentSlotIndex == 0 && Visible() && changed == 0, "Default weapon initialized immediately without switch action");
        yield return Switch(1, true);
        yield return Combo(3);
        yield return Switch(0, true);
        yield return Combo(5);

        int old = equipment.CurrentSlotIndex;
        yield return BeginSwitch();
        yield return Until(() => SwitchTime() >= .42f, "Hurt before equip point");
        player.Health.DisableIFrame(); player.Health.TakeDamage(new DamageInfo { Damage = 10 });
        Check(player.CurrentState == player.HurtState && equipment.CurrentSlotIndex == old && Visible(), "Hurt before equip cancels request, retains old weapon and restores visibility");
        yield return Until(() => player.CurrentState == player.LocomotionState, "Hurt recovery");
        yield return Frames(4);
        Check(equipment.CurrentSlotIndex == old, "Cancelled switch is not applied after hurt recovery");

        yield return BeginSwitch();
        yield return Until(() => equipment.CurrentSlotIndex != old, "Hurt after equip point");
        int committed = equipment.CurrentSlotIndex;
        player.Health.DisableIFrame(); player.Health.TakeDamage(new DamageInfo { Damage = 10 });
        Check(player.CurrentState == player.HurtState && equipment.CurrentSlotIndex == committed && Visible(), "Hurt after equip retains new weapon and restores visibility");
        yield return Until(() => player.CurrentState == player.LocomotionState, "Second hurt recovery");

        old = equipment.CurrentSlotIndex; player.Stamina.RestoreFull();
        yield return BeginSwitch();
        yield return Until(() => SwitchTime() >= .42f, "Dodge before equip");
        yield return Press(Key.LeftCtrl);
        Check(player.CurrentState == player.DodgeState && equipment.CurrentSlotIndex == old && Visible(), "Dodge cancels before equip and retains old weapon");
        yield return Until(() => player.CurrentState == player.LocomotionState && player.Motor.IsGrounded, "Dodge recovery");
        player.Motor.Teleport(origin, rotation); yield return Frames(8);

        yield return BeginSwitch();
        yield return Until(() => equipment.CurrentSlotIndex != old, "Dodge after equip");
        committed = equipment.CurrentSlotIndex;
        yield return Press(Key.LeftCtrl);
        Check(player.CurrentState == player.DodgeState && equipment.CurrentSlotIndex == committed && Visible(), "Dodge after equip retains new weapon");
        yield return Until(() => player.CurrentState == player.LocomotionState && player.Motor.IsGrounded, "Second dodge recovery");
        player.Motor.Teleport(origin, rotation); yield return Frames(8);

        old = equipment.CurrentSlotIndex;
        yield return BeginSwitch();
        yield return Until(() => SwitchTime() >= .20f, "Pause phase");
        Time.timeScale = 0; float pausedTime = SwitchTime(); yield return Frames(8);
        Check(Mathf.Abs(SwitchTime() - pausedTime) < .02f && equipment.CurrentSlotIndex == old && !equipment.CanSwitch, "Pause freezes switch animation and equipment commit");
        Time.timeScale = 1;
        yield return Until(() => player.CurrentState == player.LocomotionState, "Resume switch");
        Check(equipment.CurrentSlotIndex != old && Visible(), "Resume completes pending action once");

        old = equipment.CurrentSlotIndex;
        yield return BeginSwitch(); yield return Until(() => SwitchTime() >= .42f, "Disable equipment hidden phase");
        equipment.enabled = false; yield return Frames(3);
        Check(player.CurrentState == player.LocomotionState && equipment.CurrentSlotIndex == old && Visible(), "Disabled equipment exits action and restores old weapon");
        equipment.enabled = true; yield return Frames(3);
        Check(equipment.CurrentSlotIndex == old, "Re-enable does not commit a cancelled request");

        old = equipment.CurrentSlotIndex;
        yield return BeginSwitch(); yield return Until(() => SwitchTime() >= .42f, "Death before equip");
        player.Health.DisableIFrame(); player.Health.TakeDamage(new DamageInfo { Damage = 10000 });
        Check(player.CurrentState == player.DeadState && equipment.CurrentSlotIndex == old && Visible(), "Death before equip restores old weapon visibility");
        yield return Until(() => player.CurrentState == player.LocomotionState && player.Motor.IsGrounded && !player.Health.IsDead && player.InputReader.isActiveAndEnabled, "Natural respawn and fade-in complete", 15);
        Check(equipment.CurrentSlotIndex == old && Visible() && !ActiveHitboxes(), "Respawn keeps valid equipped weapon and no pending action");

        player.Health.DisableIFrame(); player.Health.TakeDamage(new DamageInfo { Damage = 20 });
        yield return Until(() => player.CurrentState == player.LocomotionState, "Prepare healing");
        yield return Until(() => player.CurrentState == player.LocomotionState && player.Motor.IsGrounded, "Healing grounded prerequisite");
        yield return Frames(3);
        File.WriteAllText("Logs/WeaponSwitch_HealingPrerequisites.txt", $"grounded={player.Motor.IsGrounded}; flaskAvailable={flask.IsAvailable}; canUse={flask.CanUse}; itemsEnabled={player.Items.isActiveAndEnabled}; item={player.Items.CurrentItem}; inputEnabled={player.InputReader.isActiveAndEnabled}; keyboard={keyboard.enabled}; action={input.actions.FindAction("UseItem").enabled}\n");
        yield return Press(Key.R);
        Check(player.CurrentState == player.HealState && Hidden() && !equipment.EquipSlot(1 - equipment.CurrentSlotIndex), "Healing hides weapon and rejects animated switching");
        yield return Until(() => player.CurrentState == player.LocomotionState, "Healing finishes", 6);
        Check(Visible(), "Healing restores visibility after switch interruptions");
        yield return Switch(1 - equipment.CurrentSlotIndex, false);
        Check(player.GetComponentsInChildren<WeaponHitbox>(true).Length == 2, "All switches reuse the original two models");
    }
    private static void Capture(string path)
    {
        var cameraObject = new GameObject("SwitchCaptureCamera"); var lightObject = new GameObject("SwitchCaptureLight");
        var target = new RenderTexture(640, 760, 24); var pixels = new Texture2D(640, 760, TextureFormat.RGB24, false);
        var transforms = player.GetComponentsInChildren<Transform>(true); int[] layers = transforms.Select(t => t.gameObject.layer).ToArray();
        var previous = RenderTexture.active;
        try
        {
            cameraObject.hideFlags = lightObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (var child in transforms) child.gameObject.layer = 31;
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.targetTexture = target; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f,.15f,.18f); camera.fieldOfView = 34;
            camera.transform.position = player.transform.TransformPoint(new Vector3(3.3f,1.7f,3.7f));
            camera.transform.LookAt(player.transform.position + Vector3.up * .95f);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(35,-30,0);
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,640,760),0,0); pixels.Apply(); File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<transforms.Length;i++) transforms[i].gameObject.layer=layers[i];
            RenderTexture.active=previous;
            Object.Destroy(cameraObject); Object.Destroy(lightObject); Object.Destroy(target); Object.Destroy(pixels);
        }
    }
}