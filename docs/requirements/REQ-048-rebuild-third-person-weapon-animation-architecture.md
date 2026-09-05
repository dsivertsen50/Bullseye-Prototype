# REQ-048 — Rebuild Third-Person Weapon Animation Architecture

## Summary

Replace the current fragile/manual third-person weapon posing system with a layered animation architecture based on:

1. Existing locomotion animations for whole-body movement.
2. Weapon-class upper-body poses where needed.
3. A stable dominant-hand weapon attachment.
4. Two-bone IK for the supporting hand.
5. Elbow hints for natural arm bending.
6. Rig weighting/blending for movement states such as sprinting, aiming, prone, reloads, and recoil.

The purpose of this ticket is **not to perfect every animation immediately**. The purpose is to establish a reliable architecture that makes future weapon positioning and animation substantially easier.

The existing custom weapon/arm positioning tool may be deprecated or simplified if it conflicts with this architecture.

---

# Problem

The current third-person weapon animation workflow is proving extremely difficult to maintain.

Current issues include:

* Weapons can become disconnected from the player's hands.
* Weapon positioning requires excessive manual adjustment.
* Moving a weapon may undesirably move the hand or arm.
* Arm, elbow, hand, and weapon offsets have become interdependent.
* Different locomotion states require repeated manual corrections.
* Sprinting, crouching, prone, aiming, and firing can produce noticeably incorrect arm poses.
* Recoil currently causes excessive arm movement/flailing instead of believable weapon recoil.
* Adding additional weapons will greatly multiply the amount of manual adjustment required.
* The custom positioning/editor tool created during the previous animation work is unreliable and should not become a core dependency.

The current approach appears to be attempting to author too much of the final character pose through per-weapon transforms.

We should instead allow animation and IK to solve most of the character's pose.

---

# Primary Goal

Create a scalable third-person weapon animation architecture where:

> **Locomotion controls the character body, the dominant hand carries the weapon, and IK places the support hand onto the weapon.**

The system should make it possible to configure a new long gun primarily by adjusting:

* its dominant/right-hand attachment offset;
* a support/left-hand grip target;
* an elbow hint;

rather than manually positioning both arms.

---

# Scope Strategy

Do **not** attempt to convert every weapon during the initial implementation.

The first implementation/proof of concept should use:

> **AK / Rifle only**

and demonstrate that the architecture works correctly through:

* Idle
* Walking
* Strafing
* Sprinting
* Crouching
* Prone
* Jumping
* Normal aiming where currently supported

Only after the AK works acceptably should this architecture be extended to:

* DMR
* Shotgun
* Pistol

Avoid simultaneously changing every weapon.

---

# 1. Preserve Existing Base Locomotion

Existing character locomotion animations should continue to drive the player's fundamental movement.

Examples include:

* Idle
* Walk
* Run
* Sprint
* Strafe
* Crouch
* Prone
* Jump
* Wall run
* Dolphin dive

REQ-048 should **not** require separate complete locomotion animation sets for every weapon.

Do not create systems equivalent to:

* AK_Walk
* AK_Run
* AK_CrouchWalk
* AK_Strafe
* DMR_Walk
* DMR_Run
* Shotgun_Walk
* Shotgun_Run
* etc.

Weapon handling should instead be layered on top of the existing locomotion system wherever practical.

---

# 2. Add a Dedicated Third-Person Weapon Animation Rig

Use Unity's Animation Rigging system for the third-person/world player.

Create a clearly organized hierarchy, conceptually similar to:

```text
Player
├── Character Rig / Armature
├── Animator
└── RigBuilder
    └── ThirdPersonWeaponRig
        ├── LeftHandIK
        ├── LeftElbowHint
        └── Optional Aim Rig
```

Use Unity-supported rig constraints rather than manually driving character bones wherever possible.

At minimum, support:

* Two Bone IK for the left/support arm.
* IK target weight control.
* IK hint weight control.
* Smooth rig weight blending.

