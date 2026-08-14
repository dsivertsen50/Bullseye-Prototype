# REQ-002 — Dual-Controller Multiplayer Testing

## Goal

Allow two Xbox controllers connected to the same development computer to be used for convenient two-player multiplayer testing.

Each controller should independently control one networked player during the existing two-player local development/testing workflow.

## Desired Behavior

When testing two connected multiplayer players on the same computer:

1. Controller 1 controls Player 1.
2. Controller 2 controls Player 2.
3. Input from Controller 1 must not control Player 2.
4. Input from Controller 2 must not control Player 1.
5. Both players should be controllable during the multiplayer test without repeatedly changing input configuration.
6. Movement, looking, jumping, and shooting should all work from the assigned controller.

Keyboard and mouse support should continue to work unless changing it is genuinely necessary.

## Investigation Requirement

Before implementing anything, inspect:

* the current multiplayer testing workflow
* `PlayerControls.inputactions`
* `PlayerMovement`
* `PlayerLook`
* `PlayerShoot`
* `PlayerNetworkSetup`
* the Player prefab
* Netcode ownership behavior
* how Unity currently detects both connected Xbox controllers
* whether editor/window focus affects controller input
* whether the project is currently using Multiplayer Play Mode or another multiple-instance testing method

Determine why the controllers are not currently isolated to individual networked players.

Do not assume `PlayerInput`, `PlayerInputManager`, or another particular solution is required before investigating the current architecture.

## Constraints

* Preserve Netcode for GameObjects networking.
* Do not convert the game to local split-screen multiplayer.
* The two players should remain separate networked players.
* Preserve existing movement, look, jump, shooting, bullseye, death, and respawn behavior.
* Prefer the smallest reliable solution appropriate for prototype testing.
* Do not introduce a lobby or permanent controller-selection UI.
* Do not redesign the entire input architecture unless necessary.
* Avoid adding new packages unless genuinely required.

## Acceptance Criteria

### AC1 — Independent movement

Moving Controller 1 moves only Player 1.

Moving Controller 2 moves only Player 2.

### AC2 — Independent look

The right stick on each controller changes only the corresponding player's view.

### AC3 — Independent jump

Jump input from each controller affects only its corresponding player.

### AC4 — Independent shooting

Fire input from each controller causes only its corresponding player to shoot.

### AC5 — Simultaneous testing

Both controllers can be used as part of the same two-player multiplayer test workflow without manually remapping controls between players.

### AC6 — Network ownership preserved

A client still cannot control another client's networked player.

### AC7 — Existing gameplay preserved

The change must not break:

* network connection
* player spawning
* movement
* mouse/keyboard input unless explicitly necessary
* shooting
* bullseye behavior
* killing
* respawning

## Validation

The agent should:

1. Inspect the existing input and multiplayer testing architecture.
2. Identify the actual cause of the current controller behavior.
3. Explain the proposed implementation before making broad architectural changes.
4. Implement the smallest reasonable solution.
5. Allow Unity to compile.
6. Read the Unity Console through MCP and resolve errors caused by the implementation.
7. Inspect relevant input/player configuration afterward.
8. State which acceptance criteria can be verified automatically.
9. Clearly identify behaviors requiring human two-controller multiplayer playtesting.

Do not claim simultaneous two-controller behavior is verified unless it has actually been tested with both physical controllers.
