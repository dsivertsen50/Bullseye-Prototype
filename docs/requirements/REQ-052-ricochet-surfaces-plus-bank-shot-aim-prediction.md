# REQ-052 — Ricochet Surfaces + Bank-Shot Aim Prediction

## Summary

Add a configurable **ricochet surface system** that allows certain walls and other level geometry to reflect bullets.

Level designers should be able to make an otherwise normal surface capable of ricocheting bullets by adding a reusable Unity component to that object's collider.

Working component name:

```text
RicochetSurface
```

For example:

```text
Wall GameObject
├── Mesh Renderer
├── Collider
└── RicochetSurface
```

When a hitscan weapon fires into a surface containing this component, the bullet should reflect from that surface based on the incoming shot angle and continue along the reflected trajectory.

Additionally, while the player is aiming at a valid ricochet surface, the game should calculate the projected ricochet trajectory **before the player fires** and display a subtle indicator at the location where the reflected shot is predicted to hit.

This will allow skilled players to deliberately aim bullets off walls and potentially hit bullseyes that cannot be attacked directly.

Example:

```text
                     ENEMY
                       ●
                       |
                       |

PLAYER  ----------->  WALL
                         \
                          \
                           \
                            ●

                 Predicted ricochet impact
```

The mechanic should reward geometry awareness and difficult bank shots rather than automatically helping shots hit enemies.

---

# Goals

- Allow selected level surfaces to ricochet bullets.
- Make ricochet capability opt-in per surface.
- Allow designers to add ricochet behavior through a simple Unity component.
- Reflect shots using physically intuitive angle calculations.
- Show players where a ricocheted shot is expected to hit.
- Allow skilled players to intentionally shoot enemies from otherwise inaccessible angles.
- Integrate with the existing hitscan weapon and bullseye systems.
- Avoid duplicating the existing damage or shooting architecture.
- Keep the system extensible for additional ricochet rules later.

---

# 1. Ricochet Surface Component

Create a reusable component with a working name such as:

```csharp
RicochetSurface
```

A level designer should be able to add this component to any appropriate GameObject that has a collider.

Conceptually:

```text
Select Wall
    ↓
Add Component
    ↓
RicochetSurface
    ↓
Bullets can now ricochet from this collider
```

The presence of this component should determine whether bullets can reflect from that surface.

Do NOT require:

- Special tags
- Special object names
- Hard-coded wall references
- Specific prefabs

unless the existing project architecture already provides a cleaner equivalent.

The desired workflow is essentially:

> Drag/add the ricochet component onto a surface and that surface gains ricochet behavior.

---

# 2. Normal Surfaces Remain Unchanged

Surfaces without the ricochet component should behave exactly as they currently do.

Example:

```text
Normal Wall

Bullet
  ↓
Impact
  ↓
Existing bullet impact behavior
```

versus:

```text
Ricochet Wall

Bullet
  ↓
Impact
  ↓
Reflect
  ↓
Continue along new trajectory
```

REQ-052 must not cause all walls in the game to ricochet shots.

---

# 3. Hitscan Ricochet

The existing guns appear to use hitscan shooting.

Ricochet behavior should therefore be integrated with the existing hitscan raycast system rather than introducing physical bullet GameObjects solely for this feature.

Conceptually:

```text
Original Raycast
       ↓
Hits RicochetSurface
       ↓
Calculate reflection direction
       ↓
Launch second raycast
       ↓
Process second hit
```

The reflected direction should use the surface normal.

Conceptual calculation:

```csharp
Vector3 reflectedDirection =
    Vector3.Reflect(incomingDirection, hit.normal);
```

Cursor should use the appropriate equivalent within the existing shooting architecture.

---

# 4. One Ricochet Maximum Initially

For REQ-052, a bullet should support a maximum of:

```text
1 ricochet
```

Example:

```text
Player
  ↓
Ricochet Wall
  ↓
Second Surface
  ↓
Shot ends
```

Do NOT allow:

```text
Wall
→ Wall
→ Ceiling
→ Floor
→ Wall
→ Player
```

in this requirement.

The system should be architected cleanly enough that multiple ricochets could potentially be added later, but one bounce is sufficient for the first implementation.

Suggested configuration:

```csharp
maxRicochets = 1;
```

---

# 5. Reflection Angle

