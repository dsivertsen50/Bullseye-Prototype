# REQ-056 — In-Game Scoreboard & Match Statistics Overlay

## Summary

Add an in-game scoreboard that players can open at any time during an active match without pausing gameplay.

Controller controls:

```text
Menu / Start Button = Pause Menu
View / Select / Back Button = Scoreboard
```

Mouse and keyboard controls:

```text
Escape = Pause Menu
Tab = Scoreboard
```

The scoreboard should display current match statistics using the telemetry/statistics foundation introduced in REQ-055.

The initial scoreboard should focus on essential match information and should not attempt to display every telemetry statistic available.

This ticket should also establish a clean separation between:

```text
Current Match Statistics
```

and future:

```text
Lifetime / Career Statistics
```

Lifetime statistics will eventually be accessible from the main menu but are outside the scope of REQ-056.

---

# Goals

1. Add an in-game scoreboard.
2. Map the scoreboard to the Xbox-style View/Select button.
3. Map the scoreboard to Tab on keyboard.
4. Keep the existing Menu/Start and Escape pause behavior unchanged.
5. Display live current-match player statistics.
6. Use the authoritative statistics created by REQ-055.
7. Do not pause gameplay when the scoreboard is visible.
8. Make the scoreboard work correctly in multiplayer.
9. Build the UI so additional match statistics can be added later.
10. Keep match statistics separate from future lifetime statistics.

---

# 1. Input Mapping

Add a dedicated input action for:

```text
Scoreboard
```

Do not directly hardcode controller button polling inside the scoreboard UI script if the project already uses an input-action architecture.

Use the existing input system and conventions.

Default bindings should be:

## Controller

```text
Xbox View / Select / Back Button
```

This is the small button to the left of the Xbox/Home button.

The existing:

```text
Menu / Start Button
```

must continue opening the Pause Menu.

---

## Mouse and Keyboard

Use:

```text
Tab
```

for the scoreboard.

The existing:

```text
Escape
```

must continue opening the Pause Menu.

---

# 2. Hold-to-View Behavior

The scoreboard should initially behave as a **hold-to-view overlay**.

Example:

```text
Player holds Tab
    ↓
Scoreboard appears

Player releases Tab
    ↓
Scoreboard disappears
```

Controller:

```text
Player holds View / Select
    ↓
Scoreboard appears

Player releases View / Select
    ↓
Scoreboard disappears
```

Do not require a second press to close the scoreboard.

This behavior should feel similar to traditional multiplayer FPS scoreboards.

---

# 3. Gameplay Must Continue

Opening the scoreboard must **not pause the match**.

While the scoreboard is visible:

- Other players continue moving.
- Weapons continue functioning according to normal gameplay rules.
- Match timers continue.
- Physics continue.
- Network simulation continues.
- The local player remains vulnerable.

Do not change:

```csharp
Time.timeScale
```

or otherwise invoke Pause Menu behavior.

---

# 4. Scoreboard UI

Create an in-game scoreboard overlay.

The exact visual style can remain simple for now.

It should:

- Appear above gameplay.
- Be readable quickly.
- Use a semi-transparent background where useful.
- Clearly distinguish the local player.
- Support multiple players.
- Scale appropriately across common resolutions.

The UI should not require major final-art polish in this ticket.

Focus on functionality and structure.

---

# 5. Initial Scoreboard Columns

At minimum, display:

```text
Player
Eliminations
Assists
Deaths
```

Example:

```text
Player             E      A      D

Player 1           8      3      4
Player 2           6      1      6
Player 3           4      2      7
Player 4           2      5      5
```

Use full column labels if space permits.

Possible layout:

```text
PLAYER        ELIMINATIONS     ASSISTS     DEATHS
```

Do not use:

```text
Kills
```

All terminology must follow REQ-055:

```text
Eliminations
```

---

# 6. Player Identification

Use the existing player display-name system if one exists.

If proper player names do not exist yet, use the current appropriate identifier/display logic.

Examples might temporarily include:

```text
Player 1
Player 2
```

Do not expose raw internal IDs such as:

```text
ClientId: 281734
```

unless no player-facing display identifier currently exists.

---

# 7. Local Player Highlight

The local player's scoreboard row should be visually distinguishable.

This may be accomplished through:

- Background highlight.
- Font weight.
- Small indicator.
- Existing UI accent.

Do not overdesign this.

The player should be able to immediately identify their own row.

---

# 8. Live Match Statistics

The scoreboard must use the live current-match statistics created in REQ-055.

Do not create independent duplicate counters inside the scoreboard.

Conceptually:

```text
CombatTelemetryManager
        ↓
PlayerMatchStats
        ↓
Scoreboard UI
```

The scoreboard should be a **consumer** of authoritative match-stat data.

---

# 9. Multiplayer Synchronization

All players should see accurate scoreboard values.

Statistics such as:

```text
Eliminations
Assists
Deaths
```

should originate from authoritative multiplayer state.

The scoreboard must not calculate eliminations or assists independently on each client.

If REQ-055 provides a synchronized player-stat structure, use that structure.

Otherwise, expose the authoritative match-stat data to clients using the project's existing Netcode conventions.

---

# 10. Scoreboard Updates

The scoreboard should reflect changes during the match.

For example:

```text
Player A:
Eliminations = 4
```

Player A earns another elimination.

The scoreboard should then show:

```text
Eliminations = 5
```

when viewed.

Updates can occur either:

- Immediately as values change.
- When the scoreboard is opened.

Do not run expensive UI rebuild logic every frame unnecessarily.

---

# 11. Sorting

Initially sort scoreboard rows primarily by:

```text
Eliminations — Highest to Lowest
```

Recommended secondary sorting:

```text
Assists — Highest to Lowest
```

and then:

```text
Deaths — Lowest to Highest
```

If all values are tied, use a stable existing player ordering.

Example:

```text
1. Player A — 8 E / 2 A / 3 D
2. Player C — 6 E / 4 A / 5 D
3. Player B — 6 E / 1 A / 4 D
```

Do not constantly reorder rows in a visually disruptive way while the scoreboard is open if avoidable.

Updating order when statistics materially change is acceptable.

---

# 12. Free-for-All Compatibility

The current game is primarily being developed as a Free-for-All multiplayer FPS.

REQ-056 should therefore initially use a player-by-player scoreboard rather than team groupings.

Example:

```text
1 — Player A
2 — Player B
3 — Player C
4 — Player D
```

Do not create team-score architecture unless the project already has reusable team support.

Future team modes may extend this system later.

---

# 13. Score / Placement

If practical, display or derive the player's current placement based on eliminations.

Example:

```text
1
2
3
4
```

or:

```text
1st
2nd
3rd
4th
```

This is optional if the initial scoreboard layout already clearly communicates ordering.

Do not create a separate scoring system merely for placement.

---

# 14. Scoreboard vs Pause Menu

The scoreboard and Pause Menu must remain separate systems.

## Pause

```text
Controller:
Menu / Start

Keyboard:
Escape
```

Pause may:

- Open settings.
- Capture/release cursor.
- Provide leave-match functionality.
- Trigger whatever pause behavior currently exists.

---

## Scoreboard

```text
Controller:
View / Select

Keyboard:
Tab
```

Scoreboard:

- Does not pause.
- Does not open settings.
- Does not stop gameplay.
- Does not replace the Pause Menu.

---

# 15. Scoreboard While Paused

If the Pause Menu is already open, pressing the scoreboard button should not create conflicting overlapping interfaces.

Preferred behavior:

```text
Pause Menu open
    ↓
Scoreboard input ignored
```

Alternatively, if the existing UI architecture supports interface layering safely, Cursor may implement a clean alternative.

Do not allow both menus to fight for:

- Cursor state.
- Input focus.
- Canvas ordering.
- Player input state.

---

# 16. Pause While Scoreboard Is Open

If the player presses the Pause button while holding/opening the scoreboard:

Preferred behavior:

```text
Scoreboard closes
Pause Menu opens
```

Pause should take priority.

---

# 17. Cursor / Mouse Behavior

