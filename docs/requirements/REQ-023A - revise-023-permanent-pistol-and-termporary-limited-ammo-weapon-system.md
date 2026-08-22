# REQ-023 Revision A — Permanent Pistol and Temporary Limited-Ammo Weapon System

## 1. Purpose of This Revision

REQ-023 was previously implemented as a Halo-style two-weapon inventory system in which players:

* Spawned with two normal weapons.
* Could replace either weapon.
* Could collect ammunition from duplicate weapons.
* Maintained two equivalent weapon inventory slots.

We are changing that design.

The existing REQ-023 implementation should be **refactored rather than duplicated**.

The new design is intentionally simpler and should become the weapon inventory model for the prototype going forward.

---

# 2. New Core Design

Every player always possesses one permanent baseline weapon:

```text
Permanent Weapon:
Pistol
```

For the current prototype, this is the existing Ruger model.

The Ruger model is temporary and will eventually be replaced, so the architecture should refer to this concept as the:

```text
Default Pistol
```

rather than hardcoding gameplay logic around the Ruger name.

In addition to the permanent pistol, a player may carry:

```text
0 or 1 Temporary Weapon
```

Examples of temporary weapons might eventually include:

* Rifle
* Shotgun
* Sniper rifle
* Automatic weapon
* Heavy weapon
* Other specialty weapons

The temporary weapon has **finite ammunition**.

When its ammunition is exhausted, the player loses access to that weapon and returns to using the pistol.

---

# 3. Design Philosophy

The pistol is the player's permanent fallback weapon.

Temporary weapons are battlefield resources.

Conceptually:

```text
Pistol
=
Always available

Temporary weapon
=
Limited-duration combat advantage
```

Players should therefore make decisions about:

* Whether to pick up a temporary weapon.
* When to use its ammunition.
* Whether to conserve it.
* Whether to replace it with another temporary weapon.
* Whether another player's dropped weapon is worth retrieving.

This should create map control and resource competition without requiring a traditional inventory system.

---

# 4. Player Inventory

The player's weapon inventory is now:

```text
Permanent Pistol
+
Optional Temporary Weapon
```

Not:

```text
Weapon Slot 1
+
Weapon Slot 2
```

These weapons have intentionally different roles.

The permanent pistol cannot be discarded or replaced.

The temporary weapon can be:

* Acquired.
* Used.
* Swapped.
* Dropped.
* Lost on death.
* Removed when ammunition reaches zero.

---

# 5. Starting Loadout

Every player should spawn with:

```text
Permanent Pistol
```

and:

```text
No Temporary Weapon
```

Players should no longer spawn with the rifle previously added by REQ-023.

Remove the rifle from the default starting loadout.

---

# 6. Permanent Pistol Ammo

The permanent pistol has **unlimited total ammunition**.

However, it still uses a magazine and reload system.

Example:

```text
Pistol Magazine:
10 rounds

Total Reserve:
Unlimited
```

The player may fire:

```text
10 shots
```

and then must reload.

Reloading restores the magazine from the infinite ammunition supply.

Example:

```text
10 / ∞
↓
Fire 10 rounds
↓
0 / ∞
↓
Reload
↓
10 / ∞
```

The pistol should therefore never become permanently unusable due to ammunition depletion.

---

# 7. Pistol Reloading

The permanent pistol must retain normal reload behavior.

The infinite ammunition system should **not** mean:

* Infinite magazine size.
* No reload animation.
* Automatic continuous shooting.
* No downtime between magazines.

The player must still complete the existing reload process.

Preserve existing:

* Reload animation.
* Reload timing.
* Weapon audio.
* Firing behavior.

where currently functional.

---

# 8. Pistol Cannot Be Dropped

The permanent pistol is intrinsic to the player's loadout.

It should never appear as a normal dropped weapon due to:

* Picking up another weapon.
* Switching weapons.
* Death.
* Running out of ammo.

The player always respawns with the pistol.

---

# 9. Temporary Weapons

Weapons found around the map are **Temporary Weapons**.

Each temporary weapon has a finite ammunition supply.

Example:

```text
Rifle

Magazine Size:
30 rounds

Magazines Available:
3

Total Starting Ammunition:
90 rounds
```

The exact weapon configuration should remain data-driven.

---

# 10. Temporary Weapon Ammo Configuration

Each temporary weapon should define values such as:

```text
Magazine Size
Starting Magazine Ammo
Number of Magazines / Reserve Ammo
Maximum Total Ammo
```

