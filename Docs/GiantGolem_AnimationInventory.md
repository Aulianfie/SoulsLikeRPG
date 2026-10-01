# Giant Golem 动画导入与清单

日期：2026-10-01。目标项目：`G:/Unity Project/SoulsLikeRPG/SoulsLikeRPG`。Unity：`2022.3.62f3c1`，URP。

## 导入记录

- 资源：Giant Golem AnimSet v1.41；用户提供本地 unitypackage，本次没有下载或安装软件。
- 来源：`G:\百度网盘下载\Unity3D_Giant Golem AnimSet_V1.41_进击巨人Boss角色攻击动画\Giant Golem AnimSet v1.41.unitypackage`。
- 位置：`Assets/ThirdParty/GiantGolemAnimSet/`。原包目录拼作 `Gient_Golem_AnimSet`，导入时只重定位目录，保留全部原 GUID。
- 包 SHA-256：`03f78e3d8a06b50f12f61ca2f636883ec8d91be6af3fc3f4830e884630834a39`。
- 共 118 项：9 个文件夹、92 个 FBX、13 个演示 Animator Controller、1 个 Prefab、1 个演示场景、1 个材质和1份 PDF。
- 动画：45种动作，各有普通/Root Motion 版本和 Inplace 原地版本，共90个动作片段；另有 T-pose 模型及其参考片段。
- 作者：包内说明标注 wemakethegame。PDF 确认同时提供 Root Motion 和 In-Place 选项，但没有说明编号攻击的具体招式。
- 使用说明：包内 PDF 限定购买者使用并禁止转让、网络再分发；本次不验证购买凭据，也不将其标记为免费/CC0资源。

## 验证结果与边界

- 92 个 FBX 均被 Unity 加载：91 个 Humanoid（90 个动作文件及 T-pose），另一个是 Generic 的 Dome 环境模型。
- T-pose 的 Avatar 为 valid / human；90 个动作文件使用 Copy From Other Avatar，共享 T-pose Avatar，不各自生成 Avatar。有效 Avatar 已逐项解析确认。
- 90 个动作都在隔离的 Preview Scene 中通过 Playables 驱动 T-pose 模型，比较起始与中间姿态，全部成功；这不是全帧视觉验收或正式 Boss 战斗测试。
- 采样时 Console：0 error / 0 warning；当时不在 Play Mode、没有编译进行中。
- 导入资源的非内置 GUID 引用均能解析。原演示场景缺失 Lighting Settings 已清空失效引用，使用场景默认值；没有改动游戏关卡。
- 游戏主场景验证前后均无新脏状态；现有玩家/敌人 prefab、控制器、业务脚本与项目设置没有由本次任务修改。
- 保留原包材质：Dome 演示材质使用内置 Standard shader，URP 的演示场景视觉表现尚未验收；若出现粉色，应另做项目自有 URP 材质。
- 未检查重定向到其他角色的视觉质量、脚底滑动、根位移距离、攻击命中帧、场地适配。
- 完整机器可读证据：`Docs/GiantGolem_ImportValidation.json`。

## 关键导入设置

**全部90个动作片段都开启 Loop Time**，包括攻击、受击、Stun_Start/End、倒地、起身和死亡。这里只记录，不批量改写供应商设置。接入 Boss 时应在项目自有 clip/对应进口设置中按用途处理：移动、待机和 Stun_Loop 循环；攻击、受击、起身、死亡及 Stun_Start/End 通常单次播放。

普通版可用于评估 Root Motion，原地版更便于沿用现有 EnemyMotor/NavMeshAgent 的移动权威。冲刺/跳跃攻击若选原地版，仍需要程序实现对应的实际位移；动画文件不会自动生成伤害、投石物、冲击波或无敌窗口。

## 怎么查看

1. 在 Unity Project 面板打开 `Assets/ThirdParty/GiantGolemAnimSet/Animation/Humanoid/`。
2. 展开 FBX 左侧箭头，选中内部的 AnimationClip，在 Inspector 的 Preview 区播放；不要只选 FBX 的 Model 导入页。
3. 动画文件主要是骨架；若预览缺少外观，把 `00_T-pose_golem.FBX` 作为预览模型，或在独立测试场景放置该模型并使用包内单动作 controller。
4. 原地版本位于 `Animation/Humanoid/Inplace/`，文件和片段名有 `_inplace` 后缀。
5. 演示场景：`ScenePreview/Gient_Golem_AnimSet.unity`。环境 prefab：`Prefab/Dome.prefab`，它不是 Boss prefab。`AnimatorContrl/` 中13个 controller 是动作演示资源，不能当成已实现的 Boss AI。

