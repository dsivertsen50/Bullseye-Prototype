# REQ-024 — Weapon-Specific Animations and Sprint Weapon Animation Integration

## 1. Summary

Improve first-person weapon animation behavior so that the pistol, AK, and shotgun no longer all use effectively the same animation set.

The current prototype has working weapon animations, but the rifle and shotgun appear to inherit or reuse pistol-style animations. This does not visually make sense and makes the weapons feel too similar.

REQ-024 should:

* Preserve the current functional weapon system.
* Give the Pistol, AK, and Shotgun appropriate weapon-specific first-person animation behavior.
* Reuse appropriate animations from the imported Cowsins FPS Engine wherever practical.
* Add or improve a dedicated sprint weapon pose/animation.
* Ensure sprinting looks meaningfully different from ordinary walking.
* Keep animation setup generic enough to support additional weapons later.
* Avoid rewriting functioning shooting, ammo, pickup, networking, or damage systems.

The goal is for each weapon to feel like a different physical object in the player's hands.

---

# 2. Current Problem

The prototype currently has:

```text
Pistol
AK
Shotgun
```

However, the AK and Shotgun currently appear to use animation behavior that is essentially the same as the pistol.

Examples may include:

* Similar idle movement.
* Similar firing movement.
* Similar aiming behavior.
* Similar reload/equip behavior.
* Similar weapon positioning.
* Similar movement bob.

This makes larger weapons appear unnaturally pistol-like.

There is also currently little or no visually distinct weapon behavior while sprinting.

The weapon appears to continue using approximately the normal walking motion during sprint.

---

# 3. Desired Result

Each weapon should have animation behavior appropriate to its general weapon type.

Conceptually:

```text
Pistol
→ compact one-handed/two-handed handgun behavior

AK
→ rifle-style shoulder-mounted behavior

Shotgun
→ long-gun / shotgun-style behavior
```

The exact final animation quality does not need to be AAA or final-production quality.

However, the difference between weapon classes should be visually obvious.

---

# 4. Use the Existing FPS Engine Assets

The project already contains the imported Cowsins FPS Engine.

Where compatible, inspect and reuse its existing:

* Weapon animations
* Animator Controllers
* Animation Clips
* Weapon movement behavior
* Equip animations
* Holster animations
* Reload animations
* ADS transitions
* Sprint poses
* Sprint animations
* Weapon sway
* Procedural motion systems

Do not recreate equivalent animations from scratch if appropriate existing assets are already available.

---

# 5. Important Integration Constraint

The existing Bullseye gameplay code should remain authoritative for the game's weapon behavior.

Cowsins should be treated primarily as a source of:

```text
Animation
Presentation
Motion
Audio/visual behavior
```

rather than replacing the current multiplayer weapon architecture.

Do not reintroduce the full Cowsins gameplay system if that would conflict with:

* Netcode for GameObjects
* Current weapon ownership
* Revised REQ-023 inventory behavior
* Existing damage logic
* Bullseye hit detection
* Ammo handling
* Temporary weapon pickups
* Respawning

Prefer adapting useful presentation components into the current architecture.

---

# 6. Custom Weapon Models

REQ-024 should operate on the custom weapon assets currently used by the project:

```text
Assets/Weapons/Pistol/Prefab/
Assets/Weapons/AK/Prefab/
Assets/Weapons/Shotgun/Prefab/
```

Exact prefab filenames should be discovered from the project rather than assumed.

The current Ruger model is being replaced by the custom Pistol model.

The new custom models should remain the visible weapon meshes.

Do not replace them with Cowsins weapon models.

---

# 7. Cowsins Models Are Animation References Only

If Cowsins animations are authored around its own weapon meshes, those meshes may be used temporarily during implementation to understand:

* Hierarchy
* Rig structure
* Animator setup
* Pivot locations
* Hand targets
* Animation requirements

But the final visible first-person weapons should be the custom:

* Pistol
* AK
* Shotgun

whenever technically practical.

