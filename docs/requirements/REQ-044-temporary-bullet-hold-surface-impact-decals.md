# REQ-044 — Temporary Bullet Hole / Surface Impact Decals

## Objective

Add a reusable bullet-impact decal system so that firearm shots temporarily leave visible bullet marks on surfaces they strike.

The pistol, DMR, AK, shotgun, and future sniper rifle should use the same general family of bullet-hole visuals, but each weapon should produce different impact sizes, patterns, and maximum decal ranges.

This system should improve weapon feedback and make firefights leave temporary visual evidence in the environment without permanently modifying level geometry.

---

## Background

Current weapon fire produces hits against players and environmental surfaces, but environmental impacts do not leave a persistent visual mark.

REQ-044 should add temporary bullet-hole decals to valid surfaces after a weapon shot successfully intersects them.

The system should be designed around the existing weapon architecture and remain extensible for future weapons.

The initial implementation does **not** need advanced material-specific damage such as:

* Glass cracking
* Wood splintering
* Concrete debris
* Metal deformation
* Penetration
* Ricochets
* Destructible geometry

Those can be implemented separately later.

---

# 1. Core Bullet Impact Behavior

When a firearm raycast / hitscan shot intersects a valid environmental surface:

1. Determine which weapon fired the shot.
2. Determine the distance between the weapon/fire origin and the impact point.
3. Determine whether that distance is within the weapon's allowed bullet-mark distance.
4. If valid, spawn a bullet-hole decal at the impact location.
5. Orient the decal so that it sits flush against the surface.
6. Apply a very small surface offset to prevent z-fighting.
7. Optionally choose one decal texture randomly from an array of available bullet-hole variants.
8. Remove or recycle the decal after a configurable amount of time.

Bullet marks should be cosmetic only.

They must not affect:

* Damage
* Hit detection
* Physics
* Player movement
* Projectile behavior
* Network authority

---

# 2. Shared Bullet Hole Visual Set

All firearm types should use the same general family of bullet-hole graphics.

For example:

```text
BulletHole_01
BulletHole_02
BulletHole_03
BulletHole_04
```

The system should expose something similar to:

```text
Bullet Hole Decal Variants
[0] BulletHole_01
[1] BulletHole_02
[2] BulletHole_03
[3] BulletHole_04
```

When an impact occurs, randomly select one of the available variants.

This should allow us to create visual variety without requiring weapon-specific artwork.

If only one bullet-hole texture is assigned, the system should still work normally.

---

# 3. Weapon-Specific Bullet Hole Size

Each weapon should define a configurable decal-size multiplier.

Initial desired behavior:

| Weapon       | Bullet Mark Size              |
| ------------ | ----------------------------- |
| Pistol       | Medium                        |
| DMR          | Medium                        |
| AK           | Small                         |
| Sniper Rifle | Medium-Large                  |
| Shotgun      | Small individual pellet marks |

Suggested approximate relative scale:

```text
AK             = 0.75
Shotgun Pellet = 0.65–0.75
Pistol         = 1.00
DMR            = 1.00
Sniper         = 1.10–1.20
```

These numbers are starting points only.

All sizes must be configurable from weapon definitions / inspectors without modifying code.

The sniper impact should only be slightly larger than the pistol or DMR rather than dramatically oversized.

---

# 4. Weapon-Specific Bullet Mark Range

A bullet mark should only appear when the surface is within an appropriate distance for that weapon.

This is separate from the weapon's actual damage or hitscan range.

For example, a weapon may still technically hit something outside the bullet-decal range while simply not creating a persistent mark.

Desired relative behavior:

### Shotgun

Bullet-hole decals should only appear at relatively close range.

```text
Range Category:
Close
```

Because the shotgun produces a pellet spread, long-distance surfaces should not become covered in tiny decals.

---

### Pistol

Bullet marks should appear from:

```text
Close → Medium
```

---

### AK

Bullet marks should appear from:

```text
Close → Medium
```

The AK should produce smaller individual bullet marks than the pistol.

---

### DMR

Bullet marks should appear from:

```text
Close → Medium-Long
```

The DMR should therefore leave visible impacts at noticeably greater distances than the pistol or AK.

---

### Sniper Rifle — Future Weapon

The sniper rifle should already be supported by the architecture even though it has not yet been implemented.

Bullet marks should appear from:

```text
Close → Very Long
```

The sniper should have the greatest valid decal range.

Do not hard-code the system around currently available weapons.

---

# 5. Configurable Range Values

