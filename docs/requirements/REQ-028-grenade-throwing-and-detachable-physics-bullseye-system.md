# REQ-028 — Grenade Throwing and Detachable Physics Bullseye System

## Summary

Add a throwable grenade mechanic that interacts directly with the Bullseye vulnerability system.

The grenade should:

* be throwable by the player,
* use Rigidbody physics,
* follow a physical throwing arc,
* explode after a short configurable fuse,
* apply radial physical force to nearby players,
* apply radial physical force to nearby detachable Bullseyes,
* deal **no conventional health damage**.

The primary gameplay purpose of the grenade is to temporarily knock an opponent's Bullseye off their body.

When a Bullseye is detached:

* it should become a physical object,
* behave like a relatively heavy ball,
* bounce/roll naturally,
* remain linked to its owning player,
* remain vulnerable to gunfire,
* and return to its owner's body automatically after approximately **5–7 seconds** if it has not been destroyed.

If a detached Bullseye is destroyed through normal weapon fire, its owning player should be killed according to the Bullseye vulnerability rules.

This system must function correctly in multiplayer.

---

# Core Gameplay Intent

The grenade is primarily a **displacement and vulnerability tool**, not a conventional explosive weapon.

Typical interaction:

```text
Player A throws grenade
        ↓
Grenade lands near Player B
        ↓
Explosion occurs
        ↓
Player B is physically pushed away
        +
Player B's Bullseye is knocked off
        ↓
Bullseye falls / rolls across ground
        ↓
Player B is temporarily exposed
```

Other players may then attempt to shoot the detached Bullseye.

If nobody destroys it:

```text
5–7 seconds
     ↓
Bullseye automatically returns
     ↓
Reattaches to Player B
     ↓
Normal Bullseye behavior resumes
```

This should create a temporary combat opportunity without making grenades direct damage weapons.

---

# Implementation Strategy

Because this feature affects:

* input,
* physics,
* multiplayer synchronization,
* Bullseye movement,
* player vulnerability,
* damage,
* respawning,

implement REQ-028 in distinct phases.

Recommended sequence:

1. Grenade input and spawning
2. Grenade throwing physics
3. Explosion and radial force
4. Bullseye detach state
5. Detached Bullseye Rigidbody behavior
6. Detached Bullseye damage handling
7. Automatic Bullseye return
8. Multiplayer synchronization
9. Polish and tuning

Each phase should be tested before proceeding where practical.

---

# Phase 1 — Grenade Input

## 1. Keyboard and Mouse Input

Map grenade throwing to:

```text
C
```

Pressing `C` should initiate a grenade throw.

---

# 2. Gamepad Input

Map grenade throwing to:

```text
Left Trigger
```

Use the project's existing Unity Input System architecture.

Do not implement direct device polling if actions/input maps already exist.

---

# 3. Input Conflict Check

Before assigning Left Trigger, inspect the current input configuration.

If Left Trigger is already assigned to another active gameplay function, do not silently overwrite that behavior.

Instead:

* preserve the existing binding architecture,
* update the appropriate Input Action,
* and clearly document any discovered conflict.

The intended final grenade binding for this requirement is:

```text
Keyboard:
C

Gamepad:
Left Trigger
```

---

# Phase 2 — Grenade Prefab

## 4. Create Grenade Prefab Architecture

Create a grenade prefab in an appropriate location such as:

```text
Assets/
    Weapons/
        Grenades/
            Prefabs/
```

or another location consistent with the existing project organization.

Suggested name:

```text
Grenade
```

or:

```text
GrenadePrefab
```

---

# 5. Temporary Visual

If no final grenade model currently exists, use a simple temporary mesh.

Examples:

* sphere,
* capsule,
* primitive grenade-like placeholder.

The visual asset must be easy to replace later without rewriting the grenade logic.

---

# 6. Required Grenade Components

The grenade prefab should include at minimum:

```text
Transform
Collider
Rigidbody
Grenade behavior component
Network components required by existing multiplayer architecture
```

