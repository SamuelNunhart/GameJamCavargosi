using UnityEngine;

/// <summary>Owns cursor, settings locks and camera selection for an opted-in story scene.</summary>
[DefaultExecutionOrder(-100)]
public sealed class StoryPlayerRig : MonoBehaviour
{
    [SerializeField] private StoryId story = StoryId.Father;
    public StoryId Story => story;

    public static StoryPlayerRig Instance { get; private set; }
    [SerializeField] private PlayerControlLocker controlLocker;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Camera[] managedCameras;
    [Tooltip("Character mesh renderers hidden while this rig is playing. Keep props out of this list.")]
    [SerializeField] private Renderer[] firstPersonHiddenRenderers;
    private bool[] previousRenderingStates;
    private bool settingsLock;
    private bool cutsceneLock;
    private Pose startingPose;
    public Camera ActiveCamera { get; private set; }
    public Camera GameplayCamera => gameplayCamera;
    public bool CanInteract => !controlLocker.IsLocked && !AudioSettingsOverlay.IsOpen;

    private void Awake()
    {
        Instance = this;
        if (firstPersonHiddenRenderers != null)
        {
            previousRenderingStates = new bool[firstPersonHiddenRenderers.Length];
            for (int i = 0; i < firstPersonHiddenRenderers.Length; i++)
            {
                Renderer characterRenderer = firstPersonHiddenRenderers[i];
                if (characterRenderer == null) continue;
                previousRenderingStates[i] = characterRenderer.forceRenderingOff;
                characterRenderer.forceRenderingOff = true;
            }
        }
        if (spawnPoint != null) PlacePlayer(new Pose(spawnPoint.position, spawnPoint.rotation));
        startingPose = new Pose(transform.position, transform.rotation);
        SelectCamera(gameplayCamera);
        SetSettingsOpen(AudioSettingsOverlay.IsOpen);
        RefreshCursor();
    }

    private void LateUpdate() { RefreshCursor(); }

    /// <summary>Returns the player to the pose used when this scene started.</summary>
    public void ReturnToStart()
    {
        if (TryGetComponent(out Rigidbody body) && !body.isKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        PlacePlayer(startingPose);
        if (gameplayCamera.TryGetComponent(out PlayerLook look)) look.ResetPitch();
    }

    private void PlacePlayer(Pose pose)
    {
        CharacterController controller = GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(pose.position, pose.rotation);
        if (controller != null) controller.enabled = wasEnabled;
    }

    private void OnDestroy()
    {
        if (previousRenderingStates != null)
        {
            for (int i = 0; i < previousRenderingStates.Length; i++)
            {
                Renderer characterRenderer = firstPersonHiddenRenderers[i];
                if (characterRenderer != null)
                    characterRenderer.forceRenderingOff = previousRenderingStates[i];
            }
        }
        if (Instance == this) Instance = null;
    }

    /// <summary>Owns exactly one settings lock; dialogue and cutscene locks remain independent.</summary>
    public void SetSettingsOpen(bool open)
    {
        if (settingsLock != open)
        {
            settingsLock = open;
            if (open) controlLocker.AcquireLock();
            else controlLocker.ReleaseLock();
        }
        RefreshCursor();
    }

    /// <summary>Starts a view using a camera explicitly assigned to this rig.</summary>
    public void BeginCutscene(Camera camera)
    {
        if (camera == null || !camera.gameObject.activeInHierarchy ||
            System.Array.IndexOf(managedCameras, camera) < 0) return;
        if (!cutsceneLock) { cutsceneLock = true; controlLocker.AcquireLock(); }
        SelectCamera(camera);
    }

    /// <summary>Returns the camera without releasing locks owned by settings or dialogue.</summary>
    public void EndCutscene()
    {
        SelectCamera(gameplayCamera);
        if (cutsceneLock) { cutsceneLock = false; controlLocker.ReleaseLock(); }
    }

    private void SelectCamera(Camera camera)
    {
        foreach (Camera candidate in managedCameras)
        {
            if (candidate == null) continue;
            bool active = candidate == camera;
            candidate.enabled = active;
            AudioListener listener = candidate.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = active;
        }
        ActiveCamera = camera;
    }

    /// <summary>This rig is the sole cursor owner; dialogue keeps the view locked but cannot move it.</summary>
    public void RefreshCursor()
    {
        bool unlocked = settingsLock || AudioSettingsOverlay.IsOpen;
        Cursor.lockState = unlocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = unlocked;
    }
}
