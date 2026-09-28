public readonly struct StatUpgradePreview
{
    public int CurrentValue { get; }
    public int NextValue { get; }
    public float CurrentEffect { get; }
    public float NextEffect { get; }

    public StatUpgradePreview(int currentValue, int nextValue,
        float currentEffect, float nextEffect)
    {
        CurrentValue = currentValue;
        NextValue = nextValue;
        CurrentEffect = currentEffect;
        NextEffect = nextEffect;
    }
}
