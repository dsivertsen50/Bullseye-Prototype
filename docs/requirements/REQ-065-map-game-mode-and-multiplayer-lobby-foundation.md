# REQ-065 — Map, Game Mode & Multiplayer Lobby Foundation

## Summary

Create the foundational **match configuration and multiplayer lobby system** for Bullseye.

REQ-064 establishes the ability for remote players to connect through Unity Relay.

REQ-065 should build on that foundation by introducing:

* four configurable game-mode slots,
* six configurable map slots,
* map preview images,
* one-sentence map descriptions,
* map/game-mode selection for custom matches,
* a reusable multiplayer lobby,
* a visible list of connected players,
* synchronized selected map/game mode,
* and a foundation that can later support private sessions, public sessions, and matchmaking.

The primary initial flow should be:

```text
HOST CUSTOM MATCH
        ↓
Choose Game Mode
        ↓
Choose Map
        ↓
Create / Enter Lobby
        ↓
Other Players Join
        ↓
Lobby Shows All Players
        ↓
Host Starts Match
        ↓
Selected Map + Mode Launch
```

The lobby architecture should also be reusable later for:

```text
PUBLIC SESSION
        ↓
Player joins available game
        ↓
Same Lobby Screen

MATCHMAKING
        ↓
Player matched into session
        ↓
Same Lobby Screen
```

Do not build Bullseye's complete matchmaking system in this ticket.

---

# 1. Primary Goal

Bullseye needs to begin treating a multiplayer match as a configurable entity rather than simply:

```text
Start Network
     ↓
Load Current Gameplay Scene
```

The new conceptual model should be:

```text
Multiplayer Session
        |
        +--> Game Mode
        |
        +--> Map
        |
        +--> Players
        |
        +--> Visibility / Join Method
        |
        +--> Match State
```

---

# 2. Custom Match Flow

Create a Custom Match flow similar to:

```text
CUSTOM MATCH

GAME MODE
[ Free For All                  ▼ ]

MAP
[ Prototype Arena               ▼ ]

[ CREATE LOBBY ]
```

Once the lobby exists:

```text
CUSTOM MATCH LOBBY

Mode: Free For All
Map: Prototype Arena

Players
------------------------
HostPlayer           HOST
BrotherPlayer
------------------------

Join Code: X7K92Q

[ START MATCH ]
```

The exact presentation may differ according to the existing menu style.

---

# 3. Four Game Mode Placeholders

Create support for **four game-mode definition slots**.

For now:

```text
Game Mode 01
Game Mode 02
Game Mode 03
Game Mode 04
```

At least one should represent the currently playable mode.

Recommended initial setup:

```text
01 — Free For All
02 — Placeholder Mode
03 — Placeholder Mode
04 — Placeholder Mode
```

Do not invent full gameplay logic for the three additional modes.

They exist to establish the menu/data architecture.

---

# 4. GameModeDefinition

Create a reusable configurable definition for game modes.

Prefer a ScriptableObject or equivalent data-driven architecture.

Suggested concept:

```csharp
GameModeDefinition
{
    string GameModeId;

    string DisplayName;

    string ShortDescription;

    bool IsAvailable;

    // future settings
}
```

Possible stable IDs:

```text
mode_ffa
mode_02
mode_03
mode_04
```

Do not make the UI depend directly on display strings.

---

# 5. Game Mode Availability

Because only the existing gameplay mode may actually function today, definitions should support:

```text
IsAvailable
```

If a mode is not implemented yet, the UI may show:

```text
COMING SOON
```

or disable selection/start appropriately.

Do not create fake gameplay behavior for placeholder modes.

---

# 6. Game Mode Selection UI

The host should be able to browse the four game-mode entries.

Example:

```text
GAME MODE

[ Free For All       ]
[ Mode 02            ]
[ Mode 03            ]
[ Mode 04            ]
```

or:

```text
GAME MODE
< Free For All >
```

