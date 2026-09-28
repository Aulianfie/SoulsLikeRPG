using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyReward : MonoBehaviour
{
    [SerializeField, Min(0)] private int _soulReward = 100;
    [SerializeField] private SoulWallet _wallet;

    private EnemyHealth _health;
    private bool _rewarded;

    public int SoulReward => _soulReward;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        _health.Died += HandleDied;
        _health.Revived += HandleRevived;

        if (_health.CurrentHealth > 0)
            _rewarded = false;
    }

    private void Start()
    {
        ResolveWallet();
    }

    private void OnDisable()
    {
        _health.Died -= HandleDied;
        _health.Revived -= HandleRevived;
    }

    private void HandleDied()
    {
        if (_rewarded)
            return;

        _rewarded = true;
        ResolveWallet();
        if (_wallet != null)
            _wallet.AddSouls(_soulReward);
        else
            Debug.LogWarning("EnemyReward 未找到 SoulWallet，本次击杀无法发放 Soul。", this);
    }

    private void HandleRevived()
    {
        _rewarded = false;
    }

    private void ResolveWallet()
    {
        if (_wallet != null)
            return;

        // Prefab 无法保存场景玩家引用；实例只在初始化/发奖时解析同场景的钱包。
        foreach (SoulWallet wallet in FindObjectsOfType<SoulWallet>())
        {
            if (wallet.gameObject.scene == gameObject.scene)
            {
                _wallet = wallet;
                return;
            }
        }
    }
}
