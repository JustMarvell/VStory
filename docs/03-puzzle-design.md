# Puzzle Design Workshop

> **Implementation status summary:** the ward-placement puzzle (concept 2) is **implemented** using real XRI socket-interactor code, along with a generalized `PuzzleManager` that can support future puzzle types. The other three concepts (star navigation, volcano timing, Masamper call-and-response) remain **design-only**, unchanged from the original brainstorm.

## VR puzzle design principle

VR puzzles play differently than flatscreen ones. Real hands + real depth perception make physical manipulation puzzles (grabbing, aligning, placing, combining) both easier (spatial intuition does the work) and more satisfying than the flatscreen equivalent. **Don't port point-and-click puzzle logic in** — design around grab/align/place/combine. This principle is validated by how naturally the ward puzzle came together using XRI's built-in socket snapping.

---

## Sangihe folklore research (surface-level web search — not exhaustive)

Unchanged from original research. Real cultural threads used instead of generic fantasy tropes:

- **Origin of the name "Sangihe"** — traces to a story of King Gumansalangi sailing from Manado and becoming stranded on the island.
- **Guardian dolls ("urǒ")** — Sangir residents traditionally hang small dolls to protect crops from thieves; per legend, the dolls "pursue" the thief. *(This is the one already built — see below.)*
- **Mount Awu** — an active volcano on Sangir Island; a violent 1856 eruption had a death toll possibly as high as 6,000, with further major eruptions in 1966 and 2004.
- **Masamper** — a local choral call-and-response singing tradition.

*(Note: general web search, not deep ethnographic research. If more specific/authentic legends are known from local sources, those should take priority — happy to search more precisely for named legends if provided.)*

---

## Puzzle concept 2 — Guardian doll (urǒ) ward placement — ✅ IMPLEMENTED

**Concept:** place ward dolls around a village/grove in correct positions/orientations to seal out a thieving spirit. Clues via environmental storytelling (carvings, elder dialogue, symbols on dolls matching symbols at correct locations).

**Implemented code (`WardSocket.cs`, `WardDoll.cs`):**

```csharp
// WardDoll.cs
public class WardDoll : MonoBehaviour
{
    public string dollId;
}

// WardSocket.cs (paraphrased from real XRI-integrated implementation)
public class WardSocket : MonoBehaviour
{
    [SerializeField] string requiredDollId;
    [SerializeField] string puzzleId;
    [SerializeField] string stepId;
    XRSocketInteractor socketInteractor;

    void Awake()
    {
        socketInteractor = GetComponent<Interactors.XRSocketInteractor>();
        socketInteractor.selectEntered.AddListener(OnDollPlaced);
    }

    void OnDollPlaced(SelectEnterEventArgs args)
    {
        var doll = args.interactableObject.transform.GetComponent<WardDoll>();
        if (doll != null && doll.dollId == requiredDollId)
            PuzzleManager.Current.ReportSubStepComplete(puzzleId, stepId);
    }
}
```

This is real, working code against the actual current XRI namespace (`Interactors.XRSocketInteractor`) and event API (`selectEntered.AddListener`) — more precise than the original design sketch, which only approximated the API surface.

**Not yet implemented from the original design:**
- **Symbol-matching clue design** (fish doll → water shrine, fire doll → volcano side) — the mechanical placement/matching system works, but no actual symbol/environmental-clue content has been confirmed built yet. Worth checking whether dolls currently have any visual distinction beyond `dollId`, or whether that's still placeholder.
- **Reset affordance** — the original plan called for a physical reset totem/lever so players can recover from a fumble without a full chapter reload. No evidence of this in the current puzzle scripts; worth adding, since XRI socket interactors are usually easy enough to un-grab and re-place, but a mis-thrown doll rolling out of reach is a real risk without one.

**Confirmed working: `PuzzleManager.cs`**

```csharp
public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Current { get; private set; }

    [SerializeField] List<PuzzleDefinitionEntry> puzzleDefinitions;
    readonly Dictionary<string, HashSet<string>> requirements = new();
    readonly Dictionary<string, HashSet<string>> progress = new();

    [System.Serializable]
    public class PuzzleDefinitionEntry
    {
        public string puzzleId;
        public List<string> requiredStepIds;
    }

    void Awake()
    {
        Current = this;
        foreach (var def in puzzleDefinitions)
        {
            requirements[def.puzzleId] = new HashSet<string>(def.requiredStepIds);
            progress[def.puzzleId] = new HashSet<string>();
        }
    }

    public void ReportSubStepComplete(string puzzleId, string stepId)
    {
        if (!progress.ContainsKey(puzzleId)) return;
        progress[puzzleId].Add(stepId);
        if (progress[puzzleId].SetEquals(requirements[puzzleId]))
            QuestManager.SetFlag($"{puzzleId}_solved");
    }
}
```

