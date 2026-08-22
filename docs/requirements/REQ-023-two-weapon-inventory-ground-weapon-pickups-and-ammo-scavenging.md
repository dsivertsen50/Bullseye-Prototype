# REQ-023 — Two-Weapon Inventory, Ground Weapon Pickups, and Ammo Scavenging

## 1. Summary

Implement a Halo-style two-weapon inventory and ground-weapon sandbox.

Each player should:

* Spawn with exactly two weapons.
* Begin with the current Ruger pistol and a basic rifle.
* Have one active weapon and one inactive weapon.
* Be able to quickly swap between the two carried weapons.
* Pick up weapons found in the world.
* Replace only the weapon they are currently holding when picking up a different weapon.
* Collect ammunition by interacting with duplicate weapons they already possess.
* Drop the replaced weapon into the world.
* Maintain all inventory and pickup behavior correctly in multiplayer.

The system should be designed generically so that future weapons can be added without rewriting the inventory system.

The current Ruger is temporary and will eventually be replaced by another pistol model.

---

# 2. Design Goal

The inventory should feel similar to the weapon sandbox in Halo.

The player should not have:

* A weapon wheel.
* A backpack.
* More than two carried weapons.
* Complex inventory menus.

Instead, weapon choice should revolve around simple battlefield decisions:

> Which two weapons do I want to carry?

and:

> Which weapon am I willing to give up for the one on the ground?

---

# 3. Two-Weapon Inventory

Every player has exactly:

```text
Weapon Slot 1
Weapon Slot 2
```

One slot is:

```text
Active
```

and the other is:

```text
Inactive
```

The Active weapon is the weapon currently shown in first person and capable of firing.

The Inactive weapon remains in the player's inventory but is not currently usable until the player switches weapons.

---

# 4. Starting Loadout

For the current prototype, every player should spawn with:

```text
Slot 1:
Current Ruger pistol

Slot 2:
Basic rifle
```

The exact rifle model may be temporary.

The system must not hardcode the inventory logic specifically around:

* Ruger
* Rifle
* Pistol
* Rifle classes

They are simply two starting weapon definitions.

Future starting loadouts should be configurable.

---

# 5. Generic Weapon Definitions

Create or extend a generic weapon data architecture.

A weapon should have configurable information such as:

```text
Weapon ID
Display Name
Weapon Prefab
First-Person Model
World Pickup Model/Prefab

Magazine Size
Starting Magazine Ammo
Starting Reserve Ammo
Maximum Reserve Ammo

Fire Rate
Damage
Reload Time

Audio
Animations
Other weapon-specific data
```

A ScriptableObject-based weapon definition is acceptable and likely preferred if it fits the existing FPS architecture.

Example:

```text
WeaponDefinition

Weapon ID:
ruger_22

Display Name:
Ruger .22

Magazine Size:
10

Max Reserve:
60
```

Do not require weapon-specific inventory code for each new gun.

---

# 6. Weapon Switching

The player must be able to switch between their two carried weapons at any time when gameplay input is active.

## Mouse / Keyboard

Primary weapon-switch input:

```text
Mouse Wheel Up
Mouse Wheel Down
```

Because there are only two weapon slots, either scroll direction can toggle to the opposite weapon.

Also provide a keyboard alternative:

```text
Q
```

Recommended behavior:

```text
Mouse Wheel → toggle carried weapon
Q → toggle carried weapon
```

This ensures keyboard-only players are not required to use a mouse wheel.

If the existing FPS Engine already has a clean weapon-switch binding that conflicts with Q, preserve the existing safe binding or select a nearby equivalent.

---

# 7. Gamepad Weapon Switching

Use the controller's **north face button**.

Examples:

```text
Xbox:
Y

PlayStation:
Triangle

Nintendo-style:
X
```

Use the Input System's semantic north-button binding rather than hardcoding Xbox-specific hardware where possible.

Conceptually:

```text
<Gamepad>/buttonNorth
```

Pressing the north button should toggle between the player's two carried weapons.

---

# 8. Weapon Switching Behavior

Example inventory:

```text
Active:
Ruger

Inactive:
Rifle
```

Player scrolls the mouse wheel or presses Y.

Result:

```text
Active:
Rifle

Inactive:
Ruger
```

Another switch returns to:

```text
Active:
Ruger

Inactive:
Rifle
```

There is no need for a weapon-selection menu.

---

# 9. Weapon-Switch Animation

Switching weapons should not instantly pop one model out and another into view if the imported FPS system already provides appropriate equip/unequip animations.

