using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 临时候选动作检查工具，交付前移出 Assets。
public static class WeaponSwitchPreview
{
    public const string Folder = "Assets/ThirdParty/DoubleL/Animations/WeaponSwitch";
    public static void Prepare()
    {
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/DoubleL/Models/T-Pose.fbx").OfType<Avatar>().Single();
        if (!avatar.isValid || !avatar.isHuman) throw new Exception("Source Avatar invalid");
        string report = "";
        foreach (string path in Directory.GetFiles(Folder, "*.fbx"))
        {
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
            if (!motion.humanMotion) throw new Exception("Not Humanoid: " + path);
            report += $"{motion.name}: seconds={motion.length}; human={motion.humanMotion}\n";
        }
        File.WriteAllText("Logs/WeaponSwitch_Clips.txt", report);
    }

    public static void Preview()
    {
        var previewScene = EditorSceneManager.NewPreviewScene();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Day10Setup.PlayerPath);
        GameObject actor = Object.Instantiate(prefab);
        var cameraObject = new GameObject("SwitchPreviewCamera");
        var lightObject = new GameObject("SwitchPreviewLight");
        SceneManager.MoveGameObjectToScene(actor, previewScene);
        SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
        SceneManager.MoveGameObjectToScene(lightObject, previewScene);
        RenderTexture target = new RenderTexture(320, 380, 24);
        var sheet = new Texture2D(1600, 1520, TextureFormat.RGB24, false);
        var sample = new Texture2D(320, 380, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        PlayableGraph graph = default;
        try
        {
            actor.hideFlags = cameraObject.hideFlags = lightObject.hideFlags = HideFlags.HideAndDontSave;
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Transform child in actor.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            foreach (MonoBehaviour script in actor.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            Animator animator = actor.GetComponentInChildren<Animator>();
            if (!animator.avatar.isValid || !animator.avatar.isHuman) throw new Exception("Player Avatar invalid");
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            animator.Rebind();
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.targetTexture = target; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .15f, .18f);
            camera.fieldOfView = 34;
            camera.transform.position = new Vector3(3.3f, 1.7f, 3.7f);
            camera.transform.LookAt(new Vector3(0, .95f, 0));
            Light light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.5f; light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(35, -30, 0);
            string[] names = { "1Hand_Base_Weapon_Change_R_1", "1Hand_Base_Weapon_Change_R_2", "2Hand_Base_Weapon_Change_1", "2Hand_Base_Weapon_Change_2" };
            string poses = "Columns=0,.25,.5,.75,1 normalized; rows=" + string.Join(",", names) + "\n";
            for (int row = 0; row < names.Length; row++)
            {
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + names[row] + ".fbx").OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
                graph = PlayableGraph.Create("SwitchPreview"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator); output.SetSourcePlayable(playable);
                graph.Play();
                for (int col = 0; col < 5; col++)
                {
                    playable.SetTime(clip.length * col / 4.0); playable.SetDone(false); graph.Evaluate(0);
                    camera.Render(); RenderTexture.active = target;
                    sample.ReadPixels(new Rect(0, 0, 320, 380), 0, 0); sample.Apply();
                    sheet.SetPixels(col * 320, (3 - row) * 380, 320, 380, sample.GetPixels());
                    Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    poses += names[row] + " t=" + col / 4f + "; rightHand=" + actor.transform.InverseTransformPoint(hand.position) + "\n";
                }
                graph.Destroy();
            }
            sheet.Apply(); File.WriteAllBytes("Logs/WeaponSwitch_GenericPreview.png", sheet.EncodeToPNG());
            File.WriteAllText("Logs/WeaponSwitch_GenericPoses.txt", poses);
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            RenderTexture.active = previous;
            Object.DestroyImmediate(actor); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(target); Object.DestroyImmediate(sample); Object.DestroyImmediate(sheet);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }
}