Each weapon definition should expose something similar to:

```text
Enable Surface Impact Decals
Bullet Decal Size
Maximum Bullet Decal Distance
Bullet Decal Variant Set
```

Example conceptual values:

```text
Pistol
Maximum Decal Distance = 50

AK
Maximum Decal Distance = 60

DMR
Maximum Decal Distance = 100

Shotgun
Maximum Decal Distance = 25

Sniper
Maximum Decal Distance = 200+
```

These are placeholders rather than mandatory final gameplay distances.

Use units that make sense within the existing Unity world scale.

The important requirement is that the values remain inspector-configurable.

---

# 6. Pistol Behavior

The pistol should:

* Produce one bullet-hole decal per environmental impact.
* Use a medium-sized impact decal.
* Randomly select from the shared bullet-hole texture collection.
* Create decals from close through medium distance.
* Produce no decal if the impact exceeds the pistol's configured maximum decal range.

---

# 7. DMR Behavior

The DMR should:

* Produce one bullet-hole decal per environmental impact.
* Use approximately the same decal size as the pistol.
* Randomly select from the same shared decal set.
* Produce decals at greater distances than the pistol or AK.
* Support close through medium-long impact distances.

Because the DMR is a precision weapon, its marks should correspond closely with the actual hit location.

---

# 8. AK Behavior

The AK should:

* Produce one bullet-hole decal per environmental impact.
* Use smaller marks than the pistol or DMR.
* Use the shared decal texture collection.
* Produce marks from close through medium range.
* Support rapid-fire decal generation without significant performance degradation.

Because the AK can fire quickly, performance safeguards are especially important.

---

# 9. Future Sniper Rifle Behavior

Although the sniper rifle does not currently exist, REQ-044 must allow it to use the decal system later without requiring a rewrite.

The sniper should eventually:

* Produce one impact decal per shot.
* Use a mark slightly larger than the pistol / DMR.
* Use the same shared bullet-hole texture family.
* Produce impacts from close through very long range.
* Have the greatest configurable decal range of any standard firearm.

Do not add the sniper weapon itself as part of this requirement.

Only ensure that the decal architecture supports it.

---

# 10. Shotgun Impact Pattern

The shotgun is fundamentally different from the other firearms.

Rather than producing a single bullet mark, environmental hits should represent the weapon's pellet spread.

Each pellet that hits a valid nearby environmental surface may create its own small decal.

The resulting pattern should therefore look like a naturally scattered shotgun blast.

Example:

```text
     •       •

         •

   •          •

       •
```

The exact number and positions of marks should correspond to the shotgun's existing pellet / spread calculation wherever practical.

Do **not** simply create an identical predetermined decal pattern for every shot.

The pattern should vary from shot to shot.

---

# 11. Shotgun Decal Limits

Because a shotgun may potentially generate many decals per trigger pull, add reasonable safeguards.

Expose settings such as:

```text
Maximum Shotgun Impact Decals Per Shot
```

For example, if the shotgun simulation contains 12 pellets, it may be reasonable to permit up to 8–12 decals depending on performance.

If multiple pellets strike approximately the same position, it is acceptable to prevent excessive overlapping decals.

The shotgun should only leave these marks at close range.

---

# 12. Surface Alignment

Bullet decals must correctly follow surface orientation.

Examples:

### Wall

The decal sits against the vertical wall.

### Floor

The decal lies flat against the floor.

### Ceiling

The decal faces downward from the ceiling.

### Angled Geometry

The decal aligns with the surface normal.

Use the raycast hit normal to orient the decal.

The decal should be placed approximately:

```text
ImpactPoint + SurfaceNormal * SmallOffset
```

to prevent texture flickering / z-fighting.

---

# 13. Valid Surfaces

Bullet marks should initially appear on ordinary environmental geometry.

Examples:

* Walls
* Floors
* Ceilings
* Props
* Map geometry
* Static meshes

Provide a reasonable filtering system so some objects can explicitly reject bullet decals.

Possible approaches include:

```text
LayerMask
Tag
Surface component
Material metadata
```

The implementation should follow whichever approach best fits the existing architecture.

---

# 14. Player Characters

Do not place normal environmental bullet-hole decals directly onto player character meshes as part of REQ-044.

Player hits already have their own gameplay consequences.

Blood effects, character wounds, armor impacts, or player-specific hit decals should be treated as separate future systems.

---

# 15. Bullseyes

Do not use the standard bullet-hole decal system to visually damage the player's bullseye.

