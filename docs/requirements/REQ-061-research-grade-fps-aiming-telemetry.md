````markdown
# REQ-061 — Research-Grade FPS Aiming Telemetry

## Summary

Expand the existing telemetry system so Bullseye can support future research on FPS aiming behavior, particularly causal analysis of how players adjust their aim when the optimal target—the bullseye—is located away from conventional FPS aiming locations such as the head or center torso.

The telemetry system must capture enough information to reconstruct:

1. Where the player's crosshair was when an engagement began.
2. Where the opponent's bullseye was located.
3. How the player's aim moved toward the opponent.
4. How much correction was required before firing.
5. Where each shot was aimed and where it landed.
6. Whether the shot hit the bullseye, another body region, or missed.
7. How these outcomes differ by weapon, distance, input device, movement state, and bullseye position.
8. Randomized experimental conditions assigned by the game.

This ticket should extend the existing telemetry architecture rather than replace it.

---

# Goals

The telemetry system should allow future analysis of questions such as:

> What is the causal effect of moving the optimal aiming location away from conventional FPS aiming points on target acquisition, aim correction, accuracy, and time-to-kill?

It should also support secondary analyses such as:

- Mouse/keyboard versus controller aiming behavior.
- Differences across weapon classes.
- Effect of target distance on aim correction.
- Effect of player and target movement.
- First-shot accuracy.
- Reaction time.
- Aim-path efficiency.
- Bullseye hit probability.
- Whether players initially aim toward the head/torso before correcting toward the bullseye.

The system should be designed so that additional research questions can be studied later without requiring a complete telemetry rewrite.

---

# Important Research / Privacy Constraint

This system must be designed with future IRB-reviewed human-subjects research in mind.

Do **not** require personally identifying information for research telemetry.

Research data should use a generated anonymous/pseudonymous `ResearchParticipantId`.

Do not store the following in the research telemetry dataset unless explicitly added in a future approved requirement:

- Player real name
- Email address
- Steam display name
- Raw Steam ID
- IP address
- Voice chat
- Text chat
- Exact geographic location

If account linkage is eventually required for withdrawal or longitudinal analysis, the identifying/account linkage should be stored separately from the analytical telemetry dataset.

The telemetry architecture should also support a future boolean such as:

`ResearchConsentActive`

so rich research telemetry can be collected only from players who have opted into an approved study.

For development/testing, the research telemetry system may operate locally with test IDs before the consent system exists.

---

# 1. Create a Research Engagement Concept

Add the concept of an `Engagement`.

An engagement represents a period in which one player is actively attempting to aim at or attack another player.

Each engagement should receive a unique:

`EngagementId`

An engagement should attempt to begin when the local player meaningfully acquires another player as a potential target.

The implementation should avoid requiring perfect semantic knowledge of player intent.

A practical initial definition may be:

- Another living player enters an configurable angular region around the center of the attacker's screen/crosshair.
- The target is reasonably visible / unobstructed.
- The target remains within this region for a configurable minimum duration.

Suggested configurable values:

- Engagement acquisition cone: approximately 10–20 degrees.
- Minimum acquisition duration: approximately 100–200 ms.
- Engagement timeout after losing the target: approximately 1–2 seconds.

These values must be Inspector-configurable.

Do not hard-code them.

---

# 2. Engagement Start Event

When a new engagement begins, record an:

`EngagementStarted`

event.

At minimum capture:

- `ResearchParticipantId`
- `MatchId`
- `EngagementId`
- `AttackerPlayerId`
- `TargetPlayerId`
- `Timestamp`
- `MatchTime`
- `WeaponId`
- `WeaponClass`
- `InputMethod`
- `AttackerPosition`
- `TargetPosition`
- `DistanceToTarget`
- `AttackerMovementState`
- `TargetMovementState`
- `AttackerVelocity`
- `TargetVelocity`
- `IsAttackerGrounded`
- `IsTargetGrounded`
- `AttackerStance`
- `TargetStance`
- `AttackerIsADS`
- `TargetBullseyeAttached`
- `TargetBullseyeWorldPosition`
- `TargetBullseyeLocalPosition`
- `TargetBullseyeBodyRegion`
- `TargetBullseyeSurfaceNormal`
- `CrosshairWorldDirection`
- `CrosshairScreenPosition`
- `AngularErrorToBullseye`
- `AngularErrorToHead`
- `AngularErrorToCenterMass`
- `LineOfSightToTarget`
- `ExperimentalConditionId`, if applicable

