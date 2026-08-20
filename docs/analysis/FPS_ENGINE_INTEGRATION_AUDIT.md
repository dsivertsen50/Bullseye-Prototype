# FPS Engine Integration Audit

**Date:** 2026-08-19  
**Branch:** `experiment/fps-engine-integration`  
**Scope:** Architectural analysis only. No gameplay code, packages, prefabs, scenes, or ProjectSettings were modified. The Cowsins FPS Engine project was treated as read-only.

**Projects compared**

| Role | Path | Notes |
|---|---|---|
| Authoritative game | `Bullseye Prototype_20260811` | Git repo. Netcode for GameObjects multiplayer. Keep existing player, spawn, respawn, bullseye, health, and damage systems. |
| Read-only reference | `Bullseye_Prototype_20260819/FPS Engine` | Clean Cowsins FPS Engine install. Do not modify. |

**Classification key**

| Code | Meaning |
|---|---|
| **A. Adopt** | Copy or reuse this Cowsins subsystem (or a clearly bounded subset of its assets) with little or no rewrite. |
| **B. Adapt / bridge** | Keep the idea or asset, but wrap, subset, or re-host it so Bullseye remains authoritative. |
| **C. Do not use** | Conflicts with Bullseye multiplayer, bullseye rules, or would replace a working system. |
| **D. Requires further investigation** | Not blocked, but not proven. Needs a later spike, license check, or HDRP conversion test. |

---

## Executive recommendation

Cowsins weapon **assets** (pistol prefab, `Weapon_SO`, animator, SFX, muzzle VFX) can be used without replacing Bullseye's network player.

Cowsins weapon **runtime** (`WeaponController` + `WeaponStates` + `PlayerDependencies`) **cannot** be dropped onto `Player.prefab` as-is. It is a single-player player stack, not a weapon plugin.

The integration strategy should be:

1. Keep Bullseye as the authority for Netcode, ownership, spawn, respawn, movement, look, health, and bullseye damage.
2. Adopt Cowsins first-person pistol presentation and, later, local fire feel (animation, recoil, sway, ADS pose, audio/VFX).
3. Bridge Cowsins systems that expect `IPlayerMovementStateProvider`, `IDamageable`, `InputManager`, and `PoolManager`.
4. Do not adopt Cowsins health, hitscan damage application, player controller, pause/UI shell, pickups, or static input.

Smallest useful vertical slice:

> Local owner sees and fires one Cowsins pistol (viewmodel + fire animation + muzzle/SFX). Hitscan damage, bullseye vulnerability, death, and respawn remain Bullseye's existing NGO path.

---

## Headline findings

1. **Cowsins is single-player.** No `NetworkBehaviour`, Netcode, Mirror, or Photon references exist under `Assets/Cowsins`.
2. **Cowsins is Built-in RP.** `Packages/manifest.json` has no HDRP/URP. `GraphicsSettings.m_CustomRenderPipeline` is empty. Weapon materials use the Built-in Standard shader (`fileID: 46`) and Built-in particle shaders. Bullseye is HDRP 17.4.0.
3. **WeaponController is not standalone.** `Start()` requires `PlayerDependencies` on the same GameObject. `PlayerDependencies.Awake()` hard-requires movement, stats, interact, control, and multiplier interfaces, plus serialized refs to input, camera FOV, camera effects, weapon effects, UI, and crosshair.
4. **Damage paths are incompatible.** Cowsins applies `IDamageable.Damage(float, bool isHeadshot)` via tags `Critical` / `BodyShot`. Bullseye applies server-authoritative zone damage only after `BullseyeTarget.TryRegisterHit(shooterClientId)`.
5. **Input architectures collide.** Cowsins `InputManager` owns a **static** `PlayerActions` instance and reads `Gamepad.current`. Bullseye binds devices per local owner through `LocalPlayerInputBinding` (required for dual-controller local multiplayer).
6. **Movement colliders collide.** Cowsins player is `Rigidbody` + capsule. Bullseye player is `CharacterController` + `NetworkTransform`. Replacing Bullseye movement would also replace crouch replication and bullseye-mover coupling.
7. **Unity versions match.** Both projects are `6000.4.4f1`. Input System is `1.19.0` in both. That is not a blocker.
8. **REQ-014 already created a local-only weapon mount.** `Player.prefab` has `Camera/WeaponView/WeaponMount` with nested `Ruger22_FirstPerson`, gated by `FirstPersonWeaponView`. That is the correct host for a Cowsins pistol (replace the Ruger), not `CowsinsFPSController`.
9. **Bullseye pose is simulated, not transform-synced.** `BullseyeMover` replicates a server RNG seed plus jump/crouch/turn influence lists; all peers run the same 30 Hz path. Do not put `NetworkTransform` on the bullseye or let a Cowsins hitbox replace it.
10. **Hitscan selection is client-trusted today.** The shooter owner raycasts locally; the server applies zone damage after `HitServerRpc`. Cowsins `HitscanShootStyle` is also camera-local. Adopting it would not add server validation.

---

## 1. Cowsins player / controller architecture

**Classification: C. Do not use** as the networked player.  
**B. Adapt / bridge** only the interfaces weapons query (`IPlayerMovementStateProvider`, `IPlayerControlProvider`, `IPlayerMultipliers`).

### How Cowsins is composed

Primary prefab: `FPS Engine/Assets/Cowsins/Prefabs/PlayerControllers/CowsinsFPSController.prefab`.  
Alternate: `MovementCowsinsFPSController.prefab` (movement-focused; still not networked). Neither is a candidate to replace `Player.prefab`.

Observed hierarchy (simplified):

```text
CowsinsFPSController
├── Player                  (Rigidbody + PlayerMovement + WeaponController + PlayerDependencies + ...)
├── CameraPivot
│   └── CameraContainer / Head
│       └── Main Camera     (excludes Weapons + UI layers)
│           └── WeaponCamera (Weapons + UI overlay camera)
├── WeaponHolder
├── WeaponsEffects
├── FastActions
├── PlayerGraphics
├── GeneralManagers         (SoundManager, CoinManager, ExperienceManager, AddonManager)
├── PoolManager
└── InputManager
```

Core player scripts, all in namespace `cowsins`:

| Script | Path | Role |
|---|---|---|
| `PlayerDependencies` | `Scripts/Player/PlayerDependencies.cs` | Service locator. Must not be removed, per its own comment. Resolves every player interface in `Awake`. |
| `PlayerMovement` | `Scripts/Movement/PlayerMovement.cs` | Rigidbody mover. `[RequireComponent(typeof(Rigidbody))]`. Implements `IPlayerMovementStateProvider` and `IPlayerMovementEventsProvider`. |
| `PlayerStates` | `Scripts/Player/PlayerState/PlayerStates.cs` | Movement FSM (default, jump, crouch, dash, climb, dead). Holds a concrete `PlayerMovement` reference. |
| `PlayerStats` | `Scripts/Player/PlayerStats.cs` | Local health/shield. Implements `IDamageable`. |
| `PlayerControl` | `Scripts/Player/PlayerControl.cs` | Controllable / movement-controllable flags. Tied to `PauseMenu.isPaused` and `PlayerStats.IsDead`. |
| `PlayerMultipliers` | `Scripts/Player/PlayerMultipliers.cs` | Damage / heal / weight multipliers. |
| `PlayerGraphics` | `Scripts/Player/PlayerGraphics.cs` | Third-person visual follower using `PlayerOrientation`. |
| `PlayerDebugger` | `Scripts/Player/PlayerDebugger.cs` | Editor/debug helper. |

Movement behaviours under `Scripts/Behaviours/Movement Behaviours/`:

- `BasicMovementBehaviour`, `GroundDetectionBehaviour`, `JumpBehaviour`, `CrouchSlideBehaviour`
- `CameraLookBehaviour` (look + recoil application)
- `DashBehaviour`, `WallRunBehaviour`, `WallBounceBehaviour`, `ClimbLadderBehaviour`
- `GrapplingHookBehaviour`, `StaminaBehaviour`, `FootstepsBehaviour`, `VelocityHandlerBehaviour`, `SpeedLinesBehaviour`