The outgoing bullet trajectory should be based on the actual incoming shot direction and the normal of the surface that was hit.

Conceptually:

```text
Incoming angle = outgoing angle
```

Example:

```text
          /
         / reflected shot
        /
-------●-------- wall
      /
     /
    / incoming shot
```

Do NOT use arbitrary predetermined ricochet directions.

The player should be able to change the reflected path by changing their aim.

---

# 6. Very Shallow / Invalid Angles

Cursor should prevent pathological ricochets.

Examples could include:

- A ray effectively parallel with the wall
- Immediate self-intersection with the same collider
- Extremely tiny reflected segments
- Numerical loops caused by starting the reflected ray inside the wall

After the first collision, offset the reflected ray origin slightly away from the surface.

Conceptually:

```csharp
ricochetOrigin =
    hit.point + hit.normal * smallOffset;
```

Use an appropriate small value rather than a large visible displacement.

---

# 7. Ricochet Distance

The reflected ray should have a finite range.

The total distance traveled should respect the weapon's existing effective/raycast range wherever practical.

Preferred behavior:

```text
Weapon maximum ray distance
      =
Initial segment
      +
Remaining ricochet segment
```

Example:

```text
Weapon range = 100 m

First hit after 30 m

Remaining ricochet range = approximately 70 m
```

Do not accidentally double a weapon's range merely because it bounced.

---

# 8. Weapon Compatibility

Cursor should inspect the current weapon system and determine which weapons use the shared hitscan implementation.

Ricochet behavior should preferably integrate with that common shooting path rather than being coded individually for every weapon.

However, the architecture should support eventually controlling whether a weapon is allowed to ricochet.

For example:

```csharp
bool canRicochet;
```

or an equivalent property in the weapon definition.

For REQ-052:

- Standard firearm hitscan bullets may ricochet where appropriate.
- Shotguns require special handling as described later.
- Grenades do NOT use this bullet ricochet system.

Do not change projectile/grenade behavior.

---

# 9. Ricochet Damage

For the first implementation, a ricocheted shot should retain the normal damage/bullseye interaction of the weapon.

Example:

```text
Direct rifle bullet → bullseye damage

Ricocheted rifle bullet → same bullseye damage
```

Do NOT automatically reduce damage solely because the shot ricocheted unless the existing weapon system requires such behavior.

Damage reduction can be considered as a future balance adjustment.

---

# 10. Bullseye Interaction

This is an important gameplay objective.

A ricocheted bullet must be capable of hitting an enemy bullseye exactly like a normal bullet.

Example:

```text
Enemy facing away from Player.

Bullseye is exposed on enemy's back.

Direct line:
BLOCKED / impossible

Player aims at ricochet wall.

Player
   \
    \
     WALL
        \
         \
          ● Enemy Bullseye
```

If the reflected ray hits the bullseye:

```text
Apply normal bullseye hit logic.
```

Do NOT create separate ricochet-specific bullseye damage code.

Reuse the current authoritative hit/damage system.

---

# 11. Other Player Body Hits

If a reflected shot hits an ordinary valid player hitbox instead of the bullseye, process that hit according to the existing weapon/player damage rules.

The ricochet system should modify:

```text
HOW THE RAY TRAVELS
```

not create a second combat system.

---

# 12. Ricochet Impact Effects

When the first ray hits a ricochet surface, the game should still show an appropriate impact at the ricochet point.

For example:

```text
Bullet
   ↓

Wall ●  <- bullet impact / spark
      \
       \
        reflected shot
```

Reuse the project's existing:

- Bullet impact decals
- Impact effects
- Surface hit effects
- Sounds

where practical.

A future requirement can add a distinctive ricochet sound if needed.

---

# 13. Ricochet Aim Prediction

The player should be able to see where the bank shot is predicted to go **before firing**.

When the player's weapon aim ray intersects a valid RicochetSurface:

```text
Aim Ray
   ↓
Hit Ricochet Wall
   ↓
Calculate reflected direction
   ↓
Prediction Ray
   ↓
Find predicted second impact
```

The game should display an indicator at that second impact point.

Example:

```text
                         ● Predicted impact
                       /
                     /
                   /
              WALL ●
                  /
                /
PLAYER --------
```

The marker helps the player understand where the bullet will land after reflecting.

---

# 14. Indicator Meaning

The indicator should mean:

> If the player fires approximately along the current aiming ray, the ricocheted bullet is expected to hit here.

It should NOT:

- Lock onto enemies
- Automatically identify bullseyes
- Adjust the player's aim
- Guarantee a hit
- Bend the bullet toward a target

It is only a trajectory visualization.

This should remain a skill-based mechanic.

---

# 15. Indicator Only on Valid Ricochet

The predicted second-impact marker should only appear when:

1. The player's current aim first intersects a valid RicochetSurface.
2. The reflected ray finds a valid second collision within its allowed range.

If the player aims at a normal wall:

```text
No ricochet marker.
```

If the player aims into the sky:

```text
No ricochet marker.
```

If the reflected trajectory never intersects anything:

```text
No second-impact marker.
```

---

# 16. Indicator Location

Place the marker at the actual predicted collision point of the reflected ray.

Align it appropriately with the second surface normal so it appears attached to the destination surface.

Example:

```csharp
indicator.position = predictedHit.point;
indicator.rotation =
    Quaternion.LookRotation(predictedHit.normal);
```

Cursor should adapt the implementation to the selected marker asset and existing UI/world-space systems.

---

# 17. Indicator Style

Use a temporary visual indicator initially.

Possible options:

- Small circle
- Dot
- Reticle-like decal
- Ring
- Subtle projected marker

Do NOT spend significant time creating final art.

The indicator should be:

- Visible enough to aim with
- Small enough not to obscure enemies
- Clearly distinguishable from the standard crosshair
- Non-intrusive

---

# 18. Optional Ricochet Point Visualization

If useful, also show a subtle indicator at the first collision point on the ricochet wall.

For example:

```text
                 Destination
                     ●

                    /
                   /
       Bounce ●---/
             /
            /
PLAYER ----
```

However, the **second impact location** is the core requirement.

Do not overcomplicate the HUD if one marker provides enough information.

---

# 19. Prediction Must Match Real Shot

This is critical.

The predicted trajectory and the actual fired trajectory must use the **same ricochet calculation logic**.

Do NOT independently implement:

```text
Aim Prediction Formula
```

and:

```text
Actual Bullet Formula
```

in separate systems that may drift apart.

Prefer a shared function such as:

```csharp
CalculateRicochetTrajectory(...)
```

used by:

```text
Aim Predictor
```

and:

```text
Actual Shot
```

This ensures that:

```text
Marker says bullet goes here
```

and:

```text
Bullet actually goes there
```

remain consistent.

---

# 20. Hip Fire vs ADS

The predictor should respect the game's actual weapon aiming system.

Cursor should inspect how current hitscan origin/direction differs between:

- Hip fire
- ADS / zoom
- Different weapon types

The predictor must use the same effective firing direction the real shot will use.

Do not use the center of the screen blindly if the weapon system contains spread or other directional corrections.

---

# 21. Weapon Spread

Weapons with spread introduce a special consideration.

The predictor should represent the **intended/central trajectory**, not guarantee the exact destination of a randomized spread shot.

For weapons with accuracy bloom/spread:

```text
Indicator = center/nominal ricochet path
Actual bullet = may vary according to weapon spread
```

Do not disable weapon spread just to make ricochet prediction exact.

This keeps normal weapon balancing intact.

---

# 22. Shotgun Behavior

The shotgun fires multiple pellets with spread.

Do NOT display a separate ricochet destination marker for every pellet.

For REQ-052:

Use the central/nominal aim trajectory for the visual ricochet predictor.

Actual shotgun pellets may each independently interact with the ricochet system if doing so integrates cleanly with the existing pellet raycast architecture.

Preferred behavior:

```text
Each shotgun pellet:
    Raycast
    ↓
If RicochetSurface:
    Reflect pellet once
```

This could produce interesting bank-shot spreads.

However, do not rewrite the entire shotgun system if this proves disproportionately invasive.

At minimum, the architecture should not break shotgun firing.

---

# 23. Surface Configuration

The RicochetSurface component should expose useful configuration.

Potential fields:

```csharp
[SerializeField]
private bool ricochetEnabled = true;
```

Potential future options could include:

```text
Ricochet strength
Allowed weapon types
Maximum bounce count
Material-specific effects
Damage multiplier
```

but these do NOT all need implementation now.

REQ-052 primarily requires:

```text
Surface has RicochetSurface
        ↓
Bullet can ricochet
```

