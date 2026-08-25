# REQ-031 — Bullseye Position Body HUD

## Summary

Create a HUD element in the **top-right corner of the player's screen** that displays a simplified representation of the player's body and shows the current location of that player's bullseye.

The purpose of this HUD element is to give the player tactical awareness of their own vulnerability.

Because the bullseye moves around the player's body, the player should be able to glance at the HUD and understand approximately where their bullseye is located without needing to see their character from third person.

For example:

* Bullseye near the player's chest → HUD marker appears on the chest.
* Bullseye near the player's back → HUD indicates that the bullseye is on the rear side.
* Bullseye near the head → HUD marker appears near the head.
* Bullseye near the lower body → HUD marker appears near the lower portion of the body.

This system should work with the project's **current capsule player representation**, but it must be designed so that the visualization can later be adapted to the final humanoid player model.

The body diagram does **not** need to reproduce the player's actual animation or movement.

It represents the player's body spatially, not their current pose.

---

# Gameplay Purpose

The Bullseye mechanic is intended to influence how players move and fight.

Knowing the location of one's own bullseye creates tactical decisions.

For example:

* If the bullseye is on the player's left side, the player may choose to keep that side away from an opponent.
* If the bullseye has moved toward the front of the player, pushing directly toward an enemy may be riskier.
* If the bullseye is currently on the back, the player may be more comfortable engaging opponents in front of them.
* Turning to protect the bullseye may itself influence the bullseye's movement because of the existing delayed-turn mechanic.

The HUD should therefore provide useful information without giving the player perfect third-person awareness.

---

# 1. HUD Placement

Add the Bullseye Body HUD to the:

**Top-right corner of the gameplay HUD**

It should remain visible during normal gameplay.

The element should not obstruct:

* Crosshair
* Health display
* Weapon information
* Kill/death statistics
* Other important HUD elements

Exact dimensions can be tuned later.

For the prototype, prioritize readability over final visual polish.

---

# 2. Body Diagram

Display a simplified representation of the player's body.

For the eventual game, this should resemble a humanoid silhouette.

The diagram does NOT need to:

* Animate
* Walk
* Run
* Jump
* Aim
* Match the player's current pose
* Rotate with every movement animation

It should function more like a **body map**.

Conceptually:

```text
      O
     /|\
     / \
```

with a bullseye marker placed somewhere on or around the figure.

The final art can replace this temporary diagram later.

---

# 3. Current Capsule Compatibility

The player is currently represented by a capsule rather than the final humanoid model.

REQ-031 must therefore work with the current capsule.

Do not hard-code the entire HUD system specifically around humanoid bones that do not yet exist.

Instead, derive the bullseye's location using its position relative to the player's body/root.

The implementation should be able to determine information such as:

* Vertical position on the body
* Horizontal/side position
* Front versus back

from the bullseye's current local position.

This can then be mapped onto the HUD diagram.

When the humanoid model is eventually introduced, the mapping can be refined without replacing the entire HUD system.

---

# 4. Bullseye HUD Marker

Display a small bullseye icon on the body diagram representing the player's **actual current bullseye location**.

The marker should update as the physical/networked bullseye moves.

The HUD marker must follow:

* Normal randomized bullseye movement
* Bullseye crawl
* Bullseye movement caused by player turning
* Any other existing bullseye-position behavior

The HUD should read the authoritative/current bullseye state rather than creating a separate simulated bullseye.

There should only be one actual bullseye position.

The world bullseye and HUD bullseye are two representations of the same state.

---

# 5. Local Player Only

The HUD must show the bullseye belonging to the **local player**.

Example:

Player 1's screen shows Player 1's bullseye location.

Player 2's screen shows Player 2's bullseye location.

Player 1 must not accidentally see Player 2's bullseye diagram because of network initialization order or shared UI references.

This requirement should follow the same local-player ownership principles used by other HUD elements.

---

# 6. Body-Space Mapping

Create a reusable method for translating the bullseye's 3D position into a simplified HUD body position.

Prefer using the bullseye's position in **player-local space** rather than world space.

Conceptually:

`World Bullseye Position`

→ `Player Local Position`

→ `Normalized Body Position`

→ `HUD Marker Position`

This is important because the player may be:

* Rotating
* Moving
* Jumping
* Standing at different locations in the world

The HUD should represent where the bullseye is **relative to the player's body**, regardless of where the player is in the map.

---

# 7. Vertical Mapping

The HUD should represent the approximate vertical location of the bullseye.

