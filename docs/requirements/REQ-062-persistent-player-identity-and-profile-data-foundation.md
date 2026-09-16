# REQ-062 — Persistent Player Identity & Profile Data Foundation

## Summary

Create a persistent local player identity and profile/statistics foundation for Bullseye.

The game already tracks an increasing amount of match telemetry and player performance data through systems developed in REQ-055, REQ-056, and REQ-061. We now need an architecture that allows an individual player to retain a stable identity and lifetime statistics across multiple game sessions.

This ticket is **not** intended to create a polished profile screen, account-registration system, progression system, or Steam integration yet.

Instead, REQ-062 should establish the underlying data architecture so that future systems can reliably answer questions such as:

- Who is this player?
- Is this the same player who played yesterday?
- How many matches has this player completed?
- What are their lifetime eliminations, deaths, assists, and wins?
- What are their statistics with each weapon?
- How have they interacted with the moving bullseye mechanic over time?
- How should match statistics be aggregated into lifetime statistics?
- How can a future Steam identity be attached without redesigning the entire system?
- How can research telemetry remain separate from the player's normal game identity?

The implementation should be modular, extensible, and compatible with eventual Steam integration.

---

# 1. Primary Goal

Create a persistent architecture with the following conceptual relationship:

```text
PlayerIdentity
      |
      v
PlayerProfile
      |
      +----------------------+
      |                      |
      v                      v
LifetimeStats           WeaponStats
      ^
      |
      |
Finalized MatchStats
```

Each local player should receive a stable internal identifier.

That identifier should remain the same after:

- ending a match,
- returning to the main menu,
- closing the game,
- reopening the game,
- restarting the computer.

Lifetime statistics should persist between sessions.

---

# 2. Important Architectural Principle

The game should distinguish between several different concepts that may eventually be related but should **not** be treated as the same identifier.

At minimum, distinguish:

```text
PlayerProfileId
SteamId                  // Future
ResearchParticipantId    // Future / research-specific
NetworkClientId          // Temporary match/session identity
```

These IDs serve different purposes.

They should not be treated as interchangeable.

---

# 3. PlayerIdentity

Create a persistent identity object/model for the player.

Suggested structure:

```csharp
PlayerIdentity
{
    string PlayerProfileId;
    string DisplayName;

    string SteamId; // optional / unused for now

    DateTime CreatedAtUtc;
    DateTime LastPlayedAtUtc;
}
```

The exact class names may differ if Cursor determines that another architecture better fits the project.

The important requirement is the behavior.

---

# 4. PlayerProfileId

Every player should receive a persistent internal ID.

Recommended implementation:

```text
GUID / UUID
```

Example:

```text
4fa7d893-13f6-451f-82c5-2b556a7d1ef8
```

The ID should:

- be generated automatically the first time the player launches the game,
- be stored persistently,
- be reused on subsequent launches,
- not depend on the player's display name,
- not depend on NetworkObjectId,
- not depend on NGO ClientId,
- not change between matches.

Do not use:

```text
ClientId
NetworkObjectId
PlayerObjectId
```

as the persistent profile identifier.

Those identifiers are temporary networking constructs.

---

# 5. Player Profile

Create a persistent `PlayerProfile` data model.

Example:

```csharp
PlayerProfile
{
    PlayerIdentity Identity;
    LifetimeStats LifetimeStats;

    Dictionary<string, WeaponLifetimeStats> WeaponStats;
}
```

This should be designed so additional profile categories can be added later.

Potential future additions include:

```text
Progression
Achievements
Cosmetics
RankedStats
GameModeStats
SeasonStats
Settings
SteamMetadata
```

Do not implement those systems now.

Only ensure the architecture does not make them difficult to add later.

---

# 6. Lifetime Statistics

Create a lifetime statistics model.

At minimum, support:

```text
MatchesPlayed
MatchesCompleted

Wins
Losses

Eliminations
Deaths
Assists

ShotsFired
ShotsHit

TotalDamageDealt
TotalDamageReceived

TotalPlayTimeSeconds
```

Where feasible, also include existing Bullseye-specific metrics already tracked by the telemetry system.

Examples include:

```text
AttachedBullseyeEliminations
DetachedBullseyeEliminations

BullseyeHits
BodyHits
HeadHits

GrenadeBullseyeDetachments

BodySlamEliminations

LongestEliminationDistance

TotalEliminationDistance

RicochetEliminations
```

Do not duplicate calculation logic unnecessarily.

