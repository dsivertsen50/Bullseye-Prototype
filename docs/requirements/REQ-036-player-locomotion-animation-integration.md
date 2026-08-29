# REQ-036 — Player Locomotion Animation Integration

## Summary

Integrate the currently available Mixamo animations with the existing player controller and newly rigged `Player Character V1` humanoid mesh.

The goal of this requirement is to establish the player's initial **full-body locomotion animation system**, allowing the visible player model to respond naturally to existing movement states including:

* Standing
* Walking
* Sprinting
* Crouching
* Crouch walking
* Going prone
* Remaining prone
* Returning from crouch to standing

This requirement should establish a reusable Animator architecture that can be expanded as additional animations become available.

The animation system must work correctly in multiplayer. A player's full-body animations are particularly important from the perspective of **other players observing that player**, although animation should also be reflected appropriately for the local player's first-person representation wherever the body is visible.

Animation must remain **visual only**. Existing player movement/controller code remains authoritative over actual movement.

---

# 1. Available Animation Assets

The following Mixamo `.fbx` animation files are currently available:

### Standing

* `Standing Idle.fbx`
* `Walking Forward.fbx`
* `Walking Backward.fbx`
* `Sprint Forward.fbx`

### Crouching

* `Standing to Crouching.fbx`
* `Crouching Idle.fbx`
* `Crouching Walk Forward.fbx`
* `Crouching Walk Backward.fbx`
* `Crouching Walk Left.fbx`
* `Crouching Walk Right.fbx`
* `Crouching to Standing.fbx`

### Prone

* `Crouch to Prone.fbx`
* `Prone Idle.fbx`

These animations should be integrated without requiring changes to their source FBX files.

---

# 2. Humanoid Retargeting

All animation clips should use Unity's **Humanoid animation system** so that the Mixamo animations can be retargeted onto `Player Character V1`.

The player character should have one authoritative Humanoid Avatar configuration.

Where appropriate, imported animation FBXs should reference/reuse the same compatible Avatar rather than creating unrelated animation rigs.

The implementation should verify that:

* Bone mapping is valid.
* The character remains correctly oriented.
* Hands, feet, hips, spine, and head animate properly.
* The character does not unexpectedly change scale.
* Animation does not cause the player GameObject to drift away from its network/controller position.

---

# 3. Root Motion

**Do not use animation root motion to move the player.**

The existing player controller remains responsible for:

* Position
* Velocity
* Sprinting
* Crouching
* Prone movement/state
* Jumping
* Gravity
* Network movement

Animator root motion should therefore be disabled.

Animations should visually represent movement already being produced by the player controller.

Import settings should be configured as necessary to prevent Mixamo clips from producing unwanted positional drift.

---

# 4. Animator Architecture

Create or update a reusable Animator Controller for the player.

The Animator should be organized around major locomotion states rather than creating ad-hoc animation triggers throughout gameplay code.

At minimum, support the following state groups:

1. Standing
2. Standing Locomotion
3. Sprinting
4. Standing → Crouching
5. Crouching
6. Crouching Locomotion
7. Crouching → Standing
8. Crouching → Prone
9. Prone

The architecture should make it straightforward to later add:

* Standing strafing
* Jumping
* Falling
* Landing
* Prone crawling
* Prone → crouch
* Dolphin dive
* Reload animations
* Grenade animations
* Weapon-specific poses
* Death animations
* Hit reactions

Do not tightly couple the system only to the currently available clips.

---

# 5. Animator Parameters

Expose animation parameters based upon **player state**, rather than individual keyboard/controller buttons.

Suggested parameters include:

* `MoveX` — float
* `MoveY` — float
* `MoveSpeed` — float
* `IsMoving` — bool
* `IsSprinting` — bool
* `IsCrouching` — bool
* `IsProne` — bool
* `IsGrounded` — bool

Additional parameters or triggers may be added where necessary for state transitions.

For example:

* `EnterCrouch`
* `ExitCrouch`
* `EnterProne`

However, avoid creating unnecessary triggers if state-driven transitions are more reliable.

The Animator should respond to the **actual resulting player state**, not directly to a particular keyboard or gamepad input.

This is important because the game supports multiple input methods and may later support AI, replays, or other movement sources.

---

# 6. Standing Idle

### Condition

Player is:

* Standing
* Grounded
* Not moving

### Animation

`Standing Idle.fbx`

The animation should loop continuously until the player begins another action.

---

# 7. Walking Forward

### Condition

Player is:

* Standing
* Moving primarily forward
* Not sprinting

