using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Navigation between the illustrated title menu and its story selection panel.</summary>
public sealed class StorySelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button arthurButton;
    [SerializeField] private SceneLoader sceneLoader;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ShowMainMenu();
    }

    /// <summary>Open story selection and focus the available story for keyboard navigation.</summary>
    public void ShowStories()
    {
        selectionPanel.SetActive(true);
        playButton.gameObject.SetActive(false);
        Select(arthurButton);
    }

    /// <summary>Return to the title screen without loading or resetting a story.</summary>
    public void ShowMainMenu()
    {
        selectionPanel.SetActive(false);
        playButton.gameObject.SetActive(true);
        Select(playButton);
    }

    /// <summary>Start the existing story through the project's scene loader.</summary>
    public void PlayArthur()
    {
        sceneLoader.LoadGameScene();
    }

    private static void Select(Button button)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
