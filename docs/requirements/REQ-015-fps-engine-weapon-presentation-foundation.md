# REQ-015 — FPS Engine Weapon Presentation Foundation

## Status

Ready for implementation on:

`experiment/fps-engine-integration`

This requirement **supersedes the previous REQ-014 first-person pistol implementation**. The existing Bullseye first-person weapon setup is rudimentary and may be replaced or substantially refactored as part of this ticket.

---

## Background

Bullseye currently has a basic first-person Ruger pistol model mounted underneath the local player's camera.

The current implementation successfully proved that:

* a local-only first-person weapon can be displayed;
* remote players do not need to render another player's first-person weapon;
* the weapon can hide when the player dies;
* Bullseye shooting can remain camera-centered and independent of the visible weapon model.

However, the existing implementation has little weapon presentation polish.

The Cowsins FPS Engine reference project contains mature assets and implementations for:

* firing animations;
* reload animations;
* holster/unholster animations;
* inspect animations;
* weapon sway;
* recoil;
* weapon bob;
* aiming/ADS presentation;
* muzzle VFX;
* weapon SFX;
* shell/effect systems;
* weapon configuration.

An architectural audit determined that the complete Cowsins `WeaponController` runtime should **not** be imported into the Bullseye player because it is tightly coupled to the Cowsins single-player controller, static input system, health system, inventory system, interaction system, UI, and other dependencies.

Bullseye should instead build its own lightweight weapon presentation layer while reusing appropriate Cowsins presentation assets and patterns.

---

# Goal

Create a reusable **Bullseye-owned first-person weapon presentation system** using selected Cowsins FPS Engine assets and presentation concepts.

The immediate weapon remains the existing Ruger 22 model.

**Do not replace the Ruger with a Cowsins weapon model.**

The purpose of this requirement is to bring over the reusable parts of FPS Engine that make weapons feel polished, particularly:

* weapon animation infrastructure;
* firing animation;
* firing sounds;
* muzzle flash/VFX;
* configurable presentation data;
* a foundation for recoil, sway, ADS, reload, and other effects.

The resulting architecture should make it easy to:

1. tune the Ruger;
2. add additional weapons later;
3. reuse imported Cowsins animation/audio/VFX assets where compatible;
4. replace or modify any imported presentation behavior without depending on the complete Cowsins FPS controller.

---

# Core Architectural Rule

Cowsins may help determine:

> **How firing a weapon looks, sounds, and feels.**

Bullseye continues to determine:

> **Whether a shot hits another player and what gameplay effect that hit causes.**

These systems must remain separate.

---

# Existing Bullseye Systems That Remain Authoritative

Do not replace or redesign the following systems as part of this requirement:

* `NetworkObject`
* Netcode for GameObjects ownership
* `NetworkTransform`
* `PlayerMovement`
* `PlayerLook`
* `LocalPlayerInputBinding`
* `PlayerHealth`
* `BullseyeMover`
* `BullseyeTarget`
* `BullseyeDamageZones`
* `CapsuleBodySurface`
* server-authoritative death
* server-authoritative respawn
* health regeneration
* existing bullseye damage rules
* existing local multiplayer device assignment

The existing `PlayerShoot` raycast and damage path must also remain functionally authoritative during this requirement.

Current combat flow should remain conceptually:

```text
Local Fire Input
      ↓
PlayerShoot
      ↓
Bullseye camera-centered raycast
      ↓
BullseyeTarget
      ↓
PlayerHealth / server RPC
      ↓
Damage / death / respawn
```

Weapon presentation should be triggered alongside that system, not replace it.

---

# Cowsins Reference Project

Treat the FPS Engine project as **read-only source/reference material**.

Do not modify files in the FPS Engine reference project.

Relevant assets observed during architecture inspection include:

```text
Assets/Cowsins/Animations/
Assets/Cowsins/SFX/
Assets/Cowsins/VFX/
Assets/Cowsins/Materials/
Assets/Cowsins/ScriptableObjects/
Assets/Cowsins/Scripts/Effects/
Assets/Cowsins/Scripts/Weapons/
```

Particularly useful reference assets include:

* pistol firing sounds;
* reload sounds;
* holster/unholster sounds;
* muzzle VFX;
* weapon animation clips;
* pistol Animator Controller;
* recoil curves/settings;
* sway implementation;
* weapon presentation configuration.

---

# Third-Party Asset Organization

Any Cowsins assets copied into Bullseye should live under a clearly separated folder such as:

```text
Assets/
└── ThirdParty/
    └── Cowsins/
        ├── Animations/
        ├── Audio/
        ├── VFX/
        ├── Materials/
        └── Configuration/
```

Preserve `.meta` files when copying Unity assets that reference other imported assets.

Do not scatter Cowsins source assets throughout Bullseye-owned folders.

Bullseye-created integration scripts should **not** live inside `Assets/ThirdParty/Cowsins`.

Use a structure similar to:

```text
Assets/
└── Bullseye/
    └── Scripts/
        └── Weapons/
            ├── WeaponPresentationController.cs
            ├── WeaponPresentationConfig.cs
            └── related Bullseye-owned scripts
```

If the project currently uses a different Bullseye script folder convention, follow the existing convention instead.

---

# Do Not Import Cowsins Weapon Models

Do not copy or use Cowsins weapon meshes/models for gameplay presentation in this ticket.

In particular, do not replace the existing Ruger model with:

* `Pistol_WeaponObject`
* Cowsins pistol meshes;
* Cowsins rifle;
* Cowsins SMG;
* Cowsins shotgun;
* Cowsins arms rig.

The existing:

`Ruger 22.fbx`

should remain the visible first-person firearm for this ticket.

The system should be designed so another mesh can easily replace it later.

---

# Animation Requirement

Create a reusable weapon animation architecture.

The current static Ruger presentation may be restructured or replaced.

The weapon presentation system should support animation states/events for at least:

* Idle
* Fire
* Aim/ADS
* Reload
* Holster
* Unholster

Only **Fire** must be fully functional in this ticket.

The other states should have clean architectural support for later requirements.

## Cowsins Animation Assets

Inspect Cowsins weapon animation assets, particularly the pistol Animator Controller and related clips.

Reuse Cowsins animation assets where they can operate correctly with the Bullseye Ruger model.

Do **not** force an animation clip onto the Ruger if it relies on:

* Cowsins-specific bones;
* the Cowsins FPS arms rig;
* missing transforms;
* `ParentConstraint` targets that do not exist;
* Cowsins weapon hierarchy assumptions.

If Cowsins clips cannot directly animate the Ruger:

1. retain the imported assets for reference/use on compatible future weapons;
2. reproduce the relevant presentation behavior using Bullseye-owned transforms/animations;
3. use Cowsins timing, structure, and presentation patterns as guidance.

The implementation should favor a maintainable Bullseye animation system over forcing incompatible Cowsins rigging to work.

---

# Weapon Presentation Controller

Create a Bullseye-owned weapon presentation component.

Suggested name:

`WeaponPresentationController`

Exact name may vary if another name better fits existing conventions.

It should be responsible only for local presentation.

Possible responsibilities include:

* identifying the local weapon root;
* accessing its Animator;
* firing the weapon animation;
* playing the firing sound;
* spawning or triggering muzzle VFX;
* exposing hooks for future reload;
* exposing hooks for future ADS;
* exposing hooks for future recoil;
* exposing hooks for future sway;
* hiding/showing presentation when appropriate.

It must **not**:

* raycast for player damage;
* apply health damage;
* call Cowsins `IDamageable`;
* manage NGO ownership;
* determine deaths;
* determine respawns;
* own player movement;
* poll Cowsins input;
* manage weapon inventory.

---

# Firing Integration

When the owner fires using the existing Bullseye Fire action:

```text
Fire Input
    ↓
PlayerShoot
    ├── Existing Bullseye shot logic
    │
    └── WeaponPresentationController
            ├── Fire animation
            ├── Fire sound
            └── Muzzle effect
```

The visual/audio presentation should occur immediately for the local player.

Presentation should occur even if the shot misses.

Hitmarker behavior remains dependent on an actual valid Bullseye hit.

---

# Fire Animation

Each trigger pull should produce visible first-person weapon motion.

At minimum:

* weapon kicks backward/upward or otherwise visibly reacts;
* animation completes quickly enough to feel responsive;
* weapon returns cleanly toward its resting position;
* repeated shots should restart or blend appropriately.

Use Cowsins animation assets where compatible.

If they are incompatible with the Ruger hierarchy, implement equivalent animation using:

* an Animator;
* procedural transform animation;
* or another simple reusable local presentation approach.

Do not couple firing animation to Cowsins `WeaponController`.

---

# Fire Sound

Reuse an appropriate Cowsins pistol firing sound.

The weapon should play a firing sound whenever the local player fires.

For this first implementation:

* an `AudioSource` on or near the local first-person weapon is acceptable;
* Cowsins `SoundManager` is **not required**;
* Cowsins `PoolManager` is **not required**.

