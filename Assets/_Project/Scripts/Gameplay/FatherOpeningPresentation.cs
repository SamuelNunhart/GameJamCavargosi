using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Scene references and temporary presentation for Father's scripted first night.</summary>
internal sealed class FatherOpeningPresentation : MonoBehaviour
{
    [SerializeField] private Transform couchShot;
    [SerializeField] private Transform couchMark;
    [SerializeField] private Transform ghostMark;
    [SerializeField] private Transform waterMark;
    [SerializeField] private Transform sinkRepairShot;
    [SerializeField] private Transform leftSinkDoor;
    [SerializeField] private Transform rightSinkDoor;
    [SerializeField] private Transform repairWrench;
    [SerializeField] private Transform pipeFitting;
    [SerializeField] private Sprite ghostSprite;
    [SerializeField] private Material drinkPropMaterial;
    [SerializeField] private DialogueData firstRepairReaction;
    [SerializeField] private DialogueData beforeShot;
    [SerializeField] private DialogueData ghostArrival;
    [SerializeField] private DialogueData firstPinReply;
    [SerializeField] private DialogueData ghostConfrontation;

    internal Transform CouchShot => couchShot;
    internal Transform SinkRepairShot => sinkRepairShot;
    internal DialogueData FirstRepairReaction => firstRepairReaction;
    internal DialogueData BeforeShot => beforeShot;
    internal DialogueData GhostArrival => ghostArrival;
    internal DialogueData FirstPinReply => firstPinReply;
    internal DialogueData GhostConfrontation => ghostConfrontation;

    private GameObject heldDrink;
    private Transform bottle;
    private Transform cup;
    private Transform drinkLevel;
    private GameObject ghost;
    private GameObject couch;
    private GameObject sinkWater;
    private Quaternion leftDoorClosed;
    private Quaternion rightDoorClosed;
    private bool sinkDoorsMoved;
    private Pose wrenchOnShelf;
    private Quaternion wrenchSideRotation;
    private bool wrenchLifted;
    private Canvas phoneCanvas;
    private RectTransform phone;
    private TMP_Text phoneReadout;
    private Volume visionVolume;
    private VolumeProfile visionProfile;
    private UniversalAdditionalCameraData cameraData;
    private bool originalPostProcessing;
    private LayerMask originalVolumeMask;
    private Vignette vignette;
    private DepthOfField blur;

    internal bool OwnsDialogue(DialogueData dialogue) =>
        dialogue == firstRepairReaction || dialogue == beforeShot || dialogue == ghostArrival ||
        dialogue == firstPinReply || dialogue == ghostConfrontation;

    internal void ShowDrink(Transform view)
    {
        heldDrink = new GameObject("Opening Vodka and Cup");
        heldDrink.transform.SetParent(view, false);
        var bottleObject = new GameObject("Vodka Bottle");
        bottle = bottleObject.transform;
        bottle.SetParent(heldDrink.transform, false);
        bottle.localPosition = new Vector3(.22f, -.13f, .61f);
        Cylinder(bottle, "Bottle", Vector3.zero, new Vector3(.078f, .09f, .078f), new Color(.55f, .67f, .72f));
        Cylinder(bottle, "Label", Vector3.up * .01f, new Vector3(.079f, .034f, .079f), new Color(.62f, .19f, .17f));
        Cylinder(bottle, "Neck", Vector3.up * .106f, new Vector3(.034f, .025f, .034f), new Color(.55f, .67f, .72f));
        cup = Cylinder(heldDrink.transform, "Cup", new Vector3(.08f, -.152f, .57f),
            new Vector3(.082f, .046f, .082f), new Color(.34f, .45f, .50f));
        drinkLevel = Cylinder(heldDrink.transform, "Vodka in Cup", cup.localPosition + Vector3.up * .05f,
            new Vector3(.065f, .001f, .065f), new Color(.75f, .83f, .86f));
        drinkLevel.gameObject.SetActive(false);
    }

