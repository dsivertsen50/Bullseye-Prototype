# REQ-030 — Main Menu and Multiplayer Session Flow

## Summary

Create the first complete startup/main-menu experience for Bullseye.

When the game launches, the player should arrive at a polished main menu rather than immediately entering gameplay or relying on development networking controls.

Because Bullseye is currently a multiplayer-only game, the main menu should focus primarily on:

* Joining multiplayer games
* Hosting multiplayer games
* Public/private hosting options
* Controller instructions
* Game settings
* Credits
* Quitting the game

This requirement should establish the menu architecture and multiplayer flow without overcommitting the project to a specific long-term matchmaking provider.

---

# Goals

1. Create a proper startup experience for Bullseye.
2. Allow players to host multiplayer sessions from the main menu.
3. Allow players to join multiplayer sessions.
4. Distinguish between public and private games.
5. Reuse the settings already available from the pause menu.
6. Provide controller and keyboard/mouse instructions.
7. Add a credits page.
8. Ensure the entire menu can be navigated using either mouse/keyboard or gamepad.
9. Build the menu so future matchmaking, lobby, map selection, and game-mode systems can be added cleanly.

---

# 1. Main Menu

When Bullseye launches, display a dedicated main-menu scene.

The initial menu should contain:

* **Play**
* **Controls**
* **Settings**
* **Credits**
* **Quit**

Do not immediately spawn the player into a multiplayer gameplay scene.

The menu should function without requiring a multiplayer session to already exist.

---

# 2. Play Menu

Selecting **Play** should open a multiplayer submenu.

Provide the following options:

* **Join Game**
* **Host Game**
* **Back**

The structure should be designed so additional options can eventually be added, such as:

* Quick Play
* Server Browser
* Friends
* Recent Servers
* Ranked
* Custom Games

These future systems are out of scope for REQ-030.

---

# 3. Host Game

Selecting **Host Game** should open a Host Game configuration screen.

At minimum, the player should be able to choose:

### Game Visibility

* **Public**
* **Private**

The player should clearly see which option is selected.

---

## Public Game

A public game represents a session that is intended to be discoverable by other players.

For the current prototype, implement as much of the public-host flow as the existing networking architecture reasonably supports.

If a full matchmaking/discovery service has not yet been implemented, the menu and code architecture should still distinguish a public game from a private game so that proper matchmaking can be connected later.

Do not create fake matchmaking behavior that implies players have joined an online matchmaking service when they have not.

---

## Private Game

A private game should be intended for invited players or players with a join code/session identifier.

The Host Game menu should be structured so a private-game join method can be supported.

Depending on the networking system currently available, this may initially use:

* Join code
* Lobby code
* IP/address
* Session identifier
* Existing Netcode connection method

Prefer a **join-code/session-code abstraction** if practical so the UI is not permanently tied to direct-IP networking.

---

# 4. Host Game Button

The Host Game screen should contain:

* Visibility selection
* **Start Game / Create Game**
* **Back**

When the host starts the session:

1. Create the multiplayer session.
2. Establish the local player as host.
3. Load the appropriate multiplayer gameplay scene.
4. Spawn the host correctly.
5. Allow other players to join according to the selected visibility/settings.

Existing multiplayer functionality must continue to work.

---

# 5. Join Game

Selecting **Join Game** should open a Join Game screen.

The design should anticipate two ways of joining:

### Public Games

A public-game area should eventually display discoverable games.

For REQ-030, if automatic public-game discovery is not yet available, it is acceptable for this portion to be a clearly labeled placeholder or basic implementation.

Do not fabricate server listings.

The architecture should make it possible to add a server/lobby browser later.

### Private Games

Provide an input field or equivalent mechanism for entering the information required to join a private session.

Prefer terminology such as:

**Enter Join Code**

rather than exposing technical networking terminology to players.

If the current multiplayer implementation requires another value during development, adapt the UI while keeping networking implementation details separated from the final user-facing design where practical.

