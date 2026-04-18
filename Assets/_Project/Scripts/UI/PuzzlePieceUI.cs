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
    private bool inputEnabled = true;

    public int CorrectSlotIndex => correctSlotIndex;
    public RectTransform RectTransform => rectTransform;
    public bool IsPlaced { get; private set; }

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(PuzzleManagerUI puzzleManager)
    {
        CacheReferences();

        manager = puzzleManager;
        IsPlaced = false;
        inputEnabled = true;
        canvasGroup.blocksRaycasts = true;
        rectTransform.localScale = Vector3.one;
    }

    public void SetInputEnabled(bool value)
    {
        CacheReferences();

        inputEnabled = value;
        canvasGroup.blocksRaycasts = value;

        if (!value)
            rectTransform.localScale = Vector3.one;
    }

    public void SetSize(Vector2 size)
    {
        CacheReferences();
        rectTransform.sizeDelta = size;
    }

    public void SetSpawnState(Vector3 position, float zRotation)
    {
        CacheReferences();

        IsPlaced = false;
        rectTransform.position = position;
        rectTransform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        rectTransform.localScale = Vector3.one;
        canvasGroup.blocksRaycasts = inputEnabled;
    }

    public void LockToSlot(PuzzleSlotUI slot)
    {
        CacheReferences();

        IsPlaced = true;
        rectTransform.position = slot.RectTransform.position;
        rectTransform.rotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        canvasGroup.blocksRaycasts = inputEnabled;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsPlaced || manager == null || !inputEnabled || manager.InputLocked)
            return;

        CacheReferences();

        canvasGroup.blocksRaycasts = false;
        rectTransform.rotation = Quaternion.identity;
        rectTransform.SetAsLastSibling();
        rectTransform.localScale = Vector3.one * pickupScale;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsPlaced || manager == null || !inputEnabled || manager.InputLocked)
            return;

        CacheReferences();
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsPlaced || manager == null || !inputEnabled || manager.InputLocked)
            return;

        CacheReferences();

        canvasGroup.blocksRaycasts = inputEnabled;
        rectTransform.localScale = Vector3.one;

        manager.TrySnapPiece(this);
    }
}