using System;
using UnityEngine;

[CreateAssetMenu(menuName = "SoulsLike RPG/Consumables/Consumable Data")]
public sealed class ConsumableData : ScriptableObject
{
    [SerializeField]
    private string _itemId;
    [SerializeField]
    private string _displayName;
    [SerializeField]
    private Sprite _icon;
    [SerializeField, Min(1)]
    private int _maxCharges = 3;
    [SerializeField, Range(0f, 1f)]
    private float _consumePoint = 0.45f;
    [SerializeField, Range(0f, 1f)]
    private float _completionPoint = 0.95f;
    [SerializeField, Range(0f, 1f)]
    private float _movementMultiplier = 0.4f;
    [SerializeField]
    private ConsumableEffect[] _effects = Array.Empty<ConsumableEffect>();

    public string ItemId => _itemId;
    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
    public int MaxCharges => Mathf.Max(1, _maxCharges);
    public float ConsumePoint => Mathf.Clamp(_consumePoint, 0.05f, 0.9f);
    public float CompletionPoint => Mathf.Clamp(_completionPoint, ConsumePoint, 1f);
    public float MovementMultiplier => Mathf.Clamp01(_movementMultiplier);
    public System.Collections.Generic.IReadOnlyList<ConsumableEffect> Effects => _effects;

    // 至少一种效果有意义即可使用；满资源的效果不阻止其余效果。
    public bool CanApply(GameObject player)
    {
        foreach (ConsumableEffect effect in _effects)
        {
            if (effect != null &&
                effect.CanApply(player))
            {
                return true;
            }
        }

        return false;
    }

    public bool Apply(GameObject player)
    {
        bool applied = false;
        foreach (ConsumableEffect effect in _effects)
        {
            if (effect != null &&
                effect.CanApply(player))
            {
                applied |= effect.Apply(player);
            }
        }

        return applied;
    }
}
