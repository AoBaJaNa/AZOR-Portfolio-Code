using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public enum EnemySoundType
{
    Attack,
    Hit,
    Die,
    Skill,
    Footstep
}

[System.Serializable]
public class EnemySoundData
{
    public EnemySoundType type;
    public AudioClip clip;
    public bool allowOverlap = true;
    public float cooldown = 0.2f;

    public int maxSimultaneous = 3;
}

[System.Serializable]
public class EnemySoundSet
{
    public EnemyType enemyType;
    public List<EnemySoundData> soundList;
}


public class GlobalEnemySoundManager : MonoBehaviour
{
    public static GlobalEnemySoundManager Instance;

    public List<EnemySoundSet> enemySoundSets;

    private Dictionary<EnemyType, Dictionary<EnemySoundType, EnemySoundData>> soundDict;
    private Dictionary<EnemyType, Dictionary<EnemySoundType, float>> lastPlayTime;

    // Pool for each EnemySoundType
    private Dictionary<EnemySoundType, List<AudioSource>> audioPools =
        new Dictionary<EnemySoundType, List<AudioSource>>();

    // Tracking play start time to find the oldest audio
    private Dictionary<AudioSource, float> playStartTime =
        new Dictionary<AudioSource, float>();

    private void Awake()
    {
        Instance = this;

        soundDict = new Dictionary<EnemyType, Dictionary<EnemySoundType, EnemySoundData>>();
        lastPlayTime = new Dictionary<EnemyType, Dictionary<EnemySoundType, float>>();

        // Build dictionaries
        foreach (var set in enemySoundSets)
        {
            var innerDict = new Dictionary<EnemySoundType, EnemySoundData>();
            var timeDict = new Dictionary<EnemySoundType, float>();

            foreach (var s in set.soundList)
            {
                innerDict[s.type] = s;
                timeDict[s.type] = -999f;

                // Prepare audio pool per type
                if (!audioPools.ContainsKey(s.type))
                {
                    audioPools[s.type] = new List<AudioSource>();
                    for (int i = 0; i < s.maxSimultaneous; i++)
                    {
                        var src = gameObject.AddComponent<AudioSource>();
                        src.playOnAwake = false;
                        audioPools[s.type].Add(src);
                        playStartTime[src] = -999f;
                    }
                }
            }

            soundDict[set.enemyType] = innerDict;
            lastPlayTime[set.enemyType] = timeDict;
        }
    }

    public void PlaySound(EnemyType enemyType, EnemySoundType soundType)
    {
        if (!soundDict.ContainsKey(enemyType))
            return;
        if (!soundDict[enemyType].ContainsKey(soundType))
            return;

        EnemySoundData data = soundDict[enemyType][soundType];

        // Cooldown check
        if (!data.allowOverlap)
        {
            float lastTime = lastPlayTime[enemyType][soundType];
            if (Time.time - lastTime < data.cooldown)
                return;

            lastPlayTime[enemyType][soundType] = Time.time;
        }

        PlayFromPool(data);
    }

    private void PlayFromPool(EnemySoundData data)
    {
        List<AudioSource> pool = audioPools[data.type];

        AudioSource freeSource = null;

        foreach (var src in pool)
        {
            if (!src.isPlaying)
            {
                freeSource = src;
                break;
            }
        }

        if (freeSource == null)
        {
            float oldestTime = float.MaxValue;
            foreach (var src in pool)
            {
                if (playStartTime[src] < oldestTime)
                {
                    oldestTime = playStartTime[src];
                    freeSource = src;
                }
            }

            // Stop the oldest one
            freeSource.Stop();
        }

        // Play
        freeSource.clip = data.clip;
        freeSource.Play();

        // Update start time
        playStartTime[freeSource] = Time.time;
    }
}


