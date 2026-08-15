# Game Architecture — Levels, Chapters, Checkpoints, Flow

> **Implementation status summary:** the data model, save system, Addressables-based scene loading, and the test level's trigger-based area activation are **implemented** in `Scripts/Core` and match the original design closely. The two real gaps are (1) checkpoint respawn doesn't re-apply quest-flag state to the scene, and (2) nothing yet aggregates chapter completion into story unlock. Both are flagged inline below and in the README's Known Gaps list.

## Core principle

Data-driven structure: a small number of ScriptableObject definitions + a single save-state object that scenes read from and write to. **Scenes stay dumb** — logic lives in central managers, not scattered scene-specific MonoBehaviours. This held up in practice — confirmed against the real repo.

---

## 1. Data model — ✅ Implemented

```csharp
// StoryDefinition.cs
[CreateAssetMenu(menuName = "Story/StoryDefinition")]
public class StoryDefinition : ScriptableObject
{
    public string storyId;
    public string displayName;
    public List<ChapterDefinition> chapters;
    public StoryDefinition unlockRequirement; // null = always unlocked (Story A)
}

// ChapterDefinition.cs
[CreateAssetMenu(menuName = "Story/ChapterDefinition")]
public class ChapterDefinition : ScriptableObject
{
    public string chapterId;
    public string storyId;
    public Vector3 checkpointSpawnPos;
    public Quaternion checkpointSpawnRot;
    public AssetReference sceneRef;
}
```

Matches the plan closely. One deliberate difference: `ChapterDefinition` currently has **no `requiredQuests` list** — this is the missing piece for chapter-completion tracking (see §4/§5 gaps below). Adding a `List<string> requiredQuestFlags` field here is the natural next step.

---

## 2. Save state — ✅ Implemented (`SaveData.cs`, `SaveManager.cs`, `SaveDataWrapper.cs`)

```csharp
[Serializable]
public class SaveData
{
    public Dictionary<string, bool> questFlags = new();
    public Dictionary<string, bool> storiesUnlocked = new();
    public Dictionary<string, string> lastCheckpointPerStory = new();
    public Dictionary<string, int> inventoryCounts = new();
}
```

The `JsonUtility`-can't-serialize-`Dictionary` problem (flagged as a decision point in the original brainstorm) was correctly solved with a parallel key/value list wrapper:

```csharp
[System.Serializable]
public class SaveDataWrapper
{
    public List<string> flagKeys = new();
    public List<bool> flagValues = new();
    public List<string> storyKeys = new();
    public List<bool> storyValues = new();
    public List<string> checkpointStoryKeys = new();
    public List<string> checkpointChapterValues = new();
}
```

**Gap:** `inventoryCounts` has no corresponding fields in `SaveDataWrapper`, so it's silently dropped on save/load. Not urgent while nothing writes to it, but should either be wired in (two more parallel lists) or removed from `SaveData` until inventory tracking is actually needed — leaving it as-is risks a confusing bug later when something starts writing to it and the data quietly doesn't persist.

`SaveManager` writes to disk immediately inside `QuestManager.SetFlag` (see §5) rather than only on an explicit save call — this matches the "save on checkpoint-relevant moments, not just on quit" guidance from the original plan, arguably even more eagerly than originally scoped (every flag set triggers a disk write). Fine at current scale; worth debouncing later if flag-setting becomes frequent enough to matter for performance.

---

## 3. Checkpoint flow — ⚠️ Partially implemented (`CheckpointManager.cs`)

```csharp
public class CheckpointManager : MonoBehaviour
{
    [SerializeField] Transform xrOriginRoot;

    public void SetCheckpoint(ChapterDefinition chapter)
    {
        SaveManager.Current.lastCheckpointPerStory[chapter.storyId] = chapter.chapterId;
        SaveManager.SaveToDisk();
    }

    public void RespawnAtCheckpoint(ChapterDefinition chapter)
    {
        xrOriginRoot.SetPositionAndRotation(chapter.checkpointSpawnPos, chapter.checkpointSpawnRot);
    }
}
```

**Good:** correctly moves `xrOriginRoot` (the rig root) rather than the camera directly — this was called out explicitly in the original plan as a VR-specific requirement (camera has a real-head-height local offset; moving it directly desyncs the play space), and it's implemented correctly here.

