using System.Collections.Generic;
using UnityEngine;

public class PlayerControlLocker : MonoBehaviour
{
    [SerializeField] private List<Behaviour> behavioursToDisable = new List<Behaviour>();

    private int lockCount;

    public bool IsLocked => lockCount > 0;

    public void AcquireLock()
    {
        lockCount++;
        ApplyLockState();
    }

    public void ReleaseLock()
    {
        if (lockCount == 0)
            return;

        lockCount--;
        ApplyLockState();
    }

    public void ClearLocks()
    {
        if (lockCount == 0)
            return;

        lockCount = 0;
        ApplyLockState();
    }

    private void ApplyLockState()
    {
        bool shouldEnableBehaviours = lockCount == 0;

        foreach (Behaviour behaviour in behavioursToDisable)
        {
            if (behaviour != null)
                behaviour.enabled = shouldEnableBehaviours;
        }
    }
}
