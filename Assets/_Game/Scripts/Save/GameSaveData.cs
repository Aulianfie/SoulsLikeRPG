using System;

[Serializable]
public sealed class GameSaveData
{
    public const int CurrentVersion = 1;

    public int version;
    public string sceneName;
    public string checkpointId;

    public GameSaveData(string sceneName, string checkpointId)
    {
        version = CurrentVersion;
        this.sceneName = sceneName;
        this.checkpointId = checkpointId;
    }
}
