# REQ-045 — Bullet Impact and Near-Miss Audio

## Objective

Add firearm impact and near-miss audio feedback to the existing shooting system.

REQ-045 should introduce two related systems:

1. **Bullet Impact Sounds**
   When a bullet strikes an environmental surface, randomly select and play a sound effect from an inspector-configurable array.

2. **Bullet Flyby / Near-Miss Sounds**
   When a hitscan shot passes sufficiently close to another player without hitting them, play a bullet flyby / whiz sound for that nearby player.

These effects should improve the perceived power, directionality, and danger of gunfire without altering weapon damage or hit detection.

---

# 1. Bullet Impact Sound Effects

When a firearm hits a valid environmental surface, play an impact sound at the impact location.

The sound should correspond spatially with the bullet-hole decal introduced in REQ-044.

Conceptually:

```text
Shot Fired
    ↓
Environmental Hit
    ↓
Bullet Hole Decal
    +
Impact Sound
```

The sound should use Unity 3D spatial audio so its apparent direction corresponds with the impact location.

---

# 2. Random Impact Sound Array

Expose an inspector-configurable collection of impact sounds.

Example:

```text
Bullet Impact Sounds

[0] BulletImpact_01
[1] BulletImpact_02
[2] BulletImpact_03
[3] BulletImpact_04
[4] BulletImpact_05
```

Each valid impact should randomly select one sound from the array.

This prevents repeated gunfire from producing an obviously identical sound every time.

If only one sound is assigned, the system should still function normally.

If no sounds are assigned, shooting should continue without errors.

---

# 3. Small Pitch and Volume Variation

For additional variation, optionally apply small randomized changes to:

```text
Pitch
Volume
```

For example:

```text
Pitch Range:
0.95 – 1.05

Volume Range:
0.90 – 1.00
```

These values should be configurable.

Variation should remain subtle and should not make impacts sound unnaturally distorted.

---

# 4. Shared vs Weapon-Specific Impact Sounds

The initial implementation may use the same shared impact-sound collection for:

* Pistol
* AK
* DMR
* Shotgun
* Future sniper rifle

However, the architecture should make it possible to override these sounds per weapon later if desired.

For example:

```text
Global Bullet Impact Sounds
        ↓
Weapon Override?
        ↓
Yes → Weapon Impact Sounds
No  → Shared Impact Sounds
```

Do not require weapon-specific audio for REQ-045.

---

# 5. Integration with REQ-044

Where practical, the impact-audio system should use the same validated surface-hit event used to create bullet decals.

Do not perform unnecessary duplicate raycasts merely to play audio.

Conceptually:

```text
Valid Environmental Hit
        ↓
Impact Event
        ↓
├── Spawn Bullet Decal
└── Play Impact Sound
```

This should keep visual and audio impact feedback synchronized.

---

# 6. Shotgun Impact Audio

Shotgun pellets may strike many surfaces simultaneously.

Do **not** necessarily play one full-volume impact sound for every individual pellet.

Doing so could create:

* Excessive volume
* Audio clipping
* An unnatural wall of noise
* Unnecessary AudioSource creation

Instead, provide reasonable shotgun impact-audio limiting.

Suggested options:

```text
Maximum Impact Sounds Per Shotgun Blast = 2–4
```

or aggregate nearby pellet impacts into fewer sounds.

Individual pellet decals may still appear according to REQ-044.

The exact implementation may be chosen based on what sounds most natural.

---

# 7. Future Surface-Specific Sounds

The architecture should allow future support for different impact sounds based on surface material.

For example:

```text
Concrete
Metal
Wood
Glass
Dirt
```

However, material-specific impact audio is **not required** for REQ-045.

For now, all ordinary environmental surfaces may use the same shared array.

Do not over-engineer this requirement solely to implement a material system that does not yet exist.

---

# 8. Near-Miss / Bullet Flyby System

Add a near-miss system for hitscan firearms.

When a hitscan shot passes sufficiently close to another player but does not actually hit that player, that player should hear a bullet flyby / whiz sound.

This should give the sensation of bullets narrowly passing nearby.

Example:

```text
Shooter
   \
    \
     \   WHIZ
      \     O  Target Player
       \
        X Wall Impact
```

The nearby player does not need to take damage for the flyby effect to occur.

---

# 9. Hitscan Proximity Detection

Because current firearms use hitscan / scan-based shooting, determine whether the shot line passes near another player.

Treat the fired shot as a line segment:

```text
Shot Origin
    ↓
Final Hit Point / Maximum Shot Distance
```

For every eligible non-shooter player, determine the shortest distance between that player's near-miss reference point and the shot line segment.

Conceptually:

```text
distance = DistanceFromPlayerToShotLine
```

If:

```text
distance <= NearMissRadius
```

then that player may receive a flyby sound.

---

# 10. Near-Miss Reference Point

The system should not depend on the current temporary capsule architecture.

Use a player reference point or collider configuration that will continue to function with the humanoid character.

Possible implementations:

```text
Player center / torso transform
Character collider bounds
Dedicated NearMissReceiver component
```