Avoid writing custom inverse-kinematics math unless Unity's standard Animation Rigging components are demonstrably insufficient.

---

# 3. Dominant Hand Owns the Weapon

For right-handed weapons, establish the following relationship:

```text
Right Hand
    ↓
Weapon Socket
    ↓
Weapon
    ↓
Left Hand Grip Target
```

The weapon should remain stably attached to a dedicated socket associated with the player's right hand.

The right-hand attachment should be considered the primary weapon attachment.

Do not make both hands independently attempt to determine the weapon transform.

Moving the left-hand IK target should never move the weapon.

The intended dependency should be:

> Right hand moves weapon → weapon moves left-hand target → IK moves left arm to target.

Not:

> Weapon and both hands continuously fight each other for position.

---

# 4. Create a Right-Hand Weapon Socket

Add a dedicated transform such as:

```text
RightHandWeaponSocket
```

The equipped third-person weapon should attach to this socket.

Each WeaponDefinition should be capable of specifying a small local position/rotation offset relative to this socket if different weapons require different alignment.

Suggested configuration fields:

```text
ThirdPersonWeaponPositionOffset
ThirdPersonWeaponRotationOffset
```

These offsets should control the **weapon relative to the hand/socket**, not directly modify arm bones.

The weapon should remain correctly parented and anchored during:

* locomotion;
* aim;
* crouch;
* prone;
* jumps;
* weapon firing;
* network replication.

---

# 5. Add Weapon-Specific Left-Hand Grip Targets

Every two-handed weapon prefab should have a dedicated child transform such as:

```text
LeftHandGrip
```

Example:

```text
AK
├── Muzzle
├── LeftHandGrip
└── Other Weapon Components
```

`LeftHandGrip` should be positioned where the player's left/support hand should naturally hold that specific weapon.

Examples:

* AK → foregrip/handguard
* DMR → forward handguard
* Shotgun → pump/fore-end
* Future sniper → rifle fore-end

The character's left hand should use Two Bone IK to follow this transform.

This allows weapon setup to be performed by moving a simple target on the weapon rather than manually manipulating arm bones.

---

# 6. Add Left Elbow Hint Support

Each two-handed weapon should also support an elbow hint target.

Example:

```text
AK
├── LeftHandGrip
└── LeftElbowHint
```

Alternatively, elbow hints may live on the character if that produces cleaner architecture, provided weapons can specify or influence their position.

Use this target as the Hint for the left-arm Two Bone IK constraint.

The goal is to prevent:

* elbows bending backward;
* elbows sticking unnaturally outward;
* arms collapsing into the torso;
* unpredictable elbow direction.

The elbow hint should be easily adjustable in the Unity Scene view.

---

# 7. Left Arm Two Bone IK

Create a Two Bone IK constraint for:

```text
LeftUpperArm
    ↓
LeftLowerArm / Forearm
    ↓
LeftHand
```

Target:

```text
EquippedWeapon.LeftHandGrip
```

Hint:

```text
EquippedWeapon.LeftElbowHint
```

When a compatible two-handed weapon is equipped:

```text
LeftHandIK weight → 1
```

When no two-handed weapon is equipped, the system should gracefully disable or reduce this constraint.

Do not directly rotate the left shoulder, elbow, and wrist every frame to match weapon-specific values.

---

# 8. Weapon Classes / Shared Poses

Introduce a concept of broad third-person weapon pose categories.

Initial categories should be:

```text
Pistol
LongGun
```

Potential future categories may include:

```text
Heavy
Launcher
Melee
```

The current weapons should roughly map as:

```text
Pistol → Pistol

AK → LongGun
DMR → LongGun
Shotgun → LongGun
Future Sniper → LongGun
```

The purpose is to avoid creating unique animation states for every individual weapon when several weapons can reasonably share a similar upper-body posture.

---

# 9. Upper-Body Weapon Pose Layer

Create or configure an Animator layer for third-person weapon handling.

