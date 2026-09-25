# REQ-075 Follow-Up — Redesign Lobby Layout

## Problem

The REQ-075 lobby sizing changes did not produce a good lobby layout.

The current lobby screen has several visual problems:

- The player list is far too wide.
- Despite its width, the player list is not tall enough to display all 8 player slots.
- The player list overlaps/covers the map description.
- Match information, lobby information, and controls compete for the same central screen area.
- There is excessive horizontal space devoted to the player table even though each player row contains very little information.
- The overall visual hierarchy is unclear.

Do not continue trying to fix this by incrementally widening or shrinking the existing player-list panel.

Instead, rearrange the lobby into a clear TWO-COLUMN layout.

---

# 1. Overall Layout

Divide the lobby overlay into two primary columns:

LEFT COLUMN:
Match information and lobby-management information.

RIGHT COLUMN:
Player roster and match-start controls.

Conceptually:

+---------------------------------------------------------------+
|                                                               |
|   MATCH INFORMATION              |   PLAYERS 1 / 8             |
|                                  |                             |
|   [ MAP PREVIEW ]                |   Player 1        HOST      |
|   Map Name                       |   Waiting...                 |
|   Map Description                |   Waiting...                 |
|                                  |   Waiting...                 |
|   GAME MODE                      |   Waiting...                 |
|   Free-For-All                   |   Waiting...                 |
|   No one is your friend.         |   Waiting...                 |
|   Most eliminations wins.        |   Waiting...                 |
|                                  |                             |
|   JOIN CODE                      |   [ Change Map / Mode ]      |
|   R766NG   [Copy Code]           |   [     Start Match    ]     |
|                                  |                             |
|   [ Cancel Lobby ]               |                             |
|                                                               |
+---------------------------------------------------------------+

This diagram is conceptual rather than pixel-perfect. Preserve the existing visual style while implementing this hierarchy.

---

# 2. Left Column — Map Information

Place the selected map information in the UPPER-LEFT portion of the lobby.

Display:

- Map preview image
- Map name
- Map description

The map description must have enough dedicated space that it does NOT extend underneath or behind the player list.

The map information should remain visually grouped.

For example:

[Map Preview]

Fief

"A medieval castle arena built around..."

The exact arrangement of the image/name/description can be adjusted to produce the cleanest result.

---

# 3. Left Column — Game Mode

Immediately beneath the map information, display the selected game mode.

Display:

GAME MODE

Free-For-All

"No one is your friend. Most eliminations wins."

The mode name should be more visually prominent than its description.

The mode description must wrap cleanly within the LEFT column.

Do not allow it to extend into the player-list area.

---

# 4. Left Column — Join Code

Place the lobby Join Code beneath the Game Mode information.

Example:

JOIN CODE

R766NG     [Copy Code]

The join code should be clearly readable but should NOT use an excessively large font.

Keep the existing Copy Code functionality.

The Join Code and Copy Code button should preferably occupy the same horizontal row if there is adequate room.

---

# 5. Left Column — Cancel Lobby

Place:

[ Cancel Lobby ]

beneath the Join Code section.

This should be the primary destructive/back-out action on the left side.

Preserve the existing Cancel Lobby functionality.

---

# 6. Right Column — Player List

Move the player roster entirely to the RIGHT side of the lobby.

The player list should become:

- NARROWER horizontally
- SIGNIFICANTLY TALLER vertically

The roster must be capable of displaying ALL 8 PLAYER SLOTS simultaneously without clipping.

Scrolling should NOT be necessary for an 8-player lobby.

Conceptually:

PLAYERS 1 / 8

---------------------------------
Player             Status
---------------------------------
PlayerName         HOST
Waiting...
Waiting...
Waiting...
Waiting...
Waiting...
Waiting...
Waiting...
---------------------------------

The exact columns can be refined based on the existing data model.

For now, the player roster primarily needs room for:

- Player name
- Host status

Retain any important existing player-state information if currently implemented.

Do not devote large amounts of horizontal space to information that does not exist yet.

---

# 7. Future Player Information

Design the roster so that another SMALL column could reasonably be added later.

For example:

Player | Rank | Status

or:

Player | Level | Host

Do NOT implement a ranking/level system as part of this ticket.

The goal is simply to avoid creating a layout that would have to be completely rebuilt when one additional compact player attribute is introduced later.

---

# 8. Right Column — Change Map / Mode

Directly UNDER the player list, place:

[ Change Map / Mode ]

This button should remain available to the lobby host according to the existing behavior.

Preserve its current functionality.

---

# 9. Right Column — Start Match

Directly UNDER Change Map / Mode, place:

[ Start Match ]

This should be the lowest and most visually prominent action on the right side.

Conceptually:

[ Change Map / Mode ]

[      Start Match      ]

The Start Match button should visually read as the primary action.

Do not change the underlying match-start logic.

---

# 10. Public/Private Match Label

The current large centered "PUBLIC MATCH" heading consumes substantial prime screen space.

Reduce its prominence.

It may remain as a small label indicating lobby visibility, such as:

PUBLIC MATCH

or:

PUBLIC LOBBY

This could appear near the top of the left Match Information column.

Do not remove the public/private distinction from the underlying lobby system.

---

# 11. Preserve Background Character

The menu player character from REQ-075 should remain visible in the background.

The new lobby overlay should not unnecessarily cover the entire screen.

Where practical, preserve some negative space so that the slowly rotating menu character remains part of the lobby presentation.

Future dance/emote animation support should remain unaffected.

---

# 12. Responsive Layout / Anchoring

Do not accomplish this redesign solely through arbitrary absolute coordinates that only look correct at the current development resolution.

Inspect and appropriately configure Unity UI:

- anchors
- pivots
- layout groups
- Content Size Fitters
- text wrapping
- panel sizing

The lobby should remain coherent at supported aspect ratios/resolutions.

Exact scaling behavior can follow the project's existing Canvas Scaler configuration.

---

# Acceptance Criteria

The follow-up is complete when:

1. The lobby uses a clear two-column layout.
2. Map information appears in the upper-left area.
3. The map description never extends behind the player roster.
4. Game Mode information appears below the map information.
5. Join Code information appears below Game Mode.
6. Cancel Lobby appears below the Join Code.
7. The player roster occupies the right side.
8. The player roster is narrower than the current implementation.
9. ALL 8 player slots are visible simultaneously.
10. No player rows are clipped.
11. The roster has enough flexibility for a future compact Rank/Level column.
12. Change Map / Mode appears below the player roster.
13. Start Match appears below Change Map / Mode.
14. Start Match is visually identifiable as the primary host action.
15. The oversized PUBLIC MATCH heading is reduced in prominence.
16. Existing lobby functionality remains intact.
17. Existing Join Code and Copy Code functionality remains intact.
18. Existing Change Map / Mode functionality remains intact.
19. Existing Start Match functionality remains intact.
20. Existing Cancel Lobby functionality remains intact.
21. The rotating menu player remains visible in the background where practical.
22. The layout behaves reasonably across supported screen sizes/aspect ratios.

## Important Scope Constraint

This is a UI/layout correction.

Do NOT use this follow-up as an opportunity to:

- rewrite Relay
- redesign lobby networking
- change lobby capacity
- implement ranking
- implement matchmaking
- implement parties
- change game-mode logic
- change map-loading logic

Reuse the existing lobby functionality and reorganize its presentation.