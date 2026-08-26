# REQ-032 — Bullseye Shatter and Player Death Presentation

## Summary

Improve the visual and audio presentation of player death by causing the player's bullseye to physically shatter when the player is killed.

When a player's health reaches zero, the bullseye should break apart into visible fragments, one glass-breaking sound should be randomly selected from a configurable collection of sound effects, and the killed player should temporarily freeze in their exact death pose.

After a short configurable delay, the defeated player's body should disappear and the existing respawn sequence should proceed.

This system should build on the existing health, death, bullseye, networking, and respawn systems rather than replacing them.

---

## Goals

Implement the following death sequence:

1. Player receives lethal damage.
2. Player movement and combat immediately stop.
3. Player remains visually frozen in the position and orientation in which they died.
4. The player's intact bullseye disappears or is disabled.
5. A shattered version of the bullseye appears at exactly the same location and orientation.
6. Bullseye fragments physically burst apart.
7. One randomly selected glass-breaking sound effect plays.
8. The dead player remains visible briefly as a frozen body while the bullseye fragments fall away.
9. The player's body disappears.
10. The existing respawn countdown and respawn system continue.
11. Player respawns normally with a restored intact bullseye.

The effect should be visible to all connected players.

---

# 1. Bullseye Shatter Effect

When the bullseye is destroyed as part of player death, it should visually break apart rather than simply disappearing.

The recommended implementation is a **pre-fractured bullseye prefab**.

The normal bullseye should remain a single object during gameplay for performance and gameplay simplicity.

A separate shattered version should contain multiple individual pieces representing fragments of the bullseye.

Example hierarchy:

```text
Bullseye_Shattered
├── Fragment_01
├── Fragment_02
├── Fragment_03
├── Fragment_04
├── Fragment_05
├── Fragment_06
├── Fragment_07
└── etc.
```

Each fragment should be capable of moving independently when the shatter effect occurs.

Each fragment should preferably contain:

* Mesh Renderer
* Mesh Filter
* Rigidbody
* Collider

The exact number of fragments is not important for the initial implementation.

Approximately **8–20 fragments** should be sufficient for the prototype.

The system should avoid requiring real-time mesh destruction or runtime procedural fracturing.

---

# 2. Shattered Bullseye Prefab

Create or support a configurable shattered-bullseye prefab.

Suggested inspector field:

```csharp
[SerializeField] private GameObject shatteredBullseyePrefab;
```

The prefab should be spawned at the precise:

* position
* rotation
* approximate scale

of the player's intact bullseye at the moment of death.

The intact bullseye should then be hidden or disabled.

The shattered prefab should visually resemble the existing bullseye closely enough that the transition from intact bullseye to shattered pieces appears instantaneous.

---

# 3. Fragment Physics

Each bullseye fragment should use Rigidbody physics.

When the shatter occurs, fragments should receive outward force so that they visibly separate rather than simply falling straight down.

The force should generally originate from the center of the bullseye.

Possible implementation:

```csharp
fragmentRigidbody.AddExplosionForce(
    shatterForce,
    bullseyeCenter,
    shatterRadius,
    upwardModifier
);
```

Expose tuning values through the Inspector.

Suggested fields:

```csharp
[SerializeField] private float shatterForce = 2.5f;
[SerializeField] private float shatterRadius = 1.0f;
[SerializeField] private float upwardModifier = 0.2f;
[SerializeField] private float fragmentLifetime = 4.0f;
```

The exact values should be easy to tune.

Fragments should:

* collide with the environment if practical
* respond to gravity
* tumble naturally
* eventually disappear

The fragments should not remain permanently in the scene.

---

# 4. Fragment Cleanup

Shattered bullseye fragments should automatically be removed after a configurable period.

Suggested default:

```text
Fragment Lifetime: 3–5 seconds
```

The cleanup system should prevent repeated deaths from leaving large numbers of physics objects in the level.

For the prototype, destroying the fragment objects after the timer is acceptable.

Object pooling may be considered later if performance becomes an issue.

---

# 5. Glass Breaking Sound Effect

A glass-breaking or brittle-shattering sound should play when the bullseye breaks.

The system must support **multiple possible sound effects** rather than a single hard-coded clip.

Expose an AudioClip array in the Inspector.

Example:

```csharp
[SerializeField] private AudioClip[] bullseyeBreakSounds;
```

The developer should be able to drag sound files directly into this array from the Unity Project window.

Example Inspector configuration:

```text
Bullseye Break Sounds
    Size: 4

    Element 0: GlassBreak_01
    Element 1: GlassBreak_02
    Element 2: GlassBreak_03
    Element 3: GlassBreak_04
```

Whenever a bullseye shatters, randomly select one valid clip.

Example behavior:

```csharp
AudioClip selectedClip =
    bullseyeBreakSounds[
        Random.Range(0, bullseyeBreakSounds.Length)
    ];
```

The system should safely handle:

* an empty array
* missing AudioClips
* missing AudioSource

without generating exceptions.

---

# 6. Positional Death Audio

The glass-breaking sound should preferably be played as positional 3D audio from the location where the player died.

This allows other players to hear where a death occurred.

The dying player should also hear the sound appropriately.

The sound should not be attached to an object that is immediately destroyed before the clip finishes.

Possible solutions include:

* `AudioSource.PlayClipAtPoint()`
* temporary audio object
* existing reusable world audio system

Use whichever approach best matches the existing project architecture.

---

# 7. Freeze the Player on Death

When lethal damage occurs, the player's character should immediately stop moving.

The player should remain frozen in the exact:

* position
* rotation
* stance

that existed at the moment of death.

For example, if a player dies:

* while crouching, they remain crouched
* while facing sideways, they remain facing sideways
* while airborne, they visually remain where they were killed until the death disappearance begins

The goal is a brief **freeze-frame / statue-like death effect**.

---

# 8. Disable Player Control

Immediately upon death, the killed player's gameplay input should be disabled.

The player should not be able to:

* walk
* sprint
* jump
* crouch
* rotate their character
* fire
* reload
* throw grenades
* switch weapons
* interact with pickups

Camera behavior may remain under the control of the existing death/respawn implementation unless changing it is necessary to prevent unintended movement.

Gameplay authority should recognize the player as dead during this entire sequence.

---

# 9. Freeze Physics

The player's movement physics should also stop.

The implementation should prevent:

* gravity moving the dead player
* momentum sliding the player
* knockback moving the dead player
* additional explosions pushing the player
* CharacterController movement continuing after death

Depending on the current player architecture, this may involve temporarily disabling the movement controller, CharacterController, Rigidbody movement, or related systems.

The frozen state should not permanently alter the player object's physics configuration.

Normal behavior must be restored when the player respawns.

---

# 10. Additional Damage After Death

Once the player has entered the death state, additional hits should have no gameplay effect.

The dead player should not:

* take additional damage
* trigger additional bullseye breaks
* play additional death sounds
* increment kill counts multiple times
* trigger multiple respawn sequences

The existing authoritative death state should guard against duplicate death events.

---

# 11. Freeze Duration

Expose a configurable delay controlling how long the dead player's frozen body remains visible.

Suggested default:

```text
Death Freeze Duration: 1.0–1.5 seconds
```

Example Inspector field:

```csharp
[SerializeField] private float deathFreezeDuration = 1.25f;
```

The duration should be independently adjustable from the total respawn delay where possible.

---

# 12. Player Disappearance

After the freeze duration expires, the dead player's visible body should disappear.

This should include the relevant player visuals such as:

* player mesh/capsule
* held weapon
* bullseye
* health-related world UI
* other visual attachments that should not remain during respawn

The player's network object does **not necessarily need to be despawned** if the current respawn system already reuses the same network player object.

Prefer hiding or disabling visual/gameplay components if that integrates more cleanly with the current architecture.

Do not break the existing multiplayer player ownership system.

---

# 13. Shattered Pieces May Remain Briefly

The player's body and the shattered bullseye do not need to disappear simultaneously.

Preferred visual sequence:

```text
0.00 sec
Player dies

0.00 sec
Movement freezes
Bullseye shatters
Glass sound plays

0.00–1.25 sec
Frozen player remains visible
Fragments fly/fall around player

~1.25 sec
Player body disappears

~3–5 sec
Remaining bullseye fragments disappear
```

This allows the shatter effect to remain visible briefly after the body vanishes.

---

# 14. Existing Respawn Countdown

REQ-032 should integrate with the existing respawn countdown system.

Do not create a second independent respawn system.

The player's existing death UI/countdown should continue to function.

If the current system uses a three-second respawn countdown, the visual death sequence should occur inside that overall process.

Example:

```text
Death
↓
Bullseye shatters
↓
Player frozen
↓
Body disappears
↓
Existing countdown completes
↓
Respawn
```

The precise overlap between the freeze animation and countdown can be determined according to the existing architecture.

---

# 15. Respawn Restoration

When the player respawns:

* movement must be restored
* input must be restored
* weapon use must be restored
* physics must behave normally
* player visuals must be visible
* intact bullseye must be restored
* shattered bullseye must not remain attached
* health must reset according to the existing health system
* bullseye mechanics must resume normally

The player should not remain in any part of the temporary frozen death state.

---

# 16. Multiplayer / Networking Requirements

The death presentation must work correctly in multiplayer.

When Player A kills Player B:

### Player A should see:

* Player B freeze
* Player B's bullseye shatter
* fragments burst outward
* Player B disappear
* Player B eventually respawn

### Player B should see:

* their death state begin
* bullseye break effects where applicable
* glass-breaking audio
* existing respawn UI/countdown
* eventual respawn

### Other players should see:

* the same death presentation for Player B

The authoritative player death event should occur only once.

---

# 17. Network Architecture

The server/host should remain authoritative over:

* whether the player is dead
* kill attribution
* health reaching zero
* disabling gameplay
* respawn timing
* respawn position

The bullseye shatter itself is primarily a visual effect.

Where practical, the network should send a single death/shatter event to clients, and each client may locally simulate the fragment Rigidbody physics.

It is **not necessary to continuously network-sync every bullseye fragment** unless the current project architecture makes that easy.

This avoids unnecessary network traffic for cosmetic physics.

All clients should start the effect from approximately the same bullseye position and rotation.

---

# 18. Separation From Existing Grenade Bullseye Dislodgement

The game already allows grenades to potentially knock the bullseye away from the player.

REQ-032 must distinguish between:

### Bullseye Dislodgement

Player is still alive.

The bullseye:

* becomes detached
* may expose the player
* eventually returns according to the existing mechanic

### Bullseye Destruction / Player Death

Player has been killed.

The bullseye:

* permanently shatters for that life
* does not return
* is restored only when the player respawns

A grenade knocking the bullseye loose must **not automatically trigger the death shatter effect** unless that grenade also kills the player.

---

# 19. Bullseye Position When Already Dislodged

If the player's bullseye is currently detached when the player dies, the shatter effect should occur at the bullseye's **current world position**, not necessarily at the player's body.

Example:

```text
Player body     Bullseye
    O               ◎
   /|\      → grenade has knocked target away
   / \

Player dies

    O          * * *
   /|\       * shattered *
   / \          pieces
```

This preserves the physical relationship established by the grenade system.

If implementing this immediately is impractical, shattering at the player's bullseye anchor is acceptable as a temporary prototype fallback, but the preferred implementation is to shatter at the actual bullseye location.

---

# 20. Suggested Component

A dedicated component may be created to manage the effect.

Possible name:

```text
BullseyeShatterController
```

Potential responsibilities:

```csharp
public class BullseyeShatterController : MonoBehaviour
{
    [Header("Shatter")]
    [SerializeField] private GameObject shatteredBullseyePrefab;
    [SerializeField] private float shatterForce = 2.5f;
    [SerializeField] private float shatterRadius = 1f;
    [SerializeField] private float upwardModifier = 0.2f;
    [SerializeField] private float fragmentLifetime = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] bullseyeBreakSounds;
    [SerializeField] private float breakVolume = 1f;

    public void ShatterBullseye()
    {
        // Spawn fractured prefab.
        // Hide intact bullseye.
        // Apply fragment forces.
        // Play randomly selected sound.
        // Clean fragments up later.
    }
}
```

This is illustrative rather than a required exact implementation.

Cursor should adapt the implementation to the project's existing architecture.

---

# 21. Suggested Death-State Flow

Conceptually:

```csharp
HandlePlayerDeath()
{
    if (isDead)
        return;

    isDead = true;

    DisableGameplay();

    FreezePlayer();

    bullseyeShatterController.ShatterBullseye();

    StartDeathPresentation();
}
```

Then:

```csharp
DeathPresentation()
{
    Wait(deathFreezeDuration);

    HidePlayerVisuals();

    ContinueExistingRespawnSequence();
}
```

Again, reuse existing health/death/respawn systems wherever possible rather than duplicating their logic.

---

# 22. Inspector Configuration

The relevant death/shatter component should expose the primary effect settings through the Unity Inspector.

At minimum:

```text
Shatter Settings
-----------------
Shattered Bullseye Prefab
Shatter Force
Shatter Radius
Upward Modifier
Fragment Lifetime

Death Presentation
------------------
Death Freeze Duration

Audio
-----
Bullseye Break Sounds [Array]
Break Volume
```

This is important because the developers should be able to tune the effect without modifying code.

---

# 23. Art / Prefab Placeholder Support

The final bullseye model is not yet finalized.

Therefore, the implementation should not tightly depend on the current bullseye mesh.

The fractured bullseye should be referenced through a configurable prefab so that it can later be replaced with a final art asset.

The existing placeholder bullseye may be manually divided into several pieces for testing.

Future art can replace both:

```text
Intact Bullseye Prefab
```

and:

```text
Shattered Bullseye Prefab
```

without rewriting the death system.

---

# 24. Sound Asset Placeholder Support

Do not hard-code specific sound files.

The system should simply provide an Inspector array where sound effects can be dragged in later.

