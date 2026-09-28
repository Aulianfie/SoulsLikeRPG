using System;

[Serializable]
public sealed class GameSaveData
{
    public const int CurrentVersion = 2;

    public int version;
    public string sceneName;
    public string checkpointId;
    public int souls = 1000;
    public int level = 1;
    public int vigor = 1;
    public int endurance = 1;
    public int strength = 1;

    public GameSaveData(string sceneName, string checkpointId)
    {
        version = CurrentVersion;
        this.sceneName = sceneName;
        this.checkpointId = checkpointId;
    }
}