Use whichever layout fits the existing Bullseye menu best.

---

# 7. Optional Game Mode Description

If straightforward, display a brief description for each mode.

Example:

```text
FREE FOR ALL

Every player for themselves.
Most eliminations wins.
```

Descriptions should come from `GameModeDefinition`.

Do not hard-code description text into UI scripts.

---

# 8. Six Map Placeholders

Create support for **six map definitions**.

Suggested initial structure:

```text
Map 01 — Current Prototype Map
Map 02 — Placeholder
Map 03 — Placeholder
Map 04 — Placeholder
Map 05 — Placeholder
Map 06 — Placeholder
```

All six should appear through the same data-driven system.

---

# 9. MapDefinition

Create a reusable configurable map definition.

Suggested concept:

```csharp
MapDefinition
{
    string MapId;

    string DisplayName;

    Sprite PreviewImage;

    string Description;

    string SceneName;

    bool IsAvailable;
}
```

Exact implementation may differ.

The important requirements are:

```text
Stable ID
Display Name
Preview Image
Description
Gameplay Scene Reference
Availability
```

---

# 10. Stable Map IDs

Use stable internal IDs.

Example:

```text
map_01
map_02
map_03
map_04
map_05
map_06
```

Do not use only:

```text
"Prototype Arena"
```

as the technical key.

Display names should be freely changeable later.

---

# 11. Map Selection UI

Provide six map entries.

Example:

```text
MAPS

[ MAP 01 ] [ MAP 02 ] [ MAP 03 ]

[ MAP 04 ] [ MAP 05 ] [ MAP 06 ]
```

A tiled/card layout is preferred because map imagery will eventually become important.

---

# 12. Map Preview Image

When the mouse hovers over a map, display a larger preview image of that map.

Example:

```text
-----------------------------------------
|                                       |
|          MAP PREVIEW IMAGE            |
|                                       |
-----------------------------------------

Prototype Arena

A compact combat arena built around close-
to-mid-range firefights and vertical movement.
```

The preview image should come from:

```text
MapDefinition.PreviewImage
```

rather than being hard-coded into the UI.

---

# 13. Controller Equivalent to Hover

Controllers do not have hover behavior.

Therefore:

```text
Mouse Hover
```

and:

```text
Controller Focus
```

should produce the same preview behavior.

Example:

```text
D-Pad / Left Stick
        ↓
Focus Map 03
        ↓
Map 03 Preview Image Appears
        ↓
Map 03 Description Appears
```

This is essential.

Do not design the map-selection system around mouse hover alone.

---

# 14. Map Description

Every map definition must support a **single-sentence descriptor**.

Example:

```text
A compact arena focused on close-range fights and rapid vertical movement.
```

For placeholder maps, temporary text is acceptable:

```text
A future Bullseye battleground currently under development.
```

Descriptions should be easy to edit through the Inspector/asset.

---

# 15. Placeholder Preview Images

Create a reasonable fallback image for maps that do not yet have final screenshots.

Examples:

```text
MAP 02

IMAGE
COMING SOON
```

Do not require final map artwork to complete REQ-065.

The architecture should accept final screenshots later simply by changing the map definition asset.

---

# 16. Map Availability

Map definitions should contain:

```text
IsAvailable
```

The existing playable map should be available.

Unbuilt placeholder maps may remain visible but unavailable.

Example:

```text
MAP 04
COMING SOON
```

The UI foundation should still show all six slots.

---

# 17. Do Not Load Missing Scenes

If:

```text
MapDefinition.SceneName
```

does not point to an implemented scene, do not attempt to load it.

Instead:

```text
Start Match disabled
```

or:

```text
Map unavailable
```

should be shown.

Avoid missing-scene exceptions.

---

# 18. Selected Map State

The host should have one authoritative:

```text
SelectedMapId
```

Example:

```text
map_01
```

This selection should eventually be shared with everyone in the multiplayer session.

Clients should not maintain competing independent map selections.

