# REQ-072 — Replace Bullseye Diagram with Live Bullseye Tracking Body Camera

## Summary

Replace the current HUD system that indicates the player's Bullseye position using static front/back body diagrams and a moving Bullseye icon.

The existing system currently provides an approximate representation similar to:

```text
Front Body Image
Back Body Image
+
Moving Bullseye Indicator
```

This should be replaced with a small **live third-person camera feed** located in the upper-right portion of the player's HUD.

The new camera should:

* Track the local player's actual Bullseye decal.
* Stay approximately 2 feet / ~0.6 meters away from the Bullseye.
* Point toward the Bullseye and surrounding portion of the player's body.
* Follow the Bullseye as it crawls across the player's mesh.
* Show the player's actual third-person body.
* Show the actual Bullseye location.
* Remain readable even when the player is standing in a very dark area.
* Render into a small HUD window through a RenderTexture.
* Be optimized so it does not effectively render the game world a second time at full quality.
* Support an intentionally stylized/low-fidelity visual treatment.

The result should feel like a small body-monitoring camera rather than a cheap diagram.

---

# 1. Goal

The player needs to know where their moving Bullseye is located on their own body.

Currently, this information is communicated through an abstract body diagram.

Instead, the player should see something much closer to:

```text
                           ┌─────────────┐
                           │             │
                           │   LIVE      │
                           │  BODY VIEW  │
                           │       ◎     │
                           │             │
                           └─────────────┘
```

where the displayed image is actually being rendered from the player's third-person model.

As the Bullseye moves around:

* chest
* shoulder
* back
* arm
* leg
* side
* etc.

the camera should move with it.

The player should therefore be able to glance at the display and visually understand:

> "My Bullseye is currently on the back of my left shoulder."

rather than interpreting a 2D diagram.

---

# 2. Remove/Retire Existing Bullseye Diagram

Inspect the existing Bullseye HUD implementation.

The current:

* front body image
* rear body image
* crawling HUD Bullseye marker
* front/back switching logic

should no longer be the primary Bullseye-location UI.

Do not immediately delete reusable code if it is referenced elsewhere.

Instead:

1. Identify the existing system.
2. Disable or retire its visible UI.
3. Preserve any useful underlying Bullseye-position logic if other systems depend on it.
4. Replace the visible representation with the new live-camera system.

There should not be two simultaneous Bullseye-location displays after REQ-072 is complete unless a debug option intentionally enables the old one.

---

# 3. Local Bullseye Camera

Create a dedicated secondary camera for the local player.

Conceptually:

```text
BullseyeTrackingCamera
```

This camera should exist only for purposes of generating the player's Bullseye/body HUD view.

It should NOT replace:

* the main gameplay camera
* spectator cameras
* death cameras
* third-person cameras used by remote players

This is a specialized HUD camera.

---

# 4. RenderTexture Output

The Bullseye camera should render into a RenderTexture.

That RenderTexture should then be displayed through the HUD using an appropriate UI component such as:

```text
RawImage
```

or the equivalent architecture already used in the project's UI.

Conceptually:

```text
BullseyeTrackingCamera
        ↓
RenderTexture
        ↓
HUD RawImage
        ↓
Upper-right Bullseye Monitor
```

Do NOT display the second camera directly to the main screen viewport.

---

# 5. HUD Position

Place the Bullseye monitor in the upper-right area of the HUD.

It should be:

* visible at a glance
* relatively small
* large enough to identify body orientation
* not obstructive
* visually separated from other HUD elements

Its exact:

* anchor
* width
* height
* padding
* screen margin

should be configurable in the UI.

Do not hard-code it to absolute screen coordinates.

---

# 6. Camera Tracking Target

The Bullseye tracking camera must use the Bullseye's actual gameplay/world location.

It should follow the same Bullseye position being used by the Bullseye decal system.

Do NOT create an independent approximation of the Bullseye position for the camera.

Preferred relationship:

```text
Authoritative Bullseye Surface Position
               ↓
Bullseye Decal
               ↓
Tracking Camera Target
```

