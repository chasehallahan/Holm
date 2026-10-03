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

    [Tooltip("Jab/slap scrub target. While jabbing, the primary hand follows it. Auto-found if empty.")]
    [SerializeField] private SwingTarget swingTarget;

    [Header("Guard pose")]
    [Tooltip("Right shoulder bone the guard hangs off (e.g. clavicle_r).")]
    [SerializeField] private Transform guardAnchor;

    [Tooltip("Left shoulder bone (e.g. clavicle_l). Auto-found from the right one if left empty.")]
    [SerializeField] private Transform leftGuardAnchor;

    [Tooltip("Offset from the shoulder, in PLAYER space: x=right, y=up, z=forward (meters). X is mirrored for the left hand.")]
    [SerializeField] private Vector3 guardOffset = new Vector3(0.05f, -0.25f, 0.4f);

    [SerializeField] private float raiseSpeed = 8f; // how fast the hands raise/lower when you start/stop aiming

    [Header("Aim sweep (mouse orbits the hands around the shoulders)")]
    [Tooltip("Mouse delta -> how fast the aim sweeps around the sphere.")]
    [SerializeField] private float aimSensitivity = 0.004f;
    [Tooltip("Max sweep away from the resting guard direction (keeps the hands in front).")]
    [SerializeField] private float aimClamp = 0.8f;

    [Header("Guard reach")]
    [Tooltip("How far from the shoulder the hands rest in guard.")]
    [SerializeField] private float baseReach = 0.30f;
    [Tooltip("How far the hook bows out to the side at its widest (meters).")]
    [SerializeField] private float hookBow = 0.45f;

    [Header("Debug")]
    [Tooltip("Force aiming ON so the hands stay raised - lets you position/tune the guard without holding LMB. Uncheck when done.")]
    [SerializeField] private bool forceAim = true;
    [Tooltip("Draw the reach spheres (translucent red) in the Scene view while playing.")]
    [SerializeField] private bool showReachSphere = true;

    private Vector2 _aim;    // accumulated sweep around the sphere (where on the surface)

    private Arm _right;
    private Arm _left;
    private FBIKChain _rightArm;   // elbow bend goal rides this chain
    private Transform _elbowGoal;

    public bool Aiming { get; private set; } = false; // whether the player is aiming, which raises the hands

    // Per-hand state. Both hands share the aim sweep and swing amount; they differ in which
    // shoulder they hang off (anchor) and how the extension is signed (the seesaw, via `side`).
    private class Arm
    {
        public IKEffector effector;
        public Transform anchor;
        public Transform target;
        public float side;        // +1 right, -1 left
        public float drawReach;   // cached for the gizmo
    }

    void Awake()
    {
        if (input == null) input = GetComponent<PlayerInputReader>();
        if (_fbbik is null) _fbbik = GetComponentInChildren<FullBodyBipedIK>();
        if (aimPivot == null) aimPivot = transform.Find("CameraRig/CameraPivot");
        _ikSolver = _fbbik.solver;
        if (swingTarget == null) swingTarget = GetComponent<SwingTarget>();
    }

    void OnEnable()
    {
        if (_ikSolver != null) _ikSolver.OnPreUpdate += UpdateHands;
    }

    void Start()
    {
        _ikSolver = _fbbik.solver;
        _ikSolver.OnPreUpdate -= UpdateHands; // avoid double-subscribe from OnEnable racing Start
        _ikSolver.OnPreUpdate += UpdateHands;

        // Auto-wire the left-side references from the right ones if not assigned in the Inspector.
        if (leftGuardAnchor == null && guardAnchor != null && guardAnchor.parent != null)
            leftGuardAnchor = guardAnchor.parent.Find("clavicle_l");
        if (leftHandTarget == null && handTarget != null && handTarget.parent != null)
            leftHandTarget = handTarget.parent.Find("LeftHandTarget");

        _right = MakeArm(_ikSolver.rightHandEffector, guardAnchor,     handTarget,     +1f);
        _left  = MakeArm(_ikSolver.leftHandEffector,  leftGuardAnchor, leftHandTarget, -1f);

        // Elbow bend goal for the hook: the constraint pulls the ELBOW toward this transform
        // while the hand goes to its own target. Parented to the player so it travels with us.
        _rightArm = _ikSolver.GetChain(FullBodyBipedChain.RightArm);
        _elbowGoal = new GameObject("RightElbowGoal").transform;
        _elbowGoal.SetParent(transform, false);
        _rightArm.bendConstraint.bendGoal = _elbowGoal;

        // Lock & hide the cursor for gameplay (press Esc in the editor to free it).
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        if (_ikSolver != null) _ikSolver.OnPreUpdate -= UpdateHands;
    }

    private Arm MakeArm(IKEffector eff, Transform anchor, Transform target, float side)
    {
        var arm = new Arm { effector = eff, anchor = anchor, target = target, side = side };

        if (eff != null && target != null && eff.bone != null && eff.bone.parent != null)
        {
            eff.target = target;
            eff.positionWeight = 1f;
            eff.rotationWeight = 0f; // scene serializes 0.2; fists follow the arms until scrub-coupled rotation lands
        }
        return arm;
    }

    private void UpdateHands()
    {
        if (_right == null) return;
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

        // Hands rest at guard; all extension now comes from SwingTarget's scrub (v2).
        arm.drawReach = baseReach;

        Vector3 handPos = arm.anchor.position + dir * baseReach;

        if (arm.side > 0f && swingTarget != null)
        {
            _rightArm.bendConstraint.weight = 0f; // natural elbow unless a hook says otherwise

            if (swingTarget.Extend01 > 0f)
            {
                // Jab = blend from the LIVE guard position out to the crosshair far point.
                // Gated on the scrub VALUE so releasing RMB eases home instead of snapping.
                handPos = Vector3.Lerp(handPos, swingTarget.TargetPoint(), swingTarget.Extend01);
            }
            else if (Mathf.Abs(swingTarget.Swing01) > 0.0001f)
            {
                // Hook = quadratic bezier from the LIVE guard position, bowed to the swing's
                // side, into the crosshair far point. |Swing01| = progress along it, sign =
                // fronthand(+) vs backhand(-). Elevation rides TargetPoint's camera pitch.
                float s = swingTarget.Swing01;
                float p = Mathf.Abs(s);
                Vector3 far = swingTarget.TargetPoint();
                Vector3 mid = Vector3.Lerp(handPos, far, 0.5f) + aimPivot.right * (hookBow * Mathf.Sign(s));
                handPos = Vector3.Lerp(Vector3.Lerp(handPos, mid, p), Vector3.Lerp(mid, far, p), p);

                // A hook is an ELBOW event: flare it to the swing's side near shoulder height
                // so the forearm sweeps instead of spearing. Weight rides the swing progress.
                // ponytail: constants inline; knobs only if tuning demands them
                _elbowGoal.position = arm.anchor.position
                                    + aimPivot.right * (0.55f * Mathf.Sign(s))
                                    + Vector3.up * 0.05f;
                _rightArm.bendConstraint.weight = p;
            }
        }

        // Aiming -> blend IK weight up to 1; releasing -> down to 0 (default arm pose).
        float w = Aiming ? 1f : 0f;
        // Unpin the LEFT hand while the right punches: FBBIK drags the body into the punch
        // (pre-solve guard math can't see that drag), and a pinned left fist stays nailed to
        // its pre-punch world spot. Unpinned, it rides the body like a real guard.
        if (arm.side < 0f && swingTarget != null &&
            (swingTarget.Extend01 > 0f || Mathf.Abs(swingTarget.Swing01) > 0.0001f))
            w = 0f;
        float t = 1f - Mathf.Exp(-raiseSpeed * Time.deltaTime);
        arm.effector.positionWeight = Mathf.Lerp(arm.effector.positionWeight, w, t);

        arm.target.position = handPos;
    }

    void Update()
    {
        bool jabbing = input != null && input.BlockHeld;
        bool swinging = input != null && input.AttackHeld;

        Aiming = forceAim || swinging || jabbing;

        // Guard sweep follows the mouse only OUTSIDE a jab or swing - in a mode, the mouse belongs
        // to the scrub and the guard must hold still under it.
        if (Aiming && !jabbing && !swinging)
        {
            _aim += input.Look * aimSensitivity;
            _aim = Vector2.ClampMagnitude(_aim, aimClamp);
        }
        else if (!Aiming)
        {
            _aim = Vector2.Lerp(_aim, Vector2.zero, 1f - Mathf.Exp(-raiseSpeed * Time.deltaTime));
        }
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
