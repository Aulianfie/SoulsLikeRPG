using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHUDView : MonoBehaviour
{
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _manaFill;
    [SerializeField] private Image _staminaFill;

    public void SetHealth(float currentValue, float maxValue)
    {
        SetFill(_healthFill, currentValue, maxValue);
    }

    public void SetMana(float currentValue, float maxValue)
    {
        SetFill(_manaFill, currentValue, maxValue);
    }

    public void SetStamina(float currentValue, float maxValue)
    {
        SetFill(_staminaFill, currentValue, maxValue);
    }

    private static void SetFill(Image image, float currentValue, float maxValue)
    {
        if (image == null)
        {
            return;
        }

        image.fillAmount = maxValue > 0f
            ? Mathf.Clamp01(currentValue / maxValue)
            : 0f;
    }
}
