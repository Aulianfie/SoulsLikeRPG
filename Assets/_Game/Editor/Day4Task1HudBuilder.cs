using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

public static class Day4Task1HudBuilder
{
    private const string AtlasPath = "Assets/_Game/UI/Textures/HUD Texture.png";
    private const string FrameOverlayPath = "Assets/_Game/UI/Textures/HUD_BarFrameOverlay.png";
    private const string PrefabPath = "Assets/_Game/UI/Prefabs/PF_PlayerHUD.prefab";

    [MenuItem("Tools/SoulsLike RPG/Day4/Build Task1 HUD")]
    public static void Build()
    {
        ConfigureAtlas();
        GenerateFrameOverlay();

        Sprite healthFill = LoadSprite(AtlasPath, "HUD_RedFill");
        Sprite manaFill = LoadSprite(AtlasPath, "HUD_BlueFill");
        Sprite staminaFill = LoadSprite(AtlasPath, "HUD_GreenFill");
        Sprite ornament = LoadSprite(AtlasPath, "HUD_LeftOrnament");
        Sprite frameOverlay = AssetDatabase.LoadAssetAtPath<Sprite>(FrameOverlayPath);

        GameObject prefabSource = BuildPrefabSource(
            frameOverlay,
            ornament,
            healthFill,
            manaFill,
            staminaFill);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, PrefabPath);
        Object.DestroyImmediate(prefabSource);

        Canvas canvas = FindOrCreateScreenCanvas();
        Transform existing = canvas.transform.Find("PlayerHUD");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        instance.name = "PlayerHUD";
        RectTransform instanceRect = instance.GetComponent<RectTransform>();
        instanceRect.localScale = Vector3.one;
        instanceRect.localRotation = Quaternion.identity;
        instanceRect.anchoredPosition = new Vector2(30f, -24f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instanceRect);

        WirePresenterToPlayer(instance.GetComponent<PlayerHUDPresenter>());

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Selection.activeGameObject = instance;

