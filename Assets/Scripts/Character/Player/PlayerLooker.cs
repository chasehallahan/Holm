using RootMotion.FinalIK;
using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
public class PlayerLooker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Custom PlayerInputReader script. Reads Move/Look/etc from the Input System wrapper on this GameObject.")]
    [SerializeField] private PlayerInputReader input;

    [Tooltip("Transform that the Camera is parented under. We rotate this pivot (not the Camera directly).")]
    [SerializeField] private Transform cameraPivot;

    [Tooltip("Final IK LookAtIK component controlling head/spine look direction.")]
    [SerializeField] private LookAtIK lookAt;

    [Tooltip("Head bone transform. Used for camera follow if Eyes is not provided and/or for debugging.")]
    [SerializeField] private Transform headEndBone;

    [Tooltip("Preferred eye/eyes anchor transform. CameraPivot follows this position.")]
    [SerializeField] private Transform eyes;

    [Header("Look Sensitivity")]
    [Tooltip("Mouse/controller look sensitivity multiplier.")]
    [SerializeField] private float lookSensitivity = 0.2f;

    [Tooltip("Maximum angle (degrees) you can look downward from neutral pitch.")]
    [Range(0f, 89f)]
    [SerializeField] private float maxLookDown = 60f;

    [Tooltip("Maximum angle (degrees) you can look upward from neutral pitch.")]
    [Range(0f, 89f)]
    [SerializeField] private float maxLookUp = 70f;

    [Header("Body Rotation")]
    [Tooltip("Max allowed yaw (degrees) between body forward and view direction before the body starts turning to catch up.")]
    [Range(0f, 120f)] [SerializeField] private float maxUpperBodyTwist = 30f;

    [Tooltip("How resposively the body turns to reduce view-body yaw offset once beyond the twist limit.")]
    [SerializeField] private float bodyTurnCorrection = 80f;

    [Tooltip("Extra degrees beyond MaxUpperBodyTwist before body catch-up begins. Helps avoid micro-corrections/jitter.")]
    [SerializeField] private float bodyTurnDeadzone = 0f;

    [Header("Look Target")]
    [Tooltip("Distance (meters) in front of the camera where the LookTarget is placed. Larger reduces cross-eye / extreme bending.")]
    [Min(0.1f)] [SerializeField] private float targetDistance = 15f;

    [Tooltip("Smoothing speed for the LookTarget position. Higher = snappier (less lag). Exponential smoothing.")]
    [Range(0f, 200f)] [SerializeField] private float targetPositionLerp = 30f;

    private Transform _lookTarget;
    private float _pitch;      // Camera pitch (up/down)
    private Quaternion _yaw;    // Camera yaw (left/right)

    // Public accessors
    //public Transform LookTarget => _lookTarget;
    //public float Pitch => _pitch;
    //public float ViewYaw => _viewYaw;
    //public float BodyYaw => _bodyYaw;
    //public Vector2 LookInput => input is not null ? input.Look : Vector2.zero;

    private void Awake()
    {
        if (input is null) input = GetComponent<PlayerInputReader>();
        if (!RequireRef.Check(input, this, nameof(input))) return;

        if (cameraPivot is null) cameraPivot = transform.Find("CameraRig/CameraPivot");
        if (!RequireRef.Check(cameraPivot, this, nameof(cameraPivot))) return;

        if (headEndBone is null) headEndBone = transform.Find("Armature/root/pelvis/spine/chest/neck/head/head_end");
        if (!RequireRef.Check(headEndBone, this, nameof(headEndBone))) return;

        if (eyes is null) eyes = headEndBone.GetChild(0);
        if (!RequireRef.Check(eyes, this, nameof(eyes))) return;

        if (lookAt is null) lookAt = GetComponent<LookAtIK>();
        if (!RequireRef.Check(lookAt, this, nameof(lookAt))) return;

        lookAt.solver.OnPostUpdate += PostIKFollow;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RequireRef.Warn(lookAt, this, nameof(lookAt));
        RequireRef.Warn(input, this, nameof(input));
        RequireRef.Warn(cameraPivot, this, nameof(cameraPivot));
        RequireRef.Warn(headEndBone, this, nameof(headEndBone));
        RequireRef.Warn(eyes, this, nameof(eyes));
    }
