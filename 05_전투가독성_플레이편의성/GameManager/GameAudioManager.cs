using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class GameAudioManager : MonoBehaviour
{
    private const string SoundLabel = "SoundLabel";

    private readonly Dictionary<string, SoundAsset> soundDict = new Dictionary<string, SoundAsset>();
    private readonly Dictionary<string, float> lastPlayTime = new Dictionary<string, float>();
    private readonly Queue<string> pendingOneShots = new Queue<string>();

    private AsyncOperationHandle initializeHandle;
    private AsyncOperationHandle<IList<SoundAsset>> soundLoadHandle;
    private AudioSource uiSFXSource;
    private AudioSource commonSFXSource;
    private AudioSource bgmSource;

    private bool isInitialized;
    private bool isInitializing;
    private bool ownsSoundLoadHandle;
    private string currentBGMKey;
    private string pendingBGMKey;

    public static GameAudioManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject root = new GameObject(nameof(GameAudioManager));
        DontDestroyOnLoad(root);
        root.AddComponent<GameAudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        uiSFXSource = CreateAudioSource("UI SFX", false);
        commonSFXSource = CreateAudioSource("Common SFX", false);
        bgmSource = CreateAudioSource("BGM", true);

        BeginInitialization();
    }

    private void Start()
    {
        ApplyVolumes();
        SettingManager.OnChangeVolume += ApplyVolumes;
    }

    private void OnDestroy()
    {
        SettingManager.OnChangeVolume -= ApplyVolumes;

        if (Instance == this)
            Instance = null;

        if (ownsSoundLoadHandle && soundLoadHandle.IsValid())
            Addressables.Release(soundLoadHandle);

        if (initializeHandle.IsValid())
            Addressables.Release(initializeHandle);
    }

    private AudioSource CreateAudioSource(string childName, bool loop)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private void BeginInitialization()
    {
        if (isInitialized || isInitializing)
            return;

        StartCoroutine(InitializeCoroutine());
    }

    private IEnumerator InitializeCoroutine()
    {
        isInitializing = true;

        GameSession session = GameSession.Instance;
        IReadOnlyDictionary<string, SoundAsset> sessionSoundMap = session.GetSoundAssetMap();
        if (sessionSoundMap != null && sessionSoundMap.Count > 0)
        {
            BuildSoundCache(sessionSoundMap.Values);
            CompleteInitialization();
            yield break;
        }

        initializeHandle = Addressables.InitializeAsync(false);
        yield return initializeHandle;

        soundLoadHandle = Addressables.LoadAssetsAsync<SoundAsset>(SoundLabel, null);
        ownsSoundLoadHandle = true;
        yield return soundLoadHandle;

        if (soundLoadHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("[GameAudioManager] Failed to load SoundLabel assets.");
            isInitializing = false;
            yield break;
        }

        BuildSoundCache(soundLoadHandle.Result);
        CompleteInitialization();
    }

    private void CompleteInitialization()
    {
        isInitialized = true;
        isInitializing = false;
        ApplyVolumes();
        FlushPendingRequests();
    }

    private void BuildSoundCache(IEnumerable<SoundAsset> sounds)
    {
        soundDict.Clear();
        lastPlayTime.Clear();

        foreach (SoundAsset sound in sounds)
        {
            if (sound == null)
                continue;

            string key = sound.KeyName;
            soundDict[key] = sound;
            lastPlayTime[key] = -999f;
            sound.PreloadSound();
        }
    }

    private static GameAudioManager EnsureInstance()
    {
        if (Instance == null)
            Bootstrap();

        return Instance;
    }

    public static void RefreshVolumes()
    {
        EnsureInstance().ApplyVolumes();
    }

    public static void PlayUI(string keyName)
    {
        EnsureInstance().PlayQueued(keyName);
    }

    public static void PlayCommon(string keyName)
    {
        EnsureInstance().PlayQueued(keyName);
    }

    public static void PlayBGM(string keyName)
    {
        EnsureInstance().PlayBGMQueued(keyName);
    }

    public static void PlayBGMClip(AudioClip clip, string bgmKey = null)
    {
        EnsureInstance().PlayBGMClipInternal(clip, bgmKey);
    }

    public static void StopBGM()
    {
        EnsureInstance().StopBGMInternal();
    }

    private void PlayQueued(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return;

        BeginInitialization();

        if (!isInitialized)
        {
            pendingOneShots.Enqueue(keyName);
            return;
        }

        PlayInternal(keyName);
    }

    private void PlayBGMQueued(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return;

        BeginInitialization();

        if (!isInitialized)
        {
            pendingBGMKey = keyName;
            return;
        }

        PlayBGMInternal(keyName);
    }

    private void FlushPendingRequests()
    {
        while (pendingOneShots.Count > 0)
        {
            PlayInternal(pendingOneShots.Dequeue());
        }

        if (!string.IsNullOrWhiteSpace(pendingBGMKey))
        {
            string queuedKey = pendingBGMKey;
            pendingBGMKey = null;
            PlayBGMInternal(queuedKey);
        }
    }

    private void PlayInternal(string keyName)
    {
        if (!TryGetPlayableSound(keyName, out SoundAsset sound))
            return;

        if (!sound.allowOverlap && Time.time - lastPlayTime[keyName] < sound.Cooldown)
            return;

        lastPlayTime[keyName] = Time.time;

        switch (sound.category)
        {
            case Category.Skill:
                commonSFXSource.PlayOneShot(sound.clip);
                break;

            case Category.BGM:
                PlayBGMInternal(keyName);
                break;

            default:
                uiSFXSource.PlayOneShot(sound.clip);
                break;
        }
    }

    private void PlayBGMInternal(string keyName)
    {
        if (!TryGetPlayableSound(keyName, out SoundAsset sound))
            return;

        PlayBGMClipInternal(sound.clip, keyName);
    }

    private void PlayBGMClipInternal(AudioClip clip, string bgmKey = null)
    {
        if (clip == null)
            return;

        string resolvedKey = string.IsNullOrWhiteSpace(bgmKey) ? clip.name : bgmKey;
        if (currentBGMKey == resolvedKey && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;
        bgmSource.Play();
        currentBGMKey = resolvedKey;
        ApplyVolumes();
    }

    private void StopBGMInternal()
    {
        pendingBGMKey = null;
        currentBGMKey = null;

        if (bgmSource == null)
            return;

        bgmSource.Stop();
        bgmSource.clip = null;
    }

    private bool TryGetPlayableSound(string keyName, out SoundAsset sound)
    {
        if (!soundDict.TryGetValue(keyName, out sound) || sound == null || sound.clip == null)
        {
            Debug.LogWarning($"[GameAudioManager] Sound missing: {keyName}");
            return false;
        }

        return true;
    }

    private void ApplyVolumes()
    {
        UserSetting settings = GameSession.Instance != null ? GameSession.Instance.Settings : null;

        float normalVolume = settings != null ? settings.NormalSFX : 1f;
        float skillVolume = settings != null ? settings.SkillSFX : 1f;
        float bgmVolume = settings != null ? settings.BGM : 1f;

        if (uiSFXSource != null)
            uiSFXSource.volume = normalVolume;

        if (commonSFXSource != null)
            commonSFXSource.volume = skillVolume;

        if (bgmSource != null)
            bgmSource.volume = bgmVolume;
    }
}

