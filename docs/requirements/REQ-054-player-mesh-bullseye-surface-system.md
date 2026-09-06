````markdown
# REQ-054 — Player-Mesh Bullseye Surface System

## Summary

Replace the current bullseye implementation in which a physical bullseye GameObject crawls around the player's cylindrical body with a new system in which:

- The actual animated player character mesh is the visible player body.
- The attached bullseye appears as a **2D bullseye mark/decal rendered directly against the surface of the player's mesh**.
- The bullseye can move continuously across valid areas of the player's body.
- The bullseye remains an actual gameplay hit target despite no longer being a physical disc while attached.
- When removed by a Bullseye-displacing mechanic such as the **Combustion Grenade** or **Magnetism Grenade**, the mesh bullseye disappears and a physical bullseye GameObject appears at its current location.
- When that physical bullseye is returned to the player, the physical object disappears and the mesh/decal bullseye becomes active again.

This should replace the legacy cylindrical-body bullseye traversal architecture rather than layering another system on top of it.

---

# Goals

1. Make the actual player model the surface on which the bullseye exists.
2. Eliminate the appearance of a physical bullseye disc floating/crawling over the player while it is attached.
3. Preserve all existing bullseye gameplay functionality.
4. Allow the bullseye to move naturally over an animated/skinned player mesh.
5. Preserve player influence over bullseye movement.
6. Preserve autonomous/random bullseye movement.
7. Preserve grenade-based bullseye removal.
8. Preserve multiplayer synchronization.
9. Create a cleaner architecture for future bullseye-related mechanics.

---

# Important Architectural Change

The bullseye should no longer fundamentally be treated as:

> "A physical object moving around the player's body."

Instead, it should fundamentally be treated as:

> "A gameplay position on the player's body surface."

That authoritative surface position should drive:

- Bullseye rendering
- Bullseye hit detection
- Autonomous movement
- Player-influenced movement
- Networking
- Physical bullseye spawning when detached
- Physical bullseye reattachment

The visible representation can therefore change depending on the bullseye's state.

---

# Bullseye States

Introduce or formalize an explicit bullseye state system.

At minimum:

```text
Attached
Detached
Returning
```

Additional internal states may be added if useful.

---

## Attached

When attached:

- The bullseye appears on the player's actual mesh.
- There should NOT be a visible physical bullseye disc hovering over the character.
- The bullseye should conform visually to the player's body surface.
- The bullseye continues moving around the player.
- The bullseye remains shootable.
- Grenades capable of removing the bullseye can still interact with it.

---

## Detached

When detached:

- The mesh/decal bullseye immediately disappears.
- A physical bullseye object appears at the bullseye's current world-space surface position.
- The physical object should inherit an appropriate orientation based on the surface it detached from.
- Existing grenade forces / attraction / physics should operate on the physical object.
- The attached bullseye hit target should be disabled.

There must NEVER be both:

- a visible attached bullseye

and

- a visible detached physical bullseye

at the same time for the same player.

---

## Returning

When the existing return behavior begins:

- The physical bullseye should perform its return behavior.
- The attached decal should remain hidden during the return.
- Once reattachment is completed:
  - disable/despawn the physical bullseye;
  - establish the new valid surface position;
  - reactivate the attached visual bullseye;
  - reactivate attached bullseye hit detection.

The transition should appear seamless.

---

# Player Prefab Changes

The actual player character should become the primary visible player body.

Remove the legacy cylinder from any responsibility for:

- visible player geometry;
- bullet impact positioning;
- bullseye movement;
- bullseye rendering;
- determining valid bullseye locations.

## IMPORTANT — Player Collision

Do NOT automatically replace reliable player locomotion collision with a complex animated MeshCollider.

If the current cylinder/capsule is necessary for:

- CharacterController movement;
- Rigidbody stability;
- stairs;
- walls;
- grounding;
- movement collision;

then it may remain as an **invisible locomotion collider**.

The requirement is that players should no longer SEE or GAMEPLAY-INTERACT with the cylinder as though it were the player's body.

Separate:

```text
Player locomotion collider
```

from:

```text
Player visual/damage body
```

The actual character mesh and appropriate hitboxes should represent the player for combat.

---

# Attached Bullseye Rendering

Create a new system capable of displaying the bullseye against the animated player's mesh.

The result should look approximately like a bullseye painted/stuck directly onto the player's body.

It should:

- follow body animation;
- follow rotation;
- follow crouching;
- follow prone;
- follow jumping;
- follow wall running;
- follow dolphin diving;
- follow weapon animations;
- remain visually close to the mesh;
- not noticeably float above the character;
- not sink underneath the character;
- not remain floating in space when the underlying body part moves.

---

# Implementation Strategy

Cursor should inspect the existing character materials, SkinnedMeshRenderer, HDRP configuration, UV layout, animation architecture, and bullseye code before choosing the rendering implementation.

Possible techniques include:

1. A shader/material-based bullseye positioned in player-mesh surface/UV space.
2. A decal-based system positioned and oriented against the animated player surface.
3. A secondary surface-following overlay mesh.
4. Another appropriate skinned-mesh technique.

Do NOT force a technique simply because it is easiest to prototype if it fails when the character animates.

### Preferred Outcome

A shader/surface-based approach is preferred if practical because the bullseye then naturally follows deformation of the SkinnedMeshRenderer.

However, Cursor may choose another implementation if testing demonstrates that it is substantially more reliable.

---

# Do Not Use a Per-Frame Dynamic MeshCollider

Do NOT solve this by continuously rebuilding a full MeshCollider from the animated SkinnedMeshRenderer every frame unless there is an extremely compelling reason.

Avoid unnecessarily expensive solutions such as repeatedly baking the entire character mesh merely to determine bullseye movement.

The solution should be appropriate for an online multiplayer FPS with multiple simultaneous players.

---

# Bullseye Surface Navigation

The bullseye must be capable of moving between valid positions on the character.

Do NOT simply generate arbitrary XYZ offsets relative to the player.

The movement should understand that the bullseye belongs to the player's body surface.

A good architecture may involve a configurable **Bullseye Surface Map**.

For example:

```text
Head
Neck
Upper Chest
Lower Chest
Upper Back
Lower Back
Left Shoulder
Right Shoulder
Left Upper Arm
Right Upper Arm
Left Forearm
Right Forearm
Left Thigh
Right Thigh
Left Lower Leg
Right Lower Leg
```

The exact regions should be determined based on the player's mesh.

---

# Surface Graph / Valid Paths

Strongly consider creating valid surface points, regions, or paths rather than allowing completely unrestricted random movement.

For example:

```text
Upper Chest
    ↓
Lower Chest
    ↓
Abdomen

Upper Chest
    ↔
Left Shoulder
    ↔
Left Upper Arm

Upper Chest
    ↔
Right Shoulder
    ↔
Right Upper Arm

Upper Chest
    ↑
Neck
    ↑
Head
```

The bullseye could interpolate between valid surface locations.

This allows us to control:

- where the bullseye may appear;
- how it moves;
- which body areas connect naturally;
- avoidance of impossible paths;
- movement across UV seams;
- gameplay balancing;
- player manipulation mechanics.

The system should be DATA-DRIVEN/configurable rather than hard-coding movement logic individually for every body part.

---

# Smooth Movement

The bullseye should not simply teleport between random body locations during normal movement.

Movement should have:

- a start position;
- a destination;
- movement speed;
- interpolation;
- optional pauses;
- another destination after arrival.

Example:

```text
Current Surface Location
        ↓
Choose Valid Neighbor/Target
        ↓
Move Smoothly
        ↓
Pause
        ↓
Choose Another Target
```

Movement speed and pause ranges should be configurable.

---

# Autonomous Bullseye Movement

Preserve the concept that the bullseye moves on its own.

The player should never have perfect control over its location.

When unaffected by player actions, it should wander semi-randomly across the player.

Movement should feel unpredictable without looking completely erratic.

Expose configuration for values such as:

```text
BaseMovementSpeed
MinPauseDuration
MaxPauseDuration
RandomDirectionWeight
RegionSelectionWeights
```

Exact tuning can be adjusted later.

---

# Player Manipulation of Bullseye Position

Preserve the existing concept that players can INDIRECTLY manipulate where their bullseye travels.

This is an important Bullseye mechanic.

The player should influence the bullseye but should not directly steer it like a cursor.

---

## Jump Influence

Repeated jumping should bias the bullseye upward toward the player's head.

For example:

```text
Jump
→ slight upward influence

Repeated jumps
→ increasingly strong tendency toward upper torso / neck / head
```

This does NOT necessarily mean:

```text
Jump = instantly move bullseye one body region upward
```

Instead, jumping should modify the bullseye's movement bias.

