using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 定向实播验收；退出后恢复用户存档的原始字节。
[InitializeOnLoad]
public static class EquipmentHUDValidation
{
    private static readonly List<string> Checks = new List<string>();
    private static bool _running, _queued, _saveExisted;
    private static byte[] _originalSave;
    private static double _next, _deadline;
    private static int _phase, _switches, _desiredSlot;
    private static PlayerEquipment _equipment;
    private static Image _icon;

    static EquipmentHUDValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Run()
    {
        _queued = true;
    }

    private static void Begin()
    {
        _queued = false;
        if (EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("需要已保存的 Edit Mode 场景。");
        Checks.Clear();
        _saveExisted = File.Exists(SaveService.SaveFilePath);
        _originalSave = _saveExisted ? File.ReadAllBytes(SaveService.SaveFilePath) : null;
        if (_saveExisted) File.WriteAllBytes("Logs/EquipmentHUDBackup/OriginalSave.json", _originalSave);
        _phase = _switches = 0;
        _deadline = EditorApplication.timeSinceStartup + 60;
        _running = true;
        EditorApplication.isPlaying = true;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Checks.Add("PASS: " + message);
    }

    private static void Tick()
    {
        if (_queued && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !SessionState.GetBool("WeaponSwitch.Probe.Running", false)) Begin();
        if (!_running || !EditorApplication.isPlaying || EditorApplication.isCompiling ||
            EditorApplication.timeSinceStartup < _next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("HUD 验收超时。");
            if (_phase == 0)
            {
                _next = EditorApplication.timeSinceStartup + 2;
                _phase = 1;
                return;
            }
            if (_phase == 1)
            {
                var scene = EditorSceneManager.GetActiveScene();
                _equipment = Object.FindObjectsOfType<PlayerEquipment>().Single(p => p.gameObject.scene == scene);
                if (!_equipment.CanSwitch || !_equipment.GetComponent<PlayerMotor>().IsGrounded) return;
                foreach (EnemyStateMachine enemy in Object.FindObjectsOfType<EnemyStateMachine>()) enemy.gameObject.SetActive(false);
                WeaponSlotPresenter presenter = Object.FindObjectsOfType<WeaponSlotPresenter>().Single();
                _icon = presenter.transform.Find("WeaponIcon").GetComponent<Image>();
                Check(new SerializedObject(presenter).FindProperty("_equipment").objectReferenceValue == _equipment, "Presenter references current scene player");
                Check(_icon.enabled && _icon.sprite == _equipment.CurrentWeapon.Icon, "Startup icon matches equipped weapon");
                Check(_equipment.CurrentWeapon.Icon != null, "Default weapon has icon");
                Check(presenter.GetComponentsInChildren<TMP_Text>().Length == 0, "Weapon slot has no name/header text");
                var flask = Object.FindObjectsOfType<HealingFlaskPresenter>().Single();
                Check(flask.GetComponentsInChildren<TMP_Text>().Length == 1, "Item slot only shows quantity");
                RectTransform weaponRect = presenter.GetComponent<RectTransform>();
                RectTransform flaskRect = flask.GetComponent<RectTransform>();
                Check(weaponRect.anchoredPosition == new Vector2(48, 64) && flaskRect.anchoredPosition == new Vector2(204, 64), "Slots are adjacent at lower left");
                Check(weaponRect.sizeDelta == Vector2.one * 144 && flaskRect.sizeDelta == Vector2.one * 144, "Slot sizes match");
                Check(presenter.GetComponent<CanvasGroup>().blocksRaycasts == false && flask.GetComponent<CanvasGroup>().blocksRaycasts == false, "HUD does not intercept pointer input");
                PlayerHealingFlask bottle = _equipment.GetComponent<PlayerHealingFlask>();
                TMP_Text count = flask.transform.Find("Count").GetComponent<TMP_Text>();
                bottle.RestoreCharges(1);
                Check(count.text == "1 / " + bottle.MaxCharges, "Item event updates quantity to one");
                bottle.RestoreCharges(0);
                Check(count.text == "0 / " + bottle.MaxCharges && flask.transform.Find("FlaskIcon").GetComponent<Image>().color.a < 1, "Empty flask quantity and dimming work");
                bottle.Refill();
                Check(count.text == bottle.MaxCharges + " / " + bottle.MaxCharges, "Refill refreshes quantity");
                presenter.gameObject.SetActive(false);
                presenter.gameObject.SetActive(true);
                Check(_icon.sprite == _equipment.CurrentWeapon.Icon, "Re-enabling HUD resynchronizes equipment");
                _phase = 2;
            }
            if (_phase == 2)
            {
                if (!_equipment.CanSwitch || !_equipment.GetComponent<PlayerMotor>().IsGrounded) return;
                _desiredSlot = _equipment.CurrentSlotIndex == 0 ? 1 : 0;
                _equipment.CycleWeapon();
                _phase = 3;
            }
            if (_phase == 3)
            {
                CheckOncePerSwitch();
                if (_equipment.CurrentSlotIndex != _desiredSlot || !_equipment.CanSwitch) return;
                Check(_icon.sprite == _equipment.CurrentWeapon.Icon && _icon.enabled, "Switch " + (_switches + 1) + " completed with matching HUD icon");
                Check(_equipment.CurrentHitbox.gameObject.activeSelf, "Switch " + (_switches + 1) + " has active held weapon");
                Check(_equipment.GetComponentsInChildren<WeaponHitbox>(true).Count(h => h.gameObject.activeInHierarchy) == 1, "Exactly one held weapon active after switch " + (_switches + 1));
                _switches++;
                if (_switches < 4) { _phase = 2; return; }
                EquipmentHUDSetup.CaptureRuntimeFrame();
                _phase = 4;
                _next = EditorApplication.timeSinceStartup + 1;
            }
            if (_phase == 4)
            {
                Check(File.Exists("Docs/EquipmentHUD_GameView.png"), "Main scene and HUD rendered at 1920x1080");
                File.WriteAllLines("Logs/EquipmentHUD_Validation.txt", Checks);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            Checks.Add("FAIL: " + exception);
            File.WriteAllLines("Logs/EquipmentHUD_Validation.txt", Checks);
            EditorApplication.isPlaying = false;
        }
    }

    private static void CheckOncePerSwitch()
    {
        // 每帧检查实际替换点，不能提前把请求的武器当成当前武器。
        if (_icon.sprite != _equipment.CurrentWeapon.Icon)
            throw new InvalidOperationException("武器动画期间 HUD 与实际装备不同步。");
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!_running || state != PlayModeStateChange.EnteredEditMode) return;
        try
        {
            if (_saveExisted) File.WriteAllBytes(SaveService.SaveFilePath, _originalSave);
            else if (File.Exists(SaveService.SaveFilePath)) File.Delete(SaveService.SaveFilePath);
            File.AppendAllText("Logs/EquipmentHUD_Validation.txt", "Original user save restored; returned to Edit Mode.\n");
        }
        finally { _running = false; }
    }
}
