# REQ-013 — Death Screen and Respawn Countdown

## Summary

Add a short respawn delay and visible countdown after a player is killed.

When a player dies:

1. Their character is killed according to the existing death logic.
2. Their screen remains viewing the location where they died.
3. Player movement, aiming, and shooting are disabled.
4. A large countdown appears on screen:

```text
3
2
1
```

5. After the countdown completes, the player respawns using the existing respawn system.
6. The countdown disappears and normal player control resumes.

The initial respawn delay should be **3 seconds**.

---

# Design Intent

Death currently transitions too quickly into respawning.

Adding a short pause should:

* Make kills feel more meaningful.
* Give players clear feedback that they died.
* Prevent confusion about whether they were teleported or respawned.
* Give the player a moment to understand where the final shot occurred.
* Create a cleaner transition between death and the next life.

The desired experience is:

```text
Player dies
    ↓
View remains at death location
    ↓
3
2
1
    ↓
Respawn
```

---

# Functional Requirements

## 1. Respawn Delay

When player health reaches zero, do not immediately respawn the player.

Instead, begin a:

```text
3-second respawn countdown
```

The respawn delay should preferably be configurable in the Inspector.

Initial value:

```text
Respawn Delay = 3 seconds
```

---

# 2. Preserve View at Death Location

After death, the local player's screen should remain viewing the location where the player died.

The player should **not** immediately transition to the respawn location.

The death view should approximately preserve the camera position and orientation from the moment of death.

Example:

```text
Player 1 is looking toward Player 2
        ↓
Player 1 is killed
        ↓
Player 1 continues seeing that part of the arena
        ↓
Countdown occurs
        ↓
Player 1 respawns elsewhere
```

For this prototype, a static death camera is sufficient.

The camera does not need to follow the killer.

---

# 3. Freeze Player Control While Dead

Once the player dies, disable their gameplay control until respawn.

The dead player should not be able to:

* Move
* Jump
* Crouch
* Turn their character
* Fire
* Damage another player
* Manipulate their bullseye through movement inputs

Input may still technically be received by the input system, but gameplay actions should not execute while the player is dead.

---

# 4. Respawn Countdown UI

Display a large countdown near the center of the local player's screen.

The countdown should display:

```text
3
```

then:

```text
2
```

then:

```text
1
```

and then disappear when the player respawns.

The text should be:

* Large
* Easy to read
* Centered or approximately centered
* Visible over the frozen death view

A simple prototype UI is sufficient.

No final artwork or animation is required.

---

# 5. Optional Supporting Text

If convenient, the countdown may include simple supporting text such as:

```text
RESPAWNING IN

3
```

However, this is optional.

The number itself is the primary requirement.

---

# 6. Local UI Only

The respawn countdown should only appear for the player who died.

Example:

```text
Player 1 dies
```

Expected:

```text
Player 1 screen:
3
2
1

Player 2 screen:
normal gameplay
```

Player 2 should not see Player 1's countdown as their own UI.

---

# 7. Existing Respawn System

After the countdown finishes, use the project's existing respawn functionality.

Do not build a separate competing respawn system if the existing one can be extended.

The expected sequence is:

```text
Death confirmed
        ↓
Player marked dead
        ↓
Countdown starts
        ↓
3 seconds pass
        ↓
Existing respawn logic executes
        ↓
Player appears at valid spawn location
```

---

# 8. Respawn State Reset

When the player respawns, the new life should begin normally.

Existing respawn behavior should continue to restore/reset relevant systems.

This should include, where applicable:

* Health restored to 8 / 8
* Player movement enabled
* Camera control enabled
* Shooting enabled
* Bullseye system active
* Health regeneration state reset
* Damage state reset
* Countdown UI hidden

No state from the previous death countdown should remain active.

---

# 9. Camera Transition

The camera should remain at the death location during the countdown.

When respawn occurs, it may immediately switch to the player's normal first-person camera at the new spawn location.

A sophisticated transition is not required.

For this ticket, the following is acceptable:

```text
Death camera
      ↓
3
2
1
      ↓
Immediate cut
      ↓
Respawn camera
```

No fade animation is required.

---

# 10. Dead Player Representation

This ticket does not require a final death animation or ragdoll.

If the existing player object is hidden, disabled, or otherwise handled during death, preserve that behavior where practical.

The key requirement is that disabling the player character must **not** inadvertently destroy or disable the local death-view camera before the countdown is complete.

The implementation agent should structure the death camera/countdown accordingly.

---

# Multiplayer Authority

Death and respawn remain authoritative gameplay events.

The authoritative instance should determine:

* When health reaches zero.
* When the player enters the dead state.
* When the player is eligible to respawn.
* Where the player respawns.
* When the player's gameplay state becomes active again.

The countdown UI itself is local presentation.

Conceptually:

```text
Server/authority:
Player 1 dies
        ↓
Player 1 marked dead
        ↓
Respawn delay

Player 1 client:
Freeze death view
        ↓
Display 3 → 2 → 1
        ↓
Receive/execute respawn
```

The displayed countdown should correspond closely to the actual authoritative respawn timing.

---

# Prevent Multiple Respawn Processes

A death event should only start one countdown and one respawn sequence.

Repeated damage events, RPCs, or other death-related callbacks must not cause:

* Multiple countdowns
* Multiple respawn coroutines
* Multiple respawn requests
* Multiple player objects to spawn

Once the player is dead, additional damage should have no gameplay effect until the next life begins.

---

# Countdown Timing

The initial desired timing is approximately:

```text
Death occurs

0–1 seconds:
3

1–2 seconds:
2

2–3 seconds:
1

At approximately 3 seconds:
Respawn
```

