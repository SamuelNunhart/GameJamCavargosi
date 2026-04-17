using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NPCInteractable : MonoBehaviour
{
    [SerializeField] private DialogueData dialogue;
    [SerializeField] private string playerTag = "Player";

    private bool playerInRange;
    private bool hasPlayed;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
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

        if (!hasPlayed && DialogueUI.Instance != null && !DialogueUI.Instance.IsDialogueOpen)
            DialogueUI.Instance.SetPromptVisible(true, "Press E");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInRange = false;

        if (DialogueUI.Instance != null)
            DialogueUI.Instance.SetPromptVisible(false);
    }

    private void OnDialogueFinished()
    {
        hasPlayed = true;

        if (DialogueUI.Instance != null)
            DialogueUI.Instance.SetPromptVisible(false);
    }
}