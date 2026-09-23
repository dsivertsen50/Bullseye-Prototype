# REQ-069 — First-Person Body, Hands, Weapon Presentation, and Simplified Reload Animations

## Summary

Rework the game's first-person presentation so that the player feels like they are controlling an actual character rather than a floating camera with a floating weapon.

This requirement has two major objectives:

1. Replace complicated first-person reload animations with a simple **weapon-lowering reload system**.
2. Begin introducing the player's **visible body and hands into the first-person view**, synchronized as closely as practical with the character that other players see.

The objective is not to achieve final AAA-quality first-person animation in this ticket. The objective is to establish a clean, maintainable foundation that can be improved incrementally.

---

## Background

The current first-person system has several presentation problems:

- Reloading would require weapon-specific hand and magazine animations that are expensive and tedious to create.
- The player currently cannot see their own body.
- Looking downward does not reveal the player's legs.
- Player hands are not visibly holding the weapon.
- First-person movement does not visually communicate many of the animations/actions occurring on the world-view player character.

Rather than investing heavily in bespoke reload animations, reloads should be deliberately stylized and simplified.

At the same time, the first-person camera should begin feeling physically connected to the actual player character.

---

# Part 1 — Simplified Off-Screen Reload System

## Goal

Remove the need for traditional reload animations.

When a player reloads, the weapon should simply lower below the visible portion of the screen, remain off-screen for the appropriate reload duration, and then rise back into its normal position when the weapon is ready to fire.

---

## Reload Sequence

A normal reload should follow approximately this sequence:

1. Player initiates reload.
2. Weapon firing is disabled.
3. Weapon smoothly lowers below the bottom of the camera view.
4. Weapon remains off-screen for the reload period.
5. Ammunition is updated according to the existing weapon/ammunition system.
6. Weapon smoothly rises back into its normal first-person position.
7. Weapon firing becomes available again.

Example:

```text
Normal Weapon Position
        ↓
Weapon lowers
        ↓
Weapon completely off-screen
        ↓
Reload timer
        ↓
Weapon rises
        ↓
Normal Weapon Position
```

No magazine manipulation, bolt manipulation, hand animation, or other detailed reload animation is required.

---

## Weapon-Specific Reload Timing

Different weapons must be able to have different reload durations.

For example:

```text
Pistol          Short reload
DMR             Medium reload
Assault Rifle   Medium reload
Shotgun         Longer reload
Sniper Rifle    Longer reload
Heavy Weapons   Potentially very long reload
```

Do NOT hard-code these durations into the animation controller.

The weapon definition/configuration should contain an adjustable reload duration.

Prefer something similar to:

```text
ReloadDuration
ReloadLowerDuration
ReloadRaiseDuration
```

Or the equivalent configuration appropriate to the existing weapon architecture.

The important requirement is that designers can modify these values without modifying code.

---

## Reload Lower/Raise Animation

The lowering and raising should use simple procedural interpolation rather than requiring authored animation clips.

Expose useful tuning values such as:

- Lower position
- Lower rotation if desired
- Lowering speed/duration
- Raising speed/duration
- Reload off-screen duration

The weapon should move smoothly rather than instantly disappearing.

A very small amount of rotation while lowering is acceptable if it makes the movement look better.

---

## Reload State

The weapon system should have a reliable reload state.

While reloading:

- Weapon cannot fire.
- Weapon cannot initiate another reload.
- Reload timing remains synchronized with the actual ammunition state.
- Weapon should not visually return until the reload has completed.

The existing weapon-switching behavior should also be inspected.

If weapon switching during reload is currently allowed, make sure it cannot result in:

- duplicated ammunition,
- incorrect ammunition counts,
- weapons remaining permanently lowered,
- firing during reload,
- broken first-person positioning.

Do not unnecessarily redesign the entire weapon state machine if the existing implementation is functional.

---

# Part 2 — Visible First-Person Character Body

## Goal

The player should begin seeing their own character from the first-person perspective.

Currently the camera behaves as though it is floating independently of the character.

At minimum, the player should be able to look downward and see their legs/body.

---

## First-Person Body Visibility

When looking downward, the local player should be able to see appropriate portions of their character.

At minimum:

- legs,
- feet,
- lower torso where appropriate.

Additional torso visibility is desirable if it does not create camera clipping problems.

