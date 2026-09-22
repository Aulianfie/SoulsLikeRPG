using System.Collections.Generic;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Day5 Task3 场景与相机装配器（可重复执行）：
/// 1. Player_Day1 预制体：确保存在 CameraRoot（静止相机锚点，位于胸部高度）。
/// 2. 01_CombatTest 场景：
///    - FreeLook 的 Follow/LookAt 重绑定到 CameraRoot；
///    - 创建/更新 CM_LockOn_Player 锁定虚拟相机（Follow=CameraRoot、LookAt=CameraTarget）；
///    - 创建/更新 CameraRig（LockOnCameraRig 组件）；
///    - CinemachineBrain 默认混合时间改为 0.4s；
///    - 把场景敌人的组件级修改固化回预制体，并保证场景内有两个敌人实例。
/// </summary>
public static class Day5Task3CameraBuilder
{
    private const string PlayerPrefabPath =
        "Assets/_Game/Prefabs/Characters/Player_Day1.prefab";
    private const string EnemyPrefabPath =
        "Assets/_Game/Prefabs/Characters/EnemyDummy_Day0.prefab";

    [MenuItem("Tools/Day5/Build Task3 Camera Rig")]
    public static void BuildTask3CameraRig()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("请先退出预制体编辑模式再执行该装配。");
            return;
        }

        EnsurePlayerCameraRoot();

        // ---- 场景对象查找 ----
        GameObject playerInstance = GameObject.Find("Player_Day1");
        if (playerInstance == null)
        {
            Debug.LogError("找不到场景中的 Player_Day1 实例。");
            return;
        }

        CinemachineFreeLook freeLook = Object.FindObjectOfType<CinemachineFreeLook>();
        CinemachineBrain brain = Object.FindObjectOfType<CinemachineBrain>();
        if (freeLook == null || brain == null)
        {
            Debug.LogError("场景中缺少 CinemachineFreeLook 或 CinemachineBrain。");
            return;
        }

        Transform cameraRoot = playerInstance.transform.Find("CameraRoot");
        Transform cameraTarget = playerInstance.transform.Find("CameraTarget");
        if (cameraRoot == null || cameraTarget == null)
        {
            Debug.LogError(
                "玩家实例缺少 CameraRoot / CameraTarget（预制体同步失败？）。");
            return;
        }

        // ---- 1. FreeLook 重绑定到静止锚点 ----
        freeLook.Follow = cameraRoot;
        freeLook.LookAt = cameraRoot;

        // ---- 2. 锁定虚拟相机 ----
        CinemachineVirtualCamera lockOnCamera = SetupLockOnCamera(
            cameraRoot,
            cameraTarget
        );

        // ---- 3. CameraRig ----
        SetupCameraRig(
            playerInstance.GetComponent<PlayerTargeting>(),
            cameraRoot,
            cameraTarget,
            lockOnCamera
        );

        // ---- 4. Brain 混合时间（切换目标/进出锁定的镜头混合）----
        SerializedObject brainObject = new SerializedObject(brain);
        brainObject.FindProperty("m_DefaultBlend.m_Time").floatValue = 0.5f;
        brainObject.ApplyModifiedPropertiesWithoutUndo();

        // ---- 5. 敌人：固化 overrides + 保证两个实例 ----
        EnsureTwoEnemies();

        // ---- 6. 保存 ----
        EditorSceneManager.MarkSceneDirty(playerInstance.scene);
        EditorSceneManager.SaveScene(playerInstance.scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Day5 Task3 相机装配完成：FreeLook->CameraRoot，" +
            "CM_LockOn_Player 就绪，CameraRig 就绪，敌人数量=" +
            Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Length
        );
    }

    /// <summary>玩家预制体：确保存在 CameraRoot（胸部锚点）。</summary>
    private static void EnsurePlayerCameraRoot()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            if (contents.transform.Find("CameraRoot") == null)
            {
                GameObject cameraRoot = new GameObject("CameraRoot");
                cameraRoot.transform.SetParent(contents.transform, false);
                cameraRoot.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                Debug.Log("已为 Player_Day1 预制体添加 CameraRoot。");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    /// <summary>创建或更新锁定虚拟相机。</summary>
    private static CinemachineVirtualCamera SetupLockOnCamera(
        Transform cameraRoot,
        Transform cameraTarget
    )
    {
        GameObject cameraObject = GameObject.Find("CM_LockOn_Player");

        if (cameraObject == null)
        {
            cameraObject = new GameObject("CM_LockOn_Player");
        }

        CinemachineVirtualCamera camera =
            cameraObject.GetComponent<CinemachineVirtualCamera>();
        if (camera == null)
        {
            camera = cameraObject.AddComponent<CinemachineVirtualCamera>();
        }

        camera.Priority = 5;
        camera.Follow = cameraRoot;
        camera.LookAt = cameraTarget;
        camera.m_Lens.FieldOfView = 55f;

        // Body：保持"玩家背后"的第三人称偏移，带位置阻尼。
        CinemachineTransposer transposer =
            camera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null)
        {
            transposer = camera.AddCinemachineComponent<CinemachineTransposer>();
        }

        transposer.m_BindingMode =
            CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        transposer.m_FollowOffset = new Vector3(0f, 1.6f, -3.4f);
        transposer.m_XDamping = 0.15f;
        transposer.m_YDamping = 0.15f;
        transposer.m_ZDamping = 0.15f;

        // Aim：把 CameraTarget（平滑点）稳定在画面中心。
        CinemachineComposer composer =
            camera.GetCinemachineComponent<CinemachineComposer>();
        if (composer == null)
        {
            composer = camera.AddCinemachineComponent<CinemachineComposer>();
        }

        composer.m_TrackedObjectOffset = Vector3.zero;
        composer.m_HorizontalDamping = 0.3f;
        composer.m_VerticalDamping = 0.3f;

        return camera;
    }

    /// <summary>创建或更新 CameraRig 对象。</summary>
    private static void SetupCameraRig(
        PlayerTargeting targeting,
        Transform cameraRoot,
        Transform cameraTarget,
        CinemachineVirtualCamera lockOnCamera
    )
    {
        GameObject rigObject = GameObject.Find("CameraRig");

        if (rigObject == null)
        {
            rigObject = new GameObject("CameraRig");
        }

        LockOnCameraRig rig = rigObject.GetComponent<LockOnCameraRig>();
        if (rig == null)
        {
            rig = rigObject.AddComponent<LockOnCameraRig>();
        }

        SerializedObject rigObjectSerialized = new SerializedObject(rig);
        rigObjectSerialized.FindProperty("_targeting").objectReferenceValue =
            targeting;
        rigObjectSerialized.FindProperty("_cameraRoot").objectReferenceValue =
            cameraRoot;
        rigObjectSerialized.FindProperty("_cameraTarget").objectReferenceValue =
            cameraTarget;
        rigObjectSerialized.FindProperty("_lockOnCamera").objectReferenceValue =
            lockOnCamera;
        rigObjectSerialized.FindProperty("_smoothTime").floatValue = 0.35f;
        rigObjectSerialized.FindProperty("_rootTurnSharpness").floatValue = 2.5f;
        rigObjectSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 敌人处理：
    /// 1. 把场景实例上的组件级修改（参数/材质等）固化回预制体，
    ///    使场景与预制体一致、后续新实例继承同样参数；
    ///    位置/旋转等实例级差异保留在场景实例上。
    /// 2. 场景中不足两个敌人时，复制出第二个放在玩家左前侧。
    /// </summary>
    private static void EnsureTwoEnemies()
    {
        GameObject enemyInstance = GameObject.Find("EnemyDummy_Day0");
        if (enemyInstance == null)
        {
            Debug.LogWarning("场景中找不到 EnemyDummy_Day0，跳过敌人处理。");
            return;
        }

        ApplyEnemyComponentOverrides(enemyInstance);
        EnsureSecondEnemy(enemyInstance);
    }

    /// <summary>
    /// 把场景敌人的组件级 overrides 固化回预制体资产。
    /// 注意：GetPropertyModifications 返回的 target 是"资产中的对应对象"，
    /// 需要先映射回场景实例对象才能调用 ApplyObjectOverride；
    /// 根对象的位置/旋转/名字等实例级差异需要保留，不参与固化。
    /// </summary>
    private static void ApplyEnemyComponentOverrides(GameObject enemyInstance)
    {
        PropertyModification[] modifications =
            PrefabUtility.GetPropertyModifications(enemyInstance);
        if (modifications == null || modifications.Length == 0)
            return;

        Object assetRoot =
            PrefabUtility.GetCorrespondingObjectFromSource(enemyInstance);
        Object assetRootTransform = PrefabUtility.GetCorrespondingObjectFromSource(
            enemyInstance.transform
        );

        Dictionary<Object, Object> assetToScene = BuildAssetToSceneMap(enemyInstance);

        HashSet<Object> handled = new HashSet<Object>();
        int appliedCount = 0;

        foreach (PropertyModification modification in modifications)
        {
            Object target = modification.target;
            if (target == null) continue;

            // 根对象（位置/旋转/名字/层等）保持实例级差异，不固化。
            if (target == enemyInstance) continue;
            if (target == enemyInstance.transform) continue;
            if (target == assetRoot) continue;
            if (target == assetRootTransform) continue;

            // 资产对象 -> 场景实例对象
            Object sceneTarget = target;
            if (!PrefabUtility.IsPartOfPrefabInstance(target) &&
                assetToScene.TryGetValue(target, out Object mapped))
            {
                sceneTarget = mapped;
            }

            if (!handled.Add(sceneTarget)) continue;

            if (!PrefabUtility.IsPartOfPrefabInstance(sceneTarget))
            {
                Debug.LogWarning(
                    $"跳过无法固化的修改项：{sceneTarget.name} " +
                    $"({modification.propertyPath})"
                );
                continue;
            }

            try
            {
                PrefabUtility.ApplyObjectOverride(
                    sceneTarget,
                    EnemyPrefabPath,
                    InteractionMode.AutomatedAction
                );
                appliedCount++;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    $"固化修改失败（已跳过）：{sceneTarget.name} - " +
                    exception.Message
                );
            }
        }

        Debug.Log($"敌人组件修改已固化回预制体（{appliedCount} 个对象）。");
    }

    /// <summary>建立"预制体资产内对象 -> 场景实例对象"的映射。</summary>
    private static Dictionary<Object, Object> BuildAssetToSceneMap(
        GameObject instanceRoot
    )
    {
        Dictionary<Object, Object> map = new Dictionary<Object, Object>();

        foreach (Transform sceneTransform in
            instanceRoot.GetComponentsInChildren<Transform>(true))
        {
            Object assetTransform =
                PrefabUtility.GetCorrespondingObjectFromSource(sceneTransform);
            if (assetTransform != null)
                map[assetTransform] = sceneTransform;

            foreach (Component component in
                sceneTransform.GetComponents<Component>())
            {
                if (component == null) continue;

                Object assetComponent =
                    PrefabUtility.GetCorrespondingObjectFromSource(component);
                if (assetComponent != null)
                    map[assetComponent] = component;
            }
        }

        return map;
    }

    /// <summary>保证场景中存在两个敌人实例（第二个位于玩家左前侧）。</summary>
    private static void EnsureSecondEnemy(GameObject enemyInstance)
    {
        EnemyHealth[] enemies = Object.FindObjectsByType<EnemyHealth>(
            FindObjectsSortMode.None
        );

        if (enemies.Length >= 2)
        {
            // 修复历史遗留：两个实例同名时给非原型的那个加后缀。
            foreach (EnemyHealth enemy in enemies)
            {
                if (enemy.gameObject == enemyInstance) continue;
                if (enemy.name == enemyInstance.name)
                {
                    enemy.name = "EnemyDummy_Day0 (1)";
                    Debug.Log($"已将第二个敌人重命名为：{enemy.name}");
                }
            }

            Debug.Log($"场景中已有 {enemies.Length} 个敌人，跳过复制。");
            return;
        }

        GameObject sourcePrefab =
            PrefabUtility.GetCorrespondingObjectFromSource(enemyInstance);
        if (sourcePrefab == null)
        {
            Debug.LogError("无法获取敌人的源预制体，复制失败。");
            return;
        }

        GameObject clone = (GameObject)PrefabUtility.InstantiatePrefab(
            sourcePrefab,
            enemyInstance.scene
        );
        clone.transform.position = new Vector3(-3f, 0f, 2f);
        clone.transform.rotation = enemyInstance.transform.rotation;
        clone.name = "EnemyDummy_Day0 (1)";

        Debug.Log($"已创建第二个敌人实例：{clone.name} @ (-3, 0, 2)。");
    }
}
