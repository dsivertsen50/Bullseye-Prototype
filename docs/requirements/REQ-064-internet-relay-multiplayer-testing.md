# REQ-064 — Internet Multiplayer Relay Testing

## Summary

Add **Internet multiplayer testing through Unity Relay** so two players on separate networks can host and join the same Bullseye match using a short join code.

The immediate target use case is:

```text
Player A — Utah
    ↓
Host Online Match
    ↓
Relay-backed session created
    ↓
Join Code generated
    ↓
Player B — Nevada
    ↓
Enter Join Code
    ↓
Join same Bullseye match
```

This represents an important milestone for Bullseye:

> Two separate computers on two separate Internet connections should be able to play the existing multiplayer game together.

REQ-064 should **not replace the existing local multiplayer development workflow**.

Instead, Bullseye should support two distinct connection paths:

```text
LOCAL DEVELOPMENT
    Direct/local NGO multiplayer
    Same-machine testing
    Multiplayer Play Mode / local clients

ONLINE RELAY
    Unity Multiplayer Services
    Relay-backed session
    Join code
    Remote Internet clients
```

The underlying gameplay systems should remain shared.

---

# 1. Primary Goal

Allow a player to:

```text
HOST ONLINE
```

and receive a join code.

Another player should then be able to:

```text
JOIN ONLINE
```

enter that join code, and connect to the same game over the Internet.

Once connected, the existing multiplayer functionality should behave as normally as possible.

At minimum, remote players should be able to:

```text
Spawn
Move
Look
Aim
Shoot
Take damage
Die
Respawn
Use weapons
See one another
Interact with existing networked gameplay
```

This ticket is primarily about **establishing the connection**.

Do not attempt to redesign every gameplay networking system at the same time.

---

# 2. Critical Requirement — Preserve Local Multiplayer Testing

REQ-064 must **not break or remove the existing local multiplayer development workflow**.

The developer must continue to be able to test multiple players on one development machine.

Conceptually:

```text
ConnectionMode

LOCAL
    Existing local testing path

RELAY
    New Internet session path
```

Local testing should not require:

```text
Relay allocation
Internet join code
Remote player
UGS session creation
```

where avoidable.

The goal is:

```text
Existing Local Multiplayer
          +
New Relay Multiplayer
```

not:

```text
Existing Local Multiplayer
          ↓
        Removed
          ↓
Relay Required For Everything
```

---

# 3. Do Not Replace Netcode for GameObjects

Bullseye already uses:

```text
Netcode for GameObjects (NGO)
```

REQ-064 should build on that architecture.

Do not migrate the project to:

```text
Netcode for Entities
Mirror
Photon
FishNet
custom networking
```

as part of this ticket.

Relay is the Internet connection layer.

NGO remains responsible for the game's networked GameObjects and gameplay state.

---

# 4. Recommended Unity Architecture

Use the current Unity multiplayer stack appropriate for the existing NGO project:

```text
Unity Gaming Services
        ↓
Authentication
        ↓
Multiplayer Services / Sessions
        ↓
Relay
        ↓
Unity Transport
        ↓
Netcode for GameObjects
```

Use the current supported Unity Multiplayer Services SDK and Sessions architecture rather than introducing legacy Relay/Lobby integration patterns unless compatibility with the existing project specifically requires otherwise.

Cursor should inspect installed Unity/package versions before making package changes.

Do not blindly downgrade working multiplayer packages.

---

# 5. Connection Mode Abstraction

Create a clear distinction between connection modes.

Suggested concept:

```csharp
public enum MultiplayerConnectionMode
{
    Local,
    Relay
}
```

Exact naming is flexible.

The important requirement is that gameplay code should not have to ask:

```text
"Am I using Relay?"
```

throughout dozens of scripts.

Connection establishment should be centralized.

Conceptually:

```text
Menu
   ↓
Multiplayer Connection Service
   ↓
Choose Transport Setup
   ├── Local
   └── Relay
   ↓
NGO NetworkManager
   ↓
Gameplay
```

---

# 6. Centralized Multiplayer Session Service