This ensures the HUD camera always agrees with what other players actually see.

---

# 7. Camera Distance

The target viewing distance should initially be approximately:

```text
2 feet
```

or roughly:

```text
0.6 meters
```

from the Bullseye.

Expose this as an Inspector setting, for example:

```text
Tracking Distance = 0.6
```

Do not assume exactly 0.6 m will work for every body location.

The camera system should be able to adjust slightly when necessary to avoid:

* clipping into the body
* entering the player mesh
* excessive close-up views
* camera intersection with arms/legs
* unusable angles

---

# 8. Use Bullseye Surface Normal

Where available, use the Bullseye's current surface normal to determine the camera's preferred position.

Conceptually:

```text
Camera Position =
Bullseye Position
+
Surface Normal * Tracking Distance
```

The camera then looks back toward the Bullseye.

This is preferable to simply positioning the camera at a fixed world-space offset.

For example:

### Bullseye on Chest

Camera should move in front of the chest.

### Bullseye on Back

Camera should move behind the player.

### Bullseye on Left Arm

Camera should move outward from the left arm.

### Bullseye on Right Leg

Camera should move outward from the right leg.

This should naturally give the player a view of the part of the body containing the Bullseye.

---

# 9. Camera Aim

The camera should point approximately toward:

```text
Bullseye Position
```

but should include enough surrounding body geometry to provide context.

Do not frame the Bullseye so tightly that the monitor becomes:

```text
◎
```

with no indication of where on the body it is.

The player needs to see enough body around the Bullseye to understand its location.

A slight framing offset or camera distance adjustment may therefore be necessary.

---

# 10. Field of View

Expose a dedicated Bullseye camera FOV.

Suggested starting range:

```text
35–55 degrees
```

Tune during testing.

The goal is to show:

* the Bullseye
* nearby body parts
* enough of the player's body to understand orientation

without showing an unnecessarily large section of the map.

---

# 11. Camera Smoothing

The Bullseye crawls across the player's body.

The camera should NOT instantaneously snap to every tiny Bullseye movement.

Use smooth position and rotation interpolation.

Expose values such as:

```text
Position Smooth Speed
Rotation Smooth Speed
Maximum Camera Speed
```

The monitor should feel like a small camera operator following the Bullseye.

It should not:

* jitter
* twitch
* teleport excessively
* shake due to animation bones
* rapidly flip orientation

---

# 12. Prevent Orientation Flipping

Special attention is needed as the Bullseye moves across transitions such as:

```text
front → side → back
```

Surface normals may change rapidly between neighboring triangles.

Naively orienting the camera directly from every instantaneous normal could cause:

* camera flipping
* severe rotation
* jitter
* disorienting spins

Smooth the desired camera direction.

If necessary, maintain a stable camera orientation using:

* interpolated normals
* previous camera orientation
* body-relative up direction
* damping
* angular limits

The camera should behave intentionally rather than exactly reproducing noisy mesh-normal changes.

---

# 13. Player Animation

The body camera should display the player's real animated third-person body.

This includes, where applicable:

* standing
* walking
* sprinting
* crouching
* prone
* jumping
* dolphin diving
* weapon poses
* future animations

The camera should not show a separate static mannequin.

It should represent what the player's actual third-person model is doing.

---

# 14. Local Player Third-Person Model Visibility

Inspect how the project currently handles the local player's:

* third-person body
* first-person arms
* camera culling
* owner visibility
* shadows

The Bullseye camera must be able to render the local player's third-person mesh even if that mesh is intentionally hidden from the main first-person gameplay camera.

For example:

```text
Main Gameplay Camera
    → first-person arms/weapons
    → does not render obstructive local third-person geometry

Bullseye Tracking Camera
    → renders local third-person body
    → renders Bullseye
```

Use camera layers/culling masks or existing owner-visibility architecture to accomplish this.

Do NOT make the third-person body visible inside the player's normal first-person view merely to make REQ-072 work.

