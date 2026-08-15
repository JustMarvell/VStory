# Dialogue System + FMOD

> **Note:** this topic was discussed in chat but wasn't previously written to a doc file — this is the first version of this file. **Implementation status summary:** the core data-driven dialogue structure (`DialogueLine`, `DialogueSequence`, `DialogueRunner`) is **implemented** as basic text-only lines with no FMOD wiring yet, no world-space follow/placement logic yet, and no continue/skip-via-ray-interactor confirmed yet. FMOD itself is **not yet integrated** into the project.

## World-space dialogue UI: placement and comfort first

Since the dialogue UI floats in front of the player, the biggest early decision isn't the text system — it's **how it follows the player without causing discomfort**. Three approaches, in order of recommendation:

1. **Static-in-world placement (best when possible):** if a dialogue is triggered by an NPC or object at a fixed location, anchor the panel near that NPC/object rather than the player's head. No follow logic needed, no comfort risk, reads as more diegetic.
2. **Damped follow (for player-triggered/ambient dialogue):** canvas smoothly interpolates toward a target position relative to the player's head, but with lag — never rigidly parented to the camera (rigid head-lock is a known nausea trigger for some players).
3. **Fixed-distance, player-facing billboard:** panel stays at a constant distance, rotates to face the player, but only re-centers position once the player has turned past an angle threshold (e.g. 30–40°) rather than continuously tracking.

```csharp
public class DialoguePanelFollow : MonoBehaviour
{
    public Transform headTransform;
    public float followDistance = 1.2f;
    public float angleThreshold = 35f;
    public float followSpeed = 3f;

    void LateUpdate()
    {
        Vector3 targetPos = headTransform.position + headTransform.forward * followDistance;
        float angle = Vector3.Angle(transform.forward, headTransform.forward);
        if (angle > angleThreshold || Vector3.Distance(transform.position, targetPos) > 0.1f)
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        transform.rotation = Quaternion.LookRotation(transform.position - headTransform.position);
    }
}
```

**Implementation status:** ⬜ Not yet built. `DialogueRunner.cs` exists but no panel-follow behavior was found in the repo — worth checking whether the current test dialogue panel is manually placed in-scene (acceptable for static/anchored dialogue, per option 1 above) or not yet positioned at all.

Prefer option 1 wherever dialogue is tied to a specific speaker (most of the game, given a story-driven design) and reserve options 2/3 for narration or system messages with no clear physical source.

## Text panel setup

- World-space `Canvas` (Render Mode: World Space) with TextMeshPro for text rendering — TMP's SDF rendering stays crisp at VR resolutions in a way legacy UI Text doesn't.
- Continue/skip interaction: XRI's `XR Ray Interactor` pointed at world-space UI works once a `TrackedDeviceGraphicRaycaster` is added to the canvas (replaces the standard `GraphicRaycaster` for XR input) — buttons just need standard `Button` components.
- Keep panel size/text scale generous — VR headsets have lower effective text resolution than a monitor at reading distance; test actual readability on hardware once available, not just editor preview.

**Implementation status:** ⬜ Not confirmed. Worth checking whether the existing dialogue panel prefab uses TMP + `TrackedDeviceGraphicRaycaster` + ray-interactable buttons, since none of that was visible from the script folder alone (it would live in a scene/prefab, not a script).

## Data-driven dialogue structure — ✅ Implemented (simplified)

```csharp
// DialogueLine.cs — implemented
[Serializable]
public struct DialogueLine
{
    public string speakerName;
    [TextArea] public string text;
}

// DialogueSequence.cs — implemented
[CreateAssetMenu(menuName = "Dialogue/DialogueSequence")]
public class DialogueSequence : ScriptableObject
{
    public string sequenceId;
    public List<DialogueLine> lines;
    public string setFlagOnComplete;
}
```

This matches the original plan's shape closely, with one deliberate simplification: the originally-planned `fmodEventPath` and `anchorOverride` fields on `DialogueLine` are **not present yet** — the struct is currently text-only (`speakerName` + `text`). This is a sensible reduction for validating the text/flow first, per the build order below, but means FMOD hookup and per-line anchor overrides are both still greenfield work, not just "wire up an existing field."

`setFlagOnComplete` on the sequence (rather than per-line) matches the "a whole conversation completing is one flag" framing from the original design — good, this was the recommended granularity.

