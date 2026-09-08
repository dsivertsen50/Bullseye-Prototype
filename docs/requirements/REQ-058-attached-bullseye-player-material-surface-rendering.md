````markdown
# REQ-058 — Attached Bullseye Player-Material Surface Rendering

## Summary

Replace the current HDRP `DecalProjector`-based attached bullseye visual with a **true player-mesh/material-based bullseye rendering system**.

REQ-054 successfully changed the bullseye from a physical object crawling around a cylinder into an authoritative gameplay position on the animated player's body surface.

However, the attached visual remains unreliable.

The current `DecalProjector` implementation:

- disappears when the body surface leaves the projection volume;
- frequently displays only part of the circular bullseye;
- paints nearby parts of the same character mesh, especially the arm when the bullseye is near the hip;
- cannot properly wrap a large bullseye around thin body parts;
- requires conflicting projection-depth settings;
- fundamentally behaves like a box projection rather than a mark attached to the player's skin.

REQ-058 should **stop using an HDRP DecalProjector as the attached bullseye rendering solution**.

Instead, render the attached bullseye as part of the **actual HDRP character material/shader**, using the existing Bullseye surface-position system to tell the shader where and how to draw the target.

The result should visually resemble a bullseye painted directly onto the player's skin/clothing.

---

# Parent Requirement

This ticket is a corrective follow-up to:

```text
REQ-054 — Player-Mesh Bullseye Surface System
```

Preserve the working REQ-054 architecture unless a change is specifically required for rendering.

Do NOT restart the Bullseye system from scratch.

---

# Current Working Systems — Preserve These

The following are largely working and should NOT be rewritten as part of REQ-058:

- `BullseyeMover`
- `BullseyeSurfaceMap`
- `BullseyeSurfaceRegion`
- server-authoritative bullseye movement
- 16-region body surface graph
- bone-local surface anchors
- front/back surface interpolation
- cylindrical/orbit interpolation around torso and limbs
- player influence on Bullseye movement
- jump influence
- `AttachedHitTarget`
- Bullseye damage behavior
- Bullseye detachment
- Combustion Grenade interaction
- Magnetism Grenade interaction
- physical detached Bullseye
- Bullseye return behavior
- networking
- respawn behavior

The problem being solved by REQ-058 is primarily:

```text
ATTACHED BULLSEYE VISUALIZATION
```

not Bullseye gameplay.

---

# Problem With Current DecalProjector

The current attached visual uses:

```text
Player
└── BullseyeSystem
    └── AttachedVisual
        └── HDRP DecalProjector
```

This is no longer the desired architecture.

A `DecalProjector` projects through a volume.

That creates unavoidable problems for this mechanic.

Example:

```text
Bullseye at hip

          ARM
           |
       ◎   |
      HIP  |

Projector volume can intersect both HIP and ARM.
```

Because the torso and arm are part of the same `SkinnedMeshRenderer`, receiver-layer filtering cannot reliably distinguish:

```text
Paint hip
Do not paint arm
```

within that single renderer.

Projection depth also creates a fundamental conflict:

```text
Shallow projector
→ body curvature leaves projection box
→ bullseye disappears / loses pieces

Deep projector
→ reaches more curved geometry
→ paints opposite side / neighboring geometry
```

Do not attempt to solve REQ-058 by continuing to tune projector depth, angle fade, pivot, or orientation.

---

# Required Architectural Direction

The attached Bullseye should be rendered **inside the player's actual character shader/material path**.

Conceptually:

```text
Player SkinnedMeshRenderer
        ↓
Existing HDRP Character Material
        ↓
Normal Character Base Color
        +
Bullseye Surface Stamp
        ↓
Final Character Appearance
```

The bullseye should be composited onto the player's Base Color rather than rendered as a separate physical object hovering above the surface.

---

# Desired Visual Model

While attached:

```text
Animated Character Mesh
        ↓
Character Material
        ↓
Bullseye position supplied to shader
        ↓
Shader determines which character pixels belong to Bullseye
        ↓
Bullseye texture blended into Base Color
```

There should be:

- no projector box;
- no hovering disc;
- no sleeve object;
- no duplicate visual mesh required merely to display the Bullseye;
- no z-fighting between the Bullseye and character;
- no Bullseye object capable of physically penetrating the player mesh.

