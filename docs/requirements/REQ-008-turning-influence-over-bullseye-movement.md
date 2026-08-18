# REQ-008 — Turning Influence Over Bullseye Movement

## Goal

Allow player turning to influence the circumferential position of the player's own bullseye.

When a player turns left or right, the bullseye should gradually begin moving around the player's body in the **same direction as the turn**, but with a noticeable delay.

The purpose of this mechanic is to create a strategic tradeoff when turning toward an opponent.

For example, if:

* an opponent is to the player's right
* the player's bullseye is currently on the left/opposite side of their body
* the player turns right to face the opponent

then the rightward turn should begin influencing the bullseye to move rightward around the player's body after a short delay.

This may gradually bring the bullseye around toward the opponent's view, creating additional vulnerability as a consequence of turning to engage them.

The player should influence the bullseye rather than directly control it.

---

## Current State

The project currently has:

* working two-player networked multiplayer
* independent Xbox controller input
* keyboard/mouse input
* walking and jumping
* sprint
* crouch
* aim/zoom
* shooting
* death and respawn
* continuous randomized bullseye surface movement
* location-based bullseye damage
* jump-based upward bullseye influence
* crouch-based downward bullseye influence
* synchronized multiplayer bullseye behavior

The existing systems should be preserved.

---

# Desired Behavior

When the player rotates horizontally:

1. Detect the direction and meaningful amount of player yaw rotation.
2. Do not immediately move the bullseye because of the turn.
3. After a configurable short delay, begin applying circumferential bullseye influence in the same direction as the player's turn.
4. Apply the influence smoothly.
5. Allow the bullseye to continue its existing randomized movement at the same time.
6. Preserve existing vertical influence from jumping and crouching.
7. Stop or decay the turning influence after the player stops turning.
8. Keep the bullseye attached to the body surface throughout the movement.

The resulting behavior should feel like the bullseye **lags behind the player's turning action and is then gradually pulled in the same rotational direction**.

It should not behave like a rigid object fixed to the player's orientation.

---

# Example Behavior

Assume Player 1 is facing forward.

Their bullseye is currently positioned on the left side of their body.

An opponent appears approximately 90 degrees to Player 1's right.

Player 1 turns right to face the opponent.

Expected behavior:

1. Player 1's camera/body begins turning right immediately.
2. The bullseye does not instantly jump around the body.
3. After the configured delay, the bullseye begins receiving rightward circumferential influence.
4. It gradually moves from the left side toward the back/front/right portions of the body depending on its current movement and random path.
5. This may eventually move the bullseye into a more visible position for the opponent Player 1 just turned toward.

This mechanic should create risk when rapidly reorienting toward another player.

---

# Investigation Requirement

Before implementing anything, inspect:

* `PlayerLook`
* `PlayerMovement`
* `PlayerNetworkSetup`
* the Player prefab
* mouse look behavior
* controller look behavior
* how player yaw/body rotation is currently determined
* current randomized bullseye movement
* capsule surface traversal
* jump-based bullseye influence
* crouch-based bullseye influence
* bullseye network authority and synchronization
* death and respawn handling

Determine:

1. which transform represents meaningful horizontal player facing direction
2. how yaw changes are currently applied
3. where bullseye movement direction is calculated
4. how the new circumferential influence can be combined with existing random and vertical influences

Do not replace the working bullseye movement system.

Do not make broad changes to the look/camera architecture unless genuinely necessary.

---

# Turning Detection Requirements

## Horizontal Rotation

Turning influence should be based primarily on **horizontal/yaw rotation**.

Looking upward or downward should not create circumferential bullseye movement.

Examples:

* turning right → rightward circumferential influence
* turning left → leftward circumferential influence
* looking straight up → no turning influence
* looking straight down → no turning influence

Use the player body's meaningful local yaw/facing rotation rather than an unrelated camera transform if the camera can pitch independently.

---

## Turn Direction

The implementation must correctly determine whether the player is turning:

* left
* right
* not meaningfully turning

Handle angle wrapping correctly.

For example, crossing from approximately `359°` to `1°` should be interpreted as a small rotation rather than a 358-degree turn.

---

# Delayed Influence Requirements

Turning must not affect the bullseye instantaneously.

After a meaningful turn begins, wait a configurable amount of time before the corresponding bullseye influence becomes active.

Expose a value conceptually similar to:

`turn influence delay`

Choose a reasonable prototype default.

A starting point may be approximately:

`0.25–0.5 seconds`

The exact value should remain configurable for playtesting.

The delay should be noticeable enough that the bullseye feels like it responds after the player's movement rather than being mechanically locked to rotation.

---

# Influence Strength

Once active, turning should apply circumferential movement influence to the bullseye.

The amount of influence should depend on the player's turning behavior.

Prefer a system where larger/faster turns can create stronger influence than extremely small aiming adjustments.

However, keep the implementation simple and tunable.

Expose at minimum:

* turn bullseye influence strength

If useful, the implementation may also expose:

* maximum turning influence
* turn-rate scaling

