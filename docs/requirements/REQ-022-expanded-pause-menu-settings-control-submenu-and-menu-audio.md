# REQ-022 — Expanded Pause Menu Settings, Controls Submenu, and Menu Audio

## 1. Summary

Expand the existing pause menu into a more complete settings interface.

The current pause menu primarily displays the game's controls. Instead, the pause menu should provide access to a dedicated **Settings** menu, with **Controls** moved into its own submenu.

The first settings to implement are:

* Master Volume
* Sound Effects Volume
* Music Volume
* Screen Brightness
* Controls submenu

The pause menu should also play UI sound effects when the player navigates through or interacts with menu elements.

All audio clips used for menu interaction must be exposed as easy-to-assign Unity Inspector fields so custom sound files can be dragged into the appropriate slots later.

---

## 2. Goals

The player should be able to:

1. Pause the game.
2. Open the Settings menu.
3. Adjust game audio volume.
4. Adjust screen brightness.
5. Open a separate Controls submenu.
6. Navigate all of these menus using:

   * Mouse
   * Keyboard
   * Gamepad
7. Hear appropriate sounds when navigating and selecting menu options.
8. Have their settings persist between play sessions.

These settings are **local client preferences** and should not be synchronized over the network.

---

## 3. Pause Menu Structure

Update the existing pause menu so its top-level structure is approximately:

```text
PAUSED

Resume
Settings
Controls
Quit
```

If the existing pause menu contains other working options, they should not be unnecessarily removed.

### Important

The current controls information should **no longer occupy the main pause menu screen**.

Selecting:

```text
Controls
```

should open a dedicated Controls submenu.

Selecting:

```text
Settings
```

should open the new Settings submenu.

---

## 4. Settings Menu

The Settings submenu should initially contain:

```text
SETTINGS

Master Volume        [ Slider ]
Sound Effects        [ Slider ]
Music Volume         [ Slider ]
Brightness           [ Slider ]

Back
```

Exact visual styling is not important yet.

The priority is functionality and a clean structure that can be restyled later.

---

## 5. Master Volume

Add a Master Volume slider.

Suggested range:

```text
0% — 100%
```

Behavior:

* 100% = normal/full game audio.
* 0% = muted.
* Intermediate values scale appropriately.

Master Volume should affect all game audio controlled by the game's AudioMixer, including:

* Weapons
* Player sounds
* Bullseye/damage sounds
* Menu sounds
* Music
* Other future audio categories

Use Unity's audio system appropriately rather than attempting to manually adjust every `AudioSource` individually.

An `AudioMixer`-based implementation is preferred if the project does not already have one.

---

## 6. Sound Effects Volume

Add a separate Sound Effects slider.

Suggested range:

```text
0% — 100%
```

This should control gameplay and UI sound effects independently of music.

Examples include:

* Gunfire
* Weapon manipulation
* Damage
* Bullseye cracking
* Movement sounds
* Menu interaction sounds
* Future environmental effects

Sound Effects Volume should still be affected by Master Volume.

Conceptually:

```text
Final SFX Volume =
Master Volume × SFX Volume
```

Do not manually calculate this in code if Unity AudioMixer routing can handle the hierarchy cleanly.

---

## 7. Music Volume

Add a Music Volume slider.

Suggested range:

```text
0% — 100%
```

There may not yet be meaningful music in the prototype.

That is okay.

The setting and mixer routing should still be established now so that future music can be assigned to the Music mixer group without rewriting the settings system.

Music Volume should also remain subordinate to Master Volume.

---

## 8. Brightness

Add a Brightness slider.

The setting should change the perceived brightness of the rendered game.

### Important

Do **not** attempt to modify the player's physical monitor brightness.

Use an appropriate Unity/HDRP rendering solution, preferably through exposure/post-processing.

Suggested user-facing range:

```text
50% — 150%
```

or an equivalent internally mapped exposure range.

The default value should result in the game looking exactly or approximately as it currently does.

The slider must support both making the game:

* Darker than default
* Brighter than default

Avoid extreme values that result in an unusably black or completely blown-out image.

The implementation should be compatible with the project's existing HDRP configuration.

---

## 9. Controls Submenu

Move the existing control instructions into their own submenu.

Example:

```text
CONTROLS

Move               WASD / Left Stick
Look               Mouse / Right Stick
Jump               Space / [Gamepad Button]
Sprint             Shift / Left Stick Press
Aim / Zoom          Right Mouse / Right Stick Press
Fire               Left Mouse / Right Trigger
Pause               Escape / Start

Back
```

Use the project's **actual current bindings** rather than blindly copying the example above.

The existing controls display can be reused rather than rebuilt if practical.

The goal of this requirement is primarily to reorganize it into a separate screen.

No control rebinding system is required by REQ-022.

---

## 10. Menu Navigation Sound Effects

Add sound feedback for pause-menu interaction.

At minimum, support three optional clips:

### Navigate

Played when the currently selected menu item changes.

Examples:

* Moving from Resume → Settings
* Moving between settings sliders
* Moving between menu buttons using the gamepad

### Select

Played when the player activates a button.

Examples:

* Opening Settings
* Opening Controls
* Pressing Back
* Pressing Resume

### Back

Played when returning to the previous menu.

Examples:

```text
Settings → Pause Menu
Controls → Pause Menu
```

It is acceptable for Select and Back to initially use the same sound file, but they should have separate assignable fields.

---

## 11. Inspector Audio Slots

Menu sound clips must **not be hardcoded**.

Expose clearly labeled fields in the relevant menu audio component.

For example:

```text
Menu Audio
    Navigate Sound: [ AudioClip ]
    Select Sound:   [ AudioClip ]
    Back Sound:     [ AudioClip ]
```

These should appear in the Unity Inspector so audio files can simply be dragged into the slots later.

If appropriate, also expose:

```text
Menu Audio Source: [ AudioSource ]
```

or create/manage an appropriate dedicated UI AudioSource automatically.

Missing audio clips should not cause errors.

If one of the fields is empty, the menu should simply perform the interaction silently.

---

## 12. Slider Audio Behavior

Interacting with sliders should provide subtle audio feedback.

However, do **not** play a sound every frame while an analog stick is held.

This could produce extremely rapid/repetitive audio.

Instead, use reasonable throttling or discrete adjustment behavior.

For example:

* A navigation sound when initially selecting the slider.
* A subtle adjustment sound at reasonable intervals while changing its value.

If adding a separate adjustment sound significantly complicates the implementation, the existing Navigate sound may be reused.

---

## 13. Controller Navigation

All new menus must be fully operable with a gamepad.

This is particularly important because controller navigation has previously been a problem with the pause menu.

The player should be able to:

1. Open Pause.
2. Navigate to Settings.
3. Enter Settings.
4. Move between sliders.
5. Adjust slider values with the controller.
6. Select Back.
7. Open Controls.
8. Return from Controls.
9. Resume the game.

A mouse should **not** be required at any point.

There should always be a sensible selected UI element when a submenu opens.

For example:

```text
Pause Menu opens
→ Resume selected

Settings opens
→ Master Volume selected

Controls opens
→ Back button or another sensible element selected
```

Use Unity's EventSystem/UI navigation rather than implementing a completely separate controller-only menu system unless necessary.

---

## 14. Mouse and Keyboard Support

Existing mouse functionality should continue working.

Keyboard navigation should also work where supported by the UI/EventSystem.

The menu must continue to support:

* Mouse hover
* Mouse clicking
* Keyboard navigation
* Gamepad navigation

Adding controller support must not break mouse interaction.

---

## 15. Back Behavior

Submenus should have predictable Back behavior.

From:

```text
Pause Menu
```

pressing the existing pause/back input should resume the game if that is current behavior.

From:

```text
Settings
```

Back should return to:

```text
Pause Menu
```

From:

```text
Controls
```

Back should return to:

```text
Pause Menu
```

Opening and closing submenus should **not** accidentally unpause the game.

---

## 16. Settings Persistence

The following settings should persist after exiting Play Mode / restarting the standalone game:

* Master Volume
* Sound Effects Volume
* Music Volume
* Brightness

Use a lightweight local persistence mechanism such as `PlayerPrefs` unless the project already has a better settings system.

Example conceptual keys:

```text
MasterVolume
SFXVolume
MusicVolume
Brightness
```

Exact key names are implementation details.

On startup:

1. Load saved values if available.
2. Otherwise use sensible defaults.
3. Apply the values to the corresponding systems.
4. Update slider positions to reflect the active values.