The physical Bullseye mesh remains necessary ONLY for:

```text
Detached
Returning
```

states.

---

# Preferred Rendering Technology

Use a **proper HDRP-compatible Shader Graph / HDRP Lit shader path**.

Strong preference:

```text
HDRP Lit Shader Graph
```

or another HDRP-supported character shader implementation.

Do NOT repeat the failed experiment of creating a minimal custom Unlit shader that Unity HDRP does not reliably render on the `SkinnedMeshRenderer`.

The existing appearance of the player must be preserved.

The new shader must retain appropriate existing:

- Base Color / Albedo
- Normal Map
- Mask Map
- Roughness / Smoothness
- Metallic
- Alpha behavior
- lighting
- shadows
- HDRP compatibility

The Bullseye should be an additional layer in the player's material, not a replacement for the player's normal appearance.

---

# Bullseye Texture

Use the existing bullseye texture if appropriate:

```text
Assets/Player/BullseyeSurfaceDecal.png
```

or create a clean replacement texture if necessary.

The texture should contain:

- transparent exterior;
- circular red/white target;
- clean alpha;
- no unwanted border artifacts.

Do NOT regenerate the existing texture through a known broken alpha-generation path.

---

# Bullseye Shader Parameters

Each player must be capable of having a unique Bullseye location.

The character shader should receive Bullseye state from `BullseyeSurfaceVisual` or a replacement visual controller.

Likely parameters include concepts such as:

```text
_BullseyeEnabled

_BullseyeRadius

_BullseyeCenterWS

_BullseyeTangentWS

_BullseyeBitangentWS

_BullseyeNormalWS

_BullseyeWrapAxisWS

_BullseyeWrapRadius

_BullseyeCurrentRegion

_BullseyeTargetRegion

_BullseyeTravelProgress

_BullseyeTexture
```

Exact property names are implementation-specific.

Do not hard-code global values that force every player to show the Bullseye at the same location.

---

# MaterialPropertyBlock

Strongly prefer supplying per-player Bullseye shader values using:

```csharp
MaterialPropertyBlock
```

or an equivalent instance-safe technique.

Do NOT instantiate a completely new character Material every frame.

Do NOT modify the shared character Material in a way that causes all players to inherit one player's Bullseye location.

Example intended architecture:

```text
Shared Character Material
           ↓
Player A MaterialPropertyBlock
    Bullseye at Chest

Player B MaterialPropertyBlock
    Bullseye at Arm

Player C MaterialPropertyBlock
    Bullseye Detached / Disabled
```

All players must be independently renderable.

---

# Surface Region Masking

This is CRITICAL.

World-space distance alone is not sufficient.

The shader must understand which portion of the character body a fragment belongs to.

Otherwise:

```text
Bullseye centered on hip
+
Arm happens to be nearby in world space
=
Bullseye appears on arm
```

This must NOT happen.

Create a way for the character shader to identify body surface regions corresponding to the REQ-054 Bullseye graph.

For example:

```text
Head
Neck

UpperChest
LowerChest
UpperBack
LowerBack

LeftShoulder
RightShoulder

LeftUpperArm
RightUpperArm

LeftForearm
RightForearm

LeftThigh
RightThigh

LeftLowerLeg
RightLowerLeg
```

The implementation may use:

- a UV-space body-region mask texture;
- vertex colors;
- an unused UV channel;
- bone-weight-derived region metadata;
- another reliable mesh attribute.

Choose the approach that works best with the actual Mixamo mesh.

---

# Region Map Generation

If practical, create an EDITOR tool that generates the initial region assignment from existing Mixamo bone weights.

Example concept:

```text
Vertex strongly influenced by LeftArm bone
→ LeftUpperArm region

Vertex strongly influenced by Spine / Chest
→ Chest region

Vertex strongly influenced by Head
→ Head region
```

The generated map may be manually adjusted if necessary.

The mapping should NOT require manually assigning every individual vertex.

---

# Region Boundaries

The body-region system does not need pixel-perfect anatomical segmentation.

Its purpose is to prevent obvious visual leakage.

For example:

```text
Hip Bullseye
```

may legitimately distort around the side of the lower torso.

