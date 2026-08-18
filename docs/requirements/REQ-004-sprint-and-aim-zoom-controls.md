REQ-004 — Sprint and Aim Zoom Controls
Goal

Enhance the existing player controls with two basic FPS movement/aiming features:

Sprint
Aim/Zoom

Both features should work with keyboard/mouse and Xbox controllers while preserving the existing multiplayer input isolation established in REQ-002.

These should remain simple prototype implementations with configurable values so they can be tuned during playtesting.

Desired Controls
Sprint

Sprint should activate from:

Keyboard: Left Shift
Xbox Controller: Left Stick Press / L3

While sprinting:

player movement speed should increase to 2.25× normal walking speed
sprint should be limited to a maximum continuous duration of 7.5 seconds
both the speed multiplier and duration should be configurable in the Unity Inspector
Aim / Zoom

Aim/zoom should activate from:

Mouse: Right Mouse Button
Xbox Controller: Right Stick Press / R3

While the aim/zoom input is held:

the player's camera should smoothly zoom in slightly
for the current prototype, use a configurable 25% FOV reduction
releasing the input should smoothly restore the normal field of view

The zoom amount will eventually depend on the equipped weapon, so the implementation should avoid unnecessarily hard-coding the current zoom amount into the input system.

Investigation Requirement

Before implementing anything, inspect:

PlayerControls.inputactions
PlayerMovement
PlayerLook
PlayerNetworkSetup
the Player prefab
the player camera setup
existing Xbox controller bindings
keyboard and mouse bindings
Netcode ownership behavior
the dual-controller solution implemented for REQ-002
any existing movement-speed variables
any existing camera FOV or aiming-related logic

Determine the smallest appropriate way to add these controls to the existing architecture.

Do not assume a particular stamina, camera, or weapon architecture is required before investigating the current project.

Sprint Requirements
Basic Sprint Behavior

While the sprint input is active and sprint is available:

The player should move at 2.25× their normal walking speed.
Sprint should affect normal player locomotion without changing unrelated physics behavior.
Sprint should work with both keyboard/mouse and controller input.
Sprint should affect only the locally owned player.

The default sprint multiplier should be:

2.25

This value should be configurable in the Inspector.

Sprint Duration

A player should be able to sprint continuously for a maximum of:

7.5 seconds

The sprint duration should be configurable in the Inspector.

If the player continues holding the sprint input after the maximum duration expires:

sprint should stop
movement should return to normal walking speed
the player should not remain permanently stuck in sprint state

For this requirement, do not build a polished stamina system or stamina UI.

Use the simplest reliable sprint-duration implementation appropriate to the existing movement architecture.

If a reset/recharge behavior is necessary to make repeated sprinting functional, keep it simple, expose relevant timing values where useful, and clearly document the behavior in the completion summary.

Do not add:

stamina bars
stamina sounds
exhaustion effects
movement penalties
complex stamina regeneration systems

unless genuinely required for the basic functionality.

Sprint Input Behavior

Sprint should use:

Keyboard

Left Shift

Xbox Controller

Left Stick Press / L3

Input from one local controller must not activate sprint for the other networked player.

The implementation must preserve the controller isolation established in REQ-002.

Aim / Zoom Requirements
Basic Zoom Behavior

Holding the aim/zoom input should slightly narrow the local player's camera field of view.

For the current prototype, the default zoom should represent approximately a:

25% reduction in the player's normal FOV

For example, if the normal FOV were 60, the zoomed FOV would be approximately 45.

Do not assume that 60 is the actual current FOV. Determine the existing camera FOV and calculate the zoom relative to it.

The amount of zoom should be configurable.

Smooth Camera Transition

Zooming should not instantly snap between FOV values unless the existing architecture makes a smooth transition impractical.

Prefer a short, smooth transition when:

entering aim/zoom
leaving aim/zoom

Expose a zoom transition speed or duration in the Inspector if useful.

The effect should feel responsive rather than cinematic or slow.

Aim / Zoom Input Behavior

Aim/zoom should use:

Mouse

Right Mouse Button

Xbox Controller

Right Stick Press / R3

For the current implementation, aim/zoom should remain active while the input is held and return to normal when released.

The input should affect only the locally owned player's camera.

One player's aim/zoom input must not change another player's camera.

Future Weapon Compatibility

The amount of zoom will eventually depend on the weapon currently held.

Do not implement a full weapon-dependent aiming system as part of REQ-004.

However, avoid placing the zoom value directly inside low-level input handling in a way that would make future weapon-specific zoom difficult.

Prefer a structure where the current zoom amount can later be supplied or overridden by the equipped weapon.

For now, there is only one default zoom configuration.

Multiplayer Requirements

