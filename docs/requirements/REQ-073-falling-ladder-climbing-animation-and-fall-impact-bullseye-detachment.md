````markdown
# REQ-073 — Falling Animation, Ladder Climbing Animation, and Fall-Impact Bullseye Detachment

## Summary

Integrate two new player animations:

1. **Falling**
2. **Ladder Climbing**

The Falling animation should only activate after the player has been continuously falling for at least:

```text
2.0 seconds
```

The Ladder Climbing animation should be driven by the player's actual movement along a ladder:

- moving upward → animation plays forward
- moving downward → animation plays in reverse
- not moving on ladder → animation pauses/holds appropriately
- leaving the ladder at the top → normal locomotion/idle resumes

Additionally, a sufficiently large fall should knock the player's Bullseye off their body.

If the player falls more than:

```text
20 feet
```

approximately:

```text
6.1 meters
```

vertically before landing, the landing impact should trigger the existing Bullseye-detachment behavior used by grenade-type mechanics.

The detached Bullseye should then return/reattach through the existing Bullseye return system.

Do not create a separate Bullseye lifecycle specifically for falling.

---

# 1. Goals

REQ-073 should:

- integrate the new Falling animation
- delay Falling animation until a meaningful fall occurs
- integrate the new Ladder Climbing animation with REQ-068 ladder functionality
- synchronize climbing animation speed/direction with actual ladder movement
- reverse the climbing animation when descending
- transition cleanly from climbing into normal movement
- detect significant fall distance
- detach the Bullseye after falls greater than 20 feet
- reuse existing Bullseye detachment and reattachment architecture
- work correctly in multiplayer

---

# 2. New Animation Assets

The two new animation clips have already been imported/prepared in Unity:

```text
Falling
Ladder Climbing
```

Cursor should inspect the existing Animator Controller and animation architecture before modifying it.

Use the existing third-person humanoid animation system rather than creating a parallel Animator.

---

# 3. Falling State Definition

The Falling animation should NOT immediately trigger whenever the player briefly leaves the ground.

Normal events such as:

- jumping
- stepping off a small ledge
- briefly becoming airborne on uneven terrain

should not immediately switch the character into the Falling animation.

The player must be continuously descending/falling for at least:

```text
2.0 seconds
```

before the Falling animation becomes active.

---

# 4. Fall Timer

Track continuous falling duration.

Conceptually:

```csharp
fallDuration
```

The timer should begin only when the player is:

```text
not grounded
AND
descending
AND
not climbing a ladder
```

Conceptually:

```text
isGrounded == false
verticalVelocity < 0
isClimbingLadder == false
```

The exact implementation should use the existing player movement architecture.

---

# 5. Falling Animation Trigger

Before:

```text
fallDuration < 2 seconds
```

continue using the appropriate existing airborne/jump behavior.

Once:

```text
fallDuration >= 2 seconds
```

transition into:

```text
Falling
```

The Falling animation should remain active until the player:

- lands
- grabs/enters a ladder
- otherwise enters another valid locomotion state

---

# 6. Reset Fall Timer

Reset the fall timer when appropriate.

Examples:

```text
player lands
player begins ladder climbing
player respawns
player is otherwise no longer in a valid falling state
```

Avoid carrying the previous fall timer into a later unrelated jump/fall.

---

# 7. Do Not Count Ladder Movement as Falling

Being on a ladder must override Falling logic.

While actively attached to/climbing a ladder:

```text
isFalling = false
```

even if gravity/vertical velocity values would otherwise suggest downward movement.

Descending a ladder is climbing behavior, not falling behavior.

---

# 8. Falling Animation Transitions

Transitions into and out of Falling should be clean.

Example:

```text
Jump
   ↓
Airborne
   ↓
2 seconds continuous falling
   ↓
Falling Animation
   ↓
Landing
   ↓
Idle / Walk / Run / Other Ground State
```

Avoid:

- animation flickering
- rapid state switching near the 2-second threshold
- remaining stuck in Falling after landing

---

# 9. Landing From Falling Animation

