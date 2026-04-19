using UnityEngine;

public class GridManager : MonoBehaviour
{
    public void CheckAllRotations()
    {
        // 1. Získame všetky rúrky v scéne
        PipeTile[] allTiles = FindObjectsOfType<PipeTile>();

        int totalRealTiles = 0;
        int correctRealTiles = 0;
        PipeTile firstIncorrectTile = null;

        // 2. Prejdeme zoznam a skontrolujeme tie, ktoré sú "isReal"
        foreach (PipeTile tile in allTiles)
        {
            if (tile.isReal)
            {
                totalRealTiles++;

                if (tile.IsCorrect())
                {
                    correctRealTiles++;
                }
                else if (firstIncorrectTile == null)
                {
                    // Zapamätáme si prvú chybu na detailný výpis
                    firstIncorrectTile = tile;
                }
            }
        }

        // 3. Výpis stavu do konzoly (pre tvoj prehľad)
        Debug.Log($"<color=cyan>Stav: {correctRealTiles} / {totalRealTiles} správne.</color>");

        // 4. Ak niečo nesedí, vypíšeme detail a zastavíme kontrolu
        if (correctRealTiles < totalRealTiles)
        {
            if (firstIncorrectTile != null)
            {
                // Keďže cieľ je vždy 0, vypíšeme koľkokrát ešte treba kliknúť
                int clicksNeeded = 4 - firstIncorrectTile.currentRotationIndex;
                Debug.Log($"<color=orange>Hra pokračuje.</color> Rúrka <b>'{firstIncorrectTile.gameObject.name}'</b> " +
                          $"je v polohe {firstIncorrectTile.currentRotationIndex}. " +
                          $"Potrebuje ešte {clicksNeeded} klik(y) pre návrat do pôvodného stavu.");
            }
            return;
        }

        // 5. Víťazstvo
        if (totalRealTiles > 0)
        {
            Debug.Log("<color=green>VŠETKO JE SPRÁVNE! 🎉</color>");
            Debug.Log("Všetky reálne rúrky sú v pôvodných pozíciách.");

            if (FamilyMinigamePlaceholderController.TryCompleteActiveSceneMinigame())
                return;

            if (GameManager.instance != null)
            {
                GameManager.instance.Win();
            }
        }
        else
        {
            Debug.LogWarning("Nenašiel som žiadne rúrky s označením 'isReal'!");
        }
    }
}
