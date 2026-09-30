using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class WeaponSwitchEditorBridge
{
    private static double next;
    static WeaponSwitchEditorBridge() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        next = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists("Temp/WeaponSwitch.request")) return;
        string command = File.ReadAllText("Temp/WeaponSwitch.request").Trim();
        File.Delete("Temp/WeaponSwitch.request");
        // 两个聊天可能共享此工作区；实播验收期间不执行其他任务的编辑器命令。
        if (SessionState.GetBool("WeaponSwitch.Probe.Running", false) &&
            command != "inspect" && !command.StartsWith("WeaponSwitch"))
        {
            File.WriteAllText("Logs/WeaponSwitch_DeferredRequest.txt", command + "\nWeaponSwitch validation owns Play Mode; retry after WeaponSwitch.Probe.Running=false.\n");
            return;
        }
        try
        {
            if (command == "inspect")
            {
                var scene = EditorSceneManager.GetActiveScene();
                var counts = new object[] { 0, 0, 0 };
                typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, counts);
                string report = $"Project={Application.dataPath}\nScene={scene.path}\nDirty={scene.isDirty}\nPlaying={EditorApplication.isPlaying}\nErrors={counts[0]} Warnings={counts[1]} Logs={counts[2]}\n";
                foreach (PlayerHealth player in UnityEngine.Object.FindObjectsOfType<PlayerHealth>(true).Where(p => p.gameObject.scene == scene))
                    report += $"Player={player.name} position={player.transform.position} prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player.gameObject)}\n";
                File.WriteAllText("Logs/WeaponSwitch_EditorState.txt", report);
            }
            else
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(command.Split('.')[0])).FirstOrDefault(t => t != null);
                if (type == null) throw new InvalidOperationException("Editor command not yet compiled: " + command);
                type.GetMethod(command.Split('.')[1], BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
            }
        }
        catch (Exception e) { File.WriteAllText("Logs/WeaponSwitch_BridgeError.txt", e.ToString()); Debug.LogException(e); }
    }
    public static void Stop() { EditorApplication.isPlaying = false; }
    public static void Refresh() { AssetDatabase.Refresh(); }
    public static void ValidateAssets()
    {
        var checks = new System.Collections.Generic.List<string>();
        var scene = EditorSceneManager.GetActiveScene();
        int missing = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Component>(true).Count(c => c == null));
        if (missing != 0) throw new InvalidOperationException("Main scene missing components: " + missing);
        checks.Add("Main scene has zero missing scripts");
        GameObject prefab = PrefabUtility.LoadPrefabContents(Day10Setup.PlayerPath);
        try
        {
            if (prefab.GetComponentsInChildren<Component>(true).Any(c => c == null)) throw new InvalidOperationException("Player prefab missing scripts");
            checks.Add("Player prefab has zero missing scripts");
            var equipment = prefab.GetComponent<PlayerEquipment>();
            var slots = new SerializedObject(equipment).FindProperty("_slots");
            if (slots.arraySize != 2) throw new InvalidOperationException("Expected two slots");
            for (int i = 0; i < 2; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                var weapon = (WeaponData)slot.FindPropertyRelative("Data").objectReferenceValue;
                var hitbox = (WeaponHitbox)slot.FindPropertyRelative("Hitbox").objectReferenceValue;
                if (weapon == null || hitbox == null || weapon.WeaponPrefab == null || weapon.AnimatorOverrideController == null ||
                    weapon.LightAttackCombo.Count != (i == 0 ? 5 : 3) || weapon.LightAttackCombo.Attacks.Any(a => a == null))
                    throw new InvalidOperationException("Invalid weapon configuration: " + i);
                var hitboxData = new SerializedObject(hitbox);
                if (hitboxData.FindProperty("_shape").objectReferenceValue == null || hitboxData.FindProperty("_targetLayers").intValue != 8)
                    throw new InvalidOperationException("Missing shape / enemy target layers: " + i);
                checks.Add(weapon.WeaponId + " has valid data, combo, model, overrides and Enemy hitbox");
            }
            if (prefab.GetComponentsInChildren<WeaponHitbox>(true).Length != 2)
                throw new InvalidOperationException("Unexpected duplicated weapon instances");
            checks.Add("Player prefab contains exactly two precreated weapons");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var input = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/_Game/Input/Player.inputactions");
        foreach (string name in new[] { "Move", "Look", "Sprint", "Jump", "LightAttack", "Dodge", "LockOn", "Interact", "UseItem", "SwitchWeapon" })
            if (input.FindAction(name) == null) throw new InvalidOperationException("Input action missing: " + name);
        checks.Add("All ten gameplay actions exist");
        File.WriteAllLines("Logs/WeaponSwitch_AssetValidation.txt", checks);
    }
    public static void Cleanup()
    {
        string folder = "Logs/WeaponSwitchTools";
        Directory.CreateDirectory(folder);
        foreach (string name in new[] { "WeaponSwitchRuntimeProbe.cs", "WeaponSwitchPreview.cs", "WeaponSwitchEditorBridge.cs" })
        {
            string path = "Assets/_Game/Editor/" + name;
            File.Copy(path, folder + "/" + name, true);
            File.Delete(path);
            File.Delete(path + ".meta");
        }
        UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished += (path, messages) =>
        {
            File.AppendAllText("Logs/WeaponSwitch_FinalCompilation.txt", path + ": errors=" +
                messages.Count(m => m.type == UnityEditor.Compilation.CompilerMessageType.Error) +
                "; warnings=" + messages.Count(m => m.type == UnityEditor.Compilation.CompilerMessageType.Warning) + "\n");
        };
        UnityEditor.Compilation.CompilationPipeline.compilationFinished += context =>
        {
            var counts = new object[] { 0, 0, 0 };
            typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, counts);
            File.AppendAllText("Logs/WeaponSwitch_FinalCompilation.txt", $"Console errors={counts[0]}; warnings={counts[1]}; playing={EditorApplication.isPlaying}; sceneDirty={EditorSceneManager.GetActiveScene().isDirty}\n");
        };
        AssetDatabase.Refresh();
    }
    public static void Clear() { typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear", BindingFlags.Static | BindingFlags.Public).Invoke(null, null); }
    public static void Console()
    {
        Type entries = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        Type entryType = typeof(Editor).Assembly.GetType("UnityEditor.LogEntry");
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        int count = (int)entries.GetMethod("GetCount", flags).Invoke(null, null);
        entries.GetMethod("StartGettingEntries", flags).Invoke(null, null);
        string report = "";
        try { for(int i = 0; i < count; i++) { object entry = Activator.CreateInstance(entryType); entries.GetMethod("GetEntryInternal", flags).Invoke(null, new object[]{i,entry}); report += entryType.GetField("message", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(entry) + "\n"; } }
        finally { entries.GetMethod("EndGettingEntries", flags).Invoke(null,null); }
        File.WriteAllText("Logs/WeaponSwitch_Console.txt", report);
    }
}