For example:

```text
Rifle

Rounds Per Magazine:
30

Starting Magazines:
3

Total Ammo:
90
```

or an equivalent representation:

```text
Magazine:
30

Reserve:
60
```

The exact internal representation is an implementation detail.

The important gameplay rule is:

> Temporary weapons contain a known finite quantity of ammunition.

---

# 11. Generic Weapon Definitions

Preserve the generic weapon-data architecture created during the original REQ-023 where practical.

A weapon definition may contain:

```text
Weapon ID
Display Name
Weapon Type

First-Person Prefab
World Pickup Prefab

Magazine Size
Default Pickup Ammo

Fire Rate
Damage
Reload Time

Animations
Audio
Other weapon-specific data
```

Add a classification such as:

```text
Permanent Default Weapon

or

Temporary Pickup Weapon
```

if useful.

Do not create special hardcoded scripts for every individual rifle, shotgun, etc.

---

# 12. Deliberate Weapon Pickup

Temporary weapons must **never automatically enter the player's inventory simply because the player walks over them**.

Picking up a weapon must require deliberate interaction.

Recommended interaction:

```text
Keyboard:
E

Gamepad:
West face button
```

Examples:

```text
Xbox:
X

PlayStation:
Square
```

Use Unity Input System semantic bindings where possible.

---

# 13. Pickup Prompt

When the player is within range and looking at a temporary weapon, display a local prompt.

Example:

```text
RIFLE

Hold E to Pick Up
```

Controller equivalent:

```text
RIFLE

Hold X to Pick Up
```

If controller glyph support already exists, use it.

Otherwise simple text is acceptable for the prototype.

---

# 14. Pickup Hold

Prefer a short deliberate hold:

```text
Approximately 0.3–0.5 seconds
```

before the weapon is acquired.

This helps prevent accidental pickups during combat.

If the currently implemented REQ-023 interaction already has suitable hold behavior, reuse it.

---

# 15. Carry Limit

The player may carry:

```text
1 Permanent Pistol
+
Maximum 1 Temporary Weapon
```

A player may never carry two temporary weapons simultaneously.

Examples:

Valid:

```text
Pistol + Rifle
```

Valid:

```text
Pistol + Shotgun
```

Valid:

```text
Pistol only
```

Invalid:

```text
Pistol + Rifle + Shotgun
```

---

# 16. Picking Up a Weapon With No Temporary Weapon

If the player currently possesses:

```text
Pistol only
```

and deliberately picks up a Rifle:

Result:

```text
Permanent:
Pistol

Temporary:
Rifle
```

The newly acquired temporary weapon should become the active weapon.

---

# 17. Picking Up Another Temporary Weapon

If the player already possesses a temporary weapon, picking up a different temporary weapon should replace it.

Example:

Player currently has:

```text
Permanent:
Pistol

Temporary:
Rifle
```

Ground weapon:

```text
Shotgun
```

Player deliberately picks up Shotgun.

Result:

```text
Permanent:
Pistol

Temporary:
Shotgun
```

The Rifle is dropped into the world with its remaining ammunition.

---

# 18. Replacement Must Be Deliberate

Do not automatically replace a player's temporary weapon merely because another weapon is nearby.

The player must intentionally perform the pickup interaction.

This is particularly important if multiple weapons are lying near each other.

---

# 19. Dropped Temporary Weapon Ammo

When a temporary weapon is dropped, it must retain its current ammunition state.

Example:

Player has Rifle:

```text
Magazine:
12 / 30

Reserve:
30
```

Player swaps Rifle for Shotgun.

The dropped Rifle should remain:

```text
Magazine:
12

Reserve:
30
```

or equivalent total remaining ammunition.

Do not refill dropped weapons.

---

# 20. Weapon Switching

When the player has a temporary weapon, they should be able to switch freely between:

```text
Permanent Pistol
↔
Temporary Weapon
```

Recommended bindings should remain from the original REQ-023:

## Mouse / Keyboard

```text
Mouse Wheel
```

and:

```text
Q
```

## Gamepad

Use:

```text
North face button
```

Examples:

```text
Xbox:
Y

PlayStation:
Triangle
```

---

# 21. Weapon Switching With No Temporary Weapon

If the player only possesses the pistol:

```text
Pistol
+
No Temporary Weapon
```

pressing:

```text
Mouse Wheel
Q
Y / buttonNorth
```

should do nothing.

It should not cause an error or weapon disappearance.

---

# 22. Temporary Weapon Ammunition

