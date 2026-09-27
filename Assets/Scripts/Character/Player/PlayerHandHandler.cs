using RootMotion.FinalIK;
using UnityEngine;

public class PlayerHandHandler : MonoBehaviour
{
    private FullBodyBipedIK _fbbik;
    private IKSolverFullBodyBiped _ikSolver;

    [Header("References")]
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private Transform aimPivot;        //CameraRig/CameraPivot
    [SerializeField] private Transform handTarget;      //RightHandTarget
    [SerializeField] private Transform leftHandTarget;  //LeftHandTarget (auto-found from RightHandTarget's sibling if empty)

    [Header("Guard pose")]
    [Tooltip("Right shoulder bone the guard hangs off (e.g. clavicle_r).")]
    [SerializeField] private Transform guardAnchor;

    [Tooltip("Left shoulder bone (e.g. clavicle_l). Auto-found from the right one if left empty.")]
    [SerializeField] private Transform leftGuardAnchor;

    [Tooltip("Offset from the shoulder, in PLAYER space: x=right, y=up, z=forward (meters). X is mirrored for the left hand.")]
    [SerializeField] private Vector3 guardOffset = new Vector3(0.5f, -0.25f, 0.4f);

    [Tooltip("Max distance a hand can reach from the shoulder (kept below true arm length).")]
    [SerializeField] private float _maxRadius = .55f;

    [SerializeField] private float raiseSpeed = 8f; // how fast the hands raise/lower when you start/stop aiming

    [Header("Aim sweep (mouse orbits the hands around the shoulders)")]
    [Tooltip("Mouse delta -> how fast the aim sweeps around the sphere.")]
    [SerializeField] private float aimSensitivity = 0.004f;
    [Tooltip("Max sweep away from the resting guard direction (keeps the hands in front).")]
    [SerializeField] private float aimClamp = 0.8f;

    [Header("Swing extension (faster mouse = reach further out)")]
    [Tooltip("Arm reach when the mouse is still (smaller = more tucked, leaving room to swing out).")]
    [SerializeField] private float baseReach = 0.30f;
    [Tooltip("How far past baseReach a full swing can push the hand (meters).")]
    [SerializeField] private float maxExtension = 0.25f;
    [Tooltip("Mouse speed (pixels/SECOND) at which you reach FULL extension.")]
    [SerializeField] private float fullSwingSpeed = 1200f;
    [Tooltip("How fast the reach eases toward its target (lower = ramps out more slowly).")]
    [SerializeField] private float extendSmooth = 14f;
    [Tooltip("Mouse speed, as a FRACTION of fullSwingSpeed, that latches a committed punch.")]
    [SerializeField] private float swingTrigger = 0.5f;
    [Tooltip("Seconds for a latched punch to drive all the way out and retract (the whole committed motion).")]
    [SerializeField] private float punchDuration = 0.35f;
    [Tooltip("How far to its OWN side of the crosshair a full punch lands (meters). Keeps fists shoulder-width instead of converging on one point.")]
    [SerializeField] private float punchSpread = 0.15f;
    [Tooltip("How far the lead shoulder drives forward (and the trail shoulder pulls back) at full punch (meters). The torso pivot.")]
    [SerializeField] private float shoulderTwist = 0.12f;
    [Tooltip("How far the hips shift toward the punch at full extension (meters). The weight transfer.")]
    [SerializeField] private float bodyLean = 0.08f;

    [Header("Grip rotation")]
    [Tooltip("How much the hand orientation is driven. 0 = hands follow the arms naturally (unarmed fists); 1 = hands point along the reach (for weapons).")]
    [Range(0f, 1f)]
    [SerializeField] private float handRotationWeight = 0f;

    [Tooltip("Fine-tune wrist roll on top of the natural orientation (degrees). Only matters when handRotationWeight > 0.")]
    [SerializeField] private Vector3 gripEuler = Vector3.zero;

    [Header("Debug")]
    [Tooltip("Force aiming ON so the hands stay raised - lets you position/tune the guard without holding LMB. Uncheck when done.")]
    [SerializeField] private bool forceAim = true;
    [Tooltip("Draw the reach spheres (translucent red) in the Scene view while playing.")]
    [SerializeField] private bool showReachSphere = true;

    private Vector2 _aim;      // accumulated sweep around the sphere (where on the surface)
    private float _extend;     // current extension from the active punch, in meters
    private float _lean;       // -1..+1 horizontal swing direction: which hand leads the punch (frozen for a punch's duration)
    private Vector3 _punchFwd; // lagged camera forward: where the committed punch is going
    private float _punchTime = -1f; // progress 0..1 of the active punch; <0 = idle, ready to throw
    private float _punchPower;      // 0..1 reach of the active punch, captured from swing speed at launch
    private bool _armed = true;     // re-arm gate: mouse must slow down between punches

    private Arm _right;
    private Arm _left;

    public bool Aiming { get; set; } = false; // whether the player is aiming, which raises the hands

