using System;
#if UNITY_EDITOR
using System.Collections;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runs Father's timing minigame at each configured job station.</summary>
public sealed class FatherSinkRepair : MonoBehaviour
{
    public enum RepairResult { Unfinished, Fixed, SlowDrip, Leaking, FixedAndDishes, BarelyDrains }
    internal enum Job { Sink, Dinner, Power, Bike }

    [Serializable]
    private sealed class JobStation
    {
        public Job job;
        public Transform geometry;
        public Collider target;
    }

    [SerializeField] private StoryId story = StoryId.Father;
    [SerializeField] private StoryPlayerRig playerRig;
    [SerializeField] private PlayerControlLocker controlLocker;
    [SerializeField] private FatherFridgeInteractable fridge;
    [SerializeField] private Collider aimTarget;
    [SerializeField] private Transform sinkGeometry;
    [SerializeField, Min(0.1f)] private float interactionReach = 1.8f;
    [SerializeField] private FatherBlackout chapter;
    [SerializeField] private JobStation[] extraStations = Array.Empty<JobStation>();

    private const float HitFeedbackTime = 0.24f;
    private const float RestartFeedbackTime = 0.65f;
    private const float SoberHalfWidth = .0306667f;
    private const float SoberSweepSpeed = 1.584f;
    private static readonly float[] TargetCenters = { .5f, .67f, .36f, .57f, .43f, .63f };
    private static readonly float[] TargetHalfWidths =
        { SoberHalfWidth, SoberHalfWidth, SoberHalfWidth * 1.5f, SoberHalfWidth * 1.5f * 1.5f };
    private static readonly float[] SweepSpeeds =
        { SoberSweepSpeed, SoberSweepSpeed / 1.5f, SoberSweepSpeed / 1.5f, SoberSweepSpeed / (1.5f * 1.5f) };
    private static readonly Color32 Amber = new Color32(215, 158, 85, 255);
    private static readonly Color32 Ivory = new Color32(242, 226, 194, 255);

    private Canvas overlay;
    private RectTransform targetZone;
    private RectTransform marker;
    private TextMeshProUGUI feedback;
    private TextMeshProUGUI heading;
    private readonly RepairResult[] results = new RepairResult[4];
    private int difficulty;
    private int hits;
    private int misses;
    private float halfWindow;
    private float sweepTime;
    private float sweepSpeed;
    private float feedbackTime;
    private bool isOpen;
    public RepairResult Result => GetResult(Job.Sink);
    internal Job ActiveJob { get; private set; }
    internal bool HasChapterJobs => extraStations.Length > 0;
    internal int CompletedJobs { get; private set; }
    public event Action RepairCompleted;
    private int RoundLimit => TargetCenters.Length - difficulty;

#if UNITY_EDITOR // DEBUG-ONLY: remove before release; see docs/debug-skips.md
    private void Update()
    {
        if (playerRig != null && playerRig.UsesCenteredInteractions && Input.GetKeyDown(KeyCode.F9))
            StartCoroutine(CaptureAimAtEndOfFrame());
    }

