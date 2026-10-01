using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 按实际按钮与输入事件验证集成结果。测试会写存档，结束时恢复用户原始字节。
[InitializeOnLoad]
public static class BlessingUIValidation
{
    private const string Running = "BlessingUI.Validation.Running";

    private static readonly List<string> Checks = new List<string>();
    private static double _next;
    private static double _deadline;
    private static int _phase;
    private static BlessingMenuRoot _menu;
    private static ProgressionPresenter _presenter;
    private static PlayerProgression _player;
    private static SoulWallet _wallet;
    private static CheckpointSite _checkpoint;
    private static Button[] _nav;
    private static LevelUpPanel _view;
    private static Keyboard _keyboard;
    private static bool _keyboardAdded;
    private static Gamepad _gamepad;
    private static bool _gamepadAdded;
    private static int _runtimeErrors;
    private static int _runtimeWarnings;

    static BlessingUIValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += StateChanged;
        Application.logMessageReceived += RecordLog;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        if (File.Exists("Temp/BlessingUI.request"))
        {
            string command = File.ReadAllText("Temp/BlessingUI.request").Trim();
            File.Delete("Temp/BlessingUI.request");
            try
            {
                if (command == "inspect")
                {
                    Inspect();
                }
                else if (command == "preview")
                {
                    BlessingUIBuilder.RenderPreviews();
                }
                else if (command == "polish")
                {
                    BlessingUIBuilder.PolishNavigation();
                }
                else if (command == "validate")
                {
                    Run();
                }
                else if (command == "save")
                {
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                }
            }
            catch (Exception e)
            {
                File.WriteAllText("Logs/BlessingUI_CommandError.txt", e.ToString());
            }
        }

        if (!SessionState.GetBool(Running, false) ||
            !EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < _next)
        {
            return;
        }

        try
        {
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                throw new TimeoutException("赐福 UI 验证超时。");
            }