The grenade must behave as an actual physical object.

---

# 7. Rigidbody Behavior

Configure the grenade so that it:

* responds to gravity,
* collides with the environment,
* can bounce slightly,
* can roll,
* loses momentum naturally.

Avoid making the grenade:

* excessively bouncy,
* extremely light,
* or prone to flying unrealistically after minor collisions.

---

# Phase 3 — Grenade Throw

## 8. Throw Origin

Spawn or release the grenade from a point near the local first-person weapon/camera.

Do not spawn it directly inside:

* the player's collider,
* the weapon collider,
* or another location likely to cause immediate collision with the thrower.

Prefer a configurable:

```text
GrenadeThrowOrigin
```

Transform.

---

# 9. Throw Direction

The grenade should initially travel approximately toward the player's aiming direction.

Use the camera/aim direction as the primary reference.

Conceptually:

```text
Camera Forward
      +
Slight Upward Component
      =
Throw Direction
```

This should produce a natural arc.

---

# 10. Throw Force

Expose configurable values such as:

```text
Throw Force
Upward Throw Bias
```

The initial prototype should allow a useful medium-distance grenade throw.

Exact strength should be tuned through playtesting.

---

# 11. Grenade Throw Animation

A basic grenade throw animation or first-person weapon movement should occur if an appropriate existing animation is available.

However, a custom high-quality throw animation is not required to complete the core functionality.

Do not delay the physics implementation solely because no appropriate grenade animation exists.

---

# 12. Grenade Availability

For the initial prototype, give each player a configurable grenade count per life.

Recommended default:

```text
1 grenade per life
```

Expose the value so it may later be changed.

For example:

```text
StartingGrenades = 1
```

After throwing the available grenade, additional grenade input should do nothing until:

* the player respawns,
* or another future grenade replenishment system is implemented.

Grenade pickups are out of scope for this requirement.

---

# Phase 4 — Fuse and Explosion

## 13. Grenade Fuse

The grenade should explode after a configurable delay.

Recommended initial value:

```text
2.5–3.0 seconds
```

Exact timing should be exposed in the Inspector.

Example:

```text
FuseDuration = 2.75
```

The fuse begins when the grenade is thrown.

---

# 14. Explosion Radius

The grenade should have a configurable radial effect.

Example configuration:

```text
ExplosionRadius
ExplosionForce
UpwardModifier
```

The exact values should be tuned after testing.

---

# 15. Explosion Does No Direct Damage

The grenade must **not directly subtract health**.

Specifically:

```text
Grenade Explosion Damage = 0
```

Do not send normal weapon damage events from the grenade explosion.

Its gameplay effects should come from:

* player displacement,
* Bullseye detachment,
* Bullseye displacement.

---

# 16. Explosion Visual

At explosion time, support a replaceable explosion effect.

For the prototype this may be:

* a simple particle system,
* an existing effect,
* or a temporary placeholder.

Provide an Inspector slot for an explosion VFX prefab.

Example:

```text
ExplosionVFX
```

---

# 17. Explosion Audio

Provide an Inspector slot for an explosion audio clip.

Example:

```text
ExplosionSFX
```

Do not require the sound file to exist for the gameplay system to function.

It should be easy for a developer to drag a sound asset into the field later.

---

# Phase 5 — Player Knockback

## 18. Radial Player Force

Players inside the grenade's effective radius should be pushed away from the explosion.

Direction should be based on:

```text
Explosion Position
        →
Player Position
```

with an optional upward force component.

---

# 19. Knockback Scaling

Players closer to the grenade should experience stronger displacement.

Players near the edge of the radius should experience less.

Conceptually:

```text
Close:
Strong Push

Medium:
Moderate Push

Edge:
Small Push
```

Prefer smooth distance-based falloff.

---

# 20. Player Movement Compatibility

The existing player movement system may use:

* CharacterController,
* Rigidbody,
* custom movement,
* or Cowsins-derived movement behavior.

