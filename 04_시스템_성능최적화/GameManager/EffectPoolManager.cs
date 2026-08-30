using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class EffectPoolManager : MonoBehaviour
{
    public static EffectPoolManager Instance;

    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, Coroutine> autoReturnCoroutines = new Dictionary<GameObject, Coroutine>();
    private HashSet<GameObject> queuedObjects = new HashSet<GameObject>();
    private Transform inactiveSpawnRoot;

    private void Awake()
    {
        Instance = this;

        GameObject inactiveRootObject = new GameObject("__EffectPoolInactiveRoot");
        inactiveRootObject.transform.SetParent(transform, false);
        inactiveRootObject.SetActive(false);
        inactiveSpawnRoot = inactiveRootObject.transform;
    }

    public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation, bool autoReturn = true)
    {
        return GetFromPoolInternal(prefab, position, rotation, autoReturn, true);
    }

    public GameObject GetFromPoolInactive(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return GetFromPoolInternal(prefab, position, rotation, false, false);
    }

    private GameObject GetFromPoolInternal(GameObject prefab, Vector3 position, Quaternion rotation, bool autoReturn, bool activateOnSpawn)
    {
        if (prefab == null) return null;

        if (!poolDictionary.ContainsKey(prefab))
        {
            poolDictionary.Add(prefab, new Queue<GameObject>());
        }

        GameObject obj = null;

        // 이미 파괴된 인스턴스는 큐에서 건너뛴다.
        while (poolDictionary[prefab].Count > 0)
        {
            obj = poolDictionary[prefab].Dequeue();
            queuedObjects.Remove(obj);

            if (obj != null)
                break;

            obj = null;
        }

        if (obj == null)
        {
            if (activateOnSpawn)
            {
                obj = Instantiate(prefab);
            }
            else
            {
                obj = inactiveSpawnRoot != null
                    ? Instantiate(prefab, inactiveSpawnRoot)
                    : Instantiate(prefab);

                if (obj.activeSelf)
                    obj.SetActive(false);

                obj.transform.SetParent(null, false);
            }
        }

        if (autoReturnCoroutines.TryGetValue(obj, out Coroutine pendingCoroutine) && pendingCoroutine != null)
        {
            StopCoroutine(pendingCoroutine);
            autoReturnCoroutines.Remove(obj);
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;

        if (activateOnSpawn)
            obj.SetActive(true);
        else if (obj.activeSelf)
            obj.SetActive(false);

        if (autoReturn)
        {
            Coroutine autoReturnCoroutine = StartCoroutine(ReturnToPoolAfterTime(prefab, obj));
            autoReturnCoroutines[obj] = autoReturnCoroutine;
        }

        return obj;
    }

    private IEnumerator ReturnToPoolAfterTime(GameObject prefab, GameObject obj)
    {
        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        float duration = ps != null ? ps.main.duration + ps.main.startLifetime.constantMax : 3f;

        yield return new WaitForSeconds(duration);

        if (obj == null)
        {
            autoReturnCoroutines.Remove(obj);
            yield break;
        }

        autoReturnCoroutines.Remove(obj);

        obj.SetActive(false);

        if (poolDictionary.ContainsKey(prefab))
        {
            if (!queuedObjects.Contains(obj))
            {
                poolDictionary[prefab].Enqueue(obj);
                queuedObjects.Add(obj);
            }
        }
    }
    public void ReturnToPoolDirect(GameObject prefab, GameObject obj)
    {
        if (obj == null || prefab == null) return;

        if (autoReturnCoroutines.TryGetValue(obj, out Coroutine pendingCoroutine) && pendingCoroutine != null)
        {
            StopCoroutine(pendingCoroutine);
            autoReturnCoroutines.Remove(obj);
        }

        if (obj.activeSelf)
            obj.SetActive(false);

        if (!poolDictionary.ContainsKey(prefab))
            poolDictionary.Add(prefab, new Queue<GameObject>());

        if (poolDictionary.ContainsKey(prefab))
        {
            if (!queuedObjects.Contains(obj))
            {
                poolDictionary[prefab].Enqueue(obj);
                queuedObjects.Add(obj);
            }
        }
    }
}

