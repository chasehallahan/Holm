using UnityEngine;

public class SwingTarget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputReader input;
    [SerializeField] private Transform aimPivot; // CameraRig/CameraPivot

    [Header("Unarmed data")]
    [Tooltip("Full jab reach from the camera, meters.")]
    [SerializeField] private float reach = 0.8f;

    [Header("Jab scrub")]
    [Tooltip("Mouse Y pixels to scrub from guard to full reach.")]
    [SerializeField] private float pixelsToFullReach = 400f;

    [Tooltip("Extension lost per second when the mouse goes still (spring back to guard).")]
    [SerializeField] private float idleDecay = 1.5f;

    [Tooltip("How much faster pulling the mouse back retracts vs pushing extends.")]
    [SerializeField] private float retractBoost = 2.5f;

    private float _extend01; // 0 = guard, 1 = full reach - THE scrub position
    public float Extend01 => _extend01; // 0 = guard, 1 = far point

    public bool Jabbing { get; private set; }

    void Awake()
    {
        if (input == null) input = GetComponent<PlayerInputReader>();
        if (aimPivot == null) aimPivot = transform.Find("CameraRig/CameraPivot");
    }

    void OnDisable()
    {
        Jabbing = false;
        _extend01 = 0f;
    }

    void Update()
    {
        Jabbing = input.BlockHeld;

        if (Jabbing)
        {
            float push = input.Look.y / pixelsToFullReach;
            _extend01 += push >= 0f ? push : push * retractBoost; // yank back = fast retract
            if (push <= 0f)
                _extend01 -= idleDecay * Time.deltaTime;          // spring home only when not pushing
            _extend01 = Mathf.Clamp01(_extend01);
        }
        else
        {
            _extend01 = Mathf.MoveTowards(_extend01, 0f, 4f * Time.deltaTime);
        }
    }

    public Vector3 TargetPoint() // full-reach point on the crosshair ray
    {
        return aimPivot.position + aimPivot.forward * reach;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Jabbing ? Color.red : Color.cyan;
        Gizmos.DrawSphere(TargetPoint(), 0.05f);
    }
}