Do not assume `Rigidbody.AddExplosionForce()` can be directly used on the player.

Inspect the current player controller and apply knockback using an approach compatible with it.

The result should visually and mechanically feel like the explosion pushes the player.

---

# 21. Knockback Must Not Break Player Control

After grenade knockback:

* the player must regain normal movement,
* sprinting must still work,
* jumping must still work,
* camera controls must still work.

Avoid permanent velocity or movement states.

---

# Phase 6 — Bullseye Detachment State

## 22. Bullseye States

Extend the Bullseye system so that a Bullseye can exist in at least two major states:

```text
Attached
```

and:

```text
Detached
```

Conceptually:

```text
BullseyeState.Attached

BullseyeState.Detached
```

Exact architecture may differ.

---

# 23. Normal Attached State

When attached, preserve existing Bullseye behavior.

This includes:

* randomized movement across the player surface,
* movement caused by turning,
* movement caused by other player actions,
* current network synchronization,
* existing hit detection.

REQ-028 must not permanently replace or disable the current Bullseye movement system.

---

# 24. Detachment Trigger

If a player is sufficiently affected by a grenade explosion, their Bullseye should detach.

Bullseye detachment should use a configurable explosion threshold.

For the initial prototype, a Bullseye within the main effective explosion radius may detach.

Potential future tuning may distinguish between:

```text
Outer radius:
push only

Inner radius:
push + detach Bullseye
```

The architecture should allow this distinction if practical.

---

# 25. Bullseye Leaves Player Surface

Upon detachment:

1. stop normal surface-following Bullseye movement,
2. detach the Bullseye from the player's body hierarchy if currently parented,
3. preserve its world position,
4. enable physics,
5. apply explosion force.

The Bullseye should visibly fly off the player.

---

# Phase 7 — Detached Bullseye Physics

## 26. Rigidbody Requirement

A detached Bullseye must behave using Rigidbody physics.

The Bullseye should act approximately like:

```text
a fairly heavy physical ball
```

rather than:

```text
a lightweight ping-pong ball
```

---

# 27. Rigidbody Activation

When attached:

* Rigidbody physics may be disabled,
* Rigidbody may be kinematic,
* or equivalent behavior may be used.

When detached:

```text
Rigidbody physics = active
Gravity = active
Collisions = active
```

Use whichever configuration works best with the existing Bullseye architecture.

---

# 28. Bullseye Physical Behavior

While detached, the Bullseye should:

* fall,
* bounce moderately,
* roll,
* collide with level geometry,
* respond to grenade explosion force.

It should settle relatively quickly rather than bouncing continuously.

---

# 29. Bullseye Weight

Expose configurable Rigidbody settings.

Recommended emphasis:

```text
moderate/high mass
low/moderate bounce
noticeable drag
```

Exact values should be tuned visually.

---

# 30. Bullseye Must Stay Linked to Owner

Detaching the Bullseye must **not sever its gameplay relationship with its player**.

Every Bullseye must retain a reference to:

```text
Owning Player
```

even while physically detached.

Conceptually:

```text
Detached Bullseye B
        ↓
still belongs to
        ↓
Player B
```

This relationship is critical for damage and death processing.

---

# Phase 8 — Detached Bullseye Vulnerability

## 31. Bullseye Remains Shootable

While on the ground, a detached Bullseye remains a valid hitscan target.

Existing weapons should be able to strike it.

REQ-025 spread and REQ-026 weapon damage should continue to apply.

---

# 32. Damage Transfers to Owner

Damage inflicted upon a detached Bullseye should be applied to its owning player according to the project's Bullseye damage rules.

Conceptually:

```text
Player A shoots
       ↓
Detached Bullseye belonging to Player B
       ↓
Bullseye registers hit
       ↓
Damage applied to Player B
```

---

# 33. Detached Bullseye Is Extremely Dangerous

A detached Bullseye represents a major vulnerability.

For the prototype, detached Bullseyes should preferably be treated as a highly vulnerable target.

