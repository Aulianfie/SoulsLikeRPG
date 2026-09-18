using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class Day0ImportUtility
{
    private static readonly string[] HumanoidModels =
    {
        "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx",
        "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard.fbx",
        "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/Models/Mannequin_F.fbx"
    };

    [MenuItem("Day0/Configure Humanoid Imports")]
    private static void ConfigureHumanoidImports()
    {
        foreach (string path in HumanoidModels)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                Debug.LogError($"[Day0] ModelImporter not found: {path}");
                continue;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.importAnimation = !path.EndsWith("Mannequin_F.fbx", StringComparison.Ordinal);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.optimizeGameObjects = false;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool loops = IsLoopingClip(clip.name);
                clip.loopTime = loops;
                clip.loopPose = loops;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = false;
                clip.keepOriginalPositionY = false;
                clip.keepOriginalPositionXZ = false;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            ReportModel(path);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Day0] Humanoid import configuration completed.");
    }

    [MenuItem("Day0/Report Imported Animations")]
    private static void ReportImportedAnimations()
    {
        foreach (string path in HumanoidModels)
        {
            ReportModel(path);
        }
    }

    private static bool IsLoopingClip(string clipName)
    {
        string name = clipName.ToLowerInvariant();
        return name.Contains("idle") || name.Contains("walk") || name.Contains("jog") ||
               name.Contains("run") || name.Contains("sprint") || name.Contains("strafe");
    }

    private static void ReportModel(string path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Avatar avatar = assets.OfType<Avatar>().FirstOrDefault();
        string clips = string.Join(", ", assets.OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .Select(clip => clip.name)
            .OrderBy(name => name));

        Debug.Log($"[Day0] {path} | Avatar valid={avatar != null && avatar.isValid}, human={avatar != null && avatar.isHuman} | Clips: {clips}");
    }
}
