````markdown
# REQ-069C — Integrate Dedicated First-Person Body and Arms

## Summary

Replace the temporary/procedural first-person body implementation from REQ-069 with the new dedicated Blender-created first-person assets.

The following assets now exist and have been verified visually in Unity:

```text
Assets/Player/SM_StickMan_FPBody.fbx
Assets/Player/SM_StickMan_FPArms.fbx
```

Use the actual matching filenames in `Assets/Player/` if Unity imported them with slightly different names.

These assets were created from the existing rigged StickMan character and retain the existing Mixamo-compatible skeleton.

The new architecture should be:

```text
WORLD PRESENTATION
└── Complete existing StickMan character
    ├── full body
    ├── world arms
    ├── world weapon
    └── existing third-person animations

FIRST-PERSON PRESENTATION
├── Dedicated FPBody
│   ├── pelvis
│   ├── hips
│   ├── legs
│   └── feet
│
├── Dedicated FPArms
│   ├── left arm
│   └── right arm
│
└── Existing first-person weapon
```

The new assets should replace the previous approach of procedurally deleting triangles from the world character.

---

# 1. First Inspect the Existing REQ-069 Implementation

Before changing anything, inspect the current implementation and identify:

- `FirstPersonBodyView`
- `LocalTorsoCap`
- runtime triangle filtering/deletion
- any first-person arm extraction from `SM_StickMan`
- local-owner rendering/culling logic
- first-person weapon hierarchy
- current first-person camera hierarchy
- existing Animator references
- world character renderer
- any code added specifically to hide individual world-body bones/renderers

Do not assume the previous REQ-069 implementation needs to be preserved.

The dedicated Blender assets now supersede the procedural mesh-cutting approach.

---

# 2. Preserve the Full World Character

The existing world-view StickMan remains the complete authoritative visual representation seen by other players.

Do NOT:

- replace the world mesh with `FPBody`,
- remove the world character's torso,
- remove world-view arms,
- modify the world character geometry,
- use the FP arms for remote players.

Remote players should continue seeing:

- full head,
- torso,
- arms,
- legs,
- world weapon,
- existing third-person animations.

REQ-069C is primarily a LOCAL PLAYER presentation change.

---

# 3. Remove the Old Runtime Mesh-Cutting Workaround

The following approach is no longer needed:

```text
Full StickMan mesh
→ inspect bone weights
→ remove triangles belonging to upper-body bones
→ generate partial lower body
→ add LocalTorsoCap
```

Remove or disable this behavior.

Specifically eliminate the need for:

- runtime triangle deletion,
- generated first-person lower-body mesh copies,
- `LocalTorsoCap`,
- procedural shoulder/arm extraction,
- hacks that hide geometry because the original body was being sliced.

Do not leave both architectures running simultaneously.

If obsolete scripts are still referenced elsewhere and cannot safely be deleted immediately, clearly mark/deactivate the obsolete pathway and ensure only the new dedicated assets are used during gameplay.

---

# 4. Integrate `SM_StickMan_FPBody`

Instantiate/use the dedicated FPBody asset for the owning/local player.

The FPBody should:

- only be visible to the owning player's first-person camera,
- not replace the complete world character,
- use the existing locomotion/gameplay state,
- animate consistently with the world character,
- remain local presentation whenever networking is unnecessary.

The FPBody should visually represent:

- pelvis,
- hips,
- thighs,
- legs,
- feet.

There should be no runtime mesh cutting.

---

# 5. FPBody Animation / Rig Sharing

The new FPBody came from the same original rigged character.

Determine the cleanest way to have it follow the same locomotion as the world character.

Prefer a reusable setup where the first-person lower body reflects the same states as the world body:

```text
Idle
Walking
Sprinting
Jumping
Falling
Landing
Crouching
Prone
Dolphin dive / body slam where applicable
Ladder climbing when implemented
```

Do NOT create an entirely independent locomotion state machine if that can be avoided.

The objective is:

> The legs I see in first person should approximately perform the same locomotion that another player sees my character performing.

If the existing Animator/avatar can be reused safely, prefer that.

If animation synchronization requires a separate Animator using the same parameters/state, implement it cleanly and document the approach.

---

# 6. First-Person Lower-Body Positioning

Do not assume the FPBody should occupy exactly the same visual position as the world mesh.

The first-person lower body is a presentation asset.

Add configurable local positioning so we can tune the Halo-like body-awareness effect.

Expose values equivalent to:

```text
FPBodyPositionOffset
FPBodyRotationOffset
FPBodyScaleOffset
```

Scale should normally remain 1 and should only exist if genuinely useful.

The important values are likely position offsets.

We expect the FPBody may need to sit:

- slightly behind the first-person camera,
- slightly below the first-person camera,

so the player sees natural legs when looking downward without staring directly into the top of the pelvis.

Do NOT permanently modify the FBX geometry for this tuning.

---

# 7. Desired FPBody Visibility

The target is approximately:

### Looking forward

Player should generally see no lower body.

### Looking somewhat downward

Player begins seeing:

- upper legs/thighs,
- perhaps a small amount of pelvis.

### Looking steeply downward

Player sees:

- thighs,
- knees,
- lower legs,
- feet.

The player should NOT normally see:

- the hidden top termination of the FPBody,
- an obvious waist cap,
- a giant pelvis surface directly underneath the camera,
- an open torso,
- the inside of the character.

The positioning values should be easy to adjust in the Inspector so we can tune this visually.

Do not attempt to solve poor positioning by modifying mesh topology in code.

---

# 8. Integrate `SM_StickMan_FPArms`

Use the dedicated FPArms asset for the local first-person weapon presentation.

These arms are NOT world-character arms.

They should belong to the first-person presentation system.

Conceptually:

```text
FirstPersonCamera
└── FirstPersonPresentation
    ├── FPArms
    └── FPWeapon
```

The exact hierarchy may differ based on the existing project architecture.

The critical design principle is:

> FPArms and FPWeapon are camera-relative first-person presentation assets.

They should NOT be driven by the physical position of the world character's shoulder bones.

---

# 9. First-Person Arms Must Hold the Weapon

The previous implementation allowed arms to hang at the player's sides.

That is not acceptable.

The new FPArms must visually hold the currently equipped first-person weapon.

Inspect the existing weapon architecture for current or proposed grip transforms such as:

```text
RightHandGrip
LeftHandGrip
```

If appropriate grip transforms do not exist, establish a reusable system for them.

For long guns:

- dominant hand should remain near the pistol grip/trigger,
- support hand should remain near the forward grip/handguard.

For pistols:

- use an appropriate first-person pistol grip.

For heavy weapons:

- support a separate heavy weapon placement.

Exact polish is not required in 069C, but the hands cannot remain at the character's sides.

---

# 10. Prefer IK / Grip Targets Over Per-Weapon Hand Animation

Do not create bespoke hand animation clips for every weapon unless unavoidable.

Prefer:

- configurable weapon grip targets,
- IK,
- reusable procedural hand placement,
- common first-person weapon pose categories.

The system should ultimately support:

```text
ShortGun
LongGun
HeavyGun
```

or the equivalent weapon categories already in the project.

Individual weapons should be adjustable without rewriting the first-person arm system.

---

# 11. FPArms Positioning

Expose configurable first-person arm positioning values.

For example:

```text
FPArmsPositionOffset
FPArmsRotationOffset
```

The shoulder ends of the dedicated FP arms should remain outside the normal first-person field of view.

Do not try to visually connect the FP arms to the FPBody's shoulders.

There are intentionally no first-person shoulders connecting these systems.

The illusion should come from:

- camera framing,
- weapon placement,
- arm placement.

---

# 12. Existing First-Person Weapon Must Remain the Visual Anchor

Do not reposition the first-person weapon merely to make the arms easier to integrate if that would break the current aiming feel.