The existing Bullseye lethality logic should be used where possible.

Do not create a second unrelated health pool for the Bullseye unless necessary.

---

# 34. Bullseye Destruction

If the Bullseye receives enough valid weapon damage to trigger its destruction condition:

```text
Bullseye destroyed
        ↓
Owning player dies
```

This should use the existing player death and respawn system.

Do not create a separate death implementation.

---

# 35. Destroyed Bullseye Behavior

Once destruction has killed the owning player:

* stop the detached return timer,
* prevent the Bullseye from reattaching,
* clean up the detached object/state,
* allow normal player respawn logic to recreate/reset the Bullseye.

---

# 36. Owner Cannot Escape Vulnerability by Moving Away

The detached Bullseye remains linked to the owner regardless of how far the player moves during the detachment period.

Example:

```text
Player runs 15 meters away
         ↓
Bullseye still belongs to that player
         ↓
Opponent shoots Bullseye
         ↓
Owner receives damage / dies
```

---

# Phase 9 — Automatic Bullseye Return

## 37. Return Timer

When a Bullseye becomes detached, start a configurable return timer.

Desired range:

```text
5–7 seconds
```

Recommended starting value:

```text
6 seconds
```

Expose this value:

```text
DetachedReturnDelay
```

---

# 38. Return Conditions

The Bullseye should return if:

* its owner is still alive,
* its owner has not respawned,
* the Bullseye has not been destroyed,
* the return timer expires.

---

# 39. Return Behavior

When the timer expires:

1. disable detached physics,
2. stop Bullseye velocity,
3. return the Bullseye to the owning player's body,
4. restore normal Bullseye attachment,
5. resume standard movement behavior.

---

# 40. Return Animation / Transition

Do not require the Bullseye to physically travel all the way back through world geometry.

For the initial prototype, prefer a reliable transition.

Acceptable approaches include:

```text
Quick interpolation toward owner
```

or:

```text
brief visual recall animation
```

followed by reattachment.

Avoid an obvious single-frame teleport if a simple interpolation can be implemented safely.

---

# 41. Return Speed

If interpolation is used, expose:

```text
BullseyeReturnSpeed
```

The return should be quick.

The Bullseye should not spend another several seconds slowly flying across the map.

Suggested visual return time:

```text
~0.25–0.75 seconds
```

---

# 42. Physics During Return

When recall begins:

* disable collision interactions that could prevent return,
* disable gravity,
* stop existing velocity,
* prevent other explosions from knocking it away during the recall transition.

Once reattached, restore the normal attached-state configuration.

---

# 43. Reattachment Position

The returning Bullseye should reattach to a valid location on the owner's body.

It may:

* return to its previous surface position,
* return to a default attachment location,
* or immediately request a valid new location from the existing Bullseye movement system.

Prefer the implementation that integrates most cleanly with the current randomized Bullseye system.

---

# 44. Resume Normal Bullseye Movement

After reattachment:

* random movement should resume,
* turning-based movement should resume,
* other existing Bullseye manipulation behavior should resume.

The Bullseye must not remain frozen.

---

# Phase 10 — Interaction With Additional Grenades

## 45. Grenade Hits Already Detached Bullseye

A grenade explosion may affect a Bullseye that is already detached.

In that case:

```text
Do NOT restart attachment logic
Do NOT create another Bullseye
```

Instead:

* apply additional physical force,
* keep the existing owner relationship,
* preserve the existing return timer unless intentionally configured otherwise.

For the initial prototype, **do not reset the return timer** when an already-detached Bullseye is hit by another grenade.

This prevents players from indefinitely keeping a Bullseye detached through grenade spam.

---

# 46. Grenade Chain Interaction

Grenades may physically influence other grenades if this naturally occurs through Rigidbody collisions.

However, elaborate explosive chain reactions are not required.

---

# Phase 11 — Interaction With Death and Respawn

## 47. Player Dies While Bullseye Detached

