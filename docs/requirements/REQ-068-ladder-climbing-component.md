# REQ-068 — Ladder Climbing Component

## Summary

Add a reusable ladder/climbable component that can be attached to vertical level geometry to allow players to climb vertically.

The system should be designed so that level designers can make an object climbable simply by adding a component such as:

```text
LadderClimbable
```

to that object.

For REQ-068, focus only on the **gameplay movement and interaction system**.

Do **not** create a climbing animation yet.

The initial implementation may use the player's existing pose while climbing. Animation integration will be handled in a later requirement.

The intended workflow is:

```text
Create ladder / vertical object
        ↓
Add LadderClimbable component
        ↓
Player approaches object
        ↓
Player begins climbing
        ↓
Forward/back movement becomes vertical movement
        ↓
Player reaches top or bottom
        ↓
Normal movement resumes
```

---

# 1. LadderClimbable Component

Create a reusable Unity component for climbable vertical surfaces.

Suggested name:

```csharp
LadderClimbable
```

Attaching this component to an object should designate that object as climbable.

Examples:

```text
Ladder
Vertical pipe
Climbing wall
Future rope ladder
Industrial rung structure
```

For now, the system is primarily intended for ladders.

Do not hardcode specific ladder GameObjects or scene names.

---

# 2. Required Collider

The climbable object should use a Collider to define the area where climbing is possible.

Recommended implementation:

```text
Ladder GameObject
├── Visual Mesh
├── Collider
└── LadderClimbable
```

The LadderClimbable script should either:

- Use the object's existing collider

or

- Reference a dedicated climbing trigger collider.

A trigger-based interaction volume may be preferable if it prevents the ladder's physical geometry from interfering with player movement.

---

# 3. Detect Ladder Proximity

The player should detect when they are within range of an object containing:

```csharp
LadderClimbable
```

The system should determine:

```text
CurrentLadder
```

or equivalent.

When no climbable object is nearby:

```text
CurrentLadder = null
```

Do not scan the entire scene every frame.

Use colliders, triggers, raycasts, or the most appropriate existing interaction architecture.

---

# 4. Entering a Ladder

The player should be able to begin climbing when:

```text
Player is within ladder interaction range
+
Player moves toward the ladder
```

For example:

```text
Player approaches ladder
        ↓
Pushes forward
        ↓
Character enters ladder climbing state
```

An additional interact button should not be required unless necessary for reliability.

The preferred experience is that ladders feel natural and automatic.

---

# 5. Ladder Climbing State

Add a clear climbing state to the player movement system.

Conceptually:

```text
Normal
Jumping
Crouching
Prone
Sprinting
Climbing
```

While:

```text
MovementState == Climbing
```

normal grounded locomotion should be temporarily overridden.

Do not attempt to simulate ladder climbing by simply adding upward velocity while leaving the rest of the movement system active.

---

# 6. Vertical Movement

While climbing:

```text
Forward Input = Move Up
Backward Input = Move Down
```

Examples:

### Controller

```text
Left Stick Up
→ climb upward

Left Stick Down
→ climb downward
```

### Keyboard

```text
W
→ climb upward

S
→ climb downward
```

Expose ladder climbing speed.

Suggested Inspector variable:

```csharp
float climbSpeed = 3f;
```

---

# 7. Horizontal Movement While Climbing

For the initial implementation, horizontal movement should be disabled while attached to the ladder.

Meaning:

```text
A / D
Left Stick Left / Right
```

should NOT allow the player to strafe sideways away from the ladder.

This prevents the character from drifting off the climbable surface.

A future system may support lateral ladder movement if needed.

---

# 8. Maintain Player Position Relative to Ladder

When the player enters the climbing state, keep them aligned to the ladder.

Prevent:

- Drifting away from ladder.
- Moving through ladder.
- Slowly rotating off ladder.
- Falling because of gravity.
- Sliding sideways.

Determine an appropriate climbing plane from the LadderClimbable component.

Conceptually:

```text
Player
   ↓
snap/align to ladder climbing plane
   ↓
move along ladder's vertical axis
```

Any initial snapping should feel subtle and not teleport the player dramatically.

---

# 9. Ladder Orientation

