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
    [SerializeField] private Vector3 guardOffset = new Vector3(0.05f, -0.25f, 0.4f);

    [Tooltip("Max distance a hand can reach from the shoulder (kept below true arm length).")]
    [SerializeField] private float _maxRadius = .55f;

    [Tooltip("Min distance a hand pulls in to when the OTHER hand is punching out (the chamber).")]
    [SerializeField] private float minReach = 0.12f;

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
    [Tooltip("Mouse speed (pixels/SECOND) at which you reach FULL extension. Higher = you must swing faster/harder.")]
    [SerializeField] private float fullSwingSpeed = 1000f;
    [Tooltip("Velocity response curve. 1 = linear; >1 = small flicks barely extend, only a real swing does.")]
    [SerializeField] private float extendCurve = 2f;
    [Tooltip("How fast the reach eases BACK IN after a swing (extension itself snaps out instantly).")]
    [SerializeField] private float extendSmooth = 14f;

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

    private Vector2 _aim;    // accumulated sweep around the sphere (where on the surface)
    private float _extend;   // current (smoothed) overall extension from swinging
    private float _lean;     // -1 = swinging left, +1 = swinging right (drives the seesaw)

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
        DriveArm(_right, _lean);
        DriveArm(_left, _lean);
    }

    private void DriveArm(Arm arm, float lean)
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

        // Seesaw: the hand leading the swing (lean toward its side) gets the full extension, the
        // trailing hand chambers in at half strength, and a straight jab (no lean) extends both a
        // quarter. Clamp between the chamber (minReach) and arm length.
        float share = 0.5f + 0.5f * lean * arm.side; // 1 = this hand leads the swing, 0 = it trails
        float reach = Mathf.Clamp(baseReach + _extend * (1.5f * share - 0.5f), minReach, _maxRadius);
        arm.drawReach = reach;

        Vector3 handPos = arm.anchor.position + dir * reach;
        Quaternion handRot = Quaternion.LookRotation(dir, transform.up) * arm.gripCalib * Quaternion.Euler(gripEuler);

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

        if (Aiming)
        {
            _aim += input.Look * aimSensitivity;
            _aim = Vector2.ClampMagnitude(_aim, aimClamp);
        }
        else
        {
            _aim = Vector2.Lerp(_aim, Vector2.zero, 1f - Mathf.Exp(-raiseSpeed * Time.deltaTime));
        }

        // Overall extension grows with how fast the mouse is moving (a swing), then eases back down.
        // Speed is pixels/second (delta / dt) so the feel is framerate-independent.
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = Aiming ? input.Look.magnitude / dt : 0f;
        float swing = Mathf.Pow(Mathf.Clamp01(speed / fullSwingSpeed), extendCurve);
        float extendTarget = swing * maxExtension;
        if (extendTarget > _extend)
            _extend = extendTarget; // a punch snaps out instantly...
        else
            _extend = Mathf.Lerp(_extend, extendTarget, 1f - Mathf.Exp(-extendSmooth * Time.deltaTime)); // ...and eases back in

        // Horizontal swing DIRECTION drives the seesaw: which hand punches out vs chambers.
        float horiz = Aiming ? input.Look.x / dt : 0f;
        float leanTarget = Mathf.Clamp(horiz / fullSwingSpeed, -1f, 1f);
        _lean = Mathf.Lerp(_lean, leanTarget, 1f - Mathf.Exp(-extendSmooth * Time.deltaTime));
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