Where practical, values should represent the state at the precise moment the engagement begins.

---

# 3. Bullseye Position Metrics

The bullseye system is central to the planned research.

For every engagement, capture where the target's bullseye is located.

Create standardized body-region classifications such as:

- Head
- UpperTorso
- LowerTorso
- LeftArm
- RightArm
- LeftLeg
- RightLeg
- Other

Do not rely only on the region label.

Also capture:

- World-space position.
- Local-space position relative to the target.
- Surface normal.
- Distance from center mass.
- Angular displacement from center mass from the attacker's perspective.
- Angular displacement from the head from the attacker's perspective.

Suggested fields:

`BullseyeDistanceFromCenterMass`

`BullseyeAngularDisplacementFromCenterMass`

`BullseyeAngularDisplacementFromHead`

These variables are particularly important for future causal analysis.

---

# 4. Aim Trajectory Sampling

We need to reconstruct how the player's aim moves during an engagement.

Do **not** record the camera/crosshair continuously throughout the entire match at maximum frame rate.

Instead, capture high-frequency aim telemetry only during relevant engagement windows.

When an engagement begins, start an `AimTrajectorySample` stream.

Sample at a configurable rate.

Default target:

**30 Hz**

Allow alternatives such as:

- 20 Hz
- 30 Hz
- 60 Hz

through configuration.

Each sample should contain:

- `MatchId`
- `EngagementId`
- `Timestamp`
- `MillisecondsSinceEngagementStart`
- `CrosshairWorldDirection`
- `CameraWorldPosition`
- `CameraForwardVector`
- `CrosshairScreenPosition`
- `AngularErrorToBullseye`
- `AngularErrorToHead`
- `AngularErrorToCenterMass`
- `TargetBullseyeWorldPosition`
- `DistanceToTarget`
- `AttackerVelocity`
- `TargetVelocity`
- `AttackerMovementState`
- `TargetMovementState`
- `IsADS`
- `CurrentZoomLevel`
- `WeaponId`

This data should make it possible to reconstruct the approximate path of the player's reticle over time.

---

# 5. Aim Trajectory Window

Aim trajectory capture should begin when an engagement is acquired.

It should continue until one of the following occurs:

- The target is eliminated.
- The attacker is eliminated.
- The target has been lost for the configured timeout.
- A new target clearly replaces the previous target.
- A configurable maximum engagement duration is reached.

Aim sampling should therefore be event-triggered rather than constantly recording throughout the match.

---

# 6. First-Aim Direction / Initial Aim Bias

Capture metrics that allow us to determine where the player initially moves their crosshair after acquiring the target.

For each engagement, derive or store:

- Crosshair position at engagement start.
- Direction of the first meaningful aim movement.
- Crosshair position after 100 ms.
- Crosshair position after 150 ms.
- Crosshair position after 250 ms.
- Angular distance moved toward bullseye.
- Angular distance moved toward head.
- Angular distance moved toward center mass.

If feasible, calculate:

`InitialAimDirection`

and:

`InitialAimTargetPreference`

Possible classifications:

- TowardBullseye
- TowardHead
- TowardCenterMass
- AwayFromTarget
- Indeterminate

However, preserve the underlying continuous measurements so researchers are not dependent on these classifications.

---

# 7. Trigger Pull / Shot Fired Telemetry

Extend the existing shot telemetry.

For every shot fired, capture:

- `ShotId`
- `MatchId`
- `EngagementId`
- `ResearchParticipantId`
- `Timestamp`
- `MillisecondsSinceEngagementStart`
- `WeaponId`
- `WeaponClass`
- `FireMode`
- `InputMethod`
- `AttackerPosition`
- `TargetPosition`
- `DistanceToTarget`
- `CrosshairWorldDirection`
- `CrosshairScreenPosition`
- `AttackerMovementState`
- `TargetMovementState`
- `AttackerVelocity`
- `TargetVelocity`
- `AttackerStance`
- `TargetStance`
- `IsADS`
- `ZoomLevel`
- `BullseyeWorldPosition`
- `BullseyeBodyRegion`
- `AngularErrorToBullseyeAtTrigger`
- `AngularErrorToHeadAtTrigger`
- `AngularErrorToCenterMassAtTrigger`
- `TimeSincePreviousShot`
- `ShotNumberWithinEngagement`

