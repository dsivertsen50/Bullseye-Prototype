# REQ-027 — Weapon-Specific Reload, Sprint, and Handling Animations

## Summary

Improve first-person weapon animation behavior so that the pistol, AK, shotgun, and future weapons feel visually distinct.

The current weapon animation implementation still shares too much behavior between weapon types.

REQ-027 should establish a modular weapon-animation system in which each weapon can define or reference its own animations for:

* firing,
* reloading,
* idle movement,
* walking,
* sprinting,
* aiming,
* weapon draw/equip,
* and other relevant first-person states.

Where practical, reuse compatible animations from the imported Cowsins FPS Engine rather than creating new animations from scratch.

A particularly important issue to address is the current shotgun sprint animation.

At present, the shotgun is largely pulled downward and out of the player's view while sprinting.

This does not feel appropriate.

Instead, the shotgun should remain visibly carried in front of the player and exhibit a noticeable side-to-side sprint sway similar in spirit to the current AK sprint behavior.

The system must remain modular so future weapons can have unique animation sets without requiring changes to the core player or weapon-animation code.

---

# Goals

REQ-027 should:

* make each weapon feel visually distinct,
* add reload animations,
* improve sprinting weapon presentation,
* remove inappropriate shared pistol-like animation behavior,
* reuse Cowsins animations where appropriate,
* provide configurable animation references per weapon,
* support future weapons cleanly,
* preserve current multiplayer behavior,
* preserve existing weapon switching,
* preserve shooting, damage, and reticle systems.

---

# 1. Weapon-Specific Animation Profiles

Each weapon should be able to define its own animation profile.

Prefer extending the existing weapon configuration architecture.

Conceptually:

```text
WeaponAnimationProfile
```

A profile may contain references for:

```text
Idle

Walk

Sprint

Aim

Fire

Reload

Equip / Draw

Unequip / Holster

Empty Fire

Optional Future States
```

Exact naming and implementation should follow the existing architecture.

Do not hard-code animation decisions based on names such as:

```text
if weapon == "AK"
if weapon == "Shotgun"
if weapon == "Pistol"
```

Weapon differences should come from data/configuration.

---

# 2. Reuse Cowsins FPS Engine Animations

The project currently includes the Cowsins FPS Engine.

Inspect the Cowsins animation assets and determine whether suitable animations exist for:

* pistol reload,
* rifle reload,
* shotgun reload,
* pistol sprint,
* rifle sprint,
* shotgun sprint,
* idle,
* aiming,
* firing,
* equip/draw.

Prefer reusing Cowsins assets when:

* they are compatible with the current first-person weapon setup,
* they look appropriate,
* and they can be integrated without importing unnecessary Cowsins gameplay systems.

Do not replace the Bullseye project's core weapon system with Cowsins weapon logic solely to obtain animations.

The goal is to reuse animation assets and useful animation behavior while preserving the project's existing architecture.

---

# 3. Reload Animations

Each current weapon should have an appropriate reload animation.

At minimum:

```text
Pistol
→ pistol-style magazine reload

AK
→ rifle-style magazine reload

Shotgun
→ shotgun-appropriate reload
```

The weapons should no longer appear to share one generic reload behavior.

---

# 4. Pistol Reload

The pistol should use a reload animation appropriate to a handgun.

The animation should visually communicate:

1. lowering or repositioning the pistol,
2. removing/replacing the magazine,
3. returning the pistol to firing position.

If a suitable Cowsins pistol reload exists, prefer using it.

Prototype-level animation quality is acceptable.

---

# 5. AK Reload

The AK should use a rifle-specific reload animation.

The animation should feel noticeably heavier and more substantial than the pistol reload.

It should visually communicate a magazine change or equivalent reload action appropriate to the rifle.

Prefer a compatible Cowsins rifle reload if available.

---

# 6. Shotgun Reload

The shotgun should use an animation that visually reads as a shotgun reload.

If the currently imported shotgun is visually compatible with:

* a shell-by-shell reload,
* magazine-fed reload,
* or another appropriate animation,

use the best available match.

Do not force a pistol or generic rifle reload animation onto the shotgun.

For this requirement, the reload animation does not need to implement detailed shell-by-shell ammunition logic unless that is already easy to support.

The primary goal is correct visual presentation.

---

# 7. Reload Animation Timing

Reload duration and gameplay reload duration should correspond.

The player should not regain firing capability substantially before the reload animation visually completes.

