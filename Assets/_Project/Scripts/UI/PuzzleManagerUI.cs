using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PuzzleManagerUI : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private int columns = 5;
    [SerializeField] private int rows = 4;
    [SerializeField] private RectTransform boardArea;
    [SerializeField] private PuzzleSlotUI slotPrefab;
    [SerializeField] private Vector2 slotSpacing = new Vector2(6f, 6f);
    [SerializeField] private int boardPadding = 8;

    [Header("Spawn")]
    [SerializeField] private RectTransform spawnArea;
    [SerializeField] private float boardAvoidPadding = 50f;
    [SerializeField] private float spawnRotationRange = 10f;

    [Header("Pieces")]
    [SerializeField] private List<PuzzlePieceUI> pieces = new List<PuzzlePieceUI>();
    [SerializeField] private float snapDistance = 55f;

    [Header("Progress Events")]
    [SerializeField] private List<int> progressThresholds = new List<int> { 5, 10, 15 };

    [Header("Solved")]
    [SerializeField] private UnityEvent onPuzzleSolved;

    private GridLayoutGroup boardGrid;
    private readonly List<PuzzleSlotUI> slots = new List<PuzzleSlotUI>();
    private readonly HashSet<int> triggeredThresholds = new HashSet<int>();

    private Vector2 cellSize;
    private int placedCount;
    private bool inputLocked;
    private bool isSolved;

    public int PlacedCount => placedCount;
    public bool InputLocked => inputLocked;
    public bool IsSolved => isSolved;

    public event Action<int> PiecePlaced;
    public event Action<int> ProgressThresholdReached;
    public event Action PuzzleSolved;

    private void Awake()
    {
        boardGrid = boardArea.GetComponent<GridLayoutGroup>();

        if (boardGrid == null)
        {
            Debug.LogError("BoardArea needs a GridLayoutGroup component.");
        }
    }

    private void Start()
    {
        BuildBoard();
        PreparePieces();
        ScatterPieces();
    }

    private void BuildBoard()
    {
        ClearGeneratedSlots();

        boardGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        boardGrid.constraintCount = columns;
        boardGrid.spacing = slotSpacing;
        boardGrid.padding = new RectOffset(boardPadding, boardPadding, boardPadding, boardPadding);

        float usableWidth =
            boardArea.rect.width
            - boardGrid.padding.left
            - boardGrid.padding.right
            - ((columns - 1) * slotSpacing.x);

        float usableHeight =
            boardArea.rect.height
            - boardGrid.padding.top
            - boardGrid.padding.bottom
            - ((rows - 1) * slotSpacing.y);

        cellSize = new Vector2(usableWidth / columns, usableHeight / rows);
        boardGrid.cellSize = cellSize;

        int slotCount = columns * rows;

        for (int i = 0; i < slotCount; i++)
        {
            PuzzleSlotUI slot = Instantiate(slotPrefab, boardArea);
            slot.Initialize(i);
            slots.Add(slot);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(boardArea);
        Canvas.ForceUpdateCanvases();
    }

    private void PreparePieces()
    {
        int expected = columns * rows;

        if (pieces.Count != expected)
        {
            Debug.LogWarning($"Expected {expected} pieces, but found {pieces.Count}.");
        }

        foreach (PuzzlePieceUI piece in pieces)
        {
            if (piece == null)
                continue;

            piece.Setup(this);
            piece.SetSize(cellSize);
        }
    }

    public void ScatterPieces()
    {
        placedCount = 0;
        inputLocked = false;
        isSolved = false;
        triggeredThresholds.Clear();

        foreach (PuzzleSlotUI slot in slots)
        {
            slot.SetOccupied(false);
        }

        Vector2 halfPieceSize = cellSize * 0.5f;

        foreach (PuzzlePieceUI piece in pieces)
        {
            if (piece == null)
                continue;

            piece.Setup(this);
            piece.SetSize(cellSize);
            piece.SetInputEnabled(true);

            Vector3 spawnPos = GetRandomSpawnPositionOutsideBoard(halfPieceSize);
            float randomRotation = UnityEngine.Random.Range(-spawnRotationRange, spawnRotationRange);

            piece.SetSpawnState(spawnPos, randomRotation);
        }
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;

        foreach (PuzzlePieceUI piece in pieces)
        {
            if (piece == null)
                continue;

            piece.SetInputEnabled(!locked);
        }
    }

    public void TrySnapPiece(PuzzlePieceUI piece)
    {
        if (inputLocked || isSolved)
            return;

        int targetIndex = piece.CorrectSlotIndex;

        if (targetIndex < 0 || targetIndex >= slots.Count)
        {
            Debug.LogWarning($"{piece.name} has invalid slot index {targetIndex}.");
            return;
        }

        PuzzleSlotUI targetSlot = slots[targetIndex];

        if (targetSlot.IsOccupied)
            return;

        float distance = Vector2.Distance(piece.RectTransform.position, targetSlot.RectTransform.position);

        if (distance <= snapDistance)
        {
            targetSlot.SetOccupied(true);
            piece.LockToSlot(targetSlot);

            placedCount++;
            PiecePlaced?.Invoke(placedCount);

            if (!isSolved && progressThresholds.Contains(placedCount) && !triggeredThresholds.Contains(placedCount) && placedCount < pieces.Count)
            {
                triggeredThresholds.Add(placedCount);
                ProgressThresholdReached?.Invoke(placedCount);
            }

            if (!isSolved && placedCount >= pieces.Count)
            {
                isSolved = true;
                Debug.Log("Puzzle solved.");

                PuzzleSolved?.Invoke();
                onPuzzleSolved?.Invoke();
            }
        }
    }

    private Vector3 GetRandomSpawnPositionOutsideBoard(Vector2 halfPieceSize)
    {
        Rect spawnRect = GetWorldRect(spawnArea);
        Rect boardRect = ExpandRect(GetWorldRect(boardArea), boardAvoidPadding);

        float minX = spawnRect.xMin + halfPieceSize.x;
        float maxX = spawnRect.xMax - halfPieceSize.x;
        float minY = spawnRect.yMin + halfPieceSize.y;
        float maxY = spawnRect.yMax - halfPieceSize.y;

        for (int i = 0; i < 100; i++)
        {
            float x = UnityEngine.Random.Range(minX, maxX);
            float y = UnityEngine.Random.Range(minY, maxY);
            Vector2 point = new Vector2(x, y);

            if (!boardRect.Contains(point))
            {
                return new Vector3(x, y, 0f);
            }
        }

        float fallbackX = UnityEngine.Random.Range(minX, maxX);
        float fallbackY = UnityEngine.Random.Range(minY, maxY);
        return new Vector3(fallbackX, fallbackY, 0f);
    }

    private Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float xMin = corners[0].x;
        float yMin = corners[0].y;
        float xMax = corners[2].x;
        float yMax = corners[2].y;

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private Rect ExpandRect(Rect rect, float amount)
    {
        rect.xMin -= amount;
        rect.xMax += amount;
        rect.yMin -= amount;
        rect.yMax += amount;
        return rect;
    }

    private void ClearGeneratedSlots()
    {
        for (int i = boardArea.childCount - 1; i >= 0; i--)
        {
            Destroy(boardArea.GetChild(i).gameObject);
        }

        slots.Clear();
    }
}