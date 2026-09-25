# REQ-076 — Profile Display Name Entry & Functional Copy Code

## Summary

Several menu controls currently appear interactive but cannot actually be used.

This requirement addresses two issues:

1. The Profile > Display Name field must allow the player to enter and save a display name.
2. Lobby Copy Code buttons must be selectable and must actually copy the lobby code to the system clipboard.

Both features must support mouse/keyboard AND controller-driven menu navigation.

---

# 1. Profile Display Name — Current Problem

In the main menu:

Profile
→ Display Name

there is currently a text-entry field for the player's display name.

However, selecting the field does not provide a usable way to enter text.

The player must be able to create and edit their display name.

---

# 2. Keyboard / Mouse Display Name Entry

When using mouse and keyboard:

- Clicking/selecting the Display Name field should focus it.
- The player should be able to type normally using a physical keyboard.
- Backspace/Delete should function normally.
- Existing text should be editable.
- Enter should confirm the name where appropriate.
- Clicking/selecting another UI element should commit or appropriately end editing.

Use the project's existing Unity UI system and input architecture rather than creating an unrelated text-entry system if an appropriate input-field component already exists.

---

# 3. Controller Display Name Entry

The Display Name field must also be usable without a physical keyboard.

When a controller user selects/activates Display Name, open an in-game virtual keyboard.

Conceptually:

DISPLAY NAME

[ Cade___________ ]

A B C D E F G H I
J K L M N O P Q R
S T U V W X Y Z

0 1 2 3 4 5 6 7 8 9

[ SPACE ] [ BACKSPACE ]

[ CANCEL ]       [ CONFIRM ]

Exact layout can be adjusted to fit the existing Bullseye menu style.

### Controller Navigation

The player should be able to:

- navigate characters using D-pad or left stick
- select a character using the normal UI Submit/Confirm button
- use Backspace
- insert a space
- Confirm the completed name
- Cancel without unintentionally changing the saved name

Use the project's existing controller/UI input mappings where possible.

Do not interfere with gameplay controls.

---

# 4. Display Name Validation

Add reasonable basic validation.

The display name should:

- not be empty
- have a configurable maximum character length
- trim unnecessary leading/trailing whitespace
- support ordinary letters and numbers
- support spaces where reasonable

Do not build a profanity-filtering or account-moderation system in this ticket.

If the existing player/profile architecture already specifies display-name restrictions, preserve those rather than creating conflicting rules.

---

# 5. Persist Display Name

Once confirmed, the player's Display Name should persist rather than resetting every time the Profile menu is opened.

Inspect the existing persistent-player/profile foundation before adding a new storage mechanism.

If REQ-062 or subsequent profile work already created persistent player identity/profile data, integrate Display Name into that existing architecture.

Do NOT create a second competing profile-storage system.

At minimum, the saved Display Name should survive:

- closing/reopening the Profile menu
- returning to the main menu
- entering a lobby

Where supported by the existing profile architecture, it should also survive restarting the game.

---

# 6. Use Display Name in Multiplayer UI

Where the existing multiplayer system displays the local player's name, use the saved Display Name when practical.

For example:

PLAYERS 2 / 8

Cade                HOST
PlayerTwo
Waiting...
...

Do not redesign networking identity or account authentication as part of REQ-076.

This is simply the player-facing display name.

If networking requires a separate internal player ID, preserve that internal ID.

Display Name and unique/internal Player ID should remain separate concepts.

---

# 7. Copy Code — Current Problem

The lobby contains a:

[ Copy Code ]

button beside the Join Code.

However:

- it is not currently selectable through normal menu navigation
- controller focus cannot reliably reach it
- it may not actually copy anything to the system clipboard

Make this a fully functional UI button.

---

# 8. Copy Code Navigation

Copy Code must participate in the normal menu navigation system.

It must be selectable using:

- mouse
- keyboard navigation
- controller D-pad
- controller stick navigation

When selected, it should use the same highlighted/focused visual state as other menu buttons.

Ensure the navigation path around the Join Code section makes sense.

Do not allow the focus to become trapped on the Copy Code button.

---

# 9. Clipboard Functionality

Activating Copy Code should copy ONLY the current lobby code to the operating system clipboard.

Example:

JOIN CODE: 76G8HB [Copy Code]

Selecting Copy Code should place:

76G8HB

onto the clipboard.

Do NOT copy:

"JOIN CODE: 76G8HB"

or other explanatory text.

Use an appropriate Unity/system clipboard implementation that works in the project's supported desktop builds.

The clipboard action should use the CURRENT generated lobby code and should not contain a hard-coded example code.

---

# 10. Copy Confirmation

Provide immediate visual feedback after successfully activating Copy Code.

For example:

[ Copy Code ]

temporarily becomes:

[ Copied! ]

After approximately 1–2 seconds, it can return to:

[ Copy Code ]

Alternatively, a small "Copied!" message may briefly appear beside the button if that works better with the current UI architecture.

The feedback should be noticeable but unobtrusive.

Do not open another popup just to confirm the copy operation.

---

# 11. Multiple Lobby Screens

Inspect the project for every place where a lobby Join Code and Copy Code button can appear.

If the same reusable lobby component is used, fix the shared component.

If public/private/custom lobby screens contain separate implementations, ensure the relevant Copy Code buttons behave consistently.

Avoid fixing only one screen if the same nonfunctional control exists elsewhere.

---

# 12. Important Scope Constraints

REQ-076 does NOT require:

- account creation
- Steam identity integration
- Unity Authentication redesign
- matchmaking
- parties
- friend lists
- profanity filtering
- cloud profile synchronization
- networking refactors

This requirement is specifically about making existing menu interactions functional.

---

# Acceptance Criteria

REQ-076 is complete when:

1. Profile > Display Name can be selected.
2. Physical keyboard users can type/edit a Display Name.
3. Controller users can enter a Display Name through an in-game virtual keyboard.
4. Controller users can navigate, type, backspace, confirm, and cancel.
5. Display Name has reasonable length/empty-name validation.
6. Confirmed Display Name is stored using the existing profile/persistence architecture.
7. The saved Display Name appears again when returning to Profile.
8. The saved Display Name is used in existing multiplayer player-name displays where appropriate.
9. Internal multiplayer/player IDs remain separate from Display Name.
10. Copy Code can be selected with mouse, keyboard, and controller.
11. Copy Code visually highlights when focused.
12. Activating Copy Code copies the CURRENT lobby code to the system clipboard.
13. Only the code itself is copied.
14. Copy Code briefly provides "Copied!" feedback.
15. Copy functionality works wherever the relevant lobby Copy Code control appears.
16. Existing menu navigation, profile functionality, lobby functionality, and Relay functionality remain intact.