**Gap (the most important one in the whole project right now):** `RespawnAtCheckpoint` only repositions the player — it never re-applies quest-flag state to the scene. The original plan called this out by name as "commonly forgotten": *"checkpoints that remember progress numerically but not visually."* Concretely, right now: if a player solves the ward puzzle, dies in the combat encounter after it, and respawns at the checkpoint, `QuestManager.IsFlagSet("puzzle1_solved")` correctly still returns `true`, but the ward dolls will be back in their un-placed starting positions in the scene, and any already-cleared `EncounterManager` will replay its enemies too.

**Fix shape:** add something like `ApplyQuestFlagsToScene(chapterId)` called from `GameManager.LoadChapter`'s completion callback (see §7), which:
- Iterates puzzles/encounters relevant to the chapter, checks their `_solved`/`_cleared` flags, and forces already-complete ones into their finished visual state (fill sockets without requiring re-placement, set `EnemyController` straight to `Dead`, etc.) — this needs a small addition to `PuzzleManager`/`EncounterManager` (a "ForceComplete" or "RestoreState" method) rather than replaying the normal completion path.

### Chapter-as-checkpoint granularity

Unchanged from the plan — chapters double as checkpoints, no fine-grained mid-chapter autosave. No sub-checkpoint system exists yet, which is fine at current content scale.

---

## 4. Level unlock logic — ⚠️ Half-implemented (`StoryUnlockUtility.cs`)

```csharp
public static class StoryUnlockUtility
{
    public static bool IsUnlocked(StoryDefinition story)
    {
        if (story.unlockRequirement == null) return true;
        return SaveManager.Current.storiesUnlocked.TryGetValue(story.unlockRequirement.storyId, out var done) && done;
    }
}
```

This correctly implements the **read** side exactly as planned. The **write** side doesn't exist yet — nothing in the repo currently sets `storiesUnlocked[storyId] = true`. This is the direct consequence of `ChapterDefinition` having no `requiredQuests` list (§1): there's no way yet to determine "this chapter is done," so there's nothing to aggregate into "this story is done."

**Fix shape (two small additions get this working end-to-end):**
1. Add `List<string> requiredQuestFlags` to `ChapterDefinition`.
2. Add a small check — either polled on `QuestManager.OnFlagSet`, or checked explicitly when the player reaches a "chapter end" trigger — that does: *are all of this chapter's `requiredQuestFlags` set? → if this was the story's last chapter, `SaveManager.Current.storiesUnlocked[storyId] = true` and save.*

This is genuinely the single most valuable next piece to build — it's the missing link between "individual systems correctly report completion via quest flags" (which all works) and "the level-select screen actually reflects story progress" (which nothing currently drives).

---

## 5. Quest tracking — ✅ Implemented, slightly improved over plan (`QuestManager.cs`)

```csharp
public static class QuestManager
{
    public static event Action<string> OnFlagSet;

    public static bool IsFlagSet(string flagId) =>
        SaveManager.Current.questFlags.TryGetValue(flagId, out var val) && val;

    public static void SetFlag(string flagId)
    {
        if (IsFlagSet(flagId)) return; // not in original plan — good addition
        SaveManager.Current.questFlags[flagId] = true;
        OnFlagSet?.Invoke(flagId);
        SaveManager.SaveToDisk();
    }
}
```

The `if (IsFlagSet(flagId)) return;` early-out wasn't explicitly in the original design but is a solid addition — prevents redundant saves and redundant `OnFlagSet` event firing if something calls `SetFlag` on an already-completed objective (e.g., a player re-entering a cleared encounter's trigger volume). `FlagQuestTest.cs` exists as a minimal smoke test for this system and confirms it works as expected.

This remains the correct shared completion currency across combat, puzzles, and dialogue — validated in practice, not just in design.

---

## 6. Addressables — ✅ Implemented correctly (`GameManager.cs`)

The async scene-loading is implemented properly — the previous Addressable scene is unloaded before the next is loaded, avoiding the classic unload/load race condition:

