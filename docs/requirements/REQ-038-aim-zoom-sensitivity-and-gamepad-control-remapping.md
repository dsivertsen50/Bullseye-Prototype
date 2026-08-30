# REQ-038 — Aim/Zoom Sensitivity & Gamepad Control Remapping

## Summary

Improve aiming precision while the player is using Aim/Zoom and update the gamepad control scheme so that aiming uses the **Left Trigger**.

Currently, aiming/zooming with a controller is mapped to the **Right Stick Click**, while grenade throwing uses the **Left Trigger**. This requirement should change those bindings so that:

* **Left Trigger (LT)** = Aim / Zoom
* **Left Shoulder / Left Bumper (LB)** = Throw Grenade
* **Right Stick Click (R3/RS)** = No longer used for Aim / Zoom
* Existing **keyboard and mouse controls should remain unchanged**

In addition, when Aim/Zoom is active, camera/look sensitivity should be reduced so that the player can make smaller, more deliberate aiming adjustments.

---

## Goals

1. Make Aim/Zoom easier and more natural to use on a gamepad.
2. Reduce aiming sensitivity while zoomed/aiming.
3. Move Aim/Zoom to the conventional **Left Trigger** control.
4. Move grenade throwing from Left Trigger to **Left Shoulder / Left Bumper**.
5. Preserve all existing keyboard and mouse bindings.
6. Ensure the changes work correctly in multiplayer and do not interfere with existing weapon, movement, grenade, or animation systems.

---

# Functional Requirements

## 1. Gamepad Aim / Zoom Remapping

Change the gamepad binding for Aim/Zoom from:

**Current:**

* Right Stick Click / R3 / RS

to:

**New:**

* Left Trigger / LT

The player should begin Aim/Zoom behavior while the Left Trigger is held.

Releasing the Left Trigger should return the weapon and camera to their normal hip-fire state.

This should continue using the existing Aim/Zoom system rather than creating a separate aiming implementation.

Existing behavior associated with aiming should continue to function, including where applicable:

* Weapon movement toward the aimed position
* Camera FOV changes
* Reticle changes
* Weapon-specific aiming behavior
* Third-person aiming state/animation
* Networked aiming state
* Any accuracy/spread modifiers currently associated with Aim/Zoom

---

## 2. Remove Aim From Right Stick Click

The Right Stick Click should no longer activate Aim/Zoom.

Do not leave both bindings active unless another system explicitly requires the Right Stick Click for something else.

If Right Stick Click currently has no other purpose, it may remain unassigned for now.

Do not automatically assign another action to it as part of this ticket.

---

# 3. Grenade Control Remapping

Grenade throwing is currently mapped to:

* Left Trigger / LT on gamepad
* `C` on keyboard

Change the **gamepad grenade binding only** to:

* **Left Shoulder / Left Bumper / LB**

Keyboard controls should remain unchanged:

* `C` = Throw Grenade

The existing grenade system should otherwise behave exactly as it does now.

This includes:

* Grenade prefab spawning
* Throw direction
* Throw force
* Grenade physics
* Explosion behavior
* Bullseye interaction/dislodging
* Multiplayer synchronization
* Cooldowns or grenade limits if currently implemented

Only the input binding should change.

---

# 4. Reduced Aim Sensitivity

When the player is actively holding Aim/Zoom, reduce look sensitivity relative to normal hip-fire sensitivity.

This should apply to:

* Controller right-stick look input
* Mouse look input, unless the existing FPS system already provides a separate mouse ADS sensitivity mechanism

The player should be able to make substantially finer adjustments while aiming.

### Desired Behavior

Example:

Normal Look Sensitivity:

`1.0x`

Aim/Zoom Look Sensitivity:

Approximately:

`0.55x – 0.70x`

The exact default can be tuned during implementation, but begin around:

**0.65x normal sensitivity**

Example:

```text
Normal Sensitivity = 1.0
Aim Sensitivity Multiplier = 0.65

Effective Aim Sensitivity =
Normal Sensitivity × Aim Sensitivity Multiplier
```

Do **not** permanently modify the player's base sensitivity value.

Instead, apply a multiplier whenever Aim/Zoom is active.

---

# 5. Configurable Aim Sensitivity Multiplier

Expose the Aim/Zoom sensitivity multiplier as a serialized/configurable value rather than hardcoding it.

Example:

```text
Aim Sensitivity Multiplier
Default: 0.65
```

Reasonable Inspector range:

```text
0.1 – 1.0
```

This allows the value to be easily tuned later without modifying code.