Use an Avatar Mask so that weapon-ready poses primarily affect appropriate upper-body bones rather than replacing locomotion in the legs.

Potential affected regions may include:

* spine;
* chest;
* shoulders;
* upper arms;
* lower arms;
* hands.

The exact mask should be adjusted carefully so that existing locomotion remains natural.

Potential shared poses:

```text
Pistol Ready
Long Gun Ready
Pistol Sprint
Long Gun Sprint
Long Gun Prone
```

Do not create more poses than needed during this ticket.

For the initial AK proof-of-concept, prioritize:

```text
LongGunReady
LongGunSprint
```

and only create a separate prone upper-body pose if regular `LongGunReady + IK` does not look acceptable when prone.

---

# 10. Movement Should Continue Under the Weapon Pose

The player should retain normal locomotion movement while the upper body maintains an appropriate weapon posture.

Example:

```text
Walk animation
+
LongGunReady upper-body layer
+
LeftHand IK
```

Likewise:

```text
Crouch Walk
+
LongGunReady upper-body layer
+
LeftHand IK
```

The system should not require manually authoring AK-specific versions of the walking/crouching animations.

---

# 11. Sprint Handling

Sprinting may intentionally use a different weapon posture.

When the player enters sprint:

* Transition toward an appropriate sprint weapon pose.
* Allow IK weighting to change if necessary.
* Do not abruptly snap the arms between poses.

For example:

```text
Normal:
LongGunReady
LeftHandIK Weight = 1.0
```

Transitioning into sprint may use:

```text
LongGunSprint
LeftHandIK Weight = configurable
```

The exact weight should be visually tuned rather than hardcoded as a required value.

When leaving sprint:

* smoothly restore the normal weapon-ready pose;
* smoothly restore normal IK weight.

---

# 12. Crouch Handling

Crouching should primarily continue using the crouch locomotion animations.

Where possible:

```text
Crouch Locomotion
+
LongGunReady
+
IK
```

Avoid creating an entirely separate set of crouch animations for each weapon.

Create a crouch-specific upper-body override only if the normal long-gun pose cannot produce acceptable results.

---

# 13. Prone Handling

Prone may legitimately require a distinct upper-body posture because the geometry of the player's body changes substantially.

Support an optional:

```text
LongGunProne
```

upper-body pose.

The weapon should:

* remain in the player's hands;
* not float above the ground excessively;
* not intersect badly with the torso;
* retain usable left-hand IK.

Do not create unique prone poses for AK, DMR, shotgun, etc. unless a specific weapon proves incompatible with the shared long-gun prone pose.

---

# 14. Aim / Pitch Architecture

Third-person aiming should ultimately involve the character's torso rather than only rotating/flailing the arms.

Where practical within this ticket, establish an aim rig that can distribute vertical aiming through appropriate upper-body bones.

Potential bones:

```text
Spine
Chest
UpperChest
```

Use an appropriate Unity Animation Rigging aim constraint.

The intended behavior is:

```text
Player Aim Direction
      ↓
Torso / Spine Aim Adjustment
      ↓
Right Hand / Weapon
      ↓
Left Hand IK follows weapon
```

Avoid rotating each arm independently toward the target.

If implementing full pitch/aim rigging would substantially expand the ticket, establish the architecture/hooks now and leave final tuning for a later ticket.

---

# 15. Recoil Architecture

Remove or disable third-person recoil implementations that cause the arms to independently flail outward.

Recoil should conceptually originate from the weapon or from a small additive upper-body recoil motion.

Preferred relationship:

```text
Weapon recoils backward/upward
        ↓
Hands remain attached
        ↓
Arms respond naturally
```

rather than:

```text
Shot
↓
Rotate both arms outward
```

For this ticket, prioritize stable weapon/hand relationships over sophisticated recoil.

It is acceptable for third-person recoil to temporarily be subtle if necessary.

Do not retain obviously broken arm-flailing recoil merely because it currently exists.

---

# 16. Reload Compatibility

