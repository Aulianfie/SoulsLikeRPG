# 赐福转向与死亡掉魂

已接入 `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity`，玩家 Prefab 为 `Assets/_Game/Prefabs/Characters/Player_Day1.prefab`。

## 操作与规则

- 靠近赐福按 E：停止水平移动，立即在水平面上转向赐福，再播放原交互动画。动画完成后才回血、重置敌人、保存并打开菜单；受击、死亡、目标失效或坠落仍可取消交互。
- 玩家死亡：钱包归零，用 `SoulDrop.prefab` 在死亡附近生成一份包含全部所持魂的掉落。空中死亡时使用最后站稳的地面位置，并在附近 NavMesh 上定位，避免掉在半空或深坑里。
- 复活后靠近掉魂，会出现 `[E] 取回遗失的魂（金钱数）`。按 E 立即返还金额并销毁掉落，不播放赐福休息动画。
- 同时最多有一份有效掉魂。没有拾取就再次死亡，旧掉落与其中的钱永久丢失；本次只掉落当前钱包里的魂。当前钱包为零时，仍销毁旧掉落，但不生成零金额掉落。
- 取回的金额可以与复活后新赚的钱相加；已拾取或已过期的实例不能重复发钱。钱包继续遵循已有整数上限规则。

例如：

| 操作 | 钱包 | 地上的魂 |
| --- | ---: | ---: |
| 持有 1000 时第一次死亡 | 0 | 1000 |
| 未拾取，重新赚到 150 | 150 | 1000 |
| 再次死亡 | 0 | 150，原 1000 丢失 |
| 拾取新掉落 | 150 | 无 |

## 实现分工

- `PlayerMotor.FacePosition` 处理朝向；`PlayerInteractState.Enter` 在动画调用前请求转向。
- `IInteractable.RequiresInteractionAnimation` 区分需要动画的赐福与即时拾取的魂，复用原有 PlayerInteractor / E 键输入。
- `PlayerSoulDrop` 监听 PlayerHealth.Died，管理当前掉落、死亡替换、取回资格与存档数据。
- `SoulDrop` 提供交互提示并把拾取请求交给所属玩家；VisualRoot 继续与碰撞器及业务逻辑分离。
- `CheckpointManager` 监听钱包、成长和掉魂变化，在帧末合并保存完整快照。

保留了用户对 SoulDrop 根节点 **2 倍缩放**的调整。移除了上次模型接入时的静态 `SoulDrop_ModelPreview`；现在实际掉落由死亡事件生成。重新运行模型接入工具也不会在已有掉魂控制器的场景中添加静态预览。

## 存档 v3

在原有字段上新增 `hasSoulDrop`、`droppedSouls` 和 `soulDropPosition`。死亡、拾取和旧掉落丢失都会保存。退出并重新进入游戏时，恢复钱包、成长、赐福，以及未取回的魂和位置。

兼容旧档：v1 补齐初始 1000 Soul 与初始属性；v2 保留原魂和成长数据；两者迁移为 v3，默认没有遗留掉魂。坏档、未来版本和无效坐标继续被拒绝，原文件不自动覆盖。

## 验证方式

- 配置菜单：`Tools > SoulsLike RPG > Day8 > Setup Death Soul Drop`，本次已执行并保存，无需再配置。
- 运行验证：`Tools > SoulsLike RPG > Day8 > Validate Death Soul Drop and Facing`。
- 验证覆盖真实转向与交互动画、死亡与淡出复活、物理触发提示、模拟键盘 E 拾取、重复拾取、连续死亡替换、零余额死亡、空中死亡落点、存档迁移，以及退出 / 重新进入 Play Mode 后恢复。
- 结果：`Logs/DeathSoulDrop_Validation.json`；运行日志：`Logs/DeathSoulDrop_RuntimeConsole.log`。验证结束会恢复原始存档字节或原始不存在状态。
- 未执行独立 Player Build；重启测试指退出并重新进入 Unity Play Mode。

本次验证结果：Unity 编译通过；新增功能 **30/30** 项、升级与赐福菜单回归 **53/53** 项、存档与重新进入 Play Mode 回归 **42/42** 项全部通过。三个运行日志均没有非预期错误或警告。Console 保留了一条损坏存档测试主动触发的预期警告，不是正常存档加载故障。

验证结束后，实际存档与三套验证前备份的 SHA-256 均一致；当前编辑器已回到 Edit Mode。场景和玩家 Prefab 已保存，初始金币仍为 1000。