Opening the scoreboard must not unnecessarily release or capture the mouse.

Because this is a quick hold-to-view gameplay overlay:

```text
Mouse look should retain normal gameplay cursor-lock behavior.
```

The scoreboard is not initially interactive.

Players do not need to click scoreboard rows.

---

# 18. Player Control While Scoreboard Is Visible

The scoreboard is intended primarily as an information overlay.

Do not intentionally disable:

- Movement.
- Looking.
- Jumping.
- Crouching.
- Sprinting.

However, there is one important input consideration:

```text
Tab / View is being held.
```

That input itself must not conflict with another gameplay action.

Inspect existing controls before assigning it.

If Tab or View is currently used by another function, migrate that function appropriately rather than allowing both to trigger simultaneously.

---

# 19. Shooting While Viewing Scoreboard

Do not intentionally disable weapon use merely because the scoreboard is visible unless there is a strong existing architecture reason.

The player remains in active gameplay.

However, the scoreboard should visually cover enough of the screen that attempting to fight while viewing it naturally carries a disadvantage.

This matches normal FPS behavior.

---

# 20. Dynamic Player Count

The scoreboard must support players joining and leaving.

When a player joins:

```text
Add scoreboard row
```

When a player leaves:

```text
Remove scoreboard row
```

Do not assume a fixed number of players.

---

# 21. Disconnected Players

Once a player disconnects, remove them from the active scoreboard unless the existing match architecture intentionally preserves disconnected player results.

For REQ-056, active connected players are the priority.

---

# 22. Future Statistics Expansion

The scoreboard architecture should allow more columns or views later.

Potential future match statistics include:

```text
Body Slam Eliminations
Bullseyes Detached
Attached Bullseye Eliminations
Detached Bullseye Eliminations
Accuracy
Bullseye Hit Percentage
Longest Elimination
Weapon-specific Eliminations
```

Do not display all of these now.

The initial scoreboard should remain readable and uncluttered.

---

# 23. Detailed Match Statistics — Future Compatibility

REQ-056 should make it possible to later create either:

```text
Basic Scoreboard
```

and:

```text
Detailed Match Statistics
```

without replacing the current UI/data architecture.

For example, a future scoreboard could have tabs such as:

```text
Scoreboard
Combat
Weapons
```

This is future scope only.

---

# 24. Lifetime Statistics Separation

Bullseye will eventually have a separate system for persistent player career statistics.

Examples:

```text
Lifetime Eliminations
Lifetime Deaths
Lifetime Assists
Lifetime Body Slam Eliminations
Lifetime Bullseyes Detached
Weapon Eliminations
Longest Elimination
Total Matches
Wins
Accuracy
```

These should eventually be viewable from the Main Menu.

REQ-056 must **not** implement lifetime persistence.

However, avoid naming the current scoreboard/stat structures ambiguously.

Prefer terminology such as:

```text
PlayerMatchStats
MatchScoreboard
CurrentMatchStatistics
```

rather than generic names that imply lifetime data.

---

# 25. UI Architecture

Suggested structure:

```text
ScoreboardCanvas
    └── ScoreboardPanel
        ├── Header
        ├── ColumnHeaders
        └── PlayerRows
```

Each player row could use a reusable prefab such as:

```text
ScoreboardPlayerRow
```

containing:

```text
Player Name
Eliminations
Assists
Deaths
```

Cursor may use whatever structure best fits the existing UI system.

---

# 26. Scoreboard Controller

Prefer a dedicated UI controller such as:

```text
ScoreboardUI
```

or:

```text
MatchScoreboardController
```

Responsibilities may include:

- Showing/hiding scoreboard.
- Reading match-stat data.
- Creating/removing player rows.
- Updating displayed values.
- Sorting rows.

Do not put scoreboard UI logic directly into weapon or combat scripts.

---

# 27. Input State

Scoreboard visibility should conceptually respond to:

```text
Scoreboard Started/Pressed
    ↓
Show

Scoreboard Canceled/Released
    ↓
Hide
```

Use the project's Input System conventions.

Avoid implementing this as constant polling if an event-based input approach is already being used.

