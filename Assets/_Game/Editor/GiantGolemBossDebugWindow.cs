using UnityEditor;
using UnityEngine;

public sealed class GiantGolemBossDebugWindow : EditorWindow
{
    private BossBrain boss;
    private Vector2 scroll;

    [MenuItem("Tools/SoulsLike RPG/Giant Golem/3 Boss AI Debugger")]
    public static void Open()
    {
        GetWindow<GiantGolemBossDebugWindow>("Golem AI");
    }

    private void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        boss = (BossBrain)EditorGUILayout.ObjectField("Boss", boss, typeof(BossBrain), true);
        if (boss == null &&
            GUILayout.Button("查找当前场景 Boss"))
        {
            boss = FindObjectOfType<BossBrain>();
        }

        if (boss == null)
        {
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 Play Mode 后可观察决策和强制播放技能。", MessageType.Info);
            return;
        }

        var player = FindObjectOfType<PlayerStateMachine>();
        using (new EditorGUI.DisabledScope(player == null ||
                player.Health.IsDead ||
                player.CurrentState != player.LocomotionState))
        {
            if (GUILayout.Button("传送玩家到 Boss 测试点"))
            {
                TeleportPlayerToApproachPoint();
            }
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        var b = boss.Blackboard;
        EditorGUILayout.LabelField("行为节点", b.ActiveNode);
        EditorGUILayout.LabelField("目标", b.Target != null ? b.Target.name : "无");
        EditorGUILayout.LabelField("距离 / 夹角 / 左右", $"{b.Distance:F2}m / {b.Angle:F1}° / {b.Side:F2}（负=左）");
        EditorGUILayout.LabelField(
            "本招 / 上招",
            (b.CurrentSkill != null ? b.CurrentSkill.Id : "无") + " / " + (b.LastSkill != null ? b.LastSkill.Id : "无")
        );
        EditorGUILayout.LabelField("最后决策", boss.LastDecision + "；累计 " + boss.DecisionCount);
        EditorGUILayout.LabelField("执行阶段", boss.Runner.Phase + $"  t={boss.Runner.NormalizedTime:F3}");
        EditorGUILayout.LabelField("路径 / 移动 / 投射", $"{b.PathReachable} / {b.MovementClear} / {b.ThrowClear}");
        EditorGUILayout.LabelField("运行中的石头", boss.Runner.LiveProjectileCount.ToString());
        bool auto = EditorGUILayout.Toggle("自动 AI", boss.Automatic);
        if (auto != boss.Automatic)
        {
            boss.SetAutomatic(auto);
        }

        if (GUILayout.Button("重置 Boss（生命、位置、历史、技能、投射物）"))
        {
            boss.ResetForCheckpoint();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("上次抽签的候选概率与筛除原因", EditorStyles.boldLabel);
        foreach (var c in boss.Selector.Candidates)
        {
            EditorGUILayout.LabelField(
                c.Skill.Id,
                $"{c.Probability:P1}  权重 {c.Weight:F2}  {c.Reason}  CD {Mathf.Max(0, b.ReadyAt(c.Skill.Family) - Time.time):F1}s"
            );
        }

        EditorGUILayout.LabelField("快跑接近", boss.Selector.RunProbability.ToString("P1"));
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("强制技能用于检查动作，绕过距离与冷却筛选；须先关闭自动 AI。死亡、回位和目标失效仍可中断技能。", MessageType.Info);
        using (new EditorGUI.DisabledScope(boss.Automatic ||
                boss.Runner.IsRunning))
        {
            foreach (var skill in boss.Skills)
            {
                if (skill != null &&
                    GUILayout.Button(skill.Id))
                {
                    boss.TryForceSkill(skill.Id);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    public static void TeleportPlayerToApproachPoint()
    {
        if (!EditorApplication.isPlaying)
        {
            throw new System.InvalidOperationException("测试传送只在 Play Mode 使用。");
        }

        var player = FindObjectOfType<PlayerStateMachine>();
        var point = GameObject.Find("GiantGolem_ApproachPoint");
        if (player == null ||
            point == null ||
            player.Health.IsDead ||
            player.CurrentState != player.LocomotionState)
        {
            return;
        }

        Vector3 previous = player.transform.position;
        player.Targeting.ClearTarget();
        player.InputReader.ClearPendingActions();
        player.Motor.Teleport(point.transform.position + Vector3.up * .1f, point.transform.rotation);
        foreach (var camera in FindObjectsOfType<Cinemachine.CinemachineVirtualCameraBase>())
        {
            if (camera.Follow != null &&
                camera.Follow.IsChildOf(player.transform))
            {
                camera.OnTargetObjectWarped(camera.Follow, player.transform.position - previous);
            }
        }
    }
}