Do not assume ladders always face world-forward.

The system should use the climbable object's transform.

For example:

```text
ladder.transform.up
```

should define the primary climbing direction when appropriate.

Likewise, the ladder's forward direction should determine which side the player climbs from.

This allows ladders to be rotated in level design.

---

# 10. Gravity While Climbing

Disable or override gravity while the player is actively climbing.

The player should not:

- Slowly slide down.
- Fall while stationary.
- Accelerate downward.
- Fight against gravity every frame.

When the player leaves the ladder, normal gravity should resume immediately.

---

# 11. Stopping on Ladder

The player should be able to stop climbing and remain stationary.

Example:

```text
Player climbs halfway up ladder
        ↓
Releases movement stick
        ↓
Player remains at that height
```

They should not automatically slide downward.

---

# 12. Camera Control

Normal camera/look controls should continue functioning while climbing.

The player should still be able to:

```text
Look left
Look right
Look up
Look down
```

Do not lock camera rotation to the ladder.

However, climbing movement itself should remain vertically constrained.

---

# 13. Character Facing Direction

When entering a ladder, rotate the character appropriately so the player faces the ladder.

The world-view player mesh should face toward the climbing surface.

This will also make adding ladder animations later significantly easier.

Avoid instant harsh rotation if practical.

Expose optional rotation speed:

```csharp
float ladderRotationSpeed;
```

---

# 14. Climbing From Bottom

Typical ladder entry:

```text
Player walks toward bottom of ladder
        ↓
Moves forward
        ↓
Climbing begins
        ↓
Forward input moves player upward
```

The player should not need to jump onto the first rung.

---

# 15. Climbing Down From Top

The system should also support entering a ladder from above.

Example:

```text
Player walks near ladder opening
        ↓
Moves backward/down toward ladder
        ↓
Player attaches to ladder
        ↓
Player climbs downward
```

This is important so ladders are useful in both directions.

Implementation can use:

- Top entry trigger
- Ladder bounds detection
- Directional checks
- Existing ladder volume

Choose whichever fits the movement architecture best.

---

# 16. Reaching the Top

When the player reaches the top of the ladder, they need to transition back into normal movement.

Because ladder animations are not part of REQ-068, use a simple functional transition.

Possible initial behavior:

```text
Player reaches top
        ↓
Small forward/upward movement
        ↓
Player is placed safely on top surface
        ↓
Exit climbing state
```

Do not require an animation.

The objective is simply to prevent the player from becoming stuck at the final rung.

---

# 17. Top Exit Marker

If useful, allow LadderClimbable to optionally define a top exit point.

Example hierarchy:

```text
Ladder
├── LadderClimbable
├── BottomPoint
└── TopExitPoint
```

Suggested serialized reference:

```csharp
Transform topExitPoint;
```

If assigned, the system may use this point to position the player when exiting from the top.

This is optional but may greatly improve reliability across different ladder geometry.

---

# 18. Bottom Exit

When climbing downward and reaching the bottom:

```text
Exit climbing state
        ↓
Restore gravity
        ↓
Return to normal locomotion
```

The player should smoothly transition onto the ground.

---

# 19. Jumping Off Ladder

Allow the player to intentionally leave the ladder using Jump.

Example:

```text
A button / Space
```

while climbing:

```text
Detach from ladder
        ↓
Restore gravity
        ↓
Perform a small jump away from ladder
```

Expose values such as:

```csharp
float ladderJumpAwayForce;
float ladderJumpUpForce;
```

This behavior should feel similar to jumping backward away from the ladder.

---

# 20. Exit Ladder by Moving Away

If practical, allow movement away from the ladder to detach the player.

For example:

```text
Player pulls backward away from ladder
```

could exit climbing mode if they are not intentionally climbing downward.

However, this must not conflict with:

```text
Backward Input = Climb Down
```

Therefore, Jump should remain the clearest manual detach method for the initial implementation.

---

# 21. Sprinting

Sprinting should be disabled while climbing.

Do not allow:

```text
Sprint input
→ increased climbing speed
```

unless explicitly added in a future requirement.

Climbing should use its own:

```text
climbSpeed
```

value.

---

# 22. Crouch and Prone

Disable transitions into:

