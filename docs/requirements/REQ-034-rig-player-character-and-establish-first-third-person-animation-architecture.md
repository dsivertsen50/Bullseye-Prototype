# REQ-034 — Rig Player Character and Establish First-/Third-Person Animation Architecture

## Summary

Prepare the new player model:

```text
Player Character V1.fbx
```

for future animation by establishing a proper humanoid character rig and a clear animation architecture for both:

1. **External / third-person presentation**
   What other players see when looking at this player.

2. **Internal / first-person presentation**
   What the local player sees through their own FPS camera.

The game is a first-person shooter, so these two presentation systems should **not be assumed to use identical animation assets or identical transforms**.

The external character requires a convincing full-body rig for multiplayer movement and combat animation.

The internal first-person presentation should remain optimized for:

* responsiveness
* weapon feel
* recoil
* reloads
* sprint motion
* aiming
* camera-relative animation

REQ-034 should establish the rigging and architectural foundation needed for both systems.

---

# 1. Primary Goal

Rig `Player Character V1` so that it can support future humanoid animations.

At minimum, the character should have a working skeletal hierarchy capable of supporting:

* idle
* walking
* running
* sprinting
* crouching
* jumping
* falling
* aiming
* weapon holding
* firing
* reloading
* grenade throwing
* death poses

Actual implementation of the complete animation library is **not required in this ticket**.

The focus of REQ-034 is:

* rigging
* Avatar configuration
* Animator readiness
* bone/socket preparation
* first-person vs third-person architecture
* multiplayer readiness

---

# 2. Important Architectural Principle

The game should treat the local first-person animation presentation and the networked external character presentation as **related but separate systems**.

Conceptually:

```text
Player Root
│
├── Gameplay / Networking
│
├── ThirdPersonVisual
│   └── Full-body humanoid character
│
└── FirstPersonVisual
    └── FPS weapon / arms presentation
```

The two visual systems should represent the same gameplay state but do not need to use identical animations.

---

# 3. Third-Person / External View

The external character is what all other players see.

This should use the full:

```text
Player Character V1
```

body.

The external character needs a humanoid skeleton capable of realistic full-body animation.

Eventually this system should support:

* feet moving during locomotion
* crouched posture
* jumping
* upper-body aiming
* weapon handling
* reloads
* grenade throws
* death poses

This is the animation system that will communicate a player's physical actions to opponents.

---

# 4. First-Person / Internal View

The local player's first-person visuals should remain optimized specifically for FPS gameplay.

The first-person presentation may include:

* weapon
* hands
* arms
* partial body if desired later

It should not be constrained to exactly reproduce the third-person character's animation.

For example:

### Firing

First person may have:

* exaggerated recoil
* weapon kick
* camera shake
* fast recovery
* procedural weapon motion

Third person may show:

* smaller weapon recoil
* upper-body reaction
* muzzle movement

### Reloading

First person may use a detailed reload animation visible close to the camera.

Third person may use a simpler corresponding reload motion.

### Sprinting

First person may use:

* larger weapon sway
* weapon lowering
* camera-relative bob

Third person may use:

* full-body running animation
* arm and leg locomotion

Both systems communicate the same gameplay action without needing to look identical.

---

# 5. Rig the Player Character

`Player Character V1.fbx` is currently unrigged.

Create or import an appropriate skeletal rig for the character.

Preferred target:

```text
Unity Humanoid-compatible rig
```

Where practical, configure the model so Unity can import it using:

```text
Animation Type: Humanoid
```

This will make it easier to use:

* Unity animations
* imported animation packs
* FPS Engine animations where compatible
* Mixamo or similar humanoid animations
* future custom animations

---

# 6. External Rigging Tool

Unity itself is not intended to be the primary mesh-rigging application.

If the model must be manually rigged, it may be necessary to use a DCC tool such as:

```text
Blender
```

or another appropriate rigging workflow.

Cursor should determine what parts of this process can reasonably be configured inside Unity.

If the actual skeleton and skin weights cannot be generated reliably from within the current Unity workflow, prepare the project structure and provide clear instructions for the required external rigging step rather than implementing an unsafe or highly fragile workaround.

