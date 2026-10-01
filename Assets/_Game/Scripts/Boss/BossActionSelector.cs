using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BossSelectionSettings
{
    [Min(.1f)] public float nearRange = 3.2f;
    [Min(.1f)] public float farRange = 11f;
    [Min(0)] public float sideDeadZone = .45f;
    [Range(0, 1)] public float previousPenalty = .35f;
    [Range(0, 1)] public float recentPenalty = .7f;
    [Min(0)] public float runWeight = 60;
    [Min(0)] public float throwWeight = 40;
    [Min(0)] public float dwellCap = 1;
}

public sealed class BossActionSelector
{
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
    readonly float[] familyWeights = new float[6];
    readonly int[] familyCounts = new int[6];

    public BossSkillData Select(IReadOnlyList<BossSkillData> skills, BossBlackboard board, BossSelectionSettings settings,
        float now, float familySample, float variantSample, out bool run)
    {
        Candidates.Clear(); Array.Clear(familyWeights, 0, 6); Array.Clear(familyCounts, 0, 6);
        RunProbability = 0;
        bool far = board.Distance >= settings.farRange;
        RunWeight = far && board.PathReachable ? settings.runWeight : 0;
        run = false;
        foreach (var skill in skills)
        {
            if (skill == null) continue;
            string reason = IneligibleReason(skill, board, settings, now);
            string factors = "";
            float weight = reason == null ? Mathf.Max(0, skill.BaseWeight) : 0;
            // 四个普通动作共享一个类别预算：先对有效动作数量归一，再做类别抽签。
            if (weight > 0)
            {
                if (far) weight *= settings.throwWeight;
                else if (board.Distance <= settings.nearRange)
                    weight *= skill.Family == BossSkillFamily.Ordinary ? 50 : skill.Family == BossSkillFamily.Stomp ? 55 : skill.Family == BossSkillFamily.Whirlwind ? 20 : 30;
                else weight *= skill.Family == BossSkillFamily.Dash ? 45 : skill.Family == BossSkillFamily.Whirlwind ? 35 : 20;
                if (board.LastSkill != null && board.LastSkill.Family == skill.Family)
                { weight *= settings.previousPenalty; factors += "；上类别×" + settings.previousPenalty.ToString("F2"); }
                int recent = board.RecentCount(skill.Family);
                weight *= Mathf.Pow(settings.recentPenalty, recent);
                if (recent > 0) factors += "；近期出现" + recent + "次";
                if (skill.Family == BossSkillFamily.Stomp) weight *= 1 + Mathf.Min(settings.dwellCap, board.SideDwell * .15f);
                if (skill.Family == BossSkillFamily.ThrowStone) weight *= 1 + Mathf.Min(settings.dwellCap, board.FarDwell * .08f);
                if (board.LastSkill != null && (board.LastSkill.Family == BossSkillFamily.Dash || board.LastSkill.Family == BossSkillFamily.Whirlwind) && skill.Family == BossSkillFamily.Ordinary) weight *= 1.2f;
                familyCounts[(int)skill.Family]++;
            }
            Candidates.Add(new Candidate { Skill = skill, Weight = weight, Reason = reason ?? "可用" + factors });
        }
        bool ordinaryAlternative = Candidates.Exists(c => c.Weight > 0 && c.Skill.Family == BossSkillFamily.Ordinary && c.Skill != board.LastSkill);
        foreach (var candidate in Candidates)
        {
            if (candidate.Weight <= 0) continue;
            int family = (int)candidate.Skill.Family;
            candidate.Weight /= familyCounts[family];
            familyWeights[family] += candidate.Weight;
        }
        float total = RunWeight; for (int i = 0; i < 6; i++) total += familyWeights[i];
        if (total <= 0) return null;
        // 类别概率保持不变；普通动作的重复惩罚仅调整该类别内的变体抽签。
        foreach (var c in Candidates)
            if (c.Skill.Family == BossSkillFamily.Ordinary && c.Skill == board.LastSkill && ordinaryAlternative)
            {
                c.Weight *= board.SameSkillCount >= 2 ? 0 : .35f;
                c.Reason += board.SameSkillCount >= 2 ? "；连续同动作排除" : "；上个动作×0.35";
            }
        for (int family = 0; family < 6; family++)
        {
            float variants = 0;
            foreach (var c in Candidates) if ((int)c.Skill.Family == family) variants += c.Weight;
            foreach (var c in Candidates) if ((int)c.Skill.Family == family && variants > 0)
                c.Probability = familyWeights[family] / total * c.Weight / variants;
        }
        RunProbability = RunWeight / total;
        float sample = Mathf.Clamp(familySample, 0, .999999f) * total;
        if (sample < RunWeight) { run = true; return null; }
        sample -= RunWeight;
        int chosenFamily = -1;
        for (int i = 0; i < 6; i++) { if (sample < familyWeights[i]) { chosenFamily = i; break; } sample -= familyWeights[i]; }
        float variantTotal = 0;
        foreach (var candidate in Candidates)
        {
            if ((int)candidate.Skill.Family != chosenFamily || candidate.Weight <= 0) continue;
            variantTotal += candidate.Weight;
        }
        float variant = Mathf.Clamp(variantSample, 0, .999999f) * variantTotal;
        foreach (var candidate in Candidates)
        {
            if ((int)candidate.Skill.Family != chosenFamily || candidate.Weight <= 0) continue;
            if (variant < candidate.Weight) return candidate.Skill;
            variant -= candidate.Weight;
        }
        return null;
    }

    public static string IneligibleReason(BossSkillData skill, BossBlackboard b, BossSelectionSettings s, float now)
    {
        if (b.Target == null) return "目标失效";
        if (now < b.ReadyAt(skill.Family)) return "冷却";
        if (b.Distance < skill.MinRange || b.Distance > skill.MaxRange) return "距离";
        if (b.Angle > skill.MaxAngle) return "夹角";
        if (b.Distance >= s.farRange && skill.Family != BossSkillFamily.ThrowStone) return "远距行动池";
        if (skill.Family == BossSkillFamily.Stomp)
        {
            if (Mathf.Abs(b.Side) <= s.sideDeadZone) return "中央死区";
            if (skill.Side == BossSkillSide.Left && b.Side >= 0 || skill.Side == BossSkillSide.Right && b.Side <= 0) return "脚侧";
        }
        if (skill.MoveSpeed > 0 && !b.MovementClear) return "移动受阻";
        if (skill.Family == BossSkillFamily.ThrowStone && !b.ThrowClear) return "投射受阻";
        if (skill.BaseWeight <= 0) return "权重为零";
        return null;
    }
}
