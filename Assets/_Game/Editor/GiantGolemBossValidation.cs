using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// 复用项目现有的编辑器协程验收方式；测试对象均为新场景中的真实组件。
[InitializeOnLoad]
public static class GiantGolemBossValidation
{
    private const string Running = "Golem.Validation.Running";
    private const string ReportPath = "Docs/GiantGolem_BossAI_Validation.json";

    [Serializable]
    public class Check
    {
        public string name;
        public string detail;
        public bool passed;
    }

    [Serializable]
    public class Report
    {
        public string timestamp;
        public string scene;
        public int passed;
        public int failed;
        public int runtimeErrors;
        public int runtimeWarnings;
        public Check[] checks;
        public string[] manualChecks;
    }

    [Serializable]
    public class Handoff
    {
        public string timestamp;
        public string scene;
        public int errors;
        public int warnings;
        public bool playing;
        public bool dirty;
        public bool temporaryBridgeRemoved;
    }

    private static readonly List<Check> checks = new List<Check>();
    private static IEnumerator routine;
    private static Func<bool> waiting;
    private static double next;
    private static double deadline;
    private static double waitDeadline;
    private static int errors;
    private static int warnings;
    private static BossBrain boss;
    private static PlayerStateMachine player;
    private static EnemyStateMachine small;
    private static readonly List<string> samples = new List<string>();

