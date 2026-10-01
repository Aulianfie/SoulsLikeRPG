# Day11 动画候选审核报告

日期：2026-10-01。范围：本次仅查找、提取、导入和预览动画候选，等待你选择。

## 结果与审核入口

- 已新增 **16个完整动作候选 + 10个Hold/End配套片段**，FBX总计约23.37MiB。长剑跳劈3个（2套姿势+1个位移对照）、大剑跳劈3个；长剑/大剑战技各5个。
- 候选位于 `Assets/ThirdParty/DoubleL/Day11_Candidates/`，分为LongSword/GreatSword，再分JumpAttack/WeaponSkill。
- Unity菜单：`Tools > SoulsLike RPG > Day11 > Animation Candidates`。独立预览窗口使用当前`Player_Day1.prefab`的Humanoid Avatar和实际武器，支持切换、播放、拖动时间、旋转观察、定位FBX。无需进入Play Mode。
- 先退出当前Play Mode，刷新Project等待编译，再打开该菜单。候选已放入主项目，当前已打开的编辑器没有成功响应刷新，因此本次实际Unity导入与预览验证在G盘临时副本完成。
- 预览是临时克隆，并替换为易观察的蓝色身体、金色武器材质；未修改正式材质。预览关闭根运动，没有接入跳跃重力或攻击判定。
- 五帧序列图：`Logs/Day11/Previews/`，分别采样片段5%、27.5%、50%、72.5%、95%的时间位置。
- 正式PlayerCombat、状态机、输入、Animator Controller、武器配置、轻击动画、玩家预制体和游戏场景均未修改。

## 来源与现有项目检查

当前武器：`WD_LongSword.asset`与`WD_GreatSword.asset`；现有长剑轻击5段、大剑轻击3段。检查了项目`Assets/DoubleL/`、`Assets/ThirdParty/DoubleL/`、`Assets/_Game/Animations/`和Quaternius动画资源路径；本轮优先选用本地资源包中专门的Jump_Attack与Skill，而不复制现有轻击充当候选。

用户提供的本地资源包根目录：`G:/BaiduNetdiskDownload/DoubleL1/Animation/`。读取4个资源包索引，共5811个非Unreal的FBX/anim条目（含同动作不同形式，并非5811套独立动作）：

- One Hand Base：`RPGAnimations - One Hand Base1.11.unitypackage`
- One Hand Up：`RPGAnimations - One Hand Up.unitypackage`
- Two Hand Base：`RPGAnimations - Two Hand Base1.10.unitypackage`
- Two Hand Up：`RPGAnimations - Two Hand Up1.6.unitypackage`

来源为用户已下载的本地DoubleL/RPGAnimations资源；本次没有下载新资源。许可范围未独立核验。仅提取选中的26个FBX及导入元数据，没有整包导入或引入包内脚本/演示场景。

## Unity验证证据与边界

- 使用本机已有Unity 2022.3.62f3c1，在`G:/Unity Project/SoulsLikeRPG/_codex_day11_review`临时验证副本执行。候选设置为Humanoid并复用项目既有DoubleL T-Pose Avatar；实际采样目标为当前Player预制体的有效Humanoid Avatar。
- 26/26片段：`humanMotion=True`、时长大于0、目标Player Avatar有效。每片段采样61个时间点，另用启用Root Motion的连续采样记录根位移包围盒。
- 最终版本编译及验证完成：日志含`[Day11] VALIDATION_COMPLETE`和`Exiting batchmode successfully now!`，退出码0；没有C#编译错误。出现既有环境的CS1668 LIB路径警告，未修改系统配置。Unity启动许可握手日志出现错误后成功取得许可，不影响本次成功验证。
- 实际查看了16个完整候选的动作序列图；可见动作姿态正常变化，未见明显的骨骼爆开。五帧截图无法证明全过程没有穿模或扭腕，因此自然握持、双手贴柄、地面碰撞、与实际重力的同步均保留人工审核。
- Root位移是该Player上重定向后、启用Root Motion的采样范围（非净位移、非游戏世界移动距离）；InPlace是资源名称标签，不代表严格零位移。身体腾跃还会表现在Hips/腿部，即使根Y=0也可能有大幅身体升降。
- 验证副本不是完整关卡副本，本次未测试战斗系统回归、Build或游戏中的跳劈/战技。

