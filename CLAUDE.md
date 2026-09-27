# Hólm — agent context

Half Sword-like first-person melee game. Unity 6.3, FinalIK (FBBIK) for all body IK;
PuppetMaster owned but deliberately unused until full-body physics is needed.
Style: minimal code, fewest knobs, delete before adding. Never touch `Assets/Plugins/`.
Working branch `HandChune`, PR #1 open against `master`.

## Current scope

Hand combat only: mouse click + movement drives the position and orientation of the hand
IK targets. Walking (PlayerStepper) and camera (PlayerLooker) exist and work; their
backlogs live as TODO comments in those files. `docs/HandSystem.md` is the source of
truth for the hand system — read it before changing hand code.

## Decided architecture (not yet built): weapon target

Vocabulary rule: nothing is "driven" except the target; everything downstream MATCHES it.
1. **Weapon target** — kinematic pose steered by the mouse (our math, never blocked).
2. **Weapon body** — Rigidbody that tries to match the target under a capped force
   ("motor power"); lag, parries, binds emerge from the sim.
3. **Hands** — IK targets = grip points on the weapon BODY. Gripping hand has zero
   independent logic; empty hands keep guard logic. Fists = zero-length weapon.

## Hand system today (PlayerHandHandler)

- Hold attack (or `forceAim`) raises fists to a guard on a per-shoulder sphere; mouse
  sweeps the guard (`_aim`, clamped).
- Extension = mouse speed in px/sec vs `fullSwingSpeed`, smoothed (`extendSmooth`).
- Pivot mechanics: swinging LEFT throws the RIGHT cross (`-_lean` in `leadT`); the
  trailing hand chambers toward the body.
- Punch flies along a committed line: `_punchFwd` lags the camera ~1/4s so the swing's
  own camera drift (`aimLookFactor` in PlayerLooker) can't steer a punch in flight.
- Each fist lands `punchSpread` to its OWN side of that line.
- Body engagement: `shoulderTwist` / `bodyLean` FBBIK positionOffsets (additive, reset
  each frame, stack on PlayerStepper's body target, never rotate the player transform —
  the camera must never snap), plus solver `pullBodyHorizontal = 0.3` and arm chain
  `reach = 0.25` set in Start.
- Debug: `forceAim` defaults ON; `showReachSphere` draws reach gizmos and gates the
  `[HandDbg]` console log (speed/target/extend/reach). Both are temporary.

## Repo hygiene

~1200 tracked RootMotion plugin files show modified from line-ending churn. NEVER
`git add -A` / `git add Assets/Plugins`. Stage files by explicit path. The churn stays
uncommitted on purpose.