---

# 15. First-Person Arms

The Bullseye monitor should primarily represent the third-person player body.

Avoid rendering duplicate first-person-only arms/weapons inside the monitor if those objects are separate from the third-person character representation.

The Bullseye camera should preferably see:

```text
Third-Person Character
Third-Person Weapon
Bullseye
```

not:

```text
First-Person Arms
+
Third-Person Arms
```

---

# 16. Camera Culling Mask

This secondary camera should NOT render the entire game world at normal fidelity.

Create an appropriate culling configuration.

At minimum, investigate restricting the camera to relevant layers such as:

```text
Local Player Third-Person Body
Bullseye
Relevant Equipped Weapon
Minimal Required Environment
```

The goal is to avoid effectively rendering:

```text
Full Gameplay Scene × 2
```

every frame.

---

# 17. Environment Rendering

Ideally, some nearby environment may remain visible because it helps the feed feel like an actual camera.

However, this should be balanced against performance.

Preferred priorities:

1. Player body
2. Bullseye
3. Equipped third-person weapon if appropriate
4. Nearby visual context
5. Distant environment only if inexpensive

If rendering the full environment is too costly, it is acceptable for the feed to have a stylized or simplified background.

---

# 18. Low-Resolution Rendering

The RenderTexture should intentionally use a substantially lower resolution than the main game.

Suggested initial values to test:

```text
256 × 256
```

or:

```text
320 × 320
```

or another appropriately small resolution based on the HUD aspect ratio.

The exact value should be configurable.

There is no reason to render this small HUD element at full screen resolution.

This should be one of the primary performance optimizations.

---

# 19. Render Update Rate

Investigate whether the Bullseye camera needs to render at the full gameplay frame rate.

A monitor-style feed may look perfectly acceptable at:

```text
15 FPS
20 FPS
30 FPS
```

while the main game continues rendering at its normal framerate.

Expose a setting such as:

```text
Bullseye Camera Target FPS
```

or implement an equivalent controlled-render cadence.

Test:

```text
15 FPS
20 FPS
30 FPS
Full Rate
```

and choose the lowest rate that still looks polished.

---

# 20. Stylized Feed

The Bullseye monitor may intentionally have a lower-fidelity visual identity.

Potential effects include:

* slight pixelation
* lower resolution
* subtle grain
* reduced color depth
* increased contrast
* slightly desaturated colors
* scanner/camera-monitor appearance
* light posterization
* optional cel-shaded appearance

However:

These effects should primarily be considered **visual style**.

Do NOT assume that adding grain or cel shading automatically improves performance.

The primary performance savings should come from:

* low RenderTexture resolution
* limited render frequency
* limited culling mask
* simplified camera rendering
* reduced unnecessary effects

---

# 21. Recommended Initial Style

For the first implementation, prefer something simple such as:

```text
Low Resolution
+
Slightly Increased Contrast
+
Subtle Grain
+
Fixed Exposure
```

before creating a custom cel-shading pipeline.

Cel shading can be investigated later if it integrates cleanly with HDRP.

Do not significantly increase REQ-072 scope merely to achieve a complicated shader style.

---

# 22. Dark Area Visibility

The Bullseye monitor must remain useful when the actual player is standing in darkness.

Example:

The player enters a nearly unlit room.

The main gameplay world may appropriately be dark.

The Bullseye monitor should still allow the player to identify:

* their body
* their Bullseye
* the Bullseye's location

Do not simply reproduce complete darkness in the monitor.

---

# 23. Dedicated Exposure

Investigate giving the Bullseye camera a dedicated exposure configuration independent from the primary gameplay camera.

In HDRP, use the appropriate:

* camera settings
* volume layer
* exposure override
* post-processing setup

so the Bullseye feed maintains predictable visibility.

The exact implementation should respect the project's existing HDRP setup.

The Bullseye monitor should not change the exposure of the main gameplay camera.

---

# 24. Bullseye Visibility

The Bullseye itself should remain highly visible in the monitor.

