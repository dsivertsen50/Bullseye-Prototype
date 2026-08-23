# REQ-025 — Modular Weapon Reticle, Bloom, and Accuracy System

## Summary

Replace the current fixed reticle with a modular, weapon-configurable reticle and accuracy system.

Each weapon should be able to define its own reticle behavior and firing accuracy characteristics. The default reticle style should consist of approximately four separate marks arranged around the center of the screen in a cross pattern:

* Top
* Bottom
* Left
* Right

The marks should **not intersect at the center**.

The space between these reticle marks represents the weapon's current firing spread.

As weapon accuracy decreases, the reticle should expand outward. As accuracy recovers, the reticle should move back toward the center.

Accuracy should currently be affected by:

1. Rapid firing
2. Sprinting

The weapon's actual hitscan direction must correspond to the current spread represented by the reticle.

This system must be designed so that future weapons can easily define different reticle sizes, bloom behavior, recovery rates, and accuracy characteristics without modifying the core reticle system.

---

# Goals

The system should:

* Provide visual feedback for weapon accuracy.
* Make rapid firing progressively less accurate where appropriate.
* Make sprinting temporarily reduce weapon accuracy.
* Allow accuracy to recover quickly after the player stops firing or sprinting.
* Ensure hitscan spread approximately corresponds to what the reticle communicates visually.
* Allow every weapon to have independently configurable reticle and bloom settings.
* Support substantially more weapons than the current three without requiring new reticle code for every weapon.
* Preserve multiplayer functionality.

---

# 1. Reticle Appearance

Replace the current static reticle with a four-part dynamic reticle.

Conceptually:

```text
       |
       
   —       —
       
       |
```

The four branches must remain separated from the center.

The exact visual style can remain simple for the prototype.

For example, each reticle branch may initially be:

* a short line,
* small rectangle,
* dot,
* or simple sprite.

The architecture should allow the visual asset to be replaced later.

---

# 2. Dynamic Reticle Gap

Each reticle has a configurable distance from the center of the screen.

Call this value something conceptually similar to:

```text
CurrentSpread
```

or:

```text
CurrentReticleGap
```

The four reticle elements should move symmetrically outward as this value increases.

For example:

```text
Low spread:

      |
    —   —
      |
```

```text
High spread:

         |

    —         —

         |
```

The reticle should animate smoothly between these states rather than instantly jumping between positions.

---

# 3. Weapon-Specific Reticle Configuration

Reticle behavior must be stored as weapon configuration rather than hard-coded by weapon name.

Prefer adding configuration fields to the existing weapon data/configuration system if one already exists.

If appropriate, this may use:

* weapon ScriptableObjects,
* weapon configuration components,
* serializable weapon settings,
* or the project's existing weapon architecture.

Each weapon should be able to configure at least the following values:

```text
Minimum Reticle Spread
Maximum Reticle Spread

Bloom Per Shot
Bloom Recovery Rate
Bloom Recovery Delay

Sprint Spread
Sprint Recovery Rate

Maximum Accuracy Spread

Reticle Visual / Sprite
Reticle Element Size
```

Exact naming may differ depending on the existing project architecture.

Do not require all weapons to use identical values.

---

# 4. Base Weapon Accuracy

Each weapon should have a minimum/base spread.

When the player:

* is not sprinting,
* has not recently fired,
* and the weapon has fully recovered,

the reticle should return to this base value.

Some weapons may eventually be extremely accurate and have a very tight minimum reticle.

Others may naturally have a wider starting spread.

---

# 5. Firing Bloom

Each shot should temporarily increase the weapon's spread.

Conceptually:

```text
CurrentSpread += BloomPerShot
```

The value must be capped at the weapon's configured maximum spread.

Example:

```text
Shot 1
|   |

Shot 2
|     |

Shot 3
|       |

Shot 4
|         |
```

Rapidly firing should therefore make the reticle progressively wider.

Slower firing should allow some or all accuracy to recover between shots.

---

# 6. Bloom Recovery

After firing stops, the reticle should begin returning toward the weapon's normal accuracy.

The recovery should be smooth.

Weapons should have independently configurable:

```text
BloomRecoveryDelay
BloomRecoveryRate
```

Example behavior:

```text
Player rapidly fires pistol.

Spread:
10 → 14 → 18 → 22 → 26

Player stops firing.

brief delay

26 → 22 → 18 → 14 → 10
```

The recovery should generally feel fast enough that the mechanic does not make weapons sluggish.

Exact values will be tuned through playtesting.

---

# 7. Hitscan Spread

The reticle must not merely be cosmetic.

When spread increases, the hitscan direction should also become less accurate.

Instead of every shot always traveling exactly through the screen center, the shot should select a randomized direction within the weapon's current spread cone.

Conceptually:

```text
Perfect accuracy:

Camera forward
      |
      |
      X
```

With bloom:

```text
Possible shot region:

      \ | /
       \|/
     ---X---
       /|\
      / | \
```

The exact implementation may use:

* randomized camera-space offsets,
* a spread cone,
* randomized screen-space points,
* or another mathematically equivalent solution.

---

# 8. Reticle Must Represent Possible Shot Area

The displayed reticle should approximately represent the maximum region in which a hitscan shot may travel.

Shots should **not routinely land visually outside the reticle**.

The system does not need pixel-perfect mathematical correspondence during the prototype, but the player's visual expectation should be accurate.

If the reticle is tight:

> The shot should be highly accurate.

If the reticle is wide:

> The shot may land anywhere within approximately that region.

---

# 9. Spread Distribution

Random hitscan spread should not simply choose between a few fixed directions.

Use a randomized spread distribution within the permitted area.

Prefer a system that avoids shots disproportionately appearing only at the outer edge of the spread.

The implementation should produce believable shot variation across the available accuracy cone.

---

# 10. Sprint Accuracy Penalty

While sprinting, the reticle should expand.

The sprint spread should have a configurable maximum or target value.

Example:

```text
Standing:
      |
    —   —
      |

Sprinting:

         |
    —         —
         |
```

This communicates that firing while sprinting is significantly less accurate.

The weapon may still fire while sprinting unless another existing mechanic prevents firing.

REQ-025 should not independently change whether sprint firing is allowed.

---

# 11. Sprint Spread Transition

The reticle should not instantly jump to its maximum sprint spread.

While sprinting:

```text
CurrentSpread → SprintSpread
```

The reticle should expand smoothly.

This expansion can happen relatively quickly.

---

# 12. Post-Sprint Accuracy Recovery

When the player stops sprinting, the reticle should not instantly snap back to perfect accuracy.

Instead, accuracy should recover over a short period.

Target feel:

```text
Player stops sprinting

Wide reticle
     ↓

~very brief recovery

Normal reticle
```

The intent is that a player cannot:

```text
Sprint → instantly stop → perfectly accurate shot
```

There should instead be a small transition period before maximum accuracy returns.

This delay should be noticeable enough to create meaningful gameplay but short enough that movement still feels responsive.

Exact timing should be configurable.

---

# 13. Firing While Sprinting

Firing bloom and sprint bloom must interact correctly.

For example:

```text
Base Spread
    +
Sprint Penalty
    +
Firing Bloom
```

The system does not necessarily need to calculate these values literally through addition, but their effects must combine appropriately.

Rapidly firing while sprinting should result in greater spread than either:

* sprinting alone, or
* rapid firing while stationary.

The resulting spread must still respect the weapon's configured maximum.

---

# 14. Maximum Spread

Each weapon must define a maximum spread.

Regardless of:

* number of shots fired,
* sprint duration,
* or combined bloom effects,

the reticle must stop expanding once this maximum is reached.

Conceptually:

```text
CurrentSpread = Clamp(
    calculatedSpread,
    MinimumSpread,
    MaximumSpread
)
```

This prevents the reticle from becoming unusably large.

---

# 15. Weapon Switching

When switching weapons, the reticle must immediately begin using the newly equipped weapon's configuration.

For example:

```text
Pistol
tight reticle
moderate bloom

→ switch →

Shotgun
wide reticle
different visual settings
different bloom
```

Weapon-specific data must not leak between weapons.

The newly selected weapon should use its own:

* minimum spread,
* maximum spread,
* bloom per shot,
* recovery behavior,
* sprint spread,
* reticle appearance.

---

# 16. Recommended Weapon Configuration Structure

Prefer creating a reusable configuration structure similar conceptually to:

```text
WeaponAccuracySettings
```

Possible fields:

```text
baseSpread
maxSpread

spreadPerShot
shotSpreadRecoveryDelay
shotSpreadRecoverySpeed

sprintSpread
sprintSpreadIncreaseSpeed
sprintSpreadRecoverySpeed

reticleSprite
reticleElementLength
reticleElementThickness
```

This is conceptual rather than prescriptive.

Use architecture consistent with the existing project.

---

# 17. Reticle Controller

Prefer having a centralized component responsible for presenting the current weapon spread.

For example:

```text
WeaponReticleController
```

Its responsibilities may include:

* determining the equipped weapon,
* reading its accuracy configuration,
* positioning the four reticle elements,
* smoothly animating their gap,
* updating the reticle when weapons change.

The reticle UI should not contain weapon-specific logic such as:

```text
if pistol...
if rifle...
if shotgun...
```

Weapon differences must come from configuration data.

---

# 18. Accuracy / Spread Controller

The gameplay accuracy calculation should ideally be separated from the visual reticle.

Conceptually:

```text
WeaponAccuracyController
        |
        ├── Determines current spread
        |
        ├── Used by hitscan
        |
        └── Reported to reticle UI
```