If the project already has a centralized weapon, camera, or player settings configuration system, place the value there rather than introducing unnecessary duplicate configuration.

---

# 6. Smooth Sensitivity Transition

If practical with the existing camera/input architecture, avoid an abrupt or jarring sensitivity change when entering Aim/Zoom.

The sensitivity may transition over approximately the same short period used for the Aim/Zoom camera or weapon transition.

For example:

```text
Hip Fire Sensitivity
        ↓
0.1–0.2 second interpolation
        ↓
Aim Sensitivity
```

Likewise, releasing Aim/Zoom may smoothly transition back to normal sensitivity.

This is preferred but should not require replacing or significantly restructuring the current FPS camera system.

The most important requirement is that aimed sensitivity is lower and predictable.

---

# 7. Maintain Existing Mouse & Keyboard Bindings

Do not change existing mouse or keyboard controls.

Specifically:

* Existing keyboard/mouse Aim/Zoom input remains unchanged.
* `C` remains the grenade input on keyboard.
* Existing mouse shooting controls remain unchanged.
* Existing movement controls remain unchanged.

This ticket is primarily a **gamepad control remapping** plus an aiming sensitivity improvement.

---

# 8. Input System Integration

Use the project's existing Unity Input System/Input Actions architecture.

Do not create an independent polling system solely for these controls if existing Input Actions can be modified.

Update the appropriate Input Action bindings so that they conceptually resemble:

```text
Aim / Zoom
Keyboard & Mouse: Existing binding
Gamepad: Left Trigger

Throw Grenade
Keyboard: C
Gamepad: Left Shoulder
```

Remove the old gamepad bindings where appropriate.

---

# 9. Avoid Input Conflicts

Verify that Left Trigger and Left Shoulder are not simultaneously bound to conflicting gameplay actions after this change.

In particular:

### Left Trigger

Should now primarily activate:

**Aim / Zoom**

It should no longer throw grenades.

### Left Shoulder

Should now activate:

**Throw Grenade**

If Left Shoulder currently performs another gameplay action, identify the conflict and consolidate/remap it rather than allowing both actions to fire simultaneously.

Do not change unrelated controls unless necessary to resolve a direct binding conflict.

---

# 10. Multiplayer Compatibility

These changes must work correctly for all players in multiplayer.

Input itself should remain local to the owning player.

Existing networked consequences should continue to synchronize normally.

Examples:

* One player holding LT should only cause that player's character to aim.
* One player pressing LB should only throw that player's grenade.
* Aim state visible to remote players should continue updating correctly.
* Grenades should continue spawning/networking using the current authoritative architecture.
* No other player should have their aim or sensitivity affected by another player's input.

---

# 11. First-Person and Third-Person Aim State

REQ-037 introduced/improved the concept of other players visually seeing a player raise their weapon while aiming.

Changing the Aim input to Left Trigger must continue driving the same aiming state.

In other words:

```text
Player holds LT
       ↓
Local first-person Aim/Zoom activates
       ↓
Reduced sensitivity activates
       ↓
Third-person/network aim state activates
```

Do not create separate definitions of "aiming" for first-person and third-person systems.

The same logical Aim state should drive both where practical.

---

# Gamepad Control Changes

| Action            | Previous Binding  | New Binding                                    |
| ----------------- | ----------------- | ---------------------------------------------- |
| Aim / Zoom        | Right Stick Click | **Left Trigger**                               |
| Throw Grenade     | Left Trigger      | **Left Shoulder / LB**                         |
| Right Stick Click | Aim / Zoom        | **Unassigned unless already needed elsewhere** |

Keyboard and mouse controls remain unchanged.

---

# Desired Controller Feel

When playing with a controller:

### Normal movement

The player should be able to turn quickly enough to navigate and respond to nearby threats.

### Holding Left Trigger

The weapon should enter Aim/Zoom.

Camera movement should immediately feel:

* Slower
* More deliberate
* Easier to make small corrections with
* Better suited for tracking the opponent's bullseye

The reduction should be noticeable without making the camera feel sluggish.

### Pressing Left Shoulder

The player should throw a grenade using the existing grenade mechanics.

LT should never accidentally throw a grenade after this ticket is complete.

---

# Edge Cases

## Aim While Moving

The reduced sensitivity should apply normally while:

* Standing
* Walking
* Crouching
* Prone

Aim should continue to function wherever it is currently permitted.

---

## Aim While Sprinting

Maintain the current sprint-versus-aim behavior.

If the current system stops sprinting when Aim is activated, preserve that behavior.

Do not introduce the ability to Aim while full-speed sprinting unless it already exists.

---

## Aim During Jumping