### Coupling that blocks a drop-in

`PlayerDependencies` requires **all** of the following on the same GameObject:

- `IPlayerMovementEventsProvider` / `IPlayerMovementStateProvider`
- `IWeaponReferenceProvider` / `IWeaponBehaviourProvider` / `IWeaponRecoilProvider` / `IWeaponEventsProvider`
- `IDamageable` / `IPlayerStatsProvider` / `IPlayerStatsEventsProvider` / `IFallHeightProvider`
- `IInteractManagerProvider` / `IInteractEventsProvider`
- `IPlayerControlProvider`
- `IPlayerMultipliers`

Plus serialized MonoBehaviours: `InputManager`, `CameraFOVManager`, `CameraEffects`, `WeaponEffects`, `UIController`, `Crosshair`, `UIEffects`.

`PlayerStates` uses `GetComponent<PlayerMovement>()` (concrete type). Jump pads, bullets, and several extras also look up `PlayerMovement` or `PlayerStats` by concrete type.

### Bullseye counterpart (authoritative)

`Assets/Prefabs/Player.prefab` components:

- `NetworkObject`, `NetworkTransform`
- `CharacterController`
- `PlayerMovement`, `PlayerLook`, `PlayerAimZoom`, `PlayerShoot`
- `PlayerHealth`, `PlayerHealthHud`, `PlayerHaptics`
- `BullseyeMover`, `BullseyeTarget`
- `PlayerNetworkSetup`, `LocalPlayerInputBinding`
- `Reticle`, `FirstPersonWeaponView`

`PlayerNetworkSetup` disables camera / look / aim zoom / haptics on non-owners. `PlayerMovement` replicates crouch through a `NetworkVariable<bool>`. `PlayerHealth` is server-authoritative with networked death and respawn.

**Do not replace this prefab with `CowsinsFPSController`.** Doing so would discard NGO ownership, crouch replication, bullseye attachment, and respawn.

If a later spike needs Cowsins bob/recoil/ADS, implement a thin Bullseye adapter that satisfies only the **weapon-facing** interfaces (`Grounded`, `IsCrouching`, `IsAiming` consumers, `IsControllable`) rather than hosting `PlayerMovement.cs`.

---

## 2. Input architecture

**Classification: C. Do not use** Cowsins `InputManager` / static `PlayerActions` as the live input path.  
**B. Adapt / bridge** by adding Reload (and later weapon-switch) actions to Bullseye `PlayerControls.inputactions` and feeding those into any adopted weapon feel code.

### Cowsins

- Asset: `Assets/Cowsins/Inputs/PlayerActions.inputactions`
- Generated: `Assets/Cowsins/Inputs/PlayerActions.cs` (Input System 1.19.0)
- Runtime: `Scripts/Managers/InputManager.cs`

Maps:

- `GameControls`: Jumping, Dashing, Reloading, Melee, Crouching, Sprinting, Firing, Grapple, Scrolling, Aiming, Interacting, Inspect, Drop, ChangeWeapons, Pause, Movement, ToggleFlashLight, InventoryOpen, InventoryFavOpen, ToggleTipsCanvas
- `UI`: Back, Select, WestButton, NorthButton, and related UI actions

Important implementation details:

- `public static PlayerActions inputActions` — one shared asset for the process.
- `Init()` constructs it once: `if (inputActions == null) inputActions = new PlayerActions();`
- `Update()` reads `Mouse.current` and **`Gamepad.current`**, not a per-player device list.
- Rebind overrides are stored in `PlayerPrefs`.
- `InputManager` is a child on `CowsinsFPSController`, but the action asset is static.

Weapon shooting is not polled from `PlayerShoot`-style code. `WeaponDefaultState` watches `inputManager.Shooting` and switches into `WeaponShootingState`, which calls `ShootBehaviour.Shoot()`.

### Bullseye

- Asset: `Assets/Input/PlayerControls.inputactions`
- Generated: `Assets/Input/PlayerControls.cs`
- Also present: default `Assets/InputSystem_Actions.inputactions` (unused by gameplay)
- Per-owner device filter: `LocalPlayerInputBinding`

Bullseye Player map today: Move, Look, Jump, Fire, Sprint, Aim, Crouch.

Each gameplay script takes an `InputActionReference` and enables the action. Non-owners disable look/aim components without disabling the shared actions (explicitly documented in `PlayerLook` / `PlayerShoot`).

`LocalPlayerInputBinding`:

- Runs at `DefaultExecutionOrder(-1000)`
- Assigns one gamepad per `NetworkManager.LocalClientId`
- Optionally gives keyboard/mouse to player 0
- Pushes XInput state so unfocused editor windows still receive pads

Both projects set `activeInputHandler: 1` (Input System package). That is compatible.

### Collision

Enabling Cowsins `InputManager` on networked players would:

- Fight Bullseye's per-player `playerActions.devices` assignment
- Make every local player read `Gamepad.current`
- Duplicate fire / aim / crouch / sprint handling
- Break REQ-002 dual-controller testing

**Bridge rule:** keep `PlayerControls` as the only enabled gameplay map. If Cowsins weapon code is later hosted, inject Bullseye fire/aim/reload into it; do not enable `PlayerActions` globally.

---

## 3. First-person weapon / viewmodel architecture

**Classification: A. Adopt** `Pistol_WeaponObject`, its animator, and the WeaponHolder/WeaponCamera *idea*.  
**B. Adapt / bridge** mounting, layering, and owner-only visibility onto Bullseye `FirstPersonWeaponView`.  
**D.** FPS arms rig / `ParentConstraint` (see Animation).

### Cowsins

Weapons are not mesh-only. Each inventory weapon is a prefab with `WeaponIdentification`:

- `Assets/Cowsins/Prefabs/Weapons/Pistol_WeaponObject.prefab` (layer 6 = `Weapons`)
- Also: SMG, Shotgun, Rifle, Revolver, RocketLauncher, Katana, `CowsinsBlankWeaponTemplate`

`WeaponIdentification` holds:

- `Weapon_SO weapon`
- `Transform[] FirePoint`
- `shellEjectPoint`, `aimPoint`, `headBone`
- Runtime ammo (`bulletsLeftInMagazine`, `totalBullets`, `magazineSize`)
- Runtime stats copied from the SO in `Awake` (damage, fireRate, spread, aim, reload, VFX, SFX)
- `IShootStyle` assigned at unholster time
- `Animator` from children (`keepAnimatorStateOnDisable = true`)
- Attachment state (`AttachmentStateManager`, default + compatible attachments)

`WeaponInventorySystem.InstantiateWeapon()` parents the prefab under `WeaponControllerSettings.weaponHolder` and `UnHolster()` enables it, assigns `Id`, and calls `WeaponSpecificEffects.Initialize()`.

Viewmodel cameras:

- `Main Camera` culling mask `262047` — world, excluding Weapons (layer 6) and UI (layer 5)
- `WeaponCamera` child, depth 0, clear flags depth-only, culling mask `96` (layers 5 + 6)

This is an overlay-camera first-person weapon setup. Weapon geometry is not meant to be seen by the world camera.

`WeaponSpecificEffects` (on the weapon prefab) implements look-based sway (Simple or PivotBased).  
`WeaponEffects` (on the player) implements locomotion bob + jump motion on `weaponEffectsTransform`.

### Bullseye (REQ-014)

`FirstPersonWeaponView`:

- Resolves `Camera/WeaponView` and `WeaponMount`
- Recursively assigns layer `FirstPersonWeapon`
- Disables colliders / kinematic rigidbodies on the viewmodel
- Shows only for the owner, and hides while dead

`Player.prefab` hierarchy:

```text
Player
├── Capsule
├── Camera
│   └── WeaponView          (layer FirstPersonWeapon)
│       └── WeaponMount
│           └── Ruger22_FirstPerson   (REQ-014; replace in Phase 1)
└── Bullseye                (SphereCollider + BullseyeTarget; no NetworkTransform)
```

Bullseye's camera currently culls **everything** (`m_Bits: 4294967295`). There is a `FirstPersonWeapon` layer (index 6) but no overlay camera yet. World camera therefore also renders the viewmodel, which is acceptable for a prototype but will clip through world geometry more than Cowsins' two-camera setup.

**Adopt the Cowsins pistol prefab as the viewmodel source. Host it under `WeaponMount`. Keep `FirstPersonWeaponView` as the network visibility gate.** Do not parent weapons to a Cowsins `WeaponHolder` on a second player object.

---

## 4. Weapon ScriptableObjects / configuration

**Classification: A. Adopt** `Weapon_SO` as the pistol configuration format, if Cowsins scripts are imported.  
**D.** Whether to keep the full SO surface or a Bullseye-specific subset.

### Types

| Type | Path | Role |
|---|---|---|
| `Item_SO` | `Scripts/PickUpSystem/Item_SO.cs` | Name, icon, pickup graphics, stack, weight. Inventory Pro `#if` optional. |
| `Weapon_SO` | `Scripts/Weapons/Weapon_SO.cs` | `[CreateAssetMenu(.../New Weapon)]`. Extends `Item_SO`. |
| `AttachmentIdentifier_SO` | `Scripts/Weapons/Attachments/` | Attachment identity. |
| `BulletTypeIdentifier_SO` | `Scripts/PickUpSystem/` | Ammo type. Unused on the stock Pistol (`bulletTypeIdentifier: {fileID: 0}`). |
| `ProceduralShot_SO` | `Scripts/Effects/ProceduralShot_SO.cs` | Kick pattern curves. |

Stock weapon assets: `ScriptableObjects/Weapons/{Pistol, Revolver, SMG, Rifle, Shotgun, RocketLauncher, Katana}.asset`.

### Pistol.asset (stock)

- `shootStyle: 0` → `Hitscan`
- `shootMethod: 1` → `PressAndHold` (semi/auto depending on fire rate; pistol fireRate `0.28`)
- `reloadStyle: 0` → `defaultReload`
- `allowAim: 1`
- `applyRecoil: 1`
- `magazineSize: 17`
- `infiniteBullets: 0`
- `weaponObject` → `Pistol_WeaponObject.prefab`

`Weapon_SO` also stores fire rate, spread, aim FOV/distance/rotation, recoil curves, audio clip arrays, muzzle VFX, bullet trail, bullet holes, procedural shot toggle, weight, and attachment compatibility.

**This is the right config object if we import Cowsins weapon scripts.** For the first slice, only a handful of fields matter (prefab reference, fireRate, audio, muzzle VFX, animator-related flags). Do not depend on Inventory Pro (`INVENTORY_PRO_ADD_ON` is undefined in this install).

---

## 5. Shooting / hitscan / projectile architecture

**Classification: C. Do not use** Cowsins damage application (`HitDetectionSystem` → `IDamageable.Damage`).  
**B. Adapt / bridge** if we later want Cowsins spread/trails/penetration **visuals**, by redirecting `OnHit` into `BullseyeTarget`.  
Keep `PlayerShoot` as damage authority for the first slice.

### Cowsins pipeline

```text
InputManager.Shooting
  → WeaponDefaultState / WeaponShootingState
    → ShootBehaviour.Shoot()
      → WeaponIdentification.Shoot(spread, damageMul, shakeMul)
        → IShootStyle.Shoot(...)
          HitscanShootStyle | ProjectileShootStyle | MeleeShootStyle | CustomShootStyle
            → WeaponControllerEvents.OnHit
              → HitDetectionSystem.Hit()
                → IDamageable.Damage(finalDamage, isHeadshot)
```

`HitscanShootStyle`:

- Ray origin: **main camera position**, direction via `CowsinsUtilities.GetSpreadDirection`
- `Physics.Raycast(..., weapon.bulletRange, hitLayer)`
- Optional second ray for penetration
- Bullet trails from `FirePoint` through `PoolManager`
- No self-hit filter by `NetworkObject`
- No bullseye component lookup

`HitDetectionSystem.Hit()`:

- Tag `Critical` → parent `IDamageable.Damage(dmg * criticalMultiplier, true)`
- Tag `BodyShot` → parent `IDamageable.Damage(dmg, false)`
- Else collider `IDamageable.Damage(dmg, false)`
- Optional distance damage falloff from `Weapon_SO`

`Bullet.cs` (projectile path) uses the same tag/`IDamageable` rules and can optionally hurt the firing player.

`IDamageable` is a local interface: `void Damage(float damage, bool isHeadshot);`

### Bullseye pipeline (authoritative)

```text
Owner PlayerShoot.Update
  → fireAction.WasPressedThisFrame()
    → haptic rumble
    → Physics.RaycastNonAlloc from camera forward, range 100
    → skip colliders whose parent NetworkObject is the shooter
    → nearest remaining hit
    → BullseyeTarget.TryRegisterHit(OwnerClientId)
      → reject self / dead
      → PlayerHealth.RegisterBullseyeHit()
        → HitServerRpc
          → reject if sender == owner
          → zone damage (head 8 / torso 4 / lower 2)
          → death / respawn on server
```

Hitscan is camera-forward, closest-hit, self-filtered, bullseye-only. Non-bullseye body hits do nothing. The **raycast itself is not re-run on the server**; damage amount and death are. That is acceptable for this prototype. Cowsins hitscan is the same camera-origin pattern with no NGO gate, so it is not an authority upgrade.

### Can Cowsins shooting run without replacing the network player?

**Hitscan *simulation* yes; damage *application* no, not without a bridge.**

If `HitscanShootStyle` were hosted on the Bullseye player:

- It would still need `PlayerDependencies`, `IPlayerMultipliers`, `PoolManager`, and `Weapon_SO`
- It would damage the wrong thing (`PlayerStats` / `EnemyHealth`, not `BullseyeTarget`)
- It would not go through `HitServerRpc`, so clients could apply local health
- It would not exclude the shooter's colliders the Bullseye way
- Spread would desync aim from the reticle unless spread is zero

**First-slice rule:** keep `PlayerShoot` for all damage. Use Cowsins only to present the shot (animation, muzzle, audio, later recoil).

A later bridge, if desired:

1. Subscribe to Cowsins `OnHit` **or** keep Bullseye raycast.
2. On a valid `BullseyeTarget`, call `TryRegisterHit`.
3. Ignore `IDamageable` on players.
4. Do not put `PlayerStats` on the networked player.

---

## 6. Recoil

**Classification: B. Adapt / bridge** `RecoilSystem` (or a simplified copy of its curves) into `PlayerLook`.  
**C.** Do not adopt `CameraLookBehaviour` as the look controller.

`RecoilSystem`:

- Listens to `OnShootHitscanProjectile`
- Evaluates `Weapon_SO.recoilX` / `recoilY` AnimationCurves
- Exposes `recoilPitchOffset` / `recoilYawOffset`
- `WeaponController` implements `IWeaponRecoilProvider` by forwarding those offsets

`CameraLookBehaviour.Tick()` applies them:

```text
cameraYaw  += mouseX + RecoilYawOffset * dt
cameraPitch -= mouseY - RecoilPitchOffset * dt
```

Bullseye `PlayerLook` currently rotates the player yaw and camera local pitch from input only. There is no recoil.

**Bridge:** on owner fire, add Cowsins (or SO-driven) pitch/yaw offsets inside `PlayerLook`. Do not run Cowsins look code; it also owns sensitivity, invert, wallrun roll, and aim assist, and reads `Gamepad.current` through `InputManager`.