#endif

    void Reset()
    {
        lookAt = GetComponentInChildren<LookAtIK>();
        input = GetComponent<PlayerInputReader>();
        cameraPivot = transform.Find("CameraRig/CameraPivot");
        headEndBone = transform.Find("Armature/root/pelvis/spine/chest/neck/head/head_end");
        eyes = headEndBone.GetChild(0);
    }

    void Start()
    {
        // Create look target if needed
        if (_lookTarget is null)
        {
            _lookTarget = new GameObject("LookTarget").transform;
            _lookTarget.SetParent(transform, true);
            _lookTarget.position = cameraPivot.position + cameraPivot.forward * targetDistance;
        }

        lookAt.solver.target = _lookTarget;
        cameraPivot.position = eyes.position;

        // Initialize rotation angles (yaw left/right, pitch up/down)
        Vector3 flatFwd = Vector3.ProjectOnPlane(cameraPivot.forward, Vector3.up);
        if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = transform.forward;
        _yaw = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);
        _pitch = cameraPivot.localEulerAngles.x;
        if (_pitch > 180f) _pitch -= 360f;
    }

    void Update()
    {
        HandleLook();
    }

    private void HandleLook()
    {
        if (input is null || cameraPivot is null) return;

        Vector2 look = input.Look;
        float yawDelta = look.x * lookSensitivity;
        float pitchDelta = look.y * lookSensitivity;

        // Update camera yaw (left/right)
        _yaw *= Quaternion.AngleAxis(yawDelta, Vector3.up);

        // Update camera pitch (up/down) - clamped
        _pitch = Mathf.Clamp(_pitch - pitchDelta, -maxLookDown, maxLookUp);

        // Calculate how far camera is twisted (left/right) from Player
        Vector3 bodyFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 viewFwd = Vector3.ProjectOnPlane(_yaw * Vector3.forward, Vector3.up).normalized;
        float yawOffset = Vector3.SignedAngle(bodyFwd, viewFwd, Vector3.up);

        // Clamp how much the cameraPivot is allowed to twist relative to Player
        float clampedYawOffset = Mathf.Clamp(yawOffset, -maxUpperBodyTwist, maxUpperBodyTwist);
        float excessYaw = yawOffset - clampedYawOffset;

        if (Mathf.Abs(excessYaw) > bodyTurnDeadzone)
        {
            // Exponential body-turn smoothing factor
            float t = Mathf.Exp(bodyTurnCorrection * Time.deltaTime);

            // Rotate body by a fraction of the excess this frame
            float bodyDelta = excessYaw * t;
            transform.rotation = Quaternion.AngleAxis(bodyDelta, Vector3.up) * transform.rotation;

            // Recompute yaw offset after rotating body
            bodyFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            yawOffset = Vector3.SignedAngle(bodyFwd, viewFwd, Vector3.up);
            clampedYawOffset = Mathf.Clamp(yawOffset, -maxUpperBodyTwist, maxUpperBodyTwist);

        }

        // Apply local camera pivot rotation
        Quaternion yawRel = Quaternion.AngleAxis(clampedYawOffset, Vector3.up);
        Quaternion pitchRel = Quaternion.AngleAxis(_pitch, Vector3.right);
        cameraPivot.localRotation = yawRel * pitchRel;
    }

    void OnDestroy()
    {
        if (lookAt != null) lookAt.solver.OnPostUpdate -= PostIKFollow;
    }

    void PostIKFollow()
    { 
        // Calculate desired look target position
        Vector3 origin = eyes.position;
        Vector3 dir = cameraPivot.forward;
        Vector3 desiredTargetPos = origin + dir * targetDistance;

        // Smooth the target movement
        float t = 1f - Mathf.Exp(-targetPositionLerp * Time.deltaTime);
        _lookTarget.position = Vector3.Lerp(_lookTarget.position, desiredTargetPos, t);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw look target
        if (_lookTarget is not null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_lookTarget.position, 0.15f);
        }

        // Draw camera info
        if (cameraPivot is not null)
        {
            Gizmos.color = Color.cyan;
            if (_lookTarget is not null)
                Gizmos.DrawLine(cameraPivot.position, _lookTarget.position);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(cameraPivot.position, cameraPivot.forward * targetDistance);
        }
    }
}