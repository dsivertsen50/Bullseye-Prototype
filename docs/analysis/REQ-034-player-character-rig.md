# REQ-034 Player Character Rig Notes

The original unrigged source mesh is preserved at:

```text
Assets/Player/Player Character V1.fbx
```

Do not overwrite that file. The unrigged visual wrapper remains at `Assets/Player/PlayerCharacterV1.prefab` as a fallback.

REQ-034 generated a **prototype Humanoid rig** inside Unity:

```text
Assets/Player/PlayerCharacterV1_Rigged.prefab
Assets/Player/PlayerCharacterV1_WeightedMesh.asset
Assets/Player/PlayerCharacterV1_RiggedAvatar.asset
Assets/Player/AC_PlayerThirdPerson.controller
Assets/Player/Idle_PlayerThirdPerson.anim
```

The live player visual is the nested instance under `Player/VisualRoot`, pointing at `PlayerCharacterV1_Rigged.prefab`.

Skin weights are automatic distance weights suitable for prototype locomotion tests. They are not production Mixamo/Blender weights.

To rebuild the prototype rig from the original FBX in the **main Unity Editor** (not a Multiplayer Play Mode virtual player):

```text
Unity menu: Bullseye / Rebuild Player Character Rig
```

## Architecture

Third-person full-body animation is driven by gameplay state, not by networked bones:

* `PlayerAnimationState` (owner-written NetworkVariables for speed, grounded, sprint, aim, grenade throw; crouch/dead/reload/aim pitch are read from existing gameplay components)
* `PlayerThirdPersonAnimator` drives `AC_PlayerThirdPerson` on the rigged visual
* First-person weapons stay on `WeaponPresentationController` / `FirstPersonWeaponView` (`FirstPersonWeapon` layer)
* Local full-body visual is still hidden via `PlayerNetworkSetup` (`LocalPlayerBody` layer)

Death freeze sets `Animator.speed = 0`. Respawn restores speed and plays Idle.

## Replacing this rig with Mixamo or Blender

Do **not** overwrite `Player Character V1.fbx`. Import the new rigged file alongside it, for example:

```text
Assets/Player/Player Character V1_Rigged.fbx
```

### Mixamo

1. Export or use `Player Character V1.fbx`.
2. Upload to Mixamo Auto-Rigger.
3. Use a T-pose, standard 65-bone Mixamo skeleton.
4. Download FBX for Unity with skin.
5. Import into Unity.
6. Inspector → Rig:
   - Animation Type: **Humanoid**
   - Avatar Definition: **Create From This Model**
7. Confirm the Avatar has no critical missing bones.
8. Replace the visual under `Player/VisualRoot` with the new model.
9. Keep the existing `Animator` settings:
   - Controller: `AC_PlayerThirdPerson`
   - Apply Root Motion: off
   - Culling Mode: Always Animate
10. Re-parent sockets if bone names changed:
    - `RightHandWeaponSocket`
    - `LeftHandIKTarget`
    - `WeaponHolsterSocket`
    - `BackWeaponSocket`
    - `Bullseye*Anchor` objects

### Blender

1. Import `Player Character V1.fbx` into Blender.
2. Add a Human metarig (Rigify) or a Mixamo-compatible armature.
3. Align bones to the T-pose stickman.
4. Parent the mesh with automatic weights, then paint shoulders, elbows, hips, knees, and neck.
5. Export FBX:
   - Forward: -Z
   - Up: Y
   - Apply Transform if the Unity import scale is wrong
6. Follow the same Unity Humanoid import steps as Mixamo.

First-person weapon animation is independent of this rig. Do not retarget FPS weapon clips onto the full-body Avatar unless a later requirement says to.
