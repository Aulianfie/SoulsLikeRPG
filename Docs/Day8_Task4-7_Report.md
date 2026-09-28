# Day8 Task4–7 实现说明

已接入 `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity`，使用独立 UI Prefab `Assets/_Game/UI/Prefabs/PF_GraceProgressionUI.prefab`。

## 使用方式

1. 运行上述场景，击败敌人获得 Soul。
2. 靠近赐福按 E。交互动画完成后，先恢复玩家、激活赐福并重置敌人，再显示 Grace 菜单。
3. 选择 Level Up，选择 Vigor、Endurance 或 Strength，查看属性及收益预览，再点击 Confirm。
4. 升级面板的 Close / Escape 返回 Grace；Grace 的 Close / Escape 关闭菜单。菜单期间暂停世界，关闭后恢复玩家输入、相机输入和鼠标状态。

## Task4：成长与升级费用配置

配置资产：`Assets/_Game/Configs/Player/SO_PlayerProgression_Default.asset`。

- Vigor 控制最大 HP；Endurance 控制最大体力；Strength 控制实际攻击伤害倍率。
- Inspector 提供升级费用折线图：横轴是升级前的当前等级，纵轴是这一次升级所需 Soul。可在曲线编辑窗口添加、拖动节点；编辑后节点切线设置为直线。
- 启用 Use Upgrade Cost Curve 时使用图表；关闭或清空曲线时使用 `BaseUpgradeCost + 当前等级 * UpgradeCostPerLevel`。
- 超过最后一个节点时，按最后一段斜率继续计算。费用四舍五入，至少为 1，最大为整数上限。
- 新建配置默认线性规则为 `100 + Level * 50`，所以 1→2 为 150，10→11 为 600。现有资产保留了用户调整后的曲线，实际费用以 Inspector 的预览值为准。
- 修改 Base Cost / Cost Per Level 后，可用 Inspector 的“重建线性折线”按钮同步图表。

## Task5：升级业务

`PlayerProgression.TryUpgrade(StatType)` 负责检查资格和余额、调用钱包扣款、增加所选属性及等级、更新真实 HP / 体力 / 伤害倍率，并发送成长事件。余额不足、非法属性、死亡或数值已达整数上限时拒绝升级；扣款事件重入不会重复消费。UI 通过此入口升级。

## Task6–7：赐福菜单与升级面板

`CheckpointManager.CheckpointActivated` 在原有赐福流程完成后通知 `ProgressionPresenter` 打开菜单。

`GraceMenuUI` 提供 Level Up / Close；`LevelUpPanel` 显示 Soul、等级、所选属性的当前值与下一值、对应 HP / 体力 / 倍率收益、费用和确认状态。余额不足时禁用确认；钱包及成长事件立即刷新面板。

菜单支持鼠标按钮、EventSystem 导航和提交；Escape 或手柄 buttonEast 返回 / 关闭。Presenter 禁用或玩家死亡时也会释放暂停状态。保持了原场景的金币 HUD 位置。

## 验证记录

- Unity Editor 编译完成，最终 Console 没有错误。
- 编辑器验证菜单：`Tools > SoulsLike RPG > Day8 > Validate Task4-7 (Play Mode)`。
- 53 项自定义 Play Mode 检查通过，覆盖费用规则、真实属性更新、余额不足、扣款重入、真实赐福交互动画、敌人重置、UI 导航与点击、模拟键盘 Escape、暂停与输入恢复、禁用 / 死亡清理。
- 验证期间捕获的游戏错误 / 警告为 0。验证已恢复原存档字节，并退出 Play Mode；场景已保存。
- 验证结果：`Logs/Day8_Task4-7_Validation.json`；运行 Console：`Logs/Day8_Task4-7_RuntimeConsole.log`。
- 已检查 1920×1080 菜单截图：`Logs/Day8_GraceMenu_Preview.png`、`Logs/Day8_LevelUp_Preview.png`。
- 尚未运行独立 Player Build、实体手柄或多分辨率布局验证。

本记录范围为 Task4–7。后续 Task8 存档接入与 Task9 SoulDrop 占位已实现，见 [Day8_Task8-9_Report.md](Day8_Task8-9_Report.md)。