Its red appearance should remain consistent with the game's established Bullseye red.

If necessary, the Bullseye rendering in this camera may use:

* emissive contribution
* dedicated visibility treatment
* controlled lighting
* camera-specific rendering

provided this does not incorrectly change how the Bullseye looks to other players in the normal world.

The monitor should reliably answer:

> Where is my Bullseye?

---

# 25. Body Visibility in Darkness

It is not sufficient to make only the Bullseye glow while leaving the entire body invisible.

The player must see enough of their own body to understand where the Bullseye is located.

If required, investigate a camera-specific body treatment such as:

* fixed exposure
* low-intensity fill lighting
* simplified lit material
* replacement/custom-pass rendering
* mild rim lighting

Prefer the simplest HDRP-compatible solution.

Do not alter world lighting globally.

---

# 26. Optional Dedicated Camera Light

If fixed exposure alone is insufficient, a small dedicated light may be considered for the Bullseye camera.

However, it must not illuminate the actual multiplayer world for other players.

Any such lighting should only affect what is rendered in this specialized view.

If HDRP/light-layer functionality is used, ensure:

```text
Bullseye Camera View
```

can benefit from the illumination without creating visible world-light artifacts.

Use this only if needed.

---

# 27. Bullseye Attached State

While the Bullseye is attached to the player, the monitor should track it normally.

Example:

```text
Bullseye Attached
       ↓
Camera tracks body surface
       ↓
HUD displays body location
```

---

# 28. Bullseye Detached State

The game already supports the Bullseye becoming physically detached through mechanics such as:

* combustion/explosive effects
* Magnetism Grenade
* other future mechanics

When the Bullseye becomes detached, the camera behavior must be explicitly defined.

Preferred behavior:

The Bullseye camera should continue tracking the **physical detached Bullseye**.

This means the player's monitor becomes a live view centered on where their vulnerable Bullseye has gone.

Example:

```text
Bullseye ripped from player's back
        ↓
Physical Bullseye flies across room
        ↓
HUD camera follows detached Bullseye
```

This provides highly valuable gameplay information.

---

# 29. Detached Bullseye Camera Framing

When detached, there may no longer be a useful body surface normal.

The tracking system should therefore switch to an appropriate detached-target mode.

Potential behavior:

* follow the physical Bullseye
* maintain a stable offset
* orient toward it
* include enough surrounding environment for context
* avoid extreme spinning as the physical Bullseye rotates

Do NOT directly attach the camera orientation to the Bullseye rigidbody's rotation if that causes the monitor to spin uncontrollably.

The camera should track the Bullseye position, not necessarily inherit its rotation.

---

# 30. Bullseye Return

When the detached Bullseye returns to the player:

```text
Detached Physical Bullseye
        ↓
Bullseye Reattaches
        ↓
Decal Reappears
        ↓
Camera transitions back to attached tracking
```

The camera should smoothly return to its normal body-tracking mode.

Avoid a visually jarring one-frame teleport if possible.

---

# 31. Respawn

On player death/elimination and respawn:

* ensure the camera target is reset
* ensure no destroyed Bullseye reference remains
* reacquire the new/current Bullseye
* restore the monitor correctly

The monitor should never remain pointed at the previous death location after respawn.

---

# 32. Multiplayer Scope

The Bullseye camera is a **local-player HUD feature**.

Each client should only create/render the Bullseye monitor needed for its own local player.

Do NOT create an active RenderTexture camera for every network player on every client.

For example, in an 8-player match:

```text
Client A
→ renders Client A's Bullseye monitor

Client B
→ renders Client B's Bullseye monitor
```

not:

```text
Every client renders 8 Bullseye cameras
```

This is essential for performance.

---

# 33. Network Authority

The camera itself does not need network authority.

It should consume the local representation of already-networked Bullseye information.

The camera should not:

* move the Bullseye
* modify Bullseye state
* send movement RPCs
* determine gameplay position

It is a visualization system only.

---

# 34. Performance Requirements