Possible conceptual implementation:

```text
BullseyeInfluence.Vertical += JumpInfluence
```

Then decay that influence over time.

---

## Repeated Jumping

Repeated jumps within a configurable time window should compound the effect.

Example:

```text
Jump 1
Small upward bias

Jump 2
Moderate upward bias

Jump 3+
Strong upward bias toward head
```

The effect should decay if the player stops jumping.

Expose values such as:

```text
JumpInfluenceAmount
JumpInfluenceWindow
MaximumJumpInfluence
JumpInfluenceDecayRate
```

---

# Existing Player Influence Mechanics

Inspect the current bullseye implementation for any other player actions already affecting bullseye movement.

Where they remain appropriate, preserve those mechanics.

Do not silently delete existing Bullseye movement gameplay merely because the underlying movement architecture is being replaced.

Translate the behavior to the new Surface Position system.

---

# Future Extensibility

Structure player influence so additional actions could later affect the bullseye.

Potential future examples:

```text
Sprint → rearward/downward influence
Crouch → upper-body influence
Prone → lateral influence
Taking damage → movement disturbance
Wall running → side-specific influence
Dolphin diving → large temporary displacement
```

These are NOT necessarily requirements for REQ-054.

The architecture should simply make future influences easy to add.

---

# Attached Bullseye Hit Detection

This is CRITICAL.

The visual bullseye cannot merely be decorative.

While attached, there must still be a gameplay hit target corresponding to the visible bullseye.

A shot visually landing on the bullseye must register as a bullseye hit.

Possible architecture:

```text
Bullseye Surface Position
        ↓
Visual Bullseye
        +
Bullseye Hit Target
```

The hit target may be an invisible collider or another appropriate hitscan detection mechanism.

It must:

- track the visual bullseye;
- remain aligned during animations;
- move with the bullseye;
- remain appropriately sized;
- function correctly from all shooting angles;
- integrate with the existing hitscan system.

---

# Hitbox Alignment

The visible bullseye and actual hittable area must agree closely.

Do not allow situations where:

- the player shoots the visible bullseye and misses because the collider is elsewhere;
- the player shoots beside the visual bullseye and receives a bullseye hit;
- animation causes the visual and gameplay targets to separate.

Temporary debug visualization should be created to inspect this.

Example debug mode:

```text
Visible Bullseye = normal visual

Bullseye Hit Target = bright debug sphere/disc
```

This debug visualization should be easily disabled.

---

# Existing Damage Rules

Do NOT redesign Bullseye damage as part of REQ-054.

Preserve existing behavior for:

- bullseye hits;
- head hits;
- torso hits;
- limb hits;
- player death;
- bullseye breaking;
- respawning.

This ticket changes HOW the attached bullseye is represented and positioned, not the fundamental damage model.

---

# Grenade Detachment

The new system must remain compatible with all mechanics capable of separating a bullseye from its owner.

This includes at minimum:

- Combustion Grenade
- Magnetism Grenade

When detachment is triggered:

```text
Attached Bullseye Surface Position
              ↓
Convert to world position/orientation
              ↓
Hide attached bullseye
              ↓
Disable attached bullseye hit target
              ↓
Spawn/enable physical bullseye
              ↓
Apply grenade interaction
```

The physical bullseye should originate from the exact location where the attached bullseye was immediately before detachment.

---

# Physical Bullseye Prefab

The existing physical bullseye concept should remain available.

It should now primarily represent the bullseye while DETACHED.

Potentially maintain a dedicated prefab such as:

```text
PhysicalBullseyePrefab
```

or equivalent.

It should contain whatever is required for:

- Rigidbody physics;
- collision;
- grenade force;
- magnetism;
- networking;
- return behavior;
- visual representation.

Do NOT require the physical prefab to remain continuously active while the bullseye is attached.

Pooling may be used if appropriate.

---

# Reattachment Position

When the bullseye returns to the player, determine an appropriate valid surface position.

Ideally it should return approximately to its original surface location unless existing gameplay specifies otherwise.

The system must convert correctly from:

```text
Physical Bullseye World Position
```

back into:

```text
Attached Bullseye Surface Position
```

or select an appropriate configured reattachment point.

Do not allow reattachment to:

- place the bullseye inside the mesh;
- place it floating away from the mesh;
- attach it to the invisible locomotion collider;
- attach it to the old cylinder.

---

# Animation Compatibility

Test the bullseye during at minimum:

- Idle
- Walking
- Sprinting
- Strafing
- Jumping
- Crouching
- Prone
- Dolphin Dive
- Wall Running
- Weapon firing
- ADS
- Reloading

The bullseye should remain visually associated with the correct portion of the player's body.

---

# Head Movement

Special attention should be paid to transitions onto the head.

If the bullseye travels:

```text
Chest → Neck → Head
```

it must follow the animated head rather than remaining positioned relative to the torso/root.

This principle applies to all articulated body parts.

For example:

```text
Upper Arm → Forearm
```

must properly account for elbow animation.

---

# Networking

The bullseye system must remain multiplayer-safe.

The SERVER should remain authoritative over meaningful bullseye gameplay state.

Do not independently randomize bullseye movement on every client.

Replicate enough information for all players to observe approximately the same bullseye location.

Possible replicated state:

```text
BullseyeState

CurrentSurfaceRegion
TargetSurfaceRegion
MovementProgress

or

CurrentSurfaceCoordinate
TargetSurfaceCoordinate

InfluenceState

DetachedPhysicalBullseyeReference
```

Exact implementation is up to Cursor.

---

# Network Efficiency

Do NOT send the bullseye's full world-space transform every rendered frame if that can reasonably be avoided.

Prefer synchronizing meaningful movement state and interpolating visually on clients.

For example:

```text
SurfacePoint A
SurfacePoint B
StartTime
TravelDuration
```

allows clients to reconstruct smooth motion.

The server should remain authoritative over hit validation and state transitions.

---

# Late Joiners

A client joining an existing match should immediately see:

- whether each player's bullseye is attached or detached;
- approximately where the attached bullseye is;
- the physical bullseye if detached;
- any active return state.

Late joiners should not temporarily see duplicate bullseyes.

---

# Death / Respawn

Ensure this system integrates with player death and respawn.

On respawn:

- bullseye state should reset correctly;
- only the correct representation should be active;
- surface navigation should initialize to a valid location;
- the hit target should align correctly;
- no physical bullseye from the previous life should remain orphaned.

Preserve existing spawn/respawn rules unless specifically required otherwise.

---

# Prefab Organization

The resulting Player prefab should have clear separation between systems.

Conceptually:

```text
Player
│
├── Movement / Network Components
│
├── Hidden Locomotion Collider
│
├── Player Character Model
│   └── SkinnedMeshRenderer
│
├── Combat Hitboxes
│
└── BullseyeSystem
    ├── BullseyeSurfaceController
    ├── BullseyeVisual
    ├── BullseyeHitTarget
    └── SurfaceMap / Configuration
```

The exact hierarchy may differ.

The important requirement is separation of responsibilities.

---

# Remove Legacy Dependencies

Once the new system works, remove obsolete assumptions that the bullseye:

- moves over a cylinder;
- positions itself using cylinder dimensions;
- requires the attached physical bullseye object's Rigidbody;
- determines body locations using cylindrical angles;
- uses the legacy cylinder as the player's damage surface.

Do not retain duplicate Bullseye implementations indefinitely.

Remove or clearly deprecate obsolete code after verifying that the replacement works.

---

# Inspector Configuration

Bullseye behavior should remain highly configurable.

Where appropriate expose:

```text
Bullseye Size

Base Movement Speed

Minimum Pause Time
Maximum Pause Time

Jump Influence
Jump Influence Window
Jump Influence Decay
Maximum Jump Influence

Surface Regions

Allowed Region Connections

Region Weights

Reattachment Behavior

Debug Visualization
```

Avoid magic numbers scattered throughout code.

---

# Debugging Tools

Add useful development visualization.

When Bullseye debugging is enabled, it should be possible to visualize:

- Current surface position
- Current target position
- Bullseye hit target
- Current surface region
- Next surface region
- Current movement direction
- Player movement influence
- Bullseye state
- Physical bullseye spawn position

This will be extremely useful when diagnosing alignment issues.

The debug view should be disabled during normal gameplay.

---

# Migration Strategy

Cursor should NOT attempt to rewrite every related system simultaneously without first understanding existing dependencies.

Recommended sequence:

### Phase 1 — Inspect

Identify:

- current Bullseye scripts;
- player prefab hierarchy;
- damage/hitscan code;
- bullseye movement code;
- grenade interactions;
- respawn logic;
- networking;
- character SkinnedMeshRenderer;
- locomotion collider;
- existing body hitboxes.

