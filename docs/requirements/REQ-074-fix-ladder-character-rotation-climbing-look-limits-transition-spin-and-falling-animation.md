# REQ-074 — Fix Ladder Character Rotation, Climbing Look Limits, Transition Spin, and Falling Animation

## Summary

Follow up on REQ-073 to correct several problems with the newly implemented ladder and falling animation systems.

Current observed problems:

1. While climbing a ladder, the third-person/world-view character mesh does not remain vertically aligned with the ladder.
2. Looking left or right causes the player model to appear to rotate/orbit around an invisible circular path.
3. Camera look is being applied to the character/head in an unnatural way.
4. The player can apparently rotate/look too far behind themselves while attached to the ladder.
5. A very fast 360-degree character spin can occur while transitioning into the ladder climbing pose.
6. The Falling animation does not appear to trigger at all.

REQ-074 should diagnose and fix these issues without rebuilding the ladder movement system from scratch.

The core design rule is:

```text
While climbing a ladder:

Player/root position follows the ladder only.
Player body remains aligned to the ladder.
Camera may look around within limits.
Head/upper body may follow the camera within limits.
Camera look must NOT move the character around the ladder.
```

---

# 1. Inspect Existing REQ-073 Implementation First

Before adding new logic, inspect the implementation created for:

- ladder state
- ladder movement
- ladder-facing rotation
- camera yaw/pitch
- head look
- Animator parameters
- climb animation playback
- Falling animation state
- fall timer
- grounded detection

Identify which transforms currently receive:

```text
camera yaw
camera pitch
ladder facing rotation
character rotation
head rotation
```

The current symptoms strongly suggest that camera yaw or head-look logic may be affecting a parent/root transform rather than only the intended upper-body/head bones.

Do not add compensating rotations before identifying the current transform hierarchy and source of the unwanted movement.

---

# 2. Ladder Root Position Must Stay on the Ladder Axis

While climbing, the player's world/root position should only change according to intended ladder traversal.

For a simple vertical ladder:

```text
X = fixed relative to ladder
Z = fixed relative to ladder
Y = changes as player climbs
```

Looking left/right must NOT change the player's position.

The third-person mesh should not appear to orbit around an invisible pivot.

Incorrect:

```text
Camera turns left
    ↓
Character root rotates around ladder anchor
    ↓
Character moves in an arc
```

Correct:

```text
Camera turns left
    ↓
Character root remains in place
    ↓
Only allowed head/upper-body look changes
```

---

# 3. Separate Player Root From Visual Look Rotation

Do not use camera yaw to rotate the climbing player's root transform every frame.

While attached to a ladder, separate:

```text
Ladder Root Orientation
```

from:

```text
Camera Look Orientation
```

The ladder determines the player's body orientation.

The camera determines where the player is looking within a limited range.

Conceptually:

```text
Player Root
→ fixed facing relative to ladder

Camera
→ free look within configured limits

Head / upper torso
→ partially follows camera
```

---

# 4. Lock Body Facing to Ladder

When the player enters a ladder:

Determine the correct ladder-facing direction once.

The character should face toward the ladder surface.

Conceptually:

```text
characterForward = -ladderNormal
```

or whichever direction matches the existing ladder setup.

During climbing, preserve that orientation.

Do NOT continuously overwrite the body orientation using the camera's full yaw.

---

# 5. Do Not Rotate Around Ladder Center

Inspect whether the current implementation uses something similar to:

```csharp
transform.RotateAround(...)
```

or rotates a parent object whose pivot is offset from the character.

If so, replace that behavior.

Camera look should never use a transform operation that changes the player's world position.

Ensure:

```text
Look Rotation
```

changes rotation only.

It should not create translation.

---

# 6. Player Mesh Local Position

Inspect the hierarchy of the visual character model.

For example:

```text
PlayerRoot
    ├── CharacterController
    ├── CameraRoot
    └── VisualMesh
```

The visual mesh should maintain a stable local position relative to the player root during ladder climbing.

Camera look should not cause:

```text
VisualMesh.localPosition
```

to change.

If the mesh currently has an offset pivot or is being rotated around a parent with an offset, correct the hierarchy or rotation target.

---

# 7. Ladder Camera Look

The player should still be allowed to look around while climbing.

However, ladder look should be intentionally restricted.

The camera may move independently from the character's root orientation.

Suggested horizontal range:

```text
approximately ±60° to ±75°
```

