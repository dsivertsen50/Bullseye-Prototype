# REQ-060 — Bazooka / Rocket Launcher Heavy Weapon

## Summary

Add the new **Bazooka** model to the game as a fully functional **Heavy weapon**.

Unlike the current bullet-based firearms, the Bazooka should fire a **physical projectile / rocket** that travels through the world over time rather than performing an instantaneous hitscan.

The rocket should travel toward the point at which the player is aiming, collide with world geometry or players, and explode on impact.

Rocket speed, damage, explosion radius, and other important values must be configurable so the weapon can be balanced without rewriting code.

The initial implementation should **NOT include heat-seeking or homing functionality**, but the projectile architecture should make it straightforward to add homing behavior later.

---

# Goals

1. Import and configure the Bazooka model created for the game.
2. Add it to the weapon system as a **Heavy** weapon.
3. Use the existing `HeavyGun_Hold` / Heavy weapon pose system from REQ-049.
4. Fire an actual networked rocket projectile instead of hitscan bullets.
5. Have the rocket travel toward the player's aim point.
6. Make rocket travel speed configurable.
7. Make the rocket explode when it hits a valid target or world surface.
8. Apply configurable radial explosion damage.
9. Make the projectile system extensible so heat-seeking rockets can be implemented later.
10. Ensure the system functions correctly in multiplayer.

---

# 1. Bazooka Asset Integration

Locate the Bazooka model created for the project and configure it for use as a weapon.

Create or configure the appropriate:

- Weapon prefab
- Weapon definition
- Pickup prefab
- First-person weapon representation
- Third-person/world representation
- Network weapon references
- Weapon icon if the existing system requires one

The Bazooka should be categorized as:

```text
Weapon Category: Heavy
Pose Pattern: HeavyGun_Hold
Fire Type: Projectile
```

Do NOT create a Bazooka-specific character pose unless absolutely necessary.

The weapon should use the generalized Heavy weapon positioning/pose system established in REQ-049.

---

# 2. Projectile-Based Weapon Architecture

The Bazooka should **NOT use the standard hitscan firearm behavior**.

Instead, firing should spawn a rocket projectile.

Expected conceptual behavior:

```text
Player presses Fire
    ↓
Bazooka determines aim direction
    ↓
Server spawns RocketProjectile
    ↓
Projectile travels through world
    ↓
Projectile collides with something
    ↓
Explosion occurs
    ↓
Radial damage is calculated
    ↓
Projectile is destroyed/despawned
```

Prefer extending the existing weapon framework rather than creating an entirely disconnected Bazooka system.

If necessary, introduce a generalized firing-mode distinction such as:

```csharp
public enum WeaponFireType
{
    Hitscan,
    Projectile
}
```

or an equivalent architecture that fits the current project.

Avoid putting Bazooka-specific logic directly into generic player input scripts.

---

# 3. Rocket Projectile

Create a reusable projectile component/prefab, conceptually similar to:

```text
RocketProjectile
```

The rocket must:

- Spawn from the Bazooka muzzle.
- Travel forward through the game world.
- Have visible movement rather than teleporting to the target.
- Detect collisions.
- Explode when it hits something.
- Be synchronized appropriately across multiplayer clients.
- Despawn after exploding.
- Automatically despawn after a configurable maximum lifetime if it never hits anything.

The rocket should NOT currently:

- Track players.
- Curve toward targets.
- Acquire targets.
- Automatically lock onto enemies.

It should simply travel along its initial trajectory.

---

# 4. Aim Direction

The projectile must travel toward the location the player is actually aiming at rather than simply following the exact forward direction of the weapon model.

Use the player's existing camera/aim system to determine the intended target point.

Recommended flow:

```text
Camera center / crosshair
        ↓
Aim ray
        ↓
Determine target point
        ↓
Rocket launches from Bazooka muzzle
        ↓
Rocket direction = muzzle → target point
```

This avoids the common FPS problem where the player's crosshair points at one location but the projectile travels somewhere else because the weapon muzzle is offset from the camera.

If the aim ray does not hit anything, calculate an aim point sufficiently far into the distance.

Example:

```text
Aim Distance: 500m+
```

or another configurable value appropriate for the game.

---

# 5. Adjustable Rocket Speed

Rocket velocity must be configurable from the weapon/projectile definition.

For example:

```csharp
[SerializeField] float projectileSpeed = 35f;
```