Bullseye damage / cracking / shattering should continue to use its specialized systems.

Bullet impacts against a bullseye may trigger its normal hit behavior but should not require an environmental bullet-hole decal.

---

# 16. Decal Lifetime

Bullet marks should be temporary.

Expose:

```text
Bullet Decal Lifetime
```

A reasonable initial value might be:

```text
20–60 seconds
```

or potentially longer depending on performance testing.

Marks may optionally fade during the final portion of their lifetime.

Example:

```text
Lifetime: 45 seconds
Fade begins: final 5 seconds
```

Fading is preferred if straightforward, but simple removal is acceptable for the first implementation.

---

# 17. Maximum Active Decals

Do not allow an unlimited number of bullet marks to accumulate.

Implement a configurable global or per-client limit such as:

```text
Maximum Active Bullet Decals = 100–300
```

When the maximum is reached:

1. Recycle/remove the oldest decal.
2. Spawn the newest decal normally.

The exact default value should be chosen based on reasonable prototype performance.

---

# 18. Object Pooling

Because automatic weapons and shotguns may create decals frequently, avoid repeatedly creating and destroying GameObjects if possible.

Preferred architecture:

```text
BulletImpactDecalPool
```

The system should reuse decal objects.

Conceptually:

```text
Shot Fired
    ↓
Surface Hit
    ↓
Request Decal From Pool
    ↓
Position / Rotate / Scale
    ↓
Display Decal
    ↓
Lifetime Expires
    ↓
Return To Pool
```

This is particularly important for:

* AK automatic fire
* Shotgun pellet impacts
* Extended multiplayer firefights

---

# 19. Multiplayer Behavior

Bullet marks are cosmetic and should not require server-authoritative gameplay simulation.

However, players should ideally be able to see environmental bullet impacts caused by other players.

Use the simplest networking architecture compatible with the existing Netcode for GameObjects implementation.

Avoid synchronizing every decal as a persistent `NetworkObject` if doing so creates unnecessary network overhead.

Preferred behavior:

```text
Authoritative weapon shot / validated hit
          ↓
Impact information communicated
          ↓
Each client locally renders cosmetic decal
```

Relevant information may include:

```text
Impact position
Impact normal
Weapon / impact type
Random decal variant or seed
```

Do not significantly increase multiplayer bandwidth merely to maintain cosmetic bullet marks.

---

# 20. Late Joining Players

Late-joining players do **not** need to reconstruct bullet marks that existed before they joined the match.

Bullet impacts are temporary cosmetic events.

This intentionally avoids having to synchronize a history of environmental decals.

---

# 21. Weapon Definition Integration

Do not create large switch statements such as:

```csharp
if (weapon == pistol)
...
else if (weapon == ak)
...
else if (weapon == dmr)
...
```

Instead, integrate decal parameters into the existing weapon-definition/data architecture.

Conceptually:

```text
WeaponDefinition

Impact Decals
    Enabled
    Decal Scale
    Maximum Decal Distance
    Decal Variant Set
    Impact Pattern
    Max Impact Decals
```

Possible impact-pattern types:

```text
Single
PelletSpread
```

This should allow future weapons to participate simply by configuring their definition.

---

# 22. Suggested Architecture

Cursor may adjust names to match the current project structure.

Possible components:

```text
BulletImpactManager
BulletImpactDecal
BulletImpactDecalPool
SurfaceImpactSettings
WeaponImpactSettings
```

Example flow:

```text
Weapon Fires
      ↓
Existing Hit Detection
      ↓
RaycastHit
      ↓
Is Environmental Surface?
      ↓
Is Within Weapon Decal Range?
      ↓
Determine Impact Settings
      ↓
Get Decal From Pool
      ↓
Select Random Texture
      ↓
Position Against Surface Normal
      ↓
Apply Weapon-Specific Scale
      ↓
Display
      ↓
Expire / Return To Pool
```

Where possible, reuse the weapon's existing hit detection rather than performing an additional raycast solely for the decal system.

---

# 23. Inspector Configuration

Provide clear inspector fields wherever applicable.

Example:

```text
Surface Impact Decals

[x] Enable Bullet Decals

Decal Scale:
1.0

Maximum Decal Range:
60

Decal Variants:
    Element 0
    Element 1
    Element 2

Decal Lifetime:
45

Maximum Active Decals:
200
```

Shotgun-specific configuration may include:

```text
Impact Pattern:
Pellet Spread

Maximum Pellet Decals:
10
```

