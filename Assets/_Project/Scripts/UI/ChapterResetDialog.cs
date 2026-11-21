using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Lets the player choose which saved chapter progress to clear.</summary>
public sealed class ChapterResetDialog : MonoBehaviour
{
    [SerializeField] private CanvasGroup storySelection;
    [SerializeField] private Button opener;
    [SerializeField] private GameObject options;
    [SerializeField] private GameObject confirmation;
    [SerializeField] private Button[] chapterButtons;
    [SerializeField] private Text[] chapterStatuses;
    [SerializeField] private Button resetAllButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text confirmationMessage;

    private int pendingOption;
    private Action progressChanged;

    public void Open(Action onProgressChanged)
    {
        progressChanged = onProgressChanged;
        storySelection.interactable = false;
        storySelection.blocksRaycasts = false;
        gameObject.SetActive(true);
        ShowOptions(closeButton);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        storySelection.interactable = true;
        storySelection.blocksRaycasts = true;
        progressChanged = null;
        Select(opener);
    }

    public void RequestReset(int index)
    {
        int chapterCount = StoryChapterProgress.Chapters.Count;
        if (!options.activeInHierarchy || index < 0 || index > chapterCount) return;
        Button selected = index == chapterCount ? resetAllButton : chapterButtons[index];
        if (!selected.interactable) return;

        pendingOption = index;
        confirmationMessage.text = index == chapterCount
            ? "Reset progress for all chapters?"
            : $"Reset {StoryChapterProgress.Chapters[index]} progress?";
        options.SetActive(false);
        confirmation.SetActive(true);
        Select(cancelButton);
    }

    public void ConfirmReset()
    {
        if (!confirmation.activeInHierarchy) return;
        if (pendingOption == StoryChapterProgress.Chapters.Count) StoryChapterProgress.ResetAll();
        else StoryChapterProgress.Reset(StoryChapterProgress.Chapters[pendingOption]);
        progressChanged?.Invoke();
        ShowOptions(closeButton);
    }

    public void CancelReset()
    {
        ShowOptions(pendingOption == StoryChapterProgress.Chapters.Count ? resetAllButton : chapterButtons[pendingOption]);
    }

    private void ShowOptions(Button focus)
    {
        bool anySaved = false;
        for (int i = 0; i < chapterButtons.Length; i++)
        {
            bool saved = StoryChapterProgress.IsCompleted(StoryChapterProgress.Chapters[i]);
            chapterButtons[i].interactable = saved;
            chapterStatuses[i].text = saved ? "Reset" : "No saved progress";
            anySaved |= saved;
        }
        resetAllButton.interactable = anySaved;
        confirmation.SetActive(false);
        options.SetActive(true);
        Select(focus);
    }

    private static void Select(Button button)
    {
        if (EventSystem.current != null && button.gameObject.activeInHierarchy) button.Select();
    }
}
