using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 只负责显示；由 HealingFlaskPresenter 订阅血瓶数量变化。
[DisallowMultipleComponent]
public sealed class HealingFlaskView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _countText;
    [SerializeField, Min(0)] private int _previewCount = 3;
    [SerializeField, Min(1)] private int _previewMaximum = 3;

    private void Awake() => SetCount(_previewCount);

    public void SetCount(int count)
    {
        SetCharges(count, _previewMaximum);
    }

    public void SetCharges(int count, int maximum)
    {
        maximum = Mathf.Max(0, maximum);
        count = Mathf.Clamp(count, 0, maximum);
        if (_countText != null)
            _countText.SetText("{0} / {1}", count, maximum);
        if (_icon != null)
            _icon.color = count > 0 ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.65f);
    }
}