        Debug.Log($"[Day4 Task1] Player HUD built at {canvas.name}/PlayerHUD and saved to {PrefabPath}.");
    }

    private static void ConfigureAtlas()
    {
        TextureImporter importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (importer == null)
        {
            throw new FileNotFoundException($"HUD atlas was not found at {AtlasPath}.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        var existingSpriteIds = dataProvider.GetSpriteRects()
            .GroupBy(spriteRect => spriteRect.name)
            .ToDictionary(group => group.Key, group => group.First().spriteID);

        SpriteRect[] spriteRects =
        {
            CreateSpriteRect("HUD_BarBackground", 402f, 397f, 880f, 76f, new Vector4(44f, 18f, 44f, 18f), existingSpriteIds),
            CreateSpriteRect("HUD_RedFill", 413f, 334f, 895f, 57f, Vector4.zero, existingSpriteIds),
            CreateSpriteRect("HUD_BlueFill", 413f, 280f, 895f, 55f, Vector4.zero, existingSpriteIds),
            CreateSpriteRect("HUD_GreenFill", 413f, 222f, 895f, 58f, Vector4.zero, existingSpriteIds),
            CreateSpriteRect("HUD_LeftOrnament", 414f, 57f, 126f, 125f, Vector4.zero, existingSpriteIds)
        };

        dataProvider.SetSpriteRects(spriteRects);
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static SpriteRect CreateSpriteRect(
        string name,
        float x,
        float y,
        float width,
        float height,
        Vector4 border,
        System.Collections.Generic.IReadOnlyDictionary<string, GUID> existingSpriteIds)
    {
        return new SpriteRect
        {
            name = name,
            rect = new Rect(x, y, width, height),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            border = border,
            spriteID = existingSpriteIds.TryGetValue(name, out GUID spriteId) ? spriteId : GUID.Generate()
        };
    }

    private static void GenerateFrameOverlay()
    {
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        Color[] pixels = atlas.GetPixels(402, 397, 880, 76);

        for (int y = 18; y < 59; y++)
        {
            for (int x = 46; x < 835; x++)
            {
                Color pixel = pixels[y * 880 + x];
                pixel.a = 0f;
                pixels[y * 880 + x] = pixel;
            }
        }

        Texture2D overlay = new Texture2D(880, 76, TextureFormat.RGBA32, false);
        overlay.SetPixels(pixels);
        overlay.Apply();

        string absolutePath = Path.GetFullPath(FrameOverlayPath);
        File.WriteAllBytes(absolutePath, overlay.EncodeToPNG());
        Object.DestroyImmediate(overlay);
        AssetDatabase.ImportAsset(FrameOverlayPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(FrameOverlayPath) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(44f, 18f, 44f, 18f);
        importer.SaveAndReimport();
    }

    private static GameObject BuildPrefabSource(
        Sprite frameOverlay,
        Sprite ornament,
        Sprite healthFill,
        Sprite manaFill,
        Sprite staminaFill)
    {
        GameObject root = new GameObject(
            "PlayerHUD",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(PlayerHUDView),
            typeof(PlayerHUDPresenter));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetTopLeft(rootRect, new Vector2(30f, -24f), new Vector2(590f, 116f));
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject statusBars = new GameObject("StatusBars", typeof(RectTransform));
        RectTransform statusRect = statusBars.GetComponent<RectTransform>();
        statusRect.SetParent(rootRect, false);
        SetTopLeft(statusRect, Vector2.zero, new Vector2(590f, 116f));

        Image health = CreateBar(statusRect, "HealthBar", 550f, 0f, frameOverlay, ornament, healthFill);
        Image mana = CreateBar(statusRect, "ManaBar", 475f, -36f, frameOverlay, ornament, manaFill);
        Image stamina = CreateBar(statusRect, "StaminaBar", 535f, -72f, frameOverlay, ornament, staminaFill);

        SerializedObject hudView = new SerializedObject(root.GetComponent<PlayerHUDView>());
        hudView.FindProperty("_healthFill").objectReferenceValue = health;
        hudView.FindProperty("_manaFill").objectReferenceValue = mana;
        hudView.FindProperty("_staminaFill").objectReferenceValue = stamina;
        hudView.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject presenter = new SerializedObject(root.GetComponent<PlayerHUDPresenter>());
        presenter.FindProperty("_view").objectReferenceValue = root.GetComponent<PlayerHUDView>();
        presenter.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static Image CreateBar(
        RectTransform parent,
        string name,
        float width,
        float y,
        Sprite frameOverlay,
        Sprite ornament,
        Sprite fillSprite)
    {
        GameObject bar = new GameObject(name, typeof(RectTransform));
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.SetParent(parent, false);
        SetTopLeft(barRect, new Vector2(0f, y), new Vector2(width, 32f));

        Image background = CreateImage(barRect, "Background", null);
        Stretch(background.rectTransform, 80.71f, -38.8f, 5f, 5f);
        background.color = new Color(0.025f, 0.025f, 0.025f, 0.9f);

        Image fill = CreateImage(barRect, "Fill", fillSprite);
        Stretch(fill.rectTransform, 80.71f, -38.8f, 5f, 5f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        Image frame = CreateImage(barRect, "Frame", frameOverlay);
        Stretch(frame.rectTransform, 38.9f, 0f, -7.58f, -9.82f);
        frame.type = Image.Type.Sliced;

        Image leftOrnament = CreateImage(barRect, "LeftOrnament", ornament);
        RectTransform ornamentRect = leftOrnament.rectTransform;
        ornamentRect.anchorMin = new Vector2(0f, 0.5f);
        ornamentRect.anchorMax = new Vector2(0f, 0.5f);
        ornamentRect.pivot = new Vector2(0.5f, 0.5f);
        ornamentRect.anchoredPosition = new Vector2(32f, 0f);
        ornamentRect.sizeDelta = new Vector2(38f, 38f);
        leftOrnament.preserveAspect = true;

        return fill;
    }

    private static Image CreateImage(RectTransform parent, string name, Sprite sprite)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, -top);
    }

    private static Sprite LoadSprite(string path, string spriteName)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .FirstOrDefault(candidate => candidate.name == spriteName);

        if (sprite == null)
        {
            throw new MissingReferenceException($"Sprite '{spriteName}' was not found in {path}.");
        }

        return sprite;
    }

    private static Canvas FindOrCreateScreenCanvas()
    {
        Canvas canvas = Object.FindObjectsOfType<Canvas>(true)
            .FirstOrDefault(candidate => candidate.renderMode == RenderMode.ScreenSpaceOverlay);

        if (canvas != null)
        {
            CanvasScaler existingScaler = canvas.GetComponent<CanvasScaler>();
            if (existingScaler == null)
            {
                existingScaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            }

            ConfigureScaler(existingScaler);
            return canvas;
        }

        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Player HUD Canvas");

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ConfigureScaler(canvasObject.GetComponent<CanvasScaler>());
        return canvas;
    }

    private static void ConfigureScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void WirePresenterToPlayer(PlayerHUDPresenter presenter)
    {
        if (presenter == null)
            return;

        PlayerHealth health = Object.FindObjectsOfType<PlayerHealth>(true)
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid());
        PlayerMana mana = Object.FindObjectsOfType<PlayerMana>(true)
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid());
        PlayerStamina stamina = Object.FindObjectsOfType<PlayerStamina>(true)
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid());

        SerializedObject serializedPresenter = new SerializedObject(presenter);
        serializedPresenter.FindProperty("_view").objectReferenceValue = presenter.GetComponent<PlayerHUDView>();
        serializedPresenter.FindProperty("_health").objectReferenceValue = health;
        serializedPresenter.FindProperty("_mana").objectReferenceValue = mana;
        serializedPresenter.FindProperty("_stamina").objectReferenceValue = stamina;
        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
    }
}
