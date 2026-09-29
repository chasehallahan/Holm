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

    private float _extend01; // 0 = guard, 1 = full reach � THE scrub position

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
        }
        else
        {
            _extend01 = Mathf.MoveTowards(_extend01, 0f, 4f * Time.deltaTime); // ease home
        }
    }

    public Vector3 TargetPoint()
    {
        // Straight down the crosshair; left/right steering comes from the damped camera yaw.
        return aimPivot.position + aimPivot.forward * Mathf.Lerp(guardDistance, reach, _extend01);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Jabbing ? Color.red : Color.cyan;
        Gizmos.DrawSphere(TargetPoint(), 0.05f);
    }
}