Do not create an unnecessarily complex physics simulation.

---

# Small Aim Adjustment Protection

Normal FPS aiming often involves very small mouse or stick corrections.

These should not constantly create strong bullseye movement.

Use a configurable threshold or similar approach so that extremely small yaw changes can be ignored or have minimal effect.

Expose a value conceptually similar to:

`minimum turn rate for bullseye influence`

The goal is to distinguish meaningful turning from minor aiming jitter.

---

# Influence Decay

When the player stops turning, the turning influence should not remain indefinitely.

After turning stops:

* circumferential influence should smoothly decay or end
* existing randomized bullseye movement should remain
* existing jump/crouch influences should continue according to their own rules

Prefer a smooth decay rather than an abrupt movement discontinuity if practical.

Expose a turn influence decay/smoothing value if useful for tuning.

---

# Same-Direction Requirement

The bullseye should be influenced in the **same rotational direction as the player turned**.

Examples:

### Right Turn

Player rotates clockwise/right relative to their body orientation.

The bullseye receives rightward circumferential influence.

### Left Turn

Player rotates counterclockwise/left.

The bullseye receives leftward circumferential influence.

This behavior should be consistent regardless of the bullseye's current location around the capsule.

---

# Circumferential Surface Movement

Turning influence should primarily affect the bullseye's movement **around the circumference of the player's body**.

It should not directly force the bullseye upward or downward.

Vertical movement should continue to come from:

* existing random movement
* jump influence
* crouch influence

Turning influence should therefore add a horizontal/circumferential component to the existing surface movement.

---

# Interaction With Existing Bullseye Movement

The resulting movement should conceptually combine:

`random movement + jump/crouch vertical influence + turning circumferential influence`

These systems should coexist rather than replacing one another.

Examples:

* a bullseye randomly moving left may slow or reverse if the player generates sufficient right-turn influence
* a bullseye already moving right may move more strongly right when the player turns right
* crouching may continue pulling the bullseye downward while a right turn simultaneously moves it around the player's circumference
* jumping may move it upward while a left turn simultaneously moves it around the body

The bullseye should still retain meaningful unpredictability.

---

# Delayed / Inertial Feel

The intended experience is not simply:

`player rotates → bullseye immediately rotates`

The desired feeling is closer to:

`player rotates → brief delay → bullseye begins drifting in that direction`

This should create an impression of lag or inertia.

Do not implement true rigid-body physics unless there is a compelling technical reason.

A simple delayed and smoothed influence system is preferred.

---

# Strategic Behavior

The system should support situations where turning toward an opponent may gradually expose a previously protected bullseye.

For example:

* bullseye is on the player's left side
* opponent is on the player's right
* player turns right toward opponent
* delayed rightward influence begins moving the bullseye around the body
* opponent may gain a better opportunity to shoot it

Do not attempt to detect the opponent or intentionally move the bullseye toward enemies.

The effect must arise purely from the player's own rotation and the bullseye's surface movement.

---

# Multiplayer Requirements

Turning influence must remain compatible with the existing Netcode for GameObjects architecture.

A player's turning should affect only that player's bullseye.

The authoritative/network-synchronized bullseye movement system should determine the final path.

Both clients should observe substantially the same:

* bullseye location
* circumferential influence
* resulting movement path

Do not independently simulate incompatible random/turning influences on different clients if doing so could cause divergence.

---

# Input Independence

Turning influence should work regardless of whether the player is turning using:

* mouse
* Xbox right stick

Do not implement separate gameplay rules for mouse and controller unless required by the existing input architecture.

The gameplay influence should be based on the resulting player yaw movement rather than directly on raw input where practical.

This helps ensure equivalent behavior between input devices.

---

# Respawn Requirements

After death and respawn:

* pending turning influence should be cleared
* turn delay state should be cleared
* accumulated turn influence should be cleared
* bullseye randomized movement should resume normally
* jump/crouch influence state should reset according to existing behavior
* exactly one bullseye should exist

A turn performed during the previous life must not continue influencing the bullseye after respawn.

---

# Configurable Values

Expose useful prototype tuning parameters in the Unity Inspector.

At minimum:

* turn bullseye influence strength
* turn influence delay
* minimum meaningful turn rate / threshold

If used by the implementation, also expose:

* turn influence decay rate
* maximum turn influence
* influence smoothing

Choose reasonable defaults so the mechanic can be immediately playtested.

Do not expose unnecessary implementation-specific values.

---

# Constraints

* Preserve Netcode for GameObjects networking.
* Preserve independent controller behavior.
* Preserve keyboard/mouse input.
* Preserve walking.
* Preserve jumping.
* Preserve sprint.
* Preserve crouch.
* Preserve aim/zoom.
* Preserve shooting.
* Preserve randomized bullseye movement.
* Preserve location-based damage.
* Preserve jump upward influence.
* Preserve crouch downward influence.
* Preserve killing and respawning.
* Do not directly control bullseye position from look input.
* Do not rigidly attach bullseye angular position to player rotation.
* Do not teleport the bullseye because of turning.
* Do not detect enemies as part of this mechanic.
* Do not deliberately move the bullseye toward the nearest opponent.
* Do not add left/right manual bullseye controls.
* Do not add a bullseye HUD indicator.
* Do not build a complex physics/inertia simulation unless genuinely required.
* Avoid adding new packages unless genuinely necessary.
* Prefer the smallest reliable implementation appropriate for prototype testing.