---

# 28. Network Architecture

Do not network the actual scoreboard UI.

Network/synchronize the **statistics data**, then let each client render its own scoreboard.

Conceptually:

```text
Server
    ↓
Authoritative PlayerMatchStats
    ↓
Clients
    ↓
Local Scoreboard UI
```

Not:

```text
Server controls each client's Canvas
```

---

# 29. Scoreboard Visibility Is Local

Whether a player has their scoreboard open is local UI state.

It does not need to be synchronized to other players.

Example:

```text
Player A holds Tab.
```

Only Player A sees the scoreboard.

Other players should receive no scoreboard-open notification unless a future feature explicitly requires it.

---

# 30. Initial Visual Design

The scoreboard should fit Bullseye's existing UI style where practical.

For now, prioritize:

- Strong readability.
- Clear hierarchy.
- Consistent typography.
- Semi-transparent panel.
- Clean spacing.
- Local-player highlighting.

Avoid spending substantial development time on final artwork.

A more polished scoreboard can be designed later.

---

# 31. Screen Coverage

The scoreboard should occupy a significant central portion of the screen but should not necessarily cover the entire viewport.

Suggested layout:

```text
Top-center / central panel
```

with enough background opacity that text remains readable over bright game scenes.

---

# 32. Match Context Header

If useful and easy to implement, include a small header such as:

```text
SCOREBOARD
```

Potential future fields may include:

```text
Game Mode
Match Time Remaining
Score Limit
Map Name
```

These are optional in REQ-056.

Do not create new match-rule infrastructure merely to populate them.

---

# 33. Scoreboard State on Death

Players should still be able to view the scoreboard while:

- Alive.
- Dead.
- Waiting to respawn.

Do not unnecessarily disable scoreboard input because the local player is currently eliminated.

This is particularly useful during respawn downtime.

---

# 34. End-of-Match Compatibility

The scoreboard architecture should later be reusable for an end-of-match results screen.

REQ-056 does not need to create the final results screen.

However:

```text
Player rows
Sorting
Match stats
Placement
```

should not be implemented in a way that prevents reuse.

---

# 35. Testing Requirements

Test using at least two connected players.

---

## Test A — Controller Scoreboard

1. Enter a match using controller.
2. Hold the Xbox View/Select button.

Expected:

```text
Scoreboard appears.
```

Release the button.

Expected:

```text
Scoreboard disappears.
```

---

## Test B — Controller Pause

Press:

```text
Menu / Start
```

Expected:

```text
Pause Menu opens.
```

The scoreboard should not open.

---

## Test C — Keyboard Scoreboard

Hold:

```text
Tab
```

Expected:

```text
Scoreboard appears.
```

Release Tab.

Expected:

```text
Scoreboard disappears.
```

---

## Test D — Keyboard Pause

Press:

```text
Escape
```

Expected:

```text
Pause Menu opens.
```

Tab behavior remains unchanged.

---

## Test E — Gameplay Continues

1. Player A opens scoreboard.
2. Player B continues moving around Player A.

Expected:

Player B continues moving normally.

Match time and gameplay continue.

---

## Test F — Player Remains Vulnerable

1. Player A holds scoreboard open.
2. Player B attacks Player A.

Expected:

Player A can still take damage and be eliminated.

---

## Test G — Statistics Display

Create a match state where:

```text
Player A:
5 Eliminations
2 Assists
3 Deaths
```

Expected scoreboard:

```text
Player A    5    2    3
```

---

## Test H — Live Update

1. Open scoreboard.
2. Observe Player A's eliminations.
3. Close scoreboard.
4. Player A earns another elimination.
5. Reopen scoreboard.

Expected:

Updated elimination count is displayed.

---

## Test I — Sorting

Given:

```text
Player A = 7 Eliminations
Player B = 4 Eliminations
Player C = 9 Eliminations
```

Expected order:

```text
Player C
Player A
Player B
```

---

## Test J — Local Player Highlight

Open scoreboard.

Expected:

The local player's row is immediately distinguishable from other players.

---

## Test K — Multiple Clients

