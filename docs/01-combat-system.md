# Combat System

Covers: melee hit detection & weapon tracking → parry/block → enemy AI state machine & hit reactions → throwables/blow dart → shared damage pipeline → enemy spawn/encounter activation.

---

## 1. Melee hit detection

Naive trigger-collider-on-blade detection fails at VR swing speeds:
- **Tunneling** — fast swings can pass fully through a thin hitbox between physics steps without ever overlapping.
- **False/spam hits** — the blade resting near an enemy can register as a hit even when not actively swinging.

### Recommended approach: velocity gating + sweep testing

1. **Track blade tip velocity every `FixedUpdate`** (tip velocity, not hilt/grip velocity — the tip moves much faster on a wrist-driven swing).
   ```csharp
   Vector3 currentTipPos = bladeTip.position;
   Vector3 tipVelocity = (currentTipPos - lastTipPos) / Time.fixedDeltaTime;
   lastTipPos = currentTipPos;
   ```
2. **Sweep-test** between last frame's and this frame's tip position instead of relying on overlap:
   ```csharp
   if (Physics.Linecast(lastTipPos, currentTipPos, out RaycastHit hit, enemyLayerMask))
   {
       if (tipVelocity.magnitude > minHitVelocity)
           RegisterHit(hit, tipVelocity);
   }
   ```
   For a wide blade, sweep multiple points (tip/mid/base) or use `Physics.SphereCast` along the blade length.
3. **Velocity threshold gate** (`minHitVelocity`) so resting the blade against an enemy doesn't count as a hit. This also scales damage naturally by swing speed.
4. **Debounce per swing** — once a swing registers a hit against a given enemy, ignore further hits from that swing until velocity drops below threshold and rises again (prevents multi-hit from one motion).

### Weapon tracking: kinematic vs. physics-driven

- **Kinematic/tracked (chosen starting approach):** weapon transform directly follows the controller (XRI default `XR Grab Interactable`). Predictable, 1:1 responsive, easy to reason about. Can visually clip through geometry.
- **Physics-driven (rigidbody chasing target via `MovePosition`/PD controller):** feels weightier, can't clip through walls, but harder to tune (floaty/laggy if spring/damping is off) and costs more dev time. Revisit later only if needed.
- **XRI gotcha:** default `XR Grab Interactable` has attach-point smoothing/interpolation for comfort — this lags the weapon slightly behind the controller. **Disable/minimize this for melee weapons** (laggy sword tracking feels bad); fine to leave on for slower two-handed props.

### Haptics

Fire a short (~20-40ms) controller haptic pulse on confirmed hit, amplitude scaled by hit velocity (`SendHapticImpulse`). Disproportionately important for perceived "weight" since there's no real inertia felt through the controller otherwise.

### Build order

1. Build swing + hit detection against a static test dummy first — tune hitstop timing, haptic strength, responsiveness in isolation.
2. Add a simple enemy with hit reactions once swing feel is solid.
3. Add blocking/parry only after base combat feels good.

---

## 2. Parry / Block

VR allows **real physical blocking** — the player's weapon/shield physically intercepts the incoming attack. Build this as the base layer, with timed **parry** as a bonus layer on top.

### Physical block detection

Give enemy weapons the same tip-tracking as the player's sword. Check weapon-vs-weapon collision **before** weapon-vs-player-body:

```csharp
if (Physics.Linecast(lastTipPos, currentTipPos, out RaycastHit blockHit, playerWeaponLayerMask))
{
    ResolveBlock(blockHit, tipVelocity);
    return; // block wins over a same-moment body hit
}
if (Physics.Linecast(lastTipPos, currentTipPos, out RaycastHit playerHit, playerBodyLayerMask))
{
    RegisterHitOnPlayer(playerHit, tipVelocity);
}
```

### Block → Parry

Parry = a block landed within a tight timing window relative to the enemy's **attack-active** phase (not windup):

```csharp
float timeSinceAttackActive = Time.time - enemyAttackActiveTimestamp;
bool isParry = timeSinceAttackActive <= parryWindowSeconds; // e.g. 0.25s

if (isParry)
    TriggerParry(enemy);   // hard stagger, counter window, strong haptic + hitstop
else
    TriggerBlock(enemy);   // chip damage/stamina cost, softer stagger
```

Same physical action (weapon in the way) from the player's perspective — the game decides block vs. parry from timing. Important for VR fairness: no separate input to learn.

### Telegraphing (non-negotiable for VR fairness)

Real human reaction time is required for physical blocking, so enemy attacks need clearly readable tells:
- Windup phase noticeably longer than a flatscreen game (~0.4–0.6s minimum before the strike lands, tunable per enemy).
- Pair visual windup with an audio cue (whoosh building, grunt) — VR players often react to audio faster than peripheral vision.
- Telegraph **direction** too (wide horizontal pullback vs. overhead raise) so blocking is real spatial reasoning, not "hold shield up constantly."

