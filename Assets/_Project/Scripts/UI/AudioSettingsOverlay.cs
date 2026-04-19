using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AudioSettingsOverlay : MonoBehaviour
{
    private const string MasterVolumeKey = "AudioSettings.MasterVolume";
    private const string MusicVolumeKey = "AudioSettings.MusicVolume";
    private const string SfxVolumeKey = "AudioSettings.SfxVolume";
    private const float DefaultMasterVolume = 1f;
    private const float DefaultMusicVolume = 0.25f;
    private const float DefaultSfxVolume = 1f;
    private const float PanelWidth = 360f;
    private const float PanelMargin = 24f;
    private const float PanelSlideSpeed = 1200f;

    private static readonly HashSet<string> AllowedSceneNames = new HashSet<string>
    {
        "MainMenu",
        "Game",
        "Pipes"
    };

    private static AudioSettingsOverlay instance;

    private readonly Dictionary<int, TrackedAudioSource> musicSources = new Dictionary<int, TrackedAudioSource>();
    private readonly Dictionary<int, TrackedAudioSource> sfxSources = new Dictionary<int, TrackedAudioSource>();

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform panelTransform;
    private Slider sensitivitySlider;
    private Slider masterSlider;
    private Slider musicSlider;
    private Slider sfxSlider;
    private Text sensitivityValueLabel;
    private Text masterValueLabel;
    private Text musicValueLabel;
    private Text sfxValueLabel;
    private Font uiFont;

    private float sensitivity;
    private float masterVolume;
    private float musicVolume;
    private float sfxVolume;
    private bool isVisible;

    private struct TrackedAudioSource
    {
        public AudioSource Source;
        public float BaseVolume;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static bool IsOpen => instance != null && instance.isVisible;
    public static float MusicChannelVolume => instance != null ? instance.musicVolume : DefaultMusicVolume;

    public static void RegisterMusicSource(AudioSource source)
    {
        EnsureInstance().RegisterSource(source, isMusic: true);
    }

    public static void RegisterSfxSource(AudioSource source)
    {
        EnsureInstance().RegisterSource(source, isMusic: false);
    }

    private static AudioSettingsOverlay EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject overlayObject = new GameObject(nameof(AudioSettingsOverlay));
        instance = overlayObject.AddComponent<AudioSettingsOverlay>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolumes();
        BuildUiIfNeeded();
        ApplyAllVolumes();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        RefreshVisibilityForScene(SceneManager.GetActiveScene(), instant: true);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            instance = null;
        }
    }

    private void Update()
    {
        BuildUiIfNeeded();
        AnimatePanel();

        if (!IsSettingsAllowedInActiveScene())
            return;

        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (isVisible)
        {
            SetVisible(false);
            return;
        }

        if (ShouldBlockOpenForCurrentState())
            return;

        SetVisible(true);
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        RefreshVisibilityForScene(nextScene, instant: true);
    }

    private void RegisterSource(AudioSource source, bool isMusic)
    {
        if (source == null)
            return;

        Dictionary<int, TrackedAudioSource> targetDictionary = isMusic ? musicSources : sfxSources;
        int sourceId = source.GetInstanceID();

        if (targetDictionary.TryGetValue(sourceId, out TrackedAudioSource trackedSource))
        {
            trackedSource.Source = source;
            targetDictionary[sourceId] = trackedSource;
        }
        else
        {
            float channelVolume = isMusic ? musicVolume : sfxVolume;
            float baseVolume = channelVolume > 0.0001f
                ? Mathf.Clamp01(source.volume / channelVolume)
                : Mathf.Clamp01(source.volume);

            targetDictionary[sourceId] = new TrackedAudioSource
            {
                Source = source,
                BaseVolume = baseVolume
            };
        }

        ApplyTrackedSources(targetDictionary, isMusic ? musicVolume : sfxVolume);
    }

    private void LoadVolumes()
    {
        sensitivity = PlayerLook.CurrentSensitivity;
        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);
    }

    private void ApplyAllVolumes()
    {
        AudioListener.volume = masterVolume;
        PlayerLook.SetSensitivity(sensitivity);
        ApplyTrackedSources(musicSources, musicVolume);
        ApplyTrackedSources(sfxSources, sfxVolume);
        SyncUiWithSettings();
    }

    private void ApplyTrackedSources(Dictionary<int, TrackedAudioSource> trackedSources, float channelVolume)
    {
        if (trackedSources.Count == 0)
            return;

        List<int> deadSourceIds = null;

        foreach (KeyValuePair<int, TrackedAudioSource> pair in trackedSources)
        {
            if (pair.Value.Source == null)
            {
                if (deadSourceIds == null)
                    deadSourceIds = new List<int>();

                deadSourceIds.Add(pair.Key);
                continue;
            }

            pair.Value.Source.volume = pair.Value.BaseVolume * channelVolume;
        }

        if (deadSourceIds == null)
            return;

        for (int i = 0; i < deadSourceIds.Count; i++)
            trackedSources.Remove(deadSourceIds[i]);
    }

    private void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        AudioListener.volume = masterVolume;
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        PlayerPrefs.Save();
        SyncUiWithSettings();
    }

    private void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        ApplyTrackedSources(musicSources, musicVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        PlayerPrefs.Save();
        SyncUiWithSettings();
    }

    private void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        ApplyTrackedSources(sfxSources, sfxVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.Save();
        SyncUiWithSettings();
    }

    private void SetSensitivity(float value)
    {
        sensitivity = Mathf.Clamp(value, PlayerLook.MinSensitivity, PlayerLook.MaxSensitivity);
        PlayerLook.SetSensitivity(sensitivity);
        SyncUiWithSettings();
    }

    private void SyncUiWithSettings()
    {
        if (sensitivitySlider != null && !Mathf.Approximately(sensitivitySlider.value, sensitivity))
            sensitivitySlider.SetValueWithoutNotify(sensitivity);

        if (masterSlider != null && !Mathf.Approximately(masterSlider.value, masterVolume))
            masterSlider.SetValueWithoutNotify(masterVolume);

        if (musicSlider != null && !Mathf.Approximately(musicSlider.value, musicVolume))
            musicSlider.SetValueWithoutNotify(musicVolume);

        if (sfxSlider != null && !Mathf.Approximately(sfxSlider.value, sfxVolume))
            sfxSlider.SetValueWithoutNotify(sfxVolume);

        if (sensitivityValueLabel != null)
            sensitivityValueLabel.text = FormatSensitivity(sensitivity);

        if (masterValueLabel != null)
            masterValueLabel.text = FormatPercent(masterVolume);

        if (musicValueLabel != null)
            musicValueLabel.text = FormatPercent(musicVolume);

        if (sfxValueLabel != null)
            sfxValueLabel.text = FormatPercent(sfxVolume);
    }

    private static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    private static string FormatSensitivity(float value)
    {
        return Mathf.RoundToInt(value).ToString();
    }

    private bool IsSettingsAllowedInActiveScene()
    {
        return AllowedSceneNames.Contains(SceneManager.GetActiveScene().name);
    }

    private bool ShouldBlockOpenForCurrentState()
    {
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsDialogueOpen)
            return true;

        if (!string.Equals(SceneManager.GetActiveScene().name, "Game"))
            return false;

        PuzzleOpener[] puzzleOpeners = FindObjectsOfType<PuzzleOpener>(true);
        for (int i = 0; i < puzzleOpeners.Length; i++)
        {
            PuzzleOpener opener = puzzleOpeners[i];
            if (opener != null && (opener.IsPuzzleOpen || opener.IsTransitioning))
                return true;
        }

        WindowSequenceController[] windowSequences = FindObjectsOfType<WindowSequenceController>(true);
        for (int i = 0; i < windowSequences.Length; i++)
        {
            WindowSequenceController sequence = windowSequences[i];
            if (sequence != null && sequence.IsOpen)
                return true;
        }

        return false;
    }

    private void RefreshVisibilityForScene(Scene scene, bool instant)
    {
        if (!AllowedSceneNames.Contains(scene.name))
        {
            SetVisible(false, instant: true);
            return;
        }

        UpdateCursorForCurrentState();
        SyncUiWithSettings();

        if (instant)
            SnapPanelToTarget();
    }

    private void SetVisible(bool visible, bool instant = false)
    {
        isVisible = visible && IsSettingsAllowedInActiveScene();

        if (canvas != null)
            canvas.enabled = isVisible || !instant;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        UpdateCursorForCurrentState();

        if (instant)
            SnapPanelToTarget();
    }

    private void AnimatePanel()
    {
        if (panelTransform == null)
            return;

        float targetX = GetTargetPanelX();
        Vector2 anchoredPosition = panelTransform.anchoredPosition;
        anchoredPosition.x = Mathf.MoveTowards(anchoredPosition.x, targetX, PanelSlideSpeed * Time.unscaledDeltaTime);
        panelTransform.anchoredPosition = anchoredPosition;

        if (!isVisible && Mathf.Abs(anchoredPosition.x - targetX) <= 0.01f && canvas != null)
            canvas.enabled = false;
    }

    private void SnapPanelToTarget()
    {
        if (panelTransform == null)
            return;

        Vector2 anchoredPosition = panelTransform.anchoredPosition;
        anchoredPosition.x = GetTargetPanelX();
        panelTransform.anchoredPosition = anchoredPosition;

        if (!isVisible && canvas != null)
            canvas.enabled = false;
    }

    private float GetTargetPanelX()
    {
        return isVisible ? -PanelMargin : PanelWidth + PanelMargin;
    }

    private void UpdateCursorForCurrentState()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (isVisible || string.Equals(sceneName, "MainMenu") || string.Equals(sceneName, "Pipes"))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (string.Equals(sceneName, "Game"))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void BuildUiIfNeeded()
    {
        if (canvas != null)
            return;

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("AudioSettingsCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        canvas.enabled = false;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();

        GameObject panelObject = new GameObject("AudioSettingsPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        panelTransform = panelObject.AddComponent<RectTransform>();
        panelTransform.anchorMin = new Vector2(1f, 0f);
        panelTransform.anchorMax = new Vector2(1f, 1f);
        panelTransform.pivot = new Vector2(1f, 0.5f);
        panelTransform.sizeDelta = new Vector2(PanelWidth, -48f);
        panelTransform.anchoredPosition = new Vector2(GetTargetPanelX(), 0f);

        Image panelBackground = panelObject.AddComponent<Image>();
        panelBackground.color = new Color(0.08f, 0.08f, 0.11f, 0.94f);

        VerticalLayoutGroup layoutGroup = panelObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(28, 28, 28, 28);
        layoutGroup.spacing = 18f;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;

        CreateText("Title", panelObject.transform, "Settings", 28, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        CreateText("Hint", panelObject.transform, "Press Esc to close", 16, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.8f, 0.84f, 0.9f, 0.95f));

        sensitivityValueLabel = CreateSliderRow(
            panelObject.transform,
            "Sensitivity",
            sensitivity,
            PlayerLook.MinSensitivity,
            PlayerLook.MaxSensitivity,
            SetSensitivity,
            FormatSensitivity,
            out sensitivitySlider);
        masterValueLabel = CreateSliderRow(panelObject.transform, "Master", masterVolume, 0f, 1f, SetMasterVolume, FormatPercent, out masterSlider);
        musicValueLabel = CreateSliderRow(panelObject.transform, "Music", musicVolume, 0f, 1f, SetMusicVolume, FormatPercent, out musicSlider);
        sfxValueLabel = CreateSliderRow(panelObject.transform, "SFX", sfxVolume, 0f, 1f, SetSfxVolume, FormatPercent, out sfxSlider);

        LayoutElement spacer = new GameObject("Spacer").AddComponent<LayoutElement>();
        spacer.transform.SetParent(panelObject.transform, false);
        spacer.flexibleHeight = 1f;

        CreateText("Footer", panelObject.transform, "Changes save automatically.", 15, FontStyle.Italic, TextAnchor.LowerLeft, new Color(0.8f, 0.84f, 0.9f, 0.85f));

        SyncUiWithSettings();
    }

    private Text CreateSliderRow(
        Transform parent,
        string label,
        float initialValue,
        float minValue,
        float maxValue,
        UnityEngine.Events.UnityAction<float> onValueChanged,
        System.Func<float, string> formatValue,
        out Slider slider)
    {
        GameObject rowObject = new GameObject(label + "Row");
        rowObject.transform.SetParent(parent, false);

        VerticalLayoutGroup rowLayout = rowObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = true;

        LayoutElement rowLayoutElement = rowObject.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 72f;

        GameObject labelRowObject = new GameObject(label + "Header");
        labelRowObject.transform.SetParent(rowObject.transform, false);

        HorizontalLayoutGroup headerLayout = labelRowObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 12f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = false;

        CreateText("Label", labelRowObject.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Text valueLabel = CreateText("Value", labelRowObject.transform, formatValue(initialValue), 18, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.75f, 0.93f, 0.82f, 1f));

        GameObject sliderObject = new GameObject(label + "Slider");
        sliderObject.transform.SetParent(rowObject.transform, false);
        slider = sliderObject.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = initialValue;
        slider.direction = Slider.Direction.LeftToRight;

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(0f, 24f);

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.19f, 0.22f, 0.28f, 1f);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.25f);
        backgroundRect.anchorMax = new Vector2(1f, 0.75f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fillAreaObject = new GameObject("Fill Area");
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(12f, 0f);
        fillAreaRect.offsetMax = new Vector2(-12f, 0f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.color = new Color(0.34f, 0.78f, 0.56f, 1f);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleAreaObject = new GameObject("Handle Slide Area");
        handleAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleAreaObject.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handleObject = new GameObject("Handle");
        handleObject.transform.SetParent(handleAreaObject.transform, false);
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 20f);

        slider.targetGraphic = handleImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.onValueChanged.AddListener(onValueChanged);

        return valueLabel;
    }

    private Text CreateText(string objectName, Transform parent, string content, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = fontSize + 8f;

        return text;
    }
}
