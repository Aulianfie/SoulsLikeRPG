using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Day8Task4567Validation
{
    private const string RunningKey = "Day8.Task4567.Validation.Running";
    private const string SavePathKey = "Day8.Task4567.Validation.SavePath";
    private const string SaveExistsKey = "Day8.Task4567.Validation.SaveExists";
    private const string ReportPath = "Logs/Day8_Task4-7_Validation.json";
    private const string SaveBackupPath = "Logs/Day8_Task4-7_OriginalSave.json";
    private const string ConsolePath = "Logs/Day8_Task4-7_RuntimeConsole.log";
    private static readonly List<string> Passed = new List<string>();
    private static int _phase;
    private static int _frames;
    private static bool _failed;
    private static double _deadline;
    private static PlayerStateMachine _player;
    private static CheckpointSite _site;
    private static ProgressionPresenter _presenter;
    private static Keyboard _keyboard;

    [Serializable]
    private sealed class Result
    {
        public string scene;
        public bool passed;
        public string[] checks;
        public string failure;
    }

    static Day8Task4567Validation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(RunningKey, false))
            Application.logMessageReceived += RecordConsole;
    }

    [MenuItem("Tools/SoulsLike RPG/Day8/Validate Task4-7 (Play Mode)")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            scene.path != Day8Task123Builder.ScenePath)
            throw new InvalidOperationException("请在已保存的检查点场景、Edit Mode 中运行验证。");

        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                throw new MissingComponentException($"{child.name} 存在 Missing Script。");
        }

        Directory.CreateDirectory("Logs");
        string path = SaveService.SaveFilePath;
        SessionState.SetString(SavePathKey, path);
        SessionState.SetBool(SaveExistsKey, File.Exists(path));
        if (File.Exists(path))
            File.WriteAllBytes(SaveBackupPath, File.ReadAllBytes(path));
        File.WriteAllText(ConsolePath, "");
        SessionState.SetBool(RunningKey, true);
        Application.logMessageReceived -= RecordConsole;
        Application.logMessageReceived += RecordConsole;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Passed.Clear();
            _failed = false;
            _phase = 0;
            _frames = 0;
            _deadline = EditorApplication.timeSinceStartup + 35;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= RecordConsole;
            SessionState.SetBool(RunningKey, false);
            RestoreSave();
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || ++_frames < 10)
            return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline)
                throw new TimeoutException("实际赐福交互未在 35 秒内完成。");
            if (_phase == 0)
            {
                _player = Object.FindObjectOfType<PlayerStateMachine>();
                _site = Object.FindObjectsOfType<CheckpointSite>().First(site => site.CanInteract);
                _presenter = Object.FindObjectOfType<ProgressionPresenter>();
                Check(!_presenter.IsOpen && !_presenter.GetComponent<CanvasGroup>().blocksRaycasts,
                    "启动时 Grace UI 隐藏且不阻挡游戏点击");
                ValidateCosts();
                ValidateUpgrades();
                _player.Motor.Teleport(_site.RespawnPoint.position, _site.RespawnPoint.rotation);
                _player.Health.RestoreFull();
                _player.Health.TakeDamage(new DamageInfo { Damage = 25 });
                _player.Stamina.Consume(30f);
                _player.Respawn();
                Object.FindObjectsOfType<EnemyHealth>().First().TakeDamage(new DamageInfo { Damage = 1 });
                _phase = 1;
                _frames = 0;
            }
            else if (_phase == 1)
            {
                if (!_player.Motor.IsGrounded)
                    return;
                Check(_player.TryBeginInteraction(_site, 3.5f), "真实玩家 FSM 进入赐福交互动画");
                Check(!_presenter.IsOpen, "交互动画开始时尚未打开菜单");
                _phase = 2;
                _frames = 0;
            }
            else if (_phase == 2)
            {
                if (!_presenter.IsOpen)
                    return;
                ValidateMenus();
                Complete(null);
            }
        }
        catch (Exception exception)
        {
            Complete(exception.ToString());
        }
    }

    private static void ValidateCosts()
    {
        PlayerProgressionConfig original = AssetDatabase.LoadAssetAtPath<PlayerProgressionConfig>(
            Day8Task123Builder.ConfigPath);
        int originalCost = original.CalculateUpgradeCost(10);
        PlayerProgressionConfig defaults = ScriptableObject.CreateInstance<PlayerProgressionConfig>();
        Check(defaults.CalculateUpgradeCost(1) == 150 && defaults.CalculateUpgradeCost(10) == 600,
            "默认折线：1 级 150 Soul、10 级 600 Soul");
        Check(defaults.CalculateUpgradeCost(101) == 5150, "超出最后节点时按末段斜率延续费用");
        PlayerProgressionConfig copy = Object.Instantiate(defaults);
        try
        {
            SerializedObject serialized = new SerializedObject(copy);
            serialized.FindProperty("_upgradeCostCurve").animationCurveValue =
                AnimationCurve.Linear(1f, 100f, 10f, 1000f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(copy.CalculateUpgradeCost(5) == 500 && copy.CalculateUpgradeCost(11) == 1100,
                "自定义折线节点改变中间等级和末端外的实际费用");
            serialized.FindProperty("_useUpgradeCostCurve").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(copy.CalculateUpgradeCost(1) == 150 && copy.CalculateUpgradeCost(10) == 600,
                "关闭曲线时使用 BaseUpgradeCost + Level * UpgradeCostPerLevel");
            serialized.FindProperty("_useUpgradeCostCurve").boolValue = true;
            serialized.FindProperty("_upgradeCostCurve").animationCurveValue = new AnimationCurve();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(copy.CalculateUpgradeCost(1) == 150, "空曲线安全回退到线性规则");
            serialized.FindProperty("_upgradeCostCurve").animationCurveValue =
                AnimationCurve.Constant(1f, 10f, 150.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(copy.CalculateUpgradeCost(1) == 151, "曲线费用统一四舍五入");
            serialized.FindProperty("_upgradeCostCurve").animationCurveValue =
                AnimationCurve.Constant(1f, 10f, -10f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(copy.CalculateUpgradeCost(1) == 1, "负费用曲线至少收取 1 Soul");
        }
        finally
        {
            Object.Destroy(copy);
            Object.Destroy(defaults);
        }
        Check(original.CalculateUpgradeCost(10) == originalCost, "验证保留作者自定义费用曲线，不改写项目配置 SO");
    }

    private static void ValidateUpgrades()
    {
        PlayerProgression progression = _player.GetComponent<PlayerProgression>();
        SoulWallet wallet = _player.GetComponent<SoulWallet>();
        progression.SetProgression(1, 1, 1, 1);
        wallet.TrySpend(wallet.CurrentSouls);
        int changes = 0;
        Action listener = () => changes++;
        progression.ProgressionChanged += listener;
        Check(!progression.TryUpgrade(StatType.Vigor) && progression.Level == 1 && changes == 0,
            "Soul 不足时拒绝升级，不改属性、不发成长事件");
        int firstCost = progression.UpgradeCost;
        wallet.AddSouls(firstCost);
        bool reentrant = true;
        Action<int> reenter = souls => reentrant = progression.TryUpgrade(StatType.Endurance);
        wallet.SoulsChanged += reenter;
        Check(progression.TryUpgrade(StatType.Vigor), "Soul 恰好等于当前配置费用时可以升级 Vigor");
        wallet.SoulsChanged -= reenter;
        Check(wallet.CurrentSouls == 0 && progression.Level == 2 && progression.Vigor == 2 &&
            _player.Health.MaxHealth == 110 && changes == 1, "升级一次扣款并增加一级和一个属性，实际 HP 变为 110");
        Check(!reentrant && progression.Endurance == 1, "SoulsChanged 回调重入无法重复升级或扣款");
        int secondCost = progression.UpgradeCost;
        PlayerProgressionConfig config = AssetDatabase.LoadAssetAtPath<PlayerProgressionConfig>(
            Day8Task123Builder.ConfigPath);
        int thirdCost = config.CalculateUpgradeCost(3);
        int budget = checked(secondCost + thirdCost + 1000);
        wallet.AddSouls(budget);
        Check(progression.CanUpgrade(StatType.Endurance), "事务结束后 CanUpgrade 可正确判断下一次升级");
        Check(!progression.TryUpgrade((StatType)99) && wallet.CurrentSouls == budget,
            "非法属性枚举不会扣除 Soul");
        Check(progression.TryUpgrade(StatType.Endurance) && wallet.CurrentSouls == budget - secondCost &&
            progression.Level == 3 && _player.Stamina.MaxStamina == 108f,
            "Endurance 升级按当前配置曲线扣魂并实际增加最大体力");
        Check(progression.TryUpgrade(StatType.Strength) && wallet.CurrentSouls == 1000 &&
            progression.Level == 4 && Mathf.Approximately(progression.DamageMultiplier, 1.05f),
            "Strength 升级按当前配置曲线扣魂并增加实际攻击倍率");
        progression.SetProgression(int.MaxValue, 2, 2, 2);
        Check(!progression.TryUpgrade(StatType.Vigor) && wallet.CurrentSouls == 1000,
            "等级达到整数上限时拒绝升级且不扣魂");
        progression.SetProgression(1, int.MaxValue, 1, 1);
        Check(!progression.TryUpgrade(StatType.Vigor) && wallet.CurrentSouls == 1000,
            "属性达到整数上限时拒绝升级且不扣魂");
        progression.ProgressionChanged -= listener;
        progression.SetProgression(1, 1, 1, 1);
        _player.Health.RestoreFull();
        _player.Stamina.RestoreFull();
        wallet.TrySpend(wallet.CurrentSouls);
    }

    private static void ValidateMenus()
    {
        Check(_player.Health.CurrentHealth == _player.Health.MaxHealth &&
            _player.Stamina.CurrentStamina == _player.Stamina.MaxStamina,
            "交互动画完成后恢复玩家，再打开 Grace 菜单");
        Check(Object.FindObjectsOfType<EnemyHealth>().All(enemy => enemy.CurrentHealth == enemy.MaxHealth),
            "打开菜单前真实 CheckpointManager 已完成敌人重置");
        Check(Object.FindObjectOfType<CheckpointManager>().CurrentCheckpoint == _site && _site.IsActivated,
            "打开菜单前赐福点已激活");
        Check(!_player.InputReader.enabled && Time.timeScale == 0f &&
            _player.InputReader.MoveInput == Vector2.zero && _player.InputReader.LookInput == Vector2.zero,
            "菜单打开后暂停世界、禁用玩家输入并清空移动/视角状态");
        CinemachineFreeLook freeLook = Object.FindObjectOfType<CinemachineFreeLook>();
        Check(string.IsNullOrEmpty(freeLook.m_XAxis.m_InputAxisName) &&
            string.IsNullOrEmpty(freeLook.m_YAxis.m_InputAxisName), "菜单打开后自由相机鼠标轴停用");
        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None,
            "菜单打开后显示并解锁鼠标");
        CanvasGroup group = _presenter.GetComponent<CanvasGroup>();
        Check(group.alpha == 1f && group.blocksRaycasts, "菜单遮罩拦截穿透到游戏的点击");
        GraceMenuUI grace = _presenter.GetComponentInChildren<GraceMenuUI>(true);
        LevelUpPanel panel = _presenter.GetComponentInChildren<LevelUpPanel>(true);
        Check(grace.gameObject.activeSelf && !panel.IsVisible, "Grace 菜单只先显示 Level Up 和 Close");
        Capture("Logs/Day8_GraceMenu_Preview.png");

        EventSystem events = EventSystem.current;
        Check(events.currentSelectedGameObject.name == "LevelUpButton", "Grace 菜单初始焦点在 Level Up");
        AxisEventData move = new AxisEventData(events) { moveDir = MoveDirection.Down };
        ExecuteEvents.Execute(events.currentSelectedGameObject, move, ExecuteEvents.moveHandler);
        Check(events.currentSelectedGameObject.name == "CloseButton", "EventSystem 向下导航到 Close");
        move.moveDir = MoveDirection.Up;
        ExecuteEvents.Execute(events.currentSelectedGameObject, move, ExecuteEvents.moveHandler);
        ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        Check(panel.IsVisible && !grace.gameObject.activeSelf, "UI Submit 进入 Level Up 面板");
        Button confirm = Find<Button>(panel, "ConfirmButton");
        PlayerProgression progression = _player.GetComponent<PlayerProgression>();
        SoulWallet wallet = _player.GetComponent<SoulWallet>();
        int vigorCost = progression.UpgradeCost;
        Check(!confirm.interactable && Find<TMP_Text>(panel, "CostValue").text ==
            vigorCost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
            "余额不足时显示真实费用并禁用确认按钮");
        _presenter.ConfirmUpgrade();
        Check(progression.Level == 1 && wallet.CurrentSouls == 0, "绕过按钮发升级请求也不能透支");
        wallet.AddSouls(2450);
        Check(Find<TMP_Text>(panel, "SoulValue").text == "2,450" && confirm.interactable,
            "获得 Soul 通过事件更新面板并启用确认");
        Check(Find<TMP_Text>(panel, "VIGORButton/StatValue").text == "1  >  2" &&
            Find<TMP_Text>(panel, "VIGORButton/EffectValue").text == "100  >  110",
            "选中 Vigor 显示当前/升级后属性与 HP");
        Capture("Logs/Day8_LevelUp_Preview.png");
        Click(confirm);
        Check(progression.Level == 2 && progression.Vigor == 2 && wallet.CurrentSouls == 2450 - vigorCost &&
            _player.Health.MaxHealth == 110, "点击确认按当前曲线扣魂，升级 Vigor 并增加实际 HP");
        int enduranceCost = progression.UpgradeCost;
        Check(Find<TMP_Text>(panel, "CostValue").text ==
            enduranceCost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) &&
            Find<TMP_Text>(panel, "VIGORButton/EffectValue").text == "110  >  120" && confirm.interactable,
            "升级完成后费用、属性预览和确认可用状态立即刷新");
        Click(Find<Button>(panel, "ENDURANCEButton"));
        Check(_presenter.SelectedStat == StatType.Endurance &&
            Find<TMP_Text>(panel, "ENDURANCEButton/EffectValue").text == "100  >  108" &&
            Find<TMP_Text>(panel, "VIGORButton/StatValue").text == "2",
            "切换到 Endurance 时只预览所选属性收益");
        Click(confirm);
        Check(progression.Endurance == 2 && _player.Stamina.MaxStamina == 108f &&
            wallet.CurrentSouls == 2450 - vigorCost - enduranceCost,
            "UI Endurance 升级按当前曲线扣魂并增加实际体力");
        int strengthCost = progression.UpgradeCost;
        Click(Find<Button>(panel, "STRENGTHButton"));
        Check(Find<TMP_Text>(panel, "STRENGTHButton/EffectValue").text == "1.00  >  1.05",
            "Strength 预览保留两位伤害倍率小数");
        Click(confirm);
        Check(progression.Strength == 2 && Mathf.Approximately(progression.DamageMultiplier, 1.05f) &&
            wallet.CurrentSouls == 2450 - vigorCost - enduranceCost - strengthCost && progression.Level == 4,
            "UI Strength 升级按当前曲线扣魂并增加实际伤害倍率");
        wallet.TrySpend(wallet.CurrentSouls);
        Check(!confirm.interactable, "余额消费后确认按钮通过事件重新禁用");

        _keyboard = InputSystem.AddDevice<Keyboard>("Day8ValidationKeyboard");
        PressEscape();
        Check(grace.gameObject.activeSelf && !panel.IsVisible && _presenter.IsOpen,
            "Input System Escape 从升级面板返回 Grace，保持暂停");
        PressEscape();
        Check(!_presenter.IsOpen && _player.InputReader.enabled && Time.timeScale == 1f &&
            !group.blocksRaycasts && freeLook.m_XAxis.m_InputAxisName == "Mouse X" &&
            freeLook.m_YAxis.m_InputAxisName == "Mouse Y", "再次 Escape 关闭菜单并恢复游戏/相机输入");
        Object.FindObjectOfType<CheckpointSite>().Interact();
        Check(_presenter.IsOpen, "同一赐福可再次打开菜单");
        _presenter.OpenLevelUp();
        Click(Find<Button>(panel, "CloseButton"));
        Check(grace.gameObject.activeSelf && _presenter.IsOpen, "Level Up 的 Close 返回 Grace");
        Click(Find<Button>(grace, "CloseButton"));
        Check(!_presenter.IsOpen && Time.timeScale == 1f, "Grace 的 Close 恢复游戏");
        int level = progression.Level;
        _presenter.ConfirmUpgrade();
        Check(progression.Level == level, "菜单关闭时升级请求无效");
        _site.Interact();
        _presenter.enabled = false;
        Check(!_presenter.IsOpen && Time.timeScale == 1f && _player.InputReader.enabled,
            "禁用 Presenter 时关闭菜单并释放暂停/输入状态");
        _presenter.enabled = true;
        _site.Interact();
        Check(_presenter.IsOpen, "Presenter 重新启用后恢复正常事件订阅");
        _player.Health.Die();
        Check(!_presenter.IsOpen && Time.timeScale == 1f, "玩家死亡时菜单自动关闭，不残留暂停");
        _player.Health.ReviveFull();
        _player.Respawn();
    }

    private static T Find<T>(Component root, string path) where T : Component =>
        root.transform.Find(path).GetComponent<T>();

    private static void Click(Button button)
    {
        button.OnPointerClick(new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left });
    }

    private static void PressEscape()
    {
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
        InputSystem.Update();
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        InputSystem.Update();
    }

    private static void Capture(string path)
    {
        Camera camera = Camera.main;
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>()
            .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        Camera[] previousCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        float[] distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target;
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.1f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].worldCamera = previousCameras[i];
                canvases[i].planeDistance = distances[i];
            }
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            if (image != null)
                Object.Destroy(image);
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
        Passed.Add(description);
        Debug.Log("[Day8 Task4-7 PASS] " + description);
    }

    private static void RecordConsole(string message, string stack, LogType type)
    {
        if (type == LogType.Log)
            return;
        File.AppendAllText(ConsolePath, $"[{type}] {message}\n{stack}\n");
        if (type != LogType.Warning && !message.Contains("MCP-FOR-UNITY"))
            _failed = true;
    }

    private static void Complete(string failure)
    {
        EditorApplication.update -= Tick;
        if (_keyboard != null)
            InputSystem.RemoveDevice(_keyboard);
        _presenter?.CloseMenu();
        File.WriteAllText(ReportPath, JsonUtility.ToJson(new Result
        {
            scene = Day8Task123Builder.ScenePath,
            passed = failure == null && !_failed,
            checks = Passed.ToArray(),
            failure = failure ?? (_failed ? "运行期间存在 Console Error，见 RuntimeConsole.log。" : "")
        }, true));
        EditorApplication.isPlaying = false;
    }

    private static void RestoreSave()
    {
        Result result = JsonUtility.FromJson<Result>(File.ReadAllText(ReportPath));
        try
        {
            string path = SessionState.GetString(SavePathKey, "");
            if (SessionState.GetBool(SaveExistsKey, false))
            {
                byte[] bytes = File.ReadAllBytes(SaveBackupPath);
                File.WriteAllBytes(path, bytes);
                if (!File.ReadAllBytes(path).SequenceEqual(bytes))
                    throw new IOException("原存档字节不一致。");
            }
            else if (File.Exists(path))
                File.Delete(path);
            result.checks = result.checks.Concat(new[] { "验证结束后恢复原始存档字节/原始不存在状态" }).ToArray();
        }
        catch (Exception exception)
        {
            result.passed = false;
            result.failure += "\n恢复存档失败：" + exception;
        }
        File.WriteAllText(ReportPath, JsonUtility.ToJson(result, true));
        Debug.Log($"[Day8 Task4-7 Validation] Passed={result.passed}, Checks={result.checks.Length}; {ReportPath}");
    }
}
