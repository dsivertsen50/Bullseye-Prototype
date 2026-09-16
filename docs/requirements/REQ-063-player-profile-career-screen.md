# REQ-063 — Player Profile / Career Screen

## Summary

Create a player-facing **Profile / Career screen** that presents persistent lifetime statistics from the profile architecture created in REQ-062.

REQ-062 establishes the underlying persistent identity and statistics foundation.

REQ-063 should now make that information visible and useful to the player.

This screen should provide a clean overview of:

- player identity,
- lifetime match history,
- combat performance,
- weapon statistics,
- Bullseye-specific statistics,
- and selected career records.

This is primarily a **presentation/UI ticket**.

Do not create a second statistics system.

The Profile / Career screen should read from the persistent profile data created in REQ-062.

---

# 1. Primary Goal

Add a Profile / Career option to the game UI.

Selecting it should open a screen similar conceptually to:

```text
----------------------------------------------------
                     PROFILE
----------------------------------------------------

PlayerName

CAREER

Matches Played                         84
Wins                                   31
Losses                                 53

Eliminations                        1,204
Deaths                                932
Assists                               287

K/D                                  1.29
Accuracy                            34.2%
Win Rate                            36.9%

----------------------------------------------------

WEAPONS

DMR
Eliminations                          312
Accuracy                            41.3%

Rifle
Eliminations                          287
Accuracy                            32.8%

Shotgun
Eliminations                          211

...

----------------------------------------------------

BULLSEYE

Bullseye Eliminations                 426
Attached Bullseye Eliminations        301
Detached Bullseye Eliminations        125
Bullseye Hits                       1,083
Grenade Detachments                    87

----------------------------------------------------

RECORDS

Longest Elimination                 91.4m
Favorite Weapon                       DMR

----------------------------------------------------
```

The exact visual layout is flexible.

The architecture and presentation should be designed so the page can grow substantially later.

---

# 2. Entry Point

Add a button to the appropriate main menu location:

```text
PROFILE
```

or:

```text
CAREER
```

Preferred terminology for now:

```text
PROFILE
```

The Profile screen should be accessible outside of a match.

Do not require the player to enter gameplay to inspect lifetime statistics.

---

# 3. Profile Screen Controller

Create a dedicated profile-screen UI controller.

Suggested concept:

```csharp
PlayerProfileUI
```

or:

```csharp
CareerScreenController
```

Responsibilities should include:

```text
Open screen
Close screen
Retrieve loaded PlayerProfile
Populate statistics
Populate weapon entries
Refresh derived metrics
Handle category navigation
```

The UI controller should **not** own or calculate gameplay telemetry.

It should consume data from the profile system.

---

# 4. Data Source

REQ-063 must read from the profile architecture created in REQ-062.

Conceptually:

```text
PlayerProfileManager
        ↓
GetProfile()
        ↓
PlayerProfileUI
        ↓
Display
```

Do not load profile JSON directly from UI scripts if a centralized `PlayerProfileManager` or equivalent already owns the loaded profile.

The UI should not know where the profile is stored on disk.

---

# 5. Main Profile Header

The screen should display basic identity information.

At minimum:

```text
DisplayName
```

Optionally include a shortened/debug-friendly representation of the profile ID during development.

Example:

```text
Player
Profile ID: 4fa7d893...
```

However, the full internal profile GUID should **not** need to be prominently displayed in the production UI.

---

# 6. Display Name Editing

Allow the player to edit their display name from the Profile screen.

This should be a simple local display-name field.

Example:

```text
Display Name:
[ BullseyePlayer_____ ]

[ SAVE ]
```

Requirements:

- changing the name updates `PlayerIdentity.DisplayName`,
- the new display name persists through REQ-062's save system,
- the display name must not change `PlayerProfileId`,
- display-name uniqueness is not required,
- no online username verification is required.

Do not create an account-registration system.

---

# 7. Career Overview Section

Create a general Career section.

At minimum display:

```text
Matches Played
Matches Completed

Wins
Losses

Eliminations
Deaths
Assists
```

Derived values should include:

```text
K/D Ratio
Win Rate
Accuracy
```

Where possible, use values already derived through the profile/statistics architecture.

---

# 8. Match Statistics Presentation

Suggested presentation:

```text
MATCHES

Matches Played           84
Wins                     31
Losses                   53
Win Rate               36.9%
```