---

# 8. Determine Animation Compatibility

Before modifying the weapon system significantly, inspect how Cowsins implements its weapon animation.

Determine whether relevant movement is driven primarily by:

```text
Animator / AnimationClips
```

or:

```text
Procedural code
```

or:

```text
A combination of both
```

Identify reusable components rather than assuming every animation clip can simply be assigned to the new FBX.

---

# 9. Avoid Blind Animation Assignment

Do not simply assign a Cowsins animation clip to a custom model if:

* The hierarchy is incompatible.
* Required bones do not exist.
* Object paths differ.
* The clip would silently fail.
* The model visibly deforms incorrectly.

If direct animation reuse is incompatible, reproduce the **behavior** using the existing weapon root transforms or procedural movement rather than forcing incompatible rigs.

---

# 10. Weapon Animation Profiles

Prefer a reusable weapon-animation configuration system.

Conceptually, weapons could reference an:

```text
WeaponAnimationProfile
```

or equivalent configuration.

Examples:

```text
Pistol Animation Profile
Rifle Animation Profile
Shotgun Animation Profile
```

This could be implemented through:

* ScriptableObjects
* Animator Override Controllers
* Existing Cowsins configuration assets
* Existing weapon definitions
* Another clean data-driven approach

Use the simplest architecture that fits the current project.

---

# 11. Do Not Create Per-Weapon Code Unless Necessary

Avoid architecture such as:

```text
PistolAnimationController.cs
AKAnimationController.cs
ShotgunAnimationController.cs
```

if the differences can instead be represented through configuration.

Prefer:

```text
WeaponAnimationController
        ↓
WeaponAnimationProfile
        ↓
Pistol / Rifle / Shotgun configuration
```

This will make future weapons easier to add.

---

# 12. Pistol Animation Behavior

The Pistol should retain or gain motion appropriate for a handgun.

The current pistol animation may already be the closest to correct.

Preserve good existing behavior where practical.

Pistol behavior should include appropriate:

* Idle pose
* Walking movement
* Firing recoil
* ADS
* Equip
* Holster
* Reload
* Sprint behavior

Do not unnecessarily degrade currently working pistol animation.

---

# 13. AK Animation Behavior

The AK should behave as a long gun / rifle.

It should not visually move like a pistol.

Desired general characteristics:

* Longer weapon held farther forward.
* Rifle-style shoulder orientation.
* Appropriate rifle idle sway.
* Rifle-style walking motion.
* More substantial recoil movement.
* Appropriate ADS positioning.
* Appropriate rifle equip/holster behavior.
* Dedicated rifle sprint pose.

Reuse the closest appropriate Cowsins rifle animation setup.

---

# 14. Shotgun Animation Behavior

The Shotgun should also use a long-gun animation style but should be visually distinguishable from the AK where practical.

Desired characteristics:

* Appropriate shotgun hold position.
* Longer weapon movement.
* Stronger/heavier firing motion.
* Appropriate ADS.
* Appropriate sprint pose.
* Appropriate reload behavior for the weapon.

Use the closest available Cowsins shotgun animation configuration if one exists.

---

# 15. Shotgun Reload

If the selected shotgun design supports individual shell loading, and Cowsins includes compatible shell-by-shell reload animation, it may be used.

However, a full shell-loading system is not mandatory if it would substantially expand REQ-024.

The higher priority is:

```text
Correct weapon-specific visual motion
```

rather than completely rebuilding shotgun reload logic.

If the existing shotgun currently reloads as a magazine weapon, preserve functional reload behavior unless a safe animation-compatible improvement is straightforward.

---

# 16. Fire Animation

Each weapon should have an appropriate firing animation.

### Pistol

Compact handgun recoil.

### AK

Rifle recoil appropriate to an automatic/semi-automatic long gun.

### Shotgun

Noticeably stronger/heavier recoil.

The Shotgun should not visually recoil exactly like the Pistol.

---

# 17. Weapon-Specific Recoil

