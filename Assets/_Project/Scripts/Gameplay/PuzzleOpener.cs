using UnityEngine;
using TMPro;

public class PuzzleOpener : MonoBehaviour
{
    [Header("Puzzle")]
    [SerializeField] private GameObject puzzleCanvas;

    [Header("Player Control")]
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;

    [Header("Prompt UI")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string promptMessage = "Press E to inspect puzzle";

    private bool playerInRange;
    private bool puzzleOpen;

    private void Start()
    {
        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(false);

        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void Update()
    {
        if (!puzzleOpen && playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            OpenPuzzle();
        }

        if (puzzleOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePuzzle();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;
        ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;

        if (!puzzleOpen)
            ShowPrompt(false);
    }

    private void ShowPrompt(bool show)
    {
        if (promptRoot != null)
            promptRoot.SetActive(show);

        if (show && promptText != null)
            promptText.text = promptMessage;
    }

    public void OpenPuzzle()
    {
        puzzleOpen = true;

        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(true);

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerLook != null)
            playerLook.enabled = false;

        ShowPrompt(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ClosePuzzle()
    {
        puzzleOpen = false;

        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(false);

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerLook != null)
            playerLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerInRange)
            ShowPrompt(true);
    }
}