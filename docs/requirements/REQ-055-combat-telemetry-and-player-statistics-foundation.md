# REQ-055 — Combat Telemetry & Player Statistics Foundation

## Summary

Create an extensible, authoritative combat telemetry system for Bullseye that records how combat events occur rather than relying only on simple scoreboard counters.

As part of this ticket:

- Rename **Kills** to **Eliminations** throughout the game.
- Continue tracking player eliminations and deaths.
- Add assists.
- Track eliminations by weapon and combat source.
- Track whether an eliminated bullseye was attached to or detached from the player.
- Track bullseye detachments and the mechanic that caused them.
- Track firearm elimination distance.
- Track shots fired and relevant hit statistics.
- Build the system as a reusable telemetry/event foundation that future systems can consume.

This ticket is primarily about establishing the **combat telemetry architecture and per-match statistics**. It is **not** yet responsible for uploading research data, creating an external analytics backend, or persisting lifetime career statistics.

---

# Goals

1. Replace existing "Kills" terminology with **"Eliminations."**
2. Build one authoritative combat-event tracking system.
3. Record enough context about each combat event to reconstruct how an elimination happened.
4. Support Bullseye-specific mechanics such as:
   - Attached bullseye eliminations.
   - Detached bullseye eliminations.
   - Combustion grenade detachments.
   - Magnetism grenade detachments.
   - Body Slam eliminations.
5. Add assists based on unresolved bullseye damage.
6. Record firearm elimination distance.
7. Track basic shooting statistics such as shots fired and bullseye hits.
8. Keep the architecture extensible for:
   - Match summaries.
   - Weapon balancing.
   - Persistent player statistics.
   - Steam statistics/achievements.
   - Future opt-in research telemetry.

---

# Non-Goals

REQ-055 should **not** attempt to implement:

- Cloud telemetry.
- Research-data uploads.
- External databases.
- Player accounts.
- Lifetime/career statistics.
- Steam achievements.
- Steam leaderboards.
- A complete post-match statistics UI.
- Privacy/consent flows for research.
- Advanced analytics dashboards.

Those systems should consume the telemetry foundation created here later.

---

# 1. Rename "Kills" to "Eliminations"

Replace player-facing usage of the term:

> Kill / Kills

with:

> Elimination / Eliminations

This applies anywhere the existing statistic is shown, including where applicable:

- HUD.
- Scoreboard.
- Match statistics.
- Debug displays.
- Player-stat data structures.
- Networking messages.
- Existing gameplay scripts.

Internal code should also be renamed where doing so is reasonably safe.

Examples:

```csharp
kills
killCount
OnKill
AddKill()
```

should preferably become equivalents such as:

```csharp
eliminations
eliminationCount
OnElimination
AddElimination()
```

Do not perform dangerous unrelated refactors merely to rename terminology.

---

# 2. General Telemetry Architecture

Do **not** implement every statistic as an independent counter scattered throughout weapon and player scripts.

Create a centralized/event-driven telemetry system.

The desired conceptual flow is:

```text
Gameplay Event
    ↓
Authoritative gameplay system determines result
    ↓
CombatTelemetryManager
    ↓
Combat telemetry event recorded
    ↓
Per-player match statistics updated
```

Possible naming is flexible, but an architecture similar to the following is encouraged:

```text
CombatTelemetryManager
CombatEvent
EliminationEvent
BullseyeDamageEvent
BullseyeDetachEvent
ShotEvent
PlayerMatchStats
```

Cursor should inspect the existing architecture first and integrate this cleanly rather than duplicating systems that already exist.

---

# 3. Multiplayer Authority

Combat statistics must be recorded from the **authoritative multiplayer gameplay result**.

Do not allow every connected client to independently increment statistics from local collision or visual events.

For example:

```text
Client sees bullet hit
    ↓
Server/authoritative combat logic validates damage
    ↓
Bullseye breaks
    ↓
Server records ONE elimination event
```

The same rule applies to:

- Eliminations.
- Assists.
- Bullseye detachments.
- Body Slam eliminations.
- Shots/hits where authoritative confirmation is needed.

The architecture must avoid duplicated telemetry events.

---

# 4. Player Match Statistics

Each player should have an authoritative per-match statistics record.

At minimum, support:

```text
Eliminations
Deaths
Assists
```

Also support the following derived or directly tracked statistics:

```text
Eliminations by Weapon
Eliminations by Damage Source
Attached Bullseye Eliminations
Detached Bullseye Eliminations
Body Slam Eliminations
Bullseyes Detached
Bullseyes Detached by Detachment Method
Shots Fired
Bullseye Hits
```

Where practical, also retain sufficient raw information to derive:

```text
Average Elimination Distance
Longest Elimination Distance
Average Elimination Distance by Weapon
Bullseye Hit Percentage
```

Not every statistic needs to be displayed yet.

---

# 5. Elimination Events

Whenever a player is eliminated, record an `EliminationEvent` or equivalent.

At minimum, the event should contain:

```text
Eliminating Player
Eliminated Player
Combat Source
Weapon
Bullseye State
Elimination Distance
Match Timestamp
```

Suggested conceptual structure:

```csharp
EliminationEvent
{
    attackerPlayerId
    victimPlayerId
    sourceType
    weaponId
    bullseyeState
    distance
    matchTime
}
```

Exact implementation and data types should follow existing project conventions.

---

# 6. Elimination Source

The telemetry system must identify **how** the elimination occurred.

Create an extensible category rather than hardcoding several unrelated booleans.

Example:

```csharp
public enum EliminationSourceType
{
    Firearm,
    BodySlam,
    Grenade,
    Environmental,
    Other
}
```

Only currently implemented mechanics need to actively generate events.

The enum/data structure should be easy to extend later.

---

# 7. Weapon-Specific Eliminations

For firearm eliminations, record the specific weapon used.

The system should not rely on manually creating a new telemetry variable for every weapon.

For example, avoid:

```csharp
pistolKills
akKills
dmrKills
shotgunKills
```

Prefer something extensible such as:

```text
Weapon ID → Elimination Count
```

or events from which weapon totals can be calculated.

Existing weapons currently include examples such as:

- Pistol.
- Rifle/AK.
- Shotgun.
- DMR.

Future weapons should automatically work with the same system once they have a valid weapon identifier/definition.

---

# 8. Attached vs. Detached Bullseye Eliminations

Every bullseye elimination must record whether the bullseye was:

```text
Attached
```

or:

```text
Detached
```

at the moment it was destroyed.

This must allow statistics such as:

```text
Attached Bullseye Eliminations: 5
Detached Bullseye Eliminations: 2
```

The status must reflect the actual authoritative bullseye state at elimination time.

Do not infer it later based only on grenade use.

---

# 9. Bullseye Detachment Events

Bullseye detachment is its own meaningful telemetry event.

Whenever a bullseye becomes physically detached from a player, record a `BullseyeDetachEvent` or equivalent.

At minimum, record:

```text
Player Whose Bullseye Was Detached
Player Responsible for Detachment
Detachment Method
Match Timestamp
```

Where useful, also record:

```text
Bullseye World Position
```

---

# 10. Detachment Method

The telemetry architecture must distinguish how a bullseye was detached.

At minimum, accommodate:

```text
Combustion Grenade
Magnetism Grenade
Other/Future Mechanic
```

Suggested structure:

```csharp
public enum BullseyeDetachMethod
{
    CombustionGrenade,
    MagnetismGrenade,
    Other
}
```

This should support statistics such as:

```text
Total Bullseyes Detached
Bullseyes Detached via Combustion Grenade
Bullseyes Detached via Magnetism Grenade
```

---

# 11. Detachment Attribution

Record the player who caused the bullseye detachment separately from the player who eventually destroys the bullseye.

Example:

```text
Player A throws Magnetism Grenade.

Player B's bullseye is detached.

Player C shoots the detached bullseye with a DMR.
```

Telemetry should preserve:

```text
Player A caused the detachment.
Magnetism Grenade caused the detachment.
Player C earned the elimination.
DMR caused the elimination.
Bullseye was detached when eliminated.
```

Do not assume the player causing the detachment must also receive the elimination.

---

# 12. Assists

Add an **Assist** statistic.

Bullseye assists should initially use a game-specific rule based on unresolved bullseye damage rather than an arbitrary FPS time window.

## Assist Rule

A player qualifies for an assist when:

1. They damage an enemy player's bullseye.
2. That damage has **not fully regenerated/reset**.
3. Another player delivers the final bullseye-breaking hit.

Example:

```text
Player A shoots Player B's bullseye.

Bullseye health:
8 → 4

Before the bullseye heals:

Player C shoots the bullseye.

Bullseye health:
4 → 0
```

Result:

```text
Player C:
+1 Elimination

Player A:
+1 Assist

Player B:
+1 Death
```

---

# 13. Assist Reset

If the damaged bullseye fully regenerates before another player eliminates it, previous attackers should no longer qualify for an assist.

Example:

```text
Player A damages Player B.

Player B's bullseye completely heals.

Player C later eliminates Player B.
```

Result:

```text
Player C:
+1 Elimination

Player A:
No Assist
```

The assist state should therefore reset when bullseye health returns to the appropriate full/neutral state.

---

# 14. Multiple Assists

If multiple different players contribute unresolved bullseye damage before an elimination, all qualifying attackers may receive an assist.

Example:

```text
Player A damages Player D.
Player B damages Player D.
Player C lands the final shot.
```

If Player D's bullseye never fully regenerated during that sequence:

```text
Player C:
+1 Elimination

Player A:
+1 Assist

Player B:
+1 Assist
```

The eliminating player must never receive an assist for their own elimination.

Duplicate assist credit to the same player for the same elimination must not occur.

---

# 15. Assist Attribution Data

Maintain a set/list of qualifying attackers associated with the victim's current unresolved bullseye damage state.

Conceptually:

```text
Victim Player
    ↓
Current unresolved bullseye damage contributors
    ↓
Player A
Player B
```

When the bullseye returns to its neutral/full-health state:

```text
Clear assist contributors
```

When the victim is eliminated:

```text
Award assists
Clear assist contributors
```

Also clear stale assist state on:

- Death.
- Respawn.
- Match reset.

---

# 16. Body Slam Eliminations

Body Slam must be represented as its own elimination source.

A Body Slam elimination should:

```text
Increment Eliminations
Increment Body Slam Eliminations
Increment victim Deaths
Generate an EliminationEvent
```

It should **not** be attributed to whichever firearm the player happens to be holding.

Example:

```text
Source = BodySlam
Weapon = None
```

or the equivalent implementation.

---

# 17. Elimination Distance

For firearm eliminations, record the distance of the lethal shot.

Use the world-space distance between the attacker/shooter and the bullseye at the moment the eliminating shot is resolved.

For example:

```csharp
float distance = Vector3.Distance(
    attackerPosition,
    bullseyePosition
);
```

Use whichever transforms most accurately represent the shot origin and hit target in the existing architecture.

The stored value should use Unity's normal world-unit scale, assumed to represent meters unless the project currently defines otherwise.

---

# 18. Detached Bullseye Distance

If the bullseye is detached and lying/moving elsewhere in the world, elimination distance must be measured to the **actual detached bullseye position**.

Do not measure distance to the eliminated player's character body.

Example:

```text
Player body is 20 m away.

Detached bullseye has rolled to 13 m away.

Player shoots bullseye.
```

Recorded elimination distance:

```text
~13 m
```

---

# 19. Distance Statistics

Preserve the individual distance of every firearm elimination.

Do not store only an average.

This should allow later calculation of:

```text
Average firearm elimination distance
Median firearm elimination distance
Longest elimination
Shortest elimination
Average distance by weapon
Longest elimination by weapon
Attached bullseye elimination distance
Detached bullseye elimination distance
```

