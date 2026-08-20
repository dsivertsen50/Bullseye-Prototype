# REQ-019 — Third-Person Weapon Representation and Networked Fire Presentation

## Status

Ready for implementation on:

`experiment/fps-engine-integration`

## Background

Bullseye now has a substantially improved first-person experience.

Recent requirements have established:

* first-person Ruger presentation;
* firing animation/audio/VFX;
* Cowsins-derived movement;
* camera breathing/bob/sprint effects;
* world-space player health bars;
* Pause/Settings UI;
* controller menu navigation;
* sprint speed-line effects.

However, weapon presentation is still primarily local.

The local player sees their first-person gun, but other players do not yet have a proper multiplayer/world representation of that weapon and its actions.

This creates a major visual gap in multiplayer.

A remote player should be able to look at another player and understand:

* that they are holding a gun;
* where that gun roughly points;
* when they fire;
* when they eventually reload;
* whether they are alive/dead.

This requirement establishes that architecture.

---

# Goal

Create separate:

1. **First-person weapon presentation**
2. **Third-person/world weapon presentation**

for every Bullseye player.

The local player should continue seeing their existing first-person Ruger.

Other players should see a separate world-space Ruger attached to that player's body.

When the player fires:

* local first-person firing presentation occurs immediately;
* remote clients see the world weapon fire;
* world muzzle VFX/audio may play;
* existing Bullseye hit/damage authority remains unchanged.

---

# Core Architectural Principle

A weapon should have two visual representations:

```text
WEAPON GAMEPLAY STATE
        │
        ├──────────────► FIRST-PERSON PRESENTATION
        │                 local owner only
        │
        └──────────────► WORLD PRESENTATION
                          visible to other players
```

Neither visual representation should determine damage.

Damage remains controlled by the existing Bullseye shooting system.

---

# Desired Player Hierarchy

Restructure the player hierarchy where necessary so the two representations are clearly separated.

Conceptually:

```text
Player
│
├── Body
│   ├── Capsule / Future Character Mesh
│   │
│   ├── WorldWeaponRoot
│   │   └── Ruger22_World
│   │
│   ├── Bullseye
│   └── WorldHealthUI
│
└── LocalPresentation
    └── CameraRoot
        └── Camera
            └── WeaponView
                └── Ruger22_FirstPerson
```

Exact naming may differ.

The important distinction is:

```text
Ruger22_FirstPerson
≠
Ruger22_World
```

They may use the same source model but should be separate instances/prefabs.

---

# First-Person Weapon

Preserve the existing local first-person weapon presentation.

The owner should continue seeing:

* Ruger model;
* firing animation;
* firing SFX;
* muzzle VFX;
* existing weapon movement/effects.

The first-person weapon should remain:

* local-only;
* camera-relative;
* hidden from remote clients;
* independent of world character pose.

Do not make remote clients render another player's first-person weapon hierarchy.

---

# World Weapon

Create a separate world-space Ruger representation.

This weapon should:

* exist underneath the networked player body;
* move with the player;
* rotate appropriately with the player's world orientation;
* be visible to remote clients;
* not obstruct the local player's first-person camera.

For the initial implementation, it may also exist for the local owner but should be hidden from that owner's first-person camera if necessary.

---

# Model Reuse

Use the existing:

`Ruger 22.fbx`

for the world weapon.

Do not introduce a new Cowsins weapon model.

Create separate prefabs/configuration if appropriate:

```text
Ruger22_FirstPerson
Ruger22_World
```

The two presentations may use:

* different scale;
* different materials;
* different offsets;
* different animation approach.

---

# Temporary Body Attachment

Bullseye currently uses a simple prototype body rather than a final humanoid character.

For now, attach the world weapon to a sensible transform on the current body.

Create something conceptually like:

```text
Player
└── Body
    └── WeaponHandAnchor
        └── Ruger22_World
```

The placement does not need to look anatomically perfect on the current capsule.

It should simply communicate:

> This player is holding a firearm.

The architecture must make it straightforward to re-parent:

`WeaponHandAnchor`

to a proper hand bone when humanoid character models are introduced.

---

# Future Character Compatibility

Do not hardcode the world weapon system to capsule geometry.

The eventual desired hierarchy may become:

```text
CharacterRig
└── RightHand
    └── WeaponHandAnchor
        └── Ruger22_World
```

Therefore:

* keep weapon offsets configurable;
* isolate attachment logic;
* avoid assumptions tied permanently to the prototype capsule.

---

# World Weapon Visibility

Expected behavior:

## Player 1

Player 1 sees:

* Player 1 first-person Ruger;
* Player 2 world Ruger.