Recoil must stay **owner-local**. Remote players should not pitch from recoil; `NetworkTransform` already replicates body rotation, and first-person recoil is a viewmodel/camera effect.

---

## 7. Weapon sway / bobbing

**Classification: A. Adopt** `WeaponSpecificEffects` (look sway on the pistol prefab).  
**B. Adapt / bridge** `WeaponEffects` locomotion bob, because it reads `Rigidbody.linearVelocity`, `IPlayerMovementStateProvider.Grounded`, and `InputManager.X/Y`.

`WeaponSpecificEffects`: Simple positional/tilt sway from look input, or pivot-based sway. Initialized from `WeaponInventorySystem.UnHolster()`.

`WeaponEffects.Initialize(PlayerDependencies)`:

- Subscribes to jump/land events
- Bobs from grounded velocity and move input
- Writes `weaponEffectsTransform` local pose

Bullseye has no sway/bob today. REQ-014 explicitly deferred this.

For the first slice, **sway-only is enough**. Bob can wait until a movement-state adapter exists (`Grounded` from `CharacterController.isGrounded`, move axes from Bullseye Move action). Do not add a Rigidbody to the player just to feed Cowsins bob.

---

## 8. Aiming / ADS

**Classification: B. Adapt / bridge** Cowsins weapon pose ADS. Keep Bullseye `PlayerAimZoom` as FOV authority unless/until a single FOV owner is chosen.  
**C.** Do not run both `CameraFOVManager` and `PlayerAimZoom` on the same camera.

Cowsins `AimBehaviour`:

- Driven by `InputManager.Aiming` and `Weapon_SO.allowAim`
- Moves `WeaponIdentification.aimPoint` toward a camera-forward point (`nearClipPlane + aimDistance`)
- Applies `aimingRotation` and optional scope offset
- Fires `OnAimStart` / `OnAiming` / `OnAimStop` for FOV and spread

Cowsins `CameraFOVManager`:

- Lives on the camera
- Lerps FOV for run, wallrun, aim (`Weapon_SO.aimingFOV`), and shoot punch
- Requires `IPlayerMovementStateProvider.NormalFOV` / `RunningFOV` / `WallRunningFOV`

Bullseye `PlayerAimZoom`:

- Toggle or hold Aim action
- Reduces camera FOV by `fovReduction`
- Owner-only (disabled on remotes by `PlayerNetworkSetup`)
- Independent of any weapon pose

**Risk:** two systems writing `camera.fieldOfView`.

**First slice:** keep `PlayerAimZoom` FOV. Optionally lerp the Cowsins pistol toward its aim pose using Bullseye's Aim action, without `CameraFOVManager`. Later, pick one FOV owner.

Cowsins `alternateAiming` (toggle ADS) already matches Bullseye's default `InputActivationMode.Toggle`.

---

## 9. Reload / ammunition

**Classification: C. Do not use** for the first slice (Bullseye has infinite functional shots today).  
**B. Adapt / bridge** later, using `Weapon_SO` magazine fields + `ReloadBehaviour` timing/animation, with ammo state decided by Bullseye (local vs server).  
**D.** Whether ammo is networked.

Cowsins:

- Mag/reserve live on `WeaponIdentification` (`magazineSize`, `bulletsLeftInMagazine`, `totalBullets`, `heatRatio`)
- `ReloadBehaviour` plays SFX, waits `reloadTime` / `emptyReloadTime`, refills mag
- `WeaponReloadState` is a Weapon FSM state
- Empty mag blocks `WeaponShootingState`
- `autoReload` lives on `WeaponControllerSettings`
- Overheat is a second reload style (not used by Pistol)

Bullseye `PlayerShoot` has no ammo. Fire is a button press with no magazine.

If ammo is added later:

- Do not let Cowsins locally refuse shots in a way that desyncs from server-confirmed hits
- Either keep shooting client-predicted and validate on server, or keep ammo local-only for the prototype
- Add a Reload action to `PlayerControls`; do not enable Cowsins `PlayerActions` for it

---

## 10. Weapon switching / inventory

**Classification: C. Do not use** for the current prototype (one pistol).  
**D.** Later, if multiple weapons are desired, `WeaponInventorySystem` is the Cowsins model to study — not drop in.

`WeaponInventorySystem`:

- Array sized by `inventorySize`
- Instantiates `Weapon_SO.weaponObject` under `weaponHolder`
- Mouse wheel / number keys
- Holster animator states
- Duplicate-weapon-adds-ammo
- Drop via `InteractManager`

It requires `IInteractEventsProvider` (drop, attachment pickup). That pulls in the pickup stack.

Bullseye should stay single-weapon until the pistol slice is proven. REQ-014 already says not to jump into multiple guns.

---

## 11. Camera system

**Classification: C. Do not use** Cowsins `CameraLookBehaviour`, `CameraEffects` (as the camera owner), or the full `CameraPivot` rig.  
**B. Adapt / bridge** overlay `WeaponCamera` + Weapons layer culling, if viewmodel clipping becomes a problem.  
**A.** Near-clip / weapon-layer idea is sound.

Cowsins camera stack:

- `CameraLookBehaviour` owns yaw/pitch/roll, sensitivity (optionally `GameSettingsManager`), aim-sensitivity multiplier, recoil, wallrun/slide tilt, aim assist
- `CameraEffects`: head bob, breathing, land shake, shoot trauma shake; requires Rigidbody and jump/land events
- `CameraFOVManager`: FOV state machine
- Dual cameras: world vs weapons overlay
- `MoveCamera` keeps the rig on `cameraHead`; `CameraAnimations` is a ParentConstraint target for the weapon head bone
- `CrouchTilt`, `JumpMotion` extras

Bullseye camera stack:

- `PlayerLook` yaw on body, pitch on camera
- `PlayerAimZoom` FOV
- `HDAdditionalCameraData` on the player camera
- Single camera, currently rendering all layers
- Crouch lowers camera local Y in `PlayerMovement`

**Keep Bullseye look.** Recoil/shake should be additive on `PlayerLook` / a small owner-only camera shake component.

HDRP note: a second overlay camera is possible in HDRP but is not a Built-in `Camera.clearFlags = Depth` copy-paste. That is a **D** spike before relying on Cowsins' WeaponCamera prefab.

---

## 12. Health / damage

**Classification: C. Do not use** Cowsins `PlayerStats`, `EnemyHealth`, `IDamageable` on players, fall damage, or shield.

Cowsins `PlayerStats`:

- Local `float health` / `shield`
- `Damage(float, bool isHeadshot)` immediately mutates local values
- Optional auto-heal, fall damage
- `Die()` freezes Rigidbody optionally and fires UnityEvents
- No NetworkVariables, no server RPC, no respawn countdown

Bullseye `PlayerHealth` (authoritative):

- `NetworkVariable<int> currentHealth` / `isDead` / `respawnAtServerTime` (server write)
- Bullseye zone damage (head 8, torso 4, lower body 2) via `BullseyeDamageZones`
- Regeneration after delay
- 3s respawn (REQ-013)
- Self-hit rejected in `HitServerRpc`

Cowsins enemies (`EnemyHealth`, `Turret`, `TrainingTarget`, `CircularTargetEnemy`) are single-player targets. They are unrelated to FFA bullseye combat.

Bullseye vulnerability must stay on `BullseyeMover` + `BullseyeTarget`. The bullseye child has no `NetworkTransform`; its world pose is a deterministic simulation from server seed and influence events. A Cowsins weapon that assumes static `Critical`/`BodyShot` colliders on a capsule will fight that mechanic.

**Do not put `IDamageable` on the Bullseye player** unless a bridge implementation forwards to `RegisterBullseyeHit` and ignores Cowsins' float health. Even then, Cowsins hitscan must not call it on self or on non-bullseye colliders as if they were damageable bodies.

---

## 13. UI

