# REQ-020 — First-Person Aim / ADS Weapon Animation

## Status

Ready for implementation on:

`experiment/fps-engine-integration`

## Background

Bullseye currently has first-person weapon presentation for the Ruger 22, including a visible firing animation and other FPS Engine-derived presentation effects.

However, aiming currently does not produce convincing weapon movement.

When the player presses Aim, the camera/FOV may change, but the Ruger itself does not smoothly move into an aiming-down-sights position like weapons do in the Cowsins FPS Engine demo.

The Cowsins FPS Engine contains an `AimBehaviour` that:

* uses a weapon-specific `aimPoint`;
* moves the weapon toward the camera's aiming position;
* applies configurable aiming rotation;
* transitions between hip-fire and aiming presentation;
* works together with weapon-specific configuration.

Bullseye should adopt this presentation concept without adopting the complete Cowsins weapon runtime.

---

# Goal

Add polished first-person weapon movement when aiming.

When the player activates Aim:

```text
Hip-Fire Position
        ↓
smooth transition
        ↓
Aim / ADS Position
```

When Aim is released or toggled off:

```text
ADS Position
        ↓
smooth transition
        ↓
Hip-Fire Position
```

The Ruger should visibly move so that the player appears to be looking down the weapon's sights rather than simply zooming the camera.

---

# Desired Experience

## Normal State

The Ruger sits in its normal first-person hip-fire position.

Example conceptual position:

```text
        +
        |  reticle
        |

                 Ruger
```

The weapon should remain somewhat offset from screen center.

## Aim State

When Aim activates:

* Ruger moves upward;
* Ruger moves toward screen center;
* weapon sights align approximately with the reticle/camera forward direction;
* movement is smooth;
* existing aim FOV change occurs;
* aim feels similar to the Cowsins FPS Engine demo.

Conceptually:

```text
        sights
          ↓
        [ + ]
          |
        Ruger
```

Exact alignment should be tunable rather than hardcoded.

---

# Core Architectural Rule

Aiming presentation is cosmetic.

Do not change the existing Bullseye damage raycast solely to match the animated gun position.

Gameplay aiming should remain:

```text
Camera Forward
      ↓
PlayerShoot
      ↓
Bullseye hit detection
```

The visible gun moves to match the aim direction.

The gun itself does not become the authoritative raycast source.

---

# Cowsins Reference

Inspect and adapt the Cowsins implementation, particularly:

* `AimBehaviour`
* `WeaponIdentification.aimPoint`
* `Weapon_SO` aiming fields
* pistol aiming configuration
* relevant weapon hierarchy
* aiming position/rotation values
* aiming interpolation behavior

Use the Cowsins FPS Engine demo as the visual reference.

Do not import the complete:

* `WeaponController`
* `PlayerDependencies`
* `InputManager`
* `CameraFOVManager`

simply to gain ADS movement.

---

# Bullseye-Owned Implementation

Implement the actual integration in Bullseye-owned code.

Preferred architecture:

```text
Bullseye Aim Input
        ↓
WeaponPresentationController
        ↓
Aim Presentation
        ↓
WeaponEffectsRoot / WeaponMount
```

The implementation may extend the existing weapon presentation controller or create a small dedicated component such as:

`WeaponAimPresentation`

Exact class name is flexible.

---

# Weapon Hierarchy

Use the existing first-person presentation hierarchy established by previous requirements.

Prefer a structure that isolates weapon aiming from other effects:

```text
Camera
└── WeaponView
    └── WeaponEffectsRoot
        └── AimRoot
            └── WeaponMount
                └── Ruger22_FirstPerson
                    └── MuzzlePoint
```

Conceptually:

* `WeaponEffectsRoot` can handle bob/sway/recoil;
* `AimRoot` handles ADS positioning;
* `WeaponMount` handles model-specific alignment.

Exact hierarchy may differ if the current structure already provides equivalent separation.

---

# Hip-Fire Pose

Store the weapon's normal resting pose.

At minimum:

* local position;
* local rotation.

These values should be configurable.