Create or extend a centralized service responsible for multiplayer session lifecycle.

Suggested names:

```text
MultiplayerSessionManager
OnlineSessionManager
MultiplayerConnectionManager
SessionService
```

Responsibilities should include:

```text
Initialize Unity Gaming Services
Authenticate player for online play
Create Relay-backed session
Obtain join code
Join session by code
Leave session
Handle connection state
Handle errors
Start NGO networking through the appropriate path
```

Do not scatter Relay logic across:

```text
MainMenu
PlayerController
Weapon scripts
GameManager
Spawn scripts
```

---

# 7. Local Connection Path

Preserve a simple local development route.

Conceptually:

```text
StartLocalHost()
StartLocalClient()
```

This should continue using the project's existing local networking configuration.

Local development should remain useful for rapid testing of:

```text
Weapons
Animations
Bullseye movement
Damage
Respawning
HUD
Scoreboard
Movement
Grenades
```

Do not require creating a cloud session to test a small gameplay change locally.

---

# 8. Relay Connection Path

Create separate online operations such as:

```text
HostRelaySession()
JoinRelaySession(joinCode)
LeaveRelaySession()
```

The exact implementation may differ according to the installed version of Unity's Multiplayer Services SDK.

Use the current supported APIs for:

```text
Session creation
Relay-backed networking
Join code generation
Joining by code
```

---

# 9. Unity Gaming Services Initialization

Online multiplayer requires Unity Gaming Services to initialize before Relay session operations.

Create a reliable initialization flow.

Conceptually:

```text
Game Startup
     ↓
Initialize UGS
     ↓
Ready for Online Services
```

Initialization should:

* occur once,
* avoid duplicate initialization calls,
* expose success/failure state,
* fail gracefully if Internet services are unavailable.

Do not allow repeated button presses to create multiple competing initialization processes.

---

# 10. Prototype Authentication

For REQ-064, use Unity Authentication in the simplest appropriate prototype configuration.

Anonymous authentication is acceptable for this stage.

Conceptually:

```text
Initialize UGS
     ↓
Check authentication
     ↓
Authenticate anonymously if necessary
     ↓
Online multiplayer available
```

Do not add:

```text
email login
password login
Steam login
account registration
OAuth UI
```

in this ticket.

---

# 11. Keep UGS Identity Separate From REQ-062 Profile Identity

REQ-062 introduced:

```text
PlayerProfileId
```

Unity Authentication may introduce:

```text
UnityPlayerId
```

These should remain separate concepts.

Do not replace:

```text
PlayerProfileId
```

with:

```text
Unity Authentication PlayerId
```

Conceptually:

```text
Bullseye PlayerProfileId
    = persistent Bullseye career/profile identity

Unity Authentication PlayerId
    = Unity online-services identity

SteamId
    = future platform identity
```

These identities may eventually be associated, but they should not be conflated in REQ-064.

---

# 12. Anonymous Authentication Limitation

Treat anonymous Unity Authentication as temporary infrastructure for multiplayer prototyping.

Do not assume it is a permanent recoverable Bullseye player account.

The career/profile architecture from REQ-062 remains the player's persistent local Bullseye identity for now.

---

# 13. Host Online Flow

Add a development-facing or menu-facing option:

```text
HOST ONLINE
```

Flow:

```text
Player selects Host Online
        ↓
Ensure UGS initialized
        ↓
Ensure player authenticated
        ↓
Create multiplayer session
        ↓
Configure session for Relay networking
        ↓
Relay connection established
        ↓
NGO host starts
        ↓
Join code returned
        ↓
Display join code
```

The host should remain in control of the game's host/server role.

---

# 14. Join Code

After creating an online Relay session, display the Unity-generated join code clearly.

Example UI:

```text
ONLINE MATCH

JOIN CODE

    X7K92Q

Waiting for players...

[ COPY CODE ]

[ CANCEL ]
```

Do not hard-code assumptions about exact join-code length unless required by the API.

The displayed value should come directly from the created session.

---

# 15. Copy Join Code

If straightforward on the target platform, provide:

```text
COPY CODE
```

