# REQ-035 — Prone and Sprint Dolphin Dive

## Summary

Expand the existing crouch system to support two additional player movement states:

1. **Prone / Lay Down**

   * If the player holds the existing crouch input for a configurable amount of time, the player transitions from standing/crouching into a prone position.

2. **Dolphin Dive**

   * If the player is actively sprinting and holds the crouch input, the player performs a forward dolphin dive.
   * The dive should propel the player forward and downward.
   * Upon completing the dive/landing, the player should end in the prone state.

The existing normal crouch behavior should remain available through a short press/tap of the crouch button.

The resulting contextual control should feel approximately like:

```text
Tap Crouch
→ Crouch

Hold Crouch
→ Go Prone

Sprint + Hold Crouch
→ Dolphin Dive
→ Land Prone
```

All timings, speeds, and forces should be configurable through the Unity Inspector.

---

# 1. Preserve Existing Crouch Input

Do not introduce a new required input button for prone or dolphin diving.

Use the game's existing crouch action.

The input should now distinguish between:

```text
Short Press
Long Press
Long Press While Sprinting
```

The current keyboard and controller bindings for crouch should remain unchanged.

---

# 2. Crouch Tap Behavior

A normal short press of crouch should continue to perform the existing crouch behavior.

Example:

```text
Standing
   +
Quick Crouch Tap
   ↓
Crouched
```

The introduction of prone should not make ordinary crouching feel noticeably delayed.

Input logic should therefore detect the button duration without requiring the player to wait for the full prone timer before receiving appropriate crouch feedback.

Cursor should preserve the existing responsive crouch behavior as much as possible.

---

# 3. Hold to Go Prone

If the player continues holding the crouch button beyond a configurable threshold, transition the player into the prone state.

Suggested default:

```text
Prone Hold Duration: 0.6 seconds
```

Expose:

```csharp
[SerializeField] private float proneHoldDuration = 0.6f;
```

This value should be easily adjustable from the Inspector.

---

# 4. Prone State

Add an explicit player movement state representing prone.

Conceptually:

```text
Standing
Crouching
Prone
Sprinting
DolphinDiving
Airborne
Dead
```

The implementation does not need to use this exact enum if the current movement architecture uses another approach.

However, the code should clearly distinguish prone from ordinary crouching.

---

# 5. Prone Collider

When the player enters prone, reduce the player's collision/controller height so the gameplay collider reflects the lower body position.

For example:

```text
Standing

     O
    /|\
    / \
   [   ]
   [   ]
   [___]


Crouching

    O
   /|\
  _/ \_
 [_____]


Prone

 O______
/_______\
```

Do not replace the current CharacterController/capsule architecture.

Instead, adjust:

* CharacterController height
* CharacterController center

or the equivalent existing player collision settings.

---

# 6. Configurable Prone Height

Expose prone collision settings.

Example:

```csharp
[SerializeField] private float proneControllerHeight = 0.6f;
[SerializeField] private Vector3 proneControllerCenter;
```

Exact values should be tuned to the player mesh.

The current standing and crouching values should remain preserved and restored when leaving prone.

---

# 7. Prone Camera Height

Lower the first-person camera appropriately when the player enters prone.

The camera should move near ground level without clipping into the floor.

Use a smooth transition rather than teleporting the camera if practical.

Expose:

```text
Prone Camera Height
Prone Camera Transition Speed
```

The camera transition should feel responsive without being visually jarring.

---

# 8. External Player Visual

The external/full-body character should eventually visibly lie prone.

REQ-034 established the third-person humanoid animation architecture.

If an appropriate prone animation is available at the time REQ-035 is implemented, use it.

If one is not yet available:

* implement the gameplay state
* implement the collider
* implement the first-person camera position
* expose Animator parameters/triggers
* allow the external visual to use a temporary placeholder pose

Do not block the movement mechanic solely because finished animations are unavailable.

---

# 9. Animation Hooks

Prepare the third-person Animator for:

```text
IsProne
IsDolphinDiving
```

or equivalent parameters.

Potential configuration:

```csharp
Animator.SetBool("IsProne", true);
Animator.SetTrigger("DolphinDive");
```

The exact implementation should match the existing Animator architecture.

---

# 10. Prone Movement

Players should still be able to move while prone.

However, prone movement should be significantly slower than standing movement.

Suggested starting value:

```text
Prone Movement Speed:
~30–40% of normal walking speed
```

