# REQ-067 — Public/Private Lobby Join Code Visibility + Join Code Entry System

## Summary

Inspect and fix the current multiplayer lobby/join flow so that players can actually join another player's lobby before a match begins.

There are currently two suspected issues:

1. When hosting a publicly joinable or Relay-based lobby, the **join code does not appear until after the match starts**.
2. When selecting the option to join another game, there does not appear to be a complete method for **entering the join code**.

REQ-067 should inspect the current implementation and correct both issues.

The desired experience is:

```text
HOST
→ Create lobby
→ Join code immediately appears in lobby
→ Host can share code
→ Other player selects Join Game
→ Enters code using keyboard OR virtual controller keyboard
→ Joins lobby
→ Both players appear in lobby player list
→ Host starts match
```

The join code must therefore exist and be usable during the **lobby phase**, not only after gameplay begins.

---

# 1. Inspect Existing Multiplayer Flow

Before changing the implementation, inspect the systems introduced by the recent multiplayer requirements, particularly:

- REQ-064 Relay multiplayer
- REQ-065 game mode/map/lobby menu
- Current lobby creation
- Current Relay allocation creation
- Current Relay join-code generation
- Current Join Game menu
- Current lobby player list
- Match start transition
- Local multiplayer testing path

Determine:

- When the Relay allocation is currently created.
- When the Relay join code is currently generated.
- Why the code only appears after the match starts.
- Whether the Join Game menu already has incomplete input logic.
- Whether any join-code input UI exists but is not interactable.
- Whether keyboard input works.
- Whether controller navigation works.
- Whether joining currently expects a Relay code, lobby ID, or some other value.

Do not build a second multiplayer connection system if the existing one can be repaired.

---

# 2. Join Code Must Exist in Lobby

When a host creates a multiplayer lobby that supports remote joining, the Relay allocation/join code should be created **before the match begins**.

The code should become available as soon as the host reaches the lobby screen.

Example:

```text
PRIVATE LOBBY

Game Mode:
Bullseye FFA

Map:
Prototype Arena

JOIN CODE
─────────
AB7KQ9
─────────

Players
1. PlayerOne
2. Waiting...
3. Waiting...
4. Waiting...

[ START MATCH ]
```

The host should not need to start gameplay before receiving the join code.

---

# 3. Display Join Code Clearly

The lobby screen should prominently display the active join code.

Suggested format:

```text
JOIN CODE

AB7KQ9
```

The code should:

- Be large enough to read easily.
- Have strong visual contrast.
- Remain visible while the player is in the lobby.
- Not disappear when another player joins.
- Remain the same for the lifetime of that lobby/session unless Relay requires otherwise.

If practical, include:

```text
COPY CODE
```

for mouse/keyboard users.

Clipboard support is optional but desirable.

---

# 4. Join Code Availability

The join code should be available once:

```text
Host selects Create/Host Game
        ↓
Relay allocation is successfully created
        ↓
Join code is received
        ↓
Lobby screen opens/displays code
```

Preferably, create the Relay allocation before or during entry into the lobby.

If generating the code takes noticeable time, display:

```text
Creating lobby...
```

or:

```text
Generating join code...
```

rather than displaying an empty field.

---

# 5. Lobby Must Be Joinable Before Match Start

Remote players must be able to connect while the host is still sitting in the lobby.

Joining should NOT depend on:

- Gameplay scene loading.
- Match timer beginning.
- Player spawning.
- Host pressing Start Match.

The lobby should already be an active network session.

---

# 6. Join Game Menu

Inspect the existing Join Game option.

Selecting:

```text
JOIN GAME
```

should open a dedicated join-code entry screen.

Example:

```text
JOIN GAME

Enter Join Code:

[ _ _ _ _ _ _ ]

[ JOIN ]

[ BACK ]
```

The exact number of characters should match the Relay join-code format being used.

Do not hardcode six characters if the current Relay implementation supports a different length.

---

# 7. Physical Keyboard Input

Players using mouse/keyboard should be able to type directly into the join-code field.

Support:

- Letters
- Numbers, if Relay codes can contain them
- Backspace
- Delete if practical
- Enter/Return to submit
- Escape to cancel/back out

Input should automatically normalize appropriately.

For example:

```text
ab7kq9
```

may internally become:

```text
AB7KQ9
```

if Relay codes are case-insensitive or conventionally uppercase.

Do not require the user to manually activate Caps Lock.

---

# 8. Controller / Gamepad Code Entry

