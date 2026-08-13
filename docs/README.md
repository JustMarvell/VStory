# VR Story Game — Design Brainstorm Index

Unity **6000.3**, built from Unity's VR Project Template (XR Interaction Toolkit), targeting **Meta Quest 3** eventually. Currently developing with **XR Device Simulator** (no headset yet). Audio via **FMOD for Unity**.

## Game Concept

- Story-driven VR game (tone/scope reference: God of War, Genshin Impact, The Witcher — quest-driven story progression, not open sandbox).
- **3 separate stories** for now, structured as **3 levels** (Level 1 = Story A, Level 2 = Story B, Level 3 = Story C). Player must finish the current level to unlock the next.
- Each level/story is broken into **chapters**, which double as **checkpoints**.
- Story content is based on **local legends/folklore of the Sangihe archipelago** (North Sulawesi, Indonesia) — see `03-puzzle-design.md` for the specific folklore threads found and how they map to puzzle ideas.
- Core gameplay pillars:
  - **Melee combat** (sword)
  - **Ranged combat** (thrown rocks, blow dart)
  - **Dialogue system** — 2D UI floating in front of player, with planned voice-over via FMOD
  - **Puzzle quests**

## Documents in this set

| File | Covers |
|---|---|
| `01-combat-system.md` | Melee hit detection, weapon tracking, haptics, parry/block, enemy AI state machine & hit reactions, throwables + blow dart, the shared `DamageInfo`/`IDamageable` damage pipeline, enemy spawn/activation & encounter system |
| `02-game-architecture.md` | Data-driven level/chapter/checkpoint structure, save system, Addressables explainer, overall game flow (main menu → level select → play → return), how to build a multi-area test level |
| `03-puzzle-design.md` | Sangihe folklore research + 4 concrete puzzle concepts, VR-native puzzle design principles, shared `PuzzleManager` architecture |
| `04-dialogue-fmod.md` | World-space dialogue UI/comfort approaches, data-driven dialogue structure, FMOD wiring, subtitle/accessibility notes |

## Cross-cutting architectural principle (applies to every system above)

Every system in this project follows the same shape, established during the combat discussion and reused everywhere after:

- **Data lives in ScriptableObjects** (`EncounterDefinition`, `ChapterDefinition`, `DialogueSequence`, etc.) — not hardcoded in scene scripts.
- **Scenes stay "dumb."** Scene objects raise events / call into managers; they don't contain game logic themselves.
- **Small central managers** (`QuestManager`, `PuzzleManager`, `EncounterManager`, `SaveManager`) own state and completion logic. Gameplay components (a sword, a doll socket, a dialogue line) just report into these managers via a consistent pattern — usually a flag string or a small struct.
- **One shared "quest flag" system** (`QuestManager.SetFlag(string)`) is the common completion currency across combat encounters, puzzles, and dialogue — a chapter's completion check is just "are all required flags set?" regardless of what kind of content set them.

## Key open decisions already made (don't re-litigate without reason)

- **Death/respawn:** if the player dies mid-combat, the encounter **fully resets** (all enemies back to full HP) on respawn at the checkpoint — not partial persistence.
- **Weapon tracking:** start with **kinematic/tracked** weapon movement (XRI default), not physics-driven rigidbody weapons — revisit only if kinematic feels insufficient after testing.
- **Blocking model:** **physical block** (weapon-vs-weapon collision) is the base layer; **parry** is the same physical action but resolved as a stronger effect if timed within a window during the enemy's active-attack phase.
- **Build order philosophy (repeated across every system):** get the simplest/lowest-risk version working in isolation first, validate the shared architecture against it, then layer complexity — and build **one full vertical slice** (1 story, 1 chapter, 1 combat encounter, 1 puzzle, dialogue with placeholder VO, checkpoint/save working) before scaling to more content or more stories.
- **First puzzle to build:** the ward-placement puzzle (XRI socket interactors) — lowest risk, no custom physics/audio work.
- **Level layout for the test level:** open/free-roam (all 7 areas approachable in any order) recommended over gated/linear, at least for the first test level.

## Known risks flagged during brainstorming (worth remembering)

- **Scope risk:** 3 full stories × combat + puzzles + VO'd dialogue is a large scope. Repeated recommendation: build one vertical slice fully before expanding.
- **XR Device Simulator blind spots:** cannot validate haptics, real hand-tracking feel, motion sickness/comfort, or reaction-time-dependent mechanics (block/parry timing, blow-dart-by-breath). Plan an early real-hardware test pass specifically for melee/parry feel and comfort before those systems are considered "final."
- **VR-specific comfort rules used throughout:** move the XR Origin/rig root (not the camera directly) for teleport/checkpoint repositioning; avoid rigid head-locked UI; avoid instant object pop-in near the player (enemies, especially); avoid combining time pressure with fine motor precision in puzzles; keep "look up" requirements brief.
- **FMOD + Unity 6000.3 compatibility** should be checked directly against FMOD's current documentation before committing — wasn't independently verified during this brainstorm.