Temporary weapons do not have infinite ammunition.

Example:

```text
Rifle

Magazine:
30

Reserve:
60
```

After firing:

```text
Magazine:
0

Reserve:
60
```

the player reloads:

```text
Magazine:
30

Reserve:
30
```

Eventually:

```text
Magazine:
0

Reserve:
0
```

At this point, the temporary weapon is exhausted.

---

# 23. Temporary Weapon Exhaustion

When:

```text
Magazine Ammo = 0

and

Reserve Ammo = 0
```

the weapon should be considered exhausted.

The player should no longer retain an unusable temporary weapon indefinitely.

Preferred behavior:

```text
Final round fired
↓
Temporary weapon reaches zero total ammo
↓
Short appropriate transition
↓
Temporary weapon removed
↓
Pistol automatically equipped
```

The exhausted temporary weapon does not need to spawn as a useful world pickup because it has no ammunition remaining.

It may simply disappear from inventory.

---

# 24. Empty Magazine With Reserve Remaining

Do not remove the temporary weapon merely because its magazine reaches zero.

Example:

```text
Magazine:
0

Reserve:
60
```

The weapon is still usable.

The player should reload normally.

Only remove it when:

```text
Magazine = 0
Reserve = 0
```

---

# 25. Weapon Exhaustion While Pistol Is Active

Suppose the player has:

```text
Active:
Pistol

Temporary:
Rifle with 0 ammunition
```

This invalid state should not persist.

If the temporary weapon reaches zero ammunition, remove it from inventory regardless of which weapon is currently active.

---

# 26. Death Behavior

The permanent pistol should not drop when the player dies.

If the player has no temporary weapon:

```text
Death
↓
No weapon drop
↓
Respawn with Pistol
```

If the player has a temporary weapon:

```text
Death
↓
Temporary weapon drops at death location
↓
Weapon retains remaining ammo
↓
Player respawns with Pistol only
```

---

# 27. Example Death Drop

Player dies while carrying:

```text
Permanent Pistol

Rifle:
Magazine 17 / 30
Reserve 30
```

At the player's death location, spawn:

```text
Rifle

Magazine:
17

Reserve:
30
```

The respawned player receives:

```text
Pistol only
```

---

# 28. Death Drop Regardless of Active Weapon

The temporary weapon should drop even if the pistol was active when the player died.

Example:

```text
Active:
Pistol

Temporary inventory:
Shotgun
```

Player dies.

The Shotgun should still drop.

This prevents players from protecting temporary weapons simply by switching back to the pistol before death.

---

# 29. Dropped Weapon Persistence

Dropped temporary weapons should remain available in the world after death.

They should not immediately disappear simply because the original owner respawns.

Another player should be able to find and deliberately pick up the dropped weapon.

This is an important part of the intended weapon sandbox.

---

# 30. World Weapon Ammo State

All temporary weapons existing in the world should maintain their runtime ammunition state.

A world Rifle could therefore contain:

```text
90 rounds
```

when originally spawned.

Later:

```text
42 rounds
```

after being dropped.

Later still:

```text
8 rounds
```

after being used and dropped again.

The weapon should not reset to its default ammo quantity merely because ownership changes.

---

# 31. Picking Up Previously Used Weapons

A player should be able to pick up a weapon that has already been partially used.

Example ground weapon:

```text
Shotgun

Magazine:
2

Reserve:
4
```

After pickup, the player's Shotgun should contain:

```text
Magazine:
2

Reserve:
4
```

Do not refill it.

---

# 32. No Duplicate-Weapon Ammo Scavenging

Remove the original REQ-023 rule where walking over another copy of a weapon automatically adds ammunition.

Under the revised system:

> A weapon is a self-contained temporary resource.

Do not automatically convert duplicate weapons into reserve ammunition.

---

# 33. Same-Weapon Pickup Behavior

Suppose the player currently possesses:

```text
Rifle:
20 rounds remaining
```

and finds another Rifle:

```text
Rifle:
70 rounds remaining
```

Do **not** automatically combine the ammunition.

Instead, treat the ground Rifle as another weapon object.

The player may deliberately choose to replace their current Rifle with the ground Rifle.

If they do:

```text
New temporary weapon:
Rifle with 70 rounds
```

and their previous:

```text
Rifle with 20 rounds
```

is dropped.

This keeps the rules consistent and makes ammunition state visible through the weapons themselves.

---

# 34. Same Weapon Replacement Prompt

