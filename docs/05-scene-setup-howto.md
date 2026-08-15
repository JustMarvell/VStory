# Scene Setup How-To Reference

Quick reference for wiring up gameplay elements in a scene. Companion to `docs/01-04`.

---

## AreaTrigger (the universal "on player enter" building block)

Used to fire almost everything else below.

1. Create empty GameObject, name it `AreaTrigger_<description>` (e.g. `AreaTrigger_Combat1`).
2. Add **Box Collider** → check **Is Trigger**.
3. Size/position it to cover the entry zone.
4. Add `AreaTrigger` component.
5. Set **Area Id** (see ID guide below).
6. Wire **On Player Enter** → drag target GameObject → pick the method to call.

Requires: XR Origin (or its root) has a Collider + Rigidbody (kinematic) and is tagged `Player`.

---

## Dialogue

**Create the data:**
1. `Create > Dialogue > DialogueSequence` asset.
2. Set `Sequence Id`, add `Lines` (speaker + text each), set `Set Flag On Complete`.

**Scene setup (one panel can be reused for all dialogue in a scene):**
1. World-space `Canvas` (Render Mode: World Space), scaled down (~0.01), positioned in front of player spawn.
2. Two `TextMeshPro - Text (UI)` (speaker, body) + one `Button` ("Next") inside the Canvas.
3. Group Canvas under an empty parent → this is your `panelRoot`.
4. Empty GameObject `DialogueRunner` → add `DialogueRunner` component → assign `Speaker Text`, `Body Text`, `Panel Root`.
5. Button `OnClick()` → `DialogueRunner.Next()`.
6. Set panel inactive by default.

**Trigger it:** `AreaTrigger.On Player Enter` → `DialogueRunner.StartDialogue(sequence)` → assign the `DialogueSequence` asset.

---

## Puzzle (ward-placement pattern)

**PuzzleManager (one per scene, holds all puzzle definitions in that scene):**
1. Empty GameObject `PuzzleManager` → add `PuzzleManager` component.
2. `Puzzle Definitions` → add entry per puzzle → `Puzzle Id` + `Required Step Ids` (one per doll/socket needed to solve it).

**Per grabbable piece:**
1. Object with Rigidbody (not kinematic) + `XR Grab Interactable`.
2. Add `WardDoll` → set `Doll Id` (must match one of the `Required Step Ids`).

**Per socket:**
1. Empty GameObject at the placement position → add `XR Socket Interactor` (XRI built-in).
2. Add `WardSocket` → set `Puzzle Id` (matches `PuzzleManager` entry), `Required Doll Id`, and drag its own `XR Socket Interactor` into `Socket Interactor`.

Solved automatically fires `QuestManager.SetFlag($"{puzzleId}_solved")` once all required steps are filled.

---

## Combat Encounter

**Per enemy:**
1. Placeholder mesh + Collider on the `Enemy` layer.
2. Add `NavMeshAgent` (bake NavMesh first: `Window > AI > Navigation`, mark floor `Navigation Static`, Bake).
3. Add `EnemyController` → assign `Target` (Player-tagged transform) and `Body Renderer`.
4. Leave `Dormant` by default — do **not** call `Activate()` manually; the encounter does that.

**Encounter wrapper:**
1. Empty GameObject `Encounter_<name>` → add `EncounterManager`.
2. Set `Encounter Id`, add enemies to the `Enemies` list.

**Trigger it:** `AreaTrigger.On Player Enter` → `EncounterManager.BeginEncounter()`.

Clearing all enemies fires `QuestManager.SetFlag($"{encounterId}_cleared")`.

---

## Checkpoint

1. Create `ChapterDefinition` asset (`Create > Story > ChapterDefinition`) → set `Chapter Id`, `Story Id`.
2. Position XR Origin where you want the checkpoint, copy its Transform values into `Checkpoint Spawn Pos/Rot` on the asset, move XR Origin back.
3. Empty GameObject `CheckpointManager` → add component → assign `Xr Origin Root`.
4. `AreaTrigger.On Player Enter` → `CheckpointManager.SetCheckpoint(chapterAsset)`.

