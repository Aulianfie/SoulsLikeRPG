using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>每个 Boss 实例独立的感知与历史，不写入共享 SkillData。</summary>
[Serializable]
public sealed class BossBlackboard
{
    public Transform Target { get; internal set; }
    public float Distance { get; internal set; }
    public float Angle { get; internal set; }
    public float Side { get; internal set; }
    public bool MovementClear { get; internal set; }
    public bool ThrowClear { get; internal set; }
    public bool PathReachable { get; internal set; }
    public float SideDwell { get; internal set; }
    public float FarDwell { get; internal set; }
    public string ActiveNode { get; internal set; } = "Idle";
    public BossSkillData CurrentSkill { get; internal set; }
    public BossSkillData LastSkill { get; private set; }
    public BossSkillData LastCompletedSkill { get; private set; }
    public int SameSkillCount { get; private set; }
    public float NextDecisionTime { get; internal set; }
    readonly Dictionary<BossSkillFamily, float> readyTimes = new Dictionary<BossSkillFamily, float>();
    readonly Queue<BossSkillFamily> recent = new Queue<BossSkillFamily>();
    public float ReadyAt(BossSkillFamily family) => readyTimes.TryGetValue(family, out float value) ? value : 0;
    public int RecentCount(BossSkillFamily family) { int count = 0; foreach (var entry in recent) if (entry == family) count++; return count; }
    public void Commit(BossSkillData skill, float now) { CurrentSkill = skill; readyTimes[skill.Family] = now + skill.Cooldown; }
    public void Finish(float now, bool completed)
    {
        if (CurrentSkill == null) return;
        SameSkillCount = LastSkill == CurrentSkill ? SameSkillCount + 1 : 1;
        LastSkill = CurrentSkill;
        if (completed) LastCompletedSkill = CurrentSkill;
        recent.Enqueue(CurrentSkill.Family); if (recent.Count > 3) recent.Dequeue();
        NextDecisionTime = now + CurrentSkill.Recovery;
        CurrentSkill = null;
    }
    public void Reset()
    {
        readyTimes.Clear(); recent.Clear(); CurrentSkill = LastSkill = LastCompletedSkill = null;
        SameSkillCount = 0; SideDwell = FarDwell = 0; NextDecisionTime = 0; ActiveNode = "Idle";
    }
}
