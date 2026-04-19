using System.Collections.Generic;
using System.Collections;
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
    private const float DefaultMusicCrossfadeDuration = 1f;
    private const float GhostRevealCrossfadeDuration = 0.45f;

    private static GameplayMusicController instance;
    private static readonly string[] PreloadTrackKeys =
    {
        GhostRevealTrackKey,
        WindowMinigameTrackKey,
        KitchenDoorTrackKey
    };

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
    private readonly HashSet<string> preloadedTrackKeys = new HashSet<string>();

    private AudioSource primaryMusicSource;
    private AudioSource secondaryMusicSource;
    private AudioSource oneShotSource;
    private string currentMusicTrackKey;
    private bool currentMusicLoop;
    private string previousMusicTrackKey;
    private bool previousMusicLoop;
    private bool hasPreviousMusicTrack;
    private Coroutine preloadRoutine;
    private Coroutine musicTransitionRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void PlayIntroSceneMusic()
    {
        EnsureInstance().PlayMusicTrack(IntroTrackKey, loop: true, DefaultMusicCrossfadeDuration);
    }

    public static void PlayGhostRevealStinger()
    {
        EnsureInstance().PlayMusicTrack(GhostRevealTrackKey, loop: false, GhostRevealCrossfadeDuration);
    }

    public static void PlayWindowMinigameMusic()
    {
        EnsureInstance().PlayTemporaryMusicTrack(WindowMinigameTrackKey, loop: true, DefaultMusicCrossfadeDuration);
    }

    public static void PlayKitchenDoorMusic()
    {
        EnsureInstance().PlayMusicTrack(KitchenDoorTrackKey, loop: true, DefaultMusicCrossfadeDuration);
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
        StartPreloadingTracks();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void EnsureAudioSources()
    {
        if (primaryMusicSource == null)
        {
            primaryMusicSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(primaryMusicSource, loop: true);
            AudioSettingsOverlay.RegisterMusicSource(primaryMusicSource);
        }

        if (secondaryMusicSource == null)
        {
            secondaryMusicSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(secondaryMusicSource, loop: true);
            AudioSettingsOverlay.RegisterMusicSource(secondaryMusicSource);
            secondaryMusicSource.volume = 0f;
        }

        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(oneShotSource, loop: false);
            AudioSettingsOverlay.RegisterSfxSource(oneShotSource);
        }
    }

    private void PlayMusicTrack(string trackKey, bool loop, float crossfadeDuration)
    {
        AudioClip clip = LoadClip(trackKey);
        if (clip == null)
            return;

        EnsureAudioSources();

        AudioSource activeSource = GetPreferredActiveMusicSource();
        if (activeSource != null &&
            activeSource.isPlaying &&
            activeSource.clip == clip &&
            activeSource.loop == loop)
        {
            return;
        }

        currentMusicTrackKey = trackKey;
        currentMusicLoop = loop;

        if (musicTransitionRoutine != null)
        {
            StopCoroutine(musicTransitionRoutine);
            musicTransitionRoutine = null;
        }

        AudioSource outgoingSource = GetPreferredActiveMusicSource();
        AudioSource incomingSource = GetInactiveMusicSource(outgoingSource);

        if (outgoingSource == null || !outgoingSource.isPlaying || outgoingSource.clip == null)
        {
            PlayImmediately(incomingSource, clip, loop);
            return;
        }

        musicTransitionRoutine = StartCoroutine(CrossfadeMusicRoutine(
            outgoingSource,
            incomingSource,
            clip,
            loop,
            crossfadeDuration
        ));
    }

    private void StartPreloadingTracks()
    {
        if (preloadRoutine != null)
            return;

        preloadRoutine = StartCoroutine(PreloadTracksAsync());
    }

    private IEnumerator PreloadTracksAsync()
    {
        for (int i = 0; i < PreloadTrackKeys.Length; i++)
        {
            yield return PreloadTrackAsync(PreloadTrackKeys[i]);
        }

        preloadRoutine = null;
    }

    private IEnumerator PreloadTrackAsync(string trackKey)
    {
        if (string.IsNullOrWhiteSpace(trackKey) || preloadedTrackKeys.Contains(trackKey))
            yield break;

        if (!clipCache.TryGetValue(trackKey, out AudioClip clip) || clip == null)
        {
            ResourceRequest request = Resources.LoadAsync<AudioClip>(ResourceRoot + trackKey);
            yield return request;
            clip = request.asset as AudioClip;

            if (clip == null)
            {
                Debug.LogWarning($"GameplayMusicController could not preload AudioClip '{trackKey}' from Resources/{ResourceRoot}.", this);
                yield break;
            }

            clipCache[trackKey] = clip;
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();

            while (clip != null && clip.loadState == AudioDataLoadState.Loading)
                yield return null;
        }

        preloadedTrackKeys.Add(trackKey);
    }

    private void PlayTemporaryMusicTrack(string trackKey, bool loop, float crossfadeDuration)
    {
        if (string.IsNullOrWhiteSpace(trackKey))
            return;

        if (!hasPreviousMusicTrack && !string.IsNullOrWhiteSpace(currentMusicTrackKey))
        {
            previousMusicTrackKey = currentMusicTrackKey;
            previousMusicLoop = currentMusicLoop;
            hasPreviousMusicTrack = true;
        }

        PlayMusicTrack(trackKey, loop, crossfadeDuration);
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

        PlayMusicTrack(trackKeyToRestore, loopToRestore, DefaultMusicCrossfadeDuration);
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

        if (loadedClip.loadState == AudioDataLoadState.Loaded)
            preloadedTrackKeys.Add(trackKey);

        return loadedClip;
    }

    private IEnumerator CrossfadeMusicRoutine(
        AudioSource outgoingSource,
        AudioSource incomingSource,
        AudioClip incomingClip,
        bool incomingLoop,
        float crossfadeDuration)
    {
        if (incomingSource == null)
        {
            PlayImmediately(outgoingSource, incomingClip, incomingLoop);
            yield break;
        }

        incomingSource.Stop();
        incomingSource.clip = incomingClip;
        incomingSource.loop = incomingLoop;
        incomingSource.volume = 0f;
        incomingSource.Play();

        float targetIncomingVolume = GetTargetMusicVolume();
        float outgoingStartVolume = outgoingSource != null ? Mathf.Clamp01(outgoingSource.volume) : 0f;
        float duration = Mathf.Max(0f, crossfadeDuration);

        if (duration <= 0f)
        {
            FinishCrossfade(outgoingSource, incomingSource, targetIncomingVolume);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (outgoingSource != null)
                outgoingSource.volume = Mathf.Lerp(outgoingStartVolume, 0f, t);

            incomingSource.volume = Mathf.Lerp(0f, targetIncomingVolume, t);
            yield return null;
        }

        FinishCrossfade(outgoingSource, incomingSource, targetIncomingVolume);
    }

    private void FinishCrossfade(AudioSource outgoingSource, AudioSource incomingSource, float incomingTargetVolume)
    {
        if (outgoingSource != null)
        {
            outgoingSource.Stop();
            outgoingSource.clip = null;
            outgoingSource.volume = 0f;
        }

        if (incomingSource != null)
            incomingSource.volume = incomingTargetVolume;

        musicTransitionRoutine = null;
    }

    private void PlayImmediately(AudioSource source, AudioClip clip, bool loop)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = clip;
        source.loop = loop;
        source.volume = GetTargetMusicVolume();
        source.Play();

        AudioSource otherSource = GetInactiveMusicSource(source);
        if (otherSource != null)
        {
            otherSource.Stop();
            otherSource.clip = null;
            otherSource.volume = 0f;
        }
    }

    private AudioSource GetPreferredActiveMusicSource()
    {
        EnsureAudioSources();

        bool primaryPlaying = primaryMusicSource != null && primaryMusicSource.isPlaying && primaryMusicSource.clip != null;
        bool secondaryPlaying = secondaryMusicSource != null && secondaryMusicSource.isPlaying && secondaryMusicSource.clip != null;

        if (primaryPlaying && secondaryPlaying)
            return primaryMusicSource.volume >= secondaryMusicSource.volume ? primaryMusicSource : secondaryMusicSource;

        if (primaryPlaying)
            return primaryMusicSource;

        if (secondaryPlaying)
            return secondaryMusicSource;

        return primaryMusicSource;
    }

    private AudioSource GetInactiveMusicSource(AudioSource activeSource)
    {
        EnsureAudioSources();

        if (activeSource == primaryMusicSource)
            return secondaryMusicSource;

        if (activeSource == secondaryMusicSource)
            return primaryMusicSource;

        return primaryMusicSource;
    }

    private static float GetTargetMusicVolume()
    {
        return Mathf.Clamp01(AudioSettingsOverlay.MusicChannelVolume);
    }

    private static void ConfigureSource(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
    }
}