```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    AsyncOperationHandle<SceneInstance> currentSceneHandle;

    public void LoadChapter(ChapterDefinition chapter)
    {
        StartCoroutine(LoadChapterRoutine(chapter));
    }

    IEnumerator LoadChapterRoutine(ChapterDefinition chapter)
    {
        if (currentSceneHandle.IsValid())
            yield return Addressables.UnloadSceneAsync(currentSceneHandle);

        currentSceneHandle = Addressables.LoadSceneAsync(chapter.sceneRef, LoadSceneMode.Single);
        yield return currentSceneHandle;

        checkpointManager.RespawnAtCheckpoint(chapter);
        // gap: no ApplyQuestFlagsToScene call here yet — see §3
    }
}
```

(Reconstructed/paraphrased from the actual implementation — the sequencing and correctness are accurate to the repo.) This is exactly the "loading is async, avoid a hard instant scene-swap" guidance from the original plan, correctly followed.

**Addressables setup recap (unchanged, for reference):** package installed via Package Manager → `Window > Asset Management > Addressables > Groups` → mark scenes Addressable via the Inspector checkbox → reference via `AssetReference` on `ChapterDefinition`. No remote-content/DLC features are in use, correctly — not needed at this scope.

**Not yet relevant:** FMOD bank loading alongside Addressable scene loading — moot until FMOD integration begins (see `04-dialogue-fmod.md`).

---

## 7. Game flow: main menu → level select → play → return — ✅ Implemented

```
[Bootstrap scene] → GameManager, SaveManager, QuestManager, PuzzleManager, CheckpointManager (persistent)
  → loads MainMenu

[MainMenu scene] → MenuButtonActions.PlayButton() → GameManager.Instance.LoadLevelSelect()

[LevelSelect scene] → MenuButtonActions.SelectChapter(chapter) → GameManager.Instance.LoadChapter(chapter)

[Chapter scene, Addressables-loaded] → gameplay
```

`MenuButtonActions.cs` is a minimal, correct bridge between world-space UI buttons and `GameManager` calls:

```csharp
public class MenuButtonActions : MonoBehaviour
{
    public void PlayButton() => GameManager.Instance.LoadLevelSelect();
    public void SelectChapter(ChapterDefinition chapter) => GameManager.Instance.LoadChapter(chapter);
}
```

Bootstrap/MainMenu/LevelSelect/TestLevel/CombatTest scenes all exist in the repo, matching this flow. Level-select currently doesn't yet reflect lock/unlock visually (blocked on §4's gap) — worth revisiting once story-unlock writing exists.

---

## 8. Multi-area test level (3 dialogue + 2 combat + 2 puzzle areas) — ✅ Trigger system implemented, content partially built

`AreaTrigger.cs` is implemented exactly as planned:

```csharp
[RequireComponent(typeof(Collider))]
public class AreaTrigger : MonoBehaviour
{
    [SerializeField] string areaId;
    [SerializeField] UnityEvent onPlayerEnter;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        onPlayerEnter?.Invoke();
    }
}
```

The `[RequireComponent(typeof(Collider))]` attribute is a small correctness addition beyond the original sketch — prevents a common setup mistake (forgetting the trigger collider).

**Current content state (from repo scene folders):** a `TestLevel` scene and a separate `CombatTest` scene exist. Based on the script inventory, the **ward-placement puzzle** (1 puzzle area) and **combat encounter via `EncounterManager`/`TestDummy`** (at least 1 combat area) are functionally built. Full coverage of all 7 originally-scoped areas (3 dialogue POIs, 2 combat, 2 puzzles) should be double-checked against the actual scene contents — this doc can't fully confirm area-by-area completeness from script inspection alone.

**Gating decision (unchanged):** open/free-roam was recommended and is consistent with `AreaTrigger`'s independent, non-gated design — no `AreaGate`/locked-until-flag component exists, which is correct for this stage.

---

## Updated build order (given current state)

1. ✅ One story, one chapter, one checkpoint working end to end (load scene, position rig, save/load JSON).
2. ⚠️ In progress: full 7-area test level content (confirm actual scene coverage against the 3+2+2 target).
3. ⬜ **Next priority:** close the two architecture gaps — checkpoint flag re-application (§3) and chapter-completion → story-unlock wiring (§4) — before adding more chapters or stories, since both gaps compound with every new chapter added.
4. ⬜ Second/third stories — hold until the above is solid; this is unchanged from the original scope guidance.