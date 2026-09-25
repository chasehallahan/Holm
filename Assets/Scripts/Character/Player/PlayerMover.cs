using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
public class PlayerMover : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Input reader for movement, sprint, and jump actions.")]
    [SerializeField] private PlayerInputReader input;

    [Tooltip("Character controller for physics-based movement.")]
    [SerializeField] private CharacterController controller;

    [Tooltip("Camera pivot whose forward direction determines movement orientation.")]
    [SerializeField] private Transform cameraPivot;

    [Header("Movement")]
    [Tooltip("Base walking speed in meters per second.")]
    [SerializeField] private float walkSpeed = 4f;

    [Tooltip("Speed multiplier applied while sprinting.")]
    [SerializeField] private float sprintMultiplier = 1.5f;

    [Tooltip("Acceleration rate when input is active (m/s²).")]
    [SerializeField] private float acceleration = 10f;

    [Tooltip("Acceleration rate when input is released (m/s²).")]
    [SerializeField] private float deceleration = 10f;

    [Header("Gravity / Jump")]
    [Tooltip("Gravity acceleration (negative = downward).")]
    [SerializeField] private float gravity = -20f;

    [Tooltip("Maximum jump height in meters.")]
    [SerializeField] private float jumpHeight = 1.2f;

    // Runtime State
    private Vector3 _velocity;
    private Vector2 _currentHorizVel;
    private Vector2 _moveInput;

    public float WalkSpeed => walkSpeed;
    public Vector3 MoveInputDirection => _moveInput.normalized;
    public bool WantsToMove => _moveInput.sqrMagnitude > 0.01f;

    void Awake()
    {
        RequireRef.Check(input, this, nameof(input));
        RequireRef.Check(controller, this, nameof(controller));
        RequireRef.Check(cameraPivot, this, nameof(cameraPivot));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RequireRef.Warn(input, this, nameof(input));
        RequireRef.Warn(controller, this, nameof(controller));
        RequireRef.Warn(cameraPivot, this, nameof(cameraPivot));
    }
#endif

    void Update()
    {
        _moveInput = Vector2.ClampMagnitude(input.Move, 1f);

        HandleMove();
        HandleGravityAndJump();
        controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleMove()
    {
        float targetSpeed = walkSpeed * (input.SprintHeld ? sprintMultiplier : 1f);

        // Project camera orientation to horizontal plane
        Vector3 camFwd = cameraPivot.forward;
        Vector3 camRight = cameraPivot.right;
        camFwd.y = 0f;
        camRight.y = 0f;
        camFwd.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camFwd * _moveInput.y + camRight * _moveInput.x;
        Vector2 targetHorizVel = new Vector2(moveDir.x, moveDir.z) * targetSpeed;

        float accel = targetHorizVel.sqrMagnitude > 0.001f ? acceleration : deceleration;
        _currentHorizVel = Vector2.MoveTowards(_currentHorizVel, targetHorizVel, accel * Time.deltaTime);

        _velocity.x = _currentHorizVel.x;
        _velocity.z = _currentHorizVel.y;

        // Rotate player toward camera direction while moving
        if (WantsToMove)
        {
            Quaternion targetRot = Quaternion.LookRotation(camFwd, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

    }

    private void HandleGravityAndJump()
    {
        bool grounded = controller.isGrounded;

        // Small downward velocity to keep player grounded
        if (grounded && _velocity.y < 0f) _velocity.y = -2f;

        if (grounded && input.JumpPressed) _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
    }
}