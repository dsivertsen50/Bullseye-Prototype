# REQ-016 — Adopt FPS Engine Movement and Controls

## Status

Ready for implementation on:

`experiment/fps-engine-integration`

## Background

Bullseye currently has a functional but prototype-level first-person movement and control system.

The current implementation already supports:

* walking;
* looking;
* jumping;
* sprinting;
* crouching;
* aiming;
* firing;
* controller input;
* keyboard/mouse input;
* per-client device assignment;
* local multiplayer testing;
* movement-driven bullseye influence.

The Cowsins FPS Engine reference project contains a more mature first-person movement stack, including:

* Rigidbody-based locomotion;
* movement states;
* sprinting;
* jumping;
* crouching/sliding;
* movement acceleration/deceleration;
* camera look behavior;
* head/movement effects;
* additional advanced movement features.

The project is still early enough that Bullseye's existing movement implementation does **not** need to be preserved for its own sake.

This requirement may substantially replace or remove the existing Bullseye movement code if doing so results in a better FPS foundation.

However, Cowsins is a single-player framework and its complete player prefab cannot be adopted directly.

The architecture audit found that:

* Cowsins movement is based on `Rigidbody`, not `CharacterController`;
* its player stack is tightly coupled through `PlayerDependencies`;
* its runtime input uses a static `PlayerActions` instance;
* it reads `Gamepad.current`;
* its input architecture conflicts with Bullseye's existing per-client device assignment;
* several Cowsins player systems are coupled to local health, UI, interaction, weapons, and pause systems.

Therefore, this requirement should adopt the **Cowsins movement behavior and control design** while preserving Bullseye's multiplayer/game-specific systems.

---

# Goal

Replace Bullseye's current prototype movement/control experience with a more polished movement foundation based substantially on the Cowsins FPS Engine.

The resulting player should feel substantially closer to the Cowsins FPS Engine demo while remaining a valid Bullseye multiplayer player.

Primary goals:

1. Adopt Cowsins-style core locomotion.
2. Adopt the Cowsins control scheme/bindings where useful.
3. Allow replacing Bullseye's current `CharacterController` movement with Rigidbody-based movement if required.
4. Preserve Netcode for GameObjects ownership and synchronization.
5. Preserve Bullseye-specific mechanics.
6. Preserve local multiplayer controller assignment.
7. Create a maintainable foundation for later Cowsins-derived movement features.

---

# Core Architectural Principle

Cowsins should determine:

> How the player moves and how FPS controls feel.

Bullseye should continue to determine:

> Who owns the player, how that player is networked, how health/death work, and how movement affects the bullseye.

Do not simply replace the Bullseye network player with:

`CowsinsFPSController.prefab`

Instead, port/adapt the required movement systems into the Bullseye player architecture.

---

# Existing Systems That Must Remain Authoritative

Do not replace:

* `NetworkObject`
* NGO player ownership
* player spawning
* `NetworkTransform` or an equivalent NGO transform synchronization solution
* host/client architecture
* `LocalPlayerInputBinding` responsibilities
* `PlayerHealth`
* Bullseye damage
* death logic
* respawn timing
* bullseye deterministic simulation
* `BullseyeMover`
* `BullseyeTarget`
* `BullseyeDamageZones`
* `CapsuleBodySurface`
* weapon damage authority
* REQ-015 weapon presentation foundation

These systems may require adaptation to the new movement implementation, but their gameplay responsibilities must remain.

---

# Systems That May Be Replaced

The current implementations of the following are **not protected**:

* `PlayerMovement`
* `PlayerLook`
* `PlayerAimZoom`, if equivalent functionality is preserved
* current `CharacterController`
* existing crouch implementation
* existing sprint implementation
* existing jump implementation
* current acceleration/deceleration behavior
* existing camera movement feel

Cursor may substantially rewrite these systems if needed.

Obsolete scripts should be removed or disabled once the replacement is working.

Avoid maintaining two movement stacks simultaneously.

---

# Cowsins Reference Project

Treat the FPS Engine reference project as read-only.

Inspect and adapt relevant Cowsins code, including:

* `PlayerMovement`
* `PlayerStates`
* `BasicMovementBehaviour`
* `GroundDetectionBehaviour`
* `JumpBehaviour`
* `CrouchSlideBehaviour`
* `CameraLookBehaviour`
* `VelocityHandlerBehaviour`
* related movement interfaces
* Cowsins input action mappings

Do not modify the reference project.

Do not copy the entire Cowsins player prefab into Bullseye.

---

# Movement Architecture