Do NOT hardcode the final rocket speed.

We will need to test different values to determine how difficult it should be to lead moving targets.

Expose at minimum:

```text
Projectile Speed
Projectile Lifetime
```

Prefer placing these settings in the Bazooka's weapon definition or projectile configuration.

Changing:

```text
Projectile Speed = 25
```

to:

```text
Projectile Speed = 40
```

should require only an Inspector/ScriptableObject value change.

---

# 6. Rocket Physics

The rocket should initially travel in a predictable straight trajectory.

For the first version:

```text
Gravity Effect: 0 or very small
```

Do not automatically give the projectile a dramatic grenade-style arc.

The exact implementation may use:

- Rigidbody velocity
- Rigidbody movement
- Custom projectile movement

Choose whichever works most reliably with the existing networking architecture.

The rocket should maintain consistent speed unless a future mechanic intentionally changes it.

---

# 7. Collision Detection

The rocket should explode when it collides with:

- Players
- Floors
- Walls
- Ceilings
- Props
- Other appropriate level geometry

Prevent accidental immediate collision with:

- The player who fired it
- The Bazooka itself
- The firing player's collider immediately after spawning

The projectile should therefore either ignore the firing player's collider or otherwise safely clear the player's weapon/body before becoming collision-active.

Use sufficiently reliable collision detection so fast rockets do not pass through thin surfaces.

Consider:

```text
Collision Detection Mode = Continuous
```

or an equivalent solution if appropriate.

---

# 8. Explosion

When the rocket impacts something, create an explosion at the impact point.

The explosion should have configurable:

```text
Explosion Radius
Maximum Damage
Minimum Damage
Damage Falloff
Explosion Force
```

Example architecture:

```text
Center of explosion
      ↓
Find damageable objects inside radius
      ↓
Calculate distance
      ↓
Apply damage based on distance
```

Damage should decrease as the player gets farther from the center of the explosion.

Example concept:

```text
0m        = 100% damage
50% radius = reduced damage
Edge       = minimum or zero damage
```

Do not hardcode these specific percentages unless they fit the existing damage system.

All values should be tunable.

---

# 9. Direct Hits

A direct rocket hit on another player should be extremely powerful.

For the initial implementation, expose a configurable:

```text
Direct Hit Damage
```

A direct hit should either:

- Kill the player outright,

or

- Apply direct-hit damage plus explosion damage,

depending on whichever approach integrates most cleanly with the current damage architecture.

The value must remain configurable.

Avoid permanently hardcoding a one-hit kill because weapon balancing will happen later.

---

# 10. Bullseye Interaction

The Bazooka explosion should interact with the existing Bullseye damage system through the game's standard damage/explosion architecture.

Do NOT create a completely separate Bullseye system for the Bazooka.

If existing explosions are already capable of damaging players and/or interacting with Bullseyes, reuse those systems wherever possible.

The Bazooka itself should not automatically detach Bullseyes unless the normal explosion rules say it should.

Special grenade mechanics such as:

- Combustion Bullseye detachment
- Magnetism Bullseye pulling

should remain separate mechanics.

---

# 11. Explosion Visual Effect

Add a placeholder or existing appropriate explosion effect when the rocket detonates.

The system should support:

```text
Explosion VFX
Explosion SFX
```

Both references should be configurable.

If there is not yet a production-quality rocket explosion asset, use a clear placeholder implementation rather than blocking functionality.

Structure the system so the effect can be swapped later without code changes.

---

# 12. Rocket Visuals

The rocket should visibly travel through the environment.

Support configurable references for:

```text
Rocket Mesh
Rocket Trail
Rocket Flight Sound
Rocket Explosion Effect
```

A Trail Renderer or similar visual effect may be used so players can clearly see rocket trajectories.

This will be especially useful because projectile speed may intentionally be slow enough that players can react to incoming rockets.

---

# 13. Audio

Provide hooks for:

```text
Bazooka Fire SFX
Rocket Flight SFX
Rocket Explosion SFX
```

Use existing placeholder sounds where appropriate.

Do not tightly couple specific sound assets to projectile code.

Audio references should be replaceable through configuration.

---

# 14. Fire Rate / Reload Behavior

The Bazooka should behave like a slow Heavy weapon.

Expose configuration for:

```text
Magazine Size
Reserve Ammo
Fire Rate
Reload Time
```

