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
    [SerializeField] private Light[] houseLights = System.Array.Empty<Light>();
    [SerializeField] private GameObject daytimeBackdrop;
    [SerializeField] private Transform waterMark;
    [SerializeField] private Material sinkWaterMaterial;
    [SerializeField] private Transform sinkRepairShot;
    [SerializeField] private Transform leftSinkDoor;
    [SerializeField] private Transform rightSinkDoor;
    [SerializeField] private Transform repairWrench;
    [SerializeField] private Transform pipeFitting;
    [SerializeField] private Sprite ghostSprite;
    [SerializeField] private Material drinkPropMaterial;
    [SerializeField] private Transform fridgeVodkaBottle;
    [SerializeField] private Transform counterCup;
    [SerializeField] private GameObject phonePrefab;
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
    internal bool FastForward { get; set; }

    private Transform heldBeer;
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
    private Transform phone;
    private TMP_Text phoneReadout;
    private Volume visionVolume;
    private VolumeProfile visionProfile;
    private UniversalAdditionalCameraData cameraData;
    private bool originalPostProcessing;
    private LayerMask originalVolumeMask;
    private Vignette vignette;
    private DepthOfField blur;
    private LensDistortion distortion;
    private float distortionStrength;
    private float distortionTime;
    private bool[] daytimeLightStates;
    private bool daytimeBackdropActive;
    private Camera nightCamera;
    private CameraClearFlags daytimeClearFlags;
    private Color daytimeBackground;
    private Light moonlight;

    internal bool OwnsDialogue(DialogueData dialogue) =>
        dialogue == firstRepairReaction || dialogue == beforeShot || dialogue == ghostArrival ||
        dialogue == firstPinReply || dialogue == ghostConfrontation;

    internal IEnumerator TakeVodka(Transform view)
    {
        bottle = TakeSceneProp(fridgeVodkaBottle, "Opening Vodka Bottle");
        yield return LiftToView(bottle, view, new Vector3(.22f, -.13f, .61f), Quaternion.identity);
    }

    internal IEnumerator TakeCup(Transform view)
    {
        cup = TakeSceneProp(counterCup, "Opening Cup");
        yield return LiftToView(cup, view, new Vector3(.08f, -.152f, .57f), Quaternion.identity);
    }

    private static Transform TakeSceneProp(Transform source, string name)
    {
        if (source == null || !source.gameObject.activeInHierarchy) return null;
        Transform copy = Instantiate(source, source.position, source.rotation);
        copy.name = name;
        copy.localScale = source.lossyScale;
        source.gameObject.SetActive(false);
        return copy;
    }

    internal IEnumerator TakeBeer(Transform source, Transform view)
    {
        heldBeer = Instantiate(source.gameObject, source.position, source.rotation).transform;
        heldBeer.name = "Opening Beer in Hand";
        heldBeer.localScale = source.lossyScale;
        heldBeer.gameObject.SetActive(true);
        yield return LiftToView(heldBeer, view, new Vector3(.24f, -.17f, .66f), Quaternion.Euler(-90f, 0f, 0f));
    }

    private IEnumerator LiftToView(Transform prop, Transform view, Vector3 targetPosition, Quaternion targetRotation)
    {
        if (prop == null) yield break;
        prop.SetParent(view, true);
        foreach (Collider collider in prop.GetComponentsInChildren<Collider>())
            collider.enabled = false;
        foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>())
            renderer.shadowCastingMode = ShadowCastingMode.Off;

        Vector3 start = prop.localPosition;
        Quaternion rotation = prop.localRotation;
        const float duration = .55f;
        for (float elapsed = 0f; elapsed < duration && StepTime != float.MaxValue; elapsed += StepTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            prop.SetLocalPositionAndRotation(Vector3.Lerp(start, targetPosition, progress),
                Quaternion.Slerp(rotation, targetRotation, progress));
            yield return null;
        }
        prop.SetLocalPositionAndRotation(targetPosition, targetRotation);
    }

    internal IEnumerator PourAndDrink(Transform view)
    {
        if (bottle == null || cup == null) yield break;
        drinkLevel = Cylinder(view, "Vodka in Cup", cup.localPosition + Vector3.up * .05f,
            new Vector3(.065f, .001f, .065f), new Color(.75f, .83f, .86f));
        drinkLevel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        drinkLevel.gameObject.SetActive(false);
        Vector3 bottleRest = bottle.localPosition;
        Vector3 pouringPosition = new Vector3(.21f, -.03f, .57f);
        for (float elapsed = 0f; elapsed < 1.7f && StepTime != float.MaxValue; elapsed += StepTime)
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
        Quaternion headRest = view.rotation;
        for (float elapsed = 0f; elapsed < DrinkTiltDuration && StepTime != float.MaxValue; elapsed += StepTime)
        {
            float lift = Mathf.Clamp01(elapsed / .5f);
            cup.localPosition = Vector3.Lerp(rest, raised, Mathf.SmoothStep(0f, 1f, lift));
            drinkLevel.localPosition = cup.localPosition + liquidOffset;
            if (elapsed >= .65f) drinkLevel.gameObject.SetActive(false);
            view.rotation = headRest * DrinkTilt(elapsed);
            yield return null;
        }
        view.rotation = headRest;
        ClearDrinkProps();
    }

    private const float DrinkTiltDuration = 1.1f;

    internal IEnumerator DrinkBeer(Transform view)
    {
        Quaternion rest = view.rotation;
        Vector3 bottleRest = heldBeer != null ? heldBeer.localPosition : Vector3.zero;
        Quaternion bottleRotation = heldBeer != null ? heldBeer.localRotation : Quaternion.identity;
        for (float elapsed = 0f; elapsed < DrinkTiltDuration && StepTime != float.MaxValue; elapsed += StepTime)
        {
            view.rotation = rest * DrinkTilt(elapsed);
            if (heldBeer != null)
            {
                float lift = Mathf.SmoothStep(0f, 1f,
                    Mathf.Min(Mathf.InverseLerp(0f, .42f, elapsed),
                        1f - Mathf.InverseLerp(.72f, DrinkTiltDuration, elapsed)));
                heldBeer.SetLocalPositionAndRotation(
                    Vector3.Lerp(bottleRest, new Vector3(.19f, -.12f, .57f), lift),
                    Quaternion.Slerp(bottleRotation, Quaternion.Euler(-128f, -18f, 35f), lift));
            }
            yield return null;
        }
        view.rotation = rest;
        if (heldBeer != null) Destroy(heldBeer.gameObject);
        heldBeer = null;
    }

    private static Quaternion DrinkTilt(float elapsed)
    {
        float up = Mathf.InverseLerp(.12f, .48f, elapsed);
        float down = 1f - Mathf.InverseLerp(.69f, DrinkTiltDuration, elapsed);
        return Quaternion.Euler(-Mathf.SmoothStep(0f, 31f, Mathf.Min(up, down)), 0f, 0f);
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
        if (sinkWaterMaterial != null)
            sinkWater.GetComponent<MeshRenderer>().sharedMaterial = sinkWaterMaterial;
    }

    internal void DrainSink()
    {
        if (sinkWater == null) return;
        sinkWater.SetActive(false);
        Destroy(sinkWater);
        sinkWater = null;
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
        for (float elapsed = 0f; elapsed < duration && StepTime != float.MaxValue; elapsed += StepTime)
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
        for (float elapsed = 0f; elapsed < duration && StepTime != float.MaxValue; elapsed += StepTime)
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
        for (float elapsed = 0f; elapsed < duration && StepTime != float.MaxValue; elapsed += StepTime)
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
            distortion = visionProfile.Add<LensDistortion>(true);
        }
        var target = VisionForDrinks(drinkCount);
        vignette.intensity.value = target.darkness;
        blur.gaussianStart.value = target.blurStart;
        blur.gaussianEnd.value = target.blurEnd;
        blur.gaussianMaxRadius.value = target.blurRadius;
        distortionStrength = DistortionForDrinks(drinkCount);
        if (distortionStrength == 0f) distortion.intensity.value = 0f;
    }

    internal IEnumerator EaseIntoVision(Camera camera, int drinkCount)
    {
        if (visionVolume == null) SetVision(camera, drinkCount - 1);

        var target = VisionForDrinks(drinkCount);
        float startingDarkness = vignette.intensity.value;
        float startingBlurStart = blur.gaussianStart.value;
        float startingBlurEnd = blur.gaussianEnd.value;
        float startingBlurRadius = blur.gaussianMaxRadius.value;
        float startingDistortion = distortionStrength;
        float targetDistortion = DistortionForDrinks(drinkCount);
        const float duration = 1.8f;
        for (float elapsed = 0f; elapsed < duration && StepTime != float.MaxValue; elapsed += StepTime)
        {
            float blend = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            vignette.intensity.value = Mathf.Lerp(startingDarkness, target.darkness, blend);
            blur.gaussianStart.value = Mathf.Lerp(startingBlurStart, target.blurStart, blend);
            blur.gaussianEnd.value = Mathf.Lerp(startingBlurEnd, target.blurEnd, blend);
            blur.gaussianMaxRadius.value = Mathf.Lerp(startingBlurRadius, target.blurRadius, blend);
            distortionStrength = Mathf.Lerp(startingDistortion, targetDistortion, blend);
            yield return null;
        }
        SetVision(camera, drinkCount);
    }

    private void LateUpdate()
    {
        if (distortion == null || distortionStrength <= 0f || AudioSettingsOverlay.IsOpen) return;
        distortionTime += Time.unscaledDeltaTime;
        float drift = distortionTime * .48f;
        distortion.center.value = new Vector2(
            Mathf.Lerp(.28f, .72f, Mathf.PerlinNoise(drift, 1.3f)),
            Mathf.Lerp(.28f, .72f, Mathf.PerlinNoise(4.1f, drift)));
        float pulse = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(distortionTime * .66f, 1f));
        distortion.intensity.value = -distortionStrength * Mathf.Lerp(.6f, 1f, pulse);
    }

    private static float DistortionForDrinks(int drinkCount) =>
        drinkCount >= FatherBlackout.BlackoutBeerCount ? .25f : drinkCount == 3 ? .17f : 0f;

    internal void SetGameplayDistortion(Camera camera, int drinkCount)
    {
        if (DistortionForDrinks(drinkCount) == 0f)
        {
            ClearVision();
            return;
        }

        SetVision(camera, drinkCount);
        vignette.active = false;
        blur.active = false;
    }

    private static (float darkness, float blurStart, float blurEnd, float blurRadius) VisionForDrinks(int drinkCount) =>
        drinkCount switch
        {
            >= 4 => (.47f, .75f, 3f, .9f),
            3 => (.33f, 1.8f, 6f, .55f),
            _ => (.22f, 3f, 6f, .35f)
        };

    internal void SetNight(Camera camera)
    {
        if (visionVolume == null) SetVision(camera, FatherBlackout.BlackoutBeerCount);
        distortionStrength = DistortionForDrinks(3);
        daytimeLightStates = new bool[houseLights.Length];
        for (int i = 0; i < houseLights.Length; i++)
        {
            Light houseLight = houseLights[i];
            if (houseLight == null) continue;
            daytimeLightStates[i] = houseLight.enabled;
            houseLight.enabled = false;
        }
        if (daytimeBackdrop != null)
        {
            daytimeBackdropActive = daytimeBackdrop.activeSelf;
            daytimeBackdrop.SetActive(false);
        }
        nightCamera = camera;
        daytimeClearFlags = camera.clearFlags;
        daytimeBackground = camera.backgroundColor;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.018f, .025f, .05f);

        moonlight = new GameObject("Opening Moonlight").AddComponent<Light>();
        moonlight.transform.SetParent(transform, false);
        moonlight.transform.position = ghostMark.position + new Vector3(-1f, 2.1f, 1.2f);
        moonlight.type = LightType.Point;
        moonlight.color = new Color(.51f, .64f, .9f);
        moonlight.intensity = .85f;
        moonlight.range = 8f;

        var nightColor = visionProfile.Add<ColorAdjustments>(true);
        nightColor.postExposure.value = -1.15f;
        nightColor.colorFilter.value = new Color(.72f, .8f, 1f);
    }

    private void RestoreDaylight()
    {
        if (daytimeLightStates == null) return;
        for (int i = 0; i < houseLights.Length; i++)
        {
            if (houseLights[i] != null) houseLights[i].enabled = daytimeLightStates[i];
        }
        daytimeLightStates = null;
        if (daytimeBackdrop != null) daytimeBackdrop.SetActive(daytimeBackdropActive);
        if (nightCamera != null)
        {
            nightCamera.clearFlags = daytimeClearFlags;
            nightCamera.backgroundColor = daytimeBackground;
            nightCamera = null;
        }
        if (moonlight != null)
        {
            moonlight.enabled = false;
            Destroy(moonlight.gameObject);
        }
    }

    internal void PositionForCollapse(Transform view)
    {
        Vector3 forward = Vector3.ProjectOnPlane(couchShot.forward, Vector3.up).normalized;
        view.position = couchShot.position + Vector3.up * .6f + forward * .85f;
        view.rotation = Quaternion.LookRotation(-forward + Vector3.down * .45f, Vector3.up);
    }

    internal IEnumerator CollapseOntoCouch(Transform view)
    {
        Vector3 end = couchShot.position + Vector3.down * .28f;
        Vector3 start = view.position;
        Quaternion approach = view.rotation;
        Quaternion rest = couchShot.rotation * Quaternion.Euler(10f, 0f, 13f);
        const float duration = 1.55f;
        for (float elapsed = 0f; elapsed < duration && StepTime != float.MaxValue; elapsed += StepTime)
        {
            float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .85f, elapsed));
            float slump = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.4f, duration, elapsed));
            Vector3 position = Vector3.Lerp(start, end, reach);
            position.y = Mathf.Lerp(start.y, end.y, slump);
            view.SetPositionAndRotation(position, Quaternion.Slerp(approach, rest, slump));
            yield return null;
        }
        view.SetPositionAndRotation(end, rest);
    }

    internal IEnumerator WakeOnCouch(Transform view)
    {
        Vector3 start = view.position;
        Quaternion rest = view.rotation;
        const float duration = .8f;
        for (float elapsed = 0f; elapsed < duration && StepTime != float.MaxValue; elapsed += StepTime)
        {
            float rise = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            view.SetPositionAndRotation(Vector3.Lerp(start, couchShot.position, rise),
                Quaternion.Slerp(rest, couchShot.rotation, rise));
            yield return null;
        }
        view.SetPositionAndRotation(couchShot.position, couchShot.rotation);
    }

    internal IEnumerator RevealGhost()
    {
        if (ghostMark == null || ghostSprite == null) yield break;
        ghost = new GameObject("Father's Night Ghost");
        ghost.transform.SetParent(ghostMark, false);
        ghost.transform.rotation = couchShot.rotation;
        SpriteRenderer sprite = ghost.AddComponent<SpriteRenderer>();
        sprite.sprite = ghostSprite;
        float scale = 1.55f / ghostSprite.bounds.size.y;
        ghost.transform.localScale = Vector3.one * scale;
        Vector3 retreat = Vector3.ProjectOnPlane(couchShot.forward, Vector3.up).normalized * .55f;
        for (float elapsed = 0f; elapsed < .9f && StepTime != float.MaxValue; elapsed += StepTime)
        {
            float approach = Mathf.SmoothStep(0f, 1f, elapsed / .9f);
            ghost.transform.position = ghostMark.position + retreat * (1f - approach);
            sprite.color = new Color(1f, 1f, 1f, approach);
            yield return null;
        }
        ghost.transform.position = ghostMark.position;
        sprite.color = Color.white;
    }

    internal IEnumerator ShowPhone(Transform view)
    {
        if (phonePrefab == null) yield break;
        phone = Instantiate(phonePrefab, view, false).transform;
        phone.name = "Father's Opening Phone";
        phone.localScale = Vector3.one * 1.65f;
        phone.SetLocalPositionAndRotation(new Vector3(.18f, -.40f, .62f), Quaternion.Euler(4f, 7f, -5f));
        phone.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var panel = new GameObject("Phone PIN Screen", typeof(RectTransform), typeof(Canvas), typeof(Image));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(phone, false);
        panelRect.localPosition = new Vector3(0f, 0f, -.0039f);
        panelRect.localScale = Vector3.one * .000285f;
        panelRect.sizeDelta = new Vector2(220f, 460f);
        phoneCanvas = panel.GetComponent<Canvas>();
        phoneCanvas.renderMode = RenderMode.WorldSpace;
        var screen = panel.GetComponent<Image>();
        screen.color = new Color32(41, 52, 54, 255);
        screen.raycastTarget = false;
        phoneReadout = new GameObject("PIN Readout", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        phoneReadout.transform.SetParent(screen.transform, false);
        phoneReadout.rectTransform.sizeDelta = panelRect.sizeDelta - new Vector2(12f, 18f);
        phoneReadout.fontSize = 30f;
        phoneReadout.alignment = TextAlignmentOptions.Center;
        phoneReadout.raycastTarget = false;

        Vector3 start = phone.localPosition;
        Vector3 held = new Vector3(.145f, -.035f, .61f);
        const float raiseTime = .32f;
        for (float elapsed = 0f; elapsed < raiseTime && StepTime != float.MaxValue; elapsed += StepTime)
        {
            phone.localPosition = Vector3.Lerp(start, held, Mathf.SmoothStep(0f, 1f, elapsed / raiseTime));
            yield return null;
        }
        phone.localPosition = held;
    }

    internal IEnumerator FailPin(float delay = 0f)
    {
        if (phoneReadout == null) yield break;
        if (delay > 0f) yield return Wait(delay);
        for (int digits = 0; digits <= 4; digits++)
        {
            phoneReadout.text = $"Enter PIN\n\n{new string('•', digits)}\n\n1    2    3\n4    5    6\n7    8    9\n0";
            yield return Wait(.2f);
        }
        phoneReadout.text = "<color=#FFB3A6>Incorrect PIN</color>";
        yield return Wait(.9f);
    }

    internal IEnumerator DropPhone(Transform view)
    {
        if (phone == null) yield break;
        Vector3 start = phone.position;
        Quaternion rotation = phone.rotation;
        Vector3 seatedPosition = view.position;
        Quaternion ghostView = view.rotation;
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        Vector3 drift = forward * .65f + Vector3.Cross(Vector3.up, forward) * .15f;
        phone.SetParent(transform, true);
        Vector3 floorProbe = start + drift * .4f;
        float floorHeight = Physics.Raycast(floorProbe, Vector3.down, out RaycastHit floor, 2f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? floor.point.y : start.y - 1f;
        Vector3 floorPosition = new Vector3(start.x, floorHeight + .008f, start.z);
        const float gravity = 9.81f;
        float fallTime = Mathf.Sqrt(2f * Mathf.Max(.02f, start.y - floorPosition.y) / gravity);
        Vector3 landing = floorPosition + drift * fallTime;
        // Lean over the couch so its seat does not hide the phone on the floor.
        Vector3 leaningPosition = seatedPosition + forward * .34f + Vector3.up * .07f;
        Quaternion floorView = Quaternion.LookRotation(landing - leaningPosition);

        for (float elapsed = 0f; elapsed < fallTime && StepTime != float.MaxValue; elapsed += StepTime)
        {
            float progress = elapsed / fallTime;
            Vector3 position = start + drift * elapsed + Vector3.down * (.5f * gravity * elapsed * elapsed);
            phone.SetPositionAndRotation(position,
                rotation * Quaternion.Euler(24f * elapsed, -18f * elapsed, 38f * elapsed));
            float lean = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 eye = Vector3.Lerp(seatedPosition, leaningPosition, lean);
            view.SetPositionAndRotation(eye,
                Quaternion.Slerp(ghostView, Quaternion.LookRotation(position - eye), lean));
            yield return null;
        }
        phone.SetPositionAndRotation(landing, Quaternion.LookRotation(Vector3.down, forward));
        view.SetPositionAndRotation(leaningPosition, floorView);
        yield return Wait(.65f);
        phoneCanvas.gameObject.SetActive(false);

        const float lookBackTime = .48f;
        for (float elapsed = 0f; elapsed < lookBackTime && StepTime != float.MaxValue; elapsed += StepTime)
        {
            float blend = Mathf.SmoothStep(0f, 1f, elapsed / lookBackTime);
            view.SetPositionAndRotation(Vector3.Lerp(leaningPosition, seatedPosition, blend),
                Quaternion.Slerp(floorView, ghostView, blend));
            yield return null;
        }
        view.SetPositionAndRotation(seatedPosition, ghostView);
    }

    internal void Clear()
    {
        RestoreDaylight();
        RestoreWrench();
        CloseSinkDoors();
        ClearDrinkProps();
        if (couch != null) { couch.SetActive(false); Destroy(couch); }
        if (ghost != null) { ghost.SetActive(false); Destroy(ghost); }
        if (phone != null) { phone.gameObject.SetActive(false); Destroy(phone.gameObject); }
        ClearVision();
    }

    private void ClearDrinkProps()
    {
        if (heldBeer != null) Destroy(heldBeer.gameObject);
        PutBackSceneProp(bottle, fridgeVodkaBottle);
        PutBackSceneProp(cup, counterCup);
        if (drinkLevel != null) Destroy(drinkLevel.gameObject);
        heldBeer = bottle = cup = drinkLevel = null;
    }

    private static void PutBackSceneProp(Transform copy, Transform source)
    {
        if (copy == null) return;
        copy.gameObject.SetActive(false);
        Destroy(copy.gameObject);
        if (source != null) source.gameObject.SetActive(true);
    }

    internal void ClearVision()
    {
        if (visionVolume != null) { visionVolume.enabled = false; Destroy(visionVolume.gameObject); }
        if (visionProfile != null) Destroy(visionProfile);
        visionVolume = null;
        visionProfile = null;
        distortion = null;
        distortionStrength = 0f;
        distortionTime = 0f;
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = originalPostProcessing;
            cameraData.volumeLayerMask = originalVolumeMask;
            cameraData = null;
        }
    }

    private float StepTime => AudioSettingsOverlay.IsOpen ? 0f : FastForward ? float.MaxValue : Time.unscaledDeltaTime;

    private IEnumerator Wait(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds && StepTime != float.MaxValue; elapsed += StepTime) yield return null;
    }
}
