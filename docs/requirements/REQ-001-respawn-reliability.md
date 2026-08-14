# REQ-001 — Reliable Player Respawning

## Goal

Ensure that when a player is killed, that player reliably respawns at the intended respawn location rather than occasionally remaining at or returning to the location where they died.

## Current Behavior

The prototype currently contains a `PlayerHealth` respawn system.

Known behavior:

* Hitting another player's bullseye can kill that player.
* The killed player is intended to respawn at the configured respawn position.
* During multiplayer testing, a killed player has occasionally appeared to respawn at or return to their previous location instead of remaining at the respawn location.

## Desired Behavior

When a player is killed:

1. Only the killed player should be respawned.
2. The player should be moved to the intended respawn location.
3. The new position should synchronize correctly across connected clients.
4. The player should remain controllable after respawning.
5. The player should not snap back to the death location after being respawned.
6. Killing one player must not move or respawn another player.

For this requirement, retaining the existing prototype respawn location is acceptable. A full spawn-point system is outside scope.

## Constraints

* Preserve the existing multiplayer architecture using Netcode for GameObjects.
* Preserve existing player movement, looking, shooting, bullseye movement, and reticle behavior.
* Do not create a generalized spawn-management system unless it is genuinely necessary to fix the bug.
* Prefer the smallest reliable fix.
* Do not add unrelated gameplay features.
* Do not add scoring, death animations, respawn timers, or spawn selection as part of this requirement.

## Acceptance Criteria

### AC1 — Correct player moves

When Player A kills Player B, Player B is the player that respawns.

Player A must not be repositioned as a consequence of Player B's death.

### AC2 — Correct respawn position

The killed player is moved to the intended respawn position.

### AC3 — No snap-back

After respawning, the player does not immediately return or snap back to the position where they died.

### AC4 — Network synchronization

Other connected clients observe the killed player at the respawn location after the respawn occurs.

### AC5 — Controls remain functional

The respawned player can continue moving, looking, and shooting normally.

### AC6 — Existing gameplay preserved

The change does not break:

* multiplayer connection/spawning
* player movement
* camera/look controls
* shooting
* bullseye hit detection
* bullseye movement
* reticle behavior

## Validation

The agent should:

1. Inspect the existing `PlayerHealth`, `NetworkTransform`, ownership, and respawn implementation before changing anything.
2. Implement the smallest reasonable correction.
3. Allow Unity to compile.
4. Read the Unity Console and resolve any compilation errors introduced by the change.
5. Inspect relevant player/network configuration after implementation.
6. State which acceptance criteria can be verified automatically.
7. Clearly identify acceptance criteria requiring multiplayer human playtesting.

The agent must not claim multiplayer behavior has been verified if it has not actually been tested with multiple connected players.