Do not import a large manager hierarchy simply to play one sound.

Architecture should permit weapon-specific sounds later.

---

# Muzzle VFX

Reuse an appropriate Cowsins muzzle flash effect if possible.

The effect should appear at a configurable muzzle transform on the Ruger.

Create or identify something like:

```text
WeaponMount
└── Ruger22
    └── MuzzlePoint
```

Exact hierarchy may vary.

The muzzle point should be assignable in the Inspector.

If the imported Cowsins VFX uses Built-in Render Pipeline materials that do not render correctly in Bullseye HDRP:

* duplicate/convert only the assets needed for this effect;
* use HDRP-compatible materials/shaders;
* do not change Bullseye away from HDRP.

No magenta/pink materials are acceptable in the final result.

---

# Presentation Configuration

Create a weapon-specific configuration mechanism so that future weapons do not require rewriting the presentation script.

This may be:

* a Bullseye ScriptableObject;
* a serializable config class;
* or another clean inspector-driven approach.

Preferred direction: a Bullseye-owned ScriptableObject such as:

`WeaponPresentationConfig`

Possible fields:

```text
Weapon Name
Fire SFX
Reload SFX
Muzzle VFX
Fire Animation State
Reload Animation State
Fire Animation Speed
Muzzle Transform
Recoil settings
Sway settings
ADS position
ADS rotation
```

Not all fields need to be actively used in this requirement.

The purpose is to establish the data model now so additional weapons can later be created primarily through configuration rather than custom code.

Cowsins `Weapon_SO` may be studied and reused conceptually.

Do not create a dependency on the entire Cowsins weapon runtime solely to use `Weapon_SO`.

A smaller Bullseye-owned configuration is preferred if it reduces dependencies.

---

# Local-Only Presentation

First-person weapon presentation must only run for the owning player.

For a two-client multiplayer test:

Player 1:

* sees Player 1's first-person Ruger;
* sees Player 1's fire animation;
* hears Player 1's local weapon fire appropriately.

Player 2:

* sees Player 2's first-person Ruger;
* sees Player 2's fire animation;
* hears Player 2's local weapon fire appropriately.

A client must not see another player's first-person weapon mounted inside their own camera.

Remote-player third-person weapon presentation is out of scope.

---

# Death and Respawn

The weapon must integrate with the existing Bullseye death state.

When the local player dies:

* hide the first-person weapon;
* do not allow local weapon presentation to continue firing during the death state.

After respawn:

* show/reset the first-person weapon;
* return animation state to a valid default;
* clear any transient muzzle effects;
* allow firing again.

Existing death and respawn networking must remain unchanged.

---

# Recoil Foundation

Cowsins contains a more sophisticated recoil architecture using configurable curves.

Do not import `CameraLookBehaviour`.

Do not allow another component to take ownership of Bullseye camera look.

However, design the new presentation system so recoil can be added next without substantial restructuring.

It should be possible for a future requirement to apply:

```text
Weapon Presentation
       ↓
Recoil request
       ↓
Bullseye PlayerLook
       ↓
Local pitch/yaw offset
```

Camera recoil is not required for acceptance in this ticket unless it can be added safely and trivially.

Simple weapon-model kick during the firing animation **is required**.

---

# Weapon Sway Foundation

Cowsins' `WeaponSpecificEffects` may be inspected and selectively adapted.

Full sway is not required for completion.

However, the weapon hierarchy should include a sensible transform chain that permits future effects.

Recommended conceptual hierarchy:

```text
Camera
└── WeaponView
    └── WeaponEffectsRoot
        └── WeaponMount
            └── Ruger22
                └── MuzzlePoint
```

This permits future independent manipulation of:

* locomotion bob;
* look sway;
* recoil;
* ADS offset;
* raw model alignment.

The exact hierarchy may differ if Cursor identifies a better implementation.

---

# ADS Foundation

Keep `PlayerAimZoom` as the only component that currently owns camera FOV.

Do not import Cowsins `CameraFOVManager`.

This ticket does not need polished ADS weapon positioning.

However, architecture should allow a later ticket to interpolate the weapon toward configurable:

* ADS local position;
* ADS local rotation.

The system should not require another major hierarchy rewrite to add ADS.

---

# Reload Foundation

No functional ammunition system currently exists in Bullseye.

Do not add ammo gameplay in this requirement.

Do not block shooting based on Cowsins magazine state.

However:

* import/use reload animation assets where useful;
* import/use reload SFX where useful;
* expose a presentation method or hook for future reload animation.

For example:

```csharp
PlayReloadPresentation()
```