This keeps the visual reticle from becoming the source of truth for gameplay.

The gameplay system should determine current spread.

The reticle should visualize that value.

---

# 19. Multiplayer Authority

Hitscan and damage must continue to follow the project's existing multiplayer authority model.

Do not move authoritative hit detection entirely to UI or local-only code.

The firing player may calculate or request a spread direction according to the existing networking architecture, but the implementation must not break:

* damage synchronization,
* health,
* kills,
* respawns,
* bullseye detection,
* multiplayer player ownership.

Remote players do not need to see another player's reticle.

Reticles remain local first-person UI.

---

# 20. Existing Bullseye Hit Detection

The new spread system must continue to interact correctly with the Bullseye vulnerability mechanic.

A shot should:

1. Determine its spread-adjusted firing direction.
2. Perform the existing hitscan.
3. Determine what was struck.
4. Apply the existing body/bullseye hit logic.
5. Apply damage according to the existing health system.

REQ-025 must not bypass or replace the current bullseye hit detection system.

---

# 21. Current Weapons

Apply the system to all currently usable weapons.

The initial values do not need to be perfectly balanced.

Use reasonable prototype values that make the differences observable.

The important requirement is that each weapon is using configurable settings rather than hard-coded shared values.

---

# 22. Future Weapon Support

Adding a new weapon should ideally require only:

1. Creating/importing the weapon.
2. Assigning its existing weapon configuration.
3. Configuring its accuracy/reticle values.
4. Assigning an optional reticle visual.

It should **not** require editing the main reticle script.

---

# 23. Inspector Tuning

Expose gameplay-relevant values in the Unity Inspector or existing weapon data editor so they can be adjusted without changing code.

At minimum, expose:

```text
Base Spread
Maximum Spread

Bloom Per Shot
Bloom Recovery Delay
Bloom Recovery Speed

Sprint Spread
Sprint Recovery Speed
```

Tooltips are encouraged where useful.

---

# 24. Debugging Support

During development, optional debugging information may be added.

Useful values include:

```text
Current Spread
Base Spread
Shot Bloom
Sprint Bloom
Maximum Spread
```

If debug visualization is created, it should be easy to disable and should not appear in normal gameplay builds unless explicitly enabled.

---

# Acceptance Criteria

REQ-025 is complete when all of the following are true:

* [ ] The static reticle has been replaced with a dynamic four-part reticle.
* [ ] The four reticle elements do not intersect at screen center.
* [ ] Each weapon can define its own minimum reticle spread.
* [ ] Each weapon can define its own maximum spread.
* [ ] Each weapon can define its own bloom per shot.
* [ ] Rapid firing visibly expands the reticle.
* [ ] Reticle expansion stops at the weapon's configured maximum.
* [ ] Stopping fire allows the reticle to smoothly recover.
* [ ] Bloom recovery timing is configurable.
* [ ] Sprinting expands the reticle.
* [ ] Stopping sprint causes a brief, smooth accuracy recovery rather than an instant reset.
* [ ] Firing and sprint bloom interact correctly.
* [ ] Hitscan direction varies according to the current weapon spread.
* [ ] Hitscan shots remain approximately within the area communicated by the reticle.
* [ ] When spread is at minimum, the weapon fires with its configured maximum accuracy.
* [ ] Weapon switching updates the reticle to the new weapon's configuration.
* [ ] Weapon switching does not carry inappropriate bloom configuration from the previous weapon.
* [ ] Existing bullseye hit detection continues to work.
* [ ] Existing damage behavior continues to work.
* [ ] Existing multiplayer behavior continues to work.
* [ ] Reticle UI remains local to each player's first-person view.
* [ ] Current weapons use the modular configuration system.
* [ ] Adding a future weapon does not require modifying the core reticle implementation.
* [ ] Accuracy and bloom values can be tuned from the Inspector or the existing weapon configuration asset.

---

# Out of Scope

Do not add the following as part of REQ-025 unless required by the existing architecture:

* recoil animations,
* camera recoil,
* bullet drop,
* projectile ballistics,
* wind,
* aim-down-sights reticles,
* weapon attachments,
* laser sights,
* scopes,
* crosshair color customization,
* hit markers,
* headshot indicators,
* suppression mechanics,
* movement penalties other than sprinting,
* crouch accuracy bonuses,
* jumping accuracy penalties.

These can be addressed by later requirements.

---

# Design Intent

The reticle should become a readable representation of the player's current weapon accuracy.

The player should intuitively learn:

```text
Tight reticle
=
accurate shot
```

```text
Wide reticle
=
less predictable shot
```

Rapid firing trades precision for fire rate.

Sprinting trades immediate accuracy for mobility.

After either behavior stops, the player only needs to wait a very short period before full accuracy returns.

Most importantly, this should be a **general weapon accuracy framework**, not a special effect built specifically for the three weapons currently in the prototype.
