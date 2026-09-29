using UnityEngine;

[DisallowMultipleComponent]
public sealed class CheckpointSite : MonoBehaviour, IInteractable
{
    [SerializeField] private string _checkpointId = "checkpoint_dungeon_01";
    [SerializeField] private Transform _respawnPoint;
    [SerializeField] private string _interactionText = "[E] 在赐福处休息";

    private bool _isActivated;
    private CheckpointManager _manager;

    public string CheckpointId => _checkpointId;
    public Transform RespawnPoint => _respawnPoint;
    public bool IsActivated => _isActivated;
    public bool CanInteract => isActiveAndEnabled &&
        !string.IsNullOrWhiteSpace(_checkpointId) && _respawnPoint != null;
    public string InteractionText => _interactionText;
    public bool RequiresInteractionAnimation => true;

    public void Interact()
    {
        if (!CanInteract)
            return;

        if (_manager == null)
            _manager = FindObjectOfType<CheckpointManager>();

        if (_manager == null)
        {
            Debug.LogWarning("CheckpointManager was not found in this scene.", this);
            return;
        }

        bool wasActivated = _isActivated;
        if (_manager.ActivateCheckpoint(this))
            Debug.Log(wasActivated ? "在赐福处休息" : "赐福已发现", this);
    }

    internal void MarkActivated()
    {
        _isActivated = true;
    }
}
