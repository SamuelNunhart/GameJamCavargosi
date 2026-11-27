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
    [Header("Day 1 cutscene")]
    [SerializeField] private Transform fridgeShot;
    [SerializeField] private Transform sinkShot;
    [SerializeField] private FatherAlcoholIndicator alcoholIndicator;
    [SerializeField] private DialogueData openingAtStart;
    [SerializeField] private DialogueData openingAtFridge;
    [SerializeField] private DialogueData openingAtSink;
    [SerializeField] private DialogueData openingBeforeBlackout;
    [SerializeField] private DialogueData firstMorningDialogue;
    [SerializeField] private FatherOpeningPresentation scriptedOpening;

    public const int BlackoutBeerCount = 4;
    private const int OpeningBeerCount = 2;
    private const int FinalDay = 5;
    private const string MainMenuScene = "MainMenu";
    private CanvasGroup overlay;
    private TextMeshProUGUI caption;
    private Coroutine sequence;
    private Transform view;
    private Vector3 standingCameraPosition;
    private Quaternion standingCameraRotation;
    private float standingCameraHeight;
    private bool openingCameraMoved;
    private bool cameraLowered;

    public int Day { get; private set; } = 1;

    private void Start()
    {
        UpdateDayCounter();
        if (fridge == null || playerRig == null || playerRig.GameplayCamera == null || controlLocker == null ||
            !StoryAccess.Allows(this, StoryId.Father)) return;

        view = playerRig.GameplayCamera.transform;
        standingCameraPosition = view.localPosition;
        standingCameraRotation = view.localRotation;
        standingCameraHeight = view.position.y;
        fridge.SetDrunkenness(OpeningBeerCount);
        controlLocker.AcquireLock();
        sequence = StartCoroutine(PlayOpening());
    }

#if UNITY_EDITOR // DEBUG-ONLY: remove before release; see docs/debug-skips.md
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.BackQuote)) DebugSkipOpening();
    }

    public bool DebugSkipOpening()
    {
        if (sequence == null || AudioSettingsOverlay.IsOpen || (Day != 1 && !(Day == 2 && cameraLowered)))
            return false;

        StopCoroutine(sequence);
        fridge.DebugFinishOpeningPickups(fridgeShot != null ? fridgeShot.position : view.position,
            scriptedOpening != null ? 1 : BlackoutBeerCount - OpeningBeerCount);
        DialogueUI ui = DialogueUI.Instance;
        DialogueData dialogue = ui != null ? ui.CurrentDialogue : null;
        if (dialogue != null && (dialogue == openingAtStart || dialogue == openingAtFridge ||
                                 dialogue == openingAtSink || dialogue == openingBeforeBlackout ||
                                 (scriptedOpening != null && scriptedOpening.OwnsDialogue(dialogue))))
            ui.DebugFinishConversation();

        if (Day == 1) TryAdvanceDay();
        if (alcoholIndicator != null) alcoholIndicator.DebugResetAfterOpening();
        ReturnToStartingView();
        FinishWakeup(true);
        return true;
    }
