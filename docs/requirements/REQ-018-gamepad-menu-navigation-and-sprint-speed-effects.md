# REQ-018 — Gamepad Menu Navigation and Sprint Speed Effects

## Status

Ready for implementation on:

`experiment/fps-engine-integration`

## Background

REQ-017 added:

* first-person camera effects;
* world-space player health bars;
* Pause/Settings UI;
* sensitivity/settings controls.

The world-space health bars are working well.

However, two presentation/usability gaps remain:

1. The gamepad can **open** the Pause menu but cannot properly navigate or interact with it.
2. Sprinting still lacks the stronger speed presentation visible in the Cowsins FPS Engine demo, particularly the particle/speed-line effect that makes the player feel like they are moving rapidly through the environment.

This requirement should complete both systems.

---

# Goal

Implement:

1. **Full gamepad navigation and interaction for Pause/Settings UI**
2. **Cowsins-derived sprint speed-line / dust-particle visual effects**

The result should make controller play fully usable without requiring a mouse and make sprinting visually feel substantially faster.

---

# Feature 1 — Full Gamepad Pause Menu Navigation

## Current Problem

The assigned gamepad can currently open the Pause menu, but once the menu is visible the player cannot reliably:

* move selection between buttons;
* activate buttons;
* adjust sliders;
* navigate Settings;
* back out of submenus;
* resume the game.

This means keyboard/mouse is still required to operate the menu.

That is unacceptable for controller-first play.

---

# Required Controller Navigation

When Pause is open, the assigned gamepad for that local player must support:

### Navigation

* D-Pad Up / Down
* Left Stick Up / Down

to move between UI elements.

Where horizontal navigation is relevant:

* D-Pad Left / Right
* Left Stick Left / Right

should adjust or move between values.

### Confirm

Recommended:

* South button / A

should activate the currently selected button.

### Back

Recommended:

* East button / B

should:

* exit Settings back to the Pause screen;
* or close Pause if already on the root Pause screen.

### Pause / Menu

The Menu/Start button should continue toggling Pause.

---

# Device Ownership

This is critical.

Gamepad menu navigation must use the **same assigned local player's device** as gameplay.

Do not rely on:

`Gamepad.current`

as the authoritative device.

Do not allow a different connected controller to unexpectedly operate another player's menu.

Conceptually:

```text
LocalPlayerInputBinding
        ↓
Assigned Gamepad
        ↓
Gameplay Input OR Menu Input
```

When Player 1 pauses:

* Player 1's assigned controller operates Player 1's menu.

Player 2's controller should not accidentally operate Player 1's UI.

---

# UI Input Architecture

Use Unity's Input System UI support where practical.

Preferred architecture:

```text
Gameplay Action Map
        ↓
Pause pressed
        ↓
Gameplay controls blocked
        ↓
UI Action Map enabled
        ↓
EventSystem / UI Input Module
        ↓
Gamepad menu navigation
```

Cursor may adapt the current architecture if another clean solution is already partially implemented.

Do not introduce a second conflicting input stack.

---

# Initial Selection

When Pause opens:

* one valid UI control must automatically receive focus.

Preferred default:

`Resume`

The player should immediately be able to press the gamepad confirm button without first moving the mouse.

When Settings opens:

* focus should automatically move to the first Settings control.

---

# Visual Selection State

The currently selected UI control must have obvious visual feedback.

Examples:

* highlighted button;
* outline;
* brighter background;
* selected text state.

The player should always know which menu item is currently selected.

Use existing UI transition/highlight behavior where possible.

---

# Mouse + Gamepad Coexistence

Pause/Settings must continue working with mouse.

Both should coexist cleanly:

### Mouse

* cursor visible;
* buttons clickable;
* sliders draggable.

### Gamepad

* selection visible;
* navigation works;
* confirm/back works;
* sliders adjustable.

Switching between mouse and controller should not permanently break UI focus.

---

# Settings Slider Interaction

Gamepad must be able to adjust currently implemented settings such as:

* mouse sensitivity;
* controller sensitivity;
* master volume;
* any other existing slider settings.

When a slider is selected:

* Left decreases value.
* Right increases value.

Use sensible increments.

Continuous stick movement may smoothly modify values if easy to support.

---

# Settings Persistence

Existing setting persistence from REQ-017 must remain functional.

Changing a value by controller should save in the same way as changing it with the mouse.

---

# Death While Menu Is Open

Preserve REQ-017 behavior.

If a local player dies while Pause is open:

* close Pause;
* restore appropriate input state for death/respawn;
* do not leave the EventSystem or UI action map in a broken state.

After respawn:

* gameplay input should work normally.

---

# Local Multiplayer Considerations

REQ-018 must not break the current two-client testing architecture.

Test:

```text
Player 1
Controller 1
Pause UI

Player 2
Controller 2
Pause UI
```

Each local client instance should respond only to its intended controller under the current multi-window testing setup.

---

# Feature 2 — Sprint Speed Lines / Dust Particle Effect

## Goal

Add the visual sprint effect seen in the Cowsins FPS Engine demo.

When sprinting at meaningful speed, the local player should see subtle particles/speed lines moving through the camera view that create the perception of greater velocity.

This effect may resemble:

* dust streaks;
* wind particles;
* speed lines;
* small world-space particles moving rapidly past the camera.

Use the Cowsins implementation and `SpeedLinesBehaviour` as the primary reference.

---

# Desired Experience

Normal movement:

```text
walk
→ normal environment
```

Sprint:

```text
sprint starts
     ↓
camera motion increases
     +
speed/dust particles begin
     ↓
player feels significantly faster
```

Sprint ends:

```text
speed particles fade/stop
     ↓
camera returns to normal movement feel
```

The transition should be smooth rather than abruptly popping on/off where practical.

---

# Local-Only Effect

Sprint particles are first-person presentation.

They should be:

* owner-only;
* local-only;
* cosmetic;
* not networked.

Remote players do not need to see another player's speed-line particles.

---

# Trigger Conditions

The effect should activate when the local player:

* is alive;
* is actually sprinting;
* is moving above a configurable minimum speed.

Do not show the effect merely because the Sprint button is held while standing still.

Preferred logic:

```text
IsSprinting
AND
CurrentMovementSpeed >= threshold
AND
IsAlive
        ↓
Speed Effect Active
```

---

# Cowsins Reference

Inspect the Cowsins:

`SpeedLinesBehaviour`

and any associated:

* particle systems;
* VFX prefabs;
* materials;
* textures;
* movement-state conditions.

Reuse the implementation directly if it can operate cleanly without importing the complete Cowsins player dependency stack.

Otherwise:

1. copy/reuse the visual assets;
2. recreate the small activation behavior in Bullseye-owned code.

---

# Do Not Import Large Movement Dependencies Just for the Effect

If `SpeedLinesBehaviour` requires:

* Cowsins `PlayerDependencies`;
* Cowsins `PlayerMovement`;
* Cowsins `PlayerStates`;
* Cowsins static InputManager;

do not import those systems just to get sprint particles.

Instead create something similar to:

```text
BullseyeSprintSpeedEffects
        ↓
reads current Bullseye movement state
        ↓
controls Cowsins-derived ParticleSystem/VFX
```

---

# Recommended Placement

The sprint effect should be associated with the local camera/presentation hierarchy.

Conceptually:

```text
CameraRoot
└── CameraEffectsRoot
    ├── Camera
    ├── WeaponView
    └── SprintSpeedEffects
```

or another appropriate location.

The effect should move naturally with the local camera.

---

# Particle Behavior

The visual effect should suggest movement through air/dust.

Desired characteristics:

* particles appear near or ahead of the camera;
* move rapidly backward relative to view;
* do not obscure the center reticle excessively;
* remain subtle enough for aiming;
* increase perceived sprint speed.

Do not make the screen resemble heavy snowfall or a dense particle storm.

---

# Sprint Intensity