    private IEnumerator CaptureAimAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        Debug.Log(BuildAimReport(), this);
    }

    private string BuildAimReport()
    {
        if (!InteractionAim.TryCreate(playerRig, interactionReach, out InteractionAim aim))
            return "[Sink aim] No active player camera.";

        bool aimed = IsAimingAtSink();
        RaycastHit solid = aim.DebugSolidHit;
        string hit = solid.collider != null
            ? $"{solid.collider.name} at {solid.distance:F2} m (belongs to sink: {aim.Sees(sinkGeometry)})"
            : "none in reach";
        if (solid.collider == null)
        {
            RaycastHit distantHit = aim.DebugFindDistantSolid(playerRig.transform, 6f);
            if (distantHit.collider != null)
                hit += $"; next is {distantHit.collider.name} at {distantHit.distance:F2} m";
        }

        string top = aimTarget != null && aimTarget.enabled && aimTarget.Raycast(aim.Ray, out RaycastHit candidate, 6f)
            ? $"{candidate.distance:F2} m, usable={aim.Ray.direction.y < 0f && aim.HitsTrigger(aimTarget)}"
            : "miss";
        DialogueUI ui = DialogueUI.Instance;
        Debug.DrawRay(aim.Ray.origin, aim.Ray.direction * interactionReach, aimed ? Color.green : Color.red, 8f, false);
        return $"[Sink aim] {gameObject.scene.name}, eye={aim.Ray.origin:F3}, direction={aim.Ray.direction:F3}\n" +
            $"Reach={interactionReach:F2} m, solid={hit}, top={top}, aimed={aimed}\n" +
            $"Can interact={playerRig.CanInteract}, settings={AudioSettingsOverlay.IsOpen}, " +
            $"dialogue={ui != null && ui.IsDialogueOpen}, correct story={StoryAccess.Allows(this, story)}, " +
            $"repair open={isOpen}, result={Result}\n" +
            $"Prompt: {ui?.DebugDescribePrompt() ?? "no dialogue UI"}";
    }
