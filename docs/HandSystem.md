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
3. **Ballistic punch:** a mouse flick faster than `swingTrigger`·`fullSwingSpeed` *latches*
   a punch — `_punchTime` runs 0→1 over `punchDuration`, driving `_extend` along a
   `sin` hump (out and back) scaled by `_punchPower` (the flick speed, captured at launch).
   Mid-flight the mouse is ignored: reach follows the timer and `_lean` (leading hand) is
   frozen until the punch completes, then it's ready to throw again. Idle `_extend` = 0
   (hands rest at `baseReach`). Per-hand seesaw/chambering stays deleted; the weapon-target
   rework owns that logic when it lands.
5. **Hand position:** guard point on the sphere, then the punch extends toward the
   crosshair: `offset = dir·baseReach + aimPivot.forward·_extend`, clamped to
   `_maxRadius`; target = `anchor.position + offset`.
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
| `raiseSpeed` | 8 | Raise/lower blend speed on aim start/stop |
| `aimSensitivity` | 0.004 | Mouse delta → sweep speed |
| `aimClamp` | 0.8 | Max sweep from resting guard (keeps hands in front) |
| `baseReach` | 0.30 | Reach with a still mouse |
| `maxExtension` | 0.25 | Extra reach at full swing |
| `fullSwingSpeed` | 1200 | Mouse speed (px/sec) for 100% extension |
| `extendSmooth` | 14 | Ease rate the pivot (`_lean`) relaxes between punches |
| `swingTrigger` | 0.5 | Flick speed (fraction of `fullSwingSpeed`) that latches a punch |
| `punchDuration` | 0.35 | Seconds for a latched punch to drive out and retract |
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

## Weapon architecture (decided 2026-09-26, not built)

Vocabulary rule: nothing is "driven" except the target; everything downstream **matches**
its target, best-effort. Same relationship at every link — a goal and a follower that may
fail to reach it.

```
mouse         → steers → WEAPON TARGET   kinematic pose, our own math, pure player
                                         intent — nothing in the world can stop it
weapon BODY   → tries to match target    Rigidbody, real mass, PD/velocity-matching
                                         with a capped force ("motor power")
hands         → try to match grip points IK targets = grip points on the weapon BODY,
                on the body              FBBIK solves the arms
```

- **Weapon target** — analogous to an IK target. Mouse sweep/extension math (today's
  per-hand logic) migrates here. The arm-reach clamp (`_maxRadius` sphere) applies to
  the *target*, not the hands — that's the only place hand reach still matters while armed.
- **Weapon body** — lag, momentum, parries, binds all emerge from the sim: an enemy
  blade stops the body while the target keeps going. Two knobs carry the feel:
  **tracking force cap** (weapon weight + future strength stat) and **max target↔body
  separation** before give/snap-back.
- **Hands** — a gripping hand has *zero* independent position logic; its IK target is
  the grip point, position and rotation. Arms jolt on impact for free because they
  follow the body, not the target. Two-handed = second grip point. An empty off-hand
  keeps the current guard logic.
- **Fists** = zero-length weapon, one target per hand — current PlayerHandHandler math
  is the degenerate case and eventually shares the target code.
- **Feel signals** — each link's target↔follower gap is a gameplay input: stamina
  drain, camera shake, disarm threshold.

Half Sword reference: full active-ragdoll, joint "motor powers" chase arm poses, weapon
is a passive constrained body. Our stack is the same idea with the ragdoll cut out —
motor power becomes the body's tracking force cap.

Not designed yet: orientation input channel for the target (candidate: `Block` held +
mouse = rotate instead of translate, scroll = wrist roll), two-handed reach clamp shape
(cheap version: clamp mid-grip to the tighter shoulder sphere), weapon content
(`Assets/Weapons/` is empty).