## 建议先查看的顺序

1. 长剑跳劈：`1Hand_Base_Jump_Attack_1_InPlace`，再对比2号横斩。
2. 大剑跳劈：`2Hand_Up_Jump_Attack_InPlace`与`2Hand_Base_Jump_Attack_1_InPlace`。
3. 长剑战技：`1Hand_Up_Skill_3_InPlace`、`1Hand_Up_Skill_1_InPlace`。
4. 大剑战技：`2Hand_Up_Skill_4_InPlace`，再对比`2Hand_Base_Skill_1_InPlace`与3号前扑。

这只是审核优先级，最终动画由你选择；全部候选和配套片段保留，下一次根据你的选择再清理。

## LongSword/JumpAttack

### 1Hand_Base_Jump_Attack_1_InPlace

- 长度：1.000秒；InPlace标签：是；根水平范围：0.0341m；根竖直范围：0.0890m。
- 动作特点：单手上举后向下挥砍；腿部保持腾空姿势。
- 适用类型：LongSword/JumpAttack；审核优先级：优先查看。
- 推荐理由：典型跳劈轮廓，和地面轻击容易区分。
- 潜在问题：起止均为空中姿势，需要人工比较与现有起跳、落地的衔接。
- 来源：`RPGAnimations - One Hand Base1.11.unitypackage` 内 `Assets/DoubleL/FBX_Animations/One Hand Base/Jump/InPlace/1Hand_Base_Jump_Attack_1_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/LongSword/JumpAttack/1Hand_Base_Jump_Attack_1_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/1Hand_Base_Jump_Attack_1_InPlace.png)。

### 1Hand_Base_Jump_Attack_2_InPlace

- 长度：1.000秒；InPlace标签：是；根水平范围：0.0499m；根竖直范围：0.0438m。
- 动作特点：单手侧向挥砍、身体转向，腾空腿部姿势。
- 适用类型：LongSword/JumpAttack；审核优先级：对比候选。
- 推荐理由：空中攻击风格与1号有差异。
- 潜在问题：更偏空中横斩，未必符合你想要的向下跳劈。
- 来源：`RPGAnimations - One Hand Base1.11.unitypackage` 内 `Assets/DoubleL/FBX_Animations/One Hand Base/Jump/InPlace/1Hand_Base_Jump_Attack_2_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/LongSword/JumpAttack/1Hand_Base_Jump_Attack_2_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/1Hand_Base_Jump_Attack_2_InPlace.png)。

### 1Hand_Base_Jump_Attack_1

- 长度：1.000秒；InPlace标签：否；根水平范围：0.1646m；根竖直范围：0.0888m。
- 动作特点：1号跳劈的非InPlace版本。
- 适用类型：LongSword/JumpAttack；审核优先级：位移对照。
- 推荐理由：用于对比同一动作的位移差异。
- 潜在问题：不是第三套独立姿势；水平根位移比InPlace版大。
- 来源：`RPGAnimations - One Hand Base1.11.unitypackage` 内 `Assets/DoubleL/FBX_Animations/One Hand Base/Jump/1Hand_Base_Jump_Attack_1.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/LongSword/JumpAttack/1Hand_Base_Jump_Attack_1.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/1Hand_Base_Jump_Attack_1.png)。

## GreatSword/JumpAttack

### 2Hand_Base_Jump_Attack_1_InPlace

- 长度：1.000秒；InPlace标签：是；根水平范围：0.0754m；根竖直范围：0.1067m。
- 动作特点：双手将大剑举过头顶，再向下挥砍。
- 适用类型：GreatSword/JumpAttack；审核优先级：优先查看。
- 推荐理由：双手重武器跳劈辨识度明确。
- 潜在问题：左手握柄位置及落地刀尖高度仍需逐帧人工确认。
- 来源：`RPGAnimations - Two Hand Base1.10.unitypackage` 内 `Assets/DoubleL/FBX_Animations/Two Hand Base/Jump/InPlace/2Hand_Base_Jump_Attack_1_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/GreatSword/JumpAttack/2Hand_Base_Jump_Attack_1_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/2Hand_Base_Jump_Attack_1_InPlace.png)。

### 2Hand_Base_Jump_Attack_2_InPlace

