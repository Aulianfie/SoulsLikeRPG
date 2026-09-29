public interface IInteractable
{
    bool CanInteract { get; }
    string InteractionText { get; }
    bool RequiresInteractionAnimation { get; }
    void Interact();
}
