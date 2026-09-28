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

    [Tooltip("Minimum drift from ideal position before a step triggers.")]
    [SerializeField] private float stepTrigger = 0.08f;

    [Tooltip("Step duration in seconds.")]
    [SerializeField] [Range(0.01f, 0.3f)] private float strideDuration = 0.2f;

    [Tooltip("Maximum foot lift height during steps.")]
    [SerializeField] private float stepHeight = 0.24f;

    [Tooltip("Step arc shape. X: step progress (0-1), Y: height multiplier (0-1).")]
    [SerializeField]
    private AnimationCurve stepHeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -3f, 0f)
    );

    [Tooltip("Speed threshold (m/s) below which steps are corrective rather than directional.")]
    [SerializeField] private float correctiveSteppingThreshold = 0.05f;

    [Tooltip("Time in seconds to predict ahead for step placement.")]
    [SerializeField] private float lookAheadTime = 0.05f;

    [Header("Planted Foot Correction")]
    [Tooltip("Max allowed drift between IK target and solved bone before sliding correction.")]
    [SerializeField] private float maxPlantDivergence = 0.03f;

    [Tooltip("Max correction slide speed (m/s) to prevent visible snapping.")]
    [SerializeField] private float plantSlideSpeed = 0.6f;

    [Header("Ground Detection")]
    [Tooltip("Vertical offset from ankle bone to foot sole.")]
    [SerializeField] private float footToMesh = 0.08f;

    [Tooltip("Layers considered as walkable ground.")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("Raycast origin height above foot position.")]
    [SerializeField] private float probeUp = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Components
    private CharacterController _controller;
    private FullBodyBipedIK _fbbik;
    private IKSolverFullBodyBiped _ikSolver;

    // IK Structures
    private Foot _leftFoot;
    private Foot _rightFoot;
    private Thigh _leftThigh;
    private Thigh _rightThigh;
    private Body _body;

    // Runtime State
    private Vector3 _velocity;
    private bool _leftFootTurn = true;


    private class Foot
    {
        // Dynamic state
        public Vector3 plantedPos;
        public Vector3 stepFromPos;
        public Vector3 stepToPos;
        public float stepDuration;
        public float stepProgress = 1f;
        public bool isStepping = false;
        public Vector3 lastSolvedBonePos;

        // Immutable references
        public readonly bool isLeft;
        public readonly Transform bone;
        public readonly Transform ikTarget;
        public readonly Vector3 defaultLocalPos;
        public readonly Quaternion defaultLocalRot;

        public Foot(bool left, IKEffector effector, Transform player)
        {
            isLeft = left;
            bone = effector.bone;

            defaultLocalPos = player.InverseTransformPoint(bone.position);
            defaultLocalRot = Quaternion.Inverse(player.rotation) * bone.rotation;

            // Create IK target at current bone position
            ikTarget = new GameObject(isLeft ? "LeftFootTarget" : "RightFootTarget").transform;
            ikTarget.SetParent(player, true);
            ikTarget.SetLocalPositionAndRotation(defaultLocalPos, defaultLocalRot);
            effector.target = ikTarget;

            plantedPos = ikTarget.position;
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

        public Thigh(bool left, IKEffector effector, Transform player)
        {
            isLeft = left;
            bone = effector.bone;

            defaultLocalPos = player.InverseTransformPoint(bone.position);
            defaultLocalRot = Quaternion.Inverse(player.rotation) * bone.rotation;

            ikTarget = new GameObject(isLeft ? "LeftThighTarget" : "RightThighTarget").transform;
            ikTarget.SetParent(player.transform, true);
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
            ikTarget.SetParent(player.transform, true);
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
    }

    void Start()
    {
        _ikSolver = _fbbik.solver;

        InitializeFeet();
        InitializeThighs();
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

        // Initialize cache to prevent first-frame artifacts
        _leftFoot.lastSolvedBonePos = _leftFoot.plantedPos;
        _rightFoot.lastSolvedBonePos = _rightFoot.plantedPos;
    }

    private void InitializeThighs()
    {
        _leftThigh = new Thigh(true, _ikSolver.leftThighEffector, transform);
        _rightThigh = new Thigh(false, _ikSolver.rightThighEffector, transform);
    }

    private void InitializeBody()
    {
        _body = new Body(_ikSolver.bodyEffector, transform);
        _body.ikTarget.localPosition -= Vector3.up * stanceSlouch;
    }

    private void ConfigureEffectorWeights()
    {
        _ikSolver.leftFootEffector.positionWeight = 1f;
        _ikSolver.rightFootEffector.positionWeight = 1f;
        _ikSolver.leftFootEffector.rotationWeight = 1f;
        _ikSolver.rightFootEffector.rotationWeight = 1f;

        _ikSolver.leftThighEffector.positionWeight = 0.5f;
        _ikSolver.rightThighEffector.positionWeight = 0.5f;
        _ikSolver.leftThighEffector.rotationWeight = 0.5f;
        _ikSolver.rightThighEffector.rotationWeight = 0.5f;

        _ikSolver.bodyEffector.positionWeight = 1f;
        _ikSolver.bodyEffector.rotationWeight = 1f;
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

        UpdateVelocity();
        AdvanceStep(_leftFoot);
        AdvanceStep(_rightFoot);

        // Correct planted foot drift caused by IK leg length constraints
        CorrectPlantedFoot(_leftFoot);
        CorrectPlantedFoot(_rightFoot);

        CheckForSteps();
    }

    private void CorrectPlantedFoot(Foot foot)
    {
        if (foot.isStepping) return;

        // Compare horizontal positions only
        Vector3 planted = foot.plantedPos; planted.y = 0f;
        Vector3 solved = foot.lastSolvedBonePos; solved.y = 0f;

        Vector3 delta = solved - planted;
        float dist = delta.magnitude;

        if (dist <= maxPlantDivergence) return;

        // Slide toward solved position, clamped to max speed
        float desiredMove = dist - maxPlantDivergence;
        float maxMoveThisFrame = plantSlideSpeed * Time.deltaTime;
        float move = Mathf.Min(desiredMove, maxMoveThisFrame);

        foot.plantedPos += (delta / dist) * move;
        foot.plantedPos = ProjectToGround(foot.plantedPos);
    }

    private void UpdateVelocity()
    {
        _velocity = _controller.velocity;
        _velocity.y = 0f;
    }

    private void AdvanceStep(Foot foot)
    {
        if (!foot.isStepping) return;

        foot.stepProgress += Time.deltaTime / foot.stepDuration;

        if (foot.stepProgress >= 1f)
        {
            foot.stepProgress = 1f;
            foot.isStepping = false;
            foot.plantedPos = foot.stepToPos;
        }
    }

    private void CheckForSteps()
    {
        // TODO: Allow step overlap for running/sprinting
        if (_leftFoot.isStepping || _rightFoot.isStepping) return;

        Vector3 leftTarget = GetStepTarget(_leftFoot);
        Vector3 rightTarget = GetStepTarget(_rightFoot);

        float leftDrift = HorizontalDistance(_leftFoot.plantedPos, leftTarget);
        float rightDrift = HorizontalDistance(_rightFoot.plantedPos, rightTarget);

        bool leftNeeds = leftDrift > stepTrigger;
        bool rightNeeds = rightDrift > stepTrigger;

        if (!leftNeeds && !rightNeeds) return;

        // Single foot needs to step
        if (leftNeeds != rightNeeds)
        {
            if (leftNeeds)
            {
                StartStep(_leftFoot);
                _leftFootTurn = false;
            }
            else
            {
                StartStep(_rightFoot);
                _leftFootTurn = true;
            }
            return;
        }

        // Both need to step — prioritize urgency, then alternate
        bool leftUrgent = leftDrift > rightDrift * 1.5f;
        bool rightUrgent = rightDrift > leftDrift * 1.5f;

        if (leftUrgent || (!rightUrgent && _leftFootTurn))
        {
            StartStep(_leftFoot);
            _leftFootTurn = false;
        }
        else
        {
            StartStep(_rightFoot);
            _leftFootTurn = true;
        }
    }

    private void StartStep(Foot foot)
    {
        foot.isStepping = true;
        foot.stepFromPos = foot.ikTarget.position;
        foot.stepToPos = GetStepTarget(foot);
        foot.stepDuration = GetStepDuration(foot);
        foot.stepProgress = 0f;
    }

    private Vector3 GetStepTarget(Foot foot)
    {
        float side = foot.isLeft ? -1f : 1f;

        // Directional stepping: place foot ahead of predicted position
        if (_velocity.sqrMagnitude > correctiveSteppingThreshold * correctiveSteppingThreshold)
        {
            Vector3 fwdDir = _velocity.normalized;
            Vector3 rightDir = Vector3.Cross(Vector3.up, fwdDir).normalized;

            float predictTime = strideDuration * 0.5f + lookAheadTime;
            Vector3 predictedPos = transform.position + _velocity * predictTime;

            Vector3 target = predictedPos + fwdDir * strideLength + rightDir * (side * strideWidth);
            return ProjectToGround(target);
        }

        // Corrective stepping: return to stance position under pelvis
        Vector3 stanceTarget = transform.position + transform.right * (side * stanceWidth);
        return ProjectToGround(stanceTarget);
    }

    private float GetStepDuration(Foot foot)
    {
        // TODO: figure this out, do we need it? Does it need a tuning variable?
        return strideDuration;
    }

    private void UpdateIKTargets()
    {
        UpdateBodyIK(_body);
        UpdateFootIK(_leftFoot);
        UpdateFootIK(_rightFoot);
    }

    private void UpdateBodyIK(Body body)
    {
        // TODO: Body bob/sway logic
    }

    private void UpdateFootIK(Foot foot)
    {
        Vector3 pos;

        if (foot.isStepping)
        {
            float t = Mathf.SmoothStep(0f, 1f, foot.stepProgress);
            pos = Vector3.Lerp(foot.stepFromPos, foot.stepToPos, t);
            pos.y += stepHeightCurve.Evaluate(foot.stepProgress) * stepHeight;
        }
        else
        {
            pos = foot.plantedPos;
        }

        foot.ikTarget.position = pos;
    }

    private Vector3 ProjectToGround(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * probeUp;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeUp * 3f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            // TODO: Use hit.normal for foot rotation alignment
            return hit.point + Vector3.up * footToMesh;
        }

        return new Vector3(pos.x, transform.position.y, pos.z);
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        if (_leftFoot == null || _rightFoot == null) return;

        const float radius = 0.05f;
        const float axisLength = 0.15f;

        void DrawTarget(Transform t, Color c)
        {
            if (t == null) return;

            Gizmos.color = c;
            Gizmos.DrawWireSphere(t.position, radius);
            Gizmos.DrawLine(t.position, t.position + t.forward * axisLength);
            Gizmos.DrawLine(t.position, t.position + t.right * (axisLength * 0.75f));
            Gizmos.DrawLine(t.position, t.position + t.up * (axisLength * 0.75f));
        }

        DrawTarget(_leftFoot.ikTarget, Color.red);
        DrawTarget(_rightFoot.ikTarget, Color.red);
        DrawTarget(_leftThigh.ikTarget, new Color(1f, 0.5f, 0f));
        DrawTarget(_rightThigh.ikTarget, new Color(1f, 0.5f, 0f));
        DrawTarget(_body.ikTarget, Color.magenta);
    }

    // - TODO:
    // - set joint constraints before updating rotation much more to see how things change
    // - add foot yaw rotation to match player forward
    // - add foot pitch rotation for step (toe flat -> down -> up -> flat)
    // -- add spherecast for ground normal to adjust foot pitch for targets (and slight roll if we want)
    // - add early exit to advance step if velocity has dropped or direction switched in the last bit of the step
    // - legs cross on strafe - this is gonna need a whole logic for leg crossing in general
}