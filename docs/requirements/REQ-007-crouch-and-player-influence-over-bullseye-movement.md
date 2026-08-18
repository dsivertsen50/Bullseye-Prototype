# REQ-007 — Crouch and Player Influence Over Bullseye Movement

## Goal

Allow players to influence the vertical position of their own bullseye through movement actions while preserving the bullseye's existing randomized surface movement.

For this first version:

* **Jumping should bias the bullseye upward**
* **Crouching should bias the bullseye downward**

The player should not gain direct control over the bullseye.

Randomized bullseye movement should continue at all times, with jumping and crouching applying a temporary directional influence on top of the existing random movement.

This requirement should also add a simple crouch action to the existing player movement system.

---

## Current State

The project currently has:

* working two-player networked multiplayer
* working independent Xbox controller input
* working keyboard/mouse input
* working walking and jumping
* working sprint
* working aiming/zoom
* working shooting
* working death and respawn
* working randomized continuous bullseye movement
* working location-based bullseye damage
* upper, middle, and lower bullseye damage zones
* multiplayer synchronization of bullseye behavior

The existing systems should be preserved.

---

# Desired Behavior

## Jump Influence

When the player jumps:

1. The player's existing jump behavior should continue normally.
2. The bullseye should receive a temporary **upward influence** relative to the player's body.
3. This influence should encourage the bullseye to move toward the upper portion of the body.
4. The movement should remain smooth.
5. The bullseye should not teleport vertically.
6. Existing randomized movement should continue during the influence.

Jump influence should be strong enough to be noticeable but should not give the player precise direct control.

---

## Crouch Influence

When the player crouches:

1. The player should enter a crouched state.
2. The bullseye should receive a **downward influence** relative to the player's body.
3. This influence should encourage the bullseye to move toward the lower portion of the body.
4. Existing randomized movement should continue while crouching.
5. The bullseye should remain attached to the player's surface.
6. Returning to standing should remove the active crouch influence.

The player should not be able to instantly force the bullseye to a particular location.

---

# Investigation Requirement

Before implementing anything, inspect:

* `PlayerControls.inputactions`
* `PlayerMovement`
* `PlayerNetworkSetup`
* the Player prefab
* current player capsule dimensions
* current jump behavior
* sprint behavior
* bullseye randomized movement
* bullseye surface-following logic
* bullseye network authority/synchronization
* location-based damage-zone logic
* death and respawn handling
* current controller bindings

Determine the smallest reliable way to:

1. add crouching to the existing movement system
2. apply player-driven vertical influence to the existing bullseye movement system

Do not replace the working randomized bullseye system.

Do not redesign the entire movement or input architecture unless genuinely necessary.

---

# Crouch Controls

Add a crouch action using:

### Keyboard

**Left Control**

### Xbox Controller

Prefer:

**B button**

Before assigning the controller binding, inspect the existing input map and confirm that the button is not already used for another required gameplay action.

If B is already occupied, choose the smallest reasonable non-conflicting controller binding and document the choice in the completion summary.

---

# Crouch Behavior Requirements

Crouching should visibly lower the player.

For the current capsule-based prototype, the implementation may adjust:

* capsule height
* camera height
* player body visual height or position
* related collider values

as necessary.

The crouch should:

* visibly make the player shorter
* lower the local camera appropriately
* preserve collision behavior
* preserve network movement
* work while moving
* restore the player to their normal standing dimensions when crouch is released

Prefer **hold-to-crouch** for this prototype.

Releasing the crouch input should attempt to return the player to standing.

---

## Crouch Movement Speed

Crouching should reduce player movement speed.

Use a configurable crouch speed multiplier.

Default:

`0.6 × normal walking speed`

This value should be adjustable in the Unity Inspector.

Crouching should not provide sprint speed.

If the player attempts to sprint while crouched, prefer normal crouch-speed behavior rather than combining sprint and crouch multipliers.

---

## Standing Clearance

If practical within the current movement architecture, prevent the player from standing up when there is insufficient space above them.

Do not build a complex stance system.

A simple collision/clearance check is sufficient.

If implementing safe standing clearance would require disproportionate complexity for the current prototype, document the limitation rather than broadly redesigning the controller.

---

# Bullseye Influence Requirements

## Core Principle

Jump and crouch should **influence** the bullseye's existing randomized movement.

They should not directly assign the bullseye a new position.

The bullseye should continue to:

* crawl continuously
* change direction randomly
* move around the body circumference
* obey surface-following rules
* remain synchronized over the network

Player influence should modify the vertical component of this movement.

---

# Jump Influence Requirements

When a successful jump begins:

* apply an upward bullseye influence
* measure upward relative to the player's local body orientation
* do not use world-space Y if doing so would break body-relative behavior
* preserve smooth movement
* preserve surface following

The upward influence may persist briefly after the jump begins.

Expose useful parameters such as:

* jump bullseye influence strength
* jump influence duration

Choose reasonable prototype defaults.

The influence should be noticeable during play without guaranteeing that the bullseye reaches the upper zone.

---

# Crouch Influence Requirements

While crouching:

* apply a downward bullseye influence
* measure downward relative to the player's body
* preserve randomized movement
* preserve surface following
* stop applying the downward influence when crouch ends

Expose a configurable:

* crouch bullseye influence strength

The player should be able to meaningfully encourage the bullseye downward by remaining crouched, but random movement should still prevent exact placement.

---

# Random Movement Interaction

Existing random bullseye movement must remain active.

The resulting behavior should conceptually combine:

`existing randomized movement + player vertical influence`

rather than:

`random movement OR player-controlled movement`

For example:

* a bullseye naturally moving downward may move downward more strongly while crouching
* a bullseye naturally moving upward may slow, stop, or eventually reverse under sustained crouch influence
* jumping should bias the path upward without forcing an instantaneous vertical displacement

The implementation should preserve some uncertainty.

---

# Damage Zone Interaction

Existing location-based damage zones should continue to use the bullseye's actual current position.

Player influence can therefore indirectly change the player's vulnerability.

For example:

* crouching may encourage the bullseye toward the lower, less-lethal zone
* jumping may encourage the bullseye toward the upper, more-lethal zone

Do not add new damage rules as part of REQ-007.

Preserve the existing upper/middle/lower damage configuration.

---

# Multiplayer Requirements

Bullseye influence must follow the existing authoritative/network-synchronized bullseye architecture.

A player's jump or crouch input should affect only that player's bullseye.

Both clients should observe substantially the same influenced bullseye movement.

Do not allow each client to independently apply different influence calculations if this could cause bullseye divergence.

Crouch state itself should also be represented correctly to other networked players so that both clients observe the player as crouched or standing.

---

# Input Isolation Requirements

The dual-controller behavior established previously must remain intact.

Examples:

* Controller 1 crouch affects only Player 1.
* Controller 2 crouch affects only Player 2.
* Controller 1 jump influences only Player 1's bullseye.
* Controller 2 jump influences only Player 2's bullseye.

Keyboard/mouse controls must remain functional.

---

# Respawn Requirements

After death and respawn:

* the player should return to the standing state
* normal standing collider/camera configuration should be restored
* sprint state should reset normally
* crouch influence should not remain active
* jump influence should not remain active
* bullseye randomized movement should resume
* exactly one bullseye should exist
* damage should reset according to the existing system

No previous-life stance or influence state should persist after respawn.

---

# Configurable Values

Expose useful prototype tuning parameters in the Unity Inspector.

At minimum:

## Crouch

* crouch movement speed multiplier
* crouched player/capsule height or equivalent value
* crouched camera height or offset, if needed

Default crouch speed:

`0.6 × walking speed`

## Jump Influence

* upward bullseye influence strength
* influence duration

## Crouch Influence

* downward bullseye influence strength

Keep the number of parameters manageable.

The values should be easy to tune during multiplayer playtesting.

---

# Constraints

* Preserve Netcode for GameObjects networking.
* Preserve dual-controller isolation.
* Preserve keyboard/mouse input.
* Preserve walking.
* Preserve jumping.
* Preserve sprint.
* Preserve aim/zoom.
* Preserve shooting.
* Preserve randomized bullseye movement.
* Preserve location-based bullseye damage.
* Preserve killing and respawning.
* Do not give direct analog control of the bullseye.
* Do not allow players to manually select a bullseye position.
* Do not add turning-based bullseye influence yet.
* Do not add left/right bullseye influence yet.
* Do not remove random movement while influence is active.
* Do not add crouch animations yet.
* Do not add slide mechanics yet.
* Do not add prone behavior.
* Do not add stamina costs for crouching or bullseye influence.
* Do not redesign the entire movement controller unless necessary.
* Avoid adding new packages unless genuinely required.
* Prefer the smallest reliable implementation appropriate for prototype testing.

