# Combat System

Covers: melee hit detection → parry/block → enemy AI state machine & hit reactions → throwables/blow dart → shared damage pipeline → enemy spawn/encounter activation.

> **Implementation status summary:** melee hit detection, the shared damage pipeline, the enemy state machine (partial), and a simplified `EncounterManager` are **implemented** in `Scripts/Combat`. Parry/block, ranged weapons (rock/dart), and enemy-attacks-player are **not yet implemented**. Details inline below.

---

## 1. Melee hit detection — ✅ Implemented (`SwordWeapon.cs`)

Naive trigger-collider-on-blade detection fails at VR swing speeds:
- **Tunneling** — fast swings can pass fully through a thin hitbox between physics steps without ever overlapping.
- **False/spam hits** — the blade resting near an enemy can register as a hit even when not actively swinging.

### Approach used: velocity gating + sweep testing

1. **Blade tip velocity tracked every `FixedUpdate`** (tip transform, not hilt/grip).
2. **`Physics.Linecast`** between last and current tip position, filtered by `enemyLayerMask`.
3. **Velocity threshold gate** (`minHitVelocity`, default 1.5) — hit only registers if tip speed exceeds this.
4. **Debounce via `lastHitCollider`** — once a swing hits a given collider, that same collider is ignored until tip velocity drops back below threshold (swing ends) and rises again. This is a clean, minimal implementation of the "don't multi-hit in one swing" requirement.

```csharp
void FixedUpdate()
{
    var currentTipPos = bladeTip.position;
    var tipVelocity = (currentTipPos - lastTipPos) / Time.fixedDeltaTime;

    if (tipVelocity.magnitude > minHitVelocity &&
        Physics.Linecast(lastTipPos, currentTipPos, out var hit, enemyLayerMask))
    {
        if (hit.collider != lastHitCollider)
        {
            RegisterHit(hit, tipVelocity);
            lastHitCollider = hit.collider;
        }
    }
    else if (tipVelocity.magnitude <= minHitVelocity)
    {
        lastHitCollider = null;
    }

    lastTipPos = currentTipPos;
}
```

**Not yet implemented / open items:**
- Damage amount is a flat `baseDamage` (10) rather than velocity-scaled — original plan was to scale damage with swing speed via a `VelocityToDamage` curve. Currently only the *hit registration* is velocity-gated, not the damage amount.
- `hitZone` is hardcoded to `HitZone.Torso` — head/limb colliders aren't distinguished yet.
- Multi-point sweep (tip/mid/base) for wide blades not implemented — single point (`bladeTip`) only. Fine for a thin sword, worth revisiting if the weapon shape changes.
- Weapon tracking: uses **kinematic/tracked** movement as planned (no physics-driven rigidbody chase). XRI attach-point smoothing hasn't been explicitly addressed yet — worth checking whether it's introducing lag on the real grabbed sword object.
- **Haptics** on hit — not yet wired in (`SendHapticImpulse` call is still TODO).

### Build order (as planned, followed correctly)

1. ✅ Swing + hit detection against a static test dummy (`TestDummy.cs`) — done.
2. ✅ Basic enemy with hit reactions — done (`EnemyController`, Light/Heavy tiers only).
3. ⬜ Blocking/parry — not started.

---

## 2. Parry / Block — ⬜ Not yet implemented

Planned design (unchanged from original brainstorm, nothing built yet):

VR allows **real physical blocking** — the player's weapon/shield physically intercepts the incoming attack. Build this as the base layer, with timed **parry** as a bonus layer.

### Physical block detection (planned)

Enemy weapons would need the same tip-tracking as the player's sword. Check weapon-vs-weapon collision **before** weapon-vs-player-body:

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

### Block → Parry (planned)

```csharp
float timeSinceAttackActive = Time.time - enemyAttackActiveTimestamp;
bool isParry = timeSinceAttackActive <= parryWindowSeconds; // e.g. 0.25s

if (isParry) TriggerParry(enemy);   // hard stagger, counter window, strong haptic + hitstop
else TriggerBlock(enemy);           // chip damage/stamina cost, softer stagger
```

**Prerequisite not yet in place:** enemies don't currently deal damage to the player at all (see §3), so there's nothing to block/parry yet — building enemy-attacks-player is a prerequisite for this system, not parallel work.

### Telegraphing (non-negotiable for VR fairness — design note, unaffected by current status)