The exact API may differ.

Do not bind a Reload input action in this ticket unless required by implementation.

---

# Cowsins Systems That Must Not Be Added to the Bullseye Player

Do not attach or activate:

* `CowsinsFPSController`
* Cowsins `PlayerMovement`
* Cowsins `PlayerStates`
* Cowsins `PlayerStats`
* Cowsins `PlayerControl`
* Cowsins `InputManager`
* Cowsins static `PlayerActions` runtime path
* Cowsins `WeaponController`
* Cowsins `WeaponStates`
* Cowsins `HitDetectionSystem`
* Cowsins `HitscanShootStyle`
* Cowsins `ProjectileShootStyle`
* Cowsins `InteractManager`
* Cowsins pickups
* Cowsins `UIController`
* Cowsins `PauseMenu`

Do not add Cowsins `IDamageable` to the Bullseye player.

---

# Tags and Layers

Do not copy or execute the Cowsins `TagLayerInitializationManager`.

Bullseye already uses:

`Layer 6 = FirstPersonWeapon`

Keep this layer.

Do not automatically create Cowsins:

* `Weapons`
* `Enemy`
* `Critical`
* `BodyShot`

tags/layers simply because they exist in the FPS Engine.

The Bullseye bullseye component remains the sole intended vulnerable target.

---

# Project Settings

Do not overwrite Bullseye:

* `ProjectSettings`
* `GraphicsSettings`
* `QualitySettings`
* `DynamicsManager`
* Input configuration
* HDRP settings
* physics settings

Do not copy FPS Engine's complete project configuration.

Bullseye remains HDRP.

---

# Rendering

Any imported Cowsins asset used at runtime must render correctly in HDRP.

Check:

* Ruger materials;
* muzzle effect materials;
* particle shaders;
* any weapon animation helper graphics.

Convert only the required imported materials.

Do not run a broad project-wide render pipeline conversion without explicit need.

---

# Existing Ruger Setup

The current Ruger implementation is **not protected architecture**.

Cursor may:

* restructure `WeaponView`;
* replace `FirstPersonWeaponView`;
* alter the current weapon mount hierarchy;
* replace the existing simple presentation script;
* reposition or rescale the Ruger;
* create new presentation components.

However:

* `Ruger 22.fbx` should remain the visible gun model;
* weapon presentation must remain owner-only;
* existing Bullseye combat behavior must remain working.

Do not preserve obsolete code solely for backward compatibility if the new architecture makes it unnecessary.

---

# Expected User Experience

When entering the game:

1. Player sees the Ruger naturally positioned in first-person view.
2. Weapon remains attached correctly while moving and looking.
3. Pressing Fire immediately:

   * fires through the existing Bullseye shooting system;
   * visibly animates the weapon;
   * plays a pistol firing sound;
   * displays a muzzle flash.
4. Shooting another player's bullseye still causes the correct Bullseye damage.
5. Shooting ordinary body geometry still does no damage unless existing Bullseye rules say otherwise.
6. Death hides the weapon.
7. Respawn restores the weapon.
8. Multiplayer remains functional.

---

# Acceptance Criteria

## Compilation

* [ ] Project compiles with no new errors.
* [ ] No Cowsins complete-player runtime is required for compilation.
* [ ] No missing Cowsins singleton produces runtime exceptions.

## Weapon model

* [ ] Existing Ruger 22 remains the first-person weapon model.
* [ ] No Cowsins weapon model is used as the visible gun.
* [ ] Ruger scale/rotation/position are reasonable in first-person view.

## Presentation

* [ ] Fire produces visible weapon animation/motion.
* [ ] Fire plays a Cowsins-derived pistol firing sound.
* [ ] Fire produces a muzzle flash/VFX.
* [ ] Muzzle VFX renders correctly in HDRP.
* [ ] No magenta/pink runtime materials remain.

## Combat

* [ ] Existing `PlayerShoot` gameplay hit logic still works.
* [ ] Valid bullseye hits still register.
* [ ] Head/torso/lower bullseye damage remains unchanged.
* [ ] Body misses/non-bullseye hits remain non-damaging.
* [ ] No Cowsins `IDamageable` path applies player damage.
* [ ] Existing hitmarker still functions.
* [ ] Existing firing haptics still function.

## Multiplayer

* [ ] Host can fire normally.
* [ ] Client can fire normally.
* [ ] Each player only sees their own first-person Ruger.
* [ ] Importing presentation features does not break per-controller input binding.
* [ ] Cowsins `InputManager` is not enabled.
* [ ] NGO ownership behavior remains unchanged.