The first-person weapon presentation should remain the anchor.

The arms should be positioned/posed to match the weapon.

Conceptually:

```text
Weapon position
       ↓
RightHandGrip / LeftHandGrip
       ↓
FP hands follow grips
       ↓
FP arms follow hands
```

Rather than:

```text
World arms
       ↓
force weapon into wherever those arms happen to be
```

---

# 13. Reload Integration

Preserve the simplified REQ-069 reload design.

Reloading should remain:

```text
arms + weapon
      ↓
lower together off-screen
      ↓
reload timer
      ↓
raise together
      ↓
ready
```

Do not create detailed magazine manipulation animations.

FPArms must move with the weapon during the reload-lowering sequence.

Do not leave arms floating in place while the weapon disappears.

---

# 14. Sprint Integration

When the player begins sprinting:

- world character continues using normal third-person sprint animation,
- FPBody legs reflect sprint locomotion,
- FPArms + FPWeapon enter an appropriate first-person sprint posture.

Do not make FP arms follow the world character's exact sprint-arm transforms.

The two representations share gameplay state but have different presentation requirements.

---

# 15. Jump / Fall / Land Integration

FPBody should continue following locomotion so that:

- jumping visibly affects the legs,
- falling remains coherent,
- landing returns correctly.

FPArms/weapon may use subtle procedural movement if already supported.

Do not overbuild this in 069C.

The primary objective is architecture and functional presentation.

---

# 16. Crouch and Prone

FPBody must remain compatible with:

```text
Crouch
Prone
```

The first-person legs should not remain visually standing while the gameplay character is crouched or prone.

FPArms/weapon should continue functioning in these states.

If prone currently creates unavoidable first-person body clipping, prioritize functional behavior and expose the issue clearly rather than introducing new mesh-hiding hacks.

---

# 17. ADS / Zoom

Existing ADS/zoom behavior must remain functional.

During ADS:

- FP weapon retains correct aiming alignment,
- FP arms continue holding the weapon,
- FPBody should not obstruct the sight picture.

Preserve existing sniper behavior established in REQ-057.

Do not allow FPBody integration to break sniper zoom or movement restrictions.

---

# 18. Rendering / Camera Layers

Use clean local-player rendering logic.

The first-person camera should render:

```text
FPBody
FPArms
FPWeapon
```

as appropriate.

The full world character should remain available for third-person/remote rendering.

Do not create a situation where remote clients see:

- FPBody,
- FPArms,
- duplicated arms,
- duplicate lower bodies.

Likewise, the owner should not see both:

```text
world legs
+
FPBody legs
```

overlapping.

There must be only one intended lower-body representation in the owning player's first-person view.

---

# 19. Multiplayer

Do not unnecessarily network:

- FPBody local offset,
- FPArms local transform,
- first-person IK,
- camera-relative arm motion,
- local weapon bob,
- local reload interpolation.

These are presentation details.

Continue networking the gameplay states required by the existing game:

```text
Equipped weapon
Reload state
Sprint state
Crouch/prone state
Jump state
Aim state where needed
Weapon firing
```

Remote players should continue using the full world representation.

---

# 20. Do Not Break Local Multiplayer Testing

Preserve compatibility with:

- normal single-player/editor testing,
- local host/client testing,
- Multiplayer Play Mode,
- Relay/public/private multiplayer added in earlier requirements.

REQ-069C should not redesign networking.

If Multiplayer Play Mode currently has a separate Player 2 launch issue, do not attempt to fix that inside this ticket unless the new first-person implementation directly causes an identifiable error.

---

# 21. Inspector Tuning

Expose useful tuning fields so we can adjust the result without code changes.

At minimum consider:

```text
FPBody Position Offset
FPBody Rotation Offset

FPArms Position Offset
FPArms Rotation Offset

Left Hand Grip Offset
Right Hand Grip Offset
```

Only expose values that are actually useful.

Do not create an excessive configuration system.

The primary objective is that we can visually tune the body and arms without editing source code.