It must NOT suddenly appear on:

```text
hanging forearm
```

just because the forearm happens to occupy similar world coordinates.

Likewise:

```text
Front torso Bullseye
```

must not simultaneously paint:

```text
back torso
```

unless the Bullseye is intentionally moving around the side between those regions.

---

# Region Transition Rendering

The Bullseye moves continuously between connected regions.

Do not make it visibly teleport at region boundaries.

During a transition such as:

```text
Chest
   ↓
Shoulder
   ↓
Upper Arm
```

the shader may temporarily allow both relevant connected regions to participate.

For example:

```text
CurrentRegion
TargetRegion
TravelProgress
```

could be used to determine valid rendering surfaces.

Exact implementation is up to Cursor.

The goal is:

```text
continuous surface motion
```

without:

```text
unrelated surface leakage.
```

---

# Surface Coordinate Calculation

The Bullseye must be evaluated in **surface coordinates**, not merely raw 3D Euclidean distance.

Different parts of the body require different surface approximations.

Two primary modes should be supported.

---

# Mode A — Tangent Surface Stamp

Use for relatively broad/flatter regions such as:

- chest;
- back;
- abdomen;
- large areas of thigh;
- other broad surfaces where appropriate.

For each rendered character fragment:

```text
delta = fragmentWorldPosition - BullseyeCenter
```

Calculate coordinates along the Bullseye surface frame:

```text
u = dot(delta, tangent)
v = dot(delta, bitangent)
```

Then:

```text
surfaceDistance = sqrt(u² + v²)
```

Use `u/v` as the Bullseye texture coordinates.

Conceptually:

```text
          +v
           ↑
           |
      -----◎----- → +u
           |
```

The result should be a circular Bullseye in local surface space.

---

# Mode B — Cylindrical Surface Wrap

Use for thin / approximately cylindrical body parts such as:

- upper arms;
- forearms;
- neck;
- lower legs;
- potentially portions of thighs;
- other appropriate regions.

The existing Bullseye surface system already contains useful concepts such as:

```text
wrapAxis
wrapRadius
```

Reuse them.

Do NOT project a flat circle through the cylinder.

Instead create an unwrapped cylindrical coordinate system.

Conceptually:

```text
3D arm:

        _______
      /         \
     |     ◎     |
      \_________/

becomes approximately:

Surface coordinates:

 --------------------
|       ◎            |
|                    |
 --------------------
```

Calculate:

```text
u = signed angular distance around axis * wrapRadius
v = distance along axis
```

Then use:

```text
sqrt(u² + v²)
```

for the Bullseye circle.

Use the shortest signed angular distance around the cylinder.

This allows a Bullseye larger than the front-facing width of an arm to visually continue around its side rather than being cut off.

---

# Bullseye Size

Maintain approximately the existing gameplay diameter:

```text
0.26 m
```

unless the existing configuration supplies another value.

The rendering should derive size from the same authoritative Bullseye size used by gameplay wherever practical.

Avoid separate visual/gameplay size values that silently diverge.

---

# Bullseye Shape

The target should remain recognizably circular in **surface space**.

Acceptable:

```text
Circle visually curves around chest
Circle bends around arm
Circle looks distorted by perspective
Circle disappears partially behind the visible side of the body due to normal occlusion
```

Not acceptable:

```text
Random sectors missing from the actual surface
Half-circle because projector failed
Bullseye clipped because of projection depth
Bullseye appearing simultaneously on unrelated geometry
```

Important distinction:

A Bullseye wrapping around an arm may naturally extend around the invisible backside from the player's current camera angle.

That is correct.

It should still exist continuously on the physical surface.

---

# Occlusion

Normal mesh occlusion should determine what the camera sees.

Example:

If a Bullseye wraps around the player's arm:

```text
front half visible
back half naturally hidden by arm geometry
```

That is correct.

Do NOT render the back side through the arm.

Do NOT use an always-on-top shader.

The Bullseye remains part of the character surface and should obey normal depth testing.

---

# No Z-Fighting

Because the Bullseye is composited directly into the player's material, there should be no need for:

```text
surface offset
depth bias
floating mesh
projector pivot
projection depth
```

The Bullseye must not flicker or disappear due to z-fighting.

---

# Character Animation

