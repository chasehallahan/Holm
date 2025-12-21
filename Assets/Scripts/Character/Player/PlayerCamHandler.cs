using UnityEngine;

// This neeeds to be executed post IK to pull correct eye position
public class PlayerCamHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform that CameraPivot is parented under. We move this rig (not the Camera or CameraPivot directly).")]
    [SerializeField] private Transform cameraRig;

    [Tooltip("Preferred eye/eyes anchor transform. CameraPivot follows this position.")]
    [SerializeField] private Transform eyes;

    [Header("Tuning")]
    [Tooltip("Smoothing speed for CameraRig position following the Eyes transform. Higher = tighter, lower = floatier.")]
    [Range(0f, 500f)][SerializeField] private float cameraRigLerp = 120f;


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

    private void Start()
    {
        cameraRig.position = eyes.position;
    }

    void LateUpdate()
    {

        float t = 1f - Mathf.Exp(-cameraRigLerp * Time.deltaTime);
        cameraRig.position = Vector3.Lerp(cameraRig.position, eyes.position, t);
    }
}