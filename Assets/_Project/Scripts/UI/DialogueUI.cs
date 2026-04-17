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

    public bool IsDialogueOpen { get; private set; }

    private DialogueData currentDialogue;
    private int currentLineIndex;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private string fullCurrentText;
    private Action onDialogueFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        SetPromptVisible(false);
        SetDialogueVisible(false);
    }

    private void Update()
    {
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

    public void SetPromptVisible(bool visible, string message = "Press E")
    {
        if (promptText != null)
            promptText.text = message;

        SetCanvasGroup(promptGroup, visible);
    }

    public void StartDialogue(DialogueData dialogueData, Action finishedCallback = null)
    {
        if (dialogueData == null || dialogueData.lines == null || dialogueData.lines.Length == 0)
        {
            Debug.LogWarning("No dialogue lines assigned.");
            return;
        }

        currentDialogue = dialogueData;
        currentLineIndex = 0;
        onDialogueFinished = finishedCallback;
        IsDialogueOpen = true;

        SetPromptVisible(false);
        SetDialogueVisible(true);

        ShowLine(currentDialogue.lines[currentLineIndex]);
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

        currentDialogue = null;
        currentLineIndex = 0;
        isTyping = false;
        fullCurrentText = string.Empty;
        IsDialogueOpen = false;

        SetDialogueVisible(false);

        Action callback = onDialogueFinished;
        onDialogueFinished = null;
        callback?.Invoke();
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
}