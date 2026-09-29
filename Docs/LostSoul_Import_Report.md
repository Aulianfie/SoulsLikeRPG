# LostSoul 模型导入记录

## 原始资产与优化结果

来源：用户提供的 `D:/下载/LostSoul.fbx`。未附授权文件；本记录不推定第三方许可类型。原始 FBX 未修改，未在项目中重复导入高面数版本。

| 指标 | 原始模型 | 游戏副本 |
| --- | ---: | ---: |
| 三角面 | 1,499,654 | 11,998 |
| Blender 顶点 | 749,829 | 6,001 |
| FBX 文件大小 | 112,462,140 字节，约 107.25 MiB | 453,660 字节，约 443.03 KiB |
| 贴图实际导入尺寸 | 原图 4096×4096 | Windows 下 1024×1024 |

减面通过现有 `G:/Blender/blender.exe` 完成，没有安装软件或修改系统配置。Blender 原始 / 优化同角度渲染对比已检查，主要轮廓与晶体纹理保留；高频几何细节有所简化。

**443 KiB 是优化 FBX 的大小，不包含贴图。** 原始 FBX 内嵌的四张 PNG 分离保存在 ThirdParty 下，供以后调整质量。运行材质引用 Base Color、Normal、Metallic / Smoothness 共三张贴图，Windows 使用 BC7 / BC5 / BC7 与 Mipmaps；粗糙度通过 `Smoothness = 1 - Roughness` 转换，并与金属度打包。

## 导入与接入

- 优化 FBX：`Assets/ThirdParty/LostSoul/Models/LostSoul_Game.fbx`。
- 原始贴图：`Assets/ThirdParty/LostSoul/Textures/`。
- 派生打包贴图：`Assets/_Game/Textures/SoulDrop/LostSoul_MetallicSmoothness.png`。
- URP 材质：`Assets/_Game/Materials/M_LostSoul.mat`。
- FBX 使用中等 Mesh Compression、关闭 Read/Write、动画、骨骼模式、导入材质与自动碰撞器。Unity 顶点因 UV / 法线拆分为 11,157，三角面仍为 11,998。
- 已替换 `Assets/_Game/Prefabs/World/SoulDrop.prefab` 的 VisualRoot 子模型，保留根节点 GUID、SoulDrop.cs、VisualRoot 与独立 SphereCollider。

```text
SoulDrop
├── VisualRoot
│   └── SoulModel        [LostSoul_Game.fbx + M_LostSoul]
└── Collider             [原 SphereCollider，Is Trigger]
```

模型高度约 0.65 米，中心对齐原碰撞器中心。场景 `Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity` 新增 `SoulDrop_ModelPreview`，放在 `checkpoint_dungeon_03` 附近供预览。此实例是视觉展示，尚未接入死亡生成或拾取行为。

后续更新：用户将根节点缩放设为 2 倍，已保留。死亡生成与拾取现已实现，静态预览已移除；当前使用运行时掉落实例。详见 [死亡掉魂说明](DeathSoulDrop_Implementation.md)。

重复接入菜单：`Tools > SoulsLike RPG > Day8 > Apply LostSoul Model`；模型检查菜单：`Tools > SoulsLike RPG > Day8 > Validate LostSoul Model`。本次已经执行并保存。

## 验证与产物

- Unity 编译通过，模型导入、材质、法线贴图和贴图压缩通过检查。
- 已检查 Unity 场景中的前、后、左、右四个方向，材质可正常显示。
- 模型检查结果：`Logs/LostSoul_UnityValidation.txt`；减面记录：`Logs/LostSoul_Optimization.json`。
- 预览：`Logs/LostSoul_Source_Preview.png`、`Logs/LostSoul_Optimized_Preview.png`、`Logs/LostSoulUnity/LostSoul_Unity_Preview_1.png`。
- Task8–9 验证器已支持替换后的任意 MeshRenderer 子模型，仍检查视觉与碰撞解耦。
- 替换模型后 Task8–9 的 42 项检查再次通过，验证结束时已恢复原始存档字节。
- 未执行独立 Player Build 或 FPS 对比；面数、文件大小和纹理导入结果是此次优化的实测依据。