---

# 8. Shot Impact Telemetry

When the projectile or hitscan shot resolves, associate the impact with the corresponding `ShotId`.

Capture:

- `ShotId`
- `EngagementId`
- `ImpactWorldPosition`
- `HitPlayer`
- `HitTargetPlayer`
- `HitBullseye`
- `HitBody`
- `HitEnvironment`
- `MissedCompletely`
- `HitBodyRegion`
- `DamageDealt`
- `WasLethal`
- `TargetBullseyeAttached`
- `DistanceFromImpactToBullseye`
- `DistanceFromImpactToCenterMass`
- `DistanceFromImpactToHead`

For projectile weapons such as the bazooka, preserve the same conceptual telemetry but allow projectile travel time to separate trigger and impact events.

---

# 9. First-Shot Metrics

The first shot of an engagement is especially important.

Identify:

`IsFirstShotOfEngagement`

For each engagement, make it possible to derive:

- Time from engagement start to first shot.
- First-shot accuracy.
- First-shot bullseye accuracy.
- First-shot body-region hit.
- Angular error at first shot.
- Aim travel before first shot.
- Aim corrections before first shot.

Suggested derived field:

`TimeToFirstShotMs`

---

# 10. Aim Path Metrics

Do not rely only on final accuracy.

Capture enough trajectory data to calculate:

### Total Aim Travel

Total angular distance traveled by the reticle before firing.

### Direct Aim Distance

Shortest angular path from the starting reticle location to the bullseye.

### Aim Path Efficiency

Suggested future calculation:

`DirectAimDistance / TotalAimTravel`

A value closer to 1 represents a more direct aim path.

### Overshoot

Whether the reticle passes the bullseye and then reverses direction.

### Number of Corrections

Count significant direction reversals or corrective movements.

Do not aggressively bake these algorithms into the raw telemetry layer.

Prefer storing raw aim samples and optionally calculating derived metrics separately.

---

# 11. Target Acquisition Time

Support calculation of target acquisition latency.

Possible timestamps:

- `TargetEnteredAcquisitionConeTime`
- `EngagementStartedTime`
- `FirstAimMovementTime`
- `FirstShotTime`
- `FirstHitTime`
- `FirstBullseyeHitTime`
- `EliminationTime`

This should allow metrics including:

- Time to acquire target.
- Time to first shot.
- Time to first hit.
- Time to bullseye hit.
- Time to elimination.

---

# 12. Time-to-Kill

For each engagement that results in the target's elimination, record or calculate:

`TimeToKillMs`

Measure from:

`EngagementStarted`

to:

`TargetEliminated`

Also capture:

- Number of shots fired.
- Number of hits.
- Number of bullseye hits.
- Number of body hits.
- Weapon used for elimination.

---

# 13. Experimental Condition Support

Build generic support for randomized research conditions.

Create:

`ExperimentalConditionId`

and, if useful:

`ExperimentId`

Do not hard-code a single experiment into the telemetry architecture.

Example:

`ExperimentId = BullseyeDisplacementStudy`

`ExperimentalConditionId = RandomizedPeripheralBullseye`

The telemetry system should record the assigned condition with all relevant engagement events.

---

# 14. Randomized Bullseye Position Support

Prepare the system for a future experiment in which bullseye starting position or movement behavior can be randomized.

The telemetry system should be capable of recording:

- `BullseyeAssignmentId`
- `BullseyeAssignmentRandomSeed`
- `BullseyeAssignedBodyRegion`
- `BullseyeAssignedLocalPosition`
- `BullseyeActualLocalPosition`
- `BullseyeAssignmentTimestamp`
- `BullseyeAssignmentReason`

Possible reasons:

- Spawn
- Respawn
- NaturalCrawl
- ExperimentalRandomization
- ReturnedAfterDetach

Do not change normal bullseye gameplay behavior in this ticket unless necessary.

This ticket primarily needs to ensure telemetry can support future randomized experiments.

---

