using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";

    public void LoadGameScene()
    {
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
