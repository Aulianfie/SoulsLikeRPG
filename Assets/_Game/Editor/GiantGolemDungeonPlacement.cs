using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class GiantGolemDungeonPlacement
{
    public const string ScenePath = "Assets/_Game/Scenes/05_AncientDungeon_GiantGolem.unity";
    public const string MainScenePath = "Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity";
    public const string MainReportPath = "Docs/GiantGolem_MainScenePlacement.json";
    public static bool IsDungeonScene(string path) => path == ScenePath || path == MainScenePath;
    const string NavPath = "Assets/_Game/Navigation/NM_AncientDungeon_GiantGolem.asset";
    const string ReportPath = "Docs/GiantGolem_DungeonPlacement.json";
    [Serializable] public class PlacementReport
    {
        public string scene, sourceScene, navigation, timestamp;
        public Vector3 requested, placed, navSize, approach;
        public Quaternion rotation;
        public float agentRadius, agentHeight, voxelSize;
        public int sourceCount, reachableSamples, checkedSamples;
        public bool spawnOnNavMesh, bodyClear, savedReloadVerified, runtimeOnNavMesh, runtimeChase, runtimeReset;
        public bool runtimeWeaponHud, runtimeQuickItemHud, runtimeCheckpointReset, saveBytesRestored;
    }
    public static readonly Vector3 Requested = new Vector3(-88.5f, 39.5f, -169);
    public static void Inspect()
    {
        var scene = SceneManager.GetActiveScene(); Physics.SyncTransforms();
        var text = new System.Text.StringBuilder();
        text.AppendLine("scene=" + scene.path + " dirty=" + scene.isDirty + " playing=" + EditorApplication.isPlaying);
        foreach (var root in scene.GetRootGameObjects()) text.AppendLine("ROOT " + root.name + " pos=" + root.transform.position + " scale=" + root.transform.lossyScale);
        foreach (var surface in Object.FindObjectsOfType<NavMeshSurface>(true)) text.AppendLine("SURFACE " + surface.name + " scene=" + surface.gameObject.scene.path + " type=" + surface.agentTypeID + " collect=" + surface.collectObjects + " geometry=" + surface.useGeometry + " mask=" + surface.layerMask.value + " center=" + surface.center + " size=" + surface.size + " data=" + AssetDatabase.GetAssetPath(surface.navMeshData));
        foreach (var player in Object.FindObjectsOfType<PlayerStateMachine>(true)) text.AppendLine("PLAYER " + player.name + " scene=" + player.gameObject.scene.path + " pos=" + player.transform.position);
        foreach (var hit in Physics.RaycastAll(Requested + Vector3.up * 15, Vector3.down, 60).OrderBy(h => Mathf.Abs(h.point.y - Requested.y)))
            text.AppendLine("GROUND " + hit.collider.name + " point=" + hit.point.ToString("F3") + " normal=" + hit.normal + " layer=" + hit.collider.gameObject.layer);
        foreach (var renderer in Object.FindObjectsOfType<Renderer>().Where(r => r.gameObject.scene == scene && Vector3.Distance(r.bounds.ClosestPoint(Requested), Requested) < 15).Take(70))
            text.AppendLine("NEAR " + renderer.name + " center=" + renderer.bounds.center.ToString("F2") + " size=" + renderer.bounds.size.ToString("F2") + " collider=" + (renderer.GetComponent<Collider>() != null));
        File.WriteAllText("Logs/GolemPlacement/Inspect.txt", text.ToString());
        var candidates = new List<Vector3>();
        for (int x = -8; x <= 8; x++) for (int z = -8; z <= 8; z++)
        {
            var point = Requested + new Vector3(x * 1.25f, 0, z * 1.25f);
            var floors = Physics.RaycastAll(point + Vector3.up * 3, Vector3.down, 5).Where(h => h.normal.y > .8f && h.collider.name.Contains("floortiles") && Mathf.Abs(h.point.y - Requested.y) < .6f).ToArray();
            if (floors.Length == 0) continue; point.y = floors[0].point.y;
            if (!Physics.CheckCapsule(point + Vector3.up * 1.02f, point + Vector3.up * 3.55f, .9f, 1, QueryTriggerInteraction.Ignore)) candidates.Add(point);
        }
        File.WriteAllLines("Logs/GolemPlacement/Candidates.txt", candidates.OrderBy(p => Vector3.Distance(p, Requested)).Take(35).Select(p => p.ToString("F3")));
        Render(Requested, "Logs/GolemPlacement/AreaBefore.png");
    }
    static void Render(Vector3 center, string path)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("PlacementPreview", typeof(Camera)); SceneManager.MoveGameObjectToScene(go, preview);
        var camera = go.GetComponent<Camera>(); camera.scene = SceneManager.GetActiveScene(); camera.enabled = false;
        camera.transform.position = center + new Vector3(15, 12, 17); camera.transform.LookAt(center + Vector3.up * 1.5f); camera.fieldOfView = 55;
        var texture = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try { camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); Object.DestroyImmediate(image); EditorSceneManager.ClosePreviewScene(preview); }
    }
    [MenuItem("Tools/SoulsLike RPG/Giant Golem/6 Create Ancient Dungeon Boss Scene")]
    public static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play Mode。");
        var original = SceneManager.GetActiveScene();
        string sourcePath = original.path;
        if (original.path != "Assets/Dark Fantasy Environment/Scenes/ANCIENT DUNGEON.unity") throw new InvalidOperationException("请在 ANCIENT DUNGEON 场景创建关卡副本。");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Boss 场景已存在，生成器不会覆盖手工修改。");
        // Save As Copy 包含当前场景内容，原场景及其导航数据不改写。
        if (!EditorSceneManager.SaveScene(original, ScenePath, true)) throw new IOException("保存场景副本失败。");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        RemoveStaleCameraScripts();
        Physics.SyncTransforms();
        var navRoot = new GameObject("Navigation_GiantGolem"); navRoot.transform.position = Requested;
        var surface = navRoot.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Volume;
        surface.center = Vector3.up * 1.5f; surface.size = new Vector3(68, 18, 68); surface.layerMask = 1;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.overrideVoxelSize = true; surface.voxelSize = .12f;
        surface.overrideTileSize = true; surface.tileSize = 256;
        // 复用当前 package 的采集流程（含 NavMeshModifier），只覆盖本次数据的体型。
        var data = Bake(surface, out var settings, out int sourceCount);
        if (data == null) throw new InvalidOperationException("Boss 区域导航烘焙失败。");
        data.name = "NM_AncientDungeon_GiantGolem"; AssetDatabase.CreateAsset(data, NavPath); surface.navMeshData = data; surface.AddData();
        var candidates = new List<Vector3>();
        for (int x = -10; x <= 10; x++) for (int z = -10; z <= 10; z++)
        {
            Vector3 candidate = Requested + new Vector3(x * 1.25f, 0, z * 1.25f);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, .45f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - Requested.y) > .5f) continue;
            if (BodyClear(hit.position)) candidates.Add(hit.position);
        }
        if (candidates.Count == 0) throw new InvalidOperationException("坐标附近没有可容纳 Boss 的导航出生点。");
        Vector3 placed = candidates.OrderBy(p => Vector3.Distance(p, Requested)).First();
        var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GiantGolemBossSetup.PrefabPath), scene);
        boss.name = "GiantGolemBoss";
        var rotation = new Quaternion(1.54543347e-8f, -.7071057558f, 1.54542885e-8f, .70710784197f);
        boss.transform.SetPositionAndRotation(placed, rotation); boss.transform.localScale = Vector3.one;
        var player = Object.FindObjectsOfType<PlayerStateMachine>().Single(p => p.gameObject.scene == scene);
        GiantGolemBossSetup.Set(boss.GetComponent<BossBrain>(), "_targetOverride", player.transform);
        GiantGolemBossSetup.Set(boss.GetComponent<EnemyReward>(), "_wallet", player.GetComponent<SoulWallet>());
        GiantGolemBossSetup.Set(boss.GetComponent<EnemyTerritory>(), "_detectionRadius", 22f);
        GiantGolemBossSetup.Set(boss.GetComponent<EnemyTerritory>(), "_leashRadius", 28f);
        Vector3 approach = placed;
        var path = new NavMeshPath(); float best = float.PositiveInfinity; int reachable = 0;
        foreach (Vector3 c in candidates)
        {
            float distance = Vector3.Distance(c, placed); if (distance < 3) continue;
            if (!NavMesh.CalculatePath(placed, c, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            reachable++; float score = Vector3.Distance(c, placed + rotation * Vector3.forward * 8);
            if (score < best) { best = score; approach = c; }
        }
        if (reachable == 0) throw new InvalidOperationException("Boss 导航出生点没有可达活动区。");
        var point = new GameObject("GiantGolem_ApproachPoint"); point.transform.position = approach; point.transform.rotation = Quaternion.LookRotation(placed - approach, Vector3.up);
        var report = new PlacementReport { scene = ScenePath, sourceScene = sourcePath, navigation = NavPath, timestamp = DateTime.Now.ToString("O"), requested = Requested,
            placed = placed, rotation = rotation, navSize = surface.size, approach = approach, agentRadius = settings.agentRadius, agentHeight = settings.agentHeight,
            voxelSize = settings.voxelSize, sourceCount = sourceCount, checkedSamples = candidates.Count, reachableSamples = reachable, spawnOnNavMesh = true, bodyClear = true };
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Render(placed, "Docs/GiantGolem_AncientDungeon.png");
        EditorSceneManager.OpenScene(ScenePath);
        report.savedReloadVerified = Object.FindObjectsOfType<BossBrain>().Count(b => b.gameObject.scene == SceneManager.GetActiveScene()) == 1
            && NavMesh.SamplePosition(placed, out _, .4f, NavMesh.AllAreas);
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Selection.activeGameObject = Object.FindObjectOfType<BossBrain>().gameObject;
        SceneView.lastActiveSceneView?.LookAt(placed + Vector3.up * 2, Quaternion.Euler(25, 45, 0), 13);
        Debug.Log("[Golem] 地牢 Boss 关卡和局部导航已保存：" + placed.ToString("F3") + "，可达检查点=" + reachable);
    }
    static bool BodyClear(Vector3 point) => !Physics.CheckCapsule(point + Vector3.up * 1.02f, point + Vector3.up * 3.55f, .9f, 1, QueryTriggerInteraction.Ignore);
    static NavMeshData Bake(NavMeshSurface surface, out NavMeshBuildSettings settings, out int count)
    {
        var sources = (List<NavMeshBuildSource>)typeof(NavMeshSurface).GetMethod("CollectSources", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(surface, null);
        count = sources.Count; settings = surface.GetBuildSettings(); settings.agentRadius = .85f; settings.agentHeight = 4.4f; settings.agentClimb = .4f;
        settings.minRegionArea = 1; settings.overrideVoxelSize = true; settings.voxelSize = .12f;
        return NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(surface.center, surface.size), surface.transform.position, surface.transform.rotation);
    }
    [MenuItem("Tools/SoulsLike RPG/Giant Golem/9 Add Boss To Current Main Scene")]
    public static void AddToMainScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != MainScenePath) throw new InvalidOperationException("请在03主场景的 Edit Mode 接入 Boss。");
        if (Object.FindObjectsOfType<BossBrain>(true).Any(b => b.gameObject.scene == scene)) throw new InvalidOperationException("主场景已有 Boss，不覆盖现有配置。");
        const string navigation = "Assets/_Game/Navigation/NM_AncientDungeon_Checkpoint_GiantGolem.asset";
        if (File.Exists(navigation)) throw new InvalidOperationException("主场景 Boss 导航已存在，不覆盖现有数据。");
        // 保存用户当前场景内容后，再备份接入前的版本；不从旧场景覆盖主场景。
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("保存当前主场景失败。");
        Directory.CreateDirectory("Logs/GolemMainIntegration/Backup");
        File.Copy(MainScenePath, "Logs/GolemMainIntegration/Backup/BeforeAddingBoss.unity", true);
        Physics.SyncTransforms();
        var root = new GameObject("Navigation_GiantGolem"); root.transform.position = Requested;
        var surface = root.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Volume;
        surface.center = Vector3.up * 1.5f; surface.size = new Vector3(68, 18, 68); surface.layerMask = 1;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.overrideVoxelSize = true; surface.voxelSize = .12f;
        surface.overrideTileSize = true; surface.tileSize = 256;
        var data = Bake(surface, out var settings, out int count); if (data == null) throw new InvalidOperationException("主场景 Boss 导航烘焙失败。");
        data.name = Path.GetFileNameWithoutExtension(navigation); AssetDatabase.CreateAsset(data, navigation); surface.navMeshData = data; surface.AddData();
        var candidates = new List<Vector3>();
        for (int x = -10; x <= 10; x++) for (int z = -10; z <= 10; z++)
            if (NavMesh.SamplePosition(Requested + new Vector3(x * 1.25f, 0, z * 1.25f), out var hit, .45f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - Requested.y) < .5f && BodyClear(hit.position)) candidates.Add(hit.position);
        if (candidates.Count == 0) throw new InvalidOperationException("主场景该区域没有合格 Boss 出生点。");
        var old = JsonUtility.FromJson<PlacementReport>(File.ReadAllText(ReportPath));
        Vector3 placed = candidates.OrderBy(p => Vector3.Distance(p, old.placed)).First();
        var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GiantGolemBossSetup.PrefabPath), scene);
        boss.name = "GiantGolemBoss"; boss.transform.SetPositionAndRotation(placed, old.rotation);
        var player = Object.FindObjectsOfType<PlayerStateMachine>().Single(p => p.gameObject.scene == scene);
        GiantGolemBossSetup.Set(boss.GetComponent<BossBrain>(), "_targetOverride", player.transform);
        GiantGolemBossSetup.Set(boss.GetComponent<EnemyReward>(), "_wallet", player.GetComponent<SoulWallet>());
        GiantGolemBossSetup.Set(boss.GetComponent<EnemyTerritory>(), "_detectionRadius", 22f);
        GiantGolemBossSetup.Set(boss.GetComponent<EnemyTerritory>(), "_leashRadius", 28f);
        var path = new NavMeshPath();
        var reachable = candidates.Where(p => Vector3.Distance(p, placed) >= 3 && NavMesh.CalculatePath(placed, p, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete).ToArray();
        if (reachable.Length == 0) throw new InvalidOperationException("Boss 出生点没有可达活动区。");
        Vector3 approach = reachable.OrderBy(p => Vector3.Distance(p, old.approach)).First();
        var marker = new GameObject("GiantGolem_ApproachPoint"); marker.transform.SetPositionAndRotation(approach, Quaternion.LookRotation(placed - approach, Vector3.up));
        var report = new PlacementReport { scene = MainScenePath, sourceScene = MainScenePath, navigation = navigation, timestamp = DateTime.Now.ToString("O"), requested = Requested,
            placed = placed, rotation = old.rotation, navSize = surface.size, approach = approach, agentRadius = settings.agentRadius, agentHeight = settings.agentHeight,
            voxelSize = settings.voxelSize, sourceCount = count, checkedSamples = candidates.Count, reachableSamples = reachable.Length, spawnOnNavMesh = true, bodyClear = true };
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene(MainScenePath);
        report.savedReloadVerified = Object.FindObjectsOfType<BossBrain>().Length == 1 && NavMesh.SamplePosition(placed, out _, .4f, NavMesh.AllAreas);
        File.WriteAllText(MainReportPath, JsonUtility.ToJson(report, true));
        Selection.activeGameObject = Object.FindObjectOfType<BossBrain>().gameObject;
        SceneView.lastActiveSceneView?.LookAt(placed + Vector3.up * 2, Quaternion.Euler(25, 45, 0), 13);
        Debug.Log("[Golem] Boss 已接入当前03主场景，原有 HUD、赐福、玩家与入口导航保留。");
    }
    [MenuItem("Tools/SoulsLike RPG/Giant Golem/8 Rebake Dungeon Boss Navigation")]
    public static void Rebake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !IsDungeonScene(SceneManager.GetActiveScene().path)) throw new InvalidOperationException("请在地牢 Boss 场景的 Edit Mode 重烘导航。");
        var surface = Object.FindObjectsOfType<NavMeshSurface>().Single(s => s.name == "Navigation_GiantGolem");
        var data = Bake(surface, out _, out _); if (data == null) throw new InvalidOperationException("导航重烘失败。");
        data.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(surface.navMeshData));
        surface.RemoveData(); EditorUtility.CopySerialized(data, surface.navMeshData); Object.DestroyImmediate(data); surface.AddData();
        EditorUtility.SetDirty(surface.navMeshData); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[Golem] 已按半径0.85米、高4.4米重烘地牢 Boss 导航，请运行菜单7复测。");
    }
    public static void InspectMissing()
    {
        var scene = SceneManager.GetActiveScene(); var result = new List<string>();
        foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject); if (count == 0) continue;
            string path = t.name; for (var parent = t.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            result.Add(path + " missing=" + count + " prefab=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject));
        }
        File.WriteAllLines("Logs/GolemPlacement/MissingScripts.txt", result);
    }
    public static void Validate() { GiantGolemDungeonPlacementValidation.Run(); }
    public static void RemoveStaleCameraScripts()
    {
        var scene = SceneManager.GetActiveScene(); if (scene.path != ScenePath || EditorApplication.isPlaying) throw new InvalidOperationException("只修复新建 Boss 副本。");
        var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Environment Camera (view disabled)");
        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camera.gameObject);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/GolemPlacement/MissingScriptsRemoved.txt", camera.name + ": removed=" + count + "; source scene preserved");
    }
    public static void Cleanup()
    {
        SessionState.SetBool("Golem.Dungeon.FinalReload", true);
        AssetDatabase.DeleteAsset("Assets/_Game/Editor/GolemPlacementBridge.cs"); AssetDatabase.Refresh();
    }
}