from the player's ladder-facing direction.

Do not allow a player attached to a ladder to rotate the camera a full 180° behind themselves.

Expose the exact value in the Inspector.

Example:

```text
Ladder Max Look Yaw = 70°
```

---

# 8. Prevent Looking Directly Behind

Clamp camera yaw relative to the ladder-facing direction.

Conceptually:

```text
relativeYaw = cameraYaw - ladderFacingYaw
```

Clamp:

```text
-LadderMaxYaw
to
+LadderMaxYaw
```

Do not clamp raw world yaw values without properly handling angle wrapping.

---

# 9. Vertical Look Limits

Also restrict excessive vertical look while climbing.

The player should be able to:

- look somewhat upward
- look somewhat downward

but not twist into extreme orientations.

Suggested starting values:

```text
Ladder Look Up = 60°
Ladder Look Down = 50°
```

These should be configurable.

---

# 10. Head Look Should Follow Camera — Not Match It 1:1

The head should respond to camera look, but not copy the complete camera rotation directly.

Do NOT do something equivalent to:

```csharp
head.rotation = camera.rotation;
```

This will usually look unnatural because the camera has much more rotational freedom than a human neck.

Instead:

```text
Camera Look
    ↓
Calculate relative yaw/pitch
    ↓
Clamp values
    ↓
Apply fraction to head / upper body
```

---

# 11. Head Yaw Limit

Limit actual head rotation.

Suggested starting value:

```text
Head Yaw Limit = ±45°
```

If camera yaw exceeds the head range, the head should simply remain at its maximum allowed rotation.

Example:

```text
Camera = 70° left
Head = 45° left
```

This is expected.

---

# 12. Head Pitch Limit

Similarly clamp head up/down movement.

Suggested:

```text
Head Look Up = 35°
Head Look Down = 30°
```

Tune based on the actual character rig.

Avoid extreme neck bending.

---

# 13. Optional Upper-Chest Contribution

If head-only rotation looks unnatural, distribute some look motion through the upper spine/chest.

Example:

```text
Camera Yaw = 60°

Chest = 15°
Head = 35°
```

rather than forcing:

```text
Head = 60°
```

This is optional if the existing animation/IK system already provides a good solution.

If VeryAnimation or existing Animator rig constraints already support appropriate look behavior, reuse them.

Do not introduce unnecessary complexity.

---

# 14. Head Look Should Be Additive

The ladder climbing animation should remain the base animation.

Head look should layer on top.

Conceptually:

```text
Climbing Animation
+
Limited Head/Upper Body Look Offset
```

Do not rotate the whole animated skeleton to achieve camera look.

---

# 15. Smooth Head Look

Use damping/interpolation so the player's head does not instantly snap to camera movement.

Expose:

```text
Head Look Smooth Speed
```

The head should feel responsive but natural.

---

# 16. Remove 360-Degree Transition Spin

A very fast 360° spin currently occurs in some transitions into the climbing pose.

This must be fixed.

Likely causes to inspect include:

- Euler angle interpolation
- 0° / 360° wraparound
- incorrect target facing
- animation root rotation
- rotating toward equivalent angles using the long path
- multiple scripts fighting over player rotation
- ladder-entry code and Animator root rotation both controlling orientation

---

# 17. Use Shortest-Path Rotation

When aligning the player with the ladder, use shortest-path quaternion interpolation.

Prefer approaches equivalent to:

```csharp
Quaternion.Slerp(...)
```

or:

```csharp
Quaternion.RotateTowards(...)
```

rather than manually interpolating Euler angles.

Do not interpolate:

```text
359° → 1°
```

as though that requires:

```text
358° of rotation
```

It should rotate approximately:

```text
2°
```

---

# 18. Ladder Entry Rotation

When entering a ladder:

1. Determine intended ladder-facing rotation.
2. Smoothly rotate the character toward it.
3. Use the shortest rotational path.
4. Transition into climbing pose.
5. Do not allow camera yaw to redefine the target orientation during this transition.

The transition should feel like:

```text
Approach ladder
    ↓
Character turns toward ladder
    ↓
Hands/feet enter climbing pose
```

not:

```text
Approach ladder
    ↓
Character performs 360° spin
    ↓
Climbing pose
```

---

# 19. One System Owns Ladder Body Rotation

While climbing, exactly one gameplay system should own the player's body/root facing.

Inspect whether multiple systems currently control rotation, such as:

```text
PlayerMovement
CameraController
LadderController
Animator Root Motion
Aim Controller
```

Resolve conflicts.

Preferred ownership:

```text
LadderController
→ owns root/body orientation while climbing

CameraController
→ owns camera look within ladder constraints

Head/Look System
→ owns limited head/upper-body offsets
```

---

# 20. Disable Root Rotation From Climbing Animation

Inspect the Ladder Climbing animation's root-transform settings.

The clip should not rotate the player around the ladder.

If the imported clip contains root rotation:

Configure the animation so gameplay code remains responsible for root orientation.

The ladder animation should visually animate:

- arms
- legs
- torso
- climbing motion

without controlling world rotation.

---

# 21. Disable Root Translation From Climbing Animation

Similarly, verify the climbing animation is not introducing unintended:

```text
X
Z
```

root translation.

Vertical player movement must remain controlled by the ladder movement system.

The animation should not pull the player:

- forward
- backward
- sideways
- around a circular path

---

# 22. Animator Transition Into Ladder

Inspect the Animator transition from locomotion/airborne states into Ladder Climbing.

Ensure:

- transition duration is reasonable
- root rotation is not being blended incorrectly
- no animation clip contains an unexpected 360° root turn
- transition does not allow another locomotion state to continue controlling facing

If necessary, shorten the transition.

The animation should settle quickly into the ladder state.

---

# 23. Ladder Exit

When leaving the ladder:

Remove ladder-specific camera constraints.

Return normal:

```text
camera yaw
body facing
head look
```

behavior.

Do not leave:

- yaw clamps
- head clamps
- ladder body orientation
- paused animation speed

active after leaving the ladder.

---

# 24. Falling Animation Is Not Triggering

The Falling animation currently appears not to activate.

Debug and fix this.

Do not simply reduce the 2-second threshold unless testing proves the threshold itself is the issue.

First determine whether:

```text
fallDuration
```

is actually increasing during a genuine fall.

---

# 25. Falling State Debugging

Inspect:

```text
isGrounded
verticalVelocity
fallDuration
isClimbing
currentAnimatorState
```

during a long fall.

Add temporary debug output if necessary.

Example:

```text
Grounded: false
Vertical Velocity: -11.4
Fall Duration: 2.31
Climbing: false
Falling Animator Param: true
```

This should make it clear where the chain is failing.

---

# 26. Grounded Detection

Investigate whether the player is incorrectly being considered grounded while airborne.

A common failure would be:

```text
isGrounded
```

briefly flickering true because of:

- CharacterController ground checks
- capsule cast size
- slope proximity
- ladder colliders
- overlapping geometry

If `isGrounded` incorrectly resets every frame or repeatedly during the fall, the 2-second timer will never complete.

Fix the underlying grounded-state issue rather than adding arbitrary timer exceptions.

---

# 27. Fall Timer Conditions

The intended behavior remains:

```text
if:
    player is not grounded
    AND player is descending
    AND player is not climbing
then:
    increase fallDuration
```

Once:

```text
fallDuration >= 2.0
```

set the animator into the Falling state.

---

# 28. Jump Apex

Do not begin the falling timer while the player is still traveling upward from a normal jump.

Preferred:

```text
verticalVelocity >= 0
→ not yet counting as fall
```

After vertical velocity becomes negative:

```text
verticalVelocity < 0
→ begin / continue fall timer
```

---

# 29. Falling Animator Parameter

Inspect whether the Animator parameter actually matches what the controller is setting.

Potential issue:

```text
code sets IsFalling
```

while Animator expects:

```text
Falling
```

or equivalent.

Verify exact:

- parameter name
- parameter type
- transition condition
- state name

Do not assume the current Animator setup is connected correctly.

---

# 30. Falling State Transition

The Animator should have a valid transition into Falling that does not depend on unrelated conditions.

For example:

```text
Any State / Airborne
        ↓
IsLongFalling = true
        ↓
Falling
```

Use whichever transition architecture best fits the existing Animator.

Ensure transition conditions are actually reachable.

---

# 31. Falling Animation Playback

Once Falling activates, verify:

- Animator speed is normal
- the clip is assigned correctly
- the state is not immediately interrupted
- another state is not winning transition priority

The player should remain in Falling until:

```text
landing
OR
entering ladder
OR
another explicitly valid state
```

---

# 32. Falling Clip Root Motion

Confirm the Falling animation itself does not contain root translation or rotation that interferes with gameplay.

The controller/gravity system must continue controlling the actual fall.

---

# 33. Falling Animation Multiplayer

Once fixed, remote players must see the Falling animation after the same gameplay threshold is reached.

Do not solve the local animation while leaving remote Animator state unsynchronized.

Use the existing animation networking approach.

---

# 34. Preserve Existing 20-Foot Bullseye Detachment

Do not break the fall-distance gameplay mechanic added in REQ-073.

These remain independent:

```text
2 seconds falling
→ Falling animation
```

```text
>20 feet vertical drop
→ Bullseye detaches on landing
```

A fix to the Falling animation must not alter the fall-distance threshold.

---

# 35. Preserve Ladder Movement

Do not rewrite functional REQ-068 ladder traversal unnecessarily.

The player should still:

- move vertically
- climb upward
- climb downward
- stop mid-ladder
- exit at top
- exit at bottom

REQ-074 primarily corrects:

```text
visual rotation
camera/look behavior
Animator behavior
```

unless an underlying ladder movement bug is directly responsible for the visible problem.

---

# 36. Debug Visualization

Add temporary development-only debug tools if useful.

For ladder state, optionally show:

```text
Ladder Forward
Character Forward
Camera Forward
Relative Camera Yaw
Desired Body Rotation
Current Body Rotation
Head Yaw
Head Pitch
```

Debug rays may be useful:

```text
RED   = Ladder Normal
BLUE  = Character Forward
GREEN = Camera Forward
```

These should be disabled in production.

---

# 37. Inspector Settings

Expose useful tuning settings such as:

```text
Ladder Max Camera Yaw = 70°
Ladder Look Up Limit = 60°
Ladder Look Down Limit = 50°

Head Yaw Limit = 45°
Head Look Up Limit = 35°
Head Look Down Limit = 30°

Head Look Smooth Speed

Ladder Facing Rotation Speed

Falling Animation Delay = 2.0 sec
```

Avoid scattering these values through multiple scripts.

---

# 38. Expected Ladder Behavior

When climbing straight upward and looking forward:

```text
Player root:
stationary X/Z
moving upward Y

Body:
faces ladder

Head:
neutral
```

When looking left:

```text
Player root:
unchanged X/Z

Body:
still faces ladder

Camera:
looks left within clamp

Head:
turns left within head clamp
```

When looking far left:

```text
Camera:
stops at ladder camera limit

Head:
stops at smaller head limit
```

No part of this should cause the character to orbit around the ladder.

---

# 39. Expected Downward Behavior

When climbing downward:

```text
Body remains facing ladder
Root moves vertically downward
Climbing animation reverses
Camera retains restricted free-look
Head follows within limits
```

Looking around must not alter vertical movement direction.

---

# 40. Scope Boundaries

REQ-074 DOES include:

- fixing circular/orbiting ladder mesh behavior
- separating camera look from body/root movement
- locking character body orientation to ladder
- limiting ladder camera yaw
- limiting ladder camera pitch
- limiting head yaw
- limiting head pitch
- smoothing head look
- fixing 360° ladder-entry spin
- fixing ladder rotation interpolation
- inspecting animation root rotation
- fixing Falling animation activation
- debugging fall timer/ground detection
- preserving multiplayer animation behavior

REQ-074 DOES NOT require:

- creating new ladder animations
- creating a new Falling animation
- rebuilding REQ-068 from scratch
- changing ladder traversal speed unless needed for animation synchronization
- changing the 20-foot Bullseye detachment threshold
- redesigning the third-person animation system
- implementing full-body procedural IK
- introducing root-motion locomotion

---

# 41. Acceptance Criteria

REQ-074 is complete when:

- [ ] Looking left/right on a ladder no longer causes the player mesh to orbit around an invisible circle.
- [ ] Player X/Z position remains stable relative to the ladder while climbing.
- [ ] Body remains facing the ladder during climbing.
- [ ] Camera yaw does not rotate the entire character root.
- [ ] Camera can look left/right within a configurable restricted range.
- [ ] Player cannot look directly behind themselves while attached to a ladder.
- [ ] Vertical camera look is also restricted to reasonable values.
- [ ] Head follows horizontal camera movement within a smaller yaw range.
- [ ] Head follows vertical camera movement within a smaller pitch range.
- [ ] Head does not rotate unnaturally to match full camera rotation.
- [ ] Head movement is smooth.
- [ ] The ladder climbing animation remains the underlying base animation.
- [ ] No super-fast 360° spin occurs when entering ladder state.
- [ ] Ladder entry rotation uses the shortest rotational path.
- [ ] Only one system owns player body/root orientation during ladder climbing.
- [ ] Climbing animation root motion does not cause unwanted rotation.
- [ ] Climbing animation root motion does not cause unwanted horizontal translation.
- [ ] Upward climbing still works.
- [ ] Downward reversed climbing still works.
- [ ] Stopping on the ladder still holds an appropriate climbing pose.
- [ ] Exiting the ladder restores normal camera/body behavior.
- [ ] Falling animation successfully activates after 2 seconds of continuous descending fall.
- [ ] Short jumps do not trigger Falling.
- [ ] Falling animation remains active until landing or another valid state.
- [ ] Grounded detection does not incorrectly prevent long-fall detection.
- [ ] Falling animation works for remote players.
- [ ] >20-foot Bullseye fall detachment remains functional.
- [ ] Existing multiplayer, ladder, movement, Bullseye, and Animator systems remain functional.

---

# 42. Testing Checklist

## Ladder — No Camera Movement

1. Attach to ladder.
2. Climb upward without moving camera.
3. Confirm character travels vertically.
4. Confirm no horizontal orbiting occurs.
5. Confirm body continuously faces ladder.

---

## Ladder — Look Left/Right

1. Stop midway on ladder.
2. Slowly move camera left.
3. Slowly move camera right.
4. Confirm:
   - root remains fixed
   - mesh does not orbit
   - body remains facing ladder
   - head follows within its range

---

## Ladder — Maximum Look Angle

1. Attempt to look directly behind the character.
2. Confirm camera stops at configured yaw limit.
3. Confirm head stops before or at its smaller yaw limit.
4. Confirm character body does not rotate around to follow camera.

---

## Ladder — Look Up/Down

1. Look upward while climbing.
2. Look downward while climbing.
3. Confirm:
   - camera remains within vertical clamp
   - head moves naturally
   - neck does not bend excessively

---

## Ladder Entry

Test entry from several player headings:

```text
0°
45°
90°
135°
180°
```

relative to the ladder.

Confirm the character always chooses the shortest rotation toward the ladder.

No 360° spin should occur.

---

## Ladder Exit

1. Climb to top.
2. Exit ladder.
3. Rotate camera freely.
4. Confirm ladder camera constraints are removed.
5. Confirm normal body-turning behavior resumes.

---

## Climb Down

1. Descend ladder.
2. Look left/right.
3. Confirm:
   - climbing animation reverses
   - root moves only vertically
   - camera look does not affect root position

---

## Long Fall

1. Start from a height sufficient to remain descending for >2 seconds.
2. Observe debug values.
3. Confirm:

```text
isGrounded = false
verticalVelocity < 0
fallDuration increases
```

4. At 2 seconds, confirm Falling animation activates.

---

## Short Jump

1. Perform normal jump.
2. Confirm Falling animation does not trigger.
3. Confirm fall timer resets on landing.

---

## Falling Into Ladder

1. Begin falling.
2. Enter a ladder before the 2-second threshold.
3. Confirm fall timer resets/stops.
4. Confirm climbing animation takes over.

---

## Falling >20 Feet

1. Fall more than 20 feet.
2. Confirm Falling animation activates if duration exceeds 2 seconds.
3. Land.
4. Confirm existing Bullseye detachment still triggers.
5. Confirm Bullseye reattaches normally.

---

## Multiplayer

Test with at least two players.

Confirm remote player observes:

- stable ladder body position
- no orbiting
- no 360° ladder spin
- correct upward climbing
- correct reversed downward climbing
- sensible limited head movement
- Falling animation after long fall

---

# 43. Desired Result

Ladder traversal should now visually behave as though the character is physically attached to a ladder.

The important separation should be:

```text
Ladder
→ controls player position and body facing

Camera
→ controls restricted player view

Head / Upper Body
→ visually follows part of the camera movement

Animation
→ provides climbing motion
```

Looking around should never translate or orbit the character.

The player should be able to inspect somewhat to either side while climbing, but should not be able to rotate their head/camera directly behind themselves.

Entering the ladder should smoothly orient the character toward it without any 360° spin.

The Falling animation should also reliably activate after the existing 2-second continuous-fall threshold, while remaining independent from the >20-foot Bullseye-detachment mechanic.