The implementation may migrate Bullseye from:

```text
CharacterController
    +
custom PlayerMovement
```

to:

```text
Rigidbody
    +
Cowsins-derived movement logic
```

if this is the cleanest way to preserve the FPS Engine movement feel.

If Rigidbody movement is adopted:

* remove/disable the CharacterController once the replacement works;
* configure Rigidbody constraints appropriately for an FPS player;
* prevent uncontrolled body tipping/rotation;
* preserve player yaw behavior;
* preserve collision stability;
* preserve multiplayer transform synchronization;
* preserve respawn teleport behavior.

Do not add a Rigidbody while leaving an active CharacterController controlling the same player.

---

# Required Core Movement Features

The following must work before REQ-016 is complete.

## Walking

Player must:

* move with keyboard WASD;
* move with gamepad left stick;
* respond smoothly rather than feeling like direct transform teleportation;
* preserve correct world-relative/local-relative FPS movement.

Movement feel should use Cowsins behavior/tuning as the primary reference.

## Looking

Player must:

* look with mouse;
* look with gamepad right stick;
* yaw horizontally;
* pitch vertically;
* preserve sensible pitch clamping;
* feel substantially closer to Cowsins look behavior.

Only the owning client should process local look input.

Remote players must not run local camera look behavior.

## Jumping

Player must support Cowsins-style jumping behavior.

Requirements:

* jump only under valid grounded conditions;
* preserve gravity;
* avoid accidental repeated air jumps unless intentionally supported;
* retain stable landing behavior.

The existing Bullseye bullseye-jump influence must still trigger.

If the new movement system uses an event such as:

`OnJump`

that event should bridge into the existing Bullseye mechanic.

Conceptually:

```text
Cowsins-derived Jump
        ↓
Bullseye movement adapter
        ↓
BullseyeMover jump influence
```

## Sprinting

Adopt Cowsins-style sprint behavior.

Use the FPS Engine's behavior/tuning where practical.

Sprint must:

* respond to the intended Sprint binding;
* modify player movement speed;
* stop correctly;
* remain owner-controlled;
* not interfere with networking.

If the previous Bullseye sprint stamina/duration system conflicts with Cowsins behavior, it may be removed or replaced.

## Crouching

Adopt Cowsins-style crouch behavior.

The implementation may change:

* player collider dimensions;
* camera height;
* movement speed;
* transition timing.

The player's bullseye mechanic must still know whether the player is crouching.

Existing crouch-network synchronization may be redesigned if the new system requires it, but other clients must retain an accurate enough crouch state for gameplay and presentation.

## Sliding

If Cowsins `CrouchSlideBehaviour` can be integrated cleanly as part of crouching, include basic sliding.

If integrating slide substantially expands scope or destabilizes multiplayer, leave slide for a later requirement.

Sliding is **preferred but not mandatory** for REQ-016.

---

# Input Architecture

## Adopt the Cowsins Control Scheme

Inspect:

`Assets/Cowsins/Inputs/PlayerActions.inputactions`

Use its bindings and action organization as a reference.

Bullseye's gameplay controls should move toward supporting actions such as:

* Move
* Look
* Jump
* Sprint
* Crouch
* Fire
* Aim
* Reload
* Interact
* Weapon Switch
* Melee
* optional future movement actions

Not every action must have gameplay behavior in this requirement.

The goal is to establish a control map that can support future FPS Engine-derived features.

---

# Do Not Adopt Cowsins InputManager as the Live Input System

Do not enable or depend on the Cowsins runtime:

`InputManager`

for the network player.

Do not use the Cowsins static `PlayerActions` instance as the authoritative gameplay input path.

Do not use `Gamepad.current` to determine which local player's controller should drive the player.

The architecture audit found that this would conflict with Bullseye's per-client input device assignment.

Keep the responsibilities currently handled by:

`LocalPlayerInputBinding`

or replace it only with an equivalent multiplayer-safe abstraction.

The essential requirement is:

```text
Client 0
    → assigned device(s)
    → only Client 0 player

Client 1
    → assigned gamepad
    → only Client 1 player
```

The exact code may be refactored.

---

# Recommended Input Strategy

Preferred approach:

1. Extend or revise Bullseye's existing `PlayerControls.inputactions`.
2. Match Cowsins bindings where appropriate.
3. Continue assigning devices per local network client.
4. Feed those actions into Cowsins-derived movement logic.

Conceptually:

```text
Cowsins control scheme
          ↓
Bullseye Input Actions
          ↓
LocalPlayerInputBinding
          ↓
Owning client only
          ↓
Cowsins-derived movement
```

