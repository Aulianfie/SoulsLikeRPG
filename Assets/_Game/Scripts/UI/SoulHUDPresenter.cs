using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SoulHUDView))]
public sealed class SoulHUDPresenter : MonoBehaviour
{
    [SerializeField] private SoulHUDView _view;
    [SerializeField] private SoulWallet _wallet;

    private void Awake()
    {
        if (_view == null)
            _view = GetComponent<SoulHUDView>();
    }

    private void OnEnable()
    {
        // 允许独立 HUD Prefab 在实例化后通过 Bind 绑定场景玩家。
        if (_wallet == null)
            return;

        _wallet.SoulsChanged += HandleSoulsChanged;
        HandleSoulsChanged(_wallet.CurrentSouls);
    }

    private void Start()
    {
        if (_wallet == null)
            Debug.LogError("SoulHUDPresenter 缺少 SoulWallet 引用。", this);
    }

    private void OnDisable()
    {
        if (_wallet != null)
            _wallet.SoulsChanged -= HandleSoulsChanged;
    }

    public void Bind(SoulWallet wallet)
    {
        if (_wallet != null)
            _wallet.SoulsChanged -= HandleSoulsChanged;

        _wallet = wallet;
        if (_view == null)
            _view = GetComponent<SoulHUDView>();

        if (_wallet == null)
            return;

        if (isActiveAndEnabled)
            _wallet.SoulsChanged += HandleSoulsChanged;

        HandleSoulsChanged(_wallet.CurrentSouls);
    }

    private void HandleSoulsChanged(int currentSouls)
    {
        _view.SetSouls(currentSouls);
    }
}