For the current capsule, normalize the bullseye's vertical position relative to the player's body height.

Example conceptual mapping:

* Upper area → head/upper body
* Upper-middle → chest
* Middle → torso
* Lower-middle → hips
* Lower area → legs/lower body

Do not require exact anatomical regions yet.

The important requirement is that moving the bullseye vertically on the player visibly moves the HUD marker vertically.

---

# 8. Left/Right Mapping

The HUD should also indicate which side of the player's body the bullseye occupies.

Example:

Bullseye moves toward player's left:

→ HUD marker moves toward the left side of the body diagram.

Bullseye moves toward player's right:

→ HUD marker moves toward the right side.

The mapping must use **player-local left/right**, not world-space east/west.

---

# 9. Front vs. Back Awareness

The system should distinguish whether the bullseye is located on the:

* Front
* Back

of the player's body.

This is tactically important.

A player should be able to determine whether an opponent directly in front of them currently has a clear opportunity to hit their bullseye.

---

# 10. Front/Back Visualization

For REQ-031, implement a clear method of communicating front versus back.

Preferred approach:

Display **two simplified body silhouettes**:

**FRONT** and **BACK**

Only the relevant silhouette needs to contain the active bullseye marker.

Example:

```text
FRONT        BACK

  O            O
 /|\          /|\
 / \          / \

 (●)
```

If the bullseye is currently on the player's front:

* Display the marker on FRONT.

If the bullseye is currently on the player's back:

* Display the marker on BACK.

This is preferred over attempting to represent the entire 3D body on one flat diagram.

---

# 11. Side Positions

The bullseye may sometimes lie near the boundary between front and back.

Do not cause the HUD marker to rapidly flicker between FRONT and BACK when the bullseye is positioned near the player's side.

Implement reasonable handling for these transition zones.

Possible approaches include:

* Small front/back hysteresis threshold
* Side classification
* Stable classification until the bullseye clearly crosses the boundary

Prefer a simple robust implementation rather than excessive complexity.

If useful, the system may classify the bullseye as:

* Front
* Back
* Left Side
* Right Side

However, a dedicated side silhouette is **not required** for REQ-031.

The front/back diagrams plus horizontal marker placement should be sufficient initially.

---

# 12. HUD Update Frequency

The marker should appear responsive to bullseye movement.

It does not necessarily need to update every rendering frame if that creates unnecessary network/UI work.

Because the physical bullseye position is already synchronized, the HUD should simply visualize the local representation of that position.

Do not introduce additional network traffic solely for the HUD if the required bullseye position/state already exists locally.

---

# 13. Bullseye Vulnerability State

The grenade system can cause the bullseye to become temporarily knocked off/dislodged from the player's body.

When this occurs, the Body HUD should clearly indicate that the player is in a vulnerable state.

The body HUD should **flash red** while the bullseye is dislodged.

This should be visually obvious enough that the player immediately notices it.

---

# 14. Vulnerability Flash Behavior

When the bullseye enters its grenade-induced dislodged/vulnerable state:

1. Detect the bullseye vulnerability state.
2. Begin flashing the Body HUD red.
3. Continue flashing while the bullseye remains vulnerable.
4. Stop flashing when the bullseye has returned to its normal attached state.
5. Restore the normal HUD appearance.

The flashing may affect:

* Body silhouette
* HUD border
* Background
* Bullseye marker
* A combination of these

Prefer flashing the HUD container/border and/or silhouette rather than making every element difficult to read.

The warning should attract attention without making the HUD unusable.

---

# 15. Do Not Duplicate Vulnerability State

The HUD must not independently determine whether the bullseye *should* be vulnerable.

It should read the actual state from the existing bullseye/grenade gameplay system.

Conceptually:

`Bullseye Gameplay State`

→ `Attached`

or

→ `Dislodged / Vulnerable`

The HUD only visualizes this state.

Do not create a separate HUD timer that could become desynchronized from the actual grenade mechanic.

---

# 16. Optional Vulnerability Label

If useful for prototype clarity, the HUD may temporarily display:

**VULNERABLE**

while the bullseye is dislodged.

This is optional.

The red flashing state is the required indicator.

A final version may communicate vulnerability entirely through visuals and sound.

---

# 17. HUD Bullseye Icon

Create an easily replaceable UI sprite slot for the bullseye marker.

The prototype may use:

* Existing bullseye imagery
* Simple circular placeholder
* Temporary UI graphic

Do not permanently generate the bullseye appearance procedurally if doing so would make replacing it with final art difficult.

Prefer an Inspector-assignable sprite.

