using UnityEngine;

public class PipeTile : MonoBehaviour
{
    public enum TileType { Straight, Other } // Rozlíšime typy rúrok

    [Header("Nastavenia typu")]
    public TileType type;
    public bool isReal = true;

    [Header("Aktuálny stav (0 je cie¾)")]
    public int currentRotationIndex = 0;

    void Start()
    {
        currentRotationIndex = 0;
        int randomRotations = Random.Range(0, 4);
        for (int i = 0; i < randomRotations; i++)
        {
            ApplyVisualRotation();
        }
    }

    public void OnClick()
    {
        ApplyVisualRotation();
        if (FindObjectOfType<GridManager>() != null)
        {
            FindObjectOfType<GridManager>().CheckAllRotations();
        }
    }

    private void ApplyVisualRotation()
    {
        currentRotationIndex = (currentRotationIndex + 1) % 4;
        transform.Rotate(0, 0, -90);
    }

    public bool IsCorrect()
    {
        if (!isReal) return true;

        // Ak je rúrka rovná, akceptujeme index 0 aj 2 (otoèenie o 180 stupòov)
        if (type == TileType.Straight)
        {
            return currentRotationIndex == 0 || currentRotationIndex == 2;
        }

        // Pre ostatné (Corner, T, atï.) platí len pôvodná pozícia 0
        return currentRotationIndex == 0;
    }
}