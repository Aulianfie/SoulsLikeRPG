using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SoulHUDView : MonoBehaviour
{
    [SerializeField] private TMP_Text _soulText;

    public void SetSouls(int currentSouls)
    {
        if (_soulText != null)
            _soulText.text = currentSouls.ToString("N0", CultureInfo.InvariantCulture);
    }
}
