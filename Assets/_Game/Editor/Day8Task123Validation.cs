using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>在实际检查点场景运行验证；只修改 Play Mode 状态，不写玩家存档。</summary>
[InitializeOnLoad]
public static class Day8Task123Validation
{
    private const string RunningKey = "Day8.Task123.Validation.Running";
    private const string ResultKey = "Day8.Task123.Validation.Result";
    private const string ReportPath = "Logs/Day8_Task123_Validation.json";
    private const string ConsolePath = "Logs/Day8_RuntimeConsole.log";
    private static readonly List<string> Passed = new List<string>();
    private static int _frames;
    private static int _phase;
    private static bool _failed;
    private static EnemyHealth _damageTarget;
    private static int _expectedDamage;
    private static int _targetHealthBefore;

    [Serializable]
    private sealed class Result
    {
        public string scene;
        public bool passed;
        public string[] checks;
        public string failure;
    }

    static Day8Task123Validation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(RunningKey, false))
            Application.logMessageReceived += RecordConsole;
    }

    [MenuItem("Tools/SoulsLike RPG/Day8/Validate Task1-3 (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请在 Edit Mode 开始验证。");

        if (Application.isBatchMode)
        {
            EditorSceneManager.OpenScene(Day8Task123Builder.ScenePath);
        }
        else if (EditorSceneManager.GetActiveScene().path != Day8Task123Builder.ScenePath ||
            EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("请先手动打开并保存 03_AncientDungeon_Checkpoint 场景，再运行验证。");
        }
        VerifyReferences();
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ConsolePath, "");
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(ResultKey, false);
        Application.logMessageReceived -= RecordConsole;
        Application.logMessageReceived += RecordConsole;
        EditorApplication.isPlaying = true;
    }

    private static void VerifyReferences()
    {
        PlayerHealth player = Object.FindObjectOfType<PlayerHealth>();
        if (player == null || player.GetComponent<SoulWallet>() == null ||
            player.GetComponent<PlayerProgression>() == null)
            throw new MissingComponentException("检查点场景缺少玩家成长组件。");

        foreach (EnemyHealth health in Object.FindObjectsOfType<EnemyHealth>())
        {
            if (health.GetComponent<EnemyReward>() == null)
                throw new MissingComponentException($"{health.name} 缺少 EnemyReward。");
        }

        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                throw new MissingComponentException($"{child.name} 存在 Missing Script。");
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _frames = 0;
            _phase = 0;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(RunningKey, false);
            Application.logMessageReceived -= RecordConsole;
            bool success = SessionState.GetBool(ResultKey, false);
            Debug.Log($"[Day8 Validation] Result={success}; report={ReportPath}");
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || ++_frames < 6)
            return;

        try
        {
            if (_phase == 0)
            {
                ValidateWalletAndHud();
                ValidateRewards();
                ValidateProgression();
                BeginActualAttack();
                _phase = 1;
                _frames = 0;
                return;
            }

            Check(_damageTarget.CurrentHealth == _targetHealthBefore - _expectedDamage,
                $"实际 PlayerCombat → WeaponHitbox 命中造成 {_expectedDamage} 伤害");
            Object.FindObjectOfType<PlayerCombat>().FinishLightAttack();
            CaptureHudPreview();
            Complete(null);
        }
        catch (Exception exception)
        {
            Complete(exception.ToString());
        }
    }

    private static void ValidateWalletAndHud()
    {
        SoulWallet wallet = Object.FindObjectOfType<PlayerHealth>().GetComponent<SoulWallet>();
        SoulHUDPresenter presenter = Object.FindObjectOfType<SoulHUDPresenter>();
        TMP_Text text = presenter.GetComponentInChildren<TMP_Text>();
        Check(wallet.CurrentSouls == 0 && text.text == "0", "场景启动钱包/HUD 均为 0");
        int events = 0;
        Action<int> listener = value => events++;
        wallet.SoulsChanged += listener;
        wallet.AddSouls(120);
        Check(wallet.CurrentSouls == 120 && text.text == "120" && events == 1,
            "获得 Soul 同步发送一次事件并更新 HUD");
        wallet.AddSouls(1130);
        Check(text.text == "1,250", "HUD 使用固定千分位格式");
        wallet.AddSouls(0);
        wallet.AddSouls(-5);
        Check(wallet.CurrentSouls == 1250 && events == 2, "零/负奖励不改变钱包或发事件");
        Check(!wallet.CanAfford(-1) && !wallet.TrySpend(-1), "负消费被拒绝");
        Check(!wallet.TrySpend(1251) && wallet.CurrentSouls == 1250,
            "余额不足无法消费且余额不变");
        Check(wallet.TrySpend(0) && events == 2, "零消费成功且不发变化事件");
        Check(wallet.TrySpend(250) && text.text == "1,000" && events == 3,
            "合法消费扣款并立即刷新 HUD");
        presenter.enabled = false;
        wallet.AddSouls(150);
        Check(text.text == "1,000", "HUD 禁用后解除钱包事件订阅");
        presenter.enabled = true;
        Check(text.text == "1,150", "HUD 重新启用立即显示当前余额");
        presenter.Bind(wallet);
        presenter.Bind(wallet);
        wallet.AddSouls(100);
        Check(text.text == "1,250" && events == 5, "重复绑定后货币变化仍正确刷新");
        wallet.SoulsChanged -= listener;

        GameObject temporary = new GameObject("Day8_OverflowWallet");
        SoulWallet overflowWallet = temporary.AddComponent<SoulWallet>();
        overflowWallet.AddSouls(int.MaxValue);
        overflowWallet.AddSouls(100);
        Check(overflowWallet.CurrentSouls == int.MaxValue, "Soul 达到 int 上限时不会溢出为负数");
        Check(overflowWallet.TrySpend(int.MaxValue) && overflowWallet.CurrentSouls == 0,
            "消费完整余额后为 0");
        Object.Destroy(temporary);
    }

    private static void ValidateRewards()
    {
        SoulWallet wallet = Object.FindObjectOfType<PlayerHealth>().GetComponent<SoulWallet>();
        EnemyHealth[] enemies = Object.FindObjectsOfType<EnemyHealth>()
            .OrderBy(enemy => enemy.name).ToArray();
        Check(enemies.Length >= 2, "场景至少有两个带奖励组件的敌人");
        EnemyHealth enemy = enemies[0];
        int before = wallet.CurrentSouls;
        int reward = enemy.GetComponent<EnemyReward>().SoulReward;
        enemy.TakeDamage(new DamageInfo { Damage = enemy.MaxHealth });
        Check(wallet.CurrentSouls == before + reward, "击杀敌人按 Inspector 配置发放 Soul");
        enemy.TakeDamage(new DamageInfo { Damage = int.MaxValue });
        EnemyReward rewardComponent = enemy.GetComponent<EnemyReward>();
        rewardComponent.enabled = false;
        rewardComponent.enabled = true;
        enemy.TakeDamage(new DamageInfo { Damage = 1 });
        Check(wallet.CurrentSouls == before + reward, "重复伤害/奖励组件重启不会重复发奖");
        Check(Object.FindObjectOfType<SoulHUDPresenter>().GetComponentInChildren<TMP_Text>().text ==
            wallet.CurrentSouls.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
            "敌人击杀奖励立即显示到 Soul HUD");
        Object.FindObjectOfType<CheckpointManager>().ResetWorld();
        Check(enemy.CurrentHealth == enemy.MaxHealth, "实际 CheckpointManager.ResetWorld 复活敌人");
        enemy.TakeDamage(new DamageInfo { Damage = enemy.MaxHealth });
        Check(wallet.CurrentSouls == before + reward * 2, "赐福重置后再次击杀可以再次获得奖励");

        EnemyReward secondReward = enemies[1].GetComponent<EnemyReward>();
        SerializedObject serialized = new SerializedObject(secondReward);
        serialized.FindProperty("_soulReward").intValue = 250;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        before = wallet.CurrentSouls;
        enemies[1].TakeDamage(new DamageInfo { Damage = enemies[1].MaxHealth });
        Check(wallet.CurrentSouls == before + 250, "不同敌人可独立配置不同 Soul Reward");
        Object.FindObjectOfType<CheckpointManager>().ResetWorld();
    }

    private static void ValidateProgression()
    {
        PlayerHealth health = Object.FindObjectOfType<PlayerHealth>();
        PlayerProgression progression = health.GetComponent<PlayerProgression>();
        PlayerStamina stamina = health.GetComponent<PlayerStamina>();
        Check(progression.Level == 1 && progression.Vigor == 1 &&
            progression.Endurance == 1 && progression.Strength == 1,
            "Level/Vigor/Endurance/Strength 初始均为 1");
        Check(health.MaxHealth == 100 && stamina.MaxStamina == 100f &&
            Mathf.Approximately(progression.DamageMultiplier, 1f), "初始 HP/SP/伤害倍率为 100/100/1");
        int progressionEvents = 0;
        Action listener = () => progressionEvents++;
        progression.ProgressionChanged += listener;
        health.TakeDamage(new DamageInfo { Damage = 25 });
        stamina.Consume(30f);
        progression.SetProgression(7, 3, 2, 4);
        Check(health.MaxHealth == 120 && health.CurrentHealth == 95,
            "Vigor 3 实际增加最大 HP 到 120，并保留已损失 HP");
        Check(Mathf.Approximately(stamina.MaxStamina, 108f) &&
            Mathf.Approximately(stamina.CurrentStamina, 78f),
            "Endurance 2 实际增加最大体力到 108，并保留已消耗体力");
        Check(Mathf.Approximately(progression.DamageMultiplier, 1.15f) &&
            progression.CalculateAttackDamage(20) == 23, "Strength 4 伤害倍率为 1.15，20 基础伤害变为 23");
        Check(progression.CalculateAttackDamage(0) == 0 &&
            progression.CalculateAttackDamage(-10) == 0, "非正基础伤害不会产生伤害");
        Check(progressionEvents == 1, "基础属性变化只发一次 ProgressionChanged");
        PlayerStatsConfig stats = AssetDatabase.LoadAssetAtPath<PlayerStatsConfig>(
            "Assets/_Game/Configs/Player/SO_PlayerStats_Default.asset");
        Check(stats.MaxHealth == 100 && stats.MaxStamina == 100f,
            "成长属性不会修改共享 PlayerStatsConfig");
        progression.SetProgression(1, 1, 1, 1);
        health.RestoreFull();
        stamina.RestoreFull();
        Check(health.CurrentHealth == 100 && stamina.CurrentStamina == 100f,
            "恢复成长属性后恢复到原始最大生命/体力");
        progression.ProgressionChanged -= listener;

        // 单独玩家资源实例，验证改变最大值不能使死者复活。
        GameObject temporary = new GameObject("Day8_DeadPlayerResource");
        temporary.SetActive(false);
        PlayerHealth temporaryHealth = temporary.AddComponent<PlayerHealth>();
        SerializedObject serializedHealth = new SerializedObject(temporaryHealth);
        serializedHealth.FindProperty("_config").objectReferenceValue = stats;
        serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        temporary.SetActive(true);
        temporaryHealth.Die();
        temporaryHealth.SetMaxHealth(130);
        Check(temporaryHealth.IsDead && temporaryHealth.CurrentHealth == 0,
            "增加最大生命不会意外复活死亡玩家");
        temporaryHealth.ReviveFull();
        Check(temporaryHealth.CurrentHealth == 130, "复活恢复到成长后的最大 HP");
        Object.Destroy(temporary);
    }

    private static void BeginActualAttack()
    {
        PlayerStateMachine player = Object.FindObjectOfType<PlayerStateMachine>();
        player.enabled = false;
        player.InputReader.enabled = false;
        player.GetComponent<PlayerProgression>().SetProgression(4, 1, 1, 4);
        foreach (EnemyStateMachine enemy in Object.FindObjectsOfType<EnemyStateMachine>())
        {
            enemy.enabled = false;
            enemy.Combat.CancelAttack();
            enemy.GetComponent<NavMeshAgent>().enabled = false;
        }

        _damageTarget = Object.FindObjectsOfType<EnemyHealth>().OrderBy(enemy => enemy.name).First();
        _targetHealthBefore = _damageTarget.CurrentHealth;
        PlayerCombat combat = player.Combat;
        AttackData data = combat.CurrentAttack;
        int baseDamage = data.Damage;
        _expectedDamage = player.GetComponent<PlayerProgression>().CalculateAttackDamage(baseDamage);
        Check(combat.StartLightAttack(), "实际 PlayerCombat 能启动攻击动画");
        WeaponHitbox hitbox = player.GetComponentInChildren<WeaponHitbox>();
        BoxCollider shape = (BoxCollider)new SerializedObject(hitbox)
            .FindProperty("_shape").objectReferenceValue;
        Animator animator = player.GetComponentInChildren<Animator>();
        animator.Play("Base Layer." + data.AnimationStateName, 0,
            (data.HitWindowStart + data.HitWindowEnd) * 0.5f);
        animator.Update(0f);
        animator.speed = 0f;
        Collider targetCollider = _damageTarget.GetComponentsInChildren<Collider>()
            .First(collider => collider.enabled && !collider.isTrigger &&
                ((1 << collider.gameObject.layer) & (1 << 3)) != 0);
        _damageTarget.transform.position += shape.transform.TransformPoint(shape.center) -
            targetCollider.bounds.center;
        Physics.SyncTransforms();
        combat.TickLightAttack();
        Check(data.Damage == baseDamage, "应用力量倍率后共享 AttackData.Damage 保持原值");
    }

    private static void CaptureHudPreview()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return;

        Canvas canvas = Object.FindObjectOfType<SoulHUDPresenter>().GetComponentInParent<Canvas>();
        SoulWallet wallet = Object.FindObjectOfType<PlayerHealth>().GetComponent<SoulWallet>();
        wallet.TrySpend(wallet.CurrentSouls);
        wallet.AddSouls(2450);
        Camera camera = Camera.main;
        RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/Day8_SoulHUD_Preview.png", image.EncodeToPNG());
            Check(true, "已生成实际场景 Soul HUD 1920×1080 预览");
        }
        finally
        {
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            if (image != null)
                Object.Destroy(image);
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
        Passed.Add(description);
        Debug.Log("[Day8 Validation PASS] " + description);
    }

    private static void RecordConsole(string message, string stack, LogType type)
    {
        if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception &&
            type != LogType.Assert)
            return;

        File.AppendAllText(ConsolePath, $"[{type}] {message}\n{stack}\n");
        if (type != LogType.Warning)
            _failed = true;
    }

    private static void Complete(string failure)
    {
        EditorApplication.update -= Tick;
        bool success = failure == null && !_failed;
        File.WriteAllText(ReportPath, JsonUtility.ToJson(new Result
        {
            scene = Day8Task123Builder.ScenePath,
            passed = success,
            checks = Passed.ToArray(),
            failure = failure ?? (_failed ? "运行期间出现 Console Error；请检查 RuntimeConsole.log。" : "")
        }, true));
        SessionState.SetBool(ResultKey, success);
        EditorApplication.isPlaying = false;
    }
}
