using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class AudioSettingsOverlay : MonoBehaviour
{
    private const string MasterVolumeKey = "AudioSettings.MasterVolume";
    private const string MusicVolumeKey = "AudioSettings.MusicVolume";
    private const string SfxVolumeKey = "AudioSettings.SfxVolume";
    private const float DefaultMasterVolume = 1f;
    private const float DefaultMusicVolume = 0.25f;
    private const float DefaultSfxVolume = 1f;
    private GameObject previousSelection;
    private Font displayFont;
    private readonly List<Selectable> controls = new List<Selectable>();
    private static readonly Color Ink = new Color(0.78f, 0.9f, 0.96f);
    private static readonly Color Glow = new Color(0.55f, 0.94f, 1f);

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
            DialogueUI.Instance?.SetSettingsPaused(false);
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
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
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
        if (sensitivitySlider != null && !Mathf.Approximately(sensitivitySlider.value, SensitivityToSlider(sensitivity)))
            sensitivitySlider.SetValueWithoutNotify(SensitivityToSlider(sensitivity));

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
            return false;

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
        SetVisible(false, instant: true);
        SyncUiWithSettings();
    }

    private void SetVisible(bool visible, bool instant = false)
    {
        bool opening = visible && IsSettingsAllowedInActiveScene();
        if (opening && !isVisible && EventSystem.current != null)
            previousSelection = EventSystem.current.currentSelectedGameObject;
        isVisible = opening;
        DialogueUI.Instance?.SetSettingsPaused(isVisible);
        canvas.enabled = true;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
        if (EventSystem.current != null)
        {
            if (isVisible)
                EventSystem.current.SetSelectedGameObject(sensitivitySlider.gameObject);
            else if (previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            else
                EventSystem.current.SetSelectedGameObject(null);
        }
        UpdateCursorForCurrentState();
        if (instant) SnapPanelToTarget();
    }

    private void AnimatePanel()
    {
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, isVisible ? 1f : 0f, 8f * Time.unscaledDeltaTime);
        if (!isVisible && canvasGroup.alpha == 0f) canvas.enabled = false;
    }

    private void SnapPanelToTarget()
    {
        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvas.enabled = isVisible;
    }

    // Logarithmic travel gives the low end as much precision as the high end.
    private static float SensitivityToSlider(float value)
    {
        return Mathf.Log(value / PlayerLook.MinSensitivity) / Mathf.Log(PlayerLook.MaxSensitivity / PlayerLook.MinSensitivity);
    }

    private void SetSensitivityFromSlider(float value)
    {
        SetSensitivity(PlayerLook.MinSensitivity * Mathf.Pow(PlayerLook.MaxSensitivity / PlayerLook.MinSensitivity, value));
    }

    private void RestoreDefaults()
    {
        SetSensitivity(PlayerLook.DefaultSensitivity);
        SetMasterVolume(DefaultMasterVolume);
        SetMusicVolume(DefaultMusicVolume);
        SetSfxVolume(DefaultSfxVolume);
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
        if (canvas != null) return;
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        displayFont = Resources.Load<Font>("UI/IMFeENrm28P") ?? uiFont;
        RectTransform root = Rect("Settings", transform);
        canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.interactable = canvasGroup.blocksRaycasts = false;

        RectTransform veil = Rect("Backdrop", root);
        veil.anchorMin = Vector2.zero;
        veil.anchorMax = Vector2.one;
        veil.offsetMin = veil.offsetMax = Vector2.zero;
        veil.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.035f, 0.06f, 0.76f);

        panelTransform = Rect("Settings content", root);
        panelTransform.sizeDelta = new Vector2(620, 850);
        panelTransform.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.055f, 0.085f, 0.94f);
        TextAt("Settings", panelTransform, 42, 32, 536, 60, 46, true);
        TextAt("Make yourself comfortable.", panelTransform, 42, 98, 536, 32, 19);
        TextAt("Controls", panelTransform, 42, 155, 536, 38, 28, true);
        sensitivityValueLabel = SliderRow("Mouse sensitivity", 202, SensitivityToSlider(sensitivity), SetSensitivityFromSlider, out sensitivitySlider);
        TextAt("Sound", panelTransform, 42, 316, 536, 38, 28, true);
        masterValueLabel = SliderRow("Master volume", 365, masterVolume, SetMasterVolume, out masterSlider);
        musicValueLabel = SliderRow("Music", 465, musicVolume, SetMusicVolume, out musicSlider);
        sfxValueLabel = SliderRow("Effects & voices", 565, sfxVolume, SetSfxVolume, out sfxSlider);
        ButtonAt("Restore defaults", 42, 690, 300, RestoreDefaults);
        ButtonAt("Close", 420, 690, 158, () => SetVisible(false));
        TextAt("Saved automatically  /  Esc to close", panelTransform, 42, 776, 536, 32, 18);
        for (int i = 0; i < controls.Count; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = controls[(i + controls.Count - 1) % controls.Count];
            nav.selectOnDown = controls[(i + 1) % controls.Count];
            controls[i].navigation = nav;
        }
        SyncUiWithSettings();
        canvas.enabled = false;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private Text TextAt(string content, Transform parent, float x, float y, float width, float height, int size, bool display = false)
    {
        RectTransform rect = Rect(content, parent);
        Place(rect, x, y, width, height);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = display ? displayFont : uiFont;
        text.text = content;
        text.fontSize = size;
        text.color = Ink;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.015f, 0.035f, 0.065f, 1);
        shadow.effectDistance = new Vector2(1, -2);
        return text;
    }

    private Text SliderRow(string label, float y, float value, UnityEngine.Events.UnityAction<float> changed, out Slider slider)
    {
        TextAt(label, panelTransform, 42, y, 390, 32, 23);
        Text number = TextAt("", panelTransform, 432, y, 146, 32, 22);
        number.alignment = TextAnchor.MiddleRight;
        RectTransform rect = Rect(label + " slider", panelTransform);
        Place(rect, 42, y + 40, 536, 40);
        // A transparent hit area makes the entire slider easy to click.
        rect.gameObject.AddComponent<Image>().color = Color.clear;
        slider = rect.gameObject.AddComponent<Slider>();
        RectTransform track = Rect("Track", rect);
        Place(track, 10, 18, 516, 4);
        track.gameObject.AddComponent<Image>().color = new Color(0.19f, 0.30f, 0.37f);
        RectTransform area = Rect("Travel", rect);
        Place(area, 10, 0, 516, 40);
        RectTransform fillArea = Rect("Fill travel", rect);
        Place(fillArea, 10, 18, 516, 4);
        RectTransform fill = Rect("Fill", fillArea);
        fill.anchorMin = new Vector2(0, 0);
        fill.anchorMax = new Vector2(1, 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.70f, 0.8f);
        RectTransform handle = Rect("Handle", area);
        handle.sizeDelta = new Vector2(10, 0);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Ink;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.value = value;
        slider.colors = ControlColors();
        slider.onValueChanged.AddListener(changed);
        controls.Add(slider);
        return number;
    }

    private void ButtonAt(string label, float x, float y, float width, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Rect(label, panelTransform);
        Place(rect, x, y, width, 58);
        rect.gameObject.AddComponent<Image>().color = Color.clear;
        Text text = TextAt(label, rect, 0, 0, width, 58, 28, true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = text;
        button.colors = ControlColors();
        button.onClick.AddListener(action);
        controls.Add(button);
    }

    private static ColorBlock ControlColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = Glow;
        colors.selectedColor = Glow;
        colors.pressedColor = new Color(0.35f, 0.73f, 0.85f);
        colors.fadeDuration = 0.12f;
        return colors;
    }
}
