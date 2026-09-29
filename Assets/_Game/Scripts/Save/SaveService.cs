using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class SaveService
{
    private const string FileName = "checkpoint_save.json";

    public static string SaveFilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool Save(GameSaveData data)
    {
        return Save(data, SaveFilePath);
    }

    public static bool Save(GameSaveData data, string path)
    {
        if (!IsValid(data) || string.IsNullOrWhiteSpace(path))
            return false;

        string temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true), Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Checkpoint save failed: {exception.Message}");
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public static GameSaveData Load()
    {
        return Load(SaveFilePath);
    }

    public static GameSaveData Load(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            // 用非法哨兵区分缺失字段与合法的零余额；v1 在下面显式迁移。
            var data = new GameSaveData("", "")
            {
                version = 0, souls = -1, level = 0, vigor = 0, endurance = 0, strength = 0,
                hasSoulDrop = true, droppedSouls = -1,
                soulDropPosition = new Vector3(float.NaN, float.NaN, float.NaN)
            };
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path, Encoding.UTF8), data);
            if (data != null && data.version == 1 &&
                !string.IsNullOrWhiteSpace(data.sceneName) && !string.IsNullOrWhiteSpace(data.checkpointId))
            {
                // v1 只记录赐福；显式补齐成长数据，不依赖 JsonUtility 的字段默认行为。
                data.version = 2;
                data.souls = 1000;
                data.level = data.vigor = data.endurance = data.strength = 1;
            }
            if (data.version == 2)
            {
                data.version = GameSaveData.CurrentVersion;
                data.hasSoulDrop = false;
                data.droppedSouls = 0;
                data.soulDropPosition = Vector3.zero;
            }
            if (!IsValid(data))
            {
                Debug.LogWarning("Checkpoint save is incomplete or uses an unsupported version.");
                return null;
            }
            return data;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Checkpoint save could not be read: {exception.Message}");
            return null;
        }
    }

    private static bool IsValid(GameSaveData data)
    {
        // 空 checkpointId 表示使用默认出生点；v3 同时保存未取回的魂。
        return data != null && data.version == GameSaveData.CurrentVersion &&
            !string.IsNullOrWhiteSpace(data.sceneName) && data.souls >= 0 &&
            data.level >= 1 && data.vigor >= 1 && data.endurance >= 1 && data.strength >= 1 &&
            (data.hasSoulDrop ? data.droppedSouls > 0 : data.droppedSouls == 0) &&
            IsFinite(data.soulDropPosition.x) && IsFinite(data.soulDropPosition.y) &&
            IsFinite(data.soulDropPosition.z);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