Expose:

```csharp
[SerializeField] private float proneMoveSpeed;
```

This should be tuned later through gameplay testing.

---

# 11. No Sprinting While Prone

Players cannot sprint while prone.

If the sprint input is pressed while prone:

```text
Do not sprint.
```

The player must leave prone before sprinting again.

---

# 12. No Jumping While Prone

Players should not immediately jump directly from prone.

If jump is pressed while prone, choose one of these behaviors based on compatibility with the current controller:

### Preferred

Jump input causes the player to transition out of prone first.

The player does not jump until they are in an appropriate standing/crouched state.

### Acceptable Prototype Alternative

Ignore jump while prone.

Do not allow prone players to launch upward while still using the prone collider.

---

# 13. Leaving Prone

Pressing the crouch button while already prone should transition the player out of prone.

Preferred behavior:

```text
Prone
+
Crouch Press
↓
Crouching
```

The player may then stand normally through the existing crouch system.

This creates:

```text
Standing
↓ crouch
Crouching
↓ hold
Prone
↓ crouch
Crouching
↓ uncrouch
Standing
```

---

# 14. Stand-Up Clearance Check

Before increasing the collider from prone to crouched or standing, verify that sufficient space exists above the player.

The player should not be able to:

* stand through ceilings
* clip through low geometry
* expand their collider inside another object

Reuse the existing crouch clearance-check architecture if available.

---

# 15. Dolphin Dive Trigger

A dolphin dive should become available when:

```text
Player is sprinting
AND
Player holds the crouch button
```

The dive should not trigger from normal walking.

Recommended condition:

```text
IsSprinting == true
AND
CurrentSpeed >= MinimumDiveSpeed
AND
CrouchHeld >= DiveHoldDuration
```

---

# 16. Dolphin Dive Hold Duration

Expose a separate configurable threshold.

Suggested starting value:

```text
Dive Hold Duration: 0.3 seconds
```

Example:

```csharp
[SerializeField] private float dolphinDiveHoldDuration = 0.3f;
```

The dive threshold may be shorter than the normal prone threshold because the player's sprint state already signals intentional movement.

---

# 17. Minimum Dive Speed

Prevent a dive from triggering merely because the sprint button is technically active while the player is barely moving.

Expose:

```csharp
[SerializeField] private float minimumDolphinDiveSpeed;
```

The player should need meaningful forward sprint velocity.

---

# 18. Dolphin Dive Direction

The dive should primarily move the player in their current horizontal facing/movement direction.

Preferred:

```text
Player Forward Direction
        ↓
   Dolphin Dive
```

The dive should not use camera pitch directly.

Looking sharply upward should not launch the character into the sky.

Calculate dive direction primarily from the flattened horizontal player-forward or movement vector.

---

# 19. Dolphin Dive Movement

When triggered, the player should receive a strong temporary forward impulse.

Conceptually:

```text
Sprint
──────────────►

Hold Crouch

      O
     /|\  ───────►
     / \

        ↓

   O────────►
  /|\

        ↓

  O________
 /_________\
      Prone
```

The player should visibly leave ordinary locomotion for a brief committed dive.

---

# 20. Configurable Dive Force

Expose tuning values such as:

```csharp
[SerializeField] private float dolphinDiveForwardForce;
[SerializeField] private float dolphinDiveUpwardForce;
[SerializeField] private float dolphinDiveDuration;
```

Suggested initial intent:

* strong forward momentum
* small upward component
* gravity brings player back down
* total dive lasts well under one second before landing/recovery

Exact values should be tuned in Unity.

---

# 21. Do Not Implement Dive as Teleportation

The dolphin dive should physically move through space.

Do not simply move the player instantly from:

```text
Point A
```

to:

```text
Point B
```

The player should travel continuously so:

* opponents can track them
* collisions remain meaningful
* multiplayer synchronization remains sensible

---

# 22. Dive Commitment

Once the dolphin dive begins, normal movement control should be temporarily reduced.

The player should not be able to instantly change directions in mid-dive.

During the initial dive:

* normal movement input should have little or no effect
* sprint input should no longer accelerate the player
* another dive cannot begin
* crouch cannot restart the action

This makes diving a committed tactical maneuver rather than a directional dash.

---

# 23. Limited Mid-Air Steering

If some steering is necessary for controller feel, expose a small configurable amount.

Example:

