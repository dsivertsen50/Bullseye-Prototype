# REQ-071 — Ricochet Telemetry and Combat Feedback Integration

## Summary

Extend the existing ricochet, telemetry, and combat-feedback systems so that damage and eliminations caused by ricocheted shots are explicitly identified, recorded, and displayed to the player.

A ricocheted shot should not become indistinguishable from a normal direct shot after it bounces.

The system must preserve ricochet attribution through:

```text
Shot Fired
    ↓
Ricochet Occurs
    ↓
Projectile/Bullet Continues
    ↓
Damage Occurs
    ↓
Possible Elimination
    ↓
Telemetry Records Ricochet Context
    ↓
REQ-070 Combat Feedback Displays Ricochet Award
```

This requirement should build on the existing ricochet functionality from REQ-052, telemetry work from REQ-055/REQ-061, and combat-feedback system from REQ-070.

---

# 1. Goals

REQ-071 should accomplish four primary goals:

1. Identify when damage was caused by a ricocheted shot.
2. Record ricochet-related information in telemetry.
3. Identify eliminations caused by ricocheted shots.
4. Display ricochet-related feedback through the REQ-070 combat-feedback system.

The HUD should consume authoritative gameplay information rather than independently trying to infer whether a shot ricocheted.

---

# 2. Preserve Ricochet State

When a shot ricochets off a surface using the REQ-052 ricochet system, that shot must retain information indicating that it has ricocheted.

At minimum, preserve:

```text
hasRicocheted
ricochetCount
```

Conceptually:

```csharp
bool HasRicocheted;
int RicochetCount;
```

If bullets are represented by raycasts rather than persistent projectile objects, equivalent ricochet information must be passed forward through the hit/damage context.

Do not rely on the damage system later trying to reconstruct whether a ricochet occurred.

---

# 3. Ricochet Count

Track the number of times a shot has ricocheted.

Examples:

```text
Direct shot:
ricochetCount = 0
```

```text
One wall bounce:
ricochetCount = 1
```

```text
Two wall bounces:
ricochetCount = 2
```

This should work even if current gameplay normally allows only one ricochet.

The architecture should not assume that:

```text
ricochetCount <= 1
```

unless the existing ricochet system intentionally enforces that restriction.

---

# 4. Ricochet Damage Telemetry

Whenever a ricocheted shot successfully damages another player or their Bullseye, record that damage event as ricochet damage.

Telemetry should include, where appropriate:

```text
was_ricochet
ricochet_count
damage_amount
weapon_id
attacker_player_id
victim_player_id
target_type
bullseye_state
timestamp
match_id
```

Use the existing telemetry naming conventions and identity architecture rather than creating a parallel telemetry system.

The exact database/storage field names may differ based on the existing implementation.

---

# 5. `was_ricochet`

Every relevant damage/elimination event should have a clear ricochet indicator.

Conceptually:

```text
was_ricochet = true
```

if the damaging shot bounced at least once before causing damage.

Otherwise:

```text
was_ricochet = false
```

Prefer explicitly recording false rather than leaving the field null when the system knows the answer.

---

# 6. Ricochet Elimination Telemetry

When a ricocheted shot causes the final damage resulting in elimination, record the elimination as a ricochet elimination.

The elimination event should preserve:

```text
was_ricochet = true
ricochet_count
weapon_id
elimination_distance
bullseye_state
```

along with the existing elimination telemetry fields.

This should apply regardless of whether the eliminated Bullseye was:

```text
Attached
```

or:

```text
Detached
```

For example:

```text
was_ricochet = true
bullseye_state = detached
```

should be possible.

---

# 7. Multiple Classification Support

Combat events can satisfy multiple conditions simultaneously.

Do NOT make the telemetry architecture assume an elimination can have only one special classification.

For example, the same elimination might be:

```text
Ricochet
+
Detached Bullseye
+
Long Distance
```

The telemetry system should retain all valid properties.

