# REQ-005 — Randomized Bullseye Surface Movement

## Goal

Enhance the existing continuous bullseye surface movement so that the bullseye wanders unpredictably across the player's body rather than following a consistently predictable path.

The bullseye should continue crawling smoothly across the body at a generally fixed pace, but its direction should change periodically in a randomized way.

The purpose of this requirement is to make the moving weak point less predictable while keeping it visually trackable and fair to shoot.

This requirement should extend the working continuous surface movement implemented previously rather than replace it with a fundamentally different system.

---

## Current State

The project currently has:

* working two-player networked multiplayer
* working independent Xbox controller input
* working keyboard/mouse input
* working player movement, jumping, sprinting, looking, and zoom
* working shooting
* working death and respawn
* working bullseye weak-point damage
* a bullseye that continuously crawls across the capsule-shaped player's surface
* bullseye movement that has been confirmed to function during multiplayer testing

The existing functionality should be preserved.

---

## Desired Behavior

While a player is alive:

1. The bullseye continuously moves across the player's body surface.
2. The bullseye continues moving at approximately the configured movement speed.
3. At randomized intervals, its direction of travel should change.
4. Direction changes should result in the bullseye wandering through different areas of the body over time.
5. Direction changes should be smooth enough that the bullseye remains visually trackable.
6. The bullseye must not teleport to a new surface position when its direction changes.
7. Movement should not follow an obviously repeating path.
8. The bullseye should be capable of wandering vertically as well as around the circumference of the player.
9. The bullseye should remain correctly attached and oriented to the player surface.
10. Multiplayer clients should continue to observe substantially the same bullseye location.

The bullseye should feel unpredictable, but not erratic or visually chaotic.

---

## Investigation Requirement

Before implementing anything, inspect:

* the current bullseye movement implementation
* the surface-following logic created for continuous bullseye movement
* the existing bullseye network synchronization
* the current bullseye movement speed configuration
* bullseye hit detection
* the Player prefab
* death and respawn handling
* Netcode ownership/authority for bullseye movement

Determine how the current continuous movement direction/path is generated.

Extend the existing implementation where practical.

Do not replace a working surface-following or networking system unless there is a clear technical reason to do so.

---

# Randomized Movement Requirements

## Direction Changes

The bullseye should periodically choose a new direction of movement across the surface.

Direction changes should:

* occur at randomized intervals
* produce meaningful variation in the path
* allow movement both vertically and horizontally around the body
* avoid an obvious repeating pattern
* preserve continuous movement across the surface

The bullseye must not teleport when a new direction is selected.

---

## Smooth Direction Transitions

Randomization should not make the bullseye abruptly snap between dramatically different movement vectors every frame.

Prefer smooth transitions between the current direction and the newly selected direction.

The amount of smoothing should be configurable if practical.

The result should look like the bullseye naturally changes course while crawling across the body.

A direction change may be noticeable, but it should not resemble teleportation, jitter, or frame-to-frame randomness.

---

## Randomization Frequency

The amount of time between direction changes should be configurable.

Prefer a randomized interval between configurable minimum and maximum values rather than a single perfectly consistent timer.

For example, the implementation may expose values conceptually similar to:

* minimum time before direction change
* maximum time before direction change

Choose reasonable prototype defaults.

The exact default timing should be selected based on the current bullseye movement speed so the bullseye has enough time to visibly travel before changing direction again.

---

## Fixed Movement Pace

REQ-005 should primarily randomize **direction**, not speed.

The bullseye should continue moving at approximately the configured movement speed from the existing implementation.

Do not introduce large random speed changes as part of this requirement.

Minor variation caused naturally by surface traversal or direction smoothing is acceptable.

---

# Surface Coverage

Randomized movement should allow the bullseye to gradually explore different areas of the player's body.

Over time, it should be capable of reaching:

* upper portions of the capsule
* middle portions
* lower portions
* front
* sides
* back

The algorithm does not need to guarantee mathematically uniform coverage.

However, the bullseye should not commonly become trapped indefinitely:

* near the top
* near the bottom
* on one side
* in a small looping path

If the existing surface movement uses boundaries or constraints, randomized movement should handle those boundaries gracefully.

---

# Boundary Behavior

When the bullseye approaches a surface boundary where continuing in the current direction is not valid, it should select or transition toward another valid direction.

It should not:

* leave the player surface
* become stuck permanently at a boundary
* repeatedly jitter against a boundary
* teleport across the player to escape a boundary

Use the simplest reliable boundary behavior compatible with the existing surface traversal system.

---

# Multiplayer Requirements

Randomization must remain compatible with the existing Netcode for GameObjects architecture.

Both connected players should observe substantially the same bullseye position and movement path for each networked player.

The implementation must avoid each client independently generating random direction changes if doing so could cause bullseye positions to diverge.

Random movement decisions should be authoritative or otherwise synchronized/deterministic according to the current networking architecture.

Do not redesign the broader networking system solely for this feature.

---

# Hit Detection Requirements

The randomized bullseye must remain the player's active vulnerable hit target.

As its movement direction changes:

1. The visible bullseye and its hit collider remain aligned.
2. Shooting its current visible location should register normally.
3. Shooting a position it has moved away from should not register as a bullseye hit.
4. Random direction changes must not temporarily detach the collider from the visible target.
5. Existing damage, death, and respawn behavior must remain functional.

---

# Respawn Behavior

