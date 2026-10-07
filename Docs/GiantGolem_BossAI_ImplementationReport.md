# Giant Golem Boss AI 实施报告

实施日期：2026-10-01。范围：已批准实施方案的 Task0–6，一阶段 Boss 与独立测试场景。功能已接线并通过自动验收；键盘/手柄完整战斗手感仍需人工试玩。

## 开始试玩

1. 打开 `Assets/_Game/Scenes/04_GiantGolem_BossTest.unity`，进入 Play Mode。场景中的 Boss 默认启用自动 AI，玩家从 Boss 前方约14米处出生。
2. 使用现有玩家移动、攻击、跳跃、翻滚与中键锁定。左右脚判断以 Boss 自身朝向为准。场景有三个障碍物，可检查冲刺停止和石头碎裂；左远处保留一个普通敌人用于回归。
3. 在 `Tools > SoulsLike RPG > Giant Golem > 3 Boss AI Debugger` 打开调试窗口，点击“查找当前场景 Boss”。窗口显示当前行为、距离、夹角、左右、技能阶段、上一次抽签概率、筛除原因和类别冷却。
4. 检查某一招时，关闭窗口中的“自动 AI”，点击对应技能。强制播放绕过距离与冷却筛选，死亡、目标失效与领地回位仍会中断。重置按钮恢复生命、出生位置、历史、冷却并清理所属石头/特效。
5. 玩家死亡会走现有死亡动画、淡入淡出、重生与世界重置。测试场景关闭 CheckpointManager 的进度读写，检查点休息依然生效，测试不会覆盖正式存档。

测试场景已生成；菜单中的“2 Build Boss Test Scene”只用于初次创建，存在场景时会拒绝覆盖，防止丢失手工修改。

## 实际资源

| 资源 | 路径 |
|---|---|
| Boss 场景 | `Assets/_Game/Scenes/04_GiantGolem_BossTest.unity` |
| Boss 预制体 | `Assets/_Game/Prefabs/Boss/PF_GiantGolemBoss.prefab` |
| 石头 / 碎裂 | `Assets/_Game/Prefabs/Boss/PF_GolemRock.prefab`、`PF_GolemRockBreak.prefab` |
| 10个技能配置 | `Assets/_Game/Configs/Boss/GiantGolem/SO_*.asset` |
| Animator 与13个派生动作 | `Assets/_Game/Animations/Boss/GiantGolem/` |
| 测试导航数据 | `Assets/_Game/Navigation/NM_GolemArena.asset` |
| URP 项目材质 | `Assets/_Game/Materials/Boss/` |
| 原始第三方包 | `Assets/ThirdParty/GiantGolemAnimSet/`，原GUID、FBX和导入设置保留 |

模型高度4.4米、生命1200、击杀奖励500魂。攻击与死亡派生动作不循环；Idle/Run循环。项目没有安装行为树插件或修改默认导航类型、系统配置。

## 运行逻辑

Boss 使用独立的 `BossBrain`，普通敌人继续 `EnemyStateMachine`。二者共享 EnemyHealth、EnemyAnimator、EnemyMotor、Targetable、EnemyReward、EnemyHealthBarUI 和检查点重置契约。

行为树优先级为：死亡 → 失去目标/越界回位 → 保持当前技能 → 保持快跑接近 → 调试暂停 → 索敌等待 → 收招间隔 → 动态选招。目标感知每0.12秒更新，路径每0.2秒更新；每个技能执行期间不重新抽签。

选择先过滤距离、夹角、左右、类别冷却、移动/投射障碍，再抽技能类别，最后抽类别内动作。普通四招共用一个类别预算，选一个动作完整执行后回到决策，不生成自动连段。左右脚踩地和跳跃落地统一为 `GroundSlam`，共享类别预算、冷却和历史惩罚；近距基础类别权重为55，按有效变体数量归一。正中0.45米死区只排除左右脚，Jump 仍可用；左右脚提交后不换脚、使用矩形伤害，Jump 保留前摇转向和圆形地波。侧面停留增益只作用于左右脚。共享冷却时长由实际启动的变体决定（左右脚7秒、Jump 12秒）。

默认近距类别边界3.2米、远距11米。无历史时远处快跑60%、投石40%；上一技能类别×0.35，最近三次每次同类别×0.7；连续两次相同普通动作且存在替代动作时，该动作暂时排除。左右侧/远处停留增益封顶；冲刺或旋转后略提高普通攻击权重。所有历史和冷却保存在每个 Boss 实例的 Blackboard，技能配置不保存运行状态。

普通攻击通过实际手部 WeaponHitbox；双手共享本技能的命中集合。冲刺/旋转关闭普通 Agent 跟随，沿锁定方向程序移动，检查导航边界和胶囊障碍，结束或中断后恢复 Agent 权威。身体伤害沿路径补采样，同目标每招最多一次。

