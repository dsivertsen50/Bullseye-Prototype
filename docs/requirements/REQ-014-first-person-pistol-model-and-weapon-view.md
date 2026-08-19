# REQ-014 — First-Person Pistol Model and Weapon View

## Summary

Add the newly imported pistol model:

```text
Assets/Prefabs/Weapons/Ruger 22.fbx
```

to the player's first-person view.

When a player spawns into the game, they should see the Ruger pistol positioned in front of their first-person camera like a conventional FPS weapon.

The pistol should:

* Appear only in the owning player's first-person view.
* Move and rotate naturally with the player's camera.
* Remain aligned with the player's existing reticle.
* Not interfere with the current hitscan shooting mechanics.
* Be positioned and scaled through easily adjustable Unity Inspector values or transforms.
* Establish a reusable foundation for future weapons, reloads, recoil, firing animations, and weapon switching.

This requirement is primarily a **visual weapon presentation ticket**.

The pistol does not need new gameplay behavior yet.

---

# Design Intent

Bullseye now has enough core mechanics to begin making the prototype feel like an actual first-person shooter.

The immediate objective is:

> The player should visibly hold a gun while aiming and shooting.

Currently, firing is functionally implemented, but there is no first-person weapon model reinforcing the shooting action.

Adding the pistol should make the prototype substantially easier to judge in terms of:

* Aim feel
* Camera feel
* Shooting feel
* Weapon scale
* Reticle placement
* Future recoil
* Future reload animations

The pistol should initially serve as the prototype's default weapon.

---

# Source Asset

Use the existing imported FBX:

```text
Assets/Prefabs/Weapons/Ruger 22.fbx
```

The implementation agent should inspect the FBX before modifying the player prefab.

Because imported FBX models often have unexpected:

* Scale
* Rotation
* Pivot location
* Forward axis
* Origin location

the pistol may require a parent transform to make placement easier.

---

# Preferred Hierarchy

Create a reusable first-person weapon mount associated with the local player's first-person camera.

Conceptually:

```text
Player
│
├── FirstPersonCamera
│      │
│      └── WeaponView
│             │
│             └── WeaponMount
│                    │
│                    └── Ruger 22
│
├── Bullseye
└── Existing Player Components
```

Exact object names may differ if the existing player hierarchy already contains appropriate equivalents.

The important design principle is:

> The weapon model should have its own adjustable transform beneath the first-person camera.

Do not directly modify the FBX asset itself merely to position the gun on screen.

---

# 1. First-Person Weapon Mount

Create a transform that represents where first-person weapons are attached.

Example:

```text
FirstPersonCamera
    └── WeaponView
         └── WeaponMount
```

The weapon mount should:

* Follow the camera position.
* Follow the camera rotation.
* Allow local position adjustment.
* Allow local rotation adjustment.
* Allow local scale adjustment if necessary.
* Be reusable for future weapons.

The pistol model should be placed underneath this transform.

---

# 2. Pistol Visible in First Person

When the local player spawns:

```text
Ruger 22
→ visible in lower portion of first-person view
```

The initial placement should resemble a conventional FPS pistol presentation.

For example, approximately:

```text
               RETICLE
                  +

             [target]


                    __
                   /  \
                  |gun |
                   \___\
```

The exact positioning will require visual tuning.

A good initial placement would likely place the pistol:

* Slightly right of center.
* Toward the bottom of the screen.
* Pointing approximately toward the center-screen reticle.
* Far enough down that it does not obscure the bullseye target excessively.

---

# 3. Do Not Require Hands Yet

This ticket does not require:

* Player hands
* Arms
* Gloves
* Full first-person body
* Weapon-holding animations

The pistol may appear by itself for the prototype.

This is acceptable.

A future ticket can introduce first-person arms if desired.

---

# 4. Local Player Only

The first-person weapon model should only be visible to the player who owns that player instance.

Example:

```text
Player 1 screen
→ Player 1 sees Player 1's pistol

Player 2 screen
→ Player 2 sees Player 2's pistol
```

