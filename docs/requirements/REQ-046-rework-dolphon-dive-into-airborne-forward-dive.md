# REQ-046 — Rework Dolphin Dive Into Airborne Forward Dive

## Objective

Replace the current dolphin-dive behavior with a more convincing forward diving movement.

The current implementation behaves too much like a slide:

```text
Sprint
   ↓
Hold Crouch
   ↓
Crouch
   ↓
Continue moving forward
   ↓
Prone
```

Instead, the dolphin dive should feel like the player launches themselves forward, briefly becomes airborne, and then lands hard in the prone position.

The player camera should help communicate this movement by:

1. Rising slightly at the beginning of the dive.
2. Moving forward with the player.
3. Dropping rapidly toward prone camera height as the player lands.
4. Settling into the existing prone state.

---

# 1. Existing Trigger

Keep the existing dolphin-dive input requirement unless the current architecture requires minor cleanup.

The player should initiate a dolphin dive when:

```text
Player is sprinting
+
Player holds the crouch button
```

Current crouch controls should remain unchanged.

The existing distinction should remain:

```text
Normal movement + hold crouch
    ↓
Enter prone normally

Sprint + hold crouch
    ↓
Dolphin dive
```

Do not turn ordinary prone transitions into dives.

---

# 2. Remove Slide-Like Behavior

The current behavior appears to progressively transition through crouch while continuing horizontally along the ground.

This should be replaced.

During a successful dolphin dive, the player should not visually feel like they are:

* Sliding on their feet
* Gradually crouching
* Crouch-running
* Transitioning slowly from sprint → crouch → prone

Instead, the movement should approximately be:

```text
SPRINT
   ↓
LAUNCH
   ↗
    →→→
        ↘
          PRONE LANDING
```

The player should spend a short period off the ground or visually simulated as being off the ground.

---

# 3. Dolphin Dive Movement Phases

Implement the dive as distinct phases.

## Phase A — Launch

Immediately after the dive begins:

* Preserve the player's current forward direction.
* Add a strong forward movement impulse.
* Add a small upward component.
* Stop normal sprint movement from independently affecting the character.
* Begin the dive camera motion.

The player should feel like they deliberately threw themselves forward.

Suggested conceptual movement:

```text
Forward Velocity = High
Vertical Velocity = Small Positive Value
```

The upward force should be subtle.

This is not a jump.

The player should only rise enough to make the dive visually obvious.

---

## Phase B — Airborne Dive

For a short duration:

* Continue moving the player forward.
* Allow gravity to bring the player downward.
* Prevent normal grounded movement controls from overriding the dive.
* Maintain the player's dive direction.

A typical dive should last approximately:

```text
0.5–1.0 seconds
```

before landing, depending on tuning.

Do not hard-code the final duration if the existing controller can expose it as a configurable value.

---

## Phase C — Landing

When the player reaches the ground:

* End the airborne dive state.
* Transition immediately into prone.
* Position the camera at prone height.
* Restore prone movement behavior.
* Stop any remaining dive impulse that would cause continued sliding.

The player should not continue sliding several meters after impact unless we explicitly add that behavior later.

---

# 4. Camera Motion

The first-person camera is critical to selling the dive.

The camera should move through a short arc.

Conceptually:

```text
SPRINT CAMERA
      ↓

       ↑
     slight
      lift

       ↗
    LAUNCH

        →
       dive

          ↘

       ↓↓↓
   HARD LAND

   PRONE CAMERA
```

---

# 5. Camera Lift

At the beginning of the dolphin dive, raise the camera slightly above normal sprint height.

The lift should be noticeable but restrained.

Suggested initial range:

```text
+0.10 to +0.30 meters
```

relative to normal standing camera height.

The exact value must be configurable.

This upward motion should happen quickly:

```text
approximately 0.10–0.20 seconds
```

The goal is to communicate:

> The player pushed off the ground.

It should not resemble a normal jump.

---

# 6. Camera Forward Motion

The camera naturally follows the player's forward movement, but make sure the experience feels aggressive and committed.

