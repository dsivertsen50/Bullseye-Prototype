````markdown
# REQ-059 — Expanded Controller Rumble and Gameplay Haptics

## Summary

Expand controller vibration/rumble feedback throughout gameplay.

Add haptic feedback for:

1. **Initiating sprint**
2. **Continuous sprinting / footsteps**
3. **Jumping**
4. **Landing**
5. **Nearby grenade explosions**

The goal is to make movement and nearby impacts feel more physical without making controller vibration distracting or exhausting.

This ticket should also establish a **centralized, reusable haptics system** so future gameplay actions can easily trigger controller rumble without duplicating vibration code across many scripts.

---

# Goals

1. Add subtle haptic feedback to major movement actions.
2. Create rhythmic vibration while sprinting that loosely corresponds to running footsteps.
3. Give jumps and landings physical feedback.
4. Make nearby grenade explosions feel impactful through distance-scaled vibration.
5. Ensure vibration affects only the appropriate local player's controller.
6. Avoid excessive or constant vibration.
7. Build a reusable haptics architecture for future weapons, damage, explosions, environmental effects, etc.

---

# Core Design Principle

Do NOT implement controller vibration independently inside every gameplay script.

Avoid architecture such as:

```text
PlayerMovement
    → directly controls gamepad motors

Grenade
    → directly controls gamepad motors

Weapon
    → directly controls gamepad motors
```

Instead create a centralized local-player haptics system.

Conceptually:

```text
Gameplay Event
     ↓
PlayerHapticsController
     ↓
Haptic Effect
     ↓
Local Gamepad
```

For example:

```text
Sprint Started
        ↓
PlayerHapticsController.PlaySprintStart()

Jump
        ↓
PlayerHapticsController.PlayJump()

Grenade Explosion
        ↓
PlayerHapticsController.PlayExplosion(distance)
```

This architecture should make additional rumble effects easy to add later.

---

# Suggested Component

Create a component similar to:

```text
PlayerHapticsController
```

Exact class name may vary if an existing vibration/controller-feedback system already exists.

Before adding another system, Cursor should inspect the project for any existing:

- controller vibration;
- gamepad motor control;
- Input System haptics;
- controller feedback utility;
- rumble settings.

Reuse and extend an existing clean system if one already exists.

---

# Local Player Only

Controller rumble is LOCAL feedback.

A player's controller should only vibrate in response to events that player should physically experience.

Examples:

```text
Player A starts sprinting
→ Player A's controller vibrates

Player B's controller
→ does NOT vibrate
```

For explosion effects:

```text
Grenade explodes near Player A
→ Player A receives rumble

Grenade is far from Player B
→ Player B receives little or no rumble
```

Do not network controller motor commands.

Network gameplay events normally, then let each local player's haptics system decide what feedback should occur.

---

# Input Device Requirement

Rumble should only be attempted when the local player is actively using a compatible gamepad/controller.

Keyboard and mouse gameplay must continue normally.

The absence of a compatible controller must never generate errors.

The system should safely handle:

- controller disconnected;
- controller reconnected;
- keyboard/mouse input;
- multiple controllers connected;
- controller switching during gameplay.

---

# Motor Support

Where supported, use separate low-frequency and high-frequency motor values.

Conceptually:

```text
Low Frequency Motor
→ deeper / heavier vibration

High Frequency Motor
→ lighter / sharper vibration
```

Different actions may use different combinations.

Exact values should be Inspector-configurable rather than hard-coded.

---

# 1. Sprint Initiation Rumble

When the player successfully ENTERS sprint:

Trigger a short, light vibration.

This should communicate:

```text
Sprint has begun.
```

Desired feel:

- noticeable;
- brief;
- light;
- not comparable to an explosion;
- should not repeat continuously just because the sprint button remains held.

Example conceptual configuration:

```text
SprintStartDuration = 0.10–0.18 sec

SprintStartLowMotor = light
SprintStartHighMotor = light/moderate
```