---

## 3. Enemy AI state machine

Simple explicit FSM is sufficient for this scope — no behavior tree needed.

```
Dormant → Idle → Approach → Telegraph(Windup) → AttackActive → Recover → (back to Approach)
                                    ↓ (interrupted by player block/parry)
                                Blocked / Staggered → Recover
                                    ↓ (player successfully hits)
                                HitReact(tier) → Recover or Dead
```

- **`Dormant`**: added state before `Idle`. Enemy exists but AI is fully disabled — no `Update()` cost, not combat-reactive. Default state until an encounter activates it (see §6).
- **Telegraph → AttackActive** is the only window where a player block/parry counts as parry-timed.
- **Blocked/Staggered** briefly disables the enemy's own attack input — makes parry feel rewarding (enemy can't immediately retaliate).
- **Recover** prevents chained attacks with zero downtime; gives the player breathing room and keeps telegraphs readable.

### Hit reaction tiers

Reuse the velocity value already computed for damage — no extra data needed:

| Tier | Trigger | Reaction |
|---|---|---|
| Light | low-velocity hit | small flinch, doesn't interrupt current action |
| Heavy | high-velocity hit | full stagger, interrupts windup/attack |
| Parried | parry-timed block | hard stagger + brief vulnerable window, strongest hitstop |
| Killing blow | HP ≤ 0 | ragdoll/death animation, overrides other tiers |

Hit-location-based reactions (via head/torso/limb colliders) are a good stretch goal once velocity-tiered reactions work.

### Tuning

Expose `minHitVelocity`, `parryWindowSeconds`, windup duration, hitstop duration as **ScriptableObject fields per enemy type** — these get retuned constantly by feel, don't hardcode as constants.

**Playtest block/parry timing on real hardware early** — reaction-time-dependent mechanics are exactly what the XR Device Simulator cannot validate.

---

## 4. Throwables & blow dart — shared damage pipeline

**Core idea:** melee (sweep-test) and projectiles (collision) detect hits differently, but should feed the **same** downstream damage/reaction system.

```csharp
public struct DamageInfo
{
    public float amount;
    public Vector3 hitPoint;
    public Vector3 hitDirection;
    public float impactVelocity;
    public DamageSourceType sourceType; // Melee, Thrown, Dart
    public HitZone hitZone;             // Head, Torso, Limb
    public GameObject instigator;
    public StatusEffectData appliedEffect; // null for sword/rock, set for status darts
}

public interface IDamageable
{
    void ApplyDamage(DamageInfo info);
}
```

Every weapon type just builds a `DamageInfo` and calls `ApplyDamage` — hit reaction tiers, hitstop, haptics, VFX all read from this struct regardless of source.

### Rocks (throwable)

- Standard `Rigidbody`, grabbed via XRI, released with velocity from recent hand-motion frames (XRI default throw behavior — tune the velocity smoothing window).
- On `OnCollisionEnter`, build `DamageInfo` from impact velocity + contact point, call `ApplyDamage`.
- Reuse a shared `VelocityToDamage(float velocity, DamageCurve curve)` helper — per-weapon `AnimationCurve` (ScriptableObject) so sword/rock/dart can each have distinct velocity→damage tuning without duplicating logic.

### Blow dart

- Structurally a projectile like the rock — only the **launch method** differs (not player-arm velocity).
- Launch input decoupled from detection/damage: start with simple button-triggered launch (grip + trigger = blow, preset force + slight spread). Mic-amplitude-based "real blow" input is a viable **later swap** since it doesn't touch the damage pipeline at all.
- Add `StatusEffectData appliedEffect` (nullable) to `DamageInfo` if darts apply poison/sleep/etc., rather than branching dart logic out of the shared struct.

### Enemy-side (unified)

```csharp
public void ApplyDamage(DamageInfo info)
{
    currentHP -= info.amount;
    var tier = ResolveHitTier(info.impactVelocity, info.hitZone); // same tiering as melee
    TriggerHitReaction(tier, info.hitDirection);
    SendHapticFeedback(info.sourceType); // e.g. darts get a subtler haptic than sword hits

    if (currentHP <= 0 && currentState != State.Dead)
    {
        currentState = State.Dead;
        aiBehaviour.enabled = false;
        OnDeath?.Invoke(this);
    }
}
```
Note the `currentState != State.Dead` guard — prevents a corpse hit again (e.g. thrown rock) from double-firing `OnDeath` and breaking encounter enemy-count tracking (see §6).

### Where source type SHOULD still branch

