using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoorDialogueTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData dialogue;
    [SerializeField] private bool lockPlayerControlsDuringDialogue;

    [Header("Door")]
    [SerializeField] private Transform doorTransform;
    [SerializeField] private Vector3 openRotationEuler = new Vector3(0f, 0f, -90f);
    [SerializeField] private bool useLocalRotation = true;
    [SerializeField] private float openAngleTolerance = 1f;
    [SerializeField] private float startDelayAfterDoorOpens = 0.1f;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool interactionEnabled = true;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool rotationsCached;
    private bool playerInRange;
    private bool hasPlayed;
    private Coroutine waitForDoorRoutine;

    public bool HasPlayedDialogue => hasPlayed;
    public bool IsInteractionEnabled => interactionEnabled;

    public event Action DialogueCompleted;

    private void Reset()
    {
        ConfigureTrigger();

        if (doorTransform == null && transform.parent != null)
            doorTransform = transform.parent;

        CacheRotations();
    }

    private void OnValidate()
    {
        ConfigureTrigger();

        if (!Application.isPlaying)
        {
            if (doorTransform == null && transform.parent != null)
                doorTransform = transform.parent;

            CacheRotations();
        }
    }

    private void Awake()
    {
        ConfigureTrigger();

        if (doorTransform == null && transform.parent != null)
            doorTransform = transform.parent;

        CacheRotations();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = true;
        TryStartWaitingForDialogue();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = true;
        TryStartWaitingForDialogue();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = false;

        if (waitForDoorRoutine != null)
        {
            StopCoroutine(waitForDoorRoutine);
            waitForDoorRoutine = null;
        }
    }

    private void OnDisable()
    {
        playerInRange = false;

        if (waitForDoorRoutine != null)
        {
            StopCoroutine(waitForDoorRoutine);
            waitForDoorRoutine = null;
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (interactionEnabled)
        {
            if (playerInRange)
                TryStartWaitingForDialogue();

            return;
        }

        if (waitForDoorRoutine != null)
        {
            StopCoroutine(waitForDoorRoutine);
            waitForDoorRoutine = null;
        }
    }

    private void TryStartWaitingForDialogue()
    {
        if (!interactionEnabled || !playerInRange || hasPlayed || waitForDoorRoutine != null || dialogue == null)
            return;

        waitForDoorRoutine = StartCoroutine(WaitForDoorAndPlayDialogue());
    }

    private IEnumerator WaitForDoorAndPlayDialogue()
    {
        while (interactionEnabled && playerInRange && !hasPlayed)
        {
            if (DialogueUI.Instance == null || DialogueUI.Instance.IsDialogueOpen)
            {
                yield return null;
                continue;
            }

            if (!IsDoorOpenEnough())
            {
                yield return null;
                continue;
            }

            if (startDelayAfterDoorOpens > 0f)
                yield return new WaitForSeconds(startDelayAfterDoorOpens);

            if (!playerInRange || hasPlayed)
                break;

            if (DialogueUI.Instance == null || DialogueUI.Instance.IsDialogueOpen)
            {
                yield return null;
                continue;
            }

            bool started = DialogueUI.Instance.StartDialogue(
                dialogue,
                lockPlayerControlsDuringDialogue,
                HandleDialogueCompleted
            );

            if (started)
            {
                hasPlayed = true;
                break;
            }

            yield return null;
        }

        waitForDoorRoutine = null;
    }

    private void HandleDialogueCompleted()
    {
        DialogueCompleted?.Invoke();
    }

    private bool IsDoorOpenEnough()
    {
        if (!rotationsCached || doorTransform == null)
            return true;

        Quaternion currentRotation = useLocalRotation
            ? doorTransform.localRotation
            : doorTransform.rotation;

        float tolerance = Mathf.Max(0.01f, openAngleTolerance);
        return Quaternion.Angle(currentRotation, openRotation) <= tolerance;
    }

    private void CacheRotations()
    {
        if (doorTransform == null)
        {
            rotationsCached = false;
            return;
        }

        if (useLocalRotation)
        {
            closedRotation = doorTransform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(openRotationEuler);
        }
        else
        {
            closedRotation = doorTransform.rotation;
            openRotation = closedRotation * Quaternion.Euler(openRotationEuler);
        }

        rotationsCached = true;
    }

    private void ConfigureTrigger()
    {
        Collider triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private bool IsPlayerCollider(Collider other)
    {
        return other != null && other.CompareTag(playerTag);
    }
}
