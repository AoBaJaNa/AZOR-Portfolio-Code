using System.Collections.Generic;
using UnityEngine;

public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance;

    [Header("Enemy Prefabs")]
    public GameObject Ghoul;
    public GameObject GhostDog;
    public GameObject GhostSkull;
    public GameObject Nun;
    public GameObject Skull_Warrior;
    public GameObject Pagan;

    [Header("Elite_Enemy Prefabs")]
    public GameObject Ghoul_Elite;
    public GameObject GhostDog_Elite;
    public GameObject GhostSkull_Elite;
    public GameObject Nun_Elite;
    public GameObject Skull_Warrior_Elite;
    public GameObject Pagan_Elite;

    private readonly Dictionary<EnemyType, Queue<GameObject>> poolDictionary = new Dictionary<EnemyType, Queue<GameObject>>();

    private void Awake()
    {
        Instance = this;
    }

    public GameObject GetEnemyPrefab(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Ghoul:
                return Ghoul;
            case EnemyType.Ghoul_Elite:
                return Ghoul_Elite;
            case EnemyType.SkullWarrior:
                return Skull_Warrior;
            case EnemyType.SkullWarrior_Elite:
                return Skull_Warrior_Elite;
            case EnemyType.GhostSkull:
                return GhostSkull;
            case EnemyType.GhostSkull_Elite:
                return GhostSkull_Elite;
            case EnemyType.Pagan:
                return Pagan;
            case EnemyType.Pagan_Elite:
                return Pagan_Elite;
            case EnemyType.Nun:
                return Nun;
            case EnemyType.Nun_Elite:
                return Nun_Elite;
            case EnemyType.GhostDog:
                return GhostDog;
            case EnemyType.GhostDog_Elite:
                return GhostDog_Elite;
            default:
                return null;
        }
    }

    public GameObject SpawnFromPool(EnemyType type)
    {
        return GetFromPool(type, true);
    }

    public GameObject GetFromPoolInactive(EnemyType type)
    {
        return GetFromPool(type, false);
    }

    private GameObject GetFromPool(EnemyType type, bool activateOnSpawn)
    {
        EnsurePool(type);

        Queue<GameObject> pool = poolDictionary[type];
        while (pool.Count > 0)
        {
            GameObject enemyToSpawn = pool.Dequeue();
            // A scene cleanup can destroy an object after it was returned to this pool.
            if (enemyToSpawn == null)
                continue;

            enemyToSpawn.SetActive(activateOnSpawn);
            if (enemyToSpawn != null)
                return enemyToSpawn;
        }

        GameObject enemyPrefab = GetEnemyPrefab(type);
        if (enemyPrefab == null)
        {
            Debug.LogWarning($"EnemyPoolManager: prefab is missing for type {type}.");
            return null;
        }

        GameObject newEnemy = Instantiate(enemyPrefab);
        if (!activateOnSpawn)
            newEnemy.SetActive(false);

        return newEnemy;
    }

    public void ReturnToPool(EnemyType type, GameObject enemy)
    {
        if (enemy == null)
            return;

        EnsurePool(type);
        if (poolDictionary[type].Contains(enemy))
            return;

        enemy.SetActive(false);
        poolDictionary[type].Enqueue(enemy);
    }

    private void EnsurePool(EnemyType type)
    {
        if (!poolDictionary.ContainsKey(type))
            poolDictionary.Add(type, new Queue<GameObject>());
    }
}

