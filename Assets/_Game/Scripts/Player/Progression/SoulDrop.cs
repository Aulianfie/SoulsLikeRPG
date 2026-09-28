using UnityEngine;

/// <summary>魂模型占位入口；视觉替换不依赖具体 Mesh，也不参与货币或死亡逻辑。</summary>
[DisallowMultipleComponent]
public sealed class SoulDrop : MonoBehaviour
{
    [Tooltip("后续模型、粒子、灯光等视觉内容统一放在此节点下。")]
    [SerializeField] private Transform _visualRoot;

    public Transform VisualRoot => _visualRoot;
}