Preferred architecture:

```text
NearMissReceiver
```

attached to the player character.

This makes the system easier to tune as the player model evolves.

---

# 11. Configurable Near-Miss Radius

Expose an inspector-configurable near-miss distance.

Example:

```text
Near Miss Radius = 1.5 meters
```

This is only a starting point.

It should be easy to tune during gameplay testing.

The effect should represent bullets that pass noticeably close to the player, not every shot in the player's general vicinity.

---

# 12. Inner and Outer Near-Miss Zones

If straightforward, support two proximity ranges.

Example:

```text
Very Close Flyby:
0 – 0.75 m

Normal Flyby:
0.75 – 1.5 m
```

A very close bullet could:

* Play slightly louder
* Use a more aggressive whiz sound
* Have stronger stereo/spatial positioning

This enhancement is preferred but not mandatory for the initial implementation.

A single near-miss radius is acceptable for REQ-045.

---

# 13. Flyby Sound Array

Expose an array of bullet flyby sounds.

Example:

```text
Bullet Flyby Sounds

[0] BulletWhiz_01
[1] BulletWhiz_02
[2] BulletWhiz_03
[3] BulletWhiz_04
```

Randomly select one when a valid near miss occurs.

Support subtle pitch variation if appropriate.

---

# 14. Weapon Support

The near-miss system should support:

* Pistol
* AK
* DMR
* Shotgun
* Future sniper rifle

However, different weapon types may eventually use different flyby audio.

For REQ-045, a shared flyby library is acceptable.

The architecture should allow future overrides.

---

# 15. Shotgun Near Misses

Shotgun handling requires special care.

Do not play a separate flyby sound for every pellet passing near the same player.

A single shotgun trigger pull should normally cause no more than one near-miss audio event per nearby player.

Example:

```text
12 pellets fired
5 pass within near-miss radius
        ↓
1 shotgun near-miss audio event
```

This prevents audio spam.

---

# 16. Automatic AK Fire

The AK can generate many shots quickly.

A player standing near the path of sustained automatic fire should hear repeated flybys, but not dozens of sounds layered simultaneously.

Implement a short local cooldown.

Example:

```text
Near Miss Audio Cooldown:
0.08 – 0.20 seconds
```

Exact value should be configurable.

The purpose is to preserve the sensation of sustained incoming fire while avoiding audio overload.

---

# 17. Flyby Should Not Trigger on Direct Hits

If the bullet actually hits the player:

```text
Do NOT play the standard near-miss sound.
```

The player's existing hit feedback should take priority.

Conceptually:

```text
Bullet intersects Player
        ↓
Damage / Hit Feedback

Bullet passes near Player
        ↓
Flyby Sound
```

These should be mutually exclusive for the same shot.

---

# 18. Shooter Exclusion

The shooter must never trigger their own near-miss sound from their own bullet leaving the weapon.

Always exclude:

```text
Firing Player
```

from near-miss evaluation.

---

# 19. Multiplayer Behavior

Near-miss audio is player-specific.

Only the player who narrowly experiences the shot should hear the flyby effect.

Example:

```text
Player A fires
        ↓
Shot passes near Player B
        ↓
Player B hears WHIZ

Player C is far away
        ↓
No flyby
```

The shooter does not need to hear the target's near-miss effect.

---

# 20. Network Architecture

Avoid networking AudioSources directly.

Preferred concept:

```text
Authoritative / validated shot
          ↓
Determine shot trajectory
          ↓
Determine players near trajectory
          ↓
Send lightweight near-miss event
          ↓
Affected client plays sound locally
```

Alternatively, if each client already receives enough authoritative shot information to calculate near misses correctly and securely, the effect may be determined locally.

Use whichever approach best fits the existing Netcode for GameObjects architecture.

Near-miss audio is cosmetic and should not create significant bandwidth overhead.

---

# 21. Spatial Direction of Flyby

The flyby should ideally communicate where the shot passed relative to the listener.

Use 3D/spatialized audio where practical.

A good implementation may play the sound near the closest point on the bullet trajectory to the player.

Conceptually:

```text
Shot Line
----------------X----------------
                ↑
       Closest point to player

                  O Player
```

Play the flyby at or relative to `X`.

This should make a bullet passing the player's left side sound different from one passing on their right.

---

# 22. Do Not Attach Flyby Audio to Impact Point

The near-miss sound should represent the bullet passing the player.

Therefore:

```text
Impact Sound → Impact Point

Flyby Sound → Closest Point Near Player
```

Do not simply play both sounds at the wall where the bullet eventually lands.

---

# 23. Timing

The flyby should occur essentially when the shot passes the player.

Because the weapons are hitscan, there is no actual projectile travel time.

For normal weapon distances, immediate playback is acceptable.

Do not introduce artificial delays unless needed later for extremely long-range sniper shots.

---

# 24. Audio Manager / Pooling

Avoid frequently instantiating and destroying AudioSource GameObjects.

Use existing project audio infrastructure if available.

Otherwise, consider a simple pooled audio system.

