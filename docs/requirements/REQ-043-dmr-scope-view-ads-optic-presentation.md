# REQ-043 — DMR Scope View / ADS Optic Presentation

## Summary

Add a DMR-specific first-person scope presentation that appears when the player aims down sights with the DMR.

REQ-042 already provides functional DMR magnification.

REQ-043 should add the **visual appearance of looking through a scope** while preserving the existing camera zoom and shot-direction logic.

The DMR scope should feel like a relatively low-power combat optic rather than a high-magnification sniper scope.

The architecture must be reusable so future weapons can define different scope presentations, including a future sniper rifle with:

* A different scope visual style
* Stronger magnification
* Potential multiple zoom levels

---

# 1. Primary Goal

When the player aims with the DMR, the view should clearly communicate:

> The player is looking through an optical sight.

Currently the world simply magnifies.

After REQ-043, DMR ADS should additionally provide:

* Scope framing
* Dedicated scope reticle
* Darkened/obscured peripheral area
* Subtle lens visual treatment
* Smooth appearance/disappearance during ADS transitions

The result should feel significantly different from aiming with:

* Pistol
* AK
* Shotgun

---

# 2. Scope Is a Visual Layer Over Existing Magnification

Do not replace the magnification system established in REQ-042.

The architecture should remain:

`Normal Camera`

↓

Player activates DMR ADS

↓

REQ-042 applies DMR magnification

*

REQ-043 displays DMR optic presentation

The scope presentation should not independently modify shot trajectory.

---

# 3. DMR Scope Style

The DMR should use a relatively open, low-power scope presentation.

Target visual characteristics:

* Circular or softly rounded scope viewing area
* Darkened outer/peripheral region
* Clear center image
* Dedicated crosshair / precision reticle
* Slight lens vignette
* Subtle optical appearance

Avoid making the DMR feel like a very restrictive sniper scope.

The player should retain more situational awareness than they eventually will with the sniper rifle.

---

# 4. Scope Overlay

Create a DMR-specific scope overlay.

Suggested UI structure:

`DMRScopeOverlay`

containing elements such as:

* Scope Mask / Housing
* Lens Area
* Scope Reticle
* Edge Vignette
* Optional subtle lens effects

The overlay should only be visible when:

* DMR is equipped
* ADS is active or transitioning into ADS

---

# 5. Circular / Rounded Scope View

The central portion of the screen should visually represent the optic lens.

This may use:

* Circular mask
* Rounded mask
* Scope housing texture
* Combination of UI elements

The center of the optic must align precisely with:

**screen center / actual firing direction**

The reticle must not visually suggest a different point of aim than the weapon actually uses.

---

# 6. Peripheral Treatment

Outside the DMR optic area, darken the player's peripheral view.

For the DMR, this should not necessarily be completely black.

Preferred initial treatment:

* Significant edge darkening
* Slightly visible peripheral world
* Scope housing / shadow around lens
* Reduced visual emphasis outside optic

This reinforces the sensation of looking through a scope without removing too much situational awareness.

The exact amount should be tunable.

---

# 7. Future Sniper Distinction

Do not use one universal scope overlay for both the DMR and future sniper.

The architecture should support something conceptually similar to:

`ScopePresentationType`

Possible future configurations:

* None
* DMR
* Sniper
* Custom

The DMR presentation should remain relatively open.

The future sniper may instead use:

* Much larger blacked-out peripheral region
* Different scope housing
* Different reticle
* Stronger lens shadow
* Stronger magnification
* Variable zoom

REQ-043 should make these differences easy to configure later.

---

# 8. Dedicated DMR Reticle

While using DMR ADS, replace or hide the normal gameplay reticle as appropriate.

Display a dedicated precision-oriented DMR scope reticle.

Initial reticle can be relatively simple:

* Fine central crosshair
* Small central aiming point
* Thin lines
* Clear visibility

Do not make the reticle excessively thick.

The DMR is intended to support more precise shooting than general-purpose weapons.