**Classification: C. Do not use** `UIController`, `PauseMenu`, `PlayerUI.prefab`, inspect/attachment UI, dash UI, XP/coin UI.  
**B.** Optional later: Cowsins hitmarker / ammo counters, rehosted.  
Keep `Reticle` and `PlayerHealthHud`.

Cowsins `UIController` is a large HUD: health/shield bars, interaction prompts, attachment inspect UI, ammo/magazine/heat, inventory slots, dash slots, XP, coins, crosshair. It initializes from `PlayerDependencies` and talks to `PlayerStats`.

Cowsins `Crosshair` / `CrosshairShape` are uGUI. Bullseye `Reticle` is IMGUI OnGUI for the owner, including hit marker.

Cowsins `PauseMenu` is a static `isPaused` that `PlayerControl` and look code consult. Bullseye has no pause menu; adding this static would freeze Cowsins-driven systems in a multiplayer session incorrectly.

`PlayerHealthHud` already shows health pips and respawn countdown for the owner.

**Do not import `PlayerUI.prefab` into the networked Player.** If ammo is added later, draw it next to `PlayerHealthHud` or add a small uGUI element; do not take the Cowsins HUD shell.

---

## 14. Audio / VFX

**Classification: A. Adopt** pistol fire/reload/holster clips and muzzle VFX **assets**.  
**B. Adapt / bridge** `SoundManager` and `PoolManager` (singletons).  
**D.** HDRP conversion of particle materials; whether shells/decals are worth it.

### Assets (pistol)

- `Assets/Cowsins/SFX/Weapons/Pistol/` — Fire, Reload, Reload_Empty, Holster, Unholster
- Muzzle VFX referenced from `Weapon_SO.muzzleVFX`
- Bullet trail, bullet graphics (shells), bullet-hole impacts from `Weapon_SO` / `WeaponControllerSettings.impactEffects`

### Runtime

`SoundManager` (`Scripts/Managers/SoundManager.cs`):

- Singleton
- 2D `PlaySound` via AudioSource on the manager
- 3D `PlaySoundAtPosition` via pooled AudioSource
- Lives on `CowsinsFPSController/GeneralManagers`

`PoolManager`:

- Singleton
- Required by hitscan trails, muzzle VFX, shells, impact decals
- `HitscanShootStyle` / `WeaponEffectsSystem` / `HitDetectionSystem` will NRE if `PoolManager.Instance` is null

`ProceduralShot`:

- Singleton
- Applies `ProceduralShot_SO` translation/rotation curves to the weapon
- Optional; `ShootBehaviour` calls it when `weapon.useProceduralShot`

**First slice:** a local `AudioSource` on the owner weapon plus Instantiate-and-destroy muzzle flash is enough. Bring `PoolManager`/`SoundManager` only when pooling is actually needed. If they are imported, they must be scene singletons, **not** parented under the networked Player prefab (would duplicate per spawn). `SoundManager.Awake` already `transform.SetParent(null)`.

---

## 15. Pickups

**Classification: C. Do not use.**

Scripts under `Scripts/PickUpSystem/`:

- `Interactable`, `Pickeable`, `WeaponPickeable`, `BulletsPickeable`, `AttachmentPickeable`
- `InteractManager` — camera raycast to `Interactable` layer, hold-to-interact, drop weapon, inspect

Extras: `PowerUp`, `Healthpack`, `Coin`, `Lootbox`, `Experience`, `JumpPad`, `HurtTrigger`, `DoorInteractable`, `PointCapture`.

`PlayerDependencies` **requires** `IInteractManagerProvider` even if pickups are unused. That is a major reason WeaponController cannot be copied alone.

Bullseye has no pickup loop. Players spawn with the pistol. Do not add Cowsins world pickups to the FFA prototype.

If `WeaponController` is ever hosted behind adapters, `InteractManager` still has to exist as a no-op implementation of the two interact interfaces.

---

## 16. Animation dependencies

**Classification: A. Adopt** `NewPistolAnimatorController` and the pistol clips (shoot, reload, holster, unholster, inspect).  
**B.** Drive those states from Bullseye fire (and later reload) rather than `WeaponAnimator`'s full locomotion graph.  
**D.** Cowsins FPS arms (`FPSEngineRig`) and the pistol `ParentConstraint`.

Cowsins animation pieces:

| Asset / script | Role |
|---|---|
| `Animations/Weapons/NewWeapons/NewPistolAnimatorController.controller` | Pistol states: `shooting`, `shootingN`, `reloading`, `emptyReloading`, `holster`, `unholster`, `inspect` |
| `Resources/BlankWeaponAnimatorTemplate.controller` | Template for new weapons |
| `WeaponAnimator` | Player-level. Sets animator `speed` from movement, plays unholster/reload/inspect, hides weapon while climbing |
| `WeaponIdentification.GetCurrentShotAnimation()` | `shooting` or `shooting2+` |
| `CowsinsUtilities.ForcePlayAnim` | `animator.Play(state, 0, 0)` on fire |
| `Prefabs/Arms/FPSEngineRig.prefab` | First-person arms |
| `Pistol_WeaponObject` `ParentConstraint` on child `Weapon` | Constrains the gun to the arms/head bone |

`WeaponAnimator` requires `PlayerDependencies`, `WeaponStates`, movement events (climb hide), and `InteractManager` (inspect). It is another reason the Cowsins player is a cluster.

Pistol fire animation **can** be triggered without `WeaponAnimator`: get the child `Animator` and `Play("shooting")` on Bullseye fire.

**Arms / ParentConstraint:** the stock pistol is built to follow Cowsins' FPS rig. Parenting the whole `Pistol_WeaponObject` under Bullseye `WeaponMount` may place the mesh incorrectly if the constraint still targets a missing arms bone. **Investigate in-editor** before assuming the prefab looks right under `WeaponMount`. Mitigation: disable `ParentConstraint`, or use the inner mesh under a Bullseye offset transform (same tactic REQ-014 used for the Ruger).

Locomotion animator parameters (`speed` idle/walk/run) are optional polish, not needed for the first slice.

---

## Project comparison (packages, settings, layers, assemblies)

### Packages / `manifest.json`

| Package | Cowsins | Bullseye | Integration note |
|---|---|---|---|
| `com.unity.inputsystem` | 1.19.0 | 1.19.0 | Compatible. **A** already present. |
| `com.unity.ugui` | 2.0.0 | 2.0.0 | TMP comes with ugui 2. Cowsins UI uses TMPro. |
| `com.unity.multiplayer.center` | 1.0.1 | 1.0.1 | Irrelevant to Cowsins gameplay. |
| `com.unity.netcode.gameobjects` | **absent** | 2.13.1 | **Keep.** Cowsins does not provide a replacement. |
| `com.unity.render-pipelines.high-definition` | **absent** | 17.4.0 | **Keep HDRP.** Do not switch Bullseye to Built-in to make Cowsins materials happy. |
| `com.unity.multiplayer.playmode` | absent | 2.0.2 | Keep for local two-client testing. |
| `com.unity.ai.assistant` / collab / timeline / visualscripting | absent | present | Unrelated. |

Cowsins `manifest.json` is Built-in-RP-minimal. There is **no HDRP package** in the FPS Engine project.

Cowsins editor (`Unity6WindowEditor`, Cowsins Manager) advertises Built-in / URP / HDRP conversion videos. Conversion is expected to happen **in the destination project**, not by copying `GraphicsSettings`.

### Important ProjectSettings

| Setting | Cowsins | Bullseye | Note |
|---|---|---|---|
| Editor version | 6000.4.4f1 | 6000.4.4f1 | Match. |
| Color space | (Built-in, `LightsUseLinearIntensity: 0`) | Linear (`m_ActiveColorSpace: 1`, HDRP) | HDRP/Linear is authoritative. |
| `activeInputHandler` | 1 | 1 | Input System. Compatible. |
| Time / fixed timestep | 0.02 | 0.02 | Match. |
| Custom RP asset | none | HDRP assets per quality level | **C** do not copy Cowsins `GraphicsSettings` or `QualitySettings`. |