---

# 24. Initial Weapon Configuration

Approximate desired starting configuration:

| Weapon  | Pattern       | Relative Size | Relative Decal Range |
| ------- | ------------- | ------------: | -------------------- |
| Pistol  | Single        |          1.00 | Close–Medium         |
| AK      | Single        |          0.75 | Close–Medium         |
| DMR     | Single        |          1.00 | Close–Medium/Long    |
| Shotgun | Pellet Spread |     0.65–0.75 | Close                |
| Sniper  | Single        |     1.10–1.20 | Close–Very Long      |

Exact numeric distances should remain easy to tune after playtesting.

---

# 25. Impact Texture Setup

Create a clearly organized location where bullet-hole decal textures/materials can be assigned.

Suggested organization:

```text
Assets/
    VFX/
        BulletImpacts/
            Textures/
            Materials/
            Prefabs/
```

Do not require the final artwork for this ticket.

If necessary, use a temporary placeholder bullet-hole texture/decal during implementation.

The system should make it straightforward for us to replace the temporary visual later by dragging replacement textures/materials into the relevant configuration.

---

# 26. Debugging

Optionally provide a development/debug mode that can expose:

* Impact point
* Surface normal
* Weapon decal range
* Whether a decal was rejected because of distance
* Current active decal count

Debug visualization should not appear in production builds unless explicitly enabled.

---

# 27. Performance Requirements

The system must remain stable during sustained combat.

Specifically test:

### AK

Hold automatic fire against a nearby wall.

Verify:

* Decals spawn correctly.
* Existing shooting behavior remains responsive.
* No severe garbage collection spikes occur.

### Shotgun

Repeatedly fire shotgun blasts into nearby geometry.

Verify:

* Multiple pellet impacts appear.
* Decal count remains bounded.
* No runaway object creation occurs.

### Multiplayer

Multiple players fire simultaneously.

Verify:

* Cosmetic impact rendering does not meaningfully interfere with network gameplay.

---

# 28. Out of Scope

REQ-044 does **not** include:

* Bullet penetration
* Ricochets
* Material-dependent penetration
* Destructible walls
* Permanent map damage
* Blood decals
* Character wounds
* Gore
* Shell casings
* Muzzle flashes
* Bullet tracers
* Impact sound redesign
* Glass destruction
* Sparks or debris
* Sniper rifle implementation

These may interact with the impact system later but should remain separate systems.

---

# Acceptance Criteria

REQ-044 is complete when:

1. Shooting a valid nearby environmental surface produces a visible bullet-hole decal.
2. Bullet holes align correctly with walls, floors, ceilings, and angled surfaces.
3. Bullet-hole graphics can be selected randomly from an inspector-configurable collection.
4. Bullet holes disappear or are recycled after a configurable duration.
5. The number of simultaneously active decals is bounded.
6. The pistol produces a medium-sized single impact.
7. The DMR produces a medium-sized single impact.
8. The AK produces a smaller single impact.
9. The shotgun produces multiple small scattered pellet impacts.
10. The shotgun only produces decals at relatively short ranges.
11. The pistol and AK support close-to-medium decal distances.
12. The DMR supports close-to-medium/long decal distances.
13. The architecture already supports configuring a future sniper rifle.
14. The future sniper configuration can support slightly larger impacts and very-long-range decals.
15. Weapon-specific decal settings can be adjusted without code changes.
16. Bullet decals do not alter damage or physics.
17. Player characters do not receive ordinary environmental bullet-hole decals.
18. Bullseyes continue using their specialized damage/shatter system.
19. Rapid AK fire does not create unbounded GameObjects.
20. Repeated shotgun fire does not create unbounded decals.
21. Other multiplayer clients can see recent bullet impacts where reasonably practical.
22. Late joiners do not need historical decal synchronization.
23. Existing shooting, damage, networking, and weapon systems continue functioning normally.
24. Temporary decal art can easily be replaced with finalized artwork later.

---

# Desired Result

Gunfire should begin visibly affecting the battlefield.

After a firefight, nearby walls and surfaces should temporarily show evidence of where shots landed:

* Pistol and DMR rounds create clear medium-sized bullet holes.
* AK fire creates clusters of smaller holes.
* Shotgun blasts create recognizable scattered pellet patterns at close range.
* The future sniper rifle will create slightly larger impacts even at very long distances.

The system should feel responsive and satisfying while remaining lightweight, cosmetic, configurable, and ready for future weapon and surface-effect expansion.
