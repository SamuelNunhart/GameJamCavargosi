using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Image))]
public class PuzzlePieceUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private int correctSlotIndex;
    [SerializeField] private float pickupScale = 1.05f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private PuzzleManagerUI manager;

    public int CorrectSlotIndex => correctSlotIndex;
    public RectTransform RectTransform => rectTransform;
    public bool IsPlaced { get; private set; }

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(PuzzleManagerUI puzzleManager)
    {
        manager = puzzleManager;
        IsPlaced = false;
        canvasGroup.blocksRaycasts = true;
        rectTransform.localScale = Vector3.one;
    }

    public void SetSize(Vector2 size)
    {
        rectTransform.sizeDelta = size;
    }

    public void SetSpawnState(Vector3 position, float zRotation)
    {
        IsPlaced = false;
        rectTransform.position = position;
        rectTransform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        rectTransform.localScale = Vector3.one;
        canvasGroup.blocksRaycasts = true;
    }

    public void LockToSlot(PuzzleSlotUI slot)
    {
        IsPlaced = true;
        rectTransform.position = slot.RectTransform.position;
        rectTransform.rotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        canvasGroup.blocksRaycasts = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsPlaced || manager == null)
            return;

        canvasGroup.blocksRaycasts = false;

        // Upright while held
        rectTransform.rotation = Quaternion.identity;

        // Bring piece on top visually
        rectTransform.SetAsLastSibling();

        rectTransform.localScale = Vector3.one * pickupScale;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsPlaced || manager == null)
            return;

        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsPlaced || manager == null)
            return;

        canvasGroup.blocksRaycasts = true;
        rectTransform.localScale = Vector3.one;

        manager.TrySnapPiece(this);
    }
}