- Windup phase noticeably longer than a flatscreen game (~0.4–0.6s minimum) — `EnemyController.windupDuration` field already exists (default 0.5s), so the timing hook is in place even though nothing plays back a visual/audio telegraph yet.
- Pair visual windup with an audio cue — not yet implemented.
- Telegraph **direction** — not yet implemented (current `Telegraph` state has no directional data).

---

## 3. Enemy AI state machine — ✅ Partially implemented (`EnemyController.cs`)

### States implemented

```
Dormant → Idle → Approach → Telegraph → AttackActive → Recover → (back to Approach)
                                              ↓ (player hits with Heavy tier)
                                          HitReact → Approach
                                              ↓ (HP <= 0)
                                          Dead
```

- `Dormant`: correct default state, `Update()` returns immediately for `Dormant`/`Dead` — matches the "no AI cost while inactive" goal.
- `Activate()` only transitions out of `Dormant` once — correctly guarded.
- Debug visualization: `bodyRenderer.material.color` changes per state (nice addition beyond the original plan — makes state easy to verify visually during testing).
- `HandleApproach()` moves the enemy toward `target` via `MoveTowards`, transitions to `Telegraph` once in `attackRange`.
- Timed transitions (`Telegraph → AttackActive → Recover → Approach`) work via a `stateTimer` countdown in `Update()`.

### Not yet implemented

- **`Blocked`/`Staggered` state** — doesn't exist yet; needed once parry/block is built.
- **`AttackActive` doesn't deal damage to the player.** The state times out and moves to `Recover`, but nothing calls `ApplyDamage` on the player during this window. Confirm whether this is deliberate ("player offense before player defense" milestone sequencing) or an oversight before treating combat as feature-complete.
- **Hit reaction tiers:** only `Light`/`Heavy` are computed (`info.impactVelocity >= heavyHitVelocityThreshold`). `HitTier.Parried` and `HitTier.Killing` exist in the enum but nothing produces them yet.
- **Hit-location-based reactions** — `hitZone` isn't factored into reaction tier (ties back to `SwordWeapon` hardcoding `HitZone.Torso`).

### Tuning fields already exposed (good — matches the "expose as ScriptableObject/Inspector fields, not constants" guidance)

`approachSpeed`, `attackRange`, `windupDuration`, `attackActiveDuration`, `recoverDuration`, `maxHP`, `heavyHitVelocityThreshold` are all serialized fields on `EnemyController`. Note: these currently live directly on the component rather than a per-enemy-type ScriptableObject — fine at one-enemy-type scale, worth revisiting if multiple enemy types are added, so tuning doesn't mean duplicating a prefab per variant.

---

## 4. Throwables & blow dart — ⬜ Not yet implemented

No `RockThrowable` or `BlowDart` scripts exist in the repo yet. Design plan (unchanged):

```csharp
public struct DamageInfo
{
    public float amount;
    public Vector3 hitPoint;
    public Vector3 hitDirection;
    public float impactVelocity;
    public DamageSourceType sourceType; // Melee, Thrown, Dart
    public HitZone hitZone;
    public GameObject instigator;
    public StatusEffectData appliedEffect; // null for sword/rock, set for status darts
}
```
This struct **is already implemented** exactly as planned (see §5) — so adding rock/dart weapon scripts is now just a matter of building the detection front-end (`OnCollisionEnter` for a thrown rigidbody) and constructing this same struct, per the original plan. This is a good next milestone: it's the best test of whether the existing `DamageInfo`/`IDamageable` abstraction holds up against a second, differently-shaped weapon.

- Rocks: standard `Rigidbody`, XRI grab + release-velocity throw, `OnCollisionEnter` → build `DamageInfo` → `ApplyDamage`.
- Blow dart: same projectile shape, different launch trigger. Start with simple button-launch; mic-amplitude "real blow" input remains a stretch goal, decoupled from the damage pipeline.

---

## 5. Shared damage pipeline — ✅ Implemented (`DamageInfo.cs`)

```csharp
public enum DamageSourceType { Melee, Thrown, Dart }
public enum HitZone { Head, Torso, Limb }

public struct DamageInfo
{
    public float amount;
    public Vector3 hitPoint;
    public Vector3 hitDirection;
    public float impactVelocity;
    public DamageSourceType sourceType;
    public HitZone hitZone;
    public GameObject instigator;
    public StatusEffectData appliedEffect;
}

public interface IDamageable
{
    void ApplyDamage(DamageInfo info);
}
```