### Animation

`Walking Forward.fbx`

The animation should loop while movement continues.

Animation playback speed may optionally scale slightly with actual player movement speed if required to reduce visible foot sliding.

---

# 8. Walking Backward

### Condition

Player is:

* Standing
* Moving primarily backward
* Not sprinting

### Animation

`Walking Backward.fbx`

The animation should loop while backward movement continues.

---

# 9. Standing Strafing — Temporary Fallback

Dedicated standing strafe-left and strafe-right animations are **not currently available**.

The animation system must nevertheless remain functional when the player moves sideways.

Until appropriate animations are imported:

* Do not break locomotion when lateral movement occurs.
* Use the closest reasonable existing locomotion animation or blend.
* Avoid locking the player's legs in `Standing Idle` while the player visibly slides sideways if a reasonable temporary fallback is available.

The Animator architecture must contain an obvious place for future:

* `Walking Left`
* `Walking Right`

animations.

Once those clips are available, they should be able to replace the fallback without redesigning the controller.

---

# 10. Sprinting

### Condition

Player is:

* Standing
* Grounded
* Moving
* Sprint state is active

### Animation

`Sprint Forward.fbx`

The animation should loop continuously while sprinting.

The sprint animation should stop immediately when the player:

* Stops sprinting
* Stops moving
* Crouches
* Goes prone
* Becomes otherwise unable to sprint

The existing sprint mechanics remain authoritative.

---

# 11. Standing → Crouching

When the player initiates a crouch from the standing position:

### Animation

`Standing to Crouching.fbx`

The animation should play as a short transition animation.

After it completes, transition into either:

* `Crouching Idle`

or

* Appropriate crouch movement animation

depending upon current movement input.

Gameplay crouching should not need to wait unnecessarily for the animation to finish.

The animation represents the gameplay transition rather than controlling it.

---

# 12. Crouching Idle

### Condition

Player is:

* Crouched
* Not moving

### Animation

`Crouching Idle.fbx`

The animation should loop continuously.

---

# 13. Crouching Locomotion

Use directional movement to select the appropriate animation.

### Forward

`Crouching Walk Forward.fbx`

### Backward

`Crouching Walk Backward.fbx`

### Left

`Crouching Walk Left.fbx`

### Right

`Crouching Walk Right.fbx`

A **2D Blend Tree** is preferred for crouched locomotion if practical.

Suggested mapping:

* MoveY +1 = Forward
* MoveY -1 = Backward
* MoveX -1 = Left
* MoveX +1 = Right

Diagonal inputs should blend between the appropriate directional animations.

Example:

Forward + Right should blend:

`Crouching Walk Forward`

with:

`Crouching Walk Right`

This will establish a strong architecture that can later also be used for standing directional movement.

---

# 14. Crouching → Standing

When the player leaves the crouching state and returns to standing:

### Animation

`Crouching to Standing.fbx`

After the transition animation:

* If stationary → `Standing Idle`
* If moving forward → `Walking Forward`
* If moving backward → `Walking Backward`
* If sprinting → `Sprint Forward`

Gameplay state should remain authoritative even if input changes during the transition.

---

# 15. Crouching → Prone

The existing prone mechanic allows the player to enter prone by holding the crouch control for the required duration.

When this transition occurs:

### Animation

`Crouch to Prone.fbx`

The player should visually lower from crouching into the prone position.

Once the transition completes:

### Animation

`Prone Idle.fbx`

---

# 16. Prone Idle

### Condition

Player is:

* Prone
* Stationary

### Animation

`Prone Idle.fbx`

The animation should loop continuously.

---

# 17. Prone Movement — Temporary Fallback

There are currently no prone crawling animations.

The existing prone gameplay mechanics should continue functioning even if the player moves while prone.

Until prone locomotion animations are added:

* Maintain `Prone Idle` or another minimally disruptive visual fallback.
* Do not prevent prone movement.
* Do not allow the lack of an animation to interfere with networking or player controls.

The Animator should provide an obvious future location for:

* Prone Crawl Forward
* Prone Crawl Backward
* Prone Crawl Left
* Prone Crawl Right

---

# 18. Leaving Prone — Temporary Fallback

A dedicated:

* `Prone to Crouch`

or

* `Prone to Standing`

animation is not currently available.

Do **not** require one for this ticket.

When leaving prone, smoothly blend into the appropriate crouching or standing state according to the current gameplay logic.

Do not automatically play `Crouch to Prone` backward unless testing confirms that reversed playback looks visually acceptable.

