# REQ-036 — Animation Asset Update

## Newly Available Animations

The following animation assets have now been added and should be incorporated into the REQ-036 locomotion Animator:

### Prone

* `Prone Left Turn.fbx`
* `Prone Right Turn.fbx`
* `Prone Forward.fbx`
* `Prone Backward.fbx`
* `Prone to Crouching.fbx`

### Jumping

* `Sprint to Jump.fbx`
* `Idle to Jump.fbx`

These animations replace several temporary fallbacks described in the original REQ-036.

---

# 1. Updated Prone Locomotion

The previous temporary prone-movement fallback should no longer be used for forward/backward movement.

## Prone Forward

### Condition

Player is:

* Prone
* Grounded
* Moving primarily forward

### Animation

`Prone Forward.fbx`

This is the player's forward crawl animation.

It should loop for as long as the player continues crawling forward.

The animation must remain visual only. Existing prone movement speed and controller logic remain authoritative.

---

## Prone Backward

### Condition

Player is:

* Prone
* Grounded
* Moving primarily backward

### Animation

`Prone Backward.fbx`

The animation should loop while backward prone movement continues.

---

# 2. Prone Turning

Dedicated prone turning animations are now available.

### Left Turn

`Prone Left Turn.fbx`

### Right Turn

`Prone Right Turn.fbx`

These should visually represent a prone player rotating their body while not substantially translating across the ground.

The animation system should determine prone turning from the player's actual rotational movement rather than directly from mouse-stick input.

For example:

* Prone + meaningful leftward yaw rotation + little/no translational movement → `Prone Left Turn`
* Prone + meaningful rightward yaw rotation + little/no translational movement → `Prone Right Turn`

This is especially important in multiplayer, where remote clients should be able to see that a prone opponent is rotating rather than simply watching the entire rigid character model slide/rotate unnaturally.

A parameter such as:

`TurnSpeed`

or:

`AngularVelocity`

may be added to the Animator if useful.

Suggested interpretation:

* Negative value = turning left
* Positive value = turning right
* Near zero = not turning

Do not allow tiny mouse/controller adjustments to constantly restart a prone-turn animation. Use a reasonable threshold/dead zone.

---

# 3. Prone Direction Priority

When prone, animation selection should generally follow this priority:

1. Translating forward → `Prone Forward`
2. Translating backward → `Prone Backward`
3. Rotating left without meaningful translation → `Prone Left Turn`
4. Rotating right without meaningful translation → `Prone Right Turn`
5. Otherwise → `Prone Idle`

If the player is crawling and turning simultaneously, prioritize the crawl animation for now.

We can later add directional prone Blend Trees or additive turning if additional animations make that worthwhile.

---

# 4. Prone Left/Right Translation

There are still no dedicated animations for:

* Prone Crawl Left
* Prone Crawl Right

If gameplay currently allows lateral prone movement, continue allowing it.

For now, use the closest reasonable prone locomotion fallback rather than preventing movement.

Do not repurpose `Prone Left Turn` or `Prone Right Turn` as sideways crawl animations. Those clips should represent **rotation**, not translation.

The Animator should retain extension points for future prone-left and prone-right crawling animations.

---

# 5. Updated Prone Exit

A dedicated prone-exit animation now exists.

When the player leaves prone:

### Animation

`Prone to Crouching.fbx`

The expected transition becomes:

`Prone Idle / Prone Movement`
→
`Prone to Crouching`
→
`Crouching Idle / Crouching Movement`

The gameplay crouch state remains authoritative.

After the transition animation, immediately choose the appropriate crouch locomotion state based on current input:

* Stationary → `Crouching Idle`
* Forward → `Crouching Walk Forward`
* Backward → `Crouching Walk Backward`
* Left → `Crouching Walk Left`
* Right → `Crouching Walk Right`

Do not require the player to remain stationary during the transition.

If movement input occurs while `Prone to Crouching` is playing, gameplay should remain responsive and the Animator should transition naturally into the appropriate crouch locomotion animation.

---

# 6. Updated Full Prone State Flow

The prone portion of the Animator should now support:

### Entering Prone

`Crouching`
→
`Crouch to Prone`
→
`Prone Idle`

### Stationary

`Prone Idle`

### Crawl Forward

`Prone Forward`

### Crawl Backward

`Prone Backward`

### Turn Left

`Prone Left Turn`

### Turn Right

`Prone Right Turn`

### Exit Prone

`Prone`
→
`Prone to Crouching`
→
`Crouching`

This should be treated as the current intended prone animation architecture.

---

# 7. Jump Animation Support

Jumping should now be connected to the Animator.

Two jump-start animations are available:

* `Idle to Jump.fbx`
* `Sprint to Jump.fbx`

The existing player controller remains responsible for:

* Jump force
* Vertical velocity
* Gravity
* Ground detection
* Network movement

Animation must **not** control the actual jump trajectory.

Root motion remains disabled.

---

# 8. Idle to Jump

Use:

`Idle to Jump.fbx`