Upon landing:

```text
Falling
    ↓
Ground Contact
    ↓
Normal Animator Locomotion
```

The next animation should be determined by the player's actual movement.

Examples:

### Player lands and stops

```text
Falling
→ Idle
```

### Player lands while holding movement

```text
Falling
→ Walk / Run / Sprint
```

Do not force Idle before locomotion if the player is already moving.

---

# 10. Ladder Climbing Integration

Integrate the Ladder Climbing animation with the existing ladder system introduced in REQ-068.

When the player enters a valid ladder-climbing state:

```text
Normal Locomotion
        ↓
Ladder Climbing State
        ↓
Ladder Climbing Animation
```

The animation should represent actual movement along the ladder.

---

# 11. Upward Ladder Movement

When the player moves upward:

```text
vertical ladder movement > 0
```

play the Ladder Climbing animation forward.

Conceptually:

```text
animationSpeed > 0
```

The animation playback rate should broadly correspond to climbing speed.

If climbing speed increases or decreases later, the animation should be capable of scaling with it.

---

# 12. Downward Ladder Movement

When the player climbs downward:

```text
vertical ladder movement < 0
```

play the same Ladder Climbing animation in reverse.

Conceptually:

```text
animationSpeed < 0
```

The goal is to make hand/foot motion naturally reverse as the player descends.

Do not require a separate descending-ladder animation unless the imported clip proves unsuitable for reversal.

---

# 13. Stationary on Ladder

If the player is attached to the ladder but not moving:

```text
vertical ladder movement ≈ 0
```

the climb animation should not continue cycling as if the player is moving.

Prefer:

```text
animation playback speed = 0
```

or an equivalent solution that holds the current ladder pose.

The character should appear to remain gripping the ladder.

Do not revert to normal Idle while still attached to the ladder.

---

# 14. Ladder Animation Speed

Expose/tune the relationship between:

```text
Player Ladder Speed
```

and:

```text
Animation Playback Speed
```

The character's climbing motion should visually correspond to actual movement.

Avoid obvious:

```text
feet/hands moving rapidly while player barely moves
```

or:

```text
player moving rapidly while animation barely moves
```

Exact synchronization does not need to be physically perfect, but it should be visually convincing.

---

# 15. Ladder Animator Parameter

Use an architecture appropriate to the existing Animator Controller.

Conceptually, a useful parameter may be:

```text
ClimbSpeed
```

where:

```text
ClimbSpeed > 0
→ play forward

ClimbSpeed = 0
→ hold

ClimbSpeed < 0
→ play backward
```

Cursor should use the existing Animator architecture rather than forcing this exact parameter name if a better structure already exists.

---

# 16. Entering Ladder State

When a player successfully begins climbing a ladder:

- disable normal grounded locomotion animation
- disable Falling animation
- enter Ladder Climbing animation
- initialize the appropriate climbing pose/state

Transition should be reasonably fast.

Avoid long crossfades that make the character appear to continue running through the ladder.

---

# 17. Leaving Ladder at the Top

When the player reaches the top of the ladder and successfully transitions back onto a walkable surface:

```text
Ladder Climbing
        ↓
Grounded
        ↓
Normal Locomotion
```

The resulting animation should depend on player input/state.

Examples:

### No movement input

```text
Ladder Climbing
→ Idle
```

### Player continues moving forward

```text
Ladder Climbing
→ Walk / Run / Sprint
```

Do not remain stuck in the climbing pose after leaving the ladder.

---

# 18. Leaving Ladder at the Bottom

Likewise, when climbing downward and reaching the bottom:

```text
Ladder Climbing
→ Ground Locomotion
```

Resume the appropriate:

- Idle
- Walk
- Run
- Sprint

state.

---

# 19. Falling Off a Ladder

If the player actually detaches from the ladder before reaching a valid platform and begins falling:

```text
Ladder State Ends
       ↓
Airborne
       ↓
Fall Timer Begins
       ↓
2+ seconds
       ↓
Falling Animation
```

