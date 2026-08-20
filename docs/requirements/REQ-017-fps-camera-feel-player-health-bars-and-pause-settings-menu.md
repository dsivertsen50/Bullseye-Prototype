# REQ-017 — FPS Camera Feel, Player Health Bars, and Pause/Settings Menu

## Status

Ready for implementation on:

`experiment/fps-engine-integration`

## Background

REQ-016 adopted portions of the Cowsins FPS Engine movement/control architecture into Bullseye.

The revised movement is functional and jumping feels improved, but the game still lacks much of the first-person presentation polish visible in the FPS Engine demo.

In particular, Bullseye currently lacks:

* subtle idle/breathing camera motion;
* movement-dependent camera sway;
* sprint camera motion;
* landing camera feedback;
* polished first-person movement feel;
* world-space health indicators above players;
* a proper pause/settings interface;
* convenient in-game adjustment of sensitivity and related controls.

The Cowsins FPS Engine contains mature implementations and presentation patterns for many of these systems.

This requirement should adopt/adapt those systems where practical while preserving Bullseye's multiplayer architecture and game-specific systems.

---

# Goal

Improve the overall FPS presentation and usability of Bullseye by implementing three major features:

1. **Cowsins-derived first-person camera effects**
2. **World-space health indicators above player bodies**
3. **A multiplayer-safe pause/settings menu inspired by or adapted from Cowsins**

The primary objective is for Bullseye to begin feeling substantially closer to a polished FPS rather than a functional Unity prototype.

---

# Feature 1 — First-Person Camera Feel

## Goal

Adopt or reproduce the camera movement visible in the Cowsins FPS Engine demo.

The local player's camera should have subtle motion based on the player's current state.

This should include at minimum:

* idle breathing motion;
* walking camera motion;
* more pronounced sprint motion;
* landing feedback.

Additional Cowsins camera effects may be incorporated where safe.

---

# Camera Ownership

All camera effects must be:

* local-only;
* owner-only;
* cosmetic;
* additive to gameplay look;
* independent of network transform authority.

Remote players must not run first-person camera effects.

Do not network:

* camera bob;
* breathing;
* camera shake;
* first-person weapon sway;
* first-person FOV effects.

These are local presentation effects only.

---

# Preserve Gameplay Look

The player must retain full responsive control over:

* yaw;
* pitch;
* aiming;
* shooting.

Camera effects must not take over or fight the player's core look controller.

The desired architecture is conceptually:

```text
Player Look Input
       ↓
Gameplay Look
       ↓
Base Camera Orientation
       ↓
Camera Effects Layer
       ├── breathing
       ├── walking bob
       ├── sprint sway
       ├── landing motion
       └── future recoil/shake
       ↓
Final Camera Pose
```

Avoid multiple independent scripts writing arbitrary values directly to the same camera transform without coordination.

---

# Recommended Camera Hierarchy

Cursor may restructure the camera hierarchy if necessary.

Preferred conceptual structure:

```text
Player
└── CameraRoot
    ├── LookRoot
    │   └── CameraEffectsRoot
    │       └── Camera
    │           └── WeaponView
```

or equivalent.

Responsibilities should be separable:

* player/body yaw;
* camera pitch;
* procedural camera effects;
* weapon presentation.

The exact hierarchy may differ if another design is cleaner.

---

# Idle / Breathing Effect

When the player is:

* alive;
* grounded;
* not moving substantially;

the camera should exhibit a very subtle breathing/idle motion.

Desired character:

* subtle;
* slow;
* non-distracting;
* primarily cosmetic;
* easily configurable.

Possible motion:

* slight vertical oscillation;
* slight lateral movement;
* extremely small rotational motion.

This should resemble the Cowsins FPS Engine feel rather than exaggerated head bob.

Expose tuning values in the Inspector.

---

# Walking Camera Motion

When walking:

* camera should respond rhythmically to movement;
* motion intensity should scale with player speed;
* transition into/out of walking should be smooth.

Avoid abrupt snapping between:

`idle → walking → idle`

Camera motion should not significantly interfere with aiming.

---

# Sprint Camera Motion

Sprint should have noticeably more motion than walking.

Use the Cowsins demo as the primary feel reference.

Potential effects include:

* increased lateral sway;
* increased vertical bob;
* slight camera roll;
* subtle forward/back motion;
* optional FOV increase.

