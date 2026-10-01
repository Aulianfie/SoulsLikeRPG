# Day10：武器数据化、装备与双武器切换

日期：2026-10-01。Unity 2022.3.62f3c1；主场景 `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity`。

本轮更新：恢复长剑原来的五段，五段的动画、切入时间、伤害、体力和各窗口逐项与原 AttackData 核对一致；大剑补齐第三段。历史 3 / 2 段版本的验收记录仍保留在 `Docs/Day10_RuntimeValidation.json`；五段 / 三段版本完成以下 117 项验收，后续衔接调参另有 31 项专项验收，见文末。

## 已实现

后续更新：长剑 / 大剑统一采用肩后换武器动作，默认 0.4 秒、秒数可配置，上半身叠加播放，保留正常移动和奔跑速度；动画中点更换装备与图标。新行为及 99 项专项运行验收见 `Docs/Day10_WeaponSwitch_Report.md`；下方 117 项记录对应接入换武器动作前的连招版本。

- `WeaponData` 保存 ID、显示名、武器预制体、轻攻击连招、伤害 / 体力倍率、Animator Override 和武器类型。无运行时状态。
- `PlayerEquipment` 管理两个预创建的武器模型、当前槽位和 `WeaponChanged` 事件。切换关闭旧 Hitbox、重置连招 / 攻击缓存、更新当前 Hitbox 与动画覆盖；不反复 Instantiate / Destroy。
- `PlayerCombat` 继续共用原有命中窗口、连招窗口、体力、闪避取消、后摇和锁定攻击辅助。接入装备时读取当前武器配置；未配置装备的旧场景保留原 `_attackCombo` 备用行为。
- `SwitchWeapon` 使用 Input System 的 `<Mouse>/scroll/y`，滚轮向上为前一槽、向下为后一槽。按方向离散处理，与滚轮数值大小无关；当前两槽互相切换。
- 仅存活、输入启用、未暂停且处于 Locomotion 时允许切换。攻击、喝药、受击、闪避、跳跃、死亡及赐福菜单期间的请求不会留到结束后执行。
- 两把剑共用原 `AC_Player.controller`，大剑只覆盖 Attack1 / Attack2 / Attack3。移动、喝药、闪避、跳跃、受击、死亡、交互动画保持继承。
- 喝药的武器 Renderer 列表同时包含两把剑，退出或中断喝药时恢复原显示状态。

## 武器配置

| 武器 | 连招 | 实测初始伤害 | 每段体力 | 动作 / 手感 |
| --- | --- | --- | --- | --- |
| 长剑 | 5 段 | 25 / 25 / 25 / 25 / 25 | 20 / 20 / 20 / 20 / 20 | 完整恢复原有五段参数 |
| 大剑 | 3 段 | 40 / 48 / 56 | 30 / 33 / 36 | 三段独立双手动作，更长的完成点和后摇 |

伤害包含现有成长计算，再应用武器倍率；上表为测试新游戏力量等级 1。大剑伤害倍率 1.6、体力倍率 1.5；长剑均为 1。原来的五段连招及 AttackData 未修改。

主要资源：

- 玩家：`Assets/_Game/Prefabs/Characters/Player_Day1.prefab`，默认槽 0 长剑。
- 模型：`Assets/_Game/Prefabs/Weapons/Weapon_OneHand.prefab`、`Weapon_GreatSword.prefab`，共用原 RightHandWeaponSocket。
- 数据：`Assets/_Game/Configs/Weapons/WD_LongSword.asset`、`WD_GreatSword.asset`。
- 连招：同目录 `Combo_LongSword_Light.asset`、`Combo_GreatSword_Light.asset`；各自独立 `AD_LongSword_1..5`、`AD_GreatSword_1..3`。
- 动画覆盖：`Assets/_Game/Animations/Controllers/AOC_LongSword.overrideController`、`AOC_GreatSword.overrideController`。
- 接线工具：`Assets/_Game/Editor/Day10Setup.cs`；主场景与玩家预制体已完成设置，无需再次运行。重复运行设置工具会重新应用默认武器数值。