Do not assume the current Ruger transform values are permanently correct.

---

# ADS Pose

Create configurable aim pose values.

At minimum:

* ADS local position;
* ADS local rotation;
* transition speed.

Preferred inspector configuration:

```text
Hip Position
Hip Rotation

ADS Position
ADS Rotation

Aim In Speed
Aim Out Speed
```

Optional:

* ADS scale adjustment;
* separate aim-in and aim-out curves.

---

# Aim Point

Preferred implementation should support a weapon-specific `AimPoint`.

For example:

```text
Ruger22_FirstPerson
└── AimPoint
```

`AimPoint` should represent the point on the weapon that should approximately align with camera center when aiming.

For a pistol this would generally correspond to the rear/front sight alignment area.

If practical, derive the necessary weapon offset from this transform.

If a simpler manually configured ADS pose proves more stable for the prototype, that is acceptable.

However, architecture should allow future weapons to define their own aim position.

---

# Smooth Transition

Do not snap instantly between poses.

Aiming should interpolate smoothly.

Preferred behavior:

```text
Aim activated
→ ~quick smooth transition into ADS

Aim released
→ smooth transition back to hip
```

Exact timing should be configurable.

The transition should feel responsive rather than cinematic or sluggish.

---

# Existing Aim Input

Use the existing Bullseye Aim action.

Do not introduce Cowsins `InputManager`.

Do not poll:

`Gamepad.current`

The same Aim input should work with:

* mouse;
* assigned gamepad.

Existing toggle/hold behavior should remain unless there is a compelling implementation reason to change it.

---

# FOV Integration

Bullseye currently has an aim/FOV system.

There must remain **one clear camera FOV authority**.

If `PlayerAimZoom` currently works:

* keep it;
* coordinate weapon ADS motion with it.

Do not also enable Cowsins `CameraFOVManager`.

Desired sequence:

```text
Aim Input
    ↓
┌─────────────────────┐
│                     │
▼                     ▼
Weapon moves       Camera FOV
into ADS           adjusts
│                     │
└──────────┬──────────┘
           ↓
       Aim state
```

Both effects should feel synchronized.

---

# Sprint Interaction

Aiming and sprinting should not visually fight each other.

Preferred behavior:

If the player begins aiming while sprinting:

* sprint presentation should reduce/stop;
* weapon transitions toward ADS;
* camera sprint effects should appropriately reduce if current gameplay permits aiming while sprinting.

If Bullseye currently prevents aim while sprinting, preserve that behavior.

Do not allow:

```text
full sprint weapon animation
+
full ADS pose
```

to independently overwrite the same transform.

---

# Recoil Interaction

Existing fire/recoil presentation must continue working while aiming.

When firing from ADS:

```text
ADS Base Pose
      ↓
Fire Recoil Offset
      ↓
Return to ADS Base Pose
```

Do not return to the hip-fire position after every shot.

Weapon recoil should be additive to the current aim pose.

Conceptually:

```text
Final Weapon Pose =
Base Hip/ADS Pose
+ sway
+ bob
+ recoil
```

Where practical, keep these effects separated by transforms or coordinated code.

---

# Camera Sway / Bob Interaction

REQ-017 and REQ-018 added camera and sprint presentation effects.

Aiming should reduce excessive first-person weapon movement.

Preferred behavior while ADS:

* reduced weapon sway;
* reduced weapon bob;
* reduced sprint-style motion;
* more stable weapon alignment.

The goal is for aiming to feel more precise.

Exact reduction values should be configurable.

---

# Fire Animation Interaction

The current Ruger fire animation must remain functional while aiming.

When firing in ADS:

* weapon should remain generally centered;
* fire animation/recoil should occur;
* after firing, weapon returns to the ADS position.

Do not allow the firing animation to permanently reset the weapon to its hip-fire transform.

---

# Muzzle VFX

Existing muzzle VFX must continue working in both:

* hip-fire;
* ADS.

The `MuzzlePoint` should move naturally with the weapon.

No duplicate muzzle effect should be introduced.

---

# Death

When the player dies:

* reset/cancel ADS;
* hide first-person weapon according to existing behavior;
* clear any active aim transition.

---

# Respawn

After respawn:

* weapon should begin in hip-fire/default pose;
* ADS state should be reset;
* no stale interpolation values should remain.

The player should need to activate Aim again.

---

# Pause Menu

If Pause opens while aiming:

Preferred behavior:

* ADS presentation resets or remains safely frozen;
* after Resume, input/presentation should not become stuck.

Do not let Pause leave the gun permanently in an intermediate pose.

---

# Weapon-Specific Configuration

Aim positioning should be configurable per weapon.

Do not hardcode all values globally for Ruger.

Extend the existing weapon presentation configuration if available.

Suggested fields:

```text
Hip Local Position
Hip Local Rotation

ADS Local Position
ADS Local Rotation

Aim In Speed
Aim Out Speed

ADS Sway Multiplier
ADS Bob Multiplier
```

If an existing `WeaponPresentationConfig` already exists, extend it rather than creating duplicate configuration assets.

---

# Future Weapon Compatibility

The implementation should allow a future weapon to specify a different ADS pose.

For example:

```text
Ruger
→ iron-sight ADS pose

Rifle
→ rifle sight ADS pose

Scoped Rifle
→ optic-centered ADS pose
```

Do not make the system dependent on the Ruger's exact geometry.

---

# First-Person Only

REQ-020 concerns the **local first-person weapon**.

It does not need to animate the world/third-person Ruger into a different aiming pose.

Third-person weapon aiming can be implemented separately using networked aim pitch / character animations.

Do not block this ticket on third-person animation work.

---

# Cowsins Animation Assets

Inspect whether any Cowsins pistol animation assets can directly contribute to:

* aim transition;
* aim pose;
* weapon movement.

Reuse them if compatible.

However, the Cowsins aiming system appears to rely significantly on procedural weapon positioning rather than simply playing one animation clip.

Therefore, do not force an incompatible Cowsins animation onto the Ruger.

Reproduce Cowsins ADS behavior procedurally if that is cleaner.

---

# Manual Playtest

## Test 1 — Mouse Aim

Using keyboard/mouse:

* stand still;
* activate Aim.

Confirm:

* Ruger smoothly moves toward center;
* sights approximately align with reticle;
* FOV changes;
* no snapping.

Release/toggle Aim off.

Confirm smooth return.

---

## Test 2 — Gamepad Aim

Using the assigned controller:

* activate Aim.

Confirm the same behavior.

Do not use `Gamepad.current`.

---

## Test 3 — Fire While Aiming

Aim and fire repeatedly.

Confirm:

* gun remains in ADS;
* fire animation works;
* recoil works;
* muzzle flash works;
* hit detection works;
* weapon returns to ADS pose after recoil.

---

## Test 4 — Hip Fire

Fire without aiming.

Confirm existing hip-fire presentation remains functional.

---

## Test 5 — Walk While Aiming

Aim and walk.

Confirm:

* gun stays aligned;
* bob/sway is reduced appropriately;
* camera remains controllable.

---

## Test 6 — Sprint → Aim

Sprint, then activate Aim.

Confirm:

* sprint presentation does not fight ADS;
* weapon transitions cleanly;
* sprint speed effects behave appropriately.

---

## Test 7 — Aim → Sprint

Aim, then attempt to sprint.

Confirm behavior is coherent with current movement rules.

No stuck camera/weapon state.

---

## Test 8 — Crouch Aim

Crouch and aim.

Confirm:

* ADS remains aligned;
* weapon does not jump unexpectedly due to camera height changes.

---

## Test 9 — Jump / Land While ADS

Aim, jump, and land.

Confirm:

* weapon presentation survives camera effects;
* landing motion does not permanently break ADS alignment.

---

## Test 10 — Death While Aiming

Aim and get killed.

Confirm:

* first-person gun hides;
* ADS resets;
* respawn works;
* gun returns in hip-fire pose.

---

## Test 11 — Pause While Aiming

Aim.

Open Pause.

Resume.