Recommended initial structure:

```text
Magazine Size: 1 rocket
```

but do not hardcode this if the existing weapon definition can already configure magazine capacity.

The player should:

1. Fire one rocket.
2. Need to reload before firing again.
3. Play the appropriate reload logic/animation hooks.

Do not allow rapid-fire rockets unless the weapon configuration is intentionally changed later.

---

# 15. Recoil

Firing the Bazooka should trigger substantial weapon recoil compared with standard firearms.

Use the existing recoil architecture where possible.

Support configurable:

```text
Camera Recoil
Weapon Model Recoil
Third-Person Recoil Animation
```

Do NOT introduce uncontrolled arm flailing or procedural deformation of the existing character pose.

Preserve the Heavy weapon holding pose and apply recoil in a controlled manner.

---

# 16. Multiplayer / Netcode

The Bazooka must work correctly with Netcode for GameObjects.

The authoritative game state should determine:

- Rocket spawning
- Rocket trajectory
- Collision
- Explosion
- Damage
- Eliminations

Clients should not independently determine whether their rocket hit another player.

Recommended conceptual architecture:

```text
Client presses Fire
        ↓
Fire request sent to server
        ↓
Server validates shot
        ↓
Server spawns networked rocket
        ↓
Server controls authoritative projectile state
        ↓
Server detects impact
        ↓
Server applies explosion damage
        ↓
Explosion VFX/SFX replicated to clients
```

The exact implementation should follow the project's existing weapon/networking architecture.

---

# 17. Projectile Ownership

Each rocket must know which player fired it.

Track at minimum:

```text
Shooter NetworkObjectId
Weapon / WeaponDefinition
Projectile Damage Source
```

This is required for:

- Kill credit
- Telemetry
- Assist logic
- Future lifetime statistics
- Preventing the projectile from immediately hitting its owner

The existing elimination system from REQ-055 should properly attribute Bazooka kills to the firing player.

---

# 18. Telemetry Integration

Integrate the Bazooka with the weapon telemetry architecture created in REQ-055.

The system should be capable of tracking:

```text
Bazooka Shots Fired
Bazooka Direct Hits
Bazooka Eliminations
Bazooka Damage
```

If the existing telemetry architecture automatically tracks weapon statistics, simply ensure the Bazooka is correctly registered.

Avoid building redundant telemetry systems.

---

# 19. Future Heat-Seeking Support

Do NOT implement homing rockets in REQ-060.

However, avoid designing the projectile in a way that would require it to be rewritten later.

Prefer an extensible structure such as:

```csharp
ProjectileMovement
    ├── StraightProjectileMovement
    └── Future: HomingProjectileMovement
```

or:

```csharp
public enum ProjectileGuidanceMode
{
    Straight,
    Homing
}
```

The exact architecture is up to Cursor based on the existing codebase.

The important requirement is:

> A future ticket should be able to add target acquisition and heat-seeking behavior without rebuilding the entire Bazooka/projectile system.

Do not implement unused homing code now.

---

# 20. Inspector / Weapon Definition Configuration

The Bazooka should expose appropriate balancing values.

At minimum:

```text
Projectile Prefab
Projectile Speed
Projectile Lifetime

Magazine Size
Reserve Ammo
Fire Rate
Reload Time

Direct Hit Damage
Explosion Damage
Explosion Radius
Damage Falloff

Explosion Force

Fire SFX
Flight SFX
Explosion SFX

Explosion VFX
Projectile Trail
```

Use the project's existing ScriptableObject/weapon-definition architecture wherever possible.

---

# 21. Heavy Weapon Pose

The Bazooka must use the generalized Heavy weapon pose system.

Expected:

```text
Weapon Pose Type = Heavy
Animation / Hold Pattern = HeavyGun_Hold
```

The character should visually support the Bazooka with an appropriate heavy-weapon stance.

Do not reintroduce the old approach of manually creating a completely separate animation architecture for every individual firearm.

Weapon positioning should remain configurable so the Bazooka can be aligned correctly within the character's hands.

---

# 22. Pickup Integration

The Bazooka should function as a normal weapon pickup within the existing weapon system.

A player should be able to:

1. Approach the Bazooka pickup.
2. Pick it up using the existing weapon interaction system.
3. Equip it.
4. Fire it.
5. Reload it.
6. Switch away from it.
7. Drop/replace it according to current inventory rules.