## 资产来源与导入

使用用户此前提供的 DoubleL `RPGAnimations - Two Hand Base1.10.unitypackage` 检查样本，本次未下载新资源、未整包导入。

- `SM_Wep_Sword_03.fbx`：归入 `Assets/ThirdParty/DoubleL/Models/`；项目自建 URP 钢材质和武器预制体位于 `_Game`。
- `2Hand_Base_Attack_A_1_InPlace.fbx`、`2Hand_Base_Attack_A_2_InPlace.fbx`、`2Hand_Base_Attack_A_3_InPlace.fbx`：归入 `Assets/ThirdParty/DoubleL/Animations/TwoHandBase/`，时长约 1.833 / 1.667 / 1.833 秒，Humanoid、非循环，引用现有 DoubleL T-Pose Avatar，根位移烘焙。
- 共享 Avatar 与当前 UAL1 玩家 Avatar 均有效、Humanoid；三个攻击已在主场景当前玩家实际播放。Game View 截图已检查剑模型、握持朝向与双手攻击姿势。
- 沿用用户原有资产授权；不声明该资源免费或 CC0。

## 验收证据

`Docs/Day10Combo_RuntimeValidation.json`：**117 项 Play Mode 验收通过**，使用玩家已配对键鼠的真实 Input System 事件。不是 Unity Test Runner 测试套件。

| 验收内容 | 结果 |
| --- | --- |
| 默认长剑五段 → 大剑三段 → 长剑五段 | 通过 |
| 滚轮大小 / 方向、非法槽、同槽装备、十次循环与事件次数 | 通过 |
| 模型、当前 Hitbox、动画覆盖与连招一起切换 | 通过 |
| 真实 Physics overlap 伤害、每目标每段只受伤一次 | 通过 |
| 完整连招体力扣费 100 / 99，连击缓存和结束重置 | 通过 |
| 攻击中禁止切换，命中窗口内受击与死亡关闭 Hitbox | 通过 |
| 大剑攻击在配置窗口内闪避取消，闪避 / 跳跃正常 | 通过 |
| 大剑喝药隐藏、回血时点、扣瓶、HUD、受击打断与恢复 | 通过 |
| 赐福补充与菜单暂停、关闭后恢复输入 | 通过 |
| 中键锁定、切武器保留目标、两种武器攻击朝向辅助 | 通过 |
| 自然死亡 / 淡入淡出 / 复活，复活后切换正常 | 通过 |

朝向辅助测试固定已验证可锁定的目标，临时停止 Targeting 更新以隔离场景遮挡 / 相机追踪；真实中键获取目标和切换保留目标另行验证。死亡清理测试直接进入攻击状态；鼠标点击进入攻击已由三轮完整连招验证。

- `Docs/Day10Combo_AssetValidation.txt`：主场景 / 玩家预制体无 Missing Script，两槽数据、模型、连招、Animator Override 与 Enemy Hitbox 引用完整，十个输入 Action 均存在。
- `Docs/Day10Combo_FinalEditorState.txt`：验收结束时退出 Play Mode，主场景已保存，Console **0 错误 / 0 警告**。
- `Docs/Day10Combo_FinalCompilation.txt`：移除临时测试工具后的 Unity 编译记录。
- `Docs/Day10Combo_GreatSword_GameView.png`：当前挂点下实际大剑第三段攻击截图，含编辑器原有 Gizmos 图标。
- `Docs/Day10Combo_Preservation.json`：原始用户存档与恢复档 SHA-256 一致，`075147EE614DAF56E32CDDB4355B6E92AA48C4F4D4F2663CB75DD86DA6CB6F62`。
- `Logs/Day10ComboBackup/SceneBefore.unity` 与 `PlayerBefore.prefab`：本轮实施前备份；本轮仅修改连招资产和大剑动画覆盖，场景、玩家预制体及用户挂点调整的文件哈希保持一致。
- 临时桥接、测试靶与验收探针已移出 Assets，本轮源码留在 `Logs/Day10ComboTools/`，不会参与玩家运行或构建。

