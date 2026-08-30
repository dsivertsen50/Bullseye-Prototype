# REQ-040 — Standing and Sprint Strafe Animation Integration

## Summary

Expand the existing REQ-036 player locomotion animation system using the newly added standing and sprint strafing animations.

The following animation assets are now available:

* `Walking Left Strafe.fbx`
* `Walking Right Strafe.fbx`
* `Sprinting Left Strafe.fbx`
* `Sprinting Right Strafe.fbx`

These animations should replace the temporary standing lateral-movement fallbacks established in REQ-036 and improve sprint animation behavior when the player moves laterally.

The implementation should preserve the existing Animator architecture and extend it rather than creating a separate locomotion system.

---

# 1. Primary Goal

Remote players should be able to visually distinguish whether another player is:

* Walking forward
* Walking backward
* Strafing left
* Strafing right
* Moving diagonally
* Sprinting forward
* Sprinting toward the left
* Sprinting toward the right

The character should no longer appear to play a forward walk animation while sliding sideways.

---

# 2. New Animation Assets

Integrate:

### Standing Locomotion

`Walking Left Strafe.fbx`

`Walking Right Strafe.fbx`

### Sprint Locomotion

`Sprinting Left Strafe.fbx`

`Sprinting Right Strafe.fbx`

Existing relevant animations remain:

* `Standing Idle.fbx`
* `Walking Forward.fbx`
* `Walking Backward.fbx`
* `Sprint Forward.fbx`

---

# 3. Standing Locomotion Blend Tree

Upgrade standing locomotion to use a **2D Blend Tree** similar to the crouching locomotion system established in REQ-036.

Use the existing movement parameters where practical:

* `MoveX`
* `MoveY`

Suggested directional mapping:

| Direction | MoveX | MoveY | Animation              |
| --------- | ----: | ----: | ---------------------- |
| Forward   |     0 |    +1 | `Walking Forward`      |
| Backward  |     0 |    -1 | `Walking Backward`     |
| Left      |    -1 |     0 | `Walking Left Strafe`  |
| Right     |    +1 |     0 | `Walking Right Strafe` |

The exact Blend Tree thresholds may be tuned as necessary.

---

# 4. Diagonal Walking

Diagonal movement should blend between the appropriate animations.

Examples:

### Forward + Left

Blend between:

* `Walking Forward`
* `Walking Left Strafe`

### Forward + Right

Blend between:

* `Walking Forward`
* `Walking Right Strafe`

### Backward + Left

Blend between:

* `Walking Backward`
* `Walking Left Strafe`

### Backward + Right

Blend between:

* `Walking Backward`
* `Walking Right Strafe`

The goal is smooth directional motion without requiring unique diagonal clips.

---

# 5. Movement Input vs World Direction

Animation direction should represent movement **relative to the player's facing direction**, not absolute world-space direction.

For example:

If the player faces north but moves west:

→ Left strafe

If the player turns west and continues moving west:

→ Forward movement

Use local-space velocity or the existing normalized movement inputs rather than raw world-space velocity where practical.

---

# 6. Standing Idle

Existing behavior remains unchanged.

When the player is:

* Standing
* Grounded
* Not moving
* Not sprinting

use:

`Standing Idle.fbx`

The locomotion Blend Tree should transition smoothly to/from idle.

---

# 7. Sprint Locomotion Upgrade

Sprinting currently uses:

`Sprint Forward.fbx`

Expand sprinting so that lateral sprint movement can use the new clips.

Available sprint clips:

* `Sprint Forward.fbx`
* `Sprinting Left Strafe.fbx`
* `Sprinting Right Strafe.fbx`

The sprint system should determine the player's dominant local movement direction.

---

# 8. Sprint Forward

When the player is sprinting primarily forward:

Use:

`Sprint Forward.fbx`

This remains the default sprint animation.

---

# 9. Sprint Left

When the player is sprinting with significant leftward movement:

Use:

`Sprinting Left Strafe.fbx`

This should apply when lateral left movement becomes substantial enough that `Sprint Forward` would no longer represent the player's movement accurately.

---

# 10. Sprint Right

When the player is sprinting with significant rightward movement:

Use:

`Sprinting Right Strafe.fbx`

---

# 11. Sprint Blend Tree

Prefer a Blend Tree or similarly smooth parameter-driven system rather than hard-switching between clips.

A sprint Blend Tree may use:

* `MoveX`
* `MoveY`

Suggested mapping:

| Direction | MoveX |               MoveY | Animation                |
| --------- | ----: | ------------------: | ------------------------ |
| Forward   |     0 |                  +1 | `Sprint Forward`         |
| Left      |    -1 | 0 or forward-biased | `Sprinting Left Strafe`  |
| Right     |    +1 | 0 or forward-biased | `Sprinting Right Strafe` |

Because no dedicated backward sprint animation currently exists, backward behavior should continue using the closest reasonable fallback.

---

# 12. Sprinting Diagonally

The most common sprint movement will likely involve combinations such as:

* Forward + Left
* Forward + Right

These should blend naturally.

Example:

Strong forward + moderate left:

Mostly:

`Sprint Forward`

with some:

`Sprinting Left Strafe`

Strong left + moderate forward:

Increase the influence of:

`Sprinting Left Strafe`

The animation should visually follow the actual direction of travel rather than simply reacting to whether A/D or the left stick is being pressed.

---

# 13. Backward Sprinting

There is currently no dedicated backward sprint animation.

If gameplay allows sprinting backward, do not block or change the mechanic as part of this requirement.

Use the most reasonable available fallback.

Possible fallback behavior:

* Use `Walking Backward` at an increased playback speed.

or

* Use an existing sprint state if that looks less disruptive.

Choose the option that looks best during testing.

The architecture should leave a clear slot for a future:

`Sprinting Backward`

animation.

---

# 14. Sprint Input Does Not Automatically Mean Sprint Animation

Animation should continue to follow the **actual gameplay sprint state**, not simply whether the sprint button is held.

For example:

Sprint button held while stationary:

→ `Standing Idle`

Sprint button held while crouched:

→ crouch animation

Sprint button held while gameplay prevents sprinting:

→ normal locomotion

Only use sprint animations while the authoritative player controller reports that the player is actually sprinting.

---

# 15. Existing Crouching Locomotion

Do not modify the existing crouch directional animation implementation except as necessary for shared Animator architecture.

Existing crouch animations remain:

* `Crouching Walk Forward`
* `Crouching Walk Backward`
* `Crouching Walk Left`
* `Crouching Walk Right`

The standing Blend Tree should ideally follow the same parameter conventions as the crouching Blend Tree.

---

# 16. Existing Prone Locomotion

Prone animations remain unchanged.

Existing prone states include:

* `Prone Idle`
* `Prone Forward`
* `Prone Backward`
* `Prone Left Turn`
* `Prone Right Turn`
* `Prone to Crouching`

REQ-040 should not interfere with these states.

---

# 17. Jump Compatibility

Existing jump logic from REQ-036 must remain operational.

Current jump-start behavior includes:

### Normal / Walking Jump

`Idle to Jump.fbx`

### Sprinting Jump

`Sprint to Jump.fbx`

If a player is sprint-strafing and jumps, the jump should still be treated as a sprint jump if gameplay reports that sprinting was active at takeoff.

Therefore:

Sprint Forward + Jump

→ `Sprint to Jump`

Sprint Left + Jump

→ `Sprint to Jump`

Sprint Right + Jump

→ `Sprint to Jump`

A dedicated lateral sprint-jump animation is not required.

---

# 18. Third-Person Weapon Compatibility

REQ-040 must remain compatible with the third-person weapon posing and IK system established in REQ-037.

The new strafing animations should continue to support:

* Pistol holding
* AK holding
* Shotgun holding
* Left-hand weapon IK
* Right-hand weapon attachment
* Hip-fire pose
* Aim pose

The lower-body locomotion animations should continue operating underneath the weapon-holding system.

---

# 19. Aiming While Strafing

Players should be able to aim while moving laterally.

Example:

Player aims with AK while moving left.

Remote player should see:

* Left strafe locomotion
* Upper-body AK weapon pose
* ADS/aim adjustment

The locomotion system must not force the weapon rig back into a neutral animation pose.

This is an important use case for FPS combat.

---

# 20. Strafing While Crouched vs Standing

Ensure the animation state is determined by posture first.

Example:

Standing + Left movement

→ `Walking Left Strafe`

Crouching + Left movement

→ `Crouching Walk Left`

Sprinting + Left movement

→ `Sprinting Left Strafe`

Prone + rotation left

→ `Prone Left Turn`

Do not allow the new standing clips to leak into crouched or prone states.

---

# 21. Crossfade Quality

Transitions between directional movement should be smooth.

Examples:

* Forward → Left
* Forward → Right
* Left → Forward
* Right → Forward
* Left → Right
* Walk → Sprint
* Sprint Forward → Sprint Left
* Sprint Left → Sprint Right

Prefer Blend Tree interpolation rather than repeated Animator state transitions when possible.

Avoid visible snapping of:

* Legs
* Hips
* Torso

during normal directional changes.

---

# 22. Fast Direction Changes

Players in an FPS may rapidly alternate directions while dodging.

Example:

Left → Right → Left

The animation system must remain responsive.

Do not require a full animation cycle before changing direction.

Animator parameters should follow gameplay movement immediately, with animation blending smoothing the visual result.

---

# 23. Foot Sliding

Tune animation playback speed or Blend Tree thresholds where necessary to reduce obvious foot sliding.

Do not modify actual gameplay movement speed to match animation.

Gameplay movement remains authoritative.

Animations should adapt visually to gameplay.

---

# 24. Root Motion

Root motion remains disabled.

None of the new strafe clips should directly move:

* Player GameObject
* NetworkTransform
* CharacterController
* Rigidbody

The player movement system remains completely authoritative.

---

# 25. Multiplayer Synchronization

Remote players must see the correct directional locomotion.

For example:

Player 1 walks left.

Player 2 sees:

`Walking Left Strafe`

Player 1 begins sprinting left.

Player 2 sees:

`Sprinting Left Strafe`

Player 1 transitions toward forward sprinting.

Player 2 sees the animation smoothly blend toward:

`Sprint Forward`

Do not synchronize specific animation clip names over the network if existing replicated movement information can drive the Animator locally.

---

# 26. Local-Space Remote Movement

If remote animation is derived from networked velocity rather than locally available input, convert remote player velocity into the player's local space.

Conceptually:

`localVelocity = transform.InverseTransformDirection(worldVelocity)`

Use the resulting local X/Z components to drive equivalent `MoveX` and `MoveY` Animator parameters.

This ensures remote directional animation remains correct even though another client's raw input values are unavailable.

---

# 27. Network Efficiency

Do not transmit:

* Animator bone transforms
* Animation timestamps every frame
* Animation clip names every frame

unless technically unavoidable.

Prefer deriving locomotion from already-networked information such as:

* Position
* Velocity
* Rotation
* Sprint state
* Crouch state
* Prone state

---

# 28. Animator Architecture

REQ-040 should extend the existing `PlayerAnimationController` or equivalent created for REQ-036.

Do not create a second independent movement-animation script specifically for strafing.

The intended structure remains approximately:

### Base Layer

Locomotion / posture:

* Standing
* Walking
* Sprinting
* Crouching
* Prone
* Jumping

### Upper-Body Layer / Rig

Weapon posing from REQ-037.

---

# 29. Updated Standing Locomotion Architecture

The ideal current standing movement structure becomes:

`Standing Idle`

↓

`Standing Locomotion 2D Blend Tree`