Where practical, expose weapon recoil animation/motion values through weapon configuration.

Examples:

```text
Recoil Distance
Recoil Rotation
Recovery Speed
Camera Kick
Weapon Kick
```

The values should not necessarily be identical across all weapons.

Do not hardcode one recoil profile globally if the architecture already supports per-weapon settings.

---

# 18. ADS Animation

Aiming down sights should position each weapon independently.

The custom models will likely have different sight locations.

Each weapon therefore needs configurable ADS positioning.

For example:

```text
Pistol ADS Position
Pistol ADS Rotation

AK ADS Position
AK ADS Rotation

Shotgun ADS Position
Shotgun ADS Rotation
```

or equivalent sight-transform targets.

---

# 19. ADS Alignment

When ADS is active:

* The Pistol sights should approximately align with the screen center.
* The AK sights should approximately align with the screen center.
* The Shotgun aiming point should approximately align with the screen center.

Exact final weapon optics are not required.

The goal is to avoid obvious misalignment.

---

# 20. Hip-Fire Position

Each weapon should have its own hip-fire resting transform.

Do not assume the transform that looks good for the pistol will also look good for:

* AK
* Shotgun

Expose or preserve configurable:

```text
Hip Position
Hip Rotation
```

per weapon/profile.

---

# 21. Equip Animation

When a temporary weapon is acquired, it should enter view using an appropriate equip/draw animation where available.

Examples:

```text
Pick up AK
↓
AK rises/enters first-person position
```

or:

```text
Switch from Pistol
↓
Pistol lowers
↓
AK raises
```

Use Cowsins animation behavior if compatible.

---

# 22. Holster Animation

Switching away from a weapon should use an appropriate lower/holster transition.

The weapon should not instantly disappear if a functional animation system is available.

Example:

```text
AK active
↓
Press Y
↓
AK lowers
↓
Pistol raises
```

---

# 23. Preserve Weapon Switching Inputs

Continue using the existing weapon-switch bindings established by the revised weapon system.

Expected inputs include:

```text
Mouse Wheel
Q
Gamepad North Button
```

Examples:

```text
Xbox:
Y
```

REQ-024 should change weapon presentation, not the weapon-switch controls.

---

# 24. Sprint Animation — Major Requirement

Add a clearly distinct weapon presentation while sprinting.

Currently, sprinting appears too similar to ordinary walking.

This should be corrected.

When sprint begins:

```text
Normal weapon pose
↓
Sprint transition
↓
Dedicated sprint pose/motion
```

When sprint ends:

```text
Sprint pose
↓
Transition
↓
Normal weapon pose
```

---

# 25. Sprint Pose

The sprint pose should visually communicate that the player is moving quickly and is not immediately ready to fire.

Examples may include:

### Pistol

* Pistol lowers.
* Weapon shifts toward one side.
* Increased movement amplitude.

### AK / Shotgun

* Long gun lowers or angles away from the center.
* Weapon may rotate slightly.
* Stronger running bob.
* More pronounced forward/back motion.

Prefer animation behavior similar to the Cowsins FPS Engine demo where available.

---

# 26. Sprint Must Not Look Like Fast Walking

Simply increasing the speed of the normal walking bob is not sufficient.

The weapon should visibly change orientation or position.

The difference between:

```text
Walk
```

and:

```text
Sprint
```

should be immediately recognizable from first-person view.

---

# 27. Sprint Transition

Do not snap instantly into the sprint pose.

Use a short transition.

Example:

```text
Normal
→ 0.1–0.25 second transition
→ Sprint
```

and similarly when leaving sprint.

Exact timing can be tuned.

---

# 28. Weapon-Type Sprint Differences

The same broad sprint system may be reused across weapon classes, but the final poses should support different offsets.

Example conceptual values:

```text
Pistol Sprint Offset
AK Sprint Offset
Shotgun Sprint Offset
```

Long weapons should not be forced into the same transform as the Pistol.

---

