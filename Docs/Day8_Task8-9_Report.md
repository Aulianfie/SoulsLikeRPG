# Day8 Task8–9 实现说明

## Task8：Soul 与成长存档

沿用 `SaveService` 和 `CheckpointManager`，将 `GameSaveData.CurrentVersion` 提升为 2。保存字段为 `version`、`sceneName`、`checkpointId`、`souls`、`level`、`vigor`、`endurance`、`strength`。不保存最大 HP、最大体力或伤害倍率；读档后由当前 `PlayerProgressionConfig` 重新计算。

保存时机：

- 玩家在赐福处休息时保存当前赐福与成长。
- 钱包或成长变化后，在该帧的 LateUpdate 合并保存完整快照。包括击杀收入、升级扣款与属性递增，菜单暂停期间也能保存。
- 应用暂停或退出时保存；CheckpointManager 禁用时提交尚未保存的变化。

尚未发现赐福时允许使用空 `checkpointId` 保存成长，出生位置继续使用原有默认出生点。读取同场景存档时恢复钱包、HUD、基础成长和赐福；原有 RespawnController 继续负责出生与死亡复活。

兼容与文件保护：

- v1 赐福存档保留场景和赐福 ID，补齐 1000 Soul、等级与三项属性均为 1，然后保存为 v2。
- v2 中的 0 金币是合法余额；缺失成长字段、负余额、损坏 JSON 或不支持的版本均拒绝加载。
- 无法读取的现有存档不会被自动保存覆盖。此时角色使用默认状态，修复存档前自动保存停用；Console 会提示读取问题。
- 写入仍采用临时文件加原子替换，避免直接覆盖写入造成半份存档。

存档位置沿用 `Application.persistentDataPath/checkpoint_save.json`，本机为 `C:/Users/16PRO/AppData/LocalLow/DefaultCompany/SoulsLikeRPG/checkpoint_save.json`。这是项目原有的游戏存档位置，本次未修改系统配置，也未安装依赖。

## 初始金币

`SoulWallet` 的默认值和 `Assets/_Game/Prefabs/Characters/Player_Day1.prefab` 的初始值均设为 **1000**。无存档新游戏与 v1 迁移均从 1000 开始；已有 v2 存档恢复其中的实际余额，不重复赠送 1000。

## Task9：SoulDrop 模型占位

新增 `Assets/_Game/Prefabs/World/SoulDrop.prefab`：

```text
SoulDrop                 [SoulDrop.cs]
├── VisualRoot
│   └── Placeholder      [金色 Sphere / Renderer]
└── Collider             [SphereCollider，Is Trigger]
```

`SoulDrop.cs` 只保留视觉根引用。碰撞器独立于模型；未来将 Placeholder 替换为 SoulModel，Shader、ParticleSystem、Light 等视觉内容继续放在 VisualRoot 下。没有在场景中自动生成掉魂，也没有死亡掉魂或拾取行为，这些留给文档后续阶段。

后续更新：已使用用户提供的 LostSoul FBX 的优化副本替换 Placeholder，并在检查点场景加入视觉预览实例。减面、压缩与材质记录见 [LostSoul_Import_Report.md](LostSoul_Import_Report.md)。

编辑器配置菜单：`Tools > SoulsLike RPG > Day8 > Build Task8-9 In Open Scene`。本次已执行，无需再运行即可使用；重复执行不会覆盖已有 SoulDrop Prefab。

## 验证

- 验证菜单：`Tools > SoulsLike RPG > Day8 > Validate Task8-9 (Save and Restart)`。
- 验证使用隔离文件检查存档往返、原子替换、v1 迁移、坏档与未来版本拒绝；在实际检查点场景中检查初始金币、击杀收入自动保存、升级完整快照、退出 / 重新进入 Play Mode 的恢复、真实旧档迁移及未来版本文件保护。
- SoulDrop 检查了节点、引用、触发器与模型替换；场景无 Missing Script。
- 验证会备份原存档，并在结束后还原原始字节或原始不存在状态。测试数据与结果保存在项目 `Logs` 下。
- 结果文件：`Logs/Day8_Task8-9_Validation.json`；运行日志：`Logs/Day8_Task8-9_RuntimeConsole.log`。
- 42 项存档 / 占位检查全部通过；运行日志没有意外错误或警告。注入不支持的版本时，Console 中的拒绝读取提示属于预期保护行为。
- Task4–7 菜单回归检查已通过 53 项。
- 本次未运行独立 Player Build；重启验证使用 Unity Editor 退出并重新进入 Play Mode，未声称验证过独立程序进程重启。