Player 1 should not see Player 2's first-person pistol model floating in front of Player 2's camera.

Similarly:

```text
Player 2 should not see Player 1's first-person presentation model
```

The first-person pistol is a **local visual object**, not the opponent's world-space weapon representation.

---

# 5. Multiplayer Ownership

The implementation should respect existing network ownership.

For each spawned player:

```text
Is this player locally owned?
```

If yes:

```text
First-person pistol rendering = enabled
```

If no:

```text
First-person pistol rendering = disabled
```

The exact implementation may use:

* Ownership checks
* Local player initialization
* Renderer enable/disable
* Local-only instantiation
* Layers/camera culling
* Another architecture consistent with the existing project

The implementation agent should choose the cleanest approach.

---

# 6. Future Third-Person Weapon Model

This requirement should distinguish between:

### First-Person Weapon Model

What the player sees on their own screen.

and:

### World Weapon Model

What opponents see attached to another player's character.

REQ-014 only requires the **first-person weapon model**.

It is acceptable if other players currently do not visually see the pistol.

Future architecture may eventually look like:

```text
Player
│
├── FirstPersonWeapon
│      └── visible only to owner
│
└── WorldWeapon
       └── visible to other players
```

A third-person/world gun model can be added later when the player mesh is developed further.

---

# 7. Preserve Existing Hitscan Shooting

The pistol model is initially visual only.

Do not replace or substantially redesign the current hitscan firing logic as part of this requirement.

The existing sequence should remain:

```text
Fire input
    ↓
Existing hitscan logic
    ↓
Bullseye hit detection
    ↓
Damage
```

The newly visible pistol should accompany this behavior visually.

The weapon model does not need to spawn a projectile.

---

# 8. Preserve Reticle-Based Aim

The existing screen-center reticle should remain the authoritative aiming reference.

The shot should continue going where the reticle indicates.

Do not make the direction of the pistol mesh itself determine hit detection.

Conceptually:

```text
Camera / Reticle
      ↓
Hitscan direction
```

not:

```text
Pistol barrel transform
      ↓
Authoritative aim direction
```

at this stage.

This prevents visual model alignment from accidentally breaking aiming.

---

# 9. Visual Barrel Alignment

Although the pistol model should not determine the actual shot direction, it should visually appear to point approximately toward the reticle.

The implementation agent should adjust:

* Local position
* Local rotation
* Scale

until the pistol looks believable in first person.

Exact perfection is not required yet.

---

# 10. Model Scale

Inspect the imported FBX scale.

The pistol should appear approximately realistic relative to the player's first-person field of view.

If the imported model is:

* Extremely large
* Extremely small
* Rotated incorrectly

correct this through the weapon presentation hierarchy or appropriate import configuration.

Avoid modifying unrelated player scale values just to accommodate the gun.

---

# 11. Camera Near Clipping

Ensure the pistol remains visible and does not disappear because it is too close to the camera's near clipping plane.

If necessary, adjust:

* Weapon position
* Camera near clip plane within reasonable limits
* First-person rendering architecture

Do not make extreme camera changes that negatively affect the arena or other gameplay visuals.

---

# 12. Weapon Collision

The first-person pistol model should not physically collide with:

* The player
* Walls
* Other players
* Bullseyes
* Gameplay raycasts

unless explicitly required later.

The first-person presentation model is primarily visual.

If the FBX contains colliders or imported collision components that interfere with gameplay, disable or remove them from the first-person presentation instance.

---

# 13. Weapon Should Follow Camera

When the player:

* Looks left
* Looks right
* Looks up
* Looks down

the weapon should remain attached to the first-person view.

Expected:

```text
Camera rotates
    ↓
Weapon view rotates with camera
```

The pistol should not lag behind or remain oriented in world space.

Weapon sway is not required yet.

---

# 14. Player Movement

When the player:

* Walks
* Sprints
* Jumps
* Crouches

the pistol should remain correctly attached to the player's first-person camera.

No procedural bobbing or movement animation is required yet.

The initial weapon may remain rigidly attached to the camera.