# 29. Sprint Animation Source

Inspect Cowsins for existing:

* Sprint animation clips
* Sprint states
* Weapon sprint positioning
* Procedural sprint motion
* Animator parameters

Reuse these where practical.

If direct reuse is impossible because of custom weapon hierarchy differences, recreate the same style of movement procedurally on a shared weapon root.

---

# 30. Firing While Sprinting

Preserve the current intended gameplay rules.

If firing while sprinting is already blocked, continue blocking it.

If sprint is automatically canceled when firing, preserve that behavior.

Do not introduce the ability to fire normally from the lowered sprint pose accidentally.

Preferred behavior:

```text
Player sprints
↓
Weapon lowered
↓
Player attempts to fire
↓
Sprint ends
↓
Weapon returns toward ready position
↓
Fire allowed according to current mechanics
```

Use the existing sprint/fire relationship if already established.

---

# 31. ADS While Sprinting

Do not allow the player to remain in full sprint pose and ADS simultaneously.

Preferred behavior:

```text
Sprint active
↓
Player activates ADS
↓
Sprint exits
↓
Weapon transitions to ADS
```

Preserve current gameplay behavior if this is already implemented.

---

# 32. Reload While Sprinting

Do not create visually broken combinations where:

```text
Sprint pose
+
Reload animation
```

play simultaneously in incompatible ways.

Use the current gameplay rule if one exists.

Otherwise, acceptable options include:

* Cancel sprint when reload begins.
* Prevent reload until sprint ends.

Choose whichever aligns best with the existing movement/weapon system.

---

# 33. Movement Animation States

The first-person weapon animation system should support at minimum:

```text
Idle
Walk
Sprint
ADS
Fire
Reload
Equip
Holster
```

Not all need to be separate Animator states if procedural motion is cleaner.

The observable behavior matters more than the exact implementation.

---

# 34. Idle Weapon Motion

Preserve or improve the subtle breathing/sway behavior already integrated from Cowsins.

Idle motion may vary slightly between:

* Pistol
* AK
* Shotgun

Long weapons may appropriately feel heavier/slower.

Do not make the weapon completely static when idle unless necessary.

---

# 35. Walking Motion

Walking should retain appropriate weapon bob.

However, avoid excessive movement that:

* Obscures aiming.
* Causes motion sickness.
* Makes the reticle unreliable.
* Makes animation look cartoonish.

The motion can be tuned later.

---

# 36. Sprint Motion

Sprint may be more exaggerated than walking.

This can include:

* Increased bob.
* Lowered weapon.
* Side-to-side movement.
* Different weapon angle.

The player should be able to visually tell that sprint is active even without looking at movement speed.

---

# 37. Weapon Root Architecture

If necessary, establish a shared transform hierarchy such as:

```text
WeaponRoot
├── AnimationRoot
│   └── VisualModel
└── MuzzlePoint
```

or equivalent.

The exact hierarchy is flexible.

The goal is to allow:

* Generic procedural movement.
* Per-weapon model offsets.
* Animation.
* Recoil.
* ADS.
* Sprint positioning.

without modifying the raw imported FBX asset.

---

# 38. Do Not Modify Source FBX Transform Data Unnecessarily

The custom FBX files should remain reusable raw assets.

Prefer adjusting:

```text
Wrapper prefab transforms
Weapon visual root
ADS targets
Animation roots
```

rather than destructively altering the imported FBX.

---

# 39. Animation Events

If imported Cowsins animation clips rely on Animation Events, inspect them carefully.

Do not allow animation events to:

* Spawn Cowsins projectiles.
* Apply duplicate damage.
* Add ammunition.
* Trigger incompatible inventory logic.
* Call missing Cowsins scripts.

Only retain/use events that are safe and necessary.

The existing Bullseye weapon logic should remain responsible for gameplay outcomes.

---

# 40. Animation vs Gameplay Separation

Visual animation must not independently determine authoritative combat state.

Example:

```text
Animation:
Shotgun fires visually

Gameplay:
Existing Bullseye shooting logic determines hit/damage
```

Do not let imported animation scripts create a second shooting system.

---

# 41. Network Behavior

First-person weapon animations should primarily be local presentation.

Do not synchronize every small:

* Sway movement
* Sprint bob
* Recoil frame
* ADS interpolation

over the network.

Only synchronize weapon/gameplay states where required by the existing architecture.

This reduces unnecessary network traffic.

---

# 42. Remote Player Representation

If the project currently has third-person weapon animation support, preserve it.

However, REQ-024 primarily concerns:

```text
Local first-person weapon animation
```

Full remote third-person animation for every weapon is not required unless already straightforward.

---

# 43. Temporary Weapon Pickup Integration

REQ-024 must remain compatible with the revised REQ-023 system.

The player always has:

```text
Permanent Pistol
```

and may have:

```text
One Temporary Weapon
```

When the player picks up:

```text
AK
```

the AK should use its rifle animation profile.

When the player picks up:

```text
Shotgun
```

the Shotgun should use its shotgun animation profile.

When the temporary weapon is:

* Dropped
* Exhausted
* Lost on death

the system should return to the Pistol animation profile appropriately.

---

# 44. Runtime Animation Profile Switching

Animation behavior must update automatically when the active weapon changes.

Example:

```text
Pistol active
→ Pistol animation profile
```

Switch:

```text
AK active
→ Rifle animation profile
```

Switch back:

```text
Pistol active
→ Pistol animation profile
```

Do not require manual Inspector changes during runtime.

---

# 45. Shotgun / AK Swap

If:

```text
Player has Pistol + AK
```

and swaps AK for Shotgun:

```text
AK world weapon spawned
Shotgun acquired
```

the first-person animation configuration must update to the Shotgun profile.

No AK animation references should remain active on the Shotgun.

---

# 46. Missing Animation Fallback

The system should fail gracefully if a specific animation clip is not assigned.

For example, if the Shotgun currently lacks a unique reload animation:

* Continue using a safe generic reload temporarily.
* Do not throw a NullReferenceException.
* Do not prevent the weapon from firing.
* Log a clear warning if useful.

Functional gameplay is preferable to a broken weapon due to one missing animation.

---

# 47. Inspector Configuration

Expose useful animation configuration in the Inspector or weapon data assets.

Possible fields:

```text
Animation Profile

Idle Pose / Offset
Walk Offset
Sprint Offset
ADS Offset

Walk Bob Amount
Walk Bob Speed

Sprint Bob Amount
Sprint Bob Speed

Equip Duration
Holster Duration

Recoil Position
Recoil Rotation
Recoil Recovery

Animator Controller
Animator Override Controller
```

Do not expose every internal variable unnecessarily.

The goal is to make weapon tuning practical without editing code.

---

# 48. Cowsins Asset Safety

Do not move or delete the original Cowsins source assets unnecessarily.

If clips/controllers need modification:

Prefer:

```text
Duplicate into Bullseye-owned folder
```

rather than editing the package/source asset directly.

Suggested location:

```text
Assets/Weapons/Animations/
```

or another existing project-appropriate directory.

This helps avoid breaking the Cowsins demo/reference content.

---

# 49. Suggested Folder Structure

If useful:

```text
Assets
└── Weapons
    ├── Animations
    │   ├── Shared
    │   ├── Pistol
    │   ├── Rifle
    │   └── Shotgun
    ├── Pistol
    ├── AK
    └── Shotgun
```

Do not reorganize working assets solely to match this example.

---

# 50. Animation Retargeting

If Cowsins clips directly animate weapon transforms rather than humanoid hand rigs, inspect whether they can be duplicated/retargeted to the custom weapon hierarchy.

If retargeting is impractical, prefer:

```text
Procedural root movement
+
Custom per-weapon offsets
```

over spending excessive effort forcing incompatible animation clips.

---

# 51. Hands / Arms

