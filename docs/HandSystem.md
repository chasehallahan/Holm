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
3. **Swing extension:** normalized mouse speed (px/sec, framerate-independent), smoothed:
   `reach = clamp(baseReach + clamp01(speed/fullSwingSpeed)·maxExtension, minReach, _maxRadius)`.
   Both hands share it — per-hand seesaw/chambering deliberately deleted; the weapon-target
   rework owns that logic when it lands.
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
| `fullSwingSpeed` | 1200 | Mouse speed (px/sec) for 100% extension |
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

Not designed yet: two-handed reach clamp shape (cheap version: clamp mid-grip to the
tighter shoulder sphere), weapon content (`Assets/Weapons/` is empty).

## Swing model (designed 2026-09-28, not built) — the weapon target's motion

Replaces sphere-radius extension entirely; the guard sphere survives only as the rest
pose. Solves trajectory, commitment, AND orientation structurally.

1. **Aim line** — a ray along the camera's z axis, always live (the crosshair).
2. **Commit at acceleration onset:** the swing is detected by mouse acceleration rising;
   the commit point is the ray from the last CALM frame (before the accel spike), at the
   weapon's reach, recorded **relative to the player/camera rig**. The swipe that throws
   the swing never inherits its own camera motion — the destination is where you were
   aiming before your hand started moving.
3. **Per-weapon data (unarmed included):** `minReach`/`maxReach`, `minArc`/`maxArc`.
4. **Trajectory is an arc** from the current target pose to the committed point. The
   arc's bow plane comes from mouse velocity direction at commit — swipe right bows the
   arc rightward, swipe down is an overhead plane. Swing direction = swing type.
5. **Rotation coupling:** distance traveled along the arc maps to rotational pose —
   the fist/blade turns as a function of arc progress. This IS the orientation half of
   the control scope; no separate orientation input channel needed.
6. **Speed flattens the arc:** faster swings tend toward `minArc` (tight, direct);
   lazy swings take the fuller arc. Power expresses as geometry.
7. **End state:** on reaching the committed point the target HOLDS there while mouse
   velocity stays high; when velocity decays it returns to the sphere-projection guard
   position it would occupy had no swing happened.
