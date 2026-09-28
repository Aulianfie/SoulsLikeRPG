using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelUpPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text _soulsText;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Button[] _statButtons;
    [SerializeField] private TMP_Text[] _statValueTexts;
    [SerializeField] private TMP_Text[] _effectTexts;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _closeButton;

    private static readonly Color SelectedColor = new Color(0.3f, 0.25f, 0.14f, 1f);
    private static readonly Color UnselectedColor = new Color(0.09f, 0.10f, 0.11f, 1f);

    public event Action<StatType> StatSelected;
    public event Action ConfirmRequested;
    public event Action CloseRequested;
    public bool IsVisible => gameObject.activeSelf;

    private void OnEnable()
    {
        _statButtons[0].onClick.AddListener(SelectVigor);
        _statButtons[1].onClick.AddListener(SelectEndurance);
        _statButtons[2].onClick.AddListener(SelectStrength);
        _confirmButton.onClick.AddListener(RequestConfirm);
        _closeButton.onClick.AddListener(RequestClose);
    }

    private void OnDisable()
    {
        _statButtons[0].onClick.RemoveListener(SelectVigor);
        _statButtons[1].onClick.RemoveListener(SelectEndurance);
        _statButtons[2].onClick.RemoveListener(SelectStrength);
        _confirmButton.onClick.RemoveListener(RequestConfirm);
        _closeButton.onClick.RemoveListener(RequestClose);
    }

    public void Show(StatType selected)
    {
        gameObject.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_statButtons[(int)selected].gameObject);
    }

    public void Hide() => gameObject.SetActive(false);

    public void SetSummary(int souls, int level, int cost, bool canConfirm, string status)
    {
        _soulsText.text = souls.ToString("N0", CultureInfo.InvariantCulture);
        _levelText.text = level.ToString(CultureInfo.InvariantCulture);
        _costText.text = cost.ToString("N0", CultureInfo.InvariantCulture);
        _costText.color = souls >= cost ? new Color(0.93f, 0.82f, 0.58f) : new Color(1f, 0.48f, 0.4f);
        _confirmButton.interactable = canConfirm;
        _statusText.text = status;
    }

    public void SetStatPreview(StatType stat, StatUpgradePreview preview, bool selected)
    {
        int index = (int)stat;
        _statButtons[index].image.color = selected ? SelectedColor : UnselectedColor;
        _statValueTexts[index].text = selected
            ? $"{preview.CurrentValue}  >  {preview.NextValue}"
            : preview.CurrentValue.ToString(CultureInfo.InvariantCulture);
        string format = stat == StatType.Strength ? "0.00" : "0.##";
        string current = preview.CurrentEffect.ToString(format, CultureInfo.InvariantCulture);
        string next = preview.NextEffect.ToString(format, CultureInfo.InvariantCulture);
        _effectTexts[index].text = selected ? $"{current}  >  {next}" : current;
    }

    private void SelectVigor() => StatSelected?.Invoke(StatType.Vigor);
    private void SelectEndurance() => StatSelected?.Invoke(StatType.Endurance);
    private void SelectStrength() => StatSelected?.Invoke(StatType.Strength);
    private void RequestConfirm() => ConfirmRequested?.Invoke();
    private void RequestClose() => CloseRequested?.Invoke();
}
