# REQ-076 Follow-Up — Display Name Modal & Copy Code Navigation Fix

## Summary

REQ-076 is partially implemented, but two usability problems remain:

1. The Display Name virtual keyboard opens on top of the existing Profile screen and overlaps/interferes with the Profile UI.
2. The lobby Copy Code button still cannot be reached by controller/menu navigation.

Fix both issues without redesigning the underlying Profile or Lobby screens.

---

# 1. Display Name Keyboard — Current Problem

The virtual keyboard successfully appears when editing Display Name.

However, it currently appears within/over the normal Profile layout.

As a result, the keyboard visually overlaps:

- Profile title
- Display Name field
- Profile category tabs
- Overview statistics
- Back button
- controller instruction text

This makes the screen extremely cluttered and makes it difficult to understand which UI is currently active.

The virtual keyboard should NOT behave like another component inside the Profile page.

It should behave as a MODAL OVERLAY.

---

# 2. Create a Dedicated Display Name Modal

When the player activates:

Profile > Display Name

open a dedicated modal overlay above the Profile screen.

Conceptually:

------------------------------------------------

              EDIT DISPLAY NAME

              [ Player_ ]

        A   B   C   D   E   F   G
        H   I   J   K   L   M   N
        O   P   Q   R   S   T   U
        V   W   X   Y   Z

        0   1   2   3   4   5   6
        7   8   9

        [ SPACE ]   [ BACKSPACE ]

        [ CANCEL ]      [ CONFIRM ]

------------------------------------------------

The exact keyboard arrangement can be adjusted for readability and efficient controller navigation.

---

# 3. Hide/Suppress the Profile UI While Editing

While the Display Name modal is active, the normal Profile interface should NOT compete visually with it.

Preferred implementation:

- Add a dark/semi-transparent fullscreen modal backdrop.
- Dim the underlying Profile screen substantially.
- Disable interaction/navigation with underlying Profile controls.
- Display the virtual keyboard above that backdrop.

The player should visually understand:

"I am currently editing my Display Name."

Do NOT allow focus to move into:

- Overview
- Weapons
- Bullseye
- Profile statistics
- Save
- Back

while the keyboard modal is open.

Only modal controls should participate in navigation.

---

# 4. Keyboard Size

The current virtual keyboard is too large relative to the available menu area.

Reduce its overall footprint.

It should comfortably fit inside the menu portion of the screen without touching or extending beyond the screen boundaries.

Use a compact grid.

The keyboard does NOT need enormous individual character buttons.

Character buttons only need to be large enough that:

- the selected character is obvious
- controller navigation is readable
- mouse users can click them reliably

Preserve reasonable spacing between keys.

---

# 5. Display Name Preview

At the top of the modal, clearly show the name currently being constructed.

Example:

EDIT DISPLAY NAME

[ Player_ ]

As characters are entered, this text should update immediately.

The player should always be able to see what they have typed.

---

# 6. Modal Navigation

When the modal opens:

- automatically select the first appropriate keyboard control
- controller/D-pad navigation must remain entirely inside the modal
- mouse interaction must work
- keyboard input must continue working if supported
- Cancel closes the modal without saving
- Confirm saves the name and closes the modal

When the modal closes:

- restore Profile UI interaction
- return controller focus to a sensible Profile element, preferably Display Name

Do not leave the EventSystem with no selected object after closing the modal.

---

# 7. Copy Code — Current Problem

The lobby Copy Code button exists visually, but controller/menu navigation still cannot land on it.

This means REQ-076 is NOT complete.

Do not simply verify that the Button component has `interactable = true`.

Diagnose WHY the EventSystem/navigation system cannot select it.

---

# 8. Inspect Copy Code Navigation

Inspect the actual lobby UI hierarchy and navigation configuration.

Check for issues including:

- Button `interactable` state
- Unity Selectable Navigation mode
- Explicit navigation links
- Automatic navigation choosing another element
- missing/disabled Selectable component
- CanvasGroup `interactable`
- CanvasGroup `blocksRaycasts`
- parent objects preventing interaction
- EventSystem selection behavior
- custom menu-navigation scripts
- dynamically generated lobby controls
- navigation rebuilding when the lobby opens
- Copy Code being excluded from a custom selectable/button list

Determine the actual cause before applying the fix.

---

# 9. Required Lobby Navigation Path

The Copy Code button MUST be reachable through controller navigation.

Given the current lobby layout:

LEFT COLUMN:
- Join Code / Copy Code
- Cancel Lobby

RIGHT COLUMN:
- Player List
- Change Map / Mode
- Start Match

A sensible navigation path should exist.

For example:

When Cancel Lobby is selected:

UP → Copy Code

When Copy Code is selected:

DOWN → Cancel Lobby

There should also be a sensible horizontal route between the left and right columns where appropriate.

The exact graph may depend on the existing menu-navigation system, but Copy Code must never be skipped.

---

# 10. Copy Code Focus Appearance

When Copy Code receives focus, it must visibly display the same selected/highlighted state as other menu buttons.

Example:

JOIN CODE: 76G8HB  [ COPY CODE ]

                       ↑
                  selected state

The player should immediately know that Copy Code is the active control.

---

# 11. Verify Actual Clipboard Operation

Once Copy Code can be selected, confirm that activating it actually performs the clipboard operation.

Activation must copy the CURRENT lobby code.

Example:

Displayed:

JOIN CODE: 76G8HB

Clipboard receives exactly:

76G8HB

Do not hard-code the example value.

---

# 12. Copy Confirmation

After activation, provide brief feedback such as:

[ Copied! ]

Then restore:

[ Copy Code ]

after approximately 1–2 seconds.

IMPORTANT:

The button should REMAIN SELECTED while this temporary text changes.

Do not cause controller focus to disappear merely because the label changes.

---

# 13. Test Copy Code With Controller Specifically

Do not consider this issue resolved merely because clicking Copy Code with a mouse works.

Explicitly verify the navigation sequence using controller/D-pad navigation:

1. Open/create lobby.
2. Navigate through lobby controls.
3. Move selection onto Copy Code.
4. Observe its highlighted state.
5. Activate it using the normal controller Submit button.
6. Verify "Copied!" feedback.
7. Verify that the clipboard contains the current lobby code.
8. Continue navigating afterward and verify focus still works.

This is specifically a controller-navigation bug.

---

# Acceptance Criteria

## Display Name

1. Activating Display Name opens a dedicated modal.
2. The normal Profile screen is dimmed/suppressed behind it.
3. Underlying Profile controls cannot receive focus while the modal is open.
4. The virtual keyboard fits comfortably on screen.
5. Keyboard controls do not overlap Profile controls.
6. The current Display Name is clearly visible above the keyboard.
7. Controller navigation remains within the modal.
8. Confirm saves and closes the modal.
9. Cancel closes the modal without saving.
10. Focus returns appropriately to Profile after closing.

## Copy Code

11. Copy Code can be reached using controller/D-pad navigation.
12. Copy Code visibly highlights when selected.
13. The EventSystem actually selects the Copy Code Button.
14. Activating it with the controller copies the current lobby code.
15. Clipboard content contains only the lobby code.
16. "Copied!" feedback appears temporarily.
17. Focus remains functional after copying.
18. Mouse interaction with Copy Code continues to work.
19. Existing lobby navigation and functionality remain intact.

## Scope

Do NOT redesign:

- Profile statistics
- Profile categories
- lobby layout
- networking
- Relay
- player identity architecture

This is a targeted REQ-076 correction.