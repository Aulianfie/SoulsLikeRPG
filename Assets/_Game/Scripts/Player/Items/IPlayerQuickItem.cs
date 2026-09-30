public interface IPlayerQuickItem
{
    bool CanUse { get; }
    bool TryUse(PlayerStateMachine player);
}