```text
Crouch
Prone
Dolphin Dive
Slide
```

while actively climbing.

These movement states should not activate until the player leaves the ladder.

---

# 23. Weapons While Climbing

For REQ-068, preserve weapon state but prioritize reliable climbing movement.

The player does not need any special ladder weapon animation yet.

Do not:

- Drop weapon.
- Change equipped weapon.
- Reset inventory.

If existing shooting mechanics work correctly while climbing, they may remain enabled.

If shooting causes significant movement/animation conflicts, it is acceptable to temporarily disable firing while climbing and document that behavior for a later ticket.

Do not perform a large weapon-system rewrite as part of REQ-068.

---

# 24. Animation — Deferred

Do NOT create ladder climbing animations as part of this ticket.

The player can temporarily appear:

```text
Standing
or
Frozen in an existing movement pose
```

while vertically moving.

The important objective is to establish a robust:

```text
Climbing State
```

that a later animation system can detect.

Expose a value such as:

```csharp
bool IsClimbing
```

to the player's Animator if practical.

Potential future Animator parameter:

```text
IsClimbing = true
ClimbSpeed = verticalInput
```

But creating or sourcing the actual animation is outside the scope of REQ-068.

---

# 25. LadderClimbable Inspector

The component should expose useful configuration.

Suggested values:

```csharp
public class LadderClimbable : MonoBehaviour
{
    float climbSpeed;

    Transform topExitPoint;
    Transform bottomExitPoint;

    float playerOffsetFromSurface;

    bool allowJumpOff;
}
```

Exact architecture may differ.

Avoid unnecessarily placing all movement logic inside the ladder component itself.

Ideally:

```text
LadderClimbable
= describes the climbable surface

PlayerMovement
= performs climbing
```

---

# 26. Reusable Level Design Workflow

The system should support the following workflow:

```text
1. Place ladder mesh.

2. Add collider.

3. Add LadderClimbable component.

4. Configure optional exit points.

5. Play game.

6. Ladder works.
```

Do not require new code for every ladder.

---

# 27. Non-Ladder Vertical Objects

Although the feature is called a ladder system, it should preferably work with other vertical climbing surfaces.

Example:

```text
Metal ladder
Wooden ladder
Pipe
Rungs attached to wall
Future climbable vines
```

As long as the object contains:

```csharp
LadderClimbable
```

the movement system should treat it as a ladder-like climbable object.

---

# 28. Multiplayer Synchronization

Ladder movement must work with the existing multiplayer architecture.

Other clients should correctly observe:

- Player entering ladder.
- Player moving vertically.
- Player stopping.
- Player climbing down.
- Player jumping off.
- Player exiting at top.
- Player exiting at bottom.

Do not make climbing purely local visual movement.

It should integrate with the same player movement/network authority model used by normal locomotion.

---

# 29. Remote Player State

Other clients should know when a player is climbing.

Expose or synchronize an appropriate state such as:

```text
IsClimbing
```

if needed.

This will be particularly important when ladder animations are added later.

---

# 30. Respawn Compatibility

If a player is eliminated while climbing:

```text
Elimination system takes priority
```

REQ-066 should proceed normally.

The player's ladder state must be cleared during elimination/respawn.

After respawn:

```text
IsClimbing = false
CurrentLadder = null
Gravity restored
Normal movement restored
```

Do not allow a respawned player to remain logically attached to the previous ladder.

---

# 31. Ladder Trigger Cleanup

Ensure the player exits ladder state correctly when:

- Leaving the collider.
- Jumping away.
- Reaching the top.
- Reaching the bottom.
- Being eliminated.
- Respawning.
- Disconnecting.
- Scene changing.

Avoid stale references to destroyed or unloaded LadderClimbable objects.

---

# 32. Multiple Ladders

The system should work when several ladders are placed close together.

The player should attach to the ladder they are actually interacting with.

Avoid unpredictable switching between two nearby climbable components.

---

# 33. Ladder Width

The LadderClimbable component should use the object's collider/bounds rather than assuming one fixed ladder width.

This allows:

```text
Narrow ladders
Wide ladders
Large industrial ladders
```

without rewriting the player system.

---

# 34. Vertical Bounds