---

# 7. Required Skeleton

The rig should include standard humanoid bones wherever the character geometry supports them.

At minimum:

```text
Root / Hips
│
├── Spine
│   ├── Chest
│   │   ├── Neck
│   │   │   └── Head
│   │   │
│   │   ├── Left Shoulder
│   │   │   └── Left Upper Arm
│   │   │       └── Left Forearm
│   │   │           └── Left Hand
│   │   │
│   │   └── Right Shoulder
│   │       └── Right Upper Arm
│   │           └── Right Forearm
│   │               └── Right Hand
│
├── Left Upper Leg
│   └── Left Lower Leg
│       └── Left Foot
│
└── Right Upper Leg
    └── Right Lower Leg
        └── Right Foot
```

Finger bones are optional for the initial implementation but desirable if practical.

---

# 8. Skinning

The player mesh must be skinned to the skeleton.

Movement of the skeleton should deform the character mesh naturally enough for prototype use.

Check obvious deformation areas:

* shoulders
* elbows
* knees
* hips
* neck

There should not be extreme mesh tearing or disconnected body sections during basic test poses.

Perfect production-quality weighting is not required for REQ-034.

---

# 9. T-Pose or A-Pose

The rigged character should have a valid humanoid reference pose.

Preferred:

* T-pose

or:

* A-pose

as required by the rigging workflow.

Unity's Avatar configuration should recognize the humanoid bone mapping correctly.

---

# 10. Unity Import Configuration

Once the rigged model is available, configure the FBX appropriately.

Preferred settings:

```text
Rig
    Animation Type: Humanoid
    Avatar Definition: Create From This Model
```

Ensure Unity reports a valid Avatar.

If necessary, manually configure missing bone mappings.

---

# 11. Avatar Validation

The Unity Humanoid Avatar should successfully map the essential body bones.

At minimum verify:

* Hips
* Spine
* Chest
* Head
* Left Upper Arm
* Left Lower Arm
* Left Hand
* Right Upper Arm
* Right Lower Arm
* Right Hand
* Left Upper Leg
* Left Lower Leg
* Left Foot
* Right Upper Leg
* Right Lower Leg
* Right Foot

The Avatar should not display critical errors.

---

# 12. Third-Person Animator

Add or prepare an `Animator` component for the external player body.

Suggested location:

```text
Player
└── ThirdPersonVisual
    └── PlayerCharacterV1
        └── Animator
```

or an equivalent clean hierarchy.

The Animator should use the humanoid Avatar associated with the rigged character.

---

# 13. Third-Person Animator Controller

Create a basic Animator Controller placeholder.

Suggested name:

```text
AC_PlayerThirdPerson
```

It does not need to contain the complete final animation state machine.

For REQ-034, it may initially include only:

```text
Idle
```

or a very simple test animation.

The purpose is to verify that the rig:

* animates correctly
* remains aligned with the Player root
* works in multiplayer
* is ready for future locomotion states

---

# 14. First-Person Animator

The first-person system should remain separate.

If the current FPS weapon system already uses its own Animator or animation components, preserve that architecture.

Suggested conceptual hierarchy:

```text
Player
│
├── ThirdPersonVisual
│   └── FullBody
│       └── Animator
│
└── FirstPersonVisual
    └── WeaponRoot
        ├── Arms
        ├── Weapon
        └── FPS Animator
```

Do not force the full-body Animator to control first-person weapon animation.

---

# 15. Local Player Visibility

The full third-person body should generally remain hidden from the local first-person camera if it obstructs gameplay.

Other players should still see it.

Preferred:

```text
Local first-person camera:
Does not render ThirdPersonVisual.

Remote cameras:
Render ThirdPersonVisual normally.
```

A dedicated layer may be used.

Example:

```text
ThirdPersonPlayer
```

The local FPS camera may exclude this layer.

---

# 16. First-Person-Only Layer

Likewise, first-person weapon/arms visuals should be visible only to the owning player.

Example layer:

```text
FirstPersonOnly
```

Remote players should not see the owner's FPS weapon model floating in front of the character.

The existing weapon system may already solve this.

Preserve existing behavior where possible.

---

# 17. Animation State Sharing

Although the first-person and third-person Animators may be separate, both should eventually respond to the same gameplay state.

For example:

```text
Gameplay says:
isReloading = true
```

Then:

```text
First-person Animator
→ plays detailed FPS reload

Third-person Animator
→ plays third-person reload
```

Similarly:

```text
Gameplay says:
isSprinting = true
```

Then:

```text
First-person
→ weapon lowers and sways

Third-person
→ full-body sprint animation
```

REQ-034 should establish this architectural principle.

---

# 18. Do Not Network Raw First-Person Animation

The game should not continuously synchronize the local player's exact first-person weapon transforms across the network.

The first-person presentation is primarily cosmetic and local.

Instead, synchronize gameplay states that remote characters need.

Examples:

```text
Speed
IsGrounded
IsCrouching
IsSprinting
IsAiming
IsReloading
IsFiring
CurrentWeapon
IsThrowingGrenade
IsDead
```

Remote clients can use those states to animate the third-person model.

---

# 19. Multiplayer Animation Philosophy

Gameplay state should remain authoritative.

Animation should represent gameplay.

Animation should not control critical gameplay logic.

Avoid architecture such as:

```text
Reload finishes because animation happened.
```

Prefer:

```text
Gameplay system determines reload timing.

Animation reflects that reload state.
```

This reduces multiplayer desynchronization.

---

# 20. Third-Person Network Synchronization

Prepare the external Animator for multiplayer synchronization.

Depending on the project's current Netcode architecture, this may eventually use:

* `NetworkAnimator`
* synchronized NetworkVariables
* custom animation-state RPCs
* another existing synchronization approach

REQ-034 does not need to fully implement every animation state.

However, the rig and Animator structure should be compatible with multiplayer animation synchronization.

---

# 21. Avoid Networking Bone Transforms

Do not network individual bone positions or rotations every frame.

This would create unnecessary bandwidth usage.

Instead:

```text
Network gameplay state
        ↓
Remote Animator
        ↓
Local skeleton animation
```

Each client should animate the remote character locally based on synchronized state.

---

# 22. Root Motion

Do **not** enable animation-driven root motion for normal player movement at this stage.

The existing player controller already handles movement.

Preferred:

```text
Animator.applyRootMotion = false
```

unless the current project architecture specifically requires otherwise.

Movement should continue to be controlled by the existing multiplayer movement system.

Animations should visually follow that motion.

---

# 23. Locomotion Preparation

Prepare Animator parameters that may later drive movement animation.

Suggested parameters:

```text
Speed
ForwardSpeed
StrafeSpeed
IsGrounded
VerticalVelocity
IsCrouching
IsSprinting
```

Not all must immediately be implemented.

The eventual locomotion system should be capable of distinguishing:

* idle
* forward movement
* backward movement
* strafing
* sprinting
* crouched movement
* jumping
* falling

---

# 24. Directional Movement

Because this is a competitive FPS, third-person animations should eventually represent directional movement.

A remote player moving backward should not always visually play a forward-running animation.

Prepare for future locomotion blending such as:

```text
Forward
Backward
Strafe Left
Strafe Right
```

and diagonal combinations.

A 2D Blend Tree may eventually be appropriate.

Implementation of the final Blend Tree may occur in a later requirement.

---

# 25. Aim Direction

Prepare the character rig for upper-body aiming.

Eventually, remote players should visually indicate whether they are aiming:

* upward
* downward
* left/right

without rotating the entire character vertically.

The third-person body should generally use:

```text
Body yaw
+
Upper-body pitch
```

rather than tilting the entire character.

---

# 26. Aim Rigging / IK Preparation

The skeleton should support future:

* Animation Rigging
* IK
* hand constraints
* weapon aiming
* head direction
* spine pitch

Do not implement a complex IK system in REQ-034 unless necessary for validation.

However, avoid a rig structure that would make these systems difficult later.

---

# 27. Weapon Attachment Points

Create or identify useful weapon attachment points on the external character.

At minimum, prepare:

```text
RightHandWeaponSocket
```

Optionally:

```text
LeftHandIKTarget
WeaponHolsterSocket
BackWeaponSocket
```

These may be empty GameObjects parented to appropriate bones.

Example:

```text
RightHand
└── RightHandWeaponSocket
```

---

# 28. External Weapon Model

The external character will eventually need to visibly hold the appropriate weapon.

REQ-034 does not require perfect hand placement.

However, the rig should be prepared for:

* pistol
* AK
* shotgun

to attach to the character's hand.

The first-person weapon object should remain separate from this third-person representation.

---

# 29. Separate Weapon Instances

Do not assume the local first-person weapon mesh should be the same GameObject seen by remote players.

Preferred architecture:

```text
Gameplay weapon state
       │
       ├── FirstPerson Weapon Visual
       │     Owner only
       │
       └── ThirdPerson Weapon Visual
             Other players
```

Both represent the same equipped weapon.

This allows the first-person version to use:

* custom scale
* custom FOV positioning
* detailed reloads
* exaggerated recoil

without affecting the world-space weapon.

---

# 30. Bullseye Integration

The bullseye remains a core gameplay object and must continue to function with the rigged player.

The bullseye should not become permanently dependent on arbitrary animated mesh vertices.

Instead, prepare dedicated body anchors or bone-relative regions where useful.

Potential anchors:

```text
BullseyeHeadAnchor
BullseyeUpperTorsoAnchor
BullseyeLowerTorsoAnchor
BullseyeLeftArmAnchor
BullseyeRightArmAnchor
BullseyeLeftLegAnchor
BullseyeRightLegAnchor
```

These may later help the bullseye move convincingly across an animated body.

Full implementation of humanoid bullseye surface traversal is outside REQ-034.

---

# 31. Bullseye Follows Animation

If the bullseye is attached to a body region, it must visually move with that region as the character animates.

Example:

If the bullseye is on the upper torso and the player runs:

```text
Chest moves
→ Bullseye moves with chest
```

If the bullseye is on the arm:

```text
Arm swings
→ Bullseye follows arm
```

REQ-034 should prepare the rig and anchor hierarchy so this becomes practical.

---

# 32. Bullseye Damage Logic

Rigging must not break existing bullseye damage mechanics.

The rig is initially a presentation system.

Do not rewrite:

* health
* damage
* kill handling
* grenade dislodgement
* bullseye shatter
* respawn

unless necessary for compatibility.

---

# 33. Death Freeze Compatibility

REQ-032 freezes the player's body when killed.

The new Animator must respect that state.

When the player dies and the death freeze begins:

```text
Third-person animation should freeze.
```

Possible implementation approaches include:

```text
Animator.speed = 0
```

or another controlled animation-freeze method.

On respawn:

```text
Animator.speed = 1
```

and animation state should return to normal.

The exact implementation should avoid leaving the Animator permanently paused.

---

# 34. Future Death Animation

A proper death animation may eventually be added.

For now, preserve the REQ-032 freeze-frame effect.

REQ-034 should not replace it.

---

# 35. Crouching Preparation

The rig must support a crouched pose.

Eventually, when:

```text
IsCrouching = true
```

the third-person character should visibly crouch.

The gameplay CharacterController/collider will continue to control actual gameplay height.

The animation should visually correspond to that state.

---

# 36. Jump Preparation

The rig must support future:

* jump launch
* airborne loop
* falling
* landing

animations.

The existing physics controller remains responsible for actual jumping.

---

# 37. Sprint Preparation

The rig must support a full-body sprint animation.

The external sprint should eventually look more energetic than ordinary walking/running.

This is separate from first-person sprint weapon sway.

---

# 38. Grenade Animation Preparation

The rig should support a future grenade-throw animation.

Gameplay currently uses:

```text
Left Trigger — Gamepad
C — Keyboard
```

for grenade throwing.

Eventually:

```text
First person:
FPS arm/throw animation

Third person:
Full-body grenade throw
```

should represent the same grenade action.

No full grenade animation is required in REQ-034.

---

# 39. Animation Events

Avoid relying heavily on Animation Events for authoritative multiplayer gameplay.

They may be used for cosmetic effects such as:

* footsteps
* cloth sounds
* animation-specific effects

Critical events such as:

* firing a bullet
* spawning a grenade
* applying damage
* changing ammo

must remain controlled by gameplay systems.

---

# 40. Existing FPS Engine Animation Assets

The project already contains or has access to FPS Engine animation assets.

Where useful, determine whether any existing animations are compatible with the new Humanoid Avatar.

Do not force incompatible animations onto the rig.

If humanoid retargeting works properly, these assets may become useful in later animation tickets.

REQ-034 only needs to ensure that the new rig supports retargeting where practical.

---

# 41. Animation Retargeting Test

As part of validation, test at least one available humanoid animation on the newly rigged character if a compatible animation already exists.

For example:

```text
Idle
```

or:

```text
Walk
```

The test is intended only to prove:

```text
Humanoid Avatar
+
Animator
+
Skinning
+
Retargeting
```

are functioning.

---

# 42. Suggested Player Hierarchy

A future-friendly hierarchy may look approximately like:

```text
Player
│
├── GameplayRoot
│   ├── CharacterController
│   ├── PlayerMovement
│   ├── PlayerHealth
│   ├── NetworkObject
│   └── Network synchronization
│
├── ThirdPersonVisual
│   └── PlayerCharacterV1_Rigged
│       ├── Animator
│       ├── Armature
│       │   ├── Hips
│       │   ├── Spine
│       │   ├── Chest
│       │   ├── Head
│       │   ├── Arms
│       │   └── Legs
│       │
│       ├── Mesh
│       │
│       └── Sockets
│
├── FirstPersonVisual
│   ├── FPSArms
│   ├── WeaponRoot
│   └── FirstPersonAnimator
│
├── BullseyeSystem
│
└── CameraRoot
    └── PlayerCamera
```

This hierarchy is illustrative.

Do not unnecessarily rebuild working prefab structures just to exactly match it.

---

# 43. Animation Controller Separation

Use separate Animator Controllers where appropriate.

Suggested:

```text
AC_PlayerThirdPerson
```

for the full-body character.

And possibly:

```text
AC_PlayerFirstPerson
```

for FPS arms/weapons if not already handled separately.

These controllers should not be tightly coupled to one another.

---

# 44. Performance

Only the full-body third-person character needs to be animated for remote players.

Avoid unnecessary duplication.

For example:

A remote player does not need another player's hidden first-person arms to animate.

Likewise, the local player's hidden full-body visual should not perform unnecessarily expensive visual work if optimization becomes relevant.

Optimization is secondary during the prototype, but the architecture should allow it later.

---

# 45. Animator Culling

Configure Animator culling conservatively.

Characters should not stop animating in ways that break gameplay presentation when temporarily off-camera.

Use an appropriate culling mode based on Unity behavior and networking.

Do not optimize aggressively until correctness is verified.

---

# 46. Rig Replacement Safety

If creating a rig requires generating a modified FBX such as:

```text
Player Character V1_Rigged.fbx
```

preserve the original:

```text
Player Character V1.fbx
```

Do not overwrite the only source asset.

This allows the developers to return to the original mesh if rigging problems occur.

---

# 47. File Organization

Suggested organization:

```text
Assets
└── Characters
    └── Player
        ├── Models
        │   ├── Player Character V1.fbx
        │   └── Player Character V1_Rigged.fbx
        │
        ├── Animations
        │
        ├── Controllers
        │   └── AC_PlayerThirdPerson.controller
        │
        ├── Materials
        │
        └── Prefabs
            └── PlayerCharacterV1_Rigged.prefab
```

Use existing project conventions where appropriate.

---

# 48. Multiplayer Testing

Test with at least:

```text
Host + 1 Client
```

Verify both perspectives.

### Player 1 Internal View

Player 1 should see:

* normal FPS camera
* normal weapon visuals
* no obstructive full-body mesh
* existing weapon animation behavior

### Player 1 External Representation

Player 2 should see:

* Player 1's full rigged character
* body following movement
* body following rotation
* any test animation functioning

Repeat in the opposite direction for Player 2.

---

# 49. Do Not Break Existing Systems

The following must continue to function:

* player spawning
* movement
* sprinting
* crouching
* jumping
* aiming
* shooting
* reloading
* weapon switching
* grenade throwing
* bullseye movement
* grenade bullseye dislodgement
* health
* damage
* death
* bullseye shatter
* death freeze
* disappearance
* respawn
* kill tracking

Rigging and Animator additions should not alter authoritative gameplay behavior.

---

# 50. Acceptance Criteria

REQ-034 is complete when:

* [ ] A rigged version of `Player Character V1` exists or the exact external rigging step required to create it is documented.
* [ ] The original unrigged FBX is preserved.
* [ ] The character has a humanoid-compatible skeletal structure where feasible.
* [ ] The mesh is skinned to the skeleton.
* [ ] Major joints deform acceptably for prototype use.
* [ ] Unity recognizes the character as a valid Humanoid Avatar.
* [ ] Required humanoid bones are mapped.
* [ ] The external body has an Animator.
* [ ] A third-person Animator Controller exists.
* [ ] At least one simple animation can successfully move the rig for validation.
* [ ] Root motion does not replace existing player movement.
* [ ] The rigged body remains aligned with the Player root.
* [ ] Multiplayer spawning still works.
* [ ] Remote players see the rigged full-body character.
* [ ] The local player's first-person view remains unobstructed.
* [ ] First-person weapon presentation remains separate from the full-body Animator.
* [ ] First-person animation can remain more responsive/exaggerated than third-person animation.
* [ ] Third-person animation architecture is prepared to respond to synchronized gameplay state.
* [ ] Individual bones are not network-synchronized every frame.
* [ ] Right-hand weapon socket exists or is prepared.
* [ ] Rig supports future upper-body aiming and IK.
* [ ] Rig supports future crouching.
* [ ] Rig supports future jumping.
* [ ] Rig supports future sprinting.
* [ ] Rig supports future grenade throwing.
* [ ] Bullseye architecture remains functional.
* [ ] Bullseye attachment points can be associated with animated body regions in future.
* [ ] REQ-032 death freeze still works with the Animator.
* [ ] Animator state is restored correctly after respawn.
* [ ] No new NetworkObject ownership errors occur.
* [ ] No major new NullReferenceExceptions occur.

---

# Out of Scope

The following are not required for REQ-034:

* complete locomotion animation system
* final idle animation
* final walk animation
* final sprint animation
* final crouch animation
* final jump animation
* final grenade animation
* complete reload animations
* complete weapon IK
* foot IK
* advanced procedural animation
* realistic facial animation
* facial rigging
* lip synchronization
* ragdoll physics
* final death animations
* replacing current gameplay colliders
* redesigning damage calculations
* final humanoid bullseye traversal
* networking individual bone transforms

These should be handled in subsequent requirements.

---

# Intended Future Architecture

REQ-034 should leave the game prepared for a future animation system that operates approximately like this:

```text
                    GAMEPLAY STATE
                         │
            ┌────────────┴────────────┐
            │                         │
            ▼                         ▼

   FIRST-PERSON VIEW          THIRD-PERSON VIEW

  Owner sees this             Other players see this

  FPS arms / weapon           Full-body humanoid
         │                            │
         ▼                            ▼
  Detailed animations        World-space animations

  Recoil                     Locomotion
  Reload                     Aim direction
  Weapon sway                Weapon handling
  Sprint motion              Crouch
  Grenade throw              Jump
  ADS motion                 Grenade throw
```

The two systems should remain visually coordinated through shared gameplay states while being free to use different animations appropriate for their respective perspectives.

The goal is to make the game eventually feel excellent **both to play from inside the character and to watch from outside the character**, without sacrificing one perspective for the other.