Do not force the system into choosing only one award/category when multiple facts are true.

---

# 8. Interaction With Existing Elimination Telemetry

REQ-071 should extend the existing elimination telemetry rather than creating a separate standalone ricochet-elimination table/event unless the existing telemetry architecture requires it.

Prefer something conceptually like:

```text
EliminationEvent
{
    attacker
    victim
    weapon
    distance
    bullseyeState
    wasRicochet
    ricochetCount
    wasDolphinDive
    ...
}
```

over unrelated telemetry systems for:

```text
NormalEliminations
RicochetEliminations
DolphinDiveEliminations
```

The elimination event should be rich enough to describe how the elimination happened.

---

# 9. Ricochet Damage Statistic

Telemetry should make it possible to calculate at least:

```text
Total Ricochet Damage
```

for a player.

This does not necessarily need to be displayed in the game yet.

The telemetry must simply capture enough information for it to be calculated later.

Potential future lifetime statistic:

```text
Ricochet Damage
```

---

# 10. Ricochet Elimination Statistic

Telemetry should make it possible to calculate:

```text
Ricochet Eliminations
```

for each player.

Again, this does not require adding the statistic to the current scoreboard or profile UI.

The data must simply support it.

Potential future stat:

```text
Ricochet Eliminations: 27
```

---

# 11. Optional Ricochet Surface Context

If it can be captured cleanly from the existing REQ-052 architecture, also record information about the ricochet surface.

Potential telemetry:

```text
ricochet_surface_type
```

or:

```text
ricochet_surface_id
```

Examples might eventually include:

```text
Metal
Concrete
SpecialRicochetWall
```

However, do not create an unnecessarily complicated material taxonomy as part of this ticket.

This is secondary to reliably capturing:

```text
was_ricochet
ricochet_count
```

---

# 12. Combat Feedback Integration

REQ-071 must integrate with the combat-feedback system from REQ-070.

When the local player causes damage with a ricocheted shot, the feedback system should be capable of showing ricochet attribution.

For a normal ricochet hit, an acceptable presentation might be:

```text
+10
RICOCHET
```

or:

```text
+10  RICOCHET
```

The exact damage number should come from the actual damage/scoring event.

Do not hard-code:

```text
+10
```

---

# 13. Ricochet Elimination Feedback

If a ricocheted shot eliminates another player, display a ricochet award.

Example:

```text
+10  ELIMINATION
RICOCHET
```

or:

```text
ELIMINATION
RICOCHET
```

depending on the scoring data available.

The word:

```text
RICOCHET
```

should use the REQ-070 combat-award presentation style.

It should therefore inherit:

- small size
- italic font
- white text
- Bullseye-red outline
- near-reticle positioning
- fade behavior
- award priority rules

Do not create a separate unrelated HUD element for ricochet feedback.

---

# 14. Multiple Awards in Feedback

The combat-feedback system must support multiple valid awards arising from a single elimination.

Example:

A player shoots a ricocheted bullet that destroys a detached Bullseye.

The feedback might display:

```text
+10  ELIMINATION
RICOCHET
DETACHED BULLSEYE
```

Another future example might be:

```text
ELIMINATION
RICOCHET
LONG SHOT
```

REQ-071 does not need to redesign the REQ-070 HUD, but it must ensure ricochet classification can coexist with other awards.

---

# 15. Add Ricochet to Combat Award Types

Extend the REQ-070 combat-award architecture.

Conceptually:

```csharp
CombatAwardType
{
    None,
    Elimination,
    DolphinDive,
    DetachedBullseye,
    Ricochet
}
```

If the actual implementation uses tags, flags, data assets, or another architecture instead of an enum, follow the existing design.

The key requirement is that:

```text
Ricochet
```

becomes a recognized combat-feedback classification.

---

# 16. Authoritative Attribution

Ricochet status should come from the actual authoritative combat event.

Do not determine ricochet status based solely on a local animation, predicted trajectory, or UI event.

Preferred flow:

```text
Shot created/fired
      ↓
Shot records ricochet
      ↓
Authoritative hit occurs
      ↓
Damage context contains ricochet information
      ↓
Server/gameplay authority resolves damage
      ↓
Telemetry records result
      ↓
Attacking player receives combat feedback
```

This should prevent incorrect or duplicate awards in multiplayer.

---

# 17. Multiplayer

Ricochet feedback should only appear for the player responsible for causing the damage.

Example:

```text
Player A
    ↓
shoots wall
    ↓
bullet ricochets
    ↓
hits Player B
```

Player A should see:

```text
+Damage
RICOCHET
```

Player B should not see Player A's score/award notification.

Other players should not see it either.

The victim's own damage-response UI, if any, is separate.

---

# 18. Avoid Duplicate Telemetry

One ricochet hit should generate one damage record.

One ricochet elimination should generate one elimination record.

Do not accidentally generate:

```text
Direct damage event
+
Ricochet damage event
```

for the same damage.

Ricochet should be an attribute of the damage event, not an additional copy of it.

Correct:

```text
DamageEvent
damage = 10
was_ricochet = true
```

Incorrect:

```text
DamageEvent = 10
RicochetDamageEvent = 10
```

if both represent the same actual hit.

---

# 19. Avoid Duplicate Awards

Similarly, the REQ-070 HUD should receive only one ricochet classification per relevant hit/elimination.

A single bounce should not produce:

```text
RICOCHET
RICOCHET
```

because both the projectile system and damage system independently emitted UI events.

The authoritative damage/elimination result should drive HUD feedback.

---

# 20. Damage Source Propagation

Inspect the existing damage architecture and ensure ricochet state travels with whatever object/context represents damage.

Potential examples:

```text
DamageInfo
HitInfo
ProjectileContext
ShotContext
DamageRequest
```

If a shared damage-context object already exists, extend it.

Prefer that over adding disconnected boolean parameters to many methods.

Conceptually:

```csharp
DamageInfo
{
    DamageAmount
    Attacker
    Weapon
    HitLocation
    HasRicocheted
    RicochetCount
}
```

Cursor should use existing naming and architecture where available.

---

# 21. Distance

Existing telemetry already tracks elimination distance.

For ricochet eliminations, retain the existing elimination-distance field.

Do not change its definition silently.

If current elimination distance is:

```text
attacker-to-target distance
```

continue using that definition.

A future ticket may separately capture:

```text
total projectile path distance
```

including bounce segments.

REQ-071 does not require changing elimination-distance semantics.

---

# 22. Optional Projectile Travel Distance

If the ricochet implementation already makes this trivial, Cursor may additionally expose:

```text
projectile_path_distance
```

representing total distance traveled by the projectile through all segments.

Example:

```text
Shooter → Wall = 12m
Wall → Target = 8m

Projectile Path Distance = 20m
```

However, this is optional.

Do not significantly expand ticket scope to calculate it if the current projectile architecture does not already track path length.

---

# 23. Data Quality

Ricochet telemetry should remain internally consistent.

Examples:

```text
was_ricochet = false
ricochet_count = 0
```

is valid.

```text
was_ricochet = true
ricochet_count = 1
```

is valid.

This should generally NOT occur:

```text
was_ricochet = false
ricochet_count = 2
```

If `ricochet_count > 0`, then:

```text
was_ricochet
```

should be true.

---

# 24. Scope Boundaries

REQ-071 DOES include:

- identifying ricochet damage
- identifying ricochet eliminations
- recording ricochet status in telemetry
- recording ricochet count
- integrating ricochet awards into REQ-070
- supporting multiple simultaneous elimination classifications
- multiplayer-safe attribution
- avoiding duplicate telemetry and feedback

REQ-071 DOES NOT require:

- rebuilding ricochet physics
- changing ricochet trajectories
- changing the predicted ricochet indicator from REQ-052
- adding lifetime-stat UI
- redesigning the scoreboard
- adding new ricochet surfaces
- adding new weapons
- implementing long-shot awards
- changing damage values
- calculating complicated ricochet-angle analytics unless already available

