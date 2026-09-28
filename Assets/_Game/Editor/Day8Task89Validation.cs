using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 跨 Play Mode 验证自动保存、重启、旧档迁移与坏档保护，结束时还原用户存档。
[InitializeOnLoad]
public static class Day8Task89Validation
{
    private const string Key = "Day8.Task89.Validation.";
    private const string ReportPath = "Logs/Day8_Task8-9_Validation.json";
    private const string BackupPath = "Logs/Day8_Task8-9_OriginalSave.json";
    private const string ConsolePath = "Logs/Day8_Task8-9_RuntimeConsole.log";
    private static readonly List<string> Checks = new List<string>();
    private static int _frames;
    private static int _phase;
    private static int _expectedSouls;
    private static int _gameFrame;
    private static double _deadline;
    private static bool _expectStorageWarning;
    private static bool _runtimeError;

    [Serializable]
    private sealed class Result
    {
        public bool passed;
        public string scene;
        public string[] checks;
        public string failure;
    }

    static Day8Task89Validation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Key + "Running", false))
        {
            Application.logMessageReceived += RecordConsole;
            if (SessionState.GetInt(Key + "Cycle", 0) > 1 && !EditorApplication.isPlaying)
                EditorApplication.update += ResumePlay;
        }
    }

    [MenuItem("Tools/SoulsLike RPG/Day8/Validate Task8-9 (Save and Restart)")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            scene.path != Day8Task123Builder.ScenePath)
            throw new InvalidOperationException("请在已保存的检查点场景、Edit Mode 中运行验证。");

        Checks.Clear();
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ConsolePath, "");
        SessionState.SetString(Key + "SavePath", SaveService.SaveFilePath);
        SessionState.SetBool(Key + "SaveExists", File.Exists(SaveService.SaveFilePath));
        if (File.Exists(SaveService.SaveFilePath))
            File.WriteAllBytes(BackupPath, File.ReadAllBytes(SaveService.SaveFilePath));
        SessionState.SetBool(Key + "Running", true);
        SessionState.SetInt(Key + "Cycle", 1);
        SessionState.SetBool(Key + "Error", false);
        Application.logMessageReceived -= RecordConsole;
        Application.logMessageReceived += RecordConsole;
        try
        {
            ValidateStorage();
            ValidateAssets();
            // 只删除已经备份的明确存档文件，用于证明无存档新游戏的默认状态。
            if (File.Exists(SaveService.SaveFilePath))
                File.Delete(SaveService.SaveFilePath);
            WriteReport(null);
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void ValidateStorage()
    {
        string path = Path.GetFullPath("Logs/Day8_Task8-9_StorageChecks/save.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path))
            File.Delete(path);
        Check(SaveService.Load(path) == null, "不存在的存档返回空结果");
        var data = new GameSaveData("03_AncientDungeon_Checkpoint", "")
            { souls = 0, level = 4, vigor = 2, endurance = 2, strength = 2 };
        Check(SaveService.Save(data, path), "尚未激活赐福时可以创建 v2 成长存档");
        GameSaveData loaded = SaveService.Load(path);
        Check(loaded.version == 2 && loaded.souls == 0 && loaded.level == 4 &&
            loaded.vigor == 2 && loaded.endurance == 2 && loaded.strength == 2,
            "v2 零余额与全部基础成长字段精确往返");
        string json = File.ReadAllText(path);
        Check(!json.Contains("MaxHealth") && !json.Contains("MaxStamina") &&
            !json.Contains("DamageMultiplier"), "存档不包含派生战斗属性");
        data.souls = int.MaxValue;
        Check(SaveService.Save(data, path) && SaveService.Load(path).souls == int.MaxValue &&
            !File.Exists(path + ".tmp"), "原子替换已有文件，整数上限不溢出且不残留临时文件");
        string before = File.ReadAllText(path);
        data.level = 0;
        Check(!SaveService.Save(data, path) && File.ReadAllText(path) == before,
            "拒绝无效成长数据，保留原存档内容");

        File.WriteAllText(path,
            "{\"version\":1,\"sceneName\":\"03_AncientDungeon_Checkpoint\",\"checkpointId\":\"checkpoint_dungeon_01\"}");
        loaded = SaveService.Load(path);
        Check(loaded != null && loaded.version == 2 && loaded.souls == 1000 &&
            loaded.level == 1 && loaded.vigor == 1 && loaded.endurance == 1 && loaded.strength == 1 &&
            loaded.checkpointId == "checkpoint_dungeon_01", "v1 赐福迁移为 v2：1000 Soul 与 1 级属性");
        Check(SaveService.Save(loaded, path) && File.ReadAllText(path).Contains("\"version\": 2"),
            "迁移后的旧档可持久化为 v2");
        _expectStorageWarning = true;
        try
        {
            foreach (string invalid in new[]
            {
                "{bad json", "{}", "{\"version\":2,\"sceneName\":\"scene\"}",
                "{\"version\":3,\"sceneName\":\"scene\",\"checkpointId\":\"grace\"}",
                "{\"version\":2,\"sceneName\":\"scene\",\"souls\":-1,\"level\":1,\"vigor\":1,\"endurance\":1,\"strength\":1}"
            })
            {
                File.WriteAllText(path, invalid);
                Check(SaveService.Load(path) == null && File.ReadAllText(path) == invalid,
                    "损坏、缺字段、未来版本或负余额的存档被拒绝且不改写原文：" + invalid);
            }
        }
        finally { _expectStorageWarning = false; }
    }

    private static void ValidateAssets()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Prefabs/Characters/Player_Day1.prefab");
        Check(player.GetComponent<SoulWallet>().CurrentSouls == 1000, "玩家 Prefab 初始金币为 1000");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Day8Task89Builder.SoulDropPath);
        SoulDrop drop = prefab.GetComponent<SoulDrop>();
        Check(drop != null && drop.VisualRoot != null && drop.VisualRoot.parent == prefab.transform,
            "SoulDrop 根节点脚本绑定独立 VisualRoot");
        Check(drop.VisualRoot.GetComponentsInChildren<MeshRenderer>().Length > 0 &&
            drop.VisualRoot.GetComponentsInChildren<Collider>().Length == 0,
            "可替换模型位于 VisualRoot，模型不持有碰撞器");
        SphereCollider trigger = prefab.transform.Find("Collider").GetComponent<SphereCollider>();
        Check(trigger != null && trigger.isTrigger, "SoulDrop 碰撞器独立于视觉节点");
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                throw new MissingComponentException(child.name + " 存在 Missing Script。");
        Check(true, "实际检查点场景没有 Missing Script");
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Running", false))
            return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Checks.Clear();
            Checks.AddRange(JsonUtility.FromJson<Result>(File.ReadAllText(ReportPath)).checks);
            _frames = _phase = 0;
            _runtimeError = false;
            _deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            int cycle = SessionState.GetInt(Key + "Cycle", 0);
            if (SessionState.GetBool(Key + "Error", false) || cycle == 5)
                RestoreOriginalSave();
            else
            {
                if (cycle == 1)
                    SessionState.SetInt(Key + "Cycle", 2);
                else if (cycle == 3)
                {
                    var legacy = new GameSaveData("03_AncientDungeon_Checkpoint",
                        SessionState.GetString(Key + "CheckpointId", ""));
                    File.WriteAllText(SaveService.SaveFilePath, "{\"version\":1,\"sceneName\":\"" +
                        legacy.sceneName + "\",\"checkpointId\":\"" + legacy.checkpointId + "\"}");
                }
                else if (cycle == 4)
                    File.WriteAllText(SaveService.SaveFilePath, FutureSave);
                SessionState.SetFloat(Key + "ResumeAfter", (float)EditorApplication.timeSinceStartup + 1f);
                EditorApplication.update -= ResumePlay;
                EditorApplication.update += ResumePlay;
            }
        }
    }

    private const string FutureSave = "{\"version\":99,\"sceneName\":\"03_AncientDungeon_Checkpoint\",\"checkpointId\":\"future_grace\"}";

    private static void ResumePlay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "ResumeAfter", 0f))
            return;
        EditorApplication.update -= ResumePlay;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || ++_frames < 10 || Time.frameCount == _gameFrame)
            return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline || _runtimeError)
                throw new InvalidOperationException("运行验证超时或发生游戏错误。");
            var player = Object.FindObjectOfType<PlayerHealth>();
            var wallet = player.GetComponent<SoulWallet>();
            var progression = player.GetComponent<PlayerProgression>();
            var manager = Object.FindObjectOfType<CheckpointManager>();
            int cycle = SessionState.GetInt(Key + "Cycle", 0);
            if (cycle == 2)
            {
                ValidateRestart(wallet, progression, manager);
                SessionState.SetInt(Key + "Cycle", 3);
                WriteReport(null);
                EditorApplication.isPlaying = false;
                return;
            }
            if (cycle == 3)
            {
                Check(wallet.CurrentSouls == 1000 && SoulText() == "1,000" && progression.Level == 1 &&
                    progression.Vigor == 1 && progression.Endurance == 1 && progression.Strength == 1,
                    "真实 v1 存档启动时迁移为 1000 金币及初始成长，HUD 正确恢复");
                Check(manager.CurrentCheckpoint.CheckpointId == SessionState.GetString(Key + "CheckpointId", "") &&
                    SaveService.Load().version == 2, "旧版赐福在实际启动中恢复，并自动持久化为 v2");
                SessionState.SetInt(Key + "Cycle", 4);
                WriteReport(null);
                EditorApplication.isPlaying = false;
                return;
            }
            if (cycle == 4)
            {
                if (_phase == 0)
                {
                    Check(wallet.CurrentSouls == 1000 && manager.CurrentCheckpoint == null,
                        "不支持的未来存档不会应用到角色和赐福");
                    wallet.AddSouls(10);
                    Check(!manager.SaveCurrentProgression(), "未来版本文件存在时拒绝自动保存覆盖");
                    NextPhase();
                }
                else
                {
                    Check(File.ReadAllText(SaveService.SaveFilePath) == FutureSave,
                        "金币变化经过帧末后，未来版本文件仍逐字保持原样");
                    SessionState.SetInt(Key + "Cycle", 5);
                    WriteReport(null);
                    EditorApplication.isPlaying = false;
                }
                return;
            }

            if (_phase == 0)
            {
                Check(wallet.CurrentSouls == 1000 && SoulText() == "1,000",
                    "无存档的新游戏钱包与 HUD 均为 1000");
                Check(progression.Level == 1 && progression.Vigor == 1 &&
                    progression.Endurance == 1 && progression.Strength == 1,
                    "无存档的新游戏成长属性为 1");
                int events = 0;
                Action<int> listener = value => events++;
                wallet.SoulsChanged += listener;
                wallet.SetSouls(700);
                wallet.SetSouls(700);
                Check(wallet.CurrentSouls == 700 && SoulText() == "700" && events == 1,
                    "恢复钱包精确设置余额并只在变化时通知 HUD");
                bool rejected = false;
                try { wallet.SetSouls(-1); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected && wallet.CurrentSouls == 700, "钱包拒绝负读档余额");
                wallet.SetSouls(1000);
                wallet.SoulsChanged -= listener;

                EnemyReward reward = Object.FindObjectsOfType<EnemyReward>().First();
                _expectedSouls = 1000 + reward.SoulReward;
                reward.GetComponent<EnemyHealth>().TakeDamage(new DamageInfo { Damage = int.MaxValue });
                Check(wallet.CurrentSouls == _expectedSouls && SoulText() == _expectedSouls.ToString("N0"),
                    "真实敌人死亡奖励增加金币并立即刷新 HUD");
                NextPhase();
            }
            else if (_phase == 1)
            {
                GameSaveData saved = SaveService.Load();
                Check(saved.souls == _expectedSouls && saved.level == 1 &&
                    string.IsNullOrEmpty(saved.checkpointId), "未休息时的击杀收入也会在帧末自动保存");
                CheckpointSite site = Object.FindObjectsOfType<CheckpointSite>().First(candidate => candidate.CanInteract);
                site.Interact();
                Check(manager.CurrentCheckpoint == site && Object.FindObjectOfType<ProgressionPresenter>().IsOpen,
                    "赐福激活与菜单流程保持正常");
                foreach (StatType stat in new[] { StatType.Vigor, StatType.Endurance, StatType.Strength })
                {
                    int cost = progression.UpgradeCost;
                    Check(progression.TryUpgrade(stat), "实际升级 " + stat + " 成功");
                    _expectedSouls -= cost;
                }
                Check(SaveService.Load().level == 1, "升级扣魂回调没有写入半完成的成长事务");
                NextPhase();
            }
            else if (_phase == 2)
            {
                GameSaveData saved = SaveService.Load();
                Check(saved.souls == _expectedSouls && saved.level == 4 && saved.vigor == 2 &&
                    saved.endurance == 2 && saved.strength == 2,
                    "暂停菜单中三次升级在帧末保存最终余额和完整成长数据");
                Check(saved.checkpointId == manager.CurrentCheckpoint.CheckpointId,
                    "自动保存成长时保留当前赐福 ID");
                SessionState.SetInt(Key + "ExpectedSouls", _expectedSouls);
                SessionState.SetString(Key + "CheckpointId", saved.checkpointId);
                Object.FindObjectOfType<ProgressionPresenter>().CloseMenu();
                WriteReport(null);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void NextPhase()
    {
        _phase++;
        _gameFrame = Time.frameCount;
        _frames = 0;
    }

    private static void ValidateRestart(SoulWallet wallet, PlayerProgression progression, CheckpointManager manager)
    {
        int souls = SessionState.GetInt(Key + "ExpectedSouls", -1);
        Check(wallet.CurrentSouls == souls && SoulText() == souls.ToString("N0"),
            "退出并重新进入 Play Mode 后钱包与 HUD 恢复已保存余额");
        Check(progression.Level == 4 && progression.Vigor == 2 &&
            progression.Endurance == 2 && progression.Strength == 2,
            "重启后恢复等级、Vigor、Endurance 和 Strength");
        var config = AssetDatabase.LoadAssetAtPath<PlayerProgressionConfig>(Day8Task123Builder.ConfigPath);
        Check(progression.GetComponent<PlayerHealth>().MaxHealth == config.CalculateMaxHealth(2) &&
            Mathf.Approximately(progression.GetComponent<PlayerStamina>().MaxStamina, config.CalculateMaxStamina(2)) &&
            Mathf.Approximately(progression.DamageMultiplier, config.CalculateDamageMultiplier(2)),
            "读档后根据当前成长 SO 重新计算 HP、体力和攻击倍率");
        Check(manager.CurrentCheckpoint.CheckpointId == SessionState.GetString(Key + "CheckpointId", "") &&
            Vector3.Distance(wallet.transform.position, manager.CurrentRespawnPoint.position) < 1f,
            "重启后恢复赐福并从对应出生点出现");

        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Day8Task89Builder.SoulDropPath));
        try
        {
            SoulDrop drop = instance.GetComponent<SoulDrop>();
            for (int i = drop.VisualRoot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(drop.VisualRoot.GetChild(i).gameObject);
            new GameObject("ReplacementModel").transform.SetParent(drop.VisualRoot, false);
            Check(drop.VisualRoot != null && instance.GetComponentInChildren<SphereCollider>() != null &&
                wallet.CurrentSouls == souls, "替换魂模型时视觉根与碰撞器仍有效，金币不受占位物影响");
        }
        finally { Object.DestroyImmediate(instance); }
    }

    private static string SoulText() => Object.FindObjectOfType<SoulHUDPresenter>()
        .GetComponentInChildren<TMP_Text>().text;

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
        Checks.Add(description);
    }

    private static void WriteReport(string failure)
    {
        File.WriteAllText(ReportPath, JsonUtility.ToJson(new Result
        {
            passed = failure == null && !SessionState.GetBool(Key + "Error", false) &&
                SessionState.GetInt(Key + "Cycle", 0) == 5,
            scene = Day8Task123Builder.ScenePath, checks = Checks.ToArray(), failure = failure ?? ""
        }, true));
    }

    private static void Fail(Exception exception)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key + "Error", true);
        WriteReport(exception.ToString());
        if (EditorApplication.isPlaying)
        {
            Object.FindObjectOfType<ProgressionPresenter>()?.CloseMenu();
            EditorApplication.isPlaying = false;
        }
        else RestoreOriginalSave();
        Debug.LogError("[Day8 Task8–9 Validation] " + exception);
    }

    private static void RestoreOriginalSave()
    {
        string path = SessionState.GetString(Key + "SavePath", "");
        bool existed = SessionState.GetBool(Key + "SaveExists", false);
        if (existed)
            File.WriteAllBytes(path, File.ReadAllBytes(BackupPath));
        else if (File.Exists(path))
            File.Delete(path);
        Application.logMessageReceived -= RecordConsole;
        SessionState.SetBool(Key + "Running", false);
        var result = JsonUtility.FromJson<Result>(File.ReadAllText(ReportPath));
        result.checks = result.checks.Concat(new[] { "验证结束后恢复原始存档字节或原始不存在状态" }).ToArray();
        File.WriteAllText(ReportPath, JsonUtility.ToJson(result, true));
        Debug.Log($"[Day8 Task8–9 Validation] Passed={result.passed}, checks={result.checks.Length}; {ReportPath}");
    }

    private static void RecordConsole(string message, string stack, LogType type)
    {
        if (type == LogType.Log || message.Contains("MCP-FOR-UNITY") || _expectStorageWarning)
            return;
        if (type == LogType.Warning && SessionState.GetInt(Key + "Cycle", 0) == 4 &&
            message == "Checkpoint save is incomplete or uses an unsupported version.")
            return; // 注入未来版本时预期的保护提示。
        File.AppendAllText(ConsolePath, $"[{type}] {message}\n{stack}\n");
        if (type != LogType.Warning)
        {
            _runtimeError = true;
            SessionState.SetBool(Key + "Error", true);
        }
    }
}
