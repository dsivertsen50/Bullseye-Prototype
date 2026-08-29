# REQ-037 — Third-Person Weapon Alignment, Weapon-Holding Poses, and Aim Adjustment

## Summary

Improve the third-person representation of equipped weapons so that other players see each character holding their current weapon in a believable position.

The current Mixamo locomotion animations were not authored for weapon use, so the player's hands and arms do not naturally line up with the equipped pistol, AK, or shotgun.

REQ-037 should add a weapon-holding system that layers over the existing locomotion animations and positions both the weapon and the player's hands appropriately.

The system should support:

* Pistol
* AK / rifle
* Shotgun
* Idle
* Walking
* Crouching
* Prone where practical
* Sprinting
* Aiming

The intended approach should favor:

* Weapon attachment points
* Animator layers
* Avatar Masks
* Animation Rigging / IK

rather than creating entirely separate locomotion animation sets for every weapon.

---

# 1. Primary Goal

When another player looks at a character, the currently equipped weapon should:

* Appear physically attached to the character.
* Remain aligned with the character during animation.
* Be primarily controlled by the right hand.
* Be supported by the left hand.
* Follow appropriate weapon-specific poses.
* Move into a more deliberate aiming posture when ADS/aim is active.
* Continue working during movement animations.

The weapon should no longer appear:

* Floating
* Offset from the hands
* Clipping significantly through the torso
* Attached incorrectly to the hips/body
* Moving independently of the hands

---

# 2. Scope

REQ-037 applies primarily to the **third-person/external player representation** seen by other players.

The existing first-person weapon setup should remain separate.

Do not attempt to replace or substantially alter the first-person gun positioning or first-person weapon animations as part of this ticket.

The local player may have their third-person model hidden or partially hidden depending on the existing architecture.

The important acceptance case is:

> Player 2 sees Player 1 holding their equipped weapon correctly.

---

# 3. Weapon Attachment Architecture

Create a consistent third-person weapon attachment architecture.

The player's rig should contain a logical weapon attachment point associated with the right hand.

Suggested hierarchy or equivalent:

`RightHand`
→ `WeaponSocket`

The currently equipped third-person weapon model should be attached to this socket.

The weapon socket should allow configurable:

* Local position
* Local rotation
* Local scale if absolutely necessary

for each weapon.

Avoid applying arbitrary hard-coded world-space offsets.

All positioning should remain relative to the player's animated skeleton.

---

# 4. Per-Weapon Alignment Configuration

Each weapon may require different positioning.

Provide easily adjustable inspector configuration for:

### Pistol

* Position offset
* Rotation offset
* Left-hand support position
* Aim offset

### AK

* Position offset
* Rotation offset
* Left-hand support position
* Aim offset

### Shotgun

* Position offset
* Rotation offset
* Left-hand support position
* Aim offset

These values should not be buried in code.

The developer should be able to visually tune them in Unity.

A reusable structure such as:

`ThirdPersonWeaponPose`

or equivalent may be created.

---

# 5. Pistol Holding Pose

The pistol should be primarily controlled by the player's right hand.

The right hand should grip the pistol's primary grip.

The left hand should support the right hand/pistol in a believable two-handed stance.

Target visual:

* Weapon centered generally in front of the upper torso.
* Pistol held somewhat high.
* Weapon closer to neck/head height than the rifle and shotgun.
* Elbows somewhat bent.
* Both hands appear to support the pistol.
* Pistol is not rigidly pressed against the character's face.

The pose does not need to match professional firearm technique perfectly, but it should look intentionally weapon-ready.

---

# 6. Rifle / AK Holding Pose

The AK should be held primarily with the right hand on the rear/pistol grip.

The left hand should support the weapon farther forward.

Target visual:

* Right hand controls the main grip.
* Left hand supports the fore-end/front portion.
* Rifle sits lower than the pistol pose.
* Stock or rear of weapon should visually sit near the right shoulder where practical.
* Barrel points generally toward the player's facing/aim direction.

The weapon should look naturally supported rather than suspended from a single hand.

---

# 7. Shotgun Holding Pose

The shotgun should use a similar general pose to the AK.

Target visual:

* Right hand on rear/main grip.
* Left hand on pump/fore-end area.
* Weapon held across the upper torso.
* Stock sits toward the shoulder where appropriate.
* Weapon sits slightly below the pistol position.

Shotgun configuration should remain independent from AK configuration.

Do not assume both weapons have identical grip locations.

---

# 8. Left-Hand IK Target

Each third-person weapon prefab should expose a transform that identifies where the left hand should support the weapon.

Suggested transform:

`LeftHandIKTarget`

This should be placed manually on the weapon prefab.

Examples:

### Pistol

Target should sit around the support-hand position near the pistol grip/right hand.

### AK

Target should sit along the forward handguard.

