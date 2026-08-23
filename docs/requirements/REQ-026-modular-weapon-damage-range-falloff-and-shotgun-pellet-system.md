# REQ-026 — Modular Weapon Damage, Range Falloff, and Shotgun Pellet System

## Summary

Replace the current simplified weapon damage behavior with a modular, weapon-specific damage system.

Each weapon must be able to define:

* base damage,
* effective range,
* damage falloff over distance,
* minimum damage,
* and weapon-specific damage behavior.

The current weapons should initially follow these general balance goals:

* **Pistol:** baseline damage weapon; moderate damage and moderate range.
* **AK / Rifle:** approximately half the per-shot damage of the pistol, compensated for by substantially higher fire rate.
* **Shotgun:** potentially approximately three times the total close-range damage of the pistol, but damage should decrease substantially with distance.

These values are intended as **starting balance targets**, not permanent final values.

The architecture must support future weapons without requiring weapon-specific damage logic to be added to the main damage system.

---

# Goals

The damage system should:

* Make each weapon feel meaningfully different.
* Allow damage to vary with distance.
* Support high-damage, short-range weapons such as shotguns.
* Support low-damage, high-fire-rate weapons such as rifles.
* Preserve the existing Bullseye vulnerability mechanic.
* Preserve the existing body-location damage concept.
* Make weapon balance easy to tune from Unity.
* Support future weapons through configuration rather than hard-coded logic.
* Continue functioning correctly in multiplayer.

---

# 1. Weapon Damage Profiles

Every weapon should have its own configurable damage profile.

Prefer extending the existing weapon data/configuration system.

Conceptually, each weapon should contain a structure similar to:

```text
WeaponDamageSettings
```

Possible values include:

```text
Base Damage
Maximum Damage
Minimum Damage

Effective Range
Maximum Range

Damage Falloff Start
Damage Falloff End

Damage Falloff Curve

Pellet Count
Damage Per Pellet
```

Not every weapon needs to use every field.

For example, pellet-related values may only apply to shotgun-type weapons.

---

# 2. Baseline Damage Unit

Use the pistol as the initial reference point for balancing other weapons.

Conceptually:

```text
Pistol Damage = 1.0 baseline damage unit

AK Damage = ~0.5 pistol damage per bullet

Shotgun Close-Range Potential = ~3.0 pistol damage
```

These ratios should be configurable.

Do **not** hard-code calculations such as:

```text
AK = PistolDamage / 2
```

Instead, assign each weapon its own damage values.

The ratios above are only initial balancing guidelines.

---

# 3. Initial Pistol Behavior

The pistol should function as the baseline general-purpose weapon.

Suggested characteristics:

* moderate per-shot damage,
* moderate effective range,
* modest distance falloff,
* accurate single-shot damage.

Conceptually:

```text
Close range:
100% damage

Medium range:
approximately 80–100%

Long range:
reduced damage
```

The exact distance thresholds should be exposed for tuning.

The pistol should remain useful at moderate range.

---

# 4. Initial AK / Rifle Behavior

The AK should have significantly lower damage per bullet than the pistol.

Initial target:

```text
AK bullet ≈ 50% of pistol bullet damage
```

The AK compensates through:

* faster rate of fire,
* larger magazine,
* ability to land multiple shots rapidly.

The AK may have a similar or somewhat longer useful range than the pistol.

Its damage falloff may therefore begin later than the shotgun and potentially later than the pistol.

Exact values must remain configurable.

---

# 5. Initial Shotgun Behavior

The shotgun should be extremely dangerous at close range but rapidly become ineffective at longer ranges.

Initial balancing goal:

```text
Maximum close-range shotgun damage
≈ 3 × pistol damage
```

However, this value should represent the potential damage when a significant number of pellets connect.

The shotgun should not behave like one extremely powerful hitscan ray.

Instead, it should preferably fire multiple pellet hitscan rays.

---

# 6. Shotgun Pellet System

The shotgun should support configurable pellet count.

Example:

```text
Pellet Count = 8
```

Each pellet should:

1. receive its own spread direction,
2. perform its own hitscan,
3. determine what it struck,
4. calculate distance,
5. calculate pellet damage,
6. contribute to total damage.

Conceptually:

```text
Shotgun fires:

• • •
 • •
• • •
```

At close range, many pellets may hit the same target.

At longer range, the pellet spread causes fewer pellets to connect.

This naturally reduces effective damage with distance.

---

# 7. Pellet Damage

Each shotgun pellet should have configurable base damage.

For example:

```text
8 pellets × pellet damage
=
maximum theoretical close-range shotgun damage
```