The architecture must allow future or existing reload animations to temporarily override the support-hand IK.

During reload animations, the system should be capable of:

```text
LeftHandIK weight:
1 → 0
```

allowing the reload animation to move the hand.

Once the hand returns to the weapon:

```text
LeftHandIK weight:
0 → 1
```

This transition must support smooth interpolation rather than sudden snapping.

REQ-048 does not need to completely rebuild every reload animation unless the existing reload system prevents the new architecture from functioning.

---

# 17. Animation Rig Weight Blending

Do not abruptly enable and disable IK/rig systems wherever avoidable.

Support smooth configurable blending of:

* overall weapon rig weight;
* left-hand IK weight;
* elbow hint weight;
* aim rig weight.

Transitions should occur over short configurable durations.

Avoid visible snapping when:

* equipping weapons;
* sprinting;
* stopping sprint;
* aiming;
* beginning reload;
* ending reload;
* entering prone;
* leaving prone.

---

# 18. Editing Workflow Must Work Outside Play Mode

One of the major pain points in the current workflow is having to launch multiplayer clients simply to see or adjust weapon alignment.

REQ-048 must establish a workflow where the important weapon positioning targets can be edited directly in the Unity Scene view.

At minimum, the developer should be able to visually adjust:

```text
RightHandWeaponSocket / weapon offset
LeftHandGrip
LeftElbowHint
```

without needing to run two multiplayer clients.

If necessary, create a simple **editor-only preview mode**.

However:

> Do not build another complex custom animation editor.

A preview tool should only expose the minimum functionality needed to preview the equipped weapon and pose.

Prefer Unity's existing animation preview/Animation Rigging capabilities over custom tooling.

---

# 19. Simplify or Deprecate Existing Custom Positioning Tool

Review the custom arm/weapon positioning system created during REQ-047.

If it directly manipulates:

* shoulder rotations;
* upper-arm rotations;
* elbow rotations;
* wrist rotations;
* hand transforms;

on a per-weapon basis, it should no longer be treated as the primary solution.

Preserve only portions that remain useful under the new architecture.

For example, it may be simplified to editing:

```text
Weapon offset
LeftHandGrip
LeftElbowHint
```

Do not continue expanding the old tool merely to preserve previous work.

Architecture correctness is more important than backward compatibility with a prototype editor.

---

# 20. WeaponDefinition Integration

Extend the existing WeaponDefinition architecture rather than creating disconnected weapon-specific scripts.

Add only fields that are genuinely necessary.

Possible data:

```text
ThirdPersonPoseCategory
ThirdPersonWeaponPositionOffset
ThirdPersonWeaponRotationOffset
SupportHandIKEnabled
IKBlendSpeed
```

Whenever possible, actual grip transforms should live on the weapon prefab rather than storing arbitrary Vector3 values in code.

For example:

```text
WeaponPrefab
└── LeftHandGrip
```

is preferable to:

```text
leftHandPositionX
leftHandPositionY
leftHandPositionZ
leftHandRotationX
...
```

Avoid large collections of numeric pose fields.

---

# 21. Network Compatibility

Bullseye is multiplayer.

The third-person animation system must remain compatible with Netcode for GameObjects.

The world player observed by remote clients must correctly display:

* equipped weapon;
* locomotion;
* crouch/prone state;
* sprint state;
* aim state where already networked;
* firing;
* appropriate weapon pose.

Do not network individual IK bone transforms unless absolutely necessary.

Prefer to synchronize underlying gameplay state and allow each client to locally evaluate the Animator/Animation Rigging result.

For example:

```text
Network:
Weapon ID = AK
Movement State = Walk
Aim Pitch = X
IsSprinting = false
```

should be preferable to synchronizing:

```text
LeftElbowRotation
LeftWristRotation
RightShoulderRotation
...
```

---

# 22. Local First-Person View Must Not Be Broken

Bullseye has separate concerns for:

* the local player's first-person view;
* the third-person/world character observed by other players.