Keep the first version simple.

---

# 24. Prefab / Level Designer Workflow

The desired workflow should be easy.

Example:

```text
1. Select a wall in Unity.
2. Ensure the wall has a Collider.
3. Add RicochetSurface component.
4. Enter Play Mode.
5. Bullets can now bank off that wall.
```

This should work on:

- Walls
- Floors
- Ceilings
- Angled surfaces
- Props

provided the object has an appropriate collider.

This allows level design to intentionally create ricochet-friendly areas.

---

# 25. Angled Level Geometry

The system must work correctly on non-axis-aligned surfaces.

For example:

```text
/
/
/
```

or:

```text
      ______
     /
    /
___/
```

Reflection must be calculated from the actual raycast surface normal, not assumptions such as:

```text
walls always face X or Z.
```

---

# 26. Multiplayer Authority

Actual hits and damage must remain authoritative under the existing multiplayer architecture.

The local player may calculate trajectory prediction locally because it is visual aiming feedback.

However:

```text
Prediction marker
```

must NOT determine whether a hit occurred.

The authoritative shot system should independently process the actual fired ray using the shared ricochet logic.

Conceptually:

```text
LOCAL CLIENT

Calculate prediction
↓
Show marker

--------------------------------

AUTHORITATIVE SHOT

Fire
↓
Raycast
↓
Validate ricochet surface
↓
Reflect
↓
Raycast again
↓
Process hit
```

This prevents clients from simply claiming:

```text
"My marker was on the enemy, therefore I hit them."
```

---

# 27. Local-Only Prediction Indicator

The ricochet prediction marker should only be visible to the player currently aiming.

Do NOT network the marker to other players.

Other players do not need to see an opponent's predicted bank-shot location.

This is purely local aiming assistance.

---

# 28. Performance

Trajectory prediction may run frequently while the player aims.

Keep it lightweight.

A simple structure should normally require no more than:

```text
Raycast #1
+
Raycast #2 if first hit is ricochetable
```

per prediction update.

Do not search all RicochetSurface objects in the scene.

Determine whether the hit surface is ricochet-enabled directly from the hit collider/GameObject.

---

# 29. Component Lookup

Because the collider may be located on a child of a wall object, Cursor should account for reasonable prefab hierarchies.

For example:

```text
RicochetWall
├── RicochetSurface
└── Geometry
    └── MeshCollider
```

or:

```text
RicochetWall
├── Renderer
├── Collider
└── RicochetSurface
```

Use a stable lookup strategy appropriate to the existing architecture.

Avoid expensive hierarchy searches every frame if a cached or direct method is available.

---

# 30. Debug Visualization

Add useful development visualization.

When enabled, show:

```text
Initial aim ray
Ricochet point
Surface normal
Reflected ray
Second impact point
```

Example:

```text
             X predicted hit
            /
           /
          /
     ●---/
     ↑
 ricochet point
    /
   /
Player
```

Possible field:

```csharp
[SerializeField]
private bool showRicochetDebug;
```

Unity Gizmos or Debug.DrawRay are appropriate.

Debug visualization should make it much easier to verify reflection math.

---

# 31. No Aim Assist

This mechanic must NOT automatically seek targets.

For example:

```text
Player aims near angle that could hit enemy.
```

The game should NOT modify the ricochet direction to hit them.

The actual geometry calculation determines the destination.

This is important because the intended skill is:

> Learn to bank shots correctly.

---

# 32. No Enemy Information Leakage

The ricochet indicator should not provide information the player could not otherwise obtain.

If the reflected shot would hit an enemy behind a wall, do NOT change the marker into:

```text
Enemy detected!
```

or:

```text
Bullseye target!
```

The marker should simply appear at the first physical collision point reached by the reflected ray.

If that happens to be an enemy that is legitimately along the ray, the shot system can naturally process it, but do not introduce extra enemy highlighting or wallhack-like information.

If necessary, Cursor should use the same visibility/layer rules used by normal shooting.

---

# 33. Prediction and Player Hitboxes

Be careful about showing the exact location of hidden players through geometry.

The trajectory predictor should primarily visualize the geometric ricochet path.

If using normal gameplay player colliders in the prediction ray would reveal hidden enemy positions, exclude remote player hitboxes from the local prediction marker calculation.

Preferred first implementation:

```text
Prediction marker displays destination on world geometry.
```

