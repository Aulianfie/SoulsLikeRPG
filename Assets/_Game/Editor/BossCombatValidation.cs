using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// Validate real skill execution in the dungeon; restore the user's save after Play Mode.
[InitializeOnLoad]
public static class BossCombatValidation
{
    private const string Running = "BossCombat.Validation";
    private const string LogDirectory = "Logs/BossCombatFix/";
    private static readonly List<string> Checks = new List<string>();
    private static readonly List<string> Observations = new List<string>();
    private static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
    private static Func<bool> _waiting;
    private static double _next;
    private static double _deadline;
    private static double _waitDeadline;
    private static int _errors;
    private static int _warnings;
    private static BossBrain _boss;
    private static PlayerStateMachine _player;

    static BossCombatValidation()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += RecordLog;
    }

    [MenuItem("Tools/SoulsLike RPG/Giant Golem/Validate Combat Hit Stages")]
    public static void Run()
    {
        Begin(false);
    }

    public static void Begin(bool baseline, bool heldReplayOnly = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("请在已保存的 Edit Mode 场景运行验证。");
        }

        Directory.CreateDirectory(LogDirectory);
        if (!baseline)
        {
            ValidateSkillWindows();
        }

        SessionState.SetString(Running + ".Scene", EditorSceneManager.GetActiveScene().path);
        SessionState.SetBool(Running + ".Baseline", baseline);
        SessionState.SetBool(Running + ".HeldReplayOnly", heldReplayOnly);
        string save = SaveService.SaveFilePath;
        SessionState.SetString(Running + ".Save", save);
        SessionState.SetBool(Running + ".SaveExists", File.Exists(save));
        if (File.Exists(save))
        {
            File.Copy(save, LogDirectory + "OriginalSave.bin", true);
        }

        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene(GiantGolemDungeonPlacement.MainScenePath);
        EditorApplication.isPlaying = true;
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false))
        {
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Checks.Clear();
            Observations.Clear();
            Routines.Clear();
            _waiting = null;
            _errors = 0;
            _warnings = 0;
            _next = EditorApplication.timeSinceStartup + .5;
            _deadline = _next + 240;
            Routines.Push(Scenarios());
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            string save = SessionState.GetString(Running + ".Save", "");
            if (SessionState.GetBool(Running + ".SaveExists", false))
            {
                File.Copy(LogDirectory + "OriginalSave.bin", save, true);
                Check(File.ReadAllBytes(save).SequenceEqual(File.ReadAllBytes(LogDirectory + "OriginalSave.bin")), "Original save bytes restored");
            }
            else if (File.Exists(save))
            {
                File.Delete(save);
            }

            Check(_errors == 0 && _warnings == 0, "No runtime Console errors or warnings");
            WriteReport();
            SessionState.SetBool(Running, false);
            string originalScene = SessionState.GetString(Running + ".Scene", "");
            if (EditorSceneManager.GetActiveScene().path != originalScene)
            {
                EditorSceneManager.OpenScene(originalScene);
            }
        }
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Running, false) ||
            !EditorApplication.isPlaying ||
            EditorApplication.isPaused ||
            EditorApplication.timeSinceStartup < _next)
        {
            return;
        }

        try
        {
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                throw new TimeoutException("Boss combat validation timed out.");
            }

            if (_waiting != null)
            {
                if (EditorApplication.timeSinceStartup > _waitDeadline)
                {
                    throw new TimeoutException("Wait timed out: " + Checks.LastOrDefault());
                }

                if (!_waiting())
                {
                    return;
                }

                _waiting = null;
            }

            if (Routines.Count == 0)
            {
                WriteReport();
                EditorApplication.isPlaying = false;
                return;
            }

            IEnumerator routine = Routines.Peek();
            if (!routine.MoveNext())
            {
                Routines.Pop();
            }
            else if (routine.Current is IEnumerator nested)
            {
                Routines.Push(nested);
            }
            else if (routine.Current is Func<bool> wait)
            {
                _waiting = wait;
                _waitDeadline = EditorApplication.timeSinceStartup + 15;
            }
            else if (routine.Current is float seconds)
            {
                _next = EditorApplication.timeSinceStartup + seconds;
            }
        }
        catch (Exception e)
        {
            Check(false, e.ToString());
            WriteReport();
            EditorApplication.isPlaying = false;
        }
    }

    private static IEnumerator Scenarios()
    {
        _boss = Object.FindObjectOfType<BossBrain>();
        _player = Object.FindObjectOfType<PlayerStateMachine>();
        _boss.SetAutomatic(false);
        foreach (var enemy in Object.FindObjectsOfType<EnemyStateMachine>())
        {
            enemy.enabled = false;
            enemy.GetComponent<EnemyMotor>().Stop();
        }

        _player.InputReader.enabled = false;
        _player.Health.SetMaxHealth(1000);
        _player.Health.ReviveFull();
        yield return .3f;

        ValidateStompFootprint();

        if (SessionState.GetBool(Running + ".HeldReplayOnly", false))
        {
            yield return ValidateImmediateHeldReplay();
            yield break;
        }

        yield return ValidateStompSkills();
        yield return ValidateBeforePickupInterruptions();

        foreach (float distance in new[] { 2.2f, 5f, 7f })
        {
            _boss.ResetForCheckpoint();
            _boss.SetAutomatic(false);
            Place(_boss.transform.position + _boss.transform.forward * distance);
            yield return .3f;
            int before = _player.Health.CurrentHealth;
            Check(_boss.TryForceSkill("attack_jumpAtk"), "Jump attack starts at " + distance + "m");
            yield return new Func<bool>(() => _boss.Runner.Released);
            var area = _boss.GetComponent<BossDamageArea>();
            var feet = _player.GetComponent<CharacterController>().bounds;
            Vector3 point = feet.center;
            point.y = feet.min.y;
            var groundMethod = typeof(BossDamageArea).GetMethod("GroundHeight", BindingFlags.Instance | BindingFlags.NonPublic);
            float bossGround = (float)groundMethod.Invoke(area, new object[] { _boss.transform.position + Vector3.up, _boss.transform.position.y });
            float playerGround = (float)groundMethod.Invoke(area, new object[] { point + Vector3.up * .15f, float.NegativeInfinity });
            Vector3 queryCenter = _boss.transform.position;
            queryCenter.y = bossGround + .3f;
            var all = Physics.OverlapSphere(queryCenter, 8, 1, QueryTriggerInteraction.Ignore);
            var stored = (Collider[])typeof(BossDamageArea).GetField("_targetOverlaps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(area);
            Observations.Add("Jump query: total=" + all.Length + "; buffer=" + stored.Length + "; playerInAll=" + all.Any(c => c.GetComponentInParent<PlayerHealth>() == _player.Health) + "; playerInBuffer=" + stored.Any(c => c != null && c.GetComponentInParent<PlayerHealth>() == _player.Health));
            Observations.Add("Jump " + distance + "m: loss=" + (before - _player.Health.CurrentHealth) + "; player=" + _player.transform.position + "; feet=" + point + "; boss=" + _boss.transform.position + "; ground=" + bossGround + "/" + playerGround + "; invincible=" + _player.Health.IsInvincible);
            Check(before - _player.Health.CurrentHealth == 40, "Jump landing damages grounded player at " + distance + "m");
            yield return new Func<bool>(() => !_boss.Runner.IsRunning);
        }

        if (!SessionState.GetBool(Running + ".Baseline", false))
        {
            yield return ValidateGroundWaveAvoidance();
        }

        // The large stationary receiver tests per-strike deduplication independently of knockback or positioning.
        var receiverObject = new GameObject("BossCombat_StationaryReceiver", typeof(BoxCollider), typeof(Day11ValidationTarget));
        var box = receiverObject.GetComponent<BoxCollider>();
        box.size = new Vector3(12, 8, 12);
        var receiver = receiverObject.GetComponent<Day11ValidationTarget>();
        Physics.IgnoreCollision(box, _player.GetComponent<CharacterController>());
        foreach (string id in new[] { "attack01", "attack02", "attack03", "attack04", "attack_DashAtk", "attack_whirlwind" })
        {
            _boss.ResetForCheckpoint();
            _boss.SetAutomatic(false);
            Place(_boss.transform.position + _boss.transform.forward * 6);
            receiverObject.transform.position = _boss.transform.position + Vector3.up * 2;
            receiver.ResetHits();
            Physics.SyncTransforms();
            yield return .15f;
            Check(_boss.TryForceSkill(id), id + " starts");
            yield return new Func<bool>(() => !_boss.Runner.IsRunning);
            Observations.Add(id + ": hits=" + receiver.HitCount + "; damage=" + receiver.TotalDamage);
            int expected = id == "attack02" || id == "attack_DashAtk" ? 2 : 1;
            Check(receiver.HitCount == expected, id + " hits once per configured strike (expected " + expected + ")");
            var skill = _boss.Skills.Single(s => s.Id == id);
            Check(receiver.TotalDamage == skill.Damage * expected, id + " keeps original damage per strike");
        }

        Object.Destroy(receiverObject);
        _boss.ResetForCheckpoint();
        _boss.SetAutomatic(false);
        Place(_boss.transform.position + _boss.transform.forward * 14);
        yield return .2f;
        Check(_boss.TryForceSkill("attack_throwstone"), "Throw starts");
        yield return .4f;
        Check(!OwnedStones().Any(), "Throw starts with empty hands");
        var throwSkill = _boss.Blackboard.CurrentSkill;
        yield return new Func<bool>(() => _boss.Runner.NormalizedTime >= throwSkill.ProjectilePickup - .04f);
        Check(!OwnedStones().Any(), "No stone exists while the hand is still digging");
        yield return new Func<bool>(() => OwnedStones().Any());
        var stones = OwnedStones();
        Observations.Add("Throw pickup: time=" + _boss.Runner.NormalizedTime + "; stones=" + stones.Length + "; released=" + _boss.Runner.Released);
        Check(stones.Length == 1 && !_boss.Runner.Released && _boss.Runner.NormalizedTime >= throwSkill.ProjectilePickup,
            "Exactly one held stone appears after digging finishes");
        if (!SessionState.GetBool(Running + ".Baseline", false))
        {
            var stone = stones.Single();
            int instance = stone.GetInstanceID();
            Vector3 originalScale = stone.transform.lossyScale;
            Check(!_boss.Runner.Released && _boss.Runner.LiveProjectileCount == 0 && !stone.IsLaunched, "Held stone is not counted as a flying projectile");
            yield return .2f;
            Check(stone != null && !stone.Resolved && !stone.GetComponent<SphereCollider>().enabled, "Picked up stone survives with collision disabled");
            Check(stone.transform.IsChildOf(_boss.transform) && stone.GetComponent<Renderer>().enabled, "Held stone follows the animated hand and remains visible");
            Check(Vector3.Distance(stone.transform.lossyScale, originalScale) < .001f, "Animated hand parenting preserves stone world size");
            yield return new Func<bool>(() => _boss.Runner.Released);
            Check(stone != null && stone.GetInstanceID() == instance && stone.IsLaunched && stone.transform.parent == null && stone.GetComponent<SphereCollider>().enabled, "Release launches the same instance and enables collision");
            yield return new Func<bool>(() => stone == null || stone.Resolved);
            Check(_boss.Runner.LiveProjectileCount == 0, "Released stone resolves through its original collision lifecycle");
            _boss.SetAutomatic(false);

            foreach (string interruption in new[] { "abort", "death", "checkpoint", "disable" })
            {
                _boss.ResetForCheckpoint();
                _boss.SetAutomatic(false);
                Place(_boss.transform.position + _boss.transform.forward * 14);
                Observations.Add("Before replay " + interruption + ": " + AnimationSnapshot());
                Check(_boss.TryForceSkill("attack_throwstone"), "Held stone interruption setup: " + interruption);
                Observations.Add("After replay " + interruption + ": " + AnimationSnapshot());
                yield return .2f;
                Observations.Add("Windup replay " + interruption + ": " + AnimationSnapshot());
                Check(!_boss.Runner.Released && !OwnedStones().Any(), "Immediate replay starts empty before pickup: " + interruption);
                yield return new Func<bool>(() => OwnedStones().Any());
                Check(OwnedStones().Length == 1 && !OwnedStones()[0].IsLaunched, "Replay creates one stone at pickup: " + interruption);
                if (interruption == "death")
                {
                    _boss.GetComponent<EnemyHealth>().TakeDamage(new DamageInfo { Damage = 99999 });
                }
                else if (interruption == "checkpoint")
                {
                    _boss.ResetForCheckpoint();
                }
                else if (interruption == "disable")
                {
                    _boss.Runner.enabled = false;
                }
                else
                {
                    _boss.SetAutomatic(false);
                }

                yield return .1f;
                Check(!Object.FindObjectsOfType<BossRockProjectile>().Any(s => s.Owner == _boss.gameObject), "No held stone survives " + interruption);
                _boss.Runner.enabled = true;
            }
        }

        yield return ValidateImmediateHeldReplay();
        _boss.SetAutomatic(false);
        _boss.Runner.ClearProjectiles();
    }

    private static IEnumerator ValidateStompSkills()
    {
        foreach (string id in new[] { "attack_foot_left", "attack_foot_right" })
        {
            var skill = _boss.Skills.Single(s => s.Id == id);
            float side = skill.Side == BossSkillSide.Left ? -1 : 1;
            foreach (bool matchingSide in new[] { true, false })
            {
                _boss.ResetForCheckpoint();
                _boss.SetAutomatic(false);
                float offset = matchingSide ? side * 2.2f : -side * 2.2f;
                Place(_boss.transform.position + _boss.transform.right * offset);
                yield return .2f;
                int health = _player.Health.CurrentHealth;
                Check(_boss.TryForceSkill(id), "Real stomp starts: " + id + " matchingSide=" + matchingSide);
                yield return new Func<bool>(() => _boss.Runner.Released);
                int expected = matchingSide ? skill.Damage : 0;
                Check(health - _player.Health.CurrentHealth == expected,
                    "Real stomp only damages its own side: " + id + " loss=" + (health - _player.Health.CurrentHealth));
                var cue = Object.FindObjectsOfType<LineRenderer>().Single(line => line.name == "GolemGroundPulse");
                Check(cue.positionCount == 4 && cue.loop, "Stomp cue is a closed rectangle: " + id);
                Vector3[] corners = new Vector3[4];
                cue.GetPositions(corners);
                Vector3 firstEdge = corners[1] - corners[0];
                Vector3 secondEdge = corners[2] - corners[1];
                Check(Mathf.Abs(firstEdge.magnitude - skill.Radius * 2) < .01f &&
                    Mathf.Abs(secondEdge.magnitude - skill.Radius) < .01f &&
                    Mathf.Abs(Vector3.Dot(firstEdge.normalized, secondEdge.normalized)) < .001f,
                    "Stomp cue dimensions match damage footprint: " + id);
                yield return new Func<bool>(() => !_boss.Runner.IsRunning);
            }
        }
    }

    private static IEnumerator ValidateBeforePickupInterruptions()
    {
        foreach (string interruption in new[] { "abort", "death", "checkpoint", "disable" })
        {
            _boss.ResetForCheckpoint();
            _boss.SetAutomatic(false);
            Place(_boss.transform.position + _boss.transform.forward * 14);
            yield return .2f;
            Check(_boss.TryForceSkill("attack_throwstone"), "Pre-pickup interruption starts: " + interruption);
            yield return .2f;
            Check(!OwnedStones().Any(), "Pre-pickup hands remain empty: " + interruption);
            if (interruption == "death")
            {
                _boss.GetComponent<EnemyHealth>().TakeDamage(new DamageInfo { Damage = 99999 });
            }
            else if (interruption == "checkpoint")
            {
                _boss.ResetForCheckpoint();
            }
            else if (interruption == "disable")
            {
                _boss.Runner.enabled = false;
            }
            else
            {
                _boss.SetAutomatic(false);
            }
            yield return .1f;
            Check(!OwnedStones().Any() && !_boss.Runner.IsRunning, "Interrupt before pickup leaves no stone: " + interruption);
            _boss.Runner.enabled = true;
        }
    }

    private static void ValidateSkillWindows()
    {
        var lines = new List<string>();
        foreach (string id in GiantGolemBossAnimationAudit.AttackNames)
        {
            var skill = AssetDatabase.LoadAssetAtPath<BossSkillData>(GiantGolemBossSetup.ConfigRoot + "SO_" + id + ".asset");
            int expected = id == "attack02" || id == "attack_DashAtk" ? 2 : 1;
            if (skill.HitWindowCount != expected)
            {
                throw new InvalidOperationException("Unexpected strike count for " + id);
            }

            if (expected == 2)
            {
                if (skill.GetHitWindowIndex(.35f) != 0 || skill.GetHitWindowIndex(.5f) != -1 || skill.GetHitWindowIndex(.62f) != 1)
                {
                    throw new InvalidOperationException("Invalid separate strike windows for " + id);
                }
            }
            else if (skill.GetHitWindowIndex((skill.HitStart + skill.HitEnd) / 2) != 0)
            {
                throw new InvalidOperationException("Original single strike window changed for " + id);
            }

            lines.Add("PASS: " + id + " has " + expected + " independent strike window(s)");
        }

        File.WriteAllLines(LogDirectory + "SkillWindowValidation.txt", lines);
    }

    private static IEnumerator ValidateImmediateHeldReplay()
    {
        _boss.ResetForCheckpoint();
        _boss.SetAutomatic(false);
        Place(_boss.transform.position + _boss.transform.forward * 14);
        yield return .2f;
        Check(_boss.TryForceSkill("attack_throwstone"), "First throw starts for immediate replay");
        yield return new Func<bool>(() => _boss.Runner.Released);
        _boss.ResetForCheckpoint();
        _boss.SetAutomatic(false);
        Observations.Add("Before immediate replay: " + AnimationSnapshot());
        Check(_boss.TryForceSkill("attack_throwstone"), "Immediate replay starts");
        Observations.Add("After immediate replay: " + AnimationSnapshot());
        yield return .2f;
        Observations.Add("Windup immediate replay: " + AnimationSnapshot());
        Check(!_boss.Runner.Released && !OwnedStones().Any(), "Immediate replay uses new animation time and remains empty before pickup");
        yield return new Func<bool>(() => OwnedStones().Any());
        Check(OwnedStones().Length == 1 && !OwnedStones()[0].IsLaunched, "Immediate replay creates one held stone after pickup");
        _boss.Runner.Abort();
        yield return .1f;
        Check(!Object.FindObjectsOfType<BossRockProjectile>().Any(s => s.Owner == _boss.gameObject), "Immediate replay abort cleans up its held stone");
    }

    private static string AnimationSnapshot()
    {
        var animator = _boss.GetComponent<EnemyAnimator>().Animator;
        var current = animator.GetCurrentAnimatorStateInfo(0);
        var next = animator.GetNextAnimatorStateInfo(0);
        return "current=" + current.fullPathHash + "/" + current.normalizedTime + "; next=" + next.fullPathHash + "/" + next.normalizedTime + "; transition=" + animator.IsInTransition(0) + "; skillTime=" + _boss.Runner.NormalizedTime + "; released=" + _boss.Runner.Released;
    }

    private static BossRockProjectile[] OwnedStones()
    {
        return Object.FindObjectsOfType<BossRockProjectile>().Where(s => s.Owner == _boss.gameObject).ToArray();
    }

    private static void ValidateStompFootprint()
    {
        // 隔离地面与受击体，验证矩形边界和朝向，避免主场景障碍/击退影响几何断言。
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var attacker = new GameObject("StompValidation_Attacker", typeof(BossDamageArea));
        var target = new GameObject("StompValidation_Receiver", typeof(BoxCollider), typeof(Day11ValidationTarget));
        try
        {
            Vector3 origin = new Vector3(2000, 2000, 2000);
            ground.transform.position = origin - Vector3.up * .5f;
            ground.transform.localScale = new Vector3(40, 1, 40);
            attacker.transform.position = origin;
            target.GetComponent<BoxCollider>().size = new Vector3(.2f, 1, .2f);
            var receiver = target.GetComponent<Day11ValidationTarget>();
            var area = attacker.GetComponent<BossDamageArea>();
            var hitTargets = new HashSet<IDamageable>();
            foreach (float angle in new[] { 0f, 65f })
            {
                attacker.transform.rotation = Quaternion.Euler(0, angle, 0);
                foreach (var side in new[] { BossSkillSide.Left, BossSkillSide.Right })
                {
                    float sign = side == BossSkillSide.Left ? -1 : 1;
                    Vector3 foot = origin + attacker.transform.right * sign + attacker.transform.forward * .5f;
                    var cases = new[]
                    {
                        new Vector3(sign * 2, 0, .5f),
                        new Vector3(-sign * 2, 0, .5f),
                        new Vector3(sign * 5.5f, 0, 6),
                        new Vector3(sign * 6.5f, 0, .5f),
                        new Vector3(sign * 2, 0, 7)
                    };
                    for (int i = 0; i < cases.Length; i++)
                    {
                        target.transform.position = origin + attacker.transform.rotation * cases[i] + Vector3.up * .5f;
                        receiver.ResetHits();
                        hitTargets.Clear();
                        Physics.SyncTransforms();
                        area.Stomp(foot, 6, side, 32, hitTargets);
                        bool inside = i == 0 || i == 2;
                        Check(receiver.HitCount == (inside ? 1 : 0) && receiver.TotalDamage == (inside ? 32 : 0),
                            "Stomp rectangle " + side + " angle=" + angle + " case=" + i + " expectedInside=" + inside);
                        area.Stomp(foot, 6, side, 32, hitTargets);
                        Check(receiver.HitCount == (inside ? 1 : 0), "Stomp deduplicates receivers across colliders/repeated queries");
                    }
                }
            }
        }
        finally
        {
            Object.Destroy(target);
            Object.Destroy(attacker);
            Object.Destroy(ground);
        }
    }

    private static IEnumerator ValidateGroundWaveAvoidance()
    {
        _boss.ResetForCheckpoint();
        _boss.SetAutomatic(false);
        Place(_boss.transform.position + _boss.transform.forward * 5);
        yield return .2f;
        _player.Motor.Jump();
        _player.ChangeState(_player.AirborneState);
        yield return new Func<bool>(() => _player.GetComponent<CharacterController>().bounds.min.y > _boss.transform.position.y + .85f);
        int health = _player.Health.CurrentHealth;
        _boss.GetComponent<BossDamageArea>().Pulse(_boss.transform.position, 8, 40, new HashSet<IDamageable>());
        Check(_player.Health.CurrentHealth == health, "Player above the ground-wave height remains safe");
        yield return new Func<bool>(() => _player.Motor.IsGrounded);
        Place(_boss.transform.position + _boss.transform.forward * 5);
        yield return .2f;
        _player.Stamina.RestoreFull();
        _player.ChangeState(_player.DodgeState);
        yield return new Func<bool>(() => _player.Health.IsInvincible);
        health = _player.Health.CurrentHealth;
        _boss.GetComponent<BossDamageArea>().Pulse(_boss.transform.position, 8, 40, new HashSet<IDamageable>());
        Check(_player.Health.CurrentHealth == health, "Dodge invincibility remains safe from the ground wave");
        yield return new Func<bool>(() => !_player.Health.IsInvincible);
    }

    private static void Place(Vector3 desired)
    {
        _player.Health.ReviveFull();
        _player.Health.DisableIFrame();
        _player.Respawn();
        if (!NavMesh.SamplePosition(desired, out NavMeshHit sample, 2, NavMesh.AllAreas))
        {
            throw new InvalidOperationException("No walkable player placement at " + desired);
        }

        _player.Motor.Teleport(sample.position + Vector3.up * .05f, Quaternion.identity);
        _player.InputReader.ClearPendingActions();
        Physics.SyncTransforms();
    }

    private static void Check(bool passed, string message)
    {
        Checks.Add((passed ? "PASS: " : "FAIL: ") + message);
        WriteReport();
    }

    private static void WriteReport()
    {
        string mode = SessionState.GetBool(Running + ".Baseline", false) ? "Baseline" : "Validation";
        if (SessionState.GetBool(Running + ".HeldReplayOnly", false))
        {
            mode = "HeldReplay";
        }
        File.WriteAllLines(LogDirectory + mode + "Checks.txt", Checks);
        File.WriteAllLines(LogDirectory + mode + "Observations.txt", Observations);
    }

    private static void RecordLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying)
        {
            return;
        }

        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            _errors++;
        }
        else if (type == LogType.Warning)
        {
            _warnings++;
        }

        if (type != LogType.Log)
        {
            File.AppendAllText(LogDirectory + "RuntimeConsole.txt", type + ": " + message + "\n" + stack + "\n");
        }
    }
}