The player should visibly surge forward after launch.

Avoid excessive camera lag that makes the body appear to move without the viewpoint.

---

# 7. Camera Crash / Landing Motion

As the player lands, rapidly move the camera downward toward the prone camera position.

This should be substantially faster than the normal standing-to-prone camera transition.

Conceptually:

```text
Airborne camera
       ↓
       ↓
       ↓
PRONE HEIGHT
```

The effect should feel like the player hits the ground.

Do not instantly teleport the camera if a very short interpolation produces a better result.

Suggested landing interpolation:

```text
0.08–0.20 seconds
```

Tune based on feel.

---

# 8. Landing Camera Impact

Add a small optional camera impact when the player hits the ground.

Possible effects:

* Brief downward camera impulse
* Very small rotational pitch
* Minor screen shake
* Quick recovery

Example:

```text
Landing
  ↓
small camera impact
  ↓
settle at prone height
```

Keep this restrained.

The effect should communicate physical impact without making the player disoriented or nauseous.

Expose the intensity as a configurable value.

---

# 9. Camera Pitch

During the dive, a subtle pitch effect may improve the sensation of motion.

Suggested behavior:

### Launch

Camera pitches slightly upward.

### Descent

Camera gradually pitches downward.

### Landing

Brief additional downward pitch / impact.

### Prone

Camera returns to normal prone aiming orientation.

This should be additive camera presentation only.

Do not forcibly rotate the player's actual aim direction by a large amount.

The player should retain reasonable aiming orientation after landing.

---

# 10. Player Control During Dive

Once the dive begins, the player should be committed to the action for a short duration.

Disable or substantially reduce:

```text
Forward/back movement modification
Strafing
Normal sprint acceleration
Crouch toggling
Jumping
Another dolphin dive
```

The player may retain limited camera look.

Recommended:

```text
Horizontal/vertical camera look:
Allowed

Movement steering:
Very limited or disabled
```

This prevents the player from dramatically changing direction while airborne.

---

# 11. Dive Direction

Capture the player's movement direction when the dive begins.

Preferred:

```text
DiveDirection = Current horizontal movement direction
```

If appropriate for the current controller, use the character's forward direction when sprinting primarily forward.

Once the dive is initiated, avoid continuously recalculating the trajectory from live movement input.

This makes the move feel committed.

---

# 12. Required Sprint State

Do not allow dolphin diving from:

* Idle
* Walking
* Crouching
* Prone
* Already airborne
* Already dolphin diving

The player must be legitimately sprinting when the move is triggered.

---

# 13. Ground Requirement

The player should normally begin a dolphin dive from a grounded state.

Do not allow players to initiate a dolphin dive while already falling or jumping unless deliberately supported later.

---

# 14. Collision Handling

The dive must respect normal collision detection.

The player must not:

* Pass through walls
* Dive through solid props
* Clip through terrain
* Enter floors
* Move through another collision barrier because of the forward impulse

If the player collides with a wall during the dive, stop or shorten the forward movement appropriately.

---

# 15. Low Obstacles

If the existing character controller naturally allows the player to dive over very small terrain changes, that is acceptable.

However, REQ-046 does not require:

* Vaulting
* Diving through windows
* Diving over cover
* Automatic obstacle traversal

Those should remain separate mechanics.

---

# 16. Landing Detection

Use reliable ground detection.

Do not rely only on a fixed timer to decide when the player becomes prone.

Preferred:

```text
Launch
  ↓
Airborne
  ↓
Ground detected
  ↓
Landing
  ↓
Prone
```

A maximum dive duration may still exist as a safety fallback.

---

# 17. Prone Transition

Once landing occurs:

* Set the player's posture to prone.
* Use the existing prone collider dimensions.
* Use the existing prone camera height.
* Use the existing prone movement speed.
* Use the existing prone weapon behavior.

Avoid maintaining a separate "dolphin-prone" state after landing.

The dive should simply end in the normal prone system.

---

# 18. Collider Handling

The player's collider must transition safely from standing/sprinting dimensions to prone dimensions.