A `TestDialogue` ScriptableObject asset already exists in `ScriptableObjects/Dialogue/`, suggesting `DialogueRunner` has been smoke-tested with real data.

**`DialogueRunner.cs`** — implemented, handles stepping through `lines`, updating displayed text. Auto-advance-vs-wait-for-input behavior, and whether skip is wired to ray-interactor input yet, should be confirmed directly against the script/prefab rather than assumed here.

## FMOD integration — ⬜ Not yet started

No FMOD packages, event references, or `RuntimeManager` calls exist anywhere in the repo yet. The plan below is unchanged from the original brainstorm and is the next real milestone for this system:

```csharp
using FMODUnity;

public class DialogueRunner : MonoBehaviour
{
    private FMOD.Studio.EventInstance currentVO;

    public void PlayLine(DialogueLine line)
    {
        StopCurrentVO();
        dialogueText.text = line.text;
        if (!string.IsNullOrEmpty(line.fmodEventPath)) // field doesn't exist yet — needs adding
        {
            currentVO = RuntimeManager.CreateInstance(line.fmodEventPath);
            currentVO.start();
            currentVO.release();
        }
    }

    public void StopCurrentVO()
    {
        if (currentVO.isValid())
            currentVO.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
    }
}
```

**First steps when starting this work:**
1. Add `fmodEventPath` (string) back onto `DialogueLine` — the field was intentionally dropped for the text-only first pass, this is a one-line addition, not a redesign.
2. Verify FMOD for Unity's currently-documented supported version against Unity 6000.3 directly on FMOD's site before installing — this was flagged as unverified in the original brainstorm and still hasn't been checked.
3. Decide **auto-advance vs. wait-for-input** behavior now that real VO timing will exist — hook `EventInstance.getPlaybackState()` (or FMOD's callback system) so text auto-advances in sync with audio once VO exists, while skip still works immediately regardless of playback state (skip should stop VO immediately, not wait for it).
4. For lines with a physical speaker (an NPC), use `RuntimeManager.AttachInstanceToGameObject(currentVO, speakerTransform)` for 3D-positioned VO rather than a flat 2D/UI-attached sound — matters more in VR than flatscreen since players can localize sound direction, and a nearby NPC's voice sounding like it's coming from the player's own face breaks immersion.

## Placeholder VO strategy

Until real FMOD VO exists, keep testing with silent text-only lines (the current implementation) or scratch/placeholder audio once the `fmodEventPath` field exists — the goal is that adding real VO later is a **content swap** (new FMOD banks), not a code change. The current text-only implementation is consistent with this strategy, not a deviation from it.

## Subtitle/accessibility consideration

Since the text display is already the primary implementation, keep subtitles as the default-on baseline even once VO exists — cheaper to keep the always-active text path than build a separate subtitle toggle later. Text and VO should stay driven from the same `DialogueLine` data so they can't drift out of sync. No action needed here yet — this falls out naturally from the current implementation as long as the FMOD addition doesn't remove the text display.

## Integration with quest/checkpoint systems

`setFlagOnComplete` → `QuestManager.SetFlag()` — **implementable now**, using the same pattern validated by the puzzle and combat systems (see `01-combat-system.md`, `03-puzzle-design.md`). Worth checking whether `DialogueRunner` currently calls this on sequence completion; if not, it's a small, low-risk addition consistent with everything else already working.

This also ties into the same checkpoint gap noted elsewhere: once dialogue sequences set flags, "already seen this dialogue" state on checkpoint re-entry needs the same `ApplyQuestFlagsToScene`-style restoration pass flagged in `02-game-architecture.md` — worth handling all three systems (puzzles, encounters, dialogue) in that one fix rather than three separate ones.

## Updated build order

1. ✅ Single `DialogueSequence` asset (`TestDialogue`) displaying via `DialogueRunner` — done, text-only.
2. ⬜ Confirm/build world-space panel placement (static-anchored per option 1) and continue/skip via ray interactor — status unconfirmed, worth verifying against the actual scene/prefab next.
3. ⬜ Wire `setFlagOnComplete` → `QuestManager.SetFlag` if not already connected.
4. ⬜ Add `fmodEventPath` field back, integrate FMOD, get auto-advance-on-VO-complete working.
5. ⬜ Speaker-attached 3D audio via `AttachInstanceToGameObject`.
6. ⬜ Checkpoint-aware "already seen" suppression — blocked on the general checkpoint-flag-reapplication fix shared with puzzles/encounters.