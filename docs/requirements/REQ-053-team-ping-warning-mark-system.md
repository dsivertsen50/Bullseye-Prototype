````markdown
# REQ-053 — Team Ping / Warning Mark System

## Summary

Add a multiplayer **team ping / warning system** that allows a player to quickly mark a location or enemy for their teammates.

The player should aim their crosshair toward a location and press the Ping input.

The ping should create a highly visible **warning marker containing an exclamation mark (`!`)** that allied players can see even when walls or other geometry are between the teammate and the ping.

Players should also be able to directly ping enemy players.

Two basic ping types are required:

```text
1. Location / Warning Ping
2. Enemy Ping
```

Suggested controls:

```text
Controller:
D-Pad Down

Mouse + Keyboard:
Middle Mouse Button
```

The system should be designed as lightweight team communication for players who may not be using voice chat.

---

# Gameplay Concept

## Location Ping

Player aims somewhere:

```text
            X <- location

PLAYER ---->
```

Player presses:

```text
D-Pad Down
```

or:

```text
Middle Mouse Button
```

Teammates see:

```text
            !
            X
```

even if the location is behind geometry from their perspective.

---

## Enemy Ping

Player aims directly at an enemy:

```text
PLAYER --------> ENEMY
```

and presses Ping.

The system recognizes the enemy and creates an enemy warning marker:

```text
             !
           ENEMY
```

That marker is shared with the player's teammates.

The marker should follow the enemy for a short period so teammates can react to the warning.

---

# 1. Ping Input

Add a new input action using the project's existing input system.

Suggested name:

```text
Ping
```

or:

```text
Mark
```

Bindings:

```text
Gamepad:
D-Pad Down

Mouse + Keyboard:
Middle Mouse Button
```

Do NOT create a separate input architecture.

Use the existing Unity Input System/input action configuration.

---

# 2. Mouse + Keyboard Choice

Use:

```text
Middle Mouse Button
```

as the initial Mouse + Keyboard binding.

Halo Infinite uses a similar Mark/Ping system and defaults its keyboard Mark action to `X`, but Middle Mouse Button is preferable for Bullseye because the player can ping while maintaining movement and aim.

The input must remain rebindable if the existing control-settings system supports rebinding.

---

# 3. Location Ping

When the player presses Ping while aiming at valid world geometry:

```text
Camera/Aim Ray
      ↓
Raycast
      ↓
World Surface
      ↓
Create Ping
```

The ping should appear at or slightly above the raycast hit location.

Examples:

- Floor
- Wall
- Doorway
- Cover
- Ramp
- Platform
- Other level geometry

The ping is essentially the player's way of saying:

> Warning — look here.

---

# 4. Warning Symbol

The primary visual language for a normal ping should be:

```text
!
```

The icon may eventually receive custom artwork, but a simple temporary world-space marker is sufficient for this requirement.

Suggested visual:

```text
   !
   ●
```

The exclamation mark communicates:

```text
WARNING / ATTENTION
```

rather than functioning as a generic waypoint.

---

# 5. Ping Visibility Through Walls

This is a core requirement.

All allied players should be able to see the ping marker even when world geometry blocks their direct view.

Example:

```text
TEAMMATE

    |
    |
    |       WALL
    |      ██████
    |
    |              !
    |              X
```

The marker should remain visible to the teammate.

Possible implementations include:

- World-space UI rendered without depth occlusion
- Screen-space projected indicator
- Appropriate shader/rendering configuration
- Existing HUD marker architecture

Cursor should choose the solution best suited to the current project.

Do NOT use a physical world object that simply disappears when hidden behind a wall.

---

# 6. Team-Only Visibility

Pings must only be visible to players on the same team as the player who created the ping.

Example:

```text
Player A = Team Red
Player B = Team Red
Player C = Team Blue
```

Player A creates a ping.

Result:

```text
Player A: sees ping
Player B: sees ping
Player C: DOES NOT see ping
```

Enemy players must never receive the opposing team's ping information.

Cursor should use the project's existing team/game-mode architecture.

Do NOT create an entirely new team framework solely for REQ-053.

If the current prototype does not yet have fully implemented team play, structure the ping system so team filtering can cleanly use the eventual team identifier.

---

# 7. Ping Originator

The player who creates the ping should also see their own marker.

This gives immediate feedback that:

```text
Ping successfully registered.
```

---

# 8. Ping Lifetime

Pings should automatically expire.

Initial suggested values:

```text
Location Ping Duration:
5 seconds

Enemy Ping Duration:
4 seconds
```

Expose these as configurable values.

Example:

```csharp
[SerializeField]
private float locationPingDuration = 5f;

[SerializeField]
private float enemyPingDuration = 4f;
```

These exact numbers should remain easy to tune.

---

# 9. One Active Ping Per Player

To avoid excessive HUD clutter, each player should initially be limited to:

```text
1 active ping
```

If the same player creates another ping before their previous ping expires:

```text
Old ping disappears
↓
New ping appears
```

Example:

```text
Ping A
↓
Player pings again
↓
Ping A removed
Ping B created
```

This can be expanded later if testing suggests multiple active marks would be useful.

---

# 10. Ping Cooldown

Add a short cooldown to prevent ping spam.

Suggested initial value:

```text
0.5 seconds
```

Expose it as configurable.

Example:

```csharp
[SerializeField]
private float pingCooldown = 0.5f;
```

The purpose is only to prevent rapid input/network spam.

Do not make pinging feel slow or cumbersome.

---

# 11. Enemy Detection

If the player's ping ray directly intersects a valid enemy player, create an:

```text
Enemy Ping
```

instead of a static location ping.

Conceptually:

```text
Ping Ray
   ↓
Hit something
   ↓
Is it an enemy player?
  / \
Yes  No
 |    |
Enemy Static
Ping  Location Ping
```

Reuse the current player/team identification architecture.

---

# 12. Enemy Ping Marker

An enemy ping should be visually recognizable as a warning.

The basic marker should still use:

```text
!
```

The marker may eventually have:

- Different color
- Different border
- Enemy-specific icon
- Animation

but polished final UI is not required for REQ-053.

At minimum, players should be able to distinguish:

```text
Pinged location
```

from:

```text
Pinged enemy
```

through icon treatment, label, color, or another simple visual difference.

---

# 13. Enemy Ping Follows Enemy

Unlike a normal location ping, an enemy ping should attach logically to the enemy player.

For approximately:

```text
4 seconds
```

the marker should follow the enemy's current position.

Example:

```text
T = 0

       !
     ENEMY


T = 2 seconds

                    !
                  ENEMY
```

The ping should move with the target during its lifetime.

---

# 14. Enemy Ping Visible Through Walls

Once an enemy has been legitimately pinged, allied players should continue seeing the warning marker through walls for the remaining ping duration.

Example:

```text
PINGED ENEMY
     !
     E

████████████████ WALL

           TEAMMATE
```

The teammate still sees the marker.

This is intentional.

However, the tracking duration must remain short enough that the feature does not become persistent enemy wall tracking.

Initial target:

```text
~4 seconds
```

---

# 15. Enemy Must Be Legitimately Pinged

The player creating the enemy ping must actually have the enemy under their ping ray/crosshair when initiating the ping.

Do NOT allow:

- Automatic nearby enemy detection
- Cone-based enemy searching
- Pinging enemies through walls
- Pinging enemies simply because they are near the crosshair
- Aim assistance toward enemies

The player should have to deliberately point at the enemy.

---

# 16. Cannot Initially Ping Enemy Through Cover

A player should NOT be able to create a new enemy ping through opaque world geometry.

Example:

```text
PLAYER
   ↓

████████ WALL ████████

             ENEMY
```

Pressing Ping should result in:

```text
Location Ping on wall
```

NOT:

```text
Enemy Ping
```

This ensures that enemy pings originate from legitimate visual/contact information.

---

# 17. Existing Enemy Ping May Continue Through Cover

Once legitimately created:

```text
Enemy visible
↓
Player pings enemy
↓
Enemy moves behind wall
↓
Existing marker remains for remaining duration
```

This behavior is intentional and provides the teamwork advantage of successfully marking the enemy.

---

# 18. Enemy Death

If a pinged enemy dies:

```text
Enemy Ping should immediately clear.
```

Do not leave the warning indicator attached to a dead body or respawned player.

---

# 19. Enemy Respawn

An enemy ping must NOT survive respawn.

Example:

```text
Enemy pinged
↓
Enemy dies
↓
Ping removed
↓
Enemy respawns
```

The respawned enemy must not still be marked.

---

# 20. Static Location Ping

Normal location pings should remain attached to their original world position.

They should NOT follow moving players or dynamically seek objects.

Example:

```text
Player pings doorway.

Doorway:
    !
```

The warning remains there until:

- Duration expires
- Player places a new ping
- Ping is otherwise cleared

---

# 21. Marker Placement

For ground/world pings, slightly offset the marker from the exact collision point if necessary to prevent clipping.

For example:

```csharp
pingPosition =
    hit.point + hit.normal * smallOffset;
```

The visual warning icon itself may float slightly above the location so it is easily visible.