```csharp
[SerializeField] private float dolphinDiveAirControl = 0.1f;
```

Default should be low.

Players should not be able to sharply turn a dolphin dive after launching.

---

# 24. Dolphin Dive Landing

When the diving player reaches the ground:

```text
DolphinDiving
↓
Prone
```

The player should automatically enter the normal prone state.

Do not return directly to standing.

This is an important part of the mechanic.

---

# 25. Dive Recovery

After landing, introduce a brief configurable recovery period before full prone movement/control resumes.

Suggested starting value:

```text
Dive Recovery Duration: 0.25–0.4 seconds
```

Expose:

```csharp
[SerializeField] private float dolphinDiveRecoveryDuration;
```

This prevents repetitive dive spam and gives the movement weight.

---

# 26. Dive Cooldown

Add a configurable cooldown preventing immediate repeated dolphin dives.

Suggested:

```text
Dolphin Dive Cooldown: 1.0 second
```

Example:

```csharp
[SerializeField] private float dolphinDiveCooldown = 1f;
```

This value can be tuned later.

---

# 27. Sprint Resource Compatibility

If sprinting currently has a limited duration/stamina-like timer, dolphin diving must respect that existing system.

Diving should not:

* reset sprint duration
* create infinite sprint
* bypass sprint restrictions

If appropriate, initiating a dolphin dive may immediately end the sprint state.

---

# 28. Weapons During Prone

Players should generally retain normal combat capability while prone.

Allow:

* aiming
* firing
* reloading
* switching weapons

unless an existing weapon mechanic creates a conflict.

Prone should eventually become a legitimate combat stance rather than a non-combat state.

---

# 29. Weapon Presentation While Prone

The first-person weapon position may eventually need adjustment while prone.

For the initial implementation:

* preserve existing FPS weapon behavior
* ensure the weapon does not severely clip through the ground
* expose future animation hooks as appropriate

Detailed prone weapon animations can be handled later.

---

# 30. Weapons During Dolphin Dive

During the active dolphin dive, prevent firing.

Preferred:

```text
Dive Begins
↓
Weapon fire temporarily disabled
↓
Player lands
↓
Short recovery
↓
Weapon fire enabled
```

The player may regain weapon use once the dive recovery period is complete.

This prevents players from perfectly shooting while performing the committed movement.

---

# 31. Reload During Dive

If the player is currently reloading and initiates a dolphin dive, use the existing weapon system's safest behavior.

Preferred:

```text
Cancel or interrupt reload if supported.
```

If the existing system does not support reload cancellation cleanly, allow the underlying reload timer to finish but suppress problematic visual behavior.

Do not create ammo duplication or reload exploits.

---

# 32. Grenades During Dive

Do not allow the player to initiate a grenade throw during the active dolphin dive.

After landing and recovery:

```text
Grenade input restored.
```

---

# 33. First-Person Dolphin Dive Presentation

The local player's internal view should make the dive feel powerful.

Eventually, the FPS presentation may include:

* camera lowering
* forward camera movement
* slight camera tilt
* weapon lowering
* arm movement
* landing impact
* subtle camera shake

REQ-035 should at minimum support appropriate camera movement and animation hooks.

Do not create extreme camera rotation that makes players nauseated.

---

# 34. External Dolphin Dive Presentation

Other players should eventually see a full-body diving animation.

Expected visual:

```text
Sprint
↓
Character launches forward
↓
Body extends horizontally
↓
Character lands
↓
Character remains prone
```

If no polished animation exists yet, the gameplay motion should still be implemented with an Animator trigger available for a later animation asset.

---

# 35. Third-Person Animation Parameters

Prepare parameters such as:

```text
IsProne
IsDolphinDiving
DiveTrigger
ProneMoveSpeed
```

Adapt these names to existing conventions if necessary.

---

# 36. Multiplayer Authority

The server/host should remain authoritative over the player's movement state as appropriate for the current Netcode architecture.

Remote clients must be able to observe:

* entering prone
* leaving prone
* dolphin dive start
* dive movement
* landing
* resulting prone state

Do not implement the dive purely as an owner-only visual effect.

---

# 37. Network Movement

The actual movement created by a dolphin dive should use or remain compatible with the project's existing networked player movement system.

Do not create an unrelated second movement synchronization system specifically for dives.

---

# 38. Network Animation

Only the movement/gameplay state needs synchronization.

Do not synchronize individual bones.

Preferred:

