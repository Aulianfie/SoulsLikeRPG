# 左下角武器与道具 HUD

日期：2026-10-01；Unity 2022.3.62f3c1。

- 已组装并保存主场景 `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity`。
- 使用现有玩家 HUD Canvas：左侧 `WeaponSlotUI` 位于 `(48,64)`，右侧 `HealingFlaskUI` 位于 `(204,64)`；两槽均为 `144×144`，间隔 12，继续使用 CanvasScaler。
- 无顶部标题栏，不显示“武器”“道具”“血瓶”等名称；右侧只显示瓶数，例如 `3 / 3`。旧血瓶名称、背景与分隔线对象保留但禁用，避免破坏现有引用。
- 新武器预制体：`Assets/_Game/UI/Prefabs/PF_WeaponSlotUI.prefab`；重构已有 `PF_HealingFlaskUI.prefab`。
- `WeaponData` 新增可选 `Icon`。`WeaponSlotPresenter` 订阅 `PlayerEquipment.WeaponChanged`，`WeaponSlotView` 显示当前装备的图标；支持默认装备初始化、动画实际替换时更新、重新启用 HUD。没有武器或图标时隐藏图标、保留空槽框。
- 已为 `WD_LongSword` 和 `WD_GreatSword` 配置图标。框体采用内置 ImageGen 生成；两把剑图标从装备槽位模型烘焙为透明 PNG，优先保留场景中的模型外观。血瓶复用原 `HealingFlask.png`。
- 血瓶玩法与数量绑定继续使用原 `PlayerHealingFlask`、`HealingFlaskPresenter`、`HealingFlaskView`；这些脚本未为本次 HUD 修改。HUD 不拦截指针输入。

## 资源

全部位于 `Assets/_Game/UI/Textures/`，已导入为 Sprite，关闭 Mipmap，保留 Alpha，Unity 使用最大 512 纹理：

- `EquipmentSlot_Frame.png`
- `Weapon_LongSword_Icon.png`
- `Weapon_GreatSword_Icon.png`
- 复用 `HealingFlask.png`

框体生成工具：内置 ImageGen。提示词：单个正面方形暗黑幻想 RPG 道具槽框；古金/铜双层细边框、四角克制的哥特式装饰、暗黑内衬、框外真实透明；顶部连续边框；没有标题牌、文字、数字、武器或瓶子，中心留空供动态图标使用。

## 验证

- Unity Editor 实际编译通过。
- Play Mode 的 25 项 HUD 验收通过：启动数据绑定、无名称文本、相邻布局、禁用指针拦截、数量 1/0/补满与空瓶变暗、重新启用 HUD、四次长剑/大剑切换后图标与实际装备一致且仅一把武器启用、1920×1080 运行场景渲染。
- 切换过程中逐次检查图标是否仍对应 `CurrentWeapon`，没有提前显示尚未装备的武器。
- 验收结束恢复原用户存档字节并返回 Edit Mode。
- 最终重新导入图标后，额外验证保存状态、主场景/两份 UI 预制体无缺失脚本、武器图标与绑定引用有效；见 `Logs/EquipmentHUD_AssetValidation.txt`。
- 完整证据：`Logs/EquipmentHUD_Validation.txt`；局部美术预览：`Docs/EquipmentHUD_Preview.png`；运行场景渲染：`Docs/EquipmentHUD_GameView.png`。
- 本次 HUD 验收无运行错误。并行武器动画验收产生过赐福菜单 `NotoSansSC_Blessing` 缺少“返回”字形的警告；它不是槽位字体，本次没有修改该菜单字体。
- 最终编辑器状态：Edit Mode，主场景已保存，Console 0 错误、42 条赐福菜单 `NotoSansSC_Blessing` 缺少“返”“回”字形的重复警告。未修改系统或编辑器配置来压制这些警告。

无需手动接线。以后新增武器时在对应 `WeaponData` 的 Icon 字段指定 Sprite 即可。保留菜单工具 `Tools > SoulsLike RPG > UI > Build Weapon And Item Slots`；已有槽位时不会重复创建。尚未专门验收超宽屏或不同分辨率。

修改前 UI 预制体、主场景与武器数据备份位于 `Logs/EquipmentHUDBackup/`。本次没有安装软件、修改系统配置、下载第三方资源或更改 Packages / ProjectSettings。
