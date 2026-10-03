# Bow & Arrow — spec v1

The equipped weapon defines what the mouse buttons mean. Fists: LMB slap / RMB jab.
**Bow: LMB draws, RMB fires.** The left hand holds the riser (the "never attacks" rule
is an unarmed rule); the right hand is the string hand.

Open question for partner sign-off: RMB-fire is a two-button chord (LMB stays held).
Common alternative: release-LMB-to-loose. Spec assumes the chord until overruled.

## Slices

1. **Pose + draw** (this spec, built first): bow mode toggle, both arms posed, draw
   scrub on the right hand, camera pitch lock while drawing. No arrow.
2. **Fire:** RMB while drawn → arrow prefab, velocity ∝ draw, down the crosshair ray.
3. **Real bow asset + string visual + pickup integration** (bow is pre-equipped until
   the pickup slice exists).

## Controls (bow mode, slice 1)

| Input | Effect |
|---|---|
| `Interact` (E) | Toggle bow mode on/off (prototype stand-in for equipping) |
| LMB held + mouse PULL DOWN/BACK | `_draw01` scrubs 0→1 (350 px to full). No decay while held — it holds, like the hand scrubs. |
| LMB released | Draw eases home at 4/s, no fire |
| Camera | Pitch (Y) locked while LMB held — the pull belongs to the draw. Yaw damped as usual. |

## Components

### Scene (hand-built, editor)
- **`CheekAnchor`** — empty, child of `CameraRig/CameraPivot`, local position
  `(0.06, -0.06, -0.10)`: slightly right of the eye, below, pulled back. The string
  hand's full-draw destination. Tune by eye with the gizmo on.
- **Placeholder bow** — a thin capsule childed to the LEFT hand bone (scale ≈
  `0.04, 0.5, 0.04`), so the pose reads while there's no real asset. Cosmetic only;
  no script references it in slice 1.

### Code
- **`BowController.cs`** (new, on the Player root) — while `BowMode` is on, it owns BOTH
  hand IK targets and their effector weights. Fields:
  - refs: `PlayerInputReader input`, `Transform aimPivot`, `Transform cheekAnchor`,
    `Transform rightHandTarget`, `Transform leftHandTarget` (same target transforms
    FBBIK already binds; auto-found by name like PlayerHandHandler does)
  - knobs: `bowHoldDistance = 0.45f` (left hand ahead of the camera),
    `bowHoldRight = -0.06f` (riser sits slightly left of center),
    `pixelsToFullDraw = 350f`
  - state: `_draw01`, `public bool BowMode`, `public float Draw01 => _draw01`
- **Geometry per IK frame (OnPreUpdate, same pattern as PlayerHandHandler):**
  - grip (left hand) = `aimPivot.position + aimPivot.forward * bowHoldDistance + aimPivot.right * bowHoldRight`
  - nock rest = `grip - aimPivot.forward * 0.05f`
  - string hand (right) = `Lerp(nockRest, cheekAnchor.position, _draw01)`
  - both effector positionWeights eased to 1 while BowMode (reuse the exp-ease idiom,
    rate 8), so entering/leaving the mode blends instead of popping.
- **`PlayerHandHandler` yield** — one check: if a `BowController` exists and
  `BowMode` is true, `UpdateHands()` returns before driving anything. Bow and fists
  never fight over the effectors.
- **`PlayerLooker`** — one line, jab-pattern: while `BowMode && LMB held`, `look.y = 0`.

## Deliberately NOT in slice 1
Arrow, string rendering, draw-strength stamina, left-arm fatigue sway, quiver,
ammo, pickup. Each earns its own slice.

## Acceptance (slice 1)
- E toggles: arms blend into bow pose (left extended, right at nock) and back to guard.
- LMB + pull down: right hand travels nock → cheek, holds at any point, full draw
  reaches the cheek anchor. Release: eases back to nock.
- Horizon does not pitch while drawing; yaw still steers (damped).
- Fists (jab/slap) completely unaffected when bow mode is off.