踩踏/跳攻在实测落脚时刻触发一次地波。玩家脚底高度超过所站地面0.55米可避开；下台阶短暂离地仍可受伤，翻滚沿用现有无敌帧。圆环表示本次瞬时伤害范围，未制作扩散伤害环。

投石在0.60归一化时间锁定目标位置、0.68生成一个石头，使用固定重力弹道。连续球体扫掠及初始重叠检查处理碰撞，忽略 Boss 本体，命中/碰墙/碰地后碎裂并 Destroy，未命中有8秒寿命。普通 Abort 保留已释放石头；Boss/玩家死亡、回位、禁用、检查点恢复会清理该 Boss 所属投射物与效果。

详细测量和初始伤害/半径/位移参数见 `GiantGolem_BossSkillTiming.md`。调参修改对应 `SO_*.asset`；AI 规则修改 BossBrain 的 Selection 参数。

## 共享代码改动

| 文件 | 本次职责变化 |
|---|---|
| EnemyHealth | 通过 DamageTaken 事件通知行为控制器，取消对小怪 FSM 的直接依赖；原生命/死亡/复活事件保留 |
| EnemyStateMachine | 订阅/取消订阅伤害事件，实现 ICheckpointResettable；小怪 Hurt/Dead 保持原流程 |
| EnemyAnimator | 增加按状态 hash 播放与读归一化时间的入口，保留原方法 |
| EnemyMotor | 增加特殊移动、障碍检查和交还 NavMesh 控制权入口 |
| WeaponHitbox | 可选共享命中集合；玩家/小怪继续原 BeginAttack/EndAttack 入口 |
| CheckpointManager | 同场景收集重置契约对象；增加默认开启的进度保存开关，Boss 测试场景单独关闭 |

没有修改玩家伤害、翻滚、武器数据或玩家预制体。保留了 CheckpointManager 既有 Day12 消耗品/存档变化，不改变存档格式。主场景、玩家预制体及另外21个此前已修改文件/删除状态，与实施前的 SHA256/缺失状态一致。

## 验证证据与边界

最终机器可读结果：`GiantGolem_BossAI_Validation.json`。本次128项检查通过、0项失败，Play Mode与最终重新编译后Console均0错误/0警告。检查包含确定样本选招、真实 Play Mode、保存重载和临时入口清理后重新编译。

- 十招在真实 Animator 上完整执行，对站立玩家各结算一次：普通24、踩踏32、冲刺/旋转30、跳攻40、投石30。冲刺测试移动4.2米，旋转8米，之后 Agent 控制权恢复。
- 真实玩家跳跃/翻滚可避地波，无敌结束后受伤；短暂离地0.2米仍受伤，1.2米高度/范围外安全。
- 石头实际撞障碍碎裂销毁，寿命销毁，普通 Abort 保留已释放石头；玩家死亡后清理已飞出石头并实际重生。
- 现有锁定与武器可作用于 Boss；左右绕侧不改已提交脚侧，冲刺锁定后不追踪急转。
- Boss 死亡/禁用/再启用、检查点休息、玩家死亡恢复均验证；小怪 Hurt/Dead、重复启停通知、一次奖励、检查点恢复通过。
- 实测死亡/掉魂与恢复前后正式存档字节不变；实际重开场景后配置、导航、材质、死亡重生引用有效，无 Missing Script。
- 最终 Console 0错误/0警告。临时编辑器命令桥已移除，正式生成/测量/调试/验收菜单保留；没有向场景添加永久测试探针。

编辑器可复测菜单“4 Validate Boss AI”和“5 Validate Integration”。本次额外进行十招确定区间覆盖及保存重载；结果全部保存在同一JSON。`Logs/GiantGolem/SkillRuntime.csv`记录各招真实伤害和位移，`UserChangesPreserved.json`记录原有修改核对，`FinalEditorState.json`记录最终编译状态。日志目录不必提交，JSON报告可随项目保留。

预览：`GiantGolem_BossTest.png`是实际主相机渲染；`GiantGolem_BossGameView.png`包含玩家HUD，也包含当时编辑器开启的Gizmos/导航可视化，后者不是游戏美术特效。

仍需人工试玩的项目是连续战斗手感、自由绕侧时的镜头、跳滚时机与攻击预兆可读性。当前伤害/速度/后摇是初始配置。未实现二阶段、韧性计算/破防、处决、拖拽行为树编辑器、正式Boss专属血条或最终美术特效；投石尚无持石拾起道具表现。

后续场景接入（2026-10-02更正）：当前主场景是 `03_AncientDungeon_Checkpoint`，Boss已接入该场景，保留新版HUD、赐福、消耗品、存档与复活接线。在请求位置附近避开柱子，完成独立局部导航、真实寻路、HUD切瓶、检查点回位和存档字节恢复验证。玩家出生配置保留，调试窗口提供运行时传送至Boss测试点的按钮。见 `GiantGolem_MainSceneIntegration.md`。此前05副本基于较早的资源场景生成，保留供参考。

所有改动留在工作区，未自动提交。
