using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BossActionSelector 负责在 Boss 的技能池中选择一个技能，考虑距离、角度、冷却、历史使用等因素。
/// </summary>
[Serializable]
public sealed class BossSelectionSettings
{
    [Min(.1f)]
    public float nearRange = 3.2f;
    [Min(.1f)]
    public float farRange = 11f;
    [Min(0)]
    public float sideDeadZone = .45f;
    [Range(0, 1)]
    public float previousPenalty = .35f;
    [Range(0, 1)]
    public float recentPenalty = .7f;
    [Min(0)]
    public float runWeight = 60;
    [Min(0)]
    public float throwWeight = 40;
    [Min(0)]
    public float dwellCap = 1;
}

public sealed class BossActionSelector
{
    private const int SkillFamilyCount = 6;

    public sealed class Candidate
    {
        public BossSkillData Skill;
        public float Weight;
        public float Probability;
        public string Reason;
    }

    public List<Candidate> Candidates { get; } = new List<Candidate>();
    public float RunWeight { get; private set; }
    public float RunProbability { get; private set; }

    private readonly float[] _familyWeights = new float[SkillFamilyCount];
    private readonly int[] _familyCounts = new int[SkillFamilyCount];

    public BossSkillData Select(IReadOnlyList<BossSkillData> skills, BossBlackboard board, BossSelectionSettings settings, float now, float familySample, float variantSample, out bool run)
    {
        Candidates.Clear();
        Array.Clear(_familyWeights, 0, SkillFamilyCount);
        Array.Clear(_familyCounts, 0, SkillFamilyCount);
        RunProbability = 0;
        bool far = board.Distance >= settings.farRange;
        if (far &&
            board.PathReachable)
        {
            RunWeight = settings.runWeight;
        }
        else
        {
            RunWeight = 0;
        }

        run = false;
        foreach (var skill in skills)
        {
            if (skill == null)
            {
                continue;
            }

            string reason = IneligibleReason(skill, board, settings, now);
            string factors = "";
            float weight;
            if (reason == null)
            {
                weight = Mathf.Max(0, skill.BaseWeight);
            }
            else
            {
                weight = 0;
            }

            // 四个普通动作共享一个类别预算：先对有效动作数量归一，再做类别抽签。
            if (weight > 0)
            {
                if (far)
                {
                    weight *= settings.throwWeight;
                }
                else if (board.Distance <= settings.nearRange)
                {
                    if (skill.Family == BossSkillFamily.Ordinary)
                    {
                        weight *= 50;
                    }
                    else if (skill.Family == BossSkillFamily.Stomp)
                    {
                        weight *= 55;
                    }
                    else if (skill.Family == BossSkillFamily.Whirlwind)
                    {
                        weight *= 20;
                    }
                    else
                    {
                        weight *= 30;
                    }
                }
                else if (skill.Family == BossSkillFamily.Dash)
                {
                    weight *= 45;
                }
                else if (skill.Family == BossSkillFamily.Whirlwind)
                {
                    weight *= 35;
                }
                else
                {
                    weight *= 20;
                }

                if (board.LastSkill != null &&
                    board.LastSkill.Family == skill.Family)
                {
                    weight *= settings.previousPenalty;
                    factors += "；上类别×" + settings.previousPenalty.ToString("F2");
                }

                int recent = board.RecentCount(skill.Family);
                weight *= Mathf.Pow(settings.recentPenalty, recent);
                if (recent > 0)
                {
                    factors += "；近期出现" + recent + "次";
                }
                
                // SideDwell 表示玩家在 Boss 某一侧待了多久，FarDwell 表示玩家在远距离区域待了多久。
                // 对于 踩地 和 投石 技能，玩家在该区域停留的时间越长，权重越高。
                if (skill.Family == BossSkillFamily.Stomp)
                {
                    weight *= 1 + Mathf.Min(settings.dwellCap, board.SideDwell * .15f);
                }

                if (skill.Family == BossSkillFamily.ThrowStone)
                {
                    weight *= 1 + Mathf.Min(settings.dwellCap, board.FarDwell * .08f);
                }
                
                // 连续使用 Dash 或 Whirlwind 后，普通攻击的权重会增加。
                if (board.LastSkill != null &&
                    (board.LastSkill.Family == BossSkillFamily.Dash ||
                    board.LastSkill.Family == BossSkillFamily.Whirlwind) &&
                    skill.Family == BossSkillFamily.Ordinary)
                {
                    weight *= 1.2f;
                }

                _familyCounts[(int)skill.Family]++;
            }

            Candidates.Add(new Candidate
                {
                    Skill = skill,
                    Weight = weight,
                    Reason = reason ?? "可用" + factors
                });
        }
        // ordinaryAlternative 假设上一个技能是某一个普通攻击，且当前有其他普通攻击可选，则 ordinaryAlternative 为 true。
        bool ordinaryAlternative = Candidates.Exists(c => c.Weight > 0 &&
                c.Skill.Family == BossSkillFamily.Ordinary &&
                c.Skill != board.LastSkill);
        foreach (var candidate in Candidates)
        {
            if (candidate.Weight <= 0)
            {
                continue;
            }

            // 每一类技能都求一个平均权重，防止普通攻击因为技能数量多，概率天然变成踩地的四倍
            int family = (int)candidate.Skill.Family;
            candidate.Weight /= _familyCounts[family];
            _familyWeights[family] += candidate.Weight;
        }

        float total = RunWeight; // TODO 这里我有点疑问？RunWeight 也算在总权重里，方便计算 RunProbability
        for (int i = 0; i < SkillFamilyCount; i++)
        {
            total += _familyWeights[i];
        }

        if (total <= 0)
        {
            return null;
        }

        // 其余类别普通攻击概率保持不变，仅针对上次使用的普通攻击做权重衰减，避免连续使用同一个普通攻击。
        foreach (var c in Candidates)
        {
            if (c.Skill.Family == BossSkillFamily.Ordinary &&
                c.Skill == board.LastSkill &&
                ordinaryAlternative)
            {
                c.Weight *= board.SameSkillCount >= 2 ? 0 : .35f;
                c.Reason += board.SameSkillCount >= 2 ? "；连续同动作排除" : "；上个动作×0.35";
            }
        }

        // 计算类别概率和变体概率
        for (int family = 0; family < SkillFamilyCount; family++)
        {
            float variants = 0;
            foreach (var c in Candidates)
            {
                if ((int)c.Skill.Family == family)
                {
                    variants += c.Weight;
                }
            }

            foreach (var c in Candidates)
            {
                if ((int)c.Skill.Family == family &&
                    variants > 0)
                {
                    c.Probability = _familyWeights[family] / total * c.Weight / variants;
                }
            }
        }

        RunProbability = RunWeight / total;
        float sample = Mathf.Clamp(familySample, 0, .999999f) * total;
        if (sample < RunWeight)
        {
            run = true;
            return null;
        }
        // 先选一个SkillFamily
        sample -= RunWeight;
        int chosenFamily = -1;
        for (int i = 0; i < SkillFamilyCount; i++)
        {
            if (sample < _familyWeights[i])
            {
                chosenFamily = i;
                break;
            }

            sample -= _familyWeights[i];
        }
        // 再在该SkillFamily中选一个具体的技能
        float variantTotal = 0;
        foreach (var candidate in Candidates)
        {
            if ((int)candidate.Skill.Family != chosenFamily ||
                candidate.Weight <= 0)
            {
                continue;
            }

            variantTotal += candidate.Weight;
        }

        float variant = Mathf.Clamp(variantSample, 0, .999999f) * variantTotal;
        foreach (var candidate in Candidates)
        {
            if ((int)candidate.Skill.Family != chosenFamily ||
                candidate.Weight <= 0)
            {
                continue;
            }

            if (variant < candidate.Weight)
            {
                return candidate.Skill;
            }

            variant -= candidate.Weight;
        }

        return null;
    }
    /// <summary>
    /// 获取指定技能在当前状态下不适用的原因，如果返回 null 则表示技能可用。
    /// 有任何string返回值，表示技能不可用，返回值为原因描述。
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="b"></param>
    /// <param name="s"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static string IneligibleReason(BossSkillData skill, BossBlackboard b, BossSelectionSettings s, float now)
    {
        if (b.Target == null)
        {
            return "目标失效";
        }

        if (now < b.ReadyAt(skill.Family))
        {
            return "冷却";
        }

        if (b.Distance < skill.MinRange ||
            b.Distance > skill.MaxRange)
        {
            return "距离";
        }

        if (b.Angle > skill.MaxAngle)
        {
            return "夹角";
        }

        if (b.Distance >= s.farRange &&
            skill.Family != BossSkillFamily.ThrowStone)
        {
            return "远距行动池";
        }

        if (skill.Family == BossSkillFamily.Stomp)
        {
            if (Mathf.Abs(b.Side) <= s.sideDeadZone)
            {
                return "中央死区";
            }

            if (skill.Side == BossSkillSide.Left &&
                b.Side >= 0 ||
                skill.Side == BossSkillSide.Right &&
                b.Side <= 0)
            {
                return "脚侧";
            }
        }

        if (skill.MoveSpeed > 0 &&
            !b.MovementClear)
        {
            return "移动受阻";
        }

        if (skill.Family == BossSkillFamily.ThrowStone &&
            !b.ThrowClear)
        {
            return "投射受阻";
        }

        if (skill.BaseWeight <= 0)
        {
            return "权重为零";
        }

        return null;
    }
}