Because the effect is rendered directly through the character's `SkinnedMeshRenderer`, it must naturally follow skin deformation.

Test:

- Idle
- Walking
- Sprinting
- Strafing
- Jumping
- Crouching
- Prone
- Dolphin Dive
- Wall Run
- Shooting
- Reloading
- ADS

The Bullseye should deform along with the body.

There should be no separate object struggling to follow bones after deformation.

---

# Surface Position Source

Do NOT create another independent Bullseye movement system inside the shader.

Continue using REQ-054's authoritative surface state.

Conceptually:

```text
BullseyeMover
       ↓
BullseyeSurfaceMap
       ↓
Current Surface Pose
       ↓
BullseyeSurfaceVisual
       ↓
MaterialPropertyBlock
       ↓
Character Shader
```

Gameplay decides WHERE the Bullseye is.

The shader decides HOW that position is painted onto the mesh.

---

# AttachedHitTarget

The existing gameplay hit target should continue following the authoritative Bullseye surface pose.

REQ-058 does not require replacing it.

However, visually verify alignment between:

```text
visible shader Bullseye
```

and:

```text
AttachedHitTarget
```

They should correspond closely enough that a player shooting the visible target reliably hits the vulnerability.

---

# Debug Alignment Mode

Preserve or add a debug option that shows:

```text
Bullseye visual center
AttachedHitTarget
Current surface region
Target surface region
Surface normal
Surface tangent
Wrap axis
Wrap radius
```

This should make visual/gameplay alignment easy to diagnose.

Debug visualization must be disabled during normal play.

---

# Detached State

The new material Bullseye is ONLY used while:

```text
BullseyeState == Attached
```

When detached:

```text
_BullseyeEnabled = 0
```

or equivalent.

The character material should immediately stop drawing the target.

Then the existing physical Bullseye appears.

State sequence:

```text
ATTACHED

Player shader:
Bullseye visible

Physical bullseye:
Hidden
```

↓

```text
DETACHED

Player shader:
Bullseye disabled

Physical bullseye:
Visible + physics enabled
```

↓

```text
RETURNING

Player shader:
Bullseye disabled

Physical bullseye:
Returning
```

↓

```text
ATTACHED

Physical bullseye:
Hidden

Player shader:
Bullseye visible again
```

Never display both simultaneously.

---

# Physical Bullseye

Preserve the existing detached physical Bullseye implementation.

Do NOT redesign:

```text
BullseyePhysicalDisc
```

unless a small compatibility modification is necessary.

It remains appropriate for the Bullseye to be a physical object when it has actually been removed from the player's body.

---

# Combustion Grenade

Verify:

1. Bullseye is visible in character material.
2. Combustion Grenade removes Bullseye.
3. Shader Bullseye immediately disappears.
4. Physical Bullseye appears at correct world-space location.
5. Existing grenade behavior continues.
6. Return behavior works.
7. Shader Bullseye reappears after reattachment.

---

# Magnetism Grenade

Perform the same validation for the Magnetism Grenade.

Do not break its ability to interact with the detached physical Bullseye.

---

# Multiplayer

The rendering system must work independently for each network player.

Host example:

```text
Player A
Bullseye = chest

Player B
Bullseye = right arm
```

Both must render correctly at the same time.

Client example:

```text
Player A sees Player B's correct Bullseye
Player B sees Player A's correct Bullseye
```

Do NOT create shared-material behavior where changing one player's shader parameters moves every Bullseye.

---

# Owner Visibility

Preserve the existing behavior:

```text
hideFromOwnerCameraDistance = 0
```

or its functional equivalent.

Solo testing must still allow the developer to see the local player's Bullseye where appropriate.

Do not interpret first-person camera clipping as a reason to globally hide the attached visual.

---

# Performance

This is an online FPS.

Avoid expensive approaches such as:

```text
Bake entire SkinnedMesh every rendered frame for every player
```

unless proven necessary.

The preferred shader solution should primarily require:

- normal character rendering;
- several additional shader parameters;
- Bullseye texture sample;
- surface coordinate calculations;
- region mask evaluation.

Do not introduce a second fully baked player mesh per frame merely to draw one Bullseye if the material approach works.

---

# Shader Cost

Keep the implementation reasonable.

