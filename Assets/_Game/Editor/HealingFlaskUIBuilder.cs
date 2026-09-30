using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class HealingFlaskUIBuilder
{
    private const string IconPath = "Assets/_Game/UI/Textures/HealingFlask.png";
    private const string PrefabPath = "Assets/_Game/UI/Prefabs/PF_HealingFlaskUI.prefab";
    private const string FontPath = "Assets/_Game/UI/Fonts/NotoSansSC_Flask.asset";
    private static readonly Color Gold = new Color(0.72f, 0.59f, 0.36f);

    [MenuItem("Tools/SoulsLike RPG/UI/Build Healing Flask UI")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode，再创建血瓶 UI。");
        string baseline = ConsoleCounts();
        File.WriteAllText("Temp/HealingFlaskUI.baseline.txt", baseline);
        Sprite icon = ConfigureIcon();
        TMP_FontAsset font = EnsureFont();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            GameObject root = CreateContent(icon, font);
            try { prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { Object.DestroyImmediate(root); }
        }

        var scene = EditorSceneManager.GetActiveScene();
        bool wasDirty = scene.isDirty;
        Canvas canvas = Object.FindObjectsOfType<Canvas>(true).FirstOrDefault(value =>
            value.gameObject.scene == scene && value.renderMode == RenderMode.ScreenSpaceOverlay &&
            value.GetComponentInChildren<PlayerHUDView>(true) != null);
        if (canvas == null)
            canvas = Object.FindObjectsOfType<Canvas>(true).FirstOrDefault(value =>
                value.gameObject.scene == scene && value.renderMode == RenderMode.ScreenSpaceOverlay);
        if (canvas == null)
        {
            var canvasObject = new GameObject("FlaskCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create flask Canvas");
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (canvas.transform.Find("HealingFlaskUI") == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            instance.name = "HealingFlaskUI";
            Undo.RegisterCreatedObjectUndo(instance, "Add healing flask UI");
            EditorSceneManager.MarkSceneDirty(scene);
        }
        bool saved = !wasDirty && !string.IsNullOrEmpty(scene.path) && EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        RenderPreview(prefab);
        File.WriteAllText("Temp/HealingFlaskUI.result.txt",
            $"Prefab={PrefabPath}\nScene={scene.path}\nCanvas={canvas.name}\nSceneSaved={saved}\nOriginallyDirty={wasDirty}\nBaseline={baseline}\nAfter={ConsoleCounts()}\n" +
            "Preview=Docs/HealingFlaskUI_Preview.png\nCount preview=3; gameplay not connected.\n");
        Debug.Log("[Healing Flask UI] 血瓶 UI 已创建，默认显示 3；后续通过 HealingFlaskView.SetCount 更新数量。");
    }

    // 可在独立验证项目的批处理模式执行，不打开或保存游戏场景。
    public static void BuildPrefabOnly()
    {
        GameObject root = CreateContent(ConfigureIcon(), EnsureFont());
        try
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            root.GetComponent<HealingFlaskView>().SetCount(0);
            if (root.GetComponentsInChildren<TMP_Text>().Single(value => value.name == "Count").text != "0 / 3")
                throw new InvalidOperationException("血瓶数量显示验证失败。");
            root.GetComponent<HealingFlaskView>().SetCount(-1);
            root.GetComponent<HealingFlaskView>().SetCount(3);
            RenderPreview(prefab);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/HealingFlaskUI_Validation.txt", "Prefab created; zero/negative/positive count validated; preview rendered.\n" + ConsoleCounts());
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Sprite ConfigureIcon()
    {
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException("缺少血瓶图标：" + IconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    private static TMP_FontAsset EnsureFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null)
        {
            if (!font.HasCharacter('_') || !font.HasCharacter('/'))
            {
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.TryAddCharacters(" /R_…", out _);
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                EditorUtility.SetDirty(font);
            }
            return font;
        }
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf");
        if (source == null) throw new MissingReferenceException("缺少现有的 Noto 中文字体。");
        font = TMP_FontAsset.CreateFontAsset(source, 64, 7, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, false);
        if (!font.TryAddCharacters("血瓶0123456789 /R_…", out string missing))
            throw new InvalidOperationException("血瓶 UI 字体缺字：" + missing);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.name = "NotoSansSC_Flask";
        AssetDatabase.CreateAsset(font, FontPath);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
        return font;
    }

    private static GameObject CreateContent(Sprite icon, TMP_FontAsset font)
    {
        var root = new GameObject("HealingFlaskUI", typeof(RectTransform), typeof(CanvasGroup), typeof(HealingFlaskView));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(48, 64);
        rect.sizeDelta = new Vector2(188, 108);
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        Image border = Image(rect, "GoldBorder", new Vector2(94, 54), new Vector2(188, 108), Gold);
        Image(rect, "Background", new Vector2(94, 54), new Vector2(185, 105), new Color(0.045f, 0.037f, 0.035f, 0.94f));
        Image flask = Image(rect, "FlaskIcon", new Vector2(47, 56), new Vector2(94, 94), Color.white);
        flask.sprite = icon;
        flask.preserveAspect = true;
        Image(rect, "Divider", new Vector2(99, 54), new Vector2(1, 64), new Color(Gold.r, Gold.g, Gold.b, 0.35f));
        Text(rect, "Label", "血瓶", new Vector2(145, 79), new Vector2(80, 30), 20, Gold, font);
        TMP_Text count = Text(rect, "Count", "3 / 3", new Vector2(145, 40), new Vector2(80, 50), 26, new Color(0.95f, 0.90f, 0.79f), font);
        var view = new SerializedObject(root.GetComponent<HealingFlaskView>());
        view.FindProperty("_icon").objectReferenceValue = flask;
        view.FindProperty("_countText").objectReferenceValue = count;
        view.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static Image Image(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        SetRect(go.GetComponent<RectTransform>(), parent, position, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        SetRect(go.GetComponent<RectTransform>(), parent, position, size);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void RenderPreview(GameObject prefab)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var cameraObject = new GameObject("FlaskPreviewCamera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, preview);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.scene = preview;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 90;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.085f, 0.078f, 0.07f);
        var target = new RenderTexture(480, 360, 24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            var canvasObject = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, preview);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one * 1.6f;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(480, 360, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, 480, 360), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Docs");
                File.WriteAllBytes("Docs/HealingFlaskUI_Preview.png", texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static string ConsoleCounts()
    {
        Type type = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        MethodInfo method = type?.GetMethod("GetCountsByType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        object[] counts = { 0, 0, 0 };
        method?.Invoke(null, counts);
        return $"errors={counts[0]}, warnings={counts[1]}, logs={counts[2]}";
    }
}
