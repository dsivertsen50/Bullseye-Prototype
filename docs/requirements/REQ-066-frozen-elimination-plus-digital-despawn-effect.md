# REQ-066 — Frozen Elimination + Digital Despawn Effect

## Summary

Replace the current player death/elimination presentation with a stylized **5-second elimination sequence** designed to make eliminations feel less realistic and more like a player being removed from a simulation.

When a player's health/bullseye reaches the elimination threshold:

1. The player's mesh immediately **freezes in the exact pose and position it was in at the moment of elimination**.
2. The frozen player remains visible for **3 seconds**.
3. During those 3 seconds, the eliminated player's camera transitions out of first-person into a **controllable third-person view** around their frozen character.
4. At the 3-second mark, the frozen player begins a **digital disappearance/despawn effect**.
5. The digital effect lasts **2 seconds**.
6. At the end of the full **5-second elimination sequence**, the player respawns normally.

The existing 3-second respawn timer should therefore be replaced with a **5-second respawn cycle**.

This effect should avoid realistic death imagery, ragdolls, blood, or bodies collapsing on the ground. The intent is for elimination to feel more like being digitally removed from a simulated environment.

---

## Goals

- Create a distinctive elimination effect for Bullseye.
- Make eliminations feel stylized rather than realistic.
- Remove the need for realistic death/ragdoll behavior.
- Preserve interesting poses when players are eliminated.
- Give eliminated players something visually engaging to do during the respawn delay.
- Reinforce the idea that the match/game world could be interpreted as a simulation.
- Keep the entire elimination sequence synchronized in multiplayer.
- Increase the current respawn delay from 3 seconds to 5 seconds.

---

# 1. Elimination Trigger

The new sequence should begin whenever the existing gameplay system determines that a player has been eliminated.

This includes eliminations caused by:

- Bullseye damage
- Body damage that results in elimination
- Headshots
- Sniper rifle one-shot eliminations
- Grenades
- Rockets
- Ricochet shots
- Body slam/dolphin-dive eliminations
- Any future damage source using the standard elimination system

REQ-066 should **integrate with the existing health/elimination logic rather than creating a second death system**.

Once elimination is registered, gameplay control of that player stops immediately.

---

# 2. Freeze Player Pose

At the exact moment of elimination, capture the player's current visible pose.

The player's character should then freeze exactly where they were when eliminated.

Examples:

- Running player freezes mid-stride.
- Jumping player can remain suspended in the air.
- Dolphin-diving player can freeze halfway through the dive.
- Crouching player remains crouched.
- Prone player remains prone.
- Player firing a weapon can freeze during the recoil pose.
- Player standing on an unusual surface remains where they were.

The character should NOT:

- Collapse.
- Fall over.
- Enter a ragdoll.
- Automatically play a death animation.
- Drop to the floor.
- Continue falling because of gravity.
- Continue locomotion animations.

### Important

The frozen player should remain completely stationary even if the pose would normally be physically impossible.

For example:

> If a player is eliminated while jumping, their character can remain suspended several feet above the ground for the duration of the elimination sequence.

This is intentional and contributes to the "simulation frozen in time" visual style.

---

# 3. Animation Freeze

When the player is eliminated:

- Stop normal Animator progression.
- Preserve the exact visual pose at the moment of elimination.
- Prevent locomotion or weapon animations from changing the pose afterward.
- Prevent recoil, idle, reload, sprint, crouch transitions, etc. from continuing.

The implementation should avoid resetting the character to:

- T-pose
- Idle pose
- Default animation pose

The frozen pose should be a snapshot of the player's actual rendered pose at elimination.

---

# 4. Physics During Frozen State

During the elimination sequence, the frozen character should not respond to ordinary character physics.

Disable or suspend as necessary:

- Character movement
- Gravity
- CharacterController movement
- Rigidbody motion
- Knockback
- Grenade forces
- Magnetism forces
- Collision-based movement
- Player input

The frozen mesh should effectively become a stationary visual object until the digital despawn begins.

Other players should not be able to push the frozen character around.

---

# 5. Weapons During Frozen State

The player's currently visible world-view weapon should remain associated with the frozen pose.

For example:

- Rifle stays frozen in the player's hands.
- Sniper stays frozen wherever it was positioned.
- Rocket launcher remains frozen with the HeavyGun pose.
- Pistol remains frozen with the ShortGun pose.

The weapon should NOT continue firing, reloading, recoiling, or animating.

If the player was aiming/zooming at the time of elimination, gameplay zoom should end when the death camera begins.

---