### Shotgun

Target should sit along the pump/fore-end.

The player's left hand should use IK to follow this transform while the weapon is equipped.

---

# 9. Right-Hand Authority

The right hand should be treated as the primary weapon hand.

Preferred architecture:

* Weapon follows right hand / weapon socket.
* Left hand follows the weapon using IK.

Avoid having both hands independently attempt to position the weapon, as this can create unstable feedback loops.

Conceptually:

`Right Hand`
→ controls weapon

`Weapon`
→ defines where left hand should go

`Left Hand`
→ IK follows weapon target

---

# 10. Animation Rigging / IK

Use Unity's Animation Rigging system or an equivalent robust IK solution.

Potential rig components may include:

* Two Bone IK Constraint
* Multi-Aim Constraint
* Rig Builder
* Weighted constraints
* Upper-body aim rig

Exact implementation is flexible.

The important requirement is that weapon positioning layers over the existing Mixamo animations.

---

# 11. Preserve Locomotion Animation

REQ-036 locomotion animations should remain the base animation layer.

Examples:

* Standing Idle
* Walking
* Sprinting
* Crouching
* Crouch walking
* Prone
* Jump transitions

The weapon system should modify mainly:

* Spine
* Chest
* Shoulders
* Upper arms
* Forearms
* Hands

without replacing the entire locomotion clip.

This allows the legs and lower body to continue using Mixamo locomotion.

---

# 12. Upper-Body Animation Layer

Create an upper-body weapon layer where useful.

An Avatar Mask may be used to isolate:

* Spine
* Chest
* Shoulders
* Arms
* Hands

The lower body should continue to be controlled by the locomotion layer.

Weapon pose layers may include:

* Unarmed
* Pistol
* Rifle
* Shotgun

and potentially:

* Hip Fire
* Aim

Avoid duplicating full-body locomotion states for each weapon.

---

# 13. Weapon Pose Weighting

Weapon pose/IK should use adjustable weights.

Example:

`WeaponPoseWeight`

Range:

`0 → 1`

This allows weapon posing to blend rather than snap.

Potential states:

### Normal weapon-ready state

Weight approximately:

`1.0`

### Sprint

Weight may reduce substantially.

### Transitioning weapons

Weight may temporarily blend.

### Death

Weight may drop to zero if appropriate.

Do not hard-snap constraints unless required.

---

# 14. Normal / Hip-Fire Pose

When the player is holding a weapon but is **not aiming**, use the normal weapon-ready pose.

For pistol:

* Weapon remains relatively high.
* Both hands remain on the weapon.
* Weapon may sit slightly below true eye level.

For AK / shotgun:

* Weapon remains at a lower ready position.
* Right shoulder should support the weapon where practical.
* Barrel should still point generally forward.

This should be the pose used during normal:

* Idle
* Walking
* Crouching
* Most regular movement

---

# 15. Aiming / ADS Third-Person Pose

When the player activates the existing aim/ADS input:

Remote players should see the character move into a more deliberate aiming posture.

The actual first-person ADS mechanics remain unchanged.

Third-person aiming should visually:

* Raise the weapon slightly.
* Move the weapon somewhat closer to the player's eyes/head.
* Rotate the upper torso/arms as necessary.
* Keep the weapon pointed toward the player's aim direction.

The shift should be subtle but clearly visible.

Do not push the weapon directly through the player's face.

---

# 16. Pistol Aim Pose

When aiming with the pistol:

* Both hands remain engaged.
* Arms may extend slightly more forward.
* Pistol should move closer to eye level.
* Upper torso may lean/rotate subtly if required.

The third-person pose should clearly communicate:

> This player is aiming.

---

# 17. Rifle / Shotgun Aim Pose

When aiming with AK or shotgun:

* Weapon should raise toward the head/eyes.
* Stock should remain near the shoulder.
* Head/weapon relationship should appear more deliberate.
* Left-hand support should remain stable through IK.

Do not attempt to reproduce the exact first-person sight picture externally.

The goal is visual readability and believability.

---

# 18. Aim Direction

The third-person weapon should generally align with the player's actual aim direction.

This is especially important if the player can:

* Look upward
* Look downward
* Aim independently of pure body yaw

Where supported by the existing controller, use:

* Camera pitch
* Player aim pitch
* Existing replicated aim data

to influence the third-person upper body.

Potential implementation:

* Spine/chest aim constraint
* Weapon aim target
* Upper-body pitch adjustment

Do not rotate the entire player capsule/body vertically.

---

# 19. Vertical Aim

Remote players should be able to visually distinguish:

* Looking upward
* Looking horizontally
* Looking downward

At minimum, the weapon and upper torso should approximately follow vertical aim pitch.

Limit the amount of spine/arm rotation so the model does not deform unnaturally.

