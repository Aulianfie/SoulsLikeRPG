using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Day11Setup
{
    public const string Folder = "Assets/_Game/Configs/Weapons";
    public const string ControllerPath = "Assets/_Game/Animations/Controllers/AC_Player.controller";

    [MenuItem("Tools/SoulsLike RPG/Day11/1 Migrate Weapon Movesets")]
    public static void MigrateMovesets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play Mode。");
        Directory.CreateDirectory("Logs/Day11");
        string[] protectedPaths = Directory.GetFiles(Folder, "*.asset")
            .Where(p => Path.GetFileName(p).StartsWith("AD_") || Path.GetFileName(p).StartsWith("Combo_")).ToArray();
        byte[][] original = protectedPaths.Select(File.ReadAllBytes).ToArray();
        foreach (string id in new[] { "LongSword", "GreatSword" })
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{Folder}/WD_{id}.asset");
            if (weapon == null || weapon.LightAttackCombo == null) throw new InvalidOperationException("Missing light combo: " + id);
            AttackCombo combo = weapon.LightAttackCombo;
            var moveset = LoadOrCreate<WeaponMoveset>($"{Folder}/WM_{id}.asset");
            var data = new SerializedObject(moveset);
            data.FindProperty("_lightCombo").objectReferenceValue = combo;
            data.ApplyModifiedPropertiesWithoutUndo();
            var definition = new SerializedObject(weapon);
            definition.FindProperty("_moveset").objectReferenceValue = moveset;
            definition.ApplyModifiedPropertiesWithoutUndo();
            if (weapon.Moveset.LightCombo != combo) throw new InvalidOperationException("Combo reference changed: " + id);
        }
        AssetDatabase.SaveAssets();
        for (int i = 0; i < protectedPaths.Length; i++)
            if (!original[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i])))
                throw new InvalidOperationException("Existing combo/attack asset changed: " + protectedPaths[i]);
        File.WriteAllText("Logs/Day11/migration_assets.txt", "PASS: Both Movesets refer to the original LightCombos. Existing combo and attack asset bytes unchanged.\n");
        Debug.Log("[Day11] MOVESET_MIGRATION_COMPLETE");
    }

    public static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    [MenuItem("Tools/SoulsLike RPG/Day11/2 Configure Selected Actions")]
    public static void ConfigureActions()
    {
        MigrateMovesets();
        const string actionFolder = "Assets/_Game/Animations/Day11";
        if (!AssetDatabase.IsValidFolder(actionFolder)) AssetDatabase.CreateFolder("Assets/_Game/Animations", "Day11");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        var originalStates = machine.states.ToDictionary(s => s.state, s => s.state.motion);
        var layers = controller.layers.Select(l => l.name).ToArray();
        var controllers = new[] { "LongSword", "GreatSword" }.Select(id =>
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>($"Assets/_Game/Animations/Controllers/AOC_{id}.overrideController")).ToArray();
        var previousOverrides = controllers.Select(c =>
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            c.GetOverrides(pairs); return pairs;
        }).ToArray();
        AnimationClip longJump = CopyMotion("LongSword/JumpAttack/1Hand_Base_Jump_Attack_1_InPlace.fbx", "Jump_LongSword");
        AnimationClip greatJump = CopyMotion("GreatSword/JumpAttack/2Hand_Up_Jump_Attack_InPlace.fbx", "Jump_GreatSword");
        AnimationClip longSkill = CopyMotion("LongSword/WeaponSkill/1Hand_Up_Skill_3_InPlace.fbx", "Skill_LongSword");
        AnimationClip greatSkill = CopyMotion("GreatSword/WeaponSkill/2Hand_Base_Skill_1_InPlace.fbx", "Skill_GreatSword");
        AnimationClip jumpKey = EnsureState(machine, "JumpAttack", longJump, new Vector3(600, -100, 0));
        AnimationClip skillKey = EnsureState(machine, "WeaponSkill", longSkill, new Vector3(600, 100, 0));
        for (int i = 0; i < controllers.Length; i++)
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controllers[i].GetOverrides(pairs);
            for (int j = 0; j < pairs.Count; j++)
            {
                if (pairs[j].Key == jumpKey) pairs[j] = new KeyValuePair<AnimationClip, AnimationClip>(jumpKey, i == 0 ? longJump : greatJump);
                if (pairs[j].Key == skillKey) pairs[j] = new KeyValuePair<AnimationClip, AnimationClip>(skillKey, i == 0 ? longSkill : greatSkill);
            }
            controllers[i].ApplyOverrides(pairs);
            EditorUtility.SetDirty(controllers[i]);
            foreach (var old in previousOverrides[i])
                if (pairs.Single(p => p.Key == old.Key).Value != old.Value)
                    throw new InvalidOperationException("Existing override changed: " + old.Key.name);
        }
        ConfigureMoveset("LongSword", BuildAction("AD_LongSword_Jump", "JumpAttack", 35, 25, 0, .32f, .85f, .90f, .12f, .08f),
            BuildAction("AD_LongSword_Skill", "WeaponSkill", 45, 0, 20, .36f, .60f, .90f, .12f, .18f));
        ConfigureMoveset("GreatSword", BuildAction("AD_GreatSword_Jump", "JumpAttack", 35, 22, 0, .33f, .60f, .78f, .16f, .06f),
            BuildAction("AD_GreatSword_Skill", "WeaponSkill", 55, 0, 35, .33f, .62f, .90f, .20f, .20f));
        foreach (var original in originalStates)
            if (original.Key.motion != original.Value) throw new InvalidOperationException("Existing Animator motion changed: " + original.Key.name);
        if (!layers.SequenceEqual(controller.layers.Select(l => l.name))) throw new InvalidOperationException("Animator layers changed");
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/Day11/actions_assets.txt", "PASS: Selected Humanoid clips configured; original state motions, overrides and layer names preserved.\n");
        Debug.Log("[Day11] ACTION_ASSET_SETUP_COMPLETE");
    }

    private static AnimationClip CopyMotion(string candidate, string name)
    {
        string path = $"Assets/_Game/Animations/Day11/{name}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            AnimationClip source = Day11AnimationReview.Clip(Day11AnimationReview.Folder + "/" + candidate);
            clip = UnityEngine.Object.Instantiate(source);
            clip.name = name;
            AssetDatabase.CreateAsset(clip, path);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = settings.keepOriginalPositionY = true;
        settings.heightFromFeet = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        if (!clip.humanMotion) throw new InvalidOperationException("Action is not Humanoid: " + name);
        return clip;
    }

    private static AnimationClip EnsureState(AnimatorStateMachine machine, string name, AnimationClip motion, Vector3 position)
    {
        AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        if (state == null)
        {
            state = machine.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = machine.states.Select(s => s.state).First(s => s.name == "Attack1").writeDefaultValues;
        }
        EditorUtility.SetDirty(state);
        return state.motion as AnimationClip ?? throw new InvalidOperationException("Action state needs an AnimationClip: " + name);
    }

    private static AttackData BuildAction(string name, string state, int damage, float stamina, float mana,
        float hitStart, float hitEnd, float completion, float recovery, float rotate)
    {
        var attack = LoadOrCreate<AttackData>($"{Folder}/{name}.asset");
        var data = new SerializedObject(attack);
        data.FindProperty("_animationStateName").stringValue = state;
        data.FindProperty("_damage").intValue = damage;
        data.FindProperty("_staminaCost").floatValue = stamina;
        data.FindProperty("_manaCost").floatValue = mana;
        data.FindProperty("_hitWindowStart").floatValue = hitStart;
        data.FindProperty("_hitWindowEnd").floatValue = hitEnd;
        data.FindProperty("_completionNormalizedTime").floatValue = completion;
        data.FindProperty("_recoveryTime").floatValue = recovery;
        data.FindProperty("_rotateAssistTime").floatValue = rotate;
        data.FindProperty("_comboInputStart").floatValue = 0;
        data.FindProperty("_comboInputEnd").floatValue = 0;
        data.FindProperty("_dodgeCancelStart").floatValue = 0;
        data.FindProperty("_dodgeCancelEnd").floatValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        return attack;
    }

    private static void ConfigureMoveset(string id, AttackData jump, AttackData skill)
    {
        var moveset = AssetDatabase.LoadAssetAtPath<WeaponMoveset>($"{Folder}/WM_{id}.asset");
        var data = new SerializedObject(moveset);
        data.FindProperty("_jumpAttack").objectReferenceValue = jump;
        data.FindProperty("_weaponSkill").objectReferenceValue = skill;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/SoulsLike RPG/Day11/Validate Configured Asset References")]
    public static void ValidateAssets()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        foreach (string id in new[] { "LongSword", "GreatSword" })
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{Folder}/WD_{id}.asset");
            var combo = AssetDatabase.LoadAssetAtPath<AttackCombo>($"{Folder}/Combo_{id}_Light.asset");
            if (weapon.Moveset == null || weapon.Moveset.LightCombo != combo || combo.Count != (id == "LongSword" ? 5 : 3))
                throw new InvalidOperationException("LightCombo reference mismatch: " + id);
            foreach (var data in new[] { weapon.Moveset.JumpAttack, weapon.Moveset.WeaponSkill })
            {
                if (data == null || data.HitWindowStart >= data.HitWindowEnd || data.HitWindowEnd > data.CompletionNormalizedTime)
                    throw new InvalidOperationException("Invalid attack data: " + id);
                var state = controller.layers[0].stateMachine.states.Select(s => s.state).Single(s => s.name == data.AnimationStateName);
                var motion = weapon.AnimatorOverrideController[state.motion.name];
                var settings = AnimationUtility.GetAnimationClipSettings(motion);
                if (motion == null || !motion.humanMotion || settings.loopTime || !settings.loopBlendOrientation || !settings.loopBlendPositionY || !settings.loopBlendPositionXZ)
                    throw new InvalidOperationException("Invalid Humanoid action or root bake settings: " + id);
                string expected = (data == weapon.Moveset.JumpAttack ? "Jump_" : "Skill_") + id;
                if (motion.name != expected) throw new InvalidOperationException("Wrong Override: " + expected);
            }
        }
        string[] layers = controller.layers.Select(l => l.name).ToArray();
        if (!layers.SequenceEqual(new[] { "Base Layer", "ItemUse", "WeaponSwitch" })) throw new InvalidOperationException("Unexpected layers");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/Player_Day1.prefab");
        var animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator.applyRootMotion || !animator.avatar.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("Invalid Player rig/root motion");
        File.WriteAllText("Logs/Day11/final_assets.txt", "PASS: Both original LightCombos, all four action data/Override/Humanoid/root-bake references and Player prefab rig validated.\n");
        Debug.Log("[Day11] FINAL_ASSET_VALIDATION_PASS");
    }
}