After a player dies and respawns:

* exactly one bullseye should exist
* the bullseye should appear at a valid location on the body
* continuous movement should resume
* randomized direction changes should resume
* multiplayer synchronization should remain correct

The random movement state does not need to persist through death.

A respawned bullseye may begin with a newly selected movement direction.

---

# Configurable Values

Preserve the existing configurable movement parameters.

Expose additional values required for random movement in the Unity Inspector.

At minimum, configuration should include:

* bullseye movement speed
* surface offset
* minimum direction-change interval
* maximum direction-change interval

If used by the implementation, also expose a useful parameter such as:

* direction transition/smoothing speed

Avoid exposing unnecessary low-level parameters that do not provide useful prototype tuning.

---

# Bullseye Size Compatibility

Do not hard-code movement behavior based on the bullseye's current visual size.

The bullseye visual and vulnerable collider may be resized during gameplay tuning.

Randomized movement should continue functioning if the bullseye is made moderately larger or smaller.

Do not independently alter the current bullseye size as part of REQ-005 unless necessary to correct a technical issue.

---

# Constraints

* Preserve Netcode for GameObjects networking.
* Preserve the current two-player multiplayer workflow.
* Preserve controller isolation.
* Preserve keyboard/mouse controls.
* Preserve walking and jumping.
* Preserve sprint behavior.
* Preserve aim/zoom behavior.
* Preserve shooting.
* Preserve bullseye weak-point damage.
* Preserve killing and respawning.
* Preserve the existing continuous surface-following system where practical.
* Keep bullseye movement speed generally fixed.
* Do not implement player control over bullseye movement yet.
* Do not add body-region damage yet.
* Do not add headshot-specific behavior yet.
* Do not add multiple bullseyes.
* Do not introduce complex arbitrary-mesh pathfinding.
* Do not implement full animated humanoid mesh support yet.
* Do not add new packages unless genuinely required.
* Prefer the smallest reliable implementation appropriate for prototype testing.

---

# Acceptance Criteria

## AC1 — Continuous Randomized Movement

The bullseye continuously moves across the player's body while alive.

Its path is no longer consistently predictable or obviously repeating.

---

## AC2 — Random Direction Changes

The bullseye periodically changes its direction of travel.

The timing between direction changes varies rather than occurring at one perfectly fixed interval.

---

## AC3 — No Teleportation

Changing direction does not cause the bullseye to teleport to another position on the body.

Movement remains continuous.

---

## AC4 — Smooth Course Changes

Direction changes do not produce excessive jitter or visually chaotic movement.

The bullseye remains reasonably trackable by another player.

---

## AC5 — Fixed General Speed

Randomization does not introduce substantial random changes to the bullseye's movement speed.

The configured movement speed remains the primary control over how quickly the bullseye travels.

---

## AC6 — Broad Surface Movement

Over continued observation, the bullseye is capable of wandering through different vertical areas and different sides of the capsule.

It does not remain permanently restricted to one small section of the player.

---

## AC7 — Surface Following

The bullseye continues to remain on or immediately above the player surface.

It does not:

* float significantly away
* pass through the body
* become embedded
* detach when changing direction

---

## AC8 — Boundary Handling

The bullseye does not become permanently stuck or repeatedly jitter when reaching the limits of its valid surface movement.

It continues along a valid path.

---

## AC9 — Multiplayer Synchronization

Both multiplayer clients observe substantially the same randomized bullseye location and path for a given player.

Random decisions do not cause long-term client divergence.

---

## AC10 — Moving Hit Detection

The vulnerable collider continues to align with the visible bullseye throughout randomized movement.

Hits at the bullseye's current position register correctly.

---

## AC11 — Respawn Behavior

After death and respawn:

* exactly one bullseye exists
* it resumes surface movement
* randomized direction changes resume
* network synchronization remains functional

---

## AC12 — Configurability

The following can be tuned without editing code:

* movement speed
* minimum direction-change interval
* maximum direction-change interval

Any implemented movement-smoothing value should also be configurable if it is useful for playtesting.

---

## AC13 — Existing Gameplay Preserved

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
* bullseye damage
* killing
* respawning

---

# Validation

The agent should:

1. Inspect the existing continuous bullseye surface-movement implementation.
2. Identify how its current movement direction is generated.
3. Explain the proposed randomization approach before making broad architectural changes.
4. Extend the existing implementation with randomized direction changes.
5. Allow Unity to compile.
6. Read the Unity Console through MCP and resolve errors caused by the implementation.
7. Inspect the resulting Player prefab and bullseye configuration.
8. Confirm that random movement decisions use the appropriate network authority/synchronization.
9. Confirm that the visible bullseye and collider remain associated.
10. State which acceptance criteria can be verified automatically.
11. Clearly identify behaviors requiring human multiplayer playtesting.

Do not claim that unpredictability, visual smoothness, aiming difficulty, or multiplayer gameplay feel are satisfactory unless they have actually been evaluated during human playtesting.

---

# Completion Summary

After implementation, report:

* scripts, prefabs, or assets modified
* how random movement directions are selected
* how often direction changes occur
* how direction transitions are smoothed
* how surface boundaries are handled
* how randomized movement is synchronized over the network
* Inspector parameters available for tuning
* how the implementation behaves if the bullseye size is adjusted
* any limitations of the current capsule-specific implementation
* which behaviors still require manual multiplayer playtesting
