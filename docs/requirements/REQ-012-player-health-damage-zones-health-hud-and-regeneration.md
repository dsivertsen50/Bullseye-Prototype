# REQ-012 — Player Health, Damage Zones, Health HUD, and Regeneration

## Summary

Introduce a formal player health system and display the local player's current health in the bottom-left corner of their screen.

Each player should have:

* A maximum of **8 health units**.
* Damage determined by the body region where their bullseye is located when it is shot.
* A local health HUD visible during gameplay.
* Automatic health regeneration beginning after the player has avoided damage for 5 seconds.

Initial damage values:

| Bullseye Region | Damage |
| --------------- | -----: |
| Head            |      8 |
| Torso           |      4 |
| Lower body      |      2 |

These values are intended for the prototype and should be configurable so they can be refined later when the final player mesh and bullseye surface are developed.

---

# Design Intent

The bullseye should not always represent the same amount of danger.

Its location should create changing risk for the player.

For example:

```text
Bullseye on head
→ Extremely vulnerable
→ One successful hit kills

Bullseye on torso
→ Moderately vulnerable
→ Two hits from full health kill

Bullseye on lower body
→ Less vulnerable
→ Four hits from full health kill
```

This creates an additional strategic dimension to the moving bullseye mechanic.

A player whose bullseye moves into a dangerous location may want to use movement mechanics to manipulate it away from that region.

---

# Player Health

## Maximum Health

Each player should have:

```text
Maximum Health = 8
Starting Health = 8
```

Health should never exceed 8.

Health should never fall below 0.

---

## Death

When health reaches:

```text
0
```

the existing player death and respawn system should activate.

The new health system should integrate with the existing death system rather than creating a separate death implementation.

After respawning:

```text
Current Health = 8
```

---

# Bullseye Damage Regions

Damage should be based on the **location of the bullseye on the victim's body at the time the bullseye is successfully hit**.

Only a valid bullseye hit should deal damage.

Normal body hits should continue to follow the existing behavior and should not deal damage unless another requirement explicitly changes this later.

---

## Head Region

A successful hit against a bullseye currently located in the head region should deal:

```text
8 damage
```

Therefore:

```text
8 / 8 health
→ headshot
→ 0 / 8
→ death
```

A head-region bullseye hit should effectively function as an instant kill under the current 8-health system.

---

## Torso Region

A successful hit against a bullseye currently located in the torso region should deal:

```text
4 damage
```

Example:

```text
8 / 8
→ torso hit
→ 4 / 8

4 / 8
→ torso hit
→ 0 / 8
→ death
```

---

## Lower Body Region

A successful hit against a bullseye currently located in the lower-body region should deal:

```text
2 damage
```

Example:

```text
8 / 8
→ lower-body hit
→ 6 / 8
```

---

# Region Classification

The current prototype may not yet have a final player mesh.

Therefore, this ticket should implement a reasonable prototype method for determining whether the bullseye is currently in the:

* Head region
* Torso region
* Lower-body region

The implementation may use the existing bullseye surface, anchors, coordinates, colliders, or other existing body-position information.

The solution should avoid tightly coupling the long-term damage system to the temporary prototype geometry.

The important gameplay relationship is:

```text
Bullseye position
        ↓
Body region classification
        ↓
Damage amount
```

The body-region definitions are expected to be refined later when a proper player model is introduced.

---

# Configurable Damage Values

The following values should preferably be exposed in the Inspector or otherwise stored in an easily configurable location:

```text
Head Damage = 8
Torso Damage = 4
Lower Body Damage = 2
```

Do not unnecessarily hardcode these values throughout multiple scripts.

There should ideally be one authoritative configuration for the damage amounts.

---

# Player Health HUD

## Location

Display the local player's health in the:

> **Bottom-left corner of the player's screen**

The HUD should be local to each player's client.

Player 1 should see Player 1's health.

Player 2 should see Player 2's health.

Do not display another player's health as the local HUD.

---

# Recommended Health Display

For the prototype, use an **8-unit segmented health display**.

Conceptually:

```text
■■■■■■■■   8 / 8
```

After taking 2 damage:

```text
■■■■■■□□   6 / 8
```

After taking 4 additional damage:

```text
■■□□□□□□   2 / 8
```

The exact visual style may be simple for now.

The implementation should prioritize:

* Readability
* Correct health values
* Correct screen placement
* Easy future replacement with polished UI

A conventional continuous health bar is also acceptable if it is substantially easier to integrate with the existing UI architecture, but the preferred prototype presentation is an 8-unit segmented display because the game's damage values operate naturally in discrete units.

