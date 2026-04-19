using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoorTriggerRotator : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform doorTransform;
    [SerializeField] private Vector3 openRotationEuler = new Vector3(0f, 90f, 0f);
    [SerializeField] private bool useLocalRotation = true;
    [SerializeField] private float rotationSpeedDegreesPerSecond = 180f;

    [Header("Collision")]
    [SerializeField] private bool ensurePhysicalDoorCollider = true;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool closeWhenPlayerLeaves;
    [SerializeField] private bool interactionEnabled = true;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private int overlappingPlayerCount;
    private bool isOpen;
    private bool rotationsCached;
    private BoxCollider generatedDoorCollider;

    public bool IsInteractionEnabled => interactionEnabled;

    private void Reset()
    {
        ConfigureTrigger();

        if (doorTransform == null && transform.parent != null)
            doorTransform = transform.parent;

        UpdateGeneratedDoorCollider();
    }

    private void OnValidate()
    {
        ConfigureTrigger();

        if (!Application.isPlaying)
        {
            CacheRotations();
        }
    }

    private void Awake()
    {
        ConfigureTrigger();
        CacheRotations();
        UpdateGeneratedDoorCollider();
    }

    private void Update()
    {
        if (!rotationsCached || doorTransform == null)
            return;

        float step = Mathf.Max(0f, rotationSpeedDegreesPerSecond) * Time.deltaTime;
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;

        if (useLocalRotation)
        {
            doorTransform.localRotation = Quaternion.RotateTowards(
                doorTransform.localRotation,
                targetRotation,
                step
            );
        }
        else
        {
            doorTransform.rotation = Quaternion.RotateTowards(
                doorTransform.rotation,
                targetRotation,
                step
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!interactionEnabled)
            return;

        if (!IsPlayerCollider(other))
            return;

        overlappingPlayerCount++;
        OpenDoor();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!interactionEnabled || !IsPlayerCollider(other))
            return;

        if (overlappingPlayerCount == 0)
            overlappingPlayerCount = 1;

        OpenDoor();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        overlappingPlayerCount = Mathf.Max(0, overlappingPlayerCount - 1);

        if (closeWhenPlayerLeaves && overlappingPlayerCount == 0)
            CloseDoor();
    }

    [ContextMenu("Cache Closed Rotation")]
    public void CacheClosedRotation()
    {
        CacheRotations();
    }

    public void OpenDoor()
    {
        bool wasOpen = isOpen;
        isOpen = true;

        if (!wasOpen)
            GameplayMusicController.PlayKitchenDoorMusic();
    }

    public void CloseDoor()
    {
        isOpen = false;
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (interactionEnabled)
            return;

        overlappingPlayerCount = 0;
        CloseDoor();
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

    private void UpdateGeneratedDoorCollider()
    {
        if (!ensurePhysicalDoorCollider || doorTransform == null)
            return;

        if (HasGeneratedColliderAlreadyCovered(doorTransform))
            return;

        if (!TryGetDoorLocalBounds(doorTransform, out Vector3 center, out Vector3 size))
            return;

        generatedDoorCollider = doorTransform.GetComponent<BoxCollider>();

        if (generatedDoorCollider == null || generatedDoorCollider.isTrigger)
            generatedDoorCollider = doorTransform.gameObject.AddComponent<BoxCollider>();

        generatedDoorCollider.isTrigger = false;
        generatedDoorCollider.center = center;
        generatedDoorCollider.size = size;
    }

    private static bool HasGeneratedColliderAlreadyCovered(Transform target)
    {
        Collider[] colliders = target.GetComponents<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null || collider.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    private static bool TryGetDoorLocalBounds(Transform target, out Vector3 center, out Vector3 size)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled)
                continue;

            Bounds bounds = renderer.bounds;
            Vector3 boundsMin = bounds.min;
            Vector3 boundsMax = bounds.max;

            for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                Vector3 worldCorner = new Vector3(
                    (cornerIndex & 1) == 0 ? boundsMin.x : boundsMax.x,
                    (cornerIndex & 2) == 0 ? boundsMin.y : boundsMax.y,
                    (cornerIndex & 4) == 0 ? boundsMin.z : boundsMax.z
                );

                Vector3 localCorner = target.InverseTransformPoint(worldCorner);

                if (!hasBounds)
                {
                    min = localCorner;
                    max = localCorner;
                    hasBounds = true;
                    continue;
                }

                min = Vector3.Min(min, localCorner);
                max = Vector3.Max(max, localCorner);
            }
        }

        center = hasBounds ? (min + max) * 0.5f : Vector3.zero;
        size = hasBounds ? max - min : Vector3.zero;
        return hasBounds;
    }

    private bool IsPlayerCollider(Collider other)
    {
        return other != null && other.CompareTag(playerTag);
    }
}
