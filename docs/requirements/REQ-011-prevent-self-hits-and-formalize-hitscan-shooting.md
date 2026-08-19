# REQ-011 — Prevent Self-Hits and Formalize Hitscan Shooting

## Summary

Update the current shooting system so that a player can never shoot or damage their own bullseye.

The current bullseye can move across the player's face or other areas near the firing ray. Because the existing shooting system uses a scan/raycast, the local player's own bullseye can sometimes intersect that scan and register as a hit.

This behavior should be removed.

For the prototype, Bullseye should continue using a **hitscan/raycast-based shooting system** rather than converting all weapons to physical projectiles.

The shooting system should explicitly ignore the firing player's own bullseye and other self-colliders when determining valid weapon hits.

---

# Design Decision

## Keep Hitscan Shooting

The current weapon should remain hitscan.

Conceptually:

```text
Player presses fire
        ↓
Aim ray is generated
        ↓
Ray checks for valid targets
        ↓
Opponent bullseye hit?
    Yes → damage
    No  → no damage
```

Do not replace the current weapon with a spawned physical projectile as part of this requirement.

---

# Why Hitscan Is Preferred for the Prototype

Bullseye's main combat challenge is currently:

> Accurately hitting a small, moving vulnerability on another player.

Hitscan supports this well because the player's shot occurs immediately when the trigger is pulled.

It also keeps the prototype easier to tune and test.

A projectile system would introduce additional variables including:

* Projectile velocity
* Projectile lifetime
* Projectile collision
* Bullet drop, if applicable
* Network spawning
* Network synchronization
* Latency effects
* Projectile interpolation
* Server/client disagreement over projectile position
* Potential projectile tunneling
* Leading moving targets

Those systems may eventually be useful for particular weapons, but they are not necessary to evaluate the core Bullseye mechanic.

---

# Functional Requirements

## 1. Players Cannot Shoot Their Own Bullseye

A shot fired by a player must never register that player's own bullseye as a valid target.

For example:

```text
Player 1 fires
        ↓
Ray intersects Player 1 bullseye
        ↓
IGNORE
        ↓
Continue evaluating valid shot targets if appropriate
```

Player 1's bullseye should not:

* Take damage from Player 1.
* Cause Player 1 to die.
* Count as a successful hit.
* Trigger hit feedback intended for enemy hits.

---

## 2. Self-Hit Protection Must Be Ownership-Based

Do not solve this only by changing where the bullseye is allowed to move.

The bullseye should remain capable of moving across the face/head if that is otherwise valid according to the existing bullseye movement system.

Instead, the shooting system should understand:

> A player cannot damage their own vulnerability.

The implementation should use the existing player/network ownership relationship where possible.

Conceptually:

```text
Hit Bullseye
    ↓
Determine bullseye owner
    ↓
Is bullseye owner == shooter?
        ↓
      YES
        ↓
     Ignore hit
```

---

# 3. Ignore Appropriate Self-Colliders

The shooting ray should not be blocked incorrectly by the firing player's own character.

Depending on the existing project architecture, this may include:

* Player body collider
* Head collider
* CharacterController
* Bullseye collider
* Other colliders attached to the firing player's prefab

The implementation should ensure that the firing player's own geometry does not prevent them from shooting an opponent.

---

# 4. Preserve Enemy Bullseye Detection

The self-hit fix must not interfere with legitimate shots against another player.

Example:

```text
Player 1 fires
        ↓
Ray passes through/ignores Player 1 geometry
        ↓
Ray reaches Player 2
        ↓
Player 2 bullseye hit
        ↓
Damage Player 2
```

Player 2's bullseye should continue functioning exactly as a vulnerability.

---

# 5. Body Hits Remain Non-Damaging

The existing Bullseye design should remain intact:

> The bullseye is the player's vulnerable hit area.

If the player's body is currently non-damaging outside of the bullseye, this behavior should remain unchanged.

Example:

```text
Shot → opponent body
Result → no damage

Shot → opponent bullseye
Result → damage
```

Do not introduce conventional full-body FPS damage as part of this requirement.

---

# 6. Keep Hitscan Immediate

The weapon should continue behaving as an immediate shot.

The player should not need to wait for a simulated bullet to travel to the target.

When fire input is accepted:

```text
Fire
→ Perform hit detection
→ Determine result
→ Apply appropriate gameplay response
```

This should continue to feel immediate.

---

# Raycast Filtering

The implementation agent should inspect the current shooting architecture and choose the cleanest way to prevent self-intersections.

Valid approaches may include:

* Layer masks
* Ownership checks
* Ignoring specific colliders
* Filtering raycast results
* A combination of these approaches

The exact implementation is left to the agent.

However, the solution should be robust and reusable rather than relying on a fragile positional workaround.

---

# Preferred Behavior When Ray Intersects Self

If the ray begins inside or passes through the firing player's own collider, it should still be capable of hitting a legitimate target beyond that collider.

For example:

```text
Camera
  ↓
Player 1 face / bullseye
  ↓
Enemy bullseye
```

The presence of Player 1's own geometry should not automatically terminate the shot.

If necessary, use filtered raycast results rather than simply accepting the first collider returned.

---

# Multiplayer Requirements

Hit detection should remain compatible with the project's existing multiplayer authority model.

The system must maintain a clear distinction between:

```text
Shooter
Target
Bullseye Owner
```

For any potential bullseye hit:

```text
Shooter ID != Bullseye Owner ID
```

