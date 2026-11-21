using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Handles the father's beer pickup and its optional fridge door animation.</summary>
public sealed class FatherFridgeInteractable : MonoBehaviour
{
    [Header("Story ownership")]
    [SerializeField] private StoryId story = StoryId.Father;
    [SerializeField] private StoryPlayerRig playerRig;
    [Tooltip("Maximum distance from the camera to the fridge collider along the view ray.")]
    [SerializeField, Min(0.1f)] private float interactionReach = 1.5f;
    [SerializeField] private string prompt = "E - Take a beer";
    [SerializeField] private string feedback = "Beer taken";
    [SerializeField] private string emptyPrompt = "Out of beer";
    [Tooltip("Each active child is a bottle the player can take.")]
    [SerializeField] private Transform beerBottles;
    [Tooltip("Invoked when the bottle is taken, after the door opens if one is assigned.")]
    [SerializeField] private UnityEvent onBeerTaken = new UnityEvent();
    [Tooltip("Lower fridge door. Its local Z axis is the hinge axis.")]
    [SerializeField] private Transform fridgeDoor;
    [Tooltip("Optional trigger on the lower door used for aiming without blocking the player.")]
    [SerializeField] private Collider doorPickupTarget;
    [SerializeField] private float doorOpenAngle = 85f;
    public bool HasTakenBeer { get; private set; }
    private float feedbackUntil;
    private Coroutine pickupSequence;
    private Quaternion doorClosedRotation;

    private bool CanInteract(DialogueUI ui)
    {
        return isActiveAndEnabled && StoryAccess.Allows(this, story) && playerRig != null &&
            playerRig.CanInteract && ui != null && !ui.IsDialogueOpen;
    }

    private bool IsLookingAtFridge()
    {
        Camera view = playerRig.ActiveCamera;
        if (view == null) return false;
        var aim = new Ray(view.transform.position, view.transform.forward);
        bool hitSolid = Physics.Raycast(aim, out RaycastHit solid, interactionReach,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hitSolid && (solid.transform == transform || solid.transform.IsChildOf(transform)))
            return true;

        // Check only the door trigger, up to the nearest solid obstruction.
        return doorPickupTarget != null && doorPickupTarget.enabled && doorPickupTarget.gameObject.activeInHierarchy &&
            doorPickupTarget.Raycast(aim, out _, hitSolid ? solid.distance : interactionReach);
    }

    private void LateUpdate()
    {
        DialogueUI ui = DialogueUI.Instance;
        if (ui == null) return;
        if (!CanInteract(ui))
        {
            ui.SetPromptVisible(this, false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.Escape))
            TryTakeBeer();

        bool lookingAtIdleFridge = pickupSequence == null && IsLookingAtFridge();
        bool canTakeAnother = lookingAtIdleFridge && HasBeerInStock();
        bool showFeedback = Time.unscaledTime < feedbackUntil;
        if (canTakeAnother)
            ui.SetPromptVisible(this, true, prompt);
        else if (showFeedback)
            ui.SetPromptVisible(this, true, feedback);
        else
            ui.SetPromptVisible(this, lookingAtIdleFridge, emptyPrompt);
    }

    private Transform FindClosestAvailableBottle()
    {
        if (beerBottles == null) return null;

        Vector3 playerPosition = playerRig.transform.position;
        Transform closestBottle = null;
        float closestDistanceSquared = float.PositiveInfinity;
        for (int i = 0; i < beerBottles.childCount; i++)
        {
            Transform bottle = beerBottles.GetChild(i);
            if (!bottle.gameObject.activeInHierarchy) continue;

            float distanceSquared = (bottle.position - playerPosition).sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared) continue;

            closestBottle = bottle;
            closestDistanceSquared = distanceSquared;
        }
        return closestBottle;
    }

    private bool HasBeerInStock() => beerBottles == null ? !HasTakenBeer : FindClosestAvailableBottle() != null;

    /// <summary>Starts a pickup if the player can reach the fridge and it has beer in stock.</summary>
    public bool TryTakeBeer()
    {
        if (pickupSequence != null || !CanInteract(DialogueUI.Instance) || !HasBeerInStock() || !IsLookingAtFridge())
            return false;

        feedbackUntil = 0f;
        if (fridgeDoor == null)
            TakeBottle();
        else
            pickupSequence = StartCoroutine(OpenAndTakeBeer());

        return true;
    }

    private IEnumerator OpenAndTakeBeer()
    {
        doorClosedRotation = fridgeDoor.localRotation;
        Quaternion openRotation = doorClosedRotation * Quaternion.AngleAxis(doorOpenAngle, Vector3.forward);
        yield return AnimateDoor(doorClosedRotation, openRotation, 0.55f);
        yield return new WaitForSeconds(0.3f);
        TakeBottle();
        yield return new WaitForSeconds(0.4f);
        yield return AnimateDoor(openRotation, doorClosedRotation, 0.6f);
        pickupSequence = null;
    }

    private IEnumerator AnimateDoor(Quaternion from, Quaternion to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float blend = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            fridgeDoor.localRotation = Quaternion.Slerp(from, to, blend);
            yield return null;
        }
        fridgeDoor.localRotation = to;
    }

    private void TakeBottle()
    {
        if (beerBottles != null)
        {
            Transform bottle = FindClosestAvailableBottle();
            if (bottle == null) return;
            bottle.gameObject.SetActive(false);
        }
        HasTakenBeer = true;
        feedbackUntil = Time.unscaledTime + 2.5f;
        onBeerTaken.Invoke();
    }

    private void OnDisable()
    {
        if (pickupSequence != null)
        {
            StopCoroutine(pickupSequence);
            pickupSequence = null;
            if (fridgeDoor != null) fridgeDoor.localRotation = doorClosedRotation;
        }
        if (DialogueUI.Instance != null) DialogueUI.Instance.SetPromptVisible(this, false);
    }
}