Do not run both Cowsins input and Bullseye input simultaneously.

---

# Suggested Default Controls

Use Cowsins bindings as the main reference, while preserving currently working Bullseye controls where sensible.

Minimum required:

| Action | Keyboard/Mouse | Gamepad                                        |
| ------ | -------------- | ---------------------------------------------- |
| Move   | WASD           | Left Stick                                     |
| Look   | Mouse          | Right Stick                                    |
| Jump   | Space          | South button                                   |
| Fire   | Left Mouse     | Right Trigger                                  |
| Aim    | Right Mouse    | Right Stick Press or current mapped equivalent |
| Sprint | Left Shift     | Left Stick Press                               |
| Crouch | Left Ctrl      | East button                                    |

Existing mappings that are already working should not be changed arbitrarily unless required to align with the new control architecture.

---

# Multiplayer Ownership

Movement input must execute only for:

`IsOwner == true`

or the equivalent owner check.

Remote player instances must not:

* read local Move input;
* read local Look input;
* jump based on another client's keyboard/controller;
* sprint based on local input;
* crouch based on local input;
* process camera input.

Remote players should receive synchronized motion through NGO.

---

# Networking

The player must remain a Netcode for GameObjects player.

If Rigidbody movement is adopted, determine the cleanest compatibility with:

`NetworkTransform`

or an appropriate NGO physics/transform synchronization strategy.

Do not add a second network identity.

Do not network the Cowsins player prefab separately.

The network hierarchy should remain conceptually:

```text
Bullseye Player
├── NetworkObject
├── NetworkTransform
├── Rigidbody
├── BullseyeMovementController
├── BullseyeInput
├── PlayerHealth
├── BullseyeMover
├── BullseyeTarget
└── Camera / weapon presentation
```

Names may vary.

---

# Rigidbody Considerations

If moving to Rigidbody locomotion:

* disable gravity handling that would conflict with the adopted movement code;
* constrain Rigidbody rotation where necessary;
* avoid physics forces causing the player to tip over;
* verify interpolation settings;
* verify remote movement smoothness;
* verify local responsiveness;
* verify collision behavior between networked players.

The implementation should favor movement quality over preserving old CharacterController assumptions.

---

# Respawn Integration

The existing respawn system must continue working.

After death:

* player movement input remains disabled;
* camera behavior remains appropriate;
* Rigidbody velocity should not continue carrying the dead player around;
* any crouch/slide state must reset.

On respawn:

* position must reset to the existing spawn location;
* linear velocity should reset;
* angular velocity should reset;
* crouch state should reset;
* movement state should return to default;
* camera state should reset;
* bullseye state should reset according to existing gameplay rules.

If `NetworkTransform.Teleport` remains necessary, preserve it.

---

# Bullseye Movement Influence Integration

This is a critical acceptance requirement.

Bullseye movement currently responds to:

* jump;
* crouch;
* turning.

Changing movement systems must not remove these mechanics.

Create explicit integration events/adapters if needed.

Recommended conceptual interface:

```text
Movement Event
    ↓
BullseyeMovementInfluenceAdapter
    ↓
BullseyeMover
```

Examples:

```text
JumpStarted
    → BullseyeMover.NotifyJump()

CrouchStarted
    → BullseyeMover.NotifyCrouch(...)

TurnRate
    → BullseyeMover existing turn influence
```

Do not rewrite `BullseyeMover` solely to fit the new movement controller unless a small compatibility change is necessary.

---

# Camera Feel

Adopt as much of the Cowsins camera/look feel as can be integrated safely.

The following may be included:

* more polished mouse/gamepad look;
* sensitivity handling;
* smooth movement;
* landing motion;
* subtle camera effects.

Do not adopt Cowsins camera behavior if it:

* takes over multiplayer ownership;
* depends on Cowsins `PlayerStats`;
* depends on `PauseMenu`;
* requires Cowsins static input;
* conflicts with the weapon presentation system.

Basic Cowsins-style look feel is required.

Full head bob/camera effects are optional for this ticket.

---

# Aim Integration

The existing Aim input must remain available for REQ-015 weapon presentation.

If replacing `PlayerAimZoom`:

* preserve the same gameplay function;
* maintain one component as the sole FOV owner;
* do not allow multiple camera systems to fight over FOV.

If keeping `PlayerAimZoom` is simpler, keep it.

---

# Weapon Compatibility

REQ-016 must not break REQ-015.

After movement migration:

* Ruger remains visible;
* fire animation still works;
* pistol audio still works;
* muzzle flash still works;
* weapon hides on death;
* weapon returns on respawn.

Weapon presentation should follow the new camera correctly during:

* walking;
* sprinting;
* crouching;
* jumping;
* looking.

---

# Cowsins Systems That Must Still Not Replace Bullseye Systems

Do not adopt as authoritative:

* `CowsinsFPSController`
* `PlayerDependencies` full dependency graph
* `PlayerStats`
* Cowsins health/shield
* Cowsins damage system
* Cowsins respawn/death
* Cowsins enemies
* Cowsins pickups
* Cowsins UI
* Cowsins pause system
* Cowsins `IDamageable`
* Cowsins weapon hit detection
* Cowsins static `InputManager`

---

# Cowsins Movement Code

Unlike previous integration requirements, Cursor **may copy or adapt Cowsins movement source code** if that is the most maintainable implementation.

If source code is copied:

* place unchanged third-party source under a clearly identified third-party folder;
* preserve license/source attribution as appropriate;
* place Bullseye modifications/adapters in Bullseye-owned code;
* avoid modifying large vendor files unnecessarily.

If adapting a small amount of source into Bullseye-owned code is cleaner, that is also acceptable.

Prefer maintainability over strict adherence to one method.

---

# Do Not Import Everything to Solve Dependencies

Do not respond to a missing reference by automatically copying the entire Cowsins runtime.

If a movement class requires:

* `PlayerStats`;
* `UIController`;
* `InteractManager`;
* `WeaponController`;
* `PauseMenu`;

first determine whether the dependency can be removed, adapted, or replaced.

Movement should not require single-player health/UI systems.

---

# Existing Code Cleanup

Once the new movement architecture is validated, remove or disable obsolete movement scripts.

Avoid a final prefab containing both:

```text
Old PlayerMovement
New PlayerMovement
CharacterController
Rigidbody
multiple look controllers
multiple crouch controllers
```

There should be one clear active movement stack.

Before deleting an old script, confirm no remaining Bullseye component still depends on it.

Update those dependencies to use the new movement system or an adapter.

---

# Required Manual Testing

Test with at least two local NGO clients.

## Test 1 — Host movement

Host should:

* walk;
* look;
* jump;
* sprint;
* crouch;
* aim;
* fire.

Confirm movement feels comparable to the Cowsins reference demo.

## Test 2 — Client movement

Client should perform the same actions independently.

Confirm client does not control host.

## Test 3 — Two controllers

Use the existing dual-controller workflow.

Confirm:

* controller 1 controls only its assigned player;
* controller 2 controls only its assigned player;
* no use of `Gamepad.current` causes both players to respond;
* keyboard/mouse behavior remains appropriate.

## Test 4 — Remote synchronization

Observe Player 2 from Player 1.

Confirm:

* walking is visible;
* sprinting is visible;
* jumping is visible;
* crouching is visible;
* turning is visible;
* remote motion is reasonably smooth.

## Test 5 — Bullseye jump behavior

Jump.

Confirm the bullseye responds to jump exactly as the game mechanic expects.

## Test 6 — Bullseye crouch behavior

Crouch.

Confirm the bullseye receives the crouch influence.

## Test 7 — Bullseye turning behavior

Turn rapidly.

Confirm turn influence remains functional.

## Test 8 — Death/respawn

Kill a player.

Confirm:

* movement stops during death;
* respawn countdown works;
* player respawns correctly;
* velocity does not carry over;
* crouch/slide state does not carry over;
* movement resumes normally.

## Test 9 — Weapon presentation

Move through all required actions while firing.

Confirm REQ-015 remains functional.

---

# Acceptance Criteria

## Core movement

* [ ] Player uses the new Cowsins-derived movement implementation.
* [ ] Walking works.
* [ ] Looking works.
* [ ] Jumping works.
* [ ] Sprinting works.
* [ ] Crouching works.
* [ ] Movement quality is noticeably closer to Cowsins than the original Bullseye controller.
* [ ] Only one movement stack is active.

## Input

* [ ] Cowsins control scheme has been adopted where appropriate.
* [ ] Bullseye input actions support the required controls.
* [ ] `LocalPlayerInputBinding` or an equivalent multiplayer-safe system remains.
* [ ] Cowsins static `InputManager` is not the live gameplay input system.
* [ ] `Gamepad.current` is not used to decide multiplayer player ownership.
* [ ] Host/client controller independence remains functional.

## Networking

