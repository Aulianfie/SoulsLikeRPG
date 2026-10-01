using UnityEngine;

[CreateAssetMenu(menuName = "SoulsLike RPG/Consumables/Restore Mana")]
public sealed class RestoreManaEffect : ConsumableEffect
{
    [SerializeField, Min(0.01f)] private float _amount = 50f;
    public float Amount => Mathf.Max(0.01f, _amount);

    public override bool CanApply(GameObject player)
    {
        return player != null && player.TryGetComponent(out PlayerMana mana) &&
            mana.CurrentMana < mana.MaxMana;
    }

    public override bool Apply(GameObject player)
    {
        if (!CanApply(player)) return false;
        player.GetComponent<PlayerMana>().Restore(Amount);
        return true;
    }
}
