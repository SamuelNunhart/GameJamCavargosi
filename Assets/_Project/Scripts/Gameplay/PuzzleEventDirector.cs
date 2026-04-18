using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PuzzleEventDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PuzzleManagerUI puzzleManager;
    [SerializeField] private PuzzleOpener puzzleOpener;

    [Header("Solved Popup")]
    [SerializeField] private GameObject victoryRoot;
    [SerializeField] private TMP_Text victoryText;
    [SerializeField] private string victoryMessage = "Puzzle Complete";
    [SerializeField] private float solvedCloseDelay = 2.5f;

    [Header("Phone Event Panels")]
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
    private GameObject currentPhonePanel;

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
    }

    private void OnDisable()
    {
        if (puzzleManager != null)
        {
            puzzleManager.ProgressThresholdReached -= HandleProgressThresholdReached;
            puzzleManager.PuzzleSolved -= HandlePuzzleSolved;
        }
    }

    private void Update()
    {
        if (phoneEventActive && waitingForPhoneCompletion && Input.GetKeyDown(completePhoneEventKey))
        {
            CompleteCurrentPhoneEvent();
        }
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

    private IEnumerator RunPhoneEventRoutine()
    {
        phoneEventActive = true;
        waitingForPhoneCompletion = false;

        puzzleManager.SetInputLocked(true);
        puzzleOpener.SetPuzzleCanvasVisible(false);
        puzzleOpener.SetPhoneWorldVisible(true);

        yield return StartCoroutine(puzzleOpener.MoveToPhoneViewRoutine());

        currentPhonePanel = GetNextPhonePanel();

        if (currentPhonePanel != null)
            currentPhonePanel.SetActive(true);

        ShowPhonePrompt();

        waitingForPhoneCompletion = true;

        while (waitingForPhoneCompletion)
            yield return null;

        HidePhonePrompt();

        if (currentPhonePanel != null)
            currentPhonePanel.SetActive(false);

        currentPhonePanel = null;

        yield return StartCoroutine(puzzleOpener.MoveToPuzzleViewRoutine());

        puzzleOpener.SetPuzzleCanvasVisible(true);
        puzzleManager.SetInputLocked(false);

        phoneEventActive = false;
    }

    private IEnumerator HandleSolvedRoutine()
    {
        solvedHandled = true;

        puzzleManager.SetInputLocked(true);

        ShowVictory();

        yield return new WaitForSeconds(solvedCloseDelay);

        HideVictory();

        puzzleManager.SetInputLocked(false);
        puzzleOpener.ClosePuzzle();
    }

    private GameObject GetNextPhonePanel()
    {
        if (nextPhoneEventIndex < 0 || nextPhoneEventIndex >= phoneEventPanels.Count)
            return null;

        GameObject panel = phoneEventPanels[nextPhoneEventIndex];
        nextPhoneEventIndex++;
        return panel;
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
        foreach (GameObject panel in phoneEventPanels)
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }
}