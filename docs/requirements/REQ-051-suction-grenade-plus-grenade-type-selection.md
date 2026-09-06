# REQ-051 — Suction Grenade + Grenade Type Selection

## Summary

Add a second grenade type called the **Suction Grenade**.

Unlike the existing grenade, which uses an explosive effect to knock/dislodge bullseyes from players, the Suction Grenade creates a temporary **suction field** around itself.

For approximately **6 seconds**, any eligible player who enters that field should have their attached bullseye pulled off their body.

The detached bullseye should then be physically attracted toward the Suction Grenade, causing it to follow the grenade's movement/trajectory.

This means the grenade can affect players:

- While flying through the air
- While bouncing
- While rolling
- After landing
- While sitting stationary on the ground

For example, a player who walks too close to a Suction Grenade lying on the ground should have their bullseye sucked off and pulled toward the grenade.

Players must also be able to switch between the existing grenade and the new Suction Grenade.

For now, there are only two grenade types.

---

# High-Level Gameplay Concept

The existing grenade behaves roughly like:

```text
THROW
  ↓
GRENADE EXPLODES
  ↓
Nearby bullseyes are blasted/dislodged
```

The new grenade should behave more like:

```text
THROW SUCTION GRENADE
        ↓
Suction field activates
        ↓
Grenade flies through world
        ↓
   [ SUCTION RADIUS ]
        ↓
Touches / overlaps player
        ↓
Player's bullseye detaches
        ↓
Bullseye is pulled toward grenade
        ↓
Grenade continues flying/bouncing
        ↓
Bullseye follows its movement
        ↓
Grenade lands
        ↓
Field remains active
        ↓
Another player walks too close
        ↓
Their bullseye is sucked off too
```

The mechanic should feel substantially different from an ordinary explosive grenade.

---

# 1. Grenade Types

The game should now support at least two grenade types:

```text
1. Existing Grenade
2. Suction Grenade
```

Do NOT replace the existing grenade.

The existing grenade behavior should continue working as it currently does.

The grenade system should be structured so additional grenade types can reasonably be added later.

Avoid implementing grenade selection as something hard-coded specifically to exactly two booleans if a simple extensible grenade-type structure already exists or can be introduced cleanly.

For example:

```csharp
GrenadeType
{
    Standard,
    Suction
}
```

The exact architecture should conform to the existing weapon/grenade system.

---

# 2. Working Name

The new grenade may currently be called:

```text
Suction Grenade
```

This is a working gameplay/development name.

Do not spend time building a naming or item-description system for this requirement.

---

# 3. Suction Grenade Activation

The Suction Grenade should begin functioning **immediately after it is legitimately thrown**.

It should NOT need to wait until it lands.

This is important.

A Suction Grenade flying past another player should be capable of removing that player's bullseye.

Example:

```text
Player A
   \
    \ throws
     \
      O --------------------->

                Player B
                  |
                  |

Grenade passes close enough to Player B.

Player B's bullseye is pulled off.

The grenade continues moving.

The bullseye follows the grenade.
```

---

# 4. Suction Duration

The suction effect should remain active for approximately:

```text
6 seconds
```

Make this configurable.

Example:

```csharp
[SerializeField]
private float suctionDuration = 6f;
```

Do not permanently hard-code gameplay values if they can easily be exposed for tuning.

The timer should begin when the grenade becomes active after being thrown.

---

# 5. Suction Radius

The Suction Grenade should have a configurable spherical influence radius.

Example:

```csharp
[SerializeField]
private float suctionRadius;
```

Any eligible player who intersects this radius while the grenade is active can have their bullseye removed.

Conceptually:

```text
             ___________
          .-'           '-.
        .'                 '.
       /                     \
      |        GRENADE        |
      |           O           |
       \                     /
        '.                 .'
          '-.___________.'

          SUCTION RADIUS
```

The correct radius should be determined through gameplay testing.

Do not bake an arbitrary permanent radius into code.

---

# 6. Field Touching a Player

The grenade itself does NOT need to physically collide with the player.

The **suction field** needs to touch/overlap the player.

For example:

```text
       GRENADE
          O
      ___________
    /             \
   /    suction    \
  |      field      | PLAYER
   \               /    |
    \_____________/     |
```

