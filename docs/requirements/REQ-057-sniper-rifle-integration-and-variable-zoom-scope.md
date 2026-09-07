# REQ-057 — Sniper Rifle Integration & Variable Zoom Scope

## Summary

Add the new Sniper Rifle weapon using the model located at:

```text
Assets/Weapons/Sniper Rifle/Sniper Rifle.fbx
```

The Sniper Rifle should be integrated into the existing weapon system in the same general manner as the other long guns, including:

- Pickup/equip behavior.
- First-person weapon presentation.
- Third-person/world-player weapon presentation.
- Long-gun hand/arm positioning.
- Ammunition.
- Reloading.
- Firing.
- Recoil.
- Reticle behavior.
- Multiplayer synchronization.
- REQ-055 telemetry/statistics support.

The Sniper Rifle should introduce a new long-range combat style built around a **variable-magnification scope**.

The player may freely walk, sprint, crouch, prone, and otherwise move while carrying or firing the Sniper Rifle normally.

However:

> While the Sniper Rifle is actively scoped/zoomed, horizontal player movement is disabled.

The player may still:

- Look/aim.
- Fire.
- Reload where normally permitted.
- Crouch.
- Stand.
- Go prone.
- Transition between allowed stances.

While scoped, the normal forward/back movement input is repurposed to adjust scope magnification.

---

# Goals

1. Add `Sniper Rifle.fbx` as a fully functional weapon.
2. Integrate it using the existing long-gun architecture.
3. Give it very high damage capable of instantly breaking a bullseye.
4. Add a unique sniper scope UI.
5. Add variable magnification.
6. Prevent walking while scoped.
7. Allow normal movement while not scoped.
8. Repurpose forward/back movement input to control magnification while scoped.
9. Allow stance changes while scoped.
10. Give the Sniper Rifle appropriately slow handling/fire characteristics.
11. Integrate the weapon with REQ-055 telemetry automatically.

---

# 1. Asset Location

Use the existing model:

```text
Assets/Weapons/Sniper Rifle/Sniper Rifle.fbx
```

Inspect the asset and its materials/textures before modifying import settings unnecessarily.

If the model contains multiple meshes or material slots, preserve them appropriately.

Do not move or rename the source FBX unless necessary.

---

# 2. Weapon Definition

Create a dedicated Sniper Rifle weapon definition using the existing weapon-definition architecture.

For example:

```text
SniperRifleDefinition
```

or an equivalent ScriptableObject/configuration asset.

Do not implement the Sniper Rifle by directly modifying the DMR definition.

The Sniper Rifle must have its own settings for:

- Damage.
- Fire rate.
- Magazine size.
- Reserve ammunition.
- Reload time.
- Recoil.
- Hip-fire spread.
- ADS/scoped spread.
- Zoom.
- ADS sensitivity.
- Reticle.
- Audio.
- Telemetry weapon ID.

Reuse the existing generic firearm code wherever possible.

---

# 3. Weapon Classification

Treat the Sniper Rifle as a:

```text
Long Gun
```

for purposes including:

- Third-person weapon holding.
- Hand placement.
- Arm positioning.
- Weapon attachment.
- Weapon animation logic.
- Weapon switching.

It should use the long-gun holding architecture introduced for weapons such as:

- Rifle.
- DMR.
- Shotgun.

Do not create an entirely separate character-animation system solely for the Sniper Rifle.

---

# 4. First-Person Weapon Integration

The Sniper Rifle should appear correctly in the local player's first-person view.

Requirements:

- Correct scale.
- Correct rotation.
- Correct position.
- Weapon points forward accurately.
- Muzzle position aligns with firing logic.
- Weapon does not clip excessively through the camera.
- Reload/fire/recoil behavior works with the existing FPS weapon architecture.

Initial positioning may require tuning in the Unity Inspector.

Expose appropriate positioning values rather than hardcoding them where possible.

