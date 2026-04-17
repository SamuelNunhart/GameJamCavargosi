using UnityEngine;

public class PuzzleOpener : MonoBehaviour
{
    [SerializeField] private GameObject puzzleCanvas;
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;

    public void OpenPuzzle()
    {
        puzzleCanvas.SetActive(true);

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerLook != null) playerLook.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ClosePuzzle()
    {
        puzzleCanvas.SetActive(false);

        if (playerMovement != null) playerMovement.enabled = true;
        if (playerLook != null) playerLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (puzzleCanvas.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePuzzle();
        }
    }
}