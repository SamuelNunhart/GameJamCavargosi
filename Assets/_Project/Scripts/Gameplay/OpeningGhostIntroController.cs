using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OpeningGhostIntroController : MonoBehaviour
{
    private const float DefaultGhostFadeInDuration = 0.45f;
    private const float DefaultPhonePullDownDuration = 0.3f;

    [Header("Dialogue")]
    [SerializeField] private List<DialogueData> introDialogueSegments = new List<DialogueData>();
    [SerializeField] private int ghostRevealAfterSegmentIndex;

    [Header("Player")]
    [SerializeField] private PlayerControlLocker playerControlLocker;
    [SerializeField] private List<Behaviour> fallbackBehavioursToDisable = new List<Behaviour>();

    [Header("Camera")]
    [SerializeField] private Camera gameplayCamera;
    
    [Header("Wake Up Lighting")]
    [SerializeField] private Light wakeUpLightToEnable;
    [SerializeField] private string wakeUpLightToEnableObjectName = "Point.002";
    [SerializeField] private Light wakeUpLightToBrighten;
    [SerializeField] private string wakeUpLightToBrightenObjectName = "Point Light";
    [SerializeField] private float wakeUpLightTargetIntensity = 30f;

    [Header("Timing")]
    [SerializeField] private float disturbancePause = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float ghostMoveDuration = 1.6f;
    [SerializeField] private float ghostFadeInDuration = 0.45f;

    [Header("Auto Run")]
    [SerializeField] private bool playOnStart = true;

    [Header("Scene Objects")]
    [SerializeField] private Camera introCamera;
    [SerializeField] private Transform ghostSpawnPoint;
    [SerializeField] private Transform ghostApproachPoint;
    [SerializeField] private GameObject ghostObject;
    [SerializeField] private GameObject introPhoneObject;
    [SerializeField] private Transform phoneEndPoint;
    [SerializeField] private CanvasGroup blackoutCanvasGroup;
    [SerializeField] private Graphic blackoutGraphic;

    [Header("Intro Phone Beat")]
    [SerializeField] private int phonePullDownTriggerSegmentIndex;
    [SerializeField] private int phonePullDownTriggerLineIndex = 1;
    [SerializeField] private float phonePullDownDuration = DefaultPhonePullDownDuration;
    [SerializeField] private float phonePullDownStartDelay = 0.05f;

    [Header("Scene 2 Progression")]
    [SerializeField] private DialogueData sceneTwoIntroDialogue;
    [SerializeField] private DialogueData sceneTwoWindowDialogue;
    [SerializeField] private Collider windowDialogueTrigger;
    [SerializeField] private WindowSequenceController gatedWindowSequence;
    [SerializeField] private bool lockPlayerControlsDuringSceneTwoIntroDialogue;
    [SerializeField] private bool lockPlayerControlsDuringSceneTwoWindowDialogue;
    [SerializeField] private string sceneTwoIntroDialogueResourcePath = "Dialogue/Scene2_AfterIntro";
    [SerializeField] private string sceneTwoWindowDialogueResourcePath = "Dialogue/Scene2_WindowTrigger";

    [Header("Scene 3 Progression")]
    [SerializeField] private DialogueData sceneThreePaintingDialogue;
    [SerializeField] private Collider sceneThreePaintingTrigger;
    [SerializeField] private Collider sceneThreePuzzleDialogueTrigger;
    [SerializeField] private PuzzleOpener gatedPuzzleOpener;
    [SerializeField] private PuzzleEventDirector puzzleEventDirector;
    [SerializeField] private bool lockPlayerControlsDuringSceneThreePaintingDialogue;
    [SerializeField] private string sceneThreePaintingDialogueResourcePath = "Dialogue/Scene3_PaintingTrigger";
    [SerializeField] private string sceneThreePaintingTriggerObjectName = "PaintingDialogueTrigger";
    [SerializeField] private string sceneThreePuzzleDialogueTriggerObjectName = "PuzzleDialogueTrigger";

    [Header("Scene 4 Progression")]
    [SerializeField] private DoorTriggerRotator gatedDoorTriggerRotator;
    [SerializeField] private DoorDialogueTrigger gatedDoorDialogueTrigger;
    [SerializeField] private FamilyMinigamePlaceholderController gatedFamilyMinigame;

    private Coroutine introRoutine;
    private Coroutine ghostMoveRoutine;
    private Coroutine ghostFadeRoutine;
    private Coroutine introPhoneBeatRoutine;
    private Coroutine windowTriggerDialogueRoutine;
    private Coroutine sceneThreePaintingDialogueRoutine;
    private PlayerTriggerRelay windowDialogueTriggerRelay;
    private PlayerTriggerRelay sceneThreePaintingTriggerRelay;
    private PlayerTriggerRelay sceneThreePuzzleDialogueTriggerRelay;
    private SpriteRenderer[] ghostSpriteRenderers = Array.Empty<SpriteRenderer>();
    private int fallbackLockCount;
    private bool sequenceFinished;
    private bool ghostRevealed;
    private bool waitingForWindowDialogueTrigger;
    private bool waitingForSceneThreePaintingTrigger;
    private bool waitingForSceneThreePuzzleDialogueTrigger;
    private bool windowDialogueTriggered;
    private bool sceneThreeCompletionHandled;
    private bool sceneThreePaintingDialogueTriggered;
    private bool sceneThreePaintingDialogueCompleted;
    private bool sceneThreePuzzleDialogueTriggered;
    private bool sceneThreeMissingTriggerLogged;
    private bool sceneFourDoorUnlocked;
    private bool sceneFourFamilyUnlocked;
    private bool introPhonePoseCached;
    private bool introPhoneBeatCompleted;
    private Vector3 introPhoneInitialPosition;
    private Quaternion introPhoneInitialRotation;
    private GameObject cachedIntroPhonePoseSource;

    public bool HasCompletedPaintingDialogue => sceneThreePaintingDialogueCompleted;

    public event Action PaintingDialogueCompleted;

    private void Reset()
    {
        AutoAssignSceneReferences();
    }

    private void OnValidate()
    {
        AutoAssignSceneReferences();

        if (ghostRevealAfterSegmentIndex < 0)
            ghostRevealAfterSegmentIndex = 0;

        if (phonePullDownTriggerSegmentIndex < 0)
            phonePullDownTriggerSegmentIndex = 0;

        if (phonePullDownTriggerLineIndex < 0)
            phonePullDownTriggerLineIndex = 0;

        if (ghostFadeInDuration < 0f)
            ghostFadeInDuration = 0f;

        if (phonePullDownDuration <= 0f)
            phonePullDownDuration = DefaultPhonePullDownDuration;

        if (phonePullDownStartDelay < 0f)
            phonePullDownStartDelay = 0f;
    }

    private void Awake()
    {
        AutoAssignSceneReferences();
        EnsureWindowDialogueTriggerRelay();
        EnsureSceneThreePaintingTriggerRelay();
        EnsureSceneThreePuzzleDialogueTriggerRelay();
        EnsureBlackoutReady();
        HideGhostImmediate();
        SetCameraState(introCamera, true);
        SetCameraState(gameplayCamera, false);

        if (ShouldGateWindowSequence())
            gatedWindowSequence.SetInteractionEnabled(false);

        if (ShouldGatePuzzleSequence())
            gatedPuzzleOpener.SetInteractionEnabled(false);

        if (ShouldGateDoorSequence())
            SetDoorSequenceInteractionEnabled(false);

        if (ShouldGateFamilyMinigame())
            gatedFamilyMinigame.SetInteractionEnabled(false);
    }

    private void Start()
    {
        if (playOnStart)
            introRoutine = StartCoroutine(RunIntroSequence());
    }

    private void OnEnable()
    {
        EnsureWindowDialogueTriggerRelay();
        EnsureSceneThreePaintingTriggerRelay();
        EnsureSceneThreePuzzleDialogueTriggerRelay();
        SubscribeToWindowDialogueTrigger();
        SubscribeToSceneThreePaintingTrigger();
        SubscribeToSceneThreePuzzleDialogueTrigger();
        SubscribeToWindowSequenceCompletion();
        SubscribeToPuzzleSequenceCompletion();
        SubscribeToDoorDialogueCompletion();

        if (gatedWindowSequence != null &&
            gatedWindowSequence.HasFinishedCompletionFlow &&
            !sceneThreeCompletionHandled)
            HandleWindowSequenceCompleted();

        if (sceneThreePaintingDialogueCompleted)
            ContinueSceneThreeToPuzzleSequence();

        if (puzzleEventDirector != null && puzzleEventDirector.HasCompletedPostPuzzleFlow)
            UnlockDoorSequence();

        if (gatedDoorDialogueTrigger != null && gatedDoorDialogueTrigger.HasPlayedDialogue)
            UnlockFamilyMinigame();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        UnsubscribeFromWindowDialogueTrigger();
        UnsubscribeFromSceneThreePaintingTrigger();
        UnsubscribeFromSceneThreePuzzleDialogueTrigger();
        UnsubscribeFromWindowSequenceCompletion();
        UnsubscribeFromPuzzleSequenceCompletion();
        UnsubscribeFromDoorDialogueCompletion();

        if (introRoutine != null)
            StopCoroutine(introRoutine);

        if (ghostMoveRoutine != null)
            StopCoroutine(ghostMoveRoutine);

        if (ghostFadeRoutine != null)
            StopCoroutine(ghostFadeRoutine);

        if (introPhoneBeatRoutine != null)
            StopCoroutine(introPhoneBeatRoutine);

        if (windowTriggerDialogueRoutine != null)
            StopCoroutine(windowTriggerDialogueRoutine);

        if (sceneThreePaintingDialogueRoutine != null)
            StopCoroutine(sceneThreePaintingDialogueRoutine);

        introRoutine = null;
        ghostMoveRoutine = null;
        ghostFadeRoutine = null;
        introPhoneBeatRoutine = null;
        windowTriggerDialogueRoutine = null;
        sceneThreePaintingDialogueRoutine = null;
        waitingForWindowDialogueTrigger = false;
        waitingForSceneThreePaintingTrigger = false;
        waitingForSceneThreePuzzleDialogueTrigger = false;
        HideIntroPhoneImmediate();

        if (puzzleEventDirector == null || !puzzleEventDirector.HasPlayedPuzzleIntro)
            sceneThreePuzzleDialogueTriggered = false;

        if (!windowDialogueTriggered)
            gatedWindowSequence?.SetInteractionEnabled(true);

        if (sequenceFinished)
            return;

        SetBlackoutAlpha(0f);
        SetCameraState(introCamera, false);
        SetCameraState(gameplayCamera, true);
        ReleaseControlLock();
    }

    public void PlayIntro()
    {
        if (introRoutine != null || sequenceFinished)
            return;

        introRoutine = StartCoroutine(RunIntroSequence());
    }

    public void RevealGhostNow()
    {
        RevealGhostAtSpawnPoint();
    }

    public void MoveGhostToApproachPoint()
    {
        if (ghostObject == null || ghostApproachPoint == null)
            return;

        if (!ghostRevealed)
            RevealGhostAtSpawnPoint();

        if (ghostMoveRoutine != null)
            StopCoroutine(ghostMoveRoutine);

        ghostMoveRoutine = StartCoroutine(MoveGhostRoutine());
    }

    private IEnumerator RunIntroSequence()
    {
        AcquireControlLock();
        yield return null;
        PrepareIntroPhoneForSequence();

        if (DialogueUI.Instance == null)
        {
            Debug.LogWarning("OpeningGhostIntroController could not find DialogueUI.Instance.");
            yield return SwapToGameplayAndRestoreControl();
            yield break;
        }

        bool revealTriggered = false;

        for (int i = 0; i < introDialogueSegments.Count; i++)
        {
            DialogueData segment = introDialogueSegments[i];

            if (segment != null)
                yield return PlayDialogueSegment(segment, false, i == phonePullDownTriggerSegmentIndex);

            if (!revealTriggered && i >= ghostRevealAfterSegmentIndex)
            {
                yield return RunRevealBeat();
                revealTriggered = true;
            }
        }

        if (!revealTriggered)
            yield return RunRevealBeat();

        yield return FadeToBlack(fadeOutDuration);
        HideGhostImmediate();
        HideIntroPhoneImmediate();
        SetCameraState(introCamera, false);
        SetCameraState(gameplayCamera, true);
        ApplyWakeUpLightingTransition();
        yield return FadeFromBlack(fadeInDuration);

        ReleaseControlLock();

        if (sceneTwoIntroDialogue != null)
            yield return PlayDialogueSegment(sceneTwoIntroDialogue, lockPlayerControlsDuringSceneTwoIntroDialogue);

        BeginWaitingForWindowDialogueTrigger();
        sequenceFinished = true;
        introRoutine = null;
    }

    private IEnumerator PlayDialogueSegment(
        DialogueData dialogue,
        bool lockPlayerControls = false,
        bool allowIntroPhoneBeat = false)
    {
        bool finished = false;
        bool started = DialogueUI.Instance.StartDialogue(dialogue, lockPlayerControls, () => finished = true);

        if (!started)
            yield break;

        bool introPhoneBeatHandled = !allowIntroPhoneBeat || !ShouldRunIntroPhoneBeatFor(dialogue);

        while (!finished)
        {
            if (!introPhoneBeatHandled && ShouldTriggerIntroPhoneBeat(dialogue))
            {
                introPhoneBeatHandled = true;
                StartIntroPhoneBeat();
            }

            yield return null;
        }
    }

    private void BeginWaitingForWindowDialogueTrigger()
    {
        EnsureWindowDialogueTriggerRelay();

        if (sceneTwoWindowDialogue == null || windowDialogueTrigger == null || windowDialogueTriggerRelay == null)
        {
            gatedWindowSequence?.SetInteractionEnabled(true);
            waitingForWindowDialogueTrigger = false;
            return;
        }

        windowDialogueTriggerRelay.ResetTriggerState();

        if (gatedWindowSequence != null)
            gatedWindowSequence.SetInteractionEnabled(false);

        waitingForWindowDialogueTrigger = true;
    }

    private IEnumerator PlayWindowDialogueAndUnlockSequence()
    {
        if (sceneTwoWindowDialogue != null)
            yield return PlayDialogueSegment(sceneTwoWindowDialogue, lockPlayerControlsDuringSceneTwoWindowDialogue);

        gatedWindowSequence?.SetInteractionEnabled(true);
        windowTriggerDialogueRoutine = null;
    }

    private IEnumerator RunRevealBeat()
    {
        if (ghostRevealed)
            yield break;

        if (disturbancePause > 0f)
            yield return new WaitForSeconds(disturbancePause);

        RevealGhostAtSpawnPoint(startHidden: true);
        StartGhostFadeIn();
        MoveGhostToApproachPoint();
    }

    private void RevealGhostAtSpawnPoint(bool startHidden = false)
    {
        if (ghostObject == null)
            return;

        if (ghostSpawnPoint != null)
        {
            ghostObject.transform.SetPositionAndRotation(
                ghostSpawnPoint.position,
                ghostSpawnPoint.rotation
            );
        }

        StopGhostFadeRoutine();
        ghostObject.SetActive(true);
        SetGhostAlpha(startHidden ? 0f : 1f);
        ghostRevealed = true;
    }

    private IEnumerator MoveGhostRoutine()
    {
        if (ghostObject == null || ghostApproachPoint == null)
            yield break;

        Transform ghostTransform = ghostObject.transform;
        Vector3 startPosition = ghostTransform.position;
        Quaternion startRotation = ghostTransform.rotation;
        Vector3 endPosition = ghostApproachPoint.position;
        Quaternion endRotation = ghostApproachPoint.rotation;

        if (ghostMoveDuration <= 0f)
        {
            ghostTransform.SetPositionAndRotation(endPosition, endRotation);
            ghostMoveRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < ghostMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / ghostMoveDuration);

            ghostTransform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, endPosition, t),
                Quaternion.Slerp(startRotation, endRotation, t)
            );

            yield return null;
        }

        ghostTransform.SetPositionAndRotation(endPosition, endRotation);
        ghostMoveRoutine = null;
    }

    private IEnumerator FadeToBlack(float duration)
    {
        yield return FadeBlackout(0f, 1f, duration);
    }

    private IEnumerator FadeFromBlack(float duration)
    {
        yield return FadeBlackout(1f, 0f, duration);
    }

    private IEnumerator FadeBlackout(float from, float to, float duration)
    {
        if (blackoutCanvasGroup == null && blackoutGraphic == null)
            yield break;

        SetBlackoutAlpha(from);

        if (duration <= 0f)
        {
            SetBlackoutAlpha(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetBlackoutAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetBlackoutAlpha(to);
    }

    private IEnumerator SwapToGameplayAndRestoreControl()
    {
        SetCameraState(introCamera, false);
        SetCameraState(gameplayCamera, true);
        ApplyWakeUpLightingTransition();
        HideIntroPhoneImmediate();
        ReleaseControlLock();
        gatedWindowSequence?.SetInteractionEnabled(true);
        sequenceFinished = true;
        introRoutine = null;
        yield break;
    }

    private void AcquireControlLock()
    {
        if (playerControlLocker != null)
        {
            playerControlLocker.AcquireLock();
            return;
        }

        TryPopulateFallbackBehaviours();

        if (fallbackBehavioursToDisable.Count == 0)
        {
            Debug.LogWarning("OpeningGhostIntroController has no PlayerControlLocker or fallback behaviours assigned.");
            return;
        }

        fallbackLockCount++;
        ApplyFallbackLockState();
    }

    private void ReleaseControlLock()
    {
        if (playerControlLocker != null)
        {
            playerControlLocker.ReleaseLock();
            return;
        }

        if (fallbackLockCount == 0)
            return;

        fallbackLockCount--;
        ApplyFallbackLockState();
    }

    private void ApplyFallbackLockState()
    {
        bool shouldEnable = fallbackLockCount == 0;

        for (int i = 0; i < fallbackBehavioursToDisable.Count; i++)
        {
            Behaviour behaviour = fallbackBehavioursToDisable[i];

            if (behaviour != null)
                behaviour.enabled = shouldEnable;
        }
    }

    private void SetCameraState(Camera targetCamera, bool isActive)
    {
        if (targetCamera == null)
            return;

        targetCamera.enabled = isActive;

        AudioListener audioListener = targetCamera.GetComponent<AudioListener>();
        if (audioListener != null)
            audioListener.enabled = isActive;
    }

    private void ApplyWakeUpLightingTransition()
    {
        AutoAssignWakeUpLighting();

        if (wakeUpLightToEnable != null)
        {
            if (!wakeUpLightToEnable.gameObject.activeSelf)
                wakeUpLightToEnable.gameObject.SetActive(true);

            wakeUpLightToEnable.enabled = true;
        }

        if (wakeUpLightToBrighten != null)
            wakeUpLightToBrighten.intensity = wakeUpLightTargetIntensity;
    }

    private void HideGhostImmediate()
    {
        StopGhostFadeRoutine();
        ghostRevealed = false;
        SetGhostAlpha(0f);

        if (ghostObject != null)
            ghostObject.SetActive(false);
    }

    private void CacheGhostRenderers()
    {
        if (ghostObject == null)
        {
            ghostSpriteRenderers = Array.Empty<SpriteRenderer>();
            return;
        }

        ghostSpriteRenderers = ghostObject.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void StartGhostFadeIn()
    {
        if (ghostObject == null)
            return;

        StopGhostFadeRoutine();

        ghostFadeRoutine = StartCoroutine(FadeGhostRoutine(GetGhostFadeInDuration()));
    }

    private void StopGhostFadeRoutine()
    {
        if (ghostFadeRoutine == null)
            return;

        StopCoroutine(ghostFadeRoutine);
        ghostFadeRoutine = null;
    }

    private IEnumerator FadeGhostRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetGhostAlpha(elapsed / duration);
            yield return null;
        }

        SetGhostAlpha(1f);
        ghostFadeRoutine = null;
    }

    private float GetGhostFadeInDuration()
    {
        return ghostFadeInDuration > 0f ? ghostFadeInDuration : DefaultGhostFadeInDuration;
    }

    private void SetGhostAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (ghostSpriteRenderers == null || ghostSpriteRenderers.Length == 0)
            CacheGhostRenderers();

        for (int i = 0; i < ghostSpriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = ghostSpriteRenderers[i];

            if (spriteRenderer == null)
                continue;

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }

    private void EnsureBlackoutReady()
    {
        if (blackoutGraphic != null)
        {
            Color color = blackoutGraphic.color;
            color.r = 0f;
            color.g = 0f;
            color.b = 0f;
            color.a = 1f;
            blackoutGraphic.color = color;
        }

        SetBlackoutAlpha(0f);
    }

    private void SetBlackoutAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (blackoutCanvasGroup != null)
        {
            blackoutCanvasGroup.alpha = alpha;
            blackoutCanvasGroup.interactable = alpha > 0f;
            blackoutCanvasGroup.blocksRaycasts = alpha > 0f;
        }
        else if (blackoutGraphic != null)
        {
            Color color = blackoutGraphic.color;
            color.a = alpha;
            blackoutGraphic.color = color;
        }
    }

    private void AutoAssignSceneReferences()
    {
        Transform introCameraTransform = transform.Find("IntroCamera");
        if (introCamera == null && introCameraTransform != null)
            introCamera = introCameraTransform.GetComponent<Camera>();

        if (ghostSpawnPoint == null)
            ghostSpawnPoint = transform.Find("GhostSpawnPoint");

        if (ghostApproachPoint == null)
            ghostApproachPoint = transform.Find("GhostApproachPoint");

        if (ghostObject == null)
        {
            Transform ghostTransform = transform.Find("Ghost");
            if (ghostTransform != null)
                ghostObject = ghostTransform.gameObject;
        }

        Transform blackoutCanvasTransform = transform.Find("BlackoutCanvas");
        Transform blackTransform = blackoutCanvasTransform != null
            ? blackoutCanvasTransform.Find("Black")
            : null;

        if (blackoutCanvasGroup == null)
        {
            if (blackTransform != null)
                blackoutCanvasGroup = blackTransform.GetComponent<CanvasGroup>();

            if (blackoutCanvasGroup == null && blackoutCanvasTransform != null)
                blackoutCanvasGroup = blackoutCanvasTransform.GetComponent<CanvasGroup>();
        }

        if (blackoutGraphic == null)
        {
            if (blackTransform != null)
                blackoutGraphic = blackTransform.GetComponent<Graphic>();

            if (blackoutGraphic == null && blackoutCanvasTransform != null)
                blackoutGraphic = blackoutCanvasTransform.GetComponent<Graphic>();
        }

        if (gameplayCamera == null && Camera.main != null && Camera.main != introCamera)
            gameplayCamera = Camera.main;

        if (gameplayCamera == null)
        {
            Camera[] cameras = FindObjectsOfType<Camera>(true);

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cameraCandidate = cameras[i];

                if (cameraCandidate != null && cameraCandidate != introCamera)
                {
                    gameplayCamera = cameraCandidate;
                    break;
                }
            }
        }

        AutoAssignWakeUpLighting();
        CacheGhostRenderers();
        AutoAssignSceneTwoReferences();
        AutoAssignIntroPhoneReferences();
    }

    private void AutoAssignWakeUpLighting()
    {
        if (wakeUpLightToEnable == null)
            wakeUpLightToEnable = FindLightInScene(wakeUpLightToEnableObjectName);

        if (wakeUpLightToBrighten == null)
        {
            Vector3? referencePosition = gameplayCamera != null
                ? gameplayCamera.transform.position
                : null;

            wakeUpLightToBrighten = FindLightInScene(
                wakeUpLightToBrightenObjectName,
                referencePosition,
                transform
            );
        }
    }

    private void AutoAssignIntroPhoneReferences()
    {
        if (phoneEndPoint == null)
            phoneEndPoint = transform.Find("PhoneEndPoint");

        GameObject sceneIntroPhoneObject = FindSceneIntroPhoneObject();

        if (sceneIntroPhoneObject != null &&
            (introPhoneObject == null ||
             (gatedPuzzleOpener != null && introPhoneObject == gatedPuzzleOpener.PhoneWorldObject)))
        {
            introPhoneObject = sceneIntroPhoneObject;
        }

        if (introPhoneObject == null && gatedPuzzleOpener != null)
            introPhoneObject = gatedPuzzleOpener.PhoneWorldObject;

        CacheIntroPhonePose();
    }

    private GameObject FindSceneIntroPhoneObject()
    {
        Transform namedPhoneTransform = transform.Find("phone2k_InBed");

        if (namedPhoneTransform != null)
            return namedPhoneTransform.gameObject;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child == null)
                continue;

            string childName = child.name;

            if (string.IsNullOrWhiteSpace(childName))
                continue;

            string normalizedName = childName.ToLowerInvariant();

            if (!normalizedName.Contains("phone") ||
                normalizedName.Contains("endpoint") ||
                normalizedName.Contains("viewpoint") ||
                normalizedName.Contains("prompt") ||
                normalizedName.Contains("text"))
            {
                continue;
            }

            if (child.GetComponentInChildren<Renderer>(true) != null)
                return child.gameObject;
        }

        return null;
    }

    private void TryPopulateFallbackBehaviours()
    {
        if (fallbackBehavioursToDisable.Count > 0)
            return;

        Behaviour[] behaviours = FindObjectsOfType<Behaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];

            if (behaviour == null || behaviour == this)
                continue;

            string typeName = behaviour.GetType().Name;

            if (typeName == "PlayerMove" || typeName == "PlayerLook" || typeName == "PlayerController")
                fallbackBehavioursToDisable.Add(behaviour);
        }
    }

    private void AutoAssignSceneTwoReferences()
    {
        if (sceneTwoIntroDialogue == null && !string.IsNullOrWhiteSpace(sceneTwoIntroDialogueResourcePath))
            sceneTwoIntroDialogue = Resources.Load<DialogueData>(sceneTwoIntroDialogueResourcePath);

        if (sceneTwoWindowDialogue == null && !string.IsNullOrWhiteSpace(sceneTwoWindowDialogueResourcePath))
            sceneTwoWindowDialogue = Resources.Load<DialogueData>(sceneTwoWindowDialogueResourcePath);

        if (windowDialogueTrigger == null)
        {
            Transform triggerTransform = FindTransformInScene("WindowDialogueTrigger");
            if (triggerTransform != null)
                windowDialogueTrigger = triggerTransform.GetComponent<Collider>();
        }

        if (gatedWindowSequence == null)
        {
            WindowSequenceController[] windowSequences = FindObjectsOfType<WindowSequenceController>(true);
            if (windowSequences.Length > 0)
                gatedWindowSequence = windowSequences[0];
        }

        AutoAssignSceneThreeReferences();
    }

    private void AutoAssignSceneThreeReferences()
    {
        if (sceneThreePaintingDialogue == null && !string.IsNullOrWhiteSpace(sceneThreePaintingDialogueResourcePath))
            sceneThreePaintingDialogue = Resources.Load<DialogueData>(sceneThreePaintingDialogueResourcePath);

        if (sceneThreePaintingTrigger == null && !string.IsNullOrWhiteSpace(sceneThreePaintingTriggerObjectName))
        {
            Transform triggerTransform = FindTransformInScene(sceneThreePaintingTriggerObjectName);
            if (triggerTransform != null)
                sceneThreePaintingTrigger = triggerTransform.GetComponent<Collider>();
        }

        if (sceneThreePuzzleDialogueTrigger == null)
        {
            string triggerObjectName = string.IsNullOrWhiteSpace(sceneThreePuzzleDialogueTriggerObjectName)
                ? "PuzzleDialogueTrigger"
                : sceneThreePuzzleDialogueTriggerObjectName;
            Transform triggerTransform = FindTransformInScene(triggerObjectName);
            if (triggerTransform != null)
                sceneThreePuzzleDialogueTrigger = triggerTransform.GetComponent<Collider>();
        }

        AutoAssignSceneFourReferences();
    }

    private void AutoAssignSceneFourReferences()
    {
        if (gatedPuzzleOpener == null)
        {
            PuzzleOpener[] puzzleOpeners = FindObjectsOfType<PuzzleOpener>(true);
            if (puzzleOpeners.Length > 0)
                gatedPuzzleOpener = puzzleOpeners[0];
        }

        if (puzzleEventDirector == null)
        {
            PuzzleEventDirector[] puzzleDirectors = FindObjectsOfType<PuzzleEventDirector>(true);
            if (puzzleDirectors.Length > 0)
                puzzleEventDirector = puzzleDirectors[0];
        }

        if (gatedDoorTriggerRotator == null)
        {
            DoorTriggerRotator[] doorRotators = FindObjectsOfType<DoorTriggerRotator>(true);
            if (doorRotators.Length > 0)
                gatedDoorTriggerRotator = doorRotators[0];
        }

        if (gatedDoorDialogueTrigger == null)
        {
            DoorDialogueTrigger[] doorDialogueTriggers = FindObjectsOfType<DoorDialogueTrigger>(true);
            if (doorDialogueTriggers.Length > 0)
                gatedDoorDialogueTrigger = doorDialogueTriggers[0];
        }

        if (gatedFamilyMinigame == null)
        {
            FamilyMinigamePlaceholderController[] familyMinigames = FindObjectsOfType<FamilyMinigamePlaceholderController>(true);
            if (familyMinigames.Length > 0)
                gatedFamilyMinigame = familyMinigames[0];
        }
    }

    private bool ShouldRunIntroPhoneBeatFor(DialogueData dialogue)
    {
        return dialogue != null &&
               introPhoneObject != null &&
               phoneEndPoint != null &&
               !introPhoneBeatCompleted &&
               dialogue.lines != null &&
               phonePullDownTriggerLineIndex >= 0 &&
               phonePullDownTriggerLineIndex < dialogue.lines.Length;
    }

    private bool HasConfiguredIntroPhoneBeat()
    {
        return phonePullDownTriggerSegmentIndex >= 0 &&
               phonePullDownTriggerSegmentIndex < introDialogueSegments.Count &&
               ShouldRunIntroPhoneBeatFor(introDialogueSegments[phonePullDownTriggerSegmentIndex]);
    }

    private bool ShouldTriggerIntroPhoneBeat(DialogueData dialogue)
    {
        DialogueUI dialogueUi = DialogueUI.Instance;

        if (dialogueUi == null || dialogueUi.CurrentDialogue != dialogue)
            return false;

        return dialogueUi.CurrentLineIndex >= phonePullDownTriggerLineIndex &&
               !dialogueUi.IsTypingCurrentLine;
    }

    private IEnumerator RunIntroPhoneBeat()
    {
        if (phonePullDownStartDelay > 0f)
            yield return new WaitForSeconds(phonePullDownStartDelay);

        if (introPhoneObject != null && phoneEndPoint != null)
        {
            CacheIntroPhonePose();
            introPhoneObject.SetActive(true);

            Transform phoneTransform = introPhoneObject.transform;
            Vector3 startPosition = phoneTransform.position;
            Quaternion startRotation = phoneTransform.rotation;
            Vector3 endPosition = phoneEndPoint.position;
            Quaternion endRotation = phoneEndPoint.rotation;
            float duration = phonePullDownDuration > 0f ? phonePullDownDuration : DefaultPhonePullDownDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);

                phoneTransform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, endPosition, easedT),
                    Quaternion.Slerp(startRotation, endRotation, easedT)
                );

                yield return null;
            }

            phoneTransform.SetPositionAndRotation(endPosition, endRotation);
            introPhoneObject.SetActive(false);
            RestoreIntroPhonePose();
            introPhoneBeatCompleted = true;
        }
        
        introPhoneBeatRoutine = null;
    }

    private void StartIntroPhoneBeat()
    {
        if (introPhoneBeatRoutine != null || introPhoneBeatCompleted)
            return;

        introPhoneBeatRoutine = StartCoroutine(RunIntroPhoneBeat());
    }

    private void PrepareIntroPhoneForSequence()
    {
        introPhoneBeatCompleted = false;
        CacheIntroPhonePose();

        if (!HasConfiguredIntroPhoneBeat())
            return;

        RestoreIntroPhonePose();
        introPhoneObject.SetActive(true);
    }

    private void CacheIntroPhonePose()
    {
        if (introPhoneObject == null)
            return;

        if (introPhonePoseCached && cachedIntroPhonePoseSource == introPhoneObject)
            return;

        Transform phoneTransform = introPhoneObject.transform;
        introPhoneInitialPosition = phoneTransform.position;
        introPhoneInitialRotation = phoneTransform.rotation;
        introPhonePoseCached = true;
        cachedIntroPhonePoseSource = introPhoneObject;
    }

    private void RestoreIntroPhonePose()
    {
        if (introPhoneObject == null || !introPhonePoseCached)
            return;

        introPhoneObject.transform.SetPositionAndRotation(introPhoneInitialPosition, introPhoneInitialRotation);
    }

    private void HideIntroPhoneImmediate()
    {
        StopIntroPhoneBeatRoutine();

        if (introPhoneObject == null)
            return;

        RestoreIntroPhonePose();
        introPhoneObject.SetActive(false);
    }

    private void StopIntroPhoneBeatRoutine()
    {
        if (introPhoneBeatRoutine == null)
            return;

        StopCoroutine(introPhoneBeatRoutine);
        introPhoneBeatRoutine = null;
    }

    private void EnsureWindowDialogueTriggerRelay()
    {
        if (!Application.isPlaying || windowDialogueTrigger == null)
            return;

        windowDialogueTriggerRelay = windowDialogueTrigger.GetComponent<PlayerTriggerRelay>();

        if (windowDialogueTriggerRelay == null)
            windowDialogueTriggerRelay = windowDialogueTrigger.gameObject.AddComponent<PlayerTriggerRelay>();
    }

    private void SubscribeToWindowDialogueTrigger()
    {
        if (!Application.isPlaying || windowDialogueTriggerRelay == null)
            return;

        windowDialogueTriggerRelay.PlayerEntered -= HandleWindowDialogueTriggerEntered;
        windowDialogueTriggerRelay.PlayerEntered += HandleWindowDialogueTriggerEntered;
    }

    private void UnsubscribeFromWindowDialogueTrigger()
    {
        if (!Application.isPlaying || windowDialogueTriggerRelay == null)
            return;

        windowDialogueTriggerRelay.PlayerEntered -= HandleWindowDialogueTriggerEntered;
    }

    private void EnsureSceneThreePaintingTriggerRelay()
    {
        if (!Application.isPlaying || sceneThreePaintingTrigger == null)
            return;

        sceneThreePaintingTriggerRelay = sceneThreePaintingTrigger.GetComponent<PlayerTriggerRelay>();

        if (sceneThreePaintingTriggerRelay == null)
            sceneThreePaintingTriggerRelay = sceneThreePaintingTrigger.gameObject.AddComponent<PlayerTriggerRelay>();
    }

    private void SubscribeToSceneThreePaintingTrigger()
    {
        if (!Application.isPlaying || sceneThreePaintingTriggerRelay == null)
            return;

        sceneThreePaintingTriggerRelay.PlayerEntered -= HandleSceneThreePaintingTriggerEntered;
        sceneThreePaintingTriggerRelay.PlayerEntered += HandleSceneThreePaintingTriggerEntered;
    }

    private void UnsubscribeFromSceneThreePaintingTrigger()
    {
        if (!Application.isPlaying || sceneThreePaintingTriggerRelay == null)
            return;

        sceneThreePaintingTriggerRelay.PlayerEntered -= HandleSceneThreePaintingTriggerEntered;
    }

    private void EnsureSceneThreePuzzleDialogueTriggerRelay()
    {
        if (!Application.isPlaying || sceneThreePuzzleDialogueTrigger == null)
            return;

        sceneThreePuzzleDialogueTriggerRelay = sceneThreePuzzleDialogueTrigger.GetComponent<PlayerTriggerRelay>();

        if (sceneThreePuzzleDialogueTriggerRelay == null)
            sceneThreePuzzleDialogueTriggerRelay = sceneThreePuzzleDialogueTrigger.gameObject.AddComponent<PlayerTriggerRelay>();
    }

    private void SubscribeToSceneThreePuzzleDialogueTrigger()
    {
        if (!Application.isPlaying || sceneThreePuzzleDialogueTriggerRelay == null)
            return;

        sceneThreePuzzleDialogueTriggerRelay.PlayerEntered -= HandleSceneThreePuzzleDialogueTriggerEntered;
        sceneThreePuzzleDialogueTriggerRelay.PlayerEntered += HandleSceneThreePuzzleDialogueTriggerEntered;
    }

    private void UnsubscribeFromSceneThreePuzzleDialogueTrigger()
    {
        if (!Application.isPlaying || sceneThreePuzzleDialogueTriggerRelay == null)
            return;

        sceneThreePuzzleDialogueTriggerRelay.PlayerEntered -= HandleSceneThreePuzzleDialogueTriggerEntered;
    }

    private void SubscribeToWindowSequenceCompletion()
    {
        if (!Application.isPlaying || gatedWindowSequence == null)
            return;

        gatedWindowSequence.SequenceCompleted -= HandleWindowSequenceCompleted;
        gatedWindowSequence.SequenceCompleted += HandleWindowSequenceCompleted;
    }

    private void UnsubscribeFromWindowSequenceCompletion()
    {
        if (!Application.isPlaying || gatedWindowSequence == null)
            return;

        gatedWindowSequence.SequenceCompleted -= HandleWindowSequenceCompleted;
    }

    private void SubscribeToPuzzleSequenceCompletion()
    {
        if (!Application.isPlaying || puzzleEventDirector == null)
            return;

        puzzleEventDirector.PostPuzzleSequenceCompleted -= HandlePuzzleSequenceCompleted;
        puzzleEventDirector.PostPuzzleSequenceCompleted += HandlePuzzleSequenceCompleted;
    }

    private void UnsubscribeFromPuzzleSequenceCompletion()
    {
        if (!Application.isPlaying || puzzleEventDirector == null)
            return;

        puzzleEventDirector.PostPuzzleSequenceCompleted -= HandlePuzzleSequenceCompleted;
    }

    private void SubscribeToDoorDialogueCompletion()
    {
        if (!Application.isPlaying || gatedDoorDialogueTrigger == null)
            return;

        gatedDoorDialogueTrigger.DialogueCompleted -= HandleDoorDialogueCompleted;
        gatedDoorDialogueTrigger.DialogueCompleted += HandleDoorDialogueCompleted;
    }

    private void UnsubscribeFromDoorDialogueCompletion()
    {
        if (!Application.isPlaying || gatedDoorDialogueTrigger == null)
            return;

        gatedDoorDialogueTrigger.DialogueCompleted -= HandleDoorDialogueCompleted;
    }

    private void HandleWindowDialogueTriggerEntered(Collider other)
    {
        if (!waitingForWindowDialogueTrigger || windowDialogueTriggered)
            return;

        windowDialogueTriggered = true;
        waitingForWindowDialogueTrigger = false;
        windowTriggerDialogueRoutine = StartCoroutine(PlayWindowDialogueAndUnlockSequence());
    }

    private void HandleWindowSequenceCompleted()
    {
        if (sceneThreeCompletionHandled)
            return;

        sceneThreeCompletionHandled = true;
        BeginWaitingForSceneThreePaintingTrigger();
    }

    private void HandlePuzzleSequenceCompleted()
    {
        UnlockDoorSequence();
    }

    private void HandleDoorDialogueCompleted()
    {
        UnlockFamilyMinigame();
    }

    private void BeginWaitingForSceneThreePaintingTrigger()
    {
        EnsureSceneThreePaintingTriggerRelay();

        if (sceneThreePaintingDialogue == null ||
            sceneThreePaintingTrigger == null ||
            sceneThreePaintingTriggerRelay == null)
        {
            waitingForSceneThreePaintingTrigger = false;

            if (!sceneThreeMissingTriggerLogged && sceneThreePaintingDialogue != null)
            {
                Debug.LogWarning(
                    "OpeningGhostIntroController could not find a Scene 3 painting trigger. " +
                    "Create or assign a trigger collider named 'PaintingDialogueTrigger' near the unfinished painting.",
                    this
                );
                sceneThreeMissingTriggerLogged = true;
            }

            return;
        }

        sceneThreePaintingTriggerRelay.ResetTriggerState();
        waitingForSceneThreePaintingTrigger = true;
    }

    private void HandleSceneThreePaintingTriggerEntered(Collider other)
    {
        if (!waitingForSceneThreePaintingTrigger || sceneThreePaintingDialogueTriggered)
            return;

        sceneThreePaintingDialogueTriggered = true;
        waitingForSceneThreePaintingTrigger = false;
        sceneThreePaintingDialogueRoutine = StartCoroutine(PlaySceneThreePaintingDialogueRoutine());
    }

    private IEnumerator PlaySceneThreePaintingDialogueRoutine()
    {
        yield return PlayDialogueSegment(
            sceneThreePaintingDialogue,
            lockPlayerControlsDuringSceneThreePaintingDialogue
        );

        CompletePaintingDialogue();
        sceneThreePaintingDialogueRoutine = null;
    }

    private void CompletePaintingDialogue()
    {
        if (sceneThreePaintingDialogueCompleted)
            return;

        sceneThreePaintingDialogueCompleted = true;
        ContinueSceneThreeToPuzzleSequence();
        PaintingDialogueCompleted?.Invoke();
    }

    private void ContinueSceneThreeToPuzzleSequence()
    {
        if (ShouldUseSeparatePuzzleDialogueTrigger())
        {
            BeginWaitingForSceneThreePuzzleDialogueTrigger();
            return;
        }

        UnlockPuzzleSequence();
    }

    private void UnlockPuzzleSequence()
    {
        gatedPuzzleOpener?.SetInteractionEnabled(true);
    }

    private void BeginWaitingForSceneThreePuzzleDialogueTrigger()
    {
        if (puzzleEventDirector != null && puzzleEventDirector.HasPlayedPuzzleIntro)
        {
            UnlockPuzzleSequence();
            return;
        }

        EnsureSceneThreePuzzleDialogueTriggerRelay();

        if (sceneThreePuzzleDialogueTrigger == null || sceneThreePuzzleDialogueTriggerRelay == null)
        {
            UnlockPuzzleSequence();
            return;
        }

        sceneThreePuzzleDialogueTriggered = false;
        sceneThreePuzzleDialogueTriggerRelay.ResetTriggerState();
        waitingForSceneThreePuzzleDialogueTrigger = true;
    }

    private void HandleSceneThreePuzzleDialogueTriggerEntered(Collider other)
    {
        if (!waitingForSceneThreePuzzleDialogueTrigger || sceneThreePuzzleDialogueTriggered)
            return;

        sceneThreePuzzleDialogueTriggered = true;
        waitingForSceneThreePuzzleDialogueTrigger = false;

        if (puzzleEventDirector != null)
        {
            puzzleEventDirector.PlayPuzzleIntroIfAssigned(UnlockPuzzleSequence);
            return;
        }

        UnlockPuzzleSequence();
    }

    private void UnlockDoorSequence()
    {
        if (sceneFourDoorUnlocked)
            return;

        sceneFourDoorUnlocked = true;
        SetDoorSequenceInteractionEnabled(true);
    }

    private void UnlockFamilyMinigame()
    {
        if (sceneFourFamilyUnlocked)
            return;

        sceneFourFamilyUnlocked = true;
        gatedFamilyMinigame?.SetInteractionEnabled(true);
    }

    private void SetDoorSequenceInteractionEnabled(bool enabled)
    {
        gatedDoorTriggerRotator?.SetInteractionEnabled(enabled);
        gatedDoorDialogueTrigger?.SetInteractionEnabled(enabled);
    }

    private bool ShouldGateWindowSequence()
    {
        return sceneTwoWindowDialogue != null && windowDialogueTrigger != null && gatedWindowSequence != null;
    }

    private bool ShouldGatePuzzleSequence()
    {
        return sceneThreePaintingDialogue != null && sceneThreePaintingTrigger != null && gatedPuzzleOpener != null;
    }

    private bool ShouldUseSeparatePuzzleDialogueTrigger()
    {
        return sceneThreePuzzleDialogueTrigger != null &&
            puzzleEventDirector != null &&
            puzzleEventDirector.HasPuzzleIntroDialogue;
    }

    private bool ShouldGateDoorSequence()
    {
        return puzzleEventDirector != null && (gatedDoorTriggerRotator != null || gatedDoorDialogueTrigger != null);
    }

    private bool ShouldGateFamilyMinigame()
    {
        return gatedFamilyMinigame != null && gatedDoorDialogueTrigger != null;
    }

    private static Light FindLightInScene(
        string targetName,
        Vector3? referencePosition = null,
        Transform excludedRoot = null)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        Light closestLight = null;
        float closestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transformCandidate = transforms[i];

            if (transformCandidate == null ||
                !string.Equals(transformCandidate.name, targetName, StringComparison.Ordinal))
            {
                continue;
            }

            if (excludedRoot != null && transformCandidate.IsChildOf(excludedRoot))
                continue;

            Light lightCandidate = transformCandidate.GetComponent<Light>();

            if (lightCandidate == null)
                lightCandidate = transformCandidate.GetComponentInChildren<Light>(true);

            if (lightCandidate == null)
                continue;

            if (!referencePosition.HasValue)
                return lightCandidate;

            float distanceSqr = (lightCandidate.transform.position - referencePosition.Value).sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestLight = lightCandidate;
            }
        }

        return closestLight;
    }

    private static Transform FindTransformInScene(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transformCandidate = transforms[i];

            if (transformCandidate != null && transformCandidate.name == targetName)
                return transformCandidate;
        }

        return null;
    }
}
