# REQ-049 — Rebuild Third-Person Weapon Holding as a Procedural Arm/Hand Rig

## Summary

Replace the current third-person weapon-animation approach with a **weapon-first procedural holding system**.

We should stop trying to create or modify full animation clips so that the character happens to hold a gun correctly.

Instead:

1. Existing locomotion animations continue playing normally.
2. The equipped weapon is positioned independently in a stable third-person weapon anchor.
3. The weapon defines exactly where the right hand and left hand should grip it.
4. The player's shoulders, upper arms, elbows, forearms, wrists, and hands are procedurally positioned around the weapon using Unity Animation Rigging / IK.
5. Shared hold profiles define the general posture for:
   - `LongGun_Hold`
   - `ShortGun_Hold`
   - `HeavyGun_Hold`
6. Individual weapons may have small weapon-specific adjustments, but should not require unique locomotion animations.

This ticket intentionally moves away from the third-person weapon-animation architecture attempted in REQ-047 and REQ-048.

---

# Core Principle

The new relationship should be:

```text
Base Character Animation
        ↓
Weapon positioned correctly
        ↓
Right hand moves to weapon
Left hand moves to weapon
        ↓
Elbows / arms / wrists solve around weapon
        ↓
Final third-person pose
```

The weapon should **not** be positioned by dragging the hands around.

The hands should be positioned **around the weapon**.

---

# Why This Ticket Exists

Third-person weapon animation has become one of the most difficult parts of Bullseye development.

Previous approaches have produced recurring problems:

- Guns appearing in incorrect locations.
- Guns pointing sideways or downward.
- Guns rotating incorrectly.
- Moving the weapon unexpectedly moves the player's hand.
- Moving a hand unexpectedly changes weapon alignment.
- Custom idle animations eliminate the original idle motion.
- Upper-body override animations replace too much of the original locomotion.
- IK attempts to create the entire pose instead of simply solving limb placement.
- Creating separate animation clips per weapon is not scalable.
- Editing poses through Very Animation requires too much repetitive manual work.
- Cursor-created custom posing tools have been unreliable.
- Adding future weapons would multiply this work dramatically.

The new system should solve the simpler and more important problem:

> Put the weapon where it belongs, and make the player's arms conform to it.

---

# Important Architectural Change

## Previous Approach

Conceptually:

```text
Animation
↓
moves hands
↓
hands move weapon
```

This creates dependency problems because the weapon's transform becomes tied to whatever the animation happens to do.

---

## New Approach

Conceptually:

```text
Locomotion animation
        ↓
character body moves normally

ThirdPersonWeaponAnchor
        ↓
weapon sits in intended position

Weapon.Grip_R
        ↓
right hand IK

Weapon.Grip_L
        ↓
left hand IK
```

The weapon becomes the reference frame for the arms.

---

# 1. Preserve All Existing Locomotion Animations

Existing animations remain responsible for normal character motion:

- Standing Idle
- Walk Forward
- Walk Backward
- Strafing
- Running
- Sprinting
- Crouching
- Prone
- Jumping
- Wall running
- Dolphin diving
- other existing locomotion

REQ-049 should **not duplicate these animations per weapon**.

Do not create:

```text
AK_StandingIdle
AK_Walk
AK_Run

DMR_StandingIdle
DMR_Walk
DMR_Run

Shotgun_StandingIdle
...
```

Do not modify existing locomotion clips simply to make the character hold a weapon.

---

# 2. Remove Animation Clips as the Primary Weapon-Holding Solution

Weapon holding should no longer depend primarily on custom:

```text
AK_Hold.anim
DMR_Hold.anim
Shotgun_Hold.anim
etc.
```

Existing experimental assets may remain temporarily for comparison, but the new system must work **without requiring these clips**.

Very Animation may still be useful later for specialized artistic animation work, but it should no longer be required just to make a player hold a gun correctly.

---

# 3. Weapon Hold Classes

Each weapon must belong to one of three primary third-person hold classes:

```text
LongGun
ShortGun
HeavyGun
```

## LongGun

Examples:

- AK
- DMR
- sniper rifles
- shotguns
- future rifles
- similar two-handed long weapons

Uses:

```text
LongGun_Hold
```

## ShortGun

Examples:

- pistols
- compact sidearms

Uses:

```text
ShortGun_Hold
```

## HeavyGun

Examples:

- rocket launchers
- future large/heavy weapons

Uses:

```text
HeavyGun_Hold
```

---

# 4. Hold Profiles Are Rig Configurations, Not Locomotion Animations

Create a data structure such as:

```text
ThirdPersonWeaponHoldProfile
```

The three initial shared profiles should be:

```text
LongGun_Hold
ShortGun_Hold
HeavyGun_Hold
```

These are **procedural rig/posture profiles**, not replacements for StandingIdle, Walk, etc.

They may contain configuration such as:

```text
Weapon anchor position
Weapon anchor rotation

Right elbow hint position
Left elbow hint position

Right arm IK weight
Left arm IK weight

Shoulder / clavicle influence
Chest influence

Maximum arm reach
Blend speed
```

Do not store huge collections of manually entered Euler rotations for every bone.

---

# 5. ThirdPersonWeaponAnchor

Create a stable transform on the player:

```text
Player
└── ThirdPersonWeaponAnchor
```

The exact parent should be chosen based on testing.

Likely candidates include:

```text
UpperChest
Chest
Spine
```

or another character-space transform that naturally follows the upper torso.

The equipped third-person weapon should be placed under or driven relative to this anchor.

Example:

```text
Player
└── Armature
    └── Chest
        └── ThirdPersonWeaponAnchor
            └── EquippedWeapon
```

The weapon should therefore naturally follow:

- standing idle motion;
- walking;
- running;
- crouching;
- prone;
- character rotation;

without requiring the hands to determine the weapon transform.

---

# 6. Weapon Transform Must Be Independent of Hand IK

This is a critical requirement.

Moving:

```text
RightHand
LeftHand
RightElbow
LeftElbow
```

must **not move the weapon**.

Likewise, adjusting IK should not feed back into weapon placement.

The dependency must remain one-way:

```text
Weapon
↓
Grip targets
↓
Hands
↓
Arms
```

Never:

```text
Hands
↓
Weapon
↓
Hands
```

Avoid cyclic transform dependencies.

---

# 7. Standard Weapon Marker Contract

Every weapon prefab should support a standardized marker hierarchy.

Preferred names:

```text
Grip_R
Grip_L
Aim
Muzzle
```

Example:

```text
Weapon
├── Mesh
├── Grip_R
├── Grip_L
├── Aim
└── Muzzle
```

---

# 8. Grip_R

`Grip_R` defines exactly where the player's right hand belongs.

It should represent:

- palm position;
- wrist orientation;
- hand orientation relative to the handle/trigger area.

For a typical long gun, `Grip_R` should sit where the right palm naturally wraps around the pistol grip.

For a pistol, `Grip_R` should sit at the pistol grip.

For a heavy weapon, `Grip_R` should represent the appropriate dominant-hand control point.

---

# 9. Grip_L

`Grip_L` defines where the support hand belongs.

Examples:

### LongGun

- handguard;
- foregrip;
- shotgun pump;
- sniper fore-end.

### ShortGun

May be:

- omitted for a one-handed stance;
- used for a two-handed pistol stance.

### HeavyGun

May represent:

- front support grip;
- launcher support position;
- other appropriate support contact point.

---

# 10. Aim Marker

Each weapon should have:

```text
Aim
```

with a standardized orientation:

```text
+Z = weapon forward / barrel direction
+Y = weapon up
```

Cursor/runtime systems should use this transform instead of guessing an FBX model's local forward axis.

This allows weapons imported with unusual source-model orientations to be normalized.

---

# 11. Muzzle Marker

Continue supporting:

```text
Muzzle
```

for:

- projectile/hitscan origin where applicable;
- muzzle flash;
- effects;
- debugging;
- alignment checks.

REQ-049 should reuse existing muzzle transforms when already present.

---

# 12. Source Model Convention

Where practical, future weapon FBXs should contain empties or transforms named:

```text
Grip_R
Grip_L
Aim
Muzzle
```

before export.

This should become the recommended Bullseye weapon-modeling convention.

