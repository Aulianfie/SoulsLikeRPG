# Day10：统一换武器动作与移动叠加

日期：2026-10-01；Unity 2022.3.62f3c1。

## 当前行为

长剑和大剑统一使用肩后取武器动作 `1Hand_Base_Weapon_Change_R_2`，默认切换时长 **0.4 秒**。切换时保持正常移动和奔跑速度；持续按住移动 / 奔跑键，切换结束后无需重新按键。

Unity 通过独立 Animator Layer + Avatar Mask 实现类似上半身蒙太奇的叠加：`Base Layer.Locomotion` 持续播放移动，`WeaponSwitch.Switch` 覆盖上半身，遮罩排除 Root 和双腿。复用现有 `AM_ItemUse_UpperBody.mask`，没有修改喝药遮罩。动作末尾逐渐降低层权重，退出后归零，避免重播基础移动状态或重置步态。

## 配置位置

选择场景中的 `Player_Day1`，在 **PlayerEquipment > Shared Weapon Switch > Switch Duration** 调整秒数，默认 `0.4`，最小 `0.1`。修改 `Assets/_Game/Prefabs/Characters/Player_Day1.prefab` 中的同一字段可统一改变默认值。

| 字段 | 当前值 | 含义 |
| --- | --- | --- |
| Switch Duration | 0.4 秒 | 从开始切换到恢复可攻击的目标时长 |
| Switch Hide Point | 0.38 | 伸手到肩后时隐藏旧武器 |
| Switch Equip Point | 0.50 | 更换武器、模型和攻击配置，触发一次 WeaponChanged |
| Switch Show Point | 0.65 | 抽回手时显示新武器 |
| Switch Completion Point | 0.93 | 结束动作并恢复攻击等操作 |

后四项为动画归一化位置。`PlayerAnimator` 根据源片段长度、Completion Point 和 Switch Duration 自动计算 `WeaponSwitchSpeed` 参数，仅调整独立层的切换状态，不改变全局 Animator 速度。以后调整快慢直接修改 **Switch Duration**，无需再改状态 Speed。

## 资产与接入

- 来源：用户已有 DoubleL `RPGAnimations - One Hand Base1.11.unitypackage`；本次无下载。沿用已有资产授权，不推定 CC0。
- 原路径：`Assets/DoubleL/FBX_Animations/One Hand Base/Weapon Change/1Hand_Base_Weapon_Change_R_2.fbx`。
- 正式路径：`Assets/ThirdParty/DoubleL/Animations/WeaponSwitch/1Hand_Base_Weapon_Change_R_2.fbx`，保留原 GUID `cc364f99de28696408d0c47eb284d8cc`。
- 源片段约 1.167 秒，Humanoid、非循环，引用现有有效 DoubleL T-Pose Avatar，根位移烘焙为原地；当前 UAL1 玩家已经做姿势采样与实际播放检查。
- 控制器：`Assets/_Game/Animations/Controllers/AC_Player.controller`；新增独立 `WeaponSwitch` 层，默认权重 0，含 `Empty` 与 `Switch`。旧版 `Base Layer.WeaponSwitch` 已迁移。两把剑共用同一片段，攻击覆盖继续独立。
- `Player_Day1.prefab` 添加片段引用和切换参数；模型、挂点、武器配置和界面引用保持原样。
- `Tools > SoulsLike RPG > Day10 > Configure Shared Weapon Switch` 可重建共同层并配置玩家预制体的片段引用，保留已有切换时长，不保存主场景。
- 未采用的七个候选和临时检查源码归档于 `Logs/WeaponSwitchTools/`，不参与构建。

切换状态使用与正常移动相同的 Motor 调用，保留移动、视角和奔跑输入；开始切换的一帧也更新移动。只清理离散动作请求，解决原来清空输入后按住 W / Shift 不再触发输入事件的问题。

动作期间关闭命中窗口，丢弃攻击、跳跃、使用物品、交互和重复切换输入。地面移动状态可开始切换；闪避、受击、死亡或离地可打断。Equip 点之前打断保留旧武器，之后保留新武器；退出恢复显示并清除待切换请求。初始装备直接应用，未配置共同动作的旧控制器保持即时切换。

## 验证证据

- `Docs/Day10_WeaponSwitch_RuntimeValidation.json`：**99 项 Play Mode 检查通过**。真实滚轮、左键、键盘输入验证两方向共享片段、显隐与换装、长剑五段 / 大剑三段、输入拦截、命中窗口关闭、受击 / 闪避中断、暂停、组件禁用、死亡复活和 R 喝药恢复。
- 默认 0.4 秒：普通静止切换测得约 0.409～0.493 秒，包含输入进入与逐帧退出的开销；移动切换分别约 **0.411 / 0.416 秒**。临时运行配置为 0.6 秒时测得 **0.610 秒**，验证后恢复 0.4，未写回运行时场景。
- 步行基线与切换期间最低速度均 **4.000 m/s**；奔跑均 **6.500 m/s**。基础移动状态持续播放，腿部动画保持变化，切换结束后持续按住输入仍有效，上半身层权重归零。
- `Docs/Day10_WeaponSwitch_AssetValidation.txt`：主场景 / 玩家预制体无 Missing Script，双槽、模型、5 / 3 段连招、动画覆盖、命中引用与原十个输入动作有效。
- `Docs/Day10_WeaponSwitch_CaptureValidation.json` 和两张 `Day10_WeaponSwitch_GreatSword.png` / `LongSword.png`：单独采集最终上半身抽回手姿势，武器显示已人工检查；截图渲染开销没有混入速度测试。
- `Docs/Day10_WeaponSwitch_FinalCompilation.txt`：临时工具清理后的实际 Unity 编译与 Console 状态；本次没有生成独立 Windows 构建。
- `Docs/Day10_WeaponSwitch_Preservation.json`：场景文件、HUD、武器 Data、用户大剑三段参数、输入资产和遮罩未改；预制体只增加换武器片段引用和切换参数。原存档恢复字节一致。编辑器快照与场景文件仅 Cinemachine 自动生成 TopRig 的预览旋转不同，没有保存场景或改写相机配置。

原全身 0.8 秒版本的 67 项验收留存于 `Logs/WeaponSwitchTools/FullBody_PreOverlayValidation.json`。上半身版本首次测试遇到动画采样与脚本更新相差一帧的显隐断言问题，改为等待实际显隐阶段后最终 99 项通过；未通过修改运行逻辑绕过测试。未安装软件，未修改系统配置、Packages、ProjectSettings 或另一个 RPG 项目。
