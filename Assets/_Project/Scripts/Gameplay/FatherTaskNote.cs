using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows Father's task list on the fridge and in a close-up.</summary>
[DefaultExecutionOrder(50)]
internal sealed class FatherTaskNote : MonoBehaviour
{
    [SerializeField] private FatherBlackout chapter;
    [SerializeField] private FatherSinkRepair sink;
    [SerializeField] private StoryPlayerRig player;
    [SerializeField] private PlayerControlLocker controls;

    private const float Reach = 2f;
    private const float PaperScale = .00085f;
    private static readonly Vector2 PaperSize = new Vector2(320f, 336f);
    private static readonly Color PaperColor = new Color32(240, 212, 126, 255);
    private static readonly Color InkColor = new Color32(60, 45, 34, 255);

    private FatherFridgeInteractable fridge;
    private TextMeshProUGUI worldTasks;
    private TextMeshProUGUI closeTasks;
    private Canvas closeUp;
    private RectTransform closePaper;
    private int shownDay;
    private FatherSinkRepair.RepairResult shownResult;
    private bool inspecting;
    private float zoomTime;

    private void Start()
    {
        if (!StoryAccess.Allows(this, StoryId.Father)) return;
        fridge = GetComponentInParent<FatherFridgeInteractable>();
        var surface = new GameObject("Paper Surface", typeof(RectTransform), typeof(Canvas));
        surface.transform.SetParent(transform, false);
        surface.transform.localScale = Vector3.one * PaperScale;
        surface.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        worldTasks = BuildPaper(surface.transform, out _);
        RefreshTasks();
    }

    private void LateUpdate()
    {
        if (worldTasks == null) return;
        RefreshTasks();

        if (inspecting)
        {
            if (AudioSettingsOverlay.IsOpen || Input.GetKeyDown(KeyCode.Escape)) return;
            if (Input.GetKeyDown(KeyCode.R))
            {
                Close();
                return;
            }
            zoomTime += Time.unscaledDeltaTime;
            closePaper.localScale = Vector3.one * Mathf.SmoothStep(1.02f, 1.35f, zoomTime / .18f);
            return;
        }

        DialogueUI ui = DialogueUI.Instance;
        if (ui == null) return;
        bool canRead = player.CanInteract && !ui.IsDialogueOpen && !fridge.IsPickingUpBeer && IsAimingAtNote();
        ui.SetPromptVisible(this, canRead, "R - Read the task note");
        if (canRead && Input.GetKeyDown(KeyCode.R) && !Input.GetKeyDown(KeyCode.Escape))
            Open(ui);
    }

    private bool IsAimingAtNote()
    {
        Camera camera = player.ActiveCamera;
        if (camera == null) return false;

        var ray = new Ray(camera.transform.position, camera.transform.forward);
        if (Vector3.Dot(transform.forward, ray.direction) <= 0f ||
            !new Plane(-transform.forward, transform.position).Raycast(ray, out float distance) || distance > Reach)
            return false;

        Transform surface = worldTasks.transform.parent;
        Vector3 local = surface.InverseTransformPoint(ray.GetPoint(distance));
        if (Mathf.Abs(local.x) > PaperSize.x / 2f + 24f || Mathf.Abs(local.y) > PaperSize.y / 2f + 24f)
            return false;

        return !Physics.Raycast(ray, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private void RefreshTasks()
    {
        if (shownDay == chapter.Day && shownResult == sink.Result) return;
        shownDay = chapter.Day;
        shownResult = sink.Result;
        string sinkTask = shownResult switch
        {
            FatherSinkRepair.RepairResult.Fixed => "<s>Fix the sink</s>",
            FatherSinkRepair.RepairResult.SlowDrip => "Fix the sink\n<size=72%>It drains, but it drips.</size>",
            FatherSinkRepair.RepairResult.Leaking => "Fix the sink\n<size=72%>It drains, but leaks below.</size>",
            _ => "Fix the sink"
        };
        string electricity = shownDay >= 3 ? "\nRestore the power" : string.Empty;
        worldTasks.text = $"{sinkTask}\nCook a meal{electricity}\nFourth task (TBD)";
        if (closeTasks != null) closeTasks.text = worldTasks.text;
    }

    private void Open(DialogueUI ui)
    {
        if (closeUp == null)
        {
            var root = new GameObject("Task Note Close-up", typeof(Canvas), typeof(CanvasScaler));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            closeUp = root.GetComponent<Canvas>();
            closeUp.renderMode = RenderMode.ScreenSpaceOverlay;
            closeUp.sortingOrder = 110;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 1f;
            var shade = AddImage(root.transform, "Shade", new Color32(12, 10, 8, 190));
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            closeTasks = BuildPaper(root.transform, out closePaper);
            AddText(root.transform, "R - Put the note back", 20f, new Vector2(0f, -265f),
                new Vector2(320f, 36f), TextAlignmentOptions.Center, PaperColor);
        }

        closeTasks.text = worldTasks.text;
        zoomTime = 0f;
        closePaper.localScale = Vector3.one * 1.02f;
        ui.SetPromptVisible(this, false);
        controls.AcquireLock();
        inspecting = true;
        closeUp.gameObject.SetActive(true);
    }

    private void Close()
    {
        if (!inspecting) return;
        inspecting = false;
        closeUp.gameObject.SetActive(false);
        controls.ReleaseLock();
    }

    private void OnDisable()
    {
        Close();
        if (DialogueUI.Instance != null) DialogueUI.Instance.SetPromptVisible(this, false);
    }

    private void OnDestroy()
    {
        if (closeUp != null) Destroy(closeUp.gameObject);
    }

    private static TextMeshProUGUI BuildPaper(Transform parent, out RectTransform paper)
    {
        paper = AddImage(parent, "Yellow Sticky Note", PaperColor);
        paper.sizeDelta = PaperSize;
        AddText(paper, "Things to do", 31f, new Vector2(0f, 116f), new Vector2(268f, 42f),
            TextAlignmentOptions.Left, InkColor).fontStyle = FontStyles.Bold;
        var rule = AddImage(paper, "Rule", new Color32(145, 102, 51, 155));
        rule.sizeDelta = new Vector2(267f, 1f);
        rule.anchoredPosition = new Vector2(0f, 87f);
        return AddText(paper, string.Empty, 25f, new Vector2(0f, -27f), new Vector2(268f, 196f),
            TextAlignmentOptions.TopLeft, InkColor);
    }

    private static RectTransform AddImage(Transform parent, string title, Color color)
    {
        var image = new GameObject(title, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    private static TextMeshProUGUI AddText(Transform parent, string content, float size, Vector2 position,
        Vector2 dimensions, TextAlignmentOptions alignment, Color color)
    {
        var label = new GameObject("Note Text", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = dimensions;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.text = content;
        label.raycastTarget = false;
        return label;
    }
}