If no sounds are assigned, gameplay should continue normally without throwing an exception.

---

# 25. Performance

Bullseye fragments are cosmetic.

They should therefore:

* exist for only a few seconds
* avoid NetworkTransform unless necessary
* not participate in gameplay damage
* not affect player collision significantly
* not block bullets
* not interfere with spawn logic

If fragment colliders cause gameplay problems, they may use a dedicated non-gameplay physics layer.

---

# 26. Death Camera Compatibility

Do not substantially redesign the player's death camera as part of REQ-032.

The existing behavior of leaving the player's view at the location where they died should remain unless minor adjustments are required for compatibility.

The player should ideally be able to see at least part of the shatter effect from their death camera when circumstances allow.

A dedicated killcam or third-person death camera is outside the scope of this requirement.

---

# 27. Kill Tracking Compatibility

REQ-032 must remain compatible with the kill-tracking system established in REQ-029.

The shatter sequence is a visual consequence of death and must not independently award kills.

Only the existing authoritative death/kill system should determine:

* who died
* who receives the kill
* when the death is counted

The visual effect should listen to or be triggered by that confirmed death event.

---

# 28. Edge Cases

The implementation should safely handle:

### Empty audio array

Bullseye shatters without sound.

### Missing shattered prefab

Player death still proceeds normally.

Log a useful warning if appropriate.

### Multiple lethal hits in the same frame

Only one death sequence occurs.

### Player disconnects during death sequence

Objects are cleaned up appropriately.

### Host dies

The same effect occurs without interrupting networking.

### Client dies

All clients receive the appropriate visual state.

### Bullseye is detached when death occurs

Use its current position if possible.

### Player dies during jump

Player freezes temporarily rather than continuing to fall during the freeze-frame portion.

---

# 29. Debugging Support

Optional debug logging may be added during implementation.

Examples:

```text
[Death] Player 2 entered death state.
[Bullseye] Shatter triggered at (x, y, z).
[DeathAudio] Playing GlassBreak_03.
[Death] Hiding Player 2 visuals.
[Respawn] Restoring Player 2.
```

Debug logs should be easy to disable or remove after validation.

---

# Acceptance Criteria

REQ-032 is complete when all of the following are true:

* [ ] Lethal damage triggers the bullseye shatter effect.
* [ ] The intact bullseye disappears when shattered.
* [ ] A fractured/shattered bullseye appears at the correct location.
* [ ] Multiple bullseye fragments visibly separate.
* [ ] Fragments use Rigidbody physics.
* [ ] Fragments fall/tumble naturally.
* [ ] Fragments automatically clean themselves up.
* [ ] An Inspector-configurable array accepts multiple glass-breaking sounds.
* [ ] One sound is randomly selected whenever the bullseye breaks.
* [ ] Missing audio clips do not cause errors.
* [ ] The killed player immediately loses movement control.
* [ ] The killed player cannot fire, reload, switch weapons, throw grenades, or otherwise act.
* [ ] The player's body temporarily freezes in the position in which they died.
* [ ] The frozen body remains visible for a configurable duration.
* [ ] The body disappears after the freeze period.
* [ ] The existing respawn countdown continues to function.
* [ ] The player respawns normally.
* [ ] The intact bullseye is restored upon respawn.
* [ ] Player controls are completely restored upon respawn.
* [ ] Additional damage cannot trigger duplicate death effects.
* [ ] Death is counted only once by the kill-tracking system.
* [ ] Other multiplayer clients can see the bullseye shatter and player disappearance.
* [ ] Cosmetic fragment physics does not require continuous network synchronization.
* [ ] Grenade bullseye dislodgement remains separate from bullseye destruction.
* [ ] If the bullseye is detached when death occurs, it shatters from its current location when technically feasible.
* [ ] The system works for host and client players.
* [ ] No NullReferenceExceptions or network errors occur during death or respawn.

---

# Out of Scope

The following are not required for REQ-032:

* Procedural runtime mesh fracturing
* Advanced destruction middleware
* Gore
* ragdoll player physics
* third-person death animations
* killcams
* slow-motion effects
* screen post-processing effects
* custom final bullseye art
* final glass-breaking audio assets
* networking every individual fragment's physics
* changing the existing health/damage values
* changing kill-tracking rules
* redesigning the respawn UI

These may be considered in later requirements.

---

# Desired Result

Player death should feel much more distinctive than simply removing and respawning the player.

The intended visual rhythm is:

**Hit → Bullseye explodes into fragments → sharp glass break → player freezes like a defeated statue → body disappears → respawn.**

The shattered bullseye should reinforce the game's central visual identity: the bullseye is effectively the player's life, and destroying it should feel like physically breaking the player rather than merely reducing a conventional health bar to zero.
