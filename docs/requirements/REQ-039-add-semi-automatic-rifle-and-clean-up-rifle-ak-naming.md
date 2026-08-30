# REQ-039 — Add DMR Semi-Automatic Rifle & Clean Up Rifle/AK Naming

## Summary

Add a new **semi-automatic rifle** to the game using the newly imported DMR model and textures.

The model/assets are currently named using **DMR**, and the weapon should be referred to as the **DMR** throughout the project for now.

The existing AK weapon currently uses a definition/configuration named approximately:

`RifleDefinition`

This naming is becoming misleading because the game will now contain multiple rifles.

As part of this ticket:

1. Add the new DMR weapon.
2. Initially base its gameplay statistics on the existing AK/RifleDefinition.
3. Make the DMR **semi-automatic rather than fully automatic**.
4. Clean up misleading AK/Rifle naming where doing so can safely be accomplished.
5. Do not break existing serialized Unity references, prefabs, scenes, or networked weapon functionality during the naming cleanup.

---

# Assets

The new weapon assets are named around:

`DMR`

This may include:

* `DMR.fbx`
* DMR diffuse/albedo texture
* DMR roughness texture
* Any additional DMR textures supplied with the model

Preferred project organization:

```text
Assets/
└── Weapons/
    └── DMR/
        ├── Models/
        ├── Materials/
        ├── Textures/
        ├── Prefab/
        └── Definitions/
```

Do not unnecessarily move existing assets if doing so would create broken references.

Follow the organizational conventions already being used by:

* Pistol
* AK
* Shotgun

where practical.

---

# 1. Create the DMR Weapon Prefab

Create a usable weapon prefab from the imported DMR model.

Example:

```text
DMR
└── Prefab
    └── DMR.prefab
```

The prefab should follow the same structural conventions as the existing AK weapon wherever applicable.

This may include:

* Weapon root
* Model
* Fire/muzzle point
* Pickup components
* Network components
* Weapon scripts
* Colliders
* Audio source(s)
* Definition/configuration reference
* First-person representation
* Third-person/world representation

Reuse the current weapon architecture.

Do not create a completely separate DMR weapon system.

---

# 2. DMR Material Setup

Create or configure a material for the DMR.

Suggested name:

`MAT_DMR`

Use the same render pipeline/shader convention as the existing weapons.

For HDRP this will most likely be:

`HDRP/Lit`

Assign the supplied DMR diffuse texture to the equivalent of:

`Base Map`

The supplied roughness map should be configured appropriately for the shader being used.

Important:

Unity HDRP commonly uses **Smoothness** rather than Roughness.

Conceptually:

```text
Smoothness = 1 - Roughness
```

Therefore, do not blindly treat a roughness texture as a smoothness map without checking the current material workflow.

Prefer matching the existing AK/Shotgun/Pistol material setup if it is already visually correct.

The goal of this ticket is simply to ensure the DMR renders with its intended textures.

---

# 3. Create DMR Weapon Definition

Create a new weapon definition/configuration for the DMR.

Suggested naming:

`DMRDefinition`

or, if the project uses a more explicit convention:

`DMRWeaponDefinition`

The DMR definition should initially copy the gameplay values currently used by the AK's `RifleDefinition`.

For now, duplicate values such as:

* Damage
* Range
* Magazine capacity
* Reserve ammunition
* Reload duration
* Spread
* Hip-fire accuracy
* Aim accuracy
* Recoil
* Fire rate/cooldown
* Pickup behavior
* Weapon slot behavior
* Any existing distance falloff values
* Any movement modifiers
* Any reticle configuration

These are starting values only.

We will tune the DMR separately later.

---

# 4. DMR Must Be Semi-Automatic

The primary gameplay difference between the AK and DMR in this ticket is firing behavior.

## AK

Should remain:

**Fully Automatic**

Holding the fire button may continue firing according to its existing fire rate.

## DMR

Must be:

**Semi-Automatic**

One trigger/button press should fire **one shot**.

Holding the fire input should NOT repeatedly fire the weapon.

The player must release the fire input and press it again to fire another shot.

Conceptually:

```text
Press Fire
    ↓
Fire one DMR round

Continue holding Fire
    ↓
Do not fire again

Release Fire
    ↓
Weapon becomes eligible for another shot

Press Fire again
    ↓
Fire next round
```

This behavior must work with:

* Mouse
* Gamepad
* Multiplayer host
* Multiplayer client

---

# 5. Preserve Fire-Rate Limiting

Semi-automatic does not mean unlimited click speed.

The DMR should still respect its weapon definition's firing cooldown/fire-rate value.

For example:

```text
Player clicks
    ↓
Shot fires

Player clicks again before cooldown ends
    ↓
No shot

Cooldown completes
    ↓
Next valid press may fire
```

Initially, copy the AK's relevant fire-rate setting unless the current architecture requires a different value for reliable semi-auto behavior.

We can rebalance this later.

---

# 6. Clean Up Existing AK Naming

The existing AK appears to currently use names such as:

`RifleDefinition`

This was acceptable when it was the game's only rifle, but it is now ambiguous.

Clean up AK-specific instances where safe.

Preferred naming:

```text
RifleDefinition
        ↓
AKDefinition
```

or:

```text
AKWeaponDefinition
```

Likewise, if there are assets named generically:

```text
Rifle.prefab
RifleDefinition.asset
RiflePickup
RifleIcon
```

but they actually represent the AK specifically, rename them to clearly identify the AK.

Examples:

```text
AK.prefab
AKDefinition.asset
AKPickup
AKIcon
```

---

# 7. IMPORTANT — Do Not Rename Truly Generic Rifle Code

Before performing bulk renames, determine whether a name represents:

1. An **AK-specific asset**, or
2. A **generic rifle system/class**

Do not rename generic architecture merely because the current AK uses it.

For example, if:

```csharp
RifleDefinition
```

is a generic C# class designed to support multiple rifles, it may be appropriate to keep that class named `RifleDefinition`.

In that scenario:

```text
Class:
RifleDefinition
```

may remain generic, while the actual ScriptableObject assets become:

```text
AKDefinition
DMRDefinition
```

However, if `RifleDefinition` contains explicitly AK-specific behavior or is effectively an AK-only class, rename/refactor it appropriately.

The goal is:

**Clear naming without unnecessary architectural churn.**

---

# 8. Preserve Unity References During Renaming

Unity serialized references must remain intact.

When renaming assets:

* Rename/move them through Unity-compatible operations.
* Preserve GUID/meta relationships.
* Do not delete an asset and create an unrelated replacement solely to change its name.
* Verify prefabs still reference their weapon definitions.
* Verify scenes still reference the correct prefabs.
* Verify weapon pickup references remain valid.

If renaming a C# class is necessary, carefully update:

* File name
* Class name
* ScriptableObject attributes
* Serialized references
* Inspector references
* Prefab references
* Any switch statements or enum mappings
* Network weapon IDs
* Weapon inventory logic

Do not allow naming cleanup to break the working AK.

---

# 9. Add DMR to Weapon Identification

Wherever the project identifies weapon types, add the DMR.

For example, if an enum exists:

```csharp
WeaponType
{
    Pistol,
    AK,
    Shotgun
}
```

update it appropriately:

```csharp
WeaponType
{
    Pistol,
    AK,
    Shotgun,
    DMR
}
```

Do not add a new enum solely for this ticket if the existing architecture does not require one.

Follow the existing weapon identification system.

---

# 10. DMR Inventory Behavior

The DMR should follow the current secondary-weapon rules.

The player continues to have:

**Permanent weapon**

* Pistol

and one acquired secondary weapon such as:

* AK
* Shotgun
* DMR

The DMR should therefore behave as a secondary weapon.

If a player currently has:

```text
Pistol + AK
```

and picks up a DMR, the existing secondary weapon replacement/swap behavior should occur.

Likewise:

```text
Pistol + Shotgun
```

may become:

```text
Pistol + DMR
```

according to the existing pickup rules.

Do not allow the DMR to create an additional third weapon slot.

---

# 11. DMR Pickup

Create/configure a DMR pickup using the existing weapon pickup system.

The pickup should:

* Be visible in the world.
* Use the DMR model.
* Allow eligible players to acquire the weapon.
* Equip the DMR according to current secondary weapon rules.
* Synchronize correctly in multiplayer.
* Prevent multiple players from acquiring the same pickup simultaneously if that protection already exists.

Use the existing pickup architecture rather than implementing DMR-specific pickup logic.

---

# 12. Ammunition

For this ticket, initialize DMR ammunition settings to match the current AK/RifleDefinition.

This includes, where applicable:

* Magazine size
* Starting reserve ammunition
* Maximum reserve ammunition
* Reload behavior

These values are placeholders.

The DMR will likely receive different balancing later.

The important requirement for REQ-039 is that it correctly consumes ammunition and reloads using the existing weapon system.

---

# 13. Reloading

The DMR should support:

* Magazine ammunition
* Reserve ammunition
* Reload input
* Reload completion
* Empty-magazine behavior

Initially reuse whichever rifle reload animation/system is currently used by the AK if no DMR-specific animation exists.

Do not block implementation because a dedicated DMR reload animation has not yet been supplied.

---

# 14. First-Person Weapon Display

When the local player equips the DMR, the DMR should appear in their first-person weapon view.

It should use approximately the same starting transform/setup as the AK if that is the closest existing weapon.

Configure as necessary so that:

* It is positioned naturally.
* It does not clip excessively into the camera.
* The muzzle points forward.
* Aim/Zoom behaves correctly.
* Firing originates from the appropriate muzzle position.

Fine visual tuning can occur later.

---

# 15. Third-Person DMR Display

Other players should see the DMR weapon on the player character when equipped.

REQ-037 introduced/improved third-person weapon positioning.

The DMR should plug into that same system.

It should:

* Attach to the appropriate hand/weapon attachment setup.
* Be primarily held by the right hand.
* Be visually supported by the left hand where the existing IK/weapon positioning system allows.
* Follow the appropriate third-person aiming state.
* Move closer toward the player's aiming/head position while ADS according to the current third-person aiming system.

Do not create a separate third-person system exclusively for the DMR.

---

# 16. Aim / Zoom

REQ-038 moved Aim/Zoom to:

**Gamepad Left Trigger**

The DMR must support the same Aim/Zoom system.

While aiming:

* Weapon should transition to ADS.
* Camera/FOV behavior should follow the existing system.
* Reduced Aim sensitivity from REQ-038 should apply.
* Reticle behavior should follow the weapon system.
* Third-person players should see the aiming state.

No DMR-specific Aim input should be introduced.

---

# 17. Reticle

Use the existing rifle/AK reticle configuration as the initial DMR configuration.

Copy any applicable:

* Hip-fire reticle
* ADS reticle behavior
* Spread visualization
* Reticle expansion
* Movement bloom

These values can be differentiated later.

---

# 18. Damage

For REQ-039, copy the DMR damage settings directly from the current AK/RifleDefinition.

Do not attempt to balance the DMR yet.

The purpose is to establish a functional weapon first.

Future tuning will likely make the DMR behave differently from the AK based on:

* Higher per-shot damage
* Slower firing rate
* Greater accuracy
* Different recoil
* Different magazine capacity
* Different range characteristics

Those changes are intentionally deferred.

---

# 19. Audio

If dedicated DMR sounds are available, configure them.

Otherwise, temporarily reuse the most appropriate existing rifle sound.

Do not block the weapon from functioning because unique DMR audio is unavailable.

The audio assignment should remain easy to replace later.

---

# 20. Muzzle Flash / Firing Effects

Reuse the existing rifle firing effects initially.

The DMR should trigger:

* Muzzle flash
* Firing audio
* Hitscan/projectile behavior
* Impact effects

through the same existing systems.

---

# 21. Multiplayer

The DMR must function correctly with Netcode for GameObjects and the existing multiplayer weapon architecture.

Verify:

* Host can pick up DMR.
* Client can pick up DMR.
* Equipped DMR is visible to remote players.
* DMR firing synchronizes correctly.
* DMR ammunition behaves locally/authoritatively according to existing architecture.
* Damage is applied correctly.
* Reload state works correctly.
* Weapon switching works correctly.
* Dropping/replacing the DMR works correctly.
* Semi-auto behavior cannot accidentally become automatic because of network input handling.

---

# 22. Weapon Drop on Death

Follow the existing secondary weapon death behavior.

If the player dies while carrying:

```text
Pistol + DMR
```

the DMR should behave the same way as the AK/Shotgun secondary weapon currently behaves on death.

The permanent pistol should continue following its established rules.

---

# 23. Current Intended Weapon Taxonomy

After REQ-039, the weapon lineup should conceptually be:

## Pistol

Permanent starting sidearm.

Example identity:

`Pistol`

---

## AK

Automatic rifle.

Preferred explicit identity:

`AK`

Not merely:

`Rifle`

---

## DMR