Players using a controller must be able to enter a join code without requiring a physical keyboard.

Create an on-screen virtual keyboard.

Example:

```text
JOIN CODE

A B 7 K _ _


A B C D E F G H
I J K L M N O P
Q R S T U V W X
Y Z 0 1 2 3 4 5
6 7 8 9

[ BACKSPACE ]    [ CLEAR ]

       [ JOIN ]
```

The exact layout can be adjusted for usability.

---

# 9. Virtual Keyboard Navigation

Gamepad controls should follow standard menu behavior.

Suggested controls:

### Left Stick / D-Pad
Navigate between virtual keyboard characters/buttons.

### A / South Face Button
Select highlighted character.

### B / East Face Button
Backspace or cancel depending on context.

Recommended:

While keyboard is active:

```text
B = Backspace
```

If the input is empty:

```text
B = Return to previous menu
```

### Start / Menu Button

Optional shortcut:

```text
Submit Join Code
```

if a valid code has been entered.

---

# 10. Input System Compatibility

Use the project's existing Unity Input System/controller architecture.

Do not build keyboard navigation using legacy input APIs if the rest of the menu system uses the newer Input System.

The virtual keyboard should work with:

- Xbox controller
- Keyboard
- Mouse

It should also remain compatible with other standard gamepads supported by Unity's Input System.

---

# 11. Virtual Keyboard Character Set

Only display characters that can actually appear in the Relay join code.

Do not include unnecessary characters such as:

```text
!
@
#
$
%
&
```

if Relay does not use them.

Inspect the format produced by Unity Relay and configure the virtual keyboard appropriately.

---

# 12. Code Entry Display

The currently entered code should always be visible.

Example:

```text
JOIN CODE

AB7K_
```

Characters should appear immediately when selected.

Allow:

```text
Backspace
Clear All
```

The user should not need to restart the menu because of a typo.

---

# 13. Join Button State

The JOIN button should only be enabled when there is enough input to attempt a valid join.

Example:

```text
[ JOIN ]
```

becomes interactable once a potentially valid code exists.

However, validation should ultimately come from the networking/Relay system rather than assuming a code is valid simply because it has the expected number of characters.

---

# 14. Joining Process

When JOIN is selected:

```text
Entered Code
        ↓
Validate basic format
        ↓
Attempt Relay connection
        ↓
Connect to host
        ↓
Load/enter host lobby
        ↓
Add joining player to lobby player list
```

Display a temporary state:

```text
Joining lobby...
```

while the network connection is being attempted.

Prevent duplicate join requests caused by repeatedly pressing the Join button.

---

# 15. Successful Join

After a successful connection, the joining player should arrive at the same lobby screen as the host.

They should see:

- Lobby/player list
- Host
- Other connected players
- Selected game mode
- Selected map
- Lobby state

The host should immediately see the new player added to the lobby player list.

Example:

```text
PLAYERS

1. Cade
2. PlayerTwo
3. Waiting...
4. Waiting...
```

---

# 16. Host vs Client Controls

Preserve the behavior defined in REQ-065.

### Host

Can control:

- Game mode
- Map
- Start Match
- Potential future lobby settings

### Joining Client

Can view:

- Game mode
- Map
- Player list
- Lobby information

But should NOT be able to start the game or change host-owned settings unless intentionally supported later.

---

# 17. Invalid Code Handling

If the code does not correspond to a valid lobby, display a clear message.

Example:

```text
Unable to join lobby.

Check the join code and try again.
```

Do not:

- Crash.
- Freeze the menu.
- Leave the player stuck in a loading state.
- Dump raw Relay/network exceptions directly into the UI.

Log detailed technical errors to the Unity console for debugging.

---

# 18. Other Error States

Handle common failures such as:

### Lobby no longer exists

```text
This lobby is no longer available.
```

### Lobby full

```text
This lobby is full.
```

### Network failure

```text
Unable to connect.
Please check your connection and try again.
```

### Invalid/Incomplete Code

```text
Enter a valid join code.
```

Exact wording can differ, but the user should understand what happened.

---

# 19. Back/Cancel Behavior

At any point before joining, the player should be able to return to the previous menu.

Controller:

```text
B
```

Keyboard:

```text
Escape
```

Mouse:

```text
BACK button
```

Backing out should clean up any temporary connection/join attempt state.

---

# 20. Lobby Code Ownership

The host owns the session/join code.

Joining players may also display the code after entering the lobby if useful, especially if they need to invite additional players.