* [ ] NGO player spawning still works.
* [ ] Host movement replicates.
* [ ] Client movement replicates.
* [ ] Remote movement is reasonably smooth.
* [ ] No duplicate `NetworkObject` is introduced.
* [ ] Ownership remains correct.

## Bullseye mechanics

* [ ] Jump influence remains functional.
* [ ] Crouch influence remains functional.
* [ ] Turn influence remains functional.
* [ ] `BullseyeMover` remains deterministic/network-compatible.
* [ ] Bullseye damage logic remains unchanged.

## Health / respawn

* [ ] Existing health works.
* [ ] Existing death works.
* [ ] Existing respawn countdown works.
* [ ] Player respawns at the intended location.
* [ ] Rigidbody velocity/state resets on respawn if Rigidbody movement is used.

## Weapons

* [ ] REQ-015 weapon presentation still functions.
* [ ] Fire still uses Bullseye hit logic.
* [ ] Weapon does not break while sprinting/jumping/crouching.
* [ ] Aim still works.

## Architecture

* [ ] Cowsins health is not used.
* [ ] Cowsins damage system is not used.
* [ ] Cowsins complete player prefab does not replace the NGO player.
* [ ] Cowsins static input architecture is not used.
* [ ] No unnecessary Cowsins UI/interaction systems were imported.
* [ ] Obsolete movement code is removed or clearly disabled.

---

# Preferred Implementation Order

## Phase 1 — Input map

* inspect Cowsins control mappings;
* update Bullseye input actions;
* preserve per-client device binding.

## Phase 2 — Basic movement

Implement:

* Rigidbody/player collider;
* walk;
* gravity;
* grounded detection;
* look.

Verify host + client.

## Phase 3 — Jump and sprint

Add:

* Cowsins-derived jump;
* sprint;
* bullseye jump bridge.

Verify multiplayer.

## Phase 4 — Crouch

Add:

* crouch;
* collider/camera transition;
* bullseye crouch bridge.

Verify multiplayer.

## Phase 5 — Turn integration

Reconnect or preserve:

* turn-rate detection;
* `BullseyeMover` turn influence.

## Phase 6 — Respawn/state cleanup

Ensure:

* Rigidbody reset;
* state reset;
* movement lock during death;
* correct respawn.

## Phase 7 — Polish

If stable, optionally include:

* slide;
* head bob;
* landing camera effect;
* better acceleration;
* additional Cowsins movement feel.

Do not allow optional polish to block completion of the required core movement.

---

# Out of Scope

Unless trivial and dependency-free, do not implement:

* wall running;
* grappling;
* wall bounce;
* dash;
* ladders;
* stamina UI;
* advanced camera shake;
* advanced footsteps;
* parkour;
* fall damage;
* Cowsins health/shields;
* pickup interactions;
* inventory;
* pause menu;
* XP/coins;
* Cowsins enemies;
* weapon switching.

These can become later requirements after core locomotion proves stable in multiplayer.

---

# Agent Instructions

Before changing code:

1. Read:

   * `docs/analysis/FPS_ENGINE_INTEGRATION_AUDIT.md`
   * REQ-015
   * current `PlayerMovement`
   * current `PlayerLook`
   * `LocalPlayerInputBinding`
   * `PlayerNetworkSetup`
   * `PlayerHealth`
   * `BullseyeMover`
   * `FirstPersonWeaponView`
   * `PlayerShoot`

2. Inspect the corresponding Cowsins movement/input files in the read-only reference project.

3. Identify which Cowsins movement code can be copied/adapted without bringing in the complete player dependency graph.

4. Before implementation, make a brief internal dependency map.

5. Implement incrementally.

Do not attempt to port the entire Cowsins FPS controller in one operation.

Compile and test after each major movement phase.

---

# Definition of Done

REQ-016 is complete when Bullseye no longer feels like it is using a rudimentary prototype movement controller and instead has a stable Cowsins-derived FPS locomotion/control foundation while preserving its multiplayer and game-specific mechanics.

The final architecture should conceptually be:

```text
         BULLSEYE PLAYER
               │
      ┌────────┼────────┐
      │        │        │
     NGO    Health   Bullseye
  Ownership  Respawn   Rules
      │
      └────────┬────────┘
               │
        FPS PLAYER LAYER
               │
      Cowsins-derived
        movement/look
               │
        Bullseye input
          ownership
```

The user should be able to start two players, independently control both, experience substantially improved FPS locomotion, shoot normally, influence the bullseye through movement, die, respawn, and continue playing without relying on the Cowsins single-player player stack.
