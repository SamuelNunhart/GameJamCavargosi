using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("Prompt UI")]
    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private TMP_Text promptText;

    [Header("Dialogue UI")]
    [SerializeField] private CanvasGroup dialogueGroup;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text continueHintText;

    [Header("Typing")]
    [SerializeField] private float charactersPerSecond = 40f;

    [Header("Audio")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Control Lock")]
    [SerializeField] private PlayerControlLocker playerControlLocker;

    public bool IsDialogueOpen { get; private set; }

    private DialogueData currentDialogue;
    private int currentLineIndex;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private string fullCurrentText;
    private Action onDialogueFinished;
    private bool controlsLockedByDialogue;
    private RectTransform promptRectTransform;
    private RectTransform promptParentRectTransform;
    private Canvas promptCanvas;
    private Camera promptWorldCamera;
    private Transform promptAnchor;
    private Vector2 promptDefaultAnchoredPosition;
    private bool promptDefaultPositionCached;
    private bool promptAnchorInView = true;
    private UnityEngine.Object promptOwner;
    private string currentPromptMessage = string.Empty;

    public event Action<DialogueData> DialogueStarted;
    public event Action<DialogueData> DialogueEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        promptRectTransform = promptGroup != null ? promptGroup.transform as RectTransform : null;
        promptParentRectTransform = promptRectTransform != null ? promptRectTransform.parent as RectTransform : null;
        promptCanvas = promptRectTransform != null ? promptRectTransform.GetComponentInParent<Canvas>() : null;
        CachePromptDefaultPosition();

        SetPromptVisible(false);
        SetDialogueVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        ReleasePlayerControlLock();
    }

    private void Update()
    {
        UpdatePromptAnchorPosition();

        if (!IsDialogueOpen)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping)
                CompleteCurrentLineInstantly();
            else
                AdvanceDialogue();
        }
    }

    public bool IsPromptVisible => promptGroup != null && promptGroup.alpha > 0.001f;

    public bool IsPromptActiveFor(UnityEngine.Object owner, string message)
    {
        return owner != null &&
               promptOwner == owner &&
               IsPromptVisible &&
               string.Equals(currentPromptMessage, message ?? "Press E", StringComparison.Ordinal);
    }

    public void SetPromptVisible(bool visible, string message = "Press E")
    {
        SetPromptVisible(null, visible, message);
    }

    public void SetPromptVisible(UnityEngine.Object owner, bool visible, string message = "Press E")
    {
        if (visible)
        {
            promptOwner = owner;
            currentPromptMessage = string.IsNullOrWhiteSpace(message) ? "Press E" : message;

            if (promptText != null)
                promptText.text = currentPromptMessage;

            SetCanvasGroup(promptGroup, true);
            return;
        }

        if (owner != null && promptOwner != null && promptOwner != owner)
            return;

        promptOwner = null;
        currentPromptMessage = string.Empty;
        ClearPromptAnchor();
        SetCanvasGroup(promptGroup, visible);
    }

    public void SetPromptAnchor(Transform anchor)
    {
        promptAnchor = anchor;
        promptAnchorInView = true;
        UpdatePromptAnchorPosition();
    }

    public void ClearPromptAnchor()
    {
        promptAnchor = null;
        promptAnchorInView = true;
        RestorePromptDefaultPosition();
    }

    public bool IsPromptAnchorInView => promptAnchor == null || promptAnchorInView;

    public bool StartDialogue(DialogueData dialogueData, Action finishedCallback = null)
    {
        return StartDialogue(new DialogueRequest(dialogueData, false, finishedCallback));
    }

    public bool StartDialogue(DialogueData dialogueData, bool lockPlayerControls, Action finishedCallback = null)
    {
        return StartDialogue(new DialogueRequest(dialogueData, lockPlayerControls, finishedCallback));
    }

    public bool StartDialogue(DialogueRequest request)
    {
        if (request == null || request.Dialogue == null || request.Dialogue.lines == null || request.Dialogue.lines.Length == 0)
        {
            Debug.LogWarning("No dialogue lines assigned.");
            return false;
        }

        if (IsDialogueOpen)
        {
            Debug.LogWarning("Cannot start a new dialogue while another one is already open.");
            return false;
        }

        currentDialogue = request.Dialogue;
        currentLineIndex = 0;
        onDialogueFinished = request.OnFinished;
        IsDialogueOpen = true;

        TryAcquirePlayerControlLock(request.LockPlayerControls);

        SetPromptVisible(false);
        SetDialogueVisible(true);

        DialogueStarted?.Invoke(currentDialogue);
        ShowLine(currentDialogue.lines[currentLineIndex]);
        return true;
    }

    private void AdvanceDialogue()
    {
        currentLineIndex++;

        if (currentDialogue == null || currentLineIndex >= currentDialogue.lines.Length)
        {
            EndDialogue();
            return;
        }

        ShowLine(currentDialogue.lines[currentLineIndex]);
    }

    private void ShowLine(DialogueLine line)
    {
        if (speakerNameText != null)
            speakerNameText.text = line.speakerName;

        ApplySide(line.side);

        if (voiceSource != null)
        {
            voiceSource.Stop();

            if (line.voiceClip != null)
                voiceSource.PlayOneShot(line.voiceClip);
        }

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeLine(line.text));
    }

    private IEnumerator TypeLine(string targetText)
    {
        isTyping = true;
        fullCurrentText = targetText;
        dialogueText.text = "";

        float delay = charactersPerSecond <= 0f ? 0f : 1f / charactersPerSecond;

        foreach (char c in targetText)
        {
            dialogueText.text += c;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            else
                yield return null;
        }

        dialogueText.text = targetText;
        isTyping = false;
        typingCoroutine = null;
    }

    private void CompleteCurrentLineInstantly()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        dialogueText.text = fullCurrentText;
        isTyping = false;
        typingCoroutine = null;
    }

    private void EndDialogue()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        if (voiceSource != null)
            voiceSource.Stop();

        DialogueData finishedDialogue = currentDialogue;
        currentDialogue = null;
        currentLineIndex = 0;
        isTyping = false;
        fullCurrentText = string.Empty;
        IsDialogueOpen = false;

        SetDialogueVisible(false);
        ReleasePlayerControlLock();

        Action callback = onDialogueFinished;
        onDialogueFinished = null;
        callback?.Invoke();
        DialogueEnded?.Invoke(finishedDialogue);
    }

    private void ApplySide(DialogueSpeakerSide side)
    {
        TextAlignmentOptions alignment =
            side == DialogueSpeakerSide.Left
                ? TextAlignmentOptions.Left
                : TextAlignmentOptions.Right;

        if (speakerNameText != null)
            speakerNameText.alignment = alignment;

        if (dialogueText != null)
            dialogueText.alignment = alignment;
    }

    private void SetDialogueVisible(bool visible)
    {
        SetCanvasGroup(dialogueGroup, visible);

        if (!visible)
        {
            if (speakerNameText != null) speakerNameText.text = "";
            if (dialogueText != null) dialogueText.text = "";
        }

        if (continueHintText != null)
            continueHintText.text = visible ? "E - Continue" : "";
    }

    private void SetCanvasGroup(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void UpdatePromptAnchorPosition()
    {
        if (promptAnchor == null || promptRectTransform == null || promptParentRectTransform == null)
            return;

        Camera worldCamera = GetPromptWorldCamera();
        if (worldCamera == null)
        {
            promptAnchorInView = false;
            RestorePromptDefaultPosition();
            return;
        }

        Vector3 viewportPoint = worldCamera.WorldToViewportPoint(promptAnchor.position);
        bool inFrontOfCamera = viewportPoint.z > 0f;
        bool insideViewport =
            viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
            viewportPoint.y >= 0f && viewportPoint.y <= 1f;

        promptAnchorInView = inFrontOfCamera && insideViewport;

        if (!promptAnchorInView)
        {
            RestorePromptDefaultPosition();
            return;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, promptAnchor.position);
        Camera uiCamera = promptCanvas != null && promptCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(promptParentRectTransform, screenPoint, uiCamera, out Vector2 localPoint))
            promptRectTransform.anchoredPosition = localPoint;
    }

    private Camera GetPromptWorldCamera()
    {
        if (promptCanvas != null && promptCanvas.worldCamera != null)
            return promptCanvas.worldCamera;

        if (promptWorldCamera == null)
            promptWorldCamera = Camera.main;

        return promptWorldCamera;
    }

    private void CachePromptDefaultPosition()
    {
        if (promptRectTransform == null)
            return;

        promptDefaultAnchoredPosition = promptRectTransform.anchoredPosition;
        promptDefaultPositionCached = true;
    }

    private void RestorePromptDefaultPosition()
    {
        if (!promptDefaultPositionCached || promptRectTransform == null)
            return;

        promptRectTransform.anchoredPosition = promptDefaultAnchoredPosition;
    }

    private void TryAcquirePlayerControlLock(bool shouldLock)
    {
        controlsLockedByDialogue = false;

        if (!shouldLock)
            return;

        if (playerControlLocker == null)
        {
            Debug.LogWarning("Dialogue requested a player control lock, but no PlayerControlLocker is assigned.");
            return;
        }

        playerControlLocker.AcquireLock();
        controlsLockedByDialogue = true;
    }

    private void ReleasePlayerControlLock()
    {
        if (!controlsLockedByDialogue)
            return;

        playerControlLocker?.ReleaseLock();
        controlsLockedByDialogue = false;
    }
}
