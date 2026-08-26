# REQ-033 — Replace Player Capsule Visual with `Player Character V1.fbx`

## Summary

Replace the current visible capsule representation of the multiplayer player character with the newly imported character model:

```text
Player Character V1.fbx
```

The new model may ultimately become the final player body used in the game.

At this stage, the model is **not rigged and does not contain character animations**.

Therefore, this requirement should replace the **visual appearance** of the player while preserving the existing gameplay architecture, including:

* movement
* CharacterController/collision
* networking
* health
* bullseye
* weapons
* death/respawn
* grenade interaction
* hit detection
* camera behavior

The current capsule may remain internally as an invisible collision/controller object if that is the safest approach.

---

# 1. Goal

Players should no longer appear as simple capsules to other players.

Instead, the existing networked player prefab should visually display:

```text
Player Character V1.fbx
```

The character model should:

* follow the player's movement
* rotate with the player's orientation
* move correctly across the network
* respawn with the player
* carry the existing bullseye
* work with the existing health and death systems

No animation system is required yet.

---

# 2. Preserve Existing Player Prefab Architecture

Do **not** rebuild the player controller from scratch.

The existing player prefab already contains important systems such as:

* NetworkObject
* NetworkTransform or equivalent synchronization
* movement controller
* CharacterController and/or collider
* camera
* input handling
* weapon systems
* health
* bullseye
* grenade interactions
* kill/death logic
* respawn logic

These systems should remain intact.

REQ-033 is primarily a **visual-model substitution**.

---

# 3. Keep the Capsule for Collision if Necessary

The current capsule likely provides reliable collision and movement behavior.

It may remain in the player prefab as an invisible gameplay object.

Recommended structure:

```text
Player
├── NetworkObject
├── CharacterController
├── PlayerMovement
├── PlayerHealth
├── Weapon Systems
├── Camera
├── Collision Capsule
│
├── Visuals
│   └── Player Character V1
│
└── Bullseye
```

The capsule renderer should be disabled or removed so it is no longer visible.

The actual capsule collider or CharacterController should remain functional.

Do not replace working gameplay collision with the FBX mesh collider.

---

# 4. Player Character V1 Model

Use the imported asset:

```text
Player Character V1.fbx
```

Create a reusable prefab or prefab child from this model if needed.

Suggested name:

```text
PlayerCharacterV1
```

or:

```text
PlayerCharacterVisual
```

The model should be instantiated or placed under the existing Player prefab.

---

# 5. Model Alignment

The FBX should be positioned so that it aligns naturally with the existing player controller.

The following should be corrected as necessary:

* scale
* local position
* local rotation

The character should stand upright.

Its feet should approximately align with the bottom of the CharacterController.

The character's body center should approximately align with the existing player's center.

The character should face the same forward direction as the player controller.

For example:

```text
Player Forward
     ↑

   Head
    O
   /|\
   / \
```

The visual should not appear:

* rotated sideways
* facing backward
* floating above the ground
* embedded in the floor

---

# 6. Model Scale

Determine an appropriate scale relative to the existing capsule.

The new character should be approximately equivalent in overall gameplay height to the existing player.

Do not change the player's movement physics simply to accommodate an incorrectly scaled FBX.

Prefer adjusting the visual model's local scale.

Suggested hierarchy:

```text
Player
└── VisualRoot
    └── Player Character V1
```

This allows scale and alignment adjustments without modifying the root player transform.

---

# 7. Visual Root

If one does not already exist, create a dedicated child transform for third-person player visuals.

Suggested name:

```text
VisualRoot
```

Example:

```text
Player
├── VisualRoot
│   └── Player Character V1
│
├── Bullseye
├── Camera
├── WeaponRoot
└── Gameplay Components
```

Future character animation systems should be attached to or organized beneath this visual hierarchy rather than altering the core Player object.

---

# 8. Remove Visible Capsule

The old capsule should no longer be visible during normal gameplay.

If the existing capsule uses a MeshRenderer:

```text
Disable or remove the MeshRenderer.
```

Do not necessarily remove:

* CapsuleCollider
* CharacterController
* Rigidbody
* scripts
* network components