Player 1 should generally **not** see their own world Ruger through the first-person camera.

## Player 2

Player 2 sees:

* Player 2 first-person Ruger;
* Player 1 world Ruger.

---

# Layering

Use separate layers if helpful.

Conceptually:

```text
FirstPersonWeapon
WorldWeapon
```

The first-person camera may exclude:

`WorldWeapon`

for the owning player if necessary.

Remote cameras should see world weapons normally.

Do not reuse a layer scheme that causes:

* world gun disappearing for everyone;
* first-person gun appearing on remote clients;
* duplicate guns visible in first-person.

---

# Fire State Synchronization

When the owner fires:

```text
Owner Fire Input
       ↓
PlayerShoot
       ├── Bullseye hit logic
       │
       ├── local first-person fire presentation
       │
       └── replicated fire presentation event
                    ↓
              remote clients
                    ↓
          world weapon fire presentation
```

The replicated event should communicate only:

> A shot was fired.

It should **not** apply damage.

---

# Networking

Use an appropriate NGO mechanism for fire presentation.

Possible approaches:

* RPC;
* networked event;
* another lightweight network signal.

Do not use a continuously synchronized NetworkVariable simply to represent a momentary trigger unless necessary.

Preferred behavior:

```text
Owner fires
    ↓
immediate local presentation
    ↓
server/network event
    ↓
remote world fire presentation
```

Local first-person firing should not wait for the network round trip.

---

# No Duplicate Damage

This is critical.

The remote world weapon firing presentation must **never**:

* perform another damaging raycast;
* call `BullseyeTarget.TryRegisterHit`;
* call `PlayerHealth`;
* invoke Cowsins `IDamageable`;
* create duplicate hit registration.

Only the existing authoritative Bullseye shooting path should determine hits.

World weapon firing is presentation-only.

---

# World Fire Animation

When a remote player fires, their world weapon should visibly react.

For this prototype, acceptable behavior includes:

* slight weapon recoil;
* short firing animation;
* transform kick;
* Animator state.

Reuse/adapt existing firing presentation where useful.

Do not require a full humanoid animation rig yet.

The immediate goal is simply:

> Other players can visually recognize that someone fired.

---

# World Muzzle Flash

Add a world-space muzzle point.

Conceptually:

```text
Ruger22_World
└── MuzzlePoint
```

When the player fires:

* spawn/play muzzle VFX;
* orient VFX properly;
* show the effect to remote players.

The owner does not need to see this world muzzle flash if the first-person version already plays.

Avoid creating two overlapping muzzle flashes in the owner's camera.

---

# World Gunshot Audio

Remote players should be able to hear another player's gunfire.

The current local weapon SFX may remain as-is.

Add world-space gunshot playback associated with the firing player's world position.

Preferred behavior:

```text
Local owner
→ immediate first-person/local gunshot

Remote players
→ spatial/world gunshot
```

Use a 3D `AudioSource` or another simple spatial audio approach.

Do not introduce the entire Cowsins `SoundManager` dependency unless genuinely useful.

---

# Audio Duplication

Avoid the owner hearing two identical full-volume gunshots simultaneously from:

1. first-person audio;
2. world weapon audio.

Possible approaches include:

* do not play world audio for the firing owner;
* reduce/mute owner world audio;
* have one unified audio event.

Choose the cleanest implementation.

---

# Weapon Direction

The world weapon should generally point in the direction the player appears to be aiming.

At minimum:

* horizontal/yaw orientation should track player orientation.

Preferred:

* vertical pitch should also influence the world weapon.

Current networking primarily represents body/root yaw while first-person camera pitch is local.

If practical, synchronize a lightweight aiming pitch value.

---

# Networked Aim Pitch

If necessary, introduce a synchronized value such as:

```text
AimPitch
```

representing the player's current vertical look angle.

Requirements:

* owner writes/sends their aim pitch;
* remote players read it;
* world weapon uses it to pitch up/down;
* first-person camera remains local-only.

Do not network the complete camera transform.

Conceptually:

```text
Owner Camera Pitch
       ↓
Network AimPitch
       ↓
Remote WorldWeaponRoot
       ↓
gun points up/down
```

---

# Aim Pitch Bandwidth

Do not spam unnecessary RPCs every rendered frame.

If aim pitch is networked:

* use appropriate NGO synchronization;
* throttle/update sensibly;
* allow interpolation.

This is visual state, not critical combat authority.

---

# Remote Weapon Recoil

World recoil should be cosmetic.

When the fire event occurs:

```text
WorldWeaponRoot
       ↓
brief kick
       ↓
smooth return
```