---

# 18. Body Silhouette Assets

Similarly, the FRONT and BACK body diagrams should be easily replaceable.

Provide Inspector/reference slots such as:

* Front Body Sprite
* Back Body Sprite
* Bullseye Marker Sprite

Temporary prototype art is acceptable.

This is especially important because the current player capsule will later become a humanoid character.

The UI logic should not depend on a specific silhouette PNG.

---

# 19. Capsule-to-Humanoid Transition

REQ-031 must explicitly anticipate the future humanoid player model.

The first implementation may use capsule dimensions for normalization.

However, isolate those assumptions.

Prefer configurable/reference values such as:

* Body root
* Body height
* Body radius/width
* Bullseye movement surface reference

rather than scattering capsule-specific numbers throughout the HUD code.

When the humanoid character replaces the capsule, we should be able to update the mapping component rather than rebuilding the entire Body HUD.

---

# 20. Future Body Region Integration

REQ-029 established a player-statistics system intended to eventually track Bullseye-specific statistics.

REQ-031 should be designed with future body-region classification in mind.

The same general position information may eventually help identify:

* Head
* Upper torso
* Lower torso
* Arms
* Legs
* Front
* Back
* Bullseye

This could later support:

* Weighted hit scores
* Body-region statistics
* Bullseye-hit statistics
* Damage analysis
* End-of-match Bullseye scoring

Do NOT implement those statistics as part of REQ-031.

However, avoid writing completely separate and incompatible body-position logic exclusively for the HUD if a reusable body-region/position representation is practical.

---

# 21. UI Scaling

The Body HUD must remain positioned correctly at different screen resolutions and aspect ratios.

Anchor it to the **top-right corner** using the appropriate Unity UI anchoring system.

Test at minimum:

* Standard 16:9
* Different editor Game View resolutions

The HUD should not drift into the middle of the screen or become clipped.

---

# 22. Pause and Death Behavior

During normal gameplay:

* Body HUD visible.

During pause:

* It may remain behind the pause overlay or be hidden according to the existing HUD behavior.

During death/respawn countdown:

Prefer hiding or dimming the Body HUD because bullseye tactical positioning is not meaningful while the player is dead.

After respawn:

* Restore the Body HUD.
* Immediately show the respawned player's current bullseye location.

---

# 23. Multiplayer Architecture

No player should receive hidden information about another player's bullseye through this UI.

The HUD should bind only to the local player.

The other player's bullseye remains visible in the world according to existing gameplay rules, but their private Body HUD state should never appear on another player's screen.

---

# Suggested Prototype Layout

Top-right corner:

```text
┌─────────────────────────────┐
│        YOUR BULLSEYE        │
│                             │
│   FRONT           BACK      │
│                             │
│     O               O       │
│    /|\             /|\      │
│   / ● \           /   \     │
│     |               |       │
│    / \             / \      │
│                             │
└─────────────────────────────┘
```

The active marker should only appear on the appropriate view.

When vulnerable:

```text
┌─────────────────────────────┐
│     ⚠ VULNERABLE ⚠         │
│                             │
│     [HUD FLASHES RED]       │
│                             │
└─────────────────────────────┘
```

The exact visual design is temporary.

---

# Suggested Architecture

Prefer separating gameplay state from visualization.

Conceptually:

`BullseyeController`

Provides:

* Current bullseye transform/position
* Current attachment/vulnerability state

↓

`BullseyeBodyPositionMapper`

Calculates:

* Normalized height
* Normalized horizontal position
* Front/back classification

↓

`BullseyeBodyHUD`

Displays:

* Body silhouette(s)
* Bullseye marker
* Vulnerability warning

This keeps HUD code from controlling gameplay behavior.

---

# Multiplayer Test Scenarios

## Test 1 — Local Player HUD

1. Start a two-player multiplayer session.
2. Observe Player 1's screen.
3. Confirm the Body HUD represents Player 1's bullseye.
4. Observe Player 2's screen.
5. Confirm Player 2 sees Player 2's bullseye.
6. Confirm the HUDs are not accidentally swapped.

---

## Test 2 — Vertical Movement

1. Allow the bullseye to move vertically around the capsule.
2. Observe the Body HUD.
3. Confirm the marker moves vertically in the corresponding direction.

---

## Test 3 — Left/Right Movement

1. Move the bullseye around the left and right sides of the player.
2. Confirm the HUD marker shifts horizontally to reflect its approximate location.

---

## Test 4 — Front/Back

