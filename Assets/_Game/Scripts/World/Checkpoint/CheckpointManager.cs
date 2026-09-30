using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CheckpointManager : MonoBehaviour
{
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private PlayerStamina _playerStamina;

    private readonly Dictionary<string, CheckpointSite> _checkpoints =
        new Dictionary<string, CheckpointSite>(StringComparer.Ordinal);
    private EnemyStateMachine[] _enemies;
    private SoulWallet _wallet;
    private PlayerProgression _progression;
    private PlayerSoulDrop _soulDrop;
    private PlayerHealingFlask _flask;
    private PlayerItemController _items;
    private GameSaveData _loadedSave;
    private bool _initialized;
    private bool _canSave;
    private bool _savePending;
    private bool _hasForeignSceneSave;
    private (int level, int vigor, int endurance, int strength) _observedProgression;

    public event Action<CheckpointSite> CheckpointActivated;

    public CheckpointSite CurrentCheckpoint { get; private set; }
    public Transform CurrentRespawnPoint => CurrentCheckpoint != null
        ? CurrentCheckpoint.RespawnPoint
        : null;

    private void Awake()
    {
        if (_playerHealth != null)
        {
            _wallet = _playerHealth.GetComponent<SoulWallet>();
            _progression = _playerHealth.GetComponent<PlayerProgression>();
            _soulDrop = _playerHealth.GetComponent<PlayerSoulDrop>();
            _flask = _playerHealth.GetComponent<PlayerHealingFlask>();
            _items = _playerHealth.GetComponent<PlayerItemController>();
            if (_progression != null)
                _observedProgression = (_progression.Level, _progression.Vigor,
                    _progression.Endurance, _progression.Strength);
        }
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
        _loadedSave = SaveService.Load();
        // 无法读取的文件保留原样，避免自动保存覆盖损坏或更高版本的存档。
        _canSave = _loadedSave != null || !File.Exists(SaveService.SaveFilePath);
        _hasForeignSceneSave = _loadedSave != null && _loadedSave.sceneName != gameObject.scene.name;
        if (_loadedSave != null && _loadedSave.sceneName == gameObject.scene.name)
            TryRestoreCheckpoint(_loadedSave.checkpointId);
    }

    private void OnEnable()
    {
        if (_wallet != null)
            _wallet.SoulsChanged += HandleSoulsChanged;
        if (_progression != null)
            _progression.ProgressionChanged += HandleProgressionChanged;
        if (_soulDrop != null)
            _soulDrop.SoulDropChanged += MarkSavePending;
        if (_flask != null)
            _flask.ChargesChanged += HandleFlaskChanged;
    }

    private void Start()
    {
        // 所有玩家组件的 Awake 已结束，加载后由成长配置重新计算派生属性。
        if (_loadedSave != null && _loadedSave.sceneName == gameObject.scene.name)
        {
            if (_wallet != null)
                _wallet.SetSouls(_loadedSave.souls);
            if (_progression != null)
                _progression.SetProgression(_loadedSave.level, _loadedSave.vigor,
                    _loadedSave.endurance, _loadedSave.strength);
            if (_soulDrop != null)
                _soulDrop.RestoreFromSave(_loadedSave);
            if (_flask != null)
                _flask.RestoreCharges(_loadedSave.flaskCharges);
        }
        _initialized = true;
        _savePending = _canSave && !_hasForeignSceneSave;
        _loadedSave = null;
    }

    private void LateUpdate()
    {
        // 扣魂事件早于属性递增；同一帧结束后只写一次完整快照。
        if (_savePending)
            SaveCurrentProgression();
    }

    public bool SaveCurrentProgression()
    {
        if (!_initialized || !_canSave || _hasForeignSceneSave || _wallet == null || _progression == null)
            return false;
        var data = new GameSaveData(gameObject.scene.name,
            CurrentCheckpoint != null ? CurrentCheckpoint.CheckpointId : "")
        {
            souls = _wallet.CurrentSouls,
            level = _progression.Level,
            vigor = _progression.Vigor,
            endurance = _progression.Endurance,
            strength = _progression.Strength,
            flaskCharges = _flask != null ? _flask.CurrentCharges : -1
        };
        if (_soulDrop != null)
            _soulDrop.WriteToSave(data);
        bool saved = SaveService.Save(data);
        _savePending = !saved;
        return saved;
    }

    private void HandleSoulsChanged(int souls) => MarkSavePending();
    private void HandleFlaskChanged(int current, int maximum) => MarkSavePending();
    private void HandleProgressionChanged()
    {
        var current = (_progression.Level, _progression.Vigor,
            _progression.Endurance, _progression.Strength);
        // Start 重算派生属性也会通知，但基础成长未变时不算玩家的新进度。
        if (current == _observedProgression)
            return;
        _observedProgression = current;
        MarkSavePending();
    }

    private void MarkSavePending()
    {
        if (!_initialized)
            return;
        // 明确的钱包、成长、掉魂变化或赐福交互允许替换有效的异场景存档。
        // 损坏/未来存档仍由 _canSave 阻止覆盖。
        _hasForeignSceneSave = false;
        _savePending = true;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveCurrentProgression();
    }

    private void OnApplicationQuit() => SaveCurrentProgression();

    private void OnDisable()
    {
        if (_savePending)
            SaveCurrentProgression();
        if (_wallet != null)
            _wallet.SoulsChanged -= HandleSoulsChanged;
        if (_progression != null)
            _progression.ProgressionChanged -= HandleProgressionChanged;
        if (_soulDrop != null)
            _soulDrop.SoulDropChanged -= MarkSavePending;
        if (_flask != null)
            _flask.ChargesChanged -= HandleFlaskChanged;
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
        _items?.RefillRestItems();
        MarkSavePending();
        SaveCurrentProgression();
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
