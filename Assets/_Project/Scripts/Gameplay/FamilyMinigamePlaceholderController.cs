using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class FamilyMinigamePlaceholderController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private KeyCode skipKey = KeyCode.BackQuote;
    [SerializeField] private string promptMessage = "Press E";
    [SerializeField] private float transitionDuration = 0.35f;
    [SerializeField] private float interactionDetectionTolerance = 0.75f;
    [SerializeField] private bool enableDebugLogs;

    [Header("Scene Progression")]
    [SerializeField] private DialogueData completionDialogue;
    [SerializeField] private string completionDialogueResourcePath = "Dialogue/Scene5_AfterFamilyMinigame";
    [SerializeField] private bool lockPlayerControlsDuringCompletionDialogue;
    [SerializeField] private UnityEvent onSequenceCompleted;

    [Header("Player Control")]
    [SerializeField] private Transform playerViewTransform;
    [SerializeField] private PlayerControlLocker playerControlLocker;
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;

    [Header("Scene Objects")]
    [SerializeField] private Transform minigameViewPoint;
    [SerializeField] private string minigameViewPointObjectName = "FamilyMinigameViewPoint";

    private Collider triggerCollider;
    private bool playerInRange;
    private bool sequenceOpen;
    private bool isTransitioning;
    private bool completionHandled;
    private bool promptVisible;
    private bool playerControlLockHeld;
    private Vector3 savedViewPosition;
    private Quaternion savedViewRotation;

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
        AutoAssignCompletionDialogue();
        ShowPrompt(false);
    }

    private void Update()
    {
        UpdatePlayerRangeState();

        if (!completionHandled &&
            !sequenceOpen &&
            !isTransitioning &&
            playerInRange &&
            CanInteractNow() &&
            Input.GetKeyDown(interactKey))
        {
            DebugLog("Interact key pressed while in range. Opening family minigame placeholder.");
            OpenSequence();
            return;
        }

        if (sequenceOpen && !isTransitioning && Input.GetKeyDown(cancelKey))
        {
            DebugLog("Cancel key pressed. Closing family minigame placeholder without completion.");
            CloseSequence(false);
            return;
        }

        if (sequenceOpen && !isTransitioning && !completionHandled && Input.GetKeyDown(skipKey))
        {
            DebugLog("Skip key pressed. Completing family minigame placeholder.");
            CompleteSequence();
            return;
        }

        RefreshPrompt();
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

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        ShowPrompt(false);
        promptVisible = false;

        if (sequenceOpen && playerViewTransform != null)
            playerViewTransform.SetPositionAndRotation(savedViewPosition, savedViewRotation);

        ReleasePlayerControlLock();
        sequenceOpen = false;
        isTransitioning = false;
    }

    public void OpenSequence()
    {
        if (completionHandled || sequenceOpen || isTransitioning)
            return;

        StartCoroutine(OpenSequenceRoutine());
    }

    public void CloseSequence(bool completed)
    {
        if (!sequenceOpen || isTransitioning)
            return;

        StartCoroutine(CloseSequenceRoutine(completed));
    }

    public void CompleteSequence()
    {
        if (completionHandled || !sequenceOpen || isTransitioning)
            return;

        completionHandled = true;
        StartCoroutine(CloseSequenceRoutine(true));
    }

    private IEnumerator OpenSequenceRoutine()
    {
        isTransitioning = true;
        sequenceOpen = true;

        ShowPrompt(false);
        AcquirePlayerControlLock();

        if (playerViewTransform != null)
        {
            savedViewPosition = playerViewTransform.position;
            savedViewRotation = playerViewTransform.rotation;
        }

        yield return MoveToView(minigameViewPoint);

        isTransitioning = false;
        DebugLog("Family minigame placeholder opened.");
    }

    private IEnumerator CloseSequenceRoutine(bool completed)
    {
        isTransitioning = true;

        yield return MoveBackToSavedView();

        ReleasePlayerControlLock();

        sequenceOpen = false;
        isTransitioning = false;

        if (completed)
        {
            yield return PlayCompletionDialogueIfAssigned();
            onSequenceCompleted?.Invoke();
            ShowPrompt(false);
            DebugLog("Family minigame placeholder completed.");
        }
        else
        {
            RefreshPrompt();
            DebugLog("Family minigame placeholder closed without completion.");
        }
    }

    private IEnumerator PlayCompletionDialogueIfAssigned()
    {
        if (completionDialogue == null || DialogueUI.Instance == null)
            yield break;

        bool finished = false;
        bool started = DialogueUI.Instance.StartDialogue(
            completionDialogue,
            lockPlayerControlsDuringCompletionDialogue,
            () => finished = true
        );

        if (!started)
            yield break;

        while (!finished)
            yield return null;
    }

    private IEnumerator MoveToView(Transform targetView)
    {
        if (playerViewTransform == null || targetView == null || transitionDuration <= 0f)
            yield break;

        yield return MoveView(
            playerViewTransform.position,
            playerViewTransform.rotation,
            targetView.position,
            targetView.rotation
        );
    }

    private IEnumerator MoveBackToSavedView()
    {
        if (playerViewTransform == null || transitionDuration <= 0f)
            yield break;

        yield return MoveView(
            playerViewTransform.position,
            playerViewTransform.rotation,
            savedViewPosition,
            savedViewRotation
        );
    }

    private IEnumerator MoveView(Vector3 startPosition, Quaternion startRotation, Vector3 endPosition, Quaternion endRotation)
    {
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = t * t * (3f - 2f * t);

            playerViewTransform.position = Vector3.Lerp(startPosition, endPosition, t);
            playerViewTransform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        playerViewTransform.position = endPosition;
        playerViewTransform.rotation = endRotation;
    }

    private void RefreshPrompt()
    {
        UpdatePlayerRangeState();

        bool shouldShowPrompt = !completionHandled && !sequenceOpen && !isTransitioning && playerInRange && CanInteractNow();
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
        return DialogueUI.Instance == null || !DialogueUI.Instance.IsDialogueOpen;
    }

    private void ShowPrompt(bool show)
    {
        if (DialogueUI.Instance == null)
            return;

        DialogueUI.Instance.ClearPromptAnchor();
        DialogueUI.Instance.SetPromptVisible(this, show, promptMessage);
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

    private void AutoAssignCompletionDialogue()
    {
        if (completionDialogue != null || string.IsNullOrWhiteSpace(completionDialogueResourcePath))
            return;

        completionDialogue = Resources.Load<DialogueData>(completionDialogueResourcePath);
    }

    private void AutoAssignReferences()
    {
        triggerCollider = GetComponent<Collider>();

        if (string.IsNullOrWhiteSpace(minigameViewPointObjectName))
            minigameViewPointObjectName = "FamilyMinigameViewPoint";

        if (minigameViewPoint == null)
        {
            Transform targetTransform = FindTransformInScene(minigameViewPointObjectName);
            if (targetTransform != null)
                minigameViewPoint = targetTransform;
        }

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

        if (playerControlLocker == null)
        {
            PlayerControlLocker[] controlLockers = FindObjectsOfType<PlayerControlLocker>(true);
            if (controlLockers.Length > 0)
                playerControlLocker = controlLockers[0];
        }
    }

    private void ConfigureTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
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

    private void DebugLog(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log("[FamilyMinigamePlaceholderController] " + message, this);
    }

    private static Transform FindTransformInScene(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transformCandidate = transforms[i];

            if (transformCandidate != null && transformCandidate.name == targetName)
                return transformCandidate;
        }

        return null;
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