# 15. Preserve Random Assignment

If experimental randomization is later activated, the assignment must occur independently of:

- Player skill.
- Current score.
- Input method.
- Weapon.
- Accuracy.
- Player identity.
- Whether the player is winning or losing.

The telemetry should preserve enough information to verify that randomization occurred correctly.

If a random seed or assignment number is available, capture it.

---

# 16. Input Device

Record input method at the engagement level and shot level.

At minimum:

- MouseKeyboard
- Gamepad
- Unknown

If the player changes input devices mid-match, telemetry should reflect the device actually being used during the engagement.

Do not assume the device selected at match start remains constant.

---

# 17. Controller / Aim Assist Variables

Because future research may compare controller and mouse behavior, capture relevant aiming-assistance state.

When available:

- `AimAssistEnabled`
- `AimAssistStrength`
- `AimMagnetismActive`
- `ReticleFrictionActive`
- `SensitivityHorizontal`
- `SensitivityVertical`
- `ADSModifier`
- `DeadzoneSetting`

Do not collect hardware serial numbers or other unnecessary identifiers.

---

# 18. Weapon Context

Every engagement and shot should identify the current weapon.

Capture:

- Weapon identifier.
- Weapon class.
- Hitscan/projectile classification.
- Current zoom level.
- ADS state.
- Fire mode.
- Relevant spread/bloom value.
- Current recoil state if available.

Weapon classes should support at minimum:

- Short
- Long
- Heavy

and specific weapon IDs such as:

- Pistol
- AK
- DMR
- Shotgun
- Sniper
- Bazooka

Do not make analysis depend on display names that may later change.

Use stable internal IDs.

---

# 19. Distance

Distance is critical.

Record attacker-to-target distance:

- At engagement start.
- At every shot.
- At impact when relevant.

Prefer meters or Unity world units converted consistently to meters if the project uses a known scale.

Use one standard throughout telemetry.

---

# 20. Movement Context

Record both attacker and target movement.

Suggested categories:

- Stationary
- Walking
- Sprinting
- Crouching
- Prone
- Jumping
- Falling
- Sliding
- DolphinDiving
- WallRunning
- Other

Also retain velocity vectors so future analysis does not depend solely on categorical labels.

---

# 21. Bullseye Attached vs Detached

Existing telemetry already distinguishes attached/detached bullseye interactions.

Ensure research telemetry captures:

`BullseyeState`

Suggested values:

- Attached
- Detached
- Returning
- TemporarilyUnavailable

Aim research should generally be able to exclude engagements where the target does not currently have a normally functioning attached bullseye.

---

# 22. Engagement End Event

Record:

`EngagementEnded`

Capture:

- `EngagementId`
- `Timestamp`
- `DurationMs`
- `EndReason`
- `ShotsFired`
- `Hits`
- `BullseyeHits`
- `BodyHits`
- `TargetEliminated`
- `AttackerEliminated`
- `FinalDistance`
- `TimeToFirstShotMs`
- `TimeToFirstHitMs`
- `TimeToKillMs`, if applicable

Suggested `EndReason` values:

- TargetEliminated
- AttackerEliminated
- TargetLost
- TargetSwitched
- MatchEnded
- Timeout

---

# 23. Avoid Double-Counting Engagements

Engagement creation needs safeguards against rapidly opening and closing new engagements every few frames.

Use configurable acquisition and loss thresholds.

An opponent briefly leaving the acquisition cone should not automatically generate an entirely new engagement if the same fight continues immediately.

Prefer a short grace period before closing an engagement.

---

# 24. Multiple Enemies

The system must behave reasonably when multiple enemies are visible.

Do not automatically generate several overlapping high-frequency aim streams unless required.

Prefer identifying a primary engagement target based on something like:

1. Closest target to crosshair.
2. Visibility.
3. Stable target acquisition.
4. Recent firing target.

Document whichever logic is implemented.

The architecture should allow this logic to improve later.

---

# 25. Performance Requirements

Telemetry must not noticeably affect gameplay performance.

Requirements:

- Avoid allocating garbage every frame.
- Avoid synchronous disk/network writes during combat.
- Buffer telemetry.
- Batch writes where practical.
- Do not perform expensive scene searches for every sample.
- Cache references.
- Avoid serialization work on the main gameplay path where possible.
- Aim sampling frequency must be configurable.

