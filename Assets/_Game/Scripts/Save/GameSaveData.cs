using System;
using UnityEngine;

[Serializable]
public sealed class GameSaveData
{
    public const int CurrentVersion = 5;

    public int version;
    public string sceneName;
    public string checkpointId;
    public int souls = 1000;
    public int level = 1;
    public int vigor = 1;
    public int endurance = 1;
    public int strength = 1;
    public bool hasSoulDrop;
    public int droppedSouls;
    public Vector3 soulDropPosition;
    public int flaskCharges = -1; // v4 迁移字段，v5 同步镜像 HP 数量
    public int hpFlaskCharges = -1;
    public int mpFlaskCharges = -1;
    public int currentQuickItemSlot;

    public GameSaveData(string sceneName, string checkpointId)
    {
        version = CurrentVersion;
        this.sceneName = sceneName;
        this.checkpointId = checkpointId;
    }
}