REQ-055 does not need to implement all of these UI calculations.

The important requirement is preserving the underlying values.

---

# 20. Shots Fired

Record firearm shots fired.

A shot should be counted when the weapon actually fires a projectile/hitscan shot.

Do not count:

- Trigger pulls during reload.
- Trigger pulls with no available ammunition.
- Inputs rejected because the weapon cannot fire.
- Purely visual effects.

For a shotgun, define:

```text
1 trigger pull = 1 shot fired
```

Do not count each pellet as a separate shot fired.

---

# 21. Bullseye Hits

Track confirmed bullseye hits.

A hit should only be recorded when authoritative hit/damage logic confirms the shot actually struck a valid enemy bullseye.

This should later permit:

```text
Bullseye Hit Percentage =
Bullseye Hits / Shots Fired
```

No scoreboard display is required yet.

---

# 22. Optional General Hit Tracking

If straightforward within the existing architecture, the telemetry system may also distinguish:

```text
Bullseye Hit
Body Hit
Miss
```

However, do not substantially expand scope if body-hit tracking would require major combat-system changes.

Bullseye hits and shots fired are the priority for REQ-055.

---

# 23. Shotgun Handling

Shotgun telemetry needs special consideration because one trigger pull creates multiple pellet traces.

Requirements:

```text
1 shotgun trigger pull = 1 Shot Fired
```

For hit statistics:

- A trigger pull that lands one or more pellets on a valid bullseye should count as a bullseye hit event.
- Do not inflate firearm accuracy by counting every pellet as an independent shot.
- Pellet-level telemetry may be preserved internally if useful, but player-facing shot accuracy should remain trigger-pull based.

If a shotgun blast breaks the bullseye:

```text
Weapon = Shotgun
Elimination Distance = distance from shooter to bullseye
```

---

# 24. Match Timestamp

Combat events should include the time at which they occurred within the match.

This can be:

```text
Elapsed match seconds
```

or another stable match-time representation.

Example:

```text
143.72 seconds
```

The value does not need to be shown to the player.

It exists so event sequences can later be reconstructed.

---

# 25. Event History

Where reasonable, maintain a match-level list of combat telemetry events.

Example:

```text
12.6s — Player 2 fired DMR
12.7s — Player 2 hit Player 4 bullseye
18.1s — Player 3 used Magnetism Grenade
18.3s — Player 4 bullseye detached
20.8s — Player 2 destroyed Player 4 detached bullseye
```

This event history is primarily for telemetry/debugging/future analysis.

Do not build a player-facing combat log UI unless one already exists and integration is trivial.

---

# 26. Suggested Event Types

The implementation does not have to use this exact hierarchy, but the system should conceptually support events such as:

```text
ShotFired
BullseyeHit
BullseyeDamaged
BullseyeDetached
Elimination
AssistAwarded
PlayerDeath
```

Avoid tightly coupling the telemetry manager to every weapon prefab.

Prefer gameplay systems raising well-defined events.

---

# 27. Suggested Core Data

A possible structure is:

```csharp
public enum CombatEventType
{
    ShotFired,
    BullseyeHit,
    BullseyeDamaged,
    BullseyeDetached,
    Elimination,
    AssistAwarded
}
```

Additional enums might include:

```csharp
public enum BullseyeState
{
    Attached,
    Detached
}
```

```csharp
public enum EliminationSourceType
{
    Firearm,
    BodySlam,
    Grenade,
    Environmental,
    Other
}
```

```csharp
public enum BullseyeDetachMethod
{
    CombustionGrenade,
    MagnetismGrenade,
    Other
}
```

Cursor may choose better structures based on the current codebase.

These examples describe intent, not mandatory implementation.

---

# 28. Weapon Identification

Prefer using an existing stable weapon definition/ID if one already exists.

Do not identify telemetry weapons by:

```text
GameObject instance name
Prefab clone name
UI display string
```

if a proper weapon identifier is available.

The telemetry system should be robust against:

```text
"DMR(Clone)"
```

versus:

```text
"DMR"
```

