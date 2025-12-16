using UnityEngine;

[RequireComponent (typeof(CharacterController), typeof(PlayerInputReader))]
public class PlayerMover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform cameraPivot;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 30f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] public float maxSpeed = 20f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 20f;

    [Header("Gravity / Jump")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;

    private bool _isGrounded;
    private Vector3 _velocity;
    private Vector2 _currentHorizVel;


    // Public accessors for IK system
    public Vector3 Velocity => _velocity;
    public Vector3 HorizontalVelocity => new Vector3(_velocity.x, 0f, _velocity.z);
    public Vector2 MoveInput => input.Move;
    public bool IsGrounded => _isGrounded;
    public bool IsSprinting => input.SprintHeld;
    public float CurrentSpeed => _currentHorizVel.magnitude;


    private void Start()
    {

        if (controller is null) controller = GetComponent<CharacterController>();
        if (controller is null)
        {
            Debug.LogError("PlayerLooker: Character controller not assigned");
            enabled = false;
            return;
        }

        if (input is null) input = GetComponent<PlayerInputReader>();
        if (cameraPivot is null)
        {
            Debug.LogError("PlayerLooker: Camera pivot not assigned");
            enabled = false;
            return;
        }
        cameraPivot = cameraPivot.transform;
    }

    // Update is called once per frame
    void Update()
    {
        _isGrounded = controller.isGrounded;

        HandleMove();
        HandleGravityAndJump();

        controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleMove()
    {
        Vector2 inputMove = input.Move;
        inputMove = Vector2.ClampMagnitude(inputMove, 1f);

        float targetSpeed = moveSpeed * (input.SprintHeld ? sprintMultiplier : 1f);

        Vector3 fwd = cameraPivot.forward;
        Vector3 right = cameraPivot.right;
        fwd.y = 0f; right.y = 0f;
        fwd.Normalize(); right.Normalize();

        Vector3 moveDirection = fwd * inputMove.y + right * inputMove.x;
        Vector2 targetHorizVel = new Vector2(moveDirection.x, moveDirection.z) * moveSpeed;

        float currentAccel = targetHorizVel.sqrMagnitude > 0.001f ? acceleration : deceleration;
        _currentHorizVel = Vector2.MoveTowards(_currentHorizVel, targetHorizVel, currentAccel * Time.deltaTime);

        _velocity.x = _currentHorizVel.x;
        _velocity.z = _currentHorizVel.y;

    }

    private void HandleGravityAndJump()
    {
        if (_isGrounded && _velocity.y < 0f) _velocity.y = -2f;

        if (_isGrounded && input.JumpPressed)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        _velocity.y += gravity * Time.deltaTime;
    }
}