Confirm:

* weapon does not become stuck between hip and ADS;
* Aim continues functioning afterward.

---

# Acceptance Criteria

## ADS Presentation

* [ ] Ruger visibly moves when Aim activates.
* [ ] Weapon smoothly transitions from hip to ADS.
* [ ] Weapon smoothly returns from ADS to hip.
* [ ] Weapon sights are approximately aligned with screen center.
* [ ] ADS pose is configurable.
* [ ] Transition speed is configurable.

## Input

* [ ] Mouse Aim works.
* [ ] Gamepad Aim works.
* [ ] Existing multiplayer-safe input architecture remains.
* [ ] Cowsins static InputManager is not used.

## FOV

* [ ] Existing aim FOV behavior remains functional.
* [ ] Weapon motion and FOV transition feel coordinated.
* [ ] Only one system owns final camera FOV.

## Weapon Effects

* [ ] Fire animation works during ADS.
* [ ] Recoil works during ADS.
* [ ] Weapon returns to ADS after firing.
* [ ] Muzzle VFX works during ADS.
* [ ] Fire sound works during ADS.
* [ ] Hip-fire remains functional.

## Movement Integration

* [ ] Walking while ADS works.
* [ ] Crouching while ADS works.
* [ ] Sprint/ADS transitions are coherent.
* [ ] Camera effects do not permanently disturb ADS pose.

## State Management

* [ ] ADS resets on death.
* [ ] ADS resets correctly on respawn.
* [ ] Pause does not leave ADS in a broken state.

## Architecture

* [ ] ADS presentation is local-only.
* [ ] Existing `PlayerShoot` remains authoritative for hit detection.
* [ ] Weapon model does not determine damage ray origin.
* [ ] Aim configuration can support future weapons.
* [ ] Full Cowsins `WeaponController` is not required.

---

# Agent Instructions

Before implementation:

1. Read:

   * REQ-014
   * REQ-016
   * REQ-017
   * REQ-018
   * REQ-019
   * `FPS_ENGINE_INTEGRATION_AUDIT.md`

2. Inspect current:

   * first-person Ruger hierarchy;
   * weapon presentation controller;
   * Aim input;
   * `PlayerAimZoom`;
   * camera effects hierarchy;
   * fire/recoil presentation.

3. Inspect Cowsins:

   * `AimBehaviour`;
   * pistol `aimPoint`;
   * `Weapon_SO` aim configuration;
   * pistol prefab hierarchy;
   * pistol ADS tuning;
   * any aim-related animation assets.

4. Determine whether:

   * Cowsins animation assets can be reused directly;
   * procedural interpolation is the better implementation;
   * or a combination is appropriate.

5. Do not import the complete Cowsins runtime just to enable ADS.

---

# Implementation Priority

1. Hip/ADS weapon poses
2. Smooth transitions
3. Synchronize with existing FOV zoom
4. Fire/recoil compatibility while ADS
5. Reduce sway/bob while ADS
6. Tune positioning against the Ruger sights

---

# Out of Scope

REQ-020 does not implement:

* third-person aiming animation;
* hand/arm IK;
* Cowsins FPS arms;
* scopes;
* scope rendering;
* magnified optics;
* weapon attachments;
* aim assist;
* bullet spread changes;
* accuracy changes;
* networked ADS state;
* remote ADS animation;
* weapon switching;
* functional reload/ammo system.

Those may be implemented in later requirements.

---

# Definition of Done

REQ-020 is complete when aiming feels visually like the player is actually raising the Ruger and looking down its sights rather than merely zooming the camera.

The desired experience is:

```text
HIP FIRE

Ruger offset from center
        ↓
Aim pressed
        ↓
smooth weapon movement
+
FOV transition
        ↓
Ruger sights centered
        ↓
Fire
        ↓
ADS recoil / animation
        ↓
return to ADS
        ↓
Aim released
        ↓
smooth return to hip
```

The final result should capture the polished weapon-aiming feel of the FPS Engine while preserving Bullseye's existing multiplayer input, shooting, damage, and networking architecture.