### Tags / Layers

Cowsins `TagLayerInitializationManager` (editor, `[InitializeOnLoad]`) **auto-adds** tags and layers when the Cowsins scripts compile in a project.

Tags: `Enemy`, `FirePoint`, `Critical`, `Weapons`, `Ramp`, `BodyShot`  
Layers: `Ground`, `Weapons`, `Enemy`, `Object`, `Interactable`, `Grass`, `Metal`, `Mud`, `Wood`, `UITop`, `PostProcessing`, `Effects`, `Player`, `Ladder`

Bullseye today:

- Tags: none
- User layer 6: `FirstPersonWeapon`
- Layer 3 empty

**Do not let the initializer run unconstrained.** If Cowsins editor scripts are imported:

- `Critical` / `BodyShot` / `Enemy` tags would start meaning Cowsins damage, which we must not use on players
- A new `Weapons` layer would be added **in addition to** `FirstPersonWeapon` (names differ), likely at the next empty slot (7)
- `Ground` would occupy Bullseye's empty layer 3

**Preferred mapping:** keep `FirstPersonWeapon` as the viewmodel layer. If Cowsins prefabs are hardcoded to layer 6 by index, they already land on `FirstPersonWeapon` in Bullseye (also index 6). That is convenient **until** the initializer inserts layers before it.

`Pistol_WeaponObject` uses `m_Layer: 6`. In Cowsins that is `Weapons`. In Bullseye that is already `FirstPersonWeapon`. **If we copy the prefab without running the tag initializer, layer 6 still means viewmodel.** That is the safest first-slice path.

### Input System requirements

- Both projects: new Input System only (`activeInputHandler: 1`)
- Cowsins generated `PlayerActions` class name may **collide** if both generated C# wrappers live in `Assembly-CSharp` — they are different types (`PlayerActions` vs `PlayerControls`), so no type clash, but **enabling both maps** would duplicate Fire/Aim/Move
- Cowsins rebinding writes `PlayerPrefs`; irrelevant unless Pause/rebind UI is imported

### Physics / collision layers

| | Cowsins | Bullseye |
|---|---|---|
| Gravity | `(0, -9.8, 0)` | `(0, -9.81, 0)` |
| Default contact offset | 0.001 | 0.01 |
| Reuse collision callbacks | 1 | 0 |
| Default max angular speed | 7 | 50 |
| Layer collision matrix | all vs all | all vs all |
| Default physics material | assigned | none |

**C. Do not copy `DynamicsManager.asset`.** Differences are not required for a viewmodel. If Weapons/FirstPersonWeapon colliders exist, Bullseye already disables them in `FirstPersonWeaponView.DisableGameplayCollision`. Keep that.

Cowsins hitscan uses `WeaponControllerSettings.hitLayer`. If that mask is imported with baked layer indices, it must be reassigned in Bullseye after any layer changes.

### Rendering / HDRP

Cowsins materials sampled:

- Most weapon/env materials: Built-in Standard (`shader fileID: 46`)
- Particles: `211`, `210`, `203` (Built-in particle/additive)
- Some custom shaders (grid, UI blur)

Bullseye weapons/body already use HDRP (bullseye `_EmissiveColor`). Importing Standard-shader materials into HDRP will magenta/pink until converted (HDRP wizard or manual Lit conversion).

Cowsins VFX (muzzle flash, trails, shells) need an HDRP particle path. **D: convert one pistol material + one muzzle VFX first**, before importing the whole `Materials/` tree.

Do **not** add HDRP to the Cowsins project to “make them match.” Convert copies inside Bullseye.

### Assembly definitions

Neither project has gameplay `.asmdef` files under `Assets/`. Everything compiles into `Assembly-CSharp` / `Assembly-CSharp-Editor`.

Cowsins scripts are `namespace cowsins`. Bullseye scripts are global namespace. **No asmdef work is required** for a copy. If we later isolate Cowsins behind an asmdef, it would still reference `Unity.InputSystem` and `Unity.TextMeshPro`, and must **not** reference NGO if we keep it presentation-only.

### Resources dependencies

`Assets/Cowsins/Resources/`:

| Asset | Runtime vs editor |
|---|---|
| `CustomEditor/**` | Editor inspector art. `Resources.Load` from `WeaponControllerEditor`, `Weapon_SOEditor`, Cowsins Manager, etc. |
| `InputManager.prefab` | Runtime prefab in Resources |
| `Hitmarker.prefab` | Runtime |
| `InventoryUISlot.prefab` | Runtime UI |
| `PointCaptureUI.prefab` | Loaded by `PointCapture.cs` |
| `CompassElement.prefab` | Compass extra |
| `BlankWeaponAnimatorTemplate.controller` | Weapon creator |
| `CowsinsWeaponRegistry.asset` | Editor/runtime registry for weapon creation |

Runtime `Resources.Load` also appears in `PointCapture` and editor-only windows. **Do not copy the whole Resources folder** into Bullseye. Editor Resources can stay with editor scripts if those scripts are imported; `PointCaptureUI` is unused.

** collides:** Unity merges all `Resources/` folders. Copying `Resources/InputManager.prefab` is unnecessary if we are not using Cowsins input.

---

## Can Cowsins weapons be used without replacing the network player?

**Yes for assets and local presentation. No for `WeaponController` as currently written.**

### What works without replacing the player

- `Pistol_WeaponObject` (mesh + animator + fire points) parented under `WeaponMount`
- `FirstPersonWeaponView` owner/death gating
- Fire animation + muzzle + SFX triggered from existing `PlayerShoot` / Fire action
- Later: sway, recoil offsets in `PlayerLook`, ADS pose
- `Weapon_SO` as a data asset if scripts are imported

### What does **not** work without a large bridge

Hosting `WeaponController` requires, on the same GameObject (from actual `GetComponent` / interface resolution):

```text
PlayerDependencies
├── InputManager (serialized, static PlayerActions)
├── CameraFOVManager, CameraEffects, WeaponEffects
├── UIController, Crosshair, UIEffects
├── PlayerMovement (IPlayerMovement*)     ← Rigidbody mover
├── WeaponController (IWeapon*)           ← circular with Dependencies
├── PlayerStats (IDamageable, IPlayerStats*)
├── InteractManager (IInteract*)
├── PlayerControl
└── PlayerMultipliers

also expected nearby:
WeaponStates, WeaponAnimator, PlayerStates, AudioSource, Rigidbody,
scene singletons: PoolManager, SoundManager, (optional) ProceduralShot, PauseMenu, GameSettingsManager
```

Concrete-type couplings (not interface-only):

- `WeaponStates` → `GetComponent<WeaponController>()`, `GetComponent<InteractManager>()`
- `WeaponDefaultState` / `WeaponShootingState` / `WeaponReloadState` take `WeaponController`
- `ReloadBehaviour` casts `((WeaponController)weaponReference).AudioSource`
- `PlayerStates` → `GetComponent<PlayerMovement>()`
- Pickups → `GetComponent<PlayerDependencies>()`

**There is no supported Cowsins “weapons-only” subset.** Interfaces look modular; the FSM and several behaviours still hard-require Cowsins concrete types.

### Recommended shape

Keep one networked player (`Player.prefab`). Add a **local weapon presenter** that:

1. Instantiates or references the Cowsins pistol under `WeaponMount`
2. Listens to Bullseye fire (and later reload/aim)
3. Plays Cowsins animator/SFX/VFX
4. Never calls `IDamageable.Damage` on players
5. Never owns movement, look, health, or spawn

If a future ticket wants Cowsins `ShootBehaviour` for spread/recoil events, write Bullseye adapters for the **weapon-facing** interfaces and a no-op `InteractManager`, and **replace `HitDetectionSystem` with a Bullseye hit adapter**. That is a later phase, not the first slice.

---

## Dependency graph (do not copy one script without these)

### Cluster: cannot use WeaponController without these