    internal IEnumerator PourAndDrink()
    {
        if (heldDrink == null) yield break;
        Vector3 bottleRest = bottle.localPosition;
        Vector3 pouringPosition = new Vector3(.21f, -.03f, .57f);
        for (float elapsed = 0f; elapsed < 1.7f; elapsed += StepTime)
        {
            float tilt = Mathf.Clamp01(Mathf.Min(elapsed / .38f, (1.7f - elapsed) / .35f));
            bottle.localPosition = Vector3.Lerp(bottleRest, pouringPosition, tilt);
            bottle.localRotation = Quaternion.Euler(0f, 0f, 112f * tilt);
            if (elapsed >= .85f) drinkLevel.gameObject.SetActive(true);
            yield return null;
        }
        bottle.gameObject.SetActive(false);
        Vector3 rest = cup.localPosition;
        Vector3 raised = new Vector3(.01f, -.055f, .32f);
        Vector3 liquidOffset = drinkLevel.localPosition - rest;
        for (float elapsed = 0f; elapsed < 1.1f; elapsed += StepTime)
        {
            float lift = Mathf.Clamp01(elapsed / .5f);
            cup.localPosition = Vector3.Lerp(rest, raised, Mathf.SmoothStep(0f, 1f, lift));
            drinkLevel.localPosition = cup.localPosition + liquidOffset;
            if (elapsed >= .65f) drinkLevel.gameObject.SetActive(false);
            yield return null;
        }
        if (heldDrink != null) Destroy(heldDrink);
        heldDrink = null;
    }

    private Transform Cylinder(Transform parent, string propName, Vector3 position, Vector3 scale, Color color) =>
        PlaceProp(PrimitiveType.Cylinder, parent, propName, position, scale, color);

    private Transform PlaceProp(PrimitiveType shape, Transform parent, string propName, Vector3 position,
        Vector3 scale, Color color)
    {
        GameObject prop = GameObject.CreatePrimitive(shape);
        prop.name = propName;
        prop.transform.SetParent(parent, false);
        prop.transform.localPosition = position;
        prop.transform.localScale = scale;
        if (prop.TryGetComponent(out Collider collider))
        {
            collider.enabled = false;
            Destroy(collider);
        }
        MeshRenderer renderer = prop.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = drinkPropMaterial;
        if (heldDrink != null && prop.transform.IsChildOf(heldDrink.transform))
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(properties);
        return prop.transform;
    }

    internal void ShowCloggedSink()
    {
        if (waterMark == null || sinkWater != null) return;
        sinkWater = PlaceProp(PrimitiveType.Quad, waterMark, "Standing Sink Water", Vector3.zero, Vector3.one,
            new Color(.22f, .39f, .41f)).gameObject;
    }

    internal IEnumerator OpenSinkDoors()
    {
        if (leftSinkDoor == null || rightSinkDoor == null) yield break;
        leftDoorClosed = leftSinkDoor.localRotation;
        rightDoorClosed = rightSinkDoor.localRotation;
        sinkDoorsMoved = true;
        Vector3 leftHinge = leftSinkDoor.parent.InverseTransformDirection(Vector3.up);
        Vector3 rightHinge = rightSinkDoor.parent.InverseTransformDirection(Vector3.up);
        const float duration = .65f;
        for (float elapsed = 0f; elapsed < duration; elapsed += StepTime)
        {
            float angle = Mathf.SmoothStep(0f, 98f, elapsed / duration);
            leftSinkDoor.localRotation = Quaternion.AngleAxis(-angle, leftHinge) * leftDoorClosed;
            rightSinkDoor.localRotation = Quaternion.AngleAxis(angle, rightHinge) * rightDoorClosed;
            yield return null;
        }
        leftSinkDoor.localRotation = Quaternion.AngleAxis(-98f, leftHinge) * leftDoorClosed;
        rightSinkDoor.localRotation = Quaternion.AngleAxis(98f, rightHinge) * rightDoorClosed;
    }

    internal void CloseSinkDoors()
    {
        if (!sinkDoorsMoved) return;
        if (leftSinkDoor != null) leftSinkDoor.localRotation = leftDoorClosed;
        if (rightSinkDoor != null) rightSinkDoor.localRotation = rightDoorClosed;
        sinkDoorsMoved = false;
    }