Maintain current behavior.

The sensitivity multiplier should not create camera snapping or unexpected sensitivity resets when jumping or landing.

---

## Death / Respawn

If the player dies while holding Left Trigger:

* Aim should be cancelled.
* Sensitivity should return to normal.
* The player should not respawn already aimed.

---

## Pause Menu

Gameplay Aim and Grenade actions should not execute while navigating the pause menu.

Left Trigger and Left Shoulder may still be available for UI navigation if the existing menu system intentionally uses them, but gameplay actions must remain disabled while gameplay input is blocked.

---

# Suggested Implementation Structure

Reuse the existing Aim state wherever possible.

Conceptually:

```csharp
bool isAiming;

float normalSensitivity;
float aimSensitivityMultiplier = 0.65f;

float CurrentSensitivity =>
    isAiming
        ? normalSensitivity * aimSensitivityMultiplier
        : normalSensitivity;
```

Avoid maintaining separate Aim flags in several unrelated scripts if a central weapon/player aiming state already exists.

---

# Inspector / Configuration

Where appropriate, expose:

```text
Aim Settings
----------------------------
Aim Sensitivity Multiplier: 0.65
Sensitivity Transition Time: 0.15
```

The transition value may be omitted if the existing system already provides an Aim interpolation value that can be reused.

---

# Acceptance Criteria

REQ-038 is complete when all of the following are true:

1. Holding **Left Trigger** activates Aim/Zoom on a gamepad.
2. Releasing Left Trigger exits Aim/Zoom.
3. Right Stick Click no longer activates Aim/Zoom.
4. Pressing **Left Shoulder / LB** throws a grenade.
5. Left Trigger no longer throws grenades.
6. `C` still throws grenades on keyboard.
7. Existing mouse/keyboard Aim controls remain unchanged.
8. Look sensitivity is noticeably reduced while Aim/Zoom is active.
9. Aim sensitivity defaults to approximately **65% of normal sensitivity**.
10. The Aim sensitivity multiplier can be tuned without rewriting code.
11. Returning to hip fire correctly restores normal sensitivity.
12. Entering/exiting Aim does not cause camera snapping or sudden orientation changes.
13. Aim continues to drive the existing first-person weapon behavior.
14. Aim continues to drive any existing third-person/networked aiming state.
15. Grenade spawning, physics, explosions, and networking continue to operate as before.
16. Control changes work independently for each player in multiplayer.
17. Death/respawn correctly resets the player's Aim state and normal sensitivity.
18. No gameplay aiming or grenade actions fire while gameplay input is disabled by the pause menu.
19. No new Console errors or warnings are introduced.

---

# Testing Checklist

### Gamepad

* [ ] Hold LT → player aims
* [ ] Release LT → player stops aiming
* [ ] Aim sensitivity feels slower than normal look sensitivity
* [ ] Fine right-stick adjustments are easier while aiming
* [ ] Click Right Stick → does not Aim
* [ ] Press LB → grenade throws
* [ ] Press LT → grenade does not throw
* [ ] Aim while walking
* [ ] Aim while crouched
* [ ] Aim while prone
* [ ] Test sprint → aim transition
* [ ] Test aim → sprint transition
* [ ] Die while holding LT
* [ ] Confirm Aim resets after respawn

### Keyboard & Mouse

* [ ] Existing Aim input still works
* [ ] Existing Aim sensitivity reduction works appropriately
* [ ] `C` still throws grenade
* [ ] No keyboard bindings were unintentionally changed

### Multiplayer

* [ ] Host can Aim independently
* [ ] Client can Aim independently
* [ ] Host can throw grenade with LB
* [ ] Client can throw grenade with LB
* [ ] Remote players continue to see the appropriate Aim state
* [ ] One player's Aim input does not affect another player's camera
* [ ] One player's grenade input does not trigger another player's grenade

---

# Out of Scope

Do not add the following as part of REQ-038:

* Full controller rebinding UI
* User-adjustable ADS sensitivity in the Settings menu
* Separate sensitivity values for every weapon
* Aim assist
* Target snapping
* Reticle magnetism
* Controller response-curve redesign
* Dead-zone redesign
* Gyroscope aiming
* Changes to grenade damage or physics
* Changes to existing mouse/keyboard bindings

These may be addressed in later requirements.

---

# Future Consideration

Eventually, the Settings menu should probably expose separate values for:

* General Look Sensitivity
* Aim/ADS Sensitivity
* Controller Sensitivity
* Mouse Sensitivity

For REQ-038, however, a configurable Aim sensitivity multiplier in the player/camera configuration is sufficient.
