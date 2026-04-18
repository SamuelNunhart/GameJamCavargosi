using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NewPuzzleEventDirector : MonoBehaviour
{
    [Header("Interruption Handler")]
    [SerializeField] private PhoneInterruptionHandler phoneHandler;

    [System.Serializable]
    private class NewPhoneEventStep
    {
        public GameObject panel;
        public DialogueData dialogue;
        public bool lockPlayerControlsDuringDialogue;
    }

    [Header("References")]
    [SerializeField] private PuzzleManagerUI newPuzzleManager;
    [SerializeField] private PuzzleOpener newPuzzleOpener;

    [Header("Ghost Dialogue Beats")]
    [SerializeField] private DialogueData newPuzzleIntroDialogue;
    [SerializeField] private bool newLockPlayerControlsDuringPuzzleIntro;
    [SerializeField] private DialogueData newPostPuzzleDialogue;
    [SerializeField] private bool newLockPlayerControlsDuringPostPuzzleDialogue;

    [Header("Solved Popup")]
    [SerializeField] private GameObject newVictoryRoot;
    [SerializeField] private TMP_Text newVictoryText;
    [SerializeField] private string newVictoryMessage = "Puzzle Complete";
    [SerializeField] private float newSolvedCloseDelay = 2.5f;

    [Header("Phone Event Panels")]
    [SerializeField] private List<NewPhoneEventStep> newPhoneEvents = new List<NewPhoneEventStep>();
    [SerializeField] private List<GameObject> newPhoneEventPanels = new List<GameObject>();

    [Header("Temporary Phone Prompt")]
    [SerializeField] private GameObject newPhonePromptRoot;
    [SerializeField] private TMP_Text newPhonePromptText;
    [SerializeField] private string newPhonePromptMessage = "";
    [SerializeField] private KeyCode newCompletePhoneEventKey = KeyCode.F;

    private int newNextPhoneEventIndex;
    private int current3DInterruptionIndex = 0;
    private bool newPhoneEventActive;
    private bool newWaitingForPhoneCompletion;
    private bool newSolvedHandled;
    private bool newPuzzleIntroPlayed;
    private NewPhoneEventStep newCurrentPhoneEvent;

    public void UpdatePromptMessage(string newMessage)
    {
        newPhonePromptMessage = newMessage;
        
        // If the prompt is currently visible, update the text on screen immediately
        if (newPhonePromptRoot != null && newPhonePromptRoot.activeSelf)
        {
            newPhonePromptText.text = newPhonePromptMessage;
        }
    }

    private void Awake()
    {
        HideVictory();
        HideAllPhonePanels();
        HidePhonePrompt();
    }

    private void OnEnable()
    {
        if (newPuzzleManager != null)
        {
            newPuzzleManager.ProgressThresholdReached += HandleProgressThresholdReached;
            newPuzzleManager.PuzzleSolved += HandlePuzzleSolved;
        }

        if (newPuzzleOpener != null)
            newPuzzleOpener.PuzzleOpened += HandlePuzzleOpened;
    }

    private void OnDisable()
    {
        if (newPuzzleManager != null)
        {
            newPuzzleManager.ProgressThresholdReached -= HandleProgressThresholdReached;
            newPuzzleManager.PuzzleSolved -= HandlePuzzleSolved;
        }

        if (newPuzzleOpener != null)
            newPuzzleOpener.PuzzleOpened -= HandlePuzzleOpened;
    }

    private void Update()
    {
        if (newPhoneEventActive && newWaitingForPhoneCompletion && Input.GetKeyDown(newCompletePhoneEventKey))
        {
            CompleteCurrentPhoneEvent();
        }
    }

    private void HandlePuzzleOpened()
    {
        if (newPuzzleIntroPlayed || newPuzzleIntroDialogue == null)
            return;

        StartCoroutine(RunPuzzleIntroRoutine());
    }

    private void HandleProgressThresholdReached(int placedCount)
    {
        if (newPhoneEventActive || newSolvedHandled)
            return;

        StartCoroutine(RunPhoneEventRoutine());
    }

    private void HandlePuzzleSolved()
    {
        if (newSolvedHandled)
            return;

        StartCoroutine(HandleSolvedRoutine());
    }

    private IEnumerator RunPuzzleIntroRoutine()
    {
        newPuzzleIntroPlayed = true;

        if (newPuzzleManager != null)
            newPuzzleManager.SetInputLocked(true);

        yield return PlayDialogueIfAssigned(newPuzzleIntroDialogue, newLockPlayerControlsDuringPuzzleIntro);

        if (newPuzzleManager != null && !newPhoneEventActive && !newSolvedHandled)
            newPuzzleManager.SetInputLocked(false);
    }

    private IEnumerator RunPhoneEventRoutine()
    {
        newPhoneEventActive = true;
        newWaitingForPhoneCompletion = false;

        newPuzzleManager.SetInputLocked(true);
        newPuzzleOpener.SetPuzzleCanvasVisible(false);
        newPuzzleOpener.SetPhoneWorldVisible(true);

        yield return StartCoroutine(newPuzzleOpener.MoveToPhoneViewRoutine());

        // --- NEW LOGIC START ---
        if (phoneHandler != null)
        {
            // Trigger the current scenario
            phoneHandler.TriggerInterruption(current3DInterruptionIndex);
            
            // Increase the counter so the next time this runs, it plays the next scenario!
            current3DInterruptionIndex++; 
        }
        // --- NEW LOGIC END ---

        newCurrentPhoneEvent = GetNextPhoneEvent();

        if (newCurrentPhoneEvent != null && newCurrentPhoneEvent.panel != null)
            newCurrentPhoneEvent.panel.SetActive(true);

        yield return PlayDialogueIfAssigned(
            newCurrentPhoneEvent != null ? newCurrentPhoneEvent.dialogue : null,
            newCurrentPhoneEvent != null && newCurrentPhoneEvent.lockPlayerControlsDuringDialogue
        );

        ShowPhonePrompt();

        newWaitingForPhoneCompletion = true;

        while (newWaitingForPhoneCompletion)
            yield return null;

        HidePhonePrompt();

        if (newCurrentPhoneEvent != null && newCurrentPhoneEvent.panel != null)
            newCurrentPhoneEvent.panel.SetActive(false);

        newCurrentPhoneEvent = null;

        yield return StartCoroutine(newPuzzleOpener.MoveToPuzzleViewRoutine());

        newPuzzleOpener.SetPuzzleCanvasVisible(true);
        newPuzzleManager.SetInputLocked(false);

        newPhoneEventActive = false;
    }

    private IEnumerator HandleSolvedRoutine()
    {
        newSolvedHandled = true;

        newPuzzleManager.SetInputLocked(true);

        bool hasVictoryUI = newVictoryRoot != null || newVictoryText != null;

        if (hasVictoryUI)
        {
            ShowVictory();

            if (newSolvedCloseDelay > 0f)
                yield return new WaitForSeconds(newSolvedCloseDelay);

            HideVictory();
        }

        yield return PlayDialogueIfAssigned(newPostPuzzleDialogue, newLockPlayerControlsDuringPostPuzzleDialogue);

        newPuzzleManager.SetInputLocked(false);
        newPuzzleOpener.ClosePuzzle();
    }

    private NewPhoneEventStep GetNextPhoneEvent()
    {
        if (newPhoneEvents.Count > 0)
        {
            if (newNextPhoneEventIndex < 0 || newNextPhoneEventIndex >= newPhoneEvents.Count)
                return null;

            NewPhoneEventStep configuredEvent = newPhoneEvents[newNextPhoneEventIndex];
            newNextPhoneEventIndex++;
            return configuredEvent;
        }

        if (newNextPhoneEventIndex < 0 || newNextPhoneEventIndex >= newPhoneEventPanels.Count)
            return null;

        GameObject legacyPanel = newPhoneEventPanels[newNextPhoneEventIndex];
        newNextPhoneEventIndex++;

        return new NewPhoneEventStep
        {
            panel = legacyPanel,
            dialogue = null,
            lockPlayerControlsDuringDialogue = false
        };
    }

    public void CompleteCurrentPhoneEvent()
    {
        if (!newPhoneEventActive)
            return;

        newWaitingForPhoneCompletion = false;
    }

    private void ShowVictory()
    {
        if (newVictoryText != null)
            newVictoryText.text = newVictoryMessage;

        if (newVictoryRoot != null)
            newVictoryRoot.SetActive(true);
    }

    private void HideVictory()
    {
        if (newVictoryRoot != null)
            newVictoryRoot.SetActive(false);
    }

    private void ShowPhonePrompt()
    {
        if (newPhonePromptText != null)
            newPhonePromptText.text = newPhonePromptMessage;

        if (newPhonePromptRoot != null)
            newPhonePromptRoot.SetActive(true);
    }

    private void HidePhonePrompt()
    {
        if (newPhonePromptRoot != null)
            newPhonePromptRoot.SetActive(false);
    }

    private void HideAllPhonePanels()
    {
        foreach (NewPhoneEventStep phoneEvent in newPhoneEvents)
        {
            if (phoneEvent != null && phoneEvent.panel != null)
                phoneEvent.panel.SetActive(false);
        }

        foreach (GameObject legacyPanel in newPhoneEventPanels)
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