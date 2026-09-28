using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RespawnController : MonoBehaviour
{
    [SerializeField] private CheckpointManager _checkpointManager;
    [SerializeField] private PlayerStateMachine _player;
    [SerializeField] private Transform _defaultSpawn;
    [SerializeField] private ScreenFader _screenFader;
    [SerializeField, Min(0.1f)] private float _maxDeathAnimationWait = 5f;

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Coroutine _respawnRoutine;

    private void Awake()
    {
        if (_player == null || _checkpointManager == null || _screenFader == null)
        {
            Debug.LogError("RespawnController 缺少 Player、CheckpointManager 或 ScreenFader 引用。", this);
            enabled = false;
            return;
        }

        _initialPosition = _player.transform.position;
        _initialRotation = _player.transform.rotation;
    }

    private void OnEnable()
    {
        if (_player != null && _player.Health != null)
            _player.Health.Died += OnPlayerDied;
    }

    private void Start()
    {
        // 场景初次加载时，别的组件的 Awake 可能晚于本组件的 OnEnable。
        if (_player == null || _player.Health == null)
            return;

        _player.Health.Died -= OnPlayerDied;
        _player.Health.Died += OnPlayerDied;
        SpawnFromLoadedCheckpoint();
    }

    private void SpawnFromLoadedCheckpoint()
    {
        Transform spawn = _checkpointManager.CurrentRespawnPoint;
        if (spawn != null)
            _player.Motor.Teleport(spawn.position, spawn.rotation);
    }

    private void OnDisable()
    {
        if (_player != null && _player.Health != null)
        {
            _player.Health.Died -= OnPlayerDied;
            _player.InputReader.enabled = true;
        }

        if (_respawnRoutine != null)
            StopCoroutine(_respawnRoutine);
        _respawnRoutine = null;
        _screenFader?.Clear();
    }

    private void OnPlayerDied()
    {
        if (_respawnRoutine == null)
            _respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        // Died 事件早于 PlayerStateMachine.HandleDamageTaken；至少等一帧。
        yield return null;

        float elapsed = 0f;
        while (!_player.PlayerAnimator.IsDeathFinished() &&
               elapsed < _maxDeathAnimationWait)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _player.InputReader.enabled = false;
        yield return StartCoroutine(_screenFader.FadeOut());

        Transform spawn = _checkpointManager.CurrentRespawnPoint;
        Vector3 position = spawn != null ? spawn.position :
            _defaultSpawn != null ? _defaultSpawn.position : _initialPosition;
        Quaternion rotation = spawn != null ? spawn.rotation :
            _defaultSpawn != null ? _defaultSpawn.rotation : _initialRotation;

        _player.Targeting.ClearTarget();
        _player.InputReader.ClearPendingActions();
        _player.Motor.Teleport(position, rotation);
        _player.Health.ReviveFull();
        _player.Stamina.RestoreFull();
        _player.Respawn();
        _checkpointManager.ResetWorld();

        yield return StartCoroutine(_screenFader.FadeIn());
        _player.InputReader.enabled = true;
        _respawnRoutine = null;
    }
}