---

# 5. Third-Person / World Player Integration

Other players must see the Sniper Rifle correctly positioned in the wielder's hands.

It should use the existing long-gun hand/arm positioning system.

Requirements:

- Right hand appropriately controls the grip/trigger area.
- Left hand supports the rifle farther forward.
- Weapon remains aligned with the player's upper body.
- Weapon follows aiming direction appropriately.
- Crouch/prone/standing states do not cause major weapon detachment or floating.
- ADS/scoped posture should remain visually believable.

Do not return to the old method of manually animating the entire arms specifically around one weapon if the newer long-gun procedural/IK system is available.

---

# 6. Normal Movement Behavior

When the player is **not scoped**, the Sniper Rifle should not impose unusual movement restrictions.

The player can:

```text
Walk
Sprint
Jump
Crouch
Prone
Dolphin Dive where otherwise permitted
Wall Run where otherwise permitted
```

according to the game's normal movement rules.

The player may also fire the Sniper Rifle without using the scope.

Example:

```text
Player is walking
    ↓
Player fires Sniper Rifle from hip/unscoped
    ↓
Shot fires normally
```

Do not require the player to stop moving before firing.

---

# 7. Scoped Movement Restriction

The movement restriction applies **only while actively scoped**.

When:

```text
Sniper Rifle equipped
+
ADS/Zoom held
```

the player should no longer be able to translate across the ground through ordinary walking input.

Block:

```text
Forward walking
Backward walking
Strafing left
Strafing right
Sprint movement
```

The player should remain at their current horizontal position.

---

# 8. Scoped Stance Changes

Being scoped must **not** completely freeze the character.

While scoped, allow the player to:

```text
Stand → Crouch
Crouch → Stand
Crouch → Prone
Prone → Crouch/Stand as currently supported
```

These stance transitions should continue to use existing movement rules.

The player's camera height and weapon position should adapt correctly.

---

# 9. Scoped Looking

The player must retain full aiming/look control while scoped.

Allow:

```text
Mouse movement
Right analog stick
```

to rotate the view normally.

Only translational walking movement is disabled.

Do not freeze camera rotation.

---

# 10. Leaving the Scope Restores Movement

As soon as ADS/Zoom is released:

```text
Scoped = false
```

normal movement controls should immediately return.

Example:

```text
Hold Left Trigger
    ↓
Scoped
    ↓
Movement locked

Release Left Trigger
    ↓
Scope closes
    ↓
Normal walking immediately restored
```

There should be no lingering movement lock.

---

# 11. Scope Activation

Use the existing ADS/Zoom input:

## Controller

```text
Left Trigger
```

## Mouse and Keyboard

Use the project's existing ADS/zoom mouse binding.

Do not create a separate sniper-only ADS button.

---

# 12. Sniper Scope UI

The Sniper Rifle must have its own scope presentation.

It should not simply reuse the DMR scope overlay.

Create a sniper-style optic that visually communicates significantly greater magnification.

Possible elements include:

```text
Circular scope view
Dark/black peripheral mask
Central crosshair
Fine horizontal/vertical reticle lines
Optional range marks
```

The exact artwork can remain prototype quality.

The scope should clearly feel distinct from the DMR optic.

---

# 13. Scope Peripheral Mask

While scoped, strongly reduce or block peripheral vision outside the optic.

A traditional approach is acceptable:

```text
Black screen area
+
Circular visible scope region
```

or another clean implementation consistent with the existing rendering architecture.

Avoid showing the normal hip-fire HUD reticle simultaneously with the scope reticle.

---

# 14. Variable Magnification

Unlike the DMR, the Sniper Rifle should support **variable zoom**.

Initial target magnification range:

```text
Minimum: approximately 2x
Maximum: approximately 6x
```

These should be configurable values.

Do not hardcode the system specifically to exactly `2x` and `6x`.

Example configuration:

```csharp
minZoom = 2f;
maxZoom = 6f;
zoomAdjustmentSpeed = ...
```

Cursor should implement the zoom using the project's existing camera/FOV architecture rather than literally multiplying a camera image.

---

# 15. Initial Zoom Level

When the player first scopes in, begin at the minimum sniper magnification:

```text
~2x
```

or the configured default sniper zoom level.

Do not automatically enter at maximum magnification.

---

# 16. Movement Input Becomes Zoom Control While Scoped

Because ordinary movement is intentionally disabled while scoped, repurpose the **forward/back movement axis** to control magnification.

## Mouse and Keyboard

While scoped:

```text
W = Increase Magnification
S = Decrease Magnification
```

## Controller

While scoped:

```text
Left Stick Forward = Increase Magnification
Left Stick Backward = Decrease Magnification
```

This replaces forward/back walking only during Sniper Rifle scope mode.

---

# 17. Left/Right Movement While Scoped

While scoped:

```text
A / D
```

and:

```text
Left Stick Left / Right
```

should not move the character.

For REQ-057, they do not need to perform another action.

They may simply have no locomotion effect while scoped.

---

# 18. Continuous Zoom Adjustment

Magnification should change smoothly rather than only switching between two discrete values.

Example:

```text
2.0x
2.1x
2.2x
...
5.9x
6.0x
```

Exact increments do not need to be displayed or literal.

The perceived result should be smooth variable magnification.

---

# 19. Zoom Input Deadzone

Controller zoom adjustment must respect an appropriate analog-stick deadzone.

A slightly imperfectly centered stick must not continuously change magnification.

Use the existing Input System deadzone configuration where possible.

---

# 20. Zoom Adjustment Speed

Magnification changes should be deliberate enough for the player to select a useful zoom level.

It should not jump from:

```text
2x → 6x
```

almost instantly.

Expose a configurable zoom-adjustment speed.

Tune this during implementation.

---

# 21. Zoom Limits

Zoom must clamp cleanly between:

```text
Minimum Magnification
Maximum Magnification
```

Holding forward at maximum zoom should do nothing further.

Holding backward at minimum zoom should do nothing further.

No FOV values should become invalid.

---

# 22. Preserve Selected Zoom During Current Scope Session

If the player adjusts from:

```text
2x → 4.3x
```

that magnification should remain stable when movement input returns to neutral.

It should not drift back toward minimum zoom automatically.

---

# 23. Zoom Reset Behavior

When the player completely exits the scope, reset magnification to the configured default/minimum value for the next scope activation.

Example:

```text
Scope in
2x

Adjust to 5x

Scope out

Scope in again
2x
```

This is the preferred initial behavior for predictability.

Keep the implementation flexible enough that preserving the previous zoom level could be added later if desired.

---

# 24. Sensitivity Scaling

Scoped aiming sensitivity should be significantly lower than normal hip-fire sensitivity.

It should also account for magnification.

As magnification increases:

```text
Look sensitivity should decrease appropriately.
```

Example principle:

```text
2x = reduced sensitivity
6x = substantially reduced sensitivity
```

Do not make a 6x scope move across the environment at the same angular-feeling speed as hip fire.

Use a smooth and configurable sensitivity calculation.

---

# 25. Accuracy

The Sniper Rifle should be extremely accurate while scoped.

When properly scoped:

```text
Shot deviation/spread ≈ 0
```

or very close to zero.

The bullet/hitscan should travel precisely to the aiming point.

---

# 26. Hip-Fire Accuracy

The Sniper Rifle may still be fired without scoping.

However, hip firing should be considerably less reliable than scoped fire.

Use configurable hip-fire spread.

This preserves the ability to make emergency close-range shots without making hip-fire sniping optimal.

---

# 27. Sniper Damage

The Sniper Rifle should have extremely high damage.

A confirmed Sniper Rifle hit on a valid bullseye should immediately break that bullseye regardless of its current health.

