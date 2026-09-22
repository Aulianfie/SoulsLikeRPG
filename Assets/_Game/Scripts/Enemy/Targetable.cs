using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class Targetable : MonoBehaviour
{
    [SerializeField] private Transform _lockPoint;

    private EnemyHealth _health;

    public Transform LockPoint => _lockPoint;

    public bool IsAvailable =>
        isActiveAndEnabled &&
        _health != null &&
        _health.isActiveAndEnabled &&
        _health.CurrentHealth > 0 &&
        _lockPoint != null &&
        _lockPoint.gameObject.activeInHierarchy;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();

        if (_lockPoint == null)
            Debug.LogError("Targetable 没有配置 LockPoint。", this);
    }
}