Do not shrink the collider too early if doing so makes the player appear to clip through obstacles during the airborne portion.

Conceptually:

```text
Sprint Collider
       ↓
Dive / Temporary Collider State
       ↓
Landing
       ↓
Prone Collider
```

Cursor should use whichever approach best fits the existing character-controller architecture.

---

# 19. Temporary Dive Collider

If necessary, introduce a temporary collider configuration during the dive.

For example:

* Reduce standing height somewhat while airborne.
* Shift collider center appropriately.
* On landing, fully transition to prone dimensions.

This should prevent the invisible standing capsule from colliding with objects far above the diving player.

Do not overcomplicate this if the current controller already handles posture interpolation safely.

---

# 20. Dive Speed

Expose a configurable forward dive speed or impulse.

Suggested initial behavior:

```text
Dive Forward Speed:
Greater than sprint speed for a short burst
```

The purpose is to make the player meaningfully lunge forward.

The total distance should be modest.

Suggested initial target:

```text
approximately 2–4 meters
```

depending on the game's scale and existing sprint speed.

Tune through playtesting.

---

# 21. Upward Force

Expose:

```text
Dive Upward Force
```

This should remain substantially weaker than normal jump force.

The character only needs enough vertical movement to create a visible arc.

---

# 22. Gravity

Once launched, use normal or slightly enhanced gravity to bring the player down.

A slightly stronger downward force may make the landing feel more aggressive.

Expose an optional:

```text
Dive Gravity Multiplier
```

Suggested starting point:

```text
1.0–1.5x normal gravity
```

Do not use excessive gravity that causes snapping.

---

# 23. Existing Jump Behavior

Do not interfere with the normal jump system.

Dolphin diving and jumping should remain separate actions and states.

---

# 24. Weapon Behavior During Dive

For now, preserve the existing weapon as much as possible.

During the actual dive:

* Shooting may remain enabled if technically stable.
* Aiming/zoom may remain enabled only if it does not create severe camera conflicts.

However, if ADS conflicts with the camera dive animation, temporarily prevent entering ADS until landing.

Do not redesign weapon behavior as part of this ticket.

The priority is making the movement and camera feel correct.

---

# 25. Future Animation Integration

REQ-046 should work even before a final dolphin-dive animation is available.

Gameplay motion should come from the character controller rather than depending on animation root motion.

This is important because a future animation may simply provide the visual body movement while the existing dive code controls:

```text
Trajectory
Speed
Gravity
Collision
Landing
```

When a dolphin-dive animation is later added, it should be possible to layer it onto this mechanic without rewriting the movement system.

---

# 26. Third-Person / External Player Presentation

For networked remote players, the external character should eventually visibly transition into the dive.

Until a dedicated dolphin-dive animation is available, maintain the best available approximation without destabilizing the mechanic.

The local first-person camera behavior is the primary visual requirement for REQ-046.

---

# 27. Multiplayer

The dolphin dive must remain properly synchronized.

Other players should see the diving player's:

* Forward movement
* Airborne trajectory
* Landing position
* Final prone state

The first-person camera motion itself is local-only and should not need synchronization.

Do not network camera transforms.

---

# 28. Configurable Settings

Expose relevant tuning parameters in a clearly organized inspector section.

Example:

```text
Dolphin Dive

Trigger
Minimum Sprint Requirement

Movement
Forward Speed / Impulse
Upward Force
Gravity Multiplier
Maximum Duration
Maximum Steering

Camera
Launch Lift Height
Launch Lift Duration
Landing Drop Duration
Landing Impact Strength
Camera Pitch Amount

Landing
Ground Check
Landing Recovery Time
```

Names may differ based on the current codebase.

---

# 29. State Structure

Preferred conceptual state flow:

```text
SPRINT
   │
   │ Hold Crouch
   ▼
DIVE LAUNCH
   │
   ▼
DIVE AIRBORNE
   │
   │ Ground Contact
   ▼
DIVE LANDING
   │
   ▼
PRONE
```

Avoid:

```text
SPRINT
 ↓
CROUCH
 ↓
SLIDE
 ↓
PRONE
```

