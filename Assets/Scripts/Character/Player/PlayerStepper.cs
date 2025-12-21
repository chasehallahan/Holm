using RootMotion.FinalIK;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(FullBodyBipedIK), typeof(PlayerMover))]
public class PlayerStepper : MonoBehaviour
{

    [Header("Stance Settings")]
    [Tooltip("How wide apart the feet are while player stands (foot position to player center).")]
    [SerializeField] private float stanceWidth = 0.1f;
    [Tooltip("Negative y applied to body effector while standing to simulate natural standing.")]
    [SerializeField] private float stanceSlouch = 0.08f;

    [Header("Stride Settings")]
    [Tooltip("How wide apart the feet are while player walks (foot position to player center).")]
    [SerializeField] private float strideWidth = 0.08f;
    [Tooltip("How far forward the player steps at base walk speed.")]
    [SerializeField] private float strideLength = 0.3f;
    [Tooltip("Minimum foot distance from ideal to trigger a step.")]
    [SerializeField] private float stepTrigger = 0.08f;
    [Tooltip("How long each step takes (seconds) at base walk speed")]
    [SerializeField] private float strideDuration = 0.2f;
    [Tooltip("Minimum step duration clamp.")]
    [SerializeField] private float minStrideDuration = 0.1f;
    [Tooltip("Maximum step duration clamp.")]
    [SerializeField] private float maxStrideDuration = 0.4f;
    [Tooltip("How high feet lift during steps")]
    [SerializeField] private float stepHeight = 0.24f;
    [Tooltip("Shape of the step arc (X = step progress 0-1, Y = height 0-1)")]
    [SerializeField] private AnimationCurve stepHeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -3f, 0f)
    );

    [Tooltip("How far ahead to predict movement for step placement (seconds)")]
    [SerializeField] private float lookAheadTime = 0.05f;

    [Header("Ground Detection")]
    [Tooltip("Distance from foot bone (ankle) position to bottom of foot mesh.")]
    [SerializeField] private float footToMesh = 0.08f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float probeUp = 1.5f;

    [Header("Plant Correction")]
    [Tooltip("Deadzone (meters). If the solved foot bone drifts farther than this from plantedPos, we slide plantedPos to reduce stretch/pop.")]
    [SerializeField] private float maxPlantDivergence = 0.03f;

    [Tooltip("Max slide speed for plant correction (m/s). Prevents visible snapping.")]
    [SerializeField] private float plantSlideSpeed = 0.6f;


    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // References
    private CharacterController _controller;
    private FullBodyBipedIK _fbbik;
    private IKSolverFullBodyBiped _ikSolver;

    // Runtime state
    private Foot _leftFoot;
    private Foot _rightFoot;
    private Thigh _leftThigh;
    private Thigh _rightThigh;
    private Body _body;
    private Vector3 _velocity;
    private bool _leftFootTurn = true;


    private class Foot
    {
        // runtime dynamic
        public Vector3 plantedPos;
        public Vector3 stepFromPos;
        public Vector3 stepToPos;
        public float stepDuration;
        public float stepProgress = 1f;
        public bool isStepping = false;
        public Vector3 lastSolvedBonePos;

        //read-only
        public readonly bool isLeft;
        public readonly Transform bone;
        public readonly Transform ikTarget;

        public Foot(bool left, IKEffector effector)
        {
            isLeft = left;
            bone = effector.bone;

            // create and set ik target game object transform at bone position
            ikTarget = new GameObject(isLeft ? "LeftFootTarget" : "RightFootTarget").transform;
            ikTarget.SetParent(null, true);
            ikTarget.SetPositionAndRotation(bone.position, bone.rotation);

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

        if (_fbbik is null) _fbbik = GetComponent<FullBodyBipedIK>();
        RequireRef.Check(_fbbik, this, nameof(_fbbik));
        _ikSolver = _fbbik.solver;
    }

    void Start()
    {
        _leftFoot = new Foot(true, _ikSolver.leftFootEffector);
        _rightFoot = new Foot(false, _ikSolver.rightFootEffector);

        // ------ FEET ------
        // set inital feet plant positions
        _leftFoot.plantedPos = ProjectToGround(_leftFoot.bone.position);
        _rightFoot.plantedPos = ProjectToGround(_rightFoot.bone.position);

        _leftFoot.ikTarget.position = _leftFoot.plantedPos;
        _rightFoot.ikTarget.position = _rightFoot.plantedPos;

        // Initialize cache (prevents first-frame weirdness)
        _leftFoot.lastSolvedBonePos = _leftFoot.bone.position;
        _rightFoot.lastSolvedBonePos = _rightFoot.bone.position;

        // ------ THIGHS ------
        _leftThigh = new Thigh(true, _ikSolver.leftThighEffector, transform);
        _rightThigh = new Thigh(false, _ikSolver.rightThighEffector, transform);

        // ------ BODY ------
        _body = new Body(_ikSolver.bodyEffector, transform);
        
        // drop body effector for a little knee bend while standing
        _body.ikTarget.localPosition -= Vector3.up * stanceSlouch;

        // FBBIK settings
        _ikSolver.leftFootEffector.positionWeight = 0.5f;
        _ikSolver.rightFootEffector.positionWeight = 0.5f;
        _ikSolver.leftFootEffector.rotationWeight = 0.5f;
        _ikSolver.rightFootEffector.rotationWeight = 0.5f;

        _ikSolver.leftThighEffector.positionWeight = 1f;
        _ikSolver.rightThighEffector.positionWeight = 1f;
        _ikSolver.leftThighEffector.rotationWeight = 1f;
        _ikSolver.rightThighEffector.rotationWeight = 1f;

        _ikSolver.bodyEffector.positionWeight = 1f;
        _ikSolver.bodyEffector.rotationWeight = 1f;

        // Drive targets right before the solver runs (avoids script execution order issues)
        _ikSolver.OnPreUpdate += UpdateIKTargets;

        // Cache solved bone positions right after the solver runs
        _ikSolver.OnPostUpdate += CacheSolvedPositions;
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
        UpdateVelocity();
        AdvanceStep(_leftFoot);
        AdvanceStep(_rightFoot);

        // accounts for ik target drift from bone location due to ik leg constraints (foot skate)
        CorrectPlantedFoot(_leftFoot);
        CorrectPlantedFoot(_rightFoot);

        CheckForSteps();
    }

    private void CorrectPlantedFoot(Foot foot)
    {
        if (foot == null) return;
        if (foot.isStepping) return;

        // Planar divergence only
        Vector3 planted = foot.plantedPos; planted.y = 0f;
        Vector3 solved = foot.lastSolvedBonePos; solved.y = 0f;

        Vector3 delta = solved - planted;
        float dist = delta.magnitude;

        if (dist <= maxPlantDivergence) return;

        // How much we want to close the gap (beyond deadzone)
        float desiredMove = dist - maxPlantDivergence;

        // Limit slide per frame to avoid snapping
        float maxMoveThisFrame = plantSlideSpeed * Time.deltaTime;
        float move = Mathf.Min(desiredMove, maxMoveThisFrame);

        // Apply correction and keep it on ground
        foot.plantedPos += (delta / dist) * move;
        foot.plantedPos = ProjectToGround(foot.plantedPos);
    }

    private void UpdateVelocity()
    {
        _velocity = _controller.velocity;
        _velocity.y = 0;
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
        if (_leftFoot.isStepping || _rightFoot.isStepping) return;  // TODO: add overlap for running/sprinting

        Vector3 leftHome = GetHomePosition(_leftFoot);
        Vector3 rightHome = GetHomePosition(_rightFoot);

        float leftDrift = HorizontalDistance(_leftFoot.plantedPos, leftHome);
        float rightDrift = HorizontalDistance(_rightFoot.plantedPos, rightHome);

        bool leftNeeds = leftDrift > stepTrigger;
        bool rightNeeds = rightDrift > stepTrigger;

        // Nobody needs to step
        if (!leftNeeds && !rightNeeds) return;

        // Only one foot needs it - easy choice
        if (leftNeeds && !rightNeeds)
        {
            StartStep(_leftFoot);
            _leftFootTurn = false;
            return;
        }
        if (rightNeeds && !leftNeeds)
        {
            StartStep(_rightFoot);
            _leftFootTurn = true;
            return;
        }

        // Both need to step - alternate, but override if one is much worse
        bool leftUrgent = leftDrift > rightDrift * 1.5f;
        bool rightUrgent = rightDrift > leftDrift * 1.5f;

        if (leftUrgent)
        {
            StartStep(_leftFoot);
            _leftFootTurn = false;
        }
        else if (rightUrgent)
        {
            StartStep(_rightFoot);
            _leftFootTurn = true;
        }
        else if (_leftFootTurn)
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
        Vector3 home = GetHomePosition(foot);

        // When moving, add velocity prediction to step target
        if (_velocity.sqrMagnitude > 0.0001f)
        {
            home += _velocity * lookAheadTime;
        }

        return ProjectToGround(home);
    }

    private float GetStepDuration(Foot foot)
    {
        // TODO: me thinks this also needs to be scaled to velocity, not just distance
        float distance = Vector3.Distance(foot.stepFromPos, foot.stepToPos);
        float baseStepSpeed = strideLength / strideDuration;
        float duration = distance / baseStepSpeed;
        return Mathf.Clamp(duration, minStrideDuration, maxStrideDuration);
    }

    private void UpdateIKTargets()
    {
        UpdateBodyIK(_body);
        UpdateFootIK(_leftFoot);
        UpdateFootIK(_rightFoot);
    }

    private void UpdateBodyIK(Body body)
    {
    
    }

    private void UpdateFootIK(Foot foot)
    {
        Vector3 pos;
        if (foot.isStepping)
        {
            float t = Mathf.SmoothStep(0f, 1f, foot.stepProgress);
            pos = Vector3.Lerp(foot.stepFromPos, foot.stepToPos, t);

            float heightScale = stepHeightCurve.Evaluate(foot.stepProgress);
            pos.y += heightScale * stepHeight;
        }
        else
        {
            pos = foot.plantedPos;
        }

        foot.ikTarget.position = pos;
    }

    private Vector3 GetHomePosition(Foot foot)
    {
        Vector3 fwdDir;
        Vector3 rightDir;
        float stride;
        float width; 

        if (_velocity.sqrMagnitude > 0.001f)
        {
            
            fwdDir = _velocity.normalized;    
            stride = strideLength;
            rightDir = Vector3.Cross(Vector3.up, fwdDir).normalized;
            width = strideWidth;
        }
        else
        {
            fwdDir = transform.forward;
            stride = 0f;
            rightDir = transform.right;
            width = stanceWidth;

        }

        float side = foot.isLeft ? -width : width;
        return transform.position + fwdDir * stride + rightDir * side;
    }

    private Vector3 ProjectToGround(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * probeUp;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeUp * 3f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * footToMesh;  // TODO: add normal detection and foot rotation
        }
        return new Vector3(pos.x, transform.position.y, pos.z);
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0;
        b.y = 0;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        if (_leftFoot == null || _rightFoot == null) return;

        Vector3 leftHome = GetHomePosition(_leftFoot);
        Vector3 rightHome = GetHomePosition(_rightFoot);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(leftHome, 0.05f);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(rightHome, 0.05f);


        // Planted feet
        DrawFootGizmo(_leftFoot, Color.red);
        DrawFootGizmo(_rightFoot, Color.blue);

        Gizmos.color = Color.orange;
        Gizmos.DrawWireSphere(_leftThigh.ikTarget.position, 0.05f);
        Gizmos.DrawWireSphere(_rightThigh.ikTarget.position, 0.05f);
    }

    private void DrawFootGizmo(Foot foot, Color color)
    {
        if (foot.ikTarget == null) return;

        Gizmos.color = color;
        Gizmos.DrawSphere(foot.plantedPos, 0.04f);

        if (foot.isStepping)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(foot.stepToPos, 0.05f);

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(foot.ikTarget.position, 0.03f);

            Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
            Gizmos.DrawLine(foot.stepFromPos, foot.ikTarget.position);
            Gizmos.DrawLine(foot.ikTarget.position, foot.stepToPos);
        }
    }

    // - GetHomePosition shouldnt exist im running it twice for every step when i could just pass it
    // - hips arent aligning with body allowing full upper body 360 twist
    // - legs cross on strafe
}