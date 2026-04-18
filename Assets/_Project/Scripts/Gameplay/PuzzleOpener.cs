using System.Collections;
using UnityEngine;
using TMPro;

public class PuzzleOpener : MonoBehaviour
{
    [Header("Puzzle")]
    [SerializeField] private GameObject puzzleCanvas;

    [Header("Player Control")]
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;

    [Header("View Transition")]
    [SerializeField] private Transform playerViewTransform;
    [SerializeField] private Transform puzzleViewPoint;
    [SerializeField] private Transform phoneViewPoint;
    [SerializeField] private float transitionDuration = 0.4f;

    [Header("World Objects")]
    [SerializeField] private GameObject phoneWorldObject;

    [Header("Prompt UI")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string promptMessage = "Press E to inspect puzzle";

    private bool playerInRange;
    private bool puzzleOpen;
    private bool isTransitioning;

    private Vector3 savedViewPosition;
    private Quaternion savedViewRotation;

    public bool IsPuzzleOpen => puzzleOpen;
    public bool IsTransitioning => isTransitioning;

    private void Start()
    {
        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(false);

        if (phoneWorldObject != null)
            phoneWorldObject.SetActive(false);

        if (DialogueUI.Instance == null)
            HideLegacyPrompt();
    }

    private void Update()
    {
        if (!puzzleOpen && !isTransitioning && playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            OpenPuzzle();
        }

        if (puzzleOpen && !isTransitioning && Input.GetKeyDown(KeyCode.Escape))
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
        if (DialogueUI.Instance != null)
        {
            if (show && !DialogueUI.Instance.IsDialogueOpen)
                DialogueUI.Instance.SetPromptVisible(true, promptMessage);
            else
                DialogueUI.Instance.SetPromptVisible(false);

            HideLegacyPrompt();
            return;
        }

        if (promptRoot != null)
            promptRoot.SetActive(show);

        if (show && promptText != null)
            promptText.text = promptMessage;
    }

    private void OnDisable()
    {
        ShowPrompt(false);
    }

    private void HideLegacyPrompt()
    {
        if (DialogueUI.Instance == null && promptRoot != null)
            promptRoot.SetActive(false);
    }

    public void OpenPuzzle()
    {
        if (puzzleOpen || isTransitioning)
            return;

        StartCoroutine(OpenPuzzleRoutine());
    }

    public void ClosePuzzle()
    {
        if (!puzzleOpen || isTransitioning)
            return;

        StartCoroutine(ClosePuzzleRoutine());
    }

    public void SetPuzzleCanvasVisible(bool visible)
    {
        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(visible);
    }

    public void SetPhoneWorldVisible(bool visible)
    {
        if (phoneWorldObject != null)
            phoneWorldObject.SetActive(visible);
    }

    public IEnumerator MoveToPhoneViewRoutine()
    {
        if (isTransitioning)
            yield break;

        if (playerViewTransform == null || phoneViewPoint == null || transitionDuration <= 0f)
            yield break;

        isTransitioning = true;

        yield return MoveView(
            playerViewTransform,
            playerViewTransform.position,
            playerViewTransform.rotation,
            phoneViewPoint.position,
            phoneViewPoint.rotation,
            transitionDuration
        );

        isTransitioning = false;
    }

    public IEnumerator MoveToPuzzleViewRoutine()
    {
        if (isTransitioning)
            yield break;

        if (playerViewTransform == null || puzzleViewPoint == null || transitionDuration <= 0f)
            yield break;

        isTransitioning = true;

        yield return MoveView(
            playerViewTransform,
            playerViewTransform.position,
            playerViewTransform.rotation,
            puzzleViewPoint.position,
            puzzleViewPoint.rotation,
            transitionDuration
        );

        isTransitioning = false;
    }

    private IEnumerator OpenPuzzleRoutine()
    {
        isTransitioning = true;
        puzzleOpen = true;

        ShowPrompt(false);

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerLook != null)
            playerLook.enabled = false;

        bool canMoveView = playerViewTransform != null && puzzleViewPoint != null && transitionDuration > 0f;

        if (canMoveView)
        {
            savedViewPosition = playerViewTransform.position;
            savedViewRotation = playerViewTransform.rotation;

            yield return MoveView(
                playerViewTransform,
                savedViewPosition,
                savedViewRotation,
                puzzleViewPoint.position,
                puzzleViewPoint.rotation,
                transitionDuration
            );
        }

        if (phoneWorldObject != null)
            phoneWorldObject.SetActive(true);

        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isTransitioning = false;
    }

    private IEnumerator ClosePuzzleRoutine()
    {
        isTransitioning = true;

        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(false);

        if (phoneWorldObject != null)
            phoneWorldObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        bool canMoveView = playerViewTransform != null && puzzleViewPoint != null && transitionDuration > 0f;

        if (canMoveView)
        {
            yield return MoveView(
                playerViewTransform,
                playerViewTransform.position,
                playerViewTransform.rotation,
                savedViewPosition,
                savedViewRotation,
                transitionDuration
            );
        }

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerLook != null)
            playerLook.enabled = true;

        puzzleOpen = false;
        isTransitioning = false;

        if (playerInRange)
            ShowPrompt(true);
    }

    private IEnumerator MoveView(
        Transform viewTransform,
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 endPosition,
        Quaternion endRotation,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            viewTransform.position = Vector3.Lerp(startPosition, endPosition, t);
            viewTransform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        viewTransform.position = endPosition;
        viewTransform.rotation = endRotation;
    }
}