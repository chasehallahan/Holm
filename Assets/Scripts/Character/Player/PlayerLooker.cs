using RootMotion.FinalIK;
using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
public class PlayerLooker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Custom PlayerInputReader script. Reads Move/Look/etc from the Input System wrapper on this GameObject.")]
    [SerializeField] private PlayerInputReader input;

    [Tooltip("Transform that the Camera is parented under. We rotate/move this pivot (not the Camera directly).")]
    [SerializeField] private Transform cameraPivot;

    [Tooltip("Final IK LookAtIK component controlling head/spine look direction.")]
    [SerializeField] private LookAtIK lookAt;

    [Tooltip("Head bone transform. Used for camera follow if Eyes is not provided and/or for debugging.")]
    [SerializeField] private Transform headEndBone;

    [Tooltip("Preferred eye/eyes anchor transform. CameraPivot follows this position.")]
    [SerializeField] private Transform eyes;

    [Header("Look Sensitivity")]
    [Tooltip("Mouse/controller look sensitivity multiplier.")]
    [SerializeField] private float lookSensitivity = 20f;

    [Tooltip("Maximum angle (degrees) you can look downward from neutral pitch.")]
    [Range(0f, 89f)]
    [SerializeField] private float maxLookDown = 60f;

    [Tooltip("Maximum angle (degrees) you can look upward from neutral pitch.")]
    [Range(0f, 89f)]
    [SerializeField] private float maxLookUp = 60f;

    [Header("Body Rotation")]
    [Tooltip("Max allowed yaw (degrees) between body forward and view direction before the body starts turning to catch up.")]
    [Range(0f, 120f)]
    [SerializeField] private float maxUpperBodyTwist = 40f;

    [Tooltip("How fast (degrees/second) the body turns to reduce view-body yaw offset once beyond the twist limit.")]
    [SerializeField] private float bodyTurnSpeed = 80;

    [Tooltip("Extra degrees beyond MaxUpperBodyTwist before body catch-up begins. Helps avoid micro-corrections/jitter.")]
    [SerializeField] private float bodyTurnDeadzone = 5f;

    [Header("Look Target")]
    [Tooltip("Distance (meters) in front of the camera where the LookTarget is placed. Larger reduces cross-eye / extreme bending.")]
    [Min(0.1f)]
    [SerializeField] private float targetDistance = 15f;

    [Tooltip("Smoothing speed for the LookTarget position. Higher = snappier (less lag). Exponential smoothing.")]
    [Range(0f, 200f)]
    [SerializeField] private float targetPositionLerp = 30;

    [Tooltip("Smoothing speed for CameraPivot position following the Eyes transform. Higher = tighter, lower = floatier.")]
    [Range(0f, 200f)]
    [SerializeField] private float cameraPositionLerp = 120f;

    private Transform _lookTarget;
    private float _pitch;      // Camera pitch (up/down)
    private float _viewYaw;    // Camera yaw (left/right)
    private float _bodyYaw;    // Body/root yaw

    // Public accessors
    public Transform LookTarget => _lookTarget;
    public float Pitch => _pitch;
    public float ViewYaw => _viewYaw;
    public float BodyYaw => _bodyYaw;
    public Vector2 LookInput => input is not null ? input.Look : Vector2.zero;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (input == null) input = GetComponent<PlayerInputReader>();

        if (cameraPivot is null)
        {
            Debug.LogError("PlayerLooker: Camera pivot not assigned");
            enabled = false;
            return;
        }

        // Initialize rotations
        Vector3 currentRot = cameraPivot.localEulerAngles;
        _viewYaw = currentRot.y;
        _bodyYaw = transform.eulerAngles.y;
        _pitch = currentRot.x;

        if (lookAt is null) lookAt = GetComponentInChildren<LookAtIK>();
        if (lookAt is null)
        {
            Debug.LogWarning("PlayerLooker: No LookAtIK found.");
            enabled = false;
            return;
        }
        else
        {
            // Create look target if needed
            if (_lookTarget is null)
            {
                GameObject targetObj = new GameObject("LookTarget");
                _lookTarget = targetObj.transform;
            }

            lookAt.solver.target = _lookTarget;
            _lookTarget.position = cameraPivot.position + cameraPivot.forward * targetDistance;
        }

        if (headEndBone is null)
        {
            Debug.LogWarning("PlayerLooker: No head_end bone assigned.");
            enabled = false;
            return;
        }

        if (eyes is null)
        {
            Debug.LogWarning("PlayerLooker: No eyes transform assigned.");
            enabled = false;
            return;
        }
        cameraPivot.position = eyes.position;

    }

    void Update()
    {
        HandleLook();
    }

    void LateUpdate()
    {
        HandleLookIK();
        HandleCameraFollow();
    }

    private void HandleLook()
    {
        if (input == null || cameraPivot == null) return;

        Vector2 inputLook = input.Look;
        float mouseX = inputLook.x * lookSensitivity * Time.deltaTime;
        float mouseY = inputLook.y * lookSensitivity * Time.deltaTime;

        _viewYaw += mouseX;

        // Calculate how far camera is twisted from body
        float yawOffset = Mathf.DeltaAngle(_bodyYaw, _viewYaw);

        // Clamp the upper body twist
        float clampedOffset = Mathf.Clamp(yawOffset, -maxUpperBodyTwist, maxUpperBodyTwist);

        _pitch = Mathf.Clamp(_pitch - mouseY, -maxLookDown, maxLookUp);

        // Apply rotation to camera pivot (relative to body)
        cameraPivot.localRotation = Quaternion.Euler(_pitch, clampedOffset, 0f);

        // Turn body if we exceed the twist limit
        if (Mathf.Abs(yawOffset) > maxUpperBodyTwist + bodyTurnDeadzone)
        {
            // Smoothly turn body to catch up with camera
            float targetBodyYaw = _viewYaw - Mathf.Sign(yawOffset) * maxUpperBodyTwist;
            _bodyYaw = Mathf.MoveTowardsAngle(_bodyYaw, targetBodyYaw, bodyTurnSpeed * Time.deltaTime);

            // Apply body rotation
            transform.rotation = Quaternion.Euler(0f, _bodyYaw, 0f);
        }
    }

    private void HandleLookIK()
    {
        if (lookAt is null || _lookTarget is null || cameraPivot is null) return;

        // Calculate desired look target position
        Vector3 origin = cameraPivot.position;
        Vector3 dir = cameraPivot.forward;
        Vector3 desiredTargetPos = origin + dir * targetDistance;

        // Smooth the target movement
        float t = 1f - Mathf.Exp(-targetPositionLerp * Time.deltaTime);
        _lookTarget.position = Vector3.Lerp(_lookTarget.position, desiredTargetPos, t);
    }

    private void HandleCameraFollow()
    {
        if (cameraPivot is null || eyes is null) return;

        // Move camera pivot to follow eyes/head position
        Vector3 desired = eyes.position;
        float t = 1f - Mathf.Exp(-cameraPositionLerp * Time.deltaTime);
        cameraPivot.position = Vector3.Lerp(cameraPivot.position, desired, t);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw look target
        if (_lookTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_lookTarget.position, 0.15f);
        }

        // Draw camera info
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