Sprint and aim/zoom must remain compatible with the existing Netcode for GameObjects architecture.

Sprint

Other players should observe the actual movement resulting from sprinting through the existing network movement synchronization.

Do not create a separate network movement system just for sprinting.

Aim / Zoom

Camera FOV is local presentation state.

A player's zoom state does not need to be synchronized to other clients for this requirement unless the existing architecture specifically requires it.

Player 1 zooming their camera must not alter Player 2's camera.

Configurable Values

Expose useful prototype tuning parameters in the Unity Inspector.

At minimum:

Sprint
walking speed, if it is already configurable
sprint speed multiplier
maximum sprint duration

Default sprint values:

Sprint Speed Multiplier: 2.25
Maximum Sprint Duration: 7.5 seconds
Aim / Zoom
zoom amount / FOV reduction
zoom transition speed or duration, if used

Default zoom:

FOV Reduction: 25%

Keep these values centralized enough that they can be easily changed during playtesting.

Constraints
Preserve Netcode for GameObjects networking.
Preserve the dual-controller behavior from REQ-002.
Preserve existing keyboard and mouse controls.
Preserve player movement.
Preserve player looking.
Preserve jumping.
Preserve shooting.
Preserve bullseye behavior.
Preserve death and respawn.
Preserve multiplayer ownership rules.
Do not introduce a stamina UI.
Do not introduce weapon-specific zoom behavior yet.
Do not add aiming animations yet.
Do not add weapon sway changes yet.
Do not add movement accuracy penalties yet.
Do not redesign the full movement system unless genuinely necessary.
Do not redesign the full camera system unless genuinely necessary.
Avoid adding new packages unless genuinely required.
Prefer the smallest reliable implementation appropriate for prototype testing.
Acceptance Criteria
AC1 — Keyboard Sprint

Holding Left Shift causes the keyboard/mouse player's movement speed to increase to approximately 2.25× walking speed.

AC2 — Controller Sprint

Pressing/holding Left Stick / L3 activates sprint for the player assigned to that controller.

It must not activate sprint for another player's controller.

AC3 — Sprint Duration

A continuously held sprint lasts for no more than approximately 7.5 seconds using the default configuration.

After the allowed sprint duration expires, the player's speed returns to normal walking speed.

AC4 — Sprint Configuration

The sprint speed multiplier and maximum sprint duration can be changed without modifying code.

AC5 — Sprint Movement Compatibility

Sprint continues to work correctly while:

moving forward
moving backward
strafing
turning

Sprint must not break jumping or normal movement.

AC6 — Mouse Zoom

Holding Right Mouse Button causes the local player's camera to zoom in.

Releasing Right Mouse Button returns the camera to its normal FOV.

AC7 — Controller Zoom

Holding Right Stick / R3 causes the camera belonging to that controller's player to zoom in.

It must not zoom another player's camera.

AC8 — Zoom Amount

With the default configuration, zoom reduces the player's normal camera FOV by approximately 25%.

The amount can be changed without modifying code.

AC9 — Smooth Zoom

Entering and leaving zoom produces a short, visually smooth camera transition rather than an unnecessarily harsh snap, assuming the existing camera architecture supports this cleanly.

AC10 — Local Camera Isolation

Player 1 activating zoom affects only Player 1's camera.

Player 2 activating zoom affects only Player 2's camera.

No camera FOV state is incorrectly shared between networked players.

AC11 — Existing Gameplay Preserved

The implementation must not break:

network connection
player spawning
independent controller assignment
keyboard/mouse controls
movement
looking
jumping
shooting
moving bullseye behavior
killing
respawning
Validation

The agent should:

Inspect the current input, movement, camera, multiplayer, and controller architecture.
Identify the smallest appropriate implementation.
Add the required input actions/bindings.
Implement configurable sprint behavior.
Implement configurable aim/zoom behavior.
Allow Unity to compile.
Read the Unity Console through MCP.
Resolve errors caused by the implementation.
Inspect the Player prefab and relevant configuration afterward.
Verify keyboard/mouse behavior where possible.
State which acceptance criteria can be verified automatically.
Clearly identify behaviors requiring human controller or multiplayer playtesting.

Do not claim physical controller behavior has been verified unless it has actually been tested with the controllers.

Do not claim multiplayer camera isolation has been verified unless it has actually been tested in the multiplayer workflow.

Completion Summary

After implementation, report:

scripts and assets modified
input actions or bindings added
how sprint speed is applied
how sprint duration is tracked
how sprint becomes available again after reaching its duration limit
Inspector parameters available for sprint tuning
how camera zoom is implemented
normal and zoom FOV behavior
Inspector parameters available for zoom tuning
how the implementation preserves dual-controller isolation
any behavior requiring manual multiplayer testing