This should not alter the actual player orientation or Bullseye hit calculation.

---

# Sprint / Movement Compatibility

The world gun should remain attached correctly while the player:

* walks;
* sprints;
* jumps;
* crouches;
* turns.

No polished third-person locomotion animation is required yet.

The weapon may move rigidly with the body for now.

---

# Crouch

When a player crouches:

* the world weapon should move with the player's body/crouched stance;
* it should not remain floating at standing height.

If current crouch representation already moves/scales the body hierarchy appropriately, parent the weapon accordingly.

---

# Death

When the player dies:

* hide or disable the world weapon.

Do not leave a floating weapon at the death position unless intentionally desired later.

For this prototype:

**hide the world weapon while dead.**

---

# Respawn

When the player respawns:

* restore the world weapon;
* reset recoil/animation state;
* ensure position/orientation is correct.

---

# First-Person Death/Respawn

Preserve existing behavior:

* first-person weapon hides on death;
* returns after respawn.

Both presentations should respond to the same underlying player life state.

---

# Shared Weapon Presentation Events

Preferred architecture should avoid directly coupling every network event to specific Ruger objects.

Create a reusable presentation concept such as:

```text
WeaponPresentationEvents
```

or equivalent.

Possible events:

```text
OnFire
OnReload
OnAimChanged
OnWeaponChanged
```

Only Fire must be implemented in this requirement.

Architecture should allow future REQ tickets to add:

* Reload;
* weapon switching;
* melee;
* ADS;
* different weapon types.

---

# Future Reload Compatibility

REQ-019 does not implement functional reload gameplay.

However, world weapon architecture should permit later behavior:

```text
Player reloads
       ↓
first-person reload animation
       +
network reload event
       ↓
remote world weapon reload animation
```

Do not hardwire the system exclusively around `Fire`.

---

# Future Weapon Switching Compatibility

Likewise, future weapons should be able to define:

```text
Weapon ID
FirstPersonPrefab
WorldPrefab
PresentationConfig
```

Do not build the architecture so `Ruger22` is permanently hardcoded everywhere.

A simple weapon configuration structure is preferred.

---

# Cowsins Usage

Inspect the Cowsins FPS Engine for useful patterns/assets regarding:

* third-person weapon representation if available;
* fire effects;
* muzzle VFX;
* audio;
* animation events.

Reuse assets where useful.

Do not import:

* Cowsins player controller;
* Cowsins damage;
* Cowsins health;
* Cowsins hitscan;
* Cowsins static input;
* Cowsins complete weapon runtime;

solely to accomplish third-person presentation.

---

# Existing First-Person System

Cursor may refactor REQ-014 implementation if doing so creates a cleaner shared system.

However, preserve existing working behavior.

The local player must continue to have:

* weapon animation;
* SFX;
* muzzle VFX;
* correct ownership visibility.

---

# Suggested Architecture

Conceptually:

```text
Player
│
├── WeaponState / WeaponPresentationCoordinator
│
├── Body
│   └── WorldWeaponRoot
│       └── Ruger22_World
│
└── LocalPresentation
    └── Camera
        └── FirstPersonWeaponRoot
            └── Ruger22_FirstPerson
```

Then:

```text
PlayerShoot.Fire()
        ↓
WeaponPresentationCoordinator.Fire()
        │
        ├── Owner
        │      ↓
        │  FirstPersonWeapon.Fire()
        │
        └── Network
               ↓
          Remote clients
               ↓
          WorldWeapon.Fire()
```

The exact class structure may differ.

---

# Manual Playtest

## Test 1 — Player 1 View

Spawn two clients.

From Player 1:

Confirm:

* Player 1 sees their normal first-person gun;
* Player 2 has a visible world-space Ruger;
* Player 1 does not see duplicate guns on themselves.

---

## Test 2 — Player 2 View

Repeat from Player 2.

Confirm reciprocal behavior.

---

## Test 3 — Remote Firing

Player 2 fires while Player 1 watches.

Player 1 should see:

* Player 2 world weapon recoil/animate;
* muzzle flash;
* weapon remain attached correctly.

---

## Test 4 — Remote Audio

Player 2 fires near Player 1.

Confirm Player 1 hears world gunfire.

Move farther away if spatial audio is implemented.

Confirm sound behaves reasonably with distance.

---

## Test 5 — Existing Damage

Player 2 shoots Player 1's bullseye.

Confirm:

* world fire presentation occurs;
* only one hit is registered;
* correct health damage occurs;
* existing hitmarker behavior remains.

---

## Test 6 — Miss

