The current REQ-069 implementation has taken the wrong architectural direction and needs to be corrected before further visual tweaking.

## Problem

The first-person and third-person bodies are currently being treated as though they should be the same rendered character.

This has created several problems:

* Looking downward allows the player to look into/through the character's neck.
* The third-person arms were initially being reused for first person, but they were not properly anchored to the first-person weapon.
* The attempted fix removed too much of the first-person torso.
* The arms now remain down at the character's sides instead of visually holding the first-person weapon.

Please do **not** continue solving these problems by simply hiding arbitrary parts of the existing third-person mesh.

We need to change the architecture.

# Intended Architecture

Use a hybrid FPS body system:

```text
THIRD-PERSON / WORLD CHARACTER
    ├── Full world-visible character
    ├── Normal locomotion animations
    ├── World-view weapon
    ├── World-view arms
    └── Visible to remote players

LOCAL FIRST-PERSON VIEW
    ├── Lower body derived from world character
    │     ├── hips
    │     ├── legs
    │     └── feet
    │
    ├── Optional appropriate torso visibility
    │
    ├── NO visible local head/neck interior
    │
    └── Separate first-person arm rig
          ├── attached to first-person presentation
          ├── poses against the first-person weapon
          ├── independent of world-view arm positioning
          └── local presentation only
```

The player should **not** see the third-person character's arms trying to hold the first-person gun.

The player should have dedicated first-person arms.

---

# 1. Preserve the Third-Person Character

Do not modify or compromise the normal third-person character in order to make first person work.

Other players must continue seeing:

* the complete player model,
* head,
* torso,
* arms,
* legs,
* world-view weapon,
* normal world-view animations.

The third-person character should remain the authoritative world representation.

Do not remove body parts from what remote players see.

---

# 2. First-Person Lower Body

For the local player, we still want body awareness.

When looking downward, the player should see approximately the same:

* hips,
* legs,
* feet,
* locomotion,

that the world-view version of the character is performing.

Walking, sprinting, jumping, crouching, prone, etc. should therefore remain visually consistent with the world character.

It is acceptable and desirable for the lower body to be driven by the same locomotion Animator/state as the world representation.

The goal is that when I look down while sprinting, I can see my character's legs actually sprinting.

---

# 3. Fix the Neck / Head Problem Properly

The player must never be able to look down inside the character's neck/head.

Do not solve this by deleting most of the body.

Instead, determine the cleanest local-player rendering solution.

Possible approaches include:

* hide the head mesh from the local first-person camera,
* hide selected upper-neck/head geometry only from the local camera,
* use camera-specific rendering layers,
* use a dedicated local-body mesh variant,
* use renderer/bone masking if appropriate.

Remote players must still see the head normally.

The first-person camera should occupy a believable eye/head location without rendering internal head geometry.

---

# 4. Use Dedicated First-Person Arms

This is the most important correction.

Create/use a **separate first-person arm rig**.

The first-person arms should NOT simply be the world character's arms viewed from the camera.

They should be a dedicated visual rig used only by the local player's first-person presentation.

Conceptually:

```text
FirstPersonCamera
    └── FirstPersonRig
          ├── FirstPersonWeapon
          └── FirstPersonArms
```

Exact hierarchy may differ depending on the current architecture, but this separation needs to exist conceptually.

The first-person arms should:

* move with the first-person weapon system,
* remain correctly positioned relative to the camera,
* visually hold the weapon,
* support ADS,
* support hip-fire,
* support weapon lowering during reload,
* support weapon switching,
* support sprint positioning.

They should NOT hang down at the character's sides.

---

# 5. Anchor the Hands to the Weapon

The hands need explicit grip targets.

Each weapon should support transforms similar to:

```text
RightHandGrip
LeftHandGrip
```

For example:

```text
FirstPersonWeapon
    ├── RightHandGrip
    └── LeftHandGrip
```

The arm/hand system should then position the hands against these transforms.

Prefer:

* IK,
* procedural hand placement,
* reusable weapon grip anchors,

rather than manually creating bespoke arm animations for every gun.

The dominant hand should remain convincingly attached to the primary weapon grip.

The support hand should remain attached to the forward/support grip where applicable.

---

# 6. Do Not Drive First-Person Arms From Third-Person Arm Pose

The third-person character may have:

```text
LongGun_Hold
ShortGun_Hold
HeavyGun_Hold
```

or similar world-view animation poses.

Those poses are useful for what OTHER PLAYERS see.

They should not determine the exact first-person arm pose.

First-person arms should have their own pose/presentation logic optimized for the camera.

Both systems can share gameplay state such as:

```text
CurrentWeapon
WeaponType
IsAiming
IsSprinting
IsReloading
IsCrouched
IsProne
```

but they do not need to share identical arm transforms.

---

# 7. First-Person Torso

Do not assume the torso must be completely removed.

We want the player to feel like they inhabit a body.

When looking downward, some torso/waist visibility may be appropriate.

However, the torso should not:

* obstruct normal aiming,
* expose an open neck hole,
* clip severely into the camera,
* overlap the first-person arms.

Implement whichever torso visibility works best with the new architecture.

The critical requirement is:

> Lower-body awareness should remain while the first-person arm system is visually independent.

---

# 8. Weapon Reload Interaction

REQ-069 established that reloads should use the simplified weapon-lowering system.

The first-person arms should move WITH the weapon when it lowers.

Do not leave floating hands on screen while the weapon disappears.

Conceptually:

```text
Normal:
arms + weapon visible

Reload begins:
arms + weapon move downward together

Reload:
arms + weapon off-screen

Reload finishes:
arms + weapon return together
```

No detailed magazine animation is required.

---

# 9. Sprinting

When sprinting:

* the lower body continues using normal locomotion animation,
* first-person arms and weapon should transition into the first-person sprint presentation,
* the arms should not revert to hanging beside the world character.

This same principle applies to:

* jumping,
* landing,
* ADS,
* weapon switching,
* reloading.

The lower body and first-person arms are related through gameplay state but are different visual systems.

---

# 10. Networking

Do not network the dedicated first-person arm presentation.

Other players do not need to know:

* exact local first-person arm transform,
* camera-relative gun position,
* local hand IK,
* local weapon bob,
* local reload-lowering interpolation.

These are local presentation effects.

Continue networking the gameplay/world states already required for remote representation.

---

# 11. Desired Result

After this correction:

### Looking forward

I should see:

* first-person weapon,
* hands/arms convincingly holding the weapon.

### Looking downward

I should see:

* my hips/lower torso as appropriate,
* my legs,
* my feet,

with locomotion resembling what other players see.

I should NOT see:

* inside my neck,
* inside my head,
* an empty/open neck cavity,
* third-person arms hanging beside me,
* third-person arms attempting to line up with the first-person weapon,
* floating first-person guns without hands.

### Other players

Other players should continue seeing the complete normal world-view player.

---

# Implementation Guidance

Before making more mesh visibility changes, inspect the current first-person/third-person hierarchy, camera culling, Animator setup, weapon hierarchy, and player prefab.

Then restructure the system around this principle:

> **Shared lower-body/world locomotion + dedicated first-person camera arms.**

Please avoid another quick fix that merely hides additional bones/renderers.

If the existing architecture makes this difficult, refactor it enough to establish the correct separation now so we do not continue building future weapon animation work on top of the wrong architecture.
