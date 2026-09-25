# REQ-075 Follow-Up — Lobby UI Spacing & Player List Cleanup

## Summary

The redesigned two-column lobby is a major improvement and should be preserved.

Do NOT redesign the overall lobby again.

This follow-up should make several targeted sizing, clipping, and spacing improvements based on the current implementation:

1. Fix clipping in the player-list header and rows.
2. Reduce unnecessary vertical space in the player-list panel.
3. Move the lobby action buttons upward with the shortened player list.
4. Make Start Match larger/more prominent.
5. Redesign the Join Code section as a compact single-line element.

---

# 1. Fix Player List Text Clipping

## Current Problem

Text on the LEFT side of the player roster is being clipped.

Examples visible in the current build include:

- "Player" column header being partially cut off.
- "Initializing..." / player-state text being cut off on the left.

This appears to be a layout, masking, padding, anchoring, or text-container issue.

## Required Behavior

All player-list text must render fully within the roster.

The header should clearly display its intended columns, for example:

Player                         Status

or the equivalent existing structure.

Player names/status text such as:

Initializing...

Waiting...

must begin with adequate left padding and must never have their first characters clipped.

Inspect the actual cause rather than compensating by adding spaces to strings.

Potential areas to inspect include:

- RectTransform bounds
- Content/Viewport width
- masks / RectMask2D
- horizontal layout groups
- child anchors
- text margins
- padding
- column widths

---

# 2. Shorten the Player List Panel

## Current Problem

The player roster is now tall enough for eight players, but the surrounding panel extends significantly farther downward than necessary.

There is a large unused area beneath the player rows.

## Required Change

Resize the player-list container so that it is only tall enough to comfortably contain:

- the column header
- all 8 player slots
- reasonable padding

All eight player slots must STILL remain simultaneously visible.

Do not introduce scrolling for an eight-player lobby.

The bottom of the player-list panel should end reasonably soon after Player 8 rather than extending far below the final row.

---

# 3. Move Right-Side Buttons Up

Because the player-list panel will become shorter, move the following buttons upward so that they sit naturally underneath it:

1. Change Map / Mode
2. Start Match

Maintain reasonable spacing between:

Player List

↓

Change Map / Mode

↓

Start Match

Avoid unnecessarily large empty gaps.

---

# 4. Make Start Match More Prominent

Start Match is the primary action for the lobby host.

Increase its visual prominence.

It should be:

- taller than Change Map / Mode
- easy to identify immediately
- large enough to feel like the primary action
- consistent with the existing visual style

Conceptually:

[ Change Map / Mode ]

[                         ]
[       START MATCH       ]
[                         ]

Do not make it excessively large, but it should clearly carry more visual weight than the secondary Change Map / Mode action.

Preserve existing Start Match functionality.

---

# 5. Redesign Join Code Into One Compact Row

## Current Problem

The Join Code section currently consumes too much vertical and horizontal space.

Currently:

- "JOIN CODE" sits well above the actual code.
- The code appears separately below it.
- Copy Code is far away from the code.
- Copy Code is excessively tall.
- The explanatory text is separated from the information it describes.

## Required Layout

Combine the label, actual code, and Copy Code button into ONE horizontal row.

Conceptually:

JOIN CODE:   76G8HB   [ Copy Code ]

Immediately underneath that row:

Share the join code with invited players.

The entire Join Code section should therefore be compact.

---

# 6. Join Code Visual Hierarchy

Within the single-line Join Code row:

**JOIN CODE:**  
Small/medium label.

**76G8HB:**  
Slightly more prominent so the actual code is easy to identify.

**Copy Code:**  
Compact secondary button immediately beside the code.

The Copy Code button should NOT be substantially taller than the text/code beside it.

Its dimensions should resemble a normal compact UI button rather than the large square/rectangular button currently displayed.

Keep enough spacing that the three elements are visually distinct:

JOIN CODE:    76G8HB    [Copy Code]

but they should clearly read as one component.

---

# 7. Explanatory Text

Place:

"Share the join code with invited players."

directly underneath the Join Code row.

Use smaller secondary/helper text.

There should only be a modest vertical gap between the Join Code row and this text.

---

# 8. Cancel Lobby Position

Cancel Lobby can remain underneath the Join Code/helper-text section.

With the Join Code section now significantly more compact, move Cancel Lobby upward accordingly.

Avoid leaving unnecessary empty space simply to preserve its previous screen position.

---

# 9. Preserve Current Overall Layout

IMPORTANT:

The current two-column redesign is successful.

Do NOT substantially change:

LEFT:
- Public Lobby indicator
- Map preview
- Map name/description
- Game Mode
- Join Code
- Cancel Lobby

RIGHT:
- Players
- Player roster
- Change Map / Mode
- Start Match

This ticket is about tightening and polishing the existing design.

---

# Acceptance Criteria

The follow-up is complete when:

1. "Player" is fully visible in the player-list header.
2. "Initializing..." and other player-row text is no longer clipped on the left.
3. All 8 player slots remain simultaneously visible.
4. The player-list panel ends reasonably close to the eighth player row.
5. There is no large unused vertical area inside the player-list panel.
6. Change Map / Mode moves upward underneath the shortened player list.
7. Start Match moves upward accordingly.
8. Start Match is larger/more visually prominent than Change Map / Mode.
9. Join Code label, code, and Copy Code button appear on the same horizontal row.
10. Copy Code is compact and positioned immediately beside the code.
11. "Share the join code with invited players." appears directly underneath the Join Code row.
12. Cancel Lobby moves upward with the more compact Join Code section.
13. The current two-column lobby structure is otherwise preserved.
14. No lobby, Relay, join-code, map-selection, or match-start functionality is changed.