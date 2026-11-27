using System;
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
    public int BeersDrunk { get; private set; }
    public event Action<int> BeerDrunk;
    internal bool IsPickingUpBeer => pickupSequence != null;
    private float feedbackUntil;
    private Coroutine pickupSequence;
    private Quaternion doorClosedRotation;

#if UNITY_EDITOR // DEBUG-ONLY: remove before release; see docs/debug-skips.md
    private int openingBottlesTaken;

    internal void DebugFinishOpeningPickups(Vector3 pickupPosition, int expectedBottles)
    {
        CancelPickup();
        while (openingBottlesTaken < expectedBottles && TakeBottle(pickupPosition, out _)) openingBottlesTaken++;
    }
#endif

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

    private Transform FindClosestAvailableBottle(Vector3 pickupPosition)
    {
        if (beerBottles == null) return null;

        Transform closestBottle = null;
        float closestDistanceSquared = float.PositiveInfinity;
        for (int i = 0; i < beerBottles.childCount; i++)
        {
            Transform bottle = beerBottles.GetChild(i);
            if (!bottle.gameObject.activeInHierarchy) continue;

            float distanceSquared = (bottle.position - pickupPosition).sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared) continue;

            closestBottle = bottle;
            closestDistanceSquared = distanceSquared;
        }
        return closestBottle;
    }

    private bool HasBeerInStock() => beerBottles == null ? !HasTakenBeer : FindClosestAvailableBottle(playerRig.transform.position) != null;

    /// <summary>Starts a pickup if the player can reach the fridge and it has beer in stock.</summary>
    public bool TryTakeBeer()
    {
        if (pickupSequence != null || !CanInteract(DialogueUI.Instance) || !HasBeerInStock() || !IsLookingAtFridge())
            return false;

        StartPickup(playerRig.transform);
        return true;
    }

    /// <summary>Plays the bottle pickup, then lets the cutscene position the camera before drinking.</summary>
    internal Coroutine DrinkForOpening(Transform pickupOrigin, Func<IEnumerator> beforeDrink,
        Func<IEnumerator> takeShotProps, Func<Transform, IEnumerator> showBeer, Func<float> stepTime)
    {
        if (pickupSequence != null || (takeShotProps == null && !HasBeerInStock())) return pickupSequence;
        return StartPickup(pickupOrigin, beforeDrink, takeShotProps, showBeer, stepTime);
    }

    private Coroutine StartPickup(Transform pickupOrigin, Func<IEnumerator> beforeDrink = null,
        Func<IEnumerator> takeShotProps = null, Func<Transform, IEnumerator> showBeer = null, Func<float> stepTime = null)
    {
        feedbackUntil = 0f;
        if (fridgeDoor == null && beforeDrink == null)
        {
            if (TakeBottle(pickupOrigin.position, out _)) FinishDrink();
            return null;
        }

        pickupSequence = StartCoroutine(OpenAndTakeDrink(pickupOrigin, beforeDrink, takeShotProps, showBeer,
            stepTime ?? PickupStepTime));
        return pickupSequence;
    }

    private IEnumerator OpenAndTakeDrink(Transform pickupOrigin, Func<IEnumerator> beforeDrink,
        Func<IEnumerator> takeShotProps, Func<Transform, IEnumerator> showBeer, Func<float> stepTime)
    {
        Quaternion openRotation = Quaternion.identity;
        if (fridgeDoor != null)
        {
            doorClosedRotation = fridgeDoor.localRotation;
            openRotation = doorClosedRotation * Quaternion.AngleAxis(doorOpenAngle, Vector3.forward);
            yield return AnimateDoor(doorClosedRotation, openRotation, 0.55f, stepTime);
            yield return WaitForPickupBeat(0.3f, stepTime);
        }

        Transform bottle = null;
        bool tookBeer = takeShotProps == null && TakeBottle(pickupOrigin.position, out bottle);
        bool tookDrink = tookBeer || takeShotProps != null;
#if UNITY_EDITOR // DEBUG-ONLY: count bottles already taken before a skip
        if (tookBeer && beforeDrink != null) openingBottlesTaken++;
#endif
        if (takeShotProps != null) yield return takeShotProps();
        else if (tookBeer && showBeer != null && bottle != null) yield return showBeer(bottle);
        if (tookDrink)
        {
            yield return WaitForPickupBeat(beforeDrink == null ? .4f : .65f, stepTime);
            if (beforeDrink == null) FinishDrink();
        }

        if (fridgeDoor != null) yield return AnimateDoor(openRotation, doorClosedRotation, 0.6f, stepTime);

        if (tookDrink && beforeDrink != null)
        {
            yield return beforeDrink();
            FinishDrink();
        }

        pickupSequence = null;
    }

    private IEnumerator AnimateDoor(Quaternion from, Quaternion to, float duration, Func<float> stepTime)
    {
        for (float elapsed = 0f; elapsed < duration && stepTime() != float.MaxValue; elapsed += stepTime())
        {
            float blend = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            fridgeDoor.localRotation = Quaternion.Slerp(from, to, blend);
            yield return null;
        }
        fridgeDoor.localRotation = to;
    }

    private static float PickupStepTime() => AudioSettingsOverlay.IsOpen ? 0f : Time.deltaTime;

    private static IEnumerator WaitForPickupBeat(float seconds, Func<float> stepTime)
    {
        for (float elapsed = 0f; elapsed < seconds && stepTime() != float.MaxValue; elapsed += stepTime()) yield return null;
    }

    private bool TakeBottle(Vector3 pickupPosition, out Transform bottle)
    {
        bottle = null;
        if (beerBottles != null)
        {
            bottle = FindClosestAvailableBottle(pickupPosition);
            if (bottle == null) return false;
            bottle.gameObject.SetActive(false);
        }
        HasTakenBeer = true;
        onBeerTaken.Invoke();
        return true;
    }

    private void FinishDrink()
    {
        BeersDrunk++;
        feedbackUntil = Time.unscaledTime + 2.5f;
        BeerDrunk?.Invoke(BeersDrunk);
    }

    internal void SetDrunkenness(int drinkCount)
    {
        BeersDrunk = drinkCount;
        feedbackUntil = 0f;
    }

    private void CancelPickup()
    {
        if (pickupSequence != null)
        {
            StopCoroutine(pickupSequence);
            pickupSequence = null;
            if (fridgeDoor != null) fridgeDoor.localRotation = doorClosedRotation;
        }
    }

    private void OnDisable()
    {
        CancelPickup();
        if (DialogueUI.Instance != null) DialogueUI.Instance.SetPromptVisible(this, false);
    }
}
