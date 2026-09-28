using UnityEngine;
using RootMotion.FinalIK;

[RequireComponent(typeof(PlayerInputReader), typeof(CharacterController))]
public class PlayerLooker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Input reader for look/mouse input.")]
    [SerializeField] private PlayerInputReader input;

    [Tooltip("Camera pivot to rotate for look direction.")]
    [SerializeField] private Transform cameraPivot;

    [Tooltip("Player rig containing FBBIK and LookAtIK components.")]
    [SerializeField] private GameObject playerRig;

    [Tooltip("Optional. While this hand handler is aiming the weapon, camera look is frozen so the mouse drives the hand instead (Half Sword style). Auto-found on this object if left empty.")]
    [SerializeField] private PlayerHandHandler handHandler;

    [Tooltip("While jabbing, mouse Y drives the jab scrub, so camera pitch locks. Auto-found on this object if left empty.")]
    [SerializeField] private SwingTarget swingTarget;

    [Header("Look Sensitivity")]
    [Tooltip("Mouse/controller look sensitivity multiplier.")]
    [SerializeField] private float lookSensitivity = 0.2f;

    [Tooltip("Maximum downward pitch angle from neutral.")]
    [Range(0f, 89f)]
    [SerializeField] private float maxLookDown = 60f;

    [Tooltip("Maximum upward pitch angle from neutral.")]
    [Range(0f, 89f)]
    [SerializeField] private float maxLookUp = 70f;

    [Tooltip("While aiming the weapon, how much the camera still follows the mouse. 0 = locked, 1 = full follow.")]
    [Range(0f, 1f)]
    [SerializeField] private float aimLookFactor = 0.25f;

    [Header("Body Rotation")]
    [Tooltip("Maximum yaw offset between view and body before body rotates to catch up.")]
    [Range(0f, 120f)]
    [SerializeField] private float maxUpperBodyTwist = 30f;

    [Tooltip("Body rotation correction speed. Higher = snappier catch-up.")]
    [SerializeField] private float bodyTurnSpeed = 8f;

    [Tooltip("Deadzone before body catch-up begins. Reduces micro-corrections.")]
    [SerializeField] private float bodyTurnDeadzone = 0f;

    [Header("Look Target")]
    [Tooltip("Distance in front of camera for IK look target placement.")]
    [Min(0.1f)]
    [SerializeField] private float targetDistance = 15f;

    [Tooltip("Look target position smoothing speed.")]
    [Range(0f, 200f)]
    [SerializeField] private float targetSmoothSpeed = 30f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;

    // Components
    private LookAtIK _lookAt;
    private Transform _eyes;
    private Transform _lookTarget;

    // State
    private float _pitch;
    private Quaternion _yaw;


#if UNITY_EDITOR
    private void OnValidate()
    {
        RequireRef.Warn(playerRig, this, nameof(playerRig));
        RequireRef.Warn(input, this, nameof(input));
        RequireRef.Warn(cameraPivot, this, nameof(cameraPivot));
    }
