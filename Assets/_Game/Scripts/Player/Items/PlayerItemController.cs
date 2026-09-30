using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerItemController : MonoBehaviour
{
    [SerializeField] private MonoBehaviour _currentQuickItem;
    private PlayerStateMachine _player;

    public IPlayerQuickItem CurrentItem => _currentQuickItem as IPlayerQuickItem;

    private void Awake()
    {
        _player = GetComponent<PlayerStateMachine>();
        if (_player == null || CurrentItem == null)
            Debug.LogError("PlayerItemController 缺少玩家或当前 Quick Item。", this);
    }

    public bool TryUseItem()
    {
        return isActiveAndEnabled && _player != null && _player.InputReader.isActiveAndEnabled &&
            Time.timeScale > 0f && CurrentItem != null && CurrentItem.CanUse &&
            CurrentItem.TryUse(_player);
    }
}