- 长度：1.000秒；InPlace标签：是；根水平范围：0.0993m；根竖直范围：0.0453m。
- 动作特点：双手侧向挥砍，身体前倾。
- 适用类型：GreatSword/JumpAttack；审核优先级：对比候选。
- 推荐理由：比1号更偏横向攻击，适合做风格比较。
- 潜在问题：向下劈砍特征弱于1号；需审核双手握持。
- 来源：`RPGAnimations - Two Hand Base1.10.unitypackage` 内 `Assets/DoubleL/FBX_Animations/Two Hand Base/Jump/InPlace/2Hand_Base_Jump_Attack_2_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/GreatSword/JumpAttack/2Hand_Base_Jump_Attack_2_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/2Hand_Base_Jump_Attack_2_InPlace.png)。

### 2Hand_Up_Jump_Attack_InPlace

- 长度：1.000秒；InPlace标签：是；根水平范围：0.0799m；根竖直范围：0.1858m。
- 动作特点：高位持剑，双手上举后下砸。
- 适用类型：GreatSword/JumpAttack；审核优先级：优先查看。
- 推荐理由：大剑下劈轮廓清楚，和单手长剑区别明显。
- 潜在问题：剑尖下探幅度大，接入时要检查落地穿地与握柄。
- 来源：`RPGAnimations - Two Hand Up1.6.unitypackage` 内 `Assets/DoubleL/FBX_Animations/Two Hand Up/Jump/InPlace/2Hand_Up_Jump_Attack_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/GreatSword/JumpAttack/2Hand_Up_Jump_Attack_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/2Hand_Up_Jump_Attack_InPlace.png)。

## LongSword/WeaponSkill

### 1Hand_Base_Skill_1_InPlace

- 长度：2.667秒；InPlace标签：是；根水平范围：0.1090m；根竖直范围：0.0000m。
- 动作特点：跃起举剑后下斩，再低姿态恢复。
- 适用类型：LongSword/WeaponSkill；审核优先级：腾跃型备选。
- 推荐理由：动作幅度大，明显区别于普通连击。
- 潜在问题：身体动画有较大上升；不能仅把Root Motion关闭就认定运动匹配。
- 来源：`RPGAnimations - One Hand Base1.11.unitypackage` 内 `Assets/DoubleL/FBX_Animations/One Hand Base/Skills/InPlace/1Hand_Base_Skill_1_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/LongSword/WeaponSkill/1Hand_Base_Skill_1_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/1Hand_Base_Skill_1_InPlace.png)。

### 1Hand_Base_Skill_2_InPlace

- 长度：3.000秒；InPlace标签：是；根水平范围：0.1087m；根竖直范围：0.0000m。
- 动作特点：明显翻身腾跃后俯身挥剑。
- 适用类型：LongSword/WeaponSkill；审核优先级：翻身型备选。
- 推荐理由：辨识度高，适合偏华丽的战技风格。
- 潜在问题：约3秒且身体升降很大；不优先用于第一版简单地面战技。
- 来源：`RPGAnimations - One Hand Base1.11.unitypackage` 内 `Assets/DoubleL/FBX_Animations/One Hand Base/Skills/InPlace/1Hand_Base_Skill_2_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/LongSword/WeaponSkill/1Hand_Base_Skill_2_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/1Hand_Base_Skill_2_InPlace.png)。

### 1Hand_Base_Skill_4_InPlace

- 长度：3.067秒；InPlace标签：是；根水平范围：0.0527m；根竖直范围：0.0000m。
- 动作特点：踏步跃起后落下挥砍、低姿态收招。
- 适用类型：LongSword/WeaponSkill；审核优先级：腾跃型备选。
- 推荐理由：提供另一种较大幅度的技能表现。
- 潜在问题：较长，并包含明显腾跃；需决定是否接受这种战技风格。
- 来源：`RPGAnimations - One Hand Base1.11.unitypackage` 内 `Assets/DoubleL/FBX_Animations/One Hand Base/Skills/InPlace/1Hand_Base_Skill_4_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/LongSword/WeaponSkill/1Hand_Base_Skill_4_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/1Hand_Base_Skill_4_InPlace.png)。

### 1Hand_Up_Skill_1_InPlace

- 长度：3.333秒；InPlace标签：是；根水平范围：0.0717m；根竖直范围：0.0000m。
- 动作特点：高位起手，连续转向、踏步挥剑后回到持剑姿势。
- 适用类型：LongSword/WeaponSkill；审核优先级：优先比较。
- 推荐理由：比Base腾跃型更接近地面剑技，起手风格不同。
- 潜在问题：总长3.333秒；具体有效挥砍次数和收招长度要动态审核。
- 来源：`RPGAnimations - One Hand Up.unitypackage` 内 `Assets/DoubleL/FBX_Animations/One Hand Up/Skills/InPlace/1Hand_Up_Skill_1_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/LongSword/WeaponSkill/1Hand_Up_Skill_1_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/1Hand_Up_Skill_1_InPlace.png)。

