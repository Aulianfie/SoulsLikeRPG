using System;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class Day8Task4567Builder
{
    public const string PrefabPath = "Assets/_Game/UI/Prefabs/PF_GraceProgressionUI.prefab";
    private static readonly Color Gold = new Color(0.82f, 0.69f, 0.43f);
    private static readonly Color Light = new Color(0.94f, 0.92f, 0.85f);

    [MenuItem("Tools/SoulsLike RPG/Day8/Build Task4-7 In Open Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");

        var scene = EditorSceneManager.GetActiveScene();
        PlayerProgression player = Object.FindObjectsOfType<PlayerProgression>(true)
            .Single(candidate => candidate.gameObject.scene == scene);
        CheckpointManager checkpoints = Object.FindObjectsOfType<CheckpointManager>(true)
            .Single(candidate => candidate.gameObject.scene == scene);
        if (Object.FindObjectsOfType<EventSystem>(true).All(candidate => candidate.gameObject.scene != scene))
            throw new MissingReferenceException("场景缺少 EventSystem。");

        PlayerProgressionConfig config = AssetDatabase.LoadAssetAtPath<PlayerProgressionConfig>(
            Day8Task123Builder.ConfigPath);
        EditorUtility.SetDirty(config);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            prefab = CreatePrefab();

        ProgressionPresenter presenter = Object.FindObjectsOfType<ProgressionPresenter>(true)
            .FirstOrDefault(candidate => candidate.gameObject.scene == scene);
        if (presenter == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "GraceProgressionUI";
            Undo.RegisterCreatedObjectUndo(instance, "Add Grace progression UI");
            presenter = instance.GetComponent<ProgressionPresenter>();
        }

        SerializedObject serialized = new SerializedObject(presenter);
        serialized.FindProperty("_checkpointManager").objectReferenceValue = checkpoints;
        serialized.FindProperty("_progression").objectReferenceValue = player;
        serialized.FindProperty("_wallet").objectReferenceValue = player.GetComponent<SoulWallet>();
        serialized.FindProperty("_inputReader").objectReferenceValue = player.GetComponent<PlayerInputReader>();
        serialized.FindProperty("_health").objectReferenceValue = player.GetComponent<PlayerHealth>();
        serialized.FindProperty("_freeLookCamera").objectReferenceValue =
            Object.FindObjectsOfType<CinemachineFreeLook>(true)
                .FirstOrDefault(candidate => candidate.gameObject.scene == scene);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
        EditorUtility.SetDirty(presenter);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Day8 Task4-7] Saved {scene.path}; UI prefab={PrefabPath}.");
    }

    private static GameObject CreatePrefab()
    {
        GameObject root = new GameObject("GraceProgressionUI", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(CanvasGroup), typeof(ProgressionPresenter));
        try
        {
            root.layer = 5;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Image backdrop = CreateImage(root.transform, "Backdrop", new Color(0f, 0f, 0f, 0.6f));
            RectTransform backdropRect = backdrop.rectTransform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;

            GraceMenuUI grace = CreateGraceMenu(root.transform);
            LevelUpPanel levelUp = CreateLevelUpPanel(root.transform);
            SerializedObject presenter = new SerializedObject(root.GetComponent<ProgressionPresenter>());
            presenter.FindProperty("_graceMenu").objectReferenceValue = grace;
            presenter.FindProperty("_levelUpPanel").objectReferenceValue = levelUp;
            presenter.ApplyModifiedPropertiesWithoutUndo();
            grace.gameObject.SetActive(false);
            levelUp.gameObject.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GraceMenuUI CreateGraceMenu(Transform parent)
    {
        RectTransform rect = CreatePanel(parent, "GraceMenu", new Vector2(560f, 370f));
        GraceMenuUI view = rect.gameObject.AddComponent<GraceMenuUI>();
        CreateText(rect, "Title", "GRACE", 40f, 26f, 480f, 58f, 36f, Gold,
            TextAlignmentOptions.Center);
        CreateText(rect, "Subtitle", "REST AT THE GRACE", 40f, 87f, 480f, 26f, 17f, Light,
            TextAlignmentOptions.Center);
        Button levelUp = CreateButton(rect, "LevelUpButton", ">  LEVEL UP", 40f, 144f, 480f, 60f);
        Button close = CreateButton(rect, "CloseButton", "CLOSE", 40f, 220f, 480f, 60f);
        CreateText(rect, "Hint", "ESC / B - CLOSE", 40f, 314f, 480f, 24f, 16f, Gold,
            TextAlignmentOptions.Center);
        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("_levelUpButton").objectReferenceValue = levelUp;
        serialized.FindProperty("_closeButton").objectReferenceValue = close;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static LevelUpPanel CreateLevelUpPanel(Transform parent)
    {
        RectTransform rect = CreatePanel(parent, "LevelUpPanel", new Vector2(760f, 760f));
        LevelUpPanel view = rect.gameObject.AddComponent<LevelUpPanel>();
        CreateText(rect, "Title", "LEVEL UP", 40f, 26f, 680f, 58f, 36f, Gold,
            TextAlignmentOptions.Center);
        CreateText(rect, "SoulLabel", "SOUL", 48f, 110f, 210f, 38f, 22f, Gold);
        TMP_Text souls = CreateText(rect, "SoulValue", "0", 380f, 110f, 332f, 38f, 28f, Light,
            TextAlignmentOptions.Right);
        CreateText(rect, "LevelLabel", "LEVEL", 48f, 156f, 210f, 38f, 22f, Gold);
        TMP_Text level = CreateText(rect, "LevelValue", "1", 380f, 156f, 332f, 38f, 26f, Light,
            TextAlignmentOptions.Right);
        CreateText(rect, "ColumnsHint", "CURRENT  >  AFTER", 398f, 195f, 314f, 24f, 15f, Gold,
            TextAlignmentOptions.Right);

        Button[] buttons = new Button[3];
        TMP_Text[] values = new TMP_Text[3];
        TMP_Text[] effects = new TMP_Text[3];
        string[] stats = { "VIGOR", "ENDURANCE", "STRENGTH" };
        string[] labels = { "HP", "Stamina", "Damage multiplier" };
        for (int i = 0; i < 3; i++)
        {
            Button button = CreateButton(rect, stats[i] + "Button", "", 40f, 229f + i * 99f, 680f, 86f);
            buttons[i] = button;
            CreateText(button.transform, "StatLabel", stats[i], 20f, 8f, 240f, 35f, 24f, Light);
            CreateText(button.transform, "EffectLabel", labels[i], 20f, 48f, 240f, 25f, 18f, Gold);
            values[i] = CreateText(button.transform, "StatValue", "1", 290f, 8f, 368f, 35f, 26f, Light,
                TextAlignmentOptions.Right);
            effects[i] = CreateText(button.transform, "EffectValue", "100", 290f, 48f, 368f, 25f, 22f, Gold,
                TextAlignmentOptions.Right);
        }

        CreateText(rect, "CostLabel", "UPGRADE COST", 48f, 549f, 300f, 38f, 22f, Gold);
        TMP_Text cost = CreateText(rect, "CostValue", "150", 380f, 549f, 332f, 38f, 28f, Gold,
            TextAlignmentOptions.Right);
        TMP_Text status = CreateText(rect, "Status", "Select an attribute and confirm.",
            40f, 607f, 680f, 32f, 19f, Light, TextAlignmentOptions.Center);
        Button confirm = CreateButton(rect, "ConfirmButton", "CONFIRM", 40f, 659f, 326f, 56f);
        Button close = CreateButton(rect, "CloseButton", "CLOSE", 394f, 659f, 326f, 56f);
        CreateText(rect, "Hint", "ESC / B - BACK", 40f, 725f, 680f, 23f, 15f, Gold,
            TextAlignmentOptions.Center);

        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("_soulsText").objectReferenceValue = souls;
        serialized.FindProperty("_levelText").objectReferenceValue = level;
        serialized.FindProperty("_costText").objectReferenceValue = cost;
        serialized.FindProperty("_statusText").objectReferenceValue = status;
        serialized.FindProperty("_confirmButton").objectReferenceValue = confirm;
        serialized.FindProperty("_closeButton").objectReferenceValue = close;
        SetArray(serialized, "_statButtons", buttons);
        SetArray(serialized, "_statValueTexts", values);
        SetArray(serialized, "_effectTexts", effects);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 size)
    {
        Image panel = CreateImage(parent, name, new Color(0.045f, 0.05f, 0.055f, 0.98f));
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        Image top = CreateImage(rect, "TopLine", Gold);
        SetRect(top.rectTransform, 12f, 12f, size.x - 24f, 2f);
        top.raycastTarget = false;
        Image bottom = CreateImage(rect, "BottomLine", Gold);
        SetRect(bottom.rectTransform, 12f, size.y - 12f, size.x - 24f, 2f);
        bottom.raycastTarget = false;
        return rect;
    }

    private static Button CreateButton(Transform parent, string name, string label,
        float x, float y, float width, float height)
    {
        Image image = CreateImage(parent, name, new Color(0.12f, 0.13f, 0.14f));
        SetRect(image.rectTransform, x, y, width, height);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.35f, 1.25f, 1.05f);
        colors.selectedColor = new Color(1.35f, 1.25f, 1.05f);
        colors.pressedColor = new Color(0.8f, 0.72f, 0.55f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f);
        button.colors = colors;
        if (!string.IsNullOrEmpty(label))
            CreateText(image.transform, "Label", label, 0f, 0f, width, height, 22f, Light,
                TextAlignmentOptions.Center);
        return button;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value,
        float x, float y, float width, float height, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        SetRect(text.rectTransform, x, y, width, height);
        text.font = Day8ChineseUISetup.EnsureFont();
        text.text = Day8ChineseUISetup.Translate(value);
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static void SetRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetArray(SerializedObject serialized, string name, Object[] values)
    {
        SerializedProperty property = serialized.FindProperty(name);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

}
