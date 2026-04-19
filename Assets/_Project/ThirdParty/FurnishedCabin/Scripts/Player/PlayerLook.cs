using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    private const string MouseSensitivityKey = "InputSettings.MouseSensitivity";
    private const float DefaultMouseSensitivity = 425f;
    private const float MinMouseSensitivity = 100f;
    private const float MaxMouseSensitivity = 800f;

    private static readonly System.Collections.Generic.HashSet<PlayerLook> ActiveInstances = new System.Collections.Generic.HashSet<PlayerLook>();
    private static float savedMouseSensitivity = DefaultMouseSensitivity;
    private static bool sensitivityLoaded;

    [Header("Input")]
    [SerializeField] private string mouseXInputName = "Mouse X";
    [SerializeField] private string mouseYInputName = "Mouse Y";

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = DefaultMouseSensitivity;
    [SerializeField] private Transform playerBody;
    [SerializeField] private bool invertY = false;

    private float xRotation = 0f;
    private bool cursorIsLocked = true;

    public static float CurrentSensitivity
    {
        get
        {
            LoadSensitivityIfNeeded();
            return savedMouseSensitivity;
        }
    }

    public static float MinSensitivity => MinMouseSensitivity;
    public static float MaxSensitivity => MaxMouseSensitivity;

    private void Awake()
    {
        LoadSensitivityIfNeeded();
        mouseSensitivity = savedMouseSensitivity;
        ActiveInstances.Add(this);
    }

    private void Start()
    {
        SetCursorState(true);
    }

    private void OnDestroy()
    {
        ActiveInstances.Remove(this);
    }

    private void Update()
    {
        if (AudioSettingsOverlay.IsOpen)
        {
            if (cursorIsLocked)
            {
                SetCursorState(false);
            }

            return;
        }

        HandleCursorLock();
        SyncCursorState();

        if (cursorIsLocked)
        {
            CameraRotation();
        }
    }

    private void HandleCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorState(false);
        }

        if (Input.GetMouseButtonDown(0))
        {
            SetCursorState(true);
        }
    }

    private void SyncCursorState()
    {
        bool shouldBeLocked = Cursor.lockState == CursorLockMode.Locked && !Cursor.visible;

        if (cursorIsLocked == shouldBeLocked)
            return;

        cursorIsLocked = shouldBeLocked;
    }

    private void SetCursorState(bool locked)
    {
        cursorIsLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void CameraRotation()
    {
        if (playerBody == null)
            return;

        float mouseX = Input.GetAxisRaw(mouseXInputName) * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxisRaw(mouseYInputName) * mouseSensitivity * Time.deltaTime;

        xRotation += invertY ? mouseY : -mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }

    public static void SetSensitivity(float value)
    {
        LoadSensitivityIfNeeded();

        savedMouseSensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
        PlayerPrefs.SetFloat(MouseSensitivityKey, savedMouseSensitivity);
        PlayerPrefs.Save();

        foreach (PlayerLook playerLook in ActiveInstances)
        {
            if (playerLook != null)
                playerLook.mouseSensitivity = savedMouseSensitivity;
        }
    }

    private static void LoadSensitivityIfNeeded()
    {
        if (sensitivityLoaded)
            return;

        savedMouseSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity),
            MinMouseSensitivity,
            MaxMouseSensitivity);

        sensitivityLoaded = true;
    }
}