However, the host lobby must always display it.

---

# 21. Public vs Private Terminology

Inspect the current meaning of:

```text
Public Game
Private Game
Matchmaking
```

and ensure the join-code system is attached to the correct modes.

For the current prototype, a Relay join code is primarily needed for:

```text
Host creates game
        ↓
shares code
        ↓
friend joins directly
```

If "Public Lobby" currently means "anyone with the code can join," preserve that behavior.

Do not attempt to build full automated matchmaking as part of REQ-067 unless it already exists.

REQ-067 is specifically about making **code-based joining functional and usable**.

---

# 22. Do Not Break Local Testing

The existing ability to test multiplayer locally on one development machine must continue working.

REQ-067 should support both:

```text
Local multiplayer testing
```

and:

```text
Remote Relay multiplayer
```

Do not make Relay mandatory for every development/testing configuration unless the current architecture already requires it.

---

# 23. Lobby Lifecycle

Ensure the Relay/lobby lifecycle behaves correctly.

### Host creates lobby

```text
Create Relay allocation
Generate join code
Start host/network session
Show lobby
```

### Client joins

```text
Enter code
Join Relay allocation
Connect as client
Enter lobby
```

### Host starts match

```text
Host triggers match start
Connected clients transition together
Spawn players
Begin match
```

The network connection should already exist before the gameplay scene begins.

---

# 24. Match Start Synchronization

When the host presses:

```text
START MATCH
```

all currently connected lobby players should transition into gameplay correctly.

Joining players should not need to enter the join code again.

The Relay/network session should persist from:

```text
Lobby
→ Loading
→ Gameplay
```

rather than being recreated unnecessarily.

---

# 25. Prevent Joining After Match Start

For the initial implementation, preserve whichever behavior is safest with the current networking architecture.

If mid-match joining is not intentionally supported:

- The lobby should stop accepting new clients after the match begins.
- A player attempting to use the old code should receive an appropriate error.

Example:

```text
This match has already started.
```

Do not accidentally introduce unsupported late-join behavior.

Late joining can be addressed in a future requirement if desired.

---

# 26. Scene Architecture

Inspect whether the lobby currently exists:

- In the main menu scene
- In a dedicated lobby scene
- In the gameplay scene
- Through additive scene loading

The solution should work with the existing architecture rather than unnecessarily rebuilding the menu system.

The important requirement is:

```text
Network session established BEFORE gameplay starts.
```

---

# 27. Debug Information

During development, add useful debug logging.

Examples:

```text
[Relay] Creating allocation...
[Relay] Join code generated: AB7KQ9
[Relay] Host started successfully.
[Relay] Attempting join with code AB7KQ9.
[Relay] Client connected.
[Lobby] Player joined: PlayerTwo
```

Do not expose excessive technical logging in the production UI.

---

# 28. Connection Cleanup

Ensure failed or cancelled connection attempts are cleaned up correctly.

For example:

```text
Attempt join
→ failure
→ return to Join screen
→ enter new code
→ try again
```

must work without restarting the game.

Similarly:

```text
Join lobby
→ leave lobby
→ join another lobby
```

should not retain broken NetworkManager/Relay state.

---

# 29. Controller-First Testing

This feature must be tested without touching the keyboard.

A player using only an Xbox controller should be able to:

```text
Launch game
→ select Join Game
→ open join code screen
→ enter every character
→ correct a mistake
→ submit code
→ enter lobby
→ navigate lobby
```

If any part of that sequence requires a mouse or physical keyboard, the controller implementation is incomplete.

---

# 30. Keyboard-First Testing

A keyboard/mouse player should be able to:

```text
Select Join Game
→ click/select code input
→ type code normally
→ press Enter
→ join lobby
```

The physical keyboard should not force the user to navigate the virtual keyboard.

The virtual keyboard may remain visible when using keyboard input if that simplifies implementation, but direct typing must work.

---

# 31. Automatic Input Mode Handling

If practical, allow both input methods simultaneously.

Example:

The player can:

```text
select letters with controller
```

then immediately:

```text
type additional letters on keyboard
```

without changing settings.

Avoid requiring the player to choose:

```text
Controller Input
Keyboard Input
```

before entering the code.

---

# 32. UI Selection State

When using a gamepad, there must always be a valid selected UI element.

Opening the virtual keyboard should automatically highlight a sensible starting element.

For example:

```text
A
```

or:

```text
the first character button
```

Do not leave the EventSystem with:

```text
currentSelectedGameObject = null
```

which can make controller navigation appear broken.

---

# 33. UI Navigation Testing

Test:

- D-pad navigation.
- Left-stick navigation.
- Fast repeated movement.
- Holding a direction.
- Switching between controller and mouse.
- Backspace.
- Clear.
- Join.
- Back.
- Returning to the screen after a failed join.

Navigation should not become trapped between UI elements.

---

# 34. Preserve Lobby Player Identity

When a remote player connects, use the persistent player identity/profile foundation where available.

Display the best currently available identifier, such as:

```text
Player Name
Display Name
Player Tag
Persistent Player ID
```

Do not simply display:

```text
Client 1
Client 2
```

unless no player identity exists yet.

This should remain compatible with REQ-062 and REQ-065.

---

# 35. Security / Input Safety

Treat the join code as user input.

Sanitize input before attempting a join.

Reject unexpected characters rather than allowing arbitrary strings into networking calls.

Do not expose:

- Authentication tokens
- Relay credentials
- Allocation secrets
- Internal network information

in the visible lobby UI.

Only the intended join code should be displayed.

---

# Acceptance Criteria

REQ-067 is complete when:

- [ ] Existing multiplayer/lobby code has been inspected before implementing redundant systems.
- [ ] Hosting a Relay multiplayer game generates the join code before gameplay starts.
- [ ] The join code is visible in the host lobby.
- [ ] The host can remain in the lobby while another machine joins.
- [ ] Selecting Join Game opens a join-code entry interface.
- [ ] Physical keyboard input works.
- [ ] Enter/Return can submit a valid typed code.
- [ ] Backspace works with physical keyboard input.
- [ ] A complete virtual keyboard exists for controller users.
- [ ] The virtual keyboard can be navigated using D-pad and/or left stick.
- [ ] Controller users can enter letters/numbers.
- [ ] Controller users can delete incorrect characters.
- [ ] Controller users can submit a join code.
- [ ] Controller users can back out of the screen.
- [ ] A successful code connection places the joining player into the host's lobby.
- [ ] The host sees the joining player in the lobby player list.
- [ ] The joining player sees the host and other connected players.
- [ ] Host-selected map and game mode are visible to joining players.
- [ ] Only the host can start the match.
- [ ] Starting the match transitions all connected players into gameplay.
- [ ] Invalid codes produce a useful error message.
- [ ] Failed joins do not require restarting the game.
- [ ] Cancelling a join attempt cleans up network state.
- [ ] Leaving and joining another lobby works correctly.
- [ ] The entire joining process can be completed with an Xbox controller only.
- [ ] The entire joining process can be completed with keyboard/mouse only.
- [ ] Local multiplayer testing continues to function.
- [ ] Relay multiplayer continues to function across separate machines.
- [ ] Existing player identity/profile systems remain compatible.
- [ ] Existing lobby/map/game-mode behavior from REQ-065 remains intact.

---

# Required Test Scenario

The primary validation for REQ-067 should use **two separate machines**.

## Machine A — Host

```text
1. Launch Bullseye.
2. Select Host/Create Game.
3. Configure game mode/map.
4. Enter lobby.
5. Confirm join code is visible BEFORE starting match.
6. Do not start match.
```

Example:

```text
JOIN CODE: AB7KQ9
```

---

## Machine B — Client

Using controller only:

```text
1. Launch Bullseye.
2. Select Join Game.
3. Open join-code screen.
4. Use virtual keyboard to enter AB7KQ9.
5. Select JOIN.
6. Connect to Machine A's lobby.
```

Confirm that both machines now display both players.

Then:

```text
Machine A presses START MATCH.
```

Both machines should enter the same match.

Repeat the test using physical keyboard entry instead of the virtual keyboard.

---

# Desired Final Flow

```text
HOST PLAYER

Host Game
    ↓
Choose Mode
    ↓
Choose Map
    ↓
Create Lobby
    ↓
JOIN CODE: AB7KQ9
    ↓
Wait for Players


REMOTE PLAYER

Join Game
    ↓
Enter Code
    ↓
AB7KQ9
    ↓
Join
    ↓
Connected to Lobby


LOBBY

Cade
PlayerTwo

Mode: Bullseye FFA
Map: Prototype Arena

Host selects START MATCH
    ↓
Both players enter gameplay
```

The key principle for REQ-067 is:

**The lobby itself must be the place where multiplayer connections are established. Starting the match should begin gameplay—not create the multiplayer session.**