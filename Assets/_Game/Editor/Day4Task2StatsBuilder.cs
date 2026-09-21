using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Day4Task2StatsBuilder
{
    private const string ConfigPath = "Assets/_Game/Configs/Player/SO_PlayerStats_Default.asset";
    private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player_Day1.prefab";
    private const string HudPrefabPath = "Assets/_Game/UI/Prefabs/PF_PlayerHUD.prefab";

    [MenuItem("Tools/SoulsLike RPG/Day4/Build Task2 Player Stats")]
    public static void Build()
    {
        PlayerStatsConfig config = CreateOrLoadConfig();
        ConfigurePlayerPrefab(config);
        ConfigureHudPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireOpenScene();
        Debug.Log(
            $"[Day4 Task2] Player stats configured with {ConfigPath}; PlayerHUD is bound to the scene player.");
    }

    [MenuItem("Tools/SoulsLike RPG/Day4/Run Task2 Runtime Probe")]
    public static void RunRuntimeProbe()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Day4 Task2] Runtime Probe 需要在 Play Mode 中执行。");
            return;
        }

        PlayerHealth health = FindSceneComponent<PlayerHealth>();
        PlayerMana mana = FindSceneComponent<PlayerMana>();
        PlayerStamina stamina = FindSceneComponent<PlayerStamina>();

        if (health == null || mana == null || stamina == null)
        {
            Debug.LogError("[Day4 Task2] Runtime Probe 找不到完整的玩家数据组件。");
            return;
        }

        health.TakeDamage(new DamageInfo { Damage = 25 });
        bool manaConsumed = mana.Consume(30f);
        bool staminaConsumed = stamina.Consume(40f);

        Debug.Log(
            $"[Day4 Task2] Runtime Probe: HP={health.CurrentHealth}/{health.MaxHealth}, " +
            $"MP={mana.CurrentMana:0.##}/{mana.MaxMana:0.##} (Consume={manaConsumed}), " +
            $"SP={stamina.CurrentStamina:0.##}/{stamina.MaxStamina:0.##} (Consume={staminaConsumed}).");
    }

    private static PlayerStatsConfig CreateOrLoadConfig()
    {
        PlayerStatsConfig config = AssetDatabase.LoadAssetAtPath<PlayerStatsConfig>(ConfigPath);
        if (config != null)
            return config;

        string directory = Path.GetDirectoryName(ConfigPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
        {
            throw new DirectoryNotFoundException($"Config directory does not exist: {directory}");
        }

        config = ScriptableObject.CreateInstance<PlayerStatsConfig>();
        AssetDatabase.CreateAsset(config, ConfigPath);
        return config;
    }

    private static void ConfigurePlayerPrefab(PlayerStatsConfig config)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerHealth health = GetOrAddComponent<PlayerHealth>(root);
            PlayerMana mana = GetOrAddComponent<PlayerMana>(root);
            PlayerStamina stamina = GetOrAddComponent<PlayerStamina>(root);

            SetConfig(health, config);
            SetConfig(mana, config);
            SetConfig(stamina, config);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureHudPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            PlayerHUDView view = root.GetComponent<PlayerHUDView>();
            if (view == null)
                throw new MissingComponentException($"{HudPrefabPath} is missing PlayerHUDView.");

            PlayerHUDPresenter presenter = GetOrAddComponent<PlayerHUDPresenter>(root);
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireOpenScene()
    {
        PlayerHealth health = FindSceneComponent<PlayerHealth>();
        PlayerMana mana = FindSceneComponent<PlayerMana>();
        PlayerStamina stamina = FindSceneComponent<PlayerStamina>();
        PlayerHUDPresenter presenter = FindSceneComponent<PlayerHUDPresenter>();

        if (health == null || mana == null || stamina == null || presenter == null)
        {
            throw new MissingReferenceException(
                "Open scene must contain PlayerHealth, PlayerMana, PlayerStamina and PlayerHUDPresenter.");
        }

        SerializedObject serializedPresenter = new SerializedObject(presenter);
        serializedPresenter.FindProperty("_view").objectReferenceValue = presenter.GetComponent<PlayerHUDView>();
        serializedPresenter.FindProperty("_health").objectReferenceValue = health;
        serializedPresenter.FindProperty("_mana").objectReferenceValue = mana;
        serializedPresenter.FindProperty("_stamina").objectReferenceValue = stamina;
        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);

        EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
        EditorSceneManager.SaveScene(presenter.gameObject.scene);
        Selection.activeGameObject = health.gameObject;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetConfig(Object component, PlayerStatsConfig config)
    {
        SerializedObject serializedComponent = new SerializedObject(component);
        serializedComponent.FindProperty("_config").objectReferenceValue = config;
        serializedComponent.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        return Object.FindObjectsOfType<T>(true)
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid());
    }
}