# 6. Elimination Camera Transition

Immediately after elimination, transition the eliminated player's camera from first-person to a third-person view.

The transition should feel smooth rather than instantly teleporting the camera.

Target transition duration:

`0.25–0.75 seconds`

Expose the duration as a configurable value.

The camera should move outward so the player can clearly see their own frozen character.

Example:

```text
First-person camera
        ↓
camera pulls backward/outward
        ↓
third-person elimination camera
        ↓
frozen player visible in center of scene
```

---

# 7. Controllable Elimination Camera

For the first **3 seconds** after elimination, the eliminated player should be able to control the third-person camera.

The camera should primarily behave like an **orbit camera around the frozen player**.

Allow:

### Controller

Right Stick:
- Rotate camera horizontally.
- Rotate camera vertically.

### Mouse and Keyboard

Mouse:
- Rotate camera horizontally.
- Rotate camera vertically.

The camera should NOT allow the player to:

- Move the frozen character.
- Shoot.
- Aim.
- Reload.
- Throw grenades.
- Ping.
- Interact with pickups.
- Move around the map as a free-flying spectator.

The intended behavior is:

> "Look around my frozen elimination pose."

rather than:

> "Spectate the battlefield freely."

---

# 8. Camera Orbit Constraints

The third-person camera should orbit around the frozen player.

Suggested configurable variables:

```text
EliminationCameraDistance
EliminationCameraMinDistance
EliminationCameraMaxDistance
EliminationCameraSensitivity
EliminationCameraMinPitch
EliminationCameraMaxPitch
EliminationCameraTransitionDuration
```

The camera should prevent clipping through walls when practical.

Use existing camera collision logic if available.

The player should remain the focal point of the elimination camera.

---

# 9. Elimination Timeline

The entire sequence lasts **5 seconds**.

## Seconds 0–3: Frozen State

```text
0.0 sec
Player eliminated.

0.0–0.5 sec
Camera transitions from first-person to third-person.

0.0–3.0 sec
Player mesh remains completely frozen.
Player controls third-person orbit camera.
```

## Seconds 3–5: Digital Despawn

```text
3.0 sec
Digital disappearance effect begins.

3.0–5.0 sec
Player gradually disappears.

5.0 sec
Player is fully gone.
Respawn occurs.
```

---

# 10. Digital Despawn Effect

At exactly 3 seconds after elimination, begin a stylized digital disappearance effect.

The visual theme should resemble:

- Simulation de-rezzing
- Digital disintegration
- Pixel fragmentation
- Holographic breakdown
- Scan-line disappearance
- Glitching out of existence

The effect should NOT resemble:

- Blood
- Gore
- Burning flesh
- Dismemberment
- Realistic injury
- Realistic corpse destruction

A good conceptual reference is:

> The player's simulation instance is being deleted from the game world.

---

# 11. Initial Digital Effect Implementation

Create an implementation that can be visually improved later without rewriting the elimination system.

Preferred approach:

Use a shader/material-based dissolve effect if practical.

Potential visual progression:

```text
Normal frozen character
        ↓
digital noise appears across mesh
        ↓
small portions of mesh disappear
        ↓
effect spreads through character
        ↓
character completely dissolves
```

Optional supporting effects may include:

- Small digital particles.
- Pixel fragments.
- Horizontal glitch lines.
- Brief holographic flickering.
- Scan line moving through the body.
- Subtle electronic sound.
- Small light particles rising or falling from the dissolving character.

Keep the implementation modular so the exact visual effect can be replaced later.

---

# 12. Dissolve Direction

Expose the dissolve direction if practical.

Potential settings:

```text
BottomToTop
TopToBottom
Random
CenterOut
```

Default:

```text
BottomToTop
```

This may produce the clearest "digital deletion" effect.

---

# 13. Digital Effect Duration

Default:

```text
DigitalDespawnDuration = 2.0 seconds
```

Expose this value in the Inspector.

Changing this value should not require rewriting the underlying death system.

---

# 14. Frozen Duration

Default:

```text
FrozenEliminationDuration = 3.0 seconds
```

Expose this value in the Inspector.

---

# 15. Total Respawn Duration

Default:

```text
RespawnDelay = 5.0 seconds
```

The existing 3-second respawn timing should be updated.

The actual respawn should occur **after the digital disappearance has fully completed**.

Do not respawn the player while their previous character is still dissolving.

---

# 16. Respawn Countdown

Update any existing respawn countdown UI to use the new 5-second timing.

Suggested display:

```text
RESPAWNING IN 5
RESPAWNING IN 4
RESPAWNING IN 3
RESPAWNING IN 2
RESPAWNING IN 1
```

The countdown should remain synchronized with the elimination sequence.

Roughly:

```text
5 → Frozen
4 → Frozen
3 → Frozen
2 → Digital despawn
1 → Digital despawn
Respawn
```

Avoid creating a separate timer that can drift away from the actual respawn logic.

There should be one authoritative respawn timeline.

---

# 17. Digital Effect and Equipment

The digital despawn should affect the player's entire visible presentation.

This should include, where applicable:

- Player body mesh
- Clothing
- Bullseye decal
- Equipped weapon
- Relevant character accessories

They should disappear together rather than leaving a floating weapon behind.

If necessary, multiple renderers can receive the same dissolve progression.

---

# 18. Bullseye Behavior

If the bullseye is attached when the player is eliminated:

- Keep the bullseye visible during the frozen period.
- Include it in the digital disappearance.

If the bullseye is physically detached at the moment the player is eliminated:

- Do not suddenly teleport the detached bullseye onto the player's body.
- Allow the existing bullseye system to clean it up appropriately as part of the elimination/reset sequence.

Respawning should restore the player's normal bullseye state.

---

# 19. Multiplayer Synchronization

This feature must work correctly with Netcode for GameObjects.

All clients should agree on:

- Which player was eliminated.
- The player's elimination position.
- Player rotation.
- Frozen animation pose/state.
- Elimination start time.
- Start of digital effect.
- End of digital effect.
- Respawn timing.

Remote clients should see:

```text
Player freezes
        ↓
remains frozen for 3 seconds
        ↓
digitally disappears over 2 seconds
        ↓
player respawns
```

The eliminated player's controllable camera is **local-only** and does not need to be network synchronized.

---

# 20. Late/Network Timing Considerations

The elimination system should use an authoritative elimination timestamp/timeline rather than relying entirely on independent client coroutines.

Avoid situations where:

- One client sees the body disappear early.
- Another client still sees the frozen character.
- Player respawns before some clients finish the disappearance.
- Digital effect starts at different times for different clients.

Use the existing server-authoritative elimination/respawn architecture wherever possible.

---

# 21. Gameplay State During Elimination

The eliminated player must be considered dead immediately at `t = 0`.

They should not remain a valid gameplay target for the full five seconds.

The frozen visual representation should not:

- Take additional damage.
- Count additional hits.
- Trigger additional eliminations.
- Allow enemies to farm hit markers.
- Affect kill attribution.
- Trigger assists again.
- Interact with objectives as a living player.

Separate:

```text
Gameplay Player State
```

from:

```text
Elimination Visual State
```

The character is already eliminated even though their visual representation remains in the world temporarily.

---

# 22. Collision After Elimination

Preferably disable gameplay collision for the frozen character so frozen bodies do not become temporary obstacles.

Other players should be able to move normally through or around the eliminated visual without gameplay consequences.

If completely disabling collision causes visual/camera problems, use a dedicated non-gameplay collision layer.

---

# 23. Audio

Add hooks for elimination-effect audio.

Potential sequence:

### At elimination

Short:

```text
digital freeze / system interruption sound
```

### At digital despawn

Short:

```text
glitch / digital deletion / teleport-style sound
```

Avoid realistic injury sounds.

Audio clips themselves may be added later.

Expose AudioClip references rather than hardcoding assets.

---

# 24. Inspector Configuration

Create a centralized configuration component or extend the appropriate existing elimination configuration.

Suggested variables:

```csharp
float frozenDuration = 3f;
float dissolveDuration = 2f;
float cameraTransitionDuration = 0.5f;

float eliminationCameraDistance;
float eliminationCameraSensitivity;
float eliminationCameraMinPitch;
float eliminationCameraMaxPitch;

AudioClip freezeSound;
AudioClip digitalDespawnSound;

Material dissolveMaterial;
ParticleSystem digitalDespawnParticles;
```

Do not scatter important elimination timing constants across multiple scripts.

Ideally:

```text
Total Respawn Duration =
Frozen Duration + Digital Despawn Duration
```

rather than independently hardcoding `5`.

---

# 25. Architecture

Keep the elimination presentation modular.

Suggested conceptual structure:

```text
PlayerHealth / Damage System
        ↓
Player Eliminated
        ↓
EliminationController
        ├── Freeze Character
        ├── Disable Gameplay
        ├── Start Elimination Camera
        ├── Wait 3 seconds
        ├── Start Digital Despawn
        ├── Wait 2 seconds
        └── Trigger Existing Respawn System
```

