# REQ-021 — Cracked Bullseye Health HUD

## Status

Ready for implementation on:

`experiment/fps-engine-integration`

## Background

Bullseye currently communicates player health through a conventional local health HUD.

The game also uses world-space health bars above other players, which should remain because they provide useful combat information when looking at an opponent.

For the local player's own HUD, however, Bullseye should begin developing a more distinctive visual identity tied directly to its core mechanic.

The player's vulnerability is represented by a bullseye. The local health indicator should therefore also be represented by a bullseye.

As the player loses health, the HUD bullseye should progressively crack and deteriorate.

As existing health regeneration restores health, those cracks should progressively repair.

The bullseye artwork itself is not final, so the system must be designed so the base bullseye image can be replaced later without rewriting the HUD.

---

# Goal

Replace the existing local segmented health display with a stylized **Bullseye Health HUD**.

The HUD should:

* display a configurable bullseye image;
* visually crack as health is lost;
* progressively repair as health regenerates;
* provide clear feedback for the existing 8-health system;
* support cracking/damage sound effects;
* support optional repair sound effects;
* remain easy for a designer to modify through the Unity Inspector.

The system should be presentation-only.

Do not change the existing health, damage, regeneration, death, or respawn logic.

---

# Core Visual Concept

At full health:

```text
8 / 8 HP
→ clean bullseye
```

As health decreases:

```text
7 / 8
→ light cracking

6 / 8
→ additional cracking

5 / 8
→ moderately damaged

4 / 8
→ significantly fractured

3 / 8
→ heavily fractured

2 / 8
→ severe damage

1 / 8
→ nearly shattered

0 / 8
→ shattered / destroyed state
```

The HUD should visually communicate health even if no number is displayed.

---

# Existing Health System Remains Authoritative

The new HUD must read health from the existing:

`PlayerHealth`

Do not create separate local health state.

Do not change:

* maximum health;
* current health;
* damage values;
* health regeneration;
* regeneration delay;
* regeneration cadence;
* death;
* respawn.

The HUD simply reflects existing state.

Conceptually:

```text
PlayerHealth
     ↓
CurrentHealth
     ↓
BullseyeHealthHUD
     ↓
Base bullseye + crack presentation
```

---

# Existing 8-Health Model

Current health values are:

`0–8`

The HUD should support every health point independently.

This is important even though current attacks typically remove multiple HP at once.

Examples:

### Lower-body hit

```text
8 → 6
```

The HUD should immediately transition from clean to the appropriate 6 HP damage state.

### Torso hit

```text
8 → 4
```

The HUD should immediately become significantly fractured.

### Head-position hit

```text
8 → 0
```

The bullseye should immediately transition into the shattered/dead presentation.

---

# HUD Placement

Use the same general region currently occupied by the local health indicator unless another nearby placement clearly works better.

Preferred location:

**bottom-left corner of the local player's screen**

The HUD should:

* remain visible during normal gameplay;
* not obstruct aiming;
* remain readable at common resolutions;
* scale appropriately with UI resolution.

Use Unity UI rather than relying on hardcoded screen coordinates where practical.

---

# Base Bullseye Image

The bullseye should use a replaceable sprite/image asset.

Create an Inspector field such as:

```text
Base Bullseye Sprite
```

or equivalent.

The current implementation may use a temporary/prototype bullseye asset.

The final bullseye design will likely change.

Replacing the image later should require:

1. importing a new sprite;
2. dragging it into the appropriate Inspector field.

No code modification should be necessary.

---

# Crack Presentation Architecture

Do **not** permanently bake the crack graphics into the bullseye artwork.

Preferred hierarchy:

```text
BullseyeHealthHUD
├── BullseyeBase
├── CrackLayer01
├── CrackLayer02
├── CrackLayer03
├── CrackLayer04
├── CrackLayer05
├── CrackLayer06
├── CrackLayer07
└── Critical / Shattered Layer
```

The exact number of layers may vary if another implementation provides equivalent control.

The important design principle is:

> Base artwork and damage artwork must be separable.

This allows the bullseye to be redesigned later without rebuilding the health system.

---

# Crack Progression

Preferred implementation:

Each missing HP adds approximately one stage of cracking.

Conceptually:

```text
8 HP → 0 crack layers
7 HP → 1
6 HP → 2
5 HP → 3
4 HP → 4
3 HP → 5
2 HP → 6
1 HP → 7
0 HP → shattered state
```

Cursor may instead use precomposed sprites for individual damage states if that produces a significantly cleaner implementation.

However, the architecture must still allow:

* easy replacement;
* configurable sprites;
* no code changes when art changes.

Layered cracks are preferred.

---

# Crack Sprite Configuration

Expose all crack artwork through Inspector fields.

Preferred configuration:

```text
Base Bullseye Sprite

Crack Sprites
[0]
[1]
[2]
[3]
[4]
[5]
[6]

Shattered Sprite / Overlay
```

This may be implemented as:

`Sprite[] crackSprites`

or an equivalent serialized structure.

A designer should be able to drag PNG/sprite assets into these slots.

---

# Damage Animation

When health decreases, newly added cracking should have noticeable but brief feedback.

Suggested behavior:

1. health decreases;
2. bullseye quickly jolts/shakes;
3. appropriate new crack state appears;
4. optional brief flash;
5. HUD settles back to neutral.

Do not create an excessively long animation that makes current health unclear.

The new damage state should become readable almost immediately.

---

# HUD Shake

Add a small local HUD impact reaction when taking damage.

Possible behavior:

* short positional shake;
* tiny rotation;
* slight scale punch.

Expose intensity through the Inspector.

Suggested fields:

```text
Damage Shake Strength
Damage Shake Duration
Damage Scale Punch
```

Keep effects subtle.

---

# Crack Sound Effects

## Requirement

The implementation must include **easy Inspector slots for cracking/damage sound files**.

I want to be able to import audio clips later and drag them into the component without modifying code.

At minimum expose:

```text
Crack Sounds
    Element 0
    Element 1
    Element 2
    ...
```

Preferred implementation:

```csharp
AudioClip[] crackSounds;
```

or equivalent.

When new damage occurs, play a crack sound.

---

# Crack Sound Selection

Preferred behavior:

If multiple crack sounds are supplied, choose one randomly when damage occurs.

For example:

```text
Crack Sounds
├── crack_01.wav
├── crack_02.wav
├── crack_03.wav
└── crack_04.wav
```

Then:

```text
Player takes damage
      ↓
random crack sound selected
      ↓
play locally
```

This prevents repeated damage from sounding identical.

---

# Damage Severity and Sound

If straightforward, allow optional sound variation based on severity.

Potential configuration:

```text
Light Damage Sounds
Heavy Damage Sounds
Shatter Sounds
```

For example:

### 2 damage

Use a normal crack.

### 4 damage

Use a more forceful crack.

### 8 damage / death

Use a distinct shattering sound.

However, this is optional.

A single configurable `Crack Sounds` array plus separate `Shatter Sound` is sufficient for acceptance.

---

# Required Shatter Sound Slot

Expose a dedicated Inspector field:

```text
Shatter Sound
```

or:

```text
Shatter Sounds[]
```

When health reaches:

`0`

play the shatter effect rather than—or in addition to—the ordinary crack sound.

This field may be empty by default.

The system must work even if no sound has yet been assigned.

---

# Repair Sound Support

Also provide optional Inspector slots for health repair/regeneration sounds.

Preferred:

```text
Repair Sounds[]
```

or:

```text
Repair Sound
```

When a health point regenerates and one damage stage visually repairs:

* optionally play a subtle repair sound.

This sound should be much quieter/less aggressive than taking damage.

The feature must function correctly if no repair audio is assigned.

---

# AudioSource

Use a local UI/presentation `AudioSource`.

Do not introduce a large audio-manager dependency solely for this HUD.

The `AudioSource` should:

* be local;
* play UI-style non-spatial audio;
* support volume configuration.

Expose:

```text
Damage Sound Volume
Repair Sound Volume
```

or equivalent if useful.

---

# No Required Audio Assets

REQ-021 should create the **slots and behavior**, but does not require final sound assets.

The project should work with:

```text
Crack Sounds = empty
Shatter Sound = null
Repair Sounds = empty
```

without:

* errors;
* warnings every frame;
* null-reference exceptions.

I will be able to add final sounds later by dragging them into the Inspector.

---

# Health Regeneration / Crack Repair

The cracking system must automatically follow existing health regeneration.

Do not create another repair timer.

If `PlayerHealth` changes:

```text
3 → 4
```

the HUD should remove one level of visual damage.

If it changes:

```text
4 → 5
```

another level should repair.

Therefore, HUD repair occurs at exactly the same cadence as health regeneration.

Conceptually:

```text
PlayerHealth regeneration
        ↓
health increases by 1
        ↓
BullseyeHealthHUD observes change
        ↓
one damage stage repairs
```

---

# Repair Animation

When health increases:

* do not simply snap invisibly if a small repair effect can be added cleanly.

Preferred behavior:

1. crack subtly glows/fades;
2. crack layer disappears;
3. bullseye returns toward clean state.

Optional effects:

* soft flash;
* slight scale pulse;
* brief emissive/glow effect.

Keep it subtle.

---

# Repair Animation Timing

The repair visual should occur **after the authoritative health value increases**, not independently.

The HUD should never visually indicate more health than `PlayerHealth` actually contains.

