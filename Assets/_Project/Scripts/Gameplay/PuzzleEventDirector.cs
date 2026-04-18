using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PuzzleEventDirector : MonoBehaviour
{
    [System.Serializable]
    private class PhoneEventStep
    {
        public GameObject panel;
        public DialogueData dialogue;
        public bool lockPlayerControlsDuringDialogue;
    }

    [Header("References")]
    [SerializeField] private PuzzleManagerUI puzzleManager;
    [SerializeField] private PuzzleOpener puzzleOpener;

    [Header("Ghost Dialogue Beats")]
    [SerializeField] private DialogueData puzzleIntroDialogue;
    [SerializeField] private bool lockPlayerControlsDuringPuzzleIntro;
    [SerializeField] private DialogueData postPuzzleDialogue;
    [SerializeField] private bool lockPlayerControlsDuringPostPuzzleDialogue;

    [Header("Solved Popup")]
    [SerializeField] private GameObject victoryRoot;
    [SerializeField] private TMP_Text victoryText;
    [SerializeField] private string victoryMessage = "Puzzle Complete";
    [SerializeField] private float solvedCloseDelay = 2.5f;

    [Header("Phone Event Panels")]
    [SerializeField] private List<PhoneEventStep> phoneEvents = new List<PhoneEventStep>();
    [SerializeField] private List<GameObject> phoneEventPanels = new List<GameObject>();

    [Header("Temporary Phone Prompt")]
    [SerializeField] private GameObject phonePromptRoot;
    [SerializeField] private TMP_Text phonePromptText;
    [SerializeField] private string phonePromptMessage = "Press F to put the phone down";
    [SerializeField] private KeyCode completePhoneEventKey = KeyCode.F;

    private int nextPhoneEventIndex;
    private bool phoneEventActive;
    private bool waitingForPhoneCompletion;
    private bool solvedHandled;
    private bool puzzleIntroPlayed;
    private bool postPuzzleFlowCompleted;
    private PhoneEventStep currentPhoneEvent;

    public bool HasCompletedPostPuzzleFlow => postPuzzleFlowCompleted;

    public event System.Action PostPuzzleSequenceCompleted;

    private void Awake()
    {
        HideVictory();
        HideAllPhonePanels();
        HidePhonePrompt();
    }

    private void OnEnable()
    {
        if (puzzleManager != null)
        {
            puzzleManager.ProgressThresholdReached += HandleProgressThresholdReached;
            puzzleManager.PuzzleSolved += HandlePuzzleSolved;
        }

        if (puzzleOpener != null)
            puzzleOpener.PuzzleOpened += HandlePuzzleOpened;
    }

    private void OnDisable()
    {
        if (puzzleManager != null)
        {
            puzzleManager.ProgressThresholdReached -= HandleProgressThresholdReached;
            puzzleManager.PuzzleSolved -= HandlePuzzleSolved;
        }

        if (puzzleOpener != null)
            puzzleOpener.PuzzleOpened -= HandlePuzzleOpened;
    }

    private void Update()
    {
        if (phoneEventActive && waitingForPhoneCompletion && Input.GetKeyDown(completePhoneEventKey))
        {
            CompleteCurrentPhoneEvent();
        }
    }

    private void HandlePuzzleOpened()
    {
        if (puzzleIntroPlayed || puzzleIntroDialogue == null)
            return;

        StartCoroutine(RunPuzzleIntroRoutine());
    }

    private void HandleProgressThresholdReached(int placedCount)
    {
        if (phoneEventActive || solvedHandled)
            return;

        StartCoroutine(RunPhoneEventRoutine());
    }

    private void HandlePuzzleSolved()
    {
        if (solvedHandled)
            return;

        StartCoroutine(HandleSolvedRoutine());
    }

    private IEnumerator RunPuzzleIntroRoutine()
    {
        puzzleIntroPlayed = true;

        if (puzzleManager != null)
            puzzleManager.SetInputLocked(true);

        yield return PlayDialogueIfAssigned(puzzleIntroDialogue, lockPlayerControlsDuringPuzzleIntro);

        if (puzzleManager != null && !phoneEventActive && !solvedHandled)
            puzzleManager.SetInputLocked(false);
    }

    private IEnumerator RunPhoneEventRoutine()
    {
        phoneEventActive = true;
        waitingForPhoneCompletion = false;

        puzzleManager.SetInputLocked(true);
        puzzleOpener.SetPuzzleCanvasVisible(false);
        puzzleOpener.SetPhoneWorldVisible(true);

        yield return StartCoroutine(puzzleOpener.MoveToPhoneViewRoutine());

        currentPhoneEvent = GetNextPhoneEvent();

        if (currentPhoneEvent != null && currentPhoneEvent.panel != null)
            currentPhoneEvent.panel.SetActive(true);

        yield return PlayDialogueIfAssigned(
            currentPhoneEvent != null ? currentPhoneEvent.dialogue : null,
            currentPhoneEvent != null && currentPhoneEvent.lockPlayerControlsDuringDialogue
        );

        ShowPhonePrompt();

        waitingForPhoneCompletion = true;

        while (waitingForPhoneCompletion)
            yield return null;

        HidePhonePrompt();

        if (currentPhoneEvent != null && currentPhoneEvent.panel != null)
            currentPhoneEvent.panel.SetActive(false);

        currentPhoneEvent = null;

        yield return StartCoroutine(puzzleOpener.MoveToPuzzleViewRoutine());

        puzzleOpener.SetPuzzleCanvasVisible(true);
        puzzleManager.SetInputLocked(false);

        phoneEventActive = false;
    }

    private IEnumerator HandleSolvedRoutine()
    {
        solvedHandled = true;

        puzzleManager.SetInputLocked(true);

        bool hasVictoryUI = victoryRoot != null || victoryText != null;

        if (hasVictoryUI)
        {
            ShowVictory();

            if (solvedCloseDelay > 0f)
                yield return new WaitForSeconds(solvedCloseDelay);

            HideVictory();
        }

        yield return PlayDialogueIfAssigned(postPuzzleDialogue, lockPlayerControlsDuringPostPuzzleDialogue);

        puzzleManager.SetInputLocked(false);

        if (puzzleOpener != null && puzzleOpener.IsPuzzleOpen)
        {
            bool puzzleClosed = false;

            void HandlePuzzleClosed()
            {
                puzzleClosed = true;
            }

            puzzleOpener.PuzzleClosed += HandlePuzzleClosed;
            puzzleOpener.ClosePuzzle();

            while (!puzzleClosed)
                yield return null;

            puzzleOpener.PuzzleClosed -= HandlePuzzleClosed;
        }
        else
        {
            puzzleOpener?.ClosePuzzle();
        }

        postPuzzleFlowCompleted = true;
        PostPuzzleSequenceCompleted?.Invoke();
    }

    private PhoneEventStep GetNextPhoneEvent()
    {
        if (phoneEvents.Count > 0)
        {
            if (nextPhoneEventIndex < 0 || nextPhoneEventIndex >= phoneEvents.Count)
                return null;

            PhoneEventStep configuredEvent = phoneEvents[nextPhoneEventIndex];
            nextPhoneEventIndex++;
            return configuredEvent;
        }

        if (nextPhoneEventIndex < 0 || nextPhoneEventIndex >= phoneEventPanels.Count)
            return null;

        GameObject legacyPanel = phoneEventPanels[nextPhoneEventIndex];
        nextPhoneEventIndex++;

        return new PhoneEventStep
        {
            panel = legacyPanel,
            dialogue = null,
            lockPlayerControlsDuringDialogue = false
        };
    }

    public void CompleteCurrentPhoneEvent()
    {
        if (!phoneEventActive)
            return;

        waitingForPhoneCompletion = false;
    }

    private void ShowVictory()
    {
        if (victoryText != null)
            victoryText.text = victoryMessage;

        if (victoryRoot != null)
            victoryRoot.SetActive(true);
    }

    private void HideVictory()
    {
        if (victoryRoot != null)
            victoryRoot.SetActive(false);
    }

    private void ShowPhonePrompt()
    {
        if (phonePromptText != null)
            phonePromptText.text = phonePromptMessage;

        if (phonePromptRoot != null)
            phonePromptRoot.SetActive(true);
    }

    private void HidePhonePrompt()
    {
        if (phonePromptRoot != null)
            phonePromptRoot.SetActive(false);
    }

    private void HideAllPhonePanels()
    {
        foreach (PhoneEventStep phoneEvent in phoneEvents)
        {
            if (phoneEvent != null && phoneEvent.panel != null)
                phoneEvent.panel.SetActive(false);
        }

        foreach (GameObject legacyPanel in phoneEventPanels)
        {
            if (legacyPanel != null)
                legacyPanel.SetActive(false);
        }
    }

    private IEnumerator PlayDialogueIfAssigned(DialogueData dialogue, bool lockPlayerControls)
    {
        if (dialogue == null || DialogueUI.Instance == null)
            yield break;

        bool finished = false;
        bool started = DialogueUI.Instance.StartDialogue(
            dialogue,
            lockPlayerControls,
            () => finished = true
        );

        if (!started)
            yield break;

        while (!finished)
            yield return null;
    }
}