- **Hitstop duration/feel** — sword vs. rock vs. dart can want different freeze lengths; keep per-source-type tunable.
- **VFX/audio** — sparks vs. dust puff vs. sting, triggered from the same `ApplyDamage` call site.
- **AI aggro** — a ranged hit might alert enemies from range in a way a melee hit (already close) doesn't need to.

### Build order

1. Define `DamageInfo`/`IDamageable` first; retrofit the already-tuned sword damage call through it.
2. Add the rock — best test of whether the abstraction holds (different detection method, same pipeline).
3. Add the dart last with placeholder button-launch; treat "blow" input as a swappable front-end for later.

Signal to watch for: if adding the 2nd/3rd weapon type still requires touching enemy/hit-reaction code, the abstraction needs adjustment before adding more content.

---

## 5. Enemy spawn & encounter activation system

### Data model

```csharp
[CreateAssetMenu(menuName = "Combat/EncounterDefinition")]
public class EncounterDefinition : ScriptableObject
{
    public string encounterId;     // "story_a_ch1_combat1"
    public List<EnemySpawnData> spawns;
    public bool lockAreaUntilCleared;
}

[Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    public Vector3 localPosition;
    public Quaternion localRotation;
}
```

### Encounter manager (bridges area trigger → enemy activation → quest flag)

```csharp
public class EncounterManager : MonoBehaviour
{
    public EncounterDefinition definition;
    private List<EnemyController> spawnedEnemies = new();
    private int aliveCount;
    private bool encounterStarted;

    // Called by AreaTrigger.onPlayerEnter
    public void BeginEncounter()
    {
        if (encounterStarted) return;
        encounterStarted = true;

        foreach (var enemy in spawnedEnemies)
        {
            enemy.OnDeath += HandleEnemyDeath;
            enemy.Activate();
        }
        aliveCount = spawnedEnemies.Count;

        if (definition.lockAreaUntilCleared)
            areaBoundary.SetLocked(true);
    }

    private void HandleEnemyDeath(EnemyController enemy)
    {
        enemy.OnDeath -= HandleEnemyDeath;
        aliveCount--;
        if (aliveCount <= 0) CompleteEncounter();
    }

    private void CompleteEncounter()
    {
        if (definition.lockAreaUntilCleared)
            areaBoundary.SetLocked(false);
        QuestManager.SetFlag($"{definition.encounterId}_cleared");
    }
}
```

Wired directly into the level's `AreaTrigger.onPlayerEnter` (see `02-game-architecture.md`).

### Pre-placed-dormant vs. runtime-instantiated

- **Pre-placed in scene, `Dormant`, renderers optionally disabled (chosen default):** cheaper at runtime, no instantiation hitch, precise hand-placement in editor.
- **Runtime `Instantiate()` from spawn data:** more flexible/reusable/randomizable, but risks a frame hitch right as combat starts — bad timing for VR (reads as a technical glitch, not tension). Only use if reuse/randomization is actually needed later.

### The "reveal" moment matters in VR

Don't `SetActive(true)` an enemy from nothing right in front of the player — reads as glitchy. Prefer enemies already visually present but idle (sleeping/back turned) transitioning to alert, or a diegetic cue (growl, step out from cover already in the scene) before appearing.

### Locking the arena

Opt-in per encounter (`lockAreaUntilCleared`), not a blanket rule — not every encounter needs to trap the player. When locked, prefer a **diegetic barrier** (gate closing, roots growing across the path, a ward flickering in) over a plain invisible wall — also a good hook for the folklore/spirit theming.

### Don't `Destroy()` dead enemies immediately

- Keeps checkpoint re-entry simple: re-apply `Dead` state to existing objects instead of re-instantiating and re-killing.
- Lets ragdoll/death animation settle naturally; fade/despawn after a delay only if needed for object-count/performance reasons.

### Checkpoint integration

On chapter load, `EncounterManager` checks `QuestManager` flags first — if `{encounterId}_cleared` is already true, **skip activation entirely**, set all enemies straight to `Dead`/disabled/hidden rather than replaying the encounter.

### Decided: death/respawn behavior

**If the player dies mid-encounter, the encounter fully resets** (all enemies back to full HP) on respawn at the checkpoint — chosen over partial persistence for simplicity and because it matches convention in most combat-focused games.

### Build order

1. One enemy through `Dormant → Activate() → Idle → ... → Dead`, triggered by a basic `AreaTrigger`, no `EncounterManager` yet.
2. Add `EncounterManager` with a single-enemy encounter; confirm `OnDeath` → quest flag firing.
3. Scale to multiple enemies; confirm alive-count tracking waits for all of them.
4. Add checkpoint-aware re-entry (skip already-cleared encounters) last.