Because this feature introduces a second camera, explicitly profile its rendering cost.

Test at minimum:

```text
Monitor Disabled
vs.
Monitor Enabled
```

Measure impact on:

* FPS
* GPU frame time
* CPU frame time if relevant
* memory allocation
* RenderTexture memory

The camera should be optimized before assuming the feature is complete.

---

# 35. Performance Optimization Order

If performance is poor, optimize approximately in this order:

### 1. Reduce RenderTexture Resolution

Example:

```text
320 × 320
→
256 × 256
```

### 2. Reduce Camera Render Frequency

Example:

```text
60 FPS
→
30 FPS
→
20 FPS
```

### 3. Restrict Camera Culling Mask

Avoid rendering unnecessary world objects.

### 4. Disable Expensive Camera Effects

Do not duplicate expensive HDRP effects such as:

* volumetrics
* high-cost shadows
* motion blur
* depth of field
* unnecessary post processing
* expensive reflections

### 5. Simplify Lighting/Rendering

Only after the above should more specialized solutions be considered.

---

# 36. HDRP Frame Settings

Inspect HDRP Camera Frame Settings for the secondary camera.

Disable anything unnecessary for the tiny Bullseye feed.

Candidates to evaluate include:

* volumetric fog
* screen-space reflections
* motion vectors
* contact shadows
* expensive transparent effects
* high-quality shadow features
* post-processing features not required by the monitor

Do not blindly duplicate the main gameplay camera's complete rendering configuration.

---

# 37. Monitor Border

Add a subtle HUD frame/border around the camera feed so it visually reads as an intentional game interface element rather than a random second viewport.

Keep it understated.

Possible style:

```text
┌───────────────┐
│               │
│  BODY CAMERA  │
│               │
└───────────────┘
```

A text label is optional.

The feed should remain the visual focus.

---

# 38. Aspect Ratio

Choose an aspect ratio suitable for viewing part of the player's body.

A square or mildly portrait-oriented frame may work well.

Examples:

```text
1:1
```

or:

```text
4:5
```

Expose layout through the UI so this can be adjusted after testing.

---

# 39. Optional Bullseye Center Indicator

The actual Bullseye should normally be obvious in the rendered image.

Do not automatically add another large HUD Bullseye over it.

However, if testing shows that tracking becomes difficult in visually complicated scenes, support a subtle optional indicator such as:

```text
small corner brackets
```

around the actual Bullseye location.

This should only be added if needed.

The primary goal is to show the actual Bullseye itself.

---

# 40. Camera Occlusion / Clipping

The player body has complicated geometry.

A simple:

```text
Bullseye Position + Surface Normal * 0.6m
```

may occasionally place the camera:

* inside another limb
* behind a shoulder
* inside clothing
* inside nearby world geometry

Implement reasonable protection against unusable camera positions.

Possible approaches include:

* sphere cast from Bullseye toward desired camera position
* minimum distance
* maximum distance
* camera collision adjustment
* small normal-offset corrections

Do not build an enormous third-person camera system for this ticket.

The goal is simply to prevent obviously broken views.

---

# 41. Nearby World Geometry

If the player's Bullseye is against a wall, a two-foot outward camera position could place the monitor camera inside the wall.

Detect this situation.

Adjust the camera closer to the Bullseye if necessary.

For example:

```text
Desired distance = 0.6 m
Available distance = 0.25 m

Actual camera distance ≈ 0.22 m
```

while respecting a minimum usable distance.

This should be handled gracefully.

---

# 42. Camera Motion During Fast Player Movement

Test the monitor during:

* sprint
* jump
* fall
* landing
* dolphin dive
* prone
* rapid turning
* climbing
* grenade knockback
* Bullseye crawling

The feed should remain understandable.

Some motion is expected because it is showing the actual character.

However, excessive shake from animation bones or frame-to-frame Bullseye changes should be smoothed.

---

# 43. Pausing

When the game is paused locally, follow the project's existing pause behavior.

