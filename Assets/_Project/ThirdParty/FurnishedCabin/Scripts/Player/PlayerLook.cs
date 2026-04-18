using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private string mouseXInputName = "Mouse X";
    [SerializeField] private string mouseYInputName = "Mouse Y";

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 500f;
    [SerializeField] private Transform playerBody;
    [SerializeField] private bool invertY = false;

    private float xRotation = 0f;
    private bool cursorIsLocked = true;

    private void Start()
    {
        SetCursorState(true);
    }

    private void Update()
    {
        HandleCursorLock();

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
}