Semi-automatic rifle.

Identity:

`DMR`

---

## Shotgun

Shotgun.

Identity:

`Shotgun`

---

# Naming Goal

Avoid structures such as:

```text
Pistol
Rifle
DMR
Shotgun
```

when `Rifle` actually means specifically the AK.

Prefer:

```text
Pistol
AK
DMR
Shotgun
```

Generic rifle terminology may still exist internally where it genuinely represents behavior shared by both the AK and DMR.

---

# Acceptance Criteria

REQ-039 is complete when:

1. The DMR model exists as a functional weapon prefab.
2. The DMR diffuse texture renders correctly.
3. The supplied roughness/smoothness information is incorporated appropriately into its material.
4. A DMR weapon definition/configuration exists.
5. Its initial gameplay values match the current AK/RifleDefinition where applicable.
6. The DMR fires **one shot per fire-button press**.
7. Holding the fire button does not cause automatic DMR fire.
8. The DMR respects its fire-rate cooldown.
9. The AK remains fully automatic.
10. The DMR consumes ammunition.
11. The DMR reloads correctly.
12. The DMR can be acquired as a secondary weapon.
13. Picking it up interacts correctly with the existing one-secondary-weapon limit.
14. The DMR appears correctly in first-person.
15. Other players can see the equipped DMR.
16. Aim/Zoom works with the DMR.
17. REQ-038's reduced ADS sensitivity works while aiming the DMR.
18. The DMR can deal damage using the existing weapon/damage system.
19. The DMR works for both host and client.
20. Existing AK functionality remains intact.
21. Existing Shotgun functionality remains intact.
22. Existing Pistol functionality remains intact.
23. AK-specific assets previously named simply `Rifle`/`RifleDefinition` have been renamed where safe and appropriate.
24. Generic rifle code remains generic rather than being unnecessarily renamed.
25. No serialized prefab/definition references are broken by the naming cleanup.
26. No new Console errors or warnings are introduced.

---

# Testing Checklist

### Asset / Visual

* [ ] DMR prefab loads without missing scripts
* [ ] Diffuse texture displays correctly
* [ ] Roughness/smoothness appears reasonable
* [ ] No bright pink/missing material
* [ ] Model scale is appropriate
* [ ] Weapon orientation is correct

### Pickup

* [ ] Player with only pistol can acquire DMR
* [ ] Player with AK can replace/swap to DMR
* [ ] Player with Shotgun can replace/swap to DMR
* [ ] Player cannot carry AK + Shotgun + DMR simultaneously unless the inventory system intentionally allows it

### Firing

* [ ] Click once → exactly one shot
* [ ] Hold mouse fire → exactly one shot
* [ ] Release and click again → another shot
* [ ] Hold gamepad fire → exactly one shot
* [ ] Release and press again → another shot
* [ ] Fire-rate cooldown still applies

### Ammo

* [ ] Magazine decreases by one per shot
* [ ] Reload functions
* [ ] Reserve ammo decreases correctly
* [ ] Empty magazine prevents firing appropriately

### Aim

* [ ] LT activates ADS on controller
* [ ] Existing mouse ADS works
* [ ] ADS sensitivity reduction works
* [ ] Weapon positioning behaves correctly during ADS

### Multiplayer

* [ ] Host pickup
* [ ] Client pickup
* [ ] Host firing visible to client
* [ ] Client firing visible to host
* [ ] Damage synchronizes
* [ ] Weapon switching synchronizes
* [ ] Third-person DMR is visible
* [ ] Semi-auto behavior is consistent for host and client

### Regression

* [ ] AK still works
* [ ] AK is still fully automatic
* [ ] Shotgun still works
* [ ] Pistol still works
* [ ] Existing weapon pickups still work
* [ ] Existing weapon definitions remain assigned
* [ ] No missing references after asset renaming

---

# Out of Scope

Do not attempt major DMR balancing in REQ-039.

Specifically defer:

* Final DMR damage
* Final DMR magazine size
* Final DMR firing rate
* Unique recoil tuning
* Unique ADS zoom level
* Unique reticle
* Unique animations
* Unique reload animations
* Unique sound design
* Weapon rarity system
* Attachment system
* Scopes
* Variable zoom optics
* Weapon customization

For now, the DMR should become a **fully functional semi-automatic fourth weapon**, using the AK as its initial statistical baseline while establishing clean architecture for multiple rifle types.