The total maximum shotgun damage should be configurable independently of the number of pellets.

The implementation may calculate this through:

```text
damagePerPellet
```

or another equivalent configuration.

---

# 8. Distance-Based Damage

Damage should be calculated according to the distance between:

* the firing origin,
* and the point struck.

Each weapon must be able to define its own distance behavior.

Conceptually:

```text
0m -------- Falloff Start -------- Falloff End
|              |                       |
Full Damage    Gradually Reduced       Minimum Damage
```

---

# 9. Damage Falloff

Weapons should support progressive damage reduction over distance.

Avoid a system where damage abruptly changes at one exact distance unless necessary.

Prefer smooth falloff.

Conceptually:

```text
DamageMultiplier = EvaluateFalloff(distance)
```

Example:

```text
0–10 meters:
100% damage

10–25 meters:
100% → 60%

25+ meters:
60% or configured minimum
```

These numbers are examples only.

---

# 10. Configurable Falloff Curve

If practical, allow weapon damage falloff to use a Unity `AnimationCurve` or equivalent configurable curve.

Example:

```text
Normalized Distance
0.0 -------------------------- 1.0

Damage
1.0
 |
 |\
 | \
 |  \
 |    \
 |      0.4
```

This allows substantially different range profiles without code changes.

For example:

### Pistol

Gradual falloff.

### AK

Relatively flat damage across moderate range.

### Shotgun

Very aggressive falloff.

---

# 11. Minimum Damage

Weapons may define a minimum damage amount or minimum damage multiplier.

This prevents very long-distance hits from producing unintended values.

Example:

```text
Pistol:
minimum multiplier = 0.5

AK:
minimum multiplier = 0.6

Shotgun:
minimum multiplier = 0.1
```

These values are examples only.

---

# 12. Maximum Effective Range

Weapons should optionally define a maximum effective range.

Beyond this distance, behavior may be configured as either:

* no damage,
* minimum damage,
* or an extremely small amount of damage.

For the prototype, prefer the simplest implementation consistent with the existing hitscan system.

The shotgun should have a relatively short effective range.

---

# 13. Relationship to Existing 8-Health System

The player should continue to have:

```text
8 health units
```

Do not replace the existing health system.

Weapon damage should feed into it.

Conceptually:

```text
Weapon Base Damage
        ↓
Distance Modifier
        ↓
Hit Location Modifier / Bullseye Logic
        ↓
Final Damage
        ↓
Player Health
```

---

# 14. Hit Location

Preserve the existing distinction between:

* head,
* torso,
* lower body.

The weapon damage system should work together with the existing hit-location system rather than replacing it.

Prefer expressing body damage through configurable multipliers where practical.

Conceptually:

```text
FinalDamage =
WeaponDamage
× DistanceMultiplier
× HitLocationMultiplier
```

---

# 15. Bullseye Vulnerability

The Bullseye remains the primary vulnerability mechanic.

REQ-026 must not remove or bypass it.

If the current system requires the Bullseye to be struck for meaningful damage, preserve that behavior.

Damage calculation should still know which body region the Bullseye currently occupies.

For example:

```text
Bullseye on Head
      ↓
Head Damage Rules

Bullseye on Torso
      ↓
Torso Damage Rules

Bullseye on Lower Body
      ↓
Lower-Body Damage Rules
```

---

# 16. Preserve Headshot Lethality

The current intended health design allows a valid head/Bullseye hit to potentially kill a full-health player.

Preserve this capability.

Do not allow distance falloff to unintentionally make the intended headshot behavior impossible unless this is explicitly configured.

If necessary, allow the Bullseye/head combination to apply:

* a special multiplier,
* minimum lethal damage,
* or existing headshot logic.

Use the project's existing implementation where possible.

---

# 17. Torso and Lower-Body Balance

The original health targets were approximately:

```text
Head:
8 damage

Torso:
4 damage

Lower Body:
2 damage
```

These should now be interpreted as **baseline balancing targets**, primarily for the pistol or equivalent reference weapon.

Other weapons may differ.

For example, a lower-damage AK bullet should not automatically inflict the same 4 torso damage as a pistol shot.

Instead, weapon damage should contribute to the resulting damage.

---

# 18. Recommended Damage Model

Prefer a calculation conceptually similar to:

```text
BaseWeaponDamage
        ×
DistanceMultiplier
        ×
HitLocationMultiplier
        =
FinalDamage
```

For shotgun pellets:

```text
PelletDamage
        ×
DistanceMultiplier
        ×
HitLocationMultiplier
        =
PelletFinalDamage
```

