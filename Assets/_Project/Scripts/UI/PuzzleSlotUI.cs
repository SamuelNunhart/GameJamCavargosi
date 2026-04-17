using UnityEngine;

public class PuzzleSlotUI : MonoBehaviour
{
    public int SlotIndex { get; private set; }
    public bool IsOccupied { get; private set; }
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        RectTransform = (RectTransform)transform;
    }

    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
        IsOccupied = false;
    }

    public void SetOccupied(bool value)
    {
        IsOccupied = value;
    }
}