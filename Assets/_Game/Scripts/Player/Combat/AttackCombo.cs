using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一套连招配置：内部保存有序的 AttackData 列表。
/// 不同武器可以各自挂一份 AttackCombo（例如 LightAttackCombo）。
/// 只保存"数据"，不保存任何运行时状态。
/// </summary>
[CreateAssetMenu(
    fileName = "AttackCombo",
    menuName = "SoulsLike RPG/Combat/Attack Combo")]
public sealed class AttackCombo : ScriptableObject
{
    [Tooltip("有序的连招列表：索引 0 为第一段（Attack1）")]
    [SerializeField]
    private List<AttackData> _attacks = new List<AttackData>();

    public int Count => _attacks != null ? _attacks.Count : 0;

    public AttackData Get(int index)
    {
        if (_attacks == null || index < 0 || index >= _attacks.Count)
            return null;

        return _attacks[index];
    }

    public IReadOnlyList<AttackData> Attacks => _attacks;
}
