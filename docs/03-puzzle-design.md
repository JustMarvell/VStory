# Puzzle Design Workshop

## VR puzzle design principle

VR puzzles play differently than flatscreen ones. Real hands + real depth perception make physical manipulation puzzles (grabbing, aligning, placing, combining) both easier (spatial intuition does the work) and more satisfying than the flatscreen equivalent. **Don't port point-and-click puzzle logic in** — design around grab/align/place/combine.

---

## Sangihe folklore research (surface-level web search — not exhaustive)

Real cultural threads found, worth using instead of generic fantasy tropes:

- **Origin of the name "Sangihe"** — the archipelago's name traces to a story of King Gumansalangi sailing from Manado and becoming stranded on the island.
- **Guardian dolls ("urǒ")** — Sangir residents traditionally hang small dolls to protect crops from thieves; per legend, the dolls "pursue" the thief.
- **Mount Awu** — an active volcano on Sangir Island; a violent 1856 eruption had a death toll possibly as high as 6,000, with further major eruptions in 1966 and 2004. Volcanic activity is a real, present part of the islands' history.
- **Masamper** — a local choral call-and-response singing tradition.

*(Note: this was a general web search, not deep ethnographic research. If more specific/authentic legends are known — from local sources, family, or literature — those should take priority over these; happy to search more precisely for named legends if provided.)*

---

## Puzzle concept 1 — Star/current navigation (Gumansalangi origin story)

**Concept:** player on a boat/raft sets a course using stars, wind (cloth/flag movement), or current indicators — not told the answer directly.

**VR-native mechanic:** grab a physical steering wheel/rudder; hold up a star-chart/compass object against the night sky and align a marking with the correct constellation. Solved when the boat is physically oriented so the chart lines up correctly.

**Implementation shape:** `HeadingChecker` compares current boat heading vs. target heading (with tolerance); fires quest flag when held within tolerance for N seconds. Doesn't reuse combat systems — good "different muscle" puzzle to break up combat-heavy chapters.

**Comfort note:** extended upward neck-craning to view stars is a comfort issue for some players — keep it brief, or let the player bring the chart down to eye level.

---

## Puzzle concept 2 — Guardian doll (urǒ) ward placement — BUILD THIS FIRST

**Concept:** place ward dolls around a village/grove in correct positions/orientations to seal out a thieving spirit. Clues via environmental storytelling (carvings, elder dialogue, symbols on dolls matching symbols at correct locations).

**VR-native mechanic:** grab-and-place with orientation/identity matching — natural fit for XRI's **`XR Socket Interactor`** (snaps into place only when correct, optional rotation matching). Satisfying "click" feedback largely built-in.

**Implementation shape:**
```csharp
public class WardSocket : MonoBehaviour
{
    public string requiredDollId; // must match the doll's symbol
    public bool isFilled;

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        var doll = args.interactableObject.transform.GetComponent<WardDoll>();
        if (doll.dollId == requiredDollId)
        {
            isFilled = true;
            PuzzleManager.Current.CheckCompletion(puzzleId);
        }
    }
}
```
Central `PuzzleManager` checks "are all sockets in this group filled correctly?" and fires the quest flag — same event-driven pattern as damage pipeline/quest system.

**Difficulty lever:** don't state which doll goes where — make it symbol-matching (fish-carved doll → shrine near water; fire-carved doll → near the volcano side) so it's about reading the environment, not trial-and-error.

**Why build first:** lowest risk — pure XRI socket interactors, no custom physics or audio work. Good for validating `PuzzleManager` architecture cheaply.

---

## Puzzle concept 3 — Mount Awu timing/hazard puzzle

**Concept:** volcano activity (ash fall, tremors, lava vents) follows a readable pattern the player learns and acts within — cross a hazard zone during a safe window, or time a ritual/offering to a lull.

**VR-native mechanic:** make the player's **own physical positioning** the input rather than a flatscreen-style watch-and-button-press — e.g. physically duck behind cover when tremors build, or physically hold an object steady on an altar through a shake sequence without it toppling (reuses rigidbody/physics familiarity from throwables).

**Implementation shape:** `EruptionCycleController` runs a timed state machine (`Calm → Rumbling → Active → Calm`); other objects (hazard triggers, VFX, altar physics) subscribe to it. Worth building as a **generic reusable "environmental cycle" system** (looping timeline with named phases + events) rather than one-off code, since it could serve multiple puzzles/chapters.

---

## Puzzle concept 4 — Masamper call-and-response

**Concept:** a sealed door/gate opens when the player "answers" a sung phrase correctly, tied to the real choral tradition.

**Two implementation tiers:**
- **Low-risk (build first):** hand-held instrument-like object (small gong/chimes); player strikes in the correct **rhythm sequence** shown beforehand (physical Simon-Says). Fully buildable with XRI grab/interact, no audio-analysis risk.
- **Higher-risk/higher-payoff (stretch goal):** actual microphone pitch/rhythm matching — player sings/hums the pattern back. Real audio-DSP work (`Microphone.Start`, amplitude/pitch detection, tolerance matching). Only attempt after mic input is already validated for the blow dart (see `01-combat-system.md`), so the underlying tech problem is solved once, not twice.

---

## Shared puzzle architecture

```csharp
public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Current;
    private Dictionary<string, HashSet<string>> puzzleRequirements = new();
    private Dictionary<string, HashSet<string>> puzzleProgress = new();

    public void ReportSubStepComplete(string puzzleId, string stepId)
    {
        puzzleProgress[puzzleId].Add(stepId);
        if (puzzleProgress[puzzleId].SetEquals(puzzleRequirements[puzzleId]))
            QuestManager.SetFlag($"{puzzleId}_solved");
    }
}
```
Every puzzle type (socket-based, timing-based, rhythm-based) calls `ReportSubStepComplete` with its own step IDs — completion logic stays centralized rather than reimplemented per puzzle.

---

## VR-specific fairness/comfort rules for puzzles

- **Avoid combining time pressure with fine motor precision** — VR controller precision is inherently less exact than a mouse; pick one axis of difficulty per puzzle, not both.
- **Always provide a physical reset option** (a reset totem/lever) — VR players drop/fumble objects far more than they'd misclick on a screen; avoid forcing a full chapter reload to recover from a fumble.
- **Comfort-check any "look up" puzzle** — keep it brief, or offer an alternative (bring the object to eye level).

---

## Suggested build order

1. **Ward-placement puzzle** — lowest risk, validates `PuzzleManager` architecture.
2. **Masamper rhythm-tap version** — introduces sequence-matching logic, reusable elsewhere.
3. **Star navigation** and **volcano timing** puzzles — second pass, once architecture is proven (need more custom systems: heading-checking, cycle-based hazards).
4. **Mic-based audio puzzles** (singing, breath) — stretch goals layered on top of working button/physical versions, not initial implementations.