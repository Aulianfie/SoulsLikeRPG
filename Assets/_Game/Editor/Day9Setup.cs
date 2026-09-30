using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Day9Setup
{
    public const string DrinkPath = "Assets/ThirdParty/DoubleL/Animations/Actions/Item/Item_Drink.fbx";
    public const string BottlePath = "Assets/_Game/Prefabs/Items/PF_HealingFlaskPlaceholder.prefab";
    private const string PlayerPath = "Assets/_Game/Prefabs/Characters/Player_Day1.prefab";
    private const string HudPath = "Assets/_Game/UI/Prefabs/PF_HealingFlaskUI.prefab";
    private const string MaskPath = "Assets/_Game/Animations/Masks/AM_ItemUse_UpperBody.mask";

    [MenuItem("Tools/SoulsLike RPG/Day9/Setup Healing Flask In Open Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        var scene = EditorSceneManager.GetActiveScene();
        PlayerHealth player = Object.FindObjectsOfType<PlayerHealth>(true).Single(p => p.gameObject.scene == scene);
        if (scene.isDirty)
            throw new InvalidOperationException("请先保存当前场景，再执行 Day9 设置。");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/Day9_Setup.txt", "Setup started.\n");
        EnsureFolder("Assets/_Game/Animations/Masks");
        EnsureFolder("Assets/_Game/Prefabs/Items");
        EnsureFolder("Assets/_Game/Materials/Items");
        AnimationClip clip = ConfigureDrink();
        ConfigureAnimator(clip);
        GameObject bottle = CreateBottle();
        HumanBodyBones hand = ChooseHand(player.GetComponentInChildren<Animator>(), clip);

        GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            ConfigurePlayer(prefab, bottle, hand);
            PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        PlayerHealingFlask flask = ConfigurePlayer(player.gameObject, bottle, hand);
        ConfigureHUD(flask);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        File.AppendAllText("Logs/Day9_Setup.txt",
            $"Scene={scene.path}\nPlayer={player.name}\nDrink={clip.name}; seconds={clip.length}; humanoid={clip.humanMotion}\nHand={hand}\nBottle={BottlePath}\nHUD={HudPath}\nSetup saved.\n");
        Debug.Log("[Day9] R / 手柄 X 使用血瓶；3 瓶，每瓶 40 HP，喝药移动速度 40%。");
    }

    private static AnimationClip ConfigureDrink()
    {
        const string posePath = "Assets/ThirdParty/DoubleL/Models/T-Pose.fbx";
        var poseImporter = (ModelImporter)AssetImporter.GetAtPath(posePath);
        poseImporter.materialImportMode = ModelImporterMaterialImportMode.None;
        poseImporter.SaveAndReimport();
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(posePath).OfType<Avatar>().Single();
        if (!avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException("DoubleL T-Pose Avatar 无效。");
        var importer = (ModelImporter)AssetImporter.GetAtPath(DrinkPath);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = importer.clipAnimations;
        foreach (var setting in clips)
        {
            setting.loopTime = false;
            setting.lockRootRotation = true;
            setting.lockRootHeightY = true;
            setting.lockRootPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(DrinkPath).OfType<AnimationClip>()
            .Single(value => value.name == "Item_Drink");
        if (!clip.humanMotion) throw new InvalidOperationException("喝药动作不是 Humanoid Motion。");
        return clip;
    }

    private static void ConfigureAnimator(AnimationClip clip)
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask { name = "AM_ItemUse_UpperBody" };
            AssetDatabase.CreateAsset(mask, MaskPath);
        }
        for (int index = 0; index < (int)AvatarMaskBodyPart.LastBodyPart; index++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)index, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);
        EditorUtility.SetDirty(mask);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/_Game/Animations/Controllers/AC_Player.controller");
        int layerIndex = Array.FindIndex(controller.layers, value => value.name == "ItemUse");
        if (layerIndex < 0)
        {
            controller.AddLayer("ItemUse");
            layerIndex = controller.layers.Length - 1;
        }
        var layers = controller.layers;
        layers[layerIndex].avatarMask = mask;
        layers[layerIndex].defaultWeight = 0f;
        layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
        var machine = layers[layerIndex].stateMachine;
        AnimatorState empty = machine.states.Select(value => value.state).FirstOrDefault(value => value.name == "Empty")
            ?? machine.AddState("Empty");
        AnimatorState heal = machine.states.Select(value => value.state).FirstOrDefault(value => value.name == "Heal")
            ?? machine.AddState("Heal");
        empty.writeDefaultValues = heal.writeDefaultValues = true;
        heal.motion = clip;
        heal.speed = 1f;
        machine.defaultState = empty;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
    }

    private static HumanBodyBones ChooseHand(Animator original, AnimationClip clip)
    {
        GameObject sample = Object.Instantiate(original.gameObject);
        try
        {
            Animator animator = sample.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            AnimationMode.StartAnimationMode();
            float left = 0f, right = 0f;
            try
            {
                foreach (float time in new[] { 0.35f, 0.45f, 0.55f })
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(sample, clip, clip.length * time);
                    AnimationMode.EndSampling();
                    Vector3 head = animator.GetBoneTransform(HumanBodyBones.Head).position;
                    left += Vector3.Distance(head, animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
                    right += Vector3.Distance(head, animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
            return left < right ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        }
        finally { Object.DestroyImmediate(sample); }
    }

    private static GameObject CreateBottle()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BottlePath);
        if (existing != null) return existing;
        Material red = Material("M_FlaskPlaceholder_Red", new Color(0.45f, 0.025f, 0.035f), 0.1f);
        Material gold = Material("M_FlaskPlaceholder_Gold", new Color(0.65f, 0.43f, 0.13f), 0.65f);
        Material cork = Material("M_FlaskPlaceholder_Cork", new Color(0.17f, 0.09f, 0.035f), 0f);
        var root = new GameObject("HealingFlaskPlaceholder");
        try
        {
            Part(root.transform, "BottleBody", PrimitiveType.Sphere, new Vector3(0, -0.048f, 0), new Vector3(0.10f, 0.13f, 0.10f), red);
            Part(root.transform, "Neck", PrimitiveType.Cylinder, new Vector3(0, 0.028f, 0), new Vector3(0.035f, 0.022f, 0.035f), gold);
            Part(root.transform, "Cork", PrimitiveType.Cylinder, new Vector3(0, 0.057f, 0), new Vector3(0.031f, 0.01f, 0.031f), cork);
            return PrefabUtility.SaveAsPrefabAsset(root, BottlePath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Material Material(string name, Color color, float metallic)
    {
        string path = "Assets/_Game/Materials/Items/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", 0.4f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void Part(Transform root, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
    }

    private static PlayerHealingFlask ConfigurePlayer(GameObject player, GameObject bottle, HumanBodyBones hand)
    {
        PlayerHealingFlask flask = player.GetComponent<PlayerHealingFlask>() ?? player.AddComponent<PlayerHealingFlask>();
        PlayerItemController items = player.GetComponent<PlayerItemController>() ?? player.AddComponent<PlayerItemController>();
        PlayerWeaponVisibility visibility = player.GetComponent<PlayerWeaponVisibility>() ?? player.AddComponent<PlayerWeaponVisibility>();
        Animator animator = player.GetComponentInChildren<Animator>();
        Transform bone = animator.GetBoneTransform(hand);
        Transform held = bone.Find("HealingFlaskPlaceholder");
        if (held == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(bottle, bone);
            instance.name = "HealingFlaskPlaceholder";
            held = instance.transform;
            held.localPosition = new Vector3(0.025f, 0.005f, 0.015f);
            held.localRotation = Quaternion.Euler(0, 0, 90);
        }
        held.gameObject.SetActive(false);
        var serialized = new SerializedObject(flask);
        serialized.FindProperty("_heldBottle").objectReferenceValue = held.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var weaponData = new SerializedObject(visibility);
        var renderers = weaponData.FindProperty("_weaponRenderers");
        Renderer[] hidden = Array.Empty<Renderer>();
        if (hand == HumanBodyBones.RightHand)
        {
            var combat = new SerializedObject(player.GetComponent<PlayerCombat>());
            var weapon = combat.FindProperty("_weaponHitbox").objectReferenceValue as WeaponHitbox;
            if (weapon != null) hidden = weapon.GetComponentsInChildren<Renderer>(true);
        }
        renderers.arraySize = hidden.Length;
        for (int index = 0; index < hidden.Length; index++) renderers.GetArrayElementAtIndex(index).objectReferenceValue = hidden[index];
        weaponData.ApplyModifiedPropertiesWithoutUndo();
        var itemData = new SerializedObject(items);
        itemData.FindProperty("_currentQuickItem").objectReferenceValue = flask;
        itemData.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(player))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(flask);
            PrefabUtility.RecordPrefabInstancePropertyModifications(items);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visibility);
        }
        return flask;
    }

    private static void ConfigureHUD(PlayerHealingFlask flask)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/UI/Fonts/NotoSansSC_Flask.asset");
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        if (!font.TryAddCharacters(" /R", out string missing))
            throw new InvalidOperationException("血瓶字体缺字：" + missing);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        GameObject prefab = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            TMP_Text count = prefab.GetComponentsInChildren<TMP_Text>(true).Single(value => value.name == "Count");
            count.text = "3 / 3";
            count.fontSize = 26;
            PrefabUtility.SaveAsPrefabAsset(prefab, HudPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        PlayerHUDView hud = Object.FindObjectsOfType<PlayerHUDView>(true).Single(value => value.gameObject.scene == flask.gameObject.scene);
        Canvas canvas = hud.GetComponentInParent<Canvas>();
        Transform root = canvas.transform.Find("HealingFlaskUI");
        if (root == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath), canvas.transform);
            instance.name = "HealingFlaskUI";
            root = instance.transform;
        }
        var presenter = root.GetComponent<HealingFlaskPresenter>() ?? root.gameObject.AddComponent<HealingFlaskPresenter>();
        var data = new SerializedObject(presenter);
        data.FindProperty("_flask").objectReferenceValue = flask;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