---

# 6. Connection Feedback

Joining or hosting a game should provide visual feedback.

Examples:

* `Creating Game...`
* `Joining Game...`
* `Connecting...`

If the connection fails, display a useful message instead of silently returning to the menu.

Examples:

* `Unable to connect to game.`
* `Game could not be found.`
* `Connection failed.`
* `Session is full.`

Include a way to return to the Join Game menu.

Do not allow repeated button presses to accidentally attempt several simultaneous connections.

---

# 7. Future Lobby Compatibility

The architecture should anticipate a future pre-match lobby.

A future lobby may allow players to:

* See connected players
* Select maps
* Select game settings
* Ready up
* Invite friends
* Change teams in team modes
* Adjust kill/score limits

REQ-030 does **not** need to create this lobby.

However, avoid structuring the Host button so that hosting must always load directly into active gameplay.

The multiplayer flow should be capable of eventually becoming:

`Main Menu`
→ `Host/Join`
→ `Lobby`
→ `Match`

For now it may still function as:

`Main Menu`
→ `Host/Join`
→ `Match`

---

# 8. Controls Menu

Selecting **Controls** from the main menu should open a controls/instructions screen.

This should explain the current Bullseye controls for both:

## Keyboard and Mouse

Display the currently implemented controls, including relevant actions such as:

* Move
* Look
* Fire
* Aim/zoom
* Sprint
* Jump
* Grenade
* Weapon controls
* Pause

Use the actual input bindings currently configured in the project rather than duplicating potentially outdated bindings manually where practical.

At minimum, ensure the displayed instructions accurately reflect the game's current controls.

---

## Gamepad

Display the equivalent controller inputs.

The gamepad instructions should be easy to understand without requiring keyboard input.

Use terminology such as:

* Left Stick
* Right Stick
* Left Trigger
* Right Trigger
* Left Stick Click
* Right Stick Click
* Face buttons
* Shoulder buttons
* Menu button

The exact visual representation may be improved later.

A text-based controller guide is acceptable for REQ-030.

---

# 9. Settings Menu

Selecting **Settings** from the main menu should expose the same settings available from the existing pause menu.

Do **not** create an entirely separate settings implementation if the current pause menu already has functioning settings.

Create or reuse a shared settings system so changes made from either menu affect the same underlying configuration.

Include the settings currently supported by the pause menu, including items such as:

* Master volume
* Sound/music volume where applicable
* Screen brightness
* Other existing display/audio settings
* Controls submenu where applicable

The precise options should remain consistent with the pause-menu implementation.

---

# 10. Settings Persistence

Where practical, settings should persist when leaving the settings menu.

If the existing settings system already saves values between sessions, preserve that functionality.

If settings currently apply only during the current game session, REQ-030 does not require a completely new save system unless implementing persistence is straightforward within the existing architecture.

The important requirement is that the **main-menu Settings and pause-menu Settings operate on the same values**.

---

# 11. Credits Menu

Selecting **Credits** should open a dedicated credits screen.

At minimum, include:

## Bullseye

### Game Directors

Display the names of the game's directors.

The director names should preferably be stored in an easily editable configuration/UI field rather than buried in code.

This allows the actual names and future credits to be changed without rewriting the menu system.

The architecture should allow additional credit categories later, such as:

* Game Directors
* Programming
* Art
* Animation
* Sound
* Music
* Additional Design
* Special Thanks

Only the Game Directors section is required now.

Include a **Back** button.

---

# 12. Quit Game

Selecting **Quit** should exit the application in a built game.

When running inside the Unity Editor, the Quit button should either:

* Do nothing safely, or
* Stop Play Mode if an existing project utility already supports this behavior.

Do not generate errors when Quit is selected inside the Editor.

---

# 13. Controller Navigation

The **entire main menu must be usable with a controller**.

This is important because Bullseye is intended to support gamepad play.

Gamepad navigation must support:

* Moving between menu items
* Selecting buttons
* Going back
* Changing settings
* Selecting Public/Private
* Navigating text/input fields where practical
* Starting/hosting games

A player should not need a mouse to get from launching the game into a multiplayer match.

---

# 14. Keyboard and Mouse Navigation

The menu must also support:

* Mouse hovering
* Mouse clicking
* Keyboard navigation
* Enter/Submit
* Escape/Back

Ensure UI actions use the project's Input System rather than relying on obsolete input APIs unless required by an existing system.

---

# 15. Default Selection

When a menu screen opens while using a controller, one appropriate UI element should automatically become selected.

Example:

Opening the main menu:

`Play` is selected.

Opening Host Game:

`Public` or the first configurable option is selected.

Opening Settings:

The first available setting is selected.

This prevents the player from needing to move the mouse before controller navigation begins working.

---

# 16. Back Navigation

Every submenu must provide a consistent way to return to the previous menu.

Examples:

`Credits → Main Menu`

`Controls → Main Menu`

`Settings → Main Menu`

`Host Game → Play`

`Join Game → Play`

Support:

* UI Back button
* Escape
* Gamepad Cancel/Back button

Avoid leaving players trapped inside a submenu.

---

# 17. Menu Audio

Reuse the pause-menu navigation sounds where practical.

Menu interactions should support the same general audio feedback used by the pause menu.

Examples:

* Navigate/highlight
* Confirm/select
* Back/cancel

Do not create duplicate menu-audio systems if the existing pause menu already has one.

The sound assignments should remain editable in the Inspector.

---

# 18. Scene Structure

Create a clean scene/menu structure.

A likely flow is:

`Startup`
→ `MainMenu`
→ `Multiplayer Gameplay`

If the project does not need a separate startup/bootstrap scene, launching directly into `MainMenu` is acceptable.

Ensure all required scenes are included in Unity's Build Settings / Build Profiles as appropriate.

The multiplayer scene-loading system must continue to synchronize clients correctly.

---

# 19. UI Architecture

Avoid implementing every submenu as unrelated one-off scripts.

Prefer a reusable menu-navigation architecture.

Conceptually:

`MainMenuController`

with panels/screens such as:

* Main
* Play
* Host
* Join
* Controls
* Settings
* Credits

Only one appropriate menu screen should normally be active at a time.

---

# 20. Preserve Existing Pause Menu

REQ-030 must not break the existing in-game pause menu.

The pause menu should remain available during gameplay.

Shared components should be reused where practical, particularly:

* Settings
* Controls information
* UI navigation
* Menu sounds

The main menu and pause menu do not need identical visual layouts.

---

# Suggested Initial Menu Flow

## Main Menu

**BULLSEYE**

* Play
* Controls
* Settings
* Credits
* Quit

---

## Play

* Join Game
* Host Game
* Back

---

## Host Game

**Visibility**

* Public
* Private

**Create Game**

* Start Game
* Back

---

## Join Game

**Public Games**

Future server/game browser area

**Private Game**

* Join Code input

* Join Game

* Back

---

## Controls

Display:

* Keyboard & Mouse

* Gamepad

* Back

---

## Settings

Reuse existing settings system.

* Audio

* Display

* Controls/settings currently supported

* Back

---

## Credits

**BULLSEYE**

**Game Directors**

[Director Name]

[Director Name]

* Back

---

# Multiplayer Test Scenarios

## Test 1 — Main Menu Startup

1. Launch the project.
2. Confirm the main menu appears.
3. Confirm no gameplay character spawns automatically.
4. Confirm Play, Controls, Settings, Credits, and Quit are accessible.

---

## Test 2 — Controller Navigation

1. Launch Bullseye using only a controller.
2. Navigate through every menu.
3. Open Host Game.
4. Select Public/Private.
5. Return to the main menu.
6. Open Settings.
7. Return.
8. Open Controls.
9. Return.

