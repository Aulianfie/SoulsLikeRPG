# Day11 跳劈与战技实施记录

日期：2026-10-01。范围依据 `实现计划/Day11 跳劈和战技/SoulsLikeRPG_Day11_WeaponMoveset.md`；用户已授权继续实现至人工检查点。状态：功能及资产已实现；用户试玩反馈后已修复赐福回蓝和长剑早跳劈漏伤害，最新125项动作回归、74项真实敌人/赐福验收通过，最终人工手感验收待完成。修复详情见 Day11_RestAndDamageFix.md。

## 已实现

- `WeaponData → WeaponMoveset → LightCombo / JumpAttack / WeaponSkill`。保留原轻攻击资产和旧序列化字段，长剑五连、大剑三连继续使用原定义；Day10Setup 重跑时同步已有 Moveset 的 LightCombo。
- `PlayerAttackType` 区分 Light、Jump、WeaponSkill，统一复用 PlayerAttackState、PlayerCombat、PlayerAnimator 和 WeaponHitbox；伤害仍走已有成长与武器倍率。
- 空中 LMB 跳劈保留起跳水平速度，PlayerMotor 持续执行重力。每次腾空最多一次，包括被 Hurt 打断后的同次腾空；落地/复活重置。动画完成但尚未落地时保持末尾姿态，落地后完成收招。
- 地面 Locomotion 按 Q 发动当前武器战技，复用 PlayerMana 和已有 HUD 事件。配置/动画/资源检查通过后起手扣一次蓝；蓝量不足或启动失败不扣费。战技期间不能轻击、重复战技、喝药、换武器或闪避取消，可被 Hurt/Dead 打断。
- 同帧可执行输入的顺序：闪避、轻攻击、战技、喝药、换武器。已有起跳处理保留。退出攻击统一关闭命中窗口和清理执行状态。
- Base Layer 新增 JumpAttack/WeaponSkill，使用原 AOC_LongSword / AOC_GreatSword；原轻击 Override、ItemUse 和 WeaponSwitch 层保持原配置。场景和 Player prefab 无改动。

## 当前动作和参数

| 武器 / 动作 | 源动画 | 体力实际消耗 | MP | 命中窗口（归一化） | 完成点 / 额外后摇 |
|---|---|---:|---:|---|---|
| 长剑跳劈 | 1Hand_Base_Jump_Attack_1_InPlace | 25 | 0 | 0.32–0.85 | 0.90 / 0.12秒 |
| 大剑跳劈 | 2Hand_Up_Jump_Attack_InPlace | 33（22×1.5） | 0 | 0.33–0.60 | 0.78 / 0.16秒 |
| 长剑战技 | 1Hand_Up_Skill_3_InPlace | 0 | 20 | 0.36–0.60 | 0.90 / 0.12秒 |
| 大剑战技 | 2Hand_Base_Skill_1_InPlace | 0 | 35 | 0.33–0.62 | 0.90 / 0.20秒 |

参数是首版调试值，需按真实刀刃挥砍时刻及手感调整。四个正式 Humanoid `.anim` 副本位于 `Assets/_Game/Animations/Day11/`，源 FBX 保留在 `Assets/ThirdParty/DoubleL/Day11_Candidates/`。关闭 Root Motion，由代码控制角色碰撞体位移；Root Transform Rotation/Y/XZ 在动画副本中烘焙。长剑 Base Skill 4 继续保留为用户认可的替换候选。Hold/End 候选尚未接入，是否需要独立落地段由人工体验决定。

## 验证证据

- 先独立完成迁移，再添加新行为：`Logs/Day11/Migration_RuntimeValidation.txt`，32项真实 Play Mode 检查通过；检查原五连/三连、逐段体力消耗、命中窗口、武器切换和资源引用。迁移步骤还比对原轻击/Combo 资产字节未变化。
- 最终动作验收：`Logs/Day11/Actions_RuntimeValidation.txt`，124项真实 Play Mode 检查全部通过；`actions_validation.log` 记录 `Actions_VALIDATION_PASS checks=124`。覆盖原轻击连招、两把武器新动作和实际模拟键盘Q绑定、扣蓝一次与MP HUD、资源不足/配置缺失无扣费、真实碰撞伤害单目标去重、跳劈移动与重力、腾空受击后的重复跳劈拒绝、落地后再跳、受击/死亡打断、输入优先级、喝药、实际死亡淡出/复活、合法存档、锁定敌人和战技转向。
- 最终资产检查：`Logs/Day11/final_assets.txt` 和 `final_assets.log`，通过原 LightCombo 引用、四个动作定义/Override、Humanoid 根运动烘焙、动画层及 Player prefab Avatar 检查；最终编辑器工具编译成功。回写资产及修改后的运行时代码与验收副本逐文件校验一致，Git 确认场景/prefab/原轻攻击资产未改动。
- Unity MCP 当前不可连接；使用同版本 Unity 编辑器，在 `G:/Unity Project/SoulsLikeRPG/_codex_day11_review` 副本上导入、编译并进入真实 Play Mode，使用实际关卡 Player、Animator、CharacterController 和 WeaponHitbox。只将本次目标资产及 `.meta` 回写主项目。主编辑器尚未完成刷新后的现场验证。
- 验收备份并逐字节恢复原 `checkpoint_save.json`，验证对象只在 Play Mode 创建，不写入场景。批处理的模拟键盘采用测试专用输入路由；不修改主项目 InputSettings 或系统配置。
- 未执行独立 Player Build、物理手柄和最终人工视觉/手感检查。Q 为首版键鼠战技输入，未新增手柄绑定。
- 编辑器编译无 Error；现有环境出现 CS1668 警告（LIB 环境变量引用了不存在的 Visual Studio lib 目录）。本次未修改环境变量或安装工具。主项目刷新后的 Console 仍需现场确认。

## 现在需要人工检查

1. 主 Unity 编辑器退出当前 Play Mode，再执行 Assets → Refresh，等待导入编译结束。正式配置已生成，通常不需要重跑 Setup。
2. 打开 `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity` 进入 Play Mode；鼠标滚轮切换长剑/大剑。
3. Space 起跳，腾空后按 LMB；分别试原地、移动起跳和下落中跳劈，检查动画衔接、刀刃命中时刻、脚/刀尖穿地及明显滞空。
4. 地面按 Q，检查两把武器动画、MP20/35、前摇、受击打断、锁定朝向和收招后恢复控制。长剑 Up Skill 3 不满意时可改用已保留的 Base Skill 4。
5. 连续尝试轻击连招 → 闪避 → 起跳/跳劈 → 滚轮换武器 → Q战技 → R喝药 → 中键锁定后攻击，并验证死亡复活。记录哪把武器、哪个动作和具体问题即可。

自动验收可在已保存的 Edit Mode 从 `Tools → SoulsLike RPG → Day11 → 3 Validate All Actions (Play Mode)` 重跑。该工具会切换到验收关卡、运行测试并恢复存档；运行前保存场景。动画候选审核入口仍为同菜单下 Animation Candidates。

Day11 文档明确要求自动验收后继续人工手感检查，因此人工检查通过前不标记 Day11 完全完成。候选和验证副本暂不删除，等用户审核后再按清单清理。