must be true before enemy damage can occur.

---

# Example

## Valid Shot

```text
Player 1
    ↓ shoots
Player 2 Bullseye
    ↓
Valid enemy bullseye
    ↓
Player 2 takes damage
```

---

## Invalid Self-Hit

```text
Player 1
    ↓ shoots
Player 1 Bullseye
    ↓
Same owner
    ↓
Ignore
```

---

# Interaction With Face/Head Bullseyes

Bullseyes may continue moving onto the player's:

* Head
* Face
* Upper torso
* Other currently valid body surfaces

A bullseye appearing near the player's camera or firing line should not require special bullseye movement restrictions.

This requirement separates two concepts:

### Vulnerability location

Where an enemy is allowed to shoot the player.

### Weapon ownership

Who is allowed to damage that vulnerability.

An enemy should still be able to shoot a bullseye located on a player's face.

The player themselves should not.

---

# Camera and Shot Origin

Do not substantially redesign aiming as part of this ticket unless required to fix the self-hit bug.

The current reticle/aiming behavior should remain intact.

If the existing shot ray originates from the camera, it may continue doing so.

If changes to ray origin or collision filtering are necessary, they should preserve the fundamental expectation:

> The shot should go where the center-screen reticle indicates.

---

# Hit Feedback

Self-intersections should not trigger enemy-hit feedback.

If the game currently or later includes:

* Hit markers
* Hit sounds
* Damage indicators
* Controller rumble
* Kill confirmation

these should only activate when an actual valid enemy hit occurs.

A player's own bullseye intersecting the firing ray should behave as though it were not a valid target.

---

# Projectile Weapons — Future Compatibility

This requirement does not prohibit projectile weapons in the future.

The weapon architecture should ideally permit future weapons to use different firing models.

For example:

```text
Assault Rifle
→ Hitscan

Pistol
→ Hitscan

Shotgun
→ Multiple hitscan pellets

Rocket Launcher
→ Projectile

Grenade Launcher
→ Projectile
```

However, implementing this broader weapon architecture is not required as part of REQ-011.

---

# Out of Scope

The following are outside the scope of this requirement:

* Physical bullets for the current weapon.
* Bullet travel time.
* Bullet drop.
* Projectile networking.
* Projectile prediction.
* Lag compensation redesign.
* Weapon-specific firing systems.
* New guns.
* Reloading.
* Ammunition.
* Recoil changes.
* Damage balancing.
* Changing bullseye randomization.
* Removing the face/head from valid bullseye locations.
* Changing bullseye size.
* Changing bullseye movement speed.
* Hit-marker implementation.
* Muzzle flashes.
* Weapon animations.

---

# Acceptance Criteria

* [ ] A player cannot shoot their own bullseye.
* [ ] A player's own bullseye cannot damage or kill that player.
* [ ] A player's own bullseye does not count as an enemy hit.
* [ ] A player's own character colliders do not incorrectly block shots toward enemies.
* [ ] An opponent can still shoot a bullseye located on the player's face/head.
* [ ] Player 1 can correctly damage Player 2's bullseye.
* [ ] Player 2 can correctly damage Player 1's bullseye.
* [ ] Shots against non-bullseye opponent body surfaces continue to behave according to existing rules.
* [ ] Shooting remains hitscan/immediate.
* [ ] The center-screen reticle continues to accurately represent shot direction.
* [ ] Existing damage, death, respawn, and controller-rumble behavior continues working.
* [ ] Multiplayer clients continue to agree on valid hits.
* [ ] No new console errors are introduced.

---

# Testing Procedure

## Test 1 — Bullseye on Face

Allow Player 1's bullseye to move across Player 1's face.

Player 1 should fire repeatedly.

Expected:

* Player 1 does not damage themselves.
* Player 1 does not die.
* Self-hit feedback is not triggered.

---

## Test 2 — Enemy Shoots Face Bullseye

Allow Player 1's bullseye to move onto their face.

Have Player 2 shoot it.

Expected:

* Player 1 receives damage normally.

This verifies that face/head bullseyes remain legitimate enemy targets.

---

## Test 3 — Self Geometry Between Camera and Enemy

Position Player 1 so their own character geometry may overlap the ray origin or firing direction.

Aim at Player 2's bullseye.

Expected:

* Player 1's own geometry does not incorrectly prevent the enemy hit.

---

## Test 4 — Normal Enemy Hit

Player 1 shoots Player 2's bullseye under normal conditions.

Expected:

* Existing damage behavior functions normally.
* Existing damage rumble functions normally.

Repeat with Player 2 shooting Player 1.

---

## Test 5 — Body Miss

Shoot Player 2 somewhere that is not their bullseye.

Expected:

* Existing non-bullseye behavior remains unchanged.

---

## Test 6 — Respawn

Kill a player and allow them to respawn.

After respawn, repeat the self-hit and enemy-hit tests.

Expected:

* Ownership filtering continues functioning correctly after respawn.

---

# Prototype Design Intent

Bullseye should distinguish between **precision** and **ballistics complexity**.

At this stage, the interesting skill test is:

> Can the player place their crosshair on another player's small, moving vulnerability?

The interesting skill test is not yet:

> Can the player predict bullet travel and lead a moving target?

For that reason, the prototype should retain immediate hitscan shooting while eliminating accidental self-hits through proper ownership and collision filtering.

Projectile weapons can be evaluated later on a weapon-by-weapon basis if they add something meaningful to the combat sandbox.
