using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum Category { NormalSFX, Skill, BGM };

[CreateAssetMenu(menuName = "Sound/Sound Data", fileName = "NewSoundData")]
public class SoundAsset : ScriptableObject
{
    [Header("Basic Settings")]
    [Tooltip("Sound key used by PlaySound().")]
    [SerializeField, HideInInspector]
    internal string keyName;

    [Tooltip("Addressable sound clip")]
    public AssetReference soundClip;
    public AudioClip clip;
    public SoundAsset Active_SoundClip;
    public Category category;

    [Header("Play Options")]
    public bool allowOverlap = true;

    [System.NonSerialized]
    private AsyncOperationHandle<AudioClip> clipLoadHandle;

    [System.NonSerialized]
    private Task<AudioClip> clipLoadTask;

    public float Cooldown
    {
        get
        {
            if (!allowOverlap && clip != null)
                return clip.length;

            return 0;
        }
    }

    public string KeyName => string.IsNullOrEmpty(keyName) ? name : keyName;

    public async void PreloadSound()
    {
        await LoadClipAsync();

        if (Active_SoundClip != null)
            Active_SoundClip.PreloadSound();
    }

    public Task<AudioClip> LoadClipAsync()
    {
        if (clip != null)
            return Task.FromResult(clip);

        if (clipLoadTask != null)
            return clipLoadTask;

        clipLoadTask = LoadClipInternalAsync();
        return clipLoadTask;
    }

    private async Task<AudioClip> LoadClipInternalAsync()
    {
        if (soundClip != null && soundClip.RuntimeKeyIsValid())
        {
            clipLoadHandle = Addressables.LoadAssetAsync<AudioClip>(soundClip);
            await clipLoadHandle.Task;

            if (clipLoadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                clip = clipLoadHandle.Result;
            }
            else
            {
                Debug.LogWarning($"[SoundAsset] Failed to load sound clip: {KeyName}");
            }
        }

        return clip;
    }

    public void ReleaseSound()
    {
        if (clipLoadHandle.IsValid())
        {
            Addressables.Release(clipLoadHandle);
            clip = null;
        }

        clipLoadTask = null;

        if (Active_SoundClip != null)
            Active_SoundClip.ReleaseSound();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        keyName = name;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