The architecture should allow a proper prone-exit animation to be added later.

---

# 19. Dolphin Dive

The previously established dolphin-dive mechanic should **not be blocked by this ticket**.

A dedicated dolphin-dive animation is not currently available.

For now:

* Existing dolphin-dive gameplay should continue functioning.
* Animation may use a temporary/fallback state if necessary.
* Do not substitute `Crouch to Prone` in a way that makes the dive mechanically slower or changes player movement.

A dedicated dolphin-dive animation will be added later.

---

# 20. Jumping

There are currently no:

* Jump
* Falling
* Landing

animations included in this ticket.

Existing jump functionality must continue working.

The animation architecture should allow these states to be added later without rebuilding the locomotion system.

Do not allow the absence of these animations to interfere with jumping.

---

# 21. Animation Transition Quality

Transitions should use appropriate Animator crossfades/blend durations so animations do not visibly snap wherever possible.

Examples:

* Idle → Walk
* Walk → Sprint
* Walk → Idle
* Crouch Idle → Crouch Walk
* Direction changes while crouched

Transition duration should be short enough that controls continue feeling responsive.

Transition animations such as:

* Standing → Crouching
* Crouching → Standing
* Crouching → Prone

should not be blended so aggressively that their actual movement is no longer visible.

---

# 22. Animation Speed Synchronization

Walking/sprinting animations should visually correspond reasonably well with actual player movement speed.

If necessary, animator playback speed may be adjusted to reduce obvious:

* Foot sliding
* Moonwalking
* Excessively fast leg motion

Do not alter player gameplay speed simply to match an animation.

Animation should adapt to gameplay rather than gameplay adapting to the animation.

---

# 23. First-Person vs Third-Person / Remote Player Behavior

The game uses different perspectives for:

### Local Player

The player sees the game through the first-person camera.

### Remote Players

Other players see the full external character model.

Full-body locomotion animations are primarily intended to make **remote players appear alive and readable during combat**.

The system must therefore ensure that another player can visibly identify whether an opponent is:

* Standing
* Walking
* Sprinting
* Crouching
* Crouch walking
* Going prone
* Prone

For the local player:

* Do not allow full-body animation to manipulate the first-person camera unexpectedly.
* Do not allow Mixamo animation to override FPS weapon positioning.
* Do not allow animation to introduce camera bob or rotation unless intentionally implemented elsewhere.
* If portions of the local body are visible, they should animate consistently with the player's state.

The existing first-person weapon animation system should remain separate from this full-body locomotion system unless there is a deliberate reason to connect them.

---

# 24. Multiplayer Synchronization

Animation states must replicate correctly across the network.

Every player should see other players performing the appropriate animation based on their actual movement state.

For example:

If Player 1 crouches:

* Player 1 locally enters the crouched gameplay state.
* Player 2 sees Player 1 perform `Standing to Crouching`.
* Player 1 remains visually crouched for Player 2.
* Crouch movement animations update as Player 1 moves.

Likewise, sprinting, walking, prone, and other implemented locomotion states must be visible to all connected clients.

Avoid synchronizing individual animation clips or Animator state hashes unnecessarily if existing synchronized gameplay state can drive the remote Animator reliably.

Prefer deriving animation from already-networked player state where practical.

Animation synchronization should not significantly increase network traffic.

---

# 25. Network Ownership

Only the owning player's gameplay/controller systems should determine their movement state.

Remote clients should use replicated movement/state information to display the appropriate animation.

Animation code must not:

* Move remote players.
* Change player authority.
* Modify NetworkTransform behavior.
* Fight against the existing movement system.

---

# 26. Bullseye Compatibility

The player's bullseye must remain correctly attached to and positioned against the player's animated body.

Animation must not break:

* Bullseye positioning
* Bullseye movement system
* Bullseye surface attachment
* Bullseye hit detection
* Bullseye damage
* Bullseye shattering
* Grenade bullseye dislodgement

Because body geometry now moves with animation, verify that the bullseye continues to visually follow the relevant player body/surface as expected.

If adjustments are required to ensure that the bullseye follows animated bones correctly, make those adjustments without changing the established bullseye gameplay rules.

---

# 27. Existing Gameplay Must Remain Intact

REQ-036 must not regress existing functionality, including:

* Multiplayer movement
* Mouse/keyboard controls
* Gamepad controls
* Sprint
* Crouch
* Prone
* Dolphin dive
* Jump
* Shooting
* Weapon switching
* Grenades
* Bullseye movement
* Damage
* Death
* Respawn
* Camera behavior
* First-person weapon animations