Possible structure:

```text
WeaponImpactAudioManager
NearMissAudioManager
PooledSpatialAudioSource
```

Cursor may choose names appropriate to the existing project.

---

# 25. Audio Configuration

Provide inspector-configurable settings similar to:

```text
Bullet Impact Audio
[x] Enabled

Impact Clips:
    Element 0
    Element 1
    Element 2

Impact Volume:
1.0

Impact Pitch Variation:
0.05


Near Miss Audio
[x] Enabled

Flyby Clips:
    Element 0
    Element 1
    Element 2

Near Miss Radius:
1.5

Near Miss Volume:
1.0

Near Miss Cooldown:
0.12
```

These values should not require code changes to tune.

---

# 26. Volume Settings Integration

REQ-045 audio should respect the game's existing / future audio settings.

Ideally:

```text
Impact Sounds → SFX mixer
Flyby Sounds  → SFX mixer
```

Changing the player's sound-effects volume should affect both.

Do not hard-code audio output directly around the settings system.

---

# 27. Required Audio Placeholder Locations

Create clear inspector fields so that audio clips can easily be dragged in later.

Suggested project organization:

```text
Assets/
    Audio/
        Weapons/
            Impacts/
            Flybys/
```

Example:

```text
Assets/Audio/Weapons/Impacts/
    BulletImpact_01.wav
    BulletImpact_02.wav
    BulletImpact_03.wav

Assets/Audio/Weapons/Flybys/
    BulletWhiz_01.wav
    BulletWhiz_02.wav
    BulletWhiz_03.wav
```

Placeholder clips may be used during implementation.

Do not require final audio assets for the code architecture to function.

---

# 28. Debug Mode

Provide optional debugging support.

Useful information may include:

```text
Shot trajectory
Near-miss radius
Closest point to player
Distance from trajectory to player
Near miss triggered: YES / NO
Cooldown active
```

For example, the shot trajectory could be drawn using:

```csharp
Debug.DrawLine(...)
```

Development debugging should be disabled by default in normal gameplay.

---

# 29. Performance

Near-miss detection must not introduce a large performance cost.

Avoid expensive global object searches for every shot.

Do not use:

```csharp
FindObjectsOfType<Player>()
```

for every bullet.

Use the existing player/network registry or maintain an appropriate list of eligible players.

This is particularly important for automatic AK fire.

---

# 30. Future Extensibility

Design the audio architecture so it can eventually support:

```text
Different surface impact materials
Supersonic cracks
Subsonic weapons
Silenced weapons
Weapon-specific flybys
Different bullet calibers
Ricochet sounds
Penetration sounds
Long-range travel delay
Indoor/outdoor audio variation
```

None of these are required now.

REQ-045 should simply avoid making them unnecessarily difficult to add later.

---

# Acceptance Criteria

REQ-045 is complete when:

1. Shooting an environmental surface plays an impact sound at the impact location.
2. Impact sounds are randomly selected from an inspector-configurable array.
3. Missing or empty audio arrays do not produce errors.
4. Optional subtle pitch/volume variation can be configured.
5. Bullet decals and impact sounds use the same existing hit result where practical.
6. Shotgun impacts do not produce excessive overlapping audio.
7. A hitscan shot passing sufficiently close to another player triggers a flyby sound for that player.
8. Near-miss radius is configurable.
9. Flyby sounds are randomly selected from an inspector-configurable collection.
10. Flyby sound originates near the closest portion of the shot trajectory to the player.
11. Left/right positioning of flybys is perceptible where supported by spatial audio.
12. A direct player hit does not also trigger the normal flyby sound.
13. The shooter cannot trigger their own flyby sound.
14. Players far from the shot trajectory hear no near-miss sound.
15. Shotgun blasts trigger at most one reasonable near-miss event per affected player per trigger pull.
16. Sustained AK fire does not produce uncontrolled overlapping flyby sounds.
17. A configurable near-miss cooldown prevents audio spam.
18. Near-miss effects work correctly in multiplayer.
19. Only affected players receive their player-specific flyby feedback.
20. Audio effects respect the game's sound-effects volume configuration.
21. The implementation does not alter weapon damage or hit detection.
22. The system works with the pistol, AK, DMR, and shotgun.
23. The architecture can support the future sniper rifle without redesign.
24. Existing weapon firing, decals, multiplayer, damage, and respawn systems continue functioning correctly.

---

# Desired Result

Gunfire should become substantially more informative and threatening.

When bullets hit nearby surfaces:

```text
BANG → crack / pop / impact
```

with enough random variation that repeated fire does not sound identical.

When another player narrowly misses the local player:

```text
         WHIZ →
             O
           Player
```

the player should immediately perceive that a round passed dangerously close to them.

During a firefight, sustained AK fire should produce occasional directional whizzes around a player, a sniper round narrowly missing should feel especially noticeable, and nearby shotgun pellets should communicate danger without generating an overwhelming number of overlapping sounds.

The result should make incoming fire feel much more physical while keeping the system cosmetic, performant, configurable, and multiplayer-friendly.
