# VR Story Game — Design Brainstorm Index

Unity **6000.3**, built from Unity's VR Project Template (XR Interaction Toolkit), targeting **Meta Quest 3** eventually. Currently developing with **XR Device Simulator** (no headset yet). Audio planned via **FMOD for Unity** (not yet integrated in code).

> **Status (this update):** first vertical slice is close to done. Repo: `JustMarvell/VStory`. Bootstrap, MainMenu, LevelSelect, TestLevel, and CombatTest scenes exist; Core/Combat/Dialogue/Puzzle script folders are implemented and largely match this design set. See **"Implementation status"** call-outs in each doc for what's built vs. still planned, and the **Known Gaps** section below for things worth fixing before calling the slice done.

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
| `01-combat-system.md` | Melee hit detection, weapon tracking, haptics, parry/block, enemy AI state machine & hit reactions, throwables + blow dart, the shared `DamageInfo`/`IDamageable` damage pipeline, enemy spawn/encounter system |
| `02-game-architecture.md` | Data-driven level/chapter/checkpoint structure, save system, Addressables explainer, overall game flow (main menu → level select → play → return), how to build a multi-area test level |
| `03-puzzle-design.md` | Sangihe folklore research + 4 concrete puzzle concepts, VR-native puzzle design principles, shared `PuzzleManager` architecture |
| `04-dialogue-fmod.md` | World-space dialogue UI/comfort approaches, data-driven dialogue structure, FMOD wiring plan, subtitle/accessibility notes *(new in this update — was discussed in chat but not previously written to file)* |

## Cross-cutting architectural principle (applies to every system above)

Every system in this project follows the same shape:

- **Data lives in ScriptableObjects** (`ChapterDefinition`, `StoryDefinition`, `DialogueSequence`, etc.) — not hardcoded in scene scripts.
- **Scenes stay "dumb."** Scene objects raise events / call into managers; they don't contain game logic themselves.
- **Small central managers** (`QuestManager`, `PuzzleManager`, `EncounterManager`, `SaveManager`, `GameManager`) own state and completion logic, and are `DontDestroyOnLoad` singletons living in the Bootstrap scene.
- **One shared "quest flag" system** (`QuestManager.SetFlag(string)`) is the common completion currency across combat encounters, puzzles, and dialogue.

This held up well in the actual implementation — confirmed while reviewing the repo.

## Key decisions (status as implemented)

- **Death/respawn:** encounter should **fully reset** on respawn — *decided, not yet wired up* (see Known Gaps).
- **Weapon tracking:** kinematic/tracked — **implemented** (`SwordWeapon.cs` tracks blade tip transform directly, no physics-driven chase).
- **Blocking model:** physical block base layer + timed parry on top — **not yet implemented**. `EnemyController` currently has no `Blocked`/`Staggered` state and enemies don't yet attack the player back.
- **Build order philosophy:** vertical slice first — **on track**, this is exactly the phase the project is in.
- **First puzzle built:** ward-placement puzzle — **implemented** (`WardSocket.cs`, `WardDoll.cs`, `PuzzleManager.cs`), using real XRI `XRSocketInteractor` API.
- **Test level layout:** open/free-roam via `AreaTrigger` — **implemented** (`AreaTrigger.cs`).

## Known Gaps / TODO (found during code review of the vertical slice)

These are the concrete next things to fix or finish, in rough priority order:

1. **Checkpoint respawn doesn't re-apply quest flags to the scene.** `CheckpointManager.RespawnAtCheckpoint` repositions the XR Origin but never restores already-solved puzzle/already-cleared encounter state. Right now, dying after clearing the ward puzzle and respawning would show the puzzle as unsolved again in the scene even though the flag is set. Needs an `ApplyQuestFlagsToScene`-style pass in `GameManager.LoadChapter`'s completion callback.
2. **Nothing currently sets `storiesUnlocked` or checks chapter completion.** `ChapterDefinition` has no `requiredQuests` list, and `StoryUnlockUtility.IsUnlocked` reads a dictionary that's never written to. Individual quest flags fire correctly, but nothing yet aggregates them into "chapter complete → next story unlocked."
3. **`inventoryCounts` in `SaveData` is dropped on save/load.** `SaveDataWrapper` has no corresponding fields, so it silently doesn't persist. Either wire it in or remove it until it's needed.
4. **Enemies don't yet damage the player.** `EnemyController`'s `Telegraph`/`AttackActive` states run on a timer but nothing applies damage to the player at the end of the active window — worth confirming this is deliberate sequencing (offense before defense) rather than an oversight.
5. **Parry/block and the `Blocked`/`Staggered` enemy states are not yet implemented** — `HitTier.Parried` and `HitTier.Killing` exist in the enum but are unused; only `Light`/`Heavy` are currently computed.

## Known risks flagged during brainstorming (still worth remembering)

- **Scope risk:** 3 full stories × combat + puzzles + VO'd dialogue is a large scope. Keep prioritizing finishing one vertical slice over starting new content.
- **XR Device Simulator blind spots:** cannot validate haptics, real hand-tracking feel, motion sickness/comfort, or reaction-time-dependent mechanics (block/parry timing, blow-dart-by-breath). Plan an early real-hardware test pass specifically for melee/parry feel and comfort once those systems exist.
- **VR-specific comfort rules used throughout:** move the XR Origin/rig root (not the camera directly) for teleport/checkpoint repositioning — already followed correctly in `CheckpointManager`; avoid rigid head-locked UI; avoid instant object pop-in near the player; avoid combining time pressure with fine motor precision in puzzles; keep "look up" requirements brief.
- **FMOD + Unity 6000.3 compatibility** should be checked directly against FMOD's current documentation before integration begins — not yet verified, and no FMOD code exists in the project yet.