If `MatchesPlayed` and `MatchesCompleted` have different meanings in the current architecture, present them clearly.

Do not display confusing duplicate statistics merely because both fields exist.

Cursor should inspect REQ-062 implementation and determine which fields make sense for the player-facing UI.

---

# 9. Combat Statistics

Create a combat-statistics section.

At minimum:

```text
Eliminations
Deaths
Assists
K/D
Shots Fired
Shots Hit
Accuracy
Total Damage Dealt
```

Potential display:

```text
COMBAT

Eliminations          1,204
Deaths                  932
Assists                 287

K/D                     1.29
Accuracy               34.2%

Damage Dealt         98,421
```

Do not overload the first version with every possible telemetry variable.

Prioritize statistics that are understandable and meaningful to players.

---

# 10. K/D Behavior

Avoid displaying invalid or ugly values.

Examples:

If:

```text
Deaths = 0
Eliminations = 7
```

do not display:

```text
Infinity
NaN
```

Use sensible formatting.

Example:

```text
K/D: 7.00
```

or whatever convention the underlying profile system uses.

---

# 11. Accuracy

Accuracy should be derived from:

```text
ShotsHit / ShotsFired
```

Display as a percentage.

Example:

```text
34.2%
```

If:

```text
ShotsFired = 0
```

display:

```text
0.0%
```

rather than:

```text
NaN
```

---

# 12. Bullseye Statistics Section

Because the moving Bullseye is the game's defining mechanic, the Profile screen should have a dedicated Bullseye section.

Display available statistics such as:

```text
Bullseye Hits

Bullseye Eliminations

Attached Bullseye Eliminations

Detached Bullseye Eliminations

Grenade Bullseye Detachments

Magnetism Grenade Detachments
```

Use only statistics currently supported by the implemented telemetry/profile system.

Do not invent fake values or duplicate counters.

---

# 13. Body / Hit Statistics

If the profile currently tracks these values, include:

```text
Head Hits
Body Hits
Bullseye Hits
```

Potential presentation:

```text
HIT LOCATION

Bullseye Hits         1,083
Head Hits               621
Body Hits             3,904
```

This section may later support percentages.

For example:

```text
Bullseye Hit %
Head Hit %
Body Hit %
```

However, derived percentages are optional for this ticket.

---

# 14. Weapon Statistics Section

Create a dedicated weapon-statistics view.

The UI must populate weapons dynamically using the persistent weapon-stat collection from REQ-062.

Do **not** hard-code:

```text
Pistol UI
Rifle UI
Shotgun UI
DMR UI
Sniper UI
Bazooka UI
```

Instead, build the interface from the existing weapon data.

Conceptually:

```csharp
foreach (WeaponLifetimeStats weapon in profile.WeaponStats)
{
    CreateWeaponStatEntry(weapon);
}
```

---

# 15. Weapon Entry

Each weapon should have a player-facing stat entry.

At minimum:

```text
Weapon Name
Eliminations
Shots Fired
Shots Hit
Accuracy
Damage Dealt
Longest Elimination Distance
```

If applicable:

```text
Head Hits
Body Hits
Bullseye Hits
```

Example:

```text
DMR

Eliminations            312
Accuracy               41.3%
Damage Dealt          18,422
Bullseye Hits            148
Longest Elimination     78.2m
```

---

# 16. Stable Weapon IDs vs Display Names

REQ-062 should use stable internal weapon IDs.

REQ-063 should present human-readable weapon names.

Example:

```text
Internal ID:
weapon_dmr

Display:
DMR
```

Do not expose internal identifiers such as:

```text
weapon_rifle_01
```

to normal players.

Use the existing weapon definition's display name wherever possible.

---

# 17. Weapon Sorting

Default weapon-stat sorting should preferably prioritize:

```text
Eliminations
```

descending.

Example:

```text
DMR        312
Rifle      287
Shotgun    211
Pistol     153
Sniper      96
Bazooka     34
```

This naturally places the player's most-used/successful weapons near the top.

If there is no stat data for a weapon, Cursor may either:

```text
hide the weapon
```

or:

```text
display it with zeros
```

Use whichever produces the cleaner UI.

---

# 18. Favorite Weapon

Display:

```text
Favorite Weapon
```

Prefer to derive this rather than store it separately.

For this ticket, define `Favorite Weapon` as:

```text
weapon with the highest lifetime eliminations
```

Example:

```text
Favorite Weapon: DMR
```

