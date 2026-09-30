# Day10：统一换武器动作

日期：2026-10-01；Unity 2022.3.62f3c1。

## 动作选择

检查用户本地 `G:/BaiduNetdiskDownload/DoubleL1/Animation` 的九个主要动画包，发现单手 / 双手之间的专用动作，以及不指定目标武器的右手换武器动作。按用户偏好，长剑和大剑统一采用 `1Hand_Base_Weapon_Change_R_2`，不为两种切换方向分别配置动画。

- 来源：`RPGAnimations - One Hand Base1.11.unitypackage`，DoubleL，用户已有资源；本次无下载。许可沿用用户已有资产授权，不推定 CC0。
- 原路径：`Assets/DoubleL/FBX_Animations/One Hand Base/Weapon Change/1Hand_Base_Weapon_Change_R_2.fbx`。
- 正式路径：`Assets/ThirdParty/DoubleL/Animations/WeaponSwitch/1Hand_Base_Weapon_Change_R_2.fbx`，原 GUID `cc364f99de28696408d0c47eb284d8cc`。
- 片段约 1.167 秒，Humanoid、非循环，使用现有有效 DoubleL T-Pose Avatar，Root Motion 烘焙为原地。当前 UAL1 玩家已做姿势采样与两个方向的实际播放检查。
- 七个未采用的候选 FBX / meta 移出 Assets，保留在 `Logs/WeaponSwitchTools/Candidates/`；候选姿势图和检查源码也归档于 Logs，不参与构建。

## 已接入行为

- `AC_Player.controller` 新增唯一的 `Base Layer.WeaponSwitch`，Speed = 1.4，完整动作约 0.8 秒。两把剑的 Animator Override 共用该状态与片段，攻击覆盖继续独立。
- `PlayerEquipment` 的 `Shared Weapon Switch` 参数：Hide 0.38、Equip 0.50、Show 0.65、Completion 0.93（均为动画归一化位置）。初始装备仍立即显示；游戏内切换在 Equip 点更新装备 / 模型 / 攻击配置，触发一次 `WeaponChanged`。图标事件跟随实际装备变化。
- `PlayerWeaponSwitchState` 关闭攻击窗口，停止移动，播放共同动作；旧武器在伸手到肩后时隐藏，切换后在抽回手时显示新武器。两把模型继续预创建并复用。
- 动作期间丢弃攻击、跳跃、使用物品、交互和重复切换输入；保持仅地面移动状态能开始动画切换。闪避、受击、死亡或离地可以打断；Equip 点之前保留旧武器，之后保留新武器；退出均恢复显示、清除待切换请求。
- 未配置该动画状态的旧控制器继续采用原即时切换；没有扩展背包、背部挂载模型或刀鞘系统。

## 验证与保护

- `Docs/Day10_WeaponSwitch_RuntimeValidation.json`：67 项实际 Play Mode 验收通过。包含真实滚轮 / 左键 / 键盘输入、两个方向相同片段、准确显隐与换装点、换装后长剑五段 / 大剑三段、重复输入拦截、命中窗口关闭、受击 / 闪避中断前后的装备、暂停、组件禁用、死亡复活和 R 喝药显隐恢复。
- `Docs/Day10_WeaponSwitch_AssetValidation.txt`：场景 / 玩家预制体无 Missing Script，双槽 / 模型 / 连招 / 动画覆盖 / 命中检测引用有效，原十个输入动作保留。
- `Docs/Day10_WeaponSwitch_GreatSword.png`、`Day10_WeaponSwitch_LongSword.png`：主场景运行中抽回手时的实际角色截图，已检查。
- `Docs/Day10_WeaponSwitch_FinalCompilation.txt`：临时工具移出后的 Unity 编译及 Console 状态；未生成独立 Windows 构建。
- `Docs/Day10_WeaponSwitch_Preservation.json`：玩家预制体和大剑三段参数保持原哈希；本次验收前的当前场景副本与完成后的场景文件一致。任务开始后另一个界面任务更新了场景、图标和 HUD，本任务保留这些修改，没有保存或覆盖主场景。
- 用户存档恢复字节一致，SHA-256 `8457BADFED8BDDDF4EC7DE3A0B1E2DDF69B56158EB5411C493CB96B439347E6E`。未安装软件，未修改 Packages、ProjectSettings、系统配置或另一个 `RPG` 项目。

早期验收曾被同时启动的界面验收请求打断；其后排除了测试中体力自然恢复和复活淡入期间输入尚未启用的前置条件干扰。失败记录与原因保留在 `Logs/WeaponSwitchTools/`，最终 67 项验收通过；这些测试问题未通过改动运行逻辑绕过。

## 调整位置

改变整体动作速度：在 `AC_Player.controller` 的 WeaponSwitch 状态调整 Speed；改变实际换装和显隐时机：在玩家 `PlayerEquipment` 的 Shared Weapon Switch 中调整四个时间点。`Tools > SoulsLike RPG > Day10 > Configure Shared Weapon Switch` 只配置共同动画状态，不修改场景、玩家预制体、武器图标或连招参数。
