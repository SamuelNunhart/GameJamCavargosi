using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class FamilyMinigamePlaceholderController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private KeyCode skipKey = KeyCode.BackQuote;
    [SerializeField] private string promptMessage = "Press E";
    [SerializeField] private float transitionDuration = 0.35f;
    [SerializeField] private float interactionDetectionTolerance = 0.75f;
    [SerializeField] private bool enableDebugLogs;
    [SerializeField] private bool interactionEnabled = true;

    [Header("Scene Progression")]
    [SerializeField] private DialogueData completionDialogue;
    [SerializeField] private string completionDialogueResourcePath = "Dialogue/Scene5_AfterFamilyMinigame";
    [SerializeField] private bool lockPlayerControlsDuringCompletionDialogue;
    [SerializeField] private UnityEvent onSequenceCompleted;

    [Header("Minigame Scene")]
    [SerializeField] private string minigameSceneName = "Pipes";
    [SerializeField] private Vector3 minigameSceneOffset = new Vector3(10000f, 0f, 0f);

    [Header("Ending Presentation")]
    [SerializeField] private bool playEndingFadeAfterCompletionDialogue;
    [SerializeField] private float endingFadeDuration = 2.5f;
    [SerializeField] private Sprite endingImage;
    [SerializeField] private Color endingImageColor = Color.white;
    [SerializeField] private string endingQuestionText = "And what will you choose?";
    [SerializeField] private float endingQuestionHoldDuration = 1.75f;
    [SerializeField] private float endingQuestionFadeDuration = 2.5f;
    [SerializeField] private int endingQuestionFontSize = 72;
    [SerializeField] private Color endingQuestionColor = Color.white;

    [Header("Player Control")]
    [SerializeField] private Transform playerViewTransform;
    [SerializeField] private PlayerControlLocker playerControlLocker;
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;

    [Header("Scene Objects")]
    [SerializeField] private Transform minigameViewPoint;
    [SerializeField] private string minigameViewPointObjectName = "FamilyMinigameViewPoint";

    private Collider triggerCollider;
    private static FamilyMinigamePlaceholderController activeMinigameController;
    private bool playerInRange;
    private bool sequenceOpen;
    private bool isTransitioning;
    private bool completionHandled;
    private bool minigameSceneLoaded;
    private bool minigameSceneOffsetApplied;
    private bool promptVisible;
    private bool playerControlLockHeld;
    private Vector3 savedViewPosition;
    private Quaternion savedViewRotation;
    private Scene gameplayScene;
    private Scene loadedMinigameScene;
    private readonly List<Behaviour> suspendedPresentationBehaviours = new List<Behaviour>();
    private Canvas endingOverlayCanvas;
    private Image endingBlackoutImage;
    private Image endingPresentationImage;
    private TextMeshProUGUI endingQuestionLabel;

    public bool IsInteractionEnabled => interactionEnabled;

    private void Reset()
    {
        AutoAssignReferences();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        ConfigureTrigger();
    }

    private void Awake()
    {
        AutoAssignReferences();
        ConfigureTrigger();
        AutoAssignCompletionDialogue();
        ShowPrompt(false);
    }

    private void Update()
    {
        UpdatePlayerRangeState();

        if (!completionHandled &&
            !sequenceOpen &&
            !isTransitioning &&
            playerInRange &&
            CanInteractNow() &&
            Input.GetKeyDown(interactKey))
        {
            DebugLog("Interact key pressed while in range. Opening family minigame placeholder.");
            OpenSequence();
            return;
        }

        if (sequenceOpen && !isTransitioning && Input.GetKeyDown(cancelKey))
        {
            DebugLog("Cancel key pressed. Closing family minigame placeholder without completion.");
            CloseSequence(false);
            return;
        }

        if (sequenceOpen && !isTransitioning && !completionHandled && Input.GetKeyDown(skipKey))
        {
            DebugLog("Skip key pressed. Completing family minigame placeholder.");
            CompleteSequence();
            return;
        }

        RefreshPrompt();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = true;
        RefreshPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = false;
        RefreshPrompt();
    }

    private void OnTriggerStay(Collider other)
    {
        if (playerInRange || !IsPlayerCollider(other))
            return;

        playerInRange = true;
        RefreshPrompt();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (ReferenceEquals(activeMinigameController, this))
            activeMinigameController = null;

        ShowPrompt(false);
        promptVisible = false;
        HideEndingOverlayImmediate();
        RestoreSuspendedPresentation();

        if (sequenceOpen && !minigameSceneLoaded && playerViewTransform != null)
            playerViewTransform.SetPositionAndRotation(savedViewPosition, savedViewRotation);

        RestoreCursorState();
        ReleasePlayerControlLock();
        minigameSceneLoaded = false;
        sequenceOpen = false;
        isTransitioning = false;
    }

    public void OpenSequence()
    {
        if (!interactionEnabled || completionHandled || sequenceOpen || isTransitioning)
            return;

        StartCoroutine(OpenSequenceRoutine());
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        RefreshPrompt();
    }

    public void CloseSequence(bool completed)
    {
        if (!sequenceOpen || isTransitioning)
            return;

        StartCoroutine(CloseSequenceRoutine(completed));
    }

    public void CompleteSequence()
    {
        if (completionHandled || !sequenceOpen || isTransitioning)
            return;

        completionHandled = true;
        StartCoroutine(CloseSequenceRoutine(true));
    }

    public static bool TryCompleteActiveSceneMinigame()
    {
        if (activeMinigameController == null ||
            !activeMinigameController.sequenceOpen ||
            activeMinigameController.completionHandled ||
            activeMinigameController.isTransitioning)
            return false;

        activeMinigameController.CompleteSequence();
        return true;
    }

    private IEnumerator OpenSequenceRoutine()
    {
        isTransitioning = true;
        sequenceOpen = true;
        gameplayScene = gameObject.scene;
        activeMinigameController = this;

        ShowPrompt(false);
        AcquirePlayerControlLock();

        if (playerViewTransform != null)
        {
            savedViewPosition = playerViewTransform.position;
            savedViewRotation = playerViewTransform.rotation;
        }

        bool loadedSeparateScene = false;

        if (!string.IsNullOrWhiteSpace(minigameSceneName))
        {
            yield return LoadMinigameScene();
            loadedSeparateScene = minigameSceneLoaded;
        }

        if (!loadedSeparateScene)
            yield return MoveToView(minigameViewPoint);
        else
            ApplyOpenCursorState();

        isTransitioning = false;
        DebugLog("Family minigame placeholder opened.");
    }

    private IEnumerator CloseSequenceRoutine(bool completed)
    {
        isTransitioning = true;

        if (minigameSceneLoaded)
            yield return UnloadMinigameScene();
        else
            yield return MoveBackToSavedView();

        ReleasePlayerControlLock();

        sequenceOpen = false;
        isTransitioning = false;
        minigameSceneLoaded = false;

        if (ReferenceEquals(activeMinigameController, this))
            activeMinigameController = null;

        if (completed)
        {
            yield return PlayCompletionDialogueIfAssigned();
            yield return PlayEndingPresentationIfEnabled();
            onSequenceCompleted?.Invoke();
            ShowPrompt(false);
            DebugLog("Family minigame placeholder completed.");
        }
        else
        {
            RefreshPrompt();
            DebugLog("Family minigame placeholder closed without completion.");
        }
    }

    private IEnumerator PlayCompletionDialogueIfAssigned()
    {
        if (completionDialogue == null || DialogueUI.Instance == null)
            yield break;

        bool finished = false;
        bool started = DialogueUI.Instance.StartDialogue(
            completionDialogue,
            lockPlayerControlsDuringCompletionDialogue,
            () => finished = true
        );

        if (!started)
            yield break;

        while (!finished)
            yield return null;
    }

    private IEnumerator PlayEndingPresentationIfEnabled()
    {
        if (!playEndingFadeAfterCompletionDialogue)
            yield break;

        AcquirePlayerControlLock();
        EnsureEndingOverlay();

        if (endingBlackoutImage == null)
            yield break;

        SetEndingBlackoutAlpha(0f);
        SetEndingImageVisible(false);
        SetEndingQuestionAlpha(0f);

        float duration = Mathf.Max(0f, endingFadeDuration);

        if (duration <= 0f)
        {
            SetEndingBlackoutAlpha(1f);
            SetEndingImageVisible(endingImage != null);
            SetEndingQuestionAlpha(1f);
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetEndingBlackoutAlpha(t);
                yield return null;
            }

            SetEndingBlackoutAlpha(1f);
            SetEndingImageVisible(endingImage != null);
            SetEndingQuestionAlpha(1f);
        }

        float holdDuration = Mathf.Max(0f, endingQuestionHoldDuration);
        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        float questionFadeDuration = Mathf.Max(0f, endingQuestionFadeDuration);
        if (questionFadeDuration <= 0f)
        {
            SetEndingQuestionAlpha(0f);
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < questionFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / questionFadeDuration);
                SetEndingQuestionAlpha(1f - t);
                yield return null;
            }

            SetEndingQuestionAlpha(0f);
        }

        QuitGame();
    }

    private IEnumerator MoveToView(Transform targetView)
    {
        if (playerViewTransform == null || targetView == null || transitionDuration <= 0f)
            yield break;

        yield return MoveView(
            playerViewTransform.position,
            playerViewTransform.rotation,
            targetView.position,
            targetView.rotation
        );
    }

    private IEnumerator MoveBackToSavedView()
    {
        if (playerViewTransform == null || transitionDuration <= 0f)
            yield break;

        yield return MoveView(
            playerViewTransform.position,
            playerViewTransform.rotation,
            savedViewPosition,
            savedViewRotation
        );
    }

    private IEnumerator LoadMinigameScene()
    {
        if (SceneManager.GetSceneByName(minigameSceneName).isLoaded)
        {
            loadedMinigameScene = SceneManager.GetSceneByName(minigameSceneName);
            minigameSceneLoaded = loadedMinigameScene.IsValid() && loadedMinigameScene.isLoaded;

            if (minigameSceneLoaded)
            {
                ApplyMinigameSceneOffset();
                SuspendPresentationForMinigame();
                SceneManager.SetActiveScene(loadedMinigameScene);
            }

            yield break;
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(minigameSceneName, LoadSceneMode.Additive);

        if (loadOperation == null)
        {
            Debug.LogWarning($"Unable to load minigame scene '{minigameSceneName}'. Falling back to placeholder view.", this);
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        loadedMinigameScene = SceneManager.GetSceneByName(minigameSceneName);
        minigameSceneLoaded = loadedMinigameScene.IsValid() && loadedMinigameScene.isLoaded;

        if (!minigameSceneLoaded)
        {
            Debug.LogWarning($"Minigame scene '{minigameSceneName}' finished loading but is not available. Falling back to placeholder view.", this);
            yield break;
        }

        ApplyMinigameSceneOffset();
        SuspendPresentationForMinigame();
        SceneManager.SetActiveScene(loadedMinigameScene);
    }

    private IEnumerator UnloadMinigameScene()
    {
        RestoreSuspendedPresentation();

        if (gameplayScene.IsValid() && gameplayScene.isLoaded)
            SceneManager.SetActiveScene(gameplayScene);

        RevertMinigameSceneOffset();

        if (!loadedMinigameScene.IsValid() || !loadedMinigameScene.isLoaded)
            yield break;

        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(loadedMinigameScene);

        if (unloadOperation == null)
            yield break;

        while (!unloadOperation.isDone)
            yield return null;

        RestoreCursorState();
    }

    private void ApplyMinigameSceneOffset()
    {
        if (!loadedMinigameScene.IsValid() || !loadedMinigameScene.isLoaded || minigameSceneOffsetApplied || minigameSceneOffset == Vector3.zero)
            return;

        GameObject[] rootObjects = loadedMinigameScene.GetRootGameObjects();

        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform rootTransform = rootObjects[i] != null ? rootObjects[i].transform : null;

            if (rootTransform != null)
                rootTransform.position += minigameSceneOffset;
        }

        minigameSceneOffsetApplied = true;
    }

    private void RevertMinigameSceneOffset()
    {
        if (!loadedMinigameScene.IsValid() || !loadedMinigameScene.isLoaded || !minigameSceneOffsetApplied || minigameSceneOffset == Vector3.zero)
            return;

        GameObject[] rootObjects = loadedMinigameScene.GetRootGameObjects();

        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform rootTransform = rootObjects[i] != null ? rootObjects[i].transform : null;

            if (rootTransform != null)
                rootTransform.position -= minigameSceneOffset;
        }

        minigameSceneOffsetApplied = false;
    }

    private IEnumerator MoveView(Vector3 startPosition, Quaternion startRotation, Vector3 endPosition, Quaternion endRotation)
    {
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = t * t * (3f - 2f * t);

            playerViewTransform.position = Vector3.Lerp(startPosition, endPosition, t);
            playerViewTransform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        playerViewTransform.position = endPosition;
        playerViewTransform.rotation = endRotation;
    }

    private void RefreshPrompt()
    {
        UpdatePlayerRangeState();

        bool shouldShowPrompt = !completionHandled && !sequenceOpen && !isTransitioning && playerInRange && CanInteractNow();
        bool promptNeedsRefresh =
            shouldShowPrompt &&
            DialogueUI.Instance != null &&
            !DialogueUI.Instance.IsPromptActiveFor(this, promptMessage);

        if (promptVisible == shouldShowPrompt && !promptNeedsRefresh)
            return;

        ShowPrompt(shouldShowPrompt);
        promptVisible = shouldShowPrompt;
    }

    private bool CanInteractNow()
    {
        return interactionEnabled && (DialogueUI.Instance == null || !DialogueUI.Instance.IsDialogueOpen);
    }

    private void ShowPrompt(bool show)
    {
        if (DialogueUI.Instance == null)
            return;

        DialogueUI.Instance.ClearPromptAnchor();
        DialogueUI.Instance.SetPromptVisible(this, show, promptMessage);
    }

    private void AcquirePlayerControlLock()
    {
        if (playerControlLockHeld)
            return;

        if (playerControlLocker != null)
        {
            playerControlLocker.AcquireLock();
        }
        else
        {
            if (playerMovement != null)
                playerMovement.enabled = false;

            if (playerLook != null)
                playerLook.enabled = false;
        }

        playerControlLockHeld = true;
    }

    private void ReleasePlayerControlLock()
    {
        if (!playerControlLockHeld)
            return;

        if (playerControlLocker != null)
        {
            playerControlLocker.ReleaseLock();
        }
        else
        {
            if (playerMovement != null)
                playerMovement.enabled = true;

            if (playerLook != null)
                playerLook.enabled = true;
        }

        playerControlLockHeld = false;
    }

    private void ApplyOpenCursorState()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursorState()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SuspendPresentationForMinigame()
    {
        RestoreSuspendedPresentation();

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera == null || !camera.enabled || camera.gameObject.scene == loadedMinigameScene)
                continue;

            camera.enabled = false;
            suspendedPresentationBehaviours.Add(camera);
        }

        AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];

            if (listener == null || !listener.enabled || listener.gameObject.scene == loadedMinigameScene)
                continue;

            listener.enabled = false;
            suspendedPresentationBehaviours.Add(listener);
        }

        EventSystem[] eventSystems = FindObjectsOfType<EventSystem>(true);
        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];

            if (eventSystem == null || !eventSystem.enabled || eventSystem.gameObject.scene == loadedMinigameScene)
                continue;

            eventSystem.enabled = false;
            suspendedPresentationBehaviours.Add(eventSystem);
        }
    }

    private void RestoreSuspendedPresentation()
    {
        for (int i = 0; i < suspendedPresentationBehaviours.Count; i++)
        {
            Behaviour behaviour = suspendedPresentationBehaviours[i];

            if (behaviour != null)
                behaviour.enabled = true;
        }

        suspendedPresentationBehaviours.Clear();
    }

    private void AutoAssignCompletionDialogue()
    {
        if (completionDialogue != null || string.IsNullOrWhiteSpace(completionDialogueResourcePath))
            return;

        completionDialogue = Resources.Load<DialogueData>(completionDialogueResourcePath);
    }

    private void AutoAssignReferences()
    {
        triggerCollider = GetComponent<Collider>();

        if (string.IsNullOrWhiteSpace(minigameViewPointObjectName))
            minigameViewPointObjectName = "FamilyMinigameViewPoint";

        if (minigameViewPoint == null)
        {
            Transform targetTransform = FindTransformInScene(minigameViewPointObjectName);
            if (targetTransform != null)
                minigameViewPoint = targetTransform;
        }

        if (playerViewTransform == null && Camera.main != null)
            playerViewTransform = Camera.main.transform;

        if (playerViewTransform != null)
        {
            if (playerMovement == null)
                playerMovement = FindBehaviourInParents(playerViewTransform, "PlayerMove");

            if (playerLook == null)
                playerLook = FindBehaviourInParents(playerViewTransform, "PlayerLook");
        }

        if (playerMovement == null)
            playerMovement = FindBehaviourInScene("PlayerMove");

        if (playerLook == null)
            playerLook = FindBehaviourInScene("PlayerLook");

        if (playerControlLocker == null)
        {
            PlayerControlLocker[] controlLockers = FindObjectsOfType<PlayerControlLocker>(true);
            if (controlLockers.Length > 0)
                playerControlLocker = controlLockers[0];
        }
    }

    private void EnsureEndingOverlay()
    {
        if (endingOverlayCanvas != null && endingBlackoutImage != null && endingPresentationImage != null && endingQuestionLabel != null)
            return;

        GameObject overlayRoot = new GameObject("EndingOverlay", typeof(Canvas), typeof(GraphicRaycaster));
        overlayRoot.hideFlags = HideFlags.DontSave;

        endingOverlayCanvas = overlayRoot.GetComponent<Canvas>();
        endingOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        endingOverlayCanvas.sortingOrder = short.MaxValue;

        GameObject blackoutObject = new GameObject("Blackout", typeof(RectTransform), typeof(Image));
        blackoutObject.transform.SetParent(overlayRoot.transform, false);
        StretchFullScreen(blackoutObject.GetComponent<RectTransform>());

        endingBlackoutImage = blackoutObject.GetComponent<Image>();
        endingBlackoutImage.color = new Color(0f, 0f, 0f, 0f);
        endingBlackoutImage.raycastTarget = true;

        GameObject imageObject = new GameObject("EndingImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(overlayRoot.transform, false);
        StretchFullScreen(imageObject.GetComponent<RectTransform>());

        endingPresentationImage = imageObject.GetComponent<Image>();
        endingPresentationImage.preserveAspect = true;
        endingPresentationImage.raycastTarget = false;
        SetEndingImageVisible(endingImage != null);

        GameObject questionObject = new GameObject("EndingQuestion", typeof(RectTransform), typeof(TextMeshProUGUI));
        questionObject.transform.SetParent(overlayRoot.transform, false);

        RectTransform questionRect = questionObject.GetComponent<RectTransform>();
        StretchFullScreen(questionRect);
        questionRect.offsetMin = new Vector2(96f, 96f);
        questionRect.offsetMax = new Vector2(-96f, -96f);

        endingQuestionLabel = questionObject.GetComponent<TextMeshProUGUI>();
        endingQuestionLabel.text = endingQuestionText;
        endingQuestionLabel.fontSize = Mathf.Max(1, endingQuestionFontSize);
        endingQuestionLabel.color = endingQuestionColor;
        endingQuestionLabel.alignment = TextAlignmentOptions.Center;
        endingQuestionLabel.enableWordWrapping = true;
        endingQuestionLabel.overflowMode = TextOverflowModes.Overflow;
        endingQuestionLabel.raycastTarget = false;
        SetEndingQuestionAlpha(0f);
    }

    private void HideEndingOverlayImmediate()
    {
        SetEndingBlackoutAlpha(0f);
        SetEndingImageVisible(false);
        SetEndingQuestionAlpha(0f);
    }

    private void SetEndingBlackoutAlpha(float alpha)
    {
        if (endingBlackoutImage == null)
            return;

        Color color = endingBlackoutImage.color;
        color.a = Mathf.Clamp01(alpha);
        endingBlackoutImage.color = color;
    }

    private void SetEndingImageVisible(bool visible)
    {
        if (endingPresentationImage == null)
            return;

        endingPresentationImage.sprite = endingImage;
        endingPresentationImage.color = endingImageColor;
        endingPresentationImage.enabled = visible && endingImage != null;
    }

    private void SetEndingQuestionAlpha(float alpha)
    {
        if (endingQuestionLabel == null)
            return;

        endingQuestionLabel.text = endingQuestionText;

        Color color = endingQuestionColor;
        color.a *= Mathf.Clamp01(alpha);
        endingQuestionLabel.color = color;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void StretchFullScreen(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private void ConfigureTrigger()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void UpdatePlayerRangeState()
    {
        bool detectedInRange = IsPlayerWithinInteractionZone();

        if (playerInRange == detectedInRange)
            return;

        playerInRange = detectedInRange;
    }

    private bool IsPlayerWithinInteractionZone()
    {
        if (triggerCollider == null)
            return false;

        Transform playerReference = GetPlayerReferenceTransform();
        if (playerReference == null)
            return false;

        Vector3 playerPosition = playerReference.position;
        Vector3 closestPoint = triggerCollider.ClosestPoint(playerPosition);
        float sqrDistance = (closestPoint - playerPosition).sqrMagnitude;
        float tolerance = Mathf.Max(0f, interactionDetectionTolerance);

        return sqrDistance <= tolerance * tolerance;
    }

    private Transform GetPlayerReferenceTransform()
    {
        if (playerMovement != null)
            return playerMovement.transform;

        if (playerLook != null)
            return playerLook.transform.root;

        if (playerViewTransform != null)
            return playerViewTransform.root;

        return Camera.main != null ? Camera.main.transform.root : null;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        if (playerViewTransform == null)
            return IsLikelyPlayerHierarchy(other.transform);

        if (other.transform == playerViewTransform || other.transform.IsChildOf(playerViewTransform.root))
            return true;

        return IsLikelyPlayerHierarchy(other.transform);
    }

    private bool IsLikelyPlayerHierarchy(Transform candidate)
    {
        if (candidate == null)
            return false;

        Transform candidateRoot = candidate.root;

        if (candidateRoot.CompareTag("Player"))
            return true;

        if (!string.IsNullOrEmpty(candidateRoot.name) && candidateRoot.name.Contains("Player"))
            return true;

        if (playerMovement != null && candidateRoot == playerMovement.transform.root)
            return true;

        if (playerLook != null && candidateRoot == playerLook.transform.root)
            return true;

        if (playerViewTransform != null && candidateRoot == playerViewTransform.root)
            return true;

        Rigidbody attachedRigidbody = candidate.GetComponentInParent<Rigidbody>();
        if (attachedRigidbody != null)
        {
            Transform rigidbodyRoot = attachedRigidbody.transform.root;

            if (rigidbodyRoot.CompareTag("Player"))
                return true;

            if (!string.IsNullOrEmpty(rigidbodyRoot.name) && rigidbodyRoot.name.Contains("Player"))
                return true;
        }

        CharacterController characterController = candidate.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            Transform controllerRoot = characterController.transform.root;

            if (controllerRoot.CompareTag("Player"))
                return true;

            if (!string.IsNullOrEmpty(controllerRoot.name) && controllerRoot.name.Contains("Player"))
                return true;
        }

        return false;
    }

    private void DebugLog(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log("[FamilyMinigamePlaceholderController] " + message, this);
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

    private static MonoBehaviour FindBehaviourInParents(Transform start, string behaviourName)
    {
        if (start == null)
            return null;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour != null && behaviour.GetType().Name == behaviourName)
                return behaviour;
        }

        return null;
    }

    private static MonoBehaviour FindBehaviourInScene(string behaviourName)
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour != null && behaviour.GetType().Name == behaviourName)
                return behaviour;
        }

        return null;
    }
}
