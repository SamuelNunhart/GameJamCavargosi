using System;
using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Collider))]
public class PuzzleOpener : MonoBehaviour
{
    [Header("Puzzle")]
    [SerializeField] private GameObject puzzleCanvas;

    [Header("Player Control")]
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;
    [SerializeField] private PlayerControlLocker playerControlLocker;

    [Header("View Transition")]
    [SerializeField] private Transform playerViewTransform;
    [SerializeField] private Transform puzzleViewPoint;
    [SerializeField] private Transform phoneViewPoint;
    [SerializeField] private float transitionDuration = 0.4f;

    [Header("World Objects")]
    [SerializeField] private GameObject phoneWorldObject;

    [Header("Prompt UI")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string promptMessage = "Press E to inspect puzzle";
    [SerializeField] private float interactionDetectionTolerance = 0.75f;
    [SerializeField] private bool interactionEnabled = true;

    private Collider triggerCollider;
    private bool playerInRange;
    private bool puzzleOpen;
    private bool isTransitioning;
    private bool promptVisible;

    private Vector3 savedViewPosition;
    private Quaternion savedViewRotation;
    private bool playerControlLockHeld;

    public bool IsPuzzleOpen => puzzleOpen;
    public bool IsTransitioning => isTransitioning;
    public bool IsInteractionEnabled => interactionEnabled;
    public GameObject PhoneWorldObject => phoneWorldObject;

    public event Action PuzzleOpened;
    public event Action PuzzleClosed;

    private void Reset()
    {
        AutoAssignReferences();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        ConfigureTrigger();
    }

    private void Awake()
    {
        AutoAssignReferences();
        ConfigureTrigger();
    }

    private void Start()
    {
        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(false);

        if (phoneWorldObject != null)
            phoneWorldObject.SetActive(false);

        if (DialogueUI.Instance == null)
            HideLegacyPrompt();
    }

    private void Update()
    {
        UpdatePlayerRangeState();
        RefreshPrompt();

        if (DialogueUI.Instance != null && DialogueUI.Instance.IsDialogueOpen)
            return;

        if (!puzzleOpen && !isTransitioning && playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            OpenPuzzle();
        }

        if (puzzleOpen && !isTransitioning && Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePuzzle();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = true;
        RefreshPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = false;
        RefreshPrompt();
    }

    private void OnTriggerStay(Collider other)
    {
        if (playerInRange || !IsPlayerCollider(other))
            return;

        playerInRange = true;
        RefreshPrompt();
    }

    private void ShowPrompt(bool show)
    {
        if (DialogueUI.Instance != null)
        {
            DialogueUI.Instance.ClearPromptAnchor();
            DialogueUI.Instance.SetPromptVisible(this, show && !DialogueUI.Instance.IsDialogueOpen, promptMessage);

            HideLegacyPrompt();
            return;
        }

        if (promptRoot != null)
            promptRoot.SetActive(show);

        if (show && promptText != null)
            promptText.text = promptMessage;
    }

    private void RefreshPrompt()
    {
        bool shouldShowPrompt = !puzzleOpen && !isTransitioning && playerInRange && CanInteractNow();
        bool promptNeedsRefresh =
            shouldShowPrompt &&
            DialogueUI.Instance != null &&
            !DialogueUI.Instance.IsPromptActiveFor(this, promptMessage);

        if (promptVisible == shouldShowPrompt && !promptNeedsRefresh)
            return;

        ShowPrompt(shouldShowPrompt);
        promptVisible = shouldShowPrompt;
    }

    private bool CanInteractNow()
    {
        return interactionEnabled && (DialogueUI.Instance == null || !DialogueUI.Instance.IsDialogueOpen);
    }

    private void OnDisable()
    {
        ShowPrompt(false);
        promptVisible = false;
        ReleasePlayerControlLock();
    }

    private void HideLegacyPrompt()
    {
        if (DialogueUI.Instance == null && promptRoot != null)
            promptRoot.SetActive(false);
    }

    public void OpenPuzzle()
    {
        if (!interactionEnabled || puzzleOpen || isTransitioning)
            return;

        StartCoroutine(OpenPuzzleRoutine());
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        RefreshPrompt();
    }

    public void ClosePuzzle()
    {
        if (!puzzleOpen || isTransitioning)
            return;

        StartCoroutine(ClosePuzzleRoutine());
    }

    public void SetPuzzleCanvasVisible(bool visible)
    {
        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(visible);
    }

    public void SetPhoneWorldVisible(bool visible)
    {
        if (phoneWorldObject != null)
            phoneWorldObject.SetActive(visible);
    }

    public IEnumerator MoveToPhoneViewRoutine()
    {
        if (isTransitioning)
            yield break;

        if (playerViewTransform == null || phoneViewPoint == null || transitionDuration <= 0f)
            yield break;

        isTransitioning = true;

        yield return MoveView(
            playerViewTransform,
            playerViewTransform.position,
            playerViewTransform.rotation,
            phoneViewPoint.position,
            phoneViewPoint.rotation,
            transitionDuration
        );

        isTransitioning = false;
    }

    public IEnumerator MoveToPuzzleViewRoutine()
    {
        if (isTransitioning)
            yield break;

        if (playerViewTransform == null || puzzleViewPoint == null || transitionDuration <= 0f)
            yield break;

        isTransitioning = true;

        yield return MoveView(
            playerViewTransform,
            playerViewTransform.position,
            playerViewTransform.rotation,
            puzzleViewPoint.position,
            puzzleViewPoint.rotation,
            transitionDuration
        );

        isTransitioning = false;
    }

    private IEnumerator OpenPuzzleRoutine()
    {
        isTransitioning = true;
        puzzleOpen = true;

        ShowPrompt(false);
        AcquirePlayerControlLock();

        bool canMoveView = playerViewTransform != null && puzzleViewPoint != null && transitionDuration > 0f;

        if (canMoveView)
        {
            savedViewPosition = playerViewTransform.position;
            savedViewRotation = playerViewTransform.rotation;

            yield return MoveView(
                playerViewTransform,
                savedViewPosition,
                savedViewRotation,
                puzzleViewPoint.position,
                puzzleViewPoint.rotation,
                transitionDuration
            );
        }

        if (phoneWorldObject != null)
            phoneWorldObject.SetActive(true);

        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isTransitioning = false;
        PuzzleOpened?.Invoke();
    }

    private IEnumerator ClosePuzzleRoutine()
    {
        isTransitioning = true;

        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(false);

        if (phoneWorldObject != null)
            phoneWorldObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        bool canMoveView = playerViewTransform != null && puzzleViewPoint != null && transitionDuration > 0f;

        if (canMoveView)
        {
            yield return MoveView(
                playerViewTransform,
                playerViewTransform.position,
                playerViewTransform.rotation,
                savedViewPosition,
                savedViewRotation,
                transitionDuration
            );
        }

        ReleasePlayerControlLock();

        puzzleOpen = false;
        isTransitioning = false;

        if (playerInRange)
            RefreshPrompt();

        PuzzleClosed?.Invoke();
    }

    private IEnumerator MoveView(
        Transform viewTransform,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            viewTransform.position = Vector3.Lerp(startPosition, endPosition, t);
            viewTransform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        viewTransform.position = endPosition;
        viewTransform.rotation = endRotation;
    }

    private void AcquirePlayerControlLock()
    {
        if (playerControlLockHeld)
            return;

        if (playerControlLocker != null)
        {
            playerControlLocker.AcquireLock();
        }
        else
        {
            if (playerMovement != null)
                playerMovement.enabled = false;

            if (playerLook != null)
                playerLook.enabled = false;
        }

        playerControlLockHeld = true;
    }

    private void ReleasePlayerControlLock()
    {
        if (!playerControlLockHeld)
            return;

        if (playerControlLocker != null)
        {
            playerControlLocker.ReleaseLock();
        }
        else
        {
            if (playerMovement != null)
                playerMovement.enabled = true;

            if (playerLook != null)
                playerLook.enabled = true;
        }

        playerControlLockHeld = false;
    }

    private void UpdatePlayerRangeState()
    {
        bool detectedInRange = IsPlayerWithinInteractionZone();

        if (playerInRange == detectedInRange)
            return;

        playerInRange = detectedInRange;
    }

    private bool IsPlayerWithinInteractionZone()
    {
        if (triggerCollider == null)
            return false;

        Transform playerReference = GetPlayerReferenceTransform();
        if (playerReference == null)
            return false;

        Vector3 playerPosition = playerReference.position;
        Vector3 closestPoint = triggerCollider.ClosestPoint(playerPosition);
        float sqrDistance = (closestPoint - playerPosition).sqrMagnitude;
        float tolerance = Mathf.Max(0f, interactionDetectionTolerance);

        return sqrDistance <= tolerance * tolerance;
    }

    private Transform GetPlayerReferenceTransform()
    {
        if (playerMovement != null)
            return playerMovement.transform;

        if (playerLook != null)
            return playerLook.transform.root;

        if (playerViewTransform != null)
            return playerViewTransform.root;

        return Camera.main != null ? Camera.main.transform.root : null;
    }

    private void AutoAssignReferences()
    {
        triggerCollider = GetComponent<Collider>();

        if (playerViewTransform == null && Camera.main != null)
            playerViewTransform = Camera.main.transform;

        if (playerViewTransform != null)
        {
            if (playerMovement == null)
                playerMovement = FindBehaviourInParents(playerViewTransform, "PlayerMove");

            if (playerLook == null)
                playerLook = FindBehaviourInParents(playerViewTransform, "PlayerLook");
        }

        if (playerMovement == null)
            playerMovement = FindBehaviourInScene("PlayerMove");

        if (playerLook == null)
            playerLook = FindBehaviourInScene("PlayerLook");
    }

    private void ConfigureTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        if (playerViewTransform == null)
            return IsLikelyPlayerHierarchy(other.transform);

        if (other.transform == playerViewTransform || other.transform.IsChildOf(playerViewTransform.root))
            return true;

        return IsLikelyPlayerHierarchy(other.transform);
    }

    private bool IsLikelyPlayerHierarchy(Transform candidate)
    {
        if (candidate == null)
            return false;

        Transform candidateRoot = candidate.root;

        if (candidateRoot.CompareTag("Player"))
            return true;

        if (!string.IsNullOrEmpty(candidateRoot.name) && candidateRoot.name.Contains("Player"))
            return true;

        if (playerMovement != null && candidateRoot == playerMovement.transform.root)
            return true;

        if (playerLook != null && candidateRoot == playerLook.transform.root)
            return true;

        if (playerViewTransform != null && candidateRoot == playerViewTransform.root)
            return true;

        Rigidbody attachedRigidbody = candidate.GetComponentInParent<Rigidbody>();
        if (attachedRigidbody != null)
        {
            Transform rigidbodyRoot = attachedRigidbody.transform.root;

            if (rigidbodyRoot.CompareTag("Player"))
                return true;

            if (!string.IsNullOrEmpty(rigidbodyRoot.name) && rigidbodyRoot.name.Contains("Player"))
                return true;
        }

        CharacterController characterController = candidate.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            Transform controllerRoot = characterController.transform.root;

            if (controllerRoot.CompareTag("Player"))
                return true;

            if (!string.IsNullOrEmpty(controllerRoot.name) && controllerRoot.name.Contains("Player"))
                return true;
        }

        return false;
    }

    private static MonoBehaviour FindBehaviourInParents(Transform start, string behaviourName)
    {
        if (start == null)
            return null;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour != null && behaviour.GetType().Name == behaviourName)
                return behaviour;
        }

        return null;
    }

    private static MonoBehaviour FindBehaviourInScene(string behaviourName)
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour != null && behaviour.GetType().Name == behaviourName)
                return behaviour;
        }

        return null;
    }
}
