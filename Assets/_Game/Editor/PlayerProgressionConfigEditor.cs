using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerProgressionConfig))]
public sealed class PlayerProgressionConfigEditor : Editor
{
    private int _previewLevel = 1;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "_upgradeCostCurve");
        SerializedProperty curveProperty = serializedObject.FindProperty("_upgradeCostCurve");
        AnimationCurve curve = curveProperty.animationCurveValue;
        float maxLevel = 100f;
        float maxCost = 5100f;
        foreach (Keyframe key in curve.keys)
        {
            maxLevel = Mathf.Max(maxLevel, key.time);
            maxCost = Mathf.Max(maxCost, key.value);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("升级费用折线图", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("X = 当前等级，Y = 升级所需 Soul。点击图表添加或拖动节点。");
        EditorGUI.BeginChangeCheck();
        curve = EditorGUILayout.CurveField(curve, new Color(0.9f, 0.72f, 0.38f),
            new Rect(1f, 0f, maxLevel - 1f, maxCost * 1.1f), GUILayout.Height(160f));
        if (EditorGUI.EndChangeCheck())
        {
            // 费用表使用分段直线，避免编辑器默认平滑切线造成数值过冲。
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            curveProperty.animationCurveValue = curve;
        }

        if (GUILayout.Button("按 Base Cost / Cost Per Level 重建线性折线"))
        {
            int baseCost = serializedObject.FindProperty("_baseUpgradeCost").intValue;
            int perLevel = serializedObject.FindProperty("_upgradeCostPerLevel").intValue;
            curveProperty.animationCurveValue = AnimationCurve.Linear(1f, baseCost + (float)perLevel,
                100f, baseCost + 100f * perLevel);
        }
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.HelpBox("启用曲线时以图表为准；关闭或清空曲线时使用线性公式。" +
            "超过最后节点按末段斜率延续，费用四舍五入且至少为 1。", MessageType.Info);
        _previewLevel = Mathf.Max(1, EditorGUILayout.IntField("预览当前等级", _previewLevel));
        PlayerProgressionConfig config = (PlayerProgressionConfig)target;
        EditorGUILayout.LabelField($"Lv {_previewLevel} -> {_previewLevel + 1L}",
            $"{config.CalculateUpgradeCost(_previewLevel):N0} Soul");
        EditorGUILayout.LabelField("费用速览",
            $"Lv 1 = {config.CalculateUpgradeCost(1):N0} / Lv 10 = {config.CalculateUpgradeCost(10):N0}");
    }
}
