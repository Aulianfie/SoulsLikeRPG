using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// 仅在显式运行时测试真实 Agent；玩家位置和 AI 开关只在 Play Mode 改动。
[InitializeOnLoad]
public static class GiantGolemDungeonPlacementValidation
{
    const string Running = "Golem.Dungeon.Validation";
    static BossBrain boss;
    static PlayerStateMachine player;
    static Vector3 start;
    static double next, deadline;
    static int stage;
    static int errors, warnings;
    static GiantGolemDungeonPlacement.PlacementReport report;
    static string failure;
    static string LogDirectory => SessionState.GetString(Running + ".LogDirectory", "Logs/GolemPlacement");
    static GiantGolemDungeonPlacementValidation()
    {
        EditorApplication.playModeStateChanged += Changed; EditorApplication.update += Tick; Application.logMessageReceived += Log;
        if (SessionState.GetBool("Golem.Dungeon.FinalReload", false)) EditorApplication.delayCall += FinalSnapshot;
    }
    [MenuItem("Tools/SoulsLike RPG/Giant Golem/7 Validate Dungeon Navigation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty || !GiantGolemDungeonPlacement.IsDungeonScene(EditorSceneManager.GetActiveScene().path))
            throw new InvalidOperationException("请在已保存的地牢 Boss 场景 Edit Mode 验证。");
        Require(EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "场景仍有 Missing Script");
        bool main = EditorSceneManager.GetActiveScene().path == GiantGolemDungeonPlacement.MainScenePath;
        SessionState.SetString(Running + ".ReportPath", main ? GiantGolemDungeonPlacement.MainReportPath : "Docs/GiantGolem_DungeonPlacement.json");
        SessionState.SetString(Running + ".LogDirectory", main ? "Logs/GolemMainIntegration" : "Logs/GolemPlacement"); Directory.CreateDirectory(LogDirectory);
        string save = SaveService.SaveFilePath; SessionState.SetString(Running + ".Save", save); SessionState.SetBool(Running + ".SaveExists", File.Exists(save));
        if (File.Exists(save)) File.Copy(save, LogDirectory + "/OriginalSave.bin", true);
        SessionState.SetBool(Running + ".Completed", false);
        errors = warnings = 0; failure = null; var counts = ConsoleCounts();
        SessionState.SetString(Running + ".Failure", ""); SessionState.SetInt(Running + ".BaselineErrors", (int)counts[0]); SessionState.SetInt(Running + ".BaselineWarnings", (int)counts[1]);
        SessionState.SetBool(Running, true); SessionState.SetInt(Running + ".Errors", 0); SessionState.SetInt(Running + ".Warnings", 0); EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = JsonUtility.FromJson<GiantGolemDungeonPlacement.PlacementReport>(File.ReadAllText(SessionState.GetString(Running + ".ReportPath", "")));
            stage = 0; next = EditorApplication.timeSinceStartup + .5; deadline = next + 20;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            var counts = ConsoleCounts();
            int errorCount = Math.Max(SessionState.GetInt(Running + ".Errors", 0), (int)counts[0] - SessionState.GetInt(Running + ".BaselineErrors", 0));
            int warningCount = Math.Max(SessionState.GetInt(Running + ".Warnings", 0), (int)counts[1] - SessionState.GetInt(Running + ".BaselineWarnings", 0));
            string failed = SessionState.GetString(Running + ".Failure", "");
            if (!SessionState.GetBool(Running + ".Completed", false) && failed.Length == 0) failed = "验证未完成。";
            try
            {
                string save = SessionState.GetString(Running + ".Save", "");
                bool existed = SessionState.GetBool(Running + ".SaveExists", false);
                if (existed) File.Copy(LogDirectory + "/OriginalSave.bin", save, true); else if (File.Exists(save)) File.Delete(save);
                var restored = JsonUtility.FromJson<GiantGolemDungeonPlacement.PlacementReport>(File.ReadAllText(SessionState.GetString(Running + ".ReportPath", "")));
                restored.saveBytesRestored = existed ? File.ReadAllBytes(save).SequenceEqual(File.ReadAllBytes(LogDirectory + "/OriginalSave.bin")) : !File.Exists(save);
                File.WriteAllText(SessionState.GetString(Running + ".ReportPath", ""), JsonUtility.ToJson(restored, true));
                if (!restored.saveBytesRestored) failed += " 存档字节未恢复。";
            }
            catch (Exception e) { failed += " 存档恢复失败：" + e; }
            var text = "Runtime errors=" + errorCount + "; warnings=" + warningCount + "\n" + (failed.Length == 0 ? "All navigation checks passed" : failed);
            File.WriteAllText(LogDirectory + "/RuntimeValidation.txt", text);
            SessionState.SetBool(Running, false);
        }
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("导航验证超时");
            if (stage == 0)
            {
                boss = Object.FindObjectOfType<BossBrain>(); player = Object.FindObjectOfType<PlayerStateMachine>();
                boss.SetAutomatic(false); start = boss.transform.position;
                report.runtimeOnNavMesh = boss.GetComponent<NavMeshAgent>().isOnNavMesh;
                Require(report.runtimeOnNavMesh && Vector3.Distance(start, report.placed) < .5f, "Boss 实际出生点未落在 NavMesh 上");
                Require(Object.FindObjectsOfType<EnemyMotor>().All(e => e.IsOnNavMesh), "原有小怪导航发生回归");
                if (report.scene == GiantGolemDungeonPlacement.MainScenePath) CheckHud();
                if (report.scene == GiantGolemDungeonPlacement.MainScenePath)
                {
                    GiantGolemBossDebugWindow.TeleportPlayerToApproachPoint();
                    Require(Vector3.Distance(player.transform.position, report.approach + Vector3.up * .1f) < .2f, "调试窗口测试点传送失败");
                }
                else player.Motor.Teleport(report.approach + Vector3.up * .1f, Quaternion.LookRotation(start - report.approach, Vector3.up));
                player.InputReader.enabled = false;
                boss.enabled = false;
                Require(boss.GetComponent<EnemyMotor>().MoveTo(report.approach, 2.5f), "实际 MoveTo 请求失败");
                stage = 1; next = EditorApplication.timeSinceStartup + 1;
            }
            else if (stage == 1)
            {
                if (Vector3.Distance(start, boss.transform.position) < 1) return;
                report.runtimeChase = boss.GetComponent<NavMeshAgent>().hasPath && boss.GetComponent<NavMeshAgent>().pathStatus == NavMeshPathStatus.PathComplete;
                Require(report.runtimeChase, "Agent 实际移动后路径不完整");
                boss.GetComponent<EnemyMotor>().Stop(); boss.enabled = true;
                if (report.scene == GiantGolemDungeonPlacement.MainScenePath)
                {
                    var manager = Object.FindObjectOfType<CheckpointManager>();
                    Require(manager != null && ((ICheckpointResettable[])typeof(CheckpointManager).GetField("_enemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager)).Contains(boss), "检查点未收集 Boss");
                    manager.ResetWorld(); report.runtimeCheckpointReset = true;
                }
                else boss.ResetForCheckpoint();
                stage = 2; next = EditorApplication.timeSinceStartup + .2;
            }
            else
            {
                report.runtimeReset = Vector3.Distance(boss.transform.position, start) < .2f && boss.GetComponent<NavMeshAgent>().isOnNavMesh && boss.GetComponent<NavMeshAgent>().updatePosition;
                Require(report.runtimeReset, "回位后 Agent 与位置不一致");
                File.WriteAllText(SessionState.GetString(Running + ".ReportPath", ""), JsonUtility.ToJson(report, true));
                SessionState.SetBool(Running + ".Completed", true); EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e) { failure = e.ToString(); SessionState.SetString(Running + ".Failure", failure); File.WriteAllText(LogDirectory + "/RuntimeFailure.txt", failure); EditorApplication.isPlaying = false; }
    }
    static void CheckHud()
    {
        var weapon = Object.FindObjectOfType<WeaponSlotPresenter>(); var item = Object.FindObjectOfType<QuickItemPresenter>();
        Require(weapon != null && item != null, "新版武器/药瓶 HUD 缺失");
        var equipment = player.GetComponent<PlayerEquipment>(); var items = player.GetComponent<PlayerItemController>();
        Require(new SerializedObject(weapon).FindProperty("_equipment").objectReferenceValue == equipment && new SerializedObject(item).FindProperty("_items").objectReferenceValue == items, "HUD 未接入本场景玩家");
        var weaponIcon = (UnityEngine.UI.Image)typeof(WeaponSlotView).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon.GetComponent<WeaponSlotView>());
        report.runtimeWeaponHud = equipment.CurrentWeapon != null && weaponIcon.sprite == equipment.CurrentWeapon.Icon;
        Require(report.runtimeWeaponHud, "武器 HUD 未显示当前武器图标");
        var view = item.GetComponent<QuickItemView>();
        var itemIcon = (UnityEngine.UI.Image)typeof(QuickItemView).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        var count = (TMPro.TMP_Text)typeof(QuickItemView).GetField("_countText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        int previous = items.CurrentSlotIndex; Require(items.SlotCount == 2, "HP/MP 快捷槽丢失");
        for (int slot = 0; slot < 2; slot++)
        {
            Require(items.EquipItemSlot(slot) && itemIcon.sprite == items.CurrentItem.Data.Icon, "切瓶后图标未更新：" + slot);
            Require(count.text == items.CurrentItem.CurrentCharges + " / " + items.CurrentItem.MaxCharges, "切瓶后数量未更新：" + slot);
        }
        items.EquipItemSlot(previous); report.runtimeQuickItemHud = true;
    }
    static void Require(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }
    static void Log(string message, string trace, LogType type)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; SessionState.SetInt(Running + ".Errors", errors); }
        if (type == LogType.Warning) { warnings++; SessionState.SetInt(Running + ".Warnings", warnings); }
    }
    static object[] ConsoleCounts()
    {
        var counts = new object[] { 0, 0, 0 };
        typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, counts);
        return counts;
    }
    [Serializable] class FinalState { public string timestamp, scene; public int errors, warnings; public bool playing, dirty, temporaryBridgeRemoved; }
    static void FinalSnapshot()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += FinalSnapshot; return; }
        var counts = ConsoleCounts(); var scene = EditorSceneManager.GetActiveScene();
        string directory = scene.path == GiantGolemDungeonPlacement.MainScenePath ? "Logs/GolemMainIntegration" : "Logs/GolemPlacement"; Directory.CreateDirectory(directory);
        File.WriteAllText(directory + "/FinalEditorState.json", JsonUtility.ToJson(new FinalState { timestamp = DateTime.Now.ToString("O"), scene = scene.path,
            errors = (int)counts[0], warnings = (int)counts[1], dirty = scene.isDirty, playing = EditorApplication.isPlaying,
            temporaryBridgeRemoved = !File.Exists("Assets/_Game/Editor/GolemPlacementBridge.cs") && !File.Exists("Assets/_Game/Editor/GolemMainIntegrationBridge.cs") }, true));
        SessionState.SetBool("Golem.Dungeon.FinalReload", false);
    }
}
