using UnityEngine;

// 保留旧 Prefab/场景 GUID；实际订阅与刷新由通用 Presenter 完成。
public sealed class HealingFlaskPresenter : QuickItemPresenter
{
    [SerializeField]
    private PlayerHealingFlask _flask;

    protected override PlayerItemController ResolveItems()
    {
        return base.ResolveItems() ?? (_flask != null ? _flask.GetComponent<PlayerItemController>() : null);
    }
}
