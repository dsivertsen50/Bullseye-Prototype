# REQ-010 — Independent Per-Player Bullseye Randomization

## Summary

Ensure that each spawned player has an independently randomized bullseye movement sequence.

Players may continue using the same shared player prefab, but the runtime bullseye state and randomization behavior must be unique to each spawned player instance.

The current behavior appears to allow multiple players' bullseyes to move in sync or follow the same random movement pattern. This should be changed so that one player's bullseye movement does not determine or predict another player's bullseye movement.

---

## Design Intent

The moving bullseye is the central vulnerability mechanic of Bullseye.

Each player should present a separate and unpredictable aiming challenge.

For example:

```text
Player 1 bullseye:
Left torso → shoulder → abdomen → back

Player 2 bullseye:
Right hip → chest → lower back → shoulder
```

The movement timing and destination of Player 1's bullseye should not imply where Player 2's bullseye will move.

Using the same prefab is expected and desirable. The requirement is that the **runtime state of each prefab instance is independent**.

---

## Goals

* Give every spawned player an independently moving bullseye.
* Prevent bullseye movement sequences from being synchronized between players.
* Preserve authoritative multiplayer synchronization.
* Ensure all clients see the same position for a particular player's bullseye.
* Keep the system compatible with future additions such as player-influenced bullseye movement.

---

## Functional Requirements

### 1. Independent Bullseye State

Each spawned player instance must maintain its own bullseye movement state.

This includes, as applicable:

* Current bullseye position.
* Current target/destination position.
* Random movement timer.
* Time until next random movement.
* Randomly selected surface/location.
* Interpolation or movement progress.
* Any state used to determine the next random destination.

No bullseye runtime state should be unintentionally shared between different player instances.

---

### 2. Independent Random Selection

Whenever a player's bullseye chooses a new random destination, that selection must be made independently for that player.

Example:

```text
Player 1 random movement event
→ chooses upper-left torso

Player 2 random movement event
→ independently chooses right hip
```

Player 2 should not receive the same destination merely because Player 1 generated it.

The system should not rely on a globally reseeded random generator in a manner that causes identical player instances to produce identical sequences.

---

### 3. Independent Movement Timing

Bullseye movement timing should also be independently determined where random timing is currently supported.

Players should not consistently move their bullseyes at exactly the same moment simply because they spawned from the same prefab.

If the existing system uses a fixed interval rather than a randomized interval, it is acceptable for the interval itself to remain fixed for this ticket, but players should begin their movement cycles independently enough that their bullseyes do not remain visually synchronized.

A small randomized initial delay is acceptable if necessary.

---

## Multiplayer Authority

The bullseye represents an actual gameplay vulnerability, so its position must remain authoritative and network-consistent.

The system should follow this principle:

```text
Authoritative instance determines:
    Player A's next bullseye destination

        ↓

Bullseye moves

        ↓

That bullseye state is synchronized to all clients
```

Each client must agree on where Player A's bullseye is at any given gameplay-relevant moment.

Clients should **not independently randomize another player's bullseye locally**, because this could result in clients seeing different vulnerable locations.

---

## Expected Networking Behavior

For two players:

### Player 1

The authoritative game logic selects Player 1's bullseye movement.

Both clients should see:

```text
Player 1 bullseye → same position
```

### Player 2

The authoritative game logic separately selects Player 2's bullseye movement.

Both clients should see:

```text
Player 2 bullseye → same position
```

However:

```text
Player 1 bullseye movement
≠
Player 2 bullseye movement
```

except for occasional coincidence caused naturally by random selection.

---

## Random Seed / Random State Requirements

Review the existing bullseye randomization implementation for anything that could cause spawned players to share identical random sequences.

Potential causes may include:

* Giving every player the same explicit random seed.
* Reinitializing a random-number generator with the same value on spawn.
* Using static/shared bullseye movement state.
* Sharing timers between player instances.
* Running identical deterministic sequences starting from identical state.
* Network synchronization unintentionally copying one player's bullseye state to all players.

The implementation should ensure each player has independent randomization without sacrificing deterministic network authority where required.

The exact technical solution is left to the implementation agent.

---

## Shared Prefab Behavior

Continue using the same player prefab for all players.

Do **not** create separate prefabs such as:

```text
Player1Prefab
Player2Prefab
```

solely to solve this issue.

Each instantiated copy of the player prefab should naturally create and maintain its own bullseye state.

The desired structure is:

```text
Player Prefab
    └── Bullseye Controller

Spawn Player A
    └── Bullseye Controller A

Spawn Player B
    └── Bullseye Controller B
```

Controller A and Controller B should be separate runtime instances.

---

## Compatibility With Existing Bullseye Mechanics

Independent randomization must continue to work with the bullseye mechanics already implemented.

This includes, where applicable:

* Random movement across the player's valid surface.
* Smooth movement between positions.
* Jump-driven bullseye movement.
* Crouch-driven bullseye movement.
* Turn-driven/delayed bullseye movement.
* Bullseye hit detection.
* Damage and death.
* Respawn.

Player-controlled influences should affect only that player's bullseye.

For example:

```text
Player 1 jumps
→ Player 1's bullseye reacts

Player 2's bullseye
→ unaffected
```

---

## Respawn Behavior

When a player dies and respawns:

* Their bullseye system should resume functioning normally.
* Their randomization should remain independent from all other players.
* Respawning two players should not cause their movement cycles to become synchronized.
* The bullseye should return to a valid state and location according to the existing respawn/bullseye rules.

---

## Out of Scope

The following are outside the scope of this requirement:

* Changing the valid bullseye surface.
* Changing bullseye size.
* Changing bullseye movement speed.
* Adding new movement behaviors.
* Changing the probability of different body regions.
* Different randomization behavior for different players.
* Player-selected bullseye locations.
* Weapon changes.
* Damage balancing.
* Visual differences between player characters.
* Team colors or player skins.

This ticket is specifically concerned with **independent per-player bullseye randomization**.

---

## Acceptance Criteria

* [ ] Two spawned players can use the same player prefab.
* [ ] Each player has an independent bullseye controller/state at runtime.
* [ ] Player 1 and Player 2 do not consistently choose the same random bullseye destinations.
* [ ] Player 1 and Player 2 do not remain consistently synchronized in their movement timing.
* [ ] A random movement event for Player 1 does not cause Player 2's bullseye to move.
* [ ] Player-specific actions affecting the bullseye affect only that player's bullseye.
* [ ] All connected clients see Player 1's bullseye in the same location.
* [ ] All connected clients see Player 2's bullseye in the same location.
* [ ] The authoritative game state determines gameplay-relevant bullseye positions.
* [ ] Bullseye hit detection continues to work correctly.
* [ ] Death and respawn continue to work correctly.
* [ ] Bullseyes remain independently randomized after respawn.
* [ ] Existing multiplayer functionality is not broken.

---

## Testing Procedure

### Test 1 — Initial Spawn

Start a multiplayer game with two players.

Observe both bullseyes for several movement cycles.

Expected:

* They should not repeatedly move to identical positions.
* They should not appear to follow an identical sequence.

---

### Test 2 — Movement Timing

Observe when each player's bullseye begins random movements.

Expected:

* Players should not remain permanently synchronized.
* It is acceptable for movement events to occasionally occur near the same time by chance.

---

### Test 3 — Cross-Client Consistency

Observe Player 1 from both multiplayer clients.

Expected:

* Both clients see Player 1's bullseye in the same position.

Repeat for Player 2.

---

### Test 4 — Player-Controlled Influence

Have Player 1 jump, crouch, or turn in a way that affects their bullseye.

Expected:

* Player 1's bullseye responds according to the existing mechanic.
* Player 2's bullseye is unaffected.

Repeat using Player 2.

---

### Test 5 — Death and Respawn

Kill Player 1 and allow them to respawn.

Expected:

* Player 1's bullseye continues random movement.
* Player 1 does not become synchronized with Player 2.

Repeat for Player 2.

---

### Test 6 — Extended Observation

Allow both players to remain alive for several minutes.

Expected:

The bullseyes should behave as two independent systems rather than two copies following the same movement sequence.

---

## Prototype Design Intent

An opponent's bullseye should be unpredictable based on **that opponent's own movement and random bullseye state**.

Observing one player's bullseye should provide no useful information about where another player's bullseye is about to move.

The desired experience is:

**Every opponent presents their own continuously changing aiming problem.**