If telemetry cannot keep up, gameplay performance takes priority.

Dropped telemetry should be logged/countable rather than causing gameplay hitching.

---

# 26. Local Development Output

Before any production research backend exists, allow telemetry to be written locally for validation.

Suggested format:

JSONL, CSV, or another structured format appropriate to the existing telemetry system.

Prefer separate logical datasets/events such as:

- Matches
- Engagements
- AimSamples
- Shots
- Impacts
- Eliminations
- ExperimentalAssignments

Do not force every high-frequency aim sample into one giant match-level JSON object.

---

# 27. Schema Versioning

Add:

`TelemetrySchemaVersion`

to exported research data.

Example:

`TelemetrySchemaVersion = "1.0"`

Any future breaking change to variable definitions should increment the schema version.

This is important so research datasets collected from different game builds can be interpreted correctly.

---

# 28. Game Build Version

Every telemetry record or parent record should be traceable to:

- Game build/version.
- Telemetry schema version.
- Match version/configuration.

Suggested fields:

`GameVersion`

`TelemetrySchemaVersion`

`GameplayConfigVersion`

This is important because gameplay balancing changes could otherwise confound later analysis.

---

# 29. Clock / Timestamp Consistency

Use a consistent high-resolution match-relative clock for behavioral measurements.

For example:

`MatchTimeMs`

Do not depend entirely on wall-clock timestamps for reaction-time analysis.

For events occurring within an engagement, preferably retain:

`MillisecondsSinceEngagementStart`

This avoids precision and synchronization problems.

---

# 30. Derived Metrics Layer

Do not pollute the raw event layer with excessive calculated research metrics.

Architecture should conceptually separate:

### Raw telemetry

What actually happened.

### Derived analytics

Metrics calculated from the raw events.

Examples of derived metrics:

- Reaction time.
- Aim-path efficiency.
- Number of corrections.
- Overshoot.
- First-shot angular error.
- Bullseye displacement.
- Time-to-kill.
- Accuracy.

Raw data should remain available so these definitions can be changed later.

---

# 31. Debug Visualization

Add an optional development-only visualization or debug mode to validate that the telemetry interpretation matches gameplay.

Useful debugging displays may include:

- Current engagement target.
- Engagement ID.
- Crosshair-to-bullseye angular error.
- Crosshair-to-head angular error.
- Crosshair-to-center-mass angular error.
- Engagement timer.
- Current aim sampling status.
- Bullseye body-region classification.

This must be disabled in normal builds unless explicitly enabled.

---

# 32. Telemetry Validation Tool

Create a simple way to inspect one completed engagement.

For a selected engagement, developers should be able to verify:

- Engagement start.
- Starting crosshair direction.
- Bullseye position.
- Aim samples.
- Shots.
- Impacts.
- Engagement end.

If practical, create a small debug output summarizing:

```text
Engagement: 00142
Target: Player 2
Distance: 18.3m
Bullseye Region: LeftTorso
Bullseye Displacement from Center Mass: 12.4°
Time to First Shot: 487ms
First Shot Error: 2.8°
Shots: 4
Hits: 3
Bullseye Hits: 1
Time to Kill: 1,940ms
```

This will make telemetry QA substantially easier.

---

# 33. Compatibility with Existing REQ-055 Telemetry

Do not remove or break telemetry already implemented under REQ-055.

Existing metrics such as:

- Eliminations
- Weapon-specific eliminations
- Attached/detached bullseye kills
- Grenade detach events
- Body-slam eliminations
- Assists
- Kill distance

should continue functioning.

REQ-061 adds a more granular **research behavioral telemetry layer** around those existing match statistics.

Reuse existing IDs/events when appropriate rather than producing duplicate systems.

---

# 34. Suggested Event Architecture

A preferred conceptual event structure is:

```text
MatchStarted
    ↓
ExperimentalAssignment (optional)
    ↓
EngagementStarted
    ↓
AimTrajectorySample
AimTrajectorySample
AimTrajectorySample
    ↓
ShotFired
    ↓
ShotImpact
    ↓
AimTrajectorySample
    ↓
ShotFired
    ↓
ShotImpact
    ↓
Elimination
    ↓
EngagementEnded
    ↓
MatchEnded
```