---

# 22. Screen Projection

The ping should remain useful regardless of distance and viewing angle.

If the ping is in the player's field of view:

```text
Show marker at projected world location.
```

If the ping leaves the screen, Cursor may optionally provide a small screen-edge indication if this integrates cleanly with the existing HUD.

However:

```text
Off-screen indicator
```

is desirable but not required for initial completion.

The core requirement is visibility through geometry while the ping is on-screen.

---

# 23. Distance Scaling

Avoid allowing the world-space ping icon to become:

- Enormous at close range
- Unreadably tiny at long range

Prefer screen-space sizing or controlled scaling so the warning symbol remains understandable.

Do not spend significant time polishing exact size curves during REQ-053.

---

# 24. Distance Text

Showing distance such as:

```text
!  24m
```

may be useful eventually.

However, distance text is NOT required for REQ-053.

Prioritize the warning marker and team communication behavior.

---

# 25. Network Authority

Ping creation must work correctly in networked multiplayer.

A typical flow should be:

```text
Local player presses Ping
        ↓
Local raycast determines requested target/location
        ↓
Send ping request
        ↓
Server validates request
        ↓
Server creates authoritative ping state
        ↓
Relevant teammates receive ping
```

Do not allow one client to arbitrarily broadcast fake enemy locations.

For enemy pings, server validation should verify that the requested target:

- Exists
- Is alive
- Is an enemy
- Is within reasonable ping range
- Corresponds to a plausible line-of-sight ping request

Adapt validation to the existing networking architecture.

---

# 26. Ping Network Data

Do NOT network a physical GameObject every frame solely for a UI marker unless necessary.

The networking system should preferably transmit lightweight ping information such as:

```text
Ping owner
Ping type
World position
Target NetworkObjectId if enemy ping
Expiration time
Team
```

Then each receiving client can render the appropriate local HUD marker.

---

# 27. Enemy Tracking Networking

For an enemy ping:

```text
Do not continuously transmit ping position every frame.
```

If the enemy player already has a networked transform:

```text
Ping references enemy NetworkObject
↓
Each allied client positions marker from that player's replicated transform
```

This avoids unnecessary network traffic.

---

# 28. Ping UI Is Local Rendering

The marker should be rendered separately on each allied player's client.

Enemy clients should never receive enough ping state to display the opposing team's marker.

Do not simply hide enemy-team ping UI after broadcasting ping information to everyone if team-targeted networking is practical.

---

# 29. Free-For-All Behavior

Bullseye currently supports/has been developed heavily around free-for-all combat.

REQ-053 should NOT force team mechanics into FFA.

If the active mode has no teammates:

```text
Shared team ping functionality may be disabled.
```

or the local player may see their own ping only for testing.

Cursor should not perform a large game-mode redesign as part of this ticket.

The system simply needs to be ready to operate correctly when team assignments exist.

---

# 30. Ping Feedback

When a ping is successfully placed, provide immediate feedback.

At minimum:

```text
Marker appears instantly.
```

If an appropriate UI sound already exists, a temporary ping sound may also be used.

Do not spend substantial time creating final audio.

---

# 31. Future Voice/Callout Compatibility

The architecture should leave room for eventual callouts such as:

```text
"Enemy here!"
"Watch here!"
"Enemy sniper!"
```

but voice callouts are NOT required for REQ-053.

Do not introduce text-to-speech or recorded dialogue systems.

---

# 32. Ping Different Surfaces

Location pings should work reliably on:

- Floors
- Walls
- Ceilings
- Props
- Angled geometry
- Cover
- Platforms

The marker should use the raycast result rather than assumptions about level orientation.

---

# 33. Ping Maximum Range

Add a configurable maximum ping ray distance.

Example:

```csharp
[SerializeField]
private float maxPingDistance;
```

It should be generous enough to ping locations across normal multiplayer maps.

Do not allow effectively infinite world-space raycasts if unnecessary.

---

# 34. Ping and Ricochet Indicator Independence

REQ-053 follows REQ-052, which adds a ricochet trajectory indicator.

Keep these systems independent.

Example:

```text
Ricochet Marker
= personal aiming aid

Ping Marker
= team communication
```

Pressing Ping while looking at a ricochet destination should NOT accidentally alter the ricochet system.

Future integration can be considered separately if useful.

---

# 35. Grenade Selection Input Compatibility

REQ-051 uses:

```text
D-Pad Right
```

for grenade selection.

REQ-053 should use:

```text
D-Pad Down
```

for Ping.

Therefore:

```text
D-Pad Right = Switch Grenade
D-Pad Down  = Ping / Warning
```

