# REQ-006 — Location-Based Bullseye Damage

## Goal

Make the amount of damage dealt by a successful bullseye hit depend on where the bullseye is currently located on the player's body.

For the current capsule-based prototype, divide the player body into simple vertical damage zones.

The purpose of this requirement is to make bullseye position strategically meaningful without introducing a full anatomical hit-location system.

---

## Current State

The project currently has:

* working two-player networked multiplayer
* working independent controller input
* working keyboard/mouse input
* working player movement, jumping, sprinting, looking, and zoom
* working shooting
* working death and respawn
* working bullseye weak-point hit detection
* continuous randomized bullseye movement across the player's capsule surface
* multiplayer bullseye synchronization that has been manually tested successfully

The existing systems should be preserved.

---

## Desired Behavior

The player's capsule should be divided into three vertical bullseye damage zones:

1. **Upper Zone**
2. **Middle Zone**
3. **Lower Zone**

When the bullseye is hit, damage should be determined by the zone in which the bullseye is currently located.

Default prototype behavior:

* **Upper Zone:** 1 successful bullseye hit to kill
* **Middle Zone:** 2 successful bullseye hits to kill
* **Lower Zone:** 3 successful bullseye hits to kill

These values should be configurable.

The exact zone boundaries should also be configurable or centralized enough to adjust easily during playtesting.

---

# Investigation Requirement

Before implementing anything, inspect:

* the existing player health/damage system
* bullseye hit detection
* `PlayerShoot`
* the current bullseye movement implementation
* player death behavior
* respawn behavior
* the Player prefab
* the player capsule/body dimensions
* Netcode ownership and authority for damage
* any current health or hit-count variables
* any existing hit feedback

Determine the smallest reliable way to make bullseye damage depend on its current body-relative vertical position.

Do not build a new unrelated damage system if the existing health/damage implementation can be extended.

---

# Damage Zone Requirements

## Zone Definition

Determine the bullseye's active damage zone from its position relative to the player's body.

The zones should be based on the bullseye's **local vertical position on the player**, not its world-space height.

Rotating, moving, jumping, or respawning must not change which zone corresponds to which portion of the player's body.

For the current capsule, divide the usable vertical body area into:

* upper
* middle
* lower

The implementation does not need to correspond to real anatomy yet.

---

## Default Damage Behavior

Use the following defaults:

### Upper Zone

A successful bullseye hit should cause immediate death.

Equivalent prototype behavior:

`1 hit to kill`

### Middle Zone

A player should survive one successful bullseye hit and die on the second.

Equivalent prototype behavior:

`2 hits to kill`

### Lower Zone

A player should survive two successful bullseye hits and die on the third.

Equivalent prototype behavior:

`3 hits to kill`

---

## Configurability

The required number of successful bullseye hits for each zone should be configurable in the Unity Inspector or another centralized gameplay configuration location.

At minimum expose:

* upper-zone hits to kill
* middle-zone hits to kill
* lower-zone hits to kill

Default values:

* Upper: `1`
* Middle: `2`
* Lower: `3`

Avoid hard-coding these values across multiple scripts.

---

# Zone Boundary Requirements

Zone boundaries should be configurable enough for prototype tuning.

A reasonable default is approximately equal vertical thirds of the current body surface.

Do not assume the current capsule dimensions will remain permanent.

Prefer normalized local body height or another approach that allows the boundaries to remain meaningful if the temporary player body is resized.

If practical, expose values conceptually similar to:

* lower/middle boundary
* middle/upper boundary

Use normalized values rather than arbitrary world-space coordinates where possible.

---

# Damage State Behavior

Damage should accumulate on the player across successive successful bullseye hits.

Example:

If the bullseye is in the lower zone:

1. First successful hit is registered.
2. Player remains alive.
3. Bullseye continues moving normally.
4. Second successful hit is registered.
5. Player remains alive.
6. Third successful hit kills the player.

The damage already received should remain with the player even if the bullseye subsequently moves into another damage zone.

Do not reset accumulated damage simply because the bullseye changes location.

---

## Recommended Damage Model

Prefer a simple health/damage model rather than maintaining separate independent hit counters for each zone.

For example, the system may use:

* configurable maximum health
* upper-zone damage
* middle-zone damage
* lower-zone damage

or an equivalent architecture.

The final implementation does not have to follow that exact model if the existing damage system suggests a cleaner approach.

The important behavior is that the default values produce approximately:

* upper = lethal in 1 hit
* middle = lethal in 2 hits
* lower = lethal in 3 hits

Prefer a model that can evolve later if additional weapons or damage values are introduced.

---

# Movement Interaction

The damage zone should be evaluated at the time the bullseye is successfully hit.

Because the bullseye continuously moves:

* a shot should use the bullseye's current authoritative location
* the damage zone should not be based on where the bullseye was previously
* direction changes should not interfere with zone detection

If the bullseye is near a zone boundary, use the authoritative position at the time the hit is processed.

---

# Multiplayer Requirements

Damage and death must remain authoritative within the existing Netcode for GameObjects architecture.

A client should not independently decide how much damage another player receives if the current architecture uses server-authoritative damage.

The authoritative hit-processing system should determine:

1. whether the bullseye was successfully hit
2. which zone the bullseye occupied
3. how much damage to apply
4. whether the hit causes death

Both clients should observe the resulting health/death state consistently.

---

# Hit Feedback Requirements

Because not every bullseye hit will now cause immediate death, provide simple confirmation that a successful hit was registered.

Implement the smallest practical hit feedback appropriate to the existing prototype.

At minimum, provide one clear indication of a successful bullseye hit.

Preferred options include:

* a brief hitmarker for the shooter
* a brief visual flash on the bullseye
* both, if simple to add using the existing architecture