If the player already has the same temporary weapon type, the prompt may clarify the ammo difference.

Example:

```text
RIFLE
70 rounds remaining

Hold E to Swap
```

Displaying ammunition in the pickup prompt is preferred if easy to implement.

It is not mandatory for the first implementation.

---

# 35. No Traditional Reserve Ammo Pickup System

REQ-023 Revision A should remove or disable the concept of:

```text
Walk over weapon
→
Absorb ammo
```

Temporary ammunition belongs to the temporary weapon instance.

The player is acquiring the weapon **and its remaining ammunition together**.

---

# 36. Weapon Spawn Configuration

Map-placed temporary weapons should have configurable starting ammo.

Example Inspector configuration:

```text
Weapon:
Rifle

Starting Magazine:
30

Starting Reserve:
60
```

or:

```text
Starting Total Ammo:
90
```

depending on architecture.

This allows different world pickups to theoretically contain different ammunition quantities later.

---

# 37. Generic Temporary Weapon Example

Example Rifle configuration:

```text
Weapon:
Rifle

Magazine Size:
30

Starting Magazine:
30

Reserve:
60

Total Usable Ammo:
90
```

Example Shotgun:

```text
Weapon:
Shotgun

Magazine Size:
6

Starting Magazine:
6

Reserve:
12

Total Usable Ammo:
18
```

These values are examples only.

Balancing is not part of this requirement.

---

# 38. HUD Ammo Display

The HUD should clearly communicate ammunition differently for the two weapon classes.

## Permanent Pistol

Possible display:

```text
8 / ∞
```

or:

```text
8
∞
```

The exact styling is not important yet.

The player should understand that reserve pistol ammunition is unlimited.

## Temporary Weapon

Display normal finite ammunition:

```text
24 / 60
```

or the project's existing magazine/reserve format.

---

# 39. Temporary Weapon Scarcity

Do not regenerate temporary weapon ammo over time.

Do not give temporary weapons unlimited reserve ammunition.

Do not refill them when switching to the pistol.

The only ammunition available is the ammunition contained in that temporary weapon instance.

---

# 40. Multiplayer Authority

Continue using server/host authority for important world-weapon interactions.

The authoritative state should determine:

* Whether a weapon exists.
* Which player picked it up.
* Which player owns it.
* How much ammunition remains.
* When it is dropped.
* Where it is dropped.
* When it is exhausted.

---

# 41. Prevent Duplicate Pickups

If two players attempt to acquire the same world weapon simultaneously, only one should succeed.

Example:

```text
One Rifle exists
```

Player 1 and Player 2 both interact.

Expected:

```text
Exactly one player receives Rifle.
```

The Rifle must not be duplicated.

---

# 42. Networked Ammo State

Remaining temporary weapon ammunition is meaningful world state.

If a Rifle has:

```text
27 total rounds remaining
```

when Player 1 dies, the networked dropped Rifle must still have:

```text
27 total rounds
```

for Player 2.

Do not allow different clients to perceive different ammo amounts for the same world weapon.

---

# 43. First-Person Weapon Visibility

Continue preserving local first-person weapon behavior.

Each player should see:

* Their own pistol.
* Their own temporary weapon when active.

Another player's inventory change must not incorrectly replace the local first-person weapon.

---

# 44. Switching Animations

Where existing FPS Engine animations allow it:

```text
Pistol
↓
Holster/lower
↓
Temporary weapon
↓
Draw/raise
```

and vice versa.

Preserve existing weapon animation functionality where practical.

Do not rewrite working FPS systems unnecessarily.

---

# 45. Dropping Weapon Presentation

When a temporary weapon is dropped due to:

* Replacing it.
* Dying.

spawn its world representation at an appropriate position.

Avoid:

* Spawning inside level geometry.
* Spawning inside the player's body.
* Immediate accidental re-pickup.
* Large physics impulses that throw it far away.

A small natural drop near the player/death location is sufficient.

---

# 46. Pickup Cooldown

Consider a very short pickup lockout for the player who just dropped a weapon.

This is especially useful when swapping temporary weapons.

Example:

```text
Player drops Rifle
↓
0.5 second self-pickup lockout
```

Exact timing is configurable.

This prevents immediate accidental reversal of the swap.

---

# 47. Existing REQ-023 Systems to Preserve

Reuse existing implemented REQ-023 functionality where it remains applicable.

Likely reusable systems include:

* Generic weapon definitions.
* Ground weapon prefabs.
* Interaction raycast.
* Pickup prompts.
* Network pickup authority.
* Weapon switching input.
* First-person equip logic.
* Dropped weapon spawning.
* Runtime ammo tracking.

Do not discard good working code merely because the design has changed.

---

# 48. Existing REQ-023 Systems to Remove or Refactor

The following previous behavior should be removed or changed:

## Remove

```text
Two equivalent weapon slots
```

## Remove

```text
Ruger + Rifle starting loadout
```

## Remove

```text
Automatic duplicate-weapon ammo scavenging
```

## Remove

```text
Finite pistol reserve ammunition
```

## Remove

```text
Ability to replace the permanent pistol
```

## Replace with

```text
Permanent infinite-reserve pistol
+
One optional finite temporary weapon
```

---

# 49. Suggested Inventory State

A simplified runtime inventory might conceptually track:

```text
PermanentPistol

TemporaryWeapon
```

where:

```text
TemporaryWeapon = null
```

is valid.

Example:

```text
PermanentPistol:
Pistol instance

TemporaryWeapon:
Rifle instance
```

Do not force this exact class structure if the existing architecture supports the same behavior cleanly.

---

# 50. Important State Rules

The following states are valid:

```text
Pistol active
No temporary weapon
```

```text
Pistol active
Rifle stored
```

```text
Rifle active
Pistol available
```

The following states are invalid:

```text
No pistol
```

```text
Two temporary weapons
```

```text
Three weapons
```

```text
Temporary weapon with zero total ammo remaining in inventory
```

---

# 51. Acceptance Criteria

REQ-023 Revision A is complete when:

* [ ] Players spawn with the pistol only.
* [ ] Players no longer spawn with a rifle.
* [ ] The pistol is permanent.
* [ ] The pistol cannot be dropped.
* [ ] The pistol has unlimited reserve ammunition.
* [ ] The pistol still has a finite magazine.
* [ ] The pistol still requires reloading.
* [ ] The player may carry at most one temporary weapon.
* [ ] Temporary weapons must be deliberately picked up.
* [ ] Walking over a weapon does not automatically equip it.
* [ ] E can be used for keyboard pickup interaction.
* [ ] Gamepad west button can be used for pickup interaction.
* [ ] Acquiring a temporary weapon automatically equips it.
* [ ] Mouse wheel switches between pistol and temporary weapon.
* [ ] Q switches between pistol and temporary weapon.
* [ ] Gamepad north button / Xbox Y switches between pistol and temporary weapon.
* [ ] Switching does nothing safely when no temporary weapon exists.
* [ ] Temporary weapons have finite ammunition.
* [ ] Temporary weapons retain normal magazine/reload behavior.
* [ ] Temporary weapons do not regenerate ammunition.
* [ ] When temporary weapon ammo reaches zero, the weapon is removed.
* [ ] When a temporary weapon is exhausted, the pistol becomes active automatically.
* [ ] Picking up another temporary weapon replaces the current temporary weapon.
* [ ] The replaced temporary weapon is dropped.
* [ ] Dropped temporary weapons retain remaining ammo.
* [ ] Picking up the same weapon type does not automatically merge ammo.
* [ ] Same-type weapon pickup works as a deliberate weapon swap.
* [ ] The old automatic ammo-scavenging behavior from REQ-023 is removed.
* [ ] On death, the pistol does not drop.
* [ ] On death, any temporary weapon does drop.
* [ ] Death-dropped weapons retain remaining ammo.
* [ ] Death-dropped weapons remain available after the player respawns.
* [ ] Players respawn with pistol only.
* [ ] Two players cannot duplicate the same world weapon through simultaneous pickup.
* [ ] Remaining weapon ammo is synchronized correctly in multiplayer.
* [ ] Existing firing, aiming, weapon audio, and weapon animation systems continue functioning.

---

# 52. Testing Procedure

## Test A — Starting Loadout

1. Spawn Player 1.
2. Inspect inventory.

Expected:

```text
Pistol only
```

No Rifle should be present.

---

## Test B — Infinite Pistol Ammo

1. Fire the entire pistol magazine.
2. Reload.
3. Repeat many times.

Expected:

The pistol always reloads successfully.

The magazine remains finite.

The player never permanently runs out of pistol ammunition.

---

## Test C — Rifle Pickup

1. Place a Rifle in the scene.
2. Walk near it without pressing Interact.

Expected:

The Rifle remains on the ground.

3. Deliberately interact.

Expected:

```text
Permanent:
Pistol

Temporary:
Rifle
```