If a player dies for any reason while their Bullseye is detached:

* cancel the return process,
* clean up the detached Bullseye,
* reset the state.

---

# 48. Respawn

On respawn:

```text
Bullseye State = Attached
```

The player should receive a normal Bullseye on their body.

Also reset:

```text
Grenade count
```

to the configured starting amount.

---

# 49. Prevent Duplicate Bullseyes

Death, respawn, grenade detachment, and return must never cause:

```text
Player has 2 Bullseyes
```

or:

```text
Player has no Bullseye after respawn
```

Each living player should have exactly one active Bullseye.

---

# Phase 12 — Multiplayer Networking

## 50. Networked Grenade

Grenades must function correctly for all players.

When Player A throws a grenade:

```text
Player A sees grenade
Player B sees grenade
```

Both should see approximately the same:

* position,
* trajectory,
* bounce,
* explosion.

---

# 51. Network Authority

Use the project's existing multiplayer authority model.

Do not allow an arbitrary client to directly declare:

```text
I detached Player B's Bullseye.
```

or:

```text
I killed Player B.
```

without appropriate network authority/validation.

Explosion results, Bullseye state, and damage must follow the existing authoritative architecture.

---

# 52. Networked Bullseye State

All clients must agree whether a Bullseye is:

```text
Attached
```

or:

```text
Detached
```

When detached, clients should see approximately the same Bullseye world position.

---

# 53. Networked Reattachment

When the Bullseye returns:

* all clients should observe the state transition,
* all clients should see it reattach to the correct player,
* no client should continue seeing a duplicate detached Bullseye.

---

# 54. Owner Identification

Network synchronization must preserve the relationship:

```text
Bullseye Network Object
        ↓
Owning Player Network Object
```

This relationship must remain reliable through detachment.

---

# Phase 13 — Grenade Feedback

## 55. Throw Feedback

Provide basic feedback when a grenade is thrown.

This may include:

* animation,
* sound,
* temporary weapon movement.

Expose an optional:

```text
GrenadeThrowSFX
```

slot.

---

# 56. Bullseye Detachment Feedback

Bullseye detachment should be noticeable.

Support optional:

```text
BullseyeDetachSFX
```

and/or a small visual effect.

The effect should communicate:

> The player's Bullseye has been knocked loose.

---

# 57. Bullseye Return Feedback

Provide optional:

```text
BullseyeReturnSFX
```

or simple visual feedback when the Bullseye recalls to its owner.

Again, provide Inspector slots even if final assets are not yet available.

---

# 58. Owner Feedback

The player whose Bullseye has been detached should receive clear feedback.

At minimum, the first-person player should be able to understand:

```text
My Bullseye has been knocked off.
I am temporarily vulnerable.
```

For this ticket, simple feedback is acceptable.

Possible prototype options:

* short HUD message,
* sound effect,
* temporary Bullseye HUD change.

A full redesigned warning UI is not required.

---

# 59. Detached Bullseye HUD Interaction

The existing cracked Bullseye health HUD should continue reflecting the owner's actual health.

Do not make the HUD disappear simply because the world Bullseye has detached.

A future requirement may add a special detached-state HUD treatment.

---

# Phase 14 — Configuration

## 60. Grenade Configuration

Expose useful values in the Inspector or configuration data.

At minimum:

```text
Starting Grenade Count

Throw Force
Throw Upward Bias

Fuse Duration

Explosion Radius
Explosion Force
Explosion Upward Force

Bullseye Detach Radius

Grenade Prefab
Explosion VFX
Explosion SFX
Throw SFX
```

---

# 61. Detached Bullseye Configuration

Expose:

```text
Detached Return Delay

Bullseye Rigidbody Mass

Bullseye Drag

Bullseye Angular Drag

Bullseye Physics Material

Return Speed

Detach SFX

Return SFX
```

Use existing project conventions where appropriate.

---

# 62. Separate Explosion and Detachment Radius

Prefer supporting two configurable radii:

