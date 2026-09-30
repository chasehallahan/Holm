using UnityEngine;


public class LocalPlayerSetup : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera objects to enable for local player only (CameraRig, Cinemachine, etc).")]
    [SerializeField] private GameObject[] playerCameras;

    [Tooltip("Full body mesh visible to remote players, hidden for local player.")]
    [SerializeField] private SkinnedMeshRenderer worldMesh;

    [Tooltip("First-person body mesh visible to local player only.")]
    [SerializeField] private SkinnedMeshRenderer localMesh;

    [Tooltip("Scripts that should only run on the local player (input, camera control, etc).")]
    [SerializeField] private MonoBehaviour[] localOnlyScripts;

    [Header("Ownership")]
    [Tooltip("Temporary ownership flag. Replace with networking IsOwner/IsLocalPlayer.")]
    [SerializeField] private bool isLocalPlayer = true;

    void Awake()
    {
        RequireRef.Check(worldMesh, this, nameof(worldMesh));
        RequireRef.Check(localMesh, this, nameof(localMesh));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RequireRef.Warn(worldMesh, this, nameof(worldMesh));
        RequireRef.Warn(localMesh, this, nameof(localMesh));
    }
#endif

    void Start()
    {
        ApplyLocalState(isLocalPlayer);
    }

    public void ApplyLocalState(bool local)
    {
        // Single-player dev: world mesh stays visible so the debug third-person cam shows the
        // body; localMesh waits for real first-person arms. Revisit with networking ownership.
        if (worldMesh) worldMesh.enabled = true;
        if (localMesh) localMesh.enabled = false;

        // Cameras: only active for local player
        if (playerCameras is not null)
        {
            foreach (var cam in playerCameras)
                if (cam) cam.SetActive(local);
        }

        // Local-only scripts
        if (localOnlyScripts is not null)
        {
            foreach (var script in localOnlyScripts)
                if (script) script.enabled = local;
        }

        // Cursor lock for local player
        if (local)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}