Fall distance tracking should also begin from the appropriate point where unsupported falling begins.

---

# 20. Fall Distance Tracking

In addition to the 2-second animation timer, separately track fall distance.

Do NOT use the 2-second timer to determine whether the Bullseye should detach.

These are two independent concepts:

```text
Fall Duration
→ controls Falling animation
```

```text
Fall Distance
→ controls Bullseye detachment
```

---

# 21. Fall Start Height

When a genuine unsupported fall begins, record the player's vertical position.

Conceptually:

```csharp
fallStartY
```

This should represent the player's elevation when they begin descending without ground/ladder support.

---

# 22. Fall Landing Height

When the player lands, determine their landing elevation.

Conceptually:

```csharp
landingY
```

Calculate vertical drop:

```text
fallDistance = fallStartY - landingY
```

Only downward displacement should count.

---

# 23. Bullseye Detachment Threshold

If:

```text
fallDistance > 20 feet
```

approximately:

```text
fallDistance > 6.1 meters
```

then landing should knock the Bullseye off.

Expose the actual value as a configurable gameplay parameter.

Example:

```text
Bullseye Fall Detach Distance = 6.1
```

Do not bury the threshold in multiple scripts.

---

# 24. Why Use Vertical Drop

Use actual vertical displacement rather than:

- total player path length
- time airborne
- horizontal movement
- player velocity alone

Example:

A player falls:

```text
25 feet downward
+
30 feet horizontally
```

This qualifies because:

```text
vertical drop = 25 feet
```

A player travels far horizontally but drops only:

```text
8 feet
```

should not trigger the 20-foot detachment rule.

---

# 25. Bullseye Detachment Timing

The Bullseye should detach when the large fall ends in a landing/impact.

Preferred flow:

```text
Player Falls >20 ft
       ↓
Player Hits Ground
       ↓
Landing Confirmed
       ↓
Bullseye Detachment Trigger
       ↓
Physical Bullseye Separates
```

Do not detach the Bullseye in midair merely because the player has crossed the 20-foot threshold.

The impact causes the detachment.

---

# 26. Reuse Existing Detachment System

Critical requirement:

Do NOT create a unique "fall Bullseye" object or separate reattachment lifecycle.

Use the same underlying Bullseye detachment functionality already used by systems such as:

- grenade detachment
- Magnetism Grenade
- other existing Bullseye knock-off mechanics

Conceptually:

```text
Grenade
     \
      → Existing Bullseye Detachment System
     /
Fall Impact
```

REQ-073 should add another valid cause of detachment.

---

# 27. Detachment Cause

Where supported by the existing architecture, provide a cause/reason for the detachment.

Conceptually:

```csharp
BullseyeDetachCause.FallImpact
```

Possible existing/future causes might include:

```text
Explosion
Magnetism
FallImpact
Other
```

This is useful for:

- telemetry
- debugging
- future combat feedback
- balancing

Do not build an entirely new event system if an equivalent classification system already exists.

---

# 28. Reattachment

After falling causes Bullseye detachment, use the existing return timing/behavior.

The Bullseye should:

```text
Detach
   ↓
Remain physically separated
   ↓
Existing return timer/behavior
   ↓
Return to player
   ↓
Reattach
   ↓
Decal becomes active again
```

REQ-073 should not alter normal reattachment timing unless required for compatibility.

---

# 29. Already-Detached Bullseye

If the Bullseye is already detached when the player lands after a >20-foot fall:

Do NOT:

- spawn another Bullseye
- duplicate the Bullseye
- restart the entire Bullseye lifecycle incorrectly

The landing should simply recognize that there is no attached Bullseye available to knock off.

Existing detached state should remain authoritative.

---

# 30. No Fall Damage Requirement

REQ-073 does NOT introduce traditional health/fall damage.

A >20-foot fall currently causes:

```text
Bullseye Detachment
```

not:

```text
Player Health Damage
```

Do not add player fall damage unless required by another existing system.

---

# 31. Threshold Behavior

Use a clear threshold.

Preferred:

```text
fallDistance > 20 feet
```

or its configured metric equivalent.

Avoid floating-point edge cases where extremely small differences cause inconsistent results.

A reasonable comparison tolerance is acceptable.

---

# 32. Short Falls

Falls below the threshold should not detach the Bullseye.

Examples:

```text
5 ft
10 ft
15 ft
19 ft
```

→ No fall-impact Bullseye detachment.

---

# 33. Exactly/Near 20 Feet

Test near the threshold.

Examples:

```text
19.5 ft
20 ft
20.5 ft
21 ft
```

Ensure behavior is predictable.

The configured threshold should be the single source of truth.

---

# 34. Fall Duration vs Fall Distance Example

These systems must remain independent.

Example A:

```text
Player slowly falls 10 feet over 2.5 seconds
```

Possible result:

```text
Falling Animation = YES
Bullseye Detachment = NO
```

because:

```text
duration > 2 sec
distance < 20 ft
```

Example B:

```text
Player falls 25 feet in 1.5 seconds
```

Possible result:

```text
Falling Animation = may not reach 2-sec threshold
Bullseye Detachment = YES upon landing
```

because:

```text
distance > 20 ft
```

This distinction is intentional.

---

# 35. Respawn Reset

On respawn, reset:

```text
fallDuration
fallStartY
isFalling
climbing state
any pending fall-impact state
```

Do not allow a previous life's fall to affect the respawned player.

---

# 36. Multiplayer Authority

Fall-impact Bullseye detachment must be authoritative in multiplayer.

Do not allow each client to independently decide that the networked Bullseye has detached.

Preferred architecture:

```text
Owning Player Movement / Authoritative Movement State
        ↓
Fall Distance Determined
        ↓
Landing Validated
        ↓
Authoritative Bullseye Detach Trigger
        ↓
Existing Networked Bullseye Detachment System
```

The exact authority model should follow the project's existing Netcode architecture.

---

# 37. Remote Animation Replication

Other players should see the correct animation state.

Examples:

```text
Player A climbs ladder upward
→ other clients see Player A climbing upward
```

```text
Player A climbs downward
→ other clients see reversed/downward climbing motion
```

```text
Player A falls for 2+ seconds
→ other clients see Falling animation
```

Do not unnecessarily synchronize raw animation frames if the existing Animator/network architecture can synchronize the relevant state/parameters.

---

# 38. Ladder Animation Networking

Ensure relevant information such as:

```text
isClimbing
climbDirection / climbSpeed
```

is available to remote character animation.

Remote players should not see:

```text
upward climb animation
```

while the player is actually moving downward.

---

# 39. Fall Detachment Networking

If Player A falls more than 20 feet:

All clients should ultimately agree that Player A's Bullseye:

```text
detached
```

The existing networked Bullseye system should continue to control:

- physical detached Bullseye
- disappearance of decal
- return
- reattachment

REQ-073 should only invoke that system through the correct authoritative path.

---

# 40. Interaction With REQ-072 Bullseye Camera

REQ-072 introduces a Bullseye-tracking HUD camera.

REQ-073 must remain compatible with that system.

If a large fall causes Bullseye detachment:

```text
Fall Impact
    ↓
Bullseye Detaches
    ↓
REQ-072 camera should naturally switch to tracking detached Bullseye
```

Do not create special camera logic inside REQ-073.

Simply ensure the normal Bullseye detachment state is triggered correctly so REQ-072 can respond through its existing design.

---

# 41. Interaction With Telemetry

Where the telemetry architecture supports Bullseye detach reasons, record fall-impact detachments.

Potential event/classification:

```text
bullseye_detach_cause = fall_impact
```

or equivalent existing naming.

This should make future statistics possible such as:

```text
Bullseyes Detached by Falling
```

Do not create duplicate telemetry events if the general Bullseye-detachment event already exists.

Instead, extend/classify the existing event.

---

# 42. Animation Root Motion

Do not allow either new animation to take control of actual networked player locomotion unless the existing controller architecture explicitly requires it.