If the current prototype does not yet use finalized first-person arms/hands, do not make final arm animation a blocker for this requirement.

The key requirement is that the **weapon itself** demonstrates appropriate:

* Position
* Rotation
* Recoil
* Sprint behavior
* ADS
* Equip/holster movement

Hand/arm refinement can occur later.

---

# 52. Preserve Existing Weapon Functionality

Do not break:

* Pistol firing.
* AK firing.
* Shotgun firing.
* Damage.
* Bullseye hit detection.
* Reloads.
* Ammo depletion.
* Infinite pistol reserve.
* Temporary weapon finite ammo.
* Temporary weapon exhaustion.
* Weapon pickup.
* Weapon swap.
* Dropped weapon ammo preservation.
* Death drops.
* Respawning.
* Multiplayer ownership.
* Controller input.
* Mouse/keyboard input.

---

# 53. Acceptance Criteria

REQ-024 is complete when:

* [ ] The Pistol uses appropriate handgun-style first-person animation behavior.
* [ ] The AK no longer visually behaves like the Pistol.
* [ ] The AK uses rifle-appropriate first-person movement.
* [ ] The Shotgun no longer visually behaves like the Pistol.
* [ ] The Shotgun uses long-gun/shotgun-appropriate movement.
* [ ] Cowsins FPS Engine animation/motion assets are reused where technically appropriate.
* [ ] Custom Pistol, AK, and Shotgun models remain the visible weapon models.
* [ ] Cowsins gameplay systems do not replace the existing Bullseye weapon/network systems.
* [ ] Each weapon supports a configurable hip-fire position.
* [ ] Each weapon supports a configurable ADS position.
* [ ] AK sights can be approximately aligned when aiming.
* [ ] Pistol sights can be approximately aligned when aiming.
* [ ] Shotgun aiming can be approximately aligned.
* [ ] Firing animation/recoil differs appropriately by weapon class.
* [ ] The Shotgun recoil feels visually heavier than the Pistol.
* [ ] Walking retains functional weapon movement.
* [ ] Sprinting has a visibly distinct weapon pose/motion.
* [ ] Sprinting does not simply look like faster walking.
* [ ] Pistol sprint presentation works.
* [ ] AK sprint presentation works.
* [ ] Shotgun sprint presentation works.
* [ ] Sprint transitions smoothly into and out of the sprint pose.
* [ ] Weapon switching appropriately changes animation profiles.
* [ ] Pistol → AK switching works.
* [ ] AK → Pistol switching works.
* [ ] Pistol → Shotgun switching works.
* [ ] Shotgun → Pistol switching works.
* [ ] Replacing AK with Shotgun updates the animation setup correctly.
* [ ] Missing optional animation clips do not break gameplay.
* [ ] Existing ammo and pickup behavior from revised REQ-023 remains functional.
* [ ] Existing multiplayer behavior remains functional.
* [ ] No duplicate Cowsins shooting/damage system is introduced.

---

# 54. Testing Procedure

## Test A — Pistol

1. Spawn.
2. Observe Pistol while idle.
3. Walk.
4. Sprint.
5. ADS.
6. Fire.
7. Reload.

Expected:

The Pistol behaves appropriately and existing functionality remains intact.

---

## Test B — AK Pickup

1. Pick up the AK.
2. Observe the weapon immediately after equip.
3. Walk.
4. Sprint.
5. ADS.
6. Fire.
7. Reload.

Expected:

The AK clearly behaves as a rifle and does not simply reuse the Pistol's presentation.

---

## Test C — Shotgun Pickup

1. Replace the AK with the Shotgun.
2. Observe equip animation.
3. Walk.
4. Sprint.
5. ADS.
6. Fire.
7. Reload.

Expected:

The Shotgun has appropriate long-gun behavior and heavier-looking recoil.

---

## Test D — Sprint Comparison

Test each weapon while walking and sprinting.

Compare:

```text
Pistol Walk
Pistol Sprint

AK Walk
AK Sprint

Shotgun Walk
Shotgun Sprint
```