Player 2 fires and misses.

Confirm:

* world firing presentation still occurs;
* no damage occurs.

---

## Test 7 — Aim Direction

Player 2 looks:

* up;
* forward;
* down.

If AimPitch synchronization was implemented, confirm world weapon reasonably follows pitch.

---

## Test 8 — Movement

Observe remote player while:

* walking;
* sprinting;
* jumping;
* crouching.

Confirm world gun remains attached.

---

## Test 9 — Death

Kill the remote player.

Confirm:

* first-person weapon hides for victim;
* remote world weapon hides;
* health bar/death state remains correct.

---

## Test 10 — Respawn

Confirm after respawn:

* world weapon returns;
* first-person weapon returns;
* no stale recoil/animation state remains.

---

## Test 11 — Simultaneous Fire

Both players fire repeatedly.

Confirm:

* each sees correct first-person presentation;
* each sees the other's world firing;
* effects do not cross-control;
* no duplicate hit events appear.

---

# Acceptance Criteria

## First-Person

* [ ] Local first-person Ruger remains working.
* [ ] First-person firing animation remains working.
* [ ] Local muzzle VFX remains working.
* [ ] Local firing SFX remains working.
* [ ] First-person weapon remains owner-only.

## World Weapon

* [ ] Remote player has a visible Ruger.
* [ ] World Ruger follows player movement.
* [ ] World Ruger follows player yaw.
* [ ] World Ruger does not obstruct local first-person view.
* [ ] World weapon hides on death.
* [ ] World weapon restores on respawn.

## Networked Fire Presentation

* [ ] Remote clients receive a firing presentation event.
* [ ] Remote world gun visibly fires.
* [ ] Remote muzzle flash works.
* [ ] Remote/world gunshot audio works.
* [ ] Owner receives immediate local presentation without waiting for networking.

## Combat Safety

* [ ] Network fire presentation does not apply damage.
* [ ] Existing `PlayerShoot` remains the hit path.
* [ ] Bullseye damage remains correct.
* [ ] No duplicate damage occurs.
* [ ] Cowsins `IDamageable` is not used.

## Aim

* [ ] World gun orientation reasonably represents player facing direction.
* [ ] Aim pitch synchronization is implemented if practical.
* [ ] Aim pitch does not affect first-person camera ownership.

## Existing Features

* [ ] REQ-014 weapon presentation remains working.
* [ ] REQ-016 movement remains working.
* [ ] REQ-017 camera/health/settings remain working.
* [ ] REQ-018 controller navigation and sprint effects remain working.
* [ ] Multiplayer remains stable.

---

# Agent Instructions

Before implementation:

1. Read:

   * REQ-014
   * REQ-016
   * REQ-017
   * REQ-018
   * FPS Engine integration audit

2. Inspect:

   * current Player prefab;
   * first-person weapon hierarchy;
   * `PlayerShoot`;
   * `PlayerHealth`;
   * `PlayerNetworkSetup`;
   * camera/layer configuration;
   * network ownership model.

3. Create a clear distinction between:

   * gameplay weapon state;
   * first-person presentation;
   * world presentation.

4. Do not introduce another damage path.

5. Do not require the Cowsins complete weapon runtime.

---

# Implementation Priority

If scope becomes large, implement in this order:

1. World Ruger attached to remote player
2. Correct owner/remote visibility
3. Networked fire presentation event
4. Remote muzzle flash
5. Remote gunshot audio
6. Remote weapon recoil
7. Aim pitch synchronization

Aim pitch may be deferred if it proves substantially more complex than the core presentation.

---

# Out of Scope

REQ-019 does not implement:

* final humanoid character mesh;
* arm/hand IK;
* full third-person character animations;
* walking animation;
* sprint animation;
* crouch animation;
* reload gameplay;
* remote reload animation;
* weapon switching;
* inventory;
* attachments;
* multiple guns;
* shell casing networking;
* networked bullet trails;
* server-side hit validation;
* lag compensation.

---

# Definition of Done

REQ-019 is complete when multiplayer combat visually reads like two armed players fighting rather than two networked capsules with local-only guns.

The desired experience is:

```text
PLAYER 1
sees:
    first-person Ruger
    +
    Player 2 holding world Ruger

PLAYER 1 fires:
    immediate first-person fire presentation
    +
    Bullseye gameplay shot

PLAYER 2 sees:
    Player 1 world Ruger recoil
    +
    muzzle flash
    +
    hears gunshot
```

The first-person and world representations should be architecturally separate but driven by the same weapon action, establishing the foundation for future character models, reload animations, multiple weapons, and full third-person combat presentation.
