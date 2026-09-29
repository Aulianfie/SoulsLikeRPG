using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DeathSoulDropValidation
{
    private const string Key = "Day8.DeathSoul.Validation.";
    private const string ReportPath = "Logs/DeathSoulDrop_Validation.json";
    private const string BackupPath = "Logs/DeathSoulDrop_OriginalSave.json";
    private const string ConsolePath = "Logs/DeathSoulDrop_RuntimeConsole.log";
    private static readonly List<string> Checks = new List<string>();
    private static PlayerStateMachine _player;
    private static PlayerSoulDrop _drops;
    private static SoulWallet _wallet;
    private static CheckpointSite _site;
    private static SoulDrop _previousDrop;
    private static Vector3 _deathPosition;
    private static Vector3 _groundedPosition;
    private static int _phase;
    private static int _frame;
    private static Keyboard _keyboard;
    private static double _deadline;

    [Serializable]
    private sealed class Result
    {
        public bool passed;
        public string[] checks;
        public string failure;
    }

    static DeathSoulDropValidation()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode;
        if (SessionState.GetBool(Key + "Running", false))
        {
            Application.logMessageReceived += RecordConsole;
            if (SessionState.GetInt(Key + "Cycle", 0) == 2 && !EditorApplication.isPlaying)
                EditorApplication.update += Restart;
        }
    }

    [MenuItem("Tools/SoulsLike RPG/Day8/Validate Death Soul Drop and Facing")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            scene.path != Day8Task123Builder.ScenePath)
            throw new InvalidOperationException("请在已保存的检查点场景、Edit Mode 中开始验证。");
        Directory.CreateDirectory("Logs");
        SessionState.SetString(Key + "Path", SaveService.SaveFilePath);
        SessionState.SetBool(Key + "Existed", File.Exists(SaveService.SaveFilePath));
        if (File.Exists(SaveService.SaveFilePath))
            File.WriteAllBytes(BackupPath, File.ReadAllBytes(SaveService.SaveFilePath));
        SessionState.SetBool(Key + "Running", true);
        SessionState.SetBool(Key + "Error", false);
        SessionState.SetInt(Key + "Cycle", 1);
        File.WriteAllText(ConsolePath, "");
        Checks.Clear();
        WriteResult(null);
        Application.logMessageReceived -= RecordConsole;
        Application.logMessageReceived += RecordConsole;
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Running", false))
            return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Checks.Clear();
            Checks.AddRange(JsonUtility.FromJson<Result>(File.ReadAllText(ReportPath)).checks);
            _phase = 0;
            _frame = Time.frameCount;
            _deadline = EditorApplication.timeSinceStartup + 75;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            if (SessionState.GetBool(Key + "Error", false) || SessionState.GetInt(Key + "Cycle", 0) == 3)
            {
                string path = SessionState.GetString(Key + "Path", "");
                if (SessionState.GetBool(Key + "Existed", false))
                    File.WriteAllBytes(path, File.ReadAllBytes(BackupPath));
                else if (File.Exists(path))
                    File.Delete(path);
                Application.logMessageReceived -= RecordConsole;
                SessionState.SetBool(Key + "Running", false);
                Result result = JsonUtility.FromJson<Result>(File.ReadAllText(ReportPath));
                result.checks = result.checks.Concat(new[] { "结束后恢复原始存档字节或原始不存在状态" }).ToArray();
                File.WriteAllText(ReportPath, JsonUtility.ToJson(result, true));
                Debug.Log($"[DeathSoul Validation] Passed={result.passed}; checks={result.checks.Length}");
            }
            else
            {
                SessionState.SetFloat(Key + "ResumeAfter", (float)EditorApplication.timeSinceStartup + 1f);
                EditorApplication.update -= Restart;
                EditorApplication.update += Restart;
            }
        }
    }

    private static void Restart()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "ResumeAfter", 0f))
            return;
        EditorApplication.update -= Restart;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount - _frame < 8)
            return;
        try
        {
            if (SessionState.GetBool(Key + "Error", false) || EditorApplication.timeSinceStartup > _deadline)
                throw new InvalidOperationException("运行出错或验证超时。");
            if (_player == null)
            {
                _player = Object.FindObjectOfType<PlayerStateMachine>();
                _drops = _player.GetComponent<PlayerSoulDrop>();
                _wallet = _player.GetComponent<SoulWallet>();
                _site = Object.FindObjectsOfType<CheckpointSite>().First(site => site.CanInteract);
            }
            if (SessionState.GetInt(Key + "Cycle", 0) == 2)
            {
                ValidateRestart();
                SessionState.SetInt(Key + "Cycle", 3);
                Complete(null);
                return;
            }
            switch (_phase)
            {
                case 0:
                    ValidateStorage();
                    _drops.RestoreFromSave(new GameSaveData(_player.gameObject.scene.name, ""));
                    _wallet.SetSouls(1000);
                    foreach (EnemyStateMachine enemy in Object.FindObjectsOfType<EnemyStateMachine>())
                        enemy.enabled = false;
                    _player.Motor.Teleport(_site.RespawnPoint.position, _site.RespawnPoint.rotation);
                    _player.Health.RestoreFull();
                    Next();
                    break;
                case 1:
                    if (!Ready()) return;
                    Vector3 facing = _site.transform.position - _player.transform.position;
                    facing.y = 0;
                    _player.transform.rotation = Quaternion.LookRotation(-facing.normalized);
                    Check(_player.TryBeginInteraction(_site, 3.5f), "背对赐福时也能开始交互");
                    Check(Vector3.Angle(_player.transform.forward, facing) < 0.01f,
                        "开始交互时先强制水平转向赐福点");
                    Check(_player.CurrentState == _player.InteractState && !Object.FindObjectOfType<ProgressionPresenter>().IsOpen,
                        "转向后进入交互状态，动画开始前没有提前执行休息");
                    Next();
                    break;
                case 2:
                    if (!Object.FindObjectOfType<ProgressionPresenter>().IsOpen) return;
                    Check(true, "交互动画完成后才打开赐福菜单");
                    Object.FindObjectOfType<ProgressionPresenter>().CloseMenu();
                    Quaternion rotation = _player.transform.rotation;
                    _player.Motor.FacePosition(_player.transform.position);
                    Check(Quaternion.Angle(rotation, _player.transform.rotation) < 0.01f,
                        "目标水平位置重合时转向不会产生无效旋转");
                    Kill(1000);
                    Check(_wallet.CurrentSouls == 0 && SoulText() == "0", "真实死亡立即清空钱包并刷新 HUD");
                    Check(_drops.CurrentDrop != null && _drops.CurrentDrop.Souls == 1000 &&
                        Vector3.Distance(_drops.CurrentDrop.transform.position, _deathPosition) < 1.6f,
                        "真实死亡在可达位置生成包含全部魂的实例");
                    Check(!_drops.CurrentDrop.CanInteract && !_drops.TryRecover(_drops.CurrentDrop),
                        "死亡期间不能取回魂");
                    _previousDrop = _drops.CurrentDrop;
                    Next();
                    break;
                case 3:
                    if (!Ready()) return;
                    Check(_drops.CurrentDrop == _previousDrop && _wallet.CurrentSouls == 0,
                        "真实死亡动画和淡出复活完成后掉魂仍保留，钱包仍为零");
                    GameSaveData saved = SaveService.Load();
                    Check(saved.hasSoulDrop && saved.droppedSouls == 1000 && saved.souls == 0,
                        "死亡帧末保存了零钱包与完整掉魂数据");
                    _player.Motor.Teleport(_drops.CurrentDrop.transform.position + Vector3.right * 0.15f, Quaternion.identity);
                    Next();
                    break;
                case 4:
                    if (!Ready()) return;
                    InteractionPromptUI prompt = Object.FindObjectOfType<InteractionPromptUI>();
                    Check(prompt.GetComponent<CanvasGroup>().alpha > 0 &&
                        prompt.GetComponentInChildren<Text>().text.Contains("取回遗失的魂"),
                        "进入真实掉魂触发器后出现 E 键拾取提示");
                    _keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E));
                    InputSystem.Update();
                    Next();
                    break;
                case 5:
                    InputSystem.RemoveDevice(_keyboard);
                    _keyboard = null;
                    Check(_drops.CurrentDrop == null && _wallet.CurrentSouls == 1000 && SoulText() == "1,000",
                        "真实 E 键拾取返还全部魂并移除掉落物");
                    Check(_player.CurrentState == _player.LocomotionState,
                        "拾取立即完成，未播放赐福休息动画");
                    Check(!_drops.TryRecover(_previousDrop), "重复拾取旧实例不会重复发钱");
                    saved = SaveService.Load();
                    Check(!saved.hasSoulDrop && saved.droppedSouls == 0 && saved.souls == 1000,
                        "拾取后的存档清除掉魂并恢复钱包");
                    Kill(600);
                    _previousDrop = _drops.CurrentDrop;
                    Next();
                    break;
                case 6:
                    if (!Ready()) return;
                    Kill(150);
                    Check(_previousDrop == null || !_previousDrop.gameObject.activeSelf,
                        "未取回第一份魂时再次死亡，旧掉魂立即失效");
                    Check(_drops.CurrentDrop != null && _drops.CurrentDrop.Souls == 150 && _wallet.CurrentSouls == 0,
                        "新死亡只掉落本次持有的 150，不累计上一份 600");
                    Check(Object.FindObjectsOfType<SoulDrop>().Length == 1,
                        "场景最多存在一份有效掉魂");
                    Next();
                    break;
                case 7:
                    if (!Ready()) return;
                    _previousDrop = _drops.CurrentDrop;
                    Kill(0);
                    Check(_drops.CurrentDrop == null && (_previousDrop == null || !_previousDrop.gameObject.activeSelf),
                        "零余额再次死亡也销毁上一份魂，不生成零金额掉落");
                    Next();
                    break;
                case 8:
                    if (!Ready()) return;
                    Check(!SaveService.Load().hasSoulDrop, "零余额二次死亡的永久丢失结果也会保存");
                    _groundedPosition = _player.Motor.LastGroundedPosition;
                    _player.Motor.Jump();
                    Next();
                    break;
                case 9:
                    if (_player.Motor.IsGrounded || _player.transform.position.y < _groundedPosition.y + 0.1f) return;
                    Kill(777);
                    Check(Vector3.Distance(_drops.CurrentDrop.transform.position, _groundedPosition) < 1.6f &&
                        _drops.CurrentDrop.transform.position.y < _groundedPosition.y + 0.5f,
                        "真实跳跃离地后死亡，魂落在最后的安全地面位置");
                    Next();
                    break;
                case 10:
                    if (!Ready()) return;
                    Check(_drops.CurrentDrop.Souls == 777, "新的死亡重新建立可恢复的魂");
                    saved = SaveService.Load();
                    SessionState.SetString(Key + "ExpectedDrop", JsonUtility.ToJson(saved.soulDropPosition));
                    SessionState.SetInt(Key + "Cycle", 2);
                    Complete(null);
                    break;
            }
        }
        catch (Exception exception) { Complete(exception.ToString()); }
    }

    private static bool Ready() => !_player.Health.IsDead && _player.InputReader.enabled &&
        _player.CurrentState == _player.LocomotionState && _player.Motor.IsGrounded;

    private static void Kill(int souls)
    {
        _wallet.SetSouls(souls);
        _deathPosition = _player.transform.position;
        _player.Health.Die();
    }

    private static void ValidateStorage()
    {
        string path = "Logs/DeathSoulDrop_SaveChecks.json";
        File.WriteAllText(path, "{\"version\":2,\"sceneName\":\"scene\",\"checkpointId\":\"grace\",\"souls\":234,\"level\":4,\"vigor\":2,\"endurance\":2,\"strength\":2}");
        GameSaveData data = SaveService.Load(path);
        Check(data.version == 3 && data.souls == 234 && data.level == 4 && !data.hasSoulDrop,
            "v2 存档迁移到 v3，保留原余额和成长，不凭空生成魂");
        data.hasSoulDrop = true;
        data.droppedSouls = 500;
        data.soulDropPosition = new Vector3(1, 2, 3);
        Check(SaveService.Save(data, path) && SaveService.Load(path).droppedSouls == 500 &&
            SaveService.Load(path).soulDropPosition == data.soulDropPosition,
            "v3 掉魂金额与位置精确保存和恢复");
        string original = File.ReadAllText(path);
        data.soulDropPosition = new Vector3(float.NaN, 0, 0);
        Check(!SaveService.Save(data, path) && File.ReadAllText(path) == original,
            "拒绝无效掉魂位置且不破坏原文件");
    }

    private static void ValidateRestart()
    {
        Check(_wallet.CurrentSouls == 0 && _drops.CurrentDrop != null && _drops.CurrentDrop.Souls == 777,
            "退出并重新进入 Play Mode 后恢复零钱包和 777 掉魂");
        Vector3 expected = JsonUtility.FromJson<Vector3>(SessionState.GetString(Key + "ExpectedDrop", ""));
        Check(Vector3.Distance(_drops.CurrentDrop.transform.position, expected) < 0.001f,
            "重启后掉魂位置保持不变");
        _wallet.SetSouls(50);
        _player.Motor.Teleport(_drops.CurrentDrop.transform.position, Quaternion.identity);
        SoulDrop drop = _drops.CurrentDrop;
        bool reentered = false;
        Action<int> listener = value => reentered = _drops.TryRecover(drop);
        _wallet.SoulsChanged += listener;
        Check(_drops.TryRecover(drop) && _wallet.CurrentSouls == 827 && !reentered,
            "取回重载的魂可加到新收入上，扣除拾取资格后事件重入不会重复发钱");
        _wallet.SoulsChanged -= listener;
        Check(_drops.CurrentDrop == null && !drop.CanInteract,
            "重启后的拾取也正确清理可交互资格");
    }

    private static string SoulText() => Object.FindObjectOfType<SoulHUDPresenter>().GetComponentInChildren<TMP_Text>().text;
    private static void Next() { _phase++; _frame = Time.frameCount; }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Checks.Add(description);
    }
    private static void WriteResult(string failure)
    {
        File.WriteAllText(ReportPath, JsonUtility.ToJson(new Result
        {
            passed = failure == null && !SessionState.GetBool(Key + "Error", false) &&
                SessionState.GetInt(Key + "Cycle", 0) == 3,
            checks = Checks.ToArray(), failure = failure ?? ""
        }, true));
    }
    private static void Complete(string failure)
    {
        EditorApplication.update -= Tick;
        if (_keyboard != null) { InputSystem.RemoveDevice(_keyboard); _keyboard = null; }
        Object.FindObjectOfType<ProgressionPresenter>()?.CloseMenu();
        if (failure != null) SessionState.SetBool(Key + "Error", true);
        WriteResult(failure);
        EditorApplication.isPlaying = false;
        if (failure != null) Debug.LogError("[DeathSoul Validation] " + failure);
    }
    private static void RecordConsole(string message, string stack, LogType type)
    {
        if (type == LogType.Log || message.Contains("MCP-FOR-UNITY")) return;
        File.AppendAllText(ConsolePath, $"[{type}] {message}\n{stack}\n");
        if (type != LogType.Warning) SessionState.SetBool(Key + "Error", true);
    }
}