---

# Health HUD Requirements

The HUD should:

* Display immediately when gameplay begins.
* Start at full health.
* Update immediately when damage is received.
* Update while health regenerates.
* Return to full health after respawn.
* Remain anchored to the bottom-left corner across supported resolutions.
* Represent only the local player's health.

The display may optionally include:

```text
8 / 8
```

alongside the graphical health representation.

---

# Health Regeneration

Players should automatically recover health after avoiding damage for a period of time.

---

## Regeneration Delay

After taking damage:

```text
Regeneration Delay = 5 seconds
```

No health should regenerate during these five seconds.

The delay begins from the **most recent instance of damage**.

Example:

```text
Player damaged
→ timer starts

4 seconds pass

Player damaged again
→ timer resets to 0

5 more seconds without damage
→ regeneration begins
```

---

# Regeneration Rate

Initial prototype value:

```text
1 health unit per second
```

The regeneration rate should be configurable.

Example:

```text
Current Health = 2

5 seconds without damage
→ regeneration begins

1 second → 3 health
2 seconds → 4 health
3 seconds → 5 health
...
6 seconds → 8 health
```

Once maximum health is reached, regeneration stops.

---

# Damage Interrupts Regeneration

If a player takes damage while regeneration is occurring:

```text
Regeneration stops immediately
```

and the five-second delay starts again.

Example:

```text
4 / 8
→ regen reaches 6 / 8
→ player gets hit
→ health becomes 4 / 8
→ regen stops
→ wait another 5 seconds
```

---

# Health Regeneration Rules

Health regeneration should:

* Never occur while the player is dead.
* Never increase health above 8.
* Resume normally after the player respawns and subsequently takes damage.
* Be based on authoritative health state.
* Stop/reset whenever valid damage occurs.

Shooting, jumping, crouching, turning, or moving should **not** interrupt regeneration unless a future requirement changes this behavior.

Only receiving damage resets the regeneration delay.

---

# Multiplayer Authority

Health and damage are gameplay-critical state.

The authoritative network instance should determine:

* Whether a bullseye was successfully hit.
* Which player owns the bullseye.
* Which body region the bullseye occupies.
* How much damage is applied.
* Current player health.
* Whether the player dies.
* Health regeneration.

Clients should not independently decide their own authoritative health value.

Conceptually:

```text
Player 1 shoots Player 2 bullseye
        ↓
Valid hit confirmed
        ↓
Determine Player 2 bullseye region
        ↓
Determine damage
        ↓
Server/authority updates Player 2 health
        ↓
Player 2 HUD receives updated health
```

---

# HUD Networking

The HUD itself should not require network synchronization as a UI object.

Instead:

```text
Authoritative health state
        ↓
Synchronized to owning client
        ↓
Local HUD displays value
```

Each client should only control its own health display.

---

# Interaction With Existing Controller Rumble

The existing damage rumble should continue working.

When a player takes valid damage:

```text
Damage applied
+
Victim damage rumble
+
Health HUD updates
```

A headshot that causes immediate death may still trigger the existing damage feedback before or during the death sequence if consistent with the current implementation.

Do not redesign haptics as part of this ticket.

---

# Interaction With Existing Bullseye System

The following existing bullseye behaviors should continue to work:

* Randomized bullseye movement
* Independent per-player randomization
* Jump influence
* Crouch influence
* Turn influence
* Surface movement
* Network synchronization
* Self-hit prevention

The bullseye's current position should determine the relevant damage region when it is hit.

---

# Respawn Behavior

When a player dies and respawns:

```text
Current Health = Maximum Health = 8
```

The HUD should immediately return to full health.

Any active:

* regeneration timer
* regeneration process
* delayed health operation

from the player's previous life should be cleared.

The new life begins at full health with no regeneration currently necessary.

---

# Suggested Inspector Configuration

Where practical, expose:

### Health

* Maximum Health: `8`

### Damage

* Head Damage: `8`
* Torso Damage: `4`
* Lower Body Damage: `2`

### Regeneration

* Regeneration Delay: `5 seconds`
* Regeneration Rate: `1 health / second`

This allows prototype balancing without code changes.

---

# Out of Scope

The following are outside the scope of REQ-012:

* Final polished HUD artwork.
* Final player mesh.
* Final anatomical region boundaries.
* Armor.
* Shields.
* Health pickups.
* Healing weapons.
* Teammate healing.
* Different maximum health by player.
* Different health by game mode.
* Damage from normal body shots.
* Environmental damage.
* Fall damage.
* Damage-over-time effects.
* Player health displayed above enemy characters.
* Enemy health bars.
* Audio heartbeat effects.
* Low-health screen effects.
* Changes to controller rumble.
* Changes to weapon damage independent of bullseye location.