Animations should observe player state, not replace the systems responsible for that state.

---

# 28. Inspector / Debugging Support

Animator configuration should remain easy to inspect during development.

While the game is running, developers should be able to select a Player object and determine values such as:

* Current movement values
* Sprint state
* Crouch state
* Prone state
* Current Animator state

Avoid unnecessarily hiding all animation state behind private code that is difficult to debug.

Optional temporary Animator/debug information may be added if useful but should not appear in the production HUD.

---

# 29. Architecture / Code Quality

Do not scatter direct calls such as:

`animator.Play("Walking Forward")`

throughout movement/input scripts.

Prefer a dedicated animation-driving component such as:

`PlayerAnimationController`

or similar.

This component should:

1. Read the player's authoritative movement/state information.
2. Convert that information into Animator parameters.
3. Allow the Animator Controller to determine appropriate animation transitions.

The solution should be extensible enough that new animation files can generally be integrated by:

1. Importing the animation.
2. Adding it to the Animator.
3. Updating the relevant Blend Tree/state.

rather than rewriting the player controller.

---

# 30. Acceptance Criteria

REQ-036 is complete when all of the following are true:

### Standing

* Stationary standing players use `Standing Idle`.
* Moving forward uses `Walking Forward`.
* Moving backward uses `Walking Backward`.
* Sprinting uses `Sprint Forward`.

### Crouching

* Entering crouch plays `Standing to Crouching`.
* Stationary crouched players use `Crouching Idle`.
* Forward crouch movement uses `Crouching Walk Forward`.
* Backward crouch movement uses `Crouching Walk Backward`.
* Left crouch movement uses `Crouching Walk Left`.
* Right crouch movement uses `Crouching Walk Right`.
* Diagonal crouch movement blends reasonably between directional animations.
* Returning to standing plays `Crouching to Standing`.

### Prone

* Going from crouch to prone plays `Crouch to Prone`.
* Stationary prone players use `Prone Idle`.
* Missing prone locomotion/exit animations use graceful temporary fallbacks.

### Technical

* Player movement remains controller-driven rather than root-motion-driven.
* Animations do not cause positional drift.
* The FPS camera is not unexpectedly manipulated by the full-body Animator.
* Existing weapon animations remain operational.
* Local gameplay remains responsive during animation transitions.
* Remote players see the correct locomotion state in multiplayer.
* Animation synchronization does not create significant unnecessary network traffic.
* Bullseyes continue functioning correctly on animated player bodies.
* Existing movement and combat systems continue functioning.
* The Animator/controller is structured so additional animations can be added without major redesign.

---

# 31. Testing Checklist

Test with at least two multiplayer players.

For each player, verify that both the owning player and remote observer correctly handle:

1. Stand still.
2. Walk forward.
3. Walk backward.
4. Move sideways.
5. Sprint forward.
6. Stop sprinting.
7. Enter crouch.
8. Remain crouched.
9. Crouch-walk forward.
10. Crouch-walk backward.
11. Crouch-walk left.
12. Crouch-walk right.
13. Move diagonally while crouched.
14. Return from crouch to standing.
15. Hold crouch to enter prone.
16. Remain prone.
17. Move while prone using the temporary animation fallback.
18. Leave prone.
19. Dolphin dive.
20. Jump.
21. Shoot while performing locomotion.
22. Switch weapons while performing locomotion.
23. Throw a grenade while performing locomotion.
24. Die and respawn.

Confirm throughout testing that the character:

* Does not slide away from its gameplay position because of animation.
* Does not rotate unexpectedly.
* Does not change scale.
* Does not break the first-person camera.
* Does not break the weapon rig.
* Does not break bullseye placement or hit detection.
* Displays the same broad movement state to all multiplayer clients.

---

# 32. Future Animation Expansion

The following animation gaps are acknowledged and are **not required for REQ-036**:

* Standing Walk Left
* Standing Walk Right
* Jump Start
* Falling
* Landing
* Prone Crawl Forward
* Prone Crawl Backward
* Prone Crawl Left
* Prone Crawl Right
* Prone to Crouch
* Prone to Standing
* Dolphin Dive
* Sprint Stop
* Sprint-to-crouch/sliding transitions
* Death animations
* Hit reactions

REQ-036 should intentionally leave clean extension points for these animations.

The immediate objective is not to create the game's final animation set. It is to establish a robust animation system using the assets currently available so future animation work becomes incremental rather than requiring another locomotion-system rewrite.