or renamed instantiated GameObjects.

---

# 29. Player Identification

Use the existing multiplayer player/client/network identifier system.

Telemetry should not rely on player display names as unique identifiers.

Example conceptual fields:

```text
attackerClientId
victimClientId
```

or whatever player identity abstraction already exists.

---

# 30. Death Tracking

Every valid player elimination should increment the eliminated player's death count exactly once.

Examples:

```text
Firearm bullseye elimination → +1 Death
Detached bullseye elimination → +1 Death
Body Slam elimination → +1 Death
```

Avoid duplicate death increments from:

- Bullseye destruction.
- Player freeze/death state.
- Respawn system.
- Elimination event.

There should be one authoritative source of truth.

---

# 31. Existing Scoreboard Integration

The existing scoreboard should continue to function.

At minimum, update it from:

```text
Kills
Deaths
```

to:

```text
Eliminations
Deaths
```

If Assists can be added cleanly within the existing layout, update it to:

```text
Eliminations
Assists
Deaths
```

Do not substantially redesign the scoreboard UI in this ticket.

The telemetry architecture is more important than visual polish.

---

# 32. Debug Telemetry View

Add a lightweight debugging method so telemetry can be tested without needing a future analytics backend.

This may be:

- Inspector values.
- Debug log output.
- A development-only telemetry panel.
- An existing debug UI.

At minimum, during development we should be able to verify values such as:

```text
Eliminations
Deaths
Assists
Weapon Eliminations
Attached Bullseye Eliminations
Detached Bullseye Eliminations
Bullseyes Detached
Body Slam Eliminations
Shots Fired
Bullseye Hits
Elimination Distances
```

Do not create an elaborate production UI solely for debugging.

---

# 33. Match Reset

All match-scoped telemetry must reset correctly when a new match begins.

Clear:

```text
Player match stats
Event history
Assist contributors
Elimination distances
Weapon-specific match counters
Bullseye detachment counters
```

Respawning within the same match should **not** reset accumulated match statistics.

---

# 34. Death/Respawn State Cleanup

When a player dies:

- Resolve assists.
- Clear unresolved damage contributors.
- Clear any victim-specific temporary telemetry state.

When the player respawns:

- Their accumulated match statistics remain.
- Temporary bullseye/damage contribution state starts clean.

---

# 35. No Research Data Upload Yet

Although this telemetry is intentionally useful for future gameplay research, REQ-055 must keep the data local to the current game/match infrastructure.

Do not:

```text
Send HTTP telemetry
Upload to a server
Send data to Unity Analytics
Create cloud storage
Create external player IDs
Collect personally identifying information
```

Future tickets will handle research consent, privacy, persistence, and external telemetry if desired.

---

# 36. Extensibility Requirement

A major success criterion is that adding a future weapon or mechanic should not require rewriting the telemetry architecture.

For example, adding a future sniper rifle should ideally require only that the rifle already expose its weapon definition/ID.

The telemetry system should automatically be able to record:

```text
Sniper Rifle
Elimination
41.7 m
Detached Bullseye
```

without creating an entirely new `SniperKillTracker`.

Similarly, a future detachment mechanic should be able to add a new detachment type cleanly.

---

# 37. Preserve Rich Data, Derive Summaries Later

When deciding between:

```text
Only store aggregate count
```

and:

```text
Store event/detail that allows aggregate count to be calculated
```

prefer preserving useful event detail where reasonably lightweight.

For example, instead of storing only:

```text
Average DMR elimination distance = 18.4
```

store:

```text
12.2
16.8
26.2
```

and calculate the average later.

Do not over-engineer this into a database system.

A simple in-memory event/history structure is sufficient for REQ-055.

---

# 38. Performance

Telemetry must not meaningfully affect combat performance.

Avoid:

- Expensive object searches every shot.
- Repeated scene-wide queries.
- Excessive garbage allocation in per-frame code.
- Writing files to disk for every shot.
- Network RPC spam for statistics that the server already knows.

Telemetry should be event-driven.