重导入剑模型时，原有 `Assets/DoubleL/Model/Prefab/Example Weapon Locations/` 两个示例预制体暴露缺失盾牌 / 弓嵌套依赖，记录于 `Logs/Day10Backup/ImportConsole.txt`。这些原有示例未用于主场景或两把剑，也未修改；最终编译与运行没有相关错误。测试早期的测试靶碰撞和相机遮挡干扰已隔离，失败证据保留在 `Logs/Day10Backup/`；最终验收通过。

## 使用与范围

打开 `03_AncientDungeon_Checkpoint` 并运行：默认长剑，左键攻击，鼠标滚轮切换，大剑最多三段，长剑最多五段。保持原 R 喝药、Ctrl 闪避、中键锁定、E 交互。

数值在 `WD_*` 和对应 `AD_*` 中调整。当前装备不写入存档；重新进入游戏默认长剑，死亡复活保留本轮当前武器。未实现背包、装备 UI、强化、护甲等后续内容。

已完成编辑器编译、运行验收和资源引用检查；未生成独立 Windows 构建。未安装软件，未修改 Packages、ProjectSettings 或系统配置。

复测补记：本轮在保留当前 RightHandWeaponSocket 调整的前提下，重新完成长剑五段、大剑三段的实际运行与命中验证。117 项验收及最新截图对应当前挂点；场景和玩家预制体均未改动。第三段来自已有 DoubleL A_3 样本，已作为独立 Humanoid 片段导入并覆盖 Attack3。

## 连招衔接调参补记

- `AttackData.Combo Transition Point` 独立控制已缓存攻击何时进入下一段；`Combo Input Start/End` 只控制接收输入。参数为 0 时沿用原 Input End，兼容旧资源；有效衔接点限制在 Hit Window End 与 Completion 之间，避免截断命中或晚于完成点无法衔接。
- 有缓存且体力足够时，到衔接点直接切段；衔接点之后、输入窗口结束之前收到输入也立即衔接。无输入或体力不足时，按 Completion → Recovery → Locomotion 收招。修正 Recovery Time 提示：此参数不影响已缓存连招。
- 保留用户将 `AD_GreatSword_1.Combo Transition Point` 调至 0.55 的修改，Input End 仍为 0.68。`AD_GreatSword_2.Start Time Offset` 从 0 调至 0.25 秒，跳过部分第二段举剑前摇；其衔接第三段的 Point 仍为 0.68，第三段入点仍为 0。
- 实际 Play Mode 左键输入验证：第一段在归一化约 0.551 切段；第二段在约 0.681 切第三段。第二段从切入到命中窗口开始，原入点约 0.420 秒，调整后约 0.170 秒，减少约 0.25 秒。这是命中窗口时序测量，动作手感仍需用户试打判断。
- `Docs/Day10ComboTiming_RuntimeValidation.json`：31 项通过，含完整大剑三段、长剑旧衔接参数兼容、提前输入缓存、窗口内晚输入立即衔接、窗口外输入丢弃、切段关闭旧命中窗口及体力二次检查。临时配置将 Recovery 增至 1.2 秒时，连招仍直接衔接；单击无续招实际等待约 1.202 秒收招。该临时配置未写回资源。
- 本次未修改场景、玩家预制体、原长剑配置或存档格式；测试结束恢复用户存档原始字节。专项编译及保存保护证据见 `Docs/Day10ComboTiming_FinalCompilation.txt`、`Docs/Day10ComboTiming_Preservation.json`。未生成独立 Windows 构建。
