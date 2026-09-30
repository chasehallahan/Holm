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

## Hand system today (PlayerHandHandler) — deliberately the SIMPLE baseline

A richer version (committed punch lines, chambering, ballistic latched punches, custom
torso offsets) was built and REVERTED on 2026-09-27 — it compounded into worse feel at
every step. Do not reintroduce those mechanics without an explicit request, and change
at most ONE feel variable per playtest.

- Hold attack (or `forceAim`) raises fists to a guard on a per-shoulder sphere; mouse
  sweeps the guard (`_aim`, clamped).
- Extension (v2 scrub): RMB = jab; SwingTarget accumulates Extend01 from mouse Y and
  PlayerHandHandler lerps the RIGHT hand from its live guard position to
  SwingTarget.TargetPoint(). The left hand never attacks.
- No body-English is wired yet: ShoulderRotator/BodyTilt were evaluated and NOT added
  to the scene.
- Debug: `forceAim` defaults ON; `showReachSphere` draws reach gizmos. `DebugCamToggle`:
  press C (Crouch) for the third-person debug camera.

## Repo hygiene

~1200 tracked RootMotion plugin files show modified from line-ending churn. NEVER
`git add -A` / `git add Assets/Plugins`. Stage files by explicit path. The churn stays
uncommitted on purpose.
