using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Day4 Task2 验证工具：验证 Attack / Dodge 的 Stamina 消耗闭环，以及边界行为。
/// 使用方式：进入 Play Mode 后执行 Tools/SoulsLike RPG/Day4/Run Task2 Stamina Probe。
/// </summary>
public static class Day4Task2StaminaProbe
{
    private static int _framesToWait;

    [MenuItem("Tools/SoulsLike RPG/Day4/Run Task2 Stamina Probe")]
    public static void RunStaminaProbe()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Day4 Task2 Stamina] 需要在 Play Mode 中执行。");
            return;
        }

        PlayerStamina stamina = Object.FindFirstObjectByType<PlayerStamina>();
        PlayerInputReader inputReader =
            Object.FindFirstObjectByType<PlayerInputReader>();
        PlayerStateMachine stateMachine =
            Object.FindFirstObjectByType<PlayerStateMachine>();

        if (stamina == null || inputReader == null || stateMachine == null)
        {
            Debug.LogError("[Day4 Task2 Stamina] 找不到 PlayerStamina / PlayerInputReader / PlayerStateMachine。");
            return;
        }

        Debug.Log(
            $"[Day4 Task2 Stamina] 配置: Max={stamina.MaxStamina:0.##}, " +
            $"AttackCost={stamina.AttackCost:0.##}, DodgeCost={stamina.DodgeCost:0.##}"
        );

        // 边界场景 A：体力 = 0（严格为空）
        ForceStamina(stamina, 0f);
        bool canAttackAtZero = stamina.CanConsume(stamina.AttackCost);
        bool canDodgeAtZero = stamina.CanConsume(stamina.DodgeCost);
        Debug.Log(
            $"[Day4 Task2 Stamina] SP=0: CanConsume(Attack={stamina.AttackCost:0.##})={canAttackAtZero}, " +
            $"CanConsume(Dodge={stamina.DodgeCost:0.##})={canDodgeAtZero}"
        );

        // 边界场景 B：体力 = 25（介于 20 和 30 之间）
        ForceStamina(stamina, 25f);
        bool canAttackAt25 = stamina.CanConsume(stamina.AttackCost);
        bool canDodgeAt25 = stamina.CanConsume(stamina.DodgeCost);
        Debug.Log(
            $"[Day4 Task2 Stamina] SP=25: CanConsume(Attack)={canAttackAt25}, " +
            $"CanConsume(Dodge)={canDodgeAt25}"
        );

        // 边界场景 C：体力 = 20（恰好等于攻击消耗）
        ForceStamina(stamina, 20f);
        bool canAttackAt20 = stamina.CanConsume(stamina.AttackCost);
        Debug.Log(
            $"[Day4 Task2 Stamina] SP=20: CanConsume(Attack)={canAttackAt20} (恰好等于成本)"
        );

        // 边界场景 D：体力 = 29.9
        ForceStamina(stamina, 29.9f);
        bool canDodgeAt299 = stamina.CanConsume(stamina.DodgeCost);
        Debug.Log(
            $"[Day4 Task2 Stamina] SP=29.9: CanConsume(Dodge)={canDodgeAt299}"
        );

        // 恢复满体力做一次完整攻击验证
        ForceStamina(stamina, stamina.MaxStamina);
        InvokeInput(inputReader, "OnLightAttack");
        _framesToWait = 5;
        EditorApplication.update += PollAfterFrames;
    }

    private static void PollAfterFrames()
    {
        if (_framesToWait > 0)
        {
            _framesToWait--;
            return;
        }

        EditorApplication.update -= PollAfterFrames;

        PlayerStamina stamina = Object.FindFirstObjectByType<PlayerStamina>();
        PlayerStateMachine stateMachine =
            Object.FindFirstObjectByType<PlayerStateMachine>();
        if (stamina == null || stateMachine == null)
            return;

        Debug.Log(
            $"[Day4 Task2 Stamina] 满体力攻击后: 状态={stateMachine.CurrentStateName}, " +
            $"SP={stamina.CurrentStamina:0.##} (期望 {stamina.MaxStamina - stamina.AttackCost:0.##})"
        );
    }

    private static void ForceStamina(PlayerStamina stamina, float value)
    {
        var field = typeof(PlayerStamina).GetField(
            "_currentStamina",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        field?.SetValue(stamina, Mathf.Clamp(value, 0f, stamina.MaxStamina));
    }

    private static void InvokeInput(PlayerInputReader inputReader, string method)
    {
        MethodInfo methodInfo = typeof(PlayerInputReader).GetMethod(
            method,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        methodInfo?.Invoke(inputReader, new object[] { default });
    }
}
