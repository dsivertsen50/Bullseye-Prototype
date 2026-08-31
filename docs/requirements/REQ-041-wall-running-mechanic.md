# REQ-041 — Wall Running Mechanic

## Summary

Add a short-duration wall-running mechanic to the existing player movement controller.

A player should be able to temporarily run along a sufficiently vertical wall when they:

1. Are sprinting.
2. Jump toward the wall.
3. Make contact with a valid wall surface while airborne.

A successful wall run should allow the player to travel along the wall for a maximum of approximately **2.5 seconds** before gravity and normal airborne movement resume.

This requirement should focus on the **gameplay mechanic only**.

Dedicated wall-running animations are **not required** for REQ-041.

---

# 1. Primary Goal

Wall running should provide players with an additional movement option that rewards:

* Sprinting
* Momentum
* Jump timing
* Environmental awareness

The mechanic should feel fast and intentional without allowing players to remain attached to walls indefinitely.

The intended sequence is:

`Sprint`
→
`Jump`
→
`Contact Valid Wall`
→
`Wall Run`
→
`Exit Wall`
→
`Normal Airborne Movement`

---

# 2. Activation Requirements

A wall run may begin only when all of the following are true:

* Player is currently airborne.
* Player successfully jumped before contacting the wall.
* Player was sprinting when entering the wall interaction.
* Player has meaningful movement velocity.
* Player contacts a valid wall surface.
* Wall angle is within the allowed range.
* Player is approaching the wall rather than already standing against it.
* Player is not currently crouched or prone.

Merely walking into a wall should **not** activate wall running.

---

# 3. Valid Wall Angle

Only sufficiently vertical surfaces may be wall-run.

Valid surface angle:

**70°–90°**

Interpret this as the surface's slope relative to horizontal ground:

* `0°` = flat floor
* `90°` = perfectly vertical wall

Therefore:

* 60° slope → invalid
* 69° slope → invalid
* 70° slope → valid
* 80° slope → valid
* 90° wall → valid

Slightly imperfect or slanted wall geometry should therefore still support wall running.

---

# 4. Angle Detection

Determine the contacted surface angle using its surface normal.

Conceptually:

`surfaceAngle = angle between surface normal and Vector3.up`

The resulting angle should be between:

`70° <= surfaceAngle <= 90°`

or the practical equivalent required by the current movement architecture.

Do not rely solely on object tags such as `Wall`.

Geometry should determine whether the surface is wall-run capable.

An optional layer mask may still be used to exclude objects that should never support wall running.

---

# 5. Wall Detection

While the player is airborne following a sprint jump, detect nearby valid wall surfaces.

Use an appropriate physics query such as:

* Raycast
* SphereCast
* CapsuleCast

or another robust method compatible with the current controller.

Wall detection should occur primarily:

* To the player's left
* To the player's right

and potentially slightly forward to allow natural entry into the wall.

Avoid requiring pixel-perfect sideways contact.

---

# 6. Left and Right Walls

The system should identify whether the valid wall is:

* On the player's left
* On the player's right

Store this information while wall running.

Suggested conceptual state:

`WallSide.Left`

or

`WallSide.Right`

This will be useful later for:

* Animations
* Camera effects
* Wall-jump direction
* VFX
* Audio

---

# 7. Entering Wall Run

When all activation conditions are met:

1. Identify the wall normal.
2. Determine the direction along the wall.
3. Enter the wall-running state.
4. Preserve meaningful forward momentum.
5. Reduce or suppress normal falling behavior.
6. Begin the wall-run timer.

Do not completely stop the player's momentum when wall running begins.

The transition should feel like the player's existing sprint/jump momentum continues along the surface.

---

# 8. Wall-Run Direction

The player's movement along the wall should follow a direction tangent to the wall surface.

Conceptually:

* Wall normal points away from wall.
* Wall-run direction travels parallel to wall.
* Choose the tangent direction most closely aligned with the player's existing velocity/facing direction.