---

# 19. Selected Game Mode State

Likewise maintain:

```text
SelectedGameModeId
```

Example:

```text
mode_ffa
```

This value should belong to the match/session configuration.

---

# 20. MatchConfiguration

Create a centralized match configuration concept.

Suggested model:

```csharp
MatchConfiguration
{
    string MapId;
    string GameModeId;

    MatchVisibility Visibility;

    int MaxPlayers;
}
```

This can expand later.

Potential future fields:

```text
Score Limit
Time Limit
Teams
Respawn Rules
Weapon Rules
Grenade Rules
Custom Modifiers
```

Do not implement those yet.

---

# 21. Session Properties

For online sessions, synchronize the selected configuration through session-level data where appropriate.

Conceptually:

```text
Session

mapId = map_01
gameModeId = mode_ffa
```

The host owns these selections.

Connected lobby members should receive and display them.

---

# 22. Host Authority Over Match Settings

For a private/custom game:

```text
HOST
```

controls:

```text
Game Mode
Map
Start Match
```

Clients should be able to see the selections but should not change them.

Example client UI:

```text
Mode
Free For All

Map
Prototype Arena
```

without editable controls.

---

# 23. Lobby Screen

Create a reusable:

```text
MATCH LOBBY
```

screen.

This should not be specific only to Relay join-code sessions.

The same lobby component should eventually support:

```text
Private Custom Match

Public Joinable Match

Matchmade Match
```

---

# 24. Lobby Data Model

Conceptually:

```text
MatchLobby

Session
    |
    +--> MatchConfiguration
    |
    +--> Player List
    |
    +--> Host
    |
    +--> Join Code
    |
    +--> Session Visibility
    |
    +--> Match State
```

---

# 25. Player Roster

The lobby must display all players currently connected to the session.

Example:

```text
PLAYERS

Cade                     HOST
Brother
PlayerThree
PlayerFour
```

The list must update as players join or leave.

---

# 26. Player Display Name

Use the persistent player-facing display name established by REQ-062 / REQ-063 where practical.

Example:

```text
BullseyePlayer
```

The lobby should not simply display:

```text
Client 0
Client 1
```

when a valid player display name is available.

---

# 27. Player Tag / ID

Each lobby row should support an optional player-facing identifier.

Example:

```text
Cade              #7F42
Brother           #A912
```

This should be a **safe short identifier**, not necessarily the player's full internal `PlayerProfileId`.

Do not expose a long GUID such as:

```text
4fa7d893-13f6-451f-82c5-2b556a7d1ef8
```

as normal lobby UI.

---

# 28. Internal ID Privacy

Keep these separate:

```text
DisplayName
PublicPlayerTag
PlayerProfileId
UnityPlayerId
NetworkClientId
```

The lobby does not need to reveal internal technical identifiers.

A short public/session tag may be derived or assigned for player differentiation.

---

# 29. Host Indicator

Clearly indicate the current host.

Example:

```text
Cade                  [HOST]
```

or an icon.

Clients should understand which player owns the current session.

---

# 30. Local Player Indicator

Where useful, indicate the player's own roster entry.

Example:

```text
Cade                   YOU
Brother                HOST
```

or equivalent visual treatment.

---

# 31. Dynamic Lobby Updates

When a player joins:

```text
Session Player Added
        ↓
Lobby Refresh
        ↓
Player Appears
```

When a player leaves:

```text
Session Player Removed
        ↓
Lobby Refresh
        ↓
Player Disappears
```

Do not require reopening the lobby to refresh player names.

---

# 32. Player Name Synchronization

Ensure players entering the session publish an appropriate player-facing display name so other session members can see it.

Conceptually:

```text
REQ-063 DisplayName
        ↓
Online Session Player Data
        ↓
Lobby Roster
```

If the Bullseye profile display name is unavailable, provide a reasonable fallback.

Example:

```text
Player 7F42
```

---

# 33. Private Match Lobby