```text
WeaponController
├── PlayerDependencies
├── WeaponStates + WeaponStateFactory + all Weapon*State classes
├── WeaponContext, WeaponControllerSettings, WeaponControllerEvents
├── ShootBehaviour, ReloadBehaviour, AimBehaviour, QuickActionBehaviour
├── RecoilSystem, SpreadSystem, HitDetectionSystem
├── WeaponInventorySystem, WeaponWeightSystem, AttachmentsSystem, WeaponEffectsSystem
├── IShootStyle + Hitscan/Projectile/Melee/Custom
├── WeaponIdentification, Weapon_SO, Item_SO
├── Attachment* (even if unused, types are referenced)
├── InputManager + PlayerActions
├── PlayerMovement interfaces (implemented by PlayerMovement)
├── PlayerStats / IDamageable
├── InteractManager + IInteract*
├── PlayerControl, PlayerMultipliers
├── PoolManager, SoundManager, CowsinsUtilities
├── WeaponAnimator (reload/unholster events)
└── PauseMenu.isPaused (PlayerControl.CheckIfCanGrantControl)
```

Copying **only** `WeaponController.cs` will not compile.

### Cluster: pistol presentation (first slice)

```text
Pistol_WeaponObject.prefab
├── WeaponIdentification.cs          (if scripts imported; else strip component)
├── Weapon_SO / Pistol.asset
├── NewPistolAnimatorController + clips
├── Pistol_FPSEngineRig.fbx (or embedded mesh)
├── Pistol SFX
├── muzzle VFX prefab + materials (HDRP-converted)
├── optional WeaponSpecificEffects.cs (sway)
└── ParentConstraint → FPSEngineRig   (D: may need disable)
```

This cluster can live under `WeaponMount` **without** `WeaponController` if we do not call `WeaponIdentification.Shoot()`.

### Cluster: fire feel after presentation works

```text
Animator.Play("shooting")
+ AudioSource.PlayOneShot(Pistol_Fire)
+ muzzle VFX at FirePoint
+ RecoilSystem or simplified curves → PlayerLook
+ WeaponSpecificEffects sway
```

Still no `HitDetectionSystem`.

### Cluster: never import for this prototype

```text
PlayerMovement + all Movement Behaviours
PlayerStats, Enemy*, Turret*
UIController, PlayerUI, PauseMenu, settings menus
InteractManager, *Pickeable, PowerUp, Coin, Experience, Lootbox
MainMenuManager, PointCapture, Compass
DeathRestart, JumpPad, GrapplingHook, WallRun, Dash (as player replacement)
TagLayerInitializationManager (or disable it)
```

---

## Subsystem classification table

| # | Subsystem | Class | Rationale |
|---|---|---|---|
| 1 | Player / controller (`CowsinsFPSController`, `PlayerMovement`, `PlayerStates`) | **C** | Replaces CharacterController + NGO player. Rigidbody mover. |
| 2 | Input (`InputManager`, static `PlayerActions`) | **C** as runtime; **B** as a map of needed actions | Breaks per-player devices / dual pad. |
| 3 | Viewmodel (`Pistol_WeaponObject`, WeaponCamera idea) | **A** assets; **B** host on Bullseye | Fits `FirstPersonWeaponView`. |
| 4 | `Weapon_SO` / attachments SOs | **A** for pistol config | Inspector-tunable; Inventory Pro unused. |
| 5 | Hitscan / projectile **damage** | **C** | Wrong target model; local `IDamageable`. |
| 5b | Hitscan **presentation** (trails, spread cosmetics) | **B** / **D** | Only after damage stays on `PlayerShoot`. |
| 6 | Recoil (`RecoilSystem`) | **B** | Apply in `PlayerLook`; do not take `CameraLookBehaviour`. |
| 7 | Sway (`WeaponSpecificEffects`) | **A** | Local viewmodel. |
| 7b | Bob (`WeaponEffects`) | **B** | Needs movement adapter; not Rigidbody. |
| 8 | ADS pose (`AimBehaviour`) | **B** | Keep `PlayerAimZoom` as FOV owner initially. |
| 8b | `CameraFOVManager` | **C** for first slice | Dual FOV writers. |
| 9 | Reload / ammo | **C** now; **B** later | No ammo in Bullseye yet. |
| 10 | Inventory / switching | **C** | One pistol. Pulls pickups. |
| 11 | Camera look / effects stack | **C** | Keep `PlayerLook`. Overlay camera is **D**. |
| 12 | Health (`PlayerStats`, enemies) | **C** | Bullseye NGO + bullseye zones win. |
| 13 | UI (`UIController`, Pause, Crosshair uGUI) | **C** | Keep `Reticle` + `PlayerHealthHud`. |
| 14 | Audio/VFX assets | **A** | Convert materials. |
| 14b | `SoundManager` / `PoolManager` | **B** | Scene singletons, not on Player prefab. |
| 15 | Pickups / interact / powerups | **C** | Out of prototype scope. |
| 16 | Pistol animator / clips | **A** | Drive from Bullseye fire. |
| 16b | FPS arms + ParentConstraint | **D** | Verify pose under `WeaponMount`. |
| — | Netcode / ownership / spawn / respawn | **C** (keep Bullseye) | Authoritative. |
| — | Bullseye vulnerability + mover | **C** (keep Bullseye) | Authoritative. |
| — | HDRP / GraphicsSettings from Cowsins | **C** | Stay on HDRP; convert assets. |
| — | Tags/layers initializer | **C** / **B** | Map to `FirstPersonWeapon`; do not auto-add damage tags. |
| — | asmdefs | n/a | None on either side. |
| — | Cowsins Resources/editor tools | **C** runtime; **D** editor | Weapon creator is optional later. |

---

## Phased integration plan (do not implement in this ticket)

Guiding constraint: **never replace the networked player to get a gun on screen.**

### Phase 0 — Import hygiene (still no gameplay change beyond asset copy)

1. Copy **only** the pistol presentation cluster into something like `Assets/ThirdParty/Cowsins/` (when implementation starts; not now).
2. Do **not** copy `Scripts/Movement`, `Scripts/Player/PlayerStats`, `Scripts/UI`, `Scripts/PickUpSystem`, `Scripts/Enemies`, `Scripts/Extra` except `CowsinsUtilities` if a later phase needs it.
3. Do **not** copy `TagLayerInitializationManager` until layer mapping is decided.
4. Convert pistol materials + muzzle VFX to HDRP Lit / HDRP particles.
5. Confirm `Pistol_WeaponObject` layer 6 equals Bullseye `FirstPersonWeapon`.
6. Confirm Asset Store license allows use in this prototype (standard Unity Asset Store terms usually do; still a human check).

**Exit:** HDRP pistol prefab visible in an empty scene, magenta-free, no Cowsins player in the game scene.

### Phase 1 — Smallest useful vertical slice (recommended next implementation)

**Goal:** owning client sees a Cowsins pistol and can fire it; kills still use Bullseye hitscan + bullseye + `PlayerHealth`.

1. Replace `Ruger22_FirstPerson` under `Camera/WeaponView/WeaponMount` with `Pistol_WeaponObject` (keep the mount; do not add a second gun).
2. Keep `FirstPersonWeaponView` owner/death rules; keep collider disable.
3. Disable or retarget `ParentConstraint` if the mesh floats incorrectly without `FPSEngineRig`.
4. On owner Fire (existing `PlayerShoot` path):
   - Play pistol `Animator` shoot state
   - Play fire SFX
   - Spawn muzzle VFX at `FirePoint`
5. Do **not** call `WeaponIdentification.Shoot()`, `HitscanShootStyle`, or `IDamageable.Damage`.
6. Leave `PlayerShoot` raycast, self-hit filter, `BullseyeTarget`, and `HitServerRpc` unchanged.

**Acceptance (playtest):**