Conceptually:

```text
Full-health Bullseye
+
Sniper Hit
=
Immediate Elimination
```

This applies to:

```text
Attached Bullseyes
Detached Bullseyes
```

Do not require multiple sniper shots to destroy a full-health bullseye.

---

# 28. Preserve Bullseye Core Gameplay Rules

The Sniper Rifle's high damage should operate through the existing valid bullseye/damage architecture.

Do not create a completely separate player-death pathway just for the Sniper Rifle.

Preferred flow:

```text
Sniper shot confirmed
    ↓
Valid bullseye hit
    ↓
Apply sufficiently high damage
    ↓
Bullseye breaks
    ↓
Existing elimination system executes
```

This allows:

- Death handling.
- Respawning.
- Bullseye shatter behavior.
- Assists.
- REQ-055 telemetry.

to continue functioning normally.

---

# 29. Detached Bullseye Interaction

A detached physical bullseye must also be instantly destroyable by the Sniper Rifle.

Example:

```text
Magnetism Grenade detaches bullseye
    ↓
Bullseye lands 40 meters away
    ↓
Sniper shoots detached bullseye
    ↓
Immediate elimination
```

REQ-055 should record:

```text
Weapon = Sniper Rifle
Bullseye State = Detached
Elimination Distance = actual sniper-to-bullseye distance
```

---

# 30. Fire Mode

The Sniper Rifle should fire one shot per trigger pull.

Use:

```text
Semi-Automatic
```

input behavior.

Holding the trigger should not continuously fire rounds.

Each shot requires a distinct trigger activation.

---

# 31. Fire Rate

Use a deliberately slow fire cadence.

The Sniper Rifle should not behave like the DMR with higher damage.

After firing:

```text
Shot
    ↓
Meaningful recovery period
    ↓
Next shot allowed
```

A prototype fire interval around:

```text
~1.0–1.5 seconds
```

is reasonable.

Expose this as configuration rather than hardcoding it.

---

# 32. Bolt-Action Feel

The current model does not necessarily need a functioning animated bolt in REQ-057.

However, firing cadence and recoil should create a **bolt-action-like rhythm**.

Future animation/audio work may add:

```text
Bolt cycling
Shell ejection
Hand animation
```

Do not block functional implementation on those animations.

---

# 33. Magazine Size

Start with a small magazine appropriate for the weapon.

Recommended prototype:

```text
Magazine Capacity: 5 rounds
```

Keep this configurable in the weapon definition.

---

# 34. Reserve Ammunition

Use finite reserve ammunition, consistent with other non-pistol weapons.

Choose an initial prototype reserve value appropriate for testing.

For example:

```text
Magazine: 5
Reserve: 15
```

Exact balancing can be tuned later.

---

# 35. Reloading

Integrate with the existing reload system.

Requirements:

- Cannot fire during the prohibited portion of reload.
- Correct magazine/reserve values.
- Existing reload input works.
- Existing HUD ammunition display works.
- Multiplayer representation remains correct.

If no Sniper-specific reload animation exists, use the most appropriate existing long-gun placeholder animation.

---

# 36. Recoil

The Sniper Rifle should have strong recoil.

Compared with:

```text
Pistol
Rifle
DMR
```

the Sniper Rifle should produce noticeably greater recoil.

This should include appropriate:

```text
Camera recoil
Weapon visual recoil
Third-person recoil response where supported
```

Do not reproduce the previous arm-flailing recoil problem.

Use the newer recoil/weapon positioning architecture.

---

# 37. Scoped Recoil

Firing while scoped should strongly disrupt the sight picture.

The scope should kick upward or otherwise visibly react.

However:

- Do not automatically un-scope the player after every shot.
- Allow the player to recover the optic naturally unless future tuning determines otherwise.

---

# 38. Movement During Firing

Remember:

> The player only loses walking movement while scoped.