Preferred flow:

```text
Player requests switch
↓
Current weapon holster/lower animation
↓
Current weapon becomes inactive
↓
Other weapon becomes active
↓
New weapon draw/raise animation
↓
Player may fire
```

If animations are not available or are difficult to integrate, a functional transition is acceptable for the first implementation.

However, design the system so weapon-switch animations can be added cleanly later.

---

# 10. Prevent Firing During Switch

The player should not be able to fire during the weapon-switch transition.

For example:

```text
Switch starts
↓
Fire temporarily blocked
↓
New weapon becomes ready
↓
Fire enabled
```

Avoid exploits where the player can fire both weapons simultaneously during a switch.

---

# 11. Ground Weapons

Weapons should be able to exist physically in the game world.

A ground weapon should have:

* World model
* Collider / pickup detection
* Weapon definition
* Remaining ammunition information
* Network identity if required
* Pickup interaction logic

Ground weapons should be recognizable as the weapon they represent.

---

# 12. Pickup Interaction

Picking up a different weapon should require deliberate player input.

Do **not** automatically replace the player's current weapon merely because the player walks over it.

When a player is close enough to a weapon and looking at it, display a local interaction prompt.

Example:

```text
Hold E — Swap for Shotgun
```

Controller equivalent:

```text
Hold X — Swap for Shotgun
```

Xbox terminology is shown only as an example.

Use the appropriate west face button semantically where possible:

```text
<Gamepad>/buttonWest
```

---

# 13. Recommended Pickup Inputs

## Keyboard

Use:

```text
E
```

for weapon interaction / pickup.

## Gamepad

Use:

```text
West face button
```

Examples:

```text
Xbox:
X

PlayStation:
Square
```

This keeps:

```text
Y / Triangle
```

reserved for switching carried weapons.

---

# 14. Hold vs Press

Prefer a **short hold** for replacing a carried weapon.

Recommended:

```text
Hold interaction button for approximately 0.3–0.5 seconds
```

This reduces accidental weapon swaps.

A visible progress indicator is optional for the prototype.

If implementation complexity is significantly increased, a simple button press is acceptable initially.

---

# 15. Replace the Currently Active Weapon

The most important inventory rule:

> Picking up a different weapon replaces the weapon the player is currently holding.

Example:

Player inventory:

```text
Active:
Ruger

Inactive:
Rifle
```

Ground weapon:

```text
Shotgun
```

Player picks up Shotgun.

Result:

```text
Active:
Shotgun

Inactive:
Rifle
```

The Ruger is dropped onto the ground.

---

# 16. Active-Slot Choice Matters

Using the same inventory:

```text
Ruger
Rifle
```

Suppose the player wants to keep the Ruger but replace the Rifle.

The player must first switch to:

```text
Active:
Rifle

Inactive:
Ruger
```

Then pick up the Shotgun.

Result:

```text
Active:
Shotgun

Inactive:
Ruger
```

The Rifle is dropped.

This behavior is intentional.

It should create a meaningful, simple player decision.

---

# 17. Dropping the Replaced Weapon

When a weapon is replaced, the old active weapon should appear in the world as a ground weapon.

Preferably:

```text
Dropped near player's current position
```

The dropped weapon should retain its current ammunition state.

For example:

Player's Ruger before swap:

```text
Magazine:
5

Reserve:
17
```

Dropped Ruger should retain approximately:

```text
5 magazine
17 reserve
```

or an equivalent total-ammunition representation.

Do not magically refill dropped weapons.

---

# 18. Avoid Pickup Loops

Take care when spawning the replaced weapon.

A player should not immediately re-pick-up or re-trigger interaction with the weapon they just dropped unless they intentionally interact with it.

Possible solutions include:

* Spawn slightly away from the player's center.
* Temporarily disable pickup interaction for the dropping player.
* Require explicit interaction.
* Add a short pickup cooldown.

Use whichever solution fits the architecture cleanly.

---

# 19. Duplicate Weapon Ammo Scavenging

If a player encounters a weapon type they already possess, that weapon should function as an ammunition source.

Example:

Player owns:

```text
Ruger
Rifle
```

Ground weapon:

```text
Ruger
```

The ground Ruger should be recognized as a duplicate.

Instead of forcing a weapon replacement, the player should be able to collect ammunition from it.

---

# 20. Duplicate Detection

Weapon identity should be determined using the generic weapon definition / unique Weapon ID.

For example:

```text
Player weapon:
ruger_22

Ground weapon:
ruger_22
```

Result:

```text
Same weapon type
```

Do not determine duplicate weapons by:

* Object names
* Prefab instance names
* Display text
* Model name

Use a stable unique identifier.

---

# 21. Ammo Transfer

Suppose the player's Ruger has:

```text
Magazine:
6 / 10

Reserve:
20 / 60
```

A ground Ruger contains:

```text
10 available rounds
```

Picking up its ammo should result in:

```text
Magazine:
6 / 10

Reserve:
30 / 60
```

The magazine does not need to refill automatically.

Ammo scavenging should primarily add to reserve ammunition.

---

# 22. Respect Maximum Ammo

Ammo pickups must not exceed the weapon's configured maximum reserve.

Example:

```text
Player Reserve:
57 / 60

Ground Weapon Ammo:
10
```

Player should receive:

```text
3 rounds
```

Final reserve:

```text
60 / 60
```

The remaining ground ammo should be handled appropriately.

---

# 23. Ground Weapon Remaining Ammo

Preferred behavior:

If only part of the ground weapon's ammunition is collected:

```text
Ground weapon remains
```

with the remaining ammo.

Example:

```text
Ground weapon:
10 rounds

Player can accept:
3 rounds
```

After interaction:

```text
Player receives:
3

Ground weapon remains with:
7
```

This provides the strongest Halo-like sandbox behavior.

If preserving partial ammunition significantly complicates the initial implementation, consuming the ground weapon after transferring available ammo is acceptable temporarily, but the architecture should support remaining ammo later.

---

# 24. Full Ammo Behavior

If the player is already at maximum ammunition for that weapon:

```text
Ammo Full
```

The ground weapon should remain in the world.

It should not be consumed.

Optional local prompt:

```text
Ammo Full
```

or:

```text
Ruger Ammo Full
```

---

# 25. Duplicate Weapon in Inactive Slot

Duplicate detection applies to both carried weapon slots.

Example:

```text
Active:
Shotgun

Inactive:
Ruger
```

Ground item:

```text
Ruger
```

The player already possesses a Ruger.

Therefore, interacting with the ground Ruger should provide Ruger ammunition rather than replacing the currently active Shotgun.

The inventory system should check **both slots**.

---

# 26. Ammo Pickup Behavior

Ammo scavenging may be either:

```text
Automatic when walking over the duplicate weapon
```

or:

```text
Interaction-based
```

For REQ-023, prefer:

> **Automatic ammo scavenging when the player moves close enough to a duplicate weapon.**

This provides Halo-like behavior.

However:

> Weapon replacement must always require deliberate interaction.

This distinction is important.

---

# 27. Example: Automatic Ammo Collection

Player owns a Ruger.

Player walks over a Ruger on the ground.

If the player's Ruger needs ammo:

```text
Ammo automatically transfers
```

No button press required.

If the player's ammo is full:

```text
Nothing happens
```

The ground Ruger remains.

---

# 28. Ammo Model

Weapons should support at minimum:

```text
Magazine Ammo
Reserve Ammo
```

Example:

```text
Ruger

Magazine:
8 / 10

Reserve:
35 / 60
```

Firing subtracts from magazine ammo.

Reloading transfers reserve ammo into the magazine.

The weapon should not create new ammunition when reloading.

---

# 29. Reloading

If the current weapon implementation already supports reloads, integrate the new ammo system into it.

If not, REQ-023 may implement the minimum required reload logic.

Recommended keyboard input:

```text
R
```

Recommended controller input:

```text
Existing FPS-engine reload binding
```

Do not arbitrarily change an existing working controller reload binding.

---

# 30. Empty Weapon Behavior

If:

```text
Magazine = 0
Reserve > 0
```

the player should be able to reload.

If:

```text
Magazine = 0
Reserve = 0
```

the weapon should not fire.

The player may still:

* Switch weapons.
* Find ammunition.
* Replace the empty weapon with another weapon.

---

# 31. Weapon Pickup Prompt

When the player is targeting a different weapon that can replace the active weapon, display a local UI prompt.

Example:

```text
SHOTGUN

Hold E to Swap
```

Controller:

```text
SHOTGUN

Hold X to Swap
```

The UI can be visually simple for now.

---

# 32. Prompt Locality

Pickup prompts must be local to the player who can interact with the weapon.

Player 1 looking at a Shotgun should not cause Player 2's UI to display:

```text
Swap for Shotgun
```

This UI must not be synchronized as shared network state.

---

