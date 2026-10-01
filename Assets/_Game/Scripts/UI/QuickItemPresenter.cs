using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(QuickItemView))]
public class QuickItemPresenter : MonoBehaviour
{
    [SerializeField] private PlayerItemController _items;
    private QuickItemView _view;
    private IPlayerQuickItem _observed;
    protected virtual PlayerItemController ResolveItems() => _items;
    protected virtual void Awake() => _view = GetComponent<QuickItemView>();
    protected virtual void OnEnable()
    {
        _items = ResolveItems();
        if (_items == null)
        {
            Debug.LogError("QuickItemPresenter 缺少 PlayerItemController 引用。", this);
            return;
        }
        _items.CurrentItemChanged += BindItem;
        BindItem(_items.CurrentItem);
    }
    protected virtual void Start()
    {
        if (_items != null) BindItem(_items.CurrentItem);
    }
    protected virtual void OnDisable()
    {
        if (_items != null) _items.CurrentItemChanged -= BindItem;
        UnbindItem();
    }
    private void UnbindItem()
    {
        if (_observed != null) _observed.ChargesChanged -= Refresh;
        _observed = null;
    }
    private void BindItem(IPlayerQuickItem item)
    {
        UnbindItem();
        _observed = item;
        _view.SetItem(item?.Data);
        if (item != null)
        {
            item.ChargesChanged += Refresh;
            Refresh(item.CurrentCharges, item.MaxCharges);
        }
        else Refresh(0, 0);
    }
    private void Refresh(int current, int maximum) => _view.SetCharges(current, maximum);
}
