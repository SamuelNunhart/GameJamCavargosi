using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Handles Father's day progression, blackouts and chapter exit.</summary>
public sealed class FatherBlackout : MonoBehaviour
{
    [SerializeField] private FatherFridgeInteractable fridge;
    [SerializeField] private FatherSinkRepair sinkRepair;
    [SerializeField] private StoryPlayerRig playerRig;
    [SerializeField] private PlayerControlLocker controlLocker;
    [SerializeField, Min(0f)] private float wakeCameraDrop = 1.15f;
    [SerializeField] private TMP_Text dayCounter;

    public const int BlackoutBeerCount = 4;
    private const int FinalDay = 4;
    private const string MainMenuScene = "MainMenu";
    private CanvasGroup overlay;
    private TextMeshProUGUI caption;
    private Coroutine sequence;
    private Transform view;
    private Vector3 standingCameraPosition;
    private bool cameraLowered;

    public int Day { get; private set; } = 1;

    private void Start()
    {
        if (playerRig != null && playerRig.GameplayCamera != null)
            standingCameraPosition = playerRig.GameplayCamera.transform.localPosition;
        UpdateDayCounter();
    }

    private void OnEnable()
    {
        if (fridge != null) fridge.BeerDrunk += OnBeerDrunk;
        if (sinkRepair != null) sinkRepair.RepairCompleted += OnWorkCompleted;
    }

    private void OnDisable()
    {
        if (fridge != null) fridge.BeerDrunk -= OnBeerDrunk;
        if (sinkRepair != null) sinkRepair.RepairCompleted -= OnWorkCompleted;
        if (sequence == null) return;
        StopCoroutine(sequence);
        EndSequence();
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.gameObject);
    }

    private void OnBeerDrunk(int count)
    {
        if (count != BlackoutBeerCount || sequence != null || playerRig == null || controlLocker == null ||
            playerRig.GameplayCamera == null || !StoryAccess.Allows(this, StoryId.Father)) return;

        controlLocker.AcquireLock();
        sequence = StartCoroutine(BlackOut());
    }

    private void OnWorkCompleted()
    {
        if (sequence != null || controlLocker == null || !StoryAccess.Allows(this, StoryId.Father)) return;
        if (TryAdvanceDay()) return;

        controlLocker.AcquireLock();
        sequence = StartCoroutine(ReturnToMenu());
    }

    private IEnumerator BlackOut()
    {
        // Give the fridge door time to close before fading out.
        yield return WaitForBlackoutBeat(1.8f);
        EnsureOverlay();
        caption.text = string.Empty;
        overlay.alpha = 0f;
        overlay.gameObject.SetActive(true);
        yield return Fade(0f, 1f, .65f);

        caption.text = "I drank too much.";
        yield return WaitForBlackoutBeat(1.8f);

        bool skippedRepair = sinkRepair != null && sinkRepair.SkipStartedRepair();
        if (!TryAdvanceDay())
        {
            if (skippedRepair)
            {
                caption.text = "The sink is still broken.";
                yield return WaitForBlackoutBeat(1.5f);
            }
            yield return ReturnToMenu();
            yield break;
        }

        playerRig.ReturnToStart();
        view = playerRig.GameplayCamera.transform;
        Vector3 drop = Vector3.down * wakeCameraDrop;
        if (view.parent != null) drop = view.parent.InverseTransformVector(drop);
        Vector3 wakingPosition = standingCameraPosition + drop;
        view.localPosition = wakingPosition;
        cameraLowered = true;

        caption.text = skippedRepair
            ? $"Day {Day}\nI lost a day. The sink is still broken."
            : $"Day {Day}\nI lost a day.";
        yield return WaitForBlackoutBeat(2.3f);
        yield return Fade(1f, 0f, .7f);
        yield return WaitForBlackoutBeat(.45f);

        const float riseTime = 1.1f;
        for (float elapsed = 0f; elapsed < riseTime; elapsed += StepTime)
        {
            view.localPosition = Vector3.Lerp(wakingPosition, standingCameraPosition,
                Mathf.SmoothStep(0f, 1f, elapsed / riseTime));
            yield return null;
        }

        EndSequence();
    }

    private bool TryAdvanceDay()
    {
        if (Day >= FinalDay) return false;

        Day++;
        if (fridge != null) fridge.ClearDrunkenness();
        UpdateDayCounter();
        return true;
    }

    private IEnumerator ReturnToMenu()
    {
        EnsureOverlay();
        caption.text = string.Empty;
        overlay.gameObject.SetActive(true);
        if (overlay.alpha < 1f) yield return Fade(overlay.alpha, 1f, .55f);
        SceneManager.LoadScene(MainMenuScene);
    }

    private void UpdateDayCounter()
    {
        if (dayCounter != null) dayCounter.text = $"Day {Day}";
    }

    private void EndSequence()
    {
        if (cameraLowered && view != null) view.localPosition = standingCameraPosition;
        cameraLowered = false;
        if (overlay != null) overlay.gameObject.SetActive(false);
        if (controlLocker != null) controlLocker.ReleaseLock();
        sequence = null;
    }

    private static float StepTime => AudioSettingsOverlay.IsOpen ? 0f : Time.unscaledDeltaTime;

    private static IEnumerator WaitForBlackoutBeat(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += StepTime) yield return null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += StepTime)
        {
            overlay.alpha = Mathf.SmoothStep(from, to, elapsed / duration);
            yield return null;
        }
        overlay.alpha = to;
    }

    private void EnsureOverlay()
    {
        if (overlay != null) return;
        var root = new GameObject("Father Blackout Overlay", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        overlay = root.GetComponent<CanvasGroup>();
        overlay.alpha = 0f;
        overlay.blocksRaycasts = false;
        overlay.interactable = false;

        var shade = new GameObject("Black Screen", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        shade.transform.SetParent(root.transform, false);
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = Vector2.one;
        shade.rectTransform.offsetMin = Vector2.zero;
        shade.rectTransform.offsetMax = Vector2.zero;
        shade.color = Color.black;
        shade.raycastTarget = false;

        caption = new GameObject("Blackout Caption", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        caption.transform.SetParent(root.transform, false);
        caption.rectTransform.sizeDelta = new Vector2(940f, 168f);
        caption.fontSize = 38f;
        caption.lineSpacing = 16f;
        caption.alignment = TextAlignmentOptions.Center;
        caption.color = new Color32(242, 226, 194, 255);
        caption.raycastTarget = false;
    }
}
