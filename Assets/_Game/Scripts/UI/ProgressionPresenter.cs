using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class ProgressionPresenter : MonoBehaviour
{
    [SerializeField]
    private GraceMenuUI _graceMenu;
    [SerializeField]
    private BlessingMenuRoot _blessingMenu;
    [SerializeField]
    private LevelUpPanel _levelUpPanel;
    [SerializeField]
    private CheckpointManager _checkpointManager;
    [SerializeField]
    private PlayerProgression _progression;
    [SerializeField]
    private SoulWallet _wallet;
    [SerializeField]
    private PlayerInputReader _inputReader;
    [SerializeField]
    private PlayerHealth _health;
    [SerializeField]
    private CinemachineFreeLook _freeLookCamera;
    private CanvasGroup _canvasGroup;
    private InputAction _cancelAction;
    private StatType _selectedStat = StatType.Vigor;
    private float _previousTimeScale;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    private bool _previousInputEnabled;
    private GameObject _previousSelection;
    private string _previousXAxis;
    private string _previousYAxis;

    public bool IsOpen { get; private set; }
    public StatType SelectedStat => _selectedStat;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _cancelAction = new InputAction("CloseGrace", InputActionType.Button);
        _cancelAction.AddBinding("<Keyboard>/escape");
        _cancelAction.AddBinding("<Gamepad>/buttonEast");
        SetOverlayVisible(false);
        if (_blessingMenu != null)
        {
            _blessingMenu.Hide();
        }
        else
        {
            _graceMenu.Hide();
        }

        _levelUpPanel.Hide();
    }

    private void OnEnable()
    {
        _checkpointManager.CheckpointActivated += HandleCheckpointActivated;
        _progression.ProgressionChanged += Refresh;
        _wallet.SoulsChanged += HandleSoulsChanged;
        _health.Died += CloseMenu;
        if (_blessingMenu != null)
        {
            _blessingMenu.AttributesRequested += OpenLevelUp;
            _blessingMenu.RestRequested += RestAtCheckpoint;
            _blessingMenu.CloseRequested += CloseMenu;
        }
        else
        {
            _graceMenu.LevelUpRequested += OpenLevelUp;
            _graceMenu.CloseRequested += CloseMenu;
        }

        _levelUpPanel.StatSelected += SelectStat;
        _levelUpPanel.ConfirmRequested += ConfirmUpgrade;
        _levelUpPanel.CloseRequested += HandlePageClose;
        _cancelAction.performed += HandleCancel;
    }

    private void OnDisable()
    {
        CloseMenu();
        _checkpointManager.CheckpointActivated -= HandleCheckpointActivated;
        _progression.ProgressionChanged -= Refresh;
        _wallet.SoulsChanged -= HandleSoulsChanged;
        _health.Died -= CloseMenu;
        if (_blessingMenu != null)
        {
            _blessingMenu.AttributesRequested -= OpenLevelUp;
            _blessingMenu.RestRequested -= RestAtCheckpoint;
            _blessingMenu.CloseRequested -= CloseMenu;
        }
        else
        {
            _graceMenu.LevelUpRequested -= OpenLevelUp;
            _graceMenu.CloseRequested -= CloseMenu;
        }

        _levelUpPanel.StatSelected -= SelectStat;
        _levelUpPanel.ConfirmRequested -= ConfirmUpgrade;
        _levelUpPanel.CloseRequested -= HandlePageClose;
        _cancelAction.performed -= HandleCancel;
    }

    private void OnDestroy()
    {
        _cancelAction?.Dispose();
    }

    private void HandleCheckpointActivated(CheckpointSite checkpoint)
    {
        if (!isActiveAndEnabled ||
            _health.IsDead)
        {
            return;
        }

        if (!IsOpen)
        {
            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _previousInputEnabled = _inputReader.enabled;
            if (EventSystem.current != null)
            {
                _previousSelection = EventSystem.current.currentSelectedGameObject;
            }
            else
            {
                _previousSelection = null;
            }

            _inputReader.ClearPendingActions();
            _inputReader.enabled = false;
            if (_freeLookCamera != null)
            {
                _previousXAxis = _freeLookCamera.m_XAxis.m_InputAxisName;
                _previousYAxis = _freeLookCamera.m_YAxis.m_InputAxisName;
                _freeLookCamera.m_XAxis.m_InputAxisName = "";
                _freeLookCamera.m_YAxis.m_InputAxisName = "";
                _freeLookCamera.m_XAxis.m_InputAxisValue = 0f;
                _freeLookCamera.m_YAxis.m_InputAxisValue = 0f;
            }

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsOpen = true;
            _cancelAction.Enable();
        }

        SetOverlayVisible(true);
        BackToGrace();
    }

    public void OpenLevelUp()
    {
        if (!IsOpen)
        {
            return;
        }

        if (_blessingMenu != null)
        {
            _blessingMenu.ShowPage(BlessingPage.Attributes);
        }
        else
        {
            _graceMenu.Hide();
        }

        _levelUpPanel.Show(_selectedStat);
        Refresh();
    }

    public void SelectStat(StatType stat)
    {
        if (!IsOpen ||
            !_levelUpPanel.IsVisible ||
            (stat != StatType.Vigor &&
            stat != StatType.Endurance &&
            stat != StatType.Strength))
        {
            return;
        }

        _selectedStat = stat;
        Refresh();
    }

    public void ConfirmUpgrade()
    {
        if (!IsOpen ||
            !_levelUpPanel.IsVisible)
        {
            return;
        }

        _progression.TryUpgrade(_selectedStat);
        Refresh();
    }

    public void BackToGrace()
    {
        if (!IsOpen)
        {
            return;
        }

        _levelUpPanel.Hide();
        if (_blessingMenu != null)
        {
            _blessingMenu.ShowDefault();
        }
        else
        {
            _graceMenu.Show();
        }
    }

    public void CloseMenu()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _cancelAction.Disable();
        if (_blessingMenu != null)
        {
            _blessingMenu.Hide();
        }
        else if (_graceMenu != null)
        {
            _graceMenu.Hide();
        }

        if (_levelUpPanel != null)
        {
            _levelUpPanel.Hide();
        }

        SetOverlayVisible(false);
        Time.timeScale = _previousTimeScale;
        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
        // 场景卸载/退出 Play Mode 时，玩家可能已先于 UI 销毁。
        if (_inputReader != null)
        {
            _inputReader.ClearPendingActions();
            _inputReader.enabled = _previousInputEnabled;
        }

        if (_freeLookCamera != null)
        {
            _freeLookCamera.m_XAxis.m_InputAxisName = _previousXAxis;
            _freeLookCamera.m_YAxis.m_InputAxisName = _previousYAxis;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(_previousSelection);
        }
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    private void Refresh()
    {
        if (!IsOpen ||
            !_levelUpPanel.IsVisible)
        {
            return;
        }

        bool canUpgrade = _progression.CanUpgrade(_selectedStat);
        string status;
        if (canUpgrade)
        {
            status = "请选择属性并确认升级。";
        }
        else if (_wallet.CanAfford(_progression.UpgradeCost))
        {
            status = "当前属性无法升级。";
        }
        else
        {
            status = "金币不足。";
        }

        _levelUpPanel.SetSummary(_wallet.CurrentSouls, _progression.Level, _progression.UpgradeCost, canUpgrade, status);
        for (int i = 0; i < 3; i++)
        {
            StatType stat = (StatType)i;
            _levelUpPanel.SetStatPreview(stat, _progression.GetUpgradePreview(stat), stat == _selectedStat);
        }
    }

    private void HandleSoulsChanged(int souls)
    {
        Refresh();
    }

    private void HandlePageClose()
    {
        if (_blessingMenu != null)
        {
            CloseMenu();
        }
        else
        {
            BackToGrace();
        }
    }

    private void RestAtCheckpoint()
    {
        if (!IsOpen ||
            _checkpointManager.CurrentCheckpoint == null)
        {
            return;
        }

        // 复用既有补满资源、刷新敌人和存档行为。事件会重置首页，再展示休息说明。
        _checkpointManager.ActivateCheckpoint(_checkpointManager.CurrentCheckpoint);
        _blessingMenu.ShowPage(BlessingPage.Rest);
    }

    private void HandleCancel(InputAction.CallbackContext context)
    {
        if (_blessingMenu != null ? _blessingMenu.CurrentPage != BlessingPage.None : _levelUpPanel.IsVisible)
        {
            BackToGrace();
        }
        else
        {
            CloseMenu();
        }
    }
}
