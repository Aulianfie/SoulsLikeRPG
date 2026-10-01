# Day12 红蓝瓶美术变体

## 资产

- 模型复用：`Assets/_Game/Prefabs/Items/PF_HealingFlask_Optimized.prefab`。
- HP 材质保留：`Assets/_Game/Materials/Items/M_HealingFlask_Optimized.mat`。
- MP 材质：`Assets/_Game/Materials/Items/M_MPFlask_Blue.mat`。复制原材质，仅替换 BaseMap，保留原 Normal、Metallic/Smoothness 与参数。
- MP BaseMap：`Assets/_Game/Textures/Items/MPFlask_BaseColor.png`。
- HP HUD 图标保留：`Assets/_Game/UI/Textures/HealingFlask.png`。
- MP HUD 图标：`Assets/_Game/UI/Textures/MPFlask_Icon.png`。
- Unity 实际模型对照：`Docs/Day12_FlaskModels.png`。

使用内置 imagegen 编辑原贴图和原 HUD 图标，输出复制到 G 盘项目。没有安装软件，没有修改系统配置。原 ThirdParty 源贴图未覆盖。

按用户要求，蓝色只用于原红色液体及相应反光，保留金色金属、棕色瓶塞和玻璃高光。已将贴图应用到原模型，在 Unity 中渲染红蓝对照，确认装饰和瓶塞保持原配色。生成源图为 1254×1254，模型贴图在 Unity 以最大 1024 导入，HUD 图标以最大 512 导入；HUD 图标包含真实 alpha。

## 贴图最终提示词

Use case: precise-object-edit. Edit target: the attached 1024x1024 game-model UV texture atlas. Produce a pixel-aligned color variant for the exact same model UVs: change ONLY red/crimson liquid color regions into rich sapphire/cobalt blue. Preserve ALL gold/brass metal ornament islands, brown cork/wood islands, neutral white/silver glass highlights, shadows, bubbles, surface detail, tiny islands, and the existing island boundary layout at the exact same positions and sizes. Preserve original canvas 1024x1024. No new shapes, no repacking UV islands, no text, no border, no bottle product rendering. This is a strict selective red-to-blue recolor of an existing texture atlas; non-red areas must remain unchanged.

## 图标最终提示词

Use case: precise-object-edit. Edit the attached red healing-flask HUD icon to a matching mana-flask icon. Change ONLY the red liquid and its red reflections to sapphire/cobalt BLUE. Keep the gold metal neck collar, cross crest and base rim GOLD; keep the cork BROWN; preserve glass highlights WHITE, exact bottle silhouette, all ornament details, camera angle, size and centered framing. Actual transparent background and no shadow background. Keep the same square canvas and padding as the reference. Do not make the whole bottle blue: gold and cork remain their original colors. No text or additional decoration.
