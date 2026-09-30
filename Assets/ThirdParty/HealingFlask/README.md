# 血瓶优化资源

用户自备源文件：`D:/下载/血瓶.fbx`。未修改原文件；授权沿用源资产许可，未新增下载。

`Models/HealingFlask_Optimized.fbx` 是静态游戏道具：1400 三角面、UV 保留，瓶高 0.18m、Y 向上、原点在瓶体包围盒中心。FBX 不嵌入贴图；请一起保留 Textures 文件夹。

- BaseColor：1024 JPG，sRGB。
- Normal：1024 PNG，由原 50000 面模型烘焙到低模，按 Unity Normal Map 导入。
- MetallicSmoothness：512 PNG，线性采样；R = Metallic，A = 1 − Roughness，供 URP/Lit 使用。

项目材质：`Assets/_Game/Materials/Items/M_HealingFlask_Optimized.mat`。
已配置材质的独立预制体：`Assets/_Game/Prefabs/Items/PF_HealingFlask_Optimized.prefab`。

替换方法：打开 `PF_HealingFlaskPlaceholder.prefab`，将上述独立预制体放为子对象，删除 BottleBody、Neck、Cork，保留原根对象和玩家 Held Bottle 引用。随后在玩家右手下调整挂点。当前场景未自动替换，方便用户按上一轮教程操作。

详细预算与验证：`Docs/HealingFlask_Optimization_Report.md`。
