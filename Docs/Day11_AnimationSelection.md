# Day11 动画选择与下一步

更新：2026-10-01。用户授权由助手决定跳劈并继续实现 Day11；用户选定三个可接受的战技候选。四个首版动作已接入运行时，124项最终自动验收通过，等待人工手感验收。未清理候选资产。

| 用途 | 动画 | 状态 |
|---|---|---|
| 长剑跳劈 | 1Hand_Base_Jump_Attack_1_InPlace | 首版已接入，人工检查落地衔接 |
| 大剑跳劈 | 2Hand_Up_Jump_Attack_InPlace | 首版已接入，人工检查握柄和刀尖落地高度 |
| 长剑战技 | 1Hand_Up_Skill_3_InPlace | 首版已接入，MP20；人工验收后仍可换成 Base Skill 4 |
| 长剑战技 | 1Hand_Base_Skill_4_InPlace | 用户认可；保留为腾跃型备选 |
| 大剑战技 | 2Hand_Base_Skill_1_InPlace | 首版已接入，MP35；人工检查身体腾跃与碰撞体匹配 |

## 位移方案

第一版使用 InPlace 跳劈，由 PlayerMotor 控制水平移动、重力和落地。InPlace 不代表玩家必须原地跳：已有水平运动和空中移动仍由代码处理。

PlayerAirborneState 已允许空中 LMB 启动 JumpAttack；共享 PlayerAttackState 按攻击类型处理，跳劈保留水平速度并持续调用 TickAirborne。落地和动画完成后再结束后摇；被受击打断仍保留本次腾空已使用跳劈的记录，落地后重置。

此前在 Player Avatar 上启用 Root Motion 的采样：长剑1号 InPlace 根水平范围约0.0341m，非InPlace版约0.1646m；大剑Up跳劈 InPlace 根水平范围约0.0799m、竖直范围约0.1858m。这是采样包围范围，不是游戏中必然产生的位移。InPlace仍需检查导入时的根运动烘焙和视觉残余。

关闭根运动后，技能中的骨盆、腿部仍可能明显腾跃。长剑 Base Skill 4 和大剑 Base Skill 1 必须验证身体/脚部高度与角色碰撞体是否匹配；无法自然匹配时，再决定是否增加最小运动支持，不提前引入完整 Root Motion 系统。

## 当前接入与后续

1. WeaponMoveset 迁移已独立通过32项真实 Play Mode 检查，长剑5段和大剑3段仍引用原 LightCombo 资产。
2. 四个正式 Humanoid 动画副本位于 Assets/_Game/Animations/Day11；新增 JumpAttack/WeaponSkill 状态并接入原有两套 Override。Hold/End 配套片段保留，目前采用单段动作，长距离坠落时保持末尾姿态至落地。
3. Q 键战技复用 PlayerMana、共享攻击执行和命中窗口；起手检查完成后扣费一次，可被受击/死亡打断，首版不能用闪避取消。最终手感和命中时机需人工审核。
4. 通过关卡中的动态手感验收后，按导入清单清理未采用动画和本次临时验证副本，保留实际使用的跳劈及必要配套片段、选定战技。

已修改战斗代码、正式 Animator 配置和 Input；未修改场景或玩家 prefab。详细验证证据和操作说明见 Day11_ImplementationReport.md。长剑唯一最终战技仍可由用户在两个认可候选中决定。