This is implemented **better than the original sketch** in one respect: `puzzleDefinitions` is Inspector-configurable data (a list of `puzzleId` → `requiredStepIds`) rather than requiring code changes to add a new puzzle's requirements — a genuinely good improvement on the original design, and consistent with the project's overall "data lives in ScriptableObject/Inspector fields, not code" principle.

One thing worth double-checking: `Awake()` populates `requirements`/`progress` once from `puzzleDefinitions` — if a puzzle scene is reloaded (e.g. via checkpoint respawn) after being partially solved, this correctly starts `progress` fresh, which is actually the *desired* behavior for the "die and respawn = re-solve the puzzle unless already fully flagged" model, **but** it ties into the same checkpoint gap flagged in `02-game-architecture.md`: if the puzzle was already fully solved before death, nothing currently tells the sockets "you're already filled" on scene reload, so the player would need to re-place already-correct dolls even though `QuestManager` already has the `_solved` flag. Same root cause, same fix (an `ApplyQuestFlagsToScene`-style restoration pass).

---

## Puzzle concept 1 — Star/current navigation (Gumansalangi origin story) — ⬜ Design only, not started

Unchanged from original brainstorm:

**Concept:** player on a boat/raft sets a course using stars, wind (cloth/flag movement), or current indicators — not told the answer directly.

**VR-native mechanic:** grab a physical steering wheel/rudder; hold up a star-chart/compass object against the night sky and align a marking with the correct constellation. Solved when the boat is physically oriented so the chart lines up correctly.

**Implementation shape:** `HeadingChecker` compares current boat heading vs. target heading (with tolerance); fires quest flag when held within tolerance for N seconds — would call `PuzzleManager.ReportSubStepComplete` following the now-proven pattern from the ward puzzle.

**Comfort note:** extended upward neck-craning to view stars is a comfort issue for some players — keep it brief, or let the player bring the chart down to eye level.

---

## Puzzle concept 3 — Mount Awu timing/hazard puzzle — ⬜ Design only, not started

**Concept:** volcano activity (ash fall, tremors, lava vents) follows a readable pattern the player learns and acts within — cross a hazard zone during a safe window, or time a ritual/offering to a lull.

**VR-native mechanic:** player's own physical positioning as the input — physically duck behind cover when tremors build, or physically hold an object steady on an altar through a shake sequence without it toppling.

**Implementation shape:** `EruptionCycleController` running a timed state machine (`Calm → Rumbling → Active → Calm`); other objects subscribe to it. Worth building as a generic reusable "environmental cycle" system, same recommendation as before — no reason to revise this given nothing's built yet.

---

## Puzzle concept 4 — Masamper call-and-response — ⬜ Design only, not started

**Concept:** a sealed door/gate opens when the player "answers" a sung phrase correctly.

- **Low-risk tier (build first):** hand-held instrument (gong/chimes), rhythm-sequence matching (physical Simon-Says) — reports into `PuzzleManager` the same way the ward puzzle does.
- **Higher-risk/stretch tier:** microphone pitch/rhythm matching — still correctly gated on validating mic input for the blow dart first (see `01-combat-system.md` §4, which is also not yet started), so this remains appropriately low priority.

---

## Shared puzzle architecture — ✅ Validated in practice

The `PuzzleManager` design held up well against a real puzzle implementation, and was improved (Inspector-driven puzzle definitions) rather than needing rework. This is a good sign for reusing it directly for concepts 1, 3, and 4 without architecture changes — new puzzle types should only need a new front-end script (like `WardSocket`) calling into the existing `ReportSubStepComplete`.

---

## VR-specific fairness/comfort rules for puzzles (unchanged, still relevant to unbuilt puzzles)

- **Avoid combining time pressure with fine motor precision** — relevant to puzzle concepts 1 and 3 (both have a time/pattern component); worth deliberately picking one axis of difficulty when building them.
- **Always provide a physical reset option** — flagged above as missing even from the already-built ward puzzle; worth adding there first as a template for the rest.
- **Comfort-check any "look up" puzzle** — directly relevant to puzzle concept 1 when it's built.

---

## Updated build order

1. ✅ **Ward-placement puzzle** — done, validated `PuzzleManager` architecture.
2. ⬜ **Add the reset affordance** to the ward puzzle before considering it fully finished — small but was in the original spec and is currently missing.
3. ⬜ **Masamper rhythm-tap version** — next puzzle to build; introduces sequence-matching logic (different from ward's set-matching), reusing `PuzzleManager` as-is.
4. ⬜ **Star navigation** and **volcano timing** puzzles — second pass, need new custom systems (`HeadingChecker`, `EruptionCycleController`).
5. ⬜ **Mic-based audio puzzles** — stretch goals, blocked on mic-input validation via the blow dart first.