Confirm no mouse is required.

---

## Test 3 — Host Multiplayer Game

1. Player 1 opens Play.
2. Select Host Game.
3. Select the desired visibility.
4. Start the session.
5. Confirm the gameplay scene loads correctly.
6. Confirm the host player spawns correctly.

---

## Test 4 — Join Multiplayer Game

1. Player 1 hosts a game.
2. Player 2 opens Join Game.
3. Player 2 enters/selects the appropriate session.
4. Confirm Player 2 connects.
5. Confirm both players spawn and can interact normally.

---

## Test 5 — Settings Sharing

1. Change a setting from the main menu.
2. Start a game.
3. Open the pause menu.
4. Confirm the setting shows the same value.
5. Change it from the pause menu.
6. Return to the relevant menu/session if supported.
7. Confirm both interfaces reference the same underlying setting.

---

## Test 6 — Controls Screen

1. Open Controls.
2. Confirm keyboard/mouse bindings are shown.
3. Confirm gamepad bindings are shown.
4. Confirm they reflect the current implemented controls.

---

## Test 7 — Credits

1. Open Credits.
2. Confirm the Bullseye title appears.
3. Confirm the Game Directors section appears.
4. Confirm both director names can be configured easily.
5. Return to the main menu.

---

# Acceptance Criteria

REQ-030 is complete when:

* [ ] Bullseye launches into a dedicated main menu.
* [ ] Main menu contains Play, Controls, Settings, Credits, and Quit.
* [ ] Play opens Join Game and Host Game options.
* [ ] Host Game distinguishes Public and Private sessions.
* [ ] Host can successfully start the existing multiplayer session.
* [ ] Join Game provides a functional path for joining another player.
* [ ] Private-game architecture supports a join/session code or equivalent.
* [ ] Public-game UI is prepared for future matchmaking/discovery.
* [ ] Main-menu Settings reuse the pause-menu settings system.
* [ ] Main-menu settings and pause-menu settings remain synchronized.
* [ ] Controls display keyboard/mouse instructions.
* [ ] Controls display gamepad instructions.
* [ ] Credits contains an editable Game Directors section.
* [ ] All menu screens support controller navigation.
* [ ] All menu screens support keyboard/mouse navigation.
* [ ] Appropriate menu items are automatically selected for controller navigation.
* [ ] Back/cancel navigation works consistently.
* [ ] Existing pause-menu sounds are reused where practical.
* [ ] Host/client multiplayer gameplay continues to function.
* [ ] Existing player spawning, weapons, health, grenades, statistics, and respawn systems are not broken.
* [ ] No development-only NetworkManager buttons are required for normal gameplay startup.

---

# Out of Scope

The following are intentionally deferred:

* Full online matchmaking service
* Account creation
* Player profiles
* Steam integration
* Friends list
* Invitations
* Ranked matchmaking
* Skill-based matchmaking
* Persistent server browser
* Matchmaking regions
* Ping display
* Dedicated servers
* Lobby ready system
* Map voting
* Game-mode voting
* Character customization
* Persistent progression
* Career statistics
* Leaderboards
* Final visual polish
* Animated menu backgrounds
* Cinematic startup sequence

---

# Future Direction

REQ-030 should establish the following long-term flow:

**Launch Bullseye**

→ **Main Menu**

→ **Host / Join**

→ **Pre-Match Lobby**

→ **Match**

→ **Post-Match Results**

→ **Lobby / Main Menu**

Future requirements can then build on this foundation with:

* Player lobbies
* Player names
* Ready status
* Match settings
* Map selection
* Bullseye scoring rules
* Kill limits
* Score limits
* End-of-match results
* Public matchmaking
* Private invite codes
* Persistent player statistics

The goal of REQ-030 is not to solve the entire online infrastructure yet. It is to make Bullseye feel like a multiplayer game that a player can actually launch and navigate from beginning to end, while keeping the networking layer modular enough to evolve later.
