using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Day0ContentBuilder
{
    private const string Ual1Path = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx";
    private const string Ual2Path = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard.fbx";
    private const string FemalePath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/Models/Mannequin_F.fbx";
    private const string ControllerPath = "Assets/_Game/Animations/Controllers/AC_Player_Day0.controller";

    private static readonly string[] ScenePaths =
    {
        "Assets/_Game/Scenes/00_AnimationLab.unity",
        "Assets/_Game/Scenes/01_CombatTest.unity",
        "Assets/_Game/Scenes/02_TrainingGround.unity"
    };

    [MenuItem("Day0/Build Day0 Content")]
    private static void Build()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new InvalidOperationException("URP Lit shader was not found.");
        }

        Material playerMaterial = CreateMaterial("M_Day0_Player", shader, new Color(0.12f, 0.35f, 0.85f));
        Material enemyMaterial = CreateMaterial("M_Day0_Enemy", shader, new Color(0.72f, 0.16f, 0.12f));
        Material groundMaterial = CreateMaterial("M_Day0_Ground", shader, new Color(0.24f, 0.27f, 0.30f));
        Material wallMaterial = CreateMaterial("M_Day0_Wall", shader, new Color(0.42f, 0.44f, 0.47f));
        Material obstacleMaterial = CreateMaterial("M_Day0_Obstacle", shader, new Color(0.42f, 0.28f, 0.14f));
        Material weaponMaterial = CreateWeaponMaterial(shader);

        AnimatorController controller = BuildController(out AnimationClip weaponSlot, out AnimationClip[] attacks);
        GameObject oneHandPrefab = BuildWeaponPrefab("Weapon_OneHand", "sword_A.fbx", 1.15f, weaponMaterial);
        GameObject heavyPrefab = BuildWeaponPrefab("Weapon_Heavy", "axe_C.fbx", 1.45f, weaponMaterial);
        GameObject polearmPrefab = BuildWeaponPrefab("Weapon_Polearm", "halberd.fbx", 2.15f, weaponMaterial);

        GameObject playerPrefab = BuildCharacterPrefab(
            "Player",
            Ual1Path,
            playerMaterial,
            controller,
            weaponSlot,
            attacks,
            oneHandPrefab,
            "Assets/_Game/Prefabs/Characters/Player_Day0.prefab");

        GameObject enemyPrefab = BuildCharacterPrefab(
            "EnemyDummy",
            FemalePath,
            enemyMaterial,
            controller,
            weaponSlot,
            attacks,
            heavyPrefab,
            "Assets/_Game/Prefabs/Characters/EnemyDummy_Day0.prefab",
            addTester: false);

        BuildAnimationLab(playerPrefab, oneHandPrefab, heavyPrefab, polearmPrefab, groundMaterial);
        BuildCombatTest(playerPrefab, enemyPrefab, groundMaterial);
        BuildTrainingGround(groundMaterial, wallMaterial, obstacleMaterial);
        AddScenesToBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(ScenePaths[0], OpenSceneMode.Single);
        Debug.Log("[Day0] Prefabs, controller, and three Day0 scenes built successfully.");
    }

    private static AnimatorController BuildController(out AnimationClip weaponSlot, out AnimationClip[] attacks)
    {
        var motions = new Dictionary<string, AnimationClip>
        {
            ["Idle"] = LoadClip(Ual1Path, "Idle_Loop"),
            ["Walk"] = LoadClip(Ual1Path, "Walk_Loop"),
            ["Run"] = LoadClip(Ual1Path, "Sprint_Loop"),
            ["Roll"] = LoadClip(Ual1Path, "Roll"),
            ["Hit"] = LoadClip(Ual1Path, "Hit_Chest"),
            ["Death"] = LoadClip(Ual1Path, "Death01"),
            ["WeaponAttackTest"] = LoadClip(Ual1Path, "Sword_Attack")
        };

        weaponSlot = motions["WeaponAttackTest"];
        attacks = new[]
        {
            weaponSlot,
            LoadClip(Ual2Path, "Sword_Regular_A"),
            LoadClip(Ual2Path, "Melee_Hook"),
            LoadClip(Ual2Path, "Sword_Regular_B"),
            LoadClip(Ual2Path, "Sword_Regular_C"),
            LoadClip(Ual2Path, "Sword_Regular_Combo")
        };

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        float y = 0f;
        foreach (KeyValuePair<string, AnimationClip> entry in motions)
        {
            AnimatorState state = stateMachine.states
                .Select(item => item.state)
                .FirstOrDefault(item => item.name == entry.Key);
            if (state == null)
            {
                state = stateMachine.AddState(entry.Key, new Vector3(250f, y, 0f));
            }

            state.motion = entry.Value;
            state.writeDefaultValues = true;
            if (entry.Key == "Idle")
            {
                stateMachine.defaultState = state;
            }

            y += 55f;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static GameObject BuildWeaponPrefab(string prefabName, string sourceFile, float targetLength, Material material)
    {
        string sourcePath = $"Assets/ThirdParty/Weapons/KayKit_FantasyWeaponsBits/Models/{sourceFile}";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        GameObject root = new GameObject(prefabName);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
        model.name = "Model";
        model.transform.SetParent(root.transform, false);
        AssignMaterial(model, material);
        NormalizeLargestDimension(model, targetLength);

        string prefabPath = $"Assets/_Game/Prefabs/Weapons/{prefabName}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildCharacterPrefab(
        string rootName,
        string modelPath,
        Material material,
        RuntimeAnimatorController controller,
        AnimationClip weaponSlot,
        AnimationClip[] attacks,
        GameObject equippedWeapon,
        string prefabPath,
        bool addTester = true)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().First(item => item.isValid && item.isHuman);
        GameObject root = new GameObject(rootName);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
        model.name = "CharacterModel";
        model.transform.SetParent(root.transform, false);
        AssignMaterial(model, material);

        // Humanoid model prefabs already contain the Animator that owns their Avatar.
        // Reuse it so the hierarchy has a single animation boundary.
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1f, 0f);
        collider.height = 2f;
        collider.radius = 0.35f;

        Transform hand = FindRightHand(animator, model.transform);
        GameObject socket = new GameObject("RightHandWeaponSocket");
        socket.transform.SetParent(hand, false);
        socket.transform.localPosition = Vector3.zero;
        socket.transform.localRotation = Quaternion.identity;

        GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(equippedWeapon);
        weapon.name = equippedWeapon.name;
        weapon.transform.SetParent(socket.transform, false);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;

        if (addTester)
        {
            Day0AnimationTester tester = root.AddComponent<Day0AnimationTester>();
            tester.Animator = animator;
            tester.WeaponSlotClip = weaponSlot;
            tester.WeaponTestClips = attacks;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildAnimationLab(GameObject player, GameObject oneHand, GameObject heavy, GameObject polearm, Material ground)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateLight();
        CreateCamera(new Vector3(0f, 3.2f, -8f), new Vector3(0f, 1f, 0f));
        CreatePrimitive("Ground", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(10f, 0.2f, 10f), ground);
        GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(player);
        Day0AnimationTester labTester = playerInstance.GetComponent<Day0AnimationTester>();
        labTester.AutoPlayValidation = true;

        GameObject display = new GameObject("Weapon Display");
        PlacePrefab(oneHand, "OneHand", display.transform, new Vector3(-3f, 1.2f, 2f), new Vector3(0f, 0f, -35f));
        PlacePrefab(heavy, "Heavy", display.transform, new Vector3(0f, 1.3f, 2f), new Vector3(0f, 0f, -35f));
        PlacePrefab(polearm, "Polearm", display.transform, new Vector3(3f, 1.6f, 2f), new Vector3(0f, 0f, -35f));

        EditorSceneManager.SaveScene(scene, ScenePaths[0]);
    }

    private static void BuildCombatTest(GameObject player, GameObject enemy, Material ground)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateLight();
        CreateCamera(new Vector3(0f, 5f, -10f), new Vector3(0f, 1f, 1f));
        CreatePrimitive("Ground", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 20f), ground);
        CreateMarker("Player Spawn Point", new Vector3(-3f, 0f, -2f));

        GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(player);
        playerInstance.transform.position = new Vector3(-3f, 0f, -2f);
        GameObject enemyInstance = (GameObject)PrefabUtility.InstantiatePrefab(enemy);
        enemyInstance.transform.position = new Vector3(3f, 0f, 2f);
        enemyInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        EditorSceneManager.SaveScene(scene, ScenePaths[1]);
    }

    private static void BuildTrainingGround(Material ground, Material wall, Material obstacle)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateLight();
        CreateCamera(new Vector3(0f, 18f, -20f), new Vector3(0f, 0f, 0f));
        CreatePrimitive("Ground", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(30f, 0.2f, 30f), ground);

        GameObject walls = new GameObject("Walls");
        CreatePrimitive("Wall_North", PrimitiveType.Cube, new Vector3(0f, 1f, 15f), new Vector3(30f, 2f, 0.5f), wall, walls.transform);
        CreatePrimitive("Wall_South", PrimitiveType.Cube, new Vector3(0f, 1f, -15f), new Vector3(30f, 2f, 0.5f), wall, walls.transform);
        CreatePrimitive("Wall_East", PrimitiveType.Cube, new Vector3(15f, 1f, 0f), new Vector3(0.5f, 2f, 30f), wall, walls.transform);
        CreatePrimitive("Wall_West", PrimitiveType.Cube, new Vector3(-15f, 1f, 0f), new Vector3(0.5f, 2f, 30f), wall, walls.transform);

        GameObject obstacles = new GameObject("Obstacles");
        CreatePrimitive("Box_01", PrimitiveType.Cube, new Vector3(-5f, 1f, 1f), new Vector3(2f, 2f, 2f), obstacle, obstacles.transform);
        CreatePrimitive("Box_02", PrimitiveType.Cube, new Vector3(5f, 1.5f, -3f), new Vector3(3f, 3f, 2f), obstacle, obstacles.transform);
        CreatePrimitive("Pillar_01", PrimitiveType.Cylinder, new Vector3(-6f, 2f, 7f), new Vector3(1.5f, 2f, 1.5f), wall, obstacles.transform);
        CreatePrimitive("Pillar_02", PrimitiveType.Cylinder, new Vector3(6f, 2f, 7f), new Vector3(1.5f, 2f, 1.5f), wall, obstacles.transform);
        CreatePrimitive("Range_Block", PrimitiveType.Cube, new Vector3(0f, 0.75f, 6f), new Vector3(6f, 1.5f, 1f), obstacle, obstacles.transform);

        CreateMarker("Player Spawn", new Vector3(0f, 0f, -10f));
        CreateMarker("Enemy Spawn 01", new Vector3(-7f, 0f, 8f));
        CreateMarker("Enemy Spawn 02", new Vector3(0f, 0f, 10f));
        CreateMarker("Enemy Spawn 03", new Vector3(7f, 0f, 8f));
        EditorSceneManager.SaveScene(scene, ScenePaths[2]);
    }

    private static Material CreateMaterial(string name, Shader shader, Color color)
    {
        string path = $"Assets/_Game/Materials/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateWeaponMaterial(Shader shader)
    {
        Material material = CreateMaterial("M_Day0_Weapon", shader, Color.white);
        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(
            "Assets/ThirdParty/Weapons/KayKit_FantasyWeaponsBits/Textures/weapons_bits_texture.png");
        material.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static AnimationClip LoadClip(string path, string suffix)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(item =>
                (item.name.Equals(suffix, StringComparison.Ordinal) ||
                 item.name.EndsWith("|" + suffix, StringComparison.Ordinal)) &&
                !item.name.EndsWith(suffix + "_RM", StringComparison.Ordinal));
        if (clip == null)
        {
            throw new InvalidOperationException($"Animation clip '{suffix}' was not found in {path}.");
        }

        return clip;
    }

    private static void AssignMaterial(GameObject root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        }
    }

    private static void NormalizeLargestDimension(GameObject model, float target)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest > 0.0001f)
        {
            model.transform.localScale = Vector3.one * (target / largest);
        }
    }

    private static Transform FindRightHand(Animator animator, Transform modelRoot)
    {
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand != null) return hand;
        return modelRoot.GetComponentsInChildren<Transform>(true)
            .First(item => item.name.Equals("hand.R", StringComparison.OrdinalIgnoreCase) ||
                           item.name.Equals("RightHand", StringComparison.OrdinalIgnoreCase));
    }

    private static void PlacePrefab(GameObject prefab, string name, Transform parent, Vector3 position, Vector3 rotation)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(rotation);
    }

    private static GameObject CreatePrimitive(
        string name,
        PrimitiveType type,
        Vector3 position,
        Vector3 scale,
        Material material,
        Transform parent = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static void CreateLight()
    {
        GameObject go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
    }

    private static void CreateCamera(Vector3 position, Vector3 lookAt)
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = position;
        go.transform.LookAt(lookAt);
        Camera camera = go.AddComponent<Camera>();
        camera.fieldOfView = 55f;
        go.AddComponent<AudioListener>();
    }

    private static void CreateMarker(string name, Vector3 position)
    {
        GameObject marker = new GameObject(name);
        marker.transform.position = position;
    }

    private static void AddScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        foreach (string path in ScenePaths)
        {
            if (scenes.All(scene => scene.path != path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
