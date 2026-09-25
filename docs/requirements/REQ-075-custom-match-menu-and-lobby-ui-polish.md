# REQ-075 — Custom Match Menu & Lobby UI Polish

## Summary

Polish the existing Custom Match and lobby menu experience.

This requirement is intentionally focused on relatively small UI/navigation improvements. Do not redesign the multiplayer architecture or implement matchmaking as part of this ticket.

The primary goals are:

1. Make vertical controller navigation through game configuration predictable.
2. Replace the placeholder game modes with the five planned Bullseye game modes.
3. Improve the size/readability of the created-lobby overlay.
4. Replace the rotating capsule on the menu background with the actual player prefab.

---

## 1. Custom Match Selector Navigation

### Current Behavior

The Custom Match configuration contains horizontally arranged options for categories such as:

- Match Type
- Game Mode
- Map

When navigating downward through these categories, the selector correctly enters each new row on its left-most option.

However, when navigating upward from the Map row back to Game Mode, the selector can jump to the right-most Game Mode option.

This feels inconsistent and unpredictable.

### Required Behavior

Whenever the player moves vertically from one horizontal selection row to another:

- The newly entered row should select its LEFT-MOST option.
- This should occur whether the player entered the row by moving UP or DOWN.

Example:

Game Mode
[Free-For-All] [Team Eliminations] [Capture the Flag] [...]

Maps
[Fief] [Map 2] [Map 3] [...]

If the player is currently selecting Map 6 and presses UP:

→ selection should move to Free-For-All.

It should NOT attempt to preserve the horizontal position and select the game mode furthest to the right.

Likewise, moving DOWN from any Game Mode option into Maps should begin at the left-most map.

### Scope

Inspect the existing navigation implementation rather than hard-coding special behavior specifically for Game Mode and Maps if a reusable row-navigation solution already exists.

Mouse/keyboard and controller navigation should remain functional.

---

## 2. Replace Game Mode Placeholders

The game will now contain FIVE planned game modes rather than four.

Update the Custom Match Game Mode selection UI to display the following modes and descriptions.

### Free-For-All

**Description:**

> No one is your friend. Most eliminations wins.

### Team Eliminations

**Description:**

> You have some friends... and enemies. The team with the most eliminations wins.

### Capture the Flag

**Description:**

> The classic test of conquest. Capture the enemy flag to score points.

### Bullseye Ball

**Description:**

> Don't like ball sports? Try this one. Hold the bullseye to score points.

### Zoned Out

**Description:**

> King of the hill with a twist. Keep your bullseye in the zone to score points.

### Important

This ticket does NOT require implementing the gameplay rules for all five modes.

For modes that do not yet exist, these should remain menu/configuration placeholders.

The purpose of this change is to establish the intended five-mode menu presentation.

The existing Custom Match system should not break when an unimplemented mode is selected. If necessary, preserve existing placeholder/fallback behavior.

---

## 3. Created Lobby Overlay Improvements

After creating a lobby, the resulting lobby menu currently feels too small for the amount of information displayed.

### Required Changes

Increase the width of the lobby menu/panel by extending it further toward the RIGHT side of the screen.

The objective is to provide more horizontal room without unnecessarily covering the entire screen.

### Player List

Inspect the player-list area.

Currently, some player information can become clipped or extend beyond the available UI area.

Adjust:

- panel width
- text containers
- anchors
- padding
- layout groups
- Content Size Fitters or equivalent UI components

as appropriate so that normal player-list information is fully readable.

Do not solve this simply by making all text extremely small.

The UI should gracefully support the intended multiplayer lobby size.

### Join Code

Reduce the size of the Join Code text.

The code should remain:

- easy to locate
- easy to read
- visually distinct

but it currently occupies too much visual space relative to the rest of the lobby information.

Preserve the existing join-code functionality.

---

## 4. Replace Menu Capsule With Player Prefab

### Current Behavior

The menu background contains a capsule representing the player.

The capsule slowly rotates in place.

The slow rotation is desirable and should be retained.

### Required Change

Remove/hide the placeholder capsule and replace it with the actual third-person player prefab/model used by the game.

The player should occupy approximately the same presentation area currently occupied by the capsule.

### Rotation

The menu player should continue rotating slowly in place similarly to the existing capsule.

This should be presentation-only.

Do not rotate or otherwise modify the gameplay player prefab itself in a way that affects matches.

If necessary, instantiate a menu-specific presentation copy of the player.

### Future Animation Support

We eventually intend for the menu character to perform looping dance/emote animations while slowly rotating.

Dance animations are NOT required in REQ-075.

However, implement the menu player presentation in a way that makes it straightforward to assign an Animator/controller or looping animation later.

Desired eventual behavior:

Player Model
→ idle/dance animation
→ animation continues in place
→ entire presentation character slowly rotates for menu display

### Menu-Specific Player State

The menu presentation character should not initialize gameplay systems unnecessarily.

Where practical, avoid activating systems such as:

- player input
- movement
- weapons
- combat
- networking ownership behavior
- HUD
- gameplay cameras
- bullseye gameplay logic

The menu character is a visual presentation object, not an active player.

Reuse the existing visual prefab/model architecture where practical rather than creating a completely separate character asset that will become difficult to keep synchronized with the actual player appearance.

---

## 5. Regression Requirements

REQ-075 must not break:

- controller menu navigation
- keyboard/mouse menu navigation
- Custom Game creation
- lobby creation
- join-code generation
- existing public/private lobby behavior
- mode selection
- map selection
- local multiplayer testing
- Relay multiplayer functionality

Do not substantially refactor multiplayer/networking systems merely to accomplish these UI changes.

---

## Acceptance Criteria

REQ-075 is complete when:

1. Moving vertically between Match Type, Game Mode, and Map rows always enters the new row at its left-most option.
2. Five Game Mode options are visible:
   - Free-For-All
   - Team Eliminations
   - Capture the Flag
   - Bullseye Ball
   - Zoned Out
3. Each game mode displays its specified description.
4. Unimplemented modes can exist as placeholders without breaking Custom Match creation.
5. The created-lobby panel extends farther to the right and provides more usable space.
6. Player-list information no longer clips under normal lobby conditions.
7. Join-code text is smaller while remaining clearly readable.
8. The menu's placeholder capsule is replaced by the actual player visual/prefab.
9. The menu player slowly rotates similarly to the previous capsule.
10. The menu player does not behave as an active gameplay/network player.
11. The menu player setup is ready for a looping dance/emote animation to be assigned in a future requirement.
12. Existing Custom Game, lobby, controller, and Relay functionality continues to work.