which copies the join code to the system clipboard.

This is optional if clipboard support becomes unnecessarily complicated.

Displaying the code clearly is mandatory.

---

# 16. Join Online Flow

Add:

```text
JOIN ONLINE
```

The joining player should see something similar to:

```text
ENTER JOIN CODE

[ ______ ]

[ JOIN ]
```

Flow:

```text
Enter code
     ↓
Ensure UGS initialized
     ↓
Ensure authenticated
     ↓
Join session by code
     ↓
Configure/connect networking
     ↓
NGO client connects
     ↓
Player spawns
```

---

# 17. Join Code Input Handling

Join-code input should:

* ignore accidental leading/trailing whitespace,
* handle lowercase/uppercase appropriately,
* reject obviously empty input,
* not require a mouse,
* work with keyboard,
* be structured to support controller text entry later.

If Unity codes are case-insensitive through the service, normalize accordingly.

Do not invent a separate Bullseye join-code format.

---

# 18. Connection State

Create a clear connection-state model.

Suggested states:

```text
Offline
InitializingServices
Authenticating
CreatingSession
WaitingForPlayers
JoiningSession
Connecting
Connected
Disconnecting
Error
```

Exact names may differ.

The UI should be able to react to these states.

---

# 19. User Feedback During Connection

Do not leave the player wondering whether a button worked.

Examples:

```text
Initializing online services...
```

```text
Creating online match...
```

```text
Joining match...
```

```text
Connected.
```

```text
Unable to join match.
```

Use concise player-facing status messages.

---

# 20. Prevent Duplicate Connection Attempts

Disable or guard relevant controls while an operation is already running.

For example:

```text
JOIN
```

should not allow ten rapid clicks to start ten simultaneous join requests.

Likewise:

```text
HOST ONLINE
```

should not create multiple sessions from repeated input.

---

# 21. Error Handling

Handle common failures gracefully.

Examples:

```text
Invalid join code
Expired/nonexistent session
No Internet connection
UGS initialization failure
Authentication failure
Relay allocation/session failure
Host unavailable
Connection timeout
Session full
Unexpected disconnection
```

Do not leave the game indefinitely stuck on:

```text
Connecting...
```

---

# 22. Player-Friendly Errors

Avoid exposing raw exceptions as the only feedback.

Instead of:

```text
RequestFailedException 16001
```

prefer something like:

```text
Unable to join this match.

Check the join code and try again.
```

Developer logs should still retain technical details.

---

# 23. Logging

Add useful diagnostic logs.

Examples:

```text
[Multiplayer] Initializing Unity Gaming Services.

[Multiplayer] Authentication succeeded.

[Multiplayer] Creating Relay session.

[Multiplayer] Online session created.

[Multiplayer] Join code: X7K92Q

[Multiplayer] Joining session by code.

[Multiplayer] Relay network connected.

[Multiplayer] Client connected.

[Multiplayer] Client disconnected.
```

Avoid logging continuously every frame.

---

# 24. Do Not Log Sensitive Tokens

Do not log:

```text
authentication tokens
service credentials
private keys
access tokens
```

Join codes are designed to be shared for joining a session and may be logged during development.

---

# 25. Existing NetworkManager

Reuse the existing NGO:

```text
NetworkManager
```

where practical.

Do not create multiple competing NetworkManagers simply to support Relay.

The selected connection mode should configure the appropriate networking path before:

```text
StartHost()
```

or equivalent session-driven startup occurs.

---

# 26. Existing Player Prefab

Continue using the existing player prefab and spawning architecture.

Successful Relay connection should lead into the same normal networked player spawning path used by local multiplayer.

Conceptually:

```text
LOCAL CONNECTION ─┐
                  ├──> NGO Connected
RELAY CONNECTION ─┘
                         ↓
                  Existing Player Spawn
                         ↓
                     Gameplay
```

---

# 27. Do Not Fork Gameplay Logic

Avoid code such as:

```csharp
if (usingRelay)
{
    FireWeaponRelay();
}
else
{
    FireWeaponLocal();
}
```

Relay should change the connection transport/session path, not create a separate version of Bullseye gameplay.

