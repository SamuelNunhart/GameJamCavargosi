using System.Collections.Generic;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
}

public sealed class GameplayMusicController : MonoBehaviour
{
    private const string ResourceRoot = "Audio/Music/";
    private const string IntroTrackKey = "uvod-scrollovanie";
    private const string GhostRevealTrackKey = "ghost-first";
    private const string WindowMinigameTrackKey = "Ambiance_Forest_Birds_Loop_Stereo";
    private const string KitchenDoorTrackKey = "rodinka-na-city";

    private static GameplayMusicController instance;

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    private AudioSource musicSource;
    private AudioSource oneShotSource;
    private string currentMusicTrackKey;
    private bool currentMusicLoop;
    private string previousMusicTrackKey;
    private bool previousMusicLoop;
    private bool hasPreviousMusicTrack;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void PlayIntroSceneMusic()
    {
        EnsureInstance().PlayMusicTrack(IntroTrackKey, loop: true);
    }

    public static void PlayGhostRevealStinger()
    {
        EnsureInstance().PlayMusicTrack(GhostRevealTrackKey, loop: false);
    }

    public static void PlayWindowMinigameMusic()
    {
        EnsureInstance().PlayTemporaryMusicTrack(WindowMinigameTrackKey, loop: true);
    }

    public static void PlayKitchenDoorMusic()
    {
        EnsureInstance().PlayMusicTrack(KitchenDoorTrackKey, loop: true);
    }

    public static void RestorePreviousMusic()
    {
        EnsureInstance().RestorePreviousMusicTrack();
    }

    private static GameplayMusicController EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject controllerObject = new GameObject(nameof(GameplayMusicController));
        instance = controllerObject.AddComponent<GameplayMusicController>();
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
        EnsureAudioSources();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(musicSource, loop: true);
            AudioSettingsOverlay.RegisterMusicSource(musicSource);
        }

        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(oneShotSource, loop: false);
            AudioSettingsOverlay.RegisterSfxSource(oneShotSource);
        }
    }

    private void PlayMusicTrack(string trackKey, bool loop)
    {
        AudioClip clip = LoadClip(trackKey);
        if (clip == null)
            return;

        EnsureAudioSources();

        if (musicSource.isPlaying && musicSource.clip == clip && musicSource.loop == loop)
            return;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
        currentMusicTrackKey = trackKey;
        currentMusicLoop = loop;
    }

    private void PlayTemporaryMusicTrack(string trackKey, bool loop)
    {
        if (string.IsNullOrWhiteSpace(trackKey))
            return;

        if (!hasPreviousMusicTrack && !string.IsNullOrWhiteSpace(currentMusicTrackKey))
        {
            previousMusicTrackKey = currentMusicTrackKey;
            previousMusicLoop = currentMusicLoop;
            hasPreviousMusicTrack = true;
        }

        PlayMusicTrack(trackKey, loop);
    }

    private void RestorePreviousMusicTrack()
    {
        if (!hasPreviousMusicTrack)
            return;

        string trackKeyToRestore = previousMusicTrackKey;
        bool loopToRestore = previousMusicLoop;
        hasPreviousMusicTrack = false;
        previousMusicTrackKey = null;
        previousMusicLoop = false;

        if (string.IsNullOrWhiteSpace(trackKeyToRestore))
            return;

        PlayMusicTrack(trackKeyToRestore, loopToRestore);
    }

    private void PlayOneShotTrack(string trackKey)
    {
        AudioClip clip = LoadClip(trackKey);
        if (clip == null)
            return;

        EnsureAudioSources();
        oneShotSource.PlayOneShot(clip);
    }

    private AudioClip LoadClip(string trackKey)
    {
        if (string.IsNullOrWhiteSpace(trackKey))
            return null;

        if (clipCache.TryGetValue(trackKey, out AudioClip cachedClip) && cachedClip != null)
            return cachedClip;

        AudioClip loadedClip = Resources.Load<AudioClip>(ResourceRoot + trackKey);
        if (loadedClip == null)
        {
            Debug.LogWarning($"GameplayMusicController could not load AudioClip '{trackKey}' from Resources/{ResourceRoot}.", this);
            return null;
        }

        clipCache[trackKey] = loadedClip;
        return loadedClip;
    }

    private static void ConfigureSource(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
    }
}