Actual fired ricochet rays can still hit players normally.

This prevents the feature from accidentally becoming a detection tool for enemies behind cover.

---

# 34. Indicator Against Environment

The ideal normal use case is:

```text
Player aims at ricochet wall
↓
Reflected ray hits floor / wall / ceiling
↓
Small indicator appears at that impact point
```

This allows the player to adjust the angle until the indicator reaches the approximate location they want.

Example:

```text
Player wants to hit behind cover.

Move aim slightly left:
Marker moves right.

Move aim slightly up:
Marker changes distance.

Fine-tune angle.

Fire.
```

This should make bank shots difficult but learnable.

---

# 35. Bullet Hole Decals

REQ-052 must remain compatible with the existing bullet-hole decal system.

A ricochet shot potentially has:

```text
Impact #1:
Ricochet surface

Impact #2:
Final destination
```

Appropriate impact visuals may appear at both points.

For example:

```text
Ricochet Wall:
small impact/decal/spark

Final Wall:
normal bullet-hole decal
```

Cursor should reuse the existing decal rules.

Do not duplicate the entire decal architecture.

---

# 36. Audio

If the project already has an appropriate impact sound architecture, a ricochet impact may use an existing metal/impact sound temporarily.

A unique:

```text
PING
```

or ricochet sound could significantly improve player feedback later.

However, creating or sourcing final ricochet audio is outside the scope of REQ-052.

---

# 37. Architecture

Cursor should inspect the project before implementation.

Specifically locate:

- Hitscan firing logic
- WeaponDefinition architecture
- Weapon spread system
- Bullseye hit detection
- Player damage handling
- Bullet impact effects
- Bullet-hole decals
- Layer masks
- Network hit validation
- Shotgun pellet implementation
- ADS/aim direction logic

Do NOT create a completely separate shooting implementation for ricochet bullets.

A conceptual structure might look like:

```text
Weapon Fires
    ↓
Hitscan Resolver
    ↓
Initial Raycast
    ↓
Hit RicochetSurface?
   / \
 No   Yes
 |      |
Normal  Calculate Reflection
Hit     |
        ↓
      Second Raycast
        ↓
      Final Hit
```

Aim prediction should call the same reflection helper:

```text
Aim Predictor
    ↓
Initial Raycast
    ↓
RicochetSurface?
    ↓
Shared Reflection Calculation
    ↓
Prediction Raycast
    ↓
Display Marker
```

Cursor should adapt this concept to the existing project rather than blindly creating these exact classes.

---

# 38. Important Design Principle

The ricochet mechanic should be:

```text
Predictable
but
Difficult
```

The indicator gives players the information needed to attempt a bank shot.

It does NOT make the bank shot automatic.

A skilled player should be able to develop intuition for:

- Reflection angles
- Wall positioning
- Enemy movement
- Bullseye location
- Weapon spread
- Timing

Successful ricochet bullseye hits should feel impressive.

---

# Acceptance Criteria

## AC-1 — Ricochet Component Exists

A reusable component such as:

```text
RicochetSurface
```

can be added to a surface with a collider.

---

## AC-2 — Component Enables Ricochet

A bullet hitting a surface with RicochetSurface:

```text
Reflects
```

while an otherwise equivalent surface without the component:

```text
Does not reflect.
```

---

## AC-3 — Reflection Uses Surface Angle

Changing the player's angle of fire changes the ricochet trajectory appropriately.

---

## AC-4 — Angled Walls Work

Ricochet behavior works on rotated and angled geometry rather than only perfectly vertical walls.

---

## AC-5 — One Bounce

Shots may ricochet once.

After the reflected ray reaches its next collision:

```text
Shot terminates normally.
```

---

## AC-6 — Range Preserved

Ricochet shots do not unintentionally gain approximately twice the normal weapon range.

---

## AC-7 — Bullseye Hit

A ricocheted bullet can strike an enemy bullseye and invoke the same bullseye-hit logic as a direct shot.

---

## AC-8 — Normal Player Hit

A reflected shot can interact with normal player hitboxes according to existing weapon rules.

---

## AC-9 — Prediction Appears

When aiming at a valid ricochet surface whose reflected ray reaches world geometry:

```text
A predicted impact marker appears.
```

---

## AC-10 — Prediction Moves

As the player adjusts their aim:

```text
The predicted impact location moves according to the reflected trajectory.
```

---

## AC-11 — Normal Wall Has No Prediction

Aiming at a surface without RicochetSurface does not show the ricochet destination marker.

---

## AC-12 — Prediction Matches Actual Shot

Firing without moving the aim should produce a ricochet trajectory consistent with the displayed prediction, subject to normal weapon spread.

---

## AC-13 — No Target Locking

The ricochet system does not automatically alter bullet direction toward players or bullseyes.

---

## AC-14 — No Hidden-Enemy Detection

The trajectory indicator does not become a method for detecting players through walls or cover.

---

## AC-15 — Local Indicator

Only the aiming player sees their ricochet prediction marker.

---

## AC-16 — Existing Shooting Preserved

Shots that do not strike RicochetSurface objects behave exactly as they did before REQ-052.

---

## AC-17 — Existing Bullet Decals Preserved

Normal impact and bullet-hole behavior continues functioning.

---

## AC-18 — Multiplayer

In Host/Client gameplay:

```text
Client fires at RicochetSurface
↓
Bullet ricochets
↓
Reflected shot hits Host/Client target
```

All clients correctly observe the authoritative resulting damage/bullseye interaction without duplicate hits.

---

# Testing Scenarios

When testing becomes possible, explicitly test:

1. Fire directly into normal wall.
2. Fire directly into RicochetSurface wall.
3. Fire at approximately 45°.
4. Fire at shallow angle.
5. Fire at steep angle.
6. Ricochet off rotated wall.
7. Ricochet off angled ramp.
8. Ricochet off floor.
9. Ricochet off ceiling.
10. Verify only one bounce occurs.
11. Verify total weapon range is respected.
12. Aim at RicochetSurface and observe marker.
13. Slowly rotate aim and verify marker moves smoothly.
14. Aim away from RicochetSurface and verify marker disappears.
15. Fire where marker predicts.
16. Test rifle ricochet.
17. Test pistol ricochet.
18. Test DMR ricochet.
19. Test shotgun compatibility.
20. Hit bullseye through ricochet.
21. Hit non-bullseye player collider through ricochet.
22. Verify bullet-hole decals.
23. Test Client → Host ricochet kill.
24. Test Host → Client ricochet kill.
25. Test Client → Client ricochet hit.
26. Aim at geometry with hidden enemy behind it and verify the predictor does not reveal the enemy's location.
27. Add RicochetSurface to a new object and confirm it works without additional coding.

---

# Out of Scope

Do NOT include the following in REQ-052:

- Multiple-bounce trick shots
- Automatic enemy targeting
- Automatic bullseye targeting
- Bullet trajectory bending
- Grenade ricochets
- Player-body ricochets
- Final ricochet VFX
- Final ricochet sounds
- Damage balancing based on ricochet count
- Complex material penetration
- Destructible ricochet surfaces
- Dynamic surface deformation
- Specialized ricochet weapons
- Major weapon-system rewrite

These may be considered in later tickets.

---

# Desired Player Experience

A player sees an enemy whose bullseye is inaccessible from their current angle.

```text
        WALL

PLAYER   \

           \

             ENEMY
                ●
```

Instead of repositioning, the player looks toward a nearby ricochet wall.

As they aim:

```text
PLAYER ---------> ● WALL
                    \
                     \
                      \
                       ○
```

A small marker shows the predicted destination of the reflected bullet.

The player adjusts the crosshair.

```text
PLAYER -------> ●
                  \
                   \
                    \
                     ○ ← predicted impact moves
```

They eventually align the bank shot with the enemy's exposed bullseye.

```text
PLAYER -------> ● WALL
                  \
                   \
                    \
                     ● BULLSEYE
```

They fire.

```text
BANG

PLAYER -------> ●
                  \
                   \
                    \
                     ●

                Bullseye hit
```

The successful shot should feel difficult, intentional, and highly satisfying.

---

# Definition of Done

REQ-052 is complete when level designers can add a reusable **RicochetSurface** component to selected colliders, hitscan bullets striking those surfaces can reflect once using the actual surface normal, the reflected shot can interact normally with players and bullseyes, and the local player can see a lightweight predicted second-impact marker that accurately responds to their aim without creating aim assist, hidden-enemy detection, multiplayer desynchronization, or regressions to the existing weapon and bullet-impact systems.