using System;
using System.IO;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BlessingUIBuilder
{
    public const string PrefabPath = "Assets/_Game/UI/Prefabs/PF_BlessingUI.prefab";
    private const string Folder = "Assets/_Game/UI/Textures/Blessing/";
    private static readonly Color Gold = new Color(.67f, .52f, .31f, 1);
    private static readonly Color Light = new Color(.91f, .85f, .71f, 1);
    private static TMP_FontAsset _font;
    private static Sprite[] _icons;

    [MenuItem("Tools/SoulsLike RPG/UI/Build Unified Blessing UI")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != Day8Task123Builder.ScenePath)
            throw new InvalidOperationException("请在检查点主场景的 Edit Mode 中执行。");
        ProgressionPresenter old = Object.FindObjectsOfType<ProgressionPresenter>(true)
            .Single(p => p.gameObject.scene == scene && p.GetComponent<BlessingMenuRoot>() == null);
        if (Object.FindObjectsOfType<BlessingMenuRoot>(true).Any(p => p.gameObject.scene == scene))
            throw new InvalidOperationException("场景已经存在 BlessingUI，请直接编辑预制体。");
        Directory.CreateDirectory("Logs/BlessingUIBackup");
        // 保存副本包含用户未保存的现场内容，主场景只追加新的 UI 和停用旧 UI。
        EditorSceneManager.SaveScene(scene, "Logs/BlessingUIBackup/SceneBeforeMigration.unity", true);
        _font = CreateFont();
        Sprite background = ImportSprite(Folder + "BlessingPanel.png");
        _icons = ImportIcons();
        GameObject root = CreateContent(background);
        GameObject prefab;
        try { prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
        finally { Object.DestroyImmediate(root); }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Add unified Blessing UI");
        ProgressionPresenter presenter = instance.GetComponent<ProgressionPresenter>();
        var original = new SerializedObject(old);
        var target = new SerializedObject(presenter);
        foreach (string field in new[] { "_checkpointManager", "_progression", "_wallet", "_inputReader", "_health", "_freeLookCamera" })
            target.FindProperty(field).objectReferenceValue = original.FindProperty(field).objectReferenceValue;
        target.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
        Undo.RecordObject(old.gameObject, "Retain legacy Grace UI inactive");
        old.gameObject.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/BlessingUI_Setup.txt", "Scene=" + scene.path + "\nPrefab=" + PrefabPath +
            "\nLegacy UI retained inactive=" + old.name + "\nScene saved; original dirty contents preserved in backup.\n");
        RenderPreviews();
    }

    private static GameObject CreateContent(Sprite background)
    {
        GameObject root = new GameObject("BlessingUI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(BlessingMenuRoot));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var group = root.GetComponent<CanvasGroup>();
        group.alpha = 0; group.interactable = group.blocksRaycasts = false;
        Image scrim = Image(root.transform, "ScreenDimmer", Color.black * new Color(1,1,1,.22f));
        Stretch(scrim.rectTransform);
        scrim.raycastTarget = true;
        RectTransform panel = Rect(root.transform, "UnifiedPanel", 0, 0, 1440, 900);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        Image plate = Image(panel, "PanelArtwork", Color.white * new Color(1,1,1,.94f), background);
        Stretch(plate.rectTransform);
        plate.raycastTarget = true;

        RectTransform navRoot = Rect(panel, "Navigation", 4, 4, 403, 892);
        BlessingNavigationView navigation = navRoot.gameObject.AddComponent<BlessingNavigationView>();
        Icon(navRoot, "BlessingEmber", _icons[0], 201, 109, 160);
        Label(navRoot, "Title", "赐福", 52, 198, 300, 68, 44, TextAlignmentOptions.Center);
        Line(navRoot, 54, 283, 295);
        string[] names = { "休息", "属性", "装备", "技能", "更多" };
        Button[] buttons = new Button[5];
        GameObject[] selectedFrames = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            Button button = Button(navRoot, "Nav_" + (BlessingPage)i, "", 30, 313 + i * 92, 340, 80);
            button.image.color = new Color(.05f,.05f,.05f,.03f);
            GameObject frame = Border(button.transform, "SelectedFrame", 340, 80, new Color(.93f,.7f,.3f));
            Image wash = Image(button.transform, "SelectedWash", new Color(.68f,.45f,.12f,.17f));
            Stretch(wash.rectTransform); wash.transform.SetAsFirstSibling();
            // 高亮底板与金边一同切换，键盘焦点仍由 Button 独立显示。
            wash.transform.SetParent(frame.transform, false); Stretch(wash.rectTransform);
            frame.transform.SetAsFirstSibling();
            Icon(button.transform, "Icon", _icons[i + 1], 49, 40, 58);
            Label(button.transform, "Label", names[i], 100, 8, 212, 64, 29);
            selectedFrames[i] = frame;
            frame.SetActive(false);
            buttons[i] = button;
            ConfigureNavigationFocus(button);
        }
        SetArray(navigation, "_buttons", buttons);
        SetArray(navigation, "_selectedFrames", selectedFrames);

        RectTransform contentRoot = Rect(panel, "Content", 413, 18, 1004, 816);
        BlessingContentController content = contentRoot.gameObject.AddComponent<BlessingContentController>();
        GameObject empty = Rect(contentRoot, "DefaultPage", 0, 0, 1004, 816).gameObject;
        GameObject[] pages = new GameObject[5];
        pages[0] = Placeholder(contentRoot, "RestPage", "休息", "火种仍在，旅途未尽。", "生命、体力与血瓶已恢复。\n此处可整理属性，再继续旅程。", 1);
        pages[1] = AttributePage(contentRoot);
        pages[2] = Placeholder(contentRoot, "EquipmentPage", "装备", "整备行装", "装备内容将在此处展开。", 3);
        RectTransform slots = Rect(pages[2].transform, "EquipmentContentSlots", 62, 340, 880, 340);
        for (int i = 0; i < 4; i++)
        {
            RectTransform slot = Rect(slots, "ReservedSlot_" + i, 30 + i * 208, 40, 174, 218);
            Image(slot, "Background", new Color(.05f,.06f,.065f,.7f));
            Stretch(slot.GetChild(0).GetComponent<RectTransform>());
            Border(slot, "Border", 174, 218, new Color(.43f,.36f,.25f,.6f));
            Label(slot, "SlotHint", "待扩展", 15, 81, 144, 50, 21, TextAlignmentOptions.Center);
        }
        pages[3] = Placeholder(contentRoot, "SkillsPage", "技能", "研习与铭记", "技能系统内容将在此处展开。", 4);
        pages[4] = Placeholder(contentRoot, "MorePage", "更多", "旅途中的其他事项", "更多功能将在此处展开。", 5);
        Set(content, "_emptyPage", empty);
        SetArray(content, "_pages", pages);
        content.Show(BlessingPage.None);
        Button close = Button(panel, "CloseMenu", "ESC / B · 返回", 676, 844, 474, 42, 20);
        close.image.color = Color.clear;
        Set(root.GetComponent<BlessingMenuRoot>(), "_panel", panel.gameObject);
        Set(root.GetComponent<BlessingMenuRoot>(), "_navigation", navigation);
        Set(root.GetComponent<BlessingMenuRoot>(), "_content", content);
        Set(root.GetComponent<BlessingMenuRoot>(), "_closeButton", close);
        // 在所有视图引用挂接完成后添加 Presenter，保证运行时 Awake 的依赖完整。
        ProgressionPresenter presenter = root.AddComponent<ProgressionPresenter>();
        Set(presenter, "_blessingMenu", root.GetComponent<BlessingMenuRoot>());
        Set(presenter, "_levelUpPanel", pages[1].GetComponent<LevelUpPanel>());
        ConfigureNavigation(buttons, pages[1], close);
        return root;
    }

    private static GameObject AttributePage(Transform parent)
    {
        RectTransform page = Rect(parent, "AttributePage", 0, 0, 1004, 816);
        Image veil = Image(page, "QuietBackground", new Color(.035f,.04f,.045f,.66f));
        Stretch(veil.rectTransform);
        Heading(page, "属性", 2);
        Label(page, "CoinsLabel", "持有金币", 64, 138, 300, 40, 26);
        TMP_Text coins = Label(page, "Coins", "0", 696, 138, 242, 40, 30, TextAlignmentOptions.Right);
        Line(page, 64, 192, 874);
        Label(page, "LevelLabel", "当前等级", 64, 201, 300, 40, 26);
        TMP_Text level = Label(page, "Level", "1", 696, 201, 242, 40, 30, TextAlignmentOptions.Right);
        Label(page, "PreviewHeader", "当前    ›    升级后", 607, 259, 322, 36, 21, TextAlignmentOptions.Right);
        string[] names = { "生命力", "耐力", "力量" };
        string[] effects = { "最大生命值", "最大体力", "伤害倍率" };
        Button[] rows = new Button[3];
        TMP_Text[] values = new TMP_Text[3], effectValues = new TMP_Text[3];
        GameObject[] frames = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            Button row = Button(page, "Stat_" + (StatType)i, "", 62, 303 + i * 111, 880, 99);
            row.image.color = new Color(.065f,.072f,.08f,.85f);
            Border(row.transform, "Outline", 880, 99, new Color(.47f,.39f,.27f,.55f));
            frames[i] = Border(row.transform, "SelectedFrame", 880, 99, new Color(.92f,.70f,.34f));
            Icon(row.transform, "StatIcon", _icons[6 + i], 55, 49, 72);
            Label(row.transform, "StatLabel", names[i], 111, 6, 326, 51, 30);
            Label(row.transform, "EffectLabel", effects[i], 113, 55, 330, 33, 20).color = new Color(.66f,.61f,.51f);
            values[i] = Label(row.transform, "StatValue", "1  >  2", 482, 6, 358, 48, 31, TextAlignmentOptions.Right);
            effectValues[i] = Label(row.transform, "EffectValue", "100  >  110", 482, 53, 358, 34, 23, TextAlignmentOptions.Right);
            rows[i] = row;
        }
        Line(page, 64, 647, 874);
        Label(page, "CostLabel", "升级所需金币", 79, 661, 450, 44, 26);
        TMP_Text cost = Label(page, "Cost", "0", 696, 657, 242, 44, 32, TextAlignmentOptions.Right);
        TMP_Text status = Label(page, "Status", "请选择属性并确认升级。", 64, 709, 874, 38, 21, TextAlignmentOptions.Center);
        Button confirm = Button(page, "ConfirmUpgrade", "确认升级", 100, 760, 382, 54, 26);
        confirm.image.color = new Color(.30f,.23f,.11f,.8f);
        Border(confirm.transform, "Frame", 382, 54, Gold);
        Button close = Button(page, "Close", "关闭", 529, 760, 382, 54, 26);
        Border(close.transform, "Frame", 382, 54, new Color(.5f,.46f,.38f));
        LevelUpPanel view = page.gameObject.AddComponent<LevelUpPanel>();
        Set(view, "_soulsText", coins); Set(view, "_levelText", level); Set(view, "_costText", cost);
        Set(view, "_statusText", status); Set(view, "_confirmButton", confirm); Set(view, "_closeButton", close);
        SetArray(view, "_statButtons", rows); SetArray(view, "_statValueTexts", values);
        SetArray(view, "_effectTexts", effectValues); SetArray(view, "_selectedFrames", frames);
        var data = new SerializedObject(view); data.FindProperty("_useBlessingStyle").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
        return page.gameObject;
    }

    private static GameObject Placeholder(Transform parent, string name, string title, string subtitle, string description, int icon)
    {
        RectTransform page = Rect(parent, name, 0, 0, 1004, 816);
        Heading(page, title, icon);
        Label(page, "Subtitle", subtitle, 70, 228, 864, 60, 33, TextAlignmentOptions.Center);
        Label(page, "Description", description, 100, 299, 804, 100, 25, TextAlignmentOptions.Center).color = new Color(.72f,.67f,.57f);
        return page.gameObject;
    }

    private static void Heading(Transform parent, string title, int icon)
    {
        Icon(parent, "HeadingIcon", _icons[icon], 375, 73, 79);
        Label(parent, "Title", title, 438, 42, 340, 67, 43);
        Line(parent, 65, 123, 874);
    }

    private static void ConfigureNavigation(Button[] nav, GameObject attribute, Button close)
    {
        Button[] rows = new SerializedObject(attribute.GetComponent<LevelUpPanel>()).FindProperty("_statButtons")
            .GetArrayObjects<Button>();
        for (int i = 0; i < nav.Length; i++)
        {
            Navigation n = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = nav[(i + 4) % 5], selectOnDown = nav[(i + 1) % 5], selectOnRight = i == 1 ? rows[0] : close };
            nav[i].navigation = n;
        }
        Button confirm = attribute.transform.Find("ConfirmUpgrade").GetComponent<Button>();
        Button pageClose = attribute.transform.Find("Close").GetComponent<Button>();
        for (int i = 0; i < rows.Length; i++)
            rows[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = nav[1],
                selectOnUp = i == 0 ? nav[1] : rows[i - 1], selectOnDown = i == 2 ? confirm : rows[i + 1] };
        confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = nav[1], selectOnRight = pageClose, selectOnUp = rows[2], selectOnDown = close };
        pageClose.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = confirm, selectOnUp = rows[2], selectOnDown = close };
        close.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = nav[4], selectOnUp = nav[4] };
    }

    private static void ConfigureNavigationFocus(Button button)
    {
        Transform existing = button.transform.Find("FocusWash");
        Image wash = existing != null ? existing.GetComponent<Image>() : Image(button.transform, "FocusWash", Color.clear);
        Stretch(wash.rectTransform);
        wash.transform.SetAsFirstSibling();
        button.targetGraphic = wash;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(.75f,.59f,.32f,0);
        colors.highlightedColor = new Color(.75f,.59f,.32f,.12f);
        colors.selectedColor = new Color(.75f,.59f,.32f,.20f);
        colors.pressedColor = new Color(.93f,.70f,.34f,.28f);
        colors.disabledColor = Color.clear;
        button.colors = colors;
    }

    // 给已迁移的预制体补充独立焦点反馈，选中栏目金边保持由 NavigationView 管理。
    public static void PolishNavigation()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (Button button in root.transform.Find("UnifiedPanel/Navigation").GetComponentsInChildren<Button>(true))
                ConfigureNavigationFocus(button);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        RenderPreviews();
    }

    private static T[] GetArrayObjects<T>(this SerializedProperty array) where T : Object
    {
        T[] result = new T[array.arraySize];
        for (int i = 0; i < result.Length; i++) result[i] = (T)array.GetArrayElementAtIndex(i).objectReferenceValue;
        return result;
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
    {
        RectTransform rect = Rect(parent, name, 0, 0, 0, 0);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.sprite = sprite; image.raycastTarget = false;
        return image;
    }

    private static void Icon(Transform parent, string name, Sprite sprite, float x, float y, float size)
    {
        Image image = Image(parent, name, new Color(.82f,.74f,.57f), sprite);
        RectTransform r = image.rectTransform; r.pivot = new Vector2(.5f,.5f);
        r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = Vector2.one * size;
        image.preserveAspect = true;
    }

    private static TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, float size,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var rect = Rect(parent, name, x, y, w, h);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.fontSharedMaterial = _font.material;
        text.text = value; text.fontSize = size; text.color = Light; text.alignment = alignment;
        text.raycastTarget = false; text.enableWordWrapping = false;
        return text;
    }

    private static Button Button(Transform parent, string name, string value, float x, float y, float w, float h, float size = 26)
    {
        RectTransform rect = Rect(parent, name, x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.07f,.075f,.08f,.85f);
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.6f,1.5f,1.25f);
        colors.selectedColor = new Color(1.9f,1.7f,1.4f); colors.pressedColor = new Color(.8f,.68f,.48f);
        colors.disabledColor = new Color(.45f,.45f,.45f,.5f); colors.fadeDuration = .1f; button.colors = colors;
        if (!string.IsNullOrEmpty(value)) Label(rect, "Label", value, 10, 0, w-20, h, size, TextAlignmentOptions.Center);
        return button;
    }

    private static void Line(Transform parent, float x, float y, float w)
    {
        Image line = Image(parent, "FineRule", new Color(.55f,.43f,.28f,.55f));
        line.rectTransform.anchoredPosition = new Vector2(x,-y); line.rectTransform.sizeDelta = new Vector2(w,1);
    }

    private static GameObject Border(Transform parent, string name, float w, float h, Color color)
    {
        RectTransform frame = Rect(parent, name, 0, 0, w, h);
        for (int i = 0; i < 4; i++)
        {
            Image edge = Image(frame, "Edge" + i, color);
            edge.rectTransform.anchoredPosition = new Vector2(i == 3 ? w-1 : 0, i == 1 ? -h+1 : 0);
            edge.rectTransform.sizeDelta = i < 2 ? new Vector2(w,1) : new Vector2(1,h);
        }
        return frame.gameObject;
    }

    private static void Set(Object obj, string field, Object value)
    {
        var data = new SerializedObject(obj); data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetArray<T>(Object obj, string field, T[] values) where T : Object
    {
        var data = new SerializedObject(obj); SerializedProperty array = data.FindProperty(field); array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite ImportSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] ImportIcons()
    {
        string path = Folder + "BlessingIcons.png";
        ImportSprite(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        string[] names = { "Ember", "Rest", "Attributes", "Equipment", "Skills", "More", "Vigor", "Endurance", "Strength" };
        var sheet = new SpriteMetaData[9];
        float w = texture.width / 3f, h = texture.height / 3f;
        for (int i = 0; i < 9; i++)
            sheet[i] = new SpriteMetaData { name = names[i], alignment = 0, pivot = new Vector2(.5f,.5f),
                rect = new Rect((i % 3)*w, texture.height - (i/3+1)*h, w, h) };
        importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 618
        importer.spritesheet = sheet;
#pragma warning restore 618
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        return names.Select(name => sprites.Single(sprite => sprite.name == name)).ToArray();
    }

    private static TMP_FontAsset CreateFont()
    {
        const string path = "Assets/_Game/UI/Fonts/NotoSansSC_Blessing.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        bool created = font == null;
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf");
        if (created) font = TMP_FontAsset.CreateFontAsset(source, 64, 7, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        else font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        string chinese = "赐福休息属性装备技能更多火种仍在，旅途未尽。生命、体力与血瓶已恢复。此处可整理属性，再继续旅程。持有金币当前等级升级后生命力耐力力量最大生命值最大体力伤害倍率升级所需金币请选择属性并确认升级。关闭返回金币不足。当前属性无法升级。整备行装装备内容将在此处展开。待扩展研习与铭记技能系统内容将在此处展开。旅途中的其他事项更多功能将在此处展开。";
        string chars = chinese + "›·。\n" + new string(Enumerable.Range(32,95).Select(c=>(char)c).ToArray());
        string toAdd = new string(chars.Distinct().Where(c => !char.IsWhiteSpace(c) && !font.HasCharacter(c)).ToArray());
        try
        {
            if (toAdd.Length > 0 && !font.TryAddCharacters(toAdd, out string missing))
                throw new InvalidOperationException("赐福字体缺字：" + missing);
        }
        finally { font.atlasPopulationMode = AtlasPopulationMode.Static; }
        font.atlasPopulationMode = AtlasPopulationMode.Static; font.name = "NotoSansSC_Blessing";
        if (created)
        {
            AssetDatabase.CreateAsset(font, path); AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        }
        EditorUtility.SetDirty(font); AssetDatabase.SaveAssets(); return font;
    }

    public static void RenderPreviews()
    {
        CreateFont();
        BlessingUIPreview.Render(PrefabPath);
    }
}