If these markers exist in the imported model, the authoring tool should find them automatically.

---

# 13. Missing Marker Handling

If a weapon lacks a required marker, the editor tool should offer:

```text
Create Missing Grip_R
Create Missing Grip_L
Create Missing Aim
Create Missing Muzzle
```

The tool may create the Transform automatically.

However:

> Cursor should not pretend it can reliably infer the exact artistic grip location from arbitrary weapon geometry.

The developer may need to visually position the marker once.

That should be a simple Scene-view operation, not a character-animation operation.

---

# 14. Weapon Class Determines General Weapon Placement

The three hold profiles define approximate weapon positioning relative to the body.

Example:

```text
LongGun_Hold
```

might place a weapon:

- forward of chest;
- slightly right of body center;
- near shoulder height;
- barrel forward.

`ShortGun_Hold` may position it:

- closer to centerline;
- farther forward;
- higher relative to chest.

`HeavyGun_Hold` may position it:

- near shoulder;
- offset laterally;
- at a substantially different angle;
- in a posture appropriate for launchers/heavy weapons.

These should provide sensible starting configurations for the entire weapon class.

---

# 15. Individual Weapon Alignment

Each WeaponDefinition may contain a small fine-tuning transform if necessary:

```text
ThirdPersonAnchorPositionOffset
ThirdPersonAnchorRotationOffset
```

This is acceptable.

However, these values should adjust:

> where the weapon sits relative to its class hold profile

and should **not** directly manipulate character arm bones.

---

# 16. Weapon Forward / Upright Normalization

When equipped, the system must make the weapon:

- point forward;
- remain upright;
- align consistently relative to the character.

Use the weapon's `Aim` marker as the authoritative weapon orientation.

Do not rely on assumptions such as:

```text
FBX +X is forward
```

or:

```text
FBX +Z is forward
```

Different imported models may have different local coordinate systems.

---

# 17. Right Arm IK

Create a Two Bone IK chain:

```text
RightUpperArm
↓
RightForearm
↓
RightHand
```

Target:

```text
EquippedWeapon.Grip_R
```

The right hand should therefore be pulled onto the weapon handle.

---

# 18. Left Arm IK

For weapons requiring a support hand:

```text
LeftUpperArm
↓
LeftForearm
↓
LeftHand
```

Target:

```text
EquippedWeapon.Grip_L
```

This should place the support hand on the weapon.

---

# 19. Wrist / Hand Orientation

Hand IK must account for both:

- position;
- rotation.

It is not sufficient for the hand merely to reach the gun.

The wrist and palm should orient according to:

```text
Grip_R.rotation
Grip_L.rotation
```

This allows different weapons to specify different grip angles.

---

# 20. Elbow Hints

Both arms should support elbow hints.

Create:

```text
RightElbowHint
LeftElbowHint
```

These may be:

- part of the hold profile;
- runtime/editor targets generated relative to the torso;
- or a combination of class defaults and weapon overrides.

The purpose is to produce believable elbow direction.

Avoid:

- elbows pointing backward;
- arms folding into the chest;
- elbows excessively flaring outward;
- elbows collapsing unnaturally.

---

# 21. Shoulder / Upper Arm Support

Two Bone IK alone may produce technically correct but unattractive arm positions.

The rig may therefore apply limited additional adjustments to:

- shoulders;
- clavicles;
- upper chest;
- upper arms.

These adjustments should help the body naturally reach the weapon.

They should remain:

- small;
- predictable;
- class-driven;
- smoothly blended.

Do not build a giant manual per-bone pose database.

---

# 22. The Body Must Adapt to the Gun

The central behavior of REQ-049 is:

> Position the gun correctly first, then make the player model adapt around it.

This includes:

- shoulders;
- upper arms;
- elbows;
- forearms;
- wrists;
- hands.

The weapon should not be constantly repositioned to compensate for whatever pose the animation happens to produce.

---

# 23. Preserve Original Idle Animation

With the weapon equipped:

```text
StandingIdle
```

should continue playing normally.

Examples of motion that should remain where present:

- breathing;
- small torso movement;
- shifting weight;
- subtle idle sway.

REQ-049 should not replace StandingIdle with a static weapon pose.