If gameplay animation freezes, the Bullseye monitor should naturally freeze with it.

Do not continue unnecessarily rendering a static Bullseye camera at full frequency while paused if it can be avoided.

---

# 44. Settings / Accessibility Preparation

The architecture should allow us to later expose options such as:

```text
Bullseye Camera:
ON / OFF

Camera Size:
Small / Medium / Large

Camera Style:
Normal / Monitor / Stylized
```

REQ-072 does not require these settings UI elements yet.

Do not tightly couple the system in a way that makes those options difficult later.

---

# 45. Debug Mode

Provide a useful development/debug option.

Possible debug information:

```text
Bullseye Position
Bullseye Normal
Desired Camera Position
Actual Camera Position
Tracking Mode
Attached / Detached
```

Optionally draw debug rays:

```text
Bullseye Surface Normal
Camera Offset Direction
Camera Look Direction
```

This will make tuning the surface-tracking behavior substantially easier.

Debug visuals must be disabled for normal gameplay/builds.

---

# 46. Suggested Architecture

Cursor should inspect the existing Bullseye and HUD systems before choosing final class names.

A possible structure might be:

```text
BullseyeCameraController
BullseyeCameraHUD
BullseyeCameraTargetProvider
```

Conceptually:

### BullseyeCameraTargetProvider

Provides:

```text
Current Bullseye Position
Current Surface Normal
Attached/Detached State
Current Bullseye Transform
```

### BullseyeCameraController

Handles:

```text
Camera Position
Camera Rotation
Smoothing
Collision
Attached Tracking
Detached Tracking
Render Frequency
```

### BullseyeCameraHUD

Handles:

```text
RenderTexture
RawImage
HUD Layout
Visibility
```

These names are suggestions only.

Reuse existing systems where sensible.

---

# 47. Do Not Couple Camera to Decal Rendering Internals

The camera needs:

```text
Where is the Bullseye?
What surface direction is it facing?
Is it attached?
```

It should not need to understand every detail of how the decal is projected.

Prefer exposing Bullseye state through a stable gameplay-facing interface.

This will reduce breakage if the decal implementation changes again later.

---

# 48. Scope Boundaries

REQ-072 DOES include:

* replacing the old front/back Bullseye diagram
* local-player Bullseye tracking camera
* RenderTexture HUD display
* camera tracking attached Bullseye
* camera tracking detached Bullseye
* third-person local body rendering
* dark-area visibility
* low-resolution optimization
* render-frequency optimization
* camera smoothing
* camera collision/clipping protection
* HDRP secondary-camera optimization
* basic monitor visual treatment
* multiplayer-safe local-only camera creation

REQ-072 DOES NOT require:

* redesigning the Bullseye crawling mechanic
* changing Bullseye damage
* changing Bullseye networking
* rewriting the player animation system
* implementing a full cel-shading renderer
* rendering every player's Bullseye camera
* spectator support
* recording this camera feed
* replay functionality
* killcams
* completely redesigning the HUD
* changing the main gameplay camera

---

# 49. Acceptance Criteria

REQ-072 is complete when:

* [ ] The old front/back Bullseye diagram is no longer the primary Bullseye-location display.
* [ ] A live camera feed appears in the upper-right HUD.
* [ ] The camera renders through a RenderTexture.
* [ ] The feed displays the local player's actual third-person body.
* [ ] The feed displays the actual Bullseye.
* [ ] The camera follows the Bullseye as it crawls around the body.
* [ ] The camera generally remains approximately 2 feet / 0.6 m from the Bullseye where geometry allows.
* [ ] Camera distance is configurable.
* [ ] Camera FOV is configurable.
* [ ] The Bullseye surface normal influences camera positioning.
* [ ] Front/body/back transitions do not cause severe camera flipping.
* [ ] Camera movement is smoothed.
* [ ] The camera does not commonly clip inside the player's body.
* [ ] Nearby walls do not routinely cause the camera to render from inside geometry.
* [ ] The local third-person body can be visible to the Bullseye camera without becoming obstructively visible to the main first-person camera.
* [ ] First-person-only arms are not incorrectly duplicated in the Bullseye camera.
* [ ] The feed remains readable in dark environments.
* [ ] The Bullseye remains visible in dark environments.
* [ ] The player's body remains sufficiently visible to understand Bullseye location.
* [ ] The RenderTexture uses intentionally reduced resolution.
* [ ] The secondary camera does not unnecessarily duplicate all expensive HDRP rendering features.
* [ ] Render frequency can be reduced/tuned if necessary.
* [ ] Only the local player requires an active Bullseye HUD camera.
* [ ] Detached Bullseyes can be tracked.
* [ ] The camera transitions back to body tracking when the Bullseye reattaches.
* [ ] Respawning correctly resets/reacquires the camera target.
* [ ] Existing Bullseye gameplay remains intact.
* [ ] Existing multiplayer functionality remains intact.
* [ ] Performance impact has been profiled and is acceptable.

---

# 50. Testing Checklist

## Chest

1. Place Bullseye on the player's chest.
2. Confirm camera moves in front of the player.
3. Confirm chest and Bullseye are clearly visible.

## Back

1. Allow Bullseye to crawl onto the player's back.
2. Confirm camera moves behind the player.
3. Confirm feed clearly communicates that the Bullseye is on the back.

## Arm

1. Move Bullseye onto an arm.
2. Confirm camera provides enough surrounding body context to identify which area is shown.

## Leg

1. Move Bullseye onto a leg.
2. Confirm camera follows correctly.
3. Confirm camera does not frequently clip into the opposite leg.

## Surface Transition

Test:

```text
Chest → Side → Back
```

Confirm:

* no severe orientation snapping
* no rapid 180-degree oscillation
* reasonable smooth camera travel

## Player Movement

Test while:

* walking
* sprinting
* crouching
* prone
* jumping
* landing
* turning
* dolphin diving

Confirm feed remains usable.

## Dark Room

1. Move player into a very dark area.
2. Confirm main game remains appropriately dark.
3. Confirm Bullseye monitor still shows:

   * body
   * Bullseye
   * sufficient body context

## Wall Occlusion

1. Stand with Bullseye side close to a wall.
2. Confirm desired camera position would intersect the wall.
3. Confirm system adjusts rather than rendering from inside the wall.

## Detached Bullseye

1. Detach Bullseye with an applicable mechanic.
2. Confirm monitor switches to the physical Bullseye.
3. Confirm camera follows its position.
4. Confirm Bullseye rigidbody rotation does not cause uncontrolled camera spinning.

## Reattachment

1. Allow Bullseye to return.
2. Confirm monitor smoothly reacquires attached body tracking.

## Respawn

1. Get eliminated.
2. Respawn.
3. Confirm monitor targets the new/current player and Bullseye correctly.

## Multiplayer

Test with at least two players.

Confirm:

```text
Player 1 client
→ renders Player 1 Bullseye monitor

Player 2 client
→ renders Player 2 Bullseye monitor
```

and that each client is not unnecessarily rendering a HUD camera for every remote player.

## Performance

Compare:

```text
Bullseye Camera Disabled
vs.
Bullseye Camera Enabled
```

Test several configurations:

```text
320px / 30 FPS
256px / 30 FPS
256px / 20 FPS
```

Choose a sensible default based on visual quality and performance.

---

# 51. Desired Result

The player's Bullseye-position HUD should no longer feel like an abstract diagram.

Instead, the upper-right HUD should function like a small body camera focused on the player's vulnerability.

For example, when the Bullseye moves onto the player's rear shoulder, the player should glance at the monitor and immediately see:

```text
their animated third-person shoulder/back
+
the actual Bullseye sitting on it
```

When the Bullseye crawls toward the leg, the camera should smoothly move with it.

When the Bullseye is ripped from the player's body, the feed should follow it.

The final result should feel like an intentional part of Bullseye's visual identity and gameplay rather than a temporary diagnostic UI.