---

# Acceptance Criteria

## AC1 — Right Turn Influence

A meaningful rightward player turn eventually produces rightward circumferential bullseye influence.

---

## AC2 — Left Turn Influence

A meaningful leftward player turn eventually produces leftward circumferential bullseye influence.

---

## AC3 — Delayed Response

Bullseye turning influence does not begin instantaneously.

A configurable delay occurs between the player beginning a meaningful turn and the resulting bullseye influence.

---

## AC4 — Smooth Movement

Turning does not cause the bullseye to teleport, snap across the body, or visibly detach from the surface.

---

## AC5 — Circumferential Movement

Turning primarily affects the bullseye's movement around the circumference of the body rather than directly forcing it upward or downward.

---

## AC6 — Minor Aim Corrections

Very small mouse or controller aim corrections do not continuously generate strong turning influence.

---

## AC7 — Influence Strength Responds to Turning

Meaningful turns generate noticeable bullseye influence.

If the implementation uses turn-rate scaling, larger/faster turns should generally produce stronger influence up to the configured limit.

---

## AC8 — Influence Ends

When the player stops turning, turning influence eventually decays or ends.

It does not permanently override randomized movement.

---

## AC9 — Random Movement Preserved

The bullseye continues its existing randomized crawling behavior while turning influence is active.

The player cannot precisely position the bullseye simply by rotating.

---

## AC10 — Vertical Influence Preserved

Jumping and crouching continue to provide their existing upward and downward bullseye influences while turning influence is active.

---

## AC11 — Combined Influence

Turning and vertical influence can operate simultaneously.

For example:

* crouching + turning right can move the bullseye downward and around the body to the right
* jumping + turning left can move it upward and around the body to the left

---

## AC12 — Surface Following

Turning influence does not cause the bullseye to:

* leave the capsule surface
* float away
* become embedded
* pass through the body
* lose appropriate outward orientation

---

## AC13 — Damage Zones Preserved

Existing location-based damage remains based on the bullseye's actual resulting position.

Turning influence does not directly alter damage values.

---

## AC14 — Mouse Support

Mouse-driven player turning produces the expected bullseye influence.

---

## AC15 — Controller Support

Right-stick player turning produces the expected bullseye influence for the corresponding controller-owned player.

---

## AC16 — Multiplayer Synchronization

Both clients observe substantially the same bullseye movement resulting from player turning.

No obvious long-term divergence occurs.

---

## AC17 — Player Isolation

Player 1 turning affects only Player 1's bullseye.

Player 2 turning affects only Player 2's bullseye.

---

## AC18 — Respawn Reset

After death and respawn:

* pending turn influence is cleared
* turn-delay state is cleared
* randomized bullseye movement resumes
* other influence systems function normally
* exactly one bullseye exists

---

## AC19 — Existing Gameplay Preserved

The implementation must not break:

* network connection
* player spawning
* independent controllers
* keyboard/mouse controls
* walking
* jumping
* sprinting
* crouching
* looking
* zoom
* shooting
* randomized bullseye movement
* jump/crouch influence
* location-based damage
* killing
* respawning

---

# Validation

The agent should:

1. Inspect the current player look, networking, and bullseye movement architecture.
2. Identify which player transform/yaw value should drive turning influence.
3. Inspect how existing random, jump, and crouch influences are combined.
4. Explain the proposed delayed turning-influence approach before making broad architectural changes.
5. Implement the smallest reliable solution.
6. Ensure both mouse and controller turning use equivalent gameplay behavior.
7. Allow Unity to compile.
8. Read the Unity Console through MCP and resolve errors caused by the implementation.
9. Inspect the relevant Player prefab and Inspector configuration afterward.
10. Verify turn/influence state resets on death and respawn.
11. State which acceptance criteria can be verified automatically.
12. Clearly identify behaviors requiring human multiplayer playtesting.

Do not claim that the delayed movement feels strategically useful, intuitive, or appropriately balanced unless it has actually been evaluated during multiplayer playtesting.

Do not claim that turning toward another player creates the intended exposure tradeoff unless that behavior has actually been observed during human playtesting.

---

# Completion Summary

After implementation, report:

* scripts, prefabs, or assets modified
* which player rotation value drives the mechanic
* how left/right turn direction is determined
* how turn amount or turn rate affects influence
* how the delay is implemented
* how turning influence decays after turning stops
* how turning influence combines with randomized movement
* how it combines with jump and crouch influence
* how multiplayer synchronization is preserved
* Inspector parameters available for tuning
* how turn influence state resets on respawn
* behaviors that still require human multiplayer playtesting