Do not overwrite or conflict with existing grenade controls.

---

# 36. HUD Layering

Ping indicators should render appropriately alongside:

- Crosshair
- Bullseye HUD
- Grenade selection
- Weapon UI
- Ricochet prediction
- Existing world indicators

Avoid allowing the exclamation icon to cover the center crosshair unnecessarily.

---

# 37. Multiple Teammate Pings

Different teammates may each have one active ping.

Example:

```text
Player A ping:
!

Player B ping:
!

Player C ping:
!
```

All teammates should be capable of seeing them.

Do not globally restrict the entire team to one ping.

The restriction is:

```text
One active ping PER PLAYER
```

not:

```text
One active ping PER TEAM
```

---

# 38. Duplicate Enemy Pings

If multiple teammates ping the same enemy, do not create severe visual clutter.

For the initial implementation, it is acceptable to:

```text
Refresh/extend the existing enemy marker
```

or:

```text
Represent the enemy with one shared marker
```

if this is straightforward.

Otherwise, prioritize correct functionality over sophisticated ping merging.

Do not render five overlapping exclamation marks directly on the same enemy if that can easily be avoided.

---

# 39. Ping Ownership

Internally retain which player created each ping.

This will allow future functionality such as:

- Different teammate colors
- Ping acknowledgement
- Player labels
- Ping statistics
- Contextual callouts

None of those features are required now.

---

# 40. Ping Clearing

A ping should disappear when:

```text
Its timer expires
```

or:

```text
The owner creates a replacement ping
```

For enemy pings, also clear when:

```text
Enemy dies
Target becomes invalid
Target despawns
Match ends
```

Clean up all ping state properly during:

- Player disconnect
- Scene changes
- Match restart
- Respawn where applicable

---

# 41. No Combat Effect

A ping should NOT:

- Damage enemies
- Reveal bullseyes mechanically
- Increase weapon damage
- Apply aim assist
- Affect player movement
- Affect enemy visibility itself
- Change networking authority over enemies

It is purely a communication/visual system.

---

# 42. Architecture

Before implementing REQ-053, Cursor should inspect:

- Current Unity Input actions
- Controller bindings
- Mouse/keyboard bindings
- Team/player identifiers
- Network player objects
- Player life/death state
- Player respawn handling
- Current HUD canvas
- Existing world-space UI markers
- Layer masks
- Camera aiming ray
- Network ownership architecture

Reuse existing infrastructure.

A possible conceptual architecture:

```text
PlayerPingController
        ↓
Input: Ping
        ↓
Perform Ping Raycast
        ↓
Determine Ping Type
       / \
      /   \
Location Enemy
Ping     Ping
      \   /
       \ /
Server Validates
        ↓
Team Ping State
        ↓
Allied Clients
        ↓
PingHUDRenderer
```

Do not blindly create these exact classes if the project's architecture suggests a cleaner implementation.

---

# 43. Debugging

Add optional ping debug information if useful.

Possible debug visualization:

```text
Ping ray
Hit point
Detected enemy
Ping type
Team recipients
```

This should only be visible during development.

---

# Acceptance Criteria

## AC-1 — Controller Ping Input

Pressing:

```text
D-Pad Down
```

attempts to create a ping at the location currently under the player's aim.

---

## AC-2 — Mouse Ping Input

Pressing:

```text
Middle Mouse Button
```

attempts to create a ping.

---

## AC-3 — Location Ping

Aiming at world geometry and pressing Ping creates a warning marker at that location.

---

## AC-4 — Exclamation Symbol

The warning marker clearly contains or uses:

```text
!
```

as its main visual language.

---

## AC-5 — Team Visibility

All allied players receive and can see the ping.

---

## AC-6 — Enemy Cannot See Ping

Opposing players do NOT see the ping.

---

## AC-7 — Visible Through Walls

An allied player can see the marker even when a wall blocks direct visibility of the ping location.

---

## AC-8 — Enemy Ping

Aiming directly at an enemy and pressing Ping creates an enemy-specific warning ping.

---

## AC-9 — Enemy Ping Tracks Target

The enemy marker follows that enemy for approximately:

```text
4 seconds
```

or the configured enemy-ping duration.

---

## AC-10 — Enemy Behind Wall

If the enemy moves behind a wall after being pinged:

```text
Allied players continue seeing the marker until it expires.
```

---

## AC-11 — Cannot Ping Hidden Enemy Directly

If an enemy is already behind opaque geometry and the player presses Ping:

```text
The enemy is NOT automatically detected.
```

The blocking world surface may instead receive a normal location ping.

---

## AC-12 — Enemy Death Clears Ping