# 33. Determining Which Ground Weapon Is Targeted

Prefer a short camera-centered interaction raycast or equivalent FPS interaction system.

Conceptually:

```text
Player camera
↓
Short interaction ray
↓
Weapon pickup
```

This helps prevent ambiguous selection when several weapons are close together.

A nearby-distance check may also be required.

---

# 34. Pickup Distance

Use a configurable interaction distance.

Suggested prototype range:

```text
Approximately 2–3 meters
```

The exact number may be tuned during testing.

Expose it in an Inspector/configuration field rather than scattering the value through code.

---

# 35. Weapon Pickup Animation

When a different weapon is picked up:

Preferred flow:

```text
Interaction accepted
↓
Current weapon removed/dropped
↓
New weapon assigned to active slot
↓
Pickup/draw animation
↓
New weapon ready
```

If no pickup animation exists yet, use the existing weapon-equip animation.

---

# 36. Multiplayer Authority

Weapon pickup must be network-safe.

The authoritative game state must determine:

* Whether a ground weapon still exists.
* Which player successfully picked it up.
* Its remaining ammunition.
* Which weapon was dropped.
* Which player owns which weapons.

Two players must not be able to duplicate the same weapon by picking it up simultaneously.

---

# 37. Simultaneous Pickup Example

Suppose Player 1 and Player 2 both try to pick up the same Shotgun.

Only one request should succeed.

Expected outcome:

```text
Player 1 receives Shotgun
Player 2 does not
```

or:

```text
Player 2 receives Shotgun
Player 1 does not
```

depending on which valid authoritative request is processed first.

The weapon must not exist in both inventories.

---

# 38. Network Ownership

Use the existing Netcode for GameObjects architecture appropriately.

Do not trust the client alone to:

* Destroy a world weapon.
* Spawn a duplicate weapon.
* Give itself ammunition.
* Change authoritative inventory contents.

The host/server should validate meaningful inventory changes.

---

# 39. Local First-Person Weapons

Each player should see their own active first-person weapon.

Switching Player 1's weapon must not change Player 2's first-person weapon.

Existing network/first-person visibility conventions should be preserved.

---

# 40. Third-Person Weapon Representation

If third-person held weapon models already exist, they may be updated when switching weapons.

If they do not yet exist, this is not required for REQ-023.

The inventory system should nevertheless make it possible to add third-person weapon representations later.

---

# 41. Player Death

Do not redesign the current death and respawn system for REQ-023.

For this requirement:

On respawn, the player should receive the configured default loadout again:

```text
Ruger
Rifle
```

Any special weapon previously carried does not need to persist through death.

---

# 42. Dropping Weapons on Death

Dropping a player's carried weapons upon death is **not required by REQ-023**.

This should likely become a later sandbox requirement once basic pickups are stable.

For now:

```text
Death
↓
Current inventory discarded/reset
↓
Respawn
↓
Default two-weapon loadout
```

---

# 43. World Weapon Respawning

Weapon spawn pads and timed weapon respawns are also outside the core scope of REQ-023.

REQ-023 only needs the ability to place functional ground weapon pickups manually in the scene.

A future requirement can add:

* Weapon spawn locations.
* Respawn timers.
* Power-weapon timers.
* Map-specific weapon layouts.

---

# 44. Inspector Configuration

Important pickup and inventory values should be configurable in Unity.

Examples:

```text
Starting Weapon 1
Starting Weapon 2

Pickup Distance
Pickup Hold Duration

Weapon Switch Duration

Weapon Definitions
```

Do not require editing code simply to replace the starting Ruger with a different pistol later.

---

# 45. Input Actions

Integrate with the project's Unity Input System.

Recommended semantic actions:

```text
SwitchWeapon
Interact
Reload
```

Suggested bindings:

## SwitchWeapon

```text
Mouse Wheel Up
Mouse Wheel Down
Q
<Gamepad>/buttonNorth
```

## Interact

```text
E
<Gamepad>/buttonWest
```

## Reload

```text
R
Existing appropriate gamepad binding
```

Do not create duplicated competing input systems if the current FPS integration already has equivalent actions.

Extend the existing Input Actions setup cleanly.

---

# 46. Controller Priority

The system must work completely with an Xbox-style controller.

Testing should specifically verify:

```text
Y = switch weapon
X = interact / swap ground weapon
```

based on the Xbox layout.

The code should nevertheless use semantic Input System controls so PlayStation-compatible controllers can work naturally.

---

# 47. Mouse Wheel Debouncing

