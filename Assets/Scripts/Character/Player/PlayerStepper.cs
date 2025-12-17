using RootMotion.FinalIK;
using UnityEngine;

public class PlayerStepper : MonoBehaviour
{
    public enum FootSide { Left, Right }

    [Header("References")]
    [Tooltip("Player movement controller for reading input and velocity.")]
    [SerializeField] private PlayerMover playerMover;

    [Tooltip("Pelvis/hips transform used as the reference point for computing ideal foot placement.")]
    [SerializeField] private Transform pelvis;

    [Tooltip("FullBodyBipedIK solver whose foot effectors are driven by the generated stepper targets.")]
    [SerializeField] private FullBodyBipedIK fbbik;

    [Tooltip("Left foot bone/transform.")]
    [SerializeField] private Transform leftFoot;

    [Tooltip("Right foot bone/transform.")]
    [SerializeField] private Transform rightFoot;


    [Header("Step Placement")]
    [Tooltip("Half-width from pelvis centerline to each foot home position.")]
    [SerializeField] private float stanceWidth = 0.1f;

    [Tooltip("If foot is this far from its home (XZ), trigger a step.")]
    [SerializeField] private float stepTrigger = 0.05f;

    [Tooltip("Nominal stride length on flat ground.")]
    [SerializeField] private float stepDistance = 0.1f;

    [Tooltip("Time (seconds) for a foot to swing from its start to its target plant position.")]
    [SerializeField] private float stepDuration = 0.25f;

    [Tooltip("Minimum time between step starts.")]
    [SerializeField] private float minStepInterval = 0f;

    [Tooltip("Peak vertical lift added during the swing phase.")]
    [SerializeField] private float stepHeight = 0.8f;

    [Tooltip("Normalized 0->1->0 curve controlling swing lift over time.")]
    [SerializeField]
    private AnimationCurve stepHeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));


    [Header("Ground Sampling")]
    [SerializeField] private LayerMask groundMask;

    [Tooltip("Distance of foot bone to bottom of foot mesh.")]
    [SerializeField] private float feetYOffset = 0.05f;

    [Tooltip("How far above the probe origin to start the cast.")]
    [SerializeField] private float probeUp = 0.6f;

    [Tooltip("How far downward to search for ground.")]
    [SerializeField] private float probeDown = 1.6f;

    [Tooltip("Radius of the spherecast used for ground probing.")]
    [SerializeField] private float sphereRadius = 0.08f;

    private class Foot
    {
        public Transform target;
        public Vector3 plantedPos;
        public bool stepping;
        public float stepStartTime;
        public Vector3 stepFrom;
        public Vector3 stepTo;
    }

    private Foot _L = new Foot();
    private Foot _R = new Foot();
    private float _lastStepTime;
    private FootSide _nextFoot = FootSide.Left;



    void Awake()
    {
        if (playerMover is null) playerMover = GetComponent<PlayerMover>();
        if (!RequireRef.Check(playerMover, this, nameof(playerMover))) return;

        if (pelvis is null) pelvis = transform.Find("Armature/root/pelvis");
        if (!RequireRef.Check(pelvis, this, nameof(pelvis))) return;

        if (fbbik is null) fbbik = GetComponent<FullBodyBipedIK>();
        if (!RequireRef.Check(fbbik, this, nameof(fbbik))) return;

        if (leftFoot is null) leftFoot = pelvis.Find("thigh_l/calf_l/foot_l");
        if (!RequireRef.Check(leftFoot, this, nameof(leftFoot))) return;

        if (rightFoot is null) rightFoot = pelvis.Find("thigh_r/calf_r/foot_r");
        if (!RequireRef.Check(rightFoot, this, nameof(rightFoot))) return;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RequireRef.Warn(playerMover, this, nameof(playerMover));
        RequireRef.Warn(pelvis, this, nameof(pelvis));
        RequireRef.Warn(fbbik, this, nameof(fbbik));
        RequireRef.Warn(leftFoot, this, nameof(leftFoot));
        RequireRef.Warn(rightFoot, this, nameof(rightFoot));
    }