Instead:

```text
StandingIdle
↓
Animation Rigging modifies upper limbs after animation evaluation
↓
arms hold weapon
```

This is a core acceptance requirement.

---

# 24. Preserve Walking / Running Animation

The same principle applies during locomotion.

Example:

```text
WalkForward
↓
legs / hips / torso animate normally
↓
weapon anchor follows torso
↓
arm rig places hands onto weapon
```

Do not require:

```text
LongGun_WalkForward.anim
```

unless a future artistic enhancement specifically chooses to add one.

---

# 25. Sprint

Sprint may require different weapon-anchor positioning.

Support a state variation such as:

```text
LongGun_Hold
LongGun_SprintHold

ShortGun_Hold
ShortGun_SprintHold

HeavyGun_Hold
HeavyGun_SprintHold
```

However, these should still be **rig profiles / anchor configurations**, not complete locomotion animations.

The base Sprint animation continues underneath.

---

# 26. Crouch

Initially attempt to reuse the same class hold configuration during crouch:

```text
Crouch Animation
+
LongGun_Hold
```

Only add:

```text
LongGun_CrouchHold
ShortGun_CrouchHold
HeavyGun_CrouchHold
```

if geometry genuinely requires it.

---

# 27. Prone

Prone will likely require a special class configuration because body geometry changes significantly.

Support:

```text
LongGun_ProneHold
ShortGun_ProneHold
HeavyGun_ProneHold
```

Again:

> These should adjust weapon placement and arm-rig targets, not replace the underlying prone animation.

---

# 28. Aim

Support aim-state variants:

```text
LongGun_AimHold
ShortGun_AimHold
HeavyGun_AimHold
```

These may reposition:

- weapon anchor;
- shoulders;
- elbows;
- chest influence.

Do not replace locomotion animations solely because the player is aiming.

---

# 29. Smooth Rig Blending

All rig changes should interpolate smoothly.

Examples:

```text
Normal → Sprint
Sprint → Normal
Normal → Aim
Aim → Normal
Standing → Prone
Weapon switch
```

Do not abruptly snap:

- weapon;
- hands;
- elbows;
- shoulders.

---

# 30. Weapon Switch

When switching weapons:

1. Reduce old arm-rig influence if necessary.
2. Replace weapon.
3. Resolve new hold class.
4. Position new weapon.
5. Resolve `Grip_R` / `Grip_L`.
6. Blend arm rig onto new targets.

The system should be generic.

Do not add weapon-name-specific code such as:

```csharp
if (weaponName == "AK")
{
    ...
}
else if (weaponName == "Shotgun")
{
    ...
}
```

Use WeaponDefinition data.

---

# 31. Editor Tool

Create:

```text
Bullseye
→ Third-Person Weapon Hold Setup
```

This should replace the previous complicated custom arm-positioning editor.

The goal is **automation and preview**, not manual animation creation.

---

# 32. Editor — Weapon Selection

Allow selection of a:

```text
WeaponDefinition
```

Display:

```text
Weapon Name
Weapon Prefab
Weapon Hold Class

Grip_R
Grip_L
Aim
Muzzle

Current Anchor Offset
Current Rig Profile
Validation Status
```

---

# 33. Editor — Auto Setup Weapon

Provide:

```text
Auto Setup Weapon
```

This should:

1. Detect weapon prefab.
2. Detect `Grip_R`.
3. Detect `Grip_L`.
4. Detect `Aim`.
5. Detect `Muzzle`.
6. Read Weapon Hold Class.
7. Assign appropriate shared hold profile.
8. Create missing configuration objects.
9. Configure right/left arm targets.
10. Prepare preview.

Do not overwrite intentional custom configuration without warning.

---

# 34. Editor — Preview Weapon

Provide:

```text
Preview Weapon
```

This should configure the real Bullseye third-person player model in an editor preview scene.

The developer should immediately see:

- real player mesh;
- selected weapon;
- correct base animation;
- weapon in intended body position;
- right hand on `Grip_R`;
- left hand on `Grip_L`;
- elbow solution;
- wrist orientation.

No multiplayer client should be required.

---

# 35. Preview Locomotion Selection

Allow the developer to preview:

```text
Standing Idle
Walk Forward
Walk Backward
Strafe Left
Strafe Right
Run
Sprint
Crouch
Prone
Aim
```

through a dropdown or simple controls.

The editor should switch the **base animation**, while retaining the procedural weapon rig.

This lets the developer verify:

> "Does this gun remain correctly held while the real animations play?"

---

# 36. Scene-View Marker Editing

If a grip is incorrect, the developer should be able to select:

```text
Grip_R
Grip_L
Aim
```

and move/rotate the marker directly in Scene view.

After moving the marker:

> the associated hand should immediately follow.

This should be the primary manual authoring interaction.

The developer should not need to manipulate animation keyframes.

---

# 37. Optional Elbow Editing

Allow developer adjustment of:

```text
RightElbowHint
LeftElbowHint
```

in Scene view.

These may be:

- class-level defaults;
- weapon-specific overrides.

If a particular weapon needs a slightly different elbow direction, this should be easy to correct without creating a new animation.

---

# 38. Do Not Move Weapon When Editing Arms

This is a strict requirement.

When manually adjusting:

- elbow hints;
- IK;
- shoulder parameters;

the weapon must remain stationary relative to its weapon anchor.

This allows the developer to effectively pose the character **around the weapon**.

---

# 39. Validate All Weapons

Add:

```text
Validate All Weapons
```

Scan all WeaponDefinitions.

Example output:

```text
AK
Class: LongGun
✓ Grip_R
✓ Grip_L
✓ Aim
✓ Muzzle
✓ Hold Profile

DMR
Class: LongGun
✓ Grip_R
✓ Grip_L
✓ Aim
✓ Muzzle
✓ Hold Profile

Shotgun
Class: LongGun
✓ Grip_R
✗ Grip_L
✓ Aim
✓ Muzzle

Pistol
Class: ShortGun
✓ Grip_R
✓ Aim
✓ Muzzle

Future Rocket Launcher
Class: HeavyGun
✓ HeavyGun_Hold profile available
```

---

# 40. Auto Configure All Valid Weapons

Provide an optional batch command:

```text
Auto Configure All Valid Weapons
```

It should configure any weapons whose required markers and definitions are valid.

This is important as the weapon library grows.

---

# 41. WeaponDefinition Integration

Extend the current WeaponDefinition rather than creating disconnected per-weapon scripts.

Suggested fields:

```text
ThirdPersonHoldClass

ThirdPersonAnchorPositionOffset
ThirdPersonAnchorRotationOffset

UseLeftHandGrip

OptionalHoldProfileOverride
```

Avoid large bone-transform datasets.

---

# 42. Shared Profiles

Create:

```text
LongGun_Hold
ShortGun_Hold
HeavyGun_Hold
```

as the primary shared profiles.

Additional shared state profiles may include:

```text
LongGun_SprintHold
LongGun_ProneHold
LongGun_AimHold

ShortGun_SprintHold
ShortGun_ProneHold
ShortGun_AimHold

HeavyGun_SprintHold
HeavyGun_ProneHold
HeavyGun_AimHold
```

Do not create all variants unless needed for implementation/testing.

---

# 43. Per-Weapon Overrides

Weapons should normally use their class profile.

Example:

```text
AK
→ LongGun_Hold

DMR
→ LongGun_Hold

Shotgun
→ LongGun_Hold

Sniper
→ LongGun_Hold
```

Their different physical geometry is primarily handled by:

```text
Grip_R
Grip_L
Aim
weapon anchor offset
```

Only create a weapon-specific hold-profile override when necessary.

---

# 44. HeavyGun_Hold

`HeavyGun_Hold` must be implemented as a supported core profile even if Bullseye does not yet contain a rocket launcher.

The architecture should be ready for a future weapon such as:

```text
RocketLauncher
ThirdPersonHoldClass = HeavyGun
```

without requiring another animation-system redesign.

---

# 45. No Runtime Dependency on Very Animation

REQ-049 should not require Very Animation for normal gameplay.

Very Animation may remain installed for future hand-authored animation work, but:

- no runtime script should reference it;
- no editor automation should depend on undocumented Very Animation APIs;
- standard gameplay should work if Very Animation is later removed.