If FOV is modified while sprinting:

* there must be one clear FOV authority;
* sprint FOV must cooperate with aiming;
* ADS must override or correctly blend with sprint FOV.

Do not introduce multiple scripts independently fighting over `Camera.fieldOfView`.

---

# Landing Camera Effect

When the player lands after a meaningful jump/fall:

* briefly move or tilt the camera to communicate impact;
* return smoothly to neutral.

The effect should scale reasonably with landing intensity if straightforward.

Do not add gameplay fall damage in this requirement.

---

# Optional Camera Effects

If they can be adopted safely without significantly expanding scope, Cursor may also include:

* subtle acceleration/deceleration camera response;
* crouch camera easing;
* light sprint tilt;
* small jump departure motion.

Do not implement:

* extreme camera shake;
* wall-run camera behavior;
* dash camera behavior;
* grappling effects.

unless those movement systems already exist.

---

# Cowsins Camera Code

Inspect and reuse/adapt relevant Cowsins implementations, including where appropriate:

* `CameraEffects`
* `WeaponEffects`
* relevant movement camera behavior;
* head bob logic;
* breathing logic;
* jump/landing effects.

Do not import large dependency chains solely to gain camera motion.

If Cowsins camera effects depend heavily on:

* Cowsins `PlayerDependencies`;
* Cowsins `PlayerStats`;
* Cowsins `InputManager`;
* Cowsins pause state;
* Cowsins Rigidbody assumptions that no longer match Bullseye;

extract/reproduce the useful presentation logic in Bullseye-owned code.

Prefer:

```text
Cowsins feel/logic
        ↓
small Bullseye camera component
```

over:

```text
full Cowsins player dependency stack
```

---

# Camera Settings

Expose useful tuning values in the Inspector.

At minimum:

### Idle

* breathing amplitude;
* breathing frequency.

### Walking

* bob amplitude;
* bob frequency.

### Sprinting

* sway amplitude;
* sway frequency;
* optional roll amount;
* optional FOV increase.

### Landing

* landing intensity;
* recovery speed.

Avoid hardcoding feel values throughout multiple scripts.

---

# Feature 2 — World-Space Player Health Indicator

## Goal

Display player health above each player body in the world, similar to the health indicators visible over targets in the FPS Engine demo.

This is separate from the existing local bottom-left health HUD.

The existing local health display should remain.

---

# Health Indicator Behavior

Each player should have a world-space health indicator located approximately above the player's head/body.

Conceptually:

```text
Player
├── Body
├── Bullseye
└── WorldHealthUI
      └── HealthBar
```

The health indicator should show the player's current health relative to maximum health.

Current default health:

`8`

Examples:

```text
8 / 8 = full bar
6 / 8 = 75%
4 / 8 = 50%
2 / 8 = 25%
0 / 8 = dead
```

---

# Health Data Source

The world health display must read from the existing authoritative:

`PlayerHealth`

Do not create a second health state.

Do not use Cowsins:

`PlayerStats`

Health displayed above a player must reflect the synchronized NGO health state.

---

# World-Space UI

Preferred implementation:

* Unity World Space Canvas;
* simple health bar;
* positioned above the player;
* follows the player automatically.

The bar should face the local player's camera.

Possible implementation:

```text
WorldHealthBar
    ↓
LateUpdate
    ↓
rotate toward local camera
```

or another billboard implementation.

---

# Visibility

For the first implementation:

* remote players' health bars should be visible;
* the local player's own world-space health bar does not need to be visible to themselves.

Preferred behavior:

```text
Player 1 sees:
    Player 2 health bar

Player 2 sees:
    Player 1 health bar
```

If there are more players later, each client should see health bars over the other players.

---

# Death Behavior

When a player dies:

Either:

* hide the health bar;

or:

* briefly show it empty before hiding.

Prefer simple hiding while dead.

After respawn:

* restore the health bar;
* display full restored health.

---

# Regeneration

Because Bullseye health regenerates, the world-space bar must update dynamically as health restores.

Example:

```text
2 HP
↓
regen
3 HP
↓
4 HP
↓
...
8 HP
```

No special animation is required, but smooth bar interpolation is preferred if straightforward.

---

# Health Bar Appearance

Use the Cowsins target health UI as a visual/behavioral reference if useful.

However, Bullseye should own the resulting health UI logic.

Desired appearance:

* compact;
* readable;
* unobtrusive;
* easy to see during combat;
* not excessively large.

A simple horizontal bar is sufficient.

Do not display detailed numeric text unless it improves readability.

---

# Future Compatibility

Design the health bar so it can later support:

* player names;
* teams/colors;
* shields;
* status effects.

Do not implement those features now.

---

# Feature 3 — Pause / Settings Menu

## Goal

Adopt the useful portions of the Cowsins pause/settings interface so the player can adjust controls and game presentation without leaving the game.

This menu must be redesigned as necessary to work correctly in multiplayer.

---

# Critical Multiplayer Rule

**DO NOT pause the entire Unity simulation using `Time.timeScale = 0`.**

Bullseye is a multiplayer game.

Opening the pause menu should pause/disable only the local player's:

* movement input;
* look input;
* firing input;
* local gameplay controls.

The server, remote players, networking, health regeneration, and other gameplay must continue running.

Conceptually:

```text
Player opens menu
       ↓
Local Gameplay Input Disabled
       ↓
Cursor/Menu Input Enabled

NETWORK GAME CONTINUES
```

---

# Pause Input

Add or preserve a Pause action.

Recommended default:

### Keyboard

`Escape`

### Controller

`Start / Menu`

Pressing Pause should toggle the local menu.

---

# Pause Menu Basic Screen

Initial menu should contain at least:

```text
PAUSED

Resume

Settings

Quit to Menu / Exit
```

If no proper main menu currently exists, "Quit to Menu" may be omitted or replaced with:

`Exit Game`

during the prototype.

Do not create an entire main-menu system in this ticket.

---

# Settings Menu

Implement a settings screen accessible from Pause.

At minimum support:

## Mouse sensitivity

Adjust horizontal/vertical mouse look sensitivity.

## Controller sensitivity

Adjust gamepad look sensitivity.

If practical, separate:

* horizontal sensitivity;
* vertical sensitivity.

## Aim sensitivity

Allow aim/ADS sensitivity scaling if the current look architecture supports it cleanly.

## Invert Y

Optional but preferred.

## Master Volume

Provide a master volume adjustment.

If straightforward, also expose:

* effects volume;
* music volume.

Only Master Volume is required.

---

# Persistence

Settings should persist between play sessions.

Use an appropriate lightweight method such as:

`PlayerPrefs`

for this prototype.

At minimum persist:

* mouse sensitivity;
* controller sensitivity;
* master volume;
* invert Y if implemented.

---

# Control Bindings / Rebinding

Use the Cowsins settings/rebind system as a reference.

The menu architecture should support future runtime rebinding.

If runtime rebinding using Unity Input System can be incorporated cleanly during this ticket, allow the player to rebind common actions such as:

* Jump;
* Sprint;
* Crouch;
* Fire;
* Aim;
* Reload.

However:

**Full runtime rebinding is preferred but not required for REQ-017.**

Do not allow rebinding complexity to block delivery of:

* Pause;
* sensitivity adjustment;
* volume adjustment.

---

# Input Architecture

The pause menu must preserve Bullseye's multiplayer-safe input architecture.

Do not replace:

`LocalPlayerInputBinding`

with Cowsins:

`InputManager`

Do not use:

`Gamepad.current`

to determine player ownership.

Pause/menu input must apply to the local owning player's assigned devices.

---

# Cursor Behavior

When the pause menu is open for keyboard/mouse:

* unlock/show cursor;
* allow menu interaction.

When menu closes:

* hide/lock cursor again;
* restore FPS controls.

Controller navigation should also function if reasonably straightforward.

---

# Local Pause State

Create a Bullseye-owned local pause/menu state.

Possible concept:

```text
LocalPlayerMenuState
{
    bool IsMenuOpen;
}
```

or equivalent.

Gameplay components should be able to query whether local controls are temporarily blocked.

Do not use Cowsins global/static:

`PauseMenu.isPaused`

as the authoritative pause state.

---

# Network Behavior While Menu Is Open

If Player 1 opens Pause:

Player 1:

* cannot move;
* cannot fire;
* cannot look around;
* can navigate menu.

Player 2:

* continues moving;
* continues firing;
* continues playing normally.

Server:

* continues running.

Health regeneration:

* continues according to server time.

Bullseye movement:

* continues operating normally for active players.

---

# Player Vulnerability While Paused