    internal IEnumerator TryRepairWithWrench(bool firstAttempt)
    {
        if (repairWrench == null || pipeFitting == null) yield break;

        wrenchOnShelf = new Pose(repairWrench.localPosition, repairWrench.localRotation);
        Quaternion upright = Quaternion.LookRotation(repairWrench.forward, Vector3.up);
        wrenchSideRotation = Quaternion.Inverse(upright) * repairWrench.rotation;
        wrenchLifted = true;
        Pose fitted = WrenchAtFitting(0f);
        var lifted = new Pose(repairWrench.position + Vector3.up * .32f - sinkRepairShot.forward * .12f,
            Quaternion.Slerp(repairWrench.rotation, fitted.rotation, .45f));
        yield return MoveWrench(lifted, .8f);
        yield return MoveWrench(fitted, .75f);
        yield return Wait(.35f);

        yield return PullWrench(-38f, .9f, .32f);
        yield return Wait(.25f);
        yield return PullWrench(-44f, 1.05f, .45f);
        if (firstAttempt)
        {
            yield return Wait(.25f);
            yield return PullWrench(-50f, 1.1f, .38f);
        }

        yield return MoveWrench(lifted, .55f);
        Transform shelf = repairWrench.parent;
        var rest = new Pose(shelf.TransformPoint(wrenchOnShelf.position), shelf.rotation * wrenchOnShelf.rotation);
        yield return MoveWrench(rest, .65f);
        RestoreWrench();
        yield return Wait(.35f);
    }

    private IEnumerator PullWrench(float angle, float duration, float hold)
    {
        yield return TurnWrench(0f, angle, duration);
        yield return Wait(hold);
        yield return TurnWrench(angle, 0f, .4f);
    }

    private IEnumerator MoveWrench(Pose target, float duration)
    {
        var start = new Pose(repairWrench.position, repairWrench.rotation);
        for (float elapsed = 0f; elapsed < duration; elapsed += StepTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            repairWrench.SetPositionAndRotation(Vector3.Lerp(start.position, target.position, progress),
                Quaternion.Slerp(start.rotation, target.rotation, progress));
            yield return null;
        }
        repairWrench.SetPositionAndRotation(target.position, target.rotation);
    }