unless Cursor determines they are purely visual and no longer needed.

---

# 9. Local Player First-Person View

The local player's own third-person character body should not interfere with the first-person camera.

Verify that the new model does not:

* block the camera
* appear inside the camera
* obstruct aiming
* clip excessively into the player's weapon
* cover the screen when crouching or jumping

If necessary, the local player's body may be hidden from their own first-person camera while remaining visible to other players.

Preferred behavior:

```text
Local player:
First-person weapon/camera experience remains unchanged.

Remote players:
Player Character V1 is visible.
```

Do not remove the model entirely from the local player object if doing so would interfere with networking or future animation architecture.

Layer-based camera culling may be used if appropriate.

---

# 10. Multiplayer Visibility

All remote players should see the new model.

Example:

```text
Player 1 sees:
Player 2 = Player Character V1

Player 2 sees:
Player 1 = Player Character V1
```

The model should move and rotate according to the existing network synchronization.

Do not add separate network synchronization directly to individual body mesh components unless required.

The character model should generally inherit movement from the existing networked Player root.

---

# 11. Ownership

The visual model must not interfere with Netcode ownership.

Do not:

* add an unnecessary NetworkObject to the FBX
* independently spawn the visual model
* change player ownership
* create a second player network object

The character visual should preferably be a normal child of the existing networked player prefab.

---

# 12. Bullseye Integration

The existing bullseye should remain attached to and functional with the new player character.

REQ-033 should preserve all existing bullseye systems, including:

* randomized movement
* damage location
* turning influence
* jumping/crouching influence
* grenade dislodgement
* bullseye HUD location tracking
* shatter-on-death behavior

The bullseye's anchor or movement surface may need repositioning because the new body is no longer shaped like a capsule.

---

# 13. Temporary Bullseye Placement

Because the body model is now more humanoid, update the bullseye's visual positioning as needed so it appears attached to the body rather than floating significantly away from it.

Exact anatomically correct surface tracking is **not required** in this ticket.

The primary objective is to ensure that the existing bullseye system remains usable.

A later requirement may refine bullseye placement across the humanoid body's:

* head
* torso
* arms
* legs

---

# 14. Hit Detection

Do not automatically replace the existing player damage collider system with mesh colliders.

The current gameplay collision architecture should remain intact unless a very small adjustment is required.

For REQ-033:

```text
Visual Mesh ≠ Gameplay Collider
```

The FBX is primarily cosmetic.

Future tickets may introduce dedicated humanoid hitboxes for:

* head
* torso
* lower body

This is outside the current scope.

---

# 15. Current Damage Regions

Existing damage logic should continue functioning.

If the current system determines:

* head
* torso
* lower body

using the capsule's coordinates or bullseye location, preserve that behavior for now.

Do not redesign damage region detection as part of REQ-033.

---

# 16. Character Rotation

The visual body should rotate with the player's world-facing direction.

If the player turns left or right:

```text
Player Character V1
```

should visibly turn with them.

The model should not remain facing one direction while the player controller rotates.

---

# 17. Vertical Camera Rotation

Do not make the entire body tilt forward/backward when the player looks up or down unless the existing controller already behaves that way.

Preferred behavior:

```text
Yaw / horizontal rotation:
Rotate character body.

Pitch / vertical camera movement:
Camera only.
```

Future animation/aim-rig tickets may add upper-body aiming.

---

# 18. Jumping

When the player jumps, the new body should move upward with the existing player root.

No jump animation is required.

The body may remain in its static pose during the jump for now.

---

# 19. Crouching

The existing crouching mechanic must remain functional.

Because the character is not rigged, the body cannot yet properly animate into a crouching pose.

For this ticket, choose the simplest stable temporary behavior.

Acceptable prototype behavior:

```text
Player collider crouches as before.

Player visual moves vertically with the player but remains in its static pose.
```

Do not distort or procedurally deform the model.

The incorrect visual pose is acceptable temporarily until animations are introduced.

---

# 20. Sprinting

Sprint movement should remain unchanged.

The player body should simply move faster according to the existing sprint logic.