If there are no weapon eliminations yet:

```text
Favorite Weapon: —
```

Do not add a separate persisted `FavoriteWeapon` field unless there is a strong technical reason.

---

# 19. Career Records Section

Add a small section for notable lifetime records.

At minimum, if supported:

```text
Longest Elimination Distance
```

Potential future values may include:

```text
Most Eliminations in a Match
Longest Bullseye Elimination
Longest Ricochet Elimination
Most Assists in a Match
Highest Accuracy Match
```

Do not implement statistics that are not yet tracked.

Build the UI section so records can easily be added later.

---

# 20. Longest Elimination

Display the player's lifetime:

```text
Longest Elimination Distance
```

Format in meters.

Example:

```text
91.4 m
```

Use consistent distance formatting throughout the game.

---

# 21. Special-Elimination Statistics

If these values exist after REQ-055/061 integration, provide an area for special elimination types.

Examples:

```text
Body Slam Eliminations
Ricochet Eliminations
Detached Bullseye Eliminations
```

Potential presentation:

```text
SPECIAL ELIMINATIONS

Body Slam                14
Ricochet                  9
Detached Bullseye       125
```

Do not clutter the primary combat overview with these values.

---

# 22. Play Time

If `TotalPlayTimeSeconds` is available through REQ-062, display a human-friendly lifetime playtime.

Examples:

```text
47m
```

```text
3h 18m
```

```text
126h 42m
```

Do not display raw seconds.

---

# 23. Recommended Screen Structure

The first implementation could use categories/tabs such as:

```text
OVERVIEW
WEAPONS
BULLSEYE
```

Example:

```text
------------------------------------------------
PROFILE                 BullseyePlayer
------------------------------------------------

[ OVERVIEW ] [ WEAPONS ] [ BULLSEYE ]

OVERVIEW

Matches                         84
Wins                            31
Eliminations                 1,204
Deaths                         932
Assists                        287
K/D                           1.29
Accuracy                     34.2%

Favorite Weapon                DMR
Longest Elimination          91.4m
Play Time                  14h 22m
```

This is preferred over placing every statistic on a single enormous page.

---

# 24. Controller Navigation

The Profile screen must be fully navigable using a controller.

The game is being designed primarily with controller support in mind.

Support:

```text
Left Stick / D-Pad
    Navigate

A
    Select

B
    Back

LB / RB
    Switch categories/tabs
```

Exact inputs may follow the project's current menu-navigation conventions.

Do not introduce conflicting controller behavior.

---

# 25. Keyboard / Mouse Navigation

Support equivalent mouse/keyboard interaction.

At minimum:

```text
Mouse
    click buttons/tabs

Keyboard
    existing UI navigation controls

Escape
    back/close
```

Reuse the project's existing input system rather than creating a separate input stack.

---

# 26. Selected-State Visibility

Players should clearly understand which Profile category is selected.

For example:

```text
> OVERVIEW
  WEAPONS
  BULLSEYE
```

or use the project's existing visual button-selection treatment.

Controller focus must remain visible.

---

# 27. Responsive Stat Formatting

Create reusable formatting helpers for profile statistics.

Examples:

```text
1204 -> 1,204

0.34157 -> 34.2%

1.28791 -> 1.29

91.4283 -> 91.4 m

15432 seconds -> 4h 17m
```

Avoid duplicating formatting logic across many individual text elements.

---

# 28. Empty Profile State

The Profile screen must look reasonable for a brand-new player.

Example:

```text
Matches                   0
Wins                      0
Eliminations              0
Deaths                    0
Assists                   0
K/D                    0.00
Accuracy                0.0%

Favorite Weapon             —
Longest Elimination         —
```

Do not display:

```text
null
NaN
Infinity
-1
```

to the player.

---

# 29. Dynamic Refresh

When the Profile screen opens, it should retrieve the latest loaded profile data.

Suggested behavior:

```text
OpenProfileScreen()
    ↓
RefreshProfileUI()
```

If profile values change during development without restarting the application, reopening or refreshing the page should display the latest values.

---

# 30. Do Not Modify Stats From UI

REQ-063 should be primarily read-only.

The Profile screen may modify:

```text
DisplayName
```

but should not allow players to manually edit:

```text
Eliminations
Deaths
Wins
Weapon stats
Bullseye stats
```

Development/debug tools from REQ-062 may still modify/reset data separately.

---

# 31. Profile Save After Display Name Change