Open scoreboard on Client A.

Expected:

Only Client A sees the scoreboard.

Client B's screen is unaffected.

---

## Test L — Pause Priority

1. Hold scoreboard open.
2. Press Pause.

Expected:

```text
Scoreboard closes.
Pause Menu opens.
```

No overlapping menus.

---

## Test M — Scoreboard During Pause

1. Open Pause Menu.
2. Attempt to open scoreboard.

Expected:

Scoreboard does not create conflicting UI.

---

## Test N — Respawn

1. Die.
2. While waiting to respawn, hold scoreboard input.

Expected:

Scoreboard can still be viewed.

---

## Test O — Player Join/Leave

1. Start multiplayer match.
2. Have another player join.

Expected:

New scoreboard row appears.

3. Have that player disconnect.

Expected:

Their active scoreboard row is removed.

---

# 36. Acceptance Criteria

REQ-056 is complete when:

- [ ] A dedicated Scoreboard input action exists.
- [ ] Xbox View/Select/Back opens the scoreboard.
- [ ] Xbox Menu/Start still opens Pause.
- [ ] Tab opens the scoreboard on mouse and keyboard.
- [ ] Escape still opens Pause.
- [ ] Scoreboard is hold-to-view.
- [ ] Releasing the scoreboard button hides it.
- [ ] Scoreboard does not pause gameplay.
- [ ] Players remain vulnerable while viewing it.
- [ ] Scoreboard displays player names/identifiers.
- [ ] Scoreboard displays Eliminations.
- [ ] Scoreboard displays Assists.
- [ ] Scoreboard displays Deaths.
- [ ] No player-facing "Kills" terminology remains in this UI.
- [ ] Values come from the authoritative match-stat system created in REQ-055.
- [ ] Statistics update correctly during a match.
- [ ] Players are sorted primarily by Eliminations.
- [ ] The local player's row is visually identifiable.
- [ ] The UI supports dynamic multiplayer player counts.
- [ ] Opening the scoreboard affects only the local client's UI.
- [ ] Scoreboard and Pause Menu cannot conflict.
- [ ] Scoreboard can be opened while waiting to respawn.
- [ ] The architecture remains extensible for additional match statistics.
- [ ] Current-match statistics remain conceptually separate from future lifetime statistics.

---

# 37. Implementation Guidance for Cursor

Before implementing:

1. Inspect the existing Input System configuration.
2. Identify the current Pause/Menu bindings.
3. Confirm the correct Input System control path for Xbox View/Select.
4. Confirm whether Tab is currently mapped to another gameplay action.
5. Inspect the existing HUD/UI Canvas hierarchy.
6. Inspect REQ-055's `PlayerMatchStats` or equivalent statistics structure.
7. Inspect how match-player identities are currently synchronized.
8. Reuse existing UI and networking conventions.

Do not create an unrelated second statistics system for the scoreboard.

The scoreboard should simply present the match statistics already being tracked.

---

# 38. Future Direction

REQ-056 establishes the **current-match scoreboard**.

A future requirement should create a separate Main Menu area such as:

```text
CAREER
STATISTICS
PLAYER PROFILE
```

where the player could eventually see persistent lifetime statistics such as:

```text
Matches Played
Wins
Eliminations
Deaths
Assists
Elimination/Death Ratio
Bullseyes Detached
Detached Bullseye Eliminations
Body Slam Eliminations
Eliminations by Weapon
Bullseye Accuracy
Longest Elimination
Average Elimination Distance
```

That system will require persistent player data and should be handled separately.

---

# Final Expected Result

During an active Bullseye match:

```text
Menu / Start
```

continues to open the Pause Menu.

Holding:

```text
View / Select
```

on controller or:

```text
Tab
```

on keyboard displays a live scoreboard containing:

```text
Player
Eliminations
Assists
Deaths
```

Gameplay continues normally behind the scoreboard.

Releasing the scoreboard input immediately returns the player to the unobstructed gameplay view.

The scoreboard uses REQ-055's authoritative match statistics and provides a foundation for richer match and lifetime statistics systems later.