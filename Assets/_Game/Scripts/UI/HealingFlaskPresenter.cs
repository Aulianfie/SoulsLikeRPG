using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HealingFlaskView))]
public sealed class HealingFlaskPresenter : MonoBehaviour
{
    [SerializeField] private PlayerHealingFlask _flask;
    private HealingFlaskView _view;

    private void Awake() => _view = GetComponent<HealingFlaskView>();
    private void OnEnable()
    {
        if (_flask == null)
        {
            Debug.LogError("HealingFlaskPresenter 缺少血瓶组件引用。", this);
            return;
        }
        _flask.ChargesChanged += Refresh;
        Refresh(_flask.CurrentCharges, _flask.MaxCharges);
    }
    private void Start()
    {
        if (_flask != null) Refresh(_flask.CurrentCharges, _flask.MaxCharges);
    }
    private void OnDisable()
    {
        if (_flask != null) _flask.ChargesChanged -= Refresh;
    }
    private void Refresh(int current, int maximum) => _view.SetCharges(current, maximum);
}