If the player's valid gameplay collider enters the suction radius:

```text
Player is considered affected.
```

Cursor should inspect the existing Player prefab and determine the appropriate player collider/root object to use.

Avoid requiring the field to specifically hit the tiny bullseye collider.

The field is meant to detect the **player**, then operate on that player's bullseye.

---

# 7. Detach the Player's Bullseye

When an eligible player enters the active suction radius:

```text
Attached Bullseye
      ↓
Detach using existing bullseye-displacement system
      ↓
Bullseye becomes subject to suction
```

IMPORTANT:

Do NOT create a separate parallel version of bullseye detachment.

The game already has grenade-related bullseye dislodgement behavior.

Cursor should inspect and reuse the existing systems responsible for:

- Bullseye ownership
- Bullseye detachment
- Detached state
- Return/reconnection behavior
- Networking
- Any immunity/timers associated with bullseye detachment

The Suction Grenade should trigger the same underlying legitimate detached-bullseye state wherever practical.

---

# 8. Do Not Break the Bullseye

The Suction Grenade should NOT destroy the bullseye.

It should:

```text
PULL IT OFF
```

not:

```text
BREAK IT
```

Therefore:

```text
Suction Grenade hit
    ≠
Player death
```

Any existing gameplay consequences of having a bullseye detached should remain unchanged.

REQ-051 should not redesign what happens to a player while their bullseye is detached.

---

# 9. Bullseye Suction / Attraction

Once detached by the Suction Grenade, the bullseye should move toward the grenade.

It should NOT simply disappear.

It should visibly travel with or toward the grenade.

The intended behavior is similar to a magnetic attraction:

```text
Bullseye          Grenade
   O                 O
    \               /
     \------------->
```

The attraction should be strong enough that the relationship between the bullseye and the grenade is obvious to players.

However, avoid simply teleporting the bullseye directly to the grenade every frame if a more natural physics-based or smoothly interpolated solution is appropriate.

---

# 10. Follow the Grenade's Trajectory

This is a core requirement.

If the grenade is still moving when it removes a bullseye, the bullseye should be pulled along with the grenade's trajectory.

Example:

```text
THROWN GRENADE
      O --------------------->


PLAYER
  |
  O  <- bullseye detaches


Then:

                 O Grenade ----->

              O
           Bullseye
             ------>
```

The effect should create the visual impression that the grenade has **ripped the bullseye off the player and is carrying/pulling it away**.

The bullseye does not necessarily need to be perfectly locked to the grenade's Transform.

A slight trailing effect may actually look better.

Cursor should choose a stable implementation compatible with the existing bullseye/network physics.

---

# 11. Grenade Bouncing and Rolling

The suction field should remain attached to the grenade while it:

- Flies
- Collides with surfaces
- Bounces
- Rolls
- Slides
- Comes to rest

The suction center should always follow the physical grenade.

For example:

```text
Thrown
  ↓
Flying + sucking
  ↓
Hits wall
  ↓
Bounces + sucking
  ↓
Hits floor
  ↓
Rolls + sucking
  ↓
Stops
  ↓
Stationary + still sucking
  ↓
6-second lifetime expires
```

---

# 12. Stationary Suction Grenade

A major part of this mechanic is that the grenade remains dangerous after landing.

Example:

```text
        PLAYER
           ↓

        walking

           |
           |
           v

    _________________
   /                 \
  |   SUCTION FIELD   |
  |         O         |
   \_________________/

         grenade
```

If the player walks into the radius while the suction field is still active:

```text
Bullseye detaches
```

and is pulled toward the stationary grenade.

This allows the grenade to temporarily function as a small area-denial device.

---

# 13. Multiple Players

A single Suction Grenade should be capable of affecting multiple eligible players during its active lifetime.

Example:

```text
 Player A          Player B
    |                 |
    |                 |

       \           /
        \         /
      [ SUCTION ]
           O
```

If both players enter the field:

```text
Player A bullseye → pulled
Player B bullseye → pulled
```

This can create entertaining situations where several detached bullseyes are being attracted toward the same grenade.

---

# 14. One Extraction Per Player Per Grenade