The shader does not need a perfectly mathematically exact geodesic solution across an arbitrary human mesh.

The goal is a convincing approximation based on:

```text
surface region
+
local tangent frame
+
optional cylindrical wrapping
```

Do not implement extremely expensive iterative mesh-surface pathfinding in the shader.

---

# Current Visual Assets to Clean Up

The project currently contains multiple experiments.

Examples include:

```text
AttachedVisual
AttachedSticker
StampOverlay

BullseyeSurfaceDecal.mat
BullseyeMeshStamp.mat

BullseyeMeshStamp.shader
BullseyeMeshStamp.hlsl

BullseyePhysicalDisc.shader
BullseyePhysicalDisc.hlsl
```

Do NOT immediately delete everything at the beginning of the ticket.

First implement and verify the new character-material solution.

After successful verification:

- remove or disable the obsolete `DecalProjector` attached path;
- remove obsolete attached sticker logic;
- remove obsolete StampOverlay logic;
- remove unused serialized fields from `BullseyeSurfaceVisual`;
- delete truly unused visual assets where safe;
- preserve anything still required for the detached physical Bullseye.

The project should finish with ONE canonical attached visual system.

---

# BullseyeSurfaceVisual Cleanup

`Assets/Input/BullseyeSurfaceVisual.cs` currently contains legacy logic related to multiple attempted visual approaches.

Refactor it.

Its new responsibility should be approximately:

```text
Receive authoritative Bullseye surface pose
        ↓
Convert pose to shader parameters
        ↓
Apply per-player MaterialPropertyBlock
        ↓
Enable/disable attached Bullseye based on state
```

It should no longer need to manage competing visual implementations.

---

# Do Not Retry These Approaches

Do NOT solve REQ-058 by reverting to:

### HDRP DecalProjector

Do not continue tweaking:

```text
projectionDepth
pivot
angleFade
orientation
```

as the primary solution.

### Extra material slot

The character mesh has one submesh.

Do not append a second Material and expect another draw.

### Flat physical sticker

Do not restore the thin cylinder/card while attached.

### Curved sleeve

Do not wrap a separate cylinder around limbs.

### Existing StampOverlay

Do not simply re-enable the previously failed custom unlit `SkinnedMeshRenderer` overlay.

### Duplicate movement system

Do not create another Bullseye mover specifically for rendering.

---

# Fallback

If a proper player-material shader implementation proves genuinely impossible after inspecting the character setup, the only acceptable fallback to investigate is:

```text
rendering a patch made from the ACTUAL deformed player surface triangles
```

rather than returning to a projector or rigid sticker.

For example:

```text
CPU BakeMesh
        ↓
Identify only triangles beneath Bullseye
        ↓
Render small surface patch
```

This is a FALLBACK, not the first implementation choice.

Before using it, document precisely why the HDRP material solution cannot work.

Do not silently abandon the preferred shader approach after the first compilation problem.

---

# Implementation Sequence

## Phase 1 — Inspect

Before modifying assets, inspect:

- actual Player prefab;
- actual `SkinnedMeshRenderer`;
- current character Materials;
- current character Shader;
- mesh UV channels;
- mesh vertex colors;
- bone weights;
- `BullseyeSurfaceMap`;
- `BullseyeSurfaceVisual`;
- `AttachedHitTarget`;
- attached/detached state logic.

Verify the REAL prefab in:

```text
Assets/Player/Player.prefab
```

Do not assume a virtual/MCP representation successfully modified the real asset.

---

## Phase 2 — Prototype Character Shader

Create an HDRP-compatible character material/shader that reproduces the player's current appearance.

Before adding moving Bullseye logic, confirm:

- character renders normally;
- lighting is correct;
- textures remain correct;
- host and client can see the character.

Then add a simple static Bullseye parameter.

Confirm the Bullseye visibly appears inside the character material.

---

## Phase 3 — Per-Player Parameters

Use `MaterialPropertyBlock` or equivalent.

Verify two players can simultaneously show Bullseyes at different locations.

---

## Phase 4 — Region Mask

Implement mesh body-region identification.

Test intentionally difficult case:

```text
Bullseye on hip
Arm hanging directly beside hip
```

Required:

```text
Hip painted
Arm NOT painted
```