A Relay join-code game should support:

```text
PRIVATE MATCH

Mode: Free For All
Map: Prototype Arena

Players
-----------------
Cade        HOST
Brother
-----------------

Join Code
X7K92Q

[ START MATCH ]
```

This is the first fully functional lobby scenario.

---

# 34. Private Join Code

Continue using REQ-064's join-code functionality.

The private lobby should display the join code prominently to the host.

Example:

```text
JOIN CODE

X7K92Q
```

Clients who have already joined do not necessarily need the join code prominently displayed.

---

# 35. Public Session Compatibility

The same Lobby UI must be reusable if Bullseye later supports:

```text
PUBLICLY JOINABLE SESSIONS
```

A player joining through a public session browser should still see:

```text
Map
Game Mode
Players
Host
Lobby State
```

Do not create a completely separate "public lobby" UI.

---

# 36. Matchmaking Compatibility

Likewise, players who enter through future matchmaking should arrive at the same lobby architecture.

Conceptually:

```text
PRIVATE JOIN CODE ───────────┐
                             |
PUBLIC SESSION BROWSER ──────+--> MatchLobby
                             |
MATCHMAKING ─────────────────┘
```

This avoids three parallel lobby systems.

---

# 37. Matchmaking Behavior — Future

The eventual matchmaking design is expected to differ from custom games.

Conceptually:

```text
MATCHMAKING

Player chooses:
    Game Mode

System chooses:
    Compatible players
    Map
```

The map can later be randomized from an eligible map pool.

REQ-065 should prepare for this architecture.

Do not build the matchmaking queue itself yet.

---

# 38. Public Joinable Game Behavior — Future

A future public host may create something like:

```text
PUBLIC MATCH

Mode: Free For All
Map: Prototype Arena
Slots: 4 / 8
```

Other players may find and join it.

REQ-065 should ensure:

```text
MapId
GameModeId
PlayerCount
MaxPlayers
```

can be represented through the session architecture.

Do not build the full server/session browser unless necessary for existing testing.

---

# 39. Session Visibility

Create a conceptual visibility setting such as:

```csharp
public enum MatchVisibility
{
    Private,
    Public
}
```

The initial working flow may remain:

```text
Private
```

through REQ-064 join codes.

However, the match configuration should be ready for:

```text
Public
```

later.

---

# 40. Ready State Foundation

Prepare each lobby-player row to eventually support:

```text
READY
NOT READY
```

Do not require a full Ready system yet unless it is easy to implement cleanly.

The player row should be extensible enough that later it could display:

```text
Cade         HOST     READY
Brother               READY
Player3                NOT READY
```

---

# 41. Starting a Private Match

Only the host should see or control:

```text
START MATCH
```

The button should only become available if:

* a valid map is selected,
* the map is available,
* a valid game mode is selected,
* the mode is available,
* the multiplayer session is in a usable state.

A single-player host-only lobby may remain startable for development testing if appropriate.

---

# 42. Match Start Synchronization

When the host presses:

```text
START MATCH
```

all connected players should transition into the same selected match.

Conceptually:

```text
Host presses Start
        ↓
Validate MatchConfiguration
        ↓
Lock selected Map + Mode
        ↓
Network Scene Load
        ↓
All clients load Map scene
        ↓
Game mode initialized
        ↓
Players spawn
```

Do not allow every client to independently load whatever scene they currently have selected locally.

---

# 43. Network Scene Loading

Use the project's NGO-compatible scene loading architecture.

The host should drive the match scene transition.

Connected clients should follow the host into the selected map.

Avoid using:

```text
SceneManager.LoadScene()
```

independently on each networked client if that bypasses NGO synchronization.

---

# 44. Map Definition → Scene

Map loading should be driven by:

```text
SelectedMapId
        ↓
MapDefinition
        ↓
Scene Reference
```

Do not build giant switch statements such as:

```csharp
if (map == "Map1")
...
else if (map == "Map2")
...
```

