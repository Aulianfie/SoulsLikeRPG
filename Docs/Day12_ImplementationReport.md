# Day12：Consumable / Quick Item Framework

实现日期：2026-10-01。范围依据 `SoulsLikeRPG_Day12_Consumable_Framework_NoBuff.md`。

## 已完成

- `ConsumableData` 统一 ItemId、DisplayName、Icon、MaxCharges、ConsumePoint、CompletionPoint、MovementMultiplier、Effects 配置。SO 不持有玩家运行时次数。
- `ConsumableEffect` 提供通用 `CanApply(GameObject)` / `Apply(GameObject)` 边界，当前只有 `RestoreHealthEffect`、`RestoreManaEffect`。多效果中至少一种有效即可使用。
- `PlayerConsumable` 管理独立次数、消费、补满、读档、数量事件与手持显示。满资源、死亡、零次数不可用；消费点再次验证效果是否有意义，失败不扣次数。
- `PlayerUseItemState` 替代 `PlayerHealState`，共用原 `ItemUse.Heal` 动画、ItemUse Layer 与消费时序。状态不访问 Health/Mana，不判断瓶子类型；每次进入最多消费一次。
- `PlayerItemController` 配置两个 Quick Item 槽，提供前后切换、指定槽位、使用当前道具、通用休息补充入口和事件。按用户追加要求，切槽独立于动作状态：游戏输入启用、时间未暂停、玩家存活时即可切槽；死亡、暂停和菜单禁用输入时仍阻止切换。使用道具仍需站在地面的 Locomotion。
- HUD 使用 `QuickItemPresenter` / `QuickItemView`，通过 CurrentItemChanged 与当前道具 ChargesChanged 更新，无 Update 轮询。切槽立即更新图标与数量；零次数灰显。
- 赐福休息和真实死亡复活均补满两种瓶子；继续复用原 Health/Mana/Stamina、成长、魂和 Checkpoint 逻辑。
- Save Version 从 4 升至 5，保存 hpFlaskCharges、mpFlaskCharges、currentQuickItemSlot。原 flaskCharges 保留为迁移字段和 HP 镜像。v1–v4 链式迁移，旧血瓶数量进入 HP 槽，MP 默认按配置补满，当前槽默认 0。缺失字段用非法哨兵辨别，损坏/未来存档仍受原保护机制约束。
- 修复验收发现的 `ProgressionPresenter.CloseMenu()` 在场景卸载时访问已销毁输入组件的问题，并重跑赐福 UI 回归。

未引入 Buff、装备属性加成、Inventory、拾取、Item Database、独立蓝瓶动画或复制版 PlayerManaFlask。

## 配置与操作

| 道具 | 槽位 | 最大次数 | 每次恢复 | 消费点 | 完成点 | 移速倍率 |
|---|---:|---:|---:|---:|---:|---:|
| HP Flask | 0 | 3 | 40 HP | 0.45 | 0.95 | 0.4 |
| MP Flask | 1 | 3 | 50 MP，受 MaxMana 上限约束 | 0.45 | 0.95 | 0.4 |

- 键盘：数字键 `1` 切换道具，`R` 使用，`Q` 战技，鼠标滚轮切武器。
- 手柄：方向键下切换道具，X / buttonWest 使用。手柄输入已通过虚拟设备测试。
- 奔跑、跳跃、下落、落地、攻击、战技、受击、闪避、换武器和喝药中均可切槽。这一规则以用户追加要求为准，覆盖 Day12 原计划的 Locomotion 切槽限制。
- 喝药中切槽立即更新 HUD，但这次动画持有开始时的道具引用；手持模型、恢复效果及扣除次数仍属于原道具，结束后保留新选择。切槽不打断攻击或其他动作。死亡、暂停和赐福菜单仍禁止切槽。
- 消费点前受击/死亡：不扣次数，不恢复资源。消费点后受击：保留已提交的效果与次数，清理瓶子并恢复武器。

## 实际资产与接线

- 主场景：`Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity`。
- 玩家 Prefab：`Assets/_Game/Prefabs/Characters/Player_Day1.prefab`，配置 HP/MP 两个运行时组件与两个手持视觉对象，复用同一网格，父手骨、根节点及全部模型子节点的位置/旋转/缩放完全一致。场景内当前红瓶的调整也同步作为 Prefab 的默认持瓶姿态。
- HUD Prefab：`Assets/_Game/UI/Prefabs/PF_HealingFlaskUI.prefab`，继续使用原布局和原 HP 图标，场景 Presenter 接入 PlayerItemController。
- 配置：`Assets/_Game/Configs/Consumables/SO_HPFlask.asset`、`SO_MPFlask.asset`。
- 效果：同目录 `EF_RestoreHealth40.asset`、`EF_RestoreMana50.asset`。
- 美术：原红瓶模型/贴图保留；MP BaseMap 只将原红色液体区域改蓝，保留金属、瓶塞和玻璃高光。复用原 Normal / MetallicSmoothness。详情与最终 imagegen 提示词见 `Docs/Day12_FlaskArt.md`。
- 模型渲染：`Docs/Day12_FlaskModels.png`。