---

# 9. Reticle Asset Architecture

Do not hard-code the reticle texture into the ADS script.

Allow a weapon or scope definition to reference:

* Scope reticle sprite/texture
* Scope overlay sprite/texture
* Scope vignette if applicable

This will allow the future sniper to use completely different assets.

---

# 10. Placeholder Assets

If final art assets do not yet exist, create functional placeholders.

For example:

* Simple circular scope mask
* Basic crosshair
* Dark outer vignette

The system architecture is more important than final scope artwork in REQ-043.

All visual elements should be easy to replace in the Inspector later.

---

# 11. Scope Appearance During ADS

The scope should not instantly pop onto the screen the instant Aim is pressed.

Coordinate its appearance with the existing ADS transition.

Suggested behavior:

### Beginning ADS

* Weapon starts moving toward ADS position.
* FOV begins narrowing.
* Scope overlay fades in.
* Peripheral darkening increases.

### Fully Aimed

* Scope overlay reaches full visibility.
* Reticle is fully visible.
* DMR magnification reaches target.

This should feel like the player's eye is moving into alignment with the optic.

---

# 12. Scope Exit

When ADS is released:

* Scope overlay fades out.
* Peripheral darkening disappears.
* Normal reticle returns if appropriate.
* Camera smoothly returns to base FOV.
* Weapon returns from ADS.

The visual transition should coordinate with REQ-042's ADS exit duration.

---

# 13. Interrupted ADS

Handle rapid input correctly.

Example:

Player begins ADS.

Scope overlay is 40% visible.

Player releases Aim.

The overlay should smoothly fade back out from its current state.

Do not:

* Finish fading in first
* Flash
* Snap
* Become stuck visible

---

# 14. Scope Alpha / Transition Driver

Where practical, derive scope opacity from the existing ADS progress value.

Conceptually:

`ADSProgress = 0`

→ no scope

`ADSProgress = 0.5`

→ partially visible

`ADSProgress = 1`

→ full scope

This keeps:

* Weapon movement
* Camera zoom
* Scope appearance

synchronized.

Avoid creating three unrelated timers for the same transition.

---

# 15. Scope Housing

The optic may include visible black/dark scope housing around the lens.

This is desirable because a completely clean circular crop can look like a HUD element rather than a physical optic.

The housing may include:

* Thickened lens edge
* Slight irregular shadow
* Subtle inner ring

Final artwork can be replaced later.

---

# 16. Lens Vignette

Apply subtle darkening toward the edge of the visible lens.

The scope center should remain clear.

This should be a visual effect only.

Do not distort the center aim point.

Keep the effect subtle enough that the DMR remains comfortable to use.

---

# 17. Lens Tint

Optional:

Allow a very subtle lens color/tint.

This should be Inspector-configurable and disabled or near-neutral by default.

Do not heavily recolor the world.

The game should remain readable and accurate.

---

# 18. Lens Distortion

Do not require complex optical distortion for REQ-043.

If an inexpensive subtle lens effect is easy to implement, it may be considered.

However:

* Do not distort the aiming point.
* Do not create strong fisheye effects.
* Do not introduce expensive rendering architecture unnecessarily.

A clean scope overlay is preferable to an overcomplicated effect.

---

# 19. Scope Blur

Do not require true depth-of-field or peripheral blur for the initial implementation.

A subtle pre-authored blurred vignette may be used if aesthetically helpful.

Do not make enemies or environmental information difficult to read solely for visual realism.

---

# 20. Scope Magnification Area

The magnification implemented in REQ-042 currently affects the gameplay camera.

For REQ-043, it is acceptable for the magnified camera image to exist across the full screen while the scope overlay visually obscures/de-emphasizes the peripheral portion.

This is the preferred initial implementation.

Do not create a second rendered world camera solely to magnify only the circular scope area unless needed later.

---

# 21. Avoid RenderTexture Complexity Initially

A true picture-in-picture scope may eventually render a second camera into the lens.