If REQ-055 or REQ-061 already tracks a statistic through a centralized telemetry system, the profile system should consume finalized statistics from that system.

---

# 7. Weapon Lifetime Statistics

Each weapon should have its own persistent statistics.

Suggested structure:

```csharp
WeaponLifetimeStats
{
    string WeaponId;

    int Eliminations;

    int ShotsFired;
    int ShotsHit;

    float DamageDealt;

    int HeadHits;
    int BodyHits;
    int BullseyeHits;

    float LongestEliminationDistance;
}
```

The system should support all current weapons and automatically support future weapons where possible.

Avoid large hard-coded structures such as:

```csharp
int AkKills;
int ShotgunKills;
int PistolKills;
int DmrKills;
int SniperKills;
```

Prefer an extensible system keyed by a stable weapon identifier.

Example:

```text
weapon_pistol
weapon_ak
weapon_shotgun
weapon_dmr
weapon_sniper
weapon_bazooka
```

Use the project's existing weapon-definition architecture where practical.

---

# 8. Match Stats vs Lifetime Stats

This distinction is extremely important.

Match statistics should remain temporary during a match.

Example:

```text
MatchStats
```

At the end of the match:

```text
MatchStats
      ↓
FinalizeMatch()
      ↓
LifetimeStats
```

The lifetime profile should **not** continuously increment persistent values every time a bullet is fired.

Instead, prefer the workflow:

```text
Gameplay Event
      ↓
Match Telemetry
      ↓
Match Ends
      ↓
Finalize Match Statistics
      ↓
Update Lifetime Profile
      ↓
Save Profile
```

This reduces unnecessary disk writes and helps prevent inconsistent profile state.

---

# 9. Match Finalization

Create a centralized method responsible for incorporating completed match statistics into the player's lifetime profile.

Suggested concept:

```csharp
PlayerProfileManager.FinalizeMatch(MatchStats stats)
```

or equivalent.

The method should update values such as:

```text
MatchesPlayed += 1
Eliminations += MatchStats.Eliminations
Deaths += MatchStats.Deaths
Assists += MatchStats.Assists
ShotsFired += MatchStats.ShotsFired
ShotsHit += MatchStats.ShotsHit
```

Weapon statistics should also be aggregated.

Example:

```text
MatchStats
    Rifle:
        7 eliminations

Lifetime:
    Rifle:
        previous = 41
        new = 48
```

---

# 10. Longest / Maximum Statistics

Some statistics should not be summed.

For example:

```text
LongestEliminationDistance
```

should update using:

```csharp
Mathf.Max(
    previousLongest,
    matchLongest
);
```

Cursor should review all tracked telemetry values and determine whether each is:

```text
SUM
MAX
MIN
AVERAGE
COUNT
DERIVED
```

Avoid simply adding every statistic together.

---

# 11. Derived Statistics

Avoid storing values that can be reliably calculated from raw lifetime values.

For example:

```text
KDRatio
Accuracy
AverageEliminationDistance
WinRate
```

should preferably be calculated dynamically.

Examples:

```text
KDRatio =
Deaths > 0
? Eliminations / Deaths
: Eliminations
```

```text
Accuracy =
ShotsFired > 0
? ShotsHit / ShotsFired
: 0
```

```text
WinRate =
MatchesCompleted > 0
? Wins / MatchesCompleted
: 0
```

This prevents stored values from becoming inconsistent.

---

# 12. Persistent Storage

Save the profile locally.

Use an appropriate Unity persistence architecture.

Possible options include:

```text
JSON
binary serialization
Unity persistence abstraction
```

For now, human-readable JSON is acceptable and may make development/debugging easier.

Suggested conceptual location:

```text
Application.persistentDataPath
```

Example:

```text
/player/profile.json
```

Do not store the profile inside:

```text
Assets/
Resources/
StreamingAssets/
```

because those are not intended for player-generated persistent data.

---

# 13. Save Manager

Create a centralized profile persistence service.

Suggested architecture:

```text
PlayerProfileManager
or
PlayerProfileService
```

Responsibilities:

```text
LoadProfile()
CreateProfile()
SaveProfile()
FinalizeMatch()
GetProfile()
ResetProfile() // debug only
```

Avoid allowing unrelated gameplay scripts to directly write files.

Gameplay code should communicate with the profile manager.

---

# 14. Startup Behavior

When the game starts:

```text
Check for profile
       |
       +--> Existing profile
       |        |
       |        v
       |     Load profile
       |
       +--> No profile
                |
                v
         Generate PlayerProfileId
                |
                v
         Create default profile
                |
                v
             Save
```

