using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    void Awake() { instance = this; }

    public void Win()
    {
        Debug.Log("LEVEL COMPLETE!");
        // Tu si môeš aktivova nejakı Win Panel
    }
}