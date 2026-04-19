using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class PuzzleEventDirector : MonoBehaviour
{
    [Header("Interruption Handler")]
    [FormerlySerializedAs("newPhoneHandler")]
    [SerializeField] private PhoneInterruptionHandler phoneHandler;

    [System.Serializable]
    private class PhoneEventStep
    {
        public GameObject panel;
        public DialogueData dialogue;
        public bool lockPlayerControlsDuringDialogue;
    }

    [Header("References")]
    [FormerlySerializedAs("newPuzzleManager")]
    [SerializeField] private PuzzleManagerUI puzzleManager;
    [FormerlySerializedAs("newPuzzleOpener")]
    [SerializeField] private PuzzleOpener puzzleOpener;

    [Header("Ghost Dialogue Beats")]
    [FormerlySerializedAs("newPuzzleIntroDialogue")]
    [SerializeField] private DialogueData puzzleIntroDialogue;
    [FormerlySerializedAs("newLockPlayerControlsDuringPuzzleIntro")]
    [SerializeField] private bool lockPlayerControlsDuringPuzzleIntro;
    [FormerlySerializedAs("newPostPuzzleDialogue")]
    [SerializeField] private DialogueData postPuzzleDialogue;
    [FormerlySerializedAs("newLockPlayerControlsDuringPostPuzzleDialogue")]
    [SerializeField] private bool lockPlayerControlsDuringPostPuzzleDialogue;

    [Header("Solved Popup")]
    [FormerlySerializedAs("newVictoryRoot")]
    [SerializeField] private GameObject victoryRoot;
    [FormerlySerializedAs("newVictoryText")]
    [SerializeField] private TMP_Text victoryText;
    [FormerlySerializedAs("newVictoryMessage")]
    [SerializeField] private string victoryMessage = "Puzzle Complete";
    [FormerlySerializedAs("newSolvedCloseDelay")]
    [SerializeField] private float solvedCloseDelay = 2.5f;

    [Header("Phone Event Panels")]
    [FormerlySerializedAs("newPhoneEvents")]
    [SerializeField] private List<PhoneEventStep> phoneEvents = new List<PhoneEventStep>();
    [FormerlySerializedAs("newPhoneEventPanels")]
    [SerializeField] private List<GameObject> phoneEventPanels = new List<GameObject>();

    [Header("Temporary Phone Prompt")]
    [FormerlySerializedAs("newPhonePromptRoot")]
    [SerializeField] private GameObject phonePromptRoot;
    [FormerlySerializedAs("newPhonePromptText")]
    [SerializeField] private TMP_Text phonePromptText;
    [FormerlySerializedAs("newPhonePromptMessage")]
    [SerializeField] private string phonePromptMessage = "";
    [FormerlySerializedAs("newCompletePhoneEventKey")]
    [SerializeField] private KeyCode completePhoneEventKey = KeyCode.BackQuote;

    private int nextPhoneEventIndex;
    private int current3DInterruptionIndex;
    private bool phoneEventActive;
    private bool waitingForPhoneCompletion;
    private bool solvedHandled;
    private bool puzzleIntroPlayed;
    private bool postPuzzleFlowCompleted;
    private PhoneEventStep currentPhoneEvent;
    private Coroutine puzzleIntroRoutine;
    private event System.Action PuzzleIntroCompletedCallbacks;

    public bool HasCompletedPostPuzzleFlow => postPuzzleFlowCompleted;
    public bool HasPlayedPuzzleIntro => puzzleIntroPlayed;
    public bool HasPuzzleIntroDialogue => puzzleIntroDialogue != null;

    public event System.Action PostPuzzleSequenceCompleted;

    public void UpdatePromptMessage(string newMessage)
    {
        phonePromptMessage = newMessage;

        if (phonePromptRoot != null && phonePromptRoot.activeSelf && phonePromptText != null)
            phonePromptText.text = phonePromptMessage;
    }

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
            CompleteCurrentPhoneEvent();
    }

    private void HandlePuzzleOpened()
    {
        PlayPuzzleIntroIfAssigned();
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

    public void PlayPuzzleIntroIfAssigned(System.Action onCompleted = null)
    {
        if (onCompleted != null)
            PuzzleIntroCompletedCallbacks += onCompleted;

        if (puzzleIntroPlayed || puzzleIntroDialogue == null)
        {
            CompletePendingPuzzleIntroCallbacks();
            return;
        }

        if (puzzleIntroRoutine != null)
            return;

        puzzleIntroRoutine = StartCoroutine(RunPuzzleIntroRoutine());
    }

    private IEnumerator RunPuzzleIntroRoutine()
    {
        puzzleIntroPlayed = true;

        if (puzzleManager != null)
            puzzleManager.SetInputLocked(true);

        yield return PlayDialogueIfAssigned(puzzleIntroDialogue, lockPlayerControlsDuringPuzzleIntro);

        if (puzzleManager != null && !phoneEventActive && !solvedHandled)
            puzzleManager.SetInputLocked(false);

        puzzleIntroRoutine = null;
        CompletePendingPuzzleIntroCallbacks();
    }

    private IEnumerator RunPhoneEventRoutine()
    {
        phoneEventActive = true;
        waitingForPhoneCompletion = false;

        if (puzzleManager != null)
            puzzleManager.SetInputLocked(true);

        puzzleOpener?.SetPuzzleCanvasVisible(false);
        puzzleOpener?.SetPhoneWorldVisible(true);

        if (puzzleOpener != null)
            yield return StartCoroutine(puzzleOpener.MoveToPhoneViewRoutine());

        if (phoneHandler != null)
        {
            phoneHandler.TriggerInterruption(current3DInterruptionIndex);
            current3DInterruptionIndex++;
        }

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

        if (puzzleOpener != null)
            yield return StartCoroutine(puzzleOpener.MoveToPuzzleViewRoutine());

        puzzleOpener?.SetPuzzleCanvasVisible(true);

        if (puzzleManager != null)
            puzzleManager.SetInputLocked(false);

        phoneEventActive = false;
    }

    private IEnumerator HandleSolvedRoutine()
    {
        solvedHandled = true;

        if (puzzleManager != null)
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

        if (puzzleManager != null)
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

        while (DialogueUI.Instance != null && DialogueUI.Instance.IsDialogueOpen)
            yield return null;

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

    private void CompletePendingPuzzleIntroCallbacks()
    {
        System.Action callbacks = PuzzleIntroCompletedCallbacks;
        PuzzleIntroCompletedCallbacks = null;
        callbacks?.Invoke();
    }
}