when the player jumps while:

* Stationary

or, temporarily:

* Walking

There is currently no dedicated walking-to-jump animation.

Therefore, for this version:

### Standing Still + Jump

`Idle to Jump`

### Walking Forward + Jump

`Idle to Jump`

### Walking Backward + Jump

`Idle to Jump`

### Strafing + Jump

`Idle to Jump`

This is an intentional temporary fallback.

Future walking/running jump animations should be easy to substitute without rewriting jump gameplay logic.

---

# 9. Sprint to Jump

If the player is actively sprinting immediately before jumping:

### Animation

`Sprint to Jump.fbx`

Expected flow:

`Sprint Forward`
→
`Sprint to Jump`
→
Airborne fallback
→
appropriate grounded locomotion state

The player's actual sprint/jump mechanics remain unchanged.

---

# 10. Determining Jump Type

Jump animation selection should be based on the player's movement state **at takeoff**.

Recommended logic:

If:

`IsSprinting == true`

and the player successfully initiates a jump:

→ `Sprint to Jump`

Otherwise:

→ `Idle to Jump`

Do not select animations simply because the sprint button is held.

For example, if the player is holding sprint but is stationary and somehow allowed to jump, use `Idle to Jump`.

Actual movement state should determine the animation.

---

# 11. Walking + Jump Temporary Behavior

For now:

`Walking`
→
`Idle to Jump`

is acceptable.

Do not delay or alter the player's jump to make the transition aesthetically perfect.

Responsiveness is more important than hiding the temporary animation limitation.

Once a suitable walking/running jump animation is acquired, it should replace this fallback through the Animator rather than changes to the player controller.

---

# 12. Airborne Animation Fallback

We still do not currently have dedicated:

* Falling
* Airborne Loop
* Landing

animations.

Therefore REQ-036 should handle the airborne portion gracefully.

After the jump-start animation:

* Do not replay the jump-start animation continuously.
* Do not prevent the character from remaining airborne.
* Do not let the Animator interfere with player physics.

Use the least visually disruptive available fallback until the player becomes grounded again.

Once grounded, transition immediately into the appropriate locomotion state:

* Stationary → `Standing Idle`
* Walking → appropriate walking animation
* Sprinting → `Sprint Forward`
* Crouched → appropriate crouch state, if gameplay permits this situation

A future ticket/asset update can add proper falling and landing states.

---

# 13. Jump Animator Parameters

REQ-036 may add parameters such as:

* `IsGrounded`
* `VerticalVelocity`
* `Jump`
* `IsAirborne`

if needed.

Avoid directly checking keyboard/gamepad buttons inside the animation component.

The animation system should receive the fact that **a jump successfully occurred** from the player movement system.

This avoids playing a jump animation when the player presses Jump but gameplay rejects the jump because they are:

* Already airborne
* Otherwise unable to jump
* In a state where jumping is prohibited

---

# 14. Multiplayer Jump Synchronization

Remote players must see the correct jump-start animation.

Example:

Player 1 is sprinting and jumps.

Player 2 should observe:

`Sprint Forward`
→
`Sprint to Jump`

while Player 1 follows the networked jump trajectory.

Likewise:

Player 1 walks and jumps.

Player 2 should observe:

`Walking`
→
`Idle to Jump`

for now.

Do not network the FBX animation name itself if replicated movement/state information can reliably drive the animation.

---

# 15. Updated Known Animation Gaps

The following gaps remain acknowledged and are not required for REQ-036:

### Standing

* Walk Left
* Walk Right

### Jump

* Walking/Running to Jump
* Airborne/Falling Loop
* Landing
* Sprint Landing

### Prone

* Crawl Left
* Crawl Right

### Other

* Dedicated Dolphin Dive
* Sprint Stop
* Death
* Hit reactions

The Animator should continue to be structured so these can be incorporated incrementally.

---

# 16. Updated Acceptance Criteria

In addition to the original REQ-036 acceptance criteria:

### Prone

* Forward prone movement plays `Prone Forward`.
* Backward prone movement plays `Prone Backward`.
* Stationary left rotation can play `Prone Left Turn`.
* Stationary right rotation can play `Prone Right Turn`.
* Tiny rotational adjustments do not constantly trigger turn animations.
* Prone turning animations are not incorrectly used as lateral crawl animations.
* Leaving prone plays `Prone to Crouching`.
* After `Prone to Crouching`, the correct crouching locomotion state is selected.

### Jumping

* A stationary jump plays `Idle to Jump`.
* A walking jump currently plays `Idle to Jump`.
* A sprinting jump plays `Sprint to Jump`.
* Jump animation selection is based on movement state at takeoff.
* Animation does not alter jump physics.
* Root motion remains disabled.
* Jump animations do not loop incorrectly while airborne.
* The lack of falling/landing animations does not interfere with gameplay.
* Remote players see the appropriate jump animation.
* Landing returns the Animator to the appropriate locomotion state.

These additions should supersede the corresponding temporary prone and jump fallbacks from the original REQ-036.
