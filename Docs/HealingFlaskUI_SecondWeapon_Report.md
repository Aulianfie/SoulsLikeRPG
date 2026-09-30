# 血瓶 UI 与第二把武器资源检查

日期：2026-09-30。项目：`G:/Unity Project/SoulsLikeRPG/SoulsLikeRPG`，Unity 2022.3.62f3c1。

后续状态更新：Day9 回血瓶玩法已接入主场景，HUD 显示当前 / 最大瓶数，`Item_Drink` 已在当前玩家实播，慢走喝药与受击取消已验证。详情见 `Docs/Day9_HealingFlask_Report.md`；以下最初的资源检查和 UI 制作记录保留为历史记录。

## 血瓶 UI

- 已创建 `Assets/_Game/UI/Prefabs/PF_HealingFlaskUI.prefab`：深色底、金色边框、透明血瓶图标、中文“血瓶”、默认数量 3。
- 图标：`Assets/_Game/UI/Textures/HealingFlask.png`。由内置 ImageGen 生成，保留透明通道；Unity 按 512 Sprite 导入。
- 字体：复用项目已有 Noto Sans CJK SC 源字体，新增独立静态图集 `Assets/_Game/UI/Fonts/NotoSansSC_Flask.asset`；未安装系统字体。
- 显示组件：`HealingFlaskView.SetCount(int)` 更新数量；0 时图标变暗，负数按 0 显示。当前数量只是 UI 预览，尚未接回血、消耗、输入或存档。
- 默认布局：Canvas 左下角，偏移 `(48,64)`，尺寸 `188×108`；不会拦截 UI 点击。
- 加入场景：解锁电脑并等待 Unity 导入后，执行 `Tools > SoulsLike RPG > UI > Build Healing Flask UI`。工具优先使用玩家 HUD 的 Canvas；已有同名实例时不重复创建。场景原本有未保存修改时保留未保存状态，由用户自行保存。
- 当前并未修改 `03_AncientDungeon_Checkpoint.unity`：桌面锁屏、现有 MCP 未连接，活动编辑器不能刷新。交付的是完整预制体及一键放入场景工具。
- 已在 G 盘独立 Unity 验证项目中编译、生成预制体、检查数量更新并渲染。结果：0 错误、0 警告，批处理退出码 0。尚未在主项目 Game View 实播。
- 预览：`Docs/HealingFlaskUI_Preview.png`；验证：`Logs/HealingFlaskUI_Validation.txt`、`Logs/HealingFlaskUI_Batch.log`。

图标生成提示：深色幻想 RPG 的单个回血瓶道具图标，圆形玻璃瓶、红色液体、软木塞、古金色瓶口与小型治疗十字标记，清晰轮廓、透明背景，无文字、数量或 UI 框。

## 第二把武器：优先考虑双手剑

检查目录：`G:/BaiduNetdiskDownload/DoubleL1/Animation`。只读取用户已有资源；未下载新第三方资产，未整包导入主项目。

| 资源 | 已确认的内容 | 判断 |
| --- | --- | --- |
| `RPGAnimations - Two Hand Base1.10.unitypackage` | 1183 个 Unity 用 FBX，涵盖待机、移动、Attack_A/B/C、翻滚、跳跃、防御、换武器等 | 最适合作为双手剑的首轮候选 |
| `RPGAnimations - Two Hand Up1.6.unitypackage` | 1048 个 Unity 用 FBX，含另一套双手攻击与移动 | 后续可比较持刀姿态；本次未做重定向抽样 |
| `RPGAnimations - One Hand Base1.11.unitypackage` | 1274 个 Unity 用 FBX，含单手攻击与盾动作 | 若第二把仍是单手武器，可复用；差异感较小 |
| `RPGAnimations - Bow.unitypackage` | 983 个 Unity 用 FBX，含弓攻击、移动，包内有 `SM_Prop_Bow_02.fbx` | 动画与模型候选齐全；本次未验证弓，射击玩法需要另做 |

以上计数不含 `FBX_Unreal_Animations`、Demo `.anim` 和文件夹，不能理解为全部已验证。

### 已在独立 Unity 项目抽样验证的双手动画

包内目录前缀：`Assets/DoubleL/FBX_Animations/Two Hand Base/`。

| 动画文件 | 包内位置 | 时长 | 循环 |
| --- | --- | --- | --- |
| `2Hand_Base_Attack_A_1_InPlace.fbx` | `Attack_A/InPlace/` | 1.833 秒 | 否 |
| `2Hand_Base_Attack_A_2_InPlace.fbx` | `Attack_A/InPlace/` | 1.667 秒 | 否 |
| `2Hand_Base_Attack_A_3_InPlace.fbx` | `Attack_A/InPlace/` | 1.833 秒 | 否 |
| `2Hand_Base_Stand_Idle_A_1.fbx` | `Movement/Idle/Idle/` | 2.000 秒 | 是 |