---

# 46. Multiplayer

Do not network hand/elbow transforms individually.

Synchronize only gameplay state such as:

```text
EquippedWeaponId
IsAiming
IsSprinting
IsCrouching
IsProne
AimPitch
```

Each client should locally:

1. resolve weapon;
2. resolve hold class;
3. position weapon;
4. evaluate arm IK.

---

# 47. First-Person Protection

REQ-049 applies to the **world / third-person character**.

Do not break the separate first-person systems:

- first-person weapon position;
- first-person recoil;
- first-person reload;
- ADS;
- reticle;
- camera;
- firing.

---

# 48. Existing REQ-047 / REQ-048 Systems

The previous third-person weapon posing implementations should now be considered experimental/deprecated.

Cursor should review them and:

## Keep

Only components that directly support the new architecture, such as:

- useful weapon markers;
- usable RigBuilder setup;
- stable network state;
- useful preview infrastructure.

## Remove / Disable

Systems based on:

- per-animation weapon copies;
- hand-driven weapon positioning;
- giant collections of arm offsets;
- custom full pose animation generation;
- broken recoil arm movement;
- previous editor systems that conflict with the new workflow.

Do not continue layering new systems on top of broken legacy behavior.

---

# 49. Implementation Phases

## Phase 1 — Clean Foundation

Before adding more behavior:

- identify conflicting REQ-047/048 systems;
- disable/remove them where appropriate;
- preserve working locomotion;
- preserve weapon gameplay;
- preserve first-person systems.

---

## Phase 2 — Weapon Marker Contract

Implement support for:

```text
Grip_R
Grip_L
Aim
Muzzle
```

Add validation.

---

## Phase 3 — Player Rig

Create:

```text
ThirdPersonWeaponAnchor

RightArmIK
LeftArmIK

RightElbowHint
LeftElbowHint
```

Verify arms can follow arbitrary targets without moving the weapon.

---

## Phase 4 — Shared Hold Profiles

Create:

```text
LongGun_Hold
ShortGun_Hold
HeavyGun_Hold
```

These should position the weapon and configure general arm posture.

---

## Phase 5 — Editor Tool

Create:

```text
Bullseye
→ Third-Person Weapon Hold Setup
```

Implement:

- weapon selection;
- validation;
- Auto Setup;
- Preview;
- marker editing;
- locomotion preview.

---

## Phase 6 — Existing Weapon Validation

Test the architecture with multiple existing weapons.

At minimum:

### One LongGun

For example:

```text
AK
DMR
or Shotgun
```

### One ShortGun

Current pistol.

Do **not** architect the system specifically around one weapon.

---

## Phase 7 — Multiple LongGun Test

Test multiple different long-gun geometries.

Recommended:

```text
AK
DMR
Shotgun
```

All should begin from:

```text
LongGun_Hold
```

Their individual grip markers should account for most of the geometry difference.

---

## Phase 8 — Locomotion Validation

For multiple weapons test:

```text
StandingIdle
Walk
Strafe
Run
Sprint
Crouch
Prone
```

Base animation must continue to play.

Hands must continue to hold the weapon.

---

# Acceptance Criteria

## Architecture

- [ ] Weapon holding no longer requires weapon-specific locomotion animations.
- [ ] Existing base locomotion clips remain unmodified.
- [ ] `LongGun_Hold` exists.
- [ ] `ShortGun_Hold` exists.
- [ ] `HeavyGun_Hold` exists.
- [ ] Weapon is positioned independently of arm IK.
- [ ] Arms conform to the weapon rather than driving weapon placement.

---

## Weapon Markers

- [ ] Weapons support `Grip_R`.
- [ ] Two-handed weapons support `Grip_L`.
- [ ] Weapons support standardized `Aim`.
- [ ] Weapons support `Muzzle`.
- [ ] Aim orientation convention is standardized.
- [ ] Missing markers are clearly reported.

---

## Right Arm

- [ ] Right hand reaches `Grip_R`.
- [ ] Wrist orientation matches `Grip_R`.
- [ ] Right elbow bends naturally.
- [ ] Adjusting right-arm IK does not move the weapon.

---