When the player changes their display name:

```text
Update profile
      ↓
Save profile
      ↓
Refresh UI
```

The changed name should persist after restarting the game.

---

# 32. No Profile Deletion Button

Do not expose the REQ-062 debug reset functionality through the normal Profile UI.

Players should not accidentally erase their lifetime statistics.

A future Settings or account-management system can determine whether profile reset should ever become player-facing.

---

# 33. Visual Style

Follow the visual language of the existing Bullseye menus/HUD.

The screen should feel like part of the same game rather than a generic Unity debug panel.

Prioritize:

```text
clear hierarchy
large readable values
simple stat categories
controller readability
minimal clutter
```

Avoid overly small text.

The UI should remain readable from typical console viewing distance.

---

# 34. Reusable Stat Row

Prefer creating reusable UI components.

Conceptual prefab:

```text
ProfileStatRow
```

Structure:

```text
Label              Value
```

Example:

```text
Eliminations       1,204
```

This should allow new statistics to be added without redesigning the entire page.

---

# 35. Reusable Weapon Entry

Create a reusable UI component/prefab for weapon statistics.

Conceptual structure:

```text
WeaponProfileEntry

WeaponName
Eliminations
Accuracy
BullseyeHits
LongestElimination
```

Weapon entries should be instantiated from profile data.

---

# 36. No Weapon Images Required Yet

Do not require icons or rendered weapon images for REQ-063.

Text-based weapon entries are sufficient.

The UI architecture should permit weapon artwork/icons later.

---

# 37. No Player Character Preview Required Yet

Do not create a rotating 3D player preview or character customization panel in this ticket.

That can become part of a future player-customization/profile update.

---

# 38. Multiplayer Name Integration

Where practical, the player's persistent display name should become available to the multiplayer identity system.

However, do not undertake a major networking rewrite solely for REQ-063.

If current multiplayer players are still labeled generically such as:

```text
Player 1
Player 2
```

REQ-063 should prepare the display name so a future requirement can display:

```text
BullseyePlayer
```

during matches.

The profile screen itself is the priority.

---

# 39. Separation From Scoreboard

REQ-056 created an in-match scoreboard.

Keep the conceptual distinction:

```text
SCOREBOARD
    current match

PROFILE
    lifetime career
```

Do not have the Profile screen read directly from the active scoreboard.

Both may ultimately consume related statistical systems, but they represent different scopes.

---

# 40. Research Data Separation

Do not expose:

```text
ResearchParticipantId
Research consent status
Raw aiming samples
Telemetry event IDs
Research session IDs
```

on the normal player Profile screen.

REQ-063 is a game-facing career screen.

Research infrastructure should remain separate.

---

# 41. No Ranked Statistics Yet

Do not display or create:

```text
Rank
MMR
ELO
Competitive tier
Skill rating
Global percentile
```

No ranking system currently exists.

Do not infer competitive skill from lifetime statistics.

---

# 42. No Leaderboards Yet

Do not implement:

```text
global leaderboards
Steam leaderboards
friends leaderboards
regional rankings
```

REQ-063 only presents the local player's persisted career.

---

# 43. No Achievements Yet

Do not create an achievements interface.

Lifetime values may eventually feed achievement requirements, but achievement presentation should remain a separate ticket.

---

# 44. No Progression Yet

Do not create:

```text
Level
XP
Prestige
Battle Pass
Unlock Progress
```

unless those systems are separately designed later.

Profile and progression should not be treated as synonymous.

---

# 45. Potential Future Architecture

REQ-063 should leave room for the Profile screen to eventually grow into something like:

```text
PROFILE

Overview
Weapons
Bullseye
Game Modes
Achievements
Progression
Match History
Customization
```

Only implement:

```text
Overview
Weapons
Bullseye
```

or the closest appropriate first version now.

---

# 46. Testing Requirements

## Test A — Brand-New Profile

Use a fresh profile.

Open Profile.

Expected:

```text
DisplayName appears.
All statistics display as zero or —.
No errors.
No NaN.
No Infinity.
```

---

## Test B — Existing Lifetime Stats

Populate test lifetime values such as:

```text
Matches = 20
Wins = 8

Eliminations = 210
Deaths = 150
Assists = 67

ShotsFired = 2,000
ShotsHit = 700
```

Expected:

```text
K/D = 1.40
Accuracy = 35.0%
Win Rate = 40.0%
```

---

## Test C — Persistence