- Two NGO clients: each sees only their own pistol
- Fire still registers bullseye hits and plays existing reticle hit marker + haptics
- Death hides the pistol; respawn shows it again
- Remote players do not get a first-person gun in their face
- No Cowsins health, pause, or input map enabled

**Human playtest still required:** scale, pose, muzzle alignment vs reticle, HDRP lighting on the gun.

### Phase 2 — Fire feel (recoil + sway)

1. Add owner-local recoil to `PlayerLook` using pistol `Weapon_SO` curves or a simplified kick.
2. Enable `WeaponSpecificEffects` sway, fed from Bullseye Look action (not `InputManager`).
3. Optional: `ProceduralShot` **or** a 10-line local kick on `WeaponMount`.

Still no Cowsins hitscan.

### Phase 3 — ADS pose

1. Drive pistol aim pose from Bullseye Aim action.
2. Keep `PlayerAimZoom` as the only FOV writer.
3. Do not add `CameraFOVManager` until Phase 3 is stable.

### Phase 4 — Overlay camera / clipping (only if Phase 1 clips)

Spike HDRP overlay/weapon camera vs tightening near clip and scale. **D** until needed.

### Phase 5 — Reload / ammo (gameplay; after the pistol feels like a gun)

1. Add Reload to `PlayerControls`.
2. Local mag from `Weapon_SO.magazineSize`; play Cowsins reload animator + SFX.
3. Decide networked ammo vs prototype-local. Default for this prototype: **local-only** unless exploits matter.
4. Empty mag should block **new** shots on the owner, but must not desync existing server damage rules.

### Phase 6 — Optional Cowsins shoot-module bridge (only if Phase 2–5 are insufficient)

If we still want Cowsins spread, penetration cosmetics, or shell pooling:

1. Implement no-op/adapter `PlayerDependencies` **weapon-facing** interfaces on a local child object — **not** by replacing `PlayerMovement` / `PlayerHealth`.
2. Replace `HitDetectionSystem` with a Bullseye adapter: `OnHit` → nearest `BullseyeTarget` + existing `TryRegisterHit`.
3. Keep server authority in `PlayerHealth.HitServerRpc`.
4. Feed `InputManager` from Bullseye actions or fork `ShootBehaviour` to take an `IWeaponInput` we own.

This phase is the first time `WeaponController` is in play. Budget it as a large spike, not a small ticket.

### Explicitly out of scope for this experiment branch

- Replacing NGO
- Replacing `PlayerMovement` / `PlayerLook`
- Cowsins dash, wallrun, grapple, stamina
- Cowsins health, shield, enemies, turrets
- Pickups, lootboxes, coins, XP, capture points
- Pause menu / settings / rebind UI
- Multi-weapon inventory
- Switching Bullseye to Built-in RP

---

## Implementation risks

| Risk | Severity | Mitigation |
|---|---|---|
| `WeaponController` looks modular via interfaces but FSM still needs concrete Cowsins types | High | Do not start from WeaponController. Start from the pistol prefab. |
| HDRP pink materials / broken particles | High | Convert the pistol + one VFX before importing catalogs. |
| `ParentConstraint` without arms | Medium | Disable constraint; offset under `WeaponMount`. |
| `TagLayerInitializationManager` inserts layers and shifts indices | High | Exclude that editor script; rely on existing layer 6. |
| Static `PlayerActions` + `Gamepad.current` vs dual-controller | High | Never enable Cowsins InputManager in play mode. |
| Dual FOV writers | Medium | One owner: `PlayerAimZoom`. |
| `PoolManager`/`SoundManager` singletons on Player prefab | Medium | Scene objects, or skip until needed. |
| Cowsins `IDamageable` accidentally on player | Critical | Never add `PlayerStats`. Never call `Damage` on players. |
| Treating Cowsins hitscan as a netcode upgrade | High | Shooter-side raycast is already client-trusted; keep `HitServerRpc` and bullseye-only hits. |
| Syncing the bullseye with NetworkTransform to “fit” Cowsins hitboxes | Critical | Keep deterministic `BullseyeMover`; do not replace it with a static Critical/BodyShot collider. |
| Copying too many scripts “so it compiles” | High | Compile a **presentation-only** subset; do not fix compile errors by importing movement/UI. |
| License / third-party attribution | D | Confirm Asset Store license before committing Cowsins sources into the Bullseye git repo. |

---

## What this audit did not verify

- In-Editor pose of `Pistol_WeaponObject` under Bullseye `WeaponMount` (Ruger pose is already tuned; Cowsins pistol is not)

---

## Files consulted (non-exhaustive)

**Cowsins (read-only)**

- `Packages/manifest.json`, `ProjectSettings/{ProjectVersion,GraphicsSettings,TagManager,DynamicsManager,QualitySettings,ProjectSettings}.asset`
- `Assets/Cowsins/Scripts/Player/PlayerDependencies.cs`, `PlayerControl.cs`, `PlayerStats.cs`, `PlayerMultipliers.cs`, `PlayerGraphics.cs`
- `Assets/Cowsins/Scripts/Movement/PlayerMovement.cs`, `IPlayerMovementProvider.cs`, `CameraLookBehaviour.cs`
- `Assets/Cowsins/Scripts/Weapons/WeaponController.cs`, `WeaponIdentification.cs`, `Weapon_SO.cs`, `WeaponContext.cs`, `WeaponControllerSettings.cs`, `WeaponControllerEvents.cs`, `WeaponAnimator.cs`, `IWeaponControllerProvider.cs`
- `Assets/Cowsins/Scripts/Weapons/ShootStyles/*`, `Bullet.cs`
- `Assets/Cowsins/Scripts/Behaviours/Weapon Behaviours/*`
- `Assets/Cowsins/Scripts/Managers/InputManager.cs`, `PoolManager.cs`, `SoundManager.cs`
- `Assets/Cowsins/Scripts/Effects/WeaponEffects.cs`, `WeaponSpecificEffects.cs`, `CameraEffects.cs`, `ProceduralShot.cs`
- `Assets/Cowsins/Scripts/Camera/CameraFOVManager.cs`
- `Assets/Cowsins/Scripts/UI/UIController.cs`, `Crosshair.cs`
- `Assets/Cowsins/Scripts/PickUpSystem/*`, `Enemies/IDamageable.cs`
- `Assets/Cowsins/Scripts/Editor/TagLayerInitializationManager.cs`
- `Assets/Cowsins/Prefabs/PlayerControllers/CowsinsFPSController.prefab`
- `Assets/Cowsins/Prefabs/Weapons/Pistol_WeaponObject.prefab`
- `Assets/Cowsins/ScriptableObjects/Weapons/Pistol.asset`
- `Assets/Cowsins/Inputs/PlayerActions.inputactions`

**Bullseye**

- `Packages/manifest.json`, matching ProjectSettings files
- `Assets/Prefabs/Player.prefab`
- `Assets/Input/{PlayerShoot,PlayerLook,PlayerMovement,PlayerHealth,PlayerAimZoom,FirstPersonWeaponView,LocalPlayerInputBinding,Reticle,BullseyeTarget,PlayerHealthHud,PlayerControls.inputactions}`
- `Assets/NetworkManager/PlayerNetworkSetup.cs`
- `docs/requirements/REQ-014-first-person-pistol-model-and-weapon-view.md`

---

## Report checklist (analysis ticket)

| Item | Status |
|---|---|
| Files changed | `docs/analysis/FPS_ENGINE_INTEGRATION_AUDIT.md` only |
| Unity objects / prefabs changed | None |
| Gameplay code / packages / ProjectSettings | Unchanged |
| Cowsins project | Unchanged (read-only) |
| Acceptance: architectural comparison + classifications A–D | Documented above |
| Acceptance: phased plan starting at one pistol + Bullseye multiplayer/damage | Phase 1 |
| Still requires human playtesting | All feel questions (pose, recoil, ADS, HDRP look) |
| Uncertainties | Arms constraint, HDRP overlay camera, license, exact animator clip list |
