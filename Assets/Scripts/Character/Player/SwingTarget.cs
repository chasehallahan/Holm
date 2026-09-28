using UnityEngine;

public class SwingTarget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private Transform aimPivot; // CameraRig/CameraPivot

    [Header("Unarmed data")]
    [SerializeField] private float guardDistance = 0.35f; // rest distance, stands in for the guard sphere
    [SerializeField] private float reach = 0.8f;

    [Header("Jab scrub")]
    [Tooltip("Mouse Y pixels to scrub from guard to full reach.")]
    [SerializeField] private float pixelsToFullReach = 400f;
    [Tooltip("Mouse X -> lateral steer of the jab line.")]
    [SerializeField] private float lateralSteer = 0.002f;

    private float _extend01; // 0 = guard, 1 = full reach — THE scrub position
    private float _lateral;  // accumulated sideways steer while jabbing

    public bool Jabbing { get; private set; }

    void Awake()
    {
        if (input == null) input = GetComponent<PlayerInputReader>();
        if (aimPivot == null) aimPivot = transform.Find("CameraRig/CameraPivot");
    }

    void Update()
    {
        Jabbing = input.BlockHeld; // RMB = Block action = jab mode, for now

        if (Jabbing)
        {
            _extend01 = Mathf.Clamp01(_extend01 + input.Look.y / pixelsToFullReach);
            _lateral += input.Look.x * lateralSteer;
        }
        else
        {
            _extend01 = Mathf.MoveTowards(_extend01, 0f, 4f * Time.deltaTime); // ease home
            _lateral = Mathf.MoveTowards(_lateral, 0f, 4f * Time.deltaTime);
        }
    }

    public Vector3 TargetPoint()
    {
        Vector3 dir = (aimPivot.forward + aimPivot.right * _lateral).normalized;
        return aimPivot.position + dir * Mathf.Lerp(guardDistance, reach, _extend01);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Jabbing ? Color.red : Color.cyan;
        Gizmos.DrawSphere(TargetPoint(), 0.05f);
    }
}