## 完整动作表

时长来自 Unity 实际加载片段，播放速度为1时的秒数，均为30fps。表中动作用途按命名归类；编号攻击、防御和倒地具体表现需要逐个视觉确认。每一行均有普通版和原地版。

### 待机（1种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `idle` | 1.667 | 1.667 | 开启 / 开启 |

### 行走（8种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `move_walk_front` | 2.033 | 2.033 | 开启 / 开启 |
| `move_walk_frontL45` | 2.000 | 2.000 | 开启 / 开启 |
| `move_walk_frontR45` | 2.000 | 2.000 | 开启 / 开启 |
| `move_walk_Left` | 2.033 | 2.033 | 开启 / 开启 |
| `move_walk_Right` | 2.033 | 2.033 | 开启 / 开启 |
| `move_walk_back` | 2.000 | 2.000 | 开启 / 开启 |
| `move_walk_backL45` | 2.000 | 2.000 | 开启 / 开启 |
| `move_walk_backR45` | 2.000 | 2.000 | 开启 / 开启 |

### 奔跑（8种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `move_run_front` | 1.400 | 1.400 | 开启 / 开启 |
| `move_run_frontL45` | 1.400 | 1.400 | 开启 / 开启 |
| `move_run_frontR45` | 1.400 | 1.400 | 开启 / 开启 |
| `move_run_left` | 1.400 | 1.400 | 开启 / 开启 |
| `move_run_right` | 1.400 | 1.400 | 开启 / 开启 |
| `move_run_back` | 1.400 | 1.400 | 开启 / 开启 |
| `move_run_backL45` | 1.400 | 1.400 | 开启 / 开启 |
| `move_run_backR45` | 1.400 | 1.400 | 开启 / 开启 |

### 攻击（12种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `attack01` | 3.733 | 3.733 | 开启 / 开启 |
| `attack02` | 3.333 | 3.333 | 开启 / 开启 |
| `attack03` | 3.667 | 3.667 | 开启 / 开启 |
| `attack04` | 3.333 | 3.333 | 开启 / 开启 |
| `attack_ground01` | 4.733 | 4.733 | 开启 / 开启 |
| `attack_ground02` | 3.333 | 3.333 | 开启 / 开启 |
| `attack_foot_left` | 3.467 | 3.467 | 开启 / 开启 |
| `attack_foot_right` | 3.467 | 3.467 | 开启 / 开启 |
| `attack_DashAtk` | 4.000 | 4.000 | 开启 / 开启 |
| `attack_jumpAtk` | 3.067 | 3.067 | 开启 / 开启 |
| `attack_throwstone` | 7.000 | 7.000 | 开启 / 开启 |
| `attack_whirlwind` | 3.633 | 3.633 | 开启 / 开启 |

### 防御（3种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `defence01` | 1.333 | 1.333 | 开启 / 开启 |
| `defence02` | 3.600 | 3.600 | 开启 / 开启 |
| `defence03` | 3.600 | 3.600 | 开启 / 开启 |

### 受击（6种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `hit_front` | 0.400 | 0.400 | 开启 / 开启 |
| `hit_back` | 0.400 | 0.400 | 开启 / 开启 |
| `hit_left` | 0.400 | 0.400 | 开启 / 开启 |
| `hit_right` | 0.400 | 0.400 | 开启 / 开启 |
| `hit_shoulder_left` | 1.500 | 1.500 | 开启 / 开启 |
| `hit_shoulder_right` | 1.500 | 1.500 | 开启 / 开启 |

### 眩晕（3种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `Stun_Start` | 1.667 | 1.667 | 开启 / 开启 |
| `Stun_Loop` | 2.000 | 2.000 | 开启 / 开启 |
| `Stun_End` | 1.600 | 1.600 | 开启 / 开启 |

### 倒地和起身（2种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `down01` | 2.667 | 2.667 | 开启 / 开启 |
| `rise01` | 4.333 | 4.333 | 开启 / 开启 |

### 死亡（2种）

| 动作名 | 普通版秒数 | 原地版秒数 | 当前 Loop Time |
|---|---:|---:|---|
| `dead01` | 2.867 | 2.867 | 开启 / 开启 |
| `dead02` | 2.200 | 2.200 | 开启 / 开启 |

## 后续 AI 编排

初步方案见 `Docs/GiantGolem_BossAI_Draft.md`。该文件是设计草案，尚未实现 Boss 状态机、命中判定或场景接线。
