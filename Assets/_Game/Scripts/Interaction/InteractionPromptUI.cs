using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private Text _label;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_label != null)
            _label.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 26);
        Hide();
    }

    public void Show(string text)
    {
        if (_label == null)
            return;

        if (_label.text != text)
            _label.text = text;

        _canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }
}