Then:

```text
TotalShotgunDamage =
Sum of successful pellet damage
```

Exact implementation may vary according to the existing architecture.

---

# 19. Example Prototype Balance

The following values are examples for initial implementation only.

They must remain configurable.

Assume the pistol is the reference weapon:

```text
PISTOL

Base Damage:
1.0 reference value

Range:
Medium

Falloff:
Gradual
```

```text
AK

Base Damage:
~0.5 × pistol per bullet

Range:
Medium to long

Falloff:
Relatively gradual

Compensation:
High fire rate
```

```text
SHOTGUN

Maximum close-range total:
~3.0 × pistol shot

Range:
Short

Falloff:
Very aggressive

Compensation:
Multiple pellets / high close-range damage
```

These ratios should be tuned after playtesting.

---

# 20. Damage and Reticle Integration

REQ-026 should work correctly with the spread system introduced in REQ-025.

For the pistol and AK:

```text
Reticle / bloom determines possible hitscan direction.
```

For the shotgun:

```text
Shotgun spread determines pellet directions.
```

The shotgun pellet pattern should fit approximately within the weapon's displayed reticle/spread area.

---

# 21. Shotgun Spread

The shotgun should have a configurable pellet spread.

Each pellet should receive a randomized direction inside the configured spread cone.

Do not fire every pellet down exactly the same ray.

Example:

```text
Close target:

    [ TARGET ]

     •••••
      •••
```

Many pellets hit.

At distance:

```text
           •
     •             •

         TARGET

   •             •
          •
```

Fewer pellets hit.

This should be an important component of shotgun range limitation.

---

# 22. Shotgun Distance Falloff

Pellet spread alone may not sufficiently restrict shotgun range.

Therefore shotgun pellets should also support aggressive damage falloff.

The combination should be:

```text
Increasing distance
       ↓

Greater pellet spread
       +

Lower pellet damage
       ↓

Rapid reduction in total damage
```

This is intentional.

The shotgun should strongly reward close-range engagements.

---

# 23. Prevent Excess Shotgun Damage

Shotgun damage must be capped by its intended pellet count and damage configuration.

A single pellet should never incorrectly apply the damage of the entire shotgun blast.

Verify that:

```text
1 pellet hit
≠
full shotgun damage
```

and:

```text
8 pellet hits
=
approximately 8 × pellet damage
```

subject to distance and hit-location calculations.

---

# 24. Multiple Pellets Hitting the Same Player

Multiple shotgun pellets may hit the same player during a single shot.

This is expected.

Their damage should accumulate correctly.

However, make sure a single pellet cannot accidentally damage the same target multiple times through duplicate collision processing.

---

# 25. Death Processing

If one attack produces enough damage to kill a player, death should trigger only once.

This is particularly important for shotgun blasts where several pellets may arrive during the same frame.

Avoid:

* multiple death triggers,
* multiple kill counts,
* repeated respawn calls,
* multiple death audio events.

---

# 26. Weapon Switching

When the player switches weapons, the newly equipped weapon's damage profile must become active immediately.

For example:

```text
Pistol
    ↓
Switch
    ↓
AK

AK damage settings now apply.
```

Damage settings must not leak between weapons.

---

# 27. Future Weapon Support

The system must be designed for many additional weapons.

Examples may eventually include:

* sniper rifle,
* SMG,
* revolver,
* burst rifle,
* different shotguns,
* heavy weapons.

Adding these weapons should normally require only configuring their damage profile.

The main damage calculation should not require modifications for every new gun.

---

# 28. Weapon Archetypes

The architecture may optionally support reusable archetypes such as:

```text
Single Hitscan
Automatic Hitscan
Pellet Hitscan
```

Do not hard-code logic specifically around:

```text
Pistol
AK
Shotgun
```

Prefer behavior types or configuration.

---

# 29. Inspector Configuration

Damage values should be exposed through the Unity Inspector or the existing weapon configuration assets.

At minimum, expose:

```text
Base Damage
Effective Range
Falloff Start
Falloff End
Minimum Damage / Minimum Multiplier
```

For pellet weapons:

```text
Pellet Count
Pellet Damage
Pellet Spread
```

If using an `AnimationCurve`, expose the falloff curve as well.

---

# 30. Debug Damage Information

Add optional development/debug logging or visualization if useful.

Potential debug output:

```text
Weapon: Shotgun
Distance: 7.2m
Pellets Fired: 8
Pellets Hit: 6
Damage Before Falloff: 6.0
Distance Multiplier: 0.82
Final Damage: 4.92
```

