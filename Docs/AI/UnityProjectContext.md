# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `G:/Unity Project/SoulsLikeRPG/SoulsLikeRPG`
- Purpose: third-person Souls-like RPG combat demo; current scope is Day0 infrastructure and asset validation only.
- Last analyzed: 2026-09-18
- Last analyzed commit: unavailable (no Git repository detected)

## Confirmed Environment

- Unity version: `2022.3.62f3c1` (`1623fc0bbb97`)
- Render pipeline: Universal Render Pipeline 14.0.12
- Input system: legacy Input Manager; the new Input System package is not installed
- Target platform: Standalone Windows 64-bit

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP 14.0.12 | Confirmed | `Packages/manifest.json`, `Assets/Settings/` |
| Unity automation | CoplayDev Unity MCP embedded package | Confirmed | `Packages/manifest.json`, `Packages/MCPForUnity/` |
| Tests | Unity Test Framework 1.1.33 | Confirmed | `Packages/manifest.json` |
| UI | uGUI 1.0.0 and TextMesh Pro 3.0.7 | Confirmed | `Packages/manifest.json` |
| Animation | Built-in Mecanim/Animator modules only | Confirmed | `Packages/manifest.json` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/_Game/` | Project-owned Day0 content | Confirmed | `Day0.md` |
| `Assets/ThirdParty/` | Imported third-party source assets | Confirmed | `AGENTS.md` |
| `Assets/Settings/` | URP renderer and quality assets | Confirmed | Repository inspection |
| `Assets/_Assets/` | Existing Kitchen Chaos sample content; unrelated to Day0 | Confirmed | Repository inspection |
| `Assets/CodeMonkeyFree/` | Existing third-party/editor sample content | Confirmed | Repository inspection |

## Assembly Boundaries

- No first-party `_Game` assembly definition exists yet.
- Existing assembly definitions belong to `CodeMonkeyFree` and its editor tooling.

## Scenes And Startup Flow

- Build scenes: `Assets/Scenes/SampleScene.unity` (index 0), `00_AnimationLab` (index 1), `01_CombatTest` (index 2), and `02_TrainingGround` (index 3); all enabled.
- Day0 scenes are stored under `Assets/_Game/Scenes/` and validate with zero missing scripts or broken prefabs.
- No first-party scene-loading flow was detected.

## Architecture

- No first-party gameplay architecture exists yet. Day0 added only an import utility, a content builder, and a small animation-test component.
- Existing sample assets must not be treated as project architecture.

## Coding Conventions

- Day0 scripts are small, debug/editor-only, and single-purpose. No gameplay managers or framework abstractions were introduced.

## Testing And Validation

- Unity Test Framework is installed.
- No first-party EditMode or PlayMode test cases were detected; Unity Test Runner completed with zero discovered tests.
- Day0 validation used Editor compilation, prefab/scene serialization checks, screenshots, live animation playback, and UAL2-to-UAL1 Humanoid retarget playback.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity connection and editor version | available | Live MCP instance `SoulsLikeRPG@78c87c5379d11f49` |
| Console read | available | Baseline returned 0 warnings/errors |
| Scene inspect/modify | available | Live `SampleScene` hierarchy read succeeded |
| Build Settings read/modify | available | Live Build Settings read succeeded |
| GameObject/prefab/asset operations | available | CoplayDev Unity MCP tools |
| Animation/Animator operations | available | `manage_animation` tool |
| Tests | available but no project tests | Unity Test Framework and MCP test tools |

## Important Constraints

- Day0 must not introduce combat, FSM, combo, damage, health, skills, enemy AI, lock-on, inventory, equipment, save, or quest systems.
- Third-party source assets stay under `Assets/ThirdParty/`; project-owned derivatives go under `Assets/_Game/`.
- Humanoid Avatar and retargeting must be tested rather than inferred from import success.
- Scene, prefab, Animator, Rig, and Console decisions remain with the main agent.

## Day0 Asset Decisions

- Character and animation source: Quaternius Universal Animation Library 1 and 2 (CC0).
- Weapon source: KayKit Fantasy Weapons Bits (CC0), wrapped as one-hand sword, heavy axe, and polearm prefabs.
- UAL1 male Avatar, UAL2 animation Avatar, and UAL2 female mannequin Avatar report `valid=True` and `human=True`.
- UAL2 `Sword_Regular_A` was visually played on the UAL1 player Avatar, confirming cross-source Humanoid retargeting for the sampled clip.
- Heavy and polearm attack assignments are provisional because the free animation set does not provide dedicated great-axe and spear movesets.

## Unknowns And Confidence

- Dedicated great-sword/great-axe and spear/polearm animation quality remains unverified; current placeholders are generic melee/sword clips.
- Repository has no Git metadata, so checkpoints cannot be committed automatically.

## Source Files Inspected

- `AGENTS.md`
- `Day0.md`
- `Day0_ResourceChecklist.md`
- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- Live Unity MCP project/editor/scene/console resources

<!-- unity-onboarding:generated:end -->