#endif

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
        if (count == BlackoutBeerCount) BeginBlackout();
    }

    private IEnumerator PlayOpening()
    {
        if (scriptedOpening != null)
        {
            yield return PlayScriptedOpening();
            yield break;
        }

        // Scenes without the revised opening use the original sequence.
        yield return WaitForBlackoutBeat(.65f);
        yield return PlayOpeningDialogue(openingAtStart);

        yield return CutTo(fridgeShot);
        yield return DrinkAtFridge();
        yield return PlayOpeningDialogue(openingAtFridge);

        yield return CutTo(sinkShot);
        if (sinkShot != null) yield return InspectSink();
        yield return PlayOpeningDialogue(openingAtSink);

        yield return CutTo(fridgeShot);
        yield return DrinkAtFridge();
        yield return PlayOpeningDialogue(openingBeforeBlackout);
        yield return BlackOut(0f);
    }

    private IEnumerator PlayScriptedOpening()
    {
        yield return CutTo(sinkShot, startFromBlack: true,
            onBlack: () =>
            {
                scriptedOpening.SetVision(playerRig.GameplayCamera, OpeningBeerCount);
                scriptedOpening.ShowCloggedSink();
            });
        yield return PlayOpeningDialogue(openingAtStart);
        yield return PlaySinkRepairShot(firstAttempt: true);
        yield return PlayOpeningDialogue(scriptedOpening.FirstRepairReaction);
        yield return StandUpFromSink();

        scriptedOpening.CloseSinkDoors();
        yield return WalkToFridge();
        yield return DrinkAtFridge();
        scriptedOpening.SetVision(playerRig.GameplayCamera, fridge.BeersDrunk);
        yield return scriptedOpening.Sway(view);
        yield return PlayOpeningDialogue(openingAtFridge);

        yield return PlaySinkRepairShot(firstAttempt: false);
        yield return PlayOpeningDialogue(openingAtSink);

        yield return CutTo(fridgeShot, onBlack: scriptedOpening.CloseSinkDoors);
        yield return DrinkAtFridge(PourAndDrinkShot, () => scriptedOpening.ShowDrink(view));
        scriptedOpening.SetVision(playerRig.GameplayCamera, BlackoutBeerCount);
        yield return PlayOpeningDialogue(openingBeforeBlackout);
        yield return scriptedOpening.Sway(view, 7f);
        yield return CutTo(scriptedOpening.CouchShot, onBlack: () =>
        {
            scriptedOpening.ShowCouch();
            scriptedOpening.PositionForCollapse(view);
        });
        yield return scriptedOpening.CollapseOntoCouch(view);

        yield return CutTo(scriptedOpening.CouchShot, onBlack: () =>
        {
            scriptedOpening.SetNight(playerRig.GameplayCamera, OpeningBeerCount);
        });
        yield return scriptedOpening.RevealGhost(view);
        yield return PlayOpeningDialogue(scriptedOpening.GhostArrival);
        scriptedOpening.ShowPhone();
        yield return scriptedOpening.FailPin();
        yield return PlayOpeningDialogue(scriptedOpening.FirstPinReply);
        yield return scriptedOpening.FailPin();
        yield return scriptedOpening.Sway(view, 3f);
        yield return scriptedOpening.DropPhone();
        yield return PlayOpeningDialogue(scriptedOpening.GhostConfrontation);
        yield return BlackOut(0f, scriptedNight: true);
    }

    private IEnumerator PlaySinkRepairShot(bool firstAttempt)
    {
        yield return CutTo(scriptedOpening.SinkRepairShot);
        yield return scriptedOpening.OpenSinkDoors();
        yield return scriptedOpening.TryRepairWithWrench(firstAttempt);
    }

    private IEnumerator StandUpFromSink()
    {
        if (sinkShot == null) yield break;
        Vector3 crouchingPosition = view.position;
        Quaternion crouchingRotation = view.rotation;
        Vector3 standingPosition = new Vector3(sinkShot.position.x, standingCameraHeight, sinkShot.position.z);
        const float duration = .85f;
        for (float elapsed = 0f; elapsed < duration; elapsed += StepTime)
        {
            float blend = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            view.SetPositionAndRotation(Vector3.Lerp(crouchingPosition, standingPosition, blend),
                Quaternion.Slerp(crouchingRotation, sinkShot.rotation, blend));
            yield return null;
        }
        view.SetPositionAndRotation(standingPosition, sinkShot.rotation);
        yield return WaitForBlackoutBeat(.18f);
    }

    private IEnumerator WalkToFridge()
    {
        if (fridgeShot == null) yield break;

        Vector3 start = view.position;
        Vector3 destination = new Vector3(fridgeShot.position.x, start.y, fridgeShot.position.z);
        Vector3 path = destination - start;
        Quaternion fridgeFacing = Quaternion.LookRotation(Vector3.ProjectOnPlane(fridgeShot.forward, Vector3.up));
        Quaternion walkFacing = path.sqrMagnitude > .0001f ? Quaternion.LookRotation(path) : fridgeFacing;
        Quaternion startRotation = view.rotation;
        const float turnTime = .65f;
        for (float elapsed = 0f; elapsed < turnTime; elapsed += StepTime)
        {
            view.rotation = Quaternion.Slerp(startRotation, walkFacing, Mathf.SmoothStep(0f, 1f, elapsed / turnTime));
            yield return null;
        }
        view.rotation = walkFacing;

        float distance = path.magnitude;
        float walkTime = distance / .95f;
        const float stepsPerSecond = 1.45f;
        for (float elapsed = 0f; elapsed < walkTime; elapsed += StepTime)
        {
            float time = elapsed / walkTime;
            float progress = Mathf.SmoothStep(0f, 1f, time);
            float bob = Mathf.Sin(elapsed * stepsPerSecond * 2f * Mathf.PI) * Mathf.Sin(time * Mathf.PI) * .022f;
            view.position = start + path * progress + Vector3.up * bob;
            float lookProgress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.6f, 1f, progress));
            view.rotation = Quaternion.Slerp(walkFacing, fridgeFacing, lookProgress);
            yield return null;
        }
        view.SetPositionAndRotation(destination, fridgeFacing);

        const float lookDownTime = .65f;
        for (float elapsed = 0f; elapsed < lookDownTime; elapsed += StepTime)
        {
            float blend = Mathf.SmoothStep(0f, 1f, elapsed / lookDownTime);
            view.SetPositionAndRotation(Vector3.Lerp(destination, fridgeShot.position, blend),
                Quaternion.Slerp(fridgeFacing, fridgeShot.rotation, blend));
            yield return null;
        }
        SnapTo(fridgeShot);
    }

    private IEnumerator PourAndDrinkShot()
    {
        yield return RaiseCameraForDrink();
        yield return PlayOpeningDialogue(scriptedOpening.BeforeShot);
        yield return scriptedOpening.PourAndDrink();
    }

    private IEnumerator PlayOpeningDialogue(DialogueData dialogue)
    {
        DialogueUI ui = DialogueUI.Instance;
        if (dialogue == null || ui == null ||
            !ui.StartDialogue(new DialogueRequest(dialogue, autoAdvance: true, allowManualAdvance: scriptedOpening != null)))
            yield break;

        while (ui.CurrentDialogue == dialogue && ui.IsDialogueOpen) yield return null;
    }

    private IEnumerator CutTo(Transform shot, bool startFromBlack = false, System.Action onBlack = null)
    {
        if (shot == null) yield break;
        EnsureOverlay();
        overlay.gameObject.SetActive(true);
        if (!startFromBlack) yield return Fade(0f, 1f, .32f);
        else overlay.alpha = 1f;
        SnapTo(shot);
        onBlack?.Invoke();
        yield return Fade(1f, 0f, .35f);
    }

    private void SnapTo(Transform shot)
    {
        openingCameraMoved = true;
        view.SetPositionAndRotation(shot.position, shot.rotation);
    }

    private IEnumerator InspectSink()
    {
        Vector3 start = view.position;
        Vector3 lean = view.forward * .12f + Vector3.down * .04f;
        const float duration = 1.25f;
        for (float elapsed = 0f; elapsed < duration; elapsed += StepTime)
        {
            view.position = start + lean * Mathf.Sin(Mathf.PI * elapsed / duration);
            yield return null;
        }
        view.position = start;
    }

    private IEnumerator DrinkAtFridge(System.Func<IEnumerator> beforeDrink = null, System.Action takeShotProps = null)
    {
        int before = fridge.BeersDrunk;
        yield return fridge.DrinkForOpening(view, beforeDrink ?? RaiseCameraForDrink, takeShotProps);
        if (fridge.BeersDrunk <= before) yield break;

        if (alcoholIndicator != null) alcoholIndicator.EmphasizeDrink();
        yield return WaitForBlackoutBeat(1.1f);
    }

    private IEnumerator RaiseCameraForDrink()
    {
        Vector3 start = view.position;
        Vector3 standing = new Vector3(start.x, standingCameraHeight, start.z);
        const float duration = .75f;
        for (float elapsed = 0f; elapsed < duration; elapsed += StepTime)
        {
            view.position = Vector3.Lerp(start, standing, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        view.position = standing;
        yield return WaitForBlackoutBeat(.2f);
    }

    private void BeginBlackout()
    {
        if (sequence != null || playerRig == null || controlLocker == null ||
            playerRig.GameplayCamera == null || !StoryAccess.Allows(this, StoryId.Father)) return;

        controlLocker.AcquireLock();
        if (sinkRepair != null) sinkRepair.InterruptRepair();
        sequence = StartCoroutine(BlackOut(1.8f));
    }

    private void OnWorkCompleted()
    {
        if (sequence != null || controlLocker == null || !StoryAccess.Allows(this, StoryId.Father)) return;
        if (TryAdvanceDay()) return;

        controlLocker.AcquireLock();
        sequence = StartCoroutine(ReturnToMenu());
    }

    private IEnumerator BlackOut(float leadIn, bool scriptedNight = false)
    {
        yield return WaitForBlackoutBeat(leadIn);
        EnsureOverlay();
        caption.text = string.Empty;
        overlay.alpha = 0f;
        overlay.gameObject.SetActive(true);
        yield return Fade(0f, 1f, .65f);

        if (scriptedNight) scriptedOpening.Clear();
        else caption.text = "I drank too much.";
        yield return WaitForBlackoutBeat(1.8f);

        bool openingBlackout = Day == 1;
        if (!TryAdvanceDay())
        {
            yield return ReturnToMenu();
            yield break;
        }

        ReturnToStartingView();
        Vector3 drop = Vector3.down * wakeCameraDrop;
        if (view.parent != null) drop = view.parent.InverseTransformVector(drop);
        Vector3 wakingPosition = standingCameraPosition + drop;
        view.localPosition = wakingPosition;
        cameraLowered = true;

        caption.text = $"Day {Day}\nI lost a day.";
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

        FinishWakeup(openingBlackout);
    }

    private void ReturnToStartingView()
    {
        RestoreOpeningCamera();
        playerRig.ReturnToStart();
        view = playerRig.GameplayCamera.transform;
    }

    private void FinishWakeup(bool openingBlackout)
    {
        if (openingBlackout && firstMorningDialogue != null)
            DialogueUI.Instance?.StartDialogue(firstMorningDialogue, true);
        EndSequence();
    }

    private bool TryAdvanceDay()
    {
        if (Day >= FinalDay) return false;

        Day++;
        if (fridge != null) fridge.SetDrunkenness(0);
        UpdateDayCounter();
        return true;
    }

    private IEnumerator ReturnToMenu()
    {
        StoryChapterProgress.MarkCompleted(StoryId.Father);
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
        if (scriptedOpening != null) scriptedOpening.Clear();
        RestoreOpeningCamera();
        if (cameraLowered && view != null) view.localPosition = standingCameraPosition;
        cameraLowered = false;
        if (overlay != null) overlay.gameObject.SetActive(false);
        if (controlLocker != null) controlLocker.ReleaseLock();
        sequence = null;
    }

    private void RestoreOpeningCamera()
    {
        if (!openingCameraMoved || view == null) return;
        view.localPosition = standingCameraPosition;
        view.localRotation = standingCameraRotation;
        openingCameraMoved = false;
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