Exact tuning should remain editable.

---

# Sprint Start Trigger

Only trigger the initiation pulse when transitioning:

```text
Not Sprinting
      ↓
Sprinting
```

Do NOT repeatedly trigger it every frame while sprint input remains active.

Example:

```text
if (!wasSprinting && isSprinting)
{
    haptics.PlaySprintStart();
}
```

Equivalent architecture is acceptable.

---

# 2. Rhythmic Sprint Rumble

While the player is actively sprinting on the ground, provide a subtle rhythmic vibration intended to evoke running footsteps.

The effect should resemble:

```text
step
   step
      step
         step
```

rather than:

```text
BBBBBBBBBBBBBBBBBBBB
constant vibration
```

Each step should produce a small vibration pulse.

---

# Sprint Rhythm

The rhythm should approximately correspond to sprint cadence.

Preferred architecture:

```text
Sprint step event
        ↓
Haptic pulse
```

If the animation system already exposes reliable footstep events, use those.

If not, a configurable timed cadence is acceptable.

Example:

```text
SprintStepInterval = configurable
```

Do NOT redesign the animation system solely for this ticket.

---

# Sprint Step Rumble Feel

Each sprint-step pulse should be VERY LIGHT.

Conceptually:

```text
Footstep 1 → bump
Footstep 2 → bump
Footstep 3 → bump
```

The player should perceive the rhythm without having their controller buzz continuously.

Suggested starting feel:

```text
duration:
~0.04–0.08 seconds

strength:
low
```

Expose values in the Inspector.

---

# Optional Left/Right Alternation

If easy to implement cleanly, alternating the feel between successive steps is acceptable.

For example:

```text
Left step:
slightly more low-frequency vibration

Right step:
slightly more high-frequency vibration
```

However, this is OPTIONAL.

Do not overcomplicate REQ-059 merely to simulate individual feet.

A consistent rhythmic pulse is sufficient.

---

# Sprint Rumble Conditions

Sprint-step rumble should occur only when the player is actually sprinting appropriately.

Do NOT produce rhythmic sprint footsteps when:

- airborne;
- standing still;
- crouched;
- prone;
- sprint input is blocked;
- sprint has ended;
- player is dead;
- player is in a state where movement is disabled.

If wall-running uses a separate movement state, do not automatically treat it as normal sprinting.

Wall-run haptics can be added separately in a future ticket.

---

# 3. Jump Rumble

When the player successfully jumps, trigger a short light vibration pulse.

Desired feel:

```text
small physical push-off
```

It should be slightly noticeable but weaker than a meaningful landing or nearby explosion.

Suggested starting behavior:

```text
JumpDuration = ~0.08–0.15 sec

JumpLowMotor = light
JumpHighMotor = light
```

Trigger only when a jump actually occurs.

Do not trigger merely from pressing the jump input when jumping is unavailable.

---

# Jump + Sprint Interaction

If the player jumps while sprinting:

- stop/pause sprint-step vibration while airborne;
- trigger the Jump haptic;
- resume normal sprint rhythm only if sprinting resumes after landing.

Avoid overlapping effects creating a long uncontrolled vibration.

---

# 4. Landing Rumble

When an airborne player lands, trigger a short vibration.

Landing feedback should generally be stronger than jump feedback.

Desired feel:

```text
Jump:
small bump

Landing:
more substantial thump
```

---

# Landing Intensity

Landing strength should ideally scale based on the severity of the landing.

If the project already has access to fall velocity or fall distance, use it.

Conceptually:

```text
Small hop
→ light landing rumble

Normal jump
→ moderate-light rumble

Large fall
→ stronger rumble
```

Do NOT create a complex fall-damage system as part of this ticket.

If no reliable landing-severity value exists, use one configurable normal landing effect for now.

---

# Recommended Landing Scaling

If vertical impact velocity is available, normalize it.

Conceptually:

```text
LandingIntensity =
InverseLerp(
    MinimumLandingVelocity,
    MaximumLandingVelocity,
    abs(verticalVelocityBeforeLanding)
)
```

Then use that to scale vibration.

Clamp intensity so ordinary movement does not become excessively strong.

---

# Prevent False Landings

Do not trigger landing rumble continuously while grounded.

Trigger only on:

```text
Airborne
    ↓
Grounded
```

Avoid false landing events caused by:

- walking down small slopes;
- stairs;
- minor CharacterController ground-state flickering.

Use existing grounded-state logic where possible.

---

# 5. Nearby Grenade Explosion Rumble

When a grenade explodes near the local player, the player's controller should vibrate.

This applies at minimum to grenade explosions that produce an explosive/impact event appropriate for physical feedback.

The rumble should depend on DISTANCE.

---

# Explosion Distance Scaling

Conceptually:

```text
Grenade
   💥

Player very close
→ strong rumble

Player moderately close
→ medium rumble

Player near outer radius
→ weak rumble

Player far away
→ no rumble
```

Use world-space distance between:

```text
Explosion Position
```

and:

```text
Local Player Position
```

---

# Explosion Haptic Radius

Create a configurable value such as:

```text
ExplosionHapticRadius
```

This does NOT need to equal:

```text
damage radius
```

or:

```text
Bullseye displacement radius
```

Haptic perception may reasonably extend farther than gameplay damage.

Do not alter grenade gameplay radius as part of this ticket.

---

# Distance Formula

Something conceptually similar to:

```text
distance01 =
Clamp01(distance / ExplosionHapticRadius)

intensity =
1 - distance01
```

is acceptable.

Prefer an adjustable falloff curve if convenient:

```text
AnimationCurve ExplosionHapticFalloff
```

This would allow tuning such as:

```text
Close:
strong

Mid-range:
noticeable

Outer range:
quick fade
```

---

# Explosion Rumble Feel

A nearby explosion should feel substantially different from movement vibration.

Suggested structure:

```text
Immediate stronger impact
        ↓
brief fading rumble
```

For example:

```text
Impact phase:
short strong low-frequency hit

Decay phase:
weaker vibration fading out
```

Exact implementation may use a haptic envelope rather than one constant vibration.

---

# Example Explosion Envelope

Conceptually:

```text
0.00 sec → strong
0.10 sec → moderate
0.25 sec → weak
0.40 sec → off
```

Scaled by player distance.

Do not treat these exact values as mandatory.

They are starting points for tuning.

---

# Grenade Ownership

The player should feel a nearby grenade explosion regardless of who threw the grenade.

Examples:

```text
Enemy grenade nearby
→ rumble

Own grenade nearby
→ rumble

Teammate grenade nearby
→ rumble
```

Haptic response is based on physical proximity, not ownership.

---

# Occlusion

REQ-059 does NOT require advanced explosion occlusion.

For now:

```text
distance
```

is sufficient.

A future system could reduce vibration if a major wall is between the explosion and player.

Do not add complex raycast-based acoustic/explosion simulation unless it already exists.

---

# Different Grenade Types

Inspect the current grenade architecture.

Do not blindly apply identical haptics to every throwable if some do not represent an explosive physical event.

At minimum, explosive grenade types should support haptics.

The architecture should allow different grenade definitions to specify:

```text
HapticIntensityMultiplier
HapticRadius
HapticProfile
```

in the future.

Do not hard-code the haptic system exclusively to one specific grenade prefab if grenade inheritance/definitions provide a cleaner hook.

---

# Haptic Profiles

Strongly consider representing effects through reusable profiles/configuration.

For example:

```text
SprintStart
SprintStep
Jump
Land
Explosion
```

Each profile could define:

```text
LowMotorStrength
HighMotorStrength
Duration
Attack
Decay
```

Exact architecture is flexible.

The important requirement is avoiding dozens of unrelated magic numbers.

---

# Haptic Priority / Overlap

