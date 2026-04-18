using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OpeningGhostIntroController : MonoBehaviour
{
    private const float DefaultGhostFadeInDuration = 0.45f;

    [Header("Dialogue")]
    [SerializeField] private List<DialogueData> introDialogueSegments = new List<DialogueData>();
    [SerializeField] private int ghostRevealAfterSegmentIndex;

    [Header("Player")]
    [SerializeField] private PlayerControlLocker playerControlLocker;
    [SerializeField] private List<Behaviour> fallbackBehavioursToDisable = new List<Behaviour>();

    [Header("Camera")]
    [SerializeField] private Camera gameplayCamera;

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
    [SerializeField] private CanvasGroup blackoutCanvasGroup;
    [SerializeField] private Graphic blackoutGraphic;

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
    [SerializeField] private bool lockPlayerControlsDuringSceneThreePaintingDialogue;
    [SerializeField] private string sceneThreePaintingDialogueResourcePath = "Dialogue/Scene3_PaintingTrigger";
    [SerializeField] private string sceneThreePaintingTriggerObjectName = "PaintingDialogueTrigger";

    private Coroutine introRoutine;
    private Coroutine ghostMoveRoutine;
    private Coroutine ghostFadeRoutine;
    private Coroutine windowTriggerDialogueRoutine;
    private Coroutine sceneThreePaintingDialogueRoutine;
    private PlayerTriggerRelay windowDialogueTriggerRelay;
    private PlayerTriggerRelay sceneThreePaintingTriggerRelay;
    private SpriteRenderer[] ghostSpriteRenderers = Array.Empty<SpriteRenderer>();
    private int fallbackLockCount;
    private bool sequenceFinished;
    private bool ghostRevealed;
    private bool waitingForWindowDialogueTrigger;
    private bool waitingForSceneThreePaintingTrigger;
    private bool windowDialogueTriggered;
    private bool sceneThreeCompletionHandled;
    private bool sceneThreePaintingDialogueTriggered;
    private bool sceneThreeMissingTriggerLogged;

    private void Reset()
    {
        AutoAssignSceneReferences();
    }

    private void OnValidate()
    {
        AutoAssignSceneReferences();

        if (ghostRevealAfterSegmentIndex < 0)
            ghostRevealAfterSegmentIndex = 0;

        if (ghostFadeInDuration < 0f)
            ghostFadeInDuration = 0f;
    }

    private void Awake()
    {
        AutoAssignSceneReferences();
        EnsureWindowDialogueTriggerRelay();
        EnsureSceneThreePaintingTriggerRelay();
        EnsureBlackoutReady();
        HideGhostImmediate();
        SetCameraState(introCamera, true);
        SetCameraState(gameplayCamera, false);

        if (ShouldGateWindowSequence())
            gatedWindowSequence.SetInteractionEnabled(false);
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
        SubscribeToWindowDialogueTrigger();
        SubscribeToSceneThreePaintingTrigger();
        SubscribeToWindowSequenceCompletion();

        if (gatedWindowSequence != null &&
            gatedWindowSequence.HasFinishedCompletionFlow &&
            !sceneThreeCompletionHandled)
            HandleWindowSequenceCompleted();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        UnsubscribeFromWindowDialogueTrigger();
        UnsubscribeFromSceneThreePaintingTrigger();
        UnsubscribeFromWindowSequenceCompletion();

        if (introRoutine != null)
            StopCoroutine(introRoutine);

        if (ghostMoveRoutine != null)
            StopCoroutine(ghostMoveRoutine);

        if (ghostFadeRoutine != null)
            StopCoroutine(ghostFadeRoutine);

        if (windowTriggerDialogueRoutine != null)
            StopCoroutine(windowTriggerDialogueRoutine);

        if (sceneThreePaintingDialogueRoutine != null)
            StopCoroutine(sceneThreePaintingDialogueRoutine);

        introRoutine = null;
        ghostMoveRoutine = null;
        ghostFadeRoutine = null;
        windowTriggerDialogueRoutine = null;
        sceneThreePaintingDialogueRoutine = null;
        waitingForWindowDialogueTrigger = false;
        waitingForSceneThreePaintingTrigger = false;

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
                yield return PlayDialogueSegment(segment);

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
        SetCameraState(introCamera, false);
        SetCameraState(gameplayCamera, true);
        yield return FadeFromBlack(fadeInDuration);

        ReleaseControlLock();

        if (sceneTwoIntroDialogue != null)
            yield return PlayDialogueSegment(sceneTwoIntroDialogue, lockPlayerControlsDuringSceneTwoIntroDialogue);

        BeginWaitingForWindowDialogueTrigger();
        sequenceFinished = true;
        introRoutine = null;
    }

    private IEnumerator PlayDialogueSegment(DialogueData dialogue, bool lockPlayerControls = false)
    {
        bool finished = false;
        bool started = DialogueUI.Instance.StartDialogue(dialogue, lockPlayerControls, () => finished = true);

        if (!started)
            yield break;

        while (!finished)
            yield return null;
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

        CacheGhostRenderers();
        AutoAssignSceneTwoReferences();
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

        sceneThreePaintingDialogueRoutine = null;
    }

    private bool ShouldGateWindowSequence()
    {
        return sceneTwoWindowDialogue != null && windowDialogueTrigger != null && gatedWindowSequence != null;
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