The character body should feel positioned underneath the player's head/camera rather than several feet behind or in front of it.

---

## Camera Placement

Inspect the first-person camera's relationship to the player skeleton.

The camera should approximately correspond to the character's head/eye position.

However, do NOT simply place the camera inside the existing head mesh if doing so causes the camera to see:

- the inside of the head,
- facial geometry,
- teeth,
- eyes,
- hair,
- other internal geometry.

The local player's head may need to be hidden from the first-person camera while remaining visible to other players.

Use appropriate Unity layers/culling or another maintainable solution.

---

# Part 3 — First-Person Hands on Weapons

## Goal

Weapons should no longer appear to float unsupported in front of the camera.

Where appropriate, the player should see hands holding the weapon.

---

## Hand Placement

The player's hands should visibly interact with the weapon.

For long guns, generally:

- dominant hand near the grip/trigger,
- support hand underneath or around the forward portion of the weapon.

For pistols:

- appropriate one-handed or two-handed grip depending on the chosen visual approach.

For heavy weapons:

- use a reasonable heavy-weapon grip.

Exact grip positioning does not have to be perfect during this ticket.

The first objective is to establish a system where hands can reliably follow designated weapon grip points.

---

## Weapon Grip Targets

Where practical, weapons should expose configurable transform points such as:

```text
RightHandGrip
LeftHandGrip
```

or equivalent.

This will allow hand placement to be adjusted individually for each weapon without creating entirely separate animation systems.

This system should support all current weapons and future weapons.

---

## IK / Procedural Positioning

Investigate whether Unity's existing animation/IK system can be used so that player hands follow weapon grip targets.

The implementation should favor:

- reusable procedural positioning,
- IK,
- configurable grip transforms,

over manually authoring a completely different animation for every weapon.

This is especially important because Bullseye will contain many weapons.

---

# Part 4 — Synchronization With Third-Person Character Movement

## Goal

The body the player sees in first person should correspond reasonably closely with the player body that other players see.

The two views do not need to be pixel-perfect copies, but they should represent the same physical actions.

---

## Locomotion

First-person body movement should account for existing player actions including:

- Idle
- Walking
- Sprinting
- Jumping
- Falling
- Landing
- Crouching
- Prone
- Dolphin dive/body slam if applicable
- Ladder climbing once REQ-068 is implemented

If the third-person character is sprinting, the player's visible first-person body should look like it is sprinting.

If the character is crouched, the visible body should reflect the crouched stance.

If prone, the first-person body should not appear to remain standing underneath the camera.

---

# Part 5 — Weapon Movement During Locomotion

Weapons should react appropriately to movement.

Examples include:

### Walking

Subtle movement/bob.

### Sprinting

Weapon should move into an appropriate sprint posture rather than remaining perfectly aimed.

The existing sprint restrictions on firing should continue to apply.

### Jumping

Weapon/body should react subtly to leaving the ground.

### Landing

Weapon/body should react to the landing impact.

This can initially be procedural.

Do not require bespoke animation clips for every weapon/action combination.

---

# Part 6 — First-Person vs. World-View Architecture

Cursor should inspect the existing player and weapon architecture before making major changes.

We currently have two related but distinct visual requirements:

### First-Person View

What the local player sees.

### World View

What every other player sees when looking at that character.

These systems should remain logically connected but do not necessarily need to render identically.

It may be appropriate to use:

- one underlying character animation state,
- separate rendering rules,
- first-person weapon presentation,
- third-person weapon presentation,
- shared player locomotion state.

Avoid creating two completely independent animation systems that can easily become desynchronized.

---

# Part 7 — Multiplayer Considerations

The first-person body and weapon presentation are primarily local visual effects.

Do not unnecessarily network:

- camera movement,
- first-person weapon lowering,
- first-person hand IK,
- first-person camera bob,
- other purely local presentation details.

However, gameplay state must remain synchronized.

Examples:

```text
Reload state
Equipped weapon
Player stance
Sprint state
Jump state
Prone/crouch state
Weapon firing state
```

Remote players should continue seeing the appropriate world-view character animation/state.

The new first-person system must not break Netcode for GameObjects multiplayer behavior.

---

# Part 8 — Camera / Mesh Clipping

Special attention should be given to first-person clipping.

Test situations including:

- looking straight down,
- looking sharply upward,
- crouching,
- prone,
- sprinting,
- jumping,
- standing near walls,
- walking into walls,
- aiming/zooming,
- weapon lowering for reloads.

The player's body should not frequently clip through the camera.

Likewise, weapons and arms should not visibly pass through the camera in extreme ways.

Some minor imperfections are acceptable at this stage.

---

# Part 9 — Zoom / ADS Compatibility

The changes in this requirement must continue supporting the existing aim/zoom system.

When entering ADS/zoom:

- weapon should move to its existing aiming position,
- hands should continue following the weapon,
- visible body should not interfere significantly with the sight picture.

Existing special behavior should remain intact, including the sniper rifle's unique zoom/movement restrictions.

Reloading while zoomed should force an appropriate transition out of the aiming state if necessary.

---

# Part 10 — No Detailed Reload Animation Requirement

An important design decision from this requirement is:

> Bullseye does not require traditional animated reload sequences.

Do NOT spend substantial development time building:

- magazine removal animations,
- magazine insertion animations,
- bolt cycling animations,
- individual shell-loading hand animations,
- weapon-specific first-person reload animation clips.

The lowering system is the intended art direction unless deliberately changed in a future requirement.

---

# Implementation Priority

Because first-person full-body rendering and hand placement can become technically difficult, implement this requirement incrementally.

Recommended order:

## Phase A — Reload Simplification

1. Implement weapon lowering.
2. Make lowering/raising procedural.
3. Add configurable reload durations.
4. Ensure ammo/state logic remains correct.
5. Test all existing weapons.

## Phase B — Visible Body

1. Make local player's legs/body visible when looking downward.
2. Correct camera placement.
3. Prevent head/internal mesh clipping.
4. Validate crouch/prone/jump states.

## Phase C — Hands

1. Add visible hands/arms.
2. Establish weapon grip transforms.
3. Add IK/procedural hand placement.
4. Configure existing weapons.

## Phase D — Movement Integration

Connect first-person presentation to:

- walking,
- sprinting,
- jumping,
- landing,
- crouching,
- prone,
- other locomotion states.

Polish can occur in future animation-focused requirements.

---

# Acceptance Criteria

REQ-069 is complete when:

- [ ] Reloading no longer requires detailed reload animation clips.
- [ ] Reloading visibly lowers the weapon below the screen.
- [ ] Weapon remains lowered for its configured reload period.
- [ ] Weapon rises back into position when ready.
- [ ] Reload duration can vary by weapon.
- [ ] Reload timing remains synchronized with actual ammunition state.
- [ ] Weapons cannot fire while reloading.
- [ ] Player can see at least their legs/feet when looking downward.
- [ ] Local player's head geometry does not obstruct the first-person camera.
- [ ] Player hands are visible holding weapons where appropriate.
- [ ] Weapons provide configurable hand-grip targets or an equivalent reusable system.
- [ ] First-person hands follow weapon positioning sufficiently well for normal gameplay.
- [ ] First-person body responds appropriately to walking.
- [ ] First-person body responds appropriately to sprinting.
- [ ] First-person body responds appropriately to jumping/falling/landing.
- [ ] First-person body reflects crouching.
- [ ] First-person body reflects prone state.
- [ ] ADS/zoom continues functioning.
- [ ] Sniper zoom behavior remains functional.
- [ ] Existing third-person/world-view characters remain functional.
- [ ] Multiplayer synchronization is not broken.
- [ ] First-person-only effects are not unnecessarily networked.
- [ ] No major recurring camera/body clipping occurs during normal gameplay.

---

# Non-Goals

REQ-069 does NOT require:

- final-quality hand animation,
- finger animation,
- detailed magazine animation,
- shell-by-shell reload animation,
- bolt manipulation animation,
- perfect first-person/third-person animation matching,
- final animation polish,
- custom animations for every weapon,
- solving every possible camera clipping edge case,
- rebuilding the complete third-person animation system.

These can be addressed incrementally later.

---

# Design Principle

The long-term goal is for Bullseye's player to feel like a **physical character inhabiting the game world**, rather than a camera with a weapon attached to it.

However, animation complexity should not become a development bottleneck.

Therefore:

> Use procedural motion, reusable IK, configurable grip points, and simple presentation tricks whenever they produce an acceptable result.

The simplified off-screen reload system is intentionally part of this philosophy.