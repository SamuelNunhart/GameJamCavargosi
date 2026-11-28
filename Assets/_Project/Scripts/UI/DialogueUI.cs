using System;
using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private bool useStableDialogueLayout;

    [Header("Typing")]
    [SerializeField] private float charactersPerSecond = 40f;

    [Header("Audio")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Control Lock")]
    [SerializeField] private PlayerControlLocker playerControlLocker;

    public bool IsDialogueOpen { get; private set; }
    public DialogueData CurrentDialogue => currentDialogue;
    public int CurrentLineIndex => currentLineIndex;
    public bool IsTypingCurrentLine => isTyping;
    public bool IsAdvanceInputLocked => advanceInputLockCount > 0;

    private DialogueData currentDialogue;
    private int currentLineIndex;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool autoAdvance;
    private bool allowManualAdvance;
    private float autoAdvanceHold;
    private bool settingsPaused;
    private bool voicePausedBySettings;
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
    private int advanceInputLockCount;
    private CanvasGroup transientCenterMessageGroup;
    private TMP_Text transientCenterMessageText;
    private Coroutine transientCenterMessageRoutine;

    public event Action<DialogueData> DialogueStarted;
    public event Action<DialogueData> DialogueEnded;
    internal event Action ContinuePressedWithoutDialogue;

    internal void UsePlayerControlLocker(PlayerControlLocker locker) => playerControlLocker = locker;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureVoiceSource();

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

        // Ignore the toggle frame too, regardless of component Update ordering.
        if (settingsPaused || AudioSettingsOverlay.IsOpen || Input.GetKeyDown(KeyCode.Escape))
            return;

        bool pressedContinue = Input.GetKeyDown(KeyCode.E);
        if (!IsDialogueOpen)
        {
            if (pressedContinue) ContinuePressedWithoutDialogue?.Invoke();
            return;
        }

        if (advanceInputLockCount > 0)
            return;

        UpdateDialogueAdvance(pressedContinue);
    }

    private void UpdateDialogueAdvance(bool pressedContinue)
    {
        if (pressedContinue && (!autoAdvance || allowManualAdvance))
        {
            if (isTyping)
                CompleteCurrentLineInstantly();
            else
                AdvanceDialogue();
            return;
        }

        if (!autoAdvance || isTyping || (voiceSource != null && voiceSource.isPlaying)) return;
        autoAdvanceHold -= Time.unscaledDeltaTime;
        if (autoAdvanceHold <= 0f) AdvanceDialogue();
    }

    public bool IsPromptVisible => promptGroup != null && promptGroup.alpha > 0.001f;

#if UNITY_EDITOR // DEBUG-ONLY: remove before release; see docs/debug-skips.md
    internal string DebugDescribePrompt() =>
        $"owner={promptOwner?.name ?? "none"}, text=\"{currentPromptMessage}\", " +
        $"active={promptGroup != null && promptGroup.gameObject.activeInHierarchy}, alpha={(promptGroup != null ? promptGroup.alpha : 0f):F2}";