No sprint animation is required in REQ-033.

---

# 21. Weapons

Existing first-person weapon behavior should remain unchanged.

The new body should not interfere with:

* pistol
* AK
* shotgun
* reload
* weapon switching
* shooting
* reticle behavior

Third-person weapon attachment to the new player's hands is **not required** yet unless it already exists.

A later animation/character ticket may establish weapon sockets and hand positioning.

---

# 22. Death and Respawn

REQ-033 must work with the existing death sequence.

When the player dies:

* the new body should freeze according to REQ-032
* bullseye shatter should still occur
* the player's visuals should disappear when expected
* the player should respawn normally
* the model should become visible again after respawn

The old capsule should not suddenly become visible during death or respawn.

---

# 23. Respawn Position

The new model must correctly follow the existing player's respawn location.

No independent visual transform should remain at the player's death position.

At respawn:

```text
VisualRoot.localPosition
```

and:

```text
VisualRoot.localRotation
```

should return to their intended values if they were changed during death presentation.

---

# 24. Grenades

Existing grenade behavior should remain functional.

Grenade force should continue affecting the player's gameplay controller according to the existing implementation.

The new FBX should simply follow the resulting player movement.

Do not add Rigidbody physics to the static visual body unless required for a later ragdoll system.

---

# 25. Rigidbody on Character Mesh

Do not add a standalone non-kinematic Rigidbody to the player character visual.

Doing so could cause:

* visual separation from the player
* physics jitter
* network inconsistencies
* unexpected collisions

The character visual should inherit its transform from the Player root.

---

# 26. Rig Status

`Player Character V1.fbx` is currently unrigged.

REQ-033 should **not** attempt to automatically generate a complete animation rig unless a simple import configuration is required.

Animation work will be handled in a subsequent requirement.

However, structure the Player prefab so that adding an Animator later is straightforward.

Suggested future hierarchy:

```text
Player
└── VisualRoot
    └── PlayerCharacter
        ├── Armature
        └── Mesh
```

The exact hierarchy may change once the character is rigged.

---

# 27. Animation Readiness

REQ-033 should prepare for future animation work.

Avoid tightly coupling gameplay scripts directly to mesh child transforms.

Where practical, scripts should reference:

```text
VisualRoot
```

instead of specific mesh bones that do not yet exist.

This will make it easier to replace the unrigged FBX with a rigged version later.

---

# 28. Future Model Replacement

The system should make it easy to replace:

```text
Player Character V1.fbx
```

with a future model revision.

For example:

```text
Player Character V2.fbx
```

should ideally require replacing only the visual prefab under:

```text
VisualRoot
```

rather than rebuilding the entire network player prefab.

---

# 29. Material and Texture Support

Ensure that materials included with or assigned to `Player Character V1.fbx` render correctly in the project's current render pipeline.

If the FBX imports with missing or incorrect materials:

* preserve the model
* create or assign Unity materials as necessary
* keep materials organized with the character asset

Do not redesign the character's art as part of this ticket.

---

# 30. Prefab Organization

Suggested asset organization:

```text
Assets
└── Characters
    └── Player
        ├── Models
        │   └── Player Character V1.fbx
        │
        ├── Materials
        │
        └── Prefabs
            └── PlayerCharacterV1.prefab
```

If the current project already uses a different asset structure, follow the existing convention rather than reorganizing unnecessarily.

---

# 31. Player Prefab Safety

Because the Player prefab is used by Netcode for GameObjects, changes must be made carefully.

Before completing the requirement, verify that:

* Player prefab remains registered correctly
* NetworkManager still references the correct Player prefab
* NetworkObject remains on the correct root object
* multiplayer spawning still works
* host and client both spawn successfully

Do not create a second competing network player prefab unless explicitly necessary.

---

# 32. Suggested Hierarchy

A possible resulting prefab structure:

```text
Player
├── NetworkObject
├── CharacterController
├── PlayerMovement
├── PlayerHealth
│
├── Collision
│   └── Capsule
│       └── Renderer Disabled
│
├── VisualRoot
│   └── PlayerCharacterV1
│
├── BullseyeSystem
│   └── Bullseye
│
├── CameraRoot
│   └── PlayerCamera
│
└── WeaponSystem
```

