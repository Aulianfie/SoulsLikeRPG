using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class DeathSoulDropSetup
{
    [MenuItem("Tools/SoulsLike RPG/Day8/Setup Death Soul Drop")]
    public static void Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != Day8Task123Builder.ScenePath)
            throw new InvalidOperationException("请在检查点场景的 Edit Mode 配置掉魂。");
        SoulDrop drop = AssetDatabase.LoadAssetAtPath<GameObject>(Day8Task89Builder.SoulDropPath)
            .GetComponent<SoulDrop>();
        const string playerPath = "Assets/_Game/Prefabs/Characters/Player_Day1.prefab";
        GameObject prefab = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            Bind(prefab, drop);
            PrefabUtility.SaveAsPrefabAsset(prefab, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (PlayerHealth player in Object.FindObjectsOfType<PlayerHealth>())
        {
            if (player.gameObject.scene == scene)
                Bind(player.gameObject, drop);
        }
        GameObject preview = GameObject.Find("SoulDrop_ModelPreview");
        if (preview != null)
            Undo.DestroyObjectImmediate(preview);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Death Soul Drop] 玩家已绑定掉魂 Prefab，静态预览已移除；保留 SoulDrop 模型与缩放。");
    }

    private static void Bind(GameObject player, SoulDrop prefab)
    {
        PlayerSoulDrop controller = player.GetComponent<PlayerSoulDrop>();
        if (controller == null)
            controller = Undo.AddComponent<PlayerSoulDrop>(player);
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("_soulDropPrefab").objectReferenceValue = prefab;
        serialized.ApplyModifiedProperties();
        if (PrefabUtility.IsPartOfPrefabInstance(controller))
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
    }
}
