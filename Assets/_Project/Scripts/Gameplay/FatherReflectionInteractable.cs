using UnityEngine;

/// <summary>A repeatable first interaction for the father's scene, using editable dialogue data.</summary>
public sealed class FatherReflectionInteractable : MonoBehaviour
{
    [Header("Story ownership")]
    [Tooltip("Which story may run this interaction. All is for shared world interactions.")]
    [SerializeField] private StoryId story = StoryId.Father;
    private bool CanRunStory => isActiveAndEnabled && StoryAccess.Allows(this, story);

    [SerializeField] private StoryPlayerRig playerRig;
    [SerializeField] private Transform focusPoint;
    [SerializeField] private Collider paintingSurface;
    [SerializeField] private DialogueData dialogue;
    [SerializeField] private float interactionDistance = 2.8f;
    [SerializeField] private string prompt = "E - Look at the painting";

    private int lastFinishedFrame = -1;

    private bool IsAvailable()
    {
        if (!CanRunStory) return false;
        DialogueUI ui = DialogueUI.Instance;
        if (ui == null || playerRig == null || !playerRig.CanInteract || ui.IsDialogueOpen ||
            Time.frameCount == lastFinishedFrame) return false;
        if (playerRig.UsesCenteredInteractions)
            return paintingSurface != null &&
                InteractionAim.TryCreate(playerRig, interactionDistance, out InteractionAim aim) &&
                aim.Sees(paintingSurface.transform);

        Camera view = playerRig.ActiveCamera;
        Vector3 target = focusPoint != null ? focusPoint.position : transform.position;
        Vector3 delta = view != null ? target - view.transform.position : Vector3.zero;
        bool available = view != null &&
            delta.sqrMagnitude < interactionDistance * interactionDistance &&
            Vector3.Dot(view.transform.forward, delta.normalized) > 0.5f;
        if (available && Physics.Raycast(view.transform.position, delta.normalized, out RaycastHit hit,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            available = hit.transform == transform || hit.transform.IsChildOf(transform);
        return available;
    }

    private void LateUpdate()
    {
        if (!CanRunStory) return;
        if (DialogueUI.Instance == null) return;
        DialogueUI.Instance.SetPromptVisible(this, IsAvailable(), prompt);
        if (Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.Escape)) TryInteract();
    }

    /// <summary>Starts the reflection only when the player can see and reach the painting.</summary>
    public bool TryInteract()
    {
        return IsAvailable() && DialogueUI.Instance.StartDialogue(dialogue, true,
            () => lastFinishedFrame = Time.frameCount);
    }

    private void OnDisable()
    {
        if (DialogueUI.Instance != null) DialogueUI.Instance.SetPromptVisible(this, false);
    }
}