Rifle becomes active.

---

## Test D — Weapon Switching

With Rifle equipped:

1. Press Y.

Expected:

Pistol becomes active.

2. Press Y again.

Expected:

Rifle becomes active.

Repeat with Q and mouse wheel.

---

## Test E — No Temporary Weapon Switching

Spawn with pistol only.

Press:

```text
Y
Q
Mouse Wheel
```

Expected:

Nothing incorrect happens.

Pistol remains active.

No errors occur.

---

## Test F — Temporary Weapon Depletion

Give Rifle:

```text
Magazine:
30

Reserve:
30
```

Fire all 60 rounds, reloading as necessary.

Expected after final round:

```text
Rifle exhausted
↓
Rifle removed
↓
Pistol equipped
```

---

## Test G — Replace Temporary Weapon

Inventory:

```text
Pistol
Rifle
```

Rifle has:

```text
37 rounds remaining
```

Pick up Shotgun.

Expected:

```text
Pistol
Shotgun
```

Rifle drops with:

```text
37 rounds remaining
```

---

## Test H — Same Weapon Replacement

Player Rifle:

```text
20 rounds remaining
```

Ground Rifle:

```text
70 rounds remaining
```

Deliberately pick up ground Rifle.

Expected:

Player now has:

```text
Rifle:
70 rounds
```

Old Rifle remains on ground:

```text
Rifle:
20 rounds
```

No ammunition is automatically merged.

---

## Test I — Death Without Temporary Weapon

Player has:

```text
Pistol only
```

Die.

Expected:

No weapon pickup is created from the pistol.

Respawn with pistol.

---

## Test J — Death With Temporary Weapon

Player has:

```text
Pistol

Shotgun:
8 rounds remaining
```

Die.

Expected:

Shotgun drops at death location with:

```text
8 rounds remaining
```

Player respawns with:

```text
Pistol only
```

---

## Test K — Death While Pistol Active

Inventory:

```text
Active:
Pistol

Temporary:
Rifle with 43 rounds
```

Die.

Expected:

Rifle still drops with 43 rounds.

The fact that it was inactive does not protect it.

---

## Test L — Other Player Retrieves Death Drop

Player 1 dies and drops:

```text
Rifle:
27 rounds
```

Player 2 deliberately picks it up.

Expected:

Player 2 receives exactly that Rifle with approximately the same runtime ammo state:

```text
27 rounds remaining
```

---

## Test M — Simultaneous Multiplayer Pickup

Player 1 and Player 2 attempt to pick up the same Shotgun.

Expected:

Only one receives it.

No duplicate weapon is created.

---

# 53. Out of Scope

This revision does not require:

* Two temporary weapons.
* Traditional two-slot weapon inventory.
* Backpack inventory.
* Weapon wheel.
* Ammo boxes.
* Automatic ammunition scavenging.
* Combining ammo from duplicate weapons.
* Dropping the pistol.
* Limited pistol reserve ammunition.
* Weapon durability.
* Attachments.
* Weapon upgrades.
* Weapon rarity.
* Power-weapon timers.
* Weapon spawn timers.
* Final weapon balancing.
* Final pistol model.
* Final temporary weapon models.

---

# 54. Future Compatibility

The system should support future mechanics such as:

```text
Map weapon spawn locations
Weapon respawn timers
Rare power weapons
Weapons with very small ammo pools
Weapons with one magazine only
Dropped enemy weapons
Special weapon pickup effects
Weapon-specific HUD information
```

A powerful weapon could therefore be balanced primarily through ammunition scarcity.

Example:

```text
Rocket Launcher

Magazine:
1

Reserve:
2

Total shots:
3
```

Once all three shots are used:

```text
Rocket Launcher disappears
↓
Player returns to pistol
```

This is intentional and should be supported by the architecture.

---

# 55. Final Intended Gameplay Loop

The target weapon loop is:

```text
Spawn
↓
Permanent pistol
↓
Explore / fight
↓
Find temporary weapon
↓
Deliberately pick it up
↓
Use or conserve finite ammo
↓
Switch freely between it and pistol
↓
Temporary ammo depleted
→ return to pistol

OR

Find another temporary weapon
→ drop current temporary weapon
→ acquire new one

OR

Die
→ temporary weapon drops with remaining ammo
→ respawn with pistol
```

This replaces the original REQ-023 two-equivalent-weapon inventory model.

The revised system should become the authoritative weapon-inventory behavior going forward.
