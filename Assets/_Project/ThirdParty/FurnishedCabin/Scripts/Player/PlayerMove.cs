using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement Input")]
    [SerializeField] private string horizontalInputName = "Horizontal";
    [SerializeField] private string verticalInputName = "Vertical";

    [Header("Movement Speeds")]
    [SerializeField] private float movementSpeed = 4f;
    [SerializeField] private float crouchMovementSpeed = 2f;

    [Header("Crouch")]
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private bool toggleCrouch = false;
    [SerializeField] private float crouchHeight = 1.1f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    [SerializeField] private LayerMask crouchObstructionMask = ~0;

    [Header("Step Offset")]
    [SerializeField] private float crouchStepOffset = 0.1f;

    [Header("Camera")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float cameraCrouchOffset = 0.45f;
    [SerializeField] private float cameraTransitionSpeed = 10f;

    private CharacterController charController;

    private float standingHeight;
    private Vector3 standingCenter;
    private float standingBottomY;
    private float standingCameraY;
    private float standingStepOffset;

    private bool isCrouching;
    private bool toggleCrouchState;

    private readonly Collider[] overlapResults = new Collider[8];

    private void Awake()
    {
        charController = GetComponent<CharacterController>();

        standingHeight = charController.height;
        standingCenter = charController.center;
        standingBottomY = standingCenter.y - (standingHeight * 0.5f);
        standingStepOffset = charController.stepOffset;

        if (cameraHolder != null)
        {
            standingCameraY = cameraHolder.localPosition.y;
        }
    }

    private void Update()
    {
        HandleCrouchInput();
        UpdateCrouchState();
        UpdateControllerHeight();
        UpdateMovement();
        UpdateCameraHeight();
        UpdateStepOffset();
    }

    private void HandleCrouchInput()
    {
        if (toggleCrouch && Input.GetKeyDown(crouchKey))
        {
            toggleCrouchState = !toggleCrouchState;
        }
    }

    private void UpdateCrouchState()
    {
        bool wantsToCrouch = toggleCrouch ? toggleCrouchState : Input.GetKey(crouchKey);

        if (wantsToCrouch)
        {
            isCrouching = true;
            return;
        }

        if (CanStandUp())
        {
            isCrouching = false;
        }
        else
        {
            isCrouching = true;
        }
    }

    private void UpdateMovement()
    {
        float currentSpeed = isCrouching ? crouchMovementSpeed : movementSpeed;

        float horizontal = Input.GetAxis(horizontalInputName);
        float vertical = Input.GetAxis(verticalInputName);

        Vector3 move = (transform.right * horizontal) + (transform.forward * vertical);
        move = Vector3.ClampMagnitude(move, 1f) * currentSpeed;

        charController.SimpleMove(move);
    }

    private void UpdateControllerHeight()
    {
        float targetHeight = isCrouching ? crouchHeight : standingHeight;

        float newHeight = Mathf.MoveTowards(
            charController.height,
            targetHeight,
            crouchTransitionSpeed * Time.deltaTime
        );

        charController.height = newHeight;

        Vector3 center = charController.center;
        center.y = standingBottomY + (newHeight * 0.5f);
        charController.center = center;
    }

    private void UpdateCameraHeight()
    {
        if (cameraHolder == null)
            return;

        Vector3 localPos = cameraHolder.localPosition;

        float targetY = standingCameraY - (isCrouching ? cameraCrouchOffset : 0f);

        localPos.y = Mathf.MoveTowards(
            localPos.y,
            targetY,
            cameraTransitionSpeed * Time.deltaTime
        );

        cameraHolder.localPosition = localPos;
    }

    private void UpdateStepOffset()
    {
        if (charController.isGrounded)
        {
            float targetStepOffset = isCrouching ? crouchStepOffset : standingStepOffset;
            charController.stepOffset = Mathf.Min(targetStepOffset, charController.height);
        }
        else
        {
            charController.stepOffset = 0f;
        }
    }

    private bool CanStandUp()
    {
        float radius = Mathf.Max(0.05f, charController.radius - 0.02f);

        Vector3 capsuleCenter = transform.TransformPoint(standingCenter);
        float pointOffset = Mathf.Max(0f, (standingHeight * 0.5f) - radius);

        Vector3 bottom = capsuleCenter - Vector3.up * pointOffset;
        Vector3 top = capsuleCenter + Vector3.up * pointOffset;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            overlapResults,
            crouchObstructionMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];

            if (hit != null && hit != charController)
            {
                return false;
            }
        }

        return true;
    }
}