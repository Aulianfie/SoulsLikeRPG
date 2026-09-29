using UnityEngine;

/// <summary>可交互的掉魂实例；模型替换只影响 VisualRoot。</summary>
[DisallowMultipleComponent]
public sealed class SoulDrop : MonoBehaviour, IInteractable
{
    [Tooltip("后续模型、粒子、灯光等视觉内容统一放在此节点下。")]
    [SerializeField] private Transform _visualRoot;
    private PlayerSoulDrop _owner;

    public Transform VisualRoot => _visualRoot;
    public int Souls { get; private set; }
    public bool CanInteract => _owner != null && _owner.CanRecover(this);
    public string InteractionText => $"[E] 取回遗失的魂（{Souls:N0}）";
    public bool RequiresInteractionAnimation => false;

    public void Initialize(PlayerSoulDrop owner, int souls)
    {
        _owner = owner;
        Souls = souls;
    }

    public void Interact()
    {
        if (_owner != null)
            _owner.TryRecover(this);
    }

    public void Consume()
    {
        _owner = null;
        Souls = 0;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
