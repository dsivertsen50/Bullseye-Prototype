# REQ-047 — Third-Person Weapon Rig & Upper-Body Animation Foundation

## Objective

Rebuild the third-person/world-player weapon handling system so equipped weapons are correctly anchored to the player character, hands remain properly positioned on weapons, aiming and recoil look believable, and weapon presentation works correctly across standing, crouching, sprinting, and prone states.

The current third-person arm/weapon animations rely too heavily on manually adjusted Mixamo animations. This has created several visual problems:

* Weapons are not parented/anchored to the player's right hand.
* Weapon positioning has required extensive manual adjustment.
* Firing recoil was disabled or reduced because the weapon kicked in the wrong direction.
* Third-person aiming/ADS animation was previously poor enough that it was removed.
* While prone, the player's weapon can appear to float independently of the arms.
* The player's arms continue following the underlying Mixamo animation even when the resulting weapon position does not make sense.
* Different weapons will become increasingly difficult to support if every animation requires manual positioning.

REQ-047 should establish a reusable third-person weapon-rig architecture rather than continue correcting individual animation clips manually.

---

# Scope

This requirement applies primarily to the **third-person/world representation of players** seen by other players.

The existing first-person weapon system should remain separate.

Changes made under this requirement must not unintentionally alter:

* First-person weapon position
* First-person ADS behavior
* First-person camera behavior
* First-person weapon sway
* First-person recoil
* First-person reload presentation

Both first-person and third-person systems may consume the same gameplay states, such as:

* Current weapon
* IsAiming
* IsFiring
* IsReloading
* IsSprinting
* IsCrouching
* IsProne
* Aim direction
* Aim pitch

However, their visual implementations should remain independent.

---

# 1. Create a Right-Hand Weapon Socket

Equipped world-player weapons must no longer float independently underneath the player prefab.

Create a dedicated transform named approximately:

`WeaponSocket`

The socket should be attached beneath the player's **right-hand bone**.

Example hierarchy:

```text
Player Character
└── Armature
    └── ...
        └── RightHand
            └── WeaponSocket
                └── EquippedWorldWeapon
```

The actual weapon should be instantiated or attached beneath `WeaponSocket`.

Do not require the weapon model itself to have zero local position/rotation relative to the hand.

Instead, allow configurable weapon-specific offsets.

Each weapon may require:

* Local position offset
* Local rotation offset
* Optional local scale adjustment

For example, the pistol, AK, shotgun, DMR, and future sniper rifle may all sit differently in the player's hand.

The system should allow these offsets to be configured without modifying animation clips.

---

# 2. Weapon-Specific Third-Person Grip Configuration

Each weapon prefab should support third-person grip information.

At minimum, each world weapon should provide:

* Primary/right-hand attachment orientation
* Left-hand grip target
* Muzzle transform

Recommended hierarchy:

```text
DMR
├── Mesh
├── MuzzlePoint
└── LeftHandGrip
```

`LeftHandGrip` should be an empty transform placed where the player's support hand should contact the weapon.

This allows every weapon to define its own correct support-hand position.

Examples:

* Pistol may use a close two-handed grip.
* AK may place the left hand underneath the handguard.
* Shotgun may place the left hand farther forward.
* DMR may have a slightly extended support-hand position.
* Sniper rifle may eventually use an even farther-forward grip.

These should be configurable by repositioning transforms rather than modifying animation code.

---

# 3. Add Left-Arm IK

Use Unity's Animation Rigging system, or an equivalent appropriate solution, to keep the player's left hand attached to the weapon's `LeftHandGrip`.

The left arm should use a Two Bone IK-style chain:

```text
LeftUpperArm
    ↓
LeftLowerArm
    ↓
LeftHand
```

The end target should be the currently equipped weapon's `LeftHandGrip`.

An elbow hint may be used if necessary to maintain believable elbow orientation.

The IK should operate on top of the player's underlying animation.

This allows locomotion animations to continue while ensuring that the player's support hand does not:

* Float beside the gun
* Clip through the gun
* Lose contact during locomotion
* Remain in the wrong position when switching weapons

Weapon switching should automatically update the left-hand IK target.

---

