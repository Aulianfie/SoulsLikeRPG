/// <summary>现有小怪与 Boss 共用的世界恢复入口。</summary>
public interface ICheckpointResettable
{
    void ResetForCheckpoint();
}