Use the definition architecture.

---

# 45. Game Mode Initialization

Game mode initialization should similarly use:

```text
SelectedGameModeId
        ↓
GameModeDefinition
        ↓
Gameplay mode setup
```

For REQ-065, only the existing working game mode needs to actually initialize.

The other modes can remain unavailable placeholders.

---

# 46. Map Preview Persistence in Lobby

Once a map is selected, the Lobby should display it.

Example:

```text
PROTOTYPE ARENA

[ Map Preview ]

A compact arena built around rapid close-to-mid-range firefights.
```

This gives joining players immediate context for the upcoming game.

---

# 47. Game Mode Display in Lobby

Likewise show:

```text
FREE FOR ALL

Every player fights independently.
```

or the current short description.

Clients should know what match they are waiting to play.

---

# 48. Host Changing Selection While in Lobby

For a custom/private match, the host may change:

```text
Map
Game Mode
```

while waiting in the lobby.

When the host changes a value:

```text
Host changes map
        ↓
Session property updates
        ↓
All lobby clients update
```

Clients should see the change without reconnecting.

---

# 49. Lobby Layout Concept

A reasonable first layout:

```text
------------------------------------------------------
CUSTOM MATCH
------------------------------------------------------

GAME MODE                     MAP

Free For All                  Prototype Arena

                               [ PREVIEW IMAGE ]

                              A compact arena built
                              around rapid firefights.

------------------------------------------------------

PLAYERS

Cade                       HOST
Brother
Player3

------------------------------------------------------

JOIN CODE: X7K92Q

[ CHANGE MODE ] [ CHANGE MAP ]        [ START MATCH ]
------------------------------------------------------
```

Clients could instead see:

```text
[ LEAVE LOBBY ]
```

rather than host configuration buttons.

---

# 50. Pre-Lobby Match Configuration

An alternative valid flow is:

```text
Custom Match
      ↓
Choose Mode
      ↓
Choose Map
      ↓
Create Lobby
```

Once created, the host may still be allowed to modify those choices.

Cursor should select whichever approach best fits the existing menu architecture.

---

# 51. Controller Navigation

All match setup and lobby interfaces must support controller input.

At minimum:

```text
D-Pad / Left Stick
    Navigate

A
    Select

B
    Back

LB / RB
    Change tabs/categories if used
```

Focused map cards must trigger preview behavior.

---

# 52. Mouse and Keyboard

Support:

```text
Mouse hover
Mouse click
Keyboard navigation
Escape / Back
```

Mouse hover should update map preview immediately.

---

# 53. Selection Highlighting

The currently selected map should be obvious.

Example:

```text
[ MAP 01 ✓ ]
```

Likewise the selected game mode should remain visually distinct.

Focus and selection are different concepts:

```text
FOCUSED MAP
    determines preview

SELECTED MAP
    determines actual match map
```

Do not confuse them.

---

# 54. Map Hover vs Selection

Example:

```text
Selected:
Map 01

Mouse hovers:
Map 03

Preview:
Map 03

Actual Match Selection:
Still Map 01
```

Only clicking/selecting Map 03 should replace the actual match selection.

This distinction is important.

---

# 55. Placeholder Map Cards

All six map cards should exist from the start.

Example:

```text
[ Prototype Arena ]

[ Map 02 — Coming Soon ]

[ Map 03 — Coming Soon ]

[ Map 04 — Coming Soon ]

[ Map 05 — Coming Soon ]

[ Map 06 — Coming Soon ]
```

The names can later be changed entirely through their definitions.

---

# 56. Placeholder Mode Entries

Likewise show four mode slots.

Example:

```text
[ Free For All ]

[ Mode 02 — Coming Soon ]

[ Mode 03 — Coming Soon ]

[ Mode 04 — Coming Soon ]
```

Do not make Cursor implement three arbitrary modes just to populate the menu.

---

# 57. Data-Driven Expansion