Do not allow entering a wall run to suddenly reverse the player's travel direction.

---

# 9. Maximum Duration

Maximum wall-running duration:

**2.5 seconds**

Expose this as an Inspector-configurable value.

Suggested field:

`maxWallRunDuration = 2.5f`

This value should be easy to tune during playtesting.

Do not hard-code it throughout movement logic.

---

# 10. Gravity During Wall Run

Normal gravity should be substantially reduced during a successful wall run.

The player should not immediately fall away from the wall.

However, the player should also not feel magnetically frozen at a fixed vertical position.

Preferred behavior:

* Strongly reduced downward gravity.
* Potential small amount of gradual vertical loss.
* Continued horizontal movement.

Expose relevant values for tuning.

Examples:

* Wall-run gravity multiplier
* Maximum downward wall-run velocity
* Wall adhesion strength

Exact numbers should be tuned during testing.

---

# 11. Wall Adhesion

During a wall run, apply a small force or positional tendency toward the wall so minor geometry variation does not immediately end the run.

This should be subtle.

The player should **not** feel glued to the wall.

The purpose is simply to maintain reliable contact while moving quickly across a surface.

Expose the wall adhesion strength for tuning if necessary.

---

# 12. Player Speed

Wall running should generally preserve the player's sprint momentum.

Do not drastically accelerate the player merely because they touched a wall.

Suggested behavior:

* Capture entry velocity.
* Preserve or modestly normalize wall-running speed.
* Move parallel to the wall.
* Prevent excessive acceleration.

The existing sprint speed should provide the baseline.

---

# 13. Minimum Wall-Run Speed

Wall running should require meaningful momentum.

If the player's speed becomes too low:

→ End wall run.

Expose a minimum wall-run speed if necessary.

This prevents situations where a player slowly sticks to a wall.

---

# 14. Maximum Wall-Run Time Reached

When the 2.5-second timer expires:

* Wall-running state ends.
* Wall adhesion stops.
* Reduced gravity stops.
* Normal airborne gravity resumes.
* Player begins falling naturally.

Do not abruptly teleport or push the player away unless required for collision stability.

---

# 15. Voluntary Wall Jump

While wall running, pressing Jump should allow the player to **jump away from the wall**.

This should immediately end the wall run.

Apply:

* Upward jump force.
* Outward force away from the wall.
* Some preservation of forward momentum.

Conceptually:

`wall jump velocity = upward force + wallNormal * outward force + retained forward momentum`

The exact strength should be exposed for tuning.

---

# 16. Wall Jump Direction

The wall normal provides the natural wall-jump direction.

If the wall is on the player's right:

→ Player jumps generally left/away from the wall.

If the wall is on the player's left:

→ Player jumps generally right/away from the wall.

Do not require the player to manually aim directly away from the wall.

Movement input may influence the final direction if practical.

---

# 17. Releasing Sprint

If the player releases Sprint while wall running:

→ End the wall run.

Wall running should remain fundamentally tied to sprint movement.

Normal airborne behavior resumes.

---

# 18. Movement Away From Wall

If the player intentionally moves strongly away from the wall:

→ End the wall run.

Do not force the player to remain attached when their input clearly indicates they want to leave.

---

# 19. Losing Wall Contact

If the wall is no longer detected:

→ End the wall run.

Examples:

* Player reaches end of wall.
* Wall curves away too sharply.
* Player passes through an opening.
* Player jumps away.
* Geometry no longer satisfies valid wall-angle requirements.

Allow a very short wall-contact grace period if necessary to prevent tiny collider seams from terminating the run.

Suggested tunable value:

`wallContactGraceTime`

Keep this small.

---

# 20. Reaching the Ground

If the player becomes grounded:

→ Immediately end wall-running state.

Normal grounded movement resumes.

---

# 21. Crouching / Prone

The player should not enter wall running while:

* Crouched
* Prone

If crouch/prone input is activated during a wall run, existing gameplay rules may either:

* End the wall run and permit the action where valid.

or

* Ignore the posture change until airborne behavior resumes.

Prefer whichever integrates most naturally with the existing movement controller.

Do not allow a player to remain crouched or prone while attached to a wall.

---

# 22. Dolphin Dive

The existing dolphin-dive mechanic must remain functional.

A dolphin dive should not trigger a wall run merely because the diving player collides with a valid wall.

Wall-run entry specifically requires a qualifying sprint **jump**.

---

# 23. Same-Wall Reattachment Protection

Players should not be able to circumvent the 2.5-second duration by repeatedly:

1. Ending the wall run.
2. Remaining next to the same wall.
3. Immediately reattaching.
4. Restarting the full timer.

Track the most recently used wall/surface.

After leaving a wall run, require one of the following before the same wall may be used again:

* Player touches the ground.

or

* Player meaningfully separates from the wall and contacts another valid wall.

A short cooldown may additionally be used if needed.

The exact implementation is flexible.

The important requirement is:

> A player cannot remain indefinitely on the same wall by repeatedly restarting the wall run.

---

# 24. Switching Walls

Players should be allowed to transition between different walls where level geometry supports it.

Example:

Player wall-runs along Wall A.

Player jumps away.

Player reaches Wall B.

If activation requirements are satisfied:

→ Wall B may begin a new wall run.

This could support advanced movement through corridors and corners.

Do not require this behavior to be highly polished in REQ-041, but the architecture should not prohibit it.

---

# 25. Wall Corners

Do not attempt complex automatic corner traversal for REQ-041.

If the player reaches a sharp corner:

* Current wall contact may end.
* Player returns to normal airborne movement.
* A new valid wall may independently trigger another wall run if conditions are met.

Automatic wrapping around 90° corners can be considered later.

---

# 26. Camera Behavior

Do **not** require significant camera effects for this first implementation.

The FPS camera should continue functioning normally.

A subtle future camera tilt may eventually be desirable, but it is not required for REQ-041.

If any camera roll is added as part of experimentation:

* Keep it subtle.
* Make it configurable.
* Smoothly restore camera orientation when wall run ends.

Gameplay functionality takes priority over camera polish.

---

# 27. Third-Person Animations

Dedicated wall-running animations are **not required**.

The current difficulty of combining Mixamo locomotion with the third-person weapon-holding system should not block implementation of this mechanic.

For REQ-041:

* Existing animation may continue as the closest available fallback.
* Do not create complicated procedural arm/weapon adjustments specifically for wall running.
* Do not regress REQ-037 weapon attachment/IK behavior.

A dedicated animation ticket may be created later.

---

# 28. Third-Person Character Orientation

Although dedicated wall-running animation is not required, remote players should still see a physically coherent player orientation.

Do not rotate the full character model 90° sideways against the wall unless intentionally implemented and tested.

For now, the character may remain broadly upright while traveling along the wall.

Movement correctness is more important than a stylized wall-running body lean.

---

# 29. Weapon Compatibility

Wall running must remain compatible with:

* Pistol
* AK
* Shotgun
* Weapon switching
* Third-person weapon attachment

The weapon should remain attached to the character.

REQ-041 does not need special weapon poses for wall running.

---

# 30. Aiming

Decide wall-run ADS behavior based on the existing movement system.

Preferred initial behavior:

**Disable ADS/precision aiming during active wall running.**

The player may still look around and potentially fire if existing combat rules permit it, but the normal slowed ADS state should not interfere with wall-run movement.

If ADS is currently active when wall running begins:

→ Exit ADS.

This can be changed later based on playtesting.

---

# 31. Shooting

Wall running should not inherently prevent shooting unless technical conflicts make this necessary.

For the initial implementation:

* Allow hip-fire while wall running.
* Do not require special firing animations.
* Maintain normal weapon damage/ammo behavior.

This may be revised for balance later.

---

# 32. Grenades

Grenade functionality should remain operational.

REQ-041 should not change existing grenade physics, bullseye behavior, or grenade controls.

Whether grenade throwing while wall-running remains allowed can follow current general airborne behavior.

---

# 33. Bullseye Compatibility

The existing bullseye system must remain functional during wall running.

REQ-041 should not modify:

* Bullseye crawl logic
* Bullseye damage
* Bullseye shattering
* Grenade dislodgement
* Bullseye HUD indicators

The future animated-mesh bullseye redesign is outside the scope of this requirement.

---

# 34. Multiplayer

Wall running must function correctly in multiplayer.

Remote players should see:

* Player moving along wall.
* Player remaining temporarily suspended/reduced-gravity.
* Wall jump if performed.
* Player leaving wall and falling normally.

Movement should remain authoritative according to the existing network architecture.

Do not network unnecessary visual animation information for REQ-041.

---

# 35. Network Authority

Only the owning player's authoritative movement/controller logic should determine whether wall running begins or ends.

Remote players should receive the resulting movement through the existing network movement system.

Do not allow remote clients to independently apply forces to another player's character.

---

# 36. Wall-Run State

Add a clear movement state or equivalent flag.

Suggested:

`IsWallRunning`

Potential associated runtime data:

* Current wall collider
* Current wall normal
* Current wall side
* Current wall-run direction
* Wall-run elapsed time
* Last wall used

Avoid implementing wall running as scattered special cases throughout jump/gravity code.

---

# 37. Recommended Movement Flow

Conceptually:

### Grounded Sprint

`Grounded + Sprint`

↓

### Jump

`Airborne + SprintJumpEligible`

↓

### Valid Wall Detected

Check:

* Wall angle
* Velocity
* Wall direction
* Sprint state
* Same-wall restriction

↓

### Enter Wall Run

Set:

`IsWallRunning = true`

↓

Apply:

* Parallel wall velocity
* Reduced gravity
* Mild wall adhesion

↓

Exit when:

* 2.5 seconds elapsed
* Jump pressed
* Sprint released
* Wall contact lost
* Player moves away
* Player becomes grounded
* Speed becomes too low

↓

### Normal Airborne State

Restore normal gravity and movement.

---

# 38. Inspector Configuration

Expose important tuning values in one logical Wall Run section.

Suggested values:

* `Enable Wall Running`
* `Minimum Wall Angle = 70`
* `Maximum Wall Angle = 90`
* `Maximum Wall Run Duration = 2.5`
* `Minimum Entry Speed`
* `Minimum Wall Run Speed`
* `Wall Detection Distance`
* `Wall Adhesion Strength`
* `Wall Gravity Multiplier`
* `Wall Jump Up Force`
* `Wall Jump Outward Force`
* `Wall Contact Grace Time`
* `Same Wall Reattach Cooldown` if used
* `Wall Layer Mask`

Do not require code changes for normal gameplay tuning.

---

# 39. Debugging

Provide useful debugging information where practical.

When development/debug mode is enabled, developers should be able to inspect:

* IsWallRunning
* Detected wall
* Wall angle
* Wall normal
* Wall side
* Wall-run elapsed time
* Whether entry requirements are satisfied

Optional Debug.DrawRay lines may show:

* Wall detection direction
* Wall normal
* Wall-run direction

Do not display this information in the normal production HUD.

---

# 40. Existing Gameplay Must Remain Intact

REQ-041 must not regress:

* Walking
* Sprinting
* Strafing
* Jumping
* Crouching
* Prone
* Dolphin dive
* First-person camera
* Aiming
* Shooting
* Weapon switching
* Grenades
* Player animations
* Third-person weapon system
* Multiplayer
* Damage
* Death
* Respawn
* Bullseye functionality

