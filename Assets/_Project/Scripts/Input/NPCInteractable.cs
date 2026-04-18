using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NPCInteractable : MonoBehaviour
{
    [SerializeField] private DialogueData dialogue;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string promptMessage = "Press E";

    private bool playerInRange;
    private bool hasPlayed;
    private bool promptVisible;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        RefreshPrompt();

        if (!playerInRange || hasPlayed)
            return;

        if (DialogueUI.Instance == null)
            return;

        if (DialogueUI.Instance.IsDialogueOpen)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            DialogueUI.Instance.StartDialogue(dialogue, OnDialogueFinished);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInRange = true;
        RefreshPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInRange = false;
        RefreshPrompt();
    }

    private void OnDialogueFinished()
    {
        hasPlayed = true;
        RefreshPrompt();
    }

    private void OnDisable()
    {
        if (DialogueUI.Instance != null)
            DialogueUI.Instance.SetPromptVisible(this, false);

        promptVisible = false;
    }

    private void RefreshPrompt()
    {
        if (DialogueUI.Instance == null)
        {
            promptVisible = false;
            return;
        }

        bool shouldShowPrompt = playerInRange && !hasPlayed && !DialogueUI.Instance.IsDialogueOpen;
        bool promptNeedsRefresh = shouldShowPrompt && !DialogueUI.Instance.IsPromptActiveFor(this, promptMessage);

        if (promptVisible == shouldShowPrompt && !promptNeedsRefresh)
            return;

        DialogueUI.Instance.SetPromptVisible(this, shouldShowPrompt, promptMessage);
        promptVisible = shouldShowPrompt;
    }
}
