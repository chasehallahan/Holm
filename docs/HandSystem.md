# Hand Combat System (Half Sword-style)

**Scope:** mouse click + movement drives the position and orientation of the hand IK
targets. That's the whole system. Everything below the wrists is FinalIK's problem;
everything below the hips is [PlayerStepper](../Assets/Scripts/Character/Player/PlayerStepper.cs)'s problem.

**Core file:** [`Assets/Scripts/Character/Player/PlayerHandHandler.cs`](../Assets/Scripts/Character/Player/PlayerHandHandler.cs) (215 lines)

## How it works

```
PlayerInputReader (Look, AttackHeld, BlockHeld)
        │
        ▼
PlayerHandHandler.Update()          ← gathers per-frame state: aim sweep, extension, lean
        │
        ▼
IKSolverFullBodyBiped.OnPreUpdate → UpdateHands() → DriveArm(right), DriveArm(left)
        │
        ▼
FBBIK left/right hand effectors (position + rotation weights, target transforms)
```

1. **Raise:** `Aiming = forceAim || input.AttackHeld`. While aiming, effector
   `positionWeight` eases toward 1 (`raiseSpeed`); releasing eases back to 0 so the
   arms return to the animated pose.
2. **Aim sweep:** mouse delta accumulates into `_aim` (a 2D offset, clamped to
   `aimClamp`), shared by both hands. Each hand's direction = resting guard direction
   (mirrored X for the left) + sweep, normalized — the hands orbit a sphere around
   their shoulder anchor.
3. **Swing extension:** mouse *speed* maps through `pow(speed/fullExtendSpeed, extendCurve)`
   to an extension `_extend` on top of `baseReach`. Fast swing = full punch reach.
4. **Seesaw:** horizontal mouse direction sets `_lean` (−1..+1). A right swing extends
   the right hand and pulls the left in toward `minReach` (the chamber), and vice versa.
   `reach = clamp(baseReach + _extend * lean * side, minReach, _maxRadius)`.
5. **Hand position:** `anchor.position + dir * reach` written to the effector target.
6. **Hand rotation (mostly OFF today):** `LookRotation(dir) * gripCalib * gripEuler`.
   `gripCalib` is measured at startup so "pointing along the reach" doesn't twist the
   wrist. But `rotationWeight = positionWeight * handRotationWeight` and
   `handRotationWeight` defaults to **0**, so hands currently follow the arms naturally.

## While aiming, the camera

[PlayerLooker](../Assets/Scripts/Character/Player/PlayerLooker.cs) checks
`handHandler.Aiming` and scales look input by `aimLookFactor` (default 0.25) — the mouse
mostly drives the hands, the view only drifts with the swing.

## Inspector knobs (PlayerHandHandler)

| Field | Default | What it does |
|---|---|---|
| `guardOffset` | (0.05, −0.25, 0.4) | Resting guard position relative to shoulder, player space; X mirrored for left hand |
| `_maxRadius` | 0.55 | Hard cap on reach (keep below true arm length) |
| `minReach` | 0.12 | Chamber distance when the other hand punches |
| `raiseSpeed` | 8 | Raise/lower blend speed on aim start/stop |
| `aimSensitivity` | 0.004 | Mouse delta → sweep speed |
| `aimClamp` | 0.8 | Max sweep from resting guard (keeps hands in front) |
| `baseReach` | 0.30 | Reach with a still mouse |
| `maxExtension` | 0.25 | Extra reach at full swing |
| `fullExtendSpeed` | 25 | Mouse speed for 100% extension |
| `extendCurve` | 2 | >1 = flicks barely extend, real swings do |
| `extendSmooth` | 14 | Ease rate of reach toward target |
| `handRotationWeight` | 0 | 0 = natural hands (fists), 1 = point along reach (weapons) |
| `gripEuler` | (0,0,0) | Wrist roll fine-tune, only matters when rotation weight > 0 |

References (`input`, `_fbbik`, `aimPivot`, targets, anchors) auto-find themselves in
`Awake`/`Start` when left empty — left-side anchors/targets are derived from the right.

## Debugging

- `forceAim` (default **true**) — hands always up, no need to hold attack. Turn off for real input.
- `showReachSphere` (default true) — red translucent sphere per shoulder = current reach; gizmos only in play mode.

## Input channels

Available in [PlayerInputReader](../Assets/Scripts/Input/PlayerInputReader.cs):
`Look`, `AttackHeld/Pressed`, `BlockHeld/Pressed` (added for this system, **not consumed yet**),
plus Move/Sprint/Crouch/Jump/Interact for the rest of the character.

## What's not built yet (the orientation half)

Mouse currently controls **position only**. Open design: which channel rotates the hand
targets. Leading candidate: `Block` held + mouse = orient instead of translate
(pitch/roll), scroll = wrist roll. Also unowned: weapon grips (`Assets/Weapons/` is an
empty folder), two-handed coupling, collision/physics response of the arms.