```text
Player State = DolphinDiving
        ↓
Remote Animator
        ↓
Play Dolphin Dive Animation
```

---

# 39. Bullseye Interaction While Prone

The bullseye remains active while the player is prone.

The existing bullseye system should continue functioning.

However, the player body's new orientation means bullseye placement may eventually need refinement.

For REQ-035:

* preserve gameplay functionality
* ensure the bullseye remains associated with the player
* do not redesign the complete humanoid bullseye traversal system

---

# 40. Bullseye Movement and Posture

Where technically feasible, the bullseye's relative position should follow the new player posture.

For example:

```text
Standing:
Bullseye appears on standing body.

Prone:
Bullseye remains associated with the lowered body rather than floating at standing height.
```

Use the body anchors introduced/prepared during the character rigging work where useful.

---

# 41. Bullseye During Dolphin Dive

The bullseye should travel with the player's body during the dive.

A player should remain vulnerable while diving.

Dolphin diving must not:

* disable the bullseye
* grant invulnerability
* temporarily ignore damage

---

# 42. Damage During Dive

The player can still take damage during a dolphin dive.

If lethal damage occurs during the dive:

```text
Dive stops
↓
Existing death state begins
↓
REQ-032 bullseye shatter/death presentation runs
```

Death always takes priority over dive movement.

---

# 43. Grenade Knockback Compatibility

If the player is hit by a grenade while prone or diving, preserve existing grenade gameplay as much as practical.

Death or authoritative knockback should take precedence over cosmetic animation state.

Do not allow the player to become permanently stuck in:

```text
IsDolphinDiving = true
```

after a grenade interaction.

---

# 44. Collision During Dive

The player must continue to collide with world geometry while diving.

The dive must not permit:

* moving through walls
* passing through closed doors
* clipping through the floor
* bypassing map collision

Use the existing CharacterController/collision system.

---

# 45. Low Obstacles

The prone collider should eventually allow players to use spaces that are too low for standing characters when the map geometry supports it.

For example:

```text
Low opening

──────────────
     space
 O__________►
──────────────
```

However, level design exploitation should be monitored during testing.

---

# 46. Dive Into Obstacles

If the player dolphin dives directly into a wall or obstacle:

* collision should stop forward movement naturally
* the player should not clip through the obstacle
* the player should transition safely into prone/appropriate recovery

Do not continue applying force indefinitely against the wall.

---

# 47. Dive Off Ledges

A player may dolphin dive off a ledge.

In this case:

```text
Dive
↓
Airborne / falling
↓
Land
↓
Prone
```

Do not force the player into prone while still suspended in the air.

The dive state can transition into an airborne/falling phase until ground contact occurs.

---

# 48. Fall Safety

Do not change existing fall behavior or introduce fall damage as part of REQ-035.

If fall damage does not currently exist, it remains outside scope.

---

# 49. State Priority

Movement states should have a clear priority.

Suggested conceptual priority:

```text
Dead
↓
Dolphin Diving
↓
Airborne
↓
Prone
↓
Crouching
↓
Sprinting
↓
Standing/Walking
```

This is illustrative.

Cursor should adapt to the existing player movement architecture.

The important requirement is preventing contradictory simultaneous states such as:

```text
IsProne = true
AND
IsSprinting = true
```

---

# 50. Suggested Inspector Settings

Expose at minimum:

```text
PRONE
-----
Prone Hold Duration
Prone Move Speed
Prone Controller Height
Prone Controller Center
Prone Camera Height
Prone Transition Speed

DOLPHIN DIVE
-------------
Dolphin Dive Hold Duration
Minimum Dive Speed
Dive Forward Force
Dive Upward Force
Dive Duration
Dive Air Control
Dive Recovery Duration
Dive Cooldown
```

These values should be tunable without editing source code.

---

# 51. Debugging

Optional temporary debugging should make the new movement states easy to validate.

Example logs:

```text
[Movement] Entered crouch.
[Movement] Crouch held 0.61s — entering prone.
[Movement] Exited prone.
[Movement] Sprint + crouch hold detected — dolphin dive.
[Movement] Dolphin dive launched.
[Movement] Dolphin dive landed — entering prone.
```

Debug output should be removable or disableable.

---

# 52. Required Test Cases

Test at minimum:

### Standing + Tap Crouch

Expected:

```text
Standing → Crouched
```

### Standing + Hold Crouch

Expected:

```text
Standing → Crouched → Prone
```

### Crouched + Hold Crouch

Expected:

```text
Crouched → Prone
```

### Prone + Crouch

Expected:

```text
Prone → Crouched
```

### Sprint + Tap Crouch

Existing behavior should remain sensible and should not accidentally cause a dive if the hold threshold is not reached.

### Sprint + Hold Crouch

Expected:

```text
Sprint → Dolphin Dive → Prone
```

### Dive Into Wall

Expected:

No clipping.

### Dive Off Ledge

Expected:

Dive/fall → land → prone.

### Die During Dive

Expected:

Dive terminates and normal death process occurs.

### Multiplayer

Host and client can each:

* crouch
* go prone
* exit prone
* dolphin dive

and each player sees the other's correct state.

---

# Acceptance Criteria

REQ-035 is complete when:

* [ ] Existing crouch input remains the only required posture input.
* [ ] A short crouch press continues to produce normal crouching.
* [ ] Holding crouch for a configurable duration enters prone.
* [ ] Prone hold duration is Inspector-configurable.
* [ ] Player collider height changes appropriately while prone.
* [ ] Player camera lowers appropriately while prone.
* [ ] Prone movement works.
* [ ] Prone movement is slower than normal walking.
* [ ] Prone movement speed is configurable.
* [ ] Sprinting is unavailable while prone.
* [ ] Player cannot incorrectly jump using the prone collider.
* [ ] Player can exit prone.
* [ ] Clearance is checked before increasing collider height.
* [ ] Sprint + crouch hold triggers dolphin dive.
* [ ] Dolphin dive hold duration is configurable.
* [ ] A minimum sprint/movement speed is required for a dive.
* [ ] Dolphin dive propels the player forward.
* [ ] Dolphin dive includes configurable forward force/speed.
* [ ] Dolphin dive may include a configurable small upward force.
* [ ] Normal steering is reduced during the dive.
* [ ] Player continues to collide with world geometry during the dive.
* [ ] Player cannot dive through walls.
* [ ] Diving off a ledge behaves correctly.
* [ ] Landing from a dolphin dive places the player into prone.
* [ ] A configurable recovery period occurs after landing.
* [ ] A configurable dive cooldown prevents immediate dive spam.
* [ ] Shooting is disabled during the active dive/recovery.
* [ ] Grenade throwing is disabled during the active dive/recovery.
* [ ] Normal combat capability returns after recovery.
* [ ] Normal firing/aiming is available while simply prone.
* [ ] The player remains damageable while prone and diving.
* [ ] The bullseye remains active while prone and diving.
* [ ] Lethal damage interrupts the dive and triggers the existing death system.
* [ ] Third-person animation hooks exist for prone.
* [ ] Third-person animation hooks exist for dolphin diving.
* [ ] First-person presentation can have separate prone/dive animation behavior.
* [ ] Host can go prone and dolphin dive.
* [ ] Client can go prone and dolphin dive.
* [ ] Remote players see the posture/movement state correctly.
* [ ] Player returns to a valid movement state after respawn.
* [ ] No movement-state conflicts leave the player stuck.
* [ ] Existing sprint, jump, crouch, weapons, health, grenades, bullseye, death, and respawn systems continue functioning.

---

# Out of Scope

The following are not required for REQ-035:

* final production-quality prone animation
* final production-quality dolphin dive animation
* custom mocap
* detailed prone reload animations
* prone-specific recoil tuning
* prone accuracy bonuses
* prone damage bonuses
* stamina costs specifically for diving
* fall damage
* sliding
* combat rolls
* wall diving
* directional side dives
* backward dives
* parkour
* vaulting
* crawling through custom map geometry
* redesigning the player's hitbox system
* redesigning bullseye surface traversal

These can be considered in future requirements.

---

# Intended Player Experience

The crouch button should now provide a simple but expressive posture system:

```text
           CROUCH INPUT

              │
       ┌──────┴──────┐
       │             │
      TAP           HOLD
       │             │
       ▼             ▼
    CROUCH     Is Sprinting?
                    │
             ┌──────┴──────┐
             │             │
            NO            YES
             │             │
             ▼             ▼
           PRONE      DOLPHIN DIVE
                           │
                           ▼
                         PRONE
```

This should give players three useful movement choices without adding another control:

**crouch for cover, prone for a very low defensive position, and dolphin dive for a risky but fast transition from sprinting into cover.**