Opening the pause menu does **not** make the player invulnerable.

A paused player's avatar remains in the match.

They may still:

* be shot;
* take damage;
* die;
* respawn.

If the player dies while their menu is open:

* close the menu automatically or otherwise ensure the death/respawn UI functions correctly.

Preferred behavior:

**close Pause automatically when death occurs.**

---

# Cowsins Pause/Menu Assets

Cursor may inspect and selectively reuse:

* menu layouts;
* UI graphics;
* sliders;
* buttons;
* settings organization;
* icons;
* fonts;
* UI sound effects.

Do not import Cowsins gameplay assumptions such as:

* global time pausing;
* `PlayerStats`;
* `PlayerControl`;
* static pause state;
* single-player restart logic.

The visual layer may be Cowsins-derived.

The behavior must be Bullseye-owned and multiplayer-safe.

---

# First-Person vs Third-Person Weapon Presentation

This ticket does **not** implement separate remote third-person weapon animations.

The current first-person weapon should remain local-only.

However, camera hierarchy changes made in this ticket must preserve a clean separation between:

```text
LOCAL FIRST-PERSON PRESENTATION

Camera
└── WeaponView
    └── FirstPersonWeapon
```

and future:

```text
REMOTE / WORLD PRESENTATION

PlayerBody
└── ThirdPersonWeapon
```

Do not attach first-person weapon animation logic directly to the networked body in a way that makes future separation difficult.

A later requirement will handle:

* remote weapon models;
* third-person shooting animations;
* third-person reload animations;
* syncing weapon state between players.

---

# Existing Systems That Must Continue Working

REQ-017 must not break:

* NGO host/client spawning;
* player movement;
* jumping;
* sprinting;
* crouching;
* turning;
* Bullseye movement influences;
* Bullseye damage;
* health regeneration;
* death;
* respawn countdown;
* weapon firing;
* weapon animation;
* firing sounds;
* muzzle flash;
* hitmarker;
* controller assignment;
* keyboard/mouse input.

---

# Suggested Architecture

A possible resulting hierarchy:

```text
Player
│
├── Network/Game Components
│   ├── NetworkObject
│   ├── NetworkTransform
│   ├── PlayerHealth
│   └── BullseyeMover
│
├── Body
│   ├── Capsule / Future Mesh
│   ├── Bullseye
│   └── WorldHealthUI
│
└── LocalPresentation
    └── CameraRoot
        ├── LookRoot
        │   └── CameraEffectsRoot
        │       └── Camera
        │           └── WeaponView
        │
        └── LocalUI
            ├── Health HUD
            ├── Reticle
            └── PauseMenu
```

Exact hierarchy may differ.

---

# Required Manual Playtest

## Test 1 — Idle camera

Stand still.

Confirm:

* subtle breathing effect exists;
* motion is not distracting;
* reticle remains usable.

---

## Test 2 — Walking

Walk in multiple directions.

Confirm:

* camera movement responds to locomotion;
* transitions into/out of movement are smooth.

---

## Test 3 — Sprint

Sprint.

Confirm:

* camera motion becomes more energetic;
* effect is noticeably different from walking;
* weapon presentation remains stable.

---

## Test 4 — Jump / Land

Jump and land.

Confirm:

* existing jump works;
* landing has visible camera feedback;
* camera returns cleanly to neutral.

---

## Test 5 — Aim/Shoot During Camera Motion

Walk/sprint and fire.

Confirm:

* shooting remains accurate;
* camera effects do not break look;
* weapon animation remains functional;
* hit detection remains unchanged.

---

## Test 6 — Remote Health Bar

Player 1 looks at Player 2.

Confirm:

* health bar appears above Player 2;
* Player 1's own world health bar is not obstructing their view.

---

## Test 7 — Damage

Shoot Player 2's bullseye.

Confirm:

* Player 2 health bar decreases correctly;
* amount matches current Bullseye damage rules.

---

## Test 8 — Regeneration

Damage Player 2 without killing them.

Wait for regeneration.

Confirm:

* world health bar increases as health regenerates.

---

## Test 9 — Death / Respawn

Kill Player 2.

Confirm:

* health bar hides;
* respawn countdown continues;
* player respawns;
* health bar returns at full health.

---

## Test 10 — Pause Host

Player 1 opens Pause.

Confirm:

* Player 1 controls stop;
* menu works;
* Player 2 continues playing;
* networking continues.

---

## Test 11 — Pause Client

Player 2 opens Pause.

Confirm same behavior independently.

---

## Test 12 — Settings

Change:

* sensitivity;
* volume.

Resume game.

Confirm changes apply immediately or after leaving Settings.

Restart play session.

Confirm settings persist where applicable.

---

## Test 13 — Death While Menu Open

Open Pause.

Have another player kill the paused player.

Confirm:

* game does not freeze;
* health/death still function;
* Pause closes or otherwise transitions cleanly to respawn state.

---

# Acceptance Criteria

## Camera

* [ ] Idle breathing effect implemented.
* [ ] Walking camera motion implemented.
* [ ] Sprint camera motion implemented.
* [ ] Landing feedback implemented.
* [ ] Effects are owner-only.
* [ ] Effects are cosmetic and not networked.
* [ ] Effects do not noticeably impair aiming.
* [ ] Camera effects are Inspector-configurable.

## Player Health Bar

* [ ] Remote players have world-space health bars.
* [ ] Health reads from existing `PlayerHealth`.
* [ ] Bar updates after damage.
* [ ] Bar updates during regeneration.
* [ ] Bar hides appropriately on death.
* [ ] Bar returns after respawn.
* [ ] UI faces the local camera.
* [ ] Cowsins `PlayerStats` is not used.

## Pause

* [ ] Escape opens/closes Pause.
* [ ] Controller Menu/Start can open Pause if controller input supports it.
* [ ] Pause only disables local controls.
* [ ] Network simulation continues.
* [ ] Other players continue moving.
* [ ] Health/death/respawn continue functioning.
* [ ] Pause does not use `Time.timeScale = 0`.

## Settings

* [ ] Mouse sensitivity adjustable.
* [ ] Controller sensitivity adjustable.
* [ ] Master volume adjustable.
* [ ] Settings persist between sessions.
* [ ] Settings integrate with the active Bullseye input/look architecture.
* [ ] Cowsins static `InputManager` is not adopted as authoritative input.

## Existing Gameplay

* [ ] Multiplayer still works.
* [ ] Bullseye movement still works.
* [ ] Bullseye damage still works.
* [ ] Health regeneration still works.
* [ ] Respawn still works.
* [ ] REQ-014 weapon presentation still works.
* [ ] REQ-016 movement still works.

---

# Agent Instructions

Before modifying the project:

1. Read:

   * `docs/analysis/FPS_ENGINE_INTEGRATION_AUDIT.md`
   * REQ-014
   * REQ-016

2. Inspect existing:

   * player camera hierarchy;
   * look controller;
   * movement state;
   * `PlayerHealth`;
   * `PlayerHealthHud`;
   * `PlayerNetworkSetup`;
   * local input ownership;
   * weapon presentation hierarchy.

3. Inspect relevant Cowsins:

   * camera effects;
   * weapon effects;
   * pause menu;
   * settings UI;
   * target/world health UI if available.

4. Identify what can be reused as assets versus what should be adapted into Bullseye-owned code.

5. Do not resolve missing dependencies by importing the entire Cowsins player controller.

---

# Implementation Priority

If the ticket becomes too large, prioritize in this order:

1. Camera effects
2. World-space health bars
3. Pause menu
4. Sensitivity settings
5. Volume setting
6. Runtime control rebinding

Do not sacrifice stability of the first three features to complete full rebind support.

---

# Out of Scope

REQ-017 does not implement:

* third-person weapon models;
* remote weapon firing animation;
* remote reload animation;
* full third-person character animation;
* player names;
* teams;
* shields;
* scoreboards;
* weapon inventory;
* Cowsins health;
* global single-player pause;
* main menu overhaul;
* matchmaking;
* networked camera effects;
* advanced recoil system;
* advanced ADS system.

---

# Definition of Done

REQ-017 is complete when entering Bullseye feels materially closer to the FPS Engine demo:

* the first-person camera subtly breathes while idle;
* movement produces convincing camera motion;
* sprinting has more energetic camera sway;
* landing has physical feedback;
* enemies/other players visibly show their current health above their bodies;
* players can open a polished Pause/Settings screen and adjust important local preferences;
* opening Pause does not stop the multiplayer match.

These improvements should operate on top of the existing Bullseye multiplayer/gameplay architecture rather than replacing it.