Expected:

For all three weapons, sprinting is visually distinct from walking.

---

## Test E — Sprint Transition

1. Begin walking.
2. Start sprinting.
3. Stop sprinting repeatedly.

Expected:

The weapon smoothly enters and leaves its sprint position.

No abrupt snapping.

---

## Test F — Sprint + Fire

1. Sprint with each weapon.
2. Attempt to fire.

Expected:

Existing intended sprint/fire behavior is preserved.

The weapon should not fire incorrectly from a visually broken sprint state.

---

## Test G — Sprint + ADS

1. Sprint.
2. Activate ADS.

Expected:

Sprint and ADS do not remain active in visually incompatible states.

---

## Test H — Weapon Switching

Player has:

```text
Pistol + AK
```

Repeatedly switch using:

```text
Mouse Wheel
Q
Xbox Y
```

Expected:

Animation profiles switch correctly each time.

No model/animation mismatch occurs.

---

## Test I — Temporary Weapon Replacement

1. Pick up AK.
2. Pick up Shotgun.

Expected:

AK drops normally.

Shotgun becomes active.

Shotgun animation behavior becomes active immediately.

No AK animation configuration remains incorrectly applied.

---

## Test J — Temporary Weapon Exhaustion

1. Acquire AK.
2. Use all of its ammunition.

Expected:

AK is removed according to revised REQ-023.

Pistol returns.

Pistol animation profile is restored correctly.

---

## Test K — Death

1. Acquire Shotgun.
2. Die.

Expected:

Shotgun drops with remaining ammunition.

On respawn:

```text
Pistol only
```

Pistol animation behavior is correct.

---

## Test L — Multiplayer

Run two players.

Give:

```text
Player 1:
AK

Player 2:
Shotgun
```

Have both:

* Walk
* Sprint
* ADS
* Fire
* Switch weapons

Expected:

Each player's local first-person weapon behaves correctly.

One player's animation state does not change the other player's local weapon presentation.

---

# 55. Out of Scope

REQ-024 does not require:

* Final professional animation polish.
* Final hand/arm models.
* Motion-captured custom animation.
* Full third-person locomotion animation overhaul.
* Final weapon balancing.
* New weapon damage systems.
* New ammo systems.
* Weapon spawn timers.
* New pickup architecture.
* New networking architecture.
* Final muzzle-flash effects.
* Final sound design.
* Inspect/reload weapon animations.
* Melee animations.
* Weapon attachments.

These can be addressed separately.

---

# 56. Implementation Priority

Prioritize work in this order:

1. Inspect existing Cowsins weapon animation architecture.
2. Identify reusable pistol/rifle/shotgun animation or procedural-motion systems.
3. Preserve current Bullseye gameplay weapon architecture.
4. Establish reusable weapon-animation profiles/configuration.
5. Keep/finish Pistol animation behavior.
6. Give AK a proper rifle animation profile.
7. Give Shotgun a proper long-gun animation profile.
8. Add dedicated sprint pose/movement.
9. Configure per-weapon ADS offsets.
10. Configure per-weapon recoil.
11. Verify equip/holster transitions.
12. Regression-test revised REQ-023 and multiplayer.

---

# 57. Final Intended Experience

The player should be able to immediately recognize the physical difference between weapons through first-person motion.

The desired experience is:

```text
Spawn
↓
Pistol feels compact and lightweight
↓
Sprint
→ Pistol visibly lowers/moves into sprint pose
↓
Pick up AK
↓
AK feels like a shoulder-fired rifle
↓
Sprint
→ AK lowers/angles appropriately
↓
Fire
→ Rifle-style recoil
↓
Swap for Shotgun
↓
Shotgun feels larger/heavier
↓
Fire
→ Stronger shotgun-style recoil
```

The three weapons should no longer feel like the same weapon with different models.

REQ-024 should establish the reusable animation foundation for all future Bullseye weapons.
