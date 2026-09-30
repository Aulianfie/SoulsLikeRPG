using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class Day8ChineseUISetup
{
    public const string FontPath = "Assets/_Game/UI/Fonts/NotoSansSC_Progression.asset";
    private const string SourcePath = "Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf";
    private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>
    {
        { "GRACE", "赐福" }, { "REST AT THE GRACE", "在赐福处休息" },
        { ">  LEVEL UP", ">  升级" }, { "LEVEL UP", "升级" },
        { "CLOSE", "关闭" }, { "CONFIRM", "确认升级" },
        { "ESC / B - CLOSE", "ESC / B - 关闭" }, { "ESC / B - BACK", "ESC / B - 返回" },
        { "SOUL", "持有金币" }, { "LEVEL", "等级" },
        { "CURRENT  >  AFTER", "当前  >  升级后" },
        { "VIGOR", "生命力" }, { "ENDURANCE", "耐力" }, { "STRENGTH", "力量" },
        { "HP", "最大生命值" }, { "Stamina", "最大体力" }, { "Damage multiplier", "伤害倍率" },
        { "UPGRADE COST", "升级所需金币" },
        { "Select an attribute and confirm.", "请选择属性并确认升级。" },
        { "Cannot upgrade this attribute.", "当前属性无法升级。" },
        { "Not enough Soul.", "金币不足。" }
    };

    public static string Translate(string value) =>
        Translations.TryGetValue(value, out string chinese) ? chinese : value;

    public static TMP_FontAsset EnsureFont()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null)
            return existing;
        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (source == null)
            throw new MissingReferenceException("缺少 Noto Sans CJK 简体中文字体源文件。");
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 7,
            GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        if (font == null)
            throw new InvalidOperationException("无法生成中文字体图集。");
        string glyphs = string.Concat(Translations.Values) +
            new string(Enumerable.Range(32, 95).Select(value => (char)value).ToArray());
        if (!font.TryAddCharacters(glyphs, out string missing))
            throw new InvalidOperationException("中文字体图集缺字：" + missing);
        // 固定界面的全部字形预先烘焙，不依赖运行时系统字体或临时图集。
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.name = "NotoSansSC_Progression";
        if (!AssetDatabase.IsValidFolder("Assets/_Game/UI/Fonts"))
            AssetDatabase.CreateFolder("Assets/_Game/UI", "Fonts");
        AssetDatabase.CreateAsset(font, FontPath);
        font.material.name = font.name + " Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D texture in font.atlasTextures)
        {
            texture.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(texture, font);
        }
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    [MenuItem("Tools/SoulsLike RPG/Day8/Apply Chinese Progression UI")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        TMP_FontAsset font = EnsureFont();
        GameObject prefab = PrefabUtility.LoadPrefabContents(Day8Task4567Builder.PrefabPath);
        try
        {
            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = Translate(text.text);
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, Day8Task4567Builder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Day8 Chinese UI] 赐福与升级界面中文文字、字体已保存到原 UI Prefab。");
    }
}
