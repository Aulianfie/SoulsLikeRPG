# Day8 Task1–Task3 实现报告

日期：2026-09-28。范围：Soul Currency、Soul HUD、Player Progression。

## 完成内容

- `SoulWallet`：货币余额、加魂、余额检查、消费、`SoulsChanged(int)`；负数操作被拒绝，零操作不发事件，加魂不会发生整数溢出。
- `EnemyHealth`：新增 `Died` / `Revived` 事件；`EnemyReward` 在死亡时发放一次奖励，敌人经现有 `CheckpointManager.ResetWorld()` 复活后可再次发放。默认每次奖励 100，可分别在 Inspector 配置。
- `SoulHUDView` / `SoulHUDPresenter`：事件驱动刷新，支持 `1,250` 千分位；禁用时解除订阅，重新启用时立即显示当前余额；不使用 `Update` 轮询。
- `PlayerProgression`：只读 Level、Vigor、Endurance、Strength，默认均为 1。通过 `SetProgression(level, vigor, endurance, strength)` 统一更新基础数据、应用属性并发送 `ProgressionChanged`。
- `PlayerProgressionConfig`：为 Task3 提供基础属性公式配置：HP 100、每点 Vigor +10；体力 100、每点 Endurance +8；每点 Strength +0.05 伤害倍率。升级费用等 Task4 内容尚未加入。
- 生命/体力组件支持实例最大值；属性增加时保留已损失的资源数量，死亡玩家不会因此复活；力量倍率由 `PlayerCombat` 在开启命中窗口时应用。现有 `PlayerStatsConfig` 和 `AttackData` 资源不被改写。整数伤害四舍五入，0.5 向上。

## 资源与场景

| 路径（相对于 Unity 项目） | 用途 |
| --- | --- |
| `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity` | 已保存玩家成长组件、两个敌人的钱包引用和 `HUD/Canvas/SoulHUD` |
| `Assets/_Game/Prefabs/Characters/Player_Day1.prefab` | 加入 SoulWallet、PlayerProgression，并绑定成长配置 |
| `Assets/_Game/Prefabs/Characters/EnemyDummy_Day0.prefab` | 加入 EnemyReward，默认奖励 100；没有场景引用的实例会在初始化时寻找同场景钱包 |
| `Assets/_Game/UI/Prefabs/PF_SoulHUD.prefab` | 独立货币 HUD，右下角锚定，420×140，边距 32/20 |
| `Assets/_Game/UI/Textures/SoulHUD_Frame.png` | 用户提供的透明 PNG 的原样副本，导入为单 Sprite |
| `Assets/_Game/Configs/Player/SO_PlayerProgression_Default.asset` | 默认成长数值 |

图片来源：`实现计划/Day8/升级系统与金币系统.assets/ChatGPT 图像 2026年9月28日 19_28_04.png`。没有下载第三方资源、安装依赖或修改系统配置。

## 验证结果

- 使用现有 `G:/Unity/2022.3.62f3c1/Editor/Unity.exe` 完成修改前基线检查和修改后 Unity 编译；没有 C# 编译错误。
- 在真实 `03_AncientDungeon_Checkpoint` 场景进入 Play Mode，项目专用验证器的 **35 项检查全部通过**。这是运行时检查器，不是 Unity Test Framework 测试套件。
- 验证包含：加魂/消费/非法输入/溢出、初始 HUD 和千分位、HUD 禁用与重新启用、重复绑定、死亡重复发奖保护、实际检查点重置后再次发奖、不同敌人独立奖励、成长初值、实际 HP/SP 更新、资源损失保留、共享配置不变、死亡/复活资源行为、真实攻击伤害。
- 实际 `PlayerCombat → WeaponHitbox → EnemyHealth` 命中：基础伤害 25，Strength 4，倍率 1.15，最终造成 **29** 伤害；共享 `AttackData.Damage` 仍为 25。
- 运行阶段捕获的 Console：**0 Error、0 Warning**。编译器另有 `CS1668` 环境警告，现有 `LIB` 变量指向不存在的 VS 库目录；未修改该系统变量。Unity 启动时许可证握手日志曾报错，随后成功取得授权并完成运行。
- 已目视检查 1920×1080 实际场景截图，Soul HUD 正确位于右下角并显示 `2,450`。
- 验证结束已退出 Play Mode。验证只操作运行时副本，不调用存档保存，不保存测试期间的角色属性、敌人奖励或位置变化。
- 未执行 Windows Player 构建；未修改 Build Settings。

本地证据（`Logs/` 由项目现有 `.gitignore` 排除）：

- `Logs/Day8_Baseline.log`
- `Logs/Day8_Setup.log`
- `Logs/Day8_Validation.log`
- `Logs/Day8_RuntimeConsole.log`
- `Logs/Day8_Task123_Validation.json`
- `Logs/Day8_SoulHUD_Preview.png`

## 使用与后续范围

1. 打开 `03_AncientDungeon_Checkpoint` 并进入 Play Mode，击杀敌人后右下角 Soul 立即增加；在赐福休息重置敌人后可再次获得奖励。
2. 编辑 Enemy 的 `EnemyReward/Soul Reward` 设置不同奖励。编辑玩家 `PlayerProgression` 的四个基础值后再进入 Play Mode，可验证不同初始成长属性。
3. 数值配置统一编辑 `SO_PlayerProgression_Default`。原 `PlayerStatsConfig` 继续管理蓝量、攻击/闪避体力成本、体力恢复速度与延迟。
4. 工具菜单：`Tools/SoulsLike RPG/Day8/Build Task1-3 In Open Scene` 用于将当前有 PlayerHUD 的单玩家场景接入；`Validate Task1-3 (Play Mode)` 在已保存的检查点场景重新执行验证。
5. 升级消费、Grace Menu、Level Up Panel、存档版本升级、SoulDrop 属于 Task4–Task9，未在本次实现。当前 Soul/成长数据只保留在本次运行中。

Git 基线：`e9838a6`。开始前已有 `Assets/CodeMonkeyFree/Editor/ScriptableObjects/CodeMonkeyFreeSO.asset` 未提交改动，保留该用户资产；本次不重写或回退该文件。