The loaded profile should remain available for the remainder of the game session.

---

# 15. Display Name

For now, allow the profile to contain a display name.

Default behavior could be:

```text
Player
```

or another simple placeholder.

The system should support changing the display name later.

Do **not** build a full username creation flow yet.

The display name should not be used as the player's unique identifier.

Two players should theoretically be capable of having the same display name.

---

# 16. Steam Compatibility

Do not implement Steamworks integration in this ticket.

However, structure the identity architecture so a future Steam account can be associated with the profile.

Example:

```csharp
PlayerIdentity
{
    string PlayerProfileId;

    string SteamId;
}
```

`SteamId` may remain:

```text
null
empty
unused
```

for now.

Future architecture should be capable of doing:

```text
Local PlayerProfile
        +
Steam Account
        ↓
Associated Identity
```

without replacing `PlayerProfileId`.

---

# 17. Do Not Build Username/Password Authentication

REQ-062 should NOT create:

```text
Email login
Password login
Account registration
Forgot password
Cloud authentication
Custom authentication servers
Username uniqueness checking
```

These systems would be premature.

Steam or another platform identity provider may eventually handle authentication.

---

# 18. Networking Separation

The persistent profile system must not become tightly coupled to Netcode for GameObjects.

During a match, there may be mappings such as:

```text
NetworkClientId
      ↓
PlayerProfileId
```

However:

```text
NetworkClientId != PlayerProfileId
```

NetworkClientId is temporary.

PlayerProfileId is persistent.

A player disconnecting and reconnecting should not conceptually become a different lifetime profile.

---

# 19. Host / Client Considerations

The game currently supports multiplayer development using Netcode for GameObjects.

Do not assume that the host's locally stored profile represents all players.

Each local installation should eventually own its own persistent player profile.

For development/testing:

```text
Client A
    -> Profile A

Client B
    -> Profile B
```

If running multiple clients on the same computer creates profile collisions during testing, add a reasonable development/testing solution.

Possible solutions include:

```text
command-line profile override
editor test profile ID
separate development profile paths
```

Do not compromise the production architecture solely to support local multi-client testing.

---

# 20. Research Identity Separation

Bullseye may eventually collect gameplay telemetry for academic research.

The normal player profile identifier should **not automatically function as the research participant identifier**.

Conceptually maintain:

```text
PlayerProfileId
```

separately from:

```text
ResearchParticipantId
```

The research identifier should only be created/linked through the future research consent system.

For example:

```text
PlayerProfileId
      |
      | optional consent-based mapping
      v
ResearchParticipantId
```

Do not implement research consent in REQ-062.

Do not automatically generate a research participant ID simply because a player has a profile.

The goal here is architectural separation.

---

# 21. Telemetry Separation

Avoid writing research telemetry directly into the profile save file.

Think of the systems as:

```text
Player Profile
    = player-facing persistent game state

Match Telemetry
    = detailed gameplay event data

Research Dataset
    = separately consented/exported telemetry
```

Some aggregate values may naturally overlap.

Example:

```text
Lifetime Eliminations
```

may appear in both contexts.

But detailed research event logs should not become part of the normal player-profile file.

---

# 22. Profile Versioning

Add a schema/profile version to the saved profile.

Example:

```csharp
int ProfileVersion = 1;
```

or:

```text
"schemaVersion": 1
```

This is important because the profile structure will almost certainly change during development.

Future versions may need to migrate older save files.

REQ-062 does not need an elaborate migration framework, but the saved format should have a version number from the beginning.

---

# 23. Defensive Loading

The game should handle profile-loading failures gracefully.

Examples:

```text
missing file
empty file
malformed JSON
older profile version
missing new fields
```

Do not allow a corrupted development profile to permanently prevent the game from starting.

At minimum:

1. log the problem clearly,
2. preserve the corrupted file if practical,
3. create a clean fallback profile rather than crashing.

---

# 24. Save Reliability

Use safe save behavior.

Avoid writing directly over the only valid save file if there is a reasonable chance of leaving it corrupted.

A simple approach could be:

```text
write temp file
validate/complete write
replace profile file
```

A lightweight backup file is also acceptable.

Example:

```text
profile.json
profile.backup.json
```

Do not over-engineer this into a full cloud-save system.

---

# 25. Debug Inspector

Provide a useful development/debugging mechanism to inspect the current loaded profile.

This could be:

```text
custom inspector
debug UI
context menu
console dump
```

At minimum, developers should be able to inspect:

```text
PlayerProfileId
DisplayName
MatchesPlayed
Eliminations
Deaths
Assists
WeaponStats
```

without manually opening and interpreting the save file every time.

---

# 26. Debug Reset

Add a development-only way to reset the local player profile.

Example:

```text
Reset Player Profile
```

This should:

1. delete/reset profile data,
2. generate a new profile ID,
3. create default statistics,
4. save the new profile.

This should **not** appear as a normal production-facing button yet.

Protect it behind an editor/debug mechanism.

---

# 27. Logging

Use helpful logs such as:

```text
[PlayerProfile] Loaded profile: <id>

[PlayerProfile] Created new profile: <id>

[PlayerProfile] Saved profile.

[PlayerProfile] Finalized match:
Eliminations +12
Deaths +8
Assists +4
```

Avoid excessive per-frame or per-shot logging.

---

# 28. Integration With REQ-055

REQ-055 established expanded gameplay statistics such as:

```text
Eliminations
Per-weapon eliminations
Attached/detached bullseye eliminations
Grenade detach counts
Body slam eliminations
Assists
Elimination distance
```

REQ-062 should reuse these statistics where practical.

Do not recreate competing tracking logic.

---

# 29. Integration With REQ-056

REQ-056 introduced the in-match scoreboard.

Keep:

```text
Scoreboard Stats
```

conceptually separate from:

```text
Lifetime Profile Stats
```

The scoreboard displays the current match.

The profile stores historical career totals.

Future UI may combine them, but the underlying data should remain separate.

---

# 30. Integration With REQ-061

REQ-061 expands research-relevant aiming telemetry.

Detailed aiming telemetry should generally remain part of the telemetry/research system rather than the normal lifetime profile.

However, aggregate player-facing statistics may be appropriate.

For example:

```text
Lifetime Accuracy
```

could be derived from:

```text
Lifetime ShotsHit
Lifetime ShotsFired
```

But do not store enormous collections of aim samples inside the profile.

---

# 31. Suggested Folder Organization

Cursor should follow the project's existing conventions.

A reasonable structure might resemble:

```text
Assets/
    Scripts/
        PlayerProfile/
            PlayerIdentity.cs
            PlayerProfile.cs
            LifetimeStats.cs
            WeaponLifetimeStats.cs
            PlayerProfileManager.cs
            PlayerProfilePersistence.cs
```

This is only a suggestion.

Do not restructure unrelated systems solely to match this example.

---

# 32. Serialization Requirements

Ensure any dictionaries or complex collections used for weapon statistics serialize correctly with the selected persistence method.

Unity's built-in `JsonUtility` has limitations.

If the current architecture requires dictionaries such as:

```csharp
Dictionary<string, WeaponLifetimeStats>
```

Cursor should choose a serialization approach that reliably supports them.

Possible alternatives include:

```text
serializable List<WeaponLifetimeStats>
custom serialization wrapper
appropriate JSON library already available in project
```

Do not add a large dependency unnecessarily.

---

# 33. Stable Weapon IDs

Weapon stats must not depend solely on display names.

For example, avoid:

```text
"AK-47"
```

being the database key if that text may later be renamed.

Prefer a stable identifier such as:

```text
weapon_rifle_01
```

while the player-facing display name may change independently.

If WeaponDefinition already contains an appropriate stable identifier, reuse it.

---

# 34. No Profile UI Yet

Do not create the full profile/career screen in this ticket.

REQ-063 will handle presentation.

REQ-062 should only create enough debug visibility to verify that persistence works.

---

# 35. No Progression Yet

Do not implement:

```text
XP
levels
prestige
battle pass
unlock trees
currency
cosmetic unlocks
```

These can be layered on top of the profile architecture later if desired.

---

# 36. No Cloud Save Yet

Do not implement:

```text
Steam Cloud
PlayFab
Firebase
custom backend storage
cross-device synchronization
```

Local persistent storage is sufficient for REQ-062.

The architecture should simply avoid making cloud persistence impossible later.

---

# 37. No Achievements Yet

Do not implement Steam achievements or custom achievements in this ticket.

However, lifetime statistics should eventually make achievements easy to evaluate.

Example future condition:

```text
LifetimeBullseyeEliminations >= 100
```

REQ-062 only needs to make those statistics accessible.

---

# 38. Testing Requirements

Verify at least the following scenarios.

### Test A — First Launch

Delete any existing profile.