---

# 39. Testing Requirements

Test REQ-055 with at least two networked players where applicable.

---

## Test A — Terminology

1. Start a match.
2. Open any UI previously displaying `Kills`.

Expected:

```text
"Eliminations" is shown instead.
```

---

## Test B — Standard Firearm Elimination

1. Player A shoots Player B's attached bullseye until it breaks.

Expected:

```text
Player A Eliminations +1
Player B Deaths +1
Attached Bullseye Eliminations +1
Correct weapon receives +1 elimination
Detached Bullseye Eliminations unchanged
```

An elimination event is recorded exactly once.

---

## Test C — Weapon Attribution

1. Player A earns one elimination with Pistol.
2. Player A earns one elimination with DMR.

Expected:

```text
Total Eliminations = 2

Pistol Eliminations = 1
DMR Eliminations = 1
```

---

## Test D — Detached Bullseye Elimination

1. Detach Player B's bullseye.
2. Player A shoots the detached bullseye.

Expected:

```text
Player A Eliminations +1
Player B Deaths +1
Detached Bullseye Eliminations +1
Attached Bullseye Eliminations unchanged
```

The elimination event records:

```text
Bullseye State = Detached
```

---

## Test E — Detachment Attribution

1. Player A throws Magnetism Grenade.
2. Player B's bullseye is detached.
3. Player C destroys it.

Expected:

```text
Player A:
Bullseyes Detached +1

Detachment Method:
Magnetism Grenade

Player C:
Eliminations +1

Elimination Bullseye State:
Detached
```

Player A should not automatically receive the elimination.

---

## Test F — Combustion vs Magnetism

Detach bullseyes once with each grenade type.

Expected:

```text
Total Bullseyes Detached = 2

Combustion Detachments = 1
Magnetism Detachments = 1
```

---

## Test G — Assist

1. Player A damages Player B's bullseye.
2. Before it regenerates, Player C destroys Player B's bullseye.

Expected:

```text
Player C Eliminations +1
Player A Assists +1
Player B Deaths +1
```

---

## Test H — Assist Expiration

1. Player A damages Player B.
2. Allow Player B's bullseye to completely regenerate.
3. Player C then eliminates Player B.

Expected:

```text
Player C Eliminations +1
Player A Assists unchanged
```

---

## Test I — Multiple Assists

1. Player A damages Player D.
2. Player B damages Player D.
3. Player C delivers final hit before regeneration.

Expected:

```text
Player A Assists +1
Player B Assists +1
Player C Eliminations +1
```

---

## Test J — No Self Assist

Player A contributes damage and also eventually delivers the final hit.

Expected:

```text
Player A Eliminations +1
Player A does NOT receive an Assist for the same elimination.
```

---

## Test K — Body Slam

1. Player A eliminates Player B through Body Slam.

Expected:

```text
Player A Eliminations +1
Player A Body Slam Eliminations +1
Player B Deaths +1
```

No firearm should receive elimination credit.

---

## Test L — Elimination Distance

1. Player A earns a firearm elimination.
2. Inspect telemetry.

Expected:

```text
A reasonable distance value is stored.
```

Repeat at a visibly larger distance.

Expected:

```text
Second elimination records a larger distance.
```

---

## Test M — Detached Bullseye Distance

1. Detach Player B's bullseye.
2. Move/allow the bullseye to move away from Player B.
3. Player A destroys the bullseye.

Expected:

Distance is measured to the detached bullseye location rather than Player B's body.

---

## Test N — Shots Fired

Fire:

```text
5 Pistol rounds
3 DMR rounds
2 Shotgun blasts
```

Expected:

```text
Shots Fired = 10
```

Shotgun pellets must not inflate this value.

---

## Test O — Bullseye Hits

1. Fire several misses.
2. Fire several confirmed bullseye hits.

Expected:

```text
Shots Fired counts all valid shots.
Bullseye Hits counts only confirmed bullseye hits.
```

---

## Test P — No Duplicate Network Events

Perform one elimination while testing with multiple connected clients.