### 1Hand_Up_Skill_3_InPlace

- 长度：2.667秒；InPlace标签：是；根水平范围：0.0661m；根竖直范围：0.0000m。
- 动作特点：转体蓄势后低姿态挥剑，再恢复高位持剑。
- 适用类型：LongSword/WeaponSkill；审核优先级：优先查看。
- 推荐理由：动作轮廓较集中，有独立战技的辨识度。
- 潜在问题：低位挥剑需要确认与地面、身体的距离。
- 来源：`RPGAnimations - One Hand Up.unitypackage` 内 `Assets/DoubleL/FBX_Animations/One Hand Up/Skills/InPlace/1Hand_Up_Skill_3_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/LongSword/WeaponSkill/1Hand_Up_Skill_3_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/1Hand_Up_Skill_3_InPlace.png)。

## GreatSword/WeaponSkill

### 2Hand_Base_Skill_1_InPlace

- 长度：2.833秒；InPlace标签：是；根水平范围：0.1104m；根竖直范围：0.0000m。
- 动作特点：跃起举剑、落下挥砍，再大幅摆剑收招。
- 适用类型：GreatSword/WeaponSkill；审核优先级：对比候选。
- 推荐理由：重武器动作幅度明显。
- 潜在问题：部分姿势松开副手，腾跃及较长收招需要人工接受。
- 来源：`RPGAnimations - Two Hand Base1.10.unitypackage` 内 `Assets/DoubleL/FBX_Animations/Two Hand Base/Skills/InPlace/2Hand_Base_Skill_1_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/GreatSword/WeaponSkill/2Hand_Base_Skill_1_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/2Hand_Base_Skill_1_InPlace.png)。

### 2Hand_Base_Skill_3_InPlace

- 长度：2.000秒；InPlace标签：是；根水平范围：0.0917m；根竖直范围：0.0000m。
- 动作特点：跃身前扑、剑向下探，再恢复持剑。
- 适用类型：GreatSword/WeaponSkill；审核优先级：突进型备选。
- 推荐理由：约2秒，具有明显的重武器前扑攻击特征。
- 潜在问题：扑身动作与实际角色位移需要匹配，刀尖高度需审核。
- 来源：`RPGAnimations - Two Hand Base1.10.unitypackage` 内 `Assets/DoubleL/FBX_Animations/Two Hand Base/Skills/InPlace/2Hand_Base_Skill_3_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/GreatSword/WeaponSkill/2Hand_Base_Skill_3_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/2Hand_Base_Skill_3_InPlace.png)。

### 2Hand_Base_Skill_4_InPlace

- 长度：3.333秒；InPlace标签：是；根水平范围：0.0762m；根竖直范围：0.0000m。
- 动作特点：踏步蓄势后举剑腾跃，再挥砍收招。
- 适用类型：GreatSword/WeaponSkill；审核优先级：腾跃型备选。
- 推荐理由：大幅度双手动作，可作为华丽重击对比。
- 潜在问题：总长3.333秒；有腾跃，不优先于较紧凑的地面候选。
- 来源：`RPGAnimations - Two Hand Base1.10.unitypackage` 内 `Assets/DoubleL/FBX_Animations/Two Hand Base/Skills/InPlace/2Hand_Base_Skill_4_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/GreatSword/WeaponSkill/2Hand_Base_Skill_4_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/2Hand_Base_Skill_4_InPlace.png)。

### 2Hand_Up_Skill_1_InPlace

