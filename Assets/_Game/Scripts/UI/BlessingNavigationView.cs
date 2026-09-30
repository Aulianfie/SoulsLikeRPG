using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlessingNavigationView : MonoBehaviour
{
    [SerializeField] private Button[] _buttons;
    [SerializeField] private GameObject[] _selectedFrames;
    public event Action<BlessingPage> PageRequested;

    private void OnEnable()
    {
        _buttons[0].onClick.AddListener(Rest);
        _buttons[1].onClick.AddListener(Attributes);
        _buttons[2].onClick.AddListener(Equipment);
        _buttons[3].onClick.AddListener(Skills);
        _buttons[4].onClick.AddListener(More);
    }

    private void OnDisable()
    {
        _buttons[0].onClick.RemoveListener(Rest);
        _buttons[1].onClick.RemoveListener(Attributes);
        _buttons[2].onClick.RemoveListener(Equipment);
        _buttons[3].onClick.RemoveListener(Skills);
        _buttons[4].onClick.RemoveListener(More);
    }

    public void SetSelected(BlessingPage page)
    {
        for (int i = 0; i < _selectedFrames.Length; i++)
            _selectedFrames[i].SetActive(i == (int)page);
    }

    public void Focus(BlessingPage page = BlessingPage.Rest)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_buttons[Mathf.Clamp((int)page, 0, 4)].gameObject);
    }

    private void Rest() => PageRequested?.Invoke(BlessingPage.Rest);
    private void Attributes() => PageRequested?.Invoke(BlessingPage.Attributes);
    private void Equipment() => PageRequested?.Invoke(BlessingPage.Equipment);
    private void Skills() => PageRequested?.Invoke(BlessingPage.Skills);
    private void More() => PageRequested?.Invoke(BlessingPage.More);
}