The implementation may vary slightly depending on Unity timing behavior, but the countdown should feel intuitive and match the actual respawn delay.

Avoid displaying:

```text
0
```

unless technically necessary.

Preferred experience:

```text
3 → 2 → 1 → respawn
```

---

# Interaction With Health System

REQ-012 health behavior should remain intact.

When:

```text
Health <= 0
```

the player enters the dead state.

While dead:

* Health regeneration must not occur.
* Additional damage must not matter.
* HUD health may remain at 0 until respawn.

When the countdown completes:

```text
Health = 8 / 8
```

according to the existing respawn requirements.

---

# Interaction With Controller Rumble

Existing damage rumble may still occur when the killing shot is received.

The controller should not remain vibrating throughout the death countdown.

Any active temporary rumble should complete or be reset normally.

No special death rumble is required by this ticket.

---

# Interaction With Bullseye

While the player is dead:

* Bullseye randomization does not need to continue.
* Bullseye movement inputs should not matter.
* The bullseye should not remain a valid damage target.

After respawn:

* The player's bullseye system should resume normally.
* Independent randomization should continue working.
* The bullseye should belong to the newly active life/player state.

---

# Suggested UI Structure

The implementation may use the existing player HUD canvas.

Conceptually:

```text
Player HUD
│
├── Reticle
├── Health Display
└── Respawn Overlay
       ├── "RESPAWNING IN" (optional)
       └── Countdown Number
```

The respawn overlay should normally be hidden.

When the local player dies:

```text
Respawn Overlay = visible
```

When the player respawns:

```text
Respawn Overlay = hidden
```

---

# Inspector Configuration

Where practical, expose:

```text
Respawn Delay = 3 seconds
```

This will allow the respawn pacing to be changed later without modifying code.

The countdown UI references may also be assigned through the Inspector if consistent with the current UI architecture.

---

# Out of Scope

The following are outside the scope of REQ-013:

* Kill cams
* Following the killer after death
* Spectating other players
* Ragdolls
* Death animations
* Death sound redesign
* Scoreboards
* Kill feed
* Respawn selection
* Choosing spawn locations
* Spawn protection
* Fade-to-black effects
* Cinematic camera movement
* Final UI artwork
* Revive mechanics
* Limited lives
* Round-based elimination
* Changing the existing spawn-point algorithm

These can be added later if useful.

---

# Acceptance Criteria

* [ ] A player does not immediately respawn when killed.
* [ ] Death begins a 3-second respawn delay.
* [ ] The dead player's view remains at approximately the location and orientation where they died.
* [ ] A large `3` appears after death.
* [ ] The countdown progresses from `3` to `2` to `1`.
* [ ] The player respawns after the countdown completes.
* [ ] The countdown disappears upon respawn.
* [ ] Only the dead player sees their countdown UI.
* [ ] Other players continue playing normally during another player's countdown.
* [ ] The dead player cannot move during the countdown.
* [ ] The dead player cannot jump or crouch.
* [ ] The dead player cannot shoot.
* [ ] The dead player cannot damage another player.
* [ ] The dead player's bullseye is not a valid active vulnerability.
* [ ] Health regeneration does not occur while dead.
* [ ] The player respawns with 8 / 8 health.
* [ ] Normal movement resumes after respawn.
* [ ] Normal shooting resumes after respawn.
* [ ] Normal camera control resumes after respawn.
* [ ] Bullseye behavior resumes after respawn.
* [ ] Existing controller rumble continues functioning.
* [ ] Only one respawn sequence occurs per death.
* [ ] No duplicate player objects are created.
* [ ] No new console errors are introduced.

---

# Testing Procedure

## Test 1 — Basic Death

Have Player 2 kill Player 1.

Expected:

```text
Player 1 dies
→ view remains at death location
→ 3
→ 2
→ 1
→ Player 1 respawns
```

---

## Test 2 — Control Lock

Kill Player 1.

During the countdown, attempt to:

* Move
* Aim
* Jump
* Crouch
* Fire

Expected:

None of these actions affect gameplay.

---

## Test 3 — Other Player Continues Playing

Kill Player 1.

While Player 1 is counting down, move and shoot as Player 2.

Expected:

Player 2 continues playing normally.

Player 2 does not see Player 1's respawn overlay.

---

## Test 4 — Health Reset

Reduce Player 1 to zero health.

Expected during death:

```text
Health = 0
```

After respawn:

```text
Health = 8 / 8
```

---

## Test 5 — No Regeneration While Dead

Kill Player 1.

Observe health during the 3-second countdown.

Expected:

Health remains at zero.

Regeneration does not begin.

---

## Test 6 — Repeated Deaths

Kill and respawn Player 1 several times.

Expected:

Each death produces exactly one:

```text
3 → 2 → 1 → respawn
```

No duplicate countdowns or duplicate player objects occur.

---

## Test 7 — Both Players Die

Kill Player 1 and Player 2 close together if possible.

Expected:

Each player independently receives their own death countdown.

Each player's respawn timing and camera operate independently.

---

## Test 8 — Respawn Controls

Immediately after Player 1 respawns:

Expected:

* Movement works.
* Camera works.
* Shooting works.
* Health shows 8 / 8.
* Bullseye works.
* Countdown is no longer visible.

---

# Prototype Design Intent

Death should have a short but clear consequence without substantially slowing down the match.

The desired loop is:

```text
Fight
  ↓
Killed
  ↓
Brief moment to register death
  ↓
3
2
1
  ↓
Back into the fight
```

A **3-second respawn delay** should be long enough for the player to understand what happened while remaining short enough to preserve the fast pace of the prototype.

The static view of the death location also provides useful visual information without requiring a full kill-cam or spectator system.