- 长度：1.333秒；InPlace标签：是；根水平范围：0.0342m；根竖直范围：0.0000m。
- 动作特点：高位持剑下提膝、前踢式身体动作。
- 适用类型：GreatSword/WeaponSkill；审核优先级：低优先级。
- 推荐理由：1.333秒较短，用作动作风格对照。
- 潜在问题：五帧预览中剑的挥砍不突出；若选作踢击，现有刀刃Hitbox未必合适。
- 来源：`RPGAnimations - Two Hand Up1.6.unitypackage` 内 `Assets/DoubleL/FBX_Animations/Two Hand Up/Skills/InPlace/2Hand_Up_Skill_1_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/GreatSword/WeaponSkill/2Hand_Up_Skill_1_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/2Hand_Up_Skill_1_InPlace.png)。

### 2Hand_Up_Skill_4_InPlace

- 长度：2.300秒；InPlace标签：是；根水平范围：0.0908m；根竖直范围：0.0000m。
- 动作特点：高位持剑起手，前倾转体、低姿态挥剑，再收招。
- 适用类型：GreatSword/WeaponSkill；审核优先级：优先查看。
- 推荐理由：2.3秒，动作较集中，适合先审核的大剑战技。
- 潜在问题：需动态确认副手贴柄、前倾时的刀身穿体及转向。
- 来源：`RPGAnimations - Two Hand Up1.6.unitypackage` 内 `Assets/DoubleL/FBX_Animations/Two Hand Up/Skills/InPlace/2Hand_Up_Skill_4_InPlace.fbx`。
- 导入：`Assets/ThirdParty/DoubleL/Day11_Candidates/GreatSword/WeaponSkill/2Hand_Up_Skill_4_InPlace.fbx`。
- [查看五帧序列图](../Logs/Day11/Previews/2Hand_Up_Skill_4_InPlace.png)。

## 配套片段（不计为完整候选）

| 名称 | 时长/s | 根水平范围/m | 根竖直范围/m |
|---|---:|---:|---:|
| 1Hand_Base_Jump_Attack_1_Hold_InPlace | 0.533 | 0.0333 | 0.0889 |
| 1Hand_Base_Jump_Attack_1_End_InPlace | 1.167 | 0.0314 | 0.5417 |
| 1Hand_Base_Jump_Attack_2_Hold_InPlace | 0.533 | 0.0498 | 0.0423 |
| 1Hand_Base_Jump_Attack_2_End_InPlace | 1.167 | 0.0194 | 0.5140 |
| 2Hand_Base_Jump_Attack_1_Hold_InPlace | 0.633 | 0.0738 | 0.1067 |
| 2Hand_Base_Jump_Attack_1_End_InPlace | 1.167 | 0.0407 | 0.5385 |
| 2Hand_Base_Jump_Attack_2_Hold_InPlace | 0.500 | 0.0859 | 0.0458 |
| 2Hand_Base_Jump_Attack_2_End_InPlace | 1.167 | 0.0433 | 0.6079 |
| 2Hand_Up_Jump_Attack_Hold_InPlace | 0.500 | 0.0804 | 0.1843 |
| 2Hand_Up_Jump_Attack_End_InPlace | 1.167 | 0.0187 | 0.4749 |

Hold用于比较空中保持；End用于比较落地恢复。完整Jump_Attack通常从空中姿势开始并回到空中姿势，不等于完整“起跳到落地”流程。End片段测得约0.47~0.61m根竖直变化，后续如果由PlayerMotor控制重力，不应直接让该根位移叠加到游戏角色。

## 可追溯文件与下次清理范围

- `Docs/Day11_AnimationImportManifest.json`：26个候选/配套片段的原资源包、包内路径、原始资源ID、导入路径。
- `Docs/Day11_AnimationValidation.tsv`：全部时长、Humanoid/Avatar结果和采样指标。
- `Logs/Day11/unity_validation_final.log`：最后一次Unity编译/验证日志。
- `Logs/Day11/Previews/`：16个完整动作的五帧序列图。
- `Assets/_Game/Editor/Day11AnimationReview.cs`：独立编辑器审核窗口，可在选择完成后保留或删除。
- `G:/Unity Project/SoulsLikeRPG/_codex_day11_review/`：本次创建的临时验证副本，下次可以连同未选候选清理。
- 原下载目录、原有DoubleL资源、正式动画资产均保留。Logs按项目既有Git忽略规则未跟踪；重要来源和数字证据另存入Docs。

你审核后只需提供4项选择：LongSword JumpAttack、GreatSword JumpAttack、LongSword WeaponSkill、GreatSword WeaponSkill。可以留空，也可以保留多个待比较动作。