Do not treat this exact implementation as mandatory if the existing telemetry architecture supports the same information more cleanly.

---

# 35. Example Research Observation

The completed telemetry should allow us to reconstruct an observation similar to:

```text
Player:
ResearchParticipantId = R1048

Input:
Controller

Weapon:
DMR

Engagement:
EngagementId = E29913

Distance:
24.2 meters

Bullseye:
Right lower torso

Bullseye displacement from center mass:
14.8 degrees

Crosshair at engagement start:
5.1 degrees above center mass

First 150ms aim direction:
Toward head

Correction:
Player redirected toward bullseye after 210ms

Time to first shot:
612ms

Angular error at first shot:
3.2 degrees

First shot:
Miss

Second shot:
Body hit

Third shot:
Bullseye hit

Time to kill:
1.84 seconds
```

If the telemetry cannot reconstruct this type of encounter, then the implementation is incomplete.

---

# Acceptance Criteria

REQ-061 is complete when:

- [ ] Each combat engagement can receive a stable unique `EngagementId`.
- [ ] Engagement start and end events are recorded.
- [ ] Bullseye position is captured at engagement start.
- [ ] Bullseye body region is captured.
- [ ] Bullseye displacement from head and center mass can be measured.
- [ ] Crosshair/aim direction is sampled during active engagements.
- [ ] Aim sampling frequency is configurable.
- [ ] Aim telemetry is not continuously collected at high frequency outside engagements.
- [ ] First-shot timing can be calculated.
- [ ] Crosshair error relative to bullseye can be calculated at trigger pull.
- [ ] Crosshair error relative to head can be calculated.
- [ ] Crosshair error relative to center mass can be calculated.
- [ ] Each shot has a unique `ShotId`.
- [ ] Shot events can be linked to impact events.
- [ ] Shots can be linked to the engagement in which they occurred.
- [ ] Hit region is recorded.
- [ ] Bullseye hits can be distinguished from regular body hits.
- [ ] Misses can be distinguished from environment hits.
- [ ] Attacker/target distance is captured.
- [ ] Attacker movement state is captured.
- [ ] Target movement state is captured.
- [ ] Input method is captured.
- [ ] ADS/zoom state is captured.
- [ ] Weapon identity is captured using stable IDs.
- [ ] Controller aim-assistance state can be captured where applicable.
- [ ] Experimental condition IDs are supported.
- [ ] Future randomized bullseye assignments can be recorded.
- [ ] Raw telemetry and derived research metrics remain conceptually separate.
- [ ] Telemetry contains schema and game-build versions.
- [ ] Research telemetry uses pseudonymous IDs rather than unnecessary identity information.
- [ ] Existing REQ-055 telemetry remains functional.
- [ ] Telemetry buffering does not create noticeable gameplay hitching.
- [ ] At least one completed engagement can be inspected and reconstructed during QA.

---

# Out of Scope

Do **not** implement the following as part of REQ-061 unless required for architectural preparation:

- IRB consent UI.
- Steam research enrollment.
- Participant compensation.
- Demographic surveys.
- Cloud research database.
- Public research dashboard.
- Statistical analysis.
- Causal inference models.
- Full randomized experiment activation.
- Automatic publication/export of research datasets.
- Collection of personally identifying information.
- Collection of voice or text communication.

Those should be handled in later requirements.

---

# Future Requirement Hooks

REQ-061 should leave clean hooks for future tickets covering:

### Research Consent
Opt-in consent and withdrawal system.

### Experimental Assignment
Server-authoritative randomized bullseye-location experiments.

### Participant Survey
Demographics, FPS experience, controller history, sensitivity preferences, and gaming experience.

### Research Data Export
De-identified analysis-ready datasets.

### Longitudinal Player Research
Tracking learning/adaptation across multiple sessions while preserving pseudonymity.

---

# Design Principle

The main principle of this requirement is:

> **Capture the behavioral primitives now; derive the research metrics later.**

Do not attempt to guess every statistic we will eventually analyze.

Instead, preserve enough high-quality event, timing, aim, target, bullseye, weapon, movement, and experimental-assignment information that future analyses can reconstruct what happened during an engagement without modifying the game retroactively.
````
