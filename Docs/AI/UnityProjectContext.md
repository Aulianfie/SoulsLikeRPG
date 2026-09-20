# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `G:/Unity Project/SoulsLikeRPG/SoulsLikeRPG`
- Purpose: third-person Souls-like RPG combat demo; current scope is Day3 combat-system work, implemented one task at a time.
- Last analyzed: 2026-09-20
- Last analyzed commit: `eaa28dc`

## Confirmed Environment

- Unity version: `2022.3.62f3c1` (`1623fc0bbb97`)
- Render pipeline: Universal Render Pipeline 14.0.12
- Input system: Unity Input System 1.14.2 is installed; Active Input Handling is `Both` so Day1 can use the new system while the Day0 legacy debug tester remains functional
- Target platform: Standalone Windows 64-bit

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP 14.0.12 | Confirmed | `Packages/manifest.json`, `Assets/Settings/` |
| Unity automation | CoplayDev Unity MCP embedded package | Confirmed | `Packages/manifest.json`, `Packages/MCPForUnity/` |
| Tests | Unity Test Framework 1.1.33 | Confirmed | `Packages/manifest.json` |
| UI | uGUI 1.0.0 and TextMesh Pro 3.0.7 | Confirmed | `Packages/manifest.json` |
| Animation | Built-in Mecanim/Animator modules only | Confirmed | `Packages/manifest.json` |
| Input | Unity Input System 1.14.2 with `Both` backends enabled | Confirmed | `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/_Game/` | Project-owned gameplay, scenes, prefabs, configuration, and validation content | Confirmed | Repository inspection |
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

- A component-oriented player architecture is established: `PlayerStateMachine` owns plain-C# states, `PlayerInputReader` owns Input System requests, `PlayerMotor` owns movement, `PlayerAnimator` owns animation, and `PlayerCombat` owns attack timing.
- Combat hit detection uses `WeaponHitbox` with `Physics.OverlapBoxNonAlloc`; Day3 Task 1 adds the narrow `IDamageable` boundary and passes hit context through `DamageInfo`.
- Enemy combat feedback uses `EnemyStateMachine` with plain-C# `Idle`, `Hurt`, and `Dead` states. `EnemyHealth` owns HP, the state machine owns transitions, and `EnemyAnimator` owns animation playback.
- Player dodge uses `PlayerDodgeState`; `PlayerInputReader` buffers a one-shot Dodge request, `PlayerMotor` owns locked-direction code-driven movement and gravity, and `PlayerAnimator` owns the in-place Roll animation.
- Existing sample assets under `Assets/_Assets/` are unrelated and must not be treated as project architecture.

## Coding Conventions

- First-party scripts use the global namespace, one type per file, `PascalCase` types/methods, `_camelCase` private fields, and `[SerializeField] private` for Inspector data.
- Runtime responsibilities remain small and component-oriented; no global manager or service framework is present.

## Testing And Validation

- Unity Test Framework is installed.
- No first-party EditMode or PlayMode test cases were detected.
- Current validation relies on Unity Editor compilation, Console inspection, prefab/scene serialization checks, and manual Play Mode acceptance.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity connection and editor version | available | Live MCP instance `SoulsLikeRPG@78c87c5379d11f49` |
| Console read | available | Live Console queries succeed |
| Scene inspect/modify | available | Live `01_CombatTest` queries succeed |
| Build Settings read/modify | available | Live Build Settings read succeeded |
| GameObject/prefab/asset operations | available | CoplayDev Unity MCP tools |
| Animation/Animator operations | available | `manage_animation` tool |
| Tests | available but no project tests | Unity Test Framework and MCP test tools |

## Important Constraints

- Day3 work must follow `实现计划/Day3/Day3_Three_Agent_Tasks.md` sequentially and must not implement later tasks early.
- Day3 Task 3 implements only a basic Locomotion-to-Dodge transition. It intentionally excludes i-frames, stamina, cancel windows, roll attacks, backsteps, lock-on dodge, and Root Motion.
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
- `01_CombatTest` contains user-authored scene changes unrelated to Day3 Task 1; preserve its diff and do not treat it as Task 1 work.

## Source Files Inspected

- `AGENTS.md`
- `Day0.md`
- `Day0_ResourceChecklist.md`
- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/_Game/Scripts/Player/StateMachine/PlayerStateMachine.cs`
- `Assets/_Game/Scripts/Player/Input/PlayerInputReader.cs`
- `Assets/_Game/Scripts/Player/Combat/PlayerCombat.cs`
- `Assets/_Game/Scripts/Player/StateMachine/States/PlayerDodgeState.cs`
- `Assets/_Game/Scripts/Player/Movement/PlayerMotor.cs`
- `Assets/_Game/Scripts/Player/Animation/PlayerAnimator.cs`
- `Assets/_Game/Input/Player.inputactions`
- `Assets/_Game/Scripts/Combat/WeaponHitbox.cs`
- `Assets/_Game/Scripts/Combat/EnemyHealth.cs`
- `Assets/_Game/Scripts/Enemy/Animation/EnemyAnimator.cs`
- `Assets/_Game/Scripts/Enemy/StateMachine/EnemyStateMachine.cs`
- `实现计划/Day3/Day3_Three_Agent_Tasks.md`
- Live Unity MCP project/editor/scene/console resources

<!-- unity-onboarding:generated:end -->