Existing networked systems should ideally behave identically after the connection exists.

---

# 28. Remote Player Spawn Validation

When a Relay client joins:

* host should see remote player spawn,
* client should see host,
* client should see their own player correctly,
* player ownership should be correct,
* cameras should attach only to the local player's character,
* remote players should not control one another.

Verify existing ownership assumptions under a real remote connection.

---

# 29. Remote Movement Validation

Test:

```text
Walking
Sprinting
Jumping
Crouching
Prone
Dolphin diving
Other currently implemented movement
```

The goal is not to perfect lag compensation in REQ-064.

The goal is to confirm these systems function over Relay without obvious ownership or synchronization failures.

---

# 30. Remote Weapon Validation

At minimum, verify:

```text
Weapon equipped
Weapon visible
Shooting visible remotely
Damage occurs
Eliminations replicate
Reload state does not catastrophically fail
Weapon pickups remain functional
```

Log separate bugs if existing gameplay systems expose deeper synchronization problems.

Do not expand REQ-064 indefinitely trying to perfect every weapon behavior.

---

# 31. Remote Bullseye Validation

Verify that existing bullseye networking does not completely fail across Relay.

At minimum:

```text
Host can see client's bullseye
Client can see host's bullseye
Bullseye state remains associated with correct player
Detach/reattach events do not create duplicate objects
```

The existing bullseye surface-motion quality problem is **not part of REQ-064**.

Do not attempt to solve mesh traversal here.

---

# 32. Remote Grenade Validation

Verify current grenade systems at a basic level.

Examples:

```text
Grenade spawned by host visible to client
Grenade spawned by client visible to host
Explosion occurs for both
Bullseye detach state is consistent
```

Detailed grenade balance is outside scope.

---

# 33. Damage Authority

Do not redesign the entire damage-authority model in this ticket unless the current implementation fundamentally prevents remote play.

Cursor should inspect whether the existing host/server-authoritative damage architecture remains functional through Relay.

Document any significant authority/security limitations discovered.

---

# 34. Remote Respawn

Verify that after death:

```text
Death replicated
     ↓
Respawn occurs
     ↓
Correct player regains control
     ↓
Other clients see respawn
```

This is essential for completing an actual multiplayer test.

---

# 35. Remote Scoreboard Validation

REQ-056 introduced the match scoreboard.

Verify that basic match values continue to synchronize with a Relay client.

At minimum:

```text
Eliminations
Deaths
Assists
```

where currently supported.

Do not build a new scoreboard.

---

# 36. REQ-062 / REQ-063 Profile Interaction

Lifetime player profiles remain local for now.

Do not attempt to synchronize entire persistent profiles between host and clients.

Each installation should retain its own:

```text
PlayerProfile
```

Future tickets may determine which profile information is sent into online matches.

---

# 37. Display Name

If REQ-063 has established a persistent display name, make the multiplayer architecture capable of sending the player's display name into the session.

However, do not allow this requirement to derail the core Relay milestone.

Minimum priority:

```text
Remote connection works.
```

Secondary priority:

```text
Remote player identity/display name works.
```

---

# 38. Session Size

Use a configurable maximum player count.

Do not hard-code the entire architecture permanently to two players simply because the first cross-state test will involve two players.

Example:

```text
MaxPlayers = configurable
```

For initial testing, a small value is completely acceptable.

---

# 39. Host Is Still the Game Host

REQ-064 uses a client-host/listen-server style architecture.

Conceptually:

```text
Host Player
    =
NGO Host
    +
Playing Client
```

Relay does not mean Unity becomes authoritative over the game simulation.

The host machine still hosts the gameplay session.

---

# 40. Host Leaving

For this initial implementation, if the host leaves:

```text
Session may end.
```

That is acceptable.

Do not implement host migration in REQ-064.

Clients should receive a reasonable message and return safely to the menu.

Example:

```text
Host disconnected.
```

---

# 41. Client Leaving

A client should be able to leave without crashing the host.

Flow:

```text
Client selects Leave
     ↓
Disconnect from NGO/session
     ↓
Leave Relay session
     ↓
Return to menu
```

