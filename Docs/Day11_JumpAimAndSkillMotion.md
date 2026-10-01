# 跳劈锁定与战技前进调整

日期：2026-10-01。用户授权修改跳劈配合和战技前进，并要求提供两种战技的前进距离调整方式。

## 改动

- 跳劈前摇持续朝已有有效锁定目标平滑转向，允许目标处于更大的前方夹角（120°）。进入命中阶段后固定朝向，不在挥砍中继续追踪。起跳水平惯性、重力和单次腾空限制继续沿用。
- 腾空或跳劈中的已有锁定目标暂时移出屏幕，容忍0.35秒；地面出屏仍沿用原规则。死亡、超距离及遮挡检查继续执行。没有增加自动锁定或自动追赶位移。
- 长剑保留1Hand_Base_Jump_Attack_1_InPlace，但在抬刀进度0.25等待下落接近地面（0.9米内）后再挥砍。只暂停JumpAttack状态的播放速度，Motor继续执行重力；命中窗口改为0.32–0.62，完成点仍为0.90。等待期间关闭伤害窗口，受击/死亡退出后恢复播放参数。地面距离探测忽略Enemy层，敌人头部不会作为提前释放挥砍的地面依据。
- 大剑跳劈保留原动画及窗口。两种武器共用真实WeaponHitbox与伤害去重；没有扩大刀刃或敌人的碰撞体，没有加入自动命中、范围伤害或额外伤害触发。
- 战技通过AttackData的MoveDistance、MotionStart、MotionEnd在指定动画时段内平滑前进，使用CharacterController.Move处理墙体和身体碰撞，仍执行重力。前进方向在开始移动时确定，后续不跟随目标改变。收招/受击/死亡停止水平位移，无累积碰撞位移后的突然冲出。MP和命中结算沿用原逻辑。

## 两种战技怎么调整前进距离

在已退出Play Mode的Unity Project窗口中打开 `Assets/_Game/Configs/Weapons/`：

| 资产 | 用途 | Move Distance 默认值 | Motion Start | Motion End |
|---|---|---:|---:|---:|
| AD_LongSword_Skill.asset | 长剑战技 | 0.6米 | 0.18 | 0.50 |
| AD_GreatSword_Skill.asset | 大剑战技 | 0.8米 | 0.20 | 0.50 |

选中对应资产，在Inspector的 **Attack Motion (战技前进位移)** 区域修改 **Move Distance**。单位为Unity场景单位（本项目按米使用）：值越大，单次战技尝试前进得越远；0表示原地释放。遇到墙或敌人身体，实际距离可能短于配置值。

先只改Move Distance，每次增加或减少0.1–0.2米再试玩。希望位移早一点/晚一点开始时再改Motion Start；Motion End决定何时停止前进。Start/End是0–1归一化动画进度；距离不变时，缩短这个区间会提高前进速度，拉长则降低速度。不要通过修改Forward Impulse调距离，该字段仍为预留。

距离在每次战技起手时读取，Play Mode中修改会作用于下一次动作。需要保留的调参建议在Edit Mode修改并保存资产。Day11Setup只在旧资产首次升级时填写位移默认值，之后重跑Configure Selected Actions保留这三个位移参数（包括手动设为0）。

长剑跳劈额外参数在AD_LongSword_Jump.asset的 **Jump Strike Alignment** 区域：Align Jump Strike To Landing启用下降衔接；Jump Windup Hold Point决定抬刀停在哪一帧；Jump Strike Ground Distance决定离地多少米释放挥砍。这些参数先保留本次默认值，最终按真实动画与手感调整。

## 验证

- `Feel_RuntimeValidation.txt`：63项通过，包含两种战技空地精确前进、墙体阻挡、受击立即停止、收招无残留滑动、长剑头顶上方前摇不造成伤害、等待时重力继续、下降挥砍能命中、85°偏向锁定目标的前摇转向、短暂出屏保留锁定和挥砍后固定方向。
- 空地实测前进：长剑0.59998米、大剑0.79999米；测试墙阻挡后均只移动约0.22003米。实际值受CharacterController碰撞约束。
- `DamageFixed_RuntimeValidation.txt`：74项通过，覆盖实际场景敌人的24种武器/动作/距离/跳劈时机组合与赐福回蓝；战技测试现在保留玩家与敌人的实体碰撞。
- `Actions_RuntimeValidation.txt`：125项完整动作回归通过，包含原轻击、资源消耗、输入优先级、打断、死亡复活和回满MP。自动验证在同版本Unity副本中执行，原存档逐字节恢复；主编辑器MCP仍不可连接，需要退出Play Mode、Assets → Refresh后现场复核手感。

相关代码：PlayerAttackState、PlayerMotor、PlayerAnimator、PlayerTargeting、AttackData；Animator新增JumpAttackSpeed参数，只控制JumpAttack状态。场景、玩家Prefab、原轻击配置、武器/敌人碰撞体及两种战技的伤害/耗蓝均未改动。

自动重跑入口：`Tools → SoulsLike RPG → Day11 → Validate Jump Aim and Skill Motion`，运行前保存场景并退出Play Mode。
