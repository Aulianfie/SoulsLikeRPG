using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LostSoulModelSetup
{
    private const string ModelPath = "Assets/ThirdParty/LostSoul/Models/LostSoul_Game.fbx";
    private const string TextureFolder = "Assets/ThirdParty/LostSoul/Textures/";
    private const string PackedPath = "Assets/_Game/Textures/SoulDrop/LostSoul_MetallicSmoothness.png";
    private const string MaterialPath = "Assets/_Game/Materials/M_LostSoul.mat";
    private const string PreviewName = "SoulDrop_ModelPreview";

    [MenuItem("Tools/SoulsLike RPG/Day8/Apply LostSoul Model")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().path != Day8Task123Builder.ScenePath)
            throw new InvalidOperationException("请在检查点场景的 Edit Mode 中应用模型。");
        ConfigureImporters();
        Material material = CreateMaterial();
        string guid = AssetDatabase.AssetPathToGUID(Day8Task89Builder.SoulDropPath);
        GameObject prefab = PrefabUtility.LoadPrefabContents(Day8Task89Builder.SoulDropPath);
        try
        {
            Transform visual = prefab.GetComponent<SoulDrop>().VisualRoot;
            Transform placeholder = visual.Find("Placeholder");
            if (placeholder != null)
                Object.DestroyImmediate(placeholder.gameObject);
            Transform previousModel = visual.Find("SoulModel");
            if (previousModel != null)
                Object.DestroyImmediate(previousModel.gameObject);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), visual);
            model.name = "SoulModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            Bounds bounds = CombinedBounds(model);
            model.transform.localScale *= 0.65f / bounds.size.y;
            bounds = CombinedBounds(model);
            model.transform.position += visual.TransformPoint(new Vector3(0f, 0.45f, 0f)) - bounds.center;
            PrefabUtility.SaveAsPrefabAsset(prefab, Day8Task89Builder.SoulDropPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        if (AssetDatabase.AssetPathToGUID(Day8Task89Builder.SoulDropPath) != guid)
            throw new InvalidOperationException("SoulDrop Prefab GUID 意外改变。");
        PlacePreview();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Validate();
        Debug.Log("[LostSoul] 已替换 SoulDrop 的视觉模型并保存场景预览。");
    }

    private static void ConfigureImporters()
    {
        ModelImporter model = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        model.animationType = ModelImporterAnimationType.None;
        model.importAnimation = false;
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.meshCompression = ModelImporterMeshCompression.Medium;
        model.isReadable = false;
        model.addCollider = false;
        model.importBlendShapes = false;
        model.importCameras = false;
        model.importLights = false;
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
        model.SaveAndReimport();

        ConfigureTexture(TextureFolder + "texture_pbr_20250901.png", false, true);
        ConfigureTexture(TextureFolder + "texture_pbr_20250901_normal.png", true, false);
        ConfigureTexture(TextureFolder + "texture_pbr_20250901_roughness.png", false, false);
        ConfigureTexture(TextureFolder + "texture_pbr_20250901_metallic.png", false, false);
        ConfigureTexture(PackedPath, false, false);
    }

    private static void ConfigureTexture(string path, bool normal, bool srgb)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.isReadable = false;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.alphaSource = path == PackedPath ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        var settings = importer.GetPlatformTextureSettings("Standalone");
        settings.overridden = true;
        settings.maxTextureSize = 1024;
        settings.format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
        settings.compressionQuality = 100;
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static Material CreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_Day0_Weapon.mat");
            material = new Material(source) { name = "M_LostSoul" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "texture_pbr_20250901.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "texture_pbr_20250901_normal.png"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PackedPath));
        material.SetFloat("_BumpScale", 1f);
        material.SetFloat("_Smoothness", 1f);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void PlacePreview()
    {
        if (Object.FindObjectOfType<PlayerSoulDrop>() != null)
            return; // 已接入死亡掉魂时，由运行时生成实例。
        GameObject instance = GameObject.Find(PreviewName);
        if (instance != null)
            return;
        GameSaveData save = SaveService.Load();
        CheckpointSite[] sites = Object.FindObjectsOfType<CheckpointSite>();
        CheckpointSite site = sites.FirstOrDefault(candidate => candidate.CheckpointId == save?.checkpointId)
            ?? sites.First(candidate => candidate.CanInteract);
        Transform spawn = site.RespawnPoint;
        Vector3 position = spawn.position + spawn.forward * 1.5f + spawn.right * 0.8f;
        if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit,
                6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y;
        instance = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(Day8Task89Builder.SoulDropPath));
        instance.name = PreviewName;
        instance.transform.SetPositionAndRotation(position, spawn.rotation);
        Undo.RegisterCreatedObjectUndo(instance, "Place LostSoul preview");
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }

    [MenuItem("Tools/SoulsLike RPG/Day8/Validate LostSoul Model")]
    public static void Validate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Day8Task89Builder.SoulDropPath);
        SoulDrop drop = prefab.GetComponent<SoulDrop>();
        if (drop.VisualRoot == null || drop.VisualRoot.Find("SoulModel") == null ||
            drop.VisualRoot.GetComponentsInChildren<Collider>().Length != 0 ||
            prefab.transform.Find("Collider").GetComponent<SphereCollider>() == null)
            throw new InvalidOperationException("魂模型根节点或独立碰撞节点无效。");
        Mesh[] meshes = drop.VisualRoot.GetComponentsInChildren<MeshFilter>()
            .Select(filter => filter.sharedMesh).Distinct().ToArray();
        long triangles = meshes.Sum(mesh => Enumerable.Range(0, mesh.subMeshCount)
            .Sum(index => (long)mesh.GetIndexCount(index) / 3));
        string[] textures = { TextureFolder + "texture_pbr_20250901.png",
            TextureFolder + "texture_pbr_20250901_normal.png", PackedPath };
        foreach (string path in textures)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture.width > 1024 || texture.height > 1024 || texture.isReadable)
                throw new InvalidOperationException("贴图导入设置无效：" + path);
        }
        if (meshes.Any(mesh => mesh.isReadable) || triangles > 12000)
            throw new InvalidOperationException("游戏网格未按计划优化。");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/LostSoul_UnityValidation.txt",
            $"Triangles={triangles}\nVertices={meshes.Sum(mesh => mesh.vertexCount)}\n" +
            $"VisualHeight={CombinedBounds(prefab).size.y:F3}\n" +
            string.Join("\n", textures.Select(path =>
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                return $"{path}: {texture.width}x{texture.height}, {texture.format}";
            })));
        Debug.Log($"[LostSoul] 验证通过：{triangles:N0} 三角面，1024 压缩贴图，原脚本与碰撞节点保留。");
    }

    private static Bounds CombinedBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            throw new InvalidOperationException("模型缺少 Renderer。");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1))
            bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
}