---

# Acceptance Criteria

## AC1 — Keyboard Crouch

Holding **Left Control** places the keyboard/mouse player into the crouched state.

Releasing it returns the player to standing when standing is possible.

---

## AC2 — Controller Crouch

The assigned controller crouch input affects only the corresponding networked player.

---

## AC3 — Visible Crouch

Crouching visibly reduces the player's height.

The player's local camera is adjusted appropriately so the player also experiences the lower stance.

---

## AC4 — Crouch Movement

The player can move while crouched.

Using the default configuration, crouched movement is approximately:

`0.6 × normal walking speed`

---

## AC5 — Sprint/Crouch Interaction

Crouching does not incorrectly combine with sprint to produce excessive movement speed.

Crouch movement remains controlled by the crouch movement configuration.

---

## AC6 — Jump Upward Influence

Performing a successful jump applies a noticeable upward influence to the player's bullseye.

The bullseye moves upward relative to the body rather than world space.

---

## AC7 — Crouch Downward Influence

Remaining crouched applies a noticeable downward influence to the player's bullseye.

The influence ends when the player returns to standing.

---

## AC8 — Influence Is Gradual

Neither jumping nor crouching teleports the bullseye to another position.

Influence changes its movement gradually.

---

## AC9 — Random Movement Preserved

The bullseye continues randomized movement while jump or crouch influence is active.

Players cannot precisely place the bullseye by jumping or crouching.

---

## AC10 — Surface Following Preserved

Player influence does not cause the bullseye to:

* leave the player surface
* float away
* become embedded
* travel through the body
* lose its outward orientation

---

## AC11 — Damage Zones Preserved

The upper, middle, and lower damage zones continue functioning based on the bullseye's resulting position.

Bullseye influence does not directly alter damage values.

---

## AC12 — Multiplayer Synchronization

Both clients observe substantially the same:

* crouch state
* bullseye position
* influenced bullseye path

No obvious long-term divergence should occur.

---

## AC13 — Input Isolation

One player's jump or crouch input does not influence another player's bullseye.

---

## AC14 — Respawn Reset

After death and respawn:

* the player is standing
* crouch state is cleared
* bullseye influence state is cleared
* randomized bullseye movement resumes normally
* existing damage reset works
* exactly one bullseye exists

---

## AC15 — Configurability

The following can be tuned without modifying core gameplay code:

* crouch speed multiplier
* jump bullseye influence strength
* jump influence duration
* crouch bullseye influence strength

---

## AC16 — Existing Gameplay Preserved

The change must not break:

* network connection
* player spawning
* independent controllers
* keyboard/mouse controls
* walking
* jumping
* sprinting
* looking
* zoom
* shooting
* randomized bullseye movement
* location-based damage
* killing
* respawning

---

# Validation

The agent should:

1. Inspect the current input, movement, bullseye, damage, respawn, and networking architecture.
2. Confirm the proposed controller crouch binding does not conflict with an existing required action.
3. Explain the proposed crouch and bullseye-influence implementation before making broad architectural changes.
4. Implement the smallest reliable crouch system.
5. Add jump-based upward bullseye influence.
6. Add crouch-based downward bullseye influence.
7. Preserve existing randomized surface movement.
8. Allow Unity to compile.
9. Read the Unity Console through MCP and resolve errors caused by the implementation.
10. Inspect the Player prefab and relevant configuration afterward.
11. Verify that death/respawn clears crouch and influence state.
12. State which acceptance criteria can be verified automatically.
13. Clearly identify behaviors requiring human multiplayer playtesting.

Do not claim that the influence strength feels balanced unless it has actually been evaluated through human multiplayer playtesting.

Do not claim crouch visuals, camera height, or bullseye influence feel natural unless they have actually been observed during play.

---

# Completion Summary

After implementation, report:

* scripts, prefabs, input actions, or assets modified
* controller binding used for crouch
* how crouching changes the player's body/collider
* how the camera is adjusted during crouch
* how crouch movement speed is applied
* how jump-based bullseye influence works
* how crouch-based bullseye influence works
* how player influence combines with randomized movement
* how influenced bullseye movement remains synchronized in multiplayer
* Inspector parameters available for tuning
* how crouch and influence state reset on respawn
* any limitations requiring manual multiplayer testing
