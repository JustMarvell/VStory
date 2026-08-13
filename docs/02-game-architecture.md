# Game Architecture — Levels, Chapters, Checkpoints, Flow

## Core principle

Data-driven structure: a small number of ScriptableObject definitions + a single save-state object that scenes read from and write to. **Scenes stay dumb** — logic lives in central managers, not scattered scene-specific MonoBehaviours.

---

## 1. Data model

```csharp
[CreateAssetMenu(menuName = "Story/StoryDefinition")]
public class StoryDefinition : ScriptableObject
{
    public string storyId;              // "story_a"
    public string displayName;
    public List<ChapterDefinition> chapters;
    public StoryDefinition unlockRequirement; // null = always unlocked (Story A)
}

[CreateAssetMenu(menuName = "Story/ChapterDefinition")]
public class ChapterDefinition : ScriptableObject
{
    public string chapterId;            // "story_a_ch2"
    public AssetReference sceneRef;     // Addressables scene reference
    public Vector3 checkpointSpawnPos;
    public Quaternion checkpointSpawnRot;
    public List<QuestDefinition> requiredQuests; // must complete to advance
}
```

`AssetReference` (Addressables) instead of a hard scene reference — lets chapter scenes load/unload on demand rather than baking every story into the build settings scene list (unmanageable once real art/audio exist per chapter).

---

## 2. Save state

One flat save object, not per-scene state:

```csharp
[Serializable]
public class SaveData
{
    public Dictionary<string, bool> storiesUnlocked = new();
    public Dictionary<string, string> lastCheckpointPerStory = new(); // storyId -> chapterId
    public Dictionary<string, bool> questFlags = new();  // "story_a_ch2_puzzle1_solved"
    public Dictionary<string, int> inventoryCounts = new(); // "rock", "dart", etc.
}
```

- `JsonUtility` doesn't serialize `Dictionary` natively — either use a serializable list-of-pairs wrapper, or use **Newtonsoft.Json** (handles dictionaries directly). Decide this before shipping content, since retrofitting the save format later is painful.
- Write to `Application.persistentDataPath/save.json` **on checkpoint hit and on chapter completion**, not just on quit — VR players sometimes remove the headset mid-session rather than exiting cleanly.

---

## 3. Checkpoint flow

A checkpoint restores **position AND orientation** — a misaligned respawn rotation is disorienting in VR in a way it isn't on flatscreen.

```csharp
public void LoadCheckpoint(ChapterDefinition chapter)
{
    Addressables.LoadSceneAsync(chapter.sceneRef, LoadSceneMode.Single).Completed += handle =>
    {
        var xrOrigin = FindObjectOfType<XROrigin>();
        xrOrigin.transform.SetPositionAndRotation(chapter.checkpointSpawnPos, chapter.checkpointSpawnRot);
        ApplyQuestFlagsToScene(chapter.chapterId);
    };
}
```

- **Always move the XR Origin/rig root**, never the camera directly — the camera's local position is offset by real head height, moving it directly desyncs the play space.
- **Re-apply quest/environment state** on load (doors already opened, enemies already dead, dialogue already triggered) by reading `questFlags` — commonly forgotten, leads to checkpoints that "remember" progress numerically but not visually.

### Chapter-as-checkpoint granularity

Chapters double as checkpoints (no fine-grained mid-chapter autosave needed) — save-on-chapter-enter and save-on-chapter-complete is enough. If a chapter runs long enough that losing all progress on death feels punishing, add **local (non-persisted) sub-checkpoints** within the chapter later — decide chapter length first; skip this for the first vertical slice.

---

## 4. Level unlock logic

Centralized, not scattered condition checks:

```csharp
public bool IsStoryUnlocked(StoryDefinition story, SaveData save)
{
    if (story.unlockRequirement == null) return true;
    return save.storiesUnlocked.TryGetValue(story.unlockRequirement.storyId, out bool done) && done;
}
```

A central `StoryManager` listens for "chapter completed" events and checks whether it was the story's last chapter — individual chapter scripts don't need to know about story-level unlock logic.

---

## 5. Quest tracking (the shared completion currency)

```csharp
public class QuestManager : MonoBehaviour
{
    public static event Action<string> OnQuestFlagSet;

    public void SetFlag(string flagId)
    {
        SaveManager.Current.questFlags[flagId] = true;
        OnQuestFlagSet?.Invoke(flagId);
        SaveManager.SaveToDisk(); // debounce if too frequent
    }
}
```

Puzzle scripts, combat encounters, and dialogue nodes all call `QuestManager.SetFlag(...)` when their condition is met. A chapter's completion check is just "are all `requiredQuests` flags true?" — no direct references needed between systems.

---

## 6. Addressables — what it is and how to use it

**Problem it solves:** hard-referencing scenes/assets means Unity considers bundling everything referenced by any loaded scene — with 3 stories' worth of art/audio, this means either the whole game loads into memory at once, or you're manually managing fragile load-order code.

**What it is:** load/unload assets (scenes, prefabs, audio banks, textures) at runtime **by key/reference**, instead of hard-referencing them at build time.

**Practical use here:**
- Each chapter scene marked Addressable.
- `ChapterDefinition.sceneRef` is an `AssetReference` pointing to it.
- Entering a chapter: `Addressables.LoadSceneAsync(chapterDef.sceneRef)` — loads only that scene + dependencies.
- Leaving: unload it, freeing memory.

