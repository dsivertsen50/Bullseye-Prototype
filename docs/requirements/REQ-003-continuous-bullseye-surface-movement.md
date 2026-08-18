# REQ-003 — Continuous Bullseye Surface Movement

## Goal

Make each player's bullseye continuously move across the surface of that player's body at a fixed pace.

The bullseye should visibly crawl around the player's body rather than remaining stationary or teleporting between predefined locations.

For the current prototype, the player body is a capsule. The implementation should work reliably with the current capsule while avoiding unnecessary assumptions that would make it difficult to later replace the capsule with a more complex animated player mesh.

## Desired Behavior

While a player is alive:

1. The bullseye continuously moves across the surface of the player's body.
2. Movement is smooth rather than teleporting between points.
3. The bullseye moves at a configurable fixed speed.
4. The bullseye can travel around different sides of the body, including the front, sides, and back.
5. The bullseye remains on or slightly above the body's visible surface.
6. The bullseye remains correctly oriented relative to the surface as it moves.
7. The bullseye continues moving while the player walks, turns, jumps, or otherwise moves.
8. The bullseye remains the player's active vulnerable hit target while moving.
9. Both multiplayer clients should see substantially the same bullseye position.
10. After death and respawn, the player should have exactly one bullseye and its movement should resume normally.

The movement path does not need to be sophisticated or strategically meaningful yet. The purpose of this requirement is to establish a simple continuously moving weak point that can be playtested.

## Investigation Requirement

Before implementing anything, inspect:

* the current Player prefab
* the current player body/capsule hierarchy
* the existing bullseye object and scripts
* bullseye hit detection
* `PlayerShoot`
* player health/death behavior
* respawn behavior
* `PlayerNetworkSetup`
* Netcode ownership and synchronization of the bullseye
* how the bullseye is currently positioned relative to the player

Determine how the existing bullseye system works before changing it.

Reuse the existing bullseye and damage system where practical rather than creating a separate unrelated weak-point system.

Do not assume a particular surface-traversal algorithm before inspecting the existing architecture.

## Surface Movement Requirements

The bullseye should continuously travel along the player's visible body surface.

It should:

* remain on or immediately above the capsule surface
* not visibly float far away from the player
* not become embedded inside the player
* not travel through the inside of the capsule
* adjust its orientation so its visible face approximately follows the body's surface
* be able to move around the circumference of the capsule rather than remaining only on the front
* use smooth continuous motion

A small configurable offset from the body surface may be used to prevent clipping or z-fighting.

The current implementation only needs to support the current capsule body correctly.

## Body-Relative Behavior

Bullseye movement must be relative to the player's body rather than world space.

If the player:

* moves
* rotates
* jumps
* dies
* respawns

the bullseye must continue to behave as part of that player's body.

Player movement or rotation must not cause the bullseye to remain behind at an old world-space position.

## Multiplayer Requirements

The moving bullseye must remain compatible with the existing Netcode for GameObjects multiplayer architecture.

Both players should observe substantially the same bullseye position on a given player.

Do not rely on unrelated local simulations on each client if that can cause the visible bullseye location or its hitbox to diverge between clients.

Use an authoritative or otherwise deterministic/network-synchronized solution appropriate to the current architecture.

The solution does not need to send the bullseye transform over the network every frame if a simpler or more efficient synchronized approach produces reliable results.

## Hit Detection Requirements

The bullseye must remain the player's existing vulnerable hit target while moving.

As the bullseye moves:

1. Its hitbox/collider must move with the visible bullseye.
2. Shooting the bullseye at its current location should register normally.
3. Shooting a location the bullseye has already moved away from should not register as a bullseye hit.
4. Existing damage, death, and respawn behavior should remain intact.

Do not redesign the broader combat or damage system unless a small change is required to support moving hit detection.

## Configurable Values

Expose useful prototype tuning values in the Unity Inspector.

At minimum:

* bullseye movement speed
* bullseye surface offset

Expose additional parameters only if they are useful to the chosen implementation.

