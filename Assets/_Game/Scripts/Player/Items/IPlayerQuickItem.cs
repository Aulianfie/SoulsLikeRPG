public interface IPlayerQuickItem : IRestRefillable
{
    ConsumableData Data { get; }

    bool IsAvailable { get; }

    bool CanUse { get; }

    int CurrentCharges { get; }

    int MaxCharges { get; }

    float ConsumePoint { get; }

    float CompletionPoint { get; }

    float MovementMultiplier { get; }

    event System.Action<int, int> ChargesChanged;

    bool TryUse(PlayerStateMachine player);

    bool TryConsume();

    void RestoreCharges(int charges);

    void SetUseVisual(bool visible);
}
