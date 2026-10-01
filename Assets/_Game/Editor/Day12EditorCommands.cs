using System;
using System.IO;
using UnityEditor;

// 沿用项目已有 request 文件入口；命令只在 Unity 主线程执行。
[InitializeOnLoad]
public static class Day12EditorCommands
{
    static Day12EditorCommands() => EditorApplication.update += Poll;
    private static void Poll()
    {
        const string request = "Temp/Day12.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
        string command;
        try { command = File.ReadAllText(request).Trim(); File.Delete(request); }
        catch (IOException) { return; }
        Directory.CreateDirectory("Logs/Day12");
        try
        {
            if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "inspect") BlessingUIValidation.Inspect();
            else if (command == "save")
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity")
                    throw new InvalidOperationException("只能保存 Edit Mode 的主场景。");
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            else if (command == "setup") Invoke("Day12Setup", "Build");
            else if (command == "mounts") Invoke("Day12Setup", "InspectMounts");
            else if (command == "align") Invoke("Day12Setup", "AlignMounts");
            else if (command == "preview") Invoke("Day12Preview", "Render");
            else if (command == "validate") Invoke("Day12Validation", "Begin");
            else if (command == "regression") Day11Validation.Begin("Actions");
            else if (command == "blessing") BlessingUIValidation.Run();
            else throw new ArgumentException("Unknown Day12 command: " + command);
            File.WriteAllText("Logs/Day12/CommandResult.txt", command + " dispatched");
        }
        catch (Exception e) { File.WriteAllText("Logs/Day12/CommandError.txt", e.ToString()); }
    }
    private static void Invoke(string typeName, string method)
    {
        Type type = typeof(Day12EditorCommands).Assembly.GetType(typeName);
        if (type == null) throw new InvalidOperationException(typeName + " 尚未导入。");
        type.GetMethod(method).Invoke(null, null);
    }
}