Likewise, the player should not remain unable to fire for a long period after the visual reload has obviously finished.

Prefer:

```text
Gameplay Reload Duration
≈
Animation Duration
```

Exact values should remain configurable per weapon.

---

# 8. Firing During Reload

While a reload animation is active:

* normal firing should be prevented,
* repeated reload requests should not restart the animation,
* weapon state should remain consistent.

If the existing system supports reload cancellation, preserve it.

Do not add complex reload cancellation logic unless already supported.

---

# 9. Weapon Switching During Reload

Weapon switching should not leave the animation system in a broken reload state.

If the player switches weapons during a reload:

* cancel the previous weapon's reload cleanly if permitted,
* or prevent switching until the reload reaches an appropriate point if that is how the current system works.

The newly equipped weapon must enter its correct animation state.

---

# 10. Distinct Weapon Idle Behavior

Each weapon should feel somewhat different even while the player is standing still.

Avoid using identical idle sway values for all weapons.

For example:

### Pistol

```text
Light
Compact
Quick
Minimal screen obstruction
```

### AK

```text
Heavier
More pronounced breathing motion
Longer visual profile
```

### Shotgun

```text
Heavy
Stable
Slower sway
Substantial presence on screen
```

These are design targets, not strict mathematical requirements.

---

# 11. Walking Animation Differences

Walking weapon motion should also reflect weapon type.

The pistol may have:

* faster,
* lighter movement.

The AK may have:

* moderate sway,
* more inertia.

The shotgun may have:

* heavier,
* slower movement.

Do not exaggerate these effects so much that aiming becomes uncomfortable.

---

# 12. Sprint Animation — General Requirement

Sprinting should have a clearly distinct weapon animation from walking.

REQ-027 must ensure that sprinting does not merely appear to be:

```text
walking animation played faster
```

or:

```text
weapon moved downward
```

Instead, the first-person weapon should visibly respond to the player running.

---

# 13. AK Sprint Animation

The current AK sprint animation is a useful reference for the desired feel.

The AK should:

* remain visible,
* shift into a lower running-ready position,
* move rhythmically,
* sway laterally,
* communicate player momentum.

Preserve or improve the current AK behavior.

---

# 14. Critical Shotgun Sprint Fix

The current shotgun sprint behavior is unsatisfactory.

At present, sprinting largely causes the shotgun to move downward and disappear partially or substantially from the player's view.

This should be replaced.

The shotgun should remain clearly visible while sprinting.

Its sprint motion should include:

* lateral side-to-side sway,
* slight vertical movement,
* a lowered but still visible carry position,
* rhythmic movement corresponding to running.

Conceptually:

```text
Current:

Normal
   ↓
Sprint
   ↓
Shotgun drops mostly out of view
```

Replace with:

```text
Normal
   ↓
Sprint
   ↓
Shotgun lowers slightly
+
swings rhythmically side to side
+
remains visible
```

---

# 15. Shotgun Sprint Reference

Use the AK's current sprint animation as a behavioral reference.

The shotgun does not need to copy it exactly.

Instead, the shotgun should have a heavier version of the same general idea.

Suggested feel:

```text
AK Sprint
→ quicker lateral motion

Shotgun Sprint
→ broader, heavier lateral motion
→ slightly slower rhythm
```

The shotgun should feel like a heavier weapon being carried while running.

---

# 16. Sprint Transition

Entering sprint should smoothly transition from the current weapon state into the sprint state.

Avoid instant snapping.

Conceptually:

```text
Idle / Walk
     ↓
short transition
     ↓
Sprint Pose
```

Stopping sprint should smoothly transition back.

---

# 17. Post-Sprint Transition

When sprinting stops:

* the weapon should return naturally toward its normal ready position,
* the sprint sway should fade out,
* the weapon should not instantly teleport back into position.

This should work well alongside the sprint accuracy recovery introduced in REQ-025.

---

# 18. Sprint Animation and REQ-025

REQ-025 causes the reticle to widen while sprinting.

REQ-027 should visually reinforce the same gameplay state.

During sprint:

```text
Weapon:
visibly moving more aggressively

Reticle:
expanded

Accuracy:
reduced
```

When sprinting stops:

```text
Weapon:
returns to ready position

Reticle:
contracts

Accuracy:
recovers
```

These systems should feel synchronized.

---

# 19. Aim Animation

Each weapon should maintain an appropriate aiming behavior.