Respawn (via `GameManager.LoadChapter`, or manually for testing) calls `RespawnAtCheckpoint(chapterAsset)`.

---

## New Chapter Scene (real story chapter, separate scene)

1. Create scene, mark **Addressable** (select scene asset → check Addressable in Inspector).
2. Copy `XR Origin`, `XR Interaction Manager`, `EventSystem` in from an existing scene.
3. Add a `CheckpointManager` in the new scene.
4. Create/assign a `ChapterDefinition` asset → set `Scene Ref` to this scene.
5. Add the chapter to a `StoryDefinition`'s `Chapters` list.
6. Wire a LevelSelect button → `MenuButtonActions.SelectChapter(chapterAsset)`.

## New Standalone Level (bypasses story/unlock, e.g. Test Level)

1. Create scene, mark Addressable.
2. On `Bootstrap`'s `GameManager`, add a dedicated `AssetReference` field + `LoadX()` method (copy the `LoadTestLevel` pattern).
3. Wire a menu button directly to that method — no `ChapterDefinition`/`StoryDefinition` needed.

---

# ID Naming Guide

IDs are string keys into `QuestManager` flags and save data. **They must be unique across the whole project** (not just per-scene) since `SaveData` is one global dictionary.

## General rules

- **snake_case**, lowercase, no spaces.
- Prefix with the *scene or chapter* it belongs to when there's any chance of collision. Once you have more than one chapter reusing the same `puzzleId`/`encounterId` naming pattern (e.g. every chapter having a "puzzle1"), collisions will silently overwrite each other's save state.
- Suffix pattern for generated flags is fixed by the code, don't fight it: `{puzzleId}_solved`, `{encounterId}_cleared`, dialogue uses its own `setFlagOnComplete` directly (no auto-suffix).

## Per-field guide

| Field | Good | Bad | Why |
|---|---|---|---|
| `ChapterDefinition.chapterId` | `story_a_ch1` | `chapter1` | Bad version collides the moment Story B also has a "chapter1". |
| `ChapterDefinition.storyId` | `story_a` | `1` | Keep it a readable word, not a number — this is also used as a dictionary key and shown nowhere else for reference. |
| `EncounterManager.encounterId` | `story_a_ch1_combat1` or `tutorial_combat1` | `combat1` | Same collision risk — scope it to the chapter/level it lives in. |
| `PuzzleManager` → `puzzleId` | `story_a_ch1_puzzle1` | `puzzle1` | Same reasoning. |
| `WardDoll.dollId` / `WardSocket.requiredDollId` | `doll_fish`, `doll_fire` | `doll1`, `doll2` | These only need to be unique **within one puzzle**, not globally (they're not save-data keys, just matched locally) — but descriptive names avoid mismatch mistakes when wiring sockets in the Inspector. |
| `DialogueSequence.sequenceId` | `story_a_ch1_intro` | `dialogue1` | Same global-uniqueness reasoning. Also not currently used for anything except future debugging/logging — still worth keeping consistent. |
| `DialogueSequence.setFlagOnComplete` | `story_a_ch1_intro_talked` | `talked` | This *is* a global save-data key — collisions here mean two different conversations both silently mark each other "already seen." |
| `AreaTrigger.areaId` | `story_a_ch1_combat1_area` | `area1` | Currently unused by code (no gating logic reads it yet) — but keep the habit now so it's not a mess once something does read it later. |

## Quick decision test

Ask: *"If I search my whole project for this exact string, could it plausibly match something in a different chapter/level?"*
- If yes → prefix it with the chapter/level it belongs to.
- If no (e.g. `doll_fish` only ever matched locally within one puzzle's sockets) → short descriptive name is fine.

## Tutorial/Test Level convention (matches what we built)

Use `tutorial_` prefix for everything in the standalone test level, since it's not part of `story_a/b/c`:
- `tutorial_puzzle1`, `tutorial_combat1`, `tutorial_completed`, etc.