REQ-048 is primarily a **third-person/world-animation ticket**.

Do not unintentionally replace or destabilize the existing first-person weapon system.

The local first-person arms/gun may continue using their existing implementation unless a shared architectural change is clearly necessary.

Any shared code changes must preserve current first-person:

* firing;
* aiming;
* zoom;
* reload;
* weapon switching;
* recoil;
* HUD behavior.

---

# 23. Character Rig Protection

Do not modify or destroy the existing character skeleton/Avatar configuration without a clear need.

Avoid:

* renaming bones unnecessarily;
* changing humanoid mappings arbitrarily;
* modifying imported FBX data destructively;
* embedding gameplay scripts directly into imported FBX assets.

Use prefab instances, rig objects, and child transforms where possible.

---

# 24. Development Phases

## Phase 1 — Architecture

Implement:

* RigBuilder;
* weapon rig;
* right-hand socket;
* left-hand Two Bone IK;
* elbow hint;
* weapon prefab grip target support.

Use AK only.

Confirm the weapon can remain held correctly while standing.

---

## Phase 2 — Basic Locomotion

Test AK with:

* Idle
* Walk forward
* Walk backward
* Strafe left
* Strafe right
* Run if separate
* Jump

The support hand should remain on the rifle.

---

## Phase 3 — Alternate Stances

Test:

* Crouch
* Crouch movement
* Prone
* Prone movement

Only add stance-specific upper-body poses where clearly needed.

---

## Phase 4 — Sprint

Implement and tune:

```text
LongGunSprint
```

with appropriate rig-weight blending.

Ensure entering/exiting sprint is smooth.

---

## Phase 5 — Aim and Fire Compatibility

Verify:

* aim state;
* weapon orientation;
* firing;
* recoil;
* remote client appearance.

Remove/disable any obviously broken arm-flailing recoil.

---

## Phase 6 — Expand to Other Weapons

Only after AK works:

* DMR
* Shotgun
* Pistol

DMR and shotgun should preferentially reuse `LongGun` poses.

Do not duplicate animation assets without a visual/technical reason.

---

# 25. Debugging / Visualization

Provide useful debug visualization where inexpensive.

Examples:

* gizmo for `LeftHandGrip`;
* gizmo for `LeftElbowHint`;
* ability to identify current weapon pose category;
* optional rig-weight display;
* optional current movement/animation state readout.

These tools should assist development without becoming another large bespoke editor.

---

# 26. Performance

Animation Rigging should only evaluate necessary constraints.

Avoid:

* per-frame GameObject searches;
* repeated `GetComponent` calls inside update loops;
* instantiating targets every frame;
* network RPCs for IK updates;
* unnecessary runtime allocation.

Cache references appropriately.

---

# 27. Graceful Failure

If a weapon is missing its optional IK setup:

* do not throw continuous NullReferenceExceptions;
* log a useful warning once;
* fall back to a reasonable animation state;
* allow the weapon/game to continue functioning.

Example:

```text
[ThirdPersonWeaponRig] AK is configured as a two-handed weapon but no LeftHandGrip target was found.
```

---

# Non-Goals

REQ-048 does **not** require:

* final polished animation quality;
* complete procedural animation;
* replacing all Mixamo animations;
* Blender animation authoring;
* creating unique animation sets for every weapon;
* perfect reload animation for every gun;
* advanced procedural foot IK;
* complete recoil redesign;
* complete first-person animation redesign;
* animation motion matching;
* purchasing third-party IK/animation packages;
* rebuilding the character model or skeleton.

The primary goal is architectural stability and a dramatically easier weapon-authoring workflow.

---

# Acceptance Criteria

REQ-048 is complete when all of the following are true.

## Architecture

* [ ] Unity Animation Rigging is used for the new third-person weapon rig.
* [ ] The weapon is primarily attached to the dominant/right hand.
* [ ] A dedicated right-hand weapon socket exists.
* [ ] Two-handed weapons support a `LeftHandGrip` target.
* [ ] Left-arm Two Bone IK targets the equipped weapon's `LeftHandGrip`.
* [ ] An elbow hint controls support-arm bending.
* [ ] IK can be smoothly weighted/blended.