            Step();
        }
        catch (Exception e)
        {
            Checks.Add("FAIL: " + e);
            Finish();
        }
    }

    [MenuItem("Tools/SoulsLike RPG/UI/Validate Unified Blessing UI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("请在已保存的 Edit Mode 场景运行。");
        }

        // 不与项目中的其他实播验收同时运行。
        foreach (string name in new[]
        {
            "EquipmentHUDValidation",
            "WeaponSwitchRuntimeProbe"
        }
        )
        {
            Type type = typeof(BlessingUIValidation).Assembly.GetType(name);
            FieldInfo flag = type?.GetField("_running", BindingFlags.NonPublic | BindingFlags.Static);
            if (flag != null &&
                (bool)flag.GetValue(null))
            {
                throw new InvalidOperationException(name + " 正在验收，请稍后运行。");
            }
        }

        Inspect();
        ValidateAssets();
        Checks.Clear();
        _phase = 0;
        _runtimeWarnings = 0;
        _runtimeErrors = 0;
        Directory.CreateDirectory("Logs/BlessingUIBackup");
        string path = SaveService.SaveFilePath;
        SessionState.SetString(Running + ".SavePath", path);
        SessionState.SetBool(Running + ".SaveExists", File.Exists(path));
        if (File.Exists(path))
        {
            File.WriteAllBytes("Logs/BlessingUIBackup/OriginalSave.json", File.ReadAllBytes(path));
        }

        SessionState.SetBool(Running, true);
        SessionState.SetInt(Running + ".KeyboardId", -1);
        SessionState.SetInt(Running + ".GamepadId", -1);
        _deadline = EditorApplication.timeSinceStartup + 90;
        _next = EditorApplication.timeSinceStartup + 2;
        EditorApplication.isPlaying = true;
    }

    private static void Step()
    {
        if (_phase == 0)
        {
            _next = EditorApplication.timeSinceStartup + 2;
            _phase++;
            return;
        }

        if (_phase == 1)
        {
            var scene = EditorSceneManager.GetActiveScene();
            _menu = Object.FindObjectsOfType<BlessingMenuRoot>().Single(p => p.gameObject.scene == scene);
            _presenter = _menu.GetComponent<ProgressionPresenter>();
            _player = Object.FindObjectsOfType<PlayerProgression>().Single(p => p.gameObject.scene == scene);
            _wallet = _player.GetComponent<SoulWallet>();
            _checkpoint = Object.FindObjectsOfType<CheckpointSite>().First(p => p.gameObject.scene == scene &&
                    p.CanInteract);
            _view = _menu.GetComponentInChildren<LevelUpPanel>(true);
            _nav = _menu.transform.Find("UnifiedPanel/Navigation").GetComponentsInChildren<Button>(true);
            Check(
                !_presenter.IsOpen &&
                    _menu.GetComponent<CanvasGroup>().alpha == 0,
                "Opening scene keeps menu hidden"
            );
            _checkpoint.Interact();
            Check(
                _presenter.IsOpen &&
                    Time.timeScale == 0 &&
                    !_player.GetComponent<PlayerInputReader>().enabled,
                "Checkpoint Interact opens menu, pauses game and disables gameplay input"
            );
            Check(
                _menu.CurrentPage == BlessingPage.None &&
                    !_view.gameObject.activeInHierarchy,
                "Default right page has no attribute content"
            );
            Capture("Default");
            _nav[1].onClick.Invoke();
            Check(
                _menu.CurrentPage == BlessingPage.Attributes &&
                    _view.gameObject.activeInHierarchy,
                "Attribute navigation opens integrated upgrade page"
            );
            Check(
                _nav.All(button => button.gameObject.activeInHierarchy),
                "All five navigation entries remain visible on attribute page"
            );
            _wallet.SetSouls(3000);
            for (int i = 0; i < 3; i++)
            {
                StatType stat = (StatType)i;
                Button row = _view.transform.Find("Stat_" + stat).GetComponent<Button>();
                row.onClick.Invoke();
                Check(_presenter.SelectedStat == stat, stat + " row selects correct progression stat");
                var preview = _player.GetUpgradePreview(stat);
                Check(
                    row.transform.Find("StatValue").GetComponent<TMP_Text>().text.Contains(preview.NextValue.ToString()),
                    stat + " displays next stat value"
                );
                int before = _wallet.CurrentSouls;
                int cost = _player.UpgradeCost;
                int level = _player.Level;
                _view.transform.Find("ConfirmUpgrade").GetComponent<Button>().onClick.Invoke();
                Check(
                    _wallet.CurrentSouls == before - cost &&
                        _player.Level == level + 1 &&
                        _player.GetUpgradePreview(stat).CurrentValue == preview.NextValue,
                    stat + " confirmation uses existing upgrade transaction exactly once"
                );
                Check(
                    _view.transform.Find("Coins").GetComponent<TMP_Text>().text == _wallet.CurrentSouls.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                    stat + " wallet UI refreshes after transaction"
                );
            }

            Check(
                _player.MaxHealth == _player.GetComponent<PlayerHealth>().MaxHealth &&
                    Mathf.Approximately(_player.MaxStamina, _player.GetComponent<PlayerStamina>().MaxStamina),
                "Upgraded derived health and stamina stay consistent"
            );
            Capture("Attributes");
            _wallet.SetSouls(_player.UpgradeCost - 1);
            Button confirm = _view.transform.Find("ConfirmUpgrade").GetComponent<Button>();
            Check(
                !confirm.interactable &&
                    _view.transform.Find("Status").GetComponent<TMP_Text>().text == "金币不足。",
                "Insufficient funds disable confirmation and show message"
            );
            int insufficientCoins = _wallet.CurrentSouls;
            int insufficientLevel = _player.Level;
            _presenter.ConfirmUpgrade();
            Check(
                _wallet.CurrentSouls == insufficientCoins &&
                    _player.Level == insufficientLevel,
                "Insufficient funds never mutate progression"
            );
            _wallet.SetSouls(3000);
            foreach (int index in new[]
            {
                2,
                3,
                4
            }
            )
            {
                _nav[index].onClick.Invoke();
                Check(
                    _menu.CurrentPage == (BlessingPage)index &&
                        !_view.gameObject.activeInHierarchy,
                    ((BlessingPage)index) + " navigation switches content and hides attributes"
                );
                int before = _wallet.CurrentSouls;
                _presenter.ConfirmUpgrade();
                Check(_wallet.CurrentSouls == before, "Hidden attribute page cannot spend on " + (BlessingPage)index);
            }

            _nav[0].onClick.Invoke();
            Check(
                _menu.CurrentPage == BlessingPage.Rest &&
                    _presenter.IsOpen,
                "Rest reuses checkpoint behavior and stays inside unified panel"
            );
            Check(
                _player.GetComponent<PlayerHealingFlask>().CurrentCharges == _player.GetComponent<PlayerHealingFlask>().MaxCharges,
                "Rest refills existing healing flask"
            );
            _nav[1].onClick.Invoke();
            _view.transform.Find("Close").GetComponent<Button>().onClick.Invoke();
            Check(
                !_presenter.IsOpen &&
                    Time.timeScale == 1 &&
                    _player.GetComponent<PlayerInputReader>().enabled,
                "Attribute Close exits menu and restores time and input"
            );
            for (int i = 0; i < 3; i++)
            {
                Time.timeScale = .7f;
                _checkpoint.Interact();
                _nav[1].onClick.Invoke();
                int level = _player.Level;
                int cost = _player.UpgradeCost;
                int before = _wallet.CurrentSouls;
                _view.transform.Find("ConfirmUpgrade").GetComponent<Button>().onClick.Invoke();
                Check(
                    _player.Level == level + 1 &&
                        _wallet.CurrentSouls == before - cost,
                    "Repeated open/close cycle " + i + " has no duplicate button listeners"
                );
                _menu.transform.Find("UnifiedPanel/CloseMenu").GetComponent<Button>().onClick.Invoke();
                Check(
                    !_presenter.IsOpen &&
                        Mathf.Approximately(Time.timeScale, .7f),
                    "Close restores previous time scale in cycle " + i
                );
            }

            Time.timeScale = 1;
            _checkpoint.Interact();
            _nav[3].onClick.Invoke();
            // 后台验收不能依赖 Game View 焦点；沿用 Day11 的显式玩家输入更新。
            File.WriteAllText(
                "Logs/BlessingUI_InputRouting.txt",
                "Focused=" + Application.isFocused + "\nRouting=" + InputSystem.settings.editorInputBehaviorInPlayMode + "\nBackground=" + InputSystem.settings.backgroundBehavior + "\n"
            );
            SessionState.SetInt(Running + ".Routing", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
            SessionState.SetInt(Running + ".Background", (int)InputSystem.settings.backgroundBehavior);
            SessionState.SetInt(Running + ".Update", (int)InputSystem.settings.updateMode);
            SessionState.SetBool(Running + ".InputChanged", true);
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboardAdded = true;
            SessionState.SetInt(Running + ".KeyboardId", _keyboard.deviceId);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
            PumpInput();
            _phase++;
            _next = EditorApplication.timeSinceStartup + .2;
            return;
        }

        if (_phase == 2)
        {
            Check(
                _presenter.IsOpen &&
                    _menu.CurrentPage == BlessingPage.None,
                "Escape from content returns to default page"
            );
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            PumpInput();
            _phase++;
            _next = EditorApplication.timeSinceStartup + .2;
            return;
        }

        if (_phase == 3)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
            PumpInput();
            _phase++;
            _next = EditorApplication.timeSinceStartup + .2;
            return;
        }

        if (_phase == 4)
        {
            Check(
                !_presenter.IsOpen &&
                    _player.GetComponent<PlayerInputReader>().enabled,
                "Escape from default closes menu and restores controls"
            );
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            PumpInput();
            if (_keyboardAdded)
            {
                InputSystem.RemoveDevice(_keyboard);
            }

            _checkpoint.Interact();
            _nav[2].onClick.Invoke();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _gamepadAdded = true;
            SessionState.SetInt(Running + ".GamepadId", _gamepad.deviceId);
            InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(GamepadButton.East));
            PumpInput();
            _phase++;
            _next = EditorApplication.timeSinceStartup + .2;
            return;
        }

        if (_phase == 5)
        {
            Check(
                _presenter.IsOpen &&
                    _menu.CurrentPage == BlessingPage.None,
                "Gamepad B returns from content to default"
            );
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            PumpInput();
            _phase++;
            _next = EditorApplication.timeSinceStartup + .2;
            return;
        }

        if (_phase == 6)
        {
            InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(GamepadButton.East));
            PumpInput();
            _phase++;
            _next = EditorApplication.timeSinceStartup + .2;
            return;
        }

        if (_phase == 7)
        {
            Check(!_presenter.IsOpen &&
                    Time.timeScale == 1, "Gamepad B closes menu from default");
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            PumpInput();
            if (_gamepadAdded)
            {
                InputSystem.RemoveDevice(_gamepad);
            }

            Check(
                _runtimeErrors == 0 &&
                    _runtimeWarnings == 0,
                "No runtime Console errors or warnings during validation"
            );
            Finish();
        }
    }

    private static void PumpInput()
    {
        typeof(InputSystem).GetMethod(
            "Update",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(InputUpdateType) },
            null
        ).Invoke(null, new object[] { InputUpdateType.Manual });
    }

    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        BlessingUIPreview.CaptureRuntime("Docs/BlessingUI_GameView_" + name + ".png");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }

        Checks.Add("PASS: " + message);
    }

    private static void Finish()
    {
        File.WriteAllLines("Logs/BlessingUI_RuntimeValidation.txt", Checks);
        Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false) ||
            state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        string path = SessionState.GetString(Running + ".SavePath", "");
        try
        {
            if (SessionState.GetBool(Running + ".SaveExists", false))
            {
                File.WriteAllBytes(path, File.ReadAllBytes("Logs/BlessingUIBackup/OriginalSave.json"));
                if (!File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes("Logs/BlessingUIBackup/OriginalSave.json")))
                {
                    throw new IOException("用户存档字节恢复校验失败。");
                }
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.AppendAllText("Logs/BlessingUI_RuntimeValidation.txt", "Original user save restored; returned to Edit Mode.\n");
        }
        finally
        {
            foreach (string key in new[]
            {
                ".KeyboardId",
                ".GamepadId"
            }
            )
            {
                var device = InputSystem.GetDeviceById(SessionState.GetInt(Running + key, -1));
                if (device != null)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            if (SessionState.GetBool(Running + ".InputChanged", false))
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(Running + ".Routing", 0);
                InputSystem.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)SessionState.GetInt(Running + ".Background", 0);
                InputSystem.settings.updateMode = (InputSettings.UpdateMode)SessionState.GetInt(Running + ".Update", 0);
                SessionState.SetBool(Running + ".InputChanged", false);
            }

            SessionState.SetBool(Running, false);
        }
    }

    private static void RecordLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Running, false))
        {
            return;
        }

        if (type == LogType.Error ||
            type == LogType.Exception ||
            type == LogType.Assert)
        {
            _runtimeErrors++;
        }

        if (type == LogType.Warning)
        {
            _runtimeWarnings++;
        }

        if (type != LogType.Log)
        {
            File.AppendAllText("Logs/BlessingUI_RuntimeConsole.txt", type + ": " + message + "\n" + stack + "\n");
        }
    }

    public static void Inspect()
    {
        var counts = new object[]
        {
            0,
            0,
            0
        };
        typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, counts);
        var scene = EditorSceneManager.GetActiveScene();
        string report = $"Project={Application.dataPath}\nScene={scene.path}\nDirty={scene.isDirty}\nPlaying={EditorApplication.isPlaying}\nErrors={counts[0]} Warnings={counts[1]} Logs={counts[2]}\n";
        foreach (var p in Object.FindObjectsOfType<ProgressionPresenter>(true))
        {
            report += $"Presenter={p.name}; active={p.gameObject.activeInHierarchy}\n";
        }

        File.WriteAllText("Logs/BlessingUI_EditorState.txt", report);
        Type entries = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        Type entryType = typeof(Editor).Assembly.GetType("UnityEditor.LogEntry");
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        int count = (int)entries.GetMethod("GetCount", flags).Invoke(null, null);
        entries.GetMethod("StartGettingEntries", flags).Invoke(null, null);
        string console = "";
        try
        {
            for (int i = 0; i < count; i++)
            {
                object entry = Activator.CreateInstance(entryType);
                entries.GetMethod("GetEntryInternal", flags).Invoke(null, new object[] { i, entry });
                console += entryType.GetField("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry) + "\n";
            }
        }
        finally
        {
            entries.GetMethod("EndGettingEntries", flags).Invoke(null, null);
        }

        File.WriteAllText("Logs/BlessingUI_Console.txt", console);
    }

    private static void ValidateAssets()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlessingUIBuilder.PrefabPath);
        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
            {
                throw new InvalidOperationException("Missing script: " + t.name);
            }
        }

        foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            foreach (char c in text.text)
            {
                if (!char.IsWhiteSpace(c) &&
                    !text.font.HasCharacter(c))
                {
                    throw new InvalidOperationException("Font glyph missing: " + c + " at " + text.name);
                }
            }
        }

        var scenePresenter = Object.FindObjectsOfType<ProgressionPresenter>().Single();
        var data = new SerializedObject(scenePresenter);
        foreach (string field in new[]
        {
            "_blessingMenu",
            "_levelUpPanel",
            "_checkpointManager",
            "_progression",
            "_wallet",
            "_health",
            "_inputReader"
        }
        )
        {
            if (data.FindProperty(field).objectReferenceValue == null)
            {
                throw new InvalidOperationException("Scene reference missing: " + field);
            }
        }

        File.WriteAllText(
            "Logs/BlessingUI_AssetValidation.txt",
            "Prefab: no missing scripts; all visible Chinese glyphs present; all scene presenter references assigned.\n"
        );
    }
}
