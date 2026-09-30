using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 定向回归；测试存档会在退出 Play Mode 后按原始字节恢复。
[InitializeOnLoad]
public static class InteractionSaveRegressionValidation
{
    private const string Key = "InteractionSaveRegression.";
    private const string Backup = "Logs/InteractionSave_OriginalSave.json";
    private const string Report = "Logs/InteractionSave_Regression.json";
    private static readonly List<string> Passed = new List<string>();
    private static readonly List<string> Failed = new List<string>();
    private static byte[] _foreignSave;
    private static bool _tested;

    [Serializable]
    private sealed class Result
    {
        public bool passed;
        public string[] checks;
        public string[] failures;
    }

    static InteractionSaveRegressionValidation()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode;
    }

    [MenuItem("Tools/SoulsLike RPG/Day8/Validate Interaction and Scene Save Regression")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            scene.path != Day8Task123Builder.ScenePath)
            throw new InvalidOperationException("请在已保存的检查点场景、Edit Mode 运行验证。");
        Directory.CreateDirectory("Logs");
        SessionState.SetString(Key + "Path", SaveService.SaveFilePath);
        SessionState.SetBool(Key + "Existed", File.Exists(SaveService.SaveFilePath));
        if (File.Exists(SaveService.SaveFilePath))
            File.WriteAllBytes(Backup, File.ReadAllBytes(SaveService.SaveFilePath));
        WriteForeignSave();
        SessionState.SetBool(Key + "Running", true);
        EditorApplication.isPlaying = true;
    }

    private static void WriteForeignSave()
    {
        var save = new GameSaveData("Regression_OtherScene", "other_checkpoint")
        {
            souls = 321, level = 4, vigor = 2, endurance = 2, strength = 2,
            hasSoulDrop = true, droppedSouls = 456,
            soulDropPosition = new Vector3(12f, 3f, 4f)
        };
        if (!SaveService.Save(save))
            throw new InvalidOperationException("无法写入测试存档。");
        _foreignSave = File.ReadAllBytes(SaveService.SaveFilePath);
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Running", false))
            return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Passed.Clear();
            Failed.Clear();
            _tested = false;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            string path = SessionState.GetString(Key + "Path", "");
            if (SessionState.GetBool(Key + "Existed", false))
                File.WriteAllBytes(path, File.ReadAllBytes(Backup));
            else if (File.Exists(path))
                File.Delete(path);
            SessionState.SetBool(Key + "Running", false);
            File.WriteAllText(Report, JsonUtility.ToJson(new Result
            {
                passed = Failed.Count == 0,
                checks = Passed.ToArray(), failures = Failed.ToArray()
            }, true));
            Debug.Log($"[Interaction/Save Regression] Passed={Failed.Count == 0}; " +
                $"checks={Passed.Count}; failures={Failed.Count}; {Report}");
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || _tested || Time.frameCount < 10)
            return;
        _tested = true;
        try
        {
            PlayerStateMachine player = Object.FindObjectOfType<PlayerStateMachine>();
            CheckpointManager manager = Object.FindObjectOfType<CheckpointManager>();
            CheckpointSite site = Object.FindObjectOfType<CheckpointSite>();
            SoulWallet wallet = player.GetComponent<SoulWallet>();
            PlayerProgression progression = player.GetComponent<PlayerProgression>();
            // 从备份的预期 DTO 重建字节，避免域重载丢失静态变量。
            var expected = new GameSaveData("Regression_OtherScene", "other_checkpoint")
            {
                souls = 321, level = 4, vigor = 2, endurance = 2, strength = 2,
                hasSoulDrop = true, droppedSouls = 456,
                soulDropPosition = new Vector3(12f, 3f, 4f)
            };
            string expectedPath = "Logs/InteractionSave_Expected.json";
            SaveService.Save(expected, expectedPath);
            _foreignSave = File.ReadAllBytes(expectedPath);

            Check(ForeignUnchanged(), "异场景存档在 Start 与多帧 LateUpdate 后逐字节保留");
            Check(wallet.CurrentSouls == 1000 && progression.Level == 1 &&
                player.GetComponent<PlayerSoulDrop>().CurrentDrop == null && manager.CurrentCheckpoint == null,
                "异场景的钱包、成长、掉魂、赐福没有应用到当前场景");
            Check(!manager.SaveCurrentProgression(), "没有新进度时直接保存被拒绝");
            Call(manager, "OnApplicationPause", true);
            Call(manager, "OnApplicationQuit");
            Call(manager, "OnDisable");
            Call(manager, "OnEnable");
            Check(ForeignUnchanged(), "暂停、退出和禁用不覆盖异场景存档");
            // 模拟成长 Start 排在管理器 Start 之后的另一种合法顺序。
            Call(progression, "Start");
            Call(manager, "LateUpdate");
            Check(ForeignUnchanged(), "成长初始化通知不会被误判为玩家新进度");

            ValidateInteraction(player, site);
            wallet.AddSouls(17);
            Check(manager.SaveCurrentProgression() && SaveService.Load().sceneName == manager.gameObject.scene.name &&
                SaveService.Load().souls == 1017, "明确新增 Soul 后可以保存当前场景进度");

            WriteForeignSave();
            Call(manager, "RestoreCheckpointFromSave");
            Call(manager, "Start");
            Check(progression.TryUpgrade(StatType.Vigor) && manager.SaveCurrentProgression() &&
                SaveService.Load().level == 2 && SaveService.Load().vigor == 2,
                "明确升级后保存完整钱包和成长数据");

            WriteForeignSave();
            Call(manager, "RestoreCheckpointFromSave");
            Call(manager, "Start");
            progression.SetProgression(3, 3, 1, 1);
            Check(manager.SaveCurrentProgression() && SaveService.Load().level == 3 &&
                SaveService.Load().vigor == 3, "仅基础成长变化也允许保存，不依赖钱包事件");

            WriteForeignSave();
            Call(manager, "RestoreCheckpointFromSave");
            Call(manager, "Start");
            Check(manager.ActivateCheckpoint(site) && SaveService.Load().checkpointId == site.CheckpointId &&
                SaveService.Load().sceneName == manager.gameObject.scene.name,
                "成功赐福交互允许保存当前场景");
        }
        catch (Exception exception) { Failed.Add(exception.ToString()); }
        finally { EditorApplication.isPlaying = false; }
    }

    private static void ValidateInteraction(PlayerStateMachine player, CheckpointSite site)
    {
        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
        var nearby = (List<MonoBehaviour>)Field(interactor, "_nearby").GetValue(interactor);
        float distance = interactor.MaxInteractionDistance;
        Vector3 position = player.transform.position;
        Quaternion rotation = player.transform.rotation;
        Collider trigger = site.GetComponentsInChildren<Collider>().First(collider => collider.isTrigger);
        try
        {
            nearby.Clear();
            // 直接调用实际 Trigger 入口与 Update，隔离距离筛选造成的缓存失效。
            Call(interactor, "OnTriggerEnter", trigger);
            Check(nearby.Contains(site), "Trigger 入口确实收集到赐福目标");
            Field(interactor, "_maxInteractionDistance").SetValue(interactor, 0.1f);
            player.InputReader.ClearPendingActions();
            player.Motor.Teleport(site.transform.position + Vector3.right, rotation);
            Call(interactor, "Update");
            Check(nearby.Contains(site), "暂时超过交互距离仍保留 Trigger 内目标");
            player.Motor.Teleport(site.transform.position + Vector3.right * 0.05f, rotation);
            Call(interactor, "Update");
            Check(ReferenceEquals(Field(interactor, "_shownTarget").GetValue(interactor), site),
                "重新靠近恢复提示，无需再次 OnTriggerEnter");
            Call(interactor, "OnTriggerExit", trigger);
            Check(!nearby.Contains(site), "真正离开 Trigger 后移除目标");
            Call(interactor, "OnTriggerEnter", trigger);
            site.enabled = false;
            Call(interactor, "Update");
            Check(!nearby.Contains(site), "失效目标仍从缓存移除");
        }
        finally
        {
            site.enabled = true;
            nearby.Clear();
            Field(interactor, "_maxInteractionDistance").SetValue(interactor, distance);
            player.Motor.Teleport(position, rotation);
            Call(interactor, "Update");
        }
    }

    private static bool ForeignUnchanged() =>
        File.ReadAllBytes(SaveService.SaveFilePath).SequenceEqual(_foreignSave);
    private static void Check(bool condition, string description) =>
        (condition ? Passed : Failed).Add(description);
    private static FieldInfo Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Call(object target, string name, params object[] arguments) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, arguments);
}
