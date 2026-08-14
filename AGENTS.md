# Bullseye Prototype - Agent Instructions

## Project Purpose

This is an early multiplayer FPS prototype built in Unity using Netcode for GameObjects.

The core game concept is:
- Free-for-all multiplayer shooting.
- Each player has a visible bullseye that moves between positions on the body.
- The bullseye is the primary vulnerable hit zone.
- The purpose of the prototype is to determine whether this core mechanic is fun.

Favor simple, testable prototype implementations over production-scale architecture.

## Existing Architecture

Before changing an existing gameplay system, inspect its current implementation.

Important existing systems include:
- `Player.prefab`
- `NetworkManager`
- `PlayerMovement`
- `PlayerLook`
- `PlayerShoot`
- `PlayerHealth`
- `PlayerNetworkSetup`
- `BullseyeTarget`
- `BullseyeMover`
- `Reticle`
- Unity Input System
- Netcode for GameObjects
- `NetworkTransform`

Do not assume a system is absent merely because it is not present in the active scene hierarchy. Inspect prefabs, scripts, project assets, and NetworkManager configuration when relevant.

## General Workflow

When implementing a requirement:

1. Read the entire requirement before making changes.
2. Inspect the existing implementation and related systems.
3. Identify the smallest reasonable implementation that satisfies the requirement.
4. Preserve working behavior unless the requirement explicitly changes it.
5. Make only changes related to the requirement.
6. Allow Unity to compile after script changes.
7. Read the Unity Console through MCP after changes.
8. Fix compilation errors caused by your changes.
9. Inspect affected GameObjects, prefabs, components, or scene configuration through Unity MCP when appropriate.
10. Compare the completed implementation against every acceptance criterion.
11. Report:
   - files changed
   - Unity objects/prefabs changed
   - acceptance criteria satisfied
   - anything that still requires human playtesting
   - uncertainties or risks

Do not claim a requirement is complete if an acceptance criterion has not been verified.

## Unity MCP

Use Unity MCP when information must come from the live Unity Editor, including:
- scene hierarchy
- GameObjects
- components
- prefab/editor configuration
- Unity Console messages

Prefer inspection before mutation.

Do not make broad or unrelated Unity Editor changes.

Do not alter scenes, prefabs, assets, components, or project settings unless they are necessary for the assigned requirement.

## Multiplayer Rules

Treat multiplayer behavior as important even during prototyping.

Before modifying:
- player spawning
- ownership
- transforms
- shooting
- damage
- bullseye synchronization
- death
- respawning

inspect the existing Netcode behavior first.

Do not remove or bypass networking merely because a single-player implementation would be easier.

Clearly state when multiplayer behavior cannot be verified without running multiple clients.

## Scope Control

Do not:
- refactor unrelated working code
- rename unrelated files or GameObjects
- replace working systems without a clear reason
- introduce large frameworks for small prototype features
- add packages unless explicitly necessary
- modify generated Unity directories such as `Library`, `Temp`, or `Logs`
- delete assets or scripts unless the requirement explicitly requires it
- silently change game-design behavior outside the requirement

If you discover an unrelated issue, report it rather than fixing it unless it blocks the assigned requirement.

## Code Guidelines

Favor:
- clear C#
- small components with understandable responsibilities
- existing project patterns where reasonable
- inspector-configurable values for gameplay tuning
- simple prototype solutions that can be changed easily

Avoid unnecessary abstraction and premature optimization.

## Validation

Compilation success is necessary but not sufficient.

After implementation:
- check the Unity Console
- inspect relevant Unity configuration
- verify behavior that can be verified programmatically
- identify anything requiring human playtesting

Game-feel questions must be left to human playtesting rather than assumed to be successful.

## Git Safety

Do not commit, push, merge, reset, rebase, or change branches unless explicitly instructed.

The user controls Git history.

Do not discard existing uncommitted work.