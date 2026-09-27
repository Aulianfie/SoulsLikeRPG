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
        if (data == null || data.version != GameSaveData.CurrentVersion ||
            string.IsNullOrWhiteSpace(data.sceneName) || string.IsNullOrWhiteSpace(data.checkpointId))
            return false;

        string path = SaveFilePath;
        string temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
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
        string path = SaveFilePath;
        if (!File.Exists(path))
            return null;

        try
        {
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path, Encoding.UTF8));
            if (data == null || data.version != GameSaveData.CurrentVersion ||
                string.IsNullOrWhiteSpace(data.sceneName) || string.IsNullOrWhiteSpace(data.checkpointId))
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
}
