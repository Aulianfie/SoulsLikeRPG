using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GraceMenuUI : MonoBehaviour
{
    [SerializeField] private Button _levelUpButton;
    [SerializeField] private Button _closeButton;

    public event Action LevelUpRequested;
    public event Action CloseRequested;

    private void OnEnable()
    {
        _levelUpButton.onClick.AddListener(RequestLevelUp);
        _closeButton.onClick.AddListener(RequestClose);
    }

    private void OnDisable()
    {
        _levelUpButton.onClick.RemoveListener(RequestLevelUp);
        _closeButton.onClick.RemoveListener(RequestClose);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_levelUpButton.gameObject);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RequestLevelUp() => LevelUpRequested?.Invoke();
    private void RequestClose() => CloseRequested?.Invoke();
}
