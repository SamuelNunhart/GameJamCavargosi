using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class WindowSequenceController : MonoBehaviour
{
    private const int HotspotCount = 7;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private string promptMessage = "Press E to look out the window";
    [SerializeField] private float transitionDuration = 0.35f;
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Player Control")]
    [SerializeField] private Transform playerViewTransform;
    [SerializeField] private PlayerControlLocker playerControlLocker;
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;

    [Header("UI Text")]
    [SerializeField] private string instructionMessage = "Find all 7 objects in the panorama.";
    [SerializeField] private string completionMessage = "Everything found.";
    [SerializeField] private float completionCloseDelay = 1f;

    [Header("Visual State")]
    [SerializeField] private Color incompleteTargetColor = Color.white;
    [SerializeField] private Color completedTargetColor = new Color(0.6f, 1f, 0.6f, 1f);

    [Header("Scene Progression")]
    [SerializeField] private DialogueData completionDialogue;
    [SerializeField] private bool lockPlayerControlsDuringCompletionDialogue;
    [SerializeField] private UnityEvent onSequenceCompleted;

    [Header("Auto-Resolved Scene Objects")]
    [SerializeField] private Transform windowSequenceRoot;
    [SerializeField] private Transform windowPromptAnchor;
    [SerializeField] private Transform windowViewAnchor;
    [SerializeField] private GameObject hideSeekCanvas;
    [SerializeField] private RectTransform panoramaContainer;
    [SerializeField] private Image panoramaImage;
    [SerializeField] private RectTransform targetPanelLeft;
    [SerializeField] private RectTransform targetPanelRight;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text foundCountText;
    [SerializeField] private GameObject completionPanel;

    private readonly Button[] hotspotButtons = new Button[HotspotCount];
    private readonly Image[] targetImages = new Image[HotspotCount];
    private readonly bool[] foundStates = new bool[HotspotCount];
    private readonly Color[] initialTargetColors = new Color[HotspotCount];

    private Collider triggerCollider;
    private bool playerInRange;
    private bool sequenceOpen;
    private bool isTransitioning;
    private bool completionHandled;
    private bool playerControlLockHeld;
    private bool promptVisible;
    private int foundCount;
    private string initialInstructionText;
    private Vector3 savedViewPosition;
    private Quaternion savedViewRotation;

    public bool IsOpen => sequenceOpen;
    public bool IsCompleted => completionHandled;

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

        initialInstructionText = instructionText != null && !string.IsNullOrWhiteSpace(instructionText.text)
            ? instructionText.text
            : instructionMessage;

        RegisterHotspotListeners();
        ResetMinigameState();

        if (hideSeekCanvas != null)
            hideSeekCanvas.SetActive(false);

        if (completionPanel != null)
            completionPanel.SetActive(false);

        ShowPrompt(false);
    }

    private void Update()
    {
        if (!completionHandled && !sequenceOpen && !isTransitioning && playerInRange && CanInteractNow() && Input.GetKeyDown(interactKey))
        {
            DebugLog("Interact key pressed while in range. Opening window sequence.");
            OpenSequence();
            return;
        }

        if (sequenceOpen && !isTransitioning && Input.GetKeyDown(cancelKey))
        {
            CloseSequence(false);
            return;
        }

        RefreshPrompt();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = true;
        DebugLog($"Player entered trigger with collider '{other.name}'.");
        RefreshPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = false;
        DebugLog($"Player exited trigger with collider '{other.name}'.");
        RefreshPrompt();
    }

    private void OnTriggerStay(Collider other)
    {
        if (playerInRange || !IsPlayerCollider(other))
            return;

        playerInRange = true;
        DebugLog($"Player detected via OnTriggerStay with collider '{other.name}'.");
        RefreshPrompt();
    }

    private void OnDisable()
    {
        ShowPrompt(false);

        if (hideSeekCanvas != null)
            hideSeekCanvas.SetActive(false);

        if (completionPanel != null)
            completionPanel.SetActive(false);

        if (sequenceOpen)
            RestoreCursorState();

        ReleasePlayerControlLock();
    }

    public void OpenSequence()
    {
        if (sequenceOpen || isTransitioning || completionHandled)
        {
            DebugLog($"OpenSequence ignored. sequenceOpen={sequenceOpen}, isTransitioning={isTransitioning}, completionHandled={completionHandled}");
            return;
        }

        StartCoroutine(OpenSequenceRoutine());
    }

    public void CloseSequence(bool completed)
    {
        if (!sequenceOpen || isTransitioning)
            return;

        StartCoroutine(CloseSequenceRoutine(completed));
    }

    public void HandleHotspotFoundByName(string hotspotName)
    {
        if (string.IsNullOrWhiteSpace(hotspotName))
            return;

        for (int i = 0; i < HotspotCount; i++)
        {
            if (hotspotButtons[i] != null && hotspotButtons[i].name == hotspotName)
            {
                HandleHotspotFound(i);
                return;
            }
        }
    }

    private void HandleHotspotFound(int index)
    {
        if (!sequenceOpen || completionHandled)
            return;

        if (index < 0 || index >= HotspotCount || foundStates[index])
            return;

        foundStates[index] = true;
        foundCount++;

        if (hotspotButtons[index] != null)
            hotspotButtons[index].interactable = false;

        if (targetImages[index] != null)
            targetImages[index].color = completedTargetColor;

        UpdateFoundCounter();

        if (foundCount >= HotspotCount)
            StartCoroutine(CompleteSequenceRoutine());
    }

    private IEnumerator OpenSequenceRoutine()
    {
        isTransitioning = true;
        sequenceOpen = true;
        DebugLog("Opening window sequence.");

        ShowPrompt(false);
        AcquirePlayerControlLock();

        if (playerViewTransform != null)
        {
            savedViewPosition = playerViewTransform.position;
            savedViewRotation = playerViewTransform.rotation;
        }

        yield return MoveToView(windowViewAnchor, saveCurrentView: false);

        if (hideSeekCanvas != null)
            hideSeekCanvas.SetActive(true);

        ApplyOpenCursorState();
        DebugLog("Window sequence opened.");
        isTransitioning = false;
    }

    private IEnumerator CloseSequenceRoutine(bool completed)
    {
        isTransitioning = true;
        DebugLog($"Closing window sequence. completed={completed}");

        if (hideSeekCanvas != null)
            hideSeekCanvas.SetActive(false);

        if (completionPanel != null)
            completionPanel.SetActive(false);

        RestoreCursorState();
        yield return MoveBackToSavedView();

        ReleasePlayerControlLock();

        sequenceOpen = false;
        isTransitioning = false;

        if (completed)
        {
            yield return PlayCompletionDialogueIfAssigned();
            onSequenceCompleted?.Invoke();
            ShowPrompt(false);
            DebugLog("Window sequence completed and close finished.");
        }
        else
        {
            RefreshPrompt();
            DebugLog("Window sequence closed without completion.");
        }
    }

    private IEnumerator CompleteSequenceRoutine()
    {
        if (completionHandled)
            yield break;

        completionHandled = true;

        if (instructionText != null)
            instructionText.text = completionMessage;

        if (completionPanel != null)
            completionPanel.SetActive(true);

        if (completionCloseDelay > 0f)
            yield return new WaitForSeconds(completionCloseDelay);

        CloseSequence(true);
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

    private IEnumerator MoveToView(Transform targetView, bool saveCurrentView)
    {
        if (playerViewTransform == null || targetView == null || transitionDuration <= 0f)
            yield break;

        Vector3 startPosition = playerViewTransform.position;
        Quaternion startRotation = playerViewTransform.rotation;

        if (saveCurrentView)
        {
            savedViewPosition = startPosition;
            savedViewRotation = startRotation;
        }

        yield return MoveView(startPosition, startRotation, targetView.position, targetView.rotation);
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
        bool shouldShowPrompt = !completionHandled && !sequenceOpen && !isTransitioning && playerInRange && CanInteractNow();
        DebugLog($"RefreshPrompt => shouldShowPrompt={shouldShowPrompt}, playerInRange={playerInRange}, completionHandled={completionHandled}, sequenceOpen={sequenceOpen}, isTransitioning={isTransitioning}, canInteract={CanInteractNow()}");
        ShowPrompt(shouldShowPrompt);
    }

    private bool CanInteractNow()
    {
        return DialogueUI.Instance == null || !DialogueUI.Instance.IsDialogueOpen;
    }

    private void ShowPrompt(bool show)
    {
        if (DialogueUI.Instance != null)
        {
            if (show && windowPromptAnchor != null)
                DialogueUI.Instance.SetPromptAnchor(windowPromptAnchor);
            else
                DialogueUI.Instance.ClearPromptAnchor();

            if (show && !DialogueUI.Instance.IsPromptAnchorInView)
            {
                DebugLog("Prompt anchor is off-screen. Falling back to default prompt position.");
                DialogueUI.Instance.ClearPromptAnchor();
            }

            DialogueUI.Instance.SetPromptVisible(show, promptMessage);
        }

        if (promptVisible == show)
            return;

        promptVisible = show;
        DebugLog($"Prompt visibility changed to {show}.");
    }

    private void ApplyOpenCursorState()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursorState()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

    private void ResetMinigameState()
    {
        foundCount = 0;
        completionHandled = false;

        for (int i = 0; i < HotspotCount; i++)
        {
            foundStates[i] = false;

            if (hotspotButtons[i] != null)
                hotspotButtons[i].interactable = true;

            if (targetImages[i] != null)
                targetImages[i].color = initialTargetColors[i];
        }

        if (instructionText != null)
            instructionText.text = initialInstructionText;

        UpdateFoundCounter();
    }

    private void UpdateFoundCounter()
    {
        if (foundCountText != null)
            foundCountText.text = foundCount + " / " + HotspotCount;
    }

    private void RegisterHotspotListeners()
    {
        for (int i = 0; i < HotspotCount; i++)
        {
            if (hotspotButtons[i] == null)
                continue;

            int hotspotIndex = i;
            hotspotButtons[i].onClick.AddListener(() => HandleHotspotFound(hotspotIndex));
        }
    }

    private void AutoAssignReferences()
    {
        triggerCollider = GetComponent<Collider>();

        if (windowSequenceRoot == null)
        {
            if (transform.parent != null && transform.parent.name == "WindowSequence")
                windowSequenceRoot = transform.parent;
            else if (name == "WindowSequence")
                windowSequenceRoot = transform;
        }

        if (windowSequenceRoot == null)
            return;

        windowPromptAnchor = FindChild(windowSequenceRoot, "WindowPromptAnchor");
        windowViewAnchor = FindChild(windowSequenceRoot, "WindowViewAnchor");

        Transform canvasTransform = FindChild(windowSequenceRoot, "HideSeekCanvas");
        hideSeekCanvas = canvasTransform != null ? canvasTransform.gameObject : null;

        panoramaContainer = FindChild(windowSequenceRoot, "PanoramaContainer") as RectTransform;
        targetPanelLeft = FindChild(windowSequenceRoot, "TargetPanelLeft") as RectTransform;
        targetPanelRight = FindChild(windowSequenceRoot, "TargetPanelRight") as RectTransform;

        Transform panoramaImageTransform = FindChild(windowSequenceRoot, "PanoramaImage");
        panoramaImage = panoramaImageTransform != null ? panoramaImageTransform.GetComponent<Image>() : null;

        Transform instructionTransform = FindChild(windowSequenceRoot, "InstructionText");
        instructionText = instructionTransform != null ? instructionTransform.GetComponent<TMP_Text>() : null;

        Transform foundCountTransform = FindChild(windowSequenceRoot, "FoundCountText");
        foundCountText = foundCountTransform != null ? foundCountTransform.GetComponent<TMP_Text>() : null;

        Transform completionTransform = FindChild(windowSequenceRoot, "CompletionPanel");
        completionPanel = completionTransform != null ? completionTransform.gameObject : null;

        for (int i = 0; i < HotspotCount; i++)
        {
            string suffix = (i + 1).ToString("00");

            Transform hotspotTransform = FindChild(windowSequenceRoot, "Hotspot_" + suffix);
            hotspotButtons[i] = hotspotTransform != null ? hotspotTransform.GetComponent<Button>() : null;

            Transform targetTransform = FindChild(windowSequenceRoot, "Target_" + suffix);
            targetImages[i] = targetTransform != null ? targetTransform.GetComponent<Image>() : null;

            if (targetImages[i] != null)
                initialTargetColors[i] = targetImages[i].color;
            else
                initialTargetColors[i] = incompleteTargetColor;
        }

        AutoAssignPlayerReferences();
    }

    private void AutoAssignPlayerReferences()
    {
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

    private void DebugLog(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[WindowSequenceController] {message}", this);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChild(root.GetChild(i), childName);

            if (result != null)
                return result;
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