Do not build a polished HUD system.

The goal is simply to prevent ambiguity about whether a non-lethal shot actually registered.

If a hitmarker or bullseye flash already exists, preserve and reuse it.

---

# Death and Respawn Requirements

When accumulated damage reaches the lethal threshold:

* existing death behavior should trigger
* existing respawn behavior should remain unchanged

After respawn:

* player damage/health must reset completely
* the player should not retain damage from the previous life
* the bullseye should resume randomized movement
* damage-zone evaluation should continue functioning normally
* exactly one bullseye should exist

---

# Future Compatibility

The current player body is a capsule.

Do not implement anatomical body-part detection as part of REQ-006.

However, avoid coupling the entire damage system permanently to capsule height.

The current zone-classification logic should be reasonably isolated so that it could later be replaced by logic based on:

* head
* torso
* arms
* legs
* bones
* colliders
* animation rig regions

without requiring the entire combat system to be rewritten.

The immediate priority remains a simple, reliable capsule implementation.

---

# Configurable Values

At minimum, expose or centralize:

### Damage

* upper-zone damage or hits-to-kill
* middle-zone damage or hits-to-kill
* lower-zone damage or hits-to-kill

### Zone Boundaries

* upper-zone threshold
* lower-zone threshold

If the existing health architecture uses total health and damage values instead of hits-to-kill, choose defaults that reproduce the intended 1/2/3-hit behavior.

Avoid unnecessary tuning parameters.

---

# Constraints

* Preserve Netcode for GameObjects networking.
* Preserve independent controller behavior.
* Preserve keyboard/mouse input.
* Preserve walking.
* Preserve jumping.
* Preserve sprinting.
* Preserve looking.
* Preserve zoom.
* Preserve shooting.
* Preserve randomized bullseye movement.
* Preserve bullseye surface following.
* Preserve killing and respawning.
* Do not add real anatomical body parts yet.
* Do not add head models or humanoid rigs.
* Do not add weapon-specific damage yet.
* Do not add armor.
* Do not add healing.
* Do not add health pickups.
* Do not add a full health HUD unless genuinely necessary.
* Do not add player control over bullseye movement yet.
* Prefer the smallest reliable implementation appropriate for prototype testing.
* Avoid new packages unless genuinely required.

---

# Acceptance Criteria

## AC1 — Upper Zone

When the bullseye is in the upper zone, one successful bullseye hit kills the player using the default configuration.

---

## AC2 — Middle Zone

When the bullseye is in the middle zone:

* the first successful hit does not kill the player
* the second successful hit kills the player

using the default configuration.

---

## AC3 — Lower Zone

When the bullseye is in the lower zone:

* the first successful hit does not kill the player
* the second successful hit does not kill the player
* the third successful hit kills the player

using the default configuration.

---

## AC4 — Damage Persists Across Bullseye Movement

Damage already received remains applied even if the bullseye moves into another zone.

Example:

1. Player receives a non-lethal lower-zone hit.
2. Bullseye later moves to another zone.
3. The previously received damage is still present.

---

## AC5 — Current Position Determines Damage

The zone used for a successful hit corresponds to the bullseye's current body-relative position when the hit is processed.

---

## AC6 — Body-Relative Zones

Walking, jumping, turning, or changing world-space position does not alter the meaning of upper, middle, and lower zones.

---

## AC7 — Configurable Damage

The damage behavior for each zone can be adjusted without editing the core combat code.

---

## AC8 — Configurable Boundaries

Zone boundaries can be tuned without rewriting the zone-classification logic.

---

## AC9 — Multiplayer Authority

Both connected players observe consistent damage and death behavior.

A client cannot incorrectly determine another player's damage state independently of the authoritative combat system.

---

## AC10 — Hit Confirmation

A successful non-lethal bullseye hit provides clear prototype-level feedback so the shooter can tell the hit registered.

---

## AC11 — Respawn Reset

After death and respawn:

* accumulated damage resets
* the player returns to full/default health
* bullseye movement resumes
* location-based damage continues functioning
* no duplicate bullseyes are created

---

## AC12 — Existing Gameplay Preserved

The implementation must not break:

* network connection
* player spawning
* independent controllers
* keyboard/mouse input
* walking
* jumping
* sprinting
* looking
* zoom
* shooting
* randomized bullseye movement
* killing
* respawning

---

# Validation

The agent should:

1. Inspect the current damage, health, bullseye, shooting, respawn, and networking architecture.
2. Explain the proposed damage-zone approach before making broad architectural changes.
3. Implement body-relative vertical damage zones.
4. Configure defaults that produce 1-hit, 2-hit, and 3-hit lethality for upper, middle, and lower zones.
5. Implement simple successful-hit feedback if sufficient feedback does not already exist.
6. Allow Unity to compile.
7. Read the Unity Console through MCP and resolve errors caused by the implementation.
8. Inspect the resulting Player prefab and relevant gameplay configuration.
9. Verify health resets on respawn.
10. State which acceptance criteria can be verified automatically.
11. Clearly identify behaviors requiring manual multiplayer playtesting.

Do not claim the damage balance feels good unless it has actually been evaluated through human multiplayer playtesting.

Do not claim visual hit feedback is sufficiently readable unless it has been observed during play.

---

# Completion Summary

After implementation, report:

* scripts, prefabs, and assets modified
* how damage zones are determined
* how zone boundaries are configured
* how damage is calculated for each zone
* how accumulated damage is stored
* how damage is synchronized/authorized in multiplayer
* how successful non-lethal hits are communicated to the shooter
* how health resets on respawn
* Inspector parameters available for tuning
* limitations of the current capsule-based zone implementation
* what would likely need to change when the player is replaced with an animated humanoid body
