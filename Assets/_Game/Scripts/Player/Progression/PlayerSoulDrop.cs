using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth), typeof(SoulWallet), typeof(PlayerMotor))]
public sealed class PlayerSoulDrop : MonoBehaviour
{
    [SerializeField] private SoulDrop _soulDropPrefab;
    private PlayerHealth _health;
    private SoulWallet _wallet;
    private PlayerMotor _motor;
    private PlayerInteractor _interactor;

    public event Action SoulDropChanged;
    public SoulDrop CurrentDrop { get; private set; }

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _wallet = GetComponent<SoulWallet>();
        _motor = GetComponent<PlayerMotor>();
        _interactor = GetComponent<PlayerInteractor>();
        if (_soulDropPrefab == null)
        {
            Debug.LogError("PlayerSoulDrop 缺少 SoulDrop Prefab。", this);
            enabled = false;
        }
    }

    private void OnEnable() => _health.Died += HandleDied;
    private void OnDisable() => _health.Died -= HandleDied;

    private void HandleDied()
    {
        // 即使本次余额为零，旧的未拾取掉魂也会丢失。
        ClearDrop();
        int souls = _wallet.CurrentSouls;
        if (souls > 0)
        {
            Vector3 position = _motor.IsGrounded ? transform.position : _motor.LastGroundedPosition;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                position = hit.position;
            SpawnDrop(souls, position);
        }
        _wallet.SetSouls(0);
        SoulDropChanged?.Invoke();
    }

    public bool CanRecover(SoulDrop drop)
    {
        float distance = _interactor != null ? _interactor.MaxInteractionDistance : 3.5f;
        return isActiveAndEnabled && !_health.IsDead && drop != null &&
            drop == CurrentDrop && drop.Souls > 0 &&
            (transform.position - drop.transform.position).sqrMagnitude <= distance * distance;
    }

    public bool TryRecover(SoulDrop drop)
    {
        if (!CanRecover(drop))
            return false;
        int souls = drop.Souls;
        // 先清除拾取资格，避免 SoulsChanged 回调重复拾取同一份钱。
        ClearDrop();
        _wallet.AddSouls(souls);
        SoulDropChanged?.Invoke();
        return true;
    }

    public void WriteToSave(GameSaveData data)
    {
        data.hasSoulDrop = CurrentDrop != null;
        data.droppedSouls = CurrentDrop != null ? CurrentDrop.Souls : 0;
        data.soulDropPosition = CurrentDrop != null ? CurrentDrop.transform.position : Vector3.zero;
    }

    public void RestoreFromSave(GameSaveData data)
    {
        ClearDrop();
        if (data.hasSoulDrop && data.droppedSouls > 0 && _soulDropPrefab != null)
            SpawnDrop(data.droppedSouls, data.soulDropPosition);
        SoulDropChanged?.Invoke();
    }

    private void SpawnDrop(int souls, Vector3 position)
    {
        CurrentDrop = Instantiate(_soulDropPrefab, position, Quaternion.identity);
        CurrentDrop.name = "SoulDrop_LostSouls";
        CurrentDrop.Initialize(this, souls);
    }

    private void ClearDrop()
    {
        SoulDrop previous = CurrentDrop;
        CurrentDrop = null;
        if (previous != null)
            previous.Consume();
    }

    private void OnDestroy() => ClearDrop();
}