This is illustrative.

Cursor should adapt the hierarchy to the existing project rather than unnecessarily restructuring working components.

---

# 33. Preserve Existing Systems

The following features must still work after replacing the visible capsule:

* [ ] Multiplayer spawning
* [ ] Movement
* [ ] Mouse aiming
* [ ] Controller aiming
* [ ] Jump
* [ ] Crouch
* [ ] Sprint
* [ ] Zoom/aiming
* [ ] Shooting
* [ ] Weapon switching
* [ ] Reloading
* [ ] Grenades
* [ ] Health
* [ ] Damage
* [ ] Bullseye movement
* [ ] Bullseye dislodgement
* [ ] Bullseye shatter
* [ ] Kill tracking
* [ ] Death
* [ ] Respawn countdown
* [ ] Respawning
* [ ] Pause menu

Do not intentionally redesign these systems.

---

# 34. Testing

Test at minimum with:

```text
Host + 1 Client
```

Verify:

### Host

Host spawns using the new model.

### Client

Client spawns using the new model.

### Remote Visibility

Each player sees the other player's new character model.

### Movement

The model follows networked movement correctly.

### Rotation

The model turns appropriately.

### Jump

The model follows the player upward and downward.

### Crouch

Gameplay crouching remains functional even if the static mesh does not yet animate.

### Death

The model freezes/disappears according to the existing death sequence.

### Respawn

The model reappears at the correct spawn location.

---

# Acceptance Criteria

REQ-033 is complete when:

* [ ] `Player Character V1.fbx` is used as the visible player body.
* [ ] The old capsule mesh is no longer visible during normal gameplay.
* [ ] Existing CharacterController/capsule collision remains functional.
* [ ] The new body is correctly scaled relative to the player.
* [ ] The body is positioned with its feet approximately at ground level.
* [ ] The body faces the correct forward direction.
* [ ] The body follows player movement.
* [ ] The body follows player horizontal rotation.
* [ ] Host players display the new model.
* [ ] Client players display the new model.
* [ ] Remote players see one another using the new model.
* [ ] The first-person camera is not obstructed by the character mesh.
* [ ] Existing first-person weapons continue to work.
* [ ] Jump still works.
* [ ] Crouch still works.
* [ ] Sprint still works.
* [ ] Shooting still works.
* [ ] Weapon switching still works.
* [ ] Grenades still work.
* [ ] Health and damage still work.
* [ ] The bullseye remains functional.
* [ ] Bullseye shatter still works.
* [ ] Death presentation still works.
* [ ] Respawn still works.
* [ ] The new player model returns correctly after respawning.
* [ ] The NetworkManager player prefab configuration remains valid.
* [ ] No new NullReferenceExceptions occur.
* [ ] No new Netcode spawning or ownership errors occur.
* [ ] The visual hierarchy is structured so a rigged/animated character can replace the current FBX later.

---

# Out of Scope

The following are **not required** for REQ-033:

* Rigging the character
* Creating an armature
* Walking animation
* Running animation
* Sprint animation
* Jump animation
* Crouch animation
* Death animation
* Reload animation
* Third-person weapon animations
* IK
* hand placement
* head tracking
* upper-body aiming
* humanoid retargeting
* ragdoll physics
* replacing capsule collision with humanoid hitboxes
* detailed body-region damage colliders
* redesigning the bullseye movement algorithm

These should be addressed in subsequent requirements.

---

# Future Follow-Up

The next logical character-system ticket should focus on preparing `Player Character V1` for animation.

Likely work would include:

1. Rigging or importing a rigged version of the model.
2. Configuring Unity's Humanoid Avatar system if compatible.
3. Adding an Animator.
4. Creating locomotion states for idle, walk, sprint, jump, and crouch.
5. Synchronizing animations across multiplayer clients.
6. Adding upper-body weapon handling.
7. Eventually adding body-region hitboxes that follow the animated skeleton.

REQ-033 should therefore prioritize a clean separation between the **gameplay player controller** and the **replaceable visual character model**.