**First-time setup steps:**
1. Install the **Addressables** package (Package Manager → search "Addressables").
2. Open `Window > Asset Management > Addressables > Groups`.
3. Select an asset (scene/prefab) → check the **"Addressable"** checkbox in Inspector (assigns a default address, adds to default group).
4. Reference it via `public AssetReference sceneRef;` (or `AssetReferenceT<SceneAsset>` for stricter typing) — shows as an object picker for Addressable assets.
5. Load: `Addressables.LoadSceneAsync(sceneRef, LoadSceneMode.Single)` → returns `AsyncOperationHandle`, hook `.Completed`. Unload: `Addressables.UnloadSceneAsync(handle)`.

**Things to know early:**
- Loading is **async** — need a loading screen/transition state, not an instant scene-swap. This actually suits VR comfort anyway (a hard instant cut can be disorienting; a brief fade-to-black is standard).
- Don't over-fragment groups early — one group per story (or just "scenes" + "audio" groups) is plenty at this scope. Ignore remote content delivery/DLC features — irrelevant right now.
- Addressables and **FMOD banks are separate systems** — a chapter transition typically means "load Addressable scene" + "load FMOD bank for this chapter" as two parallel steps.
- Recommended: do a hands-on Addressables intro/sample project once before wiring it into the real architecture — clicks faster by doing than by reading.

---

## 7. Game flow: main menu → level select → play → return

```
[Bootstrap scene - always loaded first, never unloaded]
  → GameManager, SaveManager, AddressablesInitializer (DontDestroyOnLoad or never-unloaded root)
  → loads MainMenu scene additively

[MainMenu scene]
  → Press Play → loads LevelSelect scene (unload MainMenu)

[LevelSelect scene]
  → reads SaveData to know which stories are unlocked, shows UI accordingly
  → Select level → GameManager.LoadChapter(story.chapters[savedProgressIndex])

[Chapter scene, loaded via Addressables]
  → gameplay happens
  → on finish/quit: SaveManager writes to disk, GameManager unloads chapter, loads LevelSelect or MainMenu
```

- **Bootstrap scene**: holds persistent managers (`SaveManager`, `QuestManager`, `PuzzleManager`, etc.) that must survive scene loads/unloads — keeps the "dumb scenes, central managers" principle consistent.
- **Main menu (VR-specific):** a simple 3D environment with **world-space UI panels** (fits the Sangihe setting — a stylized shrine/dock), point-and-click via ray interactor, rather than a 2D overlay canvas (flat fullscreen menus feel awkward in VR — no "screen" to pin them to). Don't over-invest in menu art before the game itself works.
- **Level select:** reads `SaveData.storiesUnlocked` to gray out locked stories, shows chapter progress in the active story. V1 scope: play current chapter or replay a completed story from the start — full "jump to any chapter" replay is optional later.

---

## 8. Building a multi-area test level (3 dialogue + 2 combat + 2 puzzle areas)

**Step 1 — Greybox first.** Rough placeholder geometry for all 7 areas + connecting paths before any logic. Validates scale/walking distance in-headset-relevant terms early (VR space reads differently in-headset than in Scene view).

**Step 2 — Trigger-based area activation**, same event-driven pattern as everything else:

```csharp
public class AreaTrigger : MonoBehaviour
{
    public string areaId;
    public UnityEvent onPlayerEnter; // wired per-area in Inspector

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            onPlayerEnter.Invoke();
    }
}
```
- Dialogue areas → activates NPC's `DialogueRunner` (or just makes the NPC ray-interactable rather than auto-triggering).
- Combat areas → `EncounterManager.BeginEncounter()` (see `01-combat-system.md` §5). Keep enemies disabled/dormant by default — don't run all encounters' AI simultaneously across the whole level.
- Puzzle areas → enable puzzle interactables (sockets, dolls) — or leave always-active if there's no reason to gate them.

**Step 3 — Every area reports completion the same way**, via `QuestManager.SetFlag()`:
- Dialogue: last line → `setFlagOnComplete = "poi1_talked"`
- Combat: last enemy defeated → `QuestManager.SetFlag("combat1_cleared")`
- Puzzle: `PuzzleManager` completion → `QuestManager.SetFlag("puzzle1_solved")`

**Step 4 — Chapter completion** = all 7 flags present in `ChapterDefinition.requiredQuests`.

**Step 5 — Gating decision:**
- **Open/free-roam (recommended for the first test level):** all 7 areas approachable in any order; chapter completes when all flags are set. Simplest, validates that dialogue/combat/puzzle can coexist and correctly report into one shared system.
- **Gated/linear:** area 2 only unlocks after area 1's flag (locked door/invisible wall/NPC that won't appear yet) — needs a small `AreaGate` component checking flags. Only add if the story specifically needs forced ordering.

**Practical workflow:**
- Each area = an empty parent GameObject with a trigger collider (`Is Trigger` checked), `AreaTrigger`, and its content (NPC+panel, enemy spawns, or puzzle objects) as children — self-contained, easy to move/duplicate while blocking out.
- Use **Prefab Variants** for repeated structures (base "arena" prefab with spawn points, base "NPC + dialogue panel" prefab) so building area 2+ isn't starting from scratch.

---

## Build order (whole architecture)

1. **One story, one chapter, one checkpoint** fully working end to end (load scene, position rig, save/load JSON) before building a second chapter.
2. Add a second chapter + "advance to next chapter" transition, including re-applying quest flags on load.
3. Only then build the second/third **stories** — by this point it's mostly content work, not systems work.

**Scope reminder:** don't build all 3 stories in parallel. Get one full chapter — checkpoint, one combat encounter, one puzzle, dialogue — playable start to finish first.