This should be easy to disable.

Do not leave excessive logging enabled in production gameplay.

---

# 31. Multiplayer Authority

Damage must continue to follow the existing multiplayer authority model.

The local client should not be able to independently and authoritatively assign arbitrary damage.

Preserve the existing networked sequence for:

* firing,
* hit detection,
* damage,
* health,
* death,
* kill attribution,
* respawn.

Do not break multiplayer synchronization while implementing REQ-026.

---

# 32. Kill Attribution

The weapon and player responsible for damage should remain identifiable.

The damage system should preserve enough context to support future features such as:

* kill feed,
* damage statistics,
* weapon statistics,
* assists,
* post-match results.

Implementing those systems is not required by REQ-026.

---

# 33. Damage Data Structure

If practical, centralize information about a damage event.

Conceptually:

```text
DamageInfo
```

may eventually contain:

```text
Attacker
Victim
Weapon
Base Damage
Final Damage
Hit Point
Hit Region
Distance
Was Bullseye Hit
Was Headshot
Pellet Index
```

This is optional architecture guidance rather than a strict implementation requirement.

Avoid unnecessary complexity if the existing system already provides equivalent information.

---

# Acceptance Criteria

REQ-026 is complete when:

* [ ] Each weapon has independently configurable base damage.
* [ ] Damage values are not hard-coded by weapon name.
* [ ] Damage can vary according to shot distance.
* [ ] Each weapon can independently configure damage falloff.
* [ ] Damage falloff occurs smoothly or through an equivalent configurable implementation.
* [ ] The pistol functions as the initial baseline damage weapon.
* [ ] The AK deals approximately half the pistol's per-shot damage in the initial prototype configuration.
* [ ] The AK compensates through its existing higher fire rate.
* [ ] The shotgun can produce approximately three times pistol-level damage at very close range when enough pellets connect.
* [ ] The shotgun fires multiple independently calculated pellet hitscans.
* [ ] Each shotgun pellet has its own damage calculation.
* [ ] Shotgun pellets spread apart according to the shotgun spread configuration.
* [ ] Fewer shotgun pellets generally connect as distance increases.
* [ ] Shotgun pellet damage also decreases meaningfully with distance.
* [ ] A single shotgun pellet cannot accidentally apply full-blast damage.
* [ ] Multiple pellets can correctly damage the same player.
* [ ] A lethal shotgun blast triggers player death only once.
* [ ] The existing 8-health system remains functional.
* [ ] Existing Bullseye hit detection remains functional.
* [ ] Existing head/torso/lower-body behavior remains functional.
* [ ] Intended lethal Bullseye/headshot behavior remains possible.
* [ ] REQ-025 reticle/spread behavior remains compatible with hit detection.
* [ ] Weapon switching correctly changes damage profiles.
* [ ] Damage values are tunable from the Inspector or weapon configuration asset.
* [ ] Existing multiplayer damage synchronization continues to work.
* [ ] Future weapons can receive unique damage profiles without modifying the main damage system.

---

# Initial Balancing Philosophy

Weapon effectiveness should primarily come from different combinations of:

```text
Damage
+
Fire Rate
+
Accuracy
+
Range
+
Spread
```

Rather than making every gun statistically similar.

The initial three weapons should occupy clearly different roles:

```text
PISTOL

Reliable
Moderate damage
Moderate range
Moderate fire rate
```

```text
AK

Low damage per bullet
High fire rate
Sustained pressure
Useful at moderate range
```

```text
SHOTGUN

Extremely dangerous nearby
Multiple pellets
Rapid damage loss with distance
Poor long-range effectiveness
```

The exact numbers should remain easy to tune after multiplayer playtesting.

---

# Out of Scope

Do not add the following unless required by the existing architecture:

* armor,
* armor penetration,
* elemental damage,
* explosive damage,
* critical-hit chance,
* limb injuries,
* bleeding,
* damage-over-time,
* projectile bullet drop,
* penetration through walls,
* ricochets,
* suppressors,
* ammunition types,
* weapon attachments,
* friendly fire settings,
* kill feed,
* assists,
* detailed post-match damage statistics.

These can be addressed in later requirements.

---

# Design Intent

REQ-026 should establish a general-purpose damage framework rather than simply assigning three fixed numbers to the current weapons.

A weapon should ultimately be definable through data such as:

```text
How hard does it hit?

How quickly does damage decrease with distance?

How accurate is it?

How quickly can it fire?

Does it fire one ray or many pellets?
```

This gives future weapons room to feel substantially different while continuing to use the same underlying combat system.