A single Suction Grenade should not repeatedly remove the same player's bullseye due to trigger callbacks or bullseye-return timing.

Track players already successfully affected by that grenade.

Conceptually:

```csharp
HashSet<ulong> affectedPlayers;
```

or the project's appropriate equivalent.

Once Player A has been successfully affected by Suction Grenade X:

```text
Suction Grenade X should not detach Player A's bullseye again.
```

A different Suction Grenade may affect that player normally.

This prevents:

```text
Detach
→ Return
→ Immediately detach again
→ Return
→ Immediately detach again
```

from one grenade.

---

# 15. Already-Detached Bullseyes

If a bullseye is already detached and enters the active suction field, the grenade should be allowed to attract that detached bullseye toward itself if doing so is compatible with the existing bullseye system.

Example:

```text
Bullseye already lying on ground

           O


                  SUCTION GRENADE
                       O
              <--------

Bullseye begins moving toward grenade.
```

However:

- Do not duplicate ownership
- Do not reset return timers unnecessarily
- Do not create multiple authoritative controllers fighting over the bullseye
- Do not destabilize the existing bullseye-return system

If multiple Suction Grenades overlap, there must be a deterministic/stable way of deciding which grenade influences an already-detached bullseye.

Prefer the closest active grenade or another simple deterministic approach if needed.

Do not create a complex physics simulation solely for this edge case.

---

# 16. Bullseye Return Behavior

Do not create a new bullseye return system for REQ-051.

Use the existing detached-bullseye return behavior.

When the Suction Grenade's field expires:

```text
The grenade stops attracting bullseyes.
```

That does NOT necessarily mean every affected bullseye instantly teleports back.

The normal existing bullseye return/recovery behavior should continue.

---

# 17. Suction Force Configuration

Expose useful tuning values.

Potential settings include:

```csharp
suctionDuration
suctionRadius
suctionForce
maximumSuctionSpeed
```

Only include parameters actually useful for the chosen implementation.

The important concept is that designers should be able to tune:

- How far the grenade reaches
- How strongly it pulls
- How long it functions

without rewriting code.

---

# 18. Grenade Lifetime

After approximately six seconds of suction behavior, the Suction Grenade should deactivate.

At that point:

```text
Suction Field = OFF
```

The grenade can then be removed/despawned according to the existing grenade architecture.

It should NOT explode like the existing grenade unless some generic existing cleanup VFX requires adjustment.

The Suction Grenade should have a distinct lifecycle.

Example:

```text
Throw
↓
6 seconds of suction
↓
Deactivate
↓
Despawn
```

---

# 19. Owner Interaction

The Suction Grenade should follow the project's existing grenade rules regarding whether the thrower can ultimately affect themselves.

Because the grenade spawns very close to the throwing player, it must NOT immediately rip the thrower's bullseye off merely because it originated near their hand/body.

Use the existing grenade owner-ignore/arming behavior if one exists.

If needed, add a very short owner grace period before the thrower becomes eligible.

After that grace period, the grenade should be dangerous to its owner as well unless the existing grenade system explicitly prevents self-effects.

This creates intuitive behavior:

```text
Throw grenade
↓
No immediate self-hit during release
↓
Grenade lands
↓
Thrower walks back into active field
↓
Thrower's own bullseye can be sucked off
```

---

# 20. Network Authority

Suction behavior must be multiplayer-authoritative.

A client should NOT independently decide:

```text
"I entered the suction radius, so detach my bullseye."
```

The server/host should authoritatively validate:

- Grenade exists
- Grenade is a Suction Grenade
- Grenade is active
- Suction timer has not expired
- Player is within valid radius
- Player is eligible
- Bullseye belongs to that player
- Player has not already been affected by this grenade

The authoritative system should then trigger the existing bullseye-detachment behavior.

Bullseye attraction/movement must also replicate correctly to all clients.

---

# 21. Grenade Selection

Players must now be able to select which grenade type they currently have equipped.

For REQ-051 there are only:

```text
Existing Grenade
Suction Grenade
```

The grenade throw button should throw whichever grenade type is currently selected.

Example:

```text
Selected Grenade = Existing
↓
Throw
↓
Existing grenade spawned
```

versus:

```text
Selected Grenade = Suction
↓
Throw
↓
Suction grenade spawned
```

---

# 22. Controller Input

Map:

```text
D-Pad Right
```

to:

```text
Switch Grenade
```

For now, because there are only two grenade types, pressing D-Pad Right should toggle:

```text
Existing
   ↓
Suction
   ↓
Existing
   ↓
Suction
```

Design the underlying system so that this can become normal cycling if additional grenade types are introduced later.

---

# 23. Keyboard + Mouse Input

Map:

```text
N
```

to:

```text
Switch to Next Grenade
```

This follows the general Halo Infinite keyboard-control approach, where the game exposes a dedicated **Switch to Next Grenade** action and has used `N` as its default next-grenade binding.

For Bullseye:

```text
N
```

should cycle between:

```text
Existing Grenade
↕
Suction Grenade
```

Do NOT change the existing grenade throw key.

The existing keyboard/mouse grenade throw input should continue functioning exactly as it currently does.

---

# 24. Existing Grenade Throw Controls

REQ-051 must NOT undo the grenade-control work from previous requirements.

The existing throw action should remain the common:

```text
Throw Equipped Grenade
```

input.

In other words:

```text
Grenade Selection Input
        ↓
Changes equipped grenade type

Throw Grenade Input
        ↓
Throws currently equipped grenade
```

Selection and throwing must be separate actions.

---

# 25. Input System

Use the project's existing Unity input architecture.

Do NOT create an unrelated second input system.

Add an action similar to:

```text
SwitchGrenade
```

or:

```text
NextGrenade
```

using the project's existing naming conventions.

Bindings:

```text
Gamepad:
D-Pad Right

Keyboard:
N
```

---

# 26. HUD Grenade Indicator

The player needs a simple way to know which grenade is currently equipped.

Add or update the HUD so it can distinguish:

```text
Existing Grenade
```

from:

```text
Suction Grenade
```

This does not require polished final artwork.

For now, an appropriate temporary:

- Icon
- Text label
- Different placeholder sprite
- Highlight

is acceptable.

For example:

```text
GRENADE
[Suction]
```

The important requirement is that the player can clearly tell which grenade will be thrown before pressing the throw button.

---

# 27. Grenade Inventory / Counts

Do not perform a major grenade inventory redesign as part of this requirement unless necessary.

If the project already tracks grenade ammunition/counts, adapt that architecture so grenade counts can be associated with grenade type.

If grenades are currently unlimited during development, preserve that behavior unless the existing system dictates otherwise.

REQ-051 is primarily about:

1. A second grenade behavior
2. Grenade selection

not final grenade economy.

---

# 28. Suction Grenade Prefab

Create a separate prefab or appropriate variant for the Suction Grenade.

Example:

```text
Grenade
└── Existing Grenade

SuctionGrenade
└── Suction behavior
```

or another architecture that appropriately shares common grenade functionality.

Avoid duplicating all generic grenade code.

Shared behavior should include things such as:

- Throwing
- Network spawning
- Rigidbody physics
- Collision
- Ownership
- Despawning

The Suction Grenade should add its unique field behavior on top of the reusable grenade foundation.

---

# 29. Visual Identification

Players must eventually be able to distinguish a Suction Grenade from the existing grenade.

For REQ-051, polished art is NOT required.

A temporary distinction is acceptable, such as:

- Different material
- Different emissive appearance
- Placeholder effect
- Temporary icon
- Simple particle effect

Do not spend significant development time creating final grenade artwork.

---

# 30. Suction Field Visualization

Add a simple temporary visual cue indicating that the Suction Grenade is active.

This could eventually become something like:

```text
swirling particles
distortion
air streaks
energy field
objects visually pulling inward
```

but polished visual effects are outside the core scope of this requirement.

The initial implementation only needs enough feedback that testers can understand:

```text
"This grenade is currently sucking things toward it."
```

---

# 31. Debug Visualization

Because the exact suction radius will require tuning, add editor/development visualization.

Example:

```csharp
[SerializeField]
private bool showSuctionDebug;
```

When enabled, show:

- Suction radius
- Grenade center
- Currently detected players if practical
- Active/inactive state

Unity Gizmos are appropriate.

Example:

```text
        ___________
      /             \
     /               \
    |        O        |
     \               /
      \_____________/

      Debug Radius
```

Debug visuals must not appear in production gameplay.

---

# 32. Interaction With Existing Grenade

The existing grenade must remain fully functional.

Verify that adding grenade types does NOT break:

- Throwing
- Grenade physics
- Bullseye dislodgement
- Network spawning
- Grenade ownership
- Input handling
- Existing grenade effects

The existing grenade's behavior should not be rewritten unnecessarily.

---

# 33. Architecture Requirement

Before implementing REQ-051, Cursor should inspect the existing project and identify:

- Current grenade prefab
- Grenade throw logic
- Grenade networking
- Existing grenade bullseye-displacement logic
- Bullseye ownership
- Bullseye attachment/detachment state
- Bullseye return behavior
- Player collider configuration
- Input actions
- HUD grenade UI, if any

Reuse these systems.

Do NOT create parallel systems merely because they are easier for the LLM to generate.

A preferred conceptual architecture is:

```text
Grenade Controller
        |
        +---- Common throw/network/physics behavior
        |
        +---- Existing Grenade Behavior
        |
        +---- Suction Grenade Behavior
```

with grenade selection handled separately:

```text
Player Grenade Inventory/Selection
        ↓
Selected Grenade Type
        ↓
Throw Input
        ↓
Spawn Correct Grenade
```

Adapt this model to the project's existing architecture rather than blindly creating these exact classes.

---

# 34. Performance

Do not perform unnecessarily expensive global player searches every frame.

Because this is a small-radius physics effect, use an appropriate efficient detection approach such as:

- Trigger collider
- Physics overlap query at a reasonable cadence
- Existing networked collision architecture

Do not repeatedly call broad scene-wide operations such as:

```csharp
FindObjectsOfType<Player>()
```

every frame.

---

# 35. Edge Cases

Handle at least the following cases safely.

## Player enters field while grenade is flying

```text
Bullseye detaches
Bullseye follows grenade
```

## Player enters field while grenade is stationary

```text
Bullseye detaches
Bullseye moves toward grenade
```

## Player leaves field after bullseye has detached

Do not immediately reattach solely because they left the radius.

Use normal existing bullseye behavior.

## Grenade expires

```text
Suction stops
Grenade cleans up
```

## Same player remains inside radius

Do not repeatedly trigger detachment events.

## Multiple players enter radius

Each may be affected.

## Dead player enters radius

Do not trigger inappropriate bullseye logic.

## Grenade owner walks into field after throwing

Follow normal grenade self-effect rules after any required throw grace period.

## Already-detached bullseye enters radius

Attract it if compatible with the existing detached-bullseye architecture.

## Bullseye returns while grenade is still active

The same grenade should not repeatedly detach the same player's bullseye.

---

# Acceptance Criteria

## AC-1 — Second Grenade Type Exists

The game contains:

```text
Existing Grenade
Suction Grenade
```

and both can be thrown successfully.

---

## AC-2 — Controller Selection

Pressing:

```text
D-Pad Right
```

changes the equipped grenade type.

With two grenade types:

```text
Existing → Suction → Existing → Suction
```

---

## AC-3 — Keyboard Selection

Pressing:

```text
N
```

changes to the next grenade type.

---

## AC-4 — Throw Selected Grenade

After selecting the Suction Grenade and pressing the existing throw button:

```text
Suction Grenade is spawned/thrown.
```

After selecting the existing grenade:

```text
Existing grenade is spawned/thrown.
```

---

## AC-5 — Active While Airborne

A Suction Grenade flying close enough to a player can detach that player's bullseye before the grenade ever touches the ground.

---

## AC-6 — Bullseye Follows Grenade

If the bullseye is removed while the grenade is moving:

```text
The detached bullseye is pulled in the direction of the grenade and visibly follows its trajectory.
```

---

## AC-7 — Ground Trap

If the Suction Grenade lands while its field is still active and a player subsequently enters its radius:

```text
That player's bullseye is detached.
```

---

## AC-8 — Six-Second Duration

The suction field remains active for approximately:

```text
6 seconds
```

using a configurable duration.

---

## AC-9 — Effect Expires

After the suction lifetime ends:

```text
Players entering the former radius are no longer affected.
```

---

## AC-10 — Multiple Players

A single active Suction Grenade can remove bullseyes from multiple eligible players who enter its radius.

---

## AC-11 — No Duplicate Detachment

The same grenade does not repeatedly detach the same player's bullseye.

---

## AC-12 — Bullseye Is Not Destroyed

The Suction Grenade:

```text
detaches the bullseye
```

but does NOT:

```text
break the bullseye
```

or automatically kill the player.

---

## AC-13 — Existing Return Logic

Bullseyes removed by the Suction Grenade continue using the game's existing legitimate detached/return behavior.

---

## AC-14 — Existing Grenade Preserved

The original grenade continues functioning as it did before REQ-051.

---

## AC-15 — Multiplayer

When Client A throws a Suction Grenade near Client B:

All connected clients correctly observe:

- The Suction Grenade
- Its movement
- Client B's bullseye detaching
- The bullseye moving toward/following the grenade
- The bullseye's subsequent existing return behavior

without duplicate events or desynchronization.

---

## AC-16 — HUD Selection Feedback

The local player can clearly determine whether:

```text
Existing Grenade
```

or:

```text
Suction Grenade
```

is currently selected.

---

# Testing Scenarios

When testing becomes possible, explicitly test:

1. Switch Existing → Suction with D-Pad Right.
2. Switch Suction → Existing with D-Pad Right.
3. Switch types using `N` on keyboard.
4. Throw existing grenade after switching.
5. Throw Suction Grenade after switching.
6. Suction Grenade flies directly past a player.
7. Suction Grenade barely misses suction radius.
8. Suction Grenade flies past two players.
9. Bullseye follows rapidly moving grenade.
10. Bullseye follows bouncing grenade.
11. Bullseye follows rolling grenade.
12. Grenade comes completely to rest.
13. Player walks into radius of stationary grenade.
14. Player remains inside radius for multiple seconds.
15. Same player's bullseye returns before grenade expires.
16. Verify same grenade does not repeatedly detach that player.
17. Multiple players enter stationary field.
18. Grenade expires while player is nearby.
19. Player enters radius after expiration.
20. Thrower walks into their own grenade after initial throw.
21. Already-detached bullseye enters suction radius.
22. Client throws at Host.
23. Host throws at Client.
24. Client throws at another Client.
25. Existing grenade still behaves correctly afterward.

---

# Out of Scope

Do NOT include the following in REQ-051:

- Final grenade artwork
- Final suction VFX
- Final suction audio
- Large grenade inventory redesign
- Grenade pickups unless already necessary
- Additional grenade types
- Damage caused directly by suction
- Player-body suction
- Pulling the entire player toward the grenade
- Weapon suction
- Physics interactions with every loose object
- Destroying bullseyes
- New bullseye return system
- Major HUD redesign

The suction mechanic is specifically focused on:

```text
PLAYER'S BULLSEYE
```

not pulling the entire player or environment toward the grenade.

---

# Desired Player Experience

The Suction Grenade should create situations such as:

```text
Player throws grenade across room.

             O -------------------------->

             (suction field)

                    PLAYER
                       |
                       ● Bullseye

                         ↓

                    Bullseye rips off

                         ● ---------->
                              O ------->

                         grenade carries it away
```

It should also create a temporary trap:

```text
Grenade lands behind cover.

             O
       [SUCTION FIELD]


Enemy rounds corner.

            PLAYER
               |
               ●

               ↓

       bullseye gets ripped away

            ● -----> O
```

The distinction between grenade types should therefore be immediately understandable:

```text
EXISTING GRENADE
"Blast bullseyes away."

SUCTION GRENADE
"Rip bullseyes toward the grenade."
```

---

# Definition of Done

REQ-051 is complete when the player can switch between the existing grenade and a Suction Grenade using **D-Pad Right on controller** or **N on keyboard**, throw the selected grenade using the existing grenade-throw input, and the Suction Grenade creates an approximately six-second network-authoritative radius that can detach eligible player bullseyes while the grenade is flying or stationary and visibly pull those bullseyes along with/toward the grenade without breaking the existing grenade, bullseye, movement, or multiplayer systems.