---

# Taking Damage During Regeneration

If the player begins regenerating and is hit again:

* immediately display the new correct damage state;
* interrupt any conflicting repair animation;
* play damage feedback;
* allow the existing `PlayerHealth` system to determine when regeneration restarts.

Do not implement a separate regeneration-delay rule.

---

# Shattered / Death State

At:

`0 HP`

the bullseye should have a distinct destroyed presentation.

Options include:

* heavily cracked;
* shattered;
* pieces missing;
* broken center;
* strong fracture overlay.

Expose this through a configurable sprite/overlay.

Suggested field:

```text
Shattered Overlay
```

The exact artwork can remain temporary.

---

# Death HUD Behavior

During the existing respawn countdown:

Preferred behavior:

* shattered bullseye remains visible;
* existing respawn countdown remains readable.

Do not remove or obscure the respawn timer.

Possible layout:

```text
[ shattered bullseye ]

RESPAWNING IN 3
```

The exact layout may vary.

---

# Respawn

When the player respawns:

```text
0 → 8
```

Reset the health HUD to:

* clean bullseye;
* no crack layers;
* no stale damage animation;
* no stale repair animation.

Do not play eight individual repair sounds during respawn.

Respawn should be treated as an immediate full reset.

---

# Existing Health HUD

The current local segmented/numeric health display may be:

* removed;
* disabled;
* replaced.

Do not display both old and new local health indicators unless temporary debugging requires it.

The new Bullseye HUD becomes the primary local health presentation.

---

# Enemy Health Bars

Do **not** remove the existing health bars above other players.

The desired UI distinction is:

```text
YOUR HEALTH
→ cracked Bullseye HUD

ENEMY HEALTH
→ world-space bar above opponent
```

Both systems serve different purposes.

---

# Owner-Only Behavior

The Bullseye health HUD should display only for the owning player.

Do not display another player's cracked Bullseye HUD on the local screen.

The world health bars remain responsible for displaying remote health.

---

# Art Replaceability

The implementation should assume all prototype artwork may change.

The following must be swappable through Inspector/configuration:

* base bullseye;
* crack sprites;
* shattered state;
* damage flash if sprite-based;
* crack sounds;
* shatter sound;
* repair sounds.

No hardcoded asset paths.

---

# Suggested Component

Create a Bullseye-owned component such as:

`BullseyeHealthHud`

or:

`CrackedBullseyeHealthHud`

Exact naming may follow existing conventions.

Potential serialized fields:

```text
PlayerHealth Health Source

Image Base Bullseye

List<Image> Crack Layers

Image Shattered Overlay

AudioSource Audio Source

AudioClip[] Crack Sounds
AudioClip[] Repair Sounds
AudioClip Shatter Sound

Damage Shake Strength
Damage Shake Duration

Repair Fade Duration

Optional Flash Image
```

Exact implementation may differ.

---

# Optional Config Asset

If this becomes cleaner, presentation settings may be moved into a ScriptableObject such as:

`BullseyeHealthHudConfig`

Possible fields:

```text
Base Sprite
Crack Sprites[]
Shattered Sprite

Crack Sounds[]
Repair Sounds[]
Shatter Sound

Damage Animation Settings
Repair Animation Settings
```

This is optional.

For the prototype, serialized prefab/component fields are acceptable.

---

# Manual Playtest

## Test 1 — Full Health

Spawn.

Confirm:

* clean bullseye appears;
* no cracks;
* old local health bar does not compete with it.

---

## Test 2 — Lower Damage

Take a 2-damage hit.

Example:

```text
8 → 6
```

Confirm:

* appropriate cracking appears;
* damage feedback animation plays;
* one cracking sound plays if assigned.

---

## Test 3 — Torso Damage

From full health:

```text
8 → 4
```

Confirm:

* bullseye becomes substantially more damaged;
* presentation clearly communicates larger health loss.

---

## Test 4 — Head / Lethal Damage

Take lethal damage:

```text
8 → 0
```

Confirm:

* shattered presentation appears;
* shatter sound plays if assigned;
* respawn countdown remains visible.

---

## Test 5 — Regeneration

Take damage without dying.

Wait for existing regeneration.

Confirm:

```text
6 → 7
→ one stage repairs

7 → 8
→ bullseye returns clean
```

Repair timing must match actual health regeneration.

---

## Test 6 — Repair Sound

Assign a temporary repair sound.

Regenerate health.

Confirm:

* one appropriate sound plays per health restoration;
* no sound spam occurs.

---

## Test 7 — Damage During Repair

Take damage.

Begin regenerating.

Take another hit during regeneration.

Confirm:

* HUD immediately reflects current health;
* repair animation does not leave incorrect cracks;
* new crack sound plays.