    // Per-hand state. Both hands share the aim sweep and swing amount; they differ in which
    // shoulder they hang off (anchor) and how the extension is signed (the seesaw, via `side`).
    private class Arm
    {
        public IKEffector effector;
        public Transform anchor;
        public Transform target;
        public float side;        // +1 right, -1 left
        public Quaternion gripCalib;
        public float drawReach;   // cached for the gizmo
    }

    void Awake()
    {
        if (input == null) input = GetComponent<PlayerInputReader>();
        if (_fbbik is null) _fbbik = GetComponentInChildren<FullBodyBipedIK>();
        if (aimPivot == null) aimPivot = transform.Find("CameraRig/CameraPivot");
        _ikSolver = _fbbik.solver;
    }

    void Start()
    {
        _ikSolver = _fbbik.solver;

        // Auto-wire the left-side references from the right ones if not assigned in the Inspector.
        if (leftGuardAnchor == null && guardAnchor != null && guardAnchor.parent != null)
            leftGuardAnchor = guardAnchor.parent.Find("clavicle_l");
        if (leftHandTarget == null && handTarget != null && handTarget.parent != null)
            leftHandTarget = handTarget.parent.Find("LeftHandTarget");

        _right = MakeArm(_ikSolver.rightHandEffector, guardAnchor,     handTarget,     +1f);
        _left  = MakeArm(_ikSolver.leftHandEffector,  leftGuardAnchor, leftHandTarget, -1f);

        _ikSolver.OnPreUpdate += UpdateHands;

        // Engage the body chain: let the arms straighten toward far targets instead of stopping
        // slightly bent (reach), and let extended hands drag the torso into the punch (pullBody).
        _ikSolver.GetChain(FullBodyBipedChain.RightArm).reach = 0.25f;
        _ikSolver.GetChain(FullBodyBipedChain.LeftArm).reach = 0.25f;
        _ikSolver.pullBodyHorizontal = 0.3f;

        // Lock & hide the cursor for gameplay (press Esc in the editor to free it).
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        if (_ikSolver != null) _ikSolver.OnPreUpdate -= UpdateHands;
    }

    private Arm MakeArm(IKEffector eff, Transform anchor, Transform target, float side)
    {
        var arm = new Arm { effector = eff, anchor = anchor, target = target, side = side, gripCalib = Quaternion.identity };

        if (eff != null && target != null && eff.bone != null && eff.bone.parent != null)
        {
            eff.target = target;
            eff.positionWeight = 1f;
            // Calibrate the hand's natural "pointing axis" (out along the forearm) onto "forward",
            // so aiming points the hand along the reach without twisting the wrist.
            Vector3 pointAxisLocal = Quaternion.Inverse(eff.bone.rotation)
                                   * (eff.bone.position - eff.bone.parent.position).normalized;
            arm.gripCalib = Quaternion.FromToRotation(pointAxisLocal, Vector3.forward);
        }
        return arm;
    }

    private void UpdateHands()
    {
        if (_right == null) return;

        // Punch direction lags the camera (~1/4s) so the swing's own inadvertent camera drift
        // (aimLookFactor) doesn't drag a punch already in flight off its committed line.
        // ponytail: 4 = commit lag rate, promote to a field if the feel needs tuning
        Vector3 camFwd = aimPivot != null ? aimPivot.forward : transform.forward;
        _punchFwd = _punchFwd == Vector3.zero ? camFwd
                  : Vector3.Slerp(_punchFwd, camFwd, 1f - Mathf.Exp(-4f * Time.deltaTime));

        // Fighter mechanics, camera untouched: the lead shoulder drives forward while the trail
        // shoulder pulls back (torso pivot) and the hips shift into the punch (weight transfer).
        // Bone-level positionOffsets reset every frame and stack on PlayerStepper's body target,
        // and nothing here rotates the player transform, so the camera never snaps.
        float frac = _extend / Mathf.Max(maxExtension, 0.01f);
        float twist = -_lean * frac * shoulderTwist; // pivot left = right shoulder forward
        _ikSolver.rightShoulderEffector.positionOffset += _punchFwd * twist;
        _ikSolver.leftShoulderEffector.positionOffset  -= _punchFwd * twist;
        _ikSolver.bodyEffector.positionOffset += _punchFwd * (frac * bodyLean);

        DriveArm(_right);
        DriveArm(_left);
    }

