# Bow & Arrow — spec v1

The equipped weapon defines what the mouse buttons mean. Fists: LMB slap / RMB jab.
**Bow: hold LMB to draw, release LMB to loose.** The left hand holds the riser (the
"never attacks" rule is an unarmed rule); the right hand is the string hand.

### Draw state machine (user-designed 2026-10-03)

```
IDLE    --LMB down-->          DRAWING   camera Y LOCKED; mouse pull-down scrubs draw01
DRAWING --draw01 == 1-->       DRAWN     Y UNLOCKS: full pitch aim, draw pinned at 1
DRAWING or DRAWN --LMB up-->   FIRE with velocity proportional to draw01, then IDLE
DRAWING or DRAWN --RMB-->      CANCEL: draw eases home, no fire, then IDLE
```

- Partial release fires weak (velocity ∝ draw01) — no free panic-cancel on the fire button.
- RMB is the cancel; it has no other job while a bow is equipped.
- DRAWN latches when draw01 reaches 1; further mouse-down in DRAWN is camera aim, not draw.

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
| LMB held (DRAWING) | Mouse pull-down scrubs `_draw01` 0→1 (350 px to full). No decay while held. Camera Y locked. |
| `_draw01` hits 1 (DRAWN) | Camera Y unlocks — full pitch aim at full draw. Draw stays pinned. |
| LMB released | FIRE, velocity ∝ `_draw01` (slice 1: debug log + draw resets; arrow is slice 2) |
| RMB (while LMB held) | Cancel — draw eases home at 4/s, no fire |
| Camera yaw | Damped as usual in every phase |

## Components

### Scene (hand-built, editor)
- **`CheekAnchor`** — empty, child of `CameraRig/CameraPivot`, local position
  `(0.06, -0.06, -0.10)`: slightly right of the eye, below, pulled back. The string
  hand's full-draw destination. Tune by eye with the gizmo on.
- **Placeholder bow** — a thin capsule childed to the LEFT hand bone (scale ≈
  `0.04, 0.5, 0.04`), so the pose reads while there's no real asset. Cosmetic only;
  no script references it in slice 1.

### Code — weapon behavior lives IN the weapon

- **`PlayerEquipment.cs`** (new, on the Player root) — owns the slot, knows nothing
  about any specific weapon:
  - refs it EXPOSES to the equipped weapon (public getters): `PlayerInputReader Input`,
    `Transform AimPivot`, `Transform RightHandTarget`, `Transform LeftHandTarget`,
    `Transform CheekAnchor` (player-anatomy anchors live player-side; weapons borrow them)
  - state: `public Weapon Current { get; private set; }`
  - `Equip(Weapon w)` / `Unequip()` — calls `w.OnEquip(this)` / `OnUnequip()`.
  - Prototype input: `Interact` toggles equipping the scene bow (pickup slice replaces
    this later).
- **`Weapon.cs`** (new, abstract MonoBehaviour, lives on the weapon prefab):
  - `public abstract void OnEquip(PlayerEquipment owner);`
  - `public abstract void OnUnequip();`
  - A weapon drives the hand IK targets itself while equipped (it subscribes to the
    FBBIK OnPreUpdate the same way PlayerHandHandler does, or ticks in its own Update
    and writes the target transforms — the weapon decides).
- **`Bow.cs : Weapon`** (new, on the bow prefab) — ALL bow logic from this spec:
  - knobs: `bowHoldDistance = 0.45f`, `bowHoldRight = -0.06f`, `pixelsToFullDraw = 350f`
  - state: `_draw01`, `public float Draw01 => _draw01`
  - per IK frame while equipped:
    - grip (left hand) = `AimPivot.position + AimPivot.forward * bowHoldDistance + AimPivot.right * bowHoldRight`
    - nock rest = `grip - AimPivot.forward * 0.05f`
    - string hand (right) = `Lerp(nockRest, CheekAnchor.position, _draw01)`
    - both effector positionWeights eased toward 1 (exp-ease, rate 8) so equip/unequip
      blends instead of popping.
- **`PlayerHandHandler` yield** — one check: `equipment.Current != null` →
  `UpdateHands()` returns. Fists are the unarmed default; a weapon owns the hands while
  equipped. (Future note, not now: fists themselves become a `Weapon`.)
- **`PlayerLooker`** — jab-pattern line: while a bow is equipped and LMB held,
  `look.y = 0`. v1 reads `equipment.Current is Bow` + input; a `Weapon.LocksPitch`
  flag only when a second weapon needs it.

## Deliberately NOT in slice 1
Arrow, string rendering, draw-strength stamina, left-arm fatigue sway, quiver,
ammo, pickup. Each earns its own slice.

## Acceptance (slice 1)
- E toggles: arms blend into bow pose (left extended, right at nock) and back to guard.
- LMB + pull down: right hand travels nock → cheek, holds at any point, full draw
  reaches the cheek anchor. Release: eases back to nock.
- Horizon does not pitch while drawing; yaw still steers (damped).
- Fists (jab/slap) completely unaffected when bow mode is off.