为避免现有序列化引用断开，`PlayerHealingFlask`、`HealingFlaskPresenter`、`HealingFlaskView` 保留原脚本 GUID，成为通用实现的薄兼容类；其中无独立 HP 使用流程。旧 PlayerHealState / IPlayerHealingItem 已移除，历史 Day11 验收调用更新为 UseItemState。

## 自动验收

| 验收 | 结果 | 证据 |
|---|---|---|
| Unity 脚本编译 | 通过，当前 Console 0 Error / 0 Warning | `Logs/Day12/EditorState.txt` |
| 配置 / Prefab / 场景引用 / 输入 / 缺失脚本 | 通过 | `Logs/Day12/AssetValidation.txt` |
| Day12 实播 + 双武器轻连击 | 110 项 PASS，0 FAIL | `Logs/Day12/RuntimeValidation.txt` |
| v5 读写、零次数、v1–v4 迁移、缺字段、损坏、未来版本 | 16 项 PASS，0 FAIL | `Logs/Day12/SaveValidation.txt` |
| 原战斗回归 | 125 项 PASS，0 FAIL | `Logs/Day12/CombatRegression.txt` |
| 赐福 UI / 升级 / 休息 / 输入恢复 / ESC / 虚拟手柄 B | 40 项 PASS，0 FAIL | `Logs/Day12/BlessingRegression.txt` |

Day12 实播包含 Prefab/场景/运行时完整挂点一致性、真实键盘攻击/奔跑/跳跃上升/下落/落地切槽、喝药中切槽后原道具的模型/效果/次数归属、暂停/菜单/死亡拦截、真实动画消费点、HP/MP 满值禁用与零次数禁用、消费点前后 Hurt、Death、一次消费、效果上限、动画中资源被补满不扣次数、多效果配置、事件与 HUD 重新启用、真实键盘 1/R、虚拟手柄方向键下、战技→回蓝闭环、休息/复活、保存及实际主场景重新载入后的数量与选槽恢复。

战斗回归包含双武器 LightCombo、JumpAttack、WeaponSkill、MP 消耗、输入优先级、Dodge、WeaponSwitch、Lock-On、Healing、Checkpoint、Respawn 和魂存档。

赐福输入夹具已沿用 Day11 的显式玩家输入更新，避免模拟 Esc/B 依赖 Game View 焦点；测试后恢复原输入设置。失败尝试保留在 `Logs/Day12/BlessingRegression_UnfocusedAttempt.txt`，最终重跑 40 项全部通过。

每轮自动实播均在开始前备份当时用户存档，结束后恢复原始字节；不会用较早备份覆盖用户人工游玩产生的新存档。当前结果来自应用追加反馈后的完整重跑。

自动验收通过场景重新载入验证存档初始化；未执行独立 Player 构建、关闭并重启整个 Unity 进程或实物手柄测试。

## 人工验收

状态：用户反馈蓝瓶挂点不一致、希望各存活动作期间都能切槽；两项反馈已修正并通过自动实播回归。修正后的实际握持视觉仍需用户在 Game View 中确认。

挂点偏差原因：外层节点虽然相同，红瓶模型子节点有场景调整（位置约 0.0627/-0.0272/-0.002），蓝瓶却保留导入姿态。现已按当前红瓶逐层复制，保存玩家 Prefab 和主场景；对齐前后证据在 `Logs/Day12/MountsBefore.txt` / `MountsAfter.txt`。

建议：战技消耗 Mana → 1 切 MP → R 使用 → Mana 恢复；切 HP → 受伤 → R 使用 → HP 恢复；赐福休息 → 两瓶补满。再连续连击、跳攻、战技、切瓶、喝药、换武器、闪避、受击与复活，检查卡状态、模型残留、重复消费、HUD 和配色。

## 开发与复现入口

- `Tools/SoulsLike RPG/Day12/1 Setup Consumables`：在已保存的主场景 Edit Mode 运行配置迁移。
- `Tools/SoulsLike RPG/Day12/2 Validate Consumables (Play Mode)`：运行资产、存档和实播检查，自动恢复用户存档。
- `Tools/SoulsLike RPG/Day12/4 Align MP Flask To HP Mount`：以当前场景红瓶为依据，对齐蓝瓶完整层级并保存 Prefab / 场景。
- `Tools/SoulsLike RPG/Day12/3 Render Flask Preview`：从真实 Unity 模型渲染红蓝对照。
- `Day12EditorCommands` 沿用项目 request 文件模式，仅在 Unity 主线程执行固定命令，提供 MCP 不可调用时的本地入口。
- 修改前的玩家 Prefab、HUD Prefab、主场景备份位于 `Logs/Day12/Backup/`。没有自动提交 Git。

后续新增消耗品时新增 ConsumableData / ConsumableEffect 并配置运行时实例即可复用动画和 HUD；当前存档 DTO 仍按 Day12 的 HP/MP 两槽保存，增加持久化槽位时需独立升级该格式。
