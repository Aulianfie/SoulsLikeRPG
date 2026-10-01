using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Real Play Mode checks, following the project's existing editor-driven validation style.
[InitializeOnLoad]
public static class Day11Validation
{
    private const string Running = "Day11.Validation.Running";
    private static readonly List<string> Checks = new List<string>();
    private static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
    private static Func<bool> _waiting;
    private static double _next;
    private static double _deadline;
    private static double _waitDeadline;
    private static int _errors;
    public static PlayerStateMachine Player { get; private set; }
    public static Animator Animator { get; private set; }

    static Day11Validation()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += StateChanged;
        Application.logMessageReceived += RecordLog;
    }

    [MenuItem("Tools/SoulsLike RPG/Day11/Validate Moveset Migration")]
    public static void RunMigration()
    {
        Day11Setup.MigrateMovesets();
        Begin("Migration");
    }

    public static void Begin(string mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("请在已保存的 Edit Mode 运行验收。");
        Directory.CreateDirectory("Logs/Day11");
        SessionState.SetString(Running + ".Mode", mode);
        SessionState.SetBool(Running + ".Batch", Application.isBatchMode);
        SessionState.SetString(Running + ".Scene", EditorSceneManager.GetActiveScene().path);
        string path = SaveService.SaveFilePath;
        SessionState.SetString(Running + ".Save", path);
        SessionState.SetBool(Running + ".SaveExists", File.Exists(path));
        if (File.Exists(path)) File.Copy(path, "Logs/Day11/OriginalSave.json", true);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity");
        EditorApplication.isPlaying = true;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Checks.Clear(); Routines.Clear(); _errors = 0; _waiting = null;
            _next = EditorApplication.timeSinceStartup + 0.5;
            _deadline = EditorApplication.timeSinceStartup + 240;
            Routines.Push(Run());
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool batch = SessionState.GetBool(Running + ".Batch", false);
            string mode = SessionState.GetString(Running + ".Mode", "Unknown");
            string save = SessionState.GetString(Running + ".Save", "");
            string report = "Logs/Day11/" + mode + "_RuntimeValidation.txt";
            if (Checks.Count == 0 && File.Exists(report)) Checks.AddRange(File.ReadAllLines(report));
            try
            {
                if (SessionState.GetBool(Running + ".SaveExists", false))
                {
                    File.Copy("Logs/Day11/OriginalSave.json", save, true);
                    Check(File.ReadAllBytes(save).SequenceEqual(File.ReadAllBytes("Logs/Day11/OriginalSave.json")), "Original save bytes restored");
                }
                else if (File.Exists(save)) File.Delete(save);
                File.WriteAllLines(report, Checks);
                if (mode == "Consumables") File.Copy(report, "Logs/Day12/RuntimeValidation.txt", true);
            }
            finally { SessionState.SetBool(Running, false); }
            bool passed = Checks.All(c => !c.StartsWith("FAIL"));
            Debug.Log($"[Day11] {mode}_VALIDATION_{(passed ? "PASS" : "FAIL")} checks={Checks.Count}");
            if (batch) EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Running, false) || Routines.Count == 0) return;
        if (EditorApplication.timeSinceStartup < _next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Day11 Play Mode check timed out");
            if (_waiting != null)
            {
                if (EditorApplication.timeSinceStartup > _waitDeadline)
                    throw new TimeoutException("Day11 wait timed out after: " + Checks.LastOrDefault());
                if (!_waiting()) return;
                _waiting = null;
            }
            IEnumerator routine = Routines.Peek();
            if (!routine.MoveNext()) { Routines.Pop(); return; }
            if (routine.Current is IEnumerator nested) Routines.Push(nested);
            else if (routine.Current is Func<bool> wait) { _waiting = wait; _waitDeadline = EditorApplication.timeSinceStartup + 20; }
            else if (routine.Current is float seconds) _next = EditorApplication.timeSinceStartup + seconds;
        }
        catch (Exception exception)
        {
            Checks.Add("FAIL: " + exception);
            File.WriteAllLines("Logs/Day11/" + SessionState.GetString(Running + ".Mode", "Unknown") + "_RuntimeValidation.txt", Checks);
            while (Routines.Count > 0) (Routines.Pop() as IDisposable)?.Dispose();
            EditorApplication.isPlaying = false;
        }
    }

    private static IEnumerator Run()
    {
        Player = Object.FindObjectsOfType<PlayerStateMachine>().Single();
        Animator = Player.GetComponentInChildren<Animator>();
        foreach (EnemyStateMachine enemy in Object.FindObjectsOfType<EnemyStateMachine>()) enemy.gameObject.SetActive(false);
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Day11_ValidationPlatform";
        platform.transform.position = new Vector3(1000, -0.5f, 1000);
        platform.transform.localScale = new Vector3(100, 1, 100);
        Player.Motor.Teleport(new Vector3(1000, 0.1f, 1000), Quaternion.identity);
        Player.Health.ReviveFull(); Player.Respawn(); Time.timeScale = 1;
        Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        yield return new Func<bool>(() => Player.Motor.IsGrounded && Player.CurrentState == Player.LocomotionState);
        Check(Animator.avatar.isHuman && Animator.avatar.isValid, "Player Humanoid Avatar valid");
        yield return LightCombos();
        if (SessionState.GetString(Running + ".Mode", "") == "Consumables")
        {
            Type type = typeof(Day11Validation).Assembly.GetType("Day12Validation");
            yield return (IEnumerator)type.GetMethod("Scenarios").Invoke(null, null);
        }
        if (SessionState.GetString(Running + ".Mode", "") == "Actions")
        {
            Type type = typeof(Day11Validation).Assembly.GetType("Day11ActionValidation");
            if (type == null) throw new InvalidOperationException("Action validation not installed");
            yield return (IEnumerator)type.GetMethod("Scenarios").Invoke(null, null);
        }
        if (SessionState.GetString(Running + ".Mode", "").StartsWith("Damage"))
            yield return Day11DamageValidation.Scenarios();
        if (SessionState.GetString(Running + ".Mode", "") == "Feel")
            yield return Day11FeelValidation.Scenarios();
        Check(_errors == 0, "No runtime Console errors");
        File.WriteAllLines("Logs/Day11/" + SessionState.GetString(Running + ".Mode", "Unknown") + "_RuntimeValidation.txt", Checks);
        EditorApplication.isPlaying = false;
    }

    public static IEnumerator LightCombos()
    {
        for (int slot = 0; slot < 2; slot++)
        {
            if (Player.Equipment.CurrentSlotIndex != slot)
            {
                Check(Player.Equipment.EquipSlot(slot), "Weapon switch starts: " + slot);
                yield return new Func<bool>(() => Player.Equipment.CurrentSlotIndex == slot && Player.CurrentState == Player.LocomotionState);
            }
            int count = slot == 0 ? 5 : 3;
            Check(Player.Combat.MaxComboCount == count, "Light combo count: " + count);
            Player.Stamina.RestoreFull();
            float before = Player.Stamina.CurrentStamina;
            float cost = Player.Combat.FirstAttackStaminaCost;
            PulseLight();
            yield return new Func<bool>(() => Player.CurrentState == Player.AttackState);
            Check(Mathf.Abs(Player.Stamina.CurrentStamina - (before - cost)) < 0.1f, "First attack stamina charged once: " + slot);
            for (int index = 0; index < count; index++)
            {
                int expected = index;
                yield return new Func<bool>(() => Get<bool>(Player.Equipment.CurrentHitbox, "_isActive"));
                Check(Player.Combat.ComboIndex == expected, "Combo segment reached: " + slot + "/" + index);
                Check(Get<bool>(Player.Equipment.CurrentHitbox, "_isActive"), "Hit window opens: " + slot + "/" + index);
                if (index + 1 >= count) break;
                yield return new Func<bool>(() => Player.Combat.IsInComboInputWindow);
                Player.Stamina.RestoreFull();
                before = Player.Stamina.CurrentStamina;
                cost = Player.Combat.NextAttackStaminaCost;
                PulseLight();
                yield return new Func<bool>(() => Player.Combat.ComboIndex == expected + 1);
                Check(Mathf.Abs(Player.Stamina.CurrentStamina - (before - cost)) < 0.1f, "Next attack stamina charged once: " + slot + "/" + (index + 1));
            }
            yield return new Func<bool>(() => Player.CurrentState == Player.LocomotionState);
            Check(!Get<bool>(Player.Equipment.CurrentHitbox, "_isActive"), "Hitbox closes and combo returns to Locomotion: " + slot);
        }
    }

    public static void PulseLight()
    {
        Set(Player.InputReader, "_lightAttackRequested", true);
        Set(Player.InputReader, "_lightAttackExpireTime", Time.time + 0.2f);
    }

    public static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Checks.Add("PASS: " + message);
        File.WriteAllLines("Logs/Day11/" + SessionState.GetString(Running + ".Mode", "Unknown") + "_RuntimeValidation.txt", Checks);
    }

    public static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    public static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void RecordLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
        if (type != LogType.Log) File.AppendAllText("Logs/Day11/RuntimeConsole.txt", type + ": " + message + "\n" + stack + "\n");
    }
}
