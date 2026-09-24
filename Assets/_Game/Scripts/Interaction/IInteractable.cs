public interface IInteractable
{
    bool CanInteract { get; }
    string InteractionText { get; }
    void Interact();
}
