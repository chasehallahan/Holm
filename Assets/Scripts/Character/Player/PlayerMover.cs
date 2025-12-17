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

    [Tooltip("FBBIK root bone/pelvis Transform for tying body effector to character controller movement.")]
    [SerializeField] private Transform pelvis;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 30f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 20f;

    [Header("Gravity / Jump")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;

    private Vector2 _inputMove;
    private bool _isGrounded;
    private Vector3 _velocity;
    private Vector2 _currentHorizVel;
    private float _targetSpeed;
    private Transform _bodyIKTarget;


    // Public accessors for IK system
    public Vector3 Velocity => _velocity;
    public Vector2 DesiredMoveDir => _inputMove;
    public bool HasMoveInput => _inputMove.sqrMagnitude > 0.01f;
    public float DesiredMoveSpeed => _targetSpeed;


    void Awake()
    {
        if (!RequireRef.Check(input, this, nameof(input))) return;
        if (!RequireRef.Check(controller, this, nameof(controller))) return;
        if (!RequireRef.Check(cameraPivot, this, nameof(cameraPivot))) return;
        if (fbbik is null) fbbik = GetComponent<FullBodyBipedIK>();
        if (!RequireRef.Check(fbbik, this, nameof(fbbik))) return;
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

    private void Start()
    {
        if (_bodyIKTarget is null) _bodyIKTarget = new GameObject("BodyIKTarget").transform;
        
        _bodyIKTarget.position = pelvis.position;
        _bodyIKTarget.rotation = pelvis.rotation;
        _bodyIKTarget.SetParent(transform, true);

        fbbik.solver.bodyEffector.target = _bodyIKTarget;
    }

    // Update is called once per frame
    void Update()
    {
        _inputMove = Vector2.ClampMagnitude(input.Move, 1f);
        _isGrounded = controller.isGrounded;

        HandleMove();
        HandleGravityAndJump();

        controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleMove()
    {
        // TODO: Add fbbik body tilt
        Vector2 move = Vector2.ClampMagnitude(_inputMove, 1f);

        _targetSpeed = moveSpeed * (input.SprintHeld ? sprintMultiplier : 1f);

        Vector3 fwd = cameraPivot.forward;
        Vector3 right = cameraPivot.right;
        fwd.y = 0f; right.y = 0f;
        fwd.Normalize(); right.Normalize();

        Vector3 moveDirection = fwd * move.y + right * move.x;
        Vector2 targetHorizVel = new Vector2(moveDirection.x, moveDirection.z) * _targetSpeed;

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
