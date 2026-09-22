using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Day5 锁定标记 UI 装配器（可重复执行）：
/// 1. 配置 LockOnIndicator.png 的导入设置（Sprite / Single / Alpha / 无 Mipmap）；
/// 2. 在主 Canvas 下创建唯一的 LockOnIndicator（容器挂 LockOnIndicatorUI，
///    子级 Image 显示锁定图标），不挂在任何 Enemy / Player 层级下；
/// 3. 绑定 PlayerTargeting / Camera / Image / Canvas 引用并保存场景。
/// </summary>
public static class Day5LockOnIndicatorBuilder
{
    private const string SpritePath =
        "Assets/_Game/UI/Textures/LockOn/LockOnIndicator.png";

    private const float IndicatorSize = 48f;

    [MenuItem("Tools/Day5/Build LockOn Indicator UI")]
    public static void Build()
    {
        if (!ConfigureSpriteImporter())
            return;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            Debug.LogError(
                $"找不到锁定标记 Sprite：{SpritePath}，" +
                "请确认 PNG 已复制到该路径。"
            );
            return;
        }

        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            Debug.LogError("场景中找不到 UI Canvas，装配中止。");
            return;
        }

        PlayerTargeting targeting = Object.FindObjectOfType<PlayerTargeting>();
        Camera mainCamera = Camera.main;
        if (targeting == null || mainCamera == null)
        {
            Debug.LogError("场景中缺少 PlayerTargeting 或 MainCamera，装配中止。");
            return;
        }

        RectTransform containerRect = EnsureContainer(canvas.transform);
        RectTransform imageRect = EnsureIndicatorImage(
            containerRect,
            sprite);

        LockOnIndicatorUI ui = ConfigureComponent(
            containerRect.gameObject,
            targeting,
            mainCamera,
            imageRect,
            canvas.GetComponent<RectTransform>());

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Selection.activeGameObject = containerRect.gameObject;

        Debug.Log(
            "Day5 锁定标记 UI 已装配完成：" +
            $"{canvas.name}/LockOnIndicator（Image 初始隐藏，引用已绑定）。"
        );
    }

    /// <summary>配置 PNG 的 Sprite 导入设置。</summary>
    private static bool ConfigureSpriteImporter()
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(SpritePath) as TextureImporter;

        if (importer == null)
        {
            Debug.LogError(
                $"找不到贴图资产：{SpritePath}（请先把 PNG 放进项目）。"
            );
            return false;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return true;
    }

    /// <summary>创建或更新 LockOnIndicator 容器（脚本宿主 + 定位点）。</summary>
    private static RectTransform EnsureContainer(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("LockOnIndicator");

        GameObject containerObject;
        if (existing != null)
        {
            containerObject = existing.gameObject;
        }
        else
        {
            containerObject = new GameObject(
                "LockOnIndicator",
                typeof(RectTransform));
            containerObject.transform.SetParent(canvasTransform, false);
        }

        RectTransform rect = containerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(IndicatorSize, IndicatorSize);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        return rect;
    }

    /// <summary>创建或更新 Image 子级（显示锁定图标，初始隐藏）。</summary>
    private static RectTransform EnsureIndicatorImage(
        RectTransform containerRect,
        Sprite sprite
    )
    {
        Transform existing = containerRect.Find("Image");

        GameObject imageObject;
        if (existing != null)
        {
            imageObject = existing.gameObject;
        }
        else
        {
            imageObject = new GameObject(
                "Image",
                typeof(RectTransform),
                typeof(Image));
            imageObject.transform.SetParent(containerRect, false);
        }

        if (imageObject.GetComponent<Image>() == null)
            imageObject.AddComponent<Image>();

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(IndicatorSize, IndicatorSize);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        // 初始隐藏，由 LockOnIndicatorUI 在锁定时显示。
        imageObject.SetActive(false);

        return rect;
    }

    /// <summary>挂载并绑定 LockOnIndicatorUI 的引用。</summary>
    private static LockOnIndicatorUI ConfigureComponent(
        GameObject containerObject,
        PlayerTargeting targeting,
        Camera mainCamera,
        RectTransform imageRect,
        RectTransform canvasRect
    )
    {
        LockOnIndicatorUI ui =
            containerObject.GetComponent<LockOnIndicatorUI>();
        if (ui == null)
            ui = containerObject.AddComponent<LockOnIndicatorUI>();

        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("_targeting").objectReferenceValue = targeting;
        serialized.FindProperty("_camera").objectReferenceValue = mainCamera;
        serialized.FindProperty("_indicator").objectReferenceValue = imageRect;
        serialized.FindProperty("_canvasRect").objectReferenceValue = canvasRect;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return ui;
    }

    /// <summary>从 PlayerHUD 反推主 Canvas（避免误选其他 Canvas）。</summary>
    private static Canvas FindMainCanvas()
    {
        GameObject hud = GameObject.Find("PlayerHUD");
        if (hud != null)
        {
            Canvas canvas = hud.GetComponentInParent<Canvas>();
            if (canvas != null)
                return canvas;
        }

        GameObject named = GameObject.Find("Canvas");
        if (named != null)
        {
            Canvas canvas = named.GetComponent<Canvas>();
            if (canvas != null)
                return canvas;
        }

        return Object.FindObjectOfType<Canvas>();
    }
}
