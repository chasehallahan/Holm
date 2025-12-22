using UnityEngine;

[DefaultExecutionOrder(10095)] // Execute after IK solvers
public class PlayerCamHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root transform that follows the eye position. Parent of CameraPivot.")]
    [SerializeField] private Transform cameraRig;

    [Tooltip("Eye anchor transform. CameraRig position tracks this.")]
    [SerializeField] private Transform eyes;

    [Header("Smoothing")]
    [Tooltip("Position smoothing speed. Higher = tighter tracking, lower = floatier.")]
    [Range(0f, 500f)]
    [SerializeField] private float positionSmoothSpeed = 120f;


    void Awake()
    {
        RequireRef.Check(cameraRig, this, nameof(cameraRig));
        RequireRef.Check(eyes, this, nameof(eyes));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RequireRef.Warn(cameraRig, this, nameof(cameraRig));
        RequireRef.Warn(eyes, this, nameof(eyes));
    }
#endif

    void Start()
    {
        cameraRig.position = eyes.position;
    }

    void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime);
        cameraRig.position = Vector3.Lerp(cameraRig.position, eyes.position, t);
    }
}