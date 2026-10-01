# Boss 接入当前主场景

接入日期：2026-10-02。

当前应使用 `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity` 测试。之前更新的武器/药瓶 HUD、HP/MP 消耗品、赐福、成长、存档和复活接线都在这里。`05_AncientDungeon_GiantGolem` 基于较早的资源场景创建，没有这些完整接线，不作为当前主场景。

| 场景 | 用途 |
|---|---|
| 03_AncientDungeon_Checkpoint | 当前主场景，保留已有功能并接入 Boss |
| 04_GiantGolem_BossTest | 独立 Boss AI / 技能测试场景 |
| 05_AncientDungeon_GiantGolem | 之前基于旧资源场景生成的 Boss 副本，保留供参考 |
| Dark Fantasy Environment/Scenes/ANCIENT DUNGEON | 较早的资源场景 |

## 新增内容

- `GiantGolemBoss`：现有 Boss 预制体实例，位置 `(-89.75, 39.648, -167.75)`，朝向和体型沿用之前的放置结果，目标与奖励绑定主场景玩家。
- `Navigation_GiantGolem`：68 × 18 × 68 米的局部导航，按半径0.85米、高度4.4米烘焙。
- 独立数据：`Assets/_Game/Navigation/NM_AncientDungeon_Checkpoint_GiantGolem.asset`。主场景原入口导航和05的导航不改写。
- `GiantGolem_ApproachPoint`：`(-97.25, 39.680, -167.75)`，用于运行模式下的近处测试。

玩家编辑态位置、默认复活点和存档中的赐福选择没有修改。Boss 通过现有 `ICheckpointResettable` 被 CheckpointManager 收集，不增加第二套检查点流程。

## 如何测试

1. 打开03主场景并进入 Play Mode。
2. 打开 `Tools > SoulsLike RPG > Giant Golem > 3 Boss AI Debugger`。
3. 点击“查找当前场景 Boss”。玩家处于 Locomotion 时，点击“传送玩家到 Boss 测试点”。
4. 保持“自动 AI”开启可测试战斗；关闭后可强制播放技能检查动作。

测试传送通过原 PlayerMotor，并通知跟随相机位置跳变。它只改变运行时位置，退出 Play Mode 恢复编辑态；不改变复活点。人工试玩期间仍沿用主场景正常保存规则。

调整该区域的障碍物后，使用菜单 `8 Rebake Dungeon Boss Navigation` 重烘。菜单 `7 Validate Dungeon Navigation` 现在支持03和05；验证会备份当时的存档，结束后恢复原始字节。

## 本次验证

- 保存重开03后，Boss及局部导航有效；真实Agent寻路移动超过1米，路径完整。
- 武器HUD图标匹配当前武器；调用原切槽入口后，HP/MP药瓶图标和数量随当前道具更新。
- 调试窗口的测试点传送入口实测通过。
- 检查点确实收集Boss；调用原ResetWorld后，Boss恢复出生点与导航控制权，原有小怪仍在导航上。
- 测试结束后，原存档字节完全恢复。
- 与接入前的实时场景备份比较，1845个原有序列化记录均保留。唯一变化的原有记录是SceneRoots，追加了三个根节点；1844个原有对象记录没有内容变化。
- 玩家预制体、三个HUD/赐福预制体、原资源场景、05场景、原有导航数据和全局NavMesh配置的SHA256一致。
- 最终重新编译后Console为0错误、0警告。

证据：`GiantGolem_MainScenePlacement.json`、`Logs/GolemMainIntegration/RuntimeValidation.txt`、`SceneObjectsPreserved.json`、`ProtectedAssetsVerified.json`和`FinalEditorState.json`。

场景备份位于 `Logs/GolemMainIntegration/Backup/BeforeAddingBoss.unity`。本次未自动提交Git。连续战斗、镜头手感和本场地攻击预兆仍需人工试玩。
