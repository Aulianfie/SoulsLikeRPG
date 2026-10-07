using System;
using System.IO;
using System.Linq;
using Cinemachine;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 只生成项目自有资源。模型和 FBX 动画保留在 ThirdParty，源导入设置不变。
public static class GiantGolemBossSetup
{
    public const string ScenePath = "Assets/_Game/Scenes/04_GiantGolem_BossTest.unity";
    public const string PrefabPath = "Assets/_Game/Prefabs/Boss/PF_GiantGolemBoss.prefab";
    public const string ConfigRoot = "Assets/_Game/Configs/Boss/GiantGolem/";
    private const string AnimationRoot = "Assets/_Game/Animations/Boss/GiantGolem/";
    private const string MaterialRoot = "Assets/_Game/Materials/Boss/";
    private const string RockPath = "Assets/_Game/Prefabs/Boss/PF_GolemRock.prefab";
    private const string BreakPath = "Assets/_Game/Prefabs/Boss/PF_GolemRockBreak.prefab";

    public static void InspectRoots()
    {
        File.WriteAllLines(
            "Logs/GiantGolem/Roots.txt",
            SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name + " | " + string.Join(",", g.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)))
        );
    }

    [MenuItem("Tools/SoulsLike RPG/Giant Golem/2 Build Boss Test Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("请在已保存场景的 Edit Mode 生成 Boss 场景。");
        }

        if (File.Exists(ScenePath))
        {
            throw new InvalidOperationException("测试场景已经存在，请直接打开；生成器不会覆盖场景中的手工修改。");
        }

        foreach (string folder in new[]
        {
            ConfigRoot,
            AnimationRoot,
            MaterialRoot,
            "Assets/_Game/Prefabs/Boss/",
            "Assets/_Game/Navigation/"
        }
        )
        {
            EnsureFolder(folder.TrimEnd('/'));
        }

        var clips = GiantGolemBossAnimationAudit.AttackNames.Concat(new[] { "idle", "move_run_front", "dead01" }).ToDictionary(n => n, CloneClip);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(AnimationRoot + "AC_GiantGolemBoss.controller");
        foreach (string name in clips.Keys)
        {
            string stateName;
            if (name == "idle")
            {
                stateName = "Idle";
            }
            else if (name == "move_run_front")
            {
                stateName = "Chase";
            }
            else if (name == "dead01")
            {
                stateName = "Dead";
            }
            else
            {
                stateName = name;
            }

            var state = controller.layers[0].stateMachine.AddState(stateName);
            state.motion = clips[name];
            if (stateName == "Idle")
            {
                controller.layers[0].stateMachine.defaultState = state;
            }
        }

        Material body = Material("M_GolemBody", new Color(.38f, .44f, .46f));
        Material stone = Material("M_GolemStone", new Color(.27f, .29f, .30f));
        Material ground = Material("M_GolemArena", new Color(.22f, .24f, .27f));
        Material cue = Material("M_GolemGroundPulse", new Color(1f, .42f, .07f), true);
        CreateRockAssets(stone);
        BossSkillData[] skills = CreateSkills();
        CreateBoss(controller, skills, body, cue);
        // 复制现有场景的玩家、UI、相机和检查点接线；仅改副本。
        if (!AssetDatabase.CopyAsset("Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity", ScenePath))
        {
            throw new IOException("复制现有场景失败。");
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var checkpoint in Object.FindObjectsOfType<CheckpointSite>(true).Where(c => c.gameObject.scene == scene))
        {
            checkpoint.transform.SetParent(null, true);
        }

        foreach (var enemy in Object.FindObjectsOfType<EnemyStateMachine>(true).Where(c => c.gameObject.scene == scene).ToArray())
        {
            Object.DestroyImmediate(enemy.gameObject);
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            bool keep = root.GetComponentInChildren<PlayerStateMachine>(true) != null ||
                root.GetComponentInChildren<Canvas>(true) != null ||
                root.GetComponent<Camera>() != null &&
                root.CompareTag("MainCamera") ||
                root.GetComponent<CinemachineVirtualCameraBase>() != null ||
                root.GetComponent<LockOnCameraRig>() != null ||
                root.GetComponent<CheckpointManager>() != null ||
                root.GetComponent<RespawnController>() != null ||
                root.GetComponentInChildren<ScreenFader>(true) != null ||
                root.GetComponent<CheckpointSite>() != null ||
                root.name == "EventSystem" ||
                root.name == "DefaultSpawn" ||
                root.GetComponent<Light>() != null &&
                root.GetComponent<Light>().type == LightType.Directional;
            if (!keep)
            {
                Object.DestroyImmediate(root);
            }
        }

        var player = Object.FindObjectOfType<PlayerStateMachine>();
        player.transform.SetPositionAndRotation(new Vector3(0, .12f, -14), Quaternion.identity);
        foreach (var spawn in scene.GetRootGameObjects().Where(g => g.name == "DefaultSpawn"))
        {
            spawn.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
        }

        var checkpoints = Object.FindObjectsOfType<CheckpointSite>(true);
        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpoints[i].transform.position = new Vector3(-6 - i * 4, 0, -18);
            Set(checkpoints[i], "_checkpointId", "golem_test_" + i);
        }

        foreach (var manager in Object.FindObjectsOfType<CheckpointManager>())
        {
            Set(manager, "_persistProgression", false);
        }

        var arena = new GameObject("GolemArena");
        Cube("Ground", arena.transform, new Vector3(0, -.5f, 0), new Vector3(80, 1, 80), ground);
        Cube("Wall_N", arena.transform, new Vector3(0, 2, 39), new Vector3(80, 4, 1), stone);
        Cube("Wall_S", arena.transform, new Vector3(0, 2, -39), new Vector3(80, 4, 1), stone);
        Cube("Wall_E", arena.transform, new Vector3(39, 2, 0), new Vector3(1, 4, 80), stone);
        Cube("Wall_W", arena.transform, new Vector3(-39, 2, 0), new Vector3(1, 4, 80), stone);
        Cube("Obstacle_Left", arena.transform, new Vector3(-9, 2.5f, 2), new Vector3(3, 5, 3), stone);
        Cube("Obstacle_Right", arena.transform, new Vector3(9, 2.5f, -4), new Vector3(3, 5, 3), stone);
        Cube("Obstacle_Far", arena.transform, new Vector3(2, 2.5f, 19), new Vector3(4, 5, 2), stone);
        var surface = arena.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1;
        surface.BuildNavMesh();
        if (surface.navMeshData == null)
        {
            throw new InvalidOperationException("NavMesh 烘焙失败。");
        }

        AssetDatabase.CreateAsset(surface.navMeshData, "Assets/_Game/Navigation/NM_GolemArena.asset");
        var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        boss.name = "GiantGolemBoss";
        boss.transform.SetPositionAndRotation(new Vector3(0, .05f, 0), Quaternion.Euler(0, 180, 0));
        Set(boss.GetComponent<BossBrain>(), "_targetOverride", player.transform);
        Set(boss.GetComponent<EnemyReward>(), "_wallet", player.GetComponent<SoulWallet>());
        var small = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/EnemyDummy_Day0.prefab"),
            scene
        );
        small.name = "Enemy_Regression";
        small.transform.position = new Vector3(-23, .05f, 12);
        var camera = Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(0, 3.5f, -20), Quaternion.Euler(12, 0, 0));
        foreach (var free in Object.FindObjectsOfType<CinemachineFreeLook>())
        {
            free.m_Orbits[0] = new CinemachineFreeLook.Orbit(4.5f, 4.7f);
            free.m_Orbits[1] = new CinemachineFreeLook.Orbit(2.5f, 5.5f);
            free.m_Orbits[2] = new CinemachineFreeLook.Orbit(.5f, 4.7f);
        }

        foreach (var lockCam in Object.FindObjectsOfType<CinemachineVirtualCamera>())
        {
            var transposer = lockCam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_FollowOffset = new Vector3(0, 2.2f, -5.5f);
            }
        }

        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.5f, .5f, .5f);
        EnsureTestLight();
        Lightmapping.Clear();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(
            "Logs/GiantGolem/Setup.txt",
            "Scene=" + ScenePath + "\nPrefab=" + PrefabPath + "\nSkills=10; Height=4.4; NavMesh=True; TestSavePersistence=False\n"
        );
        Selection.activeGameObject = boss;
        Debug.Log("[Golem] Boss、10 个技能、相机/UI/检查点和独立场景已保存。");
    }

    private static AnimationClip CloneClip(string name)
    {
        var source = AssetDatabase.LoadAllAssetsAtPath(GiantGolemBossAnimationAudit.SourceRoot + "Inplace/" + name + "_inplace.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        var clip = Object.Instantiate(source);
        clip.name = name;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = name == "idle" ||
            name == "move_run_front";
        settings.loopBlend = settings.loopTime;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var serialized = new SerializedObject(clip);
        serialized.FindProperty("m_AnimationClipSettings.m_LoopBlendOrientation").boolValue = true;
        serialized.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionXZ").boolValue = true;
        serialized.FindProperty("m_AnimationClipSettings.m_KeepOriginalOrientation").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(clip, AnimationRoot + name + ".anim");
        return clip;
    }

    private static BossSkillData[] CreateSkills()
    {
        var skills = GiantGolemBossAnimationAudit.AttackNames.Select(name =>
            {
                var skill = ScriptableObject.CreateInstance<BossSkillData>();
                AssetDatabase.CreateAsset(skill, ConfigRoot + "SO_" + name + ".asset");
                Set(skill, "_id", name);
                Set(skill, "_stateName", name);
                Set(skill, "_maxRange", 2.8f);
                Set(skill, "_maxAngle", 100f);
                Set(skill, "_cooldown", 2.5f);
                Set(skill, "_damage", 24);
                Set(skill, "_recovery", .55f);
                Set(skill, "_directionLock", .30f);
                Set(skill, "_hitStart", .38f);
                Set(skill, "_hitEnd", .62f);
                Set(skill, "_release", .50f);
                return skill;
            }).ToArray();
        Set(skills[1], "_directionLock", .18f);
        Set(skills[1], "_hitStart", .23f);
        Set(skills[1], "_hitEnd", .76f);
        Set(skills[2], "_hitStart", .37f);
        Set(skills[2], "_hitEnd", .68f);
        Set(skills[3], "_directionLock", .20f);
        Set(skills[3], "_hitStart", .24f);
        Set(skills[3], "_hitEnd", .74f);
        for (int i = 4; i <= 5; i++)
        {
            Set(skills[i], "_family", (int)BossSkillFamily.GroundSlam);
            Set(skills[i], "_side", i == 4 ? (int)BossSkillSide.Left : (int)BossSkillSide.Right);
            Set(skills[i], "_damageKind", (int)BossDamageKind.GroundPulse);
            Set(skills[i], "_maxRange", 5.5f);
            Set(skills[i], "_maxAngle", 180f);
            Set(skills[i], "_cooldown", 7f);
            Set(skills[i], "_radius", 6f);
            Set(skills[i], "_damage", 32);
            Set(skills[i], "_directionLock", .20f);
            Set(skills[i], "_hitStart", .49f);
            Set(skills[i], "_hitEnd", .51f);
            Set(skills[i], "_release", .49f);
        }

        ConfigureMove(skills[6], BossSkillFamily.Dash, 4, 11, .27f, .56f, 10f, .31f, .64f, 2f);
        ConfigureMove(skills[7], BossSkillFamily.Whirlwind, 2, 10.5f, .25f, .76f, 6f, .25f, .78f, 3.1f);
        Set(skills[6], "_cooldown", 9f);
        Set(skills[7], "_cooldown", 11f);
        var jump = skills[8];
        Set(jump, "_family", (int)BossSkillFamily.GroundSlam);
        Set(jump, "_damageKind", (int)BossDamageKind.GroundPulse);
        Set(jump, "_maxRange", 7f);
        Set(jump, "_maxAngle", 180f);
        Set(jump, "_cooldown", 12f);
        Set(jump, "_radius", 8f);
        Set(jump, "_damage", 40);
        Set(jump, "_directionLock", .28f);
        Set(jump, "_hitStart", .59f);
        Set(jump, "_hitEnd", .61f);
        Set(jump, "_release", .59f);
        Set(jump, "_recovery", .85f);
        var thrown = skills[9];
        Set(thrown, "_family", (int)BossSkillFamily.ThrowStone);
        Set(thrown, "_damageKind", (int)BossDamageKind.Projectile);
        Set(thrown, "_minRange", 11f);
        Set(thrown, "_maxRange", 32f);
        Set(thrown, "_maxAngle", 180f);
        Set(thrown, "_cooldown", 10f);
        Set(thrown, "_damage", 30);
        Set(thrown, "_directionLock", .60f);
        Set(thrown, "_release", .68f);
        Set(thrown, "_projectilePickup", .25f);
        Set(thrown, "_hitStart", .68f);
        Set(thrown, "_hitEnd", .70f);
        ConfigureCombatHitWindows(skills[1], skills[6]);
        return skills;
    }

    [MenuItem("Tools/SoulsLike RPG/Giant Golem/Apply Multi Hit Windows")]
    public static void ApplyCombatHitWindows()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请在 Edit Mode 配置技能窗口。");
        }

        var attack2 = AssetDatabase.LoadAssetAtPath<BossSkillData>(ConfigRoot + "SO_attack02.asset");
        var dash = AssetDatabase.LoadAssetAtPath<BossSkillData>(ConfigRoot + "SO_attack_DashAtk.asset");
        ConfigureCombatHitWindows(attack2, dash);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureCombatHitWindows(BossSkillData attack2, BossSkillData dash)
    {
        ConfigureHitWindows(attack2, new Vector2(.23f, .43f), new Vector2(.58f, .76f));
        ConfigureHitWindows(dash, new Vector2(.31f, .43f), new Vector2(.53f, .72f));
        Set(dash, "_hitEnd", .72f);
    }

    private static void ConfigureHitWindows(BossSkillData skill, params Vector2[] windows)
    {
        var serialized = new SerializedObject(skill);
        var property = serialized.FindProperty("_hitWindows");
        property.arraySize = windows.Length;
        for (int i = 0; i < windows.Length; i++)
        {
            var window = property.GetArrayElementAtIndex(i);
            window.FindPropertyRelative("_start").floatValue = windows[i].x;
            window.FindPropertyRelative("_end").floatValue = windows[i].y;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ConfigureMove(BossSkillData s, BossSkillFamily family, float min, float max, float moveStart, float moveEnd, float speed, float hitStart, float hitEnd, float radius)
    {
        Set(s, "_family", (int)family);
        Set(s, "_damageKind", (int)BossDamageKind.BodySweep);
        Set(s, "_minRange", min);
        Set(s, "_maxRange", max);
        Set(s, "_directionLock", .20f);
        Set(s, "_moveStart", moveStart);
        Set(s, "_moveEnd", moveEnd);
        Set(s, "_moveSpeed", speed);
        Set(s, "_hitStart", hitStart);
        Set(s, "_hitEnd", hitEnd);
        Set(s, "_radius", radius);
        Set(s, "_damage", 30);
    }

    public static void ApplyMeasuredTiming()
    {
        foreach (var skill in AssetDatabase.FindAssets("t:BossSkillData", new[] { ConfigRoot.TrimEnd('/') }).Select(g => AssetDatabase.LoadAssetAtPath<BossSkillData>(AssetDatabase.GUIDToAssetPath(g))))
        {
            if (skill.Family == BossSkillFamily.Ordinary)
            {
                Set(skill, "_maxRange", 2.8f);
            }

            if (skill.Id == "attack02")
            {
                Set(skill, "_directionLock", .18f);
            }

            if (skill.Id == "attack04")
            {
                Set(skill, "_directionLock", .20f);
            }

            if (skill.Family == BossSkillFamily.GroundSlam)
            {
                float release;
                if (skill.IsSidedGroundSlam)
                {
                    release = .49f;
                }
                else
                {
                    release = .59f;
                }

                Set(skill, "_hitStart", release);
                Set(skill, "_hitEnd", release + .02f);
                Set(skill, "_release", release);
            }
        }

        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Set(prefab.GetComponent<BossBrain>(), "_selection.nearRange", 3.2f);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        Set(Object.FindObjectOfType<BossBrain>(), "_selection.nearRange", 3.2f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    public static void RepairTestSceneWiring()
    {
        if (EditorApplication.isPlaying ||
            SceneManager.GetActiveScene().path != ScenePath)
        {
            throw new InvalidOperationException("仅在 Boss 测试场景 Edit Mode 执行。");
        }

        ApplyMeasuredTiming();
        var fader = Object.FindObjectOfType<ScreenFader>();
        if (fader == null)
        {
            var canvas = new GameObject("BossTestFadeCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 1000;
            var fade = new GameObject("ScreenFader", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(ScreenFader));
            fade.transform.SetParent(canvas.transform, false);
            var rect = fade.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            fade.GetComponent<Image>().color = Color.black;
            fade.GetComponent<CanvasGroup>().alpha = 0;
            fader = fade.GetComponent<ScreenFader>();
        }

        foreach (var respawn in Object.FindObjectsOfType<RespawnController>())
        {
            Set(respawn, "_screenFader", fader);
        }

        EnsureTestLight();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void EnsureTestLight()
    {
        if (!Object.FindObjectsOfType<Light>().Any(l => l.type == LightType.Directional))
        {
            var light = new GameObject("GolemArenaLight", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
        }

        foreach (var light in Object.FindObjectsOfType<Light>())
        {
            if (light.type == LightType.Directional)
            {
                light.intensity = 1.3f;
                light.color = new Color(1, .96f, .9f);
                light.transform.rotation = Quaternion.Euler(40, -35, 0);
            }
        }
    }

    private static void CreateBoss(AnimatorController controller, BossSkillData[] skills, Material body, Material cue)
    {
        var root = new GameObject("PF_GiantGolemBoss");
        root.SetActive(false);
        root.layer = 3;
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GiantGolemBossAnimationAudit.SourceRoot + "00_T-pose_golem.FBX"));
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * 2.33979988f;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 3;
            }

            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = r.sharedMaterials.Select(_ => body).ToArray();
            }

            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(GiantGolemBossAnimationAudit.SourceRoot + "00_T-pose_golem.FBX").OfType<Avatar>().First();
            var collider = root.AddComponent<CapsuleCollider>();
            collider.height = 4.4f;
            collider.radius = .85f;
            collider.center = Vector3.up * 2.2f;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = .85f;
            agent.height = 4.4f;
            agent.speed = 5f;
            agent.acceleration = 18;
            agent.angularSpeed = 0;
            agent.stoppingDistance = 3.5f;
            var health = root.AddComponent<EnemyHealth>();
            Set(health, "_maxHealth", 1200);
            root.AddComponent<EnemyMotor>();
            var animationDriver = root.AddComponent<EnemyAnimator>();
            Set(animationDriver, "_animator", animator);
            var territory = root.AddComponent<EnemyTerritory>();
            Set(territory, "_patrolRadius", 0f);
            Set(territory, "_detectionRadius", 30f);
            Set(territory, "_leashRadius", 36f);
            var lockPoint = Child("LockPoint", root.transform);
            lockPoint.localPosition = Vector3.up * 2.8f;
            Set(root.AddComponent<Targetable>(), "_lockPoint", lockPoint);
            Set(root.AddComponent<EnemyReward>(), "_soulReward", 500);
            var damageArea = root.AddComponent<BossDamageArea>();
            Set(damageArea, "_cueMaterial", cue);
            var runner = root.AddComponent<BossSkillRunner>();
            // 编辑器取人形骨骼需要 Animator 已初始化。
            root.SetActive(true);
            animator.Rebind();
            animator.Update(0);
            root.SetActive(false);
            Set(runner, "_leftHand", Hand(animator.GetBoneTransform(HumanBodyBones.LeftHand)));
            Set(runner, "_rightHand", Hand(animator.GetBoneTransform(HumanBodyBones.RightHand)));
            Set(runner, "_leftFoot", animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            Set(runner, "_rightFoot", animator.GetBoneTransform(HumanBodyBones.RightFoot));
            var socket = Child("ThrowSocket", animator.GetBoneTransform(HumanBodyBones.RightHand));
            socket.localPosition = new Vector3(0, .15f, .3f);
            Set(runner, "_throwSocket", socket);
            Set(
                runner,
                "_rockPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(RockPath).GetComponent<BossRockProjectile>()
            );
            var brain = root.AddComponent<BossBrain>();
            var so = new SerializedObject(brain);
            var array = so.FindProperty("_skills");
            array.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            var canvas = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(root.transform, false);
            canvas.transform.localPosition = Vector3.up * 5f;
            canvas.transform.localScale = Vector3.one * .01f;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(260, 45);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvas.transform, false);
            background.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 16);
            background.GetComponent<Image>().color = new Color(.08f, .08f, .08f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fr = fill.GetComponent<RectTransform>();
            fr.sizeDelta = new Vector2(256, 12);
            fr.pivot = new Vector2(0, .5f);
            fr.anchoredPosition = new Vector2(-128, 0);
            fill.GetComponent<Image>().color = new Color(.75f, .12f, .1f);
            var label = new GameObject("HP", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(canvas.transform, false);
            label.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 30);
            label.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 23);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "1200 / 1200";
            var bar = canvas.AddComponent<EnemyHealthBarUI>();
            Set(bar, "_health", health);
            Set(bar, "_fillImage", fill.GetComponent<Image>());
            Set(bar, "_healthText", text);
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static WeaponHitbox Hand(Transform bone)
    {
        var hand = Child("BossHandHitbox", bone);
        hand.gameObject.layer = 3;
        var shape = hand.gameObject.AddComponent<BoxCollider>();
        shape.isTrigger = true;
        shape.size = Vector3.one * .52f;
        shape.enabled = false;
        var hitbox = hand.gameObject.AddComponent<WeaponHitbox>();
        Set(hitbox, "_shape", shape);
        Set(hitbox, "_targetLayers", 1);
        return hitbox;
    }

    private static void CreateRockAssets(Material stone)
    {
        var effect = new GameObject("GolemRockBreak", typeof(ParticleSystem));
        var ps = effect.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = .25f;
        main.loop = false;
        main.startLifetime = .7f;
        main.startSpeed = 4;
        main.startSize = .2f;
        main.gravityModifier = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 16) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .25f;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
        renderer.sharedMaterial = stone;
        Object.DestroyImmediate(cube);
        var breakPrefab = PrefabUtility.SaveAsPrefabAsset(effect, BreakPath);
        Object.DestroyImmediate(effect);
        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "PF_GolemRock";
        rock.transform.localScale = Vector3.one * .9f;
        rock.GetComponent<Renderer>().sharedMaterial = stone;
        rock.GetComponent<SphereCollider>().isTrigger = true;
        var projectile = rock.AddComponent<BossRockProjectile>();
        Set(projectile, "_breakEffect", breakPrefab);
        PrefabUtility.SaveAsPrefabAsset(rock, RockPath);
        Object.DestroyImmediate(rock);
    }

    private static Material Material(string name, Color color, bool unlit = false)
    {
        var material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        material.name = name;
        material.color = color;
        AssetDatabase.CreateAsset(material, MaterialRoot + name + ".mat");
        return material;
    }

    private static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Transform Child(string name, Transform parent)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null)
        {
            throw new MissingFieldException(target.GetType().Name, field);
        }

        if (value is int i)
        {
            p.intValue = i;
        }
        else if (value is float f)
        {
            p.floatValue = f;
        }
        else if (value is bool b)
        {
            p.boolValue = b;
        }
        else if (value is string s)
        {
            p.stringValue = s;
        }
        else
        {
            p.objectReferenceValue = value as Object;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