Do not create a separate pickup framework exclusively for Heavy weapons.

---

# 23. Debug Visualization

Add optional development/debug visualization for projectile behavior.

When debugging is enabled, Cursor may provide:

- Rocket trajectory line
- Explosion radius sphere
- Impact location
- Aim target point

Example:

```text
DebugProjectileTrajectory = true
DebugExplosionRadius = true
```

These should be disabled by default in normal gameplay.

This will make balancing rocket speed and explosion radius much easier.

---

# 24. Code Architecture Requirements

Cursor should first inspect the existing:

- Weapon definitions
- Fire logic
- Damage system
- Explosion/grenade system
- Networking system
- Pickup system
- Telemetry system
- Heavy weapon pose system

Reuse existing systems wherever practical.

Do NOT create:

```text
BazookaManager
BazookaDamageManager
BazookaNetworkManager
BazookaExplosionManager
```

if equivalent generalized systems already exist.

Prefer reusable components such as:

```text
ProjectileWeapon
RocketProjectile
ExplosionDamage
ProjectileDefinition
```

where they provide real architectural value.

The implementation should support future projectile weapons without significant duplication.

---

# Acceptance Criteria

REQ-060 is complete when all of the following are true:

- [ ] The Bazooka model is imported and configured as a usable weapon.
- [ ] The Bazooka is classified as a Heavy weapon.
- [ ] The third-person player uses the `HeavyGun_Hold` pose system.
- [ ] The Bazooka can be picked up and equipped through the existing weapon system.
- [ ] Pulling the trigger launches a visible rocket projectile.
- [ ] The rocket is NOT hitscan.
- [ ] The rocket travels toward the player's crosshair/aim point.
- [ ] Rocket speed can be changed from configuration without editing projectile code.
- [ ] Rocket lifetime is configurable.
- [ ] Rockets reliably collide with world geometry.
- [ ] Rockets reliably collide with players.
- [ ] Rockets do not immediately collide with the player who fired them.
- [ ] Rockets explode on impact.
- [ ] Explosion radius is configurable.
- [ ] Explosion damage is configurable.
- [ ] Explosion damage supports distance-based falloff.
- [ ] Direct-hit damage is configurable.
- [ ] Explosion VFX can be assigned.
- [ ] Explosion SFX can be assigned.
- [ ] Rocket trail/visuals can be assigned.
- [ ] Fire rate and reload timing can be configured.
- [ ] The default configuration supports a one-rocket magazine.
- [ ] Rocket firing and damage work correctly in multiplayer.
- [ ] The server remains authoritative over projectile hits and damage.
- [ ] Eliminations are properly credited to the Bazooka's owner.
- [ ] The Bazooka integrates with REQ-055 weapon telemetry.
- [ ] The projectile architecture can support future homing behavior.
- [ ] No heat-seeking functionality is actually implemented yet.
- [ ] Existing hitscan firearms continue to work normally.
- [ ] Existing grenades continue to work normally.
- [ ] No new compilation errors or significant runtime errors are introduced.

---

# Out of Scope

Do NOT implement the following as part of REQ-060:

- Heat-seeking rockets
- Target lock-on
- Lock-on HUD
- Vehicle targeting
- Multiple rocket types
- Player-controlled rockets
- Rocket jumping as a specifically tuned mechanic
- New Bullseye detachment mechanics
- Custom Bazooka kill effects
- Final production explosion artwork
- Final production audio
- Final weapon balancing

These can be handled through later requirements.

---

# Future Extension

A later requirement may add a **heat-seeking / lock-on mode**.

That future system may include:

```text
Target acquisition
Lock-on timer
Target indicator
Lock confirmation sound
Homing strength
Maximum tracking angle
Tracking duration
Target escape behavior
```

REQ-060 should only establish the projectile architecture necessary to make that possible later.

---

# Implementation Principle

The Bazooka should introduce **projectile weapons as a reusable weapon category**, not merely add one special-case gun.

The desired architecture is:

```text
Weapon System
│
├── Hitscan Weapons
│   ├── Pistol
│   ├── AK
│   ├── DMR
│   └── Sniper Rifle
│
└── Projectile Weapons
    └── Bazooka
         │
         └── RocketProjectile
              │
              ├── Straight trajectory now
              └── Homing capability later
```

This should establish a clean foundation for any future rocket launchers or other projectile-based weapons.