Expose useful tuning controls in Inspector.

Preferred values include:

* particle emission rate;
* particle speed;
* minimum movement speed;
* sprint intensity;
* fade-in speed;
* fade-out speed;
* particle size;
* spawn distance;
* effect opacity/intensity where supported.

---

# Optional Speed Scaling

If straightforward, scale the effect based on actual movement speed.

For example:

```text
walking
→ 0

slow sprint
→ low effect

full sprint
→ full effect
```

This is preferred but not required.

---

# HDRP Compatibility

Bullseye remains HDRP.

If Cowsins speed-line materials use Built-in Render Pipeline shaders:

* duplicate/convert only the required materials;
* use appropriate HDRP-compatible particle/VFX shaders;
* preserve the visual effect as closely as reasonable.

No pink/magenta particles are acceptable.

Do not change the project's render pipeline.

---

# Camera Effect Integration

REQ-017 already introduced camera movement effects.

The new sprint visual should complement, not replace:

* walking bob;
* sprint camera sway;
* breathing;
* landing feedback.

When sprinting, the combined experience should include:

```text
stronger movement
+
camera motion
+
speed particles
```

without becoming visually chaotic.

---

# Weapon Compatibility

Sprint effects must not interfere with:

* Ruger presentation;
* muzzle flash;
* weapon animation;
* firing;
* aiming;
* reticle visibility.

The speed particles should not render so heavily that aiming at the bullseye becomes difficult.

---

# Death / Respawn

When the local player dies:

* immediately disable sprint speed effects.

Do not continue playing particles during the respawn countdown.

After respawn:

* reset the effect;
* leave it inactive until the player actually sprints again.

---

# Pause Interaction

When the local player opens Pause:

Preferred behavior:

* stop or fade the local sprint effect.

Because the player's controls are disabled, the movement should stop and therefore the effect should naturally disappear.

Do not globally pause particle simulation if that affects other clients.

---

# Existing Systems That Must Remain Working

REQ-018 must not break:

* host/client multiplayer;
* controller assignment;
* keyboard/mouse input;
* movement;
* jumping;
* sprinting;
* crouching;
* Bullseye movement influence;
* shooting;
* weapon animations;
* muzzle VFX;
* health;
* health bars;
* regeneration;
* death;
* respawn;
* pause menu;
* settings persistence.

---

# Manual Playtest

## Test 1 — Open Pause With Controller

Use gamepad Start/Menu.

Confirm Pause opens.

Without touching the mouse:

* Resume is selected;
* D-Pad/left stick changes selection;
* A activates selection;
* B backs out.

---

## Test 2 — Settings With Controller

Open:

`Pause → Settings`

Using only controller:

* select Controller Sensitivity;
* adjust using left/right;
* adjust volume;
* return to Pause;
* resume.

Confirm values actually change.

---

## Test 3 — Mouse After Controller

Navigate menu with controller.

Then move the mouse.

Confirm:

* mouse interaction still works;
* UI does not become stuck.

Switch back to controller and confirm selection works again.

---

## Test 4 — Controller Assignment

With two gamepads / two local clients:

* Client 1 opens Pause.
* Client 2 continues playing.

Confirm Client 2's controller does not operate Client 1's menu.

Repeat in reverse.

---

## Test 5 — Death While Paused

Open Pause.

Have the other player kill the paused player.

Confirm:

* menu closes;
* death/respawn works;
* gameplay input restores after respawn.

---

## Test 6 — Sprint Effect

Stand still while holding Sprint.

Confirm no speed effect appears.

Walk normally.

Confirm little or no speed effect appears.

Sprint forward.

Confirm:

* speed particles appear;
* existing sprint camera effect remains;
* motion feels substantially faster.

Stop sprinting.

Confirm effect disappears smoothly.

---

## Test 7 — Sprint + Turning

Sprint while turning rapidly.

Confirm speed particles:

* remain visually stable;
* follow camera correctly;
* do not behave like world geometry stuck to the screen.

---