Multiple haptic events can occur simultaneously.

Example:

```text
Player sprinting
        ↓
Player jumps
        ↓
Grenade explodes nearby
```

The system must handle this intentionally.

Do NOT let unrelated scripts constantly overwrite controller motor values.

---

# Priority

Suggested priority:

```text
Explosion / major impacts
        ↓
Landing
        ↓
Jump
        ↓
Sprint Start
        ↓
Sprint Steps
```

A higher-priority effect may temporarily override a weaker effect.

Example:

```text
Sprint step rumble occurring
        ↓
Grenade explodes
        ↓
Explosion rumble overrides step
        ↓
Explosion ends
        ↓
Sprint rhythm may resume if still sprinting
```

Exact implementation may use:

- priorities;
- channels;
- maximum motor value;
- effect blending.

Choose the simplest reliable approach.

---

# Haptic Blending

If multiple compatible effects occur simultaneously, it is acceptable to combine them.

However:

```text
motorStrength
```

must always be clamped to safe supported values.

Never allow stacking to exceed the controller API's expected range.

---

# Stopping Motors

This is CRITICAL.

Ensure controller motors reliably return to zero when:

- haptic effect ends;
- gameplay pauses;
- player dies;
- scene changes;
- match ends;
- controller disconnects;
- PlayerHapticsController disables;
- local player object despawns.

There must never be a bug where the controller continues vibrating indefinitely.

Implement a reliable:

```text
StopHaptics()
```

or equivalent.

---

# Pause Menu

When gameplay is paused:

```text
active gameplay rumble should stop
```

Do not allow sprint/explosion rumble to continue underneath a paused game.

REQ-059 does NOT require menu-navigation vibration.

That can be a separate enhancement.

---

# Death / Respawn

On player death:

- cancel sprint vibration;
- cancel movement vibration;
- cancel active gameplay effects where appropriate.

On respawn:

- haptics should begin from a clean state;
- no old effect should resume unexpectedly.

Death-specific vibration is NOT required by REQ-059.

---

# Settings Compatibility

If the project already has controller vibration settings, respect them.

If not, structure the system so a future setting can easily control:

```text
Controller Vibration:
ON / OFF
```

and eventually perhaps:

```text
Vibration Strength:
0–100%
```

REQ-059 does not necessarily require adding the settings UI now unless trivial.

However, avoid architecture that would make this difficult later.

---

# Recommended Global Strength

Include a central multiplier such as:

```text
MasterHapticStrength = 1.0
```

All effects should multiply through this value.

Conceptually:

```text
FinalStrength =
EffectStrength
*
MasterHapticStrength
```

This will make future vibration-strength settings easy to implement.

---

# Inspector Configuration

Expose useful tuning values.

For example:

```text
GENERAL

MasterHapticStrength

SPRINT START

SprintStartLowMotor
SprintStartHighMotor
SprintStartDuration

SPRINT STEPS

SprintStepLowMotor
SprintStepHighMotor
SprintStepDuration
SprintStepInterval

JUMP

JumpLowMotor
JumpHighMotor
JumpDuration

LAND

LandingMinStrength
LandingMaxStrength
LandingDuration
MinimumLandingVelocity
MaximumLandingVelocity

EXPLOSION

ExplosionHapticRadius
ExplosionLowMotorStrength
ExplosionHighMotorStrength
ExplosionDuration
ExplosionFalloff
```

Exact grouping may vary.

Avoid unnecessary hard-coded values.

---

# Suggested Starting Feel

These are NOT strict requirements.

They are intended as first-pass tuning guidance.

## Sprint Start

```text
Intensity:
Light

Duration:
~0.12 sec
```

---

## Sprint Step

```text
Intensity:
Very light

Duration:
~0.05 sec

Cadence:
roughly synchronized with sprint footsteps
```

---

## Jump

```text
Intensity:
Light

Duration:
~0.10 sec
```

---

## Landing