---

# 22. Implementation Order

Implement this incrementally.

## Phase 1 — Remove Old Mesh Cutting

- Identify old procedural first-person body code.
- Disable/remove runtime triangle filtering.
- Remove `LocalTorsoCap`.
- Preserve full world character.

## Phase 2 — FPBody

- Add dedicated FPBody asset.
- Connect it to locomotion.
- Make it owner-only.
- Add configurable positioning.
- Verify walking/sprinting/jumping/crouch/prone.

## Phase 3 — FPArms

- Add dedicated FPArms asset.
- Parent/configure it as part of first-person presentation.
- Ensure shoulder ends remain outside the camera view.
- Establish weapon grip relationship.

## Phase 4 — Weapon Integration

- Align hands with one existing test weapon first.
- Confirm hip-fire.
- Confirm ADS.
- Confirm sprint.
- Confirm reload lowering.

Once ONE weapon works correctly, generalize the system to the remaining weapon categories.

Do NOT try to manually tune every weapon before proving the architecture with one test weapon.

---

# 23. Initial Test Weapon

Use one representative weapon to establish the system before applying it everywhere.

Prefer an existing normal long gun that already has reliable first-person behavior.

The test should prove:

```text
FP arms visible
+
hands on weapon
+
weapon fires
+
ADS works
+
sprint works
+
reload lowering works
```

Once that architecture is stable, extend the grip configuration to the other weapons.

---

# 24. Acceptance Criteria

REQ-069C is complete when:

- [ ] Existing complete world StickMan remains intact.
- [ ] Remote players still see the full character.
- [ ] Old runtime triangle-cutting FPBody system is no longer active.
- [ ] `LocalTorsoCap` workaround is no longer required.
- [ ] Dedicated `SM_StickMan_FPBody` asset is used.
- [ ] Dedicated `SM_StickMan_FPArms` asset is used.
- [ ] Owner sees only one intended first-person lower body.
- [ ] Owner does not see world-view arms overlapping FP arms.
- [ ] FPBody follows locomotion appropriately.
- [ ] Walking looks coherent.
- [ ] Sprinting looks coherent.
- [ ] Jump/fall/landing do not visibly break FPBody.
- [ ] Crouch works.
- [ ] Prone remains functional.
- [ ] FPBody position can be tuned in Inspector.
- [ ] Looking forward does not prominently show the legs.
- [ ] Looking downward reveals legs/feet naturally.
- [ ] Player does not normally see the FPBody's upper termination.
- [ ] Dedicated FP arms appear in first person.
- [ ] Shoulder termination of FP arms is not normally visible.
- [ ] FP arms visually hold the equipped weapon.
- [ ] Hands do not hang at the player's sides.
- [ ] At least one representative weapon has correctly configured hand placement.
- [ ] ADS continues working.
- [ ] Sprint weapon presentation continues working.
- [ ] Reload lowers FP arms and weapon together.
- [ ] Existing firing logic is unaffected.
- [ ] World-view weapon presentation remains functional.
- [ ] FP-only transforms are not unnecessarily networked.
- [ ] No duplicate bodies/arms appear on remote clients.

---

# 25. Non-Goals

Do NOT use 069C to attempt:

- final-quality hand/finger animation,
- bespoke reload animations,
- perfect recoil animation,
- final weapon-by-weapon pose polish,
- world-view animation overhaul,
- new locomotion animations,
- re-rigging the StickMan,
- modifying the Blender assets procedurally,
- networking camera-relative first-person presentation,
- solving the unrelated Multiplayer Play Mode Player 2 launch bug.

Those can be handled separately.

---

# Design Principle

The new architecture should be:

> **Full world character for everyone else. Dedicated animated lower body for first-person body awareness. Dedicated camera-relative arms for first-person weapon interaction.**

The FPBody and FPArms assets now exist specifically so we no longer need to force one world mesh to perform all three jobs.

Prioritize a clean, maintainable architecture over another visual workaround.
````
