using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CheckpointManager : MonoBehaviour
{
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private PlayerStamina _playerStamina;

    private readonly Dictionary<string, CheckpointSite> _checkpoints =
        new Dictionary<string, CheckpointSite>(StringComparer.Ordinal);
    private EnemyStateMachine[] _enemies;

    public event Action<CheckpointSite> CheckpointActivated;

    public CheckpointSite CurrentCheckpoint { get; private set; }
    public Transform CurrentRespawnPoint => CurrentCheckpoint != null
        ? CurrentCheckpoint.RespawnPoint
        : null;

    private void Awake()
    {
        EnemyStateMachine[] allEnemies = FindObjectsOfType<EnemyStateMachine>(true);
        var sceneEnemies = new List<EnemyStateMachine>(allEnemies.Length);
        foreach (EnemyStateMachine enemy in allEnemies)
        {
            if (enemy.gameObject.scene == gameObject.scene)
                sceneEnemies.Add(enemy);
        }
        _enemies = sceneEnemies.ToArray();

        foreach (CheckpointSite checkpoint in FindObjectsOfType<CheckpointSite>(true))
        {
            if (checkpoint.gameObject.scene != gameObject.scene ||
                string.IsNullOrWhiteSpace(checkpoint.CheckpointId))
                continue;

            if (!_checkpoints.TryAdd(checkpoint.CheckpointId, checkpoint))
                Debug.LogWarning($"Duplicate checkpoint ID: {checkpoint.CheckpointId}", checkpoint);
        }

        RestoreCheckpointFromSave();
    }

    private void RestoreCheckpointFromSave()
    {
        GameSaveData data = SaveService.Load();
        if (data != null && data.sceneName == gameObject.scene.name)
            TryRestoreCheckpoint(data.checkpointId);
    }

    public bool ActivateCheckpoint(CheckpointSite checkpoint)
    {
        if (checkpoint == null || !checkpoint.CanInteract ||
            !_checkpoints.TryGetValue(checkpoint.CheckpointId, out CheckpointSite registered) ||
            registered != checkpoint || _playerHealth == null || _playerStamina == null ||
            _playerHealth.IsDead)
            return false;

        CurrentCheckpoint = checkpoint;
        checkpoint.MarkActivated();
        _playerHealth.RestoreFull();
        _playerStamina.RestoreFull();
        SaveService.Save(new GameSaveData(gameObject.scene.name, checkpoint.CheckpointId));
        ResetWorld();
        CheckpointActivated?.Invoke(checkpoint);
        return true;
    }

    public void ResetWorld()
    {
        foreach (EnemyStateMachine enemy in _enemies)
        {
            if (enemy != null)
                enemy.ResetForCheckpoint();
        }
    }

    public bool TryRestoreCheckpoint(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId) ||
            !_checkpoints.TryGetValue(checkpointId, out CheckpointSite checkpoint) ||
            !checkpoint.enabled || !checkpoint.gameObject.activeInHierarchy ||
            checkpoint.RespawnPoint == null)
            return false;

        CurrentCheckpoint = checkpoint;
        checkpoint.MarkActivated();
        return true;
    }
}