Change display name.

Example:

```text
BullseyePlayer
```

Close game.

Restart.

Expected:

```text
BullseyePlayer
```

still appears.

---

## Test D — Weapon Population

Create stats for multiple weapons.

Example:

```text
DMR      50 eliminations
Rifle    40 eliminations
Pistol   10 eliminations
```

Expected:

Weapon entries are generated automatically.

Default order:

```text
DMR
Rifle
Pistol
```

---

## Test E — Favorite Weapon

Using the previous example:

Expected:

```text
Favorite Weapon: DMR
```

---

## Test F — Zero Deaths

Profile:

```text
Eliminations = 5
Deaths = 0
```

Expected:

K/D displays a sensible numeric value.

No divide-by-zero errors.

---

## Test G — Zero Shots

Profile:

```text
ShotsFired = 0
ShotsHit = 0
```

Expected:

```text
Accuracy = 0.0%
```

---

## Test H — Controller Navigation

Using controller:

- open Profile,
- change tabs,
- scroll weapon entries,
- edit display name if practical through current UI,
- return to main menu.

Expected:

All primary Profile functions can be used without a mouse.

---

## Test I — Large Statistics

Test:

```text
Eliminations = 123456
```

Expected:

```text
123,456
```

without breaking UI layout.

---

# 47. Acceptance Criteria

REQ-063 is complete when:

- [ ] A Profile option is available from the main menu.
- [ ] Opening Profile shows persistent data from REQ-062.
- [ ] The player's display name is shown.
- [ ] The display name can be changed and persists after restart.
- [ ] Matches, wins, losses, eliminations, deaths, and assists can be displayed.
- [ ] K/D is displayed correctly.
- [ ] Accuracy is displayed correctly.
- [ ] Win rate is displayed correctly.
- [ ] Zero-value profiles do not produce NaN/Infinity/errors.
- [ ] Relevant Bullseye-specific lifetime statistics are displayed.
- [ ] Weapon statistics populate dynamically from profile data.
- [ ] Weapon entries are not individually hard-coded.
- [ ] Weapon display names are player-friendly rather than internal IDs.
- [ ] Favorite Weapon is derived from weapon statistics.
- [ ] Longest elimination distance is displayed if available.
- [ ] Lifetime playtime is formatted appropriately if available.
- [ ] UI refreshes from the current loaded profile when opened.
- [ ] Controller navigation works.
- [ ] Keyboard/mouse navigation works.
- [ ] Profile UI does not directly modify combat statistics.
- [ ] The screen remains separate from the REQ-056 match scoreboard.
- [ ] No research identifiers or raw research telemetry are exposed.
- [ ] No ranked system, progression system, or achievements system is introduced.
- [ ] No Steam integration is required for the Profile screen to function.
- [ ] The architecture allows additional Profile categories to be added later.

---

# 48. Explicit Non-Goals

Do NOT implement the following in REQ-063:

```text
Steam integration
Steam profile picture
Steam Cloud
Steam achievements
Global leaderboards
Friends leaderboards
Ranked MMR
Competitive tiers
XP
Player levels
Prestige
Battle pass
Cosmetic progression
Character customization
3D player preview
Match history
Research consent
Research dashboards
Account login
Email/password registration
Cloud profiles
```

These should remain future requirements.

---

# 49. Future Extensions

REQ-063 should make future Profile expansions relatively easy.

Potential later tickets may add:

```text
REQ-0XX — Match History

REQ-0XX — Player Progression / XP

REQ-0XX — Achievements

REQ-0XX — Steam Identity Integration

REQ-0XX — Steam Cloud Profiles

REQ-0XX — Game Mode Career Statistics

REQ-0XX — Player Customization

REQ-0XX — Public Player Cards

REQ-0XX — Lifetime Records and Medals
```

Do not implement these systems preemptively.

---

# 50. Design Philosophy

REQ-062 answers:

> "Who is this player, and what persistent statistics belong to them?"

REQ-063 should answer:

> "How can the player actually see and understand their career?"

The Profile screen should make accumulated play feel tangible without prematurely turning Bullseye into a progression-heavy game.

Keep the first implementation clean and useful.

The primary objectives are:

```text
Persistent identity
        ↓
Meaningful lifetime statistics
        ↓
Clear player-facing presentation
```

This screen can become much richer later, but REQ-063 should establish a strong and extensible foundation for the player's visible career in Bullseye.