If compatible aim animations or poses exist in Cowsins, use them where useful.

The transition between:

```text
Hip Fire
↔
Aim
```

should remain smooth.

Do not allow the weapon to jump abruptly between poses.

---

# 20. Sprinting While Aiming

Preserve current gameplay rules regarding whether aiming is allowed while sprinting.

REQ-027 should not independently change those rules.

If aiming automatically stops when sprinting begins:

* transition cleanly from aim to sprint.

If sprinting stops:

* return to the appropriate ready state.

---

# 21. Fire Animation

Ensure each weapon's firing animation suits the weapon.

Examples:

### Pistol

```text
Quick recoil impulse
Compact movement
```

### AK

```text
Faster repeated recoil
Rifle movement
```

### Shotgun

```text
Large, heavy recoil impulse
Slower recovery
```

Avoid using the same pistol-like recoil animation for all weapons.

---

# 22. Automatic Weapon Animation

The AK should correctly handle repeated fire animation when firing automatically.

The animation should not restart in an obviously broken way every frame.

It should feel appropriate for the current rate of fire.

---

# 23. Shotgun Fire Animation

The shotgun should have significantly more visual recoil than:

* the pistol,
* the AK.

It should communicate the weapon's high close-range damage established in REQ-026.

The shotgun should feel powerful even before damage feedback is considered.

---

# 24. Weapon Weight and Inertia

Use animation values to help communicate weapon weight.

Suggested relative feel:

```text
Pistol
Lightest

AK
Medium

Shotgun
Heaviest
```

This can affect:

* sway amplitude,
* sway speed,
* sprint movement,
* recoil recovery,
* transition speed.

Do not change actual player movement speed as part of this requirement.

---

# 25. Equip / Draw Animation

Where practical, give weapons an equip animation.

When switching weapons:

```text
Old weapon exits
     ↓
New weapon enters
     ↓
New weapon ready
```

Avoid instant visual replacement if the existing weapon system allows an animation transition.

A simple prototype draw animation is acceptable.

---

# 26. Weapon-Specific Animation Parameters

Some distinctions may be created through configurable parameters rather than unique animation clips.

Expose useful values such as:

```text
Idle Sway Amount

Idle Sway Speed

Walk Bob Amount

Walk Bob Speed

Sprint Sway Amount

Sprint Sway Speed

Sprint Lowering Amount

Aim Transition Speed

Equip Transition Speed

Recoil Strength

Recoil Recovery
```

Use the project's existing animation system where possible.

---

# 27. Animation Overrides

If multiple weapons use a common Animator Controller, prefer a modular override architecture such as:

```text
Animator Override Controller
```

or an equivalent data-driven system.

Each weapon should be able to replace specific animations while retaining common state-machine logic.

For example:

```text
Shared State Machine

Idle
Walk
Sprint
Fire
Reload
Aim

      ↓

Weapon-specific animation clips
```

This is preferred over duplicating a large Animator Controller for every weapon unless duplication is necessary.

---

# 28. Future Weapon Support

Future weapons should be able to define animation behavior without rewriting the central animation controller.

Adding a weapon should ideally involve:

1. creating/importing the weapon prefab,
2. assigning the weapon configuration,
3. assigning an animation profile,
4. selecting/replacing animation clips,
5. tuning sway/recoil values.

The core player animation code should not require modification.

---

# 29. Missing Animation Fallback

If a weapon does not yet have a custom animation for a given state, provide a safe fallback.

For example:

```text
No custom idle
→ generic idle

No custom walk
→ generic walk
```

However, log a useful development warning where appropriate so missing animation assignments can be identified.

Do not allow a missing optional animation to completely break the weapon.

---

# 30. First-Person Only

REQ-027 primarily concerns the local player's first-person weapon presentation.

Do not require full third-person weapon animation synchronization as part of this ticket unless the current architecture already provides it easily.

Other players do not need to see the exact first-person animation rig.

---

# 31. Multiplayer Safety

The animation changes must not interfere with:

* player ownership,
* weapon ownership,
* firing RPCs,
* damage,
* health,
* respawning,
* weapon pickups,
* weapon switching.

The local player should only control their own first-person weapon animations.

---

# 32. Respawn Handling

When the player dies and respawns:

* weapon animation state should reset cleanly,
* no reload should remain stuck active,
* sprint state should reset,
* aim state should reset,
* the equipped weapon should return to its normal ready state.

---

# 33. Existing Systems to Preserve