Implemented **exactly** as planned in the original brainstorm, including the optional `StatusEffectData` field for future dart status effects (`StatusEffectData.cs` already exists as a supporting type). `EnemyController` implements `IDamageable` and correctly guards against double-firing `OnDeath`:

```csharp
public void ApplyDamage(DamageInfo info)
{
    if (currentState == EnemyState.Dead) return; // correct guard
    ...
}
```

This is the one piece of the plan that's already fully validated against real code — `SwordWeapon` → `DamageInfo` → `EnemyController.ApplyDamage` works end-to-end.

---

## 6. Enemy spawn & encounter activation — ✅ Implemented, simplified (`EncounterManager.cs`)

The implemented version is a reasonable simplification of the original design: instead of an `EncounterDefinition` ScriptableObject + prefab spawn-position data, enemies are **hand-placed in the scene and assigned directly** via a `List<EnemyController>` on `EncounterManager`. This is a sensible reduction in complexity for a first vertical slice — the original ScriptableObject-driven version is worth revisiting once encounters need to be reused/randomized across chapters, but isn't needed yet.

```csharp
public class EncounterManager : MonoBehaviour
{
    [SerializeField] string encounterId;
    [SerializeField] List<EnemyController> enemies;

    int aliveCount;
    bool started;

    public void BeginEncounter()
    {
        if (started) return;
        started = true;
        aliveCount = enemies.Count;
        foreach (var enemy in enemies)
        {
            enemy.OnDeath += HandleEnemyDeath;
            enemy.Activate();
        }
    }

    void HandleEnemyDeath(EnemyController enemy)
    {
        enemy.OnDeath -= HandleEnemyDeath;
        aliveCount--;
        if (aliveCount <= 0) CompleteEncounter();
    }

    void CompleteEncounter() => QuestManager.SetFlag($"{encounterId}_cleared");
}
```

Correctly wires into `QuestManager.SetFlag` — same completion currency as puzzles and dialogue, as planned.

**Two activation paths currently coexist in the repo:**
- `EncounterManager.BeginEncounter()` — the "real" system, intended to be called from an `AreaTrigger.onPlayerEnter`.
- `TestDummy.cs` — a lighter-weight manual test harness that calls `enemy.Activate()` directly on trigger enter for a single enemy, then destroys itself. Useful for isolated melee-feel testing (matches the "test against a static dummy first" build-order guidance), but separate from the real encounter-clearing flow. Worth being deliberate about which one any given test scene is using, so `_cleared` flags aren't missed.

**Not yet implemented (carried over from the original design, still relevant):**
- **Pre-placed-dormant vs. runtime-instantiated:** pre-placed is what's implemented (enemies exist in-scene, `Dormant` by default) — matches the recommended default.
- **The "reveal" moment** (avoid instant pop-in) — not addressed yet; enemies currently just switch state, no transition/reveal treatment.
- **Arena locking (`lockAreaUntilCleared`)** — not present in the current `EncounterManager`; no diegetic-barrier or invisible-wall locking exists yet. Fine to add later, opt-in per encounter as originally planned.
- **Checkpoint-aware re-entry** (skip already-cleared encounters, force enemies straight to `Dead` on scene load) — **not implemented**. This is the same underlying gap flagged in `02-game-architecture.md` (checkpoint respawn doesn't re-apply quest-flag state to the scene) — fixing that generally will fix this specifically too.
- **Don't-destroy-dead-enemies guidance** — currently moot since there's no despawn/destroy call at all yet; worth confirming intentionally once death animations exist.

### Decided: death/respawn behavior

**If the player dies mid-encounter, the encounter should fully reset** (all enemies back to full HP) on respawn at the checkpoint — decided, but **not yet wired up**, since there's currently no player-death trigger at all (ties back to §3's "enemies don't damage the player yet" gap) and no checkpoint-state-reapplication logic (ties back to the `CheckpointManager` gap in `02-game-architecture.md`).

### Updated build order (given current state)

1. ✅ One enemy through `Dormant → Activate() → ... → Dead`, triggered by a basic trigger — done via `TestDummy`.
2. ✅ `EncounterManager` with single/multi-enemy encounters, `OnDeath` → quest flag firing — done.
3. ⬜ Player-facing enemy attacks (prerequisite for parry/block and for meaningful "death" to exist at all).
4. ⬜ Checkpoint-aware re-entry (skip already-cleared encounters) — blocked on the general checkpoint-flag-reapplication fix.