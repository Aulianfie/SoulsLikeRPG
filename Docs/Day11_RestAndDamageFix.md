# 赐福回蓝与跳劈伤害修复

日期：2026-10-01。用户报告赐福只回血、不回蓝，以及跳劈疑似无伤害。本次针对现有 Day11 功能排查并修复。

## 确认的原因与改动

1. **赐福遗漏回蓝调用**：CheckpointManager.ActivateCheckpoint 原来只回 HP、体力和补血瓶。真实休息基线为 HP130/130、体力108/108、MP80/100。PlayerMana 新增 RestoreFull，赐福接入已有 ManaChanged 事件，修复后 MP100/100、HUD蓝条满。死亡复活也补上回满蓝。
2. **长剑早发动跳劈的命中窗口过早结束**：站在真实敌人前0.8米、实际离地后约0.08秒发动时，刀刃在约0.685–0.818的动画进度碰到敌人，原0.32–0.60窗口已关闭，HP损失为0。延长 AD_LongSword_Jump 命中窗口至0.32–0.85，并将完成点改为0.90；Day11Setup同步更新，后续重跑设置也保留修复。仍使用原 WeaponHitbox、去重和伤害公式。
3. 大剑跳劈、长剑战技、大剑战技在修复前的正常敌人体积测试中已有伤害，保留原窗口。无敌人碰撞体扩张、自动扣血或新增范围伤害。

## 真实敌人验证

使用 `03_AncientDungeon_Checkpoint` 实际 EnemyHealth 和原始非Trigger身体碰撞体，关闭敌人移动/AI以固定位置，保留玩家实际动画、CharacterController重力和WeaponHitbox LateUpdate检测。测试0.8、1.2、1.6米距离；每把武器跳劈覆盖离地后约0.08、0.25、0.45秒三种发动时机。

| 动作 | 修复后近距离HP损失 | 每次动作伤害通知数 |
|---|---:|---:|
| 长剑跳劈 | 40，三个发动时机均命中 | 1 |
| 大剑跳劈 | 64，三个发动时机均命中 | 1 |
| 长剑战技 | 52 | 1 |
| 大剑战技 | 100（敌人原HP100，被击杀） | 1 |

数值是当前加载存档成长属性下的实测HP损失，包含玩家成长和武器倍率；死亡时HP损失最多等于敌人剩余HP。测试断言使用既有伤害公式核对实际掉血数值。长剑本次跳劈在1.2/1.6米处刀刃没有碰到敌人，因此空挥不掉血；移动起跳仍可通过保留的水平速度接近目标。

本次真实敌人/赐福验收74项通过，完整动作回归125项通过（含新增实际死亡复活回满MP检查）。两轮运行时均无Console Error，原存档逐字节恢复。自动验证在同版本Unity项目副本完成，修改后的代码与目标资产逐文件校验一致并回写主项目；主编辑器MCP仍不可连接。

证据位于 `Logs/Day11/`：

- `baseline_rest.txt` / `fixed_rest.txt`：实际赐福资源前后对比。
- `baseline_damage.csv` / `fixed_damage.csv`：24种武器、动作、距离、跳劈时机组合的敌人HP损失和命中次数。
- `baseline_blade_poses.csv` / `fixed_blade_poses.csv`：实际刀刃位置、敌人重叠、归一化时间与窗口状态。
- `DamageFixed_RuntimeValidation.txt` / `damage_fixed.log`：74项通过。
- `Actions_RuntimeValidation.txt` / `actions_validation.log`：完整动作回归125项通过。

此前124项验收使用的大范围测试靶，只证明伤害流程与去重能运行，没有覆盖正常敌人高度下的早跳劈漏伤害。本次新增 `Day11DamageValidation`，可从 `Tools → SoulsLike RPG → Day11 → Validate Rest and Real Enemy Damage` 在已保存的Edit Mode重跑。

主Unity退出Play Mode并Assets → Refresh后，再贴近敌人测试Space后快速按LMB，以及地面Q战技。赐福休息时HP、MP、体力与血瓶应全部恢复。场景、Prefab、原轻攻击配置和大剑/战技数值未修改。

## 后续更新：跳劈下降衔接与战技前进

同日用户进一步反馈后，长剑跳劈改为前摇抬刀等待下降接近地面，再释放挥砍；当前命中窗口为0.32–0.62，替代本报告所述的早期延长窗口方案。当前fixed_damage.csv及fixed_blade_poses.csv已更新为下降衔接和战技前进版本，74项实际敌人验收及125项动作回归仍全部通过。新增63项瞄准/位移验证也通过。当前调参和实现说明见 [Day11_JumpAimAndSkillMotion.md](Day11_JumpAimAndSkillMotion.md)。
