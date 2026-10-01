using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>每个 Boss 实例独立的感知与历史，不写入共享 SkillData。</summary>
[Serializable]
public sealed class BossBlackboard
{
    // Public properties
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

    // Runtime state
    private readonly Dictionary<BossSkillFamily, float> _familyReadyTimes = new Dictionary<BossSkillFamily, float>();
    private readonly Queue<BossSkillFamily> _recentSkillFamilies = new Queue<BossSkillFamily>();

    public float ReadyAt(BossSkillFamily family)
    {
        if (_familyReadyTimes.TryGetValue(family, out float value))
        {
            return value;
        }
        else
        {
            return 0;
        }
    }

    public int RecentCount(BossSkillFamily family)
    {
        int count = 0;
        foreach (var entry in _recentSkillFamilies)
        {
            if (entry == family)
            {
                count++;
            }
        }

        return count;
    }

    public void Commit(BossSkillData skill, float now)
    {
        CurrentSkill = skill;
        _familyReadyTimes[skill.Family] = now + skill.Cooldown;
    }

    public void Finish(float now, bool completed)
    {
        if (CurrentSkill == null)
        {
            return;
        }

        if (LastSkill == CurrentSkill)
        {
            SameSkillCount = SameSkillCount + 1;
        }
        else
        {
            SameSkillCount = 1;
        }

        LastSkill = CurrentSkill;
        if (completed)
        {
            LastCompletedSkill = CurrentSkill;
        }

        _recentSkillFamilies.Enqueue(CurrentSkill.Family);
        if (_recentSkillFamilies.Count > 3)
        {
            _recentSkillFamilies.Dequeue();
        }

        NextDecisionTime = now + CurrentSkill.Recovery;
        CurrentSkill = null;
    }

    public void Reset()
    {
        _familyReadyTimes.Clear();
        _recentSkillFamilies.Clear();
        LastCompletedSkill = null;
        LastSkill = null;
        CurrentSkill = null;
        SameSkillCount = 0;
        FarDwell = 0;
        SideDwell = 0;
        NextDecisionTime = 0;
        ActiveNode = "Idle";
    }
}
