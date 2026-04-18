using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerTriggerRelay : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform playerViewTransform;
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;
    [SerializeField] private bool triggerOnce = true;

    private Collider triggerCollider;
    private bool hasTriggered;

    public event Action<Collider> PlayerEntered;

    private void Reset()
    {
        AutoAssignReferences();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        ConfigureTrigger();
    }

    private void Awake()
    {
        AutoAssignReferences();
        ConfigureTrigger();
    }

    public void ResetTriggerState()
    {
        hasTriggered = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryNotify(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryNotify(other);
    }

    private void TryNotify(Collider other)
    {
        if (!enabled)
            return;

        if (triggerOnce && hasTriggered)
            return;

        if (!IsPlayerCollider(other))
            return;

        hasTriggered = true;
        PlayerEntered?.Invoke(other);
    }

    private void AutoAssignReferences()
    {
        triggerCollider = GetComponent<Collider>();

        if (playerViewTransform == null && Camera.main != null)
            playerViewTransform = Camera.main.transform;

        if (playerViewTransform != null)
        {
            if (playerMovement == null)
                playerMovement = FindBehaviourInParents(playerViewTransform, "PlayerMove");

            if (playerLook == null)
                playerLook = FindBehaviourInParents(playerViewTransform, "PlayerLook");
        }

        if (playerMovement == null)
            playerMovement = FindBehaviourInScene("PlayerMove");

        if (playerLook == null)
            playerLook = FindBehaviourInScene("PlayerLook");
    }

    private void ConfigureTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag))
            return true;

        if (playerViewTransform == null)
            return IsLikelyPlayerHierarchy(other.transform);

        if (other.transform == playerViewTransform || other.transform.IsChildOf(playerViewTransform.root))
            return true;

        return IsLikelyPlayerHierarchy(other.transform);
    }

    private bool IsLikelyPlayerHierarchy(Transform candidate)
    {
        if (candidate == null)
            return false;

        Transform candidateRoot = candidate.root;

        if (!string.IsNullOrWhiteSpace(playerTag) && candidateRoot.CompareTag(playerTag))
            return true;

        if (!string.IsNullOrEmpty(candidateRoot.name) && candidateRoot.name.Contains("Player"))
            return true;

        if (playerMovement != null && candidateRoot == playerMovement.transform.root)
            return true;

        if (playerLook != null && candidateRoot == playerLook.transform.root)
            return true;

        if (playerViewTransform != null && candidateRoot == playerViewTransform.root)
            return true;

        Rigidbody attachedRigidbody = candidate.GetComponentInParent<Rigidbody>();
        if (attachedRigidbody != null)
        {
            Transform rigidbodyRoot = attachedRigidbody.transform.root;

            if ((!string.IsNullOrWhiteSpace(playerTag) && rigidbodyRoot.CompareTag(playerTag)) ||
                (!string.IsNullOrEmpty(rigidbodyRoot.name) && rigidbodyRoot.name.Contains("Player")))
                return true;
        }

        CharacterController characterController = candidate.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            Transform controllerRoot = characterController.transform.root;

            if ((!string.IsNullOrWhiteSpace(playerTag) && controllerRoot.CompareTag(playerTag)) ||
                (!string.IsNullOrEmpty(controllerRoot.name) && controllerRoot.name.Contains("Player")))
                return true;
        }

        return false;
    }

    private static MonoBehaviour FindBehaviourInParents(Transform start, string behaviourName)
    {
        if (start == null)
            return null;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour != null && behaviour.GetType().Name == behaviourName)
                return behaviour;
        }

        return null;
    }

    private static MonoBehaviour FindBehaviourInScene(string behaviourName)
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour != null && behaviour.GetType().Name == behaviourName)
                return behaviour;
        }

        return null;
    }
}
