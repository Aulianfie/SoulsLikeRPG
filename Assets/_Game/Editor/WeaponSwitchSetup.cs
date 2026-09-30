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
        var machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "WeaponSwitch");
        if (state == null) state = machine.AddState("WeaponSwitch", new Vector3(620, 350));
        state.motion = clip;
        state.speed = 1.4f;
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        File.WriteAllText("Logs/WeaponSwitch_Setup.txt", $"Shared state=Base Layer.WeaponSwitch\nClip={ClipPath}\nSeconds={clip.length}\nSpeed={state.speed}\nAll slots use the same action; no per-weapon animation overrides.\n");
    }
}