---

## Test 8 — Respawn

Die.

Allow respawn.

Confirm:

* shattered state clears;
* clean full-health bullseye returns;
* no sequence of individual repair sounds plays.

---

## Test 9 — Empty Audio Slots

Remove all assigned HUD audio clips.

Play normally.

Confirm:

* no exceptions;
* health visualization continues working.

---

## Test 10 — Replace Asset

Swap the base bullseye sprite in Inspector.

Confirm:

* HUD works with replacement artwork;
* no code modification is required.

---

# Acceptance Criteria

## Core HUD

* [ ] Existing local health bar is replaced by the Bullseye Health HUD.
* [ ] Full health displays a clean bullseye.
* [ ] Damage progressively cracks the bullseye.
* [ ] All 8 HP states can be represented.
* [ ] 0 HP displays a distinct shattered state.
* [ ] HUD is owner-only.

## Regeneration

* [ ] Crack repair follows existing `PlayerHealth` regeneration.
* [ ] No independent repair timer is introduced.
* [ ] Crack damage decreases as health increases.
* [ ] Full health restores clean bullseye.
* [ ] Damage during regeneration displays correctly.
* [ ] Respawn immediately resets to full-health presentation.

## Audio

* [ ] Inspector provides an easily editable `Crack Sounds` collection.
* [ ] Multiple crack clips can be dragged into the collection.
* [ ] A random assigned crack sound can play when damage occurs.
* [ ] Inspector provides a dedicated shatter sound slot or collection.
* [ ] Inspector provides optional repair sound slot(s).
* [ ] Empty audio fields cause no errors.
* [ ] Final sound files can be added without code changes.

## Presentation

* [ ] Damage produces a small visual impact reaction.
* [ ] Repair has a subtle visual transition.
* [ ] Shattered state does not obscure respawn information.
* [ ] HUD does not interfere with the reticle.

## Existing Gameplay

* [ ] `PlayerHealth` remains authoritative.
* [ ] Damage values remain unchanged.
* [ ] Regen delay/rate remain unchanged.
* [ ] Death remains unchanged.
* [ ] Respawn remains unchanged.
* [ ] Enemy overhead health bars remain functional.
* [ ] Multiplayer remains functional.

## Replaceability

* [ ] Base bullseye artwork can be replaced through Inspector.
* [ ] Crack artwork can be replaced through Inspector.
* [ ] Shattered artwork can be replaced through Inspector.
* [ ] Audio can be replaced through Inspector.
* [ ] No asset paths are hardcoded.

---

# Agent Instructions

Before modifying code:

1. Inspect:

   * `PlayerHealth`;
   * existing local `PlayerHealthHud`;
   * world-space player health bars;
   * death/respawn HUD;
   * local player ownership checks.

2. Preserve the existing server-authoritative health system.

3. Replace only the local health presentation.

4. Build the HUD around replaceable art and audio references.

5. Do not wait for final bullseye/crack artwork.

Use placeholder UI graphics if necessary to establish the system.

6. Ensure Inspector fields are clearly named so a non-programmer can later drag in:

   * bullseye sprite;
   * crack sprites;
   * cracking WAV/MP3/OGG files;
   * shatter sound;
   * repair sound.

---

# Implementation Priority

1. Replace local health bar with base Bullseye image.
2. Bind HUD state to `PlayerHealth`.
3. Implement progressive crack layers.
4. Implement regeneration-driven repair.
5. Add shattered/death state.
6. Add damage shake/feedback.
7. Add crack audio slots.
8. Add shatter audio slot.
9. Add optional repair audio.
10. Validate respawn behavior.

---

# Out of Scope

REQ-021 does not implement:

* final bullseye artwork;
* final crack artwork;
* final audio assets;
* changing the world-space enemy health bars;
* changing health values;
* changing regeneration;
* changing damage amounts;
* damage to the actual world bullseye model;
* persistent scars between lives;
* different crack patterns per body damage zone;
* animated glass fragments;
* complex shaders;
* UI particle simulations.

Those can be considered later.

---

# Definition of Done

REQ-021 is complete when the local player's health no longer looks like a generic FPS health bar.

Instead:

```text
FULL HEALTH
      ↓
clean Bullseye

TAKE DAMAGE
      ↓
cracking + impact
      ↓
crack sound

MORE DAMAGE
      ↓
progressively fractured Bullseye

REGENERATE
      ↓
cracks gradually repair
at the exact PlayerHealth cadence

DEATH
      ↓
shattered Bullseye
      ↓
respawn countdown

RESPAWN
      ↓
clean Bullseye
```

All bullseye artwork, cracking artwork, and cracking/repair audio should be replaceable through clearly labeled Unity Inspector fields so final visual and sound assets can be added later without modifying code.