Changing settings should save them automatically or when leaving the Settings screen.

A separate Apply button is **not required**.

---

## 17. Multiplayer Requirements

These settings are client-local.

Do not:

* Add NetworkVariables for them.
* Send RPCs when settings change.
* Synchronize settings between players.
* Allow one player's settings to alter another player's audio or display.

For example:

```text
Player 1 Master Volume = 30%
Player 2 Master Volume = 100%
```

should be valid when running as separate clients.

The menu system should remain compatible with the project's Netcode for GameObjects implementation.

---

## 18. Pause Behavior

Existing multiplayer pause behavior should be preserved unless fixing it is required for the new UI.

Do not accidentally:

* Stop networking.
* Freeze another player's client.
* Pause the host in a way that prevents connected clients from functioning.
* Modify gameplay authority.

REQ-022 is a **menu/settings enhancement**, not a redesign of multiplayer pause architecture.

---

## 19. Suggested Architecture

The agent may adapt this to the existing project structure, but a clean implementation could include something similar to:

```text
PauseMenuController
SettingsMenuController
GameSettingsManager
MenuAudioController
```

Responsibilities could approximately be:

### PauseMenuController

Handles:

* Showing/hiding pause menu
* Resume
* Settings navigation
* Controls navigation
* Quit
* Menu-state transitions

### SettingsMenuController

Handles:

* Slider UI
* Updating displayed settings
* Passing changes to GameSettingsManager

### GameSettingsManager

Handles:

* Master volume
* SFX volume
* Music volume
* Brightness
* Loading settings
* Saving settings
* Applying settings

### MenuAudioController

Handles:

* Navigate sound
* Select sound
* Back sound
* Optional slider adjustment sounds

Do not create unnecessary scripts if equivalent functionality already exists.

Prefer extending the existing pause-menu architecture cleanly.

---

## 20. AudioMixer Structure

If an appropriate AudioMixer does not already exist, create one.

A reasonable structure would be:

```text
Master
├── SFX
│   ├── Weapons
│   ├── Player
│   └── UI
└── Music
```

The full hierarchy does not need to be implemented if unnecessary.

At minimum, the system needs to distinguish:

```text
Master
SFX
Music
```

Existing AudioSources should not all need to be manually rewritten merely to complete this requirement.

Integrate existing audio into the mixer where reasonably safe.

Do not break working gunfire or other existing sound effects.

---

## 21. Default Settings

Use sensible defaults such as:

```text
Master Volume:       100%
Sound Effects:       100%
Music Volume:        100%
Brightness:          Default/current appearance
```

If previously saved values exist, those values should override the defaults.

---

## 22. Visual Requirements

This remains a prototype, so extensive menu art is not required.

However:

* Menu elements should be aligned consistently.
* Text should be readable.
* The currently selected button/slider should be visually obvious.
* Sliders should clearly indicate their current position.
* Settings and Controls should visually behave as submenus rather than all UI appearing simultaneously.

Do not spend significant implementation effort on final styling.

The menu will likely receive a dedicated visual redesign later.

---

## 23. Preserve Existing Functionality

Do not regress existing gameplay systems.

In particular, verify that this change does not break:

* Multiplayer
* Player movement
* Shooting
* Weapon animations
* Player health
* Health regeneration
* Respawning
* Bullseye behavior
* Existing pause/resume functionality
* Keyboard/mouse input
* Gamepad gameplay input

The pause menu should continue preventing the local player from accidentally firing/moving while manipulating menu UI, according to the current pause implementation.

---

## 24. Acceptance Criteria

REQ-022 is complete when all of the following are true:

* [ ] Opening the pause menu displays the primary pause-menu options rather than the controls screen alone.
* [ ] A Settings option exists.
* [ ] A Controls option exists.
* [ ] Controls opens as a separate submenu.
* [ ] Settings opens as a separate submenu.
* [ ] Settings contains a Master Volume slider.
* [ ] Settings contains a Sound Effects Volume slider.
* [ ] Settings contains a Music Volume slider.
* [ ] Settings contains a Brightness slider.
* [ ] Master Volume audibly changes overall volume.
* [ ] SFX Volume changes sound-effect volume independently of music.
* [ ] Music Volume is implemented and ready for current/future music.
* [ ] Brightness visibly changes the game's rendered brightness.
* [ ] Default brightness resembles the game's appearance before REQ-022.
* [ ] Settings persist between sessions.
* [ ] Menu navigation works with mouse.
* [ ] Menu navigation works with keyboard.
* [ ] Menu navigation works with gamepad.
* [ ] Settings sliders can be adjusted with gamepad input.
* [ ] Opening a submenu automatically selects a sensible UI element.
* [ ] Navigate interactions can play a sound.
* [ ] Select interactions can play a sound.
* [ ] Back interactions can play a sound.
* [ ] Navigate, Select, and Back clips have easy-to-find Inspector slots for custom `AudioClip` files.
* [ ] Empty audio slots do not cause errors.
* [ ] Back from Settings returns to the pause menu.
* [ ] Back from Controls returns to the pause menu.
* [ ] Navigating submenus does not unintentionally resume gameplay.
* [ ] One client's settings do not modify another client's settings.
* [ ] Existing gameplay functionality continues working.

---

## 25. Testing Procedure

### Test A — Menu Structure

1. Start the game.
2. Open the pause menu.
3. Confirm the main menu includes Settings and Controls.
4. Confirm the controls instructions are not permanently occupying the primary pause menu.
5. Open Settings.
6. Return to the pause menu.
7. Open Controls.
8. Return to the pause menu.

Expected result:

All menu transitions work normally.

---

### Test B — Audio Settings

1. Fire the Ruger and note its volume.
2. Open Settings.
3. Reduce SFX Volume.
4. Resume.
5. Fire again.

Expected result:

The Ruger is noticeably quieter.

Then:

1. Set Master Volume to 0%.
2. Resume gameplay.

Expected result:

All routed game audio is muted.

Restore Master Volume afterward.

---

### Test C — Brightness

1. Observe the game's normal appearance.
2. Open Settings.
3. Reduce Brightness.
4. Observe the game.
5. Increase Brightness above default.

Expected result:

The game becomes visibly darker and brighter without reaching unusable extremes.

---

### Test D — Persistence

1. Set recognizable values, for example:

```text
Master:      65%
SFX:         40%
Music:       75%
Brightness:  noticeably above default
```

2. Exit the game.
3. Restart it.
4. Open Settings.

Expected result:

The previous settings remain active and the sliders display the saved values.

---

### Test E — Controller Navigation

Using only an Xbox controller:

1. Open the pause menu.
2. Navigate to Settings.
3. Open Settings.
4. Navigate between every slider.
5. Change every slider.
6. Return to Pause.
7. Open Controls.
8. Return.
9. Resume gameplay.

Expected result:

No mouse interaction is required.

---

### Test F — Menu Audio

Assign temporary audio clips to:

```text
Navigate Sound
Select Sound
Back Sound
```

Navigate throughout the menu.

Expected result:

* Moving between UI elements plays Navigate.
* Activating an option plays Select.
* Returning to a previous screen plays Back.
* Audio does not rapidly spam while manipulating a slider.

---

### Test G — Multiplayer

Run the normal multiplayer test configuration.

On Client/Player 1:

```text
Master Volume = low
Brightness = dark
```

On Client/Player 2:

```text
Master Volume = high
Brightness = bright
```

Expected result:

Each client's settings remain independent.

No settings RPCs or synchronization should occur.

---

## 26. Out of Scope

REQ-022 does **not** require:

* Key rebinding
* Controller rebinding
* Resolution selection
* Fullscreen/windowed selection
* Graphics-quality presets
* FOV adjustment
* Mouse sensitivity
* Controller sensitivity
* Accessibility settings
* Final menu artwork
* Final menu animations
* Final sound effects
* Display/monitor hardware brightness control

These can be added in later requirements.

---

## 27. Implementation Priority

Priority order:

1. Restructure pause menu and create submenus.
2. Ensure controller navigation works throughout.
3. Implement Master/SFX/Music volume.
4. Implement brightness.
5. Implement persistent settings.
6. Implement navigation/select/back menu sounds.
7. Expose sound clips cleanly in the Inspector.
8. Regression-test multiplayer and existing gameplay.

Favor a clean, extensible settings architecture because additional options will likely be added later.

Do not replace stable existing gameplay systems merely to implement this requirement.
