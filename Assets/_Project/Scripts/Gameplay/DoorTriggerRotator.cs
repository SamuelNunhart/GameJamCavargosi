using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoorTriggerRotator : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform doorTransform;
    [SerializeField] private Vector3 openRotationEuler = new Vector3(0f, 90f, 0f);
    [SerializeField] private bool useLocalRotation = true;
    [SerializeField] private float rotationSpeedDegreesPerSecond = 180f;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool closeWhenPlayerLeaves;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private int overlappingPlayerCount;
    private bool isOpen;
    private bool rotationsCached;

    private void Reset()
    {
        ConfigureTrigger();

        if (doorTransform == null && transform.parent != null)
            doorTransform = transform.parent;
    }

    private void OnValidate()
    {
        ConfigureTrigger();

        if (!Application.isPlaying)
            CacheRotations();
    }

    private void Awake()
    {
        ConfigureTrigger();
        CacheRotations();
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
        if (!IsPlayerCollider(other))
            return;

        overlappingPlayerCount++;
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
        isOpen = true;
    }

    public void CloseDoor()
    {
        isOpen = false;
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
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
