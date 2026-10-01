using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WeaponSwitchSetup
{
    public const string ClipPath = "Assets/ThirdParty/DoubleL/Animations/WeaponSwitch/1Hand_Base_Weapon_Change_R_2.fbx";
    [MenuItem("Tools/SoulsLike RPG/Day10/Configure Shared Weapon Switch")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play Mode。");
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
        if (!clip.humanMotion || clip.isLooping) throw new InvalidOperationException("需要非循环 Humanoid 换武器动作。");
        const string path = "Assets/_Game/Animations/Controllers/AC_Player.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/_Game/Animations/Masks/AM_ItemUse_UpperBody.mask");
        if (controller == null || mask == null) throw new InvalidOperationException("缺少玩家控制器或上半身遮罩。");
        // 将旧版全身切换迁移到独立层；Base Layer 继续播放走路 / 奔跑。
        var baseMachine = controller.layers[0].stateMachine;
        var oldState = baseMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "WeaponSwitch");
        if (oldState != null) baseMachine.RemoveState(oldState);
        if (!controller.parameters.Any(p => p.name == "WeaponSwitchSpeed"))
            controller.AddParameter("WeaponSwitchSpeed", AnimatorControllerParameterType.Float);
        if (!controller.layers.Any(l => l.name == "WeaponSwitch")) controller.AddLayer("WeaponSwitch");
        var layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == "WeaponSwitch");
        var layer = layers[index];
        layer.avatarMask = mask;
        layer.defaultWeight = 0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        var machine = layer.stateMachine;
        var empty = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Empty")
            ?? machine.AddState("Empty", new Vector3(250, 80));
        empty.motion = null;
        empty.writeDefaultValues = true;
        machine.defaultState = empty;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Switch")
            ?? machine.AddState("Switch", new Vector3(500, 80));
        state.motion = clip;
        state.speed = 1f;
        state.speedParameterActive = true;
        state.speedParameter = "WeaponSwitchSpeed";
        state.writeDefaultValues = true;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        GameObject prefab = PrefabUtility.LoadPrefabContents(Day10Setup.PlayerPath);
        try
        {
            var values = new SerializedObject(prefab.GetComponent<PlayerAnimator>());
            values.FindProperty("_weaponSwitchClip").objectReferenceValue = clip;
            values.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, Day10Setup.PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        File.WriteAllText("Logs/WeaponSwitch_Setup.txt", $"Shared state=WeaponSwitch.Switch\nMask=AM_ItemUse_UpperBody\nClip={ClipPath}\nSource seconds={clip.length}\nDuration=PlayerEquipment.SwitchDuration (default 0.4s)\nSpeed parameter=WeaponSwitchSpeed\nBase layer and locomotion speed are preserved.\n");
    }
}
