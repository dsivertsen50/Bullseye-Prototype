# REQ-029 — Networked Player Statistics and Kill Tracking

## Summary

Implement a networked player-statistics system that tracks kills and deaths for each player during a multiplayer match.

This requirement should establish the foundation for a broader **Bullseye performance/statistics system**. Kills and deaths are the first statistics to be implemented, but the architecture should allow additional metrics to be added later without replacing the system.

Future Bullseye-specific metrics may include:

* Bullseye hits
* Total successful hits
* Accuracy
* Hits by body region
* Damage dealt
* Weighted hit score based on body region
* Bullseye-specific scoring
* Grenade kills
* Weapon-specific statistics
* Other metrics that distinguish Bullseye from a traditional FPS

For REQ-029, however, **only kills and deaths need to be fully implemented and displayed**.

---

## Goals

1. Track each player's kills during a multiplayer match.
2. Track each player's deaths.
3. Correctly identify which player caused a kill.
4. Support kills caused by both firearms and grenades.
5. Synchronize statistics across the network.
6. Create an extensible player-statistics structure for future Bullseye-specific metrics.
7. Provide a simple way for the player to see their current kill/death totals.

---

## Functional Requirements

### 1. Player Statistics Component

Create a reusable player statistics component/system.

Each networked player should have statistics associated with them.

At minimum, implement:

* `Kills`
* `Deaths`

These values should begin at `0` when a new match/session begins.

The statistics system should be structured so additional fields can easily be added later.

Possible future statistics include:

* `BullseyeHits`
* `HeadHits`
* `TorsoHits`
* `LowerBodyHits`
* `TotalHits`
* `ShotsFired`
* `DamageDealt`
* `WeightedHitScore`

Do **not** implement all of these metrics in this requirement. They are listed so the architecture does not assume kills are the game's primary statistic.

---

## 2. Kill Attribution

Whenever a player dies, determine which player caused the death.

Example:

* Player 1 shoots Player 2.
* Player 2's health reaches zero.
* Player 1 receives `+1 Kill`.
* Player 2 receives `+1 Death`.

The existing damage system should retain information about the attacking player so the death system can determine who should receive credit.

Kill attribution must work regardless of the weapon used.

This should include:

* Pistol
* AK/rifle
* Shotgun
* Grenade

The architecture should also support future weapons without requiring custom kill-tracking logic for every weapon.

---

## 3. Grenade Kill Attribution

Grenades must retain information about the player who threw them.

If a grenade explosion kills another player:

* The grenade owner receives the kill.
* The killed player receives the death.

Example:

Player 1 throws a grenade.

The grenade explodes and kills Player 2.

Result:

* Player 1 Kills = +1
* Player 2 Deaths = +1

The grenade itself should **not** be treated as the player responsible for the kill.

---

## 4. Self-Kills

If a player kills themselves, such as through their own grenade:

* The player receives `+1 Death`.
* The player does **not** receive `+1 Kill`.

Do not award another player a kill unless that player was actually responsible for the death.

---

## 5. Deaths Without an Attacker

The statistics system should support deaths where there is no valid attacking player.

Examples may eventually include:

* Environmental hazards
* Falling
* Map hazards
* Debug/admin death
* Other future mechanics

In these situations:

* The dying player receives `+1 Death`.
* No player receives a kill.

---

## 6. Network Authority

Kill and death statistics must be authoritative on the server/host.

Clients should not independently decide whether they earned a kill.

The authoritative death sequence should approximately follow:

1. Player receives damage.
2. Damage contains information identifying the attacker.
3. Server determines that health has reached zero.
4. Server confirms the victim.
5. Server identifies the responsible attacker, if one exists.
6. Server increments the attacker's kills.
7. Server increments the victim's deaths.
8. Updated statistics synchronize to all clients.
9. Existing death/respawn sequence continues.

The implementation should work with the project's existing Netcode for GameObjects architecture.

---

## 7. Preserve Attacker Information Through Damage

The existing damage architecture may need to be extended so that damage includes information about its source.

A damage event should be able to identify at least:

* Attacking player
* Victim
* Amount of damage
* Damage source / weapon when available

Only attacker attribution is required for the kill counter, but retaining the damage source is strongly preferred because future requirements may track:

* Weapon-specific kills
* Grenade kills
* Bullseye hits
* Damage by weapon
* Head/torso/lower-body hits

Do not unnecessarily rewrite the existing health or weapon systems. Extend them where possible.

---

## 8. Minimal Statistics HUD

Add a simple temporary statistics display to the player's HUD.

For now, display:

**Kills: X**
**Deaths: Y**

The exact visual design is not important yet.

This can be small and unobtrusive because a later requirement will create a proper scoreboard and potentially replace this UI.

The statistics displayed must belong to the **local player**, not whichever Player object happens to initialize first.

Example:

Player 1's screen:

> Kills: 4
> Deaths: 2

Player 2's screen:

> Kills: 1
> Deaths: 5

Each player must see their own statistics correctly.

---

## 9. Statistics Persist Through Respawn

Kills and deaths must **not reset when a player respawns**.

Example:

Player 1 has:

* 3 kills
* 2 deaths

Player 1 dies and respawns.

After respawning:

* Kills = 3
* Deaths = 3

The player statistics belong to the player/match session rather than the temporary alive/dead state of the character.

Statistics should only reset when appropriate for a new match/session.

---

## 10. Prepare for Bullseye-Specific Scoring

Bullseye should eventually reward more than simply securing kills.

Do not implement the final scoring system yet, but ensure the statistics architecture can support a later primary performance metric.

One possible future system might award different values based on successful hits, for example:

* Bullseye hit
* Head-area hit
* Torso hit
* Lower-body hit
* Other body-region hits

These could eventually contribute to a weighted score.

For example, the game may eventually compare players using something resembling:

**Bullseye Score + weighted hit performance + kills**

rather than treating kills as the sole measure of player performance.

REQ-029 should therefore avoid tightly coupling concepts such as:

`score = kills`

Kills should instead be stored as **one statistic among multiple possible statistics**.

---

# Architecture Preference

Prefer something conceptually similar to:

`Player`
→ `PlayerStats`
→ individual networked statistics

rather than implementing kill counters separately inside each weapon script.

Weapons and grenades should report damage/attacker information.

The health/death system should determine whether a death occurred.

The player-statistics system should record the result.

This separation will make future statistics significantly easier to implement.

---

# Multiplayer Test Scenarios

## Test 1 — Basic Firearm Kill

1. Start a multiplayer session with two players.
2. Player 1 kills Player 2 with the pistol.
3. Confirm:

   * Player 1 Kills = 1
   * Player 2 Deaths = 1
4. Allow Player 2 to respawn.
5. Confirm the statistics remain unchanged after respawn.

---

## Test 2 — Reverse Kill

After Test 1:

1. Player 2 kills Player 1.
2. Confirm:

   * Player 1: 1 Kill, 1 Death
   * Player 2: 1 Kill, 1 Death

---

## Test 3 — Grenade Kill

1. Player 1 throws a grenade.
2. Grenade kills Player 2.
3. Confirm:

   * Player 1 receives +1 Kill.
   * Player 2 receives +1 Death.

---

## Test 4 — Grenade Self-Kill

1. Player 1 throws a grenade near themselves.
2. Player 1 dies from their own grenade.
3. Confirm:

   * Player 1 receives +1 Death.
   * Player 1 does NOT receive +1 Kill.

---

## Test 5 — Client Synchronization

1. Start a host and client.
2. Have each player kill the other at least once.
3. Confirm statistics remain identical and synchronized across the network.
4. Confirm each player's local HUD displays that player's own statistics.

---

## Test 6 — Multiple Respawns

1. Player 1 kills Player 2 three times.
2. Player 2 respawns after each death.
3. Confirm:

   * Player 1 Kills = 3
   * Player 2 Deaths = 3
4. Confirm no statistics were reset during respawning.

---

# Acceptance Criteria

REQ-029 is complete when:

* [ ] Every player has a networked statistics record.
* [ ] Kills are tracked separately for each player.
* [ ] Deaths are tracked separately for each player.
* [ ] Firearm kills credit the correct attacking player.
* [ ] Grenade kills credit the player who threw the grenade.
* [ ] Self-kills do not award a kill.
* [ ] Deaths without a valid attacker do not award a kill.
* [ ] Statistics synchronize correctly between host and clients.
* [ ] Statistics survive player death and respawn.
* [ ] A minimal HUD shows the local player's kills and deaths.
* [ ] Existing shooting, damage, health, regeneration, death, and respawn functionality continues to work.
* [ ] The implementation does not equate total game score directly with kill count.
* [ ] The statistics architecture can reasonably be extended with Bullseye hits, body-region hits, damage, accuracy, and weighted scoring later.

---

# Out of Scope

The following are intentionally deferred to future requirements:

* Full multiplayer scoreboard
* Match timer
* Kill limit
* Match win condition
* Kill feed
* Final Bullseye scoring formula
* Weighted body-region scoring
* Bullseye hit statistics
* Accuracy statistics
* Weapon-specific statistics
* Persistent career statistics
* Saving statistics between game launches
* Player rankings
* Leaderboards
* XP/progression

---

# Future Direction

A later requirement should build the proper Bullseye scoring system on top of REQ-029.

The eventual goal is for **kills to remain important but secondary**.

Bullseye should ideally have a performance metric unique to the game's central mechanic—for example, rewarding direct bullseye hits or calculating a weighted performance score from where players successfully hit opponents.

REQ-029 establishes the infrastructure needed to experiment with those systems without committing to the final scoring formula yet.
