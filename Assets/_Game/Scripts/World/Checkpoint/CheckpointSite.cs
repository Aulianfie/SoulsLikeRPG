using UnityEngine;

[DisallowMultipleComponent]
public sealed class CheckpointSite : MonoBehaviour, IInteractable
{
    [SerializeField] private string _checkpointId = "checkpoint_dungeon_01";
    [SerializeField] private Transform _respawnPoint;
    [SerializeField] private string _interactionText = "[E] 在赐福处休息";

    private bool _isActivated;

    public string CheckpointId => _checkpointId;
    public Transform RespawnPoint => _respawnPoint;
    public bool IsActivated => _isActivated;
    public bool CanInteract => isActiveAndEnabled &&
        !string.IsNullOrWhiteSpace(_checkpointId) && _respawnPoint != null;
    public string InteractionText => _interactionText;

    public void Interact()
    {
        if (!CanInteract)
            return;

        _isActivated = true;
        Debug.Log($"Checkpoint activated: {_checkpointId}", this);
    }
}