Clamp extreme angles as necessary.

---

# 20. Horizontal Aim

Normal character rotation should continue controlling most horizontal/yaw aiming.

If the existing game supports looking somewhat independently from body rotation, a limited upper-body yaw offset may be applied.

Avoid twisting the upper torso unrealistically beyond reasonable limits.

---

# 21. Network Synchronization

Third-person weapon pose and aim state must display correctly to other multiplayer clients.

Remote clients should know:

* Which weapon is equipped.
* Whether the player is aiming.
* Relevant aim pitch/yaw if required.
* Relevant locomotion state.

Do not continuously network individual bone positions.

Do not synchronize IK transforms every frame if the pose can be reconstructed locally using:

* Equipped weapon
* Existing player transform
* Aim state
* Aim direction

Prefer deterministic local visual reconstruction from already-networked gameplay state.

---

# 22. Weapon Switching

When the player switches secondary weapons:

The third-person model should update immediately to the newly equipped weapon.

Example:

AK
→ Shotgun

The system should:

1. Disable/remove the previous third-person weapon.
2. Enable/attach the new weapon.
3. Apply the correct right-hand weapon offset.
4. Change the left-hand IK target.
5. Apply the correct weapon pose.
6. Preserve locomotion.

The left hand should not remain attached to the previous weapon location.

---

# 23. Permanent Pistol Compatibility

The existing weapon system includes the pistol as the player's permanent weapon.

REQ-037 must support transitioning between:

* Pistol
* AK
* Shotgun

without requiring separate player prefabs.

---

# 24. Sprinting

When sprinting, the normal weapon pose may be modified.

The player should not necessarily maintain a perfect two-handed ADS-ready posture while sprinting.

For this ticket:

* Preserve the existing sprint animation.
* Allow weapon rig/IK weight to reduce if necessary.
* Keep the weapon attached to the character.
* Avoid severe arm distortion.

A more stylized weapon-specific sprint pose can be implemented later.

The system should expose a clean path for adding those poses.

---

# 25. Crouching

Weapon poses should remain active while crouching.

Crouching locomotion should continue using the existing REQ-036 animations.

The upper-body weapon pose should layer over these animations.

Ensure:

* Weapon does not clip heavily into thighs/knees.
* Hands stay attached to the weapon.
* Aim mode still functions from crouch.

---

# 26. Prone

Prone weapon posing is potentially more complex.

REQ-037 should make a best effort to keep the weapon aligned while prone.

At minimum:

* Weapon remains attached to the right hand.
* Weapon does not remain floating at standing chest height.
* Left hand attempts to support the weapon where practical.

If normal standing weapon IK creates severe deformation while prone, allow a separate prone weapon rig weight or pose.

A polished prone firing pose is not required if it would substantially expand the ticket.

The architecture should allow prone-specific poses later.

---

# 27. Jumping

Jump animations should continue functioning.

Weapon should remain attached during:

* Idle jump
* Walking jump
* Sprint jump

Upper-body rigging should not prevent jump animations from playing.

---

# 28. Dolphin Dive

The existing dolphin-dive gameplay must remain functional.

If weapon IK looks unreasonable during the dive:

* Reduce weapon rig weight temporarily.
* Keep the weapon attached to the character.
* Restore the normal weapon pose after entering prone.

Do not allow weapon posing to interfere with the dive mechanics.

---

# 29. Firing Compatibility

REQ-037 should prepare the third-person weapon for visible firing.

If existing third-person muzzle flashes or shooting effects already exist, they should remain aligned with the weapon.

The weapon prefab should expose a logical:

`Muzzle`

transform if one does not already exist.

Future firing/recoil animations should be able to use this structure.

Third-person recoil animation is not required unless already simple to support.

---

# 30. Weapon Model Orientation

The imported FBX files may not share identical forward/up axes.

The attachment system should normalize this through per-weapon socket offsets.

Do not modify player skeleton bones simply to compensate for incorrectly oriented weapon imports.

Each weapon should have its own configurable local alignment.

---

# 31. Debug / Setup Visualization

Make weapon setup easy to tune.

When selecting a weapon or player prefab, developers should be able to identify:

* Weapon socket
* Weapon root
* Left-hand IK target
* Muzzle
* Aim target if applicable

Use clearly named transforms.

Optional gizmos may be added if helpful.

---

# 32. Recommended Hierarchy

An example weapon prefab hierarchy could be:

`ThirdPerson_Pistol`

* `Model`
* `LeftHandIKTarget`
* `Muzzle`

`ThirdPerson_AK`

* `Model`
* `LeftHandIKTarget`
* `Muzzle`

`ThirdPerson_Shotgun`

* `Model`
* `LeftHandIKTarget`
* `Muzzle`

Exact naming may differ, but the architecture should be consistent.

---

