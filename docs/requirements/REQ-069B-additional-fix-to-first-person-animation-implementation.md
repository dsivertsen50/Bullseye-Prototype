The current implementation is substantially closer, but there is still an architectural issue that needs to be corrected before we polish animations.

## Current Problems

1. The first-person arms appear visibly cut/severed at their upper ends.
2. The lower-body view appears to be created by literally truncating/hiding part of the third-person character mesh.
3. This produces an obvious "body cut in half" appearance rather than a natural first-person body.
4. We should not continue hiding additional parts of the world mesh to solve this.

## Intended Architecture

Please treat these as THREE distinct presentation components:

```text
Player
│
├── WorldBody
│   └── Complete third-person character
│       - head
│       - torso
│       - world arms
│       - hips
│       - legs
│       - feet
│
├── FirstPersonBody
│   └── Body-awareness representation
│       - appropriate torso/waist
│       - hips
│       - legs
│       - feet
│
└── FirstPersonArms
    └── Dedicated FPS arms
        - positioned relative to FPS weapon
        - separate from world arms
        - local presentation only
```

The first-person lower body can share locomotion state and animation with the third-person character, but it does NOT need to literally be the same rendered SkinnedMeshRenderer.

# Do Not Solve This by Cutting the World Mesh

Please inspect how the current lower-body result was produced.

If the implementation is:

* disabling bones,
* hiding arbitrary body sections,
* collapsing upper-body bones,
* clipping the existing character,
* or otherwise truncating the full third-person SkinnedMeshRenderer,

replace that approach.

We need a clean first-person body representation, not a mutilated version of the third-person renderer.

# Preferred First-Person Body Approach

Ideally, FirstPersonBody should contain enough geometry that looking downward feels anatomically continuous:

* lower torso/abdomen where appropriate,
* waist,
* hips,
* thighs,
* legs,
* feet.

It should NOT expose:

* an open waist seam,
* an open chest cavity,
* an open neck,
* missing geometry that is obvious when looking downward.

It can still use the same skeleton/animation state as the world character if practical.

If the existing character mesh cannot cleanly render only the desired body sections because it is one continuous mesh/submesh, DO NOT create increasingly complicated runtime hacks.

Instead, identify that limitation clearly.

A dedicated first-person body mesh variant may ultimately be required. It can reuse the existing rig/skeleton while containing only the geometry appropriate for first-person body awareness.

If an art-side mesh adjustment in Blender would be cleaner than a code-side workaround, tell me rather than forcing a poor runtime solution.

# Dedicated First-Person Arms

FirstPersonArms must remain completely separate from WorldBody arms.

They should be positioned relative to:

```text
FirstPersonCamera
    └── FirstPersonPresentation
        ├── FirstPersonWeapon
        └── FirstPersonArms
```

or the equivalent architecture already established.

The arms should visually originate from outside or near the edge of the player's visible field of view and extend naturally toward the weapon.

The player should NOT see obvious severed shoulder/upper-arm ends.

If the FPS arm model ends at the shoulder, ensure that:

* the terminating geometry remains outside the normal camera view, OR
* the arm mesh has a visually acceptable sleeve/shoulder termination.

Do not attempt to attach these FPS arms directly to the third-person shoulders.

# Animation Responsibilities

Use shared GAMEPLAY state but separate PRESENTATION.

For example:

```text
IsWalking
IsSprinting
IsJumping
IsCrouched
IsProne
IsReloading
IsAiming
CurrentWeapon
```

can drive both representations.

But:

```text
Third-person arm pose
```

and:

```text
First-person arm pose
```

should remain separate.

The legs/body-awareness mesh should approximately reproduce the locomotion that other players see.

The FPS arms should instead prioritize:

* weapon grip,
* aiming,
* ADS,
* recoil,
* sprint posture,
* reload lowering,
* weapon switching.

# Important

Before changing anything else, inspect the current mesh/render architecture and determine whether the character model supports clean body-part separation.

Please report whether:

1. the character uses one or multiple SkinnedMeshRenderers,
2. the torso/arms/legs are separate submeshes,
3. the current FirstPersonBody is a duplicate mesh, modified world renderer, or bone-masked renderer,
4. a clean first-person body can be produced in Unity alone,
5. or whether creating a dedicated first-person body mesh variant in Blender would be the cleaner solution.

Do not keep patching the current visible seam until that is understood.

The design goal is:

> The world character stays complete, the first-person lower body creates convincing body awareness, and dedicated first-person arms handle weapon interaction.