## Test 8 — Sprint + Fire

Sprint and fire the Ruger.

Confirm:

* muzzle flash remains visible;
* fire animation works;
* speed particles do not obscure the reticle excessively;
* Bullseye hits still work.

---

## Test 9 — Death During Sprint

Sprint and get killed.

Confirm:

* speed effect immediately stops;
* respawn countdown remains clean;
* speed effect does not restart after respawn until sprinting.

---

# Acceptance Criteria

## Gamepad UI

* [ ] Gamepad can open Pause.
* [ ] Gamepad can navigate Pause.
* [ ] Gamepad can activate buttons.
* [ ] Gamepad can navigate Settings.
* [ ] Gamepad can adjust sliders.
* [ ] Gamepad can return/back out.
* [ ] A sensible first UI element receives focus automatically.
* [ ] Selected control has visible highlight.
* [ ] Mouse navigation remains functional.
* [ ] Controller navigation remains tied to the proper local client/device.
* [ ] Settings changed with controller persist correctly.

## Sprint Effects

* [ ] Sprint produces visible speed-line/dust-particle presentation.
* [ ] Effect is based on Cowsins `SpeedLinesBehaviour` or equivalent Cowsins-derived assets/logic.
* [ ] Effect only appears during real sprint movement.
* [ ] Effect is owner-only.
* [ ] Effect is not networked.
* [ ] Effect works in HDRP.
* [ ] Effect does not obscure aiming excessively.
* [ ] Effect stops after sprint.
* [ ] Effect stops on death.
* [ ] Effect resets correctly after respawn.

## Existing Systems

* [ ] Health bars remain working.
* [ ] REQ-017 camera effects remain working.
* [ ] Movement remains working.
* [ ] Two-client multiplayer remains working.
* [ ] Bullseye mechanics remain working.
* [ ] REQ-014 weapon presentation remains working.
* [ ] Pause does not use `Time.timeScale = 0`.

---

# Agent Instructions

Before modifying code:

1. Read:

   * REQ-014
   * REQ-016
   * REQ-017
   * `docs/analysis/FPS_ENGINE_INTEGRATION_AUDIT.md`

2. Inspect current:

   * Pause UI;
   * EventSystem;
   * Input System UI module;
   * `LocalPlayerInputBinding`;
   * PlayerControls input asset;
   * settings UI;
   * sprint state;
   * camera effects hierarchy.

3. Inspect Cowsins:

   * `SpeedLinesBehaviour`;
   * associated particles/VFX/materials;
   * Pause UI navigation/input configuration;
   * UI Input System setup.

4. Adapt only the pieces required.

Do not solve UI navigation by replacing Bullseye's multiplayer-safe input architecture with Cowsins static input.

Do not solve sprint VFX dependencies by importing the full Cowsins player.

---

# Implementation Priority

1. Gamepad Pause navigation
2. Gamepad Settings interaction
3. Sprint speed-line effect
4. HDRP visual conversion/tuning
5. Optional velocity-scaled sprint intensity

---

# Out of Scope

REQ-018 does not implement:

* third-person weapons;
* third-person weapon animations;
* character locomotion animation;
* scoreboards;
* player names;
* teams;
* networked sprint particles;
* wall-run VFX;
* dash VFX;
* grappling VFX;
* full settings overhaul;
* complete control remapping UI;
* Cowsins global pause behavior;
* Cowsins static input architecture.

---

# Definition of Done

REQ-018 is complete when a player can use Bullseye entirely with a gamepad through gameplay and the Pause/Settings interface, and when sprinting produces the distinctive Cowsins-style sense of speed through both camera motion and speed/dust particles.

The desired result is:

```text
GAMEPAD
   ↓
Gameplay
   +
Pause / Settings
   ↓
fully usable without mouse


SPRINT
   ↓
faster movement
   +
camera sway
   +
speed/dust particles
   ↓
clear visual sensation of speed
```

These features should increase the polish of the prototype without changing Bullseye's network authority or core gameplay systems.