If the pinged enemy dies:

```text
Enemy ping disappears.
```

---

## AC-13 — Respawn Does Not Retain Ping

A respawned player does not retain an enemy ping from their previous life.

---

## AC-14 — Ping Expiration

Location and enemy pings automatically disappear after their configured durations.

---

## AC-15 — New Ping Replaces Old Ping

If Player A already has an active ping and creates another:

```text
Player A's previous ping clears.
```

---

## AC-16 — Multiple Teammates

Player A and Player B can each create a ping simultaneously and their teammates can see both.

---

## AC-17 — No Enemy Information Leak

Players do not receive opposing-team ping data or indicators.

---

## AC-18 — Multiplayer Synchronization

Host and clients correctly see team pings regardless of which teammate created them.

Test:

```text
Host → Client
Client → Host
Client → Client
```

---

## AC-19 — Existing Controls Preserved

REQ-053 does not break:

```text
D-Pad Right = Grenade Selection
```

or other existing input mappings.

---

## AC-20 — Existing Combat Preserved

Ping functionality does not modify weapon firing, bullseye damage, movement, grenade behavior, or ricochet behavior.

---

# Testing Scenarios

When testing is possible, explicitly test:

1. Ping floor.
2. Ping wall.
3. Ping ceiling.
4. Ping angled surface.
5. Ping distant geometry.
6. Ping using D-Pad Down.
7. Ping using Middle Mouse Button.
8. Verify ping appears to creator.
9. Verify ping appears to teammate.
10. Verify enemy cannot see team ping.
11. View ping through wall.
12. Walk around geometry while observing ping.
13. Ping enemy directly.
14. Enemy runs while pinged.
15. Enemy moves behind wall.
16. Verify marker follows enemy.
17. Wait for enemy ping expiration.
18. Kill enemy before ping expires.
19. Verify marker clears.
20. Enemy respawns.
21. Verify marker does not return.
22. Attempt to ping enemy through wall.
23. Verify wall receives location ping instead.
24. Place second ping before first expires.
25. Verify first ping clears.
26. Spam Ping button.
27. Verify cooldown prevents excessive network/UI creation.
28. Two teammates ping different locations.
29. Two teammates ping same enemy.
30. Client pings for Host teammate.
31. Host pings for Client teammate.
32. Client pings for another Client.
33. Player disconnects with active ping.
34. Match resets with active ping.
35. Verify D-Pad Right still switches grenades.
36. Verify ricochet prediction still functions independently.

---

# Out of Scope

Do NOT include the following as part of REQ-053:

- Voice dialogue/callouts
- Text chat
- Radial communication wheel
- Ping acknowledgement
- "On my way" responses
- Defend/attack/objective-specific ping types
- Weapon pickup pings
- Grenade pings
- Item pings
- Vehicle pings
- Custom player-selected ping colors
- Persistent enemy tracking
- Enemy detection through walls
- Automatic enemy scanning
- Minimap redesign
- Tactical map
- Ping statistics
- Final polished ping artwork
- Final audio assets

These may be added later.

---

# Desired Player Experience

A player notices danger:

```text
PLAYER A

       ENEMY
         |
         |
```

Player A aims at the enemy and presses:

```text
D-Pad Down
```

Immediately:

```text
        !
      ENEMY
```

Player A's teammates receive the warning.

The enemy runs around a corner:

```text
██████████████████████
                    !
                  ENEMY
```

The teammates still see the warning marker briefly through the wall.

They now understand:

> An enemy is moving behind this wall.

After approximately four seconds:

```text
Ping expires.
```

The enemy is no longer tracked.

---

A player may also simply warn teammates about a location:

```text
PLAYER A

       suspicious doorway
              |
              v
```

Press Ping:

```text
             !
          DOORWAY
```

Teammates can see the warning marker even from the other side of nearby geometry.

The result should provide fast, intuitive teamwork without requiring voice chat.

---

# Control Summary

```text
GAMEPAD

D-Pad Right
→ Switch Grenade

D-Pad Down
→ Ping / Warning
```

```text
MOUSE + KEYBOARD

N
→ Switch Grenade

Middle Mouse Button
→ Ping / Warning
```

---

# Definition of Done

REQ-053 is complete when players can use **D-Pad Down on controller** or **Middle Mouse Button on Mouse + Keyboard** to place a networked warning ping, allied players can see those warning markers through world geometry while enemies cannot, direct pings on enemy players create a short-lived marker that follows the enemy for approximately four seconds, and the system operates without revealing unobserved enemies, spamming the network, conflicting with existing controls, or interfering with combat systems.
````