Expected:

```text
Exactly one elimination is recorded.
Exactly one death is recorded.
```

There should not be one telemetry entry per connected client.

---

## Test Q — Respawn

1. Earn an elimination.
2. Die.
3. Respawn.

Expected:

Accumulated match statistics remain intact.

Temporary assist/damage attribution state is cleared.

---

## Test R — New Match

End/restart the match.

Expected:

All match statistics and match event history reset cleanly.

---

# 40. Acceptance Criteria

REQ-055 is complete when:

- [ ] All player-facing `Kill/Kills` terminology has been changed to `Elimination/Eliminations` where appropriate.
- [ ] Existing elimination and death tracking still works.
- [ ] Combat telemetry is centralized/event-driven rather than distributed as unrelated counters.
- [ ] Multiplayer authority prevents duplicate telemetry events.
- [ ] Per-player match statistics exist.
- [ ] Eliminations can be attributed to specific weapons.
- [ ] Body Slam eliminations are tracked separately.
- [ ] Attached bullseye eliminations are tracked.
- [ ] Detached bullseye eliminations are tracked.
- [ ] Bullseye detachments are tracked separately from eliminations.
- [ ] Combustion and Magnetism grenade detachments can be distinguished.
- [ ] The player responsible for detachment is recorded.
- [ ] The player responsible for the eventual elimination is independently recorded.
- [ ] Assists are awarded for unresolved bullseye damage.
- [ ] Assist eligibility clears after complete health regeneration.
- [ ] Multiple valid contributors can receive assists.
- [ ] Eliminating players cannot assist themselves on the same elimination.
- [ ] Firearm elimination distance is recorded.
- [ ] Detached bullseye elimination distance uses the actual detached bullseye location.
- [ ] Individual elimination distances are preserved.
- [ ] Shots fired are tracked.
- [ ] Shotgun trigger pulls count as one shot rather than one shot per pellet.
- [ ] Bullseye hits are tracked.
- [ ] Match timestamps are available on relevant combat events.
- [ ] Statistics reset between matches.
- [ ] Statistics persist through ordinary respawns within a match.
- [ ] Debugging tools allow us to verify the telemetry.
- [ ] No external telemetry upload/backend has been added.
- [ ] The system is extensible enough for future weapons and combat mechanics.

---

# 41. Implementation Guidance for Cursor

Before making changes:

1. Inspect the current elimination/death tracking.
2. Inspect the bullseye health/break logic.
3. Inspect grenade detachment logic.
4. Inspect Body Slam damage/elimination logic.
5. Inspect weapon definitions.
6. Inspect weapon firing and hit confirmation.
7. Inspect the scoreboard.
8. Identify which gameplay objects currently have server authority.

Do not create duplicate combat logic merely for telemetry.

Where possible, hook telemetry into the point at which gameplay outcomes are already authoritatively confirmed.

The telemetry system should **observe and record gameplay outcomes**, not become a second independent damage system.

---

# 42. Future Compatibility

This ticket should leave us in a position where later requirements can build systems such as:

```text
Match Results Screen
Lifetime Player Statistics
Weapon Performance Statistics
Longest Elimination Records
Steam Achievements
Steam Statistics
Heatmaps
Combat Sequence Analysis
Balance Analytics
Opt-In Research Telemetry
```

without replacing the telemetry architecture created in REQ-055.

---

# Final Expected Result

After REQ-055, Bullseye should no longer think of combat performance as merely:

```text
Kills: 5
Deaths: 3
```

Instead, the game should have a structured record capable of understanding events such as:

```text
Player A used a Magnetism Grenade to detach Player B's bullseye.

Player C then destroyed that detached bullseye with a DMR from 21.4 meters away.

Player C received an Elimination.

Player B received a Death.

Player A received credit for a Magnetism Grenade bullseye detachment.

A different player who had damaged Player B's bullseye before the elimination received an Assist.
```

This combat-event foundation should become the authoritative source for Bullseye's future match statistics, balancing analytics, and optional research telemetry.