## AK Proof of Concept

With the AK equipped:

* [ ] weapon stays securely in the right hand;
* [ ] left hand remains reasonably attached to the foregrip;
* [ ] elbow bends naturally;
* [ ] arms do not visibly flail;
* [ ] weapon does not detach during movement.

Verify during:

* [ ] Idle
* [ ] Walk forward
* [ ] Walk backward
* [ ] Strafe left
* [ ] Strafe right
* [ ] Sprint
* [ ] Crouch
* [ ] Crouch movement
* [ ] Prone
* [ ] Prone movement
* [ ] Jump

## Editing

* [ ] Weapon-to-hand alignment can be adjusted from the Unity editor.
* [ ] `LeftHandGrip` can be visually repositioned in Scene view.
* [ ] `LeftElbowHint` can be visually repositioned in Scene view.
* [ ] Basic weapon alignment does not require modifying individual arm bones.
* [ ] Basic positioning does not require launching two multiplayer clients.

## Animation Layering

* [ ] Base locomotion remains functional.
* [ ] Long-gun pose is layered on top rather than replacing all locomotion.
* [ ] Sprint can use a separate upper-body posture where appropriate.
* [ ] Prone works without the weapon floating far away from the body.
* [ ] Movement transitions do not cause major snapping.

## Multiplayer

In a Host + Client test:

* [ ] Host sees client's AK correctly held.
* [ ] Client sees host's AK correctly held.
* [ ] Remote locomotion does not break the rig.
* [ ] Firing does not detach the gun.
* [ ] Crouching does not detach the gun.
* [ ] Sprinting does not detach the gun.
* [ ] Prone does not detach the gun.

## Regression

Existing gameplay remains functional:

* [ ] weapon switching;
* [ ] firing;
* [ ] hit detection;
* [ ] damage;
* [ ] reload;
* [ ] ammo;
* [ ] pickups;
* [ ] death;
* [ ] respawn;
* [ ] crouch;
* [ ] prone;
* [ ] sprint;
* [ ] first-person weapon presentation.

---

# Important Implementation Guardrails for Cursor

**Do not solve visual problems by continuously adding more per-bone offsets.**

If a pose looks wrong, first investigate:

1. base animation;
2. upper-body pose;
3. weapon socket;
4. grip target;
5. elbow hint;
6. IK weight;
7. rig ordering.

Do not immediately add another manually configurable shoulder/elbow/wrist rotation.

---

**Do not convert every weapon at once.**

Make the AK work first.

---

**Do not rebuild working gameplay systems unnecessarily.**

This ticket is an animation architecture change, not a weapon-system rewrite.

---

**Do not synchronize individual arm bones over the network.**

Synchronize gameplay state and let the animation system solve the pose locally.

---

**Do not create a large new custom editor.**

The goal of this ticket is partially to eliminate dependence on fragile bespoke positioning tools.

---

**Do not manipulate imported FBX assets destructively.**

Work through prefabs, Animator configuration, rig layers, constraints, targets, and child transforms.

---

# Desired End-State

After REQ-048, adding another rifle-style weapon should eventually look approximately like this:

1. Import/model the weapon.
2. Create its prefab.
3. Assign it to a `LongGun` pose category.
4. Attach/configure it against `RightHandWeaponSocket`.
5. Adjust the weapon's local position/rotation.
6. Move `LeftHandGrip` onto the appropriate foregrip location.
7. Position `LeftElbowHint`.
8. Test.
9. Perform minor tuning if necessary.

It should **not** require individually posing:

* shoulder;
* upper arm;
* elbow;
* forearm;
* wrist;
* hand;

for every locomotion state.

The success of REQ-048 should be judged primarily by whether the new architecture makes third-person weapon posing **predictable, reusable, and substantially easier to author**.
