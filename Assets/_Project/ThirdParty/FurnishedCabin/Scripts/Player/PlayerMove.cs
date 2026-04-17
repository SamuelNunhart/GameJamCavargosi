using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement Input")]
    [SerializeField] private string horizontalInputName = "Horizontal";
    [SerializeField] private string verticalInputName = "Vertical";

    [Header("Movement Speeds")]
    [SerializeField] private float movementSpeed = 2f;
    [SerializeField] private float crouchMovementSpeed = 1f;

    [Header("Crouch")]
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private float crouchHeight = 1.1f;
    [SerializeField] private float crouchTransitionSpeed = 8f;

    [Header("Camera")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float cameraCrouchOffset = 0.45f;

    private CharacterController charController;

    private float standingHeight;
    private Vector3 standingCenter;
    private float controllerBottom;
    private float standingCameraY;

    private void Awake()
    {
        charController = GetComponent<CharacterController>();

        standingHeight = charController.height;
        standingCenter = charController.center;
        controllerBottom = standingCenter.y - (standingHeight * 0.5f);

        if (cameraHolder != null)
        {
            standingCameraY = cameraHolder.localPosition.y;
        }
    }

    private void Update()
    {
        HandleCrouch();
        PlayerMovement();
    }

    private void PlayerMovement()
    {
        bool isCrouching = Input.GetKey(crouchKey);
        float currentSpeed = isCrouching ? crouchMovementSpeed : movementSpeed;

        float vertInput = Input.GetAxis(verticalInputName) * currentSpeed;
        float horizInput = Input.GetAxis(horizontalInputName) * currentSpeed;

        Vector3 forwardMovement = transform.forward * vertInput;
        Vector3 rightMovement = transform.right * horizInput;

        charController.SimpleMove(forwardMovement + rightMovement);
    }

    private void HandleCrouch()
    {
        bool isCrouching = Input.GetKey(crouchKey);

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        charController.height = Mathf.Lerp(
            charController.height,
            targetHeight,
            crouchTransitionSpeed * Time.deltaTime
        );

        Vector3 targetCenter = standingCenter;
        targetCenter.y = controllerBottom + (charController.height * 0.5f);

        charController.center = Vector3.Lerp(
            charController.center,
            targetCenter,
            crouchTransitionSpeed * Time.deltaTime
        );

        if (cameraHolder != null)
        {
            Vector3 localPos = cameraHolder.localPosition;
            float targetCameraY = standingCameraY - (isCrouching ? cameraCrouchOffset : 0f);

            localPos.y = Mathf.Lerp(
                localPos.y,
                targetCameraY,
                crouchTransitionSpeed * Time.deltaTime
            );

            cameraHolder.localPosition = localPos;
        }
    }
}