---

# Acceptance Criteria

* [ ] Each player starts with 8 health.
* [ ] Each player's health state is independent.
* [ ] Player health cannot exceed 8.
* [ ] Player health cannot fall below 0.
* [ ] A valid head-region bullseye hit deals 8 damage.
* [ ] A valid torso-region bullseye hit deals 4 damage.
* [ ] A valid lower-body bullseye hit deals 2 damage.
* [ ] Reaching 0 health triggers the existing death behavior.
* [ ] Respawning restores health to 8.
* [ ] The local player's health is displayed in the bottom-left corner.
* [ ] Player 1's HUD displays Player 1's health.
* [ ] Player 2's HUD displays Player 2's health.
* [ ] The health HUD updates immediately after taking damage.
* [ ] Health does not regenerate during the first 5 seconds after damage.
* [ ] Regeneration begins after 5 uninterrupted seconds without damage.
* [ ] Health initially regenerates at approximately 1 unit per second.
* [ ] Taking additional damage resets the regeneration delay.
* [ ] Taking damage during regeneration immediately stops regeneration.
* [ ] Regeneration stops at 8 health.
* [ ] Regeneration does not occur while dead.
* [ ] Health and damage remain authoritative and network-consistent.
* [ ] Existing bullseye functionality continues working.
* [ ] Existing shooting functionality continues working.
* [ ] Existing self-hit prevention continues working.
* [ ] Existing controller rumble continues working.
* [ ] Existing death and respawn behavior continues working.
* [ ] No new console errors are introduced.

---

# Testing Procedure

## Test 1 — Initial Health

Start a multiplayer game.

Expected:

```text
Player 1 = 8 / 8
Player 2 = 8 / 8
```

Each player should see their own full-health HUD.

---

## Test 2 — Lower-Body Hit

Position Player 2's bullseye in the lower region.

Have Player 1 shoot it.

Expected:

```text
Player 2:
8 → 6
```

Player 2 remains alive.

Player 2's HUD updates immediately.

---

## Test 3 — Torso Hit

Start from full health.

Shoot Player 2's torso-region bullseye.

Expected:

```text
8 → 4
```

Shoot it again before regeneration.

Expected:

```text
4 → 0
→ Player 2 dies
```

---

## Test 4 — Headshot

Start Player 2 at full health.

Shoot a bullseye located in the head region.

Expected:

```text
8 → 0
→ immediate death
```

---

## Test 5 — Regeneration Delay

Reduce Player 2 from:

```text
8 → 4
```

Wait fewer than 5 seconds.

Expected:

```text
Health remains 4
```

After 5 seconds without additional damage:

Expected:

```text
Health begins increasing
```

---

## Test 6 — Regeneration

Allow a damaged player to remain unharmed.

Expected progression using the initial tuning:

```text
4
→ 5
→ 6
→ 7
→ 8
```

Health should stop at 8.

---

## Test 7 — Interrupt Regeneration

Damage Player 2.

Wait for regeneration to begin.

Damage Player 2 again.

Expected:

* Regeneration immediately stops.
* Damage is applied.
* The five-second timer restarts.
* No further regeneration occurs until another full five seconds passes.

---

## Test 8 — Separate Player Health

Damage Player 1.

Expected:

* Player 1's health changes.
* Player 2's health does not.
* Player 1's HUD changes.
* Player 2's HUD does not.

Repeat in reverse.

---

## Test 9 — Respawn

Kill Player 1.

Allow Player 1 to respawn.

Expected:

```text
Player 1 health = 8 / 8
```

The HUD returns to full health and no regeneration state from the previous life remains active.

---

# Prototype Design Intent

The health system should reinforce the central Bullseye mechanic:

> **Where your vulnerability is located matters.**

An exposed head bullseye creates immediate danger.

A torso bullseye creates substantial danger.

A lower-body bullseye provides more survivability.

Regeneration prevents every successful engagement from permanently weakening a surviving player while still giving an attacker a window to finish the fight.

The initial model is therefore:

```text
Maximum Health:      8

Head hit:            -8
Torso hit:           -4
Lower-body hit:      -2

Regen delay:         5 seconds
Regen rate:          +1 per second
```

These values should provide a clear starting point for playtesting and can be tuned as the weapon system, player model, and overall combat pace become more developed.
