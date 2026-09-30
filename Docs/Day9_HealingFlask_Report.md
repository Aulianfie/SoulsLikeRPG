# Day9 回血瓶开发记录

日期：2026-09-30。Unity 2022.3.62f3c1 / URP。

已完成规划文档 Day9 的 5 个 Task，接入实际场景 `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity` 和玩家预制体 `Assets/_Game/Prefabs/Characters/Player_Day1.prefab`。本次未实现 Day10 双武器系统。

## 使用方式

- 受伤后按 **R** 使用当前快捷道具；**E** 继续用于交互。手柄新增 `buttonWest`（Xbox X / PlayStation 方块）绑定，本轮运行验收使用键鼠。
- 默认 **3 瓶，每瓶 40 HP**。满血、死亡、空瓶、空中或其他动作状态不能开始喝药；菜单打开时不接收喝药输入。
- 动画达到 **45%** 时才扣瓶和回血，单次只触发一次。喝药中不能重复使用或普通攻击。
- 可以边走边喝，速度为正常移动的 **40%（当前配置约 1.6 m/s）**，按冲刺也不会加速。
- 受击或闪避可打断；回血点前不扣瓶，回血点后保留已生效的消耗和治疗。离开地面也会退出喝药状态。
- 赐福休息和死亡复活补满。剩余瓶数随现有存档保存；旧 v1–v3 存档迁移到 v4，旧档默认满瓶，新档中的 0 瓶会正确保留。
- HUD 通过 `ChargesChanged` 事件显示 `当前 / 最大`；0 瓶图标变暗，无逐帧轮询。

## 实现与资源

统一入口：`UseItem → PlayerInputReader → PlayerItemController → IPlayerQuickItem → PlayerHealingFlask → PlayerHealState → PlayerHealth.Heal()`。复用已有状态机、伤害、体力、赐福和存档逻辑。

| 文件 / 资产 | 职责 |
| --- | --- |
| `Scripts/Player/Items/PlayerHealingFlask.cs` | 瓶数、事件、治疗参数和持瓶显示 |
| `Scripts/Player/Items/PlayerItemController.cs`、`IPlayerQuickItem.cs` | 通用快捷道具入口 |
| `Scripts/Player/StateMachine/States/PlayerHealState.cs` | 动画时点、慢走、取消和状态退出 |
| `Scripts/UI/HealingFlaskPresenter.cs`、`HealingFlaskView.cs` | 数量事件与 HUD 显示 |
| `Animations/Controllers/AC_Player.controller` | 新增 `ItemUse` 层，`Empty` / `Heal` 状态 |
| `Animations/Masks/AM_ItemUse_UpperBody.mask` | 上半身喝药，下半身继续现有 Locomotion |
| `Prefabs/Items/PF_HealingFlaskPlaceholder.prefab` | 右手瓶子占位，无碰撞体 |
| `UI/Prefabs/PF_HealingFlaskUI.prefab` | 血瓶图标与 3 / 3 数量 |
| `Editor/Day9Setup.cs` | 当前已保存场景的一键接线工具 |

表中路径以 `Assets/_Game/` 为根。

喝药动作来自用户已有 `RPGAnimations - Action Dead Pose1.5.unitypackage`：仅提取 `Item_Drink.fbx` 与其共享 `T-Pose.fbx`，源文件归入 `Assets/ThirdParty/DoubleL/`，保留原 GUID。共享 Avatar 验证 `isValid=True`、`isHuman=True`；动作 Humanoid、非循环、约 3.333 秒，根位移由现有 Motor 负责。已在当前玩家上实际播放。动画采样确认右手靠近头部，因此瓶子挂右手，喝药时暂时隐藏该手的武器，退出时恢复原显示状态。使用原资产授权，未下载新动画包或整包导入。

Unity 中这里用 Animator Layer + Avatar Mask 实现分层动作；基础层走路与上层喝药同时运行。没有新增动画插件或修改系统配置。

## 验证证据

- `Logs/Day9_RuntimeValidation.json`：实际 Play Mode **32 项通过**，用 Input System 向玩家已配对键鼠设备排队输入；验证延迟回血、单次扣瓶、慢走速度、上下身动画、重复输入、满血 / 空瓶拒绝、受击和闪避的前后时点、菜单输入、保存、赐福、真实死亡淡入淡出复活，以及原有攻击和跳跃。
- `Logs/Day9_LoadValidation.json`：独立运行 **3 项通过**，真实重新进入运行模式加载 0 瓶，受伤后 R 仍不可用；实际 E → InteractState → 赐福休息补满且 HUD 更新。
- 上述验收结果也保存为 `Docs/Day9_RuntimeValidation.json`、`Docs/Day9_LoadValidation.json`，编辑器状态保存为 `Docs/Day9_FinalEditorState.txt`，便于随代码审阅。
- `Logs/Day9_EditorState.txt`：最终编辑模式，场景已保存，**0 错误 / 0 警告**。
- `Logs/Day9_Setup.txt`：动作、手部、预制体和场景接线结果。
- `Logs/Day9_Drink_GameView.png`：实际 Game View 的喝药与 HUD 截图，含编辑器原有 Gizmos 图标。
- 测试前备份用户存档、退出后按原始字节恢复，原档与恢复档 SHA-256 一致：`FD61C71360571CD0D54182F1FB34EBA42F12F8EE036CF8BD3CD03F4294F0B44F`。
- 临时编辑器桥接和运行探针已移出 Assets，源码留在 `Logs/Day9ValidationTools/`；未保留自动运行测试组件。已有用户修改保留。

本轮完成编辑器编译及运行验收，未打独立平台构建。正式模型的瓶口贴合、握持角度和最终美术效果需在替换后调整。

## 替换正式瓶子

用户目前只需准备 3D 瓶子模型。将第三方源模型放入 `Assets/ThirdParty/`，再在 `PF_HealingFlaskPlaceholder.prefab` 内用模型替换 `BottleBody`、`Neck`、`Cork` 三个几何占位子物体，保留预制体根对象和玩家手上的引用即可。调整模型子物体位置、旋转和缩放，不需要修改回血代码。

数值在玩家的 `PlayerHealingFlask` Inspector 中调整：Max Charges、Heal Amount、Heal Point、Movement Multiplier。若重新接线，先保存场景并退出 Play Mode，再运行 `Tools > SoulsLike RPG > Day9 > Setup Healing Flask In Open Scene`。

后续补充：用户已提供 `D:/下载/血瓶.fbx`，优化版资源与独立 URP 预制体已生成。当前场景仍保留占位，供用户按教程替换和调整挂点。详见 `Docs/HealingFlask_Optimization_Report.md`。