```text
Intensity:
Light → moderate depending on impact

Duration:
~0.10–0.20 sec
```

---

## Nearby Explosion

```text
Intensity:
Moderate → strong when close

Duration:
~0.25–0.50 sec

Distance-scaled
```

The hierarchy should approximately feel like:

```text
Sprint Step
<
Sprint Start
≈
Jump
<
Landing
<
Nearby Explosion
```

---

# Do Not Couple Haptics to Frame Rate

Timed effects should use duration/cadence logic rather than:

```text
every frame set random vibration
```

Rumble should behave consistently across different FPS values.

---

# Networking

Do NOT network motor values.

For local movement:

```text
Local movement event
→ local haptics
```

For replicated world events such as explosions:

```text
Server-authoritative grenade explodes
        ↓
Explosion occurs on clients
        ↓
Each local player calculates distance
        ↓
Local haptics system decides intensity
```

This keeps controller behavior local while preserving synchronization of the underlying gameplay event.

---

# Split-Screen / Multiple Local Players

The current game may not require split-screen.

Do not over-engineer REQ-059 around split-screen.

However, avoid unnecessarily assuming:

```text
Gamepad.current
```

is always the correct controller if the project's player input architecture already associates devices with individual players.

Prefer the controller associated with the local `PlayerInput` where available.

---

# Implementation Sequence

## Phase 1 — Inspect Existing Input/Haptics

Inspect:

- PlayerInput setup
- Input System
- controller/gamepad handling
- existing vibration code
- movement state code
- sprint state
- jump logic
- grounded/landing detection
- grenade explosion code
- networking paths

Do not create duplicate systems unnecessarily.

---

## Phase 2 — Create Central Haptics Controller

Implement centralized:

```text
PlayerHapticsController
```

or equivalent.

Verify a simple test pulse works on the local controller.

Verify motors reliably stop.

---

## Phase 3 — Sprint Start

Connect transition:

```text
not sprinting
→ sprinting
```

to a short light pulse.

---

## Phase 4 — Sprint Rhythm

Add subtle repeated step pulses while:

```text
sprinting && grounded
```

Prefer real footstep timing if already available.

Otherwise use configurable cadence.

---

## Phase 5 — Jump

Trigger light pulse on successful jump.

---

## Phase 6 — Landing

Detect:

```text
airborne
→ grounded
```

and trigger landing rumble.

Scale with landing severity if reliable vertical impact information already exists.

---

## Phase 7 — Grenade Explosions

Connect explosion events to the local haptics controller.

Calculate player distance.

Apply falloff.

Verify own/enemy grenades both work.

---

## Phase 8 — Priority

Test overlapping effects.

Confirm nearby explosion overrides or dominates sprint-step vibration.

---

## Phase 9 — Cleanup / Safety

Verify motors stop on:

- pause;
- death;
- despawn;
- scene transition;
- controller disconnect;
- component disable.

---

# Acceptance Criteria

REQ-059 is complete when ALL of the following are true.

## Architecture

- [ ] Central reusable haptics controller exists.
- [ ] Gameplay scripts request haptic effects rather than independently managing motors.
- [ ] Haptics operate only for the appropriate local player.
- [ ] Keyboard/mouse play functions normally without errors.
- [ ] Controller disconnect/reconnect does not create errors.

## Sprint

- [ ] Entering sprint produces one short light rumble.
- [ ] Holding sprint does not repeatedly retrigger the initiation effect.
- [ ] Sprinting produces subtle rhythmic step rumble.
- [ ] Step rumble is not constant buzzing.
- [ ] Sprint rhythm stops when player stops sprinting.
- [ ] Sprint rhythm stops while airborne.
- [ ] Sprint rhythm does not occur while crouched/prone/dead.

## Jump

- [ ] Successful jump produces a light pulse.
- [ ] Failed jump input does not trigger rumble.
- [ ] Jump feedback feels distinct from sprint-step vibration.

## Landing