    static GiantGolemBossValidation()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += StateChanged;
        Application.logMessageReceived += Log;
        if (SessionState.GetBool("Golem.Validation.FinalReload", false))
        {
            EditorApplication.delayCall += FinalSnapshot;
        }
    }

    [MenuItem("Tools/SoulsLike RPG/Giant Golem/4 Validate Boss AI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("在已保存场景的 Edit Mode 执行验证。");
        }

        checks.Clear();
        warnings = 0;
        errors = 0;
        EditorSceneManager.OpenScene(GiantGolemBossSetup.ScenePath);
        EditChecks();
        WriteReport();
        SessionState.SetBool(Running, true);
        SessionState.SetString(Running + ".Mode", "All");
        SessionState.SetInt(Running + ".Errors", 0);
        SessionState.SetInt(Running + ".Warnings", 0);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/SoulsLike RPG/Giant Golem/5 Validate Integration")]
    public static void RunIntegration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().isDirty ||
            EditorSceneManager.GetActiveScene().path != GiantGolemBossSetup.ScenePath)
        {
            throw new InvalidOperationException("在已保存 Boss 场景 Edit Mode 执行。");
        }

        ReadReport();
        SessionState.SetBool(Running, true);
        SessionState.SetString(Running + ".Mode", "Integration");
        SessionState.SetInt(Running + ".Errors", 0);
        SessionState.SetInt(Running + ".Warnings", 0);
        EditorApplication.isPlaying = true;
    }

    public static void VerifySavedAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("先退出 Play Mode 并保存场景。");
        }

        ReadReport();
        var scene = EditorSceneManager.OpenScene(GiantGolemBossSetup.ScenePath);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        Test(
            all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
            "保存重载无 Missing Script",
            "实际重新打开测试场景"
        );
        var brain = Object.FindObjectOfType<BossBrain>();
        Test(
            brain != null &&
                PrefabUtility.IsPartOfPrefabInstance(brain) &&
                brain.Skills.Length == 10 &&
                brain.Skills.All(s => s != null),
            "保存重载 Boss 配置",
            "场景 prefab 实例、十个配置引用"
        );
        Test(
            NavMesh.SamplePosition(brain.transform.position, out _, 1, NavMesh.AllAreas),
            "保存重载 NavMesh 数据",
            "导航数据资产引用有效"
        );
        foreach (var respawn in Object.FindObjectsOfType<RespawnController>())
        {
            var s = new SerializedObject(respawn);
            Test(
                new[] { "_player", "_checkpointManager", "_screenFader", "_defaultSpawn" }.All(f => s.FindProperty(f).objectReferenceValue != null),
                "保存重载死亡重生引用",
                "玩家、管理器、淡入淡出和默认出生点"
            );
        }

        Test(
            all.SelectMany(t => t.GetComponents<Renderer>()).SelectMany(r => r.sharedMaterials).Where(m => m != null).All(m => m.shader != null &&
                    m.shader.name != "Hidden/InternalErrorShader"),
            "保存重载材质 Shader",
            "实际材质引用没有错误 Shader"
        );
        Selection.activeGameObject = brain.gameObject;
        WriteReport();
    }

    public static void VerifySelectionCoverage()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("在 Boss 场景 Edit Mode 执行。");
        }

        ReadReport();
        var skills = AssetDatabase.LoadAssetAtPath<GameObject>(GiantGolemBossSetup.PrefabPath).GetComponent<BossBrain>().Skills;
        foreach (var skill in skills)
        {
            float distance;
            if (skill.Family == BossSkillFamily.ThrowStone)
            {
                distance = 18;
            }
            else if (skill.MoveSpeed > 0)
            {
                distance = 6;
            }
            else if (skill.Family == BossSkillFamily.Jump)
            {
                distance = 5;
            }
            else
            {
                distance = 2.2f;
            }

            float side;
            if (skill.Side == BossSkillSide.Left)
            {
                side = -2;
            }
            else if (skill.Side == BossSkillSide.Right)
            {
                side = 2;
            }
            else
            {
                side = 0;
            }

            var b = Snapshot(distance, side);
            var selector = new BossActionSelector();
            var cfg = new BossSelectionSettings();
            selector.Select(skills, b, cfg, 100, 0, 0, out _);
            float familyProbability = selector.Candidates.Where(c => c.Skill.Family == skill.Family).Sum(c => c.Probability);
            float earlierFamilies = selector.Candidates.Where(c => (int)c.Skill.Family < (int)skill.Family).Sum(c => c.Probability);
            float earlierVariants = 0;
            foreach (var c in selector.Candidates)
            {
                if (c.Skill == skill)
                {
                    break;
                }

                if (c.Skill.Family == skill.Family)
                {
                    earlierVariants += c.Probability;
                }
            }

            float own = selector.Candidates.First(c => c.Skill == skill).Probability;
            var chosen = selector.Select(
                skills,
                b,
                cfg,
                100,
                selector.RunProbability + earlierFamilies + familyProbability * .5f,
                familyProbability > 0 ? (earlierVariants + own * .5f) / familyProbability : 0,
                out bool run
            );
            Test(own > 0 &&
                    chosen == skill &&
                    !run, "自动选招区间覆盖 " + skill.Id, "真实配置、合法位置、确定样本能选到此技能");
        }
    }

    public static void FinishHandoff()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("先退出 Play Mode 并保存场景。");
        }

        SessionState.SetBool("Golem.Validation.FinalReload", true);
        AssetDatabase.DeleteAsset("Assets/_Game/Editor/GiantGolemBossCommands.cs");
        AssetDatabase.Refresh();
    }

    private static void FinalSnapshot()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += FinalSnapshot;
            return;
        }

        var counts = new object[]
        {
            0,
            0,
            0
        };
        typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, counts);
        var scene = EditorSceneManager.GetActiveScene();
        var data = new Handoff
        {
            timestamp = DateTime.Now.ToString("O"),
            scene = scene.path,
            errors = (int)counts[0],
            warnings = (int)counts[1],
            playing = EditorApplication.isPlaying,
            dirty = scene.isDirty,
            temporaryBridgeRemoved = !File.Exists("Assets/_Game/Editor/GiantGolemBossCommands.cs")
        };
        File.WriteAllText("Logs/GiantGolem/FinalEditorState.json", JsonUtility.ToJson(data, true));
        ReadReport();
        Test(
            data.errors == 0 &&
                data.warnings == 0 &&
                data.temporaryBridgeRemoved &&
                !data.playing &&
                !data.dirty,
            "交付清理后编译与 Console",
            "临时入口已删除；重新编译后0错误/0警告；场景已保存"
        );
        SessionState.SetBool("Golem.Validation.FinalReload", false);
    }

    private static void EditChecks()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GiantGolemBossSetup.PrefabPath);
        var brain = prefab.GetComponent<BossBrain>();
        var anim = prefab.GetComponentInChildren<Animator>();
        Test(
            brain != null &&
                prefab.GetComponent<EnemyStateMachine>() == null,
            "独立 Boss 控制器",
            "Boss 不挂小怪 FSM"
        );
        Test(brain.Skills.Length == 10 &&
                brain.Skills.All(s => s != null), "十个技能接线", "四普通、两踩踏、冲刺、旋转、跳攻、投石");
        Test(
            anim.avatar != null &&
                anim.avatar.isHuman &&
                anim.avatar.isValid &&
                !anim.applyRootMotion,
            "Humanoid Avatar 与移动权威",
            "Avatar 有效；Root Motion 关闭"
        );
        var controller = (AnimatorController)anim.runtimeAnimatorController;
        Test(controller.layers[0].stateMachine.states.Length == 13, "Animator 状态接线", "10 攻击 + Idle/Chase/Dead");
        Test(
            controller.animationClips.Where(c => c.name.StartsWith("attack") ||
                    c.name.StartsWith("dead")).All(c => !AnimationUtility.GetAnimationClipSettings(c).loopTime),
            "攻击与死亡单次播放",
            "仅 Idle 和 Run 循环"
        );
        Test(
            brain.Skills.All(s => s.HitStart <= s.HitEnd &&
                    s.DirectionLock <= s.HitStart &&
                    s.HitEnd <= s.Completion),
            "技能时序顺序",
            "锁定 <= 出手 <= 结束"
        );
        foreach (var field in new[]
        {
            "_leftHand",
            "_rightHand",
            "_leftFoot",
            "_rightFoot",
            "_throwSocket",
            "_rockPrefab"
        }
        )
        {
            Test(
                new SerializedObject(prefab.GetComponent<BossSkillRunner>()).FindProperty(field).objectReferenceValue != null,
                "执行器引用 " + field,
                "预制体已落盘"
            );
        }

        Test(
            Object.FindObjectsOfType<CheckpointManager>().All(c => !new SerializedObject(c).FindProperty("_persistProgression").boolValue),
            "测试场景存档隔离",
            "正式进度不读取、不写入"
        );
        SelectorChecks(brain.Skills);
        bool stop = false;
        int aborted = 0;
        var tree = new BTSelector(
            "root",
            new BTSequence(
                "priority",
                new BTCondition("condition", () => stop),
                new BTAction("death", _ => BTStatus.Running)
            ),
            new BTAction("skill", _ => BTStatus.Running, () => aborted++)
        );
        tree.Tick(.1f);
        stop = true;
        tree.Tick(.1f);
        tree.Tick(.1f);
        Test(aborted == 1, "行为树优先级中断", "切换高优先级分支时 Running 节点只 Abort 一次");
    }

    private static BossBlackboard Snapshot(float distance, float side, float angle = 0)
    {
        var b = new BossBlackboard();
        var target = Object.FindObjectOfType<PlayerStateMachine>().transform;
        Property(b, "Target", target);
        Property(b, "Distance", distance);
        Property(b, "Side", side);
        Property(b, "Angle", angle);
        Property(b, "MovementClear", true);
        Property(b, "ThrowClear", true);
        Property(b, "PathReachable", true);
        return b;
    }

    private static void SelectorChecks(BossSkillData[] skills)
    {
        var s = new BossActionSelector();
        var cfg = new BossSelectionSettings();
        var left = Snapshot(3, -2, 90);
        s.Select(skills, left, cfg, 100, .4f, .4f, out _);
        Test(
            s.Candidates.First(c => c.Skill.Side == BossSkillSide.Left).Weight > 0 &&
                s.Candidates.First(c => c.Skill.Side == BossSkillSide.Right).Reason == "脚侧",
            "左侧只允许左脚",
            "Boss 根坐标为准"
        );
        var right = Snapshot(3, 2, 90);
        s.Select(skills, right, cfg, 100, .4f, .4f, out _);
        Test(
            s.Candidates.First(c => c.Skill.Side == BossSkillSide.Right).Weight > 0 &&
                s.Candidates.First(c => c.Skill.Side == BossSkillSide.Left).Weight == 0,
            "右侧只允许右脚",
            "前摇不会改脚"
        );
        var center = Snapshot(2.5f, 0);
        s.Select(skills, center, cfg, 100, .1f, .1f, out _);
        Test(
            s.Candidates.Where(c => c.Skill.Family == BossSkillFamily.Stomp).All(c => c.Reason == "中央死区"),
            "正中死区",
            "避免左右脚抖动"
        );
        Test(
            Mathf.Abs(s.Candidates.Where(c => c.Skill.Family == BossSkillFamily.Ordinary).Sum(c => c.Weight) - 50) < .001f,
            "普通类别权重不被四招放大",
            "类别总权重=50"
        );
        var far = Snapshot(18, 0);
        s.Select(skills, far, cfg, 100, 0, 0, out bool run);
        Test(run &&
                Mathf.Abs(s.RunProbability - .6f) < .001f, "远距快跑/投石比例", "初始快跑60%，投石40%");
        Test(
            s.Select(skills, far, cfg, 100, .9f, 0, out run).Family == BossSkillFamily.ThrowStone &&
                !run,
            "远距选石头",
            "同一输入与样本可复现"
        );
        Property(far, "ThrowClear", false);
        s.Select(skills, far, cfg, 100, .9f, 0, out run);
        Test(
            run &&
                s.Candidates.First(c => c.Skill.Family == BossSkillFamily.ThrowStone).Weight == 0,
            "投射受阻只接近",
            "障碍先于权重"
        );
        center.Commit(skills[0], 100);
        center.Finish(103, true);
        s.Select(skills, center, cfg, 101, .1f, .1f, out _);
        Test(
            s.Candidates.Where(c => c.Skill.Family == BossSkillFamily.Ordinary).All(c => c.Reason == "冷却"),
            "普通四招共享类别冷却",
            "提交时记冷却"
        );
        s.Select(skills, center, cfg, 110, .1f, .1f, out _);
        Test(
            s.Candidates.Where(c => c.Skill.Family == BossSkillFamily.Ordinary).Sum(c => c.Weight) < 50 * .35f,
            "上一类别与近期历史惩罚",
            "上一类别 ×0.35、最近每次 ×0.7"
        );
        center.Commit(skills[0], 111);
        center.Finish(114, true);
        s.Select(skills, center, cfg, 120, .1f, .1f, out _);
        Test(s.Candidates.First(c => c.Skill == skills[0]).Weight == 0, "连续两次同动作暂时排除", "存在替代普通动作时生效");
        Test(
            Mathf.Abs(s.Candidates.Sum(c => c.Probability) + s.RunProbability - 1) < .001f,
            "有效候选概率归一",
            "两级选招的最终概率总和=1"
        );
        center.Reset();
        Test(
            center.LastSkill == null &&
                center.ReadyAt(BossSkillFamily.Ordinary) == 0,
            "历史冷却重置",
            "运行状态位于每实例 Blackboard"
        );
        var none = Snapshot(50, 0);
        Property(none, "PathReachable", false);
        Test(s.Select(skills, none, cfg, 100, .5f, .5f, out run) == null &&
                !run, "无合法候选回退", "不绕过距离/路径条件");
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false))
        {
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            ReadReport();
            samples.Clear();
            samples.Add("skill,hpLoss,travel,history,damageCount");
            waiting = null;
            next = EditorApplication.timeSinceStartup + .5;
            deadline = next + 240;
            if (SessionState.GetString(Running + ".Mode", "All") == "Integration")
            {
                routine = Integration();
            }
            else
            {
                routine = Scenarios();
            }

            errors = SessionState.GetInt(Running + ".Errors", 0);
            warnings = SessionState.GetInt(Running + ".Warnings", 0);
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            ReadReport();
            errors = SessionState.GetInt(Running + ".Errors", 0);
            warnings = SessionState.GetInt(Running + ".Warnings", 0);
            Test(errors == 0, "Play Mode 无运行错误", "errors=" + errors + "; warnings=" + warnings);
            WriteReport();
            SessionState.SetBool(Running, false);
            Debug.Log("[Golem] Validation " + (checks.All(c => c.passed) ? "PASS" : "FAIL") + " checks=" + checks.Count);
        }
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Running, false) ||
            !EditorApplication.isPlaying ||
            routine == null ||
            EditorApplication.timeSinceStartup < next)
        {
            return;
        }

        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                throw new TimeoutException("Boss 验证总超时");
            }

            if (waiting != null)
            {
                if (EditorApplication.timeSinceStartup > waitDeadline)
                {
                    throw new TimeoutException("等待超时，最后检查：" + checks.LastOrDefault()?.name);
                }

                if (!waiting())
                {
                    return;
                }

                waiting = null;
            }

            if (!routine.MoveNext())
            {
                routine = null;
                WriteReport();
                EditorApplication.isPlaying = false;
                return;
            }

            if (routine.Current is Func<bool> f)
            {
                waiting = f;
                waitDeadline = EditorApplication.timeSinceStartup + 12;
            }
            else if (routine.Current is float delay)
            {
                next = EditorApplication.timeSinceStartup + delay;
            }
        }
        catch (Exception e)
        {
            Test(false, "验证执行异常", e.ToString());
            WriteReport();
            routine = null;
            Time.timeScale = 1;
            EditorApplication.isPlaying = false;
        }
    }

    private static IEnumerator Scenarios()
    {
        boss = Object.FindObjectOfType<BossBrain>();
        player = Object.FindObjectOfType<PlayerStateMachine>();
        small = Object.FindObjectOfType<EnemyStateMachine>();
        boss.SetAutomatic(false);
        small.enabled = false;
        small.GetComponent<EnemyMotor>().Stop();
        player.InputReader.enabled = false;
        player.Health.SetMaxHealth(1000);
        player.Health.ReviveFull();
        player.Respawn();
        Time.timeScale = 1;
        yield return .2f;
        Test(
            boss.GetComponent<NavMeshAgent>().isOnNavMesh &&
                small.GetComponent<NavMeshAgent>().isOnNavMesh,
            "Boss 与小怪 NavMesh 出生点",
            "真实 Play Mode Agent"
        );
        Test(boss.HasValidTarget, "Boss 目标解析", "复用场景玩家");
        foreach (var skill in boss.Skills)
        {
            boss.ResetForCheckpoint();
            boss.SetAutomatic(false);
            float distance;
            if (skill.Family == BossSkillFamily.ThrowStone)
            {
                distance = 14;
            }
            else if (skill.MoveSpeed > 0)
            {
                distance = 6;
            }
            else if (skill.Family == BossSkillFamily.Jump)
            {
                distance = 5;
            }
            else
            {
                distance = 2.2f;
            }

            Vector3 offset;
            if (skill.Side == BossSkillSide.Left)
            {
                offset = -boss.transform.right * distance;
            }
            else if (skill.Side == BossSkillSide.Right)
            {
                offset = boss.transform.right * distance;
            }
            else
            {
                offset = boss.transform.forward * distance;
            }

            Place(boss.transform.position + offset);
            yield return .2f;
            int before = player.Health.CurrentHealth;
            Vector3 start = boss.transform.position;
            int decisions = boss.DecisionCount;
            Test(boss.TryForceSkill(skill.Id), "真实技能启动 " + skill.Id, "状态存在并提交技能");
            yield return new Func<bool>(() => !boss.Runner.IsRunning);
            yield return .25f;
            int loss = before - player.Health.CurrentHealth;
            float travel = Vector3.Distance(start, boss.transform.position);
            samples.Add(skill.Id + "," + loss + "," + travel + "," + (boss.Blackboard.LastCompletedSkill != null ? boss.Blackboard.LastCompletedSkill.Id : "none") + "," + boss.GetComponent<BossDamageArea>().LastDamageCount);
            Test(boss.Blackboard.LastCompletedSkill == skill, "真实技能完成 " + skill.Id, "normalizedTime 到达结束，历史正确");
            Test(boss.DecisionCount == decisions, "执行中不重新抽签 " + skill.Id, "强制检查期间自动 AI 关闭");
            if (skill.Family != BossSkillFamily.ThrowStone)
            {
                Test(loss == skill.Damage, "真实技能命中一次 " + skill.Id, "HP loss=" + loss + "; expected=" + skill.Damage);
            }
            else
            {
                Test(
                    loss == skill.Damage &&
                        boss.Runner.LiveProjectileCount == 0,
                    "投石真实命中与销毁",
                    "HP loss=" + loss + "; live stones=" + boss.Runner.LiveProjectileCount
                );
            }

            if (skill.MoveSpeed > 0)
            {
                Test(
                    travel > 1 &&
                        boss.GetComponent<NavMeshAgent>().updatePosition,
                    "特殊位移与控制权恢复 " + skill.Id,
                    "travel=" + travel
                );
            }
        }

        File.WriteAllLines("Logs/GiantGolem/SkillRuntime.csv", samples);
        boss.ResetForCheckpoint();
        boss.SetAutomatic(false);
        var area = boss.GetComponent<BossDamageArea>();
        foreach (float height in new[]
        {
            0f,
            .2f,
            1.2f
        }
        )
        {
            Place(boss.transform.position + boss.transform.forward * 4 + Vector3.up * height);
            player.enabled = false;
            Physics.SyncTransforms();
            int before = player.Health.CurrentHealth;
            area.Pulse(boss.transform.position, 8, 40, new HashSet<IDamageable>());
            Test(
                player.Health.CurrentHealth == before - (height < .55f ? 40 : 0),
                "地波高度过滤 " + height,
                "短暂离地仍受伤；足够高度免伤"
            );
            player.enabled = true;
        }

        Place(boss.transform.position + boss.transform.forward * 10);
        int outside = player.Health.CurrentHealth;
        area.Pulse(boss.transform.position, 8, 40, new HashSet<IDamageable>());
        Test(player.Health.CurrentHealth == outside, "地波范围外安全", "10m 超过8m半径");
        Place(boss.transform.position + boss.transform.forward * 4);
        yield return .25f;
        player.Motor.Jump();
        player.ChangeState(player.AirborneState);
        yield return new Func<bool>(() => player.transform.position.y > .85f);
        int airborne = player.Health.CurrentHealth;
        area.Pulse(boss.transform.position, 8, 40, new HashSet<IDamageable>());
        Test(
            player.Health.CurrentHealth == airborne &&
                player.CurrentState == player.AirborneState,
            "真实玩家跳跃躲地波",
            "使用现有 PlayerMotor/空中状态"
        );
        yield return new Func<bool>(() => player.Motor.IsGrounded);
        yield return .2f;
        Place(boss.transform.position + boss.transform.forward * 4);
        yield return .2f;
        player.Stamina.RestoreFull();
        player.ChangeState(player.DodgeState);
        yield return new Func<bool>(() => player.Health.IsInvincible);
        int rolling = player.Health.CurrentHealth;
        area.Pulse(boss.transform.position, 8, 40, new HashSet<IDamageable>());
        Test(
            player.Health.CurrentHealth == rolling &&
                player.CurrentState == player.DodgeState,
            "真实翻滚无敌帧躲地波",
            "复用 PlayerDodgeState 的无敌窗口"
        );
        yield return new Func<bool>(() => !player.Health.IsInvincible &&
                player.CurrentState == player.LocomotionState);
        Place(boss.transform.position + boss.transform.forward * 4);
        yield return .1f;
        int afterRoll = player.Health.CurrentHealth;
        area.Pulse(boss.transform.position, 8, 40, new HashSet<IDamageable>());
        Test(player.Health.CurrentHealth == afterRoll - 40, "翻滚无敌结束后受伤", "不把整个翻滚状态设成免伤");
        yield return .6f;
        var motor = boss.GetComponent<EnemyMotor>();
        var agent = boss.GetComponent<NavMeshAgent>();
        motor.Teleport(new Vector3(9, .05f, -8), Quaternion.identity);
        motor.BeginSpecialMovement();
        bool moved = true;
        for (int i = 0; i < 25 &&
            moved; i++)
        {
            moved = motor.MoveSpecial(Vector3.forward, .25f, 1);
        }

        motor.EndSpecialMovement();
        Test(
            !moved &&
                boss.transform.position.z < -6.1f &&
                agent.updatePosition,
            "特殊位移碰障碍停止",
            "真实场景右侧石柱；不穿墙"
        );
        var rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Boss/PF_GolemRock.prefab");
        var rock = Object.Instantiate(rockPrefab, new Vector3(9, 1.5f, -10), Quaternion.identity).GetComponent<BossRockProjectile>();
        rock.Launch(boss.gameObject, new Vector3(9, 1.5f, 0), 30);
        yield return new Func<bool>(() => rock == null);
        Test(
            Object.FindObjectsOfType<ParticleSystem>().Any(p => p.name.StartsWith("PF_GolemRockBreak")),
            "石头碰障碍碎裂并销毁",
            "右侧石柱真实连续扫掠碰撞"
        );
        var expire = Object.Instantiate(rockPrefab, new Vector3(0, 12, 0), Quaternion.identity).GetComponent<BossRockProjectile>();
        GiantGolemBossSetup.Set(expire, "_lifetime", .2f);
        expire.Launch(boss.gameObject, new Vector3(0, 12, 15), 30);
        yield return new Func<bool>(() => expire == null);
        Test(true, "石头寿命销毁", "未碰撞石头到寿命后清理");
        boss.ResetForCheckpoint();
        boss.SetAutomatic(false);
        Place(boss.transform.position + boss.transform.forward * 10);
        yield return .6f;
        typeof(PlayerTargeting).GetMethod("HandleLockOnPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player.Targeting, null);
        yield return .2f;
        Test(
            player.Targeting.CurrentTarget == boss.GetComponent<Targetable>(),
            "现有玩家锁定 Boss",
            "同一 Targetable/相机接线"
        );
        player.Targeting.ClearTarget();
        Place(boss.transform.position + boss.transform.forward * 1.5f);
        yield return .3f;
        player.InputReader.enabled = true;
        player.Stamina.RestoreFull();
        int bossHP = boss.GetComponent<EnemyHealth>().CurrentHealth;
        Test(player.TryBeginAttack(PlayerAttackType.Light), "现有玩家武器攻击启动", "真实玩家战斗入口");
        yield return new Func<bool>(() => !player.Combat.IsAttacking &&
                player.CurrentState == player.LocomotionState);
        Test(
            boss.GetComponent<EnemyHealth>().CurrentHealth < bossHP &&
                !boss.Runner.IsRunning,
            "现有武器可伤害 Boss",
            "EnemyHealth 正常扣血，Boss 不进小怪 Hurt"
        );
        player.InputReader.enabled = false;
        boss.ResetForCheckpoint();
        Place(boss.transform.position + boss.transform.forward * 2.2f);
        yield return .2f;
        boss.TryForceSkill("attack01");
        yield return new Func<bool>(() => boss.Runner.NormalizedTime > .40f);
        player.gameObject.SetActive(false);
        yield return .2f;
        Test(!boss.Runner.IsRunning &&
                boss.Runner.LiveProjectileCount == 0, "目标失效取消技能", "玩家失效时关闭命中请求");
        player.gameObject.SetActive(true);
        player.InputReader.enabled = false;
        Place(boss.transform.position + boss.transform.forward * 4);
        yield return .2f;
        boss.TryForceSkill("attack_jumpAtk");
        yield return .3f;
        Place(new Vector3(37, .12f, 0));
        yield return .3f;
        Test(!boss.Runner.IsRunning &&
                boss.Blackboard.LastSkill == null, "玩家出领地回位重置", "回位完成后清空技能与历史");
        boss.ResetForCheckpoint();
        boss.SetAutomatic(false);
        Place(boss.transform.position + boss.transform.forward * 5);
        yield return .2f;
        Test(boss.TryForceSkill("attack_whirlwind"), "死亡中断准备", "旋转技能运行中");
        yield return new Func<bool>(() => boss.Runner.NormalizedTime > .35f);
        int bossSouls = player.GetComponent<SoulWallet>().CurrentSouls;
        boss.GetComponent<EnemyHealth>().TakeDamage(new DamageInfo
            {
                Damage = 99999,
                Attacker = player.gameObject
            });
        boss.GetComponent<EnemyHealth>().TakeDamage(new DamageInfo
            {
                Damage = 99999,
                Attacker = player.gameObject
            });
        yield return .1f;
        Test(
            !boss.Runner.IsRunning &&
                boss.Runner.LiveProjectileCount == 0 &&
                agent.updatePosition &&
                !boss.GetComponent<Targetable>().IsAvailable,
            "Boss 死亡即时清理",
            "命中、位移、目标、投射物关闭"
        );
        Test(
            player.GetComponent<SoulWallet>().CurrentSouls == bossSouls + 500,
            "Boss 死亡奖励一次",
            "复用 EnemyReward；重复伤害不会重复发奖"
        );
        var checkpoint = Object.FindObjectOfType<CheckpointManager>();
        checkpoint.ResetWorld();
        yield return .2f;
        Test(
            boss.GetComponent<EnemyHealth>().CurrentHealth == 1200 &&
                boss.Blackboard.LastSkill == null &&
                Vector3.Distance(boss.transform.position, boss.GetComponent<EnemyTerritory>().HomePosition) < .2f,
            "检查点恢复 Boss",
            "生命、回位、历史清零"
        );
        small.enabled = true;
        var health = small.GetComponent<EnemyHealth>();
        health.TakeDamage(new DamageInfo
            {
                Damage = 1
            });
        Test(small.CurrentState == small.HurtState, "小怪伤害事件回归", "旧 Hurt 流程保留");
        small.enabled = false;
        small.enabled = true;
        int notices = 0;
        health.DamageTaken += _ => notices++;
        health.TakeDamage(new DamageInfo
            {
                Damage = 1
            });
        Test(notices == 1 &&
                small.CurrentState == small.HurtState, "小怪启停订阅回归", "每次伤害只有一次通知");
        int souls = player.GetComponent<SoulWallet>().CurrentSouls;
        health.TakeDamage(new DamageInfo
            {
                Damage = 99999
            });
        health.TakeDamage(new DamageInfo
            {
                Damage = 99999
            });
        Test(
            small.CurrentState == small.DeadState &&
                player.GetComponent<SoulWallet>().CurrentSouls == souls + small.GetComponent<EnemyReward>().SoulReward,
            "小怪死亡奖励一次",
            "重复致死调用不重复发奖"
        );
        checkpoint.ResetWorld();
        yield return .2f;
        Test(
            health.CurrentHealth == health.MaxHealth &&
                small.CurrentState != small.DeadState,
            "检查点恢复小怪",
            "死敌与 Boss 共用重置契约"
        );
        small.gameObject.SetActive(false);
        Place(new Vector3(0, .12f, -17));
        yield return .2f;
        boss.ResetForCheckpoint();
        boss.SetAutomatic(true);
        int beforeDecision = boss.DecisionCount;
        yield return new Func<bool>(() => boss.DecisionCount > beforeDecision);
        Test(
            boss.LastDecision == "快跑接近" ||
                boss.LastDecision == "attack_throwstone",
            "自动远距行动",
            "运行中的行为树实际决策=" + boss.LastDecision
        );
        yield return new Func<bool>(() => boss.Runner.IsRunning ||
                boss.Blackboard.Distance < 8);
        Test(boss.Blackboard.ActiveNode == "接近计划" ||
                boss.Runner.IsRunning, "自动行动保持", "远距接近或技能执行");
        boss.SetAutomatic(false);
        boss.ResetForCheckpoint();
        Place(new Vector3(0, .12f, -14));
        Capture();
        WriteReport();
        Time.timeScale = 1;
    }

    private static void Place(Vector3 position)
    {
        player.Health.ReviveFull();
        player.Health.DisableIFrame();
        player.Respawn();
        player.Motor.Teleport(new Vector3(position.x, Mathf.Max(.1f, position.y), position.z), Quaternion.identity);
        player.InputReader.ClearPendingActions();
        Physics.SyncTransforms();
    }

    private static IEnumerator Integration()
    {
        boss = Object.FindObjectOfType<BossBrain>();
        player = Object.FindObjectOfType<PlayerStateMachine>();
        small = Object.FindObjectOfType<EnemyStateMachine>();
        boss.SetAutomatic(false);
        small.gameObject.SetActive(false);
        player.InputReader.enabled = false;
        player.Health.SetMaxHealth(1000);
        Place(boss.transform.position + boss.transform.forward * 30);
        yield return .2f;
        var savePath = SaveService.SaveFilePath;
        byte[] saveBefore;
        if (File.Exists(savePath))
        {
            saveBefore = File.ReadAllBytes(savePath);
        }
        else
        {
            saveBefore = null;
        }

        Test(boss.TryForceSkill("attack_throwstone"), "已释放石头的中断准备", "远距投石真实执行");
        yield return new Func<bool>(() => boss.Runner.Released &&
                boss.Runner.LiveProjectileCount > 0);
        // 飞出的石头应继续固定弹道；普通技能 Abort 不回收已释放投射物。
        var stone = Object.FindObjectsOfType<BossRockProjectile>().Single(s => s.Owner == boss.gameObject);
        Vector3 stoneStart = stone.transform.position;
        boss.Runner.Abort();
        yield return .15f;
        Test(
            stone != null &&
                Vector3.Distance(stoneStart, stone.transform.position) > .2f &&
                !boss.Runner.IsRunning,
            "普通 Abort 保留已释放石头",
            "扩展破防入口只取消执行，不回收飞行物"
        );
        player.Health.Die();
        yield return .25f;
        Test(
            !boss.Runner.IsRunning &&
                boss.Runner.LiveProjectileCount == 0 &&
                boss.Blackboard.CurrentSkill == null,
            "玩家死亡清理已飞出石头",
            "目标失效进入回位，清除所属投射物"
        );
        yield return new Func<bool>(() => !player.Health.IsDead &&
                player.CurrentState != player.DeadState);
        Test(
            Vector3.Distance(player.transform.position, new Vector3(0, .12f, -14)) < .6f &&
                boss.GetComponent<EnemyHealth>().CurrentHealth == 1200,
            "真实玩家死亡重生",
            "死亡动画、黑屏、默认出生点、世界重置"
        );
        Test(
            saveBefore == null ? !File.Exists(savePath) : File.Exists(savePath) &&
                saveBefore.SequenceEqual(File.ReadAllBytes(savePath)),
            "实测存档字节不变",
            "死亡掉魂/奖励/恢复均不覆盖正式存档"
        );
        boss.SetAutomatic(false);
        boss.ResetForCheckpoint();
        player.InputReader.enabled = false;
        Place(boss.transform.position - boss.transform.right * 2.5f);
        yield return .2f;
        var rotation = boss.transform.rotation;
        Test(boss.TryForceSkill("attack_foot_left"), "左右脚前摇绕侧准备", "提交左脚技能");
        yield return new Func<bool>(() => boss.Runner.NormalizedTime > .22f);
        Place(boss.transform.position + boss.transform.right * 2.5f);
        yield return new Func<bool>(() => !boss.Runner.IsRunning);
        Test(
            boss.Blackboard.LastCompletedSkill.Id == "attack_foot_left" &&
                Quaternion.Angle(rotation, boss.transform.rotation) < 1,
            "前摇绕侧不换脚不转正",
            "提交后固定脚侧，玩家绕到另一侧仍执行左脚"
        );
        boss.ResetForCheckpoint();
        Place(boss.transform.position + boss.transform.forward * 6);
        yield return .2f;
        boss.TryForceSkill("attack_DashAtk");
        yield return new Func<bool>(() => boss.Runner.DirectionLocked);
        Vector3 locked = boss.transform.forward;
        Place(boss.transform.position + boss.transform.right * 6);
        yield return new Func<bool>(() => !boss.Runner.IsRunning);
        Test(Vector3.Angle(locked, boss.transform.forward) < 1, "冲刺出手后不追踪急转", "程序位移使用提交后的锁定方向");
        boss.ResetForCheckpoint();
        Place(boss.transform.position + boss.transform.forward * 2.2f);
        yield return .2f;
        boss.TryForceSkill("attack01");
        yield return new Func<bool>(() => boss.Runner.NormalizedTime > .40f);
        boss.gameObject.SetActive(false);
        yield return .1f;
        Test(
            !boss.Runner.IsRunning &&
                boss.Blackboard.CurrentSkill == null &&
                boss.Runner.LiveProjectileCount == 0,
            "Boss 禁用时清理",
            "生命周期取消命中/移动/技能请求"
        );
        boss.gameObject.SetActive(true);
        yield return .2f;
        Test(
            boss.Blackboard.LastSkill == null &&
                boss.GetComponent<NavMeshAgent>().updatePosition,
            "Boss 再启用初始化",
            "实例历史归零，导航控制权恢复"
        );
        Place(boss.transform.position + boss.transform.forward * 3);
        yield return .2f;
        var checkpoint = Object.FindObjectOfType<CheckpointManager>();
        var site = Object.FindObjectsOfType<CheckpointSite>().First();
        boss.GetComponent<EnemyHealth>().TakeDamage(new DamageInfo
            {
                Damage = 50
            });
        player.Health.TakeDamage(new DamageInfo
            {
                Damage = 5
            });
        Test(checkpoint.ActivateCheckpoint(site), "真实检查点休息接受", "现有交互服务入口");
        Test(
            boss.GetComponent<EnemyHealth>().CurrentHealth == 1200 &&
                player.Health.CurrentHealth == player.Health.MaxHealth,
            "休息恢复 Boss 与玩家",
            "共享世界重置契约与玩家恢复"
        );
        foreach (var presenter in Object.FindObjectsOfType<ProgressionPresenter>())
        {
            presenter.CloseMenu();
        }

        Place(new Vector3(0, .12f, -14));
        boss.ResetForCheckpoint();
        boss.SetAutomatic(false);
        yield return .5f;
        Capture();
        ScreenCapture.CaptureScreenshot("Docs/GiantGolem_BossGameView.png");
        yield return .5f;
        Test(
            Object.FindObjectOfType<PlayerHUDView>() != null &&
                Object.FindObjectOfType<QuickItemPresenter>() != null &&
                Object.FindObjectOfType<LockOnCameraRig>() != null,
            "复用玩家 HUD 与相机组件",
            "真实场景保留现有接线"
        );
        Test(Object.FindObjectOfType<Light>() != null, "测试场景照明接线", "Lit 模型具备实际光源");
    }

    public static void Capture()
    {
        var camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        var previous = RenderTexture.active;
        var old = camera.targetTexture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("Docs/GiantGolem_BossTest.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = old;
            RenderTexture.active = previous;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }

    private static void Property(object target, string name, object value)
    {
        target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);
    }

    private static void Test(bool passed, string name, string detail)
    {
        checks.Add(new Check
            {
                name = name,
                passed = passed,
                detail = detail
            });
        WriteReport();
    }

    private static void Log(string condition, string trace, LogType type)
    {
        if (!SessionState.GetBool(Running, false))
        {
            return;
        }

        if (type == LogType.Error ||
            type == LogType.Exception ||
            type == LogType.Assert)
        {
            errors++;
            SessionState.SetInt(Running + ".Errors", errors);
        }

        if (type == LogType.Warning)
        {
            warnings++;
            SessionState.SetInt(Running + ".Warnings", warnings);
        }
    }

    private static void WriteReport()
    {
        var report = new Report
        {
            timestamp = DateTime.Now.ToString("O"),
            scene = GiantGolemBossSetup.ScenePath,
            checks = checks.ToArray(),
            passed = checks.Count(c => c.passed),
            failed = checks.Count(c => !c.passed),
            runtimeErrors = errors,
            runtimeWarnings = warnings,
            manualChecks = new[]
            {
                "键盘/手柄自由绕侧、完整战斗手感和镜头体验待人工试玩；代码触发的真实跳跃/翻滚不能替代手感验收",
                "地波圆环与碎石为功能提示，非最终美术特效"
            }
        };
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
    }

    private static void ReadReport()
    {
        checks.Clear();
        if (!File.Exists(ReportPath))
        {
            return;
        }

        var report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
        checks.AddRange(report.checks);
        errors = report.runtimeErrors;
        warnings = report.runtimeWarnings;
    }
}
