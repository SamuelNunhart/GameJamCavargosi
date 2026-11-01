using UnityEngine;
using UnityEngine.Events;

/// <summary>Placeholder beer pickup. Records a choice without changing the fridge model or playing an animation.</summary>
public sealed class FatherFridgeInteractable : MonoBehaviour
{
    [Header("Story ownership")]
    [SerializeField] private StoryId story = StoryId.Father;
    [SerializeField] private StoryPlayerRig playerRig;
    [SerializeField] private Renderer fridgeRenderer;
    [SerializeField] private Transform focusPoint;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2.8f;
    [SerializeField] private string prompt = "E - Take a beer";
    [SerializeField] private string feedback = "Beer taken (prototype)";
    [Tooltip("Future animation, sound or story consequence hook. Invoked once per play session.")]
    [SerializeField] private UnityEvent onBeerTaken = new UnityEvent();
    public bool HasTakenBeer { get; private set; }
    private float feedbackUntil;

    private bool CanReach()
    {
        if (!isActiveAndEnabled || !StoryAccess.Allows(this, story) || playerRig == null ||
            !playerRig.CanInteract || DialogueUI.Instance == null || DialogueUI.Instance.IsDialogueOpen) return false;
        Camera view = playerRig.ActiveCamera;
        if (view == null) return false;
        Vector3 target = focusPoint != null ? focusPoint.position :
            fridgeRenderer != null ? fridgeRenderer.bounds.center : transform.position;
        Vector3 delta = target - view.transform.position;
        if (delta.sqrMagnitude > interactionDistance * interactionDistance ||
            Vector3.Dot(view.transform.forward, delta.normalized) < 0.5f) return false;
        if (Physics.Raycast(view.transform.position, delta.normalized, out RaycastHit hit,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        return true;
    }

    private void LateUpdate()
    {
        if (DialogueUI.Instance == null) return;
        bool available = CanReach();
        bool showFeedback = HasTakenBeer && Time.unscaledTime < feedbackUntil;
        DialogueUI.Instance.SetPromptVisible(this, available && (!HasTakenBeer || showFeedback),
            showFeedback ? feedback : prompt);
        if (Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.Escape)) TryTakeBeer();
    }

    /// <summary>Returns true only for the first successful pickup, while in reach and in the correct story.</summary>
    public bool TryTakeBeer()
    {
        if (HasTakenBeer || !CanReach()) return false;
        HasTakenBeer = true;
        feedbackUntil = Time.unscaledTime + 2.5f;
        onBeerTaken.Invoke();
        return true;
    }

    private void OnDisable()
    {
        if (DialogueUI.Instance != null) DialogueUI.Instance.SetPromptVisible(this, false);
    }
}