Adding Map 07 later should ideally involve:

```text
Create MapDefinition
        ↓
Assign Image
        ↓
Assign Description
        ↓
Assign Scene
        ↓
Add to Map Catalog
```

rather than modifying multiple UI scripts.

Likewise adding a fifth game mode should primarily be data-driven.

---

# 58. Map Catalog

Create a centralized collection of maps.

Suggested concept:

```text
MapCatalog
```

containing:

```text
MapDefinition[]
```

or equivalent.

The UI should populate from this catalog.

---

# 59. Game Mode Catalog

Likewise create:

```text
GameModeCatalog
```

containing:

```text
GameModeDefinition[]
```

The menu should populate from these definitions.

---

# 60. Lobby Player Component

Create a reusable roster-row component.

Suggested:

```text
LobbyPlayerRow
```

Fields might include:

```text
DisplayName
PublicTag
HostIndicator
LocalPlayerIndicator
ReadyIndicator // future
```

Do not hard-code eight player-name Text objects into the screen.

---

# 61. Scalable Player List

The roster should handle changing player counts.

Use:

```text
Vertical layout
Scroll view
Dynamic instantiated player rows
```

or equivalent.

Do not assume Bullseye will permanently be limited to two players.

---

# 62. Maximum Player Count

Continue using a configurable:

```text
MaxPlayers
```

value.

Lobby UI may display:

```text
Players: 2 / 8
```

if the current session exposes both values.

Do not permanently hard-code:

```text
2 / 2
```

because the first remote test happens to involve two developers.

---

# 63. Joining an Existing Lobby

When a client joins a private session using the REQ-064 join code:

```text
Join Session
      ↓
Receive Session Configuration
      ↓
Open Lobby
      ↓
Display Current Mode
      ↓
Display Current Map
      ↓
Display Existing Players
```

The client should not have to choose the map or mode again.

---

# 64. Late Lobby Join

If another player joins after the host has already selected:

```text
mode_ffa
map_01
```

the new player should immediately see:

```text
Free For All
Prototype Arena
```

Do not rely only on transient UI events that occurred before the player joined.

Session state must remain queryable.

---

# 65. Public Lobby Roster

A future player who enters through:

```text
Find Public Match
```

should see exactly the same roster component.

Example:

```text
PUBLIC MATCH

Free For All
Prototype Arena

PLAYERS

Cade                   HOST
Brother
Player3
Player4
```

No duplicate public-specific player list is necessary.

---

# 66. Matchmade Lobby Roster

Likewise future matchmaking should display:

```text
MATCH FOUND

Free For All
Randomly Selected Map

PLAYERS

PlayerA
PlayerB
PlayerC
PlayerD
```

through the same core component.

---

# 67. Do Not Build Matchmaking Yet

REQ-065 should **not** implement:

```text
Matchmaking queue
Skill-based matching
MMR
Region selection
Queue estimates
Backfill
Ranked search
```

Only prepare the configuration/lobby architecture so matchmaking can feed into it later.

---

# 68. Do Not Build Full Public Browser Yet

REQ-065 does not need to create a production-quality:

```text
SERVER BROWSER
```

or public-match discovery interface.

The important requirement is that the lobby and session data models can support publicly joinable sessions later.

---

# 69. Private/Public Terminology

Preferred concepts:

```text
Custom Match
    Private
    Public // future

Matchmaking // future
```

Avoid calling all host-created games:

```text
Local Match
```

because custom matches may be played remotely through Relay.

---

# 70. Leave Lobby

Clients should have:

```text
LEAVE LOBBY
```

This should return them safely to the multiplayer menu and perform REQ-064 session cleanup.

---

# 71. Host Cancelling Lobby

Host should be able to:

```text
CANCEL LOBBY
```

Expected:

```text
End Session
Disconnect Members
Clear MatchConfiguration
Return to Menu
```

Clients should receive an understandable result rather than hanging indefinitely.

---

# 72. Starting State

