using UnityEngine;

public class LocalPlayerSetup : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [Tooltip("Local-only cameras (CameraPivot, Camera, Cinemachine); disabled for remote players.")]
    [SerializeField] private GameObject[] playerCameras;

    [Tooltip("Full body mesh shown to other players; hidden for the local player.")]
    [SerializeField] private SkinnedMeshRenderer worldMesh;

    [Tooltip("Headless/local body mesh shown only to the local player.")]
    [SerializeField] private SkinnedMeshRenderer localMesh;

    [Tooltip("Scripts that should run only on the local player (camera, input, etc).")]
    [SerializeField] private MonoBehaviour[] localOnlyScripts;

    [Header("Ownership (Temporary)")]
    [Tooltip("TEMP local ownership flag; will replace with networking IsOwner/isLocalPlayer later.")]
    [SerializeField] private bool isLocalPlayer = true;

    void Awake()
    {
        if (!RequireRef.Check(worldMesh, this, nameof(worldMesh))) return;
        if (!RequireRef.Check(localMesh, this, nameof(localMesh))) return;
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
        // Visuals
        if (worldMesh) worldMesh.enabled = !local;
        if (localMesh) localMesh.enabled = local;

        // Cameras
        if (playerCameras is not null)
        {
            foreach (var c in playerCameras)
            {
                if (c) c.SetActive(local);
            }
        }

        // Scripts that should run only for the owning client
        if (localOnlyScripts is not null)
        {
            foreach (var s in localOnlyScripts)
                if (s) s.enabled = local;
        }

        // Cursor (local only)
        if (local)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