One scroll action should result in one weapon swap.

Do not allow a single mouse-wheel movement to trigger:

```text
Ruger → Rifle → Ruger
```

so quickly that the player appears not to switch at all.

Use appropriate input debouncing/cooldown if necessary.

---

# 48. Weapon Switch Cooldown

Prevent repeated switch spam from corrupting state.

During an active switching animation/state:

```text
additional switch requests may be ignored
```

or safely queued.

For the prototype, ignoring new switch input until the current switch completes is acceptable.

---

# 49. Suggested Architecture

Adapt to the existing project rather than blindly creating duplicate systems.

A clean conceptual structure may contain:

```text
PlayerWeaponInventory
WeaponDefinition
WeaponInstance
GroundWeaponPickup
PlayerWeaponController
```

Possible responsibilities:

## PlayerWeaponInventory

Tracks:

```text
Slot 1
Slot 2
Active slot
```

Handles:

* Replacing weapons.
* Duplicate detection.
* Ammo transfer.
* Starting loadout.

## WeaponDefinition

Static weapon data:

```text
Weapon ID
Name
Models
Ammo limits
Stats
```

## WeaponInstance

Runtime weapon state:

```text
Magazine ammo
Reserve ammo
```

## GroundWeaponPickup

Tracks:

```text
Weapon type
Remaining ammo
Pickup state
Network state
```

## PlayerWeaponController

Handles:

* Weapon switching.
* Equip/holster.
* Firing integration.
* Local first-person model.

Reuse existing Cowsins/FPS Engine functionality wherever practical.

---

# 50. Important Integration Rule

Do not rewrite working gun systems merely to implement the inventory.

The existing imported FPS weapon functionality should be leveraged where possible.

Specifically preserve working:

* Ruger shooting
* Weapon animations
* Weapon audio
* Aim behavior
* Camera behavior

The purpose of REQ-023 is to build the inventory and pickup layer around the weapon system.

---

# 51. Acceptance Criteria

REQ-023 is complete when all of the following are true:

* [ ] Every player spawns with exactly two weapons.
* [ ] Default Slot 1 contains the current Ruger pistol.
* [ ] Default Slot 2 contains a working rifle or temporary rifle.
* [ ] Only one weapon is active at a time.
* [ ] Mouse wheel switches between the two carried weapons.
* [ ] Q switches between the two carried weapons.
* [ ] Xbox Y / gamepad north button switches between the two carried weapons.
* [ ] Switching weapons correctly changes the first-person weapon.
* [ ] The player cannot fire both weapons during a switch.
* [ ] Ground weapons can be placed in the scene.
* [ ] Ground weapons have a generic weapon identity.
* [ ] A player can deliberately pick up a different weapon.
* [ ] E functions as the keyboard weapon-interaction input.
* [ ] Xbox X / gamepad west button functions as the controller interaction input.
* [ ] Picking up a different weapon replaces the currently active weapon.
* [ ] The inactive weapon is not replaced.
* [ ] The replaced weapon is dropped into the world.
* [ ] The dropped weapon retains its ammunition state.
* [ ] Players can carry any combination of two weapons supported by the system.
* [ ] The system is not restricted to one pistol slot and one rifle slot.
* [ ] Duplicate weapons are detected using a stable Weapon ID.
* [ ] Duplicate weapons provide ammunition instead of replacing a carried weapon.
* [ ] Duplicate detection checks both inventory slots.
* [ ] Ammo scavenging adds reserve ammunition.
* [ ] Ammo cannot exceed configured maximum reserve ammo.
* [ ] A duplicate weapon is not consumed if the player has full ammo.
* [ ] Ground weapon ammunition is tracked appropriately.
* [ ] Weapon pickup UI appears only for the relevant local player.
* [ ] Two clients cannot successfully pick up the same single ground weapon.
* [ ] Inventory state functions correctly in multiplayer.
* [ ] Players receive the default loadout again when respawning.
* [ ] Existing Ruger firing, aiming, sounds, and animations continue working.

---

# 52. Testing Procedure

## Test A — Starting Loadout

1. Start a player.
2. Observe the first-person weapon.

Expected:

```text
Ruger equipped
Rifle stored
```

3. Scroll the mouse wheel.

Expected:

```text
Rifle equipped
```

4. Press Q.

Expected:

```text
Ruger equipped
```

---

## Test B — Controller Switching

Using an Xbox controller:

1. Spawn.
2. Confirm Ruger is active.
3. Press Y.

Expected:

```text
Rifle becomes active
```

4. Press Y again.

Expected:

```text
Ruger becomes active
```

---

## Test C — Replace Active Weapon

Inventory:

```text
Active:
Ruger

Inactive:
Rifle
```

Place a Shotgun pickup in the scene.

Interact with the Shotgun.

Expected:

```text
Active:
Shotgun

Inactive:
Rifle
```

Ruger appears on the ground.

---

## Test D — Replace Other Slot

Start with:

```text
Ruger
Rifle
```

Switch to Rifle.

Pick up Shotgun.

Expected:

```text
Active:
Shotgun

Inactive:
Ruger
```

Rifle is dropped.

This verifies that pickup replaces the **active weapon**, not a predetermined weapon category.

---

## Test E — Duplicate Weapon Ammo

Inventory:

```text
Ruger
Rifle
```

Reduce Ruger reserve ammo.

Place another Ruger on the ground.

Walk over it.

Expected:

```text
Ruger reserve ammo increases.
```

The player's inventory remains:

```text
Ruger
Rifle
```

No weapon slot is replaced.

---

## Test F — Duplicate Inactive Weapon

Inventory:

```text
Active:
Rifle

Inactive:
Ruger
```

Walk over another Ruger.

Expected:

Ruger ammunition increases even though the Ruger is not active.

The Rifle is not replaced.

---

## Test G — Full Ammo

Set Ruger reserve to maximum.

Walk over a ground Ruger.

Expected:

```text
No ammo added.
Ground Ruger remains.
```

---

## Test H — Ammo Cap

Set:

```text
Ruger reserve:
58 / 60
```

Ground Ruger has:

```text
10 rounds
```

Collect ammunition.

Expected:

```text
Reserve:
60 / 60
```

No ammo exceeds the configured maximum.

---

## Test I — Dropped Ammo Preservation

Set active Ruger to:

```text
Magazine:
4

Reserve:
13
```

Swap it for a Shotgun.

Inspect/re-pick-up the dropped Ruger.

Expected:

Its ammunition remains consistent with what it had when dropped rather than resetting to default.

---

## Test J — Multiplayer Race

Run two players.

Place one Shotgun.

Have both players attempt to pick it up at nearly the same time.

Expected:

Exactly one player receives the Shotgun.

The other does not.

No duplicate Shotgun is created.

---

## Test K — Local Pickup UI

Player 1 looks at a Shotgun.

Player 2 looks somewhere else.

Expected:

Only Player 1 sees:

```text
Swap for Shotgun
```

Player 2 sees no pickup prompt.

---

## Test L — Respawn

1. Pick up a non-default weapon.
2. Die.
3. Complete the existing respawn countdown.
4. Respawn.

Expected inventory:

```text
Ruger
Rifle
```

The default loadout is restored.

---

# 53. Out of Scope

REQ-023 does not require:

* More than two carried weapons.
* Inventory menus.
* Weapon wheels.
* Grenade inventories.
* Dedicated ammo-box pickups.
* Weapon spawn pads.
* Timed weapon respawning.
* Power-weapon announcements.
* Weapon rarity.
* Weapon skins.
* Attachment systems.
* Dropping a weapon manually.
* Dropping weapons on death.
* Picking up enemy equipment.
* Dual wielding.
* Final weapon balancing.
* Final rifle model.
* Final pistol model.
* Third-person weapon rendering if not already supported.

These can be addressed in later requirements.

---

# 54. Future Compatibility

The implementation should make later additions straightforward, including:

```text
Shotguns
Sniper rifles
Automatic rifles
Heavy weapons
Experimental weapons
```

as well as:

```text
Weapon spawn pads
Weapon respawn timers
Dropped weapons on death
Dedicated ammunition pickups
Map-specific weapon layouts
```

REQ-023 should establish the reusable foundation for the game's eventual weapon sandbox.

---

# 55. Implementation Priority

Implement in this order where practical:

1. Generic weapon identity/data architecture.
2. Two-slot player inventory.
3. Starting Ruger + rifle loadout.
4. Mouse wheel / Q / north-button weapon switching.
5. Ground weapon object.
6. Replace-active-weapon interaction.
7. Drop replaced weapon.
8. Duplicate weapon detection.
9. Ammo scavenging.
10. Ammo limits and remaining-ground-ammo state.
11. Local pickup prompt.
12. Multiplayer authority and race-condition protection.
13. Regression testing of existing weapon functionality.

Favor a robust generic architecture over implementing special cases for the Ruger and rifle.
