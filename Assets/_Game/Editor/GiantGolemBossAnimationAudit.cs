using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GiantGolemBossAnimationAudit
{
    public const string SourceRoot = "Assets/ThirdParty/GiantGolemAnimSet/Animation/Humanoid/";

    public static readonly string[] AttackNames =
    {
        "attack01",
        "attack02",
        "attack03",
        "attack04",
        "attack_foot_left",
        "attack_foot_right",
        "attack_DashAtk",
        "attack_whirlwind",
        "attack_jumpAtk",
        "attack_throwstone"
    };

    [Serializable]
    public class Pose
    {
        public float normalized;
        public Vector3 leftHand;
        public Vector3 rightHand;
        public Vector3 leftFoot;
        public Vector3 rightFoot;
        public Vector3 hips;
    }

    [Serializable]
    public class Motion
    {
        public string name;
        public float seconds;
        public Pose[] poses;
        public Pose[] finePoses;
    }

    [Serializable]
    public class Audit
    {
        public float sourceHeight;
        public float scale;
        public float height;
        public bool avatarValid;
        public Motion[] motions;
    }

    [MenuItem("Tools/SoulsLike RPG/Giant Golem/1 Audit Animation Poses")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Run animation audit in Edit Mode");
        }

        Scene preview = EditorSceneManager.NewPreviewScene();
        var result = new Audit();
        Material body = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        body.color = new Color(.32f, .42f, .48f);
        Material floor = new Material(body.shader);
        floor.color = new Color(.11f, .13f, .16f);
        RenderTexture previous = RenderTexture.active;
        var target = new RenderTexture(320, 240, 24);
        try
        {
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SourceRoot + "00_T-pose_golem.FBX"));
            SceneManager.MoveGameObjectToScene(actor, preview);
            foreach (Transform child in actor.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 31;
            }

            var renderers = actor.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            result.sourceHeight = bounds.size.y;
            result.height = 4.4f;
            result.scale = result.height / result.sourceHeight;
            actor.transform.localScale = Vector3.one * result.scale;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => body).ToArray();
                bounds.Encapsulate(renderer.bounds);
            }

            actor.transform.position -= Vector3.up * bounds.min.y;
            var animator = actor.GetComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(SourceRoot + "00_T-pose_golem.FBX").OfType<Avatar>().First();
            result.avatarValid = animator.avatar.isValid &&
                animator.avatar.isHuman;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // 同一编辑器帧采样多个姿态时，显式烘焙蒙皮，避免 Camera.Render 显示上一帧的网格。
            var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bakedMeshes = new List<Mesh>();
            foreach (var skin in skins)
            {
                var mesh = new Mesh();
                bakedMeshes.Add(mesh);
                var baked = new GameObject("SampledSkin", typeof(MeshFilter), typeof(MeshRenderer));
                baked.transform.SetParent(skin.transform, false);
                baked.layer = 31;
                baked.transform.localScale = Vector3.one / result.scale;
                baked.GetComponent<MeshFilter>().sharedMesh = mesh;
                baked.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = 31;
            SceneManager.MoveGameObjectToScene(ground, preview);
            ground.GetComponent<Renderer>().sharedMaterial = floor;
            var cameraObject = new GameObject("GolemAuditCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 3.3f;
            camera.cullingMask = 1 << 31;
            camera.transform.position = new Vector3(7, 5, 9);
            camera.transform.LookAt(new Vector3(0, 2.1f, 0));
            camera.backgroundColor = new Color(.06f, .07f, .09f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = false;
            camera.targetTexture = target;
            var lightObject = new GameObject("GolemAuditLight", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, preview);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(40, -25, 0);
            Directory.CreateDirectory("Docs/GiantGolem_Poses");
            var motions = new List<Motion>();
            foreach (string name in AttackNames)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(SourceRoot + "Inplace/" + name + "_inplace.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                var motion = new Motion
                {
                    name = name,
                    seconds = clip.length,
                    poses = new Pose[12],
                    finePoses = new Pose[100]
                };
                var graph = PlayableGraph.Create("GolemPoseAudit");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Audit", animator).SetSourcePlayable(playable);
                graph.Play();
                var sheet = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                var cell = new Texture2D(320, 240, TextureFormat.RGB24, false);
                try
                {
                    for (int i = 0; i < 12; i++)
                    {
                        float t = i / 11f;
                        playable.SetTime(clip.length * Mathf.Min(t, .999f));
                        graph.Evaluate(0);
                        motion.poses[i] = new Pose
                        {
                            normalized = t,
                            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position,
                            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position,
                            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position,
                            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot).position,
                            hips = animator.GetBoneTransform(HumanBodyBones.Hips).position
                        };
                        for (int skinIndex = 0; skinIndex < skins.Length; skinIndex++)
                        {
                            skins[skinIndex].BakeMesh(bakedMeshes[skinIndex]);
                        }

                        camera.Render();
                        RenderTexture.active = target;
                        cell.ReadPixels(new Rect(0, 0, 320, 240), 0, 0);
                        cell.Apply();
                        sheet.SetPixels((i % 4) * 320, (2 - i / 4) * 240, 320, 240, cell.GetPixels());
                    }

                    sheet.Apply();
                    File.WriteAllBytes("Docs/GiantGolem_Poses/" + name + ".png", sheet.EncodeToPNG());
                    for (int i = 0; i < motion.finePoses.Length; i++)
                    {
                        float t = i / 100f;
                        playable.SetTime(clip.length * t);
                        graph.Evaluate(0);
                        motion.finePoses[i] = new Pose
                        {
                            normalized = t,
                            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position,
                            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position,
                            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position,
                            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot).position,
                            hips = animator.GetBoneTransform(HumanBodyBones.Hips).position
                        };
                    }
                }
                finally
                {
                    graph.Destroy();
                    Object.DestroyImmediate(sheet);
                    Object.DestroyImmediate(cell);
                }

                motions.Add(motion);
            }

            result.motions = motions.ToArray();
            File.WriteAllText("Docs/GiantGolem_AnimationMeasurements.json", JsonUtility.ToJson(result, true));
            foreach (var mesh in bakedMeshes)
            {
                Object.DestroyImmediate(mesh);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(body);
            Object.DestroyImmediate(floor);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