REQ-027 must preserve:

* REQ-025 dynamic reticle behavior,
* REQ-025 sprint accuracy behavior,
* REQ-026 damage profiles,
* weapon pickup behavior,
* weapon switching,
* firing,
* bullseye hit detection,
* multiplayer synchronization,
* controller input,
* keyboard/mouse input.

---

# 34. Inspector / Configuration Support

Animation profiles and tuning values should be editable through:

* the Unity Inspector,
* ScriptableObjects,
* Animator Override Controllers,
* or the project's existing weapon configuration system.

Avoid requiring source-code edits to adjust:

```text
Shotgun Sprint Sway

AK Recoil

Pistol Idle Motion
```

---

# 35. Prototype Tuning Targets

The current weapons should approximately feel like:

### Pistol

```text
Compact
Quick
Controlled
Light recoil
Fast transitions
```

### AK

```text
Aggressive
Fast
Moderate weight
Noticeable running sway
Rapid firing movement
```

### Shotgun

```text
Heavy
Powerful
Large recoil
Broad sprint sway
Slower transitions
Strong visual presence
```

Exact values are expected to change after playtesting.

---

# Acceptance Criteria

REQ-027 is complete when:

* [ ] The weapon animation architecture supports weapon-specific animation profiles.
* [ ] Weapon animation behavior is configuration-driven rather than hard-coded by weapon name.
* [ ] Compatible Cowsins animation assets have been evaluated for reuse.
* [ ] The pistol has an appropriate reload animation.
* [ ] The AK has an appropriate rifle reload animation.
* [ ] The shotgun has an appropriate shotgun reload animation.
* [ ] Reload animation timing corresponds reasonably with gameplay reload timing.
* [ ] Players cannot normally fire while the reload state is active.
* [ ] Reloading does not become stuck when switching weapons.
* [ ] Each current weapon has visibly distinct idle/handling behavior.
* [ ] Sprint animation is clearly different from walking.
* [ ] The AK retains or improves its current side-to-side sprint behavior.
* [ ] The shotgun no longer simply drops substantially out of view while sprinting.
* [ ] The shotgun remains clearly visible during sprinting.
* [ ] The shotgun exhibits noticeable side-to-side movement while sprinting.
* [ ] The shotgun sprint animation feels heavier than the AK sprint animation.
* [ ] Sprint transitions are smooth.
* [ ] Post-sprint transitions are smooth.
* [ ] Sprint animation works alongside REQ-025 reticle expansion.
* [ ] The pistol firing animation feels appropriate to a handgun.
* [ ] The AK firing animation behaves appropriately during automatic fire.
* [ ] The shotgun firing animation has strong/heavy visual recoil.
* [ ] Weapon switching correctly changes animation profiles.
* [ ] Equip/draw behavior works without obvious visual popping where practical.
* [ ] Animation tuning values can be modified without editing core code.
* [ ] Missing optional animation clips fail gracefully.
* [ ] Death and respawn correctly reset weapon animation state.
* [ ] Multiplayer gameplay remains functional.
* [ ] Existing damage, reticle, health, and Bullseye systems remain functional.
* [ ] Future weapons can receive distinct animation behavior without modifying the central animation system.

---

# Out of Scope

Do not add the following as part of REQ-027 unless required by the existing architecture:

* detailed ammunition inventory,
* reserve ammunition,
* ammo pickups,
* tactical vs empty reload logic,
* shell-by-shell shotgun ammo accounting,
* weapon jamming,
* inspect weapon animations,
* melee attacks,
* third-person character reload animations,
* procedural hand IK overhaul,
* custom hand models,
* new weapon models,
* new character models,
* advanced animation blending based on terrain,
* prone animations.

These may be implemented in future requirements.

---

# Design Intent

Weapons should not feel like different models attached to the same underlying animation.

The player should be able to identify the weapon's personality through movement alone.

The intended distinction is approximately:

```text
Pistol
=
quick and controlled
```

```text
AK
=
fast and aggressive
```

```text
Shotgun
=
heavy and powerful
```

The shotgun sprint behavior is a specific priority for this requirement.

The shotgun should no longer simply disappear downward while running.

Instead, the player should visibly carry the weapon while sprinting, with substantial rhythmic lateral movement similar in concept to the AK sprint animation but adjusted to make the shotgun feel larger and heavier.

REQ-027 should establish the animation framework that allows every future Bullseye weapon to develop its own visual identity.
S