---

# 15. Death Behavior

When the local player dies:

The pistol should not remain incorrectly visible if doing so conflicts with the existing death view.

Preferred behavior:

```text
Player dies
    ↓
First-person weapon hidden
    ↓
Respawn countdown
    ↓
Player respawns
    ↓
First-person weapon visible again
```

This should integrate with REQ-013.

During:

```text
3 → 2 → 1
```

the death screen should primarily show the death location rather than a floating gun.

---

# 16. Respawn Behavior

After respawn:

* The pistol should reappear.
* It should be correctly positioned.
* It should follow the newly active player camera.
* It should remain local to the owning player.
* Existing shooting should work immediately.

Repeated deaths and respawns should not create duplicate pistol models.

---

# 17. Health HUD Compatibility

The pistol should not obscure the bottom-left health display introduced in REQ-012.

When positioning the weapon, preserve clear visibility of:

* Health HUD
* Reticle
* Respawn overlay

The pistol will likely occupy the lower-right or lower-center-right portion of the display.

---

# 18. Current Controller Rumble

Existing fire rumble should continue functioning normally.

Expected:

```text
Trigger pulled
    ↓
Hitscan fired
+
Controller rumble
+
Visible pistol remains on screen
```

No new rumble functionality is required.

---

# 19. First-Person Rendering Considerations

If practical, structure the implementation so first-person weapon presentation could later use a dedicated rendering layer.

For example:

```text
FirstPersonWeapon layer
```

This could eventually allow:

* Different weapon field of view
* Prevention of weapon clipping
* First-person-only rendering
* Separate camera rendering

However, a dedicated weapon camera is **not required for REQ-014**.

The implementation should remain as simple as possible while leaving room for future improvement.

---

# 20. Prefab Handling

The imported FBX may be converted or wrapped into a Unity prefab if needed.

A possible structure:

```text
Assets/Prefabs/Weapons/
│
├── Ruger 22.fbx
└── Ruger22_FirstPerson.prefab
```

Creating a reusable pistol prefab is preferred if it makes future weapon configuration cleaner.

Do not destructively alter the source FBX asset unnecessarily.

---

# Future Weapon Architecture

REQ-014 should begin establishing the concept that the player has a current weapon presentation.

Eventually:

```text
Player Weapon System
        ↓
Current Weapon
        ↓
Weapon Data
        ├── Model
        ├── Fire Rate
        ├── Damage behavior
        ├── Magazine size
        ├── Reload time
        ├── Recoil
        ├── Rumble
        └── Audio
```

That larger system is **not** required yet.

For now:

```text
Default weapon
= Ruger 22 pistol
```

---

# Out of Scope

The following are outside the scope of REQ-014:

* Reloading
* Magazine capacity
* Reserve ammunition
* Weapon switching
* Multiple guns
* Fire-rate changes
* New damage values
* Weapon-specific damage
* Muzzle flash
* Shell casing ejection
* Gunshot audio redesign
* Firing animation
* Reload animation
* Recoil animation
* Procedural weapon sway
* Weapon bobbing
* Aim-down-sights
* First-person arms
* Third-person gun model
* Weapon pickup system
* Projectile bullets
* Bullet drop
* Muzzle-based hit detection
* Final weapon materials
* Final weapon textures
* Final lighting polish

These should be handled in subsequent tickets.

---

# Acceptance Criteria