```text
Knockback Radius
```

and:

```text
Bullseye Detach Radius
```

For example:

```text
Large radius
=
player receives some knockback

Smaller inner radius
=
Bullseye is detached
```

This will give considerably more balancing flexibility.

---

# Initial Prototype Tuning

Exact values will require playtesting.

Reasonable initial targets:

```text
Grenades Per Life:
1

Fuse:
~2.75 seconds

Bullseye Return Delay:
~6 seconds

Player Knockback Radius:
Moderate

Bullseye Detach Radius:
Smaller than knockback radius
```

The grenade should be useful but should require a reasonably good throw to detach another player's Bullseye.

---

# Example Gameplay Scenario

```text
PLAYER A
sees Player B behind partial cover
```

Player A throws grenade:

```text
        O
       /
      /
 A --/                    B
```

Grenade lands near B:

```text
               O   B
```

Explosion:

```text
             \  |  /
           --- BOOM ---
             /  |  \
```

Player B:

```text
is pushed away
```

Bullseye B:

```text
detaches
flies in another direction
lands
rolls
```

Now:

```text
Player B ---------------- Bullseye B
   running                  on ground
```

Player A has a temporary decision:

```text
Chase Player B

OR

Shoot Bullseye B
```

If Player A shoots the Bullseye successfully:

```text
Bullseye B destroyed
        ↓
Player B killed
```

If Player A fails:

```text
~6 seconds expire
        ↓
Bullseye B recalls
        ↓
reattaches to Player B
```

This is the intended combat loop.

---

# Edge Cases

The implementation must handle the following safely.

## Grenade Explodes Beside Thrower

The thrower may:

* be physically pushed,
* have their own Bullseye detached,

if they are within the applicable radius.

Do not automatically exempt the throwing player unless the current game design already prevents self-effects.

This creates meaningful risk when throwing grenades at very close range.

---

## Bullseye Falls Off Map

If a detached Bullseye:

* falls below the level,
* enters a kill volume,
* becomes unreachable,
* or exceeds a configurable maximum distance,

do not leave it lost permanently.

Prefer triggering an early recall to the owner.

---

## Owner Falls Off Map

Existing death/respawn logic should supersede the Bullseye return timer.

---

## Owner Dies From Another Player

Cancel detached Bullseye logic and clean it up correctly.

---

## Owner Respawns Before Timer Ends

Respawn state takes priority.

Do not allow the old Bullseye to fly back to the newly respawned player later.

---

## Multiple Grenades

A detached Bullseye may receive additional explosion force, but it remains one networked Bullseye with one owner and one original return timer.

---

# Acceptance Criteria

REQ-028 is complete when:

* [ ] `C` throws a grenade on keyboard/mouse.
* [ ] Left Trigger throws a grenade on gamepad.
* [ ] Input is integrated through the existing Input System architecture.
* [ ] A grenade prefab exists.
* [ ] The grenade uses Rigidbody physics.
* [ ] The grenade follows a believable throwing arc.
* [ ] Throw force is configurable.
* [ ] The grenade has a configurable fuse.
* [ ] The grenade explodes after the fuse expires.
* [ ] Explosion radius is configurable.
* [ ] Explosion force is configurable.
* [ ] Grenades cause zero conventional health damage.
* [ ] Nearby players are pushed away by the explosion.
* [ ] Player knockback decreases with distance.
* [ ] Nearby Bullseyes can be detached.
* [ ] Bullseye detachment radius is configurable.
* [ ] The Bullseye visibly leaves the player's body.
* [ ] Detached Bullseyes activate Rigidbody physics.
* [ ] Detached Bullseyes respond to gravity.
* [ ] Detached Bullseyes collide with the environment.
* [ ] Detached Bullseyes behave like relatively heavy physical objects.
* [ ] Detached Bullseyes remain associated with their owning player.
* [ ] Detached Bullseyes remain valid weapon targets.
* [ ] Shooting a detached Bullseye damages its owner.
* [ ] Destroying a detached Bullseye can kill its owner.
* [ ] Player death triggers only once.
* [ ] A Bullseye that survives automatically returns after approximately 5–7 seconds.
* [ ] The initial configured return time is approximately 6 seconds.
* [ ] Returning Bullseyes stop normal Rigidbody movement.
* [ ] Returning Bullseyes reliably reattach to their correct owner.
* [ ] Normal Bullseye movement resumes after reattachment.
* [ ] A second grenade can push an already-detached Bullseye.
* [ ] A second grenade does not create another Bullseye.
* [ ] Additional grenade hits do not indefinitely reset the Bullseye return timer.
* [ ] Bullseyes that fall outside the playable environment cannot be permanently lost.
* [ ] Death correctly cancels detached Bullseye state.
* [ ] Respawn restores exactly one attached Bullseye.
* [ ] Grenade count resets appropriately on respawn.
* [ ] Default grenade count is configurable.
* [ ] Initial default is one grenade per life.
* [ ] Explosion VFX can be assigned from the Inspector.
* [ ] Explosion SFX can be assigned from the Inspector.
* [ ] Bullseye detach SFX can be assigned from the Inspector.
* [ ] Bullseye return SFX can be assigned from the Inspector.
* [ ] Grenade throwing works correctly for each player in multiplayer.
* [ ] Grenade physics are synchronized sufficiently for gameplay.
* [ ] Bullseye attached/detached state is synchronized.
* [ ] Detached Bullseye physics are visible to all players.
* [ ] Bullseye ownership remains synchronized.
* [ ] Bullseye destruction kills the correct player.
* [ ] Bullseye return is synchronized across clients.
* [ ] Existing shooting still works.
* [ ] Existing REQ-025 accuracy and reticle behavior still works.
* [ ] Existing REQ-026 weapon damage still works.
* [ ] Existing health and regeneration still work.
* [ ] Existing death and respawn systems still work.
* [ ] Existing weapon switching and animations still work.

---

# Out of Scope

Do not implement the following as part of REQ-028:

* grenade pickups,
* multiple grenade types,
* smoke grenades,
* flashbangs,
* incendiary grenades,
* grenade cooking,
* grenade trajectory preview,
* grenade launcher weapons,
* sticky grenades,
* destructible environments,
* player health damage from grenade explosions,
* fragmentation damage,
* grenade inventory UI beyond what is necessary for functionality,
* advanced Bullseye recall effects,
* stealing another player's detached Bullseye,
* manually retrieving a detached Bullseye,
* permanent Bullseye detachment.

These can be considered later.

---

# Architecture Guidance

Avoid implementing the Bullseye detachment as a completely separate Bullseye system.

Prefer extending the existing Bullseye controller into a state-based architecture:

```text
                 Bullseye
                    |
          +---------+---------+
          |                   |
       Attached            Detached
          |                   |
Surface movement         Rigidbody physics
Turn response            World collision
Random movement          Weapon vulnerability
          |                   |
          +---------+---------+
                    |
                  Return
                    |
                 Attached
```

The Bullseye should always retain:

```text
Owner
State
Network identity
Damage relationship
```

Only its movement/physics behavior changes.

---

# Design Intent

The grenade should create a different kind of threat from firearms.

Firearms ask:

> Can I hit the opponent's moving Bullseye?

The grenade asks:

> Can I disrupt the opponent enough to create an easier Bullseye opportunity?

A successful grenade does not automatically reward the player with damage.

Instead:

```text
Good grenade placement
        ↓
Bullseye knocked loose
        ↓
Temporary vulnerability
        ↓
Attacker must capitalize
```

The defending player meanwhile has approximately six seconds to:

* escape,
* fight back,
* protect the Bullseye,
* prevent opponents from getting a clean shot.

This should create moments where the player's body and their vulnerability are temporarily separated in physical space.

That interaction should become one of the mechanics that makes Bullseye distinctly different from a conventional arena shooter.