---

# 25. Acceptance Criteria

REQ-071 is complete when:

- [ ] A direct shot is recorded with `was_ricochet = false`.
- [ ] A ricocheted damaging shot is recorded with `was_ricochet = true`.
- [ ] Ricochet count is retained through the damage event.
- [ ] A one-bounce hit records `ricochet_count = 1`.
- [ ] Multiple ricochets can be represented by values greater than 1 if gameplay permits them.
- [ ] Ricochet damage amount is recorded through the existing telemetry system.
- [ ] Ricochet eliminations are explicitly identifiable in telemetry.
- [ ] Existing weapon ID is retained for ricochet damage/eliminations.
- [ ] Existing elimination distance remains available.
- [ ] Attached/detached Bullseye state can coexist with ricochet status.
- [ ] Ricochet status can coexist with other elimination classifications.
- [ ] The attacking player receives a `RICOCHET` feedback award.
- [ ] A ricochet elimination can display both `ELIMINATION` and `RICOCHET`.
- [ ] The Ricochet award uses the existing REQ-070 visual system.
- [ ] Ricochet feedback is shown only to the responsible player.
- [ ] One hit creates one telemetry damage event.
- [ ] One elimination creates one elimination event.
- [ ] Networking does not create duplicate ricochet notifications.
- [ ] The HUD does not independently infer ricochet status.
- [ ] Existing non-ricochet damage behavior continues working normally.
- [ ] Existing REQ-052 ricochet behavior remains intact.

---

# 26. Testing Checklist

## Direct Shot Control

1. Shoot another player directly.
2. Confirm damage occurs.
3. Confirm telemetry shows:

```text
was_ricochet = false
ricochet_count = 0
```

4. Confirm no `RICOCHET` award appears.

---

## Ricochet Damage

1. Shoot a valid ricochet surface.
2. Have the bounced shot hit another player.
3. Confirm damage occurs.
4. Confirm telemetry contains:

```text
was_ricochet = true
ricochet_count >= 1
```

5. Confirm the attacking player receives:

```text
+Damage
RICOCHET
```

---

## Ricochet Elimination

1. Reduce a target's health as needed.
2. Eliminate the target using a ricocheted shot.
3. Confirm elimination telemetry contains:

```text
was_ricochet = true
```

4. Confirm the attacker sees:

```text
ELIMINATION
RICOCHET
```

---

## Detached Bullseye + Ricochet

1. Detach another player's Bullseye.
2. Shoot a ricochet surface.
3. Hit and eliminate the detached Bullseye.
4. Confirm telemetry preserves both:

```text
was_ricochet = true
bullseye_state = detached
```

5. Confirm the combat-feedback system can display both relevant awards without overlap or duplication.

---

## Multiple Ricochets

If supported:

1. Cause a projectile to bounce multiple times.
2. Damage a target.
3. Confirm:

```text
ricochet_count
```

matches the actual number of ricochets.

---

## Multiplayer

Test with at least two players.

Confirm:

- attacker sees Ricochet feedback
- victim does not see attacker's Ricochet award
- unrelated players do not see it
- telemetry records only one damage event per actual hit
- telemetry records only one elimination event
- the Ricochet award appears only once

---

# 27. Desired Result

Ricochet shots should become a first-class combat event rather than being reduced to ordinary damage after the bounce occurs.

A successful ricochet hit should be recorded as:

```text
Damage
+
Ricochet
```

A ricochet elimination should be recorded as:

```text
Elimination
+
Ricochet
```

And the player should receive immediate feedback such as:

```text
+10
RICOCHET
```

or:

```text
+10  ELIMINATION
RICOCHET
```

This gives the game immediate reward for difficult ricochet shots while also ensuring the telemetry system can later support statistics, player profiles, balancing analysis, and research into how players use the ricochet mechanic.