Clean up session/network state appropriately.

---

# 42. Host Ending Session

The host should have a clean way to:

```text
END SESSION
```

or return to the menu.

This should:

```text
Disconnect clients
Stop NGO host
Leave/delete session as appropriate
Clear active join code
Reset session state
Return to offline/local-ready state
```

---

# 43. Rehosting

After ending an online session, the host should be able to create a new one without restarting the game.

Expected:

```text
Host Online
     ↓
Play
     ↓
End Session
     ↓
Host Online again
     ↓
New session created
```

Avoid stale Relay/session state.

---

# 44. Rejoining

Where reasonable, a player who returns to the menu should be able to enter a new valid join code and connect again without restarting the application.

Full automatic reconnect behavior is outside scope.

---

# 45. Main Menu Integration

Add simple UI paths appropriate to the existing menu.

Suggested first version:

```text
MULTIPLAYER

[ HOST ONLINE ]

[ JOIN ONLINE ]

[ LOCAL / DEVELOPMENT ]
```

This does not need to be the game's final menu architecture.

REQ-065 will later expand multiplayer setup around maps and game modes.

---

# 46. Development UI Is Acceptable

Because Bullseye remains a prototype, the initial Relay interface can be utilitarian.

Prioritize:

```text
Reliable connection
Clear join code
Clear state
Useful errors
```

over visual polish.

Do not spend disproportionate effort creating a final-production lobby interface.

---

# 47. Preserve Current Development Workflow

The developer should still be able to rapidly:

```text
Enter Play Mode
Launch local client(s)
Test multiplayer
Stop
Modify code
Repeat
```

without needing a second physical computer.

Unity Multiplayer Play Mode or the project's current equivalent workflow should remain functional.

---

# 48. Same-Machine Relay Testing

It is acceptable to optionally test Relay between multiple local instances.

However:

```text
Relay
```

should not become mandatory for same-machine development.

We need both:

```text
Fast local testing

and

Real Internet testing
```

---

# 49. Network Simulation

If Multiplayer Tools / network simulation is already available or straightforward to preserve, ensure REQ-064 does not interfere with it.

Future testing may intentionally simulate:

```text
Latency
Packet loss
Jitter
```

Do not make simulated bad-network behavior part of the acceptance criteria for this ticket.

---

# 50. Cross-State Test Scenario

The primary real-world acceptance test is:

```text
HOST
Utah
Home Internet
PC A

JOINER
Nevada
Separate Internet connection
PC B
```

The two computers must **not** need to be:

```text
on the same Wi-Fi
on the same LAN
connected through VPN
manually port-forwarded
```

The joiner should only need:

```text
Bullseye build
Internet connection
Join code
```

---

# 51. Build Compatibility

Remote players must use compatible builds.

For REQ-064, assume:

```text
same Bullseye version/build
```

Do not implement sophisticated version negotiation yet.

If easy, store/log a simple build/version string for debugging.

---

# 52. No Port Forwarding

The developer should not need to configure router port forwarding to conduct the normal Relay test.

The purpose of Relay is to provide the remote connection path.

Do not instruct the normal player flow to expose their home IP.

---

# 53. No Manual IP Entry

Do not require the joining player to enter:

```text
IP address
port
router configuration
```

for Relay mode.

The normal remote flow is:

```text
Join Code
```

---

# 54. Scene Loading

If the current architecture loads into a gameplay scene after hosting/joining, confirm that Relay clients transition appropriately.

Desired flow:

```text
Host creates session
Client joins
Players connected
        ↓
Host begins game / gameplay scene loads
        ↓
All required players transition correctly
```

Do not create the full Custom Match lobby yet.

REQ-065 will address that architecture more thoroughly.

---

# 55. Do Not Build Map Selection Yet

REQ-064 should not implement:

```text
map browser
map voting
random map selection
map rotation
```

Use the existing gameplay scene/map necessary to validate networking.

Map architecture belongs in REQ-065.

---

# 56. Do Not Build Game Modes Yet

Do not implement:

```text
Free For All selector
Team Deathmatch selector
Oddball-style mode
playlist selection
```

in this ticket.

Use the current/default gameplay mode.

REQ-065 will establish game-mode definitions and custom-match configuration.

---

# 57. No Matchmaking Yet

Do not implement:

```text
Quick Play
skill matching
public queues
MMR matching
automatic opponent discovery
```

REQ-064 is invitation-style testing:

```text
Host
    ↓
Join Code
    ↓
Known Player Joins
```

---

# 58. No Public Server Browser Yet

Do not add:

```text
public room list
server browser
session browser
searchable public lobbies
```

in this ticket.

---

# 59. No Steam Networking Yet

Do not integrate:

```text
Steamworks
Steam invites
Steam Networking Sockets
Steam lobbies
Steam matchmaking
```

in REQ-064.

Unity Relay should establish the initial Internet multiplayer milestone independently of eventual Steam integration.

---

# 60. No Dedicated Server Yet

Do not create or deploy:

```text
headless server
Unity Multiplay server
AWS server
Azure server
dedicated server executable
```

REQ-064 uses the existing host-client architecture.

Dedicated authoritative servers may be evaluated much later if Bullseye needs them.

---

# 61. No Host Migration Yet

If the host disconnects, ending the match is acceptable.

Do not implement:

```text
new host election
game-state transfer
seamless host migration
```

in REQ-064.

---

# 62. No NAT/Punchthrough Custom Code

Do not write custom NAT traversal or hole-punching systems.

That would defeat the purpose of using Relay.

---

# 63. Service Configuration

Cursor should identify any required Unity project/dashboard configuration needed for Multiplayer Services.

Where something cannot be completed purely in code, provide the developer with a concise checklist.

Example:

```text
Unity Dashboard step required:
1. ...
2. ...
3. ...
```

Do not silently assume dashboard configuration has already been completed.

---

# 64. Package Changes

Before installing or changing packages:

1. inspect current `manifest.json`,
2. inspect existing NGO/Unity Transport versions,
3. inspect Unity Editor version,
4. avoid incompatible package combinations,
5. preserve existing networking functionality.

Use current package APIs compatible with the project rather than copying outdated tutorial code.

---

# 65. Separation of Concerns

Desired architecture:

```text
UI
    ↓
MultiplayerSessionManager
    ↓
Connection Mode
    ├── Local
    └── Relay
            ↓
        UGS Session
            ↓
        Unity Transport
            ↓
        NGO NetworkManager
            ↓
          Gameplay
```

Avoid:

```text
UI button
    ↓
500 lines of Relay + authentication + NGO + scene logic
```

inside one menu script.

---

# 66. Cancellation

Where practical, allow the user to back out of:

```text
hosting
joining
waiting
```

without leaving the game in an invalid networking state.

At minimum, returning to the main menu should cleanly restore an offline state.

---

# 67. Connection Timeout

A failed join must eventually fail.

Do not permit infinite waiting.

Use appropriate service/network timeout handling and return the player to a usable state.

---

# 68. Disconnect Cleanup

After any disconnect:

```text
clear active session reference
clear join code
reset UI state
stop NGO networking as appropriate
release/leave session resources
allow another host/join attempt
```

Avoid requiring an Editor restart after a failed connection.

---

# 69. Debug Information

For development, provide enough diagnostics to troubleshoot remote testing.

Useful values may include:

```text
Connection Mode
UGS Initialization State
Authentication State
Unity Player ID
Session ID
Join Code
NGO IsHost
NGO IsClient
Local Client ID
Connected Client Count
```

This may be visible through:

```text
Inspector
debug panel
console
```

It does not need to become polished production UI.

---

# 70. Remote Test Debug Overlay

If straightforward, add a small optional developer overlay for online testing.

Example:

```text
ONLINE DEBUG

Mode: Relay
Role: Host
Players: 2
Ping: —
Session: Connected
```

Actual ping display is optional.

Do not build a full networking profiler UI.

---

# 71. First Real Internet Test Checklist

When ready for the first cross-state test:

```text
1. Build the same Bullseye version on both computers.

2. Host launches Bullseye.

3. Host selects Host Online.

4. Host receives join code.

5. Host sends join code to remote player.

6. Remote player selects Join Online.

7. Remote player enters code.

8. Both players load into the same game.

9. Both players can see each other.

10. Both players can move.

11. Both players can shoot.

12. Both players can damage/eliminate each other.

13. Both players can die and respawn.

14. Existing scoreboard/stat systems remain functional.

15. Client leaves and rejoins/new session is tested.

16. Host ends match cleanly.
```

---

# 72. Testing — Local Regression

Before considering REQ-064 complete:

### Test A — Existing Local Host

Start the game using the existing local development method.

Expected:

```text
Local multiplayer still works.
```

---

### Test B — Same-Machine Multiple Players

Use the project's existing local multi-client workflow.

Expected:

```text
Multiple local players connect.
Existing gameplay continues to function.
Relay is not required.
```

---

### Test C — Local Restart

Stop local multiplayer.

Start it again.

Expected:

```text
No stale Relay/session state interferes.
```

---

# 73. Testing — Online Host

### Test D — Create Online Match

Select:

```text
HOST ONLINE
```

Expected:

```text
UGS initializes.
Authentication succeeds.
Relay-backed session created.
Join code appears.
Host networking starts.
```

---

### Test E — Repeated Host Input

Rapidly press Host Online multiple times.

Expected:

```text
Only one host/session attempt occurs.
```

---

# 74. Testing — Online Join

### Test F — Valid Join Code

Second computer enters valid code.

Expected:

```text
Session joined.
NGO client connects.
Remote player spawns.
```

---

### Test G — Invalid Join Code

Enter an invalid code.

Expected:

```text
No crash.
Useful error shown.
Player can retry.
```

---

### Test H — Expired Session

Attempt to join a code belonging to a session that has ended.

Expected:

```text
Join fails cleanly.
Player returns to usable Join screen.
```

---

# 75. Testing — Cross-State Gameplay

### Test I — Remote Movement

Host and remote client move simultaneously.

Expected:

```text
Both players see one another moving.
Ownership remains correct.
```

---

### Test J — Remote Combat

Each player shoots and damages the other.

Expected:

```text
Damage replicates.
Eliminations occur.
```

---

### Test K — Respawning

Each player dies at least once.

Expected:

```text
Respawn succeeds for both players.
```

---

### Test L — Weapon Use

Test several existing weapons.

Expected:

```text
Basic firing/damage behavior works remotely.
```

Document individual weapon synchronization defects separately if necessary.

---

### Test M — Grenades

Each player uses a grenade.

Expected:

```text
Basic grenade spawn/explosion behavior is shared.
```

---

### Test N — Bullseye

Observe both players' bullseyes.

Expected:

```text
Correct bullseye belongs to correct player.
No catastrophic duplication/desynchronization.
```

Smooth mesh traversal is outside scope.

---

# 76. Testing — Disconnects

### Test O — Client Leaves

Remote client leaves.

Expected:

```text
Host remains functional.
Remote player despawns.
```

---

### Test P — Host Leaves

Host ends/disconnects.

Expected:

```text
Remote client is informed.
Client returns safely to menu.
```

---

### Test Q — New Session

Host creates another online session.

Expected:

```text
New join code/session works without restarting Bullseye.
```

---

# 77. Acceptance Criteria

REQ-064 is complete when:

* [ ] Existing local multiplayer testing still works.
* [ ] Relay functionality exists as an additional connection mode rather than replacing local networking.
* [ ] Bullseye remains on Netcode for GameObjects.
* [ ] Unity Gaming Services initializes reliably for online play.
* [ ] Prototype online authentication succeeds.
* [ ] Unity Authentication identity remains separate from REQ-062 PlayerProfileId.
* [ ] A player can select Host Online.
* [ ] Hosting creates a Relay-backed multiplayer session.
* [ ] A service-generated join code is displayed.
* [ ] Another player can enter the join code.
* [ ] Joining by code connects the remote player to the session.
* [ ] Two computers on different Internet connections can connect without manual IP entry.
* [ ] Router port forwarding is not required for the standard Relay flow.
* [ ] Host and client spawn correctly.
* [ ] Host and client can see one another.
* [ ] Host and client retain correct player ownership.
* [ ] Basic remote movement works.
* [ ] Basic remote shooting works.
* [ ] Damage/eliminations work remotely.
* [ ] Death and respawn function remotely.
* [ ] Existing weapon networking remains broadly functional.
* [ ] Existing grenade networking remains broadly functional.
* [ ] Existing bullseye networking remains broadly functional.
* [ ] Existing scoreboard/stat tracking remains broadly functional.
* [ ] Invalid join codes fail gracefully.
* [ ] Failed connection attempts do not require restarting the game.
* [ ] Client can leave cleanly.
* [ ] Host can end the session cleanly.
* [ ] Another online session can be created after ending the previous session.
* [ ] Connection/session code is centralized rather than duplicated throughout gameplay scripts.
* [ ] Useful networking diagnostics/logging exist.
* [ ] No matchmaking system has been added.
* [ ] No public server browser has been added.
* [ ] No dedicated-server architecture has been added.
* [ ] No Steam networking has been added.
* [ ] No map-selection system has been added.
* [ ] No game-mode selection system has been added.

---

# 78. Explicit Non-Goals

Do NOT implement the following in REQ-064:

```text
Public matchmaking

Skill-based matchmaking

Quick Play

Public server browser

Custom Match map selection

Game-mode selection

Map voting

Random map rotation

Steamworks

Steam invites

Steam matchmaking

Steam Networking Sockets

Dedicated servers

Host migration

Cloud-hosted authoritative servers

Ranked multiplayer

MMR

Party system

Friends system

Voice chat

Text chat

Anti-cheat

Full lag compensation rewrite

Full client prediction rewrite

Profile cloud sync

Research telemetry upload

Production account registration
```

These belong in later requirements.

---

# 79. Relationship to REQ-065

REQ-064 establishes:

```text
Can Bullseye players connect over the Internet?
```

REQ-065 will establish:

```text
What match are those players joining?
```

That future ticket should introduce:

```text
MapDefinition

GameModeDefinition

Custom Match Setup

Selected Map

Selected Game Mode

Session Configuration
```

The two systems should eventually work together as:

```text
HOST CUSTOM MATCH

Choose Mode
     ↓
Choose Map
     ↓
Create Relay Session
     ↓
Share Join Code
     ↓
Remote Player Joins
     ↓
Match Starts
```

Do not implement that full flow in REQ-064.

---

# 80. Why This Ticket Matters

Bullseye already contains substantial networked gameplay, but same-machine testing does not fully answer whether the game functions as a real Internet multiplayer title.

REQ-064 should establish the first genuine remote multiplayer milestone:

```text
Utah Player
        ↓
     Internet
        ↓
 Unity Relay
        ↓
     Internet
        ↓
Nevada Player
```

Once this works, remote development/testing becomes dramatically more useful.

The developers can test:

```text
real latency
remote movement
remote combat
third-person animations
bullseye synchronization
grenade synchronization
respawning
scoreboard behavior
disconnect behavior
```

under actual Internet conditions.

This is especially valuable before returning to the world-view weapon-animation work, because one developer can control a character remotely while the other directly observes third-person behavior.

---

# 81. Design Philosophy

Keep REQ-064 focused.

The objective is **not**:

> Build Bullseye's complete production multiplayer platform.

The objective is:

> Allow one Bullseye player to host an Internet session, give another player a join code, and successfully play the existing game together from separate networks.

Preserve the local development workflow.

Add Relay as a second connection path.

Keep gameplay networking shared.

Once this foundation works reliably, Bullseye can build higher-level multiplayer systems on top of it:

```text
Relay Multiplayer
        ↓
Custom Matches
        ↓
Maps + Game Modes
        ↓
Public Sessions
        ↓
Matchmaking
        ↓
Steam Integration
        ↓
Potential Dedicated Infrastructure
```

REQ-064 should accomplish the first step cleanly without prematurely building the rest.