#endif

    public void InterruptRepair()
    {
        if (GetResult(ActiveJob) == RepairResult.Unfinished) CloseRepair();
        if (DialogueUI.Instance != null) DialogueUI.Instance.SetPromptVisible(this, false);
    }

    private void LateUpdate()
    {
        if (isOpen)
        {
            UpdateRepair(Time.deltaTime);
            return;
        }

        DialogueUI ui = DialogueUI.Instance;
        if (ui == null) return;
        if (!StoryAccess.Allows(this, story) || playerRig == null || !playerRig.CanInteract ||
            (HasChapterJobs && fridge != null && fridge.IsPickingUpBeer) ||
            controlLocker == null || ui.IsDialogueOpen || !TryGetAimedJob(out Job job))
        {
            ui.SetPromptVisible(this, false);
            return;
        }

        bool available = GetResult(job) == RepairResult.Unfinished;
        ui.SetPromptVisible(this, true, available ? $"E - {JobName(job)}" : ResultText(job));
        if (available && Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.Escape))
            BeginRepair(job);
    }

    private bool TryGetAimedJob(out Job job)
    {
        job = Job.Sink;
        if (!playerRig.UsesCenteredInteractions) return IsAimingAtSink();
        if (!InteractionAim.TryCreate(playerRig, interactionReach, out InteractionAim aim)) return false;
        if (AimHitsSink(aim)) return true;
        foreach (JobStation station in extraStations)
        {
            if (station.job == Job.Power && (chapter == null || chapter.Day < FatherBlackout.PowerOutageDay)) continue;
            if (!aim.Sees(station.geometry) && !aim.HitsTrigger(station.target)) continue;
            job = station.job;
            return true;
        }
        return false;
    }

    private bool AimHitsSink(InteractionAim aim) =>
        aim.Sees(sinkGeometry) || (aim.Ray.direction.y < 0f && aim.HitsTrigger(aimTarget));

    private bool IsAimingAtSink()
    {
        // The open basin has no solid surface at the rim for a downward ray to hit.
        if (playerRig.UsesCenteredInteractions)
            return InteractionAim.TryCreate(playerRig, interactionReach, out InteractionAim aim) && AimHitsSink(aim);

        Camera view = playerRig.ActiveCamera;
        if (view == null || aimTarget == null || !aimTarget.enabled) return false;
        var ray = new Ray(view.transform.position, view.transform.forward);

        // Collider.Raycast misses when the camera has already moved inside the trigger.
        if (aimTarget.ClosestPoint(ray.origin) == ray.origin)
            return Vector3.Dot(ray.direction, aimTarget.bounds.center - ray.origin) > 0f;

        if (!aimTarget.Raycast(ray, out RaycastHit target, interactionReach)) return false;

        // The sink's solid cabinet and basin can sit just in front of the interaction trigger.
        return !Physics.Raycast(ray, out RaycastHit obstacle, target.distance,
                   Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
               (sinkGeometry != null && obstacle.collider.transform.IsChildOf(sinkGeometry));
    }

    private void BeginRepair(Job job)
    {
        ActiveJob = job;
        // Snapshot the modifier once per attempt.
        difficulty = Mathf.Min(fridge != null ? fridge.BeersDrunk : 0, SweepSpeeds.Length - 1);
        halfWindow = TargetHalfWidths[difficulty];
        sweepSpeed = SweepSpeeds[difficulty];
        feedbackTime = 0f;
        EnsureOverlay();
        heading.text = JobName(job);
        ResetRound();
        feedback.text = ProgressText();
        DialogueUI.Instance.SetPromptVisible(this, false);
        controlLocker.AcquireLock();
        isOpen = true;
        overlay.gameObject.SetActive(true);
    }

    private void UpdateRepair(float deltaTime)
    {
        // Settings owns Escape and its own control lock. Resume the same timing state after it closes.
        if (AudioSettingsOverlay.IsOpen || Input.GetKeyDown(KeyCode.Escape)) return;
        if (GetResult(ActiveJob) != RepairResult.Unfinished)
        {
            feedbackTime -= deltaTime;
            if (feedbackTime <= 0f)
            {
                CloseRepair();
                RepairCompleted?.Invoke();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            CloseRepair();
            return;
        }

        if (feedbackTime > 0f)
        {
            feedbackTime -= deltaTime;
            if (feedbackTime <= 0f) feedback.text = ProgressText();
            return;
        }

        // Judge the position the player actually saw on the previous frame.
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryHit(Mathf.PingPong(sweepTime, 1f));
            return;
        }

        sweepTime += deltaTime * sweepSpeed;
        MoveMarker(Mathf.PingPong(sweepTime, 1f));
    }

    private void TryHit(float position)
    {
        if (Mathf.Abs(position - TargetCenters[hits]) > halfWindow)
        {
            misses++;
            if (misses == RoundLimit)
            {
                ResetRound();
                feedback.text = $"{RoundLimit} misses. Starting over.";
                feedbackTime = RestartFeedbackTime;
            }
            else
            {
                feedback.text = ProgressText();
                feedbackTime = HitFeedbackTime;
            }
            return;
        }

        hits++;
        if (hits == RoundLimit)
        {
            results[(int)ActiveJob] = FinishResult();
            CompletedJobs++;
            feedback.text = ResultText(ActiveJob);
            feedbackTime = 1.7f;
            return;
        }

        feedback.text = ProgressText();
        feedbackTime = HitFeedbackTime;
        RefreshTarget();
    }

    private string ProgressText() => $"Hits {hits}/{RoundLimit}   ·   Misses {misses}/{RoundLimit}";

    private void ResetRound()
    {
        hits = 0;
        misses = 0;
        sweepTime = 0f;
        RefreshTarget();
        MoveMarker(0f);
    }

    internal RepairResult GetResult(Job job) => results[(int)job];

    private RepairResult FinishResult()
    {
        if (!HasChapterJobs)
            return difficulty == 0 ? RepairResult.Fixed : difficulty == 1 ? RepairResult.SlowDrip : RepairResult.Leaking;

        if (ActiveJob != Job.Sink) return RepairResult.Fixed;
        return difficulty switch
        {
            0 => RepairResult.FixedAndDishes,
            1 => RepairResult.Fixed,
            2 => RepairResult.Leaking,
            _ => RepairResult.BarelyDrains
        };
    }

    internal static string JobName(Job job) => job switch
    {
        Job.Sink => "Fix the sink",
        Job.Dinner => "Cook dinner",
        Job.Power => "Restore the power",
        Job.Bike => "Repair the bike",
        _ => string.Empty
    };

    internal string ResultText(Job job)
    {
        if (GetResult(job) == RepairResult.Unfinished) return string.Empty;
        return job switch
        {
            Job.Dinner => "Dinner is ready.",
            Job.Power => "The lights are back on.",
            Job.Bike => "The bike is working again.",
            _ => SinkResultText()
        };
    }

    internal string BuildChecklist(bool showPower)
    {
        string sink = Result switch
        {
            RepairResult.FixedAndDishes => "<s>Fix the sink</s>\n<size=72%>Dishes washed.</size>",
            RepairResult.Fixed => "<s>Fix the sink</s>\n<size=72%>Dishes still dirty.</size>",
            RepairResult.Leaking or RepairResult.BarelyDrains or RepairResult.SlowDrip =>
                $"Fix the sink\n<size=72%>{SinkResultText()}</size>",
            _ => JobName(Job.Sink)
        };
        string power = showPower ? $"\n{ChecklistLine(Job.Power)}" : string.Empty;
        if (showPower && GetResult(Job.Power) == RepairResult.Unfinished)
            power += "\n<size=72%>Fuse box out back.</size>";
        return $"{sink}\n{ChecklistLine(Job.Dinner)}{power}\n{ChecklistLine(Job.Bike)}";
    }

    private string ChecklistLine(Job job) =>
        GetResult(job) == RepairResult.Unfinished ? JobName(job) : $"<s>{JobName(job)}</s>";

    private string SinkResultText() => Result switch
    {
        RepairResult.FixedAndDishes => "The sink drains. The dishes are clean.",
        RepairResult.Fixed => HasChapterJobs ? "The sink drains. The dishes are still dirty." : "The sink drains properly.",
        RepairResult.SlowDrip => "The sink drains, but it drips.",
        RepairResult.Leaking => "The sink drains, but water leaks below.",
        RepairResult.BarelyDrains => "The sink barely drains and still leaks.",
        _ => string.Empty
    };

    private void CloseRepair()
    {
        if (!isOpen) return;
        isOpen = false;
        if (overlay != null) overlay.gameObject.SetActive(false);
        if (controlLocker != null) controlLocker.ReleaseLock();
    }

    private void OnDisable()
    {
        CloseRepair();
        if (DialogueUI.Instance != null) DialogueUI.Instance.SetPromptVisible(this, false);
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.gameObject);
    }

    private void RefreshTarget()
    {
        float center = TargetCenters[hits];
        targetZone.anchorMin = new Vector2(center - halfWindow, 0f);
        targetZone.anchorMax = new Vector2(center + halfWindow, 1f);
    }

    private void MoveMarker(float position)
    {
        marker.anchorMin = new Vector2(position, 0f);
        marker.anchorMax = new Vector2(position, 1f);
    }

    private void EnsureOverlay()
    {
        if (overlay != null) return;
        var root = new GameObject("Father Timing Minigame", typeof(Canvas), typeof(CanvasScaler));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        overlay = root.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0f;

        RectTransform panel = MakeImage(root.transform, "Panel", new Color32(29, 24, 20, 244));
        panel.sizeDelta = new Vector2(540f, 176f);
        heading = MakeLabel(panel, string.Empty, 29f, 58f);
        RectTransform track = MakeImage(panel, "Timing track", new Color32(65, 57, 48, 255));
        track.sizeDelta = new Vector2(442f, 20f);
        track.anchoredPosition = new Vector2(0f, 17f);
        targetZone = MakeImage(track, "Target zone", Amber);
        marker = MakeImage(track, "Marker", Ivory);
        marker.sizeDelta = new Vector2(5f, 8f);
        feedback = MakeLabel(panel, string.Empty, 23f, -22f);
        MakeLabel(panel, "E in the amber band   ·   Q to leave", 21f, -59f);
    }

    private static RectTransform MakeImage(Transform parent, string name, Color32 color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = Vector2.zero;
        return image.rectTransform;
    }

    private static TextMeshProUGUI MakeLabel(Transform parent, string message, float size, float y)
    {
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.rectTransform.sizeDelta = new Vector2(505f, 32f);
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = size;
        label.color = Ivory;
        label.text = message;
        label.raycastTarget = false;
        return label;
    }
}
