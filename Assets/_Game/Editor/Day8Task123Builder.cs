using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class Day8Task123Builder
{
    public const string ScenePath = "Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity";
    public const string ConfigPath = "Assets/_Game/Configs/Player/SO_PlayerProgression_Default.asset";
    public const string HudPrefabPath = "Assets/_Game/UI/Prefabs/PF_SoulHUD.prefab";
    private const string TexturePath = "Assets/_Game/UI/Textures/SoulHUD_Frame.png";
    private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player_Day1.prefab";
    private const string EnemyPrefabPath = "Assets/_Game/Prefabs/Characters/EnemyDummy_Day0.prefab";

    [MenuItem("Tools/SoulsLike RPG/Day8/Build Task1-3 In Open Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode 再配置 Day8 场景。");

        PlayerHealth player = Object.FindObjectsOfType<PlayerHealth>(true)
            .Single(candidate => candidate.gameObject.scene ==
                EditorSceneManager.GetActiveScene());
        PlayerProgressionConfig config = CreateConfig();
        Sprite frame = ImportFrame();
        ConfigureCharacterPrefabs(config);
        GameObject hudPrefab = CreateHudPrefab(frame);

        SoulWallet wallet = ConfigurePlayer(player.gameObject, config);
        foreach (EnemyHealth enemy in Object.FindObjectsOfType<EnemyHealth>(true)
            .Where(candidate => candidate.gameObject.scene == player.gameObject.scene))
        {
            EnemyReward reward = GetOrAdd<EnemyReward>(enemy.gameObject);
            SetReference(reward, "_wallet", wallet);
        }

        PlayerHUDPresenter playerHud = Object.FindObjectsOfType<PlayerHUDPresenter>(true)
            .FirstOrDefault(candidate => candidate.gameObject.scene == player.gameObject.scene);
        Canvas canvas = playerHud != null ? playerHud.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            throw new MissingReferenceException("当前场景需要现有 PlayerHUD Canvas。");

        SoulHUDPresenter soulHud = canvas.GetComponentInChildren<SoulHUDPresenter>(true);
        if (soulHud == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                hudPrefab, canvas.transform);
            instance.name = "SoulHUD";
            soulHud = instance.GetComponent<SoulHUDPresenter>();
        }
        SetReference(soulHud, "_wallet", wallet);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log($"[Day8 Task1-3] Saved {player.gameObject.scene.path}; " +
            $"SoulHUD={canvas.name}/SoulHUD, Wallet={wallet.name}.");
    }

    public static void BuildCheckpointSceneBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Build();
    }

    private static PlayerProgressionConfig CreateConfig()
    {
        PlayerProgressionConfig config =
            AssetDatabase.LoadAssetAtPath<PlayerProgressionConfig>(ConfigPath);
        if (config != null)
            return config;

        config = ScriptableObject.CreateInstance<PlayerProgressionConfig>();
        AssetDatabase.CreateAsset(config, ConfigPath);
        return config;
    }

    private static Sprite ImportFrame()
    {
        if (!File.Exists(TexturePath))
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../实现计划/Day8/升级系统与金币系统.assets/ChatGPT 图像 2026年9月28日 19_28_04.png"));
            File.Copy(source, TexturePath);
        }
        AssetDatabase.ImportAsset(TexturePath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
    }

    private static void ConfigureCharacterPrefabs(PlayerProgressionConfig config)
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            ConfigurePlayer(player, config);
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }

        GameObject enemy = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            GetOrAdd<EnemyReward>(enemy);
            PrefabUtility.SaveAsPrefabAsset(enemy, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(enemy);
        }
    }

    private static SoulWallet ConfigurePlayer(GameObject player, PlayerProgressionConfig config)
    {
        SoulWallet wallet = GetOrAdd<SoulWallet>(player);
        PlayerProgression progression = GetOrAdd<PlayerProgression>(player);
        SerializedObject serialized = new SerializedObject(progression);
        if (serialized.FindProperty("_config").objectReferenceValue == null)
            SetReference(progression, "_config", config);
        return wallet;
    }

    private static GameObject CreateHudPrefab(Sprite frame)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        if (existing != null)
            return existing;

        GameObject root = new GameObject("SoulHUD", typeof(RectTransform),
            typeof(CanvasGroup), typeof(SoulHUDView), typeof(SoulHUDPresenter));
        try
        {
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-32f, 20f);
            rect.sizeDelta = new Vector2(420f, 140f);
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            GameObject background = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.SetParent(rect, false);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            Image image = background.GetComponent<Image>();
            image.sprite = frame;
            image.preserveAspect = true;
            image.raycastTarget = false;

            GameObject textObject = new GameObject("SoulText", typeof(RectTransform),
                typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = new Vector2(0.29f, 0.33f);
            textRect.anchorMax = new Vector2(0.88f, 0.62f);
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 32f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 32f;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.color = new Color(1f, 0.92f, 0.72f);
            text.raycastTarget = false;
            text.text = "0";

            SetReference(root.GetComponent<SoulHUDView>(), "_soulText", text);
            SetReference(root.GetComponent<SoulHUDPresenter>(), "_view",
                root.GetComponent<SoulHUDView>());
            return PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetReference(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