Therefore this is valid:

```text
Player running/walking
    ↓
Sniper not scoped
    ↓
Player fires
```

The shot should occur.

Do not accidentally create logic such as:

```csharp
if (isMoving)
    cannotFireSniper = true;
```

That is NOT the desired behavior.

---

# 39. Stance + Scope

The following should all be valid:

```text
Standing + Scoped
Crouching + Scoped
Prone + Scoped
```

When transitioning stance while scoped:

- Scope should remain usable where practical.
- Camera should transition to the new stance height.
- Movement remains locked.
- Aim remains functional.

---

# 40. Jumping While Scoped

Do not allow scoped mode to create strange midair movement locks.

Preferred behavior:

If the player becomes airborne through an existing movement mechanic, either:

```text
Automatically exit sniper scope
```

or prevent entering full scope while airborne.

Use whichever integrates more safely with the current movement controller.

The important requirement is to avoid a state where:

```text
airborne + scoped + locomotion locked
```

breaks movement physics.

---

# 41. Sprinting and Scope Activation

If the player is sprinting and presses ADS:

```text
Stop sprint locomotion
Enter scoped state
```

once the existing ADS transition permits it.

The player should not remain sliding forward indefinitely because their movement was disabled while sprint velocity was still active.

Any normal ground velocity associated with player-controlled walking/sprinting should settle appropriately.

---

# 42. Scope Exit and Sprint

After releasing ADS:

```text
Normal locomotion returns
```

The player may resume sprinting according to the normal sprint input/state.

---

# 43. HUD Reticle

When unscoped:

Use an appropriate Sniper Rifle hip-fire reticle.

When scoped:

Hide the normal HUD reticle and show only the sniper optic/reticle.

Avoid displaying:

```text
Hip-fire crosshair
+
Sniper crosshair
```

simultaneously.

---

# 44. Zoom Feedback

If straightforward, include a small scope magnification indicator.

Example:

```text
2.0x
```

or:

```text
4.5x
```

This is recommended but not mandatory for initial functionality.

If included, keep it subtle.

---

# 45. Scope Transition

Scope entry/exit should not be an instantaneous visual pop if the existing ADS architecture supports interpolation.

Use a short transition:

```text
Normal FOV
    ↓
Smooth ADS transition
    ↓
Minimum sniper zoom
```

Likewise when exiting.

Avoid excessively long animations that make the weapon frustrating to use.

---

# 46. Audio

Integrate appropriate weapon audio hooks:

```text
Fire
Reload
Empty
Weapon equip/pickup
```

If final Sniper Rifle audio assets do not yet exist, placeholders may be used.

Do not reuse an obviously inappropriate quiet pistol firing sound as the intended final Sniper sound.

---

# 47. Pickup Integration

Integrate the Sniper Rifle into the existing weapon pickup system.

The player should be able to:

```text
Approach Sniper Rifle
Pick it up
Equip it
Swap according to current two-weapon rules
Drop it where existing weapon logic requires
```

Do not create a Sniper-specific inventory architecture.

---

# 48. Death / Dropping

The Sniper Rifle should follow the same secondary/non-default weapon death behavior defined by the current weapon system.

If the current rule causes finite-ammo secondary weapons to drop on death:

```text
Sniper Rifle should follow that rule.
```

---

# 49. Networking

The Sniper Rifle must function correctly in multiplayer.

Synchronize the same aspects currently synchronized for other weapons, including where applicable:

```text
Weapon equipped
Weapon firing
Muzzle effects
Ammo state
Reload
Third-person weapon visibility
Damage
Elimination
```

Scope UI and local camera FOV are local-client presentation state.

Do not network the actual scope overlay.

---

# 50. Authoritative Damage

Sniper damage must use the same authoritative damage validation used by existing weapons.

A client should not independently declare:

```text
"I hit someone with the sniper, therefore they die."
```

Instead:

```text
Shot
    ↓
Authoritative hit validation
    ↓
Valid bullseye hit
    ↓
High damage applied
    ↓
Elimination
```

---

# 51. REQ-055 Telemetry Integration

The Sniper Rifle must automatically participate in the telemetry foundation from REQ-055.

Record:

```text
Weapon = Sniper Rifle
```

for Sniper eliminations.

The system should support:

```text
Sniper Rifle Eliminations
Attached Bullseye Sniper Eliminations
Detached Bullseye Sniper Eliminations
Sniper Elimination Distance
Shots Fired
Bullseye Hits
Assists
```

Do not create a disconnected `SniperKillCounter`.

Use the generic telemetry system.

---

# 52. Elimination Distance

Sniper eliminations should preserve the exact elimination distance through REQ-055.

This is especially important for this weapon.

Future statistics should be able to identify values such as:

```text
Longest Sniper Elimination
Average Sniper Elimination Distance
```

REQ-057 does not need to create the lifetime statistics UI.

---

# 53. Assist Compatibility

Sniper damage should remain compatible with the assist system.

In most cases a Sniper bullseye hit will immediately eliminate the target.

If other players have valid unresolved damage contributions:

```text
Sniper shooter = Elimination
Previous qualifying contributors = Assists
```

---

# 54. Scoreboard Compatibility

REQ-056's scoreboard should require no special Sniper Rifle code.

A Sniper elimination simply increases:

```text
Eliminations
```

normally.

Any detailed weapon statistics remain telemetry data for future screens.

---

# 55. Configuration

Expose important Sniper values in a logical configuration location.

At minimum:

```text
Damage
Magazine Capacity
Reserve Ammo
Fire Interval
Reload Time
Hip-Fire Spread
Scoped Spread
Minimum Zoom
Maximum Zoom
Default Zoom
Zoom Adjustment Speed
Scoped Sensitivity
Recoil
```

Avoid burying major balance values across unrelated scripts.

---

# 56. Do Not Duplicate DMR Logic

The DMR and Sniper Rifle both use zoom, but their behaviors differ.

The shared system should support weapon-specific settings.

DMR:

```text
Normal movement while ADS
Relatively low magnification
Existing DMR scope
```

Sniper:

```text
No walking while scoped
Variable magnification
Dedicated sniper scope
Very high bullseye damage
```

Do not modify DMR behavior to match the Sniper.

---

# 57. Testing Requirements

## Test A — Pickup

1. Enter a match.
2. Pick up the Sniper Rifle.

Expected:

```text
Weapon equips through the normal weapon system.
```

---

## Test B — First-Person Position

Equip Sniper Rifle.

Expected:

- Correct orientation.
- Correct scale.
- No extreme clipping.
- Muzzle approximately aligns with shot direction.

---

## Test C — Third-Person Position

Observe another player holding the Sniper Rifle.

Expected:

```text
Sniper appears as a correctly positioned long gun.
```

---

## Test D — Walking Unscoped

1. Equip Sniper.
2. Do not ADS.
3. Walk forward/back/left/right.

Expected:

```text
Normal movement.
```

---

## Test E — Fire While Walking

1. Walk forward.
2. Fire Sniper without scoping.

Expected:

```text
Weapon fires successfully while player continues moving.
```

This is a critical requirement.

---

## Test F — Scope Entry

Hold ADS.

Expected:

```text
Sniper scope appears.
Camera magnifies.
Movement lock activates.
```

---

## Test G — Scoped Forward Input

While scoped, press:

```text
W
```

or push:

```text
Left Stick Forward
```

Expected:

```text
Player does NOT walk forward.
Magnification increases.
```

---

## Test H — Scoped Backward Input

While scoped, press:

```text
S
```

or push:

```text
Left Stick Backward
```

Expected:

```text
Player does NOT walk backward.
Magnification decreases.
```

---

## Test I — Scoped Strafing