* [ ] `Ruger 22.fbx` is successfully used as the first-person pistol model.
* [ ] The local player sees the pistol when gameplay begins.
* [ ] The pistol appears in a reasonable FPS position near the lower-right/lower-center portion of the screen.
* [ ] The pistol is scaled reasonably.
* [ ] The pistol is oriented approximately toward the center reticle.
* [ ] The pistol follows horizontal camera rotation.
* [ ] The pistol follows vertical camera rotation.
* [ ] The pistol remains attached during movement.
* [ ] The pistol remains attached during jumping.
* [ ] The pistol remains attached during crouching.
* [ ] Player 1 sees only Player 1's first-person pistol.
* [ ] Player 2 sees only Player 2's first-person pistol.
* [ ] Remote players do not see another player's first-person presentation model incorrectly floating in world space.
* [ ] The pistol does not interfere with hitscan detection.
* [ ] Existing reticle aiming remains accurate.
* [ ] The pistol does not physically block player movement.
* [ ] The pistol does not collide with the player's own bullseye.
* [ ] Existing shooting continues to work.
* [ ] Existing health/damage behavior continues to work.
* [ ] Existing controller rumble continues to work.
* [ ] The weapon does not obscure the health HUD excessively.
* [ ] The pistol is hidden appropriately during the death countdown.
* [ ] The pistol reappears correctly after respawn.
* [ ] Repeated respawns do not create duplicate pistol models.
* [ ] No new console errors are introduced.

---

# Testing Procedure

## Test 1 — Player 1 Spawn

Start Player 1.

Expected:

* Ruger pistol is visible.
* Pistol appears in first-person view.
* Reticle remains visible.
* Health HUD remains visible.

---

## Test 2 — Camera Movement

Look:

* Left
* Right
* Up
* Down

Expected:

The pistol remains consistently attached to the camera view.

---

## Test 3 — Movement

Walk around the arena.

Then:

* Jump
* Crouch
* Sprint if available

Expected:

The pistol remains correctly positioned.

No unintended world-space separation occurs.

---

## Test 4 — Shooting

Aim at Player 2's bullseye and fire.

Expected:

* Existing shot registers normally.
* Existing damage occurs.
* Existing rumble occurs.
* Pistol remains visible.
* Reticle still accurately represents the shot.

---

## Test 5 — Multiplayer Ownership

Run two-player multiplayer.

Expected:

### Player 1 screen

```text
Player 1 first-person pistol visible
```

### Player 2 screen

```text
Player 2 first-person pistol visible
```

Neither client should see another player's first-person weapon model floating incorrectly in front of that remote player.

---

## Test 6 — Health HUD

Take damage.

Expected:

* Health HUD remains readable.
* Pistol does not cover the health display.

---

## Test 7 — Death

Kill Player 1.

Expected:

```text
Player 1 dies
→ pistol hidden
→ death view remains
→ 3
→ 2
→ 1
```

---

## Test 8 — Respawn

Allow Player 1 to respawn.

Expected:

```text
Respawn
→ pistol appears
→ weapon positioned correctly
→ shooting works
```

Repeat multiple times.

No duplicate guns should appear.

---

## Test 9 — Both Players

Have both players move, aim, and fire simultaneously.

Expected:

Each player's first-person weapon presentation remains independent.

---

# Recommended Next Tickets

Once REQ-014 is working, the weapon system can begin becoming functional rather than merely visual.

The recommended sequence after this ticket is:

```text
REQ-014
First-person pistol model
        ↓
First firing recoil / weapon kick
        ↓
Magazine and ammunition
        ↓
Reload input and reload timing
        ↓
Reload animation
        ↓
Muzzle flash and gunshot audio
        ↓
Weapon-specific firing characteristics
        ↓
Additional weapons
```

I would avoid jumping immediately into multiple guns.

The more useful milestone is:

> **Make one pistol feel convincingly like a weapon.**

Once the Ruger has a visible model, firing feedback, ammunition, reload behavior, recoil, sound, and basic animation, Bullseye will have a much stronger baseline combat loop against which additional weapons can be designed.

# Prototype Design Intent

REQ-014 marks the transition from:

> **Functional shooting prototype**

toward:

> **Playable FPS prototype**

The pistol does not need to change how shooting works yet.

Its purpose is to visually connect the player's input, reticle, firing action, controller feedback, and eventual weapon mechanics into a coherent first-person experience.

The immediate success condition is simple:

```text
Spawn
→ see pistol
→ aim
→ fire
→ gun remains correctly presented
→ kill opponent
→ respawn
→ pistol returns
```

Once that feels correct, the next phase should focus on making the Ruger itself feel satisfying to fire.
