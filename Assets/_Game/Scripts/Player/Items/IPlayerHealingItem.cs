public interface IPlayerHealingItem
{
    // 使用途中即使消耗最后一瓶，也仍可完成动画；可用性不包含瓶数和满血判断。
    bool IsAvailable { get; }
    bool CanUse { get; }
    int HealAmount { get; }
    float HealPoint { get; }
    float CompletionPoint { get; }
    float MovementMultiplier { get; }
    bool TryConsume();
    void SetUseVisual(bool visible);
}