#endif

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
        autoAdvance = request.AutoAdvance;
        allowManualAdvance = request.AllowManualAdvance;
        IsDialogueOpen = true;

        TryAcquirePlayerControlLock(request.LockPlayerControls);

        SetPromptVisible(false);
        SetDialogueVisible(true);

        DialogueStarted?.Invoke(currentDialogue);
        ShowLine(currentDialogue.lines[currentLineIndex]);
        return true;
    }

    /// <summary>Suspends text reveal and the current voice without restarting the line.</summary>
    public void SetSettingsPaused(bool paused)
    {
        settingsPaused = paused;
        if (paused)
        {
            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.Pause();
                voicePausedBySettings = true;
            }
        }
        else if (voicePausedBySettings)
        {
            if (voiceSource != null) voiceSource.UnPause();
            voicePausedBySettings = false;
        }
    }

    public void AcquireAdvanceInputLock()
    {
        advanceInputLockCount++;
    }

    public void ReleaseAdvanceInputLock()
    {
        if (advanceInputLockCount > 0)
            advanceInputLockCount--;
    }

    public void ShowTemporaryCenterMessage(string message, float visibleDuration = 2.5f, float fadeDuration = 0.35f)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!EnsureTransientCenterMessageUi())
            return;

        if (transientCenterMessageRoutine != null)
            StopCoroutine(transientCenterMessageRoutine);

        transientCenterMessageText.text = message;
        transientCenterMessageGroup.alpha = 1f;
        transientCenterMessageGroup.interactable = false;
        transientCenterMessageGroup.blocksRaycasts = false;
        transientCenterMessageRoutine = StartCoroutine(RunTransientCenterMessage(visibleDuration, fadeDuration));
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

    private IEnumerator RunTransientCenterMessage(float visibleDuration, float fadeDuration)
    {
        if (visibleDuration > 0f)
            yield return new WaitForSecondsRealtime(visibleDuration);

        if (fadeDuration <= 0f)
        {
            transientCenterMessageGroup.alpha = 0f;
            transientCenterMessageText.text = string.Empty;
            transientCenterMessageRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            transientCenterMessageGroup.alpha = 1f - t;
            yield return null;
        }

        transientCenterMessageGroup.alpha = 0f;
        transientCenterMessageText.text = string.Empty;
        transientCenterMessageRoutine = null;
    }

    private void ShowLine(DialogueLine line)
    {
        autoAdvanceHold = Mathf.Max(2.6f, line.minimumReadHold);
        if (speakerNameText != null)
            speakerNameText.text = line.speakerName;

        ApplySide(line.side);

        AudioSource activeVoiceSource = EnsureVoiceSource();

        if (activeVoiceSource != null)
        {
            activeVoiceSource.Stop();
            voicePausedBySettings = false;

            if (line.voiceClip != null)
                activeVoiceSource.PlayOneShot(line.voiceClip);
        }

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        SetSettingsPaused(AudioSettingsOverlay.IsOpen);
        typingCoroutine = StartCoroutine(TypeLine(line));
    }

    private IEnumerator TypeLine(DialogueLine line)
    {
        isTyping = true;
        fullCurrentText = line.text ?? string.Empty;
        int characterCount = fullCurrentText.Length;
        if (useStableDialogueLayout)
        {
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.text = fullCurrentText;
            dialogueText.ForceMeshUpdate();
            characterCount = dialogueText.textInfo.characterCount;
        }
        else dialogueText.text = "";

        var pauses = useStableDialogueLayout ? FindLinePauses(line.pauses, fullCurrentText) : null;
        float delay = charactersPerSecond <= 0f ? 0f : 1f / charactersPerSecond;

        for (int i = 0; i < characterCount; i++)
        {
            while (settingsPaused || AudioSettingsOverlay.IsOpen)
                yield return null;

            if (useStableDialogueLayout) dialogueText.maxVisibleCharacters = i + 1;
            else dialogueText.text += fullCurrentText[i];
            float remainingDelay = delay;
            if (pauses != null && i + 1 < characterCount)
            {
                TMP_CharacterInfo character = dialogueText.textInfo.characterInfo[i];
                if (pauses.TryGetValue(character.index + character.stringLength, out float pause))
                    remainingDelay += pause;
            }
            do
            {
                yield return null;
                if (!settingsPaused && !AudioSettingsOverlay.IsOpen)
                    remainingDelay -= Time.deltaTime;
            }
            while (remainingDelay > 0f || settingsPaused || AudioSettingsOverlay.IsOpen);
        }

        RevealFullLine();
        isTyping = false;
        typingCoroutine = null;
    }

    private static Dictionary<int, float> FindLinePauses(DialoguePause[] pauses, string text)
    {
        if (pauses == null || pauses.Length == 0) return null;
        var result = new Dictionary<int, float>();
        foreach (DialoguePause pause in pauses)
        {
            if (pause == null || string.IsNullOrEmpty(pause.afterText) || pause.seconds <= 0f) continue;
            int start = text.IndexOf(pause.afterText, StringComparison.Ordinal);
            if (start >= 0) result[start + pause.afterText.Length] = pause.seconds;
        }
        return result;
    }

    private void CompleteCurrentLineInstantly()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        RevealFullLine();
        isTyping = false;
        typingCoroutine = null;
    }

    private void RevealFullLine()
    {
        if (useStableDialogueLayout) dialogueText.maxVisibleCharacters = int.MaxValue;
        else dialogueText.text = fullCurrentText;
    }