- [ ] Landing produces a short rumble.
- [ ] Landing triggers only on a genuine airborne→grounded transition.
- [ ] Normal movement over small slopes does not constantly trigger it.
- [ ] Large landings can produce stronger rumble if impact scaling is implemented.

## Grenades

- [ ] Nearby grenade explosion produces controller rumble.
- [ ] Very close explosions produce stronger feedback.
- [ ] Mid-distance explosions produce weaker feedback.
- [ ] Distant explosions produce no feedback.
- [ ] Own grenade can trigger rumble.
- [ ] Enemy grenade can trigger rumble.
- [ ] Explosion haptics do not alter gameplay explosion/damage radius.

## Effect Interaction

- [ ] Explosion rumble can override/domininate sprint-step rumble.
- [ ] Jumping interrupts normal sprint-step cadence appropriately.
- [ ] Landing feedback occurs normally after jumping.
- [ ] Multiple events cannot drive motor strength outside valid limits.

## Safety

- [ ] Motors always stop after effects finish.
- [ ] Motors stop when game pauses.
- [ ] Motors stop when player dies.
- [ ] Motors stop when player despawns.
- [ ] Motors stop when the haptics component disables.
- [ ] Motors stop when controller disconnects.
- [ ] No indefinite vibration bug exists.

## Multiplayer

- [ ] Player A sprinting does not vibrate Player B's controller.
- [ ] Explosion feedback is calculated locally based on each player's distance.
- [ ] Controller motor values are not network replicated.
- [ ] Host/controller feedback works.
- [ ] Client/controller feedback works.

---

# Required Playtest

Use a physical gamepad.

## Sprint Test

1. Begin walking.
2. Initiate sprint.
3. Confirm one light initiation pulse.
4. Continue sprinting.
5. Confirm subtle rhythmic pulses resembling footsteps.
6. Stop sprinting.
7. Confirm rumble immediately stops.

---

## Jump Test

1. Stand still.
2. Jump.
3. Confirm small jump pulse.
4. Land.
5. Confirm slightly stronger landing pulse.

Repeat while sprinting.

Confirm sprint-step vibration pauses during airborne state.

---

## Landing Test

Test:

```text
small normal jump
```

and, if possible:

```text
larger fall
```

Confirm larger impact feels at least as strong as a normal landing.

---

## Grenade Distance Test

Trigger a grenade at:

```text
Very close distance
Medium distance
Near edge of haptic radius
Outside haptic radius
```

Expected:

```text
Very Close
→ strong

Medium
→ moderate

Outer
→ light

Outside
→ none
```

---

## Multiplayer Test

Start host + client.

1. Sprint on Player A.
2. Confirm only Player A's controller receives sprint haptics.
3. Place Player A near a grenade.
4. Place Player B farther away.
5. Detonate grenade.
6. Confirm Player A receives stronger vibration than Player B.
7. Move Player B outside haptic radius.
8. Detonate again.
9. Confirm Player B receives none.

---

# Future Extensibility

REQ-059 should make future effects straightforward.

Potential later haptics include:

```text
Weapon firing
Shotgun recoil
Sniper rifle recoil
Reload completion
Taking damage
Bullseye hit
Bullseye destroyed
Grenade throw
Magnetism pull
Body Slam
Wall Running
Dolphin Dive
Ricochet nearby
Bullet flyby
Menu navigation
Vehicle/environment effects
```

These are NOT requirements for REQ-059.

The centralized architecture should simply make them easy to add later.

---

# Final Expected Experience

Movement should gain subtle physical feedback:

```text
Begin Sprint
→ small pulse

Sprint
→ bump... bump... bump... bump...

Jump
→ small pulse

Land
→ thump
```

Nearby explosives should have much greater presence:

```text
Grenade detonates nearby
        ↓
Controller receives distance-scaled impact
        ↓
brief rumble fades
```

The final system should make the controller feel more connected to the player's movement and environment without turning normal gameplay into continuous vibration.
````
