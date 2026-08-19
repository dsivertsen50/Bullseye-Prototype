# REQ-009 — Controller Haptic Feedback

## Summary

Add controller rumble/haptic feedback to improve weapon and damage feedback during gameplay.

The initial implementation should support two effects:

1. A noticeable rumble when the local player fires a weapon.
2. A subtler rumble when the local player is successfully hit by another player.

This system should be implemented as a reusable player-level haptics component rather than embedding controller vibration directly into the weapon logic. This will allow future weapons to define different rumble characteristics without requiring a redesign of the haptics system.

---

## Goals

* Make shooting feel more responsive and satisfying.
* Give the player physical feedback when they take damage.
* Ensure rumble only affects the controller belonging to the appropriate local player.
* Create a reusable foundation for weapon-specific haptic effects later.
* Allow rumble strength and duration to be tuned in the Unity Inspector.

---

## Functional Requirements

### 1. Player Haptics Component

Create a reusable haptics component for the player, such as:

`PlayerHaptics`

The component should be responsible for:

* Identifying the controller associated with the local player.
* Starting controller rumble.
* Managing rumble duration.
* Stopping/resetting rumble.
* Exposing separate methods for different gameplay events.

Suggested interface:

```csharp
PlayFireRumble();
PlayDamageRumble();
StopRumble();
```

The exact class and method names may differ if another implementation fits the existing project architecture better.

---

### 2. Fire Rumble

Whenever the local player successfully fires their weapon:

* Immediately trigger controller rumble.
* Only the firing player's controller should rumble.
* The rumble should not require waiting for a network round trip.
* The effect should be relatively short and noticeable.

Initial suggested tuning:

* Duration: approximately `0.08–0.12 seconds`
* Low-frequency motor: moderate
* High-frequency motor: moderate to moderately strong

The precise values should be Inspector-configurable.

The current prototype weapon should use one generic fire-rumble configuration.

---

### 3. Damage Rumble

Whenever a player successfully receives damage:

* Trigger rumble on that player's local controller.
* Do not rumble the attacker's controller as part of this effect.
* The damage rumble should feel clearly different from firing rumble.
* It should initially be subtler than the firing effect.

Initial suggested tuning:

* Duration: approximately `0.12–0.18 seconds`
* Overall intensity lower than fire rumble

The precise values should be Inspector-configurable.

Damage rumble should only occur when the authoritative gameplay logic determines that the player was actually hit/damaged.

---

## Multiplayer Requirements

Haptics are local feedback and should not be synchronized as a networked controller state.

### Firing

When Player A fires:

* Player A's local controller rumbles immediately.
* Player B's controller does not rumble simply because Player A fired.

### Damage

When Player A shoots Player B and the hit is confirmed:

* Player B's controller receives the damage rumble.
* Player A does not receive the damage rumble.

The implementation must avoid relying blindly on:

```csharp
Gamepad.current
```

if doing so could cause the wrong controller to vibrate.

Where possible, use the controller/input device associated with the relevant local player.

---

## Inspector Configuration

The haptics component should expose tuning parameters in the Unity Inspector.

At minimum:

### Fire Rumble

* Low-frequency motor strength
* High-frequency motor strength
* Duration

### Damage Rumble

* Low-frequency motor strength
* High-frequency motor strength
* Duration

Values should be clamped to valid ranges where appropriate.

This will allow the effects to be adjusted without editing code.

---

## Rumble Lifecycle / Cleanup

The system must ensure that a controller cannot become stuck vibrating.

Rumble should be stopped or reset when appropriate, including when:

* The configured rumble duration expires.
* The player object is disabled or destroyed.
* The controller becomes unavailable/disconnected.
* The haptics component is disabled.
* Gameplay exits or otherwise transitions away from the active player.

If a new rumble event occurs while an existing effect is playing, the implementation should handle it cleanly rather than leaving multiple uncontrolled timers running.

For the prototype, restarting/replacing the current effect with the newest rumble event is acceptable.

---

## Keyboard and Mouse Behavior

Players using keyboard and mouse should continue functioning normally.

If no compatible gamepad is associated with the player:

* Do nothing.
* Do not throw an error.
* Do not produce repeated console warnings.

Haptics are optional feedback and should never interfere with weapon firing or damage logic.

---

## Architecture for Future Weapons

Do not tightly couple rumble values to the current weapon script.

The haptics system should be designed so that future weapons can eventually request different effects.

For example:

```text
Pistol
→ short, sharp rumble

Rifle
→ slightly stronger sustained pulse

Shotgun
→ heavy low-frequency kick
```

Weapon-specific rumble profiles do **not** need to be implemented as part of this ticket.

This ticket only needs to establish the reusable foundation and generic fire/damage effects.

---

## Out of Scope

The following are explicitly out of scope for this requirement:

* Different rumble profiles for individual guns.
* Reload haptics.
* Empty-magazine haptics.
* Hit-confirmation rumble for the attacker.
* Directional damage haptics.
* Variable rumble based on damage amount.
* Advanced recoil patterns.
* Controller trigger resistance/adaptive triggers.
* Haptic feedback for jumping, crouching, movement, or bullseye movement.
* Changes to weapon mechanics.
* Changes to damage values.

These can be considered in future tickets.

---

## Acceptance Criteria

The requirement is complete when all of the following are true:

* [ ] Firing the current weapon with a controller produces a short rumble.
* [ ] The rumble occurs immediately for the player who fired.
* [ ] Firing does not cause another player's controller to rumble.
* [ ] Being successfully shot produces a separate, subtler rumble for the damaged player.
* [ ] The attacker's controller does not receive the victim's damage rumble.
* [ ] Fire rumble strength and duration can be adjusted from the Inspector.
* [ ] Damage rumble strength and duration can be adjusted from the Inspector.
* [ ] Keyboard-and-mouse gameplay continues to work without errors.
* [ ] A player with no compatible controller does not generate errors.
* [ ] Rumble automatically stops after its configured duration.
* [ ] Rumble cannot remain permanently active after the player object is disabled or destroyed.
* [ ] Existing shooting, damage, death, respawn, and multiplayer functionality continues to work.
* [ ] The implementation provides a reusable player-level haptics foundation suitable for future weapon-specific effects.

---

## Testing Notes

Test with two multiplayer clients and two Xbox controllers where possible.

Verify the following scenarios:

1. **Player 1 fires**

   * Player 1's controller rumbles.
   * Player 2's controller does not.

2. **Player 2 fires**

   * Player 2's controller rumbles.
   * Player 1's controller does not.

3. **Player 1 shoots Player 2**

   * Player 1 receives fire rumble.
   * Player 2 receives damage rumble.

4. **Player 2 shoots Player 1**

   * Player 2 receives fire rumble.
   * Player 1 receives damage rumble.

5. **Rapid firing**

   * Rumble continues to behave predictably.
   * Controller does not become stuck vibrating.

6. **Death and respawn**

   * Haptics do not interfere with death or respawn.
   * Controller is not left vibrating during or after respawn.

7. **No controller**

   * Keyboard/mouse play continues normally without errors.

---

## Prototype Design Intent

This feature is intended primarily to improve **game feel**, not add a new gameplay mechanic.

The desired experience is:

**Fire weapon → immediate physical kick.**

**Get shot → subtle physical acknowledgement that damage occurred.**

The implementation should remain simple enough that it can be expanded later as the prototype begins introducing distinct guns, recoil characteristics, ammunition, and reload mechanics.
