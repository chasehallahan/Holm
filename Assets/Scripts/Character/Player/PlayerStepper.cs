using RootMotion.FinalIK;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerMover))]
public class PlayerStepper : MonoBehaviour
{
    [Header("Stance Settings")]
    [Tooltip("Lateral distance from player center to each foot while idle.")]
    [SerializeField] private float stanceWidth = 0.1f;

    [Tooltip("Downward body offset to create natural knee bend while standing.")]
    [SerializeField][Range(0.04f, 0.16f)] private float stanceSlouch = 0.1f;


    [Header("Stride Settings")]
    [Tooltip("Lateral distance from player center to each foot while walking.")]
    [SerializeField] private float strideWidth = 0.08f;

    [Tooltip("Forward distance of each step at base walk speed.")]
    [SerializeField] private float strideLength = 0.3f;


    [Header("Step Settings")]
    [Tooltip("Minimum drift from ideal position before a step triggers.")]
    [SerializeField] private float stepTrigger = 0.05f;

    [Tooltip("Minimum step duration in seconds.")]
    [SerializeField][Range(0.01f, 0.3f)] private float minStepDuration = 0.1f;

    [Tooltip("Maximum step duration in seconds.")]
    [SerializeField][Range(0.01f, 0.3f)] private float maxStepDuration = 0.3f;

    [Tooltip("Foot lift height during steps.")]
    [SerializeField] private float walkStepHeight = 0.24f;

