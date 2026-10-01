using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class Day12Setup
{
    public const string ScenePath = "Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity";
    public const string ConfigFolder = "Assets/_Game/Configs/Consumables/";
    private const string PlayerPath = "Assets/_Game/Prefabs/Characters/Player_Day1.prefab";
    private const string HudPath = "Assets/_Game/UI/Prefabs/PF_HealingFlaskUI.prefab";

    [MenuItem("Tools/SoulsLike RPG/Day12/1 Setup Consumables")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != ScenePath)
            throw new InvalidOperationException("请在已保存的主场景 Edit Mode 执行 Day12 设置。");
        Directory.CreateDirectory("Logs/Day12/Backup");
        foreach (string path in new[] { ScenePath, PlayerPath, HudPath })
        {
            string backup = "Logs/Day12/Backup/" + Path.GetFileName(path);
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        EnsureFolder(ConfigFolder.TrimEnd('/'));
        Material red = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Items/M_HealingFlask_Optimized.mat");
        Material blue = CreateManaMaterial(red);
        Sprite hpIcon = ImportIcon("Assets/_Game/UI/Textures/HealingFlask.png");
        Sprite mpIcon = ImportIcon("Assets/_Game/UI/Textures/MPFlask_Icon.png");
        RestoreHealthEffect hpEffect = Asset<RestoreHealthEffect>("EF_RestoreHealth40");
        RestoreManaEffect mpEffect = Asset<RestoreManaEffect>("EF_RestoreMana50");
        Set(hpEffect, "_amount", 40);
        Set(mpEffect, "_amount", 50f);
        ConsumableData hp = ConfigureData("SO_HPFlask", "hp_flask", "红露滴圣杯瓶", hpIcon, hpEffect);
        ConsumableData mp = ConfigureData("SO_MPFlask", "mp_flask", "蓝露滴圣杯瓶", mpIcon, mpEffect);

        GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPath);
        try { ConfigurePlayer(prefab, hp, mp, red, blue); PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        PlayerStateMachine player = Object.FindObjectsOfType<PlayerStateMachine>(true).Single(p => p.gameObject.scene == scene);
        ConfigurePlayer(player.gameObject, hp, mp, red, blue);
        GameObject hud = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            hud.GetComponent<QuickItemView>().SetItem(hp);
            hud.GetComponent<QuickItemView>().SetCharges(3, 3);
            PrefabUtility.SaveAsPrefabAsset(hud, HudPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }
        QuickItemPresenter presenter = Object.FindObjectsOfType<QuickItemPresenter>(true).Single(p => p.gameObject.scene == scene);
        Undo.RecordObject(presenter, "Bind quick item HUD");
        Set(presenter, "_items", player.Items != null ? player.Items : player.GetComponent<PlayerItemController>());
        presenter.GetComponent<QuickItemView>().SetItem(hp);
        presenter.GetComponent<QuickItemView>().SetCharges(3, 3);
        foreach (Component c in presenter.GetComponentsInChildren<Component>(true)) Record(c);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/Day12/Setup.txt", "Scene=" + scene.path +
            "\nPlayerPrefab=" + PlayerPath + "\nHUD=" + HudPath +
            "\nSlots=HP,MP; charges=3,3; effects=HP40,MP50; consume=.45; complete=.95; movement=.4\n" +
            "Keyboard=1 switch, R use; Gamepad=DPadDown switch, X use\nSaved=True\n");
        Debug.Log("[Day12] 消耗品与双槽已配置并保存。1 切换，R 使用。");
    }

    private static ConsumableData ConfigureData(string name, string id, string display, Sprite icon, ConsumableEffect effect)
    {
        var data = Asset<ConsumableData>(name);
        var so = new SerializedObject(data);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_displayName").stringValue = display;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_maxCharges").intValue = 3;
        so.FindProperty("_consumePoint").floatValue = .45f;
        so.FindProperty("_completionPoint").floatValue = .95f;
        so.FindProperty("_movementMultiplier").floatValue = .4f;
        var effects = so.FindProperty("_effects"); effects.arraySize = 1;
        effects.GetArrayElementAtIndex(0).objectReferenceValue = effect;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void ConfigurePlayer(GameObject player, ConsumableData hpData, ConsumableData mpData, Material red, Material blue)
    {
        PlayerHealingFlask hp = player.GetComponent<PlayerHealingFlask>();
        if (hp == null) throw new MissingReferenceException("玩家缺少旧回血瓶组件，停止迁移。");
        var hpSerialized = new SerializedObject(hp);
        GameObject held = hpSerialized.FindProperty("_heldBottle").objectReferenceValue as GameObject;
        if (held == null) throw new MissingReferenceException("玩家缺少已验证的手持瓶子，停止迁移。");
        Set(hp, "_data", hpData);
        held.SetActive(false);
        Tint(held, red);
        PlayerConsumable mp = player.GetComponents<PlayerConsumable>().FirstOrDefault(p => p != hp);
        if (mp == null) mp = Undo.AddComponent<PlayerConsumable>(player);
        Transform existing = held.transform.parent.Find("MPFlask");
        GameObject mpHeld = existing != null ? existing.gameObject : Object.Instantiate(held, held.transform.parent);
        mpHeld.name = "MPFlask";
        mpHeld.transform.SetParent(held.transform.parent, false);
        CopyPose(held.transform, mpHeld.transform);
        Tint(mpHeld, blue);
        mpHeld.SetActive(false);
        Set(mp, "_data", mpData);
        Set(mp, "_heldBottle", mpHeld);
        var items = new SerializedObject(player.GetComponent<PlayerItemController>());
        var slots = items.FindProperty("_quickItemSlots"); slots.arraySize = 2;
        slots.GetArrayElementAtIndex(0).objectReferenceValue = hp;
        slots.GetArrayElementAtIndex(1).objectReferenceValue = mp;
        items.FindProperty("_defaultSlot").intValue = 0;
        items.ApplyModifiedPropertiesWithoutUndo();
        // 所有当前可装备武器都参与隐藏/恢复；避免第二把武器残留。
        var visibility = new SerializedObject(player.GetComponent<PlayerWeaponVisibility>());
        Renderer[] weapons = player.GetComponentsInChildren<WeaponHitbox>(true)
            .SelectMany(w => w.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
        var renderers = visibility.FindProperty("_weaponRenderers"); renderers.arraySize = weapons.Length;
        for (int i = 0; i < weapons.Length; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
        visibility.ApplyModifiedPropertiesWithoutUndo();
        Record(hp); Record(mp); Record(player.GetComponent<PlayerItemController>()); Record(player.GetComponent<PlayerWeaponVisibility>());
        foreach (Renderer r in held.GetComponentsInChildren<Renderer>(true)) Record(r);
        Record(held);
    }

    public static void InspectMounts()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var player = Object.FindObjectsOfType<PlayerStateMachine>(true).Single(p => p.gameObject.scene == scene);
        File.WriteAllText("Logs/Day12/Mounts.txt", "Scene\n" + DescribeMounts(player.gameObject) + "\nPrefab\n" +
            DescribeMounts(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath)));
    }

    private static string DescribeMounts(GameObject player)
    {
        return string.Join("\n", player.GetComponents<PlayerConsumable>().Select(item =>
        {
            var root = (GameObject)new SerializedObject(item).FindProperty("_heldBottle").objectReferenceValue;
            return item.Data.name + " parent=" + AnimationUtility.CalculateTransformPath(root.transform.parent, player.transform) +
                "\n" + string.Join("\n", root.GetComponentsInChildren<Transform>(true).Select(t =>
                    AnimationUtility.CalculateTransformPath(t, root.transform) + " pos=" + t.localPosition.ToString("F6") +
                    " rot=" + t.localRotation.ToString("F6") + " scale=" + t.localScale.ToString("F6")));
        }));
    }

    [MenuItem("Tools/SoulsLike RPG/Day12/4 Align MP Flask To HP Mount")]
    public static void AlignMounts()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != ScenePath)
            throw new InvalidOperationException("需要已保存的 Edit Mode 主场景。");
        var player = Object.FindObjectsOfType<PlayerStateMachine>(true).Single(p => p.gameObject.scene == scene);
        File.WriteAllText("Logs/Day12/MountsBefore.txt", DescribeMounts(player.gameObject));
        // 场景当前红瓶是用户调好的依据，包括模型子节点的 Prefab Override。
        var source = ((GameObject)new SerializedObject(player.GetComponent<PlayerHealingFlask>()).FindProperty("_heldBottle").objectReferenceValue).transform;
        var prefab = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            AlignPlayerMount(prefab, source);
            PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AlignPlayerMount(player.gameObject, source);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/Day12/MountsAfter.txt", DescribeMounts(player.gameObject));
    }

    private static void AlignPlayerMount(GameObject player, Transform source)
    {
        var hp = player.GetComponent<PlayerHealingFlask>();
        var mp = player.GetComponents<PlayerConsumable>().Single(p => p != hp);
        var red = ((GameObject)new SerializedObject(hp).FindProperty("_heldBottle").objectReferenceValue).transform;
        var blue = ((GameObject)new SerializedObject(mp).FindProperty("_heldBottle").objectReferenceValue).transform;
        if (red != source) CopyPose(source, red);
        blue.SetParent(red.parent, false);
        CopyPose(source, blue);
    }

    private static void CopyPose(Transform source, Transform target)
    {
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        Record(target);
        if (source.childCount != target.childCount)
            throw new InvalidOperationException("红蓝瓶模型层级不一致，无法安全对齐。");
        for (int i = 0; i < source.childCount; i++) CopyPose(source.GetChild(i), target.GetChild(i));
    }

    private static void Tint(GameObject root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
    }
    private static void Record(Object obj)
    {
        EditorUtility.SetDirty(obj);
        if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
    }
    private static void Set(Object obj, string field, object value)
    {
        var data = new SerializedObject(obj);
        var p = data.FindProperty(field);
        if (value is int integer) p.intValue = integer;
        else if (value is float number) p.floatValue = number;
        else p.objectReferenceValue = (Object)value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Asset<T>(string name) where T : ScriptableObject
    {
        string path = ConfigFolder + name + ".asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); asset.name = name; AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    private static Material CreateManaMaterial(Material original)
    {
        const string texturePath = "Assets/_Game/Textures/Items/MPFlask_BaseColor.png";
        const string materialPath = "Assets/_Game/Materials/Items/M_MPFlask_Blue.mat";
        if (!File.Exists(texturePath)) throw new FileNotFoundException("缺少蓝色区域变体贴图。");
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(original) { name = "M_MPFlask_Blue" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.CopyPropertiesFromMaterial(original);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        EditorUtility.SetDirty(material);
        return material;
    }
    private static Sprite ImportIcon(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