The existing health, scoring, telemetry, and respawn systems should remain the authoritative sources for actual gameplay state.

REQ-066 should primarily control the **presentation of elimination**.

---

# 26. Compatibility With Existing Systems

Verify compatibility with:

- Player health
- Bullseye damage
- Bullseye detach/return
- Headshots
- Weapon damage
- Grenades
- Magnetism grenades
- Rocket launcher
- Dolphin dive/body slam
- Player animations
- Weapon world-view animations
- Player respawn
- Kill tracking
- Assist tracking
- Telemetry
- Scoreboard
- Local multiplayer testing
- Relay multiplayer
- Player identity/profile systems

Do not break existing elimination attribution or telemetry while replacing the visual death sequence.

---

# 27. No Ragdoll Requirement

Do NOT introduce a ragdoll system as part of REQ-066.

The intended visual identity is specifically:

```text
Impact
→ instant freeze
→ frozen simulation state
→ digital deletion
→ respawn
```

not:

```text
Impact
→ body falls over
→ corpse
→ respawn
```

---

# 28. Edge Cases

Test elimination while the player is:

- Standing
- Walking
- Sprinting
- Jumping
- Falling
- Crouching
- Going prone
- Already prone
- Dolphin diving
- Reloading
- Shooting
- Aiming
- Sniper zooming
- Holding a grenade
- Holding each weapon type
- Using a Heavy weapon
- On stairs
- On slopes
- In the air
- Against a wall
- Near another player
- Being affected by a grenade
- Being affected by magnetism
- Being hit by a rocket

The frozen pose should remain stable in each case.

---

# 29. Camera Edge Cases

Test the elimination camera:

- Near walls.
- In corners.
- Under low ceilings.
- While eliminated in the air.
- While prone.
- Against geometry.
- Near another player.
- During multiple simultaneous eliminations.

The camera should attempt to maintain a usable third-person view without excessive clipping.

---

# 30. Respawn Reset

When the player respawns:

Restore all normal systems, including:

- Player input.
- Character movement.
- Gravity.
- Character controller.
- Animator.
- Weapon functionality.
- Camera.
- First-person view.
- Health.
- Bullseye.
- Collision.
- Renderer state.
- Original materials.
- Any dissolve shader properties.

The newly spawned player must NOT inherit:

- Frozen animation state.
- Dissolve percentage.
- Disabled renderer state.
- Elimination camera.
- Disabled movement.
- Modified physics state.

---

# Acceptance Criteria

REQ-066 is complete when:

- [ ] Eliminating a player immediately stops their gameplay control.
- [ ] The player's mesh freezes in the exact visible pose present at elimination.
- [ ] The player does not collapse or ragdoll.
- [ ] Players eliminated in mid-air remain frozen in mid-air.
- [ ] The frozen character remains visible for approximately 3 seconds.
- [ ] The eliminated player's camera transitions to third-person.
- [ ] The eliminated player can rotate/orbit the camera around their frozen character.
- [ ] Camera control does not allow free spectator movement.
- [ ] The player cannot shoot, move, interact, or affect gameplay while eliminated.
- [ ] At approximately 3 seconds, a digital disappearance effect begins.
- [ ] The digital disappearance takes approximately 2 seconds.
- [ ] The effect contains no blood, gore, or realistic corpse behavior.
- [ ] The player and equipped weapon disappear together.
- [ ] The entire elimination sequence lasts approximately 5 seconds.
- [ ] Respawn occurs after the disappearance finishes.
- [ ] Existing respawn UI/countdown is updated from 3 seconds to 5 seconds.
- [ ] All clients see the same frozen position and elimination timing.
- [ ] The third-person elimination camera remains local to the eliminated player.
- [ ] Frozen players cannot receive additional damage or count as living players.
- [ ] Kill/assist/telemetry systems still record the elimination exactly once.
- [ ] Respawn completely restores animation, movement, rendering, camera, health, weapons, and bullseye state.
- [ ] The system functions in both local multiplayer testing and Relay multiplayer.

---

# Desired Player Experience

The player should experience elimination approximately like this:

```text
BANG

The player's character instantly freezes mid-action.

The camera smoothly pulls outside the body.

For several seconds, the player can rotate the camera and look at
their character suspended exactly where they were eliminated.

A subtle digital/glitch effect begins.

The character starts breaking apart into digital fragments.

The frozen character completely disappears.

RESPAWN.
```

The overall feeling should be:

**"You were removed from the simulation."**

rather than:

**"Your character died and left a corpse."**