That architecture can produce effects such as:

* Magnified optic center
* Unmagnified peripheral vision
* Independent optic FOV

However, this is **not required for REQ-043**.

For the current DMR:

Use the existing full-camera magnification plus scope overlay.

This should be:

* Simpler
* More performant
* Easier to maintain
* Appropriate for the current prototype

Leave room to revisit picture-in-picture optics later if desired.

---

# 22. First-Person Weapon Model

The DMR model should remain visible appropriately during ADS.

The existing ADS positioning should place the scope/optic in visual alignment with the camera.

The scope overlay should help create the illusion that the player's eye is looking through the weapon optic.

Avoid simultaneously showing an obviously misaligned physical optic beside the center-screen scope overlay.

Tune DMR ADS:

* Position
* Rotation

as necessary.

---

# 23. Physical Scope Alignment

When fully aimed:

The center of the physical DMR optic should visually align with the center of the scope overlay as closely as practical.

Depending on the first-person weapon implementation, it may be desirable for portions of the physical weapon/scope housing to become less visible as the overlay reaches full opacity.

Do not allow obvious duplicate scope rings to create a distracting visual.

---

# 24. Normal Reticle

When DMR ADS is fully active:

Hide the normal hip-fire reticle.

Use only the DMR scope reticle.

When ADS ends:

Restore the appropriate DMR hip-fire reticle.

The transition should not briefly show both reticles at full opacity.

---

# 25. Accuracy of Reticle

The DMR scope reticle center must correspond to the actual center-screen shooting ray.

Test this explicitly at:

* Short range
* Medium range
* Long range

Do not offset actual shooting merely to match a visually misaligned UI reticle.

Correct the visual alignment instead.

---

# 26. Scope Movement

The scope overlay itself should generally remain centered on screen.

Do not make the entire UI scope texture sway significantly with weapon sway.

The weapon may move slightly beneath/around the optic presentation, but the player's actual aiming reference should remain stable.

Future weapon sway may move the actual firing direction if intentionally implemented, but that is outside REQ-043.

---

# 27. Recoil

When the DMR fires while scoped:

Existing recoil mechanics should remain functional.

REQ-043 does not require the scope overlay to physically jump around the screen.

Camera recoil may naturally cause the scoped view to move because the camera aim changes.

Keep the reticle aligned with screen-center firing logic.

---

# 28. Scope Visibility Conditions

The DMR scope overlay should be hidden whenever:

* DMR is not equipped
* Player is not aiming
* Player is dead
* Player is respawning
* ADS is forcibly cancelled
* Wall run cancels ADS
* Sprint cancels ADS
* Pause/menu state requires gameplay HUD suppression

Do not allow the scope overlay to become stuck.

---

# 29. Weapon Switching

If the player switches weapons while DMR ADS is active:

Immediately begin removing the DMR scope presentation.

Example:

DMR scoped

→ switch to pistol

Result:

* DMR scope fades/removes
* Magnification exits
* Pistol HUD/reticle becomes active

The new weapon must not inherit DMR scope graphics.

---

# 30. Death / Respawn

On death:

* Remove scope overlay
* Hide DMR scope reticle
* Clear ADS visual state

On respawn:

* Scope overlay begins hidden
* Normal HUD state is restored

Do not preserve scope opacity across death.

---

# 31. Sprint Compatibility

If sprinting cancels ADS:

Scope overlay should smoothly disappear as ADS exits.

Do not leave peripheral darkening visible while sprinting.

---

# 32. Wall-Running Compatibility

REQ-041 wall running should remain compatible.

If entering a wall run cancels ADS:

* DMR scope view should exit.
* Magnification should return to normal.
* Scope overlay should disappear.

No scope UI should remain while wall-running unless gameplay intentionally supports ADS wall-running later.

---

# 33. Crouch and Prone

DMR scoped ADS should remain available while:

* Crouching
* Prone

where normal ADS is permitted.

The scope presentation should remain identical.

Posture should not alter:

* Scope reticle placement
* Scope mask
* Magnification

unless intentionally configured later.

---

# 34. Multiplayer

Scope presentation is **local-only**.

Do not network:

* Scope overlay
* Scope reticle
* Scope opacity
* Scope vignette
* Scope lens effects

Remote players only need the existing replicated ADS state so REQ-037 can display the player's third-person aiming pose.

---

# 35. Weapon/Optic Definition Architecture

Create reusable scope presentation configuration.

Possible architecture:

`ScopeDefinition`

or scope fields within:

`WeaponDefinition`

Suggested values:

* `UsesScopeOverlay`
* `ScopeOverlaySprite`
* `ScopeReticleSprite`
* `ScopePeripheralOpacity`
* `ScopeLensTint`
* `ScopeTransitionCurve`
* `ScopeStyle`

The exact implementation can follow existing project conventions.

---

# 36. Separate Magnification From Scope Style

Do not assume:

`Magnification == ScopeStyle`

For example, a future weapon might have:

* 1.5x red-dot magnifier
* No circular scope overlay

Another might have:

* 2x sniper scope
* Heavy black scope mask

Another:

* 4x optic
* Different reticle

Therefore maintain separate concepts:

### Optical Behavior

`Magnification`

### Visual Presentation

`Scope Style`

---

# 37. DMR Initial Configuration

Suggested DMR configuration:

### Magnification

Existing REQ-042 value:

`~1.25x`

### Scope Style

`DMR`

### Peripheral Visibility

Partially visible / significantly darkened

### Reticle

Thin precision crosshair

### Lens Tint

None or extremely subtle

### Overlay

Circular or rounded low-power optic

---

# 38. Future Sniper Architecture

The eventual sniper rifle should be able to define a different configuration.

Conceptually:

### Sniper

`ScopeStyle = Sniper`

`Magnification = ~2.0x`

Possible future:

`AvailableMagnifications = [2.0x, 4.0x]`

Its presentation may include:

* Nearly black peripheral area
* Larger circular lens
* Different crosshair
* Range markings
* Stronger lens shadow
* Different transition
* Variable zoom UI

Do not implement the sniper now.

---

# 39. Future Multiple Zoom Levels

REQ-043 should not implement variable magnification.

However, the scope presentation should not assume the optic has only one zoom forever.

The future sniper may have:

`Primary Zoom`

and

`Secondary Zoom`

or multiple magnification steps.

Its reticle and scope visual should remain active while zoom level changes.

---

# 40. Scope UI Layering

Ensure sensible HUD rendering order.

Conceptually:

Gameplay World

↓

Weapon Viewmodel

↓

Scope Presentation

↓

Scope Reticle

↓

Critical UI if required

Avoid allowing unrelated HUD elements to accidentally render over the DMR lens in visually disruptive ways.

Exact HUD elements may be handled individually.

---

# 41. Pause Menu

Opening the pause menu should either:

* Hide gameplay scope UI beneath the menu

or

* Leave it visually behind a fully dominant pause overlay

depending on current UI architecture.

Closing the pause menu must restore the correct state without duplicate scope elements.

---

# 42. Resolution / Aspect Ratio

Scope presentation must work across common screen resolutions and aspect ratios.

Do not rely on fixed pixel coordinates.

Use appropriate Canvas anchoring/scaling.

Test at minimum:

* 16:9
* A different windowed resolution

The optic should remain centered.

The scope mask should not become oval because of incorrect UI scaling.

---

# 43. Performance

REQ-043 should remain inexpensive.

Prefer:

* UI masks
* Sprites
* Simple shader effects

over:

* Additional cameras
* Continuous RenderTextures
* Expensive post-processing

unless clearly necessary.

The scope should not materially reduce multiplayer performance.

---

# 44. Asset Replacement

All placeholder scope visuals must be easy to replace later.

Do not make replacement require changes to gameplay code.

Artists/developers should be able to swap:

* Scope housing
* Reticle
* Vignette
* Lens overlay

