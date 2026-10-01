using UnityEngine;

[CreateAssetMenu(menuName = "SoulsLike RPG/Consumables/Restore Health")]
public sealed class RestoreHealthEffect : ConsumableEffect
{
    [SerializeField, Min(1)] private int _amount = 40;
    public int Amount => Mathf.Max(1, _amount);

    public override bool CanApply(GameObject player)
    {
        return player != null && player.TryGetComponent(out PlayerHealth health) &&
            !health.IsDead && health.CurrentHealth < health.MaxHealth;
    }

    public override bool Apply(GameObject player)
    {
        if (!CanApply(player)) return false;
        player.GetComponent<PlayerHealth>().Heal(Amount);
        return true;
    }
}
