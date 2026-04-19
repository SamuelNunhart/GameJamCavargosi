using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class WindowSequenceController : MonoBehaviour
{
    private const int HotspotCount = 7;
    private static readonly Vector3[] PanoramaWorldCorners = new Vector3[4];
    private static readonly Vector3[] PanoramaViewportWorldCorners = new Vector3[4];

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private string promptMessage = "Press E to look out the window";
    [SerializeField] private float transitionDuration = 0.35f;
    [SerializeField] private float interactionDetectionTolerance = 0.75f;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool interactionEnabled = true;

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

    [Header("Panorama Navigation")]
    [SerializeField] private bool enablePanoramaScrolling = true;
    [SerializeField] private KeyCode scrollLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode alternateScrollLeftKey = KeyCode.A;
    [SerializeField] private KeyCode scrollRightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode alternateScrollRightKey = KeyCode.D;
    [SerializeField] private float panoramaScrollSpeed = 900f;
    [SerializeField] [Range(0.05f, 1f)] private float panoramaButtonStepViewportFraction = 0.35f;

    [Header("Scene Progression")]
    [SerializeField] private DialogueData completionDialogue;
    [SerializeField] private string completionDialogueResourcePath = "Dialogue/Scene3_AfterWindow";
    [SerializeField] private bool lockPlayerControlsDuringCompletionDialogue;
    [SerializeField] private UnityEvent onSequenceCompleted;

    [Header("Testing")]
    [SerializeField] private bool enableSkipShortcut = true;
    [SerializeField] private KeyCode skipSequenceKey = KeyCode.BackQuote;

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
    private bool sequenceCompletionNotified;
    private bool playerControlLockHeld;
    private bool promptVisible;
    private int foundCount;
    private int heldPanoramaScrollDirection;
    private string initialInstructionText;
    private Vector2 initialPanoramaAnchoredPosition;
    private Vector3 savedViewPosition;
    private Quaternion savedViewRotation;
    private bool uiLayoutNormalized;

    public bool IsOpen => sequenceOpen;
    public bool IsCompleted => completionHandled;
    public bool HasFinishedCompletionFlow => sequenceCompletionNotified;
    public bool IsInteractionEnabled => interactionEnabled;

    public event Action SequenceCompleted;

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
        NormalizeWindowMinigameLayout();
        ConfigureTrigger();
        AutoAssignCompletionDialogue();

        initialInstructionText = instructionText != null && !string.IsNullOrWhiteSpace(instructionText.text)
            ? instructionText.text
            : instructionMessage;
        initialPanoramaAnchoredPosition = panoramaImage != null
            ? panoramaImage.rectTransform.anchoredPosition
            : Vector2.zero;

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
        UpdatePlayerRangeState();

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

        if (sequenceOpen && !isTransitioning && !completionHandled && enableSkipShortcut && Input.GetKeyDown(skipSequenceKey))
        {
            DebugLog("Skip shortcut pressed. Completing window sequence for testing.");
            SkipSequenceForTesting();
            return;
        }

        if (sequenceOpen && !isTransitioning)
            UpdatePanoramaScroll();

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
        heldPanoramaScrollDirection = 0;

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
        if (!interactionEnabled || sequenceOpen || isTransitioning || completionHandled)
        {
            DebugLog($"OpenSequence ignored. interactionEnabled={interactionEnabled}, sequenceOpen={sequenceOpen}, isTransitioning={isTransitioning}, completionHandled={completionHandled}");
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

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        UpdatePlayerRangeState();
        RefreshPrompt();
    }

    public void ScrollLeft()
    {
        ScrollPanoramaLeft();
    }

    public void ScrollRight()
    {
        ScrollPanoramaRight();
    }

    public void ScrollPanoramaLeft()
    {
        MovePanoramaHorizontally(GetPanoramaButtonStep());
    }

    public void ScrollPanoramaRight()
    {
        MovePanoramaHorizontally(-GetPanoramaButtonStep());
    }

    public void BeginScrollLeft()
    {
        BeginPanoramaScrollLeft();
    }

    public void BeginScrollRight()
    {
        BeginPanoramaScrollRight();
    }

    public void BeginPanoramaScrollLeft()
    {
        heldPanoramaScrollDirection = -1;
    }

    public void BeginPanoramaScrollRight()
    {
        heldPanoramaScrollDirection = 1;
    }

    public void EndScroll()
    {
        EndPanoramaScroll();
    }

    public void EndPanoramaScroll()
    {
        heldPanoramaScrollDirection = 0;
    }

    private void HandleHotspotFound(int index)
    {
        if (!sequenceOpen || completionHandled)
            return;

        if (index < 0 || index >= HotspotCount || foundStates[index])
            return;

        MarkHotspotAsFound(index);

        UpdateFoundCounter();

        if (foundCount >= HotspotCount)
            StartCoroutine(CompleteSequenceRoutine());
    }

    private void SkipSequenceForTesting()
    {
        if (!sequenceOpen || isTransitioning || completionHandled)
            return;

        for (int i = 0; i < HotspotCount; i++)
        {
            if (!foundStates[i])
                MarkHotspotAsFound(i);
        }

        UpdateFoundCounter();
        StartCoroutine(CompleteSequenceRoutine());
    }

    private IEnumerator OpenSequenceRoutine()
    {
        isTransitioning = true;
        sequenceOpen = true;
        DebugLog("Opening window sequence.");
        GameplayMusicController.PlayWindowMinigameMusic();

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

        ClampPanoramaToViewport();
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

        heldPanoramaScrollDirection = 0;
        RestoreCursorState();
        yield return MoveBackToSavedView();
        GameplayMusicController.RestorePreviousMusic();

        ReleasePlayerControlLock();

        sequenceOpen = false;
        isTransitioning = false;

        if (completed)
        {
            yield return PlayCompletionDialogueIfAssigned();
            onSequenceCompleted?.Invoke();
            sequenceCompletionNotified = true;
            SequenceCompleted?.Invoke();
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
        UpdatePlayerRangeState();
        bool canInteract = CanInteractNow();
        bool shouldShowPrompt = interactionEnabled && !completionHandled && !sequenceOpen && !isTransitioning && playerInRange && canInteract;
        bool promptNeedsRefresh =
            shouldShowPrompt &&
            DialogueUI.Instance != null &&
            !DialogueUI.Instance.IsPromptActiveFor(this, promptMessage);

        DebugLog($"RefreshPrompt => shouldShowPrompt={shouldShowPrompt}, playerInRange={playerInRange}, completionHandled={completionHandled}, sequenceOpen={sequenceOpen}, isTransitioning={isTransitioning}, canInteract={canInteract}, promptNeedsRefresh={promptNeedsRefresh}");

        if (promptVisible == shouldShowPrompt && !promptNeedsRefresh)
            return;

        ShowPrompt(shouldShowPrompt);
    }

    private bool CanInteractNow()
    {
        return interactionEnabled && (DialogueUI.Instance == null || !DialogueUI.Instance.IsDialogueOpen);
    }

    private void ShowPrompt(bool show)
    {
        if (DialogueUI.Instance != null)
        {
            DialogueUI.Instance.ClearPromptAnchor();
            DialogueUI.Instance.SetPromptVisible(this, show, promptMessage);
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
        sequenceCompletionNotified = false;
        heldPanoramaScrollDirection = 0;

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

        if (panoramaImage != null)
            panoramaImage.rectTransform.anchoredPosition = initialPanoramaAnchoredPosition;

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

    private void MarkHotspotAsFound(int index)
    {
        foundStates[index] = true;
        foundCount++;

        if (hotspotButtons[index] != null)
            hotspotButtons[index].interactable = false;

        if (targetImages[index] != null)
            targetImages[index].color = completedTargetColor;
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

    private void NormalizeWindowMinigameLayout()
    {
        if (uiLayoutNormalized || hideSeekCanvas == null)
            return;

        RectTransform canvasRect = hideSeekCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        NormalizeCanvasRoot(canvasRect);
        NormalizeInstructionPanel(canvasRect);
        NormalizeTargetPanel(canvasRect);

        uiLayoutNormalized = true;
        Canvas.ForceUpdateCanvases();
    }

    private void NormalizeCanvasRoot(RectTransform canvasRect)
    {
        Canvas canvas = hideSeekCanvas.GetComponent<Canvas>();
        if (canvas != null)
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler canvasScaler = hideSeekCanvas.GetComponent<CanvasScaler>();
        if (canvasScaler != null)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
        }

        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
    }

    private void NormalizeInstructionPanel(RectTransform canvasRect)
    {
        RectTransform instructionPanelRect = instructionText != null
            ? instructionText.rectTransform.parent as RectTransform
            : null;

        if (instructionPanelRect == null)
            return;

        FlattenPanelHierarchy(instructionPanelRect, canvasRect);
        targetPanelLeft = instructionPanelRect;
    }

    private void NormalizeTargetPanel(RectTransform canvasRect)
    {
        RectTransform targetPanelRect = null;

        for (int i = 0; i < targetImages.Length; i++)
        {
            if (targetImages[i] == null)
                continue;

            targetPanelRect = targetImages[i].rectTransform.parent as RectTransform;
            if (targetPanelRect != null)
                break;
        }

        if (targetPanelRect == null)
            return;

        FlattenPanelHierarchy(targetPanelRect, canvasRect);
        targetPanelRight = targetPanelRect;
    }

    private void FlattenPanelHierarchy(RectTransform panelRect, RectTransform canvasRect)
    {
        if (panelRect == null || canvasRect == null)
            return;

        RectSnapshot panelSnapshot = CaptureRectSnapshot(panelRect);
        List<RectSnapshot> childSnapshots = CaptureDirectChildRectSnapshots(panelRect);

        int siblingIndex = GetCanvasSiblingIndex(panelRect, canvasRect);
        ApplyRectSnapshot(panelSnapshot, canvasRect, siblingIndex);

        for (int i = 0; i < childSnapshots.Count; i++)
            ApplyRectSnapshot(childSnapshots[i], panelRect, childSnapshots[i].SiblingIndex);
    }

    private static List<RectSnapshot> CaptureDirectChildRectSnapshots(RectTransform parentRect)
    {
        List<RectSnapshot> snapshots = new List<RectSnapshot>();
        if (parentRect == null)
            return snapshots;

        for (int i = 0; i < parentRect.childCount; i++)
        {
            RectTransform childRect = parentRect.GetChild(i) as RectTransform;
            if (childRect == null)
                continue;

            snapshots.Add(CaptureRectSnapshot(childRect));
        }

        return snapshots;
    }

    private static int GetCanvasSiblingIndex(RectTransform rectTransform, RectTransform canvasRect)
    {
        Transform currentParent = rectTransform.parent;
        if (currentParent != null && currentParent.parent == canvasRect)
            return currentParent.GetSiblingIndex();

        if (currentParent == canvasRect)
            return rectTransform.GetSiblingIndex();

        return canvasRect.childCount;
    }

    private static RectSnapshot CaptureRectSnapshot(RectTransform rectTransform)
    {
        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);

        return new RectSnapshot(
            rectTransform,
            worldCorners[0],
            worldCorners[1],
            worldCorners[2],
            worldCorners[3],
            rectTransform.GetSiblingIndex());
    }

    private static void ApplyRectSnapshot(RectSnapshot snapshot, RectTransform newParent, int siblingIndex)
    {
        if (snapshot.RectTransform == null || newParent == null)
            return;

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        CollectLocalBounds(snapshot.BottomLeft, newParent, ref min, ref max);
        CollectLocalBounds(snapshot.TopLeft, newParent, ref min, ref max);
        CollectLocalBounds(snapshot.TopRight, newParent, ref min, ref max);
        CollectLocalBounds(snapshot.BottomRight, newParent, ref min, ref max);

        RectTransform rectTransform = snapshot.RectTransform;
        rectTransform.SetParent(newParent, false);
        rectTransform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, newParent.childCount - 1));
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = max - min;
        rectTransform.anchoredPosition = (min + max) * 0.5f;
    }

    private static void CollectLocalBounds(Vector3 worldPoint, RectTransform parentRect, ref Vector2 min, ref Vector2 max)
    {
        Vector3 localPoint = parentRect.InverseTransformPoint(worldPoint);
        min = Vector2.Min(min, localPoint);
        max = Vector2.Max(max, localPoint);
    }

    private readonly struct RectSnapshot
    {
        public RectSnapshot(
            RectTransform rectTransform,
            Vector3 bottomLeft,
            Vector3 topLeft,
            Vector3 topRight,
            Vector3 bottomRight,
            int siblingIndex)
        {
            RectTransform = rectTransform;
            BottomLeft = bottomLeft;
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            SiblingIndex = siblingIndex;
        }

        public RectTransform RectTransform { get; }
        public Vector3 BottomLeft { get; }
        public Vector3 TopLeft { get; }
        public Vector3 TopRight { get; }
        public Vector3 BottomRight { get; }
        public int SiblingIndex { get; }
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

    private void UpdatePanoramaScroll()
    {
        if (!enablePanoramaScrolling || panoramaContainer == null || panoramaImage == null)
            return;

        int scrollDirection = heldPanoramaScrollDirection;

        if (Input.GetKey(scrollLeftKey) || Input.GetKey(alternateScrollLeftKey))
            scrollDirection = -1;
        else if (Input.GetKey(scrollRightKey) || Input.GetKey(alternateScrollRightKey))
            scrollDirection = 1;

        if (scrollDirection == 0)
            return;

        float deltaX = -scrollDirection * Mathf.Max(0f, panoramaScrollSpeed) * Time.unscaledDeltaTime;
        MovePanoramaHorizontally(deltaX);
    }

    private void MovePanoramaHorizontally(float deltaX)
    {
        if (!sequenceOpen || !enablePanoramaScrolling || panoramaImage == null || panoramaContainer == null || Mathf.Approximately(deltaX, 0f))
            return;

        RectTransform panoramaRect = panoramaImage.rectTransform;
        Vector2 anchoredPosition = panoramaRect.anchoredPosition;
        anchoredPosition.x += deltaX;
        panoramaRect.anchoredPosition = anchoredPosition;

        ClampPanoramaToViewport();
    }

    private void ClampPanoramaToViewport()
    {
        if (panoramaContainer == null || panoramaImage == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform viewportRect = ResolvePanoramaViewport();
        if (viewportRect == null)
            return;

        RectTransform panoramaRect = panoramaImage.rectTransform;
        GetHorizontalWorldBounds(panoramaRect, PanoramaWorldCorners, out float panoramaMinX, out float panoramaMaxX);
        GetHorizontalWorldBounds(viewportRect, PanoramaViewportWorldCorners, out float viewportMinX, out float viewportMaxX);

        float panoramaWidth = panoramaMaxX - panoramaMinX;
        float viewportWidth = viewportMaxX - viewportMinX;
        float worldDeltaX = 0f;

        if (panoramaWidth <= viewportWidth)
        {
            float panoramaCenterX = (panoramaMinX + panoramaMaxX) * 0.5f;
            float viewportCenterX = (viewportMinX + viewportMaxX) * 0.5f;
            worldDeltaX = viewportCenterX - panoramaCenterX;
        }
        else
        {
            if (panoramaMinX > viewportMinX)
                worldDeltaX = viewportMinX - panoramaMinX;
            else if (panoramaMaxX < viewportMaxX)
                worldDeltaX = viewportMaxX - panoramaMaxX;
        }

        if (Mathf.Approximately(worldDeltaX, 0f))
            return;

        Vector2 anchoredPosition = panoramaRect.anchoredPosition;
        anchoredPosition.x += ConvertWorldDeltaToAnchoredDeltaX(panoramaRect, worldDeltaX);
        panoramaRect.anchoredPosition = anchoredPosition;
    }

    private RectTransform ResolvePanoramaViewport()
    {
        if (panoramaContainer == null)
            return hideSeekCanvas != null ? hideSeekCanvas.transform as RectTransform : null;

        RectMask2D rectMask = panoramaContainer.GetComponentInParent<RectMask2D>(includeInactive: true);
        if (rectMask != null)
            return rectMask.transform as RectTransform;

        Mask mask = panoramaContainer.GetComponentInParent<Mask>(includeInactive: true);
        if (mask != null)
            return mask.transform as RectTransform;

        return panoramaContainer;
    }

    private static void GetHorizontalWorldBounds(RectTransform rectTransform, Vector3[] cornersBuffer, out float minX, out float maxX)
    {
        rectTransform.GetWorldCorners(cornersBuffer);

        minX = float.PositiveInfinity;
        maxX = float.NegativeInfinity;

        for (int i = 0; i < cornersBuffer.Length; i++)
        {
            minX = Mathf.Min(minX, cornersBuffer[i].x);
            maxX = Mathf.Max(maxX, cornersBuffer[i].x);
        }
    }

    private static float ConvertWorldDeltaToAnchoredDeltaX(RectTransform rectTransform, float worldDeltaX)
    {
        if (rectTransform == null || rectTransform.parent == null)
            return worldDeltaX;

        Vector3 localDelta = rectTransform.parent.InverseTransformVector(new Vector3(worldDeltaX, 0f, 0f));
        return localDelta.x;
    }

    private float GetPanoramaButtonStep()
    {
        if (panoramaContainer == null)
            return 0f;

        float viewportWidth = panoramaContainer.rect.width;
        float viewportFraction = Mathf.Clamp01(panoramaButtonStepViewportFraction);
        return viewportWidth * viewportFraction;
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
        DebugLog($"Player range state updated by proximity check. playerInRange={playerInRange}");
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
