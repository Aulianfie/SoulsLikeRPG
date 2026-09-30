# 赐福 UI 重构交付记录

日期：2026-10-01。Unity 2022.3.62f3c1，URP，uGUI + TextMesh Pro。

## 已完成

- 主场景：`Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity`。
- 新预制体：`Assets/_Game/UI/Prefabs/PF_BlessingUI.prefab`；场景实例名 `PF_BlessingUI`。
- 左侧导航：赐福火种图标、标题、休息 / 属性 / 装备 / 技能 / 更多；栏目选中金边与鼠标、键盘焦点分别显示。
- 右侧默认页为淡几何圆环背景；全部内容页位于同一个大面板内。
- 属性页展示金币、等级、生命力 / 耐力 / 力量、当前值与单项升级后预览、费用、确认升级和关闭。
- 装备页包含 `EquipmentContentSlots/ReservedSlot_0..3`，供后续接入装备系统。
- 技能和更多页包含独立标题与占位说明，可正常切换。
- 休息直接调用现有检查点逻辑，恢复生命、体力、血瓶并刷新世界与存档。
- 属性页关闭按钮关闭赐福界面；ESC / 手柄 B 先返回默认页，再按一次关闭。关闭后恢复原时间倍率、输入与光标状态。

## 代码职责

| 文件 | 职责 |
| --- | --- |
| `BlessingMenuRoot.cs` | 菜单展示、导航意图、返回默认页 |
| `BlessingNavigationView.cs` | 五个导航按钮与栏目选中反馈 |
| `BlessingContentController.cs` | 互斥展示默认页和五个内容页 |
| `BlessingPage.cs` | 栏目枚举 |
| `LevelUpPanel.cs` | 复用既有升级视图，增加金边选中态与统一样式 |
| `ProgressionPresenter.cs` | 复用原暂停、输入恢复、升级事件与刷新流程；兼容旧赐福 UI |
| `BlessingUIBuilder.cs` | 编辑器资源导入、字体、预制体生成与场景接线 |
| `BlessingUIPreview.cs` | 隔离预览场景与实际游戏场景截图 |
| `BlessingUIValidation.cs` | 定向 Play Mode 集成验收与存档恢复 |

`PlayerProgression / SoulWallet / PlayerProgressionConfig` 的升级规则未修改。新界面通过原来的 `GetUpgradePreview / TryUpgrade` 取得预览和提交升级。装备、技能、更多页当前只提供结构，不引入相应玩法系统或空的业务脚本。

## 资源

- `Assets/_Game/UI/Textures/Blessing/BlessingPanel.png`：原创深色底板、金边、废墟树影与几何圆环。
- `Assets/_Game/UI/Textures/Blessing/BlessingIcons.png`：原创透明九宫格图标，在 Unity 中切成九个 Sprite 并挂接。
- `Assets/_Game/UI/Fonts/NotoSansSC_Blessing.asset`：单独烘焙的静态中文图集，复用项目已有字体源。
- 生成方式与完整提示词：`Docs/BlessingUI_ArtBrief.md`。
- 没有新增第三方下载或安装软件，没有修改系统配置或项目设置。

## 验证

- Unity 已编译，最终 Console 错误与警告均为 0。
- 40 项 Play Mode 行为检查通过，包括检查点交互开菜单、五页切换、三项属性事务、金额刷新、金币不足保护、隐藏页不能扣金币、休息回血瓶、三轮反复开关、时间倍率恢复、ESC 与模拟手柄 B 返回。
- 预制体无 Missing Script，场景 Presenter 引用齐全，全部静态中文文案字形存在。
- 检查了 1920×1080、1280×720、1280×960、2560×1080 的面板预览，布局完整。
- 主场景迁移前的 1843 个序列化对象 ID 全部保留；新 UI 通过预制体实例添加。
- 原用户存档已恢复，按原始字节校验；没有保存测试中的金币与属性变化。
- 验收日志：`Logs/BlessingUI_RuntimeValidation.txt`、`Logs/BlessingUI_AssetValidation.txt`、`Logs/BlessingUI_EditorState.txt`。
- 实际场景截图：`Docs/BlessingUI_GameView_Default.png`、`Docs/BlessingUI_GameView_Attributes.png`。截图中的金币与属性可能来自测试过程。
- 尚未进行独立 Windows Player 构建或实体手柄测试。

## 回退与后续

旧场景对象 `GraceProgressionUI` 保留并停用，原 `PF_GraceProgressionUI.prefab` 保留。迁移前现场副本在 `Logs/BlessingUIBackup/SceneBeforeMigration.unity`。

现有主场景无需手动挂接即可使用。编辑器菜单 `Tools/SoulsLike RPG/UI/Validate Unified Blessing UI` 可再次执行定向验收，运行前保存场景并退出 Play Mode。

接装备系统时，在预制体的 `UnifiedPanel/Content/EquipmentPage/EquipmentContentSlots` 下扩展；技能与更多页分别使用 `SkillsPage`、`MorePage`。属性页继续复用 `LevelUpPanel` 与原 Presenter。