Choose reasonable default values so the feature can be tested immediately.

## Future Compatibility

The current player body is a capsule, but it may later become a more complex animated humanoid mesh.

Do not attempt to build a complete generalized animated-mesh traversal system as part of REQ-003.

However:

* avoid scattering hard-coded capsule dimensions throughout unrelated scripts
* keep capsule-specific surface calculation reasonably isolated
* avoid tightly coupling movement logic to the current temporary player visual
* prefer an implementation that could later have its surface-position logic replaced or extended

The goal is not to overengineer for the future. The goal is simply to avoid making the current prototype unnecessarily difficult to evolve.

## Constraints

* Preserve Netcode for GameObjects networking.
* Preserve the existing two-player testing workflow.
* Preserve existing controller behavior.
* Preserve movement, looking, jumping, shooting, killing, and respawning.
* Preserve the existing bullseye damage behavior.
* Do not implement player control over bullseye movement yet.
* Do not add location-based damage yet.
* Do not add headshot-specific behavior yet.
* Do not add multiple bullseyes.
* Do not add bullseye abilities or power-ups.
* Do not add complex pathfinding over arbitrary meshes.
* Do not implement full animated `SkinnedMeshRenderer` support yet.
* Prefer the smallest reliable implementation appropriate for the current prototype.
* Avoid adding new packages unless genuinely required.

## Acceptance Criteria

### AC1 — Continuous movement

The bullseye continuously moves while the player is alive.

It does not periodically teleport between predefined locations.

### AC2 — Fixed pace

The bullseye moves at a reasonably consistent fixed speed.

The movement speed can be adjusted from the Unity Inspector.

### AC3 — Surface following

The bullseye remains on or immediately above the capsule surface while moving.

It does not visibly float away from the player, become buried inside the capsule, or travel through the capsule interior.

### AC4 — Surface coverage

The bullseye can visibly travel around multiple sides of the player, including the front, sides, and back.

### AC5 — Surface orientation

The bullseye remains visually oriented outward from the body as it moves around the capsule.

### AC6 — Player-relative movement

Walking, rotating, and jumping do not detach the bullseye from the player or leave it behind in world space.

### AC7 — Multiplayer synchronization

Both connected players see substantially the same bullseye location on each networked player.

No obvious long-term divergence occurs between clients.

### AC8 — Moving hitbox

The bullseye's vulnerable hit detection follows its visible position.

Shooting the current bullseye position registers a hit.

Shooting a location it has already moved away from does not register as a bullseye hit.

### AC9 — Respawn behavior

After death and respawn:

* the player has exactly one bullseye
* the bullseye is positioned correctly on the player
* movement resumes
* network synchronization remains functional

### AC10 — Existing gameplay preserved

The change must not break:

* network connection
* player spawning
* controller assignment
* movement
* looking
* jumping
* shooting
* killing
* respawning
* existing bullseye damage behavior

## Validation

The agent should:

1. Inspect the existing bullseye, player, shooting, health, respawn, and networking architecture.
2. Explain the proposed surface-movement approach before making broad architectural changes.
3. Implement the smallest reasonable solution for the current capsule player.
4. Allow Unity to compile.
5. Read the Unity Console through MCP and resolve errors caused by the implementation.
6. Inspect the resulting Player prefab and bullseye configuration afterward.
7. Verify that only one bullseye exists per player after repeated respawns.
8. State which acceptance criteria can be verified automatically.
9. Clearly identify behaviors requiring human multiplayer playtesting.

Do not claim that smoothness, visual surface following, multiplayer visual synchronization, or moving-target gameplay feel are verified unless they have actually been observed during multiplayer playtesting.

## Completion Summary

After implementation, report:

* the surface-movement approach used
* scripts, prefabs, or assets modified or created
* how the bullseye position is synchronized over the network
* how its moving hit detection works
* Inspector parameters available for tuning
* limitations of the current capsule implementation
* what would likely need to change when the capsule is eventually replaced with an animated character mesh
