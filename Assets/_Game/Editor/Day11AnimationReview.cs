using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Isolated editor preview: never edits the gameplay scene, prefab or controller.
public sealed class Day11AnimationReview : EditorWindow
{
    public const string Folder = "Assets/ThirdParty/DoubleL/Day11_Candidates";
    private string[] _paths = Array.Empty<string>();
    private int _index;
    private float _time;
    private bool _playing;
    private double _lastUpdate;
    private ReviewActor _actor;
    private AnimationClip _clip;
    private float _yaw = 35f;

    [MenuItem("Tools/SoulsLike RPG/Day11/Animation Candidates")]
    public static void Open() => GetWindow<Day11AnimationReview>("Day11 动画审核");

    private void OnEnable()
    {
        _paths = CandidatePaths();
        EditorApplication.update += Tick;
        _lastUpdate = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        _actor?.Dispose();
        _actor = null;
    }

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_playing && _clip != null) _time = (_time + (float)(now - _lastUpdate)) % _clip.length;
        _lastUpdate = now;
        if (_playing) Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("当前 Player + 实际武器的独立预览。Hold / End 是配套片段；审核后再决定保留。", MessageType.Info);
        if (_paths.Length == 0) { EditorGUILayout.LabelField("未找到候选。"); return; }
        int chosen = EditorGUILayout.Popup("候选", _index, _paths.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray());
        if (_actor == null || chosen != _index) Load(chosen);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("上一个")) Load((_index + _paths.Length - 1) % _paths.Length);
        _playing = GUILayout.Toggle(_playing, "播放", "Button");
        if (GUILayout.Button("下一个")) Load((_index + 1) % _paths.Length);
        EditorGUILayout.EndHorizontal();
        _time = EditorGUILayout.Slider("时间（秒）", _time, 0f, _clip.length);
        _yaw = EditorGUILayout.Slider("观察角度", _yaw, -180f, 180f);
        EditorGUILayout.LabelField($"{_clip.length:F3}s · Humanoid={_clip.humanMotion}");
        if (GUILayout.Button("在 Project 中定位 FBX")) EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(_paths[_index]));
        Rect rect = GUILayoutUtility.GetRect(100f, 1000f, 180f, 1000f);
        if (Event.current.type == EventType.Repaint)
        {
            _actor.Sample(_time);
            GUI.DrawTexture(rect, _actor.Render(rect, _yaw), ScaleMode.ScaleToFit, false);
        }
    }

    private void Load(int index)
    {
        _actor?.Dispose();
        _index = index;
        _clip = Clip(_paths[index]);
        _actor = new ReviewActor(_clip, _paths[index].Contains("/GreatSword/"));
        _time = 0;
    }

    public static string[] CandidatePaths() => Directory.GetFiles(Folder, "*.fbx", SearchOption.AllDirectories)
        .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
    public static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));

    // Batch entry point; saves only candidate imports and review evidence.
    [MenuItem("Tools/SoulsLike RPG/Day11/Validate and Export Candidate Previews")]
    public static void ValidateBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode 再导出动画预览。");
        Directory.CreateDirectory("Logs/Day11/Previews");
        Avatar source = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/DoubleL/Models/T-Pose.fbx").OfType<Avatar>().Single();
        if (!source.isHuman || !source.isValid) throw new InvalidOperationException("DoubleL source Avatar invalid");
        var report = new StringBuilder("Name\tSeconds\tHumanoid\tPlayerAvatarValid\tRootRangeMeters\tHipsRangeMeters\tMinHandDistanceMeters\tRootHorizontalRangeMeters\tRootVerticalRangeMeters\tPath\n");
        foreach (string path in CandidatePaths())
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = source;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = importer.importLights = false;
            importer.SaveAndReimport();
            AnimationClip clip = Clip(path);
            if (!clip.humanMotion || clip.length <= 0) throw new InvalidOperationException("Invalid candidate: " + path);
            using (var actor = new ReviewActor(clip, path.Contains("/GreatSword/")))
            {
                Vector3 initialPosition = actor.Animator.transform.localPosition;
                Quaternion initialRotation = actor.Animator.transform.localRotation;
                Bounds rootBounds = new Bounds(actor.Animator.transform.position, Vector3.zero);
                actor.Animator.applyRootMotion = true;
                actor.Sample(0);
                for (int i = 0; i < 60; i++)
                {
                    actor.Advance(clip.length / 60f);
                    rootBounds.Encapsulate(actor.Animator.transform.position);
                }
                actor.Animator.applyRootMotion = false;
                actor.Animator.transform.localPosition = initialPosition;
                actor.Animator.transform.localRotation = initialRotation;
                actor.Sample(0);
                Bounds hipsBounds = new Bounds(actor.Animator.GetBoneTransform(HumanBodyBones.Hips).position, Vector3.zero);
                float minHands = float.MaxValue;
                for (int i = 0; i <= 60; i++)
                {
                    actor.Sample(clip.length * i / 60f);
                    rootBounds.Encapsulate(actor.Animator.transform.position);
                    hipsBounds.Encapsulate(actor.Animator.GetBoneTransform(HumanBodyBones.Hips).position);
                    minHands = Mathf.Min(minHands, Vector3.Distance(actor.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position,
                        actor.Animator.GetBoneTransform(HumanBodyBones.RightHand).position));
                }
                report.AppendLine($"{clip.name}\t{clip.length:F3}\t{clip.humanMotion}\t{actor.Animator.avatar.isValid}\t{rootBounds.size.magnitude:F4}\t{hipsBounds.size.magnitude:F4}\t{minHands:F4}\t{new Vector2(rootBounds.size.x, rootBounds.size.z).magnitude:F4}\t{rootBounds.size.y:F4}\t{path}");
                if (!path.Contains("_Hold") && !path.Contains("_End"))
                {
                    var sheet = new Texture2D(1500, 400, TextureFormat.RGB24, false);
                    for (int i = 0; i < 5; i++)
                    {
                        actor.Sample(clip.length * (0.05f + 0.9f * i / 4f));
                        RenderTexture rendered = actor.Render(new Rect(0, 0, 300, 400), 35f) as RenderTexture;
                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = rendered;
                        sheet.ReadPixels(new Rect(0, 0, 300, 400), i * 300, 0);
                        RenderTexture.active = previous;
                    }
                    sheet.Apply();
                    File.WriteAllBytes("Logs/Day11/Previews/" + clip.name + ".png", sheet.EncodeToPNG());
                    DestroyImmediate(sheet);
                }
            }
            Debug.Log("[Day11] Reviewed " + clip.name);
        }
        File.WriteAllText("Logs/Day11/animation_validation.tsv", report.ToString(), new UTF8Encoding(false));
        AssetDatabase.SaveAssets();
        Debug.Log("[Day11] VALIDATION_COMPLETE");
    }

    private sealed class ReviewActor : IDisposable
    {
        private readonly PreviewRenderUtility _preview;
        private readonly GameObject _root;
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private readonly System.Collections.Generic.List<Material> _materials = new System.Collections.Generic.List<Material>();
        private readonly System.Collections.Generic.List<(SkinnedMeshRenderer renderer, Mesh mesh)> _baked = new System.Collections.Generic.List<(SkinnedMeshRenderer, Mesh)>();
        private Bounds _framing;
        public Animator Animator { get; }

        public ReviewActor(AnimationClip clip, bool greatSword)
        {
            _preview = new PreviewRenderUtility();
            _root = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/Player_Day1.prefab"));
            _root.hideFlags = HideFlags.HideAndDontSave;
            _root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (MonoBehaviour behaviour in _root.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (Camera camera in _root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (AudioListener listener in _root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (SkinnedMeshRenderer renderer in _root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
            // Neutral, unlit preview materials make silhouettes and weapon intersections readable under URP.
            foreach (Renderer renderer in _root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    material.SetColor("_BaseColor", renderer is SkinnedMeshRenderer ? new Color(0.45f, 0.68f, 0.88f) : new Color(0.9f, 0.83f, 0.55f));
                    _materials.Add(material);
                    return material;
                }).ToArray();
            }
            // Explicit CPU baking prevents stale GPU skinning in offscreen batch previews.
            foreach (SkinnedMeshRenderer renderer in _root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                var display = new GameObject("Day11_BakedPreview");
                display.transform.SetParent(renderer.transform, false);
                display.AddComponent<MeshFilter>().sharedMesh = mesh;
                display.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                _baked.Add((renderer, mesh));
                renderer.enabled = false;
            }
            foreach (Transform child in _root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Weapon_GreatSword") child.gameObject.SetActive(greatSword);
                if (child.name == "Weapon_OneHand") child.gameObject.SetActive(!greatSword);
            }
            Animator = _root.GetComponentsInChildren<Animator>().First(a => a.avatar != null && a.avatar.isHuman);
            if (!Animator.avatar.isValid) throw new InvalidOperationException("Current Player Avatar invalid");
            Animator.runtimeAnimatorController = null;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Animator.applyRootMotion = false;
            _preview.AddSingleGO(_root);
            _graph = PlayableGraph.Create("Day11 isolated animation review");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _playable = AnimationClipPlayable.Create(_graph, clip);
            _playable.SetApplyFootIK(false);
            _playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(_graph, "Review", Animator).SetSourcePlayable(_playable);
            _graph.Play();
            Animator.Rebind();
            bool firstBounds = true;
            for (int i = 0; i <= 30; i++)
            {
                Sample(clip.length * i / 30f);
                foreach (Renderer renderer in _root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) continue;
                    if (firstBounds) { _framing = renderer.bounds; firstBounds = false; }
                    else _framing.Encapsulate(renderer.bounds);
                }
            }
            Sample(0);
        }

        public void Sample(float seconds)
        {
            _playable.SetTime(seconds);
            _graph.Evaluate(0f);
            foreach (var baked in _baked) baked.renderer.BakeMesh(baked.mesh);
        }

        public void Advance(float seconds) => _graph.Evaluate(seconds);

        public Texture Render(Rect rect, float yaw)
        {
            _preview.BeginPreview(rect, GUIStyle.none);
            Camera camera = _preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(1.65f, _framing.size.magnitude * 0.6f / Mathf.Min(1f, rect.width / rect.height));
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            Vector3 target = _framing.center;
            camera.transform.position = target + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0.4f, 10);
            camera.transform.LookAt(target);
            _preview.lights[0].intensity = 1.3f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(40, 40, 0);
            _preview.lights[1].intensity = 0.8f;
            _preview.ambientColor = Color.gray;
            _preview.Render(true);
            return _preview.EndPreview();
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _preview.Cleanup();
            foreach (Material material in _materials) DestroyImmediate(material);
            foreach (var baked in _baked) DestroyImmediate(baked.mesh);
        }
    }
}
