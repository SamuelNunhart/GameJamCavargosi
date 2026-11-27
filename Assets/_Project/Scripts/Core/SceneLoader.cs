using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";

    public void LoadGameScene()
    {
        LoadStory(StoryId.Arthur);
    }

    internal void LoadStory(StoryId story)
    {
        ChapterSceneSetup.SelectNextStory(story);
        LoadScene(gameSceneName);
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneLoader was asked to load an empty scene name.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