## Left Arm

- [ ] Left hand reaches `Grip_L`.
- [ ] Wrist orientation matches `Grip_L`.
- [ ] Left elbow bends naturally.
- [ ] Adjusting left-arm IK does not move the weapon.

---

## Base Animation Preservation

With a weapon equipped:

- [ ] StandingIdle continues animating.
- [ ] Walk continues animating.
- [ ] Strafe continues animating.
- [ ] Run continues animating.
- [ ] Crouch continues animating.
- [ ] Prone continues animating.
- [ ] Weapon holding does not replace these animations with static poses.

---

## Multiple Weapons

At least:

- [ ] two different LongGun weapons work;
- [ ] one ShortGun works.

All LongGun tests should start from:

```text
LongGun_Hold
```

without requiring weapon-specific animation clips.

---

## Editor Workflow

- [ ] `Bullseye → Third-Person Weapon Hold Setup` exists.
- [ ] WeaponDefinition can be selected.
- [ ] Weapon class is displayed.
- [ ] Marker validation is displayed.
- [ ] `Auto Setup Weapon` works.
- [ ] `Preview Weapon` works outside multiplayer Play Mode.
- [ ] Base preview animation can be changed.
- [ ] Grip markers can be visually edited.
- [ ] Elbow hints can be visually edited.
- [ ] Adjustments update the preview immediately.

---

## Scalability

Adding a future weapon should generally require:

1. Import model.
2. Assign WeaponDefinition.
3. Assign:
   - `LongGun`,
   - `ShortGun`,
   - or `HeavyGun`.
4. Add/verify:
   - `Grip_R`,
   - `Grip_L` if applicable,
   - `Aim`,
   - `Muzzle`.
5. Run `Auto Setup Weapon`.
6. Preview.
7. Make small marker adjustments if necessary.

It should **not** require creating a new set of movement animations.

---

# Critical Guardrails for Cursor

## DO NOT use custom animation clips as the primary method for holding weapons.

REQ-049 explicitly moves away from that architecture.

## DO NOT modify StandingIdle, Walk, Run, Crouch, Prone, etc. just to position arms around weapons.

The original animation must remain underneath the procedural rig.

## DO NOT parent weapon placement behavior to IK-driven hand transforms.

The weapon must remain independently positioned.

## DO NOT move the weapon in response to elbow or hand corrections.

The body adapts to the weapon.

## DO NOT create dozens of per-weapon arm rotation fields.

Use IK targets, grip transforms, elbow hints, class profiles, and small offsets.

## DO NOT hard-code specific weapons.

No architecture such as:

```csharp
if (weaponName == "AK")
{
    ...
}
```

Use WeaponDefinition configuration.

## DO NOT optimize only for the AK.

This system is explicitly for:

- pistols;
- AKs;
- DMRs;
- shotguns;
- snipers;
- future rifles;
- rocket launchers;
- future weapons.

## DO NOT build another custom animation editor.

Build a **weapon setup and preview tool**.

The developer should manipulate:

- weapon markers;
- elbow hints;
- class/weapon configuration;

not animation timelines.

## DO NOT destroy the original locomotion motion.

Idle motion must remain visible.

Walking motion must remain visible.

The player's character should look animated **while the procedural arm rig keeps the weapon correctly held**.

---

# Success Definition

REQ-049 succeeds when Bullseye no longer treats:

> "How does this character hold this gun?"

as an animation-clip-authoring problem.

Instead:

> The weapon is positioned correctly relative to the player. The weapon tells Bullseye exactly where each hand belongs. Shared `LongGun_Hold`, `ShortGun_Hold`, and `HeavyGun_Hold` profiles establish the general posture. Unity's rigging system then positions the player's shoulders, arms, elbows, wrists, and hands around the weapon while the original locomotion animation continues underneath.

The ultimate workflow should be:

```text
Import weapon
↓
Assign LongGun / ShortGun / HeavyGun
↓
Place or verify Grip_R / Grip_L / Aim / Muzzle
↓
Auto Setup
↓
Preview
↓
Minor marker/elbow correction if necessary
↓
Done
```

**Adding Weapon #20 should not be substantially harder than adding Weapon #5.**