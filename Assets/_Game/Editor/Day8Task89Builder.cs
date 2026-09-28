using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Day8Task89Builder
{
    public const string SoulDropPath = "Assets/_Game/Prefabs/World/SoulDrop.prefab";
    private const string MaterialPath = "Assets/_Game/Materials/M_SoulDrop_Placeholder.mat";
    private const string PlayerPath = "Assets/_Game/Prefabs/Characters/Player_Day1.prefab";

    [MenuItem("Tools/SoulsLike RPG/Day8/Build Task8-9 In Open Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().path != Day8Task123Builder.ScenePath)
            throw new InvalidOperationException("请在检查点场景的 Edit Mode 中配置 Task8–9。");

        GameObject playerPrefab = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            SetStartingSouls(playerPrefab.GetComponent<SoulWallet>());
            PrefabUtility.SaveAsPrefabAsset(playerPrefab, PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(playerPrefab);
        }

        SoulWallet wallet = Object.FindObjectsOfType<SoulWallet>(true)
            .Single(candidate => candidate.gameObject.scene == EditorSceneManager.GetActiveScene());
        Undo.RecordObject(wallet, "Set initial Soul to 1000");
        if (SetStartingSouls(wallet))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(wallet);
            EditorSceneManager.MarkSceneDirty(wallet.gameObject.scene);
        }

        CreateSoulDrop();
        AssetDatabase.SaveAssets();
        if (wallet.gameObject.scene.isDirty)
            EditorSceneManager.SaveScene(wallet.gameObject.scene);
        Debug.Log("[Day8 Task8–9] 初始 Soul = 1000，SoulDrop 占位 Prefab 已创建。");
    }

    private static bool SetStartingSouls(SoulWallet wallet)
    {
        if (wallet == null)
            throw new MissingComponentException("玩家缺少 SoulWallet。");
        SerializedObject serialized = new SerializedObject(wallet);
        serialized.FindProperty("_currentSouls").intValue = 1000;
        return serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSoulDrop()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SoulDropPath) != null)
            return;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Materials/M_Day0_Weapon.mat");
            if (source == null)
                throw new MissingReferenceException("缺少现有 URP 占位材质。");
            material = new Material(source) { name = "M_SoulDrop_Placeholder" };
            material.SetColor("_BaseColor", new Color(1f, 0.68f, 0.12f));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        GameObject root = new GameObject("SoulDrop", typeof(SoulDrop));
        try
        {
            Transform visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Placeholder";
            sphere.transform.SetParent(visualRoot, false);
            sphere.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            sphere.transform.localScale = Vector3.one * 0.35f;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.GetComponent<Renderer>().sharedMaterial = material;

            GameObject colliderObject = new GameObject("Collider", typeof(SphereCollider));
            colliderObject.transform.SetParent(root.transform, false);
            SphereCollider trigger = colliderObject.GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.45f, 0f);
            trigger.radius = 0.5f;

            SerializedObject serialized = new SerializedObject(root.GetComponent<SoulDrop>());
            serialized.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SoulDropPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
