using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Day10Setup
{
    public const string PlayerPath = "Assets/_Game/Prefabs/Characters/Player_Day1.prefab";
    public const string ConfigFolder = "Assets/_Game/Configs/Weapons";
    public const string GreatSwordPath = "Assets/_Game/Prefabs/Weapons/Weapon_GreatSword.prefab";
    private const string ControllerPath = "Assets/_Game/Animations/Controllers/AC_Player.controller";

    [MenuItem("Tools/SoulsLike RPG/Day10/Setup Weapons In Open Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play Mode。");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity")
            throw new InvalidOperationException("请打开 03_AncientDungeon_Checkpoint 主场景。");
        PlayerCombat player = Object.FindObjectsOfType<PlayerCombat>(true).Single(p => p.gameObject.scene == scene);
        Directory.CreateDirectory("Logs/Day10Backup");
        // 先备份包含用户未保存修改的内存场景，再原位接入装备。
        string copy = "Assets/_Game/Scenes/Day10_Before.unity";
        if (!EditorSceneManager.SaveScene(scene, copy, true)) throw new IOException("无法备份当前场景。");
        File.Copy(copy, "Logs/Day10Backup/SceneBeforeInMemory.unity", true);
        AssetDatabase.DeleteAsset(copy);
        EnsureFolder(ConfigFolder);
        AnimationClip[] heavyClips = ImportHeavyClips();
        GameObject heavyPrefab = BuildGreatSword();
        var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorState[] attacks = baseController.layers[0].stateMachine.states.Select(s => s.state)
            .Where(s => s.name.StartsWith("Attack")).OrderBy(s => s.name).ToArray();
        AnimatorOverrideController lightOverride = BuildOverride("AOC_LongSword", baseController, attacks, null);
        AnimatorOverrideController heavyOverride = BuildOverride("AOC_GreatSword", baseController, attacks, heavyClips);
        AttackCombo lightCombo = BuildCombo("LongSword", 5, false);
        AttackCombo heavyCombo = BuildCombo("GreatSword", 3, true);
        WeaponData light = BuildWeapon("LongSword", "长剑", WeaponType.StraightSword,
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Weapons/Weapon_OneHand.prefab"), lightCombo, lightOverride, 1f, 1f);
        WeaponData heavy = BuildWeapon("GreatSword", "大剑", WeaponType.GreatSword,
            heavyPrefab, heavyCombo, heavyOverride, 1.6f, 1.5f);
        GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            ConfigurePlayer(prefab, light, heavy);
            PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        ConfigurePlayer(player.gameObject, light, heavy);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/Day10_Setup.txt", $"Scene={scene.path}\nPlayer={player.name}\nPrefab={PlayerPath}\nLongSword=5 hits, damage x1, stamina x1\nGreatSword=3 hits, damage x1.6, stamina x1.5\n" +
            string.Join("\n", heavyClips.Select(c => $"Clip={c.name}; seconds={c.length}; human={c.humanMotion}")) + "\nSaved.\n");
        Debug.Log("[Day10] 默认长剑，鼠标滚轮切换长剑 / 大剑；仅移动状态允许切换。");
    }

    [MenuItem("Tools/SoulsLike RPG/Day10/Restore Five-Hit Sword And Three-Hit Greatsword")]
    public static void UpdateCombos()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play Mode。");
        AnimationClip[] heavyClips = ImportHeavyClips();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorState[] attacks = controller.layers[0].stateMachine.states.Select(s => s.state)
            .Where(s => s.name.StartsWith("Attack")).OrderBy(s => s.name).ToArray();
        BuildCombo("LongSword", 5, false);
        // 保留大剑已有两段的调参，只为缺失的第三段创建默认参数。
        BuildCombo("GreatSword", 3, true, true);
        BuildOverride("AOC_GreatSword", controller, attacks, heavyClips);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/Day10Combo_Setup.txt", "LongSword=5 original attacks; GreatSword=3 attacks\n" +
            string.Join("\n", heavyClips.Select(c => $"Clip={c.name}; seconds={c.length}; human={c.humanMotion}")) + "\nOnly combo assets and greatsword overrides updated.\n");
        Debug.Log("[Day10] 已恢复长剑五段，接入大剑第三段；场景、玩家预制体和挂点未修改。");
    }

    private static AnimationClip[] ImportHeavyClips()
    {
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/DoubleL/Models/T-Pose.fbx").OfType<Avatar>().Single();
        if (!avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("DoubleL Avatar 无效。");
        return Enumerable.Range(1, 3).Select(index =>
        {
            string path = $"Assets/ThirdParty/DoubleL/Animations/TwoHandBase/2Hand_Base_Attack_A_{index}_InPlace.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = false;
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            AnimationClip motion = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
            if (!motion.humanMotion) throw new InvalidOperationException("大剑动作非 Humanoid：" + path);
            return motion;
        }).ToArray();
    }

    private static GameObject BuildGreatSword()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GreatSwordPath);
        if (existing != null) return existing;
        const string modelPath = "Assets/ThirdParty/DoubleL/Models/SM_Wep_Sword_03.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        EnsureFolder("Assets/_Game/Materials/Weapons");
        var metal = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_GreatSword_Steel" };
        metal.SetColor("_BaseColor", new Color(0.48f, 0.52f, 0.57f));
        metal.SetFloat("_Metallic", 0.8f);
        metal.SetFloat("_Smoothness", 0.55f);
        string materialPath = "Assets/_Game/Materials/Weapons/M_GreatSword_Steel.mat";
        Material saved = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (saved == null) { AssetDatabase.CreateAsset(metal, materialPath); saved = metal; }
        else Object.DestroyImmediate(metal);
        var root = new GameObject("Weapon_GreatSword");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            model.name = "Model";
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = saved;
            // FBX 自带剑柄原点，保留模型坐标；挂点与现有剑共用。
            var shapeObject = new GameObject("BladeHitbox");
            shapeObject.transform.SetParent(model.transform, false);
            var shape = shapeObject.AddComponent<BoxCollider>();
            Bounds bounds = model.GetComponentsInChildren<MeshFilter>().First().sharedMesh.bounds;
            shape.center = bounds.center;
            shape.size = new Vector3(Mathf.Max(bounds.size.x, 0.16f), bounds.size.y * 0.8f, Mathf.Max(bounds.size.z, 0.16f));
            shape.isTrigger = true;
            shape.enabled = false;
            var hitbox = root.AddComponent<WeaponHitbox>();
            var data = new SerializedObject(hitbox);
            data.FindProperty("_shape").objectReferenceValue = shape;
            data.FindProperty("_targetLayers").intValue = 1 << LayerMask.NameToLayer("Enemy");
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, GreatSwordPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static AnimatorOverrideController BuildOverride(string name, AnimatorController controller, AnimatorState[] attacks, AnimationClip[] clips)
    {
        string path = "Assets/_Game/Animations/Controllers/" + name + ".overrideController";
        var result = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (result == null) { result = new AnimatorOverrideController(controller) { name = name }; AssetDatabase.CreateAsset(result, path); }
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        result.GetOverrides(overrides);
        // 只覆盖攻击片段，保留移动、喝药、闪避、受击、死亡等所有片段。
        for (int index = 0; index < overrides.Count; index++)
        {
            int attackIndex = Array.FindIndex(attacks, state => state.motion == overrides[index].Key);
            AnimationClip replacement = clips != null && attackIndex >= 0 && attackIndex < clips.Length ? clips[attackIndex] : null;
            overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[index].Key, replacement);
        }
        result.ApplyOverrides(overrides);
        EditorUtility.SetDirty(result);
        return result;
    }

    private static AttackCombo BuildCombo(string weapon, int count, bool heavy, bool preserveExisting = false)
    {
        AttackCombo combo = Asset<AttackCombo>($"{ConfigFolder}/Combo_{weapon}_Light.asset");
        var data = new SerializedObject(combo);
        var list = data.FindProperty("_attacks"); list.arraySize = count;
        for (int index = 0; index < count; index++)
        {
            string path = $"{ConfigFolder}/AD_{weapon}_{index + 1}.asset";
            AttackData attack = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (preserveExisting && attack != null)
            {
                list.GetArrayElementAtIndex(index).objectReferenceValue = attack;
                continue;
            }
            attack = Asset<AttackData>(path);
            AttackData source = AssetDatabase.LoadAssetAtPath<AttackData>($"Assets/_Game/Configs/Combat/AD_LightAttack_{index + 1}.asset");
            EditorUtility.CopySerialized(source, attack);
            attack.name = $"AD_{weapon}_{index + 1}";
            if (heavy)
            {
                var values = new SerializedObject(attack);
                values.FindProperty("_startTimeOffset").floatValue = index == 1 ? 0.25f : 0f;
                values.FindProperty("_damage").intValue = 25 + index * 5;
                values.FindProperty("_staminaCost").floatValue = 20 + index * 2;
                values.FindProperty("_hitWindowStart").floatValue = 0.25f;
                values.FindProperty("_hitWindowEnd").floatValue = 0.55f;
                values.FindProperty("_completionNormalizedTime").floatValue = 0.86f;
                values.FindProperty("_recoveryTime").floatValue = 0.18f;
                values.FindProperty("_comboInputStart").floatValue = 0.2f;
                values.FindProperty("_comboInputEnd").floatValue = 0.68f;
                values.FindProperty("_comboTransitionPoint").floatValue = index == 0 ? 0.55f : 0.68f;
                values.FindProperty("_dodgeCancelStart").floatValue = 0.72f;
                values.FindProperty("_dodgeCancelEnd").floatValue = 0.95f;
                values.FindProperty("_rotateAssistTime").floatValue = 0.24f;
                values.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(attack);
            list.GetArrayElementAtIndex(index).objectReferenceValue = attack;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        return combo;
    }

    private static WeaponData BuildWeapon(string id, string display, WeaponType type, GameObject prefab, AttackCombo combo, AnimatorOverrideController controller, float damage, float stamina)
    {
        WeaponData weapon = Asset<WeaponData>($"{ConfigFolder}/WD_{id}.asset");
        var data = new SerializedObject(weapon);
        data.FindProperty("_weaponId").stringValue = id;
        data.FindProperty("_displayName").stringValue = display;
        data.FindProperty("_weaponType").enumValueIndex = (int)type;
        data.FindProperty("_weaponPrefab").objectReferenceValue = prefab;
        data.FindProperty("_lightAttackCombo").objectReferenceValue = combo;
        // Day11 assets keep their actions when the Day10 authoring tool is rerun.
        if (weapon.Moveset != null)
        {
            var moveset = new SerializedObject(weapon.Moveset);
            moveset.FindProperty("_lightCombo").objectReferenceValue = combo;
            moveset.ApplyModifiedPropertiesWithoutUndo();
        }
        data.FindProperty("_animatorOverrideController").objectReferenceValue = controller;
        data.FindProperty("_damageMultiplier").floatValue = damage;
        data.FindProperty("_staminaMultiplier").floatValue = stamina;
        data.ApplyModifiedPropertiesWithoutUndo();
        return weapon;
    }

    private static void ConfigurePlayer(GameObject player, WeaponData light, WeaponData heavy)
    {
        var combat = new SerializedObject(player.GetComponent<PlayerCombat>());
        var longSword = combat.FindProperty("_weaponHitbox").objectReferenceValue as WeaponHitbox;
        if (longSword == null) throw new InvalidOperationException("玩家长剑命中组件缺失。");
        Transform socket = longSword.transform.parent;
        Transform existing = socket.Find("Weapon_GreatSword");
        var greatSword = existing != null ? existing.GetComponent<WeaponHitbox>() :
            ((GameObject)PrefabUtility.InstantiatePrefab(heavy.WeaponPrefab, socket)).GetComponent<WeaponHitbox>();
        greatSword.name = "Weapon_GreatSword";
        greatSword.transform.localPosition = longSword.transform.localPosition;
        greatSword.transform.localRotation = longSword.transform.localRotation;
        greatSword.gameObject.SetActive(false);
        longSword.gameObject.SetActive(true);
        var equipment = player.GetComponent<PlayerEquipment>() ?? player.AddComponent<PlayerEquipment>();
        var data = new SerializedObject(equipment);
        var slots = data.FindProperty("_slots"); slots.arraySize = 2;
        for (int index = 0; index < 2; index++)
        {
            slots.GetArrayElementAtIndex(index).FindPropertyRelative("Data").objectReferenceValue = index == 0 ? light : heavy;
            slots.GetArrayElementAtIndex(index).FindPropertyRelative("Hitbox").objectReferenceValue = index == 0 ? longSword : greatSword;
        }
        data.FindProperty("_defaultSlot").intValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        var visibility = player.GetComponent<PlayerWeaponVisibility>();
        if (visibility != null)
        {
            var visible = new SerializedObject(visibility);
            var renderers = visible.FindProperty("_weaponRenderers");
            // 只有原本隐藏右手武器时才扩展，尊重现有喝药手配置。
            if (renderers.arraySize > 0)
            {
                Renderer[] all = longSword.GetComponentsInChildren<Renderer>(true).Concat(greatSword.GetComponentsInChildren<Renderer>(true)).ToArray();
                renderers.arraySize = all.Length;
                for (int index = 0; index < all.Length; index++) renderers.GetArrayElementAtIndex(index).objectReferenceValue = all[index];
                visible.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(visibility);
            }
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(equipment);
    }

    private static T Asset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
