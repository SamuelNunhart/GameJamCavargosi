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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void PlayIntroSceneMusic()
    {
        EnsureInstance().PlayLoopingTrack(IntroTrackKey);
    }

    public static void PlayGhostRevealStinger()
    {
        EnsureInstance().PlayOneShotTrack(GhostRevealTrackKey);
    }

    public static void PlayWindowMinigameMusic()
    {
        EnsureInstance().PlayLoopingTrack(WindowMinigameTrackKey);
    }

    public static void PlayKitchenDoorMusic()
    {
        EnsureInstance().PlayLoopingTrack(KitchenDoorTrackKey);
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
        }

        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(oneShotSource, loop: false);
        }
    }

    private void PlayLoopingTrack(string trackKey)
    {
        AudioClip clip = LoadClip(trackKey);
        if (clip == null)
            return;

        EnsureAudioSources();

        if (musicSource.isPlaying && musicSource.clip == clip)
            return;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
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