through Inspector references or equivalent data configuration.

---

# 45. Existing Gameplay Must Remain Intact

REQ-043 must not regress:

* REQ-042 magnification
* DMR shooting
* Shot accuracy
* Weapon switching
* Reloading
* Mouse input
* Gamepad input
* ADS sensitivity
* Sprint
* Crouch
* Prone
* Jump
* Wall running
* Death
* Respawn
* Multiplayer
* Third-person weapon aiming
* Other weapons' reticles
* Pause menu

---

# 46. Acceptance Criteria

REQ-043 is complete when:

### DMR Scope

* ADS with DMR displays a recognizable scope view.
* Scope is visually centered.
* Dedicated DMR reticle appears.
* Normal hip-fire reticle disappears while scoped.
* Peripheral area becomes appropriately darkened.
* Scope presentation feels like a low-power optic rather than a high-power sniper scope.

### Transition

* Scope fades in with ADS.
* Scope fades out when ADS ends.
* Rapid ADS input does not cause flashing.
* Interrupted transitions smoothly reverse.
* Scope appearance remains synchronized with camera zoom.

### Aiming

* Scope center corresponds to actual shot direction.
* Shots land where the center reticle indicates.
* Existing DMR magnification remains functional.
* ADS sensitivity remains functional.

### State Handling

* Sprint removes scope when ADS is cancelled.
* Wall-running removes scope when ADS is cancelled.
* Weapon switching removes DMR scope.
* Death removes scope.
* Respawn does not retain scope.
* Pause/resume does not leave scope stuck.

### Other Weapons

* Pistol does not display DMR scope.
* AK does not display DMR scope.
* Shotgun does not display DMR scope.
* Their existing ADS behavior remains intact.

### Architecture

* Scope style is configurable independently of magnification.
* DMR visual assets can be replaced without code changes.
* Future sniper can use a different scope appearance.
* Future sniper can eventually support multiple zoom levels without redesigning the DMR system.

---

# 47. Testing Checklist

Test:

1. Equip DMR.
2. Hip-fire view.
3. Enter ADS.
4. Verify scope appears.
5. Verify DMR magnification still works.
6. Verify dedicated reticle is centered.
7. Fire at close-range target.
8. Fire at medium-range target.
9. Fire at long-range target.
10. Confirm shot alignment.
11. Release ADS.
12. Rapidly press/release ADS.
13. Reverse ADS halfway through transition.
14. ADS with mouse.
15. ADS with controller.
16. Walk while scoped.
17. Crouch while scoped.
18. Prone while scoped.
19. Shoot repeatedly while scoped.
20. Reload while scoped / verify existing ADS rules.
21. Start sprinting.
22. Verify scope exits.
23. Enter wall run from ADS.
24. Verify scope exits.
25. Switch DMR → pistol while scoped.
26. Switch DMR → AK while scoped.
27. Switch DMR → shotgun while scoped.
28. Verify DMR graphics do not remain.
29. Die while scoped.
30. Respawn.
31. Pause while scoped.
32. Resume.
33. Resize/window the game if practical.
34. Verify scope remains centered and circular.
35. Test with a second multiplayer player.
36. Verify remote player's screen is unaffected by another player's scope usage.

---

# 48. Future Improvements Not Required for REQ-043

Possible future optic improvements:

* Custom final DMR scope artwork
* Illuminated reticle
* Reticle brightness setting
* Scope lens glare
* Scope dirt/scratches
* Peripheral blur
* Chromatic aberration near lens edge
* Scope sway
* Breath/steady-aim mechanic
* Picture-in-picture scopes
* Scope glint visible to enemies
* Range markings
* Bullet-drop markings
* Rangefinder
* Variable magnification
* Sniper 2x/4x zoom modes
* Different optic attachments

These are outside the immediate scope.

The purpose of REQ-043 is to turn the existing functional DMR zoom into a convincing **low-power scoped ADS experience** while establishing reusable scope-presentation architecture for future weapons.