    [Tooltip("Step arc shape. X: step progress (0-1), Y: height multiplier (0-1).")]
    [SerializeField]
    private AnimationCurve stepHeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -3f, 0f)
    );


    [Header("Corrective Stepping Settings")]
    [Tooltip("Speed threshold (m/s) below which steps are corrective rather than locomotive.")]
    [SerializeField] private float correctiveSteppingThreshold = 0.2f;

    [Tooltip("Foot move speed (m/s) while corrective stepping (constrained by min/max step duration).")]
    [SerializeField] private float correctiveFootSpeed = 2f;


    [Header("Body Settings")]
    [Tooltip("How much the body dips at mid-stride (both feet spread).")]
    [SerializeField] private float bodyDip = 0.04f;

    [Tooltip("Body center of mass over a step. X: step progress (0-1), Y: height multiplier (0-1).")]
    [SerializeField]
    private AnimationCurve bodyDipCurve = new AnimationCurve(
    new Keyframe(0f, 0f, 0f, 3f),
    new Keyframe(0.5f, 1f, 0f, 0f),
    new Keyframe(1f, 0f, -3f, 0f)
    );

    [Tooltip("How quickly body motion responds to changes.")]
    [SerializeField] private float bodyMotionSmoothing = 10f;


    [Header("Feet Settings")]
    [Tooltip("Maximum forward/backward foot tilt in degrees (pitch).")]
    [SerializeField] private float maxAnklePitch = 30f;

    [Tooltip("Maximum side-to-side foot tilt in degrees (roll).")]
    [SerializeField] private float maxAnkleRoll = 15f;

    [Tooltip("Vertical offset from ankle bone to foot sole.")]
    [SerializeField] private float footToMesh = 0.08f;

    [Tooltip("(About) How wide is yo foot at the widest point.")]
    [SerializeField] private float footWidth = 0.107f;

    [Tooltip("(About) How long is yo foot at the longest point (heel to toe).")]
    [SerializeField] private float footLength = 0.279f;


    [Header("Ground Detection")]
    [Tooltip("Layers considered as walkable ground.")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("Raycast origin height above foot position.")]
    [SerializeField] private float probeUp = 1.5f;


    [Header("Debug")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private bool drawStepGizmos = true;
    [SerializeField] private float gizmoSphere = 0.04f;
    [SerializeField] private float gizmoLine = 0.15f;

    // Components
    private CharacterController _controller;
    private FullBodyBipedIK _fbbik;
    private IKSolverFullBodyBiped _ikSolver;
    private PlayerMover _playerMover;

    private float _walkSpeed;
    private float _playerAccel;

    // IK Structures
    private Foot _leftFoot;
    private Foot _rightFoot;
    private Thigh _leftThigh;
    private Thigh _rightThigh;
    private Body _body;

    // Runtime State
    private float _deltaTime;
    private Vector3 _xzVelocity = Vector3.zero;
    private float _xzSpeed = 0f;
    private bool _leftFootTurn = true;
    private bool _wasMoving = false;
    private float _currentBounce;
    private bool IsLocomotion => _playerMover.WantsToMove && _xzSpeed > correctiveSteppingThreshold;


    private class Foot
    {
        // Dynamic state
        public Vector3 plantedPos;
        public Quaternion plantedRot;
        public Vector3 stepFromPos;
        public Quaternion stepFromRot;
        public Vector3 stepToPos;
        public Quaternion stepToRot;
        public float stepDuration;
        public float stepProgress = 1f;
        public bool isStepping = false;
        public bool isCorrective = false;
        public Vector3 lastSolvedBonePos;

        // Immutable references
        public readonly bool isLeft;
        public readonly Transform bone;
        public readonly Transform ikTarget;
        public readonly Vector3 defaultLocalPos;
        public readonly Quaternion defaultLocalRot;
        public readonly Quaternion boneAxisOffset;

        public Foot(bool left, IKEffector effector, Transform player)
        {
            isLeft = left;
            bone = effector.bone;

            defaultLocalPos = player.InverseTransformPoint(bone.position);
            defaultLocalRot = Quaternion.Inverse(player.rotation) * bone.rotation;

            Quaternion flatFwd = Quaternion.LookRotation(player.forward, Vector3.up);
            boneAxisOffset = Quaternion.Inverse(flatFwd) * bone.rotation;

            // Create IK target at current bone position
            ikTarget = new GameObject(isLeft ? "LeftFootTarget" : "RightFootTarget").transform;
            ikTarget.SetParent(player, true);
            ikTarget.SetLocalPositionAndRotation(defaultLocalPos, defaultLocalRot);
            effector.target = ikTarget;

            plantedPos = ikTarget.position;
            plantedRot = ikTarget.rotation;
            stepProgress = 1f;
            isStepping = false;
        }
    }

    private class Thigh
    {
        public readonly bool isLeft;
        public readonly Transform bone;
        public readonly Transform ikTarget;
        public readonly Vector3 defaultLocalPos;
        public readonly Quaternion defaultLocalRot;

        public Thigh(bool left, IKEffector effector, Transform body)
        {
            isLeft = left;
            bone = effector.bone;

            defaultLocalPos = body.InverseTransformPoint(bone.position);
            defaultLocalRot = Quaternion.Inverse(body.rotation) * bone.rotation;

            ikTarget = new GameObject(isLeft ? "LeftThighTarget" : "RightThighTarget").transform;
            ikTarget.SetParent(body, true);
            ikTarget.SetLocalPositionAndRotation(defaultLocalPos, defaultLocalRot);
            effector.target = ikTarget;
        }
    }

    private class Body
    {
        public readonly Transform bone;
        public readonly Transform ikTarget;
        public readonly Vector3 defaultLocalPos;
        public readonly Quaternion defaultLocalRot;

        public Body(IKEffector effector, Transform player)
        {
            bone = effector.bone;
            defaultLocalPos = player.InverseTransformPoint(bone.position);
            defaultLocalRot = Quaternion.Inverse(player.rotation) * bone.rotation;

            ikTarget = new GameObject("BodyEffectorTarget").transform;
            ikTarget.SetParent(player, true);
            ikTarget.SetLocalPositionAndRotation(defaultLocalPos, defaultLocalRot);
            effector.target = ikTarget;
        }
    }

    void Awake()
    {
        if (_controller is null) _controller = GetComponent<CharacterController>();
        RequireRef.Check(_controller, this, nameof(_controller));

        if (_fbbik is null) _fbbik = GetComponentInChildren<FullBodyBipedIK>();
        RequireRef.Check(_fbbik, this, nameof(_fbbik));

        if (_playerMover is null) _playerMover = GetComponent<PlayerMover>();
        RequireRef.Check(_fbbik, this, nameof(_fbbik));
    }

    void Start()
    {
        _ikSolver = _fbbik.solver;
        _walkSpeed = _playerMover.WalkSpeed;

        InitializeFeet();
        InitializeBody();
        ConfigureEffectorWeights();

        // Hook into solver to avoid execution order issues
        _ikSolver.OnPreUpdate += UpdateIKTargets;
        _ikSolver.OnPostUpdate += CacheSolvedPositions;
    }

    private void InitializeFeet()
    {
        _leftFoot = new Foot(true, _ikSolver.leftFootEffector, transform);
        _rightFoot = new Foot(false, _ikSolver.rightFootEffector, transform);

        _leftFoot.plantedPos = _leftFoot.ikTarget.position;
        _rightFoot.plantedPos = _rightFoot.ikTarget.position;

        _leftFoot.plantedRot = _leftFoot.ikTarget.rotation;
        _rightFoot.plantedRot = _rightFoot.ikTarget.rotation;

        // Initialize cache to prevent first-frame artifacts
        _leftFoot.lastSolvedBonePos = _leftFoot.plantedPos;
        _rightFoot.lastSolvedBonePos = _rightFoot.plantedPos;
    }

    private void InitializeBody()
    {
        _body = new Body(_ikSolver.bodyEffector, transform);
        _leftThigh = new Thigh(true, _ikSolver.leftThighEffector, _body.ikTarget);
        _rightThigh = new Thigh(false, _ikSolver.rightThighEffector, _body.ikTarget);

        _body.ikTarget.localPosition -= Vector3.up * stanceSlouch;  // thighs need to intialize off default body position
    }

    private void ConfigureEffectorWeights()
    {
        _ikSolver.leftFootEffector.positionWeight = 1f;
        _ikSolver.rightFootEffector.positionWeight = 1f;
        _ikSolver.leftFootEffector.rotationWeight = 0.5f;
        _ikSolver.rightFootEffector.rotationWeight = 0.5f;

        _ikSolver.leftLegChain.bendConstraint.bendGoal = CreateBendGoal(_leftFoot);
        _ikSolver.rightLegChain.bendConstraint.bendGoal = CreateBendGoal(_rightFoot);
        _ikSolver.leftLegChain.bendConstraint.weight = 1f;
        _ikSolver.rightLegChain.bendConstraint.weight = 1f;

        _ikSolver.leftThighEffector.positionWeight = 0.2f;
        _ikSolver.rightThighEffector.positionWeight = 0.2f;
        _ikSolver.leftThighEffector.rotationWeight = 0.2f;
        _ikSolver.rightThighEffector.rotationWeight = 0.2f;

        _ikSolver.bodyEffector.positionWeight = 1f;
        _ikSolver.bodyEffector.rotationWeight = 0.5f;
        _ikSolver.bodyEffector.effectChildNodes = true; // "use thighs" in inspector
    }
    private Transform CreateBendGoal(Foot foot)
    {
        bool left = foot.isLeft;
        Transform goal = new GameObject(left ? "LeftKneeGoal" : "RightKneeGoal").transform;
        goal.SetParent(transform);
        goal.localPosition = new Vector3(left ? -stanceWidth : stanceWidth, 0.5f, 0.5f);
        return goal;
    }

    private void OnDestroy()
    {
        if (_ikSolver == null) return;
        _ikSolver.OnPreUpdate -= UpdateIKTargets;
        _ikSolver.OnPostUpdate -= CacheSolvedPositions;
    }

    private void CacheSolvedPositions()
    {
        if (_leftFoot != null) _leftFoot.lastSolvedBonePos = _leftFoot.bone.position;
        if (_rightFoot != null) _rightFoot.lastSolvedBonePos = _rightFoot.bone.position;
    }

    void Update()
    {
        if (!_controller.isGrounded) return;

        _deltaTime = Time.deltaTime;

        _xzVelocity = _controller.velocity;
        _xzVelocity.y = 0f;
        _xzSpeed = _xzVelocity.magnitude;

        bool isLocomotion = _playerMover.WantsToMove && _xzSpeed > 0.05f;
    
        // Detect movement onset - force immediate step
        if (isLocomotion && !_wasMoving && !_leftFoot.isStepping && !_rightFoot.isStepping)
        {
            ForceFirstStep();
        }
        _wasMoving = isLocomotion;

        AdvanceStep(_leftFoot);
        AdvanceStep(_rightFoot);

        CheckForSteps();
    }

    private void ForceFirstStep()
    {
        // Pick the foot that's further back relative to movement direction
        Vector3 moveDir = _xzVelocity.normalized;

        float leftDot = Vector3.Dot(_leftFoot.plantedPos - transform.position, moveDir);
        float rightDot = Vector3.Dot(_rightFoot.plantedPos - transform.position, moveDir);

        // More negative = further behind in movement direction
        Foot backFoot = leftDot < rightDot ? _leftFoot : _rightFoot;

        Vector3 targetXZ = GetStepTargetXZ(backFoot);
        StartStep(backFoot, targetXZ);
        _leftFootTurn = !backFoot.isLeft;
    }

    private void AdvanceStep(Foot foot)
    {
        if (!foot.isStepping) return;

        foot.stepProgress += _deltaTime / foot.stepDuration;

        if (foot.stepProgress >= 1f)
        {
            foot.stepProgress = 1f;
            foot.isStepping = false;
            foot.isCorrective = false;
            foot.plantedPos = foot.stepToPos;
            foot.plantedRot = foot.stepToRot;
        }
    }

    private void CheckForSteps()
    {
        // TODO: Allow step overlap for running/sprinting
        if (_leftFoot.isStepping || _rightFoot.isStepping) return;

        Vector3 leftTargetXZ = GetStepTargetXZ(_leftFoot);
        Vector3 rightTargetXZ =  GetStepTargetXZ(_rightFoot);

        float leftDrift = HorizontalDistance(_leftFoot.plantedPos, leftTargetXZ);
        float rightDrift = HorizontalDistance(_rightFoot.plantedPos, rightTargetXZ);

        bool leftNeeds = leftDrift > stepTrigger;
        bool rightNeeds = rightDrift > stepTrigger;

        if (!leftNeeds && !rightNeeds) return;

        // Single foot needs to step
        if (leftNeeds != rightNeeds)
        {
            if (leftNeeds)
            {
                StartStep(_leftFoot, leftTargetXZ);
                _leftFootTurn = false;
            }
            else
            {
                StartStep(_rightFoot, rightTargetXZ);
                _leftFootTurn = true;
            }
            return;
        }

        // Both need to step — prioritize urgency, then alternate
        bool leftUrgent = leftDrift > rightDrift * 1.5f;
        bool rightUrgent = rightDrift > leftDrift * 1.5f;

        if (leftUrgent || (!rightUrgent && _leftFootTurn))
        {
            StartStep(_leftFoot, leftTargetXZ);
            _leftFootTurn = false;
        }
        else
        {
            StartStep(_rightFoot, rightTargetXZ);
            _leftFootTurn = true;
        }
    }

    private void StartStep(Foot foot, Vector3 targetXZ)
    {
        foot.isStepping = true;
        foot.isCorrective = !IsLocomotion;
        foot.stepFromPos = foot.lastSolvedBonePos;
        foot.stepFromRot = foot.bone.rotation;
        foot.stepToPos = ProjectFootToGround(foot, targetXZ, out Quaternion targetRot);
        foot.stepToRot = targetRot;
        foot.stepDuration = GetStepDuration(foot);
        foot.stepProgress = 0f;
    }

    private Vector3 GetStepTargetXZ(Foot foot)
    {
        float side = foot.isLeft ? -1f : 1f;

        if (IsLocomotion)
        {
            Vector3 moveDir = _xzVelocity.normalized;
            float speedRatio = Mathf.Clamp01(_xzSpeed / _walkSpeed);
            float currentStride = strideLength * Mathf.Max(speedRatio, 0.4f);

            // Predict where body will be when step lands
            float footSpeed = _walkSpeed * 2f;
            float estimatedDuration = currentStride / footSpeed;
            Vector3 predictedBodyPos = transform.position + _xzVelocity * estimatedDuration;

            Vector3 forward = moveDir * (currentStride * 0.5f);
            Vector3 lateral = Vector3.Cross(Vector3.up, moveDir) * (side * strideWidth);

            return predictedBodyPos + forward + lateral;
        }
        else
        {
            return transform.position + transform.right * (side * stanceWidth);
        }
    }

    private float GetStepDuration(Foot foot)
    {
        float distance = Vector3.Distance(foot.stepFromPos, foot.stepToPos);

        if (foot.isCorrective)
        {
            return Mathf.Clamp(distance / correctiveFootSpeed, minStepDuration, maxStepDuration);
        }
        else
        {
            float footSpeed = _walkSpeed * 2f;
            return Mathf.Clamp(distance / footSpeed, minStepDuration, maxStepDuration);
        }

    }

    private void UpdateIKTargets()
    {
        UpdateBodyIK(_body);
        UpdateFootIK(_leftFoot);
        UpdateFootIK(_rightFoot);
    }

    private void UpdateBodyIK(Body body)
    {

        Vector3 basePos = body.defaultLocalPos - Vector3.up * stanceSlouch;

        bool stepping = _leftFoot.isStepping || _rightFoot.isStepping;
        bool corrective = _leftFoot.isCorrective || _rightFoot.isCorrective;
        if (!stepping || corrective)
        {
            body.ikTarget.localPosition = Vector3.Lerp(body.ikTarget.localPosition, basePos, bodyMotionSmoothing * _deltaTime);
            return;
        }

        // Get step progress from whichever foot is stepping (or use 0 if neither)
        float stepProgress = 0f;
        if (_leftFoot.isStepping) stepProgress = _leftFoot.stepProgress;
        else if (_rightFoot.isStepping) stepProgress = _rightFoot.stepProgress;

        // Calculate target bounce (dip at mid-step)
        float targetBounce = 0f;
        targetBounce = bodyDipCurve.Evaluate(stepProgress) * bodyDip;

        // Smooth transitions
        _currentBounce = Mathf.Lerp(_currentBounce, targetBounce, bodyMotionSmoothing * _deltaTime);

        // Apply bounce (vertical offset)
        body.ikTarget.localPosition = basePos - Vector3.up * _currentBounce;
    }

    private void UpdateFootIK(Foot foot)
    {
        Vector3 pos;
        Quaternion rot;

        if (foot.isStepping)
        {
            float t = Mathf.SmoothStep(0f, 1f, foot.stepProgress);
            pos = Vector3.Lerp(foot.stepFromPos, foot.stepToPos, t);

            float elevationChange = foot.stepToPos.y - foot.stepFromPos.y;
            float stepHeight = walkStepHeight;

            // If stepping up, add extra height so peak clears destination
            if (elevationChange > 0f)
            {
                stepHeight = Mathf.Max(walkStepHeight, elevationChange * 0.5f + 0.02f);
            }

            pos.y += stepHeightCurve.Evaluate(foot.stepProgress) * stepHeight;

            // TODO: make this a serialized curve for foot heel lift to toe down to flat to land
            float rotT = foot.stepProgress < 0.5f
                ? Mathf.SmoothStep(0f, 0.3f, foot.stepProgress * 2f)      // slow out of start rotation
                : Mathf.SmoothStep(0.3f, 1f, (foot.stepProgress - 0.5f) * 2f); // ease into end rotation
            rot = Quaternion.Slerp(foot.stepFromRot, foot.stepToRot, rotT);
        }
        else
        {
            pos = foot.plantedPos;
            rot = foot.plantedRot;
        }

        foot.ikTarget.SetPositionAndRotation(pos, rot);
    }

    private Vector3 ProjectFootToGround(Foot foot, Vector3 position, out Quaternion groundRot)
    {
        Vector3 hipForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        float radius = footWidth * 0.5f;
        float halfLength = (footLength - footWidth) * 0.5f;

        // Elevate origin for downward cast
        Vector3 origin = position + Vector3.up * probeUp;
        Vector3 point1 = origin - hipForward * halfLength; // heel
        Vector3 point2 = origin + hipForward * halfLength; // toe

        if (!Physics.CapsuleCast(point1, point2, radius, Vector3.down, out RaycastHit hit, probeUp * 3f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            groundRot = Quaternion.LookRotation(hipForward, Vector3.up);
            return new Vector3(position.x, transform.position.y, position.z);
        }

        groundRot = CalculateFootRotation(foot, hipForward, hit.normal);
        return hit.point + Vector3.up * footToMesh;
    }

    private Quaternion CalculateFootRotation(Foot foot, Vector3 hipForward, Vector3 groundNormal)
    {
        Quaternion flatRot = Quaternion.LookRotation(hipForward, Vector3.up);

        // Project forward onto slope
        Vector3 groundForward = Vector3.ProjectOnPlane(hipForward, groundNormal);
        Quaternion groundRot = Quaternion.LookRotation(groundForward.normalized, groundNormal);

        // Get delta in local space
        Quaternion delta = Quaternion.Inverse(flatRot) * groundRot;
        Vector3 localEuler = delta.eulerAngles;

        // Normalize to -180..180 before clamping
        float pitch = NormalizeAngle(localEuler.x);
        float roll = NormalizeAngle(localEuler.z);

        pitch = Mathf.Clamp(pitch, -maxAnklePitch, maxAnklePitch);
        roll = Mathf.Clamp(roll, -maxAnkleRoll, maxAnkleRoll);

        Quaternion clampedLocal = Quaternion.Euler(pitch, 0f, roll);

        return flatRot * clampedLocal * foot.boneAxisOffset;
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmos()
    {
        if (!showDebug || !drawStepGizmos || !Application.isPlaying) return;
        if (_leftFoot == null || _rightFoot == null) return;

        void DrawFootDebug(Foot foot)
        {
            if (foot == null) return;

            // Planted (where the foot is "anchored")
            Gizmos.color = foot.isLeft ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 0.6f, 1f);
            Gizmos.DrawWireSphere(foot.plantedPos, gizmoSphere);

            // IK target (actual effector target each frame)
            if (foot.ikTarget != null)
            {
                Gizmos.color = foot.isLeft ? new Color(1f, 0f, 0f, 0.9f) : new Color(0f, 0.4f, 1f, 0.9f);
                Gizmos.DrawSphere(foot.ikTarget.position, gizmoSphere * 0.65f);
                Gizmos.DrawLine(foot.ikTarget.position, foot.ikTarget.position + foot.ikTarget.forward * gizmoLine);
            }

            // Step path (only if stepping)
            if (foot.isStepping)
            {
                // Start of step
                Gizmos.color = new Color(1f, 1f, 0f, 0.9f); // yellow
                Gizmos.DrawWireSphere(foot.stepFromPos, gizmoSphere * 0.9f);

                // Destination
                Gizmos.color = new Color(0f, 1f, 0f, 0.9f); // green
                Gizmos.DrawWireSphere(foot.stepToPos, gizmoSphere * 0.9f);

                // Line from start to destination
                Gizmos.DrawLine(foot.stepFromPos, foot.stepToPos);

                // Progress point along the step
                float t = Mathf.Clamp01(foot.stepProgress);
                Vector3 prog = Vector3.Lerp(foot.stepFromPos, foot.stepToPos, t);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f); // orange
                Gizmos.DrawSphere(prog, gizmoSphere * 0.55f);
            }
        }

        DrawFootDebug(_leftFoot);
        DrawFootDebug(_rightFoot);

        if (_leftThigh?.ikTarget != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(_leftThigh.ikTarget.position, gizmoSphere);
        }
        if (_rightThigh?.ikTarget != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(_rightThigh.ikTarget.position, gizmoSphere);
        }
    }

    // - TODO:
    // - knees, bend targets, and goals
    // - foot rotation and hip rotation still kinda wonky
    // - figure out lean with velocity
    // - - its both a body effector position and rotational change about the pelvis
    // - - - prob just do a new target for lean, initiated @ body ik target pos/rot parented to pelvis. push body effector towards that delta
    // - add early exit to advance step if velocity has dropped or direction switched in the last bit of the step
    // - - **important, it's ugly** also early exity to body dip for corrective stepping. maybe just a foot.iscorrective at this point
    // - - body dip should probably also scale off velocity
    // - legs cross on strafe - this is gonna need a whole logic for leg crossing in general
}