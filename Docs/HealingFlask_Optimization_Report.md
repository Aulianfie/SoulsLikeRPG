# 血瓶模型优化记录

日期：2026-09-30。源文件：用户提供 `D:/下载/血瓶.fbx`，48,825,788 字节；原文件保留。使用已有 G 盘 Blender 4.3.2，无新软件安装或系统配置修改。

## 参照与预算

项目没有另一个同尺寸的血瓶，以同类容器 `Assets/Dark Fantasy Environment/Meshes/SM_vase_04.fbx` 作近似参照。该模型原始高度约 0.55m；本次血瓶统一为手持道具高度 0.18m。预算按资产文件体积设置，同时额外限制面数。

| 项目 | 参照容器 | 血瓶优化版 | 对比 |
| --- | ---: | ---: | --- |
| FBX 文件 | 74,272 字节 | 76,940 字节 | +3.59%，小于允许的 +30% |
| 三角面 | 1,112 | 1,400 | +25.90%，小于 +30% |
| 完整 FBX + 关联贴图文件集合 | 16,937,556 字节 | 1,934,829 字节 | 小于参照集合的 130% |

参照的三张 `M_vases` 贴图为多花瓶共享图集，表中统计的是完整关联贴图集合。FBX 单文件与面数也分别通过 130% 上限，便于评估单个血瓶的成本。最终精确字节数以 `HealingFlask_Optimization.json` 为准。

原 FBX 包含 1 个网格、25,002 顶点、50,000 三角面，4 张 4096 嵌入贴图占约 94% 文件体积。优化后的完整模型与贴图约 1.94 MB，较原始 FBX 减少约 96%。FBX 约 77 KB，其余为实际需要的独立贴图；资源包应整体保留。

## 已执行的优化

- Blender Decimate 减至 1400 三角面，保留 UV；未单纯缩小网格导入精度。
- 从高模向低模烘焙 1024 法线贴图，补偿曲率和细节的变化。
- 基础色由 4096 缩到 1024，保存为质量 90 的 JPG；法线 1024 PNG。
- 金属与粗糙度合并为 512 的 URP Metallic / Smoothness 图：R 金属度，A 平滑度。
- PNG 做无损存储优化；贴图在 Unity 使用压缩导入、Mipmaps，禁用 Read/Write。
- 模型统一米制尺寸、向上轴和中心原点；静态模型，不导入动画或碰撞体。
- 新建 URP/Lit 材质和独立预制体，未修改当前玩家、场景或占位血瓶预制体。

## 验证

- Blender 优化脚本退出码 0；已对比源模型与优化模型的同机位渲染。近看仍存在低模轮廓变化，保留瓶体、瓶口、塞子和徽章的主要形状。
- 抽样高模表面到低模的距离：95% 小于约 0.56mm，最大约 1.13mm，模型高度 180mm。这个测量是单向抽样，不代表完整双向 Hausdorff 误差。
- Unity 2022.3.62f3c1 实际导入：1713 顶点、1400 三角面，Bounds 约 `(0.14, 0.18, 0.10)m`；导入后的顶点数包含 UV / 法线拆分。
- 1024 基础色、1024 法线、512 合并贴图及 URP 材质验证通过；实际 Unity 离屏渲染见 `HealingFlask_UnityPreview.png`。
- 资产生成前后主场景均未修改，Console 0 错误 / 0 警告；未进行场景中的持瓶姿态验收，挂点仍需用户替换后调整。
- 原始文件与工作副本 SHA-256 相同；记录在 `HealingFlask_Optimization.json`。

数据：`HealingFlask_Optimization.json`；Unity 结果：`HealingFlask_UnityValidation.txt`。
处理脚本备份：`Logs/FlaskOptimizationTools/`。

## 交付

- 源资产：`Assets/ThirdParty/HealingFlask/Models/HealingFlask_Optimized.fbx` 与 `Textures/`。
- 可直接使用的预制体：`Assets/_Game/Prefabs/Items/PF_HealingFlask_Optimized.prefab`。
- 材质：`Assets/_Game/Materials/Items/M_HealingFlask_Optimized.mat`。
- 可移走的模型与贴图压缩包：`Exports/HealingFlask_Optimized.zip`；其中未包含项目里的 URP 材质和预制体，跨项目使用时按 README 配置材质。

替换当前占位时保留 `HealingFlaskPlaceholder` 根对象，将优化预制体作为其子对象，再删除原来的三个几何占位。血瓶参数、显示隐藏和回血代码继续复用。