1. Position the bullseye on the player's front.
2. Confirm it appears on the FRONT diagram.
3. Allow it to move around to the player's back.
4. Confirm it appears on the BACK diagram.

---

## Test 5 — Player Rotation

1. Position the bullseye on the player's front.
2. Rotate the entire player 180 degrees.
3. Confirm the HUD still correctly treats the bullseye as being on the player's front.

The result must be based on player-local space rather than world-space orientation.

---

## Test 6 — Bullseye Random Movement

1. Allow normal bullseye movement to run.
2. Observe the Body HUD for an extended period.
3. Confirm the HUD marker continuously corresponds to the world bullseye's approximate location.

---

## Test 7 — Turn-Induced Bullseye Movement

1. Turn the player enough to trigger the existing bullseye drift behavior.
2. Confirm the physical bullseye moves.
3. Confirm the HUD marker reflects that movement.

---

## Test 8 — Grenade Vulnerability

1. Player 2 throws a grenade affecting Player 1's bullseye.
2. Cause Player 1's bullseye to become dislodged/vulnerable.
3. Confirm Player 1's Body HUD begins flashing red.
4. Confirm Player 2's Body HUD does NOT begin flashing unless Player 2 is also vulnerable.
5. Allow Player 1's bullseye to return to its normal state.
6. Confirm the red flashing stops immediately when vulnerability ends.

---

## Test 9 — Death and Respawn

1. Kill Player 1.
2. Confirm the Body HUD is hidden or appropriately inactive during the respawn countdown.
3. Respawn Player 1.
4. Confirm the Body HUD returns.
5. Confirm it shows the correct new/current bullseye position.

---

## Test 10 — Resolution Scaling

Test the HUD at several Game View resolutions.

Confirm:

* It remains anchored in the top-right.
* The marker remains inside the intended body-map area.
* The silhouettes remain readable.
* The HUD does not overlap critical UI unnecessarily.

---

# Acceptance Criteria

REQ-031 is complete when:

* [ ] A Bullseye Body HUD appears in the top-right corner during gameplay.
* [ ] The HUD represents the local player's body.
* [ ] The HUD displays the local player's current bullseye location.
* [ ] Bullseye vertical movement produces corresponding HUD movement.
* [ ] Bullseye left/right movement produces corresponding HUD movement.
* [ ] The HUD distinguishes front from back.
* [ ] Mapping is based on player-local space rather than world-space position.
* [ ] Rotating or moving the player does not incorrectly change body-relative classification.
* [ ] The HUD follows the actual existing bullseye rather than simulating a separate one.
* [ ] Random bullseye movement is reflected correctly.
* [ ] Turn-induced bullseye movement is reflected correctly.
* [ ] The HUD flashes red while the player's bullseye is grenade-displaced/vulnerable.
* [ ] The warning stops when the bullseye returns to its normal attached state.
* [ ] The HUD reads the existing gameplay vulnerability state rather than maintaining an independent timer.
* [ ] Player 1 sees only Player 1's Body HUD information.
* [ ] Player 2 sees only Player 2's Body HUD information.
* [ ] Body and bullseye UI sprites can be replaced easily.
* [ ] The implementation works with the current capsule.
* [ ] Capsule-specific assumptions are isolated so a humanoid character can replace the capsule later.
* [ ] The HUD remains correctly anchored at different resolutions.
* [ ] Existing bullseye, grenade, health, damage, networking, and respawn functionality continues to work.

---

# Out of Scope

The following are intentionally deferred:

* Animated body diagram
* Mirroring player locomotion animations
* Exact humanoid skeletal mapping
* Final character artwork
* Final HUD artwork
* Weighted body-region scoring
* Bullseye hit statistics
* Damage heat maps
* Enemy body HUDs
* Spectator HUD
* Persistent player statistics
* Post-match analytics
* Character customization
* Different silhouette art for different character models
* Showing exact enemy viewing angles

---

# Future Direction

The Bullseye Body HUD should eventually become one of the game's defining tactical interfaces.

A future version may combine:

* Current bullseye position
* Bullseye vulnerability state
* Body damage information
* Bullseye-hit statistics
* Weighted body-region scoring
* Temporary status effects
* Player-specific character silhouettes

The important distinction is that this is **not simply a health indicator**.

The Body HUD tells the player:

**"Where am I vulnerable right now?"**

That information should influence how the player turns, approaches enemies, retreats, uses cover, and responds to grenades.

REQ-031 establishes that system while remaining compatible with the current capsule prototype and the eventual transition to humanoid characters.