### Phase 2 — Establish Surface Position

Create the new authoritative bullseye surface-position architecture.

Verify the position can follow the animated player.

### Phase 3 — Visual Bullseye

Render the attached bullseye against the player mesh.

### Phase 4 — Hit Detection

Attach gameplay hit detection to that same position.

### Phase 5 — Movement

Recreate autonomous movement and player movement influence using the new surface system.

### Phase 6 — Detachment

Connect the surface state to the physical Bullseye prefab.

### Phase 7 — Grenades

Verify Combustion and Magnetism grenade behavior.

### Phase 8 — Multiplayer

Verify server authority, replication, late joining, death, and respawn.

### Phase 9 — Cleanup

Remove obsolete cylinder-based Bullseye logic.

---

# Acceptance Criteria

REQ-054 is complete when ALL of the following are true.

## Player Appearance

- [ ] The actual player character is the visible world player.
- [ ] The old cylindrical body is no longer visible.
- [ ] The old cylinder is no longer the surface used for Bullseye movement.
- [ ] Any remaining simple locomotion collider is invisible and gameplay-separated from the visual body.

## Bullseye Visual

- [ ] Attached bullseye looks painted/projected/stuck onto the actual character mesh.
- [ ] No physical disc visibly floats around the player while attached.
- [ ] Bullseye follows animation correctly.
- [ ] Bullseye can occupy multiple body regions.
- [ ] Bullseye moves smoothly between positions.

## Movement

- [ ] Bullseye autonomously moves.
- [ ] Movement remains semi-random.
- [ ] Repeated jumping biases Bullseye movement toward the head.
- [ ] Jump influence decays appropriately.
- [ ] Player influence does not grant direct Bullseye control.

## Combat

- [ ] Attached bullseye remains shootable.
- [ ] Hit detection closely matches the visible bullseye.
- [ ] Existing Bullseye damage behavior remains functional.
- [ ] Existing body/head damage behavior remains functional.

## Detachment

- [ ] Combustion Grenade can detach the bullseye.
- [ ] Magnetism Grenade can detach/interact with the bullseye.
- [ ] Attached decal disappears immediately upon detachment.
- [ ] Physical bullseye appears at the correct location.
- [ ] Only one bullseye representation exists at a time.

## Return

- [ ] Physical bullseye can return.
- [ ] Physical bullseye disappears after successful reattachment.
- [ ] Mesh/decal bullseye reappears.
- [ ] Bullseye hit detection reactivates.
- [ ] Movement resumes normally.

## Animation

- [ ] Works while idle.
- [ ] Works while walking.
- [ ] Works while sprinting.
- [ ] Works while jumping.
- [ ] Works while crouched.
- [ ] Works while prone.
- [ ] Works during dolphin dive.
- [ ] Works during wall run.
- [ ] Works while firing/reloading/ADS.

## Multiplayer

- [ ] Server maintains authoritative Bullseye state.
- [ ] All clients see approximately the same bullseye location.
- [ ] Detachment synchronizes.
- [ ] Physical Bullseye physics synchronizes appropriately.
- [ ] Reattachment synchronizes.
- [ ] Death/respawn synchronizes.
- [ ] Late joiners receive correct Bullseye state.
- [ ] No duplicate Bullseyes appear.

---

# Non-Goals

REQ-054 does NOT require:

- redesigning the Bullseye damage system;
- changing health values;
- changing weapons;
- changing grenade tuning;
- adding new grenades;
- redesigning player animations;
- implementing every possible future Bullseye manipulation mechanic;
- replacing a reliable CharacterController/capsule purely for visual reasons.

---

# Final Expected Result

Before REQ-054:

```text
Player
    ↓
Cylinder
    ↓
Physical Bullseye object crawls around cylinder
```

After REQ-054:

```text
Animated Player Mesh
        ↓
Authoritative Bullseye Surface Position
        ↓
 ┌───────────────┬─────────────────┐
 │               │                 │
Visual Decal   Hit Target     Movement Logic
 │               │                 │
 └───────────────┴─────────────────┘
                 ↓
        Bullseye State
                 ↓
       Detached by Grenade
                 ↓
       Physical Bullseye
                 ↓
             Returns
                 ↓
       Mesh Bullseye Restored
```

The Bullseye should finally feel like a vulnerability that **belongs to the player's actual body**, rather than a separate object orbiting/crawling around an invisible cylinder.
````
