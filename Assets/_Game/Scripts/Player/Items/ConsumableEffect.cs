using UnityEngine;

public abstract class ConsumableEffect : ScriptableObject
{
    public abstract bool CanApply(GameObject player);

    public abstract bool Apply(GameObject player);
}