#endif

    void Reset()
    {
        playerMover = GetComponent<PlayerMover>();
        pelvis = transform.Find("Armature/root/pelvis");
        fbbik = GetComponent<FullBodyBipedIK>();
        if (pelvis != null)
        {
            leftFoot = pelvis.Find("thigh_l/calf_l/foot_l");
            rightFoot = pelvis.Find("thigh_r/calf_r/foot_r");
        }
    }

    void Start()
    {
        _L.target = new GameObject("LeftFootTarget").transform;
        _R.target = new GameObject("RightFootTarget").transform;

        _L.target.SetParent(null, true);
        _R.target.SetParent(null, true);

        _L.plantedPos = leftFoot.position;
        _R.plantedPos = rightFoot.position;

        _L.target.position = _L.plantedPos;
        _R.target.position = _R.plantedPos;

        fbbik.solver.leftFootEffector.target = _L.target;
        fbbik.solver.rightFootEffector.target = _R.target;

        fbbik.solver.leftFootEffector.positionWeight = 1f;
        fbbik.solver.rightFootEffector.positionWeight = 1f;

        // TEMP: Keep rotation off until stepping feels solid.
        fbbik.solver.leftFootEffector.rotationWeight = 0f;
        fbbik.solver.rightFootEffector.rotationWeight = 0f;

        _lastStepTime = -minStepInterval;
    }

    void Update()
    {
        UpdateFootTargets();
    }

    private void UpdateFootTargets()
    {
        float t = Time.time;

        Vector3 vel = playerMover is not null ? playerMover.Velocity : Vector3.zero;
        vel.y = 0f;
        float speed = vel.magnitude;

        // Normalize speed to 0-1 range based on expected movement speeds
        float desired = Mathf.Max(playerMover.DesiredMoveSpeed, 0.01f);
        float normalizedSpeed = Mathf.Clamp01(speed / desired);

        // Calculate direction
        Vector3 moveDir = speed > 0.0001f ? vel / speed : transform.forward;
        moveDir.y = 0f;
        moveDir.Normalize();

        Vector3 forward = moveDir;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // Scale forward placement by velocity
        float forwardLead = normalizedSpeed * speed * stepDuration * 0.5f;

        // Calculate home positions
        Vector3 leftHome = pelvis.position + right * -stanceWidth + forward * forwardLead;
        Vector3 rightHome = pelvis.position + right * stanceWidth + forward * forwardLead;

        // Update any feet currently swinging
        if (_L.stepping) UpdateSwing(_L, t);
        if (_R.stepping) UpdateSwing(_R, t);

        // Planted feet stay put
        if (!_L.stepping) _L.target.position = _L.plantedPos;
        if (!_R.stepping) _R.target.position = _R.plantedPos;

        // Don't start new steps if one is still swinging
        if (_L.stepping || _R.stepping) return;
        if (t - _lastStepTime < minStepInterval) return;

        // Check distance from home positions
        float leftErr = PlanarDist(_L.plantedPos, leftHome);
        float rightErr = PlanarDist(_R.plantedPos, rightHome);

        // Scale trigger threshold slightly with speed (tighter tolerance when standing)
        float currentTrigger = Mathf.Lerp(stepTrigger * 2f, stepTrigger, normalizedSpeed);

        bool leftWants = leftErr > currentTrigger;
        bool rightWants = rightErr > currentTrigger;

        if (!leftWants && !rightWants) return;

        // Choose which foot to step
        FootSide chosen;
        if (normalizedSpeed > 0.1f)
        {
            // Walking: prefer alternation
            chosen = ChooseFoot(leftWants, rightWants, leftErr, rightErr);
        }
        else
        {
            // Near-stationary: just pick worst foot
            chosen = (leftErr >= rightErr) ? FootSide.Left : FootSide.Right;
        }

        // Step to home position + scaled forward step
        float forwardStep = normalizedSpeed * stepDistance;

        if (chosen == FootSide.Left)
        {
            Vector3 targetPos = leftHome + forward * forwardStep;
            StartSwing(_L, targetPos, t);
        }
        else
        {
            Vector3 targetPos = rightHome + forward * forwardStep;
            StartSwing(_R, targetPos, t);
        }

        _nextFoot = (chosen == FootSide.Left) ? FootSide.Right : FootSide.Left;
        _lastStepTime = t;
    }

    private FootSide ChooseFoot(bool leftWants, bool rightWants, float leftErr, float rightErr)
    {
        if (leftWants && !rightWants) return FootSide.Left;
        if (rightWants && !leftWants) return FootSide.Right;

        // Both want. Prefer alternating unless one is significantly worse.
        if (_nextFoot == FootSide.Left && leftErr >= rightErr * 0.85f) return FootSide.Left;
        if (_nextFoot == FootSide.Right && rightErr >= leftErr * 0.85f) return FootSide.Right;

        return (leftErr >= rightErr) ? FootSide.Left : FootSide.Right;
    }

    private void StartSwing(Foot foot, Vector3 homePos, float now)
    {
        foot.stepping = true;
        foot.stepStartTime = now;
        foot.stepFrom = foot.target.position;

        // Project target position to ground
        Vector3 grounded = ProjectToGround(homePos);
        foot.stepTo = grounded;
    }

    private void UpdateSwing(Foot foot, float now)
    {
        float u = Mathf.Clamp01((now - foot.stepStartTime) / stepDuration);

        // Smooth interpolation using cosine ease
        float smoothU = (1f - Mathf.Cos(u * Mathf.PI)) * 0.5f;
        Vector3 pos = Vector3.Lerp(foot.stepFrom, foot.stepTo, smoothU);

        // Add vertical lift during swing
        float lift = stepHeightCurve.Evaluate(u) * stepHeight;
        pos.y += lift;

        foot.target.position = pos;

        // Complete the step when time is up
        if (u >= 1f)
        {
            foot.stepping = false;
            foot.plantedPos = foot.stepTo;
            foot.target.position = foot.plantedPos;
        }
    }

    private Vector3 ProjectToGround(Vector3 worldPos)
    {
        Vector3 start = worldPos + Vector3.up * probeUp;
        float dist = probeUp + probeDown;

        if (Physics.SphereCast(start, sphereRadius, Vector3.down, out RaycastHit hit, dist, groundMask))
        {
            return hit.point + Vector3.up * feetYOffset;
        }

        // Fallback if no ground found
        return new Vector3(worldPos.x, pelvis.position.y, worldPos.z);
    }

    private static float PlanarDist(Vector3 a, Vector3 b)
    {
        Vector2 A = new Vector2(a.x, a.z);
        Vector2 B = new Vector2(b.x, b.z);
        return Vector2.Distance(A, B);
    }
}