    private void DriveArm(Arm arm)
    {
        if (arm.anchor == null || arm.target == null || arm.effector == null) return;

        // Resting guard direction (mirror X for the left side so it rests on its own side).
        Vector3 rest = transform.forward * guardOffset.z
                     + transform.up      * guardOffset.y
                     + transform.right   * (guardOffset.x * arm.side);
        Vector3 baseDir = rest.normalized;

        // Both hands share the same sweep around the sphere.
        Vector3 dir = (baseDir
                     + transform.right * _aim.x
                     + transform.up    * _aim.y).normalized;

        // Punch extends from the guard TOWARD THE CROSSHAIR (aimPivot.forward carries camera
        // pitch). The hand leading the swing gets the full extension; the trailing hand
        // chambers back toward the body instead of punching too.
        // ponytail: chamber strength 0.5 inline; the weapon-target rework owns this logic later.
        Vector3 punchDir = _punchFwd;
        Vector3 punchRight = Vector3.Cross(Vector3.up, punchDir).normalized;
        // Pivot mechanics: swinging LEFT throws the RIGHT cross (and vice versa), hence -lean.
        float leadT = 0.5f - 0.5f * _lean * arm.side;                  // 1 = leads, 0 = trails
        float guardReach = baseReach - _extend * (1f - leadT) * 0.5f;  // trailing hand tucks in
        float extendThis = _extend * leadT;
        // Each fist lands to its OWN side of the crosshair, like a fighter pivoting into the
        // punch, instead of both darting at the same point.
        Vector3 spread = punchRight * (arm.side * punchSpread * extendThis / Mathf.Max(maxExtension, 0.01f));
        Vector3 offset = dir * guardReach + punchDir * extendThis + spread;
        if (offset.magnitude > _maxRadius) offset = offset.normalized * _maxRadius;
        arm.drawReach = offset.magnitude;

        Vector3 handPos = arm.anchor.position + offset;
        Quaternion handRot = Quaternion.LookRotation(offset.normalized, transform.up) * arm.gripCalib * Quaternion.Euler(gripEuler);

        // Aiming -> blend IK weight up to 1; releasing -> down to 0 (default arm pose).
        float w = Aiming ? 1f : 0f;
        float t = 1f - Mathf.Exp(-raiseSpeed * Time.deltaTime);
        arm.effector.positionWeight = Mathf.Lerp(arm.effector.positionWeight, w, t);
        arm.effector.rotationWeight = arm.effector.positionWeight * handRotationWeight;

        arm.target.SetPositionAndRotation(handPos, handRot);
    }

    void Update()
    {
        Aiming = forceAim || input.AttackHeld;

        // The guard sweep only follows the mouse when no punch is in flight — mid-punch, the
        // mouse's energy belongs to the punch, and the guard point must not slide under it.
        if (Aiming && _punchTime < 0f)
        {
            _aim += input.Look * aimSensitivity;
            _aim = Vector2.ClampMagnitude(_aim, aimClamp);
        }
        else if (!Aiming)
        {
            _aim = Vector2.Lerp(_aim, Vector2.zero, 1f - Mathf.Exp(-raiseSpeed * Time.deltaTime));
        }

        // A punch is a committed event, not a live mouse-follow: a fast enough flick LATCHES a
        // punch that drives out and auto-retracts on its own timer, ignoring the mouse mid-flight.
        // That commitment is what reads as "I threw a punch" instead of "my hand tracks the cursor".
        float speed = Aiming ? input.Look.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) : 0f;

        // Re-arm gate: after a punch, the mouse must SLOW DOWN below half the trigger before the
        // next one can latch. A deliberate flick punches; continuous steering doesn't machine-gun.
        if (!_armed && _punchTime < 0f && speed < 0.5f * swingTrigger * fullSwingSpeed)
            _armed = true;

        if (_armed && _punchTime < 0f && Aiming && speed >= swingTrigger * fullSwingSpeed)
        {
            _armed = false;
            _punchTime = 0f;                                       // latch a new punch
            _punchPower = Mathf.Clamp01(speed / fullSwingSpeed);   // harder flick = fuller reach
            float horiz = input.Look.x / Mathf.Max(Time.deltaTime, 0.0001f);
            _lean = Mathf.Clamp(horiz / fullSwingSpeed, -1f, 1f);  // freeze which hand leads for the whole punch
        }

        if (_punchTime >= 0f)
        {
            _punchTime += Time.deltaTime / Mathf.Max(punchDuration, 0.01f);
            // Asymmetric hump: t^0.75 skews the sin peak to ~40% in — snap out, longer retract.
            float t = Mathf.Pow(Mathf.Clamp01(_punchTime), 0.75f);
            _extend = _punchPower * maxExtension * Mathf.Sin(Mathf.PI * t);
            if (_punchTime >= 1f) { _punchTime = -1f; _extend = 0f; }
        }
        else
        {
            _extend = 0f;
            _lean = Mathf.Lerp(_lean, 0f, 1f - Mathf.Exp(-extendSmooth * Time.deltaTime)); // relax the pivot between punches
        }

        // ponytail: temp diagnostics for punch feel - rides the showReachSphere debug flag, delete with it
        if (showReachSphere && Aiming && Time.frameCount % 30 == 0)
            Debug.Log($"[HandDbg] speed={speed:F0}px/s punchT={_punchTime:F2} extend={_extend:F3}m power={_punchPower:F2}");
    }

    private void OnDrawGizmos()
    {
        if (!showReachSphere || !Application.isPlaying) return;
        DrawArmGizmo(_right);
        DrawArmGizmo(_left);
    }

    private void DrawArmGizmo(Arm arm)
    {
        if (arm == null || arm.anchor == null) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.22f);
        Gizmos.DrawSphere(arm.anchor.position, arm.drawReach);
        Gizmos.color = new Color(1f, 0f, 0f, 0.55f);
        Gizmos.DrawWireSphere(arm.anchor.position, arm.drawReach);
    }
}