## Death/respawn

* [ ] Weapon hides on death.
* [ ] Weapon resets correctly on respawn.
* [ ] Weapon reappears after respawn.
* [ ] Respawn countdown remains functional.

## Architecture

* [ ] Cowsins source/reference project remains untouched.
* [ ] Imported third-party assets are clearly separated.
* [ ] Bullseye integration code is Bullseye-owned.
* [ ] FPS Engine `WeaponController` is not attached to the Bullseye network player.
* [ ] FPS Engine `PlayerStats` is not attached.
* [ ] FPS Engine player movement is not attached.
* [ ] Cowsins tags/layers initializer is not imported/executed.
* [ ] Bullseye remains HDRP.

---

# Manual Playtest

After implementation, test with two local multiplayer clients.

### Test 1 — Weapon appearance

For both players:

* spawn;
* confirm Ruger is visible;
* look around;
* walk;
* sprint;
* crouch;
* jump.

Confirm weapon remains stable and appropriately positioned.

### Test 2 — Fire presentation

Fire repeatedly.

Confirm:

* weapon moves;
* firing sound plays;
* muzzle effect appears;
* presentation responds immediately.

### Test 3 — Bullseye hit

Player 1 fires at Player 2's bullseye.

Confirm:

* presentation plays;
* hitmarker appears;
* health decreases according to zone;
* damage remains server-controlled.

Repeat from Player 2.

### Test 4 — Non-bullseye hit

Shoot Player 2's body away from the bullseye.

Confirm no Bullseye health is removed.

### Test 5 — Death

Kill Player 2.

Confirm:

* Player 2 weapon disappears;
* existing respawn countdown occurs;
* weapon returns after respawn.

### Test 6 — Input independence

Use the current two-controller testing workflow.

Confirm importing Cowsins presentation assets did not change controller assignment behavior.

---

# Deliverables

Implementation should include:

1. Reusable Bullseye weapon presentation architecture.
2. Existing Ruger integrated into that architecture.
3. Cowsins-derived firing sound.
4. Cowsins-derived muzzle VFX.
5. Functional firing presentation animation.
6. Foundation for future:

   * recoil;
   * sway;
   * ADS;
   * reload;
   * weapon-specific configuration.
7. Clearly organized copied Cowsins presentation assets.
8. No dependency on the Cowsins complete player/controller stack.

---

# Agent Instructions

Before modifying the project:

1. Read:

   * `docs/analysis/FPS_ENGINE_INTEGRATION_AUDIT.md`
   * the previous REQ-014 if it is still present;
   * existing weapon presentation code;
   * `PlayerShoot`;
   * `PlayerLook`;
   * `PlayerHealth`;
   * `FirstPersonWeaponView`;
   * `PlayerNetworkSetup`;
   * `LocalPlayerInputBinding`.

2. Inspect the relevant Cowsins animation, SFX, and VFX assets in the read-only FPS Engine reference project.

3. Determine the smallest asset subset required for this requirement.

4. Do not solve missing dependencies by copying large sections of the Cowsins runtime.

If a Cowsins presentation asset depends on a large Cowsins gameplay subsystem, recreate the small presentation behavior in Bullseye-owned code instead.

Prefer:

> small, explicit Bullseye code + reusable Cowsins assets

over:

> importing the Cowsins player framework just to gain an effect.

---

# Out of Scope

This requirement does **not** add:

* Cowsins weapon models;
* Cowsins first-person arms;
* new playable weapons;
* ammo;
* magazine limits;
* reload gameplay;
* weapon pickups;
* weapon switching;
* loadouts;
* attachments;
* Cowsins health;
* Cowsins movement;
* Cowsins hitscan damage;
* Cowsins inventory;
* Cowsins UI;
* third-person weapon models;
* networked muzzle effects;
* server-side hit validation;
* lag compensation;
* shell casing physics;
* penetration;
* bullet trails;
* full camera recoil;
* full weapon sway;
* full weapon bob;
* polished ADS.

Those may be addressed incrementally after this weapon presentation foundation is stable.

---

## Definition of Done

REQ-015 is complete when **the existing Ruger remains functionally a Bullseye weapon but begins to feel like a polished FPS weapon**:

```text
Ruger 22
   +
Cowsins-derived animation/audio/VFX
   +
Bullseye-owned presentation system
   +
existing Bullseye combat/network architecture
```

The game should still function exactly as Bullseye from a gameplay-authority perspective, while establishing a reusable presentation architecture that can be extended rather than rebuilt as additional weapons and weapon mechanics are introduced.
