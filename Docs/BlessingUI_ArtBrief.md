# 赐福 UI 原创资源生成记录

生成方式：Codex 内置 image_gen。最终资源复制到项目内，不依赖生成工具缓存路径。
没有安装软件、修改系统配置或新增第三方下载。中文字体复用项目已有 Noto Sans CJK 字体源。

## 底板提示词

Create an ORIGINAL production game UI BACKGROUND TEXTURE, flat front-on, landscape aspect ratio 1536x1024. Entire image is a unified rectangular dark souls-like blessing menu panel, WITHOUT ANY TEXT, icons, buttons, menu entries, numbers, screenshots or game environment behind it. Full panel fills canvas, very thin restrained aged brass-gold double outer border inset 12px, small elegant corner ornaments. A SINGLE vertical fine gold divider at exactly 28 percent of image width separates a slender left navigation background from a spacious right content background, no other divider. Near-black charcoal slate surface, subtle worn paper/stone grain, soft translucent-looking dark tones. LEFT: upper 75 percent mostly empty dark space for actual Unity icons and text; bottom 25 percent has faint tasteful silhouettes of ruined stone arches, tiny broken gothic tower and bare branches in dark muted warm gray, quiet and subdued. RIGHT: very faint thin geometric concentric rings, crosshair and a tiny abstract ember sigil centered at x=64%,y=51%, very low contrast aged bronze, unobtrusive enough to place text over. Restrained thin line decoration only. Palette charcoal #111315, muted brass #947249. Crisp usable UI texture, no poster composition, no glow explosion, no bright gradients, no coins, no humanoid badge, no text, no watermark.

输出：`Assets/_Game/UI/Textures/Blessing/BlessingPanel.png`，Unity Sprite Single。

## 图标提示词

Create one ORIGINAL production UI ICON SPRITE SHEET for a dark fantasy RPG. Transparent background with real alpha. Square image, EXACT 3 columns x 3 rows regular grid of 9 cells. Each icon centered in its cell at (1/6,1/6), (1/2,1/6), (5/6,1/6), etc, same cell size, broad transparent padding (at least 20 percent each cell). Each icon single flat antique champagne gold color with restrained shading and confident clean 6px-equivalent lines, sharp legibility at 40px. No text, no cell borders, no labels, no backplates. Row 1 left: elegant upward FLAME rising from two horizontal elliptical BASE rings, abstract sacred ember, NOT a coin or circular badge. Row 1 center: small CAMPFIRE with flame and two crossed sticks. Row 1 right: front-facing HUMAN BODY silhouette with head, shoulders, torso, arms, legs and 4 small attribute nodes near shoulders and hips, NO enclosing circle or shield, clearly human not coin. Row 2 left: simple medieval HELMET with T-shaped visor. Row 2 center: open SPELL BOOK with three simple rune marks. Row 2 right: THREE round dots on a horizontal line, no surrounding circle. Row 3 left: stylized HEART with simple inner pulse mark. Row 3 center: running HUMAN figure, simple athletic pictogram. Row 3 right: flexing ARM pictogram. All nine icons same gold palette and visual weight. No photographic detail, no ornate heraldry, no coins, no extra decorations, no glow, no watermarks. Asset type: usable Unity UI icon atlas, not a poster or interface mockup.

输出：`Assets/_Game/UI/Textures/Blessing/BlessingIcons.png`，Unity Sprite Multiple，真实 alpha。
子图：`Ember / Rest / Attributes / Equipment / Skills / More / Vigor / Endurance / Strength`。
图标切分使用 Unity 导入器，未把整张概念图烘焙成按钮或文字。

高亮底板、细线、按钮框由 Unity Image 组合，可在预制体中独立调节尺寸和颜色。