# 4. Separate Locomotion From Upper-Body Weapon Presentation

The current system should be reorganized so that Mixamo locomotion animations are not solely responsible for weapon handling.

The Animator should conceptually separate:

### Base Locomotion Layer

Examples:

* Idle
* Walk
* Run
* Sprint
* Jump
* Fall
* Land
* Crouch
* Prone
* Prone movement
* Wall run
* Dolphin dive

### Upper-Body Weapon Layer

Examples:

* Weapon idle / ready pose
* Aiming pose
* Firing reaction
* Reload
* Sprint weapon posture
* Prone weapon posture

Use an appropriate Avatar Mask so the weapon layer affects primarily:

* Spine
* Chest
* Shoulders
* Upper arms
* Lower arms
* Hands

The exact bones included may be adjusted if necessary for natural blending.

The goal is to allow the lower body to continue performing locomotion while the upper body independently maintains believable weapon handling.

Example:

```text
Lower Body:
Sprint animation

Upper Body:
Rifle sprint / carry pose
```

or:

```text
Lower Body:
Prone crawl

Upper Body:
Prone rifle pose
```

Avoid forcing every locomotion animation to contain perfect gun-handling animation.

---

# 5. Restore Third-Person Aiming / ADS Presentation

Third-person players should visibly transition into an aiming posture when they press the Aim/Zoom input.

This does NOT need to reproduce the exact first-person ADS positioning.

Instead, other players should see the character:

* Raise the weapon
* Bring the stock toward the shoulder for rifles
* Bring the hands into a deliberate firing position
* Reposition the shoulders/elbows appropriately
* Orient the torso and weapon toward the player's aim direction

The aim transition should blend smoothly rather than instantly snapping.

Conceptually:

```text
Hip Fire / Ready Pose
        ↓
    Aim Input
        ↓
Third-Person Aim Pose
```

An aim weight between approximately `0` and `1` should be supported so aiming can smoothly interpolate.

Example conceptual values:

```text
Not aiming:
AimWeight = 0

Fully aiming:
AimWeight = 1
```

Do not restore the previous aiming animation if it produces obviously incorrect arm or weapon positioning.

The objective is to establish a new reliable aiming system.

---

# 6. Add World-Player Aim Direction / Aim Pitch

The world player's weapon should respond to the direction the local player is actually aiming.

Currently, the player's body orientation may not sufficiently communicate vertical aiming.

Add support for third-person aim pitch.

For example:

* Looking upward should cause the weapon and upper torso to pitch upward.
* Looking downward should cause the weapon and upper torso to pitch downward.

This should primarily influence the upper body rather than rotating the entire character unnaturally.

A dedicated `AimTarget` or equivalent procedural target may be used.

Conceptually:

```text
Player Camera / Aim Direction
            ↓
       Networked Aim Pitch
            ↓
Third-Person Upper Body
            ↓
       Weapon Direction
```

The system must remain compatible with multiplayer.

Do not network arbitrary IK transform positions every frame if the existing gameplay aim data can be used to reconstruct the visual pose locally.

---

# 7. Correct Third-Person Weapon Recoil

Restore visible recoil when a world-player weapon fires.

Previous recoil behavior was removed or weakened because the gun moved in an incorrect direction.

Recoil should now be implemented relative to the correct weapon orientation rather than relying on incorrect world-space movement.

On firing, the third-person weapon should visually:

1. Kick slightly backward toward the player.
2. Rotate slightly upward.
3. Recover toward its resting position.

The recoil should be quick but visible.

Different weapons should eventually support different recoil profiles.

Example:

### Pistol

* Moderate upward snap
* Small rearward movement

### AK

* Smaller per-shot movement
* Rapid repeated impulses during automatic fire

### Shotgun

* Strong rearward and upward recoil

### DMR

* Strong, sharp recoil with controlled recovery

Values should remain configurable.

Avoid moving the weapon so far that the player's hands obviously detach from it.

Where appropriate, recoil may affect the arms/upper body as well as the weapon itself.

---

# 8. Prone Weapon Handling

Prone presentation needs special attention.

Currently, the player may continue following the Mixamo prone animation while the gun floats or moves independently.

REQ-047 should establish a dedicated **prone upper-body weapon pose**.

When the player enters prone:

* The weapon must remain anchored to the right hand.
* The left hand must remain attached through IK.
* The weapon should remain oriented generally forward.
* The torso/shoulders should assume a believable firing posture.
* The gun should no longer float independently above/beside the player's arms.

A polished animated prone firing sequence is NOT required for this ticket.

A stable and believable static/blended upper-body prone rifle pose is acceptable as the initial implementation.

The lower body may continue using the existing Mixamo prone/crawl animation.

Conceptually:

```text
Lower Body:
Existing Prone / Crawl Animation

Upper Body:
Dedicated Prone Weapon Pose

Left Hand:
IK → Weapon Grip

Right Hand:
WeaponSocket
```

This system should allow later prone animation improvements without rebuilding the entire weapon architecture.

---

# 9. Sprinting Weapon Presentation

Ensure the new rig remains compatible with sprinting.

When sprinting:

* Weapon should remain attached correctly.
* Hands should remain convincingly connected.
* Weapon should not violently snap because IK is competing with the sprint animation.
* The upper body may transition into a weapon-specific sprint/carry pose.

If necessary, the weight of certain IK or aiming constraints may be reduced during sprinting.

Players should not normally appear to ADS while fully sprinting unless gameplay explicitly allows it.

---

# 10. Crouching and Other Stances

The weapon rig should continue functioning while:

* Standing
* Walking
* Running
* Sprinting
* Crouching
* Moving while crouched
* Prone
* Moving while prone
* Jumping
* Falling

The system should be designed so future actions such as:

* Wall running
* Dolphin diving
* Sliding
* Mantling

can temporarily override or blend weapon posing without requiring the weapon to be manually repositioned.

---

# 11. Weapon Switching

When the player changes their secondary weapon:

1. Remove/deactivate the previous third-person world weapon.
2. Attach the new weapon to the same right-hand `WeaponSocket`.
3. Apply that weapon's configured socket offset.
4. Update the left-hand IK target.
5. Update weapon-specific animation/recoil configuration.
6. Maintain correct multiplayer replication.

Weapon switching should not require modifying the player's skeleton or Animator Controller for every new gun.

---

# 12. Multiplayer Requirements

The authoritative gameplay behavior of weapons should remain unchanged.

Third-person rigging is primarily visual.

Remote clients must see appropriate:

* Equipped weapon
* Aiming state
* Firing state
* Reloading state
* Stance
* Weapon switching
* Aim direction / aim pitch
* Recoil response

Avoid synchronizing IK bone transforms directly across the network unless absolutely necessary.

Prefer synchronizing gameplay state and reconstructing the visual rig locally.

For example:

```text
Network:
Player fired DMR

Remote Client:
Play DMR third-person recoil locally
```

This should minimize unnecessary network traffic.

---

# 13. Do Not Break First-Person Presentation

The local player's first-person weapon rig should remain independent.

The local player may have:

```text
FirstPersonWeaponRig
```

while the character visible to other players uses:

```text
WorldWeaponRig
```

Do not require the first-person gun and third-person world gun to share identical transforms.

First-person presentation may intentionally use exaggerated or physically impossible positioning if it improves gameplay visibility.

---

# 14. Debugging / Editor Tools

Where practical, expose useful configuration fields in the Inspector.

Recommended adjustable values include:

* Weapon socket offset
* Weapon socket rotation
* Left-hand grip transform
* IK weight
* Aim IK weight
* Aim transition speed
* Maximum upward aim pitch
* Maximum downward aim pitch
* Recoil distance
* Recoil rotation
* Recoil recovery speed
* Prone pose blending
* Sprint pose blending

If possible, provide Scene View gizmos or clearly named transforms so the developer can easily reposition:

* WeaponSocket
* LeftHandGrip
* AimTarget
* Elbow hint

without editing code.

---

# Suggested World Player Hierarchy

The final implementation does not need to match this exactly, but the architecture should approximately support:

```text
WorldPlayer
│
├── PlayerCharacter
│   └── Armature
│       └── ...
│           └── RightHand
│               └── WeaponSocket
│                   └── EquippedWorldWeapon
│                       ├── MuzzlePoint
│                       └── LeftHandGrip
│
├── Animator
│   ├── Base Locomotion Layer
│   └── Upper Body Weapon Layer
│
├── RigBuilder
│   └── WeaponRig
│       ├── LeftArmIK
│       └── UpperBodyAimRig
│
├── AimTarget
│
└── WorldWeaponAnimationController
```

Exact class and GameObject names may vary according to the existing project architecture.

---

# 15. Implementation Priority

Implement this requirement incrementally in the following order:

### Phase 1 — Weapon Attachment

* Create right-hand WeaponSocket.
* Attach equipped world weapon.
* Configure weapon offsets.
* Verify weapon remains attached during existing animations.

### Phase 2 — Left-Hand IK

* Add weapon-specific `LeftHandGrip`.
* Add left-arm IK.
* Verify support hand maintains weapon contact.

### Phase 3 — Animation Layer Separation

* Establish upper-body weapon layer.
* Add appropriate Avatar Mask.
* Ensure locomotion continues underneath.

### Phase 4 — Aiming

* Restore third-person aiming.
* Blend weapon toward shoulder/firing posture.
* Add vertical aim pitch.

### Phase 5 — Recoil

* Add procedural world-player recoil.
* Correct recoil direction.
* Add configurable weapon recoil profiles.

### Phase 6 — Prone

* Establish dedicated prone upper-body weapon pose.
* Ensure both hands remain connected to weapon.
* Remove floating-gun behavior.

Do not attempt to perfect every animation before the foundational rig works correctly.

---

# Acceptance Criteria

REQ-047 is complete when:

* [ ] Equipped third-person weapons are attached to a socket beneath the player's right-hand bone.
* [ ] Weapons no longer float independently of the player's hands.
* [ ] Each weapon can define its own right-hand positional/rotational offset.
* [ ] Each supported weapon contains or references a configurable left-hand grip target.
* [ ] The player's left hand follows the weapon through IK.
* [ ] Switching weapons correctly updates the world weapon and left-hand grip.
* [ ] Locomotion and upper-body weapon presentation are separated sufficiently to prevent Mixamo locomotion from fully controlling weapon handling.
* [ ] Third-person aiming is restored and looks substantially more believable than the previous removed ADS animation.
* [ ] Aiming smoothly transitions rather than snapping.
* [ ] The world player's weapon/upper torso visually responds to vertical aim direction.
* [ ] Firing produces correctly directed visible recoil.
* [ ] Recoil does not cause the weapon to visibly detach from the player's hands.
* [ ] Prone players maintain a believable weapon position.
* [ ] The weapon no longer floats while prone.
* [ ] Existing standing, walking, sprinting, crouching, jumping, and prone locomotion remain functional.
* [ ] Remote multiplayer clients see the appropriate weapon, aiming, firing, stance, and recoil behavior.
* [ ] First-person weapon positioning and first-person ADS behavior remain unaffected.
* [ ] The system is reusable for future weapons without requiring extensive manual modification of every locomotion animation.

---

# Out of Scope

The following do not need to be fully completed under REQ-047:

* Perfect final-quality reload animations
* Unique reload animations for every weapon
* Final weapon-specific sprint animations
* Final prone crawl animations
* Finger animation
* Detailed hand/finger gripping
* Procedural foot placement
* Final wall-running weapon animation
* Final dolphin-dive weapon animation
* Final sniper rifle animation
* Weapon clipping prevention against nearby walls
* Full-body additive recoil polish
* Final animation sound design

These may be addressed in later requirements once the new weapon-rig foundation is stable.

---

# Development Note

The primary goal of REQ-047 is **not to find better Mixamo animations**.

Mixamo locomotion animations should continue providing useful base body movement, but weapon positioning should no longer depend on each source animation already having perfect rifle or pistol hand placement.

The new system should combine:

**Base Animation + Weapon Socket + Upper-Body Pose + IK + Procedural Aim/Recoil**

so that future weapons and movement mechanics can reuse the same architecture.

When choosing between additional manual animation offsets and a reusable rig-driven solution, prefer the reusable rig-driven solution.
