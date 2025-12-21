using RootMotion.FinalIK;
using UnityEngine;

[RequireComponent (typeof(CharacterController), typeof(PlayerInputReader))]
public class PlayerMover : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Custom PlayerInputReader script. Reads Move/Look/etc from the Input System wrapper on this GameObject.")]
    [SerializeField] private PlayerInputReader input;

    [Tooltip("Character Controller component in the base Player GameObject.")]
    [SerializeField] private CharacterController controller;

    [Tooltip("Transform that the Camera is parented under. We rotate this pivot (not the Camera directly).")]
    [SerializeField] private Transform cameraPivot;

    [Tooltip("FullBodyBipedIK solver whose foot effectors are driven by the generated stepper targets.")]
    [SerializeField] private FullBodyBipedIK fbbik;

    [Header("Movement")]
    [SerializeField] public float walkSpeed = 4f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField] private float deceleration = 12f;

    [Header("Gravity / Jump")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;

    private Vector3 _velocity;
    private Vector2 _currentHorizVel;
    private bool _wantsMove = false;

    public bool WantsMove => _wantsMove;


    void Awake()
    {
        RequireRef.Check(input, this, nameof(input));
        RequireRef.Check(controller, this, nameof(controller));
        RequireRef.Check(cameraPivot, this, nameof(cameraPivot));
        if (fbbik is null) fbbik = GetComponent<FullBodyBipedIK>();
        RequireRef.Check(fbbik, this, nameof(fbbik));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RequireRef.Warn(fbbik, this, nameof(fbbik));
        RequireRef.Warn(input, this, nameof(input));
        RequireRef.Warn(controller, this, nameof(controller));
        RequireRef.Warn(cameraPivot, this, nameof(cameraPivot));
    }
#endif

    void Update()
    {
        HandleMove();
        HandleGravityAndJump();

        controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleMove()
    {

        Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);

        _wantsMove = move.sqrMagnitude > 0f;

        float targetSpeed = walkSpeed * (input.SprintHeld ? sprintMultiplier : 1f);

        Vector3 fwd = cameraPivot.forward;
        Vector3 right = cameraPivot.right;
        fwd.y = 0f; right.y = 0f;
        fwd.Normalize(); right.Normalize();

        Vector3 moveDirection = fwd * move.y + right * move.x;
        Vector2 targetHorizVel = new Vector2(moveDirection.x, moveDirection.z) * targetSpeed;

        float currentAccel = targetHorizVel.sqrMagnitude > 0.001f ? acceleration : deceleration;
        _currentHorizVel = Vector2.MoveTowards(_currentHorizVel, targetHorizVel, currentAccel * Time.deltaTime);

        _velocity.x = _currentHorizVel.x;
        _velocity.z = _currentHorizVel.y;
    }

    private void HandleGravityAndJump()
    {
        bool grounded = controller.isGrounded;

        if (grounded && _velocity.y < 0f) _velocity.y = -2f;

        if (grounded && input.JumpPressed)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        _velocity.y += gravity * Time.deltaTime;
    }
}