Wall running should extend the existing movement controller rather than replacing it.

---

# 41. Acceptance Criteria

REQ-041 is complete when:

### Activation

* Walking into a wall does not trigger wall running.
* Sprinting into a wall without jumping does not trigger wall running.
* Jumping into a wall without sprinting does not trigger wall running.
* Sprint-jumping into a valid wall can trigger wall running.
* Crouched/prone players cannot initiate wall running.

### Surface Detection

* Surfaces below 70° do not trigger wall running.
* A 70° surface may trigger wall running.
* Vertical 90° walls trigger wall running.
* Slightly imperfect wall geometry remains usable.
* Invalid layers/surfaces can be excluded.

### Wall Running

* Player travels primarily parallel to the wall.
* Player retains meaningful sprint momentum.
* Gravity is reduced while wall running.
* Player does not appear completely magnetized to the wall.
* Player stays on the wall reliably enough for normal gameplay.
* Wall running lasts no longer than approximately 2.5 seconds.

### Exit

* Timer expiration ends the wall run.
* Losing wall contact ends the wall run.
* Releasing sprint ends the wall run.
* Reaching the ground ends the wall run.
* Moving away from the wall can end the wall run.

### Wall Jump

* Pressing Jump during a wall run launches the player away from the wall.
* Wall jump contains upward force.
* Wall jump contains outward force.
* Some forward momentum is preserved.

### Exploit Prevention

* Player cannot repeatedly reset the 2.5-second timer against the same wall indefinitely.
* Landing resets same-wall restrictions.
* A sufficiently separate second wall may support another wall run.

### Multiplayer

* Owning player experiences correct wall-run movement.
* Remote player sees the wall runner moving correctly.
* Wall jumps replicate correctly.
* Ending a wall run replicates correctly.
* No major network jitter is introduced.

### Compatibility

* Weapons remain attached.
* Shooting functionality remains intact.
* Existing locomotion continues working.
* Existing animation system does not break.
* Bullseye functionality remains intact.
* Death/respawn correctly clears wall-running state.

---

# 42. Testing Checklist

Test at least the following:

1. Walk into vertical wall.
2. Sprint into vertical wall without jumping.
3. Jump into wall without sprinting.
4. Sprint-jump into vertical wall.
5. Sprint-jump into 80° wall.
6. Attempt wall run against 60° wall.
7. Attempt wall run against 69° wall.
8. Attempt wall run against 70° wall.
9. Run on wall for full 2.5 seconds.
10. Jump away before timer expires.
11. Release sprint during wall run.
12. Move away from wall.
13. Reach end of wall.
14. Cross a small collider seam.
15. Attempt to repeatedly reattach to same wall.
16. Land and retry same wall.
17. Wall-jump from left-side wall.
18. Wall-jump from right-side wall.
19. Transition from Wall A to Wall B.
20. Shoot while wall-running.
21. Wall-run with pistol.
22. Wall-run with AK.
23. Wall-run with shotgun.
24. Attempt ADS before entering wall run.
25. Attempt crouch/prone interaction during wall run.
26. Die while wall-running.
27. Respawn and verify movement state is reset.
28. Test all major cases with a second multiplayer player observing.

---

# 43. Future Improvements Not Required for REQ-041

Potential future wall-running enhancements include:

* Dedicated left/right wall-run animations
* Weapon-aware wall-run animation poses
* Camera roll
* Wall-run sound effects
* Footstep/contact effects
* Wall-run particles
* Dedicated wall-jump animations
* Momentum-based wall-run duration
* Curved wall traversal
* Automatic corner traversal
* Horizontal/vertical wall-running variations
* Special level-design wall-run materials
* Stamina limitations
* Combat balance restrictions

These are intentionally outside the scope of REQ-041.

The goal of this requirement is to first establish a reliable, responsive, multiplayer-compatible **sprint-jump-to-wall-run mechanic** that can later receive visual and audio polish.