Then test:

```text
Bullseye on front torso
```

Required:

```text
Front painted
Back NOT painted
```

---

## Phase 5 — Tangent Surface Rendering

Implement broad-surface Bullseye coordinates.

Test:

- chest;
- abdomen;
- back;
- thigh.

Bullseye should remain complete and visually attached.

---

## Phase 6 — Cylindrical Wrapping

Implement wrapped coordinates for:

- upper arm;
- forearm;
- neck;
- lower leg.

Test with existing approximately 0.26 m Bullseye.

The target should visibly continue around the body's curvature.

---

## Phase 7 — Continuous Movement

Connect shader rendering to the existing moving surface pose.

Test movement:

```text
Chest
→ side
→ back

Chest
→ shoulder
→ upper arm

Chest
→ neck
→ head

Hip
→ thigh
```

No teleporting.

No unrelated surfaces painting.

No disappearing.

---

## Phase 8 — Gameplay Alignment

Enable Bullseye hit-target debug visualization.

Ensure:

```text
visible Bullseye
≈
AttachedHitTarget
```

throughout movement.

---

## Phase 9 — Detachment

Verify:

- Combustion Grenade;
- Magnetism Grenade;
- physical Bullseye;
- return;
- shader reactivation.

---

## Phase 10 — Multiplayer

Test host + client.

Verify:

- both attached Bullseyes visible;
- independent locations;
- movement synchronized;
- detach synchronized;
- return synchronized;
- no shared-material state contamination.

---

## Phase 11 — Cleanup

Once verified:

- remove DecalProjector dependency;
- remove obsolete attached sticker;
- remove obsolete overlay;
- simplify `BullseyeSurfaceVisual`;
- retain physical-disc assets required for detached state.

---

# Acceptance Criteria

REQ-058 is complete when ALL of the following are true.

## Rendering Architecture

- [ ] Attached Bullseye no longer uses an HDRP `DecalProjector`.
- [ ] Attached Bullseye is rendered through the actual character mesh/material.
- [ ] Player's normal HDRP appearance is preserved.
- [ ] No attached physical disc is visible.
- [ ] No curved sleeve object is visible.
- [ ] No duplicate overlay character is required for the normal attached visual.

## Visual Quality

- [ ] Bullseye visibly looks painted onto the character surface.
- [ ] Bullseye remains recognizable as a circular target.
- [ ] Natural surface/perspective warping is acceptable.
- [ ] Random missing sectors are NOT present.
- [ ] Bullseye does not z-fight.
- [ ] Bullseye does not disappear into the mesh.

## Surface Isolation

- [ ] Hip Bullseye does not paint nearby arm.
- [ ] Torso-front Bullseye does not simultaneously paint torso-back.
- [ ] Bullseye does not paint held guns.
- [ ] Bullseye does not paint environment geometry.
- [ ] Bullseye only appears on intended character surface regions.

## Surface Movement

- [ ] Bullseye follows existing REQ-054 movement.
- [ ] Bullseye moves continuously between connected regions.
- [ ] Chest → side → back works.
- [ ] Chest → shoulder → arm works.
- [ ] Chest → neck → head works.
- [ ] Hip → thigh works.

## Wrapping

- [ ] Bullseye follows chest/stomach curvature.
- [ ] Bullseye wraps around upper arm.
- [ ] Bullseye wraps around forearm.
- [ ] Bullseye wraps around neck when appropriate.
- [ ] Bullseye wraps around lower leg when appropriate.
- [ ] Large Bullseye does not behave like a rigid flat card.

## Animation

- [ ] Idle works.
- [ ] Walking works.
- [ ] Sprinting works.
- [ ] Strafing works.
- [ ] Jumping works.
- [ ] Crouching works.
- [ ] Prone works.
- [ ] Dolphin Dive works.
- [ ] Wall Running works.
- [ ] Shooting works.
- [ ] Reloading works.
- [ ] ADS works.

## Combat

- [ ] Existing `AttachedHitTarget` remains functional.
- [ ] Visible Bullseye closely matches hit target.
- [ ] Shooting visible Bullseye reliably registers.
- [ ] Existing damage behavior remains unchanged.

## Detachment