    private IEnumerator TurnWrench(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += StepTime)
        {
            Pose pose = WrenchAtFitting(Mathf.SmoothStep(from, to, elapsed / duration));
            repairWrench.SetPositionAndRotation(pose.position, pose.rotation);
            yield return null;
        }
        Pose end = WrenchAtFitting(to);
        repairWrench.SetPositionAndRotation(end.position, end.rotation);
    }

    private Pose WrenchAtFitting(float angle)
    {
        // The wrench model's origin is on the handle. Keep its jaw centered on the pipe during the turn.
        Quaternion rotation = pipeFitting.rotation *
            Quaternion.Euler(-angle * .3f, angle, 0f) * wrenchSideRotation;
        Vector3 jawFromOrigin = Vector3.Scale(new Vector3(0f, .025f, .189f), repairWrench.lossyScale);
        return new Pose(pipeFitting.position - rotation * jawFromOrigin, rotation);
    }

    private void RestoreWrench()
    {
        if (!wrenchLifted) return;
        if (repairWrench != null)
            repairWrench.SetLocalPositionAndRotation(wrenchOnShelf.position, wrenchOnShelf.rotation);
        wrenchLifted = false;
    }

    internal void ShowCouch()
    {
        if (couchMark == null) return;
        couch = new GameObject("Opening Couch");
        couch.transform.SetParent(couchMark, false);
        var fabric = new Color(.29f, .19f, .15f);
        var cushions = new Color(.37f, .26f, .2f);
        PlaceProp(PrimitiveType.Cube, couch.transform, "Base", new Vector3(0f, .22f, 0f),
            new Vector3(2.2f, .44f, .9f), fabric);
        PlaceProp(PrimitiveType.Cube, couch.transform, "Back", new Vector3(0f, .55f, -.38f),
            new Vector3(2.2f, .8f, .21f), fabric);
        for (int side = -1; side <= 1; side += 2)
        {
            PlaceProp(PrimitiveType.Cube, couch.transform, "Arm", new Vector3(side, .48f, .01f),
                new Vector3(.24f, .36f, .86f), fabric);
            PlaceProp(PrimitiveType.Cube, couch.transform, "Seat", new Vector3(side * .44f, .5f, .02f),
                new Vector3(.86f, .14f, .7f), cushions);
        }
    }

    internal void SetVision(Camera camera, int drinkCount)
    {
        if (visionVolume == null)
        {
            cameraData = camera.GetUniversalAdditionalCameraData();
            originalPostProcessing = cameraData.renderPostProcessing;
            originalVolumeMask = cameraData.volumeLayerMask;
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = originalVolumeMask.value | (1 << gameObject.layer);
            var volumeObject = new GameObject("Opening Alcohol Vision");
            volumeObject.layer = gameObject.layer;
            volumeObject.transform.SetParent(transform, false);
            visionVolume = volumeObject.AddComponent<Volume>();
            visionVolume.isGlobal = true;
            visionVolume.priority = 50;
            visionProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            visionVolume.sharedProfile = visionProfile;
            vignette = visionProfile.Add<Vignette>(true);
            vignette.color.value = Color.black;
            vignette.smoothness.value = .65f;
            blur = visionProfile.Add<DepthOfField>(true);
            blur.mode.value = DepthOfFieldMode.Gaussian;
        }
        vignette.intensity.value = drinkCount >= 4 ? .47f : drinkCount == 3 ? .33f : .22f;
        blur.gaussianStart.value = drinkCount >= 4 ? .75f : drinkCount == 3 ? 1.8f : 3f;
        blur.gaussianEnd.value = drinkCount >= 4 ? 3f : 6f;
        blur.gaussianMaxRadius.value = drinkCount >= 4 ? .9f : drinkCount == 3 ? .55f : .35f;
    }

    internal void SetNight(Camera camera, int drinkCount)
    {
        SetVision(camera, drinkCount);
        var nightColor = visionProfile.Add<ColorAdjustments>(true);
        nightColor.postExposure.value = -.65f;
        nightColor.colorFilter.value = new Color(.83f, .89f, 1f);
    }

    internal IEnumerator Sway(Transform view, float degrees = 2f)
    {
        Quaternion rest = view.rotation;
        for (float elapsed = 0f; elapsed < 1.2f; elapsed += StepTime)
        {
            view.rotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed / 1.2f * Mathf.PI * 2f) * degrees);
            yield return null;
        }
        view.rotation = rest;
    }

    internal void PositionForCollapse(Transform view)
    {
        Vector3 forward = Vector3.ProjectOnPlane(couchShot.forward, Vector3.up).normalized;
        view.position = couchShot.position + Vector3.up * .6f + forward * .85f;
        view.rotation = Quaternion.LookRotation(-forward + Vector3.down * .45f, Vector3.up);
    }

    internal IEnumerator CollapseOntoCouch(Transform view)
    {
        Vector3 end = couchShot.position;
        Vector3 start = view.position;
        Quaternion approach = view.rotation;
        Quaternion rest = couchShot.rotation;
        for (float elapsed = 0f; elapsed < 1.7f; elapsed += StepTime)
        {
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1.45f, elapsed));
            view.position = Vector3.Lerp(start, end, fall);
            view.rotation = Quaternion.Slerp(approach, rest, fall) *
                Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 5f) * (1f - fall) * 9f);
            yield return null;
        }
        view.SetPositionAndRotation(end, rest);
    }

    internal IEnumerator RevealGhost(Transform view)
    {
        if (ghostMark == null || ghostSprite == null) yield break;
        ghost = new GameObject("Father's Night Ghost");
        ghost.transform.SetParent(ghostMark, false);
        ghost.transform.rotation = view.rotation;
        SpriteRenderer sprite = ghost.AddComponent<SpriteRenderer>();
        sprite.sprite = ghostSprite;
        float scale = 1.55f / ghostSprite.bounds.size.y;
        ghost.transform.localScale = Vector3.one * scale;
        Vector3 retreat = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized * .55f;
        for (float elapsed = 0f; elapsed < .9f; elapsed += StepTime)
        {
            float approach = Mathf.SmoothStep(0f, 1f, elapsed / .9f);
            ghost.transform.position = ghostMark.position + retreat * (1f - approach);
            sprite.color = new Color(1f, 1f, 1f, approach);
            yield return null;
        }
        ghost.transform.position = ghostMark.position;
        sprite.color = Color.white;
    }

    internal void ShowPhone()
    {
        var panel = new GameObject("Father's Phone", typeof(Canvas), typeof(CanvasScaler));
        panel.transform.SetParent(transform, false);
        phoneCanvas = panel.GetComponent<Canvas>();
        phoneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        phoneCanvas.sortingOrder = 90;
        var scaler = panel.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        var backing = new GameObject("Phone Body", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        backing.transform.SetParent(panel.transform, false);
        backing.color = new Color32(17, 18, 20, 255);
        backing.raycastTarget = false;
        phone = backing.rectTransform;
        phone.sizeDelta = new Vector2(158f, 224f);
        phone.anchoredPosition = new Vector2(0f, -48f);
        phone.localRotation = Quaternion.Euler(0f, 0f, -6f);
        var screen = new GameObject("Screen", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        screen.transform.SetParent(phone, false);
        screen.rectTransform.sizeDelta = new Vector2(142f, 204f);
        screen.color = new Color32(41, 52, 54, 255);
        screen.raycastTarget = false;
        phoneReadout = new GameObject("PIN Readout", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        phoneReadout.transform.SetParent(screen.transform, false);
        phoneReadout.rectTransform.sizeDelta = screen.rectTransform.sizeDelta;
        phoneReadout.fontSize = 18f;
        phoneReadout.alignment = TextAlignmentOptions.Center;
        phoneReadout.raycastTarget = false;
    }

    internal IEnumerator FailPin()
    {
        if (phoneReadout == null) yield break;
        for (int digits = 0; digits <= 4; digits++)
        {
            phoneReadout.text = $"Enter PIN\n\n{new string('•', digits)}\n\n1    2    3\n4    5    6\n7    8    9\n0";
            yield return Wait(.2f);
        }
        phoneReadout.text = "<color=#FFB3A6>Incorrect PIN</color>";
        yield return Wait(.9f);
    }

    internal IEnumerator DropPhone()
    {
        if (phone == null) yield break;
        Vector2 start = phone.anchoredPosition;
        for (float elapsed = 0f; elapsed < .48f; elapsed += StepTime)
        {
            float fall = elapsed / .48f;
            phone.anchoredPosition = start + new Vector2(90f * fall, -520f * fall * fall);
            phone.localRotation = Quaternion.Euler(0f, 0f, -6f - 32f * fall);
            yield return null;
        }
        phoneCanvas.gameObject.SetActive(false);
    }

    internal void Clear()
    {
        RestoreWrench();
        CloseSinkDoors();
        if (heldDrink != null) Destroy(heldDrink);
        if (sinkWater != null) { sinkWater.SetActive(false); Destroy(sinkWater); }
        if (couch != null) { couch.SetActive(false); Destroy(couch); }
        if (ghost != null) { ghost.SetActive(false); Destroy(ghost); }
        if (phoneCanvas != null) { phoneCanvas.gameObject.SetActive(false); Destroy(phoneCanvas.gameObject); }
        if (visionVolume != null) { visionVolume.enabled = false; Destroy(visionVolume.gameObject); }
        if (visionProfile != null) Destroy(visionProfile);
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = originalPostProcessing;
            cameraData.volumeLayerMask = originalVolumeMask;
            cameraData = null;
        }
    }

    private static float StepTime => AudioSettingsOverlay.IsOpen ? 0f : Time.unscaledDeltaTime;

    private static IEnumerator Wait(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += StepTime) yield return null;
    }
}