for the dolphin-dive path.

---

# 30. Optional Landing Recovery

Consider a very short period immediately after impact during which normal prone movement is reduced.

Example:

```text
0.10–0.30 seconds
```

This can make the landing feel heavier.

Do not make the player feel excessively locked in place.

Expose this value so it can be set to zero if unwanted.

---

# 31. Audio Hooks

Do not require new audio assets in this ticket, but expose or preserve an event/hook for:

```text
DolphinDiveLaunch
DolphinDiveLand
```

This will allow us to add:

* Clothing movement
* Grunt
* Ground impact
* Equipment rattle

later without redesigning the mechanic.

---

# 32. Camera Comfort

The camera movement should feel dramatic without being uncomfortable.

Avoid:

* Large violent screen shake
* Excessive roll
* Large forced aim changes
* Long camera bob
* Camera clipping into the ground

The camera should never descend below a safe prone eye height.

---

# 33. Testing Scenarios

Test the dolphin dive under the following conditions:

### Flat Ground

Sprint forward and trigger dive.

Expected:

```text
launch → short airborne arc → hard landing → prone
```

---

### Into a Wall

Sprint directly toward a wall and dive.

Expected:

* Player does not clip through wall.
* Forward motion stops appropriately.
* Player still resolves into a valid posture.

---

### Small Elevation Change

Dive across slightly uneven terrain.

Expected:

* Ground detection remains reliable.
* No infinite airborne state.

---

### Edge / Ledge

Dive near the edge of an elevated surface.

Expected:

* Player should continue falling naturally if no ground is beneath them.
* Do not force prone in midair.

Once ground is reached, transition into prone.

---

### Multiplayer

Observe another player dolphin diving.

Expected:

* Movement trajectory synchronizes correctly.
* Final prone position matches across clients.

---

# Acceptance Criteria

REQ-046 is complete when:

1. Sprinting and holding crouch initiates a true dolphin-dive state.
2. The player no longer visibly performs a normal crouch-slide before entering prone.
3. The player receives a short burst of forward movement.
4. The player receives a small upward launch.
5. The player becomes briefly airborne or follows a visually convincing airborne arc.
6. Gravity brings the player back toward the ground.
7. The first-person camera rises slightly when the dive begins.
8. The camera travels naturally with the diving player.
9. The camera drops rapidly to prone height when the player lands.
10. Landing can include a small configurable camera-impact effect.
11. Camera movement does not substantially alter the player's actual aim direction.
12. The player has limited or no movement steering during the dive.
13. The player cannot repeatedly initiate dives while already diving.
14. Dolphin diving requires sprinting.
15. Normal hold-crouch behavior still enters prone without a dolphin dive when the player is not sprinting.
16. Collision prevents diving through walls and solid geometry.
17. Landing is determined primarily through ground detection rather than only a timer.
18. The player enters the existing normal prone state after landing.
19. The existing prone collider, camera, and movement behavior continue functioning afterward.
20. Diving off a ledge does not force the player into a fake grounded state in midair.
21. Other multiplayer clients correctly observe the player's movement and final prone state.
22. The first-person camera effect remains local and is not unnecessarily network synchronized.
23. Movement parameters are configurable in the inspector.
24. Camera parameters are configurable in the inspector.
25. The mechanic remains compatible with adding a dedicated dolphin-dive animation later.
26. Existing sprint, crouch, prone, jump, firing, damage, and multiplayer systems continue functioning normally.

---

# Desired Result

The dolphin dive should feel like an aggressive emergency movement rather than a slide.

Current:

```text
RUN → crouch → slide → prone
```

Desired:

```text
RUN
 ↓
LAUNCH ↗
        →→→
            ↘
          CRASH
            ↓
          PRONE
```

From first person, the player should feel their viewpoint rise slightly as they throw themselves forward, travel through a short dive, and then rapidly crash down to ground level.

Even before a dedicated character animation is added, the movement and camera behavior should clearly communicate:

> "I just dove onto the ground."

rather than:

> "I crouched while moving forward."