Start game.

Expected:

```text
New PlayerProfileId generated.
Default profile created.
Profile saved.
```

---

### Test B — Restart Persistence

Record PlayerProfileId.

Close game completely.

Restart game.

Expected:

```text
Same PlayerProfileId loads.
```

---

### Test C — Match Aggregation

Example match:

```text
10 eliminations
6 deaths
3 assists
100 shots
40 hits
```

Finalize match.

Expected lifetime changes:

```text
Eliminations += 10
Deaths += 6
Assists += 3
ShotsFired += 100
ShotsHit += 40
MatchesPlayed += 1
```

---

### Test D — Second Match

Play/finalize another test match.

Confirm values accumulate rather than overwrite.

---

### Test E — Weapon Statistics

Example:

```text
Pistol = 2 eliminations
DMR = 5 eliminations
Shotgun = 3 eliminations
```

Confirm each weapon updates independently.

---

### Test F — Maximum Statistic

Match 1:

```text
Longest elimination = 42m
```

Match 2:

```text
Longest elimination = 30m
```

Lifetime result:

```text
42m
```

Match 3:

```text
Longest elimination = 75m
```

Lifetime result:

```text
75m
```

---

### Test G — Corrupt Save

Temporarily corrupt the profile save file.

Start the game.

Expected:

```text
Game does not crash.
Error is logged.
Fallback profile is created or loaded safely.
```

---

### Test H — Profile Reset

Use development reset function.

Expected:

```text
Old profile cleared.
New PlayerProfileId generated.
Stats reset.
```

---

# 39. Acceptance Criteria

REQ-062 is complete when:

- [ ] A new installation automatically generates a persistent PlayerProfileId.
- [ ] PlayerProfileId survives game restarts.
- [ ] Player identity is separate from NGO ClientId and NetworkObjectId.
- [ ] A persistent PlayerProfile model exists.
- [ ] Lifetime statistics persist between sessions.
- [ ] Match statistics can be finalized into lifetime statistics.
- [ ] Match data is accumulated rather than replacing lifetime totals.
- [ ] Maximum statistics such as longest elimination distance aggregate correctly.
- [ ] Per-weapon lifetime statistics persist.
- [ ] Weapon stats use stable weapon identifiers.
- [ ] Derived metrics are calculated rather than unnecessarily persisted.
- [ ] The profile contains a schema/version value.
- [ ] Save/load failures are handled gracefully.
- [ ] A development method exists to inspect the loaded profile.
- [ ] A development method exists to reset the profile.
- [ ] Detailed research telemetry is not written into the normal profile save.
- [ ] PlayerProfileId and future ResearchParticipantId remain conceptually separate.
- [ ] The architecture can later associate a SteamId without replacing PlayerProfileId.
- [ ] No custom username/password authentication has been introduced.
- [ ] No profile/career UI has been built beyond debugging tools.
- [ ] No XP/progression system has been introduced.
- [ ] Existing REQ-055/056/061 telemetry systems are reused rather than duplicated where practical.

---

# 40. Explicit Non-Goals

Do NOT implement the following as part of REQ-062:

```text
Steamworks integration
Steam login
Steam Cloud
custom account servers
email/password authentication
profile customization UI
career/profile screen
XP
levels
ranked matchmaking
MMR
cosmetics
achievements
friends lists
player search
research consent
research uploads
cloud databases
```

These are separate future requirements.

---

# 41. Future REQ-063 Dependency

REQ-062 should intentionally prepare for:

```text
REQ-063 — Player Profile / Career Screen
```

REQ-063 will consume this data foundation and present player-facing lifetime statistics.

Potential future screen:

```text
PROFILE

PlayerName

Matches Played       84
Wins                 31

Eliminations         1,204
Deaths                 932
Assists                287

K/D                   1.29
Accuracy             34.2%

Favorite Weapon       DMR

Longest Elimination   91.4m
```

Do not build this screen yet.

---

# 42. Design Philosophy

Keep this system boring, reliable, and extensible.

The profile architecture will eventually sit underneath many other systems:

```text
Lifetime Statistics
Profile Screen
Steam Identity
Achievements
Progression
Matchmaking
Research Consent
Player Analytics
Cloud Saves
```

Because of that, REQ-062 should prioritize clean separation of responsibilities over flashy functionality.

The important result of this ticket is that Bullseye gains a stable concept of:

> "This is the same player who played previous matches, and this is their persistent game history."

That identity should remain useful even as the game grows substantially beyond its current prototype state.