- 四个候选均导入为 Human，包含 Humanoid Motion，30 FPS。
- 动画采用 Copy From Other Avatar，引用包内 `Assets/DoubleL/Model/T-Pose.fbx`（GUID `af0adcb624545c24e8c836635d07b769`）。选取导入时要同时带上该模型及 `.meta`，不要只拿攻击 FBX。
- 共享 `T-PoseAvatar` 实际导入后 `isValid=True`、`isHuman=True`。
- 将每个候选在项目已有 UAL1 Humanoid 模型上采样 0、1/3、2/3 时刻；骨骼能随动作变化，未出现无效浮点变换。证明基础导入与跨来源姿势采样可用。
- 尚未对当前场景玩家做完整视觉验收、左手握柄、脚底滑动、攻击衔接、命中窗口或 Root Motion 位移验证；不能直接当成第二武器已经接入。后续优先用独立三段攻击，而不是整条长 Combo 动画。
- 原始路径位于 `Assets/DoubleL/`；正式导入主项目时源资产应归到 `Assets/ThirdParty/DoubleL/`，项目自建控制器与派生预制体仍放 `_Game`。

### 模型候选

| 模型或预制体 | 位置 / 状态 | 用途 |
| --- | --- | --- |
| `SM_Wep_Sword_03.fbx` | DoubleL 双手包 `Assets/DoubleL/Model/`；独立项目实际导入，1 个网格、280 三角面，网格边界约 `0.06×1.52×0.30m` | 双手剑候选，须检查外观、握柄朝向与材质依赖 |
| `Weapon_Heavy.prefab` / `axe_C.fbx` | 主项目 `_Game/Prefabs/Weapons/` / `ThirdParty/Weapons/KayKit_FantasyWeaponsBits/Models/` | 已有重斧，若选重武器可复用；双手剑动作是否适合斧头仍需视觉检查 |
| `Weapon_Polearm.prefab` / `halberd.fbx` | 主项目相同武器目录 | 已有长柄模型；本次未找到并验证专用长枪/戟动作，暂不作为首选 |
| `SM_sword_necroblade.prefab`、`SM_sword_bloodfang.prefab` | 主项目 `Assets/Dark Fantasy Environment/Prefabs/Weapons/` | 已有场景包剑模型，可比较美术风格；不凭名称认定双手握持适配 |
| `SM_helbard_Bloodthorn.prefab` | 主项目相同场景包目录 | 另一个现成长柄模型候选 |

未移动或修改现有场景包模型、玩家 Animator、武器预制体和战斗配置。

## 喝血瓶动画候选

补充检查：`RPGAnimations - Action Dead Pose1.5.unitypackage` 中有更值得优先检查的道具饮用动画 `Assets/DoubleL/FBX_Animations/Actions/Item/Item_Drink.fbx`。导入设置为 Humanoid、非循环，帧区间 0–100，引用同一 `T-PoseAvatar`；另有 `Item_Drink_Not.fbx`（0–115 帧）。未导入主项目，也未做这两段的视觉 / 重定向验证。上一轮重点验证的是下面的 NPC 饮用动作，漏掉了这两个候选；后续血瓶动作应优先比较 `Item_Drink`，不能只按 NPC 三段的 11.67 秒评估。

`RPGAnimations - NPC Actions1.2.unitypackage` 内包含：

| 文件 | 时长 | 说明 |
| --- | --- | --- |
| `Drinking_Stand_Start.fbx` | 2.667 秒 | 站立饮用起始 |
| `Drinking_Stand_Play_1.fbx` | 6.333 秒 | 站立饮用主体 |
| `Drinking_Stand_End.fbx` | 2.667 秒 | 收尾 |

包内目录为 `Assets/DoubleL/FBX_Animations/NPC/Drinking/`。三段均使用共享 `T-PoseAvatar`，已在 UAL1 模型上做相同的三时刻姿势采样。完整串联约 11.67 秒，更像 NPC 饮用，作为战斗血瓶动作需要裁剪或调速，并确认瓶子绑定哪只手；回血时刻尚未设定。另有 `Drinking_Stand_Play_2.fbx`，本次未抽样。

## 来源、验证与后续

- DoubleL 包均由用户预先提供于百度网盘下载目录，本次只在 G 盘 `Temp/HealingFlaskValidation/Assets/ThirdParty/DoubleL_Inspection/` 临时导入少量样本；主项目未新增这些第三方资产。
- 本次检查的双手 Base、双手 Up、NPC 包内路径清单未发现许可证 / EULA / README 文件，不推定为免费或 CC0；授权以用户已有购买或获取记录为准。
- 原始包未修改；主项目 Packages 与 ProjectSettings 未修改；未安装软件或修改系统设置。
- 动画与剑模型导入 / 采样证据：`Logs/SecondWeaponInspection.txt`、`Logs/SecondWeaponInspection_Batch.log`。
- 建议后续顺序：确定双手剑还是重斧 → 只导入选定样本和共享 Avatar 到 ThirdParty → 当前玩家视觉重定向及握柄验收 → 接第二武器；血瓶 UI 已有独立接口，可随后接数量与回血动作。