When REQ-065 is first implemented, a typical usable test may look like:

```text
GAME MODES

✓ Free For All
  Mode 02 — Coming Soon
  Mode 03 — Coming Soon
  Mode 04 — Coming Soon
```

and:

```text
MAPS

✓ Prototype Arena
  Map 02 — Coming Soon
  Map 03 — Coming Soon
  Map 04 — Coming Soon
  Map 05 — Coming Soon
  Map 06 — Coming Soon
```

This is acceptable.

The ticket is establishing architecture, not producing six complete maps.

---

# 73. Testing — Map UI

### Test A — Six Maps

Open Custom Match.

Expected:

```text
Six map slots appear.
```

---

### Test B — Mouse Hover

Hover over Map 03.

Expected:

```text
Map 03 preview image appears.
Map 03 description appears.
```

---

### Test C — Controller Focus

Move controller focus to Map 04.

Expected:

```text
Map 04 preview image appears.
Map 04 description appears.
```

---

### Test D — Selection

Hover/focus Map 03 without selecting it.

Expected:

```text
Preview changes.
Selected map does not change.
```

Select it.

Expected:

```text
SelectedMapId updates.
```

---

# 74. Testing — Game Modes

### Test E — Four Modes

Expected:

```text
Four game-mode slots appear.
```

---

### Test F — Existing Mode

Select Free For All.

Expected:

```text
SelectedGameModeId = mode_ffa
```

or the equivalent stable ID.

---

### Test G — Unavailable Mode

Attempt to start using an unavailable placeholder mode.

Expected:

```text
Start prevented cleanly.
No exception.
```

---

# 75. Testing — Private Lobby

### Test H — Host Creates Lobby

Host selects:

```text
Free For All
Prototype Arena
```

and creates the lobby.

Expected:

```text
Lobby opens.
Host appears in player list.
Map and mode are shown.
Join code is shown.
```

---

### Test I — Remote Join

Second player joins by Relay code.

Expected:

```text
Second player's name appears for both players.
```

---

### Test J — Player Leaves

Remote player leaves.

Expected:

```text
Roster updates automatically.
```

---

### Test K — Third Player Architecture

Where development testing allows, add another session member.

Expected:

```text
Another dynamic roster row appears.
No hard-coded two-player UI assumption fails.
```

---

# 76. Testing — Session Configuration

### Test L — Host Changes Map

Host changes map while client is in lobby.

Expected:

```text
Client's displayed map updates.
```

---

### Test M — Host Changes Mode

Host changes game mode.

Expected:

```text
Client's displayed mode updates.
```

---

### Test N — Client Cannot Change Settings

Client attempts to interact with host configuration.

Expected:

```text
Client cannot overwrite authoritative map/mode values.
```

---

# 77. Testing — Match Start

### Test O — Start Valid Match

Host chooses an available map and mode.

Press:

```text
START MATCH
```

Expected:

```text
All players load the same scene.
All players retain network connection.
Players spawn normally.
```

---

### Test P — Invalid Map

Select/force an unavailable placeholder map.

Expected:

```text
Match does not start.
Useful message appears.
```

---

### Test Q — Invalid Mode

Select/force unavailable game mode.

Expected:

```text
Match does not start.
Useful message appears.
```

---

# 78. Acceptance Criteria

REQ-065 is complete when:

* [ ] A Custom Match configuration screen exists.
* [ ] Four game-mode definition slots exist.
* [ ] Game modes are data-driven rather than individually hard-coded into the menu.
* [ ] The current playable mode is represented by a valid GameModeDefinition.
* [ ] Placeholder game modes can be marked unavailable.
* [ ] Six map definition slots exist.
* [ ] Maps are data-driven rather than individually hard-coded into the menu.
* [ ] Each map supports a display name.
* [ ] Each map supports a preview image.
* [ ] Each map supports a sentence-long description.
* [ ] Each map supports a gameplay scene reference.
* [ ] Each map supports an available/unavailable state.
* [ ] Mouse hover updates the map preview.
* [ ] Controller focus updates the map preview.
* [ ] Hover/focus is distinct from actual map selection.
* [ ] The selected map remains visibly highlighted.
* [ ] The selected game mode remains visibly highlighted.
* [ ] A centralized MatchConfiguration stores the selected map and mode.
* [ ] Host owns custom-match map/game-mode selection.
* [ ] Clients can see the selected map and mode.
* [ ] Session data synchronizes the selected configuration where applicable.
* [ ] A reusable multiplayer Match Lobby screen exists.
* [ ] Private Relay sessions use the Match Lobby.
* [ ] Lobby displays all currently connected players.
* [ ] Lobby roster updates when players join.
* [ ] Lobby roster updates when players leave.
* [ ] Player-facing display names are used where available.
* [ ] Host is clearly identified.
* [ ] Local player can be identified where appropriate.
* [ ] Full internal profile GUIDs are not exposed as normal lobby UI.
* [ ] Player list supports more than two players.
* [ ] Host can start a valid custom match.
* [ ] Clients cannot independently override the host's map/mode.
* [ ] Starting the match sends all connected players into the same map.
* [ ] NGO-compatible synchronized scene loading is used.
* [ ] Unavailable placeholder maps do not cause missing-scene errors.
* [ ] Unavailable game modes cannot accidentally start.
* [ ] Lobby architecture can later be reused for public sessions.
* [ ] Lobby architecture can later be reused for matchmaking.
* [ ] No complete matchmaking system has been added.
* [ ] No complete public server browser has been added.
* [ ] Existing REQ-064 Relay connectivity remains functional.
* [ ] Existing local multiplayer testing remains functional.

---

# 79. Explicit Non-Goals

Do NOT implement the following as part of REQ-065:

```text
Three new complete gameplay modes

Five new complete gameplay maps

Full matchmaking

Skill-based matchmaking

Ranked matchmaking

MMR

Public server browser

Map voting

Map bans

Playlist rotation

Party system

Friends system

Steam matchmaking

Steam invites

Dedicated servers

Host migration

Ready-up enforcement

Team balancing

Custom rule editor

Score-limit configuration

Weapon restriction menus

Bots

Map downloading

Workshop maps
```

These can be separate future requirements.

---

# 80. Future Matchmaking Direction

The architecture should make this eventual flow straightforward:

```text
MATCHMAKING

Choose Game Mode

[ Free For All ]

[ FIND MATCH ]
       ↓
Matchmaker finds players
       ↓
Eligible Map randomly selected
       ↓
Match Lobby
       ↓
Player roster visible
       ↓
Match begins
```

The player does **not necessarily choose a map during matchmaking**.

Instead:

```text
Game Mode
    = Player preference

Map
    = Match/session selection
```

REQ-065 should preserve that distinction.

---

# 81. Future Public Match Direction

Likewise:

```text
PUBLIC MATCH

Host selects:
    Game Mode
    Map

Host creates:
    Public Session

Other players find:
    Free For All
    Prototype Arena
    4 / 8 Players

Player joins
        ↓
Same Match Lobby
```

The actual public-discovery UI can come later.

---

# 82. Design Philosophy

REQ-064 answers:

> "Can these players connect to each other over the Internet?"

REQ-065 should answer:

> "What game are these connected players preparing to play, and who is currently in the lobby?"

The system should establish three reusable concepts:

```text
GameModeDefinition

MapDefinition

MatchLobby
```

Those concepts should remain useful whether the player reaches the lobby through:

```text
Private Join Code

Public Session

Matchmaking
```

The initial implementation can remain simple:

```text
1 playable game mode
+ 3 placeholders

1 playable map
+ 5 placeholders

Private Relay lobby
+ architecture ready for public/matchmade sessions
```

That gives Bullseye a clean multiplayer structure without prematurely building six maps, four full modes, or a production matchmaking service.