#if UNITY_EDITOR // DEBUG-ONLY: remove before release; see docs/debug-skips.md
    /// <summary>Completes this conversation through its normal callback and control-release path.</summary>
    public bool DebugFinishConversation()
    {
        if (!IsDialogueOpen || IsAdvanceInputLocked || AudioSettingsOverlay.IsOpen) return false;
        EndDialogue();
        return true;
    }
#endif

    private void EndDialogue()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        EnsureVoiceSource()?.Stop();
        voicePausedBySettings = false;

        DialogueData finishedDialogue = currentDialogue;
        currentDialogue = null;
        currentLineIndex = 0;
        isTyping = false;
        fullCurrentText = string.Empty;
        IsDialogueOpen = false;
        advanceInputLockCount = 0;

        SetDialogueVisible(false);
        ReleasePlayerControlLock();

        Action callback = onDialogueFinished;
        onDialogueFinished = null;
        callback?.Invoke();
        DialogueEnded?.Invoke(finishedDialogue);
    }

    private void ApplySide(DialogueSpeakerSide side)
    {
        TextAlignmentOptions alignment = useStableDialogueLayout ? TextAlignmentOptions.TopLeft :
            side == DialogueSpeakerSide.Left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;

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
            continueHintText.text = visible && (!autoAdvance || allowManualAdvance) ? "E - Continue" : "";
    }

    private bool EnsureTransientCenterMessageUi()
    {
        if (transientCenterMessageGroup != null && transientCenterMessageText != null)
            return true;

        Canvas targetCanvas = null;

        if (dialogueGroup != null)
            targetCanvas = dialogueGroup.GetComponentInParent<Canvas>();

        if (targetCanvas == null && promptGroup != null)
            targetCanvas = promptGroup.GetComponentInParent<Canvas>();

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (targetCanvas == null)
            return false;

        GameObject containerObject = new GameObject("TransientCenterMessage", typeof(RectTransform), typeof(CanvasGroup));
        containerObject.transform.SetParent(targetCanvas.transform, false);

        RectTransform containerTransform = (RectTransform)containerObject.transform;
        containerTransform.anchorMin = Vector2.zero;
        containerTransform.anchorMax = Vector2.one;
        containerTransform.offsetMin = Vector2.zero;
        containerTransform.offsetMax = Vector2.zero;

        transientCenterMessageGroup = containerObject.GetComponent<CanvasGroup>();
        transientCenterMessageGroup.alpha = 0f;
        transientCenterMessageGroup.interactable = false;
        transientCenterMessageGroup.blocksRaycasts = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(containerObject.transform, false);

        RectTransform textTransform = (RectTransform)textObject.transform;
        textTransform.anchorMin = new Vector2(0.5f, 0.5f);
        textTransform.anchorMax = new Vector2(0.5f, 0.5f);
        textTransform.pivot = new Vector2(0.5f, 0.5f);
        textTransform.anchoredPosition = Vector2.zero;
        textTransform.sizeDelta = new Vector2(900f, 200f);

        TextMeshProUGUI messageText = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = promptText != null
            ? promptText.font
            : dialogueText != null
                ? dialogueText.font
                : TMP_Settings.defaultFontAsset;

        if (fontAsset != null)
            messageText.font = fontAsset;

        messageText.alignment = TextAlignmentOptions.Center;
        messageText.enableWordWrapping = false;
        messageText.fontSize = 84f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.color = Color.white;
        messageText.alpha = 1f;
        messageText.text = string.Empty;

        transientCenterMessageText = messageText;
        return true;
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

    private AudioSource EnsureVoiceSource()
    {
        if (voiceSource != null)
        {
            AudioSettingsOverlay.RegisterSfxSource(voiceSource);
            return voiceSource;
        }

        voiceSource = GetComponent<AudioSource>();

        if (voiceSource == null)
            voiceSource = GetComponentInChildren<AudioSource>(true);

        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        AudioSettingsOverlay.RegisterSfxSource(voiceSource);

        return voiceSource;
    }
}