#endif

    void Awake()
    {
        if (input == null) input = GetComponent<PlayerInputReader>();
        RequireRef.Check(input, this, nameof(input));

        if (cameraPivot == null) cameraPivot = transform.Find("CameraRig/CameraPivot");
        RequireRef.Check(cameraPivot, this, nameof(cameraPivot));

        RequireRef.Check(playerRig, this, nameof(playerRig));

        if (handHandler == null) handHandler = GetComponent<PlayerHandHandler>();
        if (swingTarget == null) swingTarget = GetComponent<SwingTarget>();

        if (_lookAt == null) _lookAt = GetComponentInChildren<LookAtIK>();
        RequireRef.Check(_lookAt, this, nameof(_lookAt));

        if (_eyes == null) _eyes = _lookAt.solver.head.transform.Find("eyes");
        RequireRef.Check(_eyes, this, nameof(_eyes));

        _lookAt.solver.OnPostUpdate += PostIKFollow;
    }

    void Start()
    {
        // Create look target
        if (_lookTarget == null)
        {
            _lookTarget = new GameObject("LookTarget").transform;
            _lookTarget.SetParent(transform, true);
            _lookTarget.position = cameraPivot.position + cameraPivot.forward * targetDistance;
        }

        _lookAt.solver.target = _lookTarget;
        cameraPivot.position = _eyes.position;

        // Initialize yaw from current facing
        Vector3 flatFwd = Vector3.ProjectOnPlane(cameraPivot.forward, Vector3.up);
        if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = transform.forward;
        _yaw = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);

        // Initialize pitch
        _pitch = cameraPivot.localEulerAngles.x;
        if (_pitch > 180f) _pitch -= 360f;
    }

    void Update()
    {
        HandleLook();
    }

    void OnDestroy()
    {
        if (_lookAt != null)
            _lookAt.solver.OnPostUpdate -= PostIKFollow;
    }

    private void HandleLook()
    {
        if (input == null || cameraPivot == null) return;

        // Half Sword: while aiming, the camera still follows the mouse but only partway, so the
        // view drifts with your swing instead of locking dead.
        Vector2 look = input.Look;
        if (handHandler != null && handHandler.Aiming) look *= aimLookFactor;
        if (swingTarget != null && swingTarget.Jabbing) look.y = 0f; // jab owns the Y axis
        float yawDelta = look.x * lookSensitivity;
        float pitchDelta = look.y * lookSensitivity;

        // Update yaw (twist)
        _yaw *= Quaternion.AngleAxis(yawDelta, Vector3.up);

        // Update pitch
        _pitch = Mathf.Clamp(_pitch - pitchDelta, -maxLookDown, maxLookUp);

        // Calculate yaw offset between body and view
        Vector3 bodyFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 viewFwd = Vector3.ProjectOnPlane(_yaw * Vector3.forward, Vector3.up).normalized;
        float yawOffset = Vector3.SignedAngle(bodyFwd, viewFwd, Vector3.up);

        // Clamp view-to-body offset
        float clampedYawOffset = Mathf.Clamp(yawOffset, -maxUpperBodyTwist, maxUpperBodyTwist);
        float excessYaw = yawOffset - clampedYawOffset;

        // Rotate body to reduce excess yaw
        if (Mathf.Abs(excessYaw) > bodyTurnDeadzone)
        {
            float t = 1f - Mathf.Exp(-bodyTurnSpeed * Time.deltaTime);
            float bodyDelta = excessYaw * t;
            transform.rotation *= Quaternion.AngleAxis(bodyDelta, Vector3.up);

            // Recalculate offset after body rotation
            bodyFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            yawOffset = Vector3.SignedAngle(bodyFwd, viewFwd, Vector3.up);
            clampedYawOffset = Mathf.Clamp(yawOffset, -maxUpperBodyTwist, maxUpperBodyTwist);
        }

        // Apply camera pivot rotation relative to body
        Quaternion yawRel = Quaternion.AngleAxis(clampedYawOffset, Vector3.up);
        Quaternion pitchRel = Quaternion.AngleAxis(_pitch, Vector3.right);
        cameraPivot.localRotation = yawRel * pitchRel;
    }

    private void PostIKFollow()
    {
        Vector3 origin = _eyes.position;
        Vector3 desiredPos = origin + cameraPivot.forward * targetDistance;

        float t = 1f - Mathf.Exp(-targetSmoothSpeed * Time.deltaTime);
        _lookTarget.position = Vector3.Lerp(_lookTarget.position, desiredPos, t);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showDebugGizmos) return;

        if (_lookTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_lookTarget.position, 0.15f);
        }

        if (cameraPivot != null)
        {
            Gizmos.color = Color.cyan;
            if (_lookTarget != null)
                Gizmos.DrawLine(cameraPivot.position, _lookTarget.position);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(cameraPivot.position, cameraPivot.forward * targetDistance);
        }
    }
}