using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyHealthBarUI : MonoBehaviour
{
    [SerializeField] private EnemyHealth _health;
    [SerializeField] private Image _fillImage;
    [SerializeField] private TMP_Text _healthText;

    private Transform _cameraTransform;

    private void Awake()
    {
        if (_health == null)
            _health = GetComponentInParent<EnemyHealth>();

        CacheMainCamera();
    }

    private void OnEnable()
    {
        if (
            _health == null ||
            _fillImage == null ||
            _healthText == null
        )
        {
            Debug.LogError(
                "EnemyHealthBarUI 缺少生命、填充图或文字引用。",
                this
            );

            enabled = false;
            return;
        }

        _health.HealthChanged += Refresh;
        Refresh(_health.CurrentHealth, _health.MaxHealth);
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null)
            CacheMainCamera();

        if (_cameraTransform != null)
            transform.rotation = _cameraTransform.rotation;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.HealthChanged -= Refresh;
    }

    private void Refresh(int currentHealth, int maxHealth)
    {
        float normalizedHealth = maxHealth > 0
            ? currentHealth / (float)maxHealth
            : 0f;

        RectTransform fillRect = _fillImage.rectTransform;
        Vector3 fillScale = fillRect.localScale;
        fillScale.x = Mathf.Clamp01(normalizedHealth);
        fillRect.localScale = fillScale;
        _healthText.SetText(
            "{0:0} / {1:0}",
            currentHealth,
            maxHealth
        );
    }

    private void CacheMainCamera()
    {
        Camera mainCamera = Camera.main;
        _cameraTransform = mainCamera != null
            ? mainCamera.transform
            : null;
    }
}