- [ ] Combustion Grenade detaches Bullseye.
- [ ] Magnetism Grenade interacts correctly.
- [ ] Character-material Bullseye disappears immediately when detached.
- [ ] Physical Bullseye appears at correct location.
- [ ] Physical Bullseye remains functional.

## Return

- [ ] Physical Bullseye returns normally.
- [ ] Physical object disappears after reattachment.
- [ ] Character-material Bullseye reappears.
- [ ] Attached hit target reactivates.
- [ ] Movement continues normally.

## Multiplayer

- [ ] Host sees all attached Bullseyes.
- [ ] Client sees all attached Bullseyes.
- [ ] Each player can have a different Bullseye position.
- [ ] Per-player shader state does not leak between players.
- [ ] Detachment synchronizes.
- [ ] Return synchronizes.
- [ ] Respawn synchronizes.
- [ ] Late joiners see correct attached/detached state.

## Cleanup

- [ ] DecalProjector is no longer the attached rendering path.
- [ ] Obsolete attached visual experiments are removed or clearly disabled.
- [ ] Detached physical Bullseye assets remain intact.
- [ ] `BullseyeSurfaceVisual` has one clear visual responsibility.
- [ ] Unity console is clean after implementation.

---

# Non-Goals

REQ-058 does NOT include:

- redesigning autonomous Bullseye movement;
- changing crawl speed;
- changing jump influence;
- changing Bullseye damage;
- changing Bullseye size for balance;
- redesigning grenades;
- redesigning the physical detached Bullseye;
- changing weapon systems;
- changing animations;
- redesigning player locomotion;
- adding packages;
- determining whether Bullseye movement is "fun."

Those belong in separate tickets/playtests.

---

# Required Playtest

Do not consider this ticket complete solely because the shader compiles.

Run a human-visible playtest.

At minimum observe the Bullseye traveling through:

```text
Chest
→ Lower Torso
→ Hip
→ Side
→ Back
```

and:

```text
Chest
→ Shoulder
→ Upper Arm
→ Forearm
```

and:

```text
Chest
→ Neck
→ Head
```

Specific regression test:

1. Put the Bullseye on the hip.
2. Allow the player's arm to hang beside it.
3. Confirm the Bullseye exists on the hip ONLY.
4. Rotate the character/camera around the player.
5. Confirm no disconnected Bullseye segment appears on the arm.

Specific wrap test:

1. Put the Bullseye on an upper arm.
2. Use the normal ~0.26 m diameter.
3. View the arm from multiple angles.
4. Confirm the target follows the arm's curvature.
5. Confirm it does not behave like a flat card intersecting the arm.

Specific multiplayer test:

1. Start host + client.
2. Give both players attached Bullseyes.
3. Move them to different regions.
4. Verify each client sees both correct locations.
5. Detach one Bullseye.
6. Confirm only that player's shader Bullseye disappears.
7. Confirm physical Bullseye appears and returns correctly.

---

# Final Expected Architecture

Before REQ-058:

```text
Bullseye Surface Pose
        ↓
HDRP DecalProjector
        ↓
Projection Box
        ↓
Character + nearby geometry
        ↓
Missing / leaking / disappearing visual
```

After REQ-058:

```text
BullseyeMover
        ↓
BullseyeSurfaceMap
        ↓
Authoritative Surface Pose
        ↓
BullseyeSurfaceVisual
        ↓
Per-Player Shader Parameters
        ↓
Character HDRP Material
        ↓
Body Region Mask
        +
Surface Coordinate Calculation
        ↓
 ┌───────────────────────────────┐
 │ Broad Region                 │
 │ Tangent-Space Bullseye       │
 │                              │
 │ Thin Region                  │
 │ Cylindrical Surface Wrap     │
 └───────────────────────────────┘
        ↓
Bullseye painted directly
onto animated player mesh
```

Detached behavior remains:

```text
Attached
Character Shader Bullseye
        ↓
Grenade Detachment
        ↓
Shader Bullseye OFF
        ↓
Physical Bullseye ON
        ↓
Return
        ↓
Physical Bullseye OFF
        ↓
Character Shader Bullseye ON
```

The final attached Bullseye should feel like a **moving vulnerability painted directly onto the player's actual animated body**, rather than a projected box, card, sleeve, or floating object.
````
