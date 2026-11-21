using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runs the father's placeholder sink repair and records its result for this scene visit.</summary>
public sealed class FatherSinkRepair : MonoBehaviour
{
    public enum RepairResult { Unfinished, Fixed, SlowDrip, Leaking, Skipped }

    [SerializeField] private StoryId story = StoryId.Father;
    [SerializeField] private StoryPlayerRig playerRig;
    [SerializeField] private PlayerControlLocker controlLocker;
    [SerializeField] private FatherFridgeInteractable fridge;
    [SerializeField] private Collider aimTarget;
    [SerializeField] private Transform sinkGeometry;
    [SerializeField, Min(0.1f)] private float interactionReach = 1.8f;

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
    private int difficulty;
    private int hits;
    private int misses;
    private float halfWindow;
    private float sweepTime;
    private float sweepSpeed;
    private float feedbackTime;
    private bool isOpen;
    private bool hasStarted;

    public RepairResult Result { get; private set; }
    public event Action RepairCompleted;
    private int RoundLimit => TargetCenters.Length - difficulty;

    public bool SkipStartedRepair()
    {
        if (!hasStarted || Result != RepairResult.Unfinished) return false;
        Result = RepairResult.Skipped;
        CloseRepair();
        if (DialogueUI.Instance != null) DialogueUI.Instance.SetPromptVisible(this, false);
        return true;
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
        bool canReach = StoryAccess.Allows(this, story) && playerRig != null && playerRig.CanInteract &&
            controlLocker != null && aimTarget != null && aimTarget.enabled && !ui.IsDialogueOpen && IsAimingAtSink();
        ui.SetPromptVisible(this, canReach, Result == RepairResult.Unfinished ? "E - Fix the sink" : ResultText());
        if (canReach && Result == RepairResult.Unfinished && Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.Escape))
            BeginRepair();
    }

    private bool IsAimingAtSink()
    {
        Camera view = playerRig.ActiveCamera;
        if (view == null) return false;
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

    private void BeginRepair()
    {
        // Snapshot the modifier once per attempt.
        hasStarted = true;
        difficulty = Mathf.Min(fridge != null ? fridge.BeersDrunk : 0, SweepSpeeds.Length - 1);
        halfWindow = TargetHalfWidths[difficulty];
        sweepSpeed = SweepSpeeds[difficulty];
        feedbackTime = 0f;
        EnsureOverlay();
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
        if (Result != RepairResult.Unfinished)
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
            Result = difficulty == 0 ? RepairResult.Fixed : difficulty == 1 ? RepairResult.SlowDrip : RepairResult.Leaking;
            feedback.text = ResultText();
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

    private string ResultText() => Result switch
    {
        RepairResult.Fixed => "The sink drains properly.",
        RepairResult.SlowDrip => "The sink drains, but it drips.",
        RepairResult.Leaking => "The sink drains, but water leaks below.",
        RepairResult.Skipped => "The sink was left unrepaired.",
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
        var root = new GameObject("Sink Repair Overlay", typeof(Canvas), typeof(CanvasScaler));
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
        MakeLabel(panel, "Fix the sink", 29f, 58f);
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