# 33. Recommended Player Rig Structure

Conceptually:

`Player Character`

* `Animator`
* `Rig Builder`
* `Weapon Rig`

  * `Left Hand IK`
  * `Aim Constraint`
* `Skeleton`

  * `Right Hand`

    * `WeaponSocket`

Do not force this exact hierarchy if the current imported Mixamo skeleton requires a different structure.

---

# 34. Avoid Hard-Coding Weapon Logic Into Locomotion

The existing player animation controller should not become filled with special cases such as:

`if (AK) move left hand here`

Prefer reusable configuration.

Example concept:

`ThirdPersonWeaponDefinition`

containing:

* Weapon type
* Weapon prefab
* Right-hand position offset
* Right-hand rotation offset
* Left-hand IK target
* Hip pose settings
* Aim pose settings

This should make adding future weapons significantly easier.

---

# 35. Weapon Pose Extensibility

The system should support future weapons such as:

* SMGs
* Sniper rifles
* Revolvers
* Rocket launchers
* Melee weapons

without rewriting the player animation system.

New weapons should ideally require:

1. Weapon prefab
2. Socket alignment
3. Left-hand IK target
4. Pose configuration

---

# 36. Existing Gameplay Must Remain Intact

REQ-037 must not regress:

* First-person weapon positioning
* First-person ADS
* First-person firing
* Weapon switching
* Reloading
* Multiplayer
* Movement
* Sprint
* Crouch
* Prone
* Dolphin dive
* Jumping
* Damage
* Kill/respawn
* Bullseyes
* Grenades
* REQ-036 locomotion animations

Third-person weapon visuals should observe gameplay state rather than control it.

---

# 37. Acceptance Criteria

REQ-037 is complete when:

### General

* Other players see the correct equipped weapon.
* Weapon remains attached to the player during movement.
* Weapon follows the animated skeleton.
* No significant floating weapon behavior occurs.
* Weapon alignment can be tuned per weapon in the Inspector.

### Pistol

* Right hand visibly holds the pistol.
* Left hand supports the pistol/right hand.
* Pistol sits relatively high near the upper chest/neck area.
* Aiming raises the pistol toward eye level.

### AK

* Right hand holds the rear/main grip.
* Left hand supports the forward section.
* Weapon sits appropriately against or near the shoulder.
* Aiming raises the rifle closer to the player's eye/head.

### Shotgun

* Right hand holds the rear/main grip.
* Left hand supports the fore-end/pump.
* Shotgun remains visually stable during movement.
* Aiming raises it toward the head/shoulder sighting position.

### Animation

* Standing locomotion continues working.
* Walking continues working.
* Sprint animation continues working.
* Crouching animations continue working.
* Prone animations continue working.
* Jump animations continue working.
* Weapon IK does not lock or destroy the lower-body locomotion animation.

### Aiming

* Remote players can visually identify when another player is aiming.
* Third-person weapon moves closer to the eyes/head while aiming.
* Weapon roughly follows vertical aim pitch.
* Extreme aim angles do not visibly destroy the rig.

### Networking

* Equipped weapon is correct for all clients.
* Aim state appears correctly for remote players.
* Third-person posing does not require networking individual bone transforms.
* Weapon switching correctly updates the remote model.

---

# 38. Testing Checklist

Test with two multiplayer clients.

Have Player 2 observe Player 1 during:

1. Standing with pistol.
2. Walking with pistol.
3. Aiming pistol.
4. Shooting pistol.
5. Crouching with pistol.
6. Sprinting with pistol.
7. Jumping with pistol.
8. Switching to AK.
9. Standing with AK.
10. Walking with AK.
11. Aiming AK.
12. Looking upward while aiming AK.
13. Looking downward while aiming AK.
14. Crouching with AK.
15. Sprinting with AK.
16. Switching to shotgun.
17. Standing with shotgun.
18. Walking with shotgun.
19. Aiming shotgun.
20. Crouching with shotgun.
21. Going prone with each weapon where practical.
22. Leaving prone.
23. Dolphin diving.
24. Dying while armed.
25. Respawning and verifying correct weapon state.

Repeat key tests with Player 1 observing Player 2.

---

# 39. Future Improvements Not Required for REQ-037

The following can be handled in later tickets:

* Dedicated weapon-holding animation clips.
* Weapon-specific idle animations.
* Weapon-specific walk animations.
* Weapon-specific sprint animations.
* Third-person recoil.
* Reload animation synchronization.
* Third-person grenade throw animation.
* Detailed prone firearm poses.
* Shoulder switching.
* Procedural foot placement.
* Head/eye look-at.
* Full procedural aim-offset system.
* Weapon sway visible to remote players.

The goal of REQ-037 is to establish a convincing and extensible **third-person weapon-holding and aiming system** that works with the current Mixamo locomotion animations.
