using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GroundSlamSelectionValidation
{
    [MenuItem("Tools/SoulsLike RPG/Giant Golem/Validate GroundSlam Selection")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请在 Edit Mode 验证 GroundSlam 选择。");
        }

        var checks = new List<string>();
        var skills = AssetDatabase.LoadAssetAtPath<GameObject>(GiantGolemBossSetup.PrefabPath).GetComponent<BossBrain>().Skills;
        var slams = skills.Where(s => s.Family == BossSkillFamily.GroundSlam).ToArray();
        Check(slams.Length == 3, "左右脚与 Jump 共三个 GroundSlam 变体", checks);
        var jump = slams.Single(s => s.Side == BossSkillSide.Any);
        var left = slams.Single(s => s.Side == BossSkillSide.Left);
        var right = slams.Single(s => s.Side == BossSkillSide.Right);
        Check(jump.Id == "attack_jumpAtk" && !jump.IsSidedGroundSlam && left.IsSidedGroundSlam && right.IsSidedGroundSlam,
            "Jump 与左右脚的动作语义保留", checks);
        Check(skills.All(s => Enum.IsDefined(typeof(BossSkillFamily), s.Family)), "全部技能的序列化 family 有效", checks);

        var target = new GameObject("GroundSlam_SelectionTarget");
        try
        {
            var selector = new BossActionSelector();
            var cfg = new BossSelectionSettings();
            var board = Snapshot(target.transform, 2.5f, 0);
            selector.Select(skills, board, cfg, 100, .5f, .5f, out _);
            Check(selector.Candidates.Where(c => c.Skill.IsSidedGroundSlam).All(c => c.Reason == "中央死区"),
                "正中仅排除左右脚", checks);
            Check(selector.Candidates.Single(c => c.Skill == jump).Weight > 0, "正中 Jump 仍可选择", checks);

            foreach (float side in new[] { -2f, 2f })
            {
                board = Snapshot(target.transform, 3, side);
                selector.Select(skills, board, cfg, 100, .5f, .5f, out _);
                Check(selector.Candidates.Single(c => c.Skill == (side < 0 ? left : right)).Weight > 0 &&
                    selector.Candidates.Single(c => c.Skill == (side < 0 ? right : left)).Reason == "脚侧",
                    "侧面只允许对应脚：" + side, checks);
                Check(selector.Candidates.Single(c => c.Skill == jump).Weight > 0, "侧面 Jump 仍可选择：" + side, checks);
                Check(Mathf.Abs(selector.Candidates.Where(c => c.Skill.Family == BossSkillFamily.GroundSlam).Sum(c => c.Weight) - 55) < .001f,
                    "GroundSlam 共享 55 类别预算：" + side, checks);
            }

            foreach (var used in slams)
            {
                board = Snapshot(target.transform, 3, -2);
                board.Commit(used, 100);
                selector.Select(skills, board, cfg, 100 + used.Cooldown * .5f, .5f, .5f, out _);
                Check(selector.Candidates.Where(c => c.Skill.Family == BossSkillFamily.GroundSlam).All(c => c.Reason == "冷却"),
                    "任一变体启动后全类别冷却：" + used.Id, checks);
                board.Finish(101, true);
                selector.Select(skills, board, cfg, 100 + used.Cooldown, .5f, .5f, out _);
                Check(Mathf.Abs(selector.Candidates.Where(c => c.Skill.Family == BossSkillFamily.GroundSlam).Sum(c => c.Weight) -
                    55 * cfg.previousPenalty * cfg.recentPenalty) < .001f,
                    "冷却边界恢复且共享历史惩罚：" + used.Id, checks);
                board.Reset();
                Check(board.ReadyAt(BossSkillFamily.GroundSlam) == 0 && board.RecentCount(BossSkillFamily.GroundSlam) == 0,
                    "重置清除整个类别冷却和历史：" + used.Id, checks);
            }

            board = Snapshot(target.transform, 3, -2);
            Property(board, "SideDwell", 4f);
            selector.Select(skills, board, cfg, 100, .5f, .5f, out _);
            Check(selector.Candidates.Single(c => c.Skill == left).Weight > selector.Candidates.Single(c => c.Skill == jump).Weight,
                "侧面停留加权仍针对对应脚动作", checks);

            foreach (var skill in skills)
            {
                float distance = skill.Family == BossSkillFamily.ThrowStone ? 18 : skill.MoveSpeed > 0 ? 6 :
                    skill.Family == BossSkillFamily.GroundSlam && !skill.IsSidedGroundSlam ? 5 : 2.2f;
                float side = skill.Side == BossSkillSide.Left ? -distance : skill.Side == BossSkillSide.Right ? distance : 0;
                board = Snapshot(target.transform, distance, side);
                selector.Select(skills, board, cfg, 100, 0, 0, out _);
                float probability = selector.Candidates.Where(c => c.Skill.Family == skill.Family).Sum(c => c.Probability);
                float earlierFamilies = selector.Candidates.Where(c => (int)c.Skill.Family < (int)skill.Family).Sum(c => c.Probability);
                float earlierVariants = selector.Candidates.TakeWhile(c => c.Skill != skill)
                    .Where(c => c.Skill.Family == skill.Family).Sum(c => c.Probability);
                float own = selector.Candidates.Single(c => c.Skill == skill).Probability;
                var chosen = selector.Select(skills, board, cfg, 100,
                    selector.RunProbability + earlierFamilies + probability * .5f,
                    probability > 0 ? (earlierVariants + own * .5f) / probability : 0, out bool run);
                Check(own > 0 && chosen == skill && !run, "两级选择仍覆盖技能：" + skill.Id, checks);
                Check(Mathf.Abs(selector.Candidates.Sum(c => c.Probability) + selector.RunProbability - 1) < .001f,
                    "最终概率归一：" + skill.Id, checks);
            }

            board = Snapshot(target.transform, 18, 0);
            selector.Select(skills, board, cfg, 100, 0, 0, out bool farRun);
            Check(farRun && Mathf.Abs(selector.RunProbability - .6f) < .001f,
                "远距快跑 60% 与投石 40%", checks);
            Property(board, "PathReachable", false);
            Property(board, "ThrowClear", false);
            Check(selector.Select(skills, board, cfg, 100, .5f, .5f, out farRun) == null && !farRun,
                "无合法候选正常回退", checks);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }

        Directory.CreateDirectory("Logs/GroundSlam");
        File.WriteAllLines("Logs/GroundSlam/Selection.txt", checks);
        if (checks.Any(c => c.StartsWith("FAIL")))
        {
            throw new InvalidOperationException("GroundSlam 验证失败，见 Logs/GroundSlam/Selection.txt。");
        }
        Debug.Log("[GroundSlam] Selection validation passed: " + checks.Count);
    }

    private static BossBlackboard Snapshot(Transform target, float distance, float side)
    {
        var board = new BossBlackboard();
        Property(board, "Target", target);
        Property(board, "Distance", distance);
        Property(board, "Side", side);
        Property(board, "Angle", 0f);
        Property(board, "MovementClear", true);
        Property(board, "ThrowClear", true);
        Property(board, "PathReachable", true);
        return board;
    }

    private static void Property(BossBlackboard board, string name, object value)
    {
        typeof(BossBlackboard).GetProperty(name).SetValue(board, value);
    }

    private static void Check(bool passed, string description, List<string> checks)
    {
        checks.Add((passed ? "PASS " : "FAIL ") + description);
    }
}