While scoped, press:

```text
A / D
```

or push the left stick sideways.

Expected:

```text
Player does not move laterally.
```

---

## Test J — Scoped Look

While scoped, move:

```text
Mouse
Right Stick
```

Expected:

```text
Player can freely aim the scope.
```

---

## Test K — Scoped Crouch

While scoped:

```text
Stand → Crouch
```

Expected:

```text
Stance changes.
Scope remains functional.
Player still cannot walk.
```

---

## Test L — Scoped Prone

While scoped, transition to prone.

Expected:

```text
Player enters prone.
Camera updates.
Scope remains functional.
Walking remains disabled.
```

---

## Test M — Movement Restoration

1. Scope in.
2. Confirm walking is disabled.
3. Release ADS.
4. Immediately move.

Expected:

```text
Normal movement returns immediately.
```

---

## Test N — Zoom Minimum

Scope in.

Hold zoom-decrease input.

Expected:

```text
Magnification stops at minimum value.
No invalid FOV.
```

---

## Test O — Zoom Maximum

Scope in.

Hold zoom-increase input.

Expected:

```text
Magnification stops at maximum value.
No invalid FOV.
```

---

## Test P — Zoom Persistence During Scope

1. Scope in.
2. Increase magnification.
3. Release movement stick/key to neutral.

Expected:

```text
Selected magnification remains stable.
```

---

## Test Q — Zoom Reset

1. Scope in.
2. Increase to near maximum.
3. Scope out.
4. Scope back in.

Expected:

```text
Scope begins at configured default/minimum magnification.
```

---

## Test R — Scoped Sensitivity

Compare aiming at minimum vs maximum zoom.

Expected:

```text
Higher magnification provides appropriately reduced aiming sensitivity.
```

---

## Test S — Bullseye One-Shot

1. Find a full-health enemy bullseye.
2. Shoot it once with Sniper Rifle.

Expected:

```text
Bullseye immediately breaks.
Enemy is eliminated.
```

---

## Test T — Detached Bullseye One-Shot

1. Detach an enemy bullseye.
2. Shoot it once with Sniper Rifle.

Expected:

```text
Detached bullseye immediately breaks.
Enemy is eliminated.
```

---

## Test U — Miss

Fire Sniper near a player without hitting the valid target.

Expected:

```text
No elimination.
```

High damage must not mean automatic elimination merely because the trigger was pulled.

---

## Test V — Hip-Fire Spread

Fire multiple unscoped shots.

Expected:

```text
Hip-fire is noticeably less precise than scoped firing.
```

---

## Test W — Scoped Accuracy

Fire accurately through the scope.

Expected:

```text
Shot aligns extremely closely with scope center.
```

---

## Test X — Fire Cadence

Rapidly click fire.

Expected:

```text
Weapon cannot exceed configured slow Sniper fire rate.
```

---

## Test Y — Magazine

Fire until magazine is empty.

Expected:

```text
Correct magazine count.
Cannot fire additional live round until reload.
```

---

## Test Z — Telemetry

Earn a Sniper elimination.

Expected REQ-055 telemetry includes:

```text
Weapon = Sniper Rifle
Elimination +1
Correct elimination distance
Correct attached/detached bullseye state
Shots Fired updated
Bullseye Hits updated
```

---

# 58. Acceptance Criteria

REQ-057 is complete when:

- [ ] `Assets/Weapons/Sniper Rifle/Sniper Rifle.fbx` is integrated as a usable weapon.
- [ ] Sniper Rifle has its own weapon definition/configuration.
- [ ] It uses existing long-gun positioning architecture.
- [ ] First-person positioning is functional.
- [ ] Third-person positioning is functional.
- [ ] Weapon can be fired while standing still.
- [ ] Weapon can be fired while walking/moving when unscoped.
- [ ] Normal movement is unaffected while the Sniper Rifle is not scoped.
- [ ] ADS activates a dedicated Sniper scope.
- [ ] Walking is disabled only while actively scoped.
- [ ] Forward/back input adjusts zoom while scoped.
- [ ] Forward/back input does not move the player while scoped.
- [ ] Left/right movement does not move the player while scoped.
- [ ] Camera look remains functional while scoped.
- [ ] Crouching remains functional while scoped.
- [ ] Prone remains functional while scoped.
- [ ] Releasing ADS restores normal movement immediately.
- [ ] Scope supports smooth variable magnification.
- [ ] Minimum and maximum zoom values are configurable.
- [ ] Initial prototype zoom range is approximately 2x–6x.
- [ ] Magnification clamps correctly.
- [ ] Controller deadzone prevents unwanted zoom drift.
- [ ] Higher zoom appropriately reduces aiming sensitivity.
- [ ] Scoped accuracy is extremely high.
- [ ] Hip-fire remains possible but is less accurate.
- [ ] One valid Sniper hit destroys a full-health bullseye.
- [ ] Detached bullseyes are also one-shot.
- [ ] Weapon fires semi-automatically, one round per trigger pull.
- [ ] Weapon has a deliberately slow fire cadence.
- [ ] Magazine/reserve ammunition work.
- [ ] Reloading works.
- [ ] Strong recoil is implemented without destabilizing third-person arms.
- [ ] Multiplayer firing and damage remain authoritative.
- [ ] Sniper Rifle participates in REQ-055 telemetry.
- [ ] Sniper elimination distance is recorded.
- [ ] REQ-056 scoreboard receives Sniper eliminations through the normal elimination statistic.
- [ ] DMR zoom/movement behavior remains unchanged.

---

# 59. Implementation Guidance for Cursor

Before changing code:

1. Inspect how the Pistol, Rifle, Shotgun, and DMR are currently defined.
2. Identify the generic weapon definition architecture.
3. Inspect the two-weapon pickup/equip system.
4. Inspect current long-gun third-person hand positioning.
5. Inspect the DMR ADS/zoom implementation.
6. Identify which parts of DMR zoom should become reusable rather than copied.
7. Inspect the player locomotion controller.
8. Identify the cleanest point to suppress horizontal locomotion while Sniper scope mode is active.
9. Make sure stance transitions remain separate from horizontal locomotion.
10. Inspect current Input System movement vectors.
11. Reuse the movement Y-axis for Sniper zoom only while scoped.
12. Inspect firearm damage and bullseye-break handling.
13. Configure Sniper damage through existing damage logic rather than creating a parallel elimination path.
14. Integrate the Sniper weapon ID with REQ-055 telemetry.
15. Test existing DMR behavior afterward to ensure the new variable-scope system did not regress it.

Prefer extending generic systems over creating:

```text
SniperOnlyMovementController
SniperOnlyDamageSystem
SniperKillTracker
SniperNetworkManager
```

unless there is a compelling architectural reason.

---

# Final Expected Result

The Sniper Rifle should create a distinct long-range combat mode.

A player can carry the weapon and move normally:

```text
Run
Walk
Crouch
Prone
Fire from the hip
```

just like they can with another long gun.

However, when they hold ADS:

```text
Sniper scope opens
    ↓
Walking movement stops
    ↓
W / Left Stick Forward increases magnification
S / Left Stick Back decreases magnification
    ↓
Player remains able to aim, fire, crouch, stand, or go prone
```

The player may smoothly adjust the optic from approximately:

```text
2x → 6x
```

and use the rifle for deliberate long-distance shooting.

Releasing ADS immediately restores normal movement.

A valid Sniper Rifle hit against an attached or detached bullseye delivers enough damage to destroy it immediately, making the Sniper Rifle exceptionally lethal while its scoped immobility, slow fire rate, low ammunition capacity, recoil, and reduced close-range hip-fire accuracy provide meaningful tradeoffs.