Containing:

* Walking Forward
* Walking Backward
* Walking Left Strafe
* Walking Right Strafe

↓

when sprinting:

`Sprint Locomotion Blend Tree`

Containing:

* Sprint Forward
* Sprinting Left Strafe
* Sprinting Right Strafe

This should replace earlier temporary lateral fallbacks.

---

# 30. Inspector / Debugging

While testing, developers should be able to inspect:

* `MoveX`
* `MoveY`
* `MoveSpeed`
* `IsMoving`
* `IsSprinting`
* Current Animator state

It should be straightforward to verify that:

Left movement produces negative `MoveX`.

Right movement produces positive `MoveX`.

Forward produces positive `MoveY`.

Backward produces negative `MoveY`.

---

# 31. Existing Gameplay Must Remain Intact

REQ-040 must not regress:

* Mouse/keyboard movement
* Gamepad movement
* Walking
* Sprint mechanics
* Crouching
* Prone
* Dolphin dive
* Jumping
* Aiming
* Shooting
* Weapon switching
* Grenades
* Multiplayer
* Damage
* Death
* Respawn
* Bullseye behavior
* REQ-036 animation system
* REQ-037 third-person weapon posing

Animation remains a visual representation of gameplay.

---

# 32. Acceptance Criteria

REQ-040 is complete when:

### Walking

* Forward walking uses `Walking Forward`.
* Backward walking uses `Walking Backward`.
* Left movement uses `Walking Left Strafe`.
* Right movement uses `Walking Right Strafe`.
* Diagonal walking blends appropriately.
* Character no longer visibly slides sideways while playing only a forward animation.

### Sprinting

* Forward sprinting uses `Sprint Forward`.
* Leftward sprinting uses or blends toward `Sprinting Left Strafe`.
* Rightward sprinting uses or blends toward `Sprinting Right Strafe`.
* Forward-diagonal sprinting blends naturally between forward and lateral sprint clips.
* Rapid left/right sprint changes remain responsive.

### State Integration

* Crouching still uses crouching animations.
* Prone still uses prone animations.
* Jumping still uses existing jump animations.
* Sprint strafing into jump uses `Sprint to Jump`.
* Weapon posing remains intact.
* Aiming while strafing works correctly.

### Technical

* Root motion remains disabled.
* Existing movement code remains authoritative.
* Remote clients see appropriate movement direction.
* Local-space movement direction is calculated correctly.
* Animation changes do not materially increase network traffic.
* Animation playback does not alter player gameplay speed.

---

# 33. Multiplayer Testing Checklist

With two players connected, have Player 2 observe Player 1 performing:

1. Standing idle.
2. Walk forward.
3. Walk backward.
4. Walk left.
5. Walk right.
6. Walk forward-left.
7. Walk forward-right.
8. Walk backward-left.
9. Walk backward-right.
10. Rapidly alternate left/right strafing.
11. Sprint forward.
12. Sprint forward-left.
13. Sprint forward-right.
14. Sprint predominantly left.
15. Sprint predominantly right.
16. Transition from walking left to sprinting left.
17. Transition from sprinting left to forward sprint.
18. Transition from sprinting right to forward sprint.
19. Aim while walking left.
20. Aim while walking right.
21. Aim while sprint-strafing where gameplay permits.
22. Shoot while strafing.
23. Crouch while strafing.
24. Jump while walking laterally.
25. Jump while sprint-strafing.
26. Switch weapons while strafing.

Repeat key cases with Player 1 observing Player 2.

---

# 34. Remaining Locomotion Animation Gaps

After REQ-040, the major known movement-animation gaps include:

* Sprint Backward
* Dedicated diagonal movement clips, if ever desired
* Falling / airborne loop
* Landing
* Prone Crawl Left
* Prone Crawl Right
* Dolphin Dive animation

These are not required for REQ-040.

The current Blend Tree architecture should allow them to be added incrementally as new animation assets become available.
