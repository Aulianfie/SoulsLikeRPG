using System;
using UnityEngine;
using UnityEngine.UI;

// 只管理菜单展示与导航意图；暂停、休息和升级由现有 ProgressionPresenter 负责。
[DisallowMultipleComponent]
public sealed class BlessingMenuRoot : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private BlessingNavigationView _navigation;
    [SerializeField] private BlessingContentController _content;
    [SerializeField] private Button _closeButton;

    public event Action AttributesRequested;
    public event Action RestRequested;
    public event Action CloseRequested;
    public BlessingPage CurrentPage => _content.CurrentPage;

    private void OnEnable()
    {
        _navigation.PageRequested += SelectPage;
        _closeButton.onClick.AddListener(RequestClose);
    }

    private void OnDisable()
    {
        _navigation.PageRequested -= SelectPage;
        _closeButton.onClick.RemoveListener(RequestClose);
    }

    public void ShowDefault()
    {
        _panel.SetActive(true);
        ShowPage(BlessingPage.None);
        _navigation.Focus();
    }

    public void Hide() => _panel.SetActive(false);

    public void ShowPage(BlessingPage page)
    {
        _content.Show(page);
        _navigation.SetSelected(page);
    }

    public void SelectPage(BlessingPage page)
    {
        if (page < BlessingPage.Rest || page > BlessingPage.More)
            return;
        ShowPage(page);
        if (page == BlessingPage.Attributes) AttributesRequested?.Invoke();
        else if (page == BlessingPage.Rest) RestRequested?.Invoke();
    }

    private void RequestClose() => CloseRequested?.Invoke();
}
