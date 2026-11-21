using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Navigation between the illustrated title menu and its story selection panel.</summary>
public sealed class StorySelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button arthurButton;
    [SerializeField] private Button fatherButton;
    [SerializeField] private Text fatherStatus;
    [SerializeField] private Button backButton;
    [SerializeField] private Button resetProgressButton;
    [SerializeField] private ChapterResetDialog resetDialog;
    [SerializeField] private SceneLoader sceneLoader;

    [SerializeField] private string fatherSceneName = "Demo Burzon";

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ShowMainMenu();
    }

    /// <summary>Open story selection and focus the available story for keyboard navigation.</summary>
    public void ShowStories()
    {
        RefreshFatherRow();
        selectionPanel.SetActive(true);
        playButton.gameObject.SetActive(false);
        Select(arthurButton);
    }

    /// <summary>Return to the title screen without loading or resetting a story.</summary>
    public void ShowMainMenu()
    {
        selectionPanel.SetActive(false);
        if (resetDialog != null && resetDialog.gameObject.activeSelf) resetDialog.Close();
        playButton.gameObject.SetActive(true);
        Select(playButton);
    }

    /// <summary>Start the existing story through the project's scene loader.</summary>
    public void PlayArthur()
    {
        sceneLoader.LoadGameScene();
    }

    /// <summary>Load the configured father prototype without changing Arthur's destination.</summary>
    public void PlayFather()
    {
        if (StoryChapterProgress.IsCompleted(StoryId.Father)) return;
        sceneLoader.LoadScene(fatherSceneName);
    }

    public void ShowResetProgress()
    {
        resetDialog.Open(RefreshFatherRow);
    }

    private void RefreshFatherRow()
    {
        bool completed = StoryChapterProgress.IsCompleted(StoryId.Father);
        fatherButton.interactable = !completed;
        fatherStatus.text = completed ? "Completed" : "Begin his story";

        Navigation arthurNavigation = arthurButton.navigation;
        arthurNavigation.selectOnUp = resetProgressButton;
        arthurNavigation.selectOnDown = completed ? backButton : fatherButton;
        arthurButton.navigation = arthurNavigation;

        Navigation backNavigation = backButton.navigation;
        backNavigation.selectOnUp = completed ? arthurButton : fatherButton;
        backButton.navigation = backNavigation;
    }

    private static void Select(Button button)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