The player should only be allowed to climb within the valid ladder area.

Do not allow:

```text
Player reaches top
→ continues flying upward indefinitely
```

or:

```text
Player reaches bottom
→ continues moving downward through floor
```

Use collider bounds, explicit limits, or top/bottom points.

---

# 35. Debug Visualization

When LadderClimbable is selected in the Unity Editor, optionally draw Gizmos for:

- Climbing area.
- Ladder forward direction.
- Top exit.
- Bottom exit.
- Player alignment position.

This will make level setup easier.

Example:

```text
↑ Top Exit

│
│ Ladder Climbing Volume
│
│

↓ Bottom
```

---

# 36. Performance

The ladder system should not perform expensive scene-wide searches every frame.

Avoid patterns such as:

```csharp
FindObjectsOfType<LadderClimbable>()
```

inside Update.

Use trigger detection, cached references, or appropriate physics queries.

---

# 37. Compatibility

Verify compatibility with:

- Normal movement
- Sprint
- Jump
- Crouch
- Prone
- Dolphin dive
- Weapons
- Camera
- Controller input
- Keyboard/mouse input
- Player animations
- REQ-066 elimination system
- Respawning
- Netcode for GameObjects
- Relay multiplayer
- Local multiplayer testing

---

# Acceptance Criteria

REQ-068 is complete when:

- [ ] A reusable LadderClimbable component exists.
- [ ] Adding LadderClimbable to an appropriate vertical object makes it climbable.
- [ ] The system does not depend on a specific ladder prefab or object name.
- [ ] Player can approach the bottom of a ladder and enter climbing state.
- [ ] Forward input moves the player upward.
- [ ] Backward input moves the player downward.
- [ ] Player can stop halfway up and remain stationary.
- [ ] Gravity does not pull the player down while climbing.
- [ ] Player remains aligned with the ladder while climbing.
- [ ] Player does not drift sideways from normal movement input.
- [ ] Camera look controls continue functioning.
- [ ] Player faces the ladder while climbing.
- [ ] Player can reach the top and return to normal movement.
- [ ] Player can reach the bottom and return to normal movement.
- [ ] Player can climb downward from the top.
- [ ] Jump allows the player to detach from the ladder.
- [ ] Sprint does not interfere with climbing.
- [ ] Crouch/prone/dolphin-dive states do not activate while climbing.
- [ ] No ladder animation is required for this ticket.
- [ ] A clear IsClimbing/player climbing state exists for future animation work.
- [ ] Climbing works using an Xbox controller.
- [ ] Climbing works using keyboard/mouse.
- [ ] Remote multiplayer clients see ladder movement correctly.
- [ ] Elimination while climbing works correctly.
- [ ] Respawning clears all ladder state.
- [ ] Multiple ladders can exist in a scene.
- [ ] Different ladder heights work correctly.
- [ ] Rotated ladders correctly use their own orientation.
- [ ] Player cannot climb infinitely beyond the ladder's bounds.

---

# Required Test Scenario

Create at least three test ladder objects:

```text
Ladder A
Short vertical ladder

Ladder B
Tall vertical ladder

Ladder C
Same ladder rotated to face a different direction
```

Each should contain:

```text
Collider
LadderClimbable
```

Test:

```text
1. Approach Ladder A from bottom.
2. Climb upward.
3. Stop halfway.
4. Continue upward.
5. Exit at top.

6. Approach Ladder A from top.
7. Climb downward.
8. Exit at bottom.

9. Climb Ladder B.
10. Jump off halfway up.

11. Climb Ladder C.
12. Confirm rotated orientation works.

13. Enter ladder during multiplayer.
14. Confirm remote player sees vertical movement.

15. Get eliminated while climbing.
16. Confirm REQ-066 runs correctly.
17. Respawn.
18. Confirm movement is normal.
```

---

# Desired Player Experience

The feature should feel straightforward:

```text
See ladder
    ↓
Walk into it
    ↓
Push forward
    ↓
Start climbing
    ↓
Move up/down naturally
    ↓
Reach destination
    ↓
Continue normal gameplay
```

REQ-068 is primarily about building a **reliable reusable climbing movement system**.

Visual polish and ladder-specific player animations will be handled later.