The game controller should remain responsible for:

```text
fall movement
ladder movement
player position
```

Animations should visually represent those movements.

Avoid animation root motion causing:

- ladder drift
- unexpected vertical translation
- falling position changes
- network desynchronization

---

# 43. Falling Animation Root Motion

The Falling animation should be effectively in-place.

Gravity/player movement code controls actual descent.

The animation only provides the visual falling pose.

---

# 44. Ladder Animation Root Motion

Likewise, the climbing animation should not independently move the player upward/downward through root motion.

The REQ-068 ladder controller should control vertical position.

Animation playback should be driven from that movement.

---

# 45. Inspector / Configuration

Expose relevant tuning values.

Suggested fields:

```text
Falling Animation Delay = 2.0 seconds

Bullseye Fall Detach Distance = 6.1 meters

Climb Animation Speed Multiplier = 1.0

Ladder Animation Transition Duration

Falling Animation Transition Duration
```

Reuse existing settings/configuration architecture where appropriate.

---

# 46. Debugging

During development, provide useful optional debug values such as:

```text
Grounded: true/false
Falling: true/false
Fall Duration
Fall Start Height
Current Vertical Drop
Climbing: true/false
Climb Speed
Last Landing Fall Distance
Bullseye Detach Triggered: true/false
```

Optionally log:

```text
Large Fall Detected: 7.4m
Bullseye detached due to FallImpact
```

Debug output should not spam production builds.

---

# 47. Scope Boundaries

REQ-073 DOES include:

- Falling animation integration
- 2-second falling animation delay
- ladder climbing animation
- forward climbing animation
- reverse downward climbing animation
- stationary ladder pose behavior
- return to normal animation at ladder exit
- fall distance measurement
- >20 ft / ~6.1m Bullseye detachment
- reuse of existing Bullseye detachment/reattachment
- multiplayer animation/state support
- optional telemetry classification for fall detachment

REQ-073 DOES NOT require:

- traditional fall damage
- new player health mechanics
- new ladder models
- rebuilding REQ-068 ladder movement
- a new Bullseye return system
- a unique detached Bullseye prefab for falling
- new landing animations
- ragdoll behavior
- changing grenade detachment behavior
- redesigning the Animator Controller beyond what is necessary to integrate these states

---

# 48. Acceptance Criteria

REQ-073 is complete when:

- [ ] The new Falling animation is integrated.
- [ ] Brief jumps do not immediately trigger Falling.
- [ ] Falling animation begins only after approximately 2 seconds of continuous unsupported descent.
- [ ] Falling timer resets correctly after landing.
- [ ] Ladder movement does not incorrectly count as falling.
- [ ] Falling animation exits correctly when the player lands.
- [ ] Normal Idle/Walk/Run/Sprint resumes based on player movement after landing.
- [ ] The Ladder Climbing animation is integrated.
- [ ] Climbing upward plays the animation forward.
- [ ] Climbing downward plays the animation in reverse.
- [ ] Stationary ladder players do not continue cycling the climbing animation.
- [ ] Ladder animation visually corresponds reasonably to actual climb speed.
- [ ] Leaving a ladder at the top restores normal locomotion.
- [ ] Leaving a ladder at the bottom restores normal locomotion.
- [ ] Falling off a ladder correctly enters normal fall logic.
- [ ] Fall start elevation is tracked.
- [ ] Landing elevation is tracked.
- [ ] Vertical fall distance is calculated correctly.
- [ ] Falls shorter than 20 feet do not detach the Bullseye.
- [ ] Falls greater than 20 feet detach the Bullseye on landing.
- [ ] The fall threshold is configurable.
- [ ] Fall duration and fall distance are treated independently.
- [ ] Fall-impact detachment uses the existing Bullseye detachment system.
- [ ] The Bullseye returns using the existing reattachment system.
- [ ] No duplicate Bullseye is created if it is already detached.
- [ ] No traditional player fall damage is introduced.
- [ ] Fall-impact detachment works correctly in multiplayer.
- [ ] Other players see the correct Falling animation.
- [ ] Other players see correct upward/downward ladder animation.
- [ ] Animator root motion does not interfere with movement/networking.
- [ ] Respawning clears falling/climbing state correctly.
- [ ] Existing ladder, grenade, Bullseye, locomotion, and multiplayer systems remain functional.

---

# 49. Testing Checklist

## Short Jump

1. Jump normally.
2. Land before 2 seconds.
3. Confirm Falling animation does not trigger.
4. Confirm no Bullseye detachment occurs.

---

## Long Airborne Fall

1. Fall continuously for longer than 2 seconds.
2. Confirm Falling animation activates.
3. Land.
4. Confirm normal movement animation resumes.

---

## Fall Less Than 20 Feet

1. Fall approximately 15 feet.
2. Land.
3. Confirm Bullseye remains attached.

---

## Fall Greater Than 20 Feet

1. Fall approximately 25 feet.
2. Confirm Falling animation behavior.
3. Land.
4. Confirm Bullseye detaches on impact.
5. Confirm physical Bullseye behaves normally.
6. Confirm it returns and reattaches using the existing system.

---

## Fast >20-Foot Fall

Test a fall greater than 20 feet that takes less than 2 seconds if possible.

Confirm:

```text
Bullseye detachment still occurs
```

even if:

```text
Falling animation did not reach its 2-second trigger
```

---

## Slow Short Fall

Test a fall lasting longer than 2 seconds but dropping less than 20 feet if possible.

Confirm:

```text
Falling animation activates
```

but:

```text
Bullseye does not detach
```

---

## Upward Ladder

1. Enter ladder.
2. Move upward.
3. Confirm climbing animation plays forward.
4. Confirm playback visually corresponds to movement.

---

## Downward Ladder

1. Enter ladder.
2. Move downward.
3. Confirm climbing animation plays backward.
4. Confirm hands/feet visually descend rather than continuing upward motion.

---

## Stop Mid-Ladder

1. Begin climbing.
2. Stop movement.
3. Confirm animation stops/holds.
4. Confirm player remains in an appropriate climbing pose.

---

## Ladder Top

1. Climb to the top.
2. Exit onto platform while holding movement.
3. Confirm walking/running animation resumes.

Repeat without movement input.

Confirm Idle resumes instead.

---

## Ladder Bottom

1. Climb downward to ground.
2. Exit ladder.
3. Confirm normal locomotion resumes.

---

## Fall Off Ladder

1. Detach from ladder while above ground.
2. Begin falling.
3. Confirm falling timer starts.
4. Confirm Falling animation activates after 2 seconds if fall continues.
5. Confirm >20-foot drop can trigger Bullseye detachment.

---

## Bullseye Already Detached

1. Detach Bullseye through another mechanic.
2. Fall more than 20 feet.
3. Land.
4. Confirm no duplicate Bullseye appears.
5. Confirm existing detached Bullseye lifecycle continues correctly.

---

## Multiplayer

Test with at least two players.

Confirm:

- remote players see Falling animation
- remote players see upward climbing correctly
- remote players see downward climbing correctly
- fall-impact Bullseye detachment is synchronized
- detached Bullseye appears consistently across clients
- Bullseye reattachment remains synchronized

---

# 50. Desired Result

Player animation should now clearly communicate meaningful falling and ladder traversal.

A normal jump should remain visually lightweight.

A prolonged fall should become:

```text
Airborne
→ Falling Animation after 2 seconds
```

Ladder movement should visually correspond to actual movement:

```text
Up
→ Climb Forward

Down
→ Climb Reverse

Stopped
→ Hold Climbing Pose
```

And a dangerous fall should have a Bullseye-specific gameplay consequence:

```text
Fall >20 feet
        ↓
Impact Ground
        ↓
Bullseye Knocked Off
        ↓
Existing Detached Bullseye Behavior
        ↓
Existing Return / Reattachment
```

This should make falling and climbing feel more intentional while turning large falls into another